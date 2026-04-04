using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using ReSwitch.Models;
using ReSwitch.Services;

namespace ReSwitch;

public partial class SettingsWindow
{
    private AppSettings _settings = null!;
    private bool _themeRadioLoading;
    private bool _languageComboLoading;
    private UiTheme _themeAtOpen;
    private string _languageAtOpen = "en";

    /// <summary>Если задано (главное окно открыто), перед записью Re_settings.json подтянуть профили с полей главного окна.</summary>
    public Func<bool>? SyncProfilesFromMainWindow { get; set; }

    /// <summary>Если true — после первой вёрстки центрировать по рабочей области экрана (трей, <see cref="SizeToContent"/>).</summary>
    internal bool CenterOnWorkAreaAfterLoad { get; set; }

    private static SettingsWindow? _singletonInstance;

    /// <summary>Одно модальное окно настроек: повторный вызов активирует уже открытое (трей / главное окно).</summary>
    internal static void ShowSingletonOrActivate(Action<SettingsWindow> configure)
    {
        var app = System.Windows.Application.Current;
        if (app?.Dispatcher == null)
            return;

        void Run()
        {
            if (_singletonInstance != null)
            {
                _singletonInstance.Activate();
                if (_singletonInstance.WindowState == WindowState.Minimized)
                    _singletonInstance.WindowState = WindowState.Normal;
                _singletonInstance.Focus();
                return;
            }

            var dlg = new SettingsWindow();
            configure(dlg);
            _singletonInstance = dlg;
            try
            {
                if (dlg.ShowDialog() == true && app.MainWindow is MainWindow main)
                    main.ReloadFromStorage();
            }
            finally
            {
                if (_singletonInstance == dlg)
                    _singletonInstance = null;
            }
        }

        if (app.Dispatcher.CheckAccess())
            Run();
        else
            app.Dispatcher.Invoke(Run);
    }

    public SettingsWindow()
    {
        InitializeComponent();
        TooltipDelayHelper.Apply(this, MainWindow.TooltipInitialShowDelayMs);

        Loaded += OnLoaded;
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape)
                return;
            e.Handled = true;
            RevertAndClose();
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (CenterOnWorkAreaAfterLoad)
            CenterOnWorkArea();

