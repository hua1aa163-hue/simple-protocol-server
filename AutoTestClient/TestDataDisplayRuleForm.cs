using System.ComponentModel;
using AutoTestClient.Models;
using AutoTestClient.Settings;

namespace AutoTestClient;

/// <summary>
/// 测试数据展示规则编辑器。
///
/// 该窗口只负责维护“导出 Excel 字段 → 报告输出列”的配置，不参与测量
/// 线程或 MRTEST 通讯。用户可以直接调整工作表、单元格范围、聚合方式和
/// 输出列，后续新增展示项无需修改测量流程代码。
/// </summary>
public partial class TestDataDisplayRuleForm : Form
{
    private readonly SettingsStore _store;
    private readonly bool _isDesignTime;
    private readonly List<TestDataDisplayRule> _rules = new();
    private bool _cancelRequested;
    private bool _saved;
    private bool _updatingGrid;

    /// <summary>编辑中的配置副本；点击保存或关闭窗口后由调用方读取。</summary>
    public TestPlanConfiguration Configuration { get; private set; }

    /// <summary>
    /// Visual Studio WinForms Designer 使用的无参构造函数。此路径不读取
    /// 用户配置、不扫描文件夹，因此设计器可以在没有 MRTEST 的环境打开。
    /// </summary>
    public TestDataDisplayRuleForm()
        : this(new TestPlanConfiguration(), new SettingsStore(), designTime: true)
    {
    }

    public TestDataDisplayRuleForm(TestPlanConfiguration configuration, SettingsStore store)
        : this(configuration, store, designTime: false)
    {
    }

    private TestDataDisplayRuleForm(
        TestPlanConfiguration configuration,
        SettingsStore store,
        bool designTime)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(store);

        Configuration = configuration.Clone();
        Configuration.Normalize();
        _store = store;
        _isDesignTime = designTime ||
            LicenseManager.UsageMode == LicenseUsageMode.Designtime;

        InitializeComponent();
        if (_isDesignTime)
        {
            // 给设计器一个有内容的预览，便于用户直接拖拽列宽、调整字体和
            // 按钮位置；这些示例只存在于设计器实例，不会写入配置文件。
            LoadDesignerPreviewRules();
            labelStatus.Text = "设计器预览：可直接调整表格列、按钮和窗口布局。";
        }
        else
        {
            Load += TestDataDisplayRuleForm_Load;
            FormClosing += TestDataDisplayRuleForm_FormClosing;
        }

