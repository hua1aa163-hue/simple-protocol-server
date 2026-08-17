# 报文定时发送与串扰测试——项目复用上下文

> 版本日期：2026-08-17
> 来源项目：`SimpleProtocolServer`（.NET 8 Windows Forms）  
> 用途：把本文件复制到其他项目根目录，交给开发人员或 Codex 阅读，即可了解已确认需求、协议规则、可复用模块和验收标准。

## 1. 最终目标

构建一个 Windows 桌面测试工具，包含以下能力：

1. 在 `127.0.0.1:9527` 作为单客户端 TCP 服务器监听测试设备。
2. 根据 8 种标准命令生成、校验、发送 `&|...|@` 格式报文。
3. 支持手工发送和循环列表定时发送。
4. 循环列表具有两种间隔：同一轮相邻行的“行间隔（秒）”和末行到下一轮首行的“循环间隔（分钟）”。
5. 支持 Windows 投影模式切换、图片切换、定时投图和投放列表中单击选中的图片。
6. 支持对投放到第二屏幕的图片进行横向显示、横向翻转、上下翻转等处理，不改变显示器本身方向。
7. 支持严格的整文件夹串扰测试：设备确认当前测试完成后，等待 1 秒，再投放下一张并发送下一次测试命令。
8. 主界面与投影界面是两个可同时操作的非模态窗口。
9. 所有界面器件都应在 WinForms Designer 中创建和编辑；业务代码不负责动态拼装界面。
10. 代码应有面向新手的中文注释，并把协议、网络、投影系统调用与窗体业务分层。

## 2. 已确认且不可误解的默认行为

- 程序启动时默认选择：`六、单次手动测试`。
- 对应枚举：`CommandType.SingleManual`。
- 对应默认发送报文：`&|Meas|A|M|@`。
- 此命令不需要参数，所以参数框为空且不可编辑。
- 预期返回提示：先可能收到 `&|Meas|A|M|Run|@`，最终成功报文为 `&|Meas|A|OK|@`。
- 默认选择只用于启动初始化，不能锁定命令下拉框；用户之后仍可选择其余 7 种命令。
- 投影窗口默认选择“扩展屏幕”。
- 第二屏图片效果默认选择“原图”，后续可选择横向显示、横向翻转、上下翻转或组合翻转。
- 行间隔默认 1 秒；循环间隔默认 1 分钟；投图间隔默认 5 秒。

## 3. 标准命令协议

每次只发送一条 UTF-8 报文。报文必须以 `&|` 开始，以 `|@` 结束。

| 序号 | 界面命令 | 枚举 | 默认参数 | 发送报文 |
|---|---|---|---|---|
| 1 | 停止 | `Stop` | 无 | `&|Stop|@` |
| 2 | 切换配方 | `SwitchRecipe` | `qwerty` | `&|Elems|C|qwerty|@` |
| 3 | 保存配方 | `SaveRecipe` | `asdfgh` | `&|Elems|S|asdfgh|@` |
| 4 | 获取焦距 FF | `GetFocalLength` | `7500` | `&|FF|7500|@` |
| 5 | 单次自动测试 | `SingleAutomatic` | 无 | `&|Meas|A|A|@` |
| 6 | 单次手动测试 | `SingleManual` | 无 | `&|Meas|A|M|@` |
| 7 | 循环自动测试 | `ContinuousAutomatic` | 无 | `&|Meas|S|A|@` |
| 8 | 循环手动测试 | `ContinuousManual` | 无 | `&|Meas|S|M|@` |

参数规则：

- 切换配方和保存配方必须提供非空配方名称。
- VID 必须是大于 0 的整数。
- 参数不能包含 `|`、`&`、`@`、回车或换行。
- 手工编辑的报文仍必须是一条完整的 `&|...|@` 报文，不能一次粘贴多条。

## 4. 返回报文确认规则

