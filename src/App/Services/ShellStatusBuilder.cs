using System;
using System.Collections.Generic;
using System.IO;
using static OmenSuperHub.OmenHardware;

namespace OmenSuperHub {
  internal sealed class ShellStatusBuilder {
    public AppShellStatus Build(AppRuntimeState state, string baseDirectory, bool mainWindowVisible) {
      if (state == null) {
        return new AppShellStatus();
      }

      return new AppShellStatus {
        IconMode = state.CustomIconMode,
        TrayText = BuildTraySummaryText(state),
        DynamicIconValue = (int)state.CpuTemperature,
        CustomIconPath = Path.Combine(baseDirectory, "custom.ico"),
        FloatingVisible = state.FloatingBarEnabled,
        FloatingText = BuildMonitorText(state),
        FloatingTextSize = state.FloatingBarTextSize,
        FloatingLocation = state.FloatingBarLocation,
        MainWindowVisible = mainWindowVisible
      };
    }

    static string BuildTraySummaryText(AppRuntimeState state) {
      List<string> parts = new List<string>();
      parts.Add($"CPU {state.CpuTemperature:F0}C {state.CpuPowerWatts:F0}W");

      if (state.MonitorGpu)
        parts.Add($"GPU {state.GpuTemperature:F0}C {state.GpuPowerWatts:F0}W");

      float? batteryWatts = HardwareTelemetryService.GetBatteryPowerWatts(state.Battery);
      if (batteryWatts.HasValue && !state.AcOnline)
        parts.Add($"BAT {batteryWatts.Value:F0}W");

      if (state.GraphicsMode != OmenGfxMode.Unknown)
        parts.Add(FormatGfxMode(state.GraphicsMode));

      string text = string.Join(" | ", parts);
      return text.Length > 63 ? text.Substring(0, 63) : text;
    }

    static string BuildMonitorText(AppRuntimeState state) {
      List<string> lines = new List<string>();
      lines.Add($"CPU: {state.CpuTemperature:F1}°C  {state.CpuPowerWatts:F1}W");

      if (state.MonitorGpu)
        lines.Add($"GPU: {state.GpuTemperature:F1}°C  {state.GpuPowerWatts:F1}W");

      float systemPower = state.CpuPowerWatts + (state.MonitorGpu ? state.GpuPowerWatts : 0f);
      string source = state.AcOnline ? "AC" : "BAT";
      if (!state.AcOnline && state.Battery != null) {
        float? batteryWatts = HardwareTelemetryService.GetBatteryPowerWatts(state.Battery);
        if (batteryWatts.HasValue) {
          systemPower = batteryWatts.Value;
        }
      }
      lines.Add($"SYS: {systemPower:F1}W ({source})");

      if (state.MonitorFan && state.FanSpeeds != null && state.FanSpeeds.Count >= 2)
        lines.Add($"FAN: {state.FanSpeeds[0] * 100}/{state.FanSpeeds[1] * 100} RPM");

      return string.Join("\n", lines);
    }

    static string FormatGfxMode(OmenGfxMode mode) {
      switch (mode) {
        case OmenGfxMode.Hybrid:
          return "Hybrid";
        case OmenGfxMode.Discrete:
          return "Discrete";
        case OmenGfxMode.Optimus:
          return "Optimus";
        default:
          return "Unknown";
      }
    }
  }
}
