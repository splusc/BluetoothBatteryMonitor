using BluetoothBatteryMonitor.Checks;

// 手动硬件验收，只读快照，不修改用户配置。
if (args is ["--devices"])
{
    foreach (var device in await new BluetoothBatteryMonitor.BluetoothMonitor().GetDevicesAsync())
        Console.WriteLine($"{device.Name}: connected={device.IsConnected}, icon={device.IconKind}, battery={device.BatteryPercent?.ToString() ?? "unknown"}");
    return;
}

// 性能探针单独运行，便于在同一环境对比优化前后。
if (args is ["--refresh-benchmark"])
{
    TrayPresentationChecks.Run(benchmark: true);
    return;
}

DisplayLogicChecks.Run();
SettingsStoreChecks.Run();
LowBatteryChecks.Run();
AppIconChecks.Run();
LegacyDataCleanupChecks.Run();
StartupRegistrationChecks.Run();
BluetoothMonitorChecks.Run();
DeviceIconChecks.Run();
BatteryIconChecks.Run();
TrayPresentationChecks.Run();
TrayTooltipChecks.Run();
TrayMenuChecks.Run();
NativePopupMenuChecks.Run();
Console.WriteLine("All checks passed.");
// 可选输出路径仅用于导出真实图标预览，不改变应用配置。
if (args is [var previewPath]) IconPreview.Save(previewPath);
