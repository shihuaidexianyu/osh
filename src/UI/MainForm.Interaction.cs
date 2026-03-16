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
    void RefreshDashboard() {
      if (window == null || !window.IsVisible) {
        return;
      }

      var snapshot = appController.GetDashboardSnapshot();

      float totalPower = snapshot.CpuPowerWatts + snapshot.GpuPowerWatts;
      if (!snapshot.AcOnline) {
        float? batteryDischarge = GetBatteryDischargePowerWatts(snapshot.Battery);
        if (batteryDischarge.HasValue)
          totalPower = batteryDischarge.Value;
      }
      totalPowerText.Text = $"{totalPower:F1} W";
      lastUpdateText.Text = hasPendingChanges ? "有未应用的更改" : $"最近刷新: {DateTime.Now:HH:mm:ss}";

      leftCpuText.Text = $"{snapshot.CpuTemperature:F1} °C | {snapshot.CpuPowerWatts:F1} W";
      leftGpuText.Text = snapshot.MonitorGpu ? $"{snapshot.GpuTemperature:F1} °C | {snapshot.GpuPowerWatts:F1} W" : "监控关闭";
      leftBatteryText.Text = BuildBatterySummary(snapshot);
      leftFanText.Text = FormatFanRpm(snapshot.FanSpeeds);
      leftModeText.Text = $"{ConvertUsageMode(snapshot.UsageMode)} · {ConvertFanControlValue(snapshot.FanControl)}";

      gfxModeText.Text = FormatGfxMode(snapshot.GraphicsMode);
      adapterText.Text = $"{FormatAdapterStatus(snapshot.SmartAdapterStatus)} / {(snapshot.AcOnline ? "AC" : "Battery")}";
      gpuControlText.Text = FormatGpuControl(snapshot.GpuStatus);
      capabilitiesText.Text = BuildCapabilitiesSummary(snapshot);
      keyboardText.Text = FormatKeyboardType(snapshot.KeyboardType);
      fanTypeText.Text = snapshot.FanTypeInfo == null ? "Unknown" : $"{snapshot.FanTypeInfo.Fan1Type}/{snapshot.FanTypeInfo.Fan2Type}";
      UpdateTemperatureSensorsView(snapshot);
      UpdateSmartPowerVisual(snapshot);

      if (!hasPendingChanges && !IsControlInteractionActive()) {
        SyncControlState(snapshot);
      }
    }

    void MarkPendingChanges(bool setCustomUsageMode = false) {
      if (setCustomUsageMode && usageModeComboBox != null) {
        syncingControlState = true;
        try {
          SelectComboItem(usageModeComboBox, "自定义");
        } finally {
          syncingControlState = false;
        }
      }

      hasPendingChanges = true;
      UpdatePendingChangeButtons();
    }

    void ClearPendingChanges() {
      hasPendingChanges = false;
      UpdatePendingChangeButtons();
    }

    void UpdatePendingChangeButtons() {
      if (applyChangesButton != null) {
        applyChangesButton.IsEnabled = hasPendingChanges;
        applyChangesButton.Opacity = hasPendingChanges ? 1.0 : 0.55;
      }
      if (discardChangesButton != null) {
        discardChangesButton.IsEnabled = hasPendingChanges;
        discardChangesButton.Opacity = hasPendingChanges ? 1.0 : 0.55;
      }
      if (lastUpdateText != null && hasPendingChanges) {
        lastUpdateText.Text = "有未应用的更改";
      }
    }

    bool IsControlInteractionActive() {
      if (fanModeComboBox?.IsDropDownOpen == true) return true;
      if (usageModeComboBox?.IsDropDownOpen == true) return true;
      if (fanControlComboBox?.IsDropDownOpen == true) return true;
      if (fanTableComboBox?.IsDropDownOpen == true) return true;
      if (tempSensitivityComboBox?.IsDropDownOpen == true) return true;
      if (cpuPowerComboBox?.IsDropDownOpen == true) return true;
      if (gpuPowerComboBox?.IsDropDownOpen == true) return true;
      if (graphicsModeComboBox?.IsDropDownOpen == true) return true;
      if (gpuClockComboBox?.IsDropDownOpen == true) return true;
      if (floatingBarLocationComboBox?.IsDropDownOpen == true) return true;

      if (manualFanRpmSlider?.IsMouseCaptureWithin == true) return true;
      if (cpuEmergencySlider?.IsMouseCaptureWithin == true) return true;
      if (gpuEmergencySlider?.IsMouseCaptureWithin == true) return true;
      if (cpuRecoverSlider?.IsMouseCaptureWithin == true) return true;
      if (gpuRecoverSlider?.IsMouseCaptureWithin == true) return true;
      if (cpuFanBoostOnSlider?.IsMouseCaptureWithin == true) return true;
      if (gpuFanBoostOnSlider?.IsMouseCaptureWithin == true) return true;
      if (cpuFanBoostOffSlider?.IsMouseCaptureWithin == true) return true;
      if (gpuFanBoostOffSlider?.IsMouseCaptureWithin == true) return true;
      if (batteryGuardTriggerSlider?.IsMouseCaptureWithin == true) return true;
      if (batteryGuardReleaseSlider?.IsMouseCaptureWithin == true) return true;

      return false;
    }

    void SyncControlState(DashboardSnapshot snapshot) {
      syncingControlState = true;
      try {
        SelectComboItem(usageModeComboBox, ConvertUsageMode(snapshot.UsageMode));
        SelectComboItem(fanModeComboBox, snapshot.FanMode == "performance" ? "性能" : "均衡");
        int manualRpm = ParseManualFanRpm(snapshot.FanControl);
        if (snapshot.FanControl == "auto") {
          SelectComboItem(fanControlComboBox, "自动");
          SetManualFanSliderEnabled(false);
        } else if (snapshot.FanControl == "max") {
          SelectComboItem(fanControlComboBox, "最大风扇");
          SetManualFanSliderEnabled(false);
        } else {
          SelectComboItem(fanControlComboBox, "手动");
          SetManualFanSliderEnabled(true);
          if (manualFanRpmSlider != null) {
            manualFanRpmSlider.Value = manualRpm;
          }
        }
        UpdateManualFanRpmText(manualRpm);
        SelectComboItem(fanTableComboBox, snapshot.FanTable == "cool" ? "降温模式" : "安静模式");
        SelectComboItem(tempSensitivityComboBox, ConvertTempSensitivity(snapshot.TempSensitivity));
        SelectComboItem(cpuPowerComboBox, snapshot.CpuPowerSetting == "max" ? "最大" : snapshot.CpuPowerSetting);
        SelectComboItem(gpuPowerComboBox, ConvertGpuPowerValue(snapshot.GpuPowerSetting));
        SelectComboItem(graphicsModeComboBox, ConvertGraphicsModeSetting(snapshot.GraphicsModeSetting));
        if (graphicsModeComboBox != null) {
          bool canSwitchGraphics = snapshot.SystemDesignData != null && snapshot.SystemDesignData.GraphicsSwitcherSupported;
          graphicsModeComboBox.IsEnabled = canSwitchGraphics;
        }
        SelectComboItem(gpuClockComboBox, snapshot.GpuClockLimit > 0 ? $"{snapshot.GpuClockLimit} MHz" : "还原");
        smartPowerControlCheckBox.IsChecked = snapshot.SmartPowerControlEnabled;
        SyncPowerTuningControls();

        bool overlayEnabled = snapshot.FloatingBarEnabled;
        floatingBarButton.Content = overlayEnabled ? "浮窗: 开启" : "浮窗: 关闭";
        floatingBarButton.Tag = overlayEnabled;
        floatingBarButton.Background = overlayEnabled
          ? new SolidColorBrush(Color.FromRgb(229, 247, 240))
          : subtleFill;
        floatingBarButton.Foreground = overlayEnabled ? accentGreen : strongText;
        SelectComboItem(floatingBarLocationComboBox, snapshot.FloatingBarLocation == "right" ? "右上角" : "左上角");
      } finally {
        syncingControlState = false;
      }
    }

    void SelectComboItem(ComboBox comboBox, string value) {
      if (comboBox == null) return;
      if (comboBox.Items.Contains(value)) {
        comboBox.SelectedItem = value;
      } else if (comboBox.SelectedIndex < 0 && comboBox.Items.Count > 0) {
        comboBox.SelectedIndex = 0;
      }
    }

    void ApplyUsageModeToControls(string mode) {
      syncingControlState = true;
      try {
        switch (mode) {
          case "quiet":
            SelectComboItem(fanModeComboBox, "均衡");
            SelectComboItem(fanControlComboBox, "自动");
            SetManualFanSliderEnabled(false);
            SelectComboItem(fanTableComboBox, "安静模式");
            SelectComboItem(tempSensitivityComboBox, "低");
            SelectComboItem(cpuPowerComboBox, "45 W");
            SelectComboItem(gpuPowerComboBox, "节能");
            SelectComboItem(graphicsModeComboBox, "混合输出");
            SelectComboItem(gpuClockComboBox, "还原");
            if (smartPowerControlCheckBox != null) smartPowerControlCheckBox.IsChecked = true;
            break;
          case "performance":
            SelectComboItem(fanModeComboBox, "性能");
            SelectComboItem(fanControlComboBox, "自动");
            SetManualFanSliderEnabled(false);
            SelectComboItem(fanTableComboBox, "降温模式");
            SelectComboItem(tempSensitivityComboBox, "高");
            SelectComboItem(cpuPowerComboBox, "最大");
            SelectComboItem(gpuPowerComboBox, "高性能");
            SelectComboItem(graphicsModeComboBox, "混合输出");
            SelectComboItem(gpuClockComboBox, "还原");
            if (smartPowerControlCheckBox != null) smartPowerControlCheckBox.IsChecked = true;
            break;
          case "max":
            SelectComboItem(fanModeComboBox, "性能");
            SelectComboItem(fanControlComboBox, "最大风扇");
            SetManualFanSliderEnabled(false);
            SelectComboItem(fanTableComboBox, "降温模式");
            SelectComboItem(tempSensitivityComboBox, "实时");
            SelectComboItem(cpuPowerComboBox, "最大");
            SelectComboItem(gpuPowerComboBox, "高性能");
            SelectComboItem(graphicsModeComboBox, "独显直连");
            SelectComboItem(gpuClockComboBox, "还原");
            if (smartPowerControlCheckBox != null) smartPowerControlCheckBox.IsChecked = false;
            break;
          default:
            SelectComboItem(fanModeComboBox, "均衡");
            SelectComboItem(fanControlComboBox, "自动");
            SetManualFanSliderEnabled(false);
            SelectComboItem(fanTableComboBox, "安静模式");
            SelectComboItem(tempSensitivityComboBox, "中");
            SelectComboItem(cpuPowerComboBox, "65 W");
            SelectComboItem(gpuPowerComboBox, "均衡");
            SelectComboItem(graphicsModeComboBox, "混合输出");
            SelectComboItem(gpuClockComboBox, "还原");
            if (smartPowerControlCheckBox != null) smartPowerControlCheckBox.IsChecked = true;
            break;
        }
      } finally {
        syncingControlState = false;
      }
    }

    void ApplyChangesButton_Click(object sender, RoutedEventArgs e) {
      if (!hasPendingChanges) {
        return;
      }

      string selectedUsageMode = usageModeComboBox?.SelectedItem == null
        ? "balanced"
        : ConvertUsageModeBack(usageModeComboBox.SelectedItem.ToString());

      if (selectedUsageMode != "custom") {
        appController.ApplyUsageModeSetting(selectedUsageMode);
      } else {
        appController.ApplyFanModeSetting(fanModeComboBox?.SelectedItem?.ToString() == "性能" ? "performance" : "default");

        string fanControlSelection = fanControlComboBox?.SelectedItem?.ToString() ?? "自动";
        if (fanControlSelection == "自动") {
          appController.ApplyFanControlSetting("auto");
        } else if (fanControlSelection == "最大风扇") {
          appController.ApplyFanControlSetting("max");
        } else {
          int rpm = manualFanRpmSlider == null ? ManualFanMinRpm : (int)Math.Round(manualFanRpmSlider.Value);
          appController.ApplyFanControlSetting($"{rpm} RPM");
        }

        appController.ApplyFanTableSetting(fanTableComboBox?.SelectedItem?.ToString() == "降温模式" ? "cool" : "silent");
        appController.ApplyTempSensitivitySetting(ConvertTempSensitivityBack(tempSensitivityComboBox?.SelectedItem?.ToString() ?? "高"));

        string cpuPowerSelection = cpuPowerComboBox?.SelectedItem?.ToString() ?? "最大";
        appController.ApplyCpuPowerSetting(cpuPowerSelection == "最大" ? "max" : cpuPowerSelection);
        appController.ApplyGpuPowerSetting(ConvertGpuPowerValueBack(gpuPowerComboBox?.SelectedItem?.ToString() ?? "节能"));
        appController.ApplyGraphicsModeSetting(ConvertGraphicsModeSettingBack(graphicsModeComboBox?.SelectedItem?.ToString() ?? "混合输出"));

        string gpuClockSelection = gpuClockComboBox?.SelectedItem?.ToString() ?? "还原";
        appController.ApplyGpuClockSetting(gpuClockSelection == "还原" ? 0 : int.Parse(gpuClockSelection.Replace(" MHz", string.Empty)));
        appController.ApplySmartPowerControlSetting(smartPowerControlCheckBox != null && smartPowerControlCheckBox.IsChecked == true);
      }

      bool floatingEnabled = floatingBarButton != null && floatingBarButton.Tag is bool tagValue && tagValue;
      appController.ApplyFloatingBarSetting(floatingEnabled);
      if (floatingBarLocationComboBox?.SelectedItem != null) {
        appController.ApplyFloatingBarLocationSetting(floatingBarLocationComboBox.SelectedItem.ToString() == "右上角" ? "right" : "left");
      }

      var tuning = BuildPowerTuningFromSliders();
      if (tuning != null) {
        appController.ApplyPowerControlTuning(tuning);
      }

      ClearPendingChanges();
      RefreshDashboard();
    }

    void DiscardChangesButton_Click(object sender, RoutedEventArgs e) {
      var snapshot = appController.GetDashboardSnapshot();
      SyncControlState(snapshot);
      ClearPendingChanges();
      RefreshDashboard();
    }

    void UsageModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
      if (syncingControlState || usageModeComboBox.SelectedItem == null) return;
      string mode = ConvertUsageModeBack(usageModeComboBox.SelectedItem.ToString());
      if (mode == "custom") return;
      ApplyUsageModeToControls(mode);
      MarkPendingChanges();
    }

    void FanModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
      if (syncingControlState || fanModeComboBox.SelectedItem == null) return;
      MarkPendingChanges(setCustomUsageMode: true);
    }

    void FanControlComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
      if (syncingControlState || fanControlComboBox.SelectedItem == null) return;
      string selected = fanControlComboBox.SelectedItem.ToString();
      if (selected == "自动") {
        SetManualFanSliderEnabled(false);
      } else if (selected == "最大风扇") {
        SetManualFanSliderEnabled(false);
      } else {
        SetManualFanSliderEnabled(true);
      }
      MarkPendingChanges(setCustomUsageMode: true);
    }

    void ManualFanRpmSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
      int rpm = (int)Math.Round(e.NewValue);
      UpdateManualFanRpmText(rpm);

      if (syncingControlState || fanControlComboBox?.SelectedItem == null) {
        return;
      }
      if (fanControlComboBox.SelectedItem.ToString() != "手动") {
        return;
      }
      MarkPendingChanges(setCustomUsageMode: true);
    }

    void FanTableComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
      if (syncingControlState || fanTableComboBox.SelectedItem == null) return;
      MarkPendingChanges(setCustomUsageMode: true);
    }

    void TempSensitivityComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
      if (syncingControlState || tempSensitivityComboBox.SelectedItem == null) return;
      MarkPendingChanges(setCustomUsageMode: true);
    }

    void CpuPowerComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
      if (syncingControlState || cpuPowerComboBox.SelectedItem == null) return;
      MarkPendingChanges(setCustomUsageMode: true);
    }

    void GpuPowerComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
      if (syncingControlState || gpuPowerComboBox.SelectedItem == null) return;
      MarkPendingChanges(setCustomUsageMode: true);
    }

    void GraphicsModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
      if (syncingControlState || graphicsModeComboBox.SelectedItem == null) return;
      MarkPendingChanges(setCustomUsageMode: true);
    }

    void GpuClockComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
      if (syncingControlState || gpuClockComboBox.SelectedItem == null) return;
      MarkPendingChanges(setCustomUsageMode: true);
    }

    void SmartPowerControlCheckBox_Changed(object sender, RoutedEventArgs e) {
      if (syncingControlState || smartPowerControlCheckBox == null || !smartPowerControlCheckBox.IsChecked.HasValue) return;
      MarkPendingChanges(setCustomUsageMode: true);
    }

    void FloatingBarButton_Click(object sender, RoutedEventArgs e) {
      if (syncingControlState) return;
      bool currentValue = floatingBarButton != null && floatingBarButton.Tag is bool tagValue && tagValue;
      bool nextValue = !currentValue;
      floatingBarButton.Tag = nextValue;
      floatingBarButton.Content = nextValue ? "浮窗: 开启" : "浮窗: 关闭";
      floatingBarButton.Background = nextValue
        ? new SolidColorBrush(Color.FromRgb(229, 247, 240))
        : subtleFill;
      floatingBarButton.Foreground = nextValue ? accentGreen : strongText;
      MarkPendingChanges();
    }

    void FloatingBarLocationComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
      if (syncingControlState || floatingBarLocationComboBox?.SelectedItem == null) {
        return;
      }

      MarkPendingChanges();
    }

    void StrategySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
      if (sender is Slider slider) {
        UpdateThresholdValueText(slider);
      }

      if (syncingControlState) {
        return;
      }
      MarkPendingChanges();
    }

    void UpdateThresholdValueText(Slider slider) {
      if (slider == null) {
        return;
      }

      string unit = slider.Tag as string ?? string.Empty;
      string text = $"{slider.Value:F0} {unit}";
      if (slider == cpuEmergencySlider && cpuEmergencyValueText != null) cpuEmergencyValueText.Text = text;
      else if (slider == gpuEmergencySlider && gpuEmergencyValueText != null) gpuEmergencyValueText.Text = text;
      else if (slider == cpuRecoverSlider && cpuRecoverValueText != null) cpuRecoverValueText.Text = text;
      else if (slider == gpuRecoverSlider && gpuRecoverValueText != null) gpuRecoverValueText.Text = text;
      else if (slider == cpuFanBoostOnSlider && cpuFanBoostOnValueText != null) cpuFanBoostOnValueText.Text = text;
      else if (slider == gpuFanBoostOnSlider && gpuFanBoostOnValueText != null) gpuFanBoostOnValueText.Text = text;
      else if (slider == cpuFanBoostOffSlider && cpuFanBoostOffValueText != null) cpuFanBoostOffValueText.Text = text;
      else if (slider == gpuFanBoostOffSlider && gpuFanBoostOffValueText != null) gpuFanBoostOffValueText.Text = text;
      else if (slider == batteryGuardTriggerSlider && batteryGuardTriggerValueText != null) batteryGuardTriggerValueText.Text = text;
      else if (slider == batteryGuardReleaseSlider && batteryGuardReleaseValueText != null) batteryGuardReleaseValueText.Text = text;
    }

    void SyncPowerTuningControls() {
      var tuning = appController.GetPowerControlTuningSnapshot();
      if (tuning == null) {
        return;
      }

      if (cpuEmergencySlider == null) {
        return;
      }

      cpuEmergencySlider.Value = tuning.CpuEmergencyTempC;
      gpuEmergencySlider.Value = tuning.GpuEmergencyTempC;
      cpuRecoverSlider.Value = tuning.CpuRecoverTempC;
      gpuRecoverSlider.Value = tuning.GpuRecoverTempC;
      cpuFanBoostOnSlider.Value = tuning.CpuFanBoostOnTempC;
      gpuFanBoostOnSlider.Value = tuning.GpuFanBoostOnTempC;
      cpuFanBoostOffSlider.Value = tuning.CpuFanBoostOffTempC;
      gpuFanBoostOffSlider.Value = tuning.GpuFanBoostOffTempC;
      batteryGuardTriggerSlider.Value = tuning.BatteryGuardTriggerWatts;
      batteryGuardReleaseSlider.Value = tuning.BatteryGuardReleaseWatts;

      UpdateThresholdValueText(cpuEmergencySlider);
      UpdateThresholdValueText(gpuEmergencySlider);
      UpdateThresholdValueText(cpuRecoverSlider);
      UpdateThresholdValueText(gpuRecoverSlider);
      UpdateThresholdValueText(cpuFanBoostOnSlider);
      UpdateThresholdValueText(gpuFanBoostOnSlider);
      UpdateThresholdValueText(cpuFanBoostOffSlider);
      UpdateThresholdValueText(gpuFanBoostOffSlider);
      UpdateThresholdValueText(batteryGuardTriggerSlider);
      UpdateThresholdValueText(batteryGuardReleaseSlider);
    }

    PowerControlTuning BuildPowerTuningFromSliders() {
      if (cpuEmergencySlider == null) {
        return null;
      }

      return new PowerControlTuning {
        CpuEmergencyTempC = (float)cpuEmergencySlider.Value,
        GpuEmergencyTempC = (float)gpuEmergencySlider.Value,
        CpuRecoverTempC = (float)cpuRecoverSlider.Value,
        GpuRecoverTempC = (float)gpuRecoverSlider.Value,
        CpuFanBoostOnTempC = (float)cpuFanBoostOnSlider.Value,
        GpuFanBoostOnTempC = (float)gpuFanBoostOnSlider.Value,
        CpuFanBoostOffTempC = (float)cpuFanBoostOffSlider.Value,
        GpuFanBoostOffTempC = (float)gpuFanBoostOffSlider.Value,
        BatteryGuardTriggerWatts = (float)batteryGuardTriggerSlider.Value,
        BatteryGuardReleaseWatts = (float)batteryGuardReleaseSlider.Value
      };
    }

    void ResetTuningButton_Click(object sender, RoutedEventArgs e) {
      var tuning = appController.GetDefaultPowerControlTuning();
      syncingControlState = true;
      try {
        if (tuning != null) {
          cpuEmergencySlider.Value = tuning.CpuEmergencyTempC;
          gpuEmergencySlider.Value = tuning.GpuEmergencyTempC;
          cpuRecoverSlider.Value = tuning.CpuRecoverTempC;
          gpuRecoverSlider.Value = tuning.GpuRecoverTempC;
          cpuFanBoostOnSlider.Value = tuning.CpuFanBoostOnTempC;
          gpuFanBoostOnSlider.Value = tuning.GpuFanBoostOnTempC;
          cpuFanBoostOffSlider.Value = tuning.CpuFanBoostOffTempC;
          gpuFanBoostOffSlider.Value = tuning.GpuFanBoostOffTempC;
          batteryGuardTriggerSlider.Value = tuning.BatteryGuardTriggerWatts;
          batteryGuardReleaseSlider.Value = tuning.BatteryGuardReleaseWatts;
          UpdateThresholdValueText(cpuEmergencySlider);
          UpdateThresholdValueText(gpuEmergencySlider);
          UpdateThresholdValueText(cpuRecoverSlider);
          UpdateThresholdValueText(gpuRecoverSlider);
          UpdateThresholdValueText(cpuFanBoostOnSlider);
          UpdateThresholdValueText(gpuFanBoostOnSlider);
          UpdateThresholdValueText(cpuFanBoostOffSlider);
          UpdateThresholdValueText(gpuFanBoostOffSlider);
          UpdateThresholdValueText(batteryGuardTriggerSlider);
          UpdateThresholdValueText(batteryGuardReleaseSlider);
        }
      } finally {
        syncingControlState = false;
      }
      MarkPendingChanges(setCustomUsageMode: true);
    }

    void SetManualFanSliderEnabled(bool enabled) {
      if (manualFanRpmSlider != null) {
        manualFanRpmSlider.IsEnabled = enabled;
      }
      if (manualFanRpmValueText != null) {
        manualFanRpmValueText.Foreground = enabled ? strongText : mutedText;
      }
    }

    void UpdateManualFanRpmText(int rpm) {
      if (manualFanRpmValueText != null) {
        manualFanRpmValueText.Text = $"{rpm} RPM";
      }
    }

    int ParseManualFanRpm(string fanControlValue) {
      if (string.IsNullOrWhiteSpace(fanControlValue) || !fanControlValue.EndsWith(" RPM")) {
        return ManualFanMinRpm;
      }

      string text = fanControlValue.Replace(" RPM", string.Empty).Trim();
      if (!int.TryParse(text, out int rpm)) {
        return ManualFanMinRpm;
      }

      rpm = Math.Max(ManualFanMinRpm, Math.Min(ManualFanMaxRpm, rpm));
      return rpm - (rpm % ManualFanStepRpm);
    }

  }
}
