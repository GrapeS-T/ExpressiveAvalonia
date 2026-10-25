using System;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// A Material 3 navigation bar with horizontal or vertical destinations, single-selection
/// navigation over <see cref="MaterialNavigationBarItem"/> containers, and badges.
/// </summary>
/// <remarks>
/// Selection, focus and automation use the native Avalonia list contracts: the bar is a
/// <see cref="ListBox"/> whose items are <see cref="ListBoxItem"/> containers, so the
/// Selection/SelectionItem patterns, disabled destinations and pointer selection keep working.
/// The automation peer also marks the control as the navigation landmark.
/// Left/Up selects the previous destination, Right/Down the next, Home/End the first or last;
/// disabled destinations are skipped and <see cref="SelectingItemsControl.WrapSelection"/>
/// (on by default here) decides whether the strip wraps. Enter and Space activate the focused
/// destination; the list item consumes both keys to select, so activation is registered as a
/// handled-events-too class handler. Keys keep their native meaning when focus is inside
/// another control, for example a button placed in an item template.
/// Items share the available width (horizontal bar) or height (vertical bar) equally, matching
/// the three to six destination layouts of the source component. The bar is a single tab stop:
/// destinations are not tab stops, so Tab enters and leaves the bar while arrow keys move
/// focus and selection between destinations.
/// </remarks>
[PseudoClasses(":vertical", ":horizontal")]
public class MaterialNavigationBar : ListBox
{
    /// <summary>Defines the <see cref="Orientation"/> property.</summary>
    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<MaterialNavigationBar, Orientation>(nameof(Orientation), Avalonia.Layout.Orientation.Horizontal);

    /// <summary>The icon/label arrangement inherited by destinations whose own orientation is Auto.</summary>
    public static readonly StyledProperty<MaterialNavigationBarItemOrientation> ItemOrientationProperty =
        AvaloniaProperty.Register<MaterialNavigationBar, MaterialNavigationBarItemOrientation>(
            nameof(ItemOrientation), MaterialNavigationBarItemOrientation.Auto);

    static MaterialNavigationBar()
    {
        OrientationProperty.Changed.AddClassHandler<MaterialNavigationBar>((bar, _) => bar.UpdateBarState());
        ItemOrientationProperty.Changed.AddClassHandler<MaterialNavigationBar>((bar, _) => bar.UpdateItemOrientations());
        // ListBoxItem consumes Enter and Space to select; the navigation destination must still
        // execute its command, so observe handled events as well.
        KeyDownEvent.AddClassHandler<MaterialNavigationBar>(
            static (bar, e) => bar.OnNavigationKey(e), RoutingStrategies.Bubble, handledEventsToo: true);
    }

    private ITemplate<Panel?> _ownedItemsPanel = null!;

    public MaterialNavigationBar()
    {
        _ownedItemsPanel = CreateItemsPanel();
        ItemsPanel = _ownedItemsPanel;
        UpdateBarState();
    }

    /// <summary>Raised after a destination is activated by pointer, Enter or Space.</summary>
    public event EventHandler<MaterialNavigationBarItemInvokedEventArgs>? ItemInvoked;

    /// <summary>Stack destinations in a row (the default bottom bar) or in a column.</summary>
    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>Destination anatomy. Auto follows the bar orientation.</summary>
    public MaterialNavigationBarItemOrientation ItemOrientation
    {
        get => GetValue(ItemOrientationProperty);
        set => SetValue(ItemOrientationProperty, value);
    }

