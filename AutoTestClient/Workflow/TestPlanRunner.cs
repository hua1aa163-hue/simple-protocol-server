using System.Net;
using AutoTestClient.DataProcessing;
using AutoTestClient.Models;
using AutoTestClient.Monitoring;
using AutoTestClient.Networking;
using AutoTestClient.Projection;
using AutoTestClient.Protocol;
using AutoTestClient.Recipes;

namespace AutoTestClient.Workflow;

public enum TestRunState { Idle, Running, Completed, Failed, Cancelled }

public sealed record TestRunProgress(
    TestRunState State,
    int Round,
    int TotalRounds,
    string ProjectName,
    int ProjectIteration,
    int ProjectTotalIterations,
    string Message);

/// <summary>按计划、项目次数和固定图卡顺序执行一次完整测量。</summary>
public sealed class TestPlanRunner : IAsyncDisposable
{
    private readonly TcpMessageServer _server;
    private readonly IImageProjector _projector;
    private readonly MrTestDialogMonitor _monitor;
    private readonly ITestDataProcessor _dataProcessor;
    private readonly Func<string, Task> _log;
    private readonly SemaphoreSlim _runLock = new(1, 1);
    private CancellationTokenSource? _activeCancellation;
    private bool _disposed;

    public TestPlanRunner(
        TcpMessageServer server,
        IImageProjector projector,
        MrTestDialogMonitor monitor,
        ITestDataProcessor? dataProcessor = null,
        Func<string, Task>? log = null)
    {
        _server = server;
        _projector = projector;
        _monitor = monitor;
        _dataProcessor = dataProcessor ?? new TestDataProcessingService();
        _log = log ?? (_ => Task.CompletedTask);
    }

    public TestRunState State { get; private set; } = TestRunState.Idle;
    public event EventHandler<TestRunProgress>? ProgressChanged;
    public event EventHandler<TestDataProcessingResult>? DataResultProduced;

    public void Cancel() => _activeCancellation?.Cancel();

    public async Task RunAsync(TestPlanConfiguration configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        configuration = configuration.Clone();
        configuration.Normalize();
        await _runLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _activeCancellation = linked;
        State = TestRunState.Running;
        try
        {
            if (!_server.IsListening) throw new InvalidOperationException("请先启动 TCP 监听。");
            if (!_server.IsConnected) throw new InvalidOperationException("尚无 MRTEST/设备 TCP 客户端连接。");
            List<TestProject> projects = configuration.Projects
                .Where(p => p.Enabled)
                .OrderBy(p => p.Order)
                .ThenBy(p => p.Name, StringComparer.CurrentCulture)
                .ToList();
            if (projects.Count == 0) throw new InvalidOperationException("没有启用的测试项目。");
            ValidateProjects(configuration, projects);

            for (int round = 1; round <= configuration.WholePlanRepeatCount; round++)
            {
                Report(TestRunState.Running, round, configuration.WholePlanRepeatCount, string.Empty, 0, 0,
                    $"开始第 {round}/{configuration.WholePlanRepeatCount} 轮");
                foreach (TestProject project in projects)
                {
                    linked.Token.ThrowIfCancellationRequested();
                    await RunProjectAsync(configuration, project, round, linked.Token).ConfigureAwait(false);
                }
            }
            State = TestRunState.Completed;
            Report(TestRunState.Completed, configuration.WholePlanRepeatCount, configuration.WholePlanRepeatCount,
                string.Empty, 0, 0, "完整一键测试计划已完成");
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested)
        {
            State = TestRunState.Cancelled;
            Report(TestRunState.Cancelled, 0, configuration.WholePlanRepeatCount, string.Empty, 0, 0, "测试已停止");
            throw;
        }
        catch (Exception ex)
        {
            State = TestRunState.Failed;
            Report(TestRunState.Failed, 0, configuration.WholePlanRepeatCount, string.Empty, 0, 0, $"测试失败：{ex.Message}");
            throw;
        }
        finally
        {
            try
            {
                // 即使发送、弹窗序列、数据处理或取消路径异常，也不允许监视器
                // 在测试事务结束后留在后台继续处理 MRTEST 窗口。
                await StopDialogMonitoringAsync(logWhenStopped: false).ConfigureAwait(false);
            }
            finally
            {
                _activeCancellation = null;
                _runLock.Release();
            }
        }
    }

