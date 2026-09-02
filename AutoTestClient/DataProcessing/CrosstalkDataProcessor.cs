// 本文件把用户提供的 MATLAB 程序逐段翻译成 C#：定位本批导出文件夹、合并 Brightness C 列、
// 计算串扰、剔除统计边框和异常点、写 Excel，并生成热力图。
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Text;

namespace AutoTestClient.DataProcessing;

/// <summary>一张投影图片完成设备测试的时间，用于和导出文件时间逐次对应。</summary>
public sealed record CrosstalkTestRecord(
    int Sequence,
    string ImagePath,
    DateTime CompletedUtc);

/// <summary>开始测试前某个一级导出文件夹的状态，用来排除历史数据。</summary>
public sealed record ExportFolderFingerprint(
    string? ExcelPath,
    long ExcelLength,
    long ExcelLastWriteUtcTicks);

/// <summary>MATLAB 等价计算后的矩阵与统计结果；内部数值仍是比例，例如 0.03 表示 3%。</summary>
public sealed record CrosstalkCalculationResult(
    double[,] ValuesForStatistics,
    double[,] ValuesForHeatmap,
    double Maximum,
    double Minimum,
    double Mean);

/// <summary>自动处理完成后交给界面显示的结果。</summary>
public sealed record CrosstalkProcessingResult(
    string OutputDirectory,
    string RawWorkbookPath,
    string CrosstalkWorkbookPath,
    string HeatmapPath,
    int SourceFileCount,
    CrosstalkCalculationResult Calculation);

/// <summary>串扰测试数据定位、归档、计算和输出的统一入口。</summary>
public static class CrosstalkDataProcessor
{
    // MATLAB 中 AA 预分配 612 行，随后删第 1 行与最后 3 行，剩下 608=19×32 个采样点。
    public const int ExpectedBrightnessRows = 612;
    public const int HeatmapRows = 19;
    public const int HeatmapColumns = 32;
    // MATLAB exportgraphics 在当前参考环境中输出 3792×2408、300 DPI 的 PNG。
    // 固定为相同画布后，每个采样格有足够空间显示三位小数，不会再出现数字被截断。
    public const int HeatmapImageWidth = 3792;
    public const int HeatmapImageHeight = 2408;
    public const double AbnormalThreshold = 0.03;

    private static readonly TimeSpan ExportWaitTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ExportPollInterval = TimeSpan.FromMilliseconds(500);
    // A plan normally serializes iterations, but callers can process two
    // completed batches concurrently (for example when a UI queues results).
    // Reserve result directories under one process-wide lock so two batches
    // started in the same second can never select the same suffix.
    private static readonly object OutputDirectoryGate = new();

    /// <summary>
    /// 只记录所选根目录的一级子文件夹，因为 MATLAB 也是遍历 selectedFolder 的一级子文件夹，
    /// 并在每个子文件夹中按文件名取第一个 XLSX。
    /// </summary>
    public static IReadOnlyDictionary<string, ExportFolderFingerprint> CaptureSnapshot(
        string sourceRoot)
    {
        var snapshot = new Dictionary<string, ExportFolderFingerprint>(
            StringComparer.OrdinalIgnoreCase);
        foreach (string directory in Directory.EnumerateDirectories(sourceRoot))
        {
            string? excelPath = FindFirstExcelFile(directory);
            if (excelPath is null)
            {
                snapshot[directory] = new ExportFolderFingerprint(null, 0, 0);
                continue;
            }

            var file = new FileInfo(excelPath);
            snapshot[directory] = new ExportFolderFingerprint(
                excelPath, file.Length, file.LastWriteTimeUtc.Ticks);
        }
        return snapshot;
    }

