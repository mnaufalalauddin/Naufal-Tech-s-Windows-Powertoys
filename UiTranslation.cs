using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Naufal_Windows_Tech_s_Powertoys;

// Localization touches authored display properties only, not WinUI template parts,
// editable values, hardware identifiers, operation IDs or diagnostic log buffers.
internal static partial class UiTranslation
{
    private sealed class LocalizationState
    {
        internal string Code = "en";
        internal bool Applying;
        internal readonly Dictionary<DependencyProperty, UiLocalizedValue> Values = new();
        internal readonly Dictionary<DependencyProperty, long> CallbackTokens = new();
        internal RootObserver? Owner;
    }

    private sealed class RootObserver : IDisposable
    {
        private readonly FrameworkElement _root;
        private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(150) };
        internal readonly UiLocalizationLifetime<DependencyObject> Lifetime = new();
        internal bool Scanning;
        internal bool Disposed;
        internal string Code = "en";

        internal RootObserver(FrameworkElement root)
        {
            _root = root;
            root.LayoutUpdated += LayoutUpdated;
            root.Unloaded += Unloaded;
            _timer.Tick += Tick;
        }

        private void LayoutUpdated(object? sender, object args)
        {
            if (!Disposed && !Scanning && _root.IsLoaded && !_timer.IsEnabled) _timer.Start();
        }

        private void Unloaded(object sender, RoutedEventArgs args) => _timer.Stop();

        private void Tick(object? sender, object args)
        {
            _timer.Stop();
            if (Disposed || !_root.IsLoaded) return;
            Apply(_root, UiDisplaySettings.LanguageCode);
        }

