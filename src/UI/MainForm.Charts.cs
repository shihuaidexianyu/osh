using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using WinForms = System.Windows.Forms;
using static OmenSuperHub.OmenHardware;

namespace OmenSuperHub {
  public sealed partial class MainForm {
    void UpdateSmartPowerVisual(DashboardSnapshot snapshot) {
      if (snapshot == null) {
        return;
      }

      string state = snapshot.SmartPowerControlEnabled ? (snapshot.SmartPowerControlState ?? "balanced") : "manual";
      if (smartStateText != null) {
        smartStateText.Text = FormatSmartStateLabel(state);
      }
      if (smartReasonText != null) {
        smartReasonText.Text = $"原因: {FormatSmartReason(snapshot.SmartPowerControlReason)}";
      }
      if (smartStateBadge != null) {
        smartStateBadge.Background = GetSmartStateBrush(state);
      }

      float estimated = snapshot.EstimatedSystemPowerWatts > 0 ? snapshot.EstimatedSystemPowerWatts : snapshot.CpuPowerWatts + snapshot.GpuPowerWatts;
      float target = snapshot.TargetSystemPowerWatts > 1f ? snapshot.TargetSystemPowerWatts : Math.Max(30f, estimated);
      float delta = estimated - target;
      if (smartBudgetText != null) {
        smartBudgetText.Text = $"预算 {estimated:F1} / {target:F1} W ({delta:+0.0;-0.0;0.0})";
        smartBudgetText.Foreground = delta > 3f ? accentOrange : strongText;
      }
      if (smartBudgetBar != null) {
        double max = Math.Max(100d, Math.Max(target * 1.35d, estimated + 8d));
        smartBudgetBar.Maximum = max;
        smartBudgetBar.Value = ClampDouble(estimated, 0d, max);
        smartBudgetBar.Foreground = delta > 3f ? accentOrange : accentGreen;
      }

      if (smartCpuTempText != null) {
        smartCpuTempText.Text = $"{snapshot.CpuTemperature:F1} °C";
      }
      if (smartCpuTempBar != null) {
        smartCpuTempBar.Value = ClampDouble(snapshot.CpuTemperature, 0d, 100d);
        smartCpuTempBar.Foreground = snapshot.CpuTemperature >= 90f ? accentOrange : accentBlue;
      }

      if (snapshot.MonitorGpu) {
        if (smartGpuTempText != null) {
          smartGpuTempText.Text = $"{snapshot.GpuTemperature:F1} °C";
          smartGpuTempText.Foreground = strongText;
        }
        if (smartGpuTempBar != null) {
          smartGpuTempBar.IsEnabled = true;
          smartGpuTempBar.Value = ClampDouble(snapshot.GpuTemperature, 0d, 100d);
          smartGpuTempBar.Foreground = snapshot.GpuTemperature >= 84f ? accentOrange : accentBlue;
        }
      } else {
        if (smartGpuTempText != null) {
          smartGpuTempText.Text = "监控关闭";
          smartGpuTempText.Foreground = mutedText;
        }
        if (smartGpuTempBar != null) {
          smartGpuTempBar.IsEnabled = false;
          smartGpuTempBar.Value = 0;
        }
      }

    }

    void UpdateTemperatureSensorsView(DashboardSnapshot snapshot) {
      if (temperatureSensorSummaryText == null || temperatureTrendCanvas == null) {
        return;
      }

      var sensors = snapshot?.TemperatureSensors;
      if (sensors == null || sensors.Count == 0) {
        temperatureSensorSummaryText.Text = "未读取到温度传感器。";
        return;
      }

      var hottest = sensors[0];
      AppendHistory(cpuControlTempHistory, snapshot.ControlCpuTemperature);
      AppendHistory(gpuControlTempHistory, snapshot.MonitorGpu ? snapshot.ControlGpuTemperature : float.NaN);
      AppendHistory(hottestTempHistory, hottest.Celsius);
      AppendHistory(cpuWallTempHistory, snapshot.ControlCpuTempWall);
      AppendHistory(gpuWallTempHistory, snapshot.ControlGpuTempWall);
      AppendHistory(cpuLimitHistory, snapshot.SmartCpuLimitWatts > 0 ? snapshot.SmartCpuLimitWatts : float.NaN);

      temperatureSensorSummaryText.Text =
        $"已读取 {sensors.Count} 个传感器，当前最高温 {hottest.Celsius:F1} °C。";

      DrawTemperatureTrendChart();
    }

