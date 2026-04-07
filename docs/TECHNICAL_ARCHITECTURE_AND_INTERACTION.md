# osh 技术架构与交互设计文档

---

## 1. 项目定位与目标

osh 是面向特定 OMEN 硬件组合的 Windows 控制工具，当前形态为 **CLI-only + daemon 托盘驻留**。  
核心目标：

- 通过 BIOS/WMI 路径实现风扇、功耗、显卡功率、GPU 锁频等控制。
- 提供可脚本化的命令行交互。
- 通过后台调度实现持续策略控制（温控/功耗控制）。

关键约束：

- 平台：Windows x64
- 目标框架：.NET Framework 4.8
- 硬件/系统权限依赖：部分功能要求管理员权限（WMI/计划任务）

---

## 2. 顶层架构

### 2.1 目录结构（主工程）

- `src/App`
  - CLI 入口与命令分发、daemon 生命周期与托盘驻留
- `src/App/Services`
  - 配置读写、风扇曲线、硬件控制封装、遥测、日志、启动项
- `src/Hardware`
  - 面向 HP OMEN BIOS/WMI 的底层网关与模型定义
- `src/Core/Control`
  - 纯控制算法（`PowerController`）
- `src/Core/Models`
  - 遥测与数据模型
- `tests/OmenSuperHub.Tests`
  - 单元测试（控制映射与策略逻辑）

### 2.2 分层依赖关系

`Program -> CliApp -> AppRuntime/Services -> HardwareGateway -> BIOS/WMI`

依赖方向遵循：

- 上层（CLI/Runtime）依赖下层（Services/Hardware）
- 下层不反向依赖上层
- 算法层 (`Core`) 保持相对纯净，便于测试

---

## 3. 关键模块与职责

### 3.1 启动与命令层

#### `src/App/Program.cs`

- 程序主入口。
- 将 `args` 交由 `CliApp.Run(args)` 执行，并写入 `Environment.ExitCode`。

#### `src/App/CliApp.cs`

- CLI 命令路由中心。
- 已实现命令：
  - `daemon`
  - `status`
  - `config`
  - `preset <quiet|balanced|performance|max>`
  - `set <key> <value>`
- 已具备功能：
  - `daemon` 自动提权（UAC runas）
  - daemon 启动后隐藏终端并转托盘驻留

#### `src/App/DaemonTrayContext.cs`

- daemon 托盘上下文。
- 托盘图标显示运行状态。
- 提供“退出”菜单以优雅停止后台调度（调用 `runtime.Stop()`）。

### 3.2 运行时与调度层

#### `src/App/AppRuntime.cs`

- 后台控制核心（daemon 主体）。
- 能力：
  - 单实例互斥（`Mutex`）
  - 启动时恢复配置
  - 定时轮询硬件遥测
  - 风扇曲线驱动
  - 智能功耗控制（调用 `PowerController`）
  - 配置变化热加载
- 关键流程：
  1. `TryStart()`：初始化硬件与策略、启动定时器
  2. 定时执行硬件轮询与风扇控制
  3. `Stop()`：释放资源并取消调度

#### `src/App/AppBackgroundScheduler.cs`

- 定时任务调度器（`Timer` 驱动）。
- 任务类型：优化任务、硬件轮询、风扇控制循环。

### 3.3 服务层

#### `AppSettingsService`

- 配置文件路径：`%LocalAppData%\osh\settings.json`（兼容读取旧路径 `%LocalAppData%\OmenSuperHub\settings.json`）
- 当前使用 `Newtonsoft.Json` 进行序列化/反序列化。
- 保存与恢复用户配置快照（`AppSettingsSnapshot`）。

#### `SettingsRestoreService`

- 将配置快照映射为运行时恢复计划 `SettingsRestorePlan`。

#### `HardwareControlService`

- 对硬件控制操作做统一封装（风扇、功率、GPU 控制等）。

#### `HardwareTelemetryService`

- 采样 CPU/GPU 温度、功耗、传感器与电池遥测。

#### `FanCurveService`

- 风扇曲线加载、插值计算、配置持久化。

#### `StartupTaskService`

- 开机自启动相关逻辑（基于任务计划/命令）。

#### `AppErrorLogService`

- 错误日志落盘：`%LocalAppData%\osh\logs\error.log`

### 3.4 硬件层

#### `OmenHardwareGateway`

- BIOS/WMI 调用的统一网关。
- 负责向 `root\wmi` / `root\subscription` 发起请求。
- 已支持错误限频与权限提示（避免刷屏）。

#### `OmenHardware`

- 硬件协议相关模型与枚举。

### 3.5 核心算法层

#### `Core/Control/PowerController.cs`

