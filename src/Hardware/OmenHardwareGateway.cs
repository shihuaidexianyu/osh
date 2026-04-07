using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using static OmenSuperHub.OmenHardware;

namespace OmenSuperHub {
  internal sealed class OmenHardwareGateway : IOmenHardwareGateway {
    ManagementObjectSearcher searcher;
    ManagementObject biosMethods;
    readonly object biosLock = new object();
    DateTime lastAccessDeniedLogUtc = DateTime.MinValue;
    DateTime lastErrorLogUtc = DateTime.MinValue;
    string lastErrorMessage = string.Empty;

    static readonly TimeSpan ErrorLogThrottleWindow = TimeSpan.FromSeconds(30);

    public void GetFanCount() {
      SendOmenBiosWmi(0x10, new byte[] { 0x00, 0x00, 0x00, 0x00 }, 4);
    }

    public List<int> GetFanLevel() {
      List<int> fanSpeedNow = new List<int> { 0, 0 };
      byte[] fanLevel = SendOmenBiosWmi(0x2D, new byte[] { 0x00, 0x00, 0x00, 0x00 }, 128);
      if (fanLevel != null) {
        fanSpeedNow[0] = fanLevel[0];
        fanSpeedNow[1] = fanLevel[1];
      }
      return fanSpeedNow;
    }

    public byte[] GetFanTable() {
      return SendOmenBiosWmi(0x2F, new byte[] { 0x00, 0x00, 0x00, 0x00 }, 128);
    }

    public OmenFanTypeInfo GetFanTypeInfo() {
      byte[] data = SendOmenBiosWmi(0x2C, new byte[] { 0x00, 0x00, 0x00, 0x00 }, 128);
      if (data == null || data.Length == 0)
        return null;

      return new OmenFanTypeInfo {
        RawValue = data[0],
        Fan1Type = data[0] & 0x0F,
        Fan2Type = (data[0] >> 4) & 0x0F
      };
    }

    public OmenGfxMode GetGraphicsMode() {
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

    public OmenGpuStatus GetGpuStatus() {
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

    public OmenSystemDesignData GetSystemDesignData() {
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
        GraphicsSwitcherSupported = (data[7] & 0x0C) != 0,
        GraphicsHybridModeSupported = (data[7] & 0x04) != 0,
        GraphicsOptimusModeSupported = (data[7] & 0x08) != 0,
        RawData = data
      };
    }

    public void SetFanLevel(int fanSpeed1, int fanSpeed2) {
      int safeFanSpeed1 = Math.Max(0, Math.Min(64, fanSpeed1));
      int safeFanSpeed2 = Math.Max(0, Math.Min(64, fanSpeed2));
      SendOmenBiosWmi(0x2E, new byte[] { (byte)safeFanSpeed1, (byte)safeFanSpeed2 }, 0);
    }

    public void SetFanMode(byte mode) {
      SendOmenBiosWmi(0x1A, new byte[] { 0xFF, mode }, 0);
    }

    public void SetMaxGpuPower() {
      SendOmenBiosWmi(0x22, new byte[] { 0x01, 0x01, 0x01, 0x00 }, 0);
    }

    public void SetMedGpuPower() {
      SendOmenBiosWmi(0x22, new byte[] { 0x01, 0x00, 0x01, 0x00 }, 0);
    }

    public void SetMinGpuPower() {
      SendOmenBiosWmi(0x22, new byte[] { 0x00, 0x00, 0x01, 0x00 }, 0);
    }

    public void SetCpuPowerLimit(byte value) {
      SendOmenBiosWmi(0x29, new byte[] { value, value, 0xFF, 0xFF }, 0);
    }

    public void SetMaxFanSpeedOn() {
      SendOmenBiosWmi(0x27, new byte[] { 0x01 }, 0);
    }

    public void SetMaxFanSpeedOff() {
      SendOmenBiosWmi(0x27, new byte[] { 0x00 }, 0);
    }

    public OmenKeyboardType GetKeyboardType() {
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

    public OmenSmartAdapterStatus GetSmartAdapterStatus() {
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

    public byte[] SendOmenBiosWmi(uint commandType, byte[] data, int outputSize, uint command = 0x20008) {
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
          LogHardwareError(ex);
          return null;
        }
      }

      return null;
    }

    public void OmenKeyOff() {
      const string namespaceName = @"root\subscription";
      var scope = new ManagementScope(namespaceName);

      try {
        scope.Connect();

        var query = new ObjectQuery("SELECT * FROM __EventFilter WHERE Name='OmenKeyFilter'");
        var eventFilterSearcher = new ManagementObjectSearcher(scope, query);
        foreach (ManagementObject mo in eventFilterSearcher.Get()) {
          mo.Delete();
        }

        query = new ObjectQuery("SELECT * FROM CommandLineEventConsumer WHERE Name='OmenKeyConsumer'");
        var consumerSearcher = new ManagementObjectSearcher(scope, query);
        foreach (ManagementObject mo in consumerSearcher.Get()) {
          mo.Delete();
        }

        query = new ObjectQuery("SELECT * FROM __FilterToConsumerBinding WHERE Filter='__EventFilter.Name=\"OmenKeyFilter\"'");
        var bindingSearcher = new ManagementObjectSearcher(scope, query);
        foreach (ManagementObject mo in bindingSearcher.Get()) {
          mo.Delete();
        }
      } catch (Exception ex) {
        LogHardwareError(ex);
      }
    }

    public void OmenKeyOn(string method) {
      const string namespaceName = @"root\subscription";
      var scope = new ManagementScope(namespaceName);

      try {
        scope.Connect();

        var consumerClass = new ManagementClass(scope, new ManagementPath("CommandLineEventConsumer"), null);
        var consumer = consumerClass.CreateInstance();
        if (method == "custom") {
          consumer["CommandLineTemplate"] = @"cmd /c echo OmenKeyTriggered > \\.\pipe\OmenSuperHubPipe";
        } else {
          consumer["CommandLineTemplate"] = @"cmd /c echo OmenKeyShowMainWindow > \\.\pipe\OmenSuperHubPipe";
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
        LogHardwareError(ex);
      }
    }

    void LogHardwareError(Exception ex) {
      if (ex == null) {
        return;
      }

      DateTime nowUtc = DateTime.UtcNow;
      if (IsAccessDenied(ex)) {
        if (nowUtc - lastAccessDeniedLogUtc >= ErrorLogThrottleWindow) {
          Console.WriteLine("Error: 拒绝访问（请使用管理员权限运行，或检查 WMI 命名空间权限）。");
          lastAccessDeniedLogUtc = nowUtc;
        }
        return;
      }

      string message = ex.Message ?? string.Empty;
      bool shouldLog =
        !string.Equals(lastErrorMessage, message, StringComparison.Ordinal) ||
        nowUtc - lastErrorLogUtc >= ErrorLogThrottleWindow;

      if (shouldLog) {
        Console.WriteLine("Error: " + message);
        lastErrorMessage = message;
        lastErrorLogUtc = nowUtc;
      }
    }

    static bool IsAccessDenied(Exception ex) {
      if (ex is UnauthorizedAccessException) {
        return true;
      }

      string message = ex.Message ?? string.Empty;
      return message.IndexOf("access is denied", StringComparison.OrdinalIgnoreCase) >= 0 ||
             message.IndexOf("拒绝访问", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    void ResetBiosSession() {
      if (biosMethods != null) {
        biosMethods.Dispose();
        biosMethods = null;
      }

      if (searcher != null) {
        searcher.Dispose();
        searcher = null;
      }
    }
  }
}
