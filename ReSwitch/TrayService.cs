using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using ReSwitch.Models;
using ReSwitch.Services;
using Application = System.Windows.Application;

namespace ReSwitch;

public sealed class TrayService : IDisposable
{
    /// <summary>Маркер в той же строке, что и текст — левый край как у «Открыть»/«Настройки» (U+2022 «•», чуть крупнее средней точки).</summary>
    private const string TrayProfileBulletPrefix = "\u2022 ";

    /// <summary>Должно быть не меньше системного DoubleClickTime, иначе одиночный клик срабатывает раньше распознавания двойного.</summary>
    private static int SingleClickDelayMs =>
        Math.Clamp(SystemInformation.DoubleClickTime + 40, 200, 800);

    private readonly NotifyIcon _notifyIcon;
    private readonly System.Windows.Forms.Timer _singleClickTimer;

    public TrayService(UiTheme theme)
    {
        _singleClickTimer = new System.Windows.Forms.Timer { Interval = SingleClickDelayMs };
        _singleClickTimer.Tick += (_, _) =>
        {
            _singleClickTimer.Stop();
            InvokeOnUi(() => ExecuteTrayAction(isDouble: false));
        };

        _notifyIcon = new NotifyIcon
        {
            Icon = AppIconFactory.CreateTrayIcon(theme),
            Text = LocalizationService.T("Tray.BalloonTip"),
            Visible = true
        };
        // MouseUp надёжнее MouseClick для области уведомлений (часть систем не даёт стабильный Click).
        _notifyIcon.MouseUp += OnNotifyIconMouseUp;
        _notifyIcon.MouseDoubleClick += OnMouseDoubleClick;

        _notifyIcon.ContextMenuStrip = BuildContextMenu();
    }

    /// <summary>Перерисовать иконку трея под выбранную тему (цвета как в UI).</summary>
    public void RefreshIcon(UiTheme theme)
    {
        var old = _notifyIcon.Icon;
        _notifyIcon.Icon = AppIconFactory.CreateTrayIcon(theme);
        old?.Dispose();
    }

    public void RefreshTexts()
    {
        _notifyIcon.Text = LocalizationService.T("Tray.BalloonTip");
        var oldMenu = _notifyIcon.ContextMenuStrip;
        _notifyIcon.ContextMenuStrip = BuildContextMenu();
        oldMenu?.Dispose();
    }

    private ContextMenuStrip BuildContextMenu()
    {
        var menu = new ContextMenuStrip();
        var settings = SettingsStorage.Load();

        menu.Items.Add(LocalizationService.T("Tray.MenuOpen"), null, (_, _) => ShowMain());
        if (settings.AdviceEnabled)
            menu.Items.Add(LocalizationService.T("Tray.MenuRequestAdvice"), null, (_, _) => AdviceService.RequestAdviceFromTray());
        menu.Items.Add(LocalizationService.T("Tray.MenuSettings"), null, (_, _) => ShowSettings());
        menu.Items.Add(new ToolStripSeparator());

        if (settings.ShowResolutionListInTrayMenu == true && settings.Profiles.Count >= 2)
        {
            // Все профили из Re_settings.json (до 5 шт.), не только первые два.
            for (var i = 0; i < settings.Profiles.Count; i++)
            {
                var index = i;
                var p = settings.Profiles[index];
                var label = TrayProfileBulletPrefix + FormatProfileTrayMenuLabel(p, settings);
                menu.Items.Add(label, null, (_, _) => ApplyProfileFromTray(index));
            }

            menu.Items.Add(new ToolStripSeparator());
        }

        menu.Items.Add(LocalizationService.T("Tray.MenuExit"), null, (_, _) => ExitApp());
        return menu;
    }

    private static string FormatProfileTrayMenuLabel(DisplayProfile p, AppSettings settings)
    {
        var core = $"{p.Width}×{p.Height}";
        if (settings.ShowProfileNamesInTrayMenu == false)
            return core;
        if (!string.IsNullOrWhiteSpace(p.Name))
            return $"{p.Name} — {core}";
        return core;
    }

    private static void ApplyProfileFromTray(int profileIndex)
    {
        var s = SettingsStorage.Load();
        ResolutionSwitchCoordinator.ApplyProfile(
            Application.Current?.MainWindow is MainWindow mw ? mw : null,
            profileIndex,
            s);
    }

    private void OnNotifyIconMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
            return;
        _singleClickTimer.Interval = SingleClickDelayMs;
        _singleClickTimer.Stop();
        _singleClickTimer.Start();
    }

    private void OnMouseDoubleClick(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
            return;
        _singleClickTimer.Stop();
        InvokeOnUi(() => ExecuteTrayAction(isDouble: true));
    }

    private static void InvokeOnUi(Action action)
    {
        var disp = Application.Current?.Dispatcher;
        if (disp == null || disp.HasShutdownStarted)
            return;
        if (disp.CheckAccess())
            action();
        else
            disp.BeginInvoke(DispatcherPriority.Input, action);
    }

    private static void ExecuteTrayAction(bool isDouble)
    {
        if (isDouble)
        {
            ResolutionSwitchCoordinator.ToggleBetweenFirstTwoProfiles(GetOwnerWindow());
            return;
        }

        var action = SettingsStorage.Load().TraySingleClickAction ?? TrayIconClickAction.OpenWindow;
        switch (action)
        {
            case TrayIconClickAction.None:
                break;
            case TrayIconClickAction.OpenWindow:
                ShowMain();
                break;
            case TrayIconClickAction.ToggleResolution:
                ResolutionSwitchCoordinator.ToggleBetweenFirstTwoProfiles(GetOwnerWindow());
                break;
            case TrayIconClickAction.ShowRandomAdvice:
                AdviceService.RequestAdviceFromTray();
                break;
        }
    }

    private static Window? GetOwnerWindow()
    {
        return Application.Current.MainWindow is MainWindow mw ? mw : null;
    }

    private static void ShowMain()
    {
        if (Application.Current.MainWindow is not MainWindow mw)
            return;
        mw.ShowFromTray();
    }

    private static void ShowSettings()
    {
        SettingsWindow.ShowSingletonOrActivate(dlg =>
        {
            if (Application.Current.MainWindow is MainWindow mw)
            {
                dlg.LoadSettings(mw.SettingsModel);
                dlg.SyncProfilesFromMainWindow = () => mw.TryCommitAllProfilesFromUi();
            }
            else
            {
                dlg.LoadSettings(SettingsStorage.Load());
            }

            dlg.Owner = null;
            dlg.CenterOnWorkAreaAfterLoad = true;
            dlg.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        });
    }

    private static void ExitApp()
    {
        App.TryShowAutostartPromptOnFirstExit();
        App.ShutdownRequested = true;
        Application.Current.Shutdown();
    }

    public void Dispose()
    {
        _singleClickTimer.Stop();
        _singleClickTimer.Dispose();
        _notifyIcon.Visible = false;
        _notifyIcon.ContextMenuStrip?.Dispose();
        _notifyIcon.Dispose();
    }
}
