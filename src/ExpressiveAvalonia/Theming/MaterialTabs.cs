using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace ExpressiveAvalonia.Theming;

/// <summary>The Material tab indicator treatment.</summary>
public enum MaterialTabVariant
{
    Primary,
    Secondary
}

/// <summary>
/// The header composition of a Material tab. <see cref="Auto"/> derives the layout from
/// <see cref="TabItem.Icon"/> and <see cref="TabItem.Header"/>, so a tab with both renders the
/// 64px icon-and-label header, a tab with only an icon renders the icon-only header, and a tab
/// with only a header renders the label-only header.
/// </summary>
public enum MaterialTabLayout
{
    Auto,
    LabelOnly,
    IconOnly,
    IconAndLabel
}

/// <summary>Appearance, header layout, and header interaction for standard Avalonia tabs.</summary>
/// <remarks>
/// <para>
/// The upgraded templates live in <c>Themes/DesignTabs.axaml</c> and are keyed both implicitly
/// (<see cref="TabControl"/> / <see cref="TabItem"/>) and as <c>DesignTabControl</c> /
/// <c>DesignTabItem</c> for explicit opt-in. The source family defines two styles (primary and
/// secondary), two configurations (fixed and scrollable), and three layouts (icon only, label and
/// icon, label only). A badge and a focus indicator are exposed as attached properties.
/// </para>
/// <para>
/// The resolved header state is published on each tab as the style classes
/// <c>md-primary</c>, <c>md-secondary</c>, <c>md-label-only</c>, <c>md-icon-only</c>,
/// <c>md-icon-and-label</c>, <c>md-has-badge</c>, <c>md-badge-text</c>, and
/// <c>md-scrollable</c>. They are ordinary style classes rather than pseudo-classes because
/// Avalonia only lets a control add pseudo-classes to itself.
/// </para>
/// <para>
/// Navigation is automatic activation: Left/Right (Up/Down for a vertical strip), Home, and End
/// move the selection and focus, skipping disabled or collapsed headers. Enter and Space select a
/// focused header. Arrow keys inside the selected page are never consumed, and nested tab strips
/// keep their own navigation.
/// </para>
/// <para>
/// Icon-only tabs have no visible label. Set <see cref="TabItem.Header"/> (and
/// <see cref="Layout"/> to <see cref="MaterialTabLayout.IconOnly"/>) or
/// <c>AutomationProperties.Name</c> so the tab keeps an accessible name.
/// </para>
/// </remarks>
public sealed class MaterialTabs : AvaloniaObject
{
    private const double ScrollStep = 64;

    /// <summary>Primary uses a short indicator; secondary uses a full-width indicator.</summary>
    public static readonly AttachedProperty<MaterialTabVariant> VariantProperty =
        AvaloniaProperty.RegisterAttached<MaterialTabs, Control, MaterialTabVariant>(
            "Variant", MaterialTabVariant.Primary, inherits: true);

    /// <summary>Whether tab headers scroll instead of sharing the available width equally.</summary>
    public static readonly AttachedProperty<bool> IsScrollableProperty =
        AvaloniaProperty.RegisterAttached<MaterialTabs, TabControl, bool>("IsScrollable");

    /// <summary>The requested header composition; <see cref="MaterialTabLayout.Auto"/> infers it.</summary>
    public static readonly AttachedProperty<MaterialTabLayout> LayoutProperty =
        AvaloniaProperty.RegisterAttached<MaterialTabs, TabItem, MaterialTabLayout>(
            "Layout", MaterialTabLayout.Auto);

    /// <summary>The resolved header composition. Updated whenever the icon, header, or layout changes.</summary>
    public static readonly AttachedProperty<MaterialTabLayout> EffectiveLayoutProperty =
        AvaloniaProperty.RegisterAttached<MaterialTabs, TabItem, MaterialTabLayout>(
            "EffectiveLayout", MaterialTabLayout.LabelOnly);

