namespace BluetoothBatteryMonitor;

// 只跟踪当前进程内的低电量状态，重启后允许提醒仍然电量不足的设备。
internal sealed class LowBatteryReminder
{
    private readonly HashSet<string> _notified = new(StringComparer.OrdinalIgnoreCase); // 每台设备分别去重。

    // 仅可靠的高电量读数重新启用提醒，断连或读取失败不会造成通知反复出现。
    public IReadOnlyList<BluetoothDeviceSnapshot> GetNewAlerts(
        IReadOnlyList<BluetoothDeviceSnapshot> devices, AppSettings settings)
    {
        var alerts = new List<BluetoothDeviceSnapshot>();
        foreach (var device in devices)
        {
            if (!device.IsConnected || !settings.MonitoredDeviceIds.Contains(device.Id)
                || device.BatteryPercent is not byte percent || percent > 100) continue;
            if (percent > settings.LowBatteryThreshold)
            {
                _notified.Remove(device.Id);
            }
            else if (_notified.Add(device.Id))
            {
                alerts.Add(device);
            }
        }
        return alerts;
    }
}
