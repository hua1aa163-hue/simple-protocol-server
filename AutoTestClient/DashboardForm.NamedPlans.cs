using System.Text.Json;
using System.Text.Json.Serialization;
using AutoTestClient.Models;

namespace AutoTestClient;

/// <summary>
/// 主界面的命名测试计划维护。该文件只负责快照、切换和持久化，
/// 不参与测试执行，因而可在不改变 TestPlanRunner 的情况下扩展计划管理。
/// </summary>
public partial class DashboardForm
{
    private bool _namedPlanUiUpdating;
    private bool _namedPlansInitialized;
    private string _activeNamedPlanName = string.Empty;
    private string _namedPlanBaselineSignature = string.Empty;

    private static readonly JsonSerializerOptions NamedPlanSignatureOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// 在主窗体完成旧配置加载后初始化计划下拉框。
    /// 旧配置没有 SavedPlans 时，以当前编辑内容建立“默认计划”，
    /// 不会用空快照覆盖用户已经保存的项目和图卡。
    /// </summary>
    private void InitializeNamedPlans()
    {
        if (IsInDesigner() || comboTestPlans is null) return;

        _configuration.Normalize();
        if (_configuration.SavedPlans.Count == 0)
        {
            NamedTestPlan initial = NamedTestPlan.Capture(
                _configuration, NamedTestPlan.DefaultName);
            _configuration.SavedPlans.Add(initial);
            _configuration.ActivePlanName = initial.Name;
        }

        NamedTestPlan? active = FindNamedPlan(_configuration.ActivePlanName);
        if (active is null)
        {
            // ActivePlanName 为空通常表示旧配置或尚未命名保存的编辑内容。
            // 让当前内容先成为活动计划，避免首次启动时突然切换到另一套项目。
            active = _configuration.SavedPlans[0];
            _configuration.ActivePlanName = active.Name;
        }

        _activeNamedPlanName = active.Name;
        RefreshNamedPlanCombo(selectActive: true);
        // 基线取活动计划的已保存快照，而不是无条件取当前控件值。
        // 这样用户上次退出前尚未点击“保存”的编辑内容在下次切换时仍会
        // 触发保存/放弃询问，不会被另一套计划静默覆盖。
        _namedPlanBaselineSignature = BuildNamedPlanSignature(active);
        _namedPlansInitialized = true;
        UpdateNamedPlanUiState();
    }

