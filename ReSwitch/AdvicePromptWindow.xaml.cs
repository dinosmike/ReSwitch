using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using ReSwitch.Services;

namespace ReSwitch;

public partial class AdvicePromptWindow
{
    public AdvicePromptWindow()
    {
        InitializeComponent();
    }

    private void TitleBar_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void AdvicePromptWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        Owner?.Activate();
        Activate();
        // Снятие «прозрачного» HWND поверх других окон без постоянного Topmost.
        Dispatcher.BeginInvoke(
            () =>
            {
                Topmost = true;
                Topmost = false;
                Activate();
                var h = new WindowInteropHelper(this).Handle;
                if (h != IntPtr.Zero)
                    ForegroundWindow.SetForegroundWindow(h);
            },
            DispatcherPriority.ApplicationIdle);
    }

    /// <summary>Включает показ советов; главное окно откроется после закрытия диалога в любом случае (логика в App.OnStartup).</summary>
    private void Yes_OnClick(object sender, RoutedEventArgs e)
    {
        // TODO: когда API цензурной версии заработает — вернуть шаг 2:
        // Step1Panel.Visibility = Visibility.Collapsed;
        // Step2Panel.Visibility = Visibility.Visible;
        var s = SettingsStorage.Load();
        s.AdviceEnabled = true;
        s.AdviceAskOnStart = false;
        SettingsStorage.Save(s);
        Close();
    }

    /// <summary>Отключает советы; запуск приложения продолжается так же, как после «Да».</summary>
    private void No_OnClick(object sender, RoutedEventArgs e)
    {
        var s = SettingsStorage.Load();
        s.AdviceEnabled = false;
        s.AdviceAskOnStart = false;
        SettingsStorage.Save(s);
        Close();
    }

    private void OkStep2_OnClick(object sender, RoutedEventArgs e)
    {
        var s = SettingsStorage.Load();
        s.AdviceEnabled = true;
        s.AdviceAskOnStart = false;
        SettingsStorage.Save(s);
        Close();
    }

    private static class ForegroundWindow
    {
        [DllImport("user32.dll")]
        internal static extern bool SetForegroundWindow(IntPtr hWnd);
    }
}
