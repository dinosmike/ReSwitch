using System.Net.Http;
using System.Text.Json;

namespace ReSwitch.Services;

/// <summary>API fucking-great-advice.ru — только русский текст.</summary>
public static class GreatAdviceApiClient
{
    private const string UrlRandom = "https://fucking-great-advice.ru/api/random";
    private const string UrlCensored = "https://fucking-great-advice.ru/api/random/censored/";

    public static async Task<string?> FetchRandomAsync(bool censored, CancellationToken cancellationToken)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        var url = censored ? UrlCensored : UrlRandom;
        var json = await client.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
        return ParseText(json);
    }

    private static string? ParseText(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String)
                return t.GetString();
        }
        catch
        {
            // ignored
        }

        return null;
    }
}
