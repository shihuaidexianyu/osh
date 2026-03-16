using System;
using System.Collections.Generic;
using static OmenSuperHub.OmenHardware;

namespace OmenSuperHub {
  internal sealed class DashboardSnapshotBuilder {
    public DashboardSnapshot Build(AppRuntimeState state) {
      if (state == null) {
        return new DashboardSnapshot();
      }

      return new DashboardSnapshot {
        CpuTemperature = state.CpuTemperature,
        GpuTemperature = state.GpuTemperature,
        CpuPowerWatts = state.CpuPowerWatts,
        GpuPowerWatts = state.GpuPowerWatts,
        FanSpeeds = state.FanSpeeds == null ? new List<int>() : new List<int>(state.FanSpeeds),
        MonitorGpu = state.MonitorGpu,
        MonitorFan = state.MonitorFan,
        AcOnline = state.AcOnline,
        UsageMode = state.UsageMode,
        FanMode = state.FanMode,
        FanControl = state.FanControl,
        FanTable = state.FanTable,
        TempSensitivity = state.TempSensitivity,
        CpuPowerSetting = state.CpuPowerSetting,
        GpuPowerSetting = state.GpuPowerSetting,
        GpuClockLimit = state.GpuClockLimit,
        FloatingBarEnabled = state.FloatingBarEnabled,
        FloatingBarLocation = state.FloatingBarLocation,
        GraphicsMode = state.GraphicsMode,
        GpuStatus = CloneGpuStatus(state.GpuStatus),
        SystemDesignData = CloneSystemDesignData(state.SystemDesignData),
        SmartAdapterStatus = state.SmartAdapterStatus,
        FanTypeInfo = CloneFanTypeInfo(state.FanTypeInfo),
        KeyboardType = state.KeyboardType,
        Battery = CloneBatteryTelemetry(state.Battery),
        BatteryPercent = state.BatteryPercent,
        SmartPowerControlEnabled = state.SmartPowerControlEnabled,
        SmartPowerControlState = state.SmartPowerControlState,
        SmartPowerControlReason = state.SmartPowerControlReason,
        ControlCpuTemperature = state.ControlCpuTemperature,
        ControlGpuTemperature = state.ControlGpuTemperature,
        ControlCpuSensor = state.ControlCpuSensor,
        ControlGpuSensor = state.ControlGpuSensor,
        ControlCpuTempWall = state.ControlCpuTempWall,
        ControlGpuTempWall = state.ControlGpuTempWall,
        ControlThermalFeedback = state.ControlThermalFeedback,
        EstimatedSystemPowerWatts = state.EstimatedSystemPowerWatts,
        TargetSystemPowerWatts = state.TargetSystemPowerWatts,
        SmartCpuLimitWatts = state.SmartCpuLimitWatts,
        SmartGpuTier = state.SmartGpuTier,
        SmartFanBoostActive = state.SmartFanBoostActive,
        TemperatureSensors = CloneTemperatureReadings(state.TemperatureSensors)
      };
    }

    static OmenGpuStatus CloneGpuStatus(OmenGpuStatus source) {
      if (source == null) return null;
      return new OmenGpuStatus {
        CustomTgpEnabled = source.CustomTgpEnabled,
        PpabEnabled = source.PpabEnabled,
        DState = source.DState,
        ThermalThreshold = source.ThermalThreshold,
        RawData = source.RawData == null ? null : (byte[])source.RawData.Clone()
      };
    }

    static OmenSystemDesignData CloneSystemDesignData(OmenSystemDesignData source) {
      if (source == null) return null;
      return new OmenSystemDesignData {
        PowerFlags = source.PowerFlags,
        ThermalPolicyVersion = source.ThermalPolicyVersion,
        FeatureFlags = source.FeatureFlags,
        DefaultPl4 = source.DefaultPl4,
        BiosOverclockingSupport = source.BiosOverclockingSupport,
        MiscFlags = source.MiscFlags,
        DefaultConcurrentTdp = source.DefaultConcurrentTdp,
        SoftwareFanControlSupported = source.SoftwareFanControlSupported,
        ExtremeModeSupported = source.ExtremeModeSupported,
        ExtremeModeUnlocked = source.ExtremeModeUnlocked,
        GraphicsSwitcherSupported = source.GraphicsSwitcherSupported,
        GraphicsHybridModeSupported = source.GraphicsHybridModeSupported,
        GraphicsOptimusModeSupported = source.GraphicsOptimusModeSupported,
        RawData = source.RawData == null ? null : (byte[])source.RawData.Clone()
      };
    }

    static OmenFanTypeInfo CloneFanTypeInfo(OmenFanTypeInfo source) {
      if (source == null) return null;
      return new OmenFanTypeInfo {
        RawValue = source.RawValue,
        Fan1Type = source.Fan1Type,
        Fan2Type = source.Fan2Type
      };
    }

    static BatteryTelemetry CloneBatteryTelemetry(BatteryTelemetry source) {
      if (source == null) return null;
      return new BatteryTelemetry {
        PowerOnline = source.PowerOnline,
        Charging = source.Charging,
        Discharging = source.Discharging,
        DischargeRateMilliwatts = source.DischargeRateMilliwatts,
        ChargeRateMilliwatts = source.ChargeRateMilliwatts,
        RemainingCapacityMilliwattHours = source.RemainingCapacityMilliwattHours,
        VoltageMillivolts = source.VoltageMillivolts
      };
    }

    static List<TemperatureSensorReading> CloneTemperatureReadings(IList<TemperatureSensorReading> readings) {
      var snapshot = new List<TemperatureSensorReading>();
      if (readings == null) {
        return snapshot;
      }

      foreach (var reading in readings) {
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
}