    /// <summary>
    /// 等待设备把本批 Excel 写完，再按测试完成时间和测试次数匹配数据，并执行 MATLAB 等价处理。
    /// </summary>
    public static async Task<CrosstalkProcessingResult> ProcessCompletedTestAsync(
        string sourceRoot,
        string outputRoot,
        DateTime testStartedUtc,
        IReadOnlyDictionary<string, ExportFolderFingerprint> snapshot,
        IReadOnlyList<CrosstalkTestRecord> tests,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        if (tests.Count == 0) throw new ArgumentException("没有完成的测试记录。", nameof(tests));

        progress?.Report($"等待测试设备导出 {tests.Count} 份 Excel 数据...");
        IReadOnlyList<ExportFolderData> sourceFolders = await WaitForExportFoldersAsync(
            sourceRoot, testStartedUtc, snapshot, tests, cancellationToken).ConfigureAwait(false);
        progress?.Report($"已按测试时间和次数匹配到 {sourceFolders.Count} 个导出文件夹。");

        string outputDirectory = CreateUniqueOutputDirectory(outputRoot, testStartedUtc, tests.Count);
        string rawDirectory = Path.Combine(outputDirectory, "原始数据");
        Directory.CreateDirectory(rawDirectory);

        // 复制整个一级导出文件夹，而不只复制 Excel；设备保存的附图和其他原始资料也会一起归档。
        var archivedExcelFiles = new List<string>(sourceFolders.Count);
        for (int index = 0; index < sourceFolders.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ExportFolderData source = sourceFolders[index];
            string targetFolder = Path.Combine(rawDirectory,
                $"{index + 1:D3}_{Path.GetFileName(source.DirectoryPath)}");
            CopyDirectory(source.DirectoryPath, targetFolder, cancellationToken);
            archivedExcelFiles.Add(Path.Combine(targetFolder, Path.GetFileName(source.ExcelPath)));
        }

        WriteTestMapping(outputDirectory, tests, sourceFolders);
        progress?.Report("正在读取每个 Excel 的 Brightness 工作表 C 列...");
        double[,] mergedData = MergeBrightnessColumns(archivedExcelFiles);
        CrosstalkCalculationResult calculation = Calculate(mergedData);

        string folderName = Path.GetFileName(outputDirectory);
        string rawWorkbookPath = Path.Combine(
            outputDirectory, $"orig{sourceFolders.Count}_output{folderName}.xlsx");
        string crosstalkWorkbookPath = Path.Combine(
            outputDirectory, $"crosstalk {folderName}.xlsx");
        string heatmapPath = Path.Combine(
            outputDirectory, $"crosstalk {folderName}.png");

        SimpleXlsx.WriteWorkbook(rawWorkbookPath,
            [new XlsxSheetData("Sheet1", ToObjectMatrix(mergedData))]);
        WriteCrosstalkWorkbook(crosstalkWorkbookPath, calculation);
        WriteHeatmap(heatmapPath, folderName, calculation.ValuesForHeatmap);

        return new CrosstalkProcessingResult(
            outputDirectory,
            rawWorkbookPath,
            crosstalkWorkbookPath,
            heatmapPath,
            sourceFolders.Count,
            calculation);
    }

    /// <summary>把多个等长或不等长 C 列按 MATLAB 规则合成“测试点×测试图”矩阵。</summary>
    public static double[,] MergeBrightnessColumns(IReadOnlyList<string> excelFiles)
    {
        if (excelFiles.Count == 0) throw new InvalidDataException("没有可处理的 Excel 文件。");

        var columns = new List<double[]>(excelFiles.Count);
        foreach (string excelFile in excelFiles)
        {
            double[] column = SimpleXlsx.ReadNumericColumn(excelFile, "Brightness", 3);
            columns.Add(column);
        }

        int maximumRows = columns.Max(column => column.Length);
        if (maximumRows != ExpectedBrightnessRows)
        {
            throw new InvalidDataException(
                $"Brightness 工作表 C 列应读取到 {ExpectedBrightnessRows} 行（第 1 行标题、" +
                $"中间 608 个测试点、最后 3 行汇总），实际为 {maximumRows} 行。无法按 19×32 还原。");
        }

        var merged = new double[maximumRows, columns.Count];
        for (int row = 0; row < maximumRows; row++)
        {
            for (int column = 0; column < columns.Count; column++)
            {
                merged[row, column] = row < columns[column].Length
                    ? columns[column][row]
                    : double.NaN;
            }
        }
        return merged;
    }

