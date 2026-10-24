using System.Diagnostics;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Automation.Peers;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Controls;

/// <summary>A read-only, rounded circular progress indicator with native range coercion and automation.</summary>
/// <remarks>Indeterminate motion pauses off-tree, when effectively hidden, or when MaterialMotion.IsReduced is true.</remarks>
public sealed class CircularProgressIndicator : RangeBase
{
    public static readonly StyledProperty<bool> IsIndeterminateProperty =
        AvaloniaProperty.Register<CircularProgressIndicator, bool>(nameof(IsIndeterminate));
    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<CircularProgressIndicator, double>(nameof(StrokeThickness), 4,
            validate: value => double.IsFinite(value) && value > 0);
    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<CircularProgressIndicator, IBrush?>(nameof(TrackBrush));
    public static readonly StyledProperty<double> GapSizeProperty =
        AvaloniaProperty.Register<CircularProgressIndicator, double>(nameof(GapSize), 4,
            validate: value => double.IsFinite(value) && value >= 0);

    private static readonly SplineEasing ProgressEasing = new(0.2, 0, 0, 1);
    // Standard motion and gap geometry follow AndroidX material3 ProgressIndicator.kt:
    // https://github.com/androidx/androidx/blob/androidx-main/compose/material3/material3/src/commonMain/kotlin/androidx/compose/material3/ProgressIndicator.kt

    private readonly ProgressAnimationClock _clock;
    private readonly Pen _activePen = new() { LineCap = PenLineCap.Round };
    private readonly Pen _trackPen = new() { LineCap = PenLineCap.Round };

    static CircularProgressIndicator()
    {
        ValueProperty.OverrideMetadata<CircularProgressIndicator>(new(defaultBindingMode: BindingMode.OneWay));
        AffectsRender<CircularProgressIndicator>(ValueProperty, MinimumProperty, MaximumProperty,
            IsIndeterminateProperty, StrokeThicknessProperty, GapSizeProperty, TrackBrushProperty, ForegroundProperty);
    }

    public CircularProgressIndicator() => _clock = new ProgressAnimationClock(this, () => IsIndeterminate);

