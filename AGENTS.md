# Codex 协作说明

## 项目入口

- 解决方案：`SimpleProtocolServer.sln`
- 主程序：`SimpleProtocolServer/SimpleProtocolServer.csproj`
- 冒烟检查：`SimpleProtocolServer.SmokeTests/SimpleProtocolServer.SmokeTests.csproj`
- 构建：`dotnet build SimpleProtocolServer.sln -c Release`

## 修改约定

- `main` 保持可构建；日常修改默认使用 `codex/<任务名>` 分支。
- 提交应小而清晰，提交前至少运行一次 Release 构建。
- 不提交 `bin`、`obj`、`.vs`、用户配置、密钥、日志或发布产物。
- 网络、投影屏幕和外部设备相关功能无法仅靠离线构建完全验证，交付时说明实际验证范围。
- 协议有变化时，同时更新 `PROJECT_REUSE_CONTEXT.md` 和相关 README。
