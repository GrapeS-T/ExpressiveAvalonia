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
using ExpressiveAvalonia.Shapes;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// Material 3 circular progress indicator: the pinned flat <c>CircularProgressIndicator</c> and
/// the expressive <c>CircularWavyProgressIndicator</c>.
/// </summary>
/// <remarks>
/// <para>Flat determinate geometry follows <c>ProgressIndicator.kt</c>: the active arc starts at
/// 12 o'clock, sweeps <c>progress × 360°</c>, and the track paints only the remainder, shortened
/// by <c>min(sweep, gapSweep)</c> on both sides. The gap grows by the stroke width unless the cap
/// is butt or the control is taller than it is wide, and the sweep is converted from the gap in
/// DIPs over the outer diameter, exactly as the source does.</para>
/// <para>Indeterminate flat timing is the source's 6000 ms cycle: a 1080° global rotation, four
/// 90° additional turns (300 ms linear move, then hold), and a sweep between 0.1 and 0.87 (linear
/// growth over 3000 ms, then the standard curve back).</para>
/// <para>The wavy form uses the source shape pipeline: a normalized <c>RoundedPolygon</c> circle
/// morphed into a star (<c>innerRadius 0.75</c>, <c>rounding 0.35/0.4</c>, <c>innerRounding
/// 0.5</c>) with <c>max(5, round(2πr / wavelength))</c> vertices. The path is scaled by
/// <c>size − stroke</c>, centered, trimmed by arc length, gap and cap insets are taken out of the
/// track segment only, and the wave travels by shifting one full outline and counter-rotating.
/// Matches the pinned helper chain rather than the earlier guideline-video cosine
/// approximation.</para>
/// <para>Determinate amplitude changes are tweened over 500 ms with the source increasing
/// (standard) and decreasing (emphasized-accelerate) curves. Motion runs only while the control is
/// attached, effectively visible and not reduced; no hidden timer survives a detached page.</para>
/// </remarks>
public sealed class CircularProgressIndicator : RangeBase
{
    public static readonly StyledProperty<bool> IsIndeterminateProperty =
        AvaloniaProperty.Register<CircularProgressIndicator, bool>(nameof(IsIndeterminate));
    /// <summary>Active stroke width. Source: <c>CircularProgressIndicatorTokens.ActiveThickness</c> = 4.</summary>
    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<CircularProgressIndicator, double>(nameof(StrokeThickness), 4,
            validate: value => double.IsFinite(value) && value > 0);
    /// <summary>Track stroke width of the wavy form. Source: <c>TrackThickness</c> = 4.</summary>
    public static readonly StyledProperty<double> TrackThicknessProperty =
        AvaloniaProperty.Register<CircularProgressIndicator, double>(nameof(TrackThickness), 4,
            validate: value => double.IsFinite(value) && value > 0);
    /// <summary>Active stroke cap. Source default: round.</summary>
    public static readonly StyledProperty<PenLineCap> StrokeCapProperty =
        AvaloniaProperty.Register<CircularProgressIndicator, PenLineCap>(nameof(StrokeCap), PenLineCap.Round);
    /// <summary>Wavy track stroke cap. Source default: round.</summary>
    public static readonly StyledProperty<PenLineCap> TrackStrokeCapProperty =
        AvaloniaProperty.Register<CircularProgressIndicator, PenLineCap>(nameof(TrackStrokeCap), PenLineCap.Round);
    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<CircularProgressIndicator, IBrush?>(nameof(TrackBrush));
    /// <summary>Gap between the active arc and the track. Source: <c>TrackActiveSpace</c> = 4.</summary>
    public static readonly StyledProperty<double> GapSizeProperty =
        AvaloniaProperty.Register<CircularProgressIndicator, double>(nameof(GapSize), 4,
            validate: value => double.IsFinite(value) && value >= 0);
    public static readonly StyledProperty<ProgressIndicatorType> IndicatorTypeProperty =
        AvaloniaProperty.Register<CircularProgressIndicator, ProgressIndicatorType>(nameof(IndicatorType));
    /// <summary>Wavelength in DIPs; the vertex count is derived from it. Source: 15.</summary>
    public static readonly StyledProperty<double> WavelengthProperty =
        AvaloniaProperty.Register<CircularProgressIndicator, double>(nameof(Wavelength), 15,
            validate: value => double.IsFinite(value) && value > 0);
    /// <summary>
    /// Wave speed in DIPs per second. NaN (the default) resolves to <see cref="Wavelength"/>, which
    /// is the source default argument "one wavelength per second".
    /// </summary>
    public static readonly StyledProperty<double> WaveSpeedProperty =
        AvaloniaProperty.Register<CircularProgressIndicator, double>(nameof(WaveSpeed), double.NaN,
            validate: value => double.IsNaN(value) || (double.IsFinite(value) && value >= 0));
    /// <summary>Fixed amplitude of the indeterminate wavy form (0..1). Source default: 1.</summary>
    public static readonly StyledProperty<double> AmplitudeProperty =
        AvaloniaProperty.Register<CircularProgressIndicator, double>(nameof(Amplitude), 1,
            validate: value => double.IsFinite(value));
    /// <summary>
    /// Determinate amplitude as a function of progress; null uses the source default that flattens
    /// at or below 10% and from 95%.
    /// </summary>
    public static readonly StyledProperty<Func<double, double>?> AmplitudeFunctionProperty =
        AvaloniaProperty.Register<CircularProgressIndicator, Func<double, double>?>(nameof(AmplitudeFunction));
    /// <summary>
    /// Project extension: when false the track is not painted. The source equivalent is a
    /// transparent <c>trackColor</c>; this boolean exists because the native control has no way to
    /// know which default colour role the caller wants.
    /// </summary>
    public static readonly StyledProperty<bool> ShowTrackProperty =
        AvaloniaProperty.Register<CircularProgressIndicator, bool>(nameof(ShowTrack), true);

    private readonly ProgressAnimationClock _clock;
    private readonly ProgressAmplitudeAnimator _amplitude;
    private readonly CircularWaveGeometry _waveGeometry = new();
    private readonly Pen _activePen = new();
    private readonly Pen _trackPen = new();
    private readonly Pen _flatTrackPen = new();
    private IBrush? _defaultTrackBrush;
    private bool _hasDefaultTrackBrush;
    private double _wavePhase;
    private double _lastClockMilliseconds;

    static CircularProgressIndicator()
    {
        ValueProperty.OverrideMetadata<CircularProgressIndicator>(new(defaultBindingMode: BindingMode.OneWay));
        AffectsRender<CircularProgressIndicator>(ValueProperty, MinimumProperty, MaximumProperty,
            IsIndeterminateProperty, StrokeThicknessProperty, TrackThicknessProperty, StrokeCapProperty,
            TrackStrokeCapProperty, TrackBrushProperty, GapSizeProperty, IndicatorTypeProperty,
            WavelengthProperty, WaveSpeedProperty, AmplitudeProperty, AmplitudeFunctionProperty,
            ShowTrackProperty, ForegroundProperty, FlowDirectionProperty);
        AffectsMeasure<CircularProgressIndicator>(IndicatorTypeProperty);
    }

    public CircularProgressIndicator()
    {
        _clock = new ProgressAnimationClock(this, ShouldAnimate);
        _amplitude = new ProgressAmplitudeAnimator(this, InvalidateVisual, () => _clock.Refresh());
        AttachedToVisualTree += (_, _) => UpdateAmplitude();
        ResourcesChanged += (_, _) =>
        {
            _hasDefaultTrackBrush = false;
            _defaultTrackBrush = null;
            InvalidateVisual();
        };
        ActualThemeVariantChanged += (_, _) =>
        {
            _hasDefaultTrackBrush = false;
            _defaultTrackBrush = null;
            InvalidateVisual();
        };
    }

