using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Controls;

/// <summary>A horizontal slider with independently focusable lower and upper endpoints.</summary>
/// <remarks>Endpoints clamp at one another rather than exchanging identities. Values and bounds
/// must be finite; change sizes and tick frequency must also be nonnegative. A zero tick
/// frequency disables snapping. Endpoint names can be localized through the template thumbs.
/// Mouse input keeps the desktop-native press-jump-and-drag policy. Touch and pen wait for the
/// source pointer slop, decide between horizontal drag and vertical parent scroll, and never
/// change a value for a vertical movement.</remarks>
[TemplatePart("PART_LowerThumb", typeof(Thumb))]
[TemplatePart("PART_UpperThumb", typeof(Thumb))]
[TemplatePart("PART_TrackCanvas", typeof(Canvas))]
[TemplatePart("PART_TrackVisual", typeof(MaterialSliderTrack))]
public sealed class MaterialRangeSlider : TemplatedControl
{
    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<MaterialRangeSlider, double>(nameof(Minimum), 0,
            validate: double.IsFinite);
    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<MaterialRangeSlider, double>(nameof(Maximum), 100,
            validate: double.IsFinite, coerce: (o, v) => Math.Max(v, o.GetValue(MinimumProperty)));
    public static readonly StyledProperty<double> LowerValueProperty =
        AvaloniaProperty.Register<MaterialRangeSlider, double>(nameof(LowerValue), 0,
            defaultBindingMode: BindingMode.TwoWay, validate: double.IsFinite,
            coerce: (o, v) => ((MaterialRangeSlider)o).CoerceEndpoint(v, false));
    public static readonly StyledProperty<double> UpperValueProperty =
        AvaloniaProperty.Register<MaterialRangeSlider, double>(nameof(UpperValue), 100,
            defaultBindingMode: BindingMode.TwoWay, validate: double.IsFinite,
            coerce: (o, v) => ((MaterialRangeSlider)o).CoerceEndpoint(v, true));
    public static readonly StyledProperty<double> SmallChangeProperty =
        AvaloniaProperty.Register<MaterialRangeSlider, double>(nameof(SmallChange), 1,
            validate: IsNonnegative);
    public static readonly StyledProperty<double> LargeChangeProperty =
        AvaloniaProperty.Register<MaterialRangeSlider, double>(nameof(LargeChange), 10,
            validate: IsNonnegative);
    public static readonly StyledProperty<double> TickFrequencyProperty =
        AvaloniaProperty.Register<MaterialRangeSlider, double>(nameof(TickFrequency), 0,
            validate: IsNonnegative);
    public static readonly StyledProperty<bool> IsSnapToTickEnabledProperty =
        AvaloniaProperty.Register<MaterialRangeSlider, bool>(nameof(IsSnapToTickEnabled));

    /// <summary>Defines the <see cref="ValueChangeCompleted"/> routed event.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> ValueChangeCompletedEvent =
        RoutedEvent.Register<MaterialRangeSlider, RoutedEventArgs>(
            nameof(ValueChangeCompleted), RoutingStrategies.Bubble);

    private Canvas? _canvas;
    private MaterialSliderTrack? _trackVisual;
    private Thumb? _lowerThumb;
    private Thumb? _upperThumb;
    private Thumb? _activeThumb;
    // Source pointerSlop for touch/pen before a press becomes a drag. Mouse input keeps the
    // desktop-native immediate press policy and never reads this value.
    private const double TouchSlop = 8d;

    private IPointer? _capturedPointer;
    private double _grabOffset;
    private Point _pressPoint;
    private bool _dragStarted;
    private bool _verticalCancelled;
    private bool _pressNearCoincidentEndpoints;

    public double Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public double Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public double LowerValue { get => GetValue(LowerValueProperty); set => SetValue(LowerValueProperty, value); }
    public double UpperValue { get => GetValue(UpperValueProperty); set => SetValue(UpperValueProperty, value); }
    public double SmallChange { get => GetValue(SmallChangeProperty); set => SetValue(SmallChangeProperty, value); }
    public double LargeChange { get => GetValue(LargeChangeProperty); set => SetValue(LargeChangeProperty, value); }
    public double TickFrequency { get => GetValue(TickFrequencyProperty); set => SetValue(TickFrequencyProperty, value); }
    public bool IsSnapToTickEnabled { get => GetValue(IsSnapToTickEnabledProperty); set => SetValue(IsSnapToTickEnabledProperty, value); }

