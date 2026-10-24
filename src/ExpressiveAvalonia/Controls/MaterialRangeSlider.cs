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
/// frequency disables snapping. Endpoint names can be localized through the template thumbs.</remarks>
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

    private Canvas? _canvas;
    private MaterialSliderTrack? _trackVisual;
    private Thumb? _lowerThumb;
    private Thumb? _upperThumb;
    private Thumb? _activeThumb;
    private IPointer? _capturedPointer;
    private double _grabOffset;

    public double Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public double Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public double LowerValue { get => GetValue(LowerValueProperty); set => SetValue(LowerValueProperty, value); }
    public double UpperValue { get => GetValue(UpperValueProperty); set => SetValue(UpperValueProperty, value); }
    public double SmallChange { get => GetValue(SmallChangeProperty); set => SetValue(SmallChangeProperty, value); }
    public double LargeChange { get => GetValue(LargeChangeProperty); set => SetValue(LargeChangeProperty, value); }
    public double TickFrequency { get => GetValue(TickFrequencyProperty); set => SetValue(TickFrequencyProperty, value); }
    public bool IsSnapToTickEnabled { get => GetValue(IsSnapToTickEnabledProperty); set => SetValue(IsSnapToTickEnabledProperty, value); }

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
        return value - previous < next - value ? previous : next;
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
            change.Property == FlowDirectionProperty || change.Property == TickFrequencyProperty)
            UpdatePositions();
        if (change.Property == IsEnabledProperty && !IsEffectivelyEnabled)
            _capturedPointer?.Capture(null);
        (_lowerThumb as MaterialRangeThumb)?.NotifyRangeChanged(change);
        (_upperThumb as MaterialRangeThumb)?.NotifyRangeChanged(change);
    }

    private void CanvasSizeChanged(object? sender, SizeChangedEventArgs e) => UpdatePositions();

    private double Travel => Math.Max(0, (_canvas?.Bounds.Width ?? 0) - 4);

    private double Position(double value)
    {
        // Avalonia mirrors the visual tree for RTL, including pointer coordinates.
        // Halving first keeps the fraction finite for opposite extreme bounds.
        var fraction = Maximum > Minimum
            ? (value / 2 - Minimum / 2) / (Maximum / 2 - Minimum / 2)
            : 0;
        return fraction * Travel;
    }

    private void UpdatePositions()
    {
        var lower = Position(LowerValue);
        var upper = Position(UpperValue);
        if (_lowerThumb is not null)
            Canvas.SetLeft(_lowerThumb, lower - 22);
        if (_upperThumb is not null)
            Canvas.SetLeft(_upperThumb, upper - 22);
        InvalidateTrack();
    }

    internal bool IsLowerThumbFocused => _lowerThumb?.IsFocused == true;
    internal bool IsUpperThumbFocused => _upperThumb?.IsFocused == true;
    internal bool IsLowerThumbDragging => _activeThumb == _lowerThumb && _capturedPointer is not null;
    internal bool IsUpperThumbDragging => _activeThumb == _upperThumb && _capturedPointer is not null;
    internal void InvalidateTrack() => _trackVisual?.InvalidateVisual();

    private void OnRangePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_canvas is null || _capturedPointer is not null || !IsEffectivelyEnabled ||
            !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        var point = e.GetPosition(_canvas);
        var lower = Position(LowerValue) + 2;
        var upper = Position(UpperValue) + 2;
        // When endpoints coincide, the focused thumb wins; otherwise the nearest endpoint wins.
        var useUpper = Math.Abs(point.X - upper) < Math.Abs(point.X - lower) ||
            (Math.Abs(point.X - upper) == Math.Abs(point.X - lower) && _upperThumb?.IsFocused == true);
        _activeThumb = useUpper ? _upperThumb : _lowerThumb;
        if (_activeThumb is null)
            return;
        var center = useUpper ? upper : lower;
        _grabOffset = Math.Abs(point.X - center) <= 24 ? point.X - center : 0;
        _activeThumb.Focus(NavigationMethod.Pointer);
        (_activeThumb as MaterialRangeThumb)?.SetDragging(true);
        _capturedPointer = e.Pointer;
        e.Pointer.Capture(this);
        InvalidateTrack();
        e.PreventGestureRecognition();
        SetFromPoint(point.X);
        e.Handled = true;
    }

    private void OnRangePointerMoved(object? sender, PointerEventArgs e)
    {
        if (_canvas is not null && e.Pointer == _capturedPointer)
        {
            SetFromPoint(e.GetPosition(_canvas).X);
            e.Handled = true;
        }
    }

    private void OnRangePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.Pointer != _capturedPointer)
            return;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        (_activeThumb as MaterialRangeThumb)?.SetDragging(false);
        _activeThumb = null;
        _capturedPointer = null;
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
        if (Travel <= 0)
            return;
        var fraction = Math.Clamp((x - 2 - _grabOffset) / Travel, 0, 1);
        SetEndpoint(_activeThumb == _upperThumb, Minimum * (1 - fraction) + Maximum * fraction);
    }

    internal void SetEndpoint(bool upper, double value) =>
        SetCurrentValue(upper ? UpperValueProperty : LowerValueProperty, value);

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

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsFocusedProperty)
            _slider?.InvalidateTrack();
    }

    internal void NotifyRangeChanged(AvaloniaPropertyChangedEventArgs change) =>
        _peer?.NotifyRangeChanged(change);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        _slider?.HandleThumbKey(_upper, e);
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
            _slider.SetEndpoint(_upper, value);
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
