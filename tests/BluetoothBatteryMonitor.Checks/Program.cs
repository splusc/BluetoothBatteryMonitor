using BluetoothBatteryMonitor.Checks;

DisplayLogicChecks.Run();
SettingsStoreChecks.Run();
AppIconChecks.Run();
LegacyDataCleanupChecks.Run();
StartupRegistrationChecks.Run();
BluetoothMonitorChecks.Run();
BatteryIconChecks.Run();
Console.WriteLine("All checks passed.");
