using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ReSwitch.Models;

namespace ReSwitch.Services;

public static class SettingsStorage
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    private static readonly JsonDocumentOptions LenientDocOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    };

    private static readonly JsonNodeOptions LenientNodeOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private const string SettingsFileName = "Re_settings.json";
    private const string LegacySettingsFileName = "settings.json";
    private const string SettingsFileHeaderLine = "Это файл настроек программы ReSwitch";

    /// <summary>%LocalAppData%\ReSwitch — каталог настроек пользователя (Windows).</summary>
    public static string SettingsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ReSwitch");

    public static string SettingsPath => Path.Combine(SettingsDirectory, SettingsFileName);

    /// <summary>Каталог с exe — только для миграции старых файлов из папки программы.</summary>
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

    /// <summary>Одноразовый перенос Re_settings.json / settings.json из папки с exe в %LocalAppData%\ReSwitch.</summary>
    private static void TryMigrateFromProgramDirectory()
    {
        if (File.Exists(SettingsPath))
            return;

        var oldDir = GetProgramDirectory();
        var newDir = Path.GetFullPath(SettingsDirectory);
        if (string.Equals(Path.GetFullPath(oldDir), newDir, StringComparison.OrdinalIgnoreCase))
            return;

        var oldMain = Path.Combine(oldDir, SettingsFileName);
        var oldLegacy = Path.Combine(oldDir, LegacySettingsFileName);

        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            if (File.Exists(oldMain))
            {
                File.Copy(oldMain, SettingsPath, overwrite: false);
                return;
            }

            if (!File.Exists(oldLegacy))
                return;

            var rawLegacy = File.ReadAllText(oldLegacy);
            var jsonLegacy = ExtractJsonFromSettingsFile(rawLegacy);
            var fromLegacy = DeserializeSettingsOrMigrate(jsonLegacy, out _);
            if (fromLegacy?.Profiles is { Count: >= 2 })
            {
                Normalize(fromLegacy);
                MigrateAutostartFromRegistryIfNeeded(fromLegacy);
                Save(fromLegacy);
                return;
            }

            var mergedLegacy = MergeWithDefaults(fromLegacy);
            Save(mergedLegacy);
        }
        catch
        {
            // при ошибке миграции сработает обычная логика Load (дефолты/создание файла)
        }
    }

    /// <summary>Загрузка из Re_settings.json: при отсутствии или повреждении файла — значения по умолчанию; при необходимости дописываются только недостающие ключи (существующий JSON не пересобирается целиком).</summary>
    /// <remarks>
    /// Раньше настройки лежали рядом с exe; при первом запуске с новым путём файл переносится в %LocalAppData%\ReSwitch.
    /// </remarks>
    public static AppSettings Load()
    {
        try
        {
            TryMigrateFromProgramDirectory();

            if (!File.Exists(SettingsPath))
            {
                var legacyPath = Path.Combine(SettingsDirectory, LegacySettingsFileName);
                if (File.Exists(legacyPath))
                {
                    var rawLegacy = File.ReadAllText(legacyPath);
                    var jsonLegacy = ExtractJsonFromSettingsFile(rawLegacy);
                    var fromLegacy = DeserializeSettingsOrMigrate(jsonLegacy, out _);
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
            AppSettings loaded;
            bool wasFlatLegacy;
            try
            {
                loaded = DeserializeSettingsOrMigrate(json, out wasFlatLegacy);
            }
            catch
            {
                var sanitized = StripTrailingCommas(json);
                try
                {
                    loaded = DeserializeSettingsOrMigrate(sanitized, out wasFlatLegacy);
                }
                catch
                {
                    BackupCorruptedFile();
                    loaded = AppSettings.CreateDefault();
                    wasFlatLegacy = false;
                }
            }

            if (loaded == null)
                loaded = AppSettings.CreateDefault();

            if (loaded.Profiles is not { Count: >= 2 })
            {
                if (!TryRestoreProfilesFromRawJson(json, loaded))
                {
                    var merged = MergeWithDefaults(loaded);
                    Save(merged);
                    return merged;
                }
            }

            var profileCountBefore = loaded.Profiles.Count;
            var formatVersionBefore = loaded.SettingsFormatVersion;
            Normalize(loaded);
            MigrateAutostartFromRegistryIfNeeded(loaded);
            if (profileCountBefore > 5 || wasFlatLegacy || formatVersionBefore < 6)
                Save(loaded);
            return loaded;
        }
        catch
        {
            BackupCorruptedFile();
            return AppSettings.CreateDefault();
        }
    }

    private static void BackupCorruptedFile()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return;
            var backupPath = SettingsPath + ".bak";
            File.Copy(SettingsPath, backupPath, overwrite: true);
        }
        catch { /* best effort */ }
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

        JsonObject root;
        try
        {
            if (File.Exists(SettingsPath))
            {
                var rawExisting = File.ReadAllText(SettingsPath);
                var json = ExtractJsonFromSettingsFile(rawExisting);
                root = ParseJsonLenient(json);
            }
            else
            {
                root = new JsonObject();
            }
        }
        catch
        {
            root = new JsonObject();
        }

        var modelNode = JsonSerializer.SerializeToNode(settings, JsonOptions);
        if (modelNode is JsonObject modelObj)
        {
            MergeModelIntoExistingJson(root, modelObj);
            RemoveLegacyAdviceKeys(root);
        }
        else
        {
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(SettingsPath, SettingsFileHeaderLine + Environment.NewLine + json);
            return;
        }

        var outOpts = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        var outJson = root.ToJsonString(outOpts);
        File.WriteAllText(SettingsPath, SettingsFileHeaderLine + Environment.NewLine + outJson);
    }

    /// <summary>
    /// Вливает в <paramref name="root"/> значения из <paramref name="model"/> (актуальная модель приложения):
    /// для объектов — рекурсивное слияние с сохранением ключей, которых нет в модели;
    /// массивы и примитивы — как в модели (профили, скаляры).
    /// </summary>
    private static void MergeModelIntoExistingJson(JsonObject root, JsonObject model)
    {
        foreach (var kvp in model)
        {
            if (kvp.Key == "profiles" && kvp.Value is JsonArray ma && ma.Count == 0
                && root.TryGetPropertyValue("profiles", out var fp) && fp is JsonArray fa && fa.Count >= 2)
                continue;

            if (kvp.Value is JsonObject modelChild)
            {
                if (root.TryGetPropertyValue(kvp.Key, out var existing) && existing is JsonObject existingObj)
                    MergeModelIntoExistingJson(existingObj, modelChild);
                else
                    root[kvp.Key] = CloneNode(kvp.Value);
            }
            else
                root[kvp.Key] = CloneNode(kvp.Value);
        }
    }

    private static JsonNode? CloneNode(JsonNode? node)
    {
        if (node is null)
            return null;
        return JsonNode.Parse(node.ToJsonString())!;
    }

    private static void RemoveLegacyAdviceKeys(JsonObject root)
    {
        if (!root.TryGetPropertyValue("advice", out var adviceNode) || adviceNode is not JsonObject advice)
            return;
        advice.Remove("onboardingComplete");
    }

    /// <summary>Парсит JSON с максимальной терпимостью: trailing commas, комментарии, удаление висящих запятых перед } и ].</summary>
    private static JsonObject ParseJsonLenient(string json)
    {
        try
        {
            var parsed = JsonNode.Parse(json, LenientNodeOptions, LenientDocOptions);
            return parsed as JsonObject ?? new JsonObject();
        }
        catch
        {
            var sanitized = StripTrailingCommas(json);
            try
            {
                var parsed = JsonNode.Parse(sanitized, LenientNodeOptions, LenientDocOptions);
                return parsed as JsonObject ?? new JsonObject();
            }
            catch
            {
                return new JsonObject();
            }
        }
    }

    /// <summary>Удаляет запятые перед <c>}</c> и <c>]</c> (частая ошибка при ручном редактировании JSON).</summary>
    private static string StripTrailingCommas(string json) =>
        Regex.Replace(json, @",\s*([}\]])", "$1");

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

    /// <summary>
    /// Старый ключ <c>onboardingComplete</c> (true = уже ответили, не спрашивать) → <c>askOnStart</c> (true = спрашивать при старте).
    /// </summary>
    private static void MigrateAdviceAskOnStartFromJson(string json, AppSettings s)
    {
        try
        {
            using var doc = JsonDocument.Parse(json, LenientDocOptions);
            if (!doc.RootElement.TryGetProperty("advice", out var advice))
                return;

            if (advice.TryGetProperty("askOnStart", out var ask))
            {
                s.Advice.AskOnStart = ask.GetBoolean();
                return;
            }

            if (advice.TryGetProperty("onboardingComplete", out var ob))
                s.Advice.AskOnStart = !ob.GetBoolean();
            else
                s.Advice.AskOnStart = true;
        }
        catch
        {
            // оставляем значение после JsonSerializer
        }
    }

    /// <summary>Плоский JSON (до категорий) или категоризированный с ключом <c>displayMode</c>.</summary>
    private static AppSettings DeserializeSettingsOrMigrate(string json, out bool wasFlatLegacy)
    {
        using var doc = JsonDocument.Parse(json, LenientDocOptions);
        var root = doc.RootElement;
        if (root.TryGetProperty("displayMode", out _))
        {
            wasFlatLegacy = false;
            var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? AppSettings.CreateDefault();
            MigrateAdviceAskOnStartFromJson(json, loaded);
            return loaded;
        }

        wasFlatLegacy = true;
        var flat = JsonSerializer.Deserialize<AppSettingsFlatLegacy>(json, JsonOptions);
        return flat != null ? AppSettings.FromFlatLegacy(flat) : AppSettings.CreateDefault();
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

        if (partial.Profiles is { Count: 1 })
        {
            partial.Profiles.Add(d.Profiles[1].Clone());
            Normalize(partial);
            return partial;
        }

        partial.Profiles = new List<DisplayProfile> { d.Profiles[0].Clone(), d.Profiles[1].Clone() };
        Normalize(partial);
        return partial;
    }

    /// <summary>
    /// Если основной десериализатор не поднял профили (пустой список / один элемент), повторно читаем массив <c>profiles</c> из текста файла.
    /// </summary>
    private static bool TryRestoreProfilesFromRawJson(string json, AppSettings s)
    {
        try
        {
            using var doc = JsonDocument.Parse(json, LenientDocOptions);
            if (!doc.RootElement.TryGetProperty("profiles", out var arr) || arr.ValueKind != JsonValueKind.Array)
                return false;

            var list = new List<DisplayProfile>();
            foreach (var el in arr.EnumerateArray())
            {
                var dp = JsonSerializer.Deserialize<DisplayProfile>(el.GetRawText(), JsonOptions);
                if (dp != null)
                    list.Add(dp);
            }

            if (list.Count < 2)
                return false;

            s.Profiles = list;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void Normalize(AppSettings s)
    {
        s.DisplayMode ??= new DisplayModeSettings();
        s.StartupAndWindow ??= new StartupWindowSettings();
        s.Tray ??= new TrayMenuSettings();
        s.Ui ??= new UiAppearanceSettings();
        s.Advice ??= new AdviceSectionSettings();
        s.Meta ??= new SettingsMeta();

        while (s.Profiles.Count > 5)
            s.Profiles.RemoveAt(s.Profiles.Count - 1);

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
        s.ShowProfileNamesInTrayMenu ??= true;

        if (s.SettingsFormatVersion < 2)
        {
            s.AdviceAskOnStart = false;
            s.AdviceEnabled = false;
            s.SettingsFormatVersion = 2;
        }

        if (s.SettingsFormatVersion < 3)
            s.SettingsFormatVersion = 3;

        if (s.SettingsFormatVersion < 6)
            s.SettingsFormatVersion = 6;
    }
}
