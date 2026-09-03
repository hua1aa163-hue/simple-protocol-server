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

/// <summary>
/// 测试开始前的导出快照。串扰读取一级目录指纹；普通 Excel 规则同时
/// 保存工作簿指纹，用于排除未变化的历史文件。具体指纹只在本程序集内读取。
/// </summary>
public sealed class ExportSnapshot
{
    internal ExportSnapshot(
        IReadOnlyDictionary<string, ExportFolderFingerprint> value,
        IReadOnlyDictionary<string, ResultWorkbookFingerprint>? workbookBaseline = null)
    {
        Value = value;
        WorkbookBaseline = workbookBaseline ??
            new Dictionary<string, ResultWorkbookFingerprint>(StringComparer.OrdinalIgnoreCase);
    }
    internal IReadOnlyDictionary<string, ExportFolderFingerprint> Value { get; }
    internal IReadOnlyDictionary<string, ResultWorkbookFingerprint> WorkbookBaseline { get; }
}

/// <summary>项目数据处理结果；非串扰项目按当前展示规则读取 MRTEST 明细工作簿。</summary>
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
        string? heatmapPath = null,
        CrosstalkCalculationResult? crosstalkCalculation = null,
        CrosstalkAnalysisOptions? crosstalkOptions = null)
    {
        ProjectName = projectName;
        Kind = kind;
        Metrics = metrics;
        OutputDirectory = outputDirectory;
        Detail = detail;
        RawWorkbookPath = rawWorkbookPath;
        CrosstalkWorkbookPath = crosstalkWorkbookPath;
        HeatmapPath = heatmapPath;
        CrosstalkCalculation = crosstalkCalculation;
        CrosstalkOptions = crosstalkOptions;
    }

    public string ProjectName { get; }
    public TestProjectKind Kind { get; }
    public IReadOnlyList<TestMetric> Metrics { get; }
    public string? OutputDirectory { get; }
    public string? Detail { get; }
    public string? RawWorkbookPath { get; }
    public string? CrosstalkWorkbookPath { get; }
    public string? HeatmapPath { get; }
    /// <summary>串扰 19×32 原始/掩膜矩阵；非串扰项目为 null。</summary>
    public CrosstalkCalculationResult? CrosstalkCalculation { get; }
    /// <summary>生成该结果时使用的串扰参数。</summary>
    public CrosstalkAnalysisOptions? CrosstalkOptions { get; }

    // 简短别名，便于界面代码在展示层读取，不改变旧 API。
    public CrosstalkCalculationResult? Calculation => CrosstalkCalculation;
    public CrosstalkAnalysisOptions? AnalysisOptions => CrosstalkOptions;
}

/// <summary>项目数据处理扩展点；后续确认字段后只需新增实现，不改测试流程。</summary>
public interface ITestDataProcessor
{
    ExportSnapshot CaptureExportSnapshot(string exportDirectory);

    /// <summary>
    /// 可选的通用 Excel 快照。旧的处理器不实现时返回 null，
    /// 内置处理器用它排除测试开始前已经存在的工作簿。
    /// </summary>
    ExportSnapshot? CaptureResultSnapshot(string exportDirectory) => null;

    Task<TestDataProcessingResult> ProcessAsync(
        TestProject project,
        string exportDirectory,
        string outputDirectory,
        DateTime testStartedUtc,
        IReadOnlyList<TestMeasurementRecord> completedTests,
        IProgress<string>? progress,
        CancellationToken cancellationToken,
        ExportSnapshot? exportSnapshot = null);

    /// <summary>
    /// 带串扰分析选项的扩展入口。默认实现转发旧接口，故旧的第三方处理器无需修改。
    /// </summary>
    Task<TestDataProcessingResult> ProcessAsync(
        TestProject project,
        string exportDirectory,
        string outputDirectory,
        DateTime testStartedUtc,
        IReadOnlyList<TestMeasurementRecord> completedTests,
        IProgress<string>? progress,
        CancellationToken cancellationToken,
        ExportSnapshot? exportSnapshot,
        CrosstalkAnalysisOptions? analysisOptions)
        => ProcessAsync(project, exportDirectory, outputDirectory, testStartedUtc,
            completedTests, progress, cancellationToken, exportSnapshot);