串扰测试不能把“发送成功”当作“设备测试完成”。必须发送后继续接收并判断最终返回：

| 请求类型 | 成功终态 | 中间状态 | 失败处理 |
|---|---|---|---|
| Stop | `&|Stop|OK|@` | 无 | 同类非 OK 返回视为失败 |
| Elems | `&|Elems|OK|@` | 无 | `NG` 或同类非 OK 返回视为失败 |
| FF | 合法 `&|FF|...|@` 且不含 NG | 无 | 含 NG 视为失败 |
| 单次测量 | `&|Meas|A|OK|@` | 以 `&|Meas|A|` 开头并以 `Run|@` 结尾 | 同类非 Run/OK 终态视为失败 |
| 连续测量 | `&|Meas|S|OK|@` | 以 `&|Meas|S|` 开头并以 `Run|@` 结尾 | 同类非 Run/OK 终态视为失败 |

- 测量命令等待最终返回的超时时间为 600 秒。
- 其他标准命令超时时间为 10 秒。
- 与当前请求无关的报文应忽略并继续等待。
- 连接断开、发送异常、失败终态、超时或取消都必须返回失败。

## 5. 串扰测试严格流程

测试从图片列表当前选中项开始；如果没有选中项，则从第一张开始。图片列表末尾可绕回开头，但每张图片在本轮中只能测试一次。

```text
投放起始图片
    ↓
发送当前主界面报文，让设备开始测试
    ↓
接收返回报文；Run 只代表运行中，继续等待
    ↓
收到匹配的最终 OK，标记当前图片完成
    ↓
是否还有未测试图片？──否──> 整轮完成并停止
    │是
    ↓
等待 1 秒
    ↓
投放下一张图片
    ↓
再次发送报文，重复上述过程
```

必须遵守的停止条件：

- 报文发送失败：立即停止，不投下一张。
- 收到匹配的 NG 或异常终态：立即停止，不投下一张。
- 等待最终返回超时：立即停止，不投下一张。
- TCP 断开：立即停止，不投下一张。
- 下一张投放失败：立即停止，也不发送下一条测试命令。
- 用户点击“停止串扰测试”：取消当前等待并恢复界面状态。

## 6. 循环列表定时发送规则

- 点击开始后立即发送列表第一行。
- 成功发送普通行后，等待“行间隔（秒）”再发送下一行。
- 成功发送最后一行后，等待“循环间隔（分钟）”，再回到第一行。
- 任何一次发送失败、客户端断开或停止监听，都要停止循环发送。
- 运行期间锁定列表增删、两个间隔输入和普通“发送一次”按钮，防止运行参数中途变化。

## 7. 投影与图片规则

- 支持的图片扩展名：PNG、BMP、JPG、JPEG。
- 图片列表单击选中后，“投放选中图片”只投放该行对应图片。
- 可手动投放下一张，也可按秒进行定时投图。
- Windows 投影模式包括：保持当前、仅电脑屏幕、复制、仅第二屏幕、扩展屏幕。
- 第二屏图片效果包括：原图、横向显示、横向翻转（左右镜像）、上下翻转（垂直镜像）、左右及上下翻转。
- “横向显示”只在源图为竖图时顺时针旋转 90°；源图已经是横图时保持不变。
- 图片效果只处理投放图片，不调用 Windows 显示方向接口，也不旋转第二块显示器。
- 预览、手动投图、定时投图、投放选中图片和串扰测试均使用当前选择的图片效果。
- 非 BMP 图片可转换到临时 BMP 缓存后再交给 Windows 壁纸接口。
- 用户勾选恢复选项时，停止或关闭投影窗口后恢复原桌面图片。
- 投影窗体必须使用 `Show()` 打开，不设置 `Owner`，不使用 `ShowDialog()`，从而保证主界面仍可点击。

## 8. 可直接迁移的模块

建议按以下顺序从来源项目复制；复制后统一调整命名空间：

