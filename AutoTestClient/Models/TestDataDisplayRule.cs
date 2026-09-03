using System.ComponentModel;

namespace AutoTestClient.Models;

/// <summary>
/// 从 MRTEST 导出的 Excel 工作表读取一个或一组单元格时所使用的聚合方式。
/// 该枚举只描述显示层规则，不改变测量执行流程。
/// </summary>
public enum TestDataAggregation
{
    [Description("不聚合")]
    None = 0,

    [Description("平均值")]
    Average = 1,

    [Description("最小值")]
    Minimum = 2,

    [Description("最大值")]
    Maximum = 3,

    [Description("求和")]
    Sum = 4,

    [Description("计数")]
    Count = 5,

    [Description("第一个")]
    First = 6,

    [Description("最后一个")]
    Last = 7
}

/// <summary>
/// 测试结果展示规则。
///
/// 规则把 Excel 工作表中的单元格/范围映射成最终报告的一列。规则可在
/// 主界面打开的编辑器中增删和调整，数据处理层按同一配置读取 MRTEST 明细。
/// </summary>
public sealed class TestDataDisplayRule
{
    /// <summary>是否在结果展示中启用该规则。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>显示顺序，从 1 开始。</summary>
    public int Order { get; set; }

    /// <summary>规则名称，便于在编辑器中识别。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 规则适用的测试项目类型；留空表示适用于所有项目。使用可空值可让
    /// 编辑器明确区分“全部”与 Generic（通用项目）。
    /// </summary>
    public TestProjectKind? ProjectKind { get; set; }

    /// <summary>Excel 工作表名称；留空表示使用第一个工作表。</summary>
    public string Worksheet { get; set; } = string.Empty;

    /// <summary>
    /// Excel 名称单元格，或不含地址的固定标签。留空表示不读取名称，
    /// 直接使用规则名称/名称模板。
    /// </summary>
    public string NameCellOrLabel { get; set; } = string.Empty;

    /// <summary>Excel 数据单元格或范围，例如 C3、B2:D6。</summary>
    public string DataCellOrRange { get; set; } = string.Empty;

    /// <summary>
    /// 旧版本字段兼容入口。新代码应使用 <see cref="DataCellOrRange"/>；
    /// 规范化时会在两个字段之间互相补齐，旧 JSON 无需迁移脚本即可继续用。
    /// </summary>
    public string CellOrRange { get; set; } = string.Empty;

    /// <summary>
    /// 输出名称模板。支持层暂不解释占位符，保留原文供后续报告层展开，
    /// 例如 {ProjectName}_{RuleName}。
    /// </summary>
    public string NameTemplate { get; set; } = "{ProjectName}_{RuleName}";

    /// <summary>从单元格/范围得到一个显示值时使用的聚合方式。</summary>
    public TestDataAggregation Aggregation { get; set; } = TestDataAggregation.First;

    /// <summary>最终报告中的输出列名称。</summary>
    public string OutputColumn { get; set; } = string.Empty;

