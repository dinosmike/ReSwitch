using System.Windows;
using ReSwitch.Models;

namespace ReSwitch.Services;

public static class ThemeService
{
    public static void Apply(UiTheme theme)
    {
        var uri = GetThemeUri(theme);
        var rd = new ResourceDictionary { Source = uri };
        var merged = System.Windows.Application.Current.Resources.MergedDictionaries;
        if (merged.Count > 0)
            merged[0] = rd;
        else
            merged.Add(rd);

        if (System.Windows.Application.Current is global::ReSwitch.App app)
            app.RefreshIconsForTheme(theme);
    }

    private static Uri GetThemeUri(UiTheme theme)
    {
        var path = theme switch
        {
            UiTheme.Light => "Themes/Light.xaml",
            UiTheme.Fuchsia => "Themes/Fuchsia.xaml",
            UiTheme.Aquamarine => "Themes/Aquamarine.xaml",
            _ => "Themes/Dark.xaml"
        };
        return new Uri($"pack://application:,,,/{path}", UriKind.Absolute);
    }
}
