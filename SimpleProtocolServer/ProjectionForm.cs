// 投影窗体负责图片列表、Windows 投图和串扰批量流程。
// 它不直接操作 TCP，而是通过构造函数传入的回调请 MainForm 发送并等待设备完成。
using SimpleProtocolServer.Projection;

namespace SimpleProtocolServer;

/// <summary>
/// 图片投影界面：支持手动投图、定时轮播，以及“每张图片触发一次设备测试”的串扰流程。
/// </summary>
public partial class ProjectionForm : Form
{
    // 只把这些扩展名当作可投放图片，比较时忽略大小写。
    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".png", ".bmp", ".jpg", ".jpeg" };

    // Windows 投影拓扑的具体调用由服务类负责；图片由第二屏窗体显示。
    private readonly IDesktopDisplayService _desktopDisplay = new DesktopDisplayService();
    // 窗体关闭后取消所有等待、延时和批量测试。
    private readonly CancellationTokenSource _formCancellation = new();
    // 回调由 MainForm 传入：返回 true 代表报文发送且设备最终确认成功。
    private readonly Func<CancellationToken, Task<bool>>? _sendCurrentMessageAsync;
    // 当前目录中按文件名排序后的图片完整路径。
    private List<string> _imageFiles = [];
    // 单独控制“串扰测试”批次的停止，不必关闭整个窗口。
    private CancellationTokenSource? _crosstalkCancellation;
    // 当前实际已经投到桌面的图片下标；-1 表示尚未投过图片。
    private int _currentImageIndex = -1;
    // 真正铺满第二屏幕的无边框窗口；它与本控制窗口没有 Owner 关系。
    private SecondScreenProjectionForm? _secondScreenProjectionForm;
    // 状态标志用于避免重复释放，以及统一控制界面按钮是否可用。
    private bool _formCancellationDisposed;
    private bool _isCrosstalkRunning;
    private bool _isTimedProjectionRunning;

    /// <summary>供 WinForms 设计器和烟雾测试使用的无参数构造函数。</summary>
    public ProjectionForm()
    {
        InitializeComponent();
        if (cmbTopology.Items.Count > 4) cmbTopology.SelectedIndex = 4;
        if (cmbImageTransform.Items.Count > 0) cmbImageTransform.SelectedIndex = 0;
    }

    /// <summary>
    /// 正常运行时由 MainForm 调用，并传入“发送报文且等待最终返回”的异步方法。
    /// </summary>
    internal ProjectionForm(Func<CancellationToken, Task<bool>> sendCurrentMessageAsync)
        : this()
    {
        _sendCurrentMessageAsync = sendCurrentMessageAsync ??
            throw new ArgumentNullException(nameof(sendCurrentMessageAsync));
    }

    /// <summary>首次打开窗口时使用“图片”目录，并加载图片列表。</summary>
    private void ProjectionForm_Load(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtImageDirectory.Text))
        {
            txtImageDirectory.Text = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        }

        RefreshImageList(showError: false);
    }

    /// <summary>打开目录选择对话框，选择后立即刷新图片列表。</summary>
    private void btnBrowseDirectory_Click(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "选择包含投影图片的目录",
            SelectedPath = Directory.Exists(txtImageDirectory.Text)
                ? txtImageDirectory.Text
                : string.Empty,
            ShowNewFolderButton = false
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            txtImageDirectory.Text = dialog.SelectedPath;
            RefreshImageList(showError: true);
        }
    }

    /// <summary>重新扫描当前目录。</summary>
    private void btnRefreshImages_Click(object? sender, EventArgs e) =>
        RefreshImageList(showError: true);

    /// <summary>手动投放当前图片之后的下一张；到末尾后回到第一张。</summary>
    private void btnProjectNext_Click(object? sender, EventArgs e) =>
        ProjectNextImage(showError: true);

    /// <summary>投放用户在列表中单击选中的图片。</summary>
    private void btnProjectSelected_Click(object? sender, EventArgs e)
    {
        if (_imageFiles.Count == 0)
        {
            RefreshImageList(showError: true);
            if (_imageFiles.Count == 0) return;
        }

        if (lvImages.SelectedIndices.Count != 1)
        {
            MessageBox.Show(this, "请先在图片列表中单击选择一张图片。", "未选择图片",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ProjectImageAtIndex(lvImages.SelectedIndices[0], showError: true);
    }

    /// <summary>计算下一张下标，再交给统一的按下标投图方法。</summary>
    private bool ProjectNextImage(bool showError)
    {
        if (_imageFiles.Count == 0)
        {
            RefreshImageList(showError);
            if (_imageFiles.Count == 0) return false;
        }

        int nextIndex = (_currentImageIndex + 1) % _imageFiles.Count;
        return ProjectImageAtIndex(nextIndex, showError);
    }

    /// <summary>
    /// 投放指定下标的图片，同时更新预览、列表选中项、状态文字和日志。
    /// </summary>
    private bool ProjectImageAtIndex(int imageIndex, bool showError)
    {
        if (imageIndex < 0 || imageIndex >= _imageFiles.Count) return false;

        string imagePath = _imageFiles[imageIndex];

        try
        {
            // 每次投图都重新应用所选拓扑和图片效果，确保第二屏显示符合用户设置。
            DisplayTopology topology = GetSelectedTopology();
            if (topology != DisplayTopology.None)
            {
                _desktopDisplay.ApplyTopology(topology);
            }
            ProjectedImageTransform transform = GetSelectedImageTransform();
            GetSecondScreenProjectionForm().ShowImage(imagePath, transform);

            ShowPreview(imagePath);
            _currentImageIndex = imageIndex;
            ListViewItem item = lvImages.Items[imageIndex];
            item.SubItems[2].Text = "已投图";
            item.Selected = true;
            item.EnsureVisible();
            lblCurrentImage.Text = $"当前图片：{Path.GetFileName(imagePath)}";
            lblProjectionState.Text = $"已投 {imageIndex + 1}/{_imageFiles.Count}";
            AppendLog(
                $"投图成功：{Path.GetFileName(imagePath)}（{cmbTopology.Text}，{cmbImageTransform.Text}）");
            return true;
        }
        catch (Exception ex)
        {
            lblProjectionState.Text = "投图失败";
            AppendLog($"投图失败：{ex.Message}");
            if (showError)
            {
                MessageBox.Show(this, ex.Message, "投图失败",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return false;
        }
    }

    /// <summary>开始或停止不触发设备测试的普通定时轮播。</summary>
    private void btnTimedProjection_Click(object? sender, EventArgs e)
    {
        if (_isTimedProjectionRunning)
        {
            StopTimedProjection("定时投图已停止。", closeSecondScreen: true);
            return;
        }

        if (_imageFiles.Count == 0)
        {
            RefreshImageList(showError: true);
            if (_imageFiles.Count == 0) return;
        }

        // NumericUpDown 单位是秒，WinForms Timer.Interval 单位是毫秒。
        projectionTimer.Interval = checked(decimal.ToInt32(numProjectionIntervalSeconds.Value * 1000m));
        SetTimedProjectionState(true);
        AppendLog($"开始定时投图：共 {_imageFiles.Count} 张，间隔 {numProjectionIntervalSeconds.Value} 秒。");

        if (!ProjectNextImage(showError: true))
        {
            StopTimedProjection("定时投图因切图失败而停止。", closeSecondScreen: true);
            return;
        }

        projectionTimer.Start();
    }

    /// <summary>定时器每次到点投下一张；失败时自动停止轮播。</summary>
    private void projectionTimer_Tick(object? sender, EventArgs e)
    {
        if (!ProjectNextImage(showError: false))
        {
            StopTimedProjection("定时投图因切图失败而停止。", closeSecondScreen: true);
        }
    }

    /// <summary>停止普通定时轮播，并按界面选项关闭第二屏全屏窗口。</summary>
    private void StopTimedProjection(string? logMessage, bool closeSecondScreen)
    {
        projectionTimer.Stop();
        SetTimedProjectionState(false);
        if (!string.IsNullOrEmpty(logMessage)) AppendLog(logMessage);
        if (closeSecondScreen && chkCloseSecondScreenOnStop.Checked)
        {
            CloseSecondScreenProjection();
        }
    }

    /// <summary>
    /// 切换普通定时投图的界面状态。
    /// 运行时锁定目录、模式和间隔，但保留“停止定时投图”按钮可点击。
    /// </summary>
    private void SetTimedProjectionState(bool running)
    {
        _isTimedProjectionRunning = running;
        grpSettings.Enabled = !_isCrosstalkRunning;
        bool settingsEnabled = !running && !_isCrosstalkRunning;
        txtImageDirectory.Enabled = settingsEnabled;
        btnBrowseDirectory.Enabled = settingsEnabled;
        cmbTopology.Enabled = settingsEnabled;
        btnApplyTopology.Enabled = settingsEnabled;
        cmbImageTransform.Enabled = settingsEnabled;
        btnApplyImageTransform.Enabled = settingsEnabled;
        numProjectionIntervalSeconds.Enabled = settingsEnabled;
        chkCloseSecondScreenOnStop.Enabled = settingsEnabled;
        btnTimedProjection.Enabled = !_isCrosstalkRunning;
        btnRefreshImages.Enabled = !running && !_isCrosstalkRunning;
        btnProjectNext.Enabled = !running && !_isCrosstalkRunning;
        btnProjectSelected.Enabled = !running && !_isCrosstalkRunning;
        lvImages.Enabled = !running && !_isCrosstalkRunning;
        button1.Enabled = !running && !_isCrosstalkRunning;
        btnTimedProjection.Text = running ? "停止定时投图" : "开始定时投图";
        lblProjectionState.Text = running ? "定时投图中" : "未启动定时投图";
    }

    /// <summary>立即应用下拉框选中的 Win+P 投影模式。</summary>
    private void btnApplyTopology_Click(object? sender, EventArgs e)
    {
        try
        {
            DisplayTopology topology = GetSelectedTopology();
            if (topology == DisplayTopology.None)
            {
                MessageBox.Show(this, "请选择需要应用的投影模式。", "投影模式",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _desktopDisplay.ApplyTopology(topology);
            AppendLog($"已应用投影模式：{cmbTopology.Text}");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "切换投影模式失败",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>立即按所选图片效果重新投放当前图片或列表中选中的图片。</summary>
    private void btnApplyImageTransform_Click(object? sender, EventArgs e)
    {
        int imageIndex = lvImages.SelectedIndices.Count == 1
            ? lvImages.SelectedIndices[0]
            : _currentImageIndex;
        if (imageIndex < 0 || imageIndex >= _imageFiles.Count)
        {
            MessageBox.Show(this, "请先在图片列表中选择一张图片。", "未选择图片",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ProjectImageAtIndex(imageIndex, showError: true);
    }

    /// <summary>把下拉框位置转换为 DisplayTopology 枚举。</summary>
    private DisplayTopology GetSelectedTopology() => cmbTopology.SelectedIndex switch
    {
        1 => DisplayTopology.Internal,
        2 => DisplayTopology.Clone,
        3 => DisplayTopology.External,
        4 => DisplayTopology.Extend,
        _ => DisplayTopology.None
    };

    /// <summary>把图片效果下拉框转换为图片变换枚举。</summary>
    private ProjectedImageTransform GetSelectedImageTransform() =>
        cmbImageTransform.SelectedIndex switch
        {
            1 => ProjectedImageTransform.Landscape,
            2 => ProjectedImageTransform.FlipHorizontal,
            3 => ProjectedImageTransform.FlipVertical,
            4 => ProjectedImageTransform.FlipBoth,
            _ => ProjectedImageTransform.Original
        };

    /// <summary>切换图片效果时立即刷新预览，但要点击投图按钮后才改变第二屏图片。</summary>
    private void cmbImageTransform_SelectedIndexChanged(object? sender, EventArgs e)
    {
        string? imagePath = lvImages.SelectedItems.Count == 1 &&
                            lvImages.SelectedItems[0].Tag is string selectedPath
            ? selectedPath
            : _currentImageIndex >= 0 && _currentImageIndex < _imageFiles.Count
                ? _imageFiles[_currentImageIndex]
                : null;

        if (imagePath is not null) ShowPreview(imagePath);
    }

    /// <summary>
    /// 扫描图片目录、重建列表并重置当前投图位置。
    /// showError=false 用于窗口刚打开时静默尝试，避免无目录时立即打扰用户。
    /// </summary>
    private bool RefreshImageList(bool showError)
    {
        string directory = txtImageDirectory.Text.Trim();
        if (!Directory.Exists(directory))
        {
            _imageFiles = [];
            lvImages.Items.Clear();
            lblProjectionState.Text = "图片目录不存在";
            if (showError)
            {
                MessageBox.Show(this, "图片目录不存在，请重新选择。", "图片目录",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            return false;
        }

        try
        {
            _imageFiles = FindImageFiles(directory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _imageFiles = [];
            lvImages.Items.Clear();
            lblProjectionState.Text = "读取图片目录失败";
            AppendLog($"读取图片目录失败：{ex.Message}");
            if (showError)
            {
                MessageBox.Show(this, ex.Message, "读取图片目录失败",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return false;
        }

        // 文件集合可能已改变，所以旧下标不再可信，从“尚未投图”重新开始。
        _currentImageIndex = -1;
        lvImages.BeginUpdate();
        try
        {
            lvImages.Items.Clear();
            for (int index = 0; index < _imageFiles.Count; index++)
            {
                var item = new ListViewItem((index + 1).ToString());
                item.SubItems.Add(Path.GetFileName(_imageFiles[index]));
                item.SubItems.Add("待投图");
                item.Tag = _imageFiles[index];
                lvImages.Items.Add(item);
            }
        }
        finally
        {
            lvImages.EndUpdate();
        }

        grpImages.Text = $"投影图片（{_imageFiles.Count} 张）";
        lblProjectionState.Text = $"已加载 {_imageFiles.Count} 张图片";
        if (_imageFiles.Count > 0) lvImages.Items[0].Selected = true;

        if (showError && _imageFiles.Count == 0)
        {
            MessageBox.Show(this, "当前目录没有找到 PNG、BMP、JPG 或 JPEG 图片。",
                "图片数量", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        return _imageFiles.Count > 0;
    }

    /// <summary>返回目录第一层中的支持图片，并按文件名排序。</summary>
    internal static List<string> FindImageFiles(string directory) =>
        Directory.EnumerateFiles(directory)
            .Where(path => SupportedExtensions.Contains(Path.GetExtension(path)))
            .OrderBy(path => Path.GetFileName(path), StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    /// <summary>
    /// 生成串扰测试顺序。例如 4 张图从下标 2 开始，顺序为 2、3、0、1。
    /// 这样可以从任意选中图开始，又保证每张图只测试一次。
    /// </summary>
    internal static IReadOnlyList<int> BuildImageTestOrder(int startIndex, int imageCount)
    {
        if (imageCount <= 0) throw new ArgumentOutOfRangeException(nameof(imageCount));
        if (startIndex < 0 || startIndex >= imageCount)
        {
            throw new ArgumentOutOfRangeException(nameof(startIndex));
        }

        return Enumerable.Range(0, imageCount)
            .Select(offset => (startIndex + offset) % imageCount)
            .ToArray();
    }

    /// <summary>开始新批次前，把列表中所有状态恢复成“待测试”。</summary>
    private void ResetImageTestStatuses()
    {
        foreach (ListViewItem item in lvImages.Items)
        {
            item.SubItems[2].Text = "待测试";
        }
    }

    /// <summary>列表单击改变选中项时只更新预览，不会立即投图。</summary>
    private void lvImages_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (lvImages.SelectedItems.Count == 1 && lvImages.SelectedItems[0].Tag is string path)
        {
            ShowPreview(path);
        }
    }

    /// <summary>
    /// 读取图片并生成不超过预览框大小的副本。
    /// 使用副本后立即关闭源文件，避免图片文件一直被程序锁定。
    /// </summary>
    private void ShowPreview(string imagePath)
    {
        try
        {
            using Image source = Image.FromFile(imagePath);
            ProjectedImageTransformer.Apply(source, GetSelectedImageTransform());
            Size bounds = picPreview.ClientSize;
            // 取宽、高缩放比例中较小者，可以完整显示图片而不裁切。
            double scale = Math.Min(
                (double)Math.Max(1, bounds.Width) / source.Width,
                (double)Math.Max(1, bounds.Height) / source.Height);
            int width = Math.Max(1, (int)Math.Round(source.Width * Math.Min(1d, scale)));
            int height = Math.Max(1, (int)Math.Round(source.Height * Math.Min(1d, scale)));
            Image preview = new Bitmap(source, width, height);
            Image? oldImage = picPreview.Image;
            picPreview.Image = preview;
            oldImage?.Dispose();
        }
        catch
        {
            // 预览失败不阻止投图，投图时会报告准确错误。
        }
    }

    /// <summary>取得仍可用的第二屏窗口；已经关闭时自动创建一个新实例。</summary>
    private SecondScreenProjectionForm GetSecondScreenProjectionForm()
    {
        if (_secondScreenProjectionForm is null || _secondScreenProjectionForm.IsDisposed)
        {
            _secondScreenProjectionForm = new SecondScreenProjectionForm();
        }

        return _secondScreenProjectionForm;
    }

    /// <summary>关闭并释放第二屏窗口；可重复调用，不会影响控制窗口。</summary>
    private void CloseSecondScreenProjection()
    {
        SecondScreenProjectionForm? projectionForm = _secondScreenProjectionForm;
        _secondScreenProjectionForm = null;
        if (projectionForm is null || projectionForm.IsDisposed) return;

        try
        {
            projectionForm.Close();
            projectionForm.Dispose();
            AppendLog("第二屏全屏投图窗口已关闭。");
        }
        catch (Exception ex)
        {
            AppendLog($"关闭第二屏投图窗口失败：{ex.Message}");
        }
    }

    /// <summary>在投影日志末尾追加一行带时间的说明。</summary>
    private void AppendLog(string message) =>
        txtProjectionLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");

    /// <summary>关闭投影窗口。</summary>
    private void btnClose_Click(object? sender, EventArgs e) => Close();

    /// <summary>关闭前取消批量任务、停止计时器，并关闭第二屏全屏窗口。</summary>
    private void ProjectionForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        _crosstalkCancellation?.Cancel();
        _formCancellation.Cancel();

        if (_isTimedProjectionRunning)
        {
            StopTimedProjection(null, closeSecondScreen: false);
        }
        CloseSecondScreenProjection();
    }

    /// <summary>
    /// “串扰测试/停止串扰测试”按钮入口。
    /// 流程为：投图 → 发送并等待设备完成 → 下一张，直到每张图片恰好完成一次。
    /// </summary>
    private async void button1_Click(object? sender, EventArgs e)
    {
        if (_isCrosstalkRunning)
        {
            button1.Enabled = false;
            button1.Text = "正在停止...";
            _crosstalkCancellation?.Cancel();
            return;
        }

        if (_sendCurrentMessageAsync is null)
        {
            MessageBox.Show(this, "请从主界面的“扩展投影切图”按钮打开此窗口。",
                "无法发送", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (_isTimedProjectionRunning)
        {
            StopTimedProjection("定时投图已停止，开始串扰测试。", closeSecondScreen: false);
        }

        if (_imageFiles.Count == 0)
        {
            RefreshImageList(showError: true);
            if (_imageFiles.Count == 0) return;
        }

        // 优先从列表当前选中项开始；没有选中项时使用已投图片，最后才回退到第一张。
        int startIndex = lvImages.SelectedIndices.Count == 1
            ? lvImages.SelectedIndices[0]
            : _currentImageIndex >= 0 ? _currentImageIndex : 0;

        ResetImageTestStatuses();
        if (!ProjectImageAtIndex(startIndex, showError: true)) return;

        // 独立 Token 允许用户只停止本批次，同时仍能继续使用投影窗口。
        var crosstalkCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            _formCancellation.Token);
        _crosstalkCancellation = crosstalkCancellation;
        SetCrosstalkState(true);
        IReadOnlyList<int> testOrder = BuildImageTestOrder(startIndex, _imageFiles.Count);
        AppendLog(
            $"开始串扰测试：共 {testOrder.Count} 张，从 {Path.GetFileName(_imageFiles[startIndex])} 开始。");

        try
        {
            // testOrder 的长度等于图片数，因此循环结束后不会再次测试已经完成的图片。
            for (int position = 0; position < testOrder.Count; position++)
            {
                crosstalkCancellation.Token.ThrowIfCancellationRequested();
                int imageIndex = testOrder[position];

                if (position > 0)
                {
                    // 第一张已在循环前投好；从第二张开始遵循“完成后等 1 秒再投下一张”。
                    AppendLog("等待 1 秒后投放下一张图片。");
                    await Task.Delay(TimeSpan.FromSeconds(1), crosstalkCancellation.Token);
                    if (!ProjectImageAtIndex(imageIndex, showError: true))
                    {
                        lvImages.Items[imageIndex].SubItems[2].Text = "投图失败";
                        lblProjectionState.Text = $"串扰测试停止：第 {position + 1} 张投图失败";
                        return;
                    }
                }

                ListViewItem item = lvImages.Items[imageIndex];
                item.SubItems[2].Text = "测试中";
                lblProjectionState.Text =
                    $"串扰测试 {position + 1}/{testOrder.Count}：{Path.GetFileName(_imageFiles[imageIndex])}";
                AppendLog(
                    $"[{position + 1}/{testOrder.Count}] 已投图，发送报文启动设备测试。");

                // 回调内部会发送报文、忽略 Run 中间应答，并等待最终 OK/NG。
                bool completed = await _sendCurrentMessageAsync(crosstalkCancellation.Token);
                crosstalkCancellation.Token.ThrowIfCancellationRequested();
                if (!completed)
                {
                    item.SubItems[2].Text = "测试失败";
                    lblProjectionState.Text =
                        $"串扰测试停止：第 {position + 1} 张发送或确认失败";
                    AppendLog("发送或完成确认失败，不再继续投放图片。");
                    return;
                }

                item.SubItems[2].Text = "测试完成";
                AppendLog($"[{position + 1}/{testOrder.Count}] 测试完成。");
            }

            lblProjectionState.Text = $"串扰测试完成：{testOrder.Count} 张图片";
            AppendLog($"串扰测试全部完成：共 {testOrder.Count} 张图片。");
            MessageBox.Show(this, $"文件夹内 {testOrder.Count} 张图片已全部测试完成。",
                "串扰测试完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (OperationCanceledException)
        {
            if (!IsDisposed && !Disposing)
            {
                lblProjectionState.Text = "串扰测试已停止";
                AppendLog("串扰测试已停止，不再继续投图。");
            }
        }
        finally
        {
            if (ReferenceEquals(_crosstalkCancellation, crosstalkCancellation))
            {
                _crosstalkCancellation = null;
            }
            crosstalkCancellation.Dispose();

            if (!IsDisposed && !Disposing) SetCrosstalkState(false);
        }
    }

    /// <summary>运行串扰批次时锁定会改变图片集合或投影模式的控件。</summary>
    private void SetCrosstalkState(bool running)
    {
        _isCrosstalkRunning = running;
        grpSettings.Enabled = !running && !_isTimedProjectionRunning;
        btnRefreshImages.Enabled = !running && !_isTimedProjectionRunning;
        btnProjectNext.Enabled = !running && !_isTimedProjectionRunning;
        btnProjectSelected.Enabled = !running && !_isTimedProjectionRunning;
        lvImages.Enabled = !running && !_isTimedProjectionRunning;
        button1.Enabled = true;
        button1.Text = running ? "停止串扰测试" : "串扰测试";
    }

    /// <summary>安全且只执行一次地取消并释放窗体级 CancellationTokenSource。</summary>
    private void DisposeFormCancellation()
    {
        if (_formCancellationDisposed) return;

        _formCancellationDisposed = true;
        _formCancellation.Cancel();
        _formCancellation.Dispose();
    }

    /// <summary>供 Designer.Dispose 调用，确保直接释放控制窗体时第二屏窗口也会关闭。</summary>
    private void DisposeSecondScreenProjection() => CloseSecondScreenProjection();

}
