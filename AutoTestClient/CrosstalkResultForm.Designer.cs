#nullable disable
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using AutoTestClient.Controls;

namespace AutoTestClient
{
    partial class CrosstalkResultForm
    {
        private IContainer components;
        private TableLayoutPanel rootLayout;
        private TableLayoutPanel headerLayout;
        private TableLayoutPanel contentLayout;
        private TableLayoutPanel rightLayout;
        private TableLayoutPanel sourceLayout;
        private TableLayoutPanel optionsLayout;
        private TableLayoutPanel maskLayout;
        private TableLayoutPanel historyLayout;
        private TableLayoutPanel sourceDataRow;
        private TableLayoutPanel sourceOutputRow;
        private FlowLayoutPanel optionButtons;
        private FlowLayoutPanel maskButtons;
        private FlowLayoutPanel archiveButtons;
        private FlowLayoutPanel historyButtons;
        private GroupBox groupSource;
        private GroupBox groupOptions;
        private GroupBox groupMask;
        private GroupBox groupHistory;
        private TextBox textBoxSourceRoot;
        private TextBox textBoxOutputRoot;
        private TextBox textBoxColorMinimum;
        private TextBox textBoxColorMaximum;
        private TextBox textBoxThreshold;
        private TextBox textBoxMaskCoordinates;
        private TextBox textBoxMaskName;
        private TextBox textBoxMaskNote;
        private ComboBox comboMaskArchives;
        private Button buttonBrowseSource;
        private Button buttonBrowseOutput;
        private Button buttonCompute;
        private Button buttonApplyOptions;
        private Button buttonResetOptions;
        private Button buttonApplyMask;
        private Button buttonClearMask;
        private Button buttonSaveMask;
        private Button buttonLoadMask;
        private Button buttonDeleteMask;
        private Button buttonExportCurrent;
        private Button buttonOpenFolder;
        private Button buttonOpenImage;
        private Button buttonRecordHistory;
        private Button buttonDeleteHistory;
        private Button buttonClearHistory;
        private Button buttonCopyHistory;
        private Label labelSummary;
        private Label labelStats;
        private Label labelStatus;
        private Label labelMaskCountHint;
        private Label labelCoordinateHint;
        private Label labelColorMinimum;
        private Label labelColorMaximum;
        private Label labelThreshold;
        private Label labelMaskName;
        private Label labelMaskNote;
        private Label labelOptionHint;
        private Label labelMaskHint;
        private Label labelSourceRoot;
        private Label labelOutputRoot;
        private HeatmapPreviewControl heatmapPreview;
        private DataGridView gridMetrics;
        private DataGridView gridHistory;
        private DataGridViewTextBoxColumn metricNameColumn;
        private DataGridViewTextBoxColumn metricValueColumn;
        private DataGridViewTextBoxColumn metricSourceColumn;
        private DataGridViewTextBoxColumn metricStatusColumn;
        private DataGridViewTextBoxColumn historyTimeColumn;
        private DataGridViewTextBoxColumn historyFolderColumn;
        private DataGridViewTextBoxColumn historyMaskColumn;
        private DataGridViewTextBoxColumn historyMaxColumn;
        private DataGridViewTextBoxColumn historyMinColumn;
        private DataGridViewTextBoxColumn historyMeanColumn;

