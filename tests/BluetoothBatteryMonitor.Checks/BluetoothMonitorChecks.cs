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
