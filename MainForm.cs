using System;
using System.Drawing;
using System.Windows.Forms;

namespace OmenSuperHub {
  public partial class MainForm : Form {
    private static MainForm _instance;

    private readonly System.Windows.Forms.Timer refreshTimer;
    private Label cpuTempValueLabel;
    private Label cpuPowerValueLabel;
    private Label gpuTempValueLabel;
    private Label gpuPowerValueLabel;
    private Label batteryPowerValueLabel;
    private Label batteryModeValueLabel;
    private Label batteryCapacityValueLabel;
    private Label fanValueLabel;
    private Label gfxValueLabel;
    private Label adapterValueLabel;
    private Label policyValueLabel;
    private Label subtitleLabel;
    private ProgressBar batteryCapacityBar;
    private TextBox configTextBox;
    private TextBox capabilityTextBox;

    public MainForm() {
      Text = "OmenSuperHub 控制台";
      StartPosition = FormStartPosition.CenterScreen;
      MinimumSize = new Size(920, 640);
      Size = new Size(1020, 720);
      BackColor = Color.FromArgb(246, 240, 229);
      Icon = Properties.Resources.fan;

      BuildLayout();

      refreshTimer = new System.Windows.Forms.Timer();
      refreshTimer.Interval = 1000;
      refreshTimer.Tick += (s, e) => RefreshDashboard();
      refreshTimer.Start();

      Shown += (s, e) => RefreshDashboard();
      FormClosing += MainForm_FormClosing;
    }

    private void BuildLayout() {
      var root = new TableLayoutPanel {
        Dock = DockStyle.Fill,
        ColumnCount = 1,
        RowCount = 4,
        Padding = new Padding(18),
        BackColor = BackColor
      };
      root.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));
      root.RowStyles.Add(new RowStyle(SizeType.Absolute, 250));
      root.RowStyles.Add(new RowStyle(SizeType.Absolute, 210));
      root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
      Controls.Add(root);

