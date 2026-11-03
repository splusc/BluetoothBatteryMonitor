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
        var settings = new AppSettings
        {
            IconEffect = IconEffect.Personalized,
            MonitoredDeviceIds = new(StringComparer.OrdinalIgnoreCase) { "mouse" }
        };
        Set(context, "_settings", settings);
        Set(context, "_notifyIcon", notify);
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
            Check.True(notify.Text.Contains("新名称"), "复用图标仍更新悬浮提示");

            Set(context, "_devices", new[] { new BluetoothDeviceSnapshot("mouse", "新名称", true, 20, DeviceIconKind.Mouse) });
            Update.Invoke(context, null);
            Check.True(!ReferenceEquals(original, notify.Icon), "电量变化时重新绘制");
            using var low = notify.Icon!.ToBitmap();
            Check.Equal(Color.Red.ToArgb(), low.GetPixel(25, 2).ToArgb(), "重绘后同步更新阶段颜色");

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
