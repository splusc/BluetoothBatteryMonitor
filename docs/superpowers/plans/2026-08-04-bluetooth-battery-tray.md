# Bluetooth Battery Tray Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a Windows 11 tray application that displays configurable Bluetooth-device battery state through a battery-shaped icon and hover text.

**Architecture:** A WinForms `ApplicationContext` owns the tray icon, timers, dynamic menu, and application lifetime. Small framework-free classes handle Windows device enumeration, display selection, icon drawing, and JSON settings; a console check project exercises all non-UI logic without adding test packages.

**Tech Stack:** C# 14, .NET 10, WinForms, `Windows.Devices.Enumeration`, `Windows.Devices.Bluetooth`, `System.Drawing`, `System.Text.Json`.

---

## File Map

- Create: `.gitignore` - generated build and publish output exclusions.
- Create: `src/TrayMonitor/TrayMonitor.csproj` - WinForms executable and Windows target settings.
- Create: `src/TrayMonitor/Program.cs` - single-instance startup.
- Create: `src/TrayMonitor/AppModel.cs` - device/settings models and pure display logic.
- Create: `src/TrayMonitor/SettingsStore.cs` - `%LocalAppData%` JSON persistence.
- Create: `src/TrayMonitor/BluetoothMonitor.cs` - paired Bluetooth enumeration and property mapping.
- Create: `src/TrayMonitor/BatteryIcon.cs` - in-memory five-band tray icon drawing.
- Create: `src/TrayMonitor/TrayApplicationContext.cs` - timers, tooltip, menu, and shutdown.
- Create: `tests/TrayMonitor.Checks/TrayMonitor.Checks.csproj` - dependency-free check executable.
- Create: `tests/TrayMonitor.Checks/Program.cs` - check runner.
- Create: `tests/TrayMonitor.Checks/Check.cs` - minimal assertion helpers.
- Create: `tests/TrayMonitor.Checks/DisplayLogicChecks.cs` - selection, band, and tooltip checks.
- Create: `tests/TrayMonitor.Checks/SettingsStoreChecks.cs` - settings persistence checks.
- Create: `tests/TrayMonitor.Checks/BluetoothMonitorChecks.cs` - Windows battery-property normalization checks.
- Create: `tests/TrayMonitor.Checks/BatteryIconChecks.cs` - native icon creation smoke checks.
- Create: `README.md` - usage, support boundary, build, and publish instructions.

## Prerequisite

The workstation currently has no `dotnet` command. Before Task 1, obtain user approval to install the .NET 10 SDK, then run:

```powershell
winget install --id Microsoft.DotNet.SDK.10 --exact --source winget
dotnet --version
```

Expected: the second command prints a version beginning with `10.`.

### Task 1: Minimal Windows Projects

**Files:**
- Create: `.gitignore`
- Create: `src/TrayMonitor/TrayMonitor.csproj`
- Create: `src/TrayMonitor/Program.cs`
- Create: `tests/TrayMonitor.Checks/TrayMonitor.Checks.csproj`
- Create: `tests/TrayMonitor.Checks/Program.cs`

- [ ] **Step 1: Generate the two projects**

Run:

```powershell
dotnet new console --framework net10.0 --name TrayMonitor --output src/TrayMonitor
dotnet new console --framework net10.0 --name TrayMonitor.Checks --output tests/TrayMonitor.Checks
```

Expected: both commands report that the Console App template was created successfully.

- [ ] **Step 2: Replace the application project file**

Set `src/TrayMonitor/TrayMonitor.csproj` to:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows10.0.22621.0</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <SelfContained>true</SelfContained>
    <PublishSingleFile>true</PublishSingleFile>
  </PropertyGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="TrayMonitor.Checks" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Replace the check project file**

Set `tests/TrayMonitor.Checks/TrayMonitor.Checks.csproj` to:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0-windows10.0.22621.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\TrayMonitor\TrayMonitor.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4: Add the minimal entry points and ignore rules**

Set `src/TrayMonitor/Program.cs` to:

```csharp
namespace TrayMonitor;

internal static class Program
{
    [STAThread]
    private static void Main() => ApplicationConfiguration.Initialize();
}
```

Set `tests/TrayMonitor.Checks/Program.cs` to:

```csharp
Console.WriteLine("Check harness ready.");
```

Set `.gitignore` to:

```gitignore
**/bin/
**/obj/
.vs/
artifacts/
```

- [ ] **Step 5: Build both projects**

Run:

```powershell
dotnet build src/TrayMonitor/TrayMonitor.csproj
dotnet run --project tests/TrayMonitor.Checks/TrayMonitor.Checks.csproj
```

Expected: build succeeds with 0 errors; the check project prints `Check harness ready.`.

- [ ] **Step 6: Commit the project skeleton**

```powershell
git add .gitignore src tests
git commit -m "build: scaffold Windows tray application"
```

### Task 2: Display Selection and Tooltip Logic

**Files:**
- Create: `src/TrayMonitor/AppModel.cs`
- Create: `tests/TrayMonitor.Checks/Check.cs`
- Create: `tests/TrayMonitor.Checks/DisplayLogicChecks.cs`
- Modify: `tests/TrayMonitor.Checks/Program.cs`

- [ ] **Step 1: Write the failing display checks**

Set `tests/TrayMonitor.Checks/Check.cs` to:

```csharp
namespace TrayMonitor.Checks;

internal static class Check
{
    public static void Equal<T>(T expected, T actual, string name)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{name}: expected {expected}, got {actual}");
    }

    public static void True(bool value, string name)
    {
        if (!value) throw new InvalidOperationException($"{name}: expected true");
    }
}
```

Set `tests/TrayMonitor.Checks/DisplayLogicChecks.cs` to:

```csharp
using TrayMonitor;

namespace TrayMonitor.Checks;

internal static class DisplayLogicChecks
{
    public static void Run()
    {
        var devices = new[]
        {
            new BluetoothDeviceSnapshot("headset", "Headset", true, 80),
            new BluetoothDeviceSnapshot("mouse", "Mouse", true, 25),
            new BluetoothDeviceSnapshot("keyboard", "Keyboard", false, 10),
            new BluetoothDeviceSnapshot("unknown", "Unknown", true, null)
        };
        var settings = new AppSettings
        {
            Initialized = true,
            PrimaryDeviceId = "headset",
            MonitoredDeviceIds = new(StringComparer.OrdinalIgnoreCase)
            {
                "headset", "mouse", "keyboard", "unknown"
            }
        };

        settings.Mode = DisplayMode.Primary;
        Check.Equal("headset", DisplayLogic.SelectDevice(devices, settings, 0)?.Id, "primary device");

        settings.Mode = DisplayMode.Lowest;
        Check.Equal("mouse", DisplayLogic.SelectDevice(devices, settings, 0)?.Id, "lowest known connected battery");

        settings.Mode = DisplayMode.Cycle;
        Check.Equal("mouse", DisplayLogic.SelectDevice(devices, settings, 1)?.Id, "cycle index");

        Check.Equal(1, DisplayLogic.BatterySegments(0), "0 percent band");
        Check.Equal(1, DisplayLogic.BatterySegments(20), "20 percent band");
        Check.Equal(2, DisplayLogic.BatterySegments(21), "21 percent band");
        Check.Equal(5, DisplayLogic.BatterySegments(100), "100 percent band");
        Check.Equal(0, DisplayLogic.BatterySegments(null), "unknown band");

        var tooltip = DisplayLogic.BuildTooltip(
            [new BluetoothDeviceSnapshot("long", new string('X', 100), true, 85)],
            null,
            "No devices");
        Check.True(tooltip.Length <= 63, "tooltip length");
        Check.True(tooltip.EndsWith("85%", StringComparison.Ordinal), "tooltip keeps battery suffix");
    }
}
```

Replace `tests/TrayMonitor.Checks/Program.cs` with:

```csharp
using TrayMonitor.Checks;

DisplayLogicChecks.Run();
Console.WriteLine("All checks passed.");
```

