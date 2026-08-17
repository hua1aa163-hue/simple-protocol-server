// 该窗体是真正显示在第二屏幕上的无边框投图窗口，不会修改 Windows 桌面壁纸。
using SimpleProtocolServer.Projection;

namespace SimpleProtocolServer;

/// <summary>在外接非主屏上全屏显示一张经过方向/镜像处理的图片。</summary>
public partial class SecondScreenProjectionForm : Form
{
    /// <summary>创建只包含一个像素对像素画布的第二屏投图窗口。</summary>
    public SecondScreenProjectionForm()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 找到第一个非主屏的下标；返回 -1 表示当前只有主屏。
    /// 纯布尔输入使目标选择规则无需真实连接显示器也能自动测试。
    /// </summary>
    internal static int FindSecondScreenIndex(IReadOnlyList<bool> primaryFlags)
    {
        ArgumentNullException.ThrowIfNull(primaryFlags);
        for (int index = 0; index < primaryFlags.Count; index++)
        {
            if (!primaryFlags[index]) return index;
        }

        return -1;
    }

    /// <summary>加载、处理并全屏显示图片；原图片会立即释放，因此不会锁定源文件。</summary>
    internal void ShowImage(string imagePath, ProjectedImageTransform transform)
    {
        string fullPath = Path.GetFullPath(imagePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("投图文件不存在。", fullPath);
        }

        Screen[] screens = Screen.AllScreens;
        int targetIndex = FindSecondScreenIndex(screens.Select(screen => screen.Primary).ToArray());
        if (targetIndex < 0)
        {
            throw new InvalidOperationException(
                "未检测到第二屏幕。请先连接显示器，并在 Windows 中启用扩展屏幕。");
        }

        // 启用嵌入色彩配置读取，再复制原始像素并关闭源文件，避免锁住图片。
        Image? projectedImage;
        using (Image source = Image.FromFile(
                   fullPath,
                   useEmbeddedColorManagement: true))
        {
            projectedImage = new Bitmap(source);
        }

        try
        {
            ProjectedImageTransformer.Apply(projectedImage, transform);
            MoveToScreen(screens[targetIndex]);

            pixelPerfectCanvas.ReplaceImage(projectedImage);
            projectedImage = null; // 所有权已交给画布，关闭或下次切图时释放。

            if (!Visible)
            {
                // 不设置 Owner，第二屏窗口与控制窗口相互独立。
                Show();
            }

            BringToFront();
        }
        finally
        {
            // 只有在变换或显示过程中发生异常、尚未交给画布时才需要这里释放。
            projectedImage?.Dispose();
        }
    }

    /// <summary>将无边框窗口准确铺满目标屏幕，包括目标屏位于主屏左侧的负坐标情况。</summary>
    private void MoveToScreen(Screen targetScreen)
    {
        WindowState = FormWindowState.Normal;
        Bounds = targetScreen.Bounds;
    }
}
