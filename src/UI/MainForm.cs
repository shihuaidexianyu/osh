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

    readonly string[] fanModeItems = { "平衡", "狂暴" };
    readonly string[] fanControlItems = { "自动", "最大风扇", "1600 RPM", "2000 RPM", "2400 RPM", "2800 RPM", "3200 RPM", "3600 RPM" };
    readonly string[] fanTableItems = { "安静模式", "降温模式" };
    readonly string[] tempSensitivityItems = { "高", "中", "低", "实时" };
    readonly string[] cpuPowerItems = { "最大", "45 W", "55 W", "65 W", "75 W", "90 W" };
    readonly string[] gpuPowerItems = { "CTGP开+DB开", "CTGP开+DB关", "CTGP关+DB关" };
    readonly string[] gpuClockItems = { "还原", "1600 MHz", "1800 MHz", "2000 MHz", "2200 MHz", "2400 MHz" };

    Window window;
    DispatcherTimer refreshTimer;
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

    ComboBox fanModeComboBox;
    ComboBox fanControlComboBox;
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
      var card = CreateCard(220);
      var grid = CreateSettingsGrid();
      AddTitleToGrid(grid, "散热与风扇", "调节风扇模式、转速曲线和温度响应速度。");

      fanModeComboBox = CreateComboBox(fanModeItems, FanModeComboBox_SelectionChanged);
      fanControlComboBox = CreateComboBox(fanControlItems, FanControlComboBox_SelectionChanged);
      fanTableComboBox = CreateComboBox(fanTableItems, FanTableComboBox_SelectionChanged);
      tempSensitivityComboBox = CreateComboBox(tempSensitivityItems, TempSensitivityComboBox_SelectionChanged);

      AddControlRow(grid, 1, "模式", fanModeComboBox);
      AddControlRow(grid, 2, "控制", fanControlComboBox);
      AddControlRow(grid, 3, "曲线", fanTableComboBox);
      AddControlRow(grid, 4, "温度响应", tempSensitivityComboBox);
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
        Content = "启用智能功耗控制（Balanced + Emergency）",
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

    void AddControlRow(Grid grid, int rowIndex, string title, Control control) {
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

    void RefreshDashboard() {
      if (window == null || !window.IsVisible) {
        return;
      }

      var snapshot = Program.GetDashboardSnapshot();

      totalPowerText.Text = $"{snapshot.CpuPowerWatts + snapshot.GpuPowerWatts:F1} W";
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

      telemetryTextBox.Text = BuildTelemetryText(snapshot);
      configTextBox.Text = BuildConfigText(snapshot);
      helpTextBox.Text = BuildHelpText();

      SyncControlState(snapshot);
    }

    void SyncControlState(DashboardSnapshot snapshot) {
      syncingControlState = true;
      try {
        SelectComboItem(fanModeComboBox, snapshot.FanMode == "performance" ? "狂暴" : "平衡");
        SelectComboItem(fanControlComboBox, ConvertFanControlValue(snapshot.FanControl));
        SelectComboItem(fanTableComboBox, snapshot.FanTable == "cool" ? "降温模式" : "安静模式");
        SelectComboItem(tempSensitivityComboBox, ConvertTempSensitivity(snapshot.TempSensitivity));
        SelectComboItem(cpuPowerComboBox, snapshot.CpuPowerSetting == "max" ? "最大" : snapshot.CpuPowerSetting);
        SelectComboItem(gpuPowerComboBox, ConvertGpuPowerValue(snapshot.GpuPowerSetting));
        SelectComboItem(gpuClockComboBox, snapshot.GpuClockLimit > 0 ? $"{snapshot.GpuClockLimit} MHz" : "还原");
        smartPowerControlCheckBox.IsChecked = snapshot.SmartPowerControlEnabled;

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
      Program.ApplyFanControlSetting(selected == "自动" ? "auto" : selected == "最大风扇" ? "max" : selected);
      RefreshDashboard();
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
        $"Smart Power    : {(snapshot.SmartPowerControlEnabled ? "Enabled" : "Disabled")} ({snapshot.SmartPowerControlState})",
        $"Smart Reason   : {snapshot.SmartPowerControlReason}",
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

    string FormatFanRpm(List<int> fanSpeeds) {
      if (fanSpeeds == null || fanSpeeds.Count < 2) return "--";
      return $"{fanSpeeds[0]} / {fanSpeeds[1]}";
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
