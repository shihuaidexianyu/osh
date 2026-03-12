using System;

namespace OmenSuperHub {
  internal enum GpuPowerTier {
    Min = 0,
    Med = 1,
    Max = 2
  }

  internal sealed class PowerControlInput {
    public bool AcOnline { get; set; }
    public bool PerformanceMode { get; set; }
    public bool CoolFanCurve { get; set; }
    public bool MonitorGpu { get; set; }
    public bool FanControlAuto { get; set; }
    public int ManualCpuLimitWatts { get; set; }
    public GpuPowerTier ManualGpuTier { get; set; }
    public float CpuTemperatureC { get; set; }
    public float CpuPowerWatts { get; set; }
    public float GpuTemperatureC { get; set; }
    public float GpuPowerWatts { get; set; }
    public float BaseSystemPowerWatts { get; set; }
    public float? BatteryDischargePowerWatts { get; set; }
  }

  internal sealed class PowerControlDecision {
    public string State { get; set; }
    public string Reason { get; set; }
    public float EstimatedSystemPowerWatts { get; set; }
    public float TargetSystemPowerWatts { get; set; }
    public int CurrentCpuLimitWatts { get; set; }
    public GpuPowerTier CurrentGpuTier { get; set; }
    public bool FanBoostActive { get; set; }
    public bool ApplyCpuLimit { get; set; }
    public int CpuLimitWatts { get; set; }
    public bool ApplyGpuTier { get; set; }
    public GpuPowerTier GpuTier { get; set; }
    public bool ApplyFanBoost { get; set; }
  }

  internal sealed class PowerController {
    const float CpuEmergencyTemp = 95f;
    const float GpuEmergencyTemp = 87f;
    const float CpuRecoverTemp = 88f;
    const float GpuRecoverTemp = 80f;
    const float CpuFanBoostOnTemp = 92f;
    const float GpuFanBoostOnTemp = 83f;
    const float CpuFanBoostOffTemp = 87f;
    const float GpuFanBoostOffTemp = 78f;
    const int EmergencyHoldSeconds = 10;
    const int CpuAdjustCooldownSeconds = 2;
    const int GpuAdjustCooldownSeconds = 5;
    const int FanAdjustCooldownSeconds = 2;

    bool initialized;
    bool emergencyActive;
    bool fanBoostActive;
    DateTime emergencyExitAllowedUtc = DateTime.MinValue;
    DateTime lastCpuAdjustUtc = DateTime.MinValue;
    DateTime lastGpuAdjustUtc = DateTime.MinValue;
    DateTime lastFanAdjustUtc = DateTime.MinValue;
    int currentCpuLimitWatts;
    GpuPowerTier currentGpuTier = GpuPowerTier.Max;

    public void Reset() {
      initialized = false;
      emergencyActive = false;
      fanBoostActive = false;
      emergencyExitAllowedUtc = DateTime.MinValue;
      lastCpuAdjustUtc = DateTime.MinValue;
      lastGpuAdjustUtc = DateTime.MinValue;
      lastFanAdjustUtc = DateTime.MinValue;
      currentCpuLimitWatts = 0;
      currentGpuTier = GpuPowerTier.Max;
    }

    public PowerControlDecision Evaluate(PowerControlInput input) {
      var now = DateTime.UtcNow;

      if (!initialized) {
        currentCpuLimitWatts = Clamp(input.ManualCpuLimitWatts, 25, 254);
        currentGpuTier = input.ManualGpuTier;
        initialized = true;
      }

      float estimatedSystemPower = EstimateSystemPower(input);
      float targetSystemPower = CalculateTargetPower(input);

      UpdateEmergencyState(input, now);

      int desiredCpuLimit = currentCpuLimitWatts;
      GpuPowerTier desiredGpuTier = currentGpuTier;
      bool desiredFanBoost = fanBoostActive;
      string reason;

      if (emergencyActive) {
        desiredCpuLimit = Math.Min(Clamp(input.ManualCpuLimitWatts, 25, 254), input.AcOnline ? 45 : 35);
        desiredGpuTier = input.MonitorGpu ? GpuPowerTier.Min : input.ManualGpuTier;
        desiredFanBoost = input.FanControlAuto;
        reason = "thermal-emergency";
      } else {
        float overBudget = estimatedSystemPower - targetSystemPower;
        desiredGpuTier = CalculateGpuTier(input, overBudget);
        desiredCpuLimit = CalculateCpuLimit(input, targetSystemPower, desiredGpuTier, overBudget);
        desiredFanBoost = CalculateFanBoost(input);
        reason = overBudget > 0.5f ? "power-budget" : "stable";
      }

      var decision = new PowerControlDecision {
        State = emergencyActive ? "emergency" : "balanced",
        Reason = reason,
        EstimatedSystemPowerWatts = estimatedSystemPower,
        TargetSystemPowerWatts = targetSystemPower,
        CurrentCpuLimitWatts = currentCpuLimitWatts,
        CurrentGpuTier = currentGpuTier,
        FanBoostActive = fanBoostActive,
        CpuLimitWatts = desiredCpuLimit,
        GpuTier = desiredGpuTier
      };

      if (Math.Abs(desiredCpuLimit - currentCpuLimitWatts) >= 3 &&
          (now - lastCpuAdjustUtc).TotalSeconds >= CpuAdjustCooldownSeconds) {
        currentCpuLimitWatts = desiredCpuLimit;
        lastCpuAdjustUtc = now;
        decision.ApplyCpuLimit = true;
        decision.CurrentCpuLimitWatts = currentCpuLimitWatts;
      }

      if (desiredGpuTier != currentGpuTier &&
          (now - lastGpuAdjustUtc).TotalSeconds >= GpuAdjustCooldownSeconds) {
        currentGpuTier = desiredGpuTier;
        lastGpuAdjustUtc = now;
        decision.ApplyGpuTier = true;
        decision.CurrentGpuTier = currentGpuTier;
      }

      if (desiredFanBoost != fanBoostActive &&
          (now - lastFanAdjustUtc).TotalSeconds >= FanAdjustCooldownSeconds) {
        fanBoostActive = desiredFanBoost;
        lastFanAdjustUtc = now;
        decision.ApplyFanBoost = true;
        decision.FanBoostActive = fanBoostActive;
      }

      return decision;
    }

