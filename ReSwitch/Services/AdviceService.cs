using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using ReSwitch.Models;

namespace ReSwitch.Services;

/// <summary>Запрос совета по сети в фоне; UI не блокируется.</summary>
public static class AdviceService
{
    /// <summary>При старте программы — только если в настройках включены советы при запуске.</summary>
    public static void TryScheduleStartupTip(Window owner)
    {
        if (!SettingsStorage.Load().AdviceEnabled)
            return;

        RunAdviceFetchAndShow(owner, openedFromTray: false);
    }

    /// <summary>По запросу из трея — без проверки «совет при запуске», тот же сетевой цикл и оверлей.</summary>
    public static void RequestAdviceFromTray()
    {
        RunAdviceFetchAndShow(System.Windows.Application.Current?.MainWindow, openedFromTray: true);
    }

    private static void RunAdviceFetchAndShow(Window? presentationWindow, bool openedFromTray)
    {
        var dispatcher = presentationWindow?.Dispatcher ?? System.Windows.Application.Current?.Dispatcher;
        if (dispatcher == null)
            return;

        var advice = AdviceSettings.Default;
        var totalWaitSec = Math.Clamp(advice.FetchTimeoutSeconds, 1, 600);
        var censored = false;

        _ = Task.Run(async () =>
        {
            var deadline = DateTime.UtcNow.AddSeconds(totalWaitSec);
            using var totalCts = new CancellationTokenSource(TimeSpan.FromSeconds(totalWaitSec));

            string? tipText = null;
            while (DateTime.UtcNow < deadline && !totalCts.IsCancellationRequested)
            {
                var remaining = deadline - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero)
                    break;

                try
                {
                    using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(totalCts.Token);
                    attemptCts.CancelAfter(remaining);

                    var text = await GreatAdviceApiClient.FetchRandomAsync(censored, attemptCts.Token)
                        .ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        tipText = text.Trim();
                        break;
                    }
                }
                catch
                {
                    // нет сети / обрыв — пауза и следующая попытка, пока не кончилось окно ожидания
                }

                remaining = deadline - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero)
                    break;

                var pause = TimeSpan.FromSeconds(1);
                if (remaining < pause)
                    pause = remaining;
                if (pause <= TimeSpan.Zero)
                    break;

                try
                {
                    await Task.Delay(pause, totalCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }

            if (tipText == null)
                return;

            _ = dispatcher.BeginInvoke(new Action(() =>
            {
                if (presentationWindow != null && !presentationWindow.IsLoaded)
                    return;
                var w = new AdviceOverlayWindow(tipText, SettingsStorage.Load(), openedFromTray);
                w.Show();
            }));
        });
    }
}
