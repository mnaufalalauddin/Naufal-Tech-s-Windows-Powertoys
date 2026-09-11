using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Naufal_Windows_Tech_s_Powertoys;

// White text on neutral gray remains legible in both themes and wraps at all scales.
internal sealed class CatalogAvailabilityBadge
{
    internal Border View { get; }
    private readonly TextBlock _text = new()
    {
        TextWrapping = TextWrapping.Wrap,
        Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255))
    };

    internal CatalogAvailabilityBadge()
    {
        View = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(255, 107, 114, 128)),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10, 6, 10, 6),
            Margin = new Thickness(0, 4, 0, 4),
            Visibility = Visibility.Collapsed,
            Child = _text
        };
    }

    internal void Update(int verified, int total, int unavailable)
    {
        _text.Text = CatalogAvailability.Summary(verified, total, unavailable);
        View.Visibility = unavailable > 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}
