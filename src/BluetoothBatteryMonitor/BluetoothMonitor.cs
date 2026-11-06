using System.Globalization;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

namespace BluetoothBatteryMonitor;

internal sealed class BluetoothMonitor
{
    private const string ConnectedProperty = "System.Devices.Aep.IsConnected";
    private const string BatteryProperty = "System.Devices.BatteryLife";
    private const string NodeBatteryProperty = "{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2"; // Windows 蓝牙驱动记录的耳机电量。
    private const string NodeContainerProperty = "System.Devices.ContainerId"; // PnP 子节点使用的设备容器属性。
    // 设备容器用于关联 HID 硬件标识，避免把同名的 PS4、PS5 混为一类。
    private const string ContainerProperty = "System.Devices.Aep.ContainerId";
    // 经典蓝牙的外设类别可识别没有明确类型名称的键盘、鼠标和耳机。
    private const string MajorClassProperty = "System.Devices.Aep.Bluetooth.Cod.Major";
    private const string MinorClassProperty = "System.Devices.Aep.Bluetooth.Cod.Minor"; // 蓝牙次类别。
    private const string AppearanceProperty = "System.Devices.Aep.Bluetooth.Le.Appearance"; // BLE 外观类型，包含键盘、鼠标等明确子类型。
    private static readonly string[] RequestedProperties =
        [ConnectedProperty, BatteryProperty, ContainerProperty, MajorClassProperty, MinorClassProperty, AppearanceProperty];
    private static readonly TimeSpan GattTimeout = TimeSpan.FromSeconds(4);

    // 连接状态看配对端点，HFP 电量变化看蓝牙 PnP 子节点，不依赖固定轮询周期。
    public static DeviceWatcher[] CreateWatchers() =>
    [
        DeviceInformation.CreateWatcher(
            $"({BluetoothDevice.GetDeviceSelectorFromPairingState(true)}) OR ({BluetoothLEDevice.GetDeviceSelectorFromPairingState(true)})",
            RequestedProperties, DeviceInformationKind.AssociationEndpoint),
        DeviceInformation.CreateWatcher("System.Devices.DeviceInstanceId:~<\"BTH\"",
            [NodeBatteryProperty, NodeContainerProperty], DeviceInformationKind.Device)
    ];

    public async Task<IReadOnlyList<BluetoothDeviceSnapshot>> GetDevicesAsync()
    {
        var classic = await DeviceInformation.FindAllAsync(
            BluetoothDevice.GetDeviceSelectorFromPairingState(true),
            RequestedProperties,
            DeviceInformationKind.AssociationEndpoint);
        var lowEnergy = await DeviceInformation.FindAllAsync(
            BluetoothLEDevice.GetDeviceSelectorFromPairingState(true),
            RequestedProperties,
            DeviceInformationKind.AssociationEndpoint);

        var leDeviceIds = new HashSet<string>(
            lowEnergy.Select(d => d.Id), StringComparer.OrdinalIgnoreCase);
        var hardwareByContainer = classic.Count + lowEnergy.Count > 0
            ? await GetHardwareByContainerAsync()
            : new Dictionary<Guid, List<string>>();
        var batteryByContainer = await GetBatteryByContainerAsync(classic.Concat(lowEnergy));

        var snapshots = classic
            .Concat(lowEnergy)
            .Select(device => Map(device, hardwareByContainer, batteryByContainer))
            .GroupBy(device => device.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(device => device.IsConnected)
                .ThenByDescending(device => device.BatteryPercent.HasValue)
                .ThenByDescending(device => device.IconKind is not DeviceIconKind.Unknown and not DeviceIconKind.Gamepad)
                .First())
            .OrderBy(device => device.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        // Windows 未提供电量时，再尝试 BLE 标准电量服务。
        var gattTargets = snapshots
            .Where(s => s.IsConnected && !s.BatteryPercent.HasValue && leDeviceIds.Contains(s.Id))
            .ToArray();

        if (gattTargets.Length > 0)
        {
            var results = await Task.WhenAll(
                gattTargets.Select(async s => (
                    Id: s.Id,
                    Battery: await ReadBatteryWithTimeoutAsync(s.Id, GattTimeout))));

            var batteryByDevice = new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
            foreach (var (id, battery) in results)
                if (battery.HasValue) batteryByDevice[id] = battery.Value;

            if (batteryByDevice.Count > 0)
            {
                snapshots = snapshots
                    .Select(s => batteryByDevice.TryGetValue(s.Id, out var batt)
                        ? s with { BatteryPercent = batt }
                        : s)
                    .ToArray();
            }
        }

        // 用同一容器内的 Sony HID 接口读取 PS4，不能按 Wireless Controller 名称匹配。
        var ds4Targets = snapshots.Where(s => s.IsConnected && !s.BatteryPercent.HasValue
            && s.IconKind == DeviceIconKind.DualShock4).ToArray();
        foreach (var target in ds4Targets)
        {
            var endpoint = classic.Concat(lowEnergy).First(d => d.Id.Equals(target.Id, StringComparison.OrdinalIgnoreCase));
            if (!endpoint.Properties.TryGetValue(ContainerProperty, out var value)
                || !Guid.TryParse(value?.ToString(), out var container)
                || !hardwareByContainer.TryGetValue(container, out var interfaces)) continue;
            var battery = await DualShockBatteryReader.ReadAsync(interfaces);
            if (battery.HasValue)
                snapshots = snapshots.Select(s => s.Id == target.Id ? s with { BatteryPercent = battery } : s).ToArray();
        }

        var containers = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var endpoint in classic.Concat(lowEnergy))
            if (endpoint.Properties.TryGetValue(ContainerProperty, out var value)
                && Guid.TryParse(value?.ToString(), out var container) && container != Guid.Empty)
                containers[endpoint.Id] = container;
        return MergeDeviceSnapshots(snapshots, containers);
    }

