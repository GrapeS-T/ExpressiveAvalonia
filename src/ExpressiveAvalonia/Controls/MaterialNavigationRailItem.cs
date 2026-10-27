using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Path = Avalonia.Controls.Shapes.Path;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Theming;
using ExpressiveAvalonia.Tokens;

namespace ExpressiveAvalonia.Controls;

/// <summary>The optional count indicator shown by a navigation rail item.</summary>
public enum MaterialNavigationRailBadge
{
    None,

    /// <summary>A six DIP dot over the top-right of the icon.</summary>
    Small,

    /// <summary>A sixteen DIP pill carrying <see cref="MaterialNavigationRailItem.BadgeText"/>.</summary>
    Large
}

/// <summary>
/// One navigation rail destination. The wide rail animates the item between the collapsed top-icon
/// layout and the expanded start-icon layout while preserving selection.
/// </summary>
/// <remarks>
/// Adapted from <c>NavigationRailItem</c> in <c>NavigationRail.kt</c> and
/// <c>WideNavigationRailItem</c>/<c>AnimatedNavigationItem</c> at the pinned commit. Classic rail
/// items use <c>DefaultEffects</c> for indicator alpha and <c>FastSpatial</c> for the indicator
/// size; wide rail items use <c>DefaultSpatial</c> for the indicator and for the animated icon
/// position, matching <c>animateIndicatorProgressAsState</c> and
/// <c>AnimatedNavigationItem</c>. <see cref="SelectedIcon"/>, <see cref="ShowLabel"/>,
/// <see cref="ShowIcon"/>, <see cref="ShowFocusIndicator"/> and the badge are library extensions.
/// </para>
/// <para>
/// Content tint follows the same files. <c>NavigationRailItem</c> animates the icon and label with
/// <c>animateColorAsState(DefaultEffects)</c> (NavigationRail.kt 188-211); the wide
/// <c>AnimatedNavigationItem</c> passes <c>iconColor</c> directly and animates only the label
/// (<c>animateColor = true</c>, NavigationItem.kt 1248-1263). Roles: unselected
/// on-surface-variant, selected icon and start-position label on-secondary-container, selected
/// top-position label secondary, disabled inactive role at <c>DisabledAlpha</c>.
/// </para>
/// </remarks>
[PseudoClasses(":top", ":start", ":no-label", ":no-icon", ":force-focus", ":custom-content",
    ":badge-small", ":badge-large", ":wide")]
public class MaterialNavigationRailItem : ListBoxItem
{
    /// <summary>The unselected icon path. Its fill is supplied by the item theme.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<MaterialNavigationRailItem, Geometry?>(nameof(Icon));

    /// <summary>Optional selected icon path (extension); falls back to <see cref="Icon"/>.</summary>
    public static readonly StyledProperty<Geometry?> SelectedIconProperty =
        AvaloniaProperty.Register<MaterialNavigationRailItem, Geometry?>(nameof(SelectedIcon));

    /// <summary>Overrides the icon position inherited from the rail.</summary>
    public static readonly StyledProperty<MaterialNavigationItemIconPosition> IconPositionProperty =
        AvaloniaProperty.Register<MaterialNavigationRailItem, MaterialNavigationItemIconPosition>(
            nameof(IconPosition), MaterialNavigationItemIconPosition.Auto);

    /// <summary>Extension: hides the label without changing source item geometry.</summary>
    public static readonly StyledProperty<bool> ShowLabelProperty =
        AvaloniaProperty.Register<MaterialNavigationRailItem, bool>(nameof(ShowLabel), true);

    /// <summary>Extension: hides the leading icon slot.</summary>
    public static readonly StyledProperty<bool> ShowIconProperty =
        AvaloniaProperty.Register<MaterialNavigationRailItem, bool>(nameof(ShowIcon), true);

