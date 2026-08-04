using System.ComponentModel;

namespace TrayMonitor;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly BluetoothMonitor _monitor = new();
    private readonly SettingsStore _settingsStore = new();
    private readonly AppSettings _settings;
    private readonly ContextMenuStrip _menu = new();
    private readonly object _presentationLock = new();
    private readonly NotifyIcon _notifyIcon;
    private readonly System.Windows.Forms.Timer _refreshTimer = new() { Interval = 60_000 };
    private readonly System.Windows.Forms.Timer _cycleTimer = new() { Interval = 5_000 };
    private IReadOnlyList<BluetoothDeviceSnapshot> _devices = [];
    private Icon? _currentIcon;
    private bool _refreshing;
    private bool _exiting;
    private bool _settingsDirty;
    private int _cycleIndex;
    private string _emptyStatus = "正在读取蓝牙设备";

    public TrayApplicationContext()
    {
        _settings = _settingsStore.Load();
        _menu.Opening += BuildMenu;
        _currentIcon = BatteryIcon.Create(null);
        _notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = _menu,
            Icon = _currentIcon,
            Text = _emptyStatus,
            Visible = true
        };
        _refreshTimer.Tick += async (_, _) => await RefreshAsync();
        _cycleTimer.Tick += (_, _) => AdvanceCycle();
        _refreshTimer.Start();
        _cycleTimer.Start();
        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (_refreshing || _exiting) return;
        _refreshing = true;
        try
        {
            _devices = await _monitor.GetDevicesAsync();
            _emptyStatus = _devices.Count == 0 ? "未发现已配对蓝牙设备" : "没有可显示的设备";

            if (_settingsDirty) _settingsDirty = !_settingsStore.Save(_settings);

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
            var tooltip = DisplayLogic.BuildTooltip(monitored, current, _emptyStatus);
            candidate = BatteryIcon.Create(current?.BatteryPercent);
            lock (_presentationLock)
            {
                if (_exiting) return;

                var previousTooltip = _notifyIcon.Text;
                _notifyIcon.Text = tooltip;
                try
                {
                    _notifyIcon.Icon = candidate;
                }
                catch
                {
                    _notifyIcon.Text = previousTooltip;
                    throw;
                }

                var previousIcon = _currentIcon;
                _currentIcon = candidate;
                candidate = null;
                previousIcon?.Dispose();
            }
        }
        catch
        {
        }
        finally
        {
            candidate?.Dispose();
        }
    }

    private void BuildMenu(object? sender, CancelEventArgs eventArgs)
    {
        var oldItems = _menu.Items.Cast<ToolStripItem>().ToArray();
        _menu.Items.Clear();
        foreach (var item in oldItems) item.Dispose();

        var modes = new ToolStripMenuItem("显示模式");
        AddMode(modes, "主设备", DisplayMode.Primary);
        AddMode(modes, "最低电量", DisplayMode.Lowest);
        AddMode(modes, "轮播", DisplayMode.Cycle);
        _menu.Items.Add(modes);

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
        _menu.Items.Add(new ToolStripSeparator());

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

    private void SaveAndUpdate()
    {
        _settingsDirty = !_settingsStore.Save(_settings);
        UpdatePresentation();
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
        lock (_presentationLock)
        {
            _exiting = true;
            _refreshTimer.Stop();
            _cycleTimer.Stop();
            _refreshTimer.Dispose();
            _cycleTimer.Dispose();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            var currentIcon = _currentIcon;
            _currentIcon = null;
            currentIcon?.Dispose();
        }
        _menu.Dispose();
        base.ExitThreadCore();
    }
}
