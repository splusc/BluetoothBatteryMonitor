using BluetoothBatteryMonitor;

namespace BluetoothBatteryMonitor.Checks;

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
