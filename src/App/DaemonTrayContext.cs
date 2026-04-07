using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace OmenSuperHub {
  internal sealed class DaemonTrayContext : ApplicationContext {
    readonly AppRuntime runtime;
    readonly NotifyIcon trayIcon;
    readonly ContextMenuStrip menu;

    public DaemonTrayContext(AppRuntime runtime) {
      this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));

      menu = new ContextMenuStrip();
      menu.Items.Add("osh daemon 运行中").Enabled = false;
      menu.Items.Add(new ToolStripSeparator());
      menu.Items.Add("退出", null, OnExitClicked);

      trayIcon = new NotifyIcon {
        Text = "osh daemon 运行中",
        Icon = LoadTrayIcon(),
        ContextMenuStrip = menu,
        Visible = true
      };
      trayIcon.DoubleClick += OnTrayIconDoubleClick;
      trayIcon.BalloonTipTitle = "osh";
      trayIcon.BalloonTipText = "后台调度已启动，右键托盘图标可退出。";
      trayIcon.ShowBalloonTip(1500);

      Application.ApplicationExit += OnApplicationExit;
    }

    void OnTrayIconDoubleClick(object sender, EventArgs e) {
      trayIcon.ShowBalloonTip(1200, "osh", "后台调度正在运行。", ToolTipIcon.Info);
    }

    void OnExitClicked(object sender, EventArgs e) {
      ExitThread();
    }

    protected override void ExitThreadCore() {
      ShutdownRuntime();

      trayIcon.Visible = false;
      trayIcon.DoubleClick -= OnTrayIconDoubleClick;
      trayIcon.Dispose();
      menu.Dispose();
      Application.ApplicationExit -= OnApplicationExit;

      base.ExitThreadCore();
    }

    void OnApplicationExit(object sender, EventArgs e) {
      ShutdownRuntime();
    }

    void ShutdownRuntime() {
      try {
        runtime.Stop();
      } catch {
      }
    }

    static Icon LoadTrayIcon() {
      try {
        string exePath = Process.GetCurrentProcess().MainModule?.FileName;
        if (!string.IsNullOrWhiteSpace(exePath)) {
          string iconPath = Path.Combine(Path.GetDirectoryName(exePath), "Resources", "fan.ico");
          if (File.Exists(iconPath)) {
            return new Icon(iconPath);
          }

          Icon extracted = Icon.ExtractAssociatedIcon(exePath);
          if (extracted != null) {
            return extracted;
          }
        }
      } catch {
      }

      return SystemIcons.Application;
    }
  }
}
