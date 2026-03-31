using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
        Loaded += (_, _) => BuildResolutionContextMenu();
        UpdateHeaderAndTooltips();
    }

    private void BuildResolutionContextMenu()
    {
        try
        {
            var win = Window.GetWindow(this);
            if (win == null) return;

            var raw = win.TryFindResource("Rs.Profile.ResolutionQuickPickList") as string;
            if (string.IsNullOrWhiteSpace(raw)) return;

            var entries = raw!.Split(',');
            if (entries.Length == 0) return;

            var cm = new ContextMenu();
            cm.Items.Add(new MenuItem { Command = ApplicationCommands.Cut });
            cm.Items.Add(new MenuItem { Command = ApplicationCommands.Copy });
            cm.Items.Add(new MenuItem { Command = ApplicationCommands.Paste });
            cm.Items.Add(new Separator());

            foreach (var entry in entries)
            {
                var text = entry.Trim();
                if (text.Length == 0) continue;
                var mi = new MenuItem { Header = text };
                mi.Click += (_, _) => ApplyResolutionFromMenu(text);
                cm.Items.Add(mi);
            }

            WBox.ContextMenu = cm;
            HBox.ContextMenu = cm;
        }
        catch
        {
            // Prevent app crash if menu construction fails
        }
    }

    private void ApplyResolutionFromMenu(string entry)
    {
        var s = entry.Replace('×', 'x').Replace('X', 'x');
        var idx = s.IndexOf('x');
        if (idx <= 0 || idx >= s.Length - 1) return;

        if (int.TryParse(s.Substring(0, idx).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var w) &&
            int.TryParse(s.Substring(idx + 1).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var h) &&
            w >= 320 && h >= 240)
        {
            WBox.Text = w.ToString(CultureInfo.InvariantCulture);
            HBox.Text = h.ToString(CultureInfo.InvariantCulture);
        }
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
        BindDisplayMode(p);
    }

    internal void BindDisplayMode(DisplayProfile p)
    {
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
