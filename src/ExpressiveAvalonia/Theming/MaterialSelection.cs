using System;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.LogicalTree;
using ExpressiveAvalonia.Tokens;

namespace ExpressiveAvalonia.Theming;

/// <summary>Material options for the native selection controls styled by <c>Selection.axaml</c>.</summary>
/// <remarks>
/// Native controls retain their grouping, automation and pointer behavior. The radio theme also
/// supplies grouped keyboard navigation, while switch options provide the source anatomy.
/// </remarks>
public sealed class MaterialSelection : AvaloniaObject
{
    // Adds the built-in handle glyphs and widens the off handle to the source 24 units.
    private const string SwitchIconsClass = "switch-icons";

    private MaterialSelection() { }

    /// <summary>
    /// Draws the Material switch's built-in on/off glyphs inside the handle, as in the source
    /// "Icon=True" variants. The handle then keeps its 24-unit icon-bearing size in both positions
    /// instead of shrinking to 16 when off.
    /// </summary>
    /// <remarks>
    /// Built-in glyphs are drawn only while <see cref="ToggleSwitch.OnContent"/>,
    /// <see cref="ToggleSwitch.OffContent"/> and their templates are all null, so consumer content
    /// always wins; that content should stay within the 16-unit icon slot. The template reacts
    /// through the <c>switch-icons</c> class.
    /// </remarks>
    public static readonly AttachedProperty<bool> ShowSwitchIconsProperty =
        AvaloniaProperty.RegisterAttached<MaterialSelection, ToggleSwitch, bool>("ShowSwitchIcons");

    /// <summary>Enables Material keyboard navigation for themed radio buttons.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly AttachedProperty<bool> RadioKeyboardNavigationProperty =
        AvaloniaProperty.RegisterAttached<MaterialSelection, RadioButton, bool>("RadioKeyboardNavigation");

    public static bool GetRadioKeyboardNavigation(RadioButton control) => control.GetValue(RadioKeyboardNavigationProperty);
    public static void SetRadioKeyboardNavigation(RadioButton control, bool value) => control.SetValue(RadioKeyboardNavigationProperty, value);

    static MaterialSelection()
    {
        RadioKeyboardNavigationProperty.Changed.AddClassHandler<RadioButton>((control, args) =>
        {
            if (args.GetNewValue<bool>())
            {
                control.KeyDown += RadioKeyDown;
                control.GotFocus += RadioGotFocus;
                control.AttachedToVisualTree += RadioAttached;
                control.DetachedFromVisualTree += RadioDetached;
                control.PropertyChanged += RadioPropertyChanged;
                UpdateRadioGroup(control);
            }
            else
            {
                control.KeyDown -= RadioKeyDown;
                control.GotFocus -= RadioGotFocus;
                control.AttachedToVisualTree -= RadioAttached;
                control.DetachedFromVisualTree -= RadioDetached;
                control.PropertyChanged -= RadioPropertyChanged;
                control.ClearValue(KeyboardNavigation.IsTabStopProperty);
                UpdateRadioGroup(control);
            }
        });
        ShowSwitchIconsProperty.Changed.AddClassHandler<ToggleSwitch>((control, _) => UpdateClass(control));
        ToggleSwitch.OnContentProperty.Changed.AddClassHandler<ToggleSwitch>((control, _) => UpdateClass(control));
        ToggleSwitch.OffContentProperty.Changed.AddClassHandler<ToggleSwitch>((control, _) => UpdateClass(control));
        ToggleSwitch.OnContentTemplateProperty.Changed.AddClassHandler<ToggleSwitch>((control, _) => UpdateClass(control));
        ToggleSwitch.OffContentTemplateProperty.Changed.AddClassHandler<ToggleSwitch>((control, _) => UpdateClass(control));
    }

    public static bool GetShowSwitchIcons(ToggleSwitch control) => control.GetValue(ShowSwitchIconsProperty);

    public static void SetShowSwitchIcons(ToggleSwitch control, bool value) =>
        control.SetValue(ShowSwitchIconsProperty, value);

    private static RadioButton[] RadioGroup(RadioButton radio)
    {
        if (string.IsNullOrEmpty(radio.GroupName))
            return (radio.Parent as ILogical)?.LogicalChildren.OfType<RadioButton>()
                .Where(item => string.IsNullOrEmpty(item.GroupName) && GetRadioKeyboardNavigation(item)).ToArray() ?? Array.Empty<RadioButton>();
        return TopLevel.GetTopLevel(radio)?.GetVisualDescendants().OfType<RadioButton>()
            .Where(item => GetRadioKeyboardNavigation(item) && item.GroupName == radio.GroupName)
            .ToArray() ?? Array.Empty<RadioButton>();
    }

    private static void UpdateRadioGroup(RadioButton radio)
    {
        var group = RadioGroup(radio);
        RadioButton? entry = null;
        foreach (var item in group)
        {
            if (!item.IsEffectivelyEnabled || !item.IsEffectivelyVisible) continue;
            entry ??= item;
            if (item.IsChecked == true) { entry = item; break; }
        }
        foreach (var item in group)
            item.SetCurrentValue(KeyboardNavigation.IsTabStopProperty, item == entry);
    }

