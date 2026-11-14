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
/// <para>Material 3 Expressive anatomy: five track/handle sizes, a 4-DIP handle (2 DIP
/// while pressed or focused), 6-DIP handle-to-track gaps and 4-DIP stop indicators.
/// Active stops use OnPrimary; inactive stops and icons use OnSecondaryContainer.</para>
/// <para>The native Track retains pointer capture, keyboard input and automation. Discrete
/// handles share the stop grid; endpoints reach the track ends. Avalonia mirrors horizontal
/// sliders for RTL. Vertical values increase from bottom to top unless explicitly reversed.</para>
/// <para>The optional value indicator is a non-interactive 48-by-44-DIP inverse-surface pill,
/// above a horizontal handle and beside a vertical handle. It lives in the overlay layer so
/// neither the slider bounds nor a scrolling parent clips it. Only the active range endpoint
/// displays a value. Inset icons are available on standard M, L and XL sliders.</para>
/// </remarks>
public sealed class MaterialSlider : AvaloniaObject
{
    private MaterialSlider() { }

    /// <summary>Whether pressing or keyboard-focusing a handle shows the value indicator pill.</summary>
    public static readonly AttachedProperty<bool> ShowValueIndicatorProperty =
        AvaloniaProperty.RegisterAttached<MaterialSlider, Control, bool>("ShowValueIndicator");

    /// <summary>
    /// Shows stop indicators at TickFrequency intervals, or at explicit Slider.Ticks when set.
    /// Enable snapping to make these the selectable values.
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

    internal const double StopInset = 6;

    internal static double HandlePosition(Slider owner, double length, double cross) =>
        Position(Fraction(owner.Value, owner.Minimum, owner.Maximum), length, owner.Orientation,
            owner.IsDirectionReversed, StopInset, IsDiscrete(owner));

    internal static string FormatValue(double value, Control owner)
    {
        var format = GetValueFormat(owner);
        return string.IsNullOrEmpty(format)
            ? value.ToString("G4", CultureInfo.CurrentCulture)
            : value.ToString(format, CultureInfo.CurrentCulture);
    }
}

