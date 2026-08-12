// 本文件只负责 TCP 连接、收发和报文拆分，不包含任何界面逻辑。
// 初学者可以把它理解为：MainForm 通过这个类与测试设备通信。
using System.Net;
using System.Net.Sockets;
using System.Text;
using SimpleProtocolServer.Protocol;

namespace SimpleProtocolServer.Networking;

/// <summary>
/// 单客户端 TCP 服务器。
/// <para>服务器一次只服务一个客户端；客户端断开后会回到等待状态。</para>
/// <para>TCP 是字节流，读取一次不一定正好得到一条报文，因此本类还负责拆包和粘包处理。</para>
/// </summary>
internal sealed class TcpMessageServer : IAsyncDisposable
{
    // 防止恶意或错误客户端一直发送不完整报文，导致内存无限增长。
    private const int MaxPendingCharacters = 1024 * 1024;

    // 多个异步回调可能同时访问连接状态，读写这些字段前要先取得 _stateLock。
    private readonly object _stateLock = new();

    // 同一条 NetworkStream 不允许多次并发写入，否则不同报文的字节可能交叉。
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    // 下面字段分别代表监听器、当前客户端、网络流、停止信号和后台接收任务。
    private TcpListener? _listener;
    private TcpClient? _client;
    private NetworkStream? _stream;
    private CancellationTokenSource? _serverCancellation;
    private Task? _acceptTask;
    private bool _disposed;

    /// <summary>是否已经启动端口监听。</summary>
    public bool IsListening
    {
        get { lock (_stateLock) return _listener is not null; }
    }

    /// <summary>当前是否有测试设备连接。</summary>
    public bool IsConnected
    {
        get { lock (_stateLock) return _stream is not null; }
    }

    /// <summary>实际监听端口；传入端口 0 时系统会自动分配端口。</summary>
    public int Port { get; private set; }

    // 事件把后台网络变化通知给窗体。窗体收到事件后再更新日志和按钮状态。
    public event EventHandler<string>? ClientConnected;
    public event EventHandler<string>? MessageReceived;
    public event EventHandler<string>? ConnectionClosed;

    /// <summary>
    /// 启动 TCP 监听，并在后台进入 <see cref="AcceptLoopAsync"/> 等待客户端。
    /// </summary>
    public Task StartAsync(IPAddress address, int port)
    {
        lock (_stateLock)
        {
            // ThrowIf 能在对象已释放时尽早给出清晰异常。
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_listener is not null) throw new InvalidOperationException("TCP 服务器已在监听。");

            var listener = new TcpListener(address, port);
            // backlog=1：本工具只允许一个测试设备排队连接。
            listener.Start(1);

            _listener = listener;
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            _serverCancellation = new CancellationTokenSource();
            _acceptTask = AcceptLoopAsync(listener, _serverCancellation.Token);
        }

