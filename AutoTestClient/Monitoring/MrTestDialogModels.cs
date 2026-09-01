namespace AutoTestClient.Monitoring;

/// <summary>MRTEST 固定确认窗口事件。首版不读取提示文字，只传递窗口句柄。</summary>
public sealed class MrTestDialogDetectedEventArgs : EventArgs
{
    public MrTestDialogDetectedEventArgs(nint handle, int processId, string title)
    {
        Handle = handle;
        ProcessId = processId;
        Title = title;
        DetectedAtUtc = DateTime.UtcNow;
    }

    public nint Handle { get; }
    public int ProcessId { get; }
    public string Title { get; }
    public DateTime DetectedAtUtc { get; }
}

/// <summary>监视器发现的窗口信息；若窗口没有标准按钮，确认流程会回退到 Enter/关闭。</summary>
public sealed record MrTestDialogSnapshot(
    nint Handle,
    int ProcessId,
    string Title,
    nint OkButtonHandle);
