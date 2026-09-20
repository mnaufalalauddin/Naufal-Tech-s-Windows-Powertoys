using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace Naufal_Windows_Tech_s_Powertoys;

public sealed partial class MainWindow
{
    private async void AboutButton_Click(object sender, RoutedEventArgs e)
    {
        StackPanel content = new() { Spacing = 16, Margin = new Thickness(20) };
        content.Children.Add(new TextBlock
        {
            Text = AppIdentity.Product, Tag = "ui-literal", FontSize = 24,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap
        });
        content.Children.Add(new TextBlock { Text = "Version " + AppIdentity.Version });
        content.Children.Add(new TextBlock { Text = NativeUiCatalog.AboutDescription, TextWrapping = TextWrapping.Wrap });
        content.Children.Add(new TextBlock { Text = NativeUiCatalog.AboutPurpose, TextWrapping = TextWrapping.Wrap });
        content.Children.Add(new TextBlock { Text = "Developed by" });
        content.Children.Add(new TextBlock { Text = AppIdentity.Developer, Tag = "ui-literal" });
        content.Children.Add(new TextBlock { Text = "Open-Source Software" });
        content.Children.Add(new TextBlock { Text = "Licensed under the MIT License", TextWrapping = TextWrapping.Wrap });
        content.Children.Add(new HyperlinkButton { Content = "Source Code", NavigateUri = new Uri(AppIdentity.SourceUrl) });
        content.Children.Add(new TextBlock { Text = AppIdentity.SourceUrl, Tag = "ui-literal", TextWrapping = TextWrapping.Wrap });
        ToolWindow window = new(this, "About", new ScrollViewer
        {
            Content = content, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        }, initialWidth: 820, initialHeight: 700);
        await window.ShowAsync();
    }
}
