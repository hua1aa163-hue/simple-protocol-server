using System.Net;
using System.Net.Sockets;
using System.Text;
using AutoTestClient.Protocol;

namespace AutoTestClient.Networking;

/// <summary>
/// 单客户端 TCP 服务端。
/// <para>
/// MRTEST/测试设备作为 TCP 客户端连接本程序；本程序在本地端口监听，
/// 通过 <see cref="SendAsync"/> 发出 <c>&amp;|...|@</c> 命令，再由事件接收应答。
/// </para>
/// <para>
/// 一次只处理一个已建立的客户端。客户端断开后，监听循环会继续等待下一次连接。
/// </para>
/// </summary>
public sealed class TcpMessageServer : IAsyncDisposable
{
    /// <summary>默认监听端口，与第二参考项目保持一致。</summary>
    public const int DefaultPort = 9527;

    /// <summary>默认单条未完成报文缓存上限。</summary>
    public const int DefaultMaxPendingCharacters = MessageFrameParser.DefaultMaxPendingCharacters;

    private readonly object _stateLock = new();
    // 同一个 NetworkStream 上的多个写入必须串行，否则短报文可能互相交叉。
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    // 请求-应答事务也串行化，避免两个等待者同时把同一条 OK 当成自己的结果。
    private readonly SemaphoreSlim _transactionLock = new(1, 1);
    private readonly Encoding _encoding;
    private readonly int _maxPendingCharacters;
    private readonly CancellationTokenSource _lifetimeCancellation = new();

    private TcpListener? _listener;
    private TcpClient? _client;
    private NetworkStream? _stream;
    private CancellationTokenSource? _serverCancellation;
    private CancellationTokenRegistration _externalCancellationRegistration;
    private Task? _acceptTask;
    private Task? _stopTask;
    private long _connectionGeneration;
    private EndPoint? _remoteEndPoint;
    private bool _disposed;

    /// <summary>使用 UTF-8、默认 1 MB 缓存创建服务端。</summary>
    public TcpMessageServer()
        : this(new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
            DefaultMaxPendingCharacters)
    {
    }

