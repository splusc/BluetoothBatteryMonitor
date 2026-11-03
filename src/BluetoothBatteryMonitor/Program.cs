namespace BluetoothBatteryMonitor;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        using var mutex = new Mutex(true, @"Local\BluetoothBatteryMonitor", out var firstInstance);
        if (!firstInstance) return;

        LegacyDataCleanup.Run();
        ApplicationConfiguration.Initialize();
        // 视觉验收可先仅显示基础设备图形，不修改保存的样式或监控设置。
        Application.Run(new TrayApplicationContext(showBattery: !args.Contains("--base-icon")));
    }
}
