using System;
using System.ComponentModel;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using ExpressiveAvalonia.Controls;

namespace ExpressiveAvalonia.Theming;

/// <summary>Template state propagated from the native slider to its thumb.</summary>
public sealed class MaterialSlider : AvaloniaObject
{
    public static readonly AttachedProperty<bool> IsOwnerFocusedProperty = AvaloniaProperty.RegisterAttached<MaterialSlider, Thumb, bool>("IsOwnerFocused");
    public static bool GetIsOwnerFocused(Thumb thumb) => thumb.GetValue(IsOwnerFocusedProperty);
    public static void SetIsOwnerFocused(Thumb thumb, bool value) => thumb.SetValue(IsOwnerFocusedProperty, value);
}

/// <summary>Template part of the slider themes; not part of the consumer API.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class MaterialSliderThumb : Thumb
{
    public static readonly StyledProperty<bool> IsDraggingProperty =
        AvaloniaProperty.Register<MaterialSliderThumb, bool>(nameof(IsDragging));
    public bool IsDragging { get => GetValue(IsDraggingProperty); private set => SetValue(IsDraggingProperty, value); }
    protected override Type StyleKeyOverride => typeof(Thumb);
    protected override void OnDragStarted(Avalonia.Input.VectorEventArgs e)
    {
        IsDragging = true;
        base.OnDragStarted(e);
    }
    protected override void OnDragCompleted(Avalonia.Input.VectorEventArgs e)
    {
        IsDragging = false;
        base.OnDragCompleted(e);
    }
}

