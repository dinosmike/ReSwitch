using System.Text.Json.Serialization;

namespace ReSwitch.Models;

/// <summary>Элемент массива <c>profiles</c> в Re_settings.json: имя на экране и параметры режима.</summary>
public sealed class DisplayProfile
{
    /// <summary>Отображаемое имя профиля — сохраняется в JSON как <c>name</c>.</summary>
    [JsonPropertyName("name")]
    [JsonPropertyOrder(0)]
    public string Name { get; set; } = "";

    [JsonPropertyName("width")]
    [JsonPropertyOrder(1)]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    [JsonPropertyOrder(2)]
    public int Height { get; set; }

    /// <summary>Герцы (0 — по умолчанию драйвера).</summary>
    [JsonPropertyName("refreshRate")]
    [JsonPropertyOrder(3)]
    public int RefreshRate { get; set; }

    /// <summary>Бит на пиксель (например 32).</summary>
    [JsonPropertyName("bitsPerPixel")]
    [JsonPropertyOrder(4)]
    public int BitsPerPixel { get; set; }

    public DisplayProfile Clone() => new()
    {
        Name = Name,
        Width = Width,
        Height = Height,
        RefreshRate = RefreshRate,
        BitsPerPixel = BitsPerPixel
    };
}
