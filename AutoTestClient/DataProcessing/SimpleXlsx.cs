// 这里实现本项目实际需要的最小 XLSX 读写功能：读取 Brightness 工作表 C 列，
// 以及写出原始矩阵、串扰矩阵和统计值。XLSX 本质上是装有 XML 文件的 ZIP 包。
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace AutoTestClient.DataProcessing;

/// <summary>一个待写入的 Excel 工作表；二维数组的第一维是行，第二维是列。</summary>
internal sealed record XlsxSheetData(string Name, object?[,] Values, bool FirstRowIsHeader = false);

/// <summary>只依赖 .NET 自带 ZIP/XML 功能的轻量 XLSX 工具。</summary>
internal static class SimpleXlsx
{
    private const string SpreadsheetNamespace =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string OfficeRelationshipNamespace =
        "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const string PackageRelationshipNamespace =
        "http://schemas.openxmlformats.org/package/2006/relationships";
    private const string ContentTypesNamespace =
        "http://schemas.openxmlformats.org/package/2006/content-types";

    /// <summary>
    /// 按 Excel 行号读取指定工作表的一列。文字、空白或无法换算为数字的单元格以 NaN 表示，
    /// 因而第一行标题不会改变后续测试点的行号。
    /// </summary>
    internal static double[] ReadNumericColumn(string path, string sheetName, int columnNumber)
    {
        if (columnNumber <= 0) throw new ArgumentOutOfRangeException(nameof(columnNumber));

        using ZipArchive archive = ZipFile.OpenRead(path);
        XDocument workbook = LoadXmlEntry(archive, "xl/workbook.xml");
        XNamespace spreadsheet = SpreadsheetNamespace;
        XNamespace officeRelationship = OfficeRelationshipNamespace;

        XElement sheet = workbook
            .Descendants(spreadsheet + "sheet")
            .FirstOrDefault(item => string.Equals(
                (string?)item.Attribute("name"), sheetName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException($"Excel 中没有找到工作表“{sheetName}”。");

        string relationshipId = (string?)sheet.Attribute(officeRelationship + "id")
            ?? throw new InvalidDataException($"工作表“{sheetName}”缺少关系编号。");
        XDocument relationships = LoadXmlEntry(archive, "xl/_rels/workbook.xml.rels");
        XNamespace packageRelationship = PackageRelationshipNamespace;
        string target = relationships
            .Descendants(packageRelationship + "Relationship")
            .Where(item => (string?)item.Attribute("Id") == relationshipId)
            .Select(item => (string?)item.Attribute("Target"))
            .FirstOrDefault()
            ?? throw new InvalidDataException($"无法定位工作表“{sheetName}”的数据文件。");

        string worksheetPath = ResolveWorkbookTarget(target);
        XDocument worksheet = LoadXmlEntry(archive, worksheetPath);
        IReadOnlyList<string> sharedStrings = ReadSharedStrings(archive);

        var valuesByRow = new Dictionary<int, double>();
        int minimumRow = int.MaxValue;
        int maximumRow = 0;
        foreach (XElement row in worksheet.Descendants(spreadsheet + "row"))
        {
            int fallbackRow = ParsePositiveInteger((string?)row.Attribute("r"));
            int fallbackColumn = 0;
            foreach (XElement cell in row.Elements(spreadsheet + "c"))
            {
                string reference = ((string?)cell.Attribute("r") ?? string.Empty).Replace("$", "");
                int currentColumn = GetColumnNumber(reference);
                if (currentColumn == 0) currentColumn = ++fallbackColumn;
                else fallbackColumn = currentColumn;
                if (currentColumn != columnNumber) continue;

                int rowNumber = GetRowNumber(reference);
                if (rowNumber == 0) rowNumber = fallbackRow;
                if (rowNumber <= 0) continue;

                minimumRow = Math.Min(minimumRow, rowNumber);
                maximumRow = Math.Max(maximumRow, rowNumber);
                valuesByRow[rowNumber] = ReadCellNumber(cell, sharedStrings, spreadsheet);
            }
        }

        if (maximumRow == 0)
        {
            throw new InvalidDataException(
                $"工作表“{sheetName}”的第 {columnNumber} 列没有任何单元格。");
        }

        // MATLAB readmatrix(...,'Range','C:C') 会从该列第一个实际单元格开始，
        // 例如设备文件的 C3:C614 会得到 612 行，而不是在前面补 C1、C2 两个空行。
        int returnedRowCount = maximumRow - minimumRow + 1;
        double[] values = Enumerable.Repeat(double.NaN, returnedRowCount).ToArray();
        foreach ((int rowNumber, double value) in valuesByRow)
        {
            values[rowNumber - minimumRow] = value;
        }
        return values;
    }

    /// <summary>创建一个能被 Excel 直接打开的工作簿。</summary>
    internal static void WriteWorkbook(string path, IReadOnlyList<XlsxSheetData> sheets)
    {
        if (sheets.Count == 0) throw new ArgumentException("至少需要一个工作表。", nameof(sheets));

        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        using ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Create);

        WriteContentTypes(archive, sheets.Count);
        WriteRootRelationships(archive);
        WriteWorkbookXml(archive, sheets);
        WriteWorkbookRelationships(archive, sheets.Count);
        WriteStyles(archive);
        for (int index = 0; index < sheets.Count; index++)
        {
            WriteWorksheet(archive, $"xl/worksheets/sheet{index + 1}.xml", sheets[index]);
        }
    }

    /// <summary>从 XML 单元格取得数字；共享字符串和行内字符串也会尝试转成数字。</summary>
    private static double ReadCellNumber(
        XElement cell,
        IReadOnlyList<string> sharedStrings,
        XNamespace spreadsheet)
    {
        string type = (string?)cell.Attribute("t") ?? string.Empty;
        string text;
        if (type == "s")
        {
            string indexText = cell.Element(spreadsheet + "v")?.Value ?? string.Empty;
            text = int.TryParse(indexText, NumberStyles.Integer, CultureInfo.InvariantCulture,
                       out int index) && index >= 0 && index < sharedStrings.Count
                ? sharedStrings[index]
                : string.Empty;
        }
        else if (type == "inlineStr")
        {
            text = string.Concat(cell.Descendants(spreadsheet + "t").Select(item => item.Value));
        }
        else
        {
            // 公式单元格的 v 是 Excel 上次计算后缓存的结果，读取它即可。
            text = cell.Element(spreadsheet + "v")?.Value ?? string.Empty;
        }

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture,
            out double number)
            ? number
            : double.NaN;
    }

