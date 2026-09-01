#nullable disable
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace AutoTestClient
{
    partial class DashboardForm
    {
        private IContainer components = null;
        private TableLayoutPanel rootLayout;
        private GroupBox groupConnection;
        private GroupBox groupPaths;
        private GroupBox groupPlan;
        private GroupBox groupManual;
        private TableLayoutPanel connectionTable;
        private TableLayoutPanel pathsTable;
        private TableLayoutPanel planTable;
        private TableLayoutPanel planOptions;
        private TableLayoutPanel manualTable;
        private SplitContainer resultSplit;

        private Label labelBindAddress;
        private Label labelPort;
        private Label labelMrTest;
        private Label labelExport;
        private Label labelRecipeDir;
        private Label labelImageDir;
        private Label labelOutputDir;
        private Label labelConnectionState;
        private Label labelWholeRepeat;
        private Label labelProjectRepeat;
        private Label labelProjectionMode;
        private Label labelPopupDelay;
        private Label labelManualCommand;
        private Label labelProgress;

        private TextBox textBoxBindAddress;
        private TextBox textBoxPort;
        private TextBox textBoxMrTest;
        private TextBox textBoxExport;
        private TextBox textBoxRecipeDir;
        private TextBox textBoxImageDir;
        private TextBox textBoxOutputDir;
        private TextBox textBoxManualCommand;

        private Button buttonListen;
        private Button buttonStart;
        private Button buttonStop;
        private Button buttonRecipeManager;
        private Button buttonSendManual;
        private Button buttonClearLog;
        private Button buttonViewCrosstalk;
        private Button buttonBrowseMrTest;
        private Button buttonBrowseExport;
        private Button buttonBrowseRecipe;
        private Button buttonBrowseImage;
        private Button buttonBrowseOutput;

        private CheckedListBox checkedListProjects;
        private NumericUpDown numericWholeRepeat;
        private NumericUpDown numericProjectRepeat;
        private NumericUpDown numericPopupDelay;
        private ComboBox comboProjectionMode;
        private CheckBox checkAutoConfirm;
        private Button buttonApplyProjectRepeat;
        private RichTextBox textBoxLog;
        private DataGridView gridResults;
        private DataGridViewTextBoxColumn resultProjectColumn;
        private DataGridViewTextBoxColumn resultMetricColumn;
        private DataGridViewTextBoxColumn resultValueColumn;
        private DataGridViewTextBoxColumn resultSourceColumn;
        private DataGridViewTextBoxColumn resultStatusColumn;
        private DataGridViewTextBoxColumn resultOutputColumn;

        private void InitializeComponent()
        {
            DataGridViewCellStyle dataGridViewCellStyle1 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle2 = new DataGridViewCellStyle();
            rootLayout = new TableLayoutPanel();
            groupConnection = new GroupBox();
            connectionTable = new TableLayoutPanel();
            labelBindAddress = new Label();
            labelPort = new Label();
            buttonListen = new Button();
            buttonStart = new Button();
            buttonStop = new Button();
            labelConnectionState = new Label();
            textBoxBindAddress = new TextBox();
            textBoxPort = new TextBox();
            resultSplit = new SplitContainer();
            textBoxLog = new RichTextBox();
            gridResults = new DataGridView();
            resultProjectColumn = new DataGridViewTextBoxColumn();
            resultMetricColumn = new DataGridViewTextBoxColumn();
            resultValueColumn = new DataGridViewTextBoxColumn();
            resultSourceColumn = new DataGridViewTextBoxColumn();
            resultStatusColumn = new DataGridViewTextBoxColumn();
            resultOutputColumn = new DataGridViewTextBoxColumn();
            groupPaths = new GroupBox();
            pathsTable = new TableLayoutPanel();
            labelImageDir = new Label();
            textBoxImageDir = new TextBox();
            buttonBrowseImage = new Button();
            labelOutputDir = new Label();
            textBoxOutputDir = new TextBox();
            buttonBrowseOutput = new Button();
            labelMrTest = new Label();
            textBoxMrTest = new TextBox();
            buttonBrowseMrTest = new Button();
            labelExport = new Label();
            textBoxExport = new TextBox();
            buttonBrowseExport = new Button();
            labelRecipeDir = new Label();
            textBoxRecipeDir = new TextBox();
            buttonBrowseRecipe = new Button();
            groupManual = new GroupBox();
            manualTable = new TableLayoutPanel();
            labelManualCommand = new Label();
            buttonViewCrosstalk = new Button();
            buttonSendManual = new Button();
            buttonClearLog = new Button();
            textBoxManualCommand = new TextBox();
            groupPlan = new GroupBox();
            planTable = new TableLayoutPanel();
            checkedListProjects = new CheckedListBox();
            labelProgress = new Label();
            planOptions = new TableLayoutPanel();
            labelWholeRepeat = new Label();
            numericWholeRepeat = new NumericUpDown();
            labelProjectRepeat = new Label();
            numericProjectRepeat = new NumericUpDown();
            buttonApplyProjectRepeat = new Button();
            labelProjectionMode = new Label();
            comboProjectionMode = new ComboBox();
            labelPopupDelay = new Label();
            numericPopupDelay = new NumericUpDown();
            checkAutoConfirm = new CheckBox();
            buttonRecipeManager = new Button();
            rootLayout.SuspendLayout();
            groupConnection.SuspendLayout();
            connectionTable.SuspendLayout();
            ((ISupportInitialize)resultSplit).BeginInit();
            resultSplit.Panel1.SuspendLayout();
            resultSplit.Panel2.SuspendLayout();
            resultSplit.SuspendLayout();
            ((ISupportInitialize)gridResults).BeginInit();
            groupPaths.SuspendLayout();
            pathsTable.SuspendLayout();
            groupManual.SuspendLayout();
            manualTable.SuspendLayout();
            groupPlan.SuspendLayout();
            planTable.SuspendLayout();
            planOptions.SuspendLayout();
            ((ISupportInitialize)numericWholeRepeat).BeginInit();
            ((ISupportInitialize)numericProjectRepeat).BeginInit();
            ((ISupportInitialize)numericPopupDelay).BeginInit();
            SuspendLayout();
            // 
            // rootLayout
            // 
            rootLayout.AutoSize = true;
            rootLayout.ColumnCount = 1;
            rootLayout.ColumnStyles.Add(new ColumnStyle());
            rootLayout.Controls.Add(groupConnection, 0, 0);
            rootLayout.Controls.Add(resultSplit, 0, 4);
            rootLayout.Controls.Add(groupPaths, 0, 1);
            rootLayout.Controls.Add(groupManual, 0, 3);
            rootLayout.Controls.Add(groupPlan, 0, 2);
            rootLayout.Dock = DockStyle.Fill;
            rootLayout.GrowStyle = TableLayoutPanelGrowStyle.FixedSize;
            rootLayout.Location = new Point(0, 0);
            rootLayout.Margin = new Padding(0);
            rootLayout.Name = "rootLayout";
            rootLayout.Padding = new Padding(10);
            rootLayout.RowCount = 5;
            rootLayout.RowStyles.Add(new RowStyle());
            rootLayout.RowStyles.Add(new RowStyle());
            rootLayout.RowStyles.Add(new RowStyle());
            rootLayout.RowStyles.Add(new RowStyle());
            rootLayout.RowStyles.Add(new RowStyle());
            rootLayout.Size = new Size(1293, 849);
            rootLayout.TabIndex = 0;
            rootLayout.Paint += rootLayout_Paint;
            // 
            // groupConnection
            // 
            groupConnection.Controls.Add(connectionTable);
            groupConnection.Location = new Point(10, 10);
            groupConnection.Margin = new Padding(0);
            groupConnection.Name = "groupConnection";
            groupConnection.Padding = new Padding(8, 18, 8, 6);
            groupConnection.Size = new Size(1260, 83);
            groupConnection.TabIndex = 0;
            groupConnection.TabStop = false;
            groupConnection.Text = "TCP 服务端监听（MRTEST/设备作为客户端连接本程序）";
            // 
            // connectionTable
            // 
            connectionTable.ColumnCount = 8;
            connectionTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76F));
            connectionTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28F));
            connectionTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48F));
            connectionTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F));
            connectionTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88F));
            connectionTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112F));
            connectionTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90F));
            connectionTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 72F));
            connectionTable.Controls.Add(labelBindAddress, 0, 0);
            connectionTable.Controls.Add(labelPort, 2, 0);
            connectionTable.Controls.Add(buttonListen, 4, 0);
            connectionTable.Controls.Add(buttonStart, 5, 0);
            connectionTable.Controls.Add(buttonStop, 6, 0);
            connectionTable.Controls.Add(labelConnectionState, 7, 0);
            connectionTable.Controls.Add(textBoxBindAddress, 1, 0);
            connectionTable.Controls.Add(textBoxPort, 3, 0);
            connectionTable.GrowStyle = TableLayoutPanelGrowStyle.FixedSize;
            connectionTable.Location = new Point(8, 34);
            connectionTable.Margin = new Padding(0);
            connectionTable.Name = "connectionTable";
            connectionTable.Padding = new Padding(8, 4, 8, 4);
            connectionTable.RowCount = 1;
            connectionTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            connectionTable.Size = new Size(1244, 42);
            connectionTable.TabIndex = 0;
            // 
            // labelBindAddress
            // 
            labelBindAddress.Anchor = AnchorStyles.Left;
            labelBindAddress.AutoSize = true;
            labelBindAddress.Location = new Point(11, 12);
            labelBindAddress.Name = "labelBindAddress";
            labelBindAddress.Size = new Size(56, 17);
            labelBindAddress.TabIndex = 0;
            labelBindAddress.Text = "监听地址";
            // 
            // labelPort
            // 
            labelPort.Anchor = AnchorStyles.Left;
            labelPort.AutoSize = true;
            labelPort.Location = new Point(256, 12);
            labelPort.Name = "labelPort";
            labelPort.Size = new Size(32, 17);
            labelPort.TabIndex = 2;
            labelPort.Text = "端口";
            // 
            // buttonListen
            // 
            buttonListen.Location = new Point(404, 7);
            buttonListen.Name = "buttonListen";
            buttonListen.Size = new Size(82, 28);
            buttonListen.TabIndex = 4;
            buttonListen.Text = "启动监听";
            buttonListen.UseVisualStyleBackColor = true;
            buttonListen.Click += ButtonListen_Click;
            // 
            // buttonStart
            // 
            buttonStart.BackColor = Color.FromArgb(37, 99, 235);
            buttonStart.ForeColor = Color.White;
            buttonStart.Location = new Point(492, 7);
            buttonStart.Name = "buttonStart";
            buttonStart.Size = new Size(106, 28);
            buttonStart.TabIndex = 5;
            buttonStart.Text = "开始一键测试";
            buttonStart.UseVisualStyleBackColor = false;
            buttonStart.Click += ButtonStart_Click;
            // 
            // buttonStop
            // 
            buttonStop.BackColor = Color.FromArgb(220, 38, 38);
            buttonStop.Enabled = false;
            buttonStop.ForeColor = Color.White;
            buttonStop.Location = new Point(604, 7);
            buttonStop.Name = "buttonStop";
            buttonStop.Size = new Size(84, 28);
            buttonStop.TabIndex = 6;
            buttonStop.Text = "停止";
            buttonStop.UseVisualStyleBackColor = false;
            buttonStop.Click += ButtonStop_Click;
            // 
            // labelConnectionState
            // 
            labelConnectionState.Anchor = AnchorStyles.Left;
            labelConnectionState.AutoSize = true;
            labelConnectionState.Location = new Point(694, 12);
            labelConnectionState.Name = "labelConnectionState";
            labelConnectionState.Size = new Size(44, 17);
            labelConnectionState.TabIndex = 7;
            labelConnectionState.Text = "未监听";
            // 
            // textBoxBindAddress
            // 
            textBoxBindAddress.Location = new Point(87, 7);
            textBoxBindAddress.Name = "textBoxBindAddress";
            textBoxBindAddress.Size = new Size(163, 23);
            textBoxBindAddress.TabIndex = 1;
            textBoxBindAddress.Text = "127.0.0.1";
            // 
            // textBoxPort
            // 
            textBoxPort.Location = new Point(304, 7);
            textBoxPort.Name = "textBoxPort";
            textBoxPort.Size = new Size(94, 23);
            textBoxPort.TabIndex = 3;
            textBoxPort.Text = "9527";
            // 
            // resultSplit
            // 
            resultSplit.Dock = DockStyle.Fill;
            resultSplit.Location = new Point(10, 543);
            resultSplit.Margin = new Padding(0);
            resultSplit.Name = "resultSplit";
            // 
            // resultSplit.Panel1
            // 
            resultSplit.Panel1.Controls.Add(textBoxLog);
            resultSplit.Panel1.Padding = new Padding(6);
            resultSplit.Panel1.Paint += resultSplit_Panel1_Paint;
            resultSplit.Panel1MinSize = 300;
            // 
            // resultSplit.Panel2
            // 
            resultSplit.Panel2.Controls.Add(gridResults);
            resultSplit.Panel2.Padding = new Padding(6);
            resultSplit.Panel2MinSize = 360;
            resultSplit.Size = new Size(1275, 296);
            resultSplit.SplitterDistance = 600;
            resultSplit.TabIndex = 4;
            // 
            // textBoxLog
            // 
            textBoxLog.BackColor = Color.White;
            textBoxLog.Location = new Point(6, 6);
            textBoxLog.Name = "textBoxLog";
            textBoxLog.ReadOnly = true;
            textBoxLog.ScrollBars = RichTextBoxScrollBars.Vertical;
            textBoxLog.Size = new Size(588, 278);
            textBoxLog.TabIndex = 0;
            textBoxLog.Text = "";
            textBoxLog.WordWrap = false;
            textBoxLog.TextChanged += textBoxLog_TextChanged;
            // 
            // gridResults
            // 
            gridResults.AllowUserToAddRows = false;
            gridResults.AllowUserToDeleteRows = false;
            gridResults.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            gridResults.BackgroundColor = Color.White;
            dataGridViewCellStyle1.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = SystemColors.Control;
            dataGridViewCellStyle1.Font = new Font("Microsoft YaHei UI", 9F);
            dataGridViewCellStyle1.ForeColor = SystemColors.WindowText;
            dataGridViewCellStyle1.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = DataGridViewTriState.True;
            gridResults.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            gridResults.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            gridResults.Columns.AddRange(new DataGridViewColumn[] { resultProjectColumn, resultMetricColumn, resultValueColumn, resultSourceColumn, resultStatusColumn, resultOutputColumn });
            dataGridViewCellStyle2.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = SystemColors.Window;
            dataGridViewCellStyle2.Font = new Font("Microsoft YaHei UI", 9F);
            dataGridViewCellStyle2.ForeColor = SystemColors.ControlText;
            dataGridViewCellStyle2.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle2.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle2.WrapMode = DataGridViewTriState.False;
            gridResults.DefaultCellStyle = dataGridViewCellStyle2;
            gridResults.Location = new Point(6, 6);
            gridResults.Name = "gridResults";
            gridResults.ReadOnly = true;
            gridResults.RowHeadersVisible = false;
            gridResults.Size = new Size(659, 278);
            gridResults.TabIndex = 0;
            // 
            // resultProjectColumn
            // 
            resultProjectColumn.HeaderText = "项目";
            resultProjectColumn.Name = "resultProjectColumn";
            resultProjectColumn.ReadOnly = true;
            // 
            // resultMetricColumn
            // 
            resultMetricColumn.HeaderText = "指标";
            resultMetricColumn.Name = "resultMetricColumn";
            resultMetricColumn.ReadOnly = true;
            // 
            // resultValueColumn
            // 
            resultValueColumn.HeaderText = "值";
            resultValueColumn.Name = "resultValueColumn";
            resultValueColumn.ReadOnly = true;
            // 
            // resultSourceColumn
            // 
            resultSourceColumn.HeaderText = "来源";
            resultSourceColumn.Name = "resultSourceColumn";
            resultSourceColumn.ReadOnly = true;
            // 
            // resultStatusColumn
            // 
            resultStatusColumn.HeaderText = "状态";
            resultStatusColumn.Name = "resultStatusColumn";
            resultStatusColumn.ReadOnly = true;
            // 
            // resultOutputColumn
            // 
            resultOutputColumn.HeaderText = "输出目录";
            resultOutputColumn.Name = "resultOutputColumn";
            resultOutputColumn.ReadOnly = true;
            // 
            // groupPaths
            // 
            groupPaths.Controls.Add(pathsTable);
            groupPaths.Location = new Point(10, 93);
            groupPaths.Margin = new Padding(0);
            groupPaths.Name = "groupPaths";
            groupPaths.Padding = new Padding(8, 18, 8, 6);
            groupPaths.Size = new Size(1260, 133);
            groupPaths.TabIndex = 1;
            groupPaths.TabStop = false;
            groupPaths.Text = "路径设置（所有文本框均可在设计器/运行时编辑）";
            // 
            // pathsTable
            // 
            pathsTable.ColumnCount = 6;
            pathsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 74F));
            pathsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            pathsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 28F));
            pathsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 74F));
            pathsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            pathsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 28F));
            pathsTable.Controls.Add(labelImageDir, 0, 0);
            pathsTable.Controls.Add(textBoxImageDir, 1, 0);
            pathsTable.Controls.Add(buttonBrowseImage, 2, 0);
            pathsTable.Controls.Add(labelOutputDir, 3, 0);
            pathsTable.Controls.Add(textBoxOutputDir, 4, 0);
            pathsTable.Controls.Add(buttonBrowseOutput, 5, 0);
            pathsTable.Controls.Add(labelMrTest, 0, 1);
            pathsTable.Controls.Add(textBoxMrTest, 1, 1);
            pathsTable.Controls.Add(buttonBrowseMrTest, 2, 1);
            pathsTable.Controls.Add(labelExport, 3, 1);
            pathsTable.Controls.Add(textBoxExport, 4, 1);
            pathsTable.Controls.Add(buttonBrowseExport, 5, 1);
            pathsTable.Controls.Add(labelRecipeDir, 0, 2);
            pathsTable.Controls.Add(textBoxRecipeDir, 1, 2);
            pathsTable.Controls.Add(buttonBrowseRecipe, 5, 2);
            pathsTable.Dock = DockStyle.Fill;
            pathsTable.GrowStyle = TableLayoutPanelGrowStyle.FixedSize;
            pathsTable.Location = new Point(8, 34);
            pathsTable.Margin = new Padding(0);
            pathsTable.Name = "pathsTable";
            pathsTable.Padding = new Padding(8, 2, 8, 2);
            pathsTable.RowCount = 3;
            pathsTable.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33333F));
            pathsTable.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33333F));
            pathsTable.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33334F));
            pathsTable.Size = new Size(1244, 93);
            pathsTable.TabIndex = 0;
            // 
            // labelImageDir
            // 
            labelImageDir.Anchor = AnchorStyles.Left;
            labelImageDir.AutoSize = true;
            labelImageDir.Location = new Point(11, 8);
            labelImageDir.Name = "labelImageDir";
            labelImageDir.Size = new Size(56, 17);
            labelImageDir.TabIndex = 0;
            labelImageDir.Text = "图卡目录";
            // 
            // textBoxImageDir
            // 
            textBoxImageDir.Dock = DockStyle.Fill;
            textBoxImageDir.Location = new Point(85, 5);
            textBoxImageDir.Name = "textBoxImageDir";
            textBoxImageDir.Size = new Size(506, 23);
            textBoxImageDir.TabIndex = 1;
            textBoxImageDir.Text = "D:\\CHATGPT_file\\测试图卡";
            // 
            // buttonBrowseImage
            // 
            buttonBrowseImage.Dock = DockStyle.Fill;
            buttonBrowseImage.Location = new Point(597, 5);
            buttonBrowseImage.Name = "buttonBrowseImage";
            buttonBrowseImage.Size = new Size(22, 23);
            buttonBrowseImage.TabIndex = 2;
            buttonBrowseImage.Text = "...";
            buttonBrowseImage.UseVisualStyleBackColor = true;
            buttonBrowseImage.Click += BrowseImage_Click;
            // 
            // labelOutputDir
            // 
            labelOutputDir.Anchor = AnchorStyles.Left;
            labelOutputDir.AutoSize = true;
            labelOutputDir.Location = new Point(625, 8);
            labelOutputDir.Name = "labelOutputDir";
            labelOutputDir.Size = new Size(56, 17);
            labelOutputDir.TabIndex = 3;
            labelOutputDir.Text = "结果目录";
            // 
            // textBoxOutputDir
            // 
            textBoxOutputDir.Dock = DockStyle.Fill;
            textBoxOutputDir.Location = new Point(699, 5);
            textBoxOutputDir.Name = "textBoxOutputDir";
            textBoxOutputDir.Size = new Size(506, 23);
            textBoxOutputDir.TabIndex = 4;
            textBoxOutputDir.Text = "%USERPROFILE%\\Documents\\AutoTestResults";
            // 
            // buttonBrowseOutput
            // 
            buttonBrowseOutput.Dock = DockStyle.Fill;
            buttonBrowseOutput.Location = new Point(1211, 5);
            buttonBrowseOutput.Name = "buttonBrowseOutput";
            buttonBrowseOutput.Size = new Size(22, 23);
            buttonBrowseOutput.TabIndex = 5;
            buttonBrowseOutput.Text = "...";
            buttonBrowseOutput.UseVisualStyleBackColor = true;
            buttonBrowseOutput.Click += BrowseOutput_Click;
            // 
            // labelMrTest
            // 
            labelMrTest.Anchor = AnchorStyles.Left;
            labelMrTest.AutoSize = true;
            labelMrTest.Location = new Point(11, 37);
            labelMrTest.Name = "labelMrTest";
            labelMrTest.Size = new Size(56, 17);
            labelMrTest.TabIndex = 6;
            labelMrTest.Text = "MRTEST";
            // 
            // textBoxMrTest
            // 
            textBoxMrTest.Dock = DockStyle.Fill;
            textBoxMrTest.Location = new Point(85, 34);
            textBoxMrTest.Name = "textBoxMrTest";
            textBoxMrTest.Size = new Size(506, 23);
            textBoxMrTest.TabIndex = 7;
            textBoxMrTest.Text = "D:\\Program Files\\GYTech\\Setup_MRTest\\MRTest.exe";
            // 
            // buttonBrowseMrTest
            // 
            buttonBrowseMrTest.Dock = DockStyle.Fill;
            buttonBrowseMrTest.Location = new Point(597, 34);
            buttonBrowseMrTest.Name = "buttonBrowseMrTest";
            buttonBrowseMrTest.Size = new Size(22, 23);
            buttonBrowseMrTest.TabIndex = 8;
            buttonBrowseMrTest.Text = "...";
            buttonBrowseMrTest.UseVisualStyleBackColor = true;
            buttonBrowseMrTest.Click += BrowseMrTest_Click;
            // 
            // labelExport
            // 
            labelExport.Anchor = AnchorStyles.Left;
            labelExport.AutoSize = true;
            labelExport.Location = new Point(625, 37);
            labelExport.Name = "labelExport";
            labelExport.Size = new Size(56, 17);
            labelExport.TabIndex = 9;
            labelExport.Text = "导出目录";
            // 
            // textBoxExport
            // 
            textBoxExport.Dock = DockStyle.Fill;
            textBoxExport.Location = new Point(699, 34);
            textBoxExport.Name = "textBoxExport";
            textBoxExport.Size = new Size(506, 23);
            textBoxExport.TabIndex = 10;
            textBoxExport.Text = "D:\\Program Files\\GYTech\\Setup_MRTest\\ExportFile";
            // 
            // buttonBrowseExport
            // 
            buttonBrowseExport.Dock = DockStyle.Fill;
            buttonBrowseExport.Location = new Point(1211, 34);
            buttonBrowseExport.Name = "buttonBrowseExport";
            buttonBrowseExport.Size = new Size(22, 23);
            buttonBrowseExport.TabIndex = 11;
            buttonBrowseExport.Text = "...";
            buttonBrowseExport.UseVisualStyleBackColor = true;
            buttonBrowseExport.Click += BrowseExport_Click;
            // 
            // labelRecipeDir
            // 
            labelRecipeDir.Anchor = AnchorStyles.Left;
            labelRecipeDir.AutoSize = true;
            labelRecipeDir.Location = new Point(11, 67);
            labelRecipeDir.Name = "labelRecipeDir";
            labelRecipeDir.Size = new Size(56, 17);
            labelRecipeDir.TabIndex = 12;
            labelRecipeDir.Text = "配方目录";
            // 
            // textBoxRecipeDir
            // 
            pathsTable.SetColumnSpan(textBoxRecipeDir, 4);
            textBoxRecipeDir.Location = new Point(85, 63);
            textBoxRecipeDir.Name = "textBoxRecipeDir";
            textBoxRecipeDir.Size = new Size(506, 23);
            textBoxRecipeDir.TabIndex = 13;
            textBoxRecipeDir.Text = "D:\\Program Files\\GYTech\\Setup_MRTest\\Elems";
            // 
            // buttonBrowseRecipe
            // 
            buttonBrowseRecipe.Dock = DockStyle.Fill;
            buttonBrowseRecipe.Location = new Point(1211, 63);
            buttonBrowseRecipe.Name = "buttonBrowseRecipe";
            buttonBrowseRecipe.Size = new Size(22, 25);
            buttonBrowseRecipe.TabIndex = 14;
            buttonBrowseRecipe.Text = "...";
            buttonBrowseRecipe.UseVisualStyleBackColor = true;
            buttonBrowseRecipe.Click += BrowseRecipe_Click;
            // 
            // groupManual
            // 
            groupManual.Controls.Add(manualTable);
            groupManual.Location = new Point(10, 470);
            groupManual.Margin = new Padding(0);
            groupManual.Name = "groupManual";
            groupManual.Padding = new Padding(8, 18, 8, 6);
            groupManual.Size = new Size(1260, 73);
            groupManual.TabIndex = 3;
            groupManual.TabStop = false;
            groupManual.Text = "手动报文";
            // 
            // manualTable
            // 
            manualTable.ColumnCount = 5;
            manualTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 69F));
            manualTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            manualTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 109F));
            manualTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 191F));
            manualTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 144F));
            manualTable.Controls.Add(labelManualCommand, 0, 0);
            manualTable.Controls.Add(buttonViewCrosstalk, 4, 0);
            manualTable.Controls.Add(buttonSendManual, 2, 0);
            manualTable.Controls.Add(buttonClearLog, 3, 0);
            manualTable.Controls.Add(textBoxManualCommand, 1, 0);
            manualTable.GrowStyle = TableLayoutPanelGrowStyle.FixedSize;
            manualTable.Location = new Point(6, 14);
            manualTable.Margin = new Padding(0);
            manualTable.Name = "manualTable";
            manualTable.Padding = new Padding(8, 7, 8, 7);
            manualTable.RowCount = 1;
            manualTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            manualTable.Size = new Size(1244, 53);
            manualTable.TabIndex = 0;
            // 
            // labelManualCommand
            // 
            labelManualCommand.Anchor = AnchorStyles.Left;
            labelManualCommand.AutoSize = true;
            labelManualCommand.Location = new Point(11, 18);
            labelManualCommand.Name = "labelManualCommand";
            labelManualCommand.Size = new Size(56, 17);
            labelManualCommand.TabIndex = 0;
            labelManualCommand.Text = "发送报文";
            // 
            // buttonViewCrosstalk
            // 
            buttonViewCrosstalk.Enabled = false;
            buttonViewCrosstalk.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            buttonViewCrosstalk.Location = new Point(1095, 10);
            buttonViewCrosstalk.Name = "buttonViewCrosstalk";
            buttonViewCrosstalk.Size = new Size(138, 33);
            buttonViewCrosstalk.TabIndex = 4;
            buttonViewCrosstalk.Text = "查看串扰热图";
            buttonViewCrosstalk.UseVisualStyleBackColor = true;
            buttonViewCrosstalk.Click += ButtonViewCrosstalk_Click;
            // 
            // buttonSendManual
            // 
            buttonSendManual.Location = new Point(795, 10);
            buttonSendManual.Name = "buttonSendManual";
            buttonSendManual.Size = new Size(102, 27);
            buttonSendManual.TabIndex = 2;
            buttonSendManual.Text = "发送并等待完成";
            buttonSendManual.UseVisualStyleBackColor = true;
            buttonSendManual.Click += ButtonSendManual_Click;
            // 
            // buttonClearLog
            // 
            buttonClearLog.Location = new Point(904, 10);
            buttonClearLog.Name = "buttonClearLog";
            buttonClearLog.Size = new Size(84, 27);
            buttonClearLog.TabIndex = 3;
            buttonClearLog.Text = "清空日志";
            buttonClearLog.UseVisualStyleBackColor = true;
            buttonClearLog.Click += ButtonClearLog_Click;
            // 
            // textBoxManualCommand
            // 
            textBoxManualCommand.Anchor = AnchorStyles.None;
            textBoxManualCommand.Location = new Point(82, 15);
            textBoxManualCommand.Name = "textBoxManualCommand";
            textBoxManualCommand.Size = new Size(704, 23);
            textBoxManualCommand.TabIndex = 1;
            textBoxManualCommand.Text = "&|Meas|A|M|@";
            textBoxManualCommand.TextChanged += textBoxManualCommand_TextChanged;
            // 
            // groupPlan
            // 
            groupPlan.Controls.Add(planTable);
            groupPlan.Location = new Point(10, 226);
            groupPlan.Margin = new Padding(0);
            groupPlan.Name = "groupPlan";
            groupPlan.Padding = new Padding(8, 18, 8, 6);
            groupPlan.Size = new Size(1260, 244);
            groupPlan.TabIndex = 2;
            groupPlan.TabStop = false;
            groupPlan.Text = "一键测试计划";
            // 
            // planTable
            // 
            planTable.ColumnCount = 3;
            planTable.ColumnStyles.Add(new ColumnStyle());
            planTable.ColumnStyles.Add(new ColumnStyle());
            planTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            planTable.Controls.Add(checkedListProjects, 0, 0);
            planTable.Controls.Add(labelProgress, 2, 0);
            planTable.Controls.Add(planOptions, 1, 0);
            planTable.GrowStyle = TableLayoutPanelGrowStyle.FixedSize;
            planTable.Location = new Point(8, 21);
            planTable.Margin = new Padding(0);
            planTable.Name = "planTable";
            planTable.Padding = new Padding(8, 4, 8, 6);
            planTable.RowCount = 1;
            planTable.RowStyles.Add(new RowStyle());
            planTable.Size = new Size(1244, 216);
            planTable.TabIndex = 0;
            // 
            // checkedListProjects
            // 
            checkedListProjects.BorderStyle = BorderStyle.FixedSingle;
            checkedListProjects.CheckOnClick = true;
            checkedListProjects.Dock = DockStyle.Fill;
            checkedListProjects.FormattingEnabled = true;
            checkedListProjects.Items.AddRange(new object[] { "1. FOV测试 [FOV] ×1", "2. 黑白对比度 [黑白对比度] ×1", "3. 亮度均匀性 [亮度均匀性] ×1", "4. 色域 [色域] ×1", "5. 串扰 [串扰] ×1" });
            checkedListProjects.Location = new Point(11, 7);
            checkedListProjects.Name = "checkedListProjects";
            checkedListProjects.Size = new Size(383, 200);
            checkedListProjects.TabIndex = 0;
            checkedListProjects.SelectedIndexChanged += CheckedListProjects_SelectedIndexChanged;
            // 
            // labelProgress
            // 
            labelProgress.Font = new Font("Microsoft YaHei UI", 15F, FontStyle.Bold, GraphicsUnit.Point, 134);
            labelProgress.Location = new Point(718, 4);
            labelProgress.Name = "labelProgress";
            labelProgress.Padding = new Padding(8);
            labelProgress.Size = new Size(490, 206);
            labelProgress.TabIndex = 2;
            labelProgress.Text = "等待开始";
            labelProgress.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // planOptions
            // 
            planOptions.ColumnCount = 2;
            planOptions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112F));
            planOptions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            planOptions.Controls.Add(labelWholeRepeat, 0, 0);
            planOptions.Controls.Add(numericWholeRepeat, 1, 0);
            planOptions.Controls.Add(labelProjectRepeat, 0, 1);
            planOptions.Controls.Add(numericProjectRepeat, 1, 1);
            planOptions.Controls.Add(buttonApplyProjectRepeat, 0, 2);
            planOptions.Controls.Add(labelProjectionMode, 0, 3);
            planOptions.Controls.Add(comboProjectionMode, 1, 3);
            planOptions.Controls.Add(labelPopupDelay, 0, 4);
            planOptions.Controls.Add(numericPopupDelay, 1, 4);
            planOptions.Controls.Add(checkAutoConfirm, 0, 5);
            planOptions.Controls.Add(buttonRecipeManager, 0, 6);
            planOptions.GrowStyle = TableLayoutPanelGrowStyle.FixedSize;
            planOptions.Location = new Point(397, 4);
            planOptions.Margin = new Padding(0);
            planOptions.Name = "planOptions";
            planOptions.Padding = new Padding(8, 0, 8, 0);
            planOptions.RowCount = 7;
            planOptions.RowStyles.Add(new RowStyle(SizeType.Percent, 14F));
            planOptions.RowStyles.Add(new RowStyle(SizeType.Percent, 14F));
            planOptions.RowStyles.Add(new RowStyle(SizeType.Percent, 14F));
            planOptions.RowStyles.Add(new RowStyle(SizeType.Percent, 14F));
            planOptions.RowStyles.Add(new RowStyle(SizeType.Percent, 14F));
            planOptions.RowStyles.Add(new RowStyle(SizeType.Percent, 16F));
            planOptions.RowStyles.Add(new RowStyle(SizeType.Percent, 14F));
            planOptions.Size = new Size(318, 206);
            planOptions.TabIndex = 1;
            // 
            // labelWholeRepeat
            // 
            labelWholeRepeat.Anchor = AnchorStyles.Left;
            labelWholeRepeat.AutoSize = true;
            labelWholeRepeat.Location = new Point(11, 5);
            labelWholeRepeat.Name = "labelWholeRepeat";
            labelWholeRepeat.Size = new Size(80, 17);
            labelWholeRepeat.TabIndex = 0;
            labelWholeRepeat.Text = "整套计划次数";
            // 
            // numericWholeRepeat
            // 
            numericWholeRepeat.Dock = DockStyle.Fill;
            numericWholeRepeat.Location = new Point(125, 1);
            numericWholeRepeat.Margin = new Padding(5, 1, 5, 1);
            numericWholeRepeat.Maximum = new decimal(new int[] { 9999, 0, 0, 0 });
            numericWholeRepeat.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            numericWholeRepeat.Name = "numericWholeRepeat";
            numericWholeRepeat.Size = new Size(180, 23);
            numericWholeRepeat.TabIndex = 1;
            numericWholeRepeat.Value = new decimal(new int[] { 1, 0, 0, 0 });
            // 
            // labelProjectRepeat
            // 
            labelProjectRepeat.Anchor = AnchorStyles.Left;
            labelProjectRepeat.AutoSize = true;
            labelProjectRepeat.Location = new Point(11, 33);
            labelProjectRepeat.Name = "labelProjectRepeat";
            labelProjectRepeat.Size = new Size(80, 17);
            labelProjectRepeat.TabIndex = 2;
            labelProjectRepeat.Text = "当前项目次数";
            // 
            // numericProjectRepeat
            // 
            numericProjectRepeat.Dock = DockStyle.Fill;
            numericProjectRepeat.Location = new Point(125, 29);
            numericProjectRepeat.Margin = new Padding(5, 1, 5, 1);
            numericProjectRepeat.Maximum = new decimal(new int[] { 9999, 0, 0, 0 });
            numericProjectRepeat.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            numericProjectRepeat.Name = "numericProjectRepeat";
            numericProjectRepeat.Size = new Size(180, 23);
            numericProjectRepeat.TabIndex = 3;
            numericProjectRepeat.Value = new decimal(new int[] { 1, 0, 0, 0 });
            // 
            // buttonApplyProjectRepeat
            // 
            planOptions.SetColumnSpan(buttonApplyProjectRepeat, 2);
            buttonApplyProjectRepeat.Dock = DockStyle.Fill;
            buttonApplyProjectRepeat.Location = new Point(13, 57);
            buttonApplyProjectRepeat.Margin = new Padding(5, 1, 5, 1);
            buttonApplyProjectRepeat.Name = "buttonApplyProjectRepeat";
            buttonApplyProjectRepeat.Size = new Size(292, 26);
            buttonApplyProjectRepeat.TabIndex = 4;
            buttonApplyProjectRepeat.Text = "应用项目次数";
            buttonApplyProjectRepeat.UseVisualStyleBackColor = true;
            buttonApplyProjectRepeat.Click += ButtonApplyProjectRepeat_Click;
            // 
            // labelProjectionMode
            // 
            labelProjectionMode.Anchor = AnchorStyles.Left;
            labelProjectionMode.AutoSize = true;
            labelProjectionMode.Location = new Point(11, 89);
            labelProjectionMode.Name = "labelProjectionMode";
            labelProjectionMode.Size = new Size(56, 17);
            labelProjectionMode.TabIndex = 5;
            labelProjectionMode.Text = "投影模式";
            // 
            // comboProjectionMode
            // 
            comboProjectionMode.Dock = DockStyle.Fill;
            comboProjectionMode.DropDownStyle = ComboBoxStyle.DropDownList;
            comboProjectionMode.FormattingEnabled = true;
            comboProjectionMode.Items.AddRange(new object[] { "第二屏 1:1 像素", "窗口适配" });
            comboProjectionMode.Location = new Point(125, 85);
            comboProjectionMode.Margin = new Padding(5, 1, 5, 1);
            comboProjectionMode.Name = "comboProjectionMode";
            comboProjectionMode.Size = new Size(180, 25);
            comboProjectionMode.TabIndex = 6;
            // 
            // labelPopupDelay
            // 
            labelPopupDelay.Anchor = AnchorStyles.Left;
            labelPopupDelay.AutoSize = true;
            labelPopupDelay.Location = new Point(11, 117);
            labelPopupDelay.Name = "labelPopupDelay";
            labelPopupDelay.Size = new Size(81, 17);
            labelPopupDelay.TabIndex = 7;
            labelPopupDelay.Text = "弹窗稳定(ms)";
            // 
            // numericPopupDelay
            // 
            numericPopupDelay.Dock = DockStyle.Fill;
            numericPopupDelay.Location = new Point(125, 113);
            numericPopupDelay.Margin = new Padding(5, 1, 5, 1);
            numericPopupDelay.Maximum = new decimal(new int[] { 60000, 0, 0, 0 });
            numericPopupDelay.Name = "numericPopupDelay";
            numericPopupDelay.Size = new Size(180, 23);
            numericPopupDelay.TabIndex = 8;
            numericPopupDelay.Value = new decimal(new int[] { 500, 0, 0, 0 });
            // 
            // checkAutoConfirm
            // 
            checkAutoConfirm.AutoSize = true;
            checkAutoConfirm.Checked = true;
            checkAutoConfirm.CheckState = CheckState.Checked;
            planOptions.SetColumnSpan(checkAutoConfirm, 2);
            checkAutoConfirm.Location = new Point(13, 141);
            checkAutoConfirm.Margin = new Padding(5, 1, 5, 1);
            checkAutoConfirm.Name = "checkAutoConfirm";
            checkAutoConfirm.Size = new Size(155, 21);
            checkAutoConfirm.TabIndex = 9;
            checkAutoConfirm.Text = "自动点击 MRTEST 确定";
            checkAutoConfirm.UseVisualStyleBackColor = true;
            // 
            // buttonRecipeManager
            // 
            planOptions.SetColumnSpan(buttonRecipeManager, 2);
            buttonRecipeManager.Dock = DockStyle.Fill;
            buttonRecipeManager.Location = new Point(13, 173);
            buttonRecipeManager.Margin = new Padding(5, 1, 5, 1);
            buttonRecipeManager.Name = "buttonRecipeManager";
            buttonRecipeManager.Size = new Size(292, 32);
            buttonRecipeManager.TabIndex = 10;
            buttonRecipeManager.Text = "编辑测试项目";
            buttonRecipeManager.UseVisualStyleBackColor = true;
            buttonRecipeManager.Click += ButtonRecipeManager_Click;
            // 
            // DashboardForm
            // 
            AutoScaleDimensions = new SizeF(7F, 17F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(245, 247, 250);
            ClientSize = new Size(1293, 849);
            Controls.Add(rootLayout);
            Font = new Font("Microsoft YaHei UI", 9F);
            MinimumSize = new Size(1060, 700);
            Name = "DashboardForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "一键光学测试客户端";
            FormClosing += DashboardForm_FormClosing;
            Load += DashboardForm_Load;
            rootLayout.ResumeLayout(false);
            groupConnection.ResumeLayout(false);
            connectionTable.ResumeLayout(false);
            connectionTable.PerformLayout();
            resultSplit.Panel1.ResumeLayout(false);
            resultSplit.Panel2.ResumeLayout(false);
            ((ISupportInitialize)resultSplit).EndInit();
            resultSplit.ResumeLayout(false);
            ((ISupportInitialize)gridResults).EndInit();
            groupPaths.ResumeLayout(false);
            pathsTable.ResumeLayout(false);
            pathsTable.PerformLayout();
            groupManual.ResumeLayout(false);
            manualTable.ResumeLayout(false);
            manualTable.PerformLayout();
            groupPlan.ResumeLayout(false);
            planTable.ResumeLayout(false);
            planOptions.ResumeLayout(false);
            planOptions.PerformLayout();
            ((ISupportInitialize)numericWholeRepeat).EndInit();
            ((ISupportInitialize)numericProjectRepeat).EndInit();
            ((ISupportInitialize)numericPopupDelay).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
