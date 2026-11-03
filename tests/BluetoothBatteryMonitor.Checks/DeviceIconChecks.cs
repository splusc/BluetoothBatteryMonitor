using BluetoothBatteryMonitor;

namespace BluetoothBatteryMonitor.Checks;

// 验证型号识别不混淆同名的 PS4、PS5，并允许手动修正设备图标。
internal static class DeviceIconChecks
{
    public static void Run()
    {
        Check.Equal(DeviceIconKind.Xbox, DeviceIconClassifier.Classify("Xbox Wireless Controller"), "Xbox 名称");
        Check.Equal(DeviceIconKind.DualShock4, DeviceIconClassifier.Classify("Wireless Controller", @"HID\VID_054C&PID_09CC"), "PS4 硬件标识");
        Check.Equal(DeviceIconKind.DualShock4, DeviceIconClassifier.Classify("Wireless Controller", @"BTHENUM\VID&0002054C_PID&05C4"), "PS4 蓝牙标识");
        Check.Equal(DeviceIconKind.DualSense, DeviceIconClassifier.Classify("Wireless Controller", @"HID\VID_054C&PID_0CE6"), "PS5 硬件标识");
        Check.Equal(DeviceIconKind.DualSense, DeviceIconClassifier.Classify("DualShock 4", @"HID\VID_054C&PID_0CE6"), "硬件标识优先于名称");
        Check.Equal(DeviceIconKind.Gamepad, DeviceIconClassifier.Classify("Wireless Controller"), "不根据通用名称猜测 PS 型号");
        Check.Equal(DeviceIconKind.DualShock4, DeviceIconClassifier.Classify("Sony DualShock 4"), "明确的 PS4 名称");
        Check.Equal(DeviceIconKind.DualSense, DeviceIconClassifier.Classify("Sony DualSense"), "明确的 PS5 名称");
        Check.Equal(DeviceIconKind.Keyboard, DeviceIconClassifier.Classify("蓝牙键盘"), "中文键盘名称");
        Check.Equal(DeviceIconKind.Mouse, DeviceIconClassifier.Classify("MX Master", majorClass: 5, minorClass: 0x20), "鼠标设备类别");
        Check.Equal(DeviceIconKind.Keyboard, DeviceIconClassifier.Classify("K380", majorClass: 5, minorClass: 0x10), "键盘设备类别");
        Check.Equal(DeviceIconKind.Headphones, DeviceIconClassifier.Classify("AirPods Pro"), "耳机名称");
        Check.Equal(DeviceIconKind.Headphones, DeviceIconClassifier.Classify("Xbox Wireless Headset"), "Xbox 耳机不能误认成手柄");
        Check.Equal(DeviceIconKind.Headphones, DeviceIconClassifier.Classify("PS5 Headset"), "PS5 耳机不能误认成手柄");
        Check.Equal(DeviceIconKind.Gamepad, DeviceIconClassifier.Classify("DualShock 3 Controller"), "旧款 PS 手柄不能冒充 PS4");
        Check.Equal(DeviceIconKind.Unknown, DeviceIconClassifier.Classify("My Device"), "未知设备不冒充已知类型");

        var device = new BluetoothDeviceSnapshot("controller", "Wireless Controller", true, 80, DeviceIconKind.Gamepad);
        var settings = new AppSettings();
        Check.Equal(DeviceIconKind.Gamepad, settings.GetIconKind(device), "自动图标");
        settings.DeviceIcons["CONTROLLER"] = DeviceIconKind.DualSense;
        Check.Equal(DeviceIconKind.DualSense, settings.GetIconKind(device), "手动指定图标不区分设备 ID 大小写");
        settings.DeviceIcons["CONTROLLER"] = (DeviceIconKind)999;
        Check.Equal(DeviceIconKind.Gamepad, settings.GetIconKind(device), "无效图标回退自动识别");
    }
}
