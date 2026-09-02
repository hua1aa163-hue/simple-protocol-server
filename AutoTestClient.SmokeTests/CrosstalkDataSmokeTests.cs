using System.Globalization;
using System.IO.Compression;
using System.Text;
using AutoTestClient.DataProcessing;

/// <summary>
/// 串扰归档回归测试。使用最小的 Brightness 工作簿模拟 MRTEST 导出，
/// 因而不依赖现场 ExportFile，也不需要启动窗体或真实设备。
/// </summary>
internal static class CrosstalkDataSmokeTests
{
    public static async Task RunAsync(Action<bool, string> check)
    {
        string root = Path.Combine(
            Path.GetTempPath(), "AutoTestClientCrosstalkSmoke", Guid.NewGuid().ToString("N"));
        string exportRoot = Path.Combine(root, "ExportFile");
        string outputRoot = Path.Combine(root, "Results");
        Directory.CreateDirectory(exportRoot);

        try
        {
            DateTime firstStart = DateTime.UtcNow;
            IReadOnlyDictionary<string, ExportFolderFingerprint> firstSnapshot =
                CrosstalkDataProcessor.CaptureSnapshot(exportRoot);
            CreateBatch(exportRoot, "batch-1", 10.01, 11.0, 10.0);
            IReadOnlyList<CrosstalkTestRecord> firstTests = CreateRecords("batch-1");
            CrosstalkProcessingResult first = await CrosstalkDataProcessor.ProcessCompletedTestAsync(
                exportRoot,
                outputRoot,
                firstStart,
                firstSnapshot,
                firstTests,
                progress: null,
                CancellationToken.None);

            // 第二批在同一个输出根目录中运行；即使两批发生在同一秒，
            // 处理器也必须分配不同的目录，并保留第一批热图。
            DateTime secondStart = DateTime.UtcNow;
            IReadOnlyDictionary<string, ExportFolderFingerprint> secondSnapshot =
                CrosstalkDataProcessor.CaptureSnapshot(exportRoot);
            CreateBatch(exportRoot, "batch-2", 10.02, 11.0, 10.0);
            IReadOnlyList<CrosstalkTestRecord> secondTests = CreateRecords("batch-2");
            CrosstalkProcessingResult second = await CrosstalkDataProcessor.ProcessCompletedTestAsync(
                exportRoot,
                outputRoot,
                secondStart,
                secondSnapshot,
                secondTests,
                progress: null,
                CancellationToken.None);

            check(!string.Equals(first.OutputDirectory, second.OutputDirectory,
                    StringComparison.OrdinalIgnoreCase),
                "串扰连续两批输出目录不覆盖");
            check(File.Exists(first.HeatmapPath) && new FileInfo(first.HeatmapPath).Length > 0,
                "串扰第一批热力图在第二批完成后仍保留");
            check(File.Exists(second.HeatmapPath) && new FileInfo(second.HeatmapPath).Length > 0,
                "串扰第二批热力图已生成");
            check(File.Exists(first.CrosstalkWorkbookPath) && File.Exists(second.CrosstalkWorkbookPath),
                "串扰两批 Excel 结果路径均独立存在");
            check(Directory.Exists(Path.Combine(first.OutputDirectory, "原始数据")) &&
                  Directory.Exists(Path.Combine(second.OutputDirectory, "原始数据")),
                "串扰两批原始数据归档互不干涉");
        }
        finally
        {
            // 测试目录完全由本方法创建，清理失败不应掩盖回归断言结果。
            try
            {
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }
            catch
            {
                // 仅留下临时目录供诊断；不删除用户目录或现场导出数据。
            }
        }
    }

    private static IReadOnlyList<CrosstalkTestRecord> CreateRecords(string batchName)
    {
        DateTime now = DateTime.UtcNow;
        return Enumerable.Range(1, 3)
            .Select(index => new CrosstalkTestRecord(
                index,
                Path.Combine(batchName, $"{index}.png"),
                now.AddMilliseconds(index)))
            .ToArray();
    }

    private static void CreateBatch(
        string exportRoot,
        string batchName,
        double firstValue,
        double secondValue,
        double backgroundValue)
    {
        double[] values = [firstValue, secondValue, backgroundValue];
        for (int index = 0; index < values.Length; index++)
        {
            string directory = Path.Combine(exportRoot, $"{batchName}-{index + 1:D2}");
            Directory.CreateDirectory(directory);
            WriteBrightnessWorkbook(
                Path.Combine(directory, $"{batchName}-{index + 1:D2}.xlsx"), values[index]);
        }
    }

    private static void WriteBrightnessWorkbook(string path, double value)
    {
        const string spreadsheetNamespace =
            "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        const string officeRelationshipNamespace =
            "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        const string packageRelationshipNamespace =
            "http://schemas.openxmlformats.org/package/2006/relationships";
        const string contentTypesNamespace =
            "http://schemas.openxmlformats.org/package/2006/content-types";

        using ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Create);
        WriteEntry(archive, "[Content_Types].xml", $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <Types xmlns="{contentTypesNamespace}">
              <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" />
              <Default Extension="xml" ContentType="application/xml" />
              <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml" />
              <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml" />
            </Types>
            """);
        WriteEntry(archive, "_rels/.rels", $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <Relationships xmlns="{packageRelationshipNamespace}">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml" />
            </Relationships>
            """);
        WriteEntry(archive, "xl/workbook.xml", $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <workbook xmlns="{spreadsheetNamespace}" xmlns:r="{officeRelationshipNamespace}">
              <sheets><sheet name="Brightness" sheetId="1" r:id="rId1" /></sheets>
            </workbook>
            """);
        WriteEntry(archive, "xl/_rels/workbook.xml.rels", $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <Relationships xmlns="{packageRelationshipNamespace}">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml" />
            </Relationships>
            """);

        var rows = new StringBuilder();
        rows.Append($"<row r=\"1\"><c r=\"C1\" t=\"inlineStr\"><is><t>Brightness</t></is></c></row>");
        string number = value.ToString("R", CultureInfo.InvariantCulture);
        for (int row = 2; row <= CrosstalkDataProcessor.ExpectedBrightnessRows; row++)
        {
            rows.Append($"<row r=\"{row}\"><c r=\"C{row}\"><v>{number}</v></c></row>");
        }
        WriteEntry(archive, "xl/worksheets/sheet1.xml", $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <worksheet xmlns="{spreadsheetNamespace}"><sheetData>{rows}</sheetData></worksheet>
            """);
    }

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.Fastest);
        using StreamWriter writer = new(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content.Trim());
    }
}