    /// <summary>
    /// 严格翻译 MATLAB 计算段。最后一列是本底，前面的列平均分成前后两组并一一配对。
    /// 对每个采样点计算正向和反向两个比值，负数先改成 100，再从全部比值中取最小值。
    /// </summary>
    public static CrosstalkCalculationResult Calculate(double[,] mergedData)
    {
        int rowCount = mergedData.GetLength(0);
        int columnCount = mergedData.GetLength(1);
        if (rowCount != ExpectedBrightnessRows)
        {
            throw new InvalidDataException(
                $"串扰计算需要 {ExpectedBrightnessRows} 行数据，实际为 {rowCount} 行。");
        }
        if (columnCount < 3 || columnCount % 2 == 0)
        {
            throw new InvalidDataException(
                $"Excel 数量必须为大于等于 3 的奇数：前后两组数量相同，最后 1 份为本底；实际为 {columnCount} 份。");
        }

        int pairedColumnCount = (columnCount - 1) / 2;
        int backgroundColumn = columnCount - 1;
        var ratios = new double[ExpectedBrightnessRows];
        // ratios[0] 保持 MATLAB zeros(612,1) 的初始 0；真正数据从第 2 行开始计算。
        for (int row = 1; row < rowCount; row++)
        {
            var candidates = new double[pairedColumnCount * 2];
            for (int pair = 0; pair < pairedColumnCount; pair++)
            {
                double first = mergedData[row, pair];
                double second = mergedData[row, pair + pairedColumnCount];
                double background = mergedData[row, backgroundColumn];
                double q1 = DivideLikeMatlab(first - background, second - background);
                double q2 = DivideLikeMatlab(second - background, first - background);
                candidates[pair] = q1 < 0 ? 100 : q1;
                candidates[pair + pairedColumnCount] = q2 < 0 ? 100 : q2;
            }
            ratios[row] = MinimumIncludingNaN(candidates);
        }

        // MATLAB：AA(1,:)=[]，再 AA(end-2:end,:)=[]，保留原数组第 2 到第 609 行。
        double[] reshapedSource = ratios.Skip(1).Take(HeatmapRows * HeatmapColumns).ToArray();
        var restored = new double[HeatmapRows, HeatmapColumns];
        for (int row = 0; row < HeatmapRows; row++)
        {
            for (int column = 0; column < HeatmapColumns; column++)
            {
                // reshape(AA,32,19)' 的最终对应关系就是每 32 个连续值组成一行。
                restored[row, column] = reshapedSource[row * HeatmapColumns + column];
            }
        }

        var valuesForStatistics = (double[,])restored.Clone();
        var valuesForHeatmap = (double[,])restored.Clone();
        var validStatistics = new List<double>();
        for (int row = 0; row < HeatmapRows; row++)
        {
            for (int column = 0; column < HeatmapColumns; column++)
            {
                bool isBorder = row == 0 || row == HeatmapRows - 1 ||
                                column == 0 || column == HeatmapColumns - 1;
                bool isAbnormal = restored[row, column] > AbnormalThreshold;

                // 边框既不绘图也不统计；>3% 异常值保留绘图，但不进入统计。
                if (isBorder) valuesForHeatmap[row, column] = double.NaN;
                if (isBorder || isAbnormal)
                {
                    valuesForStatistics[row, column] = double.NaN;
                }
                else if (!double.IsNaN(restored[row, column]))
                {
                    validStatistics.Add(restored[row, column]);
                }
            }
        }

        if (validStatistics.Count == 0)
        {
            throw new InvalidDataException("剔除边框和大于 3% 的异常值后，没有可用于统计的数据。");
        }

        return new CrosstalkCalculationResult(
            valuesForStatistics,
            valuesForHeatmap,
            validStatistics.Max(),
            validStatistics.Min(),
            validStatistics.Average());
    }