| 模块 | 来源文件 | 作用 | 依赖 |
|---|---|---|---|
| 协议枚举 | `Protocol/CommandType.cs` | 定义 8 种命令及顺序 | 无 |
| 报文生成 | `Protocol/SimpleMessageProtocol.cs` | 参数默认值、校验、报文生成 | `CommandType` |
| 返回匹配 | `Protocol/CommandResponseMatcher.cs` | 判断 Run、OK、NG、超时 | 协议格式 |
| 循环序列 | `Protocol/CycleMessageSequence.cs` | 保存循环快照和下一行位置 | 无 |
| 双间隔计算 | `Protocol/CycleSendTiming.cs` | 决定下一次 Timer 间隔 | 无 |
| TCP 服务 | `Networking/TcpMessageServer.cs` | 单客户端监听、收发、拆包/粘包 | 协议起止标记 |
| 投影抽象 | `Projection/IDesktopDisplayService.cs` | 隔离窗体与 Windows API | `DisplayTopology` |
| 投影模式 | `Projection/DisplayTopology.cs` | 表示屏幕拓扑 | 无 |
| 图片效果 | `Projection/ProjectedImageTransform.cs` | 处理横向、左右镜像、上下镜像和组合翻转 | `System.Drawing` |
| Windows 实现 | `Projection/DesktopDisplayService.cs` | 切换投影模式、设置/恢复壁纸 | Windows API |
| 主窗体 | `MainForm.cs/.Designer.cs/.resx` | TCP、协议、循环发送协调 | 上述协议与网络模块 |
| 投影窗体 | `ProjectionForm.cs/.Designer.cs/.resx` | 图片列表、投图和串扰循环 | 投影服务与发送委托 |
| 冒烟测试 | `SimpleProtocolServer.SmokeTests/Program.cs` | 协议、网络、循环、界面验证 | 主项目 |

投影窗体与主窗体之间使用下面的异步委托作为边界：

```csharp
Func<CancellationToken, Task<bool>> sendCurrentMessageAndWaitForCompletionAsync
```

返回 `true` 表示设备已经返回匹配的最终完成报文；返回 `false` 表示必须停止串扰测试。

## 9. 迁移到其他 WinForms 项目的步骤

1. 目标项目使用 `net8.0-windows`，并启用 `<UseWindowsForms>true</UseWindowsForms>`。
2. 先复制 `Protocol` 和 `Networking` 模块，修改命名空间并运行协议/TCP 测试。
3. 需要投影功能时，再复制 `Projection` 模块及投影窗体三件套：`.cs`、`.Designer.cs`、`.resx`。
4. 在主窗体创建并订阅 `TcpMessageServer`，启动时监听 `IPAddress.Loopback` 和端口 `9527`。
5. 在主窗体构造函数的 `InitializeComponent()` 之后设置：

   ```csharp
   cmbCommand.SelectedIndex = (int)CommandType.SingleManual;
   ```

6. 命令选择事件统一调用 `SimpleMessageProtocol`，同步参数标签、是否可编辑、默认参数、预期返回和报文预览。
7. 使用 `ProjectionForm(SendCurrentMessageAndWaitForCompletionAsync)` 传入串扰测试发送委托。
8. 在投影窗体中提供原图、横向显示、横向翻转、上下翻转和组合翻转；通过 `IDesktopDisplayService.SetWallpaper` 的图片效果参数生成第二屏投图缓存。
9. 使用非模态 `_projectionForm.Show()` 打开投影窗体。
10. 把所有控件放在 `.Designer.cs` 中；业务 `.cs` 只编写事件和逻辑。
11. 在项目文件中为两个窗体保存 `SubType`、`DependentUpon` 和资源归属关系，确保 Visual Studio Designer 能识别。

## 10. 设计器约束

