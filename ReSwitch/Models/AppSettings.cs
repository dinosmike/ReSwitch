using System.Text.Json.Serialization;
using ReSwitch.Services;

namespace ReSwitch.Models;

/// <summary>
/// Re_settings.json: <c>profiles</c> — профили дисплея; остальное сгруппировано по категориям (<c>displayMode</c>, <c>startupAndWindow</c>, <c>tray</c>, <c>ui</c>, <c>advice</c>, <c>meta</c>).
/// Свойства без [JsonIgnore] ниже — только для кода (делегируют во вложенные объекты и не пишутся в JSON).
/// </summary>
public sealed class AppSettings
{
    [JsonPropertyOrder(0)]
    public List<DisplayProfile> Profiles { get; set; } = new();

    [JsonPropertyOrder(1)]
    [JsonPropertyName("displayMode")]
    public DisplayModeSettings DisplayMode { get; set; } = new();

    [JsonPropertyOrder(2)]
    [JsonPropertyName("startupAndWindow")]
    public StartupWindowSettings StartupAndWindow { get; set; } = new();

    [JsonPropertyOrder(3)]
    [JsonPropertyName("tray")]
    public TrayMenuSettings Tray { get; set; } = new();

    [JsonPropertyOrder(4)]
    [JsonPropertyName("ui")]
    public UiAppearanceSettings Ui { get; set; } = new();

    [JsonPropertyOrder(5)]
    [JsonPropertyName("advice")]
    public AdviceSectionSettings Advice { get; set; } = new();

    [JsonPropertyOrder(6)]
    [JsonPropertyName("meta")]
    public SettingsMeta Meta { get; set; } = new();

    // --- Удобство кода (не сериализуются) ---

    [JsonIgnore]
    public bool ConfirmSwitchEnabled
    {
        get => DisplayMode.ConfirmSwitchEnabled;
        set => DisplayMode.ConfirmSwitchEnabled = value;
    }

    [JsonIgnore]
    public int ConfirmTimeoutSeconds
    {
        get => DisplayMode.ConfirmTimeoutSeconds;
        set => DisplayMode.ConfirmTimeoutSeconds = value;
    }

    [JsonIgnore]
    public bool AutostartEnabled
    {
        get => StartupAndWindow.AutostartEnabled;
        set => StartupAndWindow.AutostartEnabled = value;
    }

    [JsonIgnore]
    public bool MinimizeToTrayOnCloseClick
    {
        get => StartupAndWindow.MinimizeToTrayOnCloseClick;
        set => StartupAndWindow.MinimizeToTrayOnCloseClick = value;
    }

    [JsonIgnore]
    public bool MinimizeToTrayOnStartup
    {
        get => StartupAndWindow.MinimizeToTrayOnStartup;
        set => StartupAndWindow.MinimizeToTrayOnStartup = value;
    }

    [JsonIgnore]
    public bool MinimizeToTrayAnimationEnabled
    {
        get => StartupAndWindow.MinimizeToTrayAnimationEnabled;
        set => StartupAndWindow.MinimizeToTrayAnimationEnabled = value;
    }

    [JsonIgnore]
    public TrayIconClickAction? TraySingleClickAction
    {
        get => Tray.SingleClickAction;
        set => Tray.SingleClickAction = value;
    }

    [JsonIgnore]
    public bool? ShowResolutionListInTrayMenu
    {
        get => Tray.ShowResolutionListInTrayMenu;
        set => Tray.ShowResolutionListInTrayMenu = value;
    }

    [JsonIgnore]
    public bool? ShowProfileNamesInTrayMenu
    {
        get => Tray.ShowProfileNamesInTrayMenu;
        set => Tray.ShowProfileNamesInTrayMenu = value;
    }

    [JsonIgnore]
    public UiTheme UiTheme
    {
        get => Ui.Theme;
        set => Ui.Theme = value;
    }

    [JsonIgnore]
    public string? UiLanguage
    {
        get => Ui.Language;
        set => Ui.Language = value;
    }

    [JsonIgnore]
    public bool AskOnExit
    {
        get => StartupAndWindow.AskOnExit;
        set => StartupAndWindow.AskOnExit = value;
    }

    [JsonIgnore]
    public bool AdviceEnabled
    {
        get => Advice.Enabled;
        set => Advice.Enabled = value;
    }

    [JsonIgnore]
    public bool AdviceAskOnStart
    {
        get => Advice.AskOnStart;
        set => Advice.AskOnStart = value;
    }