        BindRules();
    }

    private void TestDataDisplayRuleForm_Load(object? sender, EventArgs e)
    {
        BindRules();
    }

    private void LoadDesignerPreviewRules()
    {
        if (Configuration.DisplayRules.Count > 0) return;

        Configuration.DisplayRules = new List<TestDataDisplayRule>
        {
            new()
            {
                Enabled = true,
                Order = 1,
                ProjectKind = TestProjectKind.BrightnessUniformity,
                Name = "平均亮度",
                Worksheet = "Brightness",
                NameCellOrLabel = "平均亮度",
                DataCellOrRange = "C3:C11",
                CellOrRange = "C3:C11",
                NameTemplate = "{ProjectName}_平均亮度",
                Aggregation = TestDataAggregation.Average,
                OutputColumn = "平均亮度"
            },
            new()
            {
                Enabled = true,
                Order = 2,
                ProjectKind = TestProjectKind.Fov,
                Name = "水平FOV",
                Worksheet = "FOV",
                NameCellOrLabel = "水平FOV",
                DataCellOrRange = "B4",
                CellOrRange = "B4",
                NameTemplate = "{ProjectName}_水平FOV",
                Aggregation = TestDataAggregation.First,
                OutputColumn = "水平FOV"
            }
        };
        Configuration.Normalize();
    }

    private void BindRules(int preferredIndex = -1)
    {
        if (gridRules is null) return;

        ReadGridIntoRules();
        _updatingGrid = true;
        try
        {
            gridRules.Rows.Clear();
            _rules.Clear();
            foreach (TestDataDisplayRule rule in Configuration.DisplayRules
                         .Where(rule => rule is not null)
                         .OrderBy(rule => rule.Order)
                         .ThenBy(rule => rule.Name, StringComparer.CurrentCulture))
            {
                rule.Normalize(_rules.Count + 1);
                _rules.Add(rule);
                int rowIndex = gridRules.Rows.Add(
                    rule.Enabled,
                    rule.Order,
                    TestDataDisplayRule.GetProjectKindDisplayName(rule.ProjectKind),
                    rule.Name,
                    rule.Worksheet,
                    rule.NameCellOrLabel,
                    rule.DataCellOrRange,
                    rule.NameTemplate,
                    TestDataDisplayRule.GetAggregationDisplayName(rule.Aggregation),
                    rule.OutputColumn);
                gridRules.Rows[rowIndex].Tag = rule;
            }

            if (gridRules.Rows.Count == 0)
            {
                labelStatus.Text = "暂无规则。点击“新增规则”开始配置。";
                return;
            }

            int selected = preferredIndex < 0
                ? 0
                : Math.Clamp(preferredIndex, 0, gridRules.Rows.Count - 1);
            gridRules.CurrentCell = gridRules.Rows[selected].Cells[
                Math.Min(3, gridRules.Columns.Count - 1)];
            gridRules.Rows[selected].Selected = true;
            labelStatus.Text = $"共 {_rules.Count} 条规则。单元格可直接编辑。";
        }
        finally
        {
            _updatingGrid = false;
        }
    }

    /// <summary>
    /// DataGridView 未绑定到对象列表，显式复制每个单元格，避免 List&lt;T&gt;
    /// 在设计器/运行时没有通知时丢失最后一次输入。
    /// </summary>
    private void ReadGridIntoRules()
    {
        if (_updatingGrid || gridRules is null) return;

        foreach (DataGridViewRow row in gridRules.Rows)
        {
            if (row.IsNewRow || row.Tag is not TestDataDisplayRule rule) continue;

            rule.Enabled = ReadBool(row, "ruleEnabledColumn", rule.Enabled);
            rule.Order = ReadInt(row, "ruleOrderColumn", rule.Order);
            rule.ProjectKind = TestDataDisplayRule.ParseProjectKind(
                ReadValue(row, "ruleProjectKindColumn"));
            rule.Name = ReadText(row, "ruleNameColumn", rule.Name);
            rule.Worksheet = ReadText(row, "ruleWorksheetColumn", rule.Worksheet);
            rule.NameCellOrLabel = ReadText(row, "ruleNameCellColumn", rule.NameCellOrLabel);
            rule.DataCellOrRange = ReadText(row, "ruleCellRangeColumn",
                string.IsNullOrWhiteSpace(rule.DataCellOrRange)
                    ? rule.CellOrRange : rule.DataCellOrRange);
            // Keep the legacy field synchronized for consumers compiled against
            // the v0.1.1 model/property name.
            rule.CellOrRange = rule.DataCellOrRange;
            rule.NameTemplate = ReadText(row, "ruleTemplateColumn", rule.NameTemplate);
            rule.Aggregation = TestDataDisplayRule.ParseAggregation(
                ReadValue(row, "ruleAggregationColumn"));
            rule.OutputColumn = ReadText(row, "ruleOutputColumn", rule.OutputColumn);
            rule.Normalize(rule.Order <= 0 ? row.Index + 1 : rule.Order);
        }

        // The configuration list is updated by the add/delete/reorder/save
        // commands.  Do not assign _rules here: BindRules clears and rebuilds
        // that working list, and assigning the same instance would otherwise
        // erase the configuration during the rebuild.
    }

    private static object? ReadValue(DataGridViewRow row, string columnName)
    {
        if (row.DataGridView is null || !row.DataGridView.Columns.Contains(columnName))
            return null;
        return row.Cells[columnName].Value;
    }

    private static string ReadText(DataGridViewRow row, string columnName, string fallback)
        => ReadValue(row, columnName)?.ToString() ?? fallback;

    private static int ReadInt(DataGridViewRow row, string columnName, int fallback)
        => int.TryParse(ReadValue(row, columnName)?.ToString(), out int value)
            ? value
            : fallback;

    private static bool ReadBool(DataGridViewRow row, string columnName, bool fallback)
    {
        object? value = ReadValue(row, columnName);
        return value is bool boolean
            ? boolean
            : bool.TryParse(value?.ToString(), out bool parsed) ? parsed : fallback;
    }

    private TestDataDisplayRule? GetSelectedRule(out int rowIndex)
    {
        rowIndex = gridRules?.CurrentCell?.RowIndex ?? -1;
        if (rowIndex < 0 || gridRules is null || rowIndex >= gridRules.Rows.Count)
            return null;
        return gridRules.Rows[rowIndex].Tag as TestDataDisplayRule;
    }

    private void ButtonAddRule_Click(object? sender, EventArgs e)
    {
        if (_isDesignTime) return;

        ReadGridIntoRules();
        int nextOrder = _rules.Count == 0 ? 1 : _rules.Max(rule => rule.Order) + 1;
        var rule = new TestDataDisplayRule
        {
            Enabled = true,
            Order = nextOrder,
            Name = $"新规则{_rules.Count + 1}",
            ProjectKind = null,
            Worksheet = string.Empty,
            NameCellOrLabel = string.Empty,
            DataCellOrRange = "A1",
            CellOrRange = "A1",
            NameTemplate = "{ProjectName}_{RuleName}",
            Aggregation = TestDataAggregation.First,
            OutputColumn = $"输出列{_rules.Count + 1}"
        };
        Configuration.DisplayRules.Add(rule);
        BindRules(_rules.Count);
        labelStatus.Text = "已新增规则；请填写工作表、单元格/范围和输出列。";
    }

    private void ButtonDeleteRule_Click(object? sender, EventArgs e)
    {
        if (_isDesignTime) return;

        ReadGridIntoRules();
        TestDataDisplayRule? rule = GetSelectedRule(out int rowIndex);
        if (rule is null || !Configuration.DisplayRules.Contains(rule)) return;

        DialogResult answer = MessageBox.Show(
            this,
            $"确定删除展示规则“{rule.Name}”吗？",
            "删除展示规则",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (answer != DialogResult.Yes) return;

        Configuration.DisplayRules.Remove(rule);
        ReindexRules();
        BindRules(Math.Min(rowIndex, Configuration.DisplayRules.Count - 1));
        labelStatus.Text = "展示规则已删除。";
    }

    private void ButtonMoveUp_Click(object? sender, EventArgs e)
        => MoveSelectedRule(-1);

    private void ButtonMoveDown_Click(object? sender, EventArgs e)
        => MoveSelectedRule(1);

    private void MoveSelectedRule(int offset)
    {
        if (_isDesignTime) return;

        ReadGridIntoRules();
        TestDataDisplayRule? rule = GetSelectedRule(out int currentIndex);
        if (rule is null) return;

        List<TestDataDisplayRule> ordered = Configuration.DisplayRules
            .OrderBy(item => item.Order)
            .ThenBy(item => item.Name, StringComparer.CurrentCulture)
            .ToList();
        int index = ordered.IndexOf(rule);
        int target = index + offset;
        if (index < 0 || target < 0 || target >= ordered.Count) return;

        (ordered[index], ordered[target]) = (ordered[target], ordered[index]);
        for (int i = 0; i < ordered.Count; i++) ordered[i].Order = i + 1;
        Configuration.DisplayRules = ordered;
        BindRules(target);
        labelStatus.Text = "展示规则顺序已调整。";
    }

    private void ReindexRules()
    {
        int order = 1;
        foreach (TestDataDisplayRule rule in Configuration.DisplayRules
                     .OrderBy(item => item.Order)
                     .ThenBy(item => item.Name, StringComparer.CurrentCulture))
            rule.Order = order++;
    }

    private void ButtonSave_Click(object? sender, EventArgs e)
    {
        if (_isDesignTime) return;

        try
        {
            CommitGridEdits();
            ReadGridIntoRules();
            Configuration.DisplayRules = _rules;
            Configuration.Normalize();
            _store.Save(Configuration);
            _saved = true;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "保存展示规则失败",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ButtonCancel_Click(object? sender, EventArgs e)
    {
        if (_isDesignTime) return;
        _cancelRequested = true;
        DialogResult = DialogResult.Cancel;
        Close();
    }

    private void TestDataDisplayRuleForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_cancelRequested || _saved || _isDesignTime) return;

        // 与测试项目编辑窗口保持一致：点击右上角关闭时也保存最后一次
        // 修改后的退出值；明确点击“取消”才放弃编辑。
        try
        {
            CommitGridEdits();
            ReadGridIntoRules();
            Configuration.DisplayRules = _rules;
            Configuration.Normalize();
            _store.Save(Configuration);
            _saved = true;
            DialogResult = DialogResult.OK;
        }
        catch (Exception ex)
        {
            e.Cancel = true;
            MessageBox.Show(this, ex.Message, "保存展示规则失败",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// 保存/关闭按钮可能在文本框仍处于编辑状态时被点击。先结束当前
    /// DataGridView 编辑事务，确保最后一个字符已经写回 Cell，再复制到
    /// 规则对象，避免用户看到的退出值丢失。
    /// </summary>
    private void CommitGridEdits()
    {
        if (gridRules is null) return;
        if (gridRules.IsCurrentCellDirty)
            gridRules.CommitEdit(DataGridViewDataErrorContexts.Commit);
        if (gridRules.IsCurrentCellInEditMode)
            gridRules.EndEdit();
    }

    private void GridRules_CurrentCellDirtyStateChanged(object? sender, EventArgs e)
    {
        if (_updatingGrid || gridRules is null || !gridRules.IsCurrentCellDirty) return;
        if (gridRules.CurrentCell is DataGridViewCheckBoxCell or DataGridViewComboBoxCell)
            gridRules.CommitEdit(DataGridViewDataErrorContexts.Commit);
    }

    private void GridRules_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
    {
        if (_updatingGrid) return;
        ReadGridIntoRules();
        labelStatus.Text = "已修改；点击保存或关闭窗口保留退出值。";
    }

    private void GridRules_DataError(object? sender, DataGridViewDataErrorEventArgs e)
    {
        e.ThrowException = false;
        labelStatus.Text = "单元格格式无法识别，已保留原值。";
    }
}