    /// <summary>
    /// Forces the focus ring on, mirroring the design-source "Show focus indicator" boolean used
    /// by static previews. Keyboard focus shows the same ring without this property.
    /// </summary>
    public static readonly StyledProperty<bool> ShowFocusIndicatorProperty =
        AvaloniaProperty.Register<MaterialNavigationRailItem, bool>(nameof(ShowFocusIndicator));

    /// <summary>
    /// Explicit label text. When unset, the string value of <see cref="ContentControl.Content"/>
    /// is used, so a plain <c>Content="Home"</c> needs no extra property.
    /// </summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<MaterialNavigationRailItem, string?>(nameof(Label));

    /// <summary>The text actually rendered by the item template.</summary>
    public static readonly DirectProperty<MaterialNavigationRailItem, string?> EffectiveLabelProperty =
        AvaloniaProperty.RegisterDirect<MaterialNavigationRailItem, string?>(nameof(EffectiveLabel),
            item => item.EffectiveLabel);

    public static readonly StyledProperty<MaterialNavigationRailBadge> BadgeProperty =
        AvaloniaProperty.Register<MaterialNavigationRailItem, MaterialNavigationRailBadge>(nameof(Badge));

    /// <summary>Text of a <see cref="MaterialNavigationRailBadge.Large"/> badge.</summary>
    public static readonly StyledProperty<string?> BadgeTextProperty =
        AvaloniaProperty.Register<MaterialNavigationRailItem, string?>(nameof(BadgeText));

    /// <summary>Invoked when the item is activated by pointer, Enter, or Space.</summary>
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<MaterialNavigationRailItem, ICommand?>(nameof(Command));

    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<MaterialNavigationRailItem, object?>(nameof(CommandParameter));

    /// <summary>The selected icon, or <see cref="Icon"/> when no selected variant was supplied.</summary>
    public static readonly DirectProperty<MaterialNavigationRailItem, Geometry?> EffectiveSelectedIconProperty =
        AvaloniaProperty.RegisterDirect<MaterialNavigationRailItem, Geometry?>(
            nameof(EffectiveSelectedIcon), item => item.EffectiveSelectedIcon);

    private Geometry? _effectiveSelectedIcon;
    private string? _effectiveLabel;
    private bool _pressed;
    private MaterialNavigationRail? _rail;
    private MaterialNavigationItemLayout? _layout;
    private Path? _iconPath;
    private Path? _selectedIconPath;
    private MaterialMotion.SpringAnimation? _alphaSpring;
    private MaterialMotion.SpringAnimation? _sizeSpring;
    private MaterialMotion.SpringAnimation? _positionSpring;
    private MaterialMotion.SpringAnimation? _minHeightSpring;
    private double _indicatorAlpha;
    private double _indicatorSize;
    private double _positionProgress;
    private double _minimumHeight;
    private bool _springsAreWide;
    private bool _updating;
    private readonly SolidColorBrush _iconBrush = new(Colors.Transparent);
    private readonly SolidColorBrush _labelBrush = new(Colors.Transparent);
    private readonly MaterialSelectionColorAnimation _iconColor;
    private readonly MaterialSelectionColorAnimation _labelColor;
    private TextBlock? _label;
    private Panel? _labelHost;

    /// <summary>Source <c>DisabledAlpha</c> for <c>unselected*.copy(alpha = DisabledAlpha)</c>.</summary>
    private const double DisabledAlpha = 0.38;

    public MaterialNavigationRailItem()
    {
        _iconColor = new MaterialSelectionColorAnimation(this, color => _iconBrush.Color = color);
        _labelColor = new MaterialSelectionColorAnimation(this, color => _labelBrush.Color = color);
    }

    static MaterialNavigationRailItem()
    {
        IconProperty.Changed.AddClassHandler<MaterialNavigationRailItem>((item, _) => item.UpdateSelectedIcon());
        SelectedIconProperty.Changed.AddClassHandler<MaterialNavigationRailItem>((item, _) => item.UpdateSelectedIcon());
        LabelProperty.Changed.AddClassHandler<MaterialNavigationRailItem>((item, _) => item.UpdateLabel());
        IconPositionProperty.Changed.AddClassHandler<MaterialNavigationRailItem>((item, _) =>
        {
            item.UpdateVisualClasses();
            item.UpdateColorTargets(animate: false);
        });
    }

