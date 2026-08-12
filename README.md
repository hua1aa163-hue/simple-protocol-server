# Simple Protocol Server

用于按自定义报文协议接收、应答和定时发送消息的 .NET 8 Windows 桌面工具，并包含投影屏幕控制与冒烟检查。

## 构建

```powershell
dotnet build SimpleProtocolServer.sln -c Release
```

主程序位于 `SimpleProtocolServer/`，可复用设计与协议背景见 `PROJECT_REUSE_CONTEXT.md`。详细的 Codex 协作约定见 `AGENTS.md`。

