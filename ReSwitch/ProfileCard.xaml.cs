using System.Windows;
using System.Windows.Controls;
using ReSwitch.Models;
using ReSwitch.Services;
using WpfUserControl = System.Windows.Controls.UserControl;

namespace ReSwitch;

public partial class ProfileCard : WpfUserControl
{
    public int ProfileIndex { get; }

    public event EventHandler? ApplyClicked;
    public event EventHandler? GetFromWindowsClicked;
    public event EventHandler? RemoveClicked;

    public ProfileCard(int profileIndex, bool canRemove)
    {
        InitializeComponent();
        ProfileIndex = profileIndex;
        RemoveButton.Visibility = canRemove ? Visibility.Visible : Visibility.Collapsed;
        LocalizationService.LanguageChanged += OnLanguageChanged;
        Unloaded += (_, _) => LocalizationService.LanguageChanged -= OnLanguageChanged;
        UpdateHeaderAndTooltips();
    }

    private void OnLanguageChanged() => UpdateHeaderAndTooltips();

    private void UpdateHeaderAndTooltips()
    {
        HeaderTitle.Text = LocalizationService.T("MainWindow.ProfileHeaderFormat", ProfileIndex + 1);
        RemoveButton.ToolTip = LocalizationService.T("MainWindow.RemoveProfile");
    }

    internal void Bind(DisplayProfile p)
    {
        NameBox.Text = p.Name;
        WBox.Text = p.Width.ToString();
        HBox.Text = p.Height.ToString();
        HzBox.Text = p.RefreshRate.ToString();
        BppBox.Text = p.BitsPerPixel.ToString();
    }

    internal bool TryReadTo(DisplayProfile p, out string? error)
    {
        error = null;
        p.Name = NameBox.Text.Trim();

        if (!int.TryParse(WBox.Text.Trim(), out var wi) || wi < 320)
        {
            error = LocalizationService.T("Validation.InvalidWidth");
            return false;
        }

        if (!int.TryParse(HBox.Text.Trim(), out var he) || he < 240)
        {
            error = LocalizationService.T("Validation.InvalidHeight");
            return false;
        }

        if (!int.TryParse(HzBox.Text.Trim(), out var hz) || hz < 0)
        {
            error = LocalizationService.T("Validation.InvalidRefresh");
            return false;
        }

        if (!int.TryParse(BppBox.Text.Trim(), out var b) || b is not (16 or 24 or 32))
        {
            error = LocalizationService.T("Validation.InvalidBpp");
            return false;
        }

        p.Width = wi;
        p.Height = he;
        p.RefreshRate = hz;
        p.BitsPerPixel = b;
        return true;
    }

    private void Apply_OnClick(object sender, RoutedEventArgs e) => ApplyClicked?.Invoke(this, EventArgs.Empty);

    private void GetFromWindows_OnClick(object sender, RoutedEventArgs e) => GetFromWindowsClicked?.Invoke(this, EventArgs.Empty);

    private void Remove_OnClick(object sender, RoutedEventArgs e) => RemoveClicked?.Invoke(this, EventArgs.Empty);
}
