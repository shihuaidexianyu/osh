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
    public void DashboardSnapshotBuilder_Build_ClonesCollectionsAndNestedModels() {
      var builder = new DashboardSnapshotBuilder();
      var state = new AppRuntimeState {
        CpuTemperature = 81.5f,
        GpuTemperature = 74.2f,
        FanSpeeds = new List<int> { 23, 27 },
        AutoStartEnabled = true,
        OmenKeyMode = "custom",
        TemperatureSensors = new List<TemperatureSensorReading> {
          new TemperatureSensorReading { Name = "CPU Package", Celsius = 81.5f }
        },
        GpuStatus = new OmenGpuStatus {
          CustomTgpEnabled = true,
          RawData = new byte[] { 1, 2, 3 }
        },
        Battery = new BatteryTelemetry {
          Discharging = true,
          DischargeRateMilliwatts = 45200
        }
      };

      DashboardSnapshot snapshot = builder.Build(state);
      state.FanSpeeds[0] = 99;
      state.TemperatureSensors[0].Celsius = 10f;
      state.GpuStatus.RawData[0] = 9;

      Assert.AreEqual(23, snapshot.FanSpeeds[0]);
      Assert.IsTrue(snapshot.AutoStartEnabled);
      Assert.AreEqual("custom", snapshot.OmenKeyMode);
      Assert.AreEqual(81.5f, snapshot.TemperatureSensors[0].Celsius);
      Assert.AreEqual(1, snapshot.GpuStatus.RawData[0]);
      Assert.AreEqual(45.2f, HardwareTelemetryService.GetBatteryPowerWatts(snapshot.Battery));
    }

    [TestMethod]
    public void ShellStatusBuilder_Build_UsesBatteryPowerAndFloatingSummary() {
      var builder = new ShellStatusBuilder();
      var state = new AppRuntimeState {
        CpuTemperature = 82f,
        CpuPowerWatts = 48f,
        GpuTemperature = 71f,
        GpuPowerWatts = 62f,
        MonitorGpu = true,
        MonitorFan = true,
        FanSpeeds = new List<int> { 23, 27 },
        AcOnline = false,
        FloatingBarEnabled = true,
        FloatingBarLocation = "right",
        FloatingBarTextSize = 36,
        CustomIconMode = "dynamic",
        GraphicsMode = OmenGfxMode.Discrete,
        Battery = new BatteryTelemetry {
          Discharging = true,
          DischargeRateMilliwatts = 58600
        }
      };

      AppShellStatus status = builder.Build(state, @"C:\App", mainWindowVisible: false);

      StringAssert.Contains(status.TrayText, "BAT 59W");
      StringAssert.Contains(status.TrayText, "Discrete");
      StringAssert.Contains(status.FloatingText, "SYS: 58.6W (BAT)");
      StringAssert.Contains(status.FloatingText, "FAN: 2300/2700 RPM");
      Assert.IsTrue(status.FloatingVisible);
      Assert.AreEqual("right", status.FloatingLocation);
      Assert.AreEqual(36, status.FloatingTextSize);
      Assert.AreEqual(@"C:\App\\custom.ico".Replace(@"\\", @"\"), status.CustomIconPath);
    }

    [TestMethod]
    public void FanCurveService_LoadConfig_SavesXmlProfileInsteadOfLegacyTextFile() {
      string tempDir = Path.Combine(Path.GetTempPath(), "OmenSuperHub.Tests", Path.GetRandomFileName());
      string configPath = Path.Combine(tempDir, "fan-curves.xml");

      Directory.CreateDirectory(tempDir);
      try {
        var service = new FanCurveService(new FakeHardwareGateway(), configPath);

        service.LoadConfig("silent");

        Assert.IsTrue(File.Exists(configPath));
        string xml = File.ReadAllText(configPath);
        StringAssert.Contains(xml, "<Name>silent</Name>");
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