    public bool IsIndeterminate { get => GetValue(IsIndeterminateProperty); set => SetValue(IsIndeterminateProperty, value); }
    public double StrokeThickness { get => GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }
    public double TrackThickness { get => GetValue(TrackThicknessProperty); set => SetValue(TrackThicknessProperty, value); }
    public PenLineCap StrokeCap { get => GetValue(StrokeCapProperty); set => SetValue(StrokeCapProperty, value); }
    public PenLineCap TrackStrokeCap { get => GetValue(TrackStrokeCapProperty); set => SetValue(TrackStrokeCapProperty, value); }
    public IBrush? TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }
    public double GapSize { get => GetValue(GapSizeProperty); set => SetValue(GapSizeProperty, value); }

    /// <summary>Flat (<c>CircularProgressIndicator</c>) or wavy (<c>CircularWavyProgressIndicator</c>).</summary>
    public ProgressIndicatorType IndicatorType
    {
        get => GetValue(IndicatorTypeProperty);
        set => SetValue(IndicatorTypeProperty, value);
    }

    public double Wavelength { get => GetValue(WavelengthProperty); set => SetValue(WavelengthProperty, value); }
    public double WaveSpeed { get => GetValue(WaveSpeedProperty); set => SetValue(WaveSpeedProperty, value); }
    public double Amplitude { get => GetValue(AmplitudeProperty); set => SetValue(AmplitudeProperty, value); }
    public Func<double, double>? AmplitudeFunction { get => GetValue(AmplitudeFunctionProperty); set => SetValue(AmplitudeFunctionProperty, value); }
    public bool ShowTrack { get => GetValue(ShowTrackProperty); set => SetValue(ShowTrackProperty, value); }

    /// <summary>Clamped range fraction; also drives the determinate amplitude function.</summary>
    private double Fraction => Maximum <= Minimum
        ? 1
        : Math.Clamp((Value - Minimum) / (Maximum - Minimum), 0, 1);

    private double EffectiveWaveSpeed => double.IsNaN(WaveSpeed) ? Wavelength : WaveSpeed;

    /// <summary>
    /// The indeterminate timelines always run; a determinate wave needs the shared clock only
    /// while an amplitude is visible and the wave can travel. Amplitude transitions are driven by
    /// their own 500 ms tween.
    /// </summary>
    private bool ShouldAnimate()
    {
        if (IsIndeterminate)
            return true;
        return IndicatorType == ProgressIndicatorType.Wave &&
               _amplitude.Value > 0 &&
               EffectiveWaveSpeed > 0;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsIndeterminateProperty)
        {
            PseudoClasses.Set(":indeterminate", IsIndeterminate);
            // The source forms are separate composables; switching between them starts a fresh
            // node whose amplitude snaps to its own target and whose wave offset starts at zero.
            ResetMotion();
        }
        else if (change.Property == IndicatorTypeProperty)
        {
            ResetMotion();
        }
        else if (change.Property == WavelengthProperty || change.Property == WaveSpeedProperty ||
                 change.Property == AmplitudeProperty || change.Property == AmplitudeFunctionProperty ||
                 change.Property == StrokeThicknessProperty || change.Property == TrackThicknessProperty)
        {
            _clock?.Refresh();
        }
        if (change.Property == ValueProperty || change.Property == MinimumProperty ||
            change.Property == MaximumProperty || change.Property == IsIndeterminateProperty ||
            change.Property == IndicatorTypeProperty || change.Property == AmplitudeProperty ||
            change.Property == AmplitudeFunctionProperty)
            UpdateAmplitude();
    }

    private void UpdateAmplitude()
    {
        if (_amplitude is not null && this.IsAttachedToVisualTree())
            _amplitude.SetTarget(IsIndeterminate ? Amplitude :
                (AmplitudeFunction ?? ProgressSource.DefaultAmplitude)(Fraction));
    }

    private void ResetMotion()
    {
        _amplitude.Reset();
        _wavePhase = 0;
        _lastClockMilliseconds = 0;
        _waveGeometry.Invalidate();
        _clock?.Refresh();
        InvalidateVisual();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        // Source node scope is cancelled on detach: the amplitude animatable is dropped and the
        // next attach snaps to the current target. The accumulated wave offset is retained.
        _amplitude.Reset();
        base.OnDetachedFromVisualTree(e);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var side = IndicatorType == ProgressIndicatorType.Wave ? 48 : 40;
        return new Size(side, side);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new CircularProgressAutomationPeer(this);

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= 0 || height <= 0)
            return;

        if (IndicatorType == ProgressIndicatorType.Wave)
            RenderWavy(context, width, height);
        else
            RenderFlat(context, width, height);
    }

    private void RenderFlat(DrawingContext context, double width, double height)
    {
        var diameter = Math.Min(width, height);
        var strokeWidth = Math.Min(StrokeThickness, diameter);
        var radius = (diameter - strokeWidth) / 2;
        if (radius <= 0)
            return;

        var center = new Point(width / 2, height / 2);
        var trackBrush = EffectiveTrackBrush();

        _activePen.Brush = Foreground;
        _activePen.Thickness = strokeWidth;
        _activePen.LineCap = StrokeCap;
        _flatTrackPen.Brush = trackBrush;
        _flatTrackPen.Thickness = strokeWidth;
        _flatTrackPen.LineCap = StrokeCap;

        var adjustedGap = StrokeCap == PenLineCap.Flat || height > width
            ? GapSize
            : GapSize + strokeWidth;
        var gapSweep = diameter > 0 ? adjustedGap * 360 / (Math.PI * diameter) : 0;
        var reduced = MaterialMotion.GetIsReduced(this);

        double startDegrees;
        double sweepDegrees;
        if (IsIndeterminate)
        {
            if (reduced)
            {
                startDegrees = 0;
                sweepDegrees = 270;
            }
            else
            {
                var time = _clock.ElapsedMilliseconds % ProgressSource.CircularDuration;
                startDegrees = ProgressSource.IndeterminateRotationDegrees(time);
                sweepDegrees = ProgressSource.IndeterminateSweep(time) * 360;
            }
        }
        else
        {
            startDegrees = 270;
            sweepDegrees = Fraction * 360;
        }

        var gap = Math.Min(sweepDegrees, gapSweep);
        if (IsBrushVisible(trackBrush))
        {
            var trackSweep = 360 - sweepDegrees - gap * 2;
            if (trackSweep > 0)
                DrawCircularArc(context, _flatTrackPen, center, radius,
                    DegreesToRadians(startDegrees + sweepDegrees + gap), DegreesToRadians(trackSweep));
        }

        if (sweepDegrees > 0 && IsBrushVisible(Foreground))
            DrawCircularArc(context, _activePen, center, radius, DegreesToRadians(startDegrees),
                DegreesToRadians(Math.Min(sweepDegrees, 360)));
    }

    private void RenderWavy(DrawingContext context, double width, double height)
    {
        var reduced = MaterialMotion.GetIsReduced(this);
        var activeStroke = StrokeThickness;
        var trackStroke = TrackThickness;
        if (width <= activeStroke || height <= activeStroke)
            return;

        var wavelength = Wavelength;
        var speed = EffectiveWaveSpeed;
        var amplitude = _amplitude.Value;

        _waveGeometry.Update(new Size(width, height), wavelength, activeStroke, amplitude);
        AdvanceWavePhase(speed, wavelength, _waveGeometry.VertexCount, amplitude);

        var center = new Point(width / 2, height / 2);
        var activeBrush = Foreground;
        var trackBrush = EffectiveTrackBrush();
        var capWidth = (StrokeCap == PenLineCap.Flat && TrackStrokeCap == PenLineCap.Flat) || height > width
            ? 0
            : Math.Max(activeStroke / 2, trackStroke / 2);

        double endProgress;
        double outerRotation;
        if (IsIndeterminate)
        {
            if (reduced)
            {
                endProgress = 0.75;
                outerRotation = 90;
            }
            else
            {
                var time = _clock.ElapsedMilliseconds % ProgressSource.CircularDuration;
                endProgress = ProgressSource.IndeterminateSweep(time);
                outerRotation = ProgressSource.IndeterminateRotationDegrees(time) + 90;
            }
        }
        else
        {
            endProgress = Fraction;
            outerRotation = 0;
        }

        var effectiveEnd = Math.Clamp(endProgress, 0, 1) * _waveGeometry.ProgressLength;
        var trackGap = Math.Min(effectiveEnd, GapSize);
        var insets = Math.Min(effectiveEnd, capWidth);
        var trackSpacing = insets * 2 + trackGap;

        _trackPen.Brush = trackBrush;
        _trackPen.Thickness = trackStroke;
        _trackPen.LineCap = TrackStrokeCap;
        _activePen.Brush = activeBrush;
        _activePen.Thickness = activeStroke;
        _activePen.LineCap = StrokeCap;

        if (IsBrushVisible(trackBrush) && _waveGeometry.TrackLength > 0)
        {
            var tStart = endProgress * _waveGeometry.TrackLength + trackSpacing;
            var tStop = _waveGeometry.TrackLength - trackSpacing;
            if (tStop > tStart)
            {
                var track = CircularWaveGeometry.CreateSegment(_waveGeometry.TrackCubics,
                    _waveGeometry.TrackLengths, _waveGeometry.TrackLength, tStart, tStop - tStart,
                    outerRotation, center);
                if (track is not null)
                    context.DrawGeometry(trackBrush, _trackPen, track);
            }
        }

        if (IsBrushVisible(activeBrush) && effectiveEnd > 0)
        {
            var motion = !reduced && speed > 0 && amplitude > 0;
            var start = motion ? _wavePhase * _waveGeometry.ProgressLength : 0;
            var progress = CircularWaveGeometry.CreateSegment(_waveGeometry.ProgressCubics,
                _waveGeometry.ProgressLengths, _waveGeometry.ProgressLength, start, effectiveEnd,
                outerRotation + (motion ? -_wavePhase * 360 : 0), center);
            if (progress is not null)
                context.DrawGeometry(activeBrush, _activePen, progress);
        }
    }

    private void AdvanceWavePhase(double speed, double wavelength, int vertexCount, double amplitude)
    {
        var now = _clock.ElapsedMilliseconds;
        var delta = now >= _lastClockMilliseconds ? (now - _lastClockMilliseconds) / 1000 : 0;
        _lastClockMilliseconds = now;
        if (speed <= 0 || wavelength <= 0 || vertexCount <= 0 || amplitude <= 0 || delta <= 0)
            return;
        // Source duration: wavelength / speed × vertex count, at least 50 ms. One unit of the
        // offset state therefore advances the outline by one full wavelength.
        var durationSeconds = Math.Max(0.05, wavelength / speed * vertexCount);
        _wavePhase += delta / durationSeconds;
        _wavePhase -= Math.Floor(_wavePhase);
    }

    private static bool IsBrushVisible(IBrush? brush) =>
        brush is not null && (brush is not ISolidColorBrush solid || solid.Color.A > 0);

    /// <summary>
    /// Resolves the source track default when no brush is supplied: ProgressIndicatorTokens
    /// TrackColor for every determinate form and for the wavy indeterminate form, and transparent
    /// for the flat indeterminate form (ProgressIndicatorDefaults).
    /// </summary>
    private IBrush? EffectiveTrackBrush()
    {
        if (!ShowTrack)
            return null;
        if (TrackBrush is { } brush)
            return brush;
        if (IsIndeterminate && IndicatorType == ProgressIndicatorType.Flat)
            return null;
        if (!_hasDefaultTrackBrush)
        {
            _hasDefaultTrackBrush = true;
            _defaultTrackBrush = this.TryFindResource("MaterialSecondaryContainerBrush", ActualThemeVariant, out var value) &&
                                 value is IBrush role
                ? role
                : null;
        }
        return _defaultTrackBrush;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180;

    private static void DrawCircularArc(DrawingContext context, Pen pen, Point center, double radius,
        double start, double sweep)
    {
        if (radius <= 0 || sweep <= 0)
            return;
        if (sweep >= Math.Tau - 0.0001)
        {
            context.DrawEllipse(null, pen, center, radius, radius);
            return;
        }

        // Each submitted path is immutable after Render: Composition keeps it on the render thread.
        var geometry = new StreamGeometry();
        using (var path = geometry.Open())
        {
            path.BeginFigure(new Point(center.X + radius * Math.Cos(start),
                center.Y + radius * Math.Sin(start)), false);
            var end = start + sweep;
            path.ArcTo(new Point(center.X + radius * Math.Cos(end),
                center.Y + radius * Math.Sin(end)), new Size(radius, radius), 0, sweep > Math.PI,
                SweepDirection.Clockwise);
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

/// <summary>
/// Drawing surface used by the native <see cref="ProgressBar"/> template. Range, keyboard input and
/// automation stay on the templated parent; only the source linear geometry lives here.
/// </summary>
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
    public static readonly StyledProperty<double> TrackThicknessProperty =
        AvaloniaProperty.Register<LinearProgressPresenter, double>(nameof(TrackThickness), 4,
            validate: value => double.IsFinite(value) && value > 0);
    public static readonly StyledProperty<double> WaveHeightProperty =
        AvaloniaProperty.Register<LinearProgressPresenter, double>(nameof(WaveHeight), 10,
            validate: value => double.IsFinite(value) && value >= 0);
    public static readonly StyledProperty<double> WavelengthProperty =
        AvaloniaProperty.Register<LinearProgressPresenter, double>(nameof(Wavelength), 40,
            validate: value => double.IsFinite(value) && value > 0);
    public static readonly StyledProperty<double> IndeterminateWavelengthProperty =
        AvaloniaProperty.Register<LinearProgressPresenter, double>(nameof(IndeterminateWavelength), 20,
            validate: value => double.IsFinite(value) && value > 0);
    public static readonly StyledProperty<double> GapSizeProperty =
        AvaloniaProperty.Register<LinearProgressPresenter, double>(nameof(GapSize), 4,
            validate: value => double.IsFinite(value) && value >= 0);
    public static readonly StyledProperty<double> StopSizeProperty =
        AvaloniaProperty.Register<LinearProgressPresenter, double>(nameof(StopSize), 4,
            validate: value => double.IsFinite(value) && value >= 0);
    public static readonly StyledProperty<PenLineCap> StrokeCapProperty =
        AvaloniaProperty.Register<LinearProgressPresenter, PenLineCap>(nameof(StrokeCap), PenLineCap.Round);
    public static readonly StyledProperty<PenLineCap> TrackStrokeCapProperty =
        AvaloniaProperty.Register<LinearProgressPresenter, PenLineCap>(nameof(TrackStrokeCap), PenLineCap.Round);
    public static readonly StyledProperty<double> WaveSpeedProperty =
        AvaloniaProperty.Register<LinearProgressPresenter, double>(nameof(WaveSpeed), double.NaN,
            validate: value => double.IsNaN(value) || (double.IsFinite(value) && value >= 0));
    public static readonly StyledProperty<double> AmplitudeProperty =
        AvaloniaProperty.Register<LinearProgressPresenter, double>(nameof(Amplitude), 1,
            validate: value => double.IsFinite(value));
    public static readonly StyledProperty<Func<double, double>?> AmplitudeFunctionProperty =
        AvaloniaProperty.Register<LinearProgressPresenter, Func<double, double>?>(nameof(AmplitudeFunction));

    private readonly ProgressAnimationClock _clock;
    private readonly ProgressAmplitudeAnimator _amplitude;
    private readonly Pen _activePen = new();
    private readonly Pen _trackPen = new();
    private readonly Pen _buttPen = new() { LineCap = PenLineCap.Flat };
    private double _wavePhase;
    private double _lastClockMilliseconds;

    static LinearProgressPresenter()
    {
        AffectsRender<LinearProgressPresenter>(MinimumProperty, MaximumProperty,
            ValueProperty, IsIndeterminateProperty, OrientationProperty, IndicatorBrushProperty,
            TrackBrushProperty, FlowDirectionProperty, IndicatorTypeProperty, IndicatorThicknessProperty,
            TrackThicknessProperty, WaveHeightProperty, WavelengthProperty, IndeterminateWavelengthProperty,
            GapSizeProperty, StopSizeProperty, StrokeCapProperty, TrackStrokeCapProperty, WaveSpeedProperty,
            AmplitudeProperty, AmplitudeFunctionProperty);
        AffectsMeasure<LinearProgressPresenter>(IndicatorTypeProperty, IndicatorThicknessProperty,
            TrackThicknessProperty, WaveHeightProperty, WavelengthProperty, IndeterminateWavelengthProperty);
    }

    public LinearProgressPresenter()
    {
        ClipToBounds = true;
        _clock = new ProgressAnimationClock(this, ShouldAnimate);
        _amplitude = new ProgressAmplitudeAnimator(this, InvalidateVisual, () => _clock.Refresh());
        AttachedToVisualTree += (_, _) => UpdateAmplitude();
    }

    public double Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public double Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public bool IsIndeterminate { get => GetValue(IsIndeterminateProperty); set => SetValue(IsIndeterminateProperty, value); }
    public Orientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }
    public IBrush? IndicatorBrush { get => GetValue(IndicatorBrushProperty); set => SetValue(IndicatorBrushProperty, value); }
    public IBrush? TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }
    public ProgressIndicatorType IndicatorType { get => GetValue(IndicatorTypeProperty); set => SetValue(IndicatorTypeProperty, value); }
    public double IndicatorThickness { get => GetValue(IndicatorThicknessProperty); set => SetValue(IndicatorThicknessProperty, value); }
    public double TrackThickness { get => GetValue(TrackThicknessProperty); set => SetValue(TrackThicknessProperty, value); }
    public double WaveHeight { get => GetValue(WaveHeightProperty); set => SetValue(WaveHeightProperty, value); }
    public double Wavelength { get => GetValue(WavelengthProperty); set => SetValue(WavelengthProperty, value); }
    public double IndeterminateWavelength { get => GetValue(IndeterminateWavelengthProperty); set => SetValue(IndeterminateWavelengthProperty, value); }
    public double GapSize { get => GetValue(GapSizeProperty); set => SetValue(GapSizeProperty, value); }
    public double StopSize { get => GetValue(StopSizeProperty); set => SetValue(StopSizeProperty, value); }
    public PenLineCap StrokeCap { get => GetValue(StrokeCapProperty); set => SetValue(StrokeCapProperty, value); }
    public PenLineCap TrackStrokeCap { get => GetValue(TrackStrokeCapProperty); set => SetValue(TrackStrokeCapProperty, value); }
    public double WaveSpeed { get => GetValue(WaveSpeedProperty); set => SetValue(WaveSpeedProperty, value); }
    public double Amplitude { get => GetValue(AmplitudeProperty); set => SetValue(AmplitudeProperty, value); }
    public Func<double, double>? AmplitudeFunction { get => GetValue(AmplitudeFunctionProperty); set => SetValue(AmplitudeFunctionProperty, value); }

    private double Fraction => Maximum <= Minimum
        ? 1
        : Math.Clamp((Value - Minimum) / (Maximum - Minimum), 0, 1);

    private double EffectiveWavelength => IsIndeterminate ? IndeterminateWavelength : Wavelength;

    private double EffectiveWaveSpeed => double.IsNaN(WaveSpeed) ? EffectiveWavelength : WaveSpeed;

    private bool ShouldAnimate()
    {
        if (IsIndeterminate)
            return true;
        return IndicatorType == ProgressIndicatorType.Wave &&
               _amplitude.Value > 0 &&
               EffectiveWaveSpeed > 0;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsIndeterminateProperty || change.Property == IndicatorTypeProperty)
        {
            _amplitude.Reset();
            _wavePhase = 0;
            _lastClockMilliseconds = 0;
            _clock?.Refresh();
            InvalidateMeasure();
            InvalidateVisual();
        }
        else if (change.Property == WavelengthProperty || change.Property == IndeterminateWavelengthProperty ||
                 change.Property == WaveSpeedProperty ||
                 change.Property == AmplitudeProperty || change.Property == AmplitudeFunctionProperty ||
                 change.Property == IndicatorThicknessProperty || change.Property == TrackThicknessProperty)
        {
            _clock?.Refresh();
        }
        if (change.Property == ValueProperty || change.Property == MinimumProperty ||
            change.Property == MaximumProperty || change.Property == IsIndeterminateProperty ||
            change.Property == IndicatorTypeProperty || change.Property == AmplitudeProperty ||
            change.Property == AmplitudeFunctionProperty)
            UpdateAmplitude();
    }

    private void UpdateAmplitude()
    {
        if (_amplitude is not null && this.IsAttachedToVisualTree())
            _amplitude.SetTarget(IsIndeterminate ? Amplitude :
                (AmplitudeFunction ?? ProgressSource.DefaultAmplitude)(Fraction));
    }

    /// <summary>The source container height: thickness for the flat form, the wave band otherwise.</summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        var band = IndicatorType == ProgressIndicatorType.Wave ? WaveHeight : IndicatorThickness;
        return Orientation == Orientation.Vertical ? new Size(band, 0) : new Size(0, band);
    }

    // The source parameters live on the templated ProgressBar as attached properties. Reading them
    // here keeps the native control free of extra styled properties and avoids a binding hop on
    // every measure and render.
    private ProgressBar? _owner;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
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
        // Source node scope is cancelled on detach: the next attach snaps to the target amplitude.
        _amplitude.Reset();
        base.OnDetachedFromVisualTree(e);
    }

    private void OwnerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == MaterialProgress.IndicatorTypeProperty)
        {
            _amplitude.Reset();
            _wavePhase = 0;
            _lastClockMilliseconds = 0;
        }
        if (e.Property == MaterialProgress.IndicatorTypeProperty ||
            e.Property == MaterialProgress.ThicknessProperty ||
            e.Property == MaterialProgress.TrackThicknessProperty ||
            e.Property == MaterialProgress.WaveHeightProperty ||
            e.Property == MaterialProgress.WavelengthProperty ||
            e.Property == MaterialProgress.IndeterminateWavelengthProperty ||
            e.Property == MaterialProgress.GapSizeProperty ||
            e.Property == MaterialProgress.StopSizeProperty ||
            e.Property == MaterialProgress.StrokeCapProperty ||
            e.Property == MaterialProgress.TrackStrokeCapProperty ||
            e.Property == MaterialProgress.WaveSpeedProperty ||
            e.Property == MaterialProgress.AmplitudeProperty ||
            e.Property == MaterialProgress.AmplitudeFunctionProperty)
        {
            SyncFromOwner();
            _clock?.Refresh();
        }
    }

    private void SyncFromOwner()
    {
        if (_owner is not { } owner)
            return;
        IndicatorType = MaterialProgress.GetIndicatorType(owner);
        IndicatorThickness = MaterialProgress.GetThickness(owner);
        TrackThickness = MaterialProgress.GetTrackThickness(owner);
        WaveHeight = MaterialProgress.GetWaveHeight(owner);
        Wavelength = MaterialProgress.GetWavelength(owner);
        IndeterminateWavelength = MaterialProgress.GetIndeterminateWavelength(owner);
        GapSize = MaterialProgress.GetGapSize(owner);
        StopSize = MaterialProgress.GetStopSize(owner);
        StrokeCap = MaterialProgress.GetStrokeCap(owner);
        TrackStrokeCap = MaterialProgress.GetTrackStrokeCap(owner);
        WaveSpeed = MaterialProgress.GetWaveSpeed(owner);
        Amplitude = MaterialProgress.GetAmplitude(owner);
        AmplitudeFunction = MaterialProgress.GetAmplitudeFunction(owner);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var vertical = Orientation == Orientation.Vertical;
        var length = vertical ? Bounds.Height : Bounds.Width;
        var band = vertical ? Bounds.Width : Bounds.Height;
        if (length <= 0 || band <= 0)
            return;

        if (IndicatorType == ProgressIndicatorType.Wave)
            RenderWavy(context, length, band, vertical);
        else
            RenderFlat(context, length, band, vertical);
    }

    private void RenderFlat(DrawingContext context, double length, double band, bool vertical)
    {
        // Source: strokeWidth = size.height, and the track uses the same stroke as the indicator.
        var strokeWidth = band;
        var adjustedGap = StrokeCap == PenLineCap.Flat || band > length ? GapSize : GapSize + strokeWidth;
        var gapFraction = adjustedGap / length;
        var reduced = MaterialMotion.GetIsReduced(this);
        var active = IsBrushVisible(IndicatorBrush) ? IndicatorBrush : null;
        var track = IsBrushVisible(TrackBrush) ? TrackBrush : null;

        _activePen.Brush = active;
        _activePen.Thickness = strokeWidth;
        _activePen.LineCap = StrokeCap;
        _buttPen.Thickness = strokeWidth;
        _buttPen.Brush = active;

        void Line(double first, double second, IBrush? brush)
        {
            if (brush is null || second <= first)
                return;
            DrawFlatSegment(context, brush, first, second, length, band, strokeWidth, vertical);
        }

        if (IsIndeterminate)
        {
            double firstHead, firstTail, secondHead, secondTail;
            if (reduced)
            {
                firstHead = 0.8;
                firstTail = 0.2;
                secondHead = secondTail = 0;
            }
            else
            {
                var time = _clock.ElapsedMilliseconds % ProgressSource.LinearDuration;
                firstHead = ProgressSource.LinearKeyframe(time, ProgressSource.FirstHeadDelay, ProgressSource.FirstHeadDuration);
                firstTail = ProgressSource.LinearKeyframe(time, ProgressSource.FirstTailDelay, ProgressSource.FirstTailDuration);
                secondHead = ProgressSource.LinearKeyframe(time, ProgressSource.SecondHeadDelay, ProgressSource.SecondHeadDuration);
                secondTail = ProgressSource.LinearKeyframe(time, ProgressSource.SecondTailDelay, ProgressSource.SecondTailDuration);
            }

            if (firstHead < 1 - gapFraction)
            {
                var start = firstHead > 0 ? firstHead + gapFraction : 0;
                Line(start, 1, track);
            }
            Line(firstTail, firstHead, active);
            if (firstTail > gapFraction)
            {
                var start = secondHead > 0 ? secondHead + gapFraction : 0;
                var end = firstTail < 1 ? firstTail - gapFraction : 1;
                Line(start, end, track);
            }
            Line(secondTail, secondHead, active);
            if (secondTail > gapFraction)
            {
                var end = secondTail < 1 ? secondTail - gapFraction : 1;
                Line(0, end, track);
            }
        }
        else
        {
            var progress = Fraction;
            var trackStart = progress + Math.Min(progress, gapFraction);
            if (trackStart <= 1)
                Line(trackStart, 1, track);
            Line(0, progress, active);
            DrawFlatStopIndicator(context, length, band, vertical);
        }
    }

    private void DrawFlatSegment(DrawingContext context, IBrush brush, double startFraction,
        double endFraction, double length, double band, double strokeWidth, bool vertical)
    {
        var rtl = !vertical && FlowDirection == FlowDirection.RightToLeft;
        var barStart = (rtl ? 1 - endFraction : startFraction) * length;
        var barEnd = (rtl ? 1 - startFraction : endFraction) * length;

        // Source: when there is not enough room to draw the caps, fall back to a butt line.
        if (StrokeCap == PenLineCap.Flat || band > length)
        {
            DrawLogicalLine(context, _buttPen, brush, barStart, barEnd, length, band, vertical);
            return;
        }

        var capOffset = strokeWidth / 2;
        var adjustedStart = Math.Clamp(barStart, capOffset, length - capOffset);
        var adjustedEnd = Math.Clamp(barEnd, capOffset, length - capOffset);
        if (endFraction > startFraction)
            DrawLogicalLine(context, _activePen, brush, adjustedStart, adjustedEnd, length, band, vertical);
    }

    private static void DrawLogicalLine(DrawingContext context, Pen pen, IBrush brush,
        double start, double end, double length, double band, bool vertical)
    {
        pen.Brush = brush;
        if (vertical)
            context.DrawLine(pen, new Point(band / 2, length - start),
                new Point(band / 2, length - end));
        else
            context.DrawLine(pen, new Point(start, band / 2), new Point(end, band / 2));
    }

    private void DrawFlatStopIndicator(DrawingContext context, double length, double band, bool vertical)
    {
        var brush = IndicatorBrush;
        if (!IsBrushVisible(brush))
            return;
        var size = Math.Min(StopSize, band);
        if (size <= 0)
            return;
        // ProgressIndicatorTokens have no stop offset; ProgressIndicator.kt caps the centering
        // offset at StopIndicatorTrailingSpace = 6 dp.
        var offset = Math.Min((band - size) / 2, 6);
        var centerX = length - size / 2 - offset;
        if (!vertical && FlowDirection == FlowDirection.RightToLeft)
            centerX = length - centerX;
        var center = vertical ? new Point(band / 2, length - centerX) : new Point(centerX, band / 2);
        if (StrokeCap == PenLineCap.Round)
            context.DrawEllipse(brush, null, center, size / 2, size / 2);
        else
            context.DrawRectangle(brush, null,
                new Rect(center.X - size / 2, center.Y - size / 2, size, size));
    }

    private void RenderWavy(DrawingContext context, double length, double band, bool vertical)
    {
        var reduced = MaterialMotion.GetIsReduced(this);
        var wavelength = EffectiveWavelength;
        var speed = EffectiveWaveSpeed;
        var activeStroke = IndicatorThickness;
        var trackStroke = TrackThickness;
        var amplitude = _amplitude.Value;

        AdvanceWavePhase(speed, wavelength);

        var active = IsBrushVisible(IndicatorBrush) ? IndicatorBrush : null;
        var track = IsBrushVisible(TrackBrush) ? TrackBrush : null;
        var capWidth = (StrokeCap == PenLineCap.Flat && TrackStrokeCap == PenLineCap.Flat) || band > length
            ? 0
            : Math.Max(activeStroke / 2, trackStroke / 2);

        var fractions = new double[4];
        int pairCount;
        if (IsIndeterminate)
        {
            if (reduced)
            {
                fractions[0] = 0.2;
                fractions[1] = 0.8;
                fractions[2] = 0;
                fractions[3] = 0;
            }
            else
            {
                var time = _clock.ElapsedMilliseconds % ProgressSource.LinearDuration;
                fractions[0] = ProgressSource.LinearKeyframe(time, ProgressSource.FirstTailDelay, ProgressSource.FirstTailDuration);
                fractions[1] = ProgressSource.LinearKeyframe(time, ProgressSource.FirstHeadDelay, ProgressSource.FirstHeadDuration);
                fractions[2] = ProgressSource.LinearKeyframe(time, ProgressSource.SecondTailDelay, ProgressSource.SecondTailDuration);
                fractions[3] = ProgressSource.LinearKeyframe(time, ProgressSource.SecondHeadDelay, ProgressSource.SecondHeadDuration);
            }
            pairCount = 2;
        }
        else
        {
            fractions[0] = 0;
            fractions[1] = Fraction;
            pairCount = 1;
        }

        _activePen.Brush = active;
        _activePen.Thickness = activeStroke;
        _activePen.LineCap = StrokeCap;
        _trackPen.Brush = track;
        _trackPen.Thickness = trackStroke;
        _trackPen.LineCap = TrackStrokeCap;

        if (!vertical && FlowDirection == FlowDirection.RightToLeft)
        {
            using (context.PushTransform(Matrix.CreateRotation(Math.PI,
                       new Point(Bounds.Width / 2, Bounds.Height / 2))))
            {
                DrawWavyCore(context, length, band, vertical, fractions, pairCount, active, track,
                    activeStroke, trackStroke, wavelength, amplitude, capWidth);
            }
        }
        else
        {
            DrawWavyCore(context, length, band, vertical, fractions, pairCount, active, track,
                activeStroke, trackStroke, wavelength, amplitude, capWidth);
        }
    }

    private void DrawWavyCore(DrawingContext context, double length, double band, bool vertical,
        double[] fractions, int pairCount, IBrush? active, IBrush? track, double activeStroke,
        double trackStroke, double wavelength, double amplitude, double capWidth)
    {
        if (track is not null)
        {
            // Track is a flat polyline; the adaptive gap around the active segments is rebuilt in
            // the same right-to-left pass as the source drawing cache. Each contiguous run is one
            // line; the source keeps them as separate contours of one path.
            var nextEndTrackOffset = length - capWidth;
            var adjustedTrackGapSize = 0d;
            var activeIndicatorVisible = false;
            for (var index = 0; index < pairCount; index++)
            {
                var barTail = fractions[index * 2] * length;
                var barHead = fractions[index * 2 + 1] * length;
                if (index == 0)
                {
                    adjustedTrackGapSize = barHead < capWidth
                        ? 0
                        : Math.Min(barHead - capWidth, GapSize);
                    activeIndicatorVisible = barHead >= capWidth;
                }
                var adjustedBarHead = Math.Clamp(barHead, capWidth, length - capWidth);
                var adjustedBarTail = Math.Clamp(barTail, capWidth, length - capWidth);
                var adaptiveTrackSpacing = activeIndicatorVisible
                    ? adjustedTrackGapSize + capWidth * 2
                    : adjustedTrackGapSize;
                var segmentStart = Math.Max(capWidth, adjustedBarHead + adaptiveTrackSpacing);
                if (nextEndTrackOffset > segmentStart)
                    context.DrawLine(_trackPen,
                        MapPoint(segmentStart, band / 2, length, vertical),
                        MapPoint(nextEndTrackOffset, band / 2, length, vertical));
                if (barHead > barTail)
                    nextEndTrackOffset = Math.Max(capWidth, adjustedBarTail - adaptiveTrackSpacing);
            }
            if (nextEndTrackOffset > capWidth)
                context.DrawLine(_trackPen,
                    MapPoint(capWidth, band / 2, length, vertical),
                    MapPoint(nextEndTrackOffset, band / 2, length, vertical));
        }

        if (active is null)
            return;

        for (var index = 0; index < pairCount; index++)
        {
            var startFraction = fractions[index * 2];
            var endFraction = fractions[index * 2 + 1];
            if (endFraction - startFraction <= 0)
                continue;
            var barTail = Math.Clamp(startFraction * length, capWidth, length - capWidth);
            var barHead = Math.Clamp(endFraction * length, capWidth, length - capWidth);
            var geometry = BuildLinearWave(barTail, barHead, length, band, activeStroke,
                wavelength, amplitude, vertical);
            if (geometry is not null)
                context.DrawGeometry(active, _activePen, geometry);
        }

        if (!IsIndeterminate)
            DrawWavyStopIndicator(context, length, band, vertical, fractions[1], capWidth, trackStroke, active);
    }

    private void DrawWavyStopIndicator(DrawingContext context, double length, double band, bool vertical,
        double progressEnd, double capWidth, double trackStroke, IBrush? brush)
    {
        if (brush is null)
            return;
        var stop = Math.Min(trackStroke, StopSize);
        var xOffset = stop == trackStroke ? 0 : trackStroke / 4;
        var indicatorX = length - stop - xOffset;
        var progressX = length * progressEnd + capWidth;
        if (indicatorX <= progressX)
        {
            stop = Math.Max(0, stop - (progressX - indicatorX));
            indicatorX = progressX;
        }
        if (stop <= 0)
            return;
        var centerX = indicatorX + stop / 2;
        var center = MapPoint(centerX, band / 2, length, vertical);
        if (TrackStrokeCap == PenLineCap.Round)
        {
            context.DrawEllipse(brush, null, center, stop / 2, stop / 2);
        }
        else
        {
            var topLeft = vertical
                ? new Point(center.X - stop / 2, center.Y - stop / 2)
                : new Point(indicatorX, (band - stop) / 2);
            context.DrawRectangle(brush, null, new Rect(topLeft, new Size(stop, stop)));
        }
    }

    private void AdvanceWavePhase(double speed, double wavelength)
    {
        var now = _clock.ElapsedMilliseconds;
        var delta = now >= _lastClockMilliseconds ? (now - _lastClockMilliseconds) / 1000 : 0;
        _lastClockMilliseconds = now;
        if (speed <= 0 || wavelength <= 0 || delta <= 0)
            return;
        // Source duration: one wavelength / speed seconds, at least 50 ms.
        var durationSeconds = Math.Max(0.05, wavelength / speed);
        _wavePhase += delta / durationSeconds;
        _wavePhase -= Math.Floor(_wavePhase);
    }

    private static Point MapPoint(double x, double y, double length, bool vertical) =>
        vertical ? new Point(y, length - x) : new Point(x, y);

    private static bool IsBrushVisible(IBrush? brush) =>
        brush is not null && (brush is not ISolidColorBrush solid || solid.Color.A > 0);

    /// <summary>
    /// Builds the source linear wave: quadratic anchors every half wavelength on the centreline and
    /// alternating controls at the quarter points of height − stroke. The phase shift slides the
    /// pattern towards the start, and the amplitude scales it around the band centre.
    /// </summary>
    private StreamGeometry? BuildLinearWave(double start, double end, double length, double band,
        double stroke, double wavelength, double amplitude, bool vertical)
    {
        if (end <= start || wavelength <= 0)
            return null;
        var half = wavelength / 2;
        // Source: the drawn segment is the full path translated back by waveOffset × wavelength,
        // so the wave pattern sampled at x is the pattern at x + shift. The offset advances one
        // wavelength per duration cycle.
        var shift = _wavePhase * wavelength;
        var controlHeight = band - stroke;
        var low = start + shift;
        var high = end + shift;
        var first = (int)Math.Floor(low / half);
        var last = (int)Math.Floor(high / half);
        var geometry = new StreamGeometry();
        var hasPoint = false;
        using (var path = geometry.Open())
        {
            for (var index = first; index <= last; index++)
            {
                var anchor0 = index * half;
                var anchor1 = anchor0 + half;
                var lo = Math.Max(low, anchor0);
                var hi = Math.Min(high, anchor1);
                if (hi - lo <= 1e-9)
                    continue;
                var t0 = (lo - anchor0) / half;
                var t1 = (hi - anchor0) / half;
                var sign = (index & 1) == 0 ? 1 : -1;
                var control = controlHeight * sign;
                var quad = new Quadratic(
                    new Point(0, 0),
                    new Point(half / 2, control),
                    new Point(half, 0));
                var part = quad.Sub(t0, t1);
                var p0 = WavePoint(part.P0, anchor0 - shift, length, band, amplitude, vertical);
                var p1 = WavePoint(part.P1, anchor0 - shift, length, band, amplitude, vertical);
                var p2 = WavePoint(part.P2, anchor0 - shift, length, band, amplitude, vertical);
                if (!hasPoint)
                {
                    path.BeginFigure(p0, false);
                    hasPoint = true;
                }
                path.QuadraticBezierTo(p1, p2);
            }
            if (!hasPoint)
                return null;
            path.EndFigure(false);
        }
        return geometry;
    }

    private static Point WavePoint(Point point, double xOffset, double length, double band,
        double amplitude, bool vertical)
    {
        // Raw path y is centred on zero with peak-to-peak height − stroke; the source translates
        // the path to the band centre and then scales it towards that centre by the amplitude.
        var x = point.X + xOffset;
        var y = band / 2 + amplitude * point.Y;
        return vertical ? new Point(y, length - x) : new Point(x, y);
    }

    private readonly struct Quadratic
    {
        public Quadratic(Point p0, Point p1, Point p2)
        {
            P0 = p0;
            P1 = p1;
            P2 = p2;
        }

        public Point P0 { get; }
        public Point P1 { get; }
        public Point P2 { get; }

        public (Quadratic Start, Quadratic End) Split(double t)
        {
            var a = Lerp(P0, P1, t);
            var b = Lerp(P1, P2, t);
            var c = Lerp(a, b, t);
            return (new Quadratic(P0, a, c), new Quadratic(c, b, P2));
        }

        public Quadratic Sub(double t0, double t1)
        {
            var (head, _) = Split(t1);
            if (t0 <= 0)
                return head;
            var (_, tail) = head.Split(t0 / t1);
            return tail;
        }

        private static Point Lerp(Point a, Point b, double t) =>
            new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
    }
}

