using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace BluetoothBatteryMonitor;

internal static class BatteryIcon
{
    public static Icon Create(byte? percent)
    {
        using var bitmap = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        graphics.SmoothingMode = SmoothingMode.None;

        using var darkPen = new Pen(Color.FromArgb(230, 24, 24, 24), 3);
        using var lightPen = new Pen(Color.White, 1);
        graphics.DrawRectangle(darkPen, 2, 6, 26, 20);
        graphics.DrawRectangle(lightPen, 3, 7, 24, 18);
        graphics.FillRectangle(Brushes.White, 29, 12, 3, 8);
        graphics.DrawRectangle(Pens.Black, 29, 12, 2, 7);

        if (percent is byte value)
        {
            var brush = value <= 20 ? Brushes.Red : value <= 60 ? Brushes.Gold : Brushes.LimeGreen;
            for (var segment = 0; segment < DisplayLogic.BatterySegments(value); segment++)
                graphics.FillRectangle(brush, 5 + segment * 4, 10, 3, 12);
        }
        else
        {
            using var font = new Font("Segoe UI", 15, FontStyle.Bold, GraphicsUnit.Pixel);
            graphics.DrawString("?", font, Brushes.Gray, 10, 8);
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

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);
}
