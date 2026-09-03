using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AutoTestClient.Controls;
using AutoTestClient.DataProcessing;
using AutoTestClient.Models;

namespace AutoTestClient;

/// <summary>
/// 串扰结果与掩膜编辑窗口。
/// <para>所有计算、热图和导出均由本项目的 C# 数据处理层完成，不启动或调用 Python。</para>
/// <para>无参构造保留给 Visual Studio WinForms Designer；设计时不访问磁盘。</para>
/// </summary>
public partial class CrosstalkResultForm : Form
{
    private readonly TestDataProcessingResult? _result;
    private readonly bool _isDesignTime;
    private readonly string? _initialSourceRoot;
    private readonly string? _initialOutputRoot;
    private readonly CrosstalkAnalysisOptions? _initialOptions;
    private CrosstalkCalculationResult? _baseCalculation;
    private CrosstalkCalculationResult? _currentCalculation;
    private CrosstalkAnalysisOptions _currentOptions = CrosstalkAnalysisOptions.Default;
    private string _sourceRoot = string.Empty;
    private string _outputRoot = string.Empty;
    private string _folderName = "串扰结果";
    private string? _lastWorkbookPath;
    private string? _lastHeatmapPath;
    private bool _suppressUiEvents;
    private bool _busy;
    private readonly List<CrosstalkHistoryRecord> _history = new();

    /// <summary>Visual Studio 设计器入口。不会读取 ExportFile 或其他运行时路径。</summary>
    public CrosstalkResultForm()
        : this(CreateDesignTimeResult(), CreateDesignTimeCalculation(), designTime: true)
    {
    }

    /// <summary>兼容已有主界面的构造函数。</summary>
    public CrosstalkResultForm(TestDataProcessingResult result)
        : this(result, result.CrosstalkCalculation, designTime: false)
    {
    }

    /// <summary>接收一键测试产生的结果和可重算的 19×32 原始矩阵。</summary>
    public CrosstalkResultForm(TestDataProcessingResult result, CrosstalkCalculationResult? calculation)
        : this(result, calculation, designTime: false)
    {
    }

    /// <summary>
    /// 打开已有一键测试结果，并显式指定分析器的数据/输出目录和参数。
    /// 这使主界面可以恢复分析器上次编辑的路径，而不会把结果对象的输出目录
    /// 当成唯一数据源；整个过程仍只在 C# 数据处理层运行。
    /// </summary>
    public CrosstalkResultForm(TestDataProcessingResult result,
        CrosstalkCalculationResult? calculation,
        string? sourceRoot,
        string? outputRoot,
        CrosstalkAnalysisOptions? options = null)
        : this(result, calculation, sourceRoot, outputRoot,
            options ?? result.CrosstalkOptions ?? calculation?.AnalysisOptions,
            designTime: false)
    {
    }

    /// <summary>直接从 ExportFile 根目录加载串扰数据；计算和导出均由 C# 完成。</summary>
    public CrosstalkResultForm(string sourceRoot, string? outputRoot = null,
        CrosstalkAnalysisOptions? options = null)
        : this(null, null, sourceRoot, outputRoot, options, designTime: false)
    {
    }

    private CrosstalkResultForm(TestDataProcessingResult? result,
        CrosstalkCalculationResult? calculation, bool designTime)
        : this(result, calculation, null, null,
            result?.CrosstalkOptions ?? calculation?.AnalysisOptions, designTime)
    {
    }

    private CrosstalkResultForm(TestDataProcessingResult? result,
        CrosstalkCalculationResult? calculation, string? sourceRoot,
        string? outputRoot, CrosstalkAnalysisOptions? options, bool designTime)
    {
        _result = result;
        _baseCalculation = calculation;
        _initialSourceRoot = sourceRoot;
        _initialOutputRoot = outputRoot;
        _initialOptions = options;
        _isDesignTime = designTime || LicenseManager.UsageMode == LicenseUsageMode.Designtime;
        InitializeComponent();

        if (_isDesignTime)
        {
            InitializeDesignPreview();
            return;
        }

        Load += CrosstalkResultForm_Load;
        FormClosing += CrosstalkResultForm_FormClosing;
    }

    /// <summary>当前显示的重算结果；尚未计算时为 null。</summary>
    [Browsable(false)]
    public CrosstalkCalculationResult? CurrentCalculation => _currentCalculation;

    /// <summary>当前显示的阈值、色轴和掩膜参数。</summary>
    [Browsable(false)]
    public CrosstalkAnalysisOptions CurrentOptions => _currentOptions;

    /// <summary>当前数据源文本框值；主界面关闭分析器后用它恢复上次选择。</summary>
    [Browsable(false)]
    public string CurrentSourceRoot => (textBoxSourceRoot?.Text ?? _sourceRoot).Trim();

    /// <summary>当前结果输出目录文本框值。</summary>
    [Browsable(false)]
    public string CurrentOutputRoot => (textBoxOutputRoot?.Text ?? _outputRoot).Trim();

    /// <summary>当前坐标掩膜文本框值（1-based、含首尾）。</summary>
    [Browsable(false)]
    public string CurrentMaskCoordinates => (textBoxMaskCoordinates?.Text ?? string.Empty).Trim();

    /// <summary>当前掩膜备注文本框值。</summary>
    [Browsable(false)]
    public string CurrentMaskNote => (textBoxMaskNote?.Text ?? _currentOptions.MaskNote).Trim();

    /// <summary>嵌入主界面时可复用的热图控件；普通弹窗调用方无需使用。</summary>
    [Browsable(false)]
    public HeatmapPreviewControl HeatmapPreview => heatmapPreview;

    /// <summary>当前分析成功后触发，主界面可用来同步小型热图预览。</summary>
    public event EventHandler<CrosstalkAnalysisChangedEventArgs>? AnalysisChanged;

    /// <summary>当前显示结果发生变化时的简短通知。</summary>
    public event EventHandler? ResultChanged;

