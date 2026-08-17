// 这是一个无需第三方测试框架的“烟雾测试”控制台程序。
// 任意 Assert 失败都会抛出异常并返回非零退出码，全部通过时在最后输出成功信息。
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using SimpleProtocolServer;
using SimpleProtocolServer.Networking;
using SimpleProtocolServer.Projection;
using SimpleProtocolServer.Protocol;

// 第一组：八种界面命令都必须生成协议约定的准确字符串。
var protocolCases = new (CommandType Command, string Parameter, string Expected)[]
{
    (CommandType.Stop, "", "&|Stop|@"),
    (CommandType.SwitchRecipe, "qwerty", "&|Elems|C|qwerty|@"),
    (CommandType.SaveRecipe, "asdfgh", "&|Elems|S|asdfgh|@"),
    (CommandType.GetFocalLength, "7500", "&|FF|7500|@"),
    (CommandType.SingleAutomatic, "", "&|Meas|A|A|@"),
    (CommandType.SingleManual, "", "&|Meas|A|M|@"),
    (CommandType.ContinuousAutomatic, "", "&|Meas|S|A|@"),
    (CommandType.ContinuousManual, "", "&|Meas|S|M|@")
};

foreach ((CommandType command, string parameter, string expected) in protocolCases)
{
    // 元组让每个测试用例同时携带输入命令、输入参数和期望输出。
    Assert(SimpleMessageProtocol.TryBuildMessage(command, parameter, out string message, out _) &&
           message == expected,
        $"报文生成不正确：{command}");
}

// 第二组：错误参数和一次粘贴多条报文必须被拒绝。
Assert(!SimpleMessageProtocol.TryBuildMessage(CommandType.SwitchRecipe, "", out _, out _),
    "切换配方时仍允许空配方名");
Assert(!SimpleMessageProtocol.TryBuildMessage(CommandType.SaveRecipe, "bad|name", out _, out _),
    "配方名称仍允许使用协议保留字符");
Assert(!SimpleMessageProtocol.TryBuildMessage(CommandType.GetFocalLength, "0", out _, out _),
    "VID 仍允许非正整数");
Assert(SimpleMessageProtocol.TryValidateMessage("&|Custom|123|@", out string editable, out _) &&
       editable == "&|Custom|123|@",
    "手工编辑的完整报文无法发送");
Assert(!SimpleMessageProtocol.TryValidateMessage("&|Stop|@&|FF|7500|@", out _, out _),
    "发送框仍允许同时填入多条报文");

// 第三组：串扰流程要忽略 Run 中间态，只在最终 OK 时继续，并在 NG 时停止。
Assert(CommandResponseMatcher.Classify("&|Stop|@", "&|Stop|OK|@") ==
           ResponseClassification.Success &&
       CommandResponseMatcher.Classify("&|Elems|C|qwerty|@", "&|Elems|NG|@") ==
           ResponseClassification.Failure &&
       CommandResponseMatcher.Classify("&|Meas|A|A|@", "&|Meas|A|A|Run|@") ==
           ResponseClassification.Ignored &&
       CommandResponseMatcher.Classify("&|Meas|A|A|@", "&|Meas|A|OK|@") ==
           ResponseClassification.Success &&
       CommandResponseMatcher.Classify("&|Meas|A|A|@", "&|Meas|A|NG|Error|@") ==
           ResponseClassification.Failure,
    "串扰测试返回报文没有正确区分中间 Run、最终 OK 和失败 NG");

// 普通命令和测量命令耗时差异很大，因此使用不同超时时间。
Assert(CommandResponseMatcher.GetResponseTimeout("&|Stop|@") == TimeSpan.FromSeconds(10) &&
       CommandResponseMatcher.GetResponseTimeout("&|Meas|A|A|@") == TimeSpan.FromSeconds(600),
    "普通命令与测量命令的返回超时时间不正确");

// 从列表中间开始时应绕回开头，但每张图片只能出现一次。
Assert(ProjectionForm.BuildImageTestOrder(2, 4).SequenceEqual([2, 3, 0, 1]),
    "串扰测试没有从选中图片开始将文件夹图片各测试一次");

// 图片效果处理的是第二屏幕上的图片，不会改变显示器本身的方向。
using (var portraitImage = new Bitmap(2, 3))
{
    ProjectedImageTransformer.Apply(portraitImage, ProjectedImageTransform.Landscape);
    Assert(portraitImage.Width == 3 && portraitImage.Height == 2,
        "横向显示没有把竖图旋转为横图");
}