    /// <summary>
    /// 首次启动时显示的字段模板。默认地址对应当前 MRTEST 明细工作簿；
    /// 地址型规则默认直接采用 Excel 名称单元格，只有对比度平均值使用
    /// 固定的“白图平均值/黑图平均值”报告列名。现场版本若有差异，可直接
    /// 在规则编辑器中改写，无需修改程序。
    /// </summary>
    public static List<TestDataDisplayRule> CreateDefaultRules() => new()
    {
        new() { Order = 1, ProjectKind = TestProjectKind.Fov, Name = "水平FOV",
            Worksheet = "FOV", NameCellOrLabel = "B4", DataCellOrRange = "C4",
            CellOrRange = "C4", NameTemplate = "{ProjectName}_水平FOV",
            // 留空输出列时直接使用 B4/B5 中的名称；用户也可以在编辑器中
            // 填写输出列，把现场标签统一成报告列名。
            Aggregation = TestDataAggregation.First, OutputColumn = "" },
        new() { Order = 2, ProjectKind = TestProjectKind.Fov, Name = "垂直FOV",
            Worksheet = "FOV", NameCellOrLabel = "B5", DataCellOrRange = "C5",
            CellOrRange = "C5", NameTemplate = "{ProjectName}_垂直FOV",
            Aggregation = TestDataAggregation.First, OutputColumn = "" },
        new() { Order = 3, ProjectKind = TestProjectKind.Contrast, Name = "白图平均亮度",
            Worksheet = "Contrast", NameCellOrLabel = "白图平均值", DataCellOrRange = "C4:C12",
            CellOrRange = "C4:C12", NameTemplate = "{ProjectName}_白图平均亮度",
            Aggregation = TestDataAggregation.Average, OutputColumn = "白图平均值" },
        new() { Order = 4, ProjectKind = TestProjectKind.Contrast, Name = "黑图平均亮度",
            Worksheet = "Contrast", NameCellOrLabel = "黑图平均值", DataCellOrRange = "C13:C21",
            CellOrRange = "C13:C21", NameTemplate = "{ProjectName}_黑图平均亮度",
            Aggregation = TestDataAggregation.Average, OutputColumn = "黑图平均值" },
        new() { Order = 5, ProjectKind = TestProjectKind.Contrast, Name = "平均对比度",
            Worksheet = "Contrast", NameCellOrLabel = "B31", DataCellOrRange = "C31",
            CellOrRange = "C31", NameTemplate = "{ProjectName}_平均对比度",
            Aggregation = TestDataAggregation.First, OutputColumn = "" },
        new() { Order = 6, ProjectKind = TestProjectKind.BrightnessUniformity, Name = "平均亮度",
            Worksheet = "Uniformity", NameCellOrLabel = "B13", DataCellOrRange = "C13",
            CellOrRange = "C13", NameTemplate = "{ProjectName}_平均亮度",
            Aggregation = TestDataAggregation.First, OutputColumn = "" },
        new() { Order = 7, ProjectKind = TestProjectKind.BrightnessUniformity, Name = "均匀性",
            Worksheet = "Uniformity", NameCellOrLabel = "B16", DataCellOrRange = "C16",
            CellOrRange = "C16", NameTemplate = "{ProjectName}_均匀性",
            Aggregation = TestDataAggregation.First, OutputColumn = "" },
        new() { Order = 8, ProjectKind = TestProjectKind.Gamut, Name = "白图色温",
            Worksheet = "Gamut", NameCellOrLabel = "H3", DataCellOrRange = "H4",
            CellOrRange = "H4", NameTemplate = "{ProjectName}_白图色温",
            Aggregation = TestDataAggregation.First, OutputColumn = "" },
        new() { Order = 9, ProjectKind = TestProjectKind.Gamut, Name = "色域",
            Worksheet = "Gamut", NameCellOrLabel = "B23", DataCellOrRange = "C23",
            CellOrRange = "C23", NameTemplate = "{ProjectName}_色域",
            Aggregation = TestDataAggregation.First, OutputColumn = "" },
        new() { Order = 10, ProjectKind = TestProjectKind.Distortion, Name = "水平畸变",
            Worksheet = "Distortion", NameCellOrLabel = "B52", DataCellOrRange = "C52",
            CellOrRange = "C52", NameTemplate = "{ProjectName}_水平畸变",
            Aggregation = TestDataAggregation.First, OutputColumn = "" },
        new() { Order = 11, ProjectKind = TestProjectKind.Distortion, Name = "垂直畸变",
            Worksheet = "Distortion", NameCellOrLabel = "B61", DataCellOrRange = "C61",
            CellOrRange = "C61", NameTemplate = "{ProjectName}_垂直畸变",
            Aggregation = TestDataAggregation.First, OutputColumn = "" },
        new() { Order = 12, ProjectKind = TestProjectKind.Distortion, Name = "畸变结果",
            Worksheet = "Distortion", NameCellOrLabel = "B62", DataCellOrRange = "C62",
            CellOrRange = "C62", NameTemplate = "{ProjectName}_畸变结果",
            Aggregation = TestDataAggregation.First, OutputColumn = "" }
    };

    /// <summary>
    /// 对来自 JSON、设计器或手工输入的值做轻量规范化；不访问文件系统，
    /// 因此可安全用于 Visual Studio 设计器和配置加载。
    /// </summary>
    public void Normalize(int fallbackOrder = 1)
    {
        Order = Order <= 0 ? Math.Max(1, fallbackOrder) : Math.Clamp(Order, 1, 9999);
        Name = (Name ?? string.Empty).Trim();
        NameCellOrLabel = (NameCellOrLabel ?? string.Empty).Trim();
        Worksheet = (Worksheet ?? string.Empty).Trim();
        DataCellOrRange = (DataCellOrRange ?? string.Empty).Trim();
        CellOrRange = (CellOrRange ?? string.Empty).Trim();
        if (DataCellOrRange.Length == 0 && CellOrRange.Length > 0)
            DataCellOrRange = CellOrRange;
        if (CellOrRange.Length == 0 && DataCellOrRange.Length > 0)
            CellOrRange = DataCellOrRange;
        NameTemplate = string.IsNullOrWhiteSpace(NameTemplate)
            ? "{ProjectName}_{RuleName}"
            : NameTemplate.Trim();
        OutputColumn = (OutputColumn ?? string.Empty).Trim();
        if (!Enum.IsDefined(Aggregation))
            Aggregation = TestDataAggregation.First;
    }