    /// <summary>Raised once when a user gesture or an automation action finishes changing an endpoint.</summary>
    /// <remarks>Raised after a pointer release that ends a tap or drag, after the key-up for a key
    /// that changes an endpoint, and after a range-value automation value that actually changes the
    /// endpoint. Bound, styled and programmatic updates do not raise it; a gesture cancelled by
    /// disable, pointer-capture loss, detach or a non-horizontal gesture does not raise it, unlike
    /// endpoint updates which may already have been observed.</remarks>
    public event EventHandler<RoutedEventArgs>? ValueChangeCompleted
    {
        add => AddHandler(ValueChangeCompletedEvent, value);
        remove => RemoveHandler(ValueChangeCompletedEvent, value);
    }

    public MaterialRangeSlider()
    {
        AddHandler(PointerPressedEvent, OnRangePointerPressed, RoutingStrategies.Tunnel);
        AddHandler(PointerMovedEvent, OnRangePointerMoved, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, OnRangePointerReleased, RoutingStrategies.Tunnel);
    }

    private static bool IsNonnegative(double value) => double.IsFinite(value) && value >= 0;

    private double CoerceEndpoint(double value, bool upper)
    {
        value = Snap(Math.Clamp(value, Minimum, Maximum));
        return upper
            ? Math.Max(value, Math.Clamp(LowerValue, Minimum, Maximum))
            : Math.Min(value, Math.Clamp(UpperValue, Minimum, Maximum));
    }

