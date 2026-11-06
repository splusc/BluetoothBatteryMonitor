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
    DeviceIconKind IconKind = DeviceIconKind.Unknown,
    // 同一物理设备的旧端点 ID，仅用于兼容已保存的监控、主设备和图标选择。
    IReadOnlyList<string>? EndpointIds = null);

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
    // 低电量通知阈值（含边界），不改变托盘图标既有的颜色分段。
    public int LowBatteryThreshold { get; set; } = 20;
    // 电量小于等于此值显示红色，独立于通知阈值。
    public int RedBatteryThreshold { get; set; } = 20;
    // 高于红色上限且小于等于此值显示黄色，其余显示绿色。
    public int YellowBatteryThreshold { get; set; } = 60;

    // 未知枚举值不能阻止托盘刷新，回退到设备的自动识别结果。
    public DeviceIconKind GetIconKind(BluetoothDeviceSnapshot device) =>
        DeviceIcons.TryGetValue(device.Id, out var kind) && Enum.IsDefined(kind) ? kind : device.IconKind;

    // 端点合并后迁移旧设置，避免已监控的 BLE 端点消失或重复显示。
    public bool MergeDeviceIds(IReadOnlyList<BluetoothDeviceSnapshot> devices)
    {
        var changed = false;
        foreach (var device in devices)
        {
            // 两个旧端点曾指定不同图标时，优先延续原主设备的选择；新的容器配置优先级最高。
            if (!DeviceIcons.ContainsKey(device.Id) && PrimaryDeviceId is not null
                && (device.EndpointIds?.Contains(PrimaryDeviceId, StringComparer.OrdinalIgnoreCase) ?? false)
                && DeviceIcons.TryGetValue(PrimaryDeviceId, out var primaryIcon))
            {
                DeviceIcons[device.Id] = primaryIcon;
                changed = true;
            }
            foreach (var endpointId in device.EndpointIds ?? [])
            {
                if (string.Equals(endpointId, device.Id, StringComparison.OrdinalIgnoreCase)) continue;
                if (MonitoredDeviceIds.Remove(endpointId))
                {
                    MonitoredDeviceIds.Add(device.Id);
                    changed = true;
                }
                if (string.Equals(PrimaryDeviceId, endpointId, StringComparison.OrdinalIgnoreCase))
                {
                    PrimaryDeviceId = device.Id;
                    changed = true;
                }
                if (DeviceIcons.Remove(endpointId, out var icon))
                {
                    DeviceIcons.TryAdd(device.Id, icon);
                    changed = true;
                }
            }
        }
        return changed;
    }
}

internal static class DisplayLogic
{
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
        string emptyStatus)
    {
        if (devices.Count == 0) return emptyStatus;

        // 提示列表独立于图标轮播；未知电量和断连置后，同电量按名称、ID 固定顺序。
        var ordered = devices
            .OrderBy(device => device.IsConnected ? device.BatteryPercent ?? int.MaxValue : int.MaxValue)
            .ThenByDescending(device => device.IsConnected)
            .ThenBy(device => device.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(device => device.Id, StringComparer.OrdinalIgnoreCase);
        // 完整文字交给可滚动悬停窗口，不能按系统托盘提示的字符上限丢弃设备。
        return string.Join(Environment.NewLine, ordered.Select(FormatDevice));
    }

    private static string FormatDevice(BluetoothDeviceSnapshot device)
    {
        var suffix = !device.IsConnected
            ? "未连接"
            : device.BatteryPercent is byte percent ? $"{percent}%" : "电量未知";
        return $"{device.Name} {suffix}";
    }
}
