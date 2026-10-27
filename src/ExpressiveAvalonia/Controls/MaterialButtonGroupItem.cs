using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using AvaloniaPath = Avalonia.Controls.Shapes.Path;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Theming;
using ExpressiveAvalonia.Tokens;

namespace ExpressiveAvalonia.Controls;

/// <summary>The position of an item inside a <see cref="MaterialButtonGroup"/>.</summary>
/// <remarks>Corner radii of connected and segmented groups depend on the position, so the group
/// keeps this property in sync with the realized containers.</remarks>
public enum MaterialButtonGroupItemPosition { Only, First, Middle, Last }

/// <summary>One selectable button inside a <see cref="MaterialButtonGroup"/>.</summary>
/// <remarks>
/// <para>
/// Derives from <see cref="ToggleButton"/>, so it keeps native pointer, Space/Enter activation,
/// focus visuals, commands, access keys and the Toggle automation pattern. The owning group
/// decides whether checking is allowed (no selection, one selection, or multiple selections)
/// and normalizes the selection; the item only reports its state.
/// </para>
/// <para>
/// The item renders the three source content anatomies: the standard/connected leading icon plus
/// label, and the segmented button's fixed 18 + 8 icon slot whose checked mark fades and scales in
/// from the start edge while the label slides between the centred and icon-led positions. Standard
/// group items expose their measured width to the group's pressed expansion, which is a layout
/// behaviour of the source <c>ButtonGroup</c> rather than a render scale.
/// </para>
/// </remarks>
public class MaterialButtonGroupItem : ToggleButton
{
    /// <summary>Optional leading icon; accepts an icon control, image or any other content.</summary>
    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<MaterialButtonGroupItem, object?>(nameof(Icon));

    /// <summary>The group anatomy the item is currently rendered in.</summary>
    public static readonly StyledProperty<MaterialButtonGroupKind> KindProperty =
        AvaloniaProperty.Register<MaterialButtonGroupItem, MaterialButtonGroupKind>(nameof(Kind));

    /// <summary>The Material Expressive size shared by every item of the group.</summary>
    public static readonly StyledProperty<MaterialControlSize> ControlSizeProperty =
        AvaloniaProperty.Register<MaterialButtonGroupItem, MaterialControlSize>(
            nameof(ControlSize), MaterialControlSize.Small);

    /// <summary>The resting silhouette of the item.</summary>
    public static readonly StyledProperty<MaterialButtonShape> ButtonShapeProperty =
        AvaloniaProperty.Register<MaterialButtonGroupItem, MaterialButtonShape>(nameof(ButtonShape));

    /// <summary>The color treatment of a standard group item.</summary>
    public static readonly StyledProperty<MaterialButtonVariant> VariantProperty =
        AvaloniaProperty.Register<MaterialButtonGroupItem, MaterialButtonVariant>(
            nameof(Variant), MaterialButtonVariant.Filled);

    /// <summary>Whether the selected state replaces the leading icon with a check mark.</summary>
    public static readonly StyledProperty<bool> ReplaceIconWhenSelectedProperty =
        AvaloniaProperty.Register<MaterialButtonGroupItem, bool>(nameof(ReplaceIconWhenSelected));

    /// <summary>Whether a selected item shows a check mark before its content.</summary>
    public static readonly StyledProperty<bool> ShowSelectedIconProperty =
        AvaloniaProperty.Register<MaterialButtonGroupItem, bool>(nameof(ShowSelectedIcon));

    /// <summary>
    /// Whether the check mark is currently painted; derived from selection and retained through the
    /// outgoing segmented cross-fade until its opacity reaches rest.
    /// </summary>
    public static readonly StyledProperty<bool> ShowCheckIconProperty =
        AvaloniaProperty.Register<MaterialButtonGroupItem, bool>(nameof(ShowCheckIcon));

    /// <summary>Whether the leading icon is currently painted; derived from selection and icon.</summary>
    public static readonly StyledProperty<bool> ShowIconContentProperty =
        AvaloniaProperty.Register<MaterialButtonGroupItem, bool>(nameof(ShowIconContent));

    /// <summary>Whether the item has an icon and no content, which switches to icon-button metrics.</summary>
    public static readonly StyledProperty<bool> IsIconOnlyProperty =
        AvaloniaProperty.Register<MaterialButtonGroupItem, bool>(nameof(IsIconOnly));

