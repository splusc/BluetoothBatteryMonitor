using Microsoft.Win32;
using BluetoothBatteryMonitor;

namespace BluetoothBatteryMonitor.Checks;

internal static class LegacyDataCleanupChecks
{
    public static void Run()
    {
        var root = Path.Combine(Path.GetTempPath(), $"BluetoothBatteryMonitor-{Guid.NewGuid():N}");
        var legacyDirectory = Path.Combine(root, "TrayMonitor");
        var keyPath = $"Software\\BluetoothBatteryMonitor.Tests\\{Guid.NewGuid():N}";

        try
        {
            Directory.CreateDirectory(legacyDirectory);
            File.WriteAllText(Path.Combine(legacyDirectory, "settings.json"), "{}");
            using (var key = Registry.CurrentUser.CreateSubKey(keyPath, writable: true))
            {
                key.SetValue("TrayMonitor", "old-command");
                key.SetValue("KeepMe", "other-command");
            }

            LegacyDataCleanup.Run(legacyDirectory, keyPath);

            Check.Equal(false, Directory.Exists(legacyDirectory), "legacy settings removed");
            using var remaining = Registry.CurrentUser.OpenSubKey(keyPath);
            Check.Equal(null, remaining!.GetValue("TrayMonitor"), "legacy startup removed");
            Check.Equal("other-command", remaining.GetValue("KeepMe") as string, "unrelated startup preserved");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            Registry.CurrentUser.DeleteSubKeyTree(keyPath, throwOnMissingSubKey: false);
        }
    }
}