    /// <summary>让外部主界面把一个新计算结果推送到本窗口。</summary>
    public void SetCalculation(CrosstalkCalculationResult calculation,
        string? folderName = null, string? sourceRoot = null, string? outputRoot = null)
    {
        ArgumentNullException.ThrowIfNull(calculation);
        if (_isDesignTime) return;
        _baseCalculation = calculation;
        if (sourceRoot is not null) _sourceRoot = sourceRoot.Trim();
        if (outputRoot is not null) _outputRoot = outputRoot.Trim();
        if (!string.IsNullOrWhiteSpace(folderName)) _folderName = folderName.Trim();
        if (sourceRoot is not null) textBoxSourceRoot.Text = _sourceRoot;
        if (outputRoot is not null) textBoxOutputRoot.Text = _outputRoot;
        if (HasRawMatrix(calculation))
        {
            ApplyOptionsToControls(calculation.AnalysisOptions);
            RenderCalculation(RecalculateCurrent(calculation.AnalysisOptions), "已载入串扰计算结果。");
        }
        else
        {
            _currentCalculation = calculation;
            RenderResultImageAndMetrics();
            labelStatus.Text = "结果没有原始 19×32 矩阵，只能显示已保存热图和统计。";
        }
        RefreshMaskArchives();
    }

    /// <summary>重新读取界面参数并更新热图/统计，不会覆盖原始导出文件。</summary>
    public bool RefreshAnalysis()
    {
        if (_isDesignTime || _baseCalculation is null) return false;
        try
        {
            ApplyCurrentOptions(ReadOptionsFromControls(),
                "参数已应用；如需写出文件请点击“导出当前掩膜结果”。");
            return true;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or InvalidDataException)
        {
            ShowError(ex);
            return false;
        }
    }

    private static TestDataProcessingResult CreateDesignTimeResult() =>
        new("串扰（设计器示例）", TestProjectKind.Crosstalk,
            new[]
            {
                new TestMetric("crosstalk.max", "串扰最大值", "待测", "设计器示例"),
                new TestMetric("crosstalk.mean", "串扰平均值", "待测", "设计器示例")
            }, detail: "设计器示例数据，不代表实际测量结果。");

    private static CrosstalkCalculationResult CreateDesignTimeCalculation()
    {
        var raw = new double[CrosstalkDataProcessor.HeatmapRows,
            CrosstalkDataProcessor.HeatmapColumns];
        for (int row = 0; row < raw.GetLength(0); row++)
            for (int column = 0; column < raw.GetLength(1); column++)
                raw[row, column] = .002 +
                    ((row * raw.GetLength(1) + column) % 25) / 1000d;
        return CrosstalkDataProcessor.Recalculate(
            new CrosstalkCalculationResult(raw, raw, .03, .002, .01) { RawValues = raw },
            CrosstalkAnalysisOptions.Default);
    }

    private void InitializeDesignPreview()
    {
        labelSummary.Text = "设计器预览：串扰结果分析窗口（运行时显示实际指标和热图）";
        labelStatus.Text = "设计器示例；运行时可调整阈值、色轴和掩膜。";
        _currentOptions = CrosstalkAnalysisOptions.Default;
        ApplyOptionsToControls(_currentOptions);
        _currentCalculation = _baseCalculation;
        if (_currentCalculation is not null)
        {
            heatmapPreview.ColorMinimumPercent = _currentOptions.ColorAxisMinimumPercent;
            heatmapPreview.ColorMaximumPercent = _currentOptions.ColorAxisMaximumPercent;
            heatmapPreview.AnomalyThresholdPercent = _currentOptions.AbnormalThresholdPercent;
            heatmapPreview.SetValues(_currentCalculation.ValuesForHeatmap);
            heatmapPreview.SetMaskedCells(EnumerateMask(_currentCalculation.UserMask));
            UpdateStatistics(_currentCalculation);
        }
        RefreshHistoryGrid();
    }

    private void CrosstalkResultForm_Load(object? sender, EventArgs e)
    {
        if (_isDesignTime) return;
        LoadHistory();
        RefreshHistoryGrid();

        // 一键测试结果通常把原始归档放在 OutputDirectory；若调用方没有显式
        // 传入 ExportFile 根目录，使用该目录作为掩膜存档和再次导出的默认位置。
        // Keep a non-existing path in the editable field.  The user may be
        // configuring a data directory before MRTEST creates its first batch;
        // clearing it here would lose the last edited value on close.
        _sourceRoot = ResolveInitialSourceRoot(_initialSourceRoot ?? _result?.OutputDirectory);
        _outputRoot = (_initialOutputRoot ?? _result?.OutputDirectory ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(_outputRoot)) _outputRoot = _sourceRoot;
        _folderName = ResolveFolderName(_sourceRoot, _result?.ProjectName);
        textBoxSourceRoot.Text = _sourceRoot;
        textBoxOutputRoot.Text = _outputRoot;
        _currentOptions = (_initialOptions ?? _baseCalculation?.AnalysisOptions ??
            CrosstalkAnalysisOptions.Default).Normalize();
        ApplyOptionsToControls(_currentOptions);

        if (_baseCalculation is not null && HasRawMatrix(_baseCalculation))
        {
            try { RenderCalculation(RecalculateCurrent(_currentOptions),
                "已载入一键测试结果；可调整参数并重新分析。", addHistory: false); }
            catch (Exception ex) { ShowError(ex); }
        }
        else RenderResultImageAndMetrics();

        RefreshMaskArchives();
        // Selecting a source directory must not start a potentially expensive
        // scan/export implicitly.  ExportFile can contain many historical
        // batches, and the reference workflow requires an explicit Compute
        // click.  Keep the path ready and let the user decide when to read it.
        if (_baseCalculation is null && _result is null)
            labelStatus.Text = "已选择数据目录；点击“计算并绘图”开始 C# 分析。";
    }