        private void InitializeComponent()
        {
            components = new Container();
            rootLayout = new TableLayoutPanel();
            headerLayout = new TableLayoutPanel();
            labelSummary = new Label();
            buttonOpenImage = new Button();
            buttonOpenFolder = new Button();
            contentLayout = new TableLayoutPanel();
            groupSource = new GroupBox();
            sourceLayout = new TableLayoutPanel();
            sourceDataRow = new TableLayoutPanel();
            labelSourceRoot = new Label();
            textBoxSourceRoot = new TextBox();
            buttonBrowseSource = new Button();
            sourceOutputRow = new TableLayoutPanel();
            labelOutputRoot = new Label();
            textBoxOutputRoot = new TextBox();
            buttonBrowseOutput = new Button();
            buttonCompute = new Button();
            groupOptions = new GroupBox();
            optionsLayout = new TableLayoutPanel();
            labelColorMinimum = new Label();
            textBoxColorMinimum = new TextBox();
            labelColorMaximum = new Label();
            textBoxColorMaximum = new TextBox();
            labelThreshold = new Label();
            textBoxThreshold = new TextBox();
            optionButtons = new FlowLayoutPanel();
            buttonApplyOptions = new Button();
            buttonResetOptions = new Button();
            labelOptionHint = new Label();
            groupMask = new GroupBox();
            maskLayout = new TableLayoutPanel();
            labelCoordinateHint = new Label();
            textBoxMaskCoordinates = new TextBox();
            maskButtons = new FlowLayoutPanel();
            buttonApplyMask = new Button();
            buttonClearMask = new Button();
            textBoxMaskName = new TextBox();
            labelMaskName = new Label();
            textBoxMaskNote = new TextBox();
            labelMaskNote = new Label();
            comboMaskArchives = new ComboBox();
            labelMaskCountHint = new Label();
            archiveButtons = new FlowLayoutPanel();
            buttonSaveMask = new Button();
            buttonLoadMask = new Button();
            buttonDeleteMask = new Button();
            buttonExportCurrent = new Button();
            labelMaskHint = new Label();
            rightLayout = new TableLayoutPanel();
            labelStats = new Label();
            heatmapPreview = new HeatmapPreviewControl();
            gridMetrics = new DataGridView();
            metricNameColumn = new DataGridViewTextBoxColumn();
            metricValueColumn = new DataGridViewTextBoxColumn();
            metricSourceColumn = new DataGridViewTextBoxColumn();
            metricStatusColumn = new DataGridViewTextBoxColumn();
            labelStatus = new Label();
            groupHistory = new GroupBox();
            historyLayout = new TableLayoutPanel();
            gridHistory = new DataGridView();
            historyTimeColumn = new DataGridViewTextBoxColumn();
            historyFolderColumn = new DataGridViewTextBoxColumn();
            historyMaskColumn = new DataGridViewTextBoxColumn();
            historyMaxColumn = new DataGridViewTextBoxColumn();
            historyMinColumn = new DataGridViewTextBoxColumn();
            historyMeanColumn = new DataGridViewTextBoxColumn();
            historyButtons = new FlowLayoutPanel();
            buttonRecordHistory = new Button();
            buttonDeleteHistory = new Button();
            buttonClearHistory = new Button();
            buttonCopyHistory = new Button();
            rootLayout.SuspendLayout();
            headerLayout.SuspendLayout();
            contentLayout.SuspendLayout();
            groupSource.SuspendLayout();
            sourceLayout.SuspendLayout();
            sourceDataRow.SuspendLayout();
            sourceOutputRow.SuspendLayout();
            groupOptions.SuspendLayout();
            optionsLayout.SuspendLayout();
            optionButtons.SuspendLayout();
            groupMask.SuspendLayout();
            maskLayout.SuspendLayout();
            maskButtons.SuspendLayout();
            archiveButtons.SuspendLayout();
            rightLayout.SuspendLayout();
            ((ISupportInitialize)gridMetrics).BeginInit();
            groupHistory.SuspendLayout();
            historyLayout.SuspendLayout();
            ((ISupportInitialize)gridHistory).BeginInit();
            historyButtons.SuspendLayout();
            SuspendLayout();
            // 
            // rootLayout
            // 
            rootLayout.ColumnCount = 1;
            rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            rootLayout.Controls.Add(headerLayout, 0, 0);
            rootLayout.Controls.Add(contentLayout, 0, 1);
            rootLayout.Controls.Add(groupHistory, 0, 2);
            rootLayout.Dock = DockStyle.Fill;
            rootLayout.Location = new Point(0, 0);
            rootLayout.Margin = new Padding(0);
            rootLayout.Name = "rootLayout";
            rootLayout.Padding = new Padding(8);
            rootLayout.RowCount = 3;
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 215F));
            rootLayout.Size = new Size(1280, 900);
            rootLayout.TabIndex = 0;
            // 
            // headerLayout
            // 
            headerLayout.BackColor = Color.White;
            headerLayout.ColumnCount = 3;
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112F));
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112F));
            headerLayout.Controls.Add(labelSummary, 0, 0);
            headerLayout.Controls.Add(buttonOpenImage, 1, 0);
            headerLayout.Controls.Add(buttonOpenFolder, 2, 0);
            headerLayout.Dock = DockStyle.Fill;
            headerLayout.Location = new Point(8, 8);
            headerLayout.Margin = new Padding(0, 0, 0, 6);
            headerLayout.Name = "headerLayout";
            headerLayout.Padding = new Padding(4, 3, 4, 3);
            headerLayout.RowCount = 1;
            headerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            headerLayout.Size = new Size(1264, 54);
            headerLayout.TabIndex = 0;
            // 
            // labelSummary
            // 
            labelSummary.AutoEllipsis = true;
            labelSummary.Dock = DockStyle.Fill;
            labelSummary.Location = new Point(8, 3);
            labelSummary.Margin = new Padding(4, 0, 8, 0);
            labelSummary.Name = "labelSummary";
            labelSummary.Padding = new Padding(4, 0, 4, 0);
            labelSummary.Size = new Size(1020, 48);
            labelSummary.TabIndex = 0;
            labelSummary.Text = "串扰结果分析（运行时显示实际指标和热图）";
            labelSummary.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // buttonOpenImage
            // 
            buttonOpenImage.Dock = DockStyle.Fill;
            buttonOpenImage.Location = new Point(1039, 6);
            buttonOpenImage.Name = "buttonOpenImage";
            buttonOpenImage.Size = new Size(106, 42);
            buttonOpenImage.TabIndex = 1;
            buttonOpenImage.Text = "打开热图";
            buttonOpenImage.UseVisualStyleBackColor = true;
            buttonOpenImage.Click += ButtonOpenImage_Click;
            // 
            // buttonOpenFolder
            // 
            buttonOpenFolder.Dock = DockStyle.Fill;
            buttonOpenFolder.Location = new Point(1151, 6);
            buttonOpenFolder.Name = "buttonOpenFolder";
            buttonOpenFolder.Size = new Size(106, 42);
            buttonOpenFolder.TabIndex = 2;
            buttonOpenFolder.Text = "打开结果目录";
            buttonOpenFolder.UseVisualStyleBackColor = true;
            buttonOpenFolder.Click += ButtonOpenFolder_Click;
            // 
            // contentLayout
            // 
            contentLayout.ColumnCount = 2;
            contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 350F));
            contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            contentLayout.Controls.Add(groupSource, 0, 0);
            contentLayout.Controls.Add(rightLayout, 1, 0);
            contentLayout.Dock = DockStyle.Fill;
            contentLayout.Location = new Point(8, 68);
            contentLayout.Margin = new Padding(0);
            contentLayout.Name = "contentLayout";
            contentLayout.RowCount = 1;
            contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            contentLayout.Size = new Size(1264, 609);
            contentLayout.TabIndex = 1;
            // 
            // groupSource
            // 
            groupSource.Controls.Add(sourceLayout);
            groupSource.Dock = DockStyle.Fill;
            groupSource.Location = new Point(0, 0);
            groupSource.Margin = new Padding(0, 0, 8, 0);
            groupSource.Name = "groupSource";
            groupSource.Padding = new Padding(8, 19, 8, 8);
            groupSource.Size = new Size(342, 609);
            groupSource.TabIndex = 0;
            groupSource.TabStop = false;
            groupSource.Text = "数据与分析参数";
            // 
            // sourceLayout
            // 
            sourceLayout.AutoScroll = true;
            sourceLayout.ColumnCount = 1;
            sourceLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            sourceLayout.Controls.Add(sourceDataRow, 0, 0);
            sourceLayout.Controls.Add(sourceOutputRow, 0, 1);
            sourceLayout.Controls.Add(buttonCompute, 0, 2);
            sourceLayout.Controls.Add(groupOptions, 0, 3);
            sourceLayout.Controls.Add(groupMask, 0, 4);
            sourceLayout.Dock = DockStyle.Fill;
            sourceLayout.Location = new Point(8, 35);
            sourceLayout.Margin = new Padding(0);
            sourceLayout.Name = "sourceLayout";
            sourceLayout.RowCount = 5;
            sourceLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 51F));
            sourceLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 51F));
            sourceLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            sourceLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            sourceLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 275F));
            sourceLayout.Size = new Size(326, 566);
            sourceLayout.TabIndex = 0;
            // 
            // sourceDataRow
            // 
            sourceDataRow.ColumnCount = 3;
            sourceDataRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 68F));
            sourceDataRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            sourceDataRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 62F));
            sourceDataRow.Controls.Add(labelSourceRoot, 0, 0);
            sourceDataRow.Controls.Add(textBoxSourceRoot, 1, 0);
            sourceDataRow.Controls.Add(buttonBrowseSource, 2, 0);
            sourceDataRow.Dock = DockStyle.Fill;
            sourceDataRow.Location = new Point(0, 0);
            sourceDataRow.Margin = new Padding(0);
            sourceDataRow.Name = "sourceDataRow";
            sourceDataRow.RowCount = 1;
            sourceDataRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            sourceDataRow.Size = new Size(326, 51);
            sourceDataRow.TabIndex = 0;
            // 
            // labelSourceRoot
            // 
            labelSourceRoot.Dock = DockStyle.Fill;
            labelSourceRoot.Location = new Point(3, 0);
            labelSourceRoot.Name = "labelSourceRoot";
            labelSourceRoot.Size = new Size(62, 51);
            labelSourceRoot.TabIndex = 0;
            labelSourceRoot.Text = "数据文件夹";
            labelSourceRoot.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // textBoxSourceRoot
            // 
            textBoxSourceRoot.Dock = DockStyle.Fill;
            textBoxSourceRoot.Location = new Point(68, 2);
            textBoxSourceRoot.Margin = new Padding(0, 2, 4, 2);
            textBoxSourceRoot.Name = "textBoxSourceRoot";
            textBoxSourceRoot.Size = new Size(192, 23);
            textBoxSourceRoot.TabIndex = 1;
            // 
            // buttonBrowseSource
            // 
            buttonBrowseSource.Dock = DockStyle.Fill;
            buttonBrowseSource.Location = new Point(267, 3);
            buttonBrowseSource.Name = "buttonBrowseSource";
            buttonBrowseSource.Size = new Size(56, 45);
            buttonBrowseSource.TabIndex = 2;
            buttonBrowseSource.Text = "选择…";
            buttonBrowseSource.UseVisualStyleBackColor = true;
            buttonBrowseSource.Click += ButtonBrowseSource_Click;
            // 
            // sourceOutputRow
            // 
            sourceOutputRow.ColumnCount = 3;
            sourceOutputRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 68F));
            sourceOutputRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            sourceOutputRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 62F));
            sourceOutputRow.Controls.Add(labelOutputRoot, 0, 0);
            sourceOutputRow.Controls.Add(textBoxOutputRoot, 1, 0);
            sourceOutputRow.Controls.Add(buttonBrowseOutput, 2, 0);
            sourceOutputRow.Dock = DockStyle.Fill;
            sourceOutputRow.Location = new Point(0, 51);
            sourceOutputRow.Margin = new Padding(0);
            sourceOutputRow.Name = "sourceOutputRow";
            sourceOutputRow.RowCount = 1;
            sourceOutputRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            sourceOutputRow.Size = new Size(326, 51);
            sourceOutputRow.TabIndex = 1;
            // 
            // labelOutputRoot
            // 
            labelOutputRoot.Dock = DockStyle.Fill;
            labelOutputRoot.Location = new Point(3, 0);
            labelOutputRoot.Name = "labelOutputRoot";
            labelOutputRoot.Size = new Size(62, 51);
            labelOutputRoot.TabIndex = 0;
            labelOutputRoot.Text = "输出文件夹";
            labelOutputRoot.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // textBoxOutputRoot
            // 
            textBoxOutputRoot.Dock = DockStyle.Fill;
            textBoxOutputRoot.Location = new Point(68, 2);
            textBoxOutputRoot.Margin = new Padding(0, 2, 4, 2);
            textBoxOutputRoot.Name = "textBoxOutputRoot";
            textBoxOutputRoot.Size = new Size(192, 23);
            textBoxOutputRoot.TabIndex = 1;
            // 
            // buttonBrowseOutput
            // 
            buttonBrowseOutput.Dock = DockStyle.Fill;
            buttonBrowseOutput.Location = new Point(267, 3);
            buttonBrowseOutput.Name = "buttonBrowseOutput";
            buttonBrowseOutput.Size = new Size(56, 45);
            buttonBrowseOutput.TabIndex = 2;
            buttonBrowseOutput.Text = "选择…";
            buttonBrowseOutput.UseVisualStyleBackColor = true;
            buttonBrowseOutput.Click += ButtonBrowseOutput_Click;
            // 
            // buttonCompute
            // 
            buttonCompute.Dock = DockStyle.Fill;
            buttonCompute.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
            buttonCompute.Location = new Point(3, 105);
            buttonCompute.Name = "buttonCompute";
            buttonCompute.Size = new Size(320, 36);
            buttonCompute.TabIndex = 2;
            buttonCompute.Text = "计算并绘图（C#）";
            buttonCompute.UseVisualStyleBackColor = true;
            buttonCompute.Click += ButtonCompute_Click;
            // 
            // groupOptions
            // 
            groupOptions.Controls.Add(optionsLayout);
            groupOptions.Dock = DockStyle.Fill;
            groupOptions.Location = new Point(0, 152);
            groupOptions.Margin = new Padding(0, 8, 0, 0);
            groupOptions.Name = "groupOptions";
            groupOptions.Padding = new Padding(8, 19, 8, 8);
            groupOptions.Size = new Size(326, 139);
            groupOptions.TabIndex = 3;
            groupOptions.TabStop = false;
            groupOptions.Text = "计算与色轴参数";
            // 
            // optionsLayout
            // 
            optionsLayout.ColumnCount = 2;
            optionsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58F));
            optionsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42F));
            optionsLayout.Controls.Add(labelColorMinimum, 0, 0);
            optionsLayout.Controls.Add(textBoxColorMinimum, 1, 0);
            optionsLayout.Controls.Add(labelColorMaximum, 0, 1);
            optionsLayout.Controls.Add(textBoxColorMaximum, 1, 1);
            optionsLayout.Controls.Add(labelThreshold, 0, 2);
            optionsLayout.Controls.Add(textBoxThreshold, 1, 2);
            optionsLayout.Controls.Add(optionButtons, 0, 3);
            optionsLayout.Controls.Add(labelOptionHint, 0, 4);
            optionsLayout.Dock = DockStyle.Fill;
            optionsLayout.Location = new Point(8, 35);
            optionsLayout.Margin = new Padding(0);
            optionsLayout.Name = "optionsLayout";
            optionsLayout.RowCount = 5;
            optionsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 31F));
            optionsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 31F));
            optionsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 37F));
            optionsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            optionsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            optionsLayout.Size = new Size(310, 96);
            optionsLayout.TabIndex = 0;
            // 
            // labelColorMinimum
            // 
            labelColorMinimum.Dock = DockStyle.Fill;
            labelColorMinimum.Location = new Point(3, 0);
            labelColorMinimum.Name = "labelColorMinimum";
            labelColorMinimum.Size = new Size(173, 31);
            labelColorMinimum.TabIndex = 0;
            labelColorMinimum.Text = "色轴下限（%）";
            labelColorMinimum.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // textBoxColorMinimum
            // 
            textBoxColorMinimum.Dock = DockStyle.Fill;
            textBoxColorMinimum.Location = new Point(182, 3);
            textBoxColorMinimum.Name = "textBoxColorMinimum";
            textBoxColorMinimum.Size = new Size(125, 23);
            textBoxColorMinimum.TabIndex = 1;
            textBoxColorMinimum.Text = "0";
            textBoxColorMinimum.TextChanged += TextBoxOption_TextChanged;
            // 
            // labelColorMaximum
            // 
            labelColorMaximum.Dock = DockStyle.Fill;
            labelColorMaximum.Location = new Point(3, 31);
            labelColorMaximum.Name = "labelColorMaximum";
            labelColorMaximum.Size = new Size(173, 31);
            labelColorMaximum.TabIndex = 2;
            labelColorMaximum.Text = "色轴上限（%）";
            labelColorMaximum.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // textBoxColorMaximum
            // 
            textBoxColorMaximum.Dock = DockStyle.Fill;
            textBoxColorMaximum.Location = new Point(182, 34);
            textBoxColorMaximum.Name = "textBoxColorMaximum";
            textBoxColorMaximum.Size = new Size(125, 23);
            textBoxColorMaximum.TabIndex = 3;
            textBoxColorMaximum.Text = "50";
            textBoxColorMaximum.TextChanged += TextBoxOption_TextChanged;
            // 
            // labelThreshold
            // 
            labelThreshold.Dock = DockStyle.Fill;
            labelThreshold.Location = new Point(3, 62);
            labelThreshold.Name = "labelThreshold";
            labelThreshold.Size = new Size(173, 37);
            labelThreshold.TabIndex = 4;
            labelThreshold.Text = "异常阈值（比例）";
            labelThreshold.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // textBoxThreshold
            // 
            textBoxThreshold.Dock = DockStyle.Fill;
            textBoxThreshold.Location = new Point(182, 65);
            textBoxThreshold.Name = "textBoxThreshold";
            textBoxThreshold.Size = new Size(125, 23);
            textBoxThreshold.TabIndex = 5;
            textBoxThreshold.Text = "0.03";
            textBoxThreshold.TextChanged += TextBoxOption_TextChanged;
            // 
            // optionButtons
            // 
            optionsLayout.SetColumnSpan(optionButtons, 2);
            optionButtons.Controls.Add(buttonApplyOptions);
            optionButtons.Controls.Add(buttonResetOptions);
            optionButtons.Dock = DockStyle.Fill;
            optionButtons.Location = new Point(0, 102);
            optionButtons.Margin = new Padding(0, 3, 0, 3);
            optionButtons.Name = "optionButtons";
            optionButtons.Size = new Size(310, 34);
            optionButtons.TabIndex = 6;
            optionButtons.WrapContents = false;
            // 
            // buttonApplyOptions
            // 
            buttonApplyOptions.AutoSize = true;
            buttonApplyOptions.Location = new Point(3, 3);
            buttonApplyOptions.Name = "buttonApplyOptions";
            buttonApplyOptions.Size = new Size(75, 27);
            buttonApplyOptions.TabIndex = 0;
            buttonApplyOptions.Text = "应用参数";
            buttonApplyOptions.UseVisualStyleBackColor = true;
            buttonApplyOptions.Click += ButtonApplyOptions_Click;
            // 
            // buttonResetOptions
            // 
            buttonResetOptions.AutoSize = true;
            buttonResetOptions.Location = new Point(84, 3);
            buttonResetOptions.Name = "buttonResetOptions";
            buttonResetOptions.Size = new Size(75, 27);
            buttonResetOptions.TabIndex = 1;
            buttonResetOptions.Text = "恢复默认";
            buttonResetOptions.UseVisualStyleBackColor = true;
            buttonResetOptions.Click += ButtonResetOptions_Click;
            // 
            // labelOptionHint
            // 
            optionsLayout.SetColumnSpan(labelOptionHint, 2);
            labelOptionHint.Dock = DockStyle.Fill;
            labelOptionHint.ForeColor = Color.FromArgb(71, 85, 105);
            labelOptionHint.Location = new Point(3, 139);
            labelOptionHint.Name = "labelOptionHint";
            labelOptionHint.Size = new Size(304, 1);
            labelOptionHint.TabIndex = 7;
            labelOptionHint.Text = "阈值可填 0.03 或 3%；大于阈值的点标记异常并不计入统计。";
            // 
            // groupMask
            // 
            groupMask.Controls.Add(maskLayout);
            groupMask.Dock = DockStyle.Bottom;
            groupMask.Location = new Point(0, 299);
            groupMask.Margin = new Padding(0, 8, 0, 0);
            groupMask.Name = "groupMask";
            groupMask.Padding = new Padding(8, 19, 8, 8);
            groupMask.Size = new Size(326, 267);
            groupMask.TabIndex = 4;
            groupMask.TabStop = false;
            groupMask.Text = "排除范围与掩膜存档";
            // 
            // maskLayout
            // 
            maskLayout.ColumnCount = 2;
            maskLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            maskLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72F));
            maskLayout.Controls.Add(labelCoordinateHint, 0, 0);
            maskLayout.Controls.Add(textBoxMaskCoordinates, 0, 1);
            maskLayout.Controls.Add(maskButtons, 0, 2);
            maskLayout.Controls.Add(textBoxMaskName, 0, 3);
            maskLayout.Controls.Add(labelMaskName, 1, 3);
            maskLayout.Controls.Add(textBoxMaskNote, 0, 4);
            maskLayout.Controls.Add(labelMaskNote, 1, 4);
            maskLayout.Controls.Add(comboMaskArchives, 0, 5);
            maskLayout.Controls.Add(labelMaskCountHint, 1, 5);
            maskLayout.Controls.Add(archiveButtons, 0, 6);
            maskLayout.Controls.Add(buttonExportCurrent, 0, 7);
            maskLayout.Controls.Add(labelMaskHint, 0, 8);
            maskLayout.Dock = DockStyle.Fill;
            maskLayout.Location = new Point(8, 35);
            maskLayout.Margin = new Padding(0);
            maskLayout.Name = "maskLayout";
            maskLayout.RowCount = 9;
            maskLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 31F));
            maskLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            maskLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 31F));
            maskLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 31F));
            maskLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 31F));
            maskLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            maskLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 31F));
            maskLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            maskLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            maskLayout.Size = new Size(310, 224);
            maskLayout.TabIndex = 0;
            // 
            // labelCoordinateHint
            // 
            maskLayout.SetColumnSpan(labelCoordinateHint, 2);
            labelCoordinateHint.Dock = DockStyle.Fill;
            labelCoordinateHint.ForeColor = Color.FromArgb(71, 85, 105);
            labelCoordinateHint.Location = new Point(3, 0);
            labelCoordinateHint.Name = "labelCoordinateHint";
            labelCoordinateHint.Size = new Size(304, 31);
            labelCoordinateHint.TabIndex = 0;
            labelCoordinateHint.Text = "用户掩膜坐标（可输入多个范围）";
            // 
            // textBoxMaskCoordinates
            // 
            maskLayout.SetColumnSpan(textBoxMaskCoordinates, 2);
            textBoxMaskCoordinates.Dock = DockStyle.Fill;
            textBoxMaskCoordinates.Location = new Point(3, 34);
            textBoxMaskCoordinates.Multiline = true;
            textBoxMaskCoordinates.Name = "textBoxMaskCoordinates";
            textBoxMaskCoordinates.ScrollBars = ScrollBars.Vertical;
            textBoxMaskCoordinates.Size = new Size(304, 30);
            textBoxMaskCoordinates.TabIndex = 1;
            // 
            // maskButtons
            // 
            maskLayout.SetColumnSpan(maskButtons, 2);
            maskButtons.Controls.Add(buttonApplyMask);
            maskButtons.Controls.Add(buttonClearMask);
            maskButtons.Dock = DockStyle.Fill;
            maskButtons.Location = new Point(0, 69);
            maskButtons.Margin = new Padding(0, 2, 0, 2);
            maskButtons.Name = "maskButtons";
            maskButtons.Size = new Size(310, 27);
            maskButtons.TabIndex = 2;
            maskButtons.WrapContents = false;
            // 
            // buttonApplyMask
            // 
            buttonApplyMask.AutoSize = true;
            buttonApplyMask.Location = new Point(3, 3);
            buttonApplyMask.Name = "buttonApplyMask";
            buttonApplyMask.Size = new Size(75, 27);
            buttonApplyMask.TabIndex = 0;
            buttonApplyMask.Text = "应用坐标";
            buttonApplyMask.UseVisualStyleBackColor = true;
            buttonApplyMask.Click += ButtonApplyMask_Click;
            // 
            // buttonClearMask
            // 
            buttonClearMask.AutoSize = true;
            buttonClearMask.Location = new Point(84, 3);
            buttonClearMask.Name = "buttonClearMask";
            buttonClearMask.Size = new Size(75, 27);
            buttonClearMask.TabIndex = 1;
            buttonClearMask.Text = "清空掩膜";
            buttonClearMask.UseVisualStyleBackColor = true;
            buttonClearMask.Click += ButtonClearMask_Click;
            // 
            // textBoxMaskName
            // 
            textBoxMaskName.Dock = DockStyle.Fill;
            textBoxMaskName.Location = new Point(3, 101);
            textBoxMaskName.Name = "textBoxMaskName";
            textBoxMaskName.PlaceholderText = "存档名称";
            textBoxMaskName.Size = new Size(232, 23);
            textBoxMaskName.TabIndex = 3;
            // 
            // labelMaskName
            // 
            labelMaskName.Dock = DockStyle.Fill;
            labelMaskName.Location = new Point(241, 98);
            labelMaskName.Name = "labelMaskName";
            labelMaskName.Size = new Size(66, 31);
            labelMaskName.TabIndex = 4;
            labelMaskName.Text = "名称";
            labelMaskName.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // textBoxMaskNote
            // 
            textBoxMaskNote.Dock = DockStyle.Fill;
            textBoxMaskNote.Location = new Point(3, 132);
            textBoxMaskNote.Name = "textBoxMaskNote";
            textBoxMaskNote.PlaceholderText = "备注（可选）";
            textBoxMaskNote.Size = new Size(232, 23);
            textBoxMaskNote.TabIndex = 5;
            // 
            // labelMaskNote
            // 
            labelMaskNote.Dock = DockStyle.Fill;
            labelMaskNote.Location = new Point(241, 129);
            labelMaskNote.Name = "labelMaskNote";
            labelMaskNote.Size = new Size(66, 31);
            labelMaskNote.TabIndex = 6;
            labelMaskNote.Text = "备注";
            labelMaskNote.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // comboMaskArchives
            // 
            comboMaskArchives.Dock = DockStyle.Fill;
            comboMaskArchives.DropDownStyle = ComboBoxStyle.DropDownList;
            comboMaskArchives.FormattingEnabled = true;
            comboMaskArchives.Location = new Point(3, 163);
            comboMaskArchives.Name = "comboMaskArchives";
            comboMaskArchives.Size = new Size(232, 25);
            comboMaskArchives.TabIndex = 7;
            comboMaskArchives.SelectedIndexChanged += ComboMaskArchives_SelectedIndexChanged;
            // 
            // labelMaskCountHint
            // 
            labelMaskCountHint.Dock = DockStyle.Fill;
            labelMaskCountHint.ForeColor = Color.FromArgb(71, 85, 105);
            labelMaskCountHint.Location = new Point(241, 160);
            labelMaskCountHint.Name = "labelMaskCountHint";
            labelMaskCountHint.Size = new Size(66, 36);
            labelMaskCountHint.TabIndex = 8;
            labelMaskCountHint.Text = "存档";
            labelMaskCountHint.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // archiveButtons
            // 
            maskLayout.SetColumnSpan(archiveButtons, 2);
            archiveButtons.Controls.Add(buttonSaveMask);
            archiveButtons.Controls.Add(buttonLoadMask);
            archiveButtons.Controls.Add(buttonDeleteMask);
            archiveButtons.Dock = DockStyle.Fill;
            archiveButtons.Location = new Point(0, 198);
            archiveButtons.Margin = new Padding(0, 2, 0, 2);
            archiveButtons.Name = "archiveButtons";
            archiveButtons.Size = new Size(310, 27);
            archiveButtons.TabIndex = 9;
            archiveButtons.WrapContents = false;
            // 
            // buttonSaveMask
            // 
            buttonSaveMask.AutoSize = true;
            buttonSaveMask.Location = new Point(3, 3);
            buttonSaveMask.Name = "buttonSaveMask";
            buttonSaveMask.Size = new Size(75, 27);
            buttonSaveMask.TabIndex = 0;
            buttonSaveMask.Text = "保存";
            buttonSaveMask.UseVisualStyleBackColor = true;
            buttonSaveMask.Click += ButtonSaveMask_Click;
            // 
            // buttonLoadMask
            // 
            buttonLoadMask.AutoSize = true;
            buttonLoadMask.Location = new Point(84, 3);
            buttonLoadMask.Name = "buttonLoadMask";
            buttonLoadMask.Size = new Size(75, 27);
            buttonLoadMask.TabIndex = 1;
            buttonLoadMask.Text = "载入";
            buttonLoadMask.UseVisualStyleBackColor = true;
            buttonLoadMask.Click += ButtonLoadMask_Click;
            // 
            // buttonDeleteMask
            // 
            buttonDeleteMask.AutoSize = true;
            buttonDeleteMask.Location = new Point(165, 3);
            buttonDeleteMask.Name = "buttonDeleteMask";
            buttonDeleteMask.Size = new Size(75, 27);
            buttonDeleteMask.TabIndex = 2;
            buttonDeleteMask.Text = "删除";
            buttonDeleteMask.UseVisualStyleBackColor = true;
            buttonDeleteMask.Click += ButtonDeleteMask_Click;
            // 
            // buttonExportCurrent
            // 
            maskLayout.SetColumnSpan(buttonExportCurrent, 2);
            buttonExportCurrent.Dock = DockStyle.Fill;
            buttonExportCurrent.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            buttonExportCurrent.Location = new Point(3, 230);
            buttonExportCurrent.Name = "buttonExportCurrent";
            buttonExportCurrent.Size = new Size(304, 30);
            buttonExportCurrent.TabIndex = 10;
            buttonExportCurrent.Text = "导出当前掩膜结果（独立文件）";
            buttonExportCurrent.UseVisualStyleBackColor = true;
            buttonExportCurrent.Click += ButtonExportCurrent_Click;
            // 
            // labelMaskHint
            // 
            maskLayout.SetColumnSpan(labelMaskHint, 2);
            labelMaskHint.Dock = DockStyle.Fill;
            labelMaskHint.ForeColor = Color.FromArgb(71, 85, 105);
            labelMaskHint.Location = new Point(3, 263);
            labelMaskHint.Name = "labelMaskHint";
            labelMaskHint.Size = new Size(304, 1);
            labelMaskHint.TabIndex = 11;
            labelMaskHint.Text = "坐标格式 x1,y1-x2,y2（1 起始、含首尾）；热图上按住左键拖框可累加。";
            // 
            // rightLayout
            // 
            rightLayout.ColumnCount = 1;
            rightLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            rightLayout.Controls.Add(labelStats, 0, 0);
            rightLayout.Controls.Add(heatmapPreview, 0, 1);
            rightLayout.Controls.Add(gridMetrics, 0, 2);
            rightLayout.Controls.Add(labelStatus, 0, 3);
            rightLayout.Dock = DockStyle.Fill;
            rightLayout.Location = new Point(350, 0);
            rightLayout.Margin = new Padding(0);
            rightLayout.Name = "rightLayout";
            rightLayout.RowCount = 4;
            rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            rightLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 122F));
            rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            rightLayout.Size = new Size(914, 609);
            rightLayout.TabIndex = 1;
            // 
            // labelStats
            // 
            labelStats.Dock = DockStyle.Fill;
            labelStats.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
            labelStats.ForeColor = Color.FromArgb(15, 23, 42);
            labelStats.Location = new Point(3, 0);
            labelStats.Name = "labelStats";
            labelStats.Padding = new Padding(5, 0, 5, 0);
            labelStats.Size = new Size(908, 42);
            labelStats.TabIndex = 0;
            labelStats.Text = "最大值 —    最小值 —    均值 —    有效点 0    异常点 0    掩膜 0";
            labelStats.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // heatmapPreview
            // 
            heatmapPreview.BackColor = Color.White;
            heatmapPreview.Dock = DockStyle.Fill;
            heatmapPreview.EmptyText = "串扰热图将在测试完成后显示；可在图上拖拽添加掩膜";
            heatmapPreview.ForeColor = Color.FromArgb(31, 41, 55);
            heatmapPreview.Location = new Point(3, 45);
            heatmapPreview.MinimumSize = new Size(120, 40);
            heatmapPreview.Name = "heatmapPreview";
            heatmapPreview.Size = new Size(908, 411);
            heatmapPreview.TabIndex = 1;
            heatmapPreview.TabStop = false;
            heatmapPreview.MaskSelected += HeatmapPreview_MaskSelected;
            // 
            // gridMetrics
            // 
            gridMetrics.AllowUserToAddRows = false;
            gridMetrics.AllowUserToDeleteRows = false;
            gridMetrics.AllowUserToResizeRows = false;
            gridMetrics.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            gridMetrics.BackgroundColor = Color.White;
            gridMetrics.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            gridMetrics.Columns.AddRange(new DataGridViewColumn[] { metricNameColumn, metricValueColumn, metricSourceColumn, metricStatusColumn });
            gridMetrics.Dock = DockStyle.Fill;
            gridMetrics.Location = new Point(3, 462);
            gridMetrics.MultiSelect = false;
            gridMetrics.Name = "gridMetrics";
            gridMetrics.ReadOnly = true;
            gridMetrics.RowHeadersVisible = false;
            gridMetrics.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            gridMetrics.Size = new Size(908, 116);
            gridMetrics.TabIndex = 2;
            // 
            // metricNameColumn
            // 
            metricNameColumn.FillWeight = 23F;
            metricNameColumn.HeaderText = "指标";
            metricNameColumn.Name = "metricNameColumn";
            metricNameColumn.ReadOnly = true;
            // 
            // metricValueColumn
            // 
            metricValueColumn.FillWeight = 16F;
            metricValueColumn.HeaderText = "值";
            metricValueColumn.Name = "metricValueColumn";
            metricValueColumn.ReadOnly = true;
            // 
            // metricSourceColumn
            // 
            metricSourceColumn.FillWeight = 43F;
            metricSourceColumn.HeaderText = "来源";
            metricSourceColumn.Name = "metricSourceColumn";
            metricSourceColumn.ReadOnly = true;
            // 
            // metricStatusColumn
            // 
            metricStatusColumn.FillWeight = 18F;
            metricStatusColumn.HeaderText = "状态";
            metricStatusColumn.Name = "metricStatusColumn";
            metricStatusColumn.ReadOnly = true;
            // 
            // labelStatus
            // 
            labelStatus.AutoEllipsis = true;
            labelStatus.Dock = DockStyle.Fill;
            labelStatus.ForeColor = Color.FromArgb(71, 85, 105);
            labelStatus.Location = new Point(3, 581);
            labelStatus.Name = "labelStatus";
            labelStatus.Padding = new Padding(4, 0, 4, 0);
            labelStatus.Size = new Size(908, 28);
            labelStatus.TabIndex = 3;
            labelStatus.Text = "请选择数据文件夹并点击“计算并绘图”。";
            labelStatus.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // groupHistory
            // 
            groupHistory.Controls.Add(historyLayout);
            groupHistory.Dock = DockStyle.Fill;
            groupHistory.Location = new Point(8, 685);
            groupHistory.Margin = new Padding(0, 8, 0, 0);
            groupHistory.Name = "groupHistory";
            groupHistory.Padding = new Padding(8, 19, 8, 7);
            groupHistory.Size = new Size(1264, 207);
            groupHistory.TabIndex = 2;
            groupHistory.TabStop = false;
            groupHistory.Text = "历史统计结果（双击行可重新载入）";
            // 
            // historyLayout
            // 
            historyLayout.ColumnCount = 1;
            historyLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            historyLayout.Controls.Add(gridHistory, 0, 0);
            historyLayout.Controls.Add(historyButtons, 0, 1);
            historyLayout.Dock = DockStyle.Fill;
            historyLayout.Location = new Point(8, 35);
            historyLayout.Margin = new Padding(0);
            historyLayout.Name = "historyLayout";
            historyLayout.RowCount = 2;
            historyLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            historyLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            historyLayout.Size = new Size(1248, 165);
            historyLayout.TabIndex = 0;
            // 
            // gridHistory
            // 
            gridHistory.AllowUserToAddRows = false;
            gridHistory.AllowUserToDeleteRows = false;
            gridHistory.AllowUserToResizeRows = false;
            gridHistory.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            gridHistory.BackgroundColor = Color.White;
            gridHistory.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            gridHistory.Columns.AddRange(new DataGridViewColumn[] { historyTimeColumn, historyFolderColumn, historyMaskColumn, historyMaxColumn, historyMinColumn, historyMeanColumn });
            gridHistory.Dock = DockStyle.Fill;
            gridHistory.Location = new Point(3, 3);
            gridHistory.Name = "gridHistory";
            gridHistory.ReadOnly = true;
            gridHistory.RowHeadersVisible = false;
            gridHistory.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            gridHistory.Size = new Size(1242, 123);
            gridHistory.TabIndex = 0;
            gridHistory.CellDoubleClick += GridHistory_CellDoubleClick;
            // 
            // historyTimeColumn
            // 
            historyTimeColumn.FillWeight = 20F;
            historyTimeColumn.HeaderText = "时间";
            historyTimeColumn.Name = "historyTimeColumn";
            historyTimeColumn.ReadOnly = true;
            // 
            // historyFolderColumn
            // 
            historyFolderColumn.FillWeight = 18F;
            historyFolderColumn.HeaderText = "文件夹";
            historyFolderColumn.Name = "historyFolderColumn";
            historyFolderColumn.ReadOnly = true;
            // 
            // historyMaskColumn
            // 
            historyMaskColumn.FillWeight = 18F;
            historyMaskColumn.HeaderText = "掩膜";
            historyMaskColumn.Name = "historyMaskColumn";
            historyMaskColumn.ReadOnly = true;
            // 
            // historyMaxColumn
            // 
            historyMaxColumn.FillWeight = 14F;
            historyMaxColumn.HeaderText = "最大值";
            historyMaxColumn.Name = "historyMaxColumn";
            historyMaxColumn.ReadOnly = true;
            // 
            // historyMinColumn
            // 
            historyMinColumn.FillWeight = 14F;
            historyMinColumn.HeaderText = "最小值";
            historyMinColumn.Name = "historyMinColumn";
            historyMinColumn.ReadOnly = true;
            // 
            // historyMeanColumn
            // 
            historyMeanColumn.FillWeight = 14F;
            historyMeanColumn.HeaderText = "平均值";
            historyMeanColumn.Name = "historyMeanColumn";
            historyMeanColumn.ReadOnly = true;
            // 
            // historyButtons
            // 
            historyButtons.Controls.Add(buttonRecordHistory);
            historyButtons.Controls.Add(buttonDeleteHistory);
            historyButtons.Controls.Add(buttonClearHistory);
            historyButtons.Controls.Add(buttonCopyHistory);
            historyButtons.Dock = DockStyle.Fill;
            historyButtons.Location = new Point(0, 132);
            historyButtons.Margin = new Padding(0, 3, 0, 0);
            historyButtons.Name = "historyButtons";
            historyButtons.Size = new Size(1248, 33);
            historyButtons.TabIndex = 1;
            historyButtons.WrapContents = false;
            // 
            // buttonRecordHistory
            // 
            buttonRecordHistory.AutoSize = true;
            buttonRecordHistory.Location = new Point(3, 3);
            buttonRecordHistory.Name = "buttonRecordHistory";
            buttonRecordHistory.Size = new Size(90, 27);
            buttonRecordHistory.TabIndex = 0;
            buttonRecordHistory.Text = "记录当前结果";
            buttonRecordHistory.UseVisualStyleBackColor = true;
            buttonRecordHistory.Click += ButtonRecordHistory_Click;
            // 
            // buttonDeleteHistory
            // 
            buttonDeleteHistory.AutoSize = true;
            buttonDeleteHistory.Location = new Point(99, 3);
            buttonDeleteHistory.Name = "buttonDeleteHistory";
            buttonDeleteHistory.Size = new Size(75, 27);
            buttonDeleteHistory.TabIndex = 1;
            buttonDeleteHistory.Text = "删除选中";
            buttonDeleteHistory.UseVisualStyleBackColor = true;
            buttonDeleteHistory.Click += ButtonDeleteHistory_Click;
            // 
            // buttonClearHistory
            // 
            buttonClearHistory.AutoSize = true;
            buttonClearHistory.Location = new Point(180, 3);
            buttonClearHistory.Name = "buttonClearHistory";
            buttonClearHistory.Size = new Size(75, 27);
            buttonClearHistory.TabIndex = 2;
            buttonClearHistory.Text = "清空全部";
            buttonClearHistory.UseVisualStyleBackColor = true;
            buttonClearHistory.Click += ButtonClearHistory_Click;
            // 
            // buttonCopyHistory
            // 
            buttonCopyHistory.AutoSize = true;
            buttonCopyHistory.Location = new Point(261, 3);
            buttonCopyHistory.Name = "buttonCopyHistory";
            buttonCopyHistory.Size = new Size(75, 27);
            buttonCopyHistory.TabIndex = 3;
            buttonCopyHistory.Text = "一键复制";
            buttonCopyHistory.UseVisualStyleBackColor = true;
            buttonCopyHistory.Click += ButtonCopyHistory_Click;
            // 
            // CrosstalkResultForm
            // 
            AutoScaleDimensions = new SizeF(7F, 17F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(245, 247, 250);
            ClientSize = new Size(1280, 900);
            Controls.Add(rootLayout);
            Font = new Font("Microsoft YaHei UI", 9F);
            MinimumSize = new Size(960, 650);
            Name = "CrosstalkResultForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "串扰结果与掩膜分析（C#）";
            rootLayout.ResumeLayout(false);
            headerLayout.ResumeLayout(false);
            contentLayout.ResumeLayout(false);
            groupSource.ResumeLayout(false);
            sourceLayout.ResumeLayout(false);
            sourceDataRow.ResumeLayout(false);
            sourceDataRow.PerformLayout();
            sourceOutputRow.ResumeLayout(false);
            sourceOutputRow.PerformLayout();
            groupOptions.ResumeLayout(false);
            optionsLayout.ResumeLayout(false);
            optionsLayout.PerformLayout();
            optionButtons.ResumeLayout(false);
            optionButtons.PerformLayout();
            groupMask.ResumeLayout(false);
            maskLayout.ResumeLayout(false);
            maskLayout.PerformLayout();
            maskButtons.ResumeLayout(false);
            maskButtons.PerformLayout();
            archiveButtons.ResumeLayout(false);
            archiveButtons.PerformLayout();
            rightLayout.ResumeLayout(false);
            ((ISupportInitialize)gridMetrics).EndInit();
            groupHistory.ResumeLayout(false);
            historyLayout.ResumeLayout(false);
            ((ISupportInitialize)gridHistory).EndInit();
            historyButtons.ResumeLayout(false);
            historyButtons.PerformLayout();
            ResumeLayout(false);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.components != null) this.components.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
