using System.Windows.Forms;

namespace OmenSuperHub {
  internal sealed class AppApplicationContext : ApplicationContext {
    readonly AppRuntime runtime;

    public AppApplicationContext(AppRuntime runtime, string[] args) {
      this.runtime = runtime;
      if (!runtime.TryStart(args)) {
        ExitThread();
      }
    }

    protected override void ExitThreadCore() {
      runtime.Stop();
      base.ExitThreadCore();
    }
  }
}
