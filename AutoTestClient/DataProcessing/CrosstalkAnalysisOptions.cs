using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AutoTestClient.DataProcessing;

/// <summary>
/// 可在界面中调整的串扰分析参数。
/// <para>
/// 内部矩阵的数值是比例（0.03 表示 3%），而色轴上下限使用百分比单位，
/// 与热力图右侧的刻度一致。UserMask 采用 19×32 的 0-based 布尔矩阵；
/// 边框和异常点由计算器自动处理，不需要调用方重复构造。
/// </para>
/// </summary>
public sealed record CrosstalkAnalysisOptions
{
    public const double DefaultAbnormalThresholdRatio = 0.03d;
    public const double DefaultColorAxisMinimumPercent = 0d;
    public const double DefaultColorAxisMaximumPercent = 50d;

    public double AbnormalThresholdRatio { get; init; } = DefaultAbnormalThresholdRatio;
    public double ColorAxisMinimumPercent { get; init; } = DefaultColorAxisMinimumPercent;
    public double ColorAxisMaximumPercent { get; init; } = DefaultColorAxisMaximumPercent;

    /// <summary>用户手动画框或坐标输入得到的排除矩阵；null 表示没有用户掩膜。</summary>
    [JsonIgnore]
    public bool[,]? UserMask { get; init; }

    /// <summary>当前掩膜存档名称，仅用于 Parameters 工作表和历史记录。</summary>
    public string MaskName { get; init; } = string.Empty;

    /// <summary>当前掩膜备注，仅用于 Parameters 工作表和历史记录。</summary>
    public string MaskNote { get; init; } = string.Empty;

    /// <summary>创建默认参数对象，避免调用方依赖 record 属性初值。</summary>
    public static CrosstalkAnalysisOptions Default => new();

    /// <summary>
    /// 校验并返回一个可安全交给计算器的副本。数组会复制，避免分析过程中被界面线程修改。
    /// </summary>
    public CrosstalkAnalysisOptions Normalize(
        int rows = CrosstalkDataProcessor.HeatmapRows,
        int columns = CrosstalkDataProcessor.HeatmapColumns)
    {
        if (!double.IsFinite(AbnormalThresholdRatio))
            throw new ArgumentOutOfRangeException(nameof(AbnormalThresholdRatio),
                "异常阈值必须是有限数值。");
        if (!double.IsFinite(ColorAxisMinimumPercent) ||
            !double.IsFinite(ColorAxisMaximumPercent) ||
            ColorAxisMinimumPercent >= ColorAxisMaximumPercent)
        {
            throw new ArgumentOutOfRangeException(nameof(ColorAxisMaximumPercent),
                "色轴上下限必须是有限数值，且下限小于上限。");
        }
        if (rows <= 0 || columns <= 0)
            throw new ArgumentOutOfRangeException(nameof(rows), "掩膜尺寸必须为正数。");

        bool[,]? mask = null;
        if (UserMask is not null)
        {
            if (UserMask.GetLength(0) != rows || UserMask.GetLength(1) != columns)
                throw new ArgumentException(
                    $"用户掩膜尺寸应为 {rows}×{columns}，实际为 " +
                    $"{UserMask.GetLength(0)}×{UserMask.GetLength(1)}。",
                    nameof(UserMask));
            mask = (bool[,])UserMask.Clone();
        }

        return this with
        {
            UserMask = mask,
            MaskName = MaskName?.Trim() ?? string.Empty,
            MaskNote = MaskNote?.Trim() ?? string.Empty
        };
    }

    /// <summary>用百分比输入构造参数；例如 3 表示 3%（内部保存为 0.03）。</summary>
    public static CrosstalkAnalysisOptions FromPercent(
        double abnormalThresholdPercent,
        double colorAxisMinimumPercent = DefaultColorAxisMinimumPercent,
        double colorAxisMaximumPercent = DefaultColorAxisMaximumPercent,
        bool[,]? userMask = null,
        string? maskName = null,
        string? maskNote = null) => new()
        {
            AbnormalThresholdRatio = abnormalThresholdPercent / 100d,
            ColorAxisMinimumPercent = colorAxisMinimumPercent,
            ColorAxisMaximumPercent = colorAxisMaximumPercent,
            UserMask = userMask,
            MaskName = maskName ?? string.Empty,
            MaskNote = maskNote ?? string.Empty
        };

