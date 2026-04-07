using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Microsoft.Win32;
using LibreComputer = LibreHardwareMonitor.Hardware.Computer;

namespace OmenSuperHub {
  internal sealed class AppRuntime : IDisposable {
    static readonly IOmenHardwareGateway hardwareGateway = new OmenHardwareGateway();
    static readonly ProcessCommandService processCommandService = new ProcessCommandService();
    static readonly StartupTaskService startupTaskService = new StartupTaskService(processCommandService);
    static readonly HardwareControlService hardwareControlService = new HardwareControlService(hardwareGateway, processCommandService);
    static readonly AppSettingsService settingsService = new AppSettingsService();
    static readonly SettingsRestoreService settingsRestoreService = new SettingsRestoreService(settingsService);
    static readonly AppErrorLogService errorLogService = new AppErrorLogService();
    static readonly FanCurveService fanCurveService = new FanCurveService(hardwareGateway, settingsService);

    static readonly object powerControlLock = new object();
    static readonly object temperatureSensorsLock = new object();
    static readonly PowerController powerController = new PowerController();

    static LibreComputer libreComputer = new LibreComputer { IsCpuEnabled = true, IsGpuEnabled = true };
    static readonly HardwareTelemetryService hardwareTelemetryService =
      new HardwareTelemetryService(libreComputer, hardwareGateway, (ex, context) => errorLogService.Write(ex, context));

    static readonly Mutex singleInstanceMutex = new Mutex(false, "OmenSuperHub.CliDaemon");

    static float CPUTemp = 50;
    static float GPUTemp = 40;
    static float CPUPower = 0;
    static float GPUPower = 0;

    static int gpuClock = 0;
    const int FanMinRpm = 0;
    const int FanMaxRpm = 6400;
    const int FanRawStep = 100;
    const int FanMaxRawLevel = FanMaxRpm / FanRawStep;

    static string usageMode = "balanced";
    static string fanTable = "silent";
    static string fanMode = "performance";
    static string fanControl = "auto";
    static string tempSensitivity = "high";
    static string cpuPower = "max";
    static string gpuPower = "max";
    static string autoStart = "off";
    static string omenKey = "default";

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

    static bool monitorGPU = true;
    static bool monitorFan = true;
    static bool powerOnline = true;
    static List<int> fanSpeedNow = new List<int> { 20, 23 };
    static List<TemperatureSensorReading> currentTemperatureSensors = new List<TemperatureSensorReading>();
    static float respondSpeed = 0.4f;

    static AppBackgroundScheduler backgroundScheduler;
    static volatile bool isShuttingDown;
    static DateTime lastConfigWriteUtc = DateTime.MinValue;
    static int flagStart;

    public bool TryStart() {
      if (!singleInstanceMutex.WaitOne(0)) {
        return false;
      }

      try {
        isShuttingDown = false;
        libreComputer.Open();
        RestoreConfig();
        LoadPowerControlTuning();

        var path = settingsService.ConfigFilePath;
        if (File.Exists(path)) {
          lastConfigWriteUtc = File.GetLastWriteTimeUtc(path);
        }

        SystemEvents.PowerModeChanged += OnPowerChange;
        StartTimers();
        return true;
      } catch {
        Stop();
        throw;
      }
    }

    public void Stop() {
      if (isShuttingDown) {
        return;
      }

      isShuttingDown = true;
      SystemEvents.PowerModeChanged -= OnPowerChange;

      if (backgroundScheduler != null) {
        backgroundScheduler.Dispose();
        backgroundScheduler = null;
      }

      libreComputer.Close();

      try {
        singleInstanceMutex.ReleaseMutex();
      } catch (ApplicationException) {
      }
    }

    public void Dispose() {
      Stop();
      singleInstanceMutex.Dispose();
    }

    void StartTimers() {
      backgroundScheduler = new AppBackgroundScheduler(optimiseSchedule, HardwarePollingTick, FanControlTick);
      backgroundScheduler.Start();
    }

