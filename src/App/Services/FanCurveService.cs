using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Windows.Forms;

namespace OmenSuperHub {
  [DataContract]
  public sealed class FanCurveConfigEntry {
    [DataMember]
    public float CpuTemperature { get; set; }
    [DataMember]
    public int CpuFan1Rpm { get; set; }
    [DataMember]
    public int CpuFan2Rpm { get; set; }
    [DataMember]
    public float GpuTemperature { get; set; }
    [DataMember]
    public int GpuFan1Rpm { get; set; }
    [DataMember]
    public int GpuFan2Rpm { get; set; }
  }

  [DataContract]
  public sealed class FanCurveConfigProfile {
    [DataMember]
    public string Name { get; set; }
    [DataMember]
    public List<FanCurveConfigEntry> Entries { get; set; } = new List<FanCurveConfigEntry>();
  }

  internal sealed class FanCurveService {
    const string SilentProfileName = "silent";
    const string CoolProfileName = "cool";

    readonly IOmenHardwareGateway hardwareGateway;
    readonly AppSettingsService settingsService;
    readonly object fanMapLock = new object();
    readonly Dictionary<float, List<int>> cpuTempFanMap = new Dictionary<float, List<int>>();
    readonly Dictionary<float, List<int>> gpuTempFanMap = new Dictionary<float, List<int>>();
    List<FanCurveConfigEntry> activeEntries = new List<FanCurveConfigEntry>();

    public FanCurveService(IOmenHardwareGateway hardwareGateway, AppSettingsService settingsService) {
      this.hardwareGateway = hardwareGateway;
      this.settingsService = settingsService;
    }

    public void LoadConfig(string profileName) {
      string normalizedProfileName = NormalizeProfileName(profileName);
      List<FanCurveConfigProfile> profiles = settingsService.LoadFanCurveProfiles();
      FanCurveConfigProfile profile = profiles.FirstOrDefault(item =>
        string.Equals(item?.Name, normalizedProfileName, StringComparison.OrdinalIgnoreCase));

      if (profile != null && ApplyEntries(profile.Entries)) {
        return;
      }

      float silentCoef = normalizedProfileName == SilentProfileName ? 0.8f : 1f;
      LoadDefaultFanConfig(silentCoef);
      SaveActiveProfile(normalizedProfileName, profiles);
    }

    public int GetFanSpeedForTemperature(float cpuTemp, float gpuTemp, bool monitorGpu, int fanIndex) {
      lock (fanMapLock) {
        if (cpuTempFanMap.Count == 0 || gpuTempFanMap.Count == 0) {
          return 0;
        }

        int cpuFanSpeed = GetFanSpeedForSpecificTemperature(cpuTemp, cpuTempFanMap, fanIndex);
        if (!monitorGpu) {
          return cpuFanSpeed;
        }

        int gpuFanSpeed = GetFanSpeedForSpecificTemperature(gpuTemp, gpuTempFanMap, fanIndex);
        return Math.Max(cpuFanSpeed, gpuFanSpeed);
      }
    }

