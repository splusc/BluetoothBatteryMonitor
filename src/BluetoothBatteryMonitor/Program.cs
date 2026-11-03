namespace BluetoothBatteryMonitor;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(true, @"Local\BluetoothBatteryMonitor", out var firstInstance);
        if (!firstInstance) return;

        LegacyDataCleanup.Run();
        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationContext());
    }
}