    public bool IsIndeterminate { get => GetValue(IsIndeterminateProperty); set => SetValue(IsIndeterminateProperty, value); }
    public double StrokeThickness { get => GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }
    public IBrush? TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }
    public double GapSize { get => GetValue(GapSizeProperty); set => SetValue(GapSizeProperty, value); }

    protected override Size MeasureOverride(Size availableSize) => new(40, 40);

    protected override AutomationPeer OnCreateAutomationPeer() => new CircularProgressAutomationPeer(this);

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var diameter = Math.Min(Bounds.Width, Bounds.Height);
        var thickness = Math.Min(StrokeThickness, diameter / 2);
        var radius = (diameter - thickness) / 2;
        if (radius <= 0)
            return;

        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        _activePen.Brush = Foreground;
        _activePen.Thickness = thickness;
        _trackPen.Brush = TrackBrush;
        _trackPen.Thickness = thickness;

        var fraction = Maximum <= Minimum ? 1 : Math.Clamp((Value - Minimum) / (Maximum - Minimum), 0, 1);
        var start = -Math.PI / 2;
        var sweep = fraction * Math.Tau;
        if (IsIndeterminate)
        {
            // AndroidX standard motion: three global turns and four additional quarter-turns
            // per 6000 ms, with the arc expanding to 87% and contracting to 10%.
            var time = _clock.ElapsedMilliseconds % 6000;
            // Additional-rotation keyframes interpolate the 300 ms turns linearly; their
            // emphasized-decelerate easing belongs to the following constant-value holds.
            var quarterTurn = Math.Floor(time / 1500);
            var rotation = time / 6000 * 3 + (quarterTurn + Math.Min(time % 1500 / 300, 1)) / 4;
            start = rotation * Math.Tau;
            // Keyframes use linear interpolation up to 3000 ms; the easing on that keyframe
            // applies to the following (contracting) interval, not the preceding one.
            var progress = time < 3000
                ? 0.1 + 0.77 * (time / 3000)
                : 0.87 - 0.77 * ProgressEasing.Ease((time - 3000) / 3000);
            sweep = progress * Math.Tau;
        }

        if (sweep <= 0)
        {
            context.DrawEllipse(null, _trackPen, center, radius, radius);
            return;
        }
        if (sweep >= Math.Tau - 0.0001)
        {
            context.DrawEllipse(null, _activePen, center, radius, radius);
            return;
        }

        // AndroidX converts the cap-adjusted gap using the outer diameter, not the inset
        // stroke-center radius. Grow the gap with the sweep at the start of progress.
        var gapAngle = Math.Min(sweep, (GapSize + thickness) * 2 / diameter);
        var remaining = Math.Tau - sweep - gapAngle * 2;
        if (remaining > 0)
            DrawArc(context, _trackPen, center, radius, start + sweep + gapAngle, remaining);
        DrawArc(context, _activePen, center, radius, start, sweep);
    }

    private static void DrawArc(DrawingContext context, Pen pen,
        Point center, double radius, double start, double sweep)
    {
        // Composition retains this geometry after Render returns. Reopening a published
        // StreamGeometry mutates its native SKPath while the render thread reads it.
        // Each submitted arc must own a path that is never modified again.
        var geometry = new StreamGeometry();
        using (var path = geometry.Open())
        {
            path.BeginFigure(new Point(center.X + radius * Math.Cos(start), center.Y + radius * Math.Sin(start)), false);
            var end = start + sweep;
            path.ArcTo(new Point(center.X + radius * Math.Cos(end), center.Y + radius * Math.Sin(end)),
                new Size(radius, radius), 0, sweep > Math.PI, SweepDirection.Clockwise);
            path.EndFigure(false);
        }
        context.DrawGeometry(null, pen, geometry);
    }

    private sealed class CircularProgressAutomationPeer(CircularProgressIndicator owner) : ProgressBarAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(CircularProgressIndicator);
        protected override string? GetItemStatusCore() =>
            AutomationProperties.GetItemStatus(owner) ?? (owner.IsIndeterminate ? "Indeterminate" : base.GetItemStatusCore());

        protected override void OwnerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            base.OwnerPropertyChanged(sender, e);
            if (e.Property == IsIndeterminateProperty)
                RaisePropertyChangedEvent(AutomationElementIdentifiers.ItemStatusProperty,
                    e.OldValue is true ? "Indeterminate" : null, GetItemStatusCore());
        }

        // Unknown progress has no numeric value, rather than falsely announcing zero percent.
        protected override object? GetProviderCore(Type providerType) =>
            providerType == typeof(IRangeValueProvider) && owner.IsIndeterminate ? null : base.GetProviderCore(providerType);
    }
}

/// <summary>Drawing surface used by the native ProgressBar template; range and automation belong to its templated parent.</summary>
internal sealed class LinearProgressPresenter : Control
{
    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<LinearProgressPresenter, double>(nameof(Minimum));
    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<LinearProgressPresenter, double>(nameof(Maximum), 100);
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<LinearProgressPresenter, double>(nameof(Value));
    public static readonly StyledProperty<bool> IsIndeterminateProperty =
        AvaloniaProperty.Register<LinearProgressPresenter, bool>(nameof(IsIndeterminate));
    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<LinearProgressPresenter, Orientation>(nameof(Orientation));
    public static readonly StyledProperty<IBrush?> IndicatorBrushProperty =
        AvaloniaProperty.Register<LinearProgressPresenter, IBrush?>(nameof(IndicatorBrush));
    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<LinearProgressPresenter, IBrush?>(nameof(TrackBrush));

    private static readonly SplineEasing IndeterminateEasing = new(0.3, 0, 0.8, 0.15);
    private readonly Pen _segmentPen = new() { LineCap = PenLineCap.Round };
    private readonly ProgressAnimationClock _clock;

