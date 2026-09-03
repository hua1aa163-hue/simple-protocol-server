using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using AutoTestClient.Models;

namespace AutoTestClient.DataProcessing;

/// <summary>Excel 单元格的原始文字和可选数值。</summary>
public sealed record ExcelCellValue(string Address, string Text, double? Number, bool Exists);

/// <summary>规则引擎选中的实际工作簿。</summary>
public sealed record ResultWorkbookSelection(
    string Path,
    bool IsFallback,
    int RelativeDepth,
    DateTime LastWriteTimeUtc);

/// <summary>测试开始前工作簿的轻量指纹，用来排除未变化的历史结果。</summary>
public sealed record ResultWorkbookFingerprint(long Length, DateTime LastWriteTimeUtc);

/// <summary>
/// 纯 C# 的 MRTEST 结果读取引擎。它会优先选择 ExportFile 中最深层的
/// 工作簿，并使用可编辑规则把单元格映射为 TestMetric。
/// </summary>
public static class ResultDisplayRuleEngine
{
    private static readonly TimeSpan RecentTolerance = TimeSpan.FromSeconds(5);
    // MRTEST may create an outer summary workbook before the nested detail
    // workbook.  Keep a short post-stability quiet window so the selector can
    // observe that deeper file before returning the outer candidate.
    private static readonly TimeSpan DetailQuietWindow = TimeSpan.FromSeconds(2);
    private static readonly string[] SupportedExtensions = [".xlsx", ".xlsm"];

    /// <summary>
    /// 选择测试开始后产生的工作簿。相同批次有外层汇总表和内层明细表时，
    /// RelativeDepth 排序确保内层明细表优先。allowHistoricalFallback 仅供
    /// 手动/诊断读取旧结果；自动测试会关闭它，避免重复读取上一批文件。
    /// </summary>
    public static ResultWorkbookSelection? SelectWorkbook(
        string exportDirectory,
        TestProject project,
        DateTime startedUtc,
        ISet<string>? excludedPaths = null,
        bool allowHistoricalFallback = true,
        TimeSpan? recentTolerance = null,
        IReadOnlyDictionary<string, ResultWorkbookFingerprint>? baseline = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exportDirectory);
        ArgumentNullException.ThrowIfNull(project);
        if (!Directory.Exists(exportDirectory)) return null;

        string root = Path.GetFullPath(exportDirectory);
        TimeSpan tolerance = recentTolerance ?? RecentTolerance;
        if (tolerance < TimeSpan.Zero) tolerance = TimeSpan.Zero;
        var candidates = new List<Candidate>();
        foreach (string path in EnumerateWorkbooks(exportDirectory))
        {
            string fullPath = Path.GetFullPath(path);
            if (excludedPaths is not null && excludedPaths.Contains(fullPath)) continue;
            try
            {
                var info = new FileInfo(fullPath);
                if (!info.Exists || info.Length == 0) continue;
                DateTime write = info.LastWriteTimeUtc;
                if (baseline is not null && baseline.TryGetValue(fullPath,
                        out ResultWorkbookFingerprint? old) &&
                    old.Length == info.Length && old.LastWriteTimeUtc == write)
                    continue;
                bool recent = startedUtc == default || write >= startedUtc - tolerance;
                candidates.Add(new Candidate(
                    fullPath,
                    RelativeDepth(root, fullPath),
                    MatchScore(fullPath, project),
                    recent,
                    write,
                    info.Length));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }

        if (candidates.Count == 0) return null;
        List<Candidate> recentCandidates = candidates.Where(item => item.Recent).ToList();
        bool fallback = recentCandidates.Count == 0;
        if (fallback && !allowHistoricalFallback) return null;
        IEnumerable<Candidate> pool = fallback ? candidates : recentCandidates;
        // If the path contains a project/recipe token, keep that branch before
        // comparing nesting depth.  The token is inherited by inner files via
        // their parent directory, so this still selects the deepest detail
        // workbook while avoiding a deeper workbook from another project.
        int bestMatchScore = pool.Max(item => item.MatchScore);
        if (bestMatchScore > 0)
            pool = pool.Where(item => item.MatchScore == bestMatchScore);
        Candidate selected = pool
            // MRTEST writes a summary workbook in the outer batch folder and
            // the usable detail workbook several levels below it.  Depth is
            // therefore the primary selector; token/timestamp are only
            // tie-breakers between files at the same detail level.
            .OrderByDescending(item => item.Depth)
            .ThenByDescending(item => item.MatchScore)
            .ThenByDescending(item => item.LastWriteUtc)
            .ThenByDescending(item => item.Length)
            .ThenBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
            .First();
        return new ResultWorkbookSelection(
            selected.Path, fallback, selected.Depth, selected.LastWriteUtc);
    }

