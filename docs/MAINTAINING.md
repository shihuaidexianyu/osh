# OmenSuperHub Maintenance Map

This project is organized for low-risk iterative changes, especially when the next editor is an AI assistant.

## Dependency direction

Keep dependencies flowing in this order:

`UI -> App -> Services -> Hardware`

- `src/UI`
  - Window composition, control syncing, charts, and user interactions.
- `src/App`
  - Runtime orchestration, lifecycle, background scheduling, and strongly typed control settings.
- `src/App/Services`
  - Snapshot building, config restore, shell updates, telemetry, and hardware control semantics.
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

## MainForm split

`MainForm` is intentionally split into partial files:

- `src/UI/MainForm.cs`
  - Singleton access, window lifetime, and core fields
- `src/UI/MainForm.Layout.cs`
  - Visual tree construction and reusable control builders
- `src/UI/MainForm.Interaction.cs`
  - Dashboard refresh, control syncing, and event handlers
- `src/UI/MainForm.Charts.cs`
  - Chart drawing plus formatting helpers for telemetry display

If a UI change only affects one of these concerns, keep the edit in that file.

## AppRuntime split

`AppRuntime` is now intentionally split into partial files under `src/App`:

- `Program.cs`
  - Core runtime fields, polling/control loop internals, and interface wiring
- `AppRuntime.ControlApply.cs`
  - All `Apply*` control mutation paths and persistence trigger points
- `AppRuntime.StatePersistence.cs`
  - Runtime snapshot projection and settings restore/save flows
- `AppRuntime.Lifecycle.cs`
  - Startup, single-instance guard, pipe listener, shutdown, and fatal exception hooks

When changing runtime behavior, prefer editing the most specific partial file first.

## Startup task management

Windows startup task and legacy startup artifact cleanup are encapsulated in:

- `src/App/Services/StartupTaskService.cs`

If your change only affects scheduled-task registration, privilege level, or legacy cleanup commands, edit this service rather than `AppRuntime`.

## Guardrails

- Do not call BIOS/WMI code directly from `UI`.
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
