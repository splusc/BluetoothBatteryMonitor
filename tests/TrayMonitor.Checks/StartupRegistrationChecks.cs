using Microsoft.Win32;
using TrayMonitor;

namespace TrayMonitor.Checks;

internal static class StartupRegistrationChecks
{
    public static void Run()
    {
        var keyPath = $"Software\\TrayMonitor.Tests\\{Guid.NewGuid():N}";
        var environmentVariable = $"TrayMonitorTestsProgramFiles{Guid.NewGuid():N}";
        var previousEnvironmentValue = Environment.GetEnvironmentVariable(environmentVariable, EnvironmentVariableTarget.Process);
        const string executablePath = @"C:\Program Files\TrayMonitor\TrayMonitor.exe";
        const string command = "\"C:\\Program Files\\TrayMonitor\\TrayMonitor.exe\"";

        try
        {
            Environment.SetEnvironmentVariable(environmentVariable, @"C:\Program Files", EnvironmentVariableTarget.Process);
            var registration = new StartupRegistration(keyPath, executablePath);
            Check.Equal(false, registration.IsEnabled(), "startup defaults off");
            Check.True(registration.SetEnabled(true), "startup enable");
            Check.True(registration.IsEnabled(), "startup enabled");

            using (var key = Registry.CurrentUser.OpenSubKey(keyPath))
                Check.Equal(command, key!.GetValue("TrayMonitor") as string, "startup command");

            using (var key = Registry.CurrentUser.OpenSubKey(keyPath, writable: true))
                key!.SetValue("TrayMonitor", $"\"%{environmentVariable}%\\TrayMonitor\\TrayMonitor.exe\"", RegistryValueKind.ExpandString);
            Check.Equal(false, registration.IsEnabled(), "startup expanded command disabled");

            using (var key = Registry.CurrentUser.OpenSubKey(keyPath, writable: true))
                key!.SetValue("TrayMonitor", "\"C:\\Other\\TrayMonitor.exe\"", RegistryValueKind.String);
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