    /// <summary>Whether the tab shows a badge dot (or a badge pill when <see cref="BadgeTextProperty"/> is set).</summary>
    public static readonly AttachedProperty<bool> ShowBadgeProperty =
        AvaloniaProperty.RegisterAttached<MaterialTabs, TabItem, bool>("ShowBadge");

    /// <summary>
    /// The badge label. The source tab badge is its small variant (a 6px dot over the icon, or
    /// after the label when the header has no icon). Setting text additionally renders a pill in
    /// the trailing position for label headers; icon-only and primary icon-and-label headers keep
    /// the dot, matching the source's small badge.
    /// </summary>
    public static readonly AttachedProperty<string?> BadgeTextProperty =
        AvaloniaProperty.RegisterAttached<MaterialTabs, TabItem, string?>("BadgeText");

    /// <summary>
    /// Whether a keyboard focus ring is drawn. Defaults to true; the source component's
    /// "Show focus indicator" flag defaults to false because it is a static design state.
    /// </summary>
    public static readonly AttachedProperty<bool> ShowFocusIndicatorProperty =
        AvaloniaProperty.RegisterAttached<MaterialTabs, Control, bool>(
            "ShowFocusIndicator", true, inherits: true);

    static MaterialTabs()
    {
        InputElement.KeyDownEvent.AddClassHandler<TabControl>(OnKeyDown, RoutingStrategies.Tunnel);
        InputElement.PointerWheelChangedEvent.AddClassHandler<TabControl>(OnPointerWheelChanged, RoutingStrategies.Tunnel);
        SelectingItemsControl.SelectionChangedEvent.AddClassHandler<TabControl>(OnSelectionChanged);

        IsScrollableProperty.Changed.AddClassHandler<TabControl>(
            static (tabs, _) => UpdateContainers(tabs));
        TabItem.IconProperty.Changed.AddClassHandler<TabItem>(static (item, _) => UpdateHeaderLayout(item));
        TabItem.HeaderProperty.Changed.AddClassHandler<TabItem>(static (item, _) => UpdateHeaderLayout(item));
        LayoutProperty.Changed.AddClassHandler<TabItem>(static (item, _) => UpdateHeaderLayout(item));
        ShowBadgeProperty.Changed.AddClassHandler<TabItem>(static (item, _) => UpdateHeaderLayout(item));
        BadgeTextProperty.Changed.AddClassHandler<TabItem>(static (item, _) => UpdateHeaderLayout(item));
        VariantProperty.Changed.AddClassHandler<TabItem>(static (item, _) => UpdateHeaderLayout(item));
        VariantProperty.Changed.AddClassHandler<TabControl>(static (tabs, _) => UpdateContainers(tabs));
        // Containers are realized lazily, so resolve the header classes when each tab loads.
        // TemplateApplied runs before the first render, so no frame shows the hidden presenters.
        TemplatedControl.TemplateAppliedEvent.AddClassHandler<TabItem>(static (item, _) =>
        {
            UpdateHeaderLayout(item);
            UpdateScrollableState(item);
        });
        Control.LoadedEvent.AddClassHandler<TabItem>(static (item, _) =>
        {
            UpdateHeaderLayout(item);
            UpdateScrollableState(item);
        });
        Control.LoadedEvent.AddClassHandler<TabControl>(static (tabs, _) => UpdateContainers(tabs));
    }

    public static MaterialTabVariant GetVariant(Control control) => control.GetValue(VariantProperty);
    public static void SetVariant(Control control, MaterialTabVariant value) => control.SetValue(VariantProperty, value);
    public static bool GetIsScrollable(TabControl control) => control.GetValue(IsScrollableProperty);
    public static void SetIsScrollable(TabControl control, bool value) => control.SetValue(IsScrollableProperty, value);

