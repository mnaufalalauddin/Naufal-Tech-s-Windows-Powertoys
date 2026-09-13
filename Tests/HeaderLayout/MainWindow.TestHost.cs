using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.Graphics;

namespace Naufal_Windows_Tech_s_Powertoys;

public sealed partial class MainWindow : Window
{
    private int _checks;
    private int _cases;
    private string _case = "initialization";
    public MainWindow()
    {
        InitializeComponent();
        Title = "Isolated header layout tests — no Windows changes";
        UiDisplaySettings.KeepRecoveryControlUsable(ThemeButton);
        UiDisplaySettings.KeepRecoveryControlUsable(TextScaleButton);
        UiDisplaySettings.KeepRecoveryControlUsable(TextScaleGlyph);
        LanguageComboBox.Items.Add(new ComboBoxItem { Content = "English", Tag = "ui-language:en" });
        LanguageComboBox.SelectedIndex = 0;
        UiTranslation.Observe(RootLayout);
        UiDisplaySettings.Changed += DisplayChanged;
        UiDisplaySettings.Apply(RootLayout);
        RootLayout.Loaded += Run;
        Closed += (_, _) => { UiDisplaySettings.Changed -= DisplayChanged; UiTranslation.Release(RootLayout); };
    }

    private void DisplayChanged(object? sender, EventArgs e)
    {
        UiDisplaySettings.Apply(RootLayout);
        UpdateHeaderLayout();
    }

    private async void Run(object sender, RoutedEventArgs e)
    {
        RootLayout.Loaded -= Run;
        try
        {
            _case = $"saved startup scale={UiDisplaySettings.TextScalePercent}";
            await Task.Delay(50);
            RootLayout.UpdateLayout();
            UpdateHeaderLayout();
            RootLayout.UpdateLayout();
            CheckHeader();
            int[] sequence = [25, 50, 75, 100, 125, 150, 175, 200, 175, 150, 125, 100, 75, 50, 25, 100];
            foreach (int width in new[] { 1920, 1280, 800, 480 })
            foreach (ElementTheme theme in new[] { ElementTheme.Light, ElementTheme.Dark })
            foreach (string language in new[] { "en", "de", "id", "ar" })
            {
                AppWindow.Resize(new SizeInt32(width, 800));
                if (UiDisplaySettings.Theme != theme) UiDisplaySettings.ToggleTheme();
                UiDisplaySettings.SetLanguage(language);
                foreach (int percent in sequence)
                {
                    _case = $"width={width}, theme={theme}, language={language}, scale={percent}";
                    UiDisplaySettings.SetTextScale(percent);
                    await Task.Delay(30);
                    RootLayout.UpdateLayout();
                    CheckHeader();
                    _cases++;
                }
            }
            UiDisplaySettings.SetLanguage("en");
            foreach (int percent in sequence.Take(8))
            {
                _case = $"flyout scale={percent}";
                UiDisplaySettings.SetTextScale(percent);
                await Task.Delay(30);
                RootLayout.UpdateLayout();
                TextScaleButton_Click(TextScaleButton, new RoutedEventArgs());
                await Task.Delay(50);
                var popup = (MenuFlyout)FlyoutBase.GetAttachedFlyout(TextScaleButton);
                Check(popup.IsOpen, "scaling flyout opens");
                Check(popup.Items.Count == 10, "all eight options, separator and reset available");
                Check(popup.Items.OfType<ToggleMenuFlyoutItem>().All(item => item.FontSize >= 14), "readable flyout");
                popup.Hide();
            }
            File.AppendAllText(App.ResultPath, $"PASS: {_checks} native WinUI assertions, {_cases} layout cases, 8 flyouts. No Windows settings changed.\n");
            Environment.ExitCode = 0;
        }
        catch (Exception exception)
        {
            File.AppendAllText(App.ResultPath, "FAIL: " + _case + "\n" + exception + "\n");
            Environment.ExitCode = 1;
        }
        finally { Close(); }
    }