    private NamedTestPlan? FindNamedPlan(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        return _configuration.SavedPlans.FirstOrDefault(plan =>
            string.Equals(plan.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private string BuildNamedPlanSignature(TestPlanConfiguration configuration)
    {
        NamedTestPlan snapshot = NamedTestPlan.Capture(configuration, "签名");
        return BuildNamedPlanSignature(snapshot);
    }

    private static string BuildNamedPlanSignature(NamedTestPlan snapshot)
    {
        snapshot = snapshot.Clone();
        // 名称不属于“内容是否修改”的判断；另存为后即使名称不同，
        // 其项目/参数快照仍应被视为同一份未修改内容。
        snapshot.Name = string.Empty;
        return JsonSerializer.Serialize(snapshot, NamedPlanSignatureOptions);
    }

    private bool HasUnsavedNamedPlanChanges()
    {
        if (!_namedPlansInitialized) return false;
        try
        {
            ReadConfigurationFromControls();
            return !string.Equals(
                BuildNamedPlanSignature(_configuration),
                _namedPlanBaselineSignature,
                StringComparison.Ordinal);
        }
        catch (Exception ex)
        {
            // 无法生成签名时按“有修改”处理，切换前必须让用户明确选择，
            // 避免因异常而静默丢失当前编辑内容。
            AppendLog($"检查命名计划修改状态失败：{ex.Message}");
            return true;
        }
    }

    private void RefreshNamedPlanCombo(bool selectActive)
    {
        if (comboTestPlans is null) return;

        _namedPlanUiUpdating = true;
        try
        {
            comboTestPlans.BeginUpdate();
            comboTestPlans.Items.Clear();
            foreach (NamedTestPlan plan in _configuration.SavedPlans)
                comboTestPlans.Items.Add(plan.Name);

            if (selectActive && comboTestPlans.Items.Count > 0)
            {
                int selected = comboTestPlans.Items.IndexOf(_activeNamedPlanName);
                comboTestPlans.SelectedIndex = selected >= 0 ? selected : 0;
            }
        }
        finally
        {
            comboTestPlans.EndUpdate();
            _namedPlanUiUpdating = false;
        }
    }

    private void ComboTestPlans_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_namedPlanUiUpdating || !_namedPlansInitialized || _closing ||
            comboTestPlans.SelectedItem is not string selectedName ||
            string.IsNullOrWhiteSpace(selectedName) ||
            string.Equals(selectedName, _activeNamedPlanName, StringComparison.OrdinalIgnoreCase))
            return;

        string previousName = _activeNamedPlanName;
        bool dirty = HasUnsavedNamedPlanChanges();
        if (dirty)
        {
            DialogResult decision = MessageBox.Show(
                this,
                $"当前计划“{previousName}”有未保存的修改。\r\n是否先保存再切换？",
                "切换测试计划",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button1);
            if (decision == DialogResult.Cancel)
            {
                RestoreNamedPlanSelection(previousName);
                return;
            }
            if (decision == DialogResult.Yes)
            {
                // 只更新旧计划快照，不触发下拉框事件；随后再应用目标计划。
                SaveNamedPlanCore(previousName, refreshCombo: false, writeSettings: false);
            }
        }

        NamedTestPlan? target = FindNamedPlan(selectedName);
        if (target is null)
        {
            RestoreNamedPlanSelection(previousName);
            return;
        }

        target.ApplyTo(_configuration);
        _configuration.ActivePlanName = target.Name;
        _activeNamedPlanName = target.Name;
        ApplyConfigurationToControls();
        // 结果属于上一套计划的实际测量批次；切换计划时清空，避免把旧
        // 指标/热图误认为新计划的结果。历史文件仍保留在磁盘中，可用路径
        // 和串扰分析器再次打开。
        ResetResultDisplayHistory();
        ResetCrosstalkResultHistory();
        _namedPlanBaselineSignature = BuildNamedPlanSignature(target);
        RefreshNamedPlanCombo(selectActive: true);
        TrySaveSettingsAfterPlanChange($"已切换测试计划：{target.Name}");
    }

    private void RestoreNamedPlanSelection(string name)
    {
        _namedPlanUiUpdating = true;
        try
        {
            int index = comboTestPlans.Items.IndexOf(name);
            if (index >= 0) comboTestPlans.SelectedIndex = index;
        }
        finally
        {
            _namedPlanUiUpdating = false;
        }
    }

    private void ButtonSavePlan_Click(object? sender, EventArgs e)
    {
        if (_closing || _planRunning) return;
        ReadConfigurationFromControls();
        string name = string.IsNullOrWhiteSpace(_activeNamedPlanName)
            ? NamedTestPlan.DefaultName : _activeNamedPlanName;
        SaveNamedPlanCore(name, refreshCombo: true, writeSettings: true);
    }

    private void ButtonSavePlanAs_Click(object? sender, EventArgs e)
    {
        if (_closing || _planRunning) return;

        string suggestion = string.IsNullOrWhiteSpace(_activeNamedPlanName)
            ? NamedTestPlan.DefaultName : _activeNamedPlanName + " 副本";
        string? name = PromptNamedPlanName(suggestion);
        if (string.IsNullOrWhiteSpace(name)) return;

        name = name.Trim();
        NamedTestPlan? existing = FindNamedPlan(name);
        if (existing is not null)
        {
            DialogResult overwrite = MessageBox.Show(
                this,
                $"计划“{name}”已经存在，是否覆盖？",
                "另存测试计划",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (overwrite != DialogResult.Yes) return;
        }

        ReadConfigurationFromControls();
        SaveNamedPlanCore(name, refreshCombo: true, writeSettings: true);
    }

    private void ButtonDeletePlan_Click(object? sender, EventArgs e)
    {
        if (_closing || _planRunning || !_namedPlansInitialized) return;

        string name = comboTestPlans.SelectedItem as string ?? _activeNamedPlanName;
        NamedTestPlan? target = FindNamedPlan(name);
        if (target is null) return;

        DialogResult confirm = MessageBox.Show(
            this,
            $"确定删除测试计划“{target.Name}”吗？\r\n该计划中的项目和图卡绑定也会被删除。",
            "删除测试计划",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (confirm != DialogResult.Yes) return;

        ReadConfigurationFromControls();
        int removedIndex = _configuration.SavedPlans.IndexOf(target);
        _configuration.SavedPlans.Remove(target);
        if (_configuration.SavedPlans.Count == 0)
        {
            NamedTestPlan fallback = NamedTestPlan.Capture(
                _configuration, NamedTestPlan.DefaultName);
            _configuration.SavedPlans.Add(fallback);
        }

        int nextIndex = Math.Clamp(removedIndex, 0, _configuration.SavedPlans.Count - 1);
        NamedTestPlan next = _configuration.SavedPlans[nextIndex];
        next.ApplyTo(_configuration);
        _configuration.ActivePlanName = next.Name;
        _activeNamedPlanName = next.Name;
        ApplyConfigurationToControls();
        ResetResultDisplayHistory();
        ResetCrosstalkResultHistory();
        _namedPlanBaselineSignature = BuildNamedPlanSignature(next);
        RefreshNamedPlanCombo(selectActive: true);
        TrySaveSettingsAfterPlanChange($"已删除测试计划：{target.Name}；当前计划：{next.Name}");
    }

    /// <summary>保存当前快照；调用方可控制是否刷新下拉框和写文件。</summary>
    private void SaveNamedPlanCore(string name, bool refreshCombo, bool writeSettings)
    {
        name = (name ?? string.Empty).Trim();
        if (name.Length == 0) name = NamedTestPlan.DefaultName;
        if (name.Length > 80) name = name[..80];

        _configuration.Normalize();
        NamedTestPlan snapshot = NamedTestPlan.Capture(_configuration, name);
        int existingIndex = _configuration.SavedPlans.FindIndex(plan =>
            string.Equals(plan.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existingIndex >= 0)
            _configuration.SavedPlans[existingIndex] = snapshot;
        else
            _configuration.SavedPlans.Add(snapshot);

        _configuration.ActivePlanName = snapshot.Name;
        _activeNamedPlanName = snapshot.Name;
        _namedPlanBaselineSignature = BuildNamedPlanSignature(snapshot);
        _namedPlansInitialized = true;
        if (refreshCombo) RefreshNamedPlanCombo(selectActive: true);
        if (writeSettings) TrySaveSettingsAfterPlanChange($"已保存测试计划：{snapshot.Name}");
    }

    private void TrySaveSettingsAfterPlanChange(string logMessage)
    {
        try
        {
            _configuration.Normalize();
            _settingsStore.Save(_configuration);
            AppendLog(logMessage);
        }
        catch (Exception ex)
        {
            AppendLog($"保存测试计划失败：{ex.Message}");
        }
        UpdateNamedPlanUiState();
    }

    private void PrepareNamedPlansForSave()
    {
        if (!_namedPlansInitialized) return;
        // 当前编辑值仍然写回主配置，即使用户没有点击“保存”命名计划，
        // 这样符合主窗口“退出时恢复最后编辑值”的既有约定。
        ReadConfigurationFromControls();
        _configuration.ActivePlanName = _activeNamedPlanName;
        SyncActiveNamedPlanSnapshot();
    }

    /// <summary>
    /// 编辑器（项目/图卡或数据展示规则）返回后，当前配置已经是一次明确的
    /// 用户保存操作；同步活动计划快照，确保重启时不会出现“顶层配置已更新、
    /// 下拉计划仍是旧内容”的分裂状态。普通控件的临时编辑仍可在切换时由
    /// 基线比较提示用户保存或放弃。
    /// </summary>
    private void SyncActiveNamedPlanSnapshot()
    {
        if (!_namedPlansInitialized || string.IsNullOrWhiteSpace(_activeNamedPlanName)) return;
        try
        {
            _configuration.Normalize();
            NamedTestPlan snapshot = NamedTestPlan.Capture(
                _configuration, _activeNamedPlanName);
            int index = _configuration.SavedPlans.FindIndex(plan =>
                string.Equals(plan.Name, snapshot.Name, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
                _configuration.SavedPlans[index] = snapshot;
            else
                _configuration.SavedPlans.Add(snapshot);

            _configuration.ActivePlanName = snapshot.Name;
            _activeNamedPlanName = snapshot.Name;
            _namedPlanBaselineSignature = BuildNamedPlanSignature(snapshot);
        }
        catch (Exception ex)
        {
            // 快照是辅助持久化；即使某个历史字段无法序列化，也不应阻断
            // 当前测试/设置操作，主配置仍由调用方继续保存。
            AppendLog($"同步命名测试计划失败：{ex.Message}");
        }
    }

    private void UpdateNamedPlanUiState()
    {
        if (comboTestPlans is null || buttonSavePlan is null ||
            buttonSavePlanAs is null || buttonDeletePlan is null) return;
        bool enabled = !_closing && !_planRunning && _namedPlansInitialized;
        comboTestPlans.Enabled = enabled && comboTestPlans.Items.Count > 0;
        buttonSavePlan.Enabled = enabled;
        buttonSavePlanAs.Enabled = enabled;
        buttonDeletePlan.Enabled = enabled && comboTestPlans.Items.Count > 0;
    }

    /// <summary>
    /// 使用独立的 Designer 窗体输入名称，避免动态创建一套无法在设计器中
    /// 调整的文本框/按钮。
    /// </summary>
    private string? PromptNamedPlanName(string initialValue)
    {
        using var dialog = new NamedTestPlanNameForm(initialValue);
        return dialog.ShowDialog(this) == DialogResult.OK
            ? dialog.PlanName : null;
    }
}
