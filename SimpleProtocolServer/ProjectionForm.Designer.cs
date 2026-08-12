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
        chkRestoreWallpaper = new CheckBox();
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
        btnClose = new Button();
        projectionTimer = new System.Windows.Forms.Timer(components);
        button1 = new Button();
        grpSettings.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)numProjectionIntervalSeconds).BeginInit();
        grpImages.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)picPreview).BeginInit();
        SuspendLayout();
        // 
        // grpSettings
        // 
        grpSettings.Controls.Add(btnProjectSelected);
        grpSettings.Controls.Add(chkRestoreWallpaper);
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
        grpSettings.Size = new Size(1066, 112);
        grpSettings.TabIndex = 0;
        grpSettings.TabStop = false;
        grpSettings.Text = "投影设置";
        // 
        // chkRestoreWallpaper
        // 
        chkRestoreWallpaper.AutoSize = true;
        chkRestoreWallpaper.Checked = true;
        chkRestoreWallpaper.CheckState = CheckState.Checked;
        chkRestoreWallpaper.Location = new Point(862, 71);
        chkRestoreWallpaper.Name = "chkRestoreWallpaper";
        chkRestoreWallpaper.Size = new Size(206, 28);
        chkRestoreWallpaper.TabIndex = 8;
        chkRestoreWallpaper.Text = "停止/关闭后恢复桌面";
        chkRestoreWallpaper.UseVisualStyleBackColor = true;
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
        grpImages.Location = new Point(12, 132);
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
        btnRefreshImages.Location = new Point(15, 591);
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
        btnProjectNext.Location = new Point(240, 591);
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
        // btnClose
        // 
        btnClose.BackColor = Color.WhiteSmoke;
        btnClose.Location = new Point(945, 592);
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
        // button1
        // 
        button1.Location = new Point(774, 592);
        button1.Name = "button1";
        button1.Size = new Size(137, 42);
        button1.TabIndex = 7;
        button1.Text = "串扰测试";
        button1.UseVisualStyleBackColor = true;
        button1.Click += button1_Click;
        // 
        // ProjectionForm
        // 
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Color.FromArgb(232, 242, 247);
        ClientSize = new Size(1096, 643);
        Controls.Add(button1);
        Controls.Add(btnClose);
        Controls.Add(btnProjectNext);
        Controls.Add(btnRefreshImages);
        Controls.Add(grpImages);
        Controls.Add(grpSettings);
        Font = new Font("Microsoft YaHei UI", 9F);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        Name = "ProjectionForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "扩展投影切图 / 定时投图";
        FormClosing += ProjectionForm_FormClosing;
        Load += ProjectionForm_Load;
        grpSettings.ResumeLayout(false);
        grpSettings.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)numProjectionIntervalSeconds).EndInit();
        grpImages.ResumeLayout(false);
        grpImages.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)picPreview).EndInit();
        ResumeLayout(false);
    }

    #endregion

    // 以下字段是窗体上的控件。逻辑文件使用这些字段更新状态、读取选择并输出日志。
    private GroupBox grpSettings;
    private CheckBox chkRestoreWallpaper;
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
    private Button btnTimedProjection;
    private Label lblProjectionState;
    private Button btnClose;
    private System.Windows.Forms.Timer projectionTimer;
    private Button button1;
}
