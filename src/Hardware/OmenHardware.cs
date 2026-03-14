using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;

namespace OmenSuperHub {
  internal class OmenHardware {
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
      public byte[] RawData { get; set; }
    }

    public static void GetFanCount() {
      SendOmenBiosWmi(0x10, new byte[] { 0x00, 0x00, 0x00, 0x00 }, 4);
    }

    public static List<int> GetFanLevel() {
      List<int> fanSpeedNow = new List<int> { 0, 0 };
      byte[] fanLevel = SendOmenBiosWmi(0x2D, new byte[] { 0x00, 0x00, 0x00, 0x00 }, 128);
      if (fanLevel != null) {
        fanSpeedNow[0] = fanLevel[0];
        fanSpeedNow[1] = fanLevel[1];
      }
      return fanSpeedNow;
    }

    public static byte[] GetFanTable() {
      return SendOmenBiosWmi(0x2F, new byte[] { 0x00, 0x00, 0x00, 0x00 }, 128);
    }

    public static OmenFanTypeInfo GetFanTypeInfo() {
      byte[] data = SendOmenBiosWmi(0x2C, new byte[] { 0x00, 0x00, 0x00, 0x00 }, 128);
      if (data == null || data.Length == 0)
        return null;

      return new OmenFanTypeInfo {
        RawValue = data[0],
        Fan1Type = data[0] & 0x0F,
        Fan2Type = (data[0] >> 4) & 0x0F
      };
    }

    public static OmenGfxMode GetGraphicsMode() {
      byte[] data = SendOmenBiosWmi(0x52, null, 4, 0x01);
      if (data == null || data.Length == 0)
        return OmenGfxMode.Unknown;

      switch (data[0]) {
        case 0x00:
          return OmenGfxMode.Hybrid;
        case 0x01:
          return OmenGfxMode.Discrete;
        case 0x02:
          return OmenGfxMode.Optimus;
        default:
          return OmenGfxMode.Unknown;
      }
    }

    public static void SetGraphicsMode(OmenGfxMode mode) {
      if (mode == OmenGfxMode.Unknown)
        return;

      SendOmenBiosWmi(0x52, new byte[] { (byte)mode }, 0);
    }

    public static OmenGpuStatus GetGpuStatus() {
      byte[] data = SendOmenBiosWmi(0x21, new byte[] { 0x00, 0x00, 0x00, 0x00 }, 4);
      if (data == null || data.Length < 4)
        return null;

      return new OmenGpuStatus {
        CustomTgpEnabled = data[0] != 0,
        PpabEnabled = data[1] != 0,
        DState = data[2],
        ThermalThreshold = data[3],
        RawData = data
      };
    }

    public static OmenSystemDesignData GetSystemDesignData() {
      byte[] data = SendOmenBiosWmi(0x28, null, 128);
      if (data == null || data.Length < 9)
        return null;

      return new OmenSystemDesignData {
        PowerFlags = (ushort)(data[0] | (data[1] << 8)),
        ThermalPolicyVersion = data[2],
        FeatureFlags = data[4],
        DefaultPl4 = data[5],
        BiosOverclockingSupport = data[6],
        MiscFlags = data[7],
        DefaultConcurrentTdp = data[8],
        SoftwareFanControlSupported = (data[4] & 0x01) != 0,
        ExtremeModeSupported = (data[4] & 0x02) != 0,
        ExtremeModeUnlocked = (data[4] & 0x04) != 0,
        GraphicsSwitcherSupported = (data[7] & 0x08) != 0,
        RawData = data
      };
    }

    public static void SetFanLevel(int fanSpeed1, int fanSpeed2) {
      int safeFanSpeed1 = Math.Max(0, Math.Min(64, fanSpeed1));
      int safeFanSpeed2 = Math.Max(0, Math.Min(64, fanSpeed2));
      SendOmenBiosWmi(0x2E, new byte[] { (byte)safeFanSpeed1, (byte)safeFanSpeed2 }, 0);
    }

    public static void SetFanMode(byte mode) {
      SendOmenBiosWmi(0x1A, new byte[] { 0xFF, mode }, 0);
    }

