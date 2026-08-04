using TrayMonitor.Checks;

DisplayLogicChecks.Run();
SettingsStoreChecks.Run();
AppIconChecks.Run();
StartupRegistrationChecks.Run();
BluetoothMonitorChecks.Run();
BatteryIconChecks.Run();
Console.WriteLine("All checks passed.");