using (var horizontalImage = new Bitmap(2, 1))
{
    horizontalImage.SetPixel(0, 0, Color.Red);
    horizontalImage.SetPixel(1, 0, Color.Blue);
    ProjectedImageTransformer.Apply(horizontalImage, ProjectedImageTransform.FlipHorizontal);
    Assert(horizontalImage.GetPixel(0, 0).ToArgb() == Color.Blue.ToArgb() &&
           horizontalImage.GetPixel(1, 0).ToArgb() == Color.Red.ToArgb(),
        "横向翻转没有把图片左右对调");
}

using (var verticalImage = new Bitmap(1, 2))
{
    verticalImage.SetPixel(0, 0, Color.Red);
    verticalImage.SetPixel(0, 1, Color.Blue);
    ProjectedImageTransformer.Apply(verticalImage, ProjectedImageTransform.FlipVertical);
    Assert(verticalImage.GetPixel(0, 0).ToArgb() == Color.Blue.ToArgb() &&
           verticalImage.GetPixel(0, 1).ToArgb() == Color.Red.ToArgb(),
        "上下翻转没有把图片上下对调");
}

// 第二屏选择必须忽略主屏；只有主屏时返回 -1，防止误把全屏窗口盖到控制界面。
Assert(SecondScreenProjectionForm.FindSecondScreenIndex([true, false, false]) == 1 &&
       SecondScreenProjectionForm.FindSecondScreenIndex([true]) == -1,
    "第二屏选择规则没有优先使用非主屏或没有正确拒绝单屏环境");

// 1:1 模式只计算整数像素落点：小图居中留黑边，大图居中后以负坐标裁切。
Assert(PixelPerfectImageControl.CalculateImageLocation(
           new Size(1920, 1080), new Size(640, 480)) == new Point(640, 300) &&
       PixelPerfectImageControl.CalculateImageLocation(
           new Size(1920, 1080), new Size(3840, 2160)) == new Point(-960, -540),
    "像素对像素投图的居中或裁切坐标不正确");

// 第四组：循环报文走到末尾后必须重新从第一条开始。
var cycle = new CycleMessageSequence();
cycle.Reset(["&|Stop|@", "&|FF|7500|@"]);
Assert(cycle.TryGetNext(out string cycle1, out int position1) &&
       cycle.TryGetNext(out string cycle2, out int position2) &&
       cycle.TryGetNext(out string cycle3, out int position3) &&
       (cycle1, position1) == ("&|Stop|@", 1) &&
       (cycle2, position2) == ("&|FF|7500|@", 2) &&
       (cycle3, position3) == ("&|Stop|@", 1),
    "不同报文没有按列表顺序循环");

// 第一行后等 3 秒；最后一行后按 0.5 分钟，也就是 30 秒。
Assert(CycleSendTiming.GetNextIntervalMilliseconds(1, 2, 3m, 0.5m) == 3_000 &&
       CycleSendTiming.GetNextIntervalMilliseconds(2, 2, 3m, 0.5m) == 30_000,
    "循环列表没有区分行间隔与末行后的循环间隔");

// 第五组：启动真实的本机 TCP 服务器和客户端，验证双向收发。
await using var server = new TcpMessageServer();
// TaskCompletionSource 让事件可以被 await，并给测试设置明确的超时时间。
var connected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
var received = new List<string>();
var receivedTwice = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
server.ClientConnected += (_, _) => connected.TrySetResult();
server.MessageReceived += (_, message) =>
{
    received.Add(message);
    if (received.Count == 2) receivedTwice.TrySetResult();
};

// 端口 0 表示让操作系统分配一个当前空闲端口，避免测试与正式 9527 冲突。
await server.StartAsync(IPAddress.Loopback, 0);
using var client = new TcpClient();
await client.ConnectAsync(IPAddress.Loopback, server.Port);
await connected.Task.WaitAsync(TimeSpan.FromSeconds(3));
using NetworkStream stream = client.GetStream();

// 先验证服务器发给客户端的数据字节完全正确。
string outgoing = "&|Stop|@";
await server.SendAsync(outgoing);
byte[] readBuffer = new byte[Encoding.UTF8.GetByteCount(outgoing)];
int totalRead = 0;
while (totalRead < readBuffer.Length)
{
    int count = await stream.ReadAsync(readBuffer.AsMemory(totalRead));
    if (count == 0) break;
    totalRead += count;
}
Assert(Encoding.UTF8.GetString(readBuffer, 0, totalRead) == outgoing,
    "TCP 服务器发送的报文不正确");

// 两条返回报文分三段发送，同时验证拆包与粘包。
string response1 = "&|Stop|OK|@";
string response2 = "&|Meas|A|A|Run|@";
byte[] combined = Encoding.UTF8.GetBytes(response1 + response2);
await stream.WriteAsync(combined.AsMemory(0, 5));
await stream.WriteAsync(combined.AsMemory(5, 11));
await stream.WriteAsync(combined.AsMemory(16));
await receivedTwice.Task.WaitAsync(TimeSpan.FromSeconds(3));
Assert(received.SequenceEqual([response1, response2]), "TCP 拆包或粘包处理不正确");
await server.StopAsync();

