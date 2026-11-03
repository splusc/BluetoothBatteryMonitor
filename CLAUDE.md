# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Test

```powershell
# Build the main project
dotnet build src/BluetoothBatteryMonitor/BluetoothBatteryMonitor.csproj

# Run all tests (a custom console runner; prints "All checks passed." on success)
dotnet run --project tests/BluetoothBatteryMonitor.Checks/BluetoothBatteryMonitor.Checks.csproj

# Publish as single-file x64 executable (output: artifacts/win-x64/BluetoothBatteryMonitor.exe)
dotnet publish src/BluetoothBatteryMonitor/BluetoothBatteryMonitor.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o artifacts/win-x64
```

The solution has no `.sln` file. Projects are referenced directly via `<ProjectReference>` in the test `.csproj`. The test project gets `InternalsVisibleTo` access to `BluetoothBatteryMonitor` internals.

## Architecture

**BluetoothBatteryMonitor** (“蓝牙电量监视器”) is a Windows 11 system-tray app (.NET 10, Windows Forms) that shows Bluetooth-device battery levels as a tray icon. It targets `net10.0-windows10.0.22621.0` and uses Windows Runtime APIs (`Windows.Devices.Bluetooth`) for device enumeration.

### Entry & lifecycle

- `Program.cs` — acquires the single-instance mutex (`Local\BluetoothBatteryMonitor`), removes legacy TrayMonitor data, then calls `Application.Run(new TrayApplicationContext())`.
- `TrayApplicationContext.cs` — the app's center. Creates `NotifyIcon`, a 60-second refresh timer, and a 5-second cycle-advance timer. On first run with no saved settings, it auto-initializes by adding all currently-connected devices as monitored and picking the first as primary. It calls `BluetoothMonitor.GetDevicesAsync()`, then `DisplayLogic.SelectDevice()` + `BatteryIcon.Create()` to update the tray. Context-menu construction is fully dynamic (built each time the menu opens) from the current device list.

### Data flow

1. **`BluetoothMonitor`** queries both classic and LE paired Bluetooth devices via `DeviceInformation.FindAllAsync`, requesting `IsConnected` and `BatteryLife` properties. Duplicates (same device ID across classic + LE) are deduplicated preferring the connected+known-battery entry. Battery values are normalized to `byte?` (0–100).
2. **`AppModel.cs`** holds `BluetoothDeviceSnapshot` (Id, Name, IsConnected, BatteryPercent), `AppSettings` (Mode, MonitoredDeviceIds, PrimaryDeviceId, Initialized), and `DisplayLogic` — pure functions for selecting a device (primary/lowest/cycle) and building the tooltip text (63-char limit with ellipsis truncation).
3. **`BatteryIcon`** draws a 32×32 tray icon programmatically (GDI+): battery outline with 5 segments filled by percentage band, or a "?" for unknown battery. Colors: red ≤20%, gold ≤60%, green >60%.

### Display modes

| Mode | Behavior |
|------|----------|
| `Primary` | Shows the device whose ID matches `PrimaryDeviceId` exactly. |
| `Lowest` | Shows the monitored connected device with the lowest known battery. Devices with unknown battery are skipped. |
| `Cycle` | Rotates through all monitored connected devices every 5 seconds. |

### Persistence

- `SettingsStore` reads/writes `%LocalAppData%\BluetoothBatteryMonitor\settings.json` using `System.Text.Json`. Corrupt or missing files produce a default `AppSettings`. `MonitoredDeviceIds` is always reconstructed as `HashSet<string>(StringComparer.OrdinalIgnoreCase)` after deserialization.
- `StartupRegistration` manages the `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\BluetoothBatteryMonitor` registry value.

### Tests

Tests are in `tests/BluetoothBatteryMonitor.Checks/` and use a minimal custom assertion helper (`Check.Equal`, `Check.True`) rather than a test framework. Each `*Checks.cs` file exposes a static `Run()` method, called from `Program.cs`. Tests that touch user data create temporary directories and isolated registry keys, then clean up in a `finally` block.