- `MainForm` 和 `ProjectionForm` 的控件必须声明在各自 `.Designer.cs` 中。
- 每个控件都要有稳定且可读的 `Name`，并挂载到窗体或容器控件树。
- `System.Windows.Forms.Timer` 应放入 `components` 容器。
- 不要在业务代码中运行时创建固定按钮、标签、输入框或列表，否则无法在设计器中调节。
- 不要把可视控件字段改为局部变量。
- 不要手写覆盖 `Dispose(bool disposing)`；保留 Designer 的组件释放逻辑。
- 修改布局后应分别打开两个窗体的 Visual Studio Designer，确认无加载错误。

## 11. 验收清单

迁移完成后至少验证以下内容：

- [ ] Release 编译为 0 个错误、0 个警告（SDK 预览提示不计作项目警告）。
- [ ] 启动默认选中“六、单次手动测试”。
- [ ] 默认报文为 `&|Meas|A|M|@`，之后仍能切换全部 8 个命令。
- [ ] 8 种命令均生成准确报文；无效参数被拒绝。
- [ ] TCP 能处理半包、粘包和无效前缀字符。
- [ ] 循环列表行间隔与循环间隔分别生效。
- [ ] 投影窗口打开后主窗口仍可点击。
- [ ] 投影窗口默认使用原图，并可预览、应用横向显示、横向翻转和上下翻转。
- [ ] 图片翻转只改变投放图片，不旋转第二块显示器本身。
- [ ] 单击图片列表后可以投放选中图片。
- [ ] 串扰测试收到 Run 后继续等待，收到最终 OK 后才进入下一张。
- [ ] OK 后严格等待 1 秒再投放下一张。
- [ ] 发送失败、NG、断线、超时或投图失败时不继续投下一张。
- [ ] 文件夹中所有图片各测试一次后自动结束。
- [ ] 两个窗体都能在 Visual Studio Designer 中打开并调节所有控件。

建议验证命令：

```powershell
dotnet build .\SimpleProtocolServer.sln -c Release --no-restore -p:UseSharedCompilation=false
dotnet run --project .\SimpleProtocolServer.SmokeTests\SimpleProtocolServer.SmokeTests.csproj -c Release --no-build --no-restore
```

## 12. 可直接交给其他项目 Codex 的任务说明

复制下面这段文字，并把本文件一并放入目标项目：

```text
请读取 PROJECT_REUSE_CONTEXT.md，把其中定义的“报文定时发送与串扰测试”能力迁移到当前项目。

要求：
1. 先检查当前项目结构和既有改动，复用现有代码，不覆盖无关内容。
2. 协议、TCP、投影系统调用和窗体业务必须分层。
3. 程序启动默认选择“六、单次手动测试”，默认报文为 &|Meas|A|M|@，但后续仍可选择其他命令。
4. 串扰测试严格执行：投放起始图片 → 发送报文 → 等待匹配的最终完成返回 → 等待 1 秒 → 投放下一张 → 再发送；Run 不是完成。
5. 发送失败、NG、断线、超时或投图失败时立即停止，不继续投图。
6. 投影窗口必须非模态且不设置 Owner，保证主界面可操作。
7. 投影窗口必须提供原图、横向显示、横向翻转、上下翻转和组合翻转；效果作用于第二屏投放图片，不改变显示器方向。
8. 所有固定控件必须可在 WinForms Designer 中编辑。
9. 为新手添加中文注释，并补充与修改内容相称的自动测试。
10. 完成后执行 Release 编译和冒烟测试，报告实际结果。
```

## 13. 来源项目中的当前实现位置

- 主程序：`SimpleProtocolServer/`
- 自动验证：`SimpleProtocolServer.SmokeTests/`
- 解决方案：`SimpleProtocolServer.sln`
- 项目使用说明：`SimpleProtocolServer/README.md`

本文件描述的是已经确认的最终需求。迁移时若目标项目的协议、端口或 UI 框架不同，应明确列出差异，再调整对应模块；不要静默改变串扰测试顺序和失败停止规则。