// 第六组：WinForms 控件必须在 STA 线程创建，因此单独启动一个 STA 测试线程。
Exception? designerException = null;
var designerThread = new Thread(() =>
{
    try
    {
        // 不显示窗口，只构造窗体并递归查找控件，验证设计器默认值和事件联动。
        using var form = new MainForm();
        AssertDesignerFieldsAttached(form);
        var host = FindControl<TextBox>(form, "txtHost");
        var port = FindControl<NumericUpDown>(form, "numPort");
        var commands = FindControl<ComboBox>(form, "cmbCommand");
        var parameterLabel = FindControl<Label>(form, "lblParameter");
        var parameter = FindControl<TextBox>(form, "txtParameter");
        var preview = FindControl<TextBox>(form, "txtSendPreview");
        var expectedResponse = FindControl<TextBox>(form, "txtExpectedResponse");
        var cycleMessages = FindControl<ListBox>(form, "lstCycleMessages");
        var addCycle = FindControl<Button>(form, "btnAddCycle");
        var cycleInterval = FindControl<NumericUpDown>(form, "numIntervalMinutes");
        var rowInterval = FindControl<NumericUpDown>(form, "numRowIntervalSeconds");
        var timedButton = FindControl<Button>(form, "btnTimedSend");
        var projectionButton = FindControl<Button>(form, "btnProjection");

        Assert(host.Text == "127.0.0.1" && host.ReadOnly && port.Value == 9527 && port.ReadOnly,
            "服务器界面没有固定为 127.0.0.1:9527");
        Assert(commands.Items.Count == 8 && !preview.ReadOnly &&
               cycleMessages.Items.Count == 0 && addCycle.Text == "加入循环列表" &&
               cycleInterval.Value == 1 && rowInterval.Value == 1 &&
               timedButton.Text == "开始定时发送" && projectionButton.Text == "扩展投影切图",
            "界面缺少可编辑发送框、循环列表、双间隔或投影入口控件");

        Assert(commands.SelectedIndex == (int)CommandType.SingleManual &&
               parameterLabel.Text == SimpleMessageProtocol.GetParameterLabel(CommandType.SingleManual) &&
               parameter.Text == SimpleMessageProtocol.GetDefaultParameter(CommandType.SingleManual) &&
               parameter.Enabled == SimpleMessageProtocol.RequiresParameter(CommandType.SingleManual) &&
               expectedResponse.Text == SimpleMessageProtocol.GetExpectedResponse(CommandType.SingleManual) &&
               preview.Text == "&|Meas|A|M|@",
            "程序启动时没有默认选择单次手动测试或生成对应报文");

        // 默认选择不锁定下拉框，之后选择其他命令仍然会正常联动。
        commands.SelectedIndex = (int)CommandType.SwitchRecipe;
        Assert(parameter.Text == "qwerty" && preview.Text == "&|Elems|C|qwerty|@",
            "选择切换配方后没有实时生成报文");

        using var projectionForm = new ProjectionForm();
        AssertDesignerFieldsAttached(projectionForm);
        var topology = FindControl<ComboBox>(projectionForm, "cmbTopology");
        var imageTransform = FindControl<ComboBox>(projectionForm, "cmbImageTransform");
        var applyImageTransform = FindControl<Button>(projectionForm, "btnApplyImageTransform");
        var closeSecondScreen = FindControl<CheckBox>(
            projectionForm, "chkCloseSecondScreenOnStop");
        var projectionInterval = FindControl<NumericUpDown>(
            projectionForm, "numProjectionIntervalSeconds");
        var timedProjection = FindControl<Button>(projectionForm, "btnTimedProjection");
        var projectSelected = FindControl<Button>(projectionForm, "btnProjectSelected");
        Assert(topology.SelectedIndex == 4 && topology.Text == "扩展屏幕" &&
               imageTransform.Items.Count == 5 && imageTransform.SelectedIndex == 0 &&
               imageTransform.Text == "原图" && applyImageTransform.Text == "应用图片效果" &&
               closeSecondScreen.Checked &&
               closeSecondScreen.Text == "停止定时投图时关闭第二屏" &&
               projectionInterval.Value == 5 && timedProjection.Text == "开始定时投图" &&
               projectSelected.Text == "投放选中图片",
            "投影窗口缺少扩展屏幕、图片翻转、切图间隔、定时投图或选中图片投放设置");

        // 默认原图不会锁定，用户之后仍可选择横向翻转和上下翻转。
        imageTransform.SelectedIndex = 2;
        Assert(imageTransform.Text == "横向翻转（左右镜像）",
            "投影窗口无法选择横向翻转");
        imageTransform.SelectedIndex = 3;
        Assert(imageTransform.Text == "上下翻转（垂直镜像）",
            "投影窗口无法选择上下翻转");

        using var secondScreenForm = new SecondScreenProjectionForm();
        AssertDesignerFieldsAttached(secondScreenForm);
        var pixelCanvas = FindControl<PixelPerfectImageControl>(
            secondScreenForm, "pixelPerfectCanvas");
        Assert(secondScreenForm.FormBorderStyle == FormBorderStyle.None &&
               secondScreenForm.TopMost && !secondScreenForm.ShowInTaskbar &&
               secondScreenForm.AutoScaleMode == AutoScaleMode.None &&
               pixelCanvas.Dock == DockStyle.Fill &&
               pixelCanvas.BackColor == Color.Black,
            "第二屏投图窗口不是无边框置顶的像素对像素画布");

        // 把 2×2 原图画到 4×4 画布，四个源像素必须原样出现在中央 2×2，周围保持黑色。
        var sourcePixels = new Bitmap(2, 2);
        sourcePixels.SetPixel(0, 0, Color.Red);
        sourcePixels.SetPixel(1, 0, Color.Green);
        sourcePixels.SetPixel(0, 1, Color.Blue);
        sourcePixels.SetPixel(1, 1, Color.White);
        pixelCanvas.Size = new Size(4, 4);
        pixelCanvas.ReplaceImage(sourcePixels);
        using var renderedPixels = new Bitmap(4, 4);
        using (Graphics renderedGraphics = Graphics.FromImage(renderedPixels))
        {
            pixelCanvas.RenderPixelPerfect(renderedGraphics, renderedPixels.Size);
        }
        Assert(renderedPixels.GetPixel(0, 0).ToArgb() == Color.Black.ToArgb() &&
               renderedPixels.GetPixel(1, 1).ToArgb() == Color.Red.ToArgb() &&
               renderedPixels.GetPixel(2, 1).ToArgb() == Color.Green.ToArgb() &&
               renderedPixels.GetPixel(1, 2).ToArgb() == Color.Blue.ToArgb() &&
               renderedPixels.GetPixel(2, 2).ToArgb() == Color.White.ToArgb(),
            "第二屏画布没有按一个源像素对应一个屏幕像素绘制");
    }
    catch (Exception ex)
    {
        designerException = ex;
    }
});
designerThread.SetApartmentState(ApartmentState.STA);
designerThread.Start();
designerThread.Join();
if (designerException is not null) throw designerException;

