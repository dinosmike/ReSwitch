using System.Text.Json.Serialization;
using ReSwitch.Services;

namespace ReSwitch.Models;

/// <summary>Все настройки приложения и профили дисплея (2–5 шт., имена в JSON) хранятся только в Re_settings.json рядом с exe.</summary>
public sealed class AppSettings
{
    [JsonPropertyOrder(0)]
    public List<DisplayProfile> Profiles { get; set; } = new();

    /// <summary>Запрашивать подтверждение после смены режима; при отсутствии — откат.</summary>
    [JsonPropertyOrder(1)]
    public bool ConfirmSwitchEnabled { get; set; } = true;

    /// <summary>Секунды на подтверждение перед откатом.</summary>
    [JsonPropertyOrder(2)]
    public int ConfirmTimeoutSeconds { get; set; } = 15;

    [JsonPropertyOrder(3)]
    public bool AutostartEnabled { get; set; }

    /// <summary>Крестик в заголовке сворачивает в трей вместо полного выхода.</summary>
    [JsonPropertyOrder(4)]
    public bool MinimizeToTrayOnCloseClick { get; set; }

    /// <summary>После запуска сразу свернуть в трей (окно не показывать).</summary>
    [JsonPropertyOrder(5)]
    public bool MinimizeToTrayOnStartup { get; set; }

    /// <summary>Анимация ухода в трей: сдвиг к правому нижнему углу, уменьшение и затухание.</summary>
    [JsonPropertyOrder(6)]
    public bool MinimizeToTrayAnimationEnabled { get; set; } = true;

    /// <summary>Действие по одинарному левому клику по иконке в трее (null — как «Открыть окно»). Двойной клик всегда переключает профили.</summary>
    [JsonPropertyOrder(7)]
    public TrayIconClickAction? TraySingleClickAction { get; set; }

    /// <summary>Цветовая тема интерфейса.</summary>
    [JsonPropertyOrder(8)]
    public UiTheme UiTheme { get; set; } = UiTheme.Dark;

    /// <summary>Код языка интерфейса: ru, en, de, fr, es, kz (см. <see cref="AppLanguageCatalog"/>).</summary>
    [JsonPropertyOrder(9)]
    public string? UiLanguage { get; set; }

    /// <summary>Показывать в контекстном меню трея пункты быстрого выбора профилей (разрешений).</summary>
    [JsonPropertyOrder(10)]
    public bool? ShowResolutionListInTrayMenu { get; set; }

    /// <summary>Подписи профилей в меню трея: имя и разрешение; при выключении — только разрешение (цифры).</summary>
    [JsonPropertyOrder(11)]
    public bool? ShowProfileNamesInTrayMenu { get; set; }

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
            ConfirmSwitchEnabled = true,
            ConfirmTimeoutSeconds = 15,
            AutostartEnabled = false,
            MinimizeToTrayOnCloseClick = false,
            MinimizeToTrayOnStartup = false,
            MinimizeToTrayAnimationEnabled = true,
            TraySingleClickAction = TrayIconClickAction.OpenWindow,
            UiTheme = UiTheme.Dark,
            UiLanguage = AppLanguageCatalog.ResolveDefaultLanguage(),
            ShowResolutionListInTrayMenu = false,
            ShowProfileNamesInTrayMenu = true
        };
    }
}
