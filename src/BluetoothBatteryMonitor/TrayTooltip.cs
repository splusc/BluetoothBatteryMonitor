namespace BluetoothBatteryMonitor;

// 系统托盘提示有字符上限，改用不抢焦点、超高时可滚动的完整设备列表。
internal sealed class TrayTooltip : Form
{
    private readonly Label _label = new() { AutoSize = true, UseMnemonic = false }; // 原样显示设备名，保留 & 字符。
    private readonly Panel _panel = new() { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(8), BorderStyle = BorderStyle.FixedSingle }; // 长列表滚动区域与细边框。
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 150 }; // 离开图标和列表后自动收起。
    private Rectangle _anchor; // 首次悬停位置附近的图标区域，允许鼠标移入列表。

    public TrayTooltip()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None; // 按当前字体实测像素布局，避免首次显示时再缩放而越出工作区。
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = SystemColors.Info;
        ForeColor = SystemColors.InfoText;
        AccessibleRole = AccessibleRole.ToolTip;
        _label.Location = new Point(8, 8);
        _panel.Controls.Add(_label);
        Controls.Add(_panel);
        _timer.Tick += (_, _) =>
        {
            if (!_anchor.Contains(Cursor.Position) && !Bounds.Contains(Cursor.Position)) Dismiss();
        };
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
}
