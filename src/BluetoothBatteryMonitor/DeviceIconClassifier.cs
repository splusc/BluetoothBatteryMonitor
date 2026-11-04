using System.Text.RegularExpressions;

namespace BluetoothBatteryMonitor;

// 电量显示效果与设备选择模式分别保存，首次使用保持原有彩色电池。
internal enum IconEffect
{
    Default, // 当前横向电池。
    Personalized // 白色设备图形和随电量阶段变色的竖向电池。
}

// 内置图形使用稳定名称，以便设置文件和嵌入资源直接对应。
internal enum DeviceIconKind
{
    Unknown, // 通用蓝牙设备。
    Keyboard, // 键盘。
    Mouse, // 鼠标。
    Xbox, // Xbox 手柄。
    DualShock4, // PS4 DualShock 4。
    DualSense, // PS5 DualSense。
    Headphones, // 耳机。
    Gamepad // 无法确定型号的手柄。
}

// 优先使用型号硬件标识和系统蓝牙类型，名称只在系统未提供明确类型时兜底。
internal static class DeviceIconClassifier
{
    // appearance 为 Windows 返回的 BLE 外观编码，缺失时为 0。
    public static DeviceIconKind Classify(string name, string hardwareId = "", int majorClass = 0, int minorClass = 0, int appearance = 0)
    {
        var hardware = Regex.Match(hardwareId,
            @"VID(?:_|&(?:[0-9A-F]{4})?)([0-9A-F]{4}).*?PID[_&]([0-9A-F]{4})",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (hardware.Success && hardware.Groups[1].Value.Equals("054C", StringComparison.OrdinalIgnoreCase))
        {
            switch (hardware.Groups[2].Value.ToUpperInvariant())
            {
                case "05C4":
                case "09CC": return DeviceIconKind.DualShock4;
                case "0CE6": return DeviceIconKind.DualSense;
            }
        }

        // BLE 外观的 0x03C1/0x03C2 分别是键盘/鼠标；组合键盘可能同时暴露鼠标 HID。
        if (appearance == 0x03C1) return DeviceIconKind.Keyboard;
        if (appearance == 0x03C2) return DeviceIconKind.Mouse;
        // 经典蓝牙外设次类别中的高两位分别表示键盘和指向设备。
        if (majorClass == 5)
        {
            if ((minorClass & 0x20) != 0) return DeviceIconKind.Mouse;
            if ((minorClass & 0x10) != 0) return DeviceIconKind.Keyboard;
        }
        if (majorClass == 4 && minorClass is 1 or 2 or 4 or 6) return DeviceIconKind.Headphones;

        var normalized = name.ToLowerInvariant();
        // 平台品牌也会出现在耳机、键盘等设备名称里，应先判断明确的设备类型。
        if (normalized.Contains("headphone") || normalized.Contains("headset") || normalized.Contains("earbud")
            || normalized.Contains("airpods") || normalized.Contains("耳机")) return DeviceIconKind.Headphones;
        if (normalized.Contains("keyboard") || normalized.Contains("键盘")) return DeviceIconKind.Keyboard;
        if (normalized.Contains("mouse") || normalized.Contains("鼠标")) return DeviceIconKind.Mouse;
        if (normalized.Contains("xbox")) return DeviceIconKind.Xbox;
        if (normalized.Contains("dualsense") || Regex.IsMatch(normalized, @"\bps[ -]?5\b")) return DeviceIconKind.DualSense;
        if (Regex.IsMatch(normalized, @"dualshock[ -]?4\b|\bps[ -]?4\b")) return DeviceIconKind.DualShock4;

        // 通用游戏设备类型不能盖过上面已识别出的具体手柄型号。
        if (appearance is 0x03C3 or 0x03C4 || (majorClass == 5 && (minorClass & 0x0F) is 1 or 2))
            return DeviceIconKind.Gamepad;
        if (normalized.Contains("controller") || normalized.Contains("gamepad") || normalized.Contains("手柄")) return DeviceIconKind.Gamepad;
        return DeviceIconKind.Unknown;
    }

    // 菜单统一使用中文名称，型号名称保持品牌的官方拼写。
    public static string Label(DeviceIconKind kind) => kind switch
    {
        DeviceIconKind.Keyboard => "键盘",
        DeviceIconKind.Mouse => "鼠标",
        DeviceIconKind.Xbox => "Xbox 手柄",
        DeviceIconKind.DualShock4 => "PS4 · DualShock 4",
        DeviceIconKind.DualSense => "PS5 · DualSense",
        DeviceIconKind.Headphones => "耳机",
        DeviceIconKind.Gamepad => "通用手柄",
        _ => "通用蓝牙设备"
    };
}