    /// <summary>The edge length of the leading icon and check mark.</summary>
    public static readonly StyledProperty<double> IconSizeProperty =
        AvaloniaProperty.Register<MaterialButtonGroupItem, double>(nameof(IconSize), 24d);

    /// <summary>The gap between the leading icon, the check mark and the content.</summary>
    public static readonly StyledProperty<double> ContentSpacingProperty =
        AvaloniaProperty.Register<MaterialButtonGroupItem, double>(nameof(ContentSpacing), 8d);

    /// <summary>The position inside the group, which drives the corner radii.</summary>
    public static readonly StyledProperty<MaterialButtonGroupItemPosition> PositionProperty =
        AvaloniaProperty.Register<MaterialButtonGroupItem, MaterialButtonGroupItemPosition>(nameof(Position));

    /// <summary>
    /// The inset of the painted container inside the item's interactive target, which keeps the
    /// 48-DIP target of the smaller sizes while the visible container stays 32 or 40 DIP high.
    /// </summary>
    public static readonly StyledProperty<Thickness> ContainerInsetProperty =
        AvaloniaProperty.Register<MaterialButtonGroupItem, Thickness>(nameof(ContainerInset));

    /// <summary>The group orientation, which rotates the corner radii for vertical groups.</summary>
    public static readonly StyledProperty<Orientation> GroupOrientationProperty =
        AvaloniaProperty.Register<MaterialButtonGroupItem, Orientation>(nameof(GroupOrientation));

    /// <summary>The reserved leading slot of a segmented button: 18 DIP glyph plus an 8 DIP gap.</summary>
    public static readonly StyledProperty<double> SegmentedIconSlotWidthProperty =
        AvaloniaProperty.Register<MaterialButtonGroupItem, double>(nameof(SegmentedIconSlotWidth), 26d);

    /// <summary>
    /// The animated horizontal correction of the segmented label: −13 DIP while the leading slot is
    /// empty (so the text is optically centred) and 0 while an icon or check mark occupies it.
    /// </summary>
    public static readonly StyledProperty<double> SegmentedContentOffsetProperty =
        AvaloniaProperty.Register<MaterialButtonGroupItem, double>(nameof(SegmentedContentOffset));

    /// <summary>The animated scale of the segmented check mark, 0 to 1 from the start edge.</summary>
    public static readonly StyledProperty<double> CheckScaleProperty =
        AvaloniaProperty.Register<MaterialButtonGroupItem, double>(nameof(CheckScale), 1d);

    /// <summary>The animated opacity of the check mark.</summary>
    public static readonly StyledProperty<double> CheckOpacityProperty =
        AvaloniaProperty.Register<MaterialButtonGroupItem, double>(nameof(CheckOpacity), 1d);

    /// <summary>
    /// The animated opacity of the segmented leading icon. The icon and the active check mark
    /// cross-fade through this value when the caller replaces the icon with the check.
    /// </summary>
    public static readonly StyledProperty<double> ContentIconOpacityProperty =
        AvaloniaProperty.Register<MaterialButtonGroupItem, double>(nameof(ContentIconOpacity), 1d);

    /// <summary>
    /// True while the leading icon occupies its reserved segmented slot, including the fade-out
    /// phase of the icon→check cross-fade.
    /// </summary>
    public static readonly StyledProperty<bool> ShowIconSlotProperty =
        AvaloniaProperty.Register<MaterialButtonGroupItem, bool>(nameof(ShowIconSlot));


    private AvaloniaPath? _checkMark;
    private AvaloniaPath? _segmentedCheckMark;
    private ScaleTransform? _checkMarkScale;
    private ScaleTransform? _segmentedCheckMarkScale;

    private MaterialMotion.SpringAnimation? _offsetSpring;
    private MaterialMotion.SpringAnimation? _checkScaleSpring;
    private MaterialMotion.SpringAnimation? _checkOpacitySpring;
    private MaterialMotion.SpringAnimation? _iconOpacitySpring;
    private bool _suppressCheckAnimation;

    static MaterialButtonGroupItem()
    {
        IconProperty.Changed.AddClassHandler<MaterialButtonGroupItem>(static (item, _) => item.UpdateContentSlots());
        ContentProperty.Changed.AddClassHandler<MaterialButtonGroupItem>(static (item, _) => item.UpdateContentSlots());
        KindProperty.Changed.AddClassHandler<MaterialButtonGroupItem>(static (item, _) => item.UpdateContentSlots());
        IsCheckedProperty.Changed.AddClassHandler<MaterialButtonGroupItem>(static (item, _) => item.UpdateContentSlots());
        ShowSelectedIconProperty.Changed.AddClassHandler<MaterialButtonGroupItem>(static (item, _) => item.UpdateContentSlots());
        ReplaceIconWhenSelectedProperty.Changed.AddClassHandler<MaterialButtonGroupItem>(static (item, _) => item.UpdateContentSlots());
    }

