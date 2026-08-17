// 图片变换与屏幕拓扑分开：这里只改变投放图片本身，不旋转第二块显示器。
namespace SimpleProtocolServer.Projection;

/// <summary>投放到第二屏幕前可以选择的图片方向和镜像效果。</summary>
internal enum ProjectedImageTransform
{
    /// <summary>保持图片原始方向。</summary>
    Original,
    /// <summary>竖图自动顺时针旋转 90° 成为横图；原本已是横图时不旋转。</summary>
    Landscape,
    /// <summary>图片左右对调，形成水平镜像。</summary>
    FlipHorizontal,
    /// <summary>图片上下对调，形成垂直镜像。</summary>
    FlipVertical,
    /// <summary>图片同时左右、上下对调，效果等同旋转 180°。</summary>
    FlipBoth
}

/// <summary>集中实现图片变换，确保预览和实际投图使用完全相同的规则。</summary>
internal static class ProjectedImageTransformer
{
    /// <summary>直接修改传入图片；调用者应传入自己拥有并会释放的 Image 实例。</summary>
    internal static void Apply(Image image, ProjectedImageTransform transform)
    {
        ArgumentNullException.ThrowIfNull(image);

        RotateFlipType operation = transform switch
        {
            ProjectedImageTransform.Original => RotateFlipType.RotateNoneFlipNone,
            ProjectedImageTransform.Landscape when image.Height > image.Width =>
                RotateFlipType.Rotate90FlipNone,
            ProjectedImageTransform.Landscape => RotateFlipType.RotateNoneFlipNone,
            ProjectedImageTransform.FlipHorizontal => RotateFlipType.RotateNoneFlipX,
            ProjectedImageTransform.FlipVertical => RotateFlipType.RotateNoneFlipY,
            ProjectedImageTransform.FlipBoth => RotateFlipType.RotateNoneFlipXY,
            _ => throw new ArgumentOutOfRangeException(nameof(transform))
        };

        image.RotateFlip(operation);
    }
}