    private static void RadioAttached(object? sender, VisualTreeAttachmentEventArgs e) => UpdateRadioGroup((RadioButton)sender!);
    private static void RadioDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        var radio = (RadioButton)sender!;
        if (e.RootVisual is Visual root)
            foreach (var item in root.GetVisualDescendants().OfType<RadioButton>().Where(GetRadioKeyboardNavigation))
                UpdateRadioGroup(item);
        radio.ClearValue(KeyboardNavigation.IsTabStopProperty);
    }

    private static void RadioPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        var radio = (RadioButton)sender!;
        if (e.Property == RadioButton.GroupNameProperty && TopLevel.GetTopLevel(radio) is Visual root)
        {
            foreach (var item in root.GetVisualDescendants().OfType<RadioButton>().Where(GetRadioKeyboardNavigation))
                UpdateRadioGroup(item);
        }
        else if (e.Property == RadioButton.IsCheckedProperty ||
                 e.Property == InputElement.IsEffectivelyEnabledProperty || e.Property == Visual.IsVisibleProperty)
            UpdateRadioGroup(radio);
    }

    private static void RadioGotFocus(object? sender, FocusChangedEventArgs e)
    {
        if (sender is not RadioButton radio || e.Source != radio || e.NavigationMethod != NavigationMethod.Tab)
            return;
        var group = RadioGroup(radio);
        RadioButton? target = null;
        var backwards = (e.KeyModifiers & KeyModifiers.Shift) != 0;
        foreach (var item in group)
        {
            if (!item.IsEffectivelyEnabled || !item.IsEffectivelyVisible) continue;
            if (item.IsChecked == true) return;
            if (target is null || backwards) target = item;
        }
        if (target is null) return;
        if (target == radio) return;
        foreach (var item in group)
            item.SetCurrentValue(KeyboardNavigation.IsTabStopProperty, item == target);
        target.Focus(NavigationMethod.Tab, e.KeyModifiers);
    }

    private static void RadioKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.KeyModifiers != KeyModifiers.None || sender is not RadioButton radio || !radio.IsFocused)
            return;
        var direction = e.Key switch
        {
            Key.Down => 1, Key.Up => -1,
            Key.Right => radio.FlowDirection == FlowDirection.RightToLeft ? -1 : 1,
            Key.Left => radio.FlowDirection == FlowDirection.RightToLeft ? 1 : -1,
            _ => 0
        };
        if (direction == 0) return;
        var group = RadioGroup(radio);
        var index = Array.IndexOf(group, radio);
        if (index < 0) return;
        RadioButton? next = null;
        for (var step = 1; step <= group.Length; step++)
        {
            var candidate = group[(index + direction * step + group.Length) % group.Length];
            if (candidate.IsEffectivelyEnabled && candidate.IsEffectivelyVisible) { next = candidate; break; }
        }
        if (next is null) return;
        next.SetCurrentValue(RadioButton.IsCheckedProperty, true);
        next.Focus(NavigationMethod.Directional);
        e.Handled = true;
    }

    private static void UpdateClass(ToggleSwitch control)
    {
        var hasContent = control.OnContent is not null || control.OffContent is not null
                         || control.OnContentTemplate is not null || control.OffContentTemplate is not null;
        var showIcons = GetShowSwitchIcons(control) && !hasContent;
        SetClass(control, SwitchIconsClass, showIcons);
    }

    private static void SetClass(Control control, string name, bool present)
    {
        var has = control.Classes.Contains(name);
        if (present == has) return;
        if (present) control.Classes.Add(name);
        else control.Classes.Remove(name);
    }
}

/// <summary>
/// Retargetable Oklab color spring for one selection colour channel. The source picks the spec per
/// state (Checkbox.kt <c>colorAnimationSpecForState</c>, RadioButton.kt <c>animateColorAsState</c>),
/// so the spring can change between retargets; a disabled target snaps instead of animating.
/// </summary>
internal sealed class MaterialSelectionColorAnimation : IDisposable
{
    private readonly Control _owner;
    private readonly Action<Color> _apply;
    private MaterialColorAnimation? _animation;
    private Color _current;
    private bool _hasValue;

    internal MaterialSelectionColorAnimation(Control owner, Action<Color> apply)
    {
        _owner = owner;
        _apply = apply;
    }

    internal void Seed(Color value)
    {
        _current = value;
        _hasValue = true;
    }

    /// <summary>Moves the painted colour to <paramref name="target"/> with the source effect spring.</summary>
    internal void Set(Color target, MaterialSpring spring, bool animate)
    {
        if (!_hasValue || !animate)
        {
            _animation?.Dispose();
            _animation = null;
            _current = target;
            _hasValue = true;
            _apply(target);
            return;
        }

        _animation ??= new MaterialColorAnimation(_owner, _current, Apply, spring);
        _animation.Retarget(target, spring);
    }

    private void Apply(Color color)
    {
        _current = color;
        _apply(color);
    }

    /// <summary>Drops any running animation; the next target is applied as-is.</summary>
    internal void Reset()
    {
        _animation?.Dispose();
        _animation = null;
        _hasValue = false;
    }

    public void Dispose()
    {
        _animation?.Dispose();
        _animation = null;
        _hasValue = false;
    }
}

/// <summary>Retargetable spring for one DIP value of a selection control (radius, offset, size).</summary>
internal sealed class MaterialSelectionSpringAnimation : IDisposable
{
    private readonly Control _owner;
    private readonly Action<double> _apply;
    private readonly double _threshold;
    private MaterialMotion.SpringAnimation? _animation;
    private MaterialSpring _spring;
    private double _current;
    private bool _hasValue;

    internal MaterialSelectionSpringAnimation(Control owner, Action<double> apply, double threshold = 0.01)
    {
        _owner = owner;
        _apply = apply;
        _threshold = threshold;
    }

    internal void Seed(double value)
    {
        _current = value;
        _hasValue = true;
    }

    /// <summary>Moves the painted value to <paramref name="target"/>; snap applies it immediately.</summary>
    internal void Set(double target, MaterialSpring spring, bool animate)
    {
        if (!_hasValue || !animate)
        {
            _animation?.Dispose();
            _animation = null;
            _spring = spring;
            _current = target;
            _hasValue = true;
            _apply(target);
            return;
        }

        if (_animation is null || _spring != spring)
        {
            _animation?.Dispose();
            _spring = spring;
            _animation = MaterialMotion.Animate(_owner, _current, target, Apply, spring, _threshold);
            return;
        }
        _animation.Retarget(target);
    }

    private void Apply(double value)
    {
        _current = value;
        _apply(value);
    }

    /// <summary>Drops any running animation; the next target is applied as-is.</summary>
    internal void Reset()
    {
        _animation?.Dispose();
        _animation = null;
        _hasValue = false;
    }

