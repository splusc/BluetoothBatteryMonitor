# BluetoothBatteryMonitor

Windows 11 tray application “蓝牙电量监视器” that displays Bluetooth-device battery state as a battery icon.

## Usage

- Hover over the tray icon to see available battery status for monitored devices.
- Right-click the icon to choose `主设备` (Primary), `最低电量` (Lowest), or `轮播` (Cycle) mode.
- Use `监控设备` (Monitor Devices) and `主设备` (Primary Device) to configure what the icon represents.
- Use `开机自动启动` (Start with Windows) to launch BluetoothBatteryMonitor when the current Windows user signs in.
- Settings are stored in `%LocalAppData%\BluetoothBatteryMonitor\settings.json`.

BluetoothBatteryMonitor reads battery information exposed by Windows. Devices that use vendor-specific battery protocols are shown as battery unknown.

## Build

Requires the .NET 10 SDK on Windows 11.

```powershell
dotnet run --project tests/BluetoothBatteryMonitor.Checks/BluetoothBatteryMonitor.Checks.csproj
dotnet build src/BluetoothBatteryMonitor/BluetoothBatteryMonitor.csproj
```

## Publish

```powershell
dotnet publish src/BluetoothBatteryMonitor/BluetoothBatteryMonitor.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o artifacts/win-x64
```
