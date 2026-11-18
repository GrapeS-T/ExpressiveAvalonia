using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Presenters;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Tokens;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Controls;

/// <summary>The four Material chip families.</summary>
/// <remarks>
/// Assist and suggestion chips are action-only. Filter and input chips expose a selected state;
/// input chips additionally expose a removal affordance. The visual style axes in the source
/// export (Outlined/Elevated, icon/avatar configurations, trailing and closing icons) are mapped
/// onto <see cref="MaterialChip.Variant"/>, <see cref="MaterialChip.Icon"/>,
/// <see cref="MaterialChip.Avatar"/>, <see cref="MaterialChip.ShowIcon"/>,
/// <see cref="MaterialChip.ShowTrailingIcon"/> and <see cref="MaterialChip.IsRemovable"/>.
/// </remarks>
public enum MaterialChipKind { Assist, Filter, Input, Suggestion }

/// <summary>The two chip container treatments in the source export.</summary>
/// <remarks>Input chips have no style axis in the source; they always use the outlined treatment.</remarks>
public enum MaterialChipVariant { Outlined, Elevated }

/// <summary>A Material assist, filter, input or suggestion chip.</summary>
/// <remarks>
/// <para>
/// A chip is a real <see cref="Button"/>: it retains command execution, access keys, Space/Enter
/// activation, disabled propagation and click routing. Clicking or activating a filter or input
/// chip toggles <see cref="IsSelected"/> before <see cref="Button.Click"/> is raised, so commands
/// observe the new selection. Assist and suggestion chips never toggle selection.
/// </para>
/// <para>
/// Pressing Delete or Backspace on a removable input chip raises <see cref="RemoveRequestedEvent"/>;
/// <see cref="MaterialChipGroup"/> removes direct items and lets the event bubble for bound data.
/// </para>
/// <para>
/// <see cref="IsDraggable"/> drives the source "Dragged" visual state from a real pointer drag
/// threshold. It does not implement reordering or drag-and-drop; a group or application can react to
/// the state through the <c>:dragged</c> pseudo-class or <see cref="IsDragging"/>.
/// </para>
/// </remarks>
[TemplatePart("PART_LeadingIcon", typeof(ContentPresenter))]
[TemplatePart("PART_Avatar", typeof(ContentPresenter))]
[TemplatePart("PART_RemoveButton", typeof(Button))]
public class MaterialChip : Button
{
    public static readonly StyledProperty<MaterialChipKind> KindProperty =
        AvaloniaProperty.Register<MaterialChip, MaterialChipKind>(nameof(Kind));

    public static readonly StyledProperty<MaterialChipVariant> VariantProperty =
        AvaloniaProperty.Register<MaterialChip, MaterialChipVariant>(nameof(Variant));

    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<MaterialChip, bool>(nameof(IsSelected), defaultBindingMode: BindingMode.TwoWay,
            coerce: (owner, value) => ((MaterialChip)owner).CoerceSelection(value));

    /// <summary>Leading icon content for assist, filter, input and suggestion chips.</summary>
    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<MaterialChip, object?>(nameof(Icon));

    /// <summary>Input-chip avatar content, rendered in a 24 DIP circle.</summary>
    public static readonly StyledProperty<object?> AvatarProperty =
        AvaloniaProperty.Register<MaterialChip, object?>(nameof(Avatar));

    public static readonly StyledProperty<bool> ShowIconProperty =
        AvaloniaProperty.Register<MaterialChip, bool>(nameof(ShowIcon), true);

    /// <summary>Shows the filter-chip trailing dropdown caret.</summary>
    public static readonly StyledProperty<bool> ShowTrailingIconProperty =
        AvaloniaProperty.Register<MaterialChip, bool>(nameof(ShowTrailingIcon));

    /// <summary>Shows the input-chip closing icon.</summary>
    public static readonly StyledProperty<bool> IsRemovableProperty =
        AvaloniaProperty.Register<MaterialChip, bool>(nameof(IsRemovable));

    /// <summary>Accessible name of the closing icon. When unset, a localized remove label and chip name are used.</summary>
    public static readonly StyledProperty<string> RemoveButtonAccessibleNameProperty =
        AvaloniaProperty.Register<MaterialChip, string>(nameof(RemoveButtonAccessibleName), string.Empty);

    private static readonly StyledProperty<string?> RemoveLabelProperty =
        AvaloniaProperty.Register<MaterialChip, string?>("RemoveLabel");

    /// <summary>Enables the pointer drag threshold that produces the source "Dragged" state.</summary>
    public static readonly StyledProperty<bool> IsDraggableProperty =
        AvaloniaProperty.Register<MaterialChip, bool>(nameof(IsDraggable));

    public static readonly DirectProperty<MaterialChip, bool> IsDraggingProperty =
        AvaloniaProperty.RegisterDirect<MaterialChip, bool>(nameof(IsDragging), owner => owner.IsDragging);

    /// <summary>
    /// The leading icon content kept on screen while the leading slot collapses.
    /// </summary>
    /// <remarks>
    /// Mirrors the pinned <c>AnimatedChipContent</c> + <c>rememberRetainedState</c> pair: when
    /// <see cref="Icon"/> becomes null (or the icon stops being the active leading content) the
    /// outgoing icon keeps rendering through the shrink/fade instead of the host shrinking empty.
    /// </remarks>
    public static readonly StyledProperty<object?> LeadingSlotIconProperty =
        AvaloniaProperty.Register<MaterialChip, object?>(nameof(LeadingSlotIcon));

