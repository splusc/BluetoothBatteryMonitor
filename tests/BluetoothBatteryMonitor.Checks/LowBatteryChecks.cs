using BluetoothBatteryMonitor;

namespace BluetoothBatteryMonitor.Checks;

// 覆盖阈值边界、每设备去重和充电后重新提醒，避免每分钟打扰用户。
internal static class LowBatteryChecks
{
    public static void Run()
    {
        var reminder = new LowBatteryReminder();
        var settings = new AppSettings { MonitoredDeviceIds = new(StringComparer.OrdinalIgnoreCase) { "mouse", "keyboard" } };
        var mouse = new BluetoothDeviceSnapshot("mouse", "鼠标", true, 21);
        Check.Equal(0, reminder.GetNewAlerts([mouse], settings).Count, "高于默认阈值不提醒");
        mouse = mouse with { BatteryPercent = 20 };
        Check.Equal(1, reminder.GetNewAlerts([mouse], settings).Count, "等于阈值时提醒");
        Check.Equal(0, reminder.GetNewAlerts([mouse with { Id = "MOUSE", BatteryPercent = 10 }], settings).Count, "同设备持续低电量不重复提醒");
        reminder.GetNewAlerts([], settings);
        reminder.GetNewAlerts([mouse with { IsConnected = false }], settings);
        reminder.GetNewAlerts([mouse with { BatteryPercent = null }], settings);
        Check.Equal(0, reminder.GetNewAlerts([mouse], settings).Count, "暂时消失、断连或未知电量不重置提醒");
        reminder.GetNewAlerts([mouse with { BatteryPercent = 21 }], settings);
        Check.Equal(1, reminder.GetNewAlerts([mouse], settings).Count, "回升后再次降到阈值可以提醒");
        Check.Equal(0, reminder.GetNewAlerts([new("other", "未监控设备", true, 0)], settings).Count, "不提醒未监控设备");
        var keyboard = new BluetoothDeviceSnapshot("keyboard", "键盘", false, 0);
        Check.Equal(0, reminder.GetNewAlerts([keyboard], settings).Count, "不提醒未连接设备");
        Check.Equal(0, reminder.GetNewAlerts([keyboard with { IsConnected = true, BatteryPercent = null }], settings).Count, "不提醒未知电量");
        Check.Equal(0, reminder.GetNewAlerts([keyboard with { IsConnected = true, BatteryPercent = 101 }], settings).Count, "不提醒超出百分比范围的读数");
        Check.Equal(1, reminder.GetNewAlerts([keyboard with { IsConnected = true }], settings).Count, "另一设备零电量独立提醒");

        var custom = new LowBatteryReminder();
        settings.LowBatteryThreshold = 35;
        Check.Equal(0, custom.GetNewAlerts([mouse with { BatteryPercent = 36 }], settings).Count, "使用自定义阈值上界");
        Check.Equal(2, custom.GetNewAlerts([mouse with { BatteryPercent = 35 }, keyboard with { IsConnected = true }], settings).Count, "同次刷新收集所有新低电量设备");
        settings.LowBatteryThreshold = 10;
        custom.GetNewAlerts([mouse], settings);
        Check.Equal(1, custom.GetNewAlerts([mouse with { BatteryPercent = 10 }], settings).Count, "降低阈值后按新阈值重新判断");
    }
}