    /// <summary>共享字符串表不存在是合法情况，数字工作簿通常就没有此文件。</summary>
    private static IReadOnlyList<string> ReadSharedStrings(ZipArchive archive)
    {
        ZipArchiveEntry? entry = FindEntry(archive, "xl/sharedStrings.xml");
        if (entry is null) return [];

        using Stream stream = entry.Open();
        XDocument document = XDocument.Load(stream);
        XNamespace spreadsheet = SpreadsheetNamespace;
        return document.Descendants(spreadsheet + "si")
            .Select(item => string.Concat(item.Descendants(spreadsheet + "t")
                .Select(text => text.Value)))
            .ToArray();
    }

    /// <summary>把工作簿关系中的相对路径换算成 ZIP 内的标准路径。</summary>
    private static string ResolveWorkbookTarget(string target)
    {
        string normalized = target.Replace('\\', '/');
        if (normalized.StartsWith('/')) return normalized.TrimStart('/');

        // Excel 通常写成 worksheets/sheet1.xml；少数程序会写成 ../xl/worksheets/...。
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

    private static XDocument LoadXmlEntry(ZipArchive archive, string path)
    {
        ZipArchiveEntry entry = FindEntry(archive, path)
            ?? throw new InvalidDataException($"Excel 文件缺少必要内容：{path}");
        using Stream stream = entry.Open();
        return XDocument.Load(stream);
    }

    private static ZipArchiveEntry? FindEntry(ZipArchive archive, string path) =>
        archive.Entries.FirstOrDefault(entry => string.Equals(
            entry.FullName, path, StringComparison.OrdinalIgnoreCase));

    private static int ParsePositiveInteger(string? text) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) &&
        value > 0
            ? value
            : 0;

