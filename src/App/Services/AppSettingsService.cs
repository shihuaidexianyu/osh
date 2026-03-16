using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace OmenSuperHub {
  [DataContract]
  internal sealed class AppSettingsSnapshot {
    [DataMember]
    public string UsageMode { get; set; } = "balanced";
    [DataMember]
    public string FanTable { get; set; } = "silent";
    [DataMember]
    public string FanMode { get; set; } = "performance";
    [DataMember]
    public string FanControl { get; set; } = "auto";
    [DataMember]
    public string TempSensitivity { get; set; } = "high";
    [DataMember]
    public string CpuPower { get; set; } = "max";
    [DataMember]
    public string GpuPower { get; set; } = "max";
    [DataMember]
    public int GpuClock { get; set; }
    [DataMember]
    public string AutoStart { get; set; } = "off";
    [DataMember]
    public int AlreadyRead { get; set; }
    [DataMember]
    public string CustomIcon { get; set; } = "original";
    [DataMember]
    public string OmenKey { get; set; } = "default";
    [DataMember]
    public bool MonitorFan { get; set; } = true;
    [DataMember]
    public bool SmartPowerControlEnabled { get; set; } = true;
    [DataMember]
    public int FloatingBarSize { get; set; } = 48;
    [DataMember]
    public string FloatingBarLocation { get; set; } = "left";
    [DataMember]
    public string FloatingBar { get; set; } = "off";
    [DataMember]
    public PowerControlTuning PowerControlTuning { get; set; } = PowerController.CreateDefaultTuning();
    [DataMember]
    public List<FanCurveConfigProfile> FanCurveProfiles { get; set; } = new List<FanCurveConfigProfile>();
  }

  internal sealed class AppSettingsService {
    readonly string configFilePath;

    public AppSettingsService(string configFilePath = null) {
      this.configFilePath = string.IsNullOrWhiteSpace(configFilePath)
        ? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OmenSuperHub",
            "settings.json")
        : configFilePath;
    }

    public string ConfigFilePath => configFilePath;

    public bool TryLoadConfig(out AppSettingsSnapshot snapshot) {
      snapshot = new AppSettingsSnapshot();
      if (!File.Exists(configFilePath)) {
        return false;
      }

      try {
        using (var stream = File.OpenRead(configFilePath)) {
          var serializer = new DataContractJsonSerializer(typeof(AppSettingsSnapshot));
          snapshot = NormalizeSnapshot(serializer.ReadObject(stream) as AppSettingsSnapshot);
          return true;
        }
      } catch (Exception ex) {
        Console.WriteLine($"Error restoring configuration: {ex.Message}");
        snapshot = new AppSettingsSnapshot();
        return false;
      }
    }

    public void SaveConfig(AppSettingsSnapshot snapshot, string configName = null) {
      if (snapshot == null) {
        return;
      }

      try {
        AppSettingsSnapshot document = ReadSnapshotOrDefault();
        CopyUserSettings(document, snapshot);
        WriteSnapshot(document);
      } catch (Exception ex) {
        Console.WriteLine($"Error saving configuration: {ex.Message}");
      }
    }

    public void SavePowerControlTuning(PowerControlTuning tuning) {
      if (tuning == null) {
        return;
      }

      try {
        AppSettingsSnapshot document = ReadSnapshotOrDefault();
        document.PowerControlTuning = tuning.Clone();
        WriteSnapshot(document);
      } catch (Exception ex) {
        Console.WriteLine($"Error saving power tuning: {ex.Message}");
      }
    }

    public PowerControlTuning LoadPowerControlTuning() {
      AppSettingsSnapshot snapshot = ReadSnapshotOrDefault();
      return snapshot.PowerControlTuning == null
        ? PowerController.CreateDefaultTuning()
        : snapshot.PowerControlTuning.Clone();
    }

    public List<FanCurveConfigProfile> LoadFanCurveProfiles() {
      AppSettingsSnapshot snapshot = ReadSnapshotOrDefault();
      return CloneProfiles(snapshot.FanCurveProfiles);
    }

    public void SaveFanCurveProfiles(IEnumerable<FanCurveConfigProfile> profiles) {
      try {
        AppSettingsSnapshot document = ReadSnapshotOrDefault();
        document.FanCurveProfiles = CloneProfiles(profiles);
        WriteSnapshot(document);
      } catch (Exception ex) {
        Console.WriteLine($"Error saving fan curves: {ex.Message}");
      }
    }

    AppSettingsSnapshot ReadSnapshotOrDefault() {
      return TryLoadConfig(out AppSettingsSnapshot snapshot)
        ? snapshot
        : new AppSettingsSnapshot();
    }

    void WriteSnapshot(AppSettingsSnapshot snapshot) {
      Directory.CreateDirectory(Path.GetDirectoryName(configFilePath));
      using (var stream = File.Create(configFilePath)) {
        var serializer = new DataContractJsonSerializer(typeof(AppSettingsSnapshot));
        serializer.WriteObject(stream, NormalizeSnapshot(snapshot));
      }
    }

    static void CopyUserSettings(AppSettingsSnapshot target, AppSettingsSnapshot source) {
      target.UsageMode = source.UsageMode;
      target.FanTable = source.FanTable;
      target.FanMode = source.FanMode;
      target.FanControl = source.FanControl;
      target.TempSensitivity = source.TempSensitivity;
      target.CpuPower = source.CpuPower;
      target.GpuPower = source.GpuPower;
      target.GpuClock = source.GpuClock;
      target.AutoStart = source.AutoStart;
      target.AlreadyRead = source.AlreadyRead;
      target.CustomIcon = source.CustomIcon;
      target.OmenKey = source.OmenKey;
      target.MonitorFan = source.MonitorFan;
      target.SmartPowerControlEnabled = source.SmartPowerControlEnabled;
      target.FloatingBarSize = source.FloatingBarSize;
      target.FloatingBarLocation = source.FloatingBarLocation;
      target.FloatingBar = source.FloatingBar;
    }

    static AppSettingsSnapshot NormalizeSnapshot(AppSettingsSnapshot snapshot) {
      snapshot = snapshot ?? new AppSettingsSnapshot();
      snapshot.UsageMode = RuntimeControlSettings.ToStorageValue(RuntimeControlSettings.ParseUsageMode(snapshot.UsageMode));
      snapshot.FanTable = RuntimeControlSettings.ToStorageValue(RuntimeControlSettings.ParseFanTable(snapshot.FanTable));
      snapshot.FanMode = RuntimeControlSettings.ToStorageValue(RuntimeControlSettings.ParseFanMode(snapshot.FanMode));
      snapshot.FanControl = RuntimeControlSettings.ToStorageValue(
        RuntimeControlSettings.ParseFanControl(snapshot.FanControl, out int manualFanRpm),
        manualFanRpm);
      snapshot.TempSensitivity = RuntimeControlSettings.ToStorageValue(RuntimeControlSettings.ParseTempSensitivity(snapshot.TempSensitivity));
      snapshot.CpuPower = RuntimeControlSettings.ToCpuPowerStorageValue(
        RuntimeControlSettings.IsCpuPowerMax(snapshot.CpuPower),
        RuntimeControlSettings.ParseCpuPowerWatts(snapshot.CpuPower));
      snapshot.GpuPower = RuntimeControlSettings.ToStorageValue(RuntimeControlSettings.ParseGpuPower(snapshot.GpuPower));
      snapshot.GpuClock = Math.Max(0, snapshot.GpuClock);
      snapshot.AutoStart = snapshot.AutoStart == "on" ? "on" : "off";
      snapshot.CustomIcon = NormalizeCustomIcon(snapshot.CustomIcon);
      snapshot.OmenKey = NormalizeOmenKey(snapshot.OmenKey);
      snapshot.FloatingBarSize = NormalizeFloatingBarSize(snapshot.FloatingBarSize);
      snapshot.FloatingBarLocation = snapshot.FloatingBarLocation == "right" ? "right" : "left";
      snapshot.FloatingBar = snapshot.FloatingBar == "on" ? "on" : "off";
      snapshot.PowerControlTuning = snapshot.PowerControlTuning == null
        ? PowerController.CreateDefaultTuning()
        : snapshot.PowerControlTuning.Clone();
      snapshot.FanCurveProfiles = CloneProfiles(snapshot.FanCurveProfiles);
      return snapshot;
    }

    static string NormalizeCustomIcon(string value) {
      switch (value) {
        case "custom":
        case "dynamic":
          return value;
        default:
          return "original";
      }
    }

    static string NormalizeOmenKey(string value) {
      switch (value) {
        case "custom":
        case "none":
          return value;
        default:
          return "default";
      }
    }

    static int NormalizeFloatingBarSize(int value) {
      switch (value) {
        case 24:
        case 36:
          return value;
        default:
          return 48;
      }
    }

    static List<FanCurveConfigProfile> CloneProfiles(IEnumerable<FanCurveConfigProfile> profiles) {
      if (profiles == null) {
        return new List<FanCurveConfigProfile>();
      }

      return profiles
        .Where(profile => profile != null)
        .Select(CloneProfile)
        .ToList();
    }

    static FanCurveConfigProfile CloneProfile(FanCurveConfigProfile profile) {
      return new FanCurveConfigProfile {
        Name = profile.Name,
        Entries = profile.Entries == null
          ? new List<FanCurveConfigEntry>()
          : profile.Entries
              .Where(entry => entry != null)
              .Select(entry => new FanCurveConfigEntry {
                CpuTemperature = entry.CpuTemperature,
                CpuFan1Rpm = entry.CpuFan1Rpm,
                CpuFan2Rpm = entry.CpuFan2Rpm,
                GpuTemperature = entry.GpuTemperature,
                GpuFan1Rpm = entry.GpuFan1Rpm,
                GpuFan2Rpm = entry.GpuFan2Rpm
              })
              .ToList()
      };
    }
  }
}
