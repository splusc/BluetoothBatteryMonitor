using Microsoft.Win32;
using System.Security;

namespace BluetoothBatteryMonitor;

internal static class LegacyDataCleanup
{
    private const string DefaultStartupKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static void Run(string? settingsDirectory = null, string? startupKeyPath = null)
    {
        settingsDirectory ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TrayMonitor");
        startupKeyPath ??= DefaultStartupKeyPath;

        try
        {
            if (Directory.Exists(settingsDirectory)) Directory.Delete(settingsDirectory, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(startupKeyPath, writable: true);
            key?.DeleteValue("TrayMonitor", throwOnMissingValue: false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException)
        {
        }
    }
}
