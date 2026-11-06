using Windows.Devices.Enumeration;

namespace BluetoothBatteryMonitor;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly BluetoothMonitor _monitor = new();
    private readonly SettingsStore _settingsStore = new();
    private readonly StartupRegistration _startupRegistration = new();
    private readonly AppSettings _settings;
    private readonly LowBatteryReminder _lowBatteryReminder = new(); // 提醒状态独立于轮播和图标刷新。
    private readonly bool _showBattery;
    private readonly ContextMenuStrip _menu = new();
    private readonly NativeTrayMenu _nativeMenu = new(); // 只由系统原生菜单负责显示和键盘导航。
    private readonly NotifyIcon _notifyIcon;
    private readonly TrayTooltip _tooltip = new(); // 独立悬停窗口保留全部监控设备，不受托盘文字长度限制。
    private readonly System.Windows.Forms.Timer _refreshTimer = new() { Interval = 60_000 };
    private readonly System.Windows.Forms.Timer _cycleTimer = new() { Interval = 5_000 };
    private readonly System.Windows.Forms.Timer _deviceChangeTimer = new() { Interval = 500 }; // 合并短时间内的系统设备事件。
    private readonly List<DeviceWatcher> _deviceWatchers = []; // 退出时停止系统监听，避免继续回调已释放的界面。
    private IReadOnlyList<BluetoothDeviceSnapshot> _devices = [];
    private Icon? _currentIcon;
    // 只记住当前图标的输入，避免未变化的刷新重复解码 PNG 和分配原生图形资源。
    private (byte? Percent, IconEffect Effect, DeviceIconKind Kind, int Red, int Yellow)? _currentIconKey;
    private bool _refreshing;
    private bool _exiting;
    private bool _settingsDirty;
    private bool _menuOpen; // 系统菜单运行消息循环时，防止再次右键重建正在使用的命令。
    private int _cycleIndex;
    private string _emptyStatus = "正在读取蓝牙设备";

    public TrayApplicationContext(bool showBattery = true)
    {
        _showBattery = showBattery;
        _settings = _settingsStore.Load();
        _currentIcon = BatteryIcon.Create(null);
        _currentIconKey = (null, IconEffect.Default, DeviceIconKind.Unknown, _settings.RedBatteryThreshold, _settings.YellowBatteryThreshold);
        _notifyIcon = new NotifyIcon
        {
            Icon = _currentIcon,
            Text = string.Empty, // 禁用系统自带提示，避免与完整列表重叠。
            Visible = true
        };
        _notifyIcon.MouseUp += ShowMenu;
        _notifyIcon.MouseMove += (_, _) =>
        {
            if (!_exiting && !_menuOpen) _tooltip.ShowAt(Cursor.Position);
        };
        _refreshTimer.Tick += async (_, _) => await RefreshAsync();
        _cycleTimer.Tick += (_, _) => AdvanceCycle();
        _deviceChangeTimer.Tick += async (_, _) =>
        {
            if (_refreshing) return;
            _deviceChangeTimer.Stop();
            await RefreshAsync();
        };
        _refreshTimer.Start();
        _cycleTimer.Start();
        StartDeviceWatchers();
        _ = RefreshAsync();
    }

    // WinRT 在后台线程回调；统一投递到界面线程，并合并同一次连接引起的多个节点变化。
    private void StartDeviceWatchers()
    {
        _ = _tooltip.Handle;
        try
        {
            foreach (var watcher in BluetoothMonitor.CreateWatchers())
            {
                void Changed()
                {
                    if (_exiting || watcher.Status != DeviceWatcherStatus.EnumerationCompleted) return;
                    try
                    {
                        _tooltip.BeginInvoke((Action)(() => { if (!_exiting) _deviceChangeTimer.Start(); }));
                    }
                    catch (InvalidOperationException)
                    {
                        // 退出期间窗口可能已释放，不再安排刷新。
                    }
                }
                watcher.Added += (_, _) => Changed();
                watcher.Updated += (_, _) => Changed();
                watcher.Removed += (_, _) => Changed();
                watcher.EnumerationCompleted += (_, _) => Changed();
                _deviceWatchers.Add(watcher);
                watcher.Start();
            }
        }
        catch
        {
            // 驱动不支持监听时仍保留每分钟刷新。
        }
    }

    private async Task RefreshAsync()
    {
        if (_exiting) return;
        if (_refreshing)
        {
            _deviceChangeTimer.Start();
            return;
        }
        _deviceChangeTimer.Stop();
        _refreshing = true;
        try
        {
            _devices = await _monitor.GetDevicesAsync();
            if (_settings.MergeDeviceIds(_devices)) _settingsDirty = true;
            _emptyStatus = _devices.Count == 0 ? "未发现已配对蓝牙设备" : "没有可显示的设备";

            if (!_settings.Initialized)
            {
                var connected = _devices.Where(device => device.IsConnected).ToArray();
                if (connected.Length > 0)
                {
                    _settings.MonitoredDeviceIds.UnionWith(connected.Select(device => device.Id));
                    _settings.PrimaryDeviceId = connected[0].Id;
                    _settings.Initialized = true;
                    _settingsDirty = !_settingsStore.Save(_settings);
                    if (_settingsDirty) _settings.Initialized = false;
                }
            }
            else if (_settingsDirty)
            {
                _settingsDirty = !_settingsStore.Save(_settings);
            }
            ShowLowBatteryNotification();
        }
        catch
        {
            _devices = [];
            _emptyStatus = "蓝牙设备读取失败";
        }
        finally
        {
            _refreshing = false;
            if (!_exiting) UpdatePresentation();
        }
    }

    private void AdvanceCycle()
    {
        if (_settings.Mode != DisplayMode.Cycle) return;
        _cycleIndex++;
        UpdatePresentation();
    }

    private void UpdatePresentation()
    {
        if (_exiting) return;

        Icon? candidate = null;
        try
        {
            var current = DisplayLogic.SelectDevice(_devices, _settings, _cycleIndex);
            var monitored = _devices
                .Where(device => _settings.MonitoredDeviceIds.Contains(device.Id))
                .ToArray();
            var tooltip = DisplayLogic.BuildTooltip(monitored, _emptyStatus);
            _tooltip.Content = tooltip;
            // 默认样式不依赖设备类型；轮播到相同电量的设备时只需更新提示文字。
            var kind = _settings.IconEffect == IconEffect.Personalized && current is not null
                ? _settings.GetIconKind(current) : DeviceIconKind.Unknown;
            // 修改颜色边界也会使缓存失效，即使设备电量没有变化。
            var iconKey = (current?.BatteryPercent, _settings.IconEffect, kind, _settings.RedBatteryThreshold, _settings.YellowBatteryThreshold);
            if (_currentIcon is not null && _currentIconKey == iconKey)
            {
                return;
            }
            // 外观独立于设备选择策略，轮播时图形随当前设备一起切换。
            candidate = BatteryIcon.Create(current?.BatteryPercent, _settings.IconEffect, kind, _showBattery,
                _settings.RedBatteryThreshold, _settings.YellowBatteryThreshold);
            if (_exiting) return;

            var previousIcon = _currentIcon;
            try
            {
                _notifyIcon.Icon = candidate;
            }
            catch
            {
                try
                {
                    _notifyIcon.Icon = previousIcon;
                }
                catch
                {
                }
                if (ReferenceEquals(_notifyIcon.Icon, candidate))
                {
                    _currentIcon = candidate;
                    _currentIconKey = iconKey;
                    candidate = null;
                    previousIcon?.Dispose();
                }
                return;
            }

            _currentIcon = candidate;
            _currentIconKey = iconKey;
            candidate = null;
            previousIcon?.Dispose();
        }
        catch
        {
        }
        finally
        {
            candidate?.Dispose();
        }
    }

    // 先准备全部菜单内容，再交给系统显示，首次打开也不依赖空菜单的 Opening 事件。
    private void ShowMenu(object? sender, MouseEventArgs eventArgs)
    {
        if (_exiting || _menuOpen || eventArgs.Button != MouseButtons.Right) return;
        _tooltip.Dismiss();
        _menuOpen = true;
        try
        {
            BuildMenu();
            _nativeMenu.Show(_menu.Items, Cursor.Position);
        }
        finally
        {
            _menuOpen = false;
        }
    }

    private void BuildMenu()
    {
        var oldItems = _menu.Items.Cast<ToolStripItem>().ToArray();
        _menu.Items.Clear();
        foreach (var item in oldItems) item.Dispose();

        var modes = new ToolStripMenuItem("显示模式");
        AddMode(modes, "主设备", DisplayMode.Primary);
        AddMode(modes, "最低电量", DisplayMode.Lowest);
        AddMode(modes, "轮播", DisplayMode.Cycle);
        _menu.Items.Add(modes);

        // 新增外观选项，默认仍使用原有横向电池。
        var effects = new ToolStripMenuItem("电量显示效果");
        foreach (var (text, effect) in new[] { ("默认", IconEffect.Default), ("个性化图标", IconEffect.Personalized) })
        {
            var item = new ToolStripMenuItem(text) { Checked = _settings.IconEffect == effect };
            item.Click += (_, _) => { _settings.IconEffect = effect; SaveAndUpdate(); };
            effects.DropDownItems.Add(item);
        }
        _menu.Items.Add(effects);

        var monitored = new ToolStripMenuItem("监控设备");
        var primary = new ToolStripMenuItem("主设备");
        foreach (var device in _devices)
        {
            var monitoredItem = new ToolStripMenuItem(DeviceLabel(device))
            {
                Checked = _settings.MonitoredDeviceIds.Contains(device.Id),
                CheckOnClick = true
            };
            monitoredItem.Click += (_, _) =>
            {
                if (monitoredItem.Checked) _settings.MonitoredDeviceIds.Add(device.Id);
                else _settings.MonitoredDeviceIds.Remove(device.Id);
                _settings.Initialized = true;
                SaveAndUpdate();
            };
            monitored.DropDownItems.Add(monitoredItem);

            var primaryItem = new ToolStripMenuItem(DeviceLabel(device))
            {
                Checked = string.Equals(
                    _settings.PrimaryDeviceId,
                    device.Id,
                    StringComparison.OrdinalIgnoreCase)
            };
            primaryItem.Click += (_, _) =>
            {
                _settings.PrimaryDeviceId = device.Id;
                _settings.MonitoredDeviceIds.Add(device.Id);
                _settings.Initialized = true;
                SaveAndUpdate();
            };
            primary.DropDownItems.Add(primaryItem);
        }

        if (_devices.Count == 0)
        {
            monitored.DropDownItems.Add(new ToolStripMenuItem("未发现设备") { Enabled = false });
            primary.DropDownItems.Add(new ToolStripMenuItem("未发现设备") { Enabled = false });
        }

        _menu.Items.Add(monitored);
        _menu.Items.Add(primary);
        _menu.Items.Add(BuildDeviceIconMenu());
        var lowBattery = new ToolStripMenuItem($"低电量提醒（≤{_settings.LowBatteryThreshold}%）…");
        lowBattery.Click += (_, _) => ConfigureLowBatteryThreshold();
        _menu.Items.Add(lowBattery);
        var colors = new ToolStripMenuItem("电量颜色阈值…");
        colors.Click += (_, _) => ConfigureBatteryColors();
        _menu.Items.Add(colors);
        _menu.Items.Add(new ToolStripSeparator());

        var startup = new ToolStripMenuItem("开机自动启动")
        {
            Checked = _startupRegistration.IsEnabled(),
            CheckOnClick = true
        };
        startup.Click += (_, _) =>
        {
            if (!_startupRegistration.SetEnabled(startup.Checked))
            {
                MessageBox.Show("无法修改开机自动启动设置。", "蓝牙电量监视器", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            startup.Checked = _startupRegistration.IsEnabled();
        };
        _menu.Items.Add(startup);

        var refresh = new ToolStripMenuItem("立即刷新");
        refresh.Click += async (_, _) => await RefreshAsync();
        _menu.Items.Add(refresh);
        _menu.Items.Add(new ToolStripMenuItem("退出", null, (_, _) => ExitThread()));
    }

    private void AddMode(ToolStripMenuItem parent, string text, DisplayMode mode)
    {
        var item = new ToolStripMenuItem(text) { Checked = _settings.Mode == mode };
        item.Click += (_, _) =>
        {
            _settings.Mode = mode;
            _cycleIndex = 0;
            SaveAndUpdate();
        };
        parent.DropDownItems.Add(item);
    }

    // 驱动隐藏型号时可指定内置图形，恢复自动识别只需删除对应覆盖值。
    private ToolStripMenuItem BuildDeviceIconMenu()
    {
        var menu = new ToolStripMenuItem("设备图标");
        foreach (var device in _devices)
        {
            var deviceMenu = new ToolStripMenuItem(DeviceLabel(device));
            var automatic = new ToolStripMenuItem($"自动识别（{DeviceIconClassifier.Label(device.IconKind)}）")
            {
                Checked = !_settings.DeviceIcons.TryGetValue(device.Id, out var saved) || !Enum.IsDefined(saved)
            };
            automatic.Click += (_, _) => { _settings.DeviceIcons.Remove(device.Id); SaveAndUpdate(); };
            deviceMenu.DropDownItems.Add(automatic);
            deviceMenu.DropDownItems.Add(new ToolStripSeparator());
            foreach (var kind in Enum.GetValues<DeviceIconKind>())
            {
                var item = new ToolStripMenuItem(DeviceIconClassifier.Label(kind))
                {
                    Checked = _settings.DeviceIcons.TryGetValue(device.Id, out var selected) && selected == kind
                };
                item.Click += (_, _) => { _settings.DeviceIcons[device.Id] = kind; SaveAndUpdate(); };
                deviceMenu.DropDownItems.Add(item);
            }
            menu.DropDownItems.Add(deviceMenu);
        }
        if (_devices.Count == 0) menu.DropDownItems.Add(new ToolStripMenuItem("未发现设备") { Enabled = false });
        return menu;
    }

    private void SaveAndUpdate()
    {
        _settingsDirty = !_settingsStore.Save(_settings);
        UpdatePresentation();
        ShowLowBatteryNotification();
    }

    // 使用托盘的系统通知入口，多台设备同时不足时合并，避免通知互相覆盖。
    private void ShowLowBatteryNotification()
    {
        if (_exiting) return;
        var alerts = _lowBatteryReminder.GetNewAlerts(_devices, _settings);
        if (alerts.Count == 0) return;
        var text = string.Join(Environment.NewLine, alerts.Select(device => $"{device.Name}：{device.BatteryPercent}%"));
        // 系统托盘通知正文最多 255 个字符，预留末尾省略号。
        if (text.Length > 255) text = text[..254] + "…";
        try
        {
            // 普通电量提醒不使用系统黄色警告标志。
            _notifyIcon.ShowBalloonTip(10_000, $"蓝牙设备电量低（{alerts.Count} 台）", text, ToolTipIcon.None);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // 系统通知暂不可用时，继续保留设备读取和托盘显示。
        }
    }

    // 数值输入支持任意整数百分比，确认后沿用现有设置持久化流程。
    private void ConfigureLowBatteryThreshold()
    {
        using var dialog = new Form
        {
            Text = "低电量提醒", ClientSize = new Size(300, 130),
            FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterScreen,
            MaximizeBox = false, MinimizeBox = false, ShowInTaskbar = false,
            AutoScaleMode = AutoScaleMode.Dpi
        };
        using var label = new Label { Text = "设备电量小于等于此值时提醒（%）：", AutoSize = true, Location = new Point(16, 16) };
        using var input = new NumericUpDown
        {
            Minimum = 1, Maximum = 100, Value = _settings.LowBatteryThreshold,
            Location = new Point(16, 42), Width = 90
        };
        using var ok = new Button { Text = "确定", DialogResult = DialogResult.OK, Location = new Point(124, 88) };
        using var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Location = new Point(208, 88) };
        dialog.Controls.AddRange([label, input, ok, cancel]);
        dialog.AcceptButton = ok;
        dialog.CancelButton = cancel;
        if (dialog.ShowDialog() != DialogResult.OK) return;
        _settings.LowBatteryThreshold = (int)input.Value;
        SaveAndUpdate();
    }

    // 两个上限联动约束，始终保留红、黄、绿三个有效区间。
    private void ConfigureBatteryColors()
    {
        using var dialog = new Form
        {
            Text = "电量颜色阈值", ClientSize = new Size(340, 188),
            FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterScreen,
            MaximizeBox = false, MinimizeBox = false, ShowInTaskbar = false,
            AutoScaleMode = AutoScaleMode.Dpi
        };
        using var redLabel = new Label { Text = "红色上限（%）：", AutoSize = true, Location = new Point(16, 20) };
        using var yellowLabel = new Label { Text = "黄色上限（%）：", AutoSize = true, Location = new Point(16, 60) };
        using var red = new NumericUpDown
        {
            Minimum = 0, Maximum = _settings.YellowBatteryThreshold - 1,
            Value = _settings.RedBatteryThreshold, Location = new Point(180, 16), Width = 100
        };
        using var yellow = new NumericUpDown
        {
            Minimum = _settings.RedBatteryThreshold + 1, Maximum = 99,
            Value = _settings.YellowBatteryThreshold, Location = new Point(180, 56), Width = 100
        };
        using var ranges = new Label { AutoSize = true, Location = new Point(16, 100) };
        void UpdateRanges() => ranges.Text = $"红色 ≤{red.Value}%；黄色 ≤{yellow.Value}%；其余绿色";
        red.ValueChanged += (_, _) => { yellow.Minimum = red.Value + 1; UpdateRanges(); };
        yellow.ValueChanged += (_, _) => { red.Maximum = yellow.Value - 1; UpdateRanges(); };
        UpdateRanges();
        using var ok = new Button { Text = "确定", DialogResult = DialogResult.OK, Location = new Point(164, 144) };
        using var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Location = new Point(248, 144) };
        dialog.Controls.AddRange([redLabel, red, yellowLabel, yellow, ranges, ok, cancel]);
        dialog.AcceptButton = ok;
        dialog.CancelButton = cancel;
        if (dialog.ShowDialog() != DialogResult.OK) return;
        _settings.RedBatteryThreshold = (int)red.Value;
        _settings.YellowBatteryThreshold = (int)yellow.Value;
        SaveAndUpdate();
    }

    private static string DeviceLabel(BluetoothDeviceSnapshot device)
    {
        var state = !device.IsConnected
            ? "未连接"
            : device.BatteryPercent is byte percent ? $"{percent}%" : "电量未知";
        return $"{device.Name.Replace("&", "&&", StringComparison.Ordinal)} ({state})";
    }

    protected override void ExitThreadCore()
    {
        _exiting = true;
        _refreshTimer.Stop();
        _cycleTimer.Stop();
        _deviceChangeTimer.Stop();
        foreach (var watcher in _deviceWatchers)
            if (watcher.Status is DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted) watcher.Stop();
        _refreshTimer.Dispose();
        _cycleTimer.Dispose();
        _deviceChangeTimer.Dispose();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _tooltip.Dispose();
        var currentIcon = _currentIcon;
        _currentIcon = null;
        currentIcon?.Dispose();
        _nativeMenu.Dispose();
        _menu.Dispose();
        base.ExitThreadCore();
    }
}
