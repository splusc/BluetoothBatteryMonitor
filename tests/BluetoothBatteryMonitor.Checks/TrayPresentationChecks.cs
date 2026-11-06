using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using BluetoothBatteryMonitor;

namespace BluetoothBatteryMonitor.Checks;

// 只构建绘图所需状态，避免测试启动真实蓝牙扫描、定时器或写入用户设置。
internal static class TrayPresentationChecks
{
    private static readonly MethodInfo Update = typeof(TrayApplicationContext)
        .GetMethod("UpdatePresentation", BindingFlags.Instance | BindingFlags.NonPublic)!;

    public static void Run(bool benchmark = false)
    {
        var context = (TrayApplicationContext)RuntimeHelpers.GetUninitializedObject(typeof(TrayApplicationContext));
        using var notify = new NotifyIcon();
        using var tooltip = new TrayTooltip(); // 测试完整悬停内容，避免依赖有长度限制的 NotifyIcon.Text。
        using var deviceChangeTimer = new System.Windows.Forms.Timer(); // 隔离并发刷新检查，不执行真实扫描。
        var settings = new AppSettings
        {
            IconEffect = IconEffect.Personalized,
            MonitoredDeviceIds = new(StringComparer.OrdinalIgnoreCase) { "mouse" }
        };
        Set(context, "_settings", settings);
        Set(context, "_showBattery", true);
        Set(context, "_notifyIcon", notify);
        Set(context, "_tooltip", tooltip);
        Set(context, "_deviceChangeTimer", deviceChangeTimer);
        Set(context, "_emptyStatus", "没有可显示的设备");
        Set(context, "_devices", new[] { new BluetoothDeviceSnapshot("mouse", "鼠标", true, 80, DeviceIconKind.Mouse) });
        try
        {
            Update.Invoke(context, null);
            var original = notify.Icon;
            Check.True(original is not null, "首次刷新生成图标");
            if (benchmark)
            {
                // 测量真实刷新路径的分配与耗时，不把它误认为进程常驻内存降幅。
                var allocated = GC.GetAllocatedBytesForCurrentThread();
                var watch = Stopwatch.StartNew();
                for (var index = 0; index < 1_000; index++) Update.Invoke(context, null);
                watch.Stop();
                Console.WriteLine($"1000 次不变刷新：{watch.Elapsed.TotalMilliseconds:F1} ms，托管分配 {GC.GetAllocatedBytesForCurrentThread() - allocated:N0} 字节");
                return;
            }

            Update.Invoke(context, null);
            Check.True(ReferenceEquals(original, notify.Icon), "电量与样式不变时复用图标");
            Set(context, "_devices", new[] { new BluetoothDeviceSnapshot("mouse", "新名称", true, 80, DeviceIconKind.Mouse) });
            Update.Invoke(context, null);
            Check.True(ReferenceEquals(original, notify.Icon), "仅设备名称改变不重绘图标");
            Check.True(tooltip.Content.Contains("新名称"), "复用图标仍更新悬浮提示");

            // 从真实刷新入口验证筛选与完整列表的衔接，系统原生提示保持禁用。
            var monitored = Enumerable.Range(1, 4)
                .Select(index => new BluetoothDeviceSnapshot($"device-{index}", $"监控设备 {index} {new string('长', 40)}", true, 80))
                .ToArray();
            settings.MonitoredDeviceIds.UnionWith(monitored.Select(device => device.Id));
            Set(context, "_devices", monitored.Append(new BluetoothDeviceSnapshot("ignored", "不监控的设备", true, 80)).ToArray());
            Update.Invoke(context, null);
            Check.Equal(4, tooltip.Content.Split(Environment.NewLine).Length, "刷新后完整显示四台监控设备");
            Check.True(tooltip.Content.Contains(monitored[3].Name), "最后一台设备不会被截掉");
            Check.True(!tooltip.Content.Contains("不监控的设备"), "悬停列表继续遵循监控选择");
            Check.Equal(string.Empty, notify.Text, "禁用有字符上限的系统提示");
            var sortedTooltip = tooltip.Content;
            settings.Mode = DisplayMode.Cycle;
            Set(context, "_cycleIndex", 2);
            Update.Invoke(context, null);
            Check.Equal(sortedTooltip, tooltip.Content, "图标轮播后提示列表顺序保持不变");
            settings.Mode = DisplayMode.Lowest;

            Set(context, "_devices", new[] { new BluetoothDeviceSnapshot("mouse", "新名称", true, 20, DeviceIconKind.Mouse) });
            Update.Invoke(context, null);
            Check.True(!ReferenceEquals(original, notify.Icon), "电量变化时重新绘制");
            using var low = notify.Icon!.ToBitmap();
            Check.Equal(Color.Red.ToArgb(), low.GetPixel(25, 2).ToArgb(), "重绘后同步更新阶段颜色");

            settings.RedBatteryThreshold = 10;
            settings.YellowBatteryThreshold = 15;
            Update.Invoke(context, null);
            using (var changed = notify.Icon!.ToBitmap())
                Check.Equal(Color.LimeGreen.ToArgb(), changed.GetPixel(25, 2).ToArgb(), "电量不变时修改颜色阈值立即重绘");
            settings.YellowBatteryThreshold = 60;
            Update.Invoke(context, null);
            using (var changed = notify.Icon!.ToBitmap())
                Check.Equal(Color.Gold.ToArgb(), changed.GetPixel(25, 2).ToArgb(), "单独修改黄色上限也会使缓存失效");

            var lowIcon = notify.Icon;
            settings.DeviceIcons["mouse"] = DeviceIconKind.Keyboard;
            Update.Invoke(context, null);
            Check.True(!ReferenceEquals(lowIcon, notify.Icon), "手动图标改变立即重绘");
            var personalized = notify.Icon;
            settings.IconEffect = IconEffect.Default;
            Update.Invoke(context, null);
            Check.True(!ReferenceEquals(personalized, notify.Icon), "显示效果改变立即重绘");
            var defaultIcon = notify.Icon;
            settings.DeviceIcons["mouse"] = DeviceIconKind.Xbox;
            Update.Invoke(context, null);
            Check.True(ReferenceEquals(defaultIcon, notify.Icon), "默认电池不因设备类型变化重复绘制");
            Set(context, "_devices", new[] { new BluetoothDeviceSnapshot("mouse", "新名称", true, null, DeviceIconKind.Mouse) });
            Update.Invoke(context, null);
            Check.True(!ReferenceEquals(defaultIcon, notify.Icon), "电量变为未知时更新图标");
            settings.IconEffect = IconEffect.Personalized;
            settings.DeviceIcons.Clear();
            Set(context, "_devices", new[] { new BluetoothDeviceSnapshot("mouse", "Wireless Controller", true, null, DeviceIconKind.DualShock4) });
            Update.Invoke(context, null);
            using var expected = BatteryIcon.Create(null, IconEffect.Personalized, DeviceIconKind.DualShock4);
            using var expectedPixels = expected.ToBitmap();
            using var actualPixels = notify.Icon!.ToBitmap();
            Check.True(Enumerable.Range(0, 1024).All(p => expectedPixels.GetPixel(p % 32, p / 32) == actualPixels.GetPixel(p % 32, p / 32)),
                "最低电量模式在 PS4 电量未知时仍显示手柄，不能退回蓝牙图标");

            // 读取期间收到连接或电量事件，必须排队补刷，不能被防重入判断丢弃。
            Set(context, "_refreshing", true);
            var refresh = typeof(TrayApplicationContext).GetMethod("RefreshAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
            ((Task)refresh.Invoke(context, null)!).GetAwaiter().GetResult();
            Check.True(deviceChangeTimer.Enabled, "忙碌时的刷新请求会安排补刷");
            deviceChangeTimer.Stop();
            Set(context, "_exiting", true);
            ((Task)refresh.Invoke(context, null)!).GetAwaiter().GetResult();
            Check.True(!deviceChangeTimer.Enabled, "退出后不再安排设备刷新");
        }
        finally
        {
            var icon = notify.Icon;
            notify.Icon = null;
            icon?.Dispose();
        }
    }

    // 反射只用于隔离现有私有刷新流程，生产代码不增加测试专用接口。
    private static void Set(object context, string name, object value) =>
        typeof(TrayApplicationContext).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(context, value);
}
