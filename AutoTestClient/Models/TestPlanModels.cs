using System.Text.Json.Serialization;
using System.Text.Json;
using AutoTestClient.DataProcessing;

namespace AutoTestClient.Models;

/// <summary>首版支持的测试项目分类。后续数据字段可在对应处理器中扩展。</summary>
public enum TestProjectKind
{
    Generic = 0,
    Fov = 1,
    Contrast = 2,
    BrightnessUniformity = 3,
    Gamut = 4,
    Crosstalk = 5,
    /// <summary>畸变/视场几何结果；默认计划暂不自动新增该项目。</summary>
    Distortion = 6
}

/// <summary>弹窗出现后需要按顺序投影的固定图片步骤。</summary>
public sealed class TestStep
{
    public int Order { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ImagePath { get; set; } = string.Empty;
    public int StabilizeDelayMs { get; set; } = 500;
    public string MeasurementRequest { get; set; } = Protocol.MessageProtocol.DefaultMeasurementRequest;

    public string ResolveImagePath(string imageRoot)
    {
        if (string.IsNullOrWhiteSpace(ImagePath)) return string.Empty;
        return Path.IsPathRooted(ImagePath) ? ImagePath : Path.Combine(imageRoot ?? string.Empty, ImagePath);
    }

    [JsonIgnore]
    public string DisplayName => string.IsNullOrWhiteSpace(Name)
        ? Path.GetFileName(ImagePath)
        : Name;
}

/// <summary>一个测试项目及其固定图卡顺序。</summary>
public sealed class TestProject
{
    public bool Enabled { get; set; } = true;
    public int Order { get; set; }
    public string Name { get; set; } = string.Empty;
    public TestProjectKind Kind { get; set; } = TestProjectKind.Generic;
    public string RecipeName { get; set; } = string.Empty;
    /// <summary>可选的配方文件绝对/相对路径，仅用于界面绑定和校验。</summary>
    public string RecipeFilePath { get; set; } = string.Empty;
    public int RepeatCount { get; set; } = 1;
    public bool PopupDriven { get; set; }
    /// <summary>串扰前景图起始索引（0-based）；最后一张始终作为本底。</summary>
    public int CrosstalkStartIndex { get; set; }
    public int PopupTimeoutSeconds { get; set; } = 600;
    public List<TestStep> Steps { get; set; } = new();

    [JsonIgnore]
    public string KindDisplayName => Kind switch
    {
        TestProjectKind.Fov => "FOV",
        TestProjectKind.Contrast => "黑白对比度",
        TestProjectKind.BrightnessUniformity => "亮度均匀性",
        TestProjectKind.Gamut => "色域",
        TestProjectKind.Crosstalk => "串扰",
        TestProjectKind.Distortion => "畸变",
        _ => "通用"
    };
}

/// <summary>
/// 可命名保存的一套一键测试计划。
/// <para>
/// 这里只保存测试计划相关字段，不复制 TCP 地址、MRTEST 路径等客户端全局设置；
/// 因而切换计划不会意外改变当前机器的连接配置。Projects 会在保存时深复制。
/// </para>
/// </summary>
public sealed class NamedTestPlan
{
    public const string DefaultName = "默认计划";

    public string Name { get; set; } = DefaultName;
    public int WholePlanRepeatCount { get; set; } = 1;
    public int DefaultProjectRepeatCount { get; set; } = 1;
    public bool AutoConfirmPopups { get; set; } = true;
    public int PopupStabilizeDelayMs { get; set; } = 500;
    public Projection.ProjectionMode ProjectionMode { get; set; } = Projection.ProjectionMode.PixelPerfect;
    public int SelectedProjectIndex { get; set; }

    // 串扰分析选项属于测试计划的一部分，随计划一起保存。
    public double CrosstalkAbnormalThresholdRatio { get; set; } =
        CrosstalkAnalysisOptions.DefaultAbnormalThresholdRatio;
    public double CrosstalkColorAxisMinimumPercent { get; set; } =
        CrosstalkAnalysisOptions.DefaultColorAxisMinimumPercent;
    public double CrosstalkColorAxisMaximumPercent { get; set; } =
        CrosstalkAnalysisOptions.DefaultColorAxisMaximumPercent;
    public string CrosstalkMaskCoordinates { get; set; } = string.Empty;
    public string CrosstalkMaskName { get; set; } = string.Empty;
    public string CrosstalkMaskNote { get; set; } = string.Empty;
    public string CrosstalkAnalysisSourceDirectory { get; set; } = string.Empty;
    public string CrosstalkAnalysisOutputDirectory { get; set; } = string.Empty;