    static LinearProgressPresenter() => AffectsRender<LinearProgressPresenter>(MinimumProperty, MaximumProperty,
        ValueProperty, IsIndeterminateProperty, OrientationProperty, IndicatorBrushProperty, TrackBrushProperty, FlowDirectionProperty);

    public LinearProgressPresenter() => _clock = new ProgressAnimationClock(this, () => IsIndeterminate);

    public double Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public double Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public bool IsIndeterminate { get => GetValue(IsIndeterminateProperty); set => SetValue(IsIndeterminateProperty, value); }
    public Orientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }
    public IBrush? IndicatorBrush { get => GetValue(IndicatorBrushProperty); set => SetValue(IndicatorBrushProperty, value); }
    public IBrush? TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var vertical = Orientation == Orientation.Vertical;
        var length = vertical ? Bounds.Height : Bounds.Width;
        var thickness = Math.Min(4, vertical ? Bounds.Width : Bounds.Height);
        if (length <= 0 || thickness <= 0)
            return;

        var gap = 4 + thickness;
        if (IsIndeterminate)
        {
            // Four independently timed endpoints form the standard two-line 1750 ms cycle.
            var time = _clock.ElapsedMilliseconds % 1750;
            var firstHead = Position(time, 0, 1000) * length;
            var firstTail = Position(time, 250, 1000) * length;
            var secondHead = Position(time, 650, 850) * length;
            var secondTail = Position(time, 900, 850) * length;
            // Keep a stable incomplete indicator when accessibility disables animation.
            if (MaterialMotion.GetIsReduced(this))
            {
                firstHead = length * 0.8;
                firstTail = length * 0.2;
                secondHead = secondTail = 0;
            }

            if (firstHead < length - gap)
                Segment(context, TrackBrush, firstHead > 0 ? firstHead + gap : 0, length, length, thickness, vertical);
            Segment(context, IndicatorBrush, firstTail, firstHead, length, thickness, vertical);
            if (firstTail > gap)
                Segment(context, TrackBrush, secondHead > 0 ? secondHead + gap : 0,
                    firstTail < length ? firstTail - gap : length, length, thickness, vertical);
            Segment(context, IndicatorBrush, secondTail, secondHead, length, thickness, vertical);
            if (secondTail > gap)
                Segment(context, TrackBrush, 0, secondTail < length ? secondTail - gap : length, length, thickness, vertical);
        }
        else
        {
            var fraction = Maximum <= Minimum ? 1 : Math.Clamp((Value - Minimum) / (Maximum - Minimum), 0, 1);
            var end = length * fraction;
            Segment(context, TrackBrush, end + Math.Min(end, gap), length, length, thickness, vertical);
            Segment(context, IndicatorBrush, 0, end, length, thickness, vertical);
            // The 4 dp stop stays at the end of the track, merging into the active line at completion.
            var stop = Math.Min(4, Math.Min(thickness, length));
            var stopCenter = vertical
                ? new Point(Bounds.Width / 2, stop / 2)
                : new Point(FlowDirection == FlowDirection.RightToLeft ? stop / 2 : length - stop / 2, Bounds.Height / 2);
            context.DrawEllipse(IndicatorBrush, null, stopCenter, stop / 2, stop / 2);
        }
    }

    private static double Position(double time, double delay, double duration) =>
        IndeterminateEasing.Ease(Math.Clamp((time - delay) / duration, 0, 1));

    private void Segment(DrawingContext context, IBrush? brush, double start, double end,
        double length, double thickness, bool vertical)
    {
        if (end <= start || brush is null)
            return;
        if (!vertical && FlowDirection == FlowDirection.RightToLeft)
            (start, end) = (length - end, length - start);
        if (thickness > length)
        {
            var rect = vertical
                ? new Rect((Bounds.Width - thickness) / 2, length - end, thickness, end - start)
                : new Rect(start, (Bounds.Height - thickness) / 2, end - start, thickness);
            context.DrawRectangle(brush, null, rect);
            return;
        }

        // Endpoints are stroke centers, clamped only at the outside bounds. A rounded
        // rectangle would shorten small moving segments and get cap-adjusted gaps wrong.
        var cap = thickness / 2;
        start = Math.Clamp(start, cap, length - cap);
        end = Math.Clamp(end, cap, length - cap);
        _segmentPen.Brush = brush;
        _segmentPen.Thickness = thickness;
        var from = vertical ? new Point(Bounds.Width / 2, length - start) : new Point(start, Bounds.Height / 2);
        var to = vertical ? new Point(Bounds.Width / 2, length - end) : new Point(end, Bounds.Height / 2);
        if (start == end)
            context.DrawEllipse(brush, null, from, cap, cap);
        else
            context.DrawLine(_segmentPen, from, to);
    }
}

