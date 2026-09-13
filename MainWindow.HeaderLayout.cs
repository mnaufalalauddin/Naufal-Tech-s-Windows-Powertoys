using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Naufal_Windows_Tech_s_Powertoys;

public sealed partial class MainWindow
{
        private void TextScaleButton_Click(object sender, RoutedEventArgs e)
        {
            MenuFlyout flyout = new();
            int[] scaleOptions = { 25, 50, 75, 100, 125, 150, 175, 200 };
            foreach (int percent in scaleOptions)
            {
                int selectedPercent = percent;
                ToggleMenuFlyoutItem item = new()
                {
                    Text = $"{percent}%",
                    IsChecked = UiDisplaySettings.TextScalePercent == percent,
                    FontSize = HeaderLayoutPolicy.PopupFontSize(UiDisplaySettings.TextScalePercent)
                };
                item.Click += (_, _) => UiDisplaySettings.SetTextScale(selectedPercent);
                flyout.Items.Add(item);
            }

            flyout.Items.Add(new MenuFlyoutSeparator());
            MenuFlyoutItem resetItem = new()
            {
                Text = "Reset to 100%",
                FontSize = HeaderLayoutPolicy.PopupFontSize(UiDisplaySettings.TextScalePercent)
            };
            resetItem.Click += (_, _) => UiDisplaySettings.SetTextScale(100);
            flyout.Items.Add(resetItem);

            // Keep the popup in the window's authored tree, including while
            // closed, so language changes retain its canonical captions.
            Microsoft.UI.Xaml.Controls.Primitives.FlyoutBase.SetAttachedFlyout(TextScaleButton, flyout);
            UiTranslation.Apply(RootLayout, UiDisplaySettings.LanguageCode);
            flyout.ShowAt(TextScaleButton);
        }

    private void HeaderLayout_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateHeaderLayout();

    private void UpdateHeaderLayout()
    {
        if (HeaderLayout is null || HeaderClock is null || HeaderSettings is null) return;
        double width = HeaderLayout.ActualWidth;
        if (!double.IsFinite(width) || width <= 0) return;
        bool stacked = HeaderLayoutPolicy.StackClock(HeaderLayout.ActualWidth,
            UiDisplaySettings.TextScalePercent, UiDisplaySettings.GeometryScale);
        Grid.SetColumn(HeaderClock, 0);
        Grid.SetColumnSpan(HeaderClock, stacked ? 2 : 1);
        Grid.SetRow(HeaderSettings, stacked ? 1 : 0);
        Grid.SetColumn(HeaderSettings, stacked ? 0 : 1);
        Grid.SetColumnSpan(HeaderSettings, stacked ? 2 : 1);
        // Both recovery buttons remain at the trailing edge. On narrow windows
        // the language selector can shrink instead of pushing them off-screen.
        HeaderSettings.MaxWidth = width;
        HeaderSettings.HorizontalAlignment = stacked ? HorizontalAlignment.Stretch : HorizontalAlignment.Right;
        LanguageComboBox.MaxWidth = System.Math.Max(0, width -
            System.Math.Max(ThemeButton.Width, ThemeButton.MinWidth) -
            System.Math.Max(TextScaleButton.Width, TextScaleButton.MinWidth) - 2 * HeaderSettings.ColumnSpacing);
    }

    private void ResetTextScale_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        UiDisplaySettings.SetTextScale(100);
        args.Handled = true;
    }
}
