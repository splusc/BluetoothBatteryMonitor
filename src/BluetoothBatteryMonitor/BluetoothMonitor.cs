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
    private static readonly string[] RequestedProperties = [ConnectedProperty, BatteryProperty];
    private static readonly TimeSpan GattTimeout = TimeSpan.FromSeconds(4);

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

        var snapshots = classic
            .Concat(lowEnergy)
            .Select(Map)
            .GroupBy(device => device.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(device => device.IsConnected)
                .ThenByDescending(device => device.BatteryPercent.HasValue)
                .First())
            .OrderBy(device => device.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        // Enrich BLE devices with battery level via GATT Battery Service.
        // System.Devices.BatteryLife is not populated for Bluetooth devices;
        // battery data lives in the GATT Battery Service (0x180F) on the device.
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

        return snapshots;
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

    /// <summary>
    /// Reads battery level via the GATT Battery Service (0x180F).
    /// Returns null if the device doesn't expose the service, the read fails,
    /// or the operation times out.
    /// </summary>
    private static async Task<byte?> ReadBatteryViaGattAsync(string deviceId)
    {
        BluetoothLEDevice? device = null;
        try
        {
            device = await BluetoothLEDevice.FromIdAsync(deviceId);
            if (device is null || device.ConnectionStatus != BluetoothConnectionStatus.Connected)
                return null;

            var serviceResult = await device.GetGattServicesForUuidAsync(
                GattServiceUuids.Battery);
            if (serviceResult.Status != GattCommunicationStatus.Success
                || serviceResult.Services.Count == 0)
                return null;

            var charResult = await serviceResult.Services[0]
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
            device?.Dispose();
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

    private static BluetoothDeviceSnapshot Map(DeviceInformation device)
    {
        var connected = device.Properties.TryGetValue(ConnectedProperty, out var connectedValue)
            && connectedValue is true;
        device.Properties.TryGetValue(BatteryProperty, out var batteryValue);
        return new BluetoothDeviceSnapshot(
            device.Id,
            string.IsNullOrWhiteSpace(device.Name) ? "Bluetooth device" : device.Name,
            connected,
            NormalizeBattery(batteryValue));
    }
}
