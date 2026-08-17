// 这是第二屏全屏投图窗口的 WinForms 设计器文件，固定控件均可在设计器中调节。
#nullable disable

namespace SimpleProtocolServer;

partial class SecondScreenProjectionForm
{
    private System.ComponentModel.IContainer components = null;

    /// <summary>释放当前投放图片和设计器组件。</summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            picProjectedImage?.Image?.Dispose();
            components?.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>创建第二屏投图 PictureBox，并设置无边框、置顶和黑色背景。</summary>
    private void InitializeComponent()
    {
        picProjectedImage = new PictureBox();
        ((System.ComponentModel.ISupportInitialize)picProjectedImage).BeginInit();
        SuspendLayout();
        //
        // picProjectedImage
        //
        picProjectedImage.BackColor = Color.Black;
        picProjectedImage.Dock = DockStyle.Fill;
        picProjectedImage.Location = new Point(0, 0);
        picProjectedImage.Name = "picProjectedImage";
        picProjectedImage.Size = new Size(800, 450);
        picProjectedImage.SizeMode = PictureBoxSizeMode.Zoom;
        picProjectedImage.TabIndex = 0;
        picProjectedImage.TabStop = false;
        //
        // SecondScreenProjectionForm
        //
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Color.Black;
        ClientSize = new Size(800, 450);
        ControlBox = false;
        Controls.Add(picProjectedImage);
        FormBorderStyle = FormBorderStyle.None;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "SecondScreenProjectionForm";
        ShowIcon = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Text = "第二屏全屏投图";
        TopMost = true;
        ((System.ComponentModel.ISupportInitialize)picProjectedImage).EndInit();
        ResumeLayout(false);
    }

    #endregion

    private PictureBox picProjectedImage;
}
