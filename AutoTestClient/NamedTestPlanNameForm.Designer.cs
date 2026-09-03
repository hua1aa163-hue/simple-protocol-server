#nullable disable
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace AutoTestClient
{
    partial class NamedTestPlanNameForm
    {
        private IContainer components = null;
        private Label labelName;
        private TextBox textBoxName;
        private FlowLayoutPanel buttonPanel;
        private Button buttonOk;
        private Button buttonCancel;

        private void InitializeComponent()
        {
            components = new Container();
            labelName = new Label();
            textBoxName = new TextBox();
            buttonPanel = new FlowLayoutPanel();
            buttonOk = new Button();
            buttonCancel = new Button();
            buttonPanel.SuspendLayout();
            SuspendLayout();
            // 
            // labelName
            // 
            labelName.AutoSize = true;
            labelName.Location = new Point(14, 14);
            labelName.Name = "labelName";
            labelName.Size = new Size(68, 17);
            labelName.TabIndex = 0;
            labelName.Text = "计划名称：";
            // 
            // textBoxName
            // 
            textBoxName.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            textBoxName.Location = new Point(14, 38);
            textBoxName.MaxLength = 80;
            textBoxName.Name = "textBoxName";
            textBoxName.Size = new Size(362, 23);
            textBoxName.TabIndex = 1;
            // 
            // buttonPanel
            // 
            buttonPanel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            buttonPanel.Controls.Add(buttonCancel);
            buttonPanel.Controls.Add(buttonOk);
            buttonPanel.FlowDirection = FlowDirection.RightToLeft;
            buttonPanel.Location = new Point(14, 76);
            buttonPanel.Name = "buttonPanel";
            buttonPanel.Padding = new Padding(0, 2, 0, 0);
            buttonPanel.Size = new Size(362, 36);
            buttonPanel.TabIndex = 2;
            buttonPanel.WrapContents = false;
            // 
            // buttonOk
            // 
            buttonOk.DialogResult = DialogResult.None;
            buttonOk.Margin = new Padding(3, 2, 0, 2);
            buttonOk.Name = "buttonOk";
            buttonOk.Size = new Size(80, 29);
            buttonOk.TabIndex = 0;
            buttonOk.Text = "确定";
            buttonOk.UseVisualStyleBackColor = true;
            buttonOk.Click += ButtonOk_Click;
            // 
            // buttonCancel
            // 
            buttonCancel.DialogResult = DialogResult.Cancel;
            buttonCancel.Margin = new Padding(3, 2, 3, 2);
            buttonCancel.Name = "buttonCancel";
            buttonCancel.Size = new Size(80, 29);
            buttonCancel.TabIndex = 1;
            buttonCancel.Text = "取消";
            buttonCancel.UseVisualStyleBackColor = true;
            buttonCancel.Click += ButtonCancel_Click;
            // 
            // NamedTestPlanNameForm
            // 
            AcceptButton = buttonOk;
            AutoScaleDimensions = new SizeF(7F, 17F);
            AutoScaleMode = AutoScaleMode.Font;
            CancelButton = buttonCancel;
            ClientSize = new Size(390, 124);
            Controls.Add(buttonPanel);
            Controls.Add(textBoxName);
            Controls.Add(labelName);
            Font = new Font("Microsoft YaHei UI", 9F);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "NamedTestPlanNameForm";
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = "另存测试计划";
            buttonPanel.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }
    }
}
