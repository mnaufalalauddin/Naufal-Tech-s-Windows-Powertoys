using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using Windows.UI;
using IOPath = System.IO.Path;

namespace Naufal_Windows_Tech_s_Powertoys
{
    /// <summary>
    /// Applies the user-selected theme and UI scale to authored controls.
    /// Baselines are captured once so repeated changes never accumulate rounding
    /// errors. Logical children and item containers are traversed as well as the
    /// visible tree, which keeps virtualized report/catalog rows theme-correct.
    /// </summary>
    internal static class UiDisplaySettings
    {
        private sealed class ElementBaseline
        {
            public double? TextBlockSize { get; set; }

            public double? ControlSize { get; set; }

            public double Width { get; set; }

            public double Height { get; set; }

            public double MinWidth { get; set; }

            public double MinHeight { get; set; }

            public double MaxWidth { get; set; }

            public double MaxHeight { get; set; }

            public Thickness Margin { get; set; }

            public Thickness? ControlPadding { get; set; }

            public Thickness? BorderPadding { get; set; }

            public double? StackSpacing { get; set; }

            public double? GridRowSpacing { get; set; }

            public double? GridColumnSpacing { get; set; }

            public Brush? Foreground { get; set; }

            public bool HasAuthoredForeground { get; set; }

            public bool HasAuthoredBackground { get; set; }

            public bool HasAuthoredBorderBrush { get; set; }

            public Brush? Background { get; set; }

            public Brush? BorderBrush { get; set; }

            public Brush? ShapeFill { get; set; }

            public Brush? ShapeStroke { get; set; }
        }

        private sealed class ColumnBaseline
        {
            public GridLength Width { get; set; }

            public double MinWidth { get; set; }

            public double MaxWidth { get; set; }
        }

        private sealed class RowBaseline
        {
            public GridLength Height { get; set; }

            public double MinHeight { get; set; }

            public double MaxHeight { get; set; }
        }

        private static readonly int[] SupportedPercentages =
            { 25, 50, 75, 100, 125, 150, 175, 200 };

        private static readonly ConditionalWeakTable<FrameworkElement, ElementBaseline> ElementBaselines = new();
        private static readonly ConditionalWeakTable<FrameworkElement, object> RecoveryControls = new();
        private static readonly ConditionalWeakTable<ColumnDefinition, ColumnBaseline> ColumnBaselines = new();
        private static readonly ConditionalWeakTable<RowDefinition, RowBaseline> RowBaselines = new();
        private static readonly string PreferenceDirectory = AppDataPaths.SettingsDirectory;

        static UiDisplaySettings()
        {
            LoadPreferences();
        }

        public static ElementTheme Theme { get; private set; } = ElementTheme.Light;

        public static int TextScalePercent { get; private set; } = 100;

        public static string LanguageCode { get; private set; } = "en";

        public static double GeometryScale => TextScalePercent switch
        {
            25 => 0.70,
            50 => 0.80,
            75 => 0.90,
            100 => 1.00,
            125 => 1.14,
            150 => 1.28,
            175 => 1.42,
            200 => 1.56,
            _ => 1.00
        };

        public static event EventHandler? Changed;

        // Only the theme/scaling recovery controls opt in, not catalog content.
        internal static void KeepRecoveryControlUsable(FrameworkElement element) =>
            RecoveryControls.GetValue(element, static _ => new object());

        public static void ToggleTheme()
        {
            Theme = Theme == ElementTheme.Dark
                ? ElementTheme.Light
                : ElementTheme.Dark;
            SavePreferences();
            Changed?.Invoke(null, EventArgs.Empty);
        }

        public static void SetTextScale(int percent)
        {
            if (Array.IndexOf(SupportedPercentages, percent) < 0 ||
                TextScalePercent == percent)
            {
                return;
            }

            TextScalePercent = percent;
            SavePreferences();
            Changed?.Invoke(null, EventArgs.Empty);
        }

        public static void SetLanguage(string languageCode)
        {
            string normalized = UiTranslation.NormalizeLanguageCode(languageCode);
            if (string.Equals(LanguageCode, normalized, StringComparison.Ordinal))
            {
                return;
            }

            LanguageCode = normalized;
            SavePreferences();
            Changed?.Invoke(null, EventArgs.Empty);
        }