    private async Task RunProjectAsync(
        TestPlanConfiguration configuration,
        TestProject project,
        int round,
        CancellationToken cancellationToken)
    {
        int repeats = Math.Max(1, project.RepeatCount);
        Report(TestRunState.Running, round, configuration.WholePlanRepeatCount, project.Name, 0, repeats,
            $"项目开始：{project.Name}（{project.KindDisplayName}），本轮 {repeats} 次");

        if (!string.IsNullOrWhiteSpace(project.RecipeName))
        {
            string recipe = project.RecipeName.Trim();
            if (MessageProtocol.ContainsReservedCharacter(recipe))
                throw new FormatException($"项目“{project.Name}”的配方名称包含协议保留字符。");
            string switchCommand = $"&|Elems|C|{recipe}|@";
            await _server.SendAndWaitForCompletionAsync(switchCommand, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            string recipeFile = string.IsNullOrWhiteSpace(project.RecipeFilePath)
                ? RecipeCatalog.ResolveFilePath(configuration.RecipeDirectory, recipe)
                : (Path.IsPathRooted(project.RecipeFilePath) ? project.RecipeFilePath : Path.Combine(configuration.RecipeDirectory, project.RecipeFilePath));
            if (!string.IsNullOrWhiteSpace(recipeFile) && !File.Exists(recipeFile))
                await _log($"提示：配方文件未找到（不阻止协议切换）：{recipeFile}").ConfigureAwait(false);
            await _log($"已切换配方：{recipe}").ConfigureAwait(false);
        }

        for (int iteration = 1; iteration <= repeats; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Report(TestRunState.Running, round, configuration.WholePlanRepeatCount, project.Name,
                iteration, repeats, $"开始项目第 {iteration}/{repeats} 次");
            DateTime startedUtc = DateTime.UtcNow;
            // Capture the pre-test workbook state for every built-in result
            // path.  Crosstalk uses the existing folder fingerprint; the
            // non-crosstalk Excel reader additionally excludes unchanged
            // workbooks so a repeated project cannot display the prior run.
            ExportSnapshot? snapshot = project.Kind == TestProjectKind.Crosstalk
                ? _dataProcessor.CaptureExportSnapshot(configuration.ExportDirectory)
                : _dataProcessor.CaptureResultSnapshot(configuration.ExportDirectory);
            var records = new List<TestMeasurementRecord>();

            // MRTEST 的 FOV 与 9 点亮度均匀性配方本身就是 9Point
            // 流程。即使旧配置中的“弹窗序列”仍为 false，测量过程中
            // 也会弹出“切换9Point图像”确认框；若继续走顺序项目分支，
            // 监视器只能记录窗口而没有消费者，测试会卡在该弹窗上。
            // 将这两类配方视为隐式弹窗项目，使用同一固定图卡队列，
            // 不改变用户在界面中编辑/保存的 PopupDriven 字段。
            // 串扰的“弹窗序列”与 FOV/对比度/色域的序列语义不同：
            // 每一张图卡都是一次独立的测量事务，必须在收到该张图的
            // 最终 OK 后才能进入下一张。因此先分流到逐图实现，不能
            // 让它落入“一次命令消费整条队列”的普通弹窗流程。
            if (project.Kind == TestProjectKind.Crosstalk && project.PopupDriven)
            {
                await RunCrosstalkPopupProjectAsync(configuration, project, round, iteration, records, cancellationToken)
                    .ConfigureAwait(false);
            }
            else if (RequiresMrTestPopupSequence(project))
            {
                if (!project.PopupDriven)
                    await _log($"项目“{project.Name}”按 MRTEST 9Point 配方自动使用弹窗图卡序列。").ConfigureAwait(false);
                await RunPopupProjectAsync(configuration, project, round, iteration, records, cancellationToken)
                    .ConfigureAwait(false);
            }
            else if (project.Kind == TestProjectKind.Crosstalk)
            {
                await RunCrosstalkProjectAsync(configuration, project, round, iteration, records, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                await RunSequentialProjectAsync(configuration, project, round, iteration, records, cancellationToken)
                    .ConfigureAwait(false);
            }

            CrosstalkAnalysisOptions? analysisOptions = null;
            if (project.Kind == TestProjectKind.Crosstalk)
            {
                bool[,]? userMask = string.IsNullOrWhiteSpace(configuration.CrosstalkMaskCoordinates)
                    ? null
                    : CrosstalkMaskStore.ParseCoordinateRanges(configuration.CrosstalkMaskCoordinates);
                analysisOptions = configuration.CreateCrosstalkAnalysisOptions(
                    userMask,
                    configuration.CrosstalkMaskName);
            }
            TestDataProcessingResult result = await _dataProcessor.ProcessAsync(
                project,
                configuration.ExportDirectory,
                configuration.OutputDirectory,
                startedUtc,
                records,
                new Progress<string>(message => _ = _log(message)),
                cancellationToken,
                snapshot,
                analysisOptions,
                configuration.DisplayRules).ConfigureAwait(false);
            DataResultProduced?.Invoke(this, result);
            foreach (TestMetric metric in result.Metrics)
                await _log($"{project.Name} / {metric.DisplayName}：{metric.Value} [{metric.Status}]").ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(result.Detail)) await _log(result.Detail).ConfigureAwait(false);
        }
    }

    private static bool RequiresMrTestPopupSequence(TestProject project) =>
        project.PopupDriven ||
        project.Kind is TestProjectKind.Fov or TestProjectKind.BrightnessUniformity;

    private async Task RunSequentialProjectAsync(
        TestPlanConfiguration configuration,
        TestProject project,
        int round,
        int iteration,
        List<TestMeasurementRecord> records,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<TestStep> steps = project.Steps.Count == 0
            ? new[] { new TestStep { Order = 1, Name = project.Name, ImagePath = string.Empty } }
            : project.Steps.OrderBy(s => s.Order).ToArray();
        int sequence = 0;
        foreach (TestStep step in steps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string imagePath = step.ResolveImagePath(configuration.ImageDirectory);
            if (!string.IsNullOrWhiteSpace(imagePath))
            {
                await _projector.ProjectAsync(imagePath, configuration.ProjectionMode, cancellationToken)
                    .ConfigureAwait(false);
                await DelayAsync(step.StabilizeDelayMs, cancellationToken).ConfigureAwait(false);
            }
            string request = NormalizeRequest(step.MeasurementRequest);
            DateTime completed = await SendMeasurementAsync(configuration, request, cancellationToken).ConfigureAwait(false);
            records.Add(new TestMeasurementRecord(++sequence, project.Name, imagePath, completed));
            Report(TestRunState.Running, round, configuration.WholePlanRepeatCount, project.Name,
                iteration, project.RepeatCount, $"图卡完成：{step.DisplayName}");
        }
    }

    private async Task RunCrosstalkProjectAsync(
        TestPlanConfiguration configuration,
        TestProject project,
        int round,
        int iteration,
        List<TestMeasurementRecord> records,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<TestStep> ordered = OrderCrosstalkSteps(project.Steps, project.CrosstalkStartIndex);
        if (ordered.Count < 3 || ordered.Count % 2 == 0)
            throw new InvalidDataException("串扰图卡数量必须为大于等于 3 的奇数，且最后一张为本底图。");
        int sequence = 0;
        foreach (TestStep step in ordered)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string imagePath = step.ResolveImagePath(configuration.ImageDirectory);
            await _projector.ProjectAsync(imagePath, configuration.ProjectionMode, cancellationToken).ConfigureAwait(false);
            await DelayAsync(step.StabilizeDelayMs, cancellationToken).ConfigureAwait(false);
            DateTime completed = await SendMeasurementAsync(configuration, NormalizeRequest(step.MeasurementRequest), cancellationToken)
                .ConfigureAwait(false);
            records.Add(new TestMeasurementRecord(++sequence, project.Name, imagePath, completed));
            await _log($"串扰图卡 {sequence}/{ordered.Count} 完成：{step.DisplayName}").ConfigureAwait(false);
            if (sequence < ordered.Count) await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 串扰弹窗模式：每张图卡对应一条独立的测量命令。
    /// <para>
    /// 设备事务严格按“投图 → Arm 弹窗监视 → 发送请求 → 忽略 Run 中间态、
    /// 确认/关闭该张弹窗 → 等待最终 OK → 下一张”推进。监视器在一张图的
    /// 命令事务窗口内运行，并在进入下一张前停止，避免残留弹窗跨事务消费。
    /// </para>
    /// </summary>
    private async Task RunCrosstalkPopupProjectAsync(
        TestPlanConfiguration configuration,
        TestProject project,
        int round,
        int iteration,
        List<TestMeasurementRecord> records,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<TestStep> ordered = OrderCrosstalkSteps(project.Steps, project.CrosstalkStartIndex);
        if (ordered.Count < 3 || ordered.Count % 2 == 0)
            throw new InvalidDataException("串扰图卡数量必须为大于等于 3 的奇数，且最后一张为本底图。");

        TimeSpan popupTimeout = TimeSpan.FromSeconds(Math.Clamp(project.PopupTimeoutSeconds, 1, 600));
        int sequence = 0;
        await _log($"串扰已启用弹窗序列：每张图卡独立发送测量并等待最终 OK（最长 {popupTimeout.TotalSeconds:0} 秒）。")
            .ConfigureAwait(false);

        foreach (TestStep sourceStep in ordered)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string imagePath = sourceStep.ResolveImagePath(configuration.ImageDirectory);
            if (string.IsNullOrWhiteSpace(imagePath))
                throw new InvalidDataException($"串扰第 {sequence + 1} 张图卡路径为空。");

            int position = sequence + 1;
            await _log($"串扰图卡 {position}/{ordered.Count}：开始投图：{sourceStep.DisplayName}")
                .ConfigureAwait(false);
            await _projector.ProjectAsync(imagePath, configuration.ProjectionMode, cancellationToken)
                .ConfigureAwait(false);
            await DelayAsync(sourceStep.StabilizeDelayMs, cancellationToken).ConfigureAwait(false);
            await _log($"串扰图卡 {position}/{ordered.Count}：图卡已切换：{imagePath}")
                .ConfigureAwait(false);

            string request = NormalizeRequest(sourceStep.MeasurementRequest);
            using var popupCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var runReceived = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var finalResponseAtUtc = new TaskCompletionSource<DateTime>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            // Link the intermediate-state gate to the transaction token.
            // On timeout, disconnect, stop, or cancellation there may be
            // no future Run packet; canceling this TCS keeps the failure
            // path from waiting forever for an impossible event.
            using CancellationTokenRegistration stageCancellation =
                popupCancellation.Token.Register(() =>
                {
                    runReceived.TrySetCanceled(popupCancellation.Token);
                    finalResponseAtUtc.TrySetCanceled(popupCancellation.Token);
                });
            bool transactionArmed = false;
            EventHandler<string>? stageObserver = null;
            stageObserver = (_, response) =>
            {
                // The server serializes request/response transactions, but
                // keep an explicit arm flag so a queued packet from the
                // previous transaction cannot satisfy this image's gate.
                if (!Volatile.Read(ref transactionArmed)) return;
                if (CommandResponseMatcher.IsIntermediate(request, response))
                {
                    if (runReceived.TrySetResult(true))
                        _ = _log($"串扰图卡 {position}/{ordered.Count}：收到 Run 中间返回：{response}");
                }
                else if (CommandResponseMatcher.Classify(request, response) == ResponseClassification.Success)
                {
                    finalResponseAtUtc.TrySetResult(DateTime.UtcNow);
                }
            };

            // Subscribe both the TCP stage observer and the popup coordinator
            // before starting the monitor.  Otherwise the monitor can publish
            // a fast first dialog during its initial polling cycles while no
            // consumer is attached, leaving this image waiting until timeout.
            _server.MessageReceived += stageObserver;
            try
            {
                await using var coordinator = new FixedPopupSequenceCoordinator(
                    _monitor,
                    _projector,
                    _log,
                    configuration.ProjectionMode,
                    popupTimeout,
                    projectOnDialog: false,
                    beforeConfirm: configuration.AutoConfirmPopups
                        ? token => runReceived.Task.WaitAsync(token)
                        : null);

                // 图卡已经在发送前投影；协调器只负责消费一个弹窗并执行
                // 确定/Enter/关闭回退，不再次切换屏幕内容。
                var popupStep = new TestStep
                {
                    Order = sourceStep.Order,
                    Name = sourceStep.Name,
                    ImagePath = imagePath,
                    StabilizeDelayMs = 0,
                    MeasurementRequest = sourceStep.MeasurementRequest
                };
                coordinator.Arm(new[] { popupStep }, configuration.AutoConfirmPopups, popupCancellation.Token);

                // Arm/subscribe first, then open the per-command monitoring
                // window.  Start intentionally snapshots dialogs already on
                // screen; requeue them once so an MRTEST confirmation that
                // appeared at the boundary is not suppressed as an old dialog.
                await StartDialogMonitoringAsync(configuration).ConfigureAwait(false);
                _monitor.RequeueCurrentDialogs();

                await _log($"串扰图卡 {position}/{ordered.Count}：发送测量：{request}；等待 Run/弹窗确认/最终 OK")
                    .ConfigureAwait(false);
                // Arm the stage observer immediately before the send.  It is
                // already subscribed, so a very fast Run/OK response cannot
                // race subscription; the flag still rejects pre-send noise.
                Volatile.Write(ref transactionArmed, true);
                Task<string> responseTask = _server.SendAndWaitForCompletionAsync(
                    request, popupTimeout, popupCancellation.Token);
                Task popupTask = coordinator.Completion;
                DateTime completedUtc = await AwaitCrosstalkPopupAndResponseAsync(
                    responseTask,
                    popupTask,
                    configuration.AutoConfirmPopups
                        ? runReceived.Task
                        : null,
                    finalResponseAtUtc.Task,
                    popupTimeout,
                    popupCancellation,
                    coordinator,
                    position,
                    ordered.Count).ConfigureAwait(false);

                records.Add(new TestMeasurementRecord(++sequence, project.Name, imagePath, completedUtc));
                await _log($"串扰图卡 {position}/{ordered.Count}：已收到最终 OK（{completedUtc.ToLocalTime():HH:mm:ss.fff}），进入下一张。")
                    .ConfigureAwait(false);
            }
            finally
            {
                // Always detach the per-transaction observer, including
                // validation/send/timeout failures; otherwise a later image
                // could consume an old closure and leak handlers.
                Volatile.Write(ref transactionArmed, false);
                _server.MessageReceived -= stageObserver;
                // StopAsync 会等待轮询任务退出并清除已见句柄；这一步放在
                // 每个命令事务末尾，防止上一张弹窗影响下一张图卡。即使
                // StartDialogMonitoringAsync 在日志阶段抛错，也会安全清理。
                await StopDialogMonitoringAsync(logWhenStopped: true).ConfigureAwait(false);
            }

            if (sequence < ordered.Count)
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
        }

        await _log($"串扰弹窗序列完成：{ordered.Count} 张图卡均已收到最终 OK。")
            .ConfigureAwait(false);
    }

    private static async Task<DateTime> AwaitCrosstalkPopupAndResponseAsync(
        Task<string> responseTask,
        Task popupTask,
        Task? runTask,
        Task<DateTime> finalResponseAtUtc,
        TimeSpan timeout,
        CancellationTokenSource popupCancellation,
        FixedPopupSequenceCoordinator coordinator,
        int position,
        int total)
    {
        using var timeoutCancellation = new CancellationTokenSource();
        Task timeoutTask = Task.Delay(timeout, timeoutCancellation.Token);
        // Manual-confirm mode deliberately has no Run gate.  Do not put a
        // pre-completed placeholder task into WhenAny: it would win every
        // iteration and turn the wait into a tight CPU loop until another
        // task happens to complete.  The wait set is therefore built per
        // mode, while the success check below uses the same requirement.
        bool runRequired = runTask is not null;
        try
        {
            while (true)
            {
                Task completed = runRequired
                    ? await Task.WhenAny(responseTask, popupTask, runTask!, timeoutTask).ConfigureAwait(false)
                    : await Task.WhenAny(responseTask, popupTask, timeoutTask).ConfigureAwait(false);
                if (ReferenceEquals(completed, timeoutTask))
                {
                    string pendingDescription = runRequired
                        ? "Run、弹窗确认和最终 OK"
                        : "弹窗关闭和最终 OK";
                    throw new TimeoutException(
                        $"串扰第 {position}/{total} 张图卡在 {timeout.TotalSeconds:0} 秒内未完成{pendingDescription}。");
                }

                // 传播任一事务的失败/取消，并主动取消另一侧，避免
                // 响应失败后仍无限等待一个永远不会出现的弹窗。
                if (completed.IsFaulted || completed.IsCanceled)
                    await completed.ConfigureAwait(false);

                if (responseTask.IsCompletedSuccessfully && popupTask.IsCompletedSuccessfully &&
                    (!runRequired || runTask!.IsCompletedSuccessfully))
                {
                    await responseTask.ConfigureAwait(false);
                    await popupTask.ConfigureAwait(false);
                    if (runRequired) await runTask!.ConfigureAwait(false);
                    // The observer normally captures the exact receive time.
                    // Keep a defensive fallback for a custom server that
                    // completes its task without raising MessageReceived.
                    return finalResponseAtUtc.IsCompletedSuccessfully
                        ? await finalResponseAtUtc.ConfigureAwait(false)
                        : DateTime.UtcNow;
                }
            }
        }
        catch
        {
            popupCancellation.Cancel();
            coordinator.Fail(new InvalidOperationException(
                $"串扰第 {position}/{total} 张图卡的弹窗/测量事务未完成。"));
            try
            {
                if (runRequired)
                    await Task.WhenAll(responseTask, popupTask, runTask!).ConfigureAwait(false);
                else
                    await Task.WhenAll(responseTask, popupTask).ConfigureAwait(false);
            }
            catch { /* 保留最先发生的原始异常。 */ }
            throw;
        }
        finally
        {
            timeoutCancellation.Cancel();
        }
    }

    private async Task RunPopupProjectAsync(
        TestPlanConfiguration configuration,
        TestProject project,
        int round,
        int iteration,
        List<TestMeasurementRecord> records,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<TestStep> steps = project.Steps.OrderBy(s => s.Order)
            .Select(s => new TestStep
            {
                Order = s.Order, Name = s.Name, ImagePath = s.ResolveImagePath(configuration.ImageDirectory),
                StabilizeDelayMs = configuration.PopupStabilizeDelayMs, MeasurementRequest = s.MeasurementRequest
            }).ToArray();
        if (steps.Count == 0) throw new InvalidOperationException($"弹窗项目“{project.Name}”没有固定图卡步骤。");
        TimeSpan popupTimeout = TimeSpan.FromSeconds(Math.Clamp(project.PopupTimeoutSeconds, 1, 600));
        // Subscribe and arm the coordinator before starting the polling task.
        // A very fast MRTEST build can create its first confirmation window
        // immediately after the request; starting the monitor first used to
        // leave a small interval in which that event was published with no
        // consumer.  RequeueCurrentDialogs below also covers a dialog that
        // appeared between Start()'s initial snapshot and this call.
        using var popupCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        await using var coordinator = new FixedPopupSequenceCoordinator(
            _monitor,
            _projector,
            _log,
            configuration.ProjectionMode,
            popupTimeout);
        coordinator.Arm(steps, configuration.AutoConfirmPopups, popupCancellation.Token);
        await StartDialogMonitoringAsync(configuration).ConfigureAwait(false);
        _monitor.RequeueCurrentDialogs();
        try
        {
            string request = NormalizeRequest(steps[0].MeasurementRequest);
            await _log($"发送测量：{request}；等待最终完成返回（最长 {popupTimeout.TotalSeconds:0} 秒）")
                .ConfigureAwait(false);
            Task<string> responseTask = _server.SendAndWaitForCompletionAsync(
                request,
                popupTimeout,
                popupCancellation.Token);
            try
            {
                Task popupTask = coordinator.Completion;
                Task first = await Task.WhenAny(responseTask, popupTask).ConfigureAwait(false);
                if (first.IsFaulted || first.IsCanceled)
                {
                    popupCancellation.Cancel();
                    try { await Task.WhenAll(responseTask, popupTask).ConfigureAwait(false); } catch { }
                    await first.ConfigureAwait(false);
                }
                await Task.WhenAll(responseTask, popupTask).ConfigureAwait(false);
            }
            catch
            {
                popupCancellation.Cancel();
                coordinator.Fail(new InvalidOperationException("弹窗序列未完成，已停止当前项目。"));
                throw;
            }
        }
        finally
        {
            await StopDialogMonitoringAsync(logWhenStopped: true).ConfigureAwait(false);
        }
        DateTime completed = DateTime.UtcNow;
        for (int i = 0; i < steps.Count; i++)
            records.Add(new TestMeasurementRecord(i + 1, project.Name, steps[i].ResolveImagePath(configuration.ImageDirectory), completed));
        await _log($"弹窗项目完成：{project.Name}（{steps.Count} 张固定图卡）").ConfigureAwait(false);
    }

    private async Task<DateTime> SendMeasurementAsync(
        TestPlanConfiguration configuration,
        string request,
        CancellationToken cancellationToken)
    {
        if (!CommandResponseMatcher.SupportsRequest(request))
            throw new FormatException($"不支持等待完成的测量报文：{request}");
        await StartDialogMonitoringAsync(configuration).ConfigureAwait(false);
        try
        {
            await _log($"发送测量：{request}；等待最终完成返回（最长 600 秒）").ConfigureAwait(false);
            await _server.SendAndWaitForCompletionAsync(
                request, MessageProtocol.MeasurementResponseTimeout, cancellationToken).ConfigureAwait(false);
            return DateTime.UtcNow;
        }
        finally
        {
            await StopDialogMonitoringAsync(logWhenStopped: true).ConfigureAwait(false);
        }
    }

    private async Task StartDialogMonitoringAsync(TestPlanConfiguration configuration)
    {
        // 每条测量报文建立独立窗口期。先等待上一窗口期彻底退出，再按当前
        // MRTEST 路径重新绑定 PID，防止跨测试项保留旧窗口或旧实例状态。
        await _monitor.StopAsync().ConfigureAwait(false);
        _monitor.Start(configuration.MrTestExecutablePath);
        await _log("测量命令窗口已启动 MRTEST 弹窗监视。").ConfigureAwait(false);
    }

    private async Task StopDialogMonitoringAsync(bool logWhenStopped)
    {
        bool wasRunning = _monitor.IsRunning;
        await _monitor.StopAsync().ConfigureAwait(false);
        if (wasRunning && logWhenStopped)
            await _log("测量事务结束，MRTEST 弹窗监视已停止。").ConfigureAwait(false);
    }

    private static void ValidateProjects(TestPlanConfiguration configuration, IReadOnlyList<TestProject> projects)
    {
        var errors = new List<string>();
        if (projects.Any(p => p.Kind == TestProjectKind.Crosstalk) && string.IsNullOrWhiteSpace(configuration.ExportDirectory))
            errors.Add("串扰项目需要设置 ExportFile 导出目录。");
        if (projects.Any(p => p.Kind == TestProjectKind.Crosstalk) && string.IsNullOrWhiteSpace(configuration.OutputDirectory))
            errors.Add("串扰项目需要设置结果目录。");
        foreach (TestProject project in projects)
        {
            int imageStepCount = project.Steps.Count(s => !string.IsNullOrWhiteSpace(s.ImagePath));
            if (project.Kind == TestProjectKind.Crosstalk &&
                (imageStepCount < 3 || imageStepCount % 2 == 0))
                errors.Add($"{project.Name}：串扰图卡必须是大于等于 3 的奇数。");
            if (RequiresMrTestPopupSequence(project) && project.Steps.Count == 0)
                errors.Add($"{project.Name}：弹窗项目没有固定图卡。");
            foreach (TestStep step in project.Steps)
            {
                if (RequiresMrTestPopupSequence(project) && string.IsNullOrWhiteSpace(step.ImagePath))
                    errors.Add($"{project.Name}/{step.DisplayName}：弹窗序列图卡路径不能为空。");
                if (!string.IsNullOrWhiteSpace(step.ImagePath))
                {
                    string path = step.ResolveImagePath(configuration.ImageDirectory);
                    if (!File.Exists(path)) errors.Add($"{project.Name}/{step.DisplayName}：找不到图卡 {path}");
                }
                string request = string.IsNullOrWhiteSpace(step.MeasurementRequest)
                    ? MessageProtocol.DefaultMeasurementRequest : step.MeasurementRequest.Trim();
                if (!CommandResponseMatcher.SupportsRequest(request))
                    errors.Add($"{project.Name}/{step.DisplayName}：测量报文无法等待完成 {request}");
            }
        }
        if (errors.Count > 0)
            throw new InvalidOperationException("测试计划校验失败：\r\n" + string.Join("\r\n", errors.Take(20)));
    }

    private static string NormalizeRequest(string? request)
    {
        string value = string.IsNullOrWhiteSpace(request)
            ? MessageProtocol.DefaultMeasurementRequest
            : MessageProtocol.MigrateMeasurementRequest(request);
        if (!MessageProtocol.TryValidateMessage(value, out string normalized, out string error))
            throw new FormatException(error);
        // 再做一次迁移，防止调用方传入已 Normalize 过的旧配置或旧版本
        // 自定义步骤；真正写入 TCP 的手动测量请求始终不带 Run。
        return MessageProtocol.MigrateMeasurementRequest(normalized);
    }

    private static IReadOnlyList<TestStep> OrderCrosstalkSteps(IReadOnlyList<TestStep> source, int startIndex)
    {
        TestStep[] steps = source.Where(s => !string.IsNullOrWhiteSpace(s.ImagePath)).OrderBy(s => s.Order).ToArray();
        if (steps.Length == 0) return steps;
        // 与参考串扰程序一致：最后一张保持为本底，其余图卡从可配置起点循环投影。
        int foregroundCount = steps.Length - 1;
        int start = foregroundCount == 0 ? 0 : ((startIndex % foregroundCount) + foregroundCount) % foregroundCount;
        return steps.Take(foregroundCount).Skip(start).Concat(steps.Take(start)).Append(steps[^1]).ToArray();
    }

    private static Task DelayAsync(int milliseconds, CancellationToken cancellationToken) =>
        milliseconds <= 0 ? Task.CompletedTask : Task.Delay(milliseconds, cancellationToken);

    private void Report(TestRunState state, int round, int totalRounds, string project,
        int iteration, int totalIterations, string message)
    {
        ProgressChanged?.Invoke(this, new TestRunProgress(state, round, totalRounds, project,
            iteration, totalIterations, message));
        _ = _log(message);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _activeCancellation?.Cancel();
        await _runLock.WaitAsync().ConfigureAwait(false);
        _runLock.Release();
        _runLock.Dispose();
    }
}
