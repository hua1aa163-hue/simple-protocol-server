using System.Net;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using AutoTestClient.Models;
using AutoTestClient.Logging;
using AutoTestClient.Monitoring;
using AutoTestClient.Networking;
using AutoTestClient.Projection;
using AutoTestClient.Protocol;
using AutoTestClient.Settings;
using AutoTestClient.Workflow;
using AutoTestClient.Controls;
using AutoTestClient.DataProcessing;

namespace AutoTestClient;

/// <summary>
/// 一键测试主界面。所有可编辑控件的值在关闭时写回用户配置目录。
/// </summary>
public partial class DashboardForm : Form
{
    private readonly SettingsStore _settingsStore = new();
    private readonly TcpMessageServer _server = new();
    private readonly MrTestDialogMonitor _monitor = new();
    private readonly ScreenImageProjector _projector;
    private readonly TestDataProcessingService _dataProcessor = new();
    private TestPlanConfiguration _configuration = new();
    private TestPlanRunner? _runner;
    private Task? _runTask;
    private DataProcessing.TestDataProcessingResult? _lastCrosstalkResult;
    // 一轮中可能产生多个串扰热图；保留每一批的结果对象，允许测试结束后逐个查看。
    // 结果目录由数据处理层独立创建，这里只保存引用，不复制或覆盖任何文件。
    private readonly List<CrosstalkResultEntry> _crosstalkResultHistory = new();
    // 当前主界面热图的原始矩阵和参数；原始矩阵保留后，修改阈值/色轴/掩膜
    // 时只需重新计算，不必重新触发 MRTEST 测量。
    private CrosstalkCalculationResult? _crosstalkCalculation;
    private CrosstalkAnalysisOptions? _crosstalkAnalysisOptions;
    // 手动报文是异步事务；在收到最终应答前禁止再次点击，避免重复测量。
    private bool _manualSendInProgress;
    // 独立记录一键测试状态，避免手动事务结束时覆盖 SetRunningUi 的禁用状态。
    private bool _planRunning;
    private bool _closing;
    private readonly LogLineBuffer _logLines = new(cleanupThreshold: 200, retainedAfterCleanup: 150);
    // 手动报文没有固定图卡协调器；发送窗口内若出现弹窗，由这里兜底确认。
    // 一键计划期间仍只交给 FixedPopupSequenceCoordinator，避免两个消费者
    // 同时取同一张图卡。事务结束后监视器停止，不处理空闲窗口。
    private readonly object _manualDialogGate = new();
    private readonly HashSet<nint> _manualDialogConfirming = new();
    public DashboardForm()
    {
        InitializeComponent();
        _projector = new ScreenImageProjector(SynchronizationContext.Current);
        heatmapPreview.MaskSelected += HeatmapPreview_MaskSelected;
        if (IsInDesigner()) return;
        _server.ClientConnected += Server_ClientConnected;
        _server.MessageSent += Server_MessageSent;
        _server.MessageReceived += Server_MessageReceived;
        _server.ConnectionClosed += Server_ConnectionClosed;
        _server.ProtocolError += Server_ProtocolError;
        _monitor.MonitorError += (_, ex) => AppendLog($"弹窗监视错误：{ex.Message}");
        _monitor.MonitorLog += (_, message) => AppendLog($"弹窗监视：{message}");
        _monitor.DialogDetected += Monitor_DialogDetected;
    }

    private async void DashboardForm_Load(object? sender, EventArgs e)
    {
        if (IsInDesigner()) return;
        _configuration = _settingsStore.Load();
        ApplyConfigurationToControls();
        InitializeNamedPlans();
        AppendLog($"配置文件：{_settingsStore.FilePath}");
        AppendLog("已加载首版测试计划。TCP 角色为服务端，等待 MRTEST/设备客户端连接。");
        AppendLog($"测量请求固定默认值：{MessageProtocol.DefaultMeasurementRequest}；完成等待上限：600 秒。");
        // 与参考服务端一致，启动后自动监听；按钮仍可随时停止/重启。
        await StartListeningFromSettingsAsync();
    }

