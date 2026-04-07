using System;
using System.Windows.Forms;

namespace OmenSuperHub {
  internal sealed partial class AppRuntime {
    static void InitTrayIcon() {
      try {
        AppSettingsSnapshot snapshot;
        if (settingsService.TryLoadConfig(out snapshot)) {
          customIcon = snapshot.CustomIcon;
          if (customIcon == "custom" && !shellService.HasCustomIconFile(AppDomain.CurrentDomain.BaseDirectory)) {
            customIcon = "original";
            SaveConfig("CustomIcon");
            UpdateCheckedState("CustomIcon", "原版");
          }
        }
      } catch (Exception ex) {
        Console.WriteLine($"Error restoring configuration: {ex.Message}");
      }

      shellService.Initialize(OnShellTick, ShowMainWindow, Exit);
      RefreshShellStatus();
    }

    static bool CheckCustomIcon() {
      if (shellService.HasCustomIconFile(AppDomain.CurrentDomain.BaseDirectory)) {
        return true;
      }
      MessageBox.Show("不存在自定义图标custom.ico", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
      return false;
    }

    static void UpdateCheckedState(string group, string itemText = null, ToolStripMenuItem menuItemToCheck = null) {
      shellService.UpdateCheckedState(group, itemText, menuItemToCheck);
    }

    static void OnShellTick() {
      if (isShuttingDown) {
        return;
      }

      if (checkShowMainWindow) {
        checkShowMainWindow = false;
        ShowMainWindow();
      }

      RefreshShellStatus();

      if (countRestore > 0) {
        countRestore--;
        if (countRestore == 0) {
          RestoreConfig();
        }
      }
    }

    static void ShowMainWindow() {
      MainForm.Instance.Show();
      MainForm.Instance.WindowState = FormWindowState.Normal;
      MainForm.Instance.BringToFront();
      MainForm.Instance.Activate();
    }

    static void HandleFloatingBarToggle() {
      if (isShuttingDown) {
        return;
      }

      if (checkFloating) {
        checkFloating = false;
        if (floatingBar == "on") {
          floatingBar = "off";
          UpdateCheckedState("floatingBarGroup", "关闭浮窗");
        } else {
          floatingBar = "on";
          UpdateCheckedState("floatingBarGroup", "显示浮窗");
        }
        RefreshShellStatus();
        SaveConfig("FloatingBar");
      }
    }
  }
}
