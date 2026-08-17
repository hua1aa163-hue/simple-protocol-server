// 这是第二屏全屏投图窗口的 WinForms 设计器文件，固定控件均可在设计器中调节。
#nullable disable

namespace SimpleProtocolServer;

partial class SecondScreenProjectionForm
{
    private System.ComponentModel.IContainer components = null;

    /// <summary>释放像素画布和设计器组件。</summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            components?.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>创建第二屏像素画布，并设置无边框、置顶和黑色背景。</summary>
    private void InitializeComponent()
    {
        pixelPerfectCanvas = new Projection.PixelPerfectImageControl();
        SuspendLayout();
        //
        // pixelPerfectCanvas
        //
        pixelPerfectCanvas.BackColor = Color.Black;
        pixelPerfectCanvas.Dock = DockStyle.Fill;
        pixelPerfectCanvas.Location = new Point(0, 0);
        pixelPerfectCanvas.Name = "pixelPerfectCanvas";
        pixelPerfectCanvas.Size = new Size(800, 450);
        pixelPerfectCanvas.TabIndex = 0;
        //
        // SecondScreenProjectionForm
        //
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Color.Black;
        ClientSize = new Size(800, 450);
        ControlBox = false;
        Controls.Add(pixelPerfectCanvas);
        FormBorderStyle = FormBorderStyle.None;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "SecondScreenProjectionForm";
        ShowIcon = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Text = "第二屏全屏投图";
        TopMost = true;
        ResumeLayout(false);
    }

    #endregion

    private Projection.PixelPerfectImageControl pixelPerfectCanvas;
}
