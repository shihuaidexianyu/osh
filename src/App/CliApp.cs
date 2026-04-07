using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using LibreComputer = LibreHardwareMonitor.Hardware.Computer;
using WinFormsApp = System.Windows.Forms.Application;

namespace OmenSuperHub {
  internal static class CliApp {
    const int FanMinRpm = 1600;
    const int FanMaxRpm = 6400;
    const int FanStepRpm = 100;

    public static int Run(string[] args) {
      var errorLog = new AppErrorLogService();
      try {
        if (args == null || args.Length == 0 || IsHelp(args[0])) {
          PrintHelp();
          return 0;
        }

        string command = args[0].Trim().ToLowerInvariant();
        switch (command) {
          case "daemon":
            return RunDaemon(args);
          case "mode":
            return RunMode(args);
          case "status":
            return RunStatus();
          case "config":
            return RunConfig();
          case "preset":
            return RunPreset(args);
          case "set":
            return RunSet(args);
          default:
            Console.WriteLine($"未知命令: {args[0]}");
            PrintHelp();
            return 2;
        }
      } catch (Exception ex) {
        errorLog.Write(ex, "cli");
        Console.WriteLine("执行失败，请查看日志: %LocalAppData%\\osh\\logs\\error.log");
        Console.WriteLine(ex.Message);
        return 1;
      }
    }

    static int RunStatus() {
      var gateway = new OmenHardwareGateway();
      var process = new ProcessCommandService();
      var control = new HardwareControlService(gateway, process);
      var libre = new LibreComputer { IsCpuEnabled = true, IsGpuEnabled = true };
      var telemetry = new HardwareTelemetryService(libre, gateway);

      try {
        libre.Open();
        telemetry.RefreshImmediately();
        HardwareTelemetrySnapshot snapshot = telemetry.Poll(new HardwareTelemetryRequest {
          CurrentCpuTemperature = float.NaN,
          CurrentGpuTemperature = float.NaN,
          CurrentCpuPowerWatts = float.NaN,
          CurrentGpuPowerWatts = float.NaN,
          RespondSpeed = 1f,
          MonitorGpu = true
        });

        List<int> fan = control.GetFanLevel();
        Console.WriteLine($"CPU: {FormatTelemetryValue(snapshot.CpuTemperature, "°C")} / {FormatTelemetryValue(snapshot.CpuPowerWatts, "W")}");
        Console.WriteLine($"GPU: {FormatTelemetryValue(snapshot.GpuTemperature, "°C")} / {FormatTelemetryValue(snapshot.GpuPowerWatts, "W")}");
        Console.WriteLine($"风扇: {(fan.Count > 0 ? fan[0] * 100 : 0)} / {(fan.Count > 1 ? fan[1] * 100 : 0)} RPM");
        Console.WriteLine($"显卡模式: {snapshot.GraphicsMode}");
        Console.WriteLine($"适配器: {snapshot.SmartAdapterStatus}");
        Console.WriteLine($"键盘: {snapshot.KeyboardType}");
        Console.WriteLine($"传感器数量: {(snapshot.TemperatureSensors == null ? 0 : snapshot.TemperatureSensors.Count)}");
        if (snapshot.TemperatureSensors == null || snapshot.TemperatureSensors.Count == 0) {
          Console.WriteLine("注意: 当前未获取到温度传感器，部分读数可能不可用。");
        }
      } finally {
        libre.Close();
      }

      return 0;
    }

    static int RunDaemon(string[] args) {
      if (!IsProcessElevated()) {
        return RelaunchDaemonAsAdmin(args);
      }

      using (var runtime = new AppRuntime()) {
        if (!runtime.TryStart()) {
          Console.WriteLine("后台调度已在运行。");
          return 3;
        }

        HideConsoleWindow();
        WinFormsApp.EnableVisualStyles();
        WinFormsApp.SetCompatibleTextRenderingDefault(false);
        using (var tray = new DaemonTrayContext(runtime)) {
          WinFormsApp.Run(tray);
        }
      }

      return 0;
    }

    static void HideConsoleWindow() {
      IntPtr handle = GetConsoleWindow();
      if (handle != IntPtr.Zero) {
        ShowWindow(handle, SwHide);
      }
    }

    static bool IsProcessElevated() {
      using (WindowsIdentity identity = WindowsIdentity.GetCurrent()) {
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
      }
    }

    static int RelaunchDaemonAsAdmin(string[] args) {
      return RelaunchAsAdmin(args, "正在请求管理员权限并重启 daemon...");
    }

