using System;
using System.IO;

namespace OmenSuperHub {
  internal sealed class AppErrorLogService {
    readonly string logDirectory;

    public AppErrorLogService(string baseDirectory = null) {
      logDirectory = string.IsNullOrWhiteSpace(baseDirectory)
        ? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "osh",
            "logs")
        : baseDirectory;
    }

    public void Write(Exception ex, string context = null) {
      if (ex == null) {
        return;
      }

      try {
        Directory.CreateDirectory(logDirectory);
        string absoluteFilePath = Path.Combine(logDirectory, "error.log");
        string prefix = string.IsNullOrWhiteSpace(context) ? string.Empty : $"[{context}] ";
        File.AppendAllText(absoluteFilePath, DateTime.Now + ": " + prefix + ex + Environment.NewLine);
      } catch {
      }
    }

    public void ReportFatal(Exception ex, bool isShuttingDown) {
      Write(ex);

      if (!isShuttingDown) {
        Console.WriteLine("An unexpected error occurred. Please check the log file for details.");
      }
    }
  }
}