Console.WriteLine("SimpleProtocolServer smoke tests passed: protocol + response confirmation + cycle + TCP + second-screen projection UI.");

// 最小断言工具：条件不成立就终止测试并说明原因。
static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

// 递归遍历控件树，按 Name 查找指定类型的控件。
static T FindControl<T>(Control root, string name) where T : Control
{
    if (root is T match && root.Name == name) return match;
    foreach (Control child in root.Controls)
    {
        try
        {
            return FindControl<T>(child, name);
        }
        catch (InvalidOperationException)
        {
            // 继续查找其他子控件。
        }
    }

    throw new InvalidOperationException($"未找到控件：{name}");
}

// 检查 Designer 声明的控件字段不是 null，并且确实挂在窗体、ListView 列集合或组件容器中。
// 约定：手写状态字段使用下划线开头；Designer 控件字段不以下划线开头。
static void AssertDesignerFieldsAttached(Form form)
{
    List<Control> controls = EnumerateControls(form).ToList();
    List<ColumnHeader> columns = controls
        .OfType<ListView>()
        .SelectMany(list => list.Columns.Cast<ColumnHeader>())
        .ToList();

    FieldInfo[] designerFields = form.GetType()
        .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
        .Where(field => !field.Name.StartsWith('_') &&
                        (typeof(Control).IsAssignableFrom(field.FieldType) ||
                         field.FieldType == typeof(ColumnHeader) ||
                         field.FieldType == typeof(System.Windows.Forms.Timer)))
        .ToArray();

    foreach (FieldInfo field in designerFields)
    {
        object? value = field.GetValue(form);
        Assert(value is not null, $"{form.Name} 的 Designer 字段未初始化：{field.Name}");

        bool attached = value switch
        {
            Control control => controls.Contains(control),
            ColumnHeader column => columns.Contains(column),
            System.Windows.Forms.Timer => true,
            _ => false
        };
        Assert(attached, $"{form.Name} 的 Designer 器件未挂载：{field.Name}");
    }
}

// 深度优先遍历窗体及所有容器控件，让嵌套在 GroupBox 中的控件也能被检查到。
static IEnumerable<Control> EnumerateControls(Control root)
{
    yield return root;
    foreach (Control child in root.Controls)
    {
        foreach (Control descendant in EnumerateControls(child))
        {
            yield return descendant;
        }
    }
}
