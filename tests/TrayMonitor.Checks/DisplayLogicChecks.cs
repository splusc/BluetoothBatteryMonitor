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
            new BluetoothDeviceSnapshot("unknown", "Unknown", true, null),
            new BluetoothDeviceSnapshot("tracker", "Tracker", true, 1)
        };
        var settings = new AppSettings
        {
            Initialized = true,
            PrimaryDeviceId = "HEADSET",
            MonitoredDeviceIds = new(StringComparer.OrdinalIgnoreCase)
            {
                "headset", "mouse", "keyboard", "unknown"
            }
        };

        settings.Mode = DisplayMode.Primary;
        Check.Equal("headset", DisplayLogic.SelectDevice(devices, settings, 0)?.Id, "case-insensitive primary device");

        settings.Mode = DisplayMode.Lowest;
        Check.Equal("mouse", DisplayLogic.SelectDevice(devices, settings, 0)?.Id, "lowest ignores unmonitored device");
        Check.True(
            DisplayLogic.SelectDevice([devices[3]], settings, 0) is null,
            "lowest unknown-only selection");
        Check.True(
            DisplayLogic.SelectDevice(Array.Empty<BluetoothDeviceSnapshot>(), settings, 0) is null,
            "empty device selection");

        settings.Mode = DisplayMode.Cycle;
        Check.Equal("mouse", DisplayLogic.SelectDevice(devices, settings, 1)?.Id, "cycle index");
        Check.Equal("mouse", DisplayLogic.SelectDevice([devices[1]], settings, int.MaxValue)?.Id, "cycle reduced list wraps");

        Check.Equal(1, DisplayLogic.BatterySegments(0), "0 percent band");
        Check.Equal(1, DisplayLogic.BatterySegments(20), "20 percent band");
        Check.Equal(2, DisplayLogic.BatterySegments(21), "21 percent band");
        Check.Equal(2, DisplayLogic.BatterySegments(40), "40 percent band");
        Check.Equal(3, DisplayLogic.BatterySegments(41), "41 percent band");
        Check.Equal(3, DisplayLogic.BatterySegments(60), "60 percent band");
        Check.Equal(4, DisplayLogic.BatterySegments(61), "61 percent band");
        Check.Equal(4, DisplayLogic.BatterySegments(80), "80 percent band");
        Check.Equal(5, DisplayLogic.BatterySegments(81), "81 percent band");
        Check.Equal(5, DisplayLogic.BatterySegments(100), "100 percent band");
        Check.Equal(0, DisplayLogic.BatterySegments(null), "unknown band");

        var tooltipDevices = new[]
        {
            new BluetoothDeviceSnapshot("known", "A", true, 85),
            new BluetoothDeviceSnapshot("unknown", "B", true, null),
            new BluetoothDeviceSnapshot("offline", "C", false, null)
        };
        var multiTooltip = DisplayLogic.BuildTooltip(tooltipDevices, tooltipDevices[2], "No devices");
        Check.True(
            multiTooltip.StartsWith($"C 未连接{Environment.NewLine}", StringComparison.Ordinal),
            "tooltip puts current device first");
        Check.True(multiTooltip.Contains("B 电量未知", StringComparison.Ordinal), "tooltip includes unknown battery");

        var tooltip = DisplayLogic.BuildTooltip(
            [new BluetoothDeviceSnapshot("long", new string('X', 100), true, 85)],
            null,
            "No devices");
        Check.True(tooltip.Length <= 63, "tooltip length");
        Check.True(tooltip.EndsWith("85%", StringComparison.Ordinal), "tooltip keeps battery suffix");

        var disconnectedTooltip = DisplayLogic.BuildTooltip(
            [new BluetoothDeviceSnapshot("long-offline", new string('X', 100), false, null)],
            null,
            "No devices");
        Check.True(disconnectedTooltip.Length <= 63, "disconnected tooltip length");
        Check.True(disconnectedTooltip.EndsWith("未连接", StringComparison.Ordinal), "tooltip keeps disconnected suffix");

        var unknownTooltip = DisplayLogic.BuildTooltip(
            [new BluetoothDeviceSnapshot("long-unknown", new string('X', 100), true, null)],
            null,
            "No devices");
        Check.True(unknownTooltip.Length <= 63, "unknown tooltip length");
        Check.True(unknownTooltip.EndsWith("电量未知", StringComparison.Ordinal), "tooltip keeps unknown suffix");
    }
}
