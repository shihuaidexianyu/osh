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
    public int BatteryPercent { get; set; }
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

  internal sealed class PowerControlTuning {
    public float CpuEmergencyTempC { get; set; }
    public float GpuEmergencyTempC { get; set; }
    public float CpuRecoverTempC { get; set; }
    public float GpuRecoverTempC { get; set; }
    public float CpuFanBoostOnTempC { get; set; }
    public float GpuFanBoostOnTempC { get; set; }
    public float CpuFanBoostOffTempC { get; set; }
    public float GpuFanBoostOffTempC { get; set; }
    public float BatteryGuardTriggerWatts { get; set; }
    public float BatteryGuardReleaseWatts { get; set; }

    public static PowerControlTuning CreateDefault() {
      return new PowerControlTuning {
        CpuEmergencyTempC = 95f,
        GpuEmergencyTempC = 87f,
        CpuRecoverTempC = 88f,
        GpuRecoverTempC = 80f,
        CpuFanBoostOnTempC = 92f,
        GpuFanBoostOnTempC = 83f,
        CpuFanBoostOffTempC = 87f,
        GpuFanBoostOffTempC = 78f,
        BatteryGuardTriggerWatts = 55f,
        BatteryGuardReleaseWatts = 42f
      };
    }

    public PowerControlTuning Clone() {
      return new PowerControlTuning {
        CpuEmergencyTempC = CpuEmergencyTempC,
        GpuEmergencyTempC = GpuEmergencyTempC,
        CpuRecoverTempC = CpuRecoverTempC,
        GpuRecoverTempC = GpuRecoverTempC,
        CpuFanBoostOnTempC = CpuFanBoostOnTempC,
        GpuFanBoostOnTempC = GpuFanBoostOnTempC,
        CpuFanBoostOffTempC = CpuFanBoostOffTempC,
        GpuFanBoostOffTempC = GpuFanBoostOffTempC,
        BatteryGuardTriggerWatts = BatteryGuardTriggerWatts,
        BatteryGuardReleaseWatts = BatteryGuardReleaseWatts
      };
    }
  }

  internal sealed class PowerController {
    const int ThermalHoldSeconds = 12;
    const int BatteryGuardHoldSeconds = 8;
    const int StateMinHoldSeconds = 12;
    const int CpuAdjustCooldownSeconds = 2;
    const int GpuAdjustCooldownSeconds = 10;
    const int FanAdjustCooldownSeconds = 3;
    const int CpuStepWatts = 4;

    enum SmartState {
      Eco,
      Balanced,
      Performance,
      ThermalProtect,
      BatteryGuard
    }

    bool initialized;
    SmartState currentState = SmartState.Balanced;
    DateTime lastStateSwitchUtc = DateTime.MinValue;
    bool thermalProtectActive;
    bool batteryGuardActive;
    bool fanBoostActive;
    DateTime thermalExitAllowedUtc = DateTime.MinValue;
    DateTime batteryGuardTriggerSinceUtc = DateTime.MinValue;
    DateTime batteryGuardExitAllowedUtc = DateTime.MinValue;
    DateTime lastCpuAdjustUtc = DateTime.MinValue;
    DateTime lastGpuAdjustUtc = DateTime.MinValue;
    DateTime lastFanAdjustUtc = DateTime.MinValue;
    int currentCpuLimitWatts;
    GpuPowerTier currentGpuTier = GpuPowerTier.Max;
    PowerControlTuning tuning = NormalizeTuning(PowerControlTuning.CreateDefault());

    public static PowerControlTuning CreateDefaultTuning() {
      return PowerControlTuning.CreateDefault();
    }

    public PowerControlTuning GetTuningSnapshot() {
      return tuning.Clone();
    }

    public void UpdateTuning(PowerControlTuning next) {
      tuning = NormalizeTuning(next ?? PowerControlTuning.CreateDefault());
    }

    public void Reset() {
      initialized = false;
      currentState = SmartState.Balanced;
      lastStateSwitchUtc = DateTime.MinValue;
      thermalProtectActive = false;
      batteryGuardActive = false;
      fanBoostActive = false;
      thermalExitAllowedUtc = DateTime.MinValue;
      batteryGuardTriggerSinceUtc = DateTime.MinValue;
      batteryGuardExitAllowedUtc = DateTime.MinValue;
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
        currentState = SmartState.Balanced;
        lastStateSwitchUtc = now;
        initialized = true;
      }

      float estimatedSystemPower = EstimateSystemPower(input);
      float targetSystemPower = CalculateTargetPower(input);
      float overBudget = estimatedSystemPower - targetSystemPower;

      UpdateProtectionStates(input, now, estimatedSystemPower);
      SmartState desiredState = DecideState(input, overBudget, now);
      bool stateChanged = false;
      if (CanSwitchState(currentState, desiredState, now)) {
        currentState = desiredState;
        lastStateSwitchUtc = now;
        stateChanged = true;
      }

      int desiredCpuLimit = currentCpuLimitWatts;
      GpuPowerTier desiredGpuTier = currentGpuTier;
      bool desiredFanBoost = fanBoostActive;
      desiredGpuTier = CalculateGpuTier(input, currentState, overBudget);
      desiredCpuLimit = CalculateCpuLimit(input, targetSystemPower, desiredGpuTier, overBudget, currentState);
      desiredFanBoost = CalculateFanBoost(input, currentState);
      string reason = BuildReason(currentState, overBudget, stateChanged);

      var decision = new PowerControlDecision {
        State = FormatState(currentState),
        Reason = reason,
        EstimatedSystemPowerWatts = estimatedSystemPower,
        TargetSystemPowerWatts = targetSystemPower,
        CurrentCpuLimitWatts = currentCpuLimitWatts,
        CurrentGpuTier = currentGpuTier,
        FanBoostActive = fanBoostActive,
        CpuLimitWatts = desiredCpuLimit,
        GpuTier = desiredGpuTier
      };

      int nextCpuLimit = StepTowards(currentCpuLimitWatts, desiredCpuLimit, CpuStepWatts);
      if (nextCpuLimit != currentCpuLimitWatts &&
          (now - lastCpuAdjustUtc).TotalSeconds >= CpuAdjustCooldownSeconds) {
        currentCpuLimitWatts = nextCpuLimit;
        lastCpuAdjustUtc = now;
        decision.ApplyCpuLimit = true;
        decision.CurrentCpuLimitWatts = currentCpuLimitWatts;
        decision.CpuLimitWatts = currentCpuLimitWatts;
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

    static int StepTowards(int current, int target, int maxStep) {
      int delta = target - current;
      if (Math.Abs(delta) <= maxStep)
        return target;
      return current + (delta > 0 ? maxStep : -maxStep);
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
        target = input.PerformanceMode ? 86f : 72f;
      } else {
        target = 42f;
        if (input.BatteryPercent > 0) {
          if (input.BatteryPercent <= 20)
            target = 30f;
          else if (input.BatteryPercent <= 35)
            target = 36f;
        }
      }

      if (input.CoolFanCurve)
        target += 4f;

      if (!input.MonitorGpu)
        target -= 6f;

      if (!input.FanControlAuto)
        target -= 2f;

      return Math.Max(25f, Math.Min(120f, target));
    }

    void UpdateProtectionStates(PowerControlInput input, DateTime now, float estimatedSystemPower) {
      bool thermalHit = input.CpuTemperatureC >= tuning.CpuEmergencyTempC ||
                        (input.MonitorGpu && input.GpuTemperatureC >= tuning.GpuEmergencyTempC);
      bool thermalRecoverReady = input.CpuTemperatureC <= tuning.CpuRecoverTempC &&
                                 (!input.MonitorGpu || input.GpuTemperatureC <= tuning.GpuRecoverTempC);

      if (thermalHit) {
        thermalProtectActive = true;
        thermalExitAllowedUtc = DateTime.MinValue;
      } else if (thermalProtectActive) {
        if (!thermalRecoverReady) {
          thermalExitAllowedUtc = DateTime.MinValue;
        } else {
          if (thermalExitAllowedUtc == DateTime.MinValue)
            thermalExitAllowedUtc = now.AddSeconds(ThermalHoldSeconds);
          if (now >= thermalExitAllowedUtc) {
            thermalProtectActive = false;
            thermalExitAllowedUtc = DateTime.MinValue;
          }
        }
      }

      bool batteryGuardSignal = !input.AcOnline &&
                                (estimatedSystemPower >= tuning.BatteryGuardTriggerWatts ||
                                 (input.BatteryDischargePowerWatts.HasValue &&
                                  input.BatteryDischargePowerWatts.Value >= tuning.BatteryGuardTriggerWatts));
      bool batteryRecoverReady = !input.AcOnline &&
                                 estimatedSystemPower <= tuning.BatteryGuardReleaseWatts &&
                                 input.CpuTemperatureC <= 82f &&
                                 (!input.MonitorGpu || input.GpuTemperatureC <= 74f);

      if (batteryGuardSignal) {
        if (batteryGuardTriggerSinceUtc == DateTime.MinValue)
          batteryGuardTriggerSinceUtc = now;

        if ((now - batteryGuardTriggerSinceUtc).TotalSeconds >= BatteryGuardHoldSeconds) {
          batteryGuardActive = true;
          batteryGuardExitAllowedUtc = DateTime.MinValue;
        }
      } else {
        batteryGuardTriggerSinceUtc = DateTime.MinValue;
      }

      if (batteryGuardActive) {
        if (!batteryRecoverReady) {
          batteryGuardExitAllowedUtc = DateTime.MinValue;
        } else {
          if (batteryGuardExitAllowedUtc == DateTime.MinValue)
            batteryGuardExitAllowedUtc = now.AddSeconds(BatteryGuardHoldSeconds);
          if (now >= batteryGuardExitAllowedUtc) {
            batteryGuardActive = false;
            batteryGuardExitAllowedUtc = DateTime.MinValue;
          }
        }
      }
    }

    SmartState DecideState(PowerControlInput input, float overBudget, DateTime now) {
      if (thermalProtectActive)
        return SmartState.ThermalProtect;

      if (batteryGuardActive)
        return SmartState.BatteryGuard;

      bool cpuCool = input.CpuTemperatureC < 80f;
      bool gpuCool = !input.MonitorGpu || input.GpuTemperatureC < 72f;
      bool highLoad = input.CpuPowerWatts >= 35f || (input.MonitorGpu && input.GpuPowerWatts >= 30f);

      if (!input.AcOnline) {
        if (input.BatteryPercent > 0 && input.BatteryPercent <= 20)
          return SmartState.Eco;

        if (overBudget >= 4f)
          return SmartState.Eco;

        if (highLoad && cpuCool && gpuCool && overBudget <= -6f)
          return SmartState.Balanced;

        return SmartState.Eco;
      }

      if (overBudget >= 10f || input.CpuTemperatureC >= 90f || (input.MonitorGpu && input.GpuTemperatureC >= 82f))
        return SmartState.Balanced;

      if (highLoad && cpuCool && gpuCool && input.PerformanceMode && overBudget <= 2f)
        return SmartState.Performance;

      if (overBudget <= -10f && cpuCool && gpuCool && !input.PerformanceMode)
        return SmartState.Eco;

      return SmartState.Balanced;
    }

    bool CanSwitchState(SmartState current, SmartState next, DateTime now) {
      if (current == next)
        return false;

      bool nextProtective = next == SmartState.ThermalProtect || next == SmartState.BatteryGuard;
      bool currentProtective = current == SmartState.ThermalProtect || current == SmartState.BatteryGuard;

      if (nextProtective)
        return true;

      if (currentProtective)
        return (now - lastStateSwitchUtc).TotalSeconds >= StateMinHoldSeconds;

      return (now - lastStateSwitchUtc).TotalSeconds >= StateMinHoldSeconds;
    }

    string FormatState(SmartState state) {
      switch (state) {
        case SmartState.Eco:
          return "eco";
        case SmartState.Performance:
          return "performance";
        case SmartState.ThermalProtect:
          return "thermal_protect";
        case SmartState.BatteryGuard:
          return "battery_guard";
        default:
          return "balanced";
      }
    }

    string BuildReason(SmartState state, float overBudget, bool stateChanged) {
      switch (state) {
        case SmartState.ThermalProtect:
          return "thermal-ceiling";
        case SmartState.BatteryGuard:
          return "battery-discharge";
        case SmartState.Performance:
          return stateChanged ? "promote-performance" : "performance-window";
        case SmartState.Eco:
          return overBudget > 0f ? "power-saving" : "eco-stable";
        default:
          if (overBudget > 1.5f)
            return "budget-limit";
          return stateChanged ? "state-stabilizing" : "balanced-stable";
      }
    }

    GpuPowerTier CalculateGpuTier(PowerControlInput input, SmartState state, float overBudget) {
      GpuPowerTier manualTier = input.ManualGpuTier;
      if (!input.MonitorGpu)
        return manualTier;

      GpuPowerTier desired;
      switch (state) {
        case SmartState.ThermalProtect:
        case SmartState.BatteryGuard:
          desired = GpuPowerTier.Min;
          break;
        case SmartState.Eco:
          desired = input.GpuTemperatureC >= 74f ? GpuPowerTier.Min : GpuPowerTier.Med;
          break;
        case SmartState.Performance:
          desired = input.GpuTemperatureC >= 81f ? GpuPowerTier.Med : GpuPowerTier.Max;
          break;
        default:
          if (input.GpuTemperatureC >= 83f || overBudget >= 14f)
            desired = GpuPowerTier.Min;
          else if (input.GpuTemperatureC >= 77f || overBudget >= 7f)
            desired = GpuPowerTier.Med;
          else
            desired = input.PerformanceMode ? GpuPowerTier.Max : GpuPowerTier.Med;
          break;
      }

      return MinTier(desired, manualTier);
    }

    int CalculateCpuLimit(PowerControlInput input, float targetSystemPower, GpuPowerTier desiredGpuTier, float overBudget, SmartState state) {
      int manualCpuLimit = Clamp(input.ManualCpuLimitWatts, 25, 254);
      float gpuReserve = GetGpuReserveWatts(input, desiredGpuTier);
      float thermalPenalty = Math.Max(0f, input.CpuTemperatureC - 84f) * 1.4f;
      float cpuBudget = targetSystemPower - input.BaseSystemPowerWatts - gpuReserve - thermalPenalty;
      int desired;

      switch (state) {
        case SmartState.ThermalProtect:
          desired = Math.Min(manualCpuLimit, input.AcOnline ? 45 : 35);
          break;
        case SmartState.BatteryGuard:
          desired = Math.Min(manualCpuLimit, input.AcOnline ? 55 : 38);
          break;
        case SmartState.Eco:
          desired = Clamp((int)Math.Round(cpuBudget), 30, Math.Min(manualCpuLimit, input.AcOnline ? 70 : 50));
          if (input.BatteryPercent > 0 && input.BatteryPercent <= 20)
            desired = Math.Min(desired, 35);
          break;
        case SmartState.Performance:
          desired = Clamp((int)Math.Round(cpuBudget + 6f), 45, Math.Min(manualCpuLimit, input.AcOnline ? 125 : 70));
          break;
        default:
          desired = Clamp((int)Math.Round(cpuBudget), 35, Math.Min(manualCpuLimit, input.AcOnline ? 95 : 60));
          break;
      }

      if (overBudget >= 10f)
        desired = Math.Max(30, desired - 6);
      else if (overBudget >= 5f)
        desired = Math.Max(30, desired - 3);

      if (overBudget <= -10f && input.CpuTemperatureC < 80f)
        desired = Math.Min(Math.Min(manualCpuLimit, input.AcOnline ? 125 : 70), desired + 4);

      return Clamp(desired, 25, manualCpuLimit);
    }

    float GetGpuReserveWatts(PowerControlInput input, GpuPowerTier tier) {
      if (!input.MonitorGpu)
        return 0f;

      switch (tier) {
        case GpuPowerTier.Max:
          return input.AcOnline ? 40f : 28f;
        case GpuPowerTier.Med:
          return input.AcOnline ? 28f : 18f;
        default:
          return input.AcOnline ? 16f : 10f;
      }
    }

    bool CalculateFanBoost(PowerControlInput input, SmartState state) {
      if (!input.FanControlAuto)
        return false;

      if (state == SmartState.ThermalProtect || state == SmartState.BatteryGuard)
        return true;

      if (input.CpuTemperatureC >= tuning.CpuFanBoostOnTempC)
        return true;
      if (input.MonitorGpu && input.GpuTemperatureC >= tuning.GpuFanBoostOnTempC)
        return true;

      if (state == SmartState.Performance && input.CpuTemperatureC >= 88f)
        return true;

      if (fanBoostActive) {
        if (input.CpuTemperatureC >= tuning.CpuFanBoostOffTempC)
          return true;
        if (input.MonitorGpu && input.GpuTemperatureC >= tuning.GpuFanBoostOffTempC)
          return true;
      }

      return false;
    }

    static PowerControlTuning NormalizeTuning(PowerControlTuning raw) {
      var t = raw == null ? PowerControlTuning.CreateDefault() : raw.Clone();

      t.CpuEmergencyTempC = ClampFloat(t.CpuEmergencyTempC, 85f, 102f);
      t.GpuEmergencyTempC = ClampFloat(t.GpuEmergencyTempC, 78f, 95f);
      t.CpuRecoverTempC = ClampFloat(t.CpuRecoverTempC, 70f, 99f);
      t.GpuRecoverTempC = ClampFloat(t.GpuRecoverTempC, 65f, 92f);
      t.CpuFanBoostOnTempC = ClampFloat(t.CpuFanBoostOnTempC, 75f, 100f);
      t.GpuFanBoostOnTempC = ClampFloat(t.GpuFanBoostOnTempC, 70f, 95f);
      t.CpuFanBoostOffTempC = ClampFloat(t.CpuFanBoostOffTempC, 65f, 98f);
      t.GpuFanBoostOffTempC = ClampFloat(t.GpuFanBoostOffTempC, 60f, 90f);
      t.BatteryGuardTriggerWatts = ClampFloat(t.BatteryGuardTriggerWatts, 30f, 100f);
      t.BatteryGuardReleaseWatts = ClampFloat(t.BatteryGuardReleaseWatts, 20f, 90f);

      if (t.CpuRecoverTempC > t.CpuEmergencyTempC - 2f)
        t.CpuRecoverTempC = t.CpuEmergencyTempC - 2f;
      if (t.GpuRecoverTempC > t.GpuEmergencyTempC - 2f)
        t.GpuRecoverTempC = t.GpuEmergencyTempC - 2f;
      if (t.CpuFanBoostOffTempC > t.CpuFanBoostOnTempC - 1f)
        t.CpuFanBoostOffTempC = t.CpuFanBoostOnTempC - 1f;
      if (t.GpuFanBoostOffTempC > t.GpuFanBoostOnTempC - 1f)
        t.GpuFanBoostOffTempC = t.GpuFanBoostOnTempC - 1f;
      if (t.BatteryGuardReleaseWatts > t.BatteryGuardTriggerWatts - 3f)
        t.BatteryGuardReleaseWatts = t.BatteryGuardTriggerWatts - 3f;

      return t;
    }

    static float ClampFloat(float value, float min, float max) {
      if (value < min)
        return min;
      if (value > max)
        return max;
      return value;
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
