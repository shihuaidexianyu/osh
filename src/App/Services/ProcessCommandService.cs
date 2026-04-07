using System.Diagnostics;
using System;

namespace OmenSuperHub {
  internal sealed class ProcessResult {
    public int ExitCode { get; set; }
    public string Output { get; set; }
    public string Error { get; set; }
  }

  internal sealed class ProcessCommandService {
    const int DefaultTimeoutMs = 15000;

    public ProcessResult Execute(string command, int timeoutMs = DefaultTimeoutMs) {
      var processStartInfo = new ProcessStartInfo {
        FileName = "cmd.exe",
        Arguments = $"/c {command}",
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true,
        WindowStyle = ProcessWindowStyle.Hidden
      };

      try {
        using (var process = new Process { StartInfo = processStartInfo }) {
          process.Start();

          bool exited = timeoutMs <= 0 || process.WaitForExit(timeoutMs);
          if (!exited) {
            try {
              process.Kill();
            } catch {
            }

            return new ProcessResult {
              ExitCode = -1,
              Output = string.Empty,
              Error = $"Command timed out after {timeoutMs}ms: {command}"
            };
          }

          string output = process.StandardOutput.ReadToEnd();
          string error = process.StandardError.ReadToEnd();
          return new ProcessResult {
            ExitCode = process.ExitCode,
            Output = output,
            Error = error
          };
        }
      } catch (Exception ex) {
        return new ProcessResult {
          ExitCode = -1,
          Output = string.Empty,
          Error = ex.Message
        };
      }
    }
  }
}
