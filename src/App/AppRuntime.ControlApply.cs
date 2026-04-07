using System;

namespace OmenSuperHub {
  internal sealed partial class AppRuntime {
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

    internal static void ApplyAutoStartSetting(bool enabled) {
      ApplyAutoStart(enabled, persistConfigName: "AutoStart");
    }

    internal static void ApplyOmenKeySetting(string value) {
      ApplyOmenKey(value, persistConfigName: "OmenKey");
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
      LoadFanConfig(RuntimeControlSettings.ToStorageValue(value));
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

    static void ApplyGpuClock(int value, string persistConfigName = null) {
      gpuClock = Math.Max(0, value);
      bool applied = hardwareControlService.SetGpuClockLimit(gpuClock);
      if (!applied) {
        errorLogService.Write(new InvalidOperationException("Failed to apply GPU clock limit."), "gpu clock");
      }
      PersistControlMutation(persistConfigName);
    }

    static void ApplyAutoStart(bool enabled, string persistConfigName = null) {
      try {
        if (enabled) {
          AutoStartEnable();
          autoStart = "on";
        } else {
          AutoStartDisable();
          autoStart = "off";
        }
      } catch (Exception ex) {
        errorLogService.Write(ex, "auto start");
      }

      if (persistConfigName != null) {
        SaveConfig(persistConfigName);
      }
    }

    static void ApplyOmenKey(string value, string persistConfigName = null) {
      omenKey = NormalizeOmenKey(value);
      bool enableFloatingToggle = omenKey == "custom";
      backgroundScheduler?.SetFloatingToggleEnabled(enableFloatingToggle);

      try {
        hardwareControlService.DisableOmenKey();
        if (omenKey != "none") {
          hardwareControlService.EnableOmenKey(omenKey);
        }
      } catch (Exception ex) {
        errorLogService.Write(ex, "omen key");
      }

      if (persistConfigName != null) {
        SaveConfig(persistConfigName);
      }
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
  }
}
