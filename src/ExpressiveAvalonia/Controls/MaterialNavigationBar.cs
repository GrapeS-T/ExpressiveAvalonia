using System;
using System.Collections.Generic;
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
/// Material navigation bar: the classic <c>NavigationBar</c> and the expressive
/// <c>ShortNavigationBar</c> share one native single-selection ListBox contract.
/// </summary>
/// <remarks>
/// <para>
/// Adapted from <c>NavigationBar.kt</c> and <c>ShortNavigationBar.kt</c> at commit
/// <c>77b2199d6f056c2fe829ce25523c34a905e769c1</c>. <see cref="Kind"/> classifies the two source
/// components explicitly: <see cref="MaterialNavigationBarKind.Classic"/> is 80 DIP tall with
/// 8 DIP between weighted items; <see cref="MaterialNavigationBarKind.Short"/> is 64 DIP tall and
/// uses <see cref="ShortArrangement"/>. <see cref="MaterialShortNavigationBar"/> fixes the latter.
/// </para>
/// <para>
/// Selection, focus and automation use the native Avalonia list contracts: the bar is a
/// <see cref="ListBox"/> whose items are <see cref="ListBoxItem"/> containers, so Selection/
/// SelectionItem patterns, disabled destinations and pointer selection keep working. The bar is a
/// single tab stop; arrow keys move selection and focus, with RTL-aware horizontal arrows and
/// wrapping controlled by <see cref="SelectingItemsControl.WrapSelection"/>. Enter and Space
/// activate the focused destination.
/// </para>
/// <para>
/// The navigation surface only reports selection and invocation; it never creates a router or
/// swaps pages.
/// </para>
/// </remarks>
[PseudoClasses(":classic", ":short", ":vertical", ":horizontal")]
public class MaterialNavigationBar : ListBox
{
    /// <summary>Classic bar or expressive short bar.</summary>
    public static readonly StyledProperty<MaterialNavigationBarKind> KindProperty =
        AvaloniaProperty.Register<MaterialNavigationBar, MaterialNavigationBarKind>(nameof(Kind));

    /// <summary>Desktop extension: stack destinations in a column instead of a row.</summary>
    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<MaterialNavigationBar, Orientation>(nameof(Orientation), Orientation.Horizontal);

    /// <summary>The icon position inherited by destinations whose own position is Auto.</summary>
    public static readonly StyledProperty<MaterialNavigationItemIconPosition> ItemIconPositionProperty =
        AvaloniaProperty.Register<MaterialNavigationBar, MaterialNavigationItemIconPosition>(
            nameof(ItemIconPosition), MaterialNavigationItemIconPosition.Top);

    /// <summary>Equal-weight or centered grouping for the expressive short bar.</summary>
    public static readonly StyledProperty<MaterialShortNavigationBarArrangement> ShortArrangementProperty =
        AvaloniaProperty.Register<MaterialNavigationBar, MaterialShortNavigationBarArrangement>(
            nameof(ShortArrangement));

    private ITemplate<Panel?> _ownedItemsPanel = null!;

    static MaterialNavigationBar()
    {
        KindProperty.Changed.AddClassHandler<MaterialNavigationBar>((bar, _) => bar.OnLayoutModeChanged());
        OrientationProperty.Changed.AddClassHandler<MaterialNavigationBar>((bar, _) => bar.OnLayoutModeChanged());
        ShortArrangementProperty.Changed.AddClassHandler<MaterialNavigationBar>((bar, _) => bar.OnLayoutModeChanged());
        ItemIconPositionProperty.Changed.AddClassHandler<MaterialNavigationBar>((bar, _) => bar.UpdateItemStates());
        // ListBoxItem consumes Enter and Space to select; the navigation destination must still
        // execute its command, so observe handled events as well.
        KeyDownEvent.AddClassHandler<MaterialNavigationBar>(
            static (bar, e) => bar.OnNavigationKey(e), RoutingStrategies.Bubble, handledEventsToo: true);
    }

    public MaterialNavigationBar()
    {
        _ownedItemsPanel = CreateItemsPanel();
        ItemsPanel = _ownedItemsPanel;
        UpdateBarState();
    }

    /// <summary>Raised after a destination is activated by pointer, Enter or Space.</summary>
    public event EventHandler<MaterialNavigationBarItemInvokedEventArgs>? ItemInvoked;

