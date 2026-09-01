using System.Net;
using System.Net.Sockets;
using System.Text;
using AutoTestClient.Networking;
using AutoTestClient.Protocol;
using AutoTestClient.Models;
using AutoTestClient.DataProcessing;
using AutoTestClient.Workflow;
using AutoTestClient.Projection;
using AutoTestClient.Monitoring;

var failures = new List<string>();
void Check(bool condition, string name)
{
    if (!condition) failures.Add(name);
    Console.WriteLine($"{(condition ? "PASS" : "FAIL")} {name}");
}

Check(MessageProtocol.DefaultMeasurementRequest == "&|Meas|A|M|@", "默认测量报文精确匹配");
Check(MessageProtocol.DefaultMeasurementRunningResponse == "&|Meas|A|M|Run|@", "开始测量返回 Run");
Check(MessageProtocol.TryBuildMessage(CommandType.SingleManual, string.Empty, out string builtManual, out _) &&
      builtManual == MessageProtocol.DefaultMeasurementRequest, "标准手动命令生成不带 Run");
Check(CommandResponseMatcher.Classify(MessageProtocol.DefaultMeasurementRequest, "&|Meas|A|M|Run|@") == ResponseClassification.Ignored, "Run 是中间态");
Check(CommandResponseMatcher.IsSuccess(MessageProtocol.DefaultMeasurementRequest, "&|Meas|A|OK|@"), "最终 OK 成功");
Check(CommandResponseMatcher.IsFailure(MessageProtocol.DefaultMeasurementRequest, "&|Meas|A|NG|@"), "最终 NG 失败");
Check(MessageProtocol.MigrateMeasurementRequest(MessageProtocol.PreviousDefaultMeasurementRequest) == MessageProtocol.DefaultMeasurementRequest, "旧默认测量请求可迁移");
Check(MessageProtocol.MigrateMeasurementRequest("&|Meas|S|M|Run|@") == "&|Meas|S|M|Run|@", "自定义连续测量请求不被误迁移");
var frames = new MessageFrameParser();
Check(frames.Append("noise&|Meas|A|").Count == 0, "半包不提前返回");
var completedFrames = frames.Append("M|Run|@&|Meas|A|OK|@");
Check(completedFrames.SequenceEqual(new[] { "&|Meas|A|M|Run|@", "&|Meas|A|OK|@" }), "半包与粘包解析");

var crosstalkInput = new double[CrosstalkDataProcessor.ExpectedBrightnessRows, 3];
for (int row = 0; row < crosstalkInput.GetLength(0); row++)
{
    crosstalkInput[row, 0] = 10.01; crosstalkInput[row, 1] = 11.0; crosstalkInput[row, 2] = 10.0;
}
var crosstalk = CrosstalkDataProcessor.Calculate(crosstalkInput);
Check(crosstalk.ValuesForStatistics.GetLength(0) == 19 && crosstalk.ValuesForStatistics.GetLength(1) == 32, "串扰矩阵 19x32");
Check(Math.Abs(crosstalk.Mean - 0.01) < 1e-9, "串扰比值和 3% 异常阈值");
Check(MessageProtocol.GetResponseTimeout(MessageProtocol.DefaultMeasurementRequest) == TimeSpan.FromSeconds(600), "测量等待上限 600 秒");
var defaults = new TestPlanConfiguration(); defaults.Normalize();
Check(defaults.Projects.Count == 5 && defaults.Projects[0].Kind == TestProjectKind.Fov && defaults.Projects[^1].Kind == TestProjectKind.Crosstalk, "默认项目顺序");

await using (var server = new TcpMessageServer())
{
    await server.StartAsync(IPAddress.Loopback, 0);
    using var peer = new TcpClient();
    await peer.ConnectAsync(IPAddress.Loopback, server.Port);
    using NetworkStream stream = peer.GetStream();
    using var reader = new StreamReader(stream, Encoding.UTF8, false, 4096, leaveOpen: true);
    using var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, leaveOpen: true) { AutoFlush = true, NewLine = string.Empty };
    // 等待服务端完成连接状态更新。
    for (int i = 0; i < 20 && !server.IsConnected; i++) await Task.Delay(25);
    // 即使调用方仍传入旧版本默认值，网络层也必须只写出当前请求。
    Task<string> wait = server.SendAndWaitForCompletionAsync(MessageProtocol.PreviousDefaultMeasurementRequest, TimeSpan.FromSeconds(2));
    string request = await reader.ReadToEndUntilFrameAsync();
    Check(request == MessageProtocol.DefaultMeasurementRequest, "服务端发送迁移后的精确测量报文");
    await writer.WriteAsync("&|Meas|A|M|Run|@&|Meas|A|OK|@");
    string response = await wait;
    Check(response == "&|Meas|A|OK|@", "服务端等待最终 OK");
}