        public static void Apply(FrameworkElement root)
        {
            root.RequestedTheme = Theme;
            HashSet<DependencyObject> visited = new(ReferenceEqualityComparer.Instance);
            ApplyRecursively(
                root,
                Theme == ElementTheme.Dark,
                TextScalePercent / 100d,
                GeometryScale,
                visited);
            UiTranslation.Apply(root, LanguageCode);
        }

        private static void ApplyRecursively(
            DependencyObject element,
            bool dark,
            double fontScale,
            double geometryScale,
            ISet<DependencyObject> visited)
        {
            if (!visited.Add(element))
            {
                return;
            }

            if (element is FrameworkElement frameworkElement)
            {
                ElementBaseline baseline = ElementBaselines.GetValue(
                    frameworkElement,
                    CaptureBaseline);
                ApplyPalette(frameworkElement, baseline, dark);
                ApplyScale(frameworkElement, baseline, fontScale, geometryScale);
            }

            if (element is Grid grid)
            {
                ApplyGridDefinitions(grid, geometryScale);
            }

            foreach (DependencyObject child in EnumerateAuthoredChildren(element))
            {
                ApplyRecursively(child, dark, fontScale, geometryScale, visited);
            }
        }

        private static ElementBaseline CaptureBaseline(FrameworkElement element)
        {
            ElementBaseline baseline = new()
            {
                Width = element.Width,
                Height = element.Height,
                MinWidth = element.MinWidth,
                MinHeight = element.MinHeight,
                MaxWidth = element.MaxWidth,
                MaxHeight = element.MaxHeight,
                Margin = element.Margin
            };

            if (element is TextBlock textBlock)
            {
                baseline.TextBlockSize = textBlock.FontSize;
                baseline.Foreground = textBlock.Foreground;
                baseline.HasAuthoredForeground = HasLocalValue(textBlock, TextBlock.ForegroundProperty);
            }
            if (element is Control control)
            {
                baseline.ControlSize = control.FontSize;
                baseline.ControlPadding = control.Padding;
                baseline.Foreground = control.Foreground;
                baseline.HasAuthoredForeground = HasLocalValue(control, Control.ForegroundProperty);
                baseline.HasAuthoredBackground = HasLocalValue(control, Control.BackgroundProperty);
                baseline.HasAuthoredBorderBrush = HasLocalValue(control, Control.BorderBrushProperty);
                baseline.Background = control.Background;
                baseline.BorderBrush = control.BorderBrush;
            }
            if (element is Panel panel)
            {
                baseline.Background = panel.Background;
            }
            if (element is Border border)
            {
                baseline.BorderPadding = border.Padding;
                baseline.Background = border.Background;
                baseline.BorderBrush = border.BorderBrush;
            }
            if (element is StackPanel stackPanel)
            {
                baseline.StackSpacing = stackPanel.Spacing;
            }
            if (element is Grid grid)
            {
                baseline.GridRowSpacing = grid.RowSpacing;
                baseline.GridColumnSpacing = grid.ColumnSpacing;
            }
            if (element is Shape shape)
            {
                baseline.ShapeFill = shape.Fill;
                baseline.ShapeStroke = shape.Stroke;
            }

            return baseline;
        }