    public MaterialButtonGroupItem()
    {
        _suppressCheckAnimation = true;
        UpdateContentSlots();
        _suppressCheckAnimation = false;
    }

    /// <summary>The owning group; set by the group when the container is prepared.</summary>
    public MaterialButtonGroup? Group { get; internal set; }

    /// <summary>The spring-driven pressed-expansion progress; spatial overshoot is preserved.</summary>
    internal double PressExpansion { get; set; }

    /// <summary>The maximum width the neighbours may compress, taken from the content padding.</summary>
    internal double CompressionLimit { get; set; }

    public object? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public MaterialButtonGroupKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public MaterialControlSize ControlSize { get => GetValue(ControlSizeProperty); set => SetValue(ControlSizeProperty, value); }
    public MaterialButtonShape ButtonShape { get => GetValue(ButtonShapeProperty); set => SetValue(ButtonShapeProperty, value); }
    public MaterialButtonVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
    public bool ReplaceIconWhenSelected { get => GetValue(ReplaceIconWhenSelectedProperty); set => SetValue(ReplaceIconWhenSelectedProperty, value); }
    public bool ShowSelectedIcon { get => GetValue(ShowSelectedIconProperty); set => SetValue(ShowSelectedIconProperty, value); }
    public bool ShowCheckIcon { get => GetValue(ShowCheckIconProperty); private set => SetValue(ShowCheckIconProperty, value); }
    public bool ShowIconContent { get => GetValue(ShowIconContentProperty); private set => SetValue(ShowIconContentProperty, value); }
    public bool IsIconOnly { get => GetValue(IsIconOnlyProperty); private set => SetValue(IsIconOnlyProperty, value); }
    public double IconSize { get => GetValue(IconSizeProperty); set => SetValue(IconSizeProperty, value); }
    public double ContentSpacing { get => GetValue(ContentSpacingProperty); set => SetValue(ContentSpacingProperty, value); }
    public MaterialButtonGroupItemPosition Position { get => GetValue(PositionProperty); set => SetValue(PositionProperty, value); }
    public Thickness ContainerInset { get => GetValue(ContainerInsetProperty); set => SetValue(ContainerInsetProperty, value); }
    public Orientation GroupOrientation { get => GetValue(GroupOrientationProperty); set => SetValue(GroupOrientationProperty, value); }
    public double SegmentedIconSlotWidth { get => GetValue(SegmentedIconSlotWidthProperty); set => SetValue(SegmentedIconSlotWidthProperty, value); }
    public double SegmentedContentOffset { get => GetValue(SegmentedContentOffsetProperty); private set => SetValue(SegmentedContentOffsetProperty, value); }
    public double CheckScale { get => GetValue(CheckScaleProperty); private set => SetValue(CheckScaleProperty, value); }
    public double CheckOpacity { get => GetValue(CheckOpacityProperty); private set => SetValue(CheckOpacityProperty, value); }
    public double ContentIconOpacity { get => GetValue(ContentIconOpacityProperty); private set => SetValue(ContentIconOpacityProperty, value); }
    public bool ShowIconSlot { get => GetValue(ShowIconSlotProperty); private set => SetValue(ShowIconSlotProperty, value); }

