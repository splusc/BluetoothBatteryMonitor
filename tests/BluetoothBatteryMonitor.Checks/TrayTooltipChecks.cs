using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using BluetoothBatteryMonitor;
using Microsoft.Win32;

namespace BluetoothBatteryMonitor.Checks;

// 使用真实控件验证长列表换行、滚动和屏幕边界，不启动蓝牙扫描。
internal static class TrayTooltipChecks
{
    public static void Run()
    {
        using var tooltip = new TrayTooltip();
        // 启动即跟随托盘所在的 Windows 系统主题，不能继续使用传统黄色提示底色。
        using var themeKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        var dark = themeKey?.GetValue("SystemUsesLightTheme") is 0;
        CheckColors(tooltip, dark, SystemInformation.HighContrast);
        var entries = Enumerable.Range(1, 100).Select(index => $"设备 {index} & {new string('长', 80)} 90%");
        var content = string.Join(Environment.NewLine, entries);
        tooltip.Content = content;
        var area = Screen.PrimaryScreen!.WorkingArea;
        tooltip.ShowAt(new Point(area.Right - 1, area.Bottom - 1));
        var panel = (Panel)tooltip.Controls[0];
        var label = (Label)panel.Controls[0];
        // 直接检查真实控件与原生窗口，切换配色不改写用户的 Windows 主题设置。
        var applyTheme = typeof(TrayTooltip).GetMethod("ApplyTheme", BindingFlags.Instance | BindingFlags.NonPublic)!;
        foreach (var useDark in new[] { true, false })
        {
            applyTheme.Invoke(tooltip, [useDark, false]);
            CheckColors(tooltip, useDark, false);
            Check.Equal(0, DwmGetWindowAttribute(tooltip.Handle, 33, out var corner, sizeof(int)), "可读取窗口圆角设置");
            Check.Equal(3, corner, "提示窗使用系统小圆角");
        }
        applyTheme.Invoke(tooltip, [dark, true]);
        CheckColors(tooltip, dark, true);
        // 广播主题变化时恢复实际系统配色，验证已打开的提示也会更新。
        SendMessage(tooltip.Handle, 0x031A, 0, 0); // WM_THEMECHANGED：系统主题已变化。
        CheckColors(tooltip, dark, SystemInformation.HighContrast);
        Check.True(tooltip.Visible, "悬停时显示列表");
        Check.True(area.Contains(tooltip.Bounds), $"右下角悬停窗口不超出工作区：窗口 {tooltip.Bounds}，工作区 {area}");
        Check.Equal(content, label.Text, "长列表保留所有设备及 & 字符");
        Check.True(panel.VerticalScroll.Visible, "超高列表显示滚动条");
        Check.True(!panel.HorizontalScroll.Visible, "长名称换行后不出现横向滚动条");
        panel.AutoScrollPosition = new Point(0, label.Height);
        Check.True(panel.AutoScrollPosition.Y < 0, "可以滚动查看后面的设备");
        Check.True(label.Bottom <= panel.ClientSize.Height, "滚动可到达最后一台设备");
        var scrollPosition = panel.AutoScrollPosition;
        tooltip.Content = content.Replace("90%", "80%", StringComparison.Ordinal);
        Check.Equal(scrollPosition, panel.AutoScrollPosition, "电量更新后保留用户当前滚动位置");
        tooltip.Content = "更新后的设备 80%";
        Check.Equal("更新后的设备 80%", label.Text, "已打开的列表同步刷新电量");
        tooltip.Dismiss();
        Check.True(!tooltip.Visible, "离开后收起悬停窗口");
        foreach (var screen in Screen.AllScreens)
        {
            tooltip.ShowAt(screen.WorkingArea.Location);
            Check.True(screen.WorkingArea.Contains(tooltip.Bounds), "显示器左上角按实际悬停点定位");
            tooltip.Dismiss();
        }
    }

    // 标签和滚动区域必须同色，避免只更新窗口外层后仍出现黄色底或看不清的文字。
    private static void CheckColors(TrayTooltip tooltip, bool dark, bool highContrast)
    {
        var panel = (Panel)tooltip.Controls[0];
        var label = (Label)panel.Controls[0];
        Check.Equal(panel.BackColor, label.BackColor, "文字区域与提示背景一致");
        Check.Equal(tooltip.ForeColor, label.ForeColor, "文字继承当前主题前景色");
        if (highContrast)
        {
            Check.Equal(SystemColors.Info, panel.BackColor, "高对比度遵循系统提示背景色");
            Check.Equal(SystemColors.InfoText, label.ForeColor, "高对比度遵循系统提示文字色");
            return;
        }
        Check.True(dark ? panel.BackColor.GetBrightness() < 0.25f : panel.BackColor.GetBrightness() > 0.9f,
            "提示背景随深浅主题切换");
        Check.True(dark ? label.ForeColor.GetBrightness() > 0.9f : label.ForeColor.GetBrightness() < 0.2f,
            "提示文字与背景保持对比");
        Check.True(panel.BackColor.R == panel.BackColor.G && panel.BackColor.G == panel.BackColor.B,
            "提示使用中性灰背景，去掉传统黄色");
    }

    // 仅读取本测试窗口的 DWM 属性，不影响桌面或其他应用。
    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(nint window, int attribute, out int value, int size);
    [DllImport("user32.dll")]
    private static extern nint SendMessage(nint window, uint message, nuint wParam, nint lParam);
}
