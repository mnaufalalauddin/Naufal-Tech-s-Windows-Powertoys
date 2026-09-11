using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Naufal_Windows_Tech_s_Powertoys;

internal static class LanguageSwitchRegression
{
    internal static int Run()
    {
        int assertions = 0;
        void Check(bool condition, string message)
        {
            assertions++;
            if (!condition) throw new InvalidOperationException("Language switching regression: " + message);
        }
        string Tr(string text, string code) => UiTranslation.Translate(text, code);
        Panel root = new();
        const string availabilitySource = "33 out of 41 have been verified, but 8 tweaks can't be applied due to unavailability on this PC.";
        TextBlock availabilityText = new() { Text = availabilitySource };
        Border availabilityBadge = new() { Child = availabilityText };
        root.Children.Add(availabilityBadge);
        TextBlock heading = new() { Text = "Gaming Tweaks" };
        Button apply = new() { Content = "Apply selected" };
        apply.SetValue(AutomationProperties.NameProperty, "Apply selected");
        apply.SetValue(AutomationProperties.HelpTextProperty, "Restore selected");
        apply.SetValue(ToolTipService.ToolTipProperty, "Select all");
        ToggleSwitch toggle = new() { OnContent = "ON", OffContent = "OFF", Header = "Selected" };
        TextBox log = new() { Text = "Applying Taskbar Widgets\n0x80131501", Header = "LIVE OUTPUT", IsReadOnly = true };
        TextBox input = new() { Text = "My user input", PlaceholderText = "Select all" };
        TextBlock headerContent = new() { Text = "OVERALL PROGRESS" };
        ComboBox combo = new() { Header = headerContent };
        ContentControl literal = new() { Content = "Bahasa Indonesia", Tag = "ui-language:id" };
        combo.Items.Add(literal);
        ItemsControl rows = new();
        TextBlock rowText = new() { Text = "Restoring Taskbar Widgets" };
        ContentControl row = new() { Content = rowText };
        rows.Items.Add(row);
        ContentControl host = new() { Content = new TextBlock { Text = "Waiting to start." } };
        MenuFlyout attached = new(), context = new(), buttonMenu = new();
        MenuFlyoutItem scale = new() { Text = "Reset to 100%" };
        MenuFlyoutItem copy = new() { Text = "Copy log" };
        MenuFlyoutSubItem sub = new() { Text = "Restore selected" };
        MenuFlyoutItem nested = new() { Text = "Select all" };
        sub.Items.Add(nested);
        attached.Items.Add(scale);
        context.Items.Add(copy);
        buttonMenu.Items.Add(sub);
        FlyoutBase.SetAttachedFlyout(apply, attached);
        apply.ContextFlyout = context;
        apply.Flyout = buttonMenu;
        root.Children.AddRange([heading, apply, toggle, log, input, combo, rows, host]);
        UiTranslation.Observe(root);
        int? callbacks = null;

        // Every source language -> every target language on the same authored
        // objects. Unlike resource-only tests this executes production traversal,
        // callback registration, rendering, menus, tooltip and lifetime code.
        foreach (var from in UiTranslation.LanguageOptions)
        foreach (var to in UiTranslation.LanguageOptions)
        {
            UiTranslation.Apply(root, from.Code);
            ForceCollection();
            UiTranslation.Apply(root, to.Code);
            string code = to.Code;
            Check(heading.Text == Tr("Gaming Tweaks", code), code + " heading");
            Check(availabilityText.Text == Tr(availabilitySource, code), code + " gray badge canonical text survives every language pair");
            Check(Equals(apply.Content, Tr("Apply selected", code)), code + " button");
            Check(Equals(apply.GetValue(AutomationProperties.NameProperty), Tr("Apply selected", code)), code + " accessibility name");
            Check(Equals(apply.GetValue(AutomationProperties.HelpTextProperty), Tr("Restore selected", code)), code + " accessibility help");
            Check(Equals(apply.GetValue(ToolTipService.ToolTipProperty), Tr("Select all", code)), code + " string tooltip");
            Check(Equals(toggle.OnContent, Tr("ON", code)) && Equals(toggle.OffContent, Tr("OFF", code)), code + " toggle");
            Check(Equals(toggle.Header, Tr("Selected", code)), code + " toggle header");
            Check(Equals(log.Header, Tr("LIVE OUTPUT", code)), code + " log heading");
            Check(log.Text == "Applying Taskbar Widgets\n0x80131501", code + " raw log unchanged");
            Check(input.Text == "My user input", code + " user input unchanged");
            Check(input.PlaceholderText == Tr("Select all", code), code + " placeholder");
            Check(headerContent.Text == Tr("OVERALL PROGRESS", code), code + " object header");
            Check(Equals(literal.Content, "Bahasa Indonesia"), code + " native language name");
            Check(rowText.Text == Tr("Restoring Taskbar Widgets", code), code + " offscreen authored row");
            Check(scale.Text == Tr("Reset to 100%", code), code + " attached popup");
            Check(copy.Text == Tr("Copy log", code), code + " context popup");
            Check(sub.Text == Tr("Restore selected", code) && nested.Text == Tr("Select all", code), code + " nested popup");
            Check(root.Language == UiTranslation.GetCulture(code).Name, code + " root language");
            Check(root.FlowDirection == (UiTranslation.IsRightToLeft(code) ? FlowDirection.RightToLeft : FlowDirection.LeftToRight), code + " root flow");
            Check(log.FlowDirection == FlowDirection.LeftToRight, code + " technical log direction");
            callbacks ??= apply.CallbackCount;
            Check(apply.CallbackCount == callbacks, code + " no duplicate subscriptions");
        }

        foreach (var language in UiTranslation.LanguageOptions)
        {
            string code = language.Code;
            UiTranslation.Apply(root, code);
            heading.Text = "Applying Taskbar Widgets";
            const string changedAvailability = "32 out of 41 have been verified, but 9 tweaks can't be applied due to unavailability on this PC.";
            availabilityText.Text = changedAvailability;
            Check(availabilityText.Text == Tr(changedAvailability, code), code + " live badge count updates");
            Check(heading.Text == Tr("Applying Taskbar Widgets", code), code + " immediate live update");
            TextBlock fresh = new() { Text = "Restoring Taskbar Widgets" };
            host.Content = fresh;
            Check(fresh.Text == Tr("Restoring Taskbar Widgets", code), code + " new content translated synchronously");
            ToolTip tooltip = new() { Content = "Restore selected" };
            apply.SetValue(ToolTipService.ToolTipProperty, tooltip);
            Check(Equals(tooltip.Content, Tr("Restore selected", code)), code + " new object tooltip");
            UiTranslation.Apply(root, "en");
            Check(availabilityText.Text == changedAvailability, code + " badge dynamic update returns to canonical English");
            Check(heading.Text == "Applying Taskbar Widgets" && fresh.Text == "Restoring Taskbar Widgets", code + " live source round trip");
            Check(Equals(tooltip.Content, "Restore selected"), code + " tooltip round trip");
            UiTranslation.Apply(root, code);
            rows.Items.Remove(row);
            UiTranslation.Apply(root, code);
            Check(rowText.Text == "Restoring Taskbar Widgets", code + " removed row restores canonical before forgetting");
            Check(row.CallbackCount == 0 && rowText.CallbackCount == 0, code + " removed row unsubscribed");
            ForceCollection();
            rows.Items.Add(row);
            UiTranslation.Apply(root, "en");
            Check(rowText.Text == "Restoring Taskbar Widgets", code + " reattached row never adopts old translation");
            UiTranslation.Apply(root, code);
            root.RaiseUnloaded();
            UiTranslation.Apply(root, "en");
            Check(heading.Text == "Applying Taskbar Widgets", code + " unloading is not closing");
        }
        UiTranslation.Apply(root, "ja");
        UiTranslation.Release(root);
        UiTranslation.Release(root); // Closing/owner-close paths must be idempotent.
        Check(root.ObserverCount == 0, "window events detached");
        Check(apply.CallbackCount == 0 && heading.CallbackCount == 0 && scale.CallbackCount == 0 && nested.CallbackCount == 0, "window/popup callbacks detached");
        Check(heading.Text == "Applying Taskbar Widgets" && scale.Text == "Reset to 100%", "close restores canonical captions");
        heading.Text = "Restoring Taskbar Widgets";
        Check(heading.Text == "Restoring Taskbar Widgets", "closed controls no longer translated");
        UiTranslation.Apply(root, "id");
        Check(heading.Text == Tr("Restoring Taskbar Widgets", "id"), "fresh observer can reuse closed root");
        UiTranslation.Release(root);

        UiLocalizationLifetime<object> lifetime = new();
        WeakReference<object> weak = CreateRetainedObject(lifetime);
        ForceCollection();
        Check(IsAlive(weak), "scope retains a wrapper without external managed owners");
        int released = 0;
        lifetime.Prune(new HashSet<object>(ReferenceEqualityComparer.Instance), _ => released++);
        ForceCollection();
        Check(!IsAlive(weak), "removed wrapper collectible after cleanup");
        Check(released == 1 && lifetime.Count == 0, "removal releases once");
        weak = CreateRetainedObject(lifetime);
        lifetime.Clear(_ => released++);
        lifetime.Clear(_ => released++);
        ForceCollection();
        Check(!IsAlive(weak) && released == 2, "closing releases retained objects exactly once");

        UiLocalizedValue state = new();
        string localized = state.Resolve("Apply selected", "id", UiTranslation.Translate);
        Check(state.RestoreSource(localized) == "Apply selected", "detach uses source not rendered text");
        Check(state.RestoreSource("New backend caption") == "New backend caption", "detach preserves latest backend update");
        return assertions;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<object> CreateRetainedObject(UiLocalizationLifetime<object> lifetime)
    {
        object wrapper = new();
        lifetime.Retain(wrapper);
        return new(wrapper);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool IsAlive(WeakReference<object> weak) => weak.TryGetTarget(out _);

    private static void ForceCollection()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
}
