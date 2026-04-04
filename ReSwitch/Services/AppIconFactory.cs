using System.Drawing;
using System.IO;
using ReSwitch.Models;

namespace ReSwitch.Services;

/// <summary>Иконка приложения из встроенного <c>app.ico</c> (тот же файл, что <see cref="ApplicationIcon"/> в csproj).</summary>
public static class AppIconFactory
{
    private const string PackUri = "pack://application:,,,/app.ico";

    public static Icon CreateApplicationIcon() => CreateApplicationIcon(UiTheme.Dark);

    /// <param name="theme">Игнорируется — используется один файл <c>app.ico</c>.</param>
    public static Icon CreateApplicationIcon(UiTheme theme) => LoadEmbeddedIconClone();

    public static Icon CreateTrayIcon() => CreateTrayIcon(UiTheme.Dark);

    /// <param name="theme">Игнорируется.</param>
    public static Icon CreateTrayIcon(UiTheme theme) => LoadEmbeddedIconClone();

    private static Icon LoadEmbeddedIconClone()
    {
        var streamInfo = System.Windows.Application.GetResourceStream(new Uri(PackUri, UriKind.Absolute));
        if (streamInfo?.Stream == null)
            throw new InvalidOperationException("Embedded app.ico not found.");

        using (streamInfo.Stream)
        {
            using var icon = new Icon(streamInfo.Stream);
            return (Icon)icon.Clone();
        }
    }

    /// <summary>Экспорт встроенного <c>app.ico</c> в файл (например для инструментов).</summary>
    public static void SaveApplicationIconFile(string path)
    {
        var streamInfo = System.Windows.Application.GetResourceStream(new Uri(PackUri, UriKind.Absolute));
        if (streamInfo?.Stream == null)
            throw new InvalidOperationException("Embedded app.ico not found.");

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var fs = File.Create(path);
        using (streamInfo.Stream)
        {
            streamInfo.Stream.CopyTo(fs);
        }
    }
}
