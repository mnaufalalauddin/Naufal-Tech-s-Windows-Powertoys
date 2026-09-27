using Microsoft.UI.Xaml;
using System;
using System.Linq;

namespace Naufal_Windows_Tech_s_Powertoys;

public sealed partial class MainWindow
{
    private readonly OperationGate _homeActionGate = new();

    partial void OnHomeActivated() => _ = UpdateGamingStatusAsync();

    private void HomeQuickRepair_Click(object sender, RoutedEventArgs args) => QuickRepairButton_Click(sender, args);
    private void HomeSystemReport_Click(object sender, RoutedEventArgs args) => SystemReportButton_Click(sender, args);

    private async void HomeShaderCache_Click(object sender, RoutedEventArgs args)
    {
        using var gate = _homeActionGate.TryEnter();
        if (gate is null) return;
        TaskActivityService.TaskActivityLease? lease = null;
        CatalogProgressWindow? progress = null;
        try
        {
            var action = _gamingActionsService.GetActions().Single(item => item.Id == "ShaderCache");
            if (!await ShowConfirmationWindowAsync(action.Name, action.Confirmation, action.RunLabel)) return;
            lease = await AcquireManagedTaskAsync("CatalogAction:Gaming Tweaks:ShaderCache", "Clearing GPU Shader Cache",
                new[] { "SystemMutation", "Catalog:Gaming Tweaks" });
            if (lease is null) return;
            progress = new CatalogProgressWindow(this, "Clearing", new[] { new CatalogProgressItem(action.Id, action.Name) });
            progress.Show();
            progress.BeginItem(action.Id, "Clearing GPU shader cache...");
            lease.UpdateDetail("Clearing GPU shader cache...");
            ToolActionResult result = await CatalogActionRunner.ExecuteAsync(_gamingActionsService, action, restore: false, progress.CreateReporter(action.Id));
            if (result.SkippedUnavailable) progress.UnavailableItem(action.Id, result.Message);
            else progress.CompleteItem(action.Id, result.Success, result.Message);
            progress.UpdateOverall(1, result.Message);
            progress.Complete(result.Success || result.SkippedUnavailable, result.Message);
            lease.Complete(result.SkippedUnavailable ? "UNAVAILABLE" : result.Success ? "COMPLETED" : "FAILED", result.Message);
        }
        catch (Exception exception)
        {
            progress?.CompleteItem("ShaderCache", false, exception.Message);
            progress?.Complete(false, exception.Message);
            lease?.Complete("FAILED", exception.Message);
            if (progress is null && !_isClosed)
                await ShowMessageDialogAsync("Shader cache cleanup failed", exception.Message);
        }
        finally
        {
            lease?.Dispose();
            RefreshManagedTaskHeader();
        }
    }
}
