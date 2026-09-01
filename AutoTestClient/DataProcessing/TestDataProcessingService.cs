using AutoTestClient.Models;
using System.Globalization;

namespace AutoTestClient.DataProcessing;

/// <summary>单次设备测量与图卡的关联记录。</summary>
public sealed record TestMeasurementRecord(
    int Sequence,
    string ProjectName,
    string ImagePath,
    DateTime CompletedUtc);

/// <summary>显示层统一使用的指标值；Source 用于记录字段来源，便于后续确认导出位置。</summary>
public sealed record TestMetric(
    string Key,
    string DisplayName,
    string Value,
    string Source,
    string Status = "已计算");

/// <summary>串扰测试开始前的导出目录快照。只由本程序集内部读取具体指纹。</summary>
public sealed class ExportSnapshot
{
    internal ExportSnapshot(IReadOnlyDictionary<string, ExportFolderFingerprint> value) => Value = value;
    internal IReadOnlyDictionary<string, ExportFolderFingerprint> Value { get; }
}

/// <summary>项目数据处理结果。首版非串扰项目返回待确认占位状态，不伪造数值。</summary>
public sealed class TestDataProcessingResult
{
    public TestDataProcessingResult(
        string projectName,
        TestProjectKind kind,
        IReadOnlyList<TestMetric> metrics,
        string? outputDirectory = null,
        string? detail = null,
        string? rawWorkbookPath = null,
        string? crosstalkWorkbookPath = null,
        string? heatmapPath = null)
    {
        ProjectName = projectName;
        Kind = kind;
        Metrics = metrics;
        OutputDirectory = outputDirectory;
        Detail = detail;
        RawWorkbookPath = rawWorkbookPath;
        CrosstalkWorkbookPath = crosstalkWorkbookPath;
        HeatmapPath = heatmapPath;
    }

    public string ProjectName { get; }
    public TestProjectKind Kind { get; }
    public IReadOnlyList<TestMetric> Metrics { get; }
    public string? OutputDirectory { get; }
    public string? Detail { get; }
    public string? RawWorkbookPath { get; }
    public string? CrosstalkWorkbookPath { get; }
    public string? HeatmapPath { get; }
}

/// <summary>项目数据处理扩展点；后续确认字段后只需新增实现，不改测试流程。</summary>
public interface ITestDataProcessor
{
    ExportSnapshot CaptureExportSnapshot(string exportDirectory);

    Task<TestDataProcessingResult> ProcessAsync(
        TestProject project,
        string exportDirectory,
        string outputDirectory,
        DateTime testStartedUtc,
        IReadOnlyList<TestMeasurementRecord> completedTests,
        IProgress<string>? progress,
        CancellationToken cancellationToken,
        ExportSnapshot? exportSnapshot = null);
}

/// <summary>
/// 首版统一数据处理入口。
/// <para>
/// 串扰直接复用参考项目的完整归档、Excel、矩阵、统计和热图算法；
/// FOV、黑白对比度、亮度均匀性、色域先登记字段预留，等用户确认导出位置后接入。
/// </para>
/// </summary>
public sealed class TestDataProcessingService : ITestDataProcessor
{
    public ExportSnapshot CaptureExportSnapshot(string exportDirectory)
    {
        Directory.CreateDirectory(exportDirectory);
        return new ExportSnapshot(CrosstalkDataProcessor.CaptureSnapshot(exportDirectory));
    }

    public async Task<TestDataProcessingResult> ProcessAsync(
        TestProject project,
        string exportDirectory,
        string outputDirectory,
        DateTime testStartedUtc,
        IReadOnlyList<TestMeasurementRecord> completedTests,
        IProgress<string>? progress,
        CancellationToken cancellationToken,
        ExportSnapshot? exportSnapshot = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(completedTests);

        if (project.Kind != TestProjectKind.Crosstalk)
        {
            return new TestDataProcessingResult(
                project.Name,
                project.Kind,
                ReservedMetrics(project.Kind),
                detail: "已完成测试流程；该项目的导出字段/报告格式待确认后接入。");
        }

        if (completedTests.Count == 0)
            throw new InvalidDataException("串扰项目没有可关联的完成记录。");

        Directory.CreateDirectory(exportDirectory);
        Directory.CreateDirectory(outputDirectory);
        var snapshot = exportSnapshot?.Value ?? CrosstalkDataProcessor.CaptureSnapshot(exportDirectory);
        var records = completedTests.Select(item => new CrosstalkTestRecord(
            item.Sequence, item.ImagePath, item.CompletedUtc)).ToArray();
        CrosstalkProcessingResult result = await CrosstalkDataProcessor.ProcessCompletedTestAsync(
            exportDirectory,
            outputDirectory,
            testStartedUtc,
            snapshot,
            records,
            progress,
            cancellationToken).ConfigureAwait(false);

        string percent(double value) => $"{value * 100:0.###}%";
        var metrics = new List<TestMetric>
        {
            new("crosstalk.files", "导出文件数", result.SourceFileCount.ToString(CultureInfo.InvariantCulture), "ExportFile 一级文件夹"),
            new("crosstalk.max", "串扰最大值", percent(result.Calculation.Maximum), "Brightness C列 / MATLAB等价算法"),
            new("crosstalk.min", "串扰最小值", percent(result.Calculation.Minimum), "Brightness C列 / MATLAB等价算法"),
            new("crosstalk.mean", "串扰平均值", percent(result.Calculation.Mean), "Brightness C列 / MATLAB等价算法")
        };
        return new TestDataProcessingResult(
            project.Name,
            project.Kind,
            metrics,
            result.OutputDirectory,
            $"已处理 {result.SourceFileCount} 份导出数据；结果已写入 Excel、CSV 和热图。",
            result.RawWorkbookPath,
            result.CrosstalkWorkbookPath,
            result.HeatmapPath);
    }

    private static IReadOnlyList<TestMetric> ReservedMetrics(TestProjectKind kind) => kind switch
    {
        TestProjectKind.Fov => new[]
        {
            Reserved("fov.horizontal", "水平FOV"), Reserved("fov.vertical", "垂直FOV")
        },
        TestProjectKind.Contrast => new[] { Reserved("contrast.average", "平均对比度") },
        TestProjectKind.BrightnessUniformity => new[]
        {
            Reserved("brightness.average", "平均亮度"), Reserved("brightness.uniformity", "均匀性")
        },
        TestProjectKind.Gamut => new[]
        {
            Reserved("gamut.white.temperature", "白图色温"), Reserved("gamut.area", "色域")
        },
        _ => Array.Empty<TestMetric>()
    };

    private static TestMetric Reserved(string key, string name) =>
        new(key, name, "待确认", "MRTEST ExportFile 字段尚未确认", "预留");
}
