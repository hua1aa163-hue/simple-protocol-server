using System.Text.Json.Serialization;
using System.Text.Json;

namespace AutoTestClient.Models;

/// <summary>首版支持的测试项目分类。后续数据字段可在对应处理器中扩展。</summary>
public enum TestProjectKind
{
    Generic = 0,
    Fov = 1,
    Contrast = 2,
    BrightnessUniformity = 3,
    Gamut = 4,
    Crosstalk = 5
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
        _ => "通用"
    };
}

/// <summary>一键测试计划和客户端设置；界面中的可编辑值全部从这里持久化。</summary>
public sealed class TestPlanConfiguration
{
    /// <summary>当前配置格式版本。版本 2 修正了手动测量请求的发送报文。</summary>
    public const int CurrentSchemaVersion = 2;

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
    public string ExportDirectory { get; set; } =
        @"D:\Program Files\GYTech\Setup_MRTest\ExportFile";
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
    public Projection.ProjectionMode ProjectionMode { get; set; } = Projection.ProjectionMode.PixelPerfect;
    public int SelectedProjectIndex { get; set; }
    public List<TestProject> Projects { get; set; } = CreateDefaultProjects();

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
        SchemaVersion = CurrentSchemaVersion;
    }

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