    static void HardwarePollingTick() {
      if (isShuttingDown) {
        return;
      }

      try {
        ReloadConfigIfChanged();
        QueryHarware();
        if (monitorFan) {
          fanSpeedNow = hardwareControlService.GetFanLevel();
        }

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

    static void OnPowerChange(object sender, PowerModeChangedEventArgs e) {
      if (e.Mode == PowerModes.Resume) {
        hardwareControlService.SendResumeProbe();
      }
    }

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

    static void RestoreCPUPower() {
      if (cpuPower == "max") {
        hardwareControlService.SetCpuPowerLimit(254);
      } else if (cpuPower.Contains(" W")) {
        int value = RuntimeControlSettings.ParseCpuPowerWatts(cpuPower);
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

    static int GetManualCpuLimitWattsForController() {
      if (cpuPower == "max") {
        return 125;
      }

      if (cpuPower.EndsWith(" W")) {
        int value = RuntimeControlSettings.ParseCpuPowerWatts(cpuPower);
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
      if (!smartPowerControlEnabled || isShuttingDown) {
        return;
      }

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
            BatteryPercent = 100
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
            hardwareControlService.SetMaxFanSpeedEnabled(decision.FanBoostActive);
          }
        } catch (Exception ex) {
          errorLogService.Write(ex, "smart power control");
        }
      }
    }

    static BatteryTelemetry currentBatteryTelemetry;

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
      currentBatteryTelemetry = snapshot.BatteryTelemetry;
      if (currentBatteryTelemetry != null) {
        powerOnline = currentBatteryTelemetry.PowerOnline;
      }

      lock (temperatureSensorsLock) {
        currentTemperatureSensors = snapshot.TemperatureSensors ?? new List<TemperatureSensorReading>();
      }
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

    static void LoadFanConfig(string profileName) {
      fanCurveService.LoadConfig(profileName);
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

    static void SaveConfig() {
      settingsService.SaveConfig(CreateSettingsSnapshot());
    }

    static void RestoreConfig() {
      if (!settingsRestoreService.TryLoadRestorePlan(out SettingsRestorePlan plan)) {
        usageMode = RuntimeControlSettings.ToStorageValue(UsageModePreset.Balanced);
        ApplyControlSettings(RuntimeControlSettings.CreatePreset(UsageModePreset.Balanced));
        LoadPowerControlTuning();
        return;
      }

      usageMode = plan.UsageMode;
      ApplyControlSettings(plan.ControlSettings);
      ApplyRestoredSettings(plan);

      if (!smartPowerControlEnabled) {
        ApplySmartPowerControlDisabledState();
      }

      usageMode = InferUsageModeFromCurrentSettings();
    }

    static void ApplyRestoredSettings(SettingsRestorePlan plan) {
      if (plan == null) {
        return;
      }

      try {
        ApplyAutoStart(plan.EnableAutoStart);
      } catch (Exception ex) {
        errorLogService.Write(ex, "restore auto start");
      }

      monitorGPU = true;
      monitorFan = plan.MonitorFan;
      ApplyOmenKey(plan.OmenKey);
    }

    static void ApplySmartPowerControlDisabledState() {
      powerController.Reset();
      smartPowerControlState = "manual";
      smartPowerControlReason = "disabled";
      smartFanBoostActive = false;
    }

    static AppSettingsSnapshot CreateSettingsSnapshot() {
      var snapshot = new AppSettingsSnapshot {
        UsageMode = usageMode,
        AutoStart = autoStart,
        OmenKey = omenKey,
        MonitorFan = monitorFan,
        SmartPowerControlEnabled = smartPowerControlEnabled
      };

      CreateCurrentControlSettings().ApplyToSnapshot(snapshot);
      return snapshot;
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
      ApplySmartPowerControlSetting(settings.SmartPowerControlEnabled);
    }

    static void ApplyFanMode(FanModeOption mode) {
      fanMode = RuntimeControlSettings.ToStorageValue(mode);
      hardwareControlService.SetFanMode(mode);
      RestoreCPUPower();
    }

    static void ApplyFanControl(FanControlOption mode, int manualFanRpm) {
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
    }

    static void ApplyFanTable(FanTableOption value) {
      fanTable = RuntimeControlSettings.ToStorageValue(value);
      LoadFanConfig(RuntimeControlSettings.ToStorageValue(value));
    }

    static void ApplyTempSensitivity(TempSensitivityOption value) {
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
    }

    static void ApplyCpuPower(bool isMax, int watts) {
      cpuPower = RuntimeControlSettings.ToCpuPowerStorageValue(isMax, watts);
      hardwareControlService.SetCpuPowerLimit(isMax ? 254 : Math.Max(25, Math.Min(254, watts)));
      powerController.Reset();
    }

    static void ApplyGpuPower(GpuPowerOption value) {
      gpuPower = RuntimeControlSettings.ToStorageValue(value);
      hardwareControlService.ApplyGpuPower(value);
      powerController.Reset();
    }

    static void ApplyGpuClock(int value) {
      gpuClock = Math.Max(0, value);
      bool applied = hardwareControlService.SetGpuClockLimit(gpuClock);
      if (!applied) {
        errorLogService.Write(new InvalidOperationException("Failed to apply GPU clock limit."), "gpu clock");
      }
    }

    static void ApplyAutoStart(bool enabled) {
      if (enabled) {
        startupTaskService.EnableAutoStart(AppDomain.CurrentDomain.BaseDirectory);
        autoStart = "on";
      } else {
        startupTaskService.DisableAutoStart();
        autoStart = "off";
      }
    }

    static void ApplyOmenKey(string value) {
      omenKey = NormalizeOmenKey(value);
      hardwareControlService.DisableOmenKey();
      if (omenKey != "none") {
        hardwareControlService.EnableOmenKey(omenKey);
      }
    }

    static void ApplySmartPowerControlSetting(bool enabled) {
      smartPowerControlEnabled = enabled;
      powerController.Reset();

      if (!enabled) {
        RestoreCPUPower();
        RestoreGpuPower();
        if (fanControl == "auto") {
          hardwareControlService.SetMaxFanSpeedEnabled(false);
        }
        smartPowerControlState = "manual";
        smartPowerControlReason = "disabled";
      }

      if (enabled && smartPowerControlState == "manual" && smartPowerControlReason == "disabled") {
        smartPowerControlState = "balanced";
        smartPowerControlReason = "stable";
      }
    }

    static void ReloadConfigIfChanged() {
      string path = settingsService.ConfigFilePath;
      if (!File.Exists(path)) {
        return;
      }

      DateTime writeUtc = File.GetLastWriteTimeUtc(path);
      if (writeUtc <= lastConfigWriteUtc) {
        return;
      }

      if (!settingsService.TryLoadConfig(out AppSettingsSnapshot snapshot)) {
        return;
      }

      RuntimeControlSettings settings = RuntimeControlSettings.FromSnapshot(snapshot);
      ApplyControlSettings(settings);
      monitorFan = snapshot.MonitorFan;

      usageMode = RuntimeControlSettings.ToStorageValue(RuntimeControlSettings.ParseUsageMode(snapshot.UsageMode));
      lastConfigWriteUtc = writeUtc;
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
  }
}