    void AppendHistory(List<float> history, float value) {
      if (history == null) {
        return;
      }

      history.Add(value);
      if (history.Count > TemperatureTrendCapacity) {
        history.RemoveAt(0);
      }
    }

    void DrawTemperatureTrendChart() {
      if (temperatureTrendCanvas == null) {
        return;
      }

      double width = temperatureTrendCanvas.ActualWidth;
      if (width < 40) width = 720;
      double height = temperatureTrendCanvas.ActualHeight;
      if (height < 40) height = 220;

      float minTemp = 50f;
      float maxTemp = 100f;
      ExpandTemperatureRange(cpuControlTempHistory, ref minTemp, ref maxTemp);
      ExpandTemperatureRange(gpuControlTempHistory, ref minTemp, ref maxTemp);
      ExpandTemperatureRange(hottestTempHistory, ref minTemp, ref maxTemp);
      ExpandTemperatureRange(cpuWallTempHistory, ref minTemp, ref maxTemp);
      ExpandTemperatureRange(gpuWallTempHistory, ref minTemp, ref maxTemp);
      minTemp = Math.Max(20f, minTemp - 2f);
      maxTemp = Math.Min(110f, maxTemp + 2f);
      if (maxTemp - minTemp < 12f) {
        maxTemp = minTemp + 12f;
      }

      temperatureTrendCanvas.Children.Clear();
      DrawChartGrid(width, height, minTemp, maxTemp);
      DrawSeries(cpuControlTempHistory, width, height, minTemp, maxTemp, accentBlue, 2.2, null);
      DrawSeries(gpuControlTempHistory, width, height, minTemp, maxTemp, Brushes.IndianRed, 2.0, null);
      DrawSeries(hottestTempHistory, width, height, minTemp, maxTemp, accentOrange, 1.8, null);
      DrawSeries(cpuWallTempHistory, width, height, minTemp, maxTemp, Brushes.ForestGreen, 1.4, new DoubleCollection { 3, 2 });
      DrawSeries(gpuWallTempHistory, width, height, minTemp, maxTemp, Brushes.DarkOliveGreen, 1.4, new DoubleCollection { 3, 2 });
      DrawCpuLimitOverlay(width, height, Brushes.DimGray);
    }

    void ExpandTemperatureRange(List<float> values, ref float minTemp, ref float maxTemp) {
      if (values == null) {
        return;
      }

      foreach (float value in values) {
        if (float.IsNaN(value)) {
          continue;
        }

        if (value < minTemp) minTemp = value;
        if (value > maxTemp) maxTemp = value;
      }
    }

    void DrawChartGrid(double width, double height, float minTemp, float maxTemp) {
      const int rows = 5;
      for (int i = 0; i <= rows; i++) {
        double y = i * (height / rows);
        var line = new Line {
          X1 = 0,
          Y1 = y,
          X2 = width,
          Y2 = y,
          Stroke = new SolidColorBrush(Color.FromRgb(230, 233, 238)),
          StrokeThickness = i == rows ? 1.1 : 0.8
        };
        temperatureTrendCanvas.Children.Add(line);

        float temp = maxTemp - (float)((maxTemp - minTemp) * (y / height));
        var label = new TextBlock {
          Text = $"{temp:F0}°",
          Foreground = mutedText,
          FontSize = 10
        };
        Canvas.SetLeft(label, 4);
        Canvas.SetTop(label, Math.Max(0, y - 12));
        temperatureTrendCanvas.Children.Add(label);
      }
    }

    void DrawSeries(List<float> values, double width, double height, float minTemp, float maxTemp, Brush stroke, double thickness, DoubleCollection dash) {
      if (values == null || values.Count < 2) {
        return;
      }

      int count = values.Count;
      var polyline = new Polyline {
        Stroke = stroke,
        StrokeThickness = thickness,
        SnapsToDevicePixels = true
      };
      if (dash != null) {
        polyline.StrokeDashArray = dash;
      }

      double xStep = count > 1 ? width / (count - 1) : width;
      for (int i = 0; i < count; i++) {
        float value = values[i];
        if (float.IsNaN(value)) {
          continue;
        }

        double x = i * xStep;
        double ratio = (value - minTemp) / Math.Max(0.001f, maxTemp - minTemp);
        double y = height - (ratio * height);
        y = ClampDouble(y, 0, height);
        polyline.Points.Add(new Point(x, y));
      }

      if (polyline.Points.Count >= 2) {
        temperatureTrendCanvas.Children.Add(polyline);
      }
    }

