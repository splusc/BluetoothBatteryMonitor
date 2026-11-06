using System.Drawing;
using System.Windows.Forms;
using BluetoothBatteryMonitor;

namespace BluetoothBatteryMonitor.Checks;

// 使用真实控件验证长列表换行、滚动和屏幕边界，不启动蓝牙扫描。
internal static class TrayTooltipChecks
{
    public static void Run()
    {
        using var tooltip = new TrayTooltip();
        var entries = Enumerable.Range(1, 100).Select(index => $"设备 {index} & {new string('长', 80)} 90%");
        var content = string.Join(Environment.NewLine, entries);
        tooltip.Content = content;
        var area = Screen.PrimaryScreen!.WorkingArea;
        tooltip.ShowAt(new Point(area.Right - 1, area.Bottom - 1));
        var panel = (Panel)tooltip.Controls[0];
        var label = (Label)panel.Controls[0];
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
}
