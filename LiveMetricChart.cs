using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using Windows.Foundation;
using Windows.UI;

namespace Naufal_Windows_Tech_s_Powertoys;

// Composed native WinUI shapes: no reflection-based chart library, worker, or
// independent timer. Plot coordinates are rebuilt after layout/theme changes.
internal sealed class LiveMetricChart
{
    private readonly LiveMetricHistory _history = new();
    private readonly Canvas _plot = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly Grid _plotHost = new() { Height = 86 };
    private readonly TextBlock _maximum = new() { FontSize = 10, HorizontalAlignment = HorizontalAlignment.Right };
    private readonly TextBlock _missing = new()
    {
        Text = "Unavailable", FontSize = 10, TextWrapping = TextWrapping.Wrap,
        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
    };
    private readonly bool _network;
    private readonly byte _kind;
    internal Grid Root { get; } = new() { RowSpacing = 3, IsHitTestVisible = false, FlowDirection = FlowDirection.LeftToRight };

    internal LiveMetricChart(byte kind, bool network = false)
    {
        _kind = kind;
        _network = network;
        Root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        Root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        Root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        Grid top = new();
        top.RowDefinitions.Add(new() { Height = GridLength.Auto });
        top.RowDefinitions.Add(new() { Height = GridLength.Auto });
        if (network)
        {
            // Solid RX / dashed TX also distinguish the series without color.
            top.Children.Add(new TextBlock { Text = "RX ━  TX ┄", FontSize = 10 });
            Grid.SetRow(_maximum, 1);
        }
        top.Children.Add(_maximum);
        Root.Children.Add(top);
        Grid.SetRow(_plotHost, 1);
        _plotHost.Children.Add(_plot);
        _plotHost.Children.Add(_missing);
        Root.Children.Add(_plotHost);
        Grid axis = new();
        axis.Children.Add(new TextBlock { Text = "60 seconds", FontSize = 10 });
        axis.Children.Add(new TextBlock { Text = "0", FontSize = 10, HorizontalAlignment = HorizontalAlignment.Right });
        Grid.SetRow(axis, 2);
        Root.Children.Add(axis);
        _plotHost.SizeChanged += (_, _) => Render();
        Root.Loaded += (_, _) => Render();
    }

    internal void Add(double seconds, double? primary, double? secondary = null)
    {
        _history.Add(seconds, primary, secondary);
        Render();
    }

    internal void Render()
    {
        bool dark = UiDisplaySettings.Theme == ElementTheme.Dark;
        Color foreground = dark ? Color.FromArgb(255, 190, 207, 228) : Color.FromArgb(255, 49, 70, 95);
        _maximum.Foreground = new SolidColorBrush(foreground);
        _missing.Foreground = new SolidColorBrush(foreground);
        double ceiling = _network ? _history.NetworkCeiling() : 100;
        _maximum.Text = _network ? $"{ceiling:0.##} Mbps" : "100%";
        LiveMetricSample? latest = _history.Latest;
        _missing.Visibility = latest is null || (latest.Value.Primary is null && (!_network || latest.Value.Secondary is null))
            ? Visibility.Visible : Visibility.Collapsed;
        _plot.Children.Clear();
        double width = _plotHost.ActualWidth, height = _plotHost.ActualHeight;
        if (!double.IsFinite(width) || !double.IsFinite(height) || width < 4 || height < 4) return;
        // Keep strokes inside the plot bounds at every supported text/UI scale.
        _plot.Clip = new RectangleGeometry { Rect = new Rect(0, 0, width, height) };
        Brush grid = new SolidColorBrush(dark ? Color.FromArgb(255, 58, 77, 99) : Color.FromArgb(255, 195, 210, 222));
        for (int i = 0; i <= 6; i++)
            _plot.Children.Add(new Line { X1 = 1 + (width - 2) * i / 6, X2 = 1 + (width - 2) * i / 6,
                Y1 = 1, Y2 = height - 1, Stroke = grid, StrokeThickness = 1 });
        for (int i = 0; i <= 4; i++)
            _plot.Children.Add(new Line { X1 = 1, X2 = width - 1, Y1 = 1 + (height - 2) * i / 4,
                Y2 = 1 + (height - 2) * i / 4, Stroke = grid, StrokeThickness = 1 });
        Color primary = _kind switch
        {
            1 => dark ? Color.FromArgb(255, 203, 160, 247) : Color.FromArgb(255, 133, 72, 177),
            2 => dark ? Color.FromArgb(255, 87, 214, 145) : Color.FromArgb(255, 0, 130, 75),
            3 => dark ? Color.FromArgb(255, 255, 190, 80) : Color.FromArgb(255, 173, 100, 0),
            _ => dark ? Color.FromArgb(255, 93, 188, 255) : Color.FromArgb(255, 0, 120, 190)
        };
        Draw(_history.Project(width - 2, height - 2, ceiling), primary, height - 1, false);
        if (_network)
            Draw(_history.Project(width - 2, height - 2, ceiling, true),
                dark ? Color.FromArgb(255, 93, 188, 255) : Color.FromArgb(255, 0, 110, 180), height - 1, true);
    }

    private void Draw(IReadOnlyList<IReadOnlyList<LiveGraphPoint>> segments, Color color, double bottom, bool dashed)
    {
        foreach (IReadOnlyList<LiveGraphPoint> segment in segments)
        {
            if (segment.Count == 0) continue;
            PointCollection points = new();
            foreach (LiveGraphPoint point in segment) points.Add(new Point(point.X + 1, point.Y + 1));
            if (!dashed && segment.Count > 1)
            {
                PointCollection fill = new() { new Point(points[0].X, bottom) };
                foreach (Point point in points) fill.Add(point);
                fill.Add(new Point(points[points.Count - 1].X, bottom));
                _plot.Children.Add(new Polygon { Points = fill, Fill = new SolidColorBrush(Color.FromArgb(28, color.R, color.G, color.B)) });
            }
            if (segment.Count == 1)
            {
                Ellipse dot = new() { Width = 3, Height = 3, Fill = new SolidColorBrush(color) };
                Canvas.SetLeft(dot, points[0].X - 1.5);
                Canvas.SetTop(dot, points[0].Y - 1.5);
                _plot.Children.Add(dot);
            }
            else
            {
                Polyline line = new() { Points = points, Stroke = new SolidColorBrush(color), StrokeThickness = 1.6 };
                if (dashed) line.StrokeDashArray = new DoubleCollection { 3, 2 };
                _plot.Children.Add(line);
            }
        }
    }
}