      root.Controls.Add(BuildHeader(), 0, 0);
      root.Controls.Add(BuildMetricGrid(), 0, 1);
      root.Controls.Add(BuildStatusGrid(), 0, 2);
      root.Controls.Add(BuildDetailGrid(), 0, 3);
    }

    private Control BuildHeader() {
      var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };

      var titleLabel = new Label {
        AutoSize = true,
        Font = new Font("Microsoft YaHei UI", 22, FontStyle.Bold),
        ForeColor = Color.FromArgb(46, 36, 26),
        Text = "实时功率与硬件状态"
      };

      subtitleLabel = new Label {
        AutoSize = true,
        Font = new Font("Microsoft YaHei UI", 10, FontStyle.Regular),
        ForeColor = Color.FromArgb(114, 90, 64),
        Text = "正在等待首次刷新..."
      };
      subtitleLabel.Location = new Point(3, 46);

      var hideButton = new Button {
        Anchor = AnchorStyles.Top | AnchorStyles.Right,
        Text = "隐藏到托盘",
        Width = 118,
        Height = 34,
        FlatStyle = FlatStyle.Flat,
        BackColor = Color.FromArgb(64, 53, 41),
        ForeColor = Color.White,
        Font = new Font("Microsoft YaHei UI", 9, FontStyle.Bold)
      };
      hideButton.FlatAppearance.BorderSize = 0;
      hideButton.Location = new Point(850, 14);
      hideButton.Click += (s, e) => Hide();

      var refreshButton = new Button {
        Anchor = AnchorStyles.Top | AnchorStyles.Right,
        Text = "立即刷新",
        Width = 100,
        Height = 34,
        FlatStyle = FlatStyle.Flat,
        BackColor = Color.FromArgb(220, 123, 47),
        ForeColor = Color.White,
        Font = new Font("Microsoft YaHei UI", 9, FontStyle.Bold)
      };
      refreshButton.FlatAppearance.BorderSize = 0;
      refreshButton.Location = new Point(742, 14);
      refreshButton.Click += (s, e) => RefreshDashboard();

      panel.Resize += (s, e) => {
        hideButton.Left = panel.ClientSize.Width - hideButton.Width;
        refreshButton.Left = hideButton.Left - refreshButton.Width - 10;
      };

      panel.Controls.Add(titleLabel);
      panel.Controls.Add(subtitleLabel);
      panel.Controls.Add(refreshButton);
      panel.Controls.Add(hideButton);
      return panel;
    }

    private Control BuildMetricGrid() {
      var grid = new TableLayoutPanel {
        Dock = DockStyle.Fill,
        ColumnCount = 3,
        RowCount = 2,
        BackColor = Color.Transparent,
        Margin = new Padding(0, 8, 0, 8)
      };

      for (int i = 0; i < 3; i++) {
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
      }
      for (int i = 0; i < 2; i++) {
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
      }

      grid.Controls.Add(CreateMetricCard("CPU 温度", "0.0 °C", Color.FromArgb(30, 90, 150), out cpuTempValueLabel), 0, 0);
      grid.Controls.Add(CreateMetricCard("CPU 功率", "0.0 W", Color.FromArgb(214, 101, 38), out cpuPowerValueLabel), 1, 0);
      grid.Controls.Add(CreateMetricCard("GPU 温度", "0.0 °C", Color.FromArgb(39, 116, 86), out gpuTempValueLabel), 2, 0);
      grid.Controls.Add(CreateMetricCard("GPU 功率", "0.0 W", Color.FromArgb(143, 71, 132), out gpuPowerValueLabel), 0, 1);
      grid.Controls.Add(CreateMetricCard("电池功率", "--", Color.FromArgb(122, 85, 34), out batteryPowerValueLabel), 1, 1);
      grid.Controls.Add(CreateBatteryCard(), 2, 1);

      return grid;
    }

    private Control BuildStatusGrid() {
      var grid = new TableLayoutPanel {
        Dock = DockStyle.Fill,
        ColumnCount = 4,
        RowCount = 1,
        BackColor = Color.Transparent,
        Margin = new Padding(0, 4, 0, 8)
      };

      for (int i = 0; i < 4; i++) {
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
      }

      grid.Controls.Add(CreateMetricCard("风扇状态", "--", Color.FromArgb(70, 119, 61), out fanValueLabel), 0, 0);
      grid.Controls.Add(CreateMetricCard("显卡模式", "--", Color.FromArgb(31, 89, 105), out gfxValueLabel), 1, 0);
      grid.Controls.Add(CreateMetricCard("适配器/供电", "--", Color.FromArgb(110, 88, 48), out adapterValueLabel), 2, 0);
      grid.Controls.Add(CreateMetricCard("当前策略", "--", Color.FromArgb(93, 56, 45), out policyValueLabel), 3, 0);

      return grid;
    }

    private Control BuildDetailGrid() {
      var grid = new TableLayoutPanel {
        Dock = DockStyle.Fill,
        ColumnCount = 2,
        RowCount = 1,
        BackColor = Color.Transparent
      };
      grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
      grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));

      configTextBox = CreateDetailBox("运行配置");
      capabilityTextBox = CreateDetailBox("硬件能力");

      grid.Controls.Add(WrapDetailBox("运行配置", configTextBox), 0, 0);
      grid.Controls.Add(WrapDetailBox("硬件能力", capabilityTextBox), 1, 0);
      return grid;
    }

    private Control CreateMetricCard(string title, string value, Color accentColor, out Label valueLabel) {
      var panel = new Panel {
        Dock = DockStyle.Fill,
        Margin = new Padding(8),
        Padding = new Padding(16, 14, 16, 14),
        BackColor = Color.White
      };

      var accent = new Panel {
        Dock = DockStyle.Top,
        Height = 6,
        BackColor = accentColor
      };

      var titleLabel = new Label {
        AutoSize = true,
        Font = new Font("Microsoft YaHei UI", 9, FontStyle.Bold),
        ForeColor = Color.FromArgb(112, 93, 72),
        Text = title
      };
      titleLabel.Location = new Point(16, 26);

      valueLabel = new Label {
        AutoSize = true,
        Font = new Font("Microsoft YaHei UI", 22, FontStyle.Bold),
        ForeColor = Color.FromArgb(46, 36, 26),
        Text = value
      };
      valueLabel.Location = new Point(16, 54);

      panel.Controls.Add(accent);
      panel.Controls.Add(titleLabel);
      panel.Controls.Add(valueLabel);
      return panel;
    }

    private Control CreateBatteryCard() {
      var panel = new Panel {
        Dock = DockStyle.Fill,
        Margin = new Padding(8),
        Padding = new Padding(16, 14, 16, 14),
        BackColor = Color.White
      };

      var accent = new Panel {
        Dock = DockStyle.Top,
        Height = 6,
        BackColor = Color.FromArgb(184, 125, 39)
      };

      var titleLabel = new Label {
        AutoSize = true,
        Font = new Font("Microsoft YaHei UI", 9, FontStyle.Bold),
        ForeColor = Color.FromArgb(112, 93, 72),
        Text = "电池状态"
      };
      titleLabel.Location = new Point(16, 26);

      batteryModeValueLabel = new Label {
        AutoSize = true,
        Font = new Font("Microsoft YaHei UI", 16, FontStyle.Bold),
        ForeColor = Color.FromArgb(46, 36, 26),
        Text = "--"
      };
      batteryModeValueLabel.Location = new Point(16, 54);

      batteryCapacityValueLabel = new Label {
        AutoSize = true,
        Font = new Font("Microsoft YaHei UI", 10, FontStyle.Regular),
        ForeColor = Color.FromArgb(114, 90, 64),
        Text = "剩余容量 --"
      };
      batteryCapacityValueLabel.Location = new Point(18, 92);

      batteryCapacityBar = new ProgressBar {
        Location = new Point(18, 124),
        Width = 250,
        Height = 18,
        Style = ProgressBarStyle.Continuous,
        Maximum = 100
      };

      panel.Resize += (s, e) => {
        batteryCapacityBar.Width = Math.Max(160, panel.ClientSize.Width - 36);
      };

      panel.Controls.Add(accent);
      panel.Controls.Add(titleLabel);
      panel.Controls.Add(batteryModeValueLabel);
      panel.Controls.Add(batteryCapacityValueLabel);
      panel.Controls.Add(batteryCapacityBar);
      return panel;
    }

    private static GroupBox WrapDetailBox(string title, TextBox textBox) {
      var groupBox = new GroupBox {
        Dock = DockStyle.Fill,
        Text = title,
        Font = new Font("Microsoft YaHei UI", 9, FontStyle.Bold),
        ForeColor = Color.FromArgb(88, 68, 44),
        Padding = new Padding(12),
        Margin = new Padding(8)
      };

      textBox.Dock = DockStyle.Fill;
      groupBox.Controls.Add(textBox);
      return groupBox;
    }

    private static TextBox CreateDetailBox(string _) {
      return new TextBox {
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        BorderStyle = BorderStyle.None,
        BackColor = Color.FromArgb(252, 249, 242),
        ForeColor = Color.FromArgb(46, 36, 26),
        Font = new Font("Consolas", 10, FontStyle.Regular)
      };
    }

    private void RefreshDashboard() {
      var snapshot = Program.GetDashboardSnapshot();
      float? batteryPower = GetBatteryPower(snapshot.Battery);

      cpuTempValueLabel.Text = $"{snapshot.CpuTemperature:F1} °C";
      cpuPowerValueLabel.Text = $"{snapshot.CpuPowerWatts:F1} W";
      gpuTempValueLabel.Text = snapshot.MonitorGpu ? $"{snapshot.GpuTemperature:F1} °C" : "GPU 监控关闭";
      gpuPowerValueLabel.Text = snapshot.MonitorGpu ? $"{snapshot.GpuPowerWatts:F1} W" : "--";
      batteryPowerValueLabel.Text = batteryPower.HasValue ? $"{batteryPower.Value:F1} W" : "--";
      batteryModeValueLabel.Text = FormatBatteryMode(snapshot.Battery, snapshot.AcOnline);
      batteryCapacityValueLabel.Text = FormatBatteryCapacity(snapshot.Battery);
      batteryCapacityBar.Value = Math.Max(0, Math.Min(100, snapshot.BatteryPercent));
      fanValueLabel.Text = snapshot.MonitorFan ? $"{snapshot.FanSpeeds[0] * 100}/{snapshot.FanSpeeds[1] * 100} RPM" : "风扇监控关闭";
      gfxValueLabel.Text = FormatGraphicsMode(snapshot.GraphicsMode);
      adapterValueLabel.Text = $"{FormatAdapterStatus(snapshot.SmartAdapterStatus)} / {(snapshot.AcOnline ? "AC" : "Battery")}";
      policyValueLabel.Text = $"{snapshot.FanMode} | {snapshot.CpuPowerSetting}";

      subtitleLabel.Text = BuildSubtitle(snapshot, batteryPower);
      configTextBox.Text = BuildConfigText(snapshot, batteryPower);
      capabilityTextBox.Text = BuildCapabilityText(snapshot);
    }

    private static float? GetBatteryPower(Program.BatteryTelemetry battery) {
      if (battery == null) {
        return null;
      }

      if (battery.Discharging && battery.DischargeRateMilliwatts > 0) {
        return battery.DischargeRateMilliwatts / 1000f;
      }

      if (battery.Charging && battery.ChargeRateMilliwatts > 0) {
        return battery.ChargeRateMilliwatts / 1000f;
      }

      return null;
    }

    private static string FormatBatteryMode(Program.BatteryTelemetry battery, bool acOnline) {
      if (battery == null) {
        return "不可用";
      }

      if (battery.Discharging) {
        return "电池放电";
      }

      if (battery.Charging) {
        return "正在充电";
      }

      return acOnline ? "交流电待机" : "电池待机";
    }

    private static string FormatBatteryCapacity(Program.BatteryTelemetry battery) {
      if (battery == null || battery.RemainingCapacityMilliwattHours <= 0) {
        return "剩余容量 --";
      }

      return $"剩余容量 {battery.RemainingCapacityMilliwattHours / 1000f:F1} Wh";
    }

    private static string FormatGraphicsMode(OmenHardware.OmenGfxMode mode) {
      switch (mode) {
        case OmenHardware.OmenGfxMode.Hybrid:
          return "Hybrid";
        case OmenHardware.OmenGfxMode.Discrete:
          return "Discrete";
        case OmenHardware.OmenGfxMode.Optimus:
          return "Optimus";
        default:
          return "Unknown";
      }
    }

    private static string FormatAdapterStatus(OmenHardware.OmenSmartAdapterStatus status) {
      switch (status) {
        case OmenHardware.OmenSmartAdapterStatus.MeetsRequirement:
          return "适配器正常";
        case OmenHardware.OmenSmartAdapterStatus.BatteryPower:
          return "电池供电";
        case OmenHardware.OmenSmartAdapterStatus.BelowRequirement:
          return "适配器不足";
        case OmenHardware.OmenSmartAdapterStatus.NotFunctioning:
          return "适配器异常";
        case OmenHardware.OmenSmartAdapterStatus.NoSupport:
          return "不支持";
        default:
          return "未知";
      }
    }

    private static string BuildSubtitle(Program.DashboardSnapshot snapshot, float? batteryPower) {
      string source = snapshot.AcOnline ? "交流电" : "电池";
      string total = batteryPower.HasValue ? $"{batteryPower.Value:F1}W" : "n/a";
      return $"供电来源 {source} | CPU {snapshot.CpuPowerWatts:F1}W | GPU {(snapshot.MonitorGpu ? snapshot.GpuPowerWatts.ToString("F1") : "--")}W | 电池功率 {total}";
    }

    private static string BuildConfigText(Program.DashboardSnapshot snapshot, float? batteryPower) {
      return string.Join(Environment.NewLine, new[] {
        $"CPU 温度      : {snapshot.CpuTemperature:F1} °C",
        $"CPU 功率      : {snapshot.CpuPowerWatts:F1} W",
        $"GPU 温度      : {(snapshot.MonitorGpu ? snapshot.GpuTemperature.ToString("F1") + " °C" : "关闭监控")}",
        $"GPU 功率      : {(snapshot.MonitorGpu ? snapshot.GpuPowerWatts.ToString("F1") + " W" : "--")}",
        $"电池功率      : {(batteryPower.HasValue ? batteryPower.Value.ToString("F1") + " W" : "--")}",
        $"风扇控制      : {snapshot.FanControl}",
        $"风扇曲线      : {snapshot.FanTable}",
        $"性能模式      : {snapshot.FanMode}",
        $"CPU 限功      : {snapshot.CpuPowerSetting}",
        $"GPU 策略      : {snapshot.GpuPowerSetting}",
        $"GPU 锁频      : {(snapshot.GpuClockLimit > 0 ? snapshot.GpuClockLimit + " MHz" : "还原")}",
        $"风扇实时      : {(snapshot.MonitorFan ? snapshot.FanSpeeds[0] * 100 + " / " + snapshot.FanSpeeds[1] * 100 + " RPM" : "关闭监控")}"
      });
    }

    private static string BuildCapabilityText(Program.DashboardSnapshot snapshot) {
      string gpuControl = snapshot.GpuStatus == null
        ? "Unknown"
        : $"{(snapshot.GpuStatus.CustomTgpEnabled ? "cTGP" : "BaseTGP")} | {(snapshot.GpuStatus.PpabEnabled ? "PPAB" : "NoPPAB")} | D{snapshot.GpuStatus.DState}";

      string designFlags = snapshot.SystemDesignData == null
        ? "未知"
        : string.Join(" | ", new[] {
          snapshot.SystemDesignData.GraphicsSwitcherSupported ? "GfxSwitch" : "No GfxSwitch",
          snapshot.SystemDesignData.SoftwareFanControlSupported ? "SW Fan" : "BIOS Fan",
          $"PL4 {snapshot.SystemDesignData.DefaultPl4}W"
        });

      string batteryDetails = snapshot.Battery == null
        ? "电池遥测不可用"
        : string.Join(" | ", new[] {
          snapshot.Battery.Discharging ? $"放电 {snapshot.Battery.DischargeRateMilliwatts / 1000f:F1}W" : (snapshot.Battery.Charging ? $"充电 {snapshot.Battery.ChargeRateMilliwatts / 1000f:F1}W" : "待机"),
          $"容量 {snapshot.Battery.RemainingCapacityMilliwattHours / 1000f:F1}Wh",
          $"电压 {snapshot.Battery.VoltageMillivolts / 1000f:F2}V",
          $"电量 {snapshot.BatteryPercent}%"
        });

      return string.Join(Environment.NewLine, new[] {
        $"显卡模式      : {FormatGraphicsMode(snapshot.GraphicsMode)}",
        $"GPU 控制      : {gpuControl}",
        $"适配器状态    : {FormatAdapterStatus(snapshot.SmartAdapterStatus)}",
        $"风扇类型      : {(snapshot.FanTypeInfo == null ? "--" : snapshot.FanTypeInfo.Fan1Type + "/" + snapshot.FanTypeInfo.Fan2Type)}",
        $"键盘类型      : {(byte)snapshot.KeyboardType:X2}",
        $"平台能力      : {designFlags}",
        $"电池遥测      : {batteryDetails}"
      });
    }

    private void MainForm_FormClosing(object sender, FormClosingEventArgs e) {
      if (e.CloseReason == CloseReason.UserClosing) {
        e.Cancel = true;
        Hide();
      }
    }

    public static MainForm Instance {
      get {
        if (_instance == null || _instance.IsDisposed) {
          _instance = new MainForm();
        }
        return _instance;
      }
    }
  }
}
