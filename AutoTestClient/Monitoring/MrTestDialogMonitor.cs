using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace AutoTestClient.Monitoring;

/// <summary>
/// 监视 MRTEST 进程中用于推进固定图卡序列的确认窗口。
/// <para>
/// 采用 Win32 枚举加 250 ms 轮询作为兼容性兜底，不依赖提示文字识别；
/// 固定图片顺序由 <see cref="Workflow.FixedPopupSequenceCoordinator"/> 管理。
/// </para>
/// </summary>
public sealed class MrTestDialogMonitor : IAsyncDisposable
{
    public const string DefaultMrTestExecutablePath =
        @"D:\Program Files\GYTech\Setup_MRTest\MRTest.exe";
    private const uint BmClick = 0x00F5;
    private const uint SmtoAbortIfHung = 0x0002;
    private const uint WmCommand = 0x0111;
    private const uint WmClose = 0x0010;
    private const uint WmSysCommand = 0x0112;
    private const uint WmKeyDown = 0x0100;
    private const uint WmKeyUp = 0x0101;
    private const nint VkReturn = 0x0D;
    private const nint ScClose = 0xF060;
    private const int SwRestore = 9;
    private const uint PmNoRemove = 0;
    private const uint GwOwner = 4;
    private const uint GwEnabledPopup = 6;
    private const uint GaRoot = 2;
    private const uint GaRootOwner = 3;
    private const int GwlStyle = -16;
    private const int GwlExStyle = -20;
    private const ulong WsChild = 0x40000000UL;
    private const ulong WsPopup = 0x80000000UL;
    private const ulong WsExDlgModalFrame = 0x00000001UL;
    private const int IdOk = 1;
    private const int IdMessageBoxOk = 2;
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private static readonly TimeSpan ConfirmationAttemptTimeout = TimeSpan.FromSeconds(5);
    private readonly object _gate = new();
    // 句柄不是弹窗的身份：MRTEST 会在同一个 WinForms 容器中重用窗口。
    // 保存指纹可以在句柄不变但内容/确定按钮改变时重新触发下一张图卡。
    private readonly Dictionary<nint, DialogFingerprint> _reported = new();
    // 需要连续两次轮询得到同一指纹才上报。MRTEST 关闭窗口时偶尔会
    // 先清空标题/子控件再销毁 HWND，稳定采样可避免把这个过渡态当成
    // 下一张图卡的弹窗；正常弹窗只增加一个轮询周期的延迟。
    private readonly Dictionary<nint, CandidateSample> _candidates = new();
    private readonly HashSet<nint> _acknowledged = new();
    // 保存 ConfirmOkAsync 开始时的旧内容。仅用 HWND 集合无法区分“仍是旧
    // 弹窗”与“同一 HWND 已换成下一提示”；后者必须重新发布事件。
    private readonly Dictionary<nint, DialogFingerprint> _acknowledgedBaselines = new();
    // PollLoop 发布事件与 ConfirmOkAsync 的清理阶段可能交错。先把事件放入
    // pending，再由实际发布方原子认领，确保内容切换事件既不丢失也不重复。
    private readonly List<PendingDialogNotification> _pendingNotifications = new();
    // 现场诊断只用于定位真实 MRTEST 的窗口层级；按“类别 + HWND”去重并
    // 限速，避免 250 ms 轮询把同一窗口刷满日志。窗口标题/样式变化会在
    // 冷却期后再次出现，足以观察切图期间的句柄复用。
    private static readonly TimeSpan DiagnosticChangedLogCooldown = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan DiagnosticRepeatLogCooldown = TimeSpan.FromSeconds(30);
    private const int DiagnosticLogEntryLimit = 256;
    private readonly Dictionary<string, DiagnosticLogSample> _diagnosticLastLoggedUtc = new(StringComparer.Ordinal);
    private CancellationTokenSource? _cts;
    private Task? _task;
    private string _executablePath = string.Empty;
    private int _mrTestProcessId;
    private nint _mainWindowHandle;
    private bool _disposed;

    public event EventHandler<MrTestDialogDetectedEventArgs>? DialogDetected;
    public event EventHandler<Exception>? MonitorError;
    /// <summary>监视器动作日志（例如发现窗口、使用 Enter/关闭回退）。</summary>
    public event EventHandler<string>? MonitorLog;

    public bool IsRunning => _task is { IsCompleted: false };
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMilliseconds(250);

    /// <summary>开始新测试序列前清除已报告句柄，支持 MRTEST 重用窗口句柄。</summary>
    public void ResetSeenDialogs()
    {
        lock (_gate)
        {
            _reported.Clear();
            _candidates.Clear();
            _acknowledged.Clear();
            _acknowledgedBaselines.Clear();
            _pendingNotifications.Clear();
        }
    }

    /// <summary>
    /// 将监视器启动瞬间已经存在的弹窗重新排队一次。
    /// <para>
    /// Start 会保留启动前的窗口指纹，避免把旧窗口误当成新测试步骤；
    /// 但 MRTEST 在客户端断开时会留下“服务器退出”框，这类窗口必须先
    /// 交给空闲状态的自动确认处理，否则 MRTEST 不会重新连接。这里只
    /// 清除当前仍可见句柄的已见状态，下一轮轮询仍会执行两次稳定采样，
    /// 不放宽正常弹窗的判定条件。
    /// </para>
    /// </summary>
    public void RequeueCurrentDialogs()
    {
        Dictionary<nint, DialogFingerprint> current = CaptureCurrentDialogs();
        if (current.Count == 0) return;
        lock (_gate)
        {
            foreach (nint handle in current.Keys)
            {
                _reported.Remove(handle);
                _candidates.Remove(handle);
                _acknowledged.Remove(handle);
                _acknowledgedBaselines.Remove(handle);
                _pendingNotifications.RemoveAll(pending => pending.EventArgs.Handle == handle);
            }
        }
    }

