using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using BluetoothBatteryMonitor;

namespace BluetoothBatteryMonitor.Checks;

// 验证首次及后续原生菜单准备流程，不显示窗口、不读写用户配置。
internal static class TrayMenuChecks
{
    public static void Run()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { CheckOpening(); }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    // 菜单先完整构建再显示，不再依赖 WinForms 的 Opening 取消状态。
    private static void CheckOpening()
    {
        using var owner = new NativeTrayMenu();
        Check.Equal(false, IsWindowVisible(owner.Handle), "系统菜单所有者窗口保持隐藏");
        using var menu = new ContextMenuStrip();
        var context = (TrayApplicationContext)RuntimeHelpers.GetUninitializedObject(typeof(TrayApplicationContext));
        Set(context, "_menu", menu);
        Set(context, "_settings", new AppSettings());
        Set(context, "_devices", Array.Empty<BluetoothDeviceSnapshot>());
        Set(context, "_startupRegistration", new StartupRegistration(@"Software\BluetoothBatteryMonitor.Tests\MenuReadOnly"));
        var build = typeof(TrayApplicationContext).GetMethod("BuildMenu", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var show = typeof(TrayApplicationContext).GetMethod("ShowMenu", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Set(context, "_menuOpen", true);
        show.Invoke(context, [null, new MouseEventArgs(MouseButtons.Right, 1, 0, 0, 0)]);
        Check.Equal(0, menu.Items.Count, "已有菜单打开时忽略重复右键，不销毁当前命令");
        Set(context, "_menuOpen", false);
        show.Invoke(context, [null, new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0)]);
        Check.Equal(0, menu.Items.Count, "左键不触发菜单准备");
        build.Invoke(context, null);
        using (var first = new NativePopupMenu(menu.Items))
        {
            Check.Equal(9, GetMenuItemCount(first.Handle), "首次打开前已生成全部系统菜单项");
            Check.Equal(1, GetMenuItemCount(GetSubMenu(first.Handle, 2)), "首次无设备时保留占位项");
        }
        Set(context, "_devices", new[]
        {
            new BluetoothDeviceSnapshot("mouse", "蓝牙鼠标", true, 80, DeviceIconKind.Mouse),
            new BluetoothDeviceSnapshot("controller", "无线手柄", true, 60, DeviceIconKind.Gamepad)
        });
        build.Invoke(context, null);
        using var next = new NativePopupMenu(menu.Items);
        Check.Equal(9, GetMenuItemCount(next.Handle), "后续打开不重复累加菜单项");
        Check.Equal(2, GetMenuItemCount(GetSubMenu(next.Handle, 2)), "更新监控设备列表");
        Check.Equal(2, GetMenuItemCount(GetSubMenu(next.Handle, 4)), "更新手动设备图标列表");
        Check.Equal(false, menu.IsHandleCreated, "菜单数据更新不创建 WinForms 弹窗");
    }

    // 隔离测试只设置菜单所需状态，不启动蓝牙扫描及后台定时器。
    private static void Set(object context, string name, object value) =>
        typeof(TrayApplicationContext).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(context, value);

    // 查询系统生成的菜单，确认完整性及实时设备更新。
    [DllImport("user32.dll")]
    private static extern int GetMenuItemCount(nint menu);
    [DllImport("user32.dll")]
    private static extern nint GetSubMenu(nint menu, int position);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint window);
}
