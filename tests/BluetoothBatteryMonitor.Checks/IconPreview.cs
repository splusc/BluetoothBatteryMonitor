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
    }
}