    public void Dispose()
    {
        _animation?.Dispose();
        _animation = null;
        _hasValue = false;
    }
}

/// <summary>
/// Draws Checkbox.kt <c>drawBox</c> in the 20-unit checkbox space and springs the two source colour
/// channels. Fill and border are separate <c>animateColorAsState</c> calls in the source, so they
/// keep separate springs.
/// </summary>
/// <remarks>
/// <para>When the animated fill equals the animated border the source paints a single filled
/// rounded rectangle; otherwise it paints the fill inset by the full stroke width (radius
/// <c>radius - stroke</c>) and the border inset by half the stroke (radius
/// <c>radius - stroke / 2</c>). Keeping that rule here removes the previous Border thickness
/// transition: the 2-unit outline never changes width, only its colour and the inset fill do.</para>
/// <para>Colour target selection mirrors <c>colorAnimationSpecForState</c>: the style supplies
/// <see cref="ColorSpring"/> (FastEffects when the target state is Off, DefaultEffects otherwise).
/// While <see cref="AnimateColors"/> is false, which is how the disabled style disables the
/// source's <c>rememberUpdatedState</c> path, targets are applied immediately. The first target
/// after re-enabling also snaps, matching the fresh <c>animateColorAsState</c> state.</para>
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class MaterialSelectionBox : Control
{
    /// <summary>Target fill colour of the source <c>boxColor</c>.</summary>
    public static readonly StyledProperty<Color> BoxColorProperty =
        AvaloniaProperty.Register<MaterialSelectionBox, Color>(nameof(BoxColor));

    /// <summary>Target outline colour of the source <c>borderColor</c>.</summary>
    public static readonly StyledProperty<Color> BorderColorProperty =
        AvaloniaProperty.Register<MaterialSelectionBox, Color>(nameof(BorderColor));

    /// <summary>Source RadiusSize, in DIP.</summary>
    public static readonly StyledProperty<double> CornerRadiusProperty =
        AvaloniaProperty.Register<MaterialSelectionBox, double>(nameof(CornerRadius), 2,
            validate: value => double.IsFinite(value) && value >= 0);

    /// <summary>Source CheckboxDefaults.StrokeWidth, in DIP.</summary>
    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<MaterialSelectionBox, double>(nameof(StrokeThickness), 2,
            validate: value => double.IsFinite(value) && value >= 0);

    /// <summary>Effect spring for the current target state (Off -> FastEffects).</summary>
    public static readonly StyledProperty<MaterialSpring> ColorSpringProperty =
        AvaloniaProperty.Register<MaterialSelectionBox, MaterialSpring>(
            nameof(ColorSpring), MaterialSpring.EffectsDefault);

    /// <summary>False snaps colour changes, as the source does while disabled.</summary>
    public static readonly StyledProperty<bool> AnimateColorsProperty =
        AvaloniaProperty.Register<MaterialSelectionBox, bool>(nameof(AnimateColors), true);

    private readonly SolidColorBrush _fillBrush = new(Colors.Transparent);
    private readonly SolidColorBrush _borderBrush = new(Colors.Transparent);
    private readonly MaterialSelectionColorAnimation _fillAnimation;
    private readonly MaterialSelectionColorAnimation _borderAnimation;
    private Color _fill;
    private Color _border;
    private Color _fillTarget;
    private Color _borderTarget;
    private Pen? _borderPen;
    private double _penThickness = double.NaN;
    private bool _hasRendered;
    private bool _wasSnapped;

    public MaterialSelectionBox()
    {
        _fillAnimation = new MaterialSelectionColorAnimation(this, ApplyFill);
        _borderAnimation = new MaterialSelectionColorAnimation(this, ApplyBorder);
    }

    public Color BoxColor
    {
        get => GetValue(BoxColorProperty);
        set => SetValue(BoxColorProperty, value);
    }

    public Color BorderColor
    {
        get => GetValue(BorderColorProperty);
        set => SetValue(BorderColorProperty, value);
    }

    public double CornerRadius
    {
        get => GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public MaterialSpring ColorSpring
    {
        get => GetValue(ColorSpringProperty);
        set => SetValue(ColorSpringProperty, value);
    }

    public bool AnimateColors
    {
        get => GetValue(AnimateColorsProperty);
        set => SetValue(AnimateColorsProperty, value);
    }

    static MaterialSelectionBox()
    {
        AffectsRender<MaterialSelectionBox>(
            BoxColorProperty, BorderColorProperty, CornerRadiusProperty, StrokeThicknessProperty);
        BoxColorProperty.Changed.AddClassHandler<MaterialSelectionBox>(
            (box, args) => box.OnFillTarget(args.GetNewValue<Color>()));
        BorderColorProperty.Changed.AddClassHandler<MaterialSelectionBox>(
            (box, args) => box.OnBorderTarget(args.GetNewValue<Color>()));
        ColorSpringProperty.Changed.AddClassHandler<MaterialSelectionBox>(
            (box, _) => box.ReapplyColors());
        AnimateColorsProperty.Changed.AddClassHandler<MaterialSelectionBox>(
            (box, args) => box.OnAnimateColorsChanged(args.GetNewValue<bool>()));
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _fillAnimation.Reset();
        _borderAnimation.Reset();
        _hasRendered = false;
        _wasSnapped = false;
    }

    private bool ShouldAnimate() => _hasRendered && AnimateColors && !_wasSnapped;

    private void OnFillTarget(Color target)
    {
        _fillTarget = target;
        _fillAnimation.Set(target, ColorSpring, ShouldAnimate());
    }

    private void OnBorderTarget(Color target)
    {
        _borderTarget = target;
        _borderAnimation.Set(target, ColorSpring, ShouldAnimate());
    }

    private void OnAnimateColorsChanged(bool animate)
    {
        if (animate) return;
        // Checkbox.kt keeps the box/border state as rememberUpdatedState while disabled.
        _wasSnapped = true;
        _fillAnimation.Set(_fillTarget, ColorSpring, animate: false);
        _borderAnimation.Set(_borderTarget, ColorSpring, animate: false);
    }

    private void ReapplyColors()
    {
        var animate = ShouldAnimate();
        _fillAnimation.Set(_fillTarget, ColorSpring, animate);
        _borderAnimation.Set(_borderTarget, ColorSpring, animate);
    }

    private void ApplyFill(Color color)
    {
        _fill = color;
        _fillBrush.Color = color;
        InvalidateVisual();
    }

    private void ApplyBorder(Color color)
    {
        _border = color;
        _borderBrush.Color = color;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (!_hasRendered)
        {
            // First frame: take the styled targets as-is, as animateColorAsState starts at the
            // target on first composition instead of animating from the property default.
            _fill = BoxColor;
            _border = BorderColor;
            _fillBrush.Color = _fill;
            _borderBrush.Color = _border;
            _fillTarget = _fill;
            _borderTarget = _border;
            _fillAnimation.Seed(_fill);
            _borderAnimation.Seed(_border);
            _hasRendered = true;
        }
        if (AnimateColors)
            _wasSnapped = false;

        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (!(size > 0))
            return;
        var radius = Math.Max(0, CornerRadius);
        var stroke = Math.Max(0, StrokeThickness);

        if (_fill == _border || stroke <= 0)
        {
            context.DrawRectangle(_fillBrush, null, new Rect(0, 0, size, size), radius, radius);
            return;
        }

        // Checkbox.kt drawBox: inset fill first, then the stroke ring.
        if (size > stroke * 2)
        {
            var insetRadius = Math.Max(0, radius - stroke);
            context.DrawRectangle(_fillBrush, null,
                new Rect(stroke, stroke, size - stroke * 2, size - stroke * 2),
                insetRadius, insetRadius);
        }
        if (size > stroke)
        {
            if (_borderPen is null || _penThickness != stroke)
            {
                _borderPen = new Pen(_borderBrush, stroke);
                _penThickness = stroke;
            }
            var half = stroke / 2;
            var strokeRadius = Math.Max(0, radius - half);
            context.DrawRectangle(null, _borderPen,
                new Rect(half, half, size - stroke, size - stroke), strokeRadius, strokeRadius);
        }
    }
}

/// <summary>
/// Paints the checkbox checkmark of the pinned source: the same three-point path is partially
/// stroked by <see cref="Progress"/> and its cross/end points gravitate towards the horizontal
/// centre line by <see cref="Gravitation"/>, which is how the indeterminate mark is produced.
/// </summary>
/// <remarks>
/// <para>Checkbox.kt draws the mark with <c>drawCheck</c>: a path through 20% (left), 40% (cross)
/// and 80% (right) of the 20-unit box, stroked with square caps. <c>checkDrawFraction</c> selects
/// the drawn path length, and <c>crossCenterGravitation</c> moves the cross point (0.7 -> 0.5) and
/// the end point (0.3 -> 0.5) while the left point already sits on 0.5. At gravitation 1 the path
/// is the horizontal indeterminate bar.</para>
/// <para>Motion follows the source transition specs instead of a plain template transition. Entering
/// states use the interruptible <see cref="MaterialMotion"/> DefaultSpatial spring; a mark that
/// leaves the Off state snaps its gravitation (the source's <c>initialState == Off -> snap()</c>).
/// Leaving to Off is <c>snap(delayMillis = SnapAnimationDelay)</c>: the control keeps painting the
/// last silhouette for the source 100 ms constant and then snaps to the target, so there is no
/// reverse fade. The delay is a source constant, not a motion token, so reduced motion zeroes the
/// spring but keeps the delay. Retargeting during either phase preserves the painted position and
/// velocity.</para>
/// <para><see cref="StrokeColor"/> follows the source's <c>animatedCheckboxColor</c>
/// (<c>animateColorAsState(checkmarkColor, colorAnimationSpecForState(state))</c>): the style
/// supplies <see cref="ColorSpring"/>, FastEffects while the target state is Off and DefaultEffects
/// otherwise. Unlike the box/border roles, the source checkmark colour keeps animating while the
/// control is disabled.</para>
/// <para>The source measures the path in pixels and floors the stroke width; this control works in
/// DIP like the rest of the template, which keeps the 20-unit geometry crisp at every scale.</para>
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class MaterialCheckMark : Control
{
    // Checkbox.kt SnapAnimationDelay: leaving for Off holds the current silhouette, then snaps.
    private const int ExitHoldMilliseconds = 100;

    /// <summary>Fraction of the check path length to draw, 0..1.</summary>
    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<MaterialCheckMark, double>(nameof(Progress), validate: IsFraction);

    /// <summary>How far the cross and end points have gravitated to the centre line, 0..1.</summary>
    public static readonly StyledProperty<double> GravitationProperty =
        AvaloniaProperty.Register<MaterialCheckMark, double>(nameof(Gravitation), validate: IsFraction);

    /// <summary>Target mark colour of the source <c>checkmarkColor</c>.</summary>
    public static readonly StyledProperty<Color> StrokeColorProperty =
        AvaloniaProperty.Register<MaterialCheckMark, Color>(nameof(StrokeColor));

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<MaterialCheckMark, double>(nameof(StrokeThickness), 2,
            validate: value => double.IsFinite(value) && value >= 0);

    /// <summary>Effect spring for the current target state (Off -> FastEffects).</summary>
    public static readonly StyledProperty<MaterialSpring> ColorSpringProperty =
        AvaloniaProperty.Register<MaterialCheckMark, MaterialSpring>(
            nameof(ColorSpring), MaterialSpring.EffectsDefault);

    private readonly SolidColorBrush _colorBrush = new(Colors.Transparent);
    private readonly MaterialSelectionColorAnimation _colorAnimation;
    private Color _strokeColor;
    private Color _colorTarget;
    private Pen? _pen;
    private double _penThickness = double.NaN;
    private MaterialMotion.SpringAnimation? _progressSpring;
    private MaterialMotion.SpringAnimation? _gravitationSpring;
    private DispatcherTimer? _exitHold;
    private double _renderedProgress;
    private double _renderedGravitation;
    private bool _hasRendered;
    // A hold cancellation makes the mark Off-origin; gravitation must snap even when its target
    // change arrives later in the same style pass, so the intent survives until that change.
    private bool _snapNextGravitation;

    public MaterialCheckMark()
    {
        _colorAnimation = new MaterialSelectionColorAnimation(this, ApplyStrokeColor);
    }

    static MaterialCheckMark()
    {
        AffectsRender<MaterialCheckMark>(
            ProgressProperty, GravitationProperty, StrokeColorProperty, StrokeThicknessProperty);
        ProgressProperty.Changed.AddClassHandler<MaterialCheckMark>(
            (mark, args) => mark.OnProgressTarget(args.GetNewValue<double>()));
        GravitationProperty.Changed.AddClassHandler<MaterialCheckMark>(
            (mark, args) => mark.OnGravitationTarget(args.GetNewValue<double>()));
        StrokeColorProperty.Changed.AddClassHandler<MaterialCheckMark>(
            (mark, args) => mark.OnStrokeColorTarget(args.GetNewValue<Color>()));
        ColorSpringProperty.Changed.AddClassHandler<MaterialCheckMark>(
            (mark, _) => mark._colorAnimation.Set(mark._colorTarget, mark.ColorSpring, mark._hasRendered));
    }

    public double Progress
    {
        get => GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public double Gravitation
    {
        get => GetValue(GravitationProperty);
        set => SetValue(GravitationProperty, value);
    }

    public Color StrokeColor
    {
        get => GetValue(StrokeColorProperty);
        set => SetValue(StrokeColorProperty, value);
    }

    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    /// <summary>Effect spring applied to the next colour retarget.</summary>
    public MaterialSpring ColorSpring
    {
        get => GetValue(ColorSpringProperty);
        set => SetValue(ColorSpringProperty, value);
    }

    private static bool IsFraction(double value) => double.IsFinite(value) && value is >= 0 and <= 1;

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        CancelExitHold();
        StopSprings();
        _colorAnimation.Reset();
        // A re-templated mark starts from the state it is given instead of replaying a transition.
        _hasRendered = false;
        _snapNextGravitation = false;
    }

    private void OnStrokeColorTarget(Color target)
    {
        _colorTarget = target;
        _colorAnimation.Set(target, ColorSpring, animate: _hasRendered);
    }

    private void ApplyStrokeColor(Color color)
    {
        _strokeColor = color;
        _colorBrush.Color = color;
        InvalidateVisual();
    }

    private void OnProgressTarget(double target)
    {
        target = Math.Clamp(target, 0, 1);
        if (!_hasRendered)
        {
            _renderedProgress = target;
            InvalidateVisual();
            return;
        }

        // Checkbox.kt checkDrawFraction: targetState == Off -> snap(delayMillis = 100). Hold the
        // silhouette that is currently painted, then jump to the target when the delay elapses.
        if (target <= 0 && _renderedProgress > 0.0001)
        {
            BeginExitHold();
            return;
        }

        CancelExitHold(out var canceledHold);
        SpringProgress(target);
        if (canceledHold)
        {
            // The mark was still Off when the new target arrived, so the source picks the Off
            // transition specs: gravitation snaps (snap()) instead of morphing, and the pending
            // target that was frozen during the hold must be applied now.
            SnapGravitation();
            _snapNextGravitation = true;
        }
    }

    private void OnGravitationTarget(double target)
    {
        target = Math.Clamp(target, 0, 1);
        if (!_hasRendered)
        {
            _renderedGravitation = target;
            InvalidateVisual();
            return;
        }

        // While the exit delay is pending the painted silhouette is frozen; the snap after the
        // delay reads both targets.
        if (_exitHold is not null)
            return;

        var snap = _snapNextGravitation;
        _snapNextGravitation = false;

        // Checkbox.kt checkCenterGravitationShiftFraction: initialState == Off -> snap(). The mark
        // is still painted empty during that change, so the incoming gravity is applied directly.
        if (snap || _renderedProgress <= 0.0001)
        {
            SnapGravitation();
            return;
        }

        SpringGravitation(target);
    }

    private void BeginExitHold()
    {
        StopSprings();
        CancelExitHold();
        _exitHold = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ExitHoldMilliseconds) };
        _exitHold.Tick += OnExitHoldElapsed;
        _exitHold.Start();
    }

    private void OnExitHoldElapsed(object? sender, EventArgs e)
    {
        CancelExitHold();
        _renderedProgress = Math.Clamp(Progress, 0, 1);
        _renderedGravitation = Math.Clamp(Gravitation, 0, 1);
        InvalidateVisual();
    }

    private void CancelExitHold() => CancelExitHold(out _);

    private void CancelExitHold(out bool canceled)
    {
        canceled = _exitHold is not null;
        if (_exitHold is null)
            return;
        _exitHold.Stop();
        _exitHold.Tick -= OnExitHoldElapsed;
        _exitHold = null;
    }

    // The source's leaving spec is snap(delayMillis = 100): when the pending delay is interrupted
    // (a new state arrives), the mark is still Off, so gravity takes the Off -> snap() path.
    private void SnapGravitation()
    {
        _gravitationSpring?.Dispose();
        _gravitationSpring = null;
        _renderedGravitation = Math.Clamp(Gravitation, 0, 1);
        InvalidateVisual();
    }

    private void SpringProgress(double target)
    {
        if (_progressSpring is not null)
        {
            _progressSpring.Retarget(target);
            return;
        }
        _progressSpring = MaterialMotion.Animate(this, _renderedProgress, target,
            value =>
            {
                _renderedProgress = Math.Clamp(value, 0, 1);
                InvalidateVisual();
            },
            MaterialSpring.SpatialDefault);
    }

    private void SpringGravitation(double target)
    {
        if (_gravitationSpring is not null)
        {
            _gravitationSpring.Retarget(target);
            return;
        }
        _gravitationSpring = MaterialMotion.Animate(this, _renderedGravitation, target,
            value =>
            {
                _renderedGravitation = Math.Clamp(value, 0, 1);
                InvalidateVisual();
            },
            MaterialSpring.SpatialDefault);
    }

    private void StopSprings()
    {
        if (_progressSpring is not null)
        {
            _progressSpring.Dispose();
            _progressSpring = null;
        }
        if (_gravitationSpring is not null)
        {
            _gravitationSpring.Dispose();
            _gravitationSpring = null;
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        // A pending Off-origin snap only spans the style pass that produced it; any later
        // gravitation target change is a genuine morph.
        _snapNextGravitation = false;
        if (!_hasRendered)
        {
            // First frame: take the styled targets as-is, as the source's updateTransition does on
            // first composition instead of animating in from the default value.
            _renderedProgress = Math.Clamp(Progress, 0, 1);
            _renderedGravitation = Math.Clamp(Gravitation, 0, 1);
            _strokeColor = StrokeColor;
            _colorBrush.Color = _strokeColor;
            _colorTarget = _strokeColor;
            _colorAnimation.Seed(_strokeColor);
            _hasRendered = true;
        }
        if (StrokeThickness <= 0 || _strokeColor.A == 0)
            return;
        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (!(size > 0))
            return;
        var fraction = _renderedProgress;
        if (fraction <= 0)
            return;
        var gravity = _renderedGravitation;
        // Checkbox.kt drawCheck: the cross gravitates from (0.4, 0.7) to (0.5, 0.5),
        // while the right point moves onto the same horizontal centre line.
        var left = new Point(size * 0.2, size * 0.5);
        var cross = new Point(size * (0.4 + 0.1 * gravity), size * (0.7 - 0.2 * gravity));
        var right = new Point(size * 0.8, size * (0.3 + 0.2 * gravity));

        var first = Distance(left, cross);
        var second = Distance(cross, right);
        var target = (first + second) * fraction;
        if (!(target > 0.0001))
            return;

        var geometry = new StreamGeometry();
        using (var sink = geometry.Open())
        {
            sink.BeginFigure(left, false);
            if (target <= first)
            {
                sink.LineTo(Along(left, cross, first > 0 ? target / first : 1));
            }
            else
            {
                sink.LineTo(cross);
                var rest = second > 0 ? (target - first) / second : 1;
                sink.LineTo(Along(cross, right, Math.Clamp(rest, 0, 1)));
            }
            sink.EndFigure(false);
        }
        if (_pen is null || _penThickness != StrokeThickness)
        {
            _pen = new Pen(_colorBrush, StrokeThickness, null, PenLineCap.Square, PenLineJoin.Miter, 10);
            _penThickness = StrokeThickness;
        }
        context.DrawGeometry(null, _pen, geometry);
    }

    private static double Distance(Point from, Point to)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static Point Along(Point from, Point to, double fraction) =>
        new(from.X + (to.X - from.X) * fraction, from.Y + (to.Y - from.Y) * fraction);
}

