# 一键光学测试客户端（v0.1.1）

当前源码版本：`v0.1.1`。

这是一个 .NET 8 / C# WinForms EXE。程序自身是 TCP **服务端监听者**，MRTEST/测量设备作为 TCP 客户端连接到本机；发送的默认手动单次测量报文严格为：

```
&|Meas|A|M|@
```

首版不使用 HUD 客户端协议（例如 `c-`、`t1`、`bmp-` 等），仅使用监听端口后的 `&|...|@` 报文链。

发送 `&|Meas|A|M|@` 后，收到 `&|Meas|A|M|Run|@`（或带 `M|Run` 的回显）只视为中间状态，只有最终 `&|Meas|A|OK|@` 才算完成；`NG`、断线、取消和超时都会停止当前计划。测量响应最长等待 600 秒，普通控制命令（例如切换配方）默认等待 10 秒。旧版本配置中误保存的 `&|Meas|A|M|Run|@` 会在加载、编辑和发送入口自动迁移为新的请求；其他自定义报文不被改写。

## 首版功能

- 主界面采用“监听—路径—测试计划—手动报文—日志/结果”的布局，控件均在 `*.Designer.cs` 中声明，可继续用 Visual Studio WinForms 设计器调整。
- MRTEST 路径、ExportFile 路径、配方目录、图卡目录、结果目录、监听地址/端口、命令框、整套次数、项目次数和弹窗选项都会在退出时保存到 `%LOCALAPPDATA%\AutoTestClient\settings.json`，下次启动恢复。
- 支持“完整一键测试计划重复 N 轮”和“单个项目在一轮内重复 N 次”；默认均为 1。
- 主界面计划列表只有单击左侧复选框才改变启用状态；单击项目文字只选择该项目，便于在右侧编辑当前项目次数。日志达到 200 行时会批量移除最旧 50 行并保留最近 150 行，手动“清空日志”会同时清除内部缓冲。
- 测试项目可绑定 MRTEST 配方名、配方文件和固定图卡顺序。项目表中的“MRTEST配方”列可直接编辑；选择或更换配方文件时默认自动填充文件名（不含扩展名），必要时仍可按设备实际名称手工修正。配方管理器右侧提供鼠标选择配方/图卡、文件选择按钮和小图预览；绑定保存的是选中的实际路径，配方名仅作为发送给 MRTEST 的名称。FOV/对比度/色域以及勾选“弹窗序列”的项目会监视 MRTEST 确认窗口，按队列投影图片；确认动作依次尝试标准按钮、Enter 和安全的关闭回退，不读取提示文字。串扰勾选“弹窗序列”时采用专用逐图事务：每张图先投影，再发送一次测量请求，等待 Run 中间返回后确认/关闭弹窗，收到最终 OK 才进入下一张，不会把整组图卡交给一次命令消费。
- 弹窗监视器不再随程序启动后常驻：每条测量报文发送前启动并按当前 MRTEST 路径重新绑定 PID，收到最终响应、失败、取消或超时后立即停止；手动报文同样只在该次发送事务中监视。测试结束后的空闲窗口不会被继续操作。
- TCP 服务端实际成功发送的每条协议报文都会写入主界面的“发送：…”日志；设备返回的每条完整报文写入“收到：…”日志，便于逐条核对命令和最终响应。手动报文框中的历史默认请求也会在真正发送前迁移，日志中不会出现旧的 `M|Run` 请求。
- 串扰保留参考项目的完整流程：前景图按可配置起点循环、最后一张为本底；普通模式等待每张图的最终 OK 后再切下一张，弹窗模式则在每张命令事务内处理确认窗口；导出文件夹快照、稳定性检查、Brightness C 列合并、19×32 矩阵、边框/3% 异常值处理、Excel/CSV/热图输出均保留。每次重复都会分配独立结果目录并归档原始数据，主界面下拉框可分别查看同一计划产生的多批热图。
- 项目编辑器删除项目时只显示一次确认框，删除后按对象重排项目/图卡序号；通过下方绑定面板选择图卡或直接编辑路径时，会立即同步左侧图卡路径、当前绑定标签和预览，避免重绑事件覆盖新值。
- FOV、黑白对比度、亮度均匀性、色域已接入统一结果表，但字段位置尚未凭现场文件确认，因此显示“待确认”，不会伪造数值。后续只需在 `DataProcessing` 中增加对应处理器。

## 运行步骤

