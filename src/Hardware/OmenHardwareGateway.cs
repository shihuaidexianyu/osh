using System.Collections.Generic;
using static OmenSuperHub.OmenHardware;

namespace OmenSuperHub {
  internal sealed class OmenHardwareGateway : IOmenHardwareGateway {
    public void GetFanCount() {
      OmenHardware.GetFanCount();
    }

    public List<int> GetFanLevel() {
      return OmenHardware.GetFanLevel();
    }

    public byte[] GetFanTable() {
      return OmenHardware.GetFanTable();
    }

    public OmenFanTypeInfo GetFanTypeInfo() {
      return OmenHardware.GetFanTypeInfo();
    }

    public OmenGfxMode GetGraphicsMode() {
      return OmenHardware.GetGraphicsMode();
    }

    public void SetGraphicsMode(OmenGfxMode mode) {
      OmenHardware.SetGraphicsMode(mode);
    }

    public OmenGpuStatus GetGpuStatus() {
      return OmenHardware.GetGpuStatus();
    }

    public OmenSystemDesignData GetSystemDesignData() {
      return OmenHardware.GetSystemDesignData();
    }

    public void SetFanLevel(int fanSpeed1, int fanSpeed2) {
      OmenHardware.SetFanLevel(fanSpeed1, fanSpeed2);
    }

    public void SetFanMode(byte mode) {
      OmenHardware.SetFanMode(mode);
    }

    public void SetMaxGpuPower() {
      OmenHardware.SetMaxGpuPower();
    }

    public void SetMedGpuPower() {
      OmenHardware.SetMedGpuPower();
    }

    public void SetMinGpuPower() {
      OmenHardware.SetMinGpuPower();
    }

    public void SetCpuPowerLimit(byte value) {
      OmenHardware.SetCpuPowerLimit(value);
    }

    public void SetMaxFanSpeedOn() {
      OmenHardware.SetMaxFanSpeedOn();
    }

    public void SetMaxFanSpeedOff() {
      OmenHardware.SetMaxFanSpeedOff();
    }

    public OmenKeyboardType GetKeyboardType() {
      return OmenHardware.GetKeyboardType();
    }

    public OmenSmartAdapterStatus GetSmartAdapterStatus() {
      return OmenHardware.GetSmartAdapterStatus();
    }

    public byte[] SendOmenBiosWmi(uint commandType, byte[] data, int outputSize, uint command = 0x20008) {
      return OmenHardware.SendOmenBiosWmi(commandType, data, outputSize, command);
    }

    public void OmenKeyOff() {
      OmenHardware.OmenKeyOff();
    }

    public void OmenKeyOn(string method) {
      OmenHardware.OmenKeyOn(method);
    }
  }
}