    /// <summary>
    /// 等待文件稳定并返回选择结果；兼容调用默认允许历史回退，自动测试
    /// 通过 allowHistoricalFallback=false 只接受本轮开始后写入的文件。
    /// </summary>
    public static async Task<ResultWorkbookSelection?> WaitForWorkbookAsync(
        string exportDirectory,
        TestProject project,
        DateTime startedUtc,
        TimeSpan timeout,
        CancellationToken cancellationToken = default,
        ISet<string>? excludedPaths = null,
        IProgress<string>? progress = null,
        // Keep the public helper backward-compatible for manual/diagnostic
        // callers.  The automatic processing service explicitly passes false
        // so a stale workbook can never satisfy a new measurement.
        bool allowHistoricalFallback = true,
        TimeSpan? recentTolerance = null,
        IReadOnlyDictionary<string, ResultWorkbookFingerprint>? baseline = null)
    {
        if (timeout <= TimeSpan.Zero) timeout = TimeSpan.FromSeconds(60);
        DateTime deadline = DateTime.UtcNow + timeout;
        string? lastPath = null;
        long lastLength = -1;
        DateTime lastWrite = default;
        ResultWorkbookSelection? lastSelection = null;
        DateTime stableSinceUtc = default;

        while (DateTime.UtcNow <= deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ResultWorkbookSelection? selection = SelectWorkbook(
                exportDirectory, project, startedUtc, excludedPaths,
                allowHistoricalFallback, recentTolerance, baseline);
            if (selection is not null)
            {
                try
                {
                    var info = new FileInfo(selection.Path);
                    bool sameCandidate = string.Equals(lastPath, selection.Path,
                                      StringComparison.OrdinalIgnoreCase) &&
                                  lastLength == info.Length &&
                                  lastWrite == info.LastWriteTimeUtc &&
                                  info.Length > 0;
                    lastSelection = selection;
                    lastPath = selection.Path;
                    lastLength = info.Length;
                    lastWrite = info.LastWriteTimeUtc;
                    if (!sameCandidate)
                    {
                        stableSinceUtc = default;
                    }
                    else if (stableSinceUtc == default)
                    {
                        // The first identical observation establishes that
                        // the file is stable; keep polling for a quiet period
                        // so a deeper MRTEST detail workbook can appear.
                        stableSinceUtc = DateTime.UtcNow;
                    }
                    else if (DateTime.UtcNow - stableSinceUtc >= DetailQuietWindow &&
                             CanOpenZip(selection.Path))
                    {
                        return selection;
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
                {
                    progress?.Report("等待 Excel 导出完成：" + ex.Message);
                }
            }
            else
            {
                // A candidate can disappear while MRTEST is moving/renaming
                // its nested result directory.  Do not carry the previous
                // candidate's quiet-window timer across that gap.
                stableSinceUtc = default;
                lastPath = null;
                lastLength = -1;
                lastWrite = default;
                lastSelection = null;
            }

            if (DateTime.UtcNow >= deadline) break;
            await Task.Delay(TimeSpan.FromMilliseconds(350), cancellationToken).ConfigureAwait(false);
        }

        return lastSelection is not null &&
               (allowHistoricalFallback || !lastSelection.IsFallback) &&
               CanOpenZip(lastSelection.Path)
            ? lastSelection : null;
    }

    /// <summary>按规则从指定工作簿产生指标。</summary>
    public static IReadOnlyList<TestMetric> Extract(
        TestProject project,
        string workbookPath,
        IReadOnlyList<TestDataDisplayRule>? rules = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(workbookPath);
        if (project.Kind == TestProjectKind.Crosstalk) return Array.Empty<TestMetric>();
        if (!File.Exists(workbookPath))
            throw new FileNotFoundException("找不到 MRTEST 导出的 Excel 文件。", workbookPath);

        IReadOnlyList<TestDataDisplayRule> applicable = (rules ?? TestDataDisplayRule.CreateDefaultRules())
            .Where(rule => rule is not null && rule.Enabled &&
                           (rule.ProjectKind is null || rule.ProjectKind == project.Kind))
            .OrderBy(rule => rule.Order)
            .ThenBy(rule => rule.Name, StringComparer.CurrentCulture)
            .ToArray();

        using var workbook = new XlsxWorkbook(workbookPath);
        var result = new List<TestMetric>(applicable.Count);
        foreach (TestDataDisplayRule rule in applicable)
        {
            rule.Normalize(result.Count + 1);
            string dataRange = string.IsNullOrWhiteSpace(rule.DataCellOrRange)
                ? rule.CellOrRange : rule.DataCellOrRange;
            string key = BuildMetricKey(project, rule);
            // Keep the text entered in the editor intact, but normalize the
            // known MRTEST language/version aliases immediately before lookup.
            // This makes values such as “黑白对比度” and “SequentialContrast”
            // resolve to the same worksheet as the built-in “Contrast” rule.
            string requestedWorksheet = TestDataDisplayRule.ResolveWorksheetAlias(
                rule.Worksheet, project.Kind);
            string source = workbookPath + "!" +
                (requestedWorksheet.Length == 0 ? "<首工作表>" : requestedWorksheet) +
                "!" + dataRange;
            try
            {
                string sheet = workbook.ResolveWorksheet(requestedWorksheet);
                source = workbookPath + "!" + sheet + "!" + dataRange;
                IReadOnlyList<ExcelCellValue> values = workbook.ReadRange(sheet, dataRange);
                string value = Aggregate(values, rule.Aggregation, out bool hasValue);
                string name = ResolveName(workbook, sheet, rule, values);
                result.Add(new TestMetric(
                    key,
                    name,
                    value,
                    source,
                    hasValue ? "已计算" : "缺少数据"));
            }
            catch (Exception ex) when (ex is FormatException or InvalidDataException or KeyNotFoundException)
            {
                result.Add(new TestMetric(
                    key,
                    string.IsNullOrWhiteSpace(rule.OutputColumn) ? rule.Name : rule.OutputColumn,
                    string.Empty,
                    source,
                    "读取失败：" + ex.Message));
            }
        }
        return result;
    }

    public static IReadOnlyList<TestMetric> ExtractFromWorkbook(
        TestProject project,
        string workbookPath,
        IReadOnlyList<TestDataDisplayRule>? rules = null) =>
        Extract(project, workbookPath, rules);

    /// <summary>公开单元格读取，便于诊断和未来的规则编辑器预览。</summary>
    public static ExcelCellValue ReadCell(
        string workbookPath, string worksheet, string address)
    {
        using var workbook = new XlsxWorkbook(workbookPath);
        return workbook.ReadCell(workbook.ResolveWorksheet(worksheet), address);
    }

    public static IReadOnlyList<string> EnumerateWorkbooks(string exportDirectory)
    {
        if (!Directory.Exists(exportDirectory)) return Array.Empty<string>();
        try
        {
            return Directory.EnumerateFiles(exportDirectory, "*.*", SearchOption.AllDirectories)
                .Where(path => SupportedExtensions.Contains(
                    Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                .Where(path => !Path.GetFileName(path).StartsWith("~$", StringComparison.Ordinal))
                .Select(Path.GetFullPath)
                .ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// 记录测试开始前已存在的工作簿及其指纹。MRTEST 通常会创建带时间戳
    /// 的新目录，但部分版本会复用文件名；指纹可同时覆盖这两种行为。
    /// </summary>
    public static IReadOnlyDictionary<string, ResultWorkbookFingerprint>
        CaptureWorkbookSnapshot(string exportDirectory)
    {
        var result = new Dictionary<string, ResultWorkbookFingerprint>(
            StringComparer.OrdinalIgnoreCase);
        foreach (string path in EnumerateWorkbooks(exportDirectory))
        {
            try
            {
                var info = new FileInfo(path);
                if (info.Exists && info.Length > 0)
                    result[Path.GetFullPath(path)] =
                        new ResultWorkbookFingerprint(info.Length, info.LastWriteTimeUtc);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        return result;
    }

    private static string BuildMetricKey(TestProject project, TestDataDisplayRule rule)
    {
        string identity = string.IsNullOrWhiteSpace(rule.OutputColumn)
            ? rule.Name : rule.OutputColumn;
        if (string.IsNullOrWhiteSpace(identity)) identity = rule.DataCellOrRange;
        // Include the source coordinates as a stable discriminator.  The
        // editor intentionally permits duplicate display labels/order values;
        // those rules must remain separate rows instead of being merged by the
        // dashboard's repeated-result grouping.
        return ((int)project.Kind).ToString(CultureInfo.InvariantCulture) + ":" +
               rule.Order.ToString(CultureInfo.InvariantCulture) + ":" + identity.Trim() + ":" +
               (rule.Worksheet ?? string.Empty).Trim() + ":" +
               (rule.DataCellOrRange ?? rule.CellOrRange ?? string.Empty).Trim() + ":" +
               (rule.NameCellOrLabel ?? string.Empty).Trim();
    }

    private static string ResolveName(
        XlsxWorkbook workbook,
        string sheet,
        TestDataDisplayRule rule,
        IReadOnlyList<ExcelCellValue> data)
    {
        // OutputColumn is an optional user-facing report-column override.
        // Built-in rules leave it empty so the requested Excel name cell (or
        // fixed label for an aggregate row) is used by default.  A user can
        // fill OutputColumn in the editor when a normalized report label is
        // preferred; clearing it restores the source name.
        string outputColumn = (rule.OutputColumn ?? string.Empty).Trim();
        if (outputColumn.Length > 0) return outputColumn;

        string configured = (rule.NameCellOrLabel ?? string.Empty).Trim();
        string name = string.Empty;
        if (configured.Length > 0)
        {
            if (LooksLikeCellReference(configured))
                name = workbook.ReadCell(sheet, configured).Text.Trim();
            else
                name = configured;
        }
        if (name.Length == 0) name = (rule.Name ?? string.Empty).Trim();
        if (name.Length == 0)
            name = data.FirstOrDefault(item => item.Text.Length > 0)?.Address ?? "指标";
        return name;
    }

    private static string Aggregate(
        IReadOnlyList<ExcelCellValue> values,
        TestDataAggregation aggregation,
        out bool hasValue)
    {
        string[] texts = values.Select(item => item.Text.Trim())
            .Where(item => item.Length > 0).ToArray();
        hasValue = texts.Length > 0;
        if (!hasValue) return string.Empty;
        switch (aggregation)
        {
            case TestDataAggregation.None: return string.Join("；", texts);
            case TestDataAggregation.First: return texts[0];
            case TestDataAggregation.Last: return texts[^1];
            case TestDataAggregation.Count:
                return texts.Length.ToString(CultureInfo.InvariantCulture);
        }

        double[] numbers = values.Select(item => ParseNumber(item.Text, item.Number))
            .Where(item => item.HasValue && double.IsFinite(item.Value))
            .Select(item => item!.Value).ToArray();
        if (numbers.Length == 0)
        {
            hasValue = false;
            return string.Empty;
        }
        double aggregate = aggregation switch
        {
            TestDataAggregation.Average => numbers.Average(),
            TestDataAggregation.Minimum => numbers.Min(),
            TestDataAggregation.Maximum => numbers.Max(),
            TestDataAggregation.Sum => numbers.Sum(),
            _ => numbers[0]
        };
        return aggregate.ToString("0.######", CultureInfo.InvariantCulture) +
               InferUnitSuffix(texts);
    }

    private static double? ParseNumber(string text, double? raw)
    {
        if (raw.HasValue && double.IsFinite(raw.Value)) return raw.Value;
        if (string.IsNullOrWhiteSpace(text)) return null;
        string normalized = text.Replace(',', '.');
        var builder = new StringBuilder();
        bool digit = false;
        bool dot = false;
        bool exponent = false;
        bool exponentSignAllowed = false;
        foreach (char c in normalized)
        {
            if (char.IsDigit(c))
            {
                builder.Append(c);
                digit = true;
                exponentSignAllowed = false;
                continue;
            }
            if (!digit && builder.Length == 0 && (c == '+' || c == '-'))
            {
                builder.Append(c);
                continue;
            }
            if (c == '.' && !dot && !exponent)
            {
                builder.Append(c);
                dot = true;
                continue;
            }
            if ((c == 'e' || c == 'E') && digit && !exponent)
            {
                builder.Append(c);
                exponent = true;
                exponentSignAllowed = true;
                continue;
            }
            if (exponent && exponentSignAllowed && (c == '+' || c == '-'))
            {
                builder.Append(c);
                exponentSignAllowed = false;
                continue;
            }
            if (digit) break;
        }
        return double.TryParse(builder.ToString(), NumberStyles.Float,
            CultureInfo.InvariantCulture, out double value) ? value : null;
    }

    private static string InferUnitSuffix(IEnumerable<string> texts)
    {
        foreach (string text in texts)
        {
            if (text.Contains('%', StringComparison.Ordinal)) return "%";
            if (text.Contains('°', StringComparison.Ordinal)) return "°";
            if (text.Contains('℃', StringComparison.Ordinal)) return "℃";
            if (text.Contains("cd/m", StringComparison.OrdinalIgnoreCase)) return "cd/m²";
        }
        return string.Empty;
    }

    private static bool LooksLikeCellReference(string value)
    {
        string text = value.Replace("$", string.Empty, StringComparison.Ordinal).Trim();
        int index = 0;
        while (index < text.Length && char.IsLetter(text[index])) index++;
        return index is > 0 and <= 3 && index < text.Length &&
               text[index..].All(char.IsDigit);
    }

    private static int RelativeDepth(string root, string path)
    {
        string relative;
        try { relative = Path.GetRelativePath(root, path); }
        catch { relative = path; }
        return relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar,
            StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private static int MatchScore(string path, TestProject project)
    {
        string text = path.Replace('\\', '/');
        int score = 0;
        foreach (string token in ProjectTokens(project))
            if (!string.IsNullOrWhiteSpace(token) &&
                text.Contains(token, StringComparison.OrdinalIgnoreCase))
                score += token.Length >= 4 ? 3 : 1;
        return score;
    }

    private static IEnumerable<string> ProjectTokens(TestProject project)
    {
        if (!string.IsNullOrWhiteSpace(project.Name)) yield return project.Name.Trim();
        if (!string.IsNullOrWhiteSpace(project.RecipeName)) yield return project.RecipeName.Trim();
        switch (project.Kind)
        {
            case TestProjectKind.Fov: yield return "FOV"; yield return "视场"; break;
            case TestProjectKind.Contrast: yield return "对比度"; yield return "Contrast"; yield return "黑白"; break;
            case TestProjectKind.BrightnessUniformity: yield return "均匀性"; yield return "Uniform"; break;
            case TestProjectKind.Gamut: yield return "色域"; yield return "Gamut"; yield return "Chromaticity"; break;
            case TestProjectKind.Distortion: yield return "畸变"; yield return "Distortion"; break;
            case TestProjectKind.Crosstalk: yield return "串扰"; yield return "Crosstalk"; break;
        }
    }

    private static bool CanOpenZip(string path)
    {
        try
        {
            using ZipArchive archive = ZipFile.OpenRead(path);
            return archive.GetEntry("xl/workbook.xml") is not null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return false;
        }
    }

    private sealed record Candidate(
        string Path, int Depth, int MatchScore, bool Recent,
        DateTime LastWriteUtc, long Length);

    private sealed class XlsxWorkbook : IDisposable
    {
        private readonly ZipArchive _archive;
        private readonly Dictionary<string, string> _sheets;
        private readonly string[] _sharedStrings;
        private readonly Dictionary<string, Dictionary<string, ExcelCellValue>> _cache =
            new(StringComparer.OrdinalIgnoreCase);

        public XlsxWorkbook(string path)
        {
            try { _archive = ZipFile.OpenRead(path); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                throw new InvalidDataException("无法打开 Excel 文件：" + ex.Message, ex);
            }
            try
            {
                XDocument workbook = LoadXml("xl/workbook.xml");
                _sheets = ResolveSheets(workbook, LoadXml("xl/_rels/workbook.xml.rels"));
                _sharedStrings = ReadSharedStrings();
            }
            catch
            {
                _archive.Dispose();
                throw;
            }
        }

        public string ResolveWorksheet(string requested)
        {
            string text = (requested ?? string.Empty).Trim();
            if (text.Length > 0)
            {
                string? exact = _sheets.Keys.FirstOrDefault(key =>
                    string.Equals(key, text, StringComparison.OrdinalIgnoreCase));
                if (exact is not null) return exact;
                string normalized = NormalizeSheet(text);
                string? fuzzy = _sheets.Keys.FirstOrDefault(key =>
                    NormalizeSheet(key).Contains(normalized, StringComparison.OrdinalIgnoreCase) ||
                    normalized.Contains(NormalizeSheet(key), StringComparison.OrdinalIgnoreCase));
                if (fuzzy is not null) return fuzzy;
                throw new KeyNotFoundException(
                    "Excel 中没有找到工作表“" + text + "”。");
            }
            return _sheets.Keys.FirstOrDefault() ??
                throw new InvalidDataException("Excel 工作簿没有可读取的工作表。");
        }

        public ExcelCellValue ReadCell(string sheet, string address)
        {
            string normalized = NormalizeAddress(address);
            if (!LooksLikeCellReference(normalized))
                throw new FormatException("无效的 Excel 单元格地址：" + address);
            Dictionary<string, ExcelCellValue> cells = LoadCells(sheet);
            return cells.TryGetValue(normalized, out ExcelCellValue? value)
                ? value : new ExcelCellValue(normalized, string.Empty, null, false);
        }

        public IReadOnlyList<ExcelCellValue> ReadRange(string sheet, string range)
        {
            if (string.IsNullOrWhiteSpace(range))
                throw new FormatException("规则没有填写数据单元格/范围。");
            // 允许用户在规则编辑器中使用常见的 C4~C5/ C4～C5 写法，
            // 同时保留 Excel 原生的冒号范围语法。
            range = range.Replace('~', ':').Replace('～', ':');
            var addresses = new List<string>();
            foreach (string token in range.Split(
                         [',', '，', '、', ';', '|'],
                         StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (token.Contains(':', StringComparison.Ordinal))
                {
                    string[] ends = token.Split(':', 2, StringSplitOptions.TrimEntries);
                    addresses.AddRange(ExpandRange(ends[0], ends[1]));
                }
                else
                {
                    if (!LooksLikeCellReference(token))
                        throw new FormatException("无效的 Excel 单元格/范围：" + token);
                    addresses.Add(NormalizeAddress(token));
                }
            }
            return addresses.Select(address => ReadCell(sheet, address)).ToArray();
        }

        private Dictionary<string, ExcelCellValue> LoadCells(string sheet)
        {
            if (_cache.TryGetValue(sheet, out Dictionary<string, ExcelCellValue>? cached))
                return cached;
            if (!_sheets.TryGetValue(sheet, out string? entryPath))
                throw new KeyNotFoundException("Excel 中没有找到工作表“" + sheet + "”。");
            XDocument document = LoadXml(entryPath);
            var cells = new Dictionary<string, ExcelCellValue>(StringComparer.OrdinalIgnoreCase);
            foreach (XElement row in document.Descendants().Where(item => item.Name.LocalName == "row"))
            {
                int rowNumber = ParseInt(AttributeValue(row, "r"));
                int fallbackColumn = 0;
                foreach (XElement cell in row.Elements().Where(item => item.Name.LocalName == "c"))
                {
                    string address = NormalizeAddress(AttributeValue(cell, "r"));
                    if (!LooksLikeCellReference(address))
                    {
                        address = ColumnName(++fallbackColumn) + Math.Max(1, rowNumber);
                    }
                    else
                    {
                        fallbackColumn = ColumnNumber(address);
                    }
                    int actualRow = RowNumber(address);
                    if (actualRow <= 0) actualRow = Math.Max(1, rowNumber);
                    address = ColumnName(Math.Max(1, fallbackColumn)) + actualRow;
                    cells[address] = DecodeCell(cell, address);
                }
            }
            _cache[sheet] = cells;
            return cells;
        }

        private ExcelCellValue DecodeCell(XElement cell, string address)
        {
            string type = AttributeValue(cell, "t");
            XElement? valueNode = cell.Elements().FirstOrDefault(item => item.Name.LocalName == "v");
            string raw = valueNode?.Value ?? string.Empty;
            string text;
            if (type.Equals("inlineStr", StringComparison.OrdinalIgnoreCase))
                text = string.Concat(cell.Descendants().Where(item => item.Name.LocalName == "t")
                    .Select(item => item.Value));
            else if (type.Equals("s", StringComparison.OrdinalIgnoreCase) &&
                     int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture,
                         out int index) && index >= 0 && index < _sharedStrings.Length)
                text = _sharedStrings[index];
            else if (type.Equals("b", StringComparison.OrdinalIgnoreCase))
                text = raw == "1" ? "TRUE" : "FALSE";
            else
                text = raw;
            double? number = double.TryParse(text, NumberStyles.Float,
                CultureInfo.InvariantCulture, out double parsed) && double.IsFinite(parsed)
                ? parsed : null;
            return new ExcelCellValue(address, text, number, true);
        }

        private Dictionary<string, string> ResolveSheets(XDocument workbook, XDocument relationships)
        {
            var relMap = relationships.Descendants()
                .Where(item => item.Name.LocalName == "Relationship")
                .Select(item => new
                {
                    Id = AttributeValue(item, "Id"),
                    Target = AttributeValue(item, "Target")
                })
                .Where(item => item.Id.Length > 0)
                .ToDictionary(item => item.Id, item => ResolveTarget(item.Target),
                    StringComparer.OrdinalIgnoreCase);
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (XElement sheet in workbook.Descendants().Where(item => item.Name.LocalName == "sheet"))
            {
                string name = AttributeValue(sheet, "name").Trim();
                string id = AttributeValue(sheet, "id");
                if (name.Length > 0 && relMap.TryGetValue(id, out string? target))
                    result[name] = target;
            }
            return result;
        }

        private string[] ReadSharedStrings()
        {
            ZipArchiveEntry? entry = _archive.Entries.FirstOrDefault(item =>
                string.Equals(item.FullName, "xl/sharedStrings.xml", StringComparison.OrdinalIgnoreCase));
            if (entry is null) return Array.Empty<string>();
            using Stream stream = entry.Open();
            XDocument document = XDocument.Load(stream);
            return document.Descendants().Where(item => item.Name.LocalName == "si")
                .Select(item => string.Concat(item.Descendants()
                    .Where(text => text.Name.LocalName == "t").Select(text => text.Value)))
                .ToArray();
        }

        private XDocument LoadXml(string path)
        {
            ZipArchiveEntry? entry = _archive.Entries.FirstOrDefault(item =>
                string.Equals(item.FullName, path, StringComparison.OrdinalIgnoreCase));
            if (entry is null) throw new InvalidDataException("Excel 文件缺少必要内容：" + path);
            using Stream stream = entry.Open();
            return XDocument.Load(stream);
        }

        public void Dispose() => _archive.Dispose();

        private static string AttributeValue(XElement element, string name) =>
            element.Attributes().FirstOrDefault(attribute =>
                string.Equals(attribute.Name.LocalName, name, StringComparison.OrdinalIgnoreCase))?.Value
            ?? string.Empty;

        private static string ResolveTarget(string target)
        {
            string normalized = target.Replace('\\', '/').Trim();
            if (normalized.StartsWith('/')) normalized = normalized.TrimStart('/');
            var parts = new List<string> { "xl" };
            foreach (string part in normalized.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                if (part == ".") continue;
                if (part == "..")
                {
                    if (parts.Count > 1) parts.RemoveAt(parts.Count - 1);
                }
                else if (!(parts.Count == 1 && part.Equals("xl", StringComparison.OrdinalIgnoreCase)))
                    parts.Add(part);
            }
            return string.Join('/', parts);
        }

        private static string NormalizeSheet(string value)
        {
            string normalized = new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
            return normalized switch
            {
                "fov" => "fieldofview",
                "contrast" => "sequentialcontrast",
                "gamut" => "chromaticity",
                _ => normalized
            };
        }
    }

    private static IEnumerable<string> ExpandRange(string first, string last)
    {
        if (!LooksLikeCellReference(first) || !LooksLikeCellReference(last))
            throw new FormatException("无效的 Excel 范围：" + first + ":" + last);
        int firstColumn = ColumnNumber(first), lastColumn = ColumnNumber(last);
        int firstRow = RowNumber(first), lastRow = RowNumber(last);
        if (firstColumn > lastColumn) (firstColumn, lastColumn) = (lastColumn, firstColumn);
        if (firstRow > lastRow) (firstRow, lastRow) = (lastRow, firstRow);
        for (int row = firstRow; row <= lastRow; row++)
            for (int column = firstColumn; column <= lastColumn; column++)
                yield return ColumnName(column) + row;
    }

    private static string NormalizeAddress(string value) =>
        (value ?? string.Empty).Replace("$", string.Empty, StringComparison.Ordinal)
            .Trim().ToUpperInvariant();

    private static int ColumnNumber(string address)
    {
        int result = 0;
        foreach (char c in NormalizeAddress(address))
        {
            if (!char.IsLetter(c)) break;
            result = checked(result * 26 + c - 'A' + 1);
        }
        return result;
    }

    private static int RowNumber(string address)
    {
        string digits = new(NormalizeAddress(address).Where(char.IsDigit).ToArray());
        return int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture,
            out int result) ? result : 0;
    }

    private static int ParseInt(string value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result)
            ? result : 0;

    private static string ColumnName(int number)
    {
        var builder = new StringBuilder();
        while (number > 0)
        {
            number--;
            builder.Insert(0, (char)('A' + number % 26));
            number /= 26;
        }
        return builder.ToString();
    }
}