    private double Snap(double value)
    {
        if (!IsSnapToTickEnabled || TickFrequency <= 0 || Maximum <= Minimum)
            return value;
        var index = (value - Minimum) / TickFrequency;
        // A frequency smaller than the representable spacing cannot improve the value.
        if (!double.IsFinite(index))
            return value;
        var previous = Math.Clamp(Minimum + Math.Floor(index) * TickFrequency, Minimum, Maximum);
        var next = Math.Min(Maximum, previous + TickFrequency);
        // source snapValueToTick keeps the lower tick on an exact tie.
        return value - previous <= next - value ? previous : next;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        _capturedPointer?.Capture(null);
        if (_canvas is not null)
            _canvas.SizeChanged -= CanvasSizeChanged;
        base.OnApplyTemplate(e);
        _canvas = e.NameScope.Find<Canvas>("PART_TrackCanvas");
        _trackVisual = e.NameScope.Find<MaterialSliderTrack>("PART_TrackVisual");
        _lowerThumb = e.NameScope.Find<Thumb>("PART_LowerThumb");
        _upperThumb = e.NameScope.Find<Thumb>("PART_UpperThumb");
        if (_lowerThumb is MaterialRangeThumb lower)
            lower.SetOwner(this, false);
        if (_upperThumb is MaterialRangeThumb upper)
            upper.SetOwner(this, true);
        if (_canvas is not null)
            _canvas.SizeChanged += CanvasSizeChanged;
        UpdatePositions();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == MinimumProperty || change.Property == MaximumProperty)
        {
            CoerceValue(MaximumProperty);
            CoerceValue(UpperValueProperty);
            CoerceValue(LowerValueProperty);
        }
        else if (change.Property == TickFrequencyProperty || change.Property == IsSnapToTickEnabledProperty)
        {
            CoerceValue(UpperValueProperty);
            CoerceValue(LowerValueProperty);
        }
        if (change.Property == MinimumProperty || change.Property == MaximumProperty ||
            change.Property == LowerValueProperty || change.Property == UpperValueProperty ||
            change.Property == FlowDirectionProperty || change.Property == TickFrequencyProperty ||
            change.Property == IsSnapToTickEnabledProperty || change.Property == MaterialAssist.ControlSizeProperty)
            UpdatePositions();
        // A drag must stop when the slider or any ancestor is disabled, not only when the
        // slider's own IsEnabled changes.
        if (change.Property == InputElement.IsEffectivelyEnabledProperty && !IsEffectivelyEnabled)
            _capturedPointer?.Capture(null);
        (_lowerThumb as MaterialRangeThumb)?.NotifyRangeChanged(change);
        (_upperThumb as MaterialRangeThumb)?.NotifyRangeChanged(change);
    }

    private void CanvasSizeChanged(object? sender, SizeChangedEventArgs e) => UpdatePositions();

    private double Travel => Math.Max(0, (_canvas?.Bounds.Width ?? 0) - 4);

    /// <summary>
    /// Handle centers in track coordinates, including the 2-DIP thumb gutter at each end. The
    /// LowerValue endpoint is on the left in LTR and on the right in RTL, matching the source.
    /// Coincident endpoints are separated by ten DIP so both can be grabbed (an extension over
    /// the source, which stacks them); no active track is drawn between coincident endpoints.
    /// </summary>
    internal (double Lower, double Upper, bool Coincident) GetVisualPositions(double length)
    {
        var rtl = FlowDirection == Avalonia.Media.FlowDirection.RightToLeft;
        double At(double value)
        {
            var fraction = MaterialSlider.Fraction(value, Minimum, Maximum);
            return MaterialSlider.Position(fraction, length, Avalonia.Layout.Orientation.Horizontal, rtl,
                MaterialSlider.Corner(this, _canvas?.Bounds.Height ?? Bounds.Height),
                IsSnapToTickEnabled && TickFrequency > 0);
        }

        var lower = At(LowerValue);
        var upper = At(UpperValue);
        var coincident = LowerValue == UpperValue;
        if (Math.Abs(upper - lower) < 10)
        {
            var middle = (lower + upper) / 2;
            var left = Math.Clamp(middle - 5, 2, Math.Max(2, length - 12));
            // Keep the value order independent of the visual order.
            return rtl ? (left + 10, left, coincident) : (left, left + 10, coincident);
        }
        return (lower, upper, coincident);
    }

    private void UpdatePositions()
    {
        if (_canvas is null)
        {
            InvalidateTrack();
            return;
        }
        var (lower, upper, _) = GetVisualPositions(_canvas.Bounds.Width);
        // The thumb keeps at least the 48-DIP accessibility target; larger sizes use the
        // source's cross size so the painted handle is centered on a matching target.
        var target = Math.Max(48, _canvas.Bounds.Height);
        // Center even when the target is taller than the control: clamping the offset to zero
        // would push the 48-DIP target below the painted handle at the 44-DIP XS and S sizes.
        var top = (_canvas.Bounds.Height - target) / 2;
        if (_lowerThumb is not null)
        {
            _lowerThumb.Width = target;
            _lowerThumb.Height = target;
            Canvas.SetLeft(_lowerThumb, lower - target / 2);
            Canvas.SetTop(_lowerThumb, top);
        }
        if (_upperThumb is not null)
        {
            _upperThumb.Width = target;
            _upperThumb.Height = target;
            Canvas.SetLeft(_upperThumb, upper - target / 2);
            Canvas.SetTop(_upperThumb, top);
        }
        InvalidateTrack();
    }

    internal Thumb? LowerThumb => _lowerThumb;
    internal Thumb? UpperThumb => _upperThumb;
    internal bool IsDragging => _dragStarted;
    internal bool IsUpperEndpointActive => _activeThumb is not null && _activeThumb == _upperThumb;
    internal void InvalidateTrack() => _trackVisual?.InvalidateVisual();

    private void OnRangePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_canvas is null || _capturedPointer is not null || !IsEffectivelyEnabled)
            return;
        var current = e.GetCurrentPoint(this);
        if (!current.Properties.IsLeftButtonPressed &&
            e.Pointer.Type is not (PointerType.Touch or PointerType.Pen))
            return;
        var point = e.GetPosition(_canvas);
        var (lower, upper, coincident) = GetVisualPositions(_canvas.Bounds.Width);
        // Source rangeSliderPressDragModifier: nearest endpoint wins; an exact tie picks the
        // upper (end) endpoint. When the endpoints coincide, the press side chooses which one
        // separates: the start side (left in LTR, right in RTL) picks the lower endpoint.
        var rtl = FlowDirection == Avalonia.Media.FlowDirection.RightToLeft;
        bool useUpper;
        if (coincident)
        {
            var midpoint = (lower + upper) / 2;
            useUpper = rtl ? point.X <= midpoint : point.X >= midpoint;
        }
        else
        {
            var lowerDistance = Math.Abs(point.X - lower);
            var upperDistance = Math.Abs(point.X - upper);
            useUpper = upperDistance < lowerDistance || upperDistance == lowerDistance;
        }
        _activeThumb = useUpper ? _upperThumb : _lowerThumb;
        if (_activeThumb is null)
            return;
        var center = useUpper ? upper : lower;
        // Pressing anywhere inside the thumb's accessibility target grabs the handle without
        // jumping; the target is the source cross size, so the radius is not always 24 DIP.
        var grabRadius = double.IsFinite(_activeThumb.Width) && _activeThumb.Width > 0
            ? _activeThumb.Width / 2
            : 24;
        _grabOffset = Math.Abs(point.X - center) <= grabRadius ? point.X - center : 0;
        _activeThumb.Focus(NavigationMethod.Pointer);
        (_activeThumb as MaterialRangeThumb)?.SetDragging(true);
        _capturedPointer = e.Pointer;
        e.Pointer.Capture(this);
        InvalidateTrack();
        if (e.Pointer.Type == PointerType.Mouse)
        {
            // Desktop-native policy: the press jumps the endpoint immediately and every move
            // follows the pointer.
            _dragStarted = true;
            _verticalCancelled = false;
            e.PreventGestureRecognition();
            SetFromPoint(point.X);
        }
        else
        {
            // The source pointerInput waits for pointerSlop before a touch/pen press becomes a
            // drag. A vertical movement must remain a parent scroll with no value change.
            _dragStarted = false;
            _verticalCancelled = false;
            _pressPoint = point;
            _pressNearCoincidentEndpoints = coincident &&
                Math.Abs(point.X - lower) < TouchSlop && Math.Abs(point.X - upper) < TouchSlop;
        }
        e.Handled = true;
    }

    private void OnRangePointerMoved(object? sender, PointerEventArgs e)
    {
        if (_canvas is null || e.Pointer != _capturedPointer)
            return;
        var point = e.GetPosition(_canvas);
        if (e.Pointer.Type == PointerType.Mouse)
        {
            SetFromPoint(point.X);
            e.Handled = true;
            return;
        }
        if (_verticalCancelled)
            return;
        if (!_dragStarted)
        {
            var dx = point.X - _pressPoint.X;
            var dy = point.Y - _pressPoint.Y;
            if (dx * dx + dy * dy < TouchSlop * TouchSlop)
                return;
            if (Math.Abs(dy) >= Math.Abs(dx))
            {
                // Source rangeSliderPressDragModifier cancels the press for a vertical gesture
                // and leaves the pointer captured until it is released; no value is dispatched.
                _verticalCancelled = true;
                (_activeThumb as MaterialRangeThumb)?.SetDragging(false);
                InvalidateTrack();
                return;
            }
            // Source re-picks the captured endpoint from the drag direction when both endpoints
            // were under the press. The visual separation of coincident endpoints keeps the
            // grab offset relative to the handle that is now active.
            if (_pressNearCoincidentEndpoints)
            {
                var rtl = FlowDirection == Avalonia.Media.FlowDirection.RightToLeft;
                var useUpper = rtl ? dx < 0 : dx >= 0;
                var next = useUpper ? _upperThumb : _lowerThumb;
                if (next is not null && !ReferenceEquals(_activeThumb, next))
                {
                    (_activeThumb as MaterialRangeThumb)?.SetDragging(false);
                    _activeThumb = next;
                    var (lower, upper, _) = GetVisualPositions(_canvas.Bounds.Width);
                    _grabOffset = _pressPoint.X - (useUpper ? upper : lower);
                    (_activeThumb as MaterialRangeThumb)?.SetDragging(true);
                }
            }
            _dragStarted = true;
            e.PreventGestureRecognition();
        }
        SetFromPoint(point.X);
        e.Handled = true;
    }

    private void OnRangePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.Pointer != _capturedPointer)
            return;
        var mouse = e.Pointer.Type == PointerType.Mouse;
        var wasDrag = _dragStarted;
        var cancelled = _verticalCancelled;
        // A touch/pen press that never crossed the slop is a tap, matching the source's
        // press-release branch; the source applies the press position, not the release point.
        if (!mouse && !wasDrag && !cancelled)
            SetFromPoint(_pressPoint.X);
        e.Pointer.Capture(null);
        // Source invokes completion after a tap release and after a drag ends; capture loss
        // caused by disable, detach or another gesture never reaches here.
        if (!cancelled)
            RaiseValueChangeCompleted();
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        (_activeThumb as MaterialRangeThumb)?.SetDragging(false);
        _activeThumb = null;
        _capturedPointer = null;
        _dragStarted = false;
        _verticalCancelled = false;
        _pressNearCoincidentEndpoints = false;
        InvalidateTrack();
        base.OnPointerCaptureLost(e);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _capturedPointer?.Capture(null);
        base.OnDetachedFromVisualTree(e);
    }

    private void SetFromPoint(double x)
    {
        if (Travel <= 0 || !IsEffectivelyEnabled)
            return;
        var offset = Math.Clamp(x - 2 - _grabOffset, 0, Travel);
        var corner = IsSnapToTickEnabled && TickFrequency > 0
            ? Math.Min(MaterialSlider.Corner(this, _canvas?.Bounds.Height ?? Bounds.Height), Travel / 2)
            : 0;
        var visual = corner > 0 && Travel > 2 * corner
            ? Math.Clamp((offset - corner) / (Travel - 2 * corner), 0, 1)
            : offset / Travel;
        var fraction = FlowDirection == Avalonia.Media.FlowDirection.RightToLeft ? 1 - visual : visual;
        SetEndpoint(_activeThumb == _upperThumb, Minimum * (1 - fraction) + Maximum * fraction);
    }

    internal void SetEndpoint(bool upper, double value) =>
        SetCurrentValue(upper ? UpperValueProperty : LowerValueProperty, value);

    internal void RaiseValueChangeCompleted() =>
        RaiseEvent(new RoutedEventArgs(ValueChangeCompletedEvent, this));

    /// <summary>
    /// The source finishes a keyboard interaction on the key-up of the keys that change an
    /// endpoint. This control is horizontal-only, so those are the arrow, page, Home and End keys.
    /// </summary>
    internal void HandleThumbKeyUp(KeyEventArgs e)
    {
        if (e.Handled || e.KeyModifiers != KeyModifiers.None || !IsEffectivelyEnabled)
            return;
        if (e.Key is not (Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp or Key.PageDown))
            return;
        RaiseValueChangeCompleted();
        e.Handled = true;
    }

    internal void HandleThumbKey(bool upper, KeyEventArgs e)
    {
        if (e.Handled || e.KeyModifiers != KeyModifiers.None || !IsEffectivelyEnabled)
            return;
        var value = upper ? UpperValue : LowerValue;
        var delta = e.Key switch
        {
            Key.Left => FlowDirection == Avalonia.Media.FlowDirection.RightToLeft ? SmallChange : -SmallChange,
            Key.Right => FlowDirection == Avalonia.Media.FlowDirection.RightToLeft ? -SmallChange : SmallChange,
            Key.Down => -SmallChange,
            Key.Up => SmallChange,
            Key.PageDown => -LargeChange,
            Key.PageUp => LargeChange,
            _ => 0,
        };
        if (e.Key == Key.Home)
            value = upper ? LowerValue : Minimum;
        else if (e.Key == Key.End)
            value = upper ? Maximum : UpperValue;
        else if (delta != 0)
        {
            var next = Snap(Math.Clamp(value + delta, Minimum, Maximum));
            if (next == value && IsSnapToTickEnabled && TickFrequency > 0)
            {
                var index = (value - Minimum) / TickFrequency;
                next = Minimum + (delta > 0 ? Math.Floor(index) + 1 : Math.Ceiling(index) - 1) * TickFrequency;
            }
            value = Math.Clamp(next, Minimum, Maximum);
        }
        else
            return;
        SetEndpoint(upper, value);
        e.Handled = true;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new RangeSliderAutomationPeer(this);

    private sealed class RangeSliderAutomationPeer(MaterialRangeSlider owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
        protected override string GetClassNameCore() => nameof(MaterialRangeSlider);
    }
}

