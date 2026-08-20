// 串扰测试和数据处理成功后，用本窗体直接预览生成的热力图。
// 固定控件全部放在 CrosstalkResultForm.Designer.cs 中，便于在 Visual Studio 设计器中调节。
namespace SimpleProtocolServer;

/// <summary>显示串扰热力图的完成提示窗体，不再用文字列出最大值、最小值和平均值。</summary>
public partial class CrosstalkResultForm : Form
{
    /// <summary>供 Visual Studio WinForms 设计器使用。</summary>
    public CrosstalkResultForm()
    {
        InitializeComponent();
    }

    /// <summary>业务代码传入刚生成的 PNG，窗体会复制图片内容并立即释放源文件句柄。</summary>
    internal CrosstalkResultForm(string heatmapPath) : this()
    {
        if (string.IsNullOrWhiteSpace(heatmapPath))
        {
            throw new ArgumentException("热力图路径不能为空。", nameof(heatmapPath));
        }
        if (!File.Exists(heatmapPath))
        {
            throw new FileNotFoundException("没有找到串扰热力图。", heatmapPath);
        }

        // Image.FromFile 会持续占用文件，所以先复制到新的 Bitmap，再立即释放源 Image。
        using Image sourceImage = Image.FromFile(heatmapPath);
        picHeatmap.Image = new Bitmap(sourceImage);
        Text = $"串扰测试完成 - {Path.GetFileName(heatmapPath)}";
    }

    /// <summary>关闭按钮只关闭结果预览，不影响投影控制窗口和主程序。</summary>
    private void btnClose_Click(object? sender, EventArgs e) => Close();
}