/// <summary>
/// Draws RadioButton.kt's 20-unit glyph: a 2-unit ring at radius 10 - stroke/2 and the dot at
/// radius <see cref="DotRadius"/> - stroke/2, both in the same animated colour.
/// </summary>
/// <remarks>
/// <para>RadioButton.kt animates <c>dotRadius</c> (0 or <c>RadioButtonDotSize / 2</c> = 6) with the
/// FastSpatial spring, then draws it as <c>dotRadius - strokeWidth / 2</c>; there is no second dot
/// opacity animation. The colour comes from one <c>animateColorAsState(color, DefaultEffects)</c>
/// shared by the ring and the dot, and snaps while the control is disabled
/// (<c>rememberUpdatedState</c>). A dot whose animated radius is not positive is not drawn, so the
/// glyph has no separate visibility fade.</para>
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class MaterialRadioGlyph : Control
{
    /// <summary>Animated source colour shared by the ring and the dot.</summary>
    public static readonly StyledProperty<Color> ColorProperty =
        AvaloniaProperty.Register<MaterialRadioGlyph, Color>(nameof(Color));

    /// <summary>Source <c>dotRadius</c>: 6 when selected, 0 otherwise.</summary>
    public static readonly StyledProperty<double> DotRadiusProperty =
        AvaloniaProperty.Register<MaterialRadioGlyph, double>(nameof(DotRadius),
            validate: value => double.IsFinite(value) && value >= 0);

    /// <summary>Source RadioStrokeWidth, in DIP.</summary>
    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<MaterialRadioGlyph, double>(nameof(StrokeThickness), 2,
            validate: value => double.IsFinite(value) && value >= 0);

    /// <summary>Effect spring for the shared colour.</summary>
    public static readonly StyledProperty<MaterialSpring> ColorSpringProperty =
        AvaloniaProperty.Register<MaterialRadioGlyph, MaterialSpring>(
            nameof(ColorSpring), MaterialSpring.EffectsDefault);

    /// <summary>False snaps colour changes, as the source does while disabled.</summary>
    public static readonly StyledProperty<bool> AnimateColorsProperty =
        AvaloniaProperty.Register<MaterialRadioGlyph, bool>(nameof(AnimateColors), true);

    private readonly SolidColorBrush _brush = new(Colors.Transparent);
    private readonly MaterialSelectionColorAnimation _colorAnimation;
    private readonly MaterialSelectionSpringAnimation _dotAnimation;
    private Color _color;
    private Color _colorTarget;
    private double _renderedDotRadius;
    private Pen? _ringPen;
    private double _penThickness = double.NaN;
    private bool _hasRendered;
    private bool _wasSnapped;

    public MaterialRadioGlyph()
    {
        _colorAnimation = new MaterialSelectionColorAnimation(this, ApplyColor);
        _dotAnimation = new MaterialSelectionSpringAnimation(this, ApplyDotRadius);
    }

    static MaterialRadioGlyph()
    {
        AffectsRender<MaterialRadioGlyph>(
            ColorProperty, DotRadiusProperty, StrokeThicknessProperty);
        ColorProperty.Changed.AddClassHandler<MaterialRadioGlyph>(
            (glyph, args) => glyph.OnColorTarget(args.GetNewValue<Color>()));
        DotRadiusProperty.Changed.AddClassHandler<MaterialRadioGlyph>(
            (glyph, args) => glyph.OnDotTarget(args.GetNewValue<double>()));
        ColorSpringProperty.Changed.AddClassHandler<MaterialRadioGlyph>(
            (glyph, _) => glyph._colorAnimation.Set(
                glyph._colorTarget, glyph.ColorSpring, glyph.ShouldAnimateColor()));
        AnimateColorsProperty.Changed.AddClassHandler<MaterialRadioGlyph>(
            (glyph, args) => glyph.OnAnimateColorsChanged(args.GetNewValue<bool>()));
    }

    public Color Color
    {
        get => GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    public double DotRadius
    {
        get => GetValue(DotRadiusProperty);
        set => SetValue(DotRadiusProperty, value);
    }

    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public MaterialSpring ColorSpring
    {
        get => GetValue(ColorSpringProperty);
        set => SetValue(ColorSpringProperty, value);
    }

    public bool AnimateColors
    {
        get => GetValue(AnimateColorsProperty);
        set => SetValue(AnimateColorsProperty, value);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _colorAnimation.Reset();
        _dotAnimation.Reset();
        _hasRendered = false;
        _wasSnapped = false;
    }

    private bool ShouldAnimateColor() => _hasRendered && AnimateColors && !_wasSnapped;

    private void OnColorTarget(Color target)
    {
        _colorTarget = target;
        _colorAnimation.Set(target, ColorSpring, ShouldAnimateColor());
    }

    // RadioButton.kt animateDpAsState(dotRadius, FastSpatial); disabled does not gate the radius.
    private void OnDotTarget(double target) =>
        _dotAnimation.Set(target, MaterialSpring.SpatialFast, animate: _hasRendered);

    private void OnAnimateColorsChanged(bool animate)
    {
        if (animate) return;
        _wasSnapped = true;
        _colorAnimation.Set(_colorTarget, ColorSpring, animate: false);
    }

    private void ApplyColor(Color color)
    {
        _color = color;
        _brush.Color = color;
        InvalidateVisual();
    }

    private void ApplyDotRadius(double radius)
    {
        _renderedDotRadius = radius;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (!_hasRendered)
        {
            _color = Color;
            _brush.Color = _color;
            _renderedDotRadius = DotRadius;
            _colorTarget = _color;
            _colorAnimation.Seed(_color);
            _dotAnimation.Seed(_renderedDotRadius);
            _hasRendered = true;
        }
        if (AnimateColors)
            _wasSnapped = false;

        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (!(size > 0))
            return;
        var stroke = Math.Max(0, StrokeThickness);
        var centre = new Point(size / 2, size / 2);
        var half = stroke / 2;

        var ringRadius = size / 2 - half;
        if (ringRadius > 0 && stroke > 0)
        {
            if (_ringPen is null || _penThickness != stroke)
            {
                _ringPen = new Pen(_brush, stroke);
                _penThickness = stroke;
            }
            context.DrawEllipse(null, _ringPen, centre, ringRadius, ringRadius);
        }

        // RadioButton.kt: if (animatedDotRadius > 0) drawCircle(radius = dotRadius - stroke / 2).
        var dotRadius = _renderedDotRadius - half;
        if (_renderedDotRadius > 0 && dotRadius > 0)
            context.DrawEllipse(_brush, null, centre, dotRadius, dotRadius);
    }
}

/// <summary>
/// Template part <c>PART_SwitchKnob</c> for the native <see cref="ToggleSwitch"/>: it reads the
/// native drag position from <see cref="Canvas.LeftProperty"/> and paints the moving knobs at a
/// FastSpatial spring offset, and springs the thumb diameter (16 / 24 / 28).
/// </summary>
/// <remarks>
/// <para>Switch.kt <c>ThumbNode</c> animates the thumb size and offset with the FastSpatial spring
/// and snaps both with <c>SnapSpec</c> while pressed. The native ToggleSwitch keeps owning input:
/// it writes Canvas.Left while dragging and on toggle, so this panel treats that value as the
/// target. During a press (pointer or Space, via the native <see cref="Button.IsPressed"/> state)
/// the painted offset and size snap to the pressed geometry; after release they spring from the
/// last dragged position, which is the native equivalent of the source's press interaction.</para>
/// <para>Pressed geometry in the source is a 28-unit handle at offset 2 (unchecked) / 22 (checked).
/// The 32-unit carrier centres the handle, so Canvas.Left 0 / 20 already yields those exact left
/// edges for 16/24/28 sizes; only the diameter changes. The checked/icon/content sizes therefore
/// need no extra offset animation.</para>
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class MaterialSwitchKnobTrack : Canvas
{
    // SwitchTokens: UnselectedHandleWidth / SelectedHandleWidth / PressedHandleWidth.
    private const double UnselectedHandleSize = 16;
    private const double SelectedHandleSize = 24;
    private const double PressedHandleSize = 28;

    private readonly MaterialSelectionSpringAnimation _offsetAnimation;
    private readonly MaterialSelectionSpringAnimation _sizeAnimation;
    private ToggleSwitch? _toggle;
    private Panel? _knobs;
    private Border? _thumb;
    private double _offsetTarget;
    private double _paintedOffset;
    private bool _pointerDown;
    private bool _pressed;
    private bool _pointerReleaseInProgress;
    private bool _hasArranged;

    public MaterialSwitchKnobTrack()
    {
        _offsetAnimation = new MaterialSelectionSpringAnimation(this, ApplyOffset);
        _sizeAnimation = new MaterialSelectionSpringAnimation(this, ApplyThumbSize);
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        EnsureBound();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Unbind();
    }

    private void EnsureBound()
    {
        if (_toggle is null)
        {
            if (TemplatedParent is not ToggleSwitch owner)
                return;
            _toggle = owner;
            _pressed = owner.IsPressed;
            owner.PropertyChanged += OnTogglePropertyChanged;
            owner.AddHandler(InputElement.PointerPressedEvent, OnPointerPressedTunnel,
                RoutingStrategies.Tunnel, handledEventsToo: true);
            owner.AddHandler(InputElement.PointerReleasedEvent, OnPointerReleasedTunnel,
                RoutingStrategies.Tunnel, handledEventsToo: true);
        }

        if (_knobs is not null)
            return;

        foreach (var child in Children)
        {
            if (child is Panel panel && panel.Name == "PART_MovingKnobs")
            {
                _knobs = panel;
                break;
            }
        }
        if (_knobs is null)
            return;

        foreach (var child in _knobs.Children)
        {
            if (child is Border border && border.Name == "Thumb")
            {
                _thumb = border;
                break;
            }
        }

        _knobs.PropertyChanged += OnKnobsPropertyChanged;
        _offsetTarget = ReadOffsetTarget();
        _offsetAnimation.Set(_offsetTarget, MaterialSpring.SpatialFast, animate: false);
        UpdateSize(animate: false);
    }

    private void Unbind()
    {
        if (_toggle is not null)
        {
            _toggle.PropertyChanged -= OnTogglePropertyChanged;
            _toggle.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressedTunnel);
            _toggle.RemoveHandler(InputElement.PointerReleasedEvent, OnPointerReleasedTunnel);
            _toggle = null;
        }
        if (_knobs is not null)
        {
            _knobs.PropertyChanged -= OnKnobsPropertyChanged;
            _knobs = null;
        }
        _thumb = null;
        _pointerDown = false;
        _pressed = false;
        _pointerReleaseInProgress = false;
        _hasArranged = false;
        _paintedOffset = 0;
        _offsetAnimation.Reset();
        _sizeAnimation.Reset();
    }

    private double ReadOffsetTarget()
    {
        if (_knobs is null)
            return 0;
        var left = Canvas.GetLeft(_knobs);
        return double.IsFinite(left) ? left : 0;
    }

    private void OnKnobsPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != Canvas.LeftProperty)
            return;
        var target = ReadOffsetTarget();
        _offsetTarget = target;
        // Native drag writes every pointer move; those must follow the pointer without lag.
        // A pressed state outside the pointer-release phase (Space activation) snaps the offset,
        // as Switch.kt does with SnapSpec; release targets spring.
        _offsetAnimation.Set(target, MaterialSpring.SpatialFast,
            animate: _hasArranged && !_pointerDown && (!_pressed || _pointerReleaseInProgress));
    }

    private void OnTogglePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ToggleSwitch.IsPressedProperty)
        {
            // Button.IsPressed covers pointer presses and Space, so the pressed handle geometry
            // follows the same source press interaction for keyboard activation. It also turns
            // false on capture loss, focus loss and disable, ending a drag with no release event.
            _pressed = e.GetNewValue<bool>();
            if (_pressed)
            {
                _pointerReleaseInProgress = false;
            }
            else
            {
                _pointerDown = false;
                _pointerReleaseInProgress = false;
            }
            UpdateSize(animate: _hasArranged && !_pointerDown && !_pressed);
            return;
        }
        if (e.Property == ToggleSwitch.IsCheckedProperty
            || e.Property == ToggleSwitch.OnContentProperty
            || e.Property == ToggleSwitch.OffContentProperty
            || e.Property == ToggleSwitch.OnContentTemplateProperty
            || e.Property == ToggleSwitch.OffContentTemplateProperty
            || e.Property == MaterialSelection.ShowSwitchIconsProperty)
        {
            UpdateSize(animate: _hasArranged && !_pointerDown && !_pressed);
        }
    }

    private void OnPointerPressedTunnel(object? sender, PointerPressedEventArgs e)
    {
        EnsureBound();
        if (_pointerDown)
            return;
        _pointerDown = true;
        _pointerReleaseInProgress = false;
        // Switch.kt: SnapSpec while pressed, including the inset pressed offset.
        _offsetAnimation.Set(_offsetTarget, MaterialSpring.SpatialFast, animate: false);
        UpdateSize(animate: false);
    }

    private void OnPointerReleasedTunnel(object? sender, PointerReleasedEventArgs e)
    {
        if (!_pointerDown)
            return;
        // Runs before the native release handler: the following Canvas.Left/IsChecked changes are
        // release targets and must animate with the FastSpatial spring. Button.IsPressed turns
        // false after the native release, which starts the thumb-size release spring.
        _pointerDown = false;
        _pointerReleaseInProgress = true;
        UpdateSize(animate: _hasArranged && !_pressed);
    }

    private double SizeTarget()
    {
        // Switch.kt ThumbNode: isPressed wins over the checked/content sizes.
        if (_pointerDown || _pressed)
            return PressedHandleSize;
        if (_toggle is null)
            return UnselectedHandleSize;
        if (_toggle.IsChecked == true)
            return SelectedHandleSize;

        var hasContent = _toggle.OnContent is not null || _toggle.OffContent is not null
                         || _toggle.OnContentTemplate is not null || _toggle.OffContentTemplate is not null;
        if (hasContent || MaterialSelection.GetShowSwitchIcons(_toggle))
            return SelectedHandleSize;
        return UnselectedHandleSize;
    }

    private void UpdateSize(bool animate) =>
        _sizeAnimation.Set(SizeTarget(), MaterialSpring.SpatialFast, animate);

    private void ApplyOffset(double value)
    {
        _paintedOffset = value;
        InvalidateArrange();
    }

    private void ApplyThumbSize(double value)
    {
        if (_thumb is null)
            return;
        // The source measures one square handle; the icon slot stays centred in it.
        _thumb.Width = value;
        _thumb.Height = value;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        EnsureBound();
        _hasArranged = true;
        foreach (var child in Children)
        {
            var desired = child.DesiredSize;
            child.Arrange(new Rect(_paintedOffset, 0, desired.Width, finalSize.Height));
        }
        return finalSize;
    }
}