    /// <summary>返回用于下拉框/日志的中文显示文本。</summary>
    public static string GetAggregationDisplayName(TestDataAggregation aggregation) =>
        aggregation switch
        {
            TestDataAggregation.None => "不聚合",
            TestDataAggregation.Average => "平均值",
            TestDataAggregation.Minimum => "最小值",
            TestDataAggregation.Maximum => "最大值",
            TestDataAggregation.Sum => "求和",
            TestDataAggregation.Count => "计数",
            TestDataAggregation.First => "第一个",
            TestDataAggregation.Last => "最后一个",
            _ => "第一个"
        };

    /// <summary>项目类型在规则编辑器中的中文显示文本。</summary>
    public static string GetProjectKindDisplayName(TestProjectKind? kind) =>
        kind switch
        {
            null => "全部项目",
            TestProjectKind.Fov => "FOV",
            TestProjectKind.Contrast => "黑白对比度",
            TestProjectKind.BrightnessUniformity => "亮度均匀性",
            TestProjectKind.Gamut => "色域",
            TestProjectKind.Crosstalk => "串扰",
            TestProjectKind.Distortion => "畸变",
            _ => "通用"
        };

    /// <summary>按显示文本或枚举名称解析项目类型；“全部项目”返回 null。</summary>
    public static TestProjectKind? ParseProjectKind(object? value)
    {
        if (value is null) return null;
        if (value is TestProjectKind kind) return kind;

        string text = value.ToString()?.Trim() ?? string.Empty;
        if (text.Length == 0 || text.Equals("全部项目", StringComparison.OrdinalIgnoreCase) ||
            text.Equals("全部", StringComparison.OrdinalIgnoreCase))
            return null;
        if (Enum.TryParse<TestProjectKind>(text, true, out kind) && Enum.IsDefined(kind))
            return kind;
        return text switch
        {
            "FOV" or "FOV测试" => TestProjectKind.Fov,
            "黑白对比度" or "对比度" => TestProjectKind.Contrast,
            "亮度均匀性" or "均匀性" => TestProjectKind.BrightnessUniformity,
            "色域" => TestProjectKind.Gamut,
            "串扰" => TestProjectKind.Crosstalk,
            "畸变" or "畸变测试" => TestProjectKind.Distortion,
            "通用" => TestProjectKind.Generic,
            _ => null
        };
    }

    /// <summary>按中文名称解析聚合方式；无法解析时返回第一个值。</summary>
    public static TestDataAggregation ParseAggregation(object? value)
    {
        if (value is TestDataAggregation aggregation && Enum.IsDefined(aggregation))
            return aggregation;

        string text = value?.ToString()?.Trim() ?? string.Empty;
        if (Enum.TryParse<TestDataAggregation>(text, ignoreCase: true, out aggregation) &&
            Enum.IsDefined(aggregation))
        {
            return aggregation;
        }

        return text switch
        {
            "不聚合" => TestDataAggregation.None,
            "平均" or "平均值" => TestDataAggregation.Average,
            "最小" or "最小值" => TestDataAggregation.Minimum,
            "最大" or "最大值" => TestDataAggregation.Maximum,
            "求和" => TestDataAggregation.Sum,
            "计数" => TestDataAggregation.Count,
            "首个" or "第一个" => TestDataAggregation.First,
            "末个" or "最后一个" => TestDataAggregation.Last,
            _ => TestDataAggregation.First
        };
    }

    /// <summary>
    /// 将规则编辑器中使用的工作表别名映射为 MRTEST 实际导出的名称。
    ///
    /// MRTEST 的黑白对比度结果在不同版本/语言包中曾显示为
    /// “Contrast”“Sequential Contrast”或不带空格的“SequentialContrast”。
    /// 配置文件保留用户输入的别名，数据处理层读取前应调用此方法，避免
    /// 为了兼容现场版本而强制改写设计器里的可读文本。
    /// </summary>
    public static string ResolveWorksheetAlias(string? worksheet, TestProjectKind? projectKind = null)
    {
        string text = worksheet?.Trim() ?? string.Empty;
        if (text.Length == 0) return string.Empty;

        if (projectKind == TestProjectKind.Contrast ||
            text.Equals("Contrast", StringComparison.OrdinalIgnoreCase) ||
            text.Equals("SequentialContrast", StringComparison.OrdinalIgnoreCase) ||
            text.Equals("Sequential Contrast", StringComparison.OrdinalIgnoreCase) ||
            text.Equals("黑白对比度", StringComparison.OrdinalIgnoreCase) ||
            text.Equals("对比度", StringComparison.OrdinalIgnoreCase))
        {
            return text.Equals("Contrast", StringComparison.OrdinalIgnoreCase) ||
                   text.Equals("SequentialContrast", StringComparison.OrdinalIgnoreCase) ||
                   text.Equals("Sequential Contrast", StringComparison.OrdinalIgnoreCase) ||
                   text.Equals("黑白对比度", StringComparison.OrdinalIgnoreCase) ||
                   text.Equals("对比度", StringComparison.OrdinalIgnoreCase)
                ? "Sequential Contrast"
                : text;
        }

        return text;
    }
}
