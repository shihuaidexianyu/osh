using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using WinForms = System.Windows.Forms;
using static OmenSuperHub.OmenHardware;

namespace OmenSuperHub {
  public sealed class MainForm {
    static MainForm instance;
    public static MainForm Instance {
      get {
        if (instance == null) {
          instance = new MainForm();
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
    readonly SolidColorBrush accentOrange = new SolidColorBrush(Color.FromRgb(189, 108, 0));
    readonly SolidColorBrush accentBlue = new SolidColorBrush(Color.FromRgb(0, 103, 192));
    readonly SolidColorBrush accentGreen = new SolidColorBrush(Color.FromRgb(11, 106, 69));
    const int ManualFanMinRpm = 1600;
    const int ManualFanMaxRpm = 6400;
    const int ManualFanStepRpm = 100;

    readonly string[] fanModeItems = { "平衡", "狂暴" };
    readonly string[] fanControlModeItems = { "自动", "最大风扇", "手动" };
    readonly string[] fanTableItems = { "安静模式", "降温模式" };
    readonly string[] tempSensitivityItems = { "高", "中", "低", "实时" };
    readonly string[] cpuPowerItems = { "最大", "45 W", "55 W", "65 W", "75 W", "90 W" };
    readonly string[] gpuPowerItems = { "CTGP开+DB开", "CTGP开+DB关", "CTGP关+DB关" };
    readonly string[] gpuClockItems = { "还原", "1600 MHz", "1800 MHz", "2000 MHz", "2200 MHz", "2400 MHz" };

    Window window;
    DispatcherTimer refreshTimer;
    DispatcherTimer tuningApplyTimer;
    bool syncingControlState;

    TextBlock totalPowerText;
    TextBlock lastUpdateText;

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
    Border smartStateBadge;
    TextBlock smartStateText;
    TextBlock smartReasonText;
    ProgressBar smartBudgetBar;
    TextBlock smartBudgetText;
    ProgressBar smartCpuTempBar;
    TextBlock smartCpuTempText;
    ProgressBar smartGpuTempBar;
    TextBlock smartGpuTempText;
    TextBlock smartActionText;
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

    ComboBox fanModeComboBox;
    ComboBox fanControlComboBox;
    Slider manualFanRpmSlider;
    TextBlock manualFanRpmValueText;
    ComboBox fanTableComboBox;
    ComboBox tempSensitivityComboBox;
    ComboBox cpuPowerComboBox;
    ComboBox gpuPowerComboBox;
    ComboBox gpuClockComboBox;
    CheckBox smartPowerControlCheckBox;
    Button floatingBarButton;

    TabControl detailsTabControl;
    TextBox telemetryTextBox;
    TextBox configTextBox;
    TextBox helpTextBox;
    int lastAppliedManualFanRpm = -1;

    MainForm() {
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
      if (detailsTabControl != null && detailsTabControl.Items.Count >= 3) {
        detailsTabControl.SelectedIndex = 2;
      }
      window.Visibility = Visibility.Visible;
      window.Activate();
    }

    void EnsureWindow() {
      if (window != null) {
        return;
      }

      if (System.Windows.Application.Current == null) {
        new System.Windows.Application {
          ShutdownMode = ShutdownMode.OnExplicitShutdown
        };
      }

      window = new Window {
        Title = "OmenSuperHub",
        Width = 1260,
        Height = 860,
        MinWidth = 1080,
        MinHeight = 760,
        WindowStartupLocation = WindowStartupLocation.CenterScreen,
        Background = pageBack,
        FontFamily = new FontFamily("Segoe UI"),
        FontSize = 14
      };

      BuildLayout();

      window.Closing += (s, e) => {
        e.Cancel = true;
        window.Hide();
      };
      window.IsVisibleChanged += (s, e) => {
        if (refreshTimer != null) {
          refreshTimer.IsEnabled = window.IsVisible;
        }
      };

      refreshTimer = new DispatcherTimer {
        Interval = TimeSpan.FromMilliseconds(1200)
      };
      refreshTimer.Tick += (s, e) => RefreshDashboard();
      refreshTimer.Start();

      tuningApplyTimer = new DispatcherTimer {
        Interval = TimeSpan.FromMilliseconds(350)
      };
      tuningApplyTimer.Tick += (s, e) => {
        tuningApplyTimer.Stop();
        ApplyPowerTuningFromSliders();
      };
    }

    void BuildLayout() {
      var root = new Grid {
        Margin = new Thickness(22, 18, 22, 18),
        Background = pageBack
      };
      root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
      root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

      var leftScroll = new ScrollViewer {
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        Margin = new Thickness(0, 0, 16, 0),
        Background = Brushes.Transparent
      };
      var leftStack = new StackPanel();
      leftScroll.Content = leftStack;

      leftStack.Children.Add(CreateSidebarBrandCard());
      leftStack.Children.Add(CreateSidebarSummaryCard());
      leftStack.Children.Add(CreateSidebarNavCard());

      var rightScroll = new ScrollViewer {
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        Background = Brushes.Transparent
      };
      var rightStack = new StackPanel();
      rightScroll.Content = rightStack;

      rightStack.Children.Add(BuildHeaderPanel());
      rightStack.Children.Add(BuildCoolingPanel());
      rightStack.Children.Add(BuildPerformancePanel());
      rightStack.Children.Add(BuildSmartPowerPanel());
      rightStack.Children.Add(BuildStrategyTuningPanel());
      rightStack.Children.Add(BuildOverlayPanel());
      rightStack.Children.Add(BuildStatusPanel());
      rightStack.Children.Add(BuildDetailsPanel());

      Grid.SetColumn(leftScroll, 0);
      Grid.SetColumn(rightScroll, 1);
      root.Children.Add(leftScroll);
      root.Children.Add(rightScroll);
      window.Content = root;
    }

    Border CreateCard(double minHeight) {
      return new Border {
        Background = cardBack,
        BorderBrush = borderColor,
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(10),
        Padding = new Thickness(20),
        Margin = new Thickness(0, 0, 0, 14),
        MinHeight = minHeight
      };
    }

    TextBlock CreateSectionTitle(string text) {
      return new TextBlock {
        Text = text,
        Foreground = strongText,
        FontSize = 20,
        FontWeight = FontWeights.SemiBold,
        Margin = new Thickness(0, 0, 0, 4)
      };
    }

    TextBlock CreateSectionSubtitle(string text) {
      return new TextBlock {
        Text = text,
        Foreground = mutedText,
        FontSize = 13,
        Margin = new Thickness(0, 0, 0, 12)
      };
    }

    TextBlock CreateValueLabel(string text) {
      return new TextBlock {
        Text = text,
        Foreground = strongText,
        FontSize = 15,
        FontWeight = FontWeights.SemiBold,
        Margin = new Thickness(0, 0, 0, 8),
        TextWrapping = TextWrapping.Wrap
      };
    }

    Border CreateSidebarBrandCard() {
      var card = CreateCard(96);
      var stack = new StackPanel();
      stack.Children.Add(new TextBlock {
        Text = "OmenSuperHub",
        Foreground = strongText,
        FontSize = 28,
        FontWeight = FontWeights.Bold
      });
      stack.Children.Add(new TextBlock {
        Text = "设备控制与状态",
        Foreground = mutedText,
        FontSize = 14
      });
      card.Child = stack;
      return card;
    }

    Border CreateSidebarSummaryCard() {
      var card = CreateCard(240);
      var stack = new StackPanel();
      stack.Children.Add(CreateSectionTitle("当前状态"));
      stack.Children.Add(CreateSectionSubtitle("核心温度、功率和风扇运行状态"));

      leftCpuText = CreateValueLabel("CPU --");
      leftGpuText = CreateValueLabel("GPU --");
      leftBatteryText = CreateValueLabel("Battery --");
      leftFanText = CreateValueLabel("Fan --");
      leftModeText = CreateValueLabel("Mode --");

      stack.Children.Add(leftCpuText);
      stack.Children.Add(leftGpuText);
      stack.Children.Add(leftBatteryText);
      stack.Children.Add(leftFanText);
      stack.Children.Add(leftModeText);
      card.Child = stack;
      return card;
    }

    Border CreateSidebarNavCard() {
      var card = CreateCard(210);
      var stack = new StackPanel();
      stack.Children.Add(CreateSectionTitle("设置分类"));
      stack.Children.Add(CreateSectionSubtitle("主页面按功能分组布局"));
      stack.Children.Add(CreateNavTag("散热与风扇"));
      stack.Children.Add(CreateNavTag("功耗与性能"));
      stack.Children.Add(CreateNavTag("智能功耗"));
      stack.Children.Add(CreateNavTag("策略参数"));
      stack.Children.Add(CreateNavTag("浮窗与显示"));
      stack.Children.Add(CreateNavTag("硬件状态"));
      stack.Children.Add(CreateNavTag("实时详情"));
      card.Child = stack;
      return card;
    }

    TextBlock CreateNavTag(string text) {
      return new TextBlock {
        Text = text,
        Foreground = mutedText,
        FontSize = 14,
        Margin = new Thickness(0, 0, 0, 8)
      };
    }

    Border BuildHeaderPanel() {
      var card = CreateCard(130);

      var root = new Grid();
      root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
      root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

      var left = new StackPanel();
      left.Children.Add(new TextBlock {
        Text = "设置",
        Foreground = strongText,
        FontSize = 36,
        FontWeight = FontWeights.Bold
      });
      totalPowerText = new TextBlock {
        Text = "-- W",
        Foreground = accentOrange,
        FontSize = 44,
        FontWeight = FontWeights.Bold
      };
      lastUpdateText = new TextBlock {
        Text = "最近刷新: --",
        Foreground = mutedText,
        FontSize = 13
      };
      left.Children.Add(totalPowerText);
      left.Children.Add(lastUpdateText);

      var actions = new StackPanel {
        Orientation = Orientation.Horizontal,
        VerticalAlignment = VerticalAlignment.Top
      };
      actions.Children.Add(CreateActionButton("立即刷新", (s, e) => RefreshDashboard()));
      actions.Children.Add(CreateActionButton("帮助", (s, e) => ShowHelpSection()));
      actions.Children.Add(CreateActionButton("隐藏到托盘", (s, e) => window.Hide()));

      Grid.SetColumn(left, 0);
      Grid.SetColumn(actions, 1);
      root.Children.Add(left);
      root.Children.Add(actions);

      card.Child = root;
      return card;
    }

    Button CreateActionButton(string text, RoutedEventHandler click) {
      var button = new Button {
        Content = text,
        Margin = new Thickness(0, 0, 10, 0),
        Padding = new Thickness(14, 8, 14, 8),
        FontSize = 14,
        FontWeight = FontWeights.SemiBold,
        Foreground = strongText,
        Background = Brushes.White,
        BorderBrush = borderColor
      };
      button.Click += click;
      return button;
    }

    Border BuildCoolingPanel() {
      var card = CreateCard(270);
      var grid = CreateSettingsGrid();
      AddTitleToGrid(grid, "散热与风扇", "调节风扇模式、转速曲线和温度响应速度。");

      fanModeComboBox = CreateComboBox(fanModeItems, FanModeComboBox_SelectionChanged);
      fanControlComboBox = CreateComboBox(fanControlModeItems, FanControlComboBox_SelectionChanged);
      var manualFanControl = CreateManualFanRpmControl();
      fanTableComboBox = CreateComboBox(fanTableItems, FanTableComboBox_SelectionChanged);
      tempSensitivityComboBox = CreateComboBox(tempSensitivityItems, TempSensitivityComboBox_SelectionChanged);

      AddControlRow(grid, 1, "模式", fanModeComboBox);
      AddControlRow(grid, 2, "控制", fanControlComboBox);
      AddControlRow(grid, 3, "手动转速", manualFanControl);
      AddControlRow(grid, 4, "曲线", fanTableComboBox);
      AddControlRow(grid, 5, "温度响应", tempSensitivityComboBox);
      card.Child = grid;
      return card;
    }

    Border BuildPerformancePanel() {
      var card = CreateCard(220);
      var grid = CreateSettingsGrid();
      AddTitleToGrid(grid, "功耗与性能", "设定 CPU/GPU 功耗策略与显卡锁频上限。");

      cpuPowerComboBox = CreateComboBox(cpuPowerItems, CpuPowerComboBox_SelectionChanged);
      gpuPowerComboBox = CreateComboBox(gpuPowerItems, GpuPowerComboBox_SelectionChanged);
      gpuClockComboBox = CreateComboBox(gpuClockItems, GpuClockComboBox_SelectionChanged);
      smartPowerControlCheckBox = new CheckBox {
        Content = "启用智能功耗控制（Eco / Balanced / Performance / Protect）",
        Foreground = strongText,
        FontSize = 14,
        Margin = new Thickness(0, 6, 0, 8),
        VerticalAlignment = VerticalAlignment.Center
      };
      smartPowerControlCheckBox.Checked += SmartPowerControlCheckBox_Changed;
      smartPowerControlCheckBox.Unchecked += SmartPowerControlCheckBox_Changed;

      AddControlRow(grid, 1, "CPU 功率", cpuPowerComboBox);
      AddControlRow(grid, 2, "GPU 策略", gpuPowerComboBox);
      AddControlRow(grid, 3, "GPU 锁频", gpuClockComboBox);
      AddControlRow(grid, 4, "智能控制", smartPowerControlCheckBox);
      card.Child = grid;
      return card;
    }

    Border BuildSmartPowerPanel() {
      var card = CreateCard(250);

      var root = new Grid();
      root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
      root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
      root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
      root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
      root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

      var titleWrap = new StackPanel();
      titleWrap.Children.Add(CreateSectionTitle("智能功耗"));
      titleWrap.Children.Add(CreateSectionSubtitle("实时状态、预算占用和温度保护可视化。"));
      Grid.SetRow(titleWrap, 0);
      root.Children.Add(titleWrap);

      var stateRow = new Grid {
        Margin = new Thickness(0, 0, 0, 10)
      };
      stateRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
      stateRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

      smartStateText = new TextBlock {
        Text = "BALANCED",
        Foreground = Brushes.White,
        FontSize = 12,
        FontWeight = FontWeights.Bold
      };
      smartStateBadge = new Border {
        Background = accentBlue,
        CornerRadius = new CornerRadius(999),
        Padding = new Thickness(12, 4, 12, 4),
        Child = smartStateText,
        Margin = new Thickness(0, 0, 12, 0),
        VerticalAlignment = VerticalAlignment.Center
      };
      smartReasonText = new TextBlock {
        Text = "--",
        Foreground = mutedText,
        FontSize = 13,
        VerticalAlignment = VerticalAlignment.Center,
        TextTrimming = TextTrimming.CharacterEllipsis
      };

      Grid.SetColumn(smartStateBadge, 0);
      Grid.SetColumn(smartReasonText, 1);
      stateRow.Children.Add(smartStateBadge);
      stateRow.Children.Add(smartReasonText);
      Grid.SetRow(stateRow, 1);
      root.Children.Add(stateRow);

      var budgetWrap = new StackPanel {
        Margin = new Thickness(0, 0, 0, 10)
      };
      smartBudgetText = new TextBlock {
        Text = "预算 -- / -- W",
        Foreground = strongText,
        FontSize = 13,
        FontWeight = FontWeights.SemiBold,
        Margin = new Thickness(0, 0, 0, 4)
      };
      smartBudgetBar = new ProgressBar {
        Height = 10,
        Minimum = 0,
        Maximum = 100,
        Value = 0,
        Foreground = accentGreen,
        Background = subtleFill
      };
      budgetWrap.Children.Add(smartBudgetText);
      budgetWrap.Children.Add(smartBudgetBar);
      Grid.SetRow(budgetWrap, 2);
      root.Children.Add(budgetWrap);

      var thermalGrid = new Grid {
        Margin = new Thickness(0, 0, 0, 10)
      };
      thermalGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
      thermalGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

      var cpuThermal = CreateThermalMetric("CPU 温度", out smartCpuTempText, out smartCpuTempBar);
      var gpuThermal = CreateThermalMetric("GPU 温度", out smartGpuTempText, out smartGpuTempBar);
      Grid.SetColumn(cpuThermal, 0);
      Grid.SetColumn(gpuThermal, 1);
      thermalGrid.Children.Add(cpuThermal);
      thermalGrid.Children.Add(gpuThermal);
      Grid.SetRow(thermalGrid, 3);
      root.Children.Add(thermalGrid);

      smartActionText = new TextBlock {
        Text = "CPU -- | GPU -- | FanBoost --",
        Foreground = strongText,
        FontSize = 13,
        TextWrapping = TextWrapping.Wrap
      };
      Grid.SetRow(smartActionText, 4);
      root.Children.Add(smartActionText);

      card.Child = root;
      return card;
    }

    Border BuildStrategyTuningPanel() {
      var card = CreateCard(360);
      var root = new Grid();
      root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
      root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
      root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

      var titleWrap = new StackPanel();
      titleWrap.Children.Add(CreateSectionTitle("策略参数"));
      titleWrap.Children.Add(CreateSectionSubtitle("调节温度与电池守卫阈值（滑块实时生效）。"));
      Grid.SetRow(titleWrap, 0);
      root.Children.Add(titleWrap);

      var slidersWrap = new StackPanel {
        Margin = new Thickness(0, 4, 0, 4)
      };
      slidersWrap.Children.Add(CreateThresholdSliderRow("CPU 保护温度", 85, 102, 1, out cpuEmergencySlider, out cpuEmergencyValueText, "°C"));
      slidersWrap.Children.Add(CreateThresholdSliderRow("GPU 保护温度", 78, 95, 1, out gpuEmergencySlider, out gpuEmergencyValueText, "°C"));
      slidersWrap.Children.Add(CreateThresholdSliderRow("CPU 恢复温度", 70, 99, 1, out cpuRecoverSlider, out cpuRecoverValueText, "°C"));
      slidersWrap.Children.Add(CreateThresholdSliderRow("GPU 恢复温度", 65, 92, 1, out gpuRecoverSlider, out gpuRecoverValueText, "°C"));
      slidersWrap.Children.Add(CreateThresholdSliderRow("CPU 风扇增强开", 75, 100, 1, out cpuFanBoostOnSlider, out cpuFanBoostOnValueText, "°C"));
      slidersWrap.Children.Add(CreateThresholdSliderRow("GPU 风扇增强开", 70, 95, 1, out gpuFanBoostOnSlider, out gpuFanBoostOnValueText, "°C"));
      slidersWrap.Children.Add(CreateThresholdSliderRow("CPU 风扇增强关", 65, 98, 1, out cpuFanBoostOffSlider, out cpuFanBoostOffValueText, "°C"));
      slidersWrap.Children.Add(CreateThresholdSliderRow("GPU 风扇增强关", 60, 90, 1, out gpuFanBoostOffSlider, out gpuFanBoostOffValueText, "°C"));
      slidersWrap.Children.Add(CreateThresholdSliderRow("电池守卫触发", 30, 100, 1, out batteryGuardTriggerSlider, out batteryGuardTriggerValueText, "W"));
      slidersWrap.Children.Add(CreateThresholdSliderRow("电池守卫释放", 20, 90, 1, out batteryGuardReleaseSlider, out batteryGuardReleaseValueText, "W"));

      Grid.SetRow(slidersWrap, 1);
      root.Children.Add(slidersWrap);

      var actions = new StackPanel {
        Orientation = Orientation.Horizontal,
        HorizontalAlignment = HorizontalAlignment.Left,
        Margin = new Thickness(0, 8, 0, 0)
      };
      var resetButton = new Button {
        Content = "恢复默认",
        Padding = new Thickness(12, 6, 12, 6),
        FontSize = 13,
        FontWeight = FontWeights.SemiBold,
        Foreground = strongText,
        Background = subtleFill,
        BorderBrush = borderColor
      };
      resetButton.Click += ResetTuningButton_Click;
      actions.Children.Add(resetButton);

      Grid.SetRow(actions, 2);
      root.Children.Add(actions);

      card.Child = root;
      return card;
    }

    Border BuildOverlayPanel() {
      var card = CreateCard(110);
      var grid = CreateSettingsGrid();
      AddTitleToGrid(grid, "浮窗与显示", "控制桌面浮窗显示状态。");

      floatingBarButton = new Button {
        Content = "浮窗: 关闭",
        Padding = new Thickness(10, 6, 10, 6),
        FontSize = 14,
        FontWeight = FontWeights.SemiBold,
        Foreground = strongText,
        Background = subtleFill,
        BorderBrush = borderColor,
        HorizontalAlignment = HorizontalAlignment.Left,
        MinWidth = 160
      };
      floatingBarButton.Click += FloatingBarButton_Click;

      AddControlRow(grid, 1, "浮窗", floatingBarButton);
      card.Child = grid;
      return card;
    }

    Border BuildStatusPanel() {
      var card = CreateCard(230);
      var grid = CreateSettingsGrid();
      AddTitleToGrid(grid, "硬件状态", "读取当前硬件能力与电源/显卡状态。");

      AddValueRow(grid, 1, "显卡模式", out gfxModeText);
      AddValueRow(grid, 2, "供电/适配器", out adapterText);
      AddValueRow(grid, 3, "GPU 控制", out gpuControlText);
      AddValueRow(grid, 4, "能力", out capabilitiesText);
      AddValueRow(grid, 5, "键盘", out keyboardText);
      AddValueRow(grid, 6, "风扇类型", out fanTypeText);
      card.Child = grid;
      return card;
    }

    Border BuildDetailsPanel() {
      var card = CreateCard(320);
      var root = new Grid();
      root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
      root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

      var titleWrap = new StackPanel();
      titleWrap.Children.Add(CreateSectionTitle("实时详情"));
      titleWrap.Children.Add(CreateSectionSubtitle("遥测和当前运行配置快照。"));
      root.Children.Add(titleWrap);

      detailsTabControl = new TabControl {
        Margin = new Thickness(0, 6, 0, 0)
      };
      var telemetryTab = new TabItem { Header = "实时遥测" };
      var configTab = new TabItem { Header = "运行配置" };
      var helpTab = new TabItem { Header = "帮助" };

      telemetryTextBox = new TextBox {
        FontFamily = new FontFamily("Consolas"),
        FontSize = 13,
        IsReadOnly = true,
        BorderThickness = new Thickness(0),
        Background = Brushes.White,
        Foreground = strongText,
        AcceptsReturn = true,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        TextWrapping = TextWrapping.NoWrap
      };
      configTextBox = new TextBox {
        FontFamily = new FontFamily("Consolas"),
        FontSize = 13,
        IsReadOnly = true,
        BorderThickness = new Thickness(0),
        Background = Brushes.White,
        Foreground = strongText,
        AcceptsReturn = true,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        TextWrapping = TextWrapping.NoWrap
      };
      helpTextBox = new TextBox {
        FontFamily = new FontFamily("Segoe UI"),
        FontSize = 14,
        IsReadOnly = true,
        BorderThickness = new Thickness(0),
        Background = Brushes.White,
        Foreground = strongText,
        AcceptsReturn = true,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        TextWrapping = TextWrapping.Wrap
      };

      telemetryTab.Content = telemetryTextBox;
      configTab.Content = configTextBox;
      helpTab.Content = helpTextBox;
      detailsTabControl.Items.Add(telemetryTab);
      detailsTabControl.Items.Add(configTab);
      detailsTabControl.Items.Add(helpTab);

      Grid.SetRow(detailsTabControl, 1);
      root.Children.Add(detailsTabControl);
      card.Child = root;
      return card;
    }

    Grid CreateSettingsGrid() {
      var grid = new Grid();
      grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
      grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
      return grid;
    }

    FrameworkElement CreateThermalMetric(string title, out TextBlock valueText, out ProgressBar progressBar) {
      var wrap = new StackPanel {
        Margin = new Thickness(0, 0, 10, 0)
      };

      var titleText = new TextBlock {
        Text = title,
        Foreground = mutedText,
        FontSize = 12
      };
      valueText = new TextBlock {
        Text = "--",
        Foreground = strongText,
        FontSize = 13,
        FontWeight = FontWeights.SemiBold,
        Margin = new Thickness(0, 2, 0, 4)
      };
      progressBar = new ProgressBar {
        Height = 8,
        Minimum = 0,
        Maximum = 100,
        Value = 0,
        Foreground = accentBlue,
        Background = subtleFill
      };

      wrap.Children.Add(titleText);
      wrap.Children.Add(valueText);
      wrap.Children.Add(progressBar);
      return wrap;
    }

    FrameworkElement CreateThresholdSliderRow(string title, double min, double max, double tick, out Slider slider, out TextBlock valueText, string unit) {
      var grid = new Grid {
        Margin = new Thickness(0, 0, 0, 8)
      };
      grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
      grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
      grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

      var label = new TextBlock {
        Text = title,
        Foreground = mutedText,
        FontSize = 13,
        VerticalAlignment = VerticalAlignment.Center
      };

      slider = new Slider {
        Minimum = min,
        Maximum = max,
        TickFrequency = tick,
        SmallChange = tick,
        LargeChange = tick * 3,
        IsSnapToTickEnabled = true,
        Value = min,
        Margin = new Thickness(8, 0, 10, 0)
      };
      slider.Tag = unit;
      slider.ValueChanged += StrategySlider_ValueChanged;

      valueText = new TextBlock {
        Text = $"{min:F0} {unit}",
        Foreground = strongText,
        FontSize = 13,
        FontWeight = FontWeights.SemiBold,
        Width = 68,
        TextAlignment = TextAlignment.Right,
        VerticalAlignment = VerticalAlignment.Center
      };

      Grid.SetColumn(label, 0);
      Grid.SetColumn(slider, 1);
      Grid.SetColumn(valueText, 2);
      grid.Children.Add(label);
      grid.Children.Add(slider);
      grid.Children.Add(valueText);
      return grid;
    }

    void AddTitleToGrid(Grid grid, string title, string subtitle) {
      grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
      var wrap = new StackPanel();
      wrap.Children.Add(CreateSectionTitle(title));
      wrap.Children.Add(CreateSectionSubtitle(subtitle));
      Grid.SetRow(wrap, 0);
      Grid.SetColumn(wrap, 0);
      Grid.SetColumnSpan(wrap, 2);
      grid.Children.Add(wrap);
    }

    void AddControlRow(Grid grid, int rowIndex, string title, FrameworkElement control) {
      while (grid.RowDefinitions.Count <= rowIndex) {
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
      }

      var label = new TextBlock {
        Text = title,
        Foreground = mutedText,
        FontSize = 13,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(0, 6, 8, 6)
      };

      if (control is FrameworkElement frameworkElement) {
        frameworkElement.Margin = new Thickness(0, 2, 0, 8);
      }

      Grid.SetRow(label, rowIndex);
      Grid.SetColumn(label, 0);
      Grid.SetRow(control, rowIndex);
      Grid.SetColumn(control, 1);

      grid.Children.Add(label);
      grid.Children.Add(control);
    }

    void AddValueRow(Grid grid, int rowIndex, string title, out TextBlock valueText) {
      while (grid.RowDefinitions.Count <= rowIndex) {
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
      }

      var label = new TextBlock {
        Text = title,
        Foreground = mutedText,
        FontSize = 13,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(0, 6, 8, 6)
      };
      valueText = new TextBlock {
        Text = "--",
        Foreground = strongText,
        FontSize = 14,
        FontWeight = FontWeights.SemiBold,
        Margin = new Thickness(0, 6, 0, 6),
        TextWrapping = TextWrapping.Wrap
      };

      Grid.SetRow(label, rowIndex);
      Grid.SetColumn(label, 0);
      Grid.SetRow(valueText, rowIndex);
      Grid.SetColumn(valueText, 1);
      grid.Children.Add(label);
      grid.Children.Add(valueText);
    }

    ComboBox CreateComboBox(IEnumerable<string> items, SelectionChangedEventHandler handler) {
      var comboBox = new ComboBox {
        MinWidth = 260,
        Height = 34,
        FontSize = 14,
        Padding = new Thickness(6, 3, 6, 3),
        Background = subtleFill,
        BorderBrush = borderColor,
        Foreground = strongText
      };
      foreach (var item in items) {
        comboBox.Items.Add(item);
      }
      comboBox.SelectionChanged += handler;
      return comboBox;
    }

    FrameworkElement CreateManualFanRpmControl() {
      var wrap = new Grid();
      wrap.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
      wrap.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

      manualFanRpmSlider = new Slider {
        Minimum = ManualFanMinRpm,
        Maximum = ManualFanMaxRpm,
        TickFrequency = ManualFanStepRpm,
        SmallChange = ManualFanStepRpm,
        LargeChange = ManualFanStepRpm * 4,
        IsSnapToTickEnabled = true,
        Value = ManualFanMinRpm,
        Margin = new Thickness(0, 8, 12, 8),
        IsEnabled = false
      };
      manualFanRpmSlider.ValueChanged += ManualFanRpmSlider_ValueChanged;

      manualFanRpmValueText = new TextBlock {
        Text = $"{ManualFanMinRpm} RPM",
        Foreground = strongText,
        FontSize = 13,
        FontWeight = FontWeights.SemiBold,
        Width = 86,
        VerticalAlignment = VerticalAlignment.Center,
        TextAlignment = TextAlignment.Right
      };

      Grid.SetColumn(manualFanRpmSlider, 0);
      Grid.SetColumn(manualFanRpmValueText, 1);
      wrap.Children.Add(manualFanRpmSlider);
      wrap.Children.Add(manualFanRpmValueText);
      return wrap;
    }

    void RefreshDashboard() {
      if (window == null || !window.IsVisible) {
        return;
      }

      var snapshot = Program.GetDashboardSnapshot();

      float totalPower = snapshot.CpuPowerWatts + snapshot.GpuPowerWatts;
      if (!snapshot.AcOnline) {
        float? batteryDischarge = GetBatteryDischargePowerWatts(snapshot.Battery);
        if (batteryDischarge.HasValue)
          totalPower = batteryDischarge.Value;
      }
      totalPowerText.Text = $"{totalPower:F1} W";
      lastUpdateText.Text = $"最近刷新: {DateTime.Now:HH:mm:ss}";

      leftCpuText.Text = $"CPU {snapshot.CpuTemperature:F1}°C / {snapshot.CpuPowerWatts:F1}W";
      leftGpuText.Text = snapshot.MonitorGpu ? $"GPU {snapshot.GpuTemperature:F1}°C / {snapshot.GpuPowerWatts:F1}W" : "GPU disabled";
      leftBatteryText.Text = BuildBatterySummary(snapshot);
      leftFanText.Text = $"Fan {FormatFanRpm(snapshot.FanSpeeds)}";
      leftModeText.Text = $"Mode {(snapshot.FanMode == "performance" ? "狂暴" : "平衡")} / {ConvertFanControlValue(snapshot.FanControl)}";

      gfxModeText.Text = FormatGfxMode(snapshot.GraphicsMode);
      adapterText.Text = $"{FormatAdapterStatus(snapshot.SmartAdapterStatus)} / {(snapshot.AcOnline ? "AC" : "Battery")}";
      gpuControlText.Text = FormatGpuControl(snapshot.GpuStatus);
      capabilitiesText.Text = BuildCapabilitiesSummary(snapshot);
      keyboardText.Text = FormatKeyboardType(snapshot.KeyboardType);
      fanTypeText.Text = snapshot.FanTypeInfo == null ? "Unknown" : $"{snapshot.FanTypeInfo.Fan1Type}/{snapshot.FanTypeInfo.Fan2Type}";
      UpdateSmartPowerVisual(snapshot);

      telemetryTextBox.Text = BuildTelemetryText(snapshot);
      configTextBox.Text = BuildConfigText(snapshot);
      helpTextBox.Text = BuildHelpText();

      SyncControlState(snapshot);
    }

    void SyncControlState(DashboardSnapshot snapshot) {
      syncingControlState = true;
      try {
        SelectComboItem(fanModeComboBox, snapshot.FanMode == "performance" ? "狂暴" : "平衡");
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
        SelectComboItem(gpuClockComboBox, snapshot.GpuClockLimit > 0 ? $"{snapshot.GpuClockLimit} MHz" : "还原");
        smartPowerControlCheckBox.IsChecked = snapshot.SmartPowerControlEnabled;
        SyncPowerTuningControls();

        bool overlayEnabled = snapshot.FloatingBarEnabled;
      floatingBarButton.Content = overlayEnabled ? "浮窗: 开启" : "浮窗: 关闭";
      floatingBarButton.Background = overlayEnabled
        ? new SolidColorBrush(Color.FromRgb(229, 247, 240))
        : subtleFill;
      floatingBarButton.Foreground = overlayEnabled ? accentGreen : strongText;
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

    void FanModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
      if (syncingControlState || fanModeComboBox.SelectedItem == null) return;
      Program.ApplyFanModeSetting(fanModeComboBox.SelectedItem.ToString() == "狂暴" ? "performance" : "default");
      RefreshDashboard();
    }

    void FanControlComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
      if (syncingControlState || fanControlComboBox.SelectedItem == null) return;
      string selected = fanControlComboBox.SelectedItem.ToString();
      if (selected == "自动") {
        SetManualFanSliderEnabled(false);
        Program.ApplyFanControlSetting("auto");
      } else if (selected == "最大风扇") {
        SetManualFanSliderEnabled(false);
        Program.ApplyFanControlSetting("max");
      } else {
        SetManualFanSliderEnabled(true);
        ApplyManualFanFromSlider(force: true);
      }
      RefreshDashboard();
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

      ApplyManualFanFromSlider();
    }

    void FanTableComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
      if (syncingControlState || fanTableComboBox.SelectedItem == null) return;
      Program.ApplyFanTableSetting(fanTableComboBox.SelectedItem.ToString() == "降温模式" ? "cool" : "silent");
      RefreshDashboard();
    }

    void TempSensitivityComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
      if (syncingControlState || tempSensitivityComboBox.SelectedItem == null) return;
      Program.ApplyTempSensitivitySetting(ConvertTempSensitivityBack(tempSensitivityComboBox.SelectedItem.ToString()));
      RefreshDashboard();
    }

    void CpuPowerComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
      if (syncingControlState || cpuPowerComboBox.SelectedItem == null) return;
      string selected = cpuPowerComboBox.SelectedItem.ToString();
      Program.ApplyCpuPowerSetting(selected == "最大" ? "max" : selected);
      RefreshDashboard();
    }