    /// <summary>Runs the native activation path, used by the group's overflow menu.</summary>
    internal void PerformClick() => OnClick();

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _checkMark = e.NameScope.Find<AvaloniaPath>("PART_CheckMark");
        _segmentedCheckMark = e.NameScope.Find<AvaloniaPath>("PART_SegmentedCheckMark");
        // Source initial composition starts the inactive child at its resting values: the check
        // opacity is zero when hidden, so the first selection runs the enter transition instead
        // of popping in from the property default.
        SnapCheckState();
        UpdateCheckVisuals();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        DisposeAnimations();
        SnapCheckState();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsCheckedProperty)
            UpdateCheckMark();
        else if (change.Property == CheckScaleProperty || change.Property == CheckOpacityProperty ||
                 change.Property == FlowDirectionProperty)
            UpdateCheckVisuals();
    }

    private bool IsSegmentedCrossfade =>
        Kind == MaterialButtonGroupKind.Segmented && ReplaceIconWhenSelected && Icon is not null;

    private void UpdateCheckVisuals()
    {
        // Both marks animate on every spring frame, so each keeps one ScaleTransform that is
        // updated in place instead of a fresh transform and array per frame.
        _checkMarkScale = ApplyCheckVisual(_checkMark, _checkMarkScale);
        _segmentedCheckMarkScale = ApplyCheckVisual(_segmentedCheckMark, _segmentedCheckMarkScale);

        // The outgoing half of the segmented crossfade is retained in UpdateContentSlots. Once
        // its opacity reaches rest, the retained mark leaves the composition for good.
        if (IsSegmentedCrossfade && !(IsChecked == true && ShowSelectedIcon) &&
            ShowCheckIcon && CheckOpacity <= 0.0001)
            SetCurrentValue(ShowCheckIconProperty, false);
    }

    private ScaleTransform? ApplyCheckVisual(AvaloniaPath? path, ScaleTransform? transform)
    {
        if (path is null)
            return null;

        var scale = CheckScale;
        if (transform is null || !ReferenceEquals(path.RenderTransform, transform))
        {
            // A new template part (or the first frame) owns a new transform; later frames only
            // touch the two scale components.
            transform = new ScaleTransform(scale, scale);
            path.RenderTransform = transform;
        }
        else
        {
            transform.ScaleX = scale;
            transform.ScaleY = scale;
        }

        path.Opacity = CheckOpacity;
        return transform;
    }

    private void UpdateContentSlots()
    {
        var selected = IsChecked == true;
        var check = selected && ShowSelectedIcon;
        var segmented = Kind == MaterialButtonGroupKind.Segmented;
        // The segmented leading slot holds a single glyph: the caller's icon, the active check, or
        // both during the cross-fade. Connected/standard items show a check beside the icon unless
        // the icon is replaced, which removes it from the row immediately.
        var iconVisible = Icon is not null && !(check && (ReplaceIconWhenSelected || segmented));

        var checkChanged = check != ShowCheckIcon;
        var iconChanged = iconVisible != ShowIconContent;
        // SegmentedButtonDefaults.Icon with an inactiveContent runs a Crossfade: both children
        // stay composed while the DefaultEffects transition runs, so the outgoing check must
        // remain rendered until its opacity spring rests. The no-inactiveContent branch keeps
        // AnimatedVisibility's immediate exit (ExitTransition.None).
        var retainOutgoingCheck = !check && IsSegmentedCrossfade && ShowCheckIcon && CheckOpacity > 0;
        SetCurrentValue(ShowCheckIconProperty, check || retainOutgoingCheck);
        SetCurrentValue(ShowIconContentProperty, iconVisible);
        SetCurrentValue(ShowIconSlotProperty, segmented && Icon is not null);
        SetCurrentValue(IsIconOnlyProperty, Icon is not null && Content is null);

        if (segmented)
            UpdateSegmentedMetrics(iconVisible, check,
                animate: !_suppressCheckAnimation && (checkChanged || iconChanged));
        else
        {
            _offsetSpring?.Dispose();
            _offsetSpring = null;
            SetCurrentValue(SegmentedContentOffsetProperty, 0d);
            if (checkChanged && !_suppressCheckAnimation)
                UpdateSegmentedCheckAnimation();
        }

        if (Content is string text && !string.IsNullOrWhiteSpace(text) &&
            string.IsNullOrEmpty(AutomationProperties.GetName(this)))
            AutomationProperties.SetName(this, text);
    }

    private void UpdateSegmentedMetrics(bool iconVisible, bool check, bool animate)
    {
        var offset = iconVisible || check ? 0d : -(SegmentedIconSlotWidth / 2);
        if (Math.Abs(SegmentedContentOffset - offset) > 0.01)
        {
            if (animate && this.IsAttachedToVisualTree())
            {
                _offsetSpring?.Dispose();
                _offsetSpring = MaterialMotion.Animate(this, SegmentedContentOffset, offset,
                    value => SegmentedContentOffset = value, MaterialSpring.SpatialFast, 0.5);
            }
            else
            {
                _offsetSpring?.Dispose();
                _offsetSpring = null;
                SetCurrentValue(SegmentedContentOffsetProperty, offset);
            }
        }

        if (ReplaceIconWhenSelected && Icon is not null)
        {
            // SegmentedButtonDefaults.Icon with an inactiveContent uses Crossfade with the
            // DefaultEffects scheme: both glyphs share the reserved slot, the initial icon fades
            // out while the check fades in, and no scale is applied. Reversals retarget the same
            // springs, so the current opacity velocity is preserved.
            _checkScaleSpring?.Dispose();
            _checkScaleSpring = null;
            SetCurrentValue(CheckScaleProperty, 1d);
            AnimateSegmentedCrossfade(iconVisible, check, animate);
            return;
        }

        // Without an inactiveContent the mark animates with AnimatedVisibility (enter fade+scale,
        // exit None), and the leading icon simply leaves the slot when the check takes over.
        _iconOpacitySpring?.Dispose();
        _iconOpacitySpring = null;
        _checkOpacitySpring?.Dispose();
        _checkOpacitySpring = null;
        SetCurrentValue(ContentIconOpacityProperty, iconVisible ? 1d : 0d);
        if (check)
        {
            UpdateSegmentedCheckAnimation();
        }
        else
        {
            _checkScaleSpring?.Dispose();
            _checkScaleSpring = null;
            SetCurrentValue(CheckScaleProperty, 0d);
            SetCurrentValue(CheckOpacityProperty, 0d);
        }
    }

    private void AnimateSegmentedCrossfade(bool iconVisible, bool check, bool animate)
    {
        var iconTarget = iconVisible ? 1d : 0d;
        var checkTarget = check ? 1d : 0d;

        if (!animate || !this.IsAttachedToVisualTree() || MaterialMotion.GetIsReduced(this))
        {
            _checkOpacitySpring?.Dispose();
            _checkOpacitySpring = null;
            SetCurrentValue(ContentIconOpacityProperty, iconTarget);
            SetCurrentValue(CheckOpacityProperty, checkTarget);
            return;
        }

        if (_iconOpacitySpring is { } icon)
            icon.Retarget(iconTarget);
        else
            _iconOpacitySpring = MaterialMotion.Animate(this, ContentIconOpacity, iconTarget,
                value => ContentIconOpacity = value, MaterialSpring.EffectsDefault, 0.001);

        if (_checkOpacitySpring is { } checkSpring)
            checkSpring.Retarget(checkTarget);
        else
            _checkOpacitySpring = MaterialMotion.Animate(this, CheckOpacity, checkTarget,
                value => CheckOpacity = value, MaterialSpring.EffectsDefault, 0.001);
    }

    // SegmentedButtonDefaults.Icon: the active mark fades in with DefaultEffects and scales in from
    // zero anchored at the start/bottom corner with FastSpatial. The exit transition is None, so
    // hiding the mark clears both values immediately.
    private void UpdateSegmentedCheckAnimation()
    {
        if (!this.IsAttachedToVisualTree() || MaterialMotion.GetIsReduced(this))
        {
            SetCurrentValue(CheckScaleProperty, 1d);
            SetCurrentValue(CheckOpacityProperty, 1d);
            return;
        }

        if (_checkScaleSpring is { } scale)
            scale.Retarget(1);
        else
            _checkScaleSpring = MaterialMotion.Animate(this, CheckScale, 1,
                value => CheckScale = value, MaterialSpring.SpatialFast, 0.001);

        if (_checkOpacitySpring is { } opacity)
            opacity.Retarget(1);
        else
            _checkOpacitySpring = MaterialMotion.Animate(this, CheckOpacity, 1,
                value => CheckOpacity = value, MaterialSpring.EffectsDefault, 0.001);

    }

    private void UpdateCheckMark() => UpdateContentSlots();

    private void SnapCheckState()
    {
        var check = IsChecked == true && ShowSelectedIcon;
        var segmented = Kind == MaterialButtonGroupKind.Segmented;
        var iconVisible = Icon is not null && !(check && (ReplaceIconWhenSelected || segmented));
        SetCurrentValue(ContentIconOpacityProperty, iconVisible ? 1d : 0d);
        SetCurrentValue(CheckOpacityProperty, check ? 1d : 0d);
        SetCurrentValue(CheckScaleProperty, IsSegmentedCrossfade || check ? 1d : 0d);
        SetCurrentValue(ShowCheckIconProperty, check);
    }

    private void DisposeAnimations()
    {
        _offsetSpring?.Dispose();
        _offsetSpring = null;
        _checkScaleSpring?.Dispose();
        _checkScaleSpring = null;
        _checkOpacitySpring?.Dispose();
        _checkOpacitySpring = null;
        _iconOpacitySpring?.Dispose();
        _iconOpacitySpring = null;
    }

}