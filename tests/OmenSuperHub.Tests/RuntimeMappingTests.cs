using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static OmenSuperHub.OmenHardware;

namespace OmenSuperHub.Tests {
  [TestClass]
  public class RuntimeMappingTests {
    [TestMethod]
    public void RuntimeControlSettings_FromSnapshot_ParsesManualFanControlAndGpuClock() {
      var snapshot = new AppSettingsSnapshot {
        FanMode = "performance",
        FanControl = "3300 RPM",
        FanTable = "cool",
        TempSensitivity = "realtime",
        CpuPower = "90 W",
        GpuPower = "min",
        GpuClock = 2200,
        SmartPowerControlEnabled = false
      };

      RuntimeControlSettings settings = RuntimeControlSettings.FromSnapshot(snapshot);

      Assert.AreEqual(FanModeOption.Performance, settings.FanMode);
      Assert.AreEqual(FanControlOption.Manual, settings.FanControl);
      Assert.AreEqual(3300, settings.ManualFanRpm);
      Assert.AreEqual(FanTableOption.Cool, settings.FanTable);
      Assert.AreEqual(TempSensitivityOption.Realtime, settings.TempSensitivity);
      Assert.IsFalse(settings.CpuPowerMax);
      Assert.AreEqual(90, settings.CpuPowerWatts);
      Assert.AreEqual(GpuPowerOption.Min, settings.GpuPower);
      Assert.AreEqual(2200, settings.GpuClockLimitMhz);
      Assert.IsFalse(settings.SmartPowerControlEnabled);
    }

    [TestMethod]
    public void SettingsRestoreService_BuildPlan_NormalizesSnapshotAndProducesMenuSelections() {
      var service = new SettingsRestoreService(new AppSettingsService());
      var snapshot = new AppSettingsSnapshot {
        UsageMode = "performance",
        FanMode = "performance",
        FanControl = "3100 RPM",
        FanTable = "cool",
        TempSensitivity = "high",
        CpuPower = "75 W",
        GpuPower = "max",
        GpuClock = 2400,
        AutoStart = "on",
        CustomIcon = "dynamic",
        OmenKey = "custom",
        MonitorFan = false,
        FloatingBarSize = 36,
        FloatingBarLocation = "right",
        FloatingBar = "on"
      };

      SettingsRestorePlan plan = service.BuildPlan(snapshot);

      Assert.AreEqual("performance", plan.UsageMode);
      Assert.AreEqual("on", plan.AutoStart);
      Assert.AreEqual("dynamic", plan.CustomIcon);
      Assert.AreEqual("custom", plan.OmenKey);
      Assert.AreEqual(36, plan.FloatingBarSize);
      Assert.AreEqual("right", plan.FloatingBarLocation);
      Assert.AreEqual("on", plan.FloatingBar);
      CollectionAssert.Contains(GetSelectionKeys(plan), "autoStartGroup:开启");
      CollectionAssert.Contains(GetSelectionKeys(plan), "fanControlGroup:3100 RPM");
      CollectionAssert.Contains(GetSelectionKeys(plan), "gpuClockGroup:2400 MHz");
      CollectionAssert.Contains(GetSelectionKeys(plan), "omenKeyGroup:切换浮窗显示");
      CollectionAssert.Contains(GetSelectionKeys(plan), "monitorFanGroup:关闭风扇监控");
      CollectionAssert.Contains(GetSelectionKeys(plan), "floatingBarGroup:显示浮窗");
    }

    [TestMethod]
    public void FanCurveService_LoadConfig_SavesProfileIntoSingleSettingsFile() {
      string tempDir = Path.Combine(Path.GetTempPath(), "OmenSuperHub.Tests", Path.GetRandomFileName());
      string configPath = Path.Combine(tempDir, "settings.json");

      Directory.CreateDirectory(tempDir);
      try {
        var settingsService = new AppSettingsService(configPath);
        var service = new FanCurveService(new FakeHardwareGateway(), settingsService);

        service.LoadConfig("silent");

        Assert.IsTrue(File.Exists(configPath));
        Assert.IsTrue(settingsService.TryLoadConfig(out AppSettingsSnapshot snapshot));
        Assert.AreEqual(1, snapshot.FanCurveProfiles.Count);
        Assert.AreEqual("silent", snapshot.FanCurveProfiles[0].Name);
        Assert.AreEqual(1600, service.GetFanSpeedForTemperature(50f, 40f, monitorGpu: false, fanIndex: 0));
      } finally {
        if (Directory.Exists(tempDir)) {
          Directory.Delete(tempDir, recursive: true);
        }
      }
    }

    static List<string> GetSelectionKeys(SettingsRestorePlan plan) {
      var keys = new List<string>();
      foreach (CheckedMenuSelection selection in plan.CheckedMenuSelections) {
        keys.Add(selection.Group + ":" + selection.ItemText);
      }
      return keys;
    }

    sealed class FakeHardwareGateway : IOmenHardwareGateway {
      public OmenSystemDesignData SystemDesignData { get; set; }
      public void GetFanCount() { }
      public List<int> GetFanLevel() { return new List<int> { 0, 0 }; }
      public byte[] GetFanTable() { return new byte[0]; }
      public OmenFanTypeInfo GetFanTypeInfo() { return null; }
      public OmenGfxMode GetGraphicsMode() { return OmenGfxMode.Unknown; }
      public OmenGpuStatus GetGpuStatus() { return null; }
      public OmenSystemDesignData GetSystemDesignData() { return SystemDesignData; }
      public void SetFanLevel(int fanSpeed1, int fanSpeed2) { }
      public void SetFanMode(byte mode) { }
      public void SetMaxGpuPower() { }
      public void SetMedGpuPower() { }
      public void SetMinGpuPower() { }
      public void SetCpuPowerLimit(byte value) { }
      public void SetMaxFanSpeedOn() { }
      public void SetMaxFanSpeedOff() { }
      public OmenKeyboardType GetKeyboardType() { return OmenKeyboardType.Unknown; }
      public OmenSmartAdapterStatus GetSmartAdapterStatus() { return OmenSmartAdapterStatus.Unknown; }
      public byte[] SendOmenBiosWmi(uint commandType, byte[] data, int outputSize, uint command = 0x20008) { return null; }
      public void OmenKeyOff() { }
      public void OmenKeyOn(string method) { }
    }
  }
}
