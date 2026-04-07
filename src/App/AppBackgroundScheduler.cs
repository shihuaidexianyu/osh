using System;
using System.Threading;

namespace OmenSuperHub {
  internal sealed class AppBackgroundScheduler : IDisposable {
    readonly Action optimizeAction;
    readonly Action hardwarePollingAction;
    readonly Action fanControlAction;

    Timer optimiseTimer;
    Timer hardwarePollingTimer;
    Timer fanControlTimer;

    public AppBackgroundScheduler(Action optimizeAction, Action hardwarePollingAction, Action fanControlAction) {
      this.optimizeAction = optimizeAction;
      this.hardwarePollingAction = hardwarePollingAction;
      this.fanControlAction = fanControlAction;
    }

    public void Start() {
      optimiseTimer = new Timer(_ => optimizeAction?.Invoke(), null, 0, 30000);
      hardwarePollingTimer = new Timer(_ => hardwarePollingAction?.Invoke(), null, 100, 1000);
      fanControlTimer = new Timer(_ => fanControlAction?.Invoke(), null, 100, 1000);
    }

    public void SetFanControlLoopEnabled(bool enabled) {
      if (fanControlTimer == null) {
        return;
      }

      fanControlTimer.Change(enabled ? 0 : Timeout.Infinite, enabled ? 1000 : Timeout.Infinite);
    }

    public void Dispose() {
      if (hardwarePollingTimer != null) {
        hardwarePollingTimer.Dispose();
        hardwarePollingTimer = null;
      }

      if (optimiseTimer != null) {
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
