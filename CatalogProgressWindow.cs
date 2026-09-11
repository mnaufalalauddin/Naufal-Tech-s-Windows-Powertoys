using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI;
using Windows.ApplicationModel.DataTransfer;

namespace Naufal_Windows_Tech_s_Powertoys
{
    internal sealed record CatalogProgressItem(string Id, string Name);

    /// <summary>
    /// Modeless, resizable progress surface shared by every catalog operation.
    /// Per-item progress lives here instead of inside the catalog cards, and the
    /// overall progress remains anchored at the bottom of the window.
    /// </summary>
    internal sealed class CatalogProgressWindow
    {
        private sealed record ItemVisuals(
            string Name,
            ProgressBar Bar,
            TextBlock Percent,
            TextBlock State,
            TextBlock Detail);

        private readonly ToolWindow _window;
        private readonly Dictionary<string, ItemVisuals> _items;
        private readonly CatalogProgressState _progress;
        private readonly ProgressBar _overallBar;
        private readonly TextBlock _overallPercent;
        private readonly TextBlock _overallDetail;
        private readonly TextBlock _elapsedText;
        private readonly CatalogAvailabilityBadge _availabilityBadge = new();
        private readonly Stopwatch _stopwatch = new();
        private readonly DispatcherTimer _elapsedTimer = new();
        private readonly string _operationVerb;
        private readonly int _total;
        private bool _completed;
        private bool _succeeded;
        private DateTimeOffset _started = DateTimeOffset.Now;
        private string _summary = "Waiting to start.";
        private readonly TextBlock _exportStatus;
        private bool _exportInProgress;

        public CatalogProgressWindow(
            Window owner,
            string operationVerb,
            IReadOnlyList<CatalogProgressItem> items)
        {
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentException.ThrowIfNullOrWhiteSpace(operationVerb);
            ArgumentNullException.ThrowIfNull(items);
            if (items.Count == 0)
            {
                throw new ArgumentException("At least one progress item is required.", nameof(items));
            }

            _operationVerb = operationVerb.Trim();
            _total = items.Count;
            _items = new Dictionary<string, ItemVisuals>(StringComparer.OrdinalIgnoreCase);
            _progress = new CatalogProgressState(items.Select(item => item.Id));

            Grid root = new() { RowSpacing = 12 };
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _exportStatus = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = SecondaryTextBrush() };
            Grid.SetRow(_exportStatus, 2);
            root.Children.Add(_exportStatus);

            StackPanel itemRows = new() { Spacing = 9 };
            foreach (CatalogProgressItem item in items)
            {
                Grid heading = new() { ColumnSpacing = 12 };
                heading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                TextBlock name = new()
                {
                    Text = item.Name,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    TextWrapping = TextWrapping.Wrap
                };
                heading.Children.Add(name);

                TextBlock state = new()
                {
                    Text = "WAITING",
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = StatusBrush("WAITING"),
                    HorizontalAlignment = HorizontalAlignment.Right
                };
                Grid.SetColumn(state, 1);
                heading.Children.Add(state);

                TextBlock percent = new()
                {
                    Text = "0% complete — Waiting",
                    Foreground = WorkingBrush(),
                    Margin = new Thickness(0, 3, 0, 2)
                };

                ProgressBar bar = new()
                {
                    Minimum = 0,
                    Maximum = 100,
                    Value = 0,
                    Height = 12,
                    MinHeight = 12,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Background = TrackBrush(),
                    Foreground = WorkingBrush()
                };

                TextBlock detail = new()
                {
                    Text = "Waiting to start.",
                    FontSize = 11,
                    Foreground = SecondaryTextBrush(),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 2, 0, 0)
                };

                StackPanel body = new() { Spacing = 1 };
                body.Children.Add(heading);
                body.Children.Add(percent);
                body.Children.Add(bar);
                body.Children.Add(detail);

                Border card = new()
                {
                    BorderBrush = new SolidColorBrush(Color.FromArgb(255, 187, 199, 213)),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(12, 9, 12, 9),
                    Child = body
                };
                itemRows.Children.Add(card);
                _items[item.Id] = new ItemVisuals(item.Name, bar, percent, state, detail);
            }

