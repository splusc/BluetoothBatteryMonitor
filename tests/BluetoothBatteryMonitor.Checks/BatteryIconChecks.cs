using BluetoothBatteryMonitor;

namespace BluetoothBatteryMonitor.Checks;

internal static class BatteryIconChecks
{
    public static void Run()
    {
        // 检查真正的图形合成，高 DPI 下基础图形不得被放大裁切。
        foreach (var showBattery in new[] { false, true })
        {
            using var reference = RenderAtDpi(96, showBattery);
            foreach (var dpi in new[] { 120, 144, 192, 240, 288 })
            {
                using var actual = RenderAtDpi(dpi, showBattery);
                Check.True(Enumerable.Range(0, 1024).All(p =>
                    reference.GetPixel(p % 32, p / 32) == actual.GetPixel(p % 32, p / 32)),
                    $"{dpi} DPI 的{(showBattery ? "电池合成图" : "基础手柄")}与 96 DPI 像素一致");
            }
        }
        using var unknown = BatteryIcon.Create(null);
        using var charged = BatteryIcon.Create(75);
        Check.True(unknown.Handle != IntPtr.Zero, "unknown icon handle");
        Check.True(charged.Handle != IntPtr.Zero, "charged icon handle");

        // 通过实际绘图检查电量填充与颜色，零电量不得仍显示一格。
        foreach (var kind in Enum.GetValues<DeviceIconKind>())
        {
            // 限制原生解码缓冲，防止把美术原图误嵌入运行时资源。
            using var stream = typeof(BatteryIcon).Assembly.GetManifestResourceStream($"BluetoothBatteryMonitor.Assets.DeviceIcons.{kind}.png")!;
            using var resource = System.Drawing.Image.FromStream(stream);
            Check.True(resource.Width * resource.Height * 4 <= 4096, $"{kind} 设备图形解码缓冲不超过 4KB");
            if (kind is DeviceIconKind.DualShock4 or DeviceIconKind.DualSense or DeviceIconKind.Xbox or DeviceIconKind.Gamepad)
            {
                using var silhouette = new System.Drawing.Bitmap(resource);
                var visible = Enumerable.Range(0, 1024)
                    .Where(p => silhouette.GetPixel(p % 32, p / 32).A > 32).ToArray();
                Check.True(visible.Min(p => p % 32) <= 1 && visible.Max(p => p % 32) >= 30,
                    $"{kind} 手柄填满托盘宽度");
                Check.True(Math.Abs(visible.Min(p => p / 32) + visible.Max(p => p / 32) - 31) <= 1,
                    $"{kind} 手柄在电池覆盖前垂直居中");
            }
            using var fullIcon = BatteryIcon.Create(100, IconEffect.Personalized, kind);
            using var lowIcon = BatteryIcon.Create(15, IconEffect.Personalized, kind);
            using var emptyIcon = BatteryIcon.Create(0, IconEffect.Personalized, kind);
            using var unknownIcon = BatteryIcon.Create(null, IconEffect.Personalized, kind);
            using var full = fullIcon.ToBitmap();
            using var low = lowIcon.ToBitmap();
            using var empty = emptyIcon.ToBitmap();
            Check.Equal(32, full.Width, "托盘图标宽度");
            Check.True(IsGreen(full.GetPixel(25, 9)), $"{kind} 满电上部填充");
            Check.True(!IsGreen(low.GetPixel(25, 9)), $"{kind} 低电上部留空");
            Check.Equal(System.Drawing.Color.Red.ToArgb(), low.GetPixel(25, 26).ToArgb(), $"{kind} 低电下部红色填充");
            Check.True(!IsGreen(empty.GetPixel(25, 26)), $"{kind} 零电量留空");
            Check.True(unknownIcon.Handle != IntPtr.Zero, $"{kind} 未知电量图标");
        }

        // 在 20% 与 60% 的相邻边界核对两种效果，避免出现颜色阶段偏移。
        foreach (var (percent, color) in new (byte Percent, System.Drawing.Color Color)[]
        {
            (0, System.Drawing.Color.Red), (20, System.Drawing.Color.Red),
            (21, System.Drawing.Color.Gold), (60, System.Drawing.Color.Gold),
            (61, System.Drawing.Color.LimeGreen), (100, System.Drawing.Color.LimeGreen)
        })
        {
            using var defaultIcon = BatteryIcon.Create(percent);
            using var personalIcon = BatteryIcon.Create(percent, IconEffect.Personalized, DeviceIconKind.Xbox);
            using var defaultBitmap = defaultIcon.ToBitmap();
            using var personalBitmap = personalIcon.ToBitmap();
            Check.Equal(color.ToArgb(), defaultBitmap.GetPixel(6, 12).ToArgb(), $"默认 {percent}% 阶段颜色");
            Check.Equal(color.ToArgb(), personalBitmap.GetPixel(25, 2).ToArgb(), $"个性化 {percent}% 外框颜色");
            if (percent > 0) Check.Equal(color.ToArgb(), personalBitmap.GetPixel(25, 26).ToArgb(), $"个性化 {percent}% 填充颜色");
        }
        // 自定义阶段只影响颜色，两种效果在新的相邻边界必须一致。
        foreach (var (percent, color) in new (byte?, System.Drawing.Color)[]
        {
            (0, System.Drawing.Color.Red), (30, System.Drawing.Color.Red),
            (31, System.Drawing.Color.Gold), (70, System.Drawing.Color.Gold),
            (71, System.Drawing.Color.LimeGreen), (100, System.Drawing.Color.LimeGreen),
            (null, System.Drawing.Color.Gray)
        })
        {
            foreach (var effect in Enum.GetValues<IconEffect>())
            {
                using var icon = BatteryIcon.Create(percent, effect, redThreshold: 30, yellowThreshold: 70);
                using var pixels = icon.ToBitmap();
                if (effect == IconEffect.Personalized)
                    Check.Equal(color.ToArgb(), pixels.GetPixel(25, 2).ToArgb(), "个性化使用自定义颜色边界");
                else if (percent.HasValue)
                    Check.Equal(color.ToArgb(), pixels.GetPixel(6, 12).ToArgb(), "默认电池使用自定义颜色边界");
            }
        }
        using var personalUnknown = BatteryIcon.Create(null, IconEffect.Personalized);
        using var unknownBitmap = personalUnknown.ToBitmap();
        Check.Equal(System.Drawing.Color.Gray.ToArgb(), unknownBitmap.GetPixel(25, 2).ToArgb(), "未知电量不标示为高电量绿色");
        Check.Equal(System.Drawing.Color.Gray.ToArgb(), unknownBitmap.GetPixel(21, 15).ToArgb(), "约 33% 宽度电池的左边框");
        using var ps4Icon = BatteryIcon.Create(80, IconEffect.Personalized, DeviceIconKind.DualShock4);
        using var ps5Icon = BatteryIcon.Create(80, IconEffect.Personalized, DeviceIconKind.DualSense);
        using var ps4 = ps4Icon.ToBitmap();
        using var ps5 = ps5Icon.ToBitmap();
        Check.True(Enumerable.Range(0, 32 * 32).Any(index =>
            ps4.GetPixel(index % 32, index / 32) != ps5.GetPixel(index % 32, index / 32)), "PS4、PS5 具有独立图形");
    }

    private static System.Drawing.Bitmap RenderAtDpi(int dpi, bool showBattery)
    {
        var bitmap = new System.Drawing.Bitmap(32, 32);
        bitmap.SetResolution(dpi, dpi);
        using var graphics = System.Drawing.Graphics.FromImage(bitmap);
        BatteryIcon.DrawPersonalized(graphics, 75, DeviceIconKind.DualShock4, showBattery);
        return bitmap;
    }

    // 允许抗锯齿边缘存在少量颜色混合。
    private static bool IsGreen(System.Drawing.Color pixel) => pixel.G > 100 && pixel.G > pixel.R * 2 && pixel.G > pixel.B * 1.5;
}
