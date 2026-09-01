using System.IO;
using System.Text;

namespace AutoTestClient.Protocol;

/// <summary>
/// 从 TCP 字节流解出的字符片段中提取完整的 <c>&amp;|...|@</c> 报文。
/// </summary>
/// <remarks>
/// TCP 没有消息边界：一次读取可能只有半条报文，也可能包含多条报文。
/// 本类只处理已经由 UTF-8 解码器还原的字符；<see cref="Networking.TcpMessageServer"/>
/// 使用 <see cref="StreamReader"/> 保持跨字节读取的 UTF-8 解码状态。
/// </remarks>
public sealed class MessageFrameParser
{
    /// <summary>默认允许缓存的未完成字符数，防止异常对端耗尽内存。</summary>
    public const int DefaultMaxPendingCharacters = 1024 * 1024;

    private readonly StringBuilder _pending = new();

    /// <summary>创建使用默认 1 MB 缓存上限的解析器。</summary>
    public MessageFrameParser() : this(DefaultMaxPendingCharacters)
    {
    }

    /// <summary>创建自定义缓存上限的解析器。</summary>
    public MessageFrameParser(int maxPendingCharacters)
    {
        if (maxPendingCharacters < MessageProtocol.StartMarker.Length + MessageProtocol.EndMarker.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxPendingCharacters), "缓存上限太小，无法容纳一条最短报文。");
        }

        MaxPendingCharacters = maxPendingCharacters;
    }

    /// <summary>允许缓存的最大未完成字符数。</summary>
    public int MaxPendingCharacters { get; }

    /// <summary>当前尚未组成完整报文的字符数。</summary>
    public int PendingLength => _pending.Length;

    /// <summary>
    /// 追加一段字符并返回本次新提取出的完整报文。
    /// 返回集合是快照，调用者可以安全地在事件处理后修改它。
    /// </summary>
    public IReadOnlyList<string> Append(string? chunk)
    {
        if (!string.IsNullOrEmpty(chunk))
        {
            _pending.Append(chunk);
        }

        return ExtractPendingMessages();
    }

    /// <summary>追加字符内存片段的重载，避免调用方为切片创建临时字符串。</summary>
    public IReadOnlyList<string> Append(ReadOnlySpan<char> chunk)
    {
        if (!chunk.IsEmpty)
        {
            _pending.Append(chunk);
        }

        return ExtractPendingMessages();
    }

    /// <summary>丢弃尚未完成的半包，通常在客户端断开或开始新会话时调用。</summary>
    public void Reset() => _pending.Clear();

    /// <summary>
    /// 对一段独立文本执行一次拆包，便于单元测试半包、粘包和无效前缀。
    /// </summary>
    public static IReadOnlyList<string> Extract(
        string? text,
        int maxPendingCharacters = DefaultMaxPendingCharacters)
    {
        var parser = new MessageFrameParser(maxPendingCharacters);
        return parser.Append(text);
    }

    private IReadOnlyList<string> ExtractPendingMessages()
    {
        var messages = new List<string>();

        while (true)
        {
            // StringBuilder 没有 IndexOf；当前缓存上限只有 1 MB，转成快照
            // 比维护容易出错的增量索引更清晰，也足够满足控制报文吞吐量。
            string text = _pending.ToString();
            int start = text.IndexOf(MessageProtocol.StartMarker, StringComparison.Ordinal);

            if (start < 0)
            {
                // 单独的 '&' 可能是下一次读取中 '&|' 的前半段，必须保留。
                _pending.Clear();
                if (text.EndsWith('&'))
                {
                    _pending.Append('&');
                }

                break;
            }

            if (start > 0)
            {
                // 丢弃报文起始标记前的日志或其他垃圾字符。
                _pending.Remove(0, start);
                text = _pending.ToString();
            }

            int end = text.IndexOf(
                MessageProtocol.EndMarker,
                MessageProtocol.StartMarker.Length,
                StringComparison.Ordinal);

            // 尚未收到 |@，保留当前缓存，等待下一次读取。
            if (end < 0)
            {
                break;
            }

            int messageLength = end + MessageProtocol.EndMarker.Length;
            messages.Add(text[..messageLength]);
            _pending.Remove(0, messageLength);
        }

        if (_pending.Length > MaxPendingCharacters)
        {
            throw new ProtocolFrameTooLargeException(
                $"客户端未完成报文超过 {MaxPendingCharacters:N0} 个字符，连接应关闭。");
        }

        return messages;
    }
}

/// <summary>表示对端发送了超过允许缓存大小的未完成报文。</summary>
public sealed class ProtocolFrameTooLargeException : IOException
{
    public ProtocolFrameTooLargeException(string message) : base(message)
    {
    }
}
