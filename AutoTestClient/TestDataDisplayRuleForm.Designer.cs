#nullable disable
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace AutoTestClient
{
    partial class TestDataDisplayRuleForm
    {
        private IContainer components = null;
        private TableLayoutPanel rootLayout;
        private Label labelDescription;
        private DataGridView gridRules;
        private DataGridViewCheckBoxColumn ruleEnabledColumn;
        private DataGridViewTextBoxColumn ruleOrderColumn;
        private DataGridViewComboBoxColumn ruleProjectKindColumn;
        private DataGridViewTextBoxColumn ruleNameColumn;
        private DataGridViewTextBoxColumn ruleWorksheetColumn;
        private DataGridViewTextBoxColumn ruleNameCellColumn;
        private DataGridViewTextBoxColumn ruleCellRangeColumn;
        private DataGridViewTextBoxColumn ruleTemplateColumn;
        private DataGridViewComboBoxColumn ruleAggregationColumn;
        private DataGridViewTextBoxColumn ruleOutputColumn;
        private TableLayoutPanel bottomLayout;
        private FlowLayoutPanel editButtons;
        private FlowLayoutPanel dialogButtons;
        private Button buttonAddRule;
        private Button buttonDeleteRule;
        private Button buttonMoveUp;
        private Button buttonMoveDown;
        private Button buttonSave;
        private Button buttonCancel;
        private Label labelStatus;

        private void InitializeComponent()
        {
            components = new Container();
            DataGridViewCellStyle headerStyle = new DataGridViewCellStyle();
            DataGridViewCellStyle cellStyle = new DataGridViewCellStyle();
            rootLayout = new TableLayoutPanel();
            labelDescription = new Label();
            gridRules = new DataGridView();
            ruleEnabledColumn = new DataGridViewCheckBoxColumn();
            ruleOrderColumn = new DataGridViewTextBoxColumn();
            ruleProjectKindColumn = new DataGridViewComboBoxColumn();
            ruleNameColumn = new DataGridViewTextBoxColumn();
            ruleWorksheetColumn = new DataGridViewTextBoxColumn();
            ruleNameCellColumn = new DataGridViewTextBoxColumn();
            ruleCellRangeColumn = new DataGridViewTextBoxColumn();
            ruleTemplateColumn = new DataGridViewTextBoxColumn();
            ruleAggregationColumn = new DataGridViewComboBoxColumn();
            ruleOutputColumn = new DataGridViewTextBoxColumn();
            bottomLayout = new TableLayoutPanel();
            editButtons = new FlowLayoutPanel();
            dialogButtons = new FlowLayoutPanel();
            buttonAddRule = new Button();
            buttonDeleteRule = new Button();
            buttonMoveUp = new Button();
            buttonMoveDown = new Button();
            buttonSave = new Button();
            buttonCancel = new Button();
            labelStatus = new Label();
            ((ISupportInitialize)gridRules).BeginInit();
            rootLayout.SuspendLayout();
            gridRules.SuspendLayout();
            bottomLayout.SuspendLayout();
            editButtons.SuspendLayout();
            dialogButtons.SuspendLayout();
            SuspendLayout();
            // 
            // rootLayout
            // 
            rootLayout.ColumnCount = 1;
            rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            rootLayout.Controls.Add(labelDescription, 0, 0);
            rootLayout.Controls.Add(gridRules, 0, 1);
            rootLayout.Controls.Add(bottomLayout, 0, 2);
            rootLayout.Dock = DockStyle.Fill;
            rootLayout.Location = new Point(0, 0);
            rootLayout.Margin = new Padding(0);
            rootLayout.Name = "rootLayout";
            rootLayout.Padding = new Padding(10);
            rootLayout.RowCount = 3;
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            rootLayout.Size = new Size(1120, 620);
            rootLayout.TabIndex = 0;
            // 
            // labelDescription
            // 
            labelDescription.AutoEllipsis = true;
            labelDescription.Dock = DockStyle.Fill;
            labelDescription.Location = new Point(13, 10);
            labelDescription.Margin = new Padding(3, 0, 3, 4);
            labelDescription.Name = "labelDescription";
            labelDescription.Size = new Size(1094, 34);
            labelDescription.TabIndex = 0;
            labelDescription.Text = "按项目类型把 MRTEST Excel 的工作表、名称单元格/固定标签、数据单元格/范围映射到输出列；双击单元格即可编辑。输出列非空时作为界面指标名，清空后使用名称单元格。默认读取每批最深层明细 Excel，色域默认使用 Chromaticity!H4/C23。";
            labelDescription.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // gridRules
            // 
            gridRules.AllowUserToAddRows = false;
            gridRules.AllowUserToDeleteRows = false;
            gridRules.AllowUserToResizeRows = false;
            gridRules.AutoGenerateColumns = false;
            gridRules.BackgroundColor = Color.White;
            gridRules.BorderStyle = BorderStyle.Fixed3D;
            headerStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            headerStyle.BackColor = SystemColors.Control;
            headerStyle.Font = new Font("Microsoft YaHei UI", 9F);
            headerStyle.ForeColor = SystemColors.WindowText;
            headerStyle.SelectionBackColor = SystemColors.Highlight;
            headerStyle.SelectionForeColor = SystemColors.HighlightText;
            headerStyle.WrapMode = DataGridViewTriState.True;
            gridRules.ColumnHeadersDefaultCellStyle = headerStyle;
            gridRules.ColumnHeadersHeight = 32;
            gridRules.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            gridRules.Columns.AddRange(new DataGridViewColumn[] {
                ruleEnabledColumn,
                ruleOrderColumn,
                ruleProjectKindColumn,
                ruleNameColumn,
                ruleWorksheetColumn,
                ruleNameCellColumn,
                ruleCellRangeColumn,
                ruleTemplateColumn,
                ruleAggregationColumn,
                ruleOutputColumn });
            cellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            cellStyle.BackColor = SystemColors.Window;
            cellStyle.Font = new Font("Microsoft YaHei UI", 9F);
            cellStyle.ForeColor = SystemColors.ControlText;
            cellStyle.SelectionBackColor = SystemColors.Highlight;
            cellStyle.SelectionForeColor = SystemColors.HighlightText;
            cellStyle.WrapMode = DataGridViewTriState.False;
            gridRules.DefaultCellStyle = cellStyle;
            gridRules.Dock = DockStyle.Fill;
            gridRules.EditMode = DataGridViewEditMode.EditOnEnter;
            gridRules.Location = new Point(13, 52);
            gridRules.Margin = new Padding(3, 0, 3, 4);
            gridRules.MultiSelect = false;
            gridRules.Name = "gridRules";
            gridRules.RowHeadersVisible = false;
            gridRules.RowTemplate.Height = 28;
            gridRules.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            gridRules.Size = new Size(1094, 500);
            gridRules.TabIndex = 1;
            gridRules.CellEndEdit += GridRules_CellEndEdit;
            gridRules.CurrentCellDirtyStateChanged += GridRules_CurrentCellDirtyStateChanged;
            gridRules.DataError += GridRules_DataError;
            // 
            // ruleEnabledColumn
            // 
            ruleEnabledColumn.HeaderText = "启用";
            ruleEnabledColumn.MinimumWidth = 48;
            ruleEnabledColumn.Name = "ruleEnabledColumn";
            ruleEnabledColumn.Width = 48;
            // 
            // ruleOrderColumn
            // 
            ruleOrderColumn.HeaderText = "顺序";
            ruleOrderColumn.MinimumWidth = 52;
            ruleOrderColumn.Name = "ruleOrderColumn";
            ruleOrderColumn.SortMode = DataGridViewColumnSortMode.NotSortable;
            ruleOrderColumn.Width = 52;
            // 
            // ruleProjectKindColumn
            // 
            ruleProjectKindColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            ruleProjectKindColumn.DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton;
            ruleProjectKindColumn.DisplayStyleForCurrentCellOnly = true;
            ruleProjectKindColumn.FillWeight = 95F;
            ruleProjectKindColumn.FlatStyle = FlatStyle.Standard;
            ruleProjectKindColumn.HeaderText = "项目类型";
            ruleProjectKindColumn.Items.AddRange(new object[] {
                "全部项目", "通用", "FOV", "黑白对比度", "亮度均匀性", "色域", "串扰", "畸变" });
            ruleProjectKindColumn.MinimumWidth = 110;
            ruleProjectKindColumn.Name = "ruleProjectKindColumn";
            ruleProjectKindColumn.SortMode = DataGridViewColumnSortMode.NotSortable;
            // 
            // ruleNameColumn
            // 
            ruleNameColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            ruleNameColumn.FillWeight = 100F;
            ruleNameColumn.HeaderText = "规则名称";
            ruleNameColumn.MinimumWidth = 110;
            ruleNameColumn.Name = "ruleNameColumn";
            ruleNameColumn.SortMode = DataGridViewColumnSortMode.NotSortable;
            // 
            // ruleWorksheetColumn
            // 
            ruleWorksheetColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            ruleWorksheetColumn.FillWeight = 80F;
            ruleWorksheetColumn.HeaderText = "Excel工作表";
            ruleWorksheetColumn.MinimumWidth = 100;
            ruleWorksheetColumn.Name = "ruleWorksheetColumn";
            ruleWorksheetColumn.SortMode = DataGridViewColumnSortMode.NotSortable;
            // 
            // ruleNameCellColumn
            // 
            ruleNameCellColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            ruleNameCellColumn.FillWeight = 115F;
            ruleNameCellColumn.HeaderText = "名称单元格/固定标签";
            ruleNameCellColumn.MinimumWidth = 140;
            ruleNameCellColumn.Name = "ruleNameCellColumn";
            ruleNameCellColumn.SortMode = DataGridViewColumnSortMode.NotSortable;
            // 
            // ruleCellRangeColumn
            // 
            ruleCellRangeColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            ruleCellRangeColumn.FillWeight = 110F;
            ruleCellRangeColumn.HeaderText = "数据单元格/范围";
            ruleCellRangeColumn.MinimumWidth = 120;
            ruleCellRangeColumn.Name = "ruleCellRangeColumn";
            ruleCellRangeColumn.SortMode = DataGridViewColumnSortMode.NotSortable;
            // 
            // ruleTemplateColumn
            // 
            ruleTemplateColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            ruleTemplateColumn.FillWeight = 145F;
            ruleTemplateColumn.HeaderText = "名称模板";
            ruleTemplateColumn.MinimumWidth = 150;
            ruleTemplateColumn.Name = "ruleTemplateColumn";
            ruleTemplateColumn.SortMode = DataGridViewColumnSortMode.NotSortable;
            // 
            // ruleAggregationColumn
            // 
            ruleAggregationColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            ruleAggregationColumn.DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton;
            ruleAggregationColumn.DisplayStyleForCurrentCellOnly = true;
            ruleAggregationColumn.FillWeight = 75F;
            ruleAggregationColumn.FlatStyle = FlatStyle.Standard;
            ruleAggregationColumn.HeaderText = "聚合方式";
            ruleAggregationColumn.Items.AddRange(new object[] {
                "不聚合", "平均值", "最小值", "最大值", "求和", "计数", "第一个", "最后一个" });
            ruleAggregationColumn.MinimumWidth = 88;
            ruleAggregationColumn.Name = "ruleAggregationColumn";
            ruleAggregationColumn.SortMode = DataGridViewColumnSortMode.NotSortable;
            // 
            // ruleOutputColumn
            // 
            ruleOutputColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            ruleOutputColumn.FillWeight = 110F;
            ruleOutputColumn.HeaderText = "输出列";
            ruleOutputColumn.MinimumWidth = 110;
            ruleOutputColumn.Name = "ruleOutputColumn";
            ruleOutputColumn.SortMode = DataGridViewColumnSortMode.NotSortable;
            // 
            // bottomLayout
            // 
            bottomLayout.ColumnCount = 3;
            bottomLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 390F));
            bottomLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            bottomLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190F));
            bottomLayout.Controls.Add(editButtons, 0, 0);
            bottomLayout.Controls.Add(labelStatus, 1, 0);
            bottomLayout.Controls.Add(dialogButtons, 2, 0);
            bottomLayout.Dock = DockStyle.Fill;
            bottomLayout.Location = new Point(13, 556);
            bottomLayout.Margin = new Padding(3, 0, 3, 0);
            bottomLayout.Name = "bottomLayout";
            bottomLayout.RowCount = 1;
            bottomLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            bottomLayout.Size = new Size(1094, 44);
            bottomLayout.TabIndex = 2;
            // 
            // editButtons
            // 
            editButtons.Controls.Add(buttonAddRule);
            editButtons.Controls.Add(buttonDeleteRule);
            editButtons.Controls.Add(buttonMoveUp);
            editButtons.Controls.Add(buttonMoveDown);
            editButtons.Dock = DockStyle.Fill;
            editButtons.FlowDirection = FlowDirection.LeftToRight;
            editButtons.Location = new Point(3, 3);
            editButtons.Margin = new Padding(3);
            editButtons.Name = "editButtons";
            editButtons.Padding = new Padding(0, 3, 0, 0);
            editButtons.Size = new Size(384, 38);
            editButtons.TabIndex = 0;
            editButtons.WrapContents = false;
            // 
            // buttonAddRule
            // 
            buttonAddRule.AutoSize = true;
            buttonAddRule.Location = new Point(3, 6);
            buttonAddRule.Margin = new Padding(3, 3, 5, 3);
            buttonAddRule.Name = "buttonAddRule";
            buttonAddRule.Size = new Size(86, 29);
            buttonAddRule.TabIndex = 0;
            buttonAddRule.Text = "新增规则";
            buttonAddRule.UseVisualStyleBackColor = true;
            buttonAddRule.Click += ButtonAddRule_Click;
            // 
            // buttonDeleteRule
            // 
            buttonDeleteRule.AutoSize = true;
            buttonDeleteRule.Location = new Point(97, 6);
            buttonDeleteRule.Margin = new Padding(3, 3, 5, 3);
            buttonDeleteRule.Name = "buttonDeleteRule";
            buttonDeleteRule.Size = new Size(86, 29);
            buttonDeleteRule.TabIndex = 1;
            buttonDeleteRule.Text = "删除规则";
            buttonDeleteRule.UseVisualStyleBackColor = true;
            buttonDeleteRule.Click += ButtonDeleteRule_Click;
            // 
            // buttonMoveUp
            // 
            buttonMoveUp.AutoSize = true;
            buttonMoveUp.Location = new Point(191, 6);
            buttonMoveUp.Margin = new Padding(3, 3, 5, 3);
            buttonMoveUp.Name = "buttonMoveUp";
            buttonMoveUp.Size = new Size(48, 29);
            buttonMoveUp.TabIndex = 2;
            buttonMoveUp.Text = "上移";
            buttonMoveUp.UseVisualStyleBackColor = true;
            buttonMoveUp.Click += ButtonMoveUp_Click;
            // 
            // buttonMoveDown
            // 
            buttonMoveDown.AutoSize = true;
            buttonMoveDown.Location = new Point(249, 6);
            buttonMoveDown.Margin = new Padding(3, 3, 5, 3);
            buttonMoveDown.Name = "buttonMoveDown";
            buttonMoveDown.Size = new Size(48, 29);
            buttonMoveDown.TabIndex = 3;
            buttonMoveDown.Text = "下移";
            buttonMoveDown.UseVisualStyleBackColor = true;
            buttonMoveDown.Click += ButtonMoveDown_Click;
            // 
            // labelStatus
            // 
            labelStatus.AutoEllipsis = true;
            labelStatus.Dock = DockStyle.Fill;
            labelStatus.ForeColor = Color.DimGray;
            labelStatus.Location = new Point(396, 0);
            labelStatus.Margin = new Padding(3, 0, 3, 0);
            labelStatus.Name = "labelStatus";
            labelStatus.Size = new Size(502, 44);
            labelStatus.TabIndex = 1;
            labelStatus.Text = "暂无规则。点击“新增规则”开始配置。";
            labelStatus.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // dialogButtons
            // 
            dialogButtons.Controls.Add(buttonSave);
            dialogButtons.Controls.Add(buttonCancel);
            dialogButtons.Dock = DockStyle.Fill;
            dialogButtons.FlowDirection = FlowDirection.RightToLeft;
            dialogButtons.Location = new Point(904, 3);
            dialogButtons.Margin = new Padding(3);
            dialogButtons.Name = "dialogButtons";
            dialogButtons.Padding = new Padding(0, 3, 0, 0);
            dialogButtons.Size = new Size(184, 38);
            dialogButtons.TabIndex = 2;
            dialogButtons.WrapContents = false;
            // 
            // buttonSave
            // 
            buttonSave.DialogResult = DialogResult.None;
            buttonSave.Location = new Point(101, 6);
            buttonSave.Margin = new Padding(3, 3, 3, 3);
            buttonSave.Name = "buttonSave";
            buttonSave.Size = new Size(80, 29);
            buttonSave.TabIndex = 0;
            buttonSave.Text = "保存";
            buttonSave.UseVisualStyleBackColor = true;
            buttonSave.Click += ButtonSave_Click;
            // 
            // buttonCancel
            // 
            buttonCancel.DialogResult = DialogResult.Cancel;
            buttonCancel.Location = new Point(15, 6);
            buttonCancel.Margin = new Padding(3, 3, 3, 3);
            buttonCancel.Name = "buttonCancel";
            buttonCancel.Size = new Size(80, 29);
            buttonCancel.TabIndex = 1;
            buttonCancel.Text = "取消";
            buttonCancel.UseVisualStyleBackColor = true;
            buttonCancel.Click += ButtonCancel_Click;
            // 
            // TestDataDisplayRuleForm
            // 
            AcceptButton = buttonSave;
            AutoScaleDimensions = new SizeF(7F, 17F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(245, 247, 250);
            CancelButton = buttonCancel;
            ClientSize = new Size(1120, 620);
            Controls.Add(rootLayout);
            Font = new Font("Microsoft YaHei UI", 9F);
            MinimumSize = new Size(850, 420);
            Name = "TestDataDisplayRuleForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "测试数据展示规则";
            ((ISupportInitialize)gridRules).EndInit();
            rootLayout.ResumeLayout(false);
            gridRules.ResumeLayout(false);
            bottomLayout.ResumeLayout(false);
            editButtons.ResumeLayout(false);
            editButtons.PerformLayout();
            dialogButtons.ResumeLayout(false);
            ResumeLayout(false);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }
    }
}
