#nullable disable
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace AutoTestClient
{
    partial class CrosstalkResultForm
    {
        private IContainer components;
        private TableLayoutPanel topLayout;
        private PictureBox pictureHeatmap;
        private Label labelSummary;
        private Button buttonOpenFolder;

        private void InitializeComponent()
        {
            this.components = new Container();
            this.topLayout = new TableLayoutPanel();
            this.pictureHeatmap = new PictureBox();
            this.labelSummary = new Label();
            this.buttonOpenFolder = new Button();
            ((ISupportInitialize)(this.pictureHeatmap)).BeginInit();
            this.SuspendLayout();

            // CrosstalkResultForm
            this.AutoScaleDimensions = new SizeF(7F, 17F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.BackColor = Color.FromArgb(245, 247, 250);
            this.ClientSize = new Size(1100, 760);
            this.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.MinimumSize = new Size(700, 500);
            this.Name = "CrosstalkResultForm";
            this.StartPosition = FormStartPosition.CenterParent;
            this.Text = "串扰结果";

            // topLayout
            this.topLayout.AutoSize = false;
            this.topLayout.BackColor = Color.White;
            this.topLayout.ColumnCount = 2;
            this.topLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            this.topLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132F));
            this.topLayout.Dock = DockStyle.Top;
            this.topLayout.GrowStyle = TableLayoutPanelGrowStyle.FixedSize;
            this.topLayout.Height = 58;
            this.topLayout.Margin = new Padding(0);
            this.topLayout.Name = "topLayout";
            this.topLayout.Padding = new Padding(8, 6, 8, 6);
            this.topLayout.RowCount = 1;
            this.topLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            this.topLayout.Controls.Add(this.labelSummary, 0, 0);
            this.topLayout.Controls.Add(this.buttonOpenFolder, 1, 0);

            // pictureHeatmap
            this.pictureHeatmap.BackColor = Color.White;
            this.pictureHeatmap.Dock = DockStyle.Fill;
            this.pictureHeatmap.Margin = new Padding(0);
            this.pictureHeatmap.Name = "pictureHeatmap";
            this.pictureHeatmap.SizeMode = PictureBoxSizeMode.Zoom;

            // labelSummary
            this.labelSummary.AutoEllipsis = true;
            this.labelSummary.Dock = DockStyle.Fill;
            this.labelSummary.Margin = new Padding(0, 0, 8, 0);
            this.labelSummary.Name = "labelSummary";
            this.labelSummary.Padding = new Padding(4, 0, 4, 0);
            this.labelSummary.Text = "串扰结果摘要（运行时显示实际指标和热图）";
            this.labelSummary.TextAlign = ContentAlignment.MiddleLeft;

            // buttonOpenFolder
            this.buttonOpenFolder.Dock = DockStyle.Fill;
            this.buttonOpenFolder.Margin = new Padding(0);
            this.buttonOpenFolder.Name = "buttonOpenFolder";
            this.buttonOpenFolder.Text = "打开结果目录";
            this.buttonOpenFolder.UseVisualStyleBackColor = true;
            this.buttonOpenFolder.Click += new EventHandler(this.ButtonOpenFolder_Click);

            this.Controls.Add(this.pictureHeatmap);
            this.Controls.Add(this.topLayout);
            ((ISupportInitialize)(this.pictureHeatmap)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this._image != null)
                {
                    this._image.Dispose();
                    this._image = null;
                }
                if (this.components != null)
                {
                    this.components.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}
