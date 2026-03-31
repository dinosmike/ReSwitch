using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
        ConfirmCheck.Checked += (_, _) => UpdateConfirmDependentUi();
        ConfirmCheck.Unchecked += (_, _) => UpdateConfirmDependentUi();
        UpdateConfirmDependentUi();
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

        var action = settings.TraySingleClickAction ?? TrayIconClickAction.OpenWindow;
        if (action == TrayIconClickAction.ToggleResolution)
        {
            TraySingleToggleProfile.IsChecked = true;
        }
        else
        {
            TraySingleOpenWindow.IsChecked = true;
        }

        UpdateConfirmDependentUi();
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
        _settings.TraySingleClickAction = TraySingleToggleProfile.IsChecked == true
            ? TrayIconClickAction.ToggleResolution
            : TrayIconClickAction.OpenWindow;
        _settings.ShowResolutionListInTrayMenu = TrayShowResolutionMenuCheck.IsChecked == true;
        _settings.ShowProfileNamesInTrayMenu = TrayShowProfileNamesCheck.IsChecked == true;
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

    private void TitleBar_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void TitleBarClose_OnClick(object sender, RoutedEventArgs e)
    {
        Cancel_OnClick(sender, e);
    }
}
