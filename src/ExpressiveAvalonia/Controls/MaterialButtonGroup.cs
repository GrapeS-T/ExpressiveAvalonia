using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Controls;

/// <summary>The three Material Expressive button group anatomies.</summary>
/// <remarks>
/// <see cref="Standard"/> separates its buttons (18/12/8 DIP by size) and every button is an
/// independent action. <see cref="Connected"/> joins segments with a 2-DIP gap and paints the
/// selected segment in the secondary color with a pill silhouette. <see cref="Segmented"/>
/// joins outlined segments that share a single 1-DIP border and fills the selected segment with
/// the secondary container color.
/// </remarks>
public enum MaterialButtonGroupKind { Standard, Connected, Segmented }

/// <summary>How many items a <see cref="MaterialButtonGroup"/> may select at once.</summary>
public enum MaterialButtonGroupSelectionMode
{
    /// <summary>Items are plain action buttons; checking is suppressed.</summary>
    None,

    /// <summary>Exactly one item stays selected; clicking the selected item keeps it selected.</summary>
    Single,

    /// <summary>Any number of items can be selected independently.</summary>
    Multiple
}

/// <summary>Reports the selection after a group selection change.</summary>
public sealed class MaterialButtonGroupSelectionChangedEventArgs : RoutedEventArgs
{
    public MaterialButtonGroupSelectionChangedEventArgs(RoutedEvent routedEvent,
        int selectedIndex, object? selectedItem, IReadOnlyList<object?> selectedItems) : base(routedEvent)
    {
        SelectedIndex = selectedIndex;
        SelectedItem = selectedItem;
        SelectedItems = selectedItems;
    }

    /// <summary>The index of the item that last became selected, or -1.</summary>
    public int SelectedIndex { get; }

    /// <summary>The item that last became selected, or null.</summary>
    public object? SelectedItem { get; }

    /// <summary>Every selected item in index order.</summary>
    public IReadOnlyList<object?> SelectedItems { get; }
}

/// <summary>A Material button group built from <see cref="MaterialButtonGroupItem"/> containers.</summary>
/// <remarks>
/// <para>Items can be supplied as <see cref="MaterialButtonGroupItem"/> instances, as plain
/// content (a string becomes the label), or through <see cref="ItemsControl.ItemsSource"/>.</para>
/// <para>Connected and segmented groups are a single tab stop: Tab enters the group at the
/// selected item, arrow keys move focus with wrapping, Home and End jump to the ends, and
/// Space/Enter toggles the focused item natively. Standard groups keep one tab stop per button.</para>
/// <para>Selection follows the Material behavior: <see cref="MaterialButtonGroupSelectionMode.Single"/>
/// never empties through user interaction; assign <see cref="SelectedIndex"/> = -1 to clear it
/// programmatically. Motion uses the theme duration tokens, so Motion="Reduced" removes it.</para>
/// </remarks>
[TemplatePart("PART_ItemsPresenter", typeof(ItemsPresenter))]
public class MaterialButtonGroup : ItemsControl
{
    /// <summary>The group anatomy.</summary>
    public static readonly StyledProperty<MaterialButtonGroupKind> KindProperty =
        AvaloniaProperty.Register<MaterialButtonGroup, MaterialButtonGroupKind>(
            nameof(Kind), MaterialButtonGroupKind.Standard,
            coerce: static (o, value) => ((MaterialButtonGroup)o).CoerceKind(value));

    /// <summary>Whether the group selects nothing, one item, or many items.</summary>
    public static readonly StyledProperty<MaterialButtonGroupSelectionMode> SelectionModeProperty =
        AvaloniaProperty.Register<MaterialButtonGroup, MaterialButtonGroupSelectionMode>(nameof(SelectionMode));

    /// <summary>Horizontal groups lay items out left to right; vertical groups top to bottom.</summary>
    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<MaterialButtonGroup, Orientation>(nameof(Orientation), Avalonia.Layout.Orientation.Horizontal);

