// 串扰测试发送命令后，会用本文件判断设备返回的是“无关消息、成功还是失败”。
namespace SimpleProtocolServer.Protocol;

/// <summary>一条返回报文相对于当前请求的判断结果。</summary>
internal enum ResponseClassification
{
    /// <summary>与当前请求无关，或只是 Run 之类的中间状态，需要继续等待。</summary>
    Ignored,
    /// <summary>设备已经成功完成当前命令。</summary>
    Success,
    /// <summary>设备明确失败，或返回了同类但无法识别的终态。</summary>
    Failure
}

/// <summary>根据实际发送的报文判断返回报文是否代表操作完成。</summary>
internal static class CommandResponseMatcher
{
    /// <summary>判断该请求是否有明确的返回匹配规则，可否用于串扰测试。</summary>
    public static bool SupportsRequest(string request) =>
        request == "&|Stop|@" ||
        request.StartsWith("&|Elems|C|", StringComparison.Ordinal) ||
        request.StartsWith("&|Elems|S|", StringComparison.Ordinal) ||
        request.StartsWith("&|FF|", StringComparison.Ordinal) ||
        request == "&|Meas|A|A|@" ||
        request == "&|Meas|A|M|@" ||
        request == "&|Meas|S|A|@" ||
        request == "&|Meas|S|M|@";

    /// <summary>测量耗时较长等 600 秒，其他控制命令最多等 10 秒。</summary>
    public static TimeSpan GetResponseTimeout(string request) =>
        request.StartsWith("&|Meas|", StringComparison.Ordinal)
            ? TimeSpan.FromSeconds(600)
            : TimeSpan.FromSeconds(10);

    /// <summary>
    /// 将一条设备返回与正在等待的请求比较。
    /// 注意：TCP 可能同时收到日志或其他命令的应答，所以不能把第一条返回直接当作完成。
    /// </summary>
    public static ResponseClassification Classify(string request, string response)
    {
        // 停止命令只有精确的 OK 算成功；同为 Stop 但不是 OK，则认为失败。
        if (request == "&|Stop|@")
        {
            if (response == "&|Stop|OK|@") return ResponseClassification.Success;
            return response.StartsWith("&|Stop|", StringComparison.Ordinal)
                ? ResponseClassification.Failure
                : ResponseClassification.Ignored;
        }

        // 切换配方和保存配方共用 Elems 返回格式。
        if (request.StartsWith("&|Elems|C|", StringComparison.Ordinal) ||
            request.StartsWith("&|Elems|S|", StringComparison.Ordinal))
        {
            if (response == "&|Elems|OK|@") return ResponseClassification.Success;
            return response.StartsWith("&|Elems|", StringComparison.Ordinal)
                ? ResponseClassification.Failure
                : ResponseClassification.Ignored;
        }

        // FF 成功时返回具体焦距值，因此不能和请求做完全相等比较。
        if (request.StartsWith("&|FF|", StringComparison.Ordinal))
        {
            if (!response.StartsWith("&|FF|", StringComparison.Ordinal) ||
                !response.EndsWith("|@", StringComparison.Ordinal))
            {
                return ResponseClassification.Ignored;
            }

            return response.Contains("|NG|", StringComparison.OrdinalIgnoreCase)
                ? ResponseClassification.Failure
                : ResponseClassification.Success;
        }

        // 单次测量使用 A 前缀，连续测量使用 S 前缀。
        string? measurementPrefix = request switch
        {
            "&|Meas|A|A|@" or "&|Meas|A|M|@" => "&|Meas|A|",
            "&|Meas|S|A|@" or "&|Meas|S|M|@" => "&|Meas|S|",
            _ => null
        };

        if (measurementPrefix is null ||
            !response.StartsWith(measurementPrefix, StringComparison.Ordinal))
        {
            return ResponseClassification.Ignored;
        }

        if (response == $"{measurementPrefix}OK|@")
        {
            return ResponseClassification.Success;
        }

        // Run 只是开始执行的中间应答，必须继续等待最终 OK/NG。
        if (response.EndsWith("|Run|@", StringComparison.OrdinalIgnoreCase))
        {
            return ResponseClassification.Ignored;
        }

        // 已经确认是当前测量类型，又不是 Run 或 OK，只能作为失败终态处理。
        return ResponseClassification.Failure;
    }
}