    /// <summary>Raised when the item is activated; the owning rail forwards it.</summary>
    public event EventHandler? Invoked;

    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public Geometry? SelectedIcon { get => GetValue(SelectedIconProperty); set => SetValue(SelectedIconProperty, value); }
    public MaterialNavigationItemIconPosition IconPosition { get => GetValue(IconPositionProperty); set => SetValue(IconPositionProperty, value); }
    public bool ShowLabel { get => GetValue(ShowLabelProperty); set => SetValue(ShowLabelProperty, value); }
    public bool ShowIcon { get => GetValue(ShowIconProperty); set => SetValue(ShowIconProperty, value); }
    public bool ShowFocusIndicator { get => GetValue(ShowFocusIndicatorProperty); set => SetValue(ShowFocusIndicatorProperty, value); }
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string? EffectiveLabel => _effectiveLabel;
    public MaterialNavigationRailBadge Badge { get => GetValue(BadgeProperty); set => SetValue(BadgeProperty, value); }
    public string? BadgeText { get => GetValue(BadgeTextProperty); set => SetValue(BadgeTextProperty, value); }
    public ICommand? Command { get => GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
    public object? CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }
    public Geometry? EffectiveSelectedIcon => _effectiveSelectedIcon;

    /// <summary>Executes <see cref="Command"/> and raises <see cref="Invoked"/>; disabled items do nothing.</summary>
    public void Invoke()
    {
        if (!IsEffectivelyEnabled)
            return;

        var command = Command;
        var parameter = CommandParameter;
        if (command?.CanExecute(parameter) == true)
            command.Execute(parameter);
        Invoked?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Re-evaluates the orientation inherited from the owning rail.</summary>
    internal void RefreshRailState(bool animate = true)
    {
        _rail = ItemsControl.ItemsControlFromItemContainer(this) as MaterialNavigationRail;
        UpdateSelectedIcon();
        UpdateLabel();
        UpdateVisualClasses();
        EnsureAnimations(snap: !animate);
        UpdateIndicatorTargets(animate);
        UpdateNavigationLayout();
        UpdateColorTargets(animate);
    }

    /// <summary>Measures the item as if the wide rail were fully expanded (used for widthIn min/max 360).</summary>
    internal double MeasureExpandedWidth()
    {
        if (_layout is null)
            return 0;
        return _layout.MeasureExpanded(new Size(360, double.PositiveInfinity)).Width;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _layout = e.NameScope.Find<MaterialNavigationItemLayout>("PART_Layout");
        _iconPath = e.NameScope.Find<Path>("PART_Icon");
        _selectedIconPath = e.NameScope.Find<Path>("PART_SelectedIcon");
        _label = e.NameScope.Find<TextBlock>("PART_Label");
        _labelHost = e.NameScope.Find<Panel>("PART_LabelHost");
        ApplyTintBrushes();
        UpdateVisualClasses();
        UpdateIndicatorTargets(animate: false);
        UpdateNavigationLayout();
        UpdateColorTargets(animate: false);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _rail = ItemsControl.ItemsControlFromItemContainer(this) as MaterialNavigationRail;
        UpdateSelectedIcon();
        UpdateLabel();
        UpdateVisualClasses();
        EnsureAnimations(snap: true);
        UpdateIndicatorTargets(animate: false);
        UpdateNavigationLayout();
        UpdateColorTargets(animate: false);
        ResourcesChanged += OnResourcesChanged;
        ActualThemeVariantChanged += OnThemeVariantChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        ResourcesChanged -= OnResourcesChanged;
        ActualThemeVariantChanged -= OnThemeVariantChanged;
        _iconColor.Reset();
        _labelColor.Reset();
        DisposeAnimations();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ContentProperty)
            UpdateLabel();
        else if (change.Property == ShowLabelProperty ||
                 change.Property == ShowIconProperty || change.Property == ShowFocusIndicatorProperty ||
                 change.Property == BadgeProperty || change.Property == BadgeTextProperty)
            UpdateVisualClasses();
        else if (change.Property == ListBoxItem.IsSelectedProperty)
        {
            UpdateVisualClasses();
            UpdateIndicatorTargets(animate: true);
            UpdateColorTargets(animate: true);
        }
        else if (change.Property == IsEnabledProperty ||
                 change.Property == InputElement.IsEffectivelyEnabledProperty)
        {
            UpdateVisualClasses();
            UpdateColorTargets(animate: true);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        // ListBoxItem's PressedMixin owns the :pressed class; this flag only gates activation.
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && IsEffectivelyEnabled)
        {
            _pressed = true;
            Focus(NavigationMethod.Pointer);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_pressed)
            return;

        _pressed = false;
        // A release outside the item cancels activation, matching native button semantics.
        if (IsEffectivelyEnabled && new Rect(Bounds.Size).Contains(e.GetPosition(this)))
            Invoke();
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        _pressed = false;
        base.OnPointerCaptureLost(e);
    }

