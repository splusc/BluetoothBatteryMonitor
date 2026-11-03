using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using BluetoothBatteryMonitor;

namespace BluetoothBatteryMonitor.Checks;

// 直接导出程序真实绘制的图标，便于核对视觉大小和小尺寸清晰度。
internal static class IconPreview
{
    public static void Save(string path)
    {
        var kinds = new[] { DeviceIconKind.Xbox, DeviceIconKind.DualShock4, DeviceIconKind.DualSense, DeviceIconKind.Gamepad,
            DeviceIconKind.Keyboard, DeviceIconKind.Mouse, DeviceIconKind.Headphones, DeviceIconKind.Unknown };
        using var sheet = new Bitmap(880, 480);
        using var graphics = Graphics.FromImage(sheet);
        using var titleFont = new Font("Microsoft YaHei UI", 12, FontStyle.Regular);
        using var labelFont = new Font("Segoe UI", 9, FontStyle.Regular);
        using var centered = new StringFormat { Alignment = StringAlignment.Center };
        graphics.Clear(Color.FromArgb(32, 32, 32));
        graphics.DrawString("实际程序图标 · 白色设备 + 绿色电池（80%）", titleFont, Brushes.White, 20, 14);
        for (var index = 0; index < kinds.Length; index++)
        {
            var left = index % 4 * 220;
            var top = 52 + index / 4 * 210;
            using var icon = BatteryIcon.Create(80, IconEffect.Personalized, kinds[index]);
            using var bitmap = icon.ToBitmap();
            graphics.DrawString(DeviceIconClassifier.Label(kinds[index]), titleFont, Brushes.White,
                new RectangleF(left, top, 220, 28), centered);
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            graphics.DrawImage(bitmap, left + 62, top + 32, 96, 96);
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            foreach (var (size, offset) in new[] { (16, 38), (24, 96), (32, 154) })
            {
                graphics.DrawImage(bitmap, left + offset, top + 140, size, size);
                graphics.DrawString($"{size} px", labelFont, Brushes.LightGray, left + offset - 4, top + 178);
            }
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        sheet.Save(path, ImageFormat.Png);
        SavePs4(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path))!, "ps4-tray-preview.png"));
        SaveLayers(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path))!, "ps4-dpi-fix.png"));
    }

    private static void SaveLayers(string path)
    {
        using var sheet = new Bitmap(520, 230);
        sheet.SetResolution(96, 96);
        using var graphics = Graphics.FromImage(sheet);
        using var font = new Font("Microsoft YaHei UI", 10);
        graphics.Clear(Color.FromArgb(30, 24, 78));
        for (var index = 0; index < 2; index++)
        {
            var left = index * 260;
            using var icon = BatteryIcon.Create(75, IconEffect.Personalized, DeviceIconKind.DualShock4, index == 1);
            using var bitmap = icon.ToBitmap();
            graphics.DrawString(index == 0 ? "1. 基础手柄（无电池）" : "2. 叠加右侧电池", font, Brushes.White, left + 20, 12);
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;
            graphics.DrawImage(bitmap, new Rectangle(left + 25, 55, 96, 96), 0, 0, 32, 32, GraphicsUnit.Pixel);
            graphics.DrawImage(bitmap, new Rectangle(left + 155, 90, 32, 32), 0, 0, 32, 32, GraphicsUnit.Pixel);
            graphics.DrawString("放大检查", font, Brushes.LightGray, left + 25, 172);
            graphics.DrawString("32 px", font, Brushes.LightGray, left + 150, 172);
        }
        sheet.Save(path, ImageFormat.Png);
    }

    private static void SavePs4(string path)
    {
        using var sheet = new Bitmap(560, 210);
        using var graphics = Graphics.FromImage(sheet);
        using var font = new Font("Microsoft YaHei UI", 10);
        using var centered = new StringFormat { Alignment = StringAlignment.Center };
        graphics.Clear(Color.FromArgb(30, 24, 78));
        var levels = new byte?[] { null, 15, 55, 95 };
        for (var index = 0; index < levels.Length; index++)
        {
            var left = index * 140;
            using var icon = BatteryIcon.Create(levels[index], IconEffect.Personalized, DeviceIconKind.DualShock4);
            using var bitmap = icon.ToBitmap();
            var label = levels[index] is byte value ? $"{value}%" : "电量未知";
            graphics.DrawString(label, font, Brushes.White, new RectangleF(left, 8, 140, 24), centered);
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            graphics.DrawImage(bitmap, left + 38, 40, 64, 64);
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.DrawImage(bitmap, left + 50, 126, 40, 40);
            graphics.DrawString("40 px", font, Brushes.LightGray, new RectangleF(left, 178, 140, 24), centered);
        }
        sheet.Save(path, ImageFormat.Png);
    }
}
