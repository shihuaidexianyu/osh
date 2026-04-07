using System;
using System.IO;
using Microsoft.Win32.TaskScheduler;

namespace OmenSuperHub {
  internal sealed class StartupTaskService {
    readonly ProcessCommandService processCommandService;

    public StartupTaskService(ProcessCommandService processCommandService) {
      this.processCommandService = processCommandService;
    }

    public void EnableAutoStart(string baseDirectory) {
      string currentPath = string.IsNullOrWhiteSpace(baseDirectory)
        ? AppDomain.CurrentDomain.BaseDirectory
        : baseDirectory;

      using (TaskService ts = new TaskService()) {
        TaskDefinition td = ts.NewTask();
        td.RegistrationInfo.Description = "Start OmenSuperHub with admin rights";
        td.Principal.RunLevel = TaskRunLevel.Highest;
        td.Actions.Add(new ExecAction(Path.Combine(currentPath, "OmenSuperHub.exe"), null, null));

        LogonTrigger logonTrigger = new LogonTrigger();
        td.Triggers.Add(logonTrigger);

        td.Settings.DisallowStartIfOnBatteries = false;
        td.Settings.StopIfGoingOnBatteries = false;
        td.Settings.ExecutionTimeLimit = TimeSpan.Zero;
        td.Settings.AllowHardTerminate = false;

        ts.RootFolder.RegisterTaskDefinition(@"OmenSuperHub", td);
        Console.WriteLine("任务已创建。");
      }

      CleanLegacyArtifacts();
    }

    public void DisableAutoStart() {
      using (TaskService ts = new TaskService()) {
        Microsoft.Win32.TaskScheduler.Task existingTask = ts.FindTask("OmenSuperHub");

        if (existingTask != null) {
          ts.RootFolder.DeleteTask("OmenSuperHub");
          Console.WriteLine("任务已删除。");
        } else {
          Console.WriteLine("任务不存在，无需删除。");
        }
      }
    }

    public void CleanLegacyArtifacts() {
      string legacyFolder = @"C:\Program Files\OmenSuperHub";
      string legacyTaskName = "Omen Boot";
      string legacyRunRegDelete = @"reg delete ""HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run"" /v ""OmenSuperHub"" /f";

      if (Directory.Exists(legacyFolder)) {
        ProcessResult removeFolderResult = processCommandService.Execute($"rd /s /q \"{legacyFolder}\"");
        if (!string.IsNullOrWhiteSpace(removeFolderResult.Output)) {
          Console.WriteLine(removeFolderResult.Output);
        }
      } else {
        Console.WriteLine("旧文件夹不存在");
      }

      ProcessResult taskQueryResult = processCommandService.Execute($"schtasks /query /tn \"{legacyTaskName}\"");
      if (taskQueryResult.ExitCode == 0) {
        ProcessResult deleteTaskResult = processCommandService.Execute($"schtasks /delete /tn \"{legacyTaskName}\" /f");
        Console.WriteLine("已成功删除计划任务 \"Omen Boot\"。");
        if (!string.IsNullOrWhiteSpace(deleteTaskResult.Output)) {
          Console.WriteLine(deleteTaskResult.Output);
        }
      } else {
        Console.WriteLine($"计划任务 \"{legacyTaskName}\" 不存在。");
      }

      ProcessResult regDeleteResult = processCommandService.Execute(legacyRunRegDelete);
      Console.WriteLine("成功取消开机自启");
      if (!string.IsNullOrWhiteSpace(regDeleteResult.Output)) {
        Console.WriteLine(regDeleteResult.Output);
      }
    }
  }
}
