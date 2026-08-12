// 命令枚举集中定义界面可选项，避免在多个地方使用难懂的数字 0～7。
namespace SimpleProtocolServer.Protocol;

/// <summary>界面支持的 8 类命令，顺序与下拉框一致。</summary>
internal enum CommandType
{
    /// <summary>停止设备当前动作。</summary>
    Stop,
    /// <summary>切换到指定配方。</summary>
    SwitchRecipe,
    /// <summary>保存指定配方。</summary>
    SaveRecipe,
    /// <summary>获取焦距 FF。</summary>
    GetFocalLength,
    /// <summary>启动一次自动测量。</summary>
    SingleAutomatic,
    /// <summary>启动一次手动测量。</summary>
    SingleManual,
    /// <summary>启动连续自动测量。</summary>
    ContinuousAutomatic,
    /// <summary>启动连续手动测量。</summary>
    ContinuousManual
}
