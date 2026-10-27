using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using ExpressiveAvalonia.Controls;
using Avalonia.Threading;

namespace ExpressiveAvalonia.Theming;

/// <summary>Presentation options shared by the standard and range sliders.</summary>
/// <remarks>
/// <para>Source mapping (Slider.kt / SliderDefaults / SliderTokens at 77b2199d): the default
/// XSmall anatomy is a 16-DIP track, a 44-DIP handle and a 4-DIP handle width with 2-DIP inside
/// corners, 6-DIP handle-to-track gap, 2-DIP stop/tick inset beyond the 8-DIP outer radius and a
/// 4-DIP stop/tick dot. Sizes reuse <see cref="MaterialAssist.ControlSize"/> so a slider matches
/// the surrounding controls; the source's XSmall maps to
/// <see cref="MaterialControlSize.ExtraSmall"/>. Values above XSmall are an extension: the
/// source has a single size, so the larger track/handle/radius triples are ExpressiveAvalonia
/// metrics, not another token set.</para>
/// <para>The native Track keeps dragging, keyboard, snapping and automation. A template Track
/// adapter moves the actual thumb target onto the source corner-inset grid for interior discrete
/// values; endpoints keep the 2-DIP gutter. Pointer-to-value input remains linear, as in the
/// source SliderState rawOffset mapping; only discrete layout is corner-inset. Tick colours follow
/// the source's inverted default: active-track ticks use SecondaryContainer, inactive-track ticks
/// use Primary; stop indicators use the active-track colour.</para>
/// <para><see cref="ShowValueIndicatorProperty"/> and <see cref="ShowIconProperty"/> are
/// extensions (the source samples compose their own value label/icons; the component itself has
/// no such properties). The indicator is the shared <see cref="MaterialLabel"/> plain-tooltip
/// bubble inside the slider template: MaterialSliderTrack anchors its zero-size host on the
/// painted handle, so the bubble follows the thumb at every size, orientation, RTL and range
/// endpoint. The icon is the source volume_up glyph shown only at Medium and above, moving to the
/// inactive track when the active track is too short.</para>
/// <para>Vertical follows the source topToBottom default through the theme's
/// IsDirectionReversed=True style (minimum at the top); local values can reverse it. Horizontal
/// RTL is mirrored as a whole by the theme because Avalonia's native Track is not
/// direction-aware, so the painted and native handles stay coincident; native Left/Right
/// keyboard steps remain LTR at the Track level.</para>
/// </remarks>
public sealed class MaterialSlider : AvaloniaObject
{
    private MaterialSlider() { }

    /// <summary>Whether pressing or keyboard-focusing a handle shows the value indicator pill.</summary>
    public static readonly AttachedProperty<bool> ShowValueIndicatorProperty =
        AvaloniaProperty.RegisterAttached<MaterialSlider, Control, bool>("ShowValueIndicator");

    /// <summary>
    /// Shows stop indicators at the values selected by TickFrequency.
    /// Enable snapping with the same frequency so every indicator is selectable.
    /// </summary>
    public static readonly AttachedProperty<bool> ShowStopsProperty =
        AvaloniaProperty.RegisterAttached<MaterialSlider, Control, bool>("ShowStops");

    /// <summary>
    /// Centers the fill on the track instead of filling from the minimum end. Use a symmetric
    /// range (for example -100 to 100) so the track midpoint is the zero value.
    /// </summary>
    public static readonly AttachedProperty<bool> IsCenteredProperty =
        AvaloniaProperty.RegisterAttached<MaterialSlider, Control, bool>("IsCentered");

    /// <summary>Shows an inset icon on standard sliders of size Medium, Large or ExtraLarge.</summary>
    public static readonly AttachedProperty<bool> ShowIconProperty =
        AvaloniaProperty.RegisterAttached<MaterialSlider, Control, bool>("ShowIcon");

    /// <summary>
    /// Geometry for the leading icon. When unset the source's default icon (volume_up, 24 DIP)
    /// is used. The glyph moves to the inactive track when the active track is too short.
    /// </summary>
    public static readonly AttachedProperty<Geometry?> IconProperty =
        AvaloniaProperty.RegisterAttached<MaterialSlider, Control, Geometry?>("Icon");

    /// <summary>Format string for the value indicator; defaults to the source's compact "G4".</summary>
    public static readonly AttachedProperty<string?> ValueFormatProperty =
        AvaloniaProperty.RegisterAttached<MaterialSlider, Control, string?>("ValueFormat");