/// <summary>
/// Source constants and pure timelines shared by the flat and wavy progress ports.
/// </summary>
internal static class ProgressSource
{
    // ProgressIndicator.kt at the pinned snapshot.
    public const double LinearDuration = 1750;
    public const double FirstHeadDuration = 1000;
    public const double FirstTailDuration = 1000;
    public const double SecondHeadDuration = 850;
    public const double SecondTailDuration = 850;
    public const double FirstHeadDelay = 0;
    public const double FirstTailDelay = 250;
    public const double SecondHeadDelay = 650;
    public const double SecondTailDelay = 900;

    public const double CircularDuration = 6000;
    public const double CircularAdditionalRotationDelay = 1500;
    public const double CircularAdditionalRotationDuration = 300;
    public const double CircularAdditionalRotationTarget = 360;
    public const double CircularGlobalRotationTarget = 1080;
    public const double CircularMinProgress = 0.1;
    public const double CircularMaxProgress = 0.87;

    private static readonly SplineEasing EmphasizedAccelerate = new(0.3, 0, 0.8, 0.15);
    private static readonly SplineEasing Standard = new(0.2, 0, 0, 1);

    /// <summary>WavyProgressIndicatorDefaults.indicatorAmplitude.</summary>
    public static double DefaultAmplitude(double progress) =>
        progress <= 0.1 || progress >= 0.95 ? 0 : 1;