1. 先手动启动 `MRTest.exe`，再启动 `AutoTestClient.exe`，确认 MRTEST 路径和 `ExportFile` 路径正确。主界面的 MRTEST 路径框用于弹窗监视和进程校验，不负责启动程序。MRTEST 通常以管理员权限运行，因此客户端清单已设置为 `requireAdministrator`；首次启动会出现 UAC 提示，必须允许，否则 Windows UIPI 会阻止自动 Enter/确定/关闭弹窗。
2. 点击“启动监听”（默认 `127.0.0.1:9527`），再让 MRTEST/设备连接本机端口。状态栏显示“客户端已连接”后才可开始。
3. 点击“编辑测试项目”，逐项设置启用状态、顺序、配方名、项目次数、弹窗模式和图卡路径；在下方右侧绑定面板中可用鼠标选择实际配方文件和图卡并查看预览。串扰图卡必须为大于等于 3 的奇数，最后一张为本底。
4. 主界面设置整套次数、弹窗稳定时间、投影模式；点击“开始一键测试”。首版建议先只启用一个项目进行现场验证。
5. 结果会同时写入日志和下方结果表。串扰完成后，输出目录下会生成原始数据归档、对应表 CSV、串扰 Excel 和 PNG 热图。连续重复时每批结果使用不同的子目录；测试结束后可在“选择串扰结果”下拉框中切换批次，再点击“查看串扰热图”或打开对应结果目录。

首测建议：先只启用一个已准备好图卡和配方的项目；确认设备确实回传 `Run` 后再回传最终 `OK`。监视器在每条测量命令发送前绑定 MRTEST 的 PID，只扫描该实例，并按窗口层级排除主窗口；不依赖 `#32770` 类名或提示文字。发现确认窗口后会记录实际句柄、投影图卡和所采用的确认动作，测量事务结束后立即停止。

## 构建与发布

需要 Windows .NET 8 SDK：

```powershell
dotnet build AutoTestClient.sln -c Release
dotnet run --project AutoTestClient.SmokeTests\AutoTestClient.SmokeTests.csproj -c Release
dotnet publish AutoTestClient\AutoTestClient.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish\win-x64
```

发布目录中的 `AutoTestClient.exe` 可直接拷贝到测试机。此前已在真实 MRTEST 上完成黑白对比度和 FOV/9Point 弹窗、投图、BM_CLICK、Run→OK 与导出文件验收；`v0.1.1` 收紧监视生命周期并增加日志/计划列表回归测试。本轮在此基础上补充串扰逐图弹窗事务、重复结果隔离和编辑器重绑保护；串扰仍建议先用一轮/一次进行现场验收。

## 目录说明

- `AutoTestClient/Protocol`：`&|...|@` 拆包、命令生成和 Run→OK 应答状态机。
- `AutoTestClient/Networking`：单客户端 TCP 服务端、写锁、事务锁、断线/超时处理。
- `AutoTestClient/Monitoring`：MRTEST 确认窗口轮询、主窗口排除和按钮/Enter/关闭回退操作。
- `AutoTestClient/Workflow`：整套/项目重复、配方切换、固定弹窗队列、串扰顺序。
- `AutoTestClient/DataProcessing`：串扰完整算法和其他项目的字段预留入口。
- `AutoTestClient.SmokeTests`：不依赖 MRTEST 的协议、粘包、持久化和回环 TCP 冒烟测试。

## 在 Visual Studio 中调整界面

1. 安装 Visual Studio 2022 的“.NET 桌面开发”工作负载（需要 WinForms 设计器）。
2. 打开 `AutoTestClient.sln`，在解决方案资源管理器中展开 `AutoTestClient` 项目。
3. 双击 `DashboardForm.cs`（不要打开 `DashboardForm.Designer.cs`），然后切换到“设计”视图；右键窗体也可以选择“查看设计器”。
4. `RecipeManagerForm.cs` 和 `CrosstalkResultForm.cs` 同样可以直接打开设计器。它们带有仅供设计器使用的无参构造函数，不会读取现场配置或导出数据。
5. 文本框、按钮、标签、布局容器和结果表列都已声明为 Designer 字段。修改 `Text`、`Size`、`Dock`、`Anchor`、颜色等属性后保存即可；不要手动编辑 `*.Designer.cs` 中由设计器生成的代码。

项目已启用 `ForceDesignerDPIUnaware=true`，用于解决 150% 显示缩放下 WinForms 设计器把窗体缩成左上角小块的问题。若 Visual Studio 已经打开过旧的设计器标签，请先关闭这些标签并重新打开窗体；信息栏显示“此项目设置为在非 DPI 感知模式下打开 WinForms 设计器”即表示设置已生效。

主界面入口是 `AutoTestClient/DashboardForm.cs`，对应布局文件是 `AutoTestClient/DashboardForm.Designer.cs`。运行时配置仍会从 `%LOCALAPPDATA%\AutoTestClient\settings.json` 载入并在退出时保存，所以设计器中的路径示例不会覆盖现场上次保存的值。
