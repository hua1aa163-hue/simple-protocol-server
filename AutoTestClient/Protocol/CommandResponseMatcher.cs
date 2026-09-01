namespace AutoTestClient.Protocol;

/// <summary>一条返回报文相对于当前请求的分类。</summary>
public enum ResponseClassification
{
    /// <summary>与当前请求无关，或只是 Run 中间状态。</summary>
    Ignored,

    /// <summary>设备已成功完成当前请求。</summary>
    Success,

    /// <summary>设备返回了当前请求对应的失败/异常终态。</summary>
    Failure
}

/// <summary>
/// 根据请求判断设备返回是否已经达到最终成功或失败状态。
/// </summary>
public static class CommandResponseMatcher
{
    /// <summary>判断请求是否有内置的最终返回匹配规则。</summary>
    public static bool SupportsRequest(string? request)
    {
        string value = (request ?? string.Empty).Trim();
        if (!MessageProtocol.TryValidateMessage(value, out _, out _)) return false;

        return value == "&|Stop|@" ||
               value.StartsWith("&|Elems|C|", StringComparison.Ordinal) ||
               value.StartsWith("&|Elems|S|", StringComparison.Ordinal) ||
               value.StartsWith("&|FF|", StringComparison.Ordinal) ||
               IsMeasurementRequest(value) ||
               value.StartsWith("&|Target|", StringComparison.Ordinal);
    }

    /// <summary>返回请求对应的默认等待时长：测量 600 秒，其他命令 10 秒。</summary>
    public static TimeSpan GetResponseTimeout(string? request) =>
        IsMeasurementRequest(request)
            ? MessageProtocol.MeasurementResponseTimeout
            : MessageProtocol.ControlResponseTimeout;

    /// <summary>分类一条返回报文；未知前缀不会误判为当前请求成功。</summary>
    public static ResponseClassification Classify(string? request, string? response)
    {
        string outgoing = (request ?? string.Empty).Trim();
        string incoming = (response ?? string.Empty).Trim();
        if (outgoing.Length == 0 || incoming.Length == 0) return ResponseClassification.Ignored;

        if (outgoing == "&|Stop|@")
        {
            if (incoming.Equals("&|Stop|OK|@", StringComparison.OrdinalIgnoreCase))
                return ResponseClassification.Success;

            return incoming.StartsWith("&|Stop|", StringComparison.OrdinalIgnoreCase)
                ? ResponseClassification.Failure
                : ResponseClassification.Ignored;
        }

        if (outgoing.StartsWith("&|Elems|C|", StringComparison.Ordinal) ||
            outgoing.StartsWith("&|Elems|S|", StringComparison.Ordinal))
        {
            if (incoming.Equals("&|Elems|OK|@", StringComparison.OrdinalIgnoreCase))
                return ResponseClassification.Success;

            return incoming.StartsWith("&|Elems|", StringComparison.OrdinalIgnoreCase)
                ? ResponseClassification.Failure
                : ResponseClassification.Ignored;
        }

        if (outgoing.StartsWith("&|FF|", StringComparison.Ordinal))
        {
            if (!incoming.StartsWith("&|FF|", StringComparison.OrdinalIgnoreCase) ||
                !incoming.EndsWith(MessageProtocol.EndMarker, StringComparison.Ordinal))
            {
                return ResponseClassification.Ignored;
            }

            return ContainsStatus(incoming, "NG")
                ? ResponseClassification.Failure
                : ResponseClassification.Success;
        }

        if (outgoing.StartsWith("&|Target|", StringComparison.Ordinal))
        {
            if (!incoming.StartsWith("&|Target|", StringComparison.OrdinalIgnoreCase))
                return ResponseClassification.Ignored;

            if (ContainsStatus(incoming, "OK")) return ResponseClassification.Success;
            if (ContainsStatus(incoming, "NG")) return ResponseClassification.Failure;
            return ResponseClassification.Failure;
        }

        if (!IsMeasurementRequest(outgoing)) return ResponseClassification.Ignored;

        string? family = GetMeasurementFamily(outgoing);
        if (family is null || !incoming.StartsWith($"&|Meas|{family}|", StringComparison.OrdinalIgnoreCase))
            return ResponseClassification.Ignored;

        // 设备可能回显完整请求，也可能只返回 &|Meas|A|Run|@；二者都是中间态。
        if (HasTrailingToken(incoming, "Run")) return ResponseClassification.Ignored;
        if (HasToken(incoming, "OK")) return ResponseClassification.Success;
        if (HasToken(incoming, "NG")) return ResponseClassification.Failure;

        // 已经确认是同一种 Meas（A 或 S），却不是 Run/OK，不能继续等待到超时。
        return ResponseClassification.Failure;
    }

