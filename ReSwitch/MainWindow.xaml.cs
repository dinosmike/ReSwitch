using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WinFormsScreen = System.Windows.Forms.Screen;
using MessageBox = System.Windows.MessageBox;
using ReSwitch.Models;
using ReSwitch.Services;

namespace ReSwitch;

public partial class MainWindow
{
    /// <summary>Задержка появления подсказок (мс), задаётся ресурсом Rs.Tooltip.InitialShowDelayMs в MainWindow.xaml.</summary>
    internal static int TooltipInitialShowDelayMs { get; private set; }

    private readonly int _tooltipDelayMs;

    private AppSettings _settings = null!;
    private bool _trayHideAnimating;
    private bool _trayShowAnimating;

    private const int TrayFlyDurationMs = 400;
    private const double TrayFlyEndScale = 0.14;

    /// <summary>Модель настроек, синхронная с главным окном (для диалога «Настройки» из трея).</summary>
    internal AppSettings SettingsModel => _settings;

    public MainWindow()
    {
        InitializeComponent();
        _tooltipDelayMs = CoerceTooltipDelayMs(TryFindResource("Rs.Tooltip.InitialShowDelayMs"));
        TooltipInitialShowDelayMs = _tooltipDelayMs;
        Loaded += (_, _) => ReloadFromStorage();
        Loaded += (_, _) => LocalizationService.LanguageChanged += OnLocalizationLanguageChanged;
        Closed += (_, _) => LocalizationService.LanguageChanged -= OnLocalizationLanguageChanged;
    }

    private void OnLocalizationLanguageChanged()
    {
        ApplyCloseButtonAppearance();
    }

    private static int CoerceTooltipDelayMs(object? r) => r switch
    {
        int i => i,
        null => 100,
        IConvertible c => Convert.ToInt32(c, CultureInfo.InvariantCulture),
        _ => 100
    };

    public void ReloadFromStorage()
    {
        _settings = SettingsStorage.Load();
        LocalizationService.Apply(AppLanguageCatalog.Normalize(_settings.UiLanguage));
        ThemeService.Apply(_settings.UiTheme);

        Bind(0, P0Name, P0W, P0H, P0Hz, P0Bpp);
        Bind(1, P1Name, P1W, P1H, P1Hz, P1Bpp);
        ApplyCloseButtonAppearance();
        TooltipDelayHelper.Apply(this, _tooltipDelayMs);
        if (System.Windows.Application.Current is App app)
            app.RefreshTrayMenu();
    }

    private void ApplyCloseButtonAppearance()
    {
        var trayMode = _settings.MinimizeToTrayOnCloseClick;
        BtnClose.ToolTip = trayMode
            ? LocalizationService.T("MainWindow.TooltipCloseToTray")
            : LocalizationService.T("MainWindow.TooltipCloseExit");
        var key = trayMode ? "Rs.TitleBar.Button" : "Rs.TitleBar.Close";
        if (TryFindResource(key) is Style st)
            BtnClose.Style = st;
    }

    private void Bind(int index, System.Windows.Controls.TextBox name, System.Windows.Controls.TextBox w,
        System.Windows.Controls.TextBox h, System.Windows.Controls.TextBox hz, System.Windows.Controls.TextBox bpp)
    {
        var p = _settings.Profiles[index];
        name.Text = p.Name;
        w.Text = p.Width.ToString();
        h.Text = p.Height.ToString();
        hz.Text = p.RefreshRate.ToString();
        bpp.Text = p.BitsPerPixel.ToString();
    }

