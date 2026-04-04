using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ReSwitch.Models;
using ReSwitch.Services;

namespace ReSwitch;

public partial class AdviceOverlayWindow
{
    /// <summary>Закрыть все открытые оверлеи советов перед показом нового.</summary>
    public static void CloseAllOpen()
    {
        var app = System.Windows.Application.Current;
        if (app == null)
            return;
        var toClose = new List<AdviceOverlayWindow>();
        foreach (Window w in app.Windows)
        {
            if (w is AdviceOverlayWindow o)
                toClose.Add(o);
        }

        foreach (var o in toClose)
            o.CloseInstant();
    }

    /// <summary>Сдвиг оверлея вверх относительно расчётной позиции (логические px WPF).</summary>
    private const double VerticalOffsetUpPx = 15;

    /// <summary>Сдвиг оверлея влево относительно расчётной позиции (логические px WPF).</summary>
    private const double HorizontalOffsetLeftPx = 20;

    private readonly AppSettings _settings;
    private readonly bool _openedFromTray;
    private DateTime? _hoverFadeNotBeforeUtc;
    private bool _fadeOutStarted;
    private bool _contextMenuOpen;

    public AdviceOverlayWindow(string adviceText, AppSettings settings, bool openedFromTray = false)
    {
        InitializeComponent();
        _settings = settings;
        _openedFromTray = openedFromTray;
        var h = AdviceSettings.Default;

        TipText.Text = FormatAdviceText(adviceText, h);
        TipText.FontFamily = AdviceFontResolver.Resolve(h.FontFamily);
        TipText.FontWeight = FontWeights.Bold;
        TipText.FontSize = h.FontSizePx;
        TipText.Foreground = ParseBrush(h.ForegroundHex);
        TipText.TextAlignment = ParseTextAlignment(h.TextHorizontalAlignment);

        Opacity = 0;
        MouseEnter += OnMouseEnter;
        RootBorder.MouseEnter += OnMouseEnter;
        TipText.MouseEnter += OnMouseEnter;
        PreviewMouseLeftButtonDown += (_, _) =>
        {
            if (_contextMenuOpen)
                return;
            CloseInstant();
        };
        MouseRightButtonDown += OnMouseRightButtonDown;

        Loaded += (_, _) =>
        {
            PositionOnPrimaryScreen(h);
            BeginFadeIn(h);
            if (_openedFromTray)
            {
                var cooldown = Math.Max(0, AdviceSettings.Default.TrayOpenHoverFadeCooldownMs);
                _hoverFadeNotBeforeUtc = DateTime.UtcNow.AddMilliseconds(cooldown);
            }
        };
    }

    private static string FormatAdviceText(string adviceText, AdviceSettings h)
    {
        if (h.DisplayUppercase == false)
            return adviceText;
        return adviceText.ToUpperInvariant();
    }