    public static MaterialTabLayout GetLayout(TabItem item) => item.GetValue(LayoutProperty);
    public static void SetLayout(TabItem item, MaterialTabLayout value) => item.SetValue(LayoutProperty, value);
    public static MaterialTabLayout GetEffectiveLayout(TabItem item) => item.GetValue(EffectiveLayoutProperty);
    public static bool GetShowBadge(TabItem item) => item.GetValue(ShowBadgeProperty);
    public static void SetShowBadge(TabItem item, bool value) => item.SetValue(ShowBadgeProperty, value);
    public static string? GetBadgeText(TabItem item) => item.GetValue(BadgeTextProperty);
    public static void SetBadgeText(TabItem item, string? value) => item.SetValue(BadgeTextProperty, value);
    public static bool GetShowFocusIndicator(Control control) => control.GetValue(ShowFocusIndicatorProperty);
    public static void SetShowFocusIndicator(Control control, bool value) => control.SetValue(ShowFocusIndicatorProperty, value);

    private static void UpdateHeaderLayout(TabItem item)
    {
        var hasIcon = item.Icon is not null;
        var hasHeader = item.Header is not null;
        var requested = GetLayout(item);
        var effective = requested switch
        {
            MaterialTabLayout.Auto when hasIcon && hasHeader => MaterialTabLayout.IconAndLabel,
            MaterialTabLayout.Auto when hasIcon => MaterialTabLayout.IconOnly,
            MaterialTabLayout.Auto => MaterialTabLayout.LabelOnly,
            _ => requested
        };

        item.SetValue(EffectiveLayoutProperty, effective);
        // External helpers cannot add pseudo-classes (Avalonia reserves ':' names for the
        // control itself), so the resolved state is published as ordinary style classes that
        // DesignTabs.axaml selects with `^.md-*`.
        item.Classes.Set("md-label-only", effective == MaterialTabLayout.LabelOnly);
        item.Classes.Set("md-icon-only", effective == MaterialTabLayout.IconOnly);
        item.Classes.Set("md-icon-and-label", effective == MaterialTabLayout.IconAndLabel);
        item.Classes.Set("md-has-badge", GetShowBadge(item));
        item.Classes.Set("md-badge-text", !string.IsNullOrEmpty(GetBadgeText(item)));

        var secondary = GetVariant(item) == MaterialTabVariant.Secondary;
        item.Classes.Set("md-primary", !secondary);
        item.Classes.Set("md-secondary", secondary);
    }

    private static void UpdateContainers(TabControl tabs)
    {
        foreach (var container in tabs.GetRealizedContainers())
        {
            if (container is not TabItem item)
                continue;
            UpdateHeaderLayout(item);
            UpdateScrollableState(item);
        }
    }

    private static void UpdateScrollableState(TabItem item)
    {
        var tabs = item.FindAncestorOfType<TabControl>();
        item.Classes.Set("md-scrollable", tabs is not null && GetIsScrollable(tabs));
    }

    private static void OnSelectionChanged(TabControl tabs, SelectionChangedEventArgs e)
    {
        if (!ReferenceEquals(e.Source, tabs) || !GetIsScrollable(tabs) || tabs.SelectedIndex < 0)
            return;
        if (tabs.ContainerFromIndex(tabs.SelectedIndex) is not Control container)
            return;

        // Realized containers can be replaced by the deferred selection pipeline.
        Dispatcher.UIThread.Post(() =>
        {
            if (container.IsAttachedToVisualTree())
                container.BringIntoView();
        }, DispatcherPriority.Loaded);
    }

