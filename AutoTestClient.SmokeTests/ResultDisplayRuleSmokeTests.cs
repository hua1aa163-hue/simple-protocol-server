using System.Globalization;
using System.IO.Compression;
using System.Text;
using AutoTestClient.DataProcessing;
using AutoTestClient.Models;

/// <summary>展示规则、深层工作簿定位和服务接入的纯 C# 回归测试。</summary>
internal static class ResultDisplayRuleSmokeTests
{
    /// <summary>供顶层 SmokeTests 入口使用的同步包装。</summary>
    public static void Run(Action<bool, string> check) =>
        RunAsync(check).GetAwaiter().GetResult();

    public static async Task RunAsync(Action<bool, string> check)
    {
        string root = Path.Combine(
            Path.GetTempPath(), "AutoTestClientResultRuleSmoke", Guid.NewGuid().ToString("N"));
        string nested = Path.Combine(root, "FOV测试_批次", "明细");
        string workbookPath = Path.Combine(nested, "结果.xlsx");
        string outerWorkbookPath = Path.Combine(root, "FOV测试_外层汇总.xlsx");
        Directory.CreateDirectory(nested);
        try
        {
            WriteFovWorkbook(workbookPath);
            WriteFovWorkbook(outerWorkbookPath);
            DateTime now = DateTime.UtcNow;
            File.SetLastWriteTimeUtc(workbookPath, now);
            // Even if the outer summary is newer, the nested detail workbook
            // is the required source for MRTEST result mapping.
            File.SetLastWriteTimeUtc(outerWorkbookPath, now.AddSeconds(2));

            var project = new TestProject
            {
                Name = "FOV测试",
                RecipeName = "FOV",
                Kind = TestProjectKind.Fov
            };
            ResultWorkbookSelection? selection = ResultDisplayRuleEngine.SelectWorkbook(
                root, project, now.AddSeconds(-1));
            check(selection is not null &&
                  string.Equals(selection.Path, Path.GetFullPath(workbookPath),
                      StringComparison.OrdinalIgnoreCase) &&
                  selection.RelativeDepth > 1,
                "结果引擎优先选择最内层 XLSX 而非外层汇总");

            // 自动处理路径必须拒绝只存在于测试开始之前的历史文件；
            // 手动/诊断调用仍可通过默认参数选择历史结果。
            ResultWorkbookSelection? staleOnly = ResultDisplayRuleEngine.SelectWorkbook(
                root, project, now.AddMinutes(1),
                allowHistoricalFallback: false,
                recentTolerance: TimeSpan.Zero);
            check(staleOnly is null, "自动结果定位不会回退到历史工作簿");

            IReadOnlyList<TestMetric> metrics = ResultDisplayRuleEngine.Extract(
                project, workbookPath, TestDataDisplayRule.CreateDefaultRules());
            check(metrics.Count == 2 && metrics[0].DisplayName == "水平FOV" &&
                  metrics[1].DisplayName == "垂直FOV" &&
                  metrics[0].Value == "29.5°" && metrics[1].Value == "16.8°",
                "默认 FOV 规则读取 B/C 单元格");

            IReadOnlyList<TestDataDisplayRule> defaults = TestDataDisplayRule.CreateDefaultRules();
            check(defaults.Count == 12 &&
                  defaults.Any(rule => rule.ProjectKind == TestProjectKind.Contrast &&
                                       rule.DataCellOrRange == "C4:C12" &&
                                       rule.Aggregation == TestDataAggregation.Average &&
                                       rule.NameCellOrLabel == "白图平均值" &&
                                       rule.OutputColumn == "白图平均值") &&
                  defaults.Any(rule => rule.ProjectKind == TestProjectKind.Contrast &&
                                       rule.DataCellOrRange == "C13:C21" &&
                                       rule.NameCellOrLabel == "黑图平均值" &&
                                       rule.OutputColumn == "黑图平均值") &&
                  defaults.Any(rule => rule.ProjectKind == TestProjectKind.Gamut &&
                                       rule.NameCellOrLabel == "H3" &&
                                       rule.DataCellOrRange == "H4") &&
                  defaults.Any(rule => rule.ProjectKind == TestProjectKind.Gamut &&
                                       rule.NameCellOrLabel == "B23" &&
                                       rule.DataCellOrRange == "C23") &&
                  defaults.Count(rule => rule.ProjectKind == TestProjectKind.Distortion) == 3,
                "对比度/色域/畸变默认展示规则地址完整");

            string oldJson = "[{\"Name\":\"旧规则\",\"CellOrRange\":\"C4\",\"NameCell\":\"B4\",\"ProjectType\":\"Fov\"}]";
            IReadOnlyList<TestDataDisplayRule> parsed = TestDataDisplayRuleParser.Parse(oldJson);
            check(parsed.Count == 1 && parsed[0].DataCellOrRange == "C4" &&
                  parsed[0].NameCellOrLabel == "B4" &&
                  parsed[0].ProjectKind == TestProjectKind.Fov,
                "旧字段/别名规则 JSON 可迁移");
            string roundTrip = TestDataDisplayRuleParser.Serialize(parsed, indented: false);
            check(roundTrip.Contains("DataCellOrRange", StringComparison.Ordinal) &&
                  TestDataDisplayRuleParser.Parse(roundTrip).Count == 1,
                "展示规则 JSON 往返");

            var service = new TestDataProcessingService
            {
                NonCrosstalkExportWaitTimeout = TimeSpan.Zero
            };
            TestDataProcessingResult result = await service.ProcessAsync(
                project,
                root,
                root,
                now.AddSeconds(-1),
                Array.Empty<TestMeasurementRecord>(),
                progress: null,
                CancellationToken.None,
                exportSnapshot: null,
                analysisOptions: null,
                displayRules: parsed);
            check(result.RawWorkbookPath is not null && result.Metrics.Count == 1 &&
                  result.Metrics[0].Value == "29.5°" &&
                  string.Equals(result.OutputDirectory, nested,
                      StringComparison.OrdinalIgnoreCase),
                "数据处理服务接入展示规则并返回工作簿路径");

            IReadOnlyList<TestMetric> badWorksheetMetrics = ResultDisplayRuleEngine.Extract(
                project, workbookPath,
                new[] { new TestDataDisplayRule
                {
                    ProjectKind = TestProjectKind.Fov,
                    Worksheet = "不存在的工作表",
                    Name = "错误规则",
                    DataCellOrRange = "C4"
                } });
            check(badWorksheetMetrics.Count == 1 &&
                  badWorksheetMetrics[0].Value.Length == 0 &&
                  badWorksheetMetrics[0].Status.Contains("读取失败", StringComparison.Ordinal),
                "规则工作表写错时明确拒绝而不静默读首表");

            TestDataProcessingResult noRules = await service.ProcessAsync(
                project, root, root, now.AddSeconds(-1),
                Array.Empty<TestMeasurementRecord>(), progress: null,
                CancellationToken.None, exportSnapshot: null,
                analysisOptions: null,
                displayRules: Array.Empty<TestDataDisplayRule>());
            check(noRules.Metrics.Count == 0,
                "显式清空展示规则后不会恢复内置规则");
        }
        finally
        {
            try { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
            catch { /* 临时目录清理失败不掩盖断言结果。 */ }
        }
    }

    private static void WriteFovWorkbook(string path)
    {
        const string main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        const string officeRel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        const string packageRel = "http://schemas.openxmlformats.org/package/2006/relationships";
        const string content = "http://schemas.openxmlformats.org/package/2006/content-types";
        using ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Create);
        Add(archive, "[Content_Types].xml", $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <Types xmlns="{content}">
              <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" />
              <Default Extension="xml" ContentType="application/xml" />
              <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml" />
              <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml" />
            </Types>
            """);
        Add(archive, "_rels/.rels", $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <Relationships xmlns="{packageRel}"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml" /></Relationships>
            """);
        Add(archive, "xl/workbook.xml", $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <workbook xmlns="{main}" xmlns:r="{officeRel}"><sheets><sheet name="Field of view" sheetId="1" r:id="rId1" /></sheets></workbook>
            """);
        Add(archive, "xl/_rels/workbook.xml.rels", $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <Relationships xmlns="{packageRel}"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml" /></Relationships>
            """);
        Add(archive, "xl/worksheets/sheet1.xml", $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <worksheet xmlns="{main}"><sheetData>
              <row r="4"><c r="B4" t="inlineStr"><is><t>水平FOV</t></is></c><c r="C4" t="inlineStr"><is><t>29.5°</t></is></c></row>
              <row r="5"><c r="B5" t="inlineStr"><is><t>垂直FOV</t></is></c><c r="C5" t="inlineStr"><is><t>16.8°</t></is></c></row>
            </sheetData></worksheet>
            """);
    }

    private static void Add(ZipArchive archive, string name, string content)
    {
        ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.Fastest);
        using StreamWriter writer = new(entry.Open(), new UTF8Encoding(false));
        writer.Write(content.Trim());
    }
}
