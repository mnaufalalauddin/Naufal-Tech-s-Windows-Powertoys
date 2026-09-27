using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.UI;

namespace Naufal_Windows_Tech_s_Powertoys;

// Presentation only. It consumes the existing report snapshot; never queries or changes disks.
internal sealed partial class DiskInfoView : Grid
{
    private readonly StackPanel _body = new() { Spacing = 14 };
    private readonly Grid _summary = new() { ColumnSpacing = 16, RowSpacing = 12 };
    private readonly StackPanel _cards = new() { Spacing = 10 };
    private readonly StackPanel _details = new() { Spacing = 6 };
    private readonly IReadOnlyList<DiskReportPage> _pages;
    private readonly List<Button> _buttons = new();
    private static readonly string[] DetailProperties = ["Device name", "Serial number", "Firmware", "Bus / interface", "Capacity", "Power-on hours", "Power cycle count", "Total host reads", "Total host writes", "Wear consumed (100% = estimated wear limit)"];
    private static bool IsPhysicalPage(DiskReportPage page) => page.IsDrive && !page.Rows.Any(row => row.Property == "Provider instance");
    internal int SelectedIndex { get; private set; }
    internal int PageCount => _pages.Count;
    internal IReadOnlyList<Button> SelectorButtons => _buttons;
    internal string SelectedTitle => _pages[SelectedIndex].Title;

