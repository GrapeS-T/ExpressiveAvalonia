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
/// <remarks>
/// <para>Covers the source Circular-determinate (local-001278) and Circular-indeterminate
/// (local-001303) axes: <see cref="IndicatorType"/> Flat/Wave, 4 dp or 8 dp
/// <see cref="StrokeThickness"/> and the source "Show track" boolean. Indeterminate motion pauses
/// off-tree, when effectively hidden, when reduced motion is requested, or when the owning theme
/// disables animation.</para>
/// <para>Motion follows the guideline videos and AndroidX <c>ProgressIndicator.kt</c>: the
/// indeterminate arc rotates 1080° per 6000 ms with four 90° turns, grows to 82% and shrinks back,
/// and the track is only the remainder of the ring around it. A determinate wave keeps travelling
/// (one lobe per second) even when the value is static, and flattens out over the first and last
/// tenths of the ring, exactly as the source amplitude function does.</para>
/// </remarks>
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
    public static readonly StyledProperty<ProgressIndicatorType> IndicatorTypeProperty =
        AvaloniaProperty.Register<CircularProgressIndicator, ProgressIndicatorType>(nameof(IndicatorType));
    /// <summary>
    /// Wave amplitude in source dp on the 48 dp artboard. The guideline video measures a
    /// centreline of 20.4 ± 1.95 dp, so the crest sits on the 22 dp track centreline and the
    /// trough 4 dp inside it; the default rounds that measurement to 2.
    /// </summary>
    public static readonly StyledProperty<double> WaveAmplitudeProperty =
        AvaloniaProperty.Register<CircularProgressIndicator, double>(nameof(WaveAmplitude), 2,
            validate: value => double.IsFinite(value) && value >= 0);
    /// <summary>Number of crests around the ring; source value is 9 (one crest every 40°).</summary>
    public static readonly StyledProperty<int> WaveLobesProperty =
        AvaloniaProperty.Register<CircularProgressIndicator, int>(nameof(WaveLobes), ProgressWave.CircularLobes,
            validate: value => value >= 1);
    /// <summary>Source "Show track" boolean: paints the remaining ring behind the active arc.</summary>
    public static readonly StyledProperty<bool> ShowTrackProperty =
        AvaloniaProperty.Register<CircularProgressIndicator, bool>(nameof(ShowTrack), true);

    // Standard (0.2, 0, 0, 1) and emphasized-decelerate (0.05, 0.7, 0.1, 1) motion curves.
    private static readonly SplineEasing ProgressEasing = new(0.2, 0, 0, 1);
    private static readonly SplineEasing TurnEasing = new(0.05, 0.7, 0.1, 1);

    // AndroidX ProgressIndicator.kt indeterminate circular motion: three global turns plus four
    // 90° turns per 6000 ms cycle, and a sweep that travels between 14% and 82% over 3000 ms
    // halves. The guideline video eases the sweep in both halves and eases each 90° turn, so the
    // easing is applied to the interval it visually belongs to (upstream's keyframe builder
    // attaches it to the following interval, which would make the growth linear). The video's
    // sweep range is measured from the arc minus its round caps (61°…307° visible, i.e. 50°…297°
    // of path); upstream's 10%…87% tokens would leave only a 1° sliver of track at the peak,
    // which the video does not show.
    private const double CycleMilliseconds = 6000;
    private const double TurnMilliseconds = 300;
    private const double TurnIntervalMilliseconds = 1500;
    private const double SweepHalfMilliseconds = 3000;
    private const double MinSweep = 0.14;
    private const double MaxSweep = 0.82;

    private readonly ProgressAnimationClock _clock;
    private readonly Pen _activePen = new() { LineCap = PenLineCap.Round };
    private readonly Pen _trackPen = new() { LineCap = PenLineCap.Round };

    static CircularProgressIndicator()
    {
        ValueProperty.OverrideMetadata<CircularProgressIndicator>(new(defaultBindingMode: BindingMode.OneWay));
        AffectsRender<CircularProgressIndicator>(ValueProperty, MinimumProperty, MaximumProperty,
            IsIndeterminateProperty, StrokeThicknessProperty, GapSizeProperty, TrackBrushProperty, ForegroundProperty,
            IndicatorTypeProperty, WaveAmplitudeProperty, WaveLobesProperty, ShowTrackProperty);
    }

    public CircularProgressIndicator() => _clock = new ProgressAnimationClock(this, ShouldAnimate);

    public bool IsIndeterminate { get => GetValue(IsIndeterminateProperty); set => SetValue(IsIndeterminateProperty, value); }
    public double StrokeThickness { get => GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }
    public IBrush? TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }
    public double GapSize { get => GetValue(GapSizeProperty); set => SetValue(GapSizeProperty, value); }

    /// <summary>Flat (source default) or wavy active stroke; the track stays a plain ring.</summary>
    public ProgressIndicatorType IndicatorType
    {
        get => GetValue(IndicatorTypeProperty);
        set => SetValue(IndicatorTypeProperty, value);
    }

    /// <summary>
    /// Wave amplitude in dp on the 48 dp source artboard; the rendered amplitude scales with the
    /// control's size. The crest then sits on the track centreline and the trough is two
    /// amplitudes inside it, exactly as measured from the source.
    /// </summary>
    public double WaveAmplitude { get => GetValue(WaveAmplitudeProperty); set => SetValue(WaveAmplitudeProperty, value); }

    /// <summary>Crests around the ring; the source uses 9 (one every 40°).</summary>
    public int WaveLobes { get => GetValue(WaveLobesProperty); set => SetValue(WaveLobesProperty, value); }

    /// <summary>
    /// Source "Show track": paints the remaining ring behind the active arc, default true as in
    /// the source. Set false for a bare arc (AndroidX's transparent-track appearance).
    /// </summary>
    public bool ShowTrack { get => GetValue(ShowTrackProperty); set => SetValue(ShowTrackProperty, value); }

    /// <summary>Clamped range fraction; also drives the determinate wave envelope.</summary>
    private double Fraction => Maximum <= Minimum
        ? 1
        : Math.Clamp((Value - Minimum) / (Maximum - Minimum), 0, 1);

    /// <summary>
    /// Indeterminate motion always animates; a determinate wave animates while its phase has to
    /// travel, so a static value still shows the source's moving wave.
    /// </summary>
    private bool ShouldAnimate() => IsIndeterminate || HasTravellingWave;

    private bool HasTravellingWave =>
        IndicatorType == ProgressIndicatorType.Wave && WaveAmplitude > 0 &&
        ProgressWave.WaveEnvelope(Fraction) > 0;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        // The clock observes IsIndeterminate, visibility and reduced motion itself; everything
        // else that can switch the travelling wave on or off is reported here.
        if (change.Property == ValueProperty || change.Property == MinimumProperty ||
            change.Property == MaximumProperty || change.Property == IndicatorTypeProperty ||
            change.Property == WaveAmplitudeProperty || change.Property == WaveLobesProperty)
            _clock?.Refresh();
    }

    protected override Size MeasureOverride(Size availableSize) => new(40, 40);

    protected override AutomationPeer OnCreateAutomationPeer() => new CircularProgressAutomationPeer(this);

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var diameter = Math.Min(Bounds.Width, Bounds.Height);
        var thickness = Math.Min(StrokeThickness, diameter / 2);
        // Track centreline, as in the source: (D − t)/2, so the track's outer edge is D/2.
        var radius = (diameter - thickness) / 2;
        if (radius <= 0)
            return;

        var fraction = Fraction;
        var wavy = IndicatorType == ProgressIndicatorType.Wave;
        // Source measurement: the wavy arc's crests sit on the track centreline and its
        // centreline oscillates by the wave amplitude, so the base radius is one amplitude in.
        var amplitude = wavy ? Math.Min(ProgressWave.CircularAmplitude(diameter, WaveAmplitude), radius) : 0;

        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        _activePen.Brush = Foreground;
        _activePen.Thickness = thickness;
        _trackPen.Brush = TrackBrush;
        _trackPen.Thickness = thickness;

        var elapsed = _clock.ElapsedMilliseconds;
        var start = -Math.PI / 2;
        var sweep = fraction * Math.Tau;
        if (IsIndeterminate)
        {
            if (MaterialMotion.GetIsReduced(this))
            {
                // Reduced motion: a still three-quarter arc reads as "in progress" without
                // implying a percentage, and keeps the same sweep geometry as the animation.
                sweep = Math.Tau * 0.75;
            }
            else
            {
                // Three global turns and four additional quarter-turns per cycle, with the arc
                // growing from 14% to 82% of the ring and shrinking back.
                var time = elapsed % CycleMilliseconds;
                start = IndeterminateRotationTurns(time) * Math.Tau;
                sweep = IndeterminateSweep(time) * Math.Tau;
            }
        }
        else
        {
            // Source amplitude function: no wave below 10% or from 95% on, so the ring settles
            // into a plain circle as it completes. The guideline video's ramps are reproduced as
            // a progress envelope, which also keeps a static value stable.
            amplitude *= ProgressWave.WaveEnvelope(fraction);
        }

        // The source slides the wave one wavelength (one lobe on the ring) per second towards the
        // start of the stroke, and rotates the whole wavy path with the indicator, so the phase is
        // measured from the arc's start and advances with the shared clock.
        var wavePhase = elapsed / 1000 * Math.Tau;
        // Base radius of the wave: one (enveloped) amplitude inside the track centreline, so the
        // crest always lands on that centreline however far the wave has faded in or out.
        var waveRadius = radius - amplitude;

        if (sweep <= 0)
        {
            if (ShowTrack)
                context.DrawEllipse(null, _trackPen, center, radius, radius);
            return;
        }
        if (sweep >= Math.Tau - 0.0001)
        {
            if (wavy && amplitude > 0)
                DrawWavyArc(context, _activePen, center, waveRadius, start, Math.Tau, amplitude, WaveLobes, wavePhase);
            else
                context.DrawEllipse(null, _activePen, center, radius, radius);
            return;
        }

        // AndroidX converts the cap-adjusted gap using the outer diameter, not the inset
        // stroke-center radius. Grow the gap with the sweep at the start of progress.
        var gapAngle = Math.Min(sweep, (GapSize + thickness) * 2 / diameter);
        if (ShowTrack)
        {
            // Only the remainder of the ring is painted, on both sides of the arc, in the same
            // (possibly rotated) frame: an indeterminate indicator shows the track ahead of its
            // arc rather than a full ring behind it.
            var remaining = Math.Tau - sweep - gapAngle * 2;
            if (remaining > 0)
                DrawArc(context, _trackPen, center, radius, start + sweep + gapAngle, remaining);
        }
        if (wavy && amplitude > 0)
            DrawWavyArc(context, _activePen, center, waveRadius, start, sweep, amplitude, WaveLobes, wavePhase);
        else
            DrawArc(context, _activePen, center, radius, start, sweep);
    }

    /// <summary>Global rotation plus the four 90° turns of one indeterminate cycle, in turns.</summary>
    private static double IndeterminateRotationTurns(double time)
    {
        var turn = Math.Floor(time / TurnIntervalMilliseconds);
        var within = time - turn * TurnIntervalMilliseconds;
        var eased = TurnEasing.Ease(Math.Min(within / TurnMilliseconds, 1));
        return time / CycleMilliseconds * 3 + (turn + eased) / 4;
    }

    /// <summary>Sweep of one indeterminate cycle as a fraction of the ring.</summary>
    private static double IndeterminateSweep(double time)
    {
        var growing = time < SweepHalfMilliseconds;
        var within = growing ? time : time - SweepHalfMilliseconds;
        var eased = ProgressEasing.Ease(Math.Clamp(within / SweepHalfMilliseconds, 0, 1));
        return growing
            ? MinSweep + (MaxSweep - MinSweep) * eased
            : MaxSweep - (MaxSweep - MinSweep) * eased;
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

    /// <summary>
    /// Draws an arc whose stroke oscillates radially, as in the source "Type=Wave" variants.
    /// The source wave is anchored at the arc start, repeats every 360°/lobes, and slides one lobe
    /// per second towards that start, so <paramref name="phase"/> is added to the angle measured
    /// from the start. The arc start rotates with the indicator, which carries the wave with it.
    /// </summary>
    private static void DrawWavyArc(DrawingContext context, Pen pen, Point center, double radius,
        double start, double sweep, double amplitude, int lobes, double phase)
    {
        if (sweep <= 0 || radius <= 0)
            return;
        if (amplitude <= 0 || lobes < 1)
        {
            DrawArc(context, pen, center, radius, start, sweep);
            return;
        }

        var closed = sweep >= Math.Tau - 0.0001;
        var lobeAngle = Math.Tau / lobes;
        // Sample a twenty-fourth of a lobe so crests and troughs keep their shape at any size.
        var steps = Math.Clamp((int)Math.Ceiling(sweep / (lobeAngle / 24)), 12, 720);
        var geometry = new StreamGeometry();
        using (var path = geometry.Open())
        {
            for (var i = 0; i <= steps; i++)
            {
                var angle = start + sweep * i / steps;
                var r = radius + amplitude * Math.Cos(lobes * (angle - start) + phase);
                var point = new Point(center.X + r * Math.Cos(angle), center.Y + r * Math.Sin(angle));
                if (i == 0)
                    path.BeginFigure(point, false);
                else
                    path.LineTo(point);
            }
            path.EndFigure(closed);
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
    public static readonly StyledProperty<ProgressIndicatorType> IndicatorTypeProperty =
        AvaloniaProperty.Register<LinearProgressPresenter, ProgressIndicatorType>(nameof(IndicatorType));
    public static readonly StyledProperty<double> IndicatorThicknessProperty =
        AvaloniaProperty.Register<LinearProgressPresenter, double>(nameof(IndicatorThickness), 4,
            validate: value => double.IsFinite(value) && value > 0);
    public static readonly StyledProperty<double> WaveHeightProperty =
        AvaloniaProperty.Register<LinearProgressPresenter, double>(nameof(WaveHeight), 12,
            validate: value => double.IsFinite(value) && value >= 0);
    public static readonly StyledProperty<double> WavelengthProperty =
        AvaloniaProperty.Register<LinearProgressPresenter, double>(nameof(Wavelength), 40,
            validate: value => double.IsFinite(value) && value > 0);

    private static readonly SplineEasing IndeterminateEasing = new(0.3, 0, 0.8, 0.15);
    private readonly Pen _segmentPen = new() { LineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
    private readonly Pen _trackSegmentPen = new() { LineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
    private readonly ProgressAnimationClock _clock;

    static LinearProgressPresenter()
    {
        AffectsRender<LinearProgressPresenter>(MinimumProperty, MaximumProperty,
            ValueProperty, IsIndeterminateProperty, OrientationProperty, IndicatorBrushProperty, TrackBrushProperty,
            FlowDirectionProperty, IndicatorTypeProperty, IndicatorThicknessProperty, WaveHeightProperty, WavelengthProperty);
        AffectsMeasure<LinearProgressPresenter>(IndicatorTypeProperty, IndicatorThicknessProperty,
            WaveHeightProperty, WavelengthProperty);
    }

    public LinearProgressPresenter() => _clock = new ProgressAnimationClock(this, ShouldAnimate);

    public double Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public double Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public bool IsIndeterminate { get => GetValue(IsIndeterminateProperty); set => SetValue(IsIndeterminateProperty, value); }
    public Orientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }
    public IBrush? IndicatorBrush { get => GetValue(IndicatorBrushProperty); set => SetValue(IndicatorBrushProperty, value); }
    public IBrush? TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }
    public ProgressIndicatorType IndicatorType { get => GetValue(IndicatorTypeProperty); set => SetValue(IndicatorTypeProperty, value); }
    public double IndicatorThickness { get => GetValue(IndicatorThicknessProperty); set => SetValue(IndicatorThicknessProperty, value); }
    public double WaveHeight { get => GetValue(WaveHeightProperty); set => SetValue(WaveHeightProperty, value); }
    public double Wavelength { get => GetValue(WavelengthProperty); set => SetValue(WavelengthProperty, value); }

    /// <summary>Clamped range fraction; also drives the determinate wave envelope.</summary>
    private double Fraction => Maximum <= Minimum
        ? 1
        : Math.Clamp((Value - Minimum) / (Maximum - Minimum), 0, 1);

    /// <summary>
    /// Indeterminate motion always animates; a determinate wave animates while its phase has to
    /// travel, so a static value still shows the source's moving wave.
    /// </summary>
    private bool ShouldAnimate() => IsIndeterminate || HasTravellingWave;

    private bool HasTravellingWave =>
        IndicatorType == ProgressIndicatorType.Wave && Wavelength > 0 && WaveHeight > 0 &&
        ProgressWave.WaveEnvelope(Fraction) > 0;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        // The clock observes IsIndeterminate, visibility and reduced motion itself; everything
        // else that can switch the travelling wave on or off is reported here.
        if (change.Property == ValueProperty || change.Property == MinimumProperty ||
            change.Property == MaximumProperty || change.Property == IndicatorTypeProperty ||
            change.Property == IndicatorThicknessProperty || change.Property == WaveHeightProperty ||
            change.Property == WavelengthProperty)
            _clock?.Refresh();
    }

    /// <summary>The wave needs its full band height, so the templated parent grows to fit it.</summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        var thickness = IndicatorThickness;
        var band = IndicatorType == ProgressIndicatorType.Wave
            ? thickness + 2 * ProgressWave.LinearAmplitude(WaveHeight, Wavelength)
            : thickness;
        return Orientation == Orientation.Vertical ? new Size(band, 0) : new Size(0, band);
    }

    // The source axes live on the templated ProgressBar as attached properties. Reading them
    // here keeps the native control free of extra styled properties and avoids a binding hop
    // on every measure and render.
    private ProgressBar? _owner;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // The template root is an ancestor in the visual tree; StyledElement.TemplatedParent is
        // not public API, so the nearest ProgressBar above this presenter is the owner.
        _owner = this.GetVisualAncestors().OfType<ProgressBar>().FirstOrDefault();
        if (_owner is { } owner)
        {
            owner.PropertyChanged += OwnerPropertyChanged;
            SyncFromOwner();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_owner is { } owner)
            owner.PropertyChanged -= OwnerPropertyChanged;
        _owner = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void OwnerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == MaterialProgress.IndicatorTypeProperty || e.Property == MaterialProgress.ThicknessProperty ||
            e.Property == MaterialProgress.WaveHeightProperty || e.Property == MaterialProgress.WavelengthProperty)
            SyncFromOwner();
    }

    private void SyncFromOwner()
    {
        if (_owner is not { } owner)
            return;
        IndicatorType = MaterialProgress.GetIndicatorType(owner);
        IndicatorThickness = MaterialProgress.GetThickness(owner);
        WaveHeight = MaterialProgress.GetWaveHeight(owner);
        Wavelength = MaterialProgress.GetWavelength(owner);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var vertical = Orientation == Orientation.Vertical;
        var length = vertical ? Bounds.Height : Bounds.Width;
        var band = vertical ? Bounds.Width : Bounds.Height;
        var thickness = Math.Min(IndicatorThickness, band);
        if (length <= 0 || thickness <= 0)
            return;
        // Source-measured amplitude; the band grows with it instead of clipping the crests.
        // A parent that forces a smaller height still gets a wave that stays inside its box.
        var amplitude = IndicatorType == ProgressIndicatorType.Wave
            ? Math.Min(ProgressWave.LinearAmplitude(WaveHeight, Wavelength),
                Math.Max(0, (band - thickness) / 2))
            : 0;
        // The source slides the wave one wavelength per second towards the start of the track;
        // the same clock drives the ring's wave so both variants travel together.
        var wavePhase = _clock.ElapsedMilliseconds / 1000 * Math.Tau;

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
            Bar(context, IndicatorBrush, firstTail, firstHead, length, thickness, vertical, amplitude, wavePhase);
            if (firstTail > gap)
                Segment(context, TrackBrush, secondHead > 0 ? secondHead + gap : 0,
                    firstTail < length ? firstTail - gap : length, length, thickness, vertical);
            Bar(context, IndicatorBrush, secondTail, secondHead, length, thickness, vertical, amplitude, wavePhase);
            if (secondTail > gap)
                Segment(context, TrackBrush, 0, secondTail < length ? secondTail - gap : length, length, thickness, vertical);
        }
        else
        {
            var fraction = Fraction;
            var end = length * fraction;
            Segment(context, TrackBrush, end + Math.Min(end, gap), length, length, thickness, vertical);
            // The source drops the wave below 10% and from 95% on, where the segment flattens
            // into the stop indicator as the indicator completes.
            Bar(context, IndicatorBrush, 0, end, length, thickness, vertical,
                amplitude * ProgressWave.WaveEnvelope(fraction), wavePhase);
            // The 4 dp stop stays at the end of the track, merging into the active line at
            // completion. Source local-000699: a 4x4 dot bound to M3/sys/light/primary, which
            // is this presenter's IndicatorBrush (the ProgressBar foreground).
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
        var pen = ReferenceEquals(brush, TrackBrush) ? _trackSegmentPen : _segmentPen;
        pen.Brush = brush;
        pen.Thickness = thickness;
        var from = vertical ? new Point(Bounds.Width / 2, length - start) : new Point(start, Bounds.Height / 2);
        var to = vertical ? new Point(Bounds.Width / 2, length - end) : new Point(end, Bounds.Height / 2);
        if (start == end)
            context.DrawEllipse(brush, null, from, cap, cap);
        else
            context.DrawLine(pen, from, to);
    }

    /// <summary>
    /// Draws an indicator segment, following a sine wave when the wave axis is active. The wave is
    /// anchored at the start of the track and travels towards it, one wavelength per second, as in
    /// the source and the guideline video.
    /// </summary>
    private void Bar(DrawingContext context, IBrush? brush, double start, double end,
        double length, double thickness, bool vertical, double amplitude, double phase)
    {
        if (amplitude <= 0 || end - start <= 0.5)
        {
            Segment(context, brush, start, end, length, thickness, vertical);
            return;
        }
        if (brush is null)
            return;
        if (!vertical && FlowDirection == FlowDirection.RightToLeft)
            (start, end) = (length - end, length - start);

        var cap = thickness / 2;
        var from = Math.Clamp(start, cap, length - cap);
        var to = Math.Clamp(end, cap, length - cap);
        if (to - from <= 0.5)
        {
            var dot = vertical
                ? new Point(Bounds.Width / 2, length - to)
                : new Point(to, Bounds.Height / 2);
            context.DrawEllipse(brush, null, dot, cap, cap);
            return;
        }

        // Sample a twenty-fourth of a wavelength so the crests stay smooth at every thickness.
        var steps = Math.Clamp((int)Math.Ceiling((to - from) / Math.Max(0.75, Wavelength / 24)), 2, 720);
        var geometry = new StreamGeometry();
        using (var path = geometry.Open())
        {
            for (var i = 0; i <= steps; i++)
            {
                var position = from + (to - from) * i / steps;
                // The source path starts on the centreline and its first crest sits a quarter of
                // a wavelength along the track; adding the phase slides the pattern backwards.
                var offset = amplitude * Math.Sin(Math.Tau * position / Wavelength + phase);
                var point = vertical
                    ? new Point(Bounds.Width / 2 + offset, length - position)
                    : new Point(position, Bounds.Height / 2 + offset);
                if (i == 0)
                    path.BeginFigure(point, false);
                else
                    path.LineTo(point);
            }
            path.EndFigure(false);
        }
        _segmentPen.Brush = brush;
        _segmentPen.Thickness = thickness;
        context.DrawGeometry(null, _segmentPen, geometry);
    }
}

