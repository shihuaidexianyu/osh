using System.Collections.Generic;
using static OmenSuperHub.OmenHardware;

namespace OmenSuperHub {
  internal sealed class BatteryTelemetry {
    public bool PowerOnline { get; set; }
    public bool Charging { get; set; }
    public bool Discharging { get; set; }
    public int DischargeRateMilliwatts { get; set; }
    public int ChargeRateMilliwatts { get; set; }
    public int RemainingCapacityMilliwattHours { get; set; }
    public int VoltageMillivolts { get; set; }
  }

  internal sealed class DashboardSnapshot {
    public float CpuTemperature { get; set; }
    public float GpuTemperature { get; set; }
    public float CpuPowerWatts { get; set; }
    public float GpuPowerWatts { get; set; }
    public List<int> FanSpeeds { get; set; }
    public bool MonitorGpu { get; set; }
    public bool MonitorFan { get; set; }
    public bool AcOnline { get; set; }
    public string FanMode { get; set; }
    public string FanControl { get; set; }
    public string FanTable { get; set; }
    public string TempSensitivity { get; set; }
    public string CpuPowerSetting { get; set; }
    public string GpuPowerSetting { get; set; }
    public int GpuClockLimit { get; set; }
    public bool FloatingBarEnabled { get; set; }
    public OmenGfxMode GraphicsMode { get; set; }
    public OmenGpuStatus GpuStatus { get; set; }
    public OmenSystemDesignData SystemDesignData { get; set; }
    public OmenSmartAdapterStatus SmartAdapterStatus { get; set; }
    public OmenFanTypeInfo FanTypeInfo { get; set; }
    public OmenKeyboardType KeyboardType { get; set; }
    public BatteryTelemetry Battery { get; set; }
    public int BatteryPercent { get; set; }
    public bool SmartPowerControlEnabled { get; set; }
    public string SmartPowerControlState { get; set; }
    public string SmartPowerControlReason { get; set; }
    public float EstimatedSystemPowerWatts { get; set; }
    public float TargetSystemPowerWatts { get; set; }
    public int SmartCpuLimitWatts { get; set; }
    public string SmartGpuTier { get; set; }
    public bool SmartFanBoostActive { get; set; }
  }
}
