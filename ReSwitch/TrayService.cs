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
        menu.Items.Add(LocalizationService.T("Tray.MenuSettings"), null, (_, _) => ShowSettings());
        menu.Items.Add(new ToolStripSeparator());

        if (settings.ShowResolutionListInTrayMenu == true && settings.Profiles.Count >= 2)
        {
            for (var i = 0; i < 2; i++)
            {
                var index = i;
                var p = settings.Profiles[index];
                var label = FormatProfileTrayMenuLabel(p);
                menu.Items.Add(label, null, (_, _) => ApplyProfileFromTray(index));
            }

            menu.Items.Add(new ToolStripSeparator());
        }

        menu.Items.Add(LocalizationService.T("Tray.MenuExit"), null, (_, _) => ExitApp());
        return menu;
    }

    private static string FormatProfileTrayMenuLabel(DisplayProfile p)
    {
        var core = $"{p.Width}×{p.Height}";
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
            ResolutionSwitchCoordinator.ToggleBetweenProfiles(GetOwnerWindow());
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
                ResolutionSwitchCoordinator.ToggleBetweenProfiles(GetOwnerWindow());
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
        var dlg = new SettingsWindow();
        if (Application.Current.MainWindow is MainWindow mw)
        {
            dlg.LoadSettings(mw.SettingsModel);
            dlg.SyncProfilesFromMainWindow = () => mw.TryCommitBothProfilesFromUi();
        }
        else
        {
            dlg.LoadSettings(SettingsStorage.Load());
        }

        var owner = Application.Current.MainWindow is MainWindow mwOwner && mwOwner.IsVisible ? mwOwner : null;
        dlg.Owner = owner;
        if (dlg.ShowDialog() == true && Application.Current.MainWindow is MainWindow main)
            main.ReloadFromStorage();
    }

    private static void ExitApp()
    {
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