- 智能功耗控制状态机。
- 输入：温度、功率、供电状态、风扇策略。
- 输出：CPU 限功率、GPU 档位、风扇增强决策。
- 含保护状态：热保护、电池保护、性能/均衡/节能状态切换。

---

## 4. 运行流程（当前实现）

### 4.1 `daemon` 启动流程

1. 用户执行 `osh.exe daemon`
2. 检查管理员权限
3. 若非管理员：触发 UAC 提权并重启自身
4. 提权进程启动 `AppRuntime`
5. 隐藏控制台窗口
6. 进入托盘驻留（显示运行状态）
7. 托盘退出 -> `runtime.Stop()` -> 结束

### 4.2 配置生效路径

- `preset` / `set` 修改配置并尝试立即写入硬件。
- daemon 在轮询周期中检测配置文件变更并热重载。

### 4.3 错误处理策略

- CLI 捕获顶层异常并写日志。
- 硬件层对访问拒绝类错误做友好提示与节流输出。

---

## 5. 当前命令能力清单

- `daemon`：后台调度（自动提权 + 隐藏终端 + 托盘）
- `status`：查看实时状态
- `config`：查看当前配置
- `preset <quiet|balanced|performance|max>`：应用预设
- `set <key> <value>`：单项参数设置

限制说明：

- 当前没有显式 `stop`、`restart` 命令
- daemon 与配置命令之间的协同偏“手动”（用户心智成本较高）

---

## 6. 交互逻辑提议（建议方案）

本节聚焦你提出的两点需求：

1. 程序加入环境变量后可全局调用
2. 命令触发模式后自动进入后台调度

### 6.1 统一命令入口（推荐别名：`osh`）

建议最终用户入口统一为 `osh`（可通过安装脚本创建同名启动器或重命名主程序）。

建议命令集：

- `osh mode <quiet|balanced|performance|max> [--no-daemon]`
- `osh daemon`
- `osh stop`
- `osh restart`
- `osh status`
- `osh config`
- `osh doctor`

### 6.2 关键交互规则

#### 规则 A：`mode` 默认确保后台生效

- `osh mode performance` 默认等价于：
  1. 应用预设
  2. 检查 daemon 是否运行
  3. 未运行则自动拉起 daemon（必要时提权）

可加开关 `--no-daemon` 给高级用户。

#### 规则 B：状态可见、结果可预期

命令返回时输出结构化摘要，例如：

- `模式: performance`
- `配置写入: 成功`
- `后台调度: 已启动`
- `提权: 已确认`

#### 规则 C：退出码语义稳定

建议标准化退出码：

- `0` 成功
- `1` 内部错误
- `2` 参数错误
- `3` daemon 已运行/目标状态已达成
- `4` UAC 被取消
- `5` 权限不足且未能提权

### 6.3 PATH 与安装交互提议

建议新增命令：

- `osh install-path`：将程序目录加入用户 PATH
- `osh uninstall-path`：移除 PATH 项
- `osh doctor`：检查 PATH、权限、daemon 状态、配置可读写

### 6.4 `doctor` 建议检查项

- PATH 是否含可执行目录
- 当前是否管理员
- 配置文件可读写
- daemon 是否已运行（Mutex/进程）
- WMI 调用可达性（探测命令）

---

## 7. 推荐落地路线（迭代计划）

### 阶段 1（低风险）

- 新增命令别名 `mode`（内部复用 `preset`）
- `mode` 默认 `ensure-daemon`
- 新增 `stop`（托盘/daemon 退出通道）

### 阶段 2（体验增强）

- 新增 `doctor`
- 新增 `install-path/uninstall-path`
- 状态输出结构化（可选 JSON 输出）

### 阶段 3（自动化与运维）

- daemon 提供 IPC 控制口（用于 stop/restart）
- 提供 `--json` 输出模式供脚本自动化

---

## 8. 测试与验证建议

### 8.1 单元测试（现有）

重点保持：

- `RuntimeMappingTests`
- `PowerControllerTests`

### 8.2 集成验证（建议新增）

- `mode -> ensure-daemon` 触发路径
- UAC 取消路径返回码
- daemon 单实例行为
- stop/restart 行为一致性

---

## 9. 风险与注意事项

- 管理员权限与 UAC 是 Windows 安全边界，无法无感绕过。
- 与 OGH 并行控制同一硬件接口可能互相覆盖。
- 不同机型 BIOS/WMI 兼容性需谨慎验证。

---

## 10. 结论

当前项目已经具备了很好的控制基础（CLI、调度、托盘、权限处理、策略算法）。

面向“命令触发即生效”的用户体验，下一步建议集中在：

1. **命令模型统一（`mode` 语义化）**
2. **后台状态自动保障（ensure-daemon）**
3. **运维友好命令（`stop` / `doctor` / PATH 管理）**

这样可以在不大改内核的前提下，显著提升可用性与可维护性。