    /// <summary>
    /// One keyframe interval: the value is zero through the delay, eases to one over the duration,
    /// then holds. The source attaches the emphasized-accelerate curve to the zero keyframe, so it
    /// governs the whole interval.
    /// </summary>
    public static double LinearKeyframe(double time, double delay, double duration)
    {
        if (time <= delay)
            return 0;
        var fraction = (time - delay) / duration;
        return fraction >= 1 ? 1 : EmphasizedAccelerate.Ease(fraction);
    }

    /// <summary>Global rotation plus the four additional 90° turns, in degrees.</summary>
    public static double IndeterminateRotationDegrees(double time)
    {
        var global = time / CircularDuration * CircularGlobalRotationTarget;
        var turn = Math.Floor(time / CircularAdditionalRotationDelay);
        var within = time - turn * CircularAdditionalRotationDelay;
        var eased = Math.Min(within / CircularAdditionalRotationDuration, 1);
        return global + (turn + eased) * (CircularAdditionalRotationTarget / 4);
    }

    /// <summary>Sweep of one indeterminate cycle: linear growth, standard-curve return.</summary>
    public static double IndeterminateSweep(double time)
    {
        var half = CircularDuration / 2;
        if (time < half)
            return CircularMinProgress +
                   (CircularMaxProgress - CircularMinProgress) * (time / half);
        var fraction = Math.Clamp((time - half) / half, 0, 1);
        return CircularMaxProgress -
               (CircularMaxProgress - CircularMinProgress) * Standard.Ease(fraction);
    }
}

