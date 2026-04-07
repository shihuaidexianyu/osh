using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.TaskScheduler;
using System.Reflection;
using Microsoft.Win32;
using System.Threading.Tasks;
using System.Management;
using TaskEx = System.Threading.Tasks.Task;
using LibreComputer = LibreHardwareMonitor.Hardware.Computer;
using LibreIHardware = LibreHardwareMonitor.Hardware.IHardware;
using LibreHardwareType = LibreHardwareMonitor.Hardware.HardwareType;
using LibreISensor = LibreHardwareMonitor.Hardware.ISensor;
using LibreSensorType = LibreHardwareMonitor.Hardware.SensorType;
using static OmenSuperHub.OmenHardware;
using System.IO.Pipes;

namespace OmenSuperHub {
  internal sealed partial class AppRuntime : IAppController {
    static AppRuntime currentInstance;
    static bool suppressUsageModeAutoMark;
    static readonly IOmenHardwareGateway hardwareGateway = new OmenHardwareGateway();
    Mutex singleInstanceMutex;

    [DllImport("user32.dll")]
    static extern bool SetProcessDPIAware();

    static float CPUTemp = 50;
    static float GPUTemp = 40;
    static float CPUPower = 0;
    static float GPUPower = 0;
    static OmenGfxMode currentGfxMode = OmenGfxMode.Unknown;
    static OmenGpuStatus currentGpuStatus;
    static OmenSystemDesignData currentSystemDesignData;
    static OmenSmartAdapterStatus currentSmartAdapterStatus = OmenSmartAdapterStatus.Unknown;
    static OmenFanTypeInfo currentFanTypeInfo;
    static OmenKeyboardType currentKeyboardType = OmenKeyboardType.Unknown;
    static BatteryTelemetry currentBatteryTelemetry;
    static int textSize = 48;
    static int countRestore = 0, gpuClock = 0;
    static int alreadyRead = 0, alreadyReadCode = 1000;
    const int FanMinRpm = 0;
    const int FanMaxRpm = 6400;
    const int FanRawStep = 100;
    const int FanMaxRawLevel = FanMaxRpm / FanRawStep;
    static string usageMode = "balanced", fanTable = "silent", fanMode = "performance", fanControl = "auto", tempSensitivity = "high", cpuPower = "max", gpuPower = "max", autoStart = "off", customIcon = "original", floatingBar = "off", floatingBarLoc = "left", omenKey = "default";
    static bool smartPowerControlEnabled = true;
    static string smartPowerControlState = "balanced";
    static string smartPowerControlReason = "stable";
    static float controlCpuTemperatureC = 0f;
    static float controlGpuTemperatureC = 0f;
    static string controlCpuSensorName = "fallback";
    static string controlGpuSensorName = "fallback";
    static float controlCpuTempWallC = 88f;
    static float controlGpuTempWallC = 79f;
    static float controlThermalFeedback = 0f;
    static float estimatedSystemPowerWatts = 0;
    static float targetSystemPowerWatts = 0;
    static int smartCpuLimitWatts = 0;
    static string smartGpuTier = "max";
    static bool smartFanBoostActive = false;
    static readonly object powerControlLock = new object();
    static readonly object temperatureSensorsLock = new object();
    static readonly PowerController powerController = new PowerController();
    static readonly ProcessCommandService processCommandService = new ProcessCommandService();
    static readonly StartupTaskService startupTaskService = new StartupTaskService(processCommandService);
    static readonly HardwareControlService hardwareControlService = new HardwareControlService(hardwareGateway, processCommandService);
    static readonly AppSettingsService settingsService = new AppSettingsService();
    static readonly SettingsRestoreService settingsRestoreService = new SettingsRestoreService(settingsService);
    static readonly AppErrorLogService errorLogService = new AppErrorLogService();
    static readonly FanCurveService fanCurveService = new FanCurveService(hardwareGateway, settingsService);
    static readonly DashboardSnapshotBuilder dashboardSnapshotBuilder = new DashboardSnapshotBuilder();
    static LibreComputer libreComputer = new LibreComputer() { IsCpuEnabled = true, IsGpuEnabled = true };
    static readonly HardwareTelemetryService hardwareTelemetryService = new HardwareTelemetryService(libreComputer, hardwareGateway, (ex, context) => errorLogService.Write(ex, context));
    static readonly AppShellService shellService = new AppShellService();
    static readonly ShellStatusBuilder shellStatusBuilder = new ShellStatusBuilder();
    static bool monitorGPU = true, monitorFan = true, powerOnline = true;
    static List<int> fanSpeedNow = new List<int> { 20, 23 };
    static List<TemperatureSensorReading> currentTemperatureSensors = new List<TemperatureSensorReading>();
    static float respondSpeed = 0.4f;
    static AppBackgroundScheduler backgroundScheduler;
    static NamedPipeServerStream omenKeyPipeServer;
    static TaskEx omenKeyListenerTask;
    static int shutdownStarted = 0;
    static volatile bool checkFloating = false;
    static volatile bool checkShowMainWindow = false;
    static volatile bool isShuttingDown = false;