    /// <summary>
    /// 带展示规则的扩展入口。默认实现转发到旧的串扰选项重载，保持已有
    /// 第三方处理器（以及 SmokeTests 中的 FakeProcessor）源码兼容。
    /// </summary>
    Task<TestDataProcessingResult> ProcessAsync(
        TestProject project,
        string exportDirectory,
        string outputDirectory,
        DateTime testStartedUtc,
        IReadOnlyList<TestMeasurementRecord> completedTests,
        IProgress<string>? progress,
        CancellationToken cancellationToken,
        ExportSnapshot? exportSnapshot,
        CrosstalkAnalysisOptions? analysisOptions,
        IReadOnlyList<TestDataDisplayRule>? displayRules)
        => ProcessAsync(project, exportDirectory, outputDirectory, testStartedUtc,
            completedTests, progress, cancellationToken, exportSnapshot, analysisOptions);
}

/// <summary>
/// 首版统一数据处理入口。
/// <para>
/// 串扰直接复用参考项目的完整归档、Excel、矩阵、统计和热图算法；
/// 其余项目通过可编辑展示规则读取 MRTEST 生成的最深层 Excel 明细文件。
/// </para>
/// </summary>
public sealed class TestDataProcessingService : ITestDataProcessor
{
    /// <summary>未显式传参时使用的串扰参数；主界面可在运行前更新。</summary>
    public CrosstalkAnalysisOptions DefaultCrosstalkOptions { get; set; } =
        CrosstalkAnalysisOptions.Default;

    /// <summary>
    /// 非串扰项目等待 MRTEST 写完结果工作簿的时间。最终 OK 已收到后，
    /// 导出通常只需几百毫秒；可配置上限用于容纳现场写盘延迟。
    /// 设为零表示只尝试当前文件、不轮询等待。
    /// </summary>
    public TimeSpan NonCrosstalkExportWaitTimeout { get; set; } =
        TimeSpan.FromSeconds(30);

    public ExportSnapshot CaptureExportSnapshot(string exportDirectory)
    {
        string sourceDirectory = TestPlanConfiguration.ResolveExportDirectoryForRead(exportDirectory);
        Directory.CreateDirectory(sourceDirectory);
        return new ExportSnapshot(
            CrosstalkDataProcessor.CaptureSnapshot(sourceDirectory),
            ResultDisplayRuleEngine.CaptureWorkbookSnapshot(sourceDirectory));
    }

