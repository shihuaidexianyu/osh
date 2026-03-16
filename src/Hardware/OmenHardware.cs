namespace OmenSuperHub {
  internal static class OmenHardware {
    public enum OmenGfxMode : byte {
      Hybrid = 0x00,
      Discrete = 0x01,
      Optimus = 0x02,
      Unknown = 0xFF
    }

    public enum OmenKeyboardType : byte {
      Standard = 0x00,
      WithNumpad = 0x01,
      Tenkeyless = 0x02,
      PerKeyRgb = 0x03,
      Unknown = 0xFF
    }

    public enum OmenSmartAdapterStatus : ushort {
      NoSupport = 0x0000,
      MeetsRequirement = 0x0001,
      BelowRequirement = 0x0002,
      BatteryPower = 0x0003,
      NotFunctioning = 0x0004,
      Unknown = 0xFFFF
    }

    public sealed class OmenGpuStatus {
      public bool CustomTgpEnabled { get; set; }
      public bool PpabEnabled { get; set; }
      public byte DState { get; set; }
      public byte ThermalThreshold { get; set; }
      public byte[] RawData { get; set; }
    }

    public sealed class OmenFanTypeInfo {
      public byte RawValue { get; set; }
      public int Fan1Type { get; set; }
      public int Fan2Type { get; set; }
    }

    public sealed class OmenSystemDesignData {
      public ushort PowerFlags { get; set; }
      public byte ThermalPolicyVersion { get; set; }
      public byte FeatureFlags { get; set; }
      public byte DefaultPl4 { get; set; }
      public byte BiosOverclockingSupport { get; set; }
      public byte MiscFlags { get; set; }
      public byte DefaultConcurrentTdp { get; set; }
      public bool SoftwareFanControlSupported { get; set; }
      public bool ExtremeModeSupported { get; set; }
      public bool ExtremeModeUnlocked { get; set; }
      public bool GraphicsSwitcherSupported { get; set; }
      public bool GraphicsHybridModeSupported { get; set; }
      public bool GraphicsOptimusModeSupported { get; set; }
      public byte[] RawData { get; set; }
    }
  }
}
