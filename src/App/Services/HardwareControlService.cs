using System;
using System.Collections.Generic;
using static OmenSuperHub.OmenHardware;

namespace OmenSuperHub {
  internal sealed class HardwareControlService {
    readonly IOmenHardwareGateway hardwareGateway;
    readonly ProcessCommandService processCommandService;

    public HardwareControlService(IOmenHardwareGateway hardwareGateway, ProcessCommandService processCommandService) {
      this.hardwareGateway = hardwareGateway;
      this.processCommandService = processCommandService;
    }

    public void RefreshFanControllerPresence() {
      hardwareGateway.GetFanCount();
    }

    public List<int> GetFanLevel() {
      return hardwareGateway.GetFanLevel();
    }

    public void SetFanLevel(int fanSpeed1, int fanSpeed2) {
      hardwareGateway.SetFanLevel(fanSpeed1, fanSpeed2);
    }

    public void SetFanMode(FanModeOption mode) {
      hardwareGateway.SetFanMode(mode == FanModeOption.Performance ? (byte)0x31 : (byte)0x30);
    }

    public void SetCpuPowerLimit(int watts) {
      hardwareGateway.SetCpuPowerLimit((byte)Math.Max(1, Math.Min(254, watts)));
    }

    public void ApplyGpuPower(GpuPowerOption value) {
      switch (value) {
        case GpuPowerOption.Max:
          hardwareGateway.SetMaxGpuPower();
          break;
        case GpuPowerOption.Min:
          hardwareGateway.SetMinGpuPower();
          break;
        default:
          hardwareGateway.SetMedGpuPower();
          break;
      }
    }

    public void ApplyGraphicsMode(GraphicsModeOption value) {
      switch (value) {
        case GraphicsModeOption.Discrete:
          hardwareGateway.SetGraphicsMode(OmenGfxMode.Discrete);
          break;
        case GraphicsModeOption.Optimus:
          hardwareGateway.SetGraphicsMode(OmenGfxMode.Optimus);
          break;
        default:
          hardwareGateway.SetGraphicsMode(OmenGfxMode.Hybrid);
          break;
      }
    }

    public void SetMaxFanSpeedEnabled(bool enabled) {
      if (enabled) {
        hardwareGateway.SetMaxFanSpeedOn();
      } else {
        hardwareGateway.SetMaxFanSpeedOff();
      }
    }

    public bool SetGpuClockLimit(int freq) {
      if (freq < 210) {
        processCommandService.Execute("nvidia-smi --reset-gpu-clocks");
        return false;
      }

      processCommandService.Execute("nvidia-smi --lock-gpu-clocks=0," + freq);
      return true;
    }

    public void SendResumeProbe() {
      hardwareGateway.SendOmenBiosWmi(0x10, new byte[] { 0x00, 0x00, 0x00, 0x00 }, 4);
    }

    public void DisableOmenKey() {
      hardwareGateway.OmenKeyOff();
    }

    public void EnableOmenKey(string method) {
      hardwareGateway.OmenKeyOn(method);
    }
  }
}
