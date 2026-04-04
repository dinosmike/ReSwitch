using System.Threading;
using System.Windows;
using System.Windows.Threading;
using ReSwitch.Models;
using ReSwitch.Services;

namespace ReSwitch;

public partial class App : System.Windows.Application
{
    private Mutex? _mutex;
    private EventWaitHandle? _activateEvent;
    private RegisteredWaitHandle? _activateWaitRegistration;
    private TrayService? _tray;

    /// <summary>Истинно при «Выход» из трея — чтобы не отменять закрытие в OnClosing.</summary>
    internal static bool ShutdownRequested { get; set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        // Должен существовать до Mutex, чтобы второй процесс мог сигналить до RegisterWait.
        var activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "ReSwitch_ActivateMainWindow");

        _mutex = new Mutex(true, "ReSwitch_SingleInstance_Mutex", out var createdNew);
        if (!createdNew)
        {
            activateEvent.Set();
            activateEvent.Dispose();
            Shutdown();
            return;
        }

        _activateEvent = activateEvent;
        _activateWaitRegistration = ThreadPool.RegisterWaitForSingleObject(
            _activateEvent,
            (_, timedOut) =>
            {
                if (timedOut)
                    return;
                Dispatcher.BeginInvoke(
                    () =>
                    {
                        if (MainWindow is MainWindow mw)
                            mw.ShowFromTray();
                    },
                    DispatcherPriority.Normal);
            },
            null,
            Timeout.Infinite,
            executeOnlyOnce: false);


        var startupSettings = SettingsStorage.Load();
        AutostartService.SetEnabled(startupSettings.AutostartEnabled);

        // До показа главного окна нельзя использовать OnLastWindowClose: при первом запуске закрытие окна вопроса
        // (советы) сочтётся «последним окном» и процесс завершится до main.Show().
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        base.OnStartup(e);

        ThemeService.Apply(startupSettings.UiTheme);
        LocalizationService.Apply(AppLanguageCatalog.Normalize(startupSettings.UiLanguage));

        // Вопрос «советы при запуске» только выставляет AdviceEnabled и сбрасывает askOnStart; запуск программы идёт дальше в любом случае.
        if (startupSettings.AdviceAskOnStart)
        {
            var adviceDlg = new AdvicePromptWindow();
            adviceDlg.ShowDialog();
            startupSettings = SettingsStorage.Load();
        }

        ShutdownMode = ShutdownMode.OnLastWindowClose;
        // Icon не задаём — Windows берёт многомасштабную иконку из exe (ApplicationIcon), как у окна настроек.
        var main = new MainWindow();
        MainWindow = main;
        main.Show();

        if (startupSettings.MinimizeToTrayOnStartup)
            main.Hide();

        _tray = new TrayService(startupSettings.UiTheme);
        LocalizationService.LanguageChanged += OnLocalizationLanguageChanged;
    }

    private void OnLocalizationLanguageChanged() => _tray?.RefreshTexts();

    /// <summary>Пересобрать меню трея (профили, язык, пункты из настроек).</summary>
    internal void RefreshTrayMenu() => _tray?.RefreshTexts();

    /// <summary>Иконка трея (окно использует встроенную в exe иконку).</summary>
    public void RefreshIconsForTheme(UiTheme theme)
    {
        _tray?.RefreshIcon(theme);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_activateWaitRegistration != null && _activateEvent != null)
        {
            _activateWaitRegistration.Unregister(_activateEvent);
            _activateWaitRegistration = null;
        }

        _activateEvent?.Dispose();
        _activateEvent = null;

        LocalizationService.LanguageChanged -= OnLocalizationLanguageChanged;
        _tray?.Dispose();
        try
        {
            _mutex?.ReleaseMutex();
        }
        catch
        {
            // ignored
        }

        _mutex?.Dispose();
        base.OnExit(e);
    }

    /// <summary>Если askOnExit == true — показать вопрос про автозапуск. Вызывать ДО Shutdown().</summary>
    internal static void TryShowAutostartPromptOnFirstExit()
    {
        try
        {
            var s = SettingsStorage.Load();
            if (!s.AskOnExit)
                return;

            var dlg = new AutostartPromptWindow();
            dlg.ShowDialog();
        }
        catch
        {
            // не мешаем завершению процесса
        }
    }

}
