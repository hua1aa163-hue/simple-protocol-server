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
            ExportSnapshot? snapshot = project.Kind == TestProjectKind.Crosstalk
                ? _dataProcessor.CaptureExportSnapshot(configuration.ExportDirectory)
                : null;
            var records = new List<TestMeasurementRecord>();

            // MRTEST 的 FOV 与 9 点亮度均匀性配方本身就是 9Point
            // 流程。即使旧配置中的“弹窗序列”仍为 false，测量过程中
            // 也会弹出“切换9Point图像”确认框；若继续走顺序项目分支，
            // 监视器只能记录窗口而没有消费者，测试会卡在该弹窗上。
            // 将这两类配方视为隐式弹窗项目，使用同一固定图卡队列，
            // 不改变用户在界面中编辑/保存的 PopupDriven 字段。
            if (RequiresMrTestPopupSequence(project))
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

            TestDataProcessingResult result = await _dataProcessor.ProcessAsync(
                project,
                configuration.ExportDirectory,
                configuration.OutputDirectory,
                startedUtc,
                records,
                new Progress<string>(message => _ = _log(message)),
                cancellationToken,
                snapshot).ConfigureAwait(false);
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
        await StartDialogMonitoringAsync(configuration).ConfigureAwait(false);
        try
        {
            await using var coordinator = new FixedPopupSequenceCoordinator(
                _monitor,
                _projector,
                _log,
                configuration.ProjectionMode,
                popupTimeout);
            using var popupCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            coordinator.Arm(steps, configuration.AutoConfirmPopups, popupCancellation.Token);
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
            if (project.Kind == TestProjectKind.Crosstalk && project.PopupDriven)
                errors.Add($"{project.Name}：串扰项目使用多图逐张确认流程，不能启用弹窗序列模式。");
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
