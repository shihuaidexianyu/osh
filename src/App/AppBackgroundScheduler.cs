using System;
using System.Windows.Forms;

namespace OmenSuperHub {
  internal sealed class AppBackgroundScheduler : IDisposable {
    readonly Action optimizeAction;
    readonly Action hardwarePollingAction;
    readonly Action fanControlAction;
    readonly Action floatingToggleAction;

    System.Threading.Timer fanControlTimer;
    System.Threading.Timer hardwarePollingTimer;
    System.Windows.Forms.Timer optimiseTimer;
    System.Windows.Forms.Timer floatingToggleTimer;

    public AppBackgroundScheduler(
      Action optimizeAction,
      Action hardwarePollingAction,
      Action fanControlAction,
      Action floatingToggleAction) {
      this.optimizeAction = optimizeAction;
      this.hardwarePollingAction = hardwarePollingAction;
      this.fanControlAction = fanControlAction;
      this.floatingToggleAction = floatingToggleAction;
    }

    public void Start() {
      optimiseTimer = new System.Windows.Forms.Timer();
      optimiseTimer.Interval = 30000;
      optimiseTimer.Tick += (s, e) => optimizeAction?.Invoke();
      optimiseTimer.Start();

      hardwarePollingTimer = new System.Threading.Timer(_ => hardwarePollingAction?.Invoke(), null, 100, 1000);
      fanControlTimer = new System.Threading.Timer(_ => fanControlAction?.Invoke(), null, 100, 1000);

      floatingToggleTimer = new System.Windows.Forms.Timer();
      floatingToggleTimer.Interval = 100;
      floatingToggleTimer.Tick += (s, e) => floatingToggleAction?.Invoke();
      floatingToggleTimer.Start();
    }

    public void SetFanControlLoopEnabled(bool enabled) {
      if (fanControlTimer == null) {
        return;
      }

      fanControlTimer.Change(enabled ? 0 : System.Threading.Timeout.Infinite, enabled ? 1000 : System.Threading.Timeout.Infinite);
    }

    public void SetFloatingToggleEnabled(bool enabled) {
      if (floatingToggleTimer == null) {
        return;
      }

      floatingToggleTimer.Enabled = enabled;
    }

    public void Dispose() {
      if (hardwarePollingTimer != null) {
        hardwarePollingTimer.Dispose();
        hardwarePollingTimer = null;
      }

      if (floatingToggleTimer != null) {
        floatingToggleTimer.Stop();
        floatingToggleTimer.Dispose();
        floatingToggleTimer = null;
      }

      if (optimiseTimer != null) {
        optimiseTimer.Stop();
        optimiseTimer.Dispose();
        optimiseTimer = null;
      }

      if (fanControlTimer != null) {
        fanControlTimer.Dispose();
        fanControlTimer = null;
      }
    }
  }
}
