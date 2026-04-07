using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using System.Runtime.Serialization;
using System.Text;

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
    public string OmenKey { get; set; } = "default";
    [DataMember]
    public bool MonitorFan { get; set; } = true;
    [DataMember]
    public bool SmartPowerControlEnabled { get; set; } = true;
    [DataMember]
    public PowerControlTuning PowerControlTuning { get; set; } = PowerController.CreateDefaultTuning();
    [DataMember]
    public List<FanCurveConfigProfile> FanCurveProfiles { get; set; } = new List<FanCurveConfigProfile>();
  }

  internal sealed class AppSettingsService {
    readonly string configFilePath;

    public AppSettingsService(string configFilePath = null) {
      if (!string.IsNullOrWhiteSpace(configFilePath)) {
        this.configFilePath = configFilePath;
        return;
      }

      string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
      string currentPath = Path.Combine(localAppData, "osh", "settings.json");
      string legacyPath = Path.Combine(localAppData, "OmenSuperHub", "settings.json");

      this.configFilePath = File.Exists(currentPath)
        ? currentPath
        : (File.Exists(legacyPath) ? legacyPath : currentPath);
    }

    public string ConfigFilePath => configFilePath;

    public bool TryLoadConfig(out AppSettingsSnapshot snapshot) {
      snapshot = new AppSettingsSnapshot();
      if (!File.Exists(configFilePath)) {
        return false;
      }

      try {
        string json = File.ReadAllText(configFilePath, Encoding.UTF8);
        snapshot = NormalizeSnapshot(DeserializeSnapshot(json));
        return true;
      } catch (Exception ex) {
        Console.WriteLine($"Error restoring configuration: {ex.Message}");
        snapshot = new AppSettingsSnapshot();
        return false;
      }
    }

    public void SaveConfig(AppSettingsSnapshot snapshot) {
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
      string directoryPath = Path.GetDirectoryName(configFilePath);
      if (!string.IsNullOrWhiteSpace(directoryPath)) {
        Directory.CreateDirectory(directoryPath);
      }

      string json = SerializeSnapshot(NormalizeSnapshot(snapshot));
      File.WriteAllText(configFilePath, json, Encoding.UTF8);
    }

    static AppSettingsSnapshot DeserializeSnapshot(string json) {
      if (string.IsNullOrWhiteSpace(json)) {
        return new AppSettingsSnapshot();
      }

      var serializerSettings = new JsonSerializerSettings {
        MissingMemberHandling = MissingMemberHandling.Ignore,
        NullValueHandling = NullValueHandling.Ignore,
        ObjectCreationHandling = ObjectCreationHandling.Replace
      };

      return JsonConvert.DeserializeObject<AppSettingsSnapshot>(json, serializerSettings) ?? new AppSettingsSnapshot();
    }

    static string SerializeSnapshot(AppSettingsSnapshot snapshot) {
      return JsonConvert.SerializeObject(snapshot ?? new AppSettingsSnapshot(), Formatting.None);
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
      target.OmenKey = source.OmenKey;
      target.MonitorFan = source.MonitorFan;
      target.SmartPowerControlEnabled = source.SmartPowerControlEnabled;
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
      snapshot.OmenKey = NormalizeOmenKey(snapshot.OmenKey);
      snapshot.PowerControlTuning = snapshot.PowerControlTuning == null
        ? PowerController.CreateDefaultTuning()
        : snapshot.PowerControlTuning.Clone();
      snapshot.FanCurveProfiles = CloneProfiles(snapshot.FanCurveProfiles);
      return snapshot;
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
