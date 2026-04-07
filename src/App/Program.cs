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

    static void HardwarePollingTick() {
      if (isShuttingDown) {
        return;
      }

      try {
        QueryHarware();
        if (monitorFan)
          fanSpeedNow = hardwareControlService.GetFanLevel();
        ApplySmartPowerControl();
      } catch (Exception ex) {
        errorLogService.Write(ex, "hardware polling");
      }
    }

    static void FanControlTick() {
      if (isShuttingDown) {
        return;
      }

      int fanSpeed1 = FanRpmToRawLevel(fanCurveService.GetFanSpeedForTemperature(CPUTemp, GPUTemp, monitorGPU, 0));
      int fanSpeed2 = FanRpmToRawLevel(fanCurveService.GetFanSpeedForTemperature(CPUTemp, GPUTemp, monitorGPU, 1));
      if (monitorFan) {
        if (fanSpeed1 != fanSpeedNow[0] || fanSpeed2 != fanSpeedNow[1]) {
          hardwareControlService.SetFanLevel(fanSpeed1, fanSpeed2);
        }
      } else {
        hardwareControlService.SetFanLevel(fanSpeed1, fanSpeed2);
      }
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

    static int flagStart = 0;
    static void optimiseSchedule() {
      if (flagStart < 5) {
        flagStart++;
        if (fanControl.Contains("max")) {
          hardwareControlService.SetMaxFanSpeedEnabled(true);
        } else if (fanControl.Contains(" RPM")) {
          hardwareControlService.SetMaxFanSpeedEnabled(false);
          ApplyManualFanRpm(fanControl);
        }
      }

      hardwareControlService.RefreshFanControllerPresence();
    }

    static void OnPowerChange(object s, PowerModeChangedEventArgs e) {
      if (e.Mode == PowerModes.Resume) {
        hardwareControlService.SendResumeProbe();

        countRestore = 3;
      }

      if (e.Mode == PowerModes.StatusChange) {
        var powerStatus = SystemInformation.PowerStatus;
        if (powerStatus.PowerLineStatus == PowerLineStatus.Online) {
          Console.WriteLine("笔记本已连接到电源。");
          powerOnline = true;
        } else {
          Console.WriteLine("笔记本未连接到电源。");
          powerOnline = false;
        }
      }
    }

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

    static void InitTrayIcon() {
      try {
        AppSettingsSnapshot snapshot;
        if (settingsService.TryLoadConfig(out snapshot)) {
          customIcon = snapshot.CustomIcon;
          if (customIcon == "custom" && !shellService.HasCustomIconFile(AppDomain.CurrentDomain.BaseDirectory)) {
            customIcon = "original";
            SaveConfig("CustomIcon");
            UpdateCheckedState("CustomIcon", "原版");
          }
        }
      } catch (Exception ex) {
        Console.WriteLine($"Error restoring configuration: {ex.Message}");
      }

      shellService.Initialize(OnShellTick, ShowMainWindow, Exit);
      RefreshShellStatus();
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

    static int GetManualCpuLimitWattsForController() {
      if (cpuPower == "max")
        return 125;

      if (cpuPower.EndsWith(" W")) {
        int value;
        if (int.TryParse(cpuPower.Replace(" W", string.Empty).Trim(), out value))
          return Math.Max(25, Math.Min(254, value));
      }

      return 90;
    }

    static GpuPowerTier GetManualGpuTierForController() {
      switch (gpuPower) {
        case "max":
          return GpuPowerTier.Max;
        case "med":
          return GpuPowerTier.Med;
        default:
          return GpuPowerTier.Min;
      }
    }

    static string FormatGpuTierForDisplay(GpuPowerTier tier) {
      switch (tier) {
        case GpuPowerTier.Max:
          return "max";
        case GpuPowerTier.Med:
          return "med";
        default:
          return "min";
      }
    }

    static void ApplySmartPowerControl() {
      if (!smartPowerControlEnabled || isShuttingDown)
        return;

      lock (powerControlLock) {
        try {
          float? batteryDischarge = null;
          if (currentBatteryTelemetry != null &&
              currentBatteryTelemetry.Discharging &&
              currentBatteryTelemetry.DischargeRateMilliwatts > 0) {
            batteryDischarge = currentBatteryTelemetry.DischargeRateMilliwatts / 1000f;
          }

          List<TemperatureSensorReading> temperatureSensors = GetTemperatureSensorSnapshot();
          string cpuSensorSource;
          string gpuSensorSource;
          float cpuControlTemp = hardwareTelemetryService.SelectControlTemperature(true, temperatureSensors, CPUTemp, out cpuSensorSource);
          float gpuControlTemp = hardwareTelemetryService.SelectControlTemperature(false, temperatureSensors, GPUTemp, out gpuSensorSource);
          controlCpuTemperatureC = cpuControlTemp;
          controlGpuTemperatureC = gpuControlTemp;
          controlCpuSensorName = cpuSensorSource;
          controlGpuSensorName = gpuSensorSource;

          var input = new PowerControlInput {
            AcOnline = powerOnline,
            PerformanceMode = fanMode == "performance",
            CoolFanCurve = fanTable == "cool",
            MonitorGpu = monitorGPU,
            FanControlAuto = fanControl == "auto",
            ManualCpuLimitWatts = GetManualCpuLimitWattsForController(),
            ManualGpuTier = GetManualGpuTierForController(),
            CpuTemperatureC = cpuControlTemp,
            CpuPowerWatts = CPUPower,
            GpuTemperatureC = gpuControlTemp,
            GpuPowerWatts = GPUPower,
            BaseSystemPowerWatts = powerOnline ? (monitorGPU ? 14f : 11f) : (monitorGPU ? 10f : 8f),
            BatteryDischargePowerWatts = batteryDischarge,
            BatteryPercent = (int)Math.Round(SystemInformation.PowerStatus.BatteryLifePercent * 100f)
          };

          PowerControlDecision decision = powerController.Evaluate(input);
          smartPowerControlState = decision.State;
          smartPowerControlReason = decision.Reason;
          controlCpuTempWallC = decision.CpuTempWallC;
          controlGpuTempWallC = decision.GpuTempWallC;
          controlThermalFeedback = decision.ThermalFeedback;
          estimatedSystemPowerWatts = decision.EstimatedSystemPowerWatts;
          targetSystemPowerWatts = decision.TargetSystemPowerWatts;
          smartCpuLimitWatts = decision.CurrentCpuLimitWatts;
          smartGpuTier = FormatGpuTierForDisplay(decision.CurrentGpuTier);
          smartFanBoostActive = decision.FanBoostActive;

          if (decision.ApplyCpuLimit) {
            int cpuLimit = Math.Max(1, Math.Min(254, decision.CpuLimitWatts));
            hardwareControlService.SetCpuPowerLimit(cpuLimit);
          }

          if (decision.ApplyGpuTier) {
            switch (decision.GpuTier) {
              case GpuPowerTier.Max:
                hardwareControlService.ApplyGpuPower(GpuPowerOption.Max);
                break;
              case GpuPowerTier.Med:
                hardwareControlService.ApplyGpuPower(GpuPowerOption.Med);
                break;
              default:
                hardwareControlService.ApplyGpuPower(GpuPowerOption.Min);
                break;
            }
          }

          if (fanControl == "auto" && decision.ApplyFanBoost) {
            if (decision.FanBoostActive) {
              hardwareControlService.SetMaxFanSpeedEnabled(true);
            } else {
              hardwareControlService.SetMaxFanSpeedEnabled(false);
            }
        }
      } catch (Exception ex) {
        errorLogService.Write(ex, "smart power control");
      }
      }
    }

    static bool CheckCustomIcon() {
      if (shellService.HasCustomIconFile(AppDomain.CurrentDomain.BaseDirectory)) {
        return true;
      }
      MessageBox.Show("不存在自定义图标custom.ico", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
      return false;
    }

    static void UpdateCheckedState(string group, string itemText = null, ToolStripMenuItem menuItemToCheck = null) {
      shellService.UpdateCheckedState(group, itemText, menuItemToCheck);
    }

    static void OnShellTick() {
      if (isShuttingDown) {
        return;
      }

      if (checkShowMainWindow) {
        checkShowMainWindow = false;
        ShowMainWindow();
      }

      RefreshShellStatus();

      if (countRestore > 0) {
        countRestore--;
        if (countRestore == 0) {
          RestoreConfig();
        }
      }
    }

    static void QueryHarware() {
      HardwareTelemetrySnapshot snapshot = hardwareTelemetryService.Poll(new HardwareTelemetryRequest {
        CurrentCpuTemperature = CPUTemp,
        CurrentGpuTemperature = GPUTemp,
        CurrentCpuPowerWatts = CPUPower,
        CurrentGpuPowerWatts = GPUPower,
        RespondSpeed = respondSpeed,
        MonitorGpu = monitorGPU
      });

      CPUTemp = snapshot.CpuTemperature;
      GPUTemp = snapshot.GpuTemperature;
      CPUPower = snapshot.CpuPowerWatts;
      GPUPower = snapshot.GpuPowerWatts;
      currentGfxMode = snapshot.GraphicsMode;
      currentGpuStatus = snapshot.GpuStatus;
      currentSystemDesignData = snapshot.SystemDesignData;
      currentSmartAdapterStatus = snapshot.SmartAdapterStatus;
      currentFanTypeInfo = snapshot.FanTypeInfo;
      currentKeyboardType = snapshot.KeyboardType;
      currentBatteryTelemetry = snapshot.BatteryTelemetry;
      lock (temperatureSensorsLock) {
        currentTemperatureSensors = snapshot.TemperatureSensors ?? new List<TemperatureSensorReading>();
      }
    }

    static void ShowMainWindow() {
      MainForm.Instance.Show();
      MainForm.Instance.WindowState = FormWindowState.Normal;
      MainForm.Instance.BringToFront();
      MainForm.Instance.Activate();
    }

    static void HandleFloatingBarToggle() {
      if (isShuttingDown) {
        return;
      }

      if (checkFloating) {
        checkFloating = false;
        if (floatingBar == "on") {
          floatingBar = "off";
          UpdateCheckedState("floatingBarGroup", "关闭浮窗");
        } else {
          floatingBar = "on";
          UpdateCheckedState("floatingBarGroup", "显示浮窗");
        }
        RefreshShellStatus();
        SaveConfig("FloatingBar");
      }
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
