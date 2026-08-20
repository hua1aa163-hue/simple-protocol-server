// 这是串扰结果预览窗体的 WinForms 设计器文件。
// 图片框和按钮均可在 Visual Studio“查看设计器”中直接移动、缩放和修改属性。
#nullable disable

namespace SimpleProtocolServer;

partial class CrosstalkResultForm
{
    private System.ComponentModel.IContainer components = null;

    /// <summary>释放预览图片和设计器组件，避免大尺寸 PNG 长时间占用内存。</summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            picHeatmap?.Image?.Dispose();
            components?.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>创建结果图片框和关闭按钮；固定控件不在业务代码中动态生成。</summary>
    private void InitializeComponent()
    {
        picHeatmap = new PictureBox();
        pnlBottom = new Panel();
        btnClose = new Button();
        ((System.ComponentModel.ISupportInitialize)picHeatmap).BeginInit();
        pnlBottom.SuspendLayout();
        SuspendLayout();
        //
        // picHeatmap
        //
        picHeatmap.BackColor = Color.White;
        picHeatmap.Dock = DockStyle.Fill;
        picHeatmap.Location = new Point(0, 0);
        picHeatmap.Name = "picHeatmap";
        picHeatmap.Size = new Size(1384, 795);
        picHeatmap.SizeMode = PictureBoxSizeMode.Zoom;
        picHeatmap.TabIndex = 0;
        picHeatmap.TabStop = false;
        //
        // pnlBottom
        //
        pnlBottom.Controls.Add(btnClose);
        pnlBottom.Dock = DockStyle.Bottom;
        pnlBottom.Location = new Point(0, 795);
        pnlBottom.Name = "pnlBottom";
        pnlBottom.Size = new Size(1384, 66);
        pnlBottom.TabIndex = 1;
        //
        // btnClose
        //
        btnClose.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnClose.Location = new Point(1252, 12);
        btnClose.Name = "btnClose";
        btnClose.Size = new Size(120, 42);
        btnClose.TabIndex = 0;
        btnClose.Text = "关闭";
        btnClose.UseVisualStyleBackColor = true;
        btnClose.Click += btnClose_Click;
        //
        // CrosstalkResultForm
        //
        AcceptButton = btnClose;
        AutoScaleDimensions = new SizeF(9F, 20F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1384, 861);
        Controls.Add(picHeatmap);
        Controls.Add(pnlBottom);
        MaximizeBox = true;
        MinimizeBox = true;
        MinimumSize = new Size(900, 600);
        Name = "CrosstalkResultForm";
        ShowIcon = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "串扰测试完成";
        WindowState = FormWindowState.Maximized;
        ((System.ComponentModel.ISupportInitialize)picHeatmap).EndInit();
        pnlBottom.ResumeLayout(false);
        ResumeLayout(false);
    }

    #endregion

    private PictureBox picHeatmap;
    private Panel pnlBottom;
    private Button btnClose;
}
