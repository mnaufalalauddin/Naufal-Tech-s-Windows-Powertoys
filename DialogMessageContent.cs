using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Naufal_Windows_Tech_s_Powertoys;

internal static class DialogMessageContent
{
    // ToolWindow supplies a bounded star row; action buttons stay outside it.
    internal static ScrollViewer Create(string message) => new()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch,
        VerticalScrollMode = ScrollMode.Enabled,
        VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
        HorizontalScrollMode = ScrollMode.Disabled,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        IsTabStop = true,
        Content = new TextBlock
        {
            Text = message, TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(4, 4, 16, 4)
        }
    };
}
