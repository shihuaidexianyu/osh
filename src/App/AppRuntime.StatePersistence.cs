using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace OmenSuperHub {
  internal sealed partial class AppRuntime {
    static float? GetBatteryPowerWatts(BatteryTelemetry telemetry) {
      return HardwareTelemetryService.GetBatteryPowerWatts(telemetry);
    }

    internal static DashboardSnapshot GetDashboardSnapshot() {
      return dashboardSnapshotBuilder.Build(CreateRuntimeStateSnapshot());
    }

    static AppRuntimeState CreateRuntimeStateSnapshot() {
      return new AppRuntimeState {
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
        GpuClockLimit = gpuClock,
        AutoStartEnabled = autoStart == "on",
        OmenKeyMode = omenKey,
        FloatingBarEnabled = floatingBar == "on",
        FloatingBarLocation = floatingBarLoc,
        FloatingBarTextSize = textSize,
        CustomIconMode = customIcon,
        GraphicsMode = currentGfxMode,
        GpuStatus = currentGpuStatus,
        SystemDesignData = currentSystemDesignData,
        SmartAdapterStatus = currentSmartAdapterStatus,
        FanTypeInfo = currentFanTypeInfo,
        KeyboardType = currentKeyboardType,
        Battery = currentBatteryTelemetry,
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

    static void RefreshShellStatus() {
      shellService.RefreshStatus(shellStatusBuilder.Build(
        CreateRuntimeStateSnapshot(),
        AppDomain.CurrentDomain.BaseDirectory,
        MainForm.IsVisibleOnScreen));
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

    static void SaveConfig(string configName = null) {
      settingsService.SaveConfig(CreateSettingsSnapshot(), configName);
    }

    static void RestoreConfig() {
      if (!settingsRestoreService.TryLoadRestorePlan(out SettingsRestorePlan plan)) {
        ApplyUsageModeSetting("balanced");
        LoadPowerControlTuning();
        return;
      }

      usageMode = plan.UsageMode;
      ApplyControlSettings(plan.ControlSettings);
      ApplyRestoredSettings(plan);

      if (!smartPowerControlEnabled) {
        ApplySmartPowerControlDisabledState();
      }

      LoadPowerControlTuning();
      usageMode = InferUsageModeFromCurrentSettings();
    }

    static void ApplyRestoredSettings(SettingsRestorePlan plan) {
      if (plan == null) {
        return;
      }

      ApplyAutoStart(plan.EnableAutoStart);
      alreadyRead = plan.AlreadyRead;
      customIcon = plan.CustomIcon;
      ApplyOmenKey(plan.OmenKey);

      libreComputer.IsGpuEnabled = true;
      monitorGPU = true;
      monitorFan = plan.MonitorFan;
      textSize = plan.FloatingBarSize;
      floatingBarLoc = plan.FloatingBarLocation;
      floatingBar = plan.FloatingBar;

      RefreshShellStatus();
      ApplyCheckedMenuSelections(plan.CheckedMenuSelections);
    }

    static void ApplyCheckedMenuSelections(IEnumerable<CheckedMenuSelection> selections) {
      if (selections == null) {
        return;
      }

      foreach (CheckedMenuSelection selection in selections) {
        UpdateCheckedState(selection.Group, selection.ItemText);
      }
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
        AlreadyRead = alreadyRead,
        CustomIcon = customIcon,
        OmenKey = omenKey,
        MonitorFan = monitorFan,
        FloatingBarSize = textSize,
        FloatingBarLocation = floatingBarLoc,
        FloatingBar = floatingBar
      };

      CreateCurrentControlSettings().ApplyToSnapshot(snapshot);
      return snapshot;
    }
  }
}