- [ ] **Step 2: Run the checks and verify failure**

Run:

```powershell
dotnet run --project tests/TrayMonitor.Checks/TrayMonitor.Checks.csproj
```

Expected: compilation fails because `BluetoothDeviceSnapshot`, `AppSettings`, and `DisplayLogic` do not exist.

- [ ] **Step 3: Implement the minimal model and pure logic**

Create `src/TrayMonitor/AppModel.cs`:

```csharp
using System.Text;

namespace TrayMonitor;

internal enum DisplayMode
{
    Primary,
    Lowest,
    Cycle
}

internal sealed record BluetoothDeviceSnapshot(
    string Id,
    string Name,
    bool IsConnected,
    byte? BatteryPercent);

internal sealed class AppSettings
{
    public bool Initialized { get; set; }
    public DisplayMode Mode { get; set; } = DisplayMode.Lowest;
    public HashSet<string> MonitoredDeviceIds { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string? PrimaryDeviceId { get; set; }
}

internal static class DisplayLogic
{
    private const int TooltipLimit = 63;

    public static BluetoothDeviceSnapshot? SelectDevice(
        IReadOnlyList<BluetoothDeviceSnapshot> devices,
        AppSettings settings,
        int cycleIndex)
    {
        var eligible = devices
            .Where(device => device.IsConnected && settings.MonitoredDeviceIds.Contains(device.Id))
            .ToArray();

        if (eligible.Length == 0) return null;

        return settings.Mode switch
        {
            DisplayMode.Primary => eligible.FirstOrDefault(
                device => string.Equals(device.Id, settings.PrimaryDeviceId, StringComparison.OrdinalIgnoreCase)),
            DisplayMode.Lowest => eligible
                .Where(device => device.BatteryPercent.HasValue)
                .MinBy(device => device.BatteryPercent),
            DisplayMode.Cycle => eligible[Math.Max(0, cycleIndex) % eligible.Length],
            _ => null
        };
    }

    public static int BatterySegments(byte? percent) => percent switch
    {
        null => 0,
        <= 20 => 1,
        <= 40 => 2,
        <= 60 => 3,
        <= 80 => 4,
        _ => 5
    };

    public static string BuildTooltip(
        IReadOnlyList<BluetoothDeviceSnapshot> devices,
        BluetoothDeviceSnapshot? current,
        string emptyStatus)
    {
        if (devices.Count == 0) return Clip(emptyStatus, TooltipLimit);

        var ordered = current is null
            ? devices
            : new[] { current }.Concat(devices.Where(device => device.Id != current.Id)).ToArray();
        var text = new StringBuilder();

        foreach (var device in ordered)
        {
            var entry = FormatDevice(device, TooltipLimit);
            var separatorLength = text.Length == 0 ? 0 : Environment.NewLine.Length;
            if (text.Length + separatorLength + entry.Length > TooltipLimit) break;
            if (separatorLength > 0) text.AppendLine();
            text.Append(entry);
        }

        return text.Length == 0 ? Clip(emptyStatus, TooltipLimit) : text.ToString();
    }

    private static string FormatDevice(BluetoothDeviceSnapshot device, int limit)
    {
        var suffix = !device.IsConnected
            ? "未连接"
            : device.BatteryPercent is byte percent ? $"{percent}%" : "电量未知";
        var maxNameLength = Math.Max(1, limit - suffix.Length - 1);
        var name = device.Name.Length <= maxNameLength
            ? device.Name
            : Clip(device.Name, maxNameLength);
        return $"{name} {suffix}";
    }

    private static string Clip(string value, int limit) => value.Length <= limit
        ? value
        : limit <= 1 ? value[..limit] : $"{value[..(limit - 1)]}…";
}
```

- [ ] **Step 4: Run the checks and verify success**

Run:

```powershell
dotnet run --project tests/TrayMonitor.Checks/TrayMonitor.Checks.csproj
```

Expected: `All checks passed.`.

- [ ] **Step 5: Commit display logic**

