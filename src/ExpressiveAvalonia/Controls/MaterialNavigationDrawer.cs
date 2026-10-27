using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace ExpressiveAvalonia.Controls;

/// <summary>Reports a single-selection change in a <see cref="MaterialNavigationDrawer"/>.</summary>
public sealed class MaterialNavigationSelectionChangedEventArgs : RoutedEventArgs
{
    internal MaterialNavigationSelectionChangedEventArgs(
        RoutedEvent? routedEvent,
        MaterialNavigationItem? removedItem,
        MaterialNavigationItem? addedItem)
        : base(routedEvent)
    {
        RemovedItem = removedItem;
        AddedItem = addedItem;
    }

    /// <summary>The destination that lost selection, or null.</summary>
    public MaterialNavigationItem? RemovedItem { get; }

    /// <summary>The destination that gained selection, or null when the drawer was cleared.</summary>
    public MaterialNavigationItem? AddedItem { get; }
}

/// <summary>The visual contract of a drawer sheet.</summary>
public enum MaterialNavigationDrawerVariant
{
    /// <summary>Standard/permanent/dismissible sheet: surface color and square corners.</summary>
    Standard,

    /// <summary>Modal sheet: surface-container-low and large end corners (source CornerLargeEnd).</summary>
    Modal
}