    public ExportSnapshot? CaptureResultSnapshot(string exportDirectory)
    {
        string sourceDirectory = TestPlanConfiguration.ResolveExportDirectoryForRead(exportDirectory);
        Directory.CreateDirectory(sourceDirectory);
        return new ExportSnapshot(
            new Dictionary<string, ExportFolderFingerprint>(StringComparer.OrdinalIgnoreCase),
            ResultDisplayRuleEngine.CaptureWorkbookSnapshot(sourceDirectory));
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
        => await ProcessAsync(project, exportDirectory, outputDirectory, testStartedUtc,
            completedTests, progress, cancellationToken, exportSnapshot,
            analysisOptions: null).ConfigureAwait(false);

    /// <summary>使用指定串扰选项处理项目；非串扰项目忽略该参数。</summary>
    public async Task<TestDataProcessingResult> ProcessAsync(
        TestProject project,
        string exportDirectory,
        string outputDirectory,
        DateTime testStartedUtc,
        IReadOnlyList<TestMeasurementRecord> completedTests,
        IProgress<string>? progress,
        CancellationToken cancellationToken,
        ExportSnapshot? exportSnapshot,
        CrosstalkAnalysisOptions? analysisOptions)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(completedTests);

        if (project.Kind != TestProjectKind.Crosstalk)
        {
            return await ProcessNonCrosstalkAsync(
                project,
                exportDirectory,
                testStartedUtc,
                progress,
                cancellationToken,
                displayRules: null,
                exportSnapshot: exportSnapshot).ConfigureAwait(false);
        }

        if (completedTests.Count == 0)
            throw new InvalidDataException("串扰项目没有可关联的完成记录。");

        string sourceDirectory = TestPlanConfiguration.ResolveExportDirectoryForRead(exportDirectory);
        Directory.CreateDirectory(sourceDirectory);
        Directory.CreateDirectory(outputDirectory);
        var snapshot = exportSnapshot?.Value ?? CrosstalkDataProcessor.CaptureSnapshot(sourceDirectory);
        CrosstalkAnalysisOptions options = (analysisOptions ?? DefaultCrosstalkOptions)
            .Normalize();
        var records = completedTests.Select(item => new CrosstalkTestRecord(
            item.Sequence, item.ImagePath, item.CompletedUtc)).ToArray();
        CrosstalkProcessingResult result = await CrosstalkDataProcessor.ProcessCompletedTestAsync(
            sourceDirectory,
            outputDirectory,
            testStartedUtc,
            snapshot,
            records,
            progress,
            cancellationToken,
            options).ConfigureAwait(false);

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
            result.HeatmapPath,
            result.Calculation,
            result.AnalysisOptions);
    }

    /// <summary>
    /// 使用指定展示规则处理项目；串扰仍由原有矩阵分析路径处理，规则只
    /// 作用于 FOV、对比度、均匀性、色域和畸变等 Excel 结果。
    /// </summary>
    public async Task<TestDataProcessingResult> ProcessAsync(
        TestProject project,
        string exportDirectory,
        string outputDirectory,
        DateTime testStartedUtc,
        IReadOnlyList<TestMeasurementRecord> completedTests,
        IProgress<string>? progress,
        CancellationToken cancellationToken,
        ExportSnapshot? exportSnapshot,
        CrosstalkAnalysisOptions? analysisOptions,
        IReadOnlyList<TestDataDisplayRule>? displayRules)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(completedTests);

        if (project.Kind == TestProjectKind.Crosstalk)
        {
            // Keep one implementation of the crosstalk archive/heatmap path;
            // display rules intentionally remain empty for crosstalk.
            return await ProcessAsync(
                project, exportDirectory, outputDirectory, testStartedUtc,
                completedTests, progress, cancellationToken, exportSnapshot,
                analysisOptions).ConfigureAwait(false);
        }

        return await ProcessNonCrosstalkAsync(
            project, exportDirectory, testStartedUtc, progress, cancellationToken,
            displayRules, exportSnapshot).ConfigureAwait(false);
    }

    private async Task<TestDataProcessingResult> ProcessNonCrosstalkAsync(
        TestProject project,
        string exportDirectory,
        DateTime testStartedUtc,
        IProgress<string>? progress,
        CancellationToken cancellationToken,
        IReadOnlyList<TestDataDisplayRule>? displayRules,
        ExportSnapshot? exportSnapshot)
    {
        // Generic projects have no built-in result schema. Do not scan an
        // arbitrary export directory or accidentally display another test's
        // workbook for them.
        IReadOnlyList<TestDataDisplayRule> rules = ResolveRules(project.Kind, displayRules);
        if (rules.Count == 0)
        {
            return new TestDataProcessingResult(
                project.Name,
                project.Kind,
                Array.Empty<TestMetric>(),
                detail: "已完成测试流程；该项目未配置启用的结果展示规则。");
        }

        string sourceDirectory = TestPlanConfiguration.ResolveExportDirectoryForRead(exportDirectory);
        if (!string.Equals(sourceDirectory, exportDirectory,
                StringComparison.OrdinalIgnoreCase))
            progress?.Report($"默认导出目录不存在，兼容读取旧目录：{sourceDirectory}");
        progress?.Report($"正在定位 {project.Name} 的 MRTEST 导出 Excel...");
        ResultWorkbookSelection? selection;
        TimeSpan timeout = NonCrosstalkExportWaitTimeout;
        if (timeout < TimeSpan.Zero) timeout = TimeSpan.Zero;
        if (timeout == TimeSpan.Zero)
        {
            selection = ResultDisplayRuleEngine.SelectWorkbook(
                sourceDirectory, project, testStartedUtc,
                excludedPaths: null,
                allowHistoricalFallback: false,
                recentTolerance: TimeSpan.FromSeconds(2),
                baseline: exportSnapshot?.WorkbookBaseline);
        }
        else
        {
            selection = await ResultDisplayRuleEngine.WaitForWorkbookAsync(
                sourceDirectory,
                project,
                testStartedUtc,
                timeout,
                cancellationToken,
                progress: progress,
                allowHistoricalFallback: false,
                recentTolerance: TimeSpan.FromSeconds(2),
                baseline: exportSnapshot?.WorkbookBaseline).ConfigureAwait(false);
        }

        if (selection is null)
        {
            string detail =
                $"未找到 {project.Name} 对应的 MRTEST 导出 Excel；已保留结果列，" +
                "请确认 ExportFile 路径和 MRTEST 是否完成写盘。";
            progress?.Report(detail);
            return new TestDataProcessingResult(
                project.Name,
                project.Kind,
                ReservedMetrics(project.Kind, rules),
                detail: detail);
        }

        try
        {
            IReadOnlyList<TestMetric> metrics = ResultDisplayRuleEngine.Extract(
                project, selection.Path, rules);
            string detail = selection.IsFallback
                ? $"已读取结果文件（使用最近可用文件回退）：{selection.Path}"
                : $"已读取 MRTEST 结果文件：{selection.Path}";
            progress?.Report(detail);
            return new TestDataProcessingResult(
                project.Name,
                project.Kind,
                metrics.Count == 0 ? ReservedMetrics(project.Kind, rules) : metrics,
                detail: detail,
                rawWorkbookPath: selection.Path,
                outputDirectory: Path.GetDirectoryName(selection.Path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                                   InvalidDataException or FormatException or
                                   KeyNotFoundException)
        {
            string detail = $"读取 MRTEST 结果文件失败：{ex.Message}（{selection.Path}）";
            progress?.Report(detail);
            return new TestDataProcessingResult(
                project.Name,
                project.Kind,
                ReservedMetrics(project.Kind, rules),
                detail: detail,
                rawWorkbookPath: selection.Path,
                outputDirectory: Path.GetDirectoryName(selection.Path));
        }
    }

    private static IReadOnlyList<TestDataDisplayRule> ResolveRules(
        TestProjectKind kind,
        IReadOnlyList<TestDataDisplayRule>? configured)
    {
        // null means the caller did not supply a rule set (legacy API), so use
        // built-ins. An explicitly empty list means the user intentionally
        // disabled all display rules and must remain empty.
        IEnumerable<TestDataDisplayRule> candidates = configured ??
            TestDataDisplayRule.CreateDefaultRules();
        var applicable = candidates
            .Where(rule => rule is not null && rule.Enabled &&
                           (rule.ProjectKind is null || rule.ProjectKind == kind))
            .OrderBy(rule => rule.Order)
            .ThenBy(rule => rule.Name, StringComparer.CurrentCulture)
            .ToArray();
        return applicable;
    }

    private static IReadOnlyList<TestMetric> ReservedMetrics(
        TestProjectKind kind,
        IReadOnlyList<TestDataDisplayRule>? rules = null)
    {
        if (rules is { Count: > 0 })
        {
            return rules.Select((rule, index) =>
            {
                rule.Normalize(index + 1);
                string name = string.IsNullOrWhiteSpace(rule.OutputColumn)
                    ? (string.IsNullOrWhiteSpace(rule.Name) ? $"指标{index + 1}" : rule.Name)
                    : rule.OutputColumn;
                string key = $"{(int)kind}:{rule.Order}:{name}";
                return new TestMetric(key, name, "待确认",
                    "MRTEST ExportFile 尚未找到可读取工作簿", "未找到");
            }).ToArray();
        }

        return kind switch
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
        TestProjectKind.Distortion => new[]
        {
            Reserved("distortion.horizontal", "水平畸变"),
            Reserved("distortion.vertical", "垂直畸变"),
            Reserved("distortion.result", "畸变结果")
        },
            _ => Array.Empty<TestMetric>()
        };
    }

    private static TestMetric Reserved(string key, string name) =>
        new(key, name, "待确认", "MRTEST ExportFile 字段尚未确认", "预留");
}
