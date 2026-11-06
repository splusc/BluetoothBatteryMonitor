using BluetoothBatteryMonitor;

namespace BluetoothBatteryMonitor.Checks;

internal static class BluetoothMonitorChecks
{
    public static void Run()
    {
        Check.Equal((byte?)55, BluetoothMonitor.NormalizeBattery((byte)55), "byte battery");
        Check.Equal((byte?)80, BluetoothMonitor.NormalizeBattery((uint)80), "convertible battery");
        Check.Equal<byte?>(null, BluetoothMonitor.NormalizeBattery((byte)101), "unknown sentinel");
        Check.Equal<byte?>(null, BluetoothMonitor.NormalizeBattery(null), "missing battery");

        // 耳塞的经典蓝牙和 BLE 端点必须按容器共享 Hands-Free 子节点上的电量。
        var container = Guid.NewGuid();
        var otherContainer = Guid.NewGuid();
        var batteries = new Dictionary<Guid, byte> { [container] = 90, [otherContainer] = 40, [Guid.Empty] = 60 };
        var properties = new Dictionary<string, object> { ["System.Devices.Aep.ContainerId"] = container };
        Check.Equal<byte?>(90, BluetoothMonitor.ResolveBattery(properties, batteries), "端点电量缺失时使用同容器电量");
        properties["System.Devices.BatteryLife"] = (byte)80;
        Check.Equal<byte?>(80, BluetoothMonitor.ResolveBattery(properties, batteries), "端点已有电量时保持原值");
        properties["System.Devices.BatteryLife"] = (byte)255;
        Check.Equal<byte?>(90, BluetoothMonitor.ResolveBattery(properties, batteries), "无效电量哨兵仍可回退");
        properties["System.Devices.Aep.ContainerId"] = Guid.NewGuid();
        Check.Equal<byte?>(null, BluetoothMonitor.ResolveBattery(properties, batteries), "不能借用其他容器的电量");
        properties["System.Devices.Aep.ContainerId"] = Guid.Empty;
        Check.Equal<byte?>(null, BluetoothMonitor.ResolveBattery(properties, batteries), "空容器不能关联电量");
        properties["System.Devices.Aep.ContainerId"] = "无效容器";
        Check.Equal<byte?>(null, BluetoothMonitor.ResolveBattery(properties, batteries), "无效容器保留未知电量");

        // 两条 Bose 记录是协议端点，不能冒充左右耳；相同名称但不同容器必须保留。
        var endpoints = new[]
        {
            new BluetoothDeviceSnapshot("classic", "Bose QC 消噪耳塞 Ultra", false, 90, DeviceIconKind.Headphones),
            new BluetoothDeviceSnapshot("ble", "Bose QC 消噪耳塞 Ultra", true, 90),
            new BluetoothDeviceSnapshot("another", "Bose QC 消噪耳塞 Ultra", true, 60, DeviceIconKind.Headphones),
            new BluetoothDeviceSnapshot("no-container", "其他设备", true, 50)
        };
        var containers = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase)
        {
            ["classic"] = container, ["ble"] = container, ["another"] = otherContainer
        };
        var merged = BluetoothMonitor.MergeDeviceSnapshots(endpoints, containers);
        Check.Equal(3, merged.Count, "同容器经典蓝牙与 BLE 合并为一副耳机");
        var bose = merged.Single(device => device.BatteryPercent == 90);
        Check.True(bose.IsConnected, "任一端点已连接即显示已连接");
        Check.Equal(DeviceIconKind.Headphones, bose.IconKind, "合并后保留经典端点提供的耳机类型");
        Check.True(merged.Any(device => device.Id == "no-container"), "缺少容器的设备不误合并");

        var settings = new AppSettings
        {
            Initialized = true, Mode = DisplayMode.Primary, PrimaryDeviceId = "BLE",
            MonitoredDeviceIds = new(StringComparer.OrdinalIgnoreCase) { "ble", "classic", "another" },
            DeviceIcons = new(StringComparer.OrdinalIgnoreCase)
            {
                ["classic"] = DeviceIconKind.Mouse, ["ble"] = DeviceIconKind.Headphones
            }
        };
        Check.True(settings.MergeDeviceIds(merged), "旧端点选择迁移到物理设备");
        Check.Equal(2, settings.MonitoredDeviceIds.Count, "双端点的监控选择合并且保留其他设备");
        Check.Equal(bose.Id, DisplayLogic.SelectDevice(merged, settings, 0)?.Id, "原 BLE 主设备仍可选中");
        Check.Equal(DeviceIconKind.Headphones, settings.DeviceIcons[bose.Id], "端点图标冲突时优先保留原主设备的手动图标");
        Check.True(!settings.MergeDeviceIds(merged), "重复刷新不会反复迁移设置");
        settings.Mode = DisplayMode.Cycle;
        Check.True(DisplayLogic.SelectDevice(merged, settings, 0)?.Id != DisplayLogic.SelectDevice(merged, settings, 1)?.Id,
            "轮播只经过两台不同物理设备，不重复经过双端点");
        settings.LowBatteryThreshold = 95;
        Check.Equal(2, new LowBatteryReminder().GetNewAlerts(merged, settings).Count, "低电量提醒不重复报告耳机端点");

        var report = new byte[78];
        report[0] = 0x11;
        report[30] = 2; // USB 与蓝牙偏移不同，不能读取此处。
        report[32] = 7;
        Check.Equal<byte?>(87, DualShockBatteryReader.ParseBluetoothReport(report), "DS4 档位 7 与 DS4Windows 一致，不能显示 75%");
        report[32] = 0;
        Check.Equal<byte?>(0, DualShockBatteryReader.ParseBluetoothReport(report), "DS4 零电量");
        report[32] = 10;
        Check.Equal<byte?>(100, DualShockBatteryReader.ParseBluetoothReport(report), "DS4 满电");
        report[32] = 0x15;
        Check.Equal<byte?>(45, DualShockBatteryReader.ParseBluetoothReport(report), "DS4 充电使用 11 档满量程");
        foreach (var (status, expected) in new (byte Status, byte Expected)[]
        {
            (0x01, 12), (0x04, 50), (0x07, 87), (0x08, 100), (0x09, 100), (0x0A, 100),
            (0x10, 0), (0x11, 9), (0x17, 63), (0x18, 72), (0x1A, 90), (0x1B, 100)
        })
        {
            report[32] = status;
            Check.Equal<byte?>(expected, DualShockBatteryReader.ParseBluetoothReport(report),
                $"DS4 状态 0x{status:X2} 与 DS4Windows 换算一致");
        }
        report[32] = 0x1B;
        Check.Equal<byte?>(100, DualShockBatteryReader.ParseBluetoothReport(report), "DS4 充电完成");
        report[32] = 0x1E;
        Check.Equal<byte?>(null, DualShockBatteryReader.ParseBluetoothReport(report), "DS4 充电错误不能显示满电");
        report[32] = 0x1F;
        Check.Equal<byte?>(null, DualShockBatteryReader.ParseBluetoothReport(report), "DS4 电池故障不能显示满电");
        Check.Equal<byte?>(null, DualShockBatteryReader.ParseBluetoothReport(report.AsSpan(0, 33)), "DS4 截断报告");
        report[0] = 1;
        Check.Equal<byte?>(null, DualShockBatteryReader.ParseBluetoothReport(report), "DS4 简短报告没有电量");
    }
}
