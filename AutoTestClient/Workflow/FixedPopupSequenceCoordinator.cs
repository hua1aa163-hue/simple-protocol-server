using AutoTestClient.Models;
using AutoTestClient.Monitoring;
using AutoTestClient.Projection;

namespace AutoTestClient.Workflow;

/// <summary>
/// 将 MRTEST 的确认弹窗与预先配置好的图卡队列绑定。
/// <para>只按事件顺序消费图片，不读取或猜测提示文字。</para>
/// </summary>
public sealed class FixedPopupSequenceCoordinator : IAsyncDisposable
{
    private readonly MrTestDialogMonitor _monitor;
    private readonly IImageProjector _projector;
    private readonly Func<string, Task> _log;
    private readonly SemaphoreSlim _dialogLock = new(1, 1);
    private readonly object _gate = new();
    private readonly object _handlerGate = new();
    private readonly HashSet<Task> _activeHandlers = new();
    private readonly List<MrTestDialogDetectedEventArgs> _deferredDialogs = new();
    private Queue<TestStep> _queue = new();
    private TaskCompletionSource<bool>? _completion;
    private CancellationTokenSource? _sequenceCts;
    private bool _autoConfirm = true;
    private ProjectionMode _projectionMode = ProjectionMode.PixelPerfect;
    private TimeSpan _popupTimeout = TimeSpan.FromSeconds(600);
    // Some workflows (notably crosstalk) project the image before issuing the
    // measurement command.  In that mode the popup is only a confirmation
    // gate and must not project the same image a second time.
    private readonly bool _projectOnDialog;
    // Optional gate used by a per-image transaction.  MRTEST can create the
    // confirmation surface a little before the TCP `Run` intermediate reply;
    // callers may ask us to wait for that reply before sending BM_CLICK/Enter.
    // The default is null so the established FOV/contrast/gamut sequence is
    // byte-for-byte compatible with its previous timing.
    private readonly Func<CancellationToken, Task>? _beforeConfirm;
    private bool _disposed;
    // Arm 与监视器轮询在不同线程上运行。准备队列期间先屏蔽事件，
    // 再清空监视器的已见集合，避免首个弹窗在 completion 建立前被消费，
    // 或在清空后因同一个旧事件重复消费一张图卡。
    private bool _arming;
    private DateTime _armEpochUtc;

    public FixedPopupSequenceCoordinator(
        MrTestDialogMonitor monitor,
        IImageProjector projector,
        Func<string, Task>? log = null,
        ProjectionMode projectionMode = ProjectionMode.PixelPerfect,
        TimeSpan? popupTimeout = null,
        bool projectOnDialog = true,
        Func<CancellationToken, Task>? beforeConfirm = null)
    {
        _monitor = monitor;
        _projector = projector;
        _log = log ?? (_ => Task.CompletedTask);
        _projectionMode = projectionMode;
        _popupTimeout = NormalizePopupTimeout(popupTimeout);
        _projectOnDialog = projectOnDialog;
        _beforeConfirm = beforeConfirm;
        _monitor.DialogDetected += Monitor_DialogDetected;
    }

    public Task Completion
    {
        get { lock (_gate) return _completion?.Task ?? Task.CompletedTask; }
    }

    public int RemainingCount { get { lock (_gate) return _queue.Count; } }

