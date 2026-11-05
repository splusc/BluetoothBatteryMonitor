using System.Text.Json;
using System.Text.Json.Serialization;

namespace BluetoothBatteryMonitor;

internal sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _path;

    public SettingsStore(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BluetoothBatteryMonitor",
            "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_path)) return new AppSettings();
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), JsonOptions)
                ?? new AppSettings();
            settings.MonitoredDeviceIds = new HashSet<string>(
                settings.MonitoredDeviceIds ?? [],
                StringComparer.OrdinalIgnoreCase);
            // 反序列化不会保留字典比较器，设备 ID 必须继续忽略大小写。
            var icons = new Dictionary<string, DeviceIconKind>(StringComparer.OrdinalIgnoreCase);
            foreach (var (id, kind) in settings.DeviceIcons ?? []) icons[id] = kind;
            settings.DeviceIcons = icons;
            if (!Enum.IsDefined(settings.IconEffect)) settings.IconEffect = IconEffect.Default;
            // 手动修改配置产生的越界值回退默认阈值。
            if (settings.LowBatteryThreshold is < 1 or > 100) settings.LowBatteryThreshold = 20;
            // 分段必须覆盖三种颜色，非法配置整体回退当前默认值。
            if (settings.RedBatteryThreshold < 0 || settings.YellowBatteryThreshold > 99
                || settings.RedBatteryThreshold >= settings.YellowBatteryThreshold)
            {
                settings.RedBatteryThreshold = 20;
                settings.YellowBatteryThreshold = 60;
            }
            return settings;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return new AppSettings();
        }
    }

    public bool Save(AppSettings settings)
    {
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(_path, JsonSerializer.Serialize(settings, JsonOptions));
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