    // SliderTokens. The source's default icon instance is volume_up (local-005657).
    private static readonly Lazy<Geometry> DefaultIcon = new(() => Geometry.Parse(
        "M14 20.725V18.675C15.5 18.2416 16.7083 17.4083 17.625 16.175C18.5417 14.9416 19 13.5416 " +
        "19 11.975C19 10.4083 18.5417 9.00831 17.625 7.77498C16.7083 6.54164 15.5 5.70831 14 " +
        "5.27498V3.22498C16.0667 3.69164 17.75 4.73748 19.05 6.36248C20.35 7.98748 21 9.85831 21 " +
        "11.975C21 14.0916 20.35 15.9625 19.05 17.5875C17.75 19.2125 16.0667 20.2583 14 20.725ZM3 " +
        "15V8.99998H7L12 3.99998V20L7 15H3ZM14 16V7.94998C14.7833 8.31664 15.3958 8.86664 15.8375 " +
        "9.59998C16.2792 10.3333 16.5 11.1333 16.5 12C16.5 12.85 16.2792 13.6375 15.8375 14.3625C15.3958 " +
        "15.0875 14.7833 15.6333 14 16ZM10 8.84998L7.85 11H5V13H7.85L10 15.15V8.84998Z"));

    public static bool GetShowValueIndicator(Control control) => control.GetValue(ShowValueIndicatorProperty);
    public static void SetShowValueIndicator(Control control, bool value) => control.SetValue(ShowValueIndicatorProperty, value);

    public static bool GetShowStops(Control control) => control.GetValue(ShowStopsProperty);
    public static void SetShowStops(Control control, bool value) => control.SetValue(ShowStopsProperty, value);

    public static bool GetIsCentered(Control control) => control.GetValue(IsCenteredProperty);
    public static void SetIsCentered(Control control, bool value) => control.SetValue(IsCenteredProperty, value);

    public static bool GetShowIcon(Control control) => control.GetValue(ShowIconProperty);
    public static void SetShowIcon(Control control, bool value) => control.SetValue(ShowIconProperty, value);

    public static Geometry? GetIcon(Control control) => control.GetValue(IconProperty);
    public static void SetIcon(Control control, Geometry? value) => control.SetValue(IconProperty, value);

    public static string? GetValueFormat(Control control) => control.GetValue(ValueFormatProperty);
    public static void SetValueFormat(Control control, string? value) => control.SetValue(ValueFormatProperty, value);

    internal static Geometry GetDisplayedIcon(Control control) => GetIcon(control) ?? DefaultIcon.Value;

    // TrackThickness / HandleLength / outer corner radius, in DIP.
    internal static (double Thickness, double Length, double Radius) Metrics(MaterialControlSize size) => size switch
    {
        MaterialControlSize.ExtraSmall => (16, 44, 8),
        MaterialControlSize.Small => (24, 44, 8),
        MaterialControlSize.Medium => (40, 52, 12),
        MaterialControlSize.Large => (56, 68, 16),
        MaterialControlSize.ExtraLarge => (96, 108, 28),
        _ => (40, 52, 12),
    };

    internal static double Fraction(double value, double minimum, double maximum) => maximum > minimum
        // Halving first keeps the fraction finite for opposite extreme bounds.
        ? Math.Clamp((value / 2 - minimum / 2) / (maximum / 2 - minimum / 2), 0, 1)
        : 0;

    /// <summary>
    /// Maps a value fraction to the handle center along the track. The source lays the track out
    /// inside a thumb-width gutter, so the center travels from half the 4-DIP handle (2 DIP) to
    /// length minus 2 DIP. Vertical follows Avalonia's Track direction (minimum at the bottom
    /// unless <see cref="Slider.IsDirectionReversed"/> is set), which keeps the painted handle
    /// aligned with the native pointer target.
    /// </summary>
    internal static double Position(double fraction, double length, Orientation orientation, bool reversed,
        double corner = 0, bool discrete = false)
    {
        var travel = Math.Max(0, length - 4);
        corner = Math.Min(corner, travel / 2);
        var offset = discrete && fraction > 0 && fraction < 1
            ? corner + fraction * (travel - 2 * corner)
            : fraction * travel;
        var invert = orientation == Orientation.Horizontal ? reversed : !reversed;
        return 2 + (invert ? travel - offset : offset);
    }

    internal static bool IsDiscrete(Slider slider) => slider.IsSnapToTickEnabled &&
        (slider.TickFrequency > 0 || slider.Ticks is { Count: > 0 });

    internal static double Corner(Control owner, double cross)
    {
        var (thickness, _, radius) = Metrics(MaterialAssist.GetControlSize(owner));
        return Math.Min(radius, Math.Min(thickness, Math.Max(4, cross - 2)) / 2);
    }

    internal static double HandlePosition(Slider owner, double length, double cross) =>
        Position(Fraction(owner.Value, owner.Minimum, owner.Maximum), length, owner.Orientation,
            owner.IsDirectionReversed, Corner(owner, cross), IsDiscrete(owner));

    internal static string FormatValue(double value, Control owner)
    {
        var format = GetValueFormat(owner);
        return string.IsNullOrEmpty(format)
            ? value.ToString("G4", CultureInfo.CurrentCulture)
            : value.ToString(format, CultureInfo.CurrentCulture);
    }
}

