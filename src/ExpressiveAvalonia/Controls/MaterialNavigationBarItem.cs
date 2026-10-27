using System;
using System.Globalization;
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

/// <summary>The badge treatment of a navigation bar destination.</summary>
public enum MaterialNavigationBarBadge
{
    None,

    /// <summary>A six DIP dot over the top-right of the icon.</summary>
    Small,

    /// <summary>A sixteen DIP pill carrying <see cref="MaterialNavigationBarItem.BadgeText"/>.</summary>
    Large
}

/// <summary>
/// One source navigation bar destination: icon, optional label, optional badge, single-selection
/// ListBoxItem behavior and frame-driven Material motion.
/// </summary>
/// <remarks>
/// <para>
/// Adapted from <c>NavigationBar.kt</c> <c>NavigationBarItem</c> and
/// <c>NavigationBarItemLayout</c>, <c>ShortNavigationBar.kt</c> <c>ShortNavigationBarItem</c> and
/// <c>NavigationItem.kt</c> at the pinned AndroidX commit. The source indicator opacity uses the
/// default-effects spring and the classic indicator size uses fast-spatial; the short bar uses the
/// default-spatial spring for both, matching <c>animateFloatAsState</c> in those files.
/// </para>
/// <para>
/// Content tint follows the same files. The classic bar animates the icon and label with
/// <c>animateColorAsState(MotionSchemeKeyTokens.DefaultEffects)</c> (NavigationBar.kt 185-208), an
/// independent Oklab colour spring that never reads the indicator progress. The short bar passes
/// <c>iconColor</c>/<c>textColor</c> straight to <c>NavigationItemLayout</c> and
/// <c>animateColor = false</c> (NavigationItem.kt 356-370), so selection changes the tint on the
/// next frame. Roles: unselected on-surface-variant, selected icon and start-position label
/// on-secondary-container, selected top-position label secondary, disabled inactive role at
/// <c>DisabledAlpha</c>.
/// </para>
/// <para>
/// The bar owns selection. This item never creates a router or page: pointer release, Enter or
/// Space execute <see cref="Command"/> and raise <see cref="Invoked"/>; the owning
/// <see cref="MaterialNavigationBar"/> reports the new selection.
/// </para>
/// <para>
/// <see cref="SelectedIcon"/>, <see cref="ShowLabel"/>, <see cref="Badge"/> and
/// <see cref="BadgeText"/> are library extensions: the pinned source bar item has a single icon and
/// no badge. They are classified as extensions, not as upstream API.
/// </para>
/// </remarks>
[PseudoClasses(":top", ":start", ":no-label", ":badge-small", ":badge-large", ":short")]
public class MaterialNavigationBarItem : ListBoxItem
{
    /// <summary>The icon path. Its tint is the source inactive/active content role resolved by the item.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<MaterialNavigationBarItem, Geometry?>(nameof(Icon));

    /// <summary>Optional selected icon path (extension); falls back to <see cref="Icon"/>.</summary>
    public static readonly StyledProperty<Geometry?> SelectedIconProperty =
        AvaloniaProperty.Register<MaterialNavigationBarItem, Geometry?>(nameof(SelectedIcon));

    /// <summary>Overrides the icon position inherited from the bar.</summary>
    public static readonly StyledProperty<MaterialNavigationItemIconPosition> IconPositionProperty =
        AvaloniaProperty.Register<MaterialNavigationBarItem, MaterialNavigationItemIconPosition>(
            nameof(IconPosition), MaterialNavigationItemIconPosition.Auto);

    /// <summary>Source <c>alwaysShowLabel</c>; when false the label appears only on the selected item.</summary>
    public static readonly StyledProperty<bool> AlwaysShowLabelProperty =
        AvaloniaProperty.Register<MaterialNavigationBarItem, bool>(nameof(AlwaysShowLabel), true);

    /// <summary>Extension: hides labels on every destination, independent of selection.</summary>
    public static readonly StyledProperty<bool> ShowLabelProperty =
        AvaloniaProperty.Register<MaterialNavigationBarItem, bool>(nameof(ShowLabel), true);

    /// <summary>Extension: badge treatment over the icon.</summary>
    public static readonly StyledProperty<MaterialNavigationBarBadge> BadgeProperty =
        AvaloniaProperty.Register<MaterialNavigationBarItem, MaterialNavigationBarBadge>(nameof(Badge));

    /// <summary>Extension: text of a <see cref="MaterialNavigationBarBadge.Large"/> badge.</summary>
    public static readonly StyledProperty<string?> BadgeTextProperty =
        AvaloniaProperty.Register<MaterialNavigationBarItem, string?>(nameof(BadgeText));

