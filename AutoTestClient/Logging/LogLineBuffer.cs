namespace AutoTestClient.Logging;

/// <summary>
/// 为界面日志保留一个有界的行缓冲。达到清理阈值时批量移除最旧行，
/// 避免 RichTextBox 长时间运行后持续增长，同时保留最近的诊断上下文。
/// </summary>
public sealed class LogLineBuffer
{
    private readonly Queue<string> _lines = new();

    public LogLineBuffer(int cleanupThreshold = 200, int retainedAfterCleanup = 150)
    {
        if (cleanupThreshold < 2)
            throw new ArgumentOutOfRangeException(nameof(cleanupThreshold));
        if (retainedAfterCleanup < 1 || retainedAfterCleanup >= cleanupThreshold)
            throw new ArgumentOutOfRangeException(nameof(retainedAfterCleanup));

        CleanupThreshold = cleanupThreshold;
        RetainedAfterCleanup = retainedAfterCleanup;
    }

    public int CleanupThreshold { get; }
    public int RetainedAfterCleanup { get; }
    public int Count => _lines.Count;
    public IReadOnlyList<string> Lines => _lines.ToArray();

    /// <summary>
    /// 加入一行；返回 true 表示本次达到阈值并已批量清理旧行，调用方应
    /// 用 <see cref="Lines"/> 整体刷新显示控件。
    /// </summary>
    public bool Add(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        _lines.Enqueue(line);
        if (_lines.Count < CleanupThreshold) return false;

        while (_lines.Count > RetainedAfterCleanup)
            _lines.Dequeue();
        return true;
    }

    public void Clear() => _lines.Clear();
}
