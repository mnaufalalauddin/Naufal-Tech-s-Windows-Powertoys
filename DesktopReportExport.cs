using Microsoft.UI.Xaml;
using Microsoft.Windows.Storage.Pickers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Naufal_Windows_Tech_s_Powertoys;

internal static class DesktopReportExport
{
    // Windows.Storage.Pickers does not support elevation. This app requires
    // Administrator, so use the Windows App SDK desktop picker on the UI thread.
    internal static async Task<string?> SaveAsync(Window owner, string suggestedName, string report)
    {
        FileSavePicker picker = new(owner.AppWindow.Id)
        {
            SuggestedFileName = suggestedName,
            DefaultFileExtension = ".txt"
        };
        picker.FileTypeChoices.Add("Text File", new List<string> { ".txt" });
        PickFileResult? result = await picker.PickSaveFileAsync();
        if (result is null) return null;
        await File.WriteAllTextAsync(result.Path, report);
        return Path.GetFileName(result.Path);
    }
}
