#nullable disable
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace AutoTestClient
{
    partial class RecipeManagerForm
    {
        private IContainer components;
        private TableLayoutPanel rootLayout;
        private DataGridView gridProjects;
        private DataGridView gridSteps;
        private GroupBox groupProjects;
        private GroupBox groupSteps;
        private TableLayoutPanel projectLayout;
        private TableLayoutPanel stepLayout;
        private SplitContainer stepBindingSplit;
        private Panel bindingDetailsPanel;
        private TableLayoutPanel bindingDetailsLayout;
        private FlowLayoutPanel projectButtons;
        private FlowLayoutPanel stepButtons;
        private Button buttonAddProject;
        private Button buttonDeleteProject;
        private Button buttonAddStep;
        private Button buttonDeleteStep;
        private Button buttonSave;
        private Button buttonCancel;
        private Label labelHint;
        private TableLayoutPanel bottomLayout;

        private DataGridViewCheckBoxColumn projectEnabledColumn;
        private DataGridViewTextBoxColumn projectOrderColumn;
        private DataGridViewTextBoxColumn projectNameColumn;
        private DataGridViewComboBoxColumn projectKindColumn;
        private DataGridViewTextBoxColumn projectRecipeNameColumn;
        private DataGridViewTextBoxColumn projectRecipeFileColumn;
        private DataGridViewTextBoxColumn projectRepeatColumn;
        private DataGridViewCheckBoxColumn projectPopupColumn;
        private DataGridViewTextBoxColumn projectCrosstalkStartColumn;
        private DataGridViewTextBoxColumn projectPopupTimeoutColumn;
        private DataGridViewTextBoxColumn stepOrderColumn;
        private DataGridViewTextBoxColumn stepNameColumn;
        private DataGridViewTextBoxColumn stepImageColumn;
        private DataGridViewTextBoxColumn stepDelayColumn;
        private DataGridViewTextBoxColumn stepRequestColumn;

        // 选中项目/图卡后的绑定详情与小预览。运行时逻辑在 RecipeManagerForm.cs 中接入。
        private Label labelBindingProject;
        private Label labelBindingStep;
        private Label labelRecipeFile;
        private Label labelStepImage;
        private Label labelRecipeName;
        private Label labelRecipeFileCaption;
        private Label labelStepImageCaption;
        private Label labelImagePreviewPath;
        private ComboBox comboBoxRecipeFile;
        private TextBox textBoxBoundRecipeName;
        private ComboBox comboBoxStepImage;
        private Button buttonChooseProjectRecipe;
        private Button buttonChooseStepImage;
        private PictureBox pictureBoxImagePreview;

        private void InitializeComponent()
        {
            this.components = new Container();
            this.rootLayout = new TableLayoutPanel();
            this.gridProjects = new DataGridView();
            this.gridSteps = new DataGridView();
            this.groupProjects = new GroupBox();
            this.groupSteps = new GroupBox();
            this.projectLayout = new TableLayoutPanel();
            this.stepLayout = new TableLayoutPanel();
            this.stepBindingSplit = new SplitContainer();
            this.bindingDetailsPanel = new Panel();
            this.bindingDetailsLayout = new TableLayoutPanel();
            this.projectButtons = new FlowLayoutPanel();
            this.stepButtons = new FlowLayoutPanel();
            this.buttonAddProject = new Button();
            this.buttonDeleteProject = new Button();
            this.buttonAddStep = new Button();
            this.buttonDeleteStep = new Button();
            this.buttonSave = new Button();
            this.buttonCancel = new Button();
            this.labelHint = new Label();
            this.bottomLayout = new TableLayoutPanel();
            this.projectEnabledColumn = new DataGridViewCheckBoxColumn();
            this.projectOrderColumn = new DataGridViewTextBoxColumn();
            this.projectNameColumn = new DataGridViewTextBoxColumn();
            this.projectKindColumn = new DataGridViewComboBoxColumn();
            this.projectRecipeNameColumn = new DataGridViewTextBoxColumn();
            this.projectRecipeFileColumn = new DataGridViewTextBoxColumn();
            this.projectRepeatColumn = new DataGridViewTextBoxColumn();
            this.projectPopupColumn = new DataGridViewCheckBoxColumn();
            this.projectCrosstalkStartColumn = new DataGridViewTextBoxColumn();
            this.projectPopupTimeoutColumn = new DataGridViewTextBoxColumn();
            this.stepOrderColumn = new DataGridViewTextBoxColumn();
            this.stepNameColumn = new DataGridViewTextBoxColumn();
            this.stepImageColumn = new DataGridViewTextBoxColumn();
            this.stepDelayColumn = new DataGridViewTextBoxColumn();
            this.stepRequestColumn = new DataGridViewTextBoxColumn();
            this.labelBindingProject = new Label();
            this.labelBindingStep = new Label();
            this.labelRecipeFile = new Label();
            this.labelStepImage = new Label();
            this.labelRecipeName = new Label();
            this.labelRecipeFileCaption = new Label();
            this.labelStepImageCaption = new Label();
            this.labelImagePreviewPath = new Label();
            this.comboBoxRecipeFile = new ComboBox();
            this.textBoxBoundRecipeName = new TextBox();
            this.comboBoxStepImage = new ComboBox();
            this.buttonChooseProjectRecipe = new Button();
            this.buttonChooseStepImage = new Button();
            this.pictureBoxImagePreview = new PictureBox();
            ((ISupportInitialize)(this.gridProjects)).BeginInit();
            ((ISupportInitialize)(this.gridSteps)).BeginInit();
            ((ISupportInitialize)(this.stepBindingSplit)).BeginInit();
            this.stepBindingSplit.Panel1.SuspendLayout();
            this.stepBindingSplit.Panel2.SuspendLayout();
            this.stepBindingSplit.SuspendLayout();
            this.bindingDetailsPanel.SuspendLayout();
            this.bindingDetailsLayout.SuspendLayout();
            ((ISupportInitialize)(this.pictureBoxImagePreview)).BeginInit();
            this.SuspendLayout();

            // RecipeManagerForm
            // AutoScaleMode.Font expects the measured font dimensions (not DPI).
            // Using 96x96 here makes the designer/runtime scale the form down
            // dramatically on a normal 9pt font and is the source of the tiny
            // upper-left preview seen in Visual Studio.
            this.AutoScaleDimensions = new SizeF(7F, 17F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.BackColor = Color.FromArgb(245, 247, 250);
            this.ClientSize = new Size(1180, 720);
            this.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.MinimumSize = new Size(900, 560);
            this.Name = "RecipeManagerForm";
            this.StartPosition = FormStartPosition.CenterParent;
            this.Text = "测试项目与图卡顺序";

            // rootLayout
            this.rootLayout.AutoSize = false;
            this.rootLayout.ColumnCount = 1;
            this.rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            this.rootLayout.Dock = DockStyle.Fill;
            this.rootLayout.GrowStyle = TableLayoutPanelGrowStyle.FixedSize;
            this.rootLayout.Location = new Point(0, 0);
            this.rootLayout.Margin = new Padding(0);
            this.rootLayout.Name = "rootLayout";
            this.rootLayout.Padding = new Padding(10);
            this.rootLayout.RowCount = 3;
            // The percent rows deliberately total 100.  A 93% + absolute row
            // leaves the designer with an under-sized bottom area at some DPI
            // settings, which clips the Save/Cancel buttons.
            this.rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            this.rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            this.rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
            this.rootLayout.Controls.Add(this.groupProjects, 0, 0);
            this.rootLayout.Controls.Add(this.groupSteps, 0, 1);
            this.rootLayout.Controls.Add(this.bottomLayout, 0, 2);

            // groupProjects and projectLayout
            this.groupProjects.Dock = DockStyle.Fill;
            this.groupProjects.Margin = new Padding(0);
            this.groupProjects.Name = "groupProjects";
            this.groupProjects.Padding = new Padding(8, 18, 8, 4);
            this.groupProjects.Text = "测试项目（顺序、启用、配方和项目重复次数）";
            this.projectLayout.AutoSize = false;
            this.projectLayout.ColumnCount = 1;
            this.projectLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            this.projectLayout.Dock = DockStyle.Fill;
            this.projectLayout.GrowStyle = TableLayoutPanelGrowStyle.FixedSize;
            this.projectLayout.Margin = new Padding(0);
            this.projectLayout.Name = "projectLayout";
            this.projectLayout.RowCount = 2;
            this.projectLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            this.projectLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            this.projectLayout.Controls.Add(this.gridProjects, 0, 0);
            this.projectLayout.Controls.Add(this.projectButtons, 0, 1);
            this.groupProjects.Controls.Add(this.projectLayout);

            // gridProjects
            this.gridProjects.AllowUserToAddRows = false;
            this.gridProjects.AllowUserToDeleteRows = false;
            this.gridProjects.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            this.gridProjects.AutoGenerateColumns = false;
            this.gridProjects.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridProjects.Columns.AddRange(new DataGridViewColumn[] {
                this.projectEnabledColumn,
                this.projectOrderColumn,
                this.projectNameColumn,
                this.projectKindColumn,
                this.projectRecipeNameColumn,
                this.projectRecipeFileColumn,
                this.projectRepeatColumn,
                this.projectPopupColumn,
                this.projectCrosstalkStartColumn,
                this.projectPopupTimeoutColumn
            });
            this.gridProjects.BackgroundColor = Color.White;
            this.gridProjects.Dock = DockStyle.Fill;
            this.gridProjects.EditMode = DataGridViewEditMode.EditOnEnter;
            this.gridProjects.Margin = new Padding(0);
            this.gridProjects.MultiSelect = false;
            this.gridProjects.Name = "gridProjects";
            this.gridProjects.RowHeadersVisible = false;
            this.gridProjects.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            this.gridProjects.ColumnHeadersHeight = 34;
            this.gridProjects.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            this.gridProjects.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            this.gridProjects.SelectionChanged += new EventHandler(this.GridProjects_SelectionChanged);

            // projectEnabledColumn
            this.projectEnabledColumn.DataPropertyName = "Enabled";
            this.projectEnabledColumn.HeaderText = "启用";
            this.projectEnabledColumn.Name = "Enabled";
            this.projectEnabledColumn.FillWeight = 48F;
            this.projectEnabledColumn.Width = 48;
            // projectOrderColumn
            this.projectOrderColumn.DataPropertyName = "Order";
            this.projectOrderColumn.HeaderText = "顺序";
            this.projectOrderColumn.Name = "Order";
            this.projectOrderColumn.FillWeight = 55F;
            this.projectOrderColumn.Width = 55;
            // projectNameColumn
            this.projectNameColumn.DataPropertyName = "Name";
            this.projectNameColumn.HeaderText = "项目名称";
            this.projectNameColumn.Name = "Name";
            this.projectNameColumn.FillWeight = 180F;
            this.projectNameColumn.Width = 180;
            // projectKindColumn
            this.projectKindColumn.DataPropertyName = "Kind";
            // Use Items rather than DataSource here.  The WinForms designer's
            // property service can attempt to inspect a DataSource before the
            // column has a binding context and throw a null-reference error.
            // Items is serialized and rendered safely at design time while
            // retaining the same run-time choices.
            this.projectKindColumn.Items.AddRange(new object[] {
                Models.TestProjectKind.Generic,
                Models.TestProjectKind.Fov,
                Models.TestProjectKind.Contrast,
                Models.TestProjectKind.BrightnessUniformity,
                Models.TestProjectKind.Gamut,
                Models.TestProjectKind.Crosstalk
            });
            this.projectKindColumn.HeaderText = "类型";
            this.projectKindColumn.Name = "Kind";
            this.projectKindColumn.FillWeight = 130F;
            this.projectKindColumn.Width = 130;
            // projectRecipeNameColumn
            this.projectRecipeNameColumn.DataPropertyName = "RecipeName";
            this.projectRecipeNameColumn.HeaderText = "MRTEST配方";
            this.projectRecipeNameColumn.Name = "RecipeName";
            // Keep the protocol recipe name editable in the project grid.
            // Selecting a recipe file supplies the initial file-name stem;
            // operators can then correct it for the actual MRTEST name.
            this.projectRecipeNameColumn.ReadOnly = false;
            this.projectRecipeNameColumn.FillWeight = 150F;
            this.projectRecipeNameColumn.Width = 150;
            // projectRecipeFileColumn
            this.projectRecipeFileColumn.DataPropertyName = "RecipeFilePath";
            this.projectRecipeFileColumn.HeaderText = "配方文件(可选)";
            this.projectRecipeFileColumn.Name = "RecipeFilePath";
            this.projectRecipeFileColumn.FillWeight = 180F;
            this.projectRecipeFileColumn.Width = 180;
            // projectRepeatColumn
            this.projectRepeatColumn.DataPropertyName = "RepeatCount";
            this.projectRepeatColumn.HeaderText = "项目次数";
            this.projectRepeatColumn.Name = "RepeatCount";
            this.projectRepeatColumn.FillWeight = 80F;
            this.projectRepeatColumn.Width = 80;
            // projectPopupColumn
            this.projectPopupColumn.DataPropertyName = "PopupDriven";
            this.projectPopupColumn.HeaderText = "弹窗序列";
            this.projectPopupColumn.Name = "PopupDriven";
            this.projectPopupColumn.FillWeight = 75F;
            this.projectPopupColumn.Width = 75;
            // projectCrosstalkStartColumn
            this.projectCrosstalkStartColumn.DataPropertyName = "CrosstalkStartIndex";
            this.projectCrosstalkStartColumn.HeaderText = "串扰起点";
            this.projectCrosstalkStartColumn.Name = "CrosstalkStartIndex";
            this.projectCrosstalkStartColumn.FillWeight = 75F;
            this.projectCrosstalkStartColumn.Width = 75;
            // projectPopupTimeoutColumn
            this.projectPopupTimeoutColumn.DataPropertyName = "PopupTimeoutSeconds";
            this.projectPopupTimeoutColumn.HeaderText = "等待(s)";
            this.projectPopupTimeoutColumn.Name = "PopupTimeoutSeconds";
            this.projectPopupTimeoutColumn.FillWeight = 70F;
            this.projectPopupTimeoutColumn.Width = 70;
            // Re-apply FillWeight after the baseline Width values.  In Fill
            // mode, assigning Width later causes WinForms to rewrite the
            // weights, which otherwise makes the first/last columns consume
            // most of the grid.
            this.projectEnabledColumn.FillWeight = 48F;
            this.projectOrderColumn.FillWeight = 55F;
            this.projectNameColumn.FillWeight = 180F;
            this.projectKindColumn.FillWeight = 130F;
            this.projectRecipeNameColumn.FillWeight = 150F;
            this.projectRecipeFileColumn.FillWeight = 180F;
            this.projectRepeatColumn.FillWeight = 80F;
            this.projectPopupColumn.FillWeight = 75F;
            this.projectCrosstalkStartColumn.FillWeight = 75F;
            this.projectPopupTimeoutColumn.FillWeight = 70F;

            // projectButtons
            this.projectButtons.Controls.Add(this.buttonAddProject);
            this.projectButtons.Controls.Add(this.buttonDeleteProject);
            this.projectButtons.AutoSize = false;
            this.projectButtons.Dock = DockStyle.Fill;
            this.projectButtons.FlowDirection = FlowDirection.LeftToRight;
            this.projectButtons.Margin = new Padding(0);
            this.projectButtons.Name = "projectButtons";
            this.projectButtons.Padding = new Padding(0, 2, 0, 0);
            this.projectButtons.WrapContents = false;
            // buttonAddProject
            this.buttonAddProject.AutoSize = true;
            this.buttonAddProject.Name = "buttonAddProject";
            this.buttonAddProject.Text = "新增项目";
            this.buttonAddProject.UseVisualStyleBackColor = true;
            this.buttonAddProject.Click += new EventHandler(this.ButtonAddProject_Click);
            // buttonDeleteProject
            this.buttonDeleteProject.AutoSize = true;
            this.buttonDeleteProject.Name = "buttonDeleteProject";
            this.buttonDeleteProject.Text = "删除项目";
            this.buttonDeleteProject.UseVisualStyleBackColor = true;
            this.buttonDeleteProject.Click += new EventHandler(this.ButtonDeleteProject_Click);

            // groupSteps and stepLayout
            this.groupSteps.Dock = DockStyle.Fill;
            this.groupSteps.Margin = new Padding(0);
            this.groupSteps.Name = "groupSteps";
            this.groupSteps.Padding = new Padding(8, 18, 8, 4);
            this.groupSteps.Text = "固定图卡顺序（弹窗项目按出现顺序逐张投影，不识别提示文字）";
            this.stepLayout.AutoSize = false;
            this.stepLayout.ColumnCount = 1;
            this.stepLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            this.stepLayout.Dock = DockStyle.Fill;
            this.stepLayout.GrowStyle = TableLayoutPanelGrowStyle.FixedSize;
            this.stepLayout.Margin = new Padding(0);
            this.stepLayout.Name = "stepLayout";
            this.stepLayout.RowCount = 2;
            this.stepLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            this.stepLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            this.stepLayout.Controls.Add(this.gridSteps, 0, 0);
            this.stepLayout.Controls.Add(this.stepButtons, 0, 1);
            // Keep the existing image-step table on the left and expose a
            // dedicated binding/preview work area on the right.  The
            // splitter is vertical because its two panes are left/right.
            this.stepBindingSplit.Dock = DockStyle.Fill;
            this.stepBindingSplit.FixedPanel = FixedPanel.Panel2;
            this.stepBindingSplit.IsSplitterFixed = false;
            this.stepBindingSplit.Location = new Point(8, 18);
            this.stepBindingSplit.Name = "stepBindingSplit";
            this.stepBindingSplit.Orientation = Orientation.Vertical;
            // Leave enough room for the five existing step columns while
            // keeping a useful minimum width for the binding controls.  The
            // values also fit the form's 900-pixel minimum width.
            this.stepBindingSplit.Panel1MinSize = 500;
            this.stepBindingSplit.Panel1.Controls.Add(this.stepLayout);
            this.stepBindingSplit.Panel2MinSize = 280;
            this.stepBindingSplit.Panel2.Controls.Add(this.bindingDetailsPanel);
            this.stepBindingSplit.Size = new Size(1144, 300);
            this.stepBindingSplit.SplitterDistance = 760;
            this.stepBindingSplit.SplitterWidth = 6;
            this.stepBindingSplit.TabIndex = 0;
            this.groupSteps.Controls.Add(this.stepBindingSplit);

            // gridSteps
            this.gridSteps.AllowUserToAddRows = false;
            this.gridSteps.AllowUserToDeleteRows = false;
            this.gridSteps.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            this.gridSteps.AutoGenerateColumns = false;
            this.gridSteps.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridSteps.Columns.AddRange(new DataGridViewColumn[] {
                this.stepOrderColumn,
                this.stepNameColumn,
                this.stepImageColumn,
                this.stepDelayColumn,
                this.stepRequestColumn
            });
            this.gridSteps.BackgroundColor = Color.White;
            this.gridSteps.Dock = DockStyle.Fill;
            this.gridSteps.EditMode = DataGridViewEditMode.EditOnEnter;
            this.gridSteps.Margin = new Padding(0);
            this.gridSteps.Name = "gridSteps";
            this.gridSteps.RowHeadersVisible = false;
            this.gridSteps.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            this.gridSteps.ColumnHeadersHeight = 34;
            this.gridSteps.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            this.gridSteps.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;

            // stepOrderColumn
            this.stepOrderColumn.DataPropertyName = "Order";
            this.stepOrderColumn.HeaderText = "顺序";
            this.stepOrderColumn.Name = "StepOrder";
            this.stepOrderColumn.FillWeight = 55F;
            this.stepOrderColumn.Width = 55;
            // stepNameColumn
            this.stepNameColumn.DataPropertyName = "Name";
            this.stepNameColumn.HeaderText = "名称";
            this.stepNameColumn.Name = "StepName";
            this.stepNameColumn.FillWeight = 130F;
            this.stepNameColumn.Width = 130;
            // stepImageColumn
            this.stepImageColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            this.stepImageColumn.DataPropertyName = "ImagePath";
            this.stepImageColumn.HeaderText = "图卡路径";
            this.stepImageColumn.FillWeight = 260F;
            this.stepImageColumn.MinimumWidth = 260;
            this.stepImageColumn.Name = "ImagePath";
            // stepDelayColumn
            this.stepDelayColumn.DataPropertyName = "StabilizeDelayMs";
            this.stepDelayColumn.HeaderText = "稳定(ms)";
            this.stepDelayColumn.Name = "Delay";
            this.stepDelayColumn.FillWeight = 75F;
            this.stepDelayColumn.Width = 75;
            // stepRequestColumn
            this.stepRequestColumn.DataPropertyName = "MeasurementRequest";
            this.stepRequestColumn.HeaderText = "测量请求";
            this.stepRequestColumn.Name = "Request";
            this.stepRequestColumn.FillWeight = 210F;
            this.stepRequestColumn.Width = 210;
            this.stepOrderColumn.FillWeight = 55F;
            this.stepNameColumn.FillWeight = 130F;
            this.stepImageColumn.FillWeight = 260F;
            this.stepDelayColumn.FillWeight = 75F;
            this.stepRequestColumn.FillWeight = 210F;

            // bindingDetailsPanel/bindingDetailsLayout
            // The panel is intentionally kept independent from the two data
            // grids: selecting a row is all that is needed to edit its
            // recipe/image binding, while the original grid columns remain
            // available for direct inspection.
            this.bindingDetailsPanel.AutoScroll = true;
            this.bindingDetailsPanel.BackColor = Color.White;
            this.bindingDetailsPanel.BorderStyle = BorderStyle.FixedSingle;
            this.bindingDetailsPanel.Controls.Add(this.bindingDetailsLayout);
            this.bindingDetailsPanel.Dock = DockStyle.Fill;
            this.bindingDetailsPanel.Margin = new Padding(0);
            this.bindingDetailsPanel.Name = "bindingDetailsPanel";
            this.bindingDetailsPanel.Padding = new Padding(8);

            this.bindingDetailsLayout.ColumnCount = 3;
            this.bindingDetailsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92F));
            this.bindingDetailsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            this.bindingDetailsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104F));
            this.bindingDetailsLayout.Dock = DockStyle.Fill;
            this.bindingDetailsLayout.GrowStyle = TableLayoutPanelGrowStyle.FixedSize;
            this.bindingDetailsLayout.Margin = new Padding(0);
            this.bindingDetailsLayout.Name = "bindingDetailsLayout";
            this.bindingDetailsLayout.RowCount = 9;
            this.bindingDetailsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 18F));
            this.bindingDetailsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 16F));
            this.bindingDetailsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
            this.bindingDetailsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
            this.bindingDetailsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 14F));
            this.bindingDetailsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
            this.bindingDetailsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 14F));
            this.bindingDetailsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 14F));
            this.bindingDetailsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            this.bindingDetailsLayout.Controls.Add(this.labelBindingProject, 0, 0);
            this.bindingDetailsLayout.SetColumnSpan(this.labelBindingProject, 3);
            this.bindingDetailsLayout.Controls.Add(this.labelBindingStep, 0, 1);
            this.bindingDetailsLayout.SetColumnSpan(this.labelBindingStep, 3);
            this.bindingDetailsLayout.Controls.Add(this.labelRecipeFileCaption, 0, 2);
            this.bindingDetailsLayout.Controls.Add(this.comboBoxRecipeFile, 1, 2);
            this.bindingDetailsLayout.Controls.Add(this.buttonChooseProjectRecipe, 2, 2);
            this.bindingDetailsLayout.Controls.Add(this.labelRecipeName, 0, 3);
            this.bindingDetailsLayout.Controls.Add(this.textBoxBoundRecipeName, 1, 3);
            this.bindingDetailsLayout.SetColumnSpan(this.textBoxBoundRecipeName, 2);
            this.bindingDetailsLayout.Controls.Add(this.labelRecipeFile, 0, 4);
            this.bindingDetailsLayout.SetColumnSpan(this.labelRecipeFile, 3);
            this.bindingDetailsLayout.Controls.Add(this.labelStepImageCaption, 0, 5);
            this.bindingDetailsLayout.Controls.Add(this.comboBoxStepImage, 1, 5);
            this.bindingDetailsLayout.Controls.Add(this.buttonChooseStepImage, 2, 5);
            this.bindingDetailsLayout.Controls.Add(this.labelStepImage, 0, 6);
            this.bindingDetailsLayout.SetColumnSpan(this.labelStepImage, 3);
            this.bindingDetailsLayout.Controls.Add(this.labelImagePreviewPath, 0, 7);
            this.bindingDetailsLayout.SetColumnSpan(this.labelImagePreviewPath, 3);
            this.bindingDetailsLayout.Controls.Add(this.pictureBoxImagePreview, 0, 8);
            this.bindingDetailsLayout.SetColumnSpan(this.pictureBoxImagePreview, 3);

            // binding labels
            this.labelBindingProject.AutoEllipsis = true;
            this.labelBindingProject.Dock = DockStyle.Fill;
            this.labelBindingProject.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            this.labelBindingProject.ForeColor = Color.FromArgb(24, 51, 78);
            this.labelBindingProject.Margin = new Padding(0);
            this.labelBindingProject.Name = "labelBindingProject";
            this.labelBindingProject.Padding = new Padding(2, 0, 2, 0);
            this.labelBindingProject.Text = "当前项目：未选择";
            this.labelBindingProject.TextAlign = ContentAlignment.MiddleLeft;

            this.labelBindingStep.AutoEllipsis = true;
            this.labelBindingStep.Dock = DockStyle.Fill;
            this.labelBindingStep.ForeColor = Color.FromArgb(86, 99, 112);
            this.labelBindingStep.Margin = new Padding(0);
            this.labelBindingStep.Name = "labelBindingStep";
            this.labelBindingStep.Padding = new Padding(2, 0, 2, 0);
            this.labelBindingStep.Text = "当前图卡步骤：未选择";
            this.labelBindingStep.TextAlign = ContentAlignment.MiddleLeft;

            this.labelRecipeFile.AutoEllipsis = true;
            this.labelRecipeFile.Dock = DockStyle.Fill;
            this.labelRecipeFile.ForeColor = Color.FromArgb(86, 99, 112);
            this.labelRecipeFile.Name = "labelRecipeFile";
            this.labelRecipeFile.Padding = new Padding(2, 0, 2, 0);
            this.labelRecipeFile.Text = "配方文件：未绑定";
            this.labelRecipeFile.TextAlign = ContentAlignment.MiddleLeft;

            this.labelRecipeFileCaption.Dock = DockStyle.Fill;
            this.labelRecipeFileCaption.Name = "labelRecipeFileCaption";
            this.labelRecipeFileCaption.Text = "配方文件";
            this.labelRecipeFileCaption.TextAlign = ContentAlignment.MiddleRight;

            this.labelRecipeName.Dock = DockStyle.Fill;
            this.labelRecipeName.Name = "labelRecipeName";
            this.labelRecipeName.Text = "MRTEST配方名";
            this.labelRecipeName.TextAlign = ContentAlignment.MiddleRight;

            this.labelStepImage.AutoEllipsis = true;
            this.labelStepImage.Dock = DockStyle.Fill;
            this.labelStepImage.ForeColor = Color.FromArgb(86, 99, 112);
            this.labelStepImage.Name = "labelStepImage";
            this.labelStepImage.Padding = new Padding(2, 0, 2, 0);
            this.labelStepImage.Text = "图卡：未绑定";
            this.labelStepImage.TextAlign = ContentAlignment.MiddleLeft;

            this.labelStepImageCaption.Dock = DockStyle.Fill;
            this.labelStepImageCaption.Name = "labelStepImageCaption";
            this.labelStepImageCaption.Text = "图卡文件";
            this.labelStepImageCaption.TextAlign = ContentAlignment.MiddleRight;

            this.labelImagePreviewPath.AutoEllipsis = true;
            this.labelImagePreviewPath.Dock = DockStyle.Fill;
            this.labelImagePreviewPath.ForeColor = Color.FromArgb(86, 99, 112);
            this.labelImagePreviewPath.Name = "labelImagePreviewPath";
            this.labelImagePreviewPath.Padding = new Padding(2, 0, 2, 0);
            this.labelImagePreviewPath.Text = "预览：未选择图卡";
            this.labelImagePreviewPath.TextAlign = ContentAlignment.MiddleLeft;

            // binding selectors and actions.  Runtime code fills Items and
            // subscribes to the change/click events after construction.
            this.comboBoxRecipeFile.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            this.comboBoxRecipeFile.DropDownStyle = ComboBoxStyle.DropDownList;
            this.comboBoxRecipeFile.FormattingEnabled = true;
            this.comboBoxRecipeFile.IntegralHeight = false;
            this.comboBoxRecipeFile.Margin = new Padding(2, 0, 2, 0);
            this.comboBoxRecipeFile.Name = "comboBoxRecipeFile";
            this.comboBoxRecipeFile.SelectedIndexChanged += new EventHandler(this.ComboBoxRecipeFile_SelectedIndexChanged);

            this.textBoxBoundRecipeName.Dock = DockStyle.Fill;
            this.textBoxBoundRecipeName.Margin = new Padding(2, 0, 2, 0);
            this.textBoxBoundRecipeName.Name = "textBoxBoundRecipeName";
            this.textBoxBoundRecipeName.TextChanged += new EventHandler(this.TextBoxBoundRecipeName_TextChanged);

            this.comboBoxStepImage.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            this.comboBoxStepImage.DropDownStyle = ComboBoxStyle.DropDownList;
            this.comboBoxStepImage.FormattingEnabled = true;
            this.comboBoxStepImage.IntegralHeight = false;
            this.comboBoxStepImage.Margin = new Padding(2, 0, 2, 0);
            this.comboBoxStepImage.Name = "comboBoxStepImage";
            this.comboBoxStepImage.SelectedIndexChanged += new EventHandler(this.ComboBoxStepImage_SelectedIndexChanged);

            this.buttonChooseProjectRecipe.Dock = DockStyle.Fill;
            this.buttonChooseProjectRecipe.Margin = new Padding(2, 0, 0, 0);
            this.buttonChooseProjectRecipe.Name = "buttonChooseProjectRecipe";
            this.buttonChooseProjectRecipe.Text = "选择配方...";
            this.buttonChooseProjectRecipe.UseVisualStyleBackColor = true;
            this.buttonChooseProjectRecipe.Click += new EventHandler(this.ButtonChooseProjectRecipe_Click);

            this.buttonChooseStepImage.Dock = DockStyle.Fill;
            this.buttonChooseStepImage.Margin = new Padding(2, 0, 0, 0);
            this.buttonChooseStepImage.Name = "buttonChooseStepImage";
            this.buttonChooseStepImage.Text = "选择图卡...";
            this.buttonChooseStepImage.UseVisualStyleBackColor = true;
            this.buttonChooseStepImage.Click += new EventHandler(this.ButtonChooseStepImage_Click);

            // pictureBoxImagePreview is deliberately a small, non-locking
            // preview surface; the runtime code supplies a copied Bitmap.
            this.pictureBoxImagePreview.BackColor = Color.FromArgb(24, 30, 36);
            this.pictureBoxImagePreview.BorderStyle = BorderStyle.FixedSingle;
            this.pictureBoxImagePreview.Dock = DockStyle.Fill;
            this.pictureBoxImagePreview.Name = "pictureBoxImagePreview";
            this.pictureBoxImagePreview.SizeMode = PictureBoxSizeMode.Zoom;
            this.pictureBoxImagePreview.TabStop = false;

            // stepButtons
            this.stepButtons.Controls.Add(this.buttonAddStep);
            this.stepButtons.Controls.Add(this.buttonDeleteStep);
            this.stepButtons.AutoSize = false;
            this.stepButtons.Dock = DockStyle.Fill;
            this.stepButtons.FlowDirection = FlowDirection.LeftToRight;
            this.stepButtons.Margin = new Padding(0);
            this.stepButtons.Name = "stepButtons";
            this.stepButtons.Padding = new Padding(0, 2, 0, 0);
            this.stepButtons.WrapContents = false;
            // buttonAddStep
            this.buttonAddStep.AutoSize = true;
            this.buttonAddStep.Name = "buttonAddStep";
            this.buttonAddStep.Text = "新增图卡";
            this.buttonAddStep.UseVisualStyleBackColor = true;
            this.buttonAddStep.Click += new EventHandler(this.ButtonAddStep_Click);
            // buttonDeleteStep
            this.buttonDeleteStep.AutoSize = true;
            this.buttonDeleteStep.Name = "buttonDeleteStep";
            this.buttonDeleteStep.Text = "删除图卡";
            this.buttonDeleteStep.UseVisualStyleBackColor = true;
            this.buttonDeleteStep.Click += new EventHandler(this.ButtonDeleteStep_Click);

            // bottomLayout
            this.bottomLayout.ColumnCount = 3;
            this.bottomLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            this.bottomLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90F));
            this.bottomLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90F));
            this.bottomLayout.Dock = DockStyle.Fill;
            this.bottomLayout.GrowStyle = TableLayoutPanelGrowStyle.FixedSize;
            this.bottomLayout.Margin = new Padding(0, 4, 0, 0);
            this.bottomLayout.Name = "bottomLayout";
            this.bottomLayout.RowCount = 1;
            this.bottomLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            this.bottomLayout.Controls.Add(this.labelHint, 0, 0);
            this.bottomLayout.Controls.Add(this.buttonSave, 1, 0);
            this.bottomLayout.Controls.Add(this.buttonCancel, 2, 0);

            // labelHint
            this.labelHint.Dock = DockStyle.Fill;
            this.labelHint.Name = "labelHint";
            this.labelHint.Padding = new Padding(4, 0, 4, 0);
            this.labelHint.Text = "提示：项目次数=本轮内该项目重复次数；整套次数在主界面设置。请求默认为 &|Meas|A|M|@，设备先返回 &|Meas|A|M|Run|@，需等最终 &|Meas|A|OK|@。";
            this.labelHint.TextAlign = ContentAlignment.MiddleLeft;
            // buttonSave
            this.buttonSave.Dock = DockStyle.Fill;
            this.buttonSave.Name = "buttonSave";
            this.buttonSave.Text = "保存";
            this.buttonSave.UseVisualStyleBackColor = true;
            this.buttonSave.Click += new EventHandler(this.ButtonSave_Click);
            // buttonCancel
            this.buttonCancel.Dock = DockStyle.Fill;
            this.buttonCancel.Name = "buttonCancel";
            this.buttonCancel.Text = "取消";
            this.buttonCancel.UseVisualStyleBackColor = true;
            this.buttonCancel.Click += new EventHandler(this.ButtonCancel_Click);

            this.Controls.Add(this.rootLayout);
            ((ISupportInitialize)(this.gridProjects)).EndInit();
            ((ISupportInitialize)(this.gridSteps)).EndInit();
            this.bindingDetailsLayout.ResumeLayout(false);
            this.bindingDetailsPanel.ResumeLayout(false);
            this.stepBindingSplit.Panel1.ResumeLayout(false);
            this.stepBindingSplit.Panel2.ResumeLayout(false);
            ((ISupportInitialize)(this.stepBindingSplit)).EndInit();
            this.stepBindingSplit.ResumeLayout(false);
            ((ISupportInitialize)(this.pictureBoxImagePreview)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && (this.components != null))
            {
                this.pictureBoxImagePreview?.Image?.Dispose();
                this.components.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
