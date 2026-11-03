using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace BluetoothBatteryMonitor;

// DualShock 4 使用经典蓝牙 HID，电量不在 BLE Battery Service 中。
internal static class DualShockBatteryReader
{
    private const uint GetFeature = 0x000B0192; // IOCTL_HID_GET_FEATURE
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(2);

    public static Task<byte?> ReadAsync(IEnumerable<string> interfaces) => Task.Run(() =>
    {
        var elapsed = Stopwatch.StartNew();
        foreach (var path in interfaces)
        {
            if (DeviceIconClassifier.Classify("", path) != DeviceIconKind.DualShock4) continue;
            using var handle = CreateFile(path, 0x80000000, 3, IntPtr.Zero, 3, 0x40000000, IntPtr.Zero);
            if (handle.IsInvalid) continue; // DS4Windows 独占或 HidHide 隐藏时保留未知电量。
            var (reportSize, featureSize) = GetReportSizes(handle);
            if (reportSize < 78) continue;
            var requestedFullReport = false;
            while (elapsed.Elapsed < ReadTimeout)
            {
                var report = new byte[reportSize];
                var count = Transfer(handle, report, false, ReadTimeout - elapsed.Elapsed);
                if (count <= 0) break;
                // Windows 要求缓冲覆盖描述符中的最大报告长度，实际 0x11 数据只有 78 字节。
                var battery = ParseBluetoothReport(report.AsSpan(0, Math.Min(count, 78)));
                if (battery.HasValue) return battery;
                if (report[0] == 0x01 && !requestedFullReport)
                {
                    // 读取校准特征报告以启用完整的蓝牙输入报告，不发送灯光或震动输出。
                    requestedFullReport = true;
                    var calibration = new byte[Math.Max(41, featureSize)];
                    calibration[0] = 0x05;
                    if (Transfer(handle, calibration, true, ReadTimeout - elapsed.Elapsed) < 0) break;
                }
            }
        }
        return (byte?)null;
    });

    private static (int Input, int Feature) GetReportSizes(SafeFileHandle handle)
    {
        if (!HidD_GetPreparsedData(handle, out var preparsed)) return (0, 0);
        var caps = Marshal.AllocHGlobal(64); // HIDP_CAPS
        try
        {
            return HidP_GetCaps(preparsed, caps) == 0x00110000
                ? ((ushort)Marshal.ReadInt16(caps, 4), (ushort)Marshal.ReadInt16(caps, 8)) : (0, 0);
        }
        finally
        {
            Marshal.FreeHGlobal(caps);
            HidD_FreePreparsedData(preparsed);
        }
    }

    internal static byte? ParseBluetoothReport(ReadOnlySpan<byte> report)
    {
        // 0x01 是没有电量的简短蓝牙报告，不能将其填充零误读成低电量。
        if (report.Length != 78 || report[0] != 0x11) return null;
        var status = report[32];
        var level = status & 0x0F;
        var charging = (status & 0x10) != 0;
        if (level == 11 && charging) return 100;
        if (level > 10) return null; // 保留充电错误、保留值为未知。
        // 与 DS4Windows 的 BATTERY_MAX=8、BATTERY_MAX_USB=11 保持一致。
        // 使用整数除法，例如未充电档位 7 => 87%，而非 Linux 区间中点算法的 75%。
        return (byte)Math.Min(100, level * 100 / (charging ? 11 : 8));
    }

    // 每次 I/O 都有独立事件及超时，超时后取消并等待完成，再释放原生缓冲。
    private static int Transfer(SafeFileHandle handle, byte[] data, bool feature, TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero) return -1;
        using var completed = new EventWaitHandle(false, EventResetMode.ManualReset);
        var buffer = Marshal.AllocHGlobal(data.Length);
        var overlapped = Marshal.AllocHGlobal(Marshal.SizeOf<Overlapped>());
        try
        {
            Marshal.Copy(data, 0, buffer, data.Length);
            Marshal.StructureToPtr(new Overlapped { Event = completed.SafeWaitHandle.DangerousGetHandle() }, overlapped, false);
            var started = feature
                ? DeviceIoControl(handle, GetFeature, IntPtr.Zero, 0, buffer, data.Length, out _, overlapped)
                : ReadFile(handle, buffer, data.Length, out _, overlapped);
            if (!started && Marshal.GetLastWin32Error() != 997) return -1; // ERROR_IO_PENDING
            if (!started && !completed.WaitOne(timeout))
            {
                CancelIoEx(handle, overlapped);
                GetOverlappedResult(handle, overlapped, out _, true);
                return -1;
            }
            if (!GetOverlappedResult(handle, overlapped, out var length, true)) return -1;
            Marshal.Copy(buffer, data, 0, data.Length);
            return Math.Min(length, data.Length);
        }
        finally
        {
            Marshal.FreeHGlobal(overlapped);
            Marshal.FreeHGlobal(buffer);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Overlapped
    {
        public UIntPtr Internal;
        public UIntPtr InternalHigh;
        public uint Offset;
        public uint OffsetHigh;
        public IntPtr Event;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security,
        uint creation, uint flags, IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadFile(SafeFileHandle handle, IntPtr buffer, int size, out int read, IntPtr overlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(SafeFileHandle handle, uint code, IntPtr input, int inputSize,
        IntPtr output, int outputSize, out int returned, IntPtr overlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetOverlappedResult(SafeFileHandle handle, IntPtr overlapped, out int transferred,
        [MarshalAs(UnmanagedType.Bool)] bool wait);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CancelIoEx(SafeFileHandle handle, IntPtr overlapped);

    [DllImport("hid.dll")]
    [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool HidD_GetPreparsedData(SafeFileHandle handle, out IntPtr data);

    [DllImport("hid.dll")]
    [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool HidD_FreePreparsedData(IntPtr data);

    [DllImport("hid.dll")]
    private static extern int HidP_GetCaps(IntPtr data, IntPtr caps);
}
