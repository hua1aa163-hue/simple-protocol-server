namespace AutoTestClient.Projection;

/// <summary>
/// 投影窗口显示模式。
/// </summary>
public enum ProjectionMode
{
    /// <summary>默认按实际像素比显示图像。</summary>
    PixelPerfect = 0,

    /// <summary>平铺适配显示区域。</summary>
    FitToWindow = 1
}
