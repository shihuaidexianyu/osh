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
  internal sealed class AppRuntime : IAppController {
    static AppRuntime currentInstance;
    static bool suppressUsageModeAutoMark;
    static readonly IOmenHardwareGateway hardwareGateway = new OmenHardwareGateway();
    Mutex singleInstanceMutex;

    static string NormalizeGraphicsModeSetting(string value) {
      return RuntimeControlSettings.ToStorageValue(RuntimeControlSettings.ParseGraphicsMode(value));
    }

    internal static void ApplyFanModeSetting(string mode) {
      ApplyFanMode(RuntimeControlSettings.ParseFanMode(mode), persistConfigName: "FanMode");
    }

    internal static void ApplyFanControlSetting(string controlValue) {
      ApplyFanControl(RuntimeControlSettings.ParseFanControl(controlValue, out int manualFanRpm), manualFanRpm, persistConfigName: "FanControl");
    }

    internal static void ApplyFanTableSetting(string value) {
      ApplyFanTable(RuntimeControlSettings.ParseFanTable(value), persistConfigName: "FanTable");
    }

    internal static void ApplyTempSensitivitySetting(string value) {
      ApplyTempSensitivity(RuntimeControlSettings.ParseTempSensitivity(value), persistConfigName: "TempSensitivity");
    }

    internal static void ApplyCpuPowerSetting(string value) {
      ApplyCpuPower(RuntimeControlSettings.IsCpuPowerMax(value), RuntimeControlSettings.ParseCpuPowerWatts(value), persistConfigName: "CpuPower");
    }

    internal static void ApplyGpuPowerSetting(string value) {
      ApplyGpuPower(RuntimeControlSettings.ParseGpuPower(value), persistConfigName: "GpuPower");
    }

    internal static void ApplyGraphicsModeSetting(string value) {
      ApplyGraphicsMode(RuntimeControlSettings.ParseGraphicsMode(value), persistConfigName: "GraphicsMode");
    }

    internal static void ApplyUsageModeSetting(string mode) {
      UsageModePreset preset = RuntimeControlSettings.ParseUsageMode(mode);
      if (preset == UsageModePreset.Custom) {
        preset = UsageModePreset.Balanced;
      }

      RuntimeControlSettings settings = RuntimeControlSettings.CreatePreset(preset);
      suppressUsageModeAutoMark = true;
      try {
        ApplyControlSettings(settings);
      } finally {
        suppressUsageModeAutoMark = false;
      }

      usageMode = RuntimeControlSettings.ToStorageValue(preset);
      SaveConfig();
    }

    internal static void ApplyGpuClockSetting(int value) {
      ApplyGpuClock(value, persistConfigName: "GpuClock");
    }

    internal static void ApplyFloatingBarSetting(bool enabled) {
      floatingBar = enabled ? "on" : "off";
      RefreshShellStatus();
      SaveConfig("FloatingBar");
    }

    internal static void ApplyFloatingBarLocationSetting(string location) {
      floatingBarLoc = location == "right" ? "right" : "left";
      RefreshShellStatus();
      SaveConfig("FloatingBarLoc");
    }

    internal static void ApplySmartPowerControlSetting(bool enabled) {
      ApplySmartPowerControl(enabled, persistConfigName: "SmartPowerControl");
    }

    static void ApplyControlSettings(RuntimeControlSettings settings) {
      if (settings == null) {
        return;
      }

      ApplyFanMode(settings.FanMode);
      ApplyFanControl(settings.FanControl, settings.ManualFanRpm);
      ApplyFanTable(settings.FanTable);
      ApplyTempSensitivity(settings.TempSensitivity);
      ApplyCpuPower(settings.CpuPowerMax, settings.CpuPowerWatts);
      ApplyGpuPower(settings.GpuPower);
      ApplyGpuClock(settings.GpuClockLimitMhz);
      ApplySmartPowerControl(settings.SmartPowerControlEnabled);
      ApplyGraphicsMode(settings.GraphicsMode);
    }

    static void ApplyFanMode(FanModeOption mode, string persistConfigName = null) {
      fanMode = RuntimeControlSettings.ToStorageValue(mode);
      hardwareControlService.SetFanMode(mode);
      RestoreCPUPower();
      PersistControlMutation(persistConfigName);
    }

    static void ApplyFanControl(FanControlOption mode, int manualFanRpm, string persistConfigName = null) {
      fanControl = RuntimeControlSettings.ToStorageValue(mode, manualFanRpm);
      if (mode == FanControlOption.Auto) {
        hardwareControlService.SetMaxFanSpeedEnabled(false);
        backgroundScheduler?.SetFanControlLoopEnabled(true);
      } else if (mode == FanControlOption.Max) {
        hardwareControlService.SetMaxFanSpeedEnabled(true);
        backgroundScheduler?.SetFanControlLoopEnabled(false);
      } else {
        hardwareControlService.SetMaxFanSpeedEnabled(false);
        backgroundScheduler?.SetFanControlLoopEnabled(false);
        ApplyManualFanRpm(fanControl);
      }

      PersistControlMutation(persistConfigName);
    }

    static void ApplyFanTable(FanTableOption value, string persistConfigName = null) {
      fanTable = RuntimeControlSettings.ToStorageValue(value);
      LoadFanConfig(value == FanTableOption.Cool ? "cool.txt" : "silent.txt");
      PersistControlMutation(persistConfigName);
    }

    static void ApplyTempSensitivity(TempSensitivityOption value, string persistConfigName = null) {
      tempSensitivity = RuntimeControlSettings.ToStorageValue(value);
      switch (value) {
        case TempSensitivityOption.Realtime:
          respondSpeed = 1f;
          break;
        case TempSensitivityOption.Low:
          respondSpeed = 0.04f;
          break;
        case TempSensitivityOption.High:
          respondSpeed = 0.4f;
          break;
        default:
          respondSpeed = 0.1f;
          break;
      }

      PersistControlMutation(persistConfigName);
    }

    static void ApplyCpuPower(bool isMax, int watts, string persistConfigName = null) {
      cpuPower = RuntimeControlSettings.ToCpuPowerStorageValue(isMax, watts);
      hardwareControlService.SetCpuPowerLimit(isMax ? 254 : Math.Max(25, Math.Min(254, watts)));
      powerController.Reset();
      PersistControlMutation(persistConfigName);
    }

    static void ApplyGpuPower(GpuPowerOption value, string persistConfigName = null) {
      gpuPower = RuntimeControlSettings.ToStorageValue(value);
      hardwareControlService.ApplyGpuPower(value);
      powerController.Reset();
      PersistControlMutation(persistConfigName);
    }

    static void ApplyGraphicsMode(GraphicsModeOption value, string persistConfigName = null) {
      graphicsModeSetting = RuntimeControlSettings.ToStorageValue(value);
      hardwareControlService.ApplyGraphicsMode(value);
      PersistControlMutation(persistConfigName);
    }

    static void ApplyGpuClock(int value, string persistConfigName = null) {
      gpuClock = Math.Max(0, value);
      hardwareControlService.SetGpuClockLimit(gpuClock);
      PersistControlMutation(persistConfigName);
    }

    static void ApplySmartPowerControl(bool enabled, string persistConfigName = null) {
      smartPowerControlEnabled = enabled;
      powerController.Reset();

      if (!enabled) {
        RestoreCPUPower();
        RestoreGpuPower();
        if (fanControl == "auto")
          hardwareControlService.SetMaxFanSpeedEnabled(false);
        smartPowerControlState = "manual";
        smartPowerControlReason = "disabled";
      }

      if (enabled && smartPowerControlState == "manual" && smartPowerControlReason == "disabled") {
        smartPowerControlState = "balanced";
        smartPowerControlReason = "stable";
      }

      PersistControlMutation(persistConfigName);
    }

    static void PersistControlMutation(string persistConfigName) {
      if (persistConfigName == null) {
        return;
      }

      MarkUsageModeCustom();
      SaveConfig(persistConfigName);
    }

    internal static PowerControlTuning GetPowerControlTuningSnapshot() {
      lock (powerControlLock) {
        return powerController.GetTuningSnapshot();
      }
    }

    internal static PowerControlTuning GetDefaultPowerControlTuning() {
      return PowerController.CreateDefaultTuning();
    }

    internal static void ApplyPowerControlTuning(PowerControlTuning tuning) {
      if (tuning == null) {
        return;
      }

      lock (powerControlLock) {
        powerController.UpdateTuning(tuning);
      }
      SavePowerControlTuning();
    }

    internal static void ResetPowerControlTuningToDefault() {
      ApplyPowerControlTuning(GetDefaultPowerControlTuning());
    }

    static void MarkUsageModeCustom() {
      if (suppressUsageModeAutoMark || usageMode == "custom") {
        return;
      }

      usageMode = "custom";
      SaveConfig("UsageMode");
    }

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
    static string usageMode = "balanced", fanTable = "silent", fanMode = "performance", fanControl = "auto", tempSensitivity = "high", cpuPower = "max", gpuPower = "max", graphicsModeSetting = "hybrid", autoStart = "off", customIcon = "original", floatingBar = "off", floatingBarLoc = "left", omenKey = "default";
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
    static readonly HardwareControlService hardwareControlService = new HardwareControlService(hardwareGateway, processCommandService);
    static readonly AppSettingsService settingsService = new AppSettingsService();
    static readonly FanCurveService fanCurveService = new FanCurveService(hardwareGateway);
    static LibreComputer libreComputer = new LibreComputer() { IsCpuEnabled = true, IsGpuEnabled = true };
    static readonly HardwareTelemetryService hardwareTelemetryService = new HardwareTelemetryService(libreComputer, hardwareGateway);
    static readonly AppShellService shellService = new AppShellService();
    static bool monitorGPU = true, monitorFan = true, powerOnline = true;
    static List<int> fanSpeedNow = new List<int> { 20, 23 };
    static List<TemperatureSensorReading> currentTemperatureSensors = new List<TemperatureSensorReading>();
    static float respondSpeed = 0.4f;
    static AppBackgroundScheduler backgroundScheduler;
    static NamedPipeServerStream omenKeyPipeServer;
    static TaskEx omenKeyListenerTask;
    static int shutdownStarted = 0;
    static volatile bool checkFloating = false;
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

    void StartTimers() {
      backgroundScheduler = new AppBackgroundScheduler(
        optimiseSchedule,
        HardwarePollingTick,
        FanControlTick,
        HandleFloatingBarToggle);
      backgroundScheduler.Start();
    }

    void StartFloatingToggleTimer() {
      backgroundScheduler?.SetFloatingToggleEnabled(true);
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
        WriteErrorLog(ex, "hardware polling");
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

    void ReleaseSingleInstanceMutex() {
      if (singleInstanceMutex == null) {
        if (ReferenceEquals(currentInstance, this)) {
          currentInstance = null;
        }
        return;
      }

      try {
        singleInstanceMutex.ReleaseMutex();
      } catch (ApplicationException) {
      } finally {
        singleInstanceMutex.Dispose();
        singleInstanceMutex = null;
        if (ReferenceEquals(currentInstance, this)) {
          currentInstance = null;
        }
      }
    }

    public bool TryStart(string[] args) {
      bool isNewInstance;
      singleInstanceMutex = new Mutex(true, "MyUniqueAppMutex", out isNewInstance);
      if (!isNewInstance) {
        singleInstanceMutex.Dispose();
        singleInstanceMutex = null;
        return false;
      }

      if (Environment.OSVersion.Version.Major >= 6) {
        SetProcessDPIAware();
      }

      AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);
      Application.ThreadException += new ThreadExceptionEventHandler(Application_ThreadException);
      Application.ApplicationExit += new EventHandler(OnApplicationExit);
      currentInstance = this;

      Application.EnableVisualStyles();
      Application.SetCompatibleTextRenderingDefault(false);
      MainForm.Initialize(this);

      powerOnline = SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Online;
      Version version = Assembly.GetExecutingAssembly().GetName().Version;
      string versionString = version.ToString().Replace(".", "");
      alreadyReadCode = new Random(int.Parse(versionString)).Next(1000, 10000);

      InitTrayIcon();

      libreComputer.Open();
      StartTimers();
      getOmenKeyTask();
      StartFloatingToggleTimer();

      RestoreConfig();
      HandleFirstRunPrompt();

      SystemEvents.PowerModeChanged += new PowerModeChangedEventHandler(OnPowerChange);
      return true;
    }

    public void Stop() {
      ReleaseSingleInstanceMutex();
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

    static void AutoStartEnable() {
      string currentPath = AppDomain.CurrentDomain.BaseDirectory;

      using (TaskService ts = new TaskService()) {
        TaskDefinition td = ts.NewTask();
        td.RegistrationInfo.Description = "Start OmenSuperHub with admin rights";
        td.Principal.RunLevel = TaskRunLevel.Highest;
        td.Actions.Add(new ExecAction(Path.Combine(currentPath, "OmenSuperHub.exe"), null, null));

        LogonTrigger logonTrigger = new LogonTrigger();
        td.Triggers.Add(logonTrigger);

        td.Settings.DisallowStartIfOnBatteries = false;
        td.Settings.StopIfGoingOnBatteries = false;
        td.Settings.ExecutionTimeLimit = TimeSpan.Zero;
        td.Settings.AllowHardTerminate = false;

        ts.RootFolder.RegisterTaskDefinition(@"OmenSuperHub", td);
        Console.WriteLine("任务已创建。");
      }

      CleanUpAndRemoveTasks();
    }

    static void AutoStartDisable() {
      using (TaskService ts = new TaskService()) {
        Microsoft.Win32.TaskScheduler.Task existingTask = ts.FindTask("OmenSuperHub");

        if (existingTask != null) {
          ts.RootFolder.DeleteTask("OmenSuperHub");
          Console.WriteLine("任务已删除。");
        } else {
          Console.WriteLine("任务不存在，无需删除。");
        }
      }
    }

    public static void CleanUpAndRemoveTasks() {
      string targetFolder = @"C:\Program Files\OmenSuperHub";
      string taskName = "Omen Boot";
      string file1 = @"C:\Windows\SysWOW64\silent.txt";
      string file2 = @"C:\Windows\SysWOW64\cool.txt";

      if (Directory.Exists(targetFolder)) {
        string command = $"rd /s /q \"{targetFolder}\"";
        var result = ExecuteCommand(command);
        Console.WriteLine(result.Output);
      } else {
        Console.WriteLine("旧文件夹不存在");
      }

      if (File.Exists(file1)) {
        string command = $"del /f /q \"{file1}\"";
        var result = ExecuteCommand(command);
        Console.WriteLine($"文件已删除: {file1}");
        Console.WriteLine(result.Output);
      } else {
        Console.WriteLine($"文件不存在: {file1}");
      }

      if (File.Exists(file2)) {
        string command = $"del /f /q \"{file2}\"";
        var result = ExecuteCommand(command);
        Console.WriteLine($"文件已删除: {file2}");
        Console.WriteLine(result.Output);
      } else {
        Console.WriteLine($"文件不存在: {file2}");
      }

      string taskQueryCommand = $"schtasks /query /tn \"{taskName}\"";
      var taskQueryResult = ExecuteCommand(taskQueryCommand);
      if (taskQueryResult.ExitCode == 0) {
        string deleteTaskCommand = $"schtasks /delete /tn \"{taskName}\" /f";
        var deleteTaskResult = ExecuteCommand(deleteTaskCommand);
        Console.WriteLine("已成功删除计划任务 \"Omen Boot\"。");
        Console.WriteLine(deleteTaskResult.Output);
      } else {
        Console.WriteLine($"计划任务 \"{taskName}\" 不存在。");
      }

      string regDeleteCommand = @"reg delete ""HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run"" /v ""OmenSuperHub"" /f";
      var regDeleteResult = ExecuteCommand(regDeleteCommand);
      Console.WriteLine("成功取消开机自启");
      Console.WriteLine(regDeleteResult.Output);
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
        GraphicsMode = RuntimeControlSettings.ParseGraphicsMode(graphicsModeSetting),
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
          WriteErrorLog(ex, "smart power control");
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

    static bool SetGPUClockLimit(int freq) {
      return hardwareControlService.SetGpuClockLimit(freq);
    }

    static ProcessResult ExecuteCommand(string command) {
      return processCommandService.Execute(command);
    }

    static void UpdateCheckedState(string group, string itemText = null, ToolStripMenuItem menuItemToCheck = null) {
      shellService.UpdateCheckedState(group, itemText, menuItemToCheck);
    }

    static void OnShellTick() {
      if (isShuttingDown) {
        return;
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

    static float? GetBatteryPowerWatts(BatteryTelemetry telemetry) {
      return HardwareTelemetryService.GetBatteryPowerWatts(telemetry);
    }

    internal static DashboardSnapshot GetDashboardSnapshot() {
      return new DashboardSnapshot {
        CpuTemperature = CPUTemp,
        GpuTemperature = GPUTemp,
        CpuPowerWatts = CPUPower,
        GpuPowerWatts = GPUPower,
        FanSpeeds = new List<int>(fanSpeedNow),
        MonitorGpu = monitorGPU,
        MonitorFan = monitorFan,
        AcOnline = powerOnline,
        UsageMode = usageMode,
        FanMode = fanMode,
        FanControl = fanControl,
        FanTable = fanTable,
        TempSensitivity = tempSensitivity,
        CpuPowerSetting = cpuPower,
        GpuPowerSetting = gpuPower,
        GraphicsModeSetting = graphicsModeSetting,
        GpuClockLimit = gpuClock,
        FloatingBarEnabled = floatingBar == "on",
        FloatingBarLocation = floatingBarLoc,
        GraphicsMode = currentGfxMode,
        GpuStatus = currentGpuStatus == null ? null : new OmenGpuStatus {
          CustomTgpEnabled = currentGpuStatus.CustomTgpEnabled,
          PpabEnabled = currentGpuStatus.PpabEnabled,
          DState = currentGpuStatus.DState,
          ThermalThreshold = currentGpuStatus.ThermalThreshold,
          RawData = currentGpuStatus.RawData == null ? null : (byte[])currentGpuStatus.RawData.Clone()
        },
        SystemDesignData = currentSystemDesignData == null ? null : new OmenSystemDesignData {
          PowerFlags = currentSystemDesignData.PowerFlags,
          ThermalPolicyVersion = currentSystemDesignData.ThermalPolicyVersion,
          FeatureFlags = currentSystemDesignData.FeatureFlags,
          DefaultPl4 = currentSystemDesignData.DefaultPl4,
          BiosOverclockingSupport = currentSystemDesignData.BiosOverclockingSupport,
          MiscFlags = currentSystemDesignData.MiscFlags,
          DefaultConcurrentTdp = currentSystemDesignData.DefaultConcurrentTdp,
          SoftwareFanControlSupported = currentSystemDesignData.SoftwareFanControlSupported,
          ExtremeModeSupported = currentSystemDesignData.ExtremeModeSupported,
          ExtremeModeUnlocked = currentSystemDesignData.ExtremeModeUnlocked,
          GraphicsSwitcherSupported = currentSystemDesignData.GraphicsSwitcherSupported,
          RawData = currentSystemDesignData.RawData == null ? null : (byte[])currentSystemDesignData.RawData.Clone()
        },
        SmartAdapterStatus = currentSmartAdapterStatus,
        FanTypeInfo = currentFanTypeInfo == null ? null : new OmenFanTypeInfo {
          RawValue = currentFanTypeInfo.RawValue,
          Fan1Type = currentFanTypeInfo.Fan1Type,
          Fan2Type = currentFanTypeInfo.Fan2Type
        },
        KeyboardType = currentKeyboardType,
        Battery = currentBatteryTelemetry == null ? null : new BatteryTelemetry {
          PowerOnline = currentBatteryTelemetry.PowerOnline,
          Charging = currentBatteryTelemetry.Charging,
          Discharging = currentBatteryTelemetry.Discharging,
          DischargeRateMilliwatts = currentBatteryTelemetry.DischargeRateMilliwatts,
          ChargeRateMilliwatts = currentBatteryTelemetry.ChargeRateMilliwatts,
          RemainingCapacityMilliwattHours = currentBatteryTelemetry.RemainingCapacityMilliwattHours,
          VoltageMillivolts = currentBatteryTelemetry.VoltageMillivolts
        },
        BatteryPercent = (int)Math.Round(SystemInformation.PowerStatus.BatteryLifePercent * 100),
        SmartPowerControlEnabled = smartPowerControlEnabled,
        SmartPowerControlState = smartPowerControlState,
        SmartPowerControlReason = smartPowerControlReason,
        ControlCpuTemperature = controlCpuTemperatureC,
        ControlGpuTemperature = controlGpuTemperatureC,
        ControlCpuSensor = controlCpuSensorName,
        ControlGpuSensor = controlGpuSensorName,
        ControlCpuTempWall = controlCpuTempWallC,
        ControlGpuTempWall = controlGpuTempWallC,
        ControlThermalFeedback = controlThermalFeedback,
        EstimatedSystemPowerWatts = estimatedSystemPowerWatts,
        TargetSystemPowerWatts = targetSystemPowerWatts,
        SmartCpuLimitWatts = smartCpuLimitWatts,
        SmartGpuTier = smartGpuTier,
        SmartFanBoostActive = smartFanBoostActive,
        TemperatureSensors = GetTemperatureSensorSnapshot()
      };
    }

    static List<TemperatureSensorReading> GetTemperatureSensorSnapshot() {
      lock (temperatureSensorsLock) {
        var snapshot = new List<TemperatureSensorReading>(currentTemperatureSensors.Count);
        foreach (var reading in currentTemperatureSensors) {
          if (reading == null) {
            continue;
          }

          snapshot.Add(new TemperatureSensorReading {
            Name = reading.Name,
            Celsius = reading.Celsius
          });
        }
        return snapshot;
      }
    }

    static void ShowMainWindow() {
      MainForm.Instance.Show();
      MainForm.Instance.WindowState = FormWindowState.Normal;
      MainForm.Instance.BringToFront();
      MainForm.Instance.Activate();
    }

    static void RefreshShellStatus() {
      shellService.RefreshStatus(CreateAppShellStatus());
    }

    static AppShellStatus CreateAppShellStatus() {
      return new AppShellStatus {
        IconMode = customIcon,
        TrayText = BuildTraySummaryText(),
        DynamicIconValue = (int)CPUTemp,
        CustomIconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "custom.ico"),
        FloatingVisible = floatingBar == "on",
        FloatingText = BuildMonitorText(),
        FloatingTextSize = textSize,
        FloatingLocation = floatingBarLoc,
        MainWindowVisible = MainForm.IsVisibleOnScreen
      };
    }

    static string FormatGfxMode(OmenGfxMode mode) {
      switch (mode) {
        case OmenGfxMode.Hybrid:
          return "Hybrid";
        case OmenGfxMode.Discrete:
          return "Discrete";
        case OmenGfxMode.Optimus:
          return "Optimus";
        default:
          return "Unknown";
      }
    }

    static string FormatGpuControl(OmenGpuStatus status) {
      if (status == null)
        return "Unknown";

      string powerMode;
      if (status.CustomTgpEnabled && status.PpabEnabled)
        powerMode = "cTGP+PPAB";
      else if (status.CustomTgpEnabled)
        powerMode = "cTGP";
      else
        powerMode = "BaseTGP";

      return $"{powerMode} D{status.DState}";
    }

    static string FormatAdapterStatus(OmenSmartAdapterStatus status) {
      switch (status) {
        case OmenSmartAdapterStatus.MeetsRequirement:
          return "OK";
        case OmenSmartAdapterStatus.BatteryPower:
          return "Battery";
        case OmenSmartAdapterStatus.BelowRequirement:
          return "Low";
        case OmenSmartAdapterStatus.NotFunctioning:
          return "Fault";
        case OmenSmartAdapterStatus.NoSupport:
          return "N/A";
        default:
          return "?";
      }
    }

    static string FormatFanTypes(OmenFanTypeInfo fanTypeInfo) {
      if (fanTypeInfo == null)
        return null;

      return $"{fanTypeInfo.Fan1Type}/{fanTypeInfo.Fan2Type}";
    }

    static string BuildTraySummaryText() {
      List<string> parts = new List<string>();
      parts.Add($"CPU {CPUTemp:F0}C {CPUPower:F0}W");

      if (monitorGPU)
        parts.Add($"GPU {GPUTemp:F0}C {GPUPower:F0}W");

      float? batteryWatts = GetBatteryPowerWatts(currentBatteryTelemetry);
      if (batteryWatts.HasValue && !powerOnline)
        parts.Add($"BAT {batteryWatts.Value:F0}W");

      if (currentGfxMode != OmenGfxMode.Unknown)
        parts.Add(FormatGfxMode(currentGfxMode));

      string text = string.Join(" | ", parts);
      if (text.Length > 63)
        text = text.Substring(0, 63);
      return text;
    }

    static void LoadFanConfig(string filePath) {
      fanCurveService.LoadConfig(filePath);
    }

    static void SavePowerControlTuning() {
      PowerControlTuning tuning;
      lock (powerControlLock) {
        tuning = powerController.GetTuningSnapshot();
      }
      settingsService.SavePowerControlTuning(tuning);
    }

    static void LoadPowerControlTuning() {
      lock (powerControlLock) {
        powerController.UpdateTuning(settingsService.LoadPowerControlTuning());
      }
    }

    static void SaveConfig(string configName = null) {
      settingsService.SaveConfig(CreateSettingsSnapshot(), configName);
    }

    static void RestoreConfig() {
      AppSettingsSnapshot snapshot;
      if (!settingsService.TryLoadConfig(out snapshot)) {
        ApplyUsageModeSetting("balanced");
        LoadPowerControlTuning();
        return;
      }

      usageMode = RuntimeControlSettings.ToStorageValue(RuntimeControlSettings.ParseUsageMode(snapshot.UsageMode));
      ApplyControlSettings(RuntimeControlSettings.FromSnapshot(snapshot));

      if (fanTable.Contains("cool")) {
        LoadFanConfig("cool.txt");
        UpdateCheckedState("fanTableGroup", "降温模式");
      } else if (fanTable.Contains("silent")) {
        LoadFanConfig("silent.txt");
        UpdateCheckedState("fanTableGroup", "安静模式");
      }

      if (fanMode.Contains("performance")) {
        UpdateCheckedState("fanModeGroup", "性能模式");
      } else if (fanMode.Contains("default")) {
        UpdateCheckedState("fanModeGroup", "均衡模式");
      }

      if (fanControl == "auto") {
        UpdateCheckedState("fanControlGroup", "自动");
      } else if (fanControl.Contains("max")) {
        UpdateCheckedState("fanControlGroup", "最大风扇");
      } else if (fanControl.Contains(" RPM")) {
        UpdateCheckedState("fanControlGroup", fanControl);
      }

      switch (tempSensitivity) {
        case "realtime":
          UpdateCheckedState("tempSensitivityGroup", "实时");
          break;
        case "high":
          UpdateCheckedState("tempSensitivityGroup", "高");
          break;
        case "medium":
          UpdateCheckedState("tempSensitivityGroup", "中");
          break;
        case "low":
          UpdateCheckedState("tempSensitivityGroup", "低");
          break;
      }

      if (cpuPower == "max") {
        UpdateCheckedState("cpuPowerGroup", "最大");
      } else if (cpuPower.Contains(" W")) {
        int value = int.Parse(cpuPower.Replace(" W", "").Trim());
        if (value >= 5 && value <= 254) {
          UpdateCheckedState("cpuPowerGroup", cpuPower);
        }
      }

      switch (gpuPower) {
        case "max":
          UpdateCheckedState("gpuPowerGroup", "高性能");
          break;
        case "med":
          UpdateCheckedState("gpuPowerGroup", "均衡");
          break;
        case "min":
          UpdateCheckedState("gpuPowerGroup", "节能");
          break;
      }

      if (SetGPUClockLimit(gpuClock)) {
        UpdateCheckedState("gpuClockGroup", gpuClock + " MHz");
      } else {
        UpdateCheckedState("gpuClockGroup", "还原");
      }

      autoStart = snapshot.AutoStart;
      switch (autoStart) {
        case "on":
          AutoStartEnable();
          UpdateCheckedState("autoStartGroup", "开启");
          break;
        case "off":
          UpdateCheckedState("autoStartGroup", "关闭");
          break;
      }

      alreadyRead = snapshot.AlreadyRead;

      customIcon = snapshot.CustomIcon;
      RefreshShellStatus();
      switch (customIcon) {
        case "original":
          UpdateCheckedState("customIconGroup", "原版");
          break;
        case "custom":
          UpdateCheckedState("customIconGroup", "自定义图标");
          break;
        case "dynamic":
          UpdateCheckedState("customIconGroup", "动态图标");
          break;
      }

      omenKey = snapshot.OmenKey;
      switch (omenKey) {
        case "default":
          backgroundScheduler?.SetFloatingToggleEnabled(false);
          hardwareControlService.DisableOmenKey();
          hardwareControlService.EnableOmenKey(omenKey);
          UpdateCheckedState("omenKeyGroup", "默认");
          break;
        case "custom":
          backgroundScheduler?.SetFloatingToggleEnabled(true);
          hardwareControlService.DisableOmenKey();
          hardwareControlService.EnableOmenKey(omenKey);
          UpdateCheckedState("omenKeyGroup", "切换浮窗显示");
          break;
        case "none":
          backgroundScheduler?.SetFloatingToggleEnabled(false);
          hardwareControlService.DisableOmenKey();
          UpdateCheckedState("omenKeyGroup", "取消绑定");
          break;
      }

      libreComputer.IsGpuEnabled = true;
      monitorGPU = true;

      monitorFan = snapshot.MonitorFan;
      if (monitorFan) {
        UpdateCheckedState("monitorFanGroup", "开启风扇监控");
      } else {
        UpdateCheckedState("monitorFanGroup", "关闭风扇监控");
      }

      if (!smartPowerControlEnabled) {
        powerController.Reset();
        smartPowerControlState = "manual";
        smartPowerControlReason = "disabled";
        smartFanBoostActive = false;
      }
      LoadPowerControlTuning();
      usageMode = InferUsageModeFromCurrentSettings();

      textSize = snapshot.FloatingBarSize;
      RefreshShellStatus();
      switch (textSize) {
        case 24:
          UpdateCheckedState("floatingBarSizeGroup", "24号");
          break;
        case 36:
          UpdateCheckedState("floatingBarSizeGroup", "36号");
          break;
        case 48:
          UpdateCheckedState("floatingBarSizeGroup", "48号");
          break;
      }

      floatingBarLoc = snapshot.FloatingBarLocation;
      RefreshShellStatus();
      if (floatingBarLoc == "left") {
        UpdateCheckedState("floatingBarLocGroup", "左上角");
      } else {
        UpdateCheckedState("floatingBarLocGroup", "右上角");
      }

      floatingBar = snapshot.FloatingBar;
      RefreshShellStatus();
      if (floatingBar == "on") {
        UpdateCheckedState("floatingBarGroup", "显示浮窗");
      } else {
        UpdateCheckedState("floatingBarGroup", "关闭浮窗");
      }
    }

    static AppSettingsSnapshot CreateSettingsSnapshot() {
      return new AppSettingsSnapshot {
        UsageMode = usageMode,
        FanTable = fanTable,
        FanMode = fanMode,
        FanControl = fanControl,
        TempSensitivity = tempSensitivity,
        CpuPower = cpuPower,
        GpuPower = gpuPower,
        GraphicsModeSetting = graphicsModeSetting,
        GpuClock = gpuClock,
        AutoStart = autoStart,
        AlreadyRead = alreadyRead,
        CustomIcon = customIcon,
        OmenKey = omenKey,
        MonitorFan = monitorFan,
        SmartPowerControlEnabled = smartPowerControlEnabled,
        FloatingBarSize = textSize,
        FloatingBarLocation = floatingBarLoc,
        FloatingBar = floatingBar
      };
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

    static void getOmenKeyTask() {
      omenKeyListenerTask = TaskEx.Run(() => {
        while (!isShuttingDown) {
          try {
            using (var pipeServer = new NamedPipeServerStream("OmenSuperHubPipe", PipeDirection.In)) {
              omenKeyPipeServer = pipeServer;
              pipeServer.WaitForConnection();
              if (isShuttingDown) {
                break;
              }

              using (var reader = new StreamReader(pipeServer)) {
                string message = reader.ReadToEnd();
                if (!string.IsNullOrEmpty(message) && message.Contains("OmenKeyTriggered")) {
                  checkFloating = true;
                }
              }
            }
          } catch (ObjectDisposedException) {
            break;
          } catch (IOException) {
            if (isShuttingDown) {
              break;
            }
          } finally {
            omenKeyPipeServer = null;
          }
        }
      });
    }

    static string BuildMonitorText() {
      List<string> lines = new List<string>();
      lines.Add($"CPU: {CPUTemp:F1}°C  {CPUPower:F1}W");

      if (monitorGPU)
        lines.Add($"GPU: {GPUTemp:F1}°C  {GPUPower:F1}W");

      float systemPower = CPUPower + (monitorGPU ? GPUPower : 0f);
      string source = powerOnline ? "AC" : "BAT";
      if (!powerOnline && currentBatteryTelemetry != null) {
        float? batteryWatts = GetBatteryPowerWatts(currentBatteryTelemetry);
        if (batteryWatts.HasValue) {
          systemPower = batteryWatts.Value;
        }
      }
      lines.Add($"SYS: {systemPower:F1}W ({source})");

      if (monitorFan)
        lines.Add($"FAN: {fanSpeedNow[0] * 100}/{fanSpeedNow[1] * 100} RPM");

      return string.Join("\n", lines);
    }

    static void Exit() {
      if (Interlocked.Exchange(ref shutdownStarted, 1) != 0) {
        return;
      }

      isShuttingDown = true;
      if (omenKey == "custom") {
        hardwareControlService.DisableOmenKey();
      }

      SystemEvents.PowerModeChanged -= new PowerModeChangedEventHandler(OnPowerChange);
      StopAndDisposeTimers();
      DisposePipeServer();
      shellService.Dispose();
      libreComputer.Close();
      Application.Exit();
    }

    static void OnApplicationExit(object sender, EventArgs e) {
      if (Interlocked.Exchange(ref shutdownStarted, 1) != 0) {
        currentInstance?.ReleaseSingleInstanceMutex();
        return;
      }

      isShuttingDown = true;
      SystemEvents.PowerModeChanged -= new PowerModeChangedEventHandler(OnPowerChange);
      StopAndDisposeTimers();
      DisposePipeServer();
      shellService.Dispose();

      libreComputer.Close();
      currentInstance?.ReleaseSingleInstanceMutex();
    }

    static void StopAndDisposeTimers() {
      if (backgroundScheduler != null) {
        backgroundScheduler.Dispose();
        backgroundScheduler = null;
      }
    }

    static void DisposePipeServer() {
      var pipeServer = omenKeyPipeServer;
      omenKeyPipeServer = null;
      if (pipeServer != null) {
        pipeServer.Dispose();
      }
    }

    static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e) {
      Exception ex = (Exception)e.ExceptionObject;
      LogError(ex);
    }

    static void Application_ThreadException(object sender, ThreadExceptionEventArgs e) {
      Exception ex = e.Exception;
      LogError(ex);
    }

    static void WriteErrorLog(Exception ex, string context = null) {
      if (ex == null) {
        return;
      }

      try {
        string absoluteFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");
        string prefix = string.IsNullOrWhiteSpace(context) ? string.Empty : $"[{context}] ";
        File.AppendAllText(absoluteFilePath, DateTime.Now + ": " + prefix + ex + Environment.NewLine);
      } catch {
      }
    }

    static void LogError(Exception ex) {
      WriteErrorLog(ex);

      if (!isShuttingDown) {
        MessageBox.Show("An unexpected error occurred. Please check the log file for details.");
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

    void IAppController.ApplyGraphicsModeSetting(string value) {
      ApplyGraphicsModeSetting(value);
    }

    void IAppController.ApplyGpuClockSetting(int value) {
      ApplyGpuClockSetting(value);
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