// Internal template part: each endpoint owns a real range-value automation provider and tab stop.
internal sealed class MaterialRangeThumb : Thumb
{
    private MaterialRangeSlider? _slider;
    private bool _upper;
    private EndpointAutomationPeer? _peer;

    public MaterialRangeThumb() => Focusable = true;

    internal void SetOwner(MaterialRangeSlider slider, bool upper)
    {
        _slider = slider;
        _upper = upper;
    }

    internal void SetDragging(bool dragging) => PseudoClasses.Set(":pressed", dragging);

    internal bool IsFocusVisible { get; private set; }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        IsFocusVisible = e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional;
        _slider?.InvalidateTrack();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        IsFocusVisible = false;
        _slider?.InvalidateTrack();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsFocusedProperty || change.Property == IsPointerOverProperty)
            _slider?.InvalidateTrack();
    }

    internal void NotifyRangeChanged(AvaloniaPropertyChangedEventArgs change) =>
        _peer?.NotifyRangeChanged(change);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        _slider?.HandleThumbKey(_upper, e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        _slider?.HandleThumbKeyUp(e);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => _slider is { } slider
        ? _peer = new EndpointAutomationPeer(this, slider, _upper)
        : base.OnCreateAutomationPeer();

    private sealed class EndpointAutomationPeer : ControlAutomationPeer, IRangeValueProvider
    {
        private readonly MaterialRangeSlider _slider;
        private readonly bool _upper;

        public EndpointAutomationPeer(MaterialRangeThumb thumb, MaterialRangeSlider slider, bool upper) : base(thumb)
        {
            _slider = slider;
            _upper = upper;
        }

        public bool IsReadOnly => !_slider.IsEffectivelyEnabled;
        public double Minimum => _upper ? _slider.LowerValue : _slider.Minimum;
        public double Maximum => _upper ? _slider.Maximum : _slider.UpperValue;
        public double Value => _upper ? _slider.UpperValue : _slider.LowerValue;
        public double SmallChange => _slider.SmallChange;
        public double LargeChange => _slider.LargeChange;

        public void SetValue(double value)
        {
            if (IsReadOnly)
                throw new InvalidOperationException("The range slider is disabled.");
            if (!double.IsFinite(value) || value < Minimum || value > Maximum)
                throw new ArgumentOutOfRangeException(nameof(value));
            var current = _upper ? _slider.UpperValue : _slider.LowerValue;
            _slider.SetEndpoint(_upper, value);
            // Source range thumb semantics reports success and invokes completion only when the
            // resolved value differs from the current one.
            if ((_upper ? _slider.UpperValue : _slider.LowerValue) != current)
                _slider.RaiseValueChangeCompleted();
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Slider;
        protected override string GetClassNameCore() => nameof(MaterialRangeSlider);

        public void NotifyRangeChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == (_upper ? MaterialRangeSlider.UpperValueProperty : MaterialRangeSlider.LowerValueProperty))
                RaisePropertyChangedEvent(RangeValuePatternIdentifiers.ValueProperty, e.OldValue, e.NewValue);
            if (e.Property == (_upper ? MaterialRangeSlider.LowerValueProperty : MaterialRangeSlider.MinimumProperty))
                RaisePropertyChangedEvent(RangeValuePatternIdentifiers.MinimumProperty, e.OldValue, e.NewValue);
            if (e.Property == (_upper ? MaterialRangeSlider.MaximumProperty : MaterialRangeSlider.UpperValueProperty))
                RaisePropertyChangedEvent(RangeValuePatternIdentifiers.MaximumProperty, e.OldValue, e.NewValue);
        }
    }
}
