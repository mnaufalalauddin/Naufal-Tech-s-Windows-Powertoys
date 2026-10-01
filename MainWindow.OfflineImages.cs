using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace Naufal_Windows_Tech_s_Powertoys;

public sealed partial class MainWindow
{
    private ToolWindow? _offlineImagesWindow;

    private async void OfflineImages_Click(object sender, RoutedEventArgs args)
    {
        if (_offlineImagesWindow is { IsClosed: false }) { _offlineImagesWindow.Activate(); return; }
        OfflineImageService service = new();
        OfflineImageSession? session = null;
        bool busy = false;
        TextBlock status = new() { Text = "Offline WIM workspace — no host servicing. Inspect your source before creating a working copy.", TextWrapping = TextWrapping.Wrap };
        TextBlock safety = new() { Text = "Advanced workflow: work on a disposable image and test deployment in a VM. Only the selected index of an application-created WIM copy is serviced. No ISO, ESD, VHD, live Windows, arbitrary package/driver removal, or direct WinSxS deletion. Source files are not modified. Closing this window does not discard a mounted image: retain session.json and use Recover, then explicitly Commit or Discard. Do not delete a mounted workspace.", TextWrapping = TextWrapping.Wrap };
        TextBox source = new() { Header = "Source WIM — full local path", PlaceholderText = @"D:\Images\install.wim" };
        TextBox workspace = new() { Header = "Existing workspace parent folder — outside Windows / Program Files", PlaceholderText = @"D:\ImageWork" };
        TextBox index = new() { Header = "Image index (from Inspect WIM)", Text = "1" };
        TextBox manifest = new() { Header = "Recovery manifest — session.json (keep this path)", IsReadOnly = false };
        TextBlock location = new() { Text = "No session selected.", TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };
        ListView inventory = new() { SelectionMode = ListViewSelectionMode.Multiple, Height = 260 };
        TextBlock report = new() { Text = "Nothing has run.", TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };
        StackPanel panel = new() { Spacing = 12 };
        Button Button(string label) => new() { Content = label, HorizontalAlignment = HorizontalAlignment.Stretch };
        Button inspect = Button("Inspect source WIM / list indexes"), clone = Button("Create verified working copy"),
            recover = Button("Recover session / reconcile mount"), mount = Button("Mount verified working copy"),
            scan = Button("Load removable-item inventory"), remove = Button("Remove selected items from copy"),
            analyze = Button("Analyze copy component store"), cleanup = Button("Clean copy component store (no ResetBase)"),
            commit = Button("Commit and unmount working copy"), discard = Button("Discard mounted changes and unmount"),
            export = Button("Export committed index to optimized.wim");
        Button[] commands = [inspect, clone, recover, mount, scan, remove, analyze, cleanup, commit, discard, export];
        foreach (UIElement control in new UIElement[] { status, safety, source, index, inspect, workspace, clone, manifest, recover, location, mount, scan, inventory, remove, analyze, cleanup, commit, discard, export, report }) panel.Children.Add(control);
        ToolWindow window = new(this, "Offline Image Workspace", new ScrollViewer { Content = panel },
            primaryButtonText: "Copy", secondaryButtonText: "Save TXT", initialWidth: 1080, initialHeight: 850);
        _offlineImagesWindow = window;
        window.IsBusy = () => busy;
        Progress<string> progress = new(message => { if (!window.IsClosed) status.Text = message; });

        inspect.Click += async (_, _) => await Run("Inspect WIM", null, async () => report.Text = await service.InspectAsync(source.Text.Trim(), progress));
        clone.Click += async (_, _) =>
        {
            string input = source.Text.Trim(), parent = workspace.Text.Trim();
            if (!int.TryParse(index.Text, out int selectedIndex) || selectedIndex < 1) { status.Text = "Enter a positive index listed by Inspect WIM."; return; }
            await Run("Create working copy", $"Copy {input}\nSelected index: {selectedIndex}\nWorkspace parent: {parent}\nThe entire WIM will be copied and checksum-verified. Allow room for expansion and export (minimum twice the source size plus 10 GiB). The source is retained; this is not a backup of your current Windows installation.", async () =>
            {
                session = await service.CloneAsync(input, parent, selectedIndex, progress);
                manifest.Text = session.Manifest; inventory.Items.Clear();
                report.Text = "Clone checksum verified. No image has been mounted or serviced.\n" + session.ImageIdentity + "\nSHA-256: " + session.SourceSha256;
            });
        };
        recover.Click += async (_, _) => await Run("Recover offline session", null, async () =>
        {
            // Do not replace an active in-memory session with a different one.
            var loaded = OfflineImageSession.Load(manifest.Text.Trim());
            if (session is not null && !Terminal(session) && !OfflineImagePolicy.Same(session.Manifest, loaded.Manifest))
                throw new InvalidOperationException("Finish the current session before switching workspaces.");
            session = loaded; inventory.Items.Clear(); report.Text = await service.RecoverAsync(session, progress);
        });
        mount.Click += async (_, _) => await Run("Mount offline copy", "Mount the verified working copy read/write? The original WIM is never mounted. Keep session.json for crash recovery. The mount remains until you explicitly Commit or Discard.", async () =>
        { await service.MountAsync(RequireSession(), progress); report.Text = "Owned clone/index/mount verified. Load inventory before selecting removals."; });
        scan.Click += async (_, _) => await Run("Offline inventory", null, async () =>
        {
            var items = await service.InventoryAsync(RequireSession(), progress);
            inventory.Items.Clear(); foreach (var item in items.Where(OfflineImagePolicy.CanRemove)) inventory.Items.Add(item);
            report.Text = "Full inventory (only conservative allowlisted removable items appear in the selection list):\n" + string.Join("\n", items);
        });
        remove.Click += async (_, _) =>
        {
            var selected = inventory.SelectedItems.OfType<OfflineImageItem>().ToArray();
            if (selected.Length == 0) { status.Text = "Select at least one inventoried removable item."; return; }
            await Run("Remove offline items", "Remove the following from the working copy? Features/capabilities lose payload; provisioned apps will not be installed for new users of the image. Dependencies may break. There is no per-item restore: discard this mount and start from the source if needed.\n\n" + string.Join("\n", selected.Select(i => i.ToString())), async () =>
            { report.Text = await service.RemoveAsync(RequireSession(), selected, progress); inventory.Items.Clear(); });
        };
        analyze.Click += async (_, _) => await Run("Analyze offline component store", null, async () => report.Text = await service.AnalyzeStoreAsync(RequireSession(), progress));
        cleanup.Click += async (_, _) => await Run("Clean offline component store", "Run supported component cleanup on this copy? Superseded components may be removed. ResetBase is not used. Keep the original source and test the exported image; no space-saving amount is guaranteed.", async () =>
        { await service.CleanupAsync(RequireSession(), progress); report.Text = "Cleanup command and subsequent analysis completed on the copy. Review DISM logs. Boot compatibility is not verified."; });
        commit.Click += async (_, _) => await Run("Commit offline copy", "Commit the verified changes into working.wim and unmount it? After commit, Discard cannot undo these changes; the untouched source remains your recovery image. Boot/deployment has not been tested.", async () =>
        { await service.FinishAsync(RequireSession(), true, progress); inventory.Items.Clear(); report.Text = "Changes committed; clone and mount are no longer registered as mounted. Export the chosen index next."; });
        discard.Click += async (_, _) => await Run("Discard offline changes", "Discard uncommitted changes in this owned mount and unmount it? This does not delete the original, working copy, logs or workspace. Only changes since the mount are discarded.", async () =>
        { await service.FinishAsync(RequireSession(), false, progress); inventory.Items.Clear(); report.Text = "Owned mount discarded and unmounted. Files and recovery evidence retained."; });
        export.Click += async (_, _) => await Run("Export offline image", "Export the committed selected index to a new optimized.wim in this workspace? Existing output is never overwritten. Test the exported image in a VM before deployment.", async () => report.Text = await service.ExportAsync(RequireSession(), progress));
        window.PrimaryButton.Click += (_, _) =>
        {
            try { DataPackage data = new(); data.SetText(location.Text + "\n" + report.Text); Clipboard.SetContent(data); status.Text = "Copied report; paths may contain personal information."; }
            catch (Exception ex) { status.Text = "Copy failed: " + ex.Message; }
        };
        window.SecondaryButton.Click += async (_, _) =>
        {
            if (busy) return;
            busy = true; Buttons();
            try { await DesktopReportExport.SaveAsync(window, "Offline-Image", location.Text + "\n" + report.Text); }
            catch (Exception ex) { status.Text = "Save failed: " + ex.Message; }
            finally { busy = false; Buttons(); }
        };
        Buttons();
        try { await window.ShowAsync(); }
        finally { if (ReferenceEquals(_offlineImagesWindow, window)) _offlineImagesWindow = null; }

        OfflineImageSession RequireSession() => session ?? throw new InvalidOperationException("Create or recover a session first.");
        static bool Terminal(OfflineImageSession value) => value.State is "Committed" or "Exported" or "Discarded" or "CloneFailed";
        void Buttons()
        {
            foreach (var control in commands) control.IsEnabled = !busy;
            source.IsEnabled = index.IsEnabled = workspace.IsEnabled = manifest.IsEnabled = inventory.IsEnabled = !busy;
            window.PrimaryButton.IsEnabled = window.SecondaryButton.IsEnabled = !busy;
            clone.IsEnabled = !busy && (session is null || Terminal(session));
            mount.IsEnabled = !busy && session?.State == "Cloned";
            bool mounted = session?.State is "Mounted" or "Modified" or "RecoveryRequired";
            scan.IsEnabled = analyze.IsEnabled = discard.IsEnabled = !busy && mounted;
            remove.IsEnabled = cleanup.IsEnabled = commit.IsEnabled = !busy && mounted && !session!.VerificationFailed;
            export.IsEnabled = !busy && session?.State == "Committed";
            location.Text = session is null ? "No session selected." : $"Session: {session.State}\nSource: {session.Source}\nWorking copy: {session.Clone}\nMount: {session.Mount}\nIndex: {session.Index}\nRecovery manifest: {session.Manifest}";
        }
        async Task Run(string title, string? confirmation, Func<Task> action)
        {
            if (busy || window.IsClosed) return;
            busy = true; Buttons();
            TaskActivityService.TaskActivityLease? lease = null;
            try
            {
                if (!WindowsPrivilegeService.IsAdministrator()) { status.Text = "Reopen the application as Administrator for DISM operations. Nothing was changed."; return; }
                if (confirmation is not null && !await ShowConfirmationWindowAsync(title, confirmation, "Confirm operation")) return;
                lease = await AcquireManagedTaskAsync("OfflineImageWorkspace", title, new[] { "SystemMutation", "WindowsServicing", "OfflineImage" });
                if (lease is null) return;
                await action();
                status.Text = "Operation finished. Review the report and retained logs; no boot/deployment compatibility is implied.";
                lease.Complete(session?.VerificationFailed == true ? "WARNING" : "COMPLETED", title + "; offline evidence recorded.");
            }
            catch (Exception ex)
            {
                status.Text = "Operation not verified: " + ex.Message;
                report.Text += "\n" + status.Text + "\nRetain the workspace. Recover session before retrying after an interrupted operation.";
                lease?.Complete("WARNING", status.Text);
            }
            finally { lease?.Dispose(); RefreshManagedTaskHeader(); busy = false; Buttons(); }
        }
    }
}
