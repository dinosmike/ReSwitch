using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using ReSwitch.Models;

namespace ReSwitch.Services;

/// <summary>Создание иконки приложения (высокое разрешение для трея и окна).</summary>
public static class AppIconFactory
{
    private const int Large = 256;
    private const int Small = 48;

    public static Icon CreateApplicationIcon() => CreateApplicationIcon(UiTheme.Dark);

    public static Icon CreateApplicationIcon(UiTheme theme)
    {
        using var bmp = RenderBitmap(Large, theme);
        var hIcon = bmp.GetHicon();
        try
        {
            return (Icon)Icon.FromHandle(hIcon).Clone();
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    public static Icon CreateTrayIcon() => CreateTrayIcon(UiTheme.Dark);

    public static Icon CreateTrayIcon(UiTheme theme)
    {
        using var bmp = RenderBitmap(Small, theme);
        var hIcon = bmp.GetHicon();
        try
        {
            return (Icon)Icon.FromHandle(hIcon).Clone();
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    /// <summary>Сохранить ту же графику, что и у трея/окна, в .ico для встраивания в exe (высокое разрешение).</summary>
    public static void SaveApplicationIconFile(string path)
    {
        using var bmp = RenderBitmap(Large, UiTheme.Dark);
        var hIcon = bmp.GetHicon();
        try
        {
            using var icon = (Icon)Icon.FromHandle(hIcon).Clone();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            using var fs = File.Create(path);
            icon.Save(fs);
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    private static Bitmap RenderBitmap(int size, UiTheme theme)
    {
        GetThemeColors(theme, out var clear, out var gradTop, out var gradBot, out var borderArgb, out var textArgb);

        var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.Clear(clear);

        using var bg = new LinearGradientBrush(
            new Rectangle(0, 0, size, size),
            gradTop,
            gradBot,
            LinearGradientMode.Vertical);
        g.FillRectangle(bg, 0, 0, size, size);

        var pad = size / 10;
        using var border = new Pen(borderArgb, Math.Max(1, size / 64f));
        DrawRoundedRectangle(g, border, pad, pad, size - pad * 2, size - pad * 2, size / 8f);

        using var font = new Font("Segoe UI Semibold", size * 0.38f, FontStyle.Bold, GraphicsUnit.Pixel);
        using var textBrush = new SolidBrush(textArgb);
        const string text = "R";
        var sz = g.MeasureString(text, font);
        g.DrawString(text, font, textBrush, (size - sz.Width) / 2, (size - sz.Height) / 2 - size * 0.02f);

        return bmp;
    }

    /// <summary>Цвета согласованы с темами UI (фон, акцент).</summary>
    private static void GetThemeColors(
        UiTheme theme,
        out Color clear,
        out Color gradTop,
        out Color gradBot,
        out Color border,
        out Color text)
    {
        switch (theme)
        {
            case UiTheme.Light:
                clear = Color.FromArgb(255, 244, 245, 247);
                gradTop = Color.FromArgb(255, 255, 255, 255);
                gradBot = Color.FromArgb(255, 228, 236, 234);
                border = Color.FromArgb(190, 13, 148, 136);
                text = Color.FromArgb(255, 13, 148, 136);
                break;
            case UiTheme.Fuchsia:
                clear = Color.FromArgb(255, 20, 10, 20);
                gradTop = Color.FromArgb(255, 37, 16, 42);
                gradBot = Color.FromArgb(255, 20, 10, 20);
                border = Color.FromArgb(200, 232, 121, 249);
                text = Color.FromArgb(255, 232, 121, 249);
                break;
            case UiTheme.Aquamarine:
                clear = Color.FromArgb(255, 11, 20, 40);
                gradTop = Color.FromArgb(255, 18, 35, 65);
                gradBot = Color.FromArgb(255, 8, 14, 32);
                border = Color.FromArgb(200, 94, 179, 255);
                text = Color.FromArgb(255, 147, 197, 255);
                break;
            default:
                clear = Color.FromArgb(255, 13, 17, 23);
                gradTop = Color.FromArgb(255, 22, 32, 48);
                gradBot = Color.FromArgb(255, 10, 14, 22);
                border = Color.FromArgb(180, 61, 212, 195);
                text = Color.FromArgb(255, 61, 212, 195);
                break;
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);

    private static void DrawRoundedRectangle(Graphics g, Pen pen, float x, float y, float w, float h, float r)
    {
        using var path = new GraphicsPath();
        var d = r * 2;
        path.AddArc(x, y, d, d, 180, 90);
        path.AddArc(x + w - d, y, d, d, 270, 90);
        path.AddArc(x + w - d, y + h - d, d, d, 0, 90);
        path.AddArc(x, y + h - d, d, d, 90, 90);
        path.CloseFigure();
        g.DrawPath(pen, path);
    }
}
