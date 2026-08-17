// 接口只描述“能做什么”，具体 Windows API 实现在 DesktopDisplayService 中。
// 这样窗体依赖的是清晰的小接口，而不是一堆难懂的原生函数。
namespace SimpleProtocolServer.Projection;

/// <summary>隔离 Windows 显示器拓扑操作。</summary>
internal interface IDesktopDisplayService
{
    /// <summary>切换 Windows 投影模式。</summary>
    void ApplyTopology(DisplayTopology topology);
}
