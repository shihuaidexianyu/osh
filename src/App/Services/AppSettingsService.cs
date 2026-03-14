using System;
using Microsoft.Win32;

namespace OmenSuperHub {
  internal sealed class AppSettingsSnapshot {
    public string UsageMode { get; set; } = "balanced";
    public string FanTable { get; set; } = "silent";
    public string FanMode { get; set; } = "performance";
    public string FanControl { get; set; } = "auto";
    public string TempSensitivity { get; set; } = "high";
    public string CpuPower { get; set; } = "max";
    public string GpuPower { get; set; } = "max";
    public string GraphicsModeSetting { get; set; } = "hybrid";
    public int GpuClock { get; set; }
    public string AutoStart { get; set; } = "off";
    public int AlreadyRead { get; set; }
    public string CustomIcon { get; set; } = "original";
    public string OmenKey { get; set; } = "default";
    public bool MonitorFan { get; set; } = true;
    public bool SmartPowerControlEnabled { get; set; } = true;
    public int FloatingBarSize { get; set; } = 48;
    public string FloatingBarLocation { get; set; } = "left";
    public string FloatingBar { get; set; } = "off";
  }

  internal sealed class AppSettingsService {
    const string RegistryPath = @"Software\OmenSuperHub";

    public bool TryLoadConfig(out AppSettingsSnapshot snapshot) {
      snapshot = new AppSettingsSnapshot();

      try {
        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath)) {
          if (key == null) {
            return false;
          }

          snapshot.UsageMode = ReadString(key, "UsageMode", snapshot.UsageMode);
          snapshot.FanTable = ReadString(key, "FanTable", snapshot.FanTable);
          snapshot.FanMode = ReadString(key, "FanMode", snapshot.FanMode);
          snapshot.FanControl = ReadString(key, "FanControl", snapshot.FanControl);
          snapshot.TempSensitivity = ReadString(key, "TempSensitivity", snapshot.TempSensitivity);
          snapshot.CpuPower = ReadString(key, "CpuPower", snapshot.CpuPower);
          snapshot.GpuPower = ReadString(key, "GpuPower", snapshot.GpuPower);
          snapshot.GraphicsModeSetting = ReadString(key, "GraphicsMode", snapshot.GraphicsModeSetting);
          snapshot.GpuClock = ReadInt(key, "GpuClock", snapshot.GpuClock);
          snapshot.AutoStart = ReadString(key, "AutoStart", snapshot.AutoStart);
          snapshot.AlreadyRead = ReadInt(key, "AlreadyRead", snapshot.AlreadyRead);
          snapshot.CustomIcon = ReadString(key, "CustomIcon", snapshot.CustomIcon);
          snapshot.OmenKey = ReadString(key, "OmenKey", snapshot.OmenKey);
          snapshot.MonitorFan = ReadBool(key, "MonitorFan", snapshot.MonitorFan);
          snapshot.SmartPowerControlEnabled = ReadBool(key, "SmartPowerControl", snapshot.SmartPowerControlEnabled);
          snapshot.FloatingBarSize = ReadInt(key, "FloatingBarSize", snapshot.FloatingBarSize);
          snapshot.FloatingBarLocation = ReadString(key, "FloatingBarLoc", snapshot.FloatingBarLocation);
          snapshot.FloatingBar = ReadString(key, "FloatingBar", snapshot.FloatingBar);
          return true;
        }
      } catch (Exception ex) {
        Console.WriteLine($"Error restoring configuration: {ex.Message}");
        snapshot = new AppSettingsSnapshot();
        return false;
      }
    }

    public void SaveConfig(AppSettingsSnapshot snapshot, string configName = null) {
      try {
        using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath)) {
          if (key == null || snapshot == null) {
            return;
          }

          if (configName == null) {
            SaveAllConfigValues(key, snapshot);
            return;
          }

          switch (configName) {
            case "UsageMode":
              key.SetValue("UsageMode", snapshot.UsageMode);
              break;
            case "FanTable":
              key.SetValue("FanTable", snapshot.FanTable);
              break;
            case "FanMode":
              key.SetValue("FanMode", snapshot.FanMode);
              break;
            case "FanControl":
              key.SetValue("FanControl", snapshot.FanControl);
              break;
            case "TempSensitivity":
              key.SetValue("TempSensitivity", snapshot.TempSensitivity);
              break;
            case "CpuPower":
              key.SetValue("CpuPower", snapshot.CpuPower);
              break;
            case "GpuPower":
              key.SetValue("GpuPower", snapshot.GpuPower);
              break;
            case "GraphicsMode":
              key.SetValue("GraphicsMode", snapshot.GraphicsModeSetting);
              break;
            case "GpuClock":
              key.SetValue("GpuClock", snapshot.GpuClock);
              break;
            case "AutoStart":
              key.SetValue("AutoStart", snapshot.AutoStart);
              break;
            case "AlreadyRead":
              key.SetValue("AlreadyRead", snapshot.AlreadyRead);
              break;
            case "CustomIcon":
              key.SetValue("CustomIcon", snapshot.CustomIcon);
              break;
            case "OmenKey":
              key.SetValue("OmenKey", snapshot.OmenKey);
              break;
            case "MonitorFan":
              key.SetValue("MonitorFan", snapshot.MonitorFan);
              break;
            case "SmartPowerControl":
              key.SetValue("SmartPowerControl", snapshot.SmartPowerControlEnabled);
              break;
            case "FloatingBarSize":
              key.SetValue("FloatingBarSize", snapshot.FloatingBarSize);
              break;
            case "FloatingBarLoc":
              key.SetValue("FloatingBarLoc", snapshot.FloatingBarLocation);
              break;
            case "FloatingBar":
              key.SetValue("FloatingBar", snapshot.FloatingBar);
              break;
          }
        }
      } catch (Exception ex) {
        Console.WriteLine($"Error saving configuration: {ex.Message}");
      }
    }

    public void SavePowerControlTuning(PowerControlTuning tuning) {
      if (tuning == null) {
        return;
      }

      try {
        using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath)) {
          if (key == null) {
            return;
          }

          key.SetValue("SPT_CpuEmergencyTempC", tuning.CpuEmergencyTempC, RegistryValueKind.String);
          key.SetValue("SPT_GpuEmergencyTempC", tuning.GpuEmergencyTempC, RegistryValueKind.String);
          key.SetValue("SPT_CpuRecoverTempC", tuning.CpuRecoverTempC, RegistryValueKind.String);
          key.SetValue("SPT_GpuRecoverTempC", tuning.GpuRecoverTempC, RegistryValueKind.String);
          key.SetValue("SPT_CpuFanBoostOnTempC", tuning.CpuFanBoostOnTempC, RegistryValueKind.String);
          key.SetValue("SPT_GpuFanBoostOnTempC", tuning.GpuFanBoostOnTempC, RegistryValueKind.String);
          key.SetValue("SPT_CpuFanBoostOffTempC", tuning.CpuFanBoostOffTempC, RegistryValueKind.String);
          key.SetValue("SPT_GpuFanBoostOffTempC", tuning.GpuFanBoostOffTempC, RegistryValueKind.String);
          key.SetValue("SPT_BatteryGuardTriggerWatts", tuning.BatteryGuardTriggerWatts, RegistryValueKind.String);
          key.SetValue("SPT_BatteryGuardReleaseWatts", tuning.BatteryGuardReleaseWatts, RegistryValueKind.String);
        }
      } catch (Exception ex) {
        Console.WriteLine($"Error saving power tuning: {ex.Message}");
      }
    }

    public PowerControlTuning LoadPowerControlTuning() {
      var defaults = PowerController.CreateDefaultTuning();
      var tuning = defaults.Clone();

      try {
        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath)) {
          if (key == null) {
            return tuning;
          }

          tuning.CpuEmergencyTempC = ReadFloat(key, "SPT_CpuEmergencyTempC", defaults.CpuEmergencyTempC);
          tuning.GpuEmergencyTempC = ReadFloat(key, "SPT_GpuEmergencyTempC", defaults.GpuEmergencyTempC);
          tuning.CpuRecoverTempC = ReadFloat(key, "SPT_CpuRecoverTempC", defaults.CpuRecoverTempC);
          tuning.GpuRecoverTempC = ReadFloat(key, "SPT_GpuRecoverTempC", defaults.GpuRecoverTempC);
          tuning.CpuFanBoostOnTempC = ReadFloat(key, "SPT_CpuFanBoostOnTempC", defaults.CpuFanBoostOnTempC);
          tuning.GpuFanBoostOnTempC = ReadFloat(key, "SPT_GpuFanBoostOnTempC", defaults.GpuFanBoostOnTempC);
          tuning.CpuFanBoostOffTempC = ReadFloat(key, "SPT_CpuFanBoostOffTempC", defaults.CpuFanBoostOffTempC);
          tuning.GpuFanBoostOffTempC = ReadFloat(key, "SPT_GpuFanBoostOffTempC", defaults.GpuFanBoostOffTempC);
          tuning.BatteryGuardTriggerWatts = ReadFloat(key, "SPT_BatteryGuardTriggerWatts", defaults.BatteryGuardTriggerWatts);
          tuning.BatteryGuardReleaseWatts = ReadFloat(key, "SPT_BatteryGuardReleaseWatts", defaults.BatteryGuardReleaseWatts);
        }
      } catch (Exception ex) {
        Console.WriteLine($"Error restoring power tuning: {ex.Message}");
      }

      return tuning;
    }

    static void SaveAllConfigValues(RegistryKey key, AppSettingsSnapshot snapshot) {
      key.SetValue("UsageMode", snapshot.UsageMode);
      key.SetValue("FanTable", snapshot.FanTable);
      key.SetValue("FanMode", snapshot.FanMode);
      key.SetValue("FanControl", snapshot.FanControl);
      key.SetValue("TempSensitivity", snapshot.TempSensitivity);
      key.SetValue("CpuPower", snapshot.CpuPower);
      key.SetValue("GpuPower", snapshot.GpuPower);
      key.SetValue("GraphicsMode", snapshot.GraphicsModeSetting);
      key.SetValue("GpuClock", snapshot.GpuClock);
      key.SetValue("AutoStart", snapshot.AutoStart);
      key.SetValue("AlreadyRead", snapshot.AlreadyRead);
      key.SetValue("CustomIcon", snapshot.CustomIcon);
      key.SetValue("OmenKey", snapshot.OmenKey);
      key.SetValue("MonitorFan", snapshot.MonitorFan);
      key.SetValue("SmartPowerControl", snapshot.SmartPowerControlEnabled);
      key.SetValue("FloatingBarSize", snapshot.FloatingBarSize);
      key.SetValue("FloatingBarLoc", snapshot.FloatingBarLocation);
      key.SetValue("FloatingBar", snapshot.FloatingBar);
    }

    static string ReadString(RegistryKey key, string valueName, string fallback) {
      object raw = key.GetValue(valueName, fallback);
      return raw == null ? fallback : raw.ToString();
    }

    static int ReadInt(RegistryKey key, string valueName, int fallback) {
      object raw = key.GetValue(valueName, fallback);
      try {
        return Convert.ToInt32(raw);
      } catch {
        return fallback;
      }
    }

    static bool ReadBool(RegistryKey key, string valueName, bool fallback) {
      object raw = key.GetValue(valueName, fallback);
      try {
        return Convert.ToBoolean(raw);
      } catch {
        return fallback;
      }
    }

    static float ReadFloat(RegistryKey key, string valueName, float fallback) {
      object raw = key.GetValue(valueName, null);
      if (raw == null) {
        return fallback;
      }

      try {
        return Convert.ToSingle(raw);
      } catch {
      }

      float parsed;
      return float.TryParse(raw.ToString(), out parsed) ? parsed : fallback;
    }
  }
}
