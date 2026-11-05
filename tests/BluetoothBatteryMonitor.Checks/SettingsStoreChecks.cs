using BluetoothBatteryMonitor;

namespace BluetoothBatteryMonitor.Checks;

internal static class SettingsStoreChecks
{
    public static void Run()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"BluetoothBatteryMonitor-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "settings.json");
        try
        {
            var store = new SettingsStore(path);
            Check.Equal(false, store.Load().Initialized, "missing settings defaults");
            Check.Equal(IconEffect.Default, store.Load().IconEffect, "新设置保留当前图标效果");
            Check.Equal(20, store.Load().LowBatteryThreshold, "首次使用默认在 20% 提醒");

            var settings = new AppSettings
            {
                Initialized = true,
                Mode = DisplayMode.Cycle,
                IconEffect = IconEffect.Personalized,
                LowBatteryThreshold = 35,
                RedBatteryThreshold = 30,
                YellowBatteryThreshold = 70,
                DeviceIcons = new(StringComparer.OrdinalIgnoreCase) { ["controller"] = DeviceIconKind.DualSense },
                PrimaryDeviceId = "mouse",
                MonitoredDeviceIds = new(StringComparer.OrdinalIgnoreCase) { "mouse", "headset" }
            };
            Check.True(store.Save(settings), "settings save");

            var loaded = store.Load();
            Check.True(loaded.Initialized, "initialized round trip");
            Check.Equal(DisplayMode.Cycle, loaded.Mode, "mode round trip");
            Check.Equal(IconEffect.Personalized, loaded.IconEffect, "图标效果持久化");
            Check.Equal(35, loaded.LowBatteryThreshold, "低电量阈值重启后保留");
            Check.Equal(30, loaded.RedBatteryThreshold, "红色上限持久化");
            Check.Equal(70, loaded.YellowBatteryThreshold, "黄色上限持久化");
            Check.Equal(DeviceIconKind.DualSense, loaded.DeviceIcons["CONTROLLER"], "手柄型号持久化且 ID 不区分大小写");
            Check.Equal("mouse", loaded.PrimaryDeviceId, "primary round trip");
            Check.True(loaded.MonitoredDeviceIds.Contains("MOUSE"), "case-insensitive IDs after load");

            // 旧设置缺少新增字段时仍保留设备选择和原有模式。
            File.WriteAllText(path, """{"Initialized":true,"Mode":"Cycle","MonitoredDeviceIds":["mouse"]}""");
            var legacy = store.Load();
            Check.Equal(IconEffect.Default, legacy.IconEffect, "旧设置默认使用当前效果");
            Check.Equal(DisplayMode.Cycle, legacy.Mode, "旧设置保留轮播模式");
            Check.True(legacy.MonitoredDeviceIds.Contains("MOUSE"), "旧设置保留监控设备");
            Check.Equal(0, legacy.DeviceIcons.Count, "旧设置采用自动识别");
            Check.Equal(20, legacy.LowBatteryThreshold, "旧设置补齐默认低电量阈值");
            Check.Equal(20, legacy.RedBatteryThreshold, "旧设置默认红色上限 20%");
            Check.Equal(60, legacy.YellowBatteryThreshold, "旧设置默认黄色上限 60%");
            // 越界、相等或倒序的配置均恢复完整默认分段，不影响其他设置。
            foreach (var (red, yellow) in new[] { (-1, 60), (20, 100), (60, 20), (30, 30) })
            {
                File.WriteAllText(path, $"{{\"RedBatteryThreshold\":{red},\"YellowBatteryThreshold\":{yellow},\"LowBatteryThreshold\":35}}");
                var colors = store.Load();
                Check.Equal(20, colors.RedBatteryThreshold, "非法分段恢复默认红色上限");
                Check.Equal(60, colors.YellowBatteryThreshold, "非法分段恢复默认黄色上限");
                Check.Equal(35, colors.LowBatteryThreshold, "颜色配置修复不改变通知阈值");
            }

            foreach (var threshold in new[] { -1, 0, 101 })
            {
                File.WriteAllText(path, $"{{\"LowBatteryThreshold\":{threshold}}}");
                Check.Equal(20, store.Load().LowBatteryThreshold, "无效阈值恢复默认值");
            }
            foreach (var threshold in new[] { 1, 100 })
            {
                settings.LowBatteryThreshold = threshold;
                Check.True(store.Save(settings), "允许保存阈值范围边界");
                Check.Equal(threshold, store.Load().LowBatteryThreshold, "有效阈值边界不会被重置");
            }

            File.WriteAllText(path, """{"Initialized":true,"DeviceIcons":null,"IconEffect":999}""");
            var invalid = store.Load();
            Check.Equal(IconEffect.Default, invalid.IconEffect, "无效效果回退当前样式");
            Check.Equal(0, invalid.DeviceIcons.Count, "空图标配置采用自动识别");

            File.WriteAllText(path, """{"DeviceIcons":{"mouse":"Mouse","MOUSE":"Keyboard"}}""");
            Check.Equal(DeviceIconKind.Keyboard, store.Load().DeviceIcons["mouse"], "重复的设备 ID 使用最后一项且不导致启动崩溃");

            File.WriteAllText(path, "{not-json");
            Check.Equal(false, store.Load().Initialized, "corrupt settings defaults");
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
