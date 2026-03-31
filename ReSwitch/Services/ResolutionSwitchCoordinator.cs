using System.Windows;
using MessageBox = System.Windows.MessageBox;
using ReSwitch.Models;

namespace ReSwitch.Services;

public static class ResolutionSwitchCoordinator
{
    /// <summary>Переключить на другой из двух профилей (0 ↔ 1).</summary>
    public static void ToggleBetweenProfiles(Window? owner)
    {
        var settings = SettingsStorage.Load();
        if (settings.Profiles.Count < 2)
            return;

        if (!DisplaySettingsService.TryGetCurrentMode(out var current, out _))
            return;

        var p0 = settings.Profiles[0];
        var p1 = settings.Profiles[1];
        var m0 = DisplaySettingsService.ProfileMatchesCurrent(p0, current);
        var m1 = DisplaySettingsService.ProfileMatchesCurrent(p1, current);

        int target;
        if (m0 && !m1)
            target = 1;
        else if (m1 && !m0)
            target = 0;
        else if (m0 && m1)
            target = 1;
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
