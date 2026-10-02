using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace Naufal_Windows_Tech_s_Powertoys;

public sealed partial class MainWindow
{
    private bool _securityEvidenceInitialized;
    private static readonly BoundedReadProbe<SecurityMitigationSnapshot> SecurityMitigationProbe = new();

    private async void SecurityMitigations_Click(object sender, RoutedEventArgs args)
    {
        if (_securityEvidenceInitialized) return;
        _securityEvidenceInitialized = true;
        SecurityEvidencePanel.Children.Clear();
        TextBlock status = new() { TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };
        TextBlock report = new() { TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };
        Button reload = new() { Content = "Reload security status", HorizontalAlignment = HorizontalAlignment.Stretch };
        Button windowsSecurity = new() { Content = "Open Windows Security", HorizontalAlignment = HorizontalAlignment.Stretch };
        TextBlock notice = new() { Text = "HVCI / Memory integrity is read-only in this utility. Use Windows Security to review its configuration and driver compatibility. Existing recovery snapshots are retained.", TextWrapping = TextWrapping.Wrap };
        TextBlock lsaNotice = new() { Text = "LSA: Windows 11 22H2+ only. Enabling can block incompatible sign-in plug-ins. The original configuration is saved, but automated LSA Disable/Restore is unavailable because registry evidence cannot exclude a previous firmware lock.", TextWrapping = TextWrapping.Wrap };
        Button enableLsa = new() { Content = "Enable LSA protection without adding a firmware lock", HorizontalAlignment = HorizontalAlignment.Stretch, IsEnabled = false };
        TextBlock lsaReason = new() { TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };
        Button copy = new() { Content = "Copy security report", HorizontalAlignment = HorizontalAlignment.Stretch, IsEnabled = false };
        Button save = new() { Content = "Save security report as TXT", HorizontalAlignment = HorizontalAlignment.Stretch, IsEnabled = false };
        ScrollViewer reportViewer = new()
        {
            Content = report, MaxHeight = 420, IsTabStop = true,
            VerticalScrollMode = ScrollMode.Enabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollMode = ScrollMode.Disabled, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        foreach (UIElement item in new UIElement[] { notice, status, reload, windowsSecurity, lsaNotice, enableLsa, lsaReason, copy, save, reportViewer })
            SecurityEvidencePanel.Children.Add(item);
        UiDisplaySettings.Apply(SecurityEvidencePanel);
        SecurityMitigationNative platform = new();
        SecurityMitigationFileStore lsaStore = new(Path.Combine(AppDataPaths.LocalRoot, "Backups", "SecurityMitigations", "lsa-protection.json"), "LsaProtection");
        SecurityMitigationSnapshot? state = null;
        bool busy = false;
        reload.Click += async (_, _) => await Reload();
        windowsSecurity.Click += async (_, _) =>
        {
            try { if (!await Windows.System.Launcher.LaunchUriAsync(new Uri("windowsdefender:"))) status.Text = "Open Windows Security manually from Start."; }
            catch (Exception ex) { status.Text = "Windows Security launch failed: " + ex.Message; }
        };
        copy.Click += (_, _) =>
        {
            try { DataPackage data = new(); data.SetText(report.Text); Clipboard.SetContent(data); status.Text = "Report copied."; }
            catch (Exception ex) { status.Text = "Copy failed: " + ex.Message; }
        };
        save.Click += async (_, _) =>
        {
            if (busy) return;
            busy = true; Buttons();
            try { await DesktopReportExport.SaveAsync(this, "Security-Status", report.Text); }
            catch (Exception ex) { if (!_isClosed) status.Text = "Save failed: " + ex.Message; }
            finally { busy = false; Buttons(); }
        };
        enableLsa.Click += async (_, _) => await EnableLsa();
        await Reload();

        void Buttons()
        {
            if (_isClosed) return;
            reload.IsEnabled = windowsSecurity.IsEnabled = !busy;
            copy.IsEnabled = save.IsEnabled = !busy && state is not null;
            string reason = busy ? "Operation in progress." : state is null ? "Reload current evidence first." : LsaProtectionPolicy.BlockReason(state);
            enableLsa.IsEnabled = reason.Length == 0;
            lsaReason.Text = reason.Length == 0 ? "Available after confirmation and a fresh eligibility check." : "Unavailable: " + reason;
        }
        async Task Reload()
        {
            if (busy || _isClosed) return;
            busy = true; state = null; status.Text = "Reading security status..."; Buttons();
            try
            {
                var snapshot = await SecurityMitigationProbe.ReadAsync(platform.Read, TimeSpan.FromSeconds(25));
                if (_isClosed) return;
                state = snapshot;
                report.Text = SecurityMitigationPolicy.Report(snapshot, includeHvciControls: false);
                var backup = lsaStore.Load();
                if (backup is not null) report.Text += "\nLSA snapshot retained: " + backup.Outcome + ". Not a claim of effective rollback.\n";
                status.Text = "Read-only scan complete. Configuration and running protection are separate. Reload after restarting.";
            }
            catch (Exception ex) { if (!_isClosed) { state = null; report.Text = "Current evidence unavailable."; status.Text = "Scan failed: " + ex.Message; } }
            finally { busy = false; Buttons(); }
        }
        async Task EnableLsa()
        {
            if (busy || state is null || _isClosed) return;
            busy = true; Buttons();
            TaskActivityService.TaskActivityLease? lease = null;
            try
            {
                if (!await ShowConfirmationWindowAsync("Enable LSA protection", "This enables LSASS protection without adding a firmware lock. Incompatible authentication plug-ins can affect sign-in. Review CodeIntegrity compatibility events first. The original RunAsPPL value is saved, but automatic LSA rollback is NOT available; administrator recovery may be required.\n\nOnly LSA protection will change. No HVCI, firmware, TPM, Secure Boot, BCD or other protection is changed. Restart is required. Continue?", "Enable LSA protection")) return;
                lease = await AcquireManagedTaskAsync("SecurityMitigations:LsaProtection", "Enable LSA protection", new[] { "SystemMutation", "WindowsSecurity", "SecurityMitigations" });
                if (lease is null) return;
                CatalogStateEpoch.Invalidate();
                SecurityMitigationResult result;
                try { result = await Task.Run(() => LsaProtectionPolicy.Enable(platform, lsaStore)); }
                finally { CatalogStateEpoch.Invalidate(); }
                state = null;
                if (!_isClosed) status.Text = result.Outcome + ": " + result.Message + " Reload required.";
                lease.Complete(result.Verified ? "COMPLETED" : "WARNING", result.Message);
                if (result.Verified && result.Changed) _pendingRestarts.Add("Enable LSA protection");
            }
            catch (Exception ex) { state = null; if (!_isClosed) status.Text = "Operation unverified: " + ex.Message; lease?.Complete("FAILED", ex.Message); }
            finally { lease?.Dispose(); RefreshManagedTaskHeader(); busy = false; Buttons(); }
            if (!_isClosed) await OfferPendingRestartAsync();
        }
    }
}
