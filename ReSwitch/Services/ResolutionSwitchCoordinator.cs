using System.Windows;
using MessageBox = System.Windows.MessageBox;
using ReSwitch.Models;

namespace ReSwitch.Services;

public static class ResolutionSwitchCoordinator
{
    /// <summary>Если 1-й и 2-й профиль совпадают с текущим режимом одновременно — чередуем по клику из трея.</summary>
    private static bool _trayFirstTwoNextIsSecond;

    /// <summary>Переключение из трея только между 1-м и 2-м профилем (не по всем профилям по кругу).</summary>
    public static void ToggleBetweenFirstTwoProfiles(Window? owner)
    {
        var settings = SettingsStorage.Load();
        if (settings.Profiles.Count < 2)
            return;

        if (!DisplaySettingsService.TryGetCurrentMode(out var current, out _))
            return;

        var match0 = DisplaySettingsService.ProfileMatchesCurrent(settings.Profiles[0], current);
        var match1 = DisplaySettingsService.ProfileMatchesCurrent(settings.Profiles[1], current);

        int target;
        if (match0 && match1)
        {
            target = _trayFirstTwoNextIsSecond ? 1 : 0;
            _trayFirstTwoNextIsSecond = !_trayFirstTwoNextIsSecond;
        }
        else if (match0)
            target = 1;
        else if (match1)
            target = 0;
        else
            target = 0;

        ApplyProfile(owner, target, settings);
    }

    public static void ApplyProfile(Window? owner, int profileIndex, AppSettings? settings = null)
    {
        settings ??= SettingsStorage.Load();
        if (profileIndex < 0 || profileIndex >= settings.Profiles.Count)
            return;

        if (!DisplaySettingsService.TryGetCurrentMode(out _, out var previousRaw))
        {
            MessageBox.Show(LocalizationService.T("Errors.ReadCurrentFailed"), LocalizationService.T("Common.AppTitle"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var target = settings.Profiles[profileIndex];

        if (!DisplaySettingsService.TryTestMode(target, out var testError))
        {
            MessageBox.Show(testError ?? LocalizationService.T("Errors.ModeUnavailable"), LocalizationService.T("Common.AppTitle"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (settings.ConfirmSwitchEnabled && settings.ConfirmTimeoutSeconds > 0)
        {
            if (!DisplaySettingsService.TryApplyMode(target, out var applyError))
            {
                MessageBox.Show(applyError ?? LocalizationService.T("Errors.ApplyError"), LocalizationService.T("Common.AppTitle"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dlg = new ConfirmResolutionWindow(settings.ConfirmTimeoutSeconds)
            {
                Owner = owner
            };
            var result = dlg.ShowDialog();
            if (result != true)
            {
                DisplaySettingsService.TryApplyRaw(in previousRaw, out _);
            }
        }
        else
        {
            if (!DisplaySettingsService.TryApplyMode(target, out var applyError))
            {
                MessageBox.Show(applyError ?? LocalizationService.T("Errors.ApplyError"), LocalizationService.T("Common.AppTitle"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