    private void UpdateSelectedIcon()
    {
        SetAndRaise(EffectiveSelectedIconProperty, ref _effectiveSelectedIcon, SelectedIcon ?? Icon);
    }

    private void UpdateLabel()
    {
        SetAndRaise(EffectiveLabelProperty, ref _effectiveLabel, Label ?? Content as string);
        UpdateVisualClasses();
        _rail?.ScheduleExpandedWidthUpdate();
    }

    private void UpdateVisualClasses()
    {
        _rail ??= ItemsControl.ItemsControlFromItemContainer(this) as MaterialNavigationRail;
        var wide = _rail?.Mode == MaterialNavigationRailMode.Wide;
        var position = ResolveIconPosition();

        var top = position == MaterialNavigationItemIconPosition.Top;
        var customContent = Content is not null and not string;
        PseudoClasses.Set(":top", top);
        PseudoClasses.Set(":start", !top);
        PseudoClasses.Set(":wide", wide);
        PseudoClasses.Set(":custom-content", customContent);
        PseudoClasses.Set(":no-label", !ShowLabel || string.IsNullOrEmpty(_effectiveLabel));
        PseudoClasses.Set(":badge-small", Badge == MaterialNavigationRailBadge.Small);
        PseudoClasses.Set(":badge-large", Badge == MaterialNavigationRailBadge.Large);
        PseudoClasses.Set(":no-icon", !ShowIcon);
        PseudoClasses.Set(":force-focus", ShowFocusIndicator);

        AutomationProperties.SetItemStatus(this,
            Badge != MaterialNavigationRailBadge.None && !string.IsNullOrEmpty(BadgeText) ? BadgeText : null);
    }

    private void UpdateIndicatorTargets(bool animate)
    {
        var selected = IsSelected ? 1d : 0d;
        var wide = _rail?.Mode == MaterialNavigationRailMode.Wide;
        var position = ResolveIconPosition();
        var positionTarget = position == MaterialNavigationItemIconPosition.Start ? 1d : 0d;
        var minHeightTarget = wide
            ? (_rail?.IsExpanded == true ? 48d : 64d)
            : (_rail?.Mode == MaterialNavigationRailMode.Compact ? 56d : 56d);

        if (!animate || !this.IsAttachedToVisualTree())
        {
            _indicatorAlpha = selected;
            _indicatorSize = selected;
            _positionProgress = positionTarget;
            _minimumHeight = minHeightTarget;
            UpdateNavigationLayout();
            return;
        }

        EnsureAnimations(snap: false);
        _alphaSpring?.Retarget(selected);
        _sizeSpring?.Retarget(selected);
        if (wide)
            _positionSpring?.Retarget(positionTarget);
        else
        {
            _positionProgress = positionTarget;
        }
        _minHeightSpring?.Retarget(minHeightTarget);
    }

