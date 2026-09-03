using System.ComponentModel;

namespace AutoTestClient;

/// <summary>
/// 命名测试计划的输入窗口。使用标准 Designer 控件，保证计划名称输入
/// 对话框也可以在 Visual Studio 中调整，而不是由代码临时拼接布局。
/// </summary>
public partial class NamedTestPlanNameForm : Form
{
    private readonly bool _isDesignTime;

    /// <summary>设计器/调用方读取的计划名称。</summary>
    public string PlanName => textBoxName?.Text.Trim() ?? string.Empty;

    /// <summary>Visual Studio WinForms Designer 使用的无参构造函数。</summary>
    public NamedTestPlanNameForm()
        : this(string.Empty, designTime: true)
    {
    }

    public NamedTestPlanNameForm(string initialName)
        : this(initialName, designTime: false)
    {
    }

    private NamedTestPlanNameForm(string initialName, bool designTime)
    {
        _isDesignTime = designTime ||
            LicenseManager.UsageMode == LicenseUsageMode.Designtime;
        InitializeComponent();
        textBoxName.Text = initialName ?? string.Empty;
        if (!_isDesignTime)
            Shown += (_, _) =>
            {
                textBoxName.SelectAll();
                textBoxName.Focus();
            };
    }

    private void ButtonOk_Click(object? sender, EventArgs e)
    {
        if (_isDesignTime) return;
        if (string.IsNullOrWhiteSpace(textBoxName.Text))
        {
            MessageBox.Show(this, "请输入计划名称。", "另存测试计划",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            textBoxName.Focus();
            return;
        }
        DialogResult = DialogResult.OK;
        Close();
    }

    private void ButtonCancel_Click(object? sender, EventArgs e)
    {
        if (_isDesignTime) return;
        DialogResult = DialogResult.Cancel;
        Close();
    }
}
