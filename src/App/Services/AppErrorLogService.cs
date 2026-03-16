using System;
using System.IO;
using System.Windows.Forms;

namespace OmenSuperHub {
  internal sealed class AppErrorLogService {
    readonly string baseDirectory;

    public AppErrorLogService(string baseDirectory) {
      this.baseDirectory = string.IsNullOrWhiteSpace(baseDirectory)
        ? AppDomain.CurrentDomain.BaseDirectory
        : baseDirectory;
    }

    public void Write(Exception ex, string context = null) {
      if (ex == null) {
        return;
      }

      try {
        string absoluteFilePath = Path.Combine(baseDirectory, "error.log");
        string prefix = string.IsNullOrWhiteSpace(context) ? string.Empty : $"[{context}] ";
        File.AppendAllText(absoluteFilePath, DateTime.Now + ": " + prefix + ex + Environment.NewLine);
      } catch {
      }
    }

    public void ReportFatal(Exception ex, bool isShuttingDown) {
      Write(ex);

      if (!isShuttingDown) {
        MessageBox.Show("An unexpected error occurred. Please check the log file for details.");
      }
    }
  }
}
