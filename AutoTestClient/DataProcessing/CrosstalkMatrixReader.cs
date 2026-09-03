using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace AutoTestClient.DataProcessing;

/// <summary>
/// 可供串扰分析和后续数据处理复用的二维数值矩阵读取器。
/// <para>
/// 这是参考分析器 <c>read_matrix</c>/<c>list_data_files</c> 的纯 C# 实现；
/// 只使用 .NET 自带的文本、ZIP 和 XML API，不启动 Python，也不依赖第三方包。
/// CSV、TSV、TXT、XLSX 以及常见的 NumPy NPY（数值二维矩阵）均可读取。
/// </para>
/// </summary>
public static class CrosstalkMatrixReader
{
    private static readonly string[] SupportedSuffixArray =
        [".csv", ".tsv", ".txt", ".xlsx", ".npy"];

    private static readonly HashSet<string> SupportedSuffixSet =
        new(SupportedSuffixArray, StringComparer.OrdinalIgnoreCase);

    private const string SpreadsheetNamespace =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string OfficeRelationshipNamespace =
        "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const string PackageRelationshipNamespace =
        "http://schemas.openxmlformats.org/package/2006/relationships";

    /// <summary>参考工具支持的文件扩展名（小写，不含点号之外的路径信息）。</summary>
    public static IReadOnlySet<string> SupportedSuffixes => SupportedSuffixSet;

    /// <summary>
    /// 列出目录中支持的矩阵文件，排序规则与参考工具一致：先按修改时间，再按文件名。
    /// </summary>
    public static IReadOnlyList<string> ListDataFiles(string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        if (!Directory.Exists(folder))
            throw new DirectoryNotFoundException($"数据文件夹不存在：{folder}");

        return Directory.EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly)
            .Where(path => SupportedSuffixSet.Contains(Path.GetExtension(path)))
            .OrderBy(path => File.GetLastWriteTimeUtc(path))
            .ThenBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>读取一个非空二维数值矩阵，并移除任意位置全为 NaN 的行/列。</summary>
    public static double[,] ReadMatrix(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
            throw new FileNotFoundException($"找不到矩阵文件：{path}", path);

        string suffix = Path.GetExtension(path).ToLowerInvariant();
        double[,] matrix = suffix switch
        {
            ".xlsx" => ReadXlsx(path),
            ".npy" => ReadNpy(path),
            ".csv" or ".tsv" or ".txt" => ReadDelimited(path, suffix),
            _ => throw new InvalidDataException(
                $"不支持的矩阵文件类型“{suffix}”，支持：{string.Join(", ", SupportedSuffixArray)}。")
        };

        return RemoveAllNanRowsAndColumns(matrix, Path.GetFileName(path));
    }

    private static double[,] ReadDelimited(string path, string suffix)
    {
        string text;
        try
        {
            text = File.ReadAllText(path, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false,
                throwOnInvalidBytes: true));
        }
        catch (DecoderFallbackException)
        {
            // MRTEST 导出目录中仍可能存在 GB18030 文本；注册和使用系统代码页
            // 不会引入 Python 或任何外部运行时。
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            text = File.ReadAllText(path, Encoding.GetEncoding(936));
        }

        text = text.TrimStart('\uFEFF');
        string[] lines = text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
        if (lines.Length == 0)
            throw new InvalidDataException($"{Path.GetFileName(path)} 不是非空二维数值矩阵。");

        char? delimiter = suffix == ".tsv" ? '\t' : DetectDelimiter(lines);
        var rows = new List<double[]>(lines.Length);
        int maximumColumns = 0;
        foreach (string originalLine in lines)
        {
            string line = originalLine.TrimStart('\uFEFF');
            double[] values = delimiter is char separator
                ? ParseDelimitedLine(line, separator)
                : ParseWhitespaceLine(line);
            rows.Add(values);
            maximumColumns = Math.Max(maximumColumns, values.Length);
        }

