// 接口只描述“能做什么”，具体 Windows API 实现在 DesktopDisplayService 中。
// 这样窗体依赖的是清晰的小接口，而不是一堆难懂的原生函数。
namespace SimpleProtocolServer.Projection;

/// <summary>隔离 Windows 桌面壁纸及显示器拓扑操作。</summary>
internal interface IDesktopDisplayService
{
    /// <summary>读取当前桌面壁纸路径。</summary>
    string? GetCurrentWallpaper();
    /// <summary>切换 Windows 投影模式。</summary>
    void ApplyTopology(DisplayTopology topology);
    /// <summary>按所选图片效果处理后，把指定图片设置为桌面壁纸。</summary>
    void SetWallpaper(
        string imagePath,
        ProjectedImageTransform transform = ProjectedImageTransform.Original);
}