    private void ApplyConfigurationToControls()
    {
        textBoxBindAddress.Text = _configuration.BindAddress;
        textBoxPort.Text = _configuration.Port.ToString();
        textBoxMrTest.Text = _configuration.MrTestExecutablePath;
        textBoxExport.Text = _configuration.ExportDirectory;
        textBoxRecipeDir.Text = _configuration.RecipeDirectory;
        textBoxImageDir.Text = _configuration.ImageDirectory;
        textBoxOutputDir.Text = _configuration.OutputDirectory;
        textBoxManualCommand.Text = string.IsNullOrWhiteSpace(_configuration.ManualCommand)
            ? MessageProtocol.DefaultMeasurementRequest : _configuration.ManualCommand;
        numericWholeRepeat.Value = Math.Clamp(_configuration.WholePlanRepeatCount, 1, 9999);
        numericPopupDelay.Value = Math.Clamp(_configuration.PopupStabilizeDelayMs, 0, 60000);
        numericProjectRepeat.Value = Math.Clamp(_configuration.DefaultProjectRepeatCount, 1, 9999);
        comboProjectionMode.SelectedIndex = _configuration.ProjectionMode == ProjectionMode.FitToWindow ? 1 : 0;
        checkAutoConfirm.Checked = _configuration.AutoConfirmPopups;
        heatmapPreview.ColorMinimumPercent = _configuration.CrosstalkColorAxisMinimumPercent;
        heatmapPreview.ColorMaximumPercent = _configuration.CrosstalkColorAxisMaximumPercent;
        heatmapPreview.AnomalyThresholdPercent = _configuration.CrosstalkAbnormalThresholdRatio * 100d;
        SetCrosstalkThresholdControlValue(heatmapPreview.AnomalyThresholdPercent);
        checkedListProjects.Items.Clear();
        foreach (TestProject project in _configuration.Projects.OrderBy(p => p.Order))
            checkedListProjects.Items.Add($"{project.Order}. {project.Name} [{project.KindDisplayName}] ×{project.RepeatCount}", project.Enabled);
        if (checkedListProjects.Items.Count > 0)
            checkedListProjects.SelectedIndex = Math.Clamp(_configuration.SelectedProjectIndex, 0, checkedListProjects.Items.Count - 1);
        UpdateSelectedProjectRepeat();
        UpdateManualButtonState();
    }

    private void ReadConfigurationFromControls()
    {
        _configuration.BindAddress = textBoxBindAddress.Text.Trim();
        if (int.TryParse(textBoxPort.Text.Trim(), out int port)) _configuration.Port = Math.Clamp(port, 1, 65535);
        _configuration.MrTestExecutablePath = textBoxMrTest.Text.Trim();
        _configuration.ExportDirectory = textBoxExport.Text.Trim();
        _configuration.RecipeDirectory = textBoxRecipeDir.Text.Trim();
        _configuration.ImageDirectory = textBoxImageDir.Text.Trim();
        _configuration.OutputDirectory = textBoxOutputDir.Text.Trim();
        _configuration.ManualCommand = textBoxManualCommand.Text.Trim();
        _configuration.WholePlanRepeatCount = (int)numericWholeRepeat.Value;
        _configuration.DefaultProjectRepeatCount = (int)numericProjectRepeat.Value;
        _configuration.PopupStabilizeDelayMs = (int)numericPopupDelay.Value;
        _configuration.AutoConfirmPopups = checkAutoConfirm.Checked;
        _configuration.ProjectionMode = comboProjectionMode.SelectedIndex == 1 ? ProjectionMode.FitToWindow : ProjectionMode.PixelPerfect;
        // 串扰参数由完整分析窗口维护；这里仍把当前预览属性写回，保证设计器/运行时
        // 调整后的值会随主窗口关闭保存。
        if (double.IsFinite(heatmapPreview.AnomalyThresholdPercent))
            _configuration.CrosstalkAbnormalThresholdRatio = heatmapPreview.AnomalyThresholdPercent / 100d;
        if (numericCrosstalkThreshold is not null &&
            double.IsFinite((double)numericCrosstalkThreshold.Value))
        {
            _configuration.CrosstalkAbnormalThresholdRatio =
                (double)numericCrosstalkThreshold.Value / 100d;
            heatmapPreview.AnomalyThresholdPercent = (double)numericCrosstalkThreshold.Value;
        }
        if (double.IsFinite(heatmapPreview.ColorMinimumPercent))
            _configuration.CrosstalkColorAxisMinimumPercent = heatmapPreview.ColorMinimumPercent;
        if (double.IsFinite(heatmapPreview.ColorMaximumPercent))
            _configuration.CrosstalkColorAxisMaximumPercent = heatmapPreview.ColorMaximumPercent;
        for (int i = 0; i < _configuration.Projects.Count && i < checkedListProjects.Items.Count; i++)
            _configuration.Projects[i].Enabled = checkedListProjects.GetItemChecked(i);
        int selectedProject = checkedListProjects.SelectedIndex;
        if (selectedProject >= 0 && selectedProject < _configuration.Projects.Count)
            _configuration.Projects[selectedProject].RepeatCount = (int)numericProjectRepeat.Value;
        _configuration.SelectedProjectIndex = Math.Max(0, checkedListProjects.SelectedIndex);
        _configuration.Normalize();
    }

    private async void ButtonListen_Click(object? sender, EventArgs e)
    {
        if (_server.IsListening)
        {
            await StopListeningAsync();
            return;
        }
        await StartListeningFromSettingsAsync();
    }

