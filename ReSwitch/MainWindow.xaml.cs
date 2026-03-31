using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
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
    private bool _centeredOnWorkAreaOnce;

    private readonly List<ProfileCard> _profileCards = new();

    internal const int MaxProfiles = 5;
    private const int MinProfiles = 2;

    private const double ProfileColumnWidth = 284;
    private const double ProfileGap = 16;
    private const double ContentHorizontalMargin = 48;
    /// <summary>Должно совпадать с DockPanel Margin="24" вокруг блока с профилями — для расчёта Margin.Right у кнопки «+».</summary>
    private const double ProfileSectionDockPanelRightInset = 24;

    private const int TrayFlyDurationMs = 400;
    private const double TrayFlyEndScale = 0.14;

    /// <summary>Модель настроек, синхронная с главным окном (для диалога «Настройки» из трея).</summary>
    internal AppSettings SettingsModel => _settings;

    public MainWindow()
    {
        InitializeComponent();
        _tooltipDelayMs = CoerceTooltipDelayMs(TryFindResource("Rs.Tooltip.InitialShowDelayMs"));
        TooltipInitialShowDelayMs = _tooltipDelayMs;
        ReloadFromStorage();
        Loaded += MainWindow_OnLoaded;
        Closed += (_, _) => LocalizationService.LanguageChanged -= OnLocalizationLanguageChanged;
    }

    private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        LocalizationService.LanguageChanged += OnLocalizationLanguageChanged;
        // Совет при запуске (оверлей) — после компоновки главного окна; диалог askOnStart показывается в App до Show() главного окна.
        Dispatcher.BeginInvoke(() => AdviceService.TryScheduleStartupTip(this), DispatcherPriority.ApplicationIdle);
    }

    private void OnLocalizationLanguageChanged()
    {
        ApplyCloseButtonAppearance();
        BtnAddProfile.ToolTip = LocalizationService.T("MainWindow.AddProfile");
    }

    private static int CoerceTooltipDelayMs(object? r) => r switch
    {
        int i => i,
        null => 100,
        IConvertible c => Convert.ToInt32(c, CultureInfo.InvariantCulture),
        _ => 100
    };

    /// <summary>Совпадает с файлом и с галочкой «совет при запуске» в открытом окне настроек.</summary>
    internal void ApplyAdviceEnabledFromOverlay(bool enabled)
    {
        _settings.AdviceEnabled = enabled;
        foreach (System.Windows.Window w in System.Windows.Application.Current.Windows)
        {
            if (w is SettingsWindow sw)
                sw.SyncAdviceCheckboxesFromModel();
        }

        if (System.Windows.Application.Current is App app)
            app.RefreshTrayMenu();
    }

    public void ReloadFromStorage()
    {
        _settings = SettingsStorage.Load();
        LocalizationService.Apply(AppLanguageCatalog.Normalize(_settings.UiLanguage));
        ThemeService.Apply(_settings.UiTheme);

        RebuildProfileUi();
        ApplyCloseButtonAppearance();
        TooltipDelayHelper.Apply(this, _tooltipDelayMs);
        if (System.Windows.Application.Current is App app)
            app.RefreshTrayMenu();

        // До первого Show() задаём позицию синхронно — иначе окно мелькает в старой точке и «прыгает» после Loaded.
        if (!_centeredOnWorkAreaOnce)
        {
            CenterWindowOnWorkArea();
            ClampWindowToWorkArea();
            _centeredOnWorkAreaOnce = true;
        }
        else
        {
            ClampWindowToWorkArea();
        }
    }

    private void CenterWindowOnWorkArea()
    {
        var wa = SystemParameters.WorkArea;
        var w = Width;
        var h = Height;
        if (w <= 0 || h <= 0)
            return;

        Left = wa.Left + (wa.Width - w) / 2;
        Top = wa.Top + (wa.Height - h) / 2;
    }

    private void ClampWindowToWorkArea()
    {
        var wa = SystemParameters.WorkArea;
        var w = Width;
        var h = Height;
        if (w <= 0 || h <= 0)
            return;

        var maxLeft = wa.Left + wa.Width - w;
        var maxTop = wa.Top + wa.Height - h;
        if (Left < wa.Left) Left = wa.Left;
        if (Top < wa.Top) Top = wa.Top;
        if (Left > maxLeft) Left = maxLeft;
        if (Top > maxTop) Top = maxTop;
    }

    private void RebuildProfileUi()
    {
        ProfilesContainer.Children.Clear();
        _profileCards.Clear();

        for (var i = 0; i < _settings.Profiles.Count; i++)
        {
            var card = new ProfileCard(i, canRemove: i >= MinProfiles);
            card.Bind(_settings.Profiles[i]);
            var idx = i;
            card.ApplyClicked += (_, _) => OnApplyProfile(idx);
            card.GetFromWindowsClicked += (_, _) => OnGetFromWindows(idx);
            card.RemoveClicked += (_, _) => OnRemoveProfile(idx);
            if (i > 0)
                card.Margin = new Thickness(ProfileGap, 0, 0, 0);

            ProfilesContainer.Children.Add(card);
            _profileCards.Add(card);
        }

        BtnAddProfile.Visibility = _settings.Profiles.Count < MaxProfiles ? Visibility.Visible : Visibility.Collapsed;
        BtnAddProfile.ToolTip = LocalizationService.T("MainWindow.AddProfile");

        UpdateWindowWidthForProfileRow();
        ApplyAddButtonPlacement();
    }

    /// <summary>
    /// Горизонталь: ресурс <c>Rs.Profile.AddButton.DistanceFromWindowRightPx</c> — расстояние от правого края клиентской области окна до правого края кнопки (px).
    /// Вертикаль: центр кнопки по центру высоты Grid (как высота ряда карточек).
    /// </summary>
    private void ApplyAddButtonPlacement()
    {
        var fromWindowRight = 20d;
        var raw = TryFindResource("Rs.Profile.AddButton.DistanceFromWindowRightPx");
        if (raw is double d)
            fromWindowRight = d;
        else if (raw is IConvertible c)
            fromWindowRight = Convert.ToDouble(c, CultureInfo.InvariantCulture);

        var marginRight = fromWindowRight - ProfileSectionDockPanelRightInset;
        BtnAddProfile.Margin = new Thickness(0, 0, marginRight, 0);
    }

    private void UpdateWindowWidthForProfileRow()
    {
        var n = _settings.Profiles.Count;
        var inner = n * ProfileColumnWidth + (n > 0 ? (n - 1) * ProfileGap : 0);

        var w = ContentHorizontalMargin + inner + 4;

        MinWidth = w;
        Width = w;
    }

    private void BtnAddProfile_OnClick(object sender, RoutedEventArgs e) => OnAddProfile();

    private void OnAddProfile()
    {
        if (_settings.Profiles.Count >= MaxProfiles)
            return;

        var last = _settings.Profiles[^1];
        var nextNum = _settings.Profiles.Count + 1;
        _settings.Profiles.Add(new DisplayProfile
        {
            Name = LocalizationService.T("MainWindow.ProfileHeaderFormat", nextNum),
            Width = last.Width,
            Height = last.Height,
            RefreshRate = last.RefreshRate,
            BitsPerPixel = last.BitsPerPixel
        });
        SettingsStorage.Save(_settings);
        RebuildProfileUi();
        RefreshTrayMenuFromDisk();
    }

    private void OnRemoveProfile(int index)
    {
        if (index < MinProfiles || index >= _settings.Profiles.Count)
            return;

        _settings.Profiles.RemoveAt(index);
        SettingsStorage.Save(_settings);
        RebuildProfileUi();
        RefreshTrayMenuFromDisk();
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

    /// <summary>Считывает все профили с карточек в модель.</summary>
    internal bool TryCommitAllProfilesFromUi()
    {
        if (_profileCards.Count != _settings.Profiles.Count)
            return false;

        for (var i = 0; i < _profileCards.Count; i++)
        {
            if (!_profileCards[i].TryReadTo(_settings.Profiles[i], out var err))
            {
                MessageBox.Show(err, LocalizationService.T("Common.AppTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
        }

        return true;
    }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        if (!TryCommitAllProfilesFromUi())
            return;

        SettingsStorage.Save(_settings);
        RefreshTrayMenuFromDisk();
        ShowSaveSuccessFeedback();
    }

    private void ShowSaveSuccessFeedback()
    {
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
        SettingsWindow.ShowSingletonOrActivate(dlg =>
        {
            dlg.Owner = this;
            dlg.CenterOnWorkAreaAfterLoad = false;
            dlg.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            dlg.SyncProfilesFromMainWindow = () => TryCommitAllProfilesFromUi();
            dlg.LoadSettings(_settings);
        });
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

        App.TryShowAutostartPromptOnFirstExit();
        System.Windows.Application.Current.Shutdown();
    }

    private void OnGetFromWindows(int index)
    {
        if (index < 0 || index >= _profileCards.Count)
            return;

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
        _profileCards[index].BindDisplayMode(p);
    }

    private void OnApplyProfile(int index)
    {
        if (index < 0 || index >= _profileCards.Count)
            return;

        if (!_profileCards[index].TryReadTo(_settings.Profiles[index], out var err))
        {
            MessageBox.Show(err, LocalizationService.T("Common.AppTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SettingsStorage.Save(_settings);
        RefreshTrayMenuFromDisk();
        ResolutionSwitchCoordinator.ApplyProfile(this, index, _settings);
    }

    /// <summary>Меню трея читает профили из файла — обновляем после любой записи на диск.</summary>
    private static void RefreshTrayMenuFromDisk()
    {
        if (System.Windows.Application.Current is App app)
            app.RefreshTrayMenu();
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

        App.TryShowAutostartPromptOnFirstExit();
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