    internal static byte? NormalizeBattery(object? value)
    {
        if (value is null) return null;
        try
        {
            var number = Convert.ToInt32(value, CultureInfo.InvariantCulture);
            return number is >= 0 and <= 100 ? (byte)number : null;
        }
        catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException)
        {
            return null;
        }
    }

    // 聚合同一设备的多个蓝牙端点，保留独立容器的同名设备。
    internal static IReadOnlyList<BluetoothDeviceSnapshot> MergeDeviceSnapshots(
        IReadOnlyList<BluetoothDeviceSnapshot> devices, IReadOnlyDictionary<string, Guid> containers) => devices
        .GroupBy(device => containers.TryGetValue(device.Id, out var container) && container != Guid.Empty
            ? $"container:{container:D}" : device.Id, StringComparer.OrdinalIgnoreCase)
        .Select(group =>
        {
            var representative = group.OrderByDescending(device => device.IsConnected).First();
            var battery = group.Where(device => device.BatteryPercent.HasValue)
                .OrderByDescending(device => device.IsConnected).ThenBy(device => device.BatteryPercent).FirstOrDefault();
            var icon = group.OrderByDescending(device => device.IconKind is not DeviceIconKind.Unknown and not DeviceIconKind.Gamepad)
                .ThenByDescending(device => device.IconKind != DeviceIconKind.Unknown).First().IconKind;
            return representative with
            {
                Id = group.Key,
                BatteryPercent = battery?.BatteryPercent,
                IconKind = icon,
                EndpointIds = group.Select(device => device.Id).ToArray()
            };
        })
        .OrderBy(device => device.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();

    // 先保留端点已有电量，再按容器关联子节点，不能按名称借用其他设备的电量。
    internal static byte? ResolveBattery(IReadOnlyDictionary<string, object> properties,
        IReadOnlyDictionary<Guid, byte> batteryByContainer)
    {
        properties.TryGetValue(BatteryProperty, out var value);
        if (NormalizeBattery(value) is byte battery) return battery;
        properties.TryGetValue(ContainerProperty, out var containerValue);
        return Guid.TryParse(containerValue?.ToString(), out var container) && container != Guid.Empty
            && batteryByContainer.TryGetValue(container, out var fallback) ? fallback : null;
    }

    // HFP 耳机的电量可能仅存在于 Hands-Free 子节点，BLE 服务并非唯一来源。
    private static async Task<Dictionary<Guid, byte>> GetBatteryByContainerAsync(IEnumerable<DeviceInformation> endpoints)
    {
        var result = new Dictionary<Guid, byte>();
        var containers = endpoints.Select(device =>
            device.Properties.TryGetValue(ContainerProperty, out var value)
                && Guid.TryParse(value?.ToString(), out var container) ? container : Guid.Empty)
            .Where(container => container != Guid.Empty).Distinct().ToArray();
        if (containers.Length == 0) return result;
        try
        {
            var selector = string.Join(" OR ", containers.Select(container => $"{NodeContainerProperty}:=\"{{{container}}}\""));
            var nodes = await DeviceInformation.FindAllAsync(selector,
                [NodeContainerProperty, NodeBatteryProperty], DeviceInformationKind.Device).AsTask().WaitAsync(GattTimeout);
            foreach (var node in nodes)
            {
                if (!node.Properties.TryGetValue(NodeContainerProperty, out var value)
                    || !Guid.TryParse(value?.ToString(), out var container) || container == Guid.Empty
                    || !node.Properties.TryGetValue(NodeBatteryProperty, out var batteryValue)
                    || NormalizeBattery(batteryValue) is not byte battery) continue;
                result[container] = battery;
            }
        }
        catch
        {
            // 系统属性查询失败时仍保留原有 BLE 和手柄读取途径。
        }
        return result;
    }

    /// <summary>
    /// Reads battery level via the GATT Battery Service (0x180F).
    /// Returns null if the device doesn't expose the service, the read fails,
    /// or the operation times out.
    /// </summary>
    private static async Task<byte?> ReadBatteryViaGattAsync(string deviceId)
    {
        BluetoothLEDevice? device = null;
        // GATT 服务拥有独立的原生句柄，必须在每次读取后释放，不能只释放蓝牙设备。
        IReadOnlyList<GattDeviceService>? services = null;
        try
        {
            device = await BluetoothLEDevice.FromIdAsync(deviceId);
            if (device is null || device.ConnectionStatus != BluetoothConnectionStatus.Connected)
                return null;

            var serviceResult = await device.GetGattServicesForUuidAsync(
                GattServiceUuids.Battery);
            services = serviceResult.Services;
            if (serviceResult.Status != GattCommunicationStatus.Success
                || services.Count == 0)
                return null;

            var charResult = await services[0]
                .GetCharacteristicsForUuidAsync(GattCharacteristicUuids.BatteryLevel);
            if (charResult.Status != GattCommunicationStatus.Success
                || charResult.Characteristics.Count == 0)
                return null;

            var readResult = await charResult.Characteristics[0].ReadValueAsync();
            if (readResult.Status != GattCommunicationStatus.Success)
                return null;

            using var reader = DataReader.FromBuffer(readResult.Value);
            var value = reader.ReadByte();
            return value <= 100 ? value : null;
        }
        catch
        {
            return null;
        }
        finally
        {
            try
            {
                if (services is not null)
                    foreach (var service in services) service.Dispose();
            }
            finally
            {
                device?.Dispose();
            }
        }
    }

    /// <summary>
    /// Wraps <see cref="ReadBatteryViaGattAsync"/> with a wall-clock timeout.
    /// WinRT Bluetooth APIs can hang indefinitely on driver errors — the only
    /// known recovery is a reboot — so timeouts are essential.
    /// </summary>
    private static async Task<byte?> ReadBatteryWithTimeoutAsync(string deviceId, TimeSpan timeout)
    {
        var task = ReadBatteryViaGattAsync(deviceId);
        var completed = await Task.WhenAny(task, Task.Delay(timeout));
        return completed == task ? await task : null;
    }

    // 保留各个 HID 路径，既用于型号识别，也用于同容器 PS4 的电量读取。
    private static async Task<Dictionary<Guid, List<string>>> GetHardwareByContainerAsync()
    {
        var result = new Dictionary<Guid, List<string>>();
        try
        {
            var interfaces = await DeviceInformation.FindAllAsync(
                "System.Devices.InterfaceClassGuid:=\"{4D1E55B2-F16F-11CF-88CB-001111000030}\"",
                ["System.Devices.ContainerId"], DeviceInformationKind.DeviceInterface).AsTask().WaitAsync(GattTimeout);
            foreach (var entry in interfaces)
            {
                if (!entry.Properties.TryGetValue("System.Devices.ContainerId", out var value)
                    || !Guid.TryParse(value?.ToString(), out var container) || container == Guid.Empty) continue;
                if (!result.TryGetValue(container, out var paths)) result[container] = paths = [];
                paths.Add(entry.Id);
            }
        }
        catch
        {
            // 驱动未提供容器信息时保留名称识别和手动选择，不中断托盘刷新。
        }
        return result;
    }

    // 将设备名称、经典蓝牙类别、BLE 外观及同容器的硬件标识统一交给图形分类逻辑。
    private static BluetoothDeviceSnapshot Map(DeviceInformation device, IReadOnlyDictionary<Guid, List<string>> hardwareByContainer,
        IReadOnlyDictionary<Guid, byte> batteryByContainer)
    {
        var connected = device.Properties.TryGetValue(ConnectedProperty, out var connectedValue)
            && connectedValue is true;
        device.Properties.TryGetValue(ContainerProperty, out var containerValue);
        var hardwareId = device.Id;
        if (Guid.TryParse(containerValue?.ToString(), out var container)
            && hardwareByContainer.TryGetValue(container, out var hidIds)) hardwareId += " " + string.Join(" ", hidIds);
        device.Properties.TryGetValue(MajorClassProperty, out var majorValue);
        device.Properties.TryGetValue(MinorClassProperty, out var minorValue);
        device.Properties.TryGetValue(AppearanceProperty, out var appearanceValue);
        int.TryParse(Convert.ToString(majorValue, CultureInfo.InvariantCulture), out var majorClass);
        int.TryParse(Convert.ToString(minorValue, CultureInfo.InvariantCulture), out var minorClass);
        int.TryParse(Convert.ToString(appearanceValue, CultureInfo.InvariantCulture), out var appearance);
        return new BluetoothDeviceSnapshot(
            device.Id,
            string.IsNullOrWhiteSpace(device.Name) ? "Bluetooth device" : device.Name,
            connected,
            ResolveBattery(device.Properties, batteryByContainer),
            DeviceIconClassifier.Classify(device.Name, hardwareId, majorClass, minorClass, appearance));
    }
}
