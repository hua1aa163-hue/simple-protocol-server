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
            components = new Container();
            DataGridViewCellStyle dataGridViewCellStyle1 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle2 = new DataGridViewCellStyle();
            rootLayout = new TableLayoutPanel();
            groupProjects = new GroupBox();
            projectLayout = new TableLayoutPanel();
            gridProjects = new DataGridView();
            projectButtons = new FlowLayoutPanel();
            buttonAddProject = new Button();
            buttonDeleteProject = new Button();
            groupSteps = new GroupBox();
            stepBindingSplit = new SplitContainer();
            stepLayout = new TableLayoutPanel();
            gridSteps = new DataGridView();
            stepButtons = new FlowLayoutPanel();
            buttonAddStep = new Button();
            buttonDeleteStep = new Button();
            bindingDetailsPanel = new Panel();
            bindingDetailsLayout = new TableLayoutPanel();
            labelBindingProject = new Label();
            labelBindingStep = new Label();
            labelRecipeFileCaption = new Label();
            comboBoxRecipeFile = new ComboBox();
            buttonChooseProjectRecipe = new Button();
            labelRecipeName = new Label();
            textBoxBoundRecipeName = new TextBox();
            labelRecipeFile = new Label();
            labelStepImageCaption = new Label();
            comboBoxStepImage = new ComboBox();
            buttonChooseStepImage = new Button();
            labelStepImage = new Label();
            labelImagePreviewPath = new Label();
            pictureBoxImagePreview = new PictureBox();
            bottomLayout = new TableLayoutPanel();
            labelHint = new Label();
            buttonSave = new Button();
            buttonCancel = new Button();
            rootLayout.SuspendLayout();
            groupProjects.SuspendLayout();
            projectLayout.SuspendLayout();
            ((ISupportInitialize)gridProjects).BeginInit();
            projectButtons.SuspendLayout();
            groupSteps.SuspendLayout();
            ((ISupportInitialize)stepBindingSplit).BeginInit();
            stepBindingSplit.Panel1.SuspendLayout();
            stepBindingSplit.Panel2.SuspendLayout();
            stepBindingSplit.SuspendLayout();
            stepLayout.SuspendLayout();
            ((ISupportInitialize)gridSteps).BeginInit();
            stepButtons.SuspendLayout();
            bindingDetailsPanel.SuspendLayout();
            bindingDetailsLayout.SuspendLayout();
            ((ISupportInitialize)pictureBoxImagePreview).BeginInit();
            bottomLayout.SuspendLayout();
            SuspendLayout();
            // 
            // rootLayout
            // 
            rootLayout.ColumnCount = 1;
            rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            rootLayout.Controls.Add(groupProjects, 0, 0);
            rootLayout.Controls.Add(groupSteps, 0, 1);
            rootLayout.Controls.Add(bottomLayout, 0, 2);
            rootLayout.Dock = DockStyle.Fill;
            rootLayout.GrowStyle = TableLayoutPanelGrowStyle.FixedSize;
            rootLayout.Location = new Point(0, 0);
            rootLayout.Margin = new Padding(0);
            rootLayout.Name = "rootLayout";
            rootLayout.Padding = new Padding(10);
            rootLayout.RowCount = 3;
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
            rootLayout.Size = new Size(1180, 720);
            rootLayout.TabIndex = 0;
            // 
            // groupProjects
            // 
            groupProjects.Controls.Add(projectLayout);
            groupProjects.Dock = DockStyle.Fill;
            groupProjects.Location = new Point(10, 10);
            groupProjects.Margin = new Padding(0);
            groupProjects.Name = "groupProjects";
            groupProjects.Padding = new Padding(8, 18, 8, 4);
            groupProjects.Size = new Size(1160, 327);
            groupProjects.TabIndex = 0;
            groupProjects.TabStop = false;
            groupProjects.Text = "测试项目（顺序、启用、配方和项目重复次数）";
            // 
            // projectLayout
            // 
            projectLayout.ColumnCount = 1;
            projectLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            projectLayout.Controls.Add(gridProjects, 0, 0);
            projectLayout.Controls.Add(projectButtons, 0, 1);
            projectLayout.Dock = DockStyle.Fill;
            projectLayout.GrowStyle = TableLayoutPanelGrowStyle.FixedSize;
            projectLayout.Location = new Point(8, 34);
            projectLayout.Margin = new Padding(0);
            projectLayout.Name = "projectLayout";
            projectLayout.RowCount = 2;
            projectLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            projectLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            projectLayout.Size = new Size(1144, 289);
            projectLayout.TabIndex = 0;
            // 
            // gridProjects
            // 
            gridProjects.AllowUserToAddRows = false;
            gridProjects.AllowUserToDeleteRows = false;
            gridProjects.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            gridProjects.BackgroundColor = Color.White;
            dataGridViewCellStyle1.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = SystemColors.Control;
            dataGridViewCellStyle1.Font = new Font("Microsoft YaHei UI", 9F);
            dataGridViewCellStyle1.ForeColor = SystemColors.WindowText;
            dataGridViewCellStyle1.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = DataGridViewTriState.False;
            gridProjects.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            gridProjects.ColumnHeadersHeight = 34;
            gridProjects.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            gridProjects.Dock = DockStyle.Fill;
            gridProjects.EditMode = DataGridViewEditMode.EditOnEnter;
            gridProjects.Location = new Point(0, 0);
            gridProjects.Margin = new Padding(0);
            gridProjects.MultiSelect = false;
            gridProjects.Name = "gridProjects";
            gridProjects.RowHeadersVisible = false;
            gridProjects.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            gridProjects.Size = new Size(1144, 253);
            gridProjects.TabIndex = 0;
            gridProjects.SelectionChanged += GridProjects_SelectionChanged;
            // 
            // projectButtons
            // 
            projectButtons.Controls.Add(buttonAddProject);
            projectButtons.Controls.Add(buttonDeleteProject);
            projectButtons.Dock = DockStyle.Fill;
            projectButtons.Location = new Point(0, 253);
            projectButtons.Margin = new Padding(0);
            projectButtons.Name = "projectButtons";
            projectButtons.Padding = new Padding(0, 2, 0, 0);
            projectButtons.Size = new Size(1144, 36);
            projectButtons.TabIndex = 1;
            projectButtons.WrapContents = false;
            // 
            // buttonAddProject
            // 
            buttonAddProject.AutoSize = true;
            buttonAddProject.Location = new Point(3, 5);
            buttonAddProject.Name = "buttonAddProject";
            buttonAddProject.Size = new Size(75, 27);
            buttonAddProject.TabIndex = 0;
            buttonAddProject.Text = "新增项目";
            buttonAddProject.UseVisualStyleBackColor = true;
            buttonAddProject.Click += ButtonAddProject_Click;
            // 
            // buttonDeleteProject
            // 
            buttonDeleteProject.AutoSize = true;
            buttonDeleteProject.Location = new Point(84, 5);
            buttonDeleteProject.Name = "buttonDeleteProject";
            buttonDeleteProject.Size = new Size(75, 27);
            buttonDeleteProject.TabIndex = 1;
            buttonDeleteProject.Text = "删除项目";
            buttonDeleteProject.UseVisualStyleBackColor = true;
            buttonDeleteProject.Click += ButtonDeleteProject_Click;
            // 
            // groupSteps
            // 
            groupSteps.Controls.Add(stepBindingSplit);
            groupSteps.Dock = DockStyle.Fill;
            groupSteps.Location = new Point(10, 337);
            groupSteps.Margin = new Padding(0);
            groupSteps.Name = "groupSteps";
            groupSteps.Padding = new Padding(8, 18, 8, 4);
            groupSteps.Size = new Size(1160, 327);
            groupSteps.TabIndex = 1;
            groupSteps.TabStop = false;
            groupSteps.Text = "固定图卡顺序（弹窗项目按出现顺序逐张投影，不识别提示文字）";
            // 
            // stepBindingSplit
            // 
            stepBindingSplit.Dock = DockStyle.Fill;
            stepBindingSplit.FixedPanel = FixedPanel.Panel2;
            stepBindingSplit.Location = new Point(8, 34);
            stepBindingSplit.Name = "stepBindingSplit";
            // 
            // stepBindingSplit.Panel1
            // 
            stepBindingSplit.Panel1.Controls.Add(stepLayout);
            stepBindingSplit.Panel1MinSize = 500;
            // 
            // stepBindingSplit.Panel2
            // 
            stepBindingSplit.Panel2.Controls.Add(bindingDetailsPanel);
            stepBindingSplit.Panel2MinSize = 280;
            stepBindingSplit.Size = new Size(1144, 289);
            stepBindingSplit.SplitterDistance = 740;
            stepBindingSplit.SplitterWidth = 6;
            stepBindingSplit.TabIndex = 0;
            // 
            // stepLayout
            // 
            stepLayout.ColumnCount = 1;
            stepLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            stepLayout.Controls.Add(gridSteps, 0, 0);
            stepLayout.Controls.Add(stepButtons, 0, 1);
            stepLayout.Dock = DockStyle.Fill;
            stepLayout.GrowStyle = TableLayoutPanelGrowStyle.FixedSize;
            stepLayout.Location = new Point(0, 0);
            stepLayout.Margin = new Padding(0);
            stepLayout.Name = "stepLayout";
            stepLayout.RowCount = 2;
            stepLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            stepLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            stepLayout.Size = new Size(740, 289);
            stepLayout.TabIndex = 0;
            // 
            // gridSteps
            // 
            gridSteps.AllowUserToAddRows = false;
            gridSteps.AllowUserToDeleteRows = false;
            gridSteps.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            gridSteps.BackgroundColor = Color.White;
            dataGridViewCellStyle2.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = SystemColors.Control;
            dataGridViewCellStyle2.Font = new Font("Microsoft YaHei UI", 9F);
            dataGridViewCellStyle2.ForeColor = SystemColors.WindowText;
            dataGridViewCellStyle2.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle2.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle2.WrapMode = DataGridViewTriState.False;
            gridSteps.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle2;
            gridSteps.ColumnHeadersHeight = 34;
            gridSteps.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            gridSteps.Dock = DockStyle.Fill;
            gridSteps.EditMode = DataGridViewEditMode.EditOnEnter;
            gridSteps.Location = new Point(0, 0);
            gridSteps.Margin = new Padding(0);
            gridSteps.Name = "gridSteps";
            gridSteps.RowHeadersVisible = false;
            gridSteps.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            gridSteps.Size = new Size(740, 253);
            gridSteps.TabIndex = 0;
            // 
            // stepButtons
            // 
            stepButtons.Controls.Add(buttonAddStep);
            stepButtons.Controls.Add(buttonDeleteStep);
            stepButtons.Dock = DockStyle.Fill;
            stepButtons.Location = new Point(0, 253);
            stepButtons.Margin = new Padding(0);
            stepButtons.Name = "stepButtons";
            stepButtons.Padding = new Padding(0, 2, 0, 0);
            stepButtons.Size = new Size(740, 36);
            stepButtons.TabIndex = 1;
            stepButtons.WrapContents = false;
            // 
            // buttonAddStep
            // 
            buttonAddStep.AutoSize = true;
            buttonAddStep.Location = new Point(3, 5);
            buttonAddStep.Name = "buttonAddStep";
            buttonAddStep.Size = new Size(75, 27);
            buttonAddStep.TabIndex = 0;
            buttonAddStep.Text = "新增图卡";
            buttonAddStep.UseVisualStyleBackColor = true;
            buttonAddStep.Click += ButtonAddStep_Click;
            // 
            // buttonDeleteStep
            // 
            buttonDeleteStep.AutoSize = true;
            buttonDeleteStep.Location = new Point(84, 5);
            buttonDeleteStep.Name = "buttonDeleteStep";
            buttonDeleteStep.Size = new Size(75, 27);
            buttonDeleteStep.TabIndex = 1;
            buttonDeleteStep.Text = "删除图卡";
            buttonDeleteStep.UseVisualStyleBackColor = true;
            buttonDeleteStep.Click += ButtonDeleteStep_Click;
            // 
            // bindingDetailsPanel
            // 
            bindingDetailsPanel.AutoScroll = true;
            bindingDetailsPanel.BackColor = Color.White;
            bindingDetailsPanel.BorderStyle = BorderStyle.FixedSingle;
            bindingDetailsPanel.Controls.Add(bindingDetailsLayout);
            bindingDetailsPanel.Dock = DockStyle.Fill;
            bindingDetailsPanel.Location = new Point(0, 0);
            bindingDetailsPanel.Margin = new Padding(0);
            bindingDetailsPanel.Name = "bindingDetailsPanel";
            bindingDetailsPanel.Padding = new Padding(8);
            bindingDetailsPanel.Size = new Size(398, 289);
            bindingDetailsPanel.TabIndex = 0;
            // 
            // bindingDetailsLayout
            // 
            bindingDetailsLayout.ColumnCount = 3;
            bindingDetailsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92F));
            bindingDetailsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            bindingDetailsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104F));
            bindingDetailsLayout.Controls.Add(labelBindingProject, 0, 0);
            bindingDetailsLayout.Controls.Add(labelBindingStep, 0, 1);
            bindingDetailsLayout.Controls.Add(labelRecipeFileCaption, 0, 2);
            bindingDetailsLayout.Controls.Add(comboBoxRecipeFile, 1, 2);
            bindingDetailsLayout.Controls.Add(buttonChooseProjectRecipe, 2, 2);
            bindingDetailsLayout.Controls.Add(labelRecipeName, 0, 3);
            bindingDetailsLayout.Controls.Add(textBoxBoundRecipeName, 1, 3);
            bindingDetailsLayout.Controls.Add(labelRecipeFile, 0, 4);
            bindingDetailsLayout.Controls.Add(labelStepImageCaption, 0, 5);
            bindingDetailsLayout.Controls.Add(comboBoxStepImage, 1, 5);
            bindingDetailsLayout.Controls.Add(buttonChooseStepImage, 2, 5);
            bindingDetailsLayout.Controls.Add(labelStepImage, 0, 6);
            bindingDetailsLayout.Controls.Add(labelImagePreviewPath, 0, 7);
            bindingDetailsLayout.Controls.Add(pictureBoxImagePreview, 0, 8);
            bindingDetailsLayout.Dock = DockStyle.Fill;
            bindingDetailsLayout.GrowStyle = TableLayoutPanelGrowStyle.FixedSize;
            bindingDetailsLayout.Location = new Point(8, 8);
            bindingDetailsLayout.Margin = new Padding(0);
            bindingDetailsLayout.Name = "bindingDetailsLayout";
            bindingDetailsLayout.RowCount = 9;
            bindingDetailsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 18F));
            bindingDetailsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 16F));
            bindingDetailsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
            bindingDetailsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
            bindingDetailsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 14F));
            bindingDetailsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
            bindingDetailsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 14F));
            bindingDetailsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 14F));
            bindingDetailsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            bindingDetailsLayout.Size = new Size(380, 271);
            bindingDetailsLayout.TabIndex = 0;
            // 
            // labelBindingProject
            // 
            labelBindingProject.AutoEllipsis = true;
            bindingDetailsLayout.SetColumnSpan(labelBindingProject, 3);
            labelBindingProject.Dock = DockStyle.Fill;
            labelBindingProject.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            labelBindingProject.ForeColor = Color.FromArgb(24, 51, 78);
            labelBindingProject.Location = new Point(0, 0);
            labelBindingProject.Margin = new Padding(0);
            labelBindingProject.Name = "labelBindingProject";
            labelBindingProject.Padding = new Padding(2, 0, 2, 0);
            labelBindingProject.Size = new Size(380, 18);
            labelBindingProject.TabIndex = 0;
            labelBindingProject.Text = "当前项目：未选择";
            labelBindingProject.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // labelBindingStep
            // 
            labelBindingStep.AutoEllipsis = true;
            bindingDetailsLayout.SetColumnSpan(labelBindingStep, 3);
            labelBindingStep.Dock = DockStyle.Fill;
            labelBindingStep.ForeColor = Color.FromArgb(86, 99, 112);
            labelBindingStep.Location = new Point(0, 18);
            labelBindingStep.Margin = new Padding(0);
            labelBindingStep.Name = "labelBindingStep";
            labelBindingStep.Padding = new Padding(2, 0, 2, 0);
            labelBindingStep.Size = new Size(380, 16);
            labelBindingStep.TabIndex = 1;
            labelBindingStep.Text = "当前图卡步骤：未选择";
            labelBindingStep.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // labelRecipeFileCaption
            // 
            labelRecipeFileCaption.Dock = DockStyle.Fill;
            labelRecipeFileCaption.Location = new Point(3, 34);
            labelRecipeFileCaption.Name = "labelRecipeFileCaption";
            labelRecipeFileCaption.Size = new Size(86, 22);
            labelRecipeFileCaption.TabIndex = 2;
            labelRecipeFileCaption.Text = "配方文件";
            labelRecipeFileCaption.TextAlign = ContentAlignment.MiddleRight;
            // 
            // comboBoxRecipeFile
            // 
            comboBoxRecipeFile.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            comboBoxRecipeFile.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxRecipeFile.FormattingEnabled = true;
            comboBoxRecipeFile.IntegralHeight = false;
            comboBoxRecipeFile.Location = new Point(94, 34);
            comboBoxRecipeFile.Margin = new Padding(2, 0, 2, 0);
            comboBoxRecipeFile.Name = "comboBoxRecipeFile";
            comboBoxRecipeFile.Size = new Size(180, 25);
            comboBoxRecipeFile.TabIndex = 3;
            comboBoxRecipeFile.SelectedIndexChanged += ComboBoxRecipeFile_SelectedIndexChanged;
            // 
            // buttonChooseProjectRecipe
            // 
            buttonChooseProjectRecipe.Dock = DockStyle.Fill;
            buttonChooseProjectRecipe.Location = new Point(278, 34);
            buttonChooseProjectRecipe.Margin = new Padding(2, 0, 0, 0);
            buttonChooseProjectRecipe.Name = "buttonChooseProjectRecipe";
            buttonChooseProjectRecipe.Size = new Size(102, 22);
            buttonChooseProjectRecipe.TabIndex = 4;
            buttonChooseProjectRecipe.Text = "选择配方...";
            buttonChooseProjectRecipe.UseVisualStyleBackColor = true;
            buttonChooseProjectRecipe.Click += ButtonChooseProjectRecipe_Click;
            // 
            // labelRecipeName
            // 
            labelRecipeName.Dock = DockStyle.Fill;
            labelRecipeName.Location = new Point(3, 56);
            labelRecipeName.Name = "labelRecipeName";
            labelRecipeName.Size = new Size(86, 22);
            labelRecipeName.TabIndex = 5;
            labelRecipeName.Text = "MRTEST配方名";
            labelRecipeName.TextAlign = ContentAlignment.MiddleRight;
            // 
            // textBoxBoundRecipeName
            // 
            bindingDetailsLayout.SetColumnSpan(textBoxBoundRecipeName, 2);
            textBoxBoundRecipeName.Dock = DockStyle.Fill;
            textBoxBoundRecipeName.Location = new Point(94, 56);
            textBoxBoundRecipeName.Margin = new Padding(2, 0, 2, 0);
            textBoxBoundRecipeName.Name = "textBoxBoundRecipeName";
            textBoxBoundRecipeName.Size = new Size(284, 23);
            textBoxBoundRecipeName.TabIndex = 6;
            textBoxBoundRecipeName.TextChanged += TextBoxBoundRecipeName_TextChanged;
            // 
            // labelRecipeFile
            // 
            labelRecipeFile.AutoEllipsis = true;
            bindingDetailsLayout.SetColumnSpan(labelRecipeFile, 3);
            labelRecipeFile.Dock = DockStyle.Fill;
            labelRecipeFile.ForeColor = Color.FromArgb(86, 99, 112);
            labelRecipeFile.Location = new Point(3, 78);
            labelRecipeFile.Name = "labelRecipeFile";
            labelRecipeFile.Padding = new Padding(2, 0, 2, 0);
            labelRecipeFile.Size = new Size(374, 14);
            labelRecipeFile.TabIndex = 7;
            labelRecipeFile.Text = "配方文件：未绑定";
            labelRecipeFile.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // labelStepImageCaption
            // 
            labelStepImageCaption.Dock = DockStyle.Fill;
            labelStepImageCaption.Location = new Point(3, 92);
            labelStepImageCaption.Name = "labelStepImageCaption";
            labelStepImageCaption.Size = new Size(86, 22);
            labelStepImageCaption.TabIndex = 8;
            labelStepImageCaption.Text = "图卡文件";
            labelStepImageCaption.TextAlign = ContentAlignment.MiddleRight;
            // 
            // comboBoxStepImage
            // 
            comboBoxStepImage.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            comboBoxStepImage.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxStepImage.FormattingEnabled = true;
            comboBoxStepImage.IntegralHeight = false;
            comboBoxStepImage.Location = new Point(94, 92);
            comboBoxStepImage.Margin = new Padding(2, 0, 2, 0);
            comboBoxStepImage.Name = "comboBoxStepImage";
            comboBoxStepImage.Size = new Size(180, 25);
            comboBoxStepImage.TabIndex = 9;
            comboBoxStepImage.SelectedIndexChanged += ComboBoxStepImage_SelectedIndexChanged;
            // 
            // buttonChooseStepImage
            // 
            buttonChooseStepImage.Dock = DockStyle.Fill;
            buttonChooseStepImage.Location = new Point(278, 92);
            buttonChooseStepImage.Margin = new Padding(2, 0, 0, 0);
            buttonChooseStepImage.Name = "buttonChooseStepImage";
            buttonChooseStepImage.Size = new Size(102, 22);
            buttonChooseStepImage.TabIndex = 10;
            buttonChooseStepImage.Text = "选择图卡...";
            buttonChooseStepImage.UseVisualStyleBackColor = true;
            buttonChooseStepImage.Click += ButtonChooseStepImage_Click;
            // 
            // labelStepImage
            // 
            labelStepImage.AutoEllipsis = true;
            bindingDetailsLayout.SetColumnSpan(labelStepImage, 3);
            labelStepImage.Dock = DockStyle.Fill;
            labelStepImage.ForeColor = Color.FromArgb(86, 99, 112);
            labelStepImage.Location = new Point(3, 114);
            labelStepImage.Name = "labelStepImage";
            labelStepImage.Padding = new Padding(2, 0, 2, 0);
            labelStepImage.Size = new Size(374, 14);
            labelStepImage.TabIndex = 11;
            labelStepImage.Text = "图卡：未绑定";
            labelStepImage.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // labelImagePreviewPath
            // 
            labelImagePreviewPath.AutoEllipsis = true;
            bindingDetailsLayout.SetColumnSpan(labelImagePreviewPath, 3);
            labelImagePreviewPath.Dock = DockStyle.Fill;
            labelImagePreviewPath.ForeColor = Color.FromArgb(86, 99, 112);
            labelImagePreviewPath.Location = new Point(3, 128);
            labelImagePreviewPath.Name = "labelImagePreviewPath";
            labelImagePreviewPath.Padding = new Padding(2, 0, 2, 0);
            labelImagePreviewPath.Size = new Size(374, 14);
            labelImagePreviewPath.TabIndex = 12;
            labelImagePreviewPath.Text = "预览：未选择图卡";
            labelImagePreviewPath.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // pictureBoxImagePreview
            // 
            pictureBoxImagePreview.BackColor = Color.FromArgb(24, 30, 36);
            pictureBoxImagePreview.BorderStyle = BorderStyle.FixedSingle;
            bindingDetailsLayout.SetColumnSpan(pictureBoxImagePreview, 3);
            pictureBoxImagePreview.Dock = DockStyle.Fill;
            pictureBoxImagePreview.Location = new Point(3, 145);
            pictureBoxImagePreview.Name = "pictureBoxImagePreview";
            pictureBoxImagePreview.Size = new Size(374, 123);
            pictureBoxImagePreview.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBoxImagePreview.TabIndex = 13;
            pictureBoxImagePreview.TabStop = false;
            // 
            // bottomLayout
            // 
            bottomLayout.ColumnCount = 3;
            bottomLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            bottomLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90F));
            bottomLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90F));
            bottomLayout.Controls.Add(labelHint, 0, 0);
            bottomLayout.Controls.Add(buttonSave, 1, 0);
            bottomLayout.Controls.Add(buttonCancel, 2, 0);
            bottomLayout.Dock = DockStyle.Fill;
            bottomLayout.GrowStyle = TableLayoutPanelGrowStyle.FixedSize;
            bottomLayout.Location = new Point(10, 668);
            bottomLayout.Margin = new Padding(0, 4, 0, 0);
            bottomLayout.Name = "bottomLayout";
            bottomLayout.RowCount = 1;
            bottomLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            bottomLayout.Size = new Size(1160, 42);
            bottomLayout.TabIndex = 2;
            // 
            // labelHint
            // 
            labelHint.Dock = DockStyle.Fill;
            labelHint.Location = new Point(3, 0);
            labelHint.Name = "labelHint";
            labelHint.Padding = new Padding(4, 0, 4, 0);
            labelHint.Size = new Size(974, 42);
            labelHint.TabIndex = 0;
            labelHint.Text = "提示：项目次数=本轮内该项目重复次数；整套次数在主界面设置。请求默认为 &|Meas|A|M|@，设备先返回 &|Meas|A|M|Run|@，需等最终 &|Meas|A|OK|@。";
            labelHint.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // buttonSave
            // 
            buttonSave.Dock = DockStyle.Fill;
            buttonSave.Location = new Point(983, 3);
            buttonSave.Name = "buttonSave";
            buttonSave.Size = new Size(84, 36);
            buttonSave.TabIndex = 1;
            buttonSave.Text = "保存";
            buttonSave.UseVisualStyleBackColor = true;
            buttonSave.Click += ButtonSave_Click;
            // 
            // buttonCancel
            // 
            buttonCancel.Dock = DockStyle.Fill;
            buttonCancel.Location = new Point(1073, 3);
            buttonCancel.Name = "buttonCancel";
            buttonCancel.Size = new Size(84, 36);
            buttonCancel.TabIndex = 2;
            buttonCancel.Text = "取消";
            buttonCancel.UseVisualStyleBackColor = true;
            buttonCancel.Click += ButtonCancel_Click;
            // 
            // RecipeManagerForm
            // 
            AutoScaleDimensions = new SizeF(7F, 17F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(245, 247, 250);
            ClientSize = new Size(1180, 720);
            Controls.Add(rootLayout);
            Font = new Font("Microsoft YaHei UI", 9F);
            MinimumSize = new Size(900, 560);
            Name = "RecipeManagerForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "测试项目与图卡顺序";
            rootLayout.ResumeLayout(false);
            groupProjects.ResumeLayout(false);
            projectLayout.ResumeLayout(false);
            ((ISupportInitialize)gridProjects).EndInit();
            projectButtons.ResumeLayout(false);
            projectButtons.PerformLayout();
            groupSteps.ResumeLayout(false);
            stepBindingSplit.Panel1.ResumeLayout(false);
            stepBindingSplit.Panel2.ResumeLayout(false);
            ((ISupportInitialize)stepBindingSplit).EndInit();
            stepBindingSplit.ResumeLayout(false);
            stepLayout.ResumeLayout(false);
            ((ISupportInitialize)gridSteps).EndInit();
            stepButtons.ResumeLayout(false);
            stepButtons.PerformLayout();
            bindingDetailsPanel.ResumeLayout(false);
            bindingDetailsLayout.ResumeLayout(false);
            bindingDetailsLayout.PerformLayout();
            ((ISupportInitialize)pictureBoxImagePreview).EndInit();
            bottomLayout.ResumeLayout(false);
            ResumeLayout(false);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && (this.components != null))
            {
                if (this.pictureBoxImagePreview != null && this.pictureBoxImagePreview.Image != null)
                {
                    this.pictureBoxImagePreview.Image.Dispose();
                    this.pictureBoxImagePreview.Image = null;
                }
                this.components.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
