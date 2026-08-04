# TrayMonitor

Windows 11 tray application that displays Bluetooth-device battery state as a battery icon.

## Usage

- Hover over the tray icon to see monitored devices and exact battery percentages.
- Right-click the icon to choose Primary, Lowest, or Cycle mode.
- Use Monitor Devices and Primary Device to configure what the icon represents.
- Settings are stored in `%LocalAppData%\TrayMonitor\settings.json`.

TrayMonitor reads battery information exposed by Windows. Devices that use vendor-specific battery protocols are shown as battery unknown.

## Build

Requires the .NET 10 SDK on Windows 11.

```powershell
dotnet run --project tests/TrayMonitor.Checks/TrayMonitor.Checks.csproj
dotnet build src/TrayMonitor/TrayMonitor.csproj
```

## Publish

```powershell
dotnet publish src/TrayMonitor/TrayMonitor.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o artifacts/win-x64
```
