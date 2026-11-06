using BluetoothBatteryMonitor;

namespace BluetoothBatteryMonitor.Checks;

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
        Check.Equal("unknown", DisplayLogic.SelectDevice([devices[3]], settings, 0)?.Id,
            "unknown-only selection keeps the connected device icon");
        Check.True(
            DisplayLogic.SelectDevice(Array.Empty<BluetoothDeviceSnapshot>(), settings, 0) is null,
            "empty device selection");

        settings.Mode = DisplayMode.Cycle;
        Check.Equal("mouse", DisplayLogic.SelectDevice(devices, settings, 1)?.Id, "cycle index");
        Check.Equal("headset", DisplayLogic.SelectDevice([devices[0], devices[1]], settings, 2)?.Id, "cycle reduced list wraps");

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
            new BluetoothDeviceSnapshot("offline", "C", false, 1),
            new BluetoothDeviceSnapshot("low", "D", true, 25),
            new BluetoothDeviceSnapshot("same-level", "E", true, 25),
            new BluetoothDeviceSnapshot("empty", "F", true, 0)
        };
        var multiTooltip = DisplayLogic.BuildTooltip(tooltipDevices, "No devices");
        var sortedTooltip = string.Join(Environment.NewLine, "F 0%", "D 25%", "E 25%", "A 85%", "B 电量未知", "C 未连接");
        Check.Equal(sortedTooltip, multiTooltip, "提示按电量升序，同电量按名称，未知和断连置后");
        Check.Equal(sortedTooltip, DisplayLogic.BuildTooltip(tooltipDevices.Reverse().ToArray(), "No devices"),
            "扫描顺序变化不改变提示排序");

        var tooltip = DisplayLogic.BuildTooltip(
            [new BluetoothDeviceSnapshot("long", new string('X', 100), true, 85)],
            "No devices");
        Check.True(tooltip.Contains(new string('X', 100)), "悬停列表保留完整设备名");
        Check.True(tooltip.EndsWith("85%", StringComparison.Ordinal), "tooltip keeps battery suffix");

        var disconnectedTooltip = DisplayLogic.BuildTooltip(
            [new BluetoothDeviceSnapshot("long-offline", new string('X', 100), false, null)],
            "No devices");
        Check.True(disconnectedTooltip.Contains(new string('X', 100)), "未连接设备名不截断");
        Check.True(disconnectedTooltip.EndsWith("未连接", StringComparison.Ordinal), "tooltip keeps disconnected suffix");

        var unknownTooltip = DisplayLogic.BuildTooltip(
            [new BluetoothDeviceSnapshot("long-unknown", new string('X', 100), true, null)],
            "No devices");
        Check.True(unknownTooltip.Contains(new string('X', 100)), "未知电量设备名不截断");
        Check.True(unknownTooltip.EndsWith("电量未知", StringComparison.Ordinal), "tooltip keeps unknown suffix");

        // 三台以上长名称设备也必须逐行保留，不能碰到字符限制就丢弃后续设备。
        var manyDevices = Enumerable.Range(1, 12)
            .Select(index => new BluetoothDeviceSnapshot($"device-{index}", $"蓝牙设备 {index} {new string('长', 80)}", true, 50))
            .ToArray();
        var fullTooltip = DisplayLogic.BuildTooltip(manyDevices, "无设备");
        Check.Equal(12, fullTooltip.Split(Environment.NewLine).Length, "完整列出全部监控设备");
        foreach (var device in manyDevices)
            Check.True(fullTooltip.Contains($"{device.Name} 50%"), "所有设备名与电量均完整保留");
    }
}
