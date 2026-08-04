using TrayMonitor.Checks;

DisplayLogicChecks.Run();
SettingsStoreChecks.Run();
BluetoothMonitorChecks.Run();
BatteryIconChecks.Run();
Console.WriteLine("All checks passed.");
