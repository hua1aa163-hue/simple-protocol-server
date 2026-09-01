using System.ComponentModel;
using System.Windows.Forms;

namespace AutoTestClient.Controls;

/// <summary>
/// 预留给设计器使用的可缩放文本框。
/// </summary>
/// <remarks>
/// 当前仅保留独立类型，便于后续在主界面统一替换标准 TextBox。
/// </remarks>
public class DesignerResizableTextBox : TextBox
{
    /// <summary>
    /// 初始化可缩放文本框。
    /// </summary>
    public DesignerResizableTextBox()
    {
        AutoSize = false;
    }

    /// <summary>
    /// 预留给后续接入的设计器属性。
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsDesignerResizable => true;
}
