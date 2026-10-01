using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace Naufal_Windows_Tech_s_Powertoys;

public sealed partial class MainWindow
{
    private ToolWindow? _storageManagerWindow;

    private async void StorageManager_Click(object sender, RoutedEventArgs args)
    {
        if (_storageManagerWindow is { IsClosed: false }) { _storageManagerWindow.Activate(); return; }
        StorageServicing service = new(new StorageNativeRunner(), Path.Combine(AppDataPaths.LocalRoot, "Logs", "StorageServicing"));
        bool busy = false;
        StackPanel panel = new() { Spacing = 12 };
        TextBlock status = new() { Text = "Choose an inventory first. Nothing runs automatically. Administrative rights are required for most servicing queries and all changes.", TextWrapping = TextWrapping.Wrap };
        ComboBox area = new() { Header = "Inventory / analysis", HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (string name in new[] { "Optional Windows features", "Windows capabilities", "Component store analysis", "Third-party drivers", "Reserved storage status", "CompactOS status", "Windows Recovery Environment status" }) area.Items.Add(name);
        area.SelectedIndex = 0;
        Button inspect = new() { Content = "Load inventory / analyze", HorizontalAlignment = HorizontalAlignment.Stretch };
        ComboBox selection = new() { Header = "Individual feature / capability (select after inventory)", HorizontalAlignment = HorizontalAlignment.Stretch };
        Button enable = new() { Content = "Enable / install selected", IsEnabled = false, HorizontalAlignment = HorizontalAlignment.Stretch };
        Button disable = new() { Content = "Disable / remove selected", IsEnabled = false, HorizontalAlignment = HorizontalAlignment.Stretch };
        Button cleanup = new() { Content = "Component cleanup (without ResetBase)", HorizontalAlignment = HorizontalAlignment.Stretch };
        TextBox consent = new() { Header = "Irreversible ResetBase: type RESETBASE", PlaceholderText = "Existing updates cannot be uninstalled afterward." };
        Button reset = new() { Content = "Component cleanup + irreversible ResetBase", HorizontalAlignment = HorizontalAlignment.Stretch };
        TextBox exportPath = new() { Header = "Driver export parent folder (existing local folder)", PlaceholderText = "A new unique subfolder will be created; existing files are not overwritten." };
        Button export = new() { Content = "Export third-party driver packages", HorizontalAlignment = HorizontalAlignment.Stretch };
        TextBlock help = new() { Text = "Feature disable retains payload (no /Remove). Install uses Windows servicing sources and may download files. No automatic dependency enabling, reboot, driver deletion, WinSxS deletion, recovery partition deletion or system-folder cleanup. Use the saved before-state to decide whether to reverse a selected feature/capability change; logs do not contain installation payloads. Reserved storage, CompactOS and recovery are inventory-only here.", TextWrapping = TextWrapping.Wrap };
        TextBlock report = new() { Text = "No operation has run.", TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };
        foreach (UIElement child in new UIElement[] { status, area, inspect, selection, enable, disable, cleanup, consent, reset, exportPath, export, help, report }) panel.Children.Add(child);
        ToolWindow window = new(this, "Storage / Windows Servicing", new ScrollViewer { Content = panel }, primaryButtonText: "Copy", secondaryButtonText: "Save TXT", initialWidth: 1040, initialHeight: 820);
        _storageManagerWindow = window;
        window.IsBusy = () => busy;
        selection.SelectionChanged += (_, _) => SetAvailable();
        area.SelectionChanged += (_, _) => { selection.Items.Clear(); SetAvailable(); };
        inspect.Click += async (_, _) => await Run(async () =>
        {
            selection.Items.Clear();
            if (area.SelectedIndex < 2)
            {
                var inventory = await service.InventoryAsync((StorageItemKind)area.SelectedIndex);
                report.Text = inventory.Report;
                foreach (StorageItem item in inventory.Items) selection.Items.Add(item);
                status.Text = inventory.Items.Count == 0 ? "No selectable identities were verified. Review the raw result; an empty list does not prove nothing is installed." : $"Loaded {inventory.Items.Count} identities. Select one; every mutation rechecks its current state.";
            }
            else
            {
                StorageCommand command = area.SelectedIndex switch
                {
                    2 => StorageServicing.Dism("/Cleanup-Image", "/AnalyzeComponentStore"),
                    3 => new("pnputil.exe", new[] { "/enum-drivers" }),
                    4 => StorageServicing.Dism("/Get-ReservedStorageState"),
                    5 => new("compact.exe", new[] { "/CompactOS:query" }),
                    _ => new("reagentc.exe", new[] { "/info" })
                };
                report.Text = await service.InspectAsync(command);
                status.Text = "Inspection finished. Review exit status and provider output; unsupported commands are not reported as success.";
            }
        }, false);
        enable.Click += async (_, _) => await Change(true);
        disable.Click += async (_, _) => await Change(false);
        cleanup.Click += async (_, _) => await Mutate("Component cleanup", "Run supported DISM component cleanup? Superseded components may be removed. This has no application rollback; ResetBase is NOT used.", () => service.CleanupAsync(false, ""));
        reset.Click += async (_, _) =>
        {
            if (consent.Text != "RESETBASE") { status.Text = "Type RESETBASE exactly before requesting this irreversible operation."; return; }
            await Mutate("Irreversible ResetBase", "Permanently remove superseded component versions? Existing Windows updates cannot be uninstalled afterward. A TXT log cannot restore them. Make a tested system image backup first. This is separate from normal cleanup.", () => service.CleanupAsync(true, "RESETBASE"));
        };
        export.Click += async (_, _) =>
        {
            string destination = exportPath.Text.Trim();
            await Mutate("Export driver packages", "Export third-party driver packages to a new subfolder under:\n" + destination + "\nNo drivers will be removed. Ensure sufficient free space.", () => service.ExportDriversAsync(destination));
        };
        window.PrimaryButton.Click += (_, _) =>
        {
            try { DataPackage data = new(); data.SetText(report.Text); Clipboard.SetContent(data); status.Text = "Report copied."; }
            catch (Exception ex) { status.Text = "Copy failed: " + ex.Message; }
        };
        window.SecondaryButton.Click += async (_, _) => await Run(async () =>
        {
            string? saved = await DesktopReportExport.SaveAsync(window, "Storage-Servicing", report.Text);
            if (saved is not null) status.Text = "Saved: " + saved;
        }, false);
        SetAvailable();
        try { await window.ShowAsync(); }
        finally { if (ReferenceEquals(_storageManagerWindow, window)) _storageManagerWindow = null; }
        await OfferPendingRestartAsync();

        void SetAvailable()
        {
            area.IsEnabled = inspect.IsEnabled = selection.IsEnabled = consent.IsEnabled = exportPath.IsEnabled = !busy;
            enable.IsEnabled = disable.IsEnabled = !busy && selection.SelectedItem is StorageItem && WindowsPrivilegeService.IsAdministrator();
            cleanup.IsEnabled = reset.IsEnabled = export.IsEnabled = !busy && WindowsPrivilegeService.IsAdministrator();
            window.PrimaryButton.IsEnabled = window.SecondaryButton.IsEnabled = !busy;
        }
        async Task Change(bool enabled)
        {
            if (selection.SelectedItem is not StorageItem item) return;
            await Mutate(enabled ? "Enable / install selected" : "Disable / remove selected",
                $"{item.Kind}: {item.Name}\nInventory state: {item.State}\nRequested: {(enabled ? "enable / install" : "disable / remove")}\nThis can affect dependent apps and Windows functionality. Windows may require a restart. Installation may use Windows Update; capability removal removes its payload. Feature disable retains its payload. Current state is saved in the operation log before execution, not a complete reinstall backup.", () => service.ChangeAsync(item, enabled));
        }
        async Task Mutate(string title, string explanation, Func<Task<StorageOperationResult>> action)
        {
            if (busy) return;
            // Reserve this dialog before awaiting confirmation, so two clicks cannot queue two operations.
            busy = true; SetAvailable();
            try
            {
                if (!WindowsPrivilegeService.IsAdministrator()) { status.Text = "Administrator rights are required."; return; }
                if (!await ShowConfirmationWindowAsync(title, explanation, "Confirm operation")) return;
                using var lease = await AcquireManagedTaskAsync("StorageServicing", title, new[] { "SystemMutation", "WindowsServicing", "DriverConfiguration" });
                if (lease is null) return;
                status.Text = "Windows servicing is running. Closing is disabled; no short timeout or automatic reboot is used.";
                try
                {
                    StorageOperationResult result;
                    CatalogStateEpoch.Invalidate();
                    try { result = await action(); }
                    finally { CatalogStateEpoch.Invalidate(); }
                    report.Text = result.Report;
                    status.Text = result.Outcome + ". Review the saved log and before/after evidence.";
                    if (result.RestartRequired) { _pendingRestarts.Add(title); status.Text += " Restart Windows when convenient; effectiveness after reboot has not been verified."; }
                    lease.Complete(result.Outcome == "Failed" ? "FAILED" : result.Outcome is "Blocked" or "Unknown" or "VerificationPending" or "RebootRequired" ? "WARNING" : "COMPLETED", status.Text);
                    selection.Items.Clear();
                }
                catch (Exception ex) { lease.Complete("FAILED", ex.Message); throw; }
            }
            catch (Exception ex) { status.Text = "Operation not verified: " + ex.Message; report.Text += "\n" + ex; }
            finally { busy = false; SetAvailable(); RefreshManagedTaskHeader(); }
        }
        async Task Run(Func<Task> action, bool unused)
        {
            if (busy) return;
            busy = true; SetAvailable(); status.Text = "Reading / preparing report...";
            try { await action(); }
            catch (Exception ex) { status.Text = "Operation failed: " + ex.Message; }
            finally { busy = false; SetAvailable(); }
        }
    }
}