    /// <summary>The Material Expressive size applied to every item.</summary>
    public static readonly StyledProperty<MaterialControlSize> ControlSizeProperty =
        AvaloniaProperty.Register<MaterialButtonGroup, MaterialControlSize>(
            nameof(ControlSize), MaterialControlSize.Small);

    /// <summary>Round uses pill silhouettes; square uses the size corner radius.</summary>
    public static readonly StyledProperty<MaterialButtonShape> ButtonShapeProperty =
        AvaloniaProperty.Register<MaterialButtonGroup, MaterialButtonShape>(nameof(ButtonShape));

    /// <summary>The color treatment of a standard group.</summary>
    public static readonly StyledProperty<MaterialButtonVariant> VariantProperty =
        AvaloniaProperty.Register<MaterialButtonGroup, MaterialButtonVariant>(
            nameof(Variant), MaterialButtonVariant.Filled);

    /// <summary>Whether a selected item shows a check mark before its content.</summary>
    public static readonly StyledProperty<bool> ShowSelectedIconProperty =
        AvaloniaProperty.Register<MaterialButtonGroup, bool>(nameof(ShowSelectedIcon));

    /// <summary>Whether a selected item replaces its leading icon with the check mark.</summary>
    public static readonly StyledProperty<bool> ReplaceIconWhenSelectedProperty =
        AvaloniaProperty.Register<MaterialButtonGroup, bool>(nameof(ReplaceIconWhenSelected));

    /// <summary>
    /// The segmented button density from 0 to -3, which lowers the segment height by 4 DIP per step.
    /// Only <see cref="MaterialButtonGroupKind.Segmented"/> uses it.
    /// </summary>
    public static readonly StyledProperty<int> DensityProperty =
        AvaloniaProperty.Register<MaterialButtonGroup, int>(nameof(Density),
            validate: static value => value is >= -3 and <= 0);

    /// <summary>The index of the selected item, or -1.</summary>
    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<MaterialButtonGroup, int>(
            nameof(SelectedIndex), -1,
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay,
            coerce: static (o, value) => Math.Max(-1, value));

    /// <summary>The selected item, or null.</summary>
    public static readonly StyledProperty<object?> SelectedItemProperty =
        AvaloniaProperty.Register<MaterialButtonGroup, object?>(
            nameof(SelectedItem), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>Whether moving focus with the arrow keys also selects in single-selection groups.</summary>
    public static readonly StyledProperty<bool> SelectOnFocusProperty =
        AvaloniaProperty.Register<MaterialButtonGroup, bool>(nameof(SelectOnFocus));

    /// <summary>Raised after the selection changes, whether by pointer, keyboard or code.</summary>
    public static readonly RoutedEvent<MaterialButtonGroupSelectionChangedEventArgs> SelectionChangedEvent =
        RoutedEvent.Register<MaterialButtonGroup, MaterialButtonGroupSelectionChangedEventArgs>(
            nameof(SelectionChanged), RoutingStrategies.Bubble);

    private static readonly CornerRadius FullCornerRadius = new(9999);

    private bool _syncing;

    public MaterialButtonGroup()
    {
        ItemsPanel = new FuncTemplate<Panel?>(() => new StackPanel { Orientation = Orientation });
        Items.CollectionChanged += OnItemsChanged;
        AddHandler(KeyDownEvent, OnGroupKeyDown, RoutingStrategies.Tunnel);
    }

    // ItemsControl.OnItemsViewCollectionChanged is private protected, so the group observes the
    // public ItemCollection instead. Containers themselves are refreshed by the prepare callbacks.
    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => RefreshItems();

    /// <summary>Raised after the selection changes, whether by pointer, keyboard or code.</summary>
    public event EventHandler<MaterialButtonGroupSelectionChangedEventArgs>? SelectionChanged
    {
        add => AddHandler(SelectionChangedEvent, value);
        remove => RemoveHandler(SelectionChangedEvent, value);
    }

    public MaterialButtonGroupKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public MaterialButtonGroupSelectionMode SelectionMode { get => GetValue(SelectionModeProperty); set => SetValue(SelectionModeProperty, value); }
    public Orientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }
    public MaterialControlSize ControlSize { get => GetValue(ControlSizeProperty); set => SetValue(ControlSizeProperty, value); }
    public MaterialButtonShape ButtonShape { get => GetValue(ButtonShapeProperty); set => SetValue(ButtonShapeProperty, value); }
    public MaterialButtonVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
    public bool ShowSelectedIcon { get => GetValue(ShowSelectedIconProperty); set => SetValue(ShowSelectedIconProperty, value); }
    public bool ReplaceIconWhenSelected { get => GetValue(ReplaceIconWhenSelectedProperty); set => SetValue(ReplaceIconWhenSelectedProperty, value); }
    public int Density { get => GetValue(DensityProperty); set => SetValue(DensityProperty, value); }
    public int SelectedIndex { get => GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }
    public object? SelectedItem { get => GetValue(SelectedItemProperty); set => SetValue(SelectedItemProperty, value); }
    public bool SelectOnFocus { get => GetValue(SelectOnFocusProperty); set => SetValue(SelectOnFocusProperty, value); }