    private void CheckHeader()
    {
        foreach (FrameworkElement control in new FrameworkElement[] { TextScaleButton, ThemeButton, LanguageComboBox })
        {
            Rect bounds = control.TransformToVisual(RootLayout).TransformBounds(new Rect(0, 0, control.ActualWidth, control.ActualHeight));
            Check(bounds.Left >= -0.5 && bounds.Right <= RootLayout.ActualWidth + 0.5,
                $"{control.Name} inside width {RootLayout.ActualWidth}: {bounds}");
            Check(bounds.Top >= 0 && bounds.Bottom <= RootLayout.ActualHeight, $"{control.Name} inside height");
            Check(control.ActualWidth > 0 && control.ActualHeight > 0, $"{control.Name} is not collapsed");
        }
        Check(TextScaleButton.ActualWidth >= 32 && TextScaleButton.ActualHeight >= 32, "recovery hit target at least 32 DIP");
        // Hit-test coordinates belong to the XamlRoot host, not the root Grid's
        // logical coordinate space (which is mirrored in Arabic/Urdu).
        Rect button = TextScaleButton.TransformToVisual(null).TransformBounds(new Rect(0, 0, TextScaleButton.ActualWidth, TextScaleButton.ActualHeight));
        Point center = new(button.X + button.Width / 2, button.Y + button.Height / 2);
        Check(VisualTreeHelper.FindElementsInHostCoordinates(center, RootLayout).Contains(TextScaleButton), "scaling button center is hit-testable");
    }

    private void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(_case + ": " + message);
        _checks++;
    }

    private void ThemeButton_Click(object s, RoutedEventArgs e) => UiDisplaySettings.ToggleTheme();
    private void LanguageComboBox_SelectionChanged(object s, SelectionChangedEventArgs e) { }
    // MainWindow.xaml is linked unchanged; all system-action handlers are inert
    // in this test-only host, so accidental input cannot launch a repair/tweak.
    private void TaskStatusButton_Click(object s, RoutedEventArgs e) { }
    private void RebootButton_Click(object s, RoutedEventArgs e) { }
    private void ExitButton_Click(object s, RoutedEventArgs e) { }
    private void FullRepairButton_Click(object s, RoutedEventArgs e) { }
    private void QuickRepairButton_Click(object s, RoutedEventArgs e) { }
    private void WindowsUpdateFixButton_Click(object s, RoutedEventArgs e) { }
    private void MicrosoftStoreFixButton_Click(object s, RoutedEventArgs e) { }
    private void ExplorerFixButton_Click(object s, RoutedEventArgs e) { }
    private void DiskInfoButton_Click(object s, RoutedEventArgs e) { }
    private void SystemReportButton_Click(object s, RoutedEventArgs e) { }
    private void WindowsActivationButton_Click(object s, RoutedEventArgs e) { }
    private void OfficeActivationButton_Click(object s, RoutedEventArgs e) { }
    private void DisableDefenderButton_Click(object s, RoutedEventArgs e) { }
    private void RestoreDefenderButton_Click(object s, RoutedEventArgs e) { }
    private void BitLockerManagerButton_Click(object s, RoutedEventArgs e) { }
    private void SmartAppControlButton_Click(object s, RoutedEventArgs e) { }
    private void EssentialTweaksButton_Click(object s, RoutedEventArgs e) { }
    private void GamingTweaksButton_Click(object s, RoutedEventArgs e) { }
    private void RuntimeCompatibilityButton_Click(object s, RoutedEventArgs e) { }
    private void GpuDriverManagerButton_Click(object s, RoutedEventArgs e) { }
    private void DebloatButton_Click(object s, RoutedEventArgs e) { }
    private void MsiModeUtilityButton_Click(object s, RoutedEventArgs e) { }
    private void LegacyWindowsPanelsButton_Click(object s, RoutedEventArgs e) { }
    private void ProfileSelectionButton_Click(object s, RoutedEventArgs e) { }
    private void ApplyProfileButton_Click(object s, RoutedEventArgs e) { }
}
