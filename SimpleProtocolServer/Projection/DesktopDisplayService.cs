// 本文件是 Windows 系统 API 的薄封装：上层窗体不需要了解 P/Invoke 的细节。
using System.ComponentModel;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace SimpleProtocolServer.Projection;

/// <summary>
/// 通过 Windows API 切换桌面图片和投影模式。
/// P/Invoke（DllImport）允许托管的 C# 代码调用 user32.dll 中的原生函数。
/// </summary>
internal sealed class DesktopDisplayService : IDesktopDisplayService
{
    // SystemParametersInfo 使用的操作编号和通知标志。
    private const uint SpiGetDesktopWallpaper = 0x0073;
    private const uint SpiSetDesktopWallpaper = 0x0014;
    private const uint SpifUpdateIniFile = 0x0001;
    private const uint SpifSendWinIniChange = 0x0002;

    // SetDisplayConfig 使用的投影拓扑标志，对应 Win+P 中的四种模式。
    private const uint SdcApply = 0x00000080;
    private const uint SdcTopologyInternal = 0x00000001;
    private const uint SdcTopologyClone = 0x00000002;
    private const uint SdcTopologyExtend = 0x00000004;
    private const uint SdcTopologyExternal = 0x00000008;

    // 设置壁纸时 pvParam 是字符串路径，因此使用 string 重载。
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(
        uint uiAction,
        uint uiParam,
        string pvParam,
        uint fWinIni);

    // 获取壁纸时 Windows 需要把路径写回缓冲区，因此使用 StringBuilder 重载。
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(
        uint uiAction,
        uint uiParam,
        StringBuilder pvParam,
        uint fWinIni);

    // 该 API 用来切换“仅电脑/复制/仅第二屏/扩展”等显示器拓扑。
    [DllImport("user32.dll")]
    private static extern int SetDisplayConfig(
        uint numPathArrayElements,
        IntPtr pathArray,
        uint numModeArrayElements,
        IntPtr modeArray,
        uint flags);

    /// <summary>读取当前桌面壁纸路径；读取失败时返回 null。</summary>
    public string? GetCurrentWallpaper()
    {
        var path = new StringBuilder(1024);
        return SystemParametersInfo(SpiGetDesktopWallpaper, (uint)path.Capacity, path, 0)
            ? path.ToString()
            : null;
    }

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

    /// <summary>把指定图片设置为 Windows 桌面壁纸。</summary>
    public void SetWallpaper(
        string imagePath,
        ProjectedImageTransform transform = ProjectedImageTransform.Original)
    {
        string fullPath = Path.GetFullPath(imagePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("投图文件不存在。", fullPath);
        }

        // Windows 对 PNG/JPEG 壁纸的兼容性受系统设置影响，先转换为 24 位 BMP。
        string wallpaperPath = PrepareWallpaperImage(fullPath, transform);
        // UpdateIniFile 保存设置；SendWinIniChange 通知桌面立即刷新。
        bool success = SystemParametersInfo(
            SpiSetDesktopWallpaper,
            0,
            wallpaperPath,
            SpifUpdateIniFile | SpifSendWinIniChange);

        if (!success)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "切换桌面图片失败。");
        }
    }

    /// <summary>
    /// 如果输入不是 BMP，则转换为 24 位 BMP 并放入临时缓存。
    /// 相同文件未改变时会复用缓存，避免每次投图都重复转换。
    /// </summary>
    internal static string ConvertToBmpIfNeeded(string imagePath)
        => PrepareWallpaperImage(imagePath, ProjectedImageTransform.Original);

    /// <summary>
    /// 根据所选效果处理图片，并生成 Windows 壁纸接口兼容的 24 位 BMP。
    /// 原图模式下如果输入已经是 BMP，会直接返回原路径。
    /// </summary>
    internal static string PrepareWallpaperImage(
        string imagePath,
        ProjectedImageTransform transform)
    {
        if (transform == ProjectedImageTransform.Original &&
            string.Equals(Path.GetExtension(imagePath), ".bmp", StringComparison.OrdinalIgnoreCase))
        {
            return imagePath;
        }

        // 路径、大小和修改时间共同生成缓存键；图片变化后会得到新的缓存文件名。
        var sourceInfo = new FileInfo(imagePath);
        string cacheKey =
            $"{sourceInfo.FullName}|{sourceInfo.Length}|{sourceInfo.LastWriteTimeUtc.Ticks}|{transform}";
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(cacheKey)))[..20];
        string cacheDirectory = Path.Combine(
            Path.GetTempPath(), "SimpleProtocolServer", "WallpaperCache");
        Directory.CreateDirectory(cacheDirectory);
        string bmpPath = Path.Combine(cacheDirectory, $"wallpaper-{hash}.bmp");

        if (File.Exists(bmpPath)) return bmpPath;

        // 先写 .tmp，再原子移动到正式文件，避免程序中断后留下半个 BMP。
        string temporaryPath = bmpPath + ".tmp";
        try
        {
            using Image source = Image.FromFile(imagePath);
            ProjectedImageTransformer.Apply(source, transform);
            using var bitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format24bppRgb);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                // 透明 PNG 转为不支持透明的 BMP 时，用黑色填充透明区域。
                graphics.Clear(Color.Black);
                graphics.DrawImage(source, 0, 0, source.Width, source.Height);
            }

            bitmap.Save(temporaryPath, ImageFormat.Bmp);
            File.Move(temporaryPath, bmpPath, overwrite: true);
            return bmpPath;
        }
        finally
        {
            // 转换失败也要清理临时文件；缓存目录中的正式 BMP 会保留供以后复用。
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }
}
