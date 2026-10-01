using Microsoft.UI.Xaml;
using System;
using System.Threading.Tasks;

namespace Naufal_Windows_Tech_s_Powertoys;

public sealed partial class MainWindow
{
    private IToolToggleService EssentialCatalog() => _essentialCatalog ??= new CompositeToolToggleService(
        new FilteredToolToggleService(_essentialTweaksService, "EndTask", "ClassicContext", "Hibernation", "ServicesManual", "PhotoViewer"),
        new PerformanceLabService("Essential Windows Tweaks"),
        new EssentialBulkActionsService(_essentialActionsService, _essentialActionsService.ReadBulkStateAsync));

    private IToolToggleService GamingCatalog() => _gamingCatalog ??= GamingCatalogOwnership.Create(
        _gamingTweaksService, new GamingBcdService(), new PerformanceLabService("Gaming Tweaks"));

    private IToolToggleService AdvancedCatalog() => _advancedCatalog ??= new DebloatCatalogService(
        _debloatService, _essentialTweaksService, _essentialActionsService, _windowsAiService, _debloatRegistryLabService,
        _debloatNetworkStorageService, _xboxComponentsService, _debloatServiceGroupsService);

    private async void CatalogInventory_Click(object sender, RoutedEventArgs args)
    {
        try
        {
            var catalogs = new[] { ("Essential Windows Tweaks", EssentialCatalog()), ("Gaming Tweaks", GamingCatalog()), ("Advanced Windows Tweaks & De-Bloat", AdvancedCatalog()) };
            var rows = await Task.Run(() => CatalogInventoryReport.Build(catalogs));
            if (!_isClosed) await ShowTableReportDialogAsync("Action Inventory / Shared Targets", "Action-Inventory", rows);
        }
        catch (Exception exception) { if (!_isClosed) await ShowMessageDialogAsync("Inventory failed", exception.Message); }
    }
}