    void DrawCpuLimitOverlay(double width, double height, Brush stroke) {
      if (cpuLimitHistory == null || cpuLimitHistory.Count < 2) {
        return;
      }

      const float minLimit = 25f;
      const float maxLimit = 125f;
      int count = cpuLimitHistory.Count;
      double xStep = count > 1 ? width / (count - 1) : width;

      var polyline = new Polyline {
        Stroke = stroke,
        StrokeThickness = 1.2,
        StrokeDashArray = new DoubleCollection { 2, 3 },
        Opacity = 0.6
      };

      for (int i = 0; i < count; i++) {
        float value = cpuLimitHistory[i];
        if (float.IsNaN(value)) {
          continue;
        }

        double x = i * xStep;
        double ratio = (value - minLimit) / (maxLimit - minLimit);
        double y = height - ClampDouble(ratio, 0, 1) * height;
        polyline.Points.Add(new Point(x, y));
      }

      if (polyline.Points.Count >= 2) {
        temperatureTrendCanvas.Children.Add(polyline);
      }
    }

    string FormatSmartStateLabel(string state) {
      switch ((state ?? string.Empty).ToLowerInvariant()) {
        case "eco":
          return "ECO";
        case "performance":
          return "PERFORMANCE";
        case "thermal_protect":
          return "THERMAL PROTECT";
        case "battery_guard":
          return "BATTERY GUARD";
        case "manual":
          return "MANUAL";
        default:
          return "BALANCED";
      }
    }

    string FormatSmartReason(string reason) {
      if (string.IsNullOrWhiteSpace(reason)) {
        return "--";
      }

      switch (reason) {
        case "thermal-ceiling":
          return "温度触顶保护";
        case "battery-discharge":
          return "电池放电保护";
        case "promote-performance":
          return "性能窗口放宽";
        case "performance-window":
          return "处于性能窗口";
        case "power-saving":
          return "节能降载";
        case "temp-wall-feedback":
          return "温度墙负反馈";
        case "budget-limit":
          return "超出预算";
        case "eco-stable":
          return "节能稳定";
        case "state-stabilizing":
          return "状态稳定期";
        case "balanced-stable":
          return "平衡稳定";
        case "disabled":
          return "已关闭";
        default:
          return reason;
      }
    }

    string FormatGpuTier(string tier) {
      switch ((tier ?? string.Empty).ToLowerInvariant()) {
        case "max":
          return "高";
        case "med":
          return "中";
        case "min":
          return "低";
        default:
          return tier ?? "--";
      }
    }

    Brush GetSmartStateBrush(string state) {
      switch ((state ?? string.Empty).ToLowerInvariant()) {
        case "eco":
          return accentGreen;
        case "performance":
          return accentBlue;
        case "thermal_protect":
          return accentOrange;
        case "battery_guard":
          return new SolidColorBrush(Color.FromRgb(151, 99, 8));
        case "manual":
          return mutedText;
        default:
          return new SolidColorBrush(Color.FromRgb(72, 98, 130));
      }
    }

    static double ClampDouble(double value, double min, double max) {
      if (value < min) return min;
      if (value > max) return max;
      return value;
    }

    string BuildBatterySummary(DashboardSnapshot snapshot) {
      if (snapshot.Battery == null) return "Battery --";
      float? power = GetBatteryPowerWatts(snapshot.Battery);
      if (power.HasValue) return $"{BuildBatteryState(snapshot.Battery)} {power.Value:F1}W";
      return $"{BuildBatteryState(snapshot.Battery)} {snapshot.BatteryPercent}%";
    }

    string BuildCapabilitiesSummary(DashboardSnapshot snapshot) {
      var parts = new List<string>();
      if (snapshot.SystemDesignData != null) {
        if (snapshot.SystemDesignData.GraphicsSwitcherSupported) parts.Add("GfxSwitch");
        if (snapshot.SystemDesignData.SoftwareFanControlSupported) parts.Add("SW Fan");
        if (snapshot.SystemDesignData.DefaultPl4 > 0) parts.Add($"PL4 {snapshot.SystemDesignData.DefaultPl4}W");
      }
      if (snapshot.FanTypeInfo != null) {
        parts.Add($"Fan {snapshot.FanTypeInfo.Fan1Type}/{snapshot.FanTypeInfo.Fan2Type}");
      }
      return parts.Count == 0 ? "Unknown" : string.Join(" | ", parts);
    }

