namespace AutoTestClient.Protocol;

/// <summary>
/// 客户端界面可以选择的标准命令。
/// <para>
/// 数值顺序与主界面下拉框建议的顺序一致。协议层不依赖 WinForms，
/// 因此命令生成和自动化测试可以在没有界面的情况下单独验证。
/// </para>
/// </summary>
public enum CommandType
{
    /// <summary>停止设备当前动作。</summary>
    Stop = 0,

    /// <summary>切换到指定配方。</summary>
    SwitchRecipe = 1,

    /// <summary>保存指定配方。</summary>
    SaveRecipe = 2,

    /// <summary>获取指定 VID 对应的焦距。</summary>
    GetFocalLength = 3,

    /// <summary>启动一次自动测量。</summary>
    SingleAutomatic = 4,

    /// <summary>启动一次手动测量（默认命令）。</summary>
    SingleManual = 5,

    /// <summary>启动连续自动测量。</summary>
    ContinuousAutomatic = 6,

    /// <summary>启动连续手动测量。</summary>
    ContinuousManual = 7
}