    /// <summary>Invoked when the destination is activated by pointer, Enter or Space.</summary>
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<MaterialNavigationBarItem, ICommand?>(nameof(Command));

    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<MaterialNavigationBarItem, object?>(nameof(CommandParameter));

    /// <summary>The selected icon, or <see cref="Icon"/> when no selected variant was supplied.</summary>
    public static readonly DirectProperty<MaterialNavigationBarItem, Geometry?> EffectiveSelectedIconProperty =
        AvaloniaProperty.RegisterDirect<MaterialNavigationBarItem, Geometry?>(nameof(EffectiveSelectedIcon),
            item => item.EffectiveSelectedIcon);

    /// <summary>The label text taken from <see cref="ContentControl.Content"/>.</summary>
    public static readonly DirectProperty<MaterialNavigationBarItem, string?> LabelTextProperty =
        AvaloniaProperty.RegisterDirect<MaterialNavigationBarItem, string?>(nameof(LabelText),
            item => item.LabelText);

    /// <summary>Whether the text label is painted; false without a label, labels off, or hidden by selection.</summary>
    public static readonly DirectProperty<MaterialNavigationBarItem, bool> IsLabelVisibleProperty =
        AvaloniaProperty.RegisterDirect<MaterialNavigationBarItem, bool>(nameof(IsLabelVisible),
            item => item.IsLabelVisible);

    /// <summary>Whether non-text content is painted in the label slot.</summary>
    public static readonly DirectProperty<MaterialNavigationBarItem, bool> IsContentVisibleProperty =
        AvaloniaProperty.RegisterDirect<MaterialNavigationBarItem, bool>(nameof(IsContentVisible),
            item => item.IsContentVisible);

    private Geometry? _effectiveSelectedIcon;
    private string? _labelText;
    private bool _isLabelVisible;
    private bool _isContentVisible;
    private bool _pressed;
    private string? _appliedAutomationName;
    private MaterialNavigationItemLayout? _layout;
    private Path? _iconPath;
    private Path? _selectedIconPath;
    private MaterialNavigationBar? _bar;
    private MaterialMotion.SpringAnimation? _alphaSpring;
    private MaterialMotion.SpringAnimation? _sizeSpring;
    private double _indicatorAlpha;
    private double _indicatorSize;
    private bool _updating;
    private readonly SolidColorBrush _iconBrush = new(Colors.Transparent);
    private readonly SolidColorBrush _labelBrush = new(Colors.Transparent);
    private readonly MaterialSelectionColorAnimation _iconColor;
    private readonly MaterialSelectionColorAnimation _labelColor;
    private TextBlock? _label;
    private Panel? _labelHost;

    /// <summary>Source <c>DisabledAlpha</c> for <c>unselected*.copy(alpha = DisabledAlpha)</c>.</summary>
    private const double DisabledAlpha = 0.38;

    public MaterialNavigationBarItem()
    {
        _iconColor = new MaterialSelectionColorAnimation(this, color => _iconBrush.Color = color);
        _labelColor = new MaterialSelectionColorAnimation(this, color => _labelBrush.Color = color);
    }

    static MaterialNavigationBarItem()
    {
        IconProperty.Changed.AddClassHandler<MaterialNavigationBarItem>((item, _) => item.UpdateSelectedIcon());
        SelectedIconProperty.Changed.AddClassHandler<MaterialNavigationBarItem>((item, _) => item.UpdateSelectedIcon());
        AlwaysShowLabelProperty.Changed.AddClassHandler<MaterialNavigationBarItem>((item, _) => item.UpdateContentVisibility());
        ShowLabelProperty.Changed.AddClassHandler<MaterialNavigationBarItem>((item, _) => item.UpdateContentVisibility());
        IconPositionProperty.Changed.AddClassHandler<MaterialNavigationBarItem>((item, _) =>
        {
            item.UpdateVisualClasses();
            item.UpdateColorTargets(animate: false);
        });
    }

    /// <summary>Raised when the destination is activated; the owning bar forwards it.</summary>
    public event EventHandler? Invoked;

    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public Geometry? SelectedIcon { get => GetValue(SelectedIconProperty); set => SetValue(SelectedIconProperty, value); }
    public MaterialNavigationItemIconPosition IconPosition { get => GetValue(IconPositionProperty); set => SetValue(IconPositionProperty, value); }
    public bool AlwaysShowLabel { get => GetValue(AlwaysShowLabelProperty); set => SetValue(AlwaysShowLabelProperty, value); }
    public bool ShowLabel { get => GetValue(ShowLabelProperty); set => SetValue(ShowLabelProperty, value); }
    public MaterialNavigationBarBadge Badge { get => GetValue(BadgeProperty); set => SetValue(BadgeProperty, value); }
    public string? BadgeText { get => GetValue(BadgeTextProperty); set => SetValue(BadgeTextProperty, value); }
    public ICommand? Command { get => GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
    public object? CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }
    public Geometry? EffectiveSelectedIcon => _effectiveSelectedIcon;
    public string? LabelText => _labelText;
    public bool IsLabelVisible => _isLabelVisible;
    public bool IsContentVisible => _isContentVisible;

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