    void LoadDefaultFanConfig(float silentCoef) {
      byte[] fanTableBytes = hardwareGateway.GetFanTable();
      if (fanTableBytes == null || fanTableBytes.Length < 3) {
        GenerateDefaultMapping();
        return;
      }

      int numberOfFans = fanTableBytes[0];
      if (numberOfFans != 2) {
        MessageBox.Show("本机型不受支持！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        GenerateDefaultMapping();
        return;
      }

      int numberOfEntries = fanTableBytes[1];
      int originalMin = int.MaxValue;
      int originalMax = int.MinValue;

      for (int i = 0; i < numberOfEntries; i++) {
        int baseIndex = 2 + i * 3;
        int tempThreshold = fanTableBytes[baseIndex + 2];
        if (tempThreshold < originalMin) {
          originalMin = tempThreshold;
        }
        if (tempThreshold > originalMax) {
          originalMax = tempThreshold;
        }
      }

      float targetMin = 50.0f;
      float targetMax = 97.0f;
      var entries = new List<FanCurveConfigEntry>();

      for (int i = 0; i < numberOfEntries; i++) {
        int baseIndex = 2 + i * 3;
        int fan1Speed = fanTableBytes[baseIndex];
        int fan2Speed = fanTableBytes[baseIndex + 1];
        int originalTempThreshold = fanTableBytes[baseIndex + 2];
        float cpuTempThreshold = originalMax == originalMin
          ? targetMin
          : targetMin + (originalTempThreshold - originalMin) * (targetMax - targetMin) / (originalMax - originalMin);

        entries.Add(new FanCurveConfigEntry {
          CpuTemperature = cpuTempThreshold,
          CpuFan1Rpm = (int)(fan1Speed * silentCoef) * 100,
          CpuFan2Rpm = (int)(fan2Speed * silentCoef) * 100,
          GpuTemperature = cpuTempThreshold - 10.0f,
          GpuFan1Rpm = (int)(fan1Speed * silentCoef) * 100,
          GpuFan2Rpm = (int)(fan2Speed * silentCoef) * 100
        });
      }

      ApplyEntries(entries);
    }

    void GenerateDefaultMapping() {
      ApplyEntries(new List<FanCurveConfigEntry> {
        new FanCurveConfigEntry { CpuTemperature = 30f, CpuFan1Rpm = 0, CpuFan2Rpm = 0, GpuTemperature = 20f, GpuFan1Rpm = 0, GpuFan2Rpm = 0 },
        new FanCurveConfigEntry { CpuTemperature = 50f, CpuFan1Rpm = 1600, CpuFan2Rpm = 1900, GpuTemperature = 40f, GpuFan1Rpm = 1600, GpuFan2Rpm = 1900 },
        new FanCurveConfigEntry { CpuTemperature = 60f, CpuFan1Rpm = 2000, CpuFan2Rpm = 2300, GpuTemperature = 50f, GpuFan1Rpm = 2000, GpuFan2Rpm = 2300 },
        new FanCurveConfigEntry { CpuTemperature = 85f, CpuFan1Rpm = 4000, CpuFan2Rpm = 4300, GpuTemperature = 75f, GpuFan1Rpm = 4000, GpuFan2Rpm = 4300 },
        new FanCurveConfigEntry { CpuTemperature = 100f, CpuFan1Rpm = 6100, CpuFan2Rpm = 6400, GpuTemperature = 90f, GpuFan1Rpm = 6100, GpuFan2Rpm = 6400 }
      });
    }

    bool ApplyEntries(IEnumerable<FanCurveConfigEntry> entries) {
      if (entries == null) {
        return false;
      }

      List<FanCurveConfigEntry> normalizedEntries = entries
        .Where(entry => entry != null)
        .OrderBy(entry => entry.CpuTemperature)
        .Select(CloneEntry)
        .ToList();

      if (normalizedEntries.Count == 0) {
        return false;
      }

      lock (fanMapLock) {
        cpuTempFanMap.Clear();
        gpuTempFanMap.Clear();
        activeEntries = new List<FanCurveConfigEntry>();

        foreach (FanCurveConfigEntry entry in normalizedEntries) {
          cpuTempFanMap[entry.CpuTemperature] = new List<int> { entry.CpuFan1Rpm, entry.CpuFan2Rpm };
          gpuTempFanMap[entry.GpuTemperature] = new List<int> { entry.GpuFan1Rpm, entry.GpuFan2Rpm };
          activeEntries.Add(CloneEntry(entry));
        }
      }

      return true;
    }

    void SaveActiveProfile(string profileName, List<FanCurveConfigProfile> profiles) {
      List<FanCurveConfigProfile> snapshot = profiles == null
        ? new List<FanCurveConfigProfile>()
        : profiles
            .Where(profile => profile != null)
            .Select(CloneProfile)
            .ToList();

      snapshot.RemoveAll(profile => string.Equals(profile.Name, profileName, StringComparison.OrdinalIgnoreCase));
      snapshot.Add(new FanCurveConfigProfile {
        Name = profileName,
        Entries = GetActiveEntriesSnapshot()
      });
      settingsService.SaveFanCurveProfiles(snapshot);
    }

    List<FanCurveConfigEntry> GetActiveEntriesSnapshot() {
      lock (fanMapLock) {
        return activeEntries.Select(CloneEntry).ToList();
      }
    }

    static FanCurveConfigEntry CloneEntry(FanCurveConfigEntry entry) {
      return new FanCurveConfigEntry {
        CpuTemperature = entry.CpuTemperature,
        CpuFan1Rpm = entry.CpuFan1Rpm,
        CpuFan2Rpm = entry.CpuFan2Rpm,
        GpuTemperature = entry.GpuTemperature,
        GpuFan1Rpm = entry.GpuFan1Rpm,
        GpuFan2Rpm = entry.GpuFan2Rpm
      };
    }

    static FanCurveConfigProfile CloneProfile(FanCurveConfigProfile profile) {
      return new FanCurveConfigProfile {
        Name = profile.Name,
        Entries = profile.Entries == null
          ? new List<FanCurveConfigEntry>()
          : profile.Entries.Select(CloneEntry).ToList()
      };
    }

    static string NormalizeProfileName(string profileName) {
      return string.Equals(profileName, CoolProfileName, StringComparison.OrdinalIgnoreCase)
        ? CoolProfileName
        : SilentProfileName;
    }

    static int GetFanSpeedForSpecificTemperature(float temperature, Dictionary<float, List<int>> tempFanMap, int fanIndex) {
      float lowerBound = tempFanMap.Keys
                      .OrderBy(k => k)
                      .Where(t => t <= temperature)
                      .DefaultIfEmpty(tempFanMap.Keys.Min())
                      .LastOrDefault();

      float upperBound = tempFanMap.Keys
                      .OrderBy(k => k)
                      .Where(t => t > temperature)
                      .DefaultIfEmpty(tempFanMap.Keys.Max())
                      .FirstOrDefault();

      if (lowerBound == upperBound) {
        return tempFanMap[lowerBound][fanIndex];
      }

      int lowerSpeed = tempFanMap[lowerBound][fanIndex];
      int upperSpeed = tempFanMap[upperBound][fanIndex];
      float interpolatedSpeed = lowerSpeed + (upperSpeed - lowerSpeed) * (temperature - lowerBound) / (upperBound - lowerBound);
      return (int)interpolatedSpeed;
    }
  }
}
