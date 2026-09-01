using System.Drawing;
using System.Windows.Forms;

namespace AutoTestClient.Controls;

/// <summary>
/// 只有单击项目左侧复选框区域才改变勾选状态；单击文字只选择项目。
/// 键盘操作仍沿用标准 CheckedListBox 行为。
/// </summary>
public class CheckboxOnlyCheckedListBox : CheckedListBox
{
    public CheckboxOnlyCheckedListBox()
    {
        // 鼠标切换由 OnMouseDown 精确处理，禁止整行的 CheckOnClick 行为。
        CheckOnClick = false;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            int index = IndexFromPoint(e.Location);
            if (index != ListBox.NoMatches)
            {
                Focus();
                SelectedIndex = index;
                if (IsCheckboxHit(index, e.Location))
                    SetItemChecked(index, !GetItemChecked(index));

                // 不交给基类处理该项目的鼠标点击，否则文字区域在已选中时
                // 仍可能触发标准 CheckedListBox 的勾选切换。
                return;
            }
        }

        base.OnMouseDown(e);
    }

    protected virtual bool IsCheckboxHit(int itemIndex, Point location)
    {
        Rectangle item = GetItemRectangle(itemIndex);
        int hitWidth = Math.Max(18, SystemInformation.MenuCheckSize.Width + 6);
        var checkboxArea = new Rectangle(item.Left, item.Top, hitWidth, item.Height);
        return checkboxArea.Contains(location);
    }
}