    string ConvertFanControlValue(string value) {
      if (value == "auto") return "自动";
      if (value == "max") return "最大风扇";
      return value;
    }

    string ConvertUsageMode(string value) {
      switch ((value ?? string.Empty).ToLowerInvariant()) {
        case "quiet":
          return "安静";
        case "max":
          return "MAX";
        case "performance":
          return "性能";
        case "custom":
          return "自定义";
        default:
          return "均衡";
      }
    }

    string ConvertUsageModeBack(string value) {
      switch (value) {
        case "安静":
          return "quiet";
        case "MAX":
          return "max";
        case "性能":
          return "performance";
        case "自定义":
          return "custom";
        default:
          return "balanced";
      }
    }

    string ConvertGpuPowerValue(string value) {
      if (value == "max") return "高性能";
      if (value == "med") return "均衡";
      return "节能";
    }

    string ConvertGpuPowerValueBack(string value) {
      if (value == "高性能") return "max";
      if (value == "均衡") return "med";
      return "min";
    }

    string ConvertTempSensitivity(string value) {
      if (value == "realtime") return "实时";
      if (value == "medium") return "中";
      if (value == "low") return "低";
      return "高";
    }

    string ConvertTempSensitivityBack(string value) {
      if (value == "实时") return "realtime";
      if (value == "中") return "medium";
      if (value == "低") return "low";
      return "high";
    }

    string ConvertOmenKeyMode(string value) {
      switch ((value ?? string.Empty).ToLowerInvariant()) {
        case "custom":
          return "切换浮窗显示";
        case "none":
          return "禁用";
        default:
          return "默认";
      }
    }

    string ConvertOmenKeyModeBack(string value) {
      switch (value) {
        case "切换浮窗显示":
          return "custom";
        case "禁用":
          return "none";
        default:
          return "default";
      }
    }

    string BuildBatteryState(BatteryTelemetry telemetry) {
      if (telemetry == null) return "Unknown";
      if (telemetry.Discharging) return "Discharging";
      if (telemetry.Charging) return "Charging";
      return telemetry.PowerOnline ? "AC Idle" : "Battery Idle";
    }

    float? GetBatteryPowerWatts(BatteryTelemetry telemetry) {
      if (telemetry == null) return null;
      if (telemetry.Discharging && telemetry.DischargeRateMilliwatts > 0) return telemetry.DischargeRateMilliwatts / 1000f;
      if (telemetry.Charging && telemetry.ChargeRateMilliwatts > 0) return telemetry.ChargeRateMilliwatts / 1000f;
      return null;
    }

    float? GetBatteryDischargePowerWatts(BatteryTelemetry telemetry) {
      if (telemetry == null) return null;
      if (telemetry.Discharging && telemetry.DischargeRateMilliwatts > 0) return telemetry.DischargeRateMilliwatts / 1000f;
      return null;
    }

    string FormatFanRpm(List<int> fanSpeeds) {
      if (fanSpeeds == null || fanSpeeds.Count < 2) return "--";
      return $"{fanSpeeds[0] * 100} / {fanSpeeds[1] * 100} RPM";
    }

    string FormatGfxMode(OmenGfxMode mode) {
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

    string FormatGpuControl(OmenGpuStatus status) {
      if (status == null) return "Unknown";
      string mode = status.CustomTgpEnabled ? (status.PpabEnabled ? "cTGP+PPAB" : "cTGP") : "BaseTGP";
      return $"{mode} | D{status.DState}";
    }

    string FormatAdapterStatus(OmenSmartAdapterStatus status) {
      switch (status) {
        case OmenSmartAdapterStatus.MeetsRequirement:
          return "OK";
        case OmenSmartAdapterStatus.BatteryPower:
          return "Battery";
        case OmenSmartAdapterStatus.BelowRequirement:
          return "Low";
        case OmenSmartAdapterStatus.NotFunctioning:
          return "Fault";
        case OmenSmartAdapterStatus.NoSupport:
          return "N/A";
        default:
          return "?";
      }
    }

    string FormatKeyboardType(OmenKeyboardType type) {
      switch (type) {
        case OmenKeyboardType.Standard:
          return "Standard";
        case OmenKeyboardType.WithNumpad:
          return "With Numpad";
        case OmenKeyboardType.Tenkeyless:
          return "Tenkeyless";
        case OmenKeyboardType.PerKeyRgb:
          return "Per-Key RGB";
        default:
          return "Unknown";
      }
    }
  }
}