    /// <summary>Re-evaluates the position, label and bar layout inherited from the owning bar.</summary>
    internal void RefreshBarState()
    {
        _bar = ItemsControl.ItemsControlFromItemContainer(this) as MaterialNavigationBar;
        UpdateSelectedIcon();
        UpdateContentVisibility();
        UpdateVisualClasses();
        EnsureAnimations(snap: true);
        UpdateNavigationLayout();
        UpdateColorTargets(animate: false);
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
        UpdateSelectedIcon();
        UpdateContentVisibility();
        UpdateVisualClasses();
        UpdateIndicatorTargets(animate: false);
        UpdateNavigationLayout();
        UpdateColorTargets(animate: false);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _bar = ItemsControl.ItemsControlFromItemContainer(this) as MaterialNavigationBar;
        UpdateSelectedIcon();
        UpdateContentVisibility();
        UpdateVisualClasses();
        EnsureAnimations(snap: true);
        UpdateNavigationLayout();
        // The source animateColorAsState starts at its target on first composition: seed the tint
        // before any frame can interpolate from an unrelated colour.
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
            UpdateLabelText();
        else if (change.Property == BadgeProperty || change.Property == BadgeTextProperty)
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
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && IsEffectivelyEnabled)
        {
            _pressed = true;
            PseudoClasses.Set(":pressed", true);
            Focus(NavigationMethod.Pointer);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_pressed)
            return;

