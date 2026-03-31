using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using ReSwitch.Models;

namespace ReSwitch.Services;

public static class SettingsStorage
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        // Иначе кириллица в JSON уходит в \uXXXX — в файле должны быть обычные буквы (UTF-8).
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private const string SettingsFileName = "Re_settings.json";
    private const string LegacySettingsFileName = "settings.json";
    private const string SettingsFileHeaderLine = "Это файл настроек программы ReSwitch";

    /// <summary>Папка с exe — для Re_settings.json. При single-file publish BaseDirectory указывает на temp, поэтому берём каталог процесса (кроме dotnet run).</summary>
    public static string SettingsDirectory => GetProgramDirectory();

    public static string SettingsPath => Path.Combine(SettingsDirectory, SettingsFileName);

    private static string GetProgramDirectory()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(processPath))
        {
            var fileName = Path.GetFileName(processPath);
            if (fileName.Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase))
                return Path.GetFullPath(AppContext.BaseDirectory);

            var dir = Path.GetDirectoryName(processPath);
            if (!string.IsNullOrEmpty(dir))
                return Path.GetFullPath(dir);
        }

        return Path.GetFullPath(AppContext.BaseDirectory);
    }

    /// <summary>Загрузка из Re_settings.json: при отсутствии или повреждении файла — значения по умолчанию; при необходимости файл перезаписывается.</summary>
    /// <remarks>
    /// Раньше при отсутствии файла рядом с exe копировали %AppData%\ReSwitch\settings.json — из-за этого «первый» запуск
    /// подтягивал старые разрешения (например 3440×1440) вместо <see cref="AppSettings.CreateDefault"/>.
    /// Перенос из AppData при необходимости делайте вручную.
    /// </remarks>
    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                var legacyPath = Path.Combine(SettingsDirectory, LegacySettingsFileName);
                if (File.Exists(legacyPath))
                {
                    var rawLegacy = File.ReadAllText(legacyPath);
                    var jsonLegacy = ExtractJsonFromSettingsFile(rawLegacy);
                    var fromLegacy = JsonSerializer.Deserialize<AppSettings>(jsonLegacy, JsonOptions);
                    if (fromLegacy?.Profiles is { Count: >= 2 })
                    {
                        Normalize(fromLegacy);
                        MigrateAutostartFromRegistryIfNeeded(fromLegacy);
                        Save(fromLegacy);
                        return fromLegacy;
                    }

                    var mergedLegacy = MergeWithDefaults(fromLegacy);
                    Save(mergedLegacy);
                    return mergedLegacy;
                }

                var fresh = AppSettings.CreateDefault();
                Save(fresh);
                return fresh;
            }

            var raw = File.ReadAllText(SettingsPath);
            var json = ExtractJsonFromSettingsFile(raw);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            if (loaded?.Profiles is not { Count: >= 2 })
            {
                var merged = MergeWithDefaults(loaded);
                Save(merged);
                return merged;
            }

            Normalize(loaded);
            MigrateAutostartFromRegistryIfNeeded(loaded);
            return loaded;
        }
        catch
        {
            return AppSettings.CreateDefault();
        }
    }

    /// <summary>Раньше автозагрузка могла быть только в реестре; один раз переносим в Re_settings.json.</summary>
    private static void MigrateAutostartFromRegistryIfNeeded(AppSettings settings)
    {
        if (settings.AutostartEnabled)
            return;
        if (!AutostartService.IsEnabled())
            return;

        settings.AutostartEnabled = true;
        Save(settings);
    }

    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(SettingsDirectory);
        Normalize(settings);
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        var content = SettingsFileHeaderLine + Environment.NewLine + json;
        File.WriteAllText(SettingsPath, content);
    }

    /// <summary>Первая строка файла — человекочитаемая подпись; далее JSON. Старые файлы без строки — целиком JSON.</summary>
    private static string ExtractJsonFromSettingsFile(string raw)
    {
        var trimmed = raw.TrimStart();
        if (trimmed.StartsWith('{'))
            return trimmed;

        var lines = raw.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        if (lines.Length < 2)
            return trimmed;

        return string.Join(Environment.NewLine, lines.Skip(1)).TrimStart();
    }

    private static AppSettings MergeWithDefaults(AppSettings? partial)
    {
        var d = AppSettings.CreateDefault();
        if (partial == null)
            return d;

        if (partial.Profiles is { Count: >= 2 })
        {
            Normalize(partial);
            return partial;
        }

        partial.Profiles = d.Profiles;
        Normalize(partial);
        return partial;
    }

    private static void Normalize(AppSettings s)
    {
        for (var i = 0; i < s.Profiles.Count; i++)
        {
            var p = s.Profiles[i];
            if (string.IsNullOrWhiteSpace(p.Name))
                p.Name = $"Profile {i + 1}";
            if (p.Width < 320) p.Width = 640;
            if (p.Height < 240) p.Height = 480;
            if (p.BitsPerPixel is not (16 or 24 or 32))
                p.BitsPerPixel = 32;
            if (p.RefreshRate < 0) p.RefreshRate = 0;
        }

        if (!Enum.IsDefined(typeof(UiTheme), s.UiTheme))
            s.UiTheme = UiTheme.Dark;

        s.UiLanguage = AppLanguageCatalog.Normalize(s.UiLanguage);
        s.ShowResolutionListInTrayMenu ??= false;
    }
}
