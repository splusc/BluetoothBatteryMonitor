using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace BluetoothBatteryMonitor;

// 把现有菜单项映射为系统菜单，保留勾选、禁用状态与原来的点击逻辑。
internal sealed class NativePopupMenu : IDisposable
{
    private readonly Dictionary<uint, ToolStripMenuItem> _commands = new(); // 本次打开期间的命令映射。
    private readonly MenuHandle _menu; // 关闭主菜单时连同全部子菜单一起释放。

    public nint Handle => _menu.DangerousGetHandle();

    public NativePopupMenu(ToolStripItemCollection items) => _menu = Build(items);

    public void Invoke(uint command)
    {
        if (!_menu.IsClosed && _commands.TryGetValue(command, out var item) && item.Enabled && item.Available)
            item.PerformClick();
    }

    public void Dispose() => _menu.Dispose();

    // 子菜单只有成功加入父菜单后才转交所有权，失败路径也必须完整释放句柄。
    private MenuHandle Build(ToolStripItemCollection items)
    {
        var menu = CreatePopupMenu();
        if (menu.IsInvalid)
        {
            menu.Dispose();
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }
        try
        {
            foreach (ToolStripItem item in items)
            {
                if (!item.Available) continue;
                if (item is ToolStripSeparator)
                {
                    Append(menu, 0x800, 0, null); // MF_SEPARATOR：系统分隔线。
                    continue;
                }
                if (item is not ToolStripMenuItem entry) continue;
                var flags = (entry.Enabled ? 0u : 0x1u) | (entry.Checked ? 0x8u : 0u); // 禁用与勾选状态。
                if (entry.DropDownItems.Count > 0)
                {
                    using var child = Build(entry.DropDownItems);
                    Append(menu, flags | 0x10, (nuint)child.DangerousGetHandle(), entry.Text); // MF_POPUP：子菜单。
                    child.SetHandleAsInvalid();
                }
                else
                {
                    var command = checked((uint)_commands.Count + 1);
                    _commands.Add(command, entry);
                    Append(menu, flags, command, entry.Text);
                }
            }
            return menu;
        }
        catch
        {
            menu.Dispose();
            throw;
        }
    }

    // 系统拒绝创建菜单项时保留错误信息，由上层统一处理。
    private static void Append(MenuHandle menu, uint flags, nuint item, string? text)
    {
        if (!AppendMenu(menu, flags, item, text)) throw new Win32Exception(Marshal.GetLastPInvokeError());
    }

    // 安全句柄保证构造失败及异常退出时也能回收原生菜单资源。
    private sealed class MenuHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public MenuHandle() : base(ownsHandle: true) { }
        protected override bool ReleaseHandle() => DestroyMenu(handle);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern MenuHandle CreatePopupMenu();
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AppendMenu(MenuHandle menu, uint flags, nuint item, string? text);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyMenu(nint menu);
}

// 隐藏所有者窗口只负责菜单焦点与消息，不创建可见的 WinForms 工具弹窗。
internal sealed class NativeTrayMenu : NativeWindow, IDisposable
{
    public NativeTrayMenu() => CreateHandle(new CreateParams
    {
        Caption = "蓝牙电量监视器",
        Style = unchecked((int)0x80000000) // WS_POPUP：隐藏的顶层所有者，不设置可见标志。
    });

    public void Show(ToolStripItemCollection items, Point location)
    {
        using var menu = new NativePopupMenu(items);
        SetForegroundWindow(Handle);
        uint command;
        try
        {
            // TPM_RETURNCMD、TPM_NONOTIFY、TPM_RIGHTBUTTON：返回选择的命令，关闭后再执行。
            command = TrackPopupMenuEx(menu.Handle, 0x100 | 0x80 | 0x2, location.X, location.Y, Handle, 0);
        }
        finally
        {
            // 托盘菜单遵循系统焦点协议，避免点击空白后无法关闭或下一次右键失效。
            PostMessage(Handle, 0, 0, 0);
        }
        menu.Invoke(command);
    }

    public void Dispose() => DestroyHandle();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll")]
    private static extern uint TrackPopupMenuEx(nint menu, uint flags, int x, int y, nint owner, nint parameters);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(nint window, uint message, nuint wParam, nint lParam);
}
