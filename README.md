# BluetoothBatteryMonitor

Windows 11 tray application “蓝牙电量监视器” that displays Bluetooth-device battery state as a battery icon.

## Usage

- Hover over the tray icon to see available battery status for monitored devices.
- Right-click the icon to choose `主设备` (Primary), `最低电量` (Lowest), or `轮播` (Cycle) mode.
- Use `监控设备` (Monitor Devices) and `主设备` (Primary Device) to configure what the icon represents.
- `电量显示效果` 可选择 `默认` 或 `个性化图标`。首次使用和旧设置默认保留原有横向彩色电池；个性化效果为白色设备图形搭配竖向电池，不改变主设备、最低电量和轮播模式。两种效果的电量颜色均为 ≤20% 红色、21%～60% 黄色、>60% 绿色；个性化电池外框同步变色，未知电量显示灰色问号。
- 个性化内置图标包括键盘、鼠标、耳机、Xbox、PS4 DualShock 4、PS5 DualSense 和无 Xbox 标志的通用手柄。设备剪影裁去留白后保持原始宽高比、居中显示；手柄等比放大到托盘方框允许的最大尺寸，电池覆盖最右侧，宽度约为图标的 33%（32 像素画布上取整为 11 像素）。
- 在 `设备图标 → 设备名称` 中，可为每台蓝牙设备手动选择内置图标，也可恢复 `自动识别`。设备的 VID/PID 硬件标识优先于名称；只报告 `Wireless Controller` 且没有可靠型号信息时显示无品牌手柄，不猜测 PS4／PS5。自定义图片导入暂未提供。
- Use `开机自动启动` (Start with Windows) to launch BluetoothBatteryMonitor when the current Windows user signs in.
- Settings are stored in `%LocalAppData%\BluetoothBatteryMonitor\settings.json`.

托盘右键菜单使用 Windows 原生系统菜单，保留所有显示模式、设备选择、图标和开机启动选项。菜单内容在显示前准备完成，避免首次空菜单取消显示；原生菜单负责焦点、键盘导航和点击空白关闭。此调整用于改善第三方电脑管家对 WinForms 弹出菜单的兼容性，具体判定仍需在对应电脑管家环境中验证。

内存优化：设备图预绘制为 32×32，电量和图标不变时复用当前托盘图标，每次读取后释放 GATT 服务和设备。应用关闭 .NET 分层编译以减少常驻代码开销。进程包含 .NET、WinForms 和 Windows 蓝牙组件的基础内存，实际占用随设备、运行时和使用时间变化。

个性化图标使用明确的源／目标像素矩形绘制，避免 Windows 高 DPI 缩放把基础图形放大后裁切。视觉验收时可用 `BluetoothBatteryMonitor.exe --base-icon` 先只显示设备图形；正常启动仍显示右侧电池，参数不修改保存的设置。

BluetoothBatteryMonitor reads battery information exposed by Windows and the BLE Battery Service. Sony DualShock 4（VID 054C，PID 05C4／09CC）另通过同设备容器的蓝牙 HID 完整输入报告读取电量，采用 DS4Windows 的换算方式：未充电按 8 档、充电按 11 档满量程计算，整数取整并限制为 100%（例如未充电档位 7 显示 87%）。手柄只上报电量档位，百分比为估算值；充电错误或保留值显示未知。手柄被 DS4Windows 独占或被 HidHide 隐藏时可能无法读取；不会发送灯光或震动输出。其他尚未支持的专用协议显示电量未知。最低电量模式优先选择已知电量；全部未知时仍显示已连接设备的图标。

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

精简版（约 25.6MB），需要目标电脑已安装 [.NET 10 Desktop Runtime x64](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)。需要桌面运行时，只有基础 .NET Runtime 不够。

```powershell
dotnet publish src/BluetoothBatteryMonitor/BluetoothBatteryMonitor.csproj -p:PublishProfile=Lean -o artifacts/lean/win-x64
```

便携版自带运行时，可在未安装 .NET 的电脑上运行。使用 ZIP 压缩分发，使用前解压。

```powershell
dotnet publish src/BluetoothBatteryMonitor/BluetoothBatteryMonitor.csproj -p:PublishProfile=Portable -o artifacts/portable/win-x64
Compress-Archive -LiteralPath artifacts/portable/win-x64/BluetoothBatteryMonitor.exe -DestinationPath artifacts/portable/BluetoothBatteryMonitor.zip -Force
```

两个发布配置均仅保留简体中文语言资源及英文回退资源，关闭调试符号输出。未启用 EXE 内部压缩：实测它会增加运行时的私有内存，不适合本程序的低内存目标。ZIP 只影响下载和存储体积，解压后的运行内存不受压缩分发影响。
