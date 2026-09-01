namespace AutoTestClient.Protocol;

/// <summary>
/// 测量相关的固定报文常量和应答判断。
/// </summary>
/// <remarks>
/// 手动单次测量请求不带 <c>Run</c>；<c>Run</c> 只属于设备返回的中间状态。
/// 仍保留历史常量名称，避免引用该类的旧代码无法编译。
/// </remarks>
public static class MeasurementProtocol
{
    public const string TargetSuccess = "&|Target|OK|@";

    /// <summary>新协议自动单次测量请求。</summary>
    public const string SingleAutoMeasure = "&|Meas|A|A|Run|@";

    /// <summary>手动单次测量请求；发送后设备返回 Run 中间状态。</summary>
    public const string SingleManualMeasure = MessageProtocol.DefaultMeasurementRequest;

    /// <summary>旧版自动单次请求（没有 Run 字段）。</summary>
    public const string LegacySingleAutoMeasure = "&|Meas|A|A|@";

    /// <summary>兼容旧代码名称；当前不带 Run 的请求就是正式协议。</summary>
    public const string LegacySingleManualMeasure = SingleManualMeasure;

    /// <summary>旧版本错误保存的请求（带 Run），仅用于配置迁移。</summary>
    public const string PreviousDefaultManualMeasure = MessageProtocol.PreviousDefaultMeasurementRequest;

    public const string SingleAutoRunning = "&|Meas|A|A|Run|@";
    public const string SingleManualRunning = MessageProtocol.DefaultMeasurementRunningResponse;
    public const string SingleAutoCompleted = "&|Meas|A|OK|@";
    public const string SingleManualCompleted = "&|Meas|A|OK|@";
    public const string ContinuousAutoMeasure = "&|Meas|S|A|Run|@";
    public const string ContinuousManualMeasure = "&|Meas|S|M|Run|@";
    public const string ContinuousRunning = "&|Meas|S|Run|@";
    public const string ContinuousCompleted = "&|Meas|S|OK|@";

    /// <summary>判断返回是否属于 Target 配置阶段。</summary>
    public static bool IsTargetResponse(string? message)
    {
        string value = (message ?? string.Empty).Trim();
        return value == TargetSuccess ||
               value.StartsWith("&|Target|NG|", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>把 Target 返回中的错误码翻译为操作员可读文字。</summary>
    public static string GetTargetError(string? message)
    {
        string value = (message ?? string.Empty).Trim();
        string code = value.Split('|').ElementAtOrDefault(3) ?? string.Empty;
        return code switch
        {
            "0" => "图卡错误",
            "1" => "点模板错误",
            "2" => "创建过多测试项",
            "3" => "其他错误",
            _ => $"未知错误（返回：{value}）"
        };
    }

    /// <summary>判断返回是否为任一单次/连续测量的相关应答。</summary>
    public static bool IsMeasurementResponse(string? message)
    {
        string value = (message ?? string.Empty).Trim();
        return value.StartsWith("&|Meas|A|", StringComparison.OrdinalIgnoreCase) ||
               value.StartsWith("&|Meas|S|", StringComparison.OrdinalIgnoreCase);
    }
}