```powershell
git add src/TrayMonitor/AppModel.cs tests/TrayMonitor.Checks
git commit -m "feat: add battery display selection logic"
```

### Task 3: JSON Settings Persistence

**Files:**
- Create: `src/TrayMonitor/SettingsStore.cs`
- Create: `tests/TrayMonitor.Checks/SettingsStoreChecks.cs`
- Modify: `tests/TrayMonitor.Checks/Program.cs`

- [ ] **Step 1: Write failing persistence checks**

Create `tests/TrayMonitor.Checks/SettingsStoreChecks.cs`:

```csharp
using TrayMonitor;

namespace TrayMonitor.Checks;

internal static class SettingsStoreChecks
{
    public static void Run()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"TrayMonitor-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "settings.json");
        try
        {
            var store = new SettingsStore(path);
            Check.Equal(false, store.Load().Initialized, "missing settings defaults");

            var settings = new AppSettings
            {
                Initialized = true,
                Mode = DisplayMode.Cycle,
                PrimaryDeviceId = "mouse",
                MonitoredDeviceIds = new(StringComparer.OrdinalIgnoreCase) { "mouse", "headset" }
            };
            Check.True(store.Save(settings), "settings save");

            var loaded = store.Load();
            Check.Equal(DisplayMode.Cycle, loaded.Mode, "mode round trip");
            Check.Equal("mouse", loaded.PrimaryDeviceId, "primary round trip");
            Check.True(loaded.MonitoredDeviceIds.Contains("MOUSE"), "case-insensitive IDs after load");

            File.WriteAllText(path, "{not-json");
            Check.Equal(false, store.Load().Initialized, "corrupt settings defaults");
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
```

Add this call after `DisplayLogicChecks.Run();` in `tests/TrayMonitor.Checks/Program.cs`:

```csharp
SettingsStoreChecks.Run();
```

- [ ] **Step 2: Run the checks and verify failure**

Run:

```powershell
dotnet run --project tests/TrayMonitor.Checks/TrayMonitor.Checks.csproj
```

Expected: compilation fails because `SettingsStore` does not exist.

- [ ] **Step 3: Implement settings load and save**

Create `src/TrayMonitor/SettingsStore.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TrayMonitor;

internal sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _path;

    public SettingsStore(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TrayMonitor",
            "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_path)) return new AppSettings();
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), JsonOptions)
                ?? new AppSettings();
            settings.MonitoredDeviceIds = new HashSet<string>(
                settings.MonitoredDeviceIds ?? [],
                StringComparer.OrdinalIgnoreCase);
            return settings;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return new AppSettings();
        }
    }

    public bool Save(AppSettings settings)
    {
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(_path, JsonSerializer.Serialize(settings, JsonOptions));
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
```

- [ ] **Step 4: Run the checks and verify success**

Run:

```powershell
dotnet run --project tests/TrayMonitor.Checks/TrayMonitor.Checks.csproj
```

Expected: `All checks passed.`.

- [ ] **Step 5: Commit persistence**

```powershell
git add src/TrayMonitor/SettingsStore.cs tests/TrayMonitor.Checks
git commit -m "feat: persist tray monitor settings"
```

### Task 4: Windows Bluetooth Enumeration

**Files:**
- Create: `src/TrayMonitor/BluetoothMonitor.cs`
- Create: `tests/TrayMonitor.Checks/BluetoothMonitorChecks.cs`
- Modify: `tests/TrayMonitor.Checks/Program.cs`

- [ ] **Step 1: Write failing battery-property checks**

Create `tests/TrayMonitor.Checks/BluetoothMonitorChecks.cs`:

```csharp
using TrayMonitor;

namespace TrayMonitor.Checks;

internal static class BluetoothMonitorChecks
{
    public static void Run()
    {
        Check.Equal((byte?)55, BluetoothMonitor.NormalizeBattery((byte)55), "byte battery");
        Check.Equal((byte?)80, BluetoothMonitor.NormalizeBattery((uint)80), "convertible battery");
        Check.Equal<byte?>(null, BluetoothMonitor.NormalizeBattery((byte)101), "unknown sentinel");
        Check.Equal<byte?>(null, BluetoothMonitor.NormalizeBattery(null), "missing battery");
    }
}
```