    public List<TestProject> Projects { get; set; } = new();
    /// <summary>
    /// MRTEST 导出字段到报告列的映射也随命名计划保存；切换计划时不会把
    /// 当前计划的展示规则遗留到另一套项目上。
    /// </summary>
    public List<TestDataDisplayRule> DisplayRules { get; set; } = TestDataDisplayRule.CreateDefaultRules();

    /// <summary>从当前配置创建一个独立的命名计划快照。</summary>
    public static NamedTestPlan Capture(TestPlanConfiguration configuration, string name)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        TestPlanConfiguration copy = configuration.Clone();
        var result = new NamedTestPlan
        {
            Name = string.IsNullOrWhiteSpace(name) ? DefaultName : name.Trim(),
            WholePlanRepeatCount = copy.WholePlanRepeatCount,
            DefaultProjectRepeatCount = copy.DefaultProjectRepeatCount,
            AutoConfirmPopups = copy.AutoConfirmPopups,
            PopupStabilizeDelayMs = copy.PopupStabilizeDelayMs,
            ProjectionMode = copy.ProjectionMode,
            SelectedProjectIndex = copy.SelectedProjectIndex,
            CrosstalkAbnormalThresholdRatio = copy.CrosstalkAbnormalThresholdRatio,
            CrosstalkColorAxisMinimumPercent = copy.CrosstalkColorAxisMinimumPercent,
            CrosstalkColorAxisMaximumPercent = copy.CrosstalkColorAxisMaximumPercent,
            CrosstalkMaskCoordinates = copy.CrosstalkMaskCoordinates,
            CrosstalkMaskName = copy.CrosstalkMaskName,
            CrosstalkMaskNote = copy.CrosstalkMaskNote,
            CrosstalkAnalysisSourceDirectory = copy.CrosstalkAnalysisSourceDirectory,
            CrosstalkAnalysisOutputDirectory = copy.CrosstalkAnalysisOutputDirectory,
            Projects = copy.Projects,
            DisplayRules = copy.DisplayRules
        };
        result.Normalize(copy.DefaultProjectRepeatCount);
        return result;
    }

    /// <summary>把快照应用到配置；连接与路径等全局字段保持原值。</summary>
    public void ApplyTo(TestPlanConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        NamedTestPlan copy = Clone();
        copy.Normalize(configuration.DefaultProjectRepeatCount);
        configuration.WholePlanRepeatCount = copy.WholePlanRepeatCount;
        configuration.DefaultProjectRepeatCount = copy.DefaultProjectRepeatCount;
        configuration.AutoConfirmPopups = copy.AutoConfirmPopups;
        configuration.PopupStabilizeDelayMs = copy.PopupStabilizeDelayMs;
        configuration.ProjectionMode = copy.ProjectionMode;
        configuration.SelectedProjectIndex = copy.SelectedProjectIndex;
        configuration.CrosstalkAbnormalThresholdRatio = copy.CrosstalkAbnormalThresholdRatio;
        configuration.CrosstalkColorAxisMinimumPercent = copy.CrosstalkColorAxisMinimumPercent;
        configuration.CrosstalkColorAxisMaximumPercent = copy.CrosstalkColorAxisMaximumPercent;
        configuration.CrosstalkMaskCoordinates = copy.CrosstalkMaskCoordinates;
        configuration.CrosstalkMaskName = copy.CrosstalkMaskName;
        configuration.CrosstalkMaskNote = copy.CrosstalkMaskNote;
        configuration.CrosstalkAnalysisSourceDirectory = copy.CrosstalkAnalysisSourceDirectory;
        configuration.CrosstalkAnalysisOutputDirectory = copy.CrosstalkAnalysisOutputDirectory;
        configuration.Projects = copy.Projects;
        configuration.DisplayRules = copy.DisplayRules;
        configuration.Normalize();
    }