        public void Dispose()
        {
            if (Disposed) return;
            Disposed = true;
            _timer.Stop();
            _timer.Tick -= Tick;
            _root.LayoutUpdated -= LayoutUpdated;
            _root.Unloaded -= Unloaded;
            Lifetime.Clear(element => ReleaseElement(element, this));
        }
    }

    private static readonly ConditionalWeakTable<DependencyObject, LocalizationState> States = new();
    private static readonly ConditionalWeakTable<FrameworkElement, RootObserver> Observers = new();

    public static void Observe(FrameworkElement root)
    {
        _ = Observers.GetValue(root, static element => new(element));
    }

    public static void Release(FrameworkElement root)
    {
        if (!Observers.TryGetValue(root, out RootObserver? observer)) return;
        observer.Dispose();
        Observers.Remove(root);
    }

    public static void Apply(FrameworkElement root, string? languageCode)
    {
        string code = NormalizeLanguageCode(languageCode);
        RootObserver observer = Observers.GetValue(root, static element => new(element));
        if (observer.Disposed || observer.Scanning) return;
        observer.Code = code;
        root.FlowDirection = IsRightToLeft(code) ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        root.Language = GetCulture(code).Name;
        HashSet<DependencyObject> visited = new(ReferenceEqualityComparer.Instance);
        observer.Scanning = true;
        try
        {
            ApplyRecursively(root, observer, visited);
            observer.Lifetime.Prune(visited, element => ReleaseElement(element, observer));
        }
        finally { observer.Scanning = false; }
    }

    private static void ApplyMenu(MenuFlyout menu, RootObserver observer, ISet<DependencyObject> visited)
    {
        if (!visited.Add(menu)) return;
        observer.Lifetime.Retain(menu);
        foreach (var item in menu.Items)
        {
            item.FlowDirection = IsRightToLeft(observer.Code) ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            item.Language = GetCulture(observer.Code).Name;
            ApplyRecursively(item, observer, visited);
        }
    }

    private static void ApplyRecursively(DependencyObject element, RootObserver observer, ISet<DependencyObject> visited)
    {
        if (!visited.Add(element)) return;
        observer.Lifetime.Retain(element);
        if (element is FrameworkElement literal && literal.Tag is string tag &&
            (tag.StartsWith("ui-language:", StringComparison.Ordinal) || tag == "ui-literal"))
        {
            literal.FlowDirection = FlowDirection.LeftToRight;
            return;
        }

        LocalizationState state = States.GetValue(element, static _ => new());
        state.Code = observer.Code;
        state.Owner = observer;
        if (element is TextBlock) Watch(element, TextBlock.TextProperty, state);
        if (element is ContentControl) Watch(element, ContentControl.ContentProperty, state);
        if (element is ToggleSwitch)
        {
            Watch(element, ToggleSwitch.OnContentProperty, state);
            Watch(element, ToggleSwitch.OffContentProperty, state);
            Watch(element, ToggleSwitch.HeaderProperty, state);
        }
        if (element is TextBox box)
        {
            Watch(element, TextBox.HeaderProperty, state);
            Watch(element, TextBox.PlaceholderTextProperty, state);
            if (box.IsReadOnly) box.FlowDirection = FlowDirection.LeftToRight;
        }
        if (element is ComboBox)
        {
            Watch(element, ComboBox.HeaderProperty, state);
            Watch(element, ComboBox.PlaceholderTextProperty, state);
        }
        if (element is MenuFlyoutItem) Watch(element, MenuFlyoutItem.TextProperty, state);
        if (element is MenuFlyoutSubItem submenu)
        {
            Watch(element, MenuFlyoutSubItem.TextProperty, state);
            foreach (var item in submenu.Items) ApplyRecursively(item, observer, visited);
        }
        if (element is FrameworkElement framework)
        {
            Watch(element, AutomationProperties.NameProperty, state);
            Watch(element, AutomationProperties.HelpTextProperty, state);
            Watch(element, ToolTipService.ToolTipProperty, state);
            if (framework.ContextFlyout is MenuFlyout contextMenu) ApplyMenu(contextMenu, observer, visited);
            if (FlyoutBase.GetAttachedFlyout(framework) is MenuFlyout attachedMenu) ApplyMenu(attachedMenu, observer, visited);
        }
        if (element is Button button && button.Flyout is MenuFlyout menu) ApplyMenu(menu, observer, visited);
        foreach (DependencyProperty property in state.Values.Keys)
            if (element.GetValue(property) is DependencyObject authoredContent)
                ApplyRecursively(authoredContent, observer, visited);

        // Do not walk template-created TextBlocks: writing those would capture a
        // localized caption as a new source and may break the template's binding.
        if (element is Panel panel)
            foreach (UIElement child in panel.Children) ApplyRecursively(child, observer, visited);
        if (element is Border border && border.Child is DependencyObject borderChild)
            ApplyRecursively(borderChild, observer, visited);
        if (element is ContentControl host && host.Content is DependencyObject content)
            ApplyRecursively(content, observer, visited);
        if (element is ContentPresenter presenter && presenter.Content is DependencyObject presented)
            ApplyRecursively(presented, observer, visited);
        if (element is ItemsControl items)
            foreach (object item in items.Items)
                if (item is DependencyObject child) ApplyRecursively(child, observer, visited);
    }

    private static void Watch(DependencyObject element, DependencyProperty property, LocalizationState state)
    {
        if (!state.Values.TryGetValue(property, out var value))
        {
            state.Values[property] = value = new();
            state.CallbackTokens[property] = element.RegisterPropertyChangedCallback(property, (sender, changedProperty) =>
            {
                if (state.Applying || state.Owner is not { Disposed: false } owner) return;
                state.Code = owner.Code;
                Render(sender, changedProperty, state.Values[changedProperty], state);
                if (sender.GetValue(changedProperty) is DependencyObject child)
                    ApplyRecursively(child, owner, new HashSet<DependencyObject>(ReferenceEqualityComparer.Instance));
            });
        }
        Render(element, property, value, state);
    }

    private static void ReleaseElement(DependencyObject element, RootObserver owner)
    {
        if (!States.TryGetValue(element, out LocalizationState? state) || state.Owner != owner) return;
        foreach (var callback in state.CallbackTokens)
            element.UnregisterPropertyChangedCallback(callback.Key, callback.Value);
        state.CallbackTokens.Clear();
        // Removed rows may be cached and reinserted. Leave canonical text behind,
        // not the previous language, before their wrapper/state can be collected.
        foreach (var property in state.Values)
        {
            if (element.GetValue(property.Key) is string current)
                element.SetValue(property.Key, property.Value.RestoreSource(current));
        }
        state.Owner = null;
        States.Remove(element);
    }

    private static void Render(DependencyObject element, DependencyProperty property, UiLocalizedValue value, LocalizationState state)
    {
        if (element.GetValue(property) is not string current) return;
        string translated = value.Resolve(current, state.Code, Translate);
        if (current == translated) return;
        state.Applying = true;
        try { element.SetValue(property, translated); }
        finally { state.Applying = false; }
    }
}
