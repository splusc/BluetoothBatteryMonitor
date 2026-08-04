using Microsoft.Win32;
using TrayMonitor;

namespace TrayMonitor.Checks;

internal static class StartupRegistrationChecks
{
    public static void Run()
    {
        var keyPath = $"Software\\TrayMonitor.Tests\\{Guid.NewGuid():N}";
        const string executablePath = @"C:\Program Files\TrayMonitor\TrayMonitor.exe";
        const string command = "\"C:\\Program Files\\TrayMonitor\\TrayMonitor.exe\"";

        try
        {
            var registration = new StartupRegistration(keyPath, executablePath);
            Check.Equal(false, registration.IsEnabled(), "startup defaults off");
            Check.True(registration.SetEnabled(true), "startup enable");
            Check.True(registration.IsEnabled(), "startup enabled");

            using (var key = Registry.CurrentUser.OpenSubKey(keyPath))
                Check.Equal(command, key!.GetValue("TrayMonitor") as string, "startup command");

            using (var key = Registry.CurrentUser.OpenSubKey(keyPath, writable: true))
                key!.SetValue("TrayMonitor", "\"C:\\Other\\TrayMonitor.exe\"", RegistryValueKind.String);
            Check.Equal(false, registration.IsEnabled(), "startup changed command disabled");

            Check.True(registration.SetEnabled(false), "startup disable");
            Check.Equal(false, registration.IsEnabled(), "startup disabled");
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(keyPath, throwOnMissingSubKey: false);
        }
    }
}