/// <summary>Template part of the slider themes; not part of the consumer API.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class MaterialSliderTrack : Control
{
    public static readonly StyledProperty<Control?> OwnerProperty =
        AvaloniaProperty.Register<MaterialSliderTrack, Control?>(nameof(Owner));
    public static readonly StyledProperty<IBrush?> ActiveBrushProperty =
        AvaloniaProperty.Register<MaterialSliderTrack, IBrush?>(nameof(ActiveBrush));
    public static readonly StyledProperty<IBrush?> InactiveBrushProperty =
        AvaloniaProperty.Register<MaterialSliderTrack, IBrush?>(nameof(InactiveBrush));
    public static readonly StyledProperty<IBrush?> DisabledBrushProperty =
        AvaloniaProperty.Register<MaterialSliderTrack, IBrush?>(nameof(DisabledBrush));
    public static readonly StyledProperty<bool> IsDraggingProperty =
        AvaloniaProperty.Register<MaterialSliderTrack, bool>(nameof(IsDragging));

    private Control? _subscribedOwner;
    private AvaloniaList<double>? _ticks;

    public Control? Owner { get => GetValue(OwnerProperty); set => SetValue(OwnerProperty, value); }
    public IBrush? ActiveBrush { get => GetValue(ActiveBrushProperty); set => SetValue(ActiveBrushProperty, value); }
    public IBrush? InactiveBrush { get => GetValue(InactiveBrushProperty); set => SetValue(InactiveBrushProperty, value); }
    public IBrush? DisabledBrush { get => GetValue(DisabledBrushProperty); set => SetValue(DisabledBrushProperty, value); }
    public bool IsDragging { get => GetValue(IsDraggingProperty); set => SetValue(IsDraggingProperty, value); }

    static MaterialSliderTrack() => AffectsRender<MaterialSliderTrack>(OwnerProperty,
        ActiveBrushProperty, InactiveBrushProperty, DisabledBrushProperty, IsEnabledProperty, IsDraggingProperty);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == OwnerProperty)
            SubscribeOwner();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        SubscribeOwner();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        UnsubscribeOwner();
        base.OnDetachedFromVisualTree(e);
    }

    private void SubscribeOwner()
    {
        UnsubscribeOwner();
        _subscribedOwner = Owner;
        if (_subscribedOwner is not null)
            _subscribedOwner.PropertyChanged += OwnerChanged;
        SubscribeTicks();
    }

    private void UnsubscribeOwner()
    {
        if (_subscribedOwner is not null)
            _subscribedOwner.PropertyChanged -= OwnerChanged;
        _subscribedOwner = null;
        if (_ticks is not null)
            _ticks.CollectionChanged -= TicksChanged;
        _ticks = null;
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

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var slider = Owner as Slider;
        var range = Owner as MaterialRangeSlider;
        if (slider is null && range is null)
            return;
        var vertical = slider?.Orientation == Orientation.Vertical;
        // The native Track is extended by 22 DIP so its 48-DIP touch target does not
        // become 48 DIP of missing track. The visible 4-DIP handle defines the ends.
        var length = (vertical ? Bounds.Height : Bounds.Width) - 4;
        if (length <= 16)
            return;
        var minimum = slider?.Minimum ?? range!.Minimum;
        var maximum = slider?.Maximum ?? range!.Maximum;
        var lower = range is null ? 0 : Fraction(range.LowerValue, minimum, maximum) * length;
        var upper = Fraction(slider?.Value ?? range!.UpperValue, minimum, maximum) * length;
        var reversed = vertical ? slider?.IsDirectionReversed != true : slider?.IsDirectionReversed == true;
        var enabled = Owner!.IsEffectivelyEnabled;
        var focused = slider?.IsKeyboardFocusWithin == true;
        var lowerFocused = range?.IsLowerThumbFocused == true;
        var upperFocused = range?.IsUpperThumbFocused == true;
        var lowerGap = lowerFocused || range?.IsLowerThumbDragging == true ? 7.0 : 8.0;
        var upperGap = focused || IsDragging || upperFocused || range?.IsUpperThumbDragging == true ? 7.0 : 8.0;

        // AndroidX SliderDefaults: 16-DIP tracks, 8-DIP outer corners, 2-DIP
        // inside corners, 6-DIP clear space measured from the edge of the handle.
        if (range is not null && lower > lowerGap + 8)
        {
            Segment(context, 0, lower - lowerGap, 8, 2, false, enabled, vertical, reversed, length);
            Dot(context, 8, false, enabled, vertical, reversed, length);
        }
        if (upper < length - upperGap - 8)
        {
            Segment(context, upper + upperGap, length, 2, 8, false, enabled, vertical, reversed, length);
            Dot(context, length - 8, false, enabled, vertical, reversed, length);
        }
        var activeStart = range is null ? 0 : lower + lowerGap;
        var activeEnd = upper - upperGap;
        if (activeEnd - activeStart > (range is null ? 8 : 2))
            Segment(context, activeStart, activeEnd, range is null ? 8 : 2, 2, true,
                enabled, vertical, reversed, length);

        var showTicks = range is not null || slider!.TickPlacement != TickPlacement.None || slider.IsSnapToTickEnabled;
        if (!showTicks || maximum <= minimum)
            return;
        if (_ticks is { Count: > 0 })
        {
            for (var i = 0; i < _ticks.Count; i++)
            {
                var value = _ticks[i];
                if (value >= minimum && value <= maximum)
                    Tick(context, Fraction(value, minimum, maximum), lower, upper, lowerGap, upperGap, range is not null, enabled, vertical, reversed, length);
            }
        }
        else
        {
            var frequency = slider?.TickFrequency ?? range!.TickFrequency;
            if (frequency <= 0 || !double.IsFinite(frequency))
                return;
            var steps = (maximum / 2 - minimum / 2) / (frequency / 2);
            // More than one tick per pixel cannot be resolved; skip uniformly without
            // allocating a potentially unbounded list for very small frequencies.
            var stride = Math.Max(1, Math.Ceiling(steps / Math.Max(1, length / 4)));
            for (double index = 0; index < steps && double.IsFinite(steps); index += stride)
                Tick(context, index / steps, lower, upper, lowerGap, upperGap, range is not null, enabled, vertical, reversed, length);
        }
    }

    private static double Fraction(double value, double minimum, double maximum) => maximum > minimum
        ? Math.Clamp((value / 2 - minimum / 2) / (maximum / 2 - minimum / 2), 0, 1) : 0;

    private void Tick(DrawingContext context, double fraction, double lower, double upper,
        double lowerGap, double upperGap, bool range,
        bool enabled, bool vertical, bool reversed, double length)
    {
        if (!double.IsFinite(fraction) || fraction >= 1 || range && fraction <= 0)
            return;
        // Keep the native value-to-position mapping: custom/nonuniform Avalonia ticks
        // must meet the handle at the value they represent. Endpoint dots are inset
        // to the center of the outer cap, like AndroidX's stop indicators.
        var position = fraction <= 0 ? 8 : fraction * length;
        if (position < 8 || position > length - 8)
            return;
        if (Math.Abs(position - upper) <= upperGap || range && Math.Abs(position - lower) <= lowerGap)
            return;
        // SliderTokens: StopIndicatorColor and StopIndicatorColorSelected are both
        // SecondaryContainer, so ticks and stop indicators never use the active role.
        Dot(context, position, false, enabled, vertical, reversed, length);
    }

    private void Segment(DrawingContext context, double start, double end, double startRadius,
        double endRadius, bool active, bool enabled, bool vertical, bool reversed, double length)
    {
        if (reversed)
        {
            (start, end) = (length - end, length - start);
            (startRadius, endRadius) = (endRadius, startRadius);
        }
        var rect = vertical
            ? new Rect((Bounds.Width - 16) / 2, start + 2, 16, end - start)
            : new Rect(start + 2, (Bounds.Height - 16) / 2, end - start, 16);
        var rounded = vertical
            ? new RoundedRect(rect, startRadius, startRadius, endRadius, endRadius)
            : new RoundedRect(rect, startRadius, endRadius, endRadius, startRadius);
        using (context.PushOpacity(enabled ? 1 : active ? 0.38 : 0.12))
            context.DrawRectangle(enabled ? active ? ActiveBrush : InactiveBrush : DisabledBrush, null, rounded);
    }

    private void Dot(DrawingContext context, double position, bool primary, bool enabled,
        bool vertical, bool reversed, double length)
    {
        if (reversed)
            position = length - position;
        var center = vertical ? new Point(Bounds.Width / 2, position + 2) : new Point(position + 2, Bounds.Height / 2);
        using (context.PushOpacity(enabled ? 1 : primary ? 0.38 : 0.12))
            context.DrawEllipse(enabled ? primary ? ActiveBrush : InactiveBrush : DisabledBrush, null, center, 2, 2);
    }
}
