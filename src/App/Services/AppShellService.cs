using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace OmenSuperHub {
  internal sealed class AppShellStatus {
    public string IconMode { get; set; }
    public string TrayText { get; set; }
    public int DynamicIconValue { get; set; }
    public string CustomIconPath { get; set; }
    public bool FloatingVisible { get; set; }
    public string FloatingText { get; set; }
    public int FloatingTextSize { get; set; }
    public string FloatingLocation { get; set; }
    public bool MainWindowVisible { get; set; }
  }

  internal sealed class AppShellService : IDisposable {
    static readonly Icon defaultTrayIcon = SystemIcons.Application;
    NotifyIcon trayIcon;
    System.Windows.Forms.Timer tooltipTimer;
    FloatingForm floatingForm;
    readonly object floatingFormLock = new object();
    Icon ownedTrayIcon;

    public void Initialize(Action onTick, Action onShowMainWindow, Action onExit) {
      if (trayIcon != null) {
        return;
      }

      trayIcon = new NotifyIcon {
        Icon = defaultTrayIcon,
        ContextMenuStrip = new ContextMenuStrip(),
        Visible = true
      };
      trayIcon.MouseClick += (s, e) => {
        if (e.Button == MouseButtons.Left) {
          onShowMainWindow?.Invoke();
        }
      };
      trayIcon.ContextMenuStrip.Items.Add(new ToolStripMenuItem("退出", null, (s, e) => onExit?.Invoke()));

      tooltipTimer = new System.Windows.Forms.Timer {
        Interval = 1000
      };
      tooltipTimer.Tick += (s, e) => onTick?.Invoke();
      tooltipTimer.Start();
    }

    public bool HasCustomIconFile(string baseDirectory) {
      return File.Exists(GetCustomIconPath(baseDirectory));
    }

    public void RefreshStatus(AppShellStatus status) {
      if (status == null) {
        return;
      }

      if (trayIcon != null) {
        trayIcon.Text = LimitTrayText(status.TrayText);
        ApplyIconMode(status);
      }

      if (status.FloatingVisible) {
        ShowOrUpdateFloating(status);
      } else {
        HideFloating();
      }
    }

    public void UpdateCheckedState(string group, string itemText = null, ToolStripMenuItem menuItemToCheck = null) {
      if (trayIcon == null || trayIcon.ContextMenuStrip == null) {
        return;
      }

      if (menuItemToCheck == null) {
        menuItemToCheck = FindMenuItem(trayIcon.ContextMenuStrip.Items, itemText);
        if (menuItemToCheck == null) {
          return;
        }
      }

      UpdateMenuItemsCheckedState(trayIcon.ContextMenuStrip.Items, group, menuItemToCheck);
    }

    public void Dispose() {
      HideFloating();
      if (tooltipTimer != null) {
        tooltipTimer.Stop();
        tooltipTimer.Dispose();
        tooltipTimer = null;
      }

      ReleaseOwnedTrayIcon();
      if (trayIcon != null) {
        trayIcon.Visible = false;
        trayIcon.Dispose();
        trayIcon = null;
      }
    }

    static string GetCustomIconPath(string baseDirectory) {
      return Path.Combine(baseDirectory, "custom.ico");
    }

    void ApplyIconMode(AppShellStatus status) {
      switch (status.IconMode) {
        case "custom":
          if (!string.IsNullOrWhiteSpace(status.CustomIconPath) && File.Exists(status.CustomIconPath)) {
            SetTrayIcon(new Icon(status.CustomIconPath), true);
          } else {
            SetTrayIcon(defaultTrayIcon, false);
          }
          break;
        case "dynamic":
          SetTrayIcon(CreateDynamicIcon(status.DynamicIconValue), true);
          break;
        default:
          SetTrayIcon(defaultTrayIcon, false);
          break;
      }
    }

    void ShowOrUpdateFloating(AppShellStatus status) {
      lock (floatingFormLock) {
        if (floatingForm == null || floatingForm.IsDisposed) {
          floatingForm = new FloatingForm(status.FloatingText ?? string.Empty, status.FloatingTextSize, status.FloatingLocation);
          floatingForm.TopMost = !status.MainWindowVisible;
          floatingForm.Show();
          return;
        }

        bool shouldTopMost = !status.MainWindowVisible;
        if (floatingForm.TopMost != shouldTopMost) {
          floatingForm.TopMost = shouldTopMost;
        }

        if (status.MainWindowVisible) {
          return;
        }

        floatingForm.SetText(status.FloatingText ?? string.Empty, status.FloatingTextSize, status.FloatingLocation);
      }
    }

    void HideFloating() {
      lock (floatingFormLock) {
        if (floatingForm != null && !floatingForm.IsDisposed) {
          floatingForm.Close();
          floatingForm.Dispose();
        }
        floatingForm = null;
      }
    }

    void SetTrayIcon(Icon icon, bool ownsIcon) {
      if (trayIcon == null || icon == null) {
        return;
      }

      if (!ReferenceEquals(icon, ownedTrayIcon) && !ReferenceEquals(icon, defaultTrayIcon)) {
        ReleaseOwnedTrayIcon();
      }

      trayIcon.Icon = icon;
      ownedTrayIcon = ownsIcon ? icon : null;
    }

    void ReleaseOwnedTrayIcon() {
      if (ownedTrayIcon != null) {
        ownedTrayIcon.Dispose();
        ownedTrayIcon = null;
      }
    }

    static void UpdateMenuItemsCheckedState(ToolStripItemCollection items, string group, ToolStripMenuItem clicked) {
      foreach (ToolStripMenuItem menuItem in items.OfType<ToolStripMenuItem>()) {
        if (menuItem.Tag as string == group) {
          menuItem.Checked = menuItem == clicked;
        }
        if (menuItem.HasDropDownItems) {
          UpdateMenuItemsCheckedState(menuItem.DropDownItems, group, clicked);
        }
      }
    }

    static ToolStripMenuItem FindMenuItem(ToolStripItemCollection items, string itemText) {
      foreach (ToolStripMenuItem menuItem in items.OfType<ToolStripMenuItem>()) {
        if (menuItem.Text == itemText) {
          return menuItem;
        }

        if (menuItem.HasDropDownItems) {
          var foundItem = FindMenuItem(menuItem.DropDownItems, itemText);
          if (foundItem != null) {
            return foundItem;
          }
        }
      }
      return null;
    }

    static string LimitTrayText(string text) {
      string value = string.IsNullOrWhiteSpace(text) ? "OmenSuperHub" : text;
      return value.Length <= 63 ? value : value.Substring(0, 63);
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    static extern bool DestroyIcon(IntPtr handle);

    static Icon CreateDynamicIcon(int number) {
      using (Bitmap bitmap = new Bitmap(128, 128)) {
        using (Graphics graphics = Graphics.FromImage(bitmap)) {
          graphics.Clear(Color.Transparent);
          graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

          string text = number.ToString("00");
          using (Font font = new Font("Arial", 52, FontStyle.Bold)) {
            SizeF textSize = graphics.MeasureString(text, font);
            float x = (bitmap.Width - textSize.Width) / 2;
            float y = (bitmap.Height - textSize.Height) / 8;
            graphics.DrawString(text, font, Brushes.Tan, new PointF(x, y));
          }

          IntPtr hIcon = bitmap.GetHicon();
          try {
            using (Icon temp = Icon.FromHandle(hIcon)) {
              return (Icon)temp.Clone();
            }
          } finally {
            DestroyIcon(hIcon);
          }
        }
      }
    }
  }
}