    static int RelaunchAsAdmin(string[] args, string successMessage) {
      string exePath;
      try {
        using (Process current = Process.GetCurrentProcess()) {
          exePath = current.MainModule?.FileName;
        }
      } catch {
        exePath = null;
      }

      if (string.IsNullOrWhiteSpace(exePath)) {
        Console.WriteLine("无法定位当前可执行文件，无法自动请求管理员权限。");
        return 1;
      }

      string forwardedArgs = BuildArgumentString(args);

      try {
        var startInfo = new ProcessStartInfo {
          FileName = exePath,
          Arguments = forwardedArgs,
          UseShellExecute = true,
          Verb = "runas"
        };

        Process.Start(startInfo);
        if (!string.IsNullOrWhiteSpace(successMessage)) {
          Console.WriteLine(successMessage);
        }
        return 0;
      } catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) {
        Console.WriteLine("已取消管理员权限请求。操作未执行。");
        return 4;
      } catch (Exception ex) {
        Console.WriteLine($"自动提权失败: {ex.Message}");
        Console.WriteLine("请右键 PowerShell 选择“以管理员身份运行”后重试。 ");
        return 1;
      }
    }

    static string BuildArgumentString(string[] args) {
      if (args == null || args.Length == 0) {
        return "daemon";
      }

      var parts = new List<string>(args.Length);
      foreach (string arg in args) {
        parts.Add(QuoteArgument(arg ?? string.Empty));
      }

      return string.Join(" ", parts);
    }

    static string QuoteArgument(string value) {
      if (string.IsNullOrEmpty(value)) {
        return "\"\"";
      }

      bool hasWhitespaceOrQuote = value.IndexOfAny(new[] { ' ', '\t', '"' }) >= 0;
      if (!hasWhitespaceOrQuote) {
        return value;
      }

      string escaped = value.Replace("\\", "\\\\").Replace("\"", "\\\"");
      return $"\"{escaped}\"";
    }

    const int SwHide = 0;

    [DllImport("kernel32.dll")]
    static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    static int RunConfig() {
      var settings = new AppSettingsService();
      if (!settings.TryLoadConfig(out AppSettingsSnapshot snapshot)) {
        Console.WriteLine("未找到配置文件，将使用默认配置。");
        snapshot = new AppSettingsSnapshot();
      }

      Console.WriteLine($"UsageMode={snapshot.UsageMode}");
      Console.WriteLine($"FanMode={snapshot.FanMode}");
      Console.WriteLine($"FanControl={snapshot.FanControl}");
      Console.WriteLine($"FanTable={snapshot.FanTable}");
      Console.WriteLine($"TempSensitivity={snapshot.TempSensitivity}");
      Console.WriteLine($"CpuPower={snapshot.CpuPower}");
      Console.WriteLine($"GpuPower={snapshot.GpuPower}");
      Console.WriteLine($"GpuClock={snapshot.GpuClock}");
      Console.WriteLine($"SmartPowerControlEnabled={snapshot.SmartPowerControlEnabled}");
      Console.WriteLine($"OmenKey={snapshot.OmenKey}");
      return 0;
    }

    static int RunMode(string[] args) {
      if (args.Length < 2) {
        Console.WriteLine("用法: mode <q|b|p|m|quiet|balanced|performance|max> [--no-daemon]");
        return 2;
      }

      bool ensureDaemon = !HasFlag(args, "--no-daemon");
      if (ensureDaemon && !IsProcessElevated()) {
        return RelaunchAsAdmin(args, "正在请求管理员权限并执行 mode...");
      }

      if (!TryParseModePreset(args[1], allowShortAlias: true, out UsageModePreset preset)) {
        Console.WriteLine($"不支持的模式: {args[1]}");
        Console.WriteLine("可用模式: q|b|p|m|quiet|balanced|performance|max");
        return 2;
      }

      string presetValue = RuntimeControlSettings.ToStorageValue(preset);
      int presetExitCode = RunPreset(new[] { "preset", presetValue });
      if (presetExitCode != 0) {
        return presetExitCode;
      }

      if (!ensureDaemon) {
        Console.WriteLine("后台调度: 已跳过启动（--no-daemon）");
        return 0;
      }

      if (IsDaemonRunning()) {
        Console.WriteLine("后台调度: 已在运行");
        return 0;
      }

      int daemonExitCode = StartDaemonDetached();
      if (daemonExitCode == 0) {
        Console.WriteLine("后台调度: 启动中");
      }

      return daemonExitCode;
    }