/// <summary>The Material navigation drawer surface: a grouped, single-selection destination list.</summary>
/// <remarks>
/// <para>
/// The container matches the source component: a 360 DIP surface-container-low card with a
/// 16 DIP radius, 12 DIP padding and a vertically scrolling list. Add
/// <see cref="MaterialNavigationSectionHeader"/> and <see cref="MaterialNavigationDivider"/>
/// items between <see cref="MaterialNavigationItem"/> items to form groups; the source drawer is
/// exactly such a flat sequence, so groups stay in document order and are not separate
/// containers.
/// </para>
/// <para>
/// Selection is single-choice. <see cref="SelectedItem"/> and <see cref="SelectedIndex"/> are
/// two-way and stay synchronized with the item's own <see cref="MaterialNavigationItem.IsSelected"/>.
/// Up, Down, Home and End move focus across enabled items with wrap-around; Space and Enter
/// select. Items register themselves when they attach, so items declared in XAML, added later or
/// nested in an ordinary panel all participate.
/// </para>
/// <para>
/// Hosting in <see cref="MaterialNavigationDrawerHost"/> adds modal dismissal, scrim, focus
/// trapping and slide motion.
/// </para>
/// </remarks>
[PseudoClasses(":empty")]
[TemplatePart("PART_ItemsPresenter", typeof(ItemsPresenter))]
public sealed class MaterialNavigationDrawer : ItemsControl
{
    /// <summary>Defines the <see cref="SelectedItem"/> property.</summary>
    public static readonly StyledProperty<MaterialNavigationItem?> SelectedItemProperty =
        AvaloniaProperty.Register<MaterialNavigationDrawer, MaterialNavigationItem?>(
            nameof(SelectedItem), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Defines the <see cref="SelectedIndex"/> property.</summary>
    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<MaterialNavigationDrawer, int>(
            nameof(SelectedIndex), -1, defaultBindingMode: BindingMode.TwoWay,
            validate: static value => value >= -1);

    /// <summary>Defines the <see cref="SelectionChanged"/> routed event.</summary>
    public static readonly RoutedEvent<MaterialNavigationSelectionChangedEventArgs> SelectionChangedEvent =
        RoutedEvent.Register<MaterialNavigationDrawer, MaterialNavigationSelectionChangedEventArgs>(
            nameof(SelectionChanged), RoutingStrategies.Bubble);

    /// <summary>Defines the <see cref="Variant"/> property.</summary>
    public static readonly StyledProperty<MaterialNavigationDrawerVariant> VariantProperty =
        AvaloniaProperty.Register<MaterialNavigationDrawer, MaterialNavigationDrawerVariant>(
            nameof(Variant));

    private readonly List<MaterialNavigationItem> _registered = [];
    private MaterialNavigationItem? _selected;
    private MaterialNavigationItem? _pendingItem;
    private int _pendingIndex = -1;
    private bool _updatingSelection;

    /// <summary>Raised when the selected destination changes.</summary>
    public event EventHandler<MaterialNavigationSelectionChangedEventArgs>? SelectionChanged
    {
        add => AddHandler(SelectionChangedEvent, value);
        remove => RemoveHandler(SelectionChangedEvent, value);
    }

    /// <summary>The selected destination, or null when nothing is selected.</summary>
    public MaterialNavigationItem? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    /// <summary>The index of the selected destination among drawer items, or -1.</summary>
    public int SelectedIndex
    {
        get => GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    /// <summary>Standard or modal sheet colors and shape. The host updates this for its presentation.</summary>
    public MaterialNavigationDrawerVariant Variant
    {
        get => GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    /// <summary>Selects <paramref name="item"/> when it belongs to this drawer.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    public void Select(MaterialNavigationItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!ReferenceEquals(item.Drawer, this) && !CollectNavigationItems().Contains(item))
            return;

        if (ReferenceEquals(_selected, item))
        {
            if (!item.IsSelected)
                item.SetSelected(true);
            return;
        }

        SetSelection(item, raiseEvent: true);
    }

    /// <summary>Clears the selection. Native Material drawers normally keep one destination selected.</summary>
    public void ClearSelection()
    {
        if (_selected is null)
            return;
        SetSelection(null, raiseEvent: true);
    }

    /// <summary>The selectable destinations in document order, ignoring headers and dividers.</summary>
    public IReadOnlyList<MaterialNavigationItem> GetNavigationItems() => CollectNavigationItems();

    internal void Register(MaterialNavigationItem item)
    {
        if (_registered.Contains(item))
            return;

        _registered.Add(item);
        item.Drawer = this;

        if (ReferenceEquals(_pendingItem, item))
        {
            _pendingItem = null;
            SetSelection(item, raiseEvent: true);
            return;
        }

        if (ReferenceEquals(_selected, item))
        {
            item.SetSelected(true);
            return;
        }

        if (item.IsSelected)
        {
            SetSelection(item, raiseEvent: true);
            return;
        }

        if (_pendingIndex >= 0)
            ApplyPendingIndex();
    }

    internal void Unregister(MaterialNavigationItem item)
    {
        if (!_registered.Remove(item))
            return;

        if (ReferenceEquals(_selected, item))
        {
            _selected = null;
            SetSelectionCoreProperties(null, -1);
            RaiseEvent(new MaterialNavigationSelectionChangedEventArgs(SelectionChangedEvent, item, null));
        }
        else if (_pendingIndex >= 0)
        {
            ApplyPendingIndex();
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        ApplyVariant();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == VariantProperty)
        {
            ApplyVariant();
            return;
        }

        if (change.Property == FlowDirectionProperty)
        {
            ApplyVariant();
            return;
        }

        if (change.Property == SelectedItemProperty && !_updatingSelection)
        {
            var item = change.GetNewValue<MaterialNavigationItem?>();
            if (ReferenceEquals(item, _selected))
                return;
            _pendingIndex = -1;
            if (item is not null && !ReferenceEquals(item.Drawer, this) && !CollectNavigationItems().Contains(item))
            {
                // Items are usually generated after the initial binding is applied; keep the
                // request and resolve it when the item registers with this drawer.
                _pendingItem = item;
                return;
            }

            _pendingItem = null;
            SetSelection(item, raiseEvent: true);
        }
        else if (change.Property == SelectedIndexProperty && !_updatingSelection)
        {
            var index = change.GetNewValue<int>();
            var items = CollectNavigationItems();
            _pendingItem = null;
            if (index < 0)
            {
                _pendingIndex = -1;
                SetSelection(null, raiseEvent: true);
            }
            else if (index < items.Count)
            {
                _pendingIndex = -1;
                SetSelection(items[index], raiseEvent: true);
            }
            else
            {
                // Items may still be generating; resolve again when they register.
                _pendingIndex = index;
            }
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!e.Handled && e.KeyModifiers == KeyModifiers.None && MoveFocus(e))
            return;
        base.OnKeyDown(e);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new MaterialNavigationDrawerAutomationPeer(this);

    private bool MoveFocus(KeyEventArgs e)
    {
        var items = CollectNavigationItems();
        if (items.Count == 0)
            return false;

        var current = (e.Source as Visual)?
            .GetSelfAndVisualAncestors()
            .OfType<MaterialNavigationItem>()
            .FirstOrDefault();
        if (current is null)
            return false;

        var index = items.IndexOf(current);
        if (index < 0)
            return false;

        var target = e.Key switch
        {
            Key.Down => FindFocusable(items, index, 1),
            Key.Up => FindFocusable(items, index, -1),
            Key.Home => FindFocusable(items, -1, 1),
            Key.End => FindFocusable(items, items.Count, -1),
            _ => -1,
        };
        if (target < 0)
            return false;

        var next = items[target];
        next.Focus(NavigationMethod.Directional, e.KeyModifiers);
        next.BringIntoView();
        e.Handled = true;
        return true;
    }

    private static int FindFocusable(IReadOnlyList<MaterialNavigationItem> items, int from, int step)
    {
        for (var offset = 1; offset <= items.Count; offset++)
        {
            var index = ((from + (step * offset)) % items.Count + items.Count) % items.Count;
            var item = items[index];
            if (item.Focusable && item.IsEffectivelyEnabled && item.IsEffectivelyVisible)
                return index;
        }

        return -1;
    }

    private void SetSelection(MaterialNavigationItem? item, bool raiseEvent)
    {
        if (ReferenceEquals(_selected, item))
            return;

        var removed = _selected;
        _selected = item;

        _updatingSelection = true;
        try
        {
            removed?.SetSelected(false);
            item?.SetSelected(true);
        }
        finally
        {
            _updatingSelection = false;
        }

        var index = item is null ? -1 : CollectNavigationItems().IndexOf(item);
        if (index < 0 && item is not null)
            index = Items.IndexOf(item);
        SetSelectionCoreProperties(item, index);

        if (raiseEvent)
            RaiseEvent(new MaterialNavigationSelectionChangedEventArgs(SelectionChangedEvent, removed, item));
    }

    private void SetSelectionCoreProperties(MaterialNavigationItem? item, int index)
    {
        _updatingSelection = true;
        try
        {
            SetCurrentValue(SelectedItemProperty, item);
            SetCurrentValue(SelectedIndexProperty, index);
        }
        finally
        {
            _updatingSelection = false;
        }
    }

    private void ApplyPendingIndex()
    {
        var items = CollectNavigationItems();
        if (_pendingIndex < 0)
            return;

        if (_pendingIndex >= items.Count)
            return;

        var index = _pendingIndex;
        _pendingIndex = -1;
        SetSelection(items[index], raiseEvent: true);
    }

    private List<MaterialNavigationItem> CollectNavigationItems()
    {
        // Realized containers are ordered by the items panel, which keeps SelectedIndex in
        // document order even after insertion or recycling. Before the template is applied the
        // registration order is the only available order.
        if (Presenter?.Panel is { } panel)
        {
            var realized = panel.GetVisualDescendants()
                .OfType<MaterialNavigationItem>()
                .Where(item => ReferenceEquals(item.Drawer, this))
                .ToList();
            if (realized.Count > 0)
                return realized;
        }

        return [.. _registered];
    }

    private void ApplyVariant()
    {
        var modal = Variant == MaterialNavigationDrawerVariant.Modal;
        PseudoClasses.Set(":modal", modal);
        PseudoClasses.Set(":standard", !modal);

        // Source CornerLargeEnd is the end edge; mirror it for right-to-left reading.
        var key = FlowDirection == Avalonia.Media.FlowDirection.RightToLeft
            ? "MaterialShapeLargeStartCornerRadius"
            : "MaterialShapeLargeEndCornerRadius";
        var radius = modal && this.TryFindResource(key, out var value) && value is CornerRadius corners
            ? corners
            : default;
        SetCurrentValue(CornerRadiusProperty, radius);
    }

    private sealed class MaterialNavigationDrawerAutomationPeer(MaterialNavigationDrawer owner)
        : ItemsControlAutomationPeer(owner), ISelectionProvider
    {
        public new MaterialNavigationDrawer Owner => (MaterialNavigationDrawer)base.Owner;

        public bool CanSelectMultiple => false;

        public bool IsSelectionRequired => false;


        public IReadOnlyList<AutomationPeer> GetSelection()
        {
            if (Owner.SelectedItem is { } item &&
                ControlAutomationPeer.CreatePeerForElement(item) is { } peer)
                return [peer];
            return [];
        }
    }
}