    /// <summary>Считывает оба профиля с полей главного окна в модель (имена, разрешение и т.д.).</summary>
    internal bool TryCommitBothProfilesFromUi()
    {
        if (!ReadProfile(0, out var e0))
        {
            MessageBox.Show(e0, LocalizationService.T("Common.AppTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (!ReadProfile(1, out var e1))
        {
            MessageBox.Show(e1, LocalizationService.T("Common.AppTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        return true;
    }

    private bool ReadProfile(int index, out string? error)
    {
        error = null;
        var p = _settings.Profiles[index];
        p.Name = index == 0 ? P0Name.Text.Trim() : P1Name.Text.Trim();
        var wBox = index == 0 ? P0W : P1W;
        var hBox = index == 0 ? P0H : P1H;
        var hzBox = index == 0 ? P0Hz : P1Hz;
        var bppBox = index == 0 ? P0Bpp : P1Bpp;

        if (!int.TryParse(wBox.Text.Trim(), out var wi) || wi < 320)
        {
            error = LocalizationService.T("Validation.InvalidWidth");
            return false;
        }

        if (!int.TryParse(hBox.Text.Trim(), out var he) || he < 240)
        {
            error = LocalizationService.T("Validation.InvalidHeight");
            return false;
        }

        if (!int.TryParse(hzBox.Text.Trim(), out var hz) || hz < 0)
        {
            error = LocalizationService.T("Validation.InvalidRefresh");
            return false;
        }

        if (!int.TryParse(bppBox.Text.Trim(), out var b) || b is not (16 or 24 or 32))
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

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        if (!TryCommitBothProfilesFromUi())
            return;

        SettingsStorage.Save(_settings);
        ShowSaveSuccessFeedback();
    }

    private void ShowSaveSuccessFeedback()
    {
        // Сброс только в начале; между появлением и затуханием не вызывать BeginAnimation(null) — иначе мигание.
        SaveFeedbackCheck.BeginAnimation(UIElement.OpacityProperty, null);
        SaveFeedbackCheck.Opacity = 0;

        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        fadeIn.Completed += (_, _) =>
        {
            SaveFeedbackCheck.Opacity = 1;
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(2))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            fadeOut.Completed += (_, __) =>
            {
                SaveFeedbackCheck.BeginAnimation(UIElement.OpacityProperty, null);
                SaveFeedbackCheck.Opacity = 0;
            };
            SaveFeedbackCheck.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        };
        SaveFeedbackCheck.BeginAnimation(UIElement.OpacityProperty, fadeIn);
    }

    private void OpenSettings_OnClick(object sender, RoutedEventArgs e)
    {
        var dlg = new SettingsWindow { Owner = this };
        dlg.SyncProfilesFromMainWindow = () => TryCommitBothProfilesFromUi();
        dlg.LoadSettings(_settings);
        if (dlg.ShowDialog() == true)
            ReloadFromStorage();
    }

    private void TitleBar_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void TitleBarMinimize_OnClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void TitleBarMinimizeToTray_OnClick(object sender, RoutedEventArgs e)
    {
        HideToTrayCore(_settings);
    }

    private void TitleBarClose_OnClick(object sender, RoutedEventArgs e)
    {
        if (_settings.MinimizeToTrayOnCloseClick)
        {
            HideToTrayCore(_settings);
            return;
        }

        System.Windows.Application.Current.Shutdown();
    }

    private void P0_FromWindows_OnClick(object sender, RoutedEventArgs e) => FillFromWindows(0);

    private void P1_FromWindows_OnClick(object sender, RoutedEventArgs e) => FillFromWindows(1);

    private void FillFromWindows(int index)
    {
        if (!DisplaySettingsService.TryGetCurrentMode(out var cur, out _))
        {
            MessageBox.Show(LocalizationService.T("Errors.CouldNotGetCurrentMode"), LocalizationService.T("Common.AppTitle"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var p = _settings.Profiles[index];
        p.Width = cur.Width;
        p.Height = cur.Height;
        p.RefreshRate = cur.RefreshRate;
        p.BitsPerPixel = cur.BitsPerPixel;
        Bind(index, index == 0 ? P0Name : P1Name, index == 0 ? P0W : P1W, index == 0 ? P0H : P1H,
            index == 0 ? P0Hz : P1Hz, index == 0 ? P0Bpp : P1Bpp);
    }

    private void P0_Apply_OnClick(object sender, RoutedEventArgs e)
    {
        if (!ReadProfile(0, out var err))
        {
            MessageBox.Show(err, LocalizationService.T("Common.AppTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SettingsStorage.Save(_settings);
        ResolutionSwitchCoordinator.ApplyProfile(this, 0, _settings);
    }

    private void P1_Apply_OnClick(object sender, RoutedEventArgs e)
    {
        if (!ReadProfile(1, out var err))
        {
            MessageBox.Show(err, LocalizationService.T("Common.AppTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SettingsStorage.Save(_settings);
        ResolutionSwitchCoordinator.ApplyProfile(this, 1, _settings);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (App.ShutdownRequested)
        {
            base.OnClosing(e);
            return;
        }

        var s = SettingsStorage.Load();
        if (s.MinimizeToTrayOnCloseClick)
        {
            e.Cancel = true;
            HideToTrayCore(s);
            return;
        }

        base.OnClosing(e);
    }

    /// <summary>Скрывает окно в трей с опциональной анимацией (настройка <see cref="AppSettings.MinimizeToTrayAnimationEnabled"/>).</summary>
    private void HideToTrayCore(AppSettings s)
    {
        if (!s.MinimizeToTrayAnimationEnabled)
        {
            Hide();
            return;
        }

        if (_trayHideAnimating || _trayShowAnimating)
            return;

        _trayHideAnimating = true;
        var origLeft = Left;
        var origTop = Top;
        GetFlyToTrayTarget(origLeft, origTop, out var endLeft, out var endTop);

        var duration = TimeSpan.FromMilliseconds(TrayFlyDurationMs);
        var easing = new CubicEase { EasingMode = EasingMode.EaseIn };

        var sb = new Storyboard();

        var animLeft = new DoubleAnimation(origLeft, endLeft, duration) { EasingFunction = easing };
        Storyboard.SetTarget(animLeft, this);
        Storyboard.SetTargetProperty(animLeft, new PropertyPath(LeftProperty));
        sb.Children.Add(animLeft);

        var animTop = new DoubleAnimation(origTop, endTop, duration) { EasingFunction = easing };
        Storyboard.SetTarget(animTop, this);
        Storyboard.SetTargetProperty(animTop, new PropertyPath(TopProperty));
        sb.Children.Add(animTop);

        var fade = new DoubleAnimation(Opacity, 0, duration) { EasingFunction = easing };
        Storyboard.SetTarget(fade, this);
        Storyboard.SetTargetProperty(fade, new PropertyPath(OpacityProperty));
        sb.Children.Add(fade);

        var sx = new DoubleAnimation(TrayHideScale.ScaleX, TrayFlyEndScale, duration) { EasingFunction = easing };
        Storyboard.SetTarget(sx, TrayHideScale);
        Storyboard.SetTargetProperty(sx, new PropertyPath(ScaleTransform.ScaleXProperty));
        sb.Children.Add(sx);

        var sy = new DoubleAnimation(TrayHideScale.ScaleY, TrayFlyEndScale, duration) { EasingFunction = easing };
        Storyboard.SetTarget(sy, TrayHideScale);
        Storyboard.SetTargetProperty(sy, new PropertyPath(ScaleTransform.ScaleYProperty));
        sb.Children.Add(sy);

        sb.Completed += (_, _) =>
        {
            _trayHideAnimating = false;
            sb.Remove(this);
            BeginAnimation(LeftProperty, null);
            BeginAnimation(TopProperty, null);
            Left = origLeft;
            Top = origTop;
            BeginAnimation(OpacityProperty, null);
            Opacity = 1;
            TrayHideScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            TrayHideScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            TrayHideScale.ScaleX = 1;
            TrayHideScale.ScaleY = 1;
            Hide();
        };

        sb.Begin(this, true);
    }

    /// <summary>Открытие из трея: обратная траектория и нарастание прозрачности. Масштаб здесь не анимируем — иначе при RenderTransform на Window виден полноразмерный кадр с «миниатюрой» по центру.</summary>
    internal void ShowFromTray()
    {
        ReloadFromStorage();

        if (WindowState == WindowState.Minimized)
        {
            ShowCore();
            return;
        }

        if (IsVisible)
        {
            WindowState = WindowState.Normal;
            Topmost = true;
            Topmost = false;
            Activate();
            return;
        }

        if (!_settings.MinimizeToTrayAnimationEnabled)
        {
            ShowCore();
            return;
        }

        if (_trayShowAnimating || _trayHideAnimating)
            return;

        var endLeft = Left;
        var endTop = Top;
        GetFlyToTrayTarget(endLeft, endTop, out var startLeft, out var startTop);

        BeginAnimation(LeftProperty, null);
        BeginAnimation(TopProperty, null);
        BeginAnimation(OpacityProperty, null);
        TrayHideScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        TrayHideScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);

        Left = startLeft;
        Top = startTop;
        Opacity = 0;
        TrayHideScale.ScaleX = 1;
        TrayHideScale.ScaleY = 1;

        Show();
        WindowState = WindowState.Normal;

        _trayShowAnimating = true;
        var duration = TimeSpan.FromMilliseconds(TrayFlyDurationMs);
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };

        var sb = new Storyboard();

        var animLeft = new DoubleAnimation(startLeft, endLeft, duration) { EasingFunction = easing };
        Storyboard.SetTarget(animLeft, this);
        Storyboard.SetTargetProperty(animLeft, new PropertyPath(LeftProperty));
        sb.Children.Add(animLeft);

        var animTop = new DoubleAnimation(startTop, endTop, duration) { EasingFunction = easing };
        Storyboard.SetTarget(animTop, this);
        Storyboard.SetTargetProperty(animTop, new PropertyPath(TopProperty));
        sb.Children.Add(animTop);

        var fade = new DoubleAnimation(0, 1, duration) { EasingFunction = easing };
        Storyboard.SetTarget(fade, this);
        Storyboard.SetTargetProperty(fade, new PropertyPath(OpacityProperty));
        sb.Children.Add(fade);

        sb.Completed += (_, _) =>
        {
            _trayShowAnimating = false;
            sb.Remove(this);
            BeginAnimation(LeftProperty, null);
            BeginAnimation(TopProperty, null);
            Left = endLeft;
            Top = endTop;
            BeginAnimation(OpacityProperty, null);
            Opacity = 1;
            TrayHideScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            TrayHideScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            TrayHideScale.ScaleX = 1;
            TrayHideScale.ScaleY = 1;
            Topmost = true;
            Topmost = false;
            Activate();
        };

        sb.Begin(this, true);
    }

    private void ShowCore()
    {
        Show();
        WindowState = WindowState.Normal;
        Topmost = true;
        Topmost = false;
        Activate();
    }

    /// <summary>
    /// Цель — нижний правый угол рабочей области (к трею), но сдвиг по X ослаблен, по Y полный:
    /// окно визуально «падает» вниз, без сильного рывка вправо.
    /// </summary>
    private void GetFlyToTrayTarget(double origLeft, double origTop, out double endLeft, out double endTop)
    {
        const double horizontalFactor = 0.38;
        const double verticalFactor = 1.0;

        var w = ActualWidth > 0 ? ActualWidth : Width;
        var h = ActualHeight > 0 ? ActualHeight : Height;

        double cornerLeft;
        double cornerTop;

        var src = PresentationSource.FromVisual(this);
        if (src?.CompositionTarget != null)
        {
            try
            {
                var handle = new WindowInteropHelper(this).EnsureHandle();
                var wa = WinFormsScreen.FromHandle(handle).WorkingArea;
                var tf = src.CompositionTarget.TransformFromDevice;
                var br = tf.Transform(new System.Windows.Point(wa.Right, wa.Bottom));
                cornerLeft = br.X - w;
                cornerTop = br.Y - h;
                endLeft = origLeft + (cornerLeft - origLeft) * horizontalFactor;
                endTop = origTop + (cornerTop - origTop) * verticalFactor;
                return;
            }
            catch
            {
                // fallback ниже
            }
        }

        var waDip = SystemParameters.WorkArea;
        cornerLeft = waDip.Right - w;
        cornerTop = waDip.Bottom - h;
        endLeft = origLeft + (cornerLeft - origLeft) * horizontalFactor;
        endTop = origTop + (cornerTop - origTop) * verticalFactor;
    }
}
