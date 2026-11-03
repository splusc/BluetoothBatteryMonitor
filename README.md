# BluetoothBatteryMonitor

Windows 11 tray application “蓝牙电量监视器” that displays Bluetooth-device battery state as a battery icon.

## Usage

- Hover over the tray icon to see available battery status for monitored devices.
- Right-click the icon to choose `主设备` (Primary), `最低电量` (Lowest), or `轮播` (Cycle) mode.
- Use `监控设备` (Monitor Devices) and `主设备` (Primary Device) to configure what the icon represents.
- `电量显示效果` 可选择 `默认（当前）` 或 `个性化图标`。首次使用和旧设置默认保留原有横向彩色电池；个性化效果为白色设备图形搭配竖向电池，不改变主设备、最低电量和轮播模式。两种效果的电量颜色均为 ≤20% 红色、21%～60% 黄色、>60% 绿色；个性化电池外框同步变色，未知电量显示灰色问号。
- 个性化内置图标包括键盘、鼠标、耳机、Xbox、PS4 DualShock 4、PS5 DualSense 和无 Xbox 标志的通用手柄。PS4／PS5 的视觉比例已放大到与 Xbox 相近。
- 在 `设备图标 → 设备名称` 中，可为每台蓝牙设备手动选择内置图标，也可恢复 `自动识别`。设备的 VID/PID 硬件标识优先于名称；只报告 `Wireless Controller` 且没有可靠型号信息时显示无品牌手柄，不猜测 PS4／PS5。自定义图片导入暂未提供。
- Use `开机自动启动` (Start with Windows) to launch BluetoothBatteryMonitor when the current Windows user signs in.
- Settings are stored in `%LocalAppData%\BluetoothBatteryMonitor\settings.json`.

内存优化：设备图预绘制为 32×32，电量和图标不变时复用当前托盘图标，每次读取后释放 GATT 服务和设备。应用关闭 .NET 分层编译以减少常驻代码开销。进程包含 .NET、WinForms 和 Windows 蓝牙组件的基础内存，实际占用随设备、运行时和使用时间变化。

BluetoothBatteryMonitor reads battery information exposed by Windows. Devices that use vendor-specific battery protocols are shown as battery unknown.

## Build

Requires the .NET 10 SDK on Windows 11.

```powershell
dotnet run --project tests/BluetoothBatteryMonitor.Checks/BluetoothBatteryMonitor.Checks.csproj
dotnet build src/BluetoothBatteryMonitor/BluetoothBatteryMonitor.csproj
```

导出程序实际绘制的全套图标（含 16／24／32 像素对照）：

```powershell
dotnet run --project tests/BluetoothBatteryMonitor.Checks/BluetoothBatteryMonitor.Checks.csproj -c Release -- artifacts/device-icon-preview.png
```

## Publish

```powershell
dotnet publish src/BluetoothBatteryMonitor/BluetoothBatteryMonitor.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o artifacts/win-x64
```