    /// <summary>
    /// 创建可配置编码和缓存上限的服务端。协议默认使用不带 BOM 的 UTF-8。
    /// </summary>
    public TcpMessageServer(Encoding encoding, int maxPendingCharacters = DefaultMaxPendingCharacters)
    {
        ArgumentNullException.ThrowIfNull(encoding);
        if (maxPendingCharacters < MessageProtocol.StartMarker.Length + MessageProtocol.EndMarker.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxPendingCharacters), "缓存上限太小，无法容纳最短报文。");
        }

        _encoding = encoding;
        _maxPendingCharacters = maxPendingCharacters;
    }

    /// <summary>是否已经启动端口监听。</summary>
    public bool IsListening
    {
        get
        {
            lock (_stateLock)
            {
                return _listener is not null;
            }
        }
    }

    /// <summary>当前是否有客户端连接。</summary>
    public bool IsConnected
    {
        get
        {
            lock (_stateLock)
            {
                // Connected 属性本身可能有短暂滞后，因此以流是否存在为主。
                return _stream is not null && _client is not null;
            }
        }
    }

    /// <summary>实际监听端口；传入 0 时是操作系统分配的空闲端口。</summary>
    public int Port { get; private set; }

    /// <summary>监听端点；未启动时为 null。</summary>
    public IPEndPoint? LocalEndPoint
    {
        get
        {
            lock (_stateLock)
            {
                return _listener?.LocalEndpoint as IPEndPoint;
            }
        }
    }

    /// <summary>当前客户端端点；未连接时为 null。</summary>
    public EndPoint? RemoteEndPoint
    {
        get
        {
            lock (_stateLock)
            {
                return _remoteEndPoint;
            }
        }
    }

    /// <summary>UTF-8/自定义编码，供测试或日志显示使用。</summary>
    public Encoding Encoding => _encoding;

    /// <summary>客户端刚建立连接时触发，参数为远端端点文本。</summary>
    public event EventHandler<string>? ClientConnected;

    /// <summary>每拆出一条完整报文时触发。</summary>
    public event EventHandler<string>? MessageReceived;

    /// <summary>一条报文成功写入当前 TCP 客户端后触发。</summary>
    /// <remarks>
    /// 事件在 <see cref="NetworkStream.FlushAsync"/> 成功返回后触发，因此只表示
    /// 本端已经完成写入；设备是否完成命令仍以 <see cref="MessageReceived"/> 的最终应答为准。
    /// </remarks>
    public event EventHandler<string>? MessageSent;

    /// <summary>客户端断开或协议读取失败时触发；主动 Stop 不视为异常断线。</summary>
    public event EventHandler<string>? ConnectionClosed;

    /// <summary>发生无效 UTF-8 或超大未完成报文时触发（随后会关闭连接）。</summary>
    public event EventHandler<Exception>? ProtocolError;

    /// <summary>
    /// 在指定地址和端口启动监听。令牌取消后会异步停止本次监听；调用方仍可
    /// 直接使用 <see cref="StopAsync"/> 提前停止。
    /// </summary>
    public Task StartAsync(
        IPAddress address,
        int port,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(address);
        ValidatePort(port);
        cancellationToken.ThrowIfCancellationRequested();

        TcpListener listener;
        lock (_stateLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_listener is not null)
            {
                throw new InvalidOperationException("TCP 服务器已经启动。");
            }

            // 上一次 StopAsync 已完成后允许重新启动；停止尚未完成时不允许抢占端口。
            if (_stopTask is { IsCompleted: false })
            {
                throw new InvalidOperationException("TCP 服务器正在停止，请稍后再启动。");
            }

            listener = new TcpListener(address, port);
            listener.Start(1); // backlog=1，明确表达只服务一个客户端。

            _listener = listener;
            _serverCancellation = new CancellationTokenSource();
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            _remoteEndPoint = null;
            _acceptTask = AcceptLoopAsync(listener, _serverCancellation.Token);
            _stopTask = null;
        }

        // 注册放在状态写入之后，避免“令牌已取消”的同步回调看到半初始化对象。
        if (cancellationToken.CanBeCanceled)
        {
            CancellationTokenRegistration registration = cancellationToken.Register(
                static state => ((TcpMessageServer)state!).RequestStopFromExternalCancellation(),
                this);
            lock (_stateLock)
            {
                if (ReferenceEquals(_listener, listener) && !_disposed)
                {
                    _externalCancellationRegistration = registration;
                }
                else
                {
                    registration.Dispose();
                }
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>使用端点启动监听的便捷重载。</summary>
    public Task StartAsync(IPEndPoint endpoint, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        return StartAsync(endpoint.Address, endpoint.Port, cancellationToken);
    }

    /// <summary>
    /// 根据主机名启动监听。主机名解析失败或没有可用地址时抛出异常。
    /// </summary>
    public Task StartAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        if (!IPAddress.TryParse(host, out IPAddress? address))
        {
            IPAddress[] addresses = Dns.GetHostAddresses(host);
            address = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
                      ?? addresses.FirstOrDefault()
                      ?? throw new SocketException((int)SocketError.HostNotFound);
        }

        return StartAsync(address, port, cancellationToken);
    }

    /// <summary>向当前客户端发送一条完整 UTF-8 报文。</summary>
    public async Task SendAsync(string message, CancellationToken cancellationToken = default)
    {
        // 兼容旧版本把手动测量 Run 中间响应当成请求的配置；在网络层再做
        // 一次精确迁移，确保任何调用入口真正写出的测试请求都是不带 Run 的
        // &|Meas|A|M|@，同时不改写其他自定义报文。
        string migratedMessage = MessageProtocol.MigrateMeasurementRequest(message);
        if (!MessageProtocol.TryValidateMessage(migratedMessage, out string normalized, out string error))
        {
            throw new FormatException(error);
        }

        NetworkStream stream;
        TcpClient client;
        long generation;
        CancellationToken serverToken;

        lock (_stateLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            stream = _stream ?? throw new InvalidOperationException("尚无 TCP 客户端连接。");
            client = _client ?? throw new InvalidOperationException("尚无 TCP 客户端连接。");
            generation = _connectionGeneration;
            serverToken = _serverCancellation?.Token ?? CancellationToken.None;
        }

        using CancellationTokenSource operationCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, serverToken, _lifetimeCancellation.Token);
        CancellationToken operationToken = operationCancellation.Token;

        byte[] data = _encoding.GetBytes(normalized);
        await _sendLock.WaitAsync(operationToken).ConfigureAwait(false);
        try
        {
            // 排队等待发送锁期间客户端可能已经换了连接，绝不能把旧命令发给新客户端。
            lock (_stateLock)
            {
                if (!ReferenceEquals(_stream, stream) ||
                    !ReferenceEquals(_client, client) ||
                    _connectionGeneration != generation)
                {
                    throw new IOException("客户端连接已更换，本次报文未发送。");
                }
            }

            await stream.WriteAsync(data.AsMemory(), operationToken).ConfigureAwait(false);
            await stream.FlushAsync(operationToken).ConfigureAwait(false);
            // 仅在完整报文成功写入并刷新后通知观察者；失败/取消的写入不记为已发送。
            Raise(MessageSent, normalized);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <summary>
    /// 发送一条请求，并等待第一个满足谓词的返回报文。
    /// <para>与当前请求无关的报文会继续忽略；断线、取消和超时会抛异常。</para>
    /// </summary>
    public Task<string> SendAndWaitForResponseAsync(
        string request,
        Func<string, bool> responsePredicate,
        TimeSpan timeout,
        CancellationToken cancellationToken = default) =>
        SendAndWaitCoreAsync(
            request,
            response => responsePredicate(response)
                ? ResponseClassification.Success
                : ResponseClassification.Ignored,
            timeout,
            cancellationToken);

    /// <summary>兼容较短的方法名。</summary>
    public Task<string> SendAndWaitAsync(
        string request,
        Func<string, bool> responsePredicate,
        TimeSpan timeout,
        CancellationToken cancellationToken = default) =>
        SendAndWaitForResponseAsync(request, responsePredicate, timeout, cancellationToken);

    /// <summary>
    /// 按 <see cref="CommandResponseMatcher"/> 等待标准命令的最终 OK。
    /// 测量命令默认最多等待 600 秒，普通命令默认最多等待 10 秒。
    /// </summary>
    public Task<string> SendAndWaitForCompletionAsync(
        string request,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        string normalized = MessageProtocol.MigrateMeasurementRequest(request);
        if (!CommandResponseMatcher.SupportsRequest(normalized))
        {
            throw new NotSupportedException("当前报文没有内置的最终应答匹配规则。");
        }

        TimeSpan effectiveTimeout = timeout ?? CommandResponseMatcher.GetResponseTimeout(normalized);
        return SendAndWaitCoreAsync(
            normalized,
            response => CommandResponseMatcher.Classify(normalized, response),
            effectiveTimeout,
            cancellationToken);
    }

    /// <summary>允许以令牌作为第二个参数调用的重载。</summary>
    public Task<string> SendAndWaitForCompletionAsync(
        string request,
        CancellationToken cancellationToken) =>
        SendAndWaitForCompletionAsync(request, timeout: null, cancellationToken);

    /// <summary>
    /// 使用自定义分类器等待最终结果；返回 Failure 时抛出
    /// <see cref="DeviceResponseException"/>。
    /// </summary>
    public Task<string> SendAndWaitClassifiedAsync(
        string request,
        Func<string, ResponseClassification> classifier,
        TimeSpan timeout,
        CancellationToken cancellationToken = default) =>
        SendAndWaitCoreAsync(request, classifier, timeout, cancellationToken);

    /// <summary>停止监听并断开当前客户端；可在停止后重新 StartAsync。</summary>
    public Task StopAsync()
    {
        lock (_stateLock)
        {
            if (_stopTask is not null)
            {
                return _stopTask;
            }

            _stopTask = StopCoreAsync();
            return _stopTask;
        }
    }

    private async Task<string> SendAndWaitCoreAsync(
        string request,
        Func<string, ResponseClassification> classifier,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(classifier);
        string migratedRequest = MessageProtocol.MigrateMeasurementRequest(request);
        if (!MessageProtocol.TryValidateMessage(migratedRequest, out string normalized, out string validationError))
        {
            throw new FormatException(validationError);
        }

        ValidateTimeout(timeout);

        CancellationToken serverToken;
        lock (_stateLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            serverToken = _serverCancellation?.Token ?? CancellationToken.None;
        }

        using CancellationTokenSource operationCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, serverToken, _lifetimeCancellation.Token);
        CancellationToken operationToken = operationCancellation.Token;

        await _transactionLock.WaitAsync(operationToken).ConfigureAwait(false);
        try
        {
            var completion = new TaskCompletionSource<string>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            void OnMessage(object? _, string response)
            {
                ResponseClassification classification;
                try
                {
                    classification = classifier(response);
                }
                catch (Exception ex)
                {
                    completion.TrySetException(ex);
                    return;
                }

                switch (classification)
                {
                    case ResponseClassification.Success:
                        completion.TrySetResult(response);
                        break;
                    case ResponseClassification.Failure:
                        completion.TrySetException(new DeviceResponseException(normalized, response));
                        break;
                    // Ignored 包括 Run 中间状态，继续等待最终 OK/NG。
                }
            }

            void OnConnectionClosed(object? _, string reason) =>
                completion.TrySetException(new TcpConnectionClosedException(reason));

            // 必须先订阅再发送，避免设备快速返回时漏掉应答。
            MessageReceived += OnMessage;
            ConnectionClosed += OnConnectionClosed;
            try
            {
                await SendAsync(normalized, operationToken).ConfigureAwait(false);
                return await completion.Task.WaitAsync(timeout, operationToken).ConfigureAwait(false);
            }
            finally
            {
                MessageReceived -= OnMessage;
                ConnectionClosed -= OnConnectionClosed;
            }
        }
        finally
        {
            _transactionLock.Release();
        }
    }

    private async Task StopCoreAsync()
    {
        CancellationTokenSource? cancellation;
        TcpListener? listener;
        TcpClient? client;
        Task? acceptTask;
        CancellationTokenRegistration externalRegistration;

        lock (_stateLock)
        {
            cancellation = _serverCancellation;
            listener = _listener;
            client = _client;
            acceptTask = _acceptTask;
            externalRegistration = _externalCancellationRegistration;

            _serverCancellation = null;
            _listener = null;
            _client = null;
            _stream = null;
            _acceptTask = null;
            _remoteEndPoint = null;
            Port = 0;
            _externalCancellationRegistration = default;
        }

        // 先取消所有依赖服务端生命周期的发送/等待，再唤醒系统网络调用。
        cancellation?.Cancel();
        listener?.Stop();
        client?.Dispose();

        if (acceptTask is not null)
        {
            try
            {
                // StopAsync 一般由 UI 线程调用，不会与 AcceptLoop 处于同一个任务。
                await acceptTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // 主动停止监听。
            }
            catch (SocketException) when (cancellation?.IsCancellationRequested == true)
            {
                // listener.Stop() 用于唤醒 AcceptTcpClientAsync。
            }
        }

        externalRegistration.Dispose();
        cancellation?.Dispose();
    }

    /// <summary>CancellationToken 回调不能阻塞等待自身，因此只启动异步停止任务。</summary>
    private void RequestStopFromExternalCancellation()
    {
        _ = StopAsync();
    }

    private async Task AcceptLoopAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await listener.AcceptTcpClientAsync(cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (SocketException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    client.Dispose();
                    break;
                }

                client.NoDelay = true;
                NetworkStream stream;
                try
                {
                    stream = client.GetStream();
                }
                catch
                {
                    client.Dispose();
                    continue;
                }

                string remote = client.Client.RemoteEndPoint?.ToString() ?? "未知客户端";
                lock (_stateLock)
                {
                    if (!ReferenceEquals(_listener, listener) || cancellationToken.IsCancellationRequested)
                    {
                        stream.Dispose();
                        client.Dispose();
                        break;
                    }

                    _client = client;
                    _stream = stream;
                    _remoteEndPoint = client.Client.RemoteEndPoint;
                    _connectionGeneration++;
                }

                Raise(ClientConnected, remote);
                string? closeReason = await ReceiveLoopAsync(stream, cancellationToken)
                    .ConfigureAwait(false);

                lock (_stateLock)
                {
                    if (ReferenceEquals(_client, client))
                    {
                        _client = null;
                        _stream = null;
                        _remoteEndPoint = null;
                    }
                }

                stream.Dispose();
                client.Dispose();

                if (closeReason is not null && !cancellationToken.IsCancellationRequested)
                {
                    Raise(ConnectionClosed, closeReason);
                }
            }
        }
        finally
        {
            lock (_stateLock)
            {
                // 只有仍属于本次监听器的状态才清理，避免覆盖重启后的新监听。
                if (ReferenceEquals(_listener, listener))
                {
                    _listener = null;
                    _client = null;
                    _stream = null;
                    _remoteEndPoint = null;
                    _acceptTask = null;
                    Port = 0;
                }
            }
        }
    }

    private async Task<string?> ReceiveLoopAsync(
        NetworkStream stream,
        CancellationToken cancellationToken)
    {
        var parser = new MessageFrameParser(_maxPendingCharacters);
        try
        {
            using var reader = new StreamReader(
                stream,
                _encoding,
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 4096,
                leaveOpen: true);
            char[] buffer = new char[4096];

            while (!cancellationToken.IsCancellationRequested)
            {
                int count = await reader.ReadAsync(
                    buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
                if (count == 0)
                {
                    return "TCP 客户端已断开，服务器继续等待连接。";
                }

                IReadOnlyList<string> messages = parser.Append(buffer.AsSpan(0, count));
                foreach (string message in messages)
                {
                    Raise(MessageReceived, message);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 主动停止服务端，不报告为异常断线。
        }
        catch (ProtocolFrameTooLargeException ex)
        {
            Raise(ProtocolError, ex);
            return ex.Message;
        }
        catch (DecoderFallbackException ex)
        {
            Raise(ProtocolError, ex);
            return $"客户端发送了无效的 {_encoding.WebName} 字节：{ex.Message}";
        }
        catch (Exception ex)
        {
            return $"客户端连接已关闭：{ex.Message}";
        }

        return null;
    }

    private void Raise<T>(EventHandler<T>? handler, T value)
    {
        if (handler is null) return;

        // UI 事件处理器不应让后台接收循环因一个异常订阅者而退出。
        foreach (Delegate subscriber in handler.GetInvocationList())
        {
            try
            {
                ((EventHandler<T>)subscriber).Invoke(this, value);
            }
            catch
            {
                // 事件订阅者异常由订阅者自身记录；网络层保持可用。
            }
        }
    }

    private static void ValidatePort(int port)
    {
        if (port is < IPEndPoint.MinPort or > IPEndPoint.MaxPort)
        {
            throw new ArgumentOutOfRangeException(nameof(port), "端口必须在 0 到 65535 之间。");
        }
    }

    private static void ValidateTimeout(TimeSpan timeout)
    {
        if (timeout != Timeout.InfiniteTimeSpan && timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "超时必须大于 0，或使用 Infinite。");
        }
    }

    /// <summary>实现异步释放，取消进行中的发送/等待并释放套接字。</summary>
    public async ValueTask DisposeAsync()
    {
        lock (_stateLock)
        {
            if (_disposed) return;
            _disposed = true;
        }

        _lifetimeCancellation.Cancel();
        await StopAsync().ConfigureAwait(false);

        // 等待已进入发送/事务锁的操作退出后再释放同步对象。
        await _sendLock.WaitAsync().ConfigureAwait(false);
        _sendLock.Release();
        _sendLock.Dispose();

        await _transactionLock.WaitAsync().ConfigureAwait(false);
        _transactionLock.Release();
        _transactionLock.Dispose();
        _lifetimeCancellation.Dispose();
    }
}

/// <summary>对端在等待应答期间断开连接。</summary>
public sealed class TcpConnectionClosedException : IOException
{
    public TcpConnectionClosedException(string reason)
        : base(string.IsNullOrWhiteSpace(reason) ? "TCP 客户端已断开。" : reason)
    {
        Reason = reason;
    }

    public string Reason { get; }
}

/// <summary>设备返回与当前请求对应的失败终态。</summary>
public sealed class DeviceResponseException : InvalidOperationException
{
    public DeviceResponseException(string request, string response)
        : base($"设备返回失败或异常终态：{response}")
    {
        Request = request;
        Response = response;
    }

    public string Request { get; }

    public string Response { get; }
}
