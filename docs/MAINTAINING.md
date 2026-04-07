# OmenSuperHub Maintenance Map (CLI)

This project is organized for low-risk iterative changes, especially when the next editor is an AI assistant.

## Dependency direction

Keep dependencies flowing in this order:

`CLI -> App -> Services -> Hardware`

- `src/App`
  - CLI command entry, command orchestration, and strongly typed control settings.
- `src/App/Services`
  - Config restore, telemetry, startup task management, and hardware control semantics.
- `src/Hardware`
  - BIOS/WMI access and hardware model types.
- `src/Core`
  - Control logic that should stay UI-agnostic and hardware-agnostic.

## Where to edit

- Add or change a dashboard field:
  - Start in `src/App/Services/DashboardSnapshotBuilder.cs`
  - Then update `src/UI/MainForm.Interaction.cs` or `src/UI/MainForm.Charts.cs`
- Change startup/config restore behavior:
  - Start in `src/App/Services/SettingsRestoreService.cs`
  - Then update `src/App/Program.cs` only if runtime side effects must change
- Change tray icon or floating overlay behavior:
  - Start in `src/App/Services/ShellStatusBuilder.cs`
  - Then update `src/App/Services/AppShellService.cs`
- Change a hardware write path:
  - Start in `src/App/Services/HardwareControlService.cs`
  - Only go to `src/Hardware/OmenHardwareGateway.cs` when BIOS/WMI details must change
- Change hardware polling/telemetry interpretation:
  - Start in `src/App/Services/HardwareTelemetryService.cs`

## CLI entry

The executable is now CLI-only. Main entry and command dispatch are in:

- `src/App/Program.cs`
- `src/App/CliApp.cs`

Primary command groups currently include:

- `status`
- `config`
- `preset`
- `set`

## Runtime note

Legacy GUI runtime (`AppRuntime` and `src/UI`) is removed from project build. Maintenance should target `CliApp` and services.

## Startup task management

Windows startup task and legacy startup artifact cleanup are encapsulated in:

- `src/App/Services/StartupTaskService.cs`

If your change only affects scheduled-task registration, privilege level, or legacy cleanup commands, edit this service rather than `AppRuntime`.

## Guardrails

- Do not call BIOS/WMI code directly from CLI argument parsing; route through services.
- Prefer extending `RuntimeControlSettings` instead of adding new free-form setting strings.
- If a change only affects config-to-UI mapping, prefer editing `SettingsRestoreService` over `Program.cs`.
- If a change only affects snapshot projection, prefer editing a builder over adding more logic to `AppRuntime`.
- Keep new pure logic in services or builders so it can be covered by tests.

## Tests

The test project covers the lowest-risk, highest-value maintenance surfaces:

- `RuntimeControlSettings`
- `SettingsRestoreService`
- `DashboardSnapshotBuilder`
- `ShellStatusBuilder`

When adding new configuration mapping or snapshot formatting logic, extend those tests first.
