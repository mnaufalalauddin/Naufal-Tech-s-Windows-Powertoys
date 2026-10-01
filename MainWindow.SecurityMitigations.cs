using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace Naufal_Windows_Tech_s_Powertoys;

public sealed partial class MainWindow
{
    private ToolWindow? _securityMitigationsWindow;
    private static readonly BoundedReadProbe<SecurityMitigationSnapshot> SecurityMitigationProbe = new();

    private async void SecurityMitigations_Click(object sender, RoutedEventArgs args)
    {
        if (_securityMitigationsWindow is { IsClosed: false }) { _securityMitigationsWindow.Activate(); return; }
        TextBlock status = new() { Text = "Read-only scan on demand. No protection is changed until you choose and confirm an individual action.", TextWrapping = TextWrapping.Wrap };
        TextBlock report = new() { Text = "Loading protection evidence...", TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };
        Button reload = new() { Content = "Reload configured / effective state", HorizontalAlignment = HorizontalAlignment.Stretch };
        Button enable = new() { Content = "Enable Memory integrity (HVCI)", HorizontalAlignment = HorizontalAlignment.Stretch, IsEnabled = false };
        Button disable = new() { Content = "Disable Memory integrity (HVCI) — reduces protection", HorizontalAlignment = HorizontalAlignment.Stretch, IsEnabled = false };
        Button restore = new() { Content = "Restore exact Memory integrity snapshot", HorizontalAlignment = HorizontalAlignment.Stretch, IsEnabled = false };
        Button enableLsa = new() { Content = "Enable LSA protection without adding a firmware lock", HorizontalAlignment = HorizontalAlignment.Stretch, IsEnabled = false };
        TextBlock lsaNotice = new() { Text = "LSA: Windows 11 22H2+ only. Enabling can block incompatible sign-in plug-ins. The original configuration is saved, but automated LSA Disable/Restore is unavailable because a registry value cannot prove a previous firmware lock is absent.", TextWrapping = TextWrapping.Wrap };
        Button windowsSecurity = new() { Content = "Open Windows Security — review compatibility / other protections", HorizontalAlignment = HorizontalAlignment.Stretch };
        StackPanel panel = new() { Spacing = 12 };
        foreach (UIElement element in new UIElement[] { status, reload, windowsSecurity, enable, disable, restore, lsaNotice, enableLsa, report }) panel.Children.Add(element);
        ToolWindow window = new(this, "Security & Mitigations", new ScrollViewer { Content = panel },
            primaryButtonText: "Copy", secondaryButtonText: "Save TXT", initialWidth: 1050, initialHeight: 800);
        _securityMitigationsWindow = window;
        SecurityMitigationNative platform = new();
        SecurityMitigationFileStore store = new(Path.Combine(AppDataPaths.LocalRoot, "Backups", "SecurityMitigations", "memory-integrity.json"));
        SecurityMitigationFileStore lsaStore = new(Path.Combine(AppDataPaths.LocalRoot, "Backups", "SecurityMitigations", "lsa-protection.json"), "LsaProtection");
        bool busy = false;
        SecurityMitigationSnapshot? state = null;
        window.IsBusy = () => busy;
        reload.Click += async (_, _) => await Reload();
        enable.Click += async (_, _) => await Change(SecurityMitigationAction.EnableMemoryIntegrity);
        disable.Click += async (_, _) => await Change(SecurityMitigationAction.DisableMemoryIntegrity);
        restore.Click += async (_, _) => await Change(SecurityMitigationAction.RestoreMemoryIntegrity);
        enableLsa.Click += async (_, _) => await Change(SecurityMitigationAction.EnableMemoryIntegrity, lsa: true);
        windowsSecurity.Click += async (_, _) =>
        {
            try { if (!await Windows.System.Launcher.LaunchUriAsync(new Uri("windowsdefender:"))) status.Text = "Windows Security could not be opened. Open it from Start manually."; }
            catch (Exception ex) { status.Text = "Windows Security launch failed: " + ex.Message; }
        };
        window.PrimaryButton.Click += (_, _) =>
        {
            try { DataPackage data = new(); data.SetText(report.Text); Clipboard.SetContent(data); status.Text = "Copied protection evidence."; }
            catch (Exception ex) { status.Text = "Copy failed: " + ex.Message; }
        };
        window.SecondaryButton.Click += async (_, _) =>
        {
            if (busy) return;
            busy = true; Buttons();
            try { await DesktopReportExport.SaveAsync(window, "Security-Mitigations", report.Text); }
            catch (Exception ex) { status.Text = "Save failed: " + ex.Message; }
            finally { busy = false; Buttons(); }
        };
        try
        {
            Task<ToolWindowResult> shown = window.ShowAsync();
            await Reload();
            await shown;
        }
        finally { if (ReferenceEquals(_securityMitigationsWindow, window)) _securityMitigationsWindow = null; }
        await OfferPendingRestartAsync();

        void Buttons()
        {
            if (window.IsClosed) return;
            reload.IsEnabled = windowsSecurity.IsEnabled = !busy;
            window.PrimaryButton.IsEnabled = window.SecondaryButton.IsEnabled = !busy && state is not null;
            bool eligible = !busy && state is not null && SecurityMitigationPolicy.BlockReason(state, false).Length == 0;
            enable.IsEnabled = eligible && SecurityMitigationPolicy.BlockReason(state!, true).Length == 0;
            disable.IsEnabled = restore.IsEnabled = eligible;
            enableLsa.IsEnabled = !busy && state is not null && LsaProtectionPolicy.BlockReason(state).Length == 0;
        }
        async Task Reload()
        {
            if (busy || window.IsClosed) return;
            busy = true; state = null; Buttons();
            try
            {
                state = await SecurityMitigationProbe.ReadAsync(platform.Read, TimeSpan.FromSeconds(25));
                report.Text = SecurityMitigationPolicy.Report(state);
                var backup = store.Load();
                if (backup is not null) report.Text += "\nSnapshot outcome: " + backup.Outcome + "; original " + (backup.OriginalValue?.ToString() ?? "value absent") + ".\n";
                var lsaBackup = lsaStore.Load();
                if (lsaBackup is not null) report.Text += "\nLSA original snapshot: " + (lsaBackup.OriginalValue?.ToString() ?? "value absent") + "; outcome " + lsaBackup.Outcome + ". Retained for manual recovery, not a claim of effective rollback.\n";
                status.Text = "Read-only scan complete. Configuration and effective protection are separate. Reload after restarting to inspect running state.";
            }
            catch (Exception ex) { state = null; status.Text = "Scan unavailable; mutation controls are blocked. " + ex.Message; }
            finally { busy = false; Buttons(); }
        }
        async Task Change(SecurityMitigationAction action, bool lsa = false)
        {
            if (busy || state is null || window.IsClosed) return;
            busy = true; Buttons();
            TaskActivityService.TaskActivityLease? lease = null;
            try
            {
                string actionName = lsa ? "Enable LSA protection" : action switch { SecurityMitigationAction.EnableMemoryIntegrity => "Enable Memory integrity", SecurityMitigationAction.DisableMemoryIntegrity => "Disable Memory integrity", _ => "Restore Memory integrity snapshot" };
                string warning = lsa
                    ? "This enables LSASS protection without adding a firmware lock. Incompatible authentication plug-ins or drivers may stop loading and affect sign-in. Review CodeIntegrity compatibility events first. The original RunAsPPL value is saved, but automatic LSA rollback is NOT available; manual administrator recovery may be required."
                    : action == SecurityMitigationAction.DisableMemoryIntegrity
                    ? "Disabling Memory integrity reduces kernel-code protection. This is not a general performance recommendation."
                    : action == SecurityMitigationAction.EnableMemoryIntegrity
                        ? "Memory integrity can expose incompatible drivers and, rarely, cause startup problems. Review driver compatibility in Windows Security first."
                        : "Restore replays the original Enabled DWORD or value absence from this machine's first snapshot. It does not guess defaults or bypass new policy/UEFI restrictions.";
                if (!await ShowConfirmationWindowAsync(actionName, warning + "\n\nOnly this individual " + (lsa ? "LSA" : "HVCI") + " setting will be changed. No firmware, TPM, Secure Boot, BCD, CPU mitigation masks, Defender, or other protection is changed. Restart is required; save your work before restarting. Continue?", actionName)) return;
                lease = await AcquireManagedTaskAsync(lsa ? "SecurityMitigations:LsaProtection" : "SecurityMitigations:MemoryIntegrity", actionName, new[] { "SystemMutation", "WindowsSecurity", "SecurityMitigations" });
                if (lease is null) return;
                CatalogStateEpoch.Invalidate();
                SecurityMitigationResult result;
                try { result = await Task.Run(() => lsa ? LsaProtectionPolicy.Enable(platform, lsaStore) : SecurityMitigationPolicy.Execute(action, platform, store)); }
                finally { CatalogStateEpoch.Invalidate(); }
                status.Text = result.Outcome + ": " + result.Message;
                report.Text += "\nLast operation: " + actionName + "\n" + status.Text + "\nReload required; evidence above predates this operation.\n";
                state = null;
                lease.Complete(result.Verified ? "COMPLETED" : "WARNING", status.Text);
                if (result.Verified && result.Changed) _pendingRestarts.Add(actionName);
            }
            catch (Exception ex) { state = null; status.Text = "Operation failed or remains unverified: " + ex.Message; lease?.Complete("FAILED", status.Text); }
            finally { lease?.Dispose(); RefreshManagedTaskHeader(); busy = false; Buttons(); }
        }
    }
}
