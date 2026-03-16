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
      content.Children.Add(BuildAppBehaviorPanel());
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
      root.Children.Add(CreateSectionSubtitle("只有在预设模式不满足需求时，才需要手动改写底层参数。这里的修改会让当前模式进入自定义。"));

      fanModeComboBox = CreateComboBox(fanModeItems, FanModeComboBox_SelectionChanged);
      fanControlComboBox = CreateComboBox(fanControlModeItems, FanControlComboBox_SelectionChanged);
      var manualFanControl = CreateManualFanRpmControl();
      fanTableComboBox = CreateComboBox(fanTableItems, FanTableComboBox_SelectionChanged);
      tempSensitivityComboBox = CreateComboBox(tempSensitivityItems, TempSensitivityComboBox_SelectionChanged);

      cpuPowerComboBox = CreateComboBox(cpuPowerItems, CpuPowerComboBox_SelectionChanged);
      gpuPowerComboBox = CreateComboBox(gpuPowerItems, GpuPowerComboBox_SelectionChanged);
      gpuClockComboBox = CreateComboBox(gpuClockItems, GpuClockComboBox_SelectionChanged);

      var advancedGrid = CreateSettingsGrid();
      AddControlRow(advancedGrid, 1, "风扇模式", fanModeComboBox);
      AddControlRow(advancedGrid, 2, "风扇控制", fanControlComboBox);
      AddControlRow(advancedGrid, 3, "手动转速", manualFanControl);
      AddControlRow(advancedGrid, 4, "风扇曲线", fanTableComboBox);
      AddControlRow(advancedGrid, 5, "温度响应", tempSensitivityComboBox);
      AddControlRow(advancedGrid, 6, "CPU 功率", cpuPowerComboBox);
      AddControlRow(advancedGrid, 7, "GPU 策略", gpuPowerComboBox);
      AddControlRow(advancedGrid, 8, "GPU 锁频", gpuClockComboBox);

      root.Children.Add(advancedGrid);
      card.Child = root;
      return card;
    }

    Border BuildAppBehaviorPanel() {
      var card = CreateCard(220);
      var grid = CreateSettingsGrid();
      AddTitleToGrid(grid, "应用行为", "设置登录后自动启动，以及按下 OMEN 键时由本应用接管的动作。");

      autoStartCheckBox = new CheckBox {
        Content = "登录后自动启动 OmenSuperHub",
        Foreground = strongText,
        FontSize = 14,
        Margin = new Thickness(0, 8, 0, 8),
        VerticalAlignment = VerticalAlignment.Center
      };
      autoStartCheckBox.Checked += AutoStartCheckBox_Changed;
      autoStartCheckBox.Unchecked += AutoStartCheckBox_Changed;

      omenKeyComboBox = CreateComboBox(omenKeyItems, OmenKeyComboBox_SelectionChanged);

      AddControlRow(grid, 1, "自启动", autoStartCheckBox);
      AddControlRow(grid, 2, "OMEN 按键", omenKeyComboBox);

      var hint = new TextBlock {
        Text = "默认：触发系统原有 Omen Key 任务 | 切换浮窗显示：按键直接开关本应用浮窗 | 禁用：不注册任何按键动作",
        Foreground = mutedText,
        FontSize = 12,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 2, 0, 0)
      };
      Grid.SetRow(hint, 3);
      Grid.SetColumn(hint, 1);
      grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
      grid.Children.Add(hint);

      card.Child = grid;
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
        Text = "安静：优先低噪声和低功耗 | 均衡：默认日常模式 | 性能：优先负载表现，但仍受温度墙保护 | MAX：关闭智能限制、强制最大风扇",
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

  }
}
