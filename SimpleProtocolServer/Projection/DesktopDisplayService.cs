// 本文件是 Windows 系统 API 的薄封装：上层窗体不需要了解 P/Invoke 的细节。
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace SimpleProtocolServer.Projection;

/// <summary>
/// 通过 Windows API 切换投影模式；图片由第二屏全屏窗体负责显示。
/// P/Invoke（DllImport）允许托管的 C# 代码调用 user32.dll 中的原生函数。
/// </summary>
internal sealed class DesktopDisplayService : IDesktopDisplayService
{
    // SetDisplayConfig 使用的投影拓扑标志，对应 Win+P 中的四种模式。
    private const uint SdcApply = 0x00000080;
    private const uint SdcTopologyInternal = 0x00000001;
    private const uint SdcTopologyClone = 0x00000002;
    private const uint SdcTopologyExtend = 0x00000004;
    private const uint SdcTopologyExternal = 0x00000008;

    // 该 API 用来切换“仅电脑/复制/仅第二屏/扩展”等显示器拓扑。
    [DllImport("user32.dll")]
    private static extern int SetDisplayConfig(
        uint numPathArrayElements,
        IntPtr pathArray,
        uint numModeArrayElements,
        IntPtr modeArray,
        uint flags);

    /// <summary>应用指定的 Windows 投影模式；None 表示不做任何切换。</summary>
    public void ApplyTopology(DisplayTopology topology)
    {
        if (topology == DisplayTopology.None) return;

        // 把程序自己的枚举转换成 Windows API 认识的数值标志。
        uint topologyFlag = topology switch
        {
            DisplayTopology.Internal => SdcTopologyInternal,
            DisplayTopology.Clone => SdcTopologyClone,
            DisplayTopology.External => SdcTopologyExternal,
            DisplayTopology.Extend => SdcTopologyExtend,
            _ => throw new ArgumentOutOfRangeException(nameof(topology))
        };

        // 使用预定义拓扑时无需传入显示路径数组，所以数量为 0、指针为空。
        int result = SetDisplayConfig(0, IntPtr.Zero, 0, IntPtr.Zero, SdcApply | topologyFlag);
        if (result != 0)
        {
            throw new Win32Exception(result, "切换 Windows 投影模式失败。");
        }
    }

}
