using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;

namespace Naufal_Windows_Tech_s_Powertoys;

public sealed partial class MainWindow
{
    private async void BackgroundOwners_Click(object sender, RoutedEventArgs args)
    {
        var button = sender as Button;
        if (button is not null) button.IsEnabled = false;
        try
        {
            var snapshot = await BackgroundOwnerService.ReadAsync();
            var rows = await Task.Run(() => BackgroundOwnerReport.Build(snapshot));
            if (!_isClosed) await ShowTableReportDialogAsync("Background Owner Finder — Read-only", "Background-Owners", rows);
        }
        catch (Exception exception)
        {
            if (!_isClosed) await ShowMessageDialogAsync("Background inventory unavailable", exception.Message + "\nNo process or Windows setting was changed. Retry after the provider becomes available.");
        }
        finally { if (!_isClosed && button is not null) button.IsEnabled = true; }
    }
}