    static bool TryParseModePreset(string value, bool allowShortAlias, out UsageModePreset preset) {
      preset = UsageModePreset.Custom;
      string normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
      switch (normalized) {
        case "quiet":
          preset = UsageModePreset.Quiet;
          return true;
        case "balanced":
          preset = UsageModePreset.Balanced;
          return true;
        case "performance":
          preset = UsageModePreset.Performance;
          return true;
        case "max":
          preset = UsageModePreset.Max;
          return true;
        default:
          if (!allowShortAlias) {
            return false;
          }

          switch (normalized) {
            case "q":
              preset = UsageModePreset.Quiet;
              return true;
            case "b":
              preset = UsageModePreset.Balanced;
              return true;
            case "p":
              preset = UsageModePreset.Performance;
              return true;
            case "m":
              preset = UsageModePreset.Max;
              return true;
            default:
              return false;
          }
      }
    }

    static bool HasFlag(string[] args, string flag) {
      if (args == null || string.IsNullOrWhiteSpace(flag)) {
        return false;
      }

      foreach (string arg in args) {
        if (string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase)) {
          return true;
        }
      }

      return false;
    }

    static bool IsDaemonRunning() {
      Mutex daemonMutex = null;
      try {
        daemonMutex = Mutex.OpenExisting("osh.CliDaemon");
        return daemonMutex != null;
      } catch (WaitHandleCannotBeOpenedException) {
        return false;
      } catch {
        return false;
      } finally {
        daemonMutex?.Dispose();
      }
    }

    static int StartDaemonDetached() {
      string exePath;
      try {
        using (Process current = Process.GetCurrentProcess()) {
          exePath = current.MainModule?.FileName;
        }
      } catch {
        exePath = null;
      }

      if (string.IsNullOrWhiteSpace(exePath)) {
        Console.WriteLine("无法定位当前可执行文件，无法启动后台调度。\n请手动执行: osh.exe daemon");
        return 1;
      }

      try {
        var startInfo = new ProcessStartInfo {
          FileName = exePath,
          Arguments = "daemon",
          UseShellExecute = true,
          WindowStyle = ProcessWindowStyle.Hidden
        };
        Process.Start(startInfo);
        return 0;
      } catch (Exception ex) {
        Console.WriteLine($"后台调度启动失败: {ex.Message}");
        Console.WriteLine("请手动执行: osh.exe daemon");
        return 1;
      }
    }

    static int RunPreset(string[] args) {
      if (args.Length < 2) {
        Console.WriteLine("用法: preset <quiet|balanced|performance|max>");
        return 2;
      }

      if (!TryParseModePreset(args[1], allowShortAlias: false, out UsageModePreset preset)) {
        Console.WriteLine($"不支持的 preset: {args[1]}");
        Console.WriteLine("可用 preset: quiet|balanced|performance|max");
        return 2;
      }

      RuntimeControlSettings profile = RuntimeControlSettings.CreatePreset(preset);
      var settingsService = new AppSettingsService();
      var gateway = new OmenHardwareGateway();
      var process = new ProcessCommandService();
      var control = new HardwareControlService(gateway, process);
      var fanCurve = new FanCurveService(gateway, settingsService);

      control.SetFanMode(profile.FanMode);
      fanCurve.LoadConfig(RuntimeControlSettings.ToStorageValue(profile.FanTable));
      control.SetCpuPowerLimit(profile.CpuPowerMax ? 254 : profile.CpuPowerWatts);
      control.ApplyGpuPower(profile.GpuPower);
      if (!control.TrySetGpuClockLimit(profile.GpuClockLimitMhz, out string gpuClockError)) {
        Console.WriteLine($"应用 GPU 锁频失败: {gpuClockError}");
        return 1;
      }
      if (profile.FanControl == FanControlOption.Max) {
        control.SetMaxFanSpeedEnabled(true);
      } else {
        control.SetMaxFanSpeedEnabled(false);
      }

      var snapshot = LoadOrDefault(settingsService);
      snapshot.UsageMode = RuntimeControlSettings.ToStorageValue(preset);
      profile.ApplyToSnapshot(snapshot);
      if (!settingsService.TrySaveConfig(snapshot, out string saveError)) {
        Console.WriteLine($"预设已应用到硬件，但保存配置失败: {saveError}");
        return 1;
      }

      Console.WriteLine($"已应用预设: {snapshot.UsageMode}");
      return 0;
    }

    static int RunSet(string[] args) {
      if (args.Length < 3) {
        Console.WriteLine("用法: set <key> <value>");
        return 2;
      }

      string key = args[1].Trim().ToLowerInvariant();
      string value = args[2].Trim();

      var settingsService = new AppSettingsService();
      var gateway = new OmenHardwareGateway();
      var process = new ProcessCommandService();
      var control = new HardwareControlService(gateway, process);
      var snapshot = LoadOrDefault(settingsService);

      switch (key) {
        case "fan-mode": {
            if (!TryParseFanModeValue(value, out FanModeOption mode)) {
              Console.WriteLine("fan-mode 仅支持 default|performance");
              return 2;
            }

            control.SetFanMode(mode);
            snapshot.FanMode = RuntimeControlSettings.ToStorageValue(mode);
            break;
          }
        case "fan-control": {
            if (!TryParseFanControlValue(value, out FanControlOption mode, out int rpm, out string errorMessage)) {
              Console.WriteLine(errorMessage);
              return 2;
            }

            if (mode == FanControlOption.Auto) {
              control.SetMaxFanSpeedEnabled(false);
              snapshot.FanControl = "auto";
            } else if (mode == FanControlOption.Max) {
              control.SetMaxFanSpeedEnabled(true);
              snapshot.FanControl = "max";
            } else {
              int raw = rpm / 100;
              control.SetMaxFanSpeedEnabled(false);
              control.SetFanLevel(raw, raw);
              snapshot.FanControl = $"{rpm} RPM";
            }
            break;
          }
        case "fan-table": {
            if (!TryParseFanTableValue(value, out FanTableOption table)) {
              Console.WriteLine("fan-table 仅支持 silent|cool");
              return 2;
            }

            var fanCurve = new FanCurveService(gateway, settingsService);
            fanCurve.LoadConfig(RuntimeControlSettings.ToStorageValue(table));
            snapshot.FanTable = RuntimeControlSettings.ToStorageValue(table);
            break;
          }
        case "temp-sensitivity": {
            if (!TryParseTempSensitivityValue(value, out TempSensitivityOption sensitivity)) {
              Console.WriteLine("temp-sensitivity 仅支持 low|medium|normal|high|realtime");
              return 2;
            }

            snapshot.TempSensitivity = RuntimeControlSettings.ToStorageValue(sensitivity);
            break;
          }
        case "cpu-power": {
            if (!TryParseCpuPowerValue(value, out bool isMax, out int watts, out string errorMessage)) {
              Console.WriteLine(errorMessage);
              return 2;
            }

            control.SetCpuPowerLimit(isMax ? 254 : watts);
            snapshot.CpuPower = RuntimeControlSettings.ToCpuPowerStorageValue(isMax, watts);
            break;
          }
        case "gpu-power": {
            if (!TryParseGpuPowerValue(value, out GpuPowerOption gpu)) {
              Console.WriteLine("gpu-power 仅支持 min|med|max");
              return 2;
            }

            control.ApplyGpuPower(gpu);
            snapshot.GpuPower = RuntimeControlSettings.ToStorageValue(gpu);
            break;
          }
        case "gpu-clock": {
            if (!TryParseGpuClockValue(value, out int mhz, out string errorMessage)) {
              Console.WriteLine(errorMessage);
              return 2;
            }

            if (!control.TrySetGpuClockLimit(mhz, out string gpuClockError)) {
              Console.WriteLine($"应用 GPU 锁频失败: {gpuClockError}");
              return 1;
            }

            snapshot.GpuClock = mhz;
            break;
          }
        case "smart-power": {
            if (!TryParseOnOffValue(value, out bool enabled)) {
              Console.WriteLine("smart-power 仅支持 on|off");
              return 2;
            }

            snapshot.SmartPowerControlEnabled = enabled;
            break;
          }
        case "omen-key": {
            if (!TryParseOmenKeyValue(value, out string omenKey)) {
              Console.WriteLine("omen-key 仅支持 default|custom|none");
              return 2;
            }

            control.DisableOmenKey();
            if (omenKey != "none") {
              control.EnableOmenKey(omenKey);
            }
            snapshot.OmenKey = omenKey;
            break;
          }
        default:
          Console.WriteLine($"不支持的 key: {key}");
          return 2;
      }

      snapshot.UsageMode = "custom";
      if (!settingsService.TrySaveConfig(snapshot, out string saveError)) {
        Console.WriteLine($"设置已应用到硬件，但保存配置失败: {saveError}");
        return 1;
      }

      Console.WriteLine($"已设置 {key} = {GetAppliedValue(snapshot, key)}");
      return 0;
    }

    static AppSettingsSnapshot LoadOrDefault(AppSettingsService service) {
      if (service.TryLoadConfig(out AppSettingsSnapshot snapshot)) {
        return snapshot ?? new AppSettingsSnapshot();
      }
      return new AppSettingsSnapshot();
    }

    static bool IsHelp(string value) {
      if (value == null) return true;
      string normalized = value.Trim().ToLowerInvariant();
      return normalized == "help" || normalized == "--help" || normalized == "-h" || normalized == "/?";
    }

    static void PrintHelp() {
      Console.WriteLine("osh CLI");
      Console.WriteLine();
      Console.WriteLine("命令:");
      Console.WriteLine("  daemon");
      Console.WriteLine("    启动持续后台调度（自动提权、隐藏终端、托盘驻留）");
      Console.WriteLine();
      Console.WriteLine("  mode <q|b|p|m|quiet|balanced|performance|max> [--no-daemon]");
      Console.WriteLine("    应用预设模式（支持简写），默认自动确保后台调度运行");
      Console.WriteLine();
      Console.WriteLine("  status");
      Console.WriteLine("    读取当前温度/功率/风扇/适配器状态");
      Console.WriteLine();
      Console.WriteLine("  config");
      Console.WriteLine("    打印当前 settings.json 配置");
      Console.WriteLine();
      Console.WriteLine("  preset <quiet|balanced|performance|max>");
      Console.WriteLine("    应用预设并写入配置");
      Console.WriteLine();
      Console.WriteLine("  set <key> <value>");
      Console.WriteLine("    支持 keys:");
      Console.WriteLine("      fan-mode <default|performance>");
      Console.WriteLine("      fan-control <auto|max|\"3200 RPM\">");
      Console.WriteLine("      fan-table <silent|cool>");
      Console.WriteLine("      temp-sensitivity <low|medium|high|realtime>");
      Console.WriteLine("      cpu-power <max|45|65|90|\"65 W\">");
      Console.WriteLine("      gpu-power <min|med|max>");
      Console.WriteLine("      gpu-clock <0|1600|1800|...>");
      Console.WriteLine("      smart-power <on|off>");
      Console.WriteLine("      omen-key <default|custom|none>");
      Console.WriteLine();
      Console.WriteLine("示例:");
      Console.WriteLine("  osh.exe mode p");
      Console.WriteLine("  osh.exe mode balanced --no-daemon");
      Console.WriteLine("  osh.exe daemon");
      Console.WriteLine("  osh.exe status");
      Console.WriteLine("  osh.exe preset performance");
      Console.WriteLine("  osh.exe set fan-control \"3200 RPM\"");
      Console.WriteLine("  osh.exe set cpu-power 65");
      Console.WriteLine("  osh.exe set smart-power on");
    }

    static string FormatTelemetryValue(float value, string unit) {
      if (float.IsNaN(value) || float.IsInfinity(value)) {
        return "Unknown";
      }

      return $"{value:F1} {unit}";
    }

    static bool TryParseFanModeValue(string value, out FanModeOption mode) {
      string normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
      switch (normalized) {
        case "default":
          mode = FanModeOption.Default;
          return true;
        case "performance":
          mode = FanModeOption.Performance;
          return true;
        default:
          mode = FanModeOption.Default;
          return false;
      }
    }

    static bool TryParseFanControlValue(string value, out FanControlOption mode, out int rpm, out string errorMessage) {
      mode = FanControlOption.Auto;
      rpm = 0;
      errorMessage = null;
      string normalized = (value ?? string.Empty).Trim();
      if (string.Equals(normalized, "auto", StringComparison.OrdinalIgnoreCase)) {
        return true;
      }

      if (string.Equals(normalized, "max", StringComparison.OrdinalIgnoreCase)) {
        mode = FanControlOption.Max;
        return true;
      }

      string numeric = normalized.EndsWith(" RPM", StringComparison.OrdinalIgnoreCase)
        ? normalized.Substring(0, normalized.Length - 4).Trim()
        : normalized;
      if (!int.TryParse(numeric, NumberStyles.Integer, CultureInfo.InvariantCulture, out rpm)) {
        errorMessage = "fan-control 仅支持 auto|max|<RPM>，例如 3200 或 \"3200 RPM\"";
        return false;
      }

      if (rpm < FanMinRpm || rpm > FanMaxRpm || rpm % FanStepRpm != 0) {
        errorMessage = $"fan-control RPM 必须在 {FanMinRpm}-{FanMaxRpm} 之间，且为 {FanStepRpm} 的整数倍。";
        return false;
      }

      mode = FanControlOption.Manual;
      return true;
    }

    static bool TryParseFanTableValue(string value, out FanTableOption table) {
      string normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
      switch (normalized) {
        case "silent":
          table = FanTableOption.Silent;
          return true;
        case "cool":
          table = FanTableOption.Cool;
          return true;
        default:
          table = FanTableOption.Silent;
          return false;
      }
    }

    static bool TryParseTempSensitivityValue(string value, out TempSensitivityOption sensitivity) {
      string normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
      switch (normalized) {
        case "low":
          sensitivity = TempSensitivityOption.Low;
          return true;
        case "medium":
        case "normal":
          sensitivity = TempSensitivityOption.Medium;
          return true;
        case "high":
          sensitivity = TempSensitivityOption.High;
          return true;
        case "realtime":
          sensitivity = TempSensitivityOption.Realtime;
          return true;
        default:
          sensitivity = TempSensitivityOption.Medium;
          return false;
      }
    }

    static bool TryParseCpuPowerValue(string value, out bool isMax, out int watts, out string errorMessage) {
      string normalized = (value ?? string.Empty).Trim();
      errorMessage = null;
      if (string.Equals(normalized, "max", StringComparison.OrdinalIgnoreCase)) {
        isMax = true;
        watts = 254;
        return true;
      }

      string numeric = normalized.EndsWith(" W", StringComparison.OrdinalIgnoreCase)
        ? normalized.Substring(0, normalized.Length - 2).Trim()
        : normalized;
      if (!int.TryParse(numeric, NumberStyles.Integer, CultureInfo.InvariantCulture, out watts)) {
        isMax = false;
        watts = 0;
        errorMessage = "cpu-power 仅支持 max 或 25-254 之间的整数，例如 65 或 \"65 W\"";
        return false;
      }

      if (watts < 25 || watts > 254) {
        isMax = false;
        errorMessage = "cpu-power 必须在 25-254 W 之间，或使用 max。";
        return false;
      }

      isMax = false;
      return true;
    }

    static bool TryParseGpuPowerValue(string value, out GpuPowerOption gpuPower) {
      string normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
      switch (normalized) {
        case "min":
          gpuPower = GpuPowerOption.Min;
          return true;
        case "med":
          gpuPower = GpuPowerOption.Med;
          return true;
        case "max":
          gpuPower = GpuPowerOption.Max;
          return true;
        default:
          gpuPower = GpuPowerOption.Med;
          return false;
      }
    }

    static bool TryParseGpuClockValue(string value, out int mhz, out string errorMessage) {
      errorMessage = null;
      if (!int.TryParse((value ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out mhz)) {
        errorMessage = "gpu-clock 必须是整数，且只能为 0 或不小于 210。";
        return false;
      }

      if (mhz != 0 && mhz < 210) {
        errorMessage = "gpu-clock 只能为 0（重置）或不小于 210。";
        return false;
      }

      return true;
    }

    static bool TryParseOnOffValue(string value, out bool enabled) {
      string normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
      switch (normalized) {
        case "on":
        case "true":
        case "1":
        case "enable":
        case "enabled":
          enabled = true;
          return true;
        case "off":
        case "false":
        case "0":
        case "disable":
        case "disabled":
          enabled = false;
          return true;
        default:
          enabled = false;
          return false;
      }
    }

    static bool TryParseOmenKeyValue(string value, out string omenKey) {
      string normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
      switch (normalized) {
        case "default":
        case "custom":
        case "none":
          omenKey = normalized;
          return true;
        default:
          omenKey = null;
          return false;
      }
    }

    static string GetAppliedValue(AppSettingsSnapshot snapshot, string key) {
      switch (key) {
        case "fan-mode":
          return snapshot.FanMode;
        case "fan-control":
          return snapshot.FanControl;
        case "fan-table":
          return snapshot.FanTable;
        case "temp-sensitivity":
          return snapshot.TempSensitivity;
        case "cpu-power":
          return snapshot.CpuPower;
        case "gpu-power":
          return snapshot.GpuPower;
        case "gpu-clock":
          return snapshot.GpuClock.ToString(CultureInfo.InvariantCulture);
        case "smart-power":
          return snapshot.SmartPowerControlEnabled ? "on" : "off";
        case "omen-key":
          return snapshot.OmenKey;
        default:
          return string.Empty;
      }
    }
  }
}
