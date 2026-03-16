using System.Collections.Generic;
using static OmenSuperHub.OmenHardware;

namespace OmenSuperHub {
  internal interface IOmenHardwareGateway {
    void GetFanCount();
    List<int> GetFanLevel();
    byte[] GetFanTable();
    OmenFanTypeInfo GetFanTypeInfo();
    OmenGfxMode GetGraphicsMode();
    OmenGpuStatus GetGpuStatus();
    OmenSystemDesignData GetSystemDesignData();
    void SetFanLevel(int fanSpeed1, int fanSpeed2);
    void SetFanMode(byte mode);
    void SetMaxGpuPower();
    void SetMedGpuPower();
    void SetMinGpuPower();
    void SetCpuPowerLimit(byte value);
    void SetMaxFanSpeedOn();
    void SetMaxFanSpeedOff();
    OmenKeyboardType GetKeyboardType();
    OmenSmartAdapterStatus GetSmartAdapterStatus();
    byte[] SendOmenBiosWmi(uint commandType, byte[] data, int outputSize, uint command = 0x20008);
    void OmenKeyOff();
    void OmenKeyOn(string method);
  }
}
