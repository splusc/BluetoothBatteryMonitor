using System.Text;

namespace BluetoothBatteryMonitor;

internal enum DisplayMode
{
    Primary,
    Lowest,
    Cycle
}

internal sealed record BluetoothDeviceSnapshot(
    string Id,
    string Name,
    bool IsConnected,
    byte? BatteryPercent,
    // 自动识别的设备图形；旧调用方默认为未知设备。
    DeviceIconKind IconKind = DeviceIconKind.Unknown);

internal sealed class AppSettings
{
    public bool Initialized { get; set; }
    public DisplayMode Mode { get; set; } = DisplayMode.Lowest;
    public HashSet<string> MonitoredDeviceIds { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string? PrimaryDeviceId { get; set; }
    // 图标外观独立于主设备、最低电量和轮播模式，旧设置保持当前效果。
    public IconEffect IconEffect { get; set; } = IconEffect.Default;
    // 仅保存手动指定的内置图标，删除键即恢复自动识别。
    public Dictionary<string, DeviceIconKind> DeviceIcons { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // 未知枚举值不能阻止托盘刷新，回退到设备的自动识别结果。
    public DeviceIconKind GetIconKind(BluetoothDeviceSnapshot device) =>
        DeviceIcons.TryGetValue(device.Id, out var kind) && Enum.IsDefined(kind) ? kind : device.IconKind;
}

internal static class DisplayLogic
{
    private const int TooltipLimit = 63;

    public static BluetoothDeviceSnapshot? SelectDevice(
        IReadOnlyList<BluetoothDeviceSnapshot> devices,
        AppSettings settings,
        int cycleIndex)
    {
        var eligible = devices
            .Where(device => device.IsConnected && settings.MonitoredDeviceIds.Contains(device.Id))
            .ToArray();

        if (eligible.Length == 0) return null;

        return settings.Mode switch
        {
            DisplayMode.Primary => eligible.FirstOrDefault(
                device => string.Equals(device.Id, settings.PrimaryDeviceId, StringComparison.OrdinalIgnoreCase)),
            DisplayMode.Lowest => eligible
                .Where(device => device.BatteryPercent.HasValue)
                .MinBy(device => device.BatteryPercent) ?? eligible[0],
            DisplayMode.Cycle => eligible[Math.Max(0, cycleIndex) % eligible.Length],
            _ => null
        };
    }

    public static int BatterySegments(byte? percent) => percent switch
    {
        null => 0,
        <= 20 => 1,
        <= 40 => 2,
        <= 60 => 3,
        <= 80 => 4,
        _ => 5
    };

    public static string BuildTooltip(
        IReadOnlyList<BluetoothDeviceSnapshot> devices,
        BluetoothDeviceSnapshot? current,
        string emptyStatus)
    {
        if (devices.Count == 0) return Clip(emptyStatus, TooltipLimit);

        var ordered = current is null
            ? devices
            : new[] { current }.Concat(devices.Where(device => device.Id != current.Id)).ToArray();
        var text = new StringBuilder();

        foreach (var device in ordered)
        {
            var entry = FormatDevice(device, TooltipLimit);
            var separatorLength = text.Length == 0 ? 0 : Environment.NewLine.Length;
            if (text.Length + separatorLength + entry.Length > TooltipLimit) break;
            if (separatorLength > 0) text.AppendLine();
            text.Append(entry);
        }

        return text.Length == 0 ? Clip(emptyStatus, TooltipLimit) : text.ToString();
    }

    private static string FormatDevice(BluetoothDeviceSnapshot device, int limit)
    {
        var suffix = !device.IsConnected
            ? "未连接"
            : device.BatteryPercent is byte percent ? $"{percent}%" : "电量未知";
        var maxNameLength = Math.Max(1, limit - suffix.Length - 1);
        var name = device.Name.Length <= maxNameLength
            ? device.Name
            : Clip(device.Name, maxNameLength);
        return $"{name} {suffix}";
    }

    private static string Clip(string value, int limit) => value.Length <= limit
        ? value
        : limit <= 1 ? value[..limit] : $"{value[..(limit - 1)]}…";
}