    public MaterialNavigationBarKind Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>Destination anatomy inherited by items with <see cref="MaterialNavigationItemIconPosition.Auto"/>.</summary>
    public MaterialNavigationItemIconPosition ItemIconPosition
    {
        get => GetValue(ItemIconPositionProperty);
        set => SetValue(ItemIconPositionProperty, value);
    }

    public MaterialShortNavigationBarArrangement ShortArrangement
    {
        get => GetValue(ShortArrangementProperty);
        set => SetValue(ShortArrangementProperty, value);
    }

    /// <summary>The icon position Auto resolves to for this bar.</summary>
    internal MaterialNavigationItemIconPosition ResolveItemIconPosition() =>
        ItemIconPosition == MaterialNavigationItemIconPosition.Auto
            ? MaterialNavigationItemIconPosition.Top
            : ItemIconPosition;

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

    // The bar is the single tab stop; focus enters on the current (or first enabled) destination.
    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        if (!ReferenceEquals(e.Source, this))
            return;

        var index = SelectedIndex >= 0 && SelectedIndex < ItemCount ? SelectedIndex : FindEnabled(0, 1);
        if (ContainerFromIndex(index) is Control container && container.Focus(NavigationMethod.Directional))
            e.Handled = true;
    }

    private ITemplate<Panel?> CreateItemsPanel() => new FuncTemplate<Panel?>(() =>
        new MaterialNavigationBarPanel
        {
            Kind = Kind,
            Arrangement = ShortArrangement,
            Orientation = Orientation,
            Spacing = Kind == MaterialNavigationBarKind.Classic ? 8 : 0
        });

    private void OnLayoutModeChanged()
    {
        UpdateBarState();
    }

    private void UpdateBarState()
    {
        var shortBar = Kind == MaterialNavigationBarKind.Short;
        var vertical = Orientation == Avalonia.Layout.Orientation.Vertical;
        PseudoClasses.Set(":classic", !shortBar);
        PseudoClasses.Set(":short", shortBar);
        PseudoClasses.Set(":vertical", vertical);
        PseudoClasses.Set(":horizontal", !vertical);

        // Rebuild only the panel this control created; a consumer-supplied panel is preserved.
        if (ReferenceEquals(ItemsPanel, _ownedItemsPanel))
        {
            _ownedItemsPanel = CreateItemsPanel();
            ItemsPanel = _ownedItemsPanel;
        }
        UpdateItemStates();
    }

    private void UpdateItemStates()
    {
        foreach (var container in GetRealizedContainers())
            if (container is MaterialNavigationBarItem item)
                item.RefreshBarState();
    }

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

        if (e.Handled)
            return;

        var focused = FindItem(e.Source);
        if (focused is null && !ReferenceEquals(e.Source, this))
            return;

        var rtl = FlowDirection == Avalonia.Media.FlowDirection.RightToLeft;
        var vertical = Orientation == Avalonia.Layout.Orientation.Vertical;
        switch (e.Key)
        {
            case Key.Down:
                MoveSelection(focused, 1, e);
                return;
            case Key.Up:
                MoveSelection(focused, -1, e);
                return;
            case Key.Right:
                if (!vertical)
                    MoveSelection(focused, rtl ? -1 : 1, e);
                return;
            case Key.Left:
                if (!vertical)
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

    // Walks from start in delta steps, skipping disabled destinations.
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

    private bool IsDestinationEnabled(int index) =>
        ContainerFromIndex(index) is not Control container || container.IsEffectivelyEnabled;

    private void SelectAndFocus(int index)
    {
        if (index < 0 || index >= ItemCount)
            return;

        SelectedIndex = index;
        (ContainerFromIndex(index) as Control)?.Focus(NavigationMethod.Directional);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new NavigationBarAutomationPeer(this);

    // Returns the destination that owns the event source. A nested focusable control keeps its
    // own Enter/Space handling.
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

/// <summary>The expressive short navigation bar from <c>ShortNavigationBar.kt</c>.</summary>
public sealed class MaterialShortNavigationBar : MaterialNavigationBar
{
    public MaterialShortNavigationBar()
    {
        Kind = MaterialNavigationBarKind.Short;
    }

    protected override Type StyleKeyOverride => typeof(MaterialNavigationBar);
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