/// <summary>
/// Retargetable 500 ms amplitude tween with the source's increasing/decreasing curves.
/// </summary>
/// <remarks>
/// The source ignores a target change while an amplitude animation is still running and starts the
/// next 500 ms animation from the completed value, so rapid progress reversals do not restart the
/// curve. The shared <see cref="MaterialMotion.Tween"/> owns frame requests, detach and reduced
/// motion; this wrapper only tracks the source's "animation running" rule.
/// </remarks>
internal sealed class ProgressAmplitudeAnimator
{
    private static readonly SplineEasing Increasing = new(0.2, 0, 0, 1);
    private static readonly SplineEasing Decreasing = new(0.3, 0, 0.8, 0.15);

    private readonly Control _owner;
    private readonly Action _changed;
    private readonly Action _settled;
    private MaterialMotion.TweenAnimation? _tween;
    private double _value;
    private double _animationTarget;
    private double _requestedTarget;
    private bool _initialized;
    private bool _animating;

    public ProgressAmplitudeAnimator(Control owner, Action changed, Action settled)
    {
        _owner = owner;
        _changed = changed;
        _settled = settled;
    }

    public double Value => _value;

    public void SetTarget(double target)
    {
        target = Math.Clamp(target, 0, 1);
        _requestedTarget = target;
        if (!_initialized)
        {
            // Source: the first draw creates the Animatable at the target instead of animating.
            _initialized = true;
            _value = target;
            _animationTarget = target;
            _changed();
            _settled();
            return;
        }
        if (_animating || Math.Abs(target - _value) <= 0.0005)
            return;
        Start(target);
    }