    public static void SetMaxGpuPower() {
      SendOmenBiosWmi(0x22, new byte[] { 0x01, 0x01, 0x01, 0x00 }, 0);
    }

    public static void SetMedGpuPower() {
      SendOmenBiosWmi(0x22, new byte[] { 0x01, 0x00, 0x01, 0x00 }, 0);
    }

    public static void SetMinGpuPower() {
      SendOmenBiosWmi(0x22, new byte[] { 0x00, 0x00, 0x01, 0x00 }, 0);
    }

    public static void SetConcurrentCpuPowerLimit(byte value) {
      SendOmenBiosWmi(0x29, new byte[] { 0xFF, 0xFF, 0xFF, value }, 0);
    }

    public static void SetCpuPowerLimit(byte value) {
      SendOmenBiosWmi(0x29, new byte[] { value, value, 0xFF, 0xFF }, 0);
    }

    public static void SetCpuPowerMaxLimit(byte value) {
      SendOmenBiosWmi(0x29, new byte[] { 0xFF, 0xFF, value, 0xFF }, 0);
    }

    public static void SetMaxFanSpeedOn() {
      SendOmenBiosWmi(0x27, new byte[] { 0x01 }, 0);
    }

    public static void SetMaxFanSpeedOff() {
      SendOmenBiosWmi(0x27, new byte[] { 0x00 }, 0);
    }

    public static OmenKeyboardType GetKeyboardType() {
      byte[] data = SendOmenBiosWmi(0x2B, new byte[] { 0x00 }, 4);
      if (data == null || data.Length == 0)
        return OmenKeyboardType.Unknown;

      switch (data[0]) {
        case 0x00:
          return OmenKeyboardType.Standard;
        case 0x01:
          return OmenKeyboardType.WithNumpad;
        case 0x02:
          return OmenKeyboardType.Tenkeyless;
        case 0x03:
          return OmenKeyboardType.PerKeyRgb;
        default:
          return OmenKeyboardType.Unknown;
      }
    }

    public static bool IsMaxFanSpeedEnabled() {
      byte[] data = SendOmenBiosWmi(0x26, new byte[] { 0x00, 0x00, 0x00, 0x00 }, 4);
      return data != null && data.Length > 0 && data[0] != 0;
    }

    public static OmenSmartAdapterStatus GetSmartAdapterStatus() {
      byte[] data = SendOmenBiosWmi(0x0F, null, 4, 0x01);
      if (data == null || data.Length < 2)
        return OmenSmartAdapterStatus.Unknown;

      ushort value = (ushort)(data[0] | (data[1] << 8));
      switch (value) {
        case 0x0000:
          return OmenSmartAdapterStatus.NoSupport;
        case 0x0001:
          return OmenSmartAdapterStatus.MeetsRequirement;
        case 0x0002:
          return OmenSmartAdapterStatus.BelowRequirement;
        case 0x0003:
          return OmenSmartAdapterStatus.BatteryPower;
        case 0x0004:
          return OmenSmartAdapterStatus.NotFunctioning;
        default:
          return OmenSmartAdapterStatus.Unknown;
      }
    }

    public static ManagementObjectSearcher searcher;
    public static ManagementObject biosMethods;
    static readonly object biosLock = new object();

    static void ResetBiosSession() {
      if (biosMethods != null) {
        biosMethods.Dispose();
        biosMethods = null;
      }

      if (searcher != null) {
        searcher.Dispose();
        searcher = null;
      }
    }

