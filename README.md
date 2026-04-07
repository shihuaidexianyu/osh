# osh (CLI)

`osh` 现已是一个 **CLI-only** 的 OMEN 硬件控制工具（无可视化界面）。

项目目标是：在指定硬件组合上稳定提供风扇、功耗与基础遥测能力，替代日常常用的 OGH 控制路径。

## 当前定位

- 目标机型：`Intel Core i9-13900HX + NVIDIA GeForce RTX 4060 Laptop GPU`
- 目标平台：`Windows x64`
- 目标框架：`.NET Framework 4.8`
- 程序形态：命令行工具（`osh.exe`）

## 功能概览

- 查询当前状态：温度/功耗/风扇/适配器/键盘类型等
- 查看当前配置：读取 `%LocalAppData%\OmenSuperHub\settings.json`
- 一键应用预设：`quiet / balanced / performance / max`
- 单项设置覆盖：风扇模式、风扇控制、风扇曲线、CPU/GPU 功率、GPU 锁频、自启动、OMEN 键行为

## 命令用法

> 以下示例均在项目根目录执行。

### 帮助

`osh.exe help`

### 查看状态

`osh.exe status`

### 查看配置

`osh.exe config`

### 应用预设

`osh.exe preset quiet`

可选值：`quiet | balanced | performance | max`

### 设置单项参数

`osh.exe set <key> <value>`

常用 key：

- `fan-mode`：`quiet|balanced|performance`
- `fan-control`：`auto|max|<RPM>`（如 `3300`）
- `fan-table`：`silent|default|performance|cool`
- `temp-sensitivity`：`realtime|low|normal|high`
- `cpu-power`：如 `90` 或 `max`
- `gpu-power`：`min|med|max`
- `gpu-clock`：如 `2100`
- `smart-power`：`on|off`
- `auto-start`：`on|off`
- `omen-key`：`default|custom|none`

## 配置与日志

- 配置文件：`%LocalAppData%\OmenSuperHub\settings.json`
- 错误日志：`%LocalAppData%\OmenSuperHub\logs\error.log`

## 构建

### 1) 还原依赖

```powershell
msbuild OmenSuperHub.sln /t:Restore /p:RestorePackagesConfig=true /v:minimal
```

### 2) 构建（Debug x64）

```powershell
dotnet build OmenSuperHub.sln -c Debug -p:Platform=x64
```

### 3) 运行测试

```powershell
dotnet test tests/OmenSuperHub.Tests/OmenSuperHub.Tests.csproj -c Debug -p:Platform=x64
```

## 运行注意事项

- 建议不要与 `OMEN Gaming Hub` 同时控制同一硬件接口。
- 某些功能依赖管理员权限/WMI/计划任务能力，系统策略可能影响表现。
- 这是面向特定硬件组合的工具，不是通用 OMEN SDK。

## 维护文档

维护与代码落点说明见：`docs/MAINTAINING.md`

## 来源与致谢

- Fork 来源：[breadeding/OmenSuperHub](https://github.com/breadeding/OmenSuperHub)
- [OmenMon](https://github.com/OmenMon/OmenMon)
- [OmenHwCtl](https://github.com/GeographicCone/OmenHwCtl)
- [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor)

## 免责声明

本项目不属于 HP 或 OMEN。品牌名称仅用于说明兼容目标。

本程序会直接访问硬件和系统接口。错误使用或与其他控制软件并行运行，可能导致异常功耗、读数异常或控制失效。请在理解风险的前提下使用。
