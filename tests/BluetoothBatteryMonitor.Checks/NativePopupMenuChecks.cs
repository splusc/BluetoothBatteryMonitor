using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using BluetoothBatteryMonitor;

namespace BluetoothBatteryMonitor.Checks;

// 核对系统菜单的真实层级、状态、点击行为与句柄释放，不显示窗口。
internal static class NativePopupMenuChecks
{
    public static void Run()
    {
        using var model = new ContextMenuStrip();
        var clicks = 0;
        var toggle = new ToolStripMenuItem("监控鼠标") { Checked = true, CheckOnClick = true };
        toggle.Click += (_, _) => clicks++;
        var disabled = new ToolStripMenuItem("不可用设备") { Enabled = false };
        disabled.Click += (_, _) => clicks += 100;
        var submenu = new ToolStripMenuItem("设备图标");
        submenu.DropDownItems.Add("键盘", null, (_, _) => clicks += 10);
        model.Items.AddRange([toggle, disabled, new ToolStripSeparator(), submenu,
            new ToolStripMenuItem("隐藏项") { Available = false }]);

        nint root;
        nint child;
        using (var native = new NativePopupMenu(model.Items))
        {
            root = native.Handle;
            Check.True(root != 0, "生成系统菜单句柄");
            Check.Equal(4, GetMenuItemCount(root), "原生菜单保留可用项与分隔线，跳过隐藏项");
            var text = new StringBuilder(64);
            GetMenuString(root, 0, text, text.Capacity, 0x400);
            Check.Equal("监控鼠标", text.ToString(), "中文标题传递到系统菜单");
            Check.True((GetMenuState(root, 0, 0x400) & 0x8) != 0, "勾选状态传递到系统菜单");
            Check.True((GetMenuState(root, 1, 0x400) & 0x3) != 0, "禁用状态传递到系统菜单");
            Check.True((GetMenuState(root, 2, 0x400) & 0x800) != 0, "保留系统分隔线");
            child = GetSubMenu(root, 3);
            Check.Equal(1, GetMenuItemCount(child), "保留设备图标子菜单");
            native.Invoke(GetMenuItemID(root, 0));
            Check.Equal(1, clicks, "选中命令执行原有点击逻辑");
            Check.Equal(false, toggle.Checked, "监控设备的勾选自动切换");
            native.Invoke(GetMenuItemID(root, 1));
            Check.Equal(1, clicks, "禁用命令不能执行");
            native.Invoke(GetMenuItemID(child, 0));
            Check.Equal(11, clicks, "子菜单命令不会与主菜单串号");
            native.Invoke(0);
            native.Invoke(uint.MaxValue);
            Check.Equal(11, clicks, "取消或未知命令不执行操作");
            Check.Equal(false, model.IsHandleCreated, "构建原生菜单不创建 WinForms 菜单窗口");
        }
        Check.Equal(false, IsMenu(root), "关闭后释放主菜单句柄");
        Check.Equal(false, IsMenu(child), "关闭后同时释放子菜单句柄");
    }

    // 直接读取 Windows 菜单状态，避免只验证中间数据结构。
    [DllImport("user32.dll")]
    private static extern int GetMenuItemCount(nint menu);
    [DllImport("user32.dll")]
    private static extern uint GetMenuItemID(nint menu, int position);
    [DllImport("user32.dll")]
    private static extern uint GetMenuState(nint menu, uint item, uint flags);
    [DllImport("user32.dll")]
    private static extern nint GetSubMenu(nint menu, int position);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetMenuString(nint menu, uint item, StringBuilder text, int count, uint flags);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsMenu(nint menu);
}
