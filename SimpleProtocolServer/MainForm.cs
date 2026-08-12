
// 真正的协议规则放在 Protocol 文件夹，网络细节放在 Networking 文件夹。
using System.Net;
using SimpleProtocolServer.Networking;
using SimpleProtocolServer.Projection;
using SimpleProtocolServer.Protocol;

namespace SimpleProtocolServer;

/// <summary>
/// 主界面：监听测试设备连接、生成/发送报文、维护循环列表，并打开投影窗口。
/// </summary>
public partial class MainForm : Form
{
    // 本工具固定只监听本机 127.0.0.1:9527。
    private const int ListenPort = 9527;

    // readonly 表示这些对象在窗体生命周期内始终使用同一个实例。
    private readonly TcpMessageServer _tcpServer = new();
    // 关闭主窗体时取消所有仍在执行的发送或等待任务。
    private readonly CancellationTokenSource _formCancellation = new();
    // 保存定时发送启动时的报文列表快照及“下一条”位置。
    private readonly CycleMessageSequence _cycleMessages = new();

    // 以下字段保存跨事件的运行状态。WinForms 每次点击/Timer Tick 都会进入不同方法，
    // 因此需要字段记住“当前是否正在运行”和相关取消信号。
    private CancellationTokenSource? _timedSendCancellation;
    private ProjectionForm? _projectionForm;
    private bool _isOneShotSending;
    private bool _isTimedSendRunning;
    private bool _isTimedSendSending;
    private bool _isClosing;

    /// <summary>创建主窗体，并订阅 TCP 服务的连接、收包和断线事件。</summary>
    public MainForm()
    {
        InitializeComponent();
        // 程序启动时默认选择“六、单次手动测试”；选择事件会同步生成对应参数状态和报文。
        // 这里只设置初始选择，不会锁定下拉框，用户之后仍可选择其他命令。
        cmbCommand.SelectedIndex = (int)CommandType.SingleManual;
        _tcpServer.ClientConnected += TcpServer_ClientConnected;
        _tcpServer.MessageReceived += TcpServer_MessageReceived;
        _tcpServer.ConnectionClosed += TcpServer_ConnectionClosed;
    }

    /// <summary>窗体首次显示时自动开始监听端口。</summary>
    private async void MainForm_Load(object? sender, EventArgs e)
    {
        await StartServerAsync();
    }

