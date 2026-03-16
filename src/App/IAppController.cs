namespace OmenSuperHub {
  internal interface IAppController {
    DashboardSnapshot GetDashboardSnapshot();
    void ApplyUsageModeSetting(string mode);
    void ApplyFanModeSetting(string mode);
    void ApplyFanControlSetting(string controlValue);
    void ApplyFanTableSetting(string value);
    void ApplyTempSensitivitySetting(string value);
    void ApplyCpuPowerSetting(string value);
    void ApplyGpuPowerSetting(string value);
    void ApplyGpuClockSetting(int value);
    void ApplyAutoStartSetting(bool enabled);
    void ApplyOmenKeySetting(string value);
    void ApplyFloatingBarSetting(bool enabled);
    void ApplyFloatingBarLocationSetting(string location);
    void ApplySmartPowerControlSetting(bool enabled);
    PowerControlTuning GetPowerControlTuningSnapshot();
    PowerControlTuning GetDefaultPowerControlTuning();
    void ApplyPowerControlTuning(PowerControlTuning tuning);
  }
}
