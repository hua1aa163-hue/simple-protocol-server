using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using AutoTestClient.DataProcessing;
using AutoTestClient.Models;

namespace AutoTestClient;

/// <summary>在客户端内查看串扰热图和统计摘要；原始 Excel/CSV 仍保留在输出目录。</summary>
public partial class CrosstalkResultForm : Form
{
    private readonly TestDataProcessingResult _result;
    private readonly bool _isDesignTime;
    private Image? _image;

    /// <summary>Visual Studio WinForms Designer 使用的无参入口。</summary>
    public CrosstalkResultForm()
        : this(CreateDesignTimeResult(), designTime: true)
    {
    }

    public CrosstalkResultForm(TestDataProcessingResult result)
        : this(result, designTime: false)
    {
    }

    private CrosstalkResultForm(TestDataProcessingResult result, bool designTime)
    {
        ArgumentNullException.ThrowIfNull(result);
        _result = result;
        _isDesignTime = designTime || LicenseManager.UsageMode == LicenseUsageMode.Designtime;
        InitializeComponent();
        if (_isDesignTime)
        {
            // 设计器只展示安全的示例文本，不从 ExportFile 读取任何图片。
            labelSummary.Text = "设计器预览：串扰结果摘要（运行时显示实际指标和热图）";
        }
        else
        {
            Load += CrosstalkResultForm_Load;
        }
    }

    private static TestDataProcessingResult CreateDesignTimeResult() =>
        new(
            "串扰（设计器示例）",
            TestProjectKind.Crosstalk,
            new[]
            {
                new TestMetric("crosstalk.max", "串扰最大值", "待测", "设计器示例"),
                new TestMetric("crosstalk.mean", "串扰平均值", "待测", "设计器示例")
            },
            detail: "设计器示例数据，不代表实际测量结果。");

    private void CrosstalkResultForm_Load(object? sender, EventArgs e)
    {
        if (_isDesignTime) return;
        var values = _result.Metrics.Select(m => $"{m.DisplayName}={m.Value}").ToArray();
        labelSummary.Text = string.Join("    ", values) + Environment.NewLine + (_result.OutputDirectory ?? "");
        if (!string.IsNullOrWhiteSpace(_result.HeatmapPath) && File.Exists(_result.HeatmapPath))
        {
            try
            {
                using Image source = Image.FromFile(_result.HeatmapPath);
                _image = new Bitmap(source);
                pictureHeatmap.Image = _image;
            }
            catch (Exception ex) { labelSummary.Text += Environment.NewLine + $"热图读取失败：{ex.Message}"; }
        }
    }

    private void ButtonOpenFolder_Click(object? sender, EventArgs e)
    {
        if (_isDesignTime) return;
        if (string.IsNullOrWhiteSpace(_result.OutputDirectory) || !Directory.Exists(_result.OutputDirectory)) return;
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_result.OutputDirectory}\"") { UseShellExecute = true });
    }

}