Add this call before the final `Console.WriteLine` in `tests/TrayMonitor.Checks/Program.cs`:

```csharp
BluetoothMonitorChecks.Run();
```

- [ ] **Step 2: Run the checks and verify failure**

Run:

```powershell
dotnet run --project tests/TrayMonitor.Checks/TrayMonitor.Checks.csproj
```

Expected: compilation fails because `BluetoothMonitor` does not exist.

- [ ] **Step 3: Implement paired Classic and LE enumeration**

Create `src/TrayMonitor/BluetoothMonitor.cs`:

```csharp
using System.Globalization;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace TrayMonitor;

internal sealed class BluetoothMonitor
{
    private const string ConnectedProperty = "System.Devices.Aep.IsConnected";
    private const string BatteryProperty = "System.Devices.BatteryLife";
    private static readonly string[] RequestedProperties = [ConnectedProperty, BatteryProperty];

    public async Task<IReadOnlyList<BluetoothDeviceSnapshot>> GetDevicesAsync()
    {
        var classic = await DeviceInformation.FindAllAsync(
            BluetoothDevice.GetDeviceSelectorFromPairingState(true),
            RequestedProperties,
            DeviceInformationKind.AssociationEndpoint);
        var lowEnergy = await DeviceInformation.FindAllAsync(
            BluetoothLEDevice.GetDeviceSelectorFromPairingState(true),
            RequestedProperties,
            DeviceInformationKind.AssociationEndpoint);

        return classic
            .Concat(lowEnergy)
            .Select(Map)
            .GroupBy(device => device.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(device => device.IsConnected)
                .ThenByDescending(device => device.BatteryPercent.HasValue)
                .First())
            .OrderBy(device => device.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    internal static byte? NormalizeBattery(object? value)
    {
        if (value is null) return null;
        try
        {
            var number = Convert.ToInt32(value, CultureInfo.InvariantCulture);
            return number is >= 0 and <= 100 ? (byte)number : null;
        }
        catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException)
        {
            return null;
        }
    }

    private static BluetoothDeviceSnapshot Map(DeviceInformation device)
    {
        var connected = device.Properties.TryGetValue(ConnectedProperty, out var connectedValue)
            && connectedValue is true;
        device.Properties.TryGetValue(BatteryProperty, out var batteryValue);
        return new BluetoothDeviceSnapshot(
            device.Id,
            string.IsNullOrWhiteSpace(device.Name) ? "Bluetooth device" : device.Name,
            connected,
            NormalizeBattery(batteryValue));
    }
}
```

- [ ] **Step 4: Run checks and compile the WinRT calls**

Run:

```powershell
dotnet run --project tests/TrayMonitor.Checks/TrayMonitor.Checks.csproj
dotnet build src/TrayMonitor/TrayMonitor.csproj
```

Expected: checks print `All checks passed.` and the application builds with 0 errors.

- [ ] **Step 5: Commit Bluetooth enumeration**

```powershell
git add src/TrayMonitor/BluetoothMonitor.cs tests/TrayMonitor.Checks
git commit -m "feat: read Windows Bluetooth battery state"
```

### Task 5: Dynamic Battery Icon

**Files:**
- Create: `src/TrayMonitor/BatteryIcon.cs`
- Create: `tests/TrayMonitor.Checks/BatteryIconChecks.cs`
- Modify: `tests/TrayMonitor.Checks/Program.cs`

- [ ] **Step 1: Write failing native-icon smoke checks**

Create `tests/TrayMonitor.Checks/BatteryIconChecks.cs`:

```csharp
using TrayMonitor;

namespace TrayMonitor.Checks;

internal static class BatteryIconChecks
{
    public static void Run()
    {
        using var unknown = BatteryIcon.Create(null);
        using var charged = BatteryIcon.Create(75);
        Check.True(unknown.Handle != IntPtr.Zero, "unknown icon handle");
        Check.True(charged.Handle != IntPtr.Zero, "charged icon handle");
    }
}
```

Add this call before the final `Console.WriteLine` in `tests/TrayMonitor.Checks/Program.cs`:

```csharp
BatteryIconChecks.Run();
```

- [ ] **Step 2: Run the checks and verify failure**

Run:

```powershell
dotnet run --project tests/TrayMonitor.Checks/TrayMonitor.Checks.csproj
```

Expected: compilation fails because `BatteryIcon` does not exist.

- [ ] **Step 3: Implement in-memory icon drawing**

Create `src/TrayMonitor/BatteryIcon.cs`:

```csharp
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace TrayMonitor;

internal static class BatteryIcon
{
    public static Icon Create(byte? percent)
    {
        using var bitmap = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        graphics.SmoothingMode = SmoothingMode.None;

        using var darkPen = new Pen(Color.FromArgb(230, 24, 24, 24), 3);
        using var lightPen = new Pen(Color.White, 1);
        graphics.DrawRectangle(darkPen, 2, 6, 26, 20);
        graphics.DrawRectangle(lightPen, 3, 7, 24, 18);
        graphics.FillRectangle(Brushes.White, 29, 12, 3, 8);
        graphics.DrawRectangle(Pens.Black, 29, 12, 2, 7);

        if (percent is byte value)
        {
            var brush = value <= 20 ? Brushes.Red : value <= 60 ? Brushes.Gold : Brushes.LimeGreen;
            for (var segment = 0; segment < DisplayLogic.BatterySegments(value); segment++)
                graphics.FillRectangle(brush, 5 + segment * 4, 10, 3, 12);
        }
        else
        {
            using var font = new Font("Segoe UI", 15, FontStyle.Bold, GraphicsUnit.Pixel);
            graphics.DrawString("?", font, Brushes.Gray, 10, 8);
        }

        var handle = bitmap.GetHicon();
        try
        {
            return (Icon)Icon.FromHandle(handle).Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);
}
```

- [ ] **Step 4: Run the checks and verify success**

Run:

```powershell
dotnet run --project tests/TrayMonitor.Checks/TrayMonitor.Checks.csproj
```

Expected: `All checks passed.`.

- [ ] **Step 5: Commit icon generation**

```powershell
git add src/TrayMonitor/BatteryIcon.cs tests/TrayMonitor.Checks
git commit -m "feat: draw battery tray icons"
```

### Task 6: Tray Lifecycle and Configuration Menu

**Files:**
- Create: `src/TrayMonitor/TrayApplicationContext.cs`
- Modify: `src/TrayMonitor/Program.cs`

- [ ] **Step 1: Implement the single-instance entry point**

Replace `src/TrayMonitor/Program.cs` with:

```csharp
namespace TrayMonitor;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(true, @"Local\TrayMonitor", out var firstInstance);
        if (!firstInstance) return;

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationContext());
    }
}
```

- [ ] **Step 2: Implement the tray application context**

Create `src/TrayMonitor/TrayApplicationContext.cs`:

```csharp
using System.ComponentModel;

namespace TrayMonitor;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly BluetoothMonitor _monitor = new();
    private readonly SettingsStore _settingsStore = new();
    private readonly AppSettings _settings;
    private readonly ContextMenuStrip _menu = new();
    private readonly NotifyIcon _notifyIcon;
    private readonly System.Windows.Forms.Timer _refreshTimer = new() { Interval = 60_000 };
    private readonly System.Windows.Forms.Timer _cycleTimer = new() { Interval = 5_000 };
    private IReadOnlyList<BluetoothDeviceSnapshot> _devices = [];
    private Icon? _currentIcon;
    private bool _refreshing;
    private int _cycleIndex;
    private string _emptyStatus = "正在读取蓝牙设备";

    public TrayApplicationContext()
    {
        _settings = _settingsStore.Load();
        _menu.Opening += BuildMenu;
        _currentIcon = BatteryIcon.Create(null);
        _notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = _menu,
            Icon = _currentIcon,
            Text = _emptyStatus,
            Visible = true
        };
        _refreshTimer.Tick += async (_, _) => await RefreshAsync();
        _cycleTimer.Tick += (_, _) => AdvanceCycle();
        _refreshTimer.Start();
        _cycleTimer.Start();
        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (_refreshing) return;
        _refreshing = true;
        try
        {
            _devices = await _monitor.GetDevicesAsync();
            _emptyStatus = _devices.Count == 0 ? "未发现已配对蓝牙设备" : "没有可显示的设备";

            if (!_settings.Initialized)
            {
                var connected = _devices.Where(device => device.IsConnected).ToArray();
                if (connected.Length > 0)
                {
                    _settings.MonitoredDeviceIds.UnionWith(connected.Select(device => device.Id));
                    _settings.PrimaryDeviceId = connected[0].Id;
                    _settings.Initialized = true;
                    _settingsStore.Save(_settings);
                }
            }
        }
        catch
        {
            _devices = [];
            _emptyStatus = "蓝牙设备读取失败";
        }
        finally
        {
            _refreshing = false;
            UpdatePresentation();
        }
    }

    private void AdvanceCycle()
    {
        if (_settings.Mode != DisplayMode.Cycle) return;
        _cycleIndex++;
        UpdatePresentation();
    }

    private void UpdatePresentation()
    {
        var current = DisplayLogic.SelectDevice(_devices, _settings, _cycleIndex);
        var monitored = _devices
            .Where(device => _settings.MonitoredDeviceIds.Contains(device.Id))
            .ToArray();
        var icon = BatteryIcon.Create(current?.BatteryPercent);
        _notifyIcon.Icon = icon;
        _currentIcon?.Dispose();
        _currentIcon = icon;
        _notifyIcon.Text = DisplayLogic.BuildTooltip(monitored, current, _emptyStatus);
    }

    private void BuildMenu(object? sender, CancelEventArgs eventArgs)
    {
        _menu.Items.Clear();

        var modes = new ToolStripMenuItem("显示模式");
        AddMode(modes, "主设备", DisplayMode.Primary);
        AddMode(modes, "最低电量", DisplayMode.Lowest);
        AddMode(modes, "轮播", DisplayMode.Cycle);
        _menu.Items.Add(modes);

        var monitored = new ToolStripMenuItem("监控设备");
        var primary = new ToolStripMenuItem("主设备");
        foreach (var device in _devices)
        {
            var monitoredItem = new ToolStripMenuItem(DeviceLabel(device))
            {
                Checked = _settings.MonitoredDeviceIds.Contains(device.Id),
                CheckOnClick = true
            };
            monitoredItem.Click += (_, _) =>
            {
                if (monitoredItem.Checked) _settings.MonitoredDeviceIds.Add(device.Id);
                else _settings.MonitoredDeviceIds.Remove(device.Id);
                _settings.Initialized = true;
                SaveAndUpdate();
            };
            monitored.DropDownItems.Add(monitoredItem);

            var primaryItem = new ToolStripMenuItem(DeviceLabel(device))
            {
                Checked = string.Equals(
                    _settings.PrimaryDeviceId,
                    device.Id,
                    StringComparison.OrdinalIgnoreCase)
            };
            primaryItem.Click += (_, _) =>
            {
                _settings.PrimaryDeviceId = device.Id;
                _settings.MonitoredDeviceIds.Add(device.Id);
                _settings.Initialized = true;
                SaveAndUpdate();
            };
            primary.DropDownItems.Add(primaryItem);
        }

        if (_devices.Count == 0)
        {
            monitored.DropDownItems.Add(new ToolStripMenuItem("未发现设备") { Enabled = false });
            primary.DropDownItems.Add(new ToolStripMenuItem("未发现设备") { Enabled = false });
        }

        _menu.Items.Add(monitored);
        _menu.Items.Add(primary);
        _menu.Items.Add(new ToolStripSeparator());

        var refresh = new ToolStripMenuItem("立即刷新");
        refresh.Click += async (_, _) => await RefreshAsync();
        _menu.Items.Add(refresh);
        _menu.Items.Add(new ToolStripMenuItem("退出", null, (_, _) => ExitThread()));
    }

    private void AddMode(ToolStripMenuItem parent, string text, DisplayMode mode)
    {
        var item = new ToolStripMenuItem(text) { Checked = _settings.Mode == mode };
        item.Click += (_, _) =>
        {
            _settings.Mode = mode;
            _cycleIndex = 0;
            SaveAndUpdate();
        };
        parent.DropDownItems.Add(item);
    }

    private void SaveAndUpdate()
    {
        _settingsStore.Save(_settings);
        UpdatePresentation();
    }

    private static string DeviceLabel(BluetoothDeviceSnapshot device)
    {
        var state = !device.IsConnected
            ? "未连接"
            : device.BatteryPercent is byte percent ? $"{percent}%" : "电量未知";
        return $"{device.Name.Replace("&", "&&", StringComparison.Ordinal)} ({state})";
    }

    protected override void ExitThreadCore()
    {
        _refreshTimer.Stop();
        _cycleTimer.Stop();
        _refreshTimer.Dispose();
        _cycleTimer.Dispose();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _currentIcon?.Dispose();
        _menu.Dispose();
        base.ExitThreadCore();
    }
}
```

- [ ] **Step 3: Run all automated checks and build**

Run:

```powershell
dotnet run --project tests/TrayMonitor.Checks/TrayMonitor.Checks.csproj
dotnet build src/TrayMonitor/TrayMonitor.csproj
```

Expected: checks print `All checks passed.` and build completes with 0 errors.

- [ ] **Step 4: Perform the first tray smoke test**

Run:

```powershell
dotnet run --project src/TrayMonitor/TrayMonitor.csproj
```

Expected: one battery icon appears in the Windows tray; its right-click menu contains the three modes, device menus, refresh, and exit. Exit through the menu and confirm the icon disappears.

- [ ] **Step 5: Commit the tray workflow**

```powershell
git add src/TrayMonitor/Program.cs src/TrayMonitor/TrayApplicationContext.cs
git commit -m "feat: add configurable Bluetooth battery tray"
```

### Task 7: Documentation, Publish, and Final Verification

**Files:**
- Create: `README.md`

- [ ] **Step 1: Add concise user and developer documentation**

Create `README.md`:

```markdown
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
```

- [ ] **Step 2: Run the complete automated verification**

Run:

```powershell
dotnet run --project tests/TrayMonitor.Checks/TrayMonitor.Checks.csproj -c Release
dotnet build src/TrayMonitor/TrayMonitor.csproj -c Release
```

Expected: `All checks passed.` and both commands complete with 0 errors.

- [ ] **Step 3: Publish the self-contained executable**

Run:

```powershell
dotnet publish src/TrayMonitor/TrayMonitor.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o artifacts/win-x64
Get-Item artifacts/win-x64/TrayMonitor.exe
```

Expected: `TrayMonitor.exe` exists under `artifacts/win-x64`.

- [ ] **Step 4: Complete the Windows 11 hardware acceptance check**

Run `artifacts/win-x64/TrayMonitor.exe` and verify each item:

1. Only one tray icon appears, including after launching the executable a second time.
2. A connected device whose battery appears in Windows Settings shows the same percentage in the tooltip.
3. The icon band and color match the percentage.
4. Primary, Lowest, and Cycle modes update the icon as specified.
5. Device and mode selections survive exit and restart.
6. Turning Bluetooth off shows the unknown state without terminating the application.
7. Exit removes the tray icon.

- [ ] **Step 5: Commit documentation**

```powershell
git add README.md
git commit -m "docs: add TrayMonitor usage and build instructions"
```

- [ ] **Step 6: Confirm the worktree is clean**

Run:

```powershell
git status --short
```

Expected: no output. `artifacts/` remains ignored.