        return Task.CompletedTask;
    }

    /// <summary>把一条 UTF-8 报文发送给当前连接的测试设备。</summary>
    public async Task SendAsync(string message, CancellationToken cancellationToken = default)
    {
        NetworkStream stream;
        lock (_stateLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            stream = _stream ?? throw new InvalidOperationException("尚无 TCP 客户端连接。");
        }

        // 网络发送的是字节，所以先把 C# 字符串编码为 UTF-8。
        byte[] data = Encoding.UTF8.GetBytes(message);
        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            // 排队期间如果客户端已更换，不把旧报文发给新客户端。
            lock (_stateLock)
            {
                if (!ReferenceEquals(_stream, stream))
                {
                    throw new IOException("客户端连接已更换，本次报文未发送。");
                }
            }

            await stream.WriteAsync(data, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <summary>停止监听、断开客户端，并等待后台接收任务退出。</summary>
    public async Task StopAsync()
    {
        CancellationTokenSource? cancellation;
        TcpListener? listener;
        TcpClient? client;
        Task? acceptTask;

        lock (_stateLock)
        {
            // 先复制局部变量并清空共享状态，让其他线程立即看到“已停止”。
            cancellation = _serverCancellation;
            listener = _listener;
            client = _client;
            acceptTask = _acceptTask;

            _serverCancellation = null;
            _listener = null;
            _client = null;
            _stream = null;
            _acceptTask = null;
            Port = 0;
        }

        // Cancel 通知异步循环退出；Stop/Dispose 用于唤醒正在等待的系统调用。
        cancellation?.Cancel();
        listener?.Stop();
        client?.Dispose();

        if (acceptTask is not null)
        {
            try
            {
                await acceptTask;
            }
            catch (OperationCanceledException)
            {
                // 主动停止监听。
            }
            catch (SocketException) when (cancellation?.IsCancellationRequested == true)
            {
                // listener.Stop() 用于唤醒正在等待的 Accept。
            }
        }

        cancellation?.Dispose();
    }

    /// <summary>
    /// 循环接受客户端。一次连接结束后不会退出，而是继续等待下一次连接。
    /// </summary>
    private async Task AcceptLoopAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (SocketException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            // 禁用 Nagle 算法，让短控制报文尽快发出，减少测试等待时间。
            client.NoDelay = true;
            NetworkStream stream = client.GetStream();
            lock (_stateLock)
            {
                if (!ReferenceEquals(_listener, listener))
                {
                    stream.Dispose();
                    client.Dispose();
                    break;
                }

                _client = client;
                _stream = stream;
            }

            // ?.Invoke 表示“有人订阅事件才调用”，没有订阅者时不会报错。
            ClientConnected?.Invoke(this, client.Client.RemoteEndPoint?.ToString() ?? "未知客户端");
            string? closeReason = await ReceiveLoopAsync(stream, cancellationToken);

            lock (_stateLock)
            {
                if (ReferenceEquals(_client, client))
                {
                    _client = null;
                    _stream = null;
                }
            }

            stream.Dispose();
            client.Dispose();
            if (closeReason is not null) ConnectionClosed?.Invoke(this, closeReason);
        }
    }

    /// <summary>
    /// 从一个客户端持续读取字符，并把读取到的片段交给拆包方法处理。
    /// 返回值为断开原因；正常停止服务器时返回 null。
    /// </summary>
    private async Task<string?> ReceiveLoopAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        // pending 保存“还没有组成完整报文”的字符。
        var pending = new StringBuilder();

        try
        {
            using var reader = new StreamReader(
                stream, new UTF8Encoding(false), false, 1024, leaveOpen: true);
            var buffer = new char[1024];

            while (!cancellationToken.IsCancellationRequested)
            {
                // 一次 Read 可能只读到半条，也可能读到多条报文，这是 TCP 的正常行为。
                int count = await reader.ReadAsync(buffer.AsMemory(), cancellationToken);
                if (count == 0) return "TCP 客户端已断开，服务器继续等待连接。";

                pending.Append(buffer, 0, count);
                ExtractCompleteMessages(pending);
                if (pending.Length > MaxPendingCharacters)
                {
                    return "客户端报文超过 1 MB，连接已关闭。";
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 主动停止服务器。
        }
        catch (Exception ex)
        {
            return $"客户端连接已关闭：{ex.Message}";
        }

        return null;
    }

    /// <summary>
    /// 从缓存中反复提取 &amp;| 开头、|@ 结尾的完整报文。
    /// 完整报文通过 <see cref="MessageReceived"/> 事件交给上层。
    /// </summary>
    private void ExtractCompleteMessages(StringBuilder pending)
    {
        while (true)
        {
            string text = pending.ToString();
            int start = text.IndexOf(SimpleMessageProtocol.StartMarker, StringComparison.Ordinal);
            if (start < 0)
            {
                // 没有起始标记时丢弃垃圾字符；末尾单独的 '&' 可能是下一次 "&|" 的开头，要保留。
                pending.Clear();
                if (text.EndsWith('&')) pending.Append('&');
                return;
            }

            if (start > 0)
            {
                // 丢弃起始标记之前的无效字符，使 pending 从 &| 开始。
                pending.Remove(0, start);
                text = pending.ToString();
            }

            int end = text.IndexOf(SimpleMessageProtocol.EndMarker,
                SimpleMessageProtocol.StartMarker.Length, StringComparison.Ordinal);
            // 还没收到 |@，保留当前缓存，等待下一次网络读取。
            if (end < 0) return;

            int length = end + SimpleMessageProtocol.EndMarker.Length;
            string message = text[..length];
            pending.Remove(0, length);
            MessageReceived?.Invoke(this, message);
        }
    }

    /// <summary>实现异步释放：停止服务器，并确保没有发送任务仍占用发送锁。</summary>
    public async ValueTask DisposeAsync()
    {
        lock (_stateLock)
        {
            if (_disposed) return;
            _disposed = true;
        }

        await StopAsync();
        await _sendLock.WaitAsync();
        _sendLock.Release();
        _sendLock.Dispose();
    }
}