    private void EnsureAnimations(bool snap)
    {
        if (!this.IsAttachedToVisualTree())
            return;

        var wide = _rail?.Mode == MaterialNavigationRailMode.Wide;
        if (wide != _springsAreWide)
        {
            DisposeAnimations();
            _springsAreWide = wide;
        }

        var alphaSpring = wide ? MaterialSpring.SpatialDefault : MaterialSpring.EffectsDefault;
        var sizeSpring = wide ? MaterialSpring.SpatialDefault : MaterialSpring.SpatialFast;

        if (_alphaSpring is null)
        {
            _indicatorAlpha = snap ? (IsSelected ? 1 : 0) : _indicatorAlpha;
            _alphaSpring = MaterialMotion.Animate(this, _indicatorAlpha, _indicatorAlpha,
                value => { _indicatorAlpha = value; UpdateNavigationLayout(); }, alphaSpring, 0.01);
        }
        if (_sizeSpring is null)
        {
            _indicatorSize = snap ? (IsSelected ? 1 : 0) : _indicatorSize;
            _sizeSpring = MaterialMotion.Animate(this, _indicatorSize, _indicatorSize,
                value => { _indicatorSize = value; UpdateNavigationLayout(); }, sizeSpring, 0.01);
        }
        if (_positionSpring is null)
        {
            _positionSpring = MaterialMotion.Animate(this, _positionProgress, _positionProgress,
                value => { _positionProgress = value; UpdateNavigationLayout(); }, MaterialSpring.SpatialDefault, 0.01);
        }
        if (_minHeightSpring is null)
        {
            var target = wide ? (_rail?.IsExpanded == true ? 48d : 64d) : 56d;
            _minimumHeight = snap ? target : _minimumHeight;
            _minHeightSpring = MaterialMotion.Animate(this, _minimumHeight, _minimumHeight,
                value => { _minimumHeight = value; UpdateNavigationLayout(); }, MaterialSpring.SpatialDefault, 0.5);
        }
    }

    private void DisposeAnimations()
    {
        _alphaSpring?.Dispose();
        _alphaSpring = null;
        _sizeSpring?.Dispose();
        _sizeSpring = null;
        _positionSpring?.Dispose();
        _positionSpring = null;
        _minHeightSpring?.Dispose();
        _minHeightSpring = null;
    }

    private void UpdateNavigationLayout()
    {
        if (_layout is null || _updating)
            return;

        _updating = true;
        try
        {
            _rail ??= ItemsControl.ItemsControlFromItemContainer(this) as MaterialNavigationRail;
            var wide = _rail?.Mode == MaterialNavigationRailMode.Wide;
            var position = ResolveIconPosition();

            var hasLabel = ShowLabel &&
                (!string.IsNullOrEmpty(_effectiveLabel) || Content is not null and not string);
            _layout.Kind = wide ? MaterialNavigationItemLayoutKind.WideRail : MaterialNavigationItemLayoutKind.Rail;
            _layout.IconPosition = position;
            _layout.AlwaysShowLabel = true;
            _layout.HasLabel = hasLabel;
            _layout.MinimumItemHeight = wide ? _minimumHeight : 56d;
            _layout.IndicatorProgress = _indicatorSize;
            _layout.IndicatorAlpha = _indicatorAlpha;
            _layout.IconPositionProgress = wide
                ? _positionProgress
                : position == MaterialNavigationItemIconPosition.Start ? 1 : 0;

            var labelOpacity = !hasLabel
                ? 0
                : wide
                    ? 4 * (_positionProgress - 0.5) * (_positionProgress - 0.5)
                    : 1;
            _layout.LabelOpacity = labelOpacity;

            if (_iconPath is not null && _selectedIconPath is not null)
            {
                // SelectedIcon is a library geometry extension: only a distinct caller geometry
                // crossfades. The colour channel never rides this opacity, so the wide rail paints
                // its icon tint immediately on selection.
                var crossfade = SelectedIcon is not null;
                _iconPath.Opacity = crossfade ? 1 - Math.Clamp(_indicatorAlpha, 0, 1) : 1;
                _selectedIconPath.Opacity = crossfade ? Math.Clamp(_indicatorAlpha, 0, 1) : 0;
            }
        }
        finally
        {
            _updating = false;
        }
    }