// 验证“整套 N 轮 × 项目 N 次”的执行展开，不触碰真实屏幕或 MRTEST。
string integrationRoot = Path.Combine(Path.GetTempPath(), "AutoTestClientSmoke", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(integrationRoot);
string image1 = Path.Combine(integrationRoot, "one.png");
string image2 = Path.Combine(integrationRoot, "two.png");
File.WriteAllBytes(image1, new byte[] { 0 }); File.WriteAllBytes(image2, new byte[] { 0 });
await using (var integrationServer = new TcpMessageServer())
{
    await integrationServer.StartAsync(IPAddress.Loopback, 0);
    using var peer = new TcpClient(); await peer.ConnectAsync(IPAddress.Loopback, integrationServer.Port);
    using NetworkStream stream = peer.GetStream();
    using var reader = new StreamReader(stream, Encoding.UTF8, false, 4096, leaveOpen: true);
    using var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, leaveOpen: true) { AutoFlush = true, NewLine = string.Empty };
    for (int i = 0; i < 20 && !integrationServer.IsConnected; i++) await Task.Delay(25);
    var projector = new FakeProjector(); var processor = new FakeProcessor(); await using var monitor = new MrTestDialogMonitor();
    await using var runner = new TestPlanRunner(integrationServer, projector, monitor, processor);
    var plan = new TestPlanConfiguration { ImageDirectory = integrationRoot, WholePlanRepeatCount = 2, Projects = new List<TestProject> {
        new() { Enabled = true, Order = 1, Name = "集成通用项目", Kind = TestProjectKind.Generic, RepeatCount = 2, Steps = new() {
            new() { Order = 1, Name = "1", ImagePath = image1 }, new() { Order = 2, Name = "2", ImagePath = image2 } } } } };
    Task responder = Task.Run(async () => {
        for (int count = 0; count < 8; count++) {
            string request = await reader.ReadToEndUntilFrameAsync();
            if (request != MessageProtocol.DefaultMeasurementRequest) throw new InvalidOperationException("集成测试收到错误请求：" + request);
            await writer.WriteAsync("&|Meas|A|M|Run|@&|Meas|A|OK|@");
        }
    });
    await runner.RunAsync(plan);
    await responder;
    Check(projector.Paths.Count == 8 && processor.Calls.Count == 4, "整套/项目重复次数展开");
}

await using (var cancelServer = new TcpMessageServer())
{
    await cancelServer.StartAsync(IPAddress.Loopback, 0);
    using var peer = new TcpClient(); await peer.ConnectAsync(IPAddress.Loopback, cancelServer.Port);
    using NetworkStream stream = peer.GetStream();
    using var reader = new StreamReader(stream, Encoding.UTF8, false, 4096, leaveOpen: true);
    for (int i = 0; i < 20 && !cancelServer.IsConnected; i++) await Task.Delay(25);
    using var cts = new CancellationTokenSource();
    Task pending = cancelServer.SendAndWaitForCompletionAsync(MessageProtocol.DefaultMeasurementRequest, TimeSpan.FromSeconds(600), cts.Token);
    _ = await reader.ReadToEndUntilFrameAsync(); cts.Cancel();
    bool cancelled = false;
    try { await pending; } catch (OperationCanceledException) { cancelled = true; }
    Check(cancelled, "取消不会等待 600 秒");
}

if (failures.Count > 0)
{
    Console.Error.WriteLine($"{failures.Count} smoke test(s) failed.");
    return 1;
}
Console.WriteLine("All smoke tests passed.");
return 0;

static class StreamExtensions
{
    public static async Task<string> ReadToEndUntilFrameAsync(this StreamReader reader)
    {
        var parser = new MessageFrameParser();
        char[] buffer = new char[256];
        while (true)
        {
            int count = await reader.ReadAsync(buffer);
            if (count == 0) throw new EndOfStreamException();
            var messages = parser.Append(buffer.AsSpan(0, count));
            if (messages.Count > 0) return messages[0];
        }
    }
}

sealed class FakeProjector : IImageProjector
{
    public List<string> Paths { get; } = new();
    public Task ProjectAsync(string imagePath, ProjectionMode mode, CancellationToken cancellationToken = default) { Paths.Add(imagePath); return Task.CompletedTask; }
    public Task ClearAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

sealed class FakeProcessor : ITestDataProcessor
{
    public List<TestProject> Calls { get; } = new();
    public ExportSnapshot CaptureExportSnapshot(string exportDirectory) => throw new NotSupportedException();
    public Task<TestDataProcessingResult> ProcessAsync(TestProject project, string exportDirectory, string outputDirectory, DateTime testStartedUtc, IReadOnlyList<TestMeasurementRecord> completedTests, IProgress<string>? progress, CancellationToken cancellationToken, ExportSnapshot? exportSnapshot = null)
    {
        Calls.Add(project);
        return Task.FromResult(new TestDataProcessingResult(project.Name, project.Kind, Array.Empty<TestMetric>()));
    }
}