    /// <summary>
    /// The avatar content kept on screen while the leading slot collapses.
    /// </summary>
    /// <remarks>
    /// The avatar takes precedence over <see cref="Icon"/> exactly like the source
    /// <c>leadingContent</c> helper, so this property retains the last avatar while it fades out.
    /// </remarks>
    public static readonly StyledProperty<object?> LeadingSlotAvatarProperty =
        AvaloniaProperty.Register<MaterialChip, object?>(nameof(LeadingSlotAvatar));

    /// <summary>The animated width of the leading content slot, 0, 18 or 24 DIP.</summary>
    public static readonly StyledProperty<double> LeadingSlotWidthProperty =
        AvaloniaProperty.Register<MaterialChip, double>(nameof(LeadingSlotWidth));

    /// <summary>The animated margin of the leading content slot.</summary>
    public static readonly StyledProperty<Thickness> LeadingSlotMarginProperty =
        AvaloniaProperty.Register<MaterialChip, Thickness>(nameof(LeadingSlotMargin), new Thickness(0, -2, 0, -2));

    /// <summary>The animated opacity of the leading content slot.</summary>
    public static readonly StyledProperty<double> LeadingSlotOpacityProperty =
        AvaloniaProperty.Register<MaterialChip, double>(nameof(LeadingSlotOpacity));

    /// <summary>Whether the leading slot takes part in layout while it expands or shrinks.</summary>
    public static readonly StyledProperty<bool> IsLeadingSlotVisibleProperty =
        AvaloniaProperty.Register<MaterialChip, bool>(nameof(IsLeadingSlotVisible));

    /// <summary>The animated width of the trailing content slot, 0 or 18 DIP.</summary>
    public static readonly StyledProperty<double> TrailingSlotWidthProperty =
        AvaloniaProperty.Register<MaterialChip, double>(nameof(TrailingSlotWidth));

    /// <summary>The animated margin of the trailing content slot.</summary>
    public static readonly StyledProperty<Thickness> TrailingSlotMarginProperty =
        AvaloniaProperty.Register<MaterialChip, Thickness>(nameof(TrailingSlotMargin));

    /// <summary>The animated opacity of the trailing content slot.</summary>
    public static readonly StyledProperty<double> TrailingSlotOpacityProperty =
        AvaloniaProperty.Register<MaterialChip, double>(nameof(TrailingSlotOpacity));

    /// <summary>Whether the trailing slot takes part in layout while it expands or shrinks.</summary>
    public static readonly StyledProperty<bool> IsTrailingSlotVisibleProperty =
        AvaloniaProperty.Register<MaterialChip, bool>(nameof(IsTrailingSlotVisible));

    /// <summary>Raised when the user activates the closing icon or presses Delete/Backspace.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> RemoveRequestedEvent =
        RoutedEvent.Register<MaterialChip, RoutedEventArgs>(nameof(RemoveRequested), RoutingStrategies.Bubble);

    private const double DragThreshold = 4;
    private const double IconSlotWidth = 18;
    private const double AvatarSlotWidth = 24;
    private const double CaretSlotWidth = IconSlotWidth;

    // ChipDefaults.shapes() at the pinned commit: ChipsTokens.UnselectedShape = CornerMedium
    // (12 DIP), PressedShape = CornerSmall (8 DIP) and SelectedShape = CornerFull. CornerFull is
    // CircleShape, i.e. a size-relative 50 percent corner, not a 9999 DIP radius. Assist and
    // suggestion chips are not selectable and keep the static 8 DIP container of the anatomy.
    private const double UnselectedCornerRadius = 12;
    private const double PressedCornerRadius = 8;
    private const double StaticCornerRadius = 8;
    private const double ShapeProgressThreshold = 0.01;

    private Button? _removeButton;
    private Panel? _visual;
    private bool _pointerDown;
    private Point _pointerOrigin;
    private bool _isDragging;
    private bool _suppressClick;
    private MaterialChipAutomationPeer? _peer;
    private MaterialMotion.SpringAnimation? _shapeSpring;
    private MaterialAssist.CornerShape _shapeFrom;
    private MaterialAssist.CornerShape _shapeTarget;
    private double _shapeProgress = 1;
    private bool _shapeInitialized;
    private MaterialElevationAnimation? _elevation;
    private MaterialMotion.SpringAnimation? _leadingWidthSpring;
    private MaterialMotion.SpringAnimation? _leadingOpacitySpring;
    private MaterialMotion.SpringAnimation? _trailingWidthSpring;
    private MaterialMotion.SpringAnimation? _trailingOpacitySpring;
    private double _leadingFromWidth;
    private double _leadingWidthTarget;
    private Thickness _leadingFromMargin;
    private Thickness _leadingMarginTarget;
    private double _trailingFromWidth;
    private double _trailingWidthTarget;
    private Thickness _trailingFromMargin;
    private Thickness _trailingMarginTarget;
    private bool _slotsReady;

