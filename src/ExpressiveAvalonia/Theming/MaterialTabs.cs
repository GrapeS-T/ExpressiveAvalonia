using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Tokens;

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
/// Feedback is the shared bounded ripple (the source RippleNode), coloured with the tab's
/// <em>selected</em> content role even while unselected, as Tab.kt does. The header content tint
/// animates between the selected and unselected roles: <c>DefaultEffects</c> while selecting and
/// <c>FastEffects</c> while deselecting. The animation only paints the internal header; a
/// consumer's <see cref="TemplatedControl.Foreground"/> or brush binding stays the target.
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
    /// Whether a keyboard focus ring is drawn. Defaults to false, matching upstream: the source
    /// focus indication is the 10% focus state layer, and this ring is a desktop preview
    /// extension. Focus never changes the selection; Arrow/Home/End are a desktop extension that
    /// moves focus and selection together, Space/Enter select the focused header.
    /// </summary>
    public static readonly AttachedProperty<bool> ShowFocusIndicatorProperty =
        AvaloniaProperty.RegisterAttached<MaterialTabs, Control, bool>(
            "ShowFocusIndicator", false, inherits: true);

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
        VariantProperty.Changed.AddClassHandler<TabControl>(static (tabs, _) =>
        {
            UpdateContainers(tabs);
            UpdateIndicator(tabs, animate: false);
        });
        // Containers are realized lazily, so resolve the header classes when each tab loads.
        // TemplateApplied runs before the first render, so no frame shows the hidden presenters.
        TemplatedControl.TemplateAppliedEvent.AddClassHandler<TabItem>(static (item, e) =>
        {
            UpdateHeaderLayout(item);
            UpdateScrollableState(item);
            AttachTint(item, e.NameScope.Find<Control>("HeaderArea"), settled: false);
        });
        Control.LoadedEvent.AddClassHandler<TabItem>(static (item, _) =>
        {
            UpdateHeaderLayout(item);
            UpdateScrollableState(item);
            AttachTint(item, FindHeaderArea(item), settled: true);
        });
        // Tab.kt's TabTransition animates the content colour between the selected and unselected
        // roles. The theme expresses those endpoints as the TabItem's effective Foreground, so a
        // changed foreground is the retarget signal; the selection state chooses the spec.
        TemplatedControl.ForegroundProperty.Changed.AddClassHandler<TabItem>(
            static (item, _) => UpdateTint(item, animate: true));
        Control.LoadedEvent.AddClassHandler<TabControl>(static (tabs, _) => UpdateContainers(tabs));
        Control.LoadedEvent.AddClassHandler<TabControl>(static (tabs, _) => HookIndicator(tabs));
        TemplatedControl.TemplateAppliedEvent.AddClassHandler<TabControl>(static (tabs, e) =>
        {
            var state = IndicatorStates.GetOrCreateValue(tabs);
            state.LeftAnimation?.Dispose();
            state.WidthAnimation?.Dispose();
            state.LeftAnimation = null;
            state.WidthAnimation = null;
            state.HasValue = false;
            state.Layer = e.NameScope.Find<Canvas>("PART_IndicatorLayer");
            state.Indicator = e.NameScope.Find<Border>("PART_Indicator");
            UpdateIndicator(tabs, animate: false);
        });
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

    private sealed class TabTintState
    {
        public Control? HeaderArea;
        public SolidColorBrush? Brush;
        public MaterialColorAnimation? Animation;
        public Color Current;
        public bool HasValue;
        public bool Settled;
        public bool Hooked;
    }

    private static readonly ConditionalWeakTable<TabItem, TabTintState> TintStates = new();

    private static Control? FindHeaderArea(TabItem item) =>
        item.GetVisualDescendants().OfType<Grid>().FirstOrDefault(grid => grid.Name == "HeaderArea");

    /// <summary>
    /// Owns the source's <c>TabTransition</c> content colour for one header. It animates between
    /// the selected and unselected endpoints with <c>DefaultEffects</c> while selecting and
    /// <c>FastEffects</c> while deselecting. The endpoints remain the themed (or consumer-bound)
    /// <see cref="TemplatedControl.Foreground"/>: the animation paints its own brush on the
    /// internal header and never mutates the consumer's foreground or brush.
    /// </summary>
    private static void AttachTint(TabItem item, Control? headerArea, bool settled)
    {
        var state = TintStates.GetOrCreateValue(item);
        if (!ReferenceEquals(state.HeaderArea, headerArea))
        {
            state.Animation?.Dispose();
            state.Animation = null;
            state.HeaderArea = headerArea;
            state.Brush = null;
            state.HasValue = false;
        }

        if (!state.Hooked)
        {
            state.Hooked = true;
            item.PropertyChanged += (_, e) =>
            {
                // Enabling/disabling swaps the effective Foreground through the theme, but the
                // source defines no content-colour transition for it; snap instead of springing.
                if (e.Property == InputElement.IsEffectivelyEnabledProperty ||
                    e.Property == Visual.IsVisibleProperty)
                    UpdateTint(item, animate: false);
            };
            // A detaching header cannot keep animating: paint the final role and drop the target so
            // a recycled header cannot keep a dead template alive. The Loaded handler
            // re-resolves the header and seeds again on re-attach.
            item.DetachedFromVisualTree += (_, _) =>
            {
                if (state.Brush is { } brush)
                    brush.Color = ResolveTintColor(item);
                state.Animation?.Dispose();
                state.Animation = null;
                state.HeaderArea = null;
                state.Brush = null;
                state.HasValue = false;
                state.Settled = false;
            };
        }

        if (settled)
            state.Settled = true;

        if (headerArea is null)
            return;

        if (state.Brush is null)
        {
            state.Brush = new SolidColorBrush(ResolveTintColor(item));
            headerArea.SetValue(TextElement.ForegroundProperty, state.Brush);
        }

        // Only changes after Loaded are user-driven and eligible to animate, so the first frame
        // stays at the source's static content colour instead of animating theme setup. Settling
        // before the header lookup keeps this valid regardless of Loaded/TemplateApplied order.
        UpdateTint(item, animate: false);
    }

    /// <summary>
    /// Retargets the header tint. The painted colour is applied to the tab's own brush, so the
    /// control's <see cref="TemplatedControl.Foreground"/> stays the untouched target of the
    /// transition; rapid reversals reuse the same animation and change its spring, preserving the
    /// mid-frame position and velocity.
    /// </summary>
    private static void UpdateTint(TabItem item, bool animate)
    {
        if (!TintStates.TryGetValue(item, out var state) || state.Brush is null ||
            state.HeaderArea is null)
            return;

        var target = ResolveTintColor(item);
        if (!animate || !state.HasValue || !state.Settled || !item.IsEffectivelyEnabled ||
            MaterialMotion.GetIsReduced(item) || !item.IsAttachedToVisualTree() ||
            !item.IsEffectivelyVisible)
        {
            state.Animation?.Dispose();
            state.Animation = null;
            state.Current = target;
            state.HasValue = true;
            state.Brush.Color = target;
            return;
        }

        var spring = item.IsSelected ? MaterialSpring.EffectsDefault : MaterialSpring.EffectsFast;
        if (state.Animation is null)
        {
            var brush = state.Brush;
            state.Animation = new MaterialColorAnimation(item, state.Current, color =>
            {
                state.Current = color;
                brush.Color = color;
            }, spring);
        }

        // One instance spans direction changes: Retarget(target, spring) samples the outgoing
        // simulation before swapping the spec, so a rapid reversal keeps its velocity as well as
        // its painted position, like Compose's updateTransition animateColor.
        state.Animation.Retarget(target, spring);
    }

    private static Color ResolveTintColor(TabItem item)
    {
        // The themed (or consumer-bound) Foreground is the target of the transition.
        if (item.Foreground is ISolidColorBrush { Color: var color })
            return color;

        var key = !item.IsEffectivelyEnabled
            ? "MaterialOnSurfaceColor"
            : item.IsSelected
                ? GetVariant(item) == MaterialTabVariant.Secondary
                    ? "MaterialOnSurfaceColor"
                    : "MaterialPrimaryColor"
                : "MaterialOnSurfaceVariantColor";
        return item.TryFindResource(key, out var value) && value is Color resolved
            ? resolved
            : Colors.Transparent;
    }

    private static void OnSelectionChanged(TabControl tabs, SelectionChangedEventArgs e)
    {
        if (!ReferenceEquals(e.Source, tabs))
            return;

        UpdateIndicator(tabs, animate: true);
        if (!GetIsScrollable(tabs) || tabs.SelectedIndex < 0)
            return;

        // Realized containers can be replaced by the deferred selection pipeline. Center the
        // selected header in the viewport, clamped to the content, as ScrollableTabRow does.
        Dispatcher.UIThread.Post(() => CenterSelectedHeader(tabs, animate: true),
            DispatcherPriority.Loaded);
    }

    private static void CenterSelectedHeader(TabControl tabs, bool animate)
    {
        if (!GetIsScrollable(tabs) || tabs.SelectedIndex < 0)
            return;
        if (tabs.ContainerFromIndex(tabs.SelectedIndex) is not TabItem container ||
            !container.IsAttachedToVisualTree() ||
            FindHeaderScroller(tabs) is not { } scroller)
            return;

        var vertical = tabs.TabStripPlacement is Dock.Left or Dock.Right;
        if (vertical)
        {
            container.BringIntoView();
            return;
        }

        // center.X is measured against the viewport, so target = offset + center - viewport / 2 is
        // independent of the offset the spring currently renders. Recomputing during a running
        // morph therefore retargets without drift, and remains mirrored for RTL, where Avalonia
        // offsets and TranslatePoint are physical. TabRow publishes the same clamped offset
        // (TabPosition.calculateTabOffset).
        if (container.TranslatePoint(new Point(container.Bounds.Width / 2, 0), scroller) is not { } center)
            return;
        var viewport = scroller.Viewport.Width;
        var max = Math.Max(0, scroller.Extent.Width - viewport);
        var target = Math.Clamp(scroller.Offset.X + center.X - viewport / 2, 0, max);
        AnimateHeaderScroll(tabs, scroller, target, animate);
    }

    private sealed class TabScrollState
    {
        public ScrollViewer? Scroller;
        public EventHandler<PointerPressedEventArgs>? PointerPressed;
        public MaterialMotion.SpringAnimation? Animation;
        public bool Hooked;
    }

    private static readonly ConditionalWeakTable<TabControl, TabScrollState> ScrollStates = new();

    /// <summary>
    /// Scrolls the header viewport with the source <c>DefaultSpatial</c> spring
    /// (<c>ScrollableTabData.onLaidOut</c> → <c>animateScrollTo(..., DefaultSpatial)</c>) instead
    /// of assigning <see cref="ScrollViewer.Offset"/> immediately. The single running spring is
    /// retargeted, which preserves the current velocity across rapid reversals, and a user
    /// gesture cancels it so native scrolling is never fought.
    /// </summary>
    private static void AnimateHeaderScroll(TabControl tabs, ScrollViewer scroller, double target,
        bool animate)
    {
        var state = ScrollStates.GetOrCreateValue(tabs);
        HookScroll(tabs);
        if (!ReferenceEquals(state.Scroller, scroller))
        {
            StopHeaderScroll(state);
            HookScroller(state, scroller);
        }

        var current = scroller.Offset.X;
        if (!animate || MaterialMotion.GetIsReduced(tabs) || !tabs.IsAttachedToVisualTree() ||
            !tabs.IsEffectivelyVisible || Math.Abs(target - current) < 0.5)
        {
            StopHeaderScroll(state);
            ApplyHeaderOffset(scroller, target);
            return;
        }

        if (state.Animation is { } running)
        {
            running.Retarget(target);
            return;
        }

        state.Animation = MaterialMotion.Animate(
            tabs, current, target,
            value => ApplyHeaderOffset(scroller, value),
            MaterialSpring.SpatialDefault,
            visibilityThreshold: 0.5);
    }

    private static void ApplyHeaderOffset(ScrollViewer scroller, double x)
    {
        scroller.SetCurrentValue(ScrollViewer.OffsetProperty,
            new Vector(x, scroller.Offset.Y));
    }

    private static void HookScroll(TabControl tabs)
    {
        var state = ScrollStates.GetOrCreateValue(tabs);
        if (state.Hooked)
            return;
        state.Hooked = true;
        tabs.DetachedFromVisualTree += (_, _) => DetachHeaderScroll(tabs);
    }

    private static void HookScroller(TabScrollState state, ScrollViewer scroller)
    {
        if (state.Scroller is { } previous && state.PointerPressed is { } oldHandler)
            previous.RemoveHandler(InputElement.PointerPressedEvent, oldHandler);
        state.Scroller = scroller;
        state.PointerPressed = (_, e) =>
        {
            // Only a scrollbar/thumb press owns the viewport. A press on a tab header must not
            // cancel the spring: rapid 1→4→2 selection retargets it with velocity instead.
            if (e.Source is Visual visual &&
                visual.FindAncestorOfType<ScrollBar>(includeSelf: true) is not null)
                StopHeaderScroll(state);
        };
        scroller.AddHandler(InputElement.PointerPressedEvent, state.PointerPressed,
            RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private static void StopHeaderScroll(TabScrollState state)
    {
        state.Animation?.Dispose();
        state.Animation = null;
    }

    private static void DetachHeaderScroll(TabControl tabs)
    {
        if (!ScrollStates.TryGetValue(tabs, out var state))
            return;
        StopHeaderScroll(state);
        if (state.Scroller is { } scroller && state.PointerPressed is { } handler)
            scroller.RemoveHandler(InputElement.PointerPressedEvent, handler);
        state.Scroller = null;
        state.PointerPressed = null;
    }

    private sealed class TabIndicatorState
    {
        public Canvas? Layer;
        public Border? Indicator;
        public MaterialMotion.SpringAnimation? LeftAnimation;
        public MaterialMotion.SpringAnimation? WidthAnimation;
        public bool Hooked;
        public bool HasValue;
    }

    private static readonly ConditionalWeakTable<TabControl, TabIndicatorState> IndicatorStates = new();

    /// <summary>
    /// Positions the single source indicator over the selected header. The primary indicator is
    /// the intrinsic header width minus the source's 2 x 16 DIP text padding, clamped to 24 DIP and
    /// to the tab width, then centred in the tab; the secondary indicator spans the tab. Both
    /// animate with MotionScheme DefaultSpatial (the palette spring), not a linear translation.
    /// </summary>
    private static void UpdateIndicator(TabControl tabs, bool animate)
    {
        var state = IndicatorStates.GetOrCreateValue(tabs);
        if (state.Layer is null || state.Indicator is null)
            return;

        var horizontal = tabs.TabStripPlacement is Dock.Top or Dock.Bottom;
        state.Indicator.IsVisible = horizontal;
        if (!horizontal || tabs.SelectedIndex < 0)
            return;

        if (tabs.ContainerFromIndex(tabs.SelectedIndex) is not TabItem tab ||
            !tab.IsAttachedToVisualTree() ||
            tab.TranslatePoint(default, state.Layer) is not { } origin)
            return;

        var tabWidth = tab.Bounds.Width;
        if (!double.IsFinite(tabWidth) || tabWidth <= 0)
            return;

        var secondary = GetVariant(tabs) == MaterialTabVariant.Secondary;
        var indicatorWidth = secondary
            ? tabWidth
            : Math.Clamp(HeaderContentWidth(tab), 24, tabWidth);
        var left = origin.X + (secondary ? 0 : (tabWidth - indicatorWidth) / 2);
        var height = 3d;

        state.Indicator.Height = height;
        state.Indicator.CornerRadius = secondary ? default : new CornerRadius(3);
        var top = tabs.TabStripPlacement == Dock.Bottom
            ? origin.Y
            : origin.Y + tab.Bounds.Height - height;
        Canvas.SetTop(state.Indicator, top);

        if (!animate || !state.HasValue || MaterialMotion.GetIsReduced(tabs))
        {
            state.LeftAnimation?.Dispose();
            state.WidthAnimation?.Dispose();
            state.LeftAnimation = null;
            state.WidthAnimation = null;
            Canvas.SetLeft(state.Indicator, left);
            state.Indicator.Width = indicatorWidth;
        }
        else if (state.LeftAnimation is null || state.WidthAnimation is null)
        {
            var indicator = state.Indicator;
            state.LeftAnimation = MaterialMotion.Animate(
                tabs, Canvas.GetLeft(indicator), left,
                value => Canvas.SetLeft(indicator, value), MaterialSpring.SpatialDefault);
            state.WidthAnimation = MaterialMotion.Animate(
                tabs, indicator.Width, indicatorWidth,
                value => indicator.Width = value, MaterialSpring.SpatialDefault);
        }
        else
        {
            state.LeftAnimation.Retarget(left);
            state.WidthAnimation.Retarget(indicatorWidth);
        }

        state.HasValue = true;
    }

    private static double HeaderContentWidth(TabItem tab)
    {
        var header = tab.GetVisualDescendants().OfType<StackPanel>()
            .FirstOrDefault(panel => panel.Name == "HeaderContent");
        if (header is not null && header.Bounds.Width > 0)
            return header.Bounds.Width;
        return Math.Max(0, tab.Bounds.Width - tab.Padding.Left - tab.Padding.Right);
    }

    private static void HookIndicator(TabControl tabs)
    {
        var state = IndicatorStates.GetOrCreateValue(tabs);
        UpdateIndicator(tabs, animate: false);
        if (state.Hooked)
            return;
        state.Hooked = true;
        // Header widths, scroll offsets and the selected container change without a selection
        // event (resize, dynamic add/remove, font change). Re-aim the shared indicator.
        tabs.LayoutUpdated += (_, _) => UpdateIndicator(tabs, animate: true);
        tabs.SizeChanged += (_, _) => UpdateIndicator(tabs, animate: false);
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

        // A wheel gesture takes ownership of the viewport; never let the center spring overwrite it.
        if (ScrollStates.TryGetValue(tabs, out var state))
            StopHeaderScroll(state);

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
                // The horizontal scrollable strip centers the selection with its DefaultSpatial
                // spring from OnSelectionChanged; an immediate BringIntoView would fight it.
                if (!GetIsScrollable(tabs) || vertical)
                    target.BringIntoView();
                e.Handled = true;
                return;
            }
        }
    }
}
