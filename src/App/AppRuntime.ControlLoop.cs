using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Microsoft.Win32;

namespace OmenSuperHub {
  internal sealed partial class AppRuntime {
    static int flagStart = 0;

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
  }
}