    static int ClampFanRpm(int rpm) {
      return Math.Max(FanMinRpm, Math.Min(FanMaxRpm, rpm));
    }

    static int FanRpmToRawLevel(int rpm) {
      int clampedRpm = ClampFanRpm(rpm);
      return Math.Max(0, Math.Min(FanMaxRawLevel, clampedRpm / FanRawStep));
    }

    static bool TryParseFanRpm(string value, out int rpm) {
      rpm = FanMinRpm;
      if (string.IsNullOrWhiteSpace(value)) {
        return false;
      }

      string normalized = value.Replace(" RPM", string.Empty).Trim();
      if (!int.TryParse(normalized, out int parsed)) {
        return false;
      }

      rpm = ClampFanRpm(parsed);
      return true;
    }

    static void ApplyManualFanRpm(string value) {
      if (!TryParseFanRpm(value, out int rpm)) {
        return;
      }

      fanControl = $"{rpm} RPM";
      int rawLevel = FanRpmToRawLevel(rpm);
      hardwareControlService.SetFanLevel(rawLevel, rawLevel);
    }

    static void HandleFirstRunPrompt() {
      if (alreadyRead == alreadyReadCode) {
        return;
      }

      MainForm.Instance.ShowHelpSection();
      alreadyRead = alreadyReadCode;
      SaveConfig("AlreadyRead");
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    struct DISPLAY_DEVICE {
      [MarshalAs(UnmanagedType.U4)]
      public int cb;
      [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
      public string DeviceName;
      [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
      public string DeviceString;
      [MarshalAs(UnmanagedType.U4)]
      public DisplayDeviceStateFlags StateFlags;
      [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
      public string DeviceID;
      [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
      public string DeviceKey;
    }

    [Flags()]
    enum DisplayDeviceStateFlags : int {
      /// <summary>The device is part of the desktop.</summary>
      AttachedToDesktop = 0x1,
      MultiDriver = 0x2,
      /// <summary>The device is part of the desktop.</summary>
      PrimaryDevice = 0x4,
      /// <summary>Represents a pseudo device used to mirror application drawing for remoting or other purposes.</summary>
      MirroringDriver = 0x8,
      /// <summary>The device is VGA compatible.</summary>
      VGACompatible = 0x10,
      /// <summary>The device is removable; it cannot be the primary display.</summary>
      Removable = 0x20,
      /// <summary>The device has more display devices.</summary>
      ModesPruned = 0x8000000,
      Remote = 0x4000000,
      Disconnect = 0x2000000
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    static extern bool EnumDisplayDevices(
        string lpDevice,
        uint iDevNum,
        ref DISPLAY_DEVICE lpDisplayDevice,
        uint dwFlags);

    static string NormalizeOmenKey(string value) {
      switch ((value ?? string.Empty).Trim().ToLowerInvariant()) {
        case "custom":
          return "custom";
        case "none":
          return "none";
        default:
          return "default";
      }
    }

    static void RestoreCPUPower() {
      // 恢复CPU功耗设定
      if (cpuPower == "max") {
        hardwareControlService.SetCpuPowerLimit(254);
      } else if (cpuPower.Contains(" W")) {
        int value = int.Parse(cpuPower.Replace(" W", "").Trim());
        if (value > 10 && value <= 254) {
          hardwareControlService.SetCpuPowerLimit(value);
        }
      }
    }

    static void RestoreGpuPower() {
      switch (gpuPower) {
        case "max":
          hardwareControlService.ApplyGpuPower(GpuPowerOption.Max);
          break;
        case "med":
          hardwareControlService.ApplyGpuPower(GpuPowerOption.Med);
          break;
        default:
          hardwareControlService.ApplyGpuPower(GpuPowerOption.Min);
          break;
      }
    }

    static string InferUsageModeFromCurrentSettings() {
      RuntimeControlSettings current = CreateCurrentControlSettings();
      if (current.FanControl == FanControlOption.Manual) {
        return RuntimeControlSettings.ToStorageValue(UsageModePreset.Custom);
      }

      if (current.Matches(RuntimeControlSettings.CreatePreset(UsageModePreset.Max))) {
        return RuntimeControlSettings.ToStorageValue(UsageModePreset.Max);
      }

      if (current.Matches(RuntimeControlSettings.CreatePreset(UsageModePreset.Quiet))) {
        return RuntimeControlSettings.ToStorageValue(UsageModePreset.Quiet);
      }

      if (current.Matches(RuntimeControlSettings.CreatePreset(UsageModePreset.Balanced))) {
        return RuntimeControlSettings.ToStorageValue(UsageModePreset.Balanced);
      }

      if (current.Matches(RuntimeControlSettings.CreatePreset(UsageModePreset.Performance))) {
        return RuntimeControlSettings.ToStorageValue(UsageModePreset.Performance);
      }

      return RuntimeControlSettings.ToStorageValue(UsageModePreset.Custom);
    }

    static RuntimeControlSettings CreateCurrentControlSettings() {
      return new RuntimeControlSettings {
        FanMode = RuntimeControlSettings.ParseFanMode(fanMode),
        FanControl = RuntimeControlSettings.ParseFanControl(fanControl, out int manualFanRpm),
        ManualFanRpm = manualFanRpm,
        FanTable = RuntimeControlSettings.ParseFanTable(fanTable),
        TempSensitivity = RuntimeControlSettings.ParseTempSensitivity(tempSensitivity),
        CpuPowerMax = RuntimeControlSettings.IsCpuPowerMax(cpuPower),
        CpuPowerWatts = RuntimeControlSettings.ParseCpuPowerWatts(cpuPower),
        GpuPower = RuntimeControlSettings.ParseGpuPower(gpuPower),
        GpuClockLimitMhz = Math.Max(0, gpuClock),
        SmartPowerControlEnabled = smartPowerControlEnabled
      };
    }


    DashboardSnapshot IAppController.GetDashboardSnapshot() {
      return GetDashboardSnapshot();
    }

    void IAppController.ApplyUsageModeSetting(string mode) {
      ApplyUsageModeSetting(mode);
    }

    void IAppController.ApplyFanModeSetting(string mode) {
      ApplyFanModeSetting(mode);
    }

    void IAppController.ApplyFanControlSetting(string controlValue) {
      ApplyFanControlSetting(controlValue);
    }

    void IAppController.ApplyFanTableSetting(string value) {
      ApplyFanTableSetting(value);
    }

    void IAppController.ApplyTempSensitivitySetting(string value) {
      ApplyTempSensitivitySetting(value);
    }

    void IAppController.ApplyCpuPowerSetting(string value) {
      ApplyCpuPowerSetting(value);
    }

    void IAppController.ApplyGpuPowerSetting(string value) {
      ApplyGpuPowerSetting(value);
    }

    void IAppController.ApplyGpuClockSetting(int value) {
      ApplyGpuClockSetting(value);
    }

    void IAppController.ApplyAutoStartSetting(bool enabled) {
      ApplyAutoStartSetting(enabled);
    }

    void IAppController.ApplyOmenKeySetting(string value) {
      ApplyOmenKeySetting(value);
    }

    void IAppController.ApplyFloatingBarSetting(bool enabled) {
      ApplyFloatingBarSetting(enabled);
    }

    void IAppController.ApplyFloatingBarLocationSetting(string location) {
      ApplyFloatingBarLocationSetting(location);
    }

    void IAppController.ApplySmartPowerControlSetting(bool enabled) {
      ApplySmartPowerControlSetting(enabled);
    }

    PowerControlTuning IAppController.GetPowerControlTuningSnapshot() {
      return GetPowerControlTuningSnapshot();
    }

    PowerControlTuning IAppController.GetDefaultPowerControlTuning() {
      return GetDefaultPowerControlTuning();
    }

    void IAppController.ApplyPowerControlTuning(PowerControlTuning tuning) {
      ApplyPowerControlTuning(tuning);
    }
  }

  static class Program {
    [STAThread]
    static void Main(string[] args) {
      AppRuntime runtime = new AppRuntime();
      Application.Run(new AppApplicationContext(runtime, args));
    }
  }
}