        // read_csv 会把空白行当作 NaN 行；先保留行列结构，统一在读取结束时
        // 按参考逻辑移除所有全 NaN 行/列，内部有效数据不会被重新排序。
        if (rows.Count == 0 || maximumColumns == 0)
            throw new InvalidDataException($"{Path.GetFileName(path)} 不是非空二维数值矩阵。");

        var matrix = FillWithNan(rows.Count, maximumColumns);
        for (int row = 0; row < rows.Count; row++)
        {
            double[] values = rows[row];
            for (int column = 0; column < values.Length; column++)
                matrix[row, column] = values[column];
        }
        return matrix;
    }

    private static char? DetectDelimiter(IReadOnlyList<string> lines)
    {
        // Python's sep=None engine auto-detects common separators.  Count separators
        // on the first few non-empty lines and choose the one with the most fields.
        char[] candidates = ['\t', ',', ';', '|'];
        string[] sample = lines.Where(line => !string.IsNullOrWhiteSpace(line))
            .Take(20).ToArray();
        if (sample.Length == 0) return null;

        char? selected = null;
        int bestScore = 0;
        foreach (char candidate in candidates)
        {
            int score = sample.Sum(line => CountUnquoted(line, candidate));
            if (score > bestScore)
            {
                bestScore = score;
                selected = candidate;
            }
        }
        return selected ?? (sample.Any(line => Regex.IsMatch(line.Trim(), @"\s+")) ? null : ',');
    }

    private static int CountUnquoted(string line, char separator)
    {
        bool quoted = false;
        int count = 0;
        for (int index = 0; index < line.Length; index++)
        {
            char character = line[index];
            if (character == '"')
            {
                if (quoted && index + 1 < line.Length && line[index + 1] == '"') index++;
                else quoted = !quoted;
            }
            else if (!quoted && character == separator) count++;
        }
        return count;
    }

    private static double[] ParseDelimitedLine(string line, char separator)
    {
        var fields = new List<string>();
        var builder = new StringBuilder();
        bool quoted = false;
        for (int index = 0; index < line.Length; index++)
        {
            char character = line[index];
            if (character == '"')
            {
                if (quoted && index + 1 < line.Length && line[index + 1] == '"')
                {
                    builder.Append('"');
                    index++;
                }
                else quoted = !quoted;
            }
            else if (character == separator && !quoted)
            {
                fields.Add(builder.ToString());
                builder.Clear();
            }
            else builder.Append(character);
        }
        fields.Add(builder.ToString());
        return fields.Select(ParseNumberOrNan).ToArray();
    }

    private static double[] ParseWhitespaceLine(string line)
    {
        string trimmed = line.Trim();
        if (trimmed.Length == 0) return [double.NaN];
        return Regex.Split(trimmed, @"\s+").Select(ParseNumberOrNan).ToArray();
    }

    private static double ParseNumberOrNan(string text)
    {
        string value = text.Trim();
        if (value.Length == 0) return double.NaN;
        // pandas.to_numeric recognises the usual IEEE spellings; .NET's
        // parser does not accept the short `inf` form on every runtime.
        if (string.Equals(value, "inf", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "+inf", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "infinity", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "+infinity", StringComparison.OrdinalIgnoreCase))
            return double.PositiveInfinity;
        if (string.Equals(value, "-inf", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "-infinity", StringComparison.OrdinalIgnoreCase))
            return double.NegativeInfinity;
        if (double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture, out double number)) return number;
        if (double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.CurrentCulture, out number)) return number;
        return double.NaN;
    }

    private static double[,] ReadXlsx(string path)
    {
        using ZipArchive archive = ZipFile.OpenRead(path);
        XDocument workbook = LoadXmlEntry(archive, "xl/workbook.xml");
        // Transitional XLSX uses the namespace below; Strict OOXML uses a
        // different URI but the same element names.  Taking the workbook's
        // namespace keeps both variants readable without an external parser.
        XNamespace spreadsheet = workbook.Root?.Name.Namespace is XNamespace workbookNamespace &&
                                  workbookNamespace.NamespaceName.Length > 0
            ? workbookNamespace
            : SpreadsheetNamespace;
        XElement? sheet = workbook.Descendants(spreadsheet + "sheet").FirstOrDefault();
        if (sheet is null)
            throw new InvalidDataException($"Excel 文件“{Path.GetFileName(path)}”没有工作表。");

        string relationshipId = (string?)sheet.Attribute(XNamespace.Get(OfficeRelationshipNamespace) + "id")
            ?? throw new InvalidDataException("Excel 首个工作表缺少关系编号。");
        XDocument relationships = LoadXmlEntry(archive, "xl/_rels/workbook.xml.rels");
        string target = relationships.Descendants(XNamespace.Get(PackageRelationshipNamespace) + "Relationship")
            .Where(item => (string?)item.Attribute("Id") == relationshipId)
            .Select(item => (string?)item.Attribute("Target"))
            .FirstOrDefault()
            ?? throw new InvalidDataException("无法定位 Excel 首个工作表。");
        string worksheetPath = ResolveWorkbookTarget(Uri.UnescapeDataString(target));
        XDocument worksheet = LoadXmlEntry(archive, worksheetPath);
        IReadOnlyList<string> sharedStrings = ReadSharedStrings(archive, spreadsheet);

        var values = new Dictionary<(int Row, int Column), double>();
        int maximumRow = 0;
        int maximumColumn = 0;
        int fallbackRow = 0;
        foreach (XElement row in worksheet.Descendants(spreadsheet + "row"))
        {
            int rowNumber = ParsePositiveInteger((string?)row.Attribute("r"));
            if (rowNumber <= 0)
            {
                // The row number is optional in SpreadsheetML.  Prefer the
                // first explicit cell reference (for example A5) before
                // falling back to document order, so sparse worksheets keep
                // their coordinates even when `<row r>` was omitted.
                rowNumber = row.Elements(spreadsheet + "c")
                    .Select(cell => GetRowNumber((string?)cell.Attribute("r")))
                    .FirstOrDefault(value => value > 0);
                if (rowNumber <= 0) rowNumber = ++fallbackRow;
            }
            else fallbackRow = rowNumber;
            int fallbackColumn = 0;
            foreach (XElement cell in row.Elements(spreadsheet + "c"))
            {
                string reference = ((string?)cell.Attribute("r") ?? string.Empty).Replace("$", "",
                    StringComparison.Ordinal);
                int columnNumber = GetColumnNumber(reference);
                if (columnNumber <= 0) columnNumber = ++fallbackColumn;
                else fallbackColumn = columnNumber;
                if (rowNumber <= 0 || columnNumber <= 0) continue;
                values[(rowNumber, columnNumber)] = ReadXlsxCell(cell, sharedStrings, spreadsheet);
                maximumRow = Math.Max(maximumRow, rowNumber);
                maximumColumn = Math.Max(maximumColumn, columnNumber);
            }
        }

        if (maximumRow <= 0 || maximumColumn <= 0)
            throw new InvalidDataException($"Excel 文件“{Path.GetFileName(path)}”没有可读取的单元格。");
        var matrix = FillWithNan(maximumRow, maximumColumn);
        foreach (((int row, int column), double value) in values)
            matrix[row - 1, column - 1] = value;
        return matrix;
    }

    private static double ReadXlsxCell(XElement cell, IReadOnlyList<string> sharedStrings,
        XNamespace spreadsheet)
    {
        string type = (string?)cell.Attribute("t") ?? string.Empty;
        string text;
        if (type == "s")
        {
            string indexText = cell.Element(spreadsheet + "v")?.Value ?? string.Empty;
            text = int.TryParse(indexText, NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out int index) && index >= 0 && index < sharedStrings.Count
                ? sharedStrings[index] : string.Empty;
        }
        else if (type == "inlineStr")
        {
            text = string.Concat(cell.Descendants(spreadsheet + "t").Select(item => item.Value));
        }
        else if (type == "b")
        {
            text = (cell.Element(spreadsheet + "v")?.Value ?? string.Empty) == "1" ? "1" : "0";
        }
        else if (type == "e")
        {
            text = string.Empty;
        }
        else
        {
            // 公式单元格也读取 Excel 保存的缓存 v 值；没有缓存就按 NaN 处理。
            text = cell.Element(spreadsheet + "v")?.Value ?? string.Empty;
        }
        return ParseNumberOrNan(text);
    }

    private static IReadOnlyList<string> ReadSharedStrings(ZipArchive archive, XNamespace spreadsheet)
    {
        ZipArchiveEntry? entry = archive.Entries.FirstOrDefault(item =>
            string.Equals(item.FullName, "xl/sharedStrings.xml", StringComparison.OrdinalIgnoreCase));
        if (entry is null) return Array.Empty<string>();
        using Stream stream = entry.Open();
        XDocument document = XDocument.Load(stream);
        return document.Descendants(spreadsheet + "si")
            .Select(item => string.Concat(item.Descendants(spreadsheet + "t").Select(text => text.Value)))
            .ToArray();
    }

    private static XDocument LoadXmlEntry(ZipArchive archive, string path)
    {
        ZipArchiveEntry? entry = archive.Entries.FirstOrDefault(item =>
            string.Equals(item.FullName, path, StringComparison.OrdinalIgnoreCase));
        if (entry is null) throw new InvalidDataException($"Excel 文件缺少必要内容：{path}");
        using Stream stream = entry.Open();
        return XDocument.Load(stream);
    }

    private static string ResolveWorkbookTarget(string target)
    {
        string normalized = target.Replace('\\', '/');
        if (normalized.StartsWith("/", StringComparison.Ordinal)) return normalized.TrimStart('/');
        var parts = new List<string> { "xl" };
        foreach (string part in normalized.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == ".") continue;
            if (part == "..")
            {
                if (parts.Count > 0) parts.RemoveAt(parts.Count - 1);
                continue;
            }
            parts.Add(part);
        }
        return string.Join('/', parts);
    }

    private static int GetColumnNumber(string reference)
    {
        int column = 0;
        foreach (char character in reference)
        {
            if (!char.IsLetter(character)) break;
            column = checked(column * 26 + char.ToUpperInvariant(character) - 'A' + 1);
        }
        return column;
    }

    private static int GetRowNumber(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference)) return 0;
        string digits = new(reference.Where(char.IsDigit).ToArray());
        return ParsePositiveInteger(digits);
    }

    private static int ParsePositiveInteger(string? text) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) && value > 0
            ? value : 0;

    private static double[,] ReadNpy(string path)
    {
        using FileStream stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);
        byte[] magic = reader.ReadBytes(6);
        // NPY uses a literal 0x93 byte; encoding the string "\x93NUMPY" as
        // UTF-8 would incorrectly produce two bytes (C2 93) for that prefix.
        ReadOnlySpan<byte> expectedMagic = stackalloc byte[] { 0x93, (byte)'N', (byte)'U',
            (byte)'M', (byte)'P', (byte)'Y' };
        if (magic.Length != expectedMagic.Length || !magic.AsSpan().SequenceEqual(expectedMagic))
            throw new InvalidDataException($"NPY 文件“{Path.GetFileName(path)}”头部无效。");
        int major = reader.ReadByte();
        _ = reader.ReadByte(); // minor
        long headerLength = major switch
        {
            1 => reader.ReadUInt16(),
            2 or 3 => reader.ReadUInt32(),
            _ => throw new InvalidDataException($"不支持的 NPY 版本：{major}。")
        };
        if (headerLength <= 0 || headerLength > 1024 * 1024)
            throw new InvalidDataException("NPY 头部长度无效。");
        byte[] headerBytes = reader.ReadBytes(checked((int)headerLength));
        if (headerBytes.Length != headerLength)
            throw new InvalidDataException("NPY 文件头部不完整。");
        string header = Encoding.UTF8.GetString(headerBytes).Trim();
        string descriptor = ParseNpyDescriptor(header);
        bool fortran = ParseNpyFortranOrder(header);
        int[] shape = ParseNpyShape(header);
        if (shape.Any(value => value < 0))
            throw new InvalidDataException("NPY 文件必须是可压缩为二维的数值矩阵。");

        // Keep genuine 2-D singleton dimensions (1×N, N×1 and 1×1).  For a
        // higher-rank array, retain the reference reader's squeeze behavior
        // only when exactly two non-singleton dimensions remain.
        int rowDimension;
        int columnDimension;
        int rows;
        int columns;
        if (shape.Length == 2)
        {
            rowDimension = 0;
            columnDimension = 1;
            rows = shape[0];
            columns = shape[1];
        }
        else
        {
            int[] effectiveDimensions = shape
                .Select((value, index) => (value, index))
                .Where(item => item.value != 1)
                .Select(item => item.index)
                .ToArray();
            if (effectiveDimensions.Length != 2)
                throw new InvalidDataException("NPY 文件必须是可压缩为二维的数值矩阵。");
            rowDimension = effectiveDimensions[0];
            columnDimension = effectiveDimensions[1];
            rows = shape[rowDimension];
            columns = shape[columnDimension];
        }

        long elementCount = 1;
        foreach (int dimension in shape)
            elementCount = checked(elementCount * dimension);
        NpyType type = ParseNpyType(descriptor);
        if (elementCount > int.MaxValue)
            throw new InvalidDataException("NPY 矩阵过大，无法在内存中读取。");
        var values = new double[(int)elementCount];
        byte[] buffer = new byte[type.ItemSize];
        for (int index = 0; index < values.Length; index++)
        {
            int read = stream.Read(buffer, 0, buffer.Length);
            if (read != buffer.Length) throw new InvalidDataException("NPY 数据区不完整。");
            values[index] = DecodeNpyValue(buffer, type);
        }

        var matrix = new double[rows, columns];
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                var indices = new int[shape.Length];
                indices[rowDimension] = row;
                indices[columnDimension] = column;
                int flat = FlattenNpyIndices(indices, shape, fortran);
                matrix[row, column] = values[flat];
            }
        }
        return matrix;
    }

    private static string ParseNpyDescriptor(string header)
    {
        Match match = Regex.Match(header, @"['""]descr['""]\s*:\s*['""](?<value>[^'""]+)['""]",
            RegexOptions.CultureInvariant);
        if (!match.Success) throw new InvalidDataException("NPY 头部缺少 descr。");
        return match.Groups["value"].Value;
    }

    private static bool ParseNpyFortranOrder(string header)
    {
        Match match = Regex.Match(header, @"['""]fortran_order['""]\s*:\s*(?<value>True|False)",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        if (!match.Success) throw new InvalidDataException("NPY 头部缺少 fortran_order。");
        return string.Equals(match.Groups["value"].Value, "True", StringComparison.OrdinalIgnoreCase);
    }

    private static int[] ParseNpyShape(string header)
    {
        Match match = Regex.Match(header, @"['""]shape['""]\s*:\s*\((?<value>[^)]*)\)",
            RegexOptions.CultureInvariant);
        if (!match.Success) throw new InvalidDataException("NPY 头部缺少 shape。");
        string[] parts = match.Groups["value"].Value.Split(',', StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries);
        var shape = new List<int>();
        foreach (string part in parts)
        {
            if (!int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out int dimension) ||
                dimension < 0) throw new InvalidDataException("NPY shape 无效。");
            shape.Add(dimension);
        }
        return shape.ToArray();
    }

    private readonly record struct NpyType(bool BigEndian, char Kind, int ItemSize);

    private static NpyType ParseNpyType(string descriptor)
    {
        if (descriptor.Length < 2) throw new InvalidDataException($"不支持的 NPY 数据类型：{descriptor}。");
        char endian = descriptor[0];
        char kind = descriptor[1];
        if (!int.TryParse(descriptor[2..], NumberStyles.Integer, CultureInfo.InvariantCulture,
                out int itemSize) || itemSize <= 0)
            throw new InvalidDataException($"不支持的 NPY 数据类型：{descriptor}。");
        if (endian is not ('<' or '>' or '|' or '='))
            throw new InvalidDataException($"不支持的 NPY 字节序：{descriptor}。");
        if (kind is not ('f' or 'i' or 'u' or 'b') ||
            (kind == 'b' && itemSize != 1) ||
            (kind == 'f' && itemSize is not (4 or 8)) ||
            (kind is 'i' or 'u' && itemSize is not (1 or 2 or 4 or 8)))
            throw new InvalidDataException($"不支持的 NPY 数据类型：{descriptor}。");
        bool bigEndian = endian == '>' || (endian == '=' && !BitConverter.IsLittleEndian);
        return new NpyType(bigEndian, kind, itemSize);
    }

    private static double DecodeNpyValue(byte[] bytes, NpyType type)
    {
        if (type.Kind == 'b') return bytes[0] == 0 ? 0d : 1d;
        if (type.Kind == 'f')
        {
            if (type.ItemSize == 4)
            {
                int bits = type.BigEndian
                    ? BinaryPrimitives.ReadInt32BigEndian(bytes)
                    : BinaryPrimitives.ReadInt32LittleEndian(bytes);
                return BitConverter.Int32BitsToSingle(bits);
            }
            long bits64 = type.BigEndian
                ? BinaryPrimitives.ReadInt64BigEndian(bytes)
                : BinaryPrimitives.ReadInt64LittleEndian(bytes);
            return BitConverter.Int64BitsToDouble(bits64);
        }

        ulong unsigned = type.ItemSize switch
        {
            1 => bytes[0],
            2 => type.BigEndian ? BinaryPrimitives.ReadUInt16BigEndian(bytes) : BinaryPrimitives.ReadUInt16LittleEndian(bytes),
            4 => type.BigEndian ? BinaryPrimitives.ReadUInt32BigEndian(bytes) : BinaryPrimitives.ReadUInt32LittleEndian(bytes),
            8 => type.BigEndian ? BinaryPrimitives.ReadUInt64BigEndian(bytes) : BinaryPrimitives.ReadUInt64LittleEndian(bytes),
            _ => throw new InvalidDataException("NPY 整数宽度无效。")
        };
        if (type.Kind == 'u') return unsigned;
        long signed = type.ItemSize switch
        {
            1 => (sbyte)bytes[0],
            2 => type.BigEndian ? BinaryPrimitives.ReadInt16BigEndian(bytes) : BinaryPrimitives.ReadInt16LittleEndian(bytes),
            4 => type.BigEndian ? BinaryPrimitives.ReadInt32BigEndian(bytes) : BinaryPrimitives.ReadInt32LittleEndian(bytes),
            8 => type.BigEndian ? BinaryPrimitives.ReadInt64BigEndian(bytes) : BinaryPrimitives.ReadInt64LittleEndian(bytes),
            _ => throw new InvalidDataException("NPY 整数宽度无效。")
        };
        return signed;
    }

    private static int FlattenNpyIndices(int[] indices, int[] shape, bool fortran)
    {
        int flat = 0;
        if (fortran)
        {
            int stride = 1;
            for (int dimension = 0; dimension < shape.Length; dimension++)
            {
                flat = checked(flat + indices[dimension] * stride);
                stride = checked(stride * shape[dimension]);
            }
        }
        else
        {
            int stride = 1;
            for (int dimension = shape.Length - 1; dimension >= 0; dimension--)
            {
                flat = checked(flat + indices[dimension] * stride);
                stride = checked(stride * shape[dimension]);
            }
        }
        return flat;
    }

    private static double[,] FillWithNan(int rows, int columns)
    {
        var result = new double[rows, columns];
        for (int row = 0; row < rows; row++)
            for (int column = 0; column < columns; column++) result[row, column] = double.NaN;
        return result;
    }

    private static double[,] RemoveAllNanRowsAndColumns(double[,] matrix, string displayName)
    {
        int rows = matrix.GetLength(0), columns = matrix.GetLength(1);
        if (rows == 0 || columns == 0 || matrix.Length == 0)
            throw new InvalidDataException($"{displayName} 不是非空二维数值矩阵。");

        int[] keptRows = Enumerable.Range(0, rows)
            .Where(row => !RowAllNaN(matrix, row, columns))
            .ToArray();
        if (keptRows.Length == 0)
            throw new InvalidDataException($"{displayName} 不包含可计算的数值。");

        int[] keptColumns = Enumerable.Range(0, columns)
            .Where(column => !ColumnAllNaN(matrix, column, keptRows))
            .ToArray();
        if (keptColumns.Length == 0)
            throw new InvalidDataException($"{displayName} 不包含可计算的数值。");

        var result = new double[keptRows.Length, keptColumns.Length];
        for (int row = 0; row < keptRows.Length; row++)
            for (int column = 0; column < keptColumns.Length; column++)
                result[row, column] = matrix[keptRows[row], keptColumns[column]];
        return result;
    }

    private static bool RowAllNaN(double[,] matrix, int row, int columns)
    {
        for (int column = 0; column < columns; column++)
            if (!double.IsNaN(matrix[row, column])) return false;
        return true;
    }

    private static bool ColumnAllNaN(double[,] matrix, int column, IReadOnlyList<int> rows)
    {
        foreach (int row in rows)
            if (!double.IsNaN(matrix[row, column])) return false;
        return true;
    }
}