    /// <summary>The currently selected items in index order.</summary>
    public IReadOnlyList<object?> SelectedItems => GetSelectedItems();

    /// <summary>Lets a derived control pin the anatomy (the segmented button does this).</summary>
    protected virtual MaterialButtonGroupKind CoerceKind(MaterialButtonGroupKind value) => value;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        UpdatePanelOrientation();
        RefreshItems();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == OrientationProperty)
        {
            UpdatePanelOrientation();
            RefreshItems();
        }
        else if (change.Property == KindProperty || change.Property == ControlSizeProperty ||
                 change.Property == ButtonShapeProperty || change.Property == VariantProperty ||
                 change.Property == ShowSelectedIconProperty || change.Property == ReplaceIconWhenSelectedProperty ||
                 change.Property == DensityProperty || change.Property == SelectionModeProperty)
        {
            RefreshItems();
        }
        else if (change.Property == SelectedIndexProperty && !_syncing)
        {
            ApplySelectedIndex();
        }
        else if (change.Property == SelectedItemProperty && !_syncing)
        {
            ApplySelectedItem();
        }
    }

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey) =>
        NeedsContainer<MaterialButtonGroupItem>(item, out recycleKey);

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) =>
        new MaterialButtonGroupItem();

    protected override void ContainerForItemPreparedOverride(Control container, object? item, int index)
    {
        base.ContainerForItemPreparedOverride(container, item, index);
        if (container is not MaterialButtonGroupItem groupItem)
            return;

        groupItem.PropertyChanged -= OnItemPropertyChanged;
        groupItem.PropertyChanged += OnItemPropertyChanged;
        ApplyItemAppearance(groupItem, index);

        if (SelectionMode == MaterialButtonGroupSelectionMode.None)
            SetItemChecked(groupItem, false);
        else if (SelectionMode == MaterialButtonGroupSelectionMode.Single)
            SetItemChecked(groupItem, index == SelectedIndex);
        else if (index == SelectedIndex)
            SetItemChecked(groupItem, true);

        ApplyItemCorners(groupItem);
        UpdateTabStops();
    }

    protected override void ClearContainerForItemOverride(Control container)
    {
        if (container is MaterialButtonGroupItem groupItem)
        {
            groupItem.PropertyChanged -= OnItemPropertyChanged;
            groupItem.Group = null;
        }
        base.ClearContainerForItemOverride(container);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new MaterialButtonGroupAutomationPeer(this);

    private void UpdatePanelOrientation()
    {
        if (ItemsPanelRoot is StackPanel panel)
            panel.Orientation = Orientation;
    }

    private List<MaterialButtonGroupItem> GetGroupItems()
    {
        var items = new List<MaterialButtonGroupItem>(ItemCount);
        for (var i = 0; i < ItemCount; i++)
        {
            if (ContainerFromIndex(i) is MaterialButtonGroupItem item)
                items.Add(item);
        }
        return items;
    }

    private void RefreshItems()
    {
        var items = GetGroupItems();
        for (var i = 0; i < items.Count; i++)
        {
            ApplyItemAppearance(items[i], i);
            ApplyItemCorners(items[i]);
        }
        UpdateTabStops();

        // Keep a selection that outlived its item from pointing past the end.
        if (SelectedIndex > 0 && SelectedIndex >= ItemCount)
            SetCurrentValue(SelectedIndexProperty, -1);
    }

    private void ApplyItemAppearance(MaterialButtonGroupItem item, int index)
    {
        item.Group = this;
        item.Kind = Kind;
        item.ControlSize = ControlSize;
        item.ButtonShape = ButtonShape;
        item.Variant = Variant;
        item.ShowSelectedIcon = ShowSelectedIcon;
        item.ReplaceIconWhenSelected = ReplaceIconWhenSelected;
        item.GroupOrientation = Orientation;

        var count = ItemCount;
        item.Position = count <= 1
            ? MaterialButtonGroupItemPosition.Only
            : index == 0
                ? MaterialButtonGroupItemPosition.First
                : index == count - 1
                    ? MaterialButtonGroupItemPosition.Last
                    : MaterialButtonGroupItemPosition.Middle;

        ApplyItemSpacing(item, index);

        // Standard groups at the two smallest sizes keep the 48-DIP target with a 32/40-DIP
        // painted container, matching the source button anatomy.
        item.ContainerInset = Kind == MaterialButtonGroupKind.Standard &&
                              ControlSize is MaterialControlSize.ExtraSmall or MaterialControlSize.Small
            ? new Thickness(0, ControlSize == MaterialControlSize.ExtraSmall ? 8 : 4)
            : default;

        if (Kind == MaterialButtonGroupKind.Segmented)
            item.MinHeight = 40 + Density * 4;
    }

    private void ApplyItemSpacing(MaterialButtonGroupItem item, int index)
    {
        if (index <= 0)
        {
            item.Margin = default;
            return;
        }

        var gap = Kind switch
        {
            MaterialButtonGroupKind.Segmented => 0,
            MaterialButtonGroupKind.Connected => 2,
            _ => StandardGap(ControlSize)
        };

        item.Margin = Orientation == Avalonia.Layout.Orientation.Horizontal
            ? new Thickness(gap, 0, 0, 0)
            : new Thickness(0, gap, 0, 0);
    }

    private static double StandardGap(MaterialControlSize size) => size switch
    {
        MaterialControlSize.ExtraSmall => 18,
        MaterialControlSize.Small => 12,
        _ => 8
    };

    private double InnerRadius => Kind switch
    {
        MaterialButtonGroupKind.Segmented => 0,
        _ => ControlSize switch
        {
            MaterialControlSize.ExtraSmall => 4,
            MaterialControlSize.Small => 4,
            MaterialControlSize.Medium => 8,
            MaterialControlSize.Large => 12,
            _ => 16
        }
    };

    private double SquareRadius => ControlSize switch
    {
        MaterialControlSize.ExtraSmall => 12,
        MaterialControlSize.Small => 12,
        MaterialControlSize.Medium => 16,
        MaterialControlSize.Large => 28,
        _ => 28
    };

    private void ApplyItemCorners(MaterialButtonGroupItem item)
    {
        var inner = new CornerRadius(InnerRadius);
        CornerRadius corners;

        switch (Kind)
        {
            case MaterialButtonGroupKind.Segmented:
                corners = PositionCorners(item.Position, FullCornerRadius, default);
                break;
            case MaterialButtonGroupKind.Connected when item.IsChecked == true:
                corners = FullCornerRadius;
                break;
            case MaterialButtonGroupKind.Connected:
                // The source Type axis (Round/Square) does not change connected-segment geometry:
                // outer edges stay pill-shaped and the inner seam uses the size corner radius.
                corners = PositionCorners(item.Position, FullCornerRadius, inner);
                break;
            default:
                corners = ButtonShape == MaterialButtonShape.Round
                    ? FullCornerRadius
                    : new CornerRadius(SquareRadius);
                break;
        }

        item.SetCurrentValue(TemplatedControl.CornerRadiusProperty, corners);

        var isFirst = item.Position is MaterialButtonGroupItemPosition.First or MaterialButtonGroupItemPosition.Only;
        var thickness = Kind == MaterialButtonGroupKind.Segmented
            ? Orientation == Avalonia.Layout.Orientation.Horizontal
                ? new Thickness(isFirst ? 1 : 0, 1, 1, 1)
                : new Thickness(1, isFirst ? 1 : 0, 1, 1)
            : Kind == MaterialButtonGroupKind.Standard && Variant == MaterialButtonVariant.Outlined
                ? new Thickness(1)
                : default;

        item.SetCurrentValue(TemplatedControl.BorderThicknessProperty, thickness);
    }

    private CornerRadius PositionCorners(
        MaterialButtonGroupItemPosition position, CornerRadius outer, CornerRadius inner)
    {
        var vertical = Orientation == Avalonia.Layout.Orientation.Vertical;
        return position switch
        {
            MaterialButtonGroupItemPosition.Only => outer,
            MaterialButtonGroupItemPosition.First => vertical
                ? new CornerRadius(outer.TopLeft, outer.TopRight, inner.BottomRight, inner.BottomLeft)
                : new CornerRadius(outer.TopLeft, inner.TopRight, inner.BottomRight, outer.BottomLeft),
            MaterialButtonGroupItemPosition.Last => vertical
                ? new CornerRadius(inner.TopLeft, inner.TopRight, outer.BottomRight, outer.BottomLeft)
                : new CornerRadius(inner.TopLeft, outer.TopRight, outer.BottomRight, inner.BottomLeft),
            _ => inner
        };
    }

    private void OnItemPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != ToggleButton.IsCheckedProperty || _syncing || sender is not MaterialButtonGroupItem item)
            return;

        var isChecked = item.IsChecked == true;

        switch (SelectionMode)
        {
            case MaterialButtonGroupSelectionMode.None:
                if (isChecked)
                    SetItemChecked(item, false);
                return;

            case MaterialButtonGroupSelectionMode.Single:
                if (!isChecked)
                {
                    // Single selection never empties through interaction; clear it via SelectedIndex.
                    SetItemChecked(item, true);
                    return;
                }

                foreach (var other in GetGroupItems())
                {
                    if (ReferenceEquals(other, item) || other.IsChecked != true)
                        continue;
                    SetItemChecked(other, false);
                    ApplyItemCorners(other);
                }
                break;

            case MaterialButtonGroupSelectionMode.Multiple when !isChecked:
                // Deselecting reports the last remaining selection instead of the cleared item.
                var remaining = GetGroupItems().LastOrDefault(static i => i.IsChecked == true);
                ApplyItemCorners(item);
                UpdateSelection(remaining);
                return;
        }

        ApplyItemCorners(item);
        UpdateSelection(item);
    }

    private void SetItemChecked(MaterialButtonGroupItem item, bool value)
    {
        if (item.IsChecked == value)
            return;

        var previous = _syncing;
        _syncing = true;
        item.SetCurrentValue(ToggleButton.IsCheckedProperty, value);
        _syncing = previous;
    }

    private void UpdateSelection(MaterialButtonGroupItem? primary)
    {
        var index = primary is null ? -1 : IndexFromContainer(primary);
        var value = primary is null ? null : ItemFromContainer(primary);
        var changed = index != SelectedIndex || !Equals(value, SelectedItem);

        var previous = _syncing;
        _syncing = true;
        SetCurrentValue(SelectedIndexProperty, index);
        SetCurrentValue(SelectedItemProperty, value);
        _syncing = previous;

        UpdateTabStops();

        if (changed)
            RaiseSelectionChanged();
    }

    private void ApplySelectedIndex()
    {
        var index = SelectedIndex;
        var previous = _syncing;
        _syncing = true;

        foreach (var item in GetGroupItems())
        {
            var itemIndex = IndexFromContainer(item);
            var shouldCheck = SelectionMode switch
            {
                MaterialButtonGroupSelectionMode.None => false,
                MaterialButtonGroupSelectionMode.Single => itemIndex == index,
                _ => index < 0 ? false : itemIndex == index || item.IsChecked == true
            };

            if (item.IsChecked != shouldCheck)
                item.SetCurrentValue(ToggleButton.IsCheckedProperty, shouldCheck);
            ApplyItemCorners(item);
        }

        SetCurrentValue(SelectedItemProperty,
            index >= 0 && ContainerFromIndex(index) is { } container ? ItemFromContainer(container) : null);
        _syncing = previous;

        UpdateTabStops();
        RaiseSelectionChanged();
    }

    private void ApplySelectedItem()
    {
        var index = -1;
        var position = 0;
        foreach (var candidate in Items)
        {
            if (Equals(candidate, SelectedItem))
            {
                index = position;
                break;
            }
            position++;
        }
        SetCurrentValue(SelectedIndexProperty, index);
    }

    private void UpdateTabStops()
    {
        var items = GetGroupItems();
        if (items.Count == 0)
            return;

        if (Kind == MaterialButtonGroupKind.Standard)
        {
            foreach (var item in items)
                item.IsTabStop = true;
            return;
        }

        var tabIndex = items.FindIndex(static i => i.IsChecked == true);
        if (tabIndex < 0)
            tabIndex = items.FindIndex(static i => i.IsEffectivelyEnabled);

        for (var i = 0; i < items.Count; i++)
            items[i].IsTabStop = i == tabIndex;
    }

    private void OnGroupKeyDown(object? sender, KeyEventArgs e)
    {
        if (Kind == MaterialButtonGroupKind.Standard || e.Handled || e.KeyModifiers != KeyModifiers.None)
            return;

        var items = GetGroupItems();
        var enabled = items.Where(static i => i.IsEffectivelyEnabled).ToList();
        if (enabled.Count == 0)
            return;

        var current = items.FindIndex(static i => i.IsFocused);
        var currentEnabled = current < 0 ? -1 : enabled.IndexOf(items[current]);
        var horizontal = Orientation == Avalonia.Layout.Orientation.Horizontal;
        var rightToLeft = FlowDirection == Avalonia.Media.FlowDirection.RightToLeft;

        int target;
        switch (e.Key)
        {
            case Key.Home:
                target = 0;
                break;
            case Key.End:
                target = enabled.Count - 1;
                break;
            case Key.Left when horizontal:
                target = Step(currentEnabled, rightToLeft ? 1 : -1);
                break;
            case Key.Right when horizontal:
                target = Step(currentEnabled, rightToLeft ? -1 : 1);
                break;
            case Key.Up when !horizontal:
                target = Step(currentEnabled, -1);
                break;
            case Key.Down when !horizontal:
                target = Step(currentEnabled, 1);
                break;
            default:
                return;
        }

        var next = enabled[(target + enabled.Count) % enabled.Count];
        if (SelectOnFocus && SelectionMode == MaterialButtonGroupSelectionMode.Single)
            SetItemChecked(next, true);
        next.Focus(NavigationMethod.Directional);
        e.Handled = true;

        static int Step(int current, int delta) =>
            current < 0 ? (delta > 0 ? 0 : -1) : current + delta;
    }

    private IReadOnlyList<object?> GetSelectedItems()
    {
        var selected = new List<object?>();
        foreach (var item in GetGroupItems())
        {
            if (item.IsChecked == true)
                selected.Add(ItemFromContainer(item));
        }
        return selected;
    }

    private void RaiseSelectionChanged() =>
        RaiseEvent(new MaterialButtonGroupSelectionChangedEventArgs(
            SelectionChangedEvent, SelectedIndex, SelectedItem, GetSelectedItems()));

    private sealed class MaterialButtonGroupAutomationPeer(MaterialButtonGroup owner)
        : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
        protected override string GetClassNameCore() => nameof(MaterialButtonGroup);
    }
}
