namespace AutoTestClient.Protocol;

/// <summary>
/// 统一定义 <c>&amp;|...|@</c> 报文格式、标准命令和参数校验。
/// </summary>
/// <remarks>
/// 当前 MRTEST 接口的手动单次测量请求不包含 <c>Run</c> 字段：先发送
/// <c>&amp;|Meas|A|M|@</c>，设备开始测量后返回
/// <c>&amp;|Meas|A|M|Run|@</c>，完成时返回
/// <c>&amp;|Meas|A|OK|@</c>。旧版本曾把带 <c>Run</c> 的开始响应误保存为请求，
/// 该历史值只在配置迁移时识别，不再作为新界面的默认发送值。
/// </remarks>
public static class MessageProtocol
{
    /// <summary>每条报文的起始标记。</summary>
    public const string StartMarker = "&|";

    /// <summary>每条报文的结束标记。</summary>
    public const string EndMarker = "|@";

    /// <summary>默认手动单次测量请求（不带 Run；Run 是设备返回的中间状态）。</summary>
    public const string DefaultMeasurementRequest = "&|Meas|A|M|@";

    /// <summary>旧版本错误保存的默认请求，仅用于配置迁移。</summary>
    public const string PreviousDefaultMeasurementRequest = "&|Meas|A|M|Run|@";

    /// <summary>手动单次测量开始后的中间返回。</summary>
    public const string DefaultMeasurementRunningResponse = "&|Meas|A|M|Run|@";

    /// <summary>默认手动单次测量完成应答。</summary>
    public const string DefaultMeasurementSuccessResponse = "&|Meas|A|OK|@";

    /// <summary>测量命令允许等待的最长时间。</summary>
    public static readonly TimeSpan MeasurementResponseTimeout = TimeSpan.FromSeconds(600);

    /// <summary>普通控制命令允许等待的最长时间。</summary>
    public static readonly TimeSpan ControlResponseTimeout = TimeSpan.FromSeconds(10);

    /// <summary>返回参数框左侧应显示的说明。</summary>
    public static string GetParameterLabel(CommandType command) => command switch
    {
        CommandType.SwitchRecipe or CommandType.SaveRecipe => "配方名称：",
        CommandType.GetFocalLength => "VID(mm)：",
        _ => "命令参数："
    };

    /// <summary>返回首次打开界面时使用的默认参数。</summary>
    public static string GetDefaultParameter(CommandType command) => command switch
    {
        CommandType.SwitchRecipe => "qwerty",
        CommandType.SaveRecipe => "asdfgh",
        CommandType.GetFocalLength => "7500",
        _ => string.Empty
    };

    /// <summary>判断指定命令是否必须填写参数。</summary>
    public static bool RequiresParameter(CommandType command) => command is
        CommandType.SwitchRecipe or CommandType.SaveRecipe or CommandType.GetFocalLength;

    /// <summary>返回便于界面展示的预期返回流程。</summary>
    public static string GetExpectedResponse(CommandType command) => command switch
    {
        CommandType.Stop => "&|Stop|OK|@",
        CommandType.SwitchRecipe => "&|Elems|OK|@  或  &|Elems|NG|@",
        CommandType.SaveRecipe => "&|Elems|OK|@",
        CommandType.GetFocalLength => "&|FF|焦距|@  （NG 表示失败）",
        CommandType.SingleAutomatic => "&|Meas|A|Run|@  →  &|Meas|A|OK|@",
        CommandType.SingleManual => $"{DefaultMeasurementRequest}  →  {DefaultMeasurementRunningResponse}  →  {DefaultMeasurementSuccessResponse}",
        CommandType.ContinuousAutomatic => "&|Meas|S|Run|@  →  &|Meas|S|OK|@",
        CommandType.ContinuousManual => "&|Meas|S|Run|@  →  &|Meas|S|OK|@",
        _ => string.Empty
    };

    /// <summary>
    /// 根据命令和参数生成一条完整报文。
    /// </summary>
    public static bool TryBuildMessage(
        CommandType command,
        string? parameter,
        out string message,
        out string error)
    {
        message = string.Empty;
        error = string.Empty;
        parameter = (parameter ?? string.Empty).Trim();

        if (RequiresParameter(command) && parameter.Length == 0)
        {
            error = command == CommandType.GetFocalLength
                ? "请输入 VID。"
                : "请输入配方名称。";
            return false;
        }

        if (ContainsReservedCharacter(parameter))
        {
            error = "参数不能包含 |、&、@或换行符。";
            return false;
        }

        if (command == CommandType.GetFocalLength &&
            (!int.TryParse(parameter, out int vid) || vid <= 0))
        {
            error = "VID 必须是大于 0 的整数，例如 7500。";
            return false;
        }

        message = command switch
        {
            CommandType.Stop => "&|Stop|@",
            CommandType.SwitchRecipe => $"&|Elems|C|{parameter}|@",
            CommandType.SaveRecipe => $"&|Elems|S|{parameter}|@",
            CommandType.GetFocalLength => $"&|FF|{parameter}|@",
            CommandType.SingleAutomatic => "&|Meas|A|A|Run|@",
            CommandType.SingleManual => DefaultMeasurementRequest,
            CommandType.ContinuousAutomatic => "&|Meas|S|A|Run|@",
            CommandType.ContinuousManual => "&|Meas|S|M|Run|@",
            _ => throw new ArgumentOutOfRangeException(nameof(command), command, "未知命令。")
        };

        return true;
    }