    [JsonIgnore]
    public int SettingsFormatVersion
    {
        get => Meta.SettingsFormatVersion;
        set => Meta.SettingsFormatVersion = value;
    }

    public static AppSettings CreateDefault()
    {
        return new AppSettings
        {
            Profiles = new List<DisplayProfile>
            {
                new()
                {
                    Name = "Profile 1",
                    Width = 1920,
                    Height = 1080,
                    RefreshRate = 60,
                    BitsPerPixel = 32
                },
                new()
                {
                    Name = "Profile 2",
                    Width = 1280,
                    Height = 720,
                    RefreshRate = 60,
                    BitsPerPixel = 32
                }
            },
            DisplayMode = new DisplayModeSettings
            {
                ConfirmSwitchEnabled = true,
                ConfirmTimeoutSeconds = 15
            },
            StartupAndWindow = new StartupWindowSettings
            {
                AutostartEnabled = false,
                MinimizeToTrayOnCloseClick = false,
                MinimizeToTrayOnStartup = false,
                MinimizeToTrayAnimationEnabled = true
            },
            Tray = new TrayMenuSettings
            {
                SingleClickAction = TrayIconClickAction.OpenWindow,
                ShowResolutionListInTrayMenu = false,
                ShowProfileNamesInTrayMenu = true
            },
            Ui = new UiAppearanceSettings
            {
                Theme = UiTheme.Dark,
                Language = AppLanguageCatalog.ResolveDefaultLanguage()
            },
            Advice = new AdviceSectionSettings
            {
                Enabled = false,
                AskOnStart = true
            },
            Meta = new SettingsMeta { SettingsFormatVersion = 6 }
        };
    }

    /// <summary>Миграция из плоского JSON (формат до категорий).</summary>
    internal static AppSettings FromFlatLegacy(AppSettingsFlatLegacy src)
    {
        var s = new AppSettings
        {
            Profiles = src.Profiles is { Count: > 0 } ? src.Profiles : CreateDefault().Profiles,
            DisplayMode = new DisplayModeSettings
            {
                ConfirmSwitchEnabled = src.ConfirmSwitchEnabled,
                ConfirmTimeoutSeconds = src.ConfirmTimeoutSeconds > 0 ? src.ConfirmTimeoutSeconds : 15
            },
            StartupAndWindow = new StartupWindowSettings
            {
                AutostartEnabled = src.AutostartEnabled,
                MinimizeToTrayOnCloseClick = src.MinimizeToTrayOnCloseClick,
                MinimizeToTrayOnStartup = src.MinimizeToTrayOnStartup,
                MinimizeToTrayAnimationEnabled = src.MinimizeToTrayAnimationEnabled
            },
            Tray = new TrayMenuSettings
            {
                SingleClickAction = src.TraySingleClickAction,
                ShowResolutionListInTrayMenu = src.ShowResolutionListInTrayMenu,
                ShowProfileNamesInTrayMenu = src.ShowProfileNamesInTrayMenu
            },
            Ui = new UiAppearanceSettings
            {
                Theme = src.UiTheme,
                Language = src.UiLanguage
            },
            Advice = new AdviceSectionSettings
            {
                Enabled = src.AdviceEnabled,
                AskOnStart = !src.AdviceOnboardingComplete
            },
            Meta = new SettingsMeta { SettingsFormatVersion = src.SettingsFormatVersion }
        };
        return s;
    }
}

/// <summary>Плоский формат Re_settings.json до введения категорий (десериализация и миграция).</summary>
internal sealed class AppSettingsFlatLegacy
{
    public List<DisplayProfile> Profiles { get; set; } = new();
    public bool ConfirmSwitchEnabled { get; set; } = true;
    public int ConfirmTimeoutSeconds { get; set; } = 15;
    public bool AutostartEnabled { get; set; }
    public bool MinimizeToTrayOnCloseClick { get; set; }
    public bool MinimizeToTrayOnStartup { get; set; }
    public bool MinimizeToTrayAnimationEnabled { get; set; } = true;
    public TrayIconClickAction? TraySingleClickAction { get; set; }
    public UiTheme UiTheme { get; set; } = UiTheme.Dark;
    public string? UiLanguage { get; set; }
    public bool? ShowResolutionListInTrayMenu { get; set; }
    public bool? ShowProfileNamesInTrayMenu { get; set; }
    public bool AdviceEnabled { get; set; }
    public bool AdviceOnboardingComplete { get; set; }
    public int SettingsFormatVersion { get; set; }
}
