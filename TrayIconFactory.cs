using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace MicTrayMute;

internal static class TrayIconFactory
{
    public static Icon Create(bool muted)
    {
        using var bitmap = new Bitmap(32, 32);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        var fill = muted ? Color.FromArgb(85, 95, 108) : Color.FromArgb(220, 32, 32);
        using var brush = new SolidBrush(fill);
        using var darkBrush = new SolidBrush(Color.FromArgb(38, 43, 50));
        using var whitePen = new Pen(Color.White, 3);

        graphics.FillRoundedRectangle(brush, new Rectangle(10, 3, 12, 19), 6);
        graphics.DrawLine(whitePen, 16, 22, 16, 27);
        graphics.DrawLine(whitePen, 10, 27, 22, 27);
        graphics.FillEllipse(darkBrush, 13, 7, 6, 10);

        if (muted)
        {
            using var mutePen = new Pen(Color.FromArgb(255, 230, 80, 80), 4);
            graphics.DrawLine(mutePen, 7, 7, 25, 25);
        }

        var handle = bitmap.GetHicon();
        try
        {
            using var icon = Icon.FromHandle(handle);
            return (Icon)icon.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    private static void FillRoundedRectangle(this Graphics graphics, Brush brush, Rectangle bounds, int radius)
    {
        using var path = new GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        graphics.FillPath(brush, path);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
