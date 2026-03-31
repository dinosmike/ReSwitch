using System.Text.Json.Serialization;

namespace ReSwitch.Models;

/// <summary>Смена режима дисплея и подтверждение.</summary>
public sealed class DisplayModeSettings
{
    public bool ConfirmSwitchEnabled { get; set; } = true;
    public int ConfirmTimeoutSeconds { get; set; } = 15;
}

/// <summary>Автозагрузка и поведение окна/трея.</summary>
public sealed class StartupWindowSettings
{
    public bool AutostartEnabled { get; set; }
    public bool MinimizeToTrayOnCloseClick { get; set; }
    public bool MinimizeToTrayOnStartup { get; set; }
    public bool MinimizeToTrayAnimationEnabled { get; set; } = true;

    /// <summary>Спросить про автозапуск при выходе. true — показать вопрос, false — уже спросили, больше не показывать.</summary>
    [JsonPropertyName("askOnExit")]
    public bool AskOnExit { get; set; } = true;
}

/// <summary>Меню иконки в трее.</summary>
public sealed class TrayMenuSettings
{
    public TrayIconClickAction? SingleClickAction { get; set; }
    public bool? ShowResolutionListInTrayMenu { get; set; }
    public bool? ShowProfileNamesInTrayMenu { get; set; }
}

/// <summary>Тема и язык интерфейса.</summary>
public sealed class UiAppearanceSettings
{
    public UiTheme Theme { get; set; } = UiTheme.Dark;
    public string? Language { get; set; }
}

/// <summary>Блок «совет» при запуске.</summary>
public sealed class AdviceSectionSettings
{
    public bool Enabled { get; set; }

    /// <summary>Спрашивать ли при запуске про включение советов (первый запуск). В JSON: <c>askOnStart</c>.</summary>
    [JsonPropertyName("askOnStart")]
    public bool AskOnStart { get; set; } = true;
}

/// <summary>Служебные поля файла настроек.</summary>
public sealed class SettingsMeta
{
    /// <summary>6 — категория <c>advice</c> без вложенного блока настроек показа; 5 — старый формат с лишним блоком в JSON; 3 — категории; 2 — плоский формат; 0–1 — старые файлы.</summary>
    public int SettingsFormatVersion { get; set; }
}