    public MaterialChip()
    {
        this.Bind(RemoveLabelProperty, this.GetResourceObservable("MaterialChipRemoveText"));
        // Source ChipElevation/SelectableChipElevation: the latest active interaction selects the
        // level and internal/Elevation.kt owns the 120/150 ms ramps. IsDragging is the source
        // DragInteraction.Start state, polled by the shared helper when this chip refreshes.
        _elevation = new MaterialElevationAnimation(
            this, ResolveElevationSpecs, () => IsDragging);
        UpdateFocusRingCornerRadius();
        UpdateChipPseudoClasses();
        AddHandler(PointerPressedEvent, OnDragPointerPressed, RoutingStrategies.Tunnel);
        AddHandler(PointerMovedEvent, OnDragPointerMoved, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, OnDragPointerReleased, RoutingStrategies.Tunnel);
        AddHandler(PointerCaptureLostEvent, OnDragPointerCaptureLost, RoutingStrategies.Tunnel);
    }

    public MaterialChipKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public MaterialChipVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
    public bool IsSelected { get => GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }
    public object? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public object? Avatar { get => GetValue(AvatarProperty); set => SetValue(AvatarProperty, value); }
    public bool ShowIcon { get => GetValue(ShowIconProperty); set => SetValue(ShowIconProperty, value); }
    public bool ShowTrailingIcon { get => GetValue(ShowTrailingIconProperty); set => SetValue(ShowTrailingIconProperty, value); }
    public bool IsRemovable { get => GetValue(IsRemovableProperty); set => SetValue(IsRemovableProperty, value); }
    public string RemoveButtonAccessibleName { get => GetValue(RemoveButtonAccessibleNameProperty); set => SetValue(RemoveButtonAccessibleNameProperty, value); }
    public bool IsDraggable { get => GetValue(IsDraggableProperty); set => SetValue(IsDraggableProperty, value); }
    public object? LeadingSlotIcon { get => GetValue(LeadingSlotIconProperty); private set => SetValue(LeadingSlotIconProperty, value); }
    public object? LeadingSlotAvatar { get => GetValue(LeadingSlotAvatarProperty); private set => SetValue(LeadingSlotAvatarProperty, value); }
    public double LeadingSlotWidth { get => GetValue(LeadingSlotWidthProperty); private set => SetValue(LeadingSlotWidthProperty, value); }
    public Thickness LeadingSlotMargin { get => GetValue(LeadingSlotMarginProperty); private set => SetValue(LeadingSlotMarginProperty, value); }
    public double LeadingSlotOpacity { get => GetValue(LeadingSlotOpacityProperty); private set => SetValue(LeadingSlotOpacityProperty, value); }
    public bool IsLeadingSlotVisible { get => GetValue(IsLeadingSlotVisibleProperty); private set => SetValue(IsLeadingSlotVisibleProperty, value); }
    public double TrailingSlotWidth { get => GetValue(TrailingSlotWidthProperty); private set => SetValue(TrailingSlotWidthProperty, value); }
    public Thickness TrailingSlotMargin { get => GetValue(TrailingSlotMarginProperty); private set => SetValue(TrailingSlotMarginProperty, value); }
    public double TrailingSlotOpacity { get => GetValue(TrailingSlotOpacityProperty); private set => SetValue(TrailingSlotOpacityProperty, value); }
    public bool IsTrailingSlotVisible { get => GetValue(IsTrailingSlotVisibleProperty); private set => SetValue(IsTrailingSlotVisibleProperty, value); }

    /// <summary>True while a drag gesture has crossed the pointer threshold.</summary>
    public bool IsDragging
    {
        get => _isDragging;
        private set => SetAndRaise(IsDraggingProperty, ref _isDragging, value);
    }

    /// <summary>Whether this chip family exposes selection.</summary>
    public bool IsSelectable =>
        Kind is MaterialChipKind.Filter or MaterialChipKind.Input;

    private MaterialChipGroup? OwningGroup =>
        Parent as MaterialChipGroup ?? this.FindAncestorOfType<MaterialChipGroup>();

    internal bool EffectiveIsSelectable =>
        IsSelectable && OwningGroup?.SelectionMode != MaterialChipSelectionMode.None;

    /// <summary>Whether the closing icon is actually shown for the current family.</summary>
    public bool CanRemove => IsRemovable && Kind == MaterialChipKind.Input;

    public event EventHandler<RoutedEventArgs>? RemoveRequested
    {
        add => AddHandler(RemoveRequestedEvent, value);
        remove => RemoveHandler(RemoveRequestedEvent, value);
    }

