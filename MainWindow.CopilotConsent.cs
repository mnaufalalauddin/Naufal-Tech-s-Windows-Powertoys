using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Naufal_Windows_Tech_s_Powertoys;

public sealed partial class MainWindow
{
    // The caller creates the consent scope, in its own async execution context,
    // only after this window returns Primary. Opening or cancelling grants none.
    private static async Task<bool> ConfirmCopilotSourceAsync(Window owner)
    {
        StackPanel content = new() { Spacing = 14 };
        foreach (string message in new[]
        {
            "To check Microsoft Copilot, WinGet must use the Microsoft Store source.",
            "Choosing Agree and continue accepts the Microsoft Store source terms and allows WinGet to send this PC's two-letter region code to Microsoft. WinGet may remember that acceptance.",
            "Cancel stops this operation before any changes. Checking the source does not buy or install apps."
        })
            content.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true });
        content.Children.Add(new HyperlinkButton
        {
            Content = "Read Microsoft Store terms",
            NavigateUri = new Uri("https://aka.ms/microsoft-store-terms-of-transaction")
        });
        ToolWindow confirmation = new(owner, "Microsoft Store source agreement",
            new ScrollViewer { Content = content, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled },
            primaryButtonText: "Agree and continue", closeButtonText: "Cancel",
            initialWidth: 700, initialHeight: 460, minimumWidth: 460, minimumHeight: 300)
            { CloseOnPrimary = true };
        return await confirmation.ShowAsync() == ToolWindowResult.Primary;
    }
}