    public NamedTestPlan Clone()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };
        return JsonSerializer.Deserialize<NamedTestPlan>(
                   JsonSerializer.Serialize(this, options), options)
               ?? new NamedTestPlan();
    }

    /// <summary>清理从旧 JSON 或手工编辑文件读取的计划内容。</summary>
    public void Normalize(int fallbackProjectRepeat = 1)
    {
        Name = string.IsNullOrWhiteSpace(Name) ? DefaultName : Name.Trim();
        WholePlanRepeatCount = Math.Clamp(WholePlanRepeatCount, 1, 9999);
        DefaultProjectRepeatCount = Math.Clamp(
            DefaultProjectRepeatCount <= 0 ? fallbackProjectRepeat : DefaultProjectRepeatCount, 1, 9999);
        PopupStabilizeDelayMs = Math.Clamp(PopupStabilizeDelayMs, 0, 60000);
        if (!Enum.IsDefined(ProjectionMode))
            ProjectionMode = Projection.ProjectionMode.PixelPerfect;
        if (!double.IsFinite(CrosstalkAbnormalThresholdRatio))
            CrosstalkAbnormalThresholdRatio = CrosstalkAnalysisOptions.DefaultAbnormalThresholdRatio;
        if (!double.IsFinite(CrosstalkColorAxisMinimumPercent))
            CrosstalkColorAxisMinimumPercent = CrosstalkAnalysisOptions.DefaultColorAxisMinimumPercent;
        if (!double.IsFinite(CrosstalkColorAxisMaximumPercent) ||
            CrosstalkColorAxisMaximumPercent <= CrosstalkColorAxisMinimumPercent)
            CrosstalkColorAxisMaximumPercent = Math.Max(
                CrosstalkColorAxisMinimumPercent + 1d,
                CrosstalkAnalysisOptions.DefaultColorAxisMaximumPercent);
        CrosstalkMaskCoordinates ??= string.Empty;
        CrosstalkMaskName ??= string.Empty;
        CrosstalkMaskNote ??= string.Empty;
        CrosstalkAnalysisSourceDirectory ??= string.Empty;
        CrosstalkAnalysisOutputDirectory ??= string.Empty;
        Projects ??= new List<TestProject>();
        Projects = Projects.Where(project => project is not null).ToList();
        foreach (TestProject project in Projects)
        {
            project.Name ??= string.Empty;
            project.RecipeName ??= string.Empty;
            project.RecipeFilePath ??= string.Empty;
            project.RepeatCount = Math.Clamp(
                project.RepeatCount <= 0 ? DefaultProjectRepeatCount : project.RepeatCount, 1, 9999);
            project.CrosstalkStartIndex = Math.Max(0, project.CrosstalkStartIndex);
            project.Steps ??= new List<TestStep>();
            project.Steps = project.Steps.Where(step => step is not null).ToList();
            foreach (TestStep step in project.Steps)
            {
                step.Name ??= string.Empty;
                step.ImagePath ??= string.Empty;
                step.MeasurementRequest = string.IsNullOrWhiteSpace(step.MeasurementRequest)
                    ? Protocol.MessageProtocol.DefaultMeasurementRequest
                    : Protocol.MessageProtocol.MigrateMeasurementRequest(step.MeasurementRequest.Trim());
                step.StabilizeDelayMs = Math.Clamp(step.StabilizeDelayMs, 0, 60000);
            }
            project.Steps = project.Steps.OrderBy(step => step.Order).ToList();
        }
        Projects = Projects.OrderBy(project => project.Order)
            .ThenBy(project => project.Name, StringComparer.CurrentCulture).ToList();
        SelectedProjectIndex = Projects.Count == 0
            ? 0 : Math.Clamp(SelectedProjectIndex, 0, Projects.Count - 1);

        DisplayRules ??= new List<TestDataDisplayRule>();
        DisplayRules = DisplayRules.Where(rule => rule is not null).ToList();
        for (int index = 0; index < DisplayRules.Count; index++)
            DisplayRules[index].Normalize(index + 1);
        DisplayRules = DisplayRules
            .OrderBy(rule => rule.Order)
            .ThenBy(rule => rule.Name, StringComparer.CurrentCulture)
            .ToList();
    }
}

