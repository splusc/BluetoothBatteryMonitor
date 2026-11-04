# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Test

```powershell
# Build the main project
dotnet build src/BluetoothBatteryMonitor/BluetoothBatteryMonitor.csproj

# Run all tests (a custom console runner; prints "All checks passed." on success)
dotnet run --project tests/BluetoothBatteryMonitor.Checks/BluetoothBatteryMonitor.Checks.csproj

# 发布 x64 单文件：精简版依赖桌面运行时，便携版自带运行时。
dotnet publish src/BluetoothBatteryMonitor/BluetoothBatteryMonitor.csproj -p:PublishProfile=Lean -o artifacts/lean/win-x64
dotnet publish src/BluetoothBatteryMonitor/BluetoothBatteryMonitor.csproj -p:PublishProfile=Portable -o artifacts/portable/win-x64
```

The solution has no `.sln` file. Projects are referenced directly via `<ProjectReference>` in the test `.csproj`. The test project gets `InternalsVisibleTo` access to `BluetoothBatteryMonitor` internals.

## Architecture

**BluetoothBatteryMonitor** (“蓝牙电量监视器”) is a Windows 11 system-tray app (.NET 10, Windows Forms) that shows Bluetooth-device battery levels as a tray icon. It targets `net10.0-windows10.0.22621.0` and uses Windows Runtime APIs (`Windows.Devices.Bluetooth`) for device enumeration.

### Entry & lifecycle

- `Program.cs` — acquires the single-instance mutex (`Local\BluetoothBatteryMonitor`), removes legacy TrayMonitor data, then calls `Application.Run(new TrayApplicationContext())`.
- `TrayApplicationContext.cs` — the app's center. Creates `NotifyIcon`, a 60-second refresh timer, and a 5-second cycle-advance timer. On first run with no saved settings, it auto-initializes by adding all currently-connected devices as monitored and picking the first as primary. It calls `BluetoothMonitor.GetDevicesAsync()`, then `DisplayLogic.SelectDevice()` + `BatteryIcon.Create()` to update the tray. Context-menu construction is fully dynamic (built each time the menu opens) from the current device list.

### Data flow

1. **`BluetoothMonitor`** queries both classic and LE paired Bluetooth devices via `DeviceInformation.FindAllAsync`, requesting `IsConnected` and `BatteryLife` properties. Duplicates (same device ID across classic + LE) are deduplicated preferring the connected+known-battery entry. Battery values are normalized to `byte?` (0–100). `DualShockBatteryReader` reads Sony PS4 Bluetooth HID reports from the same device container with bounded I/O, using the DS4Windows 8/11-level conversion for discharging/charging.
2. **`AppModel.cs`** holds `BluetoothDeviceSnapshot` (Id, Name, IsConnected, BatteryPercent), `AppSettings` (Mode, MonitoredDeviceIds, PrimaryDeviceId, Initialized), and `DisplayLogic` — pure functions for selecting a device (primary/lowest/cycle) and building the tooltip text (63-char limit with ellipsis truncation).
3. **`BatteryIcon`** draws a 32×32 tray icon programmatically (GDI+): battery outline with 5 segments filled by percentage band, or a "?" for unknown battery. Colors: red ≤20%, gold ≤60%, green >60%.
4. **`DeviceIconClassifier`** 优先依据同一设备容器内的 HID VID/PID 区分 PS4／PS5，再用 Windows 的 BLE Appearance（961 为键盘、962 为鼠标）及经典蓝牙类别识别明确类型，名称最后兜底。通用游戏设备类型不覆盖已识别的具体型号；不要仅凭组合键盘同时暴露鼠标 HID 就认作鼠标。型号不明的手柄使用去掉 Xbox 标志的通用图形。`AppSettings.IconEffect` 独立于 `Mode`；`DeviceIcons` 保存每个设备的手动选择并忽略设备 ID 大小写。
5. **个性化图标** 使用 `Assets/DeviceIcons` 中的嵌入 PNG，裁去透明留白后保持比例、居中显示；按明确的像素矩形绘制，避免高 DPI 下放大裁切。竖向电池覆盖最右侧，宽度按画布的 33% 取整为 11 像素。电池填充与外框共用默认样式的三阶段颜色：≤20% 红色、21%～60% 黄色、>60% 绿色，未知电量为灰色。`--base-icon` 可仅预览基础设备图形，不修改保存设置。素材嵌入程序集，单文件发布不依赖外部图片。鼠标、键盘、耳机、蓝牙图形派生自 Lucide，许可见素材目录。
6. **内存与绘图** 运行时只嵌入预绘制的 32×32 设备图，384×384 原始剪影保留在 `Source/`；生成脚本为 `scripts/Export-TrayDeviceIcons.ps1`。托盘复用当前电量、效果和类型相同的图标，仍独立刷新悬浮提示。每次 GATT 电量读取结束后同时释放服务与设备句柄。主项目关闭分层编译，减少后台常驻场景下的多级 JIT 开销；勿通过定时强制 GC 或工作集修剪伪装内存降幅。
7. **发布体积** `Properties/PublishProfiles/Lean.pubxml` 生成依赖 .NET 10 Desktop Runtime x64 的精简单文件；`Portable.pubxml` 生成自带运行时的便携单文件，可用 ZIP 压缩分发。二者只保留简体中文卫星资源与英文回退，不输出调试符号。保持 `EnableCompressionInSingleFile=false`：虽然内部压缩可缩小 EXE，但蓝牙读取诊断中的私有内存由约 22MB 升至约 49MB。
8. **托盘菜单** `ContextMenuStrip` 只作为菜单项和点击事件的数据模型，不交给 `NotifyIcon` 显示。右键松开时先构建内容，再由 `NativeTrayMenu` 使用 `TrackPopupMenuEx` 显示系统菜单；隐藏所有者遵循 `SetForegroundWindow`／`WM_NULL` 焦点协议。`NativePopupMenu` 用安全句柄管理主、子菜单，选择命令后执行原有 `PerformClick`，取消时不执行操作；菜单打开期间不得重建命令映射。第三方电脑管家的提示需要实机复测，不能只凭原生菜单断言误报消失。

### Display modes

| Mode | Behavior |
|------|----------|
| `Primary` | Shows the device whose ID matches `PrimaryDeviceId` exactly. |
| `Lowest` | Shows the monitored connected device with the lowest known battery; if all are unknown, keeps the first connected device's icon. |
| `Cycle` | Rotates through all monitored connected devices every 5 seconds. |

### Persistence

- `SettingsStore` reads/writes `%LocalAppData%\BluetoothBatteryMonitor\settings.json` using `System.Text.Json`. Corrupt or missing files produce a default `AppSettings`. `MonitoredDeviceIds` is always reconstructed as `HashSet<string>(StringComparer.OrdinalIgnoreCase)` after deserialization.
- `StartupRegistration` manages the `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\BluetoothBatteryMonitor` registry value.

### Tests

Tests are in `tests/BluetoothBatteryMonitor.Checks/` and use a minimal custom assertion helper (`Check.Equal`, `Check.True`) rather than a test framework. Each `*Checks.cs` file exposes a static `Run()` method, called from `Program.cs`. Tests that touch user data create temporary directories and isolated registry keys, then clean up in a `finally` block.
