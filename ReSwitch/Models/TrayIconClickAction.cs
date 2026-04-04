namespace ReSwitch.Models;

/// <summary>Действие по клику по иконке в области уведомлений.</summary>
public enum TrayIconClickAction
{
    /// <summary>Ничего не делать.</summary>
    None = 0,

    /// <summary>Показать главное окно.</summary>
    OpenWindow = 1,

    /// <summary>Переключить профиль разрешения.</summary>
    ToggleResolution = 2,

    /// <summary>Показать случайный совет (оверлей).</summary>
    ShowRandomAdvice = 3
}