    private void Start(double target)
    {
        _tween?.Dispose();
        _animating = true;
        _animationTarget = target;
        Func<double, double> easing = target > _value ? Increasing.Ease : Decreasing.Ease;
        _tween = MaterialMotion.Tween(_owner, _value, target, 0.5, 0, Apply, easing);
    }

    private void Apply(double value)
    {
        _value = value;
        var completed = value == _animationTarget;
        if (completed)
            _animating = false;
        _changed();
        if (completed)
        {
            if (_requestedTarget != _animationTarget)
                Start(_requestedTarget);
            else
                _settled();
        }
    }

    /// <summary>Drops the animation; the next set snaps, as the source does on detach/rebuild.</summary>
    public void Reset()
    {
        _tween?.Dispose();
        _tween = null;
        _animating = false;
        _initialized = false;
    }
}

/// <summary>
/// Caches the normalized circle/star pair, the optional morph and the measured outline for the
/// circular wavy indicator. The source replaces its shapes only when the derived vertex count
/// changes, and reuses the morph afterwards.
/// </summary>
internal sealed class CircularWaveGeometry
{
    private int _vertexCount = -1;
    private RoundedPolygon? _circle;
    private RoundedPolygon? _star;
    private Morph? _morph;
    private Size _cachedSize;
    private double _cachedStroke;
    private double _cachedWavelength;
    private double _cachedAmplitude = double.NaN;
    private bool _hasPath;

