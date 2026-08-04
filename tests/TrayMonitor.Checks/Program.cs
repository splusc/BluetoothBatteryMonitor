using TrayMonitor.Checks;

DisplayLogicChecks.Run();
SettingsStoreChecks.Run();
BluetoothMonitorChecks.Run();
Console.WriteLine("All checks passed.");
