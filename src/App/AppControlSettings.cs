using System;

namespace OmenSuperHub {
  internal enum UsageModePreset {
    Quiet,
    Balanced,
    Performance,
    Max,
    Custom
  }

  internal enum FanModeOption {
    Default,
    Performance
  }

  internal enum FanControlOption {
    Auto,
    Max,
    Manual
  }

  internal enum FanTableOption {
    Silent,
    Cool
  }

  internal enum TempSensitivityOption {
    Low,
    Medium,
    High,
    Realtime
  }

  internal enum GpuPowerOption {
    Min,
    Med,
    Max
  }

  internal sealed class RuntimeControlSettings {
    public FanModeOption FanMode { get; set; } = FanModeOption.Default;
    public FanControlOption FanControl { get; set; } = FanControlOption.Auto;
    public int ManualFanRpm { get; set; }
    public FanTableOption FanTable { get; set; } = FanTableOption.Silent;
    public TempSensitivityOption TempSensitivity { get; set; } = TempSensitivityOption.Medium;
    public bool CpuPowerMax { get; set; }
    public int CpuPowerWatts { get; set; } = 65;
    public GpuPowerOption GpuPower { get; set; } = GpuPowerOption.Med;
    public int GpuClockLimitMhz { get; set; }
    public bool SmartPowerControlEnabled { get; set; } = true;

    public bool Matches(RuntimeControlSettings other) {
      if (other == null) {
        return false;
      }

      return FanMode == other.FanMode &&
             FanControl == other.FanControl &&
             ManualFanRpm == other.ManualFanRpm &&
             FanTable == other.FanTable &&
             TempSensitivity == other.TempSensitivity &&
             CpuPowerMax == other.CpuPowerMax &&
             CpuPowerWatts == other.CpuPowerWatts &&
             GpuPower == other.GpuPower &&
             GpuClockLimitMhz == other.GpuClockLimitMhz &&
             SmartPowerControlEnabled == other.SmartPowerControlEnabled;
    }

    public void ApplyToSnapshot(AppSettingsSnapshot snapshot) {
      if (snapshot == null) {
        return;
      }

      snapshot.FanMode = ToStorageValue(FanMode);
      snapshot.FanControl = ToStorageValue(FanControl, ManualFanRpm);
      snapshot.FanTable = ToStorageValue(FanTable);
      snapshot.TempSensitivity = ToStorageValue(TempSensitivity);
      snapshot.CpuPower = ToCpuPowerStorageValue(CpuPowerMax, CpuPowerWatts);
      snapshot.GpuPower = ToStorageValue(GpuPower);
      snapshot.GpuClock = Math.Max(0, GpuClockLimitMhz);
      snapshot.SmartPowerControlEnabled = SmartPowerControlEnabled;
    }

    public static RuntimeControlSettings CreatePreset(UsageModePreset preset) {
      switch (preset) {
        case UsageModePreset.Quiet:
          return new RuntimeControlSettings {
            FanMode = FanModeOption.Default,
            FanControl = FanControlOption.Auto,
            FanTable = FanTableOption.Silent,
            TempSensitivity = TempSensitivityOption.Low,
            CpuPowerMax = false,
            CpuPowerWatts = 45,
            GpuPower = GpuPowerOption.Min,
            GpuClockLimitMhz = 0,
            SmartPowerControlEnabled = true
          };
        case UsageModePreset.Performance:
          return new RuntimeControlSettings {
            FanMode = FanModeOption.Performance,
            FanControl = FanControlOption.Auto,
            FanTable = FanTableOption.Cool,
            TempSensitivity = TempSensitivityOption.High,
            CpuPowerMax = true,
            CpuPowerWatts = 254,
            GpuPower = GpuPowerOption.Max,
            GpuClockLimitMhz = 0,
            SmartPowerControlEnabled = true
          };
        case UsageModePreset.Max:
          return new RuntimeControlSettings {
            FanMode = FanModeOption.Performance,
            FanControl = FanControlOption.Max,
            FanTable = FanTableOption.Cool,
            TempSensitivity = TempSensitivityOption.Realtime,
            CpuPowerMax = true,
            CpuPowerWatts = 254,
            GpuPower = GpuPowerOption.Max,
            GpuClockLimitMhz = 0,
            SmartPowerControlEnabled = false
          };
        default:
          return new RuntimeControlSettings {
            FanMode = FanModeOption.Default,
            FanControl = FanControlOption.Auto,
            FanTable = FanTableOption.Silent,
            TempSensitivity = TempSensitivityOption.Medium,
            CpuPowerMax = false,
            CpuPowerWatts = 65,
            GpuPower = GpuPowerOption.Med,
            GpuClockLimitMhz = 0,
            SmartPowerControlEnabled = true
          };
      }
    }