    private MaterialNavigationItemIconPosition ResolveIconPosition()
    {
        _rail ??= ItemsControl.ItemsControlFromItemContainer(this) as MaterialNavigationRail;
        var position = IconPosition;
        if (position == MaterialNavigationItemIconPosition.Auto)
            position = _rail?.ResolveItemIconPosition() ?? MaterialNavigationItemIconPosition.Top;
        return position;
    }

    /// <summary>Attaches the owned tint brushes to the icon and label template parts.</summary>
    private void ApplyTintBrushes()
    {
        if (_iconPath is not null)
            _iconPath.Fill = _iconBrush;
        if (_selectedIconPath is not null)
            _selectedIconPath.Fill = _iconBrush;
        if (_label is not null)
            _label.Foreground = _labelBrush;
        // Rich label content keeps its template but inherits the same content role.
        _labelHost?.SetValue(TextElement.ForegroundProperty, _labelBrush);
    }

    /// <summary>
    /// Retargets the icon and label colour channels. The compact rail animates both with the
    /// independent <c>DefaultEffects</c> colour spring; the wide rail passes <c>iconColor</c>
    /// straight through and only animates the label (NavigationItem.kt 1248-1263).
    /// </summary>
    private void UpdateColorTargets(bool animate)
    {
        var wide = _rail?.Mode == MaterialNavigationRailMode.Wide;
        var attached = this.IsAttachedToVisualTree();
        var enabled = IsEffectivelyEnabled;
        var selected = IsSelected;
        var position = ResolveIconPosition();
        _iconColor.Set(
            ResolveIconColor(enabled, selected), MaterialSpring.EffectsDefault,
            animate && attached && !wide);
        _labelColor.Set(
            ResolveLabelColor(enabled, selected, position), MaterialSpring.EffectsDefault,
            animate && attached);
    }

    private Color ResolveIconColor(bool enabled, bool selected) =>
        !enabled
            ? DisabledRoleColor()
            : selected
                ? RoleColor("MaterialOnSecondaryContainerColor")
                : RoleColor("MaterialOnSurfaceVariantColor");

    private Color ResolveLabelColor(
        bool enabled, bool selected, MaterialNavigationItemIconPosition position) =>
        !enabled
            ? DisabledRoleColor()
            : !selected
                ? RoleColor("MaterialOnSurfaceVariantColor")
                : position == MaterialNavigationItemIconPosition.Start
                    ? RoleColor("MaterialOnSecondaryContainerColor")
                    : RoleColor("MaterialSecondaryColor");

    /// <summary>disabled*Color is the inactive role copied at <c>DisabledAlpha</c>.</summary>
    private Color DisabledRoleColor()
    {
        var color = RoleColor("MaterialOnSurfaceVariantColor");
        return Color.FromArgb(
            (byte)Math.Clamp(Math.Round(color.A * DisabledAlpha), 0, 255),
            color.R, color.G, color.B);
    }

    private Color RoleColor(string key) =>
        this.TryFindResource(key, ActualThemeVariant, out var value) && value is Color color
            ? color
            : Colors.Transparent;

    private void OnResourcesChanged(object? sender, ResourcesChangedEventArgs e) =>
        UpdateColorTargets(animate: false);

    private void OnThemeVariantChanged(object? sender, EventArgs e) =>
        UpdateColorTargets(animate: false);
}