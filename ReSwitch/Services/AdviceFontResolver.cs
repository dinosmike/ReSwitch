using System.Linq;
using WpfFontFamily = System.Windows.Media.FontFamily;
using WpfFonts = System.Windows.Media.Fonts;

namespace ReSwitch.Services;

/// <summary>
/// Резолвер шрифта окна совета: встроенный Helvetica LT Pro UltraCompressed, при необходимости — Roboto Condensed (старые настройки), иначе системные шрифты.
/// </summary>
public static class AdviceFontResolver
{
    private const string EmbeddedHelveticaUri = "./Fonts/#Helvetica LT Pro UltraCompressed";
    private const string EmbeddedHelveticaFamilyName = "Helvetica LT Pro UltraCompressed";

    private const string EmbeddedRobotoUri = "./Fonts/#Roboto Condensed";
    private const string EmbeddedRobotoFamilyName = "Roboto Condensed";

    private static readonly Uri PackRoot = new("pack://application:,,,/");

    public static WpfFontFamily Resolve(string? preferredFromSettings)
    {
        var preferred = string.IsNullOrWhiteSpace(preferredFromSettings)
            ? EmbeddedHelveticaFamilyName
            : preferredFromSettings.Trim();

        if (IsEmbeddedHelvetica(preferred))
            return new WpfFontFamily(PackRoot, EmbeddedHelveticaUri);

        if (IsRobotoCondensed(preferred))
            return new WpfFontFamily(PackRoot, EmbeddedRobotoUri);

        foreach (var family in WpfFonts.SystemFontFamilies)
        {
            if (string.Equals(family.Source, preferred, StringComparison.OrdinalIgnoreCase))
                return family;
            if (family.FamilyNames.Values.Any(v => string.Equals(v, preferred, StringComparison.OrdinalIgnoreCase)))
                return family;
        }

        try
        {
            return new WpfFontFamily(preferred);
        }
        catch
        {
            return new WpfFontFamily(PackRoot, EmbeddedHelveticaUri);
        }
    }

    private static bool IsEmbeddedHelvetica(string name)
    {
        var n = name.Replace(" ", "", StringComparison.Ordinal).ToLowerInvariant();
        // Back-compat: old settings value ("Helvetica Inserat LT Std") should still resolve to embedded Helvetica.
        return n.Contains("helveticainserat") || n.Contains("helveticaltproultracompressed");
    }

    private static bool IsRobotoCondensed(string name)
    {
        var n = name.Replace(" ", "", StringComparison.Ordinal).ToLowerInvariant();
        return n.Contains("robotocondensed");
    }
}