            ScrollViewer itemViewer = new()
            {
                Content = itemRows,
                VerticalScrollMode = ScrollMode.Enabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollMode = ScrollMode.Disabled,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            Grid.SetRow(itemViewer, 0);
            root.Children.Add(itemViewer);

            Grid overall = new() { RowSpacing = 4 };
            overall.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            overall.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            overall.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            overall.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            overall.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock overallHeading = new()
            {
                Text = "OVERALL PROGRESS",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            };
            overall.Children.Add(overallHeading);

            _elapsedText = new TextBlock
            {
                Text = "Elapsed: 00:00:00",
                Foreground = SecondaryTextBrush(),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetColumn(_elapsedText, 1);
            overall.Children.Add(_elapsedText);

            _overallPercent = new TextBlock
            {
                Text = $"0% complete — 0/{_total}",
                Foreground = WorkingBrush()
            };
            Grid.SetRow(_overallPercent, 1);
            overall.Children.Add(_overallPercent);

            _overallBar = new ProgressBar
            {
                Minimum = 0,
                Maximum = _total,
                Value = 0,
                Height = 14,
                MinHeight = 14,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Background = TrackBrush(),
                Foreground = WorkingBrush()
            };
            Grid.SetRow(_overallBar, 2);
            Grid.SetColumnSpan(_overallBar, 2);
            overall.Children.Add(_overallBar);

            _overallDetail = new TextBlock
            {
                Text = "Waiting to start.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = SecondaryTextBrush(),
                Margin = new Thickness(0, 4, 0, 0)
            };
            ScrollViewer overallDetailViewer = new()
            {
                Content = _overallDetail,
                MaxHeight = 130,
                VerticalScrollMode = ScrollMode.Enabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollMode = ScrollMode.Disabled,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            Grid.SetRow(overallDetailViewer, 3);
            Grid.SetColumnSpan(overallDetailViewer, 2);
            overall.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            overall.Children.Add(overallDetailViewer);
            Grid.SetRow(_availabilityBadge.View, 4);
            Grid.SetColumnSpan(_availabilityBadge.View, 2);
            overall.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            overall.Children.Add(_availabilityBadge.View);

            Border overallBorder = new()
            {
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 187, 199, 213)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(14, 11, 14, 11),
                Child = overall
            };
            Grid.SetRow(overallBorder, 1);
            root.Children.Add(overallBorder);

            string initialTitle = $"{_operationVerb} {items[0].Name}";
            _window = new ToolWindow(
                owner,
                initialTitle,
                root,
                primaryButtonText: "Copy",
                secondaryButtonText: "Save TXT",
                closeButtonText: "Close",
                initialWidth: 920,
                initialHeight: 680,
                minimumWidth: 640,
                minimumHeight: 420);
            _window.IsBusy = () => !_completed || _exportInProgress;
            _window.PrimaryButton.Click += (_, _) =>
            {
                try
                {
                    DataPackage package = new();
                    package.SetText(BuildReport());
                    Clipboard.SetContent(package);
                    Clipboard.Flush();
                    _exportStatus.Text = "Copied to Clipboard";
                }
                catch (Exception exception) { _exportStatus.Text = $"Copy failed: {exception.Message}"; }
            };
            _window.SecondaryButton.Click += async (_, _) =>
            {
                if (_exportInProgress) return;
                _exportInProgress = true;
                _window.SecondaryButton.IsEnabled = false;
                try
                {
                    // Capture a consistent report when Save is requested, even
                    // if tasks continue while the user chooses a destination.
                    string report = BuildReport();
                    string? fileName = await DesktopReportExport.SaveAsync(_window, "Powertoys-Task-Report", report);
                    if (fileName is not null && !_window.IsClosed) _exportStatus.Text = $"Saved: {fileName}";
                }
                catch (Exception exception)
                {
                    if (!_window.IsClosed) _exportStatus.Text = $"Save failed: {exception.Message}";
                }
                finally
                {
                    _exportInProgress = false;
                    if (!_window.IsClosed) _window.SecondaryButton.IsEnabled = true;
                }
            };
            _window.CloseButton.IsEnabled = false;
            UiDisplaySettings.Changed += DisplaySettingsChanged;
            _window.Closed += (_, _) =>
            {
                _elapsedTimer.Stop();
                UiDisplaySettings.Changed -= DisplaySettingsChanged;
            };

            _elapsedTimer.Interval = TimeSpan.FromSeconds(1);
            _elapsedTimer.Tick += (_, _) =>
                _elapsedText.Text = $"Elapsed: {_stopwatch.Elapsed:hh\\:mm\\:ss}";
        }

        public void Show()
        {
            _started = DateTimeOffset.Now;
            _stopwatch.Start();
            _elapsedTimer.Start();
            _ = _window.ShowAsync();
        }

        public void BeginItem(string id, string? detail = null)
        {
            if (!_progress.Update(id, "RUNNING", null, detail ?? $"{_operationVerb}: {GetName(id)}")) return;
            RenderItem(id);
            UpdateOverall(_progress.Settled, _progress.Items[id].Detail);
            _window.SetTitle($"{_operationVerb} {GetName(id)}");
        }

        public IProgress<CatalogProgressUpdate> CreateReporter(string id) =>
            new DispatcherReporter(_overallDetail.DispatcherQueue, update =>
            {
                if (_completed || !_progress.Update(id, "RUNNING", update.Percent, update.Detail)) return;
                RenderItem(id);
                UpdateOverall(_progress.Settled, update.Detail);
            });

        private sealed class DispatcherReporter(
            Microsoft.UI.Dispatching.DispatcherQueue dispatcher,
            Action<CatalogProgressUpdate> report) : IProgress<CatalogProgressUpdate>
        {
            public void Report(CatalogProgressUpdate value)
            {
                if (dispatcher.HasThreadAccess) report(value);
                else dispatcher.TryEnqueue(() => report(value));
            }
        }

        public void VerifyItem(string id, string? detail = null)
        {
            if (!_progress.Update(id, "VERIFYING", null, detail ?? $"Verifying: {GetName(id)}")) return;
            RenderItem(id);
            UpdateOverall(_progress.Settled, _progress.Items[id].Detail);
            _window.SetTitle($"Verifying {GetName(id)}");
        }

        public void CompleteItem(string id, bool success, string? detail = null)
        {
            if (!_progress.Update(id, success ? "COMPLETED" : "FAILED", success ? 100 : null,
                detail ?? (success ? "Completed and verified." : "The operation failed."))) return;
            RenderItem(id);
            UpdateOverall(_progress.Settled, _progress.Items[id].Detail);
        }

        public void UnavailableItem(string id, string detail)
        {
            if (!_progress.Update(id, "UNAVAILABLE", null, detail)) return;
            RenderItem(id);
            UpdateOverall(_progress.Settled, detail);
        }

        public void CompleteItem(string id, ToolToggleOperationResult result)
        {
            string report = CatalogVerificationReport.FormatResult(GetName(id), _operationVerb, result);
            if (result.SkippedUnavailable && result.State.IsConfirmedUnavailable)
                UnavailableItem(id, report);
            else CompleteItem(id, result.Success && result.Verified, report);
        }

        public void UpdateOverall(int completed, string? detail = null)
        {
            if (_completed) return;
            int value = _progress.Settled;
            _overallBar.Value = value;
            _overallBar.IsIndeterminate = _progress.IsOverallIndeterminate;
            _overallBar.Foreground = _progress.HasFailures ? FailureBrush() : WorkingBrush();
            int percent = (int)Math.Round(value / (double)_total * 100d);
            _overallPercent.Text = _progress.IsOverallIndeterminate
                ? "Working — waiting for Windows"
                : _progress.UnavailableCount > 0 || _progress.HasFailures
                ? $"{percent}% processed — {value}/{_total}"
                : $"{percent}% complete — {value}/{_total}";
            _overallPercent.Foreground = _progress.HasFailures ? FailureBrush() : WorkingBrush();
            _availabilityBadge.Update(_progress.VerifiedCount, _total, _progress.UnavailableCount);
            if (!string.IsNullOrWhiteSpace(detail))
            {
                _overallDetail.Text = detail;
                _summary = detail;
            }
        }

        public void Complete(bool success, string detail)
        {
            if (_completed)
            {
                return;
            }

            _completed = true;
            _overallBar.IsIndeterminate = false;
            _progress.Finish(success, detail);
            success &= _progress.CompletedWithoutErrors;
            _succeeded = success;
            foreach (string id in _items.Keys) RenderItem(id);
            _overallBar.Value = _progress.Settled;
            _stopwatch.Stop();
            _elapsedTimer.Stop();
            _elapsedText.Text = $"Elapsed: {_stopwatch.Elapsed:hh\\:mm\\:ss}";
            if (success)
            {
                _overallBar.Value = _overallBar.Maximum;
                _overallBar.Foreground = _progress.UnavailableCount > 0 ? NeutralBrush() : WorkingBrush();
                _overallPercent.Text = _progress.UnavailableCount > 0
                    ? $"100% processed — {_total}/{_total}"
                    : $"100% complete — {_total}/{_total}";
                _overallPercent.Foreground = _progress.UnavailableCount > 0 ? SecondaryTextBrush() : WorkingBrush();
                _window.SetTitle($"{_operationVerb} completed");
            }
            else
            {
                _overallBar.Foreground = FailureBrush();
                int percent = (int)Math.Round(_overallBar.Value / _overallBar.Maximum * 100d);
                _overallPercent.Text = $"{percent}% processed — {_progress.Settled}/{_total}; errors or unverified items";
                _overallPercent.Foreground = FailureBrush();
                _window.SetTitle($"{_operationVerb} failed");
            }
            _overallDetail.Text = detail;
            _summary = detail;
            _overallDetail.Foreground = success ? WorkingBrush() : FailureBrush();
            _availabilityBadge.Update(_progress.VerifiedCount, _total, _progress.UnavailableCount);
            _window.CloseButton.IsEnabled = true;
        }

        private void RenderItem(string id)
        {
            ItemVisuals visuals = GetItem(id);
            CatalogItemProgress item = _progress.Items[id];
            string state = item.Phase;
            bool failed = state is "FAILED" or "NOT VERIFIED";
            visuals.Bar.IsIndeterminate = !item.IsTerminal && state != "WAITING" && !item.Percent.HasValue;
            // Fill failures red even when no percentage was supplied by Windows.
            // The label is a terminal result, never a fabricated completion percentage.
            visuals.Bar.Value = failed || state == "COMPLETED" ? 100 : item.Percent ?? 0;
            visuals.Bar.Background = TrackBrush();
            visuals.Bar.Foreground = failed ? FailureBrush() : state == "UNAVAILABLE" ? NeutralBrush() : WorkingBrush();
            visuals.Percent.Text = state switch
            {
                "FAILED" => "Failed — see details",
                "NOT VERIFIED" => "Not verified",
                "UNAVAILABLE" => "Unavailable on this PC",
                "COMPLETED" => "100% complete — Verified",
                "SKIPPED" => "Skipped — not started",
                "WAITING" => "Waiting to start",
                "VERIFYING" => "Verifying — waiting for Windows",
                _ => item.Percent.HasValue ? $"Windows step: {item.Percent:0}% — Working" : "Working — waiting for Windows"
            };
            visuals.Percent.Foreground = failed ? FailureBrush() : state == "UNAVAILABLE" ? SecondaryTextBrush() : WorkingBrush();
            visuals.State.Text = state;
            visuals.State.Foreground = StatusBrush(state);
            visuals.Detail.Text = item.Detail;
            visuals.Detail.Foreground = failed ? FailureBrush() : SecondaryTextBrush();
        }

        private void DisplaySettingsChanged(object? sender, EventArgs args)
        {
            foreach (string id in _items.Keys) RenderItem(id);
            _overallBar.Background = TrackBrush();
            bool failed = _progress.HasFailures || _completed && !_succeeded;
            bool unavailable = _completed && _progress.UnavailableCount > 0;
            _overallBar.Foreground = failed ? FailureBrush() : unavailable ? NeutralBrush() : WorkingBrush();
            _overallPercent.Foreground = failed ? FailureBrush() : unavailable ? SecondaryTextBrush() : WorkingBrush();
            _overallDetail.Foreground = failed ? FailureBrush() : SecondaryTextBrush();
        }

        private ItemVisuals GetItem(string id) =>
            _items.TryGetValue(id, out ItemVisuals? visuals)
                ? visuals
                : throw new KeyNotFoundException($"Unknown progress item '{id}'.");

        private string BuildReport() => CatalogVerificationReport.Build(_operationVerb, _started, _stopwatch.Elapsed,
            _summary, _items.Select(pair => (pair.Key, pair.Value.Name, _progress.Items[pair.Key])));

        private string GetName(string id) => GetItem(id).Name;

        private static string ToDisplayPhase(string state) => state switch
        {
            "RUNNING" => "Working",
            "VERIFYING" => "Verifying",
            "COMPLETED" => "Completed",
            _ => state
        };

        private static Brush WorkingBrush() => new SolidColorBrush(
            UiDisplaySettings.Theme == ElementTheme.Dark
                ? Color.FromArgb(255, 108, 203, 95)
                : Color.FromArgb(255, 16, 124, 16));

        private static Brush NeutralBrush() => new SolidColorBrush(Color.FromArgb(255, 107, 114, 128));

        private static Brush FailureBrush() => new SolidColorBrush(
            UiDisplaySettings.Theme == ElementTheme.Dark
                ? Color.FromArgb(255, 255, 138, 138)
                : Color.FromArgb(255, 196, 43, 28));

        private static Brush TrackBrush() => new SolidColorBrush(
            UiDisplaySettings.Theme == ElementTheme.Dark
                ? Color.FromArgb(255, 58, 65, 75)
                : Color.FromArgb(255, 230, 230, 230));

        private static Brush SecondaryTextBrush() => new SolidColorBrush(
            UiDisplaySettings.Theme == ElementTheme.Dark
                ? Color.FromArgb(255, 185, 198, 216)
                : Color.FromArgb(255, 49, 70, 95));

        private static Brush StatusBrush(string state) => state switch
        {
            "FAILED" or "NOT VERIFIED" => FailureBrush(),
            "RUNNING" or "VERIFYING" or "COMPLETED" => WorkingBrush(),
            _ => SecondaryTextBrush()
        };
    }
}
