using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace Naufal_Windows_Tech_s_Powertoys;

public sealed partial class MainWindow
{
    private ToolWindow? _resourceAnalyzerWindow;
    private ResourceMeasurement? _resourceBaseline;

    private async void ResourceAnalyzer_Click(object sender, RoutedEventArgs args)
    {
        if (_resourceAnalyzerWindow is { IsClosed: false }) { _resourceAnalyzerWindow.Activate(); return; }
        TextBox workload = new() { Header = "Workload / conditions (keep the same for both runs)", Text = _resourceBaseline?.Workload ?? "Idle desktop; describe open apps and power source", TextWrapping = TextWrapping.Wrap };
        TextBox actions = new() { Header = "Actions / profile since baseline (your annotation)", PlaceholderText = "Example: baseline, or exact actions applied manually", TextWrapping = TextWrapping.Wrap };
        ComboBox reboot = new() { Header = "Reboot status (your annotation)", HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (string value in new[] { "Unknown / not checked", "No reboot since baseline", "Reboot required / pending", "Reboot completed" }) reboot.Items.Add(value);
        reboot.SelectedIndex = 0;
        Button baseline = new() { Content = "Capture baseline (10 samples)", HorizontalAlignment = HorizontalAlignment.Stretch };
        Button after = new() { Content = "Capture after / compare (10 samples)", IsEnabled = _resourceBaseline is not null, HorizontalAlignment = HorizontalAlignment.Stretch };
        TextBlock status = new() { Text = "Read-only. Sampling runs only when requested. No tweaks are applied.", TextWrapping = TextWrapping.Wrap };
        TextBlock report = new() { Text = _resourceBaseline is null ? "No baseline captured in this application session." : ResourceMeasurements.Report(_resourceBaseline), TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };
        StackPanel panel = new() { Spacing = 12 };
        panel.Children.Add(status); panel.Children.Add(workload); panel.Children.Add(actions); panel.Children.Add(reboot);
        panel.Children.Add(baseline); panel.Children.Add(after); panel.Children.Add(report);
        ToolWindow window = new(this, "Resource Analyzer — Before / After", new ScrollViewer { Content = panel },
            primaryButtonText: "Copy", secondaryButtonText: "Save TXT", initialWidth: 1000, initialHeight: 760);
        _resourceAnalyzerWindow = window;
        using CancellationTokenSource lifetime = new();
        bool busy = false;
        bool saving = false;
        Task capture = Task.CompletedTask;
        window.IsBusy = () => saving; // Sampling can be cancelled by closing the window.
        window.Closed += (_, _) => lifetime.Cancel();
        window.PrimaryButton.IsEnabled = _resourceBaseline is not null;
        window.SecondaryButton.IsEnabled = _resourceBaseline is not null;
        baseline.Click += (_, _) => { if (!busy) capture = Capture(false); };
        after.Click += (_, _) => { if (!busy && _resourceBaseline is not null) capture = Capture(true); };
        window.PrimaryButton.Click += (_, _) =>
        {
            try { DataPackage data = new(); data.SetText(report.Text); Clipboard.SetContent(data); status.Text = "Copied measurement report."; }
            catch (Exception ex) { status.Text = "Copy failed: " + ex.Message; }
        };
        window.SecondaryButton.Click += async (_, _) =>
        {
            if (saving || busy) return;
            saving = true;
            SetButtons(false);
            try
            {
                string? path = await DesktopReportExport.SaveAsync(window, "Resource-Measurement", report.Text);
                if (!window.IsClosed && path is not null) status.Text = "Saved: " + path;
            }
            catch (Exception ex) { if (!window.IsClosed) status.Text = "Save failed: " + ex.Message; }
            finally { saving = false; if (!window.IsClosed) SetButtons(true); }
        };
        try { await window.ShowAsync(); }
        finally { lifetime.Cancel(); await capture; if (ReferenceEquals(_resourceAnalyzerWindow, window)) _resourceAnalyzerWindow = null; }

        void SetButtons(bool enabled)
        {
            baseline.IsEnabled = enabled;
            after.IsEnabled = enabled && _resourceBaseline is not null;
            workload.IsEnabled = actions.IsEnabled = reboot.IsEnabled = enabled;
            window.PrimaryButton.IsEnabled = window.SecondaryButton.IsEnabled = enabled && _resourceBaseline is not null;
        }

        async Task Capture(bool compare)
        {
            busy = true;
            SetButtons(false);
            string conditions = workload.Text.Trim(), changes = actions.Text.Trim();
            string rebootNote = reboot.SelectedItem?.ToString() ?? "Unknown";
            try
            {
                status.Text = "Sampling 0/10. Keep this workload steady; closing this window cancels the capture.";
                Progress<int> progress = new(count => { if (!window.IsClosed) status.Text = $"Sampling {count}/10..."; });
                ResourceMeasurement run = await Task.Run(async () =>
                {
                    using var probe = new NativeResourceProbe();
                    return await ResourceMeasurements.CaptureAsync(probe, conditions, changes, rebootNote, lifetime.Token, progress);
                }, lifetime.Token);
                if (window.IsClosed) return;
                if (!compare) _resourceBaseline = run;
                report.Text = ResourceMeasurements.Report(_resourceBaseline!, compare ? run : null);
                status.Text = "Capture complete. Review valid sample counts and context. Save TXT retains the complete samples; session baseline is not persisted across application restart.";
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { if (!window.IsClosed) status.Text = "Capture failed; previous baseline retained. " + ex.Message; }
            finally { busy = false; if (!window.IsClosed) SetButtons(true); }
        }
    }
}
