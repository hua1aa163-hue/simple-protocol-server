// 使用枚举代替 Windows API 的魔法数字，让界面代码更容易阅读。
namespace SimpleProtocolServer.Projection;

/// <summary>Windows 投影模式；None 表示只切换图片，不改变显示器拓扑。</summary>
internal enum DisplayTopology
{
    /// <summary>保持当前显示器模式，只切换桌面图片。</summary>
    None = 0,
    /// <summary>仅使用电脑主屏幕。</summary>
    Internal = 1,
    /// <summary>两个屏幕显示相同内容。</summary>
    Clone = 2,
    /// <summary>仅使用第二屏幕。</summary>
    External = 3,
    /// <summary>扩展桌面到多个屏幕。</summary>
    Extend = 4
}