/// <summary>
/// Wave geometry measured from the design sources rather than approximated.
/// </summary>
/// <remarks>
/// <para>Linear building blocks (local-000545/000551, 12 dp band, stroke 4 and 8): the drawn
/// stroke outline spans y 1.229…10.771 for wavelength 40 and y 1.66…10.34 for wavelength 20.
/// With the stroke centred, the wave centreline therefore oscillates 3.229…8.771
/// (amplitude 2.7714) and 3.66…8.34 (amplitude 2.34). Both thicknesses measure the same
/// centreline, so the amplitude depends on the wavelength only, and an 8 dp stroke overflows
/// the 12 dp band exactly as the source's 13 dp tall export does. The guideline video
/// (Progress-Indicator-J, 240 dp × 4 dp track) measures a 38.3 dp wavelength and a 2.16 dp
/// centreline amplitude, and the thicker Progress-Indicator-G variant measures 2.56 dp, so the
/// export figures stay as the source of the band geometry.</para>
/// <para>Circular (48 dp artboard): both guideline videos measure the same depth, a centreline of
/// 20.4 ± 1.95 dp, so the crest sits on the track centreline (22.0 = (48 − 4)/2) and the trough
/// 4 dp inside it. The determinate video resolves eight crests (45° apart), the indeterminate one
/// nine (40°), which is also what the source token gives for a 15 dp wavelength over the 22 dp
/// radius, so the crest count stays nine and the default amplitude rounds the measured 1.95 dp
/// to 2 dp. The videos draw the wave as a rounded star; a cosine with the same count, depth and
/// phase speed is used here.</para>
/// </remarks>
internal static class ProgressWave
{
    /// <summary>Source wave band height of the linear building blocks, in dp.</summary>
    private const double LinearBand = 12;