    /// <summary>把 C12 中的 C 换算成 3。</summary>
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

    /// <summary>把 C12 中的 12 取出。</summary>
    private static int GetRowNumber(string reference)
    {
        string digits = new(reference.Where(char.IsDigit).ToArray());
        return ParsePositiveInteger(digits);
    }

    private static void WriteContentTypes(ZipArchive archive, int sheetCount) =>
        WriteXmlEntry(archive, "[Content_Types].xml", writer =>
        {
            writer.WriteStartElement("Types", ContentTypesNamespace);
            WriteType(writer, "Default", "Extension", "rels", "ContentType",
                "application/vnd.openxmlformats-package.relationships+xml");
            WriteType(writer, "Default", "Extension", "xml", "ContentType", "application/xml");
            WriteType(writer, "Override", "PartName", "/xl/workbook.xml", "ContentType",
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml");
            WriteType(writer, "Override", "PartName", "/xl/styles.xml", "ContentType",
                "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml");
            for (int index = 1; index <= sheetCount; index++)
            {
                WriteType(writer, "Override", "PartName", $"/xl/worksheets/sheet{index}.xml",
                    "ContentType",
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml");
            }
            writer.WriteEndElement();
        });

    private static void WriteType(
        XmlWriter writer,
        string element,
        string firstAttribute,
        string firstValue,
        string secondAttribute,
        string secondValue)
    {
        writer.WriteStartElement(element, ContentTypesNamespace);
        writer.WriteAttributeString(firstAttribute, firstValue);
        writer.WriteAttributeString(secondAttribute, secondValue);
        writer.WriteEndElement();
    }

    private static void WriteRootRelationships(ZipArchive archive) =>
        WriteXmlEntry(archive, "_rels/.rels", writer =>
        {
            writer.WriteStartElement("Relationships", PackageRelationshipNamespace);
            writer.WriteStartElement("Relationship", PackageRelationshipNamespace);
            writer.WriteAttributeString("Id", "rId1");
            writer.WriteAttributeString("Type",
                "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument");
            writer.WriteAttributeString("Target", "xl/workbook.xml");
            writer.WriteEndElement();
            writer.WriteEndElement();
        });

    private static void WriteWorkbookXml(ZipArchive archive, IReadOnlyList<XlsxSheetData> sheets) =>
        WriteXmlEntry(archive, "xl/workbook.xml", writer =>
        {
            writer.WriteStartElement("workbook", SpreadsheetNamespace);
            writer.WriteAttributeString("xmlns", "r", null, OfficeRelationshipNamespace);
            writer.WriteStartElement("sheets", SpreadsheetNamespace);
            for (int index = 0; index < sheets.Count; index++)
            {
                writer.WriteStartElement("sheet", SpreadsheetNamespace);
                writer.WriteAttributeString("name", MakeValidSheetName(sheets[index].Name, index));
                writer.WriteAttributeString("sheetId", (index + 1).ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("r", "id", OfficeRelationshipNamespace, $"rId{index + 1}");
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
            writer.WriteEndElement();
        });

    private static void WriteWorkbookRelationships(ZipArchive archive, int sheetCount) =>
        WriteXmlEntry(archive, "xl/_rels/workbook.xml.rels", writer =>
        {
            writer.WriteStartElement("Relationships", PackageRelationshipNamespace);
            for (int index = 1; index <= sheetCount; index++)
            {
                writer.WriteStartElement("Relationship", PackageRelationshipNamespace);
                writer.WriteAttributeString("Id", $"rId{index}");
                writer.WriteAttributeString("Type",
                    "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet");
                writer.WriteAttributeString("Target", $"worksheets/sheet{index}.xml");
                writer.WriteEndElement();
            }
            writer.WriteStartElement("Relationship", PackageRelationshipNamespace);
            writer.WriteAttributeString("Id", $"rId{sheetCount + 1}");
            writer.WriteAttributeString("Type",
                "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles");
            writer.WriteAttributeString("Target", "styles.xml");
            writer.WriteEndElement();
            writer.WriteEndElement();
        });

    /// <summary>样式 1 是三位小数，样式 2 是加粗表头。</summary>
    private static void WriteStyles(ZipArchive archive) =>
        WriteXmlEntry(archive, "xl/styles.xml", writer =>
        {
            writer.WriteStartElement("styleSheet", SpreadsheetNamespace);
            writer.WriteStartElement("numFmts", SpreadsheetNamespace); writer.WriteAttributeString("count", "1");
            writer.WriteStartElement("numFmt", SpreadsheetNamespace); writer.WriteAttributeString("numFmtId", "164");
            writer.WriteAttributeString("formatCode", "0.000"); writer.WriteEndElement();
            writer.WriteEndElement();

            writer.WriteStartElement("fonts", SpreadsheetNamespace); writer.WriteAttributeString("count", "2");
            writer.WriteStartElement("font", SpreadsheetNamespace); writer.WriteStartElement("sz", SpreadsheetNamespace);
            writer.WriteAttributeString("val", "11"); writer.WriteEndElement();
            writer.WriteStartElement("name", SpreadsheetNamespace); writer.WriteAttributeString("val", "Calibri");
            writer.WriteEndElement(); writer.WriteEndElement();
            writer.WriteStartElement("font", SpreadsheetNamespace); writer.WriteStartElement("b", SpreadsheetNamespace); writer.WriteEndElement();
            writer.WriteStartElement("sz", SpreadsheetNamespace); writer.WriteAttributeString("val", "11");
            writer.WriteEndElement(); writer.WriteStartElement("name", SpreadsheetNamespace);
            writer.WriteAttributeString("val", "Calibri"); writer.WriteEndElement();
            writer.WriteEndElement(); writer.WriteEndElement();

            writer.WriteStartElement("fills", SpreadsheetNamespace); writer.WriteAttributeString("count", "2");
            WritePatternFill(writer, "none"); WritePatternFill(writer, "gray125");
            writer.WriteEndElement();
            writer.WriteStartElement("borders", SpreadsheetNamespace); writer.WriteAttributeString("count", "1");
            writer.WriteStartElement("border", SpreadsheetNamespace);
            foreach (string edge in new[] { "left", "right", "top", "bottom", "diagonal" })
            {
                writer.WriteStartElement(edge, SpreadsheetNamespace); writer.WriteEndElement();
            }
            writer.WriteEndElement(); writer.WriteEndElement();
            writer.WriteStartElement("cellStyleXfs", SpreadsheetNamespace); writer.WriteAttributeString("count", "1");
            WriteXf(writer, 0, 0); writer.WriteEndElement();
            writer.WriteStartElement("cellXfs", SpreadsheetNamespace); writer.WriteAttributeString("count", "3");
            WriteXf(writer, 0, 0); WriteXf(writer, 164, 0); WriteXf(writer, 0, 1);
            writer.WriteEndElement();
            writer.WriteStartElement("cellStyles", SpreadsheetNamespace); writer.WriteAttributeString("count", "1");
            writer.WriteStartElement("cellStyle", SpreadsheetNamespace); writer.WriteAttributeString("name", "Normal");
            writer.WriteAttributeString("xfId", "0"); writer.WriteAttributeString("builtinId", "0");
            writer.WriteEndElement(); writer.WriteEndElement();
            writer.WriteEndElement();
        });

    private static void WritePatternFill(XmlWriter writer, string patternType)
    {
        writer.WriteStartElement("fill", SpreadsheetNamespace); writer.WriteStartElement("patternFill", SpreadsheetNamespace);
        writer.WriteAttributeString("patternType", patternType);
        writer.WriteEndElement(); writer.WriteEndElement();
    }

    private static void WriteXf(XmlWriter writer, int numberFormatId, int fontId)
    {
        writer.WriteStartElement("xf", SpreadsheetNamespace);
        writer.WriteAttributeString("numFmtId", numberFormatId.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("fontId", fontId.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("fillId", "0"); writer.WriteAttributeString("borderId", "0");
        writer.WriteAttributeString("xfId", "0");
        if (numberFormatId != 0) writer.WriteAttributeString("applyNumberFormat", "1");
        if (fontId != 0) writer.WriteAttributeString("applyFont", "1");
        writer.WriteEndElement();
    }

    private static void WriteWorksheet(ZipArchive archive, string path, XlsxSheetData sheet) =>
        WriteXmlEntry(archive, path, writer =>
        {
            int rowCount = sheet.Values.GetLength(0);
            int columnCount = sheet.Values.GetLength(1);
            writer.WriteStartElement("worksheet", SpreadsheetNamespace);
            writer.WriteStartElement("dimension", SpreadsheetNamespace);
            writer.WriteAttributeString("ref", $"A1:{GetCellReference(Math.Max(1, rowCount), Math.Max(1, columnCount))}");
            writer.WriteEndElement();
            writer.WriteStartElement("sheetData", SpreadsheetNamespace);
            for (int row = 0; row < rowCount; row++)
            {
                writer.WriteStartElement("row", SpreadsheetNamespace);
                writer.WriteAttributeString("r", (row + 1).ToString(CultureInfo.InvariantCulture));
                for (int column = 0; column < columnCount; column++)
                {
                    object? value = sheet.Values[row, column];
                    if (value is null || value is double number && double.IsNaN(number)) continue;
                    WriteCell(writer, row + 1, column + 1, value,
                        sheet.FirstRowIsHeader && row == 0);
                }
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
            writer.WriteEndElement();
        });

    private static void WriteCell(
        XmlWriter writer,
        int row,
        int column,
        object value,
        bool header)
    {
        writer.WriteStartElement("c", SpreadsheetNamespace);
        writer.WriteAttributeString("r", GetCellReference(row, column));
        if (header) writer.WriteAttributeString("s", "2");

        if (value is string text)
        {
            writer.WriteAttributeString("t", "inlineStr");
            writer.WriteStartElement("is", SpreadsheetNamespace); writer.WriteStartElement("t", SpreadsheetNamespace);
            if (text.Length != text.Trim().Length)
            {
                writer.WriteAttributeString("xml", "space", null, "preserve");
            }
            writer.WriteString(text); writer.WriteEndElement(); writer.WriteEndElement();
        }
        else
        {
            if (!header) writer.WriteAttributeString("s", "1");
            writer.WriteStartElement("v", SpreadsheetNamespace);
            writer.WriteString(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
    }

    private static string GetCellReference(int row, int column)
    {
        var letters = new StringBuilder();
        int remaining = column;
        while (remaining > 0)
        {
            remaining--;
            letters.Insert(0, (char)('A' + remaining % 26));
            remaining /= 26;
        }
        return $"{letters}{row}";
    }

    private static string MakeValidSheetName(string name, int index)
    {
        char[] invalid = ['[', ']', ':', '*', '?', '/', '\\'];
        string cleaned = new(name.Select(character => invalid.Contains(character) ? '_' : character)
            .ToArray());
        if (string.IsNullOrWhiteSpace(cleaned)) cleaned = $"Sheet{index + 1}";
        return cleaned.Length <= 31 ? cleaned : cleaned[..31];
    }

    private static void WriteXmlEntry(
        ZipArchive archive,
        string path,
        Action<XmlWriter> writeBody)
    {
        ZipArchiveEntry entry = archive.CreateEntry(path, CompressionLevel.Fastest);
        using Stream stream = entry.Open();
        using XmlWriter writer = XmlWriter.Create(stream, new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = true,
            CloseOutput = false
        });
        writer.WriteStartDocument();
        writeBody(writer);
        writer.WriteEndDocument();
    }
}