    public static byte[] SendOmenBiosWmi(uint commandType, byte[] data, int outputSize, uint command = 0x20008) {
      const string namespaceName = @"root\wmi";
      const string className = "hpqBIntM";
      string methodName = "hpqBIOSInt" + outputSize.ToString();
      byte[] sign = { 0x53, 0x45, 0x43, 0x55 };

      lock (biosLock) {
        try {
          using (var biosDataIn = new ManagementClass(namespaceName, "hpqBDataIn", null).CreateInstance()) {
            biosDataIn["Command"] = command;
            biosDataIn["CommandType"] = commandType;
            biosDataIn["Sign"] = sign;
            if (data != null) {
              biosDataIn["hpqBData"] = data;
              biosDataIn["Size"] = (uint)data.Length;
            } else {
              biosDataIn["Size"] = (uint)0;
            }

            if (searcher == null)
              searcher = new ManagementObjectSearcher(namespaceName, $"SELECT * FROM {className}");
            if (biosMethods == null)
              biosMethods = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
            if (biosMethods == null)
              return null;

            var inParams = biosMethods.GetMethodParameters(methodName);
            inParams["InData"] = biosDataIn;

            var result = biosMethods.InvokeMethod(methodName, inParams, null);
            var outData = result["OutData"] as ManagementBaseObject;
            uint returnCode = (uint)outData["rwReturnCode"];

            if (returnCode == 0) {
              if (outputSize != 0) {
                return (byte[])outData["Data"];
              }
            } else {
              Console.WriteLine("- Failed: Error " + returnCode);
              switch (returnCode) {
                case 0x03:
                  Console.WriteLine(" - Command Not Available");
                  break;
                case 0x05:
                  Console.WriteLine(" - Input or Output Size Too Small");
                  break;
              }
            }
          }
        } catch (Exception ex) {
          ResetBiosSession();
          Console.WriteLine("Error: " + ex.Message);
          return null;
        }
      }

      return null;
    }

    public static void OmenKeyOff() {
      const string namespaceName = @"root\subscription";
      var scope = new ManagementScope(namespaceName);

      try {
        scope.Connect();

        var query = new ObjectQuery("SELECT * FROM __EventFilter WHERE Name='OmenKeyFilter'");
        var searcher = new ManagementObjectSearcher(scope, query);
        foreach (ManagementObject mo in searcher.Get()) {
          mo.Delete();
        }

        query = new ObjectQuery("SELECT * FROM CommandLineEventConsumer WHERE Name='OmenKeyConsumer'");
        searcher = new ManagementObjectSearcher(scope, query);
        foreach (ManagementObject mo in searcher.Get()) {
          mo.Delete();
        }

        query = new ObjectQuery("SELECT * FROM __FilterToConsumerBinding WHERE Filter='__EventFilter.Name=\"OmenKeyFilter\"'");
        searcher = new ManagementObjectSearcher(scope, query);
        foreach (ManagementObject mo in searcher.Get()) {
          mo.Delete();
        }
      } catch (Exception ex) {
        Console.WriteLine("Error: " + ex.Message);
      }
    }

    public static void OmenKeyOn(string method) {
      const string namespaceName = @"root\subscription";
      var scope = new ManagementScope(namespaceName);

      try {
        scope.Connect();

        var consumerClass = new ManagementClass(scope, new ManagementPath("CommandLineEventConsumer"), null);
        var consumer = consumerClass.CreateInstance();
        if (method == "custom") {
          consumer["CommandLineTemplate"] = @"cmd /c echo OmenKeyTriggered > \\.\pipe\OmenSuperHubPipe";
        } else {
          consumer["CommandLineTemplate"] = @"C:\Windows\System32\schtasks.exe /run /tn ""Omen Key""";
        }
        consumer["Name"] = "OmenKeyConsumer";
        consumer.Put();

        var filterClass = new ManagementClass(scope, new ManagementPath("__EventFilter"), null);
        var filter = filterClass.CreateInstance();
        filter["EventNameSpace"] = @"root\wmi";
        filter["Name"] = "OmenKeyFilter";
        filter["Query"] = "SELECT * FROM hpqBEvnt WHERE eventData = 8613 AND eventId = 29";
        filter["QueryLanguage"] = "WQL";
        filter.Put();

        var bindingClass = new ManagementClass(scope, new ManagementPath("__FilterToConsumerBinding"), null);
        var binding = bindingClass.CreateInstance();
        binding["Consumer"] = new ManagementPath(@"root\subscription:CommandLineEventConsumer.Name='OmenKeyConsumer'");
        binding["Filter"] = new ManagementPath(@"root\subscription:__EventFilter.Name='OmenKeyFilter'");
        binding.Put();
      } catch (Exception ex) {
        Console.WriteLine("Error: " + ex.Message);
      }
    }
  }
}
