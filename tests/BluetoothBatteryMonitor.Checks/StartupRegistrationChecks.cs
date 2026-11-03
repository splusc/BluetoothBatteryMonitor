using Microsoft.Win32;
using BluetoothBatteryMonitor;

namespace BluetoothBatteryMonitor.Checks;

internal static class StartupRegistrationChecks
{
    public static void Run()
    {
        var keyPath = $"Software\\BluetoothBatteryMonitor.Tests\\{Guid.NewGuid():N}";
        var environmentVariable = $"BluetoothBatteryMonitorTestsProgramFiles{Guid.NewGuid():N}";
        var previousEnvironmentValue = Environment.GetEnvironmentVariable(environmentVariable, EnvironmentVariableTarget.Process);
        const string executablePath = @"C:\Program Files\BluetoothBatteryMonitor\BluetoothBatteryMonitor.exe";
        const string command = "\"C:\\Program Files\\BluetoothBatteryMonitor\\BluetoothBatteryMonitor.exe\"";

        try
        {
            Environment.SetEnvironmentVariable(environmentVariable, @"C:\Program Files", EnvironmentVariableTarget.Process);
            var registration = new StartupRegistration(keyPath, executablePath);
            Check.Equal(false, registration.IsEnabled(), "startup defaults off");
            Check.True(registration.SetEnabled(true), "startup enable");
            Check.True(registration.IsEnabled(), "startup enabled");

            using (var key = Registry.CurrentUser.OpenSubKey(keyPath))
                Check.Equal(command, key!.GetValue("BluetoothBatteryMonitor") as string, "startup command");

            using (var key = Registry.CurrentUser.OpenSubKey(keyPath, writable: true))
                key!.SetValue("BluetoothBatteryMonitor", $"\"%{environmentVariable}%\\BluetoothBatteryMonitor\\BluetoothBatteryMonitor.exe\"", RegistryValueKind.ExpandString);
            Check.Equal(false, registration.IsEnabled(), "startup expanded command disabled");

            using (var key = Registry.CurrentUser.OpenSubKey(keyPath, writable: true))
                key!.SetValue("BluetoothBatteryMonitor", "\"C:\\Other\\BluetoothBatteryMonitor.exe\"", RegistryValueKind.String);
            Check.Equal(false, registration.IsEnabled(), "startup changed command disabled");

            Check.True(registration.SetEnabled(false), "startup disable");
            Check.Equal(false, registration.IsEnabled(), "startup disabled");
        }
        finally
        {
            Environment.SetEnvironmentVariable(environmentVariable, previousEnvironmentValue, EnvironmentVariableTarget.Process);
            Registry.CurrentUser.DeleteSubKeyTree(keyPath, throwOnMissingSubKey: false);
        }
    }
}
