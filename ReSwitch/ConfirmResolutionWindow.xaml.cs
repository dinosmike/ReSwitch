using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ReSwitch.Services;

namespace ReSwitch;

public partial class ConfirmResolutionWindow
{
    private readonly DispatcherTimer _timer;
    private int _remaining;

    public ConfirmResolutionWindow(int timeoutSeconds)
    {
        InitializeComponent();
        TooltipDelayHelper.Apply(this, MainWindow.TooltipInitialShowDelayMs);
        _remaining = timeoutSeconds;
        UpdateCountdown();

        LocalizationService.LanguageChanged += OnLocalizationLanguageChanged;
        Closed += (_, _) => LocalizationService.LanguageChanged -= OnLocalizationLanguageChanged;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) =>
        {
            _remaining--;
            if (_remaining <= 0)
            {
                _timer.Stop();
                DialogResult = false;
                Close();
                return;
            }

            UpdateCountdown();
        };
        _timer.Start();
    }

    private void OnLocalizationLanguageChanged() => UpdateCountdown();

    private void UpdateCountdown()
    {
        CountdownText.Text = LocalizationService.T("ConfirmDialog.Countdown", _remaining);
    }

    private void ConfirmButton_OnClick(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        DialogResult = true;
        Close();
    }

    private void RevertButton_OnClick(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        DialogResult = false;
        Close();
    }

    private void TitleBar_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void TitleBarClose_OnClick(object sender, RoutedEventArgs e)
    {
        RevertButton_OnClick(sender, e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        if (DontShowAgainCheck.IsChecked == true)
        {
            var s = SettingsStorage.Load();
            s.ConfirmSwitchEnabled = false;
            SettingsStorage.Save(s);
            if (System.Windows.Application.Current.MainWindow is MainWindow main)
                main.ReloadFromStorage();
        }

        base.OnClosed(e);
    }
}
