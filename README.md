# OmenSuperHub

`OmenSuperHub` 是一个面向单一目标硬件的本地控制工具，用来替代 `OMEN Gaming Hub` 的核心功能：风扇控制、CPU/GPU 功率控制、DB 驱动切换、OMEN 键自定义，以及温度/功率监控。

## 当前目标

- 机型定位：`Intel Core i9-13900HX + NVIDIA GeForce RTX 4060 Laptop GPU`
- 平台要求：Windows、`.NET Framework 4.8`、`x64`
- 项目现状：代码和文档都按这套 Intel + NVIDIA 组合收敛，不再尝试覆盖 AMD CPU、AMD dGPU 或更广泛的 OMEN 兼容性矩阵

## 主要能力

- 通过 HP BIOS WMI 读写 OMEN 专有状态
- 通过 `LibreHardwareMonitor-pawnio` 读取 Intel CPU 遥测
- 通过 `NVAPI` / `NVML` 读取 NVIDIA dGPU 温度和功耗
- 通过任务栏和浮窗显示实时状态
- 通过计划任务实现管理员权限自启动

## 构建

首次构建或本地缺少 `packages` 目录时，先恢复 `packages.config` 依赖：

```powershell
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" OmenSuperHub.sln /t:Restore /p:RestorePackagesConfig=true /v:minimal
```

然后执行 `x64 Release` 构建：

```powershell
dotnet build OmenSuperHub.sln -c Release -p:Platform=x64
```

输出文件位于：

```text
bin\x64\Release\OmenSuperHub.exe
```

仓库也提供了 GitHub Actions 自动构建，工作流见 `.github/workflows/build-exe.yml`。每次 `push`、`pull_request` 或手动触发后，都可以在对应的 Actions 运行里下载 `OmenSuperHub-windows-x64-release` 构建产物。

如果正在运行 `OmenSuperHub.exe`，重建前需要先关闭它，否则输出文件可能被占用。

## 运行说明

- 启动前建议关闭 `OmenCommandCenterBackground`，避免和官方 OGH 同时控制同一组 BIOS/WMI 接口
- 长期替代 OGH 使用时，建议关闭 OGH 自启动，再启用 `OmenSuperHub` 自启动
- 本程序直接和 BIOS/WMI、驱动、电源策略交互，错误使用可能导致异常功耗、读数异常或系统不稳定

## 传感器来源

- `CPU 温度 / CPU 功耗`：`LibreHardwareMonitor-pawnio`，Intel 路径依赖 `PawnIO`
- `NVIDIA GPU 温度 / GPU 功耗`：`NVAPI` / `NVML`，不依赖 `PawnIO`
- `风扇转速 / 显卡模式 / Smart Adapter / 键盘类型`：HP BIOS WMI
- `电池状态`：WMI `BatteryStatus` + Windows 电源状态

更细的来源说明见 [research/sensor.md](research/sensor.md)。

## 来源与致谢

- [OmenMon](https://github.com/OmenMon/OmenMon)
- [OmenHwCtl](https://github.com/GeographicCone/OmenHwCtl)
- [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor)

## 免责声明

本项目不属于 HP 或 OMEN。品牌名称仅用于说明兼容目标。本程序会直接访问硬件和系统接口，使用者需自行承担相关风险。
