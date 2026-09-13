using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI;

namespace Naufal_Windows_Tech_s_Powertoys;

public sealed partial class MainWindow
{
    private async Task ShowBuiltInAppsAsync(BuiltInAppsService service)
    {
        Grid root = new() { RowSpacing = 10 };
        root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        root.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        StackPanel notes = new() { Spacing = 6 };
        notes.Children.Add(AppText(BuiltInAppsCatalog.Scope));
        notes.Children.Add(AppText(BuiltInAppsCatalog.Warning, warning: true));
        notes.Children.Add(AppText(BuiltInAppsCatalog.RestoreNotice));
        root.Children.Add(notes);

        StackPanel rows = new() { Spacing = 8 };
        ScrollViewer scroll = new() { Content = rows, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);
        var checks = new Dictionary<string, CheckBox>();
        var labels = new Dictionary<string, TextBlock>();
        List<Button> storeButtons = new();
        TextBlock status = AppText("Preparing...");
        bool busy = false, inventoryKnown = false;

        foreach (var target in BuiltInAppsCatalog.Targets)
        {
            Grid row = new() { ColumnSpacing = 12, Padding = new Thickness(10) };
            row.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            StackPanel detail = new() { Spacing = 4 };
            // No fixed-width glyph slot or fixed row height: supports all eight scales.
            CheckBox check = new() { Content = AppText(target.Name), IsChecked = false,
                HorizontalAlignment = HorizontalAlignment.Stretch };
            detail.Children.Add(check);
            TextBlock label = AppText("Preparing...");
            detail.Children.Add(label);
            row.Children.Add(detail);
            if (target.Kind == BuiltInAppKind.OneDriveDesktop)
                detail.Children.Add(AppText(OneDriveAppPolicy.Warning, warning: true));
            Button store = new() { Content = target.Kind == BuiltInAppKind.OneDriveDesktop
                ? OneDriveAppPolicy.RecoveryButton : "Microsoft Store", VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(store, 1);
            row.Children.Add(store);
            rows.Children.Add(new Border { Child = row, BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 180, 197, 219)) });
            checks.Add(target.Id, check);
            labels.Add(target.Id, label);
            storeButtons.Add(store);
            store.Click += async (_, _) =>
            {
                if (busy) return;
                try
                {
                    if (!await Windows.System.Launcher.LaunchUriAsync(BuiltInAppsCatalog.StoreUri(target)))
                        status.Text = target.Kind == BuiltInAppKind.OneDriveDesktop
                            ? "Microsoft website could not be opened." : "Microsoft Store could not be opened.";
                }
                catch (Exception exception) { status.Text = exception.Message; }
                // Opening a page never marks a package as restored. Analyze reads it again.
            };
        }

        StackPanel footer = new() { Spacing = 8 };
        Grid selection = new() { ColumnSpacing = 8 };
        for (int i = 0; i < 3; i++) selection.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        Button all = new() { Content = "Select all", HorizontalAlignment = HorizontalAlignment.Stretch };
        Button none = new() { Content = "De-select all", HorizontalAlignment = HorizontalAlignment.Stretch };
        Button reload = new() { Content = "Analyze / reload", HorizontalAlignment = HorizontalAlignment.Stretch };
        selection.Children.Add(all);
        Grid.SetColumn(none, 1); selection.Children.Add(none);
        Grid.SetColumn(reload, 2); selection.Children.Add(reload);
        footer.Children.Add(selection); footer.Children.Add(status);
        Grid.SetRow(footer, 2); root.Children.Add(footer);

        ToolWindow window = new(this, BuiltInAppsCatalog.Title, root,
            primaryButtonText: "Uninstall selected", secondaryButtonText: "Restore selected",
            initialWidth: 1000, initialHeight: 780) { IsBusy = () => busy };
        ApplyActionRiskStyle(window.PrimaryButton, ToolActionRisk.Danger);

