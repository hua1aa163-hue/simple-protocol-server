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
    double Mean)
{
    /// <summary>未剔除边框、异常点和用户掩膜的 19×32 原始串扰比例矩阵。</summary>
    public double[,] RawValues { get; init; } = new double[0, 0];

    /// <summary>固定的外围边框掩膜。</summary>
    public bool[,] BorderMask { get; init; } = new bool[0, 0];

    /// <summary>按当前阈值计算出的异常点掩膜（边框之外仍可用于显示）。</summary>
    public bool[,] AbnormalMask { get; init; } = new bool[0, 0];

    /// <summary>用户坐标/鼠标掩膜。</summary>
    public bool[,] UserMask { get; init; } = new bool[0, 0];

    /// <summary>本次计算实际使用的参数。</summary>
    public CrosstalkAnalysisOptions AnalysisOptions { get; init; } =
        CrosstalkAnalysisOptions.Default;

    /// <summary>统计中有效点数量。</summary>
    public int ValidPointCount
    {
        get
        {
            int count = 0;
            foreach (double value in ValuesForStatistics)
                if (double.IsFinite(value)) count++;
            return count;
        }
    }

    /// <summary>当前阈值下、去除边框后的异常点数量（用户掩膜点不计入）。</summary>
    public int AbnormalPointCount
    {
        get
        {
            bool[,]? abnormalMask = AbnormalMask;
            bool[,]? borderMask = BorderMask;
            bool[,]? userMask = UserMask;
            if (abnormalMask is null || abnormalMask.Length == 0) return 0;
            int count = 0;
            bool hasBorderMask = borderMask is not null &&
                                 borderMask.GetLength(0) == abnormalMask.GetLength(0) &&
                                 borderMask.GetLength(1) == abnormalMask.GetLength(1);
            bool hasUserMask = userMask is not null &&
                               userMask.GetLength(0) == abnormalMask.GetLength(0) &&
                               userMask.GetLength(1) == abnormalMask.GetLength(1);
            for (int row = 0; row < abnormalMask.GetLength(0); row++)
            {
                for (int column = 0; column < abnormalMask.GetLength(1); column++)
                {
                    if (abnormalMask[row, column] &&
                        (!hasBorderMask || !borderMask![row, column]) &&
                        (!hasUserMask || !userMask![row, column])) count++;
                }
            }
            return count;
        }
    }
}

/// <summary>自动处理完成后交给界面显示的结果。</summary>
public sealed record CrosstalkProcessingResult(
    string OutputDirectory,
    string RawWorkbookPath,
    string CrosstalkWorkbookPath,
    string HeatmapPath,
    int SourceFileCount,
    CrosstalkCalculationResult Calculation)
{
    public CrosstalkAnalysisOptions AnalysisOptions => Calculation.AnalysisOptions;
}

/// <summary>导出当前掩膜/色轴设置生成的独立结果文件。</summary>
public sealed record CrosstalkExportResult(
    string CrosstalkWorkbookPath,
    string HeatmapPath,
    CrosstalkCalculationResult Calculation);