    public Cubic[] ProgressCubics { get; private set; } = [];
    public double[] ProgressLengths { get; private set; } = [];
    public double ProgressLength { get; private set; }
    public Cubic[] TrackCubics { get; private set; } = [];
    public double[] TrackLengths { get; private set; } = [];
    public double TrackLength { get; private set; }
    public int VertexCount => _vertexCount;

    public void Invalidate()
    {
        _hasPath = false;
        _morph = null;
        _vertexCount = -1;
    }

    public void Update(Size size, double wavelength, double stroke, double amplitude)
    {
        if (size.Width <= 0 || size.Height <= 0 || wavelength <= 0 || stroke <= 0)
            return;
        EnsureShapes(size, wavelength, stroke);
        if (_hasPath && _cachedSize == size && _cachedStroke == stroke &&
            _cachedWavelength == wavelength && _cachedAmplitude.Equals(amplitude))
        {
            return;
        }

        if (_circle is null || _star is null)
            return;

        var width = size.Width;
        var height = size.Height;
        if (width <= stroke || height <= stroke)
            return;

        BuildProgress(width, height, stroke, amplitude);
        BuildTrack(width, height, stroke);

        _cachedSize = size;
        _cachedStroke = stroke;
        _cachedWavelength = wavelength;
        _cachedAmplitude = amplitude;
        _hasPath = true;
    }

    private void EnsureShapes(Size size, double wavelength, double stroke)
    {
        var radius = Math.Min(size.Width, size.Height) / 2 - stroke / 2;
        var count = Math.Max(5, (int)Math.Floor(2 * Math.PI * radius / wavelength + 0.5));
        if (count == _vertexCount)
            return;
        _vertexCount = count;
        _circle = RoundedShapes.Circle(count).Normalized();
        _star = RoundedShapes.Star(count, innerRadius: 0.75,
            rounding: new CornerRounding(0.35, 0.4), innerRounding: new CornerRounding(0.5)).Normalized();
        _morph = null;
        _hasPath = false;
    }