    internal DiskInfoView(IReadOnlyList<SystemReportEntry> rows)
    {
        _pages = DiskReportPresentation.Pages(rows);
        RowSpacing = 12;
        RowDefinitions.Add(new() { Height = GridLength.Auto });
        RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
        StackPanel tabs = new() { Orientation = Orientation.Horizontal, Spacing = 8 };
        for (int i = 0; i < _pages.Count; i++)
        {
            int pageIndex = i;
            DiskReportPage page = _pages[i];
            // Selectable TextBlocks consume pointer presses before Button.Click.
            // Let the button own all hit testing inside its card.
            StackPanel label = new() { Spacing = 4, MaxWidth = 260, IsHitTestVisible = false };
            label.Children.Add(Text(page.Title, 13, true));
            if (IsPhysicalPage(page))
                label.Children.Add(Text(page.Value("Disk health") +
                    "  |  " + page.Value("Temperature"), 12));
            Button button = new() { Content = label, Padding = new Thickness(12, 8, 12, 8), MinHeight = 62 };
            button.Click += (_, _) => SelectPage(pageIndex);
            _buttons.Add(button);
            tabs.Children.Add(button);
        }
        Children.Add(new ScrollViewer
        {
            Content = tabs, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollMode = ScrollMode.Enabled, VerticalScrollMode = ScrollMode.Disabled
        });
        ScrollViewer content = new()
        {
            Content = _body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        SetRow(content, 1);
        Children.Add(content);
        _summary.SizeChanged += (_, _) => ArrangeSummary();
        Loaded += (_, _) => { ArrangeSummary(); UiDisplaySettings.Apply(this); };
        SelectPage(0);
    }

    internal void SelectPage(int index)
    {
        if (index < 0 || index >= _pages.Count) throw new ArgumentOutOfRangeException(nameof(index));
        SelectedIndex = index;
        DiskReportPage page = _pages[index];
        foreach (Button button in _buttons) button.BorderThickness = new Thickness(1);
        _buttons[index].BorderThickness = new Thickness(2);
        _body.Children.Clear();
        _body.Children.Add(Text(page.Title, 23, true));
        _summary.Children.Clear();
        _cards.Children.Clear();
        _details.Children.Clear();
        bool physical = IsPhysicalPage(page);
        if (physical)
        {
            string health = page.Value("Disk health");
            _cards.Children.Add(Card("DISK HEALTH", health, health.StartsWith("Good", StringComparison.Ordinal)
                ? Color.FromArgb(255, 0, 112, 60) : health.StartsWith("Bad", StringComparison.Ordinal)
                    ? Color.FromArgb(255, 185, 28, 28) : health.StartsWith("Caution", StringComparison.Ordinal)
                        ? Color.FromArgb(255, 180, 90, 0) : Color.FromArgb(255, 99, 109, 122)));
            _cards.Children.Add(Card("TEMPERATURE", page.Value("Temperature"), Color.FromArgb(255, 0, 103, 192)));
            foreach (string property in DetailProperties)
                _details.Children.Add(TableRow(property, page.Value(property)));
            _summary.Children.Add(_cards);
            _summary.Children.Add(_details);
            _body.Children.Add(_summary);
            ArrangeSummary();
            _body.Children.Add(Text("S.M.A.R.T. / reliability data", 18, true));
            _body.Children.Add(Text("Disk health uses device SMART data, not Windows status. Endurance remaining is an estimate, not a guarantee against failure. See Health source / assessment below; unavailable data remains Unknown.", 12));
        }
        _body.Children.Add(TableRow("ATTRIBUTE / PROPERTY", "REPORTED VALUE", true));
        foreach (SystemReportEntry row in page.Rows.Where(row => row.Smart is null))
        {
            if (physical && (DetailProperties.Contains(row.Property) || row.Property is "Temperature" or "Disk health")) continue;
            if (row.IsSection) _body.Children.Add(Text(row.Property.Trim('=').Trim(), 17, true));
            else _body.Children.Add(TableRow(row.Property, row.Value));
        }
        var attributes = page.Rows.Where(row => row.Smart is not null).Select(row => row.Smart!).ToArray();
        if (attributes.Length > 0)
        {
            StackPanel table = new();
            table.Children.Add(AttributeRow(["ID", "Attribute name", "Current", "Worst", "Threshold", "Raw values"], true));
            foreach (SmartAttribute attribute in attributes)
                table.Children.Add(AttributeRow([attribute.Id.ToString("X2"), attribute.Name,
                    attribute.Current.ToString(), attribute.Worst.ToString(),
                    attribute.Threshold?.ToString() ?? "—", attribute.Raw], false));
            _body.Children.Add(new ScrollViewer { Content = table,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                HorizontalScrollMode = ScrollMode.Enabled });
        }
        _body.Children.Add(Text("Read-only snapshot. Copy / Save TXT exports the complete disk report, including all devices, volumes and provider notes. Reopen Disk Info to refresh.", 12));
        UiDisplaySettings.Apply(this);
    }

    private void ArrangeSummary()
    {
        bool wide = _summary.ActualWidth >= 720 * UiDisplaySettings.GeometryScale;
        _summary.ColumnDefinitions.Clear();
        _summary.RowDefinitions.Clear();
        // Relative columns avoid double-scaling freshly recreated definitions.
        _summary.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        if (wide) _summary.ColumnDefinitions.Add(new() { Width = new GridLength(4, GridUnitType.Star) });
        _summary.RowDefinitions.Add(new() { Height = GridLength.Auto });
        if (!wide) _summary.RowDefinitions.Add(new() { Height = GridLength.Auto });
        SetColumn(_details, wide ? 1 : 0);
        SetRow(_details, wide ? 0 : 1);
    }

    private static TextBlock Text(string value, double size, bool strong = false) => new()
    {
        Text = value, FontSize = size, TextWrapping = TextWrapping.Wrap,
        IsTextSelectionEnabled = true,
        FontWeight = strong ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal
    };

    private static Border Card(string label, string value, Color accent)
    {
        StackPanel content = new() { Spacing = 8 };
        content.Children.Add(Text(label, 12, true));
        content.Children.Add(new TextBlock
        {
            Text = value, FontSize = 24, FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = new SolidColorBrush(accent), TextWrapping = TextWrapping.Wrap
        });
        return new Border
        {
            Padding = new Thickness(14), CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush(accent),
            Background = new SolidColorBrush(Color.FromArgb(255, 238, 243, 249)), Child = content
        };
    }

    private static Grid AttributeRow(string[] values, bool header)
    {
        Grid row = new() { MinWidth = 750, Padding = new Thickness(8), ColumnSpacing = 10,
            Background = new SolidColorBrush(header ? Color.FromArgb(255, 220, 230, 241) : Color.FromArgb(255, 238, 243, 249)) };
        double[] widths = [40, 220, 75, 75, 85, 145];
        for (int i = 0; i < values.Length; i++)
        {
            row.ColumnDefinitions.Add(new() { Width = new GridLength(widths[i]) });
            var text = Text(values[i], 13, header);
            SetColumn(text, i);
            row.Children.Add(text);
        }
        return row;
    }

    private static Grid TableRow(string label, string value, bool heading = false)
    {
        Grid row = new()
        {
            Padding = new Thickness(10, 7, 10, 7), ColumnSpacing = 12,
            Background = new SolidColorBrush(heading ? Color.FromArgb(255, 220, 230, 241) : Color.FromArgb(255, 238, 243, 249)),
            MinHeight = 32
        };
        row.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new() { Width = new GridLength(1.6, GridUnitType.Star) });
        row.Children.Add(Text(label, 13, heading));
        TextBlock detail = Text(value, 13, heading);
        SetColumn(detail, 1);
        row.Children.Add(detail);
        return row;
    }
}