        ConfirmCheck.Checked += (_, _) => UpdateConfirmDependentUi();
        ConfirmCheck.Unchecked += (_, _) => UpdateConfirmDependentUi();
        UpdateConfirmDependentUi();
        UpdateTrayProfileNamesDependentUi();
    }

    private void CenterOnWorkArea()
    {
        UpdateLayout();
        var wa = SystemParameters.WorkArea;
        Left = wa.Left + (wa.Width - ActualWidth) / 2;
        Top = wa.Top + (wa.Height - ActualHeight) / 2;
    }

    public void LoadSettings(AppSettings settings)
    {
        _settings = settings;
        _themeAtOpen = settings.UiTheme;
        _languageAtOpen = AppLanguageCatalog.Normalize(settings.UiLanguage);
        _themeRadioLoading = true;
        switch (settings.UiTheme)
        {
            case UiTheme.Light:
                ThemeLightRadio.IsChecked = true;
                break;
            case UiTheme.Fuchsia:
                ThemeFuchsiaRadio.IsChecked = true;
                break;
            case UiTheme.Aquamarine:
                ThemeAquamarineRadio.IsChecked = true;
                break;
            default:
                ThemeDarkRadio.IsChecked = true;
                break;
        }

        _themeRadioLoading = false;

        _languageComboLoading = true;
        LanguageCombo.ItemsSource = AppLanguageCatalog.All;
        LanguageCombo.SelectedValue = _languageAtOpen;
        _languageComboLoading = false;

        AutostartCheck.IsChecked = settings.AutostartEnabled;
        ConfirmCheck.IsChecked = settings.ConfirmSwitchEnabled;
        CloseToTrayCheck.IsChecked = settings.MinimizeToTrayOnCloseClick;
        StartupToTrayCheck.IsChecked = settings.MinimizeToTrayOnStartup;
        TrayHideAnimationCheck.IsChecked = settings.MinimizeToTrayAnimationEnabled;
        TimeoutBox.Text = settings.ConfirmTimeoutSeconds.ToString();
        TrayShowResolutionMenuCheck.IsChecked = settings.ShowResolutionListInTrayMenu == true;
        TrayShowProfileNamesCheck.IsChecked = settings.ShowProfileNamesInTrayMenu != false;

        AdviceOnStartupCheck.IsChecked = settings.AdviceEnabled;

        var action = settings.TraySingleClickAction ?? TrayIconClickAction.OpenWindow;
        switch (action)
        {
            case TrayIconClickAction.ToggleResolution:
                TraySingleToggleProfile.IsChecked = true;
                break;
            case TrayIconClickAction.ShowRandomAdvice:
                TraySingleRandomAdvice.IsChecked = true;
                break;
            default:
                TraySingleOpenWindow.IsChecked = true;
                break;
        }

        UpdateConfirmDependentUi();
        UpdateTrayProfileNamesDependentUi();
    }

    private void TrayShowResolutionMenuCheck_OnChecked(object sender, RoutedEventArgs e) =>
        UpdateTrayProfileNamesDependentUi();

    private void UpdateTrayProfileNamesDependentUi()
    {
        var listOn = TrayShowResolutionMenuCheck.IsChecked == true;
        TrayShowProfileNamesCheck.IsEnabled = listOn;
        if (!listOn)
            TrayShowProfileNamesCheck.IsChecked = false;
    }

    /// <summary>После смены <see cref="AppSettings.AdviceEnabled"/> извне (оверлей совета) — обновить галочки блока «Совет».</summary>
    internal void SyncAdviceCheckboxesFromModel()
    {
        AdviceOnStartupCheck.IsChecked = _settings.AdviceEnabled;
    }

    private void UpdateConfirmDependentUi()
    {
        var on = ConfirmCheck.IsChecked == true;
        TimeoutBox.IsEnabled = on;
        TimeoutLabel.Opacity = on ? 1 : 0.45;
    }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        if (SyncProfilesFromMainWindow != null && !SyncProfilesFromMainWindow())
            return;

        _settings.ConfirmSwitchEnabled = ConfirmCheck.IsChecked == true;
        _settings.MinimizeToTrayOnCloseClick = CloseToTrayCheck.IsChecked == true;
        _settings.MinimizeToTrayOnStartup = StartupToTrayCheck.IsChecked == true;
        _settings.MinimizeToTrayAnimationEnabled = TrayHideAnimationCheck.IsChecked == true;
        _settings.UiTheme = ReadThemeFromRadios();
        if (TraySingleToggleProfile.IsChecked == true)
            _settings.TraySingleClickAction = TrayIconClickAction.ToggleResolution;
        else if (TraySingleRandomAdvice.IsChecked == true)
            _settings.TraySingleClickAction = TrayIconClickAction.ShowRandomAdvice;
        else
            _settings.TraySingleClickAction = TrayIconClickAction.OpenWindow;
        _settings.ShowResolutionListInTrayMenu = TrayShowResolutionMenuCheck.IsChecked == true;
        _settings.ShowProfileNamesInTrayMenu = TrayShowProfileNamesCheck.IsChecked == true;
        _settings.AdviceEnabled = AdviceOnStartupCheck.IsChecked == true;
        if (!int.TryParse(TimeoutBox.Text.Trim(), out var sec) || sec < 1)
            sec = 15;
        if (sec > 300)
            sec = 300;
        _settings.ConfirmTimeoutSeconds = sec;
        _settings.UiLanguage = AppLanguageCatalog.Normalize(LanguageCombo.SelectedValue as string ?? _settings.UiLanguage);

        var wantAutostart = AutostartCheck.IsChecked == true;
        AutostartService.SetEnabled(wantAutostart);
        _settings.AutostartEnabled = wantAutostart;

        SettingsStorage.Save(_settings);
        DialogResult = true;
        Close();
    }

    private UiTheme ReadThemeFromRadios()
    {
        if (ThemeLightRadio.IsChecked == true)
            return UiTheme.Light;
        if (ThemeFuchsiaRadio.IsChecked == true)
            return UiTheme.Fuchsia;
        if (ThemeAquamarineRadio.IsChecked == true)
            return UiTheme.Aquamarine;
        return UiTheme.Dark;
    }

    private void ThemeRadio_OnChecked(object sender, RoutedEventArgs e)
    {
        if (_themeRadioLoading)
            return;
        ThemeService.Apply(ReadThemeFromRadios());
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e) => RevertAndClose();

    /// <summary>Откат темы и языка (язык мог быть записан в файл при мгновенном выборе).</summary>
    private void RevertAndClose()
    {
        _settings.UiLanguage = _languageAtOpen;
        SettingsStorage.Save(_settings);
        LocalizationService.Apply(_languageAtOpen);
        ThemeService.Apply(_themeAtOpen);
        DialogResult = false;
        Close();
    }

    private void LanguageCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_languageComboLoading)
            return;
        if (LanguageCombo.SelectedValue is not string code)
            return;

        LocalizationService.Apply(code);
        _settings.UiLanguage = code;
        SettingsStorage.Save(_settings);
    }

    private void AdviceSectionHyperlink_OnRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
        }
        catch
        {
            // ignored
        }

        e.Handled = true;
    }

    private void TitleBar_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void TitleBarClose_OnClick(object sender, RoutedEventArgs e)
    {
        Cancel_OnClick(sender, e);
    }

    private void TitleBarAbout_OnClick(object sender, RoutedEventArgs e) => About.Show(this);

    private void TitleBarAbout_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        About.Show(this);
    }
}