    /// <summary>“启动/停止监听”按钮：根据当前状态执行相反操作。</summary>
    private async void btnListen_Click(object? sender, EventArgs e)
    {
        btnListen.Enabled = false;
        try
        {
            if (_tcpServer.IsListening)
            {
                StopTimedSend("TCP 服务器停止监听，定时发送已停止。");
                await _tcpServer.StopAsync();
                if (_isClosing) return;

                SetServerState(false, false, "未监听");
                AppendLog("TCP 服务器已停止监听。");
            }
            else
            {
                await StartServerAsync();
            }
        }
        catch (Exception ex)
        {
            if (!_isClosing)
            {
                MessageBox.Show(this, ex.Message, "TCP 服务器",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        finally
        {
            if (!_isClosing) btnListen.Enabled = true;
        }
    }

    /// <summary>启动固定地址和端口的 TCP 服务，并同步更新界面状态。</summary>
    private async Task StartServerAsync()
    {
        btnListen.Enabled = false;
        SetServerState(false, false, "启动监听中...");
        try
        {
            await _tcpServer.StartAsync(IPAddress.Loopback, ListenPort);
            if (_isClosing) return;

            SetServerState(true, false, "等待客户端");
            AppendLog($"TCP 服务器正在监听 127.0.0.1:{ListenPort}");
        }
        catch (Exception ex)
        {
            if (_isClosing) return;

            SetServerState(false, false, "监听失败");
            MessageBox.Show(this, $"无法监听 127.0.0.1:{ListenPort}：\r\n{ex.Message}",
                "TCP 服务器", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            if (!_isClosing) btnListen.Enabled = true;
        }
    }

    /// <summary>命令下拉框改变后，刷新参数提示、预期返回和报文预览。</summary>
    private void cmbCommand_SelectedIndexChanged(object? sender, EventArgs e)
    {
        CommandType command = GetSelectedCommand();
        bool needsParameter = SimpleMessageProtocol.RequiresParameter(command);

        lblParameter.Text = SimpleMessageProtocol.GetParameterLabel(command);
        txtParameter.Enabled = needsParameter;
        txtParameter.Text = SimpleMessageProtocol.GetDefaultParameter(command);
        lblParameterHint.Text = needsParameter
            ? command == CommandType.GetFocalLength
                ? "输入大于 0 的 VID 整数"
                : "输入不含 |、&、@ 的配方名称"
            : "此命令不需要参数";
        txtExpectedResponse.Text = SimpleMessageProtocol.GetExpectedResponse(command);
        UpdatePreview();
    }

    /// <summary>参数每次编辑后立即重新生成报文预览。</summary>
    private void txtParameter_TextChanged(object? sender, EventArgs e) => UpdatePreview();

    /// <summary>把当前编辑框中的一条合法报文追加到循环列表。</summary>
    private void btnAddCycle_Click(object? sender, EventArgs e)
    {
        if (!TryGetCurrentMessage(out string message, showError: true)) return;

        int index = lstCycleMessages.Items.Add(message);
        lstCycleMessages.SelectedIndex = index;
        UpdateCycleHint();
    }

    /// <summary>从循环列表删除当前选中项，并尽量保留合理的选中位置。</summary>
    private void btnRemoveCycle_Click(object? sender, EventArgs e)
    {
        int index = lstCycleMessages.SelectedIndex;
        if (index < 0) return;

        lstCycleMessages.Items.RemoveAt(index);
        if (lstCycleMessages.Items.Count > 0)
        {
            lstCycleMessages.SelectedIndex = Math.Min(index, lstCycleMessages.Items.Count - 1);
        }
        UpdateCycleHint();
    }

    /// <summary>清空循环列表。</summary>
    private void btnClearCycle_Click(object? sender, EventArgs e)
    {
        lstCycleMessages.Items.Clear();
        UpdateCycleHint();
    }

    /// <summary>双击循环列表项时，把它回填到编辑框，便于查看或修改。</summary>
    private void lstCycleMessages_DoubleClick(object? sender, EventArgs e)
    {
        if (lstCycleMessages.SelectedItem is string message)
        {
            txtSendPreview.Text = message;
        }
    }

    /// <summary>显示循环列表当前条数和发送规则。</summary>
    private void UpdateCycleHint() =>
        lblCycleHint.Text = $"已添加 {lstCycleMessages.Items.Count} 条；定时发送将按顺序循环执行";

    /// <summary>“发送一次”按钮入口；事件处理器允许使用 async void。</summary>
    private async void btnSendOnce_Click(object? sender, EventArgs e) =>
        await SendCurrentMessageAsync();

    /// <summary>
    /// 只发送主界面当前报文；主界面的“发送一次”保持原有行为，不等待设备完成。
    /// 返回 true 表示字节已经成功写入网络流。
    /// </summary>
    private async Task<bool> SendCurrentMessageAsync()
    {
        if (!TryGetCurrentMessage(out string message, showError: true)) return false;
        if (!EnsureClientConnected()) return false;
        if (_isOneShotSending) return false;

        // 禁用按钮并设置标志，防止用户在上一次发送结束前重复发送。
        _isOneShotSending = true;
        btnSendOnce.Enabled = false;
        try
        {
            await _tcpServer.SendAsync(message, _formCancellation.Token);
            AppendLog($"发送：{message}");
            return true;
        }
        catch (OperationCanceledException) when (_isClosing)
        {
            // 窗口关闭时取消发送，不再弹出错误框。
            return false;
        }
        catch (Exception ex)
        {
            if (!_isClosing)
            {
                MessageBox.Show(this, ex.Message, "发送失败",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return false;
        }
        finally
        {
            _isOneShotSending = false;
            if (!_isClosing) btnSendOnce.Enabled = !_isTimedSendRunning;
        }
    }

    /// <summary>
    /// 串扰测试专用：发送当前报文并等待设备最终完成应答。
    /// 返回 false 时投影窗体会立即停止批量测试，不会切换下一张图片。
    /// </summary>
    private async Task<bool> SendCurrentMessageAndWaitForCompletionAsync(
        CancellationToken cancellationToken)
    {
        if (_isTimedSendRunning)
        {
            MessageBox.Show(this, "请先停止循环列表定时发送，再开始串扰测试。",
                "正在定时发送", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }

        if (_isOneShotSending)
        {
            MessageBox.Show(this, "当前已有报文正在发送或等待返回，请稍候。",
                "正在等待", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }

        if (!TryGetCurrentMessage(out string message, showError: true)) return false;
        if (!EnsureClientConnected()) return false;
        if (!CommandResponseMatcher.SupportsRequest(message))
        {
            MessageBox.Show(this,
                "当前报文没有可识别的完成返回规则，无法用于串扰测试。\r\n" +
                "请使用界面中的标准命令报文。",
                "无法确认完成", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        // TaskCompletionSource 把“以后由 TCP 事件到达的结果”包装成一个可 await 的 Task。
        var completion = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        // 本地函数只在本次请求有效，finally 中会取消订阅，避免影响下一次请求。
        void OnMessage(object? sender, string response)
        {
            switch (CommandResponseMatcher.Classify(message, response))
            {
                case ResponseClassification.Success:
                    completion.TrySetResult(response);
                    break;
                case ResponseClassification.Failure:
                    completion.TrySetException(new InvalidOperationException(
                        $"设备返回失败或异常终态：{response}"));
                    break;
            }
        }

        // 等待期间一旦断线，就让等待任务以异常结束。
        void OnConnectionClosed(object? sender, string reason) =>
            completion.TrySetException(new IOException(reason));

        // 必须在发送前订阅，防止设备快速返回时漏掉完成应答。
        _tcpServer.MessageReceived += OnMessage;
        _tcpServer.ConnectionClosed += OnConnectionClosed;
        _isOneShotSending = true;
        btnSendOnce.Enabled = false;
        // 任意一个窗口关闭/停止都会取消本次操作，因此将两个 Token 合并。
        using var operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            _formCancellation.Token, cancellationToken);
        CancellationToken operationToken = operationCancellation.Token;

        try
        {
            await _tcpServer.SendAsync(message, operationToken);
            AppendLog($"串扰测试发送：{message}");

            TimeSpan timeout = CommandResponseMatcher.GetResponseTimeout(message);
            AppendLog($"等待完成返回，超时 {timeout.TotalSeconds:0} 秒...");
            // WaitAsync 同时具备“超时”和“用户取消”两种退出方式。
            string completedResponse = await completion.Task.WaitAsync(
                timeout, operationToken);

            AppendLog($"已确认完成：{completedResponse}");
            return true;
        }
        catch (TimeoutException)
        {
            if (!_isClosing)
            {
                string error = "等待设备完成返回报文超时，已取消本次投图。";
                AppendLog(error);
                MessageBox.Show(this, error, "串扰测试超时",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            return false;
        }
        catch (OperationCanceledException) when (_isClosing)
        {
            return false;
        }
        catch (OperationCanceledException)
        {
            AppendLog("串扰测试已取消。");
            return false;
        }
        catch (Exception ex)
        {
            if (!_isClosing)
            {
                AppendLog($"串扰测试未完成：{ex.Message}");
                MessageBox.Show(this, ex.Message, "串扰测试未完成",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return false;
        }
        finally
        {
            _tcpServer.MessageReceived -= OnMessage;
            _tcpServer.ConnectionClosed -= OnConnectionClosed;
            _isOneShotSending = false;
            if (!_isClosing) btnSendOnce.Enabled = !_isTimedSendRunning;
        }
    }

    /// <summary>“开始/停止定时发送”按钮入口。</summary>
    private async void btnTimedSend_Click(object? sender, EventArgs e)
    {
        if (_isTimedSendRunning)
        {
            StopTimedSend("定时发送已停止。");
            return;
        }

        if (!EnsureClientConnected()) return;
        if (lstCycleMessages.Items.Count == 0)
        {
            MessageBox.Show(this, "请先把至少一条报文加入循环列表。",
                "循环列表为空", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        // 启动时复制列表；运行中界面列表会被禁用，序列内容保持稳定。
        _cycleMessages.Reset(lstCycleMessages.Items.Cast<string>());
        var sendCancellation = new CancellationTokenSource();
        _timedSendCancellation = sendCancellation;
        SetTimedSendState(true);
        AppendLog(
            $"开始定时发送：行间隔 {numRowIntervalSeconds.Value} 秒，循环间隔 {numIntervalMinutes.Value} 分钟。");

        // 启动后立即发送第一条，不需要先等一个间隔。
        bool sent = await SendNextCycleMessageAsync(sendCancellation.Token);

        // 旧的异步发送结束时，不能操作后来重新启动的计时器。
        if (!ReferenceEquals(_timedSendCancellation, sendCancellation)) return;

        if (!sent)
        {
            StopTimedSend("首次发送失败，定时发送已停止。");
            return;
        }

        timedSendTimer.Start();
    }

    /// <summary>
    /// WinForms Timer 到点后的入口：发送下一条，并从发送完成时重新开始计时。
    /// </summary>
    private async void timedSendTimer_Tick(object? sender, EventArgs e)
    {
        CancellationTokenSource? sendCancellation = _timedSendCancellation;
        if (!_isTimedSendRunning || sendCancellation is null || _isTimedSendSending) return;

        // 从上一条实际发送完成后开始计算下一次间隔，避免网络发送耗时挤占等待时间。
        timedSendTimer.Stop();
        _isTimedSendSending = true;
        try
        {
            bool sent = await SendNextCycleMessageAsync(sendCancellation.Token);
            if (!sent && ReferenceEquals(_timedSendCancellation, sendCancellation))
            {
                StopTimedSend("定时发送失败，定时发送已停止。");
            }
            else if (ReferenceEquals(_timedSendCancellation, sendCancellation))
            {
                timedSendTimer.Start();
            }
        }
        finally
        {
            if (ReferenceEquals(_timedSendCancellation, sendCancellation))
            {
                _isTimedSendSending = false;
            }
        }
    }

    /// <summary>从循环序列取出并发送下一条报文，成功返回 true。</summary>
    private async Task<bool> SendNextCycleMessageAsync(CancellationToken cancellationToken)
    {
        if (!_tcpServer.IsConnected) return false;
        if (!_cycleMessages.TryGetNext(out string message, out int position)) return false;

        try
        {
            await _tcpServer.SendAsync(message, cancellationToken);
            // 最后一条后使用“循环间隔”，普通相邻行之间使用“行间隔”。
            bool completedCycle = position == _cycleMessages.Count;
            timedSendTimer.Interval = CycleSendTiming.GetNextIntervalMilliseconds(
                position,
                _cycleMessages.Count,
                numRowIntervalSeconds.Value,
                numIntervalMinutes.Value);

            string nextDelay = completedCycle
                ? $"下一轮等待 {numIntervalMinutes.Value} 分钟"
                : $"下一行等待 {numRowIntervalSeconds.Value} 秒";
            AppendLog($"定时发送 [{position}/{_cycleMessages.Count}]：{message}；{nextDelay}");
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception ex)
        {
            if (!_isClosing) AppendLog($"定时发送失败：{ex.Message}");
            return false;
        }
    }

    /// <summary>停止计时器、取消未完成发送，并把相关控件恢复为可编辑状态。</summary>
    private void StopTimedSend(string? logMessage = null)
    {
        bool wasRunning = _isTimedSendRunning;
        timedSendTimer.Stop();
        _isTimedSendSending = false;

        CancellationTokenSource? cancellation = _timedSendCancellation;
        _timedSendCancellation = null;
        cancellation?.Cancel();
        cancellation?.Dispose();

        SetTimedSendState(false);
        if (wasRunning && !string.IsNullOrEmpty(logMessage)) AppendLog(logMessage);
    }

    /// <summary>统一切换定时发送期间所有按钮和输入框的启用状态。</summary>
    private void SetTimedSendState(bool running)
    {
        _isTimedSendRunning = running;
        btnTimedSend.Text = running ? "停止定时发送" : "开始定时发送";
        btnSendOnce.Enabled = !running;
        numIntervalMinutes.Enabled = !running;
        numRowIntervalSeconds.Enabled = !running;
        btnAddCycle.Enabled = !running;
        btnRemoveCycle.Enabled = !running;
        btnClearCycle.Enabled = !running;
        lstCycleMessages.Enabled = !running;
        lblSendState.Text = running
            ? $"发送中：{_cycleMessages.Count} 条，行 {numRowIntervalSeconds.Value} 秒，循环 {numIntervalMinutes.Value} 分"
            : "定时发送未启动";
        lblSendState.ForeColor = running ? Color.DarkGreen : Color.DimGray;
    }

    /// <summary>读取并校验发送编辑框；失败时可选择弹出提示。</summary>
    private bool TryGetCurrentMessage(out string message, bool showError)
    {
        bool success = SimpleMessageProtocol.TryValidateMessage(
            txtSendPreview.Text, out message, out string error);

        if (!success && showError)
        {
            MessageBox.Show(this, error, "参数检查",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            txtSendPreview.Focus();
        }
        return success;
    }

    /// <summary>根据当前命令和参数生成报文；校验失败时在预览框显示原因。</summary>
    private void UpdatePreview()
    {
        bool success = SimpleMessageProtocol.TryBuildMessage(
            GetSelectedCommand(), txtParameter.Text, out string message, out string error);
        txtSendPreview.Text = success ? message : error;
    }

    /// <summary>把下拉框索引转换为强类型命令枚举。</summary>
    private CommandType GetSelectedCommand() =>
        (CommandType)Math.Clamp(cmbCommand.SelectedIndex, 0, 7);

    /// <summary>需要发送前确认测试设备仍然连接；未连接时提示用户。</summary>
    private bool EnsureClientConnected()
    {
        if (_tcpServer.IsConnected) return true;

        MessageBox.Show(this, "尚无 TCP 客户端连接到 127.0.0.1:9527。",
            "尚未连接", MessageBoxButtons.OK, MessageBoxIcon.Information);
        return false;
    }

    /// <summary>TCP 后台线程通知有客户端连接。</summary>
    private void TcpServer_ClientConnected(object? sender, string remoteEndPoint) =>
        PostToUi(() =>
        {
            SetServerState(true, true, "客户端已连接");
            AppendLog($"客户端已连接：{remoteEndPoint}");
        });

    /// <summary>记录 TCP 服务拆出的每一条完整返回报文。</summary>
    private void TcpServer_MessageReceived(object? sender, string message) =>
        PostToUi(() => AppendLog($"接收：{message}"));

    /// <summary>断线时停止依赖连接的定时发送，并恢复“等待客户端”状态。</summary>
    private void TcpServer_ConnectionClosed(object? sender, string reason) =>
        PostToUi(() =>
        {
            StopTimedSend("客户端断开，定时发送已停止。");
            SetServerState(_tcpServer.IsListening, false,
                _tcpServer.IsListening ? "等待客户端" : "未监听");
            AppendLog(reason);
        });

    /// <summary>
    /// 安全地将 TCP 后台事件切回界面线程。
    /// WinForms 控件只能由创建它们的 UI 线程访问，后台线程直接改控件会抛异常。
    /// </summary>
    private void PostToUi(Action action)
    {
        if (_isClosing || IsDisposed || Disposing || !IsHandleCreated) return;

        try
        {
            // BeginInvoke 把操作放入 UI 消息队列，不阻塞当前网络线程。
            BeginInvoke(() =>
            {
                if (!_isClosing && !IsDisposed) action();
            });
        }
        catch (InvalidOperationException)
        {
            // 窗口正在关闭时忽略过期的界面更新。
        }
    }

    /// <summary>统一更新监听按钮、状态文字和颜色。</summary>
    private void SetServerState(bool listening, bool connected, string text)
    {
        lblStatus.Text = $"● {text}";
        lblStatus.ForeColor = connected ? Color.DarkGreen
            : listening ? Color.DarkOrange
            : Color.Maroon;
        btnListen.Text = listening ? "停止监听" : "启动监听";
    }

    /// <summary>在主日志末尾追加一行带时分秒的文本。</summary>
    private void AppendLog(string text) =>
        txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}");

    /// <summary>清空主界面 TCP 日志。</summary>
    private void btnClearLog_Click(object? sender, EventArgs e) => txtLog.Clear();

    /// <summary>打开或激活唯一的非模态投影窗口。</summary>
    private void btnProjection_Click(object? sender, EventArgs e)
    {
        if (_projectionForm is { IsDisposed: false })
        {
            if (_projectionForm.WindowState == FormWindowState.Minimized)
            {
                _projectionForm.WindowState = FormWindowState.Normal;
            }

            _projectionForm.BringToFront();
            _projectionForm.Activate();
            return;
        }

        _projectionForm = new ProjectionForm(SendCurrentMessageAndWaitForCompletionAsync);
        _projectionForm.FormClosed += ProjectionForm_FormClosed;

        // 不设置 Owner、不使用 ShowDialog：两个窗口相互独立，主界面仍可操作。
        _projectionForm.Show();
    }

    /// <summary>投影窗口关闭后清除字段引用，允许以后重新创建。</summary>
    private void ProjectionForm_FormClosed(object? sender, FormClosedEventArgs e)
    {
        if (sender is not ProjectionForm closedForm ||
            !ReferenceEquals(closedForm, _projectionForm)) return;

        closedForm.FormClosed -= ProjectionForm_FormClosed;
        _projectionForm = null;
    }

    /// <summary>主窗体即将关闭：先取消后台工作、关闭投影窗体并解除事件订阅。</summary>
    private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        _isClosing = true;
        _formCancellation.Cancel();
        StopTimedSend();
        if (_projectionForm is { IsDisposed: false })
        {
            _projectionForm.FormClosed -= ProjectionForm_FormClosed;
            _projectionForm.Close();
            _projectionForm = null;
        }
        _tcpServer.ClientConnected -= TcpServer_ClientConnected;
        _tcpServer.MessageReceived -= TcpServer_MessageReceived;
        _tcpServer.ConnectionClosed -= TcpServer_ConnectionClosed;
    }

    /// <summary>主窗体关闭后异步释放 TCP 服务占用的套接字和同步对象。</summary>
    private async void MainForm_FormClosed(object? sender, FormClosedEventArgs e)
    {
        try
        {
            await _tcpServer.DisposeAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"关闭 TCP 服务器失败：{ex.Message}");
        }
        finally
        {
            _formCancellation.Dispose();
        }
    }
}
