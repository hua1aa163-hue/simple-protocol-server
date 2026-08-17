// 本控件专门用于第二屏 1:1 像素投图：禁止缩放和插值，只做居中及必要的裁切。
using System.ComponentModel;

namespace SimpleProtocolServer.Projection;

/// <summary>
/// 把源图片的每一个像素直接画到一个屏幕像素上。
/// 小图在黑底中居中，大图超出屏幕的部分会裁切，绝不缩小。
/// </summary>
public sealed class PixelPerfectImageControl : Control
{
    private Image? _projectedImage;

    /// <summary>启用双缓冲消除切图闪烁，但不会改变图片像素或使用缩放插值。</summary>
    public PixelPerfectImageControl()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        BackColor = Color.Black;
    }

    /// <summary>只供程序和测试查看，设计器不序列化运行时图片。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal Image? ProjectedImage => _projectedImage;

    /// <summary>接管新图片的所有权，并立即释放上一张图片。</summary>
    internal void ReplaceImage(Image image)
    {
        ArgumentNullException.ThrowIfNull(image);

        Image? oldImage = _projectedImage;
        _projectedImage = image;
        oldImage?.Dispose();
        Invalidate();
    }

    /// <summary>
    /// 计算原尺寸图片的左上角。负坐标表示图片比屏幕大，需要从两侧等量裁切。
    /// 整数除法确保落点始终位于真实像素边界，不会产生半像素模糊。
    /// </summary>
    internal static Point CalculateImageLocation(Size viewport, Size imageSize) =>
        new(
            (viewport.Width - imageSize.Width) / 2,
            (viewport.Height - imageSize.Height) / 2);

    /// <summary>黑底清屏后用 DrawImageUnscaled 原尺寸绘制，明确禁止任何缩放算法。</summary>
    protected override void OnPaint(PaintEventArgs e)
        => RenderPixelPerfect(e.Graphics, ClientSize);

    /// <summary>
    /// 窗口实际绘制和自动测试共用此方法，确保测试验证的正是生产显示路径。
    /// </summary>
    internal void RenderPixelPerfect(Graphics graphics, Size viewport)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        graphics.Clear(BackColor);
        Image? image = _projectedImage;
        if (image is null) return;

        Point location = CalculateImageLocation(viewport, image.Size);
        graphics.DrawImageUnscaled(image, location);
    }

    /// <summary>控件释放时同步释放当前图片像素缓冲区。</summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _projectedImage?.Dispose();
            _projectedImage = null;
        }

        base.Dispose(disposing);
    }
}