    /// <summary>生成报文；参数无效时抛出 <see cref="FormatException"/>。</summary>
    public static string BuildMessage(CommandType command, string? parameter = null)
    {
        if (!TryBuildMessage(command, parameter, out string message, out string error))
        {
            throw new FormatException(error);
        }

        return message;
    }

    /// <summary>
    /// 校验手工编辑框中的文本是否恰好包含一条完整报文。
    /// </summary>
    public static bool TryValidateMessage(
        string? text,
        out string message,
        out string error)
    {
        message = (text ?? string.Empty).Trim();
        error = string.Empty;

        if (message.Length == 0)
        {
            error = "请输入待发送报文。";
            return false;
        }

        if (message.Contains('\r') || message.Contains('\n'))
        {
            error = "每次只能发送一条报文，请删除换行符。";
            return false;
        }

        if (!message.StartsWith(StartMarker, StringComparison.Ordinal) ||
            !message.EndsWith(EndMarker, StringComparison.Ordinal))
        {
            error = "报文必须以 &| 开始，以 |@ 结束。";
            return false;
        }

        // 第二个起始标记或结束标记意味着粘贴了多条报文，
        // 不能让一次 SendAsync 意外发送多个命令。
        if (message.IndexOf(StartMarker, StartMarker.Length, StringComparison.Ordinal) >= 0 ||
            message.IndexOf(
                EndMarker,
                0,
                message.Length - EndMarker.Length,
                StringComparison.Ordinal) >= 0)
        {
            error = "发送框中不能同时放置多条报文。";
            return false;
        }

        if (message.Length <= StartMarker.Length + EndMarker.Length)
        {
            error = "报文正文不能为空。";
            return false;
        }

        return true;
    }

    /// <summary>判断字符串是否含有协议分隔符或换行。</summary>
    public static bool ContainsReservedCharacter(string? value) =>
        (value ?? string.Empty).IndexOfAny(['|', '&', '@', '\r', '\n']) >= 0;

    /// <summary>返回命令对应的等待超时（测量 600 秒，其他命令 10 秒）。</summary>
    public static TimeSpan GetResponseTimeout(string? request) =>
        (request ?? string.Empty).TrimStart().StartsWith("&|Meas|", StringComparison.Ordinal)
            ? MeasurementResponseTimeout
            : ControlResponseTimeout;

    /// <summary>
    /// 将旧版本错误保存的手动测量默认请求迁移为当前请求。
    /// 仅匹配完整的历史默认值，其他手工报文保持不变。
    /// </summary>
    public static string MigrateMeasurementRequest(string? request)
    {
        string value = (request ?? string.Empty).Trim();
        return value.Equals(PreviousDefaultMeasurementRequest, StringComparison.OrdinalIgnoreCase)
            ? DefaultMeasurementRequest
            : value;
    }

    /// <summary>兼容旧参考代码名称。</summary>
    public static bool TryBuild(
        CommandType command,
        string? parameter,
        out string message,
        out string error) =>
        TryBuildMessage(command, parameter, out message, out error);
}

/// <summary>
/// 兼容参考项目中的类名；新代码建议使用 <see cref="MessageProtocol"/>。
/// </summary>
public static class SimpleMessageProtocol
{
    public const string StartMarker = MessageProtocol.StartMarker;
    public const string EndMarker = MessageProtocol.EndMarker;
    public const string DefaultMeasurementRequest = MessageProtocol.DefaultMeasurementRequest;
    public const string PreviousDefaultMeasurementRequest = MessageProtocol.PreviousDefaultMeasurementRequest;
    public const string DefaultMeasurementRunningResponse = MessageProtocol.DefaultMeasurementRunningResponse;
    public const string DefaultMeasurementSuccessResponse = MessageProtocol.DefaultMeasurementSuccessResponse;

    public static string GetParameterLabel(CommandType command) => MessageProtocol.GetParameterLabel(command);
    public static string GetDefaultParameter(CommandType command) => MessageProtocol.GetDefaultParameter(command);
    public static bool RequiresParameter(CommandType command) => MessageProtocol.RequiresParameter(command);
    public static string GetExpectedResponse(CommandType command) => MessageProtocol.GetExpectedResponse(command);
    public static bool TryBuildMessage(CommandType command, string? parameter, out string message, out string error) =>
        MessageProtocol.TryBuildMessage(command, parameter, out message, out error);
    public static bool TryValidateMessage(string? text, out string message, out string error) =>
        MessageProtocol.TryValidateMessage(text, out message, out error);
    public static bool ContainsReservedCharacter(string? value) =>
        MessageProtocol.ContainsReservedCharacter(value);
    public static TimeSpan GetResponseTimeout(string? request) =>
        MessageProtocol.GetResponseTimeout(request);
    public static string MigrateMeasurementRequest(string? request) =>
        MessageProtocol.MigrateMeasurementRequest(request);
}