    private static void OnPointerWheelChanged(TabControl tabs, PointerWheelEventArgs e)
    {
        if (e.Handled || !GetIsScrollable(tabs))
            return;
        if (FindHeaderScroller(tabs) is not { } scroller || !IsWithin(e.Source as Visual, scroller))
            return;

        var vertical = tabs.TabStripPlacement is Dock.Left or Dock.Right;
        var extent = vertical ? scroller.Extent.Height : scroller.Extent.Width;
        var viewport = vertical ? scroller.Viewport.Height : scroller.Viewport.Width;
        var max = extent - viewport;
        if (max <= 0.5)
            return;

        var delta = Math.Abs(e.Delta.X) > Math.Abs(e.Delta.Y) ? e.Delta.X : e.Delta.Y;
        if (Math.Abs(delta) < 0.001)
            return;

        var current = vertical ? scroller.Offset.Y : scroller.Offset.X;
        var next = Math.Clamp(current - delta * ScrollStep, 0, max);
        if (Math.Abs(next - current) < 0.5)
            return;

        scroller.Offset = vertical
            ? new Vector(scroller.Offset.X, next)
            : new Vector(next, scroller.Offset.Y);
        e.Handled = true;
    }

    /// <summary>
    /// Resolves the scroll container that hosts the tab headers by walking up from a realized
    /// container, so a consumer-supplied <see cref="TabControl"/> template works without matching
    /// our part names.
    /// </summary>
    private static ScrollViewer? FindHeaderScroller(TabControl tabs)
    {
        foreach (var container in tabs.GetRealizedContainers())
        {
            if (container.FindAncestorOfType<ScrollViewer>() is { } scroller)
                return scroller;
        }
        return null;
    }

    private static bool IsWithin(Visual? source, Visual ancestor)
    {
        for (var visual = source; visual is not null; visual = visual.GetVisualParent())
        {
            if (ReferenceEquals(visual, ancestor))
                return true;
        }
        return false;
    }

    private static void OnKeyDown(TabControl tabs, KeyEventArgs e)
    {
        if (e.Handled || e.KeyModifiers != KeyModifiers.None)
            return;

        // Never consume navigation from the selected page (text fields, lists, nested tabs).
        TabItem? header = null;
        for (var visual = e.Source as Visual; visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is TabItem item)
                header = item;
            if (visual is TabControl owner)
            {
                if (owner != tabs || header is null)
                    return;
                break;
            }
        }
        if (header is null)
            return;

        // A focusable control inside a custom header (a text box, a button) owns its keys.
        if (e.Source is InputElement { Focusable: true } source && !ReferenceEquals(source, header))
            return;

        var count = tabs.ItemCount;
        if (count == 0)
            return;

        if (e.Key is Key.Enter or Key.Space)
        {
            var headerIndex = tabs.IndexFromContainer(header);
            if (header.IsEffectivelyEnabled && headerIndex >= 0)
            {
                tabs.SetCurrentValue(SelectingItemsControl.SelectedIndexProperty, headerIndex);
                e.Handled = true;
            }
            return;
        }

        var vertical = tabs.TabStripPlacement is Dock.Left or Dock.Right;
        var forward = vertical ? Key.Down : Key.Right;
        var backward = vertical ? Key.Up : Key.Left;
        if (!vertical && tabs.FlowDirection == Avalonia.Media.FlowDirection.RightToLeft)
            (forward, backward) = (backward, forward);

        int index;
        int step;
        if (e.Key == Key.Home)
            (index, step) = (-1, 1);
        else if (e.Key == Key.End)
            (index, step) = (count, -1);
        else if (e.Key == forward || e.Key == backward)
            (index, step) = (tabs.IndexFromContainer(header), e.Key == forward ? 1 : -1);
        else
            return;

        for (var visited = 0; visited < count; visited++)
        {
            index = (index + step + count) % count;
            if (tabs.ContainerFromIndex(index) is TabItem { IsEffectivelyEnabled: true, IsEffectivelyVisible: true } target)
            {
                tabs.SetCurrentValue(SelectingItemsControl.SelectedIndexProperty, index);
                target.Focus(NavigationMethod.Directional);
                target.BringIntoView();
                e.Handled = true;
                return;
            }
        }
    }
}