        _pressed = false;
        PseudoClasses.Set(":pressed", false);
        if (IsEffectivelyEnabled && new Rect(Bounds.Size).Contains(e.GetPosition(this)))
            Invoke();
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        _pressed = false;
        PseudoClasses.Set(":pressed", false);
        base.OnPointerCaptureLost(e);
    }

    private void UpdateLabelText()
    {
        // Only text content doubles as the accessible destination name; rich content keeps
        // whatever name the consumer supplied.
        var text = Content as string;
        if (text == _labelText)
            return;

        SetAndRaise(LabelTextProperty, ref _labelText, text);
        UpdateContentVisibility();

        var current = AutomationProperties.GetName(this);
        if (string.IsNullOrEmpty(current) || current == _appliedAutomationName)
        {
            _appliedAutomationName = text ?? string.Empty;
            AutomationProperties.SetName(this, _appliedAutomationName);
        }
    }

    private void UpdateContentVisibility()
    {
        var hasContent = !string.IsNullOrEmpty(_labelText) || Content is not null and not string;
        var show = ShowLabel && hasContent;
        var selected = IsSelected;
        var showText = show && !string.IsNullOrEmpty(_labelText) &&
            (AlwaysShowLabel || selected || _bar?.Kind == MaterialNavigationBarKind.Short);
        var showCustom = show && Content is not null and not string;
        PseudoClasses.Set(":no-label", !show);
        SetAndRaise(IsLabelVisibleProperty, ref _isLabelVisible, showText);
        SetAndRaise(IsContentVisibleProperty, ref _isContentVisible, showCustom);
        UpdateVisualClasses();
    }

    private void UpdateSelectedIcon()
    {
        SetAndRaise(EffectiveSelectedIconProperty, ref _effectiveSelectedIcon, SelectedIcon ?? Icon);
    }

    private void UpdateVisualClasses()
    {
        _bar ??= ItemsControl.ItemsControlFromItemContainer(this) as MaterialNavigationBar;
        var position = ResolveIconPosition();

        var top = position == MaterialNavigationItemIconPosition.Top;
        PseudoClasses.Set(":top", top);
        PseudoClasses.Set(":start", !top);
        PseudoClasses.Set(":short", _bar?.Kind == MaterialNavigationBarKind.Short);
        PseudoClasses.Set(":no-label", !ShowLabel);
        PseudoClasses.Set(":badge-small", Badge == MaterialNavigationBarBadge.Small);
        PseudoClasses.Set(":badge-large", Badge == MaterialNavigationBarBadge.Large);
        UpdateBadgeAutomation();
    }

    private void UpdateIndicatorTargets(bool animate)
    {
        var selected = IsSelected;
        var targetAlpha = selected ? 1d : 0d;
        var targetSize = targetAlpha;
        if (!animate || !this.IsAttachedToVisualTree())
        {
            _indicatorAlpha = targetAlpha;
            _indicatorSize = targetSize;
            UpdateNavigationLayout();
            return;
        }

        EnsureAnimations(snap: false);
        _alphaSpring?.Retarget(targetAlpha);
        _sizeSpring?.Retarget(targetSize);
    }

    private void EnsureAnimations(bool snap)
    {
        if (!this.IsAttachedToVisualTree())
            return;

        var shortBar = _bar?.Kind == MaterialNavigationBarKind.Short;
        var alphaSpring = shortBar ? MaterialSpring.SpatialDefault : MaterialSpring.EffectsDefault;
        var sizeSpring = shortBar ? MaterialSpring.SpatialDefault : MaterialSpring.SpatialFast;
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
    }

    private void DisposeAnimations()
    {
        _alphaSpring?.Dispose();
        _alphaSpring = null;
        _sizeSpring?.Dispose();
        _sizeSpring = null;
    }

    private void UpdateNavigationLayout()
    {
        if (_layout is null || _updating)
            return;

        _updating = true;
        try
        {
            _bar ??= ItemsControl.ItemsControlFromItemContainer(this) as MaterialNavigationBar;
            var shortBar = _bar?.Kind == MaterialNavigationBarKind.Short;
            var position = ResolveIconPosition();

            var hasContent = ShowLabel &&
                (!string.IsNullOrEmpty(_labelText) || Content is not null and not string);
            _layout.Kind = shortBar ? MaterialNavigationItemLayoutKind.ShortBar : MaterialNavigationItemLayoutKind.Bar;
            _layout.IconPosition = position;
            _layout.AlwaysShowLabel = AlwaysShowLabel || shortBar;
            _layout.HasLabel = hasContent;
            _layout.MinimumItemHeight = shortBar ? 64 : 80;
            _layout.IndicatorProgress = _indicatorSize;
            _layout.IndicatorAlpha = _indicatorAlpha;
            _layout.IconPositionProgress = position == MaterialNavigationItemIconPosition.Start ? 1 : 0;
            _layout.LabelOpacity = !hasContent
                ? 0
                : AlwaysShowLabel || shortBar
                    ? 1
                    : _indicatorAlpha;

            if (_iconPath is not null && _selectedIconPath is not null)
            {
                // The caller's SelectedIcon extension is a geometry crossfade only. Without a
                // distinct selected geometry the source has one icon, so it is painted once: the
                // tint channel (not this opacity) owns the colour change.
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
        _bar ??= ItemsControl.ItemsControlFromItemContainer(this) as MaterialNavigationBar;
        var position = IconPosition;
        if (position == MaterialNavigationItemIconPosition.Auto)
            position = _bar?.ResolveItemIconPosition() ?? MaterialNavigationItemIconPosition.Top;
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
    /// Retargets the icon and label colour channels. Classic bars use the independent
    /// <c>DefaultEffects</c> colour spring; the short bar passes tint values straight through
    /// (NavigationItem.kt 356-370), so a short-bar selection change paints on the next frame.
    /// </summary>
    private void UpdateColorTargets(bool animate)
    {
        var shortBar = _bar?.Kind == MaterialNavigationBarKind.Short;
        var animateColors = animate && !shortBar && this.IsAttachedToVisualTree();
        var enabled = IsEffectivelyEnabled;
        var selected = IsSelected;
        var position = ResolveIconPosition();
        _iconColor.Set(ResolveIconColor(enabled, selected), MaterialSpring.EffectsDefault, animateColors);
        _labelColor.Set(ResolveLabelColor(enabled, selected, position), MaterialSpring.EffectsDefault, animateColors);
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

    // The badge is decorative for pointer input but its count is meaningful to a screen reader.
    private void UpdateBadgeAutomation()
    {
        string? help = null;
        if (Badge == MaterialNavigationBarBadge.Large && !string.IsNullOrWhiteSpace(BadgeText))
        {
            var format = Resource("MaterialNavigationBarBadgeHelpText", "{0} new items");
            help = string.Format(CultureInfo.CurrentCulture, format, BadgeText);
        }
        else if (Badge == MaterialNavigationBarBadge.Small)
            help = Resource("MaterialNavigationBarBadgeDotText", "New items");

        AutomationProperties.SetHelpText(this, help ?? string.Empty);
    }

    private string Resource(string key, string fallback) =>
        this.TryFindResource(key, out var value) && value is string text ? text : fallback;
}