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
