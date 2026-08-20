// 本文件统一处理预览和第二屏所需的图片读取，避免不同入口对 TIFF 的支持不一致。
namespace SimpleProtocolServer.Projection;

/// <summary>读取常见位图格式并返回不占用源文件的内存副本。</summary>
internal static class ProjectedImageLoader
{
    /// <summary>
    /// 读取 PNG、BMP、JPG、JPEG、TIF 或 TIFF；多页 TIFF 按 Windows 默认规则显示第一帧。
    /// 返回的 Bitmap 由调用方负责释放，方法返回后源文件不会继续被锁定。
    /// </summary>
    internal static Bitmap LoadBitmapCopy(string imagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);
        string fullPath = Path.GetFullPath(imagePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("投图文件不存在。", fullPath);
        }

        // 启用嵌入色彩配置，标准 TIFF/JPEG 中的色彩信息会按 Windows 图像组件处理。
        using Image source = Image.FromFile(fullPath, useEmbeddedColorManagement: true);
        return new Bitmap(source);
    }
}
