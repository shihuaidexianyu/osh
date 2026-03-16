using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace OmenSuperHub {
  internal sealed class FanCurveService {
    readonly IOmenHardwareGateway hardwareGateway;
    readonly object fanMapLock = new object();
    readonly Dictionary<float, List<int>> cpuTempFanMap = new Dictionary<float, List<int>>();
    readonly Dictionary<float, List<int>> gpuTempFanMap = new Dictionary<float, List<int>>();

    public FanCurveService(IOmenHardwareGateway hardwareGateway) {
      this.hardwareGateway = hardwareGateway;
    }

    public void LoadConfig(string filePath) {
      float silentCoef = filePath == "silent.txt" ? 0.8f : 1f;
      string absoluteFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filePath);
      if (File.Exists(absoluteFilePath)) {
        lock (fanMapLock) {
          cpuTempFanMap.Clear();
          gpuTempFanMap.Clear();
        }

        var lines = File.ReadAllLines(absoluteFilePath);
        for (int i = 1; i < lines.Length; i++) {
          var parts = lines[i].Split(',');
          if (parts.Length == 6) {
            float cpuTemp;
            int cpuFan1Speed;
            int cpuFan2Speed;
            float gpuTemp;
            int gpuFan1Speed;
            int gpuFan2Speed;
            if (float.TryParse(parts[0], out cpuTemp) &&
                int.TryParse(parts[1], out cpuFan1Speed) &&
                int.TryParse(parts[2], out cpuFan2Speed) &&
                float.TryParse(parts[3], out gpuTemp) &&
                int.TryParse(parts[4], out gpuFan1Speed) &&
                int.TryParse(parts[5], out gpuFan2Speed)) {
              lock (fanMapLock) {
                cpuTempFanMap[cpuTemp] = new List<int> { cpuFan1Speed, cpuFan2Speed };
                gpuTempFanMap[gpuTemp] = new List<int> { gpuFan1Speed, gpuFan2Speed };
              }
            }
          } else {
            Console.WriteLine($"{absoluteFilePath} error.");
            LoadDefaultFanConfig(absoluteFilePath, silentCoef);
            return;
          }
        }
      } else {
        Console.WriteLine($"{absoluteFilePath} not found.");
        LoadDefaultFanConfig(absoluteFilePath, silentCoef);
      }
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

    void LoadDefaultFanConfig(string filePath, float silentCoef) {
      byte[] fanTableBytes = hardwareGateway.GetFanTable();
      if (fanTableBytes == null || fanTableBytes.Length < 3) {
        GenerateDefaultMapping(filePath);
        return;
      }

      int numberOfFans = fanTableBytes[0];
      if (numberOfFans != 2) {
        MessageBox.Show("本机型不受支持！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        GenerateDefaultMapping(filePath);
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

      lock (fanMapLock) {
        cpuTempFanMap.Clear();
        gpuTempFanMap.Clear();

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
          float gpuTempThreshold = cpuTempThreshold - 10.0f;

          if (!cpuTempFanMap.ContainsKey(cpuTempThreshold)) {
            cpuTempFanMap[cpuTempThreshold] = new List<int>();
          }
          cpuTempFanMap[cpuTempThreshold].Add((int)(fan1Speed * silentCoef) * 100);
          cpuTempFanMap[cpuTempThreshold].Add((int)(fan2Speed * silentCoef) * 100);

          if (!gpuTempFanMap.ContainsKey(gpuTempThreshold)) {
            gpuTempFanMap[gpuTempThreshold] = new List<int>();
          }
          gpuTempFanMap[gpuTempThreshold].Add((int)(fan1Speed * silentCoef) * 100);
          gpuTempFanMap[gpuTempThreshold].Add((int)(fan2Speed * silentCoef) * 100);
        }
      }

      List<string> lines;
      lock (fanMapLock) {
        lines = new List<string> { "CPU,Fan1,Fan2,GPU,Fan1,Fan2" };
        lines.AddRange(cpuTempFanMap.Select(kvp =>
            $"{kvp.Key:F0},{kvp.Value[0]},{kvp.Value[1]},{kvp.Key - 10.0:F0},{kvp.Value[0]},{kvp.Value[1]}"));
      }
      File.WriteAllLines(filePath, lines);
    }

    void GenerateDefaultMapping(string filePath) {
      List<string> lines;
      lock (fanMapLock) {
        cpuTempFanMap.Clear();
        cpuTempFanMap[30] = new List<int> { 0, 0 };
        cpuTempFanMap[50] = new List<int> { 1600, 1900 };
        cpuTempFanMap[60] = new List<int> { 2000, 2300 };
        cpuTempFanMap[85] = new List<int> { 4000, 4300 };
        cpuTempFanMap[100] = new List<int> { 6100, 6400 };

        gpuTempFanMap.Clear();
        foreach (var kvp in cpuTempFanMap) {
          gpuTempFanMap[kvp.Key - 10] = new List<int> { kvp.Value[0], kvp.Value[1] };
        }

        lines = new List<string> { "CPU,Fan1,Fan2,GPU,Fan1,Fan2" };
        lines.AddRange(cpuTempFanMap.Select(kvp =>
            $"{kvp.Key:F0},{kvp.Value[0]},{kvp.Value[1]},{kvp.Key - 10:F0},{kvp.Value[0]},{kvp.Value[1]}"));
      }
      File.WriteAllLines(filePath, lines);
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
