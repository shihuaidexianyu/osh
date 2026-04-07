using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
using System.IO.Pipes;
using TaskEx = System.Threading.Tasks.Task;

namespace OmenSuperHub {
  internal sealed partial class AppRuntime {
    void StartTimers() {
      backgroundScheduler = new AppBackgroundScheduler(
        optimiseSchedule,
        HardwarePollingTick,
        FanControlTick,
        HandleFloatingBarToggle);
      backgroundScheduler.Start();
    }

    void StartFloatingToggleTimer() {
      backgroundScheduler?.SetFloatingToggleEnabled(true);
    }

    void ReleaseSingleInstanceMutex() {
      if (singleInstanceMutex == null) {
        if (ReferenceEquals(currentInstance, this)) {
          currentInstance = null;
        }
        return;
      }

      try {
        singleInstanceMutex.ReleaseMutex();
      } catch (ApplicationException) {
      } finally {
        singleInstanceMutex.Dispose();
        singleInstanceMutex = null;
        if (ReferenceEquals(currentInstance, this)) {
          currentInstance = null;
        }
      }
    }

    public bool TryStart(string[] args) {
      bool isNewInstance;
      singleInstanceMutex = new Mutex(true, "MyUniqueAppMutex", out isNewInstance);
      if (!isNewInstance) {
        singleInstanceMutex.Dispose();
        singleInstanceMutex = null;
        return false;
      }

      if (Environment.OSVersion.Version.Major >= 6) {
        SetProcessDPIAware();
      }

      AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);
      Application.ThreadException += new ThreadExceptionEventHandler(Application_ThreadException);
      Application.ApplicationExit += new EventHandler(OnApplicationExit);
      currentInstance = this;

      Application.EnableVisualStyles();
      Application.SetCompatibleTextRenderingDefault(false);
      MainForm.Initialize(this);

      powerOnline = SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Online;
      Version version = Assembly.GetExecutingAssembly().GetName().Version;
      string versionString = version.ToString().Replace(".", "");
      alreadyReadCode = new Random(int.Parse(versionString)).Next(1000, 10000);

      InitTrayIcon();

      libreComputer.Open();
      StartTimers();
      getOmenKeyTask();
      StartFloatingToggleTimer();

      RestoreConfig();
      HandleFirstRunPrompt();

      SystemEvents.PowerModeChanged += new PowerModeChangedEventHandler(OnPowerChange);
      return true;
    }

    public void Stop() {
      ReleaseSingleInstanceMutex();
    }

    static void getOmenKeyTask() {
      omenKeyListenerTask = TaskEx.Run(() => {
        while (!isShuttingDown) {
          try {
            using (var pipeServer = new NamedPipeServerStream("OmenSuperHubPipe", PipeDirection.In)) {
              omenKeyPipeServer = pipeServer;
              pipeServer.WaitForConnection();
              if (isShuttingDown) {
                break;
              }

              using (var reader = new StreamReader(pipeServer)) {
                string message = reader.ReadToEnd();
                if (!string.IsNullOrEmpty(message)) {
                  if (message.Contains("OmenKeyTriggered")) {
                    checkFloating = true;
                  } else if (message.Contains("OmenKeyShowMainWindow")) {
                    checkShowMainWindow = true;
                  }
                }
              }
            }
          } catch (ObjectDisposedException) {
            break;
          } catch (IOException) {
            if (isShuttingDown) {
              break;
            }
          } finally {
            omenKeyPipeServer = null;
          }
        }
      });
    }

    static void Exit() {
      if (Interlocked.Exchange(ref shutdownStarted, 1) != 0) {
        return;
      }

      isShuttingDown = true;
      if (omenKey == "custom") {
        hardwareControlService.DisableOmenKey();
      }

      SystemEvents.PowerModeChanged -= new PowerModeChangedEventHandler(OnPowerChange);
      StopAndDisposeTimers();
      DisposePipeServer();
      shellService.Dispose();
      libreComputer.Close();
      Application.Exit();
    }

    static void OnApplicationExit(object sender, EventArgs e) {
      if (Interlocked.Exchange(ref shutdownStarted, 1) != 0) {
        currentInstance?.ReleaseSingleInstanceMutex();
        return;
      }

      isShuttingDown = true;
      SystemEvents.PowerModeChanged -= new PowerModeChangedEventHandler(OnPowerChange);
      StopAndDisposeTimers();
      DisposePipeServer();
      shellService.Dispose();

      libreComputer.Close();
      currentInstance?.ReleaseSingleInstanceMutex();
    }

    static void StopAndDisposeTimers() {
      if (backgroundScheduler != null) {
        backgroundScheduler.Dispose();
        backgroundScheduler = null;
      }
    }

    static void DisposePipeServer() {
      var pipeServer = omenKeyPipeServer;
      omenKeyPipeServer = null;
      if (pipeServer != null) {
        pipeServer.Dispose();
      }
    }

    static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e) {
      Exception ex = (Exception)e.ExceptionObject;
      errorLogService.ReportFatal(ex, isShuttingDown);
    }

    static void Application_ThreadException(object sender, ThreadExceptionEventArgs e) {
      Exception ex = e.Exception;
      errorLogService.ReportFatal(ex, isShuttingDown);
    }
  }
}