/// <summary>直接选择 ExportFile 根目录时读取到的串扰输入。</summary>
public sealed record CrosstalkFolderData(
    string SourceRoot,
    IReadOnlyList<string> Subfolders,
    IReadOnlyList<string> ExcelFiles,
    double[,] MergedData);

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
    public const double AbnormalThreshold = CrosstalkAnalysisOptions.DefaultAbnormalThresholdRatio;

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
        => await ProcessCompletedTestAsync(
            sourceRoot,
            outputRoot,
            testStartedUtc,
            snapshot,
            tests,
            progress,
            cancellationToken,
            analysisOptions: null).ConfigureAwait(false);

    /// <summary>
    /// 等待并处理一批串扰导出数据，使用指定的异常阈值、色轴和用户掩膜。
    /// 旧的无 options 重载保留 3% 阈值和 0..50% 色轴的默认行为。
    /// </summary>
    public static async Task<CrosstalkProcessingResult> ProcessCompletedTestAsync(
        string sourceRoot,
        string outputRoot,
        DateTime testStartedUtc,
        IReadOnlyDictionary<string, ExportFolderFingerprint> snapshot,
        IReadOnlyList<CrosstalkTestRecord> tests,
        IProgress<string>? progress,
        CancellationToken cancellationToken,
        CrosstalkAnalysisOptions? analysisOptions)
    {
        if (tests.Count == 0) throw new ArgumentException("没有完成的测试记录。", nameof(tests));

        CrosstalkAnalysisOptions options = (analysisOptions ?? CrosstalkAnalysisOptions.Default)
            .Normalize();

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
        CrosstalkCalculationResult calculation = Calculate(mergedData, options);

        string folderName = Path.GetFileName(outputDirectory);
        string rawWorkbookPath = Path.Combine(
            outputDirectory, $"orig{sourceFolders.Count}_output{folderName}.xlsx");
        string crosstalkWorkbookPath = Path.Combine(
            outputDirectory, $"crosstalk {folderName}.xlsx");
        string heatmapPath = Path.Combine(
            outputDirectory, $"crosstalk {folderName}.png");

        SimpleXlsx.WriteWorkbook(rawWorkbookPath,
            [new XlsxSheetData("Sheet1", ToObjectMatrix(mergedData))]);
        WriteCrosstalkWorkbook(crosstalkWorkbookPath, calculation, options);
        WriteHeatmap(heatmapPath, folderName, calculation.ValuesForHeatmap, options);

        return new CrosstalkProcessingResult(
            outputDirectory,
            rawWorkbookPath,
            crosstalkWorkbookPath,
            heatmapPath,
            sourceFolders.Count,
            calculation);
    }

    /// <summary>
    /// 把多个等长或不等长 C 列按 MATLAB 规则合成“测试点×测试图”矩阵。
    /// <para>
    /// 不在这里强制限定 612 行：参考程序会在计算阶段执行
    /// <c>values[1:-3]</c>，再裁掉首尾纯 NaN 行，最终以 608 个采样点为准。
    /// </para>
    /// </summary>
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
    /// 按参考 CrosstalkAnalyzer 的规则读取 ExportFile 根目录：一级子文件夹按名称排序，
    /// 每个子文件夹取名称排序后的首个 xlsx，并合并其 Brightness!C 列。
    /// </summary>
    public static CrosstalkFolderData ReadBrightnessFolder(string sourceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRoot);
        if (!Directory.Exists(sourceRoot))
            throw new DirectoryNotFoundException($"串扰数据文件夹不存在：{sourceRoot}");

        string[] directories = Directory.EnumerateDirectories(sourceRoot)
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (directories.Length == 0)
            throw new InvalidDataException("所选串扰数据文件夹中没有一级子文件夹。");

        var excelFiles = new List<string>(directories.Length);
        foreach (string directory in directories)
        {
            string? excel = FindFirstExcelFile(directory);
            if (excel is null)
                throw new InvalidDataException(
                    $"子文件夹“{Path.GetFileName(directory)}”中没有可读取的 xlsx 文件。");
            excelFiles.Add(excel);
        }
        double[,] merged = MergeBrightnessColumns(excelFiles);
        return new CrosstalkFolderData(sourceRoot, directories, excelFiles, merged);
    }

    /// <summary>直接从 ExportFile 根目录读取并计算，不依赖一次自动测试的完成记录。</summary>
    public static CrosstalkCalculationResult CalculateFolder(
        string sourceRoot,
        CrosstalkAnalysisOptions? analysisOptions = null)
        => Calculate(ReadBrightnessFolder(sourceRoot).MergedData, analysisOptions);

    /// <summary>
    /// 严格翻译 MATLAB 计算段。最后一列是本底，前面的列平均分成前后两组并一一配对。
    /// 对每个采样点计算正向和反向两个比值，负数先改成 100，再从全部比值中取最小值。
    /// </summary>
    public static CrosstalkCalculationResult Calculate(double[,] mergedData) =>
        Calculate(mergedData, CrosstalkAnalysisOptions.Default);

    /// <summary>使用指定分析参数计算串扰矩阵。</summary>
    public static CrosstalkCalculationResult Calculate(
        double[,] mergedData,
        CrosstalkAnalysisOptions? analysisOptions)
    {
        ArgumentNullException.ThrowIfNull(mergedData);
        int rowCount = mergedData.GetLength(0);
        int columnCount = mergedData.GetLength(1);
        // MATLAB 的 values[1:-3] 至少要有 608 个位置；额外的首尾纯 NaN
        // 行会在下面按参考程序的自动数值区规则裁掉。
        if (rowCount < HeatmapRows * HeatmapColumns + 4)
        {
            throw new InvalidDataException(
                $"串扰计算至少需要 {HeatmapRows * HeatmapColumns + 4} 行数据，实际为 {rowCount} 行；" +
                "按 MATLAB 的首行/末三行规则无法得到 608 个采样点。");
        }
        if (columnCount < 3 || columnCount % 2 == 0)
        {
            throw new InvalidDataException(
                $"Excel 数量必须为大于等于 3 的奇数：前后两组数量相同，最后 1 份为本底；实际为 {columnCount} 份。");
        }

        int pairedColumnCount = (columnCount - 1) / 2;
        int backgroundColumn = columnCount - 1;
        // Keep one ratio slot for every source row.  Typical MRTEST files have
        // 612 rows, but the reference reader may retain extra edge rows that
        // are removed by the [1:-3] + NaN trimming below.
        var ratios = new double[rowCount];
        // ratios[0] 保持 MATLAB zeros(rows,1) 的初始 0；真正数据从第 2 行开始计算。
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

        // MATLAB：AA(1,:)=[]，再 AA(end-2:end,:)=[]；参考 Python 工具还会
        // 清除 pandas 在数据区首尾保留的纯文本/空白行。这里同样只裁剪
        // 首尾 NaN，保留内部 NaN（它们会在后续统计中被排除）。
        double[] trimmed = ratios.Skip(1).Take(rowCount - 4).ToArray();
        int firstFinite = 0;
        while (firstFinite < trimmed.Length && double.IsNaN(trimmed[firstFinite]))
            firstFinite++;
        int lastFinite = trimmed.Length - 1;
        while (lastFinite >= firstFinite && double.IsNaN(trimmed[lastFinite]))
            lastFinite--;
        int sampleCount = lastFinite >= firstFinite ? lastFinite - firstFinite + 1 : 0;
        if (sampleCount != HeatmapRows * HeatmapColumns)
        {
            throw new InvalidDataException(
                $"按 MATLAB 规则清理表头及末三行后应有 {HeatmapRows * HeatmapColumns} 个采样点，" +
                $"当前为 {sampleCount}；请检查 Brightness!C:C 数据区。");
        }
        double[] reshapedSource = trimmed
            .Skip(firstFinite)
            .Take(sampleCount)
            .ToArray();
        var restored = new double[HeatmapRows, HeatmapColumns];
        for (int row = 0; row < HeatmapRows; row++)
        {
            for (int column = 0; column < HeatmapColumns; column++)
            {
                // reshape(AA,32,19)' 的最终对应关系就是每 32 个连续值组成一行。
                restored[row, column] = reshapedSource[row * HeatmapColumns + column];
            }
        }

        return ApplyAnalysisOptions(restored, analysisOptions);
    }

    /// <summary>用原始比例阈值快速计算；例如 0.03 表示 3%。</summary>
    public static CrosstalkCalculationResult Calculate(
        double[,] mergedData,
        double abnormalThresholdRatio)
        => Calculate(mergedData, new CrosstalkAnalysisOptions
        {
            AbnormalThresholdRatio = abnormalThresholdRatio
        });

    /// <summary>
    /// 计算两个同尺寸矩阵的相对差值（百分比）。
    /// <para>
    /// 这是参考分析器公开的通用 <c>calculate_crosstalk(signal, reference)</c>
    /// 入口的纯 C# 实现；返回值单位为百分比，而本类的 MATLAB 串扰矩阵
    /// 入口仍使用比例（例如 0.03 表示 3%）。没有 reference 时返回 signal
    /// 的独立副本；参考矩阵为零或结果非有限的位置返回 <see cref="double.NaN"/>。
    /// </para>
    /// </summary>
    public static double[,] CalculateCrosstalk(
        double[,] signal,
        double[,]? reference = null)
    {
        ArgumentNullException.ThrowIfNull(signal);
        if (reference is null) return (double[,])signal.Clone();
        if (signal.GetLength(0) != reference.GetLength(0) ||
            signal.GetLength(1) != reference.GetLength(1))
        {
            throw new InvalidDataException(
                $"信号与参考矩阵尺寸不一致：{signal.GetLength(0)}×{signal.GetLength(1)} / " +
                $"{reference.GetLength(0)}×{reference.GetLength(1)}。");
        }

        var result = new double[signal.GetLength(0), signal.GetLength(1)];
        for (int row = 0; row < signal.GetLength(0); row++)
        {
            for (int column = 0; column < signal.GetLength(1); column++)
            {
                double value = (signal[row, column] - reference[row, column]) /
                               Math.Abs(reference[row, column]) * 100d;
                result[row, column] = double.IsFinite(value) ? value : double.NaN;
            }
        }
        return result;
    }

    /// <summary>
    /// 读取参考分析器支持的通用二维矩阵文件（CSV/TSV/TXT/XLSX/常见 NPY）。
    /// 该转发入口让现有串扰处理器调用方无需依赖读取器具体类型。
    /// </summary>
    public static double[,] ReadMatrix(string path) => CrosstalkMatrixReader.ReadMatrix(path);

    /// <summary>列出目录内支持的通用矩阵文件，按修改时间和文件名排序。</summary>
    public static IReadOnlyList<string> ListDataFiles(string folder) =>
        CrosstalkMatrixReader.ListDataFiles(folder);

    /// <summary>计算任意矩阵的有限点统计值，支持可选排除掩膜。</summary>
    public static CrosstalkStatistics Statistics(double[,] values, bool[,]? excluded = null) =>
        CrosstalkAnalysisUtilities.Statistics(values, excluded);

    /// <summary>创建 1-based、含首尾坐标的矩形掩膜。</summary>
    public static bool[,] RectangleMask(int rows, int columns,
        int x1, int y1, int x2, int y2) =>
        CrosstalkAnalysisUtilities.RectangleMask(rows, columns, x1, y1, x2, y2);

    /// <summary>把 1-based、含首尾坐标的矩形加入现有掩膜。</summary>
    public static void AddRectangle(bool[,] mask, int x1, int y1, int x2, int y2) =>
        CrosstalkAnalysisUtilities.AddRectangle(mask, x1, y1, x2, y2);

    /// <summary>按比例阈值创建异常点掩膜。</summary>
    public static bool[,] MakeAbnormalMask(double[,] values, double threshold = .03d) =>
        CrosstalkAnalysisUtilities.MakeAbnormalMask(values, threshold);

    /// <summary>
    /// 对已有计算结果重新应用阈值、色轴和用户掩膜，无需重新读取 ExportFile。
    /// 该入口用于主界面调整选项或导出当前掩膜结果。
    /// </summary>
    public static CrosstalkCalculationResult Recalculate(
        CrosstalkCalculationResult calculation,
        CrosstalkAnalysisOptions? analysisOptions)
    {
        ArgumentNullException.ThrowIfNull(calculation);
        double[,] raw = calculation.RawValues;
        if (raw.GetLength(0) != HeatmapRows || raw.GetLength(1) != HeatmapColumns)
            throw new InvalidDataException("现有串扰结果没有可重新计算的 19×32 原始矩阵。");
        return ApplyAnalysisOptions((double[,])raw.Clone(), analysisOptions);
    }

    private static CrosstalkCalculationResult ApplyAnalysisOptions(
        double[,] restored,
        CrosstalkAnalysisOptions? analysisOptions)
    {
        if (restored.GetLength(0) != HeatmapRows || restored.GetLength(1) != HeatmapColumns)
            throw new InvalidDataException($"串扰矩阵必须为 {HeatmapRows}×{HeatmapColumns}。");

        CrosstalkAnalysisOptions options = (analysisOptions ?? CrosstalkAnalysisOptions.Default)
            .Normalize(HeatmapRows, HeatmapColumns);
        bool[,] userMask = options.UserMask is null
            ? new bool[HeatmapRows, HeatmapColumns]
            : (bool[,])options.UserMask.Clone();
        var borderMask = new bool[HeatmapRows, HeatmapColumns];
        var abnormalMask = new bool[HeatmapRows, HeatmapColumns];
        for (int row = 0; row < HeatmapRows; row++)
        {
            for (int column = 0; column < HeatmapColumns; column++)
            {
                borderMask[row, column] = row == 0 || row == HeatmapRows - 1 ||
                                          column == 0 || column == HeatmapColumns - 1;
                abnormalMask[row, column] = restored[row, column] > options.AbnormalThresholdRatio;
            }
        }

        var valuesForStatistics = (double[,])restored.Clone();
        var valuesForHeatmap = (double[,])restored.Clone();
        var validStatistics = new List<double>();
        for (int row = 0; row < HeatmapRows; row++)
        {
            for (int column = 0; column < HeatmapColumns; column++)
            {
                bool excludedFromHeatmap = borderMask[row, column] || userMask[row, column];
                bool excludedFromStatistics = excludedFromHeatmap || abnormalMask[row, column];
                // 边框和用户掩膜显示为深灰；异常值保留绘图但不进入统计。
                if (excludedFromHeatmap) valuesForHeatmap[row, column] = double.NaN;
                if (excludedFromStatistics)
                {
                    valuesForStatistics[row, column] = double.NaN;
                }
                else if (double.IsFinite(restored[row, column]))
                {
                    validStatistics.Add(restored[row, column]);
                }
            }
        }

        return new CrosstalkCalculationResult(
            valuesForStatistics,
            valuesForHeatmap,
            validStatistics.Count == 0 ? double.NaN : validStatistics.Max(),
            validStatistics.Count == 0 ? double.NaN : validStatistics.Min(),
            validStatistics.Count == 0 ? double.NaN : validStatistics.Average())
        {
            RawValues = (double[,])restored.Clone(),
            BorderMask = borderMask,
            AbnormalMask = abnormalMask,
            UserMask = userMask,
            AnalysisOptions = options
        };
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
    /// ZIP 已能打开、Brightness 工作表存在且 C 列至少有足够行数，才认为设备已经完成导出。
    /// 最终是否能按参考 MATLAB 规则裁成 608 个采样点仍由 Calculate 严格校验。
    /// 文件刚创建但 XML 仍在写入时会返回 false，轮询下一次再试。
    /// </summary>
    private static bool FilesContainCompleteBrightnessData(
        IReadOnlyList<ExportFolderData> selected)
    {
        try
        {
            return selected.All(item =>
                SimpleXlsx.ReadNumericColumn(item.ExcelPath, "Brightness", 3).Length >=
                HeatmapRows * HeatmapColumns + 4);
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
        => WriteCrosstalkWorkbook(path, calculation, calculation.AnalysisOptions);

    /// <summary>写出串扰统计工作簿，并将当前色轴、阈值和掩膜信息写入 Parameters。</summary>
    public static void WriteCrosstalkWorkbook(
        string path,
        CrosstalkCalculationResult calculation,
        CrosstalkAnalysisOptions? analysisOptions)
    {
        ArgumentNullException.ThrowIfNull(calculation);
        CrosstalkAnalysisOptions options = (analysisOptions ?? calculation.AnalysisOptions)
            .Normalize();
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
        var parameters = new object?[10, 2]
        {
            { "Parameter", "Value" },
            { "caxis_min_percent", options.ColorAxisMinimumPercent },
            { "caxis_max_percent", options.ColorAxisMaximumPercent },
            { "abnormal_threshold_ratio", options.AbnormalThresholdRatio },
            { "abnormal_threshold_percent", options.AbnormalThresholdPercent },
            { "user_mask_points", CrosstalkMaskStore.Count(calculation.UserMask) },
            { "border_excluded_points", CountTrue(calculation.BorderMask) },
            { "mask_name", options.MaskName },
            { "mask_note", options.MaskNote },
            { "export_time", DateTime.Now.ToString("O", CultureInfo.InvariantCulture) }
        };
        SimpleXlsx.WriteWorkbook(path,
        [
            new XlsxSheetData("Sheet1", statisticsValues),
            new XlsxSheetData("Sheet2", summary, FirstRowIsHeader: true),
            new XlsxSheetData("Parameters", parameters, FirstRowIsHeader: true)
        ]);
    }

    /// <summary>
    /// 从已有结果重新应用参数，并导出不覆盖原结果的独立 XLSX/PNG。
    /// outputDirectory 可以是自动测试结果目录，也可以是用户手动选择的数据目录。
    /// </summary>
    public static CrosstalkExportResult ExportCurrentResult(
        string outputDirectory,
        string folderName,
        CrosstalkCalculationResult calculation,
        CrosstalkAnalysisOptions? analysisOptions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(folderName);
        ArgumentNullException.ThrowIfNull(calculation);
        Directory.CreateDirectory(outputDirectory);
        CrosstalkAnalysisOptions options = (analysisOptions ?? calculation.AnalysisOptions)
            .Normalize();
        CrosstalkCalculationResult recalculated = Recalculate(calculation, options);
        string safeFolder = SafeFileNamePart(folderName);
        string safeMask = string.IsNullOrWhiteSpace(options.MaskName)
            ? "custom"
            : SafeFileNamePart(options.MaskName);
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
        // Include a short GUID suffix as a second-level collision guard.  A
        // user can click Export repeatedly within the same millisecond, and
        // each export must remain an independent result rather than silently
        // overwriting the previous workbook/PNG.
        string unique = Guid.NewGuid().ToString("N")[..8];
        string baseName = $"crosstalk {safeFolder}_masked_{safeMask}_{stamp}_{unique}";
        string workbookPath = Path.Combine(outputDirectory, baseName + ".xlsx");
        string heatmapPath = Path.Combine(outputDirectory, baseName + ".png");
        WriteCrosstalkWorkbook(workbookPath, recalculated, options);
        WriteHeatmap(heatmapPath, folderName, recalculated.ValuesForHeatmap, options);
        return new CrosstalkExportResult(workbookPath, heatmapPath, recalculated);
    }

    /// <summary>直接从 ExportFile 根目录计算并导出当前参数结果。</summary>
    public static CrosstalkExportResult ExportCurrentFolderResult(
        string sourceRoot,
        string outputDirectory,
        CrosstalkAnalysisOptions? analysisOptions = null)
    {
        CrosstalkFolderData folder = ReadBrightnessFolder(sourceRoot);
        CrosstalkCalculationResult calculation = Calculate(folder.MergedData, analysisOptions);
        Directory.CreateDirectory(outputDirectory);
        string folderName = Path.GetFileName(
            sourceRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(folderName)) folderName = "串扰结果";

        // The reference analyzer writes an untouched merged matrix on every
        // fresh calculation.  Keep that artifact as well, while the masked
        // result remains in the independent file returned below.
        string rawBaseName = $"orig{folder.ExcelFiles.Count}_output{SafeFileNamePart(folderName)}";
        // Keep the familiar first filename, but never overwrite a previous
        // manual calculation when the same folder is exported repeatedly.
        // Selection and write happen under the same process-wide gate so two
        // concurrent C# calls cannot claim the same suffix.
        string rawPath;
        lock (OutputDirectoryGate)
        {
            rawPath = CreateNonCollidingFilePath(outputDirectory, rawBaseName, ".xlsx");
            SimpleXlsx.WriteWorkbook(rawPath,
                [new XlsxSheetData("Sheet1", ToObjectMatrix(folder.MergedData))]);
        }

        return ExportCurrentResult(outputDirectory, folderName, calculation, analysisOptions);
    }

    private static string CreateNonCollidingFilePath(
        string directory, string baseName, string extension)
    {
        string normalizedExtension = extension.StartsWith('.') ? extension : "." + extension;
        string candidate = Path.Combine(directory, baseName + normalizedExtension);
        for (int suffix = 1; File.Exists(candidate) || Directory.Exists(candidate); suffix++)
            candidate = Path.Combine(directory, $"{baseName}_{suffix}{normalizedExtension}");
        return candidate;
    }

    /// <summary>
    /// 按 MATLAB heatmap/exportgraphics 的参考外观生成 3792×2408、300 DPI PNG。
    /// 深灰坐标区、19×32 刻度、无尾随零的三位小数、jet 色带和 NaN 图例均与参考图一致。
    /// </summary>
    public static void WriteHeatmap(string path, string folderName, double[,] heatmapValues)
        => WriteHeatmap(path, folderName, heatmapValues, CrosstalkAnalysisOptions.Default);

    /// <summary>按指定百分比色轴生成热力图；异常值仍绘制并按色轴钳制。</summary>
    public static void WriteHeatmap(
        string path,
        string folderName,
        double[,] heatmapValues,
        CrosstalkAnalysisOptions? analysisOptions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(heatmapValues);
        string? parent = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(parent)) Directory.CreateDirectory(parent);
        CrosstalkAnalysisOptions options = (analysisOptions ?? CrosstalkAnalysisOptions.Default)
            .Normalize();
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

                // 非有限值表示无法绘制的采样点；边框/用户掩膜通常已经被写成 NaN，
                // 其余的无穷值也不能被误画成红色热点。
                var rectangle = new Rectangle(
                    cellLeft, cellTop, cellRight - cellLeft, cellBottom - cellTop);
                if (!double.IsFinite(ratio))
                {
                    // Match the reference renderer's two missing-value
                    // colours: user/border exclusions are dark gray, while
                    // an otherwise invalid source sample is neutral gray.
                    bool excluded = row == 0 || row == HeatmapRows - 1 ||
                                    column == 0 || column == HeatmapColumns - 1 ||
                                    (options.UserMask is not null && options.UserMask[row, column]);
                    Color missingColor = excluded
                        ? Color.FromArgb(35, 35, 35)
                        : Color.FromArgb(90, 90, 90);
                    using var missingBrush = new SolidBrush(missingColor);
                    graphics.FillRectangle(missingBrush, rectangle);
                    graphics.DrawRectangle(gridPen,
                        rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
                    continue;
                }

                double percent = ratio * 100d;
                double normalized = (percent - options.ColorAxisMinimumPercent) /
                    (options.ColorAxisMaximumPercent - options.ColorAxisMinimumPercent);
                Color color = CrosstalkColorMap.Jet(normalized);
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

        // 色条对应用户设置的百分比色轴；超出范围的值仍绘制并钳制到两端颜色。
        for (int pixel = 1; pixel < colorBarHeight - 1; pixel++)
        {
            double normalized = 1d - (pixel - 1d) / (colorBarHeight - 3d);
            using var pen = new Pen(CrosstalkColorMap.Jet(normalized));
            graphics.DrawLine(pen,
                colorBarLeft + 1, plotTop + pixel,
                colorBarLeft + colorBarWidth - 2, plotTop + pixel);
        }
        graphics.DrawRectangle(gridPen,
            colorBarLeft, plotTop, colorBarWidth - 1, colorBarHeight - 1);

        // 显示 6 个等距刻度（含两端）；数值本身已经代表百分比，不再附加 %。
        const int tickCount = 6;
        for (int tick = 0; tick <= tickCount; tick++)
        {
            double tickValue = options.ColorAxisMinimumPercent +
                (options.ColorAxisMaximumPercent - options.ColorAxisMinimumPercent) *
                tick / (double)tickCount;
            float y = plotTop + (colorBarHeight - 1) * (float)(1d - tick / (double)tickCount);
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

    private static int CountTrue(bool[,] values)
    {
        int count = 0;
        foreach (bool value in values) if (value) count++;
        return count;
    }

    private static string SafeFileNamePart(string value)
    {
        var invalid = new HashSet<char>(Path.GetInvalidFileNameChars());
        var builder = new StringBuilder(value.Length);
        foreach (char character in value.Trim())
            builder.Append(invalid.Contains(character) ? '_' : character);
        string result = builder.ToString().Trim(' ', '.', '_');
        return string.IsNullOrWhiteSpace(result) ? "result" : result;
    }

    private static double DivideLikeMatlab(double numerator, double denominator)
    {
        // IEEE/NumPy division propagates NaN even when the denominator is
        // zero.  Check this before the explicit zero-denominator branch;
        // otherwise NaN/0 would be misclassified as -Infinity and then
        // converted to the 100 sentinel below.
        if (double.IsNaN(numerator) || double.IsNaN(denominator)) return double.NaN;
        if (denominator != 0) return numerator / denominator;
        if (numerator == 0) return double.NaN;
        return numerator > 0 ? double.PositiveInfinity : double.NegativeInfinity;
    }

    /// <summary>
    /// 与参考 Python/NumPy 实现一致：每一行忽略 NaN 后取最小值，
    /// 但保留正负无穷（负无穷会在上层按 MATLAB 规则替换为 100）。
    /// </summary>
    private static double MinimumIncludingNaN(IEnumerable<double> values)
    {
        double minimum = double.PositiveInfinity;
        bool found = false;
        foreach (double value in values)
        {
            if (double.IsNaN(value)) continue;
            found = true;
            if (value < minimum) minimum = value;
        }
        return found ? minimum : double.NaN;
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
                result[row, column] = double.IsFinite(value) ? RoundPercentage(value) : null;
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