    private static System.Windows.Media.Brush ParseBrush(string hex)
    {
        try
        {
            var o = System.Windows.Media.ColorConverter.ConvertFromString(hex.Trim());
            if (o is System.Windows.Media.Color c)
                return new SolidColorBrush(c);
        }
        catch
        {
            // ignored
        }

        return new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xD3, 0xD3, 0xD3));
    }

    private static TextAlignment ParseTextAlignment(string? s)
    {
        return s?.Trim().ToLowerInvariant() switch
        {
            "left" => TextAlignment.Left,
            "center" => TextAlignment.Center,
            _ => TextAlignment.Right
        };
    }

    /// <summary>
    /// Границы основного монитора в логических единицах WPF (как <see cref="Window.Left"/> / <see cref="Window.Top"/>).
    /// Берём полный прямоугольник экрана, включая область панели задач — отступы считаются от физического края.
    /// </summary>
    private bool TryGetPrimaryScreenLogicalRect(out Rect screen)
    {
        screen = default;
        if (Screen.PrimaryScreen == null)
            return false;

        var b = Screen.PrimaryScreen.Bounds;
        var source = PresentationSource.FromVisual(this);
        var m = source?.CompositionTarget?.TransformFromDevice;
        if (m != null)
        {
            var tl = m.Value.Transform(new System.Windows.Point(b.Left, b.Top));
            var br = m.Value.Transform(new System.Windows.Point(b.Right, b.Bottom));
            screen = new Rect(tl, br);
            return true;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        screen = new Rect(
            b.Left / dpi.DpiScaleX,
            b.Top / dpi.DpiScaleY,
            b.Width / dpi.DpiScaleX,
            b.Height / dpi.DpiScaleY);
        return true;
    }

    private void PositionOnPrimaryScreen(AdviceSettings h)
    {
        UpdateLayout();
        var aw = ActualWidth;
        var ah = ActualHeight;
        if (aw <= 0 || ah <= 0)
            return;

        if (!TryGetPrimaryScreenLogicalRect(out var screen))
            screen = SystemParameters.WorkArea;

        var corner = (h.ScreenCorner ?? "BottomRight").Trim();
        switch (corner.ToLowerInvariant())
        {
            case "bottomleft":
                Left = screen.Left + h.MarginLeft;
                Top = screen.Bottom - h.MarginBottom - ah;
                break;
            case "topright":
                Left = screen.Right - h.MarginRight - aw;
                Top = screen.Top + h.MarginTop;
                break;
            case "topleft":
                Left = screen.Left + h.MarginLeft;
                Top = screen.Top + h.MarginTop;
                break;
            case "bottomcenter":
                Left = screen.Left + (screen.Width - aw) / 2;
                Top = screen.Bottom - h.MarginBottom - ah;
                break;
            case "topcenter":
                Left = screen.Left + (screen.Width - aw) / 2;
                Top = screen.Top + h.MarginTop;
                break;
            default:
                Left = screen.Right - h.MarginRight - aw;
                Top = screen.Bottom - h.MarginBottom - ah;
                break;
        }

        Top -= VerticalOffsetUpPx;
        Left -= HorizontalOffsetLeftPx;
    }

    private void BeginFadeIn(AdviceSettings h)
    {
        var ms = Math.Max(0, h.FadeInDurationMs);
        if (ms == 0)
        {
            Opacity = 1;
            return;
        }

        var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(ms))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        BeginAnimation(OpacityProperty, anim);
    }

    private void OnMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_fadeOutStarted || _contextMenuOpen)
            return;
        if (_hoverFadeNotBeforeUtc is { } notBefore && DateTime.UtcNow < notBefore)
            return;

        _fadeOutStarted = true;
        BeginAnimation(OpacityProperty, null);
        Opacity = 1;

        var h = AdviceSettings.Default;
        var ms = Math.Max(100, h.HoverFadeOutDurationMs);
        var anim = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(ms))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        anim.Completed += (_, _) =>
        {
            if (!_contextMenuOpen)
                Close();
        };
        BeginAnimation(OpacityProperty, anim);
    }

    private void CloseInstant()
    {
        BeginAnimation(OpacityProperty, null);
        Close();
    }

    private void OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        BeginAnimation(OpacityProperty, null);
        Opacity = 1;
        _fadeOutStarted = false;

        var bg = System.Windows.Application.Current?.TryFindResource("Rs.Surface") as System.Windows.Media.Brush
                   ?? new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x16, 0x1B, 0x22));
        var fg = System.Windows.Application.Current?.TryFindResource("Rs.Text.Primary") as System.Windows.Media.Brush
                 ?? new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE6, 0xED, 0xF3));
        var lineBrush = System.Windows.Application.Current?.TryFindResource("Rs.Border") as System.Windows.Media.Brush
                        ?? new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x30, 0x36, 0x3D));
        var hoverBg = System.Windows.Application.Current?.TryFindResource("Rs.TitleBar.ButtonHoverBackground") as System.Windows.Media.Brush
                      ?? new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2D, 0x33, 0x3B));

        var line = new TextBlock
        {
            Text = "Больше не показывать советы",
            Foreground = fg,
            FontSize = 13
        };

        var box = new Border
        {
            Background = bg,
            BorderBrush = lineBrush,
            BorderThickness = new Thickness(1, 1, 1, 1),
            Padding = new Thickness(12, 8, 12, 8),
            Child = line,
            Cursor = System.Windows.Input.Cursors.Hand
        };
        box.MouseEnter += (_, _) => { box.Background = hoverBg; };
        box.MouseLeave += (_, _) => { box.Background = bg; };

        var popup = new Popup
        {
            PlacementTarget = this,
            Placement = PlacementMode.MousePoint,
            StaysOpen = false,
            AllowsTransparency = false,
            Child = box,
            PopupAnimation = PopupAnimation.None
        };

        _contextMenuOpen = true;
        popup.Closed += (_, _) =>
        {
            _contextMenuOpen = false;
            _fadeOutStarted = false;
        };

        box.MouseLeftButtonDown += (_, ev) =>
        {
            ev.Handled = true;
            popup.IsOpen = false;
            var cur = SettingsStorage.Load();
            cur.AdviceEnabled = false;
            SettingsStorage.Save(cur);
            if (System.Windows.Application.Current?.MainWindow is MainWindow mw)
                mw.ApplyAdviceEnabledFromOverlay(false);
            CloseInstant();
        };

        popup.IsOpen = true;
    }
}
