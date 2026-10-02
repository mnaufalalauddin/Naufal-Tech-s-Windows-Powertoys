using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;
using Windows.Graphics;

namespace Naufal_Windows_Tech_s_Powertoys;

public sealed partial class MainWindow
{
    private async Task CheckDialogScrollingAsync()
    {
        string message = string.Join("\n\n", Enumerable.Range(1, 32).Select(i =>
            $"• Tweak {i}\nA long explanation with warnings, recovery details and restart requirements. " +
            "Read every selected option before confirming this synthetic test.")) + "\nEND OF CONFIRMATION";
        foreach (ElementTheme theme in new[] { ElementTheme.Light, ElementTheme.Dark })
        foreach (int scale in new[] { 25, 100, 200 })
        {
            _case = $"long confirmation theme={theme}, scale={scale}";
            if (UiDisplaySettings.Theme != theme) UiDisplaySettings.ToggleTheme();
            UiDisplaySettings.SetTextScale(scale);
            ScrollViewer viewer = DialogMessageContent.Create(message);
            ToolWindow dialog = new(this, "Advanced Windows Tweaks — Apply selected", viewer,
                primaryButtonText: "Apply selected", closeButtonText: "Cancel",
                initialWidth: 680, initialHeight: 400, minimumWidth: 460, minimumHeight: 300);
            _ = dialog.ShowAsync();
            try
            {
                foreach (var size in new[] { new SizeInt32(680, 400), new SizeInt32(480, 320), new SizeInt32(1000, 700) })
                {
                    _case = $"long confirmation theme={theme}, scale={scale}, size={size.Width}x{size.Height}";
                    dialog.AppWindow.Resize(size);
                    await Task.Delay(60);
                    var root = (FrameworkElement)dialog.Content;
                    root.UpdateLayout();
                    Check(viewer.ViewportHeight > 0 && (scale < 100 || viewer.ScrollableHeight > 0),
                        $"long confirmation has bounded viewport (height={viewer.ViewportHeight}, scroll={viewer.ScrollableHeight}); 25% text may fit without overflow");
                    Check(viewer.ScrollableWidth < 1, "wrapped confirmation needs no horizontal scroll");
                    Check(((TextBlock)viewer.Content).Text == message, "all 32 warnings retained without truncation");
                    Check(viewer.IsTabStop && viewer.VerticalScrollMode == ScrollMode.Enabled, "scroll control supports focus and scrolling");
                    viewer.ChangeView(null, viewer.ScrollableHeight, null, disableAnimation: true);
                    await Task.Delay(40);
                    Check(Math.Abs(viewer.VerticalOffset - viewer.ScrollableHeight) < 2, "last warning reachable");
                    Rect viewport = viewer.TransformToVisual(root).TransformBounds(new Rect(0, 0, viewer.ActualWidth, viewer.ActualHeight));
                    foreach (Button button in new[] { dialog.PrimaryButton, dialog.CloseButton })
                    {
                        Rect bounds = button.TransformToVisual(root).TransformBounds(new Rect(0, 0, button.ActualWidth, button.ActualHeight));
                        Check(bounds.Top >= viewport.Bottom - 1 && bounds.Bottom <= root.ActualHeight + 1, "Apply/Cancel stay outside the scroll viewport");
                        Check(bounds.Left >= -1 && bounds.Right <= root.ActualWidth + 1, "footer buttons remain inside window");
                    }
                    viewer.ChangeView(null, 0, null, disableAnimation: true);
                    await Task.Delay(30);
                    Check(viewer.VerticalOffset < 1, "scroll can return to the first warning");
                }
            }
            finally { dialog.Close(); }
        }

        _case = "inline scan completion and retry";
        ContentControl host = new();
        InlineAnalysisProgress scan = new(host, new[] { new CatalogProgressItem("a", "Tweak A"), new CatalogProgressItem("b", "Tweak B") });
        scan.BeginItem("a", "Reading"); scan.CompleteItem("a", false, "Provider access denied");
        scan.UnavailableItem("b", "Not supported on this OS"); scan.UpdateOverall(2, "2/2 processed");
        scan.Complete(false, "Review failed rows");
        Check(scan.DetailsText.Contains("Provider access denied") && scan.DetailsText.Contains("Not supported"), "inline failures/unavailability remain readable");
        Check(scan.View.Children.OfType<Expander>().Single().IsExpanded, "failed scan expands details without a popup");
        InlineAnalysisProgress retry = new(host, new[] { new CatalogProgressItem("a", "Tweak A") });
        retry.CompleteItem("a", true, "Verified current state"); retry.Complete(true, "1/1 processed");
        Check(ReferenceEquals(((ScrollViewer)host.Content).Content, retry.View), "reload replaces prior inline view");
        Check(!retry.DetailsText.Contains("access denied"), "new scan does not retain stale failures");

        UiDisplaySettings.SetTextScale(100);
        ToolWindow tool = new(this, "Inline analysis test", new TextBlock { Text = "Existing tool content" },
            primaryButtonText: "Apply selected", initialWidth: 680, initialHeight: 480);
        _ = tool.ShowAsync();
        try
        {
            var embedded = tool.CreateAnalysisProgress(new[] { new CatalogProgressItem("a", "Tweak A") });
            embedded.CompleteItem("a", false, message);
            embedded.Complete(false, string.Concat(Enumerable.Repeat("Long provider failure. ", 20)));
            foreach (int scale in new[] { 100, 200 })
            {
                UiDisplaySettings.SetTextScale(scale);
                tool.AppWindow.Resize(new SizeInt32(680, 480));
                await Task.Delay(60);
                var root = (FrameworkElement)tool.Content;
                root.UpdateLayout();
                var actionBounds = tool.PrimaryButton.TransformToVisual(root).TransformBounds(new Rect(0, 0, tool.PrimaryButton.ActualWidth, tool.PrimaryButton.ActualHeight));
                Check(actionBounds.Top >= 0 && actionBounds.Bottom <= root.ActualHeight + 1, "expanded long inline failures never hide action footer");
                Check(embedded.DetailsText.Contains("END OF CONFIRMATION"), "inline details do not truncate provider output");
            }
        }
        finally { tool.Close(); }
    }
}