    public static RuntimeControlSettings FromSnapshot(AppSettingsSnapshot snapshot) {
      if (snapshot == null) {
        return CreatePreset(UsageModePreset.Balanced);
      }

      return new RuntimeControlSettings {
        FanMode = ParseFanMode(snapshot.FanMode),
        FanControl = ParseFanControl(snapshot.FanControl, out var manualFanRpm),
        ManualFanRpm = manualFanRpm,
        FanTable = ParseFanTable(snapshot.FanTable),
        TempSensitivity = ParseTempSensitivity(snapshot.TempSensitivity),
        CpuPowerMax = IsCpuPowerMax(snapshot.CpuPower),
        CpuPowerWatts = ParseCpuPowerWatts(snapshot.CpuPower),
        GpuPower = ParseGpuPower(snapshot.GpuPower),
        GpuClockLimitMhz = Math.Max(0, snapshot.GpuClock),
        SmartPowerControlEnabled = snapshot.SmartPowerControlEnabled
      };
    }

    public static UsageModePreset ParseUsageMode(string value) {
      switch ((value ?? string.Empty).ToLowerInvariant()) {
        case "quiet":
          return UsageModePreset.Quiet;
        case "performance":
          return UsageModePreset.Performance;
        case "max":
          return UsageModePreset.Max;
        case "custom":
          return UsageModePreset.Custom;
        default:
          return UsageModePreset.Balanced;
      }
    }

    public static string ToStorageValue(UsageModePreset preset) {
      switch (preset) {
        case UsageModePreset.Quiet:
          return "quiet";
        case UsageModePreset.Performance:
          return "performance";
        case UsageModePreset.Max:
          return "max";
        case UsageModePreset.Custom:
          return "custom";
        default:
          return "balanced";
      }
    }

    public static FanModeOption ParseFanMode(string value) {
      return string.Equals(value, "performance", StringComparison.OrdinalIgnoreCase)
        ? FanModeOption.Performance
        : FanModeOption.Default;
    }

    public static string ToStorageValue(FanModeOption value) {
      return value == FanModeOption.Performance ? "performance" : "default";
    }

    public static FanControlOption ParseFanControl(string value, out int manualFanRpm) {
      manualFanRpm = 0;
      if (string.Equals(value, "auto", StringComparison.OrdinalIgnoreCase)) {
        return FanControlOption.Auto;
      }

      if (string.Equals(value, "max", StringComparison.OrdinalIgnoreCase)) {
        return FanControlOption.Max;
      }

      if (!string.IsNullOrWhiteSpace(value) && value.EndsWith(" RPM", StringComparison.OrdinalIgnoreCase)) {
        int.TryParse(value.Replace(" RPM", string.Empty).Trim(), out manualFanRpm);
        manualFanRpm = Math.Max(0, manualFanRpm);
        return FanControlOption.Manual;
      }

      return FanControlOption.Auto;
    }

    public static string ToStorageValue(FanControlOption value, int manualFanRpm) {
      switch (value) {
        case FanControlOption.Max:
          return "max";
        case FanControlOption.Manual:
          return $"{Math.Max(0, manualFanRpm)} RPM";
        default:
          return "auto";
      }
    }

    public static FanTableOption ParseFanTable(string value) {
      return string.Equals(value, "cool", StringComparison.OrdinalIgnoreCase)
        ? FanTableOption.Cool
        : FanTableOption.Silent;
    }

    public static string ToStorageValue(FanTableOption value) {
      return value == FanTableOption.Cool ? "cool" : "silent";
    }

    public static TempSensitivityOption ParseTempSensitivity(string value) {
      switch ((value ?? string.Empty).ToLowerInvariant()) {
        case "low":
          return TempSensitivityOption.Low;
        case "high":
          return TempSensitivityOption.High;
        case "realtime":
          return TempSensitivityOption.Realtime;
        default:
          return TempSensitivityOption.Medium;
      }
    }

    public static string ToStorageValue(TempSensitivityOption value) {
      switch (value) {
        case TempSensitivityOption.Low:
          return "low";
        case TempSensitivityOption.High:
          return "high";
        case TempSensitivityOption.Realtime:
          return "realtime";
        default:
          return "medium";
      }
    }

    public static bool IsCpuPowerMax(string value) {
      return string.Equals(value, "max", StringComparison.OrdinalIgnoreCase);
    }

    public static int ParseCpuPowerWatts(string value) {
      if (IsCpuPowerMax(value)) {
        return 254;
      }

      if (!string.IsNullOrWhiteSpace(value) && value.EndsWith(" W", StringComparison.OrdinalIgnoreCase)) {
        if (int.TryParse(value.Replace(" W", string.Empty).Trim(), out int watts)) {
          return Math.Max(25, Math.Min(254, watts));
        }
      }

      return 65;
    }

    public static string ToCpuPowerStorageValue(bool isMax, int watts) {
      return isMax ? "max" : $"{Math.Max(25, Math.Min(254, watts))} W";
    }

    public static GpuPowerOption ParseGpuPower(string value) {
      switch ((value ?? string.Empty).ToLowerInvariant()) {
        case "max":
          return GpuPowerOption.Max;
        case "min":
          return GpuPowerOption.Min;
        default:
          return GpuPowerOption.Med;
      }
    }

    public static string ToStorageValue(GpuPowerOption value) {
      switch (value) {
        case GpuPowerOption.Max:
          return "max";
        case GpuPowerOption.Min:
          return "min";
        default:
          return "med";
      }
    }

  }
}