    private bool CoerceSelection(bool value) =>
        Kind is MaterialChipKind.Assist or MaterialChipKind.Suggestion ? false : value;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == KindProperty)
        {
            CoerceValue(IsSelectedProperty);
            UpdateChipPseudoClasses();
            UpdateShape(animate: true);
            UpdateSlots(animate: true);
            // A new chip family selects a different elevation set; with no active interaction the
            // source's animateElevation falls into its snap branch.
            _elevation?.Refresh(snap: false);
        }
        else if (change.Property == VariantProperty || change.Property == IconProperty ||
                 change.Property == AvatarProperty || change.Property == ShowIconProperty ||
                 change.Property == ShowTrailingIconProperty || change.Property == IsRemovableProperty)
        {
            UpdateChipPseudoClasses();
            UpdateSlots(animate: true);
            if (change.Property == AvatarProperty)
                UpdateShape(animate: true);
            if (change.Property == VariantProperty)
                _elevation?.Refresh(snap: false);
        }
        else if (change.Property == IsSelectedProperty)
        {
            PseudoClasses.Set(":selected", IsSelected);
            _peer?.NotifySelectionChanged((bool)(change.OldValue ?? false), IsSelected);
            UpdateShape(animate: true);
            UpdateSlots(animate: true);
            _elevation?.Refresh(snap: false);
        }
        else if (change.Property == IsPressedProperty)
        {
            UpdateShape(animate: true);
        }
        else if (change.Property == CornerRadiusProperty)
        {
            UpdateFocusRingCornerRadius();
        }
        else if (change.Property == Visual.FlowDirectionProperty)
        {
            // Corners are stored logically; only the physical mapping changes, so the current
            // morph progress is re-resolved without a new spring segment.
            if (_shapeInitialized)
                ApplyShape(_shapeProgress);
        }
        else if (change.Property == RemoveButtonAccessibleNameProperty || change.Property == RemoveLabelProperty ||
                 change.Property == ContentProperty || change.Property == AutomationProperties.NameProperty)
        {
            UpdateRemoveButtonName();
        }
    }

    private void UpdateFocusRingCornerRadius()
    {
        var r = CornerRadius;
        SetCurrentValue(MaterialAssist.FocusRingCornerRadiusProperty,
            new CornerRadius(r.TopLeft + 5, r.TopRight + 5, r.BottomRight + 5, r.BottomLeft + 5));
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateShape(animate: false);
        UpdateSlots(animate: false);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        EndDrag();
        DisposeAnimations();
        PseudoClasses.Set(":remove-pointerover", false);
        base.OnDetachedFromVisualTree(e);
    }

    private void UpdateChipPseudoClasses()
    {
        PseudoClasses.Set(":assist", Kind == MaterialChipKind.Assist);
        PseudoClasses.Set(":filter", Kind == MaterialChipKind.Filter);
        PseudoClasses.Set(":input", Kind == MaterialChipKind.Input);
        PseudoClasses.Set(":suggestion", Kind == MaterialChipKind.Suggestion);
        // Input chips have no Elevated variant in the source export.
        var elevated = Kind != MaterialChipKind.Input && Variant == MaterialChipVariant.Elevated;
        PseudoClasses.Set(":elevated", elevated);
        PseudoClasses.Set(":outlined", !elevated);
        var hasAvatar = Kind == MaterialChipKind.Input && Avatar is not null;
        PseudoClasses.Set(":has-avatar", hasAvatar);
        PseudoClasses.Set(":has-icon", !hasAvatar && ShowIcon && Icon is not null);
        PseudoClasses.Set(":trailing", Kind == MaterialChipKind.Filter && ShowTrailingIcon);
        PseudoClasses.Set(":removable", CanRemove);
        PseudoClasses.Set(":selected", IsSelected);
    }

    // Source Chip.kt shapeByInteraction + internal/AnimatedShape.kt: one progress Animatable morphs
    // between ChipShapes, pressed takes precedence over selected, and FastSpatial drives it. The
    // corners stay logical CornerBasedShape terms, so the selected CornerFull remains a 50 percent
    // corner of the visual container instead of a numeric 9999 DIP radius.
    private void UpdateShape(bool animate)
    {
        var staticShape = Kind is MaterialChipKind.Assist or MaterialChipKind.Suggestion;
        var target = staticShape
            ? MaterialAssist.CornerShape.All(StaticCornerRadius)
            : IsPressed
                ? MaterialAssist.CornerShape.All(PressedCornerRadius)
                : IsSelected || (Kind == MaterialChipKind.Input && Avatar is not null)
                    ? MaterialAssist.CornerShape.Full
                    : MaterialAssist.CornerShape.All(UnselectedCornerRadius);

        if (!animate
            || staticShape
            || !_shapeInitialized
            || !this.IsAttachedToVisualTree()
            || MaterialMotion.GetIsReduced(this))
        {
            StopShapeSpring();
            _shapeFrom = _shapeTarget = target;
            _shapeProgress = 1;
            _shapeInitialized = true;
            ApplyShape(1);
            return;
        }

        if (target.Equals(_shapeTarget))
            return;

        if (_shapeSpring is null)
        {
            // At rest: start a new segment from the shape currently displayed.
            _shapeFrom = _shapeTarget;
            _shapeTarget = target;
            StartShapeSpring(0, 0);
            return;
        }

        // Position and velocity come from one instant; the shape that gets frozen must be the
        // displayed progress that the velocity belongs to.
        var (position, velocity) = _shapeSpring.SampleState();

        if (target.Equals(_shapeFrom))
        {
            // The user reversed before the morph finished: flip progress and velocity, as
            // AnimatedShapeState.animateToShape does.
            (_shapeFrom, _shapeTarget) = (_shapeTarget, target);
            StartShapeSpring(1 - position, -velocity);
            return;
        }

        // A third target: freeze the currently visible interpolated shape and morph from zero.
        // Exact 0/1 boundaries avoid nesting blended corner terms.
        _shapeFrom = position == 1
            ? _shapeTarget
            : position == 0
                ? _shapeFrom
                : MaterialAssist.CornerShape.Lerp(_shapeFrom, _shapeTarget, position);
        _shapeTarget = target;
        StartShapeSpring(0, 0);
    }

    private void StartShapeSpring(double from, double initialVelocity)
    {
        _shapeSpring?.Dispose();
        // Chip.kt shapeByInteraction passes MotionSchemeKeyTokens.FastSpatial; the threshold
        // applies to the single morph progress, not to each resolved corner.
        _shapeSpring = MaterialMotion.Animate(this, from, 1, ApplyShape,
            MaterialSpring.SpatialFast, ShapeProgressThreshold, initialVelocity);
        ApplyShape(from);
    }

    private void ApplyShape(double progress) =>
        ApplyShape(progress, _visual?.Bounds.Size ?? Bounds.Size);

    private void ApplyShape(double progress, Size size)
    {
        _shapeProgress = progress;

        // CornerFull is half of the smaller side, so the silhouette must be resolved against the
        // visual container that paints Surface/Outline/state layer, not the 48 DIP interaction
        // target reserved around it. A replacement template without that panel falls back to the
        // control bounds so the morph keeps working. Before the first arrange there is no size
        // to resolve yet.
        var minDimension = Math.Min(size.Width, size.Height);
        if (minDimension <= 0)
            return;

        var rightToLeft = FlowDirection == Avalonia.Media.FlowDirection.RightToLeft;
        SetCurrentValue(CornerRadiusProperty,
            _shapeFrom.Resolve(_shapeTarget, progress, minDimension, rightToLeft));
    }

    private void StopShapeSpring()
    {
        _shapeSpring?.Dispose();
        _shapeSpring = null;
    }

    private void OnVisualSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        // The silhouette is size-relative, so a resize re-resolves the current morph progress
        // without starting a new spring segment.
        if (_shapeInitialized)
            ApplyShape(_shapeProgress, e.NewSize);
    }

    // Source ChipElevation/SelectableChipElevation defaults at the pinned commit. Elevated chips
    // rest at Level1 and rise to Level2 on hover; flat chips stay at Level0 except a selected
    // filter chip's hovered elevation (Level1) and every family's DraggedContainerElevation (Level4).
    // Disabled elevation is Level0 for all variants. Input chips have no elevated variant.
    private MaterialElevationSpecs ResolveElevationSpecs()
    {
        if (Kind != MaterialChipKind.Input && Variant == MaterialChipVariant.Elevated)
            return new MaterialElevationSpecs(
                Default: 1, Pressed: 1, Focused: 1, Hovered: 2, Dragged: 4, Disabled: 0);
        if (Kind == MaterialChipKind.Filter && IsSelected)
            return new MaterialElevationSpecs(
                Default: 0, Pressed: 0, Focused: 0, Hovered: 1, Dragged: 4, Disabled: 0);
        return new MaterialElevationSpecs(
            Default: 0, Pressed: 0, Focused: 0, Hovered: 0, Dragged: 4, Disabled: 0);
    }

    // AnimatingChipContent: the leading and trailing slots expand with FastSpatial and fade with
    // DefaultEffects; a hidden slot leaves the layout only once it has fully collapsed, so the
    // label never jumps. Source details kept here:
    // - rememberRetainedState(targetValue = leadingContent(...)): the outgoing avatar/icon stays
    //   rendered (and keeps its last non-null content) while AnimatedVisibility shrinks/fades it.
    // - avatar takes precedence over the leading icon; the retained value updates immediately
    //   whenever the new leading content is non-null, so a content swap is not a fade sequence.
    // - the same applies to the trailing caret and the input chip's closing icon; the retained
    //   closing button is painted but must not be interactive while it leaves.
    private void UpdateSlots(bool animate)
    {
        var isInput = Kind == MaterialChipKind.Input;
        var hasAvatar = isInput && Avatar is not null;
        var hasCheck = Kind == MaterialChipKind.Filter && IsSelected;
        var hasIcon = !hasAvatar && ShowIcon && Icon is not null;
        var leading = hasAvatar ? AvatarSlotWidth : hasIcon || hasCheck ? IconSlotWidth : 0d;
        var leadingMargin = hasAvatar
            ? new Thickness(-8, -2, 8, -2)
            : hasIcon || hasCheck
                ? new Thickness(isInput ? -4 : -8, -2, 8, -2)
                : new Thickness(0, -2, 0, -2);

        var trailing = CanRemove ? IconSlotWidth
            : Kind == MaterialChipKind.Filter && ShowTrailingIcon ? CaretSlotWidth : 0d;
        var trailingMargin = CanRemove
            ? new Thickness(8, 0, -4, 0)
            : Kind == MaterialChipKind.Filter && ShowTrailingIcon
                ? new Thickness(8, 0, -8, 0)
                : default;

        var canAnimate = animate && _slotsReady && this.IsAttachedToVisualTree() &&
                         !MaterialMotion.GetIsReduced(this);

        if (hasAvatar)
        {
            // Avatar wins over the icon in the source leadingContent helper.
            SetCurrentValue(LeadingSlotAvatarProperty, Avatar);
            SetSlotClasses(LeadingClasses, ":leading-avatar");
        }
        else if (hasIcon)
        {
            SetCurrentValue(LeadingSlotIconProperty, Icon);
            SetSlotClasses(LeadingClasses, ":leading-icon");
        }
        else if (hasCheck)
        {
            SetSlotClasses(LeadingClasses, ":leading-check");
        }
        // A null target keeps the previous leading classes/content until the slot has collapsed.

        if (CanRemove)
            SetSlotClasses(TrailingClasses, ":trailing-remove");
        else if (Kind == MaterialChipKind.Filter && ShowTrailingIcon)
            SetSlotClasses(TrailingClasses, ":trailing-caret");
        // Likewise retain the outgoing caret/closing icon during the exit.

        // The retained closing button must not run a second removal while it fades out.
        if (_removeButton is not null)
            _removeButton.IsHitTestVisible = CanRemove;
        UpdateRemoveButtonPointerOver();

        if (leading > 0)
            SetCurrentValue(IsLeadingSlotVisibleProperty, true);
        if (trailing > 0)
            SetCurrentValue(IsTrailingSlotVisibleProperty, true);

        AnimateSlot(ref _leadingWidthSpring, leading, leadingMargin, canAnimate, isLeading: true);
        AnimateSlot(ref _trailingWidthSpring, trailing, trailingMargin, canAnimate, isLeading: false);

        var leadingOpacity = leading > 0 ? 1d : 0d;
        var trailingOpacity = trailing > 0 ? 1d : 0d;
        AnimateOpacity(ref _leadingOpacitySpring, LeadingSlotOpacityProperty, leadingOpacity, canAnimate);
        AnimateOpacity(ref _trailingOpacitySpring, TrailingSlotOpacityProperty, trailingOpacity, canAnimate);

        _slotsReady = true;
    }

    private static readonly string[] LeadingClasses =
        [":leading-avatar", ":leading-icon", ":leading-check"];

    private static readonly string[] TrailingClasses =
        [":trailing-caret", ":trailing-remove"];

    private void SetSlotClasses(string[] all, string active)
    {
        foreach (var name in all)
            PseudoClasses.Set(name, name == active);
    }

    private void ClearSlotClasses(string[] all)
    {
        foreach (var name in all)
            PseudoClasses.Set(name, false);
    }

    private void AnimateSlot(ref MaterialMotion.SpringAnimation? spring, double targetWidth,
        Thickness targetMargin, bool animate, bool isLeading)
    {
        var widthProperty = isLeading ? LeadingSlotWidthProperty : TrailingSlotWidthProperty;
        var marginProperty = isLeading ? LeadingSlotMarginProperty : TrailingSlotMarginProperty;

        if (!animate)
        {
            DisposeSpring(ref spring);
            if (isLeading)
            {
                _leadingFromWidth = _leadingWidthTarget = targetWidth;
                _leadingFromMargin = _leadingMarginTarget = targetMargin;
            }
            else
            {
                _trailingFromWidth = _trailingWidthTarget = targetWidth;
                _trailingFromMargin = _trailingMarginTarget = targetMargin;
            }

            SetCurrentValue(widthProperty, targetWidth);
            SetCurrentValue(marginProperty, targetMargin);
            if (targetWidth <= 0)
            {
                SetCurrentValue(isLeading ? IsLeadingSlotVisibleProperty : IsTrailingSlotVisibleProperty, false);
                ClearSlotClasses(isLeading ? LeadingClasses : TrailingClasses);
            }
            return;
        }

        var prevTargetWidth = isLeading ? _leadingWidthTarget : _trailingWidthTarget;
        var prevTargetMargin = isLeading ? _leadingMarginTarget : _trailingMarginTarget;
        if (Math.Abs(prevTargetWidth - targetWidth) < 0.001 && prevTargetMargin == targetMargin)
            return;

        var currentWidth = GetValue(widthProperty);
        var currentMargin = GetValue(marginProperty);
        var initialVelocity = 0d;
        if (spring is { } running)
        {
            var (_, velocity) = running.SampleState();
            var prevDelta = prevTargetWidth - (isLeading ? _leadingFromWidth : _trailingFromWidth);
            var nextDelta = targetWidth - currentWidth;
            if (Math.Abs(nextDelta) > 0.1)
                initialVelocity = (prevDelta * velocity) / nextDelta;
            DisposeSpring(ref spring);
        }

        if (isLeading)
        {
            _leadingFromWidth = currentWidth;
            _leadingWidthTarget = targetWidth;
            _leadingFromMargin = currentMargin;
            _leadingMarginTarget = targetMargin;
        }
        else
        {
            _trailingFromWidth = currentWidth;
            _trailingWidthTarget = targetWidth;
            _trailingFromMargin = currentMargin;
            _trailingMarginTarget = targetMargin;
        }

        if (Math.Abs(currentWidth - targetWidth) < 0.001 && currentMargin == targetMargin)
        {
            ApplySlotProgress(1, isLeading);
            return;
        }

        spring = MaterialMotion.Animate(this, 0, 1,
            progress => ApplySlotProgress(progress, isLeading),
            MaterialSpring.SpatialFast, 0.005, initialVelocity);
    }

    private void ApplySlotProgress(double progress, bool isLeading)
    {
        var fromWidth = isLeading ? _leadingFromWidth : _trailingFromWidth;
        var targetWidth = isLeading ? _leadingWidthTarget : _trailingWidthTarget;
        var fromMargin = isLeading ? _leadingFromMargin : _trailingFromMargin;
        var targetMargin = isLeading ? _leadingMarginTarget : _trailingMarginTarget;
        var widthProperty = isLeading ? LeadingSlotWidthProperty : TrailingSlotWidthProperty;
        var marginProperty = isLeading ? LeadingSlotMarginProperty : TrailingSlotMarginProperty;

        var width = Math.Max(0, fromWidth + (targetWidth - fromWidth) * progress);
        if (targetWidth <= 0 && (width <= 0.05 || progress >= 1))
        {
            SetCurrentValue(widthProperty, 0d);
            SetCurrentValue(marginProperty, isLeading ? new Thickness(0, -2, 0, -2) : default);
            SetCurrentValue(isLeading ? IsLeadingSlotVisibleProperty : IsTrailingSlotVisibleProperty, false);
            ClearSlotClasses(isLeading ? LeadingClasses : TrailingClasses);
            return;
        }

        SetCurrentValue(widthProperty, width);
        SetCurrentValue(marginProperty, LerpThickness(fromMargin, targetMargin, progress));
    }

    private static Thickness LerpThickness(Thickness from, Thickness to, double progress) => new(
        from.Left + (to.Left - from.Left) * progress,
        from.Top + (to.Top - from.Top) * progress,
        from.Right + (to.Right - from.Right) * progress,
        from.Bottom + (to.Bottom - from.Bottom) * progress);

    private void AnimateOpacity(ref MaterialMotion.SpringAnimation? spring,
        StyledProperty<double> property, double target, bool animate)
    {
        if (!animate)
        {
            DisposeSpring(ref spring);
            SetCurrentValue(property, target);
            return;
        }

        if (spring is { } running)
        {
            running.Retarget(target);
            return;
        }

        if (Math.Abs(GetValue(property) - target) < 0.001)
            return;

        spring = MaterialMotion.Animate(this, GetValue(property), target,
            value => SetCurrentValue(property, value), MaterialSpring.EffectsDefault, 0.001);
    }

    private static void DisposeSpring(ref MaterialMotion.SpringAnimation? spring)
    {
        spring?.Dispose();
        spring = null;
    }

    private void DisposeAnimations()
    {
        DisposeSpring(ref _leadingWidthSpring);
        DisposeSpring(ref _leadingOpacitySpring);
        DisposeSpring(ref _trailingWidthSpring);
        DisposeSpring(ref _trailingOpacitySpring);
        StopShapeSpring();
        _shapeInitialized = false;
        _slotsReady = false;
    }

    protected override void OnClick()
    {
        // A completed drag is not an activation.
        if (_suppressClick)
        {
            _suppressClick = false;
            return;
        }
        if (EffectiveIsSelectable)
            SetCurrentValue(IsSelectedProperty, !IsSelected);
        base.OnClick();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (CanRemove && e.KeyModifiers == KeyModifiers.None && e.Key is Key.Delete or Key.Back)
        {
            RequestRemove();
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_removeButton is not null)
        {
            _removeButton.Click -= OnRemoveButtonClick;
            _removeButton.PropertyChanged -= OnRemoveButtonPropertyChanged;
        }
        base.OnApplyTemplate(e);
        _removeButton = e.NameScope.Find<Button>("PART_RemoveButton");
        if (_removeButton is not null)
        {
            _removeButton.Click += OnRemoveButtonClick;
            _removeButton.PropertyChanged += OnRemoveButtonPropertyChanged;
            UpdateRemoveButtonName();
        }
        UpdateRemoveButtonPointerOver();

        // The visual container paints the silhouette; its bounds, not the 48 DIP interaction
        // target reserved by the control, decide what CornerFull resolves to.
        if (_visual is not null)
            _visual.SizeChanged -= OnVisualSizeChanged;
        _visual = e.NameScope.Find<Panel>("Visual");
        if (_visual is not null)
            _visual.SizeChanged += OnVisualSizeChanged;

        UpdateShape(animate: false);
        UpdateSlots(animate: false);
    }

    private void OnRemoveButtonPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == IsPointerOverProperty ||
            e.Property == IsVisibleProperty ||
            e.Property == IsHitTestVisibleProperty)
        {
            UpdateRemoveButtonPointerOver();
        }
    }

    private void UpdateRemoveButtonPointerOver()
    {
        var over = _removeButton is { IsPointerOver: true, IsVisible: true, IsHitTestVisible: true } && CanRemove;
        PseudoClasses.Set(":remove-pointerover", over);
    }

    private void OnRemoveButtonClick(object? sender, RoutedEventArgs e)
    {
        RequestRemove();
        e.Handled = true;
    }

    private void UpdateRemoveButtonName()
    {
        if (_removeButton is not null)
        {
            var name = RemoveButtonAccessibleName;
            if (!IsSet(RemoveButtonAccessibleNameProperty))
            {
                name = GetValue(RemoveLabelProperty) ?? "Remove";
                var label = AutomationProperties.GetName(this) ?? Content as string;
                if (!string.IsNullOrWhiteSpace(label))
                    name = $"{name} {label}";
            }
            AutomationProperties.SetName(_removeButton, name);
        }
    }

    /// <summary>Raises <see cref="RemoveRequestedEvent"/> for this chip.</summary>
    public void RequestRemove()
    {
        if (!CanRemove)
            return;
        RaiseEvent(new RoutedEventArgs(RemoveRequestedEvent, this));
    }

    private void OnDragPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _suppressClick = false;
        if (!IsDraggable || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        if (_removeButton is not null && e.Source is Visual source &&
            (ReferenceEquals(_removeButton, source) || _removeButton.IsVisualAncestorOf(source)))
            return;
        _pointerDown = true;
        _pointerOrigin = e.GetPosition(this);
    }

    private void OnDragPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_pointerDown || IsDragging || !IsDraggable)
            return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            EndDrag();
            return;
        }
        var position = e.GetPosition(this);
        var dx = position.X - _pointerOrigin.X;
        var dy = position.Y - _pointerOrigin.Y;
        if (dx * dx + dy * dy >= DragThreshold * DragThreshold)
        {
            IsDragging = true;
            PseudoClasses.Set(":dragged", true);
            // DragInteraction.Start: incoming 120 ms ramp to the DraggedContainerElevation.
            _elevation?.Refresh(snap: false);
        }
    }

    private void OnDragPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (IsDragging)
            _suppressClick = true;
        EndDrag();
    }

    private void OnDragPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e) => EndDrag();

    private void EndDrag()
    {
        _pointerDown = false;
        if (!IsDragging)
            return;
        IsDragging = false;
        PseudoClasses.Set(":dragged", false);
        // DragInteraction.Stop/Cancel: outgoing 150 ms ramp; hover may remain active.
        _elevation?.Refresh(snap: false);
    }

    protected override AutomationPeer OnCreateAutomationPeer() =>
        _peer = new MaterialChipAutomationPeer(this);

    private sealed class MaterialChipAutomationPeer : ButtonAutomationPeer, ISelectionItemProvider, IToggleProvider
    {
        private readonly MaterialChip _chip;

        public MaterialChipAutomationPeer(MaterialChip owner) : base(owner) => _chip = owner;

        // The source sets role = Role.Checkbox for selectable chips; action chips stay buttons.
        protected override AutomationControlType GetAutomationControlTypeCore() =>
            _chip.EffectiveIsSelectable ? AutomationControlType.CheckBox : AutomationControlType.Button;

        protected override string GetClassNameCore() => nameof(MaterialChip);

        protected override string? GetNameCore()
        {
            var name = AutomationProperties.GetName(_chip);
            if (!string.IsNullOrWhiteSpace(name))
                return name;
            return _chip.Content as string ?? base.GetNameCore();
        }

        protected override object? GetProviderCore(Type providerType) =>
            !_chip.EffectiveIsSelectable &&
            (providerType == typeof(IToggleProvider) || providerType == typeof(ISelectionItemProvider))
                ? null
                : base.GetProviderCore(providerType);

        public bool IsSelected => _chip.IsSelected;

        public ISelectionProvider SelectionContainer
        {
            get
            {
                var group = _chip.OwningGroup;
                if (group is not null)
                    return (ControlAutomationPeer.CreatePeerForElement(group) as ISelectionProvider)!;
                return EmptySelectionProvider.Instance;
            }
        }

        public void AddToSelection()
        {
            if (!_chip.IsEffectivelyEnabled)
                throw new InvalidOperationException("The chip is disabled.");
            if (!_chip.EffectiveIsSelectable)
                throw new InvalidOperationException("The chip is not selectable.");
            _chip.SetCurrentValue(MaterialChip.IsSelectedProperty, true);
        }

        public void RemoveFromSelection()
        {
            if (!_chip.IsEffectivelyEnabled)
                throw new InvalidOperationException("The chip is disabled.");
            if (!_chip.EffectiveIsSelectable)
                throw new InvalidOperationException("The chip is not selectable.");
            _chip.SetCurrentValue(MaterialChip.IsSelectedProperty, false);
        }

        public void Select()
        {
            if (!_chip.IsEffectivelyEnabled)
                throw new InvalidOperationException("The chip is disabled.");
            if (!_chip.EffectiveIsSelectable)
                throw new InvalidOperationException("The chip is not selectable.");
            _chip.SetCurrentValue(MaterialChip.IsSelectedProperty, true);
        }

        public ToggleState ToggleState => _chip.IsSelected ? ToggleState.On : ToggleState.Off;

        public void Toggle()
        {
            if (!_chip.IsEffectivelyEnabled)
                throw new InvalidOperationException("The chip is disabled.");
            if (!_chip.EffectiveIsSelectable)
                throw new InvalidOperationException("The chip is not selectable.");
            _chip.SetCurrentValue(MaterialChip.IsSelectedProperty, !_chip.IsSelected);
        }

        public void NotifySelectionChanged(bool oldValue, bool newValue)
        {
            if (!_chip.EffectiveIsSelectable)
                return;
            RaisePropertyChangedEvent(SelectionItemPatternIdentifiers.IsSelectedProperty, oldValue, newValue);
            RaisePropertyChangedEvent(TogglePatternIdentifiers.ToggleStateProperty,
                oldValue ? ToggleState.On : ToggleState.Off,
                newValue ? ToggleState.On : ToggleState.Off);
        }

        // A selectable chip outside a group has no selection container. Reporting an empty
        // provider keeps the selection-item pattern usable instead of dereferencing a null group.
        private sealed class EmptySelectionProvider : ISelectionProvider
        {
            public static readonly EmptySelectionProvider Instance = new();
            public bool CanSelectMultiple => false;
            public bool IsSelectionRequired => false;
            public IReadOnlyList<AutomationPeer> GetSelection() => [];
        }
    }
}
