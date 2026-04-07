using System;

namespace OmenSuperHub {
  static class Program {
    [STAThread]
    static void Main(string[] args) {
      Environment.ExitCode = CliApp.Run(args);
    }
  }
}
