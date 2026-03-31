namespace ReSwitch.Models;

/// <summary>Параметры показа совета (только код, не Re_settings.json).</summary>
public sealed class AdviceSettings
{
    /// <summary>Единственный набор значений для оверлея и API.</summary>
    public static AdviceSettings Default { get; } = new();

    /// <summary>
    /// Сколько секунд после старта программы ждать совет (всё окно, с повторами запроса).
    /// Если за это время ответа нет — до следующего запуска программы не показываем.
    /// </summary>
    public int FetchTimeoutSeconds { get; set; } = 60;

    public int FadeInDurationMs { get; set; } = 1000;

    /// <summary>Затухание при наведении мыши до полной прозрачности.</summary>
    public int HoverFadeOutDurationMs { get; set; } = 2500;

    /// <summary>
    /// После показа совета из трея: столько миллисекунд игнорировать наведение для старта затухания,
    /// чтобы курсор над текстом (после клика по меню) не закрыл оверлей сразу.
    /// </summary>
    public int TrayOpenHoverFadeCooldownMs { get; set; } = 2000;

    public string FontFamily { get; set; } = "Helvetica Inserat LT Std";

    public double FontSizePx { get; set; } = 72;

    public string ForegroundHex { get; set; } = "#D3D3D3";

    /// <summary>Отступ от правого края экрана (основной монитор), в логических пикселях WPF.</summary>
    public double MarginRight { get; set; } = 10;

    /// <summary>Отступ от нижнего края экрана (основной монитор), в логических пикселях WPF.</summary>
    public double MarginBottom { get; set; } = 20;

    public double MarginLeft { get; set; } = 0;

    public double MarginTop { get; set; } = 0;

    /// <summary>BottomRight, BottomLeft, TopRight, TopLeft, BottomCenter, TopCenter.</summary>
    public string ScreenCorner { get; set; } = "BottomRight";

    /// <summary>Left, Center, Right — выравнивание текста.</summary>
    public string TextHorizontalAlignment { get; set; } = "Right";

    /// <summary>Показывать текст совета заглавными буквами. <c>null</c> или <c>true</c> — заглавные; <c>false</c> — как в API.</summary>
    public bool? DisplayUppercase { get; set; } = true;
}
