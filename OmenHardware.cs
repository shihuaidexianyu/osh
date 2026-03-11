using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OmenSuperHub {
  internal class OmenHardware {
    public enum OmenGfxMode : byte {
      Hybrid = 0x00,
      Discrete = 0x01,
      Optimus = 0x02,
      Unknown = 0xFF
    }

    public enum OmenBacklightState : byte {
      Unknown = 0x00,
      Off = 0x64,
      On = 0xE4
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

    public sealed class OmenColorTable {
      public byte ZoneCount { get; set; }
      public byte[] RawData { get; set; }
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

    public static int QueryFanCount() {
      byte[] fanCount = SendOmenBiosWmi(0x10, new byte[] { 0x00, 0x00, 0x00, 0x00 }, 4);
      return fanCount != null && fanCount.Length > 0 ? fanCount[0] : 0;
    }

    public static List<int> GetFanLevel() {
      // Send command to retrieve fan speed
      List<int> fanSpeedNow = new List<int> { 0, 0 };
      byte[] fanLevel = SendOmenBiosWmi(0x2D, new byte[] { 0x00, 0x00, 0x00, 0x00 }, 128);
      if (fanLevel != null) {
        fanSpeedNow[0] = fanLevel[0];
        fanSpeedNow[1] = fanLevel[1];
        //Console.WriteLine("GetFanLevel: " + level * 100);
      }
      return fanSpeedNow;
    }

    public static byte[] GetFanTable() {
      // 0x19-0x34?
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

    public static byte GetTemperatureSensorValue() {
      byte[] data = SendOmenBiosWmi(0x23, new byte[] { 0x01, 0x00, 0x00, 0x00 }, 4);
      return data != null && data.Length > 0 ? data[0] : (byte)0;
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
      SendOmenBiosWmi(0x2E, new byte[] { (byte)fanSpeed1, (byte)fanSpeed2 }, 0);
      //Console.WriteLine("SetFanLevel: " + fanSpeed * 100);
    }

    //mode为0x31代表狂暴模式，0x30代表平衡模式
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

    // 无作用
    public static void SetConcurrentCpuPowerLimit(byte value) {
      SendOmenBiosWmi(0x29, new byte[] { 0xFF, 0xFF, 0xFF, value }, 0);
    }

    // PL1+PL2，立即生效，狂暴平衡都生效，直接对应功率W，1-254，需关闭ts，再点击狂暴模式失效
    public static void SetCpuPowerLimit(byte value) {
      SendOmenBiosWmi(0x29, new byte[] { value, value, 0xFF, 0xFF }, 0);
      //Console.WriteLine("SetCpuPowerLimit: " + value);
    }

    // PL4，狂暴平衡都生效，50-19，100-54，180-106，200-122，需关闭ts，1-254，和SetCpuPowerLimit优先级相同
    public static void SetCpuPowerMaxLimit(byte value) {
      SendOmenBiosWmi(0x29, new byte[] { 0xFF, 0xFF, value, 0xFF }, 0);
    }

    public static void SetMaxFanSpeedOn() {
      SendOmenBiosWmi(0x27, new byte[] { 0x01 }, 0);
    }

    public static void SetMaxFanSpeedOff() {
      SendOmenBiosWmi(0x27, new byte[] { 0x00 }, 0);
    }

    public static void BacklightOn() {
      SendOmenBiosWmi(0x05, new byte[] { 0xE4 }, 0, 0x20009);
    }

    public static void BacklightOff() {
      SendOmenBiosWmi(0x05, new byte[] { 0x64 }, 0, 0x20009);
    }

    public static bool HasBacklightSupport() {
      byte[] data = SendOmenBiosWmi(0x01, new byte[] { 0x00 }, 4, 0x20009);
      return data != null && data.Length > 0 && (data[0] & 0x01) != 0;
    }

    public static OmenBacklightState GetBacklightState() {
      byte[] data = SendOmenBiosWmi(0x04, new byte[] { 0x00 }, 128, 0x20009);
      if (data == null || data.Length == 0)
        return OmenBacklightState.Unknown;

      if (data[0] == (byte)OmenBacklightState.Off)
        return OmenBacklightState.Off;
      if (data[0] == (byte)OmenBacklightState.On)
        return OmenBacklightState.On;
      return OmenBacklightState.Unknown;
    }

    public static OmenColorTable GetColorTable() {
      byte[] data = SendOmenBiosWmi(0x02, new byte[] { 0x00 }, 128, 0x20009);
      if (data == null || data.Length == 0)
        return null;

      return new OmenColorTable {
        ZoneCount = data[0],
        RawData = data
      };
    }

    public static void SetColorTable(byte[] colorTable) {
      if (colorTable == null || colorTable.Length == 0)
        throw new ArgumentException("Color table cannot be empty.", nameof(colorTable));

      SendOmenBiosWmi(0x03, colorTable, 0, 0x20009);
    }

    public static void SetlightColor() {
      byte[] dataIn = new byte[128];
      dataIn[0] = 0x03;
      for (int i = 25; i <= 36; i++)
        dataIn[i] = 0x80;
      SendOmenBiosWmi(0x03, dataIn, 0, 0x20009);
    }

    //// 似乎没有作用，且不支持AMD
    //public static void InitializeIntelOC() {
    //  string outputData = SendOmenBiosWmi(0x35, new byte[] { 0x00, 0x00, 0x00, 0x00 }, 128);
    //  //Console.WriteLine("+ OK: " + outputData);
    //}

    //// 可导致直接黑屏
    //public static void SetVoltageOffset(int volOff) {
    //  byte[] dataIn = new byte[128];
    //  dataIn[0] = 0x00;
    //  dataIn[1] = 0x03;
    //  dataIn[2] = (byte)(volOff < 0 ? 0 : 1);
    //  dataIn[3] = (byte)(Math.Abs(volOff) / 256);
    //  dataIn[4] = (byte)(Math.Abs(volOff) % 256);
    //  string outputData = SendOmenBiosWmi(0x37, dataIn, 4);
    //  Console.WriteLine("+ OK: " + outputData);
    //}

    public static string GetBornOnDate() {
      byte[] data = SendOmenBiosWmi(0x10, null, 128, 0x01);
      if (data == null || data.Length < 8)
        return null;

      string date = Encoding.ASCII.GetString(data, 0, 8);
      return date.TrimEnd('\0');
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

    public static bool HasMemoryOverclockingSupport() {
      byte[] data = SendOmenBiosWmi(0x18, new byte[] { 0x00 }, 128);
      return data != null && data.Length > 2 && data[2] != 0;
    }

    public static bool HasBiosOverclockingSupport() {
      byte[] data = SendOmenBiosWmi(0x35, new byte[] { 0x00, 0x00, 0x00, 0x00 }, 128);
      return data != null && data.Length > 2 && data[2] != 0;
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

    public static bool? GetIdleModeEnabled() {
      byte[] data = SendOmenBiosWmi(0x31, new byte[] { 0x00, 0x00, 0x00, 0x00 }, 4);
      if (data == null || data.Length == 0)
        return null;

      return data[0] != 0;
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
      string methodName = "hpqBIOSInt" + outputSize.ToString(); // Change here
      byte[] sign = { 0x53, 0x45, 0x43, 0x55 };

      lock (biosLock) {
        try {
          // Prepare the request
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

            // Obtain BIOS method class instance
            if (searcher == null)
              searcher = new ManagementObjectSearcher(namespaceName, $"SELECT * FROM {className}");
            if (biosMethods == null)
              biosMethods = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
            if (biosMethods == null)
              return null;

            // Make a call to write to the BIOS
            var inParams = biosMethods.GetMethodParameters(methodName); // Change here
            inParams["InData"] = biosDataIn;

            var result = biosMethods.InvokeMethod(methodName, inParams, null); // Change here
            var outData = result["OutData"] as ManagementBaseObject;
            uint returnCode = (uint)outData["rwReturnCode"];

            if (returnCode == 0) {
              // If operation completed successfully
              if (outputSize != 0) {
                var outputData = (byte[])outData["Data"];
                // Console.WriteLine("+ OK: " + BitConverter.ToString(outputData));
                return outputData;
              } else {
                // Console.WriteLine("+ OK");
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

        //Console.WriteLine("Omen Key Off completed successfully.");
      } catch (Exception ex) {
        Console.WriteLine("Error: " + ex.Message);
      }
    }

    public static void OmenKeyOn(string method) {
      const string namespaceName = @"root\subscription";
      var scope = new ManagementScope(namespaceName);

      try {
        scope.Connect();

        // Create CommandLineEventConsumer
        var consumerClass = new ManagementClass(scope, new ManagementPath("CommandLineEventConsumer"), null);
        var consumer = consumerClass.CreateInstance();
        string currentPath = AppDomain.CurrentDomain.BaseDirectory;
        if (method == "custom") {
          consumer["CommandLineTemplate"] = @"cmd /c echo OmenKeyTriggered > \\.\pipe\OmenSuperHubPipe";
        } else {
          consumer["CommandLineTemplate"] = @"C:\Windows\System32\schtasks.exe /run /tn ""Omen Key""";
        }
        consumer["Name"] = "OmenKeyConsumer";
        consumer.Put();

        // Create EventFilter
        var filterClass = new ManagementClass(scope, new ManagementPath("__EventFilter"), null);
        var filter = filterClass.CreateInstance();
        filter["EventNameSpace"] = @"root\wmi";
        filter["Name"] = "OmenKeyFilter";
        filter["Query"] = "SELECT * FROM hpqBEvnt WHERE eventData = 8613 AND eventId = 29";
        filter["QueryLanguage"] = "WQL";
        filter.Put();

        // Create FilterToConsumerBinding
        var bindingClass = new ManagementClass(scope, new ManagementPath("__FilterToConsumerBinding"), null);
        var binding = bindingClass.CreateInstance();
        binding["Consumer"] = new ManagementPath(@"root\subscription:CommandLineEventConsumer.Name='OmenKeyConsumer'");
        binding["Filter"] = new ManagementPath(@"root\subscription:__EventFilter.Name='OmenKeyFilter'");
        binding.Put();

        //Console.WriteLine("Omen Key On completed successfully.");
      } catch (Exception ex) {
        Console.WriteLine("Error: " + ex.Message);
      }
    }
  }
}