    private void CrosstalkResultForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        // Persist the latest valid text-box values even when the user closes
        // the window before a matrix has been loaded.  This keeps the next
        // session's threshold/color-axis defaults consistent with the last
        // edit, matching the application's other editable settings.
        if (!_isDesignTime && TryReadOptionsFromControls(out CrosstalkAnalysisOptions? options))
        {
            _currentOptions = options!;
            // Text-box edits are also valid when the analyzer is closed before
            // clicking Apply.  Recalculate once so the embedding DashboardForm
            // receives exactly the same mask/threshold state that is persisted.
            if (_baseCalculation is not null && HasRawMatrix(_baseCalculation))
            {
                try { _currentCalculation = RecalculateCurrent(_currentOptions); }
                catch (Exception) { /* keep the last successfully rendered result */ }
            }
        }
        if (!_isDesignTime) SaveHistory();
    }

    private async void ButtonCompute_Click(object? sender, EventArgs e) => await ComputeFromFolderAsync();

    private async Task ComputeFromFolderAsync()
    {
        if (_busy || _isDesignTime) return;
        string source = textBoxSourceRoot.Text.Trim();
        if (string.IsNullOrWhiteSpace(source))
        {
            ShowError(new InvalidDataException("请先选择串扰数据文件夹。"));
            return;
        }
        try
        {
            CrosstalkAnalysisOptions options = ReadOptionsFromControls();
            SetBusy(true);
            labelStatus.Text = "正在用 C# 读取 Brightness!C 列并计算 19×32 串扰矩阵…";
            string output = ResolveOutputRoot();
            CrosstalkExportResult exported = await Task.Run(() =>
                CrosstalkDataProcessor.ExportCurrentFolderResult(source, output, options));
            _sourceRoot = source;
            _outputRoot = output;
            _folderName = ResolveFolderName(source, null);
            _baseCalculation = exported.Calculation;
            _lastWorkbookPath = exported.CrosstalkWorkbookPath;
            _lastHeatmapPath = exported.HeatmapPath;
            RenderCalculation(exported.Calculation,
                $"计算完成：已写出 {Path.GetFileName(exported.CrosstalkWorkbookPath)} 和热图。",
                addHistory: false);
            RefreshMaskArchives();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            InvalidDataException or FormatException or ArgumentException or OverflowException)
        {
            ShowError(ex);
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
        finally { SetBusy(false); }
    }

    private void ButtonApplyOptions_Click(object? sender, EventArgs e) => RefreshAnalysis();

    private void ButtonResetOptions_Click(object? sender, EventArgs e)
    {
        if (_isDesignTime) return;
        ApplyOptionsToControls(CrosstalkAnalysisOptions.Default with { UserMask = CurrentMaskClone() });
        RefreshAnalysis();
    }

    private void TextBoxOption_TextChanged(object? sender, EventArgs e)
    {
        if (_suppressUiEvents || _baseCalculation is null || _busy) return;
        if (TryReadOptionsFromControls(out CrosstalkAnalysisOptions? options))
        {
            try { ApplyCurrentOptions(options!, "参数已实时应用。"); }
            catch (Exception) { /* 输入中间态，等用户完成输入 */ }
        }
    }

    private void ButtonApplyMask_Click(object? sender, EventArgs e)
    {
        if (_baseCalculation is null)
        {
            ShowError(new InvalidDataException("请先计算或载入串扰结果。"));
            return;
        }
        try
        {
            bool[,] mask = CrosstalkMaskStore.ParseCoordinateRanges(textBoxMaskCoordinates.Text,
                CrosstalkDataProcessor.HeatmapRows, CrosstalkDataProcessor.HeatmapColumns);
            ApplyUserMask(mask, "坐标掩膜已应用。", preserveCoordinates: true);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException) { ShowError(ex); }
    }

    private void ButtonClearMask_Click(object? sender, EventArgs e)
    {
        if (_baseCalculation is null) return;
        ApplyUserMask(new bool[CrosstalkDataProcessor.HeatmapRows,
            CrosstalkDataProcessor.HeatmapColumns], "用户掩膜已清空。", false);
        textBoxMaskCoordinates.Clear();
        textBoxMaskName.Clear();
        textBoxMaskNote.Clear();
    }

    private void HeatmapPreview_MaskSelected(object? sender, HeatmapMaskSelectedEventArgs e)
    {
        if (_baseCalculation is null || _suppressUiEvents) return;
        try
        {
            bool[,] mask = CurrentMaskClone() ?? new bool[CrosstalkDataProcessor.HeatmapRows,
                CrosstalkDataProcessor.HeatmapColumns];
            // 文本格式为 x,y（列,行）；拖拽事件同时提供行列坐标。
            CrosstalkMaskStore.AddRectangle(mask, e.ColumnStart, e.RowStart,
                e.ColumnEnd, e.RowEnd);
            textBoxMaskCoordinates.Text = AppendCoordinateRange(textBoxMaskCoordinates.Text,
                $"{e.ColumnStart},{e.RowStart}-{e.ColumnEnd},{e.RowEnd}");
            ApplyUserMask(mask, "已将拖拽区域加入用户掩膜。", true);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException) { ShowError(ex); }
    }

    private void ButtonSaveMask_Click(object? sender, EventArgs e)
    {
        if (_baseCalculation is null)
        {
            ShowError(new InvalidDataException("请先计算或载入串扰结果。"));
            return;
        }
        string name = textBoxMaskName.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            ShowError(new InvalidDataException("请输入掩膜存档名称。"));
            textBoxMaskName.Focus();
            return;
        }
        string archivePath = GetArchivePath();
        if (string.IsNullOrWhiteSpace(archivePath)) return;
        try
        {
            // Parse the editable coordinate text before saving.  This keeps
            // the archive in sync even when the user skipped the separate
            // “应用坐标” button.
            CrosstalkAnalysisOptions options = ReadOptionsFromControls();
            ApplyCurrentOptions(options, "坐标掩膜已应用，正在保存存档…");
            string sourceDirectory = Path.GetDirectoryName(archivePath) ?? string.Empty;
            if (!Directory.Exists(sourceDirectory))
                throw new DirectoryNotFoundException($"串扰数据文件夹不存在：{sourceDirectory}");
            CrosstalkMaskStore.Save(archivePath, name, textBoxMaskNote.Text,
                options.UserMask ?? new bool[CrosstalkDataProcessor.HeatmapRows,
                    CrosstalkDataProcessor.HeatmapColumns]);
            RefreshMaskArchives();
            comboMaskArchives.SelectedItem = name;
            _currentOptions = options with { MaskName = name, MaskNote = textBoxMaskNote.Text.Trim() };
            labelStatus.Text = $"掩膜“{name}”已保存到 {archivePath}。";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            InvalidDataException or ArgumentException or JsonException) { ShowError(ex); }
    }

    private void ButtonLoadMask_Click(object? sender, EventArgs e)
    {
        if (_baseCalculation is null || comboMaskArchives.SelectedItem is not string name ||
            string.IsNullOrWhiteSpace(name)) return;
        try
        {
            string archivePath = GetArchivePath();
            if (string.IsNullOrWhiteSpace(archivePath)) return;
            var records = CrosstalkMaskStore.Load(archivePath);
            if (!records.TryGetValue(name, out CrosstalkMaskArchive? archive))
                throw new InvalidDataException($"找不到掩膜存档“{name}”。");
            bool[,] mask = CrosstalkMaskStore.Restore(archive,
                CrosstalkDataProcessor.HeatmapRows, CrosstalkDataProcessor.HeatmapColumns);
            textBoxMaskName.Text = archive.Name;
            textBoxMaskNote.Text = archive.Note;
            textBoxMaskCoordinates.Text = BuildCoordinateSummary(mask);
            ApplyUserMask(mask, $"已载入掩膜“{archive.Name}”。", false, archive.Name, archive.Note);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            InvalidDataException or ArgumentException or JsonException) { ShowError(ex); }
    }

    private void ButtonDeleteMask_Click(object? sender, EventArgs e)
    {
        if (comboMaskArchives.SelectedItem is not string name || string.IsNullOrWhiteSpace(name)) return;
        if (MessageBox.Show(this, $"确定删除掩膜存档“{name}”吗？", "删除掩膜",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        try
        {
            string archivePath = GetArchivePath();
            if (string.IsNullOrWhiteSpace(archivePath)) return;
            CrosstalkMaskStore.Delete(archivePath, name);
            RefreshMaskArchives();
            labelStatus.Text = $"已删除掩膜存档“{name}”。";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            InvalidDataException or JsonException)
        { ShowError(ex); }
    }

    private void ButtonExportCurrent_Click(object? sender, EventArgs e)
    {
        if (_currentCalculation is null)
        {
            ShowError(new InvalidDataException("请先计算或载入串扰结果。"));
            return;
        }
        try
        {
            CrosstalkAnalysisOptions options = ReadOptionsFromControls();
            CrosstalkExportResult exported = CrosstalkDataProcessor.ExportCurrentResult(
                ResolveOutputRoot(), _folderName, _currentCalculation, options);
            _lastWorkbookPath = exported.CrosstalkWorkbookPath;
            _lastHeatmapPath = exported.HeatmapPath;
            _baseCalculation = CrosstalkDataProcessor.Recalculate(_currentCalculation, options);
            RenderCalculation(_baseCalculation,
                $"当前掩膜结果已独立导出：{Path.GetFileName(exported.CrosstalkWorkbookPath)}", false);
            MessageBox.Show(this, $"已生成独立结果文件：{Environment.NewLine}{exported.CrosstalkWorkbookPath}{Environment.NewLine}{exported.HeatmapPath}",
                "导出完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            InvalidDataException or ArgumentException or JsonException) { ShowError(ex); }
    }

    private void ButtonRecordHistory_Click(object? sender, EventArgs e)
    {
        if (_currentCalculation is null) return;
        RecordCurrentResultInternal();
        labelStatus.Text = "当前 Max / Min / Mean 已加入历史记录。";
    }

    private void RecordCurrentResultInternal()
    {
        if (_currentCalculation is null) return;
        CrosstalkCalculationResult calculation = _currentCalculation;
        _history.Add(new CrosstalkHistoryRecord
        {
            Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            Folder = _folderName,
            Mask = string.IsNullOrWhiteSpace(_currentOptions.MaskName) ? "自定义/未命名" : _currentOptions.MaskName,
            MaskNote = _currentOptions.MaskNote,
            Maximum = calculation.Maximum,
            Minimum = calculation.Minimum,
            Mean = calculation.Mean,
            OutputDirectory = ResolveOutputRoot(),
            HeatmapPath = _lastHeatmapPath ?? string.Empty,
            SourceRoot = _sourceRoot,
            ThresholdRatio = _currentOptions.AbnormalThresholdRatio,
            ColorMinimumPercent = _currentOptions.ColorAxisMinimumPercent,
            ColorMaximumPercent = _currentOptions.ColorAxisMaximumPercent,
            RawValues = Flatten(calculation.RawValues),
            UserMask = FlattenMask(calculation.UserMask)
        });
        SaveHistory();
        RefreshHistoryGrid();
    }

    private void ButtonDeleteHistory_Click(object? sender, EventArgs e)
    {
        var rows = gridHistory.SelectedRows.Cast<DataGridViewRow>().Select(row => row.Index)
            .Where(index => index >= 0).Distinct().OrderByDescending(index => index).ToArray();
        if (rows.Length == 0) return;
        foreach (int index in rows) if (index < _history.Count) _history.RemoveAt(index);
        SaveHistory();
        RefreshHistoryGrid();
        labelStatus.Text = $"已删除 {rows.Length} 条历史记录。";
    }

    private void ButtonClearHistory_Click(object? sender, EventArgs e)
    {
        if (_history.Count == 0) return;
        if (MessageBox.Show(this, "确定清空全部历史统计结果吗？", "清空历史",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        _history.Clear();
        SaveHistory();
        RefreshHistoryGrid();
        labelStatus.Text = "历史统计结果已全部清空。";
    }

    private void ButtonCopyHistory_Click(object? sender, EventArgs e)
    {
        try
        {
            var builder = new StringBuilder();
            builder.AppendLine("时间\t文件夹\t掩膜\t最大值(%)\t最小值(%)\t平均值(%)");
            foreach (CrosstalkHistoryRecord item in _history)
                builder.Append(item.Time).Append('\t').Append(item.Folder).Append('\t').Append(item.Mask)
                    .Append('\t').Append(Percent(item.Maximum)).Append('\t').Append(Percent(item.Minimum))
                    .Append('\t').AppendLine(Percent(item.Mean));
            Clipboard.SetText(builder.ToString());
            labelStatus.Text = $"已复制 {_history.Count} 条历史记录，可直接粘贴到 Excel。";
        }
        catch (Exception ex) when (ex is ExternalException or InvalidOperationException)
        { labelStatus.Text = $"复制历史记录失败：{ex.Message}"; }
    }

    private void GridHistory_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _history.Count) return;
        CrosstalkHistoryRecord item = _history[e.RowIndex];
        try
        {
            _lastHeatmapPath = string.IsNullOrWhiteSpace(item.HeatmapPath)
                ? _lastHeatmapPath : item.HeatmapPath;
            if (item.RawValues is { Length: 608 })
            {
                double[,] raw = Expand(item.RawValues, CrosstalkDataProcessor.HeatmapRows,
                    CrosstalkDataProcessor.HeatmapColumns);
                _baseCalculation = new CrosstalkCalculationResult(raw, raw, 0, 0, 0) { RawValues = raw };
                _sourceRoot = item.SourceRoot ?? string.Empty;
                _outputRoot = item.OutputDirectory ?? string.Empty;
                _folderName = item.Folder ?? "串扰结果";
                _currentOptions = new CrosstalkAnalysisOptions
                {
                    AbnormalThresholdRatio = item.ThresholdRatio,
                    ColorAxisMinimumPercent = item.ColorMinimumPercent,
                    ColorAxisMaximumPercent = item.ColorMaximumPercent,
                    MaskName = item.Mask == "自定义/未命名" ? string.Empty : item.Mask,
                    MaskNote = item.MaskNote ?? string.Empty,
                    UserMask = item.UserMask is { Length: 608 }
                        ? ExpandMask(item.UserMask, CrosstalkDataProcessor.HeatmapRows,
                            CrosstalkDataProcessor.HeatmapColumns)
                        : null
                };
                // Keep the editable path and note fields in sync with the
                // selected history row.  Without this update, closing the
                // analyzer after a history double-click could persist the
                // previous batch's paths and mask note back to the host.
                textBoxSourceRoot.Text = _sourceRoot;
                textBoxOutputRoot.Text = _outputRoot;
                textBoxMaskNote.Text = item.MaskNote ?? string.Empty;
                textBoxMaskName.Text = _currentOptions.MaskName;
                textBoxMaskCoordinates.Text = _currentOptions.UserMask is null
                    ? string.Empty : BuildCoordinateSummary(_currentOptions.UserMask);
                ApplyOptionsToControls(_currentOptions);
                RenderCalculation(RecalculateCurrent(_currentOptions),
                    "已载入历史记录；双击其他行可切换。", false);
            }
            else if (!string.IsNullOrWhiteSpace(item.HeatmapPath) && File.Exists(item.HeatmapPath))
            {
                using Image image = Image.FromFile(item.HeatmapPath);
                heatmapPreview.SetImage(image);
                labelStatus.Text = "历史记录没有原始矩阵，仅显示已保存热图。";
            }
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or InvalidDataException or JsonException)
        { ShowError(ex); }
    }

    private void ButtonOpenFolder_Click(object? sender, EventArgs e)
    {
        string folder = ResolveOutputRoot();
        if (Directory.Exists(folder))
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
    }

    private void ButtonOpenImage_Click(object? sender, EventArgs e)
    {
        string? path = _lastHeatmapPath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) path = _result?.HeatmapPath;
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private void ButtonBrowseSource_Click(object? sender, EventArgs e) => BrowseFolder(textBoxSourceRoot);
    private void ButtonBrowseOutput_Click(object? sender, EventArgs e) => BrowseFolder(textBoxOutputRoot);

    private void BrowseFolder(TextBox target)
    {
        using var dialog = new FolderBrowserDialog { SelectedPath = target.Text.Trim() };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        target.Text = dialog.SelectedPath;
        if (ReferenceEquals(target, textBoxSourceRoot))
        {
            _sourceRoot = dialog.SelectedPath;
            _folderName = ResolveFolderName(_sourceRoot, null);
            // 更换数据根目录后，不把上一批数据的坐标掩膜误套用到新矩阵。
            _baseCalculation = null;
            _currentCalculation = null;
            _currentOptions = _currentOptions with { UserMask = null, MaskName = string.Empty, MaskNote = string.Empty };
            textBoxMaskCoordinates.Clear();
            heatmapPreview.SetValues(null);
            RefreshMaskArchives();
        }
    }

    private void ComboMaskArchives_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_suppressUiEvents || comboMaskArchives.SelectedItem is not string name) return;
        try
        {
            string path = GetArchivePath();
            if (string.IsNullOrWhiteSpace(path)) return;
            if (CrosstalkMaskStore.Load(path).TryGetValue(name, out CrosstalkMaskArchive? archive))
            {
                textBoxMaskName.Text = archive.Name;
                textBoxMaskNote.Text = archive.Note;
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException)
        { labelStatus.Text = $"读取掩膜存档失败：{ex.Message}"; }
    }

    private void ApplyUserMask(bool[,] mask, string status, bool preserveCoordinates,
        string? maskName = null, string? maskNote = null)
    {
        if (_baseCalculation is null) return;
        _currentOptions = _currentOptions with
        {
            UserMask = mask,
            MaskName = maskName ?? (preserveCoordinates ? _currentOptions.MaskName : string.Empty),
            MaskNote = maskNote ?? (preserveCoordinates ? _currentOptions.MaskNote : string.Empty)
        };
        ApplyOptionsToControls(_currentOptions, updateMaskText: false);
        ApplyCurrentOptions(_currentOptions, status);
    }

    private void ApplyCurrentOptions(CrosstalkAnalysisOptions options, string status)
    {
        if (_baseCalculation is null) return;
        options = options.Normalize();
        _currentOptions = options;
        CrosstalkCalculationResult calculation = RecalculateCurrent(options);
        _currentCalculation = calculation;
        heatmapPreview.ColorMinimumPercent = options.ColorAxisMinimumPercent;
        heatmapPreview.ColorMaximumPercent = options.ColorAxisMaximumPercent;
        heatmapPreview.AnomalyThresholdPercent = options.AbnormalThresholdPercent;
        _suppressUiEvents = true;
        try
        {
            heatmapPreview.SetValues(calculation.ValuesForHeatmap);
            heatmapPreview.SetMaskedCells(EnumerateMask(calculation.UserMask));
        }
        finally { _suppressUiEvents = false; }
        UpdateStatistics(calculation);
        labelStatus.Text = status;
        AnalysisChanged?.Invoke(this, new CrosstalkAnalysisChangedEventArgs(calculation, options));
        ResultChanged?.Invoke(this, EventArgs.Empty);
    }

    private CrosstalkCalculationResult RecalculateCurrent(CrosstalkAnalysisOptions options)
    {
        if (_baseCalculation is null) throw new InvalidDataException("尚未载入串扰原始矩阵。");
        return CrosstalkDataProcessor.Recalculate(_baseCalculation, options);
    }

    private void RenderCalculation(CrosstalkCalculationResult calculation, string status,
        bool addHistory = false)
    {
        _currentCalculation = calculation;
        _currentOptions = calculation.AnalysisOptions;
        heatmapPreview.ColorMinimumPercent = _currentOptions.ColorAxisMinimumPercent;
        heatmapPreview.ColorMaximumPercent = _currentOptions.ColorAxisMaximumPercent;
        heatmapPreview.AnomalyThresholdPercent = _currentOptions.AbnormalThresholdPercent;
        _suppressUiEvents = true;
        try
        {
            heatmapPreview.SetValues(calculation.ValuesForHeatmap);
            heatmapPreview.SetMaskedCells(EnumerateMask(calculation.UserMask));
        }
        finally { _suppressUiEvents = false; }
        ApplyOptionsToControls(_currentOptions);
        UpdateStatistics(calculation);
        labelSummary.Text = BuildSummary(calculation);
        labelStatus.Text = status;
        if (addHistory) RecordCurrentResultInternal();
        AnalysisChanged?.Invoke(this, new CrosstalkAnalysisChangedEventArgs(calculation, _currentOptions));
        ResultChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RenderResultImageAndMetrics()
    {
        if (_result is null) return;
        labelSummary.Text = string.Join("    ",
            _result.Metrics.Select(metric => $"{metric.DisplayName}={metric.Value}"));
        labelStatus.Text = _result.Detail ?? "已载入测试结果。";
        gridMetrics.Rows.Clear();
        foreach (TestMetric metric in _result.Metrics)
            gridMetrics.Rows.Add(metric.DisplayName, metric.Value, metric.Source, metric.Status);
        string? path = _result.HeatmapPath;
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            if (!heatmapPreview.TryLoadImage(path, out string? error) && error is not null)
                labelStatus.Text += $" 热图读取失败：{error}";
            _lastHeatmapPath = path;
        }
    }

    private void UpdateStatistics(CrosstalkCalculationResult calculation)
    {
        labelStats.Text = $"最大值 {Percent(calculation.Maximum)}    最小值 {Percent(calculation.Minimum)}    " +
            $"均值 {Percent(calculation.Mean)}    有效点 {calculation.ValidPointCount}    " +
            $"异常点 {calculation.AbnormalPointCount}    掩膜 {CrosstalkMaskStore.Count(calculation.UserMask)}";
        gridMetrics.Rows.Clear();
        gridMetrics.Rows.Add("串扰最大值", Percent(calculation.Maximum), "Brightness C列 / C#等价算法", "已计算");
        gridMetrics.Rows.Add("串扰最小值", Percent(calculation.Minimum), "Brightness C列 / C#等价算法", "已计算");
        gridMetrics.Rows.Add("串扰平均值", Percent(calculation.Mean), "Brightness C列 / C#等价算法", "已计算");
        gridMetrics.Rows.Add("异常点数量", calculation.AbnormalPointCount.ToString(CultureInfo.InvariantCulture),
            $"> {_currentOptions.AbnormalThresholdPercent:0.###}%（边框/掩膜除外）", "已计算");
    }

    private string BuildSummary(CrosstalkCalculationResult calculation) =>
        $"串扰结果：{_folderName}    阈值 > {_currentOptions.AbnormalThresholdPercent:0.###}%    " +
        $"色轴 {_currentOptions.ColorAxisMinimumPercent:0.###}～{_currentOptions.ColorAxisMaximumPercent:0.###}%";

    private CrosstalkAnalysisOptions ReadOptionsFromControls()
    {
        if (!TryReadOptionsFromControls(out CrosstalkAnalysisOptions? options, out string? error))
            throw new FormatException(error ?? "串扰参数格式无效。");
        return options!;
    }

    private bool TryReadOptionsFromControls(out CrosstalkAnalysisOptions? options) =>
        TryReadOptionsFromControls(out options, out _);

    private bool TryReadOptionsFromControls(out CrosstalkAnalysisOptions? options, out string? error)
    {
        options = null;
        error = null;
        if (!TryParseDouble(textBoxColorMinimum.Text, out double minimum) ||
            !TryParseDouble(textBoxColorMaximum.Text, out double maximum) ||
            !TryParseThresholdRatio(textBoxThreshold.Text, out double threshold))
        {
            error = "色轴上下限和异常阈值必须是数字；阈值可输入比例（0.03）或百分号（3%）。";
            return false;
        }
        try
        {
            // The coordinate editor is the source of truth for a user mask.
            // Read it even before the first computation (the matrix dimensions
            // are fixed by the analyzer), so a mask typed in advance is not
            // silently discarded by the Compute button.  An empty editor with
            // no matrix keeps the initial null state for a clean designer
            // preview; with a matrix it means an intentionally empty mask.
            bool[,]? userMask = string.IsNullOrWhiteSpace(textBoxMaskCoordinates.Text) &&
                                (_baseCalculation is null || !HasRawMatrix(_baseCalculation))
                ? CurrentMaskClone()
                : CrosstalkMaskStore.ParseCoordinateRanges(
                    textBoxMaskCoordinates.Text,
                    CrosstalkDataProcessor.HeatmapRows,
                    CrosstalkDataProcessor.HeatmapColumns);
            options = new CrosstalkAnalysisOptions
            {
                ColorAxisMinimumPercent = minimum,
                ColorAxisMaximumPercent = maximum,
                AbnormalThresholdRatio = threshold,
                UserMask = userMask,
                MaskName = textBoxMaskName.Text.Trim(),
                MaskNote = textBoxMaskNote.Text.Trim()
            }.Normalize();
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        { error = ex.Message; return false; }
    }

    private void ApplyOptionsToControls(CrosstalkAnalysisOptions options, bool updateMaskText = true)
    {
        _suppressUiEvents = true;
        try
        {
            textBoxColorMinimum.Text = FormatNumber(options.ColorAxisMinimumPercent);
            textBoxColorMaximum.Text = FormatNumber(options.ColorAxisMaximumPercent);
            textBoxThreshold.Text = FormatNumber(options.AbnormalThresholdRatio);
            if (updateMaskText)
            {
                textBoxMaskName.Text = options.MaskName;
                textBoxMaskNote.Text = options.MaskNote;
                textBoxMaskCoordinates.Text = options.UserMask is null
                    ? string.Empty
                    : BuildCoordinateSummary(options.UserMask);
            }
        }
        finally { _suppressUiEvents = false; }
    }

    private bool[,]? CurrentMaskClone() => _currentCalculation?.UserMask is { Length: > 0 } mask
        ? (bool[,])mask.Clone()
        : _baseCalculation?.UserMask is { Length: > 0 } baseMask ? (bool[,])baseMask.Clone() : null;

    private void RefreshMaskArchives()
    {
        _suppressUiEvents = true;
        try
        {
            comboMaskArchives.Items.Clear();
            string path = GetArchivePath(silent: true);
            if (string.IsNullOrWhiteSpace(path))
            {
                labelMaskCountHint.Text = "0 个存档";
                return;
            }
            IReadOnlyDictionary<string, CrosstalkMaskArchive> records = CrosstalkMaskStore.Load(path);
            foreach (string name in records.Keys.OrderBy(item => item,
                StringComparer.CurrentCultureIgnoreCase)) comboMaskArchives.Items.Add(name);
            labelMaskCountHint.Text = $"{records.Count} 个存档";
            if (!string.IsNullOrWhiteSpace(_currentOptions.MaskName))
                comboMaskArchives.SelectedItem = _currentOptions.MaskName;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            InvalidDataException or JsonException)
        { labelStatus.Text = $"读取掩膜存档失败：{ex.Message}"; }
        finally { _suppressUiEvents = false; }
    }

    private string GetArchivePath(bool silent = false)
    {
        string source = textBoxSourceRoot.Text.Trim();
        if (string.IsNullOrWhiteSpace(source)) source = _sourceRoot;
        if (string.IsNullOrWhiteSpace(source))
        {
            if (!silent) ShowError(new InvalidDataException("请先选择串扰数据文件夹，掩膜存档将保存到该文件夹。"));
            return string.Empty;
        }
        return Path.Combine(source, ".crosstalk_masks.json");
    }

    private string ResolveOutputRoot()
    {
        string output = textBoxOutputRoot.Text.Trim();
        if (string.IsNullOrWhiteSpace(output)) output = textBoxSourceRoot.Text.Trim();
        if (string.IsNullOrWhiteSpace(output)) output = _outputRoot;
        return string.IsNullOrWhiteSpace(output)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AutoTestResults")
            : output;
    }

    private void LoadHistory()
    {
        try
        {
            if (!File.Exists(HistoryFilePath)) return;
            var records = JsonSerializer.Deserialize<List<CrosstalkHistoryRecord>>(
                File.ReadAllText(HistoryFilePath), JsonOptions);
            if (records is not null) _history.AddRange(records);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        { labelStatus.Text = $"历史记录读取失败：{ex.Message}"; }
    }

    private void SaveHistory()
    {
        try
        {
            string? directory = Path.GetDirectoryName(HistoryFilePath);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            string temporary = HistoryFilePath + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(_history, JsonOptions));
            File.Move(temporary, HistoryFilePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            JsonException or ArgumentException)
        { labelStatus.Text = $"历史记录保存失败：{ex.Message}"; }
    }

    private void RefreshHistoryGrid()
    {
        if (gridHistory is null) return;
        gridHistory.Rows.Clear();
        foreach (CrosstalkHistoryRecord item in _history)
            gridHistory.Rows.Add(item.Time, item.Folder, item.Mask, Percent(item.Maximum),
                Percent(item.Minimum), Percent(item.Mean));
    }

    private string HistoryFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AutoTestClient", "crosstalk-history.json");

    private void SetBusy(bool busy)
    {
        _busy = busy;
        buttonCompute.Enabled = !busy;
        buttonApplyOptions.Enabled = !busy;
        buttonExportCurrent.Enabled = !busy;
        Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
    }

    private void ShowError(Exception ex)
    {
        labelStatus.Text = $"错误：{ex.Message}";
        if (!_isDesignTime && !IsDisposed)
            MessageBox.Show(this, ex.Message, "串扰分析", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private static IEnumerable<(int Row, int Column)> EnumerateMask(bool[,]? mask)
    {
        if (mask is null) yield break;
        for (int row = 0; row < mask.GetLength(0); row++)
            for (int column = 0; column < mask.GetLength(1); column++)
                if (mask[row, column]) yield return (row, column);
    }

    private static string AppendCoordinateRange(string existing, string range) =>
        string.IsNullOrWhiteSpace(existing) ? range : existing.Trim() + "; " + range;

    /// <summary>把 0-based 掩膜压缩为可再次粘贴到坐标框的 1-based 行区间。</summary>
    private static string BuildCoordinateSummary(bool[,] mask)
    {
        var ranges = new List<string>();
        for (int row = 0; row < mask.GetLength(0); row++)
        {
            int column = 0;
            while (column < mask.GetLength(1))
            {
                if (!mask[row, column]) { column++; continue; }
                int start = column;
                while (column + 1 < mask.GetLength(1) && mask[row, column + 1]) column++;
                ranges.Add($"{start + 1},{row + 1}-{column + 1},{row + 1}");
                column++;
            }
        }
        return string.Join("; ", ranges);
    }

    private static bool TryParseDouble(string text, out double value) =>
        double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out value) ||
        double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    private static bool TryParseThresholdRatio(string text, out double value)
    {
        string trimmed = text.Trim();
        bool percent = trimmed.EndsWith('%');
        if (percent) trimmed = trimmed[..^1].Trim();
        if (!TryParseDouble(trimmed, out double parsed)) { value = 0; return false; }
        value = percent ? parsed / 100d : parsed;
        return double.IsFinite(value);
    }

    private static string FormatNumber(double value) => value.ToString("0.############", CultureInfo.InvariantCulture);
    private static string Percent(double value) => double.IsFinite(value) ? $"{value * 100d:0.###}%" : "—";

    private static string ResolveFolderName(string? sourceRoot, string? fallback)
    {
        if (!string.IsNullOrWhiteSpace(sourceRoot))
        {
            string trimmed = sourceRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string name = Path.GetFileName(trimmed);
            if (!string.IsNullOrWhiteSpace(name)) return name;
        }
        return string.IsNullOrWhiteSpace(fallback) ? "串扰结果" : fallback.Trim();
    }

    private static string ResolveInitialSourceRoot(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate)) return string.Empty;
        string root = candidate.Trim();
        // Automatic tests archive the selected ExportFile folders below an
        // `原始数据` child.  Pointing the analyzer there makes the Compute
        // button usable immediately instead of asking the user to navigate
        // into the archive manually.
        string archived = Path.Combine(root, "原始数据");
        if (Directory.Exists(archived)) return archived;
        return root;
    }

    private static bool HasRawMatrix(CrosstalkCalculationResult calculation) =>
        calculation.RawValues.GetLength(0) == CrosstalkDataProcessor.HeatmapRows &&
        calculation.RawValues.GetLength(1) == CrosstalkDataProcessor.HeatmapColumns;

    private static double[] Flatten(double[,] values)
    {
        if (values.GetLength(0) != CrosstalkDataProcessor.HeatmapRows ||
            values.GetLength(1) != CrosstalkDataProcessor.HeatmapColumns) return Array.Empty<double>();
        var result = new double[values.Length];
        int index = 0;
        for (int row = 0; row < values.GetLength(0); row++)
            for (int column = 0; column < values.GetLength(1); column++) result[index++] = values[row, column];
        return result;
    }

    private static double[,] Expand(double[] values, int rows, int columns)
    {
        var result = new double[rows, columns];
        int index = 0;
        for (int row = 0; row < rows; row++)
            for (int column = 0; column < columns; column++) result[row, column] = values[index++];
        return result;
    }

    private static bool[] FlattenMask(bool[,]? mask)
    {
        if (mask is null || mask.GetLength(0) != CrosstalkDataProcessor.HeatmapRows ||
            mask.GetLength(1) != CrosstalkDataProcessor.HeatmapColumns)
            return Array.Empty<bool>();
        var result = new bool[mask.Length];
        int index = 0;
        for (int row = 0; row < mask.GetLength(0); row++)
            for (int column = 0; column < mask.GetLength(1); column++) result[index++] = mask[row, column];
        return result;
    }

    private static bool[,] ExpandMask(bool[] values, int rows, int columns)
    {
        var result = new bool[rows, columns];
        int index = 0;
        for (int row = 0; row < rows; row++)
            for (int column = 0; column < columns; column++) result[row, column] = values[index++];
        return result;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
    };

    private sealed class CrosstalkHistoryRecord
    {
        public string Time { get; set; } = string.Empty;
        public string Folder { get; set; } = string.Empty;
        public string Mask { get; set; } = string.Empty;
        public string MaskNote { get; set; } = string.Empty;
        public double Maximum { get; set; }
        public double Minimum { get; set; }
        public double Mean { get; set; }
        public string OutputDirectory { get; set; } = string.Empty;
        public string HeatmapPath { get; set; } = string.Empty;
        public string SourceRoot { get; set; } = string.Empty;
        public double ThresholdRatio { get; set; } = CrosstalkAnalysisOptions.DefaultAbnormalThresholdRatio;
        public double ColorMinimumPercent { get; set; } = CrosstalkAnalysisOptions.DefaultColorAxisMinimumPercent;
        public double ColorMaximumPercent { get; set; } = CrosstalkAnalysisOptions.DefaultColorAxisMaximumPercent;
        public double[] RawValues { get; set; } = Array.Empty<double>();
        public bool[] UserMask { get; set; } = Array.Empty<bool>();
    }
}

/// <summary>主界面同步串扰预览时使用的事件参数。</summary>
public sealed class CrosstalkAnalysisChangedEventArgs : EventArgs
{
    public CrosstalkAnalysisChangedEventArgs(CrosstalkCalculationResult calculation,
        CrosstalkAnalysisOptions options)
    {
        Calculation = calculation;
        Options = options;
    }

    public CrosstalkCalculationResult Calculation { get; }
    public CrosstalkAnalysisOptions Options { get; }
}
