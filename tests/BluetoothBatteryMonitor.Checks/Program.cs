using BluetoothBatteryMonitor.Checks;

// 性能探针单独运行，便于在同一环境对比优化前后。
if (args is ["--refresh-benchmark"])
{
    TrayPresentationChecks.Run(benchmark: true);
    return;
}

DisplayLogicChecks.Run();
SettingsStoreChecks.Run();
AppIconChecks.Run();
LegacyDataCleanupChecks.Run();
StartupRegistrationChecks.Run();
BluetoothMonitorChecks.Run();
DeviceIconChecks.Run();
BatteryIconChecks.Run();
TrayPresentationChecks.Run();
Console.WriteLine("All checks passed.");
// 可选输出路径仅用于导出真实图标预览，不改变应用配置。
if (args is [var previewPath]) IconPreview.Save(previewPath);