        void UpdateControls()
        {
            bool selected = checks.Values.Any(check => check.IsChecked == true);
            window.PrimaryButton.IsEnabled = window.SecondaryButton.IsEnabled = !busy && inventoryKnown && selected;
            window.CloseButton.IsEnabled = !busy;
            all.IsEnabled = none.IsEnabled = !busy && inventoryKnown;
            reload.IsEnabled = !busy;
            foreach (var check in checks.Values) check.IsEnabled = !busy && inventoryKnown;
            foreach (var store in storeButtons) store.IsEnabled = !busy;
        }
        async Task ReadAsync()
        {
            inventoryKnown = false;
            var packages = await service.ReadAsync();
            int installed = 0;
            foreach (var target in BuiltInAppsCatalog.Targets)
            {
                var matches = packages.Where(p => BuiltInAppsCatalog.Matches(target, p)).ToArray();
                bool healthy = matches.Any(p => p.Healthy);
                if (healthy) installed++;
                labels[target.Id].Text = healthy ? "Installed" : matches.Length > 0 ? "Needs repair" : "Not installed";
                labels[target.Id].Foreground = new SolidColorBrush(healthy ? Color.FromArgb(255, 0, 112, 60)
                    : matches.Length > 0 ? Color.FromArgb(255, 196, 43, 28) : Color.FromArgb(255, 107, 114, 128));
            }
            inventoryKnown = true;
            status.Text = $"Installed: {installed}/{BuiltInAppsCatalog.Targets.Count}";
        }
        async Task ReloadAsync()
        {
            if (busy) return;
            busy = true; UpdateControls();
            try { await ReadAsync(); }
            catch (Exception exception)
            {
                status.Text = exception.Message;
                foreach (var label in labels.Values) label.Text = "Not verified";
            }
            finally { busy = false; UpdateControls(); }
        }
        async Task RunAsync(bool restore)
        {
            if (busy || !inventoryKnown) return;
            string[] ids = checks.Where(pair => pair.Value.IsChecked == true).Select(pair => pair.Key).ToArray();
            if (ids.Length == 0) return;
            busy = true; UpdateControls(); // Guard confirmation and task admission too.
            TaskActivityService.TaskActivityLease? lease = null;
            CatalogProgressWindow? progressWindow = null;
            try
            {
                var targets = BuiltInAppsCatalog.ResolveSelection(ids);
                string verb = restore ? "Restoring" : "Removing";
                string button = restore ? "Restore selected" : "Uninstall selected";
                StackPanel confirmation = new() { Spacing = 10 };
                confirmation.Children.Add(AppText(BuiltInAppsCatalog.Scope));
                confirmation.Children.Add(AppText(restore ? BuiltInAppsCatalog.RestoreNotice : BuiltInAppsCatalog.Warning, warning: true));
                if (targets.Any(t => t.Kind == BuiltInAppKind.OneDriveDesktop))
                {
                    confirmation.Children.Add(AppText(OneDriveAppPolicy.Warning, warning: true));
                    confirmation.Children.Add(AppText(OneDriveAppPolicy.SourceConsent));
                    if (restore) confirmation.Children.Add(AppText(OneDriveAppPolicy.RestoreNotice));
                }
                if (restore && targets.Any(t => t.Kind == BuiltInAppKind.Appx))
                    confirmation.Children.Add(AppText(BuiltInAppsCatalog.StoreConsent));
                confirmation.Children.Add(AppText(string.Join(Environment.NewLine, targets.Select(t => "• " + t.Name))));
                ToolWindow confirmWindow = new(window, button,
                    new ScrollViewer { Content = confirmation, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled },
                    primaryButtonText: button, closeButtonText: "Cancel", initialHeight: 560)
                    { CloseOnPrimary = true };
                if (await confirmWindow.ShowAsync() != ToolWindowResult.Primary) return;
                lease = await AcquireManagedTaskAsync("BuiltInWindowsApps", verb + ": " + BuiltInAppsCatalog.Title,
                    ["SystemMutation", "AppxDeployment", "Catalog:Advanced Windows Tweaks & De-Bloat"]);
                if (lease is null) return;
                progressWindow = new CatalogProgressWindow(window, verb,
                    targets.Select(t => new CatalogProgressItem(t.Id, t.Name)).ToArray());
                progressWindow.Show();
                var reporters = targets.ToDictionary(t => t.Id, t => progressWindow.CreateReporter(t.Id));
                var updates = new BuiltInUiProgress(update =>
                {
                    void Render()
                    {
                        switch (update.State)
                        {
                            case "RUNNING": progressWindow.BeginItem(update.Id, update.Detail); lease.UpdateDetail(update.Detail); break;
                            case "PROGRESS": reporters[update.Id].Report(new(update.Detail, update.Percent)); break;
                            case "VERIFYING": progressWindow.VerifyItem(update.Id, update.Detail); break;
                            case "COMPLETED": progressWindow.CompleteItem(update.Id, true, update.Detail); break;
                            case "FAILED": progressWindow.CompleteItem(update.Id, false, update.Detail); break;
                            case "UNAVAILABLE": progressWindow.UnavailableItem(update.Id, update.Detail); break;
                        }
                    }
                    if (DispatcherQueue.HasThreadAccess) Render();
                    else DispatcherQueue.TryEnqueue(Render);
                });
                var result = await BuiltInAppsRemoval.RunAsync(service, ids, updates, restore);
                string report = result.Report + Environment.NewLine + "Log: " + service.AuditPath;
                progressWindow.Complete(result.Success, report);
                lease.Complete(result.Success ? "COMPLETED" : "FAILED", report);
                try { await ReadAsync(); }
                catch (Exception exception) { inventoryKnown = false; status.Text = exception.Message; }
            }
            catch (Exception exception)
            {
                progressWindow?.Complete(false, exception.Message);
                lease?.Complete("FAILED", exception.Message);
                status.Text = exception.Message;
            }
            finally
            {
                lease?.Dispose();
                busy = false;
                UpdateControls();
                RefreshManagedTaskHeader();
            }
        }
        foreach (var check in checks.Values)
        {
            check.Checked += (_, _) => UpdateControls();
            check.Unchecked += (_, _) => UpdateControls();
        }
        all.Click += (_, _) => { if (!busy) foreach (var check in checks.Values) check.IsChecked = true; };
        none.Click += (_, _) => { if (!busy) foreach (var check in checks.Values) check.IsChecked = false; };
        reload.Click += async (_, _) => await ReloadAsync();
        window.PrimaryButton.Click += async (_, _) => await RunAsync(false);
        window.SecondaryButton.Click += async (_, _) => await RunAsync(true);
        UpdateControls();
        var closed = window.ShowAsync();
        await ReloadAsync();
        await closed;
    }

    private static TextBlock AppText(string text, bool warning = false) => new()
    {
        Text = text, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true,
        Foreground = new SolidColorBrush(warning ? Color.FromArgb(255, 196, 43, 28) : Color.FromArgb(255, 18, 28, 40))
    };

    private sealed class BuiltInUiProgress(Action<BuiltInAppUpdate> report) : IProgress<BuiltInAppUpdate>
    { public void Report(BuiltInAppUpdate value) => report(value); }
}