/// <summary>Native Track with stop-aligned discrete handles and physical RTL arrow navigation.</summary>
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
            _owner.AddHandler(KeyDownEvent, OwnerKeyDown, RoutingStrategies.Tunnel);
            SubscribeTicks();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_owner is not null)
        {
            _owner.PropertyChanged -= OwnerChanged;
            _owner.RemoveHandler(KeyDownEvent, OwnerKeyDown);
        }
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

    private void OwnerKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || _owner is not { IsEffectivelyEnabled: true, Orientation: Orientation.Horizontal,
                FlowDirection: FlowDirection.RightToLeft } slider || e.KeyModifiers != KeyModifiers.None ||
            e.Key is not (Key.Left or Key.Right) || slider.SmallChange == 0)
            return;
        var increase = (e.Key == Key.Left) != slider.IsDirectionReversed;
        var value = slider.Value;
        var next = Math.Clamp(value + (increase ? slider.SmallChange : -slider.SmallChange), slider.Minimum, slider.Maximum);
        if (MaterialSlider.IsDiscrete(slider))
        {
            var lower = slider.Minimum;
            var upper = slider.Maximum;
            if (slider.Ticks is { Count: > 0 } ticks)
            {
                foreach (var tick in ticks)
                {
                    if (tick <= next && tick > lower) lower = tick;
                    if (tick >= next && tick < upper) upper = tick;
                }
                next = next - lower < upper - next ? lower : upper;
                if (next == value)
                {
                    next = increase ? slider.Maximum : slider.Minimum;
                    foreach (var tick in ticks)
                        if (increase ? tick > value && tick < next : tick < value && tick > next)
                            next = tick;
                }
            }
            else
            {
                var index = (next - slider.Minimum) / slider.TickFrequency;
                next = slider.Minimum + Math.Round(index) * slider.TickFrequency;
                if (next == value)
                {
                    index = (value - slider.Minimum) / slider.TickFrequency;
                    next = slider.Minimum + (increase ? Math.Floor(index) + 1 : Math.Ceiling(index) - 1) * slider.TickFrequency;
                }
            }
        }
        slider.SetCurrentValue(Slider.ValueProperty, Math.Clamp(next, slider.Minimum, slider.Maximum));
        e.Handled = true;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var result = base.ArrangeOverride(finalSize);
        if (TemplatedParent is not Slider slider || !MaterialSlider.IsDiscrete(slider) || Thumb is null)
            return result;
        var vertical = Orientation == Orientation.Vertical;
        var length = Math.Max(0, (vertical ? finalSize.Height : finalSize.Width) - 44);
        var center = MaterialSlider.Position(MaterialSlider.Fraction(slider.Value, slider.Minimum, slider.Maximum),
            length, slider.Orientation, slider.IsDirectionReversed, MaterialSlider.StopInset, true) + 22;
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
    public static readonly StyledProperty<Popup?> ValueLabelProperty =
        AvaloniaProperty.Register<MaterialSliderTrack, Popup?>(nameof(ValueLabel));
    public static readonly StyledProperty<IBrush?> DisabledActiveTickBrushProperty =
        AvaloniaProperty.Register<MaterialSliderTrack, IBrush?>(nameof(DisabledActiveTickBrush));
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
    public Popup? ValueLabel { get => GetValue(ValueLabelProperty); set => SetValue(ValueLabelProperty, value); }
    public IBrush? DisabledActiveTickBrush { get => GetValue(DisabledActiveTickBrushProperty); set => SetValue(DisabledActiveTickBrushProperty, value); }
    public IBrush? IconBrush { get => GetValue(IconBrushProperty); set => SetValue(IconBrushProperty, value); }
    public bool IsDragging { get => GetValue(IsDraggingProperty); set => SetValue(IsDraggingProperty, value); }

    static MaterialSliderTrack() => AffectsRender<MaterialSliderTrack>(OwnerProperty, ThumbProperty,
        ActiveBrushProperty, InactiveBrushProperty, DisabledBrushProperty, DisabledHandleBrushProperty,
        TickBrushProperty, ActiveTickBrushProperty, DisabledActiveTickBrushProperty, IconBrushProperty,
        FocusBrushProperty, IsEnabledProperty, IsDraggingProperty);

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
        ValueLabel?.SetCurrentValue(Popup.IsOpenProperty, false);
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
        if (Owner is not { IsEffectivelyEnabled: true, IsEffectivelyVisible: true })
        {
            _ownerPressed = false;
            QueueIndicator(null, null, false);
        }
        InvalidateVisual();
    }

    private void TicksChanged(object? sender, NotifyCollectionChangedEventArgs e) => InvalidateVisual();
    private void QueueIndicator(Rect? anchor, string? value, bool vertical)
    {
        if (_indicatorAnchor == anchor && _indicatorValue == value && _indicatorVertical == vertical)
            return;
        _indicatorAnchor = anchor;
        _indicatorValue = value;
        _indicatorVertical = vertical;
        if (_indicatorUpdateQueued)
            return;
        _indicatorUpdateQueued = true;
        // Popup layout must not mutate the visual tree during Render.
        Dispatcher.UIThread.Post(UpdateIndicator);
    }

    private void UpdateIndicator()
    {
        _indicatorUpdateQueued = false;
        if (ValueLabel is not { } popup)
            return;
        if (_indicatorAnchor is not { } anchor || Owner is not { IsEffectivelyEnabled: true, IsEffectivelyVisible: true })
        {
            popup.SetCurrentValue(Popup.IsOpenProperty, false);
            return;
        }
        if (popup.Child is Border { Child: TextBlock text })
            text.Text = _indicatorValue;
        popup.PlacementTarget = this;
        popup.PlacementRect = anchor;
        popup.Placement = _indicatorVertical ? PlacementMode.Left : PlacementMode.Top;
        popup.HorizontalOffset = _indicatorVertical ? -12 : 0;
        popup.VerticalOffset = _indicatorVertical ? 0 : -12;
        popup.SetCurrentValue(Popup.IsOpenProperty, true);
    }


    public override void Render(DrawingContext context)
    {
        base.Render(context);
        Rect? indicatorAnchor = null;
        string? indicatorText = null;
        if (Owner is not { } owner)
        {
            QueueIndicator(null, null, false);
            return;
        }
        var slider = owner as Slider;
        var range = owner as MaterialRangeSlider;
        if (slider is null && range is null)
        {
            QueueIndicator(null, null, false);
            return;
        }

        var vertical = slider?.Orientation == Orientation.Vertical;
        var length = vertical ? Bounds.Height : Bounds.Width;
        var cross = vertical ? Bounds.Width : Bounds.Height;
        if (!(length > 24) || !(cross > 8))
        {
            QueueIndicator(null, null, false);
            return;
        }

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

        // A 4-DIP handle gutter plus 4-DIP trailing space and the stop's 2-DIP radius.
        const double handleCoreWidth = 4;
        var trackStart = handleCoreWidth / 2;
        var trackEnd = length - handleCoreWidth / 2;
        var tickStart = Math.Min(trackStart + MaterialSlider.StopInset, trackEnd);
        var tickEnd = Math.Max(trackEnd - MaterialSlider.StopInset, tickStart);
        var rtl = range is not null && owner.FlowDirection == FlowDirection.RightToLeft;
        var activeTowardStart = vertical ? reversed : !reversed;

        double Directed(double fraction)
        {
            var directed = vertical
                ? reversed ? fraction : 1 - fraction
                : reversed ? 1 - fraction : fraction;
            // Standard sliders inherit Avalonia's RTL mirror. The range template is explicitly
            // LTR because it positions each endpoint in physical coordinates.
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

        // Stop colors follow their container roles, not the older Compose inverted palette.
        void TickDot(double position, bool onActive)
        {
            var brush = enabled ? onActive ? ActiveTickBrush : TickBrush
                : onActive ? DisabledActiveTickBrush : DisabledBrush;
            if (brush is null)
                return;
            var center = vertical ? new Point(crossCenter, position) : new Point(position, crossCenter);
            using (context.PushOpacity(enabled || onActive ? 1 : 0.38))
                context.DrawEllipse(brush, null, center, 2, 2);
        }

        void StopDot(double position)
        {
            var brush = enabled ? TickBrush : DisabledBrush;
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
            indicatorAnchor = vertical
                ? new Rect(crossCenter - handleLength / 2, position - 1, handleLength, 2)
                : new Rect(position - 1, crossCenter - handleLength / 2, 2, handleLength);
            indicatorText = text;
        }

        void Icon()
        {
            if (!MaterialSlider.GetShowIcon(owner) || IconBrush is null || slider is null || centered ||
                size is MaterialControlSize.ExtraSmall or MaterialControlSize.Small)
                return;
            var glyph = MaterialSlider.GetDisplayedIcon(owner);
            var iconSize = size == MaterialControlSize.ExtraLarge ? 32.0 : 24.0;
            var inset = size == MaterialControlSize.ExtraLarge ? 16.0 : 8.0;
            var position = MaterialSlider.HandlePosition(slider, length, cross);
            var activeLength = activeTowardStart
                ? position - trackStart - 8
                : trackEnd - position - 8;
            var onActive = activeLength >= iconSize + inset * 2;
            var along = activeTowardStart
                ? (onActive ? trackStart : position + 8) + inset
                : (onActive ? trackEnd : position - 8) - inset - iconSize;
            var x = vertical ? crossCenter - iconSize / 2 : along;
            var y = vertical ? along : crossCenter - iconSize / 2;
            var brush = enabled ? onActive ? IconBrush : TickBrush : DisabledBrush;
            var scale = iconSize / 24;
            using (context.PushOpacity(enabled ? 1 : 0.38))
            using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(x, y)))
                context.DrawGeometry(brush, null, glyph);
        }

        // Enumerate actual selectable values. Explicit ticks take precedence over frequency;
        // a non-divisible frequency must not silently become evenly redistributed steps.
        void Ticks(double? leftHandle, double? rightHandle, double leftGap, double rightGap,
            bool rangeMode)
        {
            if (!MaterialSlider.GetShowStops(owner) && (slider is null || slider.TickPlacement == TickPlacement.None))
                return;

            void DrawTick(double value)
            {
                if (!double.IsFinite(value) || value < minimum || value > maximum)
                    return;
                var position = TickPosition(MaterialSlider.Fraction(value, minimum, maximum));
                if (rangeMode || centered)
                {
                    if (value == minimum || value == maximum) return;
                }
                else if (value == maximum) return;
                if (leftHandle is { } left && Math.Abs(position - left) <= leftGap + 2) return;
                if (rightHandle is { } right && Math.Abs(position - right) <= rightGap + 2) return;
                if (centered && Math.Abs(position - length / 2) <= 4) return;
                var onActive = rangeMode
                    ? range!.LowerValue != range.UpperValue && position > leftHandle + leftGap && position < rightHandle - rightGap
                    : centered
                        ? position > Math.Min(leftHandle ?? 0, length / 2) && position < Math.Max(leftHandle ?? 0, length / 2)
                        : activeTowardStart ? position < leftHandle : position > leftHandle;
                TickDot(position, onActive);
            }

            DrawTick(minimum);
            if (slider?.Ticks is { Count: > 0 } ticks)
            {
                foreach (var tick in ticks)
                    if (tick != minimum && tick != maximum) DrawTick(tick);
                return;
            }
            var frequency = slider?.TickFrequency ?? range!.TickFrequency;
            if (!(frequency > 0) || !double.IsFinite(frequency)) return;
            var steps = (maximum / 2 - minimum / 2) / (frequency / 2);
            if (!double.IsFinite(steps)) return;
            var stride = Math.Max(1, Math.Ceiling(steps / Math.Max(1, length / 4)));
            for (double index = stride; index < steps; index += stride)
                DrawTick(minimum + index * frequency);
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
            QueueIndicator(indicatorAnchor, indicatorText, vertical);
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
            // A 4-DIP division marks zero; both sides also respect the handle gap at zero.
            var beforeCenter = Math.Min(center - 2, handlePosition - gap);
            var afterCenter = Math.Max(center + 2, handlePosition + gap);
            if (handlePosition < center)
            {
                if (handlePosition - gap > tickStart)
                    Segment(trackStart, handlePosition - gap, false);
                Segment(handlePosition + gap, center - 2, true);
                Segment(afterCenter, trackEnd, false);
            }
            else
            {
                Segment(trackStart, beforeCenter, false);
                Segment(center + 2, handlePosition - gap, true);
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
        if (enabled && MaterialSlider.GetShowValueIndicator(owner) && (pressed || focusVisible))
            Indicator(handlePosition, MaterialSlider.FormatValue(value, owner));
        Icon();
        QueueIndicator(indicatorAnchor, indicatorText, vertical);
    }
}