    private void BuildProgress(double width, double height, double stroke, double amplitude)
    {
        if (amplitude is > 0 and < 1)
        {
            _morph ??= new Morph(_circle!, _star!);
            var pairs = _morph.MatchPairs;
            if (ProgressCubics.Length != pairs.Count)
            {
                ProgressCubics = new Cubic[pairs.Count];
                ProgressLengths = new double[pairs.Count];
            }
            for (var index = 0; index < pairs.Count; index++)
            {
                var (start, end) = pairs[index];
                SetCubic(index, start.Lerp(end, amplitude), width, height, stroke, ProgressCubics);
                ProgressLengths[index] = CubicPathMath.Length(ProgressCubics[index]);
            }
        }
        else
        {
            var polygon = amplitude >= 1 ? _star! : _circle!;
            var cubics = polygon.Cubics;
            if (ProgressCubics.Length != cubics.Count)
            {
                ProgressCubics = new Cubic[cubics.Count];
                ProgressLengths = new double[cubics.Count];
            }
            for (var index = 0; index < cubics.Count; index++)
            {
                ScaleInto(cubics[index], width, height, stroke, ref ProgressCubics[index]);
                ProgressLengths[index] = CubicPathMath.Length(ProgressCubics[index]);
            }
        }

        var total = 0d;
        foreach (var length in ProgressLengths)
            total += length;
        ProgressLength = total;
    }

    private void BuildTrack(double width, double height, double stroke)
    {
        var cubics = _circle!.Cubics;
        if (TrackCubics.Length != cubics.Count)
        {
            TrackCubics = new Cubic[cubics.Count];
            TrackLengths = new double[cubics.Count];
        }
        for (var index = 0; index < cubics.Count; index++)
        {
            ScaleInto(cubics[index], width, height, stroke, ref TrackCubics[index]);
            TrackLengths[index] = CubicPathMath.Length(TrackCubics[index]);
        }
        var total = 0d;
        foreach (var length in TrackLengths)
            total += length;
        TrackLength = total;
    }

    private static void SetCubic(int index, Cubic cubic, double width, double height, double stroke,
        Cubic[] target) => ScaleInto(cubic, width, height, stroke, ref target[index]);

    private static void ScaleInto(Cubic cubic, double width, double height, double stroke, ref Cubic target)
    {
        // Source processPath: scale by (size − stroke) then translate the outline centre to the
        // drawing centre. Normalized polygons span the unit square, so the result is centred at
        // size/2 and the stroke's outer edge reaches the control bounds.
        static ShapePoint Scale(ShapePoint point, double w, double h, double s) =>
            new(s / 2 + point.X * (w - s), s / 2 + point.Y * (h - s));
        target = new Cubic(
            Scale(cubic.Anchor0, width, height, stroke),
            Scale(cubic.Control0, width, height, stroke),
            Scale(cubic.Control1, width, height, stroke),
            Scale(cubic.Anchor1, width, height, stroke));
    }

    /// <summary>
    /// Extracts a contiguous path segment of <paramref name="length"/> along the closed outline,
    /// starting at <paramref name="startDistance"/>, optionally rotated around the control centre.
    /// This is the Avalonia equivalent of the source PathMeasure.getSegment plus rotate transform.
    /// </summary>
    public static StreamGeometry? CreateSegment(Cubic[] cubics, double[] lengths, double total,
        double startDistance, double length, double rotationDegrees, Point center)
    {
        if (total <= 0 || length <= 0 || cubics.Length == 0)
            return null;
        length = Math.Min(length, total);
        var start = startDistance % total;
        if (start < 0)
            start += total;

        var index = 0;
        var accumulated = 0d;
        while (index < cubics.Length && accumulated + lengths[index] <= start + 1e-9)
        {
            accumulated += lengths[index];
            index++;
        }
        if (index >= cubics.Length)
        {
            index = 0;
            accumulated = 0;
        }

        var offset = Math.Max(0, start - accumulated);
        var remainder = length;
        var radians = rotationDegrees * Math.PI / 180;
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);
        var geometry = new StreamGeometry();
        var hasFigure = false;
        using (var path = geometry.Open())
        {
            while (remainder > 1e-9)
            {
                var cubic = cubics[index];
                var cubicLength = lengths[index];
                if (cubicLength <= 1e-9 || offset >= cubicLength - 1e-9)
                {
                    offset = 0;
                    index = (index + 1) % cubics.Length;
                    continue;
                }

                var take = Math.Min(cubicLength - offset, remainder);
                var t0 = CubicPathMath.FindT(cubic, offset);
                var t1 = CubicPathMath.FindT(cubic, offset + take);
                var part = CubicPathMath.Sub(cubic, t0, t1);
                var p0 = Rotate(part.Anchor0, center, cos, sin);
                var p1 = Rotate(part.Control0, center, cos, sin);
                var p2 = Rotate(part.Control1, center, cos, sin);
                var p3 = Rotate(part.Anchor1, center, cos, sin);
                if (!hasFigure)
                {
                    path.BeginFigure(p0, false);
                    hasFigure = true;
                }
                path.CubicBezierTo(p1, p2, p3);

                remainder -= take;
                offset = 0;
                index = (index + 1) % cubics.Length;
            }
            if (!hasFigure)
                return null;
            path.EndFigure(false);
        }
        return geometry;
    }

    private static Point Rotate(ShapePoint point, Point center, double cos, double sin)
    {
        var x = point.X - center.X;
        var y = point.Y - center.Y;
        return new Point(center.X + x * cos - y * sin, center.Y + x * sin + y * cos);
    }
}

/// <summary>High-accuracy arc-length helpers for the cubic outlines used by the progress port.</summary>
internal static class CubicPathMath
{
    public static double Length(Cubic cubic) => Length(cubic, 0, 1);

    public static double Length(Cubic cubic, double from, double to)
    {
        if (to <= from)
            return 0;
        // Composite Simpson over eight intervals: the cubic's speed is smooth, so this is far
        // below a pixel of error for the sizes the indicators are drawn at.
        const int intervals = 8;
        var step = (to - from) / intervals;
        var sum = Speed(cubic, from) + Speed(cubic, to);
        for (var index = 1; index < intervals; index++)
            sum += (index & 1) == 1 ? 4 * Speed(cubic, from + index * step) : 2 * Speed(cubic, from + index * step);
        return sum * step / 3;
    }

    public static double FindT(Cubic cubic, double distance)
    {
        var total = Length(cubic);
        if (distance <= 0)
            return 0;
        if (distance >= total)
            return 1;
        var low = 0d;
        var high = 1d;
        for (var index = 0; index < 22; index++)
        {
            var middle = (low + high) / 2;
            if (Length(cubic, 0, middle) < distance)
                low = middle;
            else
                high = middle;
        }
        return (low + high) / 2;
    }

    public static Cubic Sub(Cubic cubic, double t0, double t1)
    {
        if (t0 <= 0 && t1 >= 1)
            return cubic;
        var (head, _) = cubic.Split(t1);
        if (t0 <= 0 || t1 <= 0)
            return head;
        var (_, tail) = head.Split(t0 / t1);
        return tail;
    }

    private static double Speed(Cubic cubic, double t)
    {
        var u = 1 - t;
        var dx = 3 * u * u * (cubic.Control0.X - cubic.Anchor0.X) +
                 6 * u * t * (cubic.Control1.X - cubic.Control0.X) +
                 3 * t * t * (cubic.Anchor1.X - cubic.Control1.X);
        var dy = 3 * u * u * (cubic.Control0.Y - cubic.Anchor0.Y) +
                 6 * u * t * (cubic.Control1.Y - cubic.Control0.Y) +
                 3 * t * t * (cubic.Anchor1.Y - cubic.Control1.Y);
        return Math.Sqrt(dx * dx + dy * dy);
    }
}