    float EstimateSystemPower(PowerControlInput input) {
      if (input.BatteryDischargePowerWatts.HasValue && input.BatteryDischargePowerWatts.Value > 1f)
        return input.BatteryDischargePowerWatts.Value;

      float gpuPower = input.MonitorGpu ? input.GpuPowerWatts : 0f;
      return Math.Max(0f, input.CpuPowerWatts + gpuPower + input.BaseSystemPowerWatts);
    }

    float CalculateTargetPower(PowerControlInput input) {
      float target;
      if (input.AcOnline) {
        target = input.PerformanceMode ? 82f : 68f;
      } else {
        target = 45f;
      }

      if (input.CoolFanCurve)
        target += 4f;

      if (!input.MonitorGpu)
        target -= 5f;

      return Math.Max(25f, target);
    }

    void UpdateEmergencyState(PowerControlInput input, DateTime now) {
      bool emergencyHit = input.CpuTemperatureC >= CpuEmergencyTemp ||
                          (input.MonitorGpu && input.GpuTemperatureC >= GpuEmergencyTemp);
      bool recoverReady = input.CpuTemperatureC <= CpuRecoverTemp &&
                          (!input.MonitorGpu || input.GpuTemperatureC <= GpuRecoverTemp);

      if (emergencyHit) {
        emergencyActive = true;
        emergencyExitAllowedUtc = DateTime.MinValue;
        return;
      }

      if (!emergencyActive)
        return;

      if (!recoverReady) {
        emergencyExitAllowedUtc = DateTime.MinValue;
        return;
      }

      if (emergencyExitAllowedUtc == DateTime.MinValue)
        emergencyExitAllowedUtc = now.AddSeconds(EmergencyHoldSeconds);

      if (now >= emergencyExitAllowedUtc) {
        emergencyActive = false;
        emergencyExitAllowedUtc = DateTime.MinValue;
      }
    }

    GpuPowerTier CalculateGpuTier(PowerControlInput input, float overBudget) {
      GpuPowerTier desired = input.ManualGpuTier;
      if (!input.MonitorGpu)
        return desired;

      if (input.GpuTemperatureC >= 84f || overBudget >= 16f)
        return MinTier(desired, GpuPowerTier.Min);

      if (input.GpuTemperatureC >= 79f || overBudget >= 8f)
        return MinTier(desired, GpuPowerTier.Med);

      return desired;
    }

    int CalculateCpuLimit(PowerControlInput input, float targetSystemPower, GpuPowerTier desiredGpuTier, float overBudget) {
      int manualCpuLimit = Clamp(input.ManualCpuLimitWatts, 25, 254);
      float gpuReserve = 0f;
      if (input.MonitorGpu) {
        switch (desiredGpuTier) {
          case GpuPowerTier.Max:
            gpuReserve = 36f;
            break;
          case GpuPowerTier.Med:
            gpuReserve = 25f;
            break;
          default:
            gpuReserve = 14f;
            break;
        }
      }

      float thermalPenalty = Math.Max(0f, input.CpuTemperatureC - 88f) * 1.8f;
      float cpuBudget = targetSystemPower - input.BaseSystemPowerWatts - gpuReserve - thermalPenalty;
      int desired = Clamp((int)Math.Round(cpuBudget), 30, manualCpuLimit);

      if (overBudget < -6f && input.CpuTemperatureC < 80f) {
        int recoveryLimit = Clamp((int)Math.Round(input.CpuPowerWatts + 8f), 30, manualCpuLimit);
        desired = Math.Max(desired, recoveryLimit);
      }

      return desired;
    }

    bool CalculateFanBoost(PowerControlInput input) {
      if (!input.FanControlAuto)
        return false;

      if (input.CpuTemperatureC >= CpuFanBoostOnTemp)
        return true;
      if (input.MonitorGpu && input.GpuTemperatureC >= GpuFanBoostOnTemp)
        return true;

      if (fanBoostActive) {
        if (input.CpuTemperatureC >= CpuFanBoostOffTemp)
          return true;
        if (input.MonitorGpu && input.GpuTemperatureC >= GpuFanBoostOffTemp)
          return true;
      }

      return false;
    }

    static GpuPowerTier MinTier(GpuPowerTier first, GpuPowerTier second) {
      return (GpuPowerTier)Math.Min((int)first, (int)second);
    }

    static int Clamp(int value, int min, int max) {
      if (value < min)
        return min;
      if (value > max)
        return max;
      return value;
    }
  }
}