    /// <summary>Centreline amplitude of the source 40 dp wavelength wave, in dp.</summary>
    private const double Amplitude40 = 2.7714;

    /// <summary>Centreline amplitude of the source 20 dp wavelength wave, in dp.</summary>
    private const double Amplitude20 = 2.34;

    /// <summary>Lobes around the source circular indicator: crest every 40°.</summary>
    public const int CircularLobes = 9;

    /// <summary>Source circular artboard size that the circular wave defaults refer to.</summary>
    public const double CircularArtboard = 48;

    /// <summary>
    /// Centreline amplitude of a linear wave. Independent of the stroke thickness, which the
    /// source only uses to widen the stroke; the band height scales the whole wave. The source
    /// defines two wavelengths, 20 and 40 dp, so values between them interpolate and values
    /// outside clamp to the nearest measured wave.
    /// </summary>
    public static double LinearAmplitude(double waveHeight, double wavelength)
    {
        if (!double.IsFinite(waveHeight) || waveHeight <= 0)
            return 0;
        double reference;
        if (!double.IsFinite(wavelength) || wavelength <= 20)
            reference = Amplitude20;
        else if (wavelength >= 40)
            reference = Amplitude40;
        else
            reference = Amplitude20 + (wavelength - 20) / 20 * (Amplitude40 - Amplitude20);
        return waveHeight / LinearBand * reference;
    }