    /// <summary>开始监视；路径为空时按进程名 MRTest 兜底。</summary>
    public void Start(string? mrTestExecutablePath)
    {
        string startupMessage;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (IsRunning) return;
            _executablePath = NormalizePath(mrTestExecutablePath);
            _reported.Clear();
            _candidates.Clear();
            _acknowledged.Clear();
            _acknowledgedBaselines.Clear();
            _pendingNotifications.Clear();
            _diagnosticLastLoggedUtc.Clear();
            (_mrTestProcessId, _mainWindowHandle) = FindMrTestIdentity();
            startupMessage = _mrTestProcessId != 0
                ? $"已绑定 MRTEST 进程 PID {_mrTestProcessId}。弹窗监视固定使用该进程。主窗：{DescribeWindow(_mainWindowHandle)}"
                : "启动监视时未找到 MRTEST；将在首次发现时绑定进程，绑定后不自动切换实例。";
            foreach ((nint handle, DialogFingerprint fingerprint) in CaptureCurrentDialogs())
                _reported[handle] = fingerprint;
            // Capture the newly-created source in the task closure.  StopAsync
            // intentionally clears the field before awaiting the old task;
            // reading _cts.Token from that closure could otherwise race with
            // cleanup and throw NullReferenceException during a fast
            // start/stop cycle (for example, a sequential smoke test).
            var cancellation = new CancellationTokenSource();
            _cts = cancellation;
            _task = Task.Run(() => PollLoopAsync(cancellation.Token));
        }
        Raise(MonitorLog, startupMessage);
    }

    public async Task StopAsync()
    {
        Task? task;
        CancellationTokenSource? cancellation;
        lock (_gate)
        {
            cancellation = _cts;
            cancellation?.Cancel();
            task = _task;
            _task = null;
            _cts = null;
            _reported.Clear();
            _candidates.Clear();
            _acknowledged.Clear();
            _acknowledgedBaselines.Clear();
            _pendingNotifications.Clear();
            _diagnosticLastLoggedUtc.Clear();
            _mrTestProcessId = 0;
            _mainWindowHandle = nint.Zero;
        }
        if (task is not null)
        {
            try { await task.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
        cancellation?.Dispose();
    }

    /// <summary>
    /// 重新验证窗口仍属于 MRTEST，并按“按钮 -> Enter -> 关闭”的顺序推进。
    /// </summary>
    public async Task ConfirmOkAsync(nint dialogHandle, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryGetSnapshot(dialogHandle, out MrTestDialogSnapshot? snapshot))
            throw new InvalidOperationException("MRTEST 弹窗已关闭。");

        if (!IsBoundMrTestProcess(snapshot!.ProcessId))
            throw new InvalidOperationException("自动确认已拒绝：弹窗不属于设置路径中的 MRTEST 进程。");
        if (IsMainWindow(snapshot.Handle, snapshot.ProcessId, _mainWindowHandle))
            throw new InvalidOperationException("自动确认已拒绝：目标窗口是 MRTEST 主窗口。");

        DialogFingerprint before = CreateFingerprint(snapshot);
        // 在 BM_CLICK/Enter/WM_CLOSE 的异步处理期间，MRTEST 可能仍让原句柄
        // 保持可见。先标记为已确认，轮询会抑制同一指纹的重复事件；若句柄
        // 消失或内容切换，指纹变化会自动重新入队。
        lock (_gate)
        {
            _acknowledged.Add(dialogHandle);
            _acknowledgedBaselines[dialogHandle] = before;
        }
        bool actionSent = false;
        try
        {
            if (TryClickButton(snapshot!.OkButtonHandle))
            {
                Raise(MonitorLog, $"已向 MRTEST 弹窗发送 BM_CLICK（句柄 0x{snapshot.OkButtonHandle.ToInt64():X}）。");
                actionSent = true;
            }
            if (!await WaitForDialogTransitionAsync(snapshot.Handle, before, ConfirmationAttemptTimeout, cancellationToken).ConfigureAwait(false) &&
                TryPostDialogCommand(snapshot.Handle, snapshot.OkButtonHandle))
            {
                Raise(MonitorLog, "已向 MRTEST 弹窗发送 IDOK/Enter（兼容无按钮句柄窗口）。");
                actionSent = true;
            }
            if (!await WaitForDialogTransitionAsync(snapshot.Handle, before, ConfirmationAttemptTimeout, cancellationToken).ConfigureAwait(false) &&
                TrySendForegroundEnter(snapshot.Handle))
            {
                // 某些 MRTEST 版本的 #32770 窗口由 DirectUI/自绘内容组成，
                // EnumChildWindows 看不到 Button 子句柄，且只向窗口投递
                // WM_KEYDOWN 不会进入其模态消息循环。把窗口置前后发送
                // 一次真实 Enter，等价于操作员按回车，是最后的输入回退。
                Raise(MonitorLog, "已将 MRTEST 弹窗置前并发送真实 Enter。");
                actionSent = true;
            }
            if (!await WaitForDialogTransitionAsync(snapshot.Handle, before, ConfirmationAttemptTimeout, cancellationToken).ConfigureAwait(false) &&
                TryCloseDialog(snapshot.Handle, snapshot.ProcessId))
            {
                // 某些 MRTEST 版本的确认框是自绘窗口，既没有 Button 子类也不
                // 响应 BM_CLICK；WM_CLOSE 是最后的兼容回退，避免流程永久卡住。
                Raise(MonitorLog, "MRTEST 弹窗未暴露可点击控件，已发送 WM_CLOSE 回退。");
                actionSent = true;
            }
            bool transitioned = await WaitForDialogTransitionAsync(
                snapshot.Handle, before, ConfirmationAttemptTimeout, cancellationToken).ConfigureAwait(false);
            if (!actionSent || !transitioned)
                throw new InvalidOperationException("已发送确认操作，但 MRTEST 弹窗未关闭；请检查 MRTEST 是否响应 Enter/确定。");
        }
        catch
        {
            // 失败时允许下一轮重新尝试同一个窗口，而不是把它永久卡在
            // acknowledged 集合中。
            lock (_gate)
            {
                _acknowledged.Remove(dialogHandle);
                _acknowledgedBaselines.Remove(dialogHandle);
            }
            throw;
        }
        // 让同一 HWND 的下一次弹窗重新进入队列；WaitForDialogTransitionAsync
        // 已确认旧窗口消失或内容发生变化，不会在消息队列尚未处理时重复触发。
        List<MrTestDialogDetectedEventArgs> replayNotifications = new();
        lock (_gate)
        {
            // PollLoop may have observed the next prompt while this
            // confirmation was waiting for a reused HWND to settle.  Also
            // keep the old fingerprint while a WM_CLOSE is still in flight;
            // removing it immediately would make the still-visible closing
            // surface fire a duplicate event on the next poll.
            if (TryGetSnapshot(dialogHandle, out MrTestDialogSnapshot? current) && current is not null)
            {
                DialogFingerprint currentFingerprint = CreateFingerprint(current);
                bool pollAlreadyReportedNewPrompt =
                    _reported.TryGetValue(dialogHandle, out DialogFingerprint? reported) &&
                    !reported.Equals(before);
                if (!currentFingerprint.Equals(before) && !pollAlreadyReportedNewPrompt)
                {
                    // The HWND changed content before PollLoop got a chance
                    // to publish it.  Remove the old sample so the next
                    // stable poll emits exactly one event for the new prompt.
                    _reported.Remove(dialogHandle);
                }
                else
                {
                    // Keep the old/current sample while a close is still in
                    // flight, or preserve a prompt already published by the
                    // poll loop to avoid duplicate notifications.
                    _reported[dialogHandle] = currentFingerprint;
                }
            }
            else
                _reported.Remove(dialogHandle);
            _acknowledged.Remove(dialogHandle);
            _acknowledgedBaselines.Remove(dialogHandle);

            // If PollLoop observed a new prompt while this confirmation was
            // in flight, its event may still be waiting for publication. Move
            // it out under the same lock and replay it after releasing the
            // lock; the publisher uses reference claiming, so this cannot
            // duplicate an event that PollLoop is already dispatching.  A
            // prompt that has already disappeared is stale and must not be
            // handed to the coordinator (which intentionally trusts monitor
            // events for fixed-order sequencing).
            bool replayTargetVisible =
                TryGetSnapshot(dialogHandle, out MrTestDialogSnapshot? replaySnapshot) &&
                replaySnapshot is not null &&
                replaySnapshot.ProcessId == _mrTestProcessId &&
                !IsMainWindow(dialogHandle, replaySnapshot.ProcessId, _mainWindowHandle);
            for (int i = _pendingNotifications.Count - 1; i >= 0; i--)
            {
                PendingDialogNotification pending = _pendingNotifications[i];
                if (pending.EventArgs.Handle == dialogHandle)
                {
                    _pendingNotifications.RemoveAt(i);
                    if (replayTargetVisible && !pending.Claimed)
                    {
                        pending.Claimed = true;
                        replayNotifications.Add(pending.EventArgs);
                    }
                }
            }
        }
        foreach (MrTestDialogDetectedEventArgs notification in replayNotifications)
            Raise(DialogDetected, notification);
    }

    /// <summary>人工确认模式下等待指定弹窗消失。</summary>
    public async Task WaitForClosedAsync(nint dialogHandle, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        DialogFingerprint? before = TryGetSnapshot(dialogHandle, out MrTestDialogSnapshot? initial) && initial is not null
            ? CreateFingerprint(initial)
            : null;
        DialogFingerprint? stableTransition = null;
        int stableSamples = 0;
        DateTime deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsWindow(dialogHandle) || !IsWindowVisible(dialogHandle)) return;
            // MRTEST 某些版本会复用同一个窗口句柄，把旧提示直接换成
            // 下一提示而不经过可观察的隐藏阶段；内容指纹变化等价于旧弹窗
            // 已被人工确认。
            if (before is not null && TryGetSnapshot(dialogHandle, out MrTestDialogSnapshot? current) &&
                current is not null)
            {
                DialogFingerprint fingerprint = CreateFingerprint(current);
                if (HasMeaningfulContentChange(before, fingerprint))
                {
                    if (stableTransition is not null && stableTransition.Equals(fingerprint))
                        stableSamples++;
                    else
                    {
                        stableTransition = fingerprint;
                        stableSamples = 1;
                    }
                    if (stableSamples >= 2) return;
                }
                else
                {
                    stableTransition = null;
                    stableSamples = 0;
                }
            }
            await Task.Delay(GetPollInterval(), cancellationToken).ConfigureAwait(false);
        }
        throw new TimeoutException("等待人工确认 MRTEST 弹窗关闭超时。");
    }

    public static bool TryGetSnapshot(nint dialogHandle, out MrTestDialogSnapshot? snapshot)
    {
        snapshot = null;
        if (dialogHandle == nint.Zero || !IsWindow(dialogHandle) || !IsWindowVisible(dialogHandle)) return false;
        GetWindowThreadProcessId(dialogHandle, out uint processId);
        if (processId == 0) return false;
        string title = GetWindowText(dialogHandle);
        nint ok = FindUniqueOkButton(dialogHandle);
        // 不依赖 #32770/WindowsForms10.Window 类名，也不要求必须有标准
        // Button；自绘 MRTEST 窗口由 ConfirmOkAsync 的 Enter/WM_CLOSE 回退处理。
        snapshot = new MrTestDialogSnapshot(dialogHandle, unchecked((int)processId), title, ok);
        return true;
    }

    private async Task PollLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                Dictionary<nint, DialogFingerprint> current = CaptureCurrentDialogs();

                List<MrTestDialogDetectedEventArgs> notifications = new();
                List<string> logMessages = new();
                List<(string Key, string Message)> diagnosticMessages = new();
                lock (_gate)
                {
                    var currentHandles = new HashSet<nint>();
                    foreach ((nint hwnd, DialogFingerprint fingerprint) in current)
                    {
                        currentHandles.Add(hwnd);
                        bool wasReported = _reported.TryGetValue(hwnd, out DialogFingerprint? previous);
                        // A closing MRTEST surface can remain visible for one
                        // or two polls after it clears its title/controls.
                        // Treating that empty fingerprint as a new prompt
                        // consumes the next image before the real prompt is
                        // shown.  Keep the first genuinely blank/self-drawn
                        // prompt detectable (there is no prior report), but
                        // suppress an empty transition for a known or already
                        // acknowledged HWND.
                        if (string.IsNullOrWhiteSpace(fingerprint.Signature) &&
                            ((wasReported && previous is not null &&
                              !string.IsNullOrWhiteSpace(previous.Signature)) ||
                             _acknowledged.Contains(hwnd)))
                        {
                            _candidates.Remove(hwnd);
                            diagnosticMessages.Add((
                                $"empty-transition:{hwnd.ToInt64():X}",
                                $"忽略已知 MRTEST 窗口的关闭过渡空指纹：{DescribeWindow(hwnd)}"));
                            continue;
                        }
                        CandidateSample sample = _candidates.TryGetValue(hwnd, out CandidateSample? priorSample) &&
                                                  priorSample.Fingerprint.Equals(fingerprint)
                            ? priorSample with { Count = priorSample.Count + 1 }
                            : new CandidateSample(fingerprint, 1);
                        _candidates[hwnd] = sample;
                        if (sample.Count < 2) continue;

                        bool changed = !wasReported || previous is null ||
                                       !previous.Equals(fingerprint);
                        bool acknowledged = _acknowledged.Contains(hwnd);
                        bool changedAfterAcknowledgement = acknowledged &&
                            _acknowledgedBaselines.TryGetValue(hwnd, out DialogFingerprint? acknowledgedBaseline) &&
                            !acknowledgedBaseline.Equals(fingerprint);

                        // The prompt may be replaced on the same HWND while
                        // ConfirmOkAsync is waiting for the old one to settle.
                        // Do not let the acknowledgement flag hide that new
                        // content: it is a real next-step event even if
                        // _reported was already updated by a racing poll.
                        if (changedAfterAcknowledgement)
                        {
                            _acknowledged.Remove(hwnd);
                            _acknowledgedBaselines.Remove(hwnd);
                        }
                        _reported[hwnd] = fingerprint;
                        if (changed || changedAfterAcknowledgement)
                        {
                            logMessages.Add($"发现 MRTEST 新弹窗：0x{hwnd.ToInt64():X}（{fingerprint.Title}）");
                            MrTestDialogDetectedEventArgs notification =
                                new(hwnd, fingerprint.ProcessId, fingerprint.Title);
                            _pendingNotifications.Add(new PendingDialogNotification(notification));
                            notifications.Add(notification);
                        }
                    }
                    foreach (nint hwnd in _candidates.Keys.Where(hwnd => !currentHandles.Contains(hwnd)).ToArray())
                        _candidates.Remove(hwnd);
                    foreach (nint hwnd in _reported.Keys.Where(hwnd => !currentHandles.Contains(hwnd)).ToArray())
                    {
                        _reported.Remove(hwnd);
                        _acknowledged.Remove(hwnd);
                        _acknowledgedBaselines.Remove(hwnd);
                        _pendingNotifications.RemoveAll(pending => pending.EventArgs.Handle == hwnd);
                    }
                }
                foreach (string logMessage in logMessages)
                    Raise(MonitorLog, logMessage);
                foreach ((string key, string message) in diagnosticMessages)
                    EmitDiagnostic(key, message);
                foreach (MrTestDialogDetectedEventArgs notification in notifications)
                {
                    // ConfirmOkAsync may have replayed this event while the
                    // poll loop was between releasing _gate and dispatching
                    // its local notification list.  Claiming by reference
                    // guarantees exactly one delivery in either order.
                    if (TryClaimPending(notification))
                        Raise(DialogDetected, notification);
                }
            }
            catch (Exception ex)
            {
                Raise(MonitorError, ex);
            }
            try { await Task.Delay(GetPollInterval(), cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>
    /// 获取 MRTEST 当前可见的弹窗候选。这里故意不依赖窗口类名或提示文字：
    /// 枚举绑定进程的顶层窗口并排除主窗口；当主窗口被禁用时，再从同一
    /// GUI 线程的活动/焦点/捕获句柄追踪内部模态子窗体。MRTEST 的 WinForms
    /// MessageBox、Form 和自绘确认窗都走同一条路径，不依赖类名或提示文字。
    /// </summary>
    private Dictionary<nint, DialogFingerprint> CaptureCurrentDialogs()
    {
        RefreshMrTestIdentity();
        (int knownProcessId, nint knownMainWindow) = GetCachedIdentity();
        var current = new Dictionary<nint, DialogFingerprint>();

        // 进程身份在 Start/首次发现时绑定；运行期间不再从其他 MRTEST
        // 实例重新选择，避免多个同名进程的窗口互相串用。
        if (knownProcessId == 0)
        {
            EmitDiagnostic("identity:none", "当前没有绑定 MRTEST PID，暂不枚举弹窗。");
            return current;
        }
        if (knownMainWindow == nint.Zero)
            EmitDiagnostic("identity:main-zero", $"已绑定 MRTEST PID {knownProcessId}，但主窗 HWND=0。");

        foreach (nint hwnd in EnumerateTopLevelWindows())
        {
            if (!GetWindowProcessId(hwnd, out int processId) || processId != knownProcessId)
                continue;

            nint processMainWindow = knownMainWindow != nint.Zero
                ? knownMainWindow
                : FindMainWindowHandle(knownProcessId);
            if (!IsMainWindow(hwnd, processId, processMainWindow))
            {
                // 记录同 PID 的所有可见顶层非主窗（包括被过滤的辅助窗），
                // 这样现场可直接看到 class/title/style/owner/parent，判断
                // MRTEST 是否把确认框创建成了独立顶层窗口。
                EmitDiagnostic(
                    $"top-level:{hwnd.ToInt64():X}",
                    $"同 PID 可见非主窗：{DescribeWindow(hwnd)}");
                if (TryGetSnapshot(hwnd, out MrTestDialogSnapshot? snapshot) && snapshot is not null)
                    current[hwnd] = CreateFingerprint(snapshot);
                continue;
            }

            // MRTEST 当前版本通常把确认框作为独立顶层窗口；少数版本
            // 把它作为主窗体内部的模态 WinForms 窗口。两条路径都保留，
            // 不以主窗体 Enabled 状态或窗口类名作为硬条件。
            foreach (nint childPopup in FindModalChildPopups(hwnd))
            {
                EmitDiagnostic(
                    $"child-selected:{childPopup.ToInt64():X}",
                    $"同 PID 可见子窗候选：{DescribeWindow(childPopup)}");
                if (!TryGetSnapshot(childPopup, out MrTestDialogSnapshot? childSnapshot) ||
                    childSnapshot is null)
                    continue;
                current[childPopup] = CreateFingerprint(childSnapshot);
            }
        }

        // 某些 WinForms 模态窗在 EnumWindows 期间短暂不可见，但仍可由
        // GetLastActivePopup 取得；补入同一候选集合并用句柄去重。
        if (knownMainWindow != nint.Zero &&
            IsWindow(knownMainWindow) &&
            GetWindowProcessId(knownMainWindow, out int mainProcessId) &&
            mainProcessId == knownProcessId)
        {
            nint activePopup = GetLastActivePopup(knownMainWindow);
            if (activePopup != knownMainWindow)
                EmitDiagnosticWindowIfPresent("active-popup-fallback", activePopup, knownProcessId);
            if (activePopup != nint.Zero &&
                activePopup != knownMainWindow &&
                IsWindowVisible(activePopup) &&
                GetWindowProcessId(activePopup, out int popupProcessId) &&
                IsMrTestProcess(popupProcessId) &&
                TryGetSnapshot(activePopup, out MrTestDialogSnapshot? popupSnapshot) &&
                popupSnapshot is not null)
            {
                // Do not let this compatibility path bypass the same child
                // filtering used by FindModalChildPopups.  In particular,
                // GetLastActivePopup can transiently expose a normal
                // WS_CHILD page on some WinForms builds.  AddPopupCandidate
                // keeps genuine #32770/modal-frame surfaces while rejecting
                // that ordinary panel; process/PID validation above remains
                // the safety boundary for ownerless top-level popups.
                var fallbackCandidates = new Dictionary<nint, int>();
                AddPopupCandidate(
                    fallbackCandidates,
                    activePopup,
                    knownMainWindow,
                    knownProcessId,
                    450,
                    !IsWindowEnabled(knownMainWindow),
                    stateCandidate: true);
                if (fallbackCandidates.ContainsKey(activePopup))
                    current[activePopup] = CreateFingerprint(popupSnapshot);
            }
        }

        return current;
    }

    private IEnumerable<nint> FindModalChildPopups(nint mainWindow)
    {
        if (mainWindow == nint.Zero || !IsWindow(mainWindow))
            return Array.Empty<nint>();

        int processId;
        if (!GetWindowProcessId(mainWindow, out processId)) return Array.Empty<nint>();
        bool mainDisabled = !IsWindowEnabled(mainWindow);
        var candidates = new Dictionary<nint, int>();

        uint threadId = GetWindowThreadProcessId(mainWindow, out _);
        if (threadId != 0)
        {
            var info = new GuiThreadInfo { CbSize = Marshal.SizeOf<GuiThreadInfo>() };
            if (GetGUIThreadInfo(threadId, ref info))
            {
                // active/focus/capture 是 MRTEST 自绘模态窗最可靠的现场
                // 线索，即使后续样式过滤拒绝它们，也把属性留在日志中。
                if (info.HwndActive != mainWindow)
                    EmitDiagnosticWindowIfPresent("active", info.HwndActive, processId);
                if (info.HwndFocus != mainWindow)
                    EmitDiagnosticWindowIfPresent("focus", info.HwndFocus, processId);
                if (info.HwndCapture != mainWindow)
                    EmitDiagnosticWindowIfPresent("capture", info.HwndCapture, processId);
                // GUIThreadInfo 给出的句柄是“模态状态”候选，但仍需在
                // AddPopupCandidate 中验证窗口样式/owner 链。不能仅凭
                // active/focus 就把当前 Sequential Contrast 等普通面板
                // 当作弹窗。
                AddPopupCandidate(candidates, info.HwndActive, mainWindow, processId, 500, mainDisabled, stateCandidate: true);
                AddPopupCandidate(candidates, info.HwndFocus, mainWindow, processId, 250, mainDisabled, stateCandidate: true);
                AddPopupCandidate(candidates, info.HwndCapture, mainWindow, processId, 200, mainDisabled, stateCandidate: true);
            }
        }

        nint lastActive = GetLastActivePopup(mainWindow);
        if (lastActive != mainWindow)
            EmitDiagnosticWindowIfPresent("last-active", lastActive, processId);
        AddPopupCandidate(candidates, lastActive, mainWindow, processId, 400, mainDisabled, stateCandidate: true);
        // GW_ENABLEDPOPUP 是 Win32 为模态 owner 提供的直接查询，即使
        // EnumWindows/GUIThreadInfo 在切换瞬间还没有更新，也能拿到弹窗。
        nint enabledPopup = GetWindow(mainWindow, GwEnabledPopup);
        if (enabledPopup != mainWindow)
            EmitDiagnosticWindowIfPresent("enabled-popup", enabledPopup, processId);
        AddPopupCandidate(candidates, enabledPopup, mainWindow, processId, 600, mainDisabled, stateCandidate: true);

        // GUI 状态读取不到 active 窗口时，寻找弹窗下仍启用的按钮。
        // 主窗体被禁用后，其普通按钮通常也会禁用，因而不会被选中。
        // 仅在 owner 被禁用的模态场景中使用“启用按钮反推”兜底。
        // 主窗体正常工作时，Sequential Contrast 等测试页本身就可能有
        // 可见按钮；反推会把它们的外层 WS_CHILD 面板误报为弹窗。
        if (mainDisabled)
        {
            var enabledButtons = new List<nint>();
            EnumChildWindows(mainWindow, (hwnd, _) =>
            {
                if (IsWindowVisible(hwnd) && IsWindowEnabled(hwnd) &&
                    IsButtonClass(GetClassName(hwnd)))
                    enabledButtons.Add(hwnd);
                return true;
            }, nint.Zero);
            foreach (nint button in enabledButtons)
                AddPopupCandidate(candidates, button, mainWindow, processId, 100, mainDisabled);
        }

        // 有些版本将确认窗体创建为主窗体的 WS_CHILD/#32770，既不取得
        // 焦点，也不暴露标准 Button 子句柄。遍历全部可见后代，但
        // IsDialogSurface 只放行明确的对话框表面（#32770、非子窗口
        // WS_POPUP 或 modal-frame）；普通 WindowsForms10.Window 面板
        // 即使有标题/大面积也不会进入候选。
        int childDiagnosticCount = 0;
        EnumChildWindows(mainWindow, (hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd) ||
                !GetWindowProcessId(hwnd, out int candidateProcessId) ||
                candidateProcessId != processId)
                return true;
            // 只记录有窗口级 modal 迹象或较大且有标题的后代；普通
            // 编辑框/静态控件不进入日志。每次扫描最多记录 32 个，
            // 再由 EmitDiagnosticWindowIfPresent 按 HWND 冷却去重。
            if (childDiagnosticCount < 32 && IsDiagnosticChildSurface(hwnd))
            {
                EmitDiagnosticWindowIfPresent("child", hwnd, processId);
                childDiagnosticCount++;
            }
            if (IsDialogSurface(hwnd))
                AddPopupCandidate(candidates, hwnd, mainWindow, processId, 320, mainDisabled);
            return true;
        }, nint.Zero);

        // 一次只存在一个 MRTEST 确认弹窗。若多个控件都满足条件，优先
        // active/focus 指向且拥有“确定”按钮的最深容器，避免一张图消费
        // 两个队列步骤。
        return candidates
            .OrderByDescending(pair => pair.Value)
            .ThenByDescending(pair => GetWindowArea(pair.Key))
            .Take(1)
            .Select(pair => pair.Key)
            .ToArray();
    }

    private static void AddPopupCandidate(
        Dictionary<nint, int> candidates,
        nint handle,
        nint mainWindow,
        int processId,
        int sourceScore,
        bool mainDisabled,
        bool stateCandidate = false)
    {
        if (handle == nint.Zero || handle == mainWindow ||
            !IsWindow(handle) || !IsWindowVisible(handle) ||
            !GetWindowProcessId(handle, out int candidateProcessId) ||
            candidateProcessId != processId)
            return;

        nint candidate = GetOutermostChild(handle, mainWindow);
        if (candidate == nint.Zero || candidate == mainWindow ||
            !IsWindowVisible(candidate))
            return;

        // active/focus/capture/last-active 指向的候选来自主窗体已禁用的
        // 模态链路，即使是自绘窗口、没有标题和标准按钮，也必须保留；
        // 通过启用按钮反推的普通子控件仍要求有按钮或标题，降低误报概率。
        bool hasButton = FindUniqueOkButton(candidate) != nint.Zero;
        bool hasTitle = !string.IsNullOrWhiteSpace(GetWindowText(candidate));
        bool dialogClass = GetClassName(candidate).Equals("#32770", StringComparison.OrdinalIgnoreCase);
        bool popupStyle = HasPopupStyle(candidate);
        bool childStyle = HasChildStyle(candidate);
        bool modalFrame = IsDialogFrame(candidate);

        // #32770、WS_POPUP（且不是 WS_CHILD）和 WS_EX_DLGMODALFRAME
        // 是可靠的窗口级 modal 特征，应优先保留，即使没有标题/按钮。
        bool strongModalSurface = dialogClass || popupStyle || modalFrame;

        // WS_CHILD 是本次现场误报的关键：MRTEST 的 Sequential Contrast
        // 页及其外层容器同样是 WindowsForms10.Window + WS_CHILD，且可能
        // 带标题/大面积。普通父子关系不能算 owner 链；只有主窗体确实
        // 被禁用，并且候选来自活动/焦点/捕获/last-active 模态状态时，
        // 才允许无标准 #32770 的自绘子弹窗进入候选。
        bool ownerRelation = IsOwnedBy(candidate, mainWindow);
        bool disabledOwnerModal = childStyle && mainDisabled && stateCandidate &&
                                  ownerRelation && (hasTitle || hasButton);
        if (childStyle && !strongModalSurface && !disabledOwnerModal)
            return;

        // 主窗体未禁用时，active/focus 可能只是普通编辑控件；没有明确
        // modal 特征的候选一律排除。这个条件同时防止大尺寸测试页被
        // IsDialogSurface 的标题/面积启发式选中。
        if (!strongModalSurface && !disabledOwnerModal && !hasButton && !hasTitle)
            return;
        int score = sourceScore + (hasButton ? 1000 : 0) + (hasTitle ? 100 : 0) +
                    (dialogClass ? 500 : 0) + (modalFrame ? 350 : 0);
        if (!candidates.TryGetValue(candidate, out int previous) || score > previous)
            candidates[candidate] = score;
    }

    private static nint GetOutermostChild(nint handle, nint mainWindow)
    {
        // 保留直接枚举到的对话框表面；若先走 GA_ROOT，WS_CHILD/#32770
        // 会被错误折叠回主窗体而无法上报。
        if (IsDialogSurface(handle)) return handle;
        // 对 WS_POPUP/拥有窗口，GetParent 可能返回空或只返回内部控件父级；
        // GA_ROOT 能直接取得真正的弹窗根句柄。普通 WS_CHILD 控件的根仍是
        // mainWindow，继续使用下面的父级回溯得到主窗体直属容器。
        nint root = GetAncestor(handle, GaRoot);
        if (root != nint.Zero && root != mainWindow && IsWindowVisible(root))
            return root;

        nint current = handle;
        nint parent = GetParent(current);
        while (parent != nint.Zero && parent != mainWindow)
        {
            current = parent;
            parent = GetParent(current);
        }
        return parent == mainWindow ? current : handle;
    }

    private static bool IsOwnedBy(nint child, nint owner)
    {
        if (child == nint.Zero || owner == nint.Zero) return false;
        if (GetWindow(child, GwOwner) == owner || GetParent(child) == owner) return true;
        return GetAncestor(child, GaRootOwner) == owner;
    }

    private static bool HasPopupStyle(nint hwnd)
    {
        nint raw = GetWindowLongPtr(hwnd, GwlStyle);
        ulong style = unchecked((ulong)raw.ToInt64());
        return (style & WsPopup) != 0 && (style & WsChild) == 0;
    }

    private static bool HasChildStyle(nint hwnd)
    {
        nint raw = GetWindowLongPtr(hwnd, GwlStyle);
        ulong style = unchecked((ulong)raw.ToInt64());
        return (style & WsChild) != 0;
    }

    private static bool IsDialogSurface(nint hwnd)
    {
        string className = GetClassName(hwnd);
        if (className.Equals("#32770", StringComparison.OrdinalIgnoreCase)) return true;
        if (HasPopupStyle(hwnd)) return true;
        // 普通 WinForms 面板（包括 Sequential Contrast）通常是
        // WindowsForms10.Window + WS_CHILD。除非是标准对话框或带明确
        // modal frame 的自绘窗体，不把这类子窗口当作“表面”返回。
        if (HasChildStyle(hwnd)) return IsDialogFrame(hwnd);

        // 对非标准自绘容器，只把有足够窗口面积且带标题/确认按钮的
        // 表面作为候选，避免把普通 Static、Panel 或编辑框误报。
        if (IsCommonControlClass(className) || GetWindowArea(hwnd) < 1200) return false;
        if (!string.IsNullOrWhiteSpace(GetWindowText(hwnd))) return true;
        return FindUniqueOkButton(hwnd) != nint.Zero;
    }

    private static bool IsCommonControlClass(string className)
    {
        if (string.IsNullOrWhiteSpace(className)) return false;
        string value = className.ToUpperInvariant();
        return value is "BUTTON" or "STATIC" or "EDIT" or "COMBOBOX" or "LISTBOX" or
               "SCROLLBAR" or "TOOLBARWINDOW32" or "SYSTREEVIEW32" or "SYSLISTVIEW32" ||
               value.Contains("BUTTON", StringComparison.Ordinal) ||
               value.Contains("EDIT", StringComparison.Ordinal) ||
               value.Contains("STATIC", StringComparison.Ordinal) ||
               value.Contains("COMBO", StringComparison.Ordinal) ||
               value.Contains("LISTBOX", StringComparison.Ordinal) ||
               value.Contains("SCROLL", StringComparison.Ordinal);
    }

    private void RefreshMrTestIdentity()
    {
        (int boundProcessId, nint boundMainWindow) = GetCachedIdentity();
        if (boundProcessId != 0)
        {
            // 只刷新同一个 PID 的主窗口句柄；如果进程退出，不把新启动的
            // 同名实例偷偷接管当前测试。
            if (!IsMrTestProcess(boundProcessId))
            {
                EmitDiagnostic(
                    $"identity:exited:{boundProcessId}",
                    $"绑定 MRTEST PID {boundProcessId} 已退出；不会自动切换到其他同名实例。");
                lock (_gate) _mainWindowHandle = nint.Zero;
                return;
            }

            // 句柄一旦在启动时确定就保持粘性。只有主窗口确实被销毁
            // （例如 MRTEST 自己重建主窗体）时才重新选择，避免模态框
            // 出现期间 Process.MainWindowHandle/面积排序把弹窗换成主窗。
            nint mainWindow = IsBoundWindowValid(boundMainWindow, boundProcessId)
                ? boundMainWindow
                : FindMainWindowHandle(boundProcessId, boundMainWindow);
            lock (_gate)
            {
                if (_mrTestProcessId == boundProcessId)
                    _mainWindowHandle = mainWindow;
            }
            return;
        }

        // 启动时 MRTEST 可能尚未打开；只允许这一次延迟绑定。
        (int processId, nint discoveredMainWindow) = FindMrTestIdentity();
        bool bound = false;
        lock (_gate)
        {
            if (_mrTestProcessId == 0 && processId != 0)
            {
                _mrTestProcessId = processId;
                _mainWindowHandle = discoveredMainWindow;
                bound = true;
            }
        }
        if (bound)
            Raise(
                MonitorLog,
                $"首次发现并绑定 MRTEST 进程 PID {processId}。主窗：{DescribeWindow(discoveredMainWindow)}");
    }

    private (int ProcessId, nint MainWindow) GetCachedIdentity()
    {
        lock (_gate) return (_mrTestProcessId, _mainWindowHandle);
    }

    private (int ProcessId, nint MainWindow) FindMrTestIdentity()
    {
        int selectedProcessId = 0;
        nint selectedMainWindow = nint.Zero;
        foreach (Process process in Process.GetProcesses())
        {
            try
            {
                if (!IsMrTestProcess(process.Id)) continue;
                nint mainWindow = FindMainWindowHandle(process.Id, process.MainWindowHandle);
                // MRTEST 启动器可能先创建一个没有主窗体的进程，随后再
                // 重启/派生真正的 WinForms 实例。不能在主窗体尚未出现时
                // 把启动器 PID 粘住，否则后续确认窗永远不在绑定进程内。
                // 只把已经找到有效主窗句柄的实例作为绑定候选；若暂时
                // 没有候选，RefreshMrTestIdentity 会在下一轮继续发现。
                if (!IsUsableMainWindow(mainWindow, process.Id)) continue;
                // 若有多个同路径实例，以第一个可见主窗口作为默认排除
                // 目标；绑定后仍保持 PID 粘性，避免测试中串到其他实例。
                if (selectedProcessId == 0)
                {
                    selectedProcessId = process.Id;
                    selectedMainWindow = mainWindow;
                }
            }
            catch
            {
                // 进程可能在枚举期间退出，忽略这一次并在下一轮重试。
            }
            finally { process.Dispose(); }
        }
        return (selectedProcessId, selectedMainWindow);
    }

    private static nint FindMainWindowHandle(int processId, nint reportedMainWindow = default)
    {
        nint selected = nint.Zero;
        long selectedScore = long.MinValue;
        foreach (nint hwnd in EnumerateTopLevelWindows())
        {
            if (!GetWindowProcessId(hwnd, out int candidateProcessId) ||
                candidateProcessId != processId)
                continue;

            // MRTEST 的标准/自绘确认框常常也是标题为“MRTest”的顶层
            // 窗口。#32770、对话框样式和有 owner 的窗口一律不能被选作
            // 主窗，否则后续枚举会把真正弹窗排除掉。
            if (!IsUsableMainWindow(hwnd, processId))
                continue;

            string title = GetWindowText(hwnd);
            long score = GetWindowArea(hwnd);
            if (title.Equals("MRTest", StringComparison.OrdinalIgnoreCase))
                score += 1_000_000_000L;
            // Process.MainWindowHandle 在模态窗口出现时可能暂时指向弹窗。
            // 仅把它作为很弱的加分项，不能覆盖标题/面积判断。
            if (hwnd == reportedMainWindow) score += 10_000;
            if (score > selectedScore)
            {
                selected = hwnd;
                selectedScore = score;
            }
        }
        if (selected == nint.Zero &&
            reportedMainWindow != nint.Zero &&
            IsWindow(reportedMainWindow) &&
            GetWindowProcessId(reportedMainWindow, out int reportedProcessId) &&
            reportedProcessId == processId &&
            IsUsableMainWindow(reportedMainWindow, processId))
        {
            // 主窗口被最小化时 EnumWindows 过滤了不可见窗口；保留进程
            // 提供的句柄作为临时排除目标，下一轮可见后再重新评分。
            selected = reportedMainWindow;
        }
        return selected;
    }

    private static long GetWindowArea(nint hwnd)
    {
        if (!GetWindowRect(hwnd, out NativeRect rect)) return 0;
        long width = Math.Max(0, rect.Right - rect.Left);
        long height = Math.Max(0, rect.Bottom - rect.Top);
        return width * height;
    }

    /// <summary>
    /// 以紧凑、可复制的形式输出一个窗口的现场属性。该诊断信息不参与
    /// 弹窗判定，因此即使后续过滤规则变化，也能据日志还原窗口层级。
    /// </summary>
    private static string DescribeWindow(nint hwnd)
    {
        if (hwnd == nint.Zero) return "HWND=0";

        bool exists = IsWindow(hwnd);
        int processId = GetWindowProcessId(hwnd, out int detectedProcessId)
            ? detectedProcessId
            : 0;
        uint threadId = GetWindowThreadProcessId(hwnd, out _);
        string className = SanitizeDiagnosticText(GetClassName(hwnd), 80);
        string title = SanitizeDiagnosticText(GetWindowText(hwnd), 120);
        nint parent = GetParent(hwnd);
        nint owner = GetWindow(hwnd, GwOwner);
        nint rootOwner = GetAncestor(hwnd, GaRootOwner);
        ulong style = unchecked((ulong)GetWindowLongPtr(hwnd, GwlStyle).ToInt64());
        ulong exStyle = unchecked((ulong)GetWindowLongPtr(hwnd, GwlExStyle).ToInt64());
        long area = GetWindowArea(hwnd);
        string rect = GetWindowRect(hwnd, out NativeRect bounds)
            ? $"[{bounds.Left},{bounds.Top},{bounds.Right},{bounds.Bottom}]"
            : "n/a";
        return $"HWND={FormatHandle(hwnd)} PID={processId} TID={threadId} " +
               $"class=\"{className}\" title=\"{title}\" " +
               $"visible={exists && IsWindowVisible(hwnd)} enabled={exists && IsWindowEnabled(hwnd)} " +
               $"parent={FormatHandle(parent)} owner={FormatHandle(owner)} rootOwner={FormatHandle(rootOwner)} " +
               $"style=0x{style:X8} exStyle=0x{exStyle:X8} " +
               $"child={(style & WsChild) != 0} popup={(style & WsPopup) != 0} " +
               $"modalFrame={(exStyle & WsExDlgModalFrame) != 0} rect={rect} area={area}";
    }

    private static string FormatHandle(nint hwnd) =>
        hwnd == nint.Zero ? "0" : $"0x{hwnd.ToInt64():X}";

    private static string SanitizeDiagnosticText(string? value, int maxLength)
    {
        string text = (value ?? string.Empty)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
        if (text.Length <= maxLength) return text;
        return text[..maxLength] + "...";
    }

    /// <summary>
    /// 现场只记录有窗口级模态迹象或较大有标题的子窗。普通 Button/Edit
    /// 等控件数量可能很多，限制在这里可避免日志淹没真正的弹窗信息。
    /// </summary>
    private static bool IsDiagnosticChildSurface(nint hwnd)
    {
        string className = GetClassName(hwnd);
        if (className.Equals("#32770", StringComparison.OrdinalIgnoreCase) ||
            HasPopupStyle(hwnd) || IsDialogFrame(hwnd))
            return true;
        return GetWindowArea(hwnd) >= 1200 &&
               !string.IsNullOrWhiteSpace(GetWindowText(hwnd));
    }

    private void EmitDiagnosticWindowIfPresent(
        string category,
        nint hwnd,
        int expectedProcessId = 0)
    {
        if (hwnd == nint.Zero || !IsWindow(hwnd) || !IsWindowVisible(hwnd)) return;
        if (!GetWindowProcessId(hwnd, out int processId)) return;
        if (expectedProcessId == 0)
            expectedProcessId = GetCachedIdentity().ProcessId;
        if (expectedProcessId != 0 && processId != expectedProcessId) return;
        EmitDiagnostic(
            $"{category}:{hwnd.ToInt64():X}",
            $"{category}窗口：{DescribeWindow(hwnd)}");
    }

    private void EmitDiagnostic(string key, string message)
    {
        DateTime now = DateTime.UtcNow;
        bool shouldLog;
        lock (_gate)
        {
            shouldLog = !_diagnosticLastLoggedUtc.TryGetValue(key, out DiagnosticLogSample? previous) ||
                        now - previous.LoggedAtUtc >=
                        (string.Equals(previous.Message, message, StringComparison.Ordinal)
                            ? DiagnosticRepeatLogCooldown
                            : DiagnosticChangedLogCooldown);
            if (!shouldLog) return;

            if (_diagnosticLastLoggedUtc.Count >= DiagnosticLogEntryLimit)
            {
                string? oldestKey = null;
                DateTime oldest = DateTime.MaxValue;
                foreach ((string candidateKey, DiagnosticLogSample sample) in _diagnosticLastLoggedUtc)
                {
                    if (sample.LoggedAtUtc < oldest)
                    {
                        oldest = sample.LoggedAtUtc;
                        oldestKey = candidateKey;
                    }
                }
                if (oldestKey is not null) _diagnosticLastLoggedUtc.Remove(oldestKey);
            }
            _diagnosticLastLoggedUtc[key] = new DiagnosticLogSample(now, message);
        }
        Raise(MonitorLog, $"诊断：{message}");
    }

    /// <summary>
    /// Checks that a handle is a real MRTEST top-level main window.  During
    /// startup MRTEST can briefly expose an ownerless <c>#32770</c> error
    /// dialog (and <see cref="Process.MainWindowHandle"/> may report it).
    /// Treating that handle as the main window permanently excludes the real
    /// instance that appears a moment later.  Keep this predicate shared by
    /// initial discovery, reported-handle fallback, and cache refresh so a
    /// transient dialog can never become the sticky main-window identity.
    /// </summary>
    private static bool IsUsableMainWindow(nint hwnd, int processId)
    {
        if (hwnd == nint.Zero || processId == 0 || !IsWindow(hwnd)) return false;
        if (!GetWindowProcessId(hwnd, out int actualProcessId) || actualProcessId != processId)
            return false;
        // A main window is a top-level surface.  Parent/owner relationships,
        // WS_CHILD and modal-frame styles identify a child/popup dialog even
        // when its class name is custom or temporarily empty.
        if (GetParent(hwnd) != nint.Zero || GetWindow(hwnd, GwOwner) != nint.Zero)
            return false;
        ulong style = unchecked((ulong)GetWindowLongPtr(hwnd, GwlStyle).ToInt64());
        ulong exStyle = unchecked((ulong)GetWindowLongPtr(hwnd, GwlExStyle).ToInt64());
        if ((style & WsChild) != 0 || (exStyle & WsExDlgModalFrame) != 0)
            return false;
        string className = GetClassName(hwnd);
        return !string.IsNullOrWhiteSpace(className) &&
               !className.Equals("#32770", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBoundWindowValid(nint hwnd, int processId) =>
        IsUsableMainWindow(hwnd, processId);

    private static bool IsDialogFrame(nint hwnd)
    {
        ulong exStyle = unchecked((ulong)GetWindowLongPtr(hwnd, GwlExStyle).ToInt64());
        return (exStyle & WsExDlgModalFrame) != 0;
    }

    private static nint GetProcessMainWindowHandle(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            return process.MainWindowHandle;
        }
        catch { return nint.Zero; }
    }

    private bool IsMainWindow(nint hwnd, int processId, nint cachedMainWindow)
    {
        // 绑定成功后只认缓存的主窗句柄；不能为每一个候选再次按标题/面积
        // 排序，否则弹窗出现时可能把它自己选成“主窗”。
        if (cachedMainWindow != nint.Zero) return hwnd == cachedMainWindow;
        nint processMainWindow = FindMainWindowHandle(processId, GetProcessMainWindowHandle(processId));
        return processMainWindow != nint.Zero && hwnd == processMainWindow;
    }

    private async Task<bool> WaitForDialogTransitionAsync(
        nint dialogHandle,
        DialogFingerprint before,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        DialogFingerprint? stableTransition = null;
        int stableSamples = 0;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsWindow(dialogHandle) || !IsWindowVisible(dialogHandle))
                return true;
            if (TryGetSnapshot(dialogHandle, out MrTestDialogSnapshot? current) && current is not null)
            {
                DialogFingerprint fingerprint = CreateFingerprint(current);
                // A rebuilt Button HWND is not a new prompt.  Only a visible,
                // non-empty title/control-text signature can represent a
                // same-HWND prompt transition; require two equal samples so
                // the transient "clear controls, then destroy" state is not
                // mistaken for the next image step.
                if (HasMeaningfulContentChange(before, fingerprint))
                {
                    if (stableTransition is not null && stableTransition.Equals(fingerprint))
                        stableSamples++;
                    else
                    {
                        stableTransition = fingerprint;
                        stableSamples = 1;
                    }
                    if (stableSamples >= 2) return true;
                }
                else
                {
                    stableTransition = null;
                    stableSamples = 0;
                }
            }
            await Task.Delay(GetPollInterval(), cancellationToken).ConfigureAwait(false);
        }
        return !IsWindow(dialogHandle) || !IsWindowVisible(dialogHandle);
    }

    private static bool HasMeaningfulContentChange(
        DialogFingerprint before,
        DialogFingerprint current) =>
        before.ProcessId == current.ProcessId &&
        !string.IsNullOrWhiteSpace(current.Signature) &&
        !string.Equals(before.Signature, current.Signature, StringComparison.Ordinal);

    private static bool TryPostDialogCommand(nint dialogHandle, nint buttonHandle)
    {
        bool sent = false;
        // 即使找不到独立 Button HWND，也先向对话框根窗口发送 IDOK。
        // 原生 MessageBox、DirectUI 和部分自绘 WinForms 对话框都在根
        // 窗口的默认处理器中消费这个命令。
        sent |= PostMessage(dialogHandle, WmCommand, (nint)IdOk, nint.Zero);
        if (buttonHandle != nint.Zero && IsWindow(buttonHandle))
        {
            int id = GetDlgCtrlID(buttonHandle);
            if (id > 0)
            {
                // WM_COMMAND(IDOK) 兼容没有标准 Button 类名、但仍使用
                // WinForms DialogResult 的窗口。
                sent |= PostMessage(dialogHandle, WmCommand, (nint)id, buttonHandle);
            }
        }

        // 直接给当前焦点控件和弹窗根窗口投递 Enter，等价于操作员在
        // 弹窗上按回车。WinForms 的按键消息通常先落到焦点子控件，
        // 只发给根窗口在部分版本中不会触发 AcceptButton。
        foreach (nint target in GetDialogInputTargets(dialogHandle, buttonHandle))
        {
            sent |= PostMessage(target, WmKeyDown, VkReturn, nint.Zero);
            sent |= PostMessage(target, WmKeyUp, VkReturn, nint.Zero);
        }
        nint result;
        // 即使 PostMessage 已成功入队也再同步发送一次 IDOK；前者只表示
        // 消息进入队列，并不表示 MRTEST 的模态循环已经处理它。
        if (SendMessageTimeout(
            dialogHandle, WmCommand, (nint)IdOk, nint.Zero,
            SmtoAbortIfHung, 1000, out result) != nint.Zero)
            sent = true;
        foreach (nint target in GetDialogInputTargets(dialogHandle, buttonHandle))
        {
            bool keyDown = SendMessageTimeout(
                target, WmKeyDown, VkReturn, nint.Zero,
                SmtoAbortIfHung, 1000, out result) != nint.Zero;
            bool keyUp = SendMessageTimeout(
                target, WmKeyUp, VkReturn, nint.Zero,
                SmtoAbortIfHung, 1000, out result) != nint.Zero;
            if (keyDown || keyUp) return true;
        }
        return sent;
    }

    private static bool TrySendForegroundEnter(nint dialogHandle)
    {
        if (dialogHandle == nint.Zero || !IsWindow(dialogHandle) || !IsWindowVisible(dialogHandle))
            return false;

        nint root = GetAncestor(dialogHandle, GaRoot);
        if (root == nint.Zero) root = dialogHandle;
        uint currentThread = GetCurrentThreadId();
        // 线程池线程通常尚未创建 Win32 消息队列；AttachThreadInput 对
        // 没有消息队列的线程会静默失败，先 PeekMessage 建立队列。
        _ = PeekMessage(out NativeMessage _, nint.Zero, 0, 0, PmNoRemove);
        uint dialogThread = GetWindowThreadProcessId(root, out _);
        bool attached = dialogThread != 0 && dialogThread != currentThread &&
                        AttachThreadInput(currentThread, dialogThread, true);
        try
        {
            ShowWindowAsync(root, SwRestore);
            BringWindowToTop(root);
            SetForegroundWindow(root);
            SetFocus(dialogHandle);
        }
        finally
        {
            if (attached) AttachThreadInput(currentThread, dialogThread, false);
        }

        // 不在无法置前时向用户当前窗口注入按键；这一步是有意的安全闸门。
        if (GetForegroundWindow() != root) return false;
        Thread.Sleep(60);
        // keybd_event 使用固定 ABI，不受 AnyCPU 下 INPUT 联合体大小差异
        // 影响；客户端本身已通过清单提升到与 MRTEST 相同的完整性级别。
        keybd_event(0x0D, 0, 0, nint.Zero);
        keybd_event(0x0D, 0, KeyEventKeyUp, nint.Zero);
        return true;
    }

    private static IEnumerable<nint> GetDialogInputTargets(nint dialogHandle, nint buttonHandle)
    {
        var targets = new List<nint>();
        if (buttonHandle != nint.Zero && IsWindow(buttonHandle)) targets.Add(buttonHandle);
        uint threadId = GetWindowThreadProcessId(dialogHandle, out _);
        if (threadId != 0)
        {
            var info = new GuiThreadInfo { CbSize = Marshal.SizeOf<GuiThreadInfo>() };
            if (GetGUIThreadInfo(threadId, ref info))
            {
                if (info.HwndFocus != nint.Zero) targets.Add(info.HwndFocus);
                if (info.HwndActive != nint.Zero) targets.Add(info.HwndActive);
            }
        }
        targets.Add(dialogHandle);
        return targets.Where(IsWindow).Distinct();
    }

    private bool TryCloseDialog(nint dialogHandle, int processId)
    {
        // WM_CLOSE 是最后回退，只允许发给已确认属于 MRTEST 且不是主窗的
        // 顶层窗口。这样即使自绘弹窗没有任何可访问按钮，也不会关掉 MRTEST。
        if (!IsWindow(dialogHandle) || !IsWindowVisible(dialogHandle) ||
            !IsBoundMrTestProcess(processId) || IsMainWindow(dialogHandle, processId, _mainWindowHandle))
            return false;

        nint owner = GetWindow(dialogHandle, GwOwner);
        nint rootOwner = GetAncestor(dialogHandle, GaRootOwner);
        if (owner == nint.Zero && rootOwner == nint.Zero)
        {
            // 真实 MRTEST 版本可能创建无 owner 的独立顶层确认框；
            // 前面的绑定 PID、可见性和主窗排除检查已经完成安全隔离，
            // 因此不能再因 owner 为空而跳过 WM_CLOSE。
            Raise(MonitorLog, $"MRTEST 弹窗无 owner/rootOwner，仍尝试 WM_CLOSE：{FormatHandle(dialogHandle)}。");
        }

        nint result;
        // 先同步请求关闭；SendMessageTimeout 的返回值表示消息是否在
        // 超时前被窗口线程处理，不依赖 WM_CLOSE 的 lResult（通常为 0）。
        bool sent = SendMessageTimeout(
            dialogHandle, WmClose, nint.Zero, nint.Zero,
            SmtoAbortIfHung, 1000, out result) != nint.Zero;
        if (!IsWindowVisible(dialogHandle)) return true;
        // SendMessageTimeout only proves dispatch, not that the custom shell
        // actually closed.  Queue another WM_CLOSE and, if still visible,
        // fall through to SC_CLOSE so ownerless/self-drawn dialogs get both
        // compatibility routes in the same confirmation attempt.
        sent |= PostMessage(dialogHandle, WmClose, nint.Zero, nint.Zero);
        if (!IsWindowVisible(dialogHandle)) return true;
        // WM_SYSCOMMAND/SC_CLOSE is handled by a few custom dialog shells
        // that ignore WM_CLOSE while their modal loop is active.
        bool systemCloseSent = PostMessage(dialogHandle, WmSysCommand, ScClose, nint.Zero) ||
                               SendMessageTimeout(dialogHandle, WmSysCommand, ScClose, nint.Zero,
                                   SmtoAbortIfHung, 1000, out result) != nint.Zero;
        return sent || systemCloseSent;
    }

    private bool IsMrTestProcess(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            if (!string.IsNullOrWhiteSpace(_executablePath))
            {
                // MainModule.FileName 在 MRTEST 以管理员权限运行时可能为空；
                // QueryFullProcessImageName 通常仍能读取完整路径，失败时再按
                // 进程名兜底，避免因权限差异导致整个监视器静默失效。
                string? rawPath = null;
                try { rawPath = TryGetExecutablePath(process.Id) ?? process.MainModule?.FileName; }
                catch { /* 继续按进程名兜底。 */ }
                string path = NormalizePath(rawPath);
                if (!string.IsNullOrWhiteSpace(path))
                    return path.Equals(_executablePath, StringComparison.OrdinalIgnoreCase);
            }
            return process.ProcessName.Equals("MRTest", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    private bool IsBoundMrTestProcess(int processId)
    {
        lock (_gate)
            return processId != 0 && _mrTestProcessId == processId;
    }

    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        string value = path.Trim().Trim('"');
        try { return Path.GetFullPath(value); } catch { return value; }
    }

    private TimeSpan GetPollInterval()
    {
        if (PollInterval <= TimeSpan.Zero) return TimeSpan.FromMilliseconds(50);
        return PollInterval > TimeSpan.FromSeconds(1) ? TimeSpan.FromSeconds(1) : PollInterval;
    }

    private static nint FindUniqueOkButton(nint parent)
    {
        var buttons = new List<(nint Handle, int Score)>();
        var visibleButtons = new List<nint>();
        EnumChildWindows(parent, (hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd) || !IsWindowEnabled(hwnd)) return true;
            int id = GetDlgCtrlID(hwnd);
            string cls = GetClassName(hwnd);
            string text = NormalizeButtonText(GetWindowText(hwnd));
            if (!IsButtonClass(cls)) return true;
            visibleButtons.Add(hwnd);
            int score = 0;
            if (id == IdOk) score += 100;
            // 2 在标准 Win32 对话框中通常是“取消”，只作为低优先级
            // 兼容某些 MRTEST 自定义 ID；若同时存在“确定”文字，文字优先。
            if (id == IdMessageBoxOk) score += 20;
            if (IsOkButtonText(text)) score += 120;
            if (score > 0) buttons.Add((hwnd, score));
            return true;
        }, nint.Zero);

        // 优先标准 ID/确定文字；若自绘版本只有一个按钮，也把它当作
        // 确认按钮。多个未知按钮时交给 Enter/WM_CLOSE 回退，避免误点。
        if (buttons.Count > 0)
            return buttons.OrderByDescending(item => item.Score).First().Handle;
        return visibleButtons.Count == 1 ? visibleButtons[0] : nint.Zero;
    }

    private static IEnumerable<nint> EnumerateTopLevelWindows()
    {
        var result = new List<nint>();
        EnumWindows((hwnd, _) => { if (IsWindowVisible(hwnd)) result.Add(hwnd); return true; }, nint.Zero);
        return result;
    }

    private static bool IsButtonClass(string className) =>
        className.Equals("Button", StringComparison.OrdinalIgnoreCase) ||
        className.StartsWith("WindowsForms10.BUTTON.", StringComparison.OrdinalIgnoreCase) ||
        className.StartsWith("WindowsForms10.Button.", StringComparison.OrdinalIgnoreCase) ||
        className.Contains(".BUTTON.", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeButtonText(string text) =>
        (text ?? string.Empty).Replace("&", string.Empty, StringComparison.Ordinal)
            .Replace("（", "(", StringComparison.Ordinal)
            .Replace("）", ")", StringComparison.Ordinal)
            .Trim();

    private static bool IsOkButtonText(string text) =>
        text.Equals("确定", StringComparison.OrdinalIgnoreCase) ||
        text.Equals("OK", StringComparison.OrdinalIgnoreCase) ||
        text.Equals("确定(O)", StringComparison.OrdinalIgnoreCase) ||
        text.Equals("确定/OK", StringComparison.OrdinalIgnoreCase);

    private static DialogFingerprint CreateFingerprint(MrTestDialogSnapshot snapshot)
    {
        // 子控件文本参与指纹，使 MRTEST 在复用同一窗口句柄切换提示内容时
        // 仍能触发下一张图卡；文本不会被用于识别测试项目。
        // Do not include HWNDs in the content signature.  MRTEST can rebuild
        // its Button child after a click while keeping the same prompt alive;
        // a handle-only difference must not be treated as a completed step.
        var texts = new List<string>();
        if (!string.IsNullOrWhiteSpace(snapshot.Title))
            texts.Add($"T:{snapshot.Title.Trim()}");
        EnumChildWindows(snapshot.Handle, (hwnd, _) =>
        {
            if (IsWindowVisible(hwnd))
            {
                string text = GetWindowText(hwnd);
                if (!string.IsNullOrWhiteSpace(text))
                    // Child HWNDs and dialog-control IDs are intentionally
                    // omitted.  WinForms/MRTEST may recreate an otherwise
                    // identical Button after BM_CLICK; only visible text is
                    // meaningful evidence that a reused HWND reached the
                    // next prompt.
                    texts.Add($"C:{text.Trim()}");
            }
            return true;
        }, nint.Zero);
        texts.Sort(StringComparer.Ordinal);
        return new DialogFingerprint(snapshot.ProcessId, snapshot.Title, string.Join("\u001f", texts));
    }

    private static bool GetWindowProcessId(nint hwnd, out int processId)
    {
        GetWindowThreadProcessId(hwnd, out uint raw);
        processId = unchecked((int)raw);
        return raw != 0;
    }

    private static bool TryClickButton(nint button)
    {
        if (button == nint.Zero || !IsWindow(button) || !IsWindowVisible(button) || !IsWindowEnabled(button))
            return false;
        nint result;
        // 先同步调用，确保函数返回时控件已经处理了 BM_CLICK；超时保护
        // 避免 MRTEST GUI 线程卡住客户端。
        if (SendMessageTimeout(button, BmClick, nint.Zero, nint.Zero,
            SmtoAbortIfHung, 1000, out result) != nint.Zero)
            return true;
        // 同步消息被 UIPI/自定义控件拒绝时再排队一次，后续 Enter/关闭
        // 回退仍会验证窗口是否真的发生变化。
        return PostMessage(button, BmClick, nint.Zero, nint.Zero);
    }

    private static string? TryGetExecutablePath(int processId)
    {
        nint handle = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (handle == nint.Zero) return null;
        try
        {
            var buffer = new StringBuilder(1024);
            uint length = (uint)buffer.Capacity;
            return QueryFullProcessImageName(handle, 0, buffer, ref length)
                ? buffer.ToString()
                : null;
        }
        finally { CloseHandle(handle); }
    }

    private static string GetWindowText(nint hwnd)
    {
        int length = GetWindowTextLength(hwnd);
        var buffer = new StringBuilder(Math.Max(length + 1, 256));
        GetWindowText(hwnd, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    private static string GetClassName(nint hwnd)
    {
        var buffer = new StringBuilder(256);
        GetClassName(hwnd, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    private static void Raise<T>(EventHandler<T>? handler, T args)
    {
        if (handler is null) return;
        foreach (Delegate d in handler.GetInvocationList())
        {
            try { ((EventHandler<T>)d).Invoke(null, args); } catch { }
        }
    }

    private bool TryClaimPending(MrTestDialogDetectedEventArgs notification)
    {
        lock (_gate)
        {
            for (int i = 0; i < _pendingNotifications.Count; i++)
            {
                PendingDialogNotification pending = _pendingNotifications[i];
                if (!ReferenceEquals(pending.EventArgs, notification)) continue;
                if (pending.Claimed) return false;
                pending.Claimed = true;
                _pendingNotifications.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    public async ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
        }
        await StopAsync().ConfigureAwait(false);
    }

    private delegate bool EnumWindowsProc(nint hwnd, nint lParam);

    [DllImport("user32.dll", SetLastError = true)] private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool EnumChildWindows(nint hWndParent, EnumWindowsProc lpEnumFunc, nint lParam);
    [DllImport("user32.dll")] private static extern nint GetParent(nint hWnd);
    [DllImport("user32.dll")] private static extern nint GetWindow(nint hWnd, uint uCmd);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern nint GetWindowLongPtr(nint hWnd, int nIndex);
    [DllImport("user32.dll")] private static extern nint GetAncestor(nint hWnd, uint gaFlags);
    [DllImport("user32.dll")] private static extern nint GetLastActivePopup(nint hWnd);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool GetWindowRect(nint hWnd, out NativeRect rect);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool GetGUIThreadInfo(uint threadId, ref GuiThreadInfo info);
    [DllImport("user32.dll")] private static extern bool IsWindow(nint hWnd);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint hWnd);
    [DllImport("user32.dll")] private static extern bool IsWindowEnabled(nint hWnd);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint hWnd, out uint processId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(nint hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowTextLength(nint hWnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint hWnd, StringBuilder lpClassName, int nMaxCount);
    [DllImport("user32.dll")] private static extern int GetDlgCtrlID(nint hwndCtl);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool PostMessage(nint hWnd, uint msg, nint wParam, nint lParam);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint SendMessageTimeout(
        nint hWnd, uint msg, nint wParam, nint lParam, uint flags, uint timeout, out nint result);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint hWnd);
    [DllImport("user32.dll")] private static extern bool BringWindowToTop(nint hWnd);
    [DllImport("user32.dll")] private static extern bool ShowWindowAsync(nint hWnd, int nCmdShow);
    [DllImport("user32.dll")] private static extern nint SetFocus(nint hWnd);
    [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
    [DllImport("user32.dll")] private static extern bool PeekMessage(
        out NativeMessage lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] private static extern void keybd_event(
        byte bVk, byte bScan, uint dwFlags, nint dwExtraInfo);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern nint OpenProcess(uint access, bool inheritHandle, int processId);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)] private static extern bool QueryFullProcessImageName(
        nint processHandle, uint flags, StringBuilder exeName, ref uint size);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool CloseHandle(nint handle);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private const uint KeyEventKeyUp = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct GuiThreadInfo
    {
        public int CbSize;
        public uint Flags;
        public nint HwndActive;
        public nint HwndFocus;
        public nint HwndCapture;
        public nint HwndMenuOwner;
        public nint HwndMoveSize;
        public nint HwndCaret;
        public NativeRect RcCaret;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMessage
    {
        public nint HWnd;
        public uint Message;
        public nint WParam;
        public nint LParam;
        public uint Time;
        public NativePoint Point;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    private sealed record DialogFingerprint(int ProcessId, string Title, string Signature);
    private sealed record CandidateSample(DialogFingerprint Fingerprint, int Count);
    private sealed record DiagnosticLogSample(DateTime LoggedAtUtc, string Message);

    private sealed class PendingDialogNotification
    {
        public PendingDialogNotification(MrTestDialogDetectedEventArgs eventArgs) => EventArgs = eventArgs;

        public MrTestDialogDetectedEventArgs EventArgs { get; }
        public bool Claimed { get; set; }
    }
}
