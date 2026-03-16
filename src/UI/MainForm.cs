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
  public sealed class MainForm {
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
    readonly string[] graphicsModeItems = { "混合输出", "独显直连", "Optimus" };
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
    ComboBox graphicsModeComboBox;
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
      root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
      root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

      var header = BuildHeaderPanel();
      Grid.SetRow(header, 0);
      root.Children.Add(header);

      var tabs = new TabControl {
        Margin = new Thickness(0, 14, 0, 0),
        Background = Brushes.Transparent,
        BorderThickness = new Thickness(0),
        ItemContainerStyle = CreateTabItemStyle()
      };
      tabs.Items.Add(CreateTabPage("主控制", BuildMainControlPage()));
      tabs.Items.Add(CreateTabPage("设备状态", BuildHardwareStatusPage()));
      tabs.Items.Add(CreateTabPage("高级设置", BuildAdvancedSettingsPage()));

      Grid.SetRow(tabs, 1);
      root.Children.Add(tabs);
      window.Content = root;
    }

    TabItem CreateTabPage(string header, UIElement content) {
      return new TabItem {
        Header = new TextBlock {
          Text = header,
          TextWrapping = TextWrapping.NoWrap,
          Margin = new Thickness(2, 0, 2, 0)
        },
        Content = new ScrollViewer {
          VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
          HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
          Background = Brushes.Transparent,
          Padding = new Thickness(0, 14, 0, 0),
          Content = content
        }
      };
    }

    UIElement BuildMainControlPage() {
      var controlsGrid = new Grid();
      controlsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
      controlsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

      var leftColumn = new StackPanel {
        Margin = new Thickness(0, 0, 10, 0)
      };
      leftColumn.Children.Add(BuildQuickOverviewPanel());
      leftColumn.Children.Add(BuildPerformancePanel());

      var rightColumn = new StackPanel {
        Margin = new Thickness(10, 0, 0, 0)
      };
      rightColumn.Children.Add(BuildSmartPowerPanel());
      rightColumn.Children.Add(BuildOverlayPanel());

      Grid.SetColumn(leftColumn, 0);
      Grid.SetColumn(rightColumn, 1);
      controlsGrid.Children.Add(leftColumn);
      controlsGrid.Children.Add(rightColumn);
      return controlsGrid;
    }

    UIElement BuildHardwareStatusPage() {
      var controlsGrid = new Grid();
      controlsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
      controlsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

      var leftColumn = new StackPanel {
        Margin = new Thickness(0, 0, 10, 0)
      };
      leftColumn.Children.Add(BuildStatusPanel());

      var rightColumn = new StackPanel {
        Margin = new Thickness(10, 0, 0, 0)
      };
      rightColumn.Children.Add(BuildTemperatureSensorsPanel());

      Grid.SetColumn(leftColumn, 0);
      Grid.SetColumn(rightColumn, 1);
      controlsGrid.Children.Add(leftColumn);
      controlsGrid.Children.Add(rightColumn);
      return controlsGrid;
    }

    UIElement BuildAdvancedSettingsPage() {
      var content = new StackPanel();
      content.Children.Add(BuildCoolingPanel());
      content.Children.Add(BuildStrategyTuningPanel());
      return content;
    }

    Border CreateCard(double minHeight) {
      return new Border {
        Background = cardBack,
        BorderBrush = borderColor,
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(12),
        Padding = new Thickness(20),
        Margin = new Thickness(0, 0, 0, 14),
        MinHeight = minHeight
      };
    }

    Style CreateTabItemStyle() {
      var style = new Style(typeof(TabItem));
      style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
      style.Setters.Add(new Setter(Control.BorderBrushProperty, Brushes.Transparent));
      style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
      style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(16, 8, 16, 4)));
      style.Setters.Add(new Setter(Control.MarginProperty, new Thickness(0, 0, 28, 6)));
      style.Setters.Add(new Setter(FrameworkElement.MinHeightProperty, 42d));
      style.Setters.Add(new Setter(FrameworkElement.MinWidthProperty, 112d));
      style.Setters.Add(new Setter(Control.FontSizeProperty, 14d));
      style.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
      style.Setters.Add(new Setter(Control.ForegroundProperty, mutedText));
      style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Center));

      var template = new ControlTemplate(typeof(TabItem));
      var stack = new FrameworkElementFactory(typeof(StackPanel));
      stack.SetValue(StackPanel.OrientationProperty, Orientation.Vertical);
      stack.SetValue(FrameworkElement.MinHeightProperty, new TemplateBindingExtension(FrameworkElement.MinHeightProperty));

      var border = new FrameworkElementFactory(typeof(Border));
      border.Name = "TabBorder";
      border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
      border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
      border.SetValue(Border.SnapsToDevicePixelsProperty, true);
      var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
      presenter.SetValue(ContentPresenter.ContentSourceProperty, "Header");
      presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
      presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
      presenter.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Center);
      presenter.SetValue(FrameworkElement.MarginProperty, new Thickness(2, 0, 2, 4));

      border.AppendChild(presenter);

      var indicator = new FrameworkElementFactory(typeof(Border));
      indicator.Name = "SelectionIndicator";
      indicator.SetValue(Border.HeightProperty, 2d);
      indicator.SetValue(Border.CornerRadiusProperty, new CornerRadius(999));
      indicator.SetValue(Border.BackgroundProperty, Brushes.Transparent);
      indicator.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);

      stack.AppendChild(border);
      stack.AppendChild(indicator);
      template.VisualTree = stack;

      var selectedTrigger = new Trigger {
        Property = TabItem.IsSelectedProperty,
        Value = true
      };
      selectedTrigger.Setters.Add(new Setter(Control.ForegroundProperty, strongText));
      selectedTrigger.Setters.Add(new Setter(Border.BackgroundProperty, accentBlue, "SelectionIndicator"));
      template.Triggers.Add(selectedTrigger);

      var hoverTrigger = new Trigger {
        Property = TabItem.IsMouseOverProperty,
        Value = true
      };
      hoverTrigger.Setters.Add(new Setter(Control.ForegroundProperty, strongText));
      template.Triggers.Add(hoverTrigger);

      style.Setters.Add(new Setter(Control.TemplateProperty, template));
      return style;
    }

    TextBlock CreateSectionTitle(string text) {
      return new TextBlock {
        Text = text,
        Foreground = strongText,
        FontSize = 20,
        FontWeight = FontWeights.SemiBold,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 0, 0, 4)
      };
    }

    TextBlock CreateSectionSubtitle(string text) {
      return new TextBlock {
        Text = text,
        Foreground = mutedText,
        FontSize = 13,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 0, 0, 12)
      };
    }

    Border BuildQuickOverviewPanel() {
      var card = CreateCard(210);

      var root = new StackPanel();
      root.Children.Add(CreateSectionTitle("总览"));
      root.Children.Add(CreateSectionSubtitle("温度、功率、电池与风扇状态一屏可见。"));

      var tiles = new WrapPanel {
        HorizontalAlignment = HorizontalAlignment.Stretch
      };

      tiles.Children.Add(CreateOverviewMetric("CPU", "温度 / 功率", accentBlue, out leftCpuText));
      tiles.Children.Add(CreateOverviewMetric("GPU", "温度 / 功率", accentGreen, out leftGpuText));
      tiles.Children.Add(CreateOverviewMetric("电池", "供电 / 功率", accentOrange, out leftBatteryText));
      tiles.Children.Add(CreateOverviewMetric("风扇", "真实 RPM", accentBlue, out leftFanText));
      tiles.Children.Add(CreateOverviewMetric("策略", "当前控制模式", accentGreen, out leftModeText));

      root.Children.Add(tiles);
      card.Child = root;
      return card;
    }

    Border CreateOverviewMetric(string title, string subtitle, Brush accentBrush, out TextBlock valueText) {
      Brush tileFill = softSlateFill;
      if (ReferenceEquals(accentBrush, accentBlue)) {
        tileFill = softBlueFill;
      } else if (ReferenceEquals(accentBrush, accentGreen)) {
        tileFill = softGreenFill;
      } else if (ReferenceEquals(accentBrush, accentOrange)) {
        tileFill = softOrangeFill;
      }

      var content = new StackPanel();
      content.Children.Add(new Border {
        Width = 28,
        Height = 3,
        Background = accentBrush,
        CornerRadius = new CornerRadius(2),
        Margin = new Thickness(0, 0, 0, 10),
        HorizontalAlignment = HorizontalAlignment.Left
      });
      content.Children.Add(new TextBlock {
        Text = title,
        Foreground = strongText,
        FontSize = 14,
        FontWeight = FontWeights.SemiBold
      });
      content.Children.Add(new TextBlock {
        Text = subtitle,
        Foreground = mutedText,
        FontSize = 12,
        Margin = new Thickness(0, 2, 0, 0)
      });

      valueText = new TextBlock {
        Text = "--",
        Foreground = strongText,
        FontSize = 20,
        FontWeight = FontWeights.Bold,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 10, 0, 0)
      };
      content.Children.Add(valueText);

      return new Border {
        MinWidth = 214,
        Margin = new Thickness(0, 0, 12, 12),
        Padding = new Thickness(14, 14, 14, 14),
        CornerRadius = new CornerRadius(12),
        BorderBrush = borderColor,
        BorderThickness = new Thickness(1),
        Background = tileFill,
        Child = content
      };
    }

    Border BuildHeaderPanel() {
      var card = CreateCard(132);

      var layout = new Grid();
      layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
      layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

      var left = new StackPanel();
      left.Children.Add(new TextBlock {
        Text = "OmenSuperHub",
        Foreground = strongText,
        FontSize = 30,
        FontWeight = FontWeights.Bold
      });
      left.Children.Add(new TextBlock {
        Text = "Intel i9-13900HX + RTX 4060 Laptop 的热噪功耗控制",
        Foreground = mutedText,
        FontSize = 13,
        Margin = new Thickness(0, 4, 0, 0),
        TextWrapping = TextWrapping.Wrap
      });

      var right = new StackPanel {
        HorizontalAlignment = HorizontalAlignment.Right,
        VerticalAlignment = VerticalAlignment.Center
      };
      totalPowerText = new TextBlock {
        Text = "-- W",
        Foreground = accentOrange,
        FontSize = 44,
        FontWeight = FontWeights.Bold,
        HorizontalAlignment = HorizontalAlignment.Right
      };
      lastUpdateText = new TextBlock {
        Text = "最近刷新: --",
        Foreground = mutedText,
        FontSize = 13,
        HorizontalAlignment = HorizontalAlignment.Right
      };
      right.Children.Add(new TextBlock {
        Text = "当前整机估算功耗",
        Foreground = mutedText,
        FontSize = 12,
        HorizontalAlignment = HorizontalAlignment.Right
      });
      right.Children.Add(totalPowerText);
      right.Children.Add(lastUpdateText);

      var actionRow = new StackPanel {
        Orientation = Orientation.Horizontal,
        HorizontalAlignment = HorizontalAlignment.Right,
        Margin = new Thickness(0, 10, 0, 0)
      };
      applyChangesButton = new Button {
        Content = "应用更改",
        Padding = new Thickness(14, 8, 14, 8),
        FontSize = 13,
        FontWeight = FontWeights.SemiBold,
        Foreground = Brushes.White,
        Background = accentBlue,
        BorderBrush = accentBlue,
        Margin = new Thickness(0, 0, 8, 0),
        IsEnabled = false
      };
      applyChangesButton.Click += ApplyChangesButton_Click;
      StyleButton(applyChangesButton);

      discardChangesButton = new Button {
        Content = "放弃更改",
        Padding = new Thickness(14, 8, 14, 8),
        FontSize = 13,
        FontWeight = FontWeights.SemiBold,
        Foreground = strongText,
        Background = softSlateFill,
        BorderBrush = borderColor,
        IsEnabled = false
      };
      discardChangesButton.Click += DiscardChangesButton_Click;
      StyleButton(discardChangesButton);

      actionRow.Children.Add(applyChangesButton);
      actionRow.Children.Add(discardChangesButton);
      right.Children.Add(actionRow);

      Grid.SetColumn(left, 0);
      Grid.SetColumn(right, 1);
      layout.Children.Add(left);
      layout.Children.Add(right);
      card.Child = layout;
      return card;
    }

    Border BuildCoolingPanel() {
      var card = CreateCard(430);
      var root = new StackPanel();
      root.Children.Add(CreateSectionTitle("高级覆盖"));
      root.Children.Add(CreateSectionSubtitle("只有在预设模式不满足需求时，才需要手动改写底层参数。这里的修改会让当前模式进入自定义，显卡模式切换通常需要重新登录或重启后完全生效。"));

      fanModeComboBox = CreateComboBox(fanModeItems, FanModeComboBox_SelectionChanged);
      fanControlComboBox = CreateComboBox(fanControlModeItems, FanControlComboBox_SelectionChanged);
      var manualFanControl = CreateManualFanRpmControl();
      fanTableComboBox = CreateComboBox(fanTableItems, FanTableComboBox_SelectionChanged);
      tempSensitivityComboBox = CreateComboBox(tempSensitivityItems, TempSensitivityComboBox_SelectionChanged);

      cpuPowerComboBox = CreateComboBox(cpuPowerItems, CpuPowerComboBox_SelectionChanged);
      gpuPowerComboBox = CreateComboBox(gpuPowerItems, GpuPowerComboBox_SelectionChanged);
      graphicsModeComboBox = CreateComboBox(graphicsModeItems, GraphicsModeComboBox_SelectionChanged);
      gpuClockComboBox = CreateComboBox(gpuClockItems, GpuClockComboBox_SelectionChanged);

      var advancedGrid = CreateSettingsGrid();
      AddControlRow(advancedGrid, 1, "风扇模式", fanModeComboBox);
      AddControlRow(advancedGrid, 2, "风扇控制", fanControlComboBox);
      AddControlRow(advancedGrid, 3, "手动转速", manualFanControl);
      AddControlRow(advancedGrid, 4, "风扇曲线", fanTableComboBox);
      AddControlRow(advancedGrid, 5, "温度响应", tempSensitivityComboBox);
      AddControlRow(advancedGrid, 6, "CPU 功率", cpuPowerComboBox);
      AddControlRow(advancedGrid, 7, "GPU 策略", gpuPowerComboBox);
      AddControlRow(advancedGrid, 8, "显卡模式", graphicsModeComboBox);
      AddControlRow(advancedGrid, 9, "GPU 锁频", gpuClockComboBox);

      root.Children.Add(advancedGrid);
      card.Child = root;
      return card;
    }

    Border BuildPerformancePanel() {
      var card = CreateCard(246);
      var grid = CreateSettingsGrid();
      AddTitleToGrid(grid, "主模式", "先选择目标，再让系统自动平衡温度、噪声和功耗。");

      usageModeComboBox = CreateComboBox(usageModeItems, UsageModeComboBox_SelectionChanged);
      smartPowerControlCheckBox = new CheckBox {
        Content = "启用智能功耗保护与自动调节",
        Foreground = strongText,
        FontSize = 14,
        Margin = new Thickness(0, 8, 0, 8),
        VerticalAlignment = VerticalAlignment.Center
      };
      smartPowerControlCheckBox.Checked += SmartPowerControlCheckBox_Changed;
      smartPowerControlCheckBox.Unchecked += SmartPowerControlCheckBox_Changed;

      var hintText = new TextBlock {
        Text = "安静：优先低噪声和低功耗 | 均衡：默认日常模式 | 性能：优先负载表现，但仍受温度墙保护 | MAX：关闭智能限制、强制最大风扇并切到独显直连",
        Foreground = mutedText,
        FontSize = 12,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 4, 0, 6)
      };

      AddControlRow(grid, 1, "主模式", usageModeComboBox);
      Grid.SetRow(hintText, 2);
      Grid.SetColumn(hintText, 0);
      Grid.SetColumnSpan(hintText, 2);
      grid.Children.Add(hintText);
      AddControlRow(grid, 3, "智能控制", smartPowerControlCheckBox);
      card.Child = grid;
      return card;
    }

    Border BuildSmartPowerPanel() {
      var card = CreateCard(300);

      var root = new Grid();
      root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
      root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
      root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
      root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

      var titleWrap = new StackPanel();
      titleWrap.Children.Add(CreateSectionTitle("智能功耗"));
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
        TextWrapping = TextWrapping.Wrap
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
        Padding = new Thickness(14, 8, 14, 8),
        FontSize = 13,
        FontWeight = FontWeights.SemiBold,
        Foreground = strongText,
        Background = softSlateFill,
        BorderBrush = borderColor
      };
      resetButton.Click += ResetTuningButton_Click;
      StyleButton(resetButton);
      actions.Children.Add(resetButton);

      Grid.SetRow(actions, 2);
      root.Children.Add(actions);

      card.Child = root;
      return card;
    }

    Border BuildOverlayPanel() {
      var card = CreateCard(160);
      var grid = CreateSettingsGrid();
      AddTitleToGrid(grid, "浮窗与显示", "控制桌面浮窗显示状态。");

      floatingBarButton = new Button {
        Content = "浮窗: 关闭",
        Padding = new Thickness(14, 8, 14, 8),
        FontSize = 14,
        FontWeight = FontWeights.SemiBold,
        Foreground = strongText,
        Background = softSlateFill,
        BorderBrush = borderColor,
        HorizontalAlignment = HorizontalAlignment.Left,
        MinWidth = 160
      };
      floatingBarButton.Click += FloatingBarButton_Click;
      StyleButton(floatingBarButton);
      floatingBarLocationComboBox = CreateComboBox(floatingBarLocationItems, FloatingBarLocationComboBox_SelectionChanged);

      AddControlRow(grid, 1, "浮窗", floatingBarButton);
      AddControlRow(grid, 2, "位置", floatingBarLocationComboBox);
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

    Border BuildTemperatureSensorsPanel() {
      var card = CreateCard(360);
      var root = new Grid();
      root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
      root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
      root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
      root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

      var titleWrap = new StackPanel();
      titleWrap.Children.Add(CreateSectionTitle("温度传感器"));
      titleWrap.Children.Add(CreateSectionSubtitle("递归读取主硬件与子硬件的全部温度传感器。"));
      Grid.SetRow(titleWrap, 0);
      root.Children.Add(titleWrap);

      temperatureSensorSummaryText = new TextBlock {
        Text = "正在读取...",
        Foreground = mutedText,
        FontSize = 13,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 0, 0, 8)
      };
      Grid.SetRow(temperatureSensorSummaryText, 1);
      root.Children.Add(temperatureSensorSummaryText);

      var legendWrap = new WrapPanel {
        Margin = new Thickness(0, 0, 0, 10)
      };
      legendWrap.Children.Add(CreateChartLegendItem(accentBlue, "CPU 参考温度（Package）"));
      legendWrap.Children.Add(CreateChartLegendItem(Brushes.IndianRed, "GPU 参考温度"));
      legendWrap.Children.Add(CreateChartLegendItem(accentOrange, "当前最高传感器温度"));
      legendWrap.Children.Add(CreateChartLegendItem(Brushes.ForestGreen, "CPU 保护温度线"));
      legendWrap.Children.Add(CreateChartLegendItem(Brushes.DarkOliveGreen, "GPU 保护温度线"));
      legendWrap.Children.Add(CreateChartLegendItem(Brushes.DimGray, "CPU 功耗限制线"));
      Grid.SetRow(legendWrap, 2);
      root.Children.Add(legendWrap);

      var chartBorder = new Border {
        BorderThickness = new Thickness(1),
        BorderBrush = borderColor,
        Background = Brushes.White,
        CornerRadius = new CornerRadius(6),
        MinHeight = 220,
        Padding = new Thickness(4)
      };

      temperatureTrendCanvas = new Canvas {
        Background = subtleFill,
        Height = 220,
        ClipToBounds = true
      };
      chartBorder.Child = temperatureTrendCanvas;

      Grid.SetRow(chartBorder, 3);
      root.Children.Add(chartBorder);

      card.Child = root;
      return card;
    }

    FrameworkElement CreateChartLegendItem(Brush color, string label) {
      var row = new StackPanel {
        Orientation = Orientation.Horizontal,
        VerticalAlignment = VerticalAlignment.Center
      };
      row.Children.Add(new Border {
        Width = 18,
        Height = 3,
        Background = color,
        CornerRadius = new CornerRadius(2),
        Margin = new Thickness(0, 0, 6, 0),
        VerticalAlignment = VerticalAlignment.Center
      });
      row.Children.Add(new TextBlock {
        Text = label,
        Foreground = mutedText,
        FontSize = 12,
        VerticalAlignment = VerticalAlignment.Center
      });
      return new Border {
        Background = subtleFill,
        BorderBrush = borderColor,
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(10),
        Padding = new Thickness(10, 6, 10, 6),
        Margin = new Thickness(0, 0, 10, 8),
        Child = row
      };
    }

    Grid CreateSettingsGrid() {
      var grid = new Grid();
      grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
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
        Height = 9,
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
      grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
      grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
      grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

      var label = new TextBlock {
        Text = title,
        Foreground = mutedText,
        FontSize = 13,
        VerticalAlignment = VerticalAlignment.Center,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 2, 8, 2)
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
        VerticalAlignment = VerticalAlignment.Top,
        TextWrapping = TextWrapping.Wrap,
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
        VerticalAlignment = VerticalAlignment.Top,
        TextWrapping = TextWrapping.Wrap,
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
        MinWidth = 270,
        Height = 38,
        FontSize = 14,
        Padding = new Thickness(8, 4, 8, 4),
        Background = Brushes.White,
        BorderBrush = borderColor,
        Foreground = strongText
      };
      foreach (var item in items) {
        comboBox.Items.Add(item);
      }
      comboBox.SelectionChanged += handler;
      return comboBox;
    }

    void StyleButton(Button button) {
      if (button == null) {
        return;
      }

      button.BorderThickness = new Thickness(1);
      button.Cursor = System.Windows.Input.Cursors.Hand;
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

    string ConvertGraphicsModeSetting(string value) {
      switch ((value ?? string.Empty).ToLowerInvariant()) {
        case "discrete":
          return "独显直连";
        case "optimus":
          return "Optimus";
        default:
          return "混合输出";
      }
    }

    string ConvertGraphicsModeSettingBack(string value) {
      switch (value) {
        case "独显直连":
          return "discrete";
        case "Optimus":
          return "optimus";
        default:
          return "hybrid";
      }
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