/// <summary>与参考分析器 statistics() 对应的结果结构。</summary>
public readonly record struct CrosstalkStatistics(
    double Minimum,
    double Maximum,
    double Mean,
    int Count);

/// <summary>纯 C# 的通用串扰掩膜和统计辅助方法。</summary>
public static class CrosstalkAnalysisUtilities
{
    /// <summary>
    /// 对有限且未排除的数据计算最小、最大、均值和数量；没有有效点时返回 NaN/0。
    /// </summary>
    public static CrosstalkStatistics Statistics(double[,] values, bool[,]? excluded = null)
    {
        ArgumentNullException.ThrowIfNull(values);
        int rows = values.GetLength(0), columns = values.GetLength(1);
        if (excluded is not null &&
            (excluded.GetLength(0) != rows || excluded.GetLength(1) != columns))
            throw new ArgumentException("掩膜尺寸与数据尺寸不一致。", nameof(excluded));

        double minimum = double.PositiveInfinity;
        double maximum = double.NegativeInfinity;
        double sum = 0d;
        int count = 0;
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                if (excluded is not null && excluded[row, column]) continue;
                double value = values[row, column];
                if (!double.IsFinite(value)) continue;
                minimum = Math.Min(minimum, value);
                maximum = Math.Max(maximum, value);
                sum += value;
                count++;
            }
        }
        return count == 0
            ? new CrosstalkStatistics(double.NaN, double.NaN, double.NaN, 0)
            : new CrosstalkStatistics(minimum, maximum, sum / count, count);
    }

    /// <summary>创建一个 1-based、含首尾坐标的矩形掩膜。</summary>
    public static bool[,] RectangleMask(int rows, int columns,
        int x1, int y1, int x2, int y2)
    {
        if (rows <= 0 || columns <= 0)
            throw new ArgumentOutOfRangeException(nameof(rows), "矩阵尺寸必须为正数。");
        var mask = new bool[rows, columns];
        AddRectangle(mask, x1, y1, x2, y2);
        return mask;
    }

    /// <summary>将一个 1-based、含首尾坐标的矩形加入现有掩膜。</summary>
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
            for (int column = left - 1; column < right; column++) mask[row, column] = true;
    }

    /// <summary>返回 values 中大于阈值（比例）的异常点掩膜。</summary>
    public static bool[,] MakeAbnormalMask(double[,] values, double threshold = .03d)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (!double.IsFinite(threshold))
            throw new ArgumentOutOfRangeException(nameof(threshold), "异常阈值必须是有限数值。");
        var result = new bool[values.GetLength(0), values.GetLength(1)];
        for (int row = 0; row < values.GetLength(0); row++)
            for (int column = 0; column < values.GetLength(1); column++)
                result[row, column] = values[row, column] > threshold;
        return result;
    }
}
