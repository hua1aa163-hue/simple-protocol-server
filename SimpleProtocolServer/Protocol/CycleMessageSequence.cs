// 这个小类只管理“下一条应该取哪条”，不负责计时或网络发送。
namespace SimpleProtocolServer.Protocol;

/// <summary>按列表顺序取出报文，到达末尾后自动回到第一条。</summary>
internal sealed class CycleMessageSequence
{
    // 使用数组保存启动定时发送时的快照，运行期间界面列表不会影响当前轮次。
    private string[] _messages = [];
    // 下次 TryGetNext 要读取的数组下标；数组下标从 0 开始。
    private int _nextIndex;

    /// <summary>快照中共有多少条报文。</summary>
    public int Count => _messages.Length;

    /// <summary>用新的报文集合重置序列，并从第一条重新开始。</summary>
    public void Reset(IEnumerable<string> messages)
    {
        _messages = messages.ToArray();
        _nextIndex = 0;
    }

    /// <summary>
    /// 取得下一条报文及其从 1 开始的显示位置；列表为空时返回 false。
    /// </summary>
    public bool TryGetNext(out string message, out int position)
    {
        if (_messages.Length == 0)
        {
            message = string.Empty;
            position = 0;
            return false;
        }

        position = _nextIndex + 1;
        message = _messages[_nextIndex];
        // % 是取余数：最后一条之后余数变回 0，实现循环。
        _nextIndex = (_nextIndex + 1) % _messages.Length;
        return true;
    }
}