    /// <summary>在发送测量命令前调用，之后每个弹窗按队列投影一张图。</summary>
    public void Arm(IReadOnlyList<TestStep> steps, bool autoConfirm, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(steps);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_completion is not null && !_completion.Task.IsCompleted)
                throw new InvalidOperationException("已有弹窗序列正在执行。");
            _arming = true;
            // 在清空监视器之前记录时间，保证 reset 之后刚产生的事件
            // 不会因 finally 中重新取时间而被误过滤。
            _armEpochUtc = DateTime.UtcNow;
            _deferredDialogs.Clear();
            _queue = new Queue<TestStep>(steps.Where(s => !string.IsNullOrWhiteSpace(s.ImagePath)));
            _autoConfirm = autoConfirm;
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _completion = completion;
            _sequenceCts?.Dispose();
            var sequenceCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _sequenceCts = sequenceCts;
            if (_queue.Count == 0) completion.TrySetResult(true);
            else sequenceCts.Token.Register(() => completion.TrySetCanceled(sequenceCts.Token));
        }
        try
        {
            // 任何在上述临界窗口内已经被监视器看到的窗口都会被清除；
            // 弹窗通常会持续到确认，因此下一次轮询会可靠地产生新事件。
            _monitor.ResetSeenDialogs();
        }
        finally
        {
            MrTestDialogDetectedEventArgs[] deferred;
            lock (_gate)
            {
                _arming = false;
                deferred = _deferredDialogs
                    .Where(e => e.DetectedAtUtc >= _armEpochUtc)
                    .GroupBy(e => e.Handle)
                    .Select(group => group.OrderByDescending(e => e.DetectedAtUtc).First())
                    .ToArray();
                _deferredDialogs.Clear();
            }
            foreach (MrTestDialogDetectedEventArgs dialog in deferred)
            {
                // 弹窗可能在事件排队期间已经关闭；不要让过期事件消费
                // 下一张图卡，也不要把关闭过渡态再次投影。
                if (MrTestDialogMonitor.TryGetSnapshot(dialog.Handle, out _))
                    StartDialogHandler(dialog);
            }
        }
    }

    public void Fail(Exception exception)
    {
        lock (_gate) _completion?.TrySetException(exception);
    }

    public void Disarm()
    {
        lock (_gate)
        {
            _queue.Clear();
            _sequenceCts?.Cancel();
            _sequenceCts?.Dispose();
            _sequenceCts = null;
            _completion = null;
            _arming = false;
            _armEpochUtc = DateTime.MinValue;
            _deferredDialogs.Clear();
        }
    }

    private void Monitor_DialogDetected(object? sender, MrTestDialogDetectedEventArgs e)
    {
        lock (_gate)
        {
            if (_disposed) return;
            if (_arming)
            {
                _deferredDialogs.Add(e);
                return;
            }
            if (e.DetectedAtUtc < _armEpochUtc) return;
        }
        StartDialogHandler(e);
    }

    private void StartDialogHandler(MrTestDialogDetectedEventArgs e)
    {
        Task handler = ProcessDialogAsync(e);
        lock (_handlerGate) _activeHandlers.Add(handler);
        _ = handler.ContinueWith(
            completed =>
            {
                lock (_handlerGate) _activeHandlers.Remove(completed);
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private async Task ProcessDialogAsync(MrTestDialogDetectedEventArgs args)
    {
        await _dialogLock.WaitAsync().ConfigureAwait(false);
        try
        {
            TestStep? step;
            CancellationToken token;
            bool autoConfirm;
            lock (_gate)
            {
                if (_disposed || _completion is null || _completion.Task.IsCompleted) return;
                if (_queue.Count == 0)
                {
                    _completion.TrySetException(new InvalidOperationException("MRTEST 弹窗数量超过固定图卡步骤数量。"));
                    return;
                }
                step = _queue.Dequeue();
                token = _sequenceCts?.Token ?? CancellationToken.None;
                autoConfirm = _autoConfirm;
            }

            try
            {
                if (_projectOnDialog)
                {
                    await _log($"检测到 MRTEST 确认弹窗，按固定序列投影：{step.DisplayName}").ConfigureAwait(false);
                    await _projector.ProjectAsync(step.ImagePath, _projectionMode, token).ConfigureAwait(false);
                    await _log($"图卡已切换：{step.DisplayName}（{step.ImagePath}）").ConfigureAwait(false);
                    int delay = step.StabilizeDelayMs;
                    if (delay > 0) await Task.Delay(delay, token).ConfigureAwait(false);
                }
                else
                {
                    // The caller has already projected this image before
                    // sending the command.  Keep the event/confirmation
                    // sequencing, but never replace the image here.
                    await _log($"检测到 MRTEST 确认弹窗：{step.DisplayName}（图卡已在命令前投影）").ConfigureAwait(false);
                }
                if (autoConfirm)
                {
                    if (_beforeConfirm is not null)
                    {
                        await _log($"弹窗已出现，等待当前图卡的 Run 中间返回后确认：{step.DisplayName}")
                            .ConfigureAwait(false);
                        await _beforeConfirm(token).ConfigureAwait(false);
                    }
                    await _monitor.ConfirmOkAsync(args.Handle, token).ConfigureAwait(false);
                    await _log($"已确认弹窗：{step.DisplayName}").ConfigureAwait(false);
                }
                else
                {
                    await _log($"已投影 {step.DisplayName}，等待人工点击 MRTEST 确定。").ConfigureAwait(false);
                    await _monitor.WaitForClosedAsync(args.Handle, _popupTimeout, token).ConfigureAwait(false);
                }

                lock (_gate)
                {
                    if (_queue.Count == 0) _completion?.TrySetResult(true);
                }
            }
            catch (Exception ex)
            {
                lock (_gate) _completion?.TrySetException(ex);
                await _log($"弹窗步骤失败：{ex.Message}").ConfigureAwait(false);
            }
        }
        finally { _dialogLock.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _sequenceCts?.Cancel();
            _deferredDialogs.Clear();
        }
        _monitor.DialogDetected -= Monitor_DialogDetected;
        Task[] pending;
        lock (_handlerGate) pending = _activeHandlers.ToArray();
        if (pending.Length > 0)
        {
            try { await Task.WhenAll(pending).ConfigureAwait(false); }
            catch { /* 每个步骤已经把原因写入日志/Completion。 */ }
        }
        // 不在这里 Dispose SemaphoreSlim：监视器可能已经排队了一个事件，
        // 让其自然结束可避免后台回调在 Release 时访问已释放对象。
        await Task.CompletedTask;
    }

    private static TimeSpan NormalizePopupTimeout(TimeSpan? timeout)
    {
        TimeSpan value = timeout ?? TimeSpan.FromSeconds(600);
        if (value <= TimeSpan.Zero) return TimeSpan.FromSeconds(1);
        return value > TimeSpan.FromSeconds(600) ? TimeSpan.FromSeconds(600) : value;
    }
}
