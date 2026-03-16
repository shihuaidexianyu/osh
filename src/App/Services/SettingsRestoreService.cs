using System.Collections.Generic;

namespace OmenSuperHub {
  internal sealed class CheckedMenuSelection {
    public string Group { get; set; }
    public string ItemText { get; set; }
  }

  internal sealed class SettingsRestorePlan {
    public string UsageMode { get; set; } = RuntimeControlSettings.ToStorageValue(UsageModePreset.Balanced);
    public RuntimeControlSettings ControlSettings { get; set; } = RuntimeControlSettings.CreatePreset(UsageModePreset.Balanced);
    public string AutoStart { get; set; } = "off";
    public int AlreadyRead { get; set; }
    public string CustomIcon { get; set; } = "original";
    public string OmenKey { get; set; } = "default";
    public bool MonitorFan { get; set; } = true;
    public int FloatingBarSize { get; set; } = 48;
    public string FloatingBarLocation { get; set; } = "left";
    public string FloatingBar { get; set; } = "off";
    public List<CheckedMenuSelection> CheckedMenuSelections { get; } = new List<CheckedMenuSelection>();

    public bool EnableAutoStart => AutoStart == "on";
  }

  internal sealed class SettingsRestoreService {
    readonly AppSettingsService settingsService;

    public SettingsRestoreService(AppSettingsService settingsService) {
      this.settingsService = settingsService;
    }

    public bool TryLoadRestorePlan(out SettingsRestorePlan plan) {
      plan = null;
      if (!settingsService.TryLoadConfig(out AppSettingsSnapshot snapshot)) {
        return false;
      }

      plan = BuildPlan(snapshot);
      return true;
    }

    public SettingsRestorePlan BuildPlan(AppSettingsSnapshot snapshot) {
      RuntimeControlSettings controlSettings = RuntimeControlSettings.FromSnapshot(snapshot);
      var plan = new SettingsRestorePlan {
        UsageMode = RuntimeControlSettings.ToStorageValue(RuntimeControlSettings.ParseUsageMode(snapshot?.UsageMode)),
        ControlSettings = controlSettings,
        AutoStart = NormalizeAutoStart(snapshot?.AutoStart),
        AlreadyRead = snapshot?.AlreadyRead ?? 0,
        CustomIcon = NormalizeCustomIcon(snapshot?.CustomIcon),
        OmenKey = NormalizeOmenKey(snapshot?.OmenKey),
        MonitorFan = snapshot?.MonitorFan ?? true,
        FloatingBarSize = NormalizeFloatingBarSize(snapshot?.FloatingBarSize ?? 48),
        FloatingBarLocation = NormalizeFloatingBarLocation(snapshot?.FloatingBarLocation),
        FloatingBar = NormalizeFloatingBar(snapshot?.FloatingBar)
      };

      AddSelection(plan, "fanTableGroup", GetFanTableMenuText(controlSettings.FanTable));
      AddSelection(plan, "fanModeGroup", GetFanModeMenuText(controlSettings.FanMode));
      AddSelection(plan, "fanControlGroup", GetFanControlMenuText(controlSettings.FanControl, controlSettings.ManualFanRpm));
      AddSelection(plan, "tempSensitivityGroup", GetTempSensitivityMenuText(controlSettings.TempSensitivity));
      AddSelection(plan, "cpuPowerGroup", GetCpuPowerMenuText(controlSettings.CpuPowerMax, controlSettings.CpuPowerWatts));
      AddSelection(plan, "gpuPowerGroup", GetGpuPowerMenuText(controlSettings.GpuPower));
      AddSelection(plan, "gpuClockGroup", GetGpuClockMenuText(controlSettings.GpuClockLimitMhz));
      AddSelection(plan, "autoStartGroup", plan.EnableAutoStart ? "开启" : "关闭");
      AddSelection(plan, "customIconGroup", GetCustomIconMenuText(plan.CustomIcon));
      AddSelection(plan, "omenKeyGroup", GetOmenKeyMenuText(plan.OmenKey));
      AddSelection(plan, "monitorFanGroup", plan.MonitorFan ? "开启风扇监控" : "关闭风扇监控");
      AddSelection(plan, "floatingBarSizeGroup", GetFloatingBarSizeMenuText(plan.FloatingBarSize));
      AddSelection(plan, "floatingBarLocGroup", plan.FloatingBarLocation == "left" ? "左上角" : "右上角");
      AddSelection(plan, "floatingBarGroup", plan.FloatingBar == "on" ? "显示浮窗" : "关闭浮窗");
      return plan;
    }

