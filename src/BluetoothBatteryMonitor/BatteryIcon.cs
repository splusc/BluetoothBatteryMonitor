using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace BluetoothBatteryMonitor;

internal static class BatteryIcon
{
    // 默认参数保证原有调用继续绘制当前横向电池。
    public static Icon Create(byte? percent, IconEffect effect = IconEffect.Default, DeviceIconKind kind = DeviceIconKind.Unknown)
    {
        using var bitmap = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        graphics.SmoothingMode = SmoothingMode.None;

        if (effect == IconEffect.Personalized)
        {
            DrawPersonalized(graphics, percent, kind);
        }
        else
        {
            using var darkPen = new Pen(Color.FromArgb(230, 24, 24, 24), 3);
            using var lightPen = new Pen(Color.White, 1);
            graphics.DrawRectangle(darkPen, 2, 6, 26, 20);
            graphics.DrawRectangle(lightPen, 3, 7, 24, 18);
            graphics.FillRectangle(Brushes.White, 29, 12, 3, 8);
            graphics.DrawRectangle(Pens.Black, 29, 12, 2, 7);

            if (percent is byte value)
            {
                var brush = BatteryBrush(value);
                for (var segment = 0; segment < DisplayLogic.BatterySegments(value); segment++)
                    graphics.FillRectangle(brush, 5 + segment * 4, 10, 3, 12);
            }
            else
            {
                using var font = new Font("Segoe UI", 15, FontStyle.Bold, GraphicsUnit.Pixel);
                graphics.DrawString("?", font, Brushes.Gray, 10, 8);
            }
        }

        var handle = bitmap.GetHicon();
        try
        {
            return (Icon)Icon.FromHandle(handle).Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    // 使用预绘制的透明剪影，竖向电池覆盖图形右侧，匹配预览中的组合比例。
    private static void DrawPersonalized(Graphics graphics, byte? percent, DeviceIconKind kind)
    {
        if (!Enum.IsDefined(kind)) kind = DeviceIconKind.Unknown;
        using var stream = typeof(BatteryIcon).Assembly.GetManifestResourceStream($"BluetoothBatteryMonitor.Assets.DeviceIcons.{kind}.png")
            ?? throw new InvalidOperationException($"缺少设备图标资源：{kind}");
        using var image = Image.FromStream(stream);
        // 32×32 资源已包含确认过的位置与尺寸，解码缓冲仅 4KB，无需运行时缩放。
        graphics.DrawImageUnscaled(image, 0, 0);

        // 不让设备图形透进电池空槽；外框与填充使用和默认样式相同的阶段颜色。
        graphics.FillRectangle(Brushes.Black, 20, 3, 12, 28);
        var brush = BatteryBrush(percent);
        using var border = new Pen(brush, 1.6f);
        graphics.DrawRectangle(border, 21, 4, 10, 26);
        graphics.FillRectangle(brush, 24, 1, 5, 3);
        if (percent is byte value)
        {
            var height = Math.Min((int)value, 100) * 21f / 100;
            if (height > 0) graphics.FillRectangle(brush, 23, 28 - height, 6, height);
        }
        else
        {
            using var font = new Font("Segoe UI", 12, FontStyle.Bold, GraphicsUnit.Pixel);
            graphics.DrawString("?", font, brush, 21, 12);
        }
    }

    // 两种效果共用阈值；未知电量以灰色表示，不能冒充高电量。
    private static Brush BatteryBrush(byte? percent) => percent switch
    {
        null => Brushes.Gray,
        <= 20 => Brushes.Red,
        <= 60 => Brushes.Gold,
        _ => Brushes.LimeGreen
    };

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);
}
