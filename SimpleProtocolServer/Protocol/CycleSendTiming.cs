// 本文件把“同一轮行间隔”和“下一轮循环间隔”的规则集中到一个可测试的方法中。
namespace SimpleProtocolServer.Protocol;

/// <summary>计算循环列表在一条报文发送完成后，到下一条报文之间的等待时间。</summary>
internal static class CycleSendTiming
{
    /// <summary>
    /// 返回 WinForms Timer 需要的毫秒数。
    /// 发送末行后使用循环间隔，其他行后使用行间隔。
    /// </summary>
    public static int GetNextIntervalMilliseconds(
        int sentPosition,
        int messageCount,
        decimal rowIntervalSeconds,
        decimal cycleIntervalMinutes)
    {
        if (messageCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(messageCount));
        }

        if (sentPosition < 1 || sentPosition > messageCount)
        {
            throw new ArgumentOutOfRangeException(nameof(sentPosition));
        }

        // sentPosition 与 messageCount 相等，说明刚发送的是这一轮最后一条。
        decimal milliseconds = sentPosition == messageCount
            ? cycleIntervalMinutes * 60_000m
            : rowIntervalSeconds * 1_000m;

        if (milliseconds < 1m || milliseconds > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(milliseconds));
        }

        // NumericUpDown 使用 decimal，Timer.Interval 使用 int，所以最后统一转换。
        return decimal.ToInt32(milliseconds);
    }
}
