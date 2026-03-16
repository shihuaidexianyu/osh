using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace OmenSuperHub {
  [Serializable]
  public sealed class FanCurveConfigEntry {
    public float CpuTemperature { get; set; }
    public int CpuFan1Rpm { get; set; }
    public int CpuFan2Rpm { get; set; }
    public float GpuTemperature { get; set; }
    public int GpuFan1Rpm { get; set; }
    public int GpuFan2Rpm { get; set; }
  }

  [Serializable]
  public sealed class FanCurveConfigProfile {
    public string Name { get; set; }
    public List<FanCurveConfigEntry> Entries { get; set; } = new List<FanCurveConfigEntry>();
  }

  [Serializable]
  public sealed class FanCurveConfigDocument {
    public List<FanCurveConfigProfile> Profiles { get; set; } = new List<FanCurveConfigProfile>();
  }

  internal sealed class FanCurveService {
    const string SilentProfileName = "silent";
    const string CoolProfileName = "cool";

    readonly IOmenHardwareGateway hardwareGateway;
    readonly object fanMapLock = new object();
    readonly Dictionary<float, List<int>> cpuTempFanMap = new Dictionary<float, List<int>>();
    readonly Dictionary<float, List<int>> gpuTempFanMap = new Dictionary<float, List<int>>();
    readonly string configFilePath;
    List<FanCurveConfigEntry> activeEntries = new List<FanCurveConfigEntry>();

    public FanCurveService(IOmenHardwareGateway hardwareGateway, string configFilePath = null) {
      this.hardwareGateway = hardwareGateway;
      this.configFilePath = string.IsNullOrWhiteSpace(configFilePath)
        ? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OmenSuperHub",
            "fan-curves.xml")
        : configFilePath;
    }

    public void LoadConfig(string profileName) {
      string normalizedProfileName = NormalizeProfileName(profileName);
      float silentCoef = normalizedProfileName == SilentProfileName ? 0.8f : 1f;

      if (TryLoadConfigProfile(normalizedProfileName)) {
        return;
      }

      if (TryLoadLegacyTextProfile(normalizedProfileName)) {
        SaveActiveProfile(normalizedProfileName);
        return;
      }

      LoadDefaultFanConfig(silentCoef);
      SaveActiveProfile(normalizedProfileName);
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

    bool TryLoadConfigProfile(string profileName) {
      if (!TryReadDocument(out FanCurveConfigDocument document) || document?.Profiles == null) {
        return false;
      }

      FanCurveConfigProfile profile = document.Profiles.FirstOrDefault(item =>
        string.Equals(item?.Name, profileName, StringComparison.OrdinalIgnoreCase));

      return profile != null && ApplyEntries(profile.Entries);
    }

    bool TryLoadLegacyTextProfile(string profileName) {
      string legacyFileName = profileName == CoolProfileName ? "cool.txt" : "silent.txt";
      string legacyFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, legacyFileName);
      if (!File.Exists(legacyFilePath)) {
        return false;
      }

      try {
        List<FanCurveConfigEntry> entries = ParseLegacyLines(File.ReadAllLines(legacyFilePath));
        return ApplyEntries(entries);
      } catch {
        return false;
      }
    }

    static List<FanCurveConfigEntry> ParseLegacyLines(IEnumerable<string> lines) {
      var entries = new List<FanCurveConfigEntry>();
      if (lines == null) {
        return entries;
      }

      bool isFirstLine = true;
      foreach (string line in lines) {
        if (isFirstLine) {
          isFirstLine = false;
          continue;
        }

        if (string.IsNullOrWhiteSpace(line)) {
          continue;
        }

        string[] parts = line.Split(',');
        if (parts.Length != 6) {
          throw new InvalidDataException("Legacy fan curve format is invalid.");
        }

        if (float.TryParse(parts[0], out float cpuTemp) &&
            int.TryParse(parts[1], out int cpuFan1Speed) &&
            int.TryParse(parts[2], out int cpuFan2Speed) &&
            float.TryParse(parts[3], out float gpuTemp) &&
            int.TryParse(parts[4], out int gpuFan1Speed) &&
            int.TryParse(parts[5], out int gpuFan2Speed)) {
          entries.Add(new FanCurveConfigEntry {
            CpuTemperature = cpuTemp,
            CpuFan1Rpm = cpuFan1Speed,
            CpuFan2Rpm = cpuFan2Speed,
            GpuTemperature = gpuTemp,
            GpuFan1Rpm = gpuFan1Speed,
            GpuFan2Rpm = gpuFan2Speed
          });
        }
      }

      return entries;
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
        float cpuTempThreshold;
        if (originalMax == originalMin) {
          cpuTempThreshold = targetMin;
        } else {
          cpuTempThreshold = targetMin +
              (originalTempThreshold - originalMin) * (targetMax - targetMin) / (originalMax - originalMin);
        }

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

      var normalizedEntries = entries
        .Where(entry => entry != null)
        .OrderBy(entry => entry.CpuTemperature)
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

    void SaveActiveProfile(string profileName) {
      FanCurveConfigDocument document = ReadDocumentOrDefault();
      if (document.Profiles == null) {
        document.Profiles = new List<FanCurveConfigProfile>();
      }

      document.Profiles.RemoveAll(profile =>
        string.Equals(profile?.Name, profileName, StringComparison.OrdinalIgnoreCase));

      document.Profiles.Add(new FanCurveConfigProfile {
        Name = NormalizeProfileName(profileName),
        Entries = GetActiveEntriesSnapshot()
      });

      Directory.CreateDirectory(Path.GetDirectoryName(configFilePath));
      using (var stream = File.Create(configFilePath)) {
        var serializer = new XmlSerializer(typeof(FanCurveConfigDocument));
        serializer.Serialize(stream, document);
      }
    }

    List<FanCurveConfigEntry> GetActiveEntriesSnapshot() {
      lock (fanMapLock) {
        return activeEntries.Select(CloneEntry).ToList();
      }
    }

    FanCurveConfigDocument ReadDocumentOrDefault() {
      return TryReadDocument(out FanCurveConfigDocument document) ? document : new FanCurveConfigDocument();
    }

    bool TryReadDocument(out FanCurveConfigDocument document) {
      document = null;
      if (!File.Exists(configFilePath)) {
        return false;
      }

      try {
        using (var stream = File.OpenRead(configFilePath)) {
          var serializer = new XmlSerializer(typeof(FanCurveConfigDocument));
          document = serializer.Deserialize(stream) as FanCurveConfigDocument;
          return document != null;
        }
      } catch {
        document = null;
        return false;
      }
    }

    static FanCurveConfigEntry CloneEntry(FanCurveConfigEntry entry) {
      if (entry == null) {
        return null;
      }

      return new FanCurveConfigEntry {
        CpuTemperature = entry.CpuTemperature,
        CpuFan1Rpm = entry.CpuFan1Rpm,
        CpuFan2Rpm = entry.CpuFan2Rpm,
        GpuTemperature = entry.GpuTemperature,
        GpuFan1Rpm = entry.GpuFan1Rpm,
        GpuFan2Rpm = entry.GpuFan2Rpm
      };
    }

    static string NormalizeProfileName(string profileName) {
      return string.Equals(profileName, CoolProfileName, StringComparison.OrdinalIgnoreCase)
        ? CoolProfileName
        : SilentProfileName;
    }

    static int GetFanSpeedForSpecificTemperature(float temperature, Dictionary<float, List<int>> tempFanMap, int fanIndex) {
      var lowerBound = tempFanMap.Keys
                      .OrderBy(k => k)
                      .Where(t => t <= temperature)
                      .DefaultIfEmpty(tempFanMap.Keys.Min())
                      .LastOrDefault();

      var upperBound = tempFanMap.Keys
                      .OrderBy(k => k)
                      .Where(t => t > temperature)
                      .DefaultIfEmpty(tempFanMap.Keys.Max())
                      .FirstOrDefault();

      if (lowerBound == upperBound) {
        return tempFanMap[lowerBound][fanIndex];
      }

      int lowerSpeed = tempFanMap[lowerBound][fanIndex];
      int upperSpeed = tempFanMap[upperBound][fanIndex];
      float lowerTemp = lowerBound;
      float upperTemp = upperBound;

      float interpolatedSpeed = lowerSpeed + (upperSpeed - lowerSpeed) * (temperature - lowerTemp) / (upperTemp - lowerTemp);
      return (int)interpolatedSpeed;
    }
  }
}