    /// <summary>The orientation Auto resolves to for this bar.</summary>
    internal MaterialNavigationBarItemOrientation ResolveItemOrientation() => ItemOrientation switch
    {
        MaterialNavigationBarItemOrientation.Auto => Orientation == Avalonia.Layout.Orientation.Vertical
            ? MaterialNavigationBarItemOrientation.Vertical
            : MaterialNavigationBarItemOrientation.Horizontal,
        var explicitOrientation => explicitOrientation
    };

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) =>
        new MaterialNavigationBarItem();

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        if (container is MaterialNavigationBarItem navItem)
        {
            navItem.Invoked -= OnItemInvoked;
            navItem.Invoked += OnItemInvoked;
            navItem.RefreshBarState();
        }
        InvalidateAutomationChildren();
    }

    protected override void ClearContainerForItemOverride(Control container)
    {
        if (container is MaterialNavigationBarItem navItem)
            navItem.Invoked -= OnItemInvoked;
        base.ClearContainerForItemOverride(container);
        InvalidateAutomationChildren();
    }

    private void InvalidateAutomationChildren()
    {
        if (ControlAutomationPeer.FromElement(this) is NavigationBarAutomationPeer peer)
            peer.InvalidateDestinationChildren();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        UpdateBarState();
    }

    // The bar is the single tab stop; focus enters on the current (or first enabled) destination
    // so a keyboard user can activate it immediately instead of landing on the bare container.
    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        if (!ReferenceEquals(e.Source, this))
            return;

        var index = SelectedIndex >= 0 && SelectedIndex < ItemCount ? SelectedIndex : FindEnabled(0, 1);
        if (ContainerFromIndex(index) is Control container && container.Focus(NavigationMethod.Directional))
            e.Handled = true;
    }

    // Destinations share the available width (or height) equally, as in the source layouts.
    private ITemplate<Panel?> CreateItemsPanel() => new FuncTemplate<Panel?>(() =>
        Orientation == Avalonia.Layout.Orientation.Vertical
            ? new UniformGrid { Columns = 1 }
            : new UniformGrid { Rows = 1 });

    private void OnNavigationKey(KeyEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.None)
            return;

        if (e.Key is Key.Enter or Key.Space)
        {
            var item = FindItem(e.Source);
            if (item is null || !item.IsEffectivelyEnabled)
                return;

            var index = IndexFromContainer(item);
            if (index >= 0 && SelectedIndex != index)
                SelectedIndex = index;
            item.Invoke();
            e.Handled = true;
            return;
        }

        // Arrow and Home/End navigation is implemented here because the bar must wrap and skip
        // disabled destinations even when a platform theme leaves the raw list unhandled.
        if (e.Handled)
            return;

        var focused = FindItem(e.Source);
        if (focused is null && !ReferenceEquals(e.Source, this))
            return;

        var rtl = FlowDirection == Avalonia.Media.FlowDirection.RightToLeft;
        switch (e.Key)
        {
            case Key.Down:
                MoveSelection(focused, 1, e);
                return;
            case Key.Up:
                MoveSelection(focused, -1, e);
                return;
            case Key.Right:
                MoveSelection(focused, rtl ? -1 : 1, e);
                return;
            case Key.Left:
                MoveSelection(focused, rtl ? 1 : -1, e);
                return;
            case Key.Home:
                SelectAndFocus(FindEnabled(0, 1));
                e.Handled = true;
                return;
            case Key.End:
                SelectAndFocus(FindEnabled(ItemCount - 1, -1));
                e.Handled = true;
                return;
        }
    }

    private void MoveSelection(MaterialNavigationBarItem? focusedItem, int delta, KeyEventArgs e)
    {
        var count = ItemCount;
        if (count == 0)
            return;

        var current = focusedItem is not null ? IndexFromContainer(focusedItem) : SelectedIndex;
        if (current < 0 || current >= count)
        {
            // Nothing selected yet: the first press enters the strip from the matching end.
            SelectAndFocus(FindEnabled(delta > 0 ? 0 : count - 1, delta));
            e.Handled = true;
            return;
        }

        var next = WrapSelection
            ? FindEnabled((current + delta + count) % count, delta)
            : FindEnabled(current + delta, delta);
        if (next < 0)
            return;

        SelectAndFocus(next);
        e.Handled = true;
    }

    // Walks from start in delta steps, skipping disabled destinations. With WrapSelection the
    // walk circles the strip; otherwise it stops at the ends.
    private int FindEnabled(int start, int delta)
    {
        var count = ItemCount;
        if (count == 0)
            return -1;

        if (!WrapSelection)
        {
            for (var index = start; index >= 0 && index < count; index += delta)
                if (IsDestinationEnabled(index))
                    return index;
            return -1;
        }

        var wrapped = ((start % count) + count) % count;
        for (var i = 0; i < count; i++)
        {
            if (IsDestinationEnabled(wrapped))
                return wrapped;
            wrapped = (wrapped + delta + count) % count;
        }
        return -1;
    }

    // An unrealized container is enabled unless its item disables it after realization.
    private bool IsDestinationEnabled(int index) =>
        ContainerFromIndex(index) is not Control container || container.IsEffectivelyEnabled;

    private void SelectAndFocus(int index)
    {
        if (index < 0 || index >= ItemCount)
            return;

        SelectedIndex = index;
        // Directional focus marks the state as keyboard focus, which reveals the focus ring.
        (ContainerFromIndex(index) as Control)?.Focus(NavigationMethod.Directional);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new NavigationBarAutomationPeer(this);

    // Returns the destination that owns the event source. A nested focusable control (for
    // example a button inside a custom item template) keeps its own Enter/Space handling.
    private MaterialNavigationBarItem? FindItem(object? source)
    {
        for (var visual = source as Visual; visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is MaterialNavigationBarItem item && ItemsControlFromItemContainer(item) == this)
                return item;
            if (visual is InputElement { Focusable: true })
                return null;
            if (ReferenceEquals(visual, this))
                return null;
        }
        return null;
    }

    private void OnItemInvoked(object? sender, EventArgs e)
    {
        if (sender is not MaterialNavigationBarItem item)
            return;

        var index = IndexFromContainer(item);
        if (index >= 0 && SelectedIndex != index)
            SelectedIndex = index;
        ItemInvoked?.Invoke(this, new MaterialNavigationBarItemInvokedEventArgs(item, index));
    }

    private void UpdateBarState()
    {
        var vertical = Orientation == Avalonia.Layout.Orientation.Vertical;
        PseudoClasses.Set(":vertical", vertical);
        PseudoClasses.Set(":horizontal", !vertical);
        // Only replace the panel this control created; a consumer-supplied panel is preserved.
        if (ReferenceEquals(ItemsPanel, _ownedItemsPanel))
        {
            _ownedItemsPanel = CreateItemsPanel();
            ItemsPanel = _ownedItemsPanel;
        }
        UpdateItemOrientations();
    }

    private void UpdateItemOrientations()
    {
        foreach (var container in GetRealizedContainers())
            if (container is MaterialNavigationBarItem item)
                item.RefreshBarState();
    }

    // The destinations are the list's automation children, so a screen reader announces the
    // navigation landmark followed by its destinations instead of anonymous presenter nodes.
    private sealed class NavigationBarAutomationPeer(MaterialNavigationBar owner) : ListBoxAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;

        protected override string GetClassNameCore() => nameof(MaterialNavigationBar);

        protected override AutomationLandmarkType? GetLandmarkTypeCore() => AutomationLandmarkType.Navigation;

        protected override IReadOnlyList<AutomationPeer> GetOrCreateChildrenCore()
        {
            var children = new List<AutomationPeer>();
            foreach (var container in owner.GetRealizedContainers())
                if (GetOrCreate(container) is { } peer)
                    children.Add(peer);
            return children;
        }

        internal void InvalidateDestinationChildren()
        {
            InvalidateChildren();
            RaiseChildrenChangedEvent();
        }
    }
}

/// <summary>Describes the navigation destination activated by pointer, Enter or Space.</summary>
public sealed class MaterialNavigationBarItemInvokedEventArgs : EventArgs
{
    public MaterialNavigationBarItemInvokedEventArgs(MaterialNavigationBarItem item, int index)
    {
        Item = item;
        Index = index;
    }

    public MaterialNavigationBarItem Item { get; }
    public int Index { get; }
}