    /// <summary>判断返回是否为匹配的最终成功应答。</summary>
    public static bool IsSuccess(string request, string response) =>
        Classify(request, response) == ResponseClassification.Success;

    /// <summary>判断返回是否为匹配的失败终态。</summary>
    public static bool IsFailure(string request, string response) =>
        Classify(request, response) == ResponseClassification.Failure;

    /// <summary>判断返回是否为 Run 等可忽略的中间状态。</summary>
    public static bool IsIntermediate(string request, string response) =>
        Classify(request, response) == ResponseClassification.Ignored &&
        IsMeasurementRequest(request) &&
        HasTrailingToken(response, "Run");

    /// <summary>返回请求的显示用成功应答示例。</summary>
    public static string GetExpectedSuccessResponse(string request)
    {
        request ??= string.Empty;
        request = request.Trim();
        string? family = GetMeasurementFamily(request);
        if (family is not null) return $"&|Meas|{family}|OK|@";
        if (request.StartsWith("&|Stop|", StringComparison.Ordinal)) return "&|Stop|OK|@";
        if (request.StartsWith("&|Elems|", StringComparison.Ordinal)) return "&|Elems|OK|@";
        if (request.StartsWith("&|FF|", StringComparison.Ordinal)) return "&|FF|焦距|@";
        if (request.StartsWith("&|Target|", StringComparison.Ordinal)) return "&|Target|OK|@";
        return string.Empty;
    }

    private static bool IsMeasurementRequest(string? request)
    {
        string value = (request ?? string.Empty).Trim();
        if (!value.StartsWith("&|Meas|", StringComparison.OrdinalIgnoreCase)) return false;
        return GetMeasurementFamily(value) is not null;
    }

    private static string? GetMeasurementFamily(string? request)
    {
        string value = (request ?? string.Empty).Trim();
        if (!value.StartsWith("&|Meas|", StringComparison.OrdinalIgnoreCase)) return null;
        if (!value.EndsWith(MessageProtocol.EndMarker, StringComparison.Ordinal) ||
            value.Length < MessageProtocol.StartMarker.Length + MessageProtocol.EndMarker.Length)
            return null;

        string body = value[MessageProtocol.StartMarker.Length..^MessageProtocol.EndMarker.Length];
        string[] fields = body.Split('|', StringSplitOptions.None);
        if (fields.Length < 2 || !fields[0].Equals("Meas", StringComparison.OrdinalIgnoreCase)) return null;

        return fields[1].Equals("A", StringComparison.OrdinalIgnoreCase) ||
               fields[1].Equals("S", StringComparison.OrdinalIgnoreCase)
            ? fields[1].ToUpperInvariant()
            : null;
    }

    private static bool HasTrailingToken(string value, string token)
    {
        string body = GetBody(value);
        string[] fields = body.Split('|', StringSplitOptions.None);
        return fields.Length > 0 && fields[^1].Equals(token, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasToken(string value, string token)
    {
        string body = GetBody(value);
        return body.Split('|', StringSplitOptions.None)
            .Any(field => field.Equals(token, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsStatus(string value, string token) => HasToken(value, token);

    private static string GetBody(string value)
    {
        if (!value.StartsWith(MessageProtocol.StartMarker, StringComparison.Ordinal) ||
            !value.EndsWith(MessageProtocol.EndMarker, StringComparison.Ordinal) ||
            value.Length < MessageProtocol.StartMarker.Length + MessageProtocol.EndMarker.Length)
            return string.Empty;

        return value[MessageProtocol.StartMarker.Length..^MessageProtocol.EndMarker.Length];
    }
}
