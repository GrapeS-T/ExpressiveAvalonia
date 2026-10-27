using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using AvaloniaOrientation = Avalonia.Layout.Orientation;
using ExpressiveAvalonia.Theming;
using ExpressiveAvalonia.Tokens;

namespace ExpressiveAvalonia.Controls;

/// <summary>The three Material Expressive button group anatomies.</summary>
/// <remarks>
/// <see cref="Standard"/> separates its buttons (12 DIP at the source Small size) and every button
/// is an independent action that expands on press while its neighbours compress.
/// <see cref="Connected"/> joins segments with a 2-DIP gap, paints the selected segment in the
/// secondary color with a pill silhouette, and morphs the inner seam corners on press.
/// <see cref="Segmented"/> joins outlined segments that overlap by the 1-DIP stroke and fills the
/// selected segment with the secondary container color.
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
/// <para>
/// Items can be supplied as <see cref="MaterialButtonGroupItem"/> instances, as plain content (a
/// string becomes the label), or through <see cref="ItemsControl.ItemsSource"/>.
/// </para>
/// <para>
/// Standard groups reproduce the source <c>ButtonGroup</c>: items are separated by
/// <see cref="ButtonGroupSmallTokens.BetweenSpace"/>, a pressed item expands by
/// <see cref="ExpandedRatio"/> of its width and compresses its neighbours, and items that no longer
/// fit move into the overflow menu behind the trailing icon button. Connected and segmented groups
/// are a single tab stop: Tab enters at the selected item, arrows move focus with wrapping, Home and
/// End jump to the ends, and Space/Enter toggles the focused item natively.
/// </para>
/// <para>
/// Selection follows the Material behavior: <see cref="MaterialButtonGroupSelectionMode.Single"/>
/// never empties through user interaction; assign <see cref="SelectedIndex"/> = -1 to clear it
/// programmatically. In multiple mode <see cref="SelectedItem"/>/<see cref="SelectedIndex"/> track
/// the most recently selected item and <see cref="SelectedIndices"/> exposes the full set. Shape
/// morphs are driven by the source FastSpatial spring and snap under reduced motion.
/// </para>
/// </remarks>
[TemplatePart("PART_ItemsPresenter", typeof(ItemsPresenter))]
[TemplatePart("PART_OverflowButton", typeof(Button))]
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
        AvaloniaProperty.Register<MaterialButtonGroup, Orientation>(nameof(Orientation), AvaloniaOrientation.Horizontal);

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
    /// Only <see cref="MaterialButtonGroupKind.Segmented"/> uses it; it is an Avalonia extension.
    /// </summary>
    public static readonly StyledProperty<int> DensityProperty =
        AvaloniaProperty.Register<MaterialButtonGroup, int>(nameof(Density),
            validate: static value => value is >= -3 and <= 0);

    /// <summary>The index of the selected item, or -1.</summary>
    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<MaterialButtonGroup, int>(
            nameof(SelectedIndex), -1,
            defaultBindingMode: BindingMode.TwoWay,
            coerce: static (o, value) => Math.Max(-1, value));

    /// <summary>The selected item, or null.</summary>
    public static readonly StyledProperty<object?> SelectedItemProperty =
        AvaloniaProperty.Register<MaterialButtonGroup, object?>(
            nameof(SelectedItem), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether moving focus with the arrow keys also selects in single-selection groups.</summary>
    public static readonly StyledProperty<bool> SelectOnFocusProperty =
        AvaloniaProperty.Register<MaterialButtonGroup, bool>(nameof(SelectOnFocus));

    /// <summary>
    /// The width fraction a pressed standard item borrows from its neighbours; the source
    /// <c>ButtonGroupDefaults.ExpandedRatio</c>.
    /// </summary>
    public static readonly StyledProperty<double> ExpandedRatioProperty =
        AvaloniaProperty.Register<MaterialButtonGroup, double>(nameof(ExpandedRatio), 0.15,
            validate: static value => double.IsFinite(value) && value is >= 0 and <= 1);

    /// <summary>
    /// Whether standard groups move items that do not fit into a trailing overflow menu. Segmented
    /// and connected groups keep their single roving tab stop and do not overflow.
    /// </summary>
    public static readonly StyledProperty<bool> IsOverflowEnabledProperty =
        AvaloniaProperty.Register<MaterialButtonGroup, bool>(nameof(IsOverflowEnabled), true);

    /// <summary>Accessible name of the overflow button. Override to localize.</summary>
    public static readonly StyledProperty<string> OverflowButtonAccessibleNameProperty =
        AvaloniaProperty.Register<MaterialButtonGroup, string>(
            nameof(OverflowButtonAccessibleName), "More options");

    /// <summary>Raised after the selection changes, whether by pointer, keyboard or code.</summary>
    public static readonly RoutedEvent<MaterialButtonGroupSelectionChangedEventArgs> SelectionChangedEvent =
        RoutedEvent.Register<MaterialButtonGroup, MaterialButtonGroupSelectionChangedEventArgs>(
            nameof(SelectionChanged), RoutingStrategies.Bubble);

    // Shape morphs follow internal/AnimatedShape.kt: one FastSpatial progress spring per item
    // between logical CornerBasedShape targets. CornerFull stays a 50 percent corner of the painted
    // container, so no corner is ever a numeric 9999 DIP value and a resize re-resolves it.
    private const double ShapeProgressThreshold = 0.01;

    // Matches the 12-DIP margin of the overflow button in ButtonGroups.axaml.
    private const double OverflowGap = 12;

    private readonly Dictionary<MaterialButtonGroupItem, ItemExpansion> _expansions = [];
    private readonly Dictionary<MaterialButtonGroupItem, ItemShape> _shapes = [];
    private readonly Dictionary<MaterialButtonGroupItem, double> _itemWidths = [];
    private readonly List<MaterialButtonGroupItem> _hiddenByOverflow = [];

    private Button? _overflowButton;
    private MaterialButtonGroupPanel? _panel;
    private double _availableWidth = double.PositiveInfinity;
    private bool _syncing;
    private bool _applyingOverflow;

    public MaterialButtonGroup()
    {
        ItemsPanel = new FuncTemplate<Panel?>(() => new MaterialButtonGroupPanel());
        Items.CollectionChanged += OnItemsChanged;
        Focusable = false;
        AddHandler(KeyDownEvent, OnGroupKeyDown, RoutingStrategies.Tunnel);
        LayoutUpdated += OnLayoutUpdated;
    }

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
    public double ExpandedRatio { get => GetValue(ExpandedRatioProperty); set => SetValue(ExpandedRatioProperty, value); }
    public bool IsOverflowEnabled { get => GetValue(IsOverflowEnabledProperty); set => SetValue(IsOverflowEnabledProperty, value); }
    public string OverflowButtonAccessibleName { get => GetValue(OverflowButtonAccessibleNameProperty); set => SetValue(OverflowButtonAccessibleNameProperty, value); }

    /// <summary>The currently selected items in index order.</summary>
    public IReadOnlyList<object?> SelectedItems => GetSelectedItems();

    /// <summary>The indices of every selected item in ascending order.</summary>
    public IReadOnlyList<int> SelectedIndices
    {
        get
        {
            var indices = new List<int>();
            for (var i = 0; i < ItemCount; i++)
            {
                if (ContainerFromIndex(i) is MaterialButtonGroupItem { IsChecked: true })
                    indices.Add(i);
            }
            return indices;
        }
    }

    /// <summary>Selects or clears one item while honoring <see cref="SelectionMode"/>.</summary>
    public void Select(int index, bool selected)
    {
        if (index < 0 || index >= ItemCount || ContainerFromIndex(index) is not MaterialButtonGroupItem item)
            return;

        if (SelectionMode == MaterialButtonGroupSelectionMode.Single && selected)
        {
            SelectSingle(item);
            return;
        }

        SetItemChecked(item, selected && SelectionMode != MaterialButtonGroupSelectionMode.None);
        if (!selected)
            UpdateSelection(GetGroupItems().LastOrDefault(static i => i.IsChecked == true));
        else
            UpdateSelection(item);
    }

    private void SelectSingle(MaterialButtonGroupItem item)
    {
        var previous = _syncing;
        _syncing = true;
        foreach (var other in GetGroupItems())
        {
            if (!ReferenceEquals(other, item) && other.IsChecked == true)
                other.SetCurrentValue(ToggleButton.IsCheckedProperty, false);
        }
        item.SetCurrentValue(ToggleButton.IsCheckedProperty, true);
        _syncing = previous;
        UpdateSelection(item);
    } 

    /// <summary>Lets a derived control pin the anatomy (the segmented button does this).</summary>
    protected virtual MaterialButtonGroupKind CoerceKind(MaterialButtonGroupKind value) => value;

    protected override Size MeasureOverride(Size availableSize)
    {
        _availableWidth = availableSize.Width;
        return base.MeasureOverride(availableSize);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _overflowButton = e.NameScope.Find<Button>("PART_OverflowButton");
        if (_overflowButton is not null)
        {
            _overflowButton.Click += OnOverflowButtonClick;
            AutomationProperties.SetName(_overflowButton, OverflowButtonAccessibleName);
        }
        UpdatePanel();
        RefreshItems();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == OrientationProperty || change.Property == KindProperty)
        {
            UpdatePanel();
            RefreshItems();
        }
        else if (change.Property == ControlSizeProperty || change.Property == ButtonShapeProperty ||
                 change.Property == VariantProperty || change.Property == ShowSelectedIconProperty ||
                 change.Property == ReplaceIconWhenSelectedProperty || change.Property == DensityProperty ||
                 change.Property == SelectionModeProperty)
        {
            RefreshItems();
        }
        else if (change.Property == ExpandedRatioProperty)
        {
            if (_panel is not null)
                _panel.ExpandedRatio = ExpandedRatio;
        }
        else if (change.Property == IsOverflowEnabledProperty || change.Property == FlowDirectionProperty)
        {
            ApplyOverflowVisibility();
            // Logical corners keep their start/end meaning; only the physical mapping flips.
            if (change.Property == FlowDirectionProperty)
                ResolveAllItemCorners();
        }
        else if (change.Property == SelectedIndexProperty && !_syncing)
        {
            ApplySelectedIndex();
        }
        else if (change.Property == SelectedItemProperty && !_syncing)
        {
            ApplySelectedItem();
        }
        else if (change.Property == OverflowButtonAccessibleNameProperty && _overflowButton is not null)
        {
            AutomationProperties.SetName(_overflowButton, OverflowButtonAccessibleName);
        }
    }

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey) =>
        NeedsContainer<MaterialButtonGroupItem>(item, out recycleKey);

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) =>
        new MaterialButtonGroupItem();

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        if (container is not MaterialButtonGroupItem groupItem)
            return;

        groupItem.PropertyChanged -= OnItemPropertyChanged;
        groupItem.PropertyChanged += OnItemPropertyChanged;
        groupItem.DetachedFromVisualTree -= OnItemDetached;
        groupItem.DetachedFromVisualTree += OnItemDetached;
        groupItem.SizeChanged -= OnItemSizeChanged;
        groupItem.SizeChanged += OnItemSizeChanged;
        ApplyItemAppearance(groupItem, index);

        if (SelectionMode == MaterialButtonGroupSelectionMode.None)
            SetItemChecked(groupItem, false);
        else if (SelectionMode == MaterialButtonGroupSelectionMode.Single)
            SetItemChecked(groupItem, index == SelectedIndex);
        else if (index == SelectedIndex)
            SetItemChecked(groupItem, true);

        ApplyItemCorners(groupItem, animate: false);
        UpdateTabStops();
    }

    protected override void ClearContainerForItemOverride(Control container)
    {
        if (container is MaterialButtonGroupItem groupItem)
        {
            groupItem.PropertyChanged -= OnItemPropertyChanged;
            groupItem.DetachedFromVisualTree -= OnItemDetached;
            groupItem.SizeChanged -= OnItemSizeChanged;
            DisposeItemAnimations(groupItem);
            _shapes.Remove(groupItem);
            groupItem.Group = null;
            _itemWidths.Remove(groupItem);
            _hiddenByOverflow.Remove(groupItem);
        }
        base.ClearContainerForItemOverride(container);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new MaterialButtonGroupAutomationPeer(this);

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Selection follows item identity across insertion and removal: a selected object keeps its
        // selection and the anchor index is recomputed, while a removed selection leaves only the
        // multiple-mode selections that still exist.
        var anchor = SelectedItem;
        RefreshItems();
        if (SelectionMode != MaterialButtonGroupSelectionMode.Multiple || anchor is not null)
        {
            var index = IndexOfItem(anchor);
            if (index != SelectedIndex)
                SetCurrentValue(SelectedIndexProperty, index);
        }
    }

    private int IndexOfItem(object? item)
    {
        if (item is null)
            return -1;
        for (var i = 0; i < ItemCount; i++)
        {
            if (ContainerFromIndex(i) is { } container && Equals(ItemFromContainer(container), item))
                return i;
        }
        return -1;
    }

    private void UpdatePanel()
    {
        _panel = ItemsPanelRoot as MaterialButtonGroupPanel;
        if (_panel is null)
            return;

        _panel.Orientation = Orientation;
        _panel.Spacing = SpacingFor(Kind, ControlSize);
        _panel.EqualizeWidths = Kind == MaterialButtonGroupKind.Segmented;
        _panel.ExpandedRatio = ExpandedRatio;
        _panel.InvalidateMeasure();
    }

    // Source spacing: standard 12 DIP (ButtonGroupSmallTokens.BetweenSpace), connected 2 DIP
    // (ConnectedButtonGroupSmallTokens.BetweenSpace), segmented −1 DIP so the 1 DIP outlines of
    // adjacent segments overlap exactly (Arrangement.spacedBy(-space)). The extra standard sizes
    // are an Avalonia extension of the single source Small size.
    private static double SpacingFor(MaterialButtonGroupKind kind, MaterialControlSize size) => kind switch
    {
        MaterialButtonGroupKind.Segmented => -1,
        MaterialButtonGroupKind.Connected => 2,
        _ => size switch
        {
            MaterialControlSize.ExtraSmall => 18,
            MaterialControlSize.Small => 12,
            _ => 8
        }
    };

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
            ApplyItemAppearance(items[i], i);
        UpdateTabStops();

        if (SelectedIndex >= ItemCount)
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
        item.Margin = default;

        var count = ItemCount;
        item.Position = count <= 1
            ? MaterialButtonGroupItemPosition.Only
            : index == 0
                ? MaterialButtonGroupItemPosition.First
                : index == count - 1
                    ? MaterialButtonGroupItemPosition.Last
                    : MaterialButtonGroupItemPosition.Middle;

        var compact = ControlSize is MaterialControlSize.ExtraSmall or MaterialControlSize.Small;
        item.ContainerInset = Kind != MaterialButtonGroupKind.Segmented && compact
            ? new Thickness(0, ControlSize == MaterialControlSize.ExtraSmall ? 8 : 4)
            : default;

        item.MinHeight = Kind switch
        {
            MaterialButtonGroupKind.Segmented => 40 + Density * 4,
            _ when compact => 48,
            _ => ControlSize switch
            {
                MaterialControlSize.Medium => 56,
                MaterialControlSize.Large => 96,
                _ => 136
            }
        };

        UpdateItemZIndex(item);

        SetCompressionLimit(item);
        ApplyItemCorners(item, animate: false);
    }

    // The segmented row draws the selected/pressed segment above its neighbours so the shared
    // 1-DIP outline and the rounded outer corners stay intact (source InteractionZIndex).
    private void UpdateItemZIndex(MaterialButtonGroupItem item)
    {
        if (Kind != MaterialButtonGroupKind.Segmented)
        {
            item.ZIndex = 0;
            return;
        }

        item.ZIndex = item.IsPressed ? 2 : item.IsChecked == true ? 1 : 0;
    }

    private void SetCompressionLimit(MaterialButtonGroupItem item)
    {
        var padding = item.Padding;
        var rtl = FlowDirection == FlowDirection.RightToLeft;
        item.CompressionLimit = Orientation == AvaloniaOrientation.Horizontal
            ? (rtl ? padding.Left : padding.Right)
            : (rtl ? padding.Top : padding.Bottom);
    }

    private double InnerRadius(MaterialControlSize size) => size switch
    {
        MaterialControlSize.ExtraSmall => 4,
        MaterialControlSize.Small => 8,
        MaterialControlSize.Medium => 12,
        MaterialControlSize.Large => 16,
        _ => 20
    };

    private double SquareRadius(MaterialControlSize size) => size switch
    {
        MaterialControlSize.ExtraSmall or MaterialControlSize.Small => 12,
        MaterialControlSize.Medium => 16,
        _ => 28
    };

    // ToggleButtonDefaults.shapesFor heights: pressed 8/8/12/16/16, checked 12/12/16/28/28.
    private double PressedRadius(MaterialControlSize size) => size switch
    {
        MaterialControlSize.ExtraSmall or MaterialControlSize.Small => 8,
        MaterialControlSize.Medium => 12,
        _ => 16
    };

    // Source ButtonGroupDefaults.connected* shapes and shapeByInteraction: pressed, then checked,
    // then resting. Targets stay logical CornerBasedShape terms; the physical corners are resolved
    // against the item's current painted bounds, so CornerFull follows a resize or a width morph.
    private (MaterialAssist.CornerShape Resting, MaterialAssist.CornerShape Pressed,
        MaterialAssist.CornerShape Checked) ShapeFor(MaterialButtonGroupItem item)
    {
        var inner = InnerRadius(ControlSize);
        var pressedInner = Math.Max(0, inner / 2);
        var square = SquareRadius(ControlSize);
        var full = MaterialAssist.CornerShape.Full;

        switch (Kind)
        {
            case MaterialButtonGroupKind.Segmented:
            {
                var corners = SegmentShape(item.Position, full, MaterialAssist.CornerShape.All(0));
                return (corners, corners, corners);
            }
            case MaterialButtonGroupKind.Connected:
            {
                var resting = SegmentShape(item.Position, full, MaterialAssist.CornerShape.All(inner));
                var pressed = SegmentShape(item.Position, full, MaterialAssist.CornerShape.All(pressedInner));
                return (resting, pressed, full);
            }
            default:
            {
                var resting = ButtonShape == MaterialButtonShape.Round
                    ? full
                    : MaterialAssist.CornerShape.All(square);
                return (resting, MaterialAssist.CornerShape.All(PressedRadius(ControlSize)),
                    MaterialAssist.CornerShape.All(square));
            }
        }
    }

    // Builds the logical start/end silhouette for a row position. Flow direction is applied once
    // by CornerShape.Resolve, so this stays independent of the reading direction.
    private MaterialAssist.CornerShape SegmentShape(MaterialButtonGroupItemPosition position,
        MaterialAssist.CornerShape outer, MaterialAssist.CornerShape value)
    {
        var vertical = Orientation == AvaloniaOrientation.Vertical;
        return position switch
        {
            MaterialButtonGroupItemPosition.Only => outer,
            MaterialButtonGroupItemPosition.First => vertical
                ? MaterialAssist.CornerShape.SplitRows(outer, value)
                : MaterialAssist.CornerShape.SplitEdges(outer, value),
            MaterialButtonGroupItemPosition.Last => vertical
                ? MaterialAssist.CornerShape.SplitRows(value, outer)
                : MaterialAssist.CornerShape.SplitEdges(value, outer),
            _ => value
        };
    }

    private void ApplyItemCorners(MaterialButtonGroupItem item, bool animate)
    {
        var (resting, pressed, checkedCorners) = ShapeFor(item);
        var target = item.IsChecked == true ? checkedCorners : resting;

        // shapeByInteraction precedence: pressed, then checked, then resting.
        if (item.IsPressed)
            target = pressed;

        if (!_shapes.TryGetValue(item, out var shape))
        {
            shape = new ItemShape();
            _shapes[item] = shape;
        }

        if (!animate
            || Kind == MaterialButtonGroupKind.Segmented
            || !shape.Initialized
            || !item.IsAttachedToVisualTree()
            || MaterialMotion.GetIsReduced(item))
        {
            StopItemShapeSpring(shape);
            shape.From = shape.Target = target;
            shape.Initialized = true;
            ApplyItemShapeProgress(item, shape, 1);
            return;
        }

        if (target.Equals(shape.Target))
            return;

        if (shape.Spring is null)
        {
            // At rest: start a new segment from the shape currently displayed.
            shape.From = shape.Target;
            shape.Target = target;
            StartItemShapeSpring(item, shape, 0, 0);
            return;
        }

        // AnimatedShapeState.animateToShape: retarget with the displayed position and velocity.
        var (position, velocity) = shape.Spring.SampleState();

        if (target.Equals(shape.From))
        {
            // The user reversed before the morph finished: flip progress and velocity.
            (shape.From, shape.Target) = (shape.Target, target);
            StartItemShapeSpring(item, shape, 1 - position, -velocity);
            return;
        }

        // A third target: freeze the currently visible interpolated shape and morph from zero.
        // Exact 0/1 boundaries avoid nesting blended corner terms.
        shape.From = position == 1
            ? shape.Target
            : position == 0
                ? shape.From
                : MaterialAssist.CornerShape.Lerp(shape.From, shape.Target, position);
        shape.Target = target;
        StartItemShapeSpring(item, shape, 0, 0);
    }

    private void StartItemShapeSpring(MaterialButtonGroupItem item, ItemShape shape, double from,
        double initialVelocity)
    {
        shape.Spring?.Dispose();
        // The source's shape spring is a single visibility-thresholded progress, not four corner
        // springs, and the group uses FastSpatial exactly like ButtonGroupDefaults.
        shape.Spring = MaterialMotion.Animate(item, from, 1,
            value => ApplyItemShapeProgress(item, shape, value),
            MaterialSpring.SpatialFast, ShapeProgressThreshold, initialVelocity);
        ApplyItemShapeProgress(item, shape, from);
    }

    private void StopItemShapeSpring(ItemShape shape)
    {
        shape.Spring?.Dispose();
        shape.Spring = null;
    }

    private void ApplyItemShapeProgress(MaterialButtonGroupItem item, ItemShape shape, double progress)
    {
        shape.Progress = progress;
        ResolveItemCorners(item, shape, progress, itemSize: null);
    }

    // CornerFull is half of the smaller side of the painted container, so the morph resolves
    // against the container inside ContainerInset, never the 48-DIP interaction target.
    private void ResolveItemCorners(MaterialButtonGroupItem item, ItemShape shape, double progress,
        Size? itemSize)
    {
        var size = ContainerSize(item, itemSize);
        var minDimension = Math.Min(size.Width, size.Height);
        if (minDimension <= 0)
            return;

        var rightToLeft = FlowDirection == FlowDirection.RightToLeft;
        item.SetCurrentValue(TemplatedControl.CornerRadiusProperty,
            shape.From.Resolve(shape.Target, progress, minDimension, rightToLeft));
    }

    private static Size ContainerSize(MaterialButtonGroupItem item, Size? itemSize)
    {
        var bounds = itemSize ?? item.Bounds.Size;
        var inset = item.ContainerInset;
        return new Size(
            Math.Max(0, bounds.Width - inset.Left - inset.Right),
            Math.Max(0, bounds.Height - inset.Top - inset.Bottom));
    }

    private void OnItemSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        // The silhouette is size-relative: a resize re-resolves the current morph progress without
        // starting a new spring segment.
        if (sender is MaterialButtonGroupItem item
            && _shapes.TryGetValue(item, out var shape)
            && shape.Initialized)
            ResolveItemCorners(item, shape, shape.Progress, e.NewSize);
    }

    private void ResolveAllItemCorners()
    {
        foreach (var (item, shape) in _shapes)
        {
            if (shape.Initialized)
                ResolveItemCorners(item, shape, shape.Progress, itemSize: null);
        }
    }

    private sealed class ItemShape
    {
        public MaterialAssist.CornerShape From;
        public MaterialAssist.CornerShape Target;
        public MaterialMotion.SpringAnimation? Spring;
        public double Progress = 1;
        public bool Initialized;
    }

    private void OnItemPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is not MaterialButtonGroupItem item)
            return;

        if (e.Property == ToggleButton.IsCheckedProperty)
        {
            UpdateItemZIndex(item);
            if (_syncing)
            {
                ApplyItemCorners(item, animate: true);
                return;
            }

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
                        ApplyItemCorners(other, animate: true);
                    }
                    break;

                case MaterialButtonGroupSelectionMode.Multiple when !isChecked:
                    // Deselecting reports the last remaining selection instead of the cleared item.
                    var remaining = GetGroupItems().LastOrDefault(static i => i.IsChecked == true);
                    ApplyItemCorners(item, animate: true);
                    UpdateSelection(remaining);
                    return;
            }

            ApplyItemCorners(item, animate: true);
            UpdateSelection(item);
        }
        else if (e.Property == Button.IsPressedProperty)
        {
            var pressed = item.IsPressed;
            UpdateItemZIndex(item);
            ApplyItemCorners(item, animate: true);
            if (Kind == MaterialButtonGroupKind.Standard && Orientation == AvaloniaOrientation.Horizontal)
                SetPressExpansion(item, pressed);
        }
        else if (e.Property == TemplatedControl.PaddingProperty)
        {
            SetCompressionLimit(item);
        }
    }

    private void SetPressExpansion(MaterialButtonGroupItem item, bool pressed)
    {
        if (!_expansions.TryGetValue(item, out var state))
        {
            state = new ItemExpansion();
            _expansions[item] = state;
        }

        if (MaterialMotion.GetIsReduced(this))
        {
            state.Spring?.Dispose();
            state.Spring = null;
            state.ReleasePending = false;
            item.PressExpansion = pressed ? 1 : 0;
            _panel?.InvalidateArrange();
            return;
        }

        if (pressed)
        {
            state.ReleasePending = false;
            if (state.Spring is { } running)
                running.Retarget(1);
            else
                state.Spring = MaterialMotion.Animate(this, item.PressExpansion, 1,
                    value => ApplyExpansion(item, state, value), MaterialSpring.SpatialFast, 0.001);
            return;
        }

        // The source waits until the expansion has visibly opened (75%) before retracting, so a
        // quick tap still reads as a press rather than a flicker.
        if (item.PressExpansion > 0.75 || state.Spring is null)
        {
            state.Spring?.Retarget(0);
            if (state.Spring is null)
                item.PressExpansion = 0;
            _panel?.InvalidateArrange();
        }
        else
        {
            state.ReleasePending = true;
        }
    }

    private void ApplyExpansion(MaterialButtonGroupItem item, ItemExpansion state, double value)
    {
        item.PressExpansion = value;
        _panel?.InvalidateArrange();
        if (state.ReleasePending && value >= 0.75)
        {
            state.ReleasePending = false;
            state.Spring?.Retarget(0);
        }
    }

    private void DisposeItemAnimations(MaterialButtonGroupItem item)
    {
        if (_expansions.Remove(item, out var state))
            state.Spring?.Dispose();
        // The logical shape survives a detach: the currently displayed morph is frozen into a
        // single shape, so a reattached item re-resolves its current bounds without a jump.
        if (_shapes.TryGetValue(item, out var shape))
        {
            StopItemShapeSpring(shape);
            shape.From = shape.Progress switch
            {
                >= 1 => shape.Target,
                <= 0 => shape.From,
                _ => MaterialAssist.CornerShape.Lerp(shape.From, shape.Target, shape.Progress)
            };
            shape.Target = shape.From;
            shape.Progress = 1;
        }
        item.PressExpansion = 0;
    }

    private void OnItemDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is MaterialButtonGroupItem item)
            DisposeItemAnimations(item);
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
            ApplyItemCorners(item, animate: true);
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
                item.IsTabStop = item.IsVisible;
            return;
        }

        var tabIndex = items.FindIndex(static i => i.IsChecked == true);
        if (tabIndex < 0)
            tabIndex = items.FindIndex(static i => i.IsEffectivelyEnabled);

        for (var i = 0; i < items.Count; i++)
            items[i].IsTabStop = i == tabIndex && items[i].IsVisible;
    }

    private void OnGroupKeyDown(object? sender, KeyEventArgs e)
    {
        if (Kind == MaterialButtonGroupKind.Standard || e.Handled || e.KeyModifiers != KeyModifiers.None)
            return;

        var items = GetGroupItems();
        var enabled = items.Where(static i => i.IsEffectivelyEnabled && i.IsVisible).ToList();
        if (enabled.Count == 0)
            return;

        var current = items.FindIndex(static i => i.IsFocused);
        var currentEnabled = current < 0 ? -1 : enabled.IndexOf(items[current]);
        var horizontal = Orientation == AvaloniaOrientation.Horizontal;
        var rightToLeft = FlowDirection == FlowDirection.RightToLeft;

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
            SelectSingle(next);
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

    // ---- Overflow -------------------------------------------------------------------------

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (_applyingOverflow)
            return;

        foreach (var item in GetGroupItems())
        {
            if (item.IsVisible && item.DesiredSize.Width > 0)
                _itemWidths[item] = item.DesiredSize.Width;
        }

        ApplyOverflowVisibility();
    }

    private void ApplyOverflowVisibility()
    {
        if (_overflowButton is null || Kind != MaterialButtonGroupKind.Standard ||
            Orientation != AvaloniaOrientation.Horizontal || !IsOverflowEnabled ||
            double.IsInfinity(_availableWidth) || _availableWidth <= 0)
        {
            if (_hiddenByOverflow.Count == 0 && (_overflowButton is null || !_overflowButton.IsVisible))
                return;

            _applyingOverflow = true;
            foreach (var hidden in _hiddenByOverflow.ToList())
            {
                hidden.IsVisible = true;
                hidden.IsTabStop = true;
            }
            _hiddenByOverflow.Clear();
            if (_overflowButton is not null)
                _overflowButton.IsVisible = false;
            _applyingOverflow = false;
            return;
        }

        var items = GetGroupItems();
        if (items.Count == 0)
        {
            _overflowButton.IsVisible = false;
            return;
        }

        var spacing = SpacingFor(Kind, ControlSize);
        var total = 0d;
        var unknown = false;
        foreach (var item in items)
        {
            if (item.IsVisible)
                _itemWidths[item] = item.DesiredSize.Width;
            if (!_itemWidths.TryGetValue(item, out var width) || width <= 0)
            {
                unknown = true;
                break;
            }
            total += width;
        }

        if (unknown)
        {
            // A new item arrived while others were collapsed: re-measure everything once so the
            // fit calculation sees real widths instead of trusting a stale zero.
            _applyingOverflow = true;
            foreach (var hidden in _hiddenByOverflow)
                hidden.IsVisible = true;
            _hiddenByOverflow.Clear();
            _applyingOverflow = false;
            _overflowButton.IsVisible = false;
            return;
        }

        total += spacing * (items.Count - 1);
        var available = _availableWidth;
        var needsOverflow = total > available + 0.5;

        var overflowWidth = Math.Max(48, _overflowButton.DesiredSize.Width) + OverflowGap;
        var fit = items.Count;
        if (needsOverflow)
        {
            var budget = Math.Max(0, available - overflowWidth);
            var used = 0d;
            fit = 0;
            for (var i = 0; i < items.Count; i++)
            {
                var width = _itemWidths[items[i]];
                var next = used + (fit > 0 ? spacing : 0) + width;
                if (next > budget && fit > 0)
                    break;
                used = next;
                fit++;
            }
        }

        var desiredHidden = needsOverflow ? items.Skip(fit).ToList() : [];
        if (_hiddenByOverflow.Count == desiredHidden.Count &&
            _hiddenByOverflow.Zip(desiredHidden).All(pair => ReferenceEquals(pair.First, pair.Second)) &&
            _overflowButton.IsVisible == needsOverflow)
        {
            return;
        }

        _applyingOverflow = true;
        foreach (var item in items)
        {
            var hide = desiredHidden.Contains(item);
            if (item.IsVisible == hide)
                item.IsVisible = !hide;
        }
        _hiddenByOverflow.Clear();
        _hiddenByOverflow.AddRange(desiredHidden);
        _overflowButton.IsVisible = needsOverflow;
        UpdateTabStops();
        _applyingOverflow = false;
    }

    private void OnOverflowButtonClick(object? sender, RoutedEventArgs e)
    {
        var hidden = _hiddenByOverflow.ToList();
        if (hidden.Count == 0)
            return;

        var flyout = new MaterialMenuFlyout();
        foreach (var item in hidden)
        {
            var label = ItemLabel(item);
            var menuItem = new MenuItem
            {
                Header = label,
                Icon = CreateMenuIcon(item),
                IsEnabled = item.IsEffectivelyEnabled
            };
            menuItem.Click += (_, _) =>
            {
                flyout.Hide();
                item.PerformClick();
            };
            flyout.Items.Add(menuItem);
        }

        flyout.ShowAt(_overflowButton!);
    }

    // A control can only have one visual parent, so reusing the item's live Icon in the overflow
    // MenuItem would steal it from the group. Clone the supported icon shapes instead.
    private static object? CreateMenuIcon(MaterialButtonGroupItem item) => item.Icon switch
    {
        MaterialIcon icon => new MaterialIcon
        {
            SourceId = icon.SourceId,
            IconName = icon.IconName,
            Stretch = icon.Stretch,
            IsDecorative = true,
            Width = icon.Width,
            Height = icon.Height,
        },
        PathIcon path => new PathIcon
        {
            Data = path.Data,
            Width = path.Width,
            Height = path.Height,
        },
        Image image => new Image
        {
            Source = image.Source,
            Stretch = image.Stretch,
            Width = image.Width,
            Height = image.Height,
        },
        Geometry geometry => new PathIcon { Data = geometry },
        _ => null,
    };

    private static string ItemLabel(MaterialButtonGroupItem item)
    {
        if (item.Content is string text && !string.IsNullOrWhiteSpace(text))
            return text;
        var name = AutomationProperties.GetName(item);
        return string.IsNullOrWhiteSpace(name) ? "Action" : name;
    }

    private sealed class ItemExpansion
    {
        public MaterialMotion.SpringAnimation? Spring;
        public bool ReleasePending;
    }

    private sealed class MaterialButtonGroupAutomationPeer(MaterialButtonGroup owner)
        : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
        protected override string GetClassNameCore() => nameof(MaterialButtonGroup);
    }
}