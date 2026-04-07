using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Principal;
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
        Console.WriteLine("执行失败，请查看日志: %LocalAppData%\\OmenSuperHub\\logs\\error.log");
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

      float cpuTemp = 50f, gpuTemp = 40f, cpuPower = 0f, gpuPower = 0f;
      bool monitorGpu = true;
      try {
        libre.Open();
        HardwareTelemetrySnapshot snapshot = telemetry.Poll(new HardwareTelemetryRequest {
          CurrentCpuTemperature = cpuTemp,
          CurrentGpuTemperature = gpuTemp,
          CurrentCpuPowerWatts = cpuPower,
          CurrentGpuPowerWatts = gpuPower,
          RespondSpeed = 0.4f,
          MonitorGpu = monitorGpu
        });

        List<int> fan = control.GetFanLevel();
        Console.WriteLine($"CPU: {snapshot.CpuTemperature:F1} °C / {snapshot.CpuPowerWatts:F1} W");
        Console.WriteLine($"GPU: {snapshot.GpuTemperature:F1} °C / {snapshot.GpuPowerWatts:F1} W");
        Console.WriteLine($"风扇: {(fan.Count > 0 ? fan[0] * 100 : 0)} / {(fan.Count > 1 ? fan[1] * 100 : 0)} RPM");
        Console.WriteLine($"显卡模式: {snapshot.GraphicsMode}");
        Console.WriteLine($"适配器: {snapshot.SmartAdapterStatus}");
        Console.WriteLine($"键盘: {snapshot.KeyboardType}");
        Console.WriteLine($"传感器数量: {(snapshot.TemperatureSensors == null ? 0 : snapshot.TemperatureSensors.Count)}");
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
        Console.WriteLine("正在请求管理员权限并重启 daemon...");
        return 0;
      } catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) {
        Console.WriteLine("已取消管理员权限请求，daemon 未启动。");
        return 4;
      } catch (Exception ex) {
        Console.WriteLine($"自动提权失败: {ex.Message}");
        Console.WriteLine("请右键 PowerShell 选择“以管理员身份运行”，再执行 daemon。");
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
      Console.WriteLine($"AutoStart={snapshot.AutoStart}");
      Console.WriteLine($"OmenKey={snapshot.OmenKey}");
      Console.WriteLine($"FloatingBar={snapshot.FloatingBar}");
      Console.WriteLine($"FloatingBarLocation={snapshot.FloatingBarLocation}");
      return 0;
    }

    static int RunPreset(string[] args) {
      if (args.Length < 2) {
        Console.WriteLine("用法: preset <quiet|balanced|performance|max>");
        return 2;
      }

      UsageModePreset preset = RuntimeControlSettings.ParseUsageMode(args[1]);
      if (preset == UsageModePreset.Custom) {
        Console.WriteLine("preset 不支持 custom，请使用 set 命令。\n");
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
      control.SetGpuClockLimit(profile.GpuClockLimitMhz);
      if (profile.FanControl == FanControlOption.Max) {
        control.SetMaxFanSpeedEnabled(true);
      } else {
        control.SetMaxFanSpeedEnabled(false);
      }

      var snapshot = LoadOrDefault(settingsService);
      snapshot.UsageMode = RuntimeControlSettings.ToStorageValue(preset);
      profile.ApplyToSnapshot(snapshot);
      settingsService.SaveConfig(snapshot);

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
      var startup = new StartupTaskService(process);
      var snapshot = LoadOrDefault(settingsService);

      switch (key) {
        case "fan-mode": {
            FanModeOption mode = RuntimeControlSettings.ParseFanMode(value);
            control.SetFanMode(mode);
            snapshot.FanMode = RuntimeControlSettings.ToStorageValue(mode);
            break;
          }
        case "fan-control": {
            FanControlOption mode = RuntimeControlSettings.ParseFanControl(value, out int rpm);
            if (mode == FanControlOption.Auto) {
              control.SetMaxFanSpeedEnabled(false);
              snapshot.FanControl = "auto";
            } else if (mode == FanControlOption.Max) {
              control.SetMaxFanSpeedEnabled(true);
              snapshot.FanControl = "max";
            } else {
              int normalized = NormalizeRpm(rpm);
              int raw = normalized / 100;
              control.SetMaxFanSpeedEnabled(false);
              control.SetFanLevel(raw, raw);
              snapshot.FanControl = $"{normalized} RPM";
            }
            break;
          }
        case "fan-table": {
            FanTableOption table = RuntimeControlSettings.ParseFanTable(value);
            var fanCurve = new FanCurveService(gateway, settingsService);
            fanCurve.LoadConfig(RuntimeControlSettings.ToStorageValue(table));
            snapshot.FanTable = RuntimeControlSettings.ToStorageValue(table);
            break;
          }
        case "temp-sensitivity": {
            TempSensitivityOption sensitivity = RuntimeControlSettings.ParseTempSensitivity(value);
            snapshot.TempSensitivity = RuntimeControlSettings.ToStorageValue(sensitivity);
            break;
          }
        case "cpu-power": {
            bool isMax = RuntimeControlSettings.IsCpuPowerMax(value);
            int watts = RuntimeControlSettings.ParseCpuPowerWatts(value + (value.EndsWith(" W", StringComparison.OrdinalIgnoreCase) || isMax ? string.Empty : " W"));
            control.SetCpuPowerLimit(isMax ? 254 : watts);
            snapshot.CpuPower = RuntimeControlSettings.ToCpuPowerStorageValue(isMax, watts);
            break;
          }
        case "gpu-power": {
            GpuPowerOption gpu = RuntimeControlSettings.ParseGpuPower(value);
            control.ApplyGpuPower(gpu);
            snapshot.GpuPower = RuntimeControlSettings.ToStorageValue(gpu);
            break;
          }
        case "gpu-clock": {
            int mhz = ParseInt(value, 0);
            control.SetGpuClockLimit(Math.Max(0, mhz));
            snapshot.GpuClock = Math.Max(0, mhz);
            break;
          }
        case "smart-power": {
            bool enabled = ParseOnOff(value);
            snapshot.SmartPowerControlEnabled = enabled;
            break;
          }
        case "auto-start": {
            bool enabled = ParseOnOff(value);
            if (enabled) {
              startup.EnableAutoStart(AppDomain.CurrentDomain.BaseDirectory);
              snapshot.AutoStart = "on";
            } else {
              startup.DisableAutoStart();
              snapshot.AutoStart = "off";
            }
            break;
          }
        case "omen-key": {
            string omenKey = NormalizeOmenKey(value);
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
      settingsService.SaveConfig(snapshot);
      Console.WriteLine($"已设置 {key} = {value}");
      return 0;
    }

    static AppSettingsSnapshot LoadOrDefault(AppSettingsService service) {
      if (service.TryLoadConfig(out AppSettingsSnapshot snapshot)) {
        return snapshot ?? new AppSettingsSnapshot();
      }
      return new AppSettingsSnapshot();
    }

    static int NormalizeRpm(int rpm) {
      if (rpm <= 0) return FanMinRpm;
      int clamped = Math.Max(FanMinRpm, Math.Min(FanMaxRpm, rpm));
      return clamped - (clamped % FanStepRpm);
    }

    static string NormalizeOmenKey(string value) {
      string normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
      switch (normalized) {
        case "custom":
          return "custom";
        case "none":
          return "none";
        default:
          return "default";
      }
    }

    static bool ParseOnOff(string value) {
      string normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
      return normalized == "on" || normalized == "true" || normalized == "1" || normalized == "enable" || normalized == "enabled";
    }

    static bool IsHelp(string value) {
      if (value == null) return true;
      string normalized = value.Trim().ToLowerInvariant();
      return normalized == "help" || normalized == "--help" || normalized == "-h" || normalized == "/?";
    }

    static int ParseInt(string value, int fallback) {
      if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) {
        return parsed;
      }
      return fallback;
    }

    static void PrintHelp() {
      Console.WriteLine("OmenSuperHub CLI");
      Console.WriteLine();
      Console.WriteLine("命令:");
      Console.WriteLine("  daemon");
      Console.WriteLine("    启动持续后台调度（自动提权、隐藏终端、托盘驻留）");
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
      Console.WriteLine("      auto-start <on|off>");
      Console.WriteLine("      omen-key <default|custom|none>");
      Console.WriteLine();
      Console.WriteLine("示例:");
      Console.WriteLine("  OmenSuperHub.exe daemon");
      Console.WriteLine("  OmenSuperHub.exe status");
      Console.WriteLine("  OmenSuperHub.exe preset performance");
      Console.WriteLine("  OmenSuperHub.exe set fan-control \"3200 RPM\"");
      Console.WriteLine("  OmenSuperHub.exe set cpu-power 65");
      Console.WriteLine("  OmenSuperHub.exe set smart-power on");
    }
  }
}