    void GpuPowerComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
      if (syncingControlState || gpuPowerComboBox.SelectedItem == null) return;
      Program.ApplyGpuPowerSetting(ConvertGpuPowerValueBack(gpuPowerComboBox.SelectedItem.ToString()));
      RefreshDashboard();
    }

    void GpuClockComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
      if (syncingControlState || gpuClockComboBox.SelectedItem == null) return;
      string selected = gpuClockComboBox.SelectedItem.ToString();
      Program.ApplyGpuClockSetting(selected == "还原" ? 0 : int.Parse(selected.Replace(" MHz", string.Empty)));
      RefreshDashboard();
    }

    void SmartPowerControlCheckBox_Changed(object sender, RoutedEventArgs e) {
      if (syncingControlState || smartPowerControlCheckBox == null || !smartPowerControlCheckBox.IsChecked.HasValue) return;
      Program.ApplySmartPowerControlSetting(smartPowerControlCheckBox.IsChecked.Value);
      RefreshDashboard();
    }

    void FloatingBarButton_Click(object sender, RoutedEventArgs e) {
      if (syncingControlState) return;
      var snapshot = Program.GetDashboardSnapshot();
      Program.ApplyFloatingBarSetting(!snapshot.FloatingBarEnabled);
      RefreshDashboard();
    }

    void StrategySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
      if (sender is Slider slider) {
        UpdateThresholdValueText(slider);
      }

      if (syncingControlState || tuningApplyTimer == null) {
        return;
      }

      tuningApplyTimer.Stop();
      tuningApplyTimer.Start();
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
      var tuning = Program.GetPowerControlTuningSnapshot();
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

    void ApplyPowerTuningFromSliders() {
      if (cpuEmergencySlider == null) {
        return;
      }

      var tuning = new PowerControlTuning {
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

      Program.ApplyPowerControlTuning(tuning);

      syncingControlState = true;
      try {
        SyncPowerTuningControls();
      } finally {
        syncingControlState = false;
      }
    }

    void ResetTuningButton_Click(object sender, RoutedEventArgs e) {
      Program.ResetPowerControlTuningToDefault();
      syncingControlState = true;
      try {
        SyncPowerTuningControls();
      } finally {
        syncingControlState = false;
      }
      RefreshDashboard();
    }

    void ApplyManualFanFromSlider(bool force = false) {
      if (manualFanRpmSlider == null) {
        return;
      }

      int rpm = (int)Math.Round(manualFanRpmSlider.Value);
      if (!force && rpm == lastAppliedManualFanRpm) {
        return;
      }

      lastAppliedManualFanRpm = rpm;
      Program.ApplyFanControlSetting($"{rpm} RPM");
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

      if (smartActionText != null) {
        smartActionText.Text =
          $"CPU 上限 {(snapshot.SmartCpuLimitWatts > 0 ? $"{snapshot.SmartCpuLimitWatts}W" : "--")} | " +
          $"GPU 档位 {FormatGpuTier(snapshot.SmartGpuTier)} | " +
          $"FanBoost {(snapshot.SmartFanBoostActive ? "开启" : "关闭")}";
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

    string BuildTelemetryText(DashboardSnapshot snapshot) {
      float? batteryPower = GetBatteryPowerWatts(snapshot.Battery);
      var lines = new List<string> {
        $"CPU Temp       : {snapshot.CpuTemperature:F1} °C",
        $"CPU Power      : {snapshot.CpuPowerWatts:F1} W",
        $"GPU Temp       : {(snapshot.MonitorGpu ? $"{snapshot.GpuTemperature:F1} °C" : "disabled")}",
        $"GPU Power      : {(snapshot.MonitorGpu ? $"{snapshot.GpuPowerWatts:F1} W" : "--")}",
        $"System Est     : {snapshot.EstimatedSystemPowerWatts:F1} W",
        $"System Target  : {snapshot.TargetSystemPowerWatts:F1} W",
        $"Battery Power  : {(batteryPower.HasValue ? $"{batteryPower.Value:F1} W" : "--")}",
        $"Battery State  : {BuildBatteryState(snapshot.Battery)}",
        $"Capacity       : {(snapshot.Battery != null ? $"{snapshot.Battery.RemainingCapacityMilliwattHours / 1000f:F1} Wh" : "--")}",
        $"Voltage        : {(snapshot.Battery != null ? $"{snapshot.Battery.VoltageMillivolts / 1000f:F2} V" : "--")}",
        $"Battery %      : {snapshot.BatteryPercent}%",
        $"Fan RPM        : {FormatFanRpm(snapshot.FanSpeeds)}",
        $"MUX            : {FormatGfxMode(snapshot.GraphicsMode)}",
        $"Adapter        : {FormatAdapterStatus(snapshot.SmartAdapterStatus)}"
      };
      return string.Join(Environment.NewLine, lines);
    }

    string BuildConfigText(DashboardSnapshot snapshot) {
      var lines = new List<string> {
        $"Mode           : {(snapshot.FanMode == "performance" ? "狂暴" : "平衡")}",
        $"Fan Control    : {ConvertFanControlValue(snapshot.FanControl)}",
        $"Fan Curve      : {(snapshot.FanTable == "cool" ? "降温模式" : "安静模式")}",
        $"Sensitivity    : {ConvertTempSensitivity(snapshot.TempSensitivity)}",
        $"CPU Limit      : {(snapshot.CpuPowerSetting == "max" ? "最大" : snapshot.CpuPowerSetting)}",
        $"GPU Policy     : {ConvertGpuPowerValue(snapshot.GpuPowerSetting)}",
        $"GPU Clock      : {(snapshot.GpuClockLimit > 0 ? $"{snapshot.GpuClockLimit} MHz" : "还原")}",
        $"Smart Power    : {(snapshot.SmartPowerControlEnabled ? "Enabled" : "Disabled")} ({FormatSmartStateLabel(snapshot.SmartPowerControlState)})",
        $"Smart Reason   : {FormatSmartReason(snapshot.SmartPowerControlReason)}",
        $"Smart CPU Cap  : {(snapshot.SmartCpuLimitWatts > 0 ? $"{snapshot.SmartCpuLimitWatts} W" : "--")}",
        $"Smart GPU Tier : {snapshot.SmartGpuTier}",
        $"Smart FanBoost : {(snapshot.SmartFanBoostActive ? "On" : "Off")}",
        $"Floating Bar   : {(snapshot.FloatingBarEnabled ? "开启" : "关闭")}",
        $"GPU Control    : {FormatGpuControl(snapshot.GpuStatus)}",
        $"Adapter        : {FormatAdapterStatus(snapshot.SmartAdapterStatus)}",
        $"Capabilities   : {BuildCapabilitiesSummary(snapshot)}",
        $"Keyboard       : {FormatKeyboardType(snapshot.KeyboardType)}"
      };
      return string.Join(Environment.NewLine, lines);
    }

    string BuildHelpText() {
      Version version = Assembly.GetExecutingAssembly().GetName().Version;
      return
        $"版本号：{version}{Environment.NewLine}{Environment.NewLine}" +
        "一、散热与风扇" + Environment.NewLine +
        "1. 风扇曲线支持“安静模式(silent.txt)”和“降温模式(cool.txt)”。" + Environment.NewLine +
        "2. 若需自定义曲线，请编辑同目录文本文件，格式为：CPU,Fan1,Fan2,GPU,Fan1,Fan2。" + Environment.NewLine +
        "3. 温度响应支持 实时/高/中/低，用于抑制转速抖动。" + Environment.NewLine + Environment.NewLine +
        "二、功耗与性能" + Environment.NewLine +
        "1. 模式切换会影响 CPU/GPU 行为，部分机型会在切换时重置功耗上限。" + Environment.NewLine +
        "2. CPU 功率设置会同时影响 PL1/PL2。" + Environment.NewLine +
        "3. GPU 策略与锁频用于在温度、噪音和性能之间平衡。" + Environment.NewLine +
        "4. 智能功耗控制会在不超过手动上限的前提下动态调节，并在高温时进入紧急保护。" + Environment.NewLine + Environment.NewLine +
        "三、浮窗与监控" + Environment.NewLine +
        "1. 浮窗显示每秒刷新一次，可在主页面直接开关。" + Environment.NewLine +
        "2. 主界面“实时遥测”展示 CPU/GPU/电池/风扇等核心数据。" + Environment.NewLine + Environment.NewLine +
        "四、托盘与启动" + Environment.NewLine +
        "1. 托盘菜单已精简，主要操作集中在主窗口设置页。" + Environment.NewLine +
        "2. 点击“隐藏到托盘”只隐藏窗口，不退出程序。" + Environment.NewLine + Environment.NewLine +
        "项目地址：" + Environment.NewLine +
        "https://github.com/breadeding/OmenSuperHub";
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

    string ConvertGpuPowerValue(string value) {
      if (value == "max") return "CTGP开+DB开";
      if (value == "med") return "CTGP开+DB关";
      return "CTGP关+DB关";
    }

    string ConvertGpuPowerValueBack(string value) {
      if (value == "CTGP开+DB开") return "max";
      if (value == "CTGP开+DB关") return "med";
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
