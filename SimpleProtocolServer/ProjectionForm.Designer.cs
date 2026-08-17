// 这是 Visual Studio WinForms 设计器生成的投影窗体布局文件。
// InitializeComponent 只负责创建控件、摆放位置和绑定事件；投图及串扰流程位于 ProjectionForm.cs。
// 推荐通过 Visual Studio“查看设计器”修改布局，避免手工坐标与设计器状态不一致。
#nullable disable

namespace SimpleProtocolServer;

/// <summary>ProjectionForm 的控件声明和布局部分。</summary>
partial class ProjectionForm
{
    private System.ComponentModel.IContainer components = null;

    /// <summary>释放取消令牌、预览图片和设计器组件。</summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposeFormCancellation();
            DisposeSecondScreenProjection();
            picPreview?.Image?.Dispose();
            components?.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>创建投影窗口全部控件，设置属性并把事件连接到 ProjectionForm.cs。</summary>
    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        grpSettings = new GroupBox();
        btnApplyImageTransform = new Button();
        cmbImageTransform = new ComboBox();
        lblImageTransform = new Label();
        chkCloseSecondScreenOnStop = new CheckBox();
        lblProjectionState = new Label();
        numProjectionIntervalSeconds = new NumericUpDown();
        btnTimedProjection = new Button();
        lblProjectionIntervalSeconds = new Label();
        btnApplyTopology = new Button();
        cmbTopology = new ComboBox();
        lblTopology = new Label();
        btnBrowseDirectory = new Button();
        txtImageDirectory = new TextBox();
        lblImageDirectory = new Label();
        grpImages = new GroupBox();
        txtProjectionLog = new TextBox();
        lblCurrentImage = new Label();
        picPreview = new PictureBox();
        lvImages = new ListView();
        colIndex = new ColumnHeader();
        colFileName = new ColumnHeader();
        colStatus = new ColumnHeader();
        btnRefreshImages = new Button();
        btnProjectNext = new Button();
        btnProjectSelected = new Button();
        grpData = new GroupBox();
        lblDataProcessingState = new Label();
        btnBrowseOutputDirectory = new Button();
        txtOutputDirectory = new TextBox();
        lblOutputDirectory = new Label();
        btnBrowseDataSourceDirectory = new Button();
        txtDataSourceDirectory = new TextBox();
        lblDataSourceDirectory = new Label();
        btnClose = new Button();
        projectionTimer = new System.Windows.Forms.Timer(components);
        btnCrosstalkTest = new Button();
        grpSettings.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)numProjectionIntervalSeconds).BeginInit();
        grpImages.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)picPreview).BeginInit();
        grpData.SuspendLayout();
        SuspendLayout();
        // 
        // grpSettings
        // 
        grpSettings.Controls.Add(btnApplyImageTransform);
        grpSettings.Controls.Add(cmbImageTransform);
        grpSettings.Controls.Add(lblImageTransform);
        grpSettings.Controls.Add(btnProjectSelected);
        grpSettings.Controls.Add(chkCloseSecondScreenOnStop);
        grpSettings.Controls.Add(lblProjectionState);
        grpSettings.Controls.Add(numProjectionIntervalSeconds);
        grpSettings.Controls.Add(btnTimedProjection);
        grpSettings.Controls.Add(lblProjectionIntervalSeconds);
        grpSettings.Controls.Add(btnApplyTopology);
        grpSettings.Controls.Add(cmbTopology);
        grpSettings.Controls.Add(lblTopology);
        grpSettings.Controls.Add(btnBrowseDirectory);
        grpSettings.Controls.Add(txtImageDirectory);
        grpSettings.Controls.Add(lblImageDirectory);
        grpSettings.Location = new Point(12, 12);
        grpSettings.Name = "grpSettings";
        grpSettings.Size = new Size(1066, 151);
        grpSettings.TabIndex = 0;
        grpSettings.TabStop = false;
        grpSettings.Text = "投影设置";
        //
        // btnApplyImageTransform
        //
        btnApplyImageTransform.BackColor = Color.WhiteSmoke;
        btnApplyImageTransform.Location = new Point(315, 106);
        btnApplyImageTransform.Name = "btnApplyImageTransform";
        btnApplyImageTransform.Size = new Size(153, 33);
        btnApplyImageTransform.TabIndex = 11;
        btnApplyImageTransform.Text = "应用图片效果";
        btnApplyImageTransform.UseVisualStyleBackColor = false;
        btnApplyImageTransform.Click += btnApplyImageTransform_Click;
        //
        // cmbImageTransform
        //
        cmbImageTransform.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbImageTransform.FormattingEnabled = true;
        cmbImageTransform.Items.AddRange(new object[] { "原图", "横向显示", "横向翻转（左右镜像）", "上下翻转（垂直镜像）", "左右及上下翻转" });
        cmbImageTransform.Location = new Point(112, 108);
        cmbImageTransform.Name = "cmbImageTransform";
        cmbImageTransform.Size = new Size(195, 32);
        cmbImageTransform.TabIndex = 10;
        cmbImageTransform.SelectedIndexChanged += cmbImageTransform_SelectedIndexChanged;
        //
        // lblImageTransform
        //
        lblImageTransform.AutoSize = true;
        lblImageTransform.Location = new Point(18, 112);
        lblImageTransform.Name = "lblImageTransform";
        lblImageTransform.Size = new Size(100, 24);
        lblImageTransform.TabIndex = 9;
        lblImageTransform.Text = "图片效果：";
        // 
        // chkCloseSecondScreenOnStop
        // 
        chkCloseSecondScreenOnStop.AutoSize = true;
        chkCloseSecondScreenOnStop.Checked = true;
        chkCloseSecondScreenOnStop.CheckState = CheckState.Checked;
        chkCloseSecondScreenOnStop.Location = new Point(826, 71);
        chkCloseSecondScreenOnStop.Name = "chkCloseSecondScreenOnStop";
        chkCloseSecondScreenOnStop.Size = new Size(242, 28);
        chkCloseSecondScreenOnStop.TabIndex = 8;
        chkCloseSecondScreenOnStop.Text = "停止定时投图时关闭第二屏";
        chkCloseSecondScreenOnStop.UseVisualStyleBackColor = true;
        // 
        // lblProjectionState
        // 
        lblProjectionState.AutoEllipsis = true;
        lblProjectionState.ForeColor = Color.DimGray;
        lblProjectionState.Location = new Point(827, 27);
        lblProjectionState.Name = "lblProjectionState";
        lblProjectionState.Size = new Size(145, 28);
        lblProjectionState.TabIndex = 5;
        lblProjectionState.Text = "未启动定时投图";
        // 
        // numProjectionIntervalSeconds
        // 
        numProjectionIntervalSeconds.Location = new Point(762, 70);
        numProjectionIntervalSeconds.Maximum = new decimal(new int[] { 3600, 0, 0, 0 });
        numProjectionIntervalSeconds.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
        numProjectionIntervalSeconds.Name = "numProjectionIntervalSeconds";
        numProjectionIntervalSeconds.Size = new Size(74, 30);
        numProjectionIntervalSeconds.TabIndex = 7;
        numProjectionIntervalSeconds.Value = new decimal(new int[] { 5, 0, 0, 0 });
        // 
        // btnTimedProjection
        // 
        btnTimedProjection.BackColor = Color.FromArgb(255, 246, 215);
        btnTimedProjection.Location = new Point(978, 20);
        btnTimedProjection.Name = "btnTimedProjection";
        btnTimedProjection.Size = new Size(65, 35);
        btnTimedProjection.TabIndex = 4;
        btnTimedProjection.Text = "开始定时投图";
        btnTimedProjection.UseVisualStyleBackColor = false;
        btnTimedProjection.Click += btnTimedProjection_Click;
        // 
        // lblProjectionIntervalSeconds
        // 
        lblProjectionIntervalSeconds.AutoSize = true;
        lblProjectionIntervalSeconds.Location = new Point(639, 72);
        lblProjectionIntervalSeconds.Name = "lblProjectionIntervalSeconds";
        lblProjectionIntervalSeconds.Size = new Size(130, 24);
        lblProjectionIntervalSeconds.TabIndex = 6;
        lblProjectionIntervalSeconds.Text = "投图间隔(秒)：";
        // 
        // btnApplyTopology
        // 
        btnApplyTopology.BackColor = Color.WhiteSmoke;
        btnApplyTopology.Location = new Point(238, 65);
        btnApplyTopology.Name = "btnApplyTopology";
        btnApplyTopology.Size = new Size(200, 33);
        btnApplyTopology.TabIndex = 5;
        btnApplyTopology.Text = "立即应用投影模式";
        btnApplyTopology.UseVisualStyleBackColor = false;
        btnApplyTopology.Click += btnApplyTopology_Click;
        // 
        // cmbTopology
        // 
        cmbTopology.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbTopology.FormattingEnabled = true;
        cmbTopology.Items.AddRange(new object[] { "不切换投影模式", "仅电脑屏幕", "复制屏幕", "仅第二屏幕", "扩展屏幕" });
        cmbTopology.Location = new Point(112, 69);
        cmbTopology.Name = "cmbTopology";
        cmbTopology.Size = new Size(120, 32);
        cmbTopology.TabIndex = 4;
        // 
        // lblTopology
        // 
        lblTopology.AutoSize = true;
        lblTopology.Location = new Point(18, 73);
        lblTopology.Name = "lblTopology";
        lblTopology.Size = new Size(100, 24);
        lblTopology.TabIndex = 3;
        lblTopology.Text = "投影模式：";
        // 
        // btnBrowseDirectory
        // 
        btnBrowseDirectory.BackColor = Color.WhiteSmoke;
        btnBrowseDirectory.Location = new Point(639, 25);
        btnBrowseDirectory.Name = "btnBrowseDirectory";
        btnBrowseDirectory.Size = new Size(120, 33);
        btnBrowseDirectory.TabIndex = 2;
        btnBrowseDirectory.Text = "选择目录...";
        btnBrowseDirectory.UseVisualStyleBackColor = false;
        btnBrowseDirectory.Click += btnBrowseDirectory_Click;
        // 
        // txtImageDirectory
        // 
        txtImageDirectory.Location = new Point(112, 28);
        txtImageDirectory.Name = "txtImageDirectory";
        txtImageDirectory.Size = new Size(521, 30);
        txtImageDirectory.TabIndex = 1;
        // 
        // lblImageDirectory
        // 
        lblImageDirectory.AutoSize = true;
        lblImageDirectory.Location = new Point(18, 32);
        lblImageDirectory.Name = "lblImageDirectory";
        lblImageDirectory.Size = new Size(100, 24);
        lblImageDirectory.TabIndex = 0;
        lblImageDirectory.Text = "图片目录：";
        // 
        // grpImages
        // 
        grpImages.Controls.Add(txtProjectionLog);
        grpImages.Controls.Add(lblCurrentImage);
        grpImages.Controls.Add(picPreview);
        grpImages.Controls.Add(lvImages);
        grpImages.Location = new Point(12, 171);
        grpImages.Name = "grpImages";
        grpImages.Size = new Size(1066, 441);
        grpImages.TabIndex = 1;
        grpImages.TabStop = false;
        grpImages.Text = "投影图片";
        // 
        // txtProjectionLog
        // 
        txtProjectionLog.BackColor = Color.White;
        txtProjectionLog.Font = new Font("Consolas", 9F);
        txtProjectionLog.Location = new Point(639, 336);
        txtProjectionLog.Multiline = true;
        txtProjectionLog.Name = "txtProjectionLog";
        txtProjectionLog.ReadOnly = true;
        txtProjectionLog.ScrollBars = ScrollBars.Vertical;
        txtProjectionLog.Size = new Size(396, 80);
        txtProjectionLog.TabIndex = 3;
        // 
        // lblCurrentImage
        // 
        lblCurrentImage.AutoEllipsis = true;
        lblCurrentImage.Location = new Point(639, 294);
        lblCurrentImage.Name = "lblCurrentImage";
        lblCurrentImage.Size = new Size(396, 26);
        lblCurrentImage.TabIndex = 2;
        lblCurrentImage.Text = "当前图片：无";
        // 
        // picPreview
        // 
        picPreview.BackColor = Color.Black;
        picPreview.BorderStyle = BorderStyle.FixedSingle;
        picPreview.Location = new Point(639, 41);
        picPreview.Name = "picPreview";
        picPreview.Size = new Size(396, 250);
        picPreview.SizeMode = PictureBoxSizeMode.CenterImage;
        picPreview.TabIndex = 1;
        picPreview.TabStop = false;
        // 
        // lvImages
        // 
        lvImages.Columns.AddRange(new ColumnHeader[] { colIndex, colFileName, colStatus });
        lvImages.FullRowSelect = true;
        lvImages.GridLines = true;
        lvImages.Location = new Point(16, 30);
        lvImages.MultiSelect = false;
        lvImages.Name = "lvImages";
        lvImages.Size = new Size(552, 394);
        lvImages.TabIndex = 0;
        lvImages.UseCompatibleStateImageBehavior = false;
        lvImages.View = View.Details;
        lvImages.SelectedIndexChanged += lvImages_SelectedIndexChanged;
        // 
        // colIndex
        // 
        colIndex.Text = "序号";
        colIndex.Width = 55;
        // 
        // colFileName
        // 
        colFileName.Text = "文件名";
        colFileName.Width = 285;
        // 
        // colStatus
        // 
        colStatus.Text = "状态";
        colStatus.Width = 82;
        // 
        // btnRefreshImages
        // 
        btnRefreshImages.BackColor = Color.WhiteSmoke;
        btnRefreshImages.Location = new Point(15, 780);
        btnRefreshImages.Name = "btnRefreshImages";
        btnRefreshImages.Size = new Size(193, 40);
        btnRefreshImages.TabIndex = 2;
        btnRefreshImages.Text = "刷新图片列表";
        btnRefreshImages.UseVisualStyleBackColor = false;
        btnRefreshImages.Click += btnRefreshImages_Click;
        // 
        // btnProjectNext
        // 
        btnProjectNext.BackColor = Color.FromArgb(228, 240, 252);
        btnProjectNext.Location = new Point(240, 780);
        btnProjectNext.Name = "btnProjectNext";
        btnProjectNext.Size = new Size(150, 40);
        btnProjectNext.TabIndex = 3;
        btnProjectNext.Text = "切换并投下一张";
        btnProjectNext.UseVisualStyleBackColor = false;
        btnProjectNext.Click += btnProjectNext_Click;
        // 
        // btnProjectSelected
        // 
        btnProjectSelected.BackColor = Color.FromArgb(225, 245, 229);
        btnProjectSelected.Location = new Point(458, 65);
        btnProjectSelected.Name = "btnProjectSelected";
        btnProjectSelected.Size = new Size(150, 36);
        btnProjectSelected.TabIndex = 4;
        btnProjectSelected.Text = "投放选中图片";
        btnProjectSelected.UseVisualStyleBackColor = false;
        btnProjectSelected.Click += btnProjectSelected_Click;
        //
        // grpData
        //
        grpData.Controls.Add(lblDataProcessingState);
        grpData.Controls.Add(btnBrowseOutputDirectory);
        grpData.Controls.Add(txtOutputDirectory);
        grpData.Controls.Add(lblOutputDirectory);
        grpData.Controls.Add(btnBrowseDataSourceDirectory);
        grpData.Controls.Add(txtDataSourceDirectory);
        grpData.Controls.Add(lblDataSourceDirectory);
        grpData.Location = new Point(12, 620);
        grpData.Name = "grpData";
        grpData.Size = new Size(1066, 145);
        grpData.TabIndex = 2;
        grpData.TabStop = false;
        grpData.Text = "串扰数据（测试完成后自动处理）";
        //
        // lblDataProcessingState
        //
        lblDataProcessingState.AutoEllipsis = true;
        lblDataProcessingState.ForeColor = Color.DimGray;
        lblDataProcessingState.Location = new Point(18, 108);
        lblDataProcessingState.Name = "lblDataProcessingState";
        lblDataProcessingState.Size = new Size(1029, 26);
        lblDataProcessingState.TabIndex = 6;
        lblDataProcessingState.Text = "按本次测试开始时间和图片数量匹配一级导出文件夹；最后一份数据作为本底。";
        //
        // btnBrowseOutputDirectory
        //
        btnBrowseOutputDirectory.BackColor = Color.WhiteSmoke;
        btnBrowseOutputDirectory.Location = new Point(912, 69);
        btnBrowseOutputDirectory.Name = "btnBrowseOutputDirectory";
        btnBrowseOutputDirectory.Size = new Size(135, 33);
        btnBrowseOutputDirectory.TabIndex = 5;
        btnBrowseOutputDirectory.Text = "选择输出...";
        btnBrowseOutputDirectory.UseVisualStyleBackColor = false;
        btnBrowseOutputDirectory.Click += btnBrowseOutputDirectory_Click;
        //
        // txtOutputDirectory
        //
        txtOutputDirectory.Location = new Point(142, 71);
        txtOutputDirectory.Name = "txtOutputDirectory";
        txtOutputDirectory.Size = new Size(760, 30);
        txtOutputDirectory.TabIndex = 4;
        //
        // lblOutputDirectory
        //
        lblOutputDirectory.AutoSize = true;
        lblOutputDirectory.Location = new Point(18, 74);
        lblOutputDirectory.Name = "lblOutputDirectory";
        lblOutputDirectory.Size = new Size(118, 24);
        lblOutputDirectory.TabIndex = 3;
        lblOutputDirectory.Text = "结果输出目录：";
        //
        // btnBrowseDataSourceDirectory
        //
        btnBrowseDataSourceDirectory.BackColor = Color.WhiteSmoke;
        btnBrowseDataSourceDirectory.Location = new Point(912, 27);
        btnBrowseDataSourceDirectory.Name = "btnBrowseDataSourceDirectory";
        btnBrowseDataSourceDirectory.Size = new Size(135, 33);
        btnBrowseDataSourceDirectory.TabIndex = 2;
        btnBrowseDataSourceDirectory.Text = "选择数据...";
        btnBrowseDataSourceDirectory.UseVisualStyleBackColor = false;
        btnBrowseDataSourceDirectory.Click += btnBrowseDataSourceDirectory_Click;
        //
        // txtDataSourceDirectory
        //
        txtDataSourceDirectory.Location = new Point(142, 29);
        txtDataSourceDirectory.Name = "txtDataSourceDirectory";
        txtDataSourceDirectory.Size = new Size(760, 30);
        txtDataSourceDirectory.TabIndex = 1;
        txtDataSourceDirectory.Text = "D:\\Program Files\\GYTech\\Setup_MRTest\\ExportFile";
        //
        // lblDataSourceDirectory
        //
        lblDataSourceDirectory.AutoSize = true;
        lblDataSourceDirectory.Location = new Point(18, 32);
        lblDataSourceDirectory.Name = "lblDataSourceDirectory";
        lblDataSourceDirectory.Size = new Size(118, 24);
        lblDataSourceDirectory.TabIndex = 0;
        lblDataSourceDirectory.Text = "原始数据目录：";
        // 
        // btnClose
        // 
        btnClose.BackColor = Color.WhiteSmoke;
        btnClose.Location = new Point(945, 780);
        btnClose.Name = "btnClose";
        btnClose.Size = new Size(110, 40);
        btnClose.TabIndex = 6;
        btnClose.Text = "关闭";
        btnClose.UseVisualStyleBackColor = false;
        btnClose.Click += btnClose_Click;
        // 
        // projectionTimer
        // 
        projectionTimer.Interval = 5000;
        projectionTimer.Tick += projectionTimer_Tick;
        // 
        // btnCrosstalkTest
        // 
        btnCrosstalkTest.Location = new Point(774, 780);
        btnCrosstalkTest.Name = "btnCrosstalkTest";
        btnCrosstalkTest.Size = new Size(137, 42);
        btnCrosstalkTest.TabIndex = 7;
        btnCrosstalkTest.Text = "串扰测试";
        btnCrosstalkTest.UseVisualStyleBackColor = true;
        btnCrosstalkTest.Click += btnCrosstalkTest_Click;
        // 
        // ProjectionForm
        // 
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Color.FromArgb(232, 242, 247);
        ClientSize = new Size(1096, 835);
        Controls.Add(btnCrosstalkTest);
        Controls.Add(btnClose);
        Controls.Add(grpData);
        Controls.Add(btnProjectNext);
        Controls.Add(btnRefreshImages);
        Controls.Add(grpImages);
        Controls.Add(grpSettings);
        Font = new Font("Microsoft YaHei UI", 9F);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        Name = "ProjectionForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "扩展投影切图 / 串扰数据处理 - v1.0.260817";
        FormClosing += ProjectionForm_FormClosing;
        Load += ProjectionForm_Load;
        grpSettings.ResumeLayout(false);
        grpSettings.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)numProjectionIntervalSeconds).EndInit();
        grpImages.ResumeLayout(false);
        grpImages.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)picPreview).EndInit();
        grpData.ResumeLayout(false);
        grpData.PerformLayout();
        ResumeLayout(false);
    }

    #endregion

    // 以下字段是窗体上的控件。逻辑文件使用这些字段更新状态、读取选择并输出日志。
    private GroupBox grpSettings;
    private Button btnApplyImageTransform;
    private ComboBox cmbImageTransform;
    private Label lblImageTransform;
    private CheckBox chkCloseSecondScreenOnStop;
    private NumericUpDown numProjectionIntervalSeconds;
    private Label lblProjectionIntervalSeconds;
    private Button btnApplyTopology;
    private ComboBox cmbTopology;
    private Label lblTopology;
    private Button btnBrowseDirectory;
    private TextBox txtImageDirectory;
    private Label lblImageDirectory;
    private GroupBox grpImages;
    private TextBox txtProjectionLog;
    private Label lblCurrentImage;
    private PictureBox picPreview;
    private ListView lvImages;
    private ColumnHeader colIndex;
    private ColumnHeader colFileName;
    private ColumnHeader colStatus;
    private Button btnRefreshImages;
    private Button btnProjectNext;
    private Button btnProjectSelected;
    private GroupBox grpData;
    private Label lblDataProcessingState;
    private Button btnBrowseOutputDirectory;
    private TextBox txtOutputDirectory;
    private Label lblOutputDirectory;
    private Button btnBrowseDataSourceDirectory;
    private TextBox txtDataSourceDirectory;
    private Label lblDataSourceDirectory;
    private Button btnTimedProjection;
    private Label lblProjectionState;
    private Button btnClose;
    private System.Windows.Forms.Timer projectionTimer;
    private Button btnCrosstalkTest;
}
