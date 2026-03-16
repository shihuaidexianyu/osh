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
    static MainForm instance;
    static IAppController controller;
    public static bool IsVisibleOnScreen {
      get {
        return instance != null && instance.window != null && instance.window.IsVisible;
      }
    }
    internal static void Initialize(IAppController appController) {
      controller = appController;
    }
    public static MainForm Instance {
      get {
        if (instance == null) {
          if (controller == null) {
            throw new InvalidOperationException("MainForm controller is not initialized.");
          }
          instance = new MainForm(controller);
        }
        return instance;
      }
    }

    readonly SolidColorBrush pageBack = new SolidColorBrush(Color.FromRgb(245, 246, 248));
    readonly SolidColorBrush cardBack = new SolidColorBrush(Color.FromRgb(255, 255, 255));
    readonly SolidColorBrush borderColor = new SolidColorBrush(Color.FromRgb(229, 232, 238));
    readonly SolidColorBrush subtleFill = new SolidColorBrush(Color.FromRgb(248, 249, 251));
    readonly SolidColorBrush strongText = new SolidColorBrush(Color.FromRgb(29, 33, 40));
    readonly SolidColorBrush mutedText = new SolidColorBrush(Color.FromRgb(100, 108, 120));
    readonly SolidColorBrush softBlueFill = new SolidColorBrush(Color.FromRgb(242, 246, 251));
    readonly SolidColorBrush softGreenFill = new SolidColorBrush(Color.FromRgb(243, 247, 244));
    readonly SolidColorBrush softOrangeFill = new SolidColorBrush(Color.FromRgb(250, 246, 241));
    readonly SolidColorBrush softSlateFill = new SolidColorBrush(Color.FromRgb(244, 246, 248));
    readonly SolidColorBrush accentOrange = new SolidColorBrush(Color.FromRgb(189, 108, 0));
    readonly SolidColorBrush accentBlue = new SolidColorBrush(Color.FromRgb(0, 103, 192));
    readonly SolidColorBrush accentGreen = new SolidColorBrush(Color.FromRgb(11, 106, 69));
    const int ManualFanMinRpm = 1600;
    const int ManualFanMaxRpm = 6400;
    const int ManualFanStepRpm = 100;

    readonly string[] usageModeItems = { "安静", "均衡", "性能", "MAX", "自定义" };
    readonly string[] fanModeItems = { "均衡", "性能" };
    readonly string[] fanControlModeItems = { "自动", "最大风扇", "手动" };
    readonly string[] fanTableItems = { "安静模式", "降温模式" };
    readonly string[] tempSensitivityItems = { "高", "中", "低", "实时" };
    readonly string[] cpuPowerItems = { "最大", "45 W", "55 W", "65 W", "75 W", "90 W" };
    readonly string[] gpuPowerItems = { "高性能", "均衡", "节能" };
    readonly string[] gpuClockItems = { "还原", "1600 MHz", "1800 MHz", "2000 MHz", "2200 MHz", "2400 MHz" };
    readonly string[] floatingBarLocationItems = { "左上角", "右上角" };

    Window window;
    DispatcherTimer refreshTimer;
    bool syncingControlState;
    bool hasPendingChanges;
    readonly IAppController appController;

    TextBlock totalPowerText;
    TextBlock lastUpdateText;
    Button applyChangesButton;
    Button discardChangesButton;

    TextBlock leftCpuText;
    TextBlock leftGpuText;
    TextBlock leftBatteryText;
    TextBlock leftFanText;
    TextBlock leftModeText;

    TextBlock gfxModeText;
    TextBlock adapterText;
    TextBlock gpuControlText;
    TextBlock capabilitiesText;
    TextBlock keyboardText;
    TextBlock fanTypeText;
    TextBlock temperatureSensorSummaryText;
    Canvas temperatureTrendCanvas;
    Border smartStateBadge;
    TextBlock smartStateText;
    TextBlock smartReasonText;
    ProgressBar smartBudgetBar;
    TextBlock smartBudgetText;
    ProgressBar smartCpuTempBar;
    TextBlock smartCpuTempText;
    ProgressBar smartGpuTempBar;
    TextBlock smartGpuTempText;
    Slider cpuEmergencySlider;
    Slider gpuEmergencySlider;
    Slider cpuRecoverSlider;
    Slider gpuRecoverSlider;
    Slider cpuFanBoostOnSlider;
    Slider gpuFanBoostOnSlider;
    Slider cpuFanBoostOffSlider;
    Slider gpuFanBoostOffSlider;
    Slider batteryGuardTriggerSlider;
    Slider batteryGuardReleaseSlider;
    TextBlock cpuEmergencyValueText;
    TextBlock gpuEmergencyValueText;
    TextBlock cpuRecoverValueText;
    TextBlock gpuRecoverValueText;
    TextBlock cpuFanBoostOnValueText;
    TextBlock gpuFanBoostOnValueText;
    TextBlock cpuFanBoostOffValueText;
    TextBlock gpuFanBoostOffValueText;
    TextBlock batteryGuardTriggerValueText;
    TextBlock batteryGuardReleaseValueText;

    ComboBox usageModeComboBox;
    ComboBox fanModeComboBox;
    ComboBox fanControlComboBox;
    Slider manualFanRpmSlider;
    TextBlock manualFanRpmValueText;
    ComboBox fanTableComboBox;
    ComboBox tempSensitivityComboBox;
    ComboBox cpuPowerComboBox;
    ComboBox gpuPowerComboBox;
    ComboBox gpuClockComboBox;
    ComboBox floatingBarLocationComboBox;
    CheckBox smartPowerControlCheckBox;
    Button floatingBarButton;

    readonly List<float> cpuControlTempHistory = new List<float>();
    readonly List<float> gpuControlTempHistory = new List<float>();
    readonly List<float> hottestTempHistory = new List<float>();
    readonly List<float> cpuWallTempHistory = new List<float>();
    readonly List<float> gpuWallTempHistory = new List<float>();
    readonly List<float> cpuLimitHistory = new List<float>();
    const int TemperatureTrendCapacity = 240;

    MainForm(IAppController appController) {
      this.appController = appController;
      EnsureWindow();
    }

    public WinForms.FormWindowState WindowState {
      get {
        EnsureWindow();
        switch (window.WindowState) {
          case System.Windows.WindowState.Maximized:
            return WinForms.FormWindowState.Maximized;
          case System.Windows.WindowState.Minimized:
            return WinForms.FormWindowState.Minimized;
          default:
            return WinForms.FormWindowState.Normal;
        }
      }
      set {
        EnsureWindow();
        switch (value) {
          case WinForms.FormWindowState.Maximized:
            window.WindowState = System.Windows.WindowState.Maximized;
            break;
          case WinForms.FormWindowState.Minimized:
            window.WindowState = System.Windows.WindowState.Minimized;
            break;
          default:
            window.WindowState = System.Windows.WindowState.Normal;
            break;
        }
      }
    }

    public void Show() {
      EnsureWindow();
      if (!window.IsVisible) {
        window.Show();
      }
      window.Visibility = Visibility.Visible;
    }

    public void BringToFront() {
      EnsureWindow();
      window.Topmost = true;
      window.Topmost = false;
      window.Focus();
    }

    public void Activate() {
      EnsureWindow();
      window.Activate();
    }

    public void ShowHelpSection() {
      EnsureWindow();
      if (!window.IsVisible) {
        window.Show();
      }
      window.Visibility = Visibility.Visible;
      window.Activate();
    }

  }
}
