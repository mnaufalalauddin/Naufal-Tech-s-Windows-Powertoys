using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Naufal_Windows_Tech_s_Powertoys;

// Read-only scans share their owning page/window. Mutation progress is separate.
internal sealed class InlineAnalysisProgress
{
    private readonly Dictionary<string, (string Name, string State, string Detail)> _items;
    private readonly ProgressBar _bar = new() { Minimum = 0, Height = 4 };
    private readonly TextBlock _summary = new() { TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };
    private readonly TextBlock _details = new() { TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };
    private readonly Expander _expander;
    internal StackPanel View { get; } = new() { Spacing = 4 };
    internal string DetailsText => _details.Text;

    internal InlineAnalysisProgress(ContentControl host, IReadOnlyList<CatalogProgressItem> items)
    {
        _items = items.ToDictionary(i => i.Id, i => (i.Name, "Waiting", ""), StringComparer.OrdinalIgnoreCase);
        _bar.Maximum = Math.Max(1, items.Count);
        _expander = new Expander
        {
            Header = "Analysis details",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Content = new ScrollViewer
            {
                Content = _details, MaxHeight = 140, IsTabStop = true,
                VerticalScrollMode = ScrollMode.Enabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollMode = ScrollMode.Disabled,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            }
        };
        View.Children.Add(_summary);
        View.Children.Add(_bar);
        View.Children.Add(_expander);
        // Bound even long errors/expanded details so the owning window's action
        // footer cannot be pushed off-screen at small sizes or large text scales.
        host.Content = new ScrollViewer
        {
            Content = View, MaxHeight = 200, IsTabStop = true,
            VerticalScrollMode = ScrollMode.Enabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollMode = ScrollMode.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        }; // Replace previous scan, never accumulate panels.
        host.Visibility = Visibility.Visible;
        UiDisplaySettings.Apply(View);
        _summary.Text = "Reading current state...";
        Render();
    }

    internal void Show() { } // No top-level window or automatic focus change.
    internal void BeginItem(string id, string detail) => Set(id, "Reading", detail);
    internal void VerifyItem(string id, string detail = "Verifying result") => Set(id, "Verifying", detail);
    internal void CompleteItem(string id, bool success, string detail) => Set(id, success ? "Verified" : "Failed / unverified", detail);
    internal void UnavailableItem(string id, string detail) => Set(id, "Unavailable", detail);
    internal void UpdateOverall(int completed, string detail)
    {
        _bar.Value = Math.Clamp(completed, 0, _items.Count);
        _summary.Text = detail;
    }
    internal void Complete(bool success, string detail)
    {
        _bar.Value = _bar.Maximum; // Processed, not a success percentage.
        _summary.Text = (success ? "Analysis complete. " : "Analysis failed or incomplete. ") + detail;
        _expander.IsExpanded = !success;
    }
    private void Set(string id, string state, string detail)
    {
        var item = _items[id];
        _items[id] = (item.Name, state, detail);
        Render();
    }
    private void Render() => _details.Text = string.Join("\n\n", _items.Values.Select(i => $"{i.Name} — {i.State}\n{i.Detail}"));
}