        private static void ApplyScale(
            FrameworkElement element,
            ElementBaseline baseline,
            double fontScale,
            double geometryScale)
        {
            if (baseline.TextBlockSize.HasValue && element is TextBlock textBlock)
            {
                textBlock.FontSize = Math.Max(4d, baseline.TextBlockSize.Value * fontScale);
            }
            if (baseline.ControlSize.HasValue && element is Control control)
            {
                control.FontSize = Math.Max(4d, baseline.ControlSize.Value * fontScale);
            }

            element.Width = ScaleDimension(baseline.Width, geometryScale);
            element.Height = ScaleDimension(baseline.Height, geometryScale);
            element.MinWidth = ScaleMinimum(baseline.MinWidth, geometryScale);
            element.MinHeight = ScaleMinimum(baseline.MinHeight, geometryScale);
            element.MaxWidth = ScaleMaximum(baseline.MaxWidth, geometryScale);
            element.MaxHeight = ScaleMaximum(baseline.MaxHeight, geometryScale);
            element.Margin = ScaleThickness(baseline.Margin, geometryScale);

            if (baseline.ControlPadding.HasValue && element is Control paddedControl)
            {
                paddedControl.Padding = ScaleThickness(
                    baseline.ControlPadding.Value,
                    geometryScale);
            }
            if (baseline.BorderPadding.HasValue && element is Border border)
            {
                border.Padding = ScaleThickness(
                    baseline.BorderPadding.Value,
                    geometryScale);
            }
            if (baseline.StackSpacing.HasValue && element is StackPanel stackPanel)
            {
                stackPanel.Spacing = baseline.StackSpacing.Value * geometryScale;
            }
            if (element is Grid grid)
            {
                if (baseline.GridRowSpacing.HasValue)
                {
                    grid.RowSpacing = baseline.GridRowSpacing.Value * geometryScale;
                }
                if (baseline.GridColumnSpacing.HasValue)
                {
                    grid.ColumnSpacing = baseline.GridColumnSpacing.Value * geometryScale;
                }
            }

            // A fixed-height control must still be tall enough for its scaled font
            // and vertical padding. This prevents native checkbox/toggle templates
            // from being clipped at 150-200%.
            if (element is Control sizedControl &&
                baseline.ControlSize.HasValue &&
                !double.IsNaN(baseline.Height))
            {
                double paddingHeight = sizedControl.Padding.Top + sizedControl.Padding.Bottom;
                double fontAwareHeight = sizedControl.FontSize * 1.55 + paddingHeight + 4;
                sizedControl.Height = Math.Max(sizedControl.Height, fontAwareHeight);
            }

            if (element is CheckBox checkBox)
            {
                double templateHeight = Math.Max(
                    24d * geometryScale,
                    checkBox.FontSize * 1.45 + 8d);
                checkBox.MinHeight = Math.Max(checkBox.MinHeight, templateHeight);
                checkBox.MinWidth = Math.Max(checkBox.MinWidth, 86d * geometryScale);
            }
            else if (element is ToggleSwitch toggleSwitch)
            {
                double templateHeight = Math.Max(
                    28d * geometryScale,
                    toggleSwitch.FontSize * 1.45 + 8d);
                toggleSwitch.MinHeight = Math.Max(toggleSwitch.MinHeight, templateHeight);
                toggleSwitch.MinWidth = Math.Max(toggleSwitch.MinWidth, 112d * geometryScale);
            }

            if (RecoveryControls.TryGetValue(element, out _))
            {
                if (element is TextBlock glyph)
                    glyph.FontSize = Math.Max(glyph.FontSize, HeaderLayoutPolicy.RecoveryFontMinimum);
                if (element is Control recovery)
                {
                    recovery.FontSize = Math.Max(recovery.FontSize, HeaderLayoutPolicy.RecoveryFontMinimum);
                    recovery.MinWidth = Math.Max(recovery.MinWidth, HeaderLayoutPolicy.RecoveryTargetMinimum);
                    recovery.MinHeight = Math.Max(recovery.MinHeight, HeaderLayoutPolicy.RecoveryTargetMinimum);
                    if (!double.IsNaN(recovery.Width)) recovery.Width = Math.Max(recovery.Width, recovery.MinWidth);
                    if (!double.IsNaN(recovery.Height)) recovery.Height = Math.Max(recovery.Height, recovery.MinHeight);
                }
            }
        }

        private static void ApplyGridDefinitions(Grid grid, double geometryScale)
        {
            foreach (ColumnDefinition definition in grid.ColumnDefinitions)
            {
                ColumnBaseline baseline = ColumnBaselines.GetValue(
                    definition,
                    item => new ColumnBaseline
                    {
                        Width = item.Width,
                        MinWidth = item.MinWidth,
                        MaxWidth = item.MaxWidth
                    });
                definition.Width = ScaleGridLength(baseline.Width, geometryScale);
                definition.MinWidth = ScaleMinimum(baseline.MinWidth, geometryScale);
                definition.MaxWidth = ScaleMaximum(baseline.MaxWidth, geometryScale);
            }

            foreach (RowDefinition definition in grid.RowDefinitions)
            {
                RowBaseline baseline = RowBaselines.GetValue(
                    definition,
                    item => new RowBaseline
                    {
                        Height = item.Height,
                        MinHeight = item.MinHeight,
                        MaxHeight = item.MaxHeight
                    });
                definition.Height = ScaleGridLength(baseline.Height, geometryScale);
                definition.MinHeight = ScaleMinimum(baseline.MinHeight, geometryScale);
                definition.MaxHeight = ScaleMaximum(baseline.MaxHeight, geometryScale);
            }
        }

