using System.Drawing;
using System.Windows.Forms;

namespace AutoTestClient.Projection;

/// <summary>
/// 1:1 第二屏窗口。
/// </summary>
/// <remarks>
/// 当前只提供最基础的承载能力，后续主程序可以把图像和刷新逻辑接进来。
/// </remarks>
public partial class ProjectionViewerForm : Form
{
    private readonly PictureBox _pictureBox;

    /// <summary>
    /// 初始化第二屏窗口。
    /// </summary>
    public ProjectionViewerForm()
    {
        _pictureBox = new PictureBox();
        InitializeComponent();
    }

    /// <summary>
    /// 当前显示模式。
    /// </summary>
    private ProjectionMode _mode = ProjectionMode.PixelPerfect;

    public ProjectionMode Mode
    {
        get => _mode;
        set
        {
            _mode = value;
            if (_pictureBox is not null)
                _pictureBox.SizeMode = value == ProjectionMode.FitToWindow
                    ? PictureBoxSizeMode.Zoom
                    : PictureBoxSizeMode.CenterImage;
        }
    }

    /// <summary>
    /// 设置要展示的图像。
    /// </summary>
    public void SetImage(Image? image)
    {
        _pictureBox.Image = image;
    }

    /// <summary>
    /// 将窗口移动到第二屏。
    /// </summary>
    public void MoveToSecondScreen()
    {
        var second = DisplayTopology.GetSecondScreen();
        if (second is null)
        {
            return;
        }

        StartPosition = FormStartPosition.Manual;
        WindowState = FormWindowState.Normal;
        Bounds = second.Bounds;
        WindowState = FormWindowState.Maximized;
    }

    private void InitializeComponent()
    {
        SuspendLayout();

        AutoScaleMode = AutoScaleMode.None;
        BackColor = Color.Black;
        ClientSize = new Size(800, 600);
        FormBorderStyle = FormBorderStyle.None;
        TopMost = true;
        ShowInTaskbar = false;
        KeyPreview = true;
        Name = nameof(ProjectionViewerForm);
        StartPosition = FormStartPosition.Manual;
        WindowState = FormWindowState.Maximized;

        _pictureBox.Dock = DockStyle.Fill;
        _pictureBox.BackColor = Color.Black;
        _pictureBox.SizeMode = PictureBoxSizeMode.CenterImage;

        Controls.Add(_pictureBox);

        Load += ProjectionViewerForm_Load;
        KeyDown += ProjectionViewerForm_KeyDown;

        ResumeLayout(false);
    }

    private void ProjectionViewerForm_Load(object? sender, System.EventArgs e)
    {
        MoveToSecondScreen();
    }

    private void ProjectionViewerForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            Close();
        }
    }
}
