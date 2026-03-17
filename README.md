# OmenSuperHub

`OmenSuperHub` 是一个面向特定 OMEN 硬件组合的本地控制工具，用来替代 `OMEN Gaming Hub` 中最核心的日常功能：风扇控制、CPU/GPU 功率策略、遥测监控、浮窗显示、自启动，以及 `OMEN` 按键自定义。

这个项目现在是一个“收敛目标明确”的工具，而不是通用型 OMEN 兼容层。

本仓库基于 [breadeding/OmenSuperHub](https://github.com/breadeding/OmenSuperHub) fork，并在其基础上持续调整结构、界面和目标硬件范围。

## 项目定位

- 目标机型范围：`Intel Core i9-13900HX + NVIDIA GeForce RTX 4060 Laptop GPU`
- 目标系统：`Windows`
- 构建目标：`.NET Framework 4.8`、`x64`
- 当前策略：优先保证这一套硬件组合上的可用性、稳定性和可维护性，不再追求 AMD CPU、AMD dGPU 或更广的 OMEN 型号覆盖

## 主要功能

- 预设模式切换：`安静`、`均衡`、`性能`、`MAX`
- 高级手动覆盖：风扇模式、风扇曲线、手动转速、温度响应、CPU 功率、GPU 功率、GPU 锁频
- 智能功耗控制：根据温度、功耗、电池状态自动调整 CPU/GPU 策略
- 实时状态面板：CPU/GPU 温度、功率、电池、风扇、适配器、键盘类型、风扇类型
- 托盘与浮窗：任务栏常驻、桌面浮窗、快速状态查看
- 应用行为设置：登录后自启动、`OMEN` 按键动作
- `OMEN` 按键支持：
  - `默认`：打开 `OmenSuperHub` 主页面
  - `切换浮窗显示`
  - `禁用`

## 当前能力边界

这个项目会直接读写 BIOS/WMI、显卡驱动和电源相关接口，因此能力边界是有意收窄的。

当前保留并持续维护的路径：

- HP BIOS WMI 相关的 OMEN 专有控制
- Intel CPU 遥测
- NVIDIA dGPU 遥测
- 双风扇控制和浮窗/托盘集成

当前不再主动扩展的方向：

- AMD CPU 兼容
- AMD dGPU 兼容
- 通用型 OMEN 机型兼容矩阵
- 图形模式切换写入链路

说明：项目里仍会读取当前显卡模式用于展示，但不再提供显卡模式切换功能。

## 界面与配置

当前主界面分为三块：

- `主控制`
  - 预设模式、智能功耗、浮窗控制
- `设备状态`
  - 温度、功率、适配器、键盘、风扇、温度传感器趋势
- `高级设置`
  - 应用行为、自启动、`OMEN` 按键、高级覆盖、策略参数

所有配置统一保存到：

```text
%LocalAppData%\OmenSuperHub\settings.json
```

当前版本把应用设置、智能功耗参数、风扇曲线预设统一保存到这一个 `JSON` 文件中，不再使用注册表，也不再生成 `cool.txt` / `silent.txt` / `fan-curves.xml` 这类独立配置文件。

配置策略是“只认当前版本格式”，不会兼容读取旧版文本或 XML 风扇曲线文件。

## 构建

### 1. 还原依赖

首次构建，或者本地没有 `packages` 目录时，先还原 `packages.config` 依赖：

```powershell
msbuild OmenSuperHub.sln /t:Restore /p:RestorePackagesConfig=true /v:minimal
```

如果你的环境里没有把 `msbuild` 加到 `PATH`，也可以直接使用 Visual Studio 自带的完整路径。

### 2. 构建环境依赖

本地开发或 CI 构建至少需要：

- Visual Studio 2022 或 Build Tools 中可用的 `MSBuild`
- `.NET SDK`
- `.NET Framework 4.8 Developer Pack`
- Windows 环境下的 `x64` 构建能力

### 3. 运行时依赖

项目里已经内置并引用了 `LibreHardwareMonitor-pawnio-squashed` 源码，所以构建时不需要你额外再下载这一份库。

但运行时仍有一个重要前提：

- `PawnIO`

当前 Intel CPU 的部分底层遥测依赖 `PawnIO` 提供的 `PawnIOLib`。如果目标机器没有正确安装 `PawnIO`，程序依然可以启动，但你可能会看到这些影响：

- `Intel CPU 温度`
- `Intel CPU 功耗`

读数缺失、异常，或者退回到有限的传感器路径。

也就是说：

- 构建依赖：仓库里已经带了 `LibreHardwareMonitor-pawnio-squashed`
- 运行依赖：目标系统仍需要能正常加载 `PawnIO`

### 4. 本地构建

调试版：

```powershell
dotnet build OmenSuperHub.sln -c Debug -p:Platform=x64
```

发布版：

```powershell
dotnet build OmenSuperHub.sln -c Release -p:Platform=x64
```

发布产物默认位于：

```text
bin\x64\Release\OmenSuperHub.exe
```

如果正在运行 `OmenSuperHub.exe`，重建前需要先关闭它，否则输出文件可能被占用。

## GitHub Actions

仓库内置了自动构建工作流：

```text
.github/workflows/build-exe.yml
```

触发条件：

- `push`
- `pull_request`
- 手动触发 `workflow_dispatch`

工作流会在 `windows-2022` 上：

1. 检出代码
2. 还原 `packages.config` 依赖
3. 执行 `Release x64` 构建
4. 上传可下载产物

产物名称：

```text
OmenSuperHub-windows-x64-release
```

其中会包含：

- `OmenSuperHub.exe`
- `OmenSuperHub.exe.config`（如果构建输出存在）

## 运行建议

- 启动前建议关闭 `OmenCommandCenterBackground`，避免和官方 `OMEN Gaming Hub` 同时控制同一组 BIOS/WMI 接口
- 如果长期用本项目替代 OGH，建议关闭 OGH 自启动，再启用 `OmenSuperHub` 自启动
- `OMEN` 按键默认行为现在是“打开本应用主页面”，如果你更喜欢快捷控制浮窗，可以在设置里改成“切换浮窗显示”
- 风扇曲线配置现在统一收敛到 `%LocalAppData%\OmenSuperHub\settings.json`，不再依赖部署目录、注册表或独立的 `cool.txt` / `silent.txt`
- 某些硬件状态在唤醒、切电源、驱动恢复后会延迟数秒回稳，这是当前硬件接口本身的特性之一

## 遥测与数据来源

当前项目使用的主要数据来源如下：

- `CPU 温度 / CPU 功耗`
  - `LibreHardwareMonitor-pawnio`
- `NVIDIA GPU 温度 / GPU 功耗`
  - `NVAPI` / `NVML`
- `风扇转速 / 当前显卡模式 / Smart Adapter / 键盘类型 / 风扇类型`
  - HP BIOS WMI
- `电池状态`
  - WMI `BatteryStatus` + Windows 电源状态

## 项目结构

项目当前按下面的依赖方向组织：

```text
UI -> App -> Services -> Hardware
```

主要目录说明：

- `src/UI`
  - 主窗口、浮窗、图表、交互逻辑
- `src/App`
  - 应用运行时、生命周期、后台调度、控制器接口
- `src/App/Services`
  - 配置恢复、状态快照、托盘/浮窗、遥测、硬件控制服务
- `src/Hardware`
  - BIOS/WMI 网关和硬件模型
- `src/Core`
  - 与 UI 和硬件解耦的控制逻辑
- `tests/OmenSuperHub.Tests`
  - 配置映射、快照构建、壳层状态等关键测试

如果你是维护者，建议先看 [docs/MAINTAINING.md](docs/MAINTAINING.md)。

## 测试

当前测试主要覆盖这些低风险高价值区域：

- `RuntimeControlSettings`
- `SettingsRestoreService`
- `DashboardSnapshotBuilder`
- `ShellStatusBuilder`

运行测试：

```powershell
dotnet test tests/OmenSuperHub.Tests/OmenSuperHub.Tests.csproj -c Debug -p:Platform=x64
```

## 已知限制

- 本项目不是官方 HP 软件，也不是通用 OMEN SDK
- 由于直接操作 BIOS/WMI 和驱动接口，不同机型上的行为不能保证一致
- 显卡模式切换功能已移除；当前仅保留显卡模式读取与显示
- 部分功能依赖管理员权限、计划任务、WMI 订阅或系统电源事件，安全软件和系统策略可能会影响表现

## 来源与致谢

- Fork 来源：[breadeding/OmenSuperHub](https://github.com/breadeding/OmenSuperHub)
- [OmenMon](https://github.com/OmenMon/OmenMon)
- [OmenHwCtl](https://github.com/GeographicCone/OmenHwCtl)
- [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor)

## 免责声明

本项目不属于 HP 或 OMEN。品牌名称仅用于说明兼容目标。

本程序会直接访问硬件和系统接口。错误使用、错误配置或与其他控制软件并行运行，可能导致异常功耗、读数异常、控制失效，甚至系统不稳定。请在理解风险的前提下使用。