/// <summary>Native Track with the pinned interior discrete-step thumb placement.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class MaterialSliderNativeTrack : Track
{
    private Slider? _owner;
    private INotifyCollectionChanged? _ticks;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _owner = TemplatedParent as Slider;
        if (_owner is not null)
        {
            _owner.PropertyChanged += OwnerChanged;
            SubscribeTicks();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_owner is not null) _owner.PropertyChanged -= OwnerChanged;
        if (_ticks is not null) _ticks.CollectionChanged -= TicksChanged;
        _owner = null;
        _ticks = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void OwnerChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Slider.TicksProperty) SubscribeTicks();
        if (e.Property == Slider.IsSnapToTickEnabledProperty || e.Property == Slider.TickFrequencyProperty ||
            e.Property == Slider.TicksProperty || e.Property == MaterialAssist.ControlSizeProperty)
            InvalidateArrange();
    }

    private void SubscribeTicks()
    {
        if (_ticks is not null) _ticks.CollectionChanged -= TicksChanged;
        _ticks = _owner?.Ticks;
        if (_ticks is not null) _ticks.CollectionChanged += TicksChanged;
    }

    private void TicksChanged(object? sender, NotifyCollectionChangedEventArgs e) => InvalidateArrange();

    protected override Size ArrangeOverride(Size finalSize)
    {
        var result = base.ArrangeOverride(finalSize);
        if (TemplatedParent is not Slider slider || !MaterialSlider.IsDiscrete(slider) || Thumb is null)
            return result;
        var vertical = Orientation == Orientation.Vertical;
        var length = Math.Max(0, (vertical ? finalSize.Height : finalSize.Width) - 44);
        var cross = vertical ? finalSize.Width : finalSize.Height;
        var center = MaterialSlider.Position(MaterialSlider.Fraction(slider.Value, slider.Minimum, slider.Maximum),
            length, slider.Orientation, slider.IsDirectionReversed, MaterialSlider.Corner(slider, cross), true) + 22;
        var bounds = Thumb.Bounds;
        Thumb.Arrange(vertical
            ? new Rect(bounds.X, center - bounds.Height / 2, bounds.Width, bounds.Height)
            : new Rect(center - bounds.Width / 2, bounds.Y, bounds.Width, bounds.Height));
        return result;
    }
}

/// <summary>Template part of the slider themes; not part of the consumer API.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class MaterialSliderThumb : Thumb
{
    public static readonly StyledProperty<bool> IsDraggingProperty =
        AvaloniaProperty.Register<MaterialSliderThumb, bool>(nameof(IsDragging));

    public bool IsDragging { get => GetValue(IsDraggingProperty); private set => SetValue(IsDraggingProperty, value); }
    private Slider? _discreteOwner;
    private Point _lastOwnerPoint;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        _discreteOwner = TemplatedParent is Slider slider && MaterialSlider.IsDiscrete(slider) ? slider : null;
        if (_discreteOwner is not null) _lastOwnerPoint = e.GetPosition(_discreteOwner);
        base.OnPointerPressed(e);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if (_discreteOwner is null || !IsDragging)
        {
            base.OnPointerMoved(e);
            return;
        }

        // Track.AdjustDrag is internal and compensates only its linear layout. For the inset
        // grid, measure native DragDelta in the stationary owner instead of the relocated thumb.
        var point = e.GetPosition(_discreteOwner);
        var delta = point - _lastOwnerPoint;
        _lastOwnerPoint = point;
        RaiseEvent(new VectorEventArgs { RoutedEvent = DragDeltaEvent, Vector = delta });
    }

    protected override Type StyleKeyOverride => typeof(Thumb);

    protected override void OnDragStarted(Avalonia.Input.VectorEventArgs e)
    {
        IsDragging = true;
        base.OnDragStarted(e);
    }

    protected override void OnDragCompleted(Avalonia.Input.VectorEventArgs e)
    {
        IsDragging = false;
        _discreteOwner = null;
        base.OnDragCompleted(e);
    }
}

