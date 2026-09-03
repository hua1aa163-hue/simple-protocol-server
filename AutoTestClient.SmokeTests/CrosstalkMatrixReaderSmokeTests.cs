using System.Globalization;
using System.IO.Compression;
using System.Text;
using AutoTestClient.DataProcessing;

/// <summary>通用纯 C# 矩阵读取器与辅助 API 的回归检查。</summary>
internal static class CrosstalkMatrixReaderSmokeTests
{
    public static void Run(Action<bool, string> check)
    {
        string root = Path.Combine(Path.GetTempPath(), "AutoTestClientMatrixReaderSmoke",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string csv = Path.Combine(root, "matrix.csv");
            File.WriteAllText(csv,
                "label,signal,reference,unused\r\n" +
                ",,,\r\n" +
                "1,2,4,x\r\n" +
                "2,NaN,8,x\r\n" +
                ",,,\r\n", new UTF8Encoding(false));
            double[,] csvMatrix = CrosstalkMatrixReader.ReadMatrix(csv);
            check(csvMatrix.GetLength(0) == 2 && csvMatrix.GetLength(1) == 3 &&
                  csvMatrix[0, 0] == 1d && double.IsNaN(csvMatrix[1, 1]),
                "CSV 读取并清理全 NaN 边缘行列");

            string tsv = Path.Combine(root, "matrix.tsv");
            File.WriteAllText(tsv, "a\tb\r\n3\t4\r\n5\t6\r\n", new UTF8Encoding(false));
            double[,] tsvMatrix = CrosstalkDataProcessor.ReadMatrix(tsv);
            check(tsvMatrix.GetLength(0) == 2 && tsvMatrix.GetLength(1) == 2 &&
                  tsvMatrix[1, 1] == 6d, "TSV 读取转发 API");

            string txt = Path.Combine(root, "matrix.txt");
            File.WriteAllText(txt, "header header\n7 8\n9 10\n", new UTF8Encoding(false));
            double[,] txtMatrix = CrosstalkMatrixReader.ReadMatrix(txt);
            check(txtMatrix.GetLength(0) == 2 && txtMatrix.GetLength(1) == 2 &&
                  txtMatrix[0, 0] == 7d && txtMatrix[1, 1] == 10d, "空白分隔 TXT 读取");

            string singleRow = Path.Combine(root, "single-row.csv");
            File.WriteAllText(singleRow, "1,2,3\r\n", new UTF8Encoding(false));
            double[,] singleRowMatrix = CrosstalkMatrixReader.ReadMatrix(singleRow);
            check(singleRowMatrix.GetLength(0) == 1 && singleRowMatrix.GetLength(1) == 3 &&
                  singleRowMatrix[0, 2] == 3d, "读取 1×N 二维矩阵");

            string singleColumn = Path.Combine(root, "single-column.txt");
            File.WriteAllText(singleColumn, "1\n2\n3\n", new UTF8Encoding(false));
            double[,] singleColumnMatrix = CrosstalkMatrixReader.ReadMatrix(singleColumn);
            check(singleColumnMatrix.GetLength(0) == 3 && singleColumnMatrix.GetLength(1) == 1 &&
                  singleColumnMatrix[2, 0] == 3d, "读取 N×1 二维矩阵");

            string internalBlank = Path.Combine(root, "internal-blank.csv");
            File.WriteAllText(internalBlank, "1,,3\r\n,,,\r\n4,,6\r\n", new UTF8Encoding(false));
            double[,] internalBlankMatrix = CrosstalkMatrixReader.ReadMatrix(internalBlank);
            check(internalBlankMatrix.GetLength(0) == 2 && internalBlankMatrix.GetLength(1) == 2 &&
                  internalBlankMatrix[1, 1] == 6d, "清理内部全 NaN 行列并保留有效顺序");

            string xlsx = Path.Combine(root, "matrix.xlsx");
            WriteSimpleWorkbook(xlsx);
            double[,] xlsxMatrix = CrosstalkMatrixReader.ReadMatrix(xlsx);
            check(xlsxMatrix.GetLength(0) == 2 && xlsxMatrix.GetLength(1) == 2 &&
                  xlsxMatrix[0, 0] == 11d && xlsxMatrix[1, 1] == 14d, "XLSX 首工作表读取");

            string npy = Path.Combine(root, "matrix.npy");
            WriteNpyFloat64(npy, new[,] { { 1d, 2d }, { 3d, 4d } });
            double[,] npyMatrix = CrosstalkMatrixReader.ReadMatrix(npy);
            check(npyMatrix.GetLength(0) == 2 && npyMatrix.GetLength(1) == 2 &&
                  npyMatrix[0, 1] == 2d && npyMatrix[1, 0] == 3d, "常见 NPY 读取");

            DateTime now = DateTime.UtcNow;
            File.SetLastWriteTimeUtc(csv, now.AddSeconds(-2));
            File.SetLastWriteTimeUtc(tsv, now.AddSeconds(-1));
            File.SetLastWriteTimeUtc(txt, now.AddSeconds(8));
            File.SetLastWriteTimeUtc(singleRow, now.AddSeconds(1));
            File.SetLastWriteTimeUtc(singleColumn, now.AddSeconds(2));
            File.SetLastWriteTimeUtc(internalBlank, now.AddSeconds(3));
            File.SetLastWriteTimeUtc(xlsx, now.AddSeconds(4));
            File.SetLastWriteTimeUtc(npy, now.AddSeconds(5));
            IReadOnlyList<string> listed = CrosstalkMatrixReader.ListDataFiles(root);
            check(listed.Count == 8 && Path.GetFileName(listed[0]) == "matrix.csv" &&
                  Path.GetFileName(listed[^1]) == "matrix.txt", "支持文件按修改时间列出");

            double[,] values = { { 1d, 2d }, { double.NaN, 4d } };
            bool[,] rectangle = CrosstalkAnalysisUtilities.RectangleMask(2, 2, 1, 1, 1, 2);
            CrosstalkStatistics statistics = CrosstalkAnalysisUtilities.Statistics(values, rectangle);
            check(rectangle[0, 0] && rectangle[1, 0] && statistics.Count == 2 &&
                  statistics.Minimum == 2d && statistics.Maximum == 4d && statistics.Mean == 3d,
                "矩形掩膜与有限点统计辅助 API");
            bool[,] abnormal = CrosstalkDataProcessor.MakeAbnormalMask(values, 1.5d);
            check(!abnormal[0, 0] && abnormal[0, 1] && abnormal[1, 1], "异常掩膜辅助 API");
        }
        catch (Exception ex)
        {
            check(false, "矩阵读取器冒烟测试异常：" + ex.Message);
        }
        finally
        {
            try { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
            catch { /* 仅清理本方法创建的临时目录。 */ }
        }
    }

    private static void WriteNpyFloat64(string path, double[,] values)
    {
        int rows = values.GetLength(0), columns = values.GetLength(1);
        string header = $"{{'descr': '<f8', 'fortran_order': False, 'shape': ({rows}, {columns}), }}";
        // NPY v1 headers are padded to a 16-byte alignment and end with a newline.
        int headerLength = Encoding.ASCII.GetByteCount(header) + 1;
        int padding = (16 - ((10 + headerLength) % 16)) % 16;
        header += new string(' ', padding) + "\n";
        byte[] headerBytes = Encoding.ASCII.GetBytes(header);
        using FileStream stream = File.Create(path);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: false);
        writer.Write(new byte[] { 0x93, (byte)'N', (byte)'U', (byte)'M', (byte)'P', (byte)'Y' });
        writer.Write((byte)1); writer.Write((byte)0);
        writer.Write((ushort)headerBytes.Length);
        writer.Write(headerBytes);
        for (int row = 0; row < rows; row++)
            for (int column = 0; column < columns; column++) writer.Write(values[row, column]);
    }

    private static void WriteSimpleWorkbook(string path)
    {
        const string spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        const string office = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        const string package = "http://schemas.openxmlformats.org/package/2006/relationships";
        const string content = "http://schemas.openxmlformats.org/package/2006/content-types";
        using ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Create);
        WriteEntry(archive, "[Content_Types].xml", $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <Types xmlns="{content}"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/></Types>
            """);
        WriteEntry(archive, "_rels/.rels", $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <Relationships xmlns="{package}"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>
            """);
        WriteEntry(archive, "xl/workbook.xml", $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <workbook xmlns="{spreadsheet}" xmlns:r="{office}"><sheets><sheet name="Sheet1" sheetId="1" r:id="rId1"/></sheets></workbook>
            """);
        WriteEntry(archive, "xl/_rels/workbook.xml.rels", $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <Relationships xmlns="{package}"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/></Relationships>
            """);
        WriteEntry(archive, "xl/worksheets/sheet1.xml", $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <worksheet xmlns="{spreadsheet}"><sheetData><row r="1"><c r="A1"><v>11</v></c><c r="B1"><v>12</v></c></row><row r="2"><c r="A2"><v>13</v></c><c r="B2"><v>14</v></c></row></sheetData></worksheet>
            """);
    }

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.Fastest);
        using StreamWriter writer = new(entry.Open(), new UTF8Encoding(false));
        writer.Write(content.Trim());
    }
}
