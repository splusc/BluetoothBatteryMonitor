using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace BluetoothBatteryMonitor;

// 系统托盘提示有字符上限，改用不抢焦点、超高时可滚动的完整设备列表。
internal sealed class TrayTooltip : Form
{
    private readonly Label _label = new() { AutoSize = true, UseMnemonic = false }; // 原样显示设备名，保留 & 字符。
    private readonly Panel _panel = new() { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(8) }; // 长列表滚动区域，边框交给窗口与 DWM 绘制。
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 150 }; // 离开图标和列表后自动收起。
    private Rectangle _anchor; // 首次悬停位置附近的图标区域，允许鼠标移入列表。

    public TrayTooltip()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None; // 按当前字体实测像素布局，避免首次显示时再缩放而越出工作区。
        ShowInTaskbar = false;
        TopMost = true;
        Padding = new Padding(1); // 留出一像素主题边框，避免传统控件边框破坏深色样式。
        AccessibleRole = AccessibleRole.ToolTip;
        _label.Location = new Point(8, 8);
        _panel.Controls.Add(_label);
        Controls.Add(_panel);
        UpdateTheme();
        _timer.Tick += (_, _) =>
        {
            if (!_anchor.Contains(Cursor.Position) && !Bounds.Contains(Cursor.Position)) Dismiss();
        };
    }

    // 托盘跟随 Windows 模式，不能使用可能与任务栏不同的应用模式。
    private void UpdateTheme()
    {
        var dark = false;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            dark = key?.GetValue("SystemUsesLightTheme") is 0;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // 个性化设置不可读时使用浅色，提示内容仍可正常查看。
        }
        ApplyTheme(dark, SystemInformation.HighContrast);
    }

    // 高对比度必须遵循系统配色，普通主题使用与系统提示相近的中性底色。
    private void ApplyTheme(bool dark, bool highContrast)
    {
        BackColor = highContrast ? SystemColors.WindowFrame : dark ? Color.FromArgb(69, 69, 69) : Color.FromArgb(207, 207, 207);
        _panel.BackColor = highContrast ? SystemColors.Info : dark ? Color.FromArgb(43, 43, 43) : Color.FromArgb(249, 249, 249);
        ForeColor = highContrast ? SystemColors.InfoText : dark ? Color.White : Color.FromArgb(26, 26, 26);
        if (!IsHandleCreated) return;
        var corner = highContrast ? 1 : 3; // DWMWCP_DONOTROUND / DWMWCP_ROUNDSMALL：系统小圆角。
        var border = ColorTranslator.ToWin32(BackColor); // DWM 使用 COLORREF，而非 ARGB。
        _ = DwmSetWindowAttribute(Handle, 33, ref corner, sizeof(int)); // DWMWA_WINDOW_CORNER_PREFERENCE。
        _ = DwmSetWindowAttribute(Handle, 34, ref border, sizeof(int)); // DWMWA_BORDER_COLOR；失败时保留窗口自身边框。
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        _ = DwmExtendFrameIntoClientArea(Handle, [1, 1, 1, 1]); // 一像素系统框架为无标题栏窗口保留原生阴影。
        UpdateTheme();
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        // 系统设置、颜色或主题变化时，已打开和隐藏的提示都同步更新。
        if (m.Msg is 0x001A or 0x0015 or 0x031A) UpdateTheme();
    }

    // 刷新电量时同步已打开的列表，不重新定位或抢走其他窗口的焦点。
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string Content
    {
        get => _label.Text;
        set
        {
            if (_label.Text == value) return;
            _label.Text = value;
            if (Visible) PositionPopup();
        }
    }

    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= 0x08000080; // WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW：不激活，不进入任务栏。
            return parameters;
        }
    }

    public void ShowAt(Point location)
    {
        if (Visible || string.IsNullOrEmpty(Content)) return;
        UpdateTheme(); // 再次悬停时也刷新，覆盖休眠恢复等未收到广播的情况。
        var size = SystemInformation.SmallIconSize;
        _anchor = new Rectangle(location.X - size.Width, location.Y - size.Height, size.Width * 2, size.Height * 2);
        _panel.AutoScrollPosition = Point.Empty;
        PositionPopup();
        Show();
        _timer.Start();
    }

    // 弹窗始终放在图标所在显示器的工作区内，极长名称换行，过多设备使用滚动条。
    private void PositionPopup()
    {
        var area = Screen.FromPoint(new Point(_anchor.Left + _anchor.Width / 2, _anchor.Top + _anchor.Height / 2)).WorkingArea;
        var width = Math.Min(480, Math.Max(80, area.Width - 32));
        var scrollPosition = _panel.AutoScrollPosition;
        _panel.AutoScrollPosition = Point.Empty;
        _label.MaximumSize = new Size(width - 32, 0);
        ClientSize = new Size(Math.Min(width, _label.PreferredSize.Width + 32),
            Math.Min(Math.Max(40, area.Height - 32), _label.PreferredSize.Height + 16));
        Location = new Point(Math.Clamp(_anchor.Left, area.Left, Math.Max(area.Left, area.Right - Width)),
            Math.Clamp(_anchor.Top - Height + 8, area.Top, Math.Max(area.Top, area.Bottom - Height)));
        // 轮播和电量更新不能把正在阅读后续设备的用户强制送回顶部。
        _panel.AutoScrollPosition = new Point(-scrollPosition.X, -scrollPosition.Y);
    }

    public void Dismiss()
    {
        _timer.Stop();
        Hide();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _timer.Dispose();
        base.Dispose(disposing);
    }

    // 圆角、边框和阴影交由 Windows 合成器管理，不额外分配位图或裁剪区域。
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int size);
    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(nint window, int[] margins);
}
