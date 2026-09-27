using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics;
using Windows.Graphics.Imaging;

namespace Naufal_Windows_Tech_s_Powertoys;

public sealed partial class MainWindow
{
    private async Task CheckDiskDashboardAsync()
    {
        SystemReportEntry[] rows =
        [
            new("=== LOGICAL DRIVE ===", "", true),
            new("C:", "Existing volume backend — 150 GB free / 500 GB", false),
            new("Storage device 0", "", true),
            new("Device name", "TEST FIXTURE — SATA SSD (not actual hardware)", false),
            new("Windows health (not a full SMART test)", "Healthy", false),
            new("Disk health", "Good — 90% estimated endurance remaining", false),
            new("Temperature", "29 °C", false),
            new("Firmware", "TEST01", false),
            new("Serial number", "SYNTHETIC-ONLY", false),
            new("Bus / interface", "SATA", false),
            new("Capacity", "500.00 GB (decimal)", false),
            new("Power-on hours", "402 hours", false),
            new("Wear consumed (100% = estimated wear limit)", "73%", false),
            new("Legacy SMART device TEST-PROVIDER-INSTANCE", "", true),
            new("Provider instance", "TEST-PROVIDER-INSTANCE", false),
            new("0x09", "Current=100; Worst=90; Threshold=0; Raw=000000000192", false,
                Smart: new(9, 100, 90, 0, "000000000192")),
            new("Storage device 1", "", true),
            new("Device name", "TEST FIXTURE — unsupported USB bridge", false),
            new("S.M.A.R.T. counters", "Not reported", false)
        ];
        var view = new DiskInfoView(rows) { Margin = new Thickness(16) };
        Grid root = new() { Background = new SolidColorBrush(Microsoft.UI.Colors.White) };
        root.Children.Add(view);
        Window window = new() { Content = root, Title = "Disk Info layout test — synthetic data only" };
        window.Activate();
        try
        {
            foreach (int width in new[] { 620, 1100 })
            foreach (int scale in new[] { 100, 200 })
            foreach (ElementTheme theme in new[] { ElementTheme.Light, ElementTheme.Dark })
            {
                _case = $"Disk Info width={width}, scale={scale}, theme={theme}";
                UiDisplaySettings.SetTextScale(scale);
                if (UiDisplaySettings.Theme != theme) UiDisplaySettings.ToggleTheme();
                window.AppWindow.Resize(new SizeInt32(width, 800));
                UiDisplaySettings.Apply(root);
                await Task.Delay(50);
                root.UpdateLayout();
                for (int i = 0; i < view.PageCount; i++)
                {
                    var button = view.SelectorButtons[i];
                    Check(button.Content is FrameworkElement label && !label.IsHitTestVisible, "disk label does not intercept pointer presses");
                    var peer = new Microsoft.UI.Xaml.Automation.Peers.ButtonAutomationPeer(button);
                    ((Microsoft.UI.Xaml.Automation.Provider.IInvokeProvider)peer.GetPattern(Microsoft.UI.Xaml.Automation.Peers.PatternInterface.Invoke)).Invoke();
                    await Task.Delay(35);
                    root.UpdateLayout();
                    Check(view.SelectedIndex == i, "disk tab selection updates page");
                    Check(!VisualDescendants<TextBlock>(view).Any(text => text.Text == "WINDOWS HEALTH"), "health card is not Windows status");
                    Check(view.ActualWidth <= root.ActualWidth && view.ActualWidth > 0, "disk view fits window");
                    Check(VisualDescendants<TextBlock>(view).Any(text => text.Text == view.SelectedTitle), "selected disk heading is visible");
                    Check(VisualDescendants<ScrollViewer>(view).Any(scroll => scroll.VerticalScrollBarVisibility == ScrollBarVisibility.Auto), "disk details remain vertically scrollable");
                }
                if (width == 1100 && scale == 100)
                {
                    view.SelectPage(0);
                    await Task.Delay(80);
                    await SaveDiskPreviewAsync(root, "disk-info-" + theme.ToString().ToLowerInvariant() + ".png");
                    view.SelectPage(1);
                    await Task.Delay(80);
                    await SaveDiskPreviewAsync(root, "disk-smart-attributes-" + theme.ToString().ToLowerInvariant() + ".png");
                }
            }
        }
        finally { window.Close(); }
    }

    private static async Task SaveDiskPreviewAsync(FrameworkElement root, string name)
    {
        RenderTargetBitmap bitmap = new();
        await bitmap.RenderAsync(root);
        byte[] pixels = (await bitmap.GetPixelsAsync()).ToArray();
        using var file = File.Create(Path.Combine(AppContext.BaseDirectory, name));
        using var stream = file.AsRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
            (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight, 96, 96, pixels);
        await encoder.FlushAsync();
    }
}