// One frame timer shared by every animating indicator; global accessibility notification is
// subscribed only while attached. The timer exists only while at least one indicator animates.
internal sealed class ProgressAnimationClock
{
    private static readonly List<ProgressAnimationClock> Running = new();
    private static DispatcherTimer? _timer;
    private readonly Control _owner;
    private readonly Func<bool> _isIndeterminate;
    private readonly List<Visual> _visibilityAncestors = new();
    private bool _attached;
    private bool _running;
    private long _started;
    public double ElapsedMilliseconds { get; private set; }

    public ProgressAnimationClock(Control owner, Func<bool> isIndeterminate)
    {
        _owner = owner;
        _isIndeterminate = isIndeterminate;
        owner.AttachedToVisualTree += (_, _) =>
        {
            _attached = true;
            MaterialMotion.ReducedMotionChanged += ReducedMotionChanged;
            foreach (var ancestor in owner.GetVisualAncestors())
            {
                _visibilityAncestors.Add(ancestor);
                ancestor.PropertyChanged += AncestorPropertyChanged;
            }
            Update();
        };
        owner.DetachedFromVisualTree += (_, _) =>
        {
            _attached = false;
            MaterialMotion.ReducedMotionChanged -= ReducedMotionChanged;
            foreach (var ancestor in _visibilityAncestors)
                ancestor.PropertyChanged -= AncestorPropertyChanged;
            _visibilityAncestors.Clear();
            Update();
        };
        owner.PropertyChanged += (_, change) =>
        {
            if (change.Property == CircularProgressIndicator.IsIndeterminateProperty ||
                change.Property == LinearProgressPresenter.IsIndeterminateProperty ||
                change.Property == MaterialMotion.IsReducedProperty || change.Property == Visual.IsVisibleProperty)
                Update();
        };
    }

    private void ReducedMotionChanged(object? sender, EventArgs e) => Update();

    private void AncestorPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Visual.IsVisibleProperty)
            Update();
    }

    private void Update()
    {
        var animate = _attached && _owner.IsEffectivelyVisible && _isIndeterminate() && !MaterialMotion.GetIsReduced(_owner);
        if (animate)
            Start();
        else
            Stop();
        _owner.InvalidateVisual();
    }

    private void Start()
    {
        if (_running)
            return;
        _running = true;
        _started = Stopwatch.GetTimestamp();
        Running.Add(this);
        if (_timer is null)
        {
            _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromSeconds(1d / 60) };
            _timer.Tick += OnTick;
        }
        if (!_timer.IsEnabled)
            _timer.Start();
    }

    private void Stop()
    {
        ElapsedMilliseconds = 0;
        if (!_running)
            return;
        _running = false;
        Running.Remove(this);
        if (Running.Count == 0)
            _timer?.Stop();
    }

    private static void OnTick(object? sender, EventArgs e)
    {
        // Walk backwards so a clock that stops itself during the frame cannot shift the index.
        for (var i = Running.Count - 1; i >= 0; i--)
        {
            var clock = Running[i];
            if (!clock._owner.IsEffectivelyVisible || MaterialMotion.GetIsReduced(clock._owner))
            {
                clock.Update();
                continue;
            }
            clock.ElapsedMilliseconds = Stopwatch.GetElapsedTime(clock._started).TotalMilliseconds;
            clock._owner.InvalidateVisual();
        }
    }
}