    /// <summary>界面显示用的百分比阈值（3% 对应 3）。</summary>
    [JsonIgnore]
    public double AbnormalThresholdPercent => AbnormalThresholdRatio * 100d;
}

/// <summary>掩膜存档中的一个 0-based 数据坐标点。</summary>
public readonly record struct CrosstalkMaskPoint(int Row, int Column);

/// <summary>与参考 Python 工具兼容的 .crosstalk_masks.json 条目。</summary>
public sealed record CrosstalkMaskArchive(
    string Name,
    string Note,
    int Rows,
    int Columns,
    IReadOnlyList<CrosstalkMaskPoint> Excluded);

/// <summary>串扰掩膜的坐标解析、存档和恢复工具。</summary>
public static class CrosstalkMaskStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// 读取掩膜存档。格式与参考程序一致：顶层键为名称，条目包含 note、shape、excluded。
    /// 不存在的文件返回空字典。
    /// </summary>
    public static IReadOnlyDictionary<string, CrosstalkMaskArchive> Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
            return new Dictionary<string, CrosstalkMaskArchive>(StringComparer.OrdinalIgnoreCase);

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("掩膜存档格式无效：顶层必须是对象。");

        var result = new Dictionary<string, CrosstalkMaskArchive>(StringComparer.OrdinalIgnoreCase);
        foreach (JsonProperty property in document.RootElement.EnumerateObject())
        {
            string name = property.Name.Trim();
            if (name.Length == 0) continue;
            JsonElement item = property.Value;
            if (item.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException($"掩膜“{name}”的存档条目不是对象。");

            int[] shape = ReadShape(item, name);
            string note = item.TryGetProperty("note", out JsonElement noteElement) &&
                          noteElement.ValueKind == JsonValueKind.String
                ? noteElement.GetString() ?? string.Empty
                : string.Empty;
            var points = new List<CrosstalkMaskPoint>();
            if (item.TryGetProperty("excluded", out JsonElement excluded))
            {
                if (excluded.ValueKind != JsonValueKind.Array)
                    throw new InvalidDataException($"掩膜“{name}”的 excluded 不是数组。");
                foreach (JsonElement point in excluded.EnumerateArray())
                {
                    if (point.ValueKind != JsonValueKind.Array || point.GetArrayLength() < 2)
                        continue;
                    if (!point[0].TryGetInt32(out int row) ||
                        !point[1].TryGetInt32(out int column))
                        continue;
                    // 越界点与 Python restore_mask 的行为一致：忽略而不是使整个存档失效。
                    if (row >= 0 && row < shape[0] && column >= 0 && column < shape[1])
                        points.Add(new CrosstalkMaskPoint(row, column));
                }
            }
            result[name] = new CrosstalkMaskArchive(name, note, shape[0], shape[1], points);
        }
        return result;
    }

    /// <summary>以指定名称保存掩膜，保留同一文件中其他存档，并采用临时文件原子替换。</summary>
    public static void Save(
        string path,
        string name,
        string? note,
        bool[,] mask)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(mask);
        if (mask.GetLength(0) <= 0 || mask.GetLength(1) <= 0)
            throw new ArgumentException("掩膜尺寸必须为正数。", nameof(mask));

        var records = new Dictionary<string, CrosstalkMaskArchive>(Load(path),
            StringComparer.OrdinalIgnoreCase)
        {
            [name.Trim()] = CreateArchive(name.Trim(), note ?? string.Empty, mask)
        };
        Write(path, records.Values);
    }

    /// <summary>删除一个掩膜存档；找不到名称时不报错。</summary>
    public static bool Delete(string path, string name)
    {
        if (!File.Exists(path) || string.IsNullOrWhiteSpace(name)) return false;
        var records = new Dictionary<string, CrosstalkMaskArchive>(Load(path),
            StringComparer.OrdinalIgnoreCase);
        bool removed = records.Remove(name.Trim());
        if (removed) Write(path, records.Values);
        return removed;
    }

    /// <summary>把存档恢复为指定尺寸的 0-based bool 矩阵；尺寸不一致会明确报错。</summary>
    public static bool[,] Restore(CrosstalkMaskArchive archive, int rows, int columns)
    {
        ArgumentNullException.ThrowIfNull(archive);
        if (archive.Rows != rows || archive.Columns != columns)
            throw new InvalidDataException(
                $"掩膜存档“{archive.Name}”尺寸为 {archive.Rows}×{archive.Columns}，" +
                $"与当前数据 {rows}×{columns} 不一致。");
        var mask = new bool[rows, columns];
        foreach (CrosstalkMaskPoint point in archive.Excluded)
        {
            if (point.Row >= 0 && point.Row < rows && point.Column >= 0 && point.Column < columns)
                mask[point.Row, point.Column] = true;
        }
        return mask;
    }

    /// <summary>按“x1,y1-x2,y2”解析多个 1-based、包含端点的矩形，分号/换行分隔。</summary>
    public static bool[,] ParseCoordinateRanges(
        string? text,
        int rows = CrosstalkDataProcessor.HeatmapRows,
        int columns = CrosstalkDataProcessor.HeatmapColumns)
    {
        if (rows <= 0 || columns <= 0)
            throw new ArgumentOutOfRangeException(nameof(rows));
        var result = new bool[rows, columns];
        if (string.IsNullOrWhiteSpace(text)) return result;

        foreach (string part in text.Split([';', '；', '\r', '\n'],
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] coordinateParts = part.Replace('，', ',').Split('-',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (coordinateParts.Length != 2)
                throw new FormatException($"无法识别范围“{part}”，请用 x1,y1-x2,y2。");
            int[] first = ParsePair(coordinateParts[0], part);
            int[] second = ParsePair(coordinateParts[1], part);
            AddRectangle(result, first[0], first[1], second[0], second[1]);
        }
        return result;

        static int[] ParsePair(string value, string original)
        {
            string[] numbers = value.Replace('，', ',').Split(',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (numbers.Length != 2 ||
                !int.TryParse(numbers[0], NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out int x) ||
                !int.TryParse(numbers[1], NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out int y))
                throw new FormatException($"无法识别范围“{original}”，请用 x1,y1-x2,y2。");
            return [x, y];
        }
    }

    /// <summary>添加一个 1-based、包含端点的矩形到现有掩膜。</summary>
    public static void AddRectangle(bool[,] mask, int x1, int y1, int x2, int y2)
    {
        ArgumentNullException.ThrowIfNull(mask);
        int rows = mask.GetLength(0), columns = mask.GetLength(1);
        int left = Math.Min(x1, x2), right = Math.Max(x1, x2);
        int top = Math.Min(y1, y2), bottom = Math.Max(y1, y2);
        if (left < 1 || top < 1 || right > columns || bottom > rows)
            throw new ArgumentOutOfRangeException(nameof(x1),
                $"坐标须位于 x=1..{columns}, y=1..{rows}。");
        for (int row = top - 1; row < bottom; row++)
            for (int column = left - 1; column < right; column++)
                mask[row, column] = true;
    }

    /// <summary>统计掩膜中已排除的点数。</summary>
    public static int Count(bool[,]? mask)
    {
        if (mask is null) return 0;
        int count = 0;
        foreach (bool value in mask) if (value) count++;
        return count;
    }

    private static CrosstalkMaskArchive CreateArchive(string name, string note, bool[,] mask)
    {
        var points = new List<CrosstalkMaskPoint>();
        for (int row = 0; row < mask.GetLength(0); row++)
            for (int column = 0; column < mask.GetLength(1); column++)
                if (mask[row, column]) points.Add(new CrosstalkMaskPoint(row, column));
        return new CrosstalkMaskArchive(name, note.Trim(), mask.GetLength(0), mask.GetLength(1), points);
    }

    private static int[] ReadShape(JsonElement item, string name)
    {
        if (!item.TryGetProperty("shape", out JsonElement shape) ||
            shape.ValueKind != JsonValueKind.Array || shape.GetArrayLength() < 2 ||
            !shape[0].TryGetInt32(out int rows) || !shape[1].TryGetInt32(out int columns) ||
            rows <= 0 || columns <= 0)
            throw new InvalidDataException($"掩膜“{name}”缺少有效的 shape。");
        return [rows, columns];
    }

    private static void Write(string path, IEnumerable<CrosstalkMaskArchive> values)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        var root = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (CrosstalkMaskArchive archive in values)
        {
            root[archive.Name] = new
            {
                note = archive.Note,
                shape = new[] { archive.Rows, archive.Columns },
                excluded = archive.Excluded.Select(point => new[] { point.Row, point.Column }).ToArray()
            };
        }
        string tempPath = path + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(root, JsonOptions));
        File.Move(tempPath, path, overwrite: true);
    }
}
