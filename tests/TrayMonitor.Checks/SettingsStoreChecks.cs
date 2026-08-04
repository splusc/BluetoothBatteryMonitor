using TrayMonitor;

namespace TrayMonitor.Checks;

internal static class SettingsStoreChecks
{
    public static void Run()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"TrayMonitor-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "settings.json");
        try
        {
            var store = new SettingsStore(path);
            Check.Equal(false, store.Load().Initialized, "missing settings defaults");

            var settings = new AppSettings
            {
                Initialized = true,
                Mode = DisplayMode.Cycle,
                PrimaryDeviceId = "mouse",
                MonitoredDeviceIds = new(StringComparer.OrdinalIgnoreCase) { "mouse", "headset" }
            };
            Check.True(store.Save(settings), "settings save");

            var loaded = store.Load();
            Check.True(loaded.Initialized, "initialized round trip");
            Check.Equal(DisplayMode.Cycle, loaded.Mode, "mode round trip");
            Check.Equal("mouse", loaded.PrimaryDeviceId, "primary round trip");
            Check.True(loaded.MonitoredDeviceIds.Contains("MOUSE"), "case-insensitive IDs after load");

            File.WriteAllText(path, "{not-json");
            Check.Equal(false, store.Load().Initialized, "corrupt settings defaults");
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
