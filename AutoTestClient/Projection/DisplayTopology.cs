using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace AutoTestClient.Projection;

/// <summary>
/// 显示器拓扑信息。
/// </summary>
/// <remarks>
/// 当前只保留给“第二屏窗口”使用的最小信息，后续主程序可继续扩展。
/// </remarks>
public sealed class DisplayTopology
{
    /// <summary>显示器顺序索引。</summary>
    public int Index { get; init; }

    /// <summary>显示器名称。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>显示器边界。</summary>
    public Rectangle Bounds { get; init; }

    /// <summary>是否为主显示器。</summary>
    public bool Primary { get; init; }

    /// <summary>
    /// 获取当前机器上的显示器拓扑。
    /// </summary>
    public static DisplayTopology[] Capture()
    {
        return Screen.AllScreens
            .Select((screen, index) => new DisplayTopology
            {
                Index = index,
                Name = screen.DeviceName,
                Bounds = screen.Bounds,
                Primary = screen.Primary
            })
            .ToArray();
    }

    /// <summary>
    /// 获取第二屏，如果不存在则返回空。
    /// </summary>
    public static DisplayTopology? GetSecondScreen()
    {
        return Capture().FirstOrDefault(x => !x.Primary) ?? Capture().Skip(1).FirstOrDefault();
    }
}