    /// <summary>轮询一级文件夹，数量达到本次测试次数后再按每次完成时间选择最吻合的一批。</summary>
    private static async Task<IReadOnlyList<ExportFolderData>> WaitForExportFoldersAsync(
        string sourceRoot,
        DateTime testStartedUtc,
        IReadOnlyDictionary<string, ExportFolderFingerprint> snapshot,
        IReadOnlyList<CrosstalkTestRecord> tests,
        CancellationToken cancellationToken)
    {
        DateTime deadline = DateTime.UtcNow + ExportWaitTimeout;
        Exception? lastReadError = null;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                IReadOnlyList<ExportFolderData> candidates = FindChangedExportFolders(
                    sourceRoot, testStartedUtc, snapshot);
                if (candidates.Count >= tests.Count)
                {
                    IReadOnlyList<ExportFolderData> selected =
                        SelectBestTimedWindow(candidates, tests);
                    // 两次读取之间文件大小和修改时间不变，表示导出程序已经写完，可安全复制。
                    await Task.Delay(ExportPollInterval, cancellationToken).ConfigureAwait(false);
                    if (FilesAreStable(selected) && FilesContainCompleteBrightnessData(selected))
                    {
                        return selected;
                    }
                }
                lastReadError = null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                lastReadError = ex;
            }
            await Task.Delay(ExportPollInterval, cancellationToken).ConfigureAwait(false);
        }

        string detail = lastReadError is null ? string.Empty : $" 最后一次读取错误：{lastReadError.Message}";
        throw new TimeoutException(
            $"60 秒内没有找到与本次 {tests.Count} 次测试对应的完整 Excel 数据。{detail}");
    }

    /// <summary>找出测试开始后新增，或相对开始快照已经变化的一级导出文件夹。</summary>
    private static IReadOnlyList<ExportFolderData> FindChangedExportFolders(
        string sourceRoot,
        DateTime testStartedUtc,
        IReadOnlyDictionary<string, ExportFolderFingerprint> snapshot)
    {
        var candidates = new List<ExportFolderData>();
        foreach (string directory in Directory.EnumerateDirectories(sourceRoot))
        {
            string? excelPath = FindFirstExcelFile(directory);
            if (excelPath is null) continue;
            var file = new FileInfo(excelPath);
            var current = new ExportFolderFingerprint(
                excelPath, file.Length, file.LastWriteTimeUtc.Ticks);
            bool changed = !snapshot.TryGetValue(directory, out ExportFolderFingerprint? old) ||
                           old != current;
            bool belongsToTime = file.LastWriteTimeUtc >= testStartedUtc.AddSeconds(-2);
            if (changed && belongsToTime)
            {
                candidates.Add(new ExportFolderData(
                    directory, excelPath, file.Length, file.LastWriteTimeUtc));
            }
        }
        return candidates
            .OrderBy(item => item.ExcelLastWriteUtc)
            .ThenBy(item => item.DirectoryPath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// 正常情况候选数就等于测试数；若同一时间另有设备写文件，则在按时间排序的连续窗口中，
    /// 选择与各次“设备返回完成”时间总差值最小的一组。
    /// </summary>
    public static IReadOnlyList<ExportFolderData> SelectBestTimedWindow(
        IReadOnlyList<ExportFolderData> candidates,
        IReadOnlyList<CrosstalkTestRecord> tests)
    {
        if (candidates.Count < tests.Count)
        {
            throw new ArgumentException("候选导出文件夹少于测试次数。", nameof(candidates));
        }

        ExportFolderData[] orderedCandidates = candidates
            .OrderBy(item => item.ExcelLastWriteUtc)
            .ToArray();
        CrosstalkTestRecord[] orderedTests = tests.OrderBy(item => item.Sequence).ToArray();
        int bestStart = 0;
        double bestDifference = double.MaxValue;
        for (int start = 0; start <= orderedCandidates.Length - orderedTests.Length; start++)
        {
            double totalDifference = 0;
            for (int index = 0; index < orderedTests.Length; index++)
            {
                totalDifference += Math.Abs(
                    (orderedCandidates[start + index].ExcelLastWriteUtc -
                     orderedTests[index].CompletedUtc).TotalSeconds);
            }
            if (totalDifference < bestDifference)
            {
                bestDifference = totalDifference;
                bestStart = start;
            }
        }
        return orderedCandidates.Skip(bestStart).Take(orderedTests.Length).ToArray();
    }

    /// <summary>MATLAB 的 dir('*.xlsx') 后取第一个，因此这里也只看当前层并按名称取首个。</summary>
    private static string? FindFirstExcelFile(string directory) =>
        Directory.EnumerateFiles(directory, "*.xlsx", SearchOption.TopDirectoryOnly)
            .Where(path => !Path.GetFileName(path).StartsWith("~$", StringComparison.Ordinal))
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

    private static bool FilesAreStable(IReadOnlyList<ExportFolderData> selected)
    {
        foreach (ExportFolderData item in selected)
        {
            var current = new FileInfo(item.ExcelPath);
            if (!current.Exists || current.Length != item.ExcelLength ||
                current.LastWriteTimeUtc != item.ExcelLastWriteUtc)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// ZIP 已能打开、Brightness 工作表存在且 C 列正好 612 行，才认为设备已经完成导出。
    /// 文件刚创建但 XML 仍在写入时会返回 false，轮询下一次再试。
    /// </summary>
    private static bool FilesContainCompleteBrightnessData(
        IReadOnlyList<ExportFolderData> selected)
    {
        try
        {
            return selected.All(item =>
                SimpleXlsx.ReadNumericColumn(item.ExcelPath, "Brightness", 3).Length ==
                ExpectedBrightnessRows);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            return false;
        }
    }

    private static string CreateUniqueOutputDirectory(
        string outputRoot,
        DateTime testStartedUtc,
        int testCount)
    {
        lock (OutputDirectoryGate)
        {
            string baseName = $"串扰测试_{testStartedUtc.ToLocalTime():yyyyMMdd_HHmmss}_{testCount}次";
            for (int suffix = 0; ; suffix++)
            {
                string name = suffix == 0 ? baseName : $"{baseName}_{suffix}";
                string path = Path.Combine(outputRoot, name);
                if (Directory.Exists(path)) continue;
                Directory.CreateDirectory(path);
                return path;
            }
        }
    }

    /// <summary>递归复制原始导出文件夹；每次复制前检查取消，停止程序时不会继续大量复制。</summary>
    private static void CopyDirectory(
        string sourceDirectory,
        string targetDirectory,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(targetDirectory);
        foreach (string file in Directory.EnumerateFiles(sourceDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            File.Copy(file, Path.Combine(targetDirectory, Path.GetFileName(file)), overwrite: true);
        }
        foreach (string child in Directory.EnumerateDirectories(sourceDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            CopyDirectory(child, Path.Combine(targetDirectory, Path.GetFileName(child)),
                cancellationToken);
        }
    }

    /// <summary>CSV 明确记录“第几次测试、哪张图片、何时完成、对应哪个原始文件”。</summary>
    private static void WriteTestMapping(
        string outputDirectory,
        IReadOnlyList<CrosstalkTestRecord> tests,
        IReadOnlyList<ExportFolderData> sources)
    {
        var builder = new StringBuilder();
        builder.AppendLine("测试序号,图片文件,完成时间,原始导出文件夹,原始Excel,Excel修改时间");
        CrosstalkTestRecord[] orderedTests = tests.OrderBy(item => item.Sequence).ToArray();
        for (int index = 0; index < orderedTests.Length; index++)
        {
            CrosstalkTestRecord test = orderedTests[index];
            ExportFolderData source = sources[index];
            builder.Append(test.Sequence.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(Csv(Path.GetFileName(test.ImagePath))).Append(',')
                .Append(Csv(test.CompletedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff")))
                .Append(',').Append(Csv(source.DirectoryPath)).Append(',')
                .Append(Csv(source.ExcelPath)).Append(',')
                .AppendLine(Csv(source.ExcelLastWriteUtc.ToLocalTime()
                    .ToString("yyyy-MM-dd HH:mm:ss.fff")));
        }
        File.WriteAllText(Path.Combine(outputDirectory, "测试与原始数据对应表.csv"),
            builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    private static string Csv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";

    public static void WriteCrosstalkWorkbook(
        string path,
        CrosstalkCalculationResult calculation)
    {
        object?[,] statisticsValues = ToPercentageObjectMatrix(calculation.ValuesForStatistics);
        var summary = new object?[2, 3]
        {
            { "Max", "Min", "Mean" },
            {
                RoundPercentage(calculation.Maximum),
                RoundPercentage(calculation.Minimum),
                RoundPercentage(calculation.Mean)
            }
        };
        SimpleXlsx.WriteWorkbook(path,
        [
            new XlsxSheetData("Sheet1", statisticsValues),
            new XlsxSheetData("Sheet2", summary, FirstRowIsHeader: true)
        ]);
    }

    /// <summary>
    /// 按 MATLAB heatmap/exportgraphics 的参考外观生成 3792×2408、300 DPI PNG。
    /// 深灰坐标区、19×32 刻度、无尾随零的三位小数、jet 色带和 NaN 图例均与参考图一致。
    /// </summary>
    public static void WriteHeatmap(string path, string folderName, double[,] heatmapValues)
    {
        if (heatmapValues.GetLength(0) != HeatmapRows ||
            heatmapValues.GetLength(1) != HeatmapColumns)
        {
            throw new ArgumentException(
                $"热力图数据必须为 {HeatmapRows}×{HeatmapColumns}。", nameof(heatmapValues));
        }

        using var bitmap = new Bitmap(
            HeatmapImageWidth, HeatmapImageHeight, PixelFormat.Format24bppRgb);
        bitmap.SetResolution(300, 300);
        using Graphics graphics = Graphics.FromImage(bitmap);
        // 单元格和网格按整数像素绘制，避免高质量平滑在格子边缘产生模糊色缝。
        graphics.SmoothingMode = SmoothingMode.None;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        graphics.Clear(Color.White);

        // 字号单位是 point；300 DPI 下 10 point 约为 42 个像素，与 MATLAB 参考图相近。
        using var titleFont = new Font("Microsoft YaHei UI", 10, FontStyle.Regular);
        // MATLAB 的数字字体比中文字体窄；Arial 可避免两位行号和 0.5 刻度在边缘被截断。
        using var valueFont = new Font("Arial", 7.5f, FontStyle.Regular);
        using var axisFont = new Font("Arial", 10, FontStyle.Regular);
        Color axesColor = Color.FromArgb(33, 33, 33);
        using var gridPen = new Pen(axesColor, 2);
        using var axesBrush = new SolidBrush(axesColor);
        using var textBrush = new SolidBrush(Color.Black);

        // 以下坐标按用户提供的 MATLAB 300 DPI 参考图测得。
        // 数据区恰好覆盖 32 列×19 行；最外圈为 NaN 时会露出深灰坐标区。
        const int plotLeft = 66;
        const int plotTop = 47;
        const int plotWidth = 3536;
        const int plotHeight = 2295;
        const int colorBarLeft = 3633;
        const int colorBarWidth = 69;
        const int colorBarHeight = 2195;
        const int nanLegendTop = 2273;
        const int nanLegendHeight = 69;

        graphics.FillRectangle(axesBrush, plotLeft, plotTop, plotWidth, plotHeight);

        string title = $"crosstalk {folderName}";
        SizeF titleSize = graphics.MeasureString(title, titleFont);
        graphics.DrawString(title, titleFont, textBrush,
            plotLeft + (plotWidth - titleSize.Width) / 2, 0);

        using var center = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            FormatFlags = StringFormatFlags.NoWrap
        };

        for (int row = 0; row < HeatmapRows; row++)
        {
            int cellTop = plotTop + (int)Math.Round(row * plotHeight / (double)HeatmapRows);
            int cellBottom = plotTop +
                (int)Math.Round((row + 1) * plotHeight / (double)HeatmapRows);

            // MATLAB 在左侧显示 1~19 行号，刻度与每个格子的垂直中心对齐。
            var rowLabelRectangle = new RectangleF(
                0, cellTop, plotLeft, cellBottom - cellTop);
            graphics.DrawString((row + 1).ToString(CultureInfo.InvariantCulture),
                axisFont, textBrush, rowLabelRectangle, center);

            for (int column = 0; column < HeatmapColumns; column++)
            {
                double ratio = heatmapValues[row, column];
                int cellLeft = plotLeft +
                    (int)Math.Round(column * plotWidth / (double)HeatmapColumns);
                int cellRight = plotLeft +
                    (int)Math.Round((column + 1) * plotWidth / (double)HeatmapColumns);

                // NaN 表示不绘制的边框：不填色、不画格线，直接显示深灰坐标背景。
                if (double.IsNaN(ratio)) continue;

                var rectangle = new Rectangle(
                    cellLeft, cellTop, cellRight - cellLeft, cellBottom - cellTop);
                Color color = JetColor(Math.Clamp(ratio * 100d / 3d, 0d, 1d));
                using (var fill = new SolidBrush(color)) graphics.FillRectangle(fill, rectangle);
                graphics.DrawRectangle(gridPen,
                    rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);

                // MATLAB 单元格最多保留三位小数，但会省略末尾的 0，例如 1.300 显示为 1.3。
                string value = RoundPercentage(ratio)
                    .ToString("0.###", CultureInfo.InvariantCulture);
                double luminance =
                    (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255d;
                Color foreground = luminance < 0.48 ? Color.White : Color.Black;
                using var valueBrush = new SolidBrush(foreground);
                graphics.DrawString(value, valueFont, valueBrush, rectangle, center);
            }
        }

        // MATLAB 在数据区下方显示 1~32 列号，刻度与每个格子的水平中心对齐。
        for (int column = 0; column < HeatmapColumns; column++)
        {
            int cellLeft = plotLeft +
                (int)Math.Round(column * plotWidth / (double)HeatmapColumns);
            int cellRight = plotLeft +
                (int)Math.Round((column + 1) * plotWidth / (double)HeatmapColumns);
            var columnLabelRectangle = new RectangleF(
                cellLeft, plotTop + plotHeight, cellRight - cellLeft,
                HeatmapImageHeight - (plotTop + plotHeight));
            graphics.DrawString((column + 1).ToString(CultureInfo.InvariantCulture),
                axisFont, textBrush, columnLabelRectangle, center);
        }

        // MATLAB caxis([0,3]) 对应右侧 0~3 色条；超过 3% 的异常值仍绘制并钳制为深红色。
        for (int pixel = 1; pixel < colorBarHeight - 1; pixel++)
        {
            double normalized = 1d - (pixel - 1d) / (colorBarHeight - 3d);
            using var pen = new Pen(JetColor(normalized));
            graphics.DrawLine(pen,
                colorBarLeft + 1, plotTop + pixel,
                colorBarLeft + colorBarWidth - 2, plotTop + pixel);
        }
        graphics.DrawRectangle(gridPen,
            colorBarLeft, plotTop, colorBarWidth - 1, colorBarHeight - 1);

        // MATLAB 参考图按 0.5 递增显示 0、0.5、1……3，数值本身已经代表百分比，不再附加 %。
        for (int tick = 0; tick <= 6; tick++)
        {
            double tickValue = tick / 2d;
            float y = plotTop + (colorBarHeight - 1) * (float)(1d - tickValue / 3d);
            graphics.DrawLine(gridPen,
                colorBarLeft + colorBarWidth - 1, y,
                colorBarLeft + colorBarWidth + 11, y);
            var tickRectangle = new RectangleF(
                colorBarLeft + colorBarWidth + 10, y - 32, 80, 64);
            graphics.DrawString(tickValue.ToString("0.#", CultureInfo.InvariantCulture),
                axisFont, textBrush, tickRectangle, center);
        }

        // MATLAB heatmap 会为缺失值单独显示深灰色 NaN 图例。
        graphics.FillRectangle(axesBrush,
            colorBarLeft, nanLegendTop, colorBarWidth, nanLegendHeight);
        var nanLabelRectangle = new RectangleF(
            colorBarLeft + colorBarWidth + 3, nanLegendTop,
            HeatmapImageWidth - (colorBarLeft + colorBarWidth + 3), nanLegendHeight);
        graphics.DrawString("NaN", axisFont, textBrush, nanLabelRectangle, center);

        bitmap.Save(path, ImageFormat.Png);
    }

    /// <summary>MATLAB jet 色带的近似公式，输入 0 为蓝色、0.5 为绿黄、1 为红色。</summary>
    private static Color JetColor(double value)
    {
        double red = Math.Clamp(1.5 - Math.Abs(4 * value - 3), 0, 1);
        double green = Math.Clamp(1.5 - Math.Abs(4 * value - 2), 0, 1);
        double blue = Math.Clamp(1.5 - Math.Abs(4 * value - 1), 0, 1);
        return Color.FromArgb(
            (int)Math.Round(red * 255),
            (int)Math.Round(green * 255),
            (int)Math.Round(blue * 255));
    }

    private static double DivideLikeMatlab(double numerator, double denominator)
    {
        if (denominator != 0) return numerator / denominator;
        if (numerator == 0) return double.NaN;
        return numerator > 0 ? double.PositiveInfinity : double.NegativeInfinity;
    }

    /// <summary>MATLAB min 默认传播 NaN；没有 NaN 时取普通最小值。</summary>
    private static double MinimumIncludingNaN(IEnumerable<double> values)
    {
        double minimum = double.PositiveInfinity;
        foreach (double value in values)
        {
            if (double.IsNaN(value)) return double.NaN;
            if (value < minimum) minimum = value;
        }
        return minimum;
    }

    private static object?[,] ToObjectMatrix(double[,] source)
    {
        var result = new object?[source.GetLength(0), source.GetLength(1)];
        for (int row = 0; row < source.GetLength(0); row++)
        {
            for (int column = 0; column < source.GetLength(1); column++)
            {
                result[row, column] = source[row, column];
            }
        }
        return result;
    }

    private static object?[,] ToPercentageObjectMatrix(double[,] source)
    {
        var result = new object?[source.GetLength(0), source.GetLength(1)];
        for (int row = 0; row < source.GetLength(0); row++)
        {
            for (int column = 0; column < source.GetLength(1); column++)
            {
                double value = source[row, column];
                result[row, column] = double.IsNaN(value) ? null : RoundPercentage(value);
            }
        }
        return result;
    }

    private static double RoundPercentage(double ratio) =>
        Math.Round(ratio * 100d, 3, MidpointRounding.AwayFromZero);

    /// <summary>设备的一次测试对应 ExportFile 下一个一级文件夹及其中第一个 XLSX。</summary>
    public sealed record ExportFolderData(
        string DirectoryPath,
        string ExcelPath,
        long ExcelLength,
        DateTime ExcelLastWriteUtc);
}

