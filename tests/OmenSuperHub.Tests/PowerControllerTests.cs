using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OmenSuperHub.Tests {
  [TestClass]
  public class PowerControllerTests {
    static PowerControlInput CreateBaseInput() {
      return new PowerControlInput {
        AcOnline = true,
        PerformanceMode = true,
        CoolFanCurve = false,
        MonitorGpu = true,
        FanControlAuto = true,
        ManualCpuLimitWatts = 95,
        ManualGpuTier = GpuPowerTier.Max,
        CpuTemperatureC = 78f,
        CpuPowerWatts = 45f,
        GpuTemperatureC = 70f,
        GpuPowerWatts = 55f,
        BaseSystemPowerWatts = 14f,
        BatteryPercent = 100
      };
    }

    [TestMethod]
    public void Evaluate_AtEmergencyTemperature_EntersThermalProtectState() {
      var controller = new PowerController();
      var input = CreateBaseInput();
      input.CpuTemperatureC = 98f;

      PowerControlDecision decision = controller.Evaluate(input);

      Assert.AreEqual("thermal_protect", decision.State);
      Assert.AreEqual("thermal-ceiling", decision.Reason);
      Assert.IsTrue(decision.GpuTier == GpuPowerTier.Min || decision.CurrentGpuTier == GpuPowerTier.Min);
    }

    [TestMethod]
    public void Evaluate_RespectsManualGpuTierCap() {
      var controller = new PowerController();
      var input = CreateBaseInput();
      input.ManualGpuTier = GpuPowerTier.Med;
      input.CpuTemperatureC = 65f;
      input.GpuTemperatureC = 62f;
      input.CpuPowerWatts = 25f;
      input.GpuPowerWatts = 18f;

      PowerControlDecision decision = controller.Evaluate(input);

      Assert.AreEqual(GpuPowerTier.Med, decision.CurrentGpuTier);
      Assert.IsTrue((int)decision.GpuTier <= (int)GpuPowerTier.Med);
    }

    [TestMethod]
    public void UpdateTuning_NormalizesOutOfRangeValues() {
      var controller = new PowerController();
      controller.UpdateTuning(new PowerControlTuning {
        CpuEmergencyTempC = 200f,
        GpuEmergencyTempC = 50f,
        CpuRecoverTempC = 199f,
        GpuRecoverTempC = 49f,
        CpuFanBoostOnTempC = 10f,
        GpuFanBoostOnTempC = 200f,
        CpuFanBoostOffTempC = 200f,
        GpuFanBoostOffTempC = 10f,
        CpuTempWallC = 200f,
        GpuTempWallC = 10f,
        CpuWallDeadbandC = -2f,
        GpuWallDeadbandC = 99f,
        BatteryGuardTriggerWatts = 10f,
        BatteryGuardReleaseWatts = 100f
      });

      PowerControlTuning tuning = controller.GetTuningSnapshot();

      Assert.IsTrue(tuning.CpuEmergencyTempC <= 102f);
      Assert.IsTrue(tuning.GpuEmergencyTempC >= 78f);
      Assert.IsTrue(tuning.CpuRecoverTempC <= tuning.CpuEmergencyTempC - 2f);
      Assert.IsTrue(tuning.GpuRecoverTempC <= tuning.GpuEmergencyTempC - 2f);
      Assert.IsTrue(tuning.CpuWallDeadbandC >= 0.3f && tuning.CpuWallDeadbandC <= 4f);
      Assert.IsTrue(tuning.GpuWallDeadbandC >= 0.3f && tuning.GpuWallDeadbandC <= 4f);
      Assert.IsTrue(tuning.BatteryGuardReleaseWatts <= tuning.BatteryGuardTriggerWatts - 3f);
    }
  }
}