/// <summary>
/// Draws the slider anatomy: rounded track segments, stop indicators, the handle, the leading
/// icon and the value indicator pill. The native <see cref="Track"/> above it keeps pointer,
/// keyboard, snapping and automation behavior.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class MaterialSliderTrack : Control
{
    public static readonly StyledProperty<Control?> OwnerProperty =
        AvaloniaProperty.Register<MaterialSliderTrack, Control?>(nameof(Owner));
    public static readonly StyledProperty<Thumb?> ThumbProperty =
        AvaloniaProperty.Register<MaterialSliderTrack, Thumb?>(nameof(Thumb));
    public static readonly StyledProperty<IBrush?> ActiveBrushProperty =
        AvaloniaProperty.Register<MaterialSliderTrack, IBrush?>(nameof(ActiveBrush));
    public static readonly StyledProperty<IBrush?> InactiveBrushProperty =
        AvaloniaProperty.Register<MaterialSliderTrack, IBrush?>(nameof(InactiveBrush));
    public static readonly StyledProperty<IBrush?> DisabledBrushProperty =
        AvaloniaProperty.Register<MaterialSliderTrack, IBrush?>(nameof(DisabledBrush));
    public static readonly StyledProperty<IBrush?> DisabledHandleBrushProperty =
        AvaloniaProperty.Register<MaterialSliderTrack, IBrush?>(nameof(DisabledHandleBrush));
    public static readonly StyledProperty<IBrush?> TickBrushProperty =
        AvaloniaProperty.Register<MaterialSliderTrack, IBrush?>(nameof(TickBrush));
    public static readonly StyledProperty<IBrush?> ActiveTickBrushProperty =
        AvaloniaProperty.Register<MaterialSliderTrack, IBrush?>(nameof(ActiveTickBrush));
    public static readonly StyledProperty<MaterialLabel?> ValueLabelProperty =
        AvaloniaProperty.Register<MaterialSliderTrack, MaterialLabel?>(nameof(ValueLabel));
    public static readonly StyledProperty<IBrush?> IconBrushProperty =
        AvaloniaProperty.Register<MaterialSliderTrack, IBrush?>(nameof(IconBrush));
    public static readonly StyledProperty<IBrush?> FocusBrushProperty =
        AvaloniaProperty.Register<MaterialSliderTrack, IBrush?>(nameof(FocusBrush));
    public static readonly StyledProperty<bool> IsDraggingProperty =
        AvaloniaProperty.Register<MaterialSliderTrack, bool>(nameof(IsDragging));

    private readonly Pen _focusPen = new() { Thickness = 3 };
    private Control? _subscribedOwner;
    private Thumb? _subscribedThumb;
    private AvaloniaList<double>? _ticks;
    private bool _ownerPressed;
    private bool _ownerFocusVisible;
    private bool _indicatorUpdateQueued;
    private Rect? _indicatorAnchor;
    private string? _indicatorValue;
    private bool _indicatorVertical;

    public Control? Owner { get => GetValue(OwnerProperty); set => SetValue(OwnerProperty, value); }
    public Thumb? Thumb { get => GetValue(ThumbProperty); set => SetValue(ThumbProperty, value); }
    public IBrush? ActiveBrush { get => GetValue(ActiveBrushProperty); set => SetValue(ActiveBrushProperty, value); }
    public IBrush? InactiveBrush { get => GetValue(InactiveBrushProperty); set => SetValue(InactiveBrushProperty, value); }
    public IBrush? DisabledBrush { get => GetValue(DisabledBrushProperty); set => SetValue(DisabledBrushProperty, value); }
    public IBrush? DisabledHandleBrush { get => GetValue(DisabledHandleBrushProperty); set => SetValue(DisabledHandleBrushProperty, value); }
    public IBrush? TickBrush { get => GetValue(TickBrushProperty); set => SetValue(TickBrushProperty, value); }
    public IBrush? ActiveTickBrush { get => GetValue(ActiveTickBrushProperty); set => SetValue(ActiveTickBrushProperty, value); }
    public IBrush? FocusBrush { get => GetValue(FocusBrushProperty); set => SetValue(FocusBrushProperty, value); }
    public MaterialLabel? ValueLabel { get => GetValue(ValueLabelProperty); set => SetValue(ValueLabelProperty, value); }
    public IBrush? IconBrush { get => GetValue(IconBrushProperty); set => SetValue(IconBrushProperty, value); }
    public bool IsDragging { get => GetValue(IsDraggingProperty); set => SetValue(IsDraggingProperty, value); }

    static MaterialSliderTrack() => AffectsRender<MaterialSliderTrack>(OwnerProperty, ThumbProperty,
        ActiveBrushProperty, InactiveBrushProperty, DisabledBrushProperty, DisabledHandleBrushProperty,
        TickBrushProperty, ActiveTickBrushProperty, IconBrushProperty, FocusBrushProperty, IsEnabledProperty, IsDraggingProperty);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == OwnerProperty)
            SubscribeOwner();
        else if (change.Property == ThumbProperty)
            SubscribeThumb();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        SubscribeOwner();
        SubscribeThumb();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        UnsubscribeOwner();
        _indicatorAnchor = null;
        ValueLabel?.SetCurrentValue(MaterialLabel.IsPersistentProperty, false);
        if (_subscribedThumb is not null)
            _subscribedThumb.PropertyChanged -= ThumbChanged;
        _subscribedThumb = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void SubscribeOwner()
    {
        UnsubscribeOwner();
        _subscribedOwner = Owner;
        if (_subscribedOwner is not null)
        {
            _subscribedOwner.PropertyChanged += OwnerChanged;
            _subscribedOwner.AddHandler(InputElement.PointerPressedEvent, OwnerPointerPressed, RoutingStrategies.Tunnel);
            _subscribedOwner.AddHandler(InputElement.PointerReleasedEvent, OwnerPointerReleased, RoutingStrategies.Tunnel);
            _subscribedOwner.AddHandler(InputElement.PointerCaptureLostEvent, OwnerPointerCaptureLost, RoutingStrategies.Tunnel);
            _subscribedOwner.AddHandler(InputElement.GotFocusEvent, OwnerGotFocus, RoutingStrategies.Bubble);
            _subscribedOwner.AddHandler(InputElement.LostFocusEvent, OwnerLostFocus, RoutingStrategies.Bubble);
        }
        SubscribeTicks();
    }

    private void UnsubscribeOwner()
    {
        if (_subscribedOwner is not null)
        {
            _subscribedOwner.PropertyChanged -= OwnerChanged;
            _subscribedOwner.RemoveHandler(InputElement.PointerPressedEvent, OwnerPointerPressed);
            _subscribedOwner.RemoveHandler(InputElement.PointerReleasedEvent, OwnerPointerReleased);
            _subscribedOwner.RemoveHandler(InputElement.PointerCaptureLostEvent, OwnerPointerCaptureLost);
            _subscribedOwner.RemoveHandler(InputElement.GotFocusEvent, OwnerGotFocus);
            _subscribedOwner.RemoveHandler(InputElement.LostFocusEvent, OwnerLostFocus);
        }
        _subscribedOwner = null;
        _ownerPressed = false;
        _ownerFocusVisible = false;
        if (_ticks is not null)
            _ticks.CollectionChanged -= TicksChanged;
        _ticks = null;
    }

    private void OwnerPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(_subscribedOwner).Properties.IsLeftButtonPressed)
            return;
        _ownerPressed = true;
        InvalidateVisual();
    }

    private void OwnerPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_ownerPressed)
            return;
        _ownerPressed = false;
        InvalidateVisual();
    }

    private void OwnerPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (!_ownerPressed)
            return;
        _ownerPressed = false;
        InvalidateVisual();
    }

    private void OwnerGotFocus(object? sender, FocusChangedEventArgs e)
    {
        // Window activation restores focus with Unspecified, not keyboard navigation.
        _ownerFocusVisible = e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional;
        InvalidateVisual();
    }

    private void OwnerLostFocus(object? sender, FocusChangedEventArgs e)
    {
        _ownerFocusVisible = false;
        InvalidateVisual();
    }

    private void SubscribeThumb()
    {
        if (_subscribedThumb is not null)
            _subscribedThumb.PropertyChanged -= ThumbChanged;
        _subscribedThumb = Thumb;
        if (_subscribedThumb is not null)
            _subscribedThumb.PropertyChanged += ThumbChanged;
        InvalidateVisual();
    }

    private void ThumbChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == IsPointerOverProperty || e.Property == IsFocusedProperty)
            InvalidateVisual();
    }

    private void SubscribeTicks()
    {
        if (_ticks is not null)
            _ticks.CollectionChanged -= TicksChanged;
        _ticks = (_subscribedOwner as Slider)?.Ticks;
        if (_ticks is not null)
            _ticks.CollectionChanged += TicksChanged;
    }

    private void OwnerChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Slider.TicksProperty)
            SubscribeTicks();
        InvalidateVisual();
    }

    private void TicksChanged(object? sender, NotifyCollectionChangedEventArgs e) => InvalidateVisual();
    private void QueueIndicator(Rect? anchor, string? value, bool vertical)
    {
        _indicatorAnchor = anchor;
        _indicatorValue = value;
        _indicatorVertical = vertical;
        if (_indicatorUpdateQueued)
            return;
        _indicatorUpdateQueued = true;
        // Label layout must not mutate the visual tree during Render.
        Dispatcher.UIThread.Post(UpdateIndicator);
    }

    private void UpdateIndicator()
    {
        _indicatorUpdateQueued = false;
        var label = ValueLabel;
        if (label is null)
            return;
        if (_indicatorAnchor is not { } anchor || Owner?.IsEffectivelyEnabled != true)
        {
            // The label is presentational: without a visible handle interaction it stays hidden
            // instead of tracking pointer hover on its own zero-size anchor.
            label.SetCurrentValue(MaterialLabel.IsPersistentProperty, false);
            return;
        }
        label.SetCurrentValue(MaterialLabel.LabelProperty, _indicatorValue);
        label.SetCurrentValue(MaterialLabel.IsPersistentProperty, true);

        // MaterialLabel arranges its plain-tooltip bubble 4 DIP above a zero-size anchor. Put
        // that anchor on the thumb's leading edge so the bubble follows the thumb at every
        // size, orientation and RTL mirror (the anchor travels with the slider layout).
        var cross = _indicatorVertical ? Bounds.Width : Bounds.Height;
        var (_, handleLength, _) = MaterialSlider.Metrics(MaterialAssist.GetControlSize(Owner!));
        handleLength = Math.Min(handleLength, Math.Max(4, cross));
        var x = _indicatorVertical ? cross / 2 : anchor.X + anchor.Width / 2;
        var y = _indicatorVertical ? anchor.Y + anchor.Height / 2 - handleLength / 2 : cross / 2 - handleLength / 2;
        Canvas.SetLeft(label, x);
        Canvas.SetTop(label, y);
    }


    public override void Render(DrawingContext context)
    {
        base.Render(context);
        QueueIndicator(null, null, false);
        if (Owner is not { } owner)
            return;
        var slider = owner as Slider;
        var range = owner as MaterialRangeSlider;
        if (slider is null && range is null)
            return;

        var vertical = slider?.Orientation == Orientation.Vertical;
        var length = vertical ? Bounds.Height : Bounds.Width;
        var cross = vertical ? Bounds.Width : Bounds.Height;
        if (!(length > 24) || !(cross > 8))
            return;

        var size = MaterialAssist.GetControlSize(owner);
        var (thickness, handleLength, outerRadius) = MaterialSlider.Metrics(size);
        thickness = Math.Min(thickness, Math.Max(4, cross - 2));
        outerRadius = Math.Min(outerRadius, thickness / 2);
        handleLength = Math.Min(handleLength, Math.Max(4, cross));

        var crossCenter = cross / 2;
        var enabled = owner.IsEffectivelyEnabled;
        // Focus ring: keyboard focus only (pointer focus stays clean), and only while focus is
        // still inside the slider - a thumb may hold focus while the slider itself is unfocused.
        var focusVisible = _ownerFocusVisible && owner.IsKeyboardFocusWithin;
        var reversed = slider?.IsDirectionReversed == true;
        var centered = MaterialSlider.GetIsCentered(owner);
        var minimum = slider?.Minimum ?? range!.Minimum;
        var maximum = slider?.Maximum ?? range!.Maximum;
        if (!(maximum > minimum))
            return;

        // The source lays the thumb on a 4-DIP gutter around the track: the visual track starts at
        // half the handle width and the thumb centre travels from 2 to length-2. The stop/tick
        // grid is additionally inset by the track corner radius (cornerSize = trackHeight/2).
        const double handleCoreWidth = 4;
        var trackStart = handleCoreWidth / 2;
        var trackEnd = length - handleCoreWidth / 2;
        var tickStart = Math.Min(trackStart + outerRadius, trackEnd);
        var tickEnd = Math.Max(trackEnd - outerRadius, tickStart);
        var rtl = range is not null && owner.FlowDirection == FlowDirection.RightToLeft;
        var activeTowardStart = vertical ? reversed : !reversed;

        double Directed(double fraction)
        {
            var directed = vertical
                ? reversed ? fraction : 1 - fraction
                : reversed ? 1 - fraction : fraction;
            // The single Slider is mirrored as a whole for RTL by Sliders.axaml, so only the
            // range slider (which positions its own thumbs) mirrors here.
            if (rtl)
                directed = 1 - directed;
            return directed;
        }

        double TickPosition(double fraction) => tickStart + Directed(fraction) * (tickEnd - tickStart);

        void Segment(double start, double end, bool active)
        {
            var extent = end - start;
            if (!(extent > 0.01))
                return;
            // Only actual track endpoints get the outer radius. A handle-facing edge keeps the
            // source 2-DIP inside corner.
            var startRadius = start <= trackStart + 0.01 ? outerRadius : 2;
            var endRadius = end >= trackEnd - 0.01 ? outerRadius : 2;
            var drawingStart = start;
            var drawingEnd = end;
            if (start <= trackStart + 0.01)
                drawingEnd = Math.Max(end, start + startRadius + endRadius);
            else if (end >= trackEnd - 0.01)
                drawingStart = Math.Min(start, end - startRadius - endRadius);
            var rect = vertical
                ? new Rect(crossCenter - thickness / 2, start, thickness, extent)
                : new Rect(start, crossCenter - thickness / 2, extent, thickness);
            var drawingRect = vertical
                ? new Rect(crossCenter - thickness / 2, drawingStart, thickness, drawingEnd - drawingStart)
                : new Rect(drawingStart, crossCenter - thickness / 2, drawingEnd - drawingStart, thickness);
            var brush = enabled ? active ? ActiveBrush : InactiveBrush : DisabledBrush;
            if (brush is null)
                return;
            var rounded = vertical
                ? new RoundedRect(drawingRect, startRadius, startRadius, endRadius, endRadius)
                : new RoundedRect(drawingRect, startRadius, endRadius, endRadius, startRadius);
            using (context.PushClip(rect))
            using (context.PushOpacity(enabled ? 1 : active ? 0.38 : 0.12))
                context.DrawRectangle(brush, null, rounded);
        }

        // Source roles: ticks ON the active track use the inactive track colour and ticks on the
        // inactive track use the active track colour (inverted); stop indicators always use the
        // active track colour. Disabled ticks use the disabled active/inactive track alphas.
        void TickDot(double position, bool onActive)
        {
            var brush = enabled ? onActive ? ActiveTickBrush : TickBrush : DisabledBrush;
            if (brush is null)
                return;
            var center = vertical ? new Point(crossCenter, position) : new Point(position, crossCenter);
            using (context.PushOpacity(enabled ? 1 : onActive ? 0.12 : 0.38))
                context.DrawEllipse(brush, null, center, 2, 2);
        }

        void StopDot(double position)
        {
            var brush = enabled ? ActiveBrush : DisabledBrush;
            if (brush is null)
                return;
            var center = vertical ? new Point(crossCenter, position) : new Point(position, crossCenter);
            using (context.PushOpacity(enabled ? 1 : 0.38))
                context.DrawEllipse(brush, null, center, 2, 2);
        }

        // Source ActiveHandleLeadingSpace: 6 DIP, plus half the current handle width.
        double HandleGap(bool pressed, bool focused) =>
            6 + (enabled && (pressed || focused) ? 1 : 2);

        void Handle(double position, bool pressed, bool focused)
        {
            if (!enabled && pressed)
                pressed = false;
            var handleThickness = enabled && (pressed || focused) ? 2 : 4;
            var rect = vertical
                ? new Rect(crossCenter - handleLength / 2, position - handleThickness / 2, handleLength, handleThickness)
                : new Rect(position - handleThickness / 2, crossCenter - handleLength / 2, handleThickness, handleLength);
            var rounded = new RoundedRect(rect, handleThickness / 2);
            // SliderDefaults composites the disabled handle over Surface before painting it.
            // It must not blend over the track or the caller's actual backdrop a second time.
            var handleBrush = enabled ? ActiveBrush : DisabledHandleBrush;
            if (handleBrush is null)
                return;
            context.DrawRectangle(handleBrush, null, rounded);
            if (focused && enabled)
            {
                // Follow the handle silhouette, not a circular state layer.
                var ring = rect.Inflate(4.5);
                _focusPen.Brush = FocusBrush;
                context.DrawRectangle(null, _focusPen, new RoundedRect(ring, handleThickness / 2 + 4.5));
            }
        }

        void Indicator(double position, string text)
        {
            var anchor = vertical
                ? new Rect(0, position - 0.5, 1, 1)
                : new Rect(position - 0.5, 0, 1, 1);
            QueueIndicator(anchor, text, vertical);
        }

        void Icon()
        {
            if (!MaterialSlider.GetShowIcon(owner) || IconBrush is null || slider is null || centered ||
                size is MaterialControlSize.ExtraSmall or MaterialControlSize.Small)
                return;
            var glyph = MaterialSlider.GetDisplayedIcon(owner);
            var iconSize = size == MaterialControlSize.ExtraLarge ? 32.0 : 24.0;
            var inset = size == MaterialControlSize.ExtraLarge ? 8.0 : 6.0;
            var position = MaterialSlider.HandlePosition(slider, length, cross);
            var activeLength = activeTowardStart
                ? position - trackStart - 8
                : trackEnd - position - 8;
            var onActive = activeLength >= iconSize + inset * 2;
            var start = onActive ? activeTowardStart : !activeTowardStart;
            var along = start ? inset : length - inset - iconSize;
            var x = vertical ? crossCenter - iconSize / 2 : along;
            var y = vertical ? along : crossCenter - iconSize / 2;
            var brush = enabled ? onActive ? IconBrush : ActiveBrush : DisabledBrush;
            var scale = iconSize / 24;
            using (context.PushOpacity(enabled ? 1 : 0.38))
            using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(x, y)))
                context.DrawGeometry(brush, null, glyph);
        }

        // Stops/ticks use the corner-inset value grid; discrete thumb layout shares that grid
        // for interior values while first/last steps remain at the track endpoints.
        void Ticks(double? leftHandle, double? rightHandle, double leftGap, double rightGap,
            bool rangeMode)
        {
            var showStops = MaterialSlider.GetShowStops(owner);
            var frequency = slider?.TickFrequency ?? range!.TickFrequency;
            var showFrequency = showStops || (slider is not null && slider.TickPlacement != TickPlacement.None);
            if (!showFrequency || !(frequency > 0))
                return;
            var steps = (maximum / 2 - minimum / 2) / (frequency / 2);
            if (!double.IsFinite(steps) || steps <= 0)
                return;
            var count = (int)Math.Round(steps);
            if (count <= 0)
                return;
            // More than one tick per four DIP cannot be resolved; stride uniformly.
            var stride = Math.Max(1, (int)Math.Ceiling(count / Math.Max(1.0, length / 4)));
            for (var index = 0; index <= count; index += stride)
            {
                var fraction = (double)index / count;
                var position = TickPosition(fraction);
                // source skips ticks that fall on a stop indicator
                if (rangeMode || centered)
                {
                    if (position <= tickStart + 0.01 || position >= tickEnd - 0.01)
                        continue;
                }
                else
                {
                    var stopPosition = activeTowardStart ? tickEnd : tickStart;
                    if (Math.Abs(position - stopPosition) < 0.51)
                        continue;
                }
                if (leftHandle is { } left && Math.Abs(position - left) <= leftGap)
                    continue;
                if (rightHandle is { } right && Math.Abs(position - right) <= rightGap)
                    continue;
                if (centered && Math.Abs(position - length / 2) <= Math.Max(leftGap, rightGap))
                    continue;

                bool onActive;
                if (rangeMode)
                {
                    onActive = !(range!.LowerValue == range.UpperValue) &&
                        position > (leftHandle ?? 0) + leftGap &&
                        position < (rightHandle ?? 0) - rightGap;
                }
                else if (centered)
                {
                    var handle = leftHandle ?? length / 2;
                    onActive = handle < length / 2
                        ? position > handle + leftGap && position < length / 2
                        : position > length / 2 && position < handle - leftGap;
                }
                else
                    onActive = activeTowardStart ? position < (leftHandle ?? 0) : position > (leftHandle ?? 0);
                TickDot(position, onActive);
            }
        }

        if (range is not null)
        {
            var (lower, upper, coincident) = range.GetVisualPositions(length);
            var lowerThumb = range.LowerThumb;
            var upperThumb = range.UpperThumb;
            var dragging = range.IsDragging;
            var upperActive = range.IsUpperEndpointActive;
            var lowerFocused = lowerThumb is MaterialRangeThumb { IsFocusVisible: true } && !dragging;
            var upperFocused = upperThumb is MaterialRangeThumb { IsFocusVisible: true } && !dragging;
            var lowerGap = HandleGap(dragging && !upperActive, lowerFocused);
            var upperGap = HandleGap(dragging && upperActive, upperFocused);
            // Visual order: in RTL the LowerValue handle sits on the right.
            var lowerIsLeft = lower <= upper;
            var leftHandle = lowerIsLeft ? lower : upper;
            var rightHandle = lowerIsLeft ? upper : lower;
            var leftGap = lowerIsLeft ? lowerGap : upperGap;
            var rightGap = lowerIsLeft ? upperGap : lowerGap;
            var drawStartStop = leftHandle - leftGap > tickStart;
            var drawEndStop = rightHandle + rightGap < tickEnd;

            if (drawStartStop)
                Segment(trackStart, leftHandle - leftGap, false);
            if (!coincident)
                Segment(leftHandle + leftGap, rightHandle - rightGap, true);
            if (drawEndStop)
                Segment(rightHandle + rightGap, trackEnd, false);
            if (drawStartStop)
                StopDot(tickStart);
            if (drawEndStop)
                StopDot(tickEnd);
            Handle(lower, dragging && !upperActive, lowerFocused);
            Handle(upper, dragging && upperActive, upperFocused);
            if ((dragging || lowerFocused || upperFocused) && MaterialSlider.GetShowValueIndicator(owner))
            {
                var showUpper = dragging ? upperActive : upperFocused;
                Indicator(showUpper ? upper : lower,
                    MaterialSlider.FormatValue(showUpper ? range.UpperValue : range.LowerValue, owner));
            }

            Ticks(leftHandle, rightHandle, leftGap, rightGap, true);
            Icon();
            return;
        }

        var value = slider!.Value;
        var thumb = Thumb;
        var handlePosition = MaterialSlider.HandlePosition(slider, length, cross);
        var pressed = IsDragging || _ownerPressed || (thumb is MaterialSliderThumb { IsDragging: true });
        var gap = HandleGap(pressed, focusVisible);

        if (centered)
        {
            var center = length / 2;
            // Source: the inactive segments reach the centre; the active segment runs from the
            // centre to the thumb and only the thumb side gets the gap.
            if (handlePosition < center)
            {
                if (handlePosition - gap > tickStart)
                    Segment(trackStart, handlePosition - gap, false);
                Segment(handlePosition + gap, center, true);
                Segment(center, trackEnd, false);
            }
            else
            {
                Segment(trackStart, center, false);
                Segment(center, handlePosition - gap, true);
                if (handlePosition + gap < tickEnd)
                    Segment(handlePosition + gap, trackEnd, false);
            }
            if (handlePosition - gap > tickStart)
                StopDot(tickStart);
            if (handlePosition + gap < tickEnd)
                StopDot(tickEnd);
            Ticks(handlePosition, null, gap, gap, false);
        }
        else
        {
            if (activeTowardStart)
            {
                Segment(trackStart, handlePosition - gap, true);
                if (handlePosition + gap < tickEnd)
                {
                    Segment(handlePosition + gap, trackEnd, false);
                    StopDot(tickEnd);
                }
            }
            else
            {
                if (handlePosition - gap > tickStart)
                {
                    Segment(trackStart, handlePosition - gap, false);
                    StopDot(tickStart);
                }
                Segment(handlePosition + gap, trackEnd, true);
            }
            Ticks(handlePosition, null, gap, gap, false);
        }

        Handle(handlePosition, pressed, focusVisible);
        if (MaterialSlider.GetShowValueIndicator(owner) && (pressed || focusVisible))
            Indicator(handlePosition, MaterialSlider.FormatValue(value, owner));
        Icon();
    }
}
