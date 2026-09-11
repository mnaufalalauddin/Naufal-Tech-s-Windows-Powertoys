// Minimal authored-control model to run the PRODUCTION localization adapter in
// a portable test process. These are not WinUI, RCWs, or a native visual test.
// The app's normal build separately checks the actual Windows App SDK surface.
namespace Microsoft.UI.Xaml
{
    public sealed class DependencyProperty;
    public class DependencyObject
    {
        private readonly Dictionary<DependencyProperty, object?> _values = new();
        private readonly Dictionary<long, (DependencyProperty Property, Action<DependencyObject, DependencyProperty> Changed)> _callbacks = new();
        private long _nextToken;
        public int CallbackCount => _callbacks.Count;
        public object? GetValue(DependencyProperty property) => _values.GetValueOrDefault(property);
        public void SetValue(DependencyProperty property, object? value)
        {
            if (Equals(GetValue(property), value)) return;
            _values[property] = value;
            foreach (var callback in _callbacks.Values.ToArray())
                if (callback.Property == property) callback.Changed(this, property);
        }
        public long RegisterPropertyChangedCallback(DependencyProperty property, Action<DependencyObject, DependencyProperty> changed)
        {
            _callbacks[++_nextToken] = (property, changed);
            return _nextToken;
        }
        public void UnregisterPropertyChangedCallback(DependencyProperty property, long token)
        {
            if (!_callbacks.Remove(token)) throw new InvalidOperationException("Callback was already removed.");
        }
    }
    public class UIElement : DependencyObject;
    public enum FlowDirection { LeftToRight, RightToLeft }
    public sealed class RoutedEventArgs : EventArgs;
    public delegate void RoutedEventHandler(object sender, RoutedEventArgs args);
    public class FrameworkElement : UIElement
    {
        public object? Tag { get; set; }
        public FlowDirection FlowDirection { get; set; }
        public string Language { get; set; } = "en-US";
        public bool IsLoaded { get; set; } = true;
        public Controls.Primitives.FlyoutBase? ContextFlyout { get; set; }
        public event EventHandler<object>? LayoutUpdated;
        public event RoutedEventHandler? Unloaded;
        public int ObserverCount => (LayoutUpdated?.GetInvocationList().Length ?? 0) + (Unloaded?.GetInvocationList().Length ?? 0);
        public void RaiseLayoutUpdated() => LayoutUpdated?.Invoke(this, EventArgs.Empty);
        public void RaiseUnloaded() => Unloaded?.Invoke(this, new());
    }
    public sealed class DispatcherTimer
    {
        public TimeSpan Interval { get; set; }
        public bool IsEnabled { get; private set; }
        public event EventHandler<object>? Tick;
        public void Start() => IsEnabled = true;
        public void Stop() => IsEnabled = false;
        public void FireTick() { if (IsEnabled) Tick?.Invoke(this, EventArgs.Empty); }
    }
}
namespace Microsoft.UI.Xaml.Controls
{
    public class Control : FrameworkElement;
    public class ContentControl : Control
    {
        public static readonly DependencyProperty ContentProperty = new();
        public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }
    }
    public class TextBlock : FrameworkElement
    {
        public static readonly DependencyProperty TextProperty = new();
        public string Text { get => GetValue(TextProperty) as string ?? ""; set => SetValue(TextProperty, value); }
    }
    public class ToggleSwitch : Control
    {
        public static readonly DependencyProperty OnContentProperty = new(), OffContentProperty = new(), HeaderProperty = new();
        public object? OnContent { get => GetValue(OnContentProperty); set => SetValue(OnContentProperty, value); }
        public object? OffContent { get => GetValue(OffContentProperty); set => SetValue(OffContentProperty, value); }
        public object? Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }
    }
    public class TextBox : Control
    {
        public static readonly DependencyProperty HeaderProperty = new(), PlaceholderTextProperty = new();
        public object? Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }
        public string PlaceholderText { get => GetValue(PlaceholderTextProperty) as string ?? ""; set => SetValue(PlaceholderTextProperty, value); }
        public bool IsReadOnly { get; set; }
        public string Text { get; set; } = "";
    }
    public class ItemsControl : Control { public List<object> Items { get; } = new(); }
    public class ComboBox : ItemsControl
    {
        public static readonly DependencyProperty HeaderProperty = new(), PlaceholderTextProperty = new();
        public object? Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }
    }
    public class Button : ContentControl { public Primitives.FlyoutBase? Flyout { get; set; } }
    public class Panel : FrameworkElement { public List<UIElement> Children { get; } = new(); }
    public class Border : FrameworkElement { public UIElement? Child { get; set; } }
    public class ContentPresenter : FrameworkElement { public object? Content { get; set; } }
    public class ToolTip : ContentControl;
    public class MenuFlyout : Primitives.FlyoutBase { public List<FrameworkElement> Items { get; } = new(); }
    public class MenuFlyoutItem : FrameworkElement
    {
        public static readonly DependencyProperty TextProperty = new();
        public string Text { get => GetValue(TextProperty) as string ?? ""; set => SetValue(TextProperty, value); }
    }
    public class MenuFlyoutSubItem : FrameworkElement
    {
        public static readonly DependencyProperty TextProperty = new();
        public string Text { get => GetValue(TextProperty) as string ?? ""; set => SetValue(TextProperty, value); }
        public List<FrameworkElement> Items { get; } = new();
    }
    public static class ToolTipService { public static readonly DependencyProperty ToolTipProperty = new(); }
}
namespace Microsoft.UI.Xaml.Controls.Primitives
{
    public class FlyoutBase : DependencyObject
    {
        private static readonly DependencyProperty AttachedFlyoutProperty = new();
        public static FlyoutBase? GetAttachedFlyout(FrameworkElement element) => element.GetValue(AttachedFlyoutProperty) as FlyoutBase;
        public static void SetAttachedFlyout(FrameworkElement element, FlyoutBase flyout) => element.SetValue(AttachedFlyoutProperty, flyout);
    }
}
namespace Microsoft.UI.Xaml.Automation
{
    public static class AutomationProperties
    {
        public static readonly DependencyProperty NameProperty = new(), HelpTextProperty = new();
    }
}
namespace Naufal_Windows_Tech_s_Powertoys
{
    internal static class UiDisplaySettings { internal static string LanguageCode { get; set; } = "en"; }
}