        private static IEnumerable<DependencyObject> EnumerateAuthoredChildren(
            DependencyObject element)
        {
            if (element is Panel panel)
            {
                foreach (UIElement child in panel.Children)
                {
                    yield return child;
                }
            }

            if (element is Border border && border.Child is DependencyObject borderChild)
            {
                yield return borderChild;
            }

            if (element is ContentControl contentControl &&
                contentControl.Content is DependencyObject content)
            {
                yield return content;
            }
            else if (element is ContentPresenter contentPresenter &&
                     contentPresenter.Content is DependencyObject presentedContent)
            {
                yield return presentedContent;
            }

            if (element is ItemsControl itemsControl)
            {
                foreach (object item in itemsControl.Items)
                {
                    if (item is DependencyObject dependencyItem)
                    {
                        yield return dependencyItem;
                    }
                }
            }
        }

        // Default/inherited text and shared button styles follow RequestedTheme.
        // Only explicit local colors belong to this canonical-color mapper.
        // In particular, never freeze implicit/ThemeResource style brushes or
        // prevent a profile button from changing styles after first display.
        private static bool HasLocalValue(FrameworkElement element, DependencyProperty property) =>
            element.ReadLocalValue(property) != DependencyProperty.UnsetValue;

        private static void ApplyPalette(
            FrameworkElement element,
            ElementBaseline baseline,
            bool dark)
        {
            if (element is TextBlock textBlock && baseline.HasAuthoredForeground)
            {
                textBlock.Foreground = textBlock.Tag as string == "AppRemovalRecommendationInk"
                    ? baseline.Foreground : MapForeground(baseline.Foreground, dark);
            }
            if (element is Control control)
            {
                if (baseline.HasAuthoredForeground)
                    control.Foreground = MapForeground(baseline.Foreground, dark);
                if (control is not Button || baseline.HasAuthoredBackground)
                    control.Background = MapBackground(baseline.Background, dark);
                if (control is not Button || baseline.HasAuthoredBorderBrush)
                    control.BorderBrush = MapBorder(baseline.BorderBrush, dark);
            }
            if (element is Panel panel)
            {
                panel.Background = MapBackground(baseline.Background, dark);
            }
            if (element is Border border)
            {
                border.Background = MapBackground(baseline.Background, dark);
                border.BorderBrush = MapBorder(baseline.BorderBrush, dark);
            }
            if (element is Shape shape)
            {
                shape.Fill = MapBackground(baseline.ShapeFill, dark);
                shape.Stroke = MapBorder(baseline.ShapeStroke, dark);
            }
        }

        private static Brush? MapBackground(Brush? brush, bool dark)
        {
            if (!dark || brush is not SolidColorBrush solid || solid.Color.A == 0)
            {
                return brush;
            }

            uint value = ToArgb(solid.Color);
            uint mapped = value switch
            {
                0xFFF4F7FC => 0xFF0F1722,
                0xFFF9FBFE or 0xFFF7F9FC => 0xFF151F2C,
                0xFFFFFFFF => 0xFF182332,
                0xFFEEF3F9 or 0xFFEDF2F8 => 0xFF1D2938,
                0xFFF8F2E2 => 0xFF342A18,
                0xFFB0C4DE => 0xFF284A70,
                _ when RelativeLuminance(solid.Color) > 0.78 => 0xFF182332,
                _ => value
            };
            return ToBrush(mapped);
        }

        private static Brush? MapForeground(Brush? brush, bool dark)
        {
            if (!dark || brush is not SolidColorBrush solid || solid.Color.A == 0)
            {
                return brush;
            }

            uint value = ToArgb(solid.Color);
            uint mapped = value switch
            {
                0xFF0B1520 or 0xFF121C28 => 0xFFF3F6FA,
                0xFF33465F or 0xFF34455C or 0xFF31465F => 0xFFB9C6D8,
                0xFF315486 or 0xFF1B4D91 => 0xFF9AC2FF,
                0xFF004F92 or 0xFF005FB8 or 0xFF002F6C => 0xFF75B7FF,
                0xFF008A3C or 0xFF00703C or 0xFF009951 or 0xFF107C10 => 0xFF6CCB5F,
                0xFFC15B00 or 0xFFBE5705 => 0xFFFFB45A,
                0xFFB91C1C or 0xFFB91C23 or 0xFFC42B1C => 0xFFFF8A8A,
                _ when RelativeLuminance(solid.Color) < 0.32 => 0xFFF3F6FA,
                _ => value
            };
            return ToBrush(mapped);
        }