/// <summary>一键测试计划和客户端设置；界面中的可编辑值全部从这里持久化。</summary>
public sealed class TestPlanConfiguration
{
    /// <summary>当前配置格式版本。版本 3 增加了可命名的多套测试计划。</summary>
    public const int CurrentSchemaVersion = 3;
    /// <summary>MRTEST 新版默认导出目录；界面仍允许用户改成现场目录。</summary>
    public const string DefaultMrTestExportDirectory =
        @"D:\Program Files\GYTech\Setup\_MRTest\ExportFile";
    /// <summary>旧版安装包常用的导出目录，仅在默认目录不存在时兼容读取。</summary>
    public const string LegacyMrTestExportDirectory =
        @"D:\Program Files\GYTech\Setup_MRTest\ExportFile";

    public TestPlanConfiguration Clone()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } };
        string json = JsonSerializer.Serialize(this, options);
        return JsonSerializer.Deserialize<TestPlanConfiguration>(json, options) ?? new TestPlanConfiguration();
    }

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public string BindAddress { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 9527;
    public string MrTestExecutablePath { get; set; } =
        @"D:\Program Files\GYTech\Setup_MRTest\MRTest.exe";
    public string ExportDirectory { get; set; } = DefaultMrTestExportDirectory;
    public string RecipeDirectory { get; set; } =
        @"D:\Program Files\GYTech\Setup_MRTest\Elems";
    public string ImageDirectory { get; set; } = @"D:\CHATGPT_file\测试图卡";
    public string OutputDirectory { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AutoTestResults");
    public string ManualCommand { get; set; } = Protocol.MessageProtocol.DefaultMeasurementRequest;
    public int WholePlanRepeatCount { get; set; } = 1;
    public int DefaultProjectRepeatCount { get; set; } = 1;
    public bool AutoConfirmPopups { get; set; } = true;
    public int PopupStabilizeDelayMs { get; set; } = 500;
    /// <summary>串扰异常阈值，使用比例保存（0.03 表示 3%）。</summary>
    public double CrosstalkAbnormalThresholdRatio { get; set; } =
        CrosstalkAnalysisOptions.DefaultAbnormalThresholdRatio;
    /// <summary>串扰热力图色轴下限，使用百分比单位。</summary>
    public double CrosstalkColorAxisMinimumPercent { get; set; } =
        CrosstalkAnalysisOptions.DefaultColorAxisMinimumPercent;
    /// <summary>串扰热力图色轴上限，使用百分比单位。</summary>
    public double CrosstalkColorAxisMaximumPercent { get; set; } =
        CrosstalkAnalysisOptions.DefaultColorAxisMaximumPercent;
    /// <summary>最近一次输入的串扰掩膜坐标文本，便于退出后恢复编辑状态。</summary>
    public string CrosstalkMaskCoordinates { get; set; } = string.Empty;
    /// <summary>当前选择的串扰掩膜存档名称。</summary>
    public string CrosstalkMaskName { get; set; } = string.Empty;
    /// <summary>串扰分析器上次使用的数据根目录；为空时使用 ExportDirectory。</summary>
    public string CrosstalkAnalysisSourceDirectory { get; set; } = string.Empty;
    /// <summary>串扰分析器上次使用的结果目录；为空时使用 OutputDirectory。</summary>
    public string CrosstalkAnalysisOutputDirectory { get; set; } = string.Empty;
    /// <summary>当前串扰掩膜备注。</summary>
    public string CrosstalkMaskNote { get; set; } = string.Empty;
    public Projection.ProjectionMode ProjectionMode { get; set; } = Projection.ProjectionMode.PixelPerfect;
    public int SelectedProjectIndex { get; set; }
    public List<TestProject> Projects { get; set; } = CreateDefaultProjects();
    /// <summary>
    /// MRTEST 导出数据的展示规则。每个项目完成后由数据处理层按此列表
    /// 读取最深层明细 Excel，并把指标交给主界面展示。
    /// </summary>
    public List<TestDataDisplayRule> DisplayRules { get; set; } = TestDataDisplayRule.CreateDefaultRules();
    /// <summary>用户保存的命名测试计划；旧配置缺失此字段时自动得到空集合。</summary>
    public List<NamedTestPlan> SavedPlans { get; set; } = new();
    /// <summary>上次选中的计划名称；空值表示当前编辑内容尚未命名保存。</summary>
    public string ActivePlanName { get; set; } = string.Empty;

    /// <summary>
    /// 返回实际用于读取 MRTEST 导出的目录。用户填写的路径优先；只有在
    /// 仍使用新版默认路径且该路径尚不存在时，才兼容本机常见的旧安装目录。
    /// 配置文本框本身不会被悄悄改写，用户仍可随时更改它。
    /// </summary>
    public static string ResolveExportDirectoryForRead(string? configured)
    {
        string value = configured?.Trim() ?? string.Empty;
        if (value.Length == 0) return value;
        try
        {
            if (Directory.Exists(value)) return value;
            if (string.Equals(Path.GetFullPath(value),
                              Path.GetFullPath(DefaultMrTestExportDirectory),
                              StringComparison.OrdinalIgnoreCase) &&
                Directory.Exists(LegacyMrTestExportDirectory))
                return LegacyMrTestExportDirectory;
        }
        catch (ArgumentException) { }
        catch (NotSupportedException) { }
        return value;
    }

    public void Normalize()
    {
        // 版本 1 曾把设备的 Run 中间返回误当成发送请求。无论 SchemaVersion
        // 是否缺失，都只迁移精确的历史默认值。这样即使
        // 旧 JSON 没有写入版本字段（反序列化后会使用当前属性初值），也不会
        // 把设备的 Run 中间响应继续当成发送请求；其他自定义报文保持不变。
        ManualCommand = Protocol.MessageProtocol.MigrateMeasurementRequest(ManualCommand);
        BindAddress = string.IsNullOrWhiteSpace(BindAddress) ? "127.0.0.1" : BindAddress.Trim();
        Port = Math.Clamp(Port, 1, 65535);
        WholePlanRepeatCount = Math.Clamp(WholePlanRepeatCount, 1, 9999);
        DefaultProjectRepeatCount = Math.Clamp(DefaultProjectRepeatCount, 1, 9999);
        PopupStabilizeDelayMs = Math.Clamp(PopupStabilizeDelayMs, 0, 60000);
        if (!double.IsFinite(CrosstalkAbnormalThresholdRatio))
            CrosstalkAbnormalThresholdRatio = CrosstalkAnalysisOptions.DefaultAbnormalThresholdRatio;
        if (!double.IsFinite(CrosstalkColorAxisMinimumPercent))
            CrosstalkColorAxisMinimumPercent = CrosstalkAnalysisOptions.DefaultColorAxisMinimumPercent;
        if (!double.IsFinite(CrosstalkColorAxisMaximumPercent) ||
            CrosstalkColorAxisMaximumPercent <= CrosstalkColorAxisMinimumPercent)
            CrosstalkColorAxisMaximumPercent = Math.Max(
                CrosstalkColorAxisMinimumPercent + 1d,
                CrosstalkAnalysisOptions.DefaultColorAxisMaximumPercent);
        CrosstalkMaskCoordinates ??= string.Empty;
        CrosstalkMaskName ??= string.Empty;
        CrosstalkAnalysisSourceDirectory ??= string.Empty;
        CrosstalkAnalysisOutputDirectory ??= string.Empty;
        CrosstalkMaskNote ??= string.Empty;
        SavedPlans ??= new List<NamedTestPlan>();
        var normalizedPlans = new List<NamedTestPlan>(SavedPlans.Count);
        var planNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (NamedTestPlan? plan in SavedPlans)
        {
            if (plan is null) continue;
            plan.Normalize(DefaultProjectRepeatCount);
            // 同名计划只保留第一次出现的条目，避免下拉框和切换索引不确定。
            if (planNames.Add(plan.Name)) normalizedPlans.Add(plan);
        }
        SavedPlans = normalizedPlans;
        ActivePlanName = ActivePlanName?.Trim() ?? string.Empty;
        if (ActivePlanName.Length > 0 &&
            !SavedPlans.Any(plan => string.Equals(plan.Name, ActivePlanName,
                StringComparison.OrdinalIgnoreCase)))
            ActivePlanName = string.Empty;
        Projects ??= new List<TestProject>();
        Projects = Projects.Where(project => project is not null).ToList();
        foreach (var project in Projects)
        {
            project.Name ??= string.Empty;
            project.RecipeName ??= string.Empty;
            project.RecipeFilePath ??= string.Empty;
            project.RepeatCount = Math.Clamp(project.RepeatCount <= 0 ? DefaultProjectRepeatCount : project.RepeatCount, 1, 9999);
            project.CrosstalkStartIndex = Math.Max(0, project.CrosstalkStartIndex);
            project.Steps ??= new List<TestStep>();
            project.Steps = project.Steps.Where(step => step is not null).ToList();
            foreach (var step in project.Steps)
            {
                step.Name ??= string.Empty;
                step.ImagePath ??= string.Empty;
                step.MeasurementRequest = string.IsNullOrWhiteSpace(step.MeasurementRequest)
                    ? Protocol.MessageProtocol.DefaultMeasurementRequest
                    : step.MeasurementRequest.Trim();
                step.MeasurementRequest = Protocol.MessageProtocol.MigrateMeasurementRequest(step.MeasurementRequest);
                step.StabilizeDelayMs = Math.Clamp(step.StabilizeDelayMs, 0, 60000);
            }
            project.Steps = project.Steps.OrderBy(s => s.Order).ToList();
        }
        Projects = Projects.OrderBy(p => p.Order).ThenBy(p => p.Name, StringComparer.CurrentCulture).ToList();
        SelectedProjectIndex = Projects.Count == 0 ? 0 : Math.Clamp(SelectedProjectIndex, 0, Projects.Count - 1);
        DisplayRules ??= new List<TestDataDisplayRule>();
        DisplayRules = DisplayRules.Where(rule => rule is not null).ToList();
        for (int index = 0; index < DisplayRules.Count; index++)
            DisplayRules[index].Normalize(index + 1);
        DisplayRules = DisplayRules
            .OrderBy(rule => rule.Order)
            .ThenBy(rule => rule.Name, StringComparer.CurrentCulture)
            .ToList();
        SchemaVersion = CurrentSchemaVersion;
    }

    /// <summary>将界面持久化值转换为串扰数据处理参数。</summary>
    public CrosstalkAnalysisOptions CreateCrosstalkAnalysisOptions(
        bool[,]? userMask = null,
        string? maskName = null,
        string? maskNote = null) => new()
        {
            AbnormalThresholdRatio = CrosstalkAbnormalThresholdRatio,
            ColorAxisMinimumPercent = CrosstalkColorAxisMinimumPercent,
            ColorAxisMaximumPercent = CrosstalkColorAxisMaximumPercent,
            UserMask = userMask,
            MaskName = maskName ?? CrosstalkMaskName,
            MaskNote = maskNote ?? CrosstalkMaskNote
        };

    public static List<TestProject> CreateDefaultProjects()
    {
        string root = @"D:\CHATGPT_file\测试图卡";
        static TestStep Step(int order, string name, string path) => new()
        {
            Order = order, Name = name, ImagePath = path,
            MeasurementRequest = Protocol.MessageProtocol.DefaultMeasurementRequest
        };
        return new List<TestProject>
        {
            new() { Order = 1, Name = "FOV测试", Kind = TestProjectKind.Fov, RecipeName = "FOV", PopupDriven = false,
                Steps = new() { Step(1, "FOV图卡", Path.Combine(root, "FOV", "FOV.png")) } },
            new() { Order = 2, Name = "黑白对比度", Kind = TestProjectKind.Contrast, RecipeName = "Contrast", PopupDriven = true,
                Steps = new() { Step(1, "白图", Path.Combine(root, "对比度", "白图.png")), Step(2, "黑图", Path.Combine(root, "对比度", "黑图.png")) } },
            new() { Order = 3, Name = "亮度均匀性", Kind = TestProjectKind.BrightnessUniformity, RecipeName = "Uniformity", PopupDriven = false,
                Steps = new() { Step(1, "均匀白图", Path.Combine(root, "均匀性", "白图.png")) } },
            new() { Order = 4, Name = "色域", Kind = TestProjectKind.Gamut, RecipeName = "Gamut", PopupDriven = true,
                Steps = new() { Step(1, "白图", Path.Combine(root, "色域", "白图.png")), Step(2, "红图", Path.Combine(root, "色域", "红图.png")),
                    Step(3, "绿图", Path.Combine(root, "色域", "绿图.png")), Step(4, "蓝图", Path.Combine(root, "色域", "蓝图.png")) } },
            new() { Order = 5, Name = "串扰", Kind = TestProjectKind.Crosstalk, RecipeName = "Crosstalk", PopupDriven = false,
                Steps = Enumerable.Range(1, 9).Select(i => Step(i, $"串扰图{i}", Path.Combine(root, "串扰", $"{i}.png"))).ToList() }
        };
    }
}
