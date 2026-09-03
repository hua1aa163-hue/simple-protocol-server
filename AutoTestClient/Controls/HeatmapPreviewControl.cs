using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using AutoTestClient.DataProcessing;

namespace AutoTestClient.Controls;

/// <summary>
/// 可嵌入窗体的串扰热力图预览控件。
///
/// 控件不依赖数据处理层：运行时可以通过 <see cref="SetImage(Image?)"/>
/// 显示已经导出的 PNG，也可以通过 <see cref="SetValues(double[,]?)"/>
/// 直接绘制矩阵。设计器中不会读取磁盘文件，会显示安全的占位文字。
/// </summary>
[DesignerCategory("Code")]
[DefaultEvent(nameof(MaskSelected))]
public sealed class HeatmapPreviewControl : Control
{
    private Image? _image;
    private double[,]? _values;
    private readonly HashSet<(int Row, int Column)> _maskedCells = new();
    private Point? _dragStart;
    private Point? _dragCurrent;
    private BorderStyle _borderStyle = BorderStyle.FixedSingle;

    public HeatmapPreviewControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.UserPaint, true);
        BackColor = Color.White;
        ForeColor = Color.FromArgb(31, 41, 55);
        // 主界面预览区高度约 78px；过高的最小尺寸会迫使 TableLayoutPanel
        // 撑开“等待开始”区域并挤压日志表格，因此只保留一个可用的紧凑下限。
        MinimumSize = new Size(120, 40);
        TabStop = false;
    }

    /// <summary>预览边框样式，与 WinForms 常用控件保持一致。</summary>
    [Category("外观")]
    [DefaultValue(BorderStyle.FixedSingle)]
    public BorderStyle BorderStyle
    {
        get => _borderStyle;
        set
        {
            if (_borderStyle == value) return;
            _borderStyle = value;
            Invalidate();
        }
    }

    /// <summary>PNG/Bitmap 预览。控件会复制图像，调用方仍可安全释放原对象。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Image? HeatmapImage
    {
        get => _image;
        set => SetImage(value);
    }

    /// <summary>当前矩阵的只读快照；数值按比例保存，例如 0.03 表示 3%。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double[,]? Values => _values is null ? null : (double[,])_values.Clone();

    /// <summary>是否在矩阵单元格中绘制数值。</summary>
    [Category("外观")]
    [DefaultValue(true)]
    public bool ShowCellValues { get; set; } = true;

    /// <summary>颜色轴下限，单位为百分比。</summary>
    [Category("串扰热图")]
    [DefaultValue(0d)]
    public double ColorMinimumPercent { get; set; } = 0d;

    /// <summary>颜色轴上限，单位为百分比。</summary>
    [Category("串扰热图")]
    [DefaultValue(50d)]
    public double ColorMaximumPercent { get; set; } = 50d;

    /// <summary>异常阈值，单位为百分比。超过此值的单元格使用异常边框。</summary>
    [Category("串扰热图")]
    [DefaultValue(3d)]
    public double AnomalyThresholdPercent { get; set; } = 3d;

    /// <summary>允许在矩阵上拖拽选择掩膜区域。</summary>
    [Category("串扰热图")]
    [DefaultValue(true)]
    public bool EnableMaskSelection { get; set; } = true;

    /// <summary>无数据时显示的文字。可在设计器中直接修改。</summary>
    [Category("外观")]
    [DefaultValue("串扰热图将在测试完成后显示")]
    public string EmptyText { get; set; } = "串扰热图将在测试完成后显示";

    /// <summary>用户在热图上拖拽选择一个（含首尾）矩形区域后触发。</summary>
    public event EventHandler<HeatmapMaskSelectedEventArgs>? MaskSelected;

    /// <summary>掩膜集合变化后触发。</summary>
    public event EventHandler? MaskChanged;

    /// <summary>替换当前图像。传入 null 可清空预览。</summary>
    public void SetImage(Image? image)
    {
        Image? replacement = null;
        if (image is not null)
        {
            // 复制一份，避免 Image.FromFile 保持文件锁，也避免外部释放导致绘制异常。
            replacement = new Bitmap(image);
        }

        Image? old = _image;
        _image = replacement;
        old?.Dispose();
        // SetImage 代表切换到图像/空状态；无论传入是否为 null，都清掉旧矩阵，
        // 这样调用 SetImage(null) 才真正得到空预览。
        _values = null;
        Invalidate();
    }

    /// <summary>从 PNG/JPEG 加载图像；失败时返回 false 并保留旧图像。</summary>
    public bool TryLoadImage(string? path, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(path))
        {
            SetImage(null);
            return true;
        }

        try
        {
            using Image source = Image.FromFile(path);
            SetImage(source);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or OutOfMemoryException)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>替换当前矩阵。矩阵按比例保存，控件内部会复制数据。</summary>
    public void SetValues(double[,]? values)
    {
        _values = values is null ? null : (double[,])values.Clone();
        if (_values is not null)
        {
            Image? old = _image;
            _image = null;
            old?.Dispose();
            TrimMaskToValues();
        }
        else
        {
            Image? old = _image;
            _image = null;
            old?.Dispose();
        }
        Invalidate();
    }

    /// <summary>设置（替换）掩膜单元格，坐标为 0-based。</summary>
    public void SetMaskedCells(IEnumerable<(int Row, int Column)>? cells)
    {
        _maskedCells.Clear();
        if (cells is not null)
        {
            foreach (var cell in cells)
                _maskedCells.Add(cell);
        }
        TrimMaskToValues();
        Invalidate();
        MaskChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>返回当前掩膜的快照，坐标为 0-based。</summary>
    public IReadOnlyCollection<(int Row, int Column)> GetMaskedCells() =>
        _maskedCells.ToArray();

    /// <summary>清空掩膜。</summary>
    public void ClearMask()
    {
        if (_maskedCells.Count == 0) return;
        _maskedCells.Clear();
        Invalidate();
        MaskChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _image?.Dispose();
            _image = null;
        }
        base.Dispose(disposing);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics graphics = e.Graphics;
        graphics.Clear(BackColor);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

        if (_values is not null)
        {
            DrawValues(graphics, _values);
            return;
        }

        if (_image is not null)
        {
            DrawImage(graphics, _image);
            DrawControlBorder(graphics);
            return;
        }

        using var border = new Pen(Color.FromArgb(203, 213, 225));
        graphics.DrawRectangle(border, 0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
        using var textBrush = new SolidBrush(Color.FromArgb(100, 116, 139));
        using var font = new Font(Font.FontFamily, Math.Max(8f, Font.Size - 1f), FontStyle.Regular);
        using StringFormat format = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        graphics.DrawString(EmptyText ?? string.Empty, font, textBrush, ClientRectangle, format);
        DrawControlBorder(graphics);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (!EnableMaskSelection || _values is null || e.Button != MouseButtons.Left) return;
        if (TryCellFromPoint(e.Location, _values, out Point cell))
        {
            _dragStart = cell;
            _dragCurrent = cell;
            Capture = true;
            Invalidate();
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragStart is null || _values is null || !Capture) return;
        if (TryCellFromPoint(e.Location, _values, out Point cell))
        {
            _dragCurrent = cell;
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (_dragStart is null || _values is null || e.Button != MouseButtons.Left) return;
        Point start = _dragStart.Value;
        Point end = _dragCurrent ?? start;
        _dragStart = null;
        _dragCurrent = null;
        Capture = false;

        int rowStart = Math.Min(start.Y, end.Y);
        int rowEnd = Math.Max(start.Y, end.Y);
        int columnStart = Math.Min(start.X, end.X);
        int columnEnd = Math.Max(start.X, end.X);
        for (int row = rowStart; row <= rowEnd; row++)
        {
            for (int column = columnStart; column <= columnEnd; column++)
                _maskedCells.Add((row, column));
        }
        Invalidate();
        MaskChanged?.Invoke(this, EventArgs.Empty);
        // 对外坐标采用用户界面习惯的 1-based 且首尾包含。
        MaskSelected?.Invoke(this, new HeatmapMaskSelectedEventArgs(
            rowStart + 1, columnStart + 1, rowEnd + 1, columnEnd + 1));
    }

    private void DrawImage(Graphics graphics, Image image)
    {
        Rectangle target = GetContainRectangle(image.Size, ClientRectangle);
        using var border = new Pen(Color.FromArgb(203, 213, 225));
        graphics.DrawImage(image, target);
        graphics.DrawRectangle(border, target);
    }

    private void DrawValues(Graphics graphics, double[,] values)
    {
        int rows = values.GetLength(0);
        int columns = values.GetLength(1);
        if (rows <= 0 || columns <= 0) return;

        const int margin = 8;
        Rectangle plot = new(margin, margin, Math.Max(1, Width - margin * 2), Math.Max(1, Height - margin * 2));
        double lower = ColorMinimumPercent;
        double upper = ColorMaximumPercent;
        if (!double.IsFinite(lower)) lower = 0;
        if (!double.IsFinite(upper) || upper <= lower) upper = lower + 1;

        using var gridPen = new Pen(Color.FromArgb(148, 163, 184), 1f);
        using var anomalyPen = new Pen(Color.FromArgb(220, 38, 38), Math.Max(1f, Math.Min(3f, Math.Min(plot.Width / (float)columns, plot.Height / (float)rows) / 8f)));
        using var maskBrush = new SolidBrush(Color.FromArgb(115, 15, 23, 42));
        using var valueFont = new Font(Font.FontFamily, Math.Max(6f, Math.Min(10f, Math.Min(plot.Width / (float)columns, plot.Height / (float)rows) * .32f)), FontStyle.Regular);
        using StringFormat valueFormat = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

        for (int row = 0; row < rows; row++)
        {
            int top = plot.Top + (int)Math.Round(row * plot.Height / (double)rows);
            int bottom = plot.Top + (int)Math.Round((row + 1) * plot.Height / (double)rows);
            for (int column = 0; column < columns; column++)
            {
                int left = plot.Left + (int)Math.Round(column * plot.Width / (double)columns);
                int right = plot.Left + (int)Math.Round((column + 1) * plot.Width / (double)columns);
                Rectangle cell = new(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
                double raw = values[row, column];
                bool valid = double.IsFinite(raw);
                bool userMasked = _maskedCells.Contains((row, column));
                if (!valid)
                {
                    // Keep the same visual distinction as the exported
                    // analyzer image: fixed border/user exclusions are dark
                    // gray, while an otherwise invalid source sample is
                    // neutral gray.  The border is inferred from the 19×32
                    // matrix edges because it is a fixed analyzer rule.
                    bool excluded = userMasked || row == 0 || row == rows - 1 ||
                        column == 0 || column == columns - 1;
                    Color invalidColor = excluded
                        ? Color.FromArgb(35, 35, 35)
                        : Color.FromArgb(90, 90, 90);
                    using var invalidBrush = new SolidBrush(invalidColor);
                    graphics.FillRectangle(invalidBrush, cell);
                }
                else
                {
                    using var valueBrush = new SolidBrush(ColorFor(raw * 100d, lower, upper));
                    graphics.FillRectangle(valueBrush, cell);
                    bool abnormal = raw * 100d > AnomalyThresholdPercent;
                    if (abnormal)
                        graphics.DrawRectangle(anomalyPen, cell);
                    if (ShowCellValues && !userMasked && cell.Width >= 16 && cell.Height >= 12)
                    {
                        Color fill = ColorFor(raw * 100d, lower, upper);
                        // Match the reference analyzer's luminance rule
                        // (black text above 145/255, white below it) rather
                        // than the HSL brightness heuristic used previously.
                        double luminance = .299 * fill.R + .587 * fill.G + .114 * fill.B;
                        Color textColor = luminance > 145d
                            ? Color.FromArgb(15, 23, 42)
                            : Color.White;
                        using var brush = new SolidBrush(textColor);
                        graphics.DrawString($"{raw * 100d:0.###}", valueFont, brush, cell, valueFormat);
                    }
                }

                if (userMasked)
                    graphics.FillRectangle(maskBrush, cell);
                graphics.DrawRectangle(gridPen, cell);
            }
        }

        if (_dragStart is Point start && _dragCurrent is Point current)
        {
            int rowStart = Math.Min(start.Y, current.Y);
            int rowEnd = Math.Max(start.Y, current.Y);
            int columnStart = Math.Min(start.X, current.X);
            int columnEnd = Math.Max(start.X, current.X);
            Rectangle selection = CellRangeRectangle(plot, rows, columns, rowStart, rowEnd, columnStart, columnEnd);
            using var selectionPen = new Pen(Color.FromArgb(37, 99, 235), 2f) { DashStyle = DashStyle.Dash };
            graphics.DrawRectangle(selectionPen, selection);
        }

        DrawControlBorder(graphics);
    }

    private void DrawControlBorder(Graphics graphics)
    {
        if (_borderStyle == BorderStyle.None || Width <= 0 || Height <= 0) return;
        Color color = _borderStyle == BorderStyle.Fixed3D
            ? SystemColors.ActiveBorder
            : Color.FromArgb(148, 163, 184);
        using var pen = new Pen(color, 1f);
        graphics.DrawRectangle(pen, 0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
    }

    private static Rectangle GetContainRectangle(Size imageSize, Rectangle bounds)
    {
        if (imageSize.Width <= 0 || imageSize.Height <= 0 || bounds.Width <= 0 || bounds.Height <= 0)
            return bounds;
        double scale = Math.Min(bounds.Width / (double)imageSize.Width, bounds.Height / (double)imageSize.Height);
        int width = Math.Max(1, (int)Math.Round(imageSize.Width * scale));
        int height = Math.Max(1, (int)Math.Round(imageSize.Height * scale));
        return new Rectangle(bounds.Left + (bounds.Width - width) / 2,
            bounds.Top + (bounds.Height - height) / 2, width, height);
    }

    private static Color ColorFor(double percent, double lower, double upper)
    {
        double normalized = (percent - lower) / (upper - lower);
        // Use the same three-channel piecewise colour map as the exported
        // PNG so a value has one stable visual meaning in both views.
        return CrosstalkColorMap.Jet(normalized);
    }

    private static Rectangle CellRangeRectangle(Rectangle plot, int rows, int columns,
        int rowStart, int rowEnd, int columnStart, int columnEnd)
    {
        int left = plot.Left + (int)Math.Round(columnStart * plot.Width / (double)columns);
        int top = plot.Top + (int)Math.Round(rowStart * plot.Height / (double)rows);
        int right = plot.Left + (int)Math.Round((columnEnd + 1) * plot.Width / (double)columns);
        int bottom = plot.Top + (int)Math.Round((rowEnd + 1) * plot.Height / (double)rows);
        return new Rectangle(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
    }

    private static bool TryCellFromPoint(Point point, Rectangle plot, int rows, int columns, out Point cell)
    {
        if (!plot.Contains(point))
        {
            cell = default;
            return false;
        }
        int column = Math.Clamp((int)((point.X - plot.Left) * (long)columns / Math.Max(1, plot.Width)), 0, columns - 1);
        int row = Math.Clamp((int)((point.Y - plot.Top) * (long)rows / Math.Max(1, plot.Height)), 0, rows - 1);
        cell = new Point(column, row);
        return true;
    }

    private bool TryCellFromPoint(Point point, double[,] values, out Point cell)
    {
        const int margin = 8;
        Rectangle plot = new(margin, margin, Math.Max(1, Width - margin * 2), Math.Max(1, Height - margin * 2));
        return TryCellFromPoint(point, plot, values.GetLength(0), values.GetLength(1), out cell);
    }

    private void TrimMaskToValues()
    {
        if (_values is null) return;
        int rows = _values.GetLength(0);
        int columns = _values.GetLength(1);
        _maskedCells.RemoveWhere(cell => cell.Row < 0 || cell.Column < 0 || cell.Row >= rows || cell.Column >= columns);
    }
}

/// <summary>热图掩膜选择事件；坐标为 1-based 且首尾包含。</summary>
public sealed class HeatmapMaskSelectedEventArgs : EventArgs
{
    public HeatmapMaskSelectedEventArgs(int rowStart, int columnStart, int rowEnd, int columnEnd)
    {
        RowStart = rowStart;
        ColumnStart = columnStart;
        RowEnd = rowEnd;
        ColumnEnd = columnEnd;
    }

    public int RowStart { get; }
    public int ColumnStart { get; }
    public int RowEnd { get; }
    public int ColumnEnd { get; }

    public override string ToString() =>
        $"{RowStart},{ColumnStart}-{RowEnd},{ColumnEnd}";
}