        private static Brush? MapBorder(Brush? brush, bool dark)
        {
            if (!dark || brush is not SolidColorBrush solid || solid.Color.A == 0)
            {
                return brush;
            }

            uint value = ToArgb(solid.Color);
            uint mapped = value switch
            {
                0xFF7F8C9D => 0xFF53637A,
                0xFFBBC7D5 => 0xFF45566E,
                0xFFD2DAE5 or 0xFFD3DCE8 => 0xFF34445A,
                _ when RelativeLuminance(solid.Color) > 0.72 => 0xFF45566E,
                _ => value
            };
            return ToBrush(mapped);
        }

        private static GridLength ScaleGridLength(GridLength value, double scale)
        {
            return value.GridUnitType == GridUnitType.Pixel
                ? new GridLength(Math.Max(0, value.Value * scale))
                : value;
        }

        private static double ScaleDimension(double value, double scale)
        {
            return double.IsNaN(value) ? value : Math.Max(0, value * scale);
        }

        private static double ScaleMinimum(double value, double scale)
        {
            return value <= 0 ? value : value * scale;
        }

        private static double ScaleMaximum(double value, double scale)
        {
            return double.IsInfinity(value) || value <= 0 ? value : value * scale;
        }

        private static Thickness ScaleThickness(Thickness value, double scale)
        {
            return new Thickness(
                value.Left * scale,
                value.Top * scale,
                value.Right * scale,
                value.Bottom * scale);
        }

        private static double RelativeLuminance(Color color)
        {
            static double Channel(byte channel)
            {
                double value = channel / 255d;
                return value <= 0.03928
                    ? value / 12.92
                    : Math.Pow((value + 0.055) / 1.055, 2.4);
            }

            return 0.2126 * Channel(color.R) +
                   0.7152 * Channel(color.G) +
                   0.0722 * Channel(color.B);
        }

        private static Brush ToBrush(uint value)
        {
            return new SolidColorBrush(Color.FromArgb(
                (byte)(value >> 24),
                (byte)(value >> 16),
                (byte)(value >> 8),
                (byte)value));
        }

        private static uint ToArgb(Color color)
        {
            return ((uint)color.A << 24) |
                   ((uint)color.R << 16) |
                   ((uint)color.G << 8) |
                   color.B;
        }

        private static void LoadPreferences()
        {
            try
            {
                string themePath = IOPath.Combine(PreferenceDirectory, "ui-theme.txt");
                if (File.Exists(themePath) &&
                    string.Equals(File.ReadAllText(themePath).Trim(), "Dark", StringComparison.OrdinalIgnoreCase))
                {
                    Theme = ElementTheme.Dark;
                }

                string scalePath = IOPath.Combine(PreferenceDirectory, "ui-font-scale.txt");
                if (File.Exists(scalePath) &&
                    int.TryParse(File.ReadAllText(scalePath).Trim(), out int scale) &&
                    Array.IndexOf(SupportedPercentages, scale) >= 0)
                {
                    TextScalePercent = scale;
                }

                string languagePath = IOPath.Combine(PreferenceDirectory, "ui-language.txt");
                if (File.Exists(languagePath))
                {
                    LanguageCode = UiTranslation.NormalizeLanguageCode(
                        File.ReadAllText(languagePath).Trim());
                }
            }
            catch
            {
                // Preference failures must never prevent the application from starting.
            }
        }

        private static void SavePreferences()
        {
            try
            {
                Directory.CreateDirectory(PreferenceDirectory);
                File.WriteAllText(
                    IOPath.Combine(PreferenceDirectory, "ui-theme.txt"),
                    Theme.ToString());
                File.WriteAllText(
                    IOPath.Combine(PreferenceDirectory, "ui-font-scale.txt"),
                    TextScalePercent.ToString(System.Globalization.CultureInfo.InvariantCulture));
                File.WriteAllText(
                    IOPath.Combine(PreferenceDirectory, "ui-language.txt"),
                    LanguageCode);
            }
            catch
            {
                // The UI setting still applies for the current session.
            }
        }
    }
}
