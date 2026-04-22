using System.Collections.Generic;

namespace ReSwitch.Services;

/// <summary>
/// Фиксирует, какие companion-программы (ReQuit/ReIcon/ReFlow) были обнаружены при запуске приложения.
/// Нужен для скрытых пунктов меню трея и скрытых UI-элементов в настройках.
/// </summary>
public static class CompanionAppPresence
{
    private static readonly object Gate = new();
    private static readonly HashSet<string> DetectedAtStartup = new(StringComparer.OrdinalIgnoreCase);

    public static void SetFromScan(IReadOnlyDictionary<string, bool> existsByFileName)
    {
        lock (Gate)
        {
            DetectedAtStartup.Clear();
            foreach (var kv in existsByFileName)
            {
                if (kv.Value)
                    DetectedAtStartup.Add(kv.Key);
            }
        }
    }

    public static bool AnyCompanionDetected
    {
        get
        {
            lock (Gate)
                return DetectedAtStartup.Count > 0;
        }
    }

    public static bool WasDetectedAtStartup(string exeFileName)
    {
        lock (Gate)
            return DetectedAtStartup.Contains(exeFileName);
    }
}