    /// <summary>Centreline amplitude of the circular wave, scaled from the 48 dp artboard.</summary>
    public static double CircularAmplitude(double diameter, double sourceAmplitude) =>
        double.IsFinite(sourceAmplitude) && sourceAmplitude > 0 ? sourceAmplitude * diameter / CircularArtboard : 0;

    /// <summary>
    /// Relative wave amplitude of a determinate indicator. The source's <c>indicatorAmplitude</c>
    /// drops the wave at 10% and below and from 95% on, so a completing indicator settles into a
    /// plain line, and animates the change over 500 ms; the guideline video shows exactly that
    /// envelope (flat up to ~6%, full by ~15%, flat again from ~95%). Expressing it as a function
    /// of progress keeps a static value stable while reproducing the animated ramps.
    /// </summary>
    public static double WaveEnvelope(double fraction)
    {
        if (!double.IsFinite(fraction))
            return 0;
        if (fraction <= 0.05 || fraction >= 0.98)
            return 0;
        if (fraction < 0.15)
            return SmoothStep((fraction - 0.05) / 0.10);
        if (fraction > 0.95)
            return 1 - SmoothStep((fraction - 0.95) / 0.03);
        return 1;
    }

    private static double SmoothStep(double value)
    {
        var t = Math.Clamp(value, 0, 1);
        return t * t * (3 - 2 * t);
    }
}

// One frame timer shared by every animating indicator; global accessibility notification is
// subscribed only while attached. The timer exists only while at least one indicator animates:
// an indeterminate indicator, or a determinate one whose wave still has to travel.
internal sealed class ProgressAnimationClock
{
    private static readonly List<ProgressAnimationClock> Running = new();
    private static DispatcherTimer? _timer;
    private readonly Control _owner;
    private readonly Func<bool> _shouldAnimate;
    private readonly List<Visual> _visibilityAncestors = new();
    private bool _attached;
    private bool _running;
    private long _started;
    public double ElapsedMilliseconds { get; private set; }

    public ProgressAnimationClock(Control owner, Func<bool> shouldAnimate)
    {
        _owner = owner;
        _shouldAnimate = shouldAnimate;
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

    /// <summary>Re-evaluates the owner's animate flag after a property this clock cannot observe.</summary>
    public void Refresh() => Update();

    private void AncestorPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Visual.IsVisibleProperty)
            Update();
    }

    private void Update()
    {
        var animate = _attached && _owner.IsEffectivelyVisible && _shouldAnimate() && !MaterialMotion.GetIsReduced(_owner);
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