    static void AddSelection(SettingsRestorePlan plan, string group, string itemText) {
      if (plan == null || string.IsNullOrWhiteSpace(group) || string.IsNullOrWhiteSpace(itemText)) {
        return;
      }

      plan.CheckedMenuSelections.Add(new CheckedMenuSelection {
        Group = group,
        ItemText = itemText
      });
    }

    static string GetFanTableMenuText(FanTableOption value) {
      return value == FanTableOption.Cool ? "降温模式" : "安静模式";
    }

    static string GetFanModeMenuText(FanModeOption value) {
      return value == FanModeOption.Performance ? "性能模式" : "均衡模式";
    }

    static string GetFanControlMenuText(FanControlOption value, int manualFanRpm) {
      switch (value) {
        case FanControlOption.Max:
          return "最大风扇";
        case FanControlOption.Manual:
          return RuntimeControlSettings.ToStorageValue(FanControlOption.Manual, manualFanRpm);
        default:
          return "自动";
      }
    }

    static string GetTempSensitivityMenuText(TempSensitivityOption value) {
      switch (value) {
        case TempSensitivityOption.Realtime:
          return "实时";
        case TempSensitivityOption.High:
          return "高";
        case TempSensitivityOption.Low:
          return "低";
        default:
          return "中";
      }
    }

    static string GetCpuPowerMenuText(bool isMax, int watts) {
      return isMax ? "最大" : RuntimeControlSettings.ToCpuPowerStorageValue(false, watts);
    }

    static string GetGpuPowerMenuText(GpuPowerOption value) {
      switch (value) {
        case GpuPowerOption.Max:
          return "高性能";
        case GpuPowerOption.Min:
          return "节能";
        default:
          return "均衡";
      }
    }

    static string GetGpuClockMenuText(int gpuClockLimitMhz) {
      return gpuClockLimitMhz >= 210 ? gpuClockLimitMhz + " MHz" : "还原";
    }

    static string GetCustomIconMenuText(string value) {
      switch (value) {
        case "custom":
          return "自定义图标";
        case "dynamic":
          return "动态图标";
        default:
          return "原版";
      }
    }

    static string GetOmenKeyMenuText(string value) {
      switch (value) {
        case "custom":
          return "切换浮窗显示";
        case "none":
          return "取消绑定";
        default:
          return "默认";
      }
    }

    static string GetFloatingBarSizeMenuText(int value) {
      switch (value) {
        case 24:
          return "24号";
        case 36:
          return "36号";
        default:
          return "48号";
      }
    }

    static string NormalizeAutoStart(string value) {
      return value == "on" ? "on" : "off";
    }

    static string NormalizeCustomIcon(string value) {
      switch (value) {
        case "custom":
        case "dynamic":
          return value;
        default:
          return "original";
      }
    }

    static string NormalizeOmenKey(string value) {
      switch (value) {
        case "custom":
        case "none":
          return value;
        default:
          return "default";
      }
    }

    static int NormalizeFloatingBarSize(int value) {
      switch (value) {
        case 24:
        case 36:
          return value;
        default:
          return 48;
      }
    }

    static string NormalizeFloatingBarLocation(string value) {
      return value == "right" ? "right" : "left";
    }

    static string NormalizeFloatingBar(string value) {
      return value == "on" ? "on" : "off";
    }
  }
}
