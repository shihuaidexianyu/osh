# OmenSuperHub Maintenance Map (CLI-only)

本仓库当前是 CLI-only 架构，维护时请以“命令层 -> 服务层 -> 硬件层”的方向进行。

## 依赖方向

`CLI -> App -> Services -> Hardware`

- `src/App`
  - CLI 入口与命令路由（`Program.cs`, `CliApp.cs`）
  - 强类型控制参数映射（`AppControlSettings.cs`）
- `src/App/Services`
  - 配置、遥测、硬件控制、自启动任务、外部命令执行
- `src/Hardware`
  - BIOS/WMI 交互与硬件模型
- `src/Core`
  - 纯控制逻辑（如 `PowerController`）

## 已移除模块（不要再作为修改入口）

- `src/UI/*`
- `AppRuntime*`
- `ShellStatusBuilder` / `AppShellService`
- `DashboardSnapshotBuilder` / `DashboardModels`

如果看到旧分支或历史文档还提到这些模块，请以当前代码树为准。

## 常见改动落点

- 新增 CLI 子命令：`src/App/CliApp.cs`
- 调整配置持久化：`src/App/Services/AppSettingsService.cs`
- 调整启动项行为：`src/App/Services/StartupTaskService.cs`
- 调整风扇曲线策略：`src/App/Services/FanCurveService.cs`
- 调整遥测解释逻辑：`src/App/Services/HardwareTelemetryService.cs`
- 调整硬件写入路径：`src/App/Services/HardwareControlService.cs`
- 修改 BIOS/WMI 协议细节：`src/Hardware/OmenHardwareGateway.cs`

## 维护守则

- 不要在 CLI 参数解析层直接写 BIOS/WMI，统一走 service。
- 新配置项优先扩展 `RuntimeControlSettings`（`AppControlSettings.cs`）语义，不要散落字符串常量。
- 功能变更优先补充/更新测试，再改实现。

## 测试范围

当前测试项目：`tests/OmenSuperHub.Tests`

主要覆盖：

- `RuntimeControlSettings`
- `SettingsRestoreService`
- `FanCurveService`
- `PowerController`

建议每次改动后运行：

```powershell
dotnet test tests/OmenSuperHub.Tests/OmenSuperHub.Tests.csproj -c Debug -p:Platform=x64
```