    private async Task StartListeningFromSettingsAsync()
    {
        ReadConfigurationFromControls();
        if (!TryResolveAddress(_configuration.BindAddress, out IPAddress? address, out string error))
        {
            AppendLog(error);
            if (!_closing) MessageBox.Show(this, error, "监听地址", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        try
        {
            await _server.StartAsync(address!, _configuration.Port);
            buttonListen.Text = "停止监听";
            labelConnectionState.Text = $"监听中：{address}:{_server.Port}，等待客户端";
            UpdateManualButtonState();
            AppendLog($"TCP 服务端已监听 {address}:{_server.Port}");
        }
        catch (Exception ex)
        {
            AppendLog($"启动监听失败：{ex.Message}");
            if (!_closing) MessageBox.Show(this, ex.Message, "启动监听失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task StopListeningAsync()
    {
        try { await _server.StopAsync(); }
        catch (Exception ex) { AppendLog($"停止监听失败：{ex.Message}"); }
        buttonListen.Text = "启动监听";
        labelConnectionState.Text = "未监听";
        UpdateManualButtonState();
    }

    private async void ButtonStart_Click(object? sender, EventArgs e)
    {
        if (_runTask is { IsCompleted: false }) return;
        ReadConfigurationFromControls();
        SyncActiveNamedPlanSnapshot();
        try { _settingsStore.Save(_configuration); } catch (Exception ex) { AppendLog($"保存配置失败：{ex.Message}"); }
        if (!_server.IsListening || !_server.IsConnected)
        {
            MessageBox.Show(this, "请先启动监听，并等待 MRTEST/设备客户端连接。", "无法开始", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        try
        {
            _dataProcessor.DefaultCrosstalkOptions = BuildCrosstalkAnalysisOptions();
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or InvalidDataException)
        {
            ShowCrosstalkError(ex);
            return;
        }
        _runner ??= new TestPlanRunner(_server, _projector, _monitor, _dataProcessor,
            log: message => { AppendLog(message); return Task.CompletedTask; });
        _runner.DataResultProduced -= Runner_DataResultProduced;
        _runner.DataResultProduced += Runner_DataResultProduced;
        _runner.ProgressChanged -= Runner_ProgressChanged;
        _runner.ProgressChanged += Runner_ProgressChanged;
        ResetResultDisplayHistory();
        ResetCrosstalkResultHistory();
        SetRunningUi(true);
        _runTask = RunPlanSafeAsync(_configuration);
        await _runTask;
    }

    private async Task RunPlanSafeAsync(TestPlanConfiguration configuration)
    {
        try
        {
            await _runner!.RunAsync(configuration);
            AppendLog("一键测试计划完成。");
        }
        catch (OperationCanceledException) { AppendLog("一键测试已停止。"); }
        catch (Exception ex)
        {
            AppendLog($"一键测试失败：{ex.Message}");
            if (!_closing) MessageBox.Show(this, ex.Message, "测试失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            if (!_closing)
            {
                SetRunningUi(false);
                labelProgress.Text = _runner?.State switch
                {
                    TestRunState.Completed => "完成",
                    TestRunState.Cancelled => "已停止",
                    TestRunState.Failed => "失败",
                    _ => "等待开始"
                };
            }
        }
    }

    private async void ButtonStop_Click(object? sender, EventArgs e)
    {
        _runner?.Cancel();
        if (_server.IsConnected)
        {
            try
            {
                await _server.SendAndWaitForCompletionAsync("&|Stop|@", TimeSpan.FromSeconds(10));
                AppendLog("已向设备发送停止命令。");
            }
            catch (Exception ex) { AppendLog($"停止命令未完成：{ex.Message}"); }
        }
    }

    private async void ButtonSendManual_Click(object? sender, EventArgs e)
    {
        if (_manualSendInProgress) return;

        // 旧版本默认值曾误把 Run 中间响应当作发送请求。手动发送入口
        // 也必须先迁移，避免用户配置或旧界面残留值绕过设置迁移直接发出。
        string command = MessageProtocol.MigrateMeasurementRequest(textBoxManualCommand.Text);
        if (!string.Equals(command, textBoxManualCommand.Text.Trim(), StringComparison.Ordinal))
        {
            textBoxManualCommand.Text = command;
            AppendLog($"已将历史测量请求迁移为：{command}");
        }
        if (!MessageProtocol.TryValidateMessage(command, out string normalized, out string error))
        {
            MessageBox.Show(this, error, "报文格式", MessageBoxButtons.OK, MessageBoxIcon.Warning); return;
        }
        normalized = MessageProtocol.MigrateMeasurementRequest(normalized);
        if (!_server.IsListening || !_server.IsConnected)
        {
            const string notConnected = "请先启动监听，并等待 MRTEST/设备客户端连接。";
            AppendLog($"手动报文未发送：{notConnected}");
            if (!_closing)
                MessageBox.Show(this, notConnected, "无法发送报文", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _manualSendInProgress = true;
        UpdateManualButtonState();
        try
        {
            ReadConfigurationFromControls();
            _configuration.ManualCommand = normalized;
            SyncActiveNamedPlanSnapshot();
            try { _settingsStore.Save(_configuration); } catch (Exception ex) { AppendLog($"保存手动报文失败：{ex.Message}"); }
            await _monitor.StopAsync();
            _monitor.Start(_configuration.MrTestExecutablePath);
            AppendLog("手动报文发送窗口已启动 MRTEST 弹窗监视。");
            string response = await _server.SendAndWaitForCompletionAsync(normalized);
            AppendLog($"手动报文完成：{response}");
        }
        catch (Exception ex)
        {
            AppendLog($"手动报文失败：{ex.Message}");
            if (!_closing)
                MessageBox.Show(this, ex.Message, "报文失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            try
            {
                await _monitor.StopAsync();
                AppendLog("手动报文事务结束，MRTEST 弹窗监视已停止。");
            }
            catch (Exception ex)
            {
                AppendLog($"停止 MRTEST 弹窗监视失败：{ex.Message}");
            }
            _manualSendInProgress = false;
            UpdateManualButtonState();
        }
    }

    private void ButtonClearLog_Click(object? sender, EventArgs e)
    {
        _logLines.Clear();
        textBoxLog.Clear();
    }

    private void ButtonRecipeManager_Click(object? sender, EventArgs e)
    {
        ReadConfigurationFromControls();
        using var form = new RecipeManagerForm(_configuration, _settingsStore);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            _configuration = form.Configuration;
            ApplyConfigurationToControls();
            SyncActiveNamedPlanSnapshot();
            _settingsStore.Save(_configuration);
            AppendLog("测试项目/固定图卡顺序已更新并保存。");
        }
    }

    /// <summary>
    /// 打开测试数据展示规则编辑器。编辑器使用配置副本，只有点击保存（或
    /// 关闭窗口触发保存）后才把规则写回主界面和用户配置；测量运行期间
    /// 禁止打开，避免修改中的规则与正在执行的项目产生歧义。
    /// </summary>
    private void ButtonDataDisplayRules_Click(object? sender, EventArgs e)
    {
        if (_planRunning || _closing)
        {
            AppendLog("一键测试进行中，暂不能编辑数据展示规则。");
            return;
        }

        ReadConfigurationFromControls();
        using var form = new TestDataDisplayRuleForm(_configuration, _settingsStore);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            _configuration = form.Configuration;
            ApplyConfigurationToControls();
            SyncActiveNamedPlanSnapshot();
            try { _settingsStore.Save(_configuration); }
            catch (Exception ex) { AppendLog($"保存数据展示规则失败：{ex.Message}"); }
            AppendLog($"数据展示规则已更新：{_configuration.DisplayRules.Count} 条。");
        }
    }

    private void Runner_DataResultProduced(object? sender, DataProcessing.TestDataProcessingResult result)
    {
        OnUi(() =>
        {
            AddDisplayResult(result);
            if (result.Kind == TestProjectKind.Crosstalk &&
                (!string.IsNullOrWhiteSpace(result.HeatmapPath) || result.CrosstalkCalculation is not null))
            {
                var entry = new CrosstalkResultEntry(
                    result,
                    BuildCrosstalkResultDisplay(result, _crosstalkResultHistory.Count + 1));
                _crosstalkResultHistory.Add(entry);
                comboCrosstalkResults.Items.Add(entry);
                // 新结果默认选中，保持原有“查看串扰热图”按钮语义；
                // 用户也可以在测试结束后从下拉框切换到前几次结果。
                comboCrosstalkResults.SelectedIndex = comboCrosstalkResults.Items.Count - 1;
                _lastCrosstalkResult = result;
                UpdateCrosstalkViewState();
                if (result.CrosstalkCalculation is not null)
                    ApplyCrosstalkAnalysisToDashboard(result.CrosstalkCalculation, result.CrosstalkOptions);
                else if (!string.IsNullOrWhiteSpace(result.HeatmapPath))
                    _ = TryLoadCrosstalkPreviewImage(result.HeatmapPath);
            }
        });
    }

    private void ComboCrosstalkResults_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (comboCrosstalkResults.SelectedItem is CrosstalkResultEntry entry)
        {
            _lastCrosstalkResult = entry.Result;
            if (entry.Result.CrosstalkCalculation is not null)
                ApplyCrosstalkAnalysisToDashboard(entry.Result.CrosstalkCalculation, entry.Result.CrosstalkOptions);
            else if (!string.IsNullOrWhiteSpace(entry.Result.HeatmapPath))
                TryLoadCrosstalkPreviewImage(entry.Result.HeatmapPath);
        }
        else if (_crosstalkResultHistory.Count == 0)
            _lastCrosstalkResult = null;
        UpdateCrosstalkViewState();
    }

    private void ButtonViewCrosstalk_Click(object? sender, EventArgs e)
    {
        if (_lastCrosstalkResult is null)
        {
            ButtonCrosstalkAnalyze_Click(sender, e);
            return;
        }

        // Reuse the analyzer's last editable source/output paths and options
        // when opening a result from the plan.  The result itself remains the
        // immutable input batch; the analyzer can still export a new masked
        // version without overwriting that batch.
        CrosstalkAnalysisOptions options;
        try
        {
            options = BuildCrosstalkAnalysisOptions();
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or InvalidDataException)
        {
            ShowCrosstalkError(ex);
            return;
        }
        string sourceRoot = string.IsNullOrWhiteSpace(_configuration.CrosstalkAnalysisSourceDirectory)
            ? _lastCrosstalkResult.OutputDirectory ?? _configuration.ExportDirectory
            : _configuration.CrosstalkAnalysisSourceDirectory;
        string outputRoot = string.IsNullOrWhiteSpace(_configuration.CrosstalkAnalysisOutputDirectory)
            ? _configuration.OutputDirectory
            : _configuration.CrosstalkAnalysisOutputDirectory;
        using var form = new CrosstalkResultForm(
            _lastCrosstalkResult,
            _lastCrosstalkResult.CrosstalkCalculation,
            sourceRoot,
            outputRoot,
            options);
        form.ShowDialog(this);
        PersistCrosstalkFormState(form);
        if (form.CurrentCalculation is not null)
            ApplyCrosstalkAnalysisToDashboard(form.CurrentCalculation, form.CurrentOptions);
    }

    /// <summary>主界面“串扰分析/参数”按钮：直接选择 ExportFile 根目录并打开完整分析器。</summary>
    private void ButtonCrosstalkAnalyze_Click(object? sender, EventArgs e)
    {
        if (_planRunning)
        {
            AppendLog("一键测试进行中，暂不能打开串扰分析器。");
            return;
        }

        ReadConfigurationFromControls();
        CrosstalkAnalysisOptions options;
        try
        {
            options = BuildCrosstalkAnalysisOptions();
        }
        catch (Exception ex)
        {
            ShowCrosstalkError(ex);
            return;
        }

        string sourceRoot = string.IsNullOrWhiteSpace(_configuration.CrosstalkAnalysisSourceDirectory)
            ? _configuration.ExportDirectory
            : _configuration.CrosstalkAnalysisSourceDirectory;
        string outputRoot = string.IsNullOrWhiteSpace(_configuration.CrosstalkAnalysisOutputDirectory)
            ? _configuration.OutputDirectory
            : _configuration.CrosstalkAnalysisOutputDirectory;
        using var form = new CrosstalkResultForm(
            sourceRoot,
            outputRoot,
            options);
        form.AnalysisChanged += CrosstalkForm_AnalysisChanged;
        form.ShowDialog(this);
        form.AnalysisChanged -= CrosstalkForm_AnalysisChanged;
        PersistCrosstalkFormState(form);
        if (form.CurrentCalculation is not null)
            ApplyCrosstalkAnalysisToDashboard(form.CurrentCalculation, form.CurrentOptions);
    }

    /// <summary>
    /// 将完整分析器的可编辑状态（路径、阈值、色轴、坐标掩膜和备注）
    /// 写回主配置。这样关闭分析器后再次打开仍使用上次修改后的值。
    /// </summary>
    private void PersistCrosstalkFormState(CrosstalkResultForm form)
    {
        if (form is null) return;
        _configuration.CrosstalkAnalysisSourceDirectory = form.CurrentSourceRoot;
        _configuration.CrosstalkAnalysisOutputDirectory = form.CurrentOutputRoot;
        _configuration.CrosstalkMaskCoordinates = form.CurrentMaskCoordinates;
        _configuration.CrosstalkMaskNote = form.CurrentMaskNote;
        ApplyCrosstalkOptionsToConfiguration(form.CurrentOptions);
        // 即使分析器尚未计算任何矩阵，也要让主界面的可编辑阈值
        // 立即反映刚刚保存的值。
        heatmapPreview.AnomalyThresholdPercent = form.CurrentOptions.AbnormalThresholdPercent;
        heatmapPreview.ColorMinimumPercent = form.CurrentOptions.ColorAxisMinimumPercent;
        heatmapPreview.ColorMaximumPercent = form.CurrentOptions.ColorAxisMaximumPercent;
        SetCrosstalkThresholdControlValue(form.CurrentOptions.AbnormalThresholdPercent);
        SyncActiveNamedPlanSnapshot();
        try { _settingsStore.Save(_configuration); }
        catch (Exception ex) { AppendLog($"保存串扰分析参数失败：{ex.Message}"); }
    }

    private void ButtonCrosstalkOpenResult_Click(object? sender, EventArgs e)
    {
        if (_lastCrosstalkResult is not null)
        {
            ButtonViewCrosstalk_Click(sender, e);
            return;
        }
        ButtonCrosstalkAnalyze_Click(sender, e);
    }

    private void CrosstalkForm_AnalysisChanged(object? sender, EventArgs e)
    {
        if (sender is CrosstalkResultForm form && form.CurrentCalculation is not null)
            OnUi(() => ApplyCrosstalkAnalysisToDashboard(form.CurrentCalculation, form.CurrentOptions));
    }

    private void HeatmapPreview_MaskSelected(object? sender, HeatmapMaskSelectedEventArgs e)
    {
        if (_crosstalkCalculation is null) return;
        bool[,] mask = _crosstalkCalculation.UserMask.Length == 0
            ? new bool[CrosstalkDataProcessor.HeatmapRows, CrosstalkDataProcessor.HeatmapColumns]
            : (bool[,])_crosstalkCalculation.UserMask.Clone();
        try
        {
            CrosstalkMaskStore.AddRectangle(mask, e.ColumnStart, e.RowStart, e.ColumnEnd, e.RowEnd);
            CrosstalkAnalysisOptions options = (_crosstalkAnalysisOptions ?? BuildCrosstalkAnalysisOptions()) with
            {
                UserMask = mask
            };
            CrosstalkCalculationResult updated = CrosstalkDataProcessor.Recalculate(
                _crosstalkCalculation, options);
            ApplyCrosstalkAnalysisToDashboard(updated, options);
            _configuration.CrosstalkMaskCoordinates = BuildCoordinateSummary(mask);
            AppendLog($"主界面热图已添加掩膜区域：{e}。");
        }
        catch (Exception ex)
        {
            ShowCrosstalkError(ex);
        }
    }

    private void Runner_ProgressChanged(object? sender, TestRunProgress progress) =>
        OnUi(() => labelProgress.Text = progress.Message);

    private CrosstalkAnalysisOptions BuildCrosstalkAnalysisOptions(bool[,]? userMask = null)
    {
        // 配置保存的是原始比例；界面/配置文件默认 0.03 即 3%。
        if (userMask is null && !string.IsNullOrWhiteSpace(_configuration.CrosstalkMaskCoordinates))
            userMask = CrosstalkMaskStore.ParseCoordinateRanges(
                _configuration.CrosstalkMaskCoordinates,
                CrosstalkDataProcessor.HeatmapRows,
                CrosstalkDataProcessor.HeatmapColumns);
        CrosstalkAnalysisOptions options = _configuration.CreateCrosstalkAnalysisOptions(
            userMask,
            _configuration.CrosstalkMaskName);
        return options.Normalize();
    }

    private void ApplyCrosstalkOptionsToConfiguration(CrosstalkAnalysisOptions options)
    {
        CrosstalkAnalysisOptions normalized = options.Normalize();
        _configuration.CrosstalkAbnormalThresholdRatio = normalized.AbnormalThresholdRatio;
        _configuration.CrosstalkColorAxisMinimumPercent = normalized.ColorAxisMinimumPercent;
        _configuration.CrosstalkColorAxisMaximumPercent = normalized.ColorAxisMaximumPercent;
        _configuration.CrosstalkMaskName = normalized.MaskName;
        _configuration.CrosstalkMaskNote = normalized.MaskNote;
        if (normalized.UserMask is not null)
            _configuration.CrosstalkMaskCoordinates = BuildCoordinateSummary(normalized.UserMask);
        _configuration.Normalize();
    }

    private void ApplyCrosstalkAnalysisToDashboard(
        CrosstalkCalculationResult calculation,
        CrosstalkAnalysisOptions? options)
    {
        if (IsDisposed || Disposing) return;
        _crosstalkCalculation = calculation;
        _crosstalkAnalysisOptions = options ?? calculation.AnalysisOptions;
        CrosstalkAnalysisOptions effective = _crosstalkAnalysisOptions.Normalize();
        heatmapPreview.ColorMinimumPercent = effective.ColorAxisMinimumPercent;
        heatmapPreview.ColorMaximumPercent = effective.ColorAxisMaximumPercent;
        heatmapPreview.AnomalyThresholdPercent = effective.AbnormalThresholdPercent;
        SetCrosstalkThresholdControlValue(effective.AbnormalThresholdPercent);
        heatmapPreview.SetValues(calculation.ValuesForHeatmap);
        var cells = new List<(int Row, int Column)>();
        bool[,] userMask = calculation.UserMask;
        if (userMask.Length > 0)
        {
            for (int row = 0; row < userMask.GetLength(0); row++)
                for (int column = 0; column < userMask.GetLength(1); column++)
                    if (userMask[row, column]) cells.Add((row, column));
        }
        heatmapPreview.SetMaskedCells(cells);
        labelCrosstalkPreviewHint.Text =
            $"Max {FormatPercent(calculation.Maximum)}  Min {FormatPercent(calculation.Minimum)}  " +
            $"Mean {FormatPercent(calculation.Mean)}  异常 {calculation.AbnormalPointCount}";
        buttonCrosstalkOpenResult.Enabled = true;
        AppendLog($"串扰热图已更新：Max {FormatPercent(calculation.Maximum)}，" +
            $"Min {FormatPercent(calculation.Minimum)}，Mean {FormatPercent(calculation.Mean)}，" +
            $"异常点 {calculation.AbnormalPointCount}。");
    }

    private static string FormatPercent(double ratio) =>
        double.IsFinite(ratio)
            ? $"{ratio * 100d:0.###}%"
            : "—";

    private static string BuildCoordinateSummary(bool[,] mask)
    {
        // 将点集压缩为若干行矩形，便于下一次启动恢复；无法合并的点仍会以 1×1 矩形保存。
        var rectangles = new List<string>();
        int rows = mask.GetLength(0), columns = mask.GetLength(1);
        for (int row = 0; row < rows; row++)
        {
            int column = 0;
            while (column < columns)
            {
                if (!mask[row, column]) { column++; continue; }
                int start = column;
                while (column + 1 < columns && mask[row, column + 1]) column++;
                rectangles.Add($"{start + 1},{row + 1}-{column + 1},{row + 1}");
                column++;
            }
        }
        return string.Join(';', rectangles);
    }

    private void ButtonApplyProjectRepeat_Click(object? sender, EventArgs e)
    {
        int index = checkedListProjects.SelectedIndex;
        if (index < 0 || index >= _configuration.Projects.Count) return;
        _configuration.Projects[index].RepeatCount = (int)numericProjectRepeat.Value;
        ApplyConfigurationToControls();
        ReadConfigurationFromControls();
        SyncActiveNamedPlanSnapshot();
        _settingsStore.Save(_configuration);
        AppendLog($"已将项目“{_configuration.Projects[index].Name}”设置为 {(int)numericProjectRepeat.Value} 次。");
    }

    private void UpdateSelectedProjectRepeat()
    {
        int index = checkedListProjects.SelectedIndex;
        if (index >= 0 && index < _configuration.Projects.Count)
            numericProjectRepeat.Value = Math.Clamp(_configuration.Projects[index].RepeatCount, 1, 9999);
    }

    private void CheckedListProjects_SelectedIndexChanged(object? sender, EventArgs e) => UpdateSelectedProjectRepeat();

    private void BrowseMrTest_Click(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog { Filter = "MRTEST 程序|MRTest.exe|可执行文件|*.exe|所有文件|*.*", FileName = textBoxMrTest.Text };
        if (dialog.ShowDialog(this) == DialogResult.OK) textBoxMrTest.Text = dialog.FileName;
    }

    private void BrowseExport_Click(object? sender, EventArgs e) => BrowseFolder(textBoxExport);
    private void BrowseRecipe_Click(object? sender, EventArgs e) => BrowseFolder(textBoxRecipeDir);
    private void BrowseImage_Click(object? sender, EventArgs e) => BrowseFolder(textBoxImageDir);
    private void BrowseOutput_Click(object? sender, EventArgs e) => BrowseFolder(textBoxOutputDir);
    private void BrowseFolder(TextBox target)
    {
        using var dialog = new FolderBrowserDialog { SelectedPath = target.Text };
        if (dialog.ShowDialog(this) == DialogResult.OK) target.Text = dialog.SelectedPath;
    }

    private void Server_ClientConnected(object? sender, string endpoint) => OnUi(() =>
    {
        labelConnectionState.Text = $"客户端已连接：{endpoint}";
        UpdateManualButtonState();
        AppendLog($"TCP 客户端已连接：{endpoint}");
    });
    private void Server_MessageSent(object? sender, string message) => OnUi(() => AppendLog($"发送：{message}"));
    private void Server_MessageReceived(object? sender, string message) => OnUi(() => AppendLog($"收到：{message}"));
    private void Server_ConnectionClosed(object? sender, string reason) => OnUi(() =>
    {
        labelConnectionState.Text = _server.IsListening ? "监听中，等待客户端" : "未监听";
        UpdateManualButtonState();
        AppendLog($"TCP 客户端断开：{reason}");
    });
    private void Server_ProtocolError(object? sender, Exception ex) => OnUi(() => AppendLog($"协议错误：{ex.Message}"));

    private void Monitor_DialogDetected(object? sender, MrTestDialogDetectedEventArgs dialog)
    {
        AppendLog($"发现 MRTEST 弹窗：0x{dialog.Handle.ToInt64():X}（{dialog.Title}）");

        // 一键测试进行中，固定图卡协调器是唯一消费者；手动报文期间没有
        // 固定图卡队列，由这里按当前自动确认设置处理弹窗。正常空闲状态
        // 监视器已经停止，不会继续操作 MRTEST 窗口。
        if (_closing || _planRunning || _runTask is { IsCompleted: false }) return;
        if (!_manualSendInProgress || !_configuration.AutoConfirmPopups) return;
        lock (_manualDialogGate)
        {
            if (!_manualDialogConfirming.Add(dialog.Handle)) return;
        }
        _ = ConfirmManualDialogAsync(dialog);
    }

    private async Task ConfirmManualDialogAsync(MrTestDialogDetectedEventArgs dialog)
    {
        try
        {
            await _monitor.ConfirmOkAsync(dialog.Handle).ConfigureAwait(false);
            AppendLog($"手动报文期间已自动确认 MRTEST 弹窗：0x{dialog.Handle.ToInt64():X}");
        }
        catch (Exception ex)
        {
            AppendLog($"手动报文期间自动确认 MRTEST 弹窗失败：{ex.Message}");
        }
        finally
        {
            lock (_manualDialogGate) _manualDialogConfirming.Remove(dialog.Handle);
        }
    }

    private void DashboardForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (IsInDesigner()) return;
        if (_closing) return;
        _closing = true;
        try
        {
            PrepareNamedPlansForSave(); _settingsStore.Save(_configuration);
            _runner?.Cancel();
            try { _runTask?.GetAwaiter().GetResult(); } catch { }
            _monitor.StopAsync().GetAwaiter().GetResult();
            _server.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _projector.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _runner?.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _monitor.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        catch (Exception ex) { AppendLog($"关闭时释放资源失败：{ex.Message}"); }
    }

    private void SetRunningUi(bool running)
    {
        _planRunning = running;
        buttonStart.Enabled = !running && !_manualSendInProgress;
        buttonStop.Enabled = running;
        buttonRecipeManager.Enabled = !running;
        buttonDataDisplayRules.Enabled = !running;
        buttonListen.Enabled = !running;
        buttonCrosstalkAnalyze.Enabled = !running && !_closing;
        buttonApplyCrosstalkThreshold.Enabled = !running && !_closing;
        UpdateNamedPlanUiState();
        UpdateManualButtonState();
        UpdateCrosstalkViewState();
        labelProgress.Text = running ? "正在执行..." : "等待开始";
    }

    private void ResetCrosstalkResultHistory()
    {
        _crosstalkResultHistory.Clear();
        comboCrosstalkResults.Items.Clear();
        _lastCrosstalkResult = null;
        _crosstalkCalculation = null;
        _crosstalkAnalysisOptions = null;
        heatmapPreview.SetValues(null);
        heatmapPreview.ClearMask();
        labelCrosstalkPreviewHint.Text = "支持掩膜、阈值和历史结果";
        UpdateCrosstalkViewState();
    }

    private void UpdateCrosstalkViewState()
    {
        if (IsDisposed || Disposing) return;
        bool hasResults = _crosstalkResultHistory.Count > 0;
        comboCrosstalkResults.Enabled = !_planRunning && hasResults;
        buttonViewCrosstalk.Enabled = !_planRunning &&
            (_lastCrosstalkResult?.CrosstalkCalculation is not null ||
             _lastCrosstalkResult?.HeatmapPath is string path && File.Exists(path));
        buttonCrosstalkOpenResult.Enabled = !_planRunning &&
            (_crosstalkCalculation is not null ||
             _lastCrosstalkResult?.HeatmapPath is string heatmap && File.Exists(heatmap));
    }

    private bool TryLoadCrosstalkPreviewImage(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        if (!heatmapPreview.TryLoadImage(path, out string? error))
        {
            AppendLog($"主界面串扰热图预览读取失败：{error}");
            return false;
        }
        labelCrosstalkPreviewHint.Text = "已加载串扰热图；点击“打开热图”可查看详细结果";
        buttonCrosstalkOpenResult.Enabled = true;
        return true;
    }

    private void ShowCrosstalkError(Exception exception)
    {
        AppendLog($"串扰分析失败：{exception.Message}");
        if (!_closing)
            MessageBox.Show(this, exception.Message, "串扰分析", MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
    }

    private static string BuildCrosstalkResultDisplay(
        DataProcessing.TestDataProcessingResult result,
        int ordinal)
    {
        string folder = string.IsNullOrWhiteSpace(result.OutputDirectory)
            ? "未指定输出目录"
            : Path.GetFileName(result.OutputDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(folder)) folder = result.OutputDirectory ?? "未指定输出目录";
        return $"第 {ordinal} 次：{folder}";
    }

    private sealed class CrosstalkResultEntry
    {
        public CrosstalkResultEntry(DataProcessing.TestDataProcessingResult result, string displayText)
        {
            Result = result;
            DisplayText = displayText;
        }

        public DataProcessing.TestDataProcessingResult Result { get; }
        public string DisplayText { get; }
        public override string ToString() => DisplayText;
    }

    private void UpdateManualButtonState()
    {
        if (IsDisposed || Disposing) return;
        if (!_planRunning)
            buttonStart.Enabled = !_manualSendInProgress && !_closing;
        buttonSendManual.Enabled = !_manualSendInProgress && !_planRunning && !_closing &&
            _server.IsListening && _server.IsConnected;
    }

    private void OnUi(Action action)
    {
        if (IsDisposed || Disposing) return;
        try { if (InvokeRequired) BeginInvoke(action); else action(); } catch (InvalidOperationException) { }
    }

    private void AppendLog(string message)
    {
        OnUi(() =>
        {
            string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
            bool cleaned = _logLines.Add(line);
            if (cleaned)
                textBoxLog.Lines = _logLines.Lines.ToArray();
            else
                textBoxLog.AppendText(line + Environment.NewLine);
            textBoxLog.SelectionStart = textBoxLog.TextLength; textBoxLog.ScrollToCaret();
        });
    }

    private static bool TryResolveAddress(string text, out IPAddress? address, out string error)
    {
        if (IPAddress.TryParse(text, out address)) { error = string.Empty; return true; }
        try
        {
            address = Dns.GetHostAddresses(text).FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
            if (address is not null) { error = string.Empty; return true; }
        }
        catch (Exception ex) { error = $"监听地址解析失败：{ex.Message}"; return false; }
        error = "请输入有效的 IPv4 地址或主机名。"; return false;
    }

    private bool IsInDesigner() => LicenseManager.UsageMode == LicenseUsageMode.Designtime || DesignMode;

    private void textBoxLog_TextChanged(object sender, EventArgs e)
    {

    }

    private void resultSplit_Panel1_Paint(object sender, PaintEventArgs e)
    {

    }

    private void rootLayout_Paint(object sender, PaintEventArgs e)
    {

    }

    private void textBoxManualCommand_TextChanged(object sender, EventArgs e)
    {

    }
}
