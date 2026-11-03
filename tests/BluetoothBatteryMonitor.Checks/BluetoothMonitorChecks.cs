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
    }
}
