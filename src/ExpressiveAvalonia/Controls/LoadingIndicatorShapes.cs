using Avalonia;
using Avalonia.Media;
using ExpressiveAvalonia.Shapes;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// Geometry and morph sequences for the Material loading indicator, ported from
/// androidx <c>LoadingIndicator.kt</c> and <c>LoadingIndicatorDefaults</c>.
/// </summary>
/// <remarks>
/// The determinate indicator morphs between a circle rotated by 18 degrees and a soft burst as
/// progress goes from zero to one. The indeterminate indicator cycles through SoftBurst,
/// Cookie9Sided, Pentagon, Pill, Sunny, Cookie4Sided and Oval, including a final morph from the
/// last shape back to the first. Every polygon is normalized before a morph is built.
/// </remarks>
internal static class LoadingIndicatorShapes
{
    /// <summary>
    /// Source <c>MorphIntervalMillis</c>: the indeterminate loop starts one morph per slot and
    /// waits for the remainder of this interval after the spring settles.
    /// </summary>
    internal const double MorphIntervalMilliseconds = 650;

    /// <summary>Source <c>GlobalRotationDurationMillis</c>: one full turn of the container.</summary>
    internal const double RotationMilliseconds = 4666;

    /// <summary>Source morph spring stiffness (<c>spring(stiffness = 200f)</c>).</summary>
    internal const double MorphStiffness = 200;

    /// <summary>Source morph spring damping ratio (<c>spring(dampingRatio = 0.6f)</c>).</summary>
    internal const double MorphDampingRatio = 0.6;

    /// <summary>
    /// Source <c>visibilityThreshold = 0.1f</c> on the morph spring: the animation reports
    /// <c>Finished</c> once the value is this close to its target, which is when the loop banks
    /// the quarter turn and snaps the progress back to zero.
    /// </summary>
    internal const double MorphVisibilityThreshold = 0.1;

    /// <summary>Spring used for every morph, evaluated from the start of its target segment.</summary>
    internal static readonly SpringSimulation MorphSpring = new(MorphStiffness, MorphDampingRatio);

    /// <summary>
    /// Time after which a morph that starts at zero is within the visibility threshold of one.
    /// The source passes a tenfold displacement and threshold pair to SpringEstimation; both scale
    /// the logarithmic decay identically, so this is the settle time of the unit morph.
    /// </summary>
    internal static readonly double MorphDurationMilliseconds =
        SpringDuration.EstimateMilliseconds(MorphStiffness, MorphDampingRatio, 0, -10, 1);

    /// <summary>Source container width in device-independent units.</summary>
    public const double ContainerWidth = 48;

    /// <summary>Source container height in device-independent units.</summary>
    public const double ContainerHeight = 48;

    /// <summary>Source active-indicator size in device-independent units.</summary>
    public const double IndicatorSize = 38;

    /// <summary>
    /// The source <c>ActiveIndicatorScale</c>: the active shape occupies
    /// <see cref="IndicatorSize"/> of the smaller container dimension.
    /// </summary>
    public static double ActiveIndicatorScale =>
        IndicatorSize / Math.Min(ContainerWidth, ContainerHeight);

    /// <summary>The source indeterminate polygon sequence.</summary>
    public static IReadOnlyList<RoundedPolygon> IndeterminatePolygons { get; } =
    [
        MaterialShapeCatalog.SoftBurst,
        MaterialShapeCatalog.Cookie9Sided,
        MaterialShapeCatalog.Pentagon,
        MaterialShapeCatalog.Pill,
        MaterialShapeCatalog.Sunny,
        MaterialShapeCatalog.Cookie4Sided,
        MaterialShapeCatalog.Oval
    ];

    /// <summary>
    /// The source determinate polygon sequence: a circle rotated by 360/20 degrees, which morphs
    /// more smoothly into the SoftBurst because both shapes are rotated by the same angle.
    /// </summary>
    public static IReadOnlyList<RoundedPolygon> DeterminatePolygons { get; } =
    [
        Rotate(MaterialShapeCatalog.Circle, 360d / 20),
        MaterialShapeCatalog.SoftBurst
    ];

    /// <summary>Morphs for the indeterminate sequence, including last-to-first.</summary>
    public static IReadOnlyList<Morph> IndeterminateMorphs { get; } =
        BuildMorphSequence(IndeterminatePolygons, circularSequence: true);

    /// <summary>Morphs for the determinate sequence.</summary>
    public static IReadOnlyList<Morph> DeterminateMorphs { get; } =
        BuildMorphSequence(DeterminatePolygons, circularSequence: false);

    /// <summary>
    /// Uniform path scale for the indeterminate sequence. The value includes the source
    /// <c>ActiveIndicatorScale</c> and the per-rotation bounds allowance.
    /// </summary>
    public static double IndeterminateScaleFactor { get; } =
        CalculateScaleFactor(IndeterminatePolygons) * ActiveIndicatorScale;

    /// <summary>
    /// Uniform path scale for the determinate sequence. The value includes the source
    /// <c>ActiveIndicatorScale</c> and the per-rotation bounds allowance.
    /// </summary>
    public static double DeterminateScaleFactor { get; } =
        CalculateScaleFactor(DeterminatePolygons) * ActiveIndicatorScale;

    /// <summary>
    /// Builds the morph sequence exactly as the source: normalized consecutive polygons, plus the
    /// last-to-first morph when <paramref name="circularSequence"/> is true.
    /// </summary>
    public static IReadOnlyList<Morph> BuildMorphSequence(
        IReadOnlyList<RoundedPolygon> polygons, bool circularSequence)
    {
        if (polygons.Count < 2)
            throw new ArgumentException("At least two polygons are required.", nameof(polygons));

        var morphs = new List<Morph>(polygons.Count);
        for (var index = 0; index < polygons.Count; index++)
        {
            if (index + 1 < polygons.Count)
            {
                morphs.Add(new Morph(polygons[index].Normalized(), polygons[index + 1].Normalized()));
            }
            else if (circularSequence)
            {
                morphs.Add(new Morph(polygons[index].Normalized(), polygons[0].Normalized()));
            }
        }

        return morphs;
    }

    /// <summary>
    /// Calculates the scale that keeps every shape fully inside its container during rotation.
    /// A plain axis-aligned bounds check would clip a rotating shape, so the square max bounds are
    /// compared against the tight bounds and the smallest ratio wins.
    /// </summary>
    public static double CalculateScaleFactor(IReadOnlyList<RoundedPolygon> polygons)
    {
        var scaleFactor = 1d;
        foreach (var polygon in polygons)
        {
            var bounds = polygon.CalculateBounds();
            var maxBounds = polygon.CalculateMaxBounds();
            var scaleX = bounds.Width / maxBounds.Width;
            var scaleY = bounds.Height / maxBounds.Height;
            // A pill can have a much larger one-axis ratio, so use the larger of the two.
            scaleFactor = Math.Min(scaleFactor, Math.Max(scaleX, scaleY));
        }
        return scaleFactor;
    }

    /// <summary>
    /// Creates the frame path for a square of <paramref name="size"/> units. The path is already
    /// scaled by <c>size × scaleFactor</c> and translated so the control-point bounds center of the
    /// emitted outline sits at the square center, matching the source <c>processPath</c> (which
    /// centers on <c>Path.getBounds()</c>, not on the painted outline's tight bounds).
    /// </summary>
    /// <remarks>
    /// Each frame needs a new <see cref="StreamGeometry"/>: Avalonia opens stream geometry by
    /// appending to its native path and exposes no clear operation, so a previously drawn instance
    /// cannot be recycled. The cubic interpolation itself allocates no list or array.
    /// </remarks>
    public static StreamGeometry CreateFrame(Morph morph, double progress, double size,
        double scaleFactor) => CreateFrame(morph, progress, size, size, scaleFactor);

    /// <summary>Non-square variant of <see cref="CreateFrame(Morph, double, double, double)"/>.</summary>
    public static StreamGeometry CreateFrame(Morph morph, double progress, double width,
        double height, double scaleFactor)
    {
        var (scaleX, scaleY, offsetX, offsetY) =
            FramePlacement(morph, progress, width, height, scaleFactor);

        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        morph.AppendTo(context, progress, scaleX, scaleY, offsetX, offsetY);
        return geometry;
    }

    /// <summary>
    /// Writes a frame into <paramref name="path"/>. Avalonia's <see cref="StreamGeometry"/> has no
    /// reset operation, so the destination must not already contain path data; passing a cached
    /// instance would append a second outline instead of replacing the first.
    /// </summary>
    public static void PopulateFrame(StreamGeometry path, Morph morph, double progress,
        double size, double scaleFactor) =>
        PopulateFrame(path, morph, progress, size, size, scaleFactor);

    /// <summary>Non-square variant of the fresh-destination populate overload.</summary>
    public static void PopulateFrame(StreamGeometry path, Morph morph, double progress,
        double width, double height, double scaleFactor)
    {
        var (scaleX, scaleY, offsetX, offsetY) =
            FramePlacement(morph, progress, width, height, scaleFactor);

        using var context = path.Open();
        morph.AppendTo(context, progress, scaleX, scaleY, offsetX, offsetY);
    }

    /// <summary>
    /// Scale and translation that place an emitted frame in the requested box. Both frame
    /// entrypoints share this pivot: the source <c>processPath</c> centers on the control-point
    /// bounds of the emitted path, which is exactly <see cref="Morph.CalculateBoundsAt"/>, so an
    /// asymmetric or custom sequence paints off-center by the difference between those bounds and
    /// the tight ink bounds.
    /// </summary>
    private static (double ScaleX, double ScaleY, double OffsetX, double OffsetY) FramePlacement(
        Morph morph, double progress, double width, double height, double scaleFactor)
    {
        var bounds = morph.CalculateBoundsAt(progress);
        var scaleX = width * scaleFactor;
        var scaleY = height * scaleFactor;
        return (scaleX, scaleY, width / 2 - bounds.Center.X * scaleX,
            height / 2 - bounds.Center.Y * scaleY);
    }

    private static RoundedPolygon Rotate(RoundedPolygon polygon, double degrees)
    {
        var radians = degrees * Math.PI / 180;
        return polygon.Transformed(point => new ShapePoint(
            point.X * Math.Cos(radians) - point.Y * Math.Sin(radians),
            point.X * Math.Sin(radians) + point.Y * Math.Cos(radians)));
    }

    /// <summary>
    /// Builds the arbitrary-shape container outline for the control bounds, following the same
    /// <see cref="Stretch"/> contract as the Avalonia <c>Shape</c> and <c>MaterialShape</c>
    /// geometry adapters. The source <c>containerShape</c> receives the layout size and returns an
    /// outline in that coordinate space; here an authored <see cref="Geometry"/> is mapped into the
    /// same space instead. The returned instance is never the caller's geometry: it is a clone
    /// whose <see cref="Geometry.Transform"/> composes the source transform with the stretch, so
    /// painting the fill and clipping the indicator can use one identical outline without locking
    /// the authored value.
    /// </summary>
    internal static Geometry StretchGeometry(Geometry source, Rect target, Stretch stretch)
    {
        var sourceTransform = source.Transform?.Value ?? Matrix.Identity;
        var sourceBounds = source.Bounds;
        if (stretch == Stretch.None || sourceBounds.Width <= 0 || sourceBounds.Height <= 0 ||
            target.Width <= 0 || target.Height <= 0)
        {
            return CloneWithTransform(source, sourceTransform);
        }

        var stretchX = target.Width / sourceBounds.Width;
        var stretchY = target.Height / sourceBounds.Height;
        double scaleX, scaleY;
        switch (stretch)
        {
            case Stretch.Fill:
                scaleX = stretchX;
                scaleY = stretchY;
                break;
            case Stretch.UniformToFill:
                scaleX = scaleY = Math.Max(stretchX, stretchY);
                break;
            default:
                scaleX = scaleY = Math.Min(stretchX, stretchY);
                break;
        }

        var offsetX = target.X - sourceBounds.X * scaleX + (target.Width - sourceBounds.Width * scaleX) / 2;
        var offsetY = target.Y - sourceBounds.Y * scaleY + (target.Height - sourceBounds.Height * scaleY) / 2;
        return CloneWithTransform(source, sourceTransform *
            new Matrix(scaleX, 0, 0, scaleY, offsetX, offsetY));
    }

    private static Geometry CloneWithTransform(Geometry source, Matrix transform)
    {
        var clone = source.Clone();
        clone.Transform = new MatrixTransform(transform);
        return clone;
    }
}

/// <summary>
/// Timeline of the source indeterminate <c>LaunchedEffect</c>. Each slot starts a lower-damping
/// spring towards the next polygon, banks a quarter turn when the spring settles, and holds the
/// snapped-to-zero progress until the 650 ms slot ends.
/// </summary>
/// <remarks>
/// The slot mapping is anchored at the animation start, exactly like the source
/// <c>morphProgress</c>/<c>currentMorphIndex</c>/<c>morphRotationTargetAngle</c> state, but the
/// source re-keys only the morph index on a polygon-list change. <see cref="Restart"/> reproduces
/// that replacement: the index restarts at the first morph of the new sequence while the
/// remembered progress and the banked quarter turns continue. The restarted effect animates from
/// the preserved progress with a zero initial velocity because cancelling the previous
/// <c>Animatable</c> animation resets its velocity. It also restarts the infinite repeatable
/// rotation tween from its remembered value, so <see cref="Frame.Rotation"/> reproduces the source
/// <c>globalRotation</c> value including the backward jump at every 4666 ms repetition boundary.
/// </remarks>
internal sealed class LoadingIndicatorCycle
{
    private const double FullRotationDegrees = 360;

    private double _restartElapsed = -1;
    private double _restartProgress;
    private double _restartSettleMilliseconds;
    private double _restartRotation;
    private long _restartCompleted;

    /// <summary>
    /// Frame at a clock time in milliseconds since the animation started: the morph slot used for
    /// the index, the spring progress within it, the number of settled morphs, which is the source
    /// quarter-turn count, and the remembered <c>globalRotation</c> Animatable value.
    /// </summary>
    internal readonly record struct Frame(long Slot, double Progress, long Completed, double Rotation);

    internal Frame At(double elapsedMilliseconds)
    {
        // The animation clock restarts from zero when a hidden or reduced control resumes, which
        // discards the replacement anchor together with the rest of the cycle state.
        if (_restartElapsed >= 0 && elapsedMilliseconds < _restartElapsed)
            _restartElapsed = -1;

        if (_restartElapsed < 0)
        {
            var completedSlots = (long)(elapsedMilliseconds / LoadingIndicatorShapes.MorphIntervalMilliseconds);
            var rotation = ContinuousRotation(elapsedMilliseconds);
            var within = elapsedMilliseconds -
                completedSlots * LoadingIndicatorShapes.MorphIntervalMilliseconds;
            if (within >= LoadingIndicatorShapes.MorphDurationMilliseconds)
                return new Frame(completedSlots, 0, completedSlots + 1, rotation);
            return new Frame(completedSlots,
                LoadingIndicatorShapes.MorphSpring.Evaluate(0, 0, 1, within / 1000).Position,
                completedSlots, rotation);
        }

        var sinceRestart = elapsedMilliseconds - _restartElapsed;
        var restartedRotation = RestartedRotation(sinceRestart);
        if (sinceRestart < LoadingIndicatorShapes.MorphIntervalMilliseconds)
        {
            if (sinceRestart >= _restartSettleMilliseconds)
                return new Frame(1, 0, _restartCompleted + 1, restartedRotation);
            return new Frame(0,
                LoadingIndicatorShapes.MorphSpring
                    .Evaluate(_restartProgress, 0, 1, sinceRestart / 1000).Position,
                _restartCompleted, restartedRotation);
        }

        var phase = sinceRestart - LoadingIndicatorShapes.MorphIntervalMilliseconds;
        var slot = (long)(phase / LoadingIndicatorShapes.MorphIntervalMilliseconds);
        var morphElapsed = phase - slot * LoadingIndicatorShapes.MorphIntervalMilliseconds;
        if (morphElapsed >= LoadingIndicatorShapes.MorphDurationMilliseconds)
        {
            var completed = _restartCompleted + 1 + slot + 1;
            return new Frame(1 + slot + 1, 0, completed, restartedRotation);
        }
        return new Frame(1 + slot,
            LoadingIndicatorShapes.MorphSpring.Evaluate(0, 0, 1, morphElapsed / 1000).Position,
            _restartCompleted + 1 + slot, restartedRotation);
    }

    /// <summary>
    /// Value of the source <c>globalRotation</c> Animatable while its first
    /// <c>infiniteRepeatable(tween(4666, LinearEasing), Restart)</c> still runs: the tween captured
    /// zero as its initial value, so each repetition is a linear 0→360 sweep.
    /// </summary>
    private static double ContinuousRotation(double elapsedMilliseconds) =>
        elapsedMilliseconds % LoadingIndicatorShapes.RotationMilliseconds /
        LoadingIndicatorShapes.RotationMilliseconds * FullRotationDegrees;

    /// <summary>
    /// Value after the effect restarts. <c>Animatable.animateTo</c> builds
    /// <c>TargetBasedAnimation</c> with the remembered value as <c>initialValue</c>, and
    /// <c>VectorizedInfiniteRepeatableSpec.getValueFromNanos</c> passes that same initial value to
    /// every repetition, so each 4666 ms iteration is a linear <c>R0 → 360</c> sweep and the value
    /// jumps back to <c>R0</c> at each iteration boundary.
    /// </summary>
    private double RestartedRotation(double sinceRestartMilliseconds) =>
        _restartRotation + (FullRotationDegrees - _restartRotation) *
        (sinceRestartMilliseconds % LoadingIndicatorShapes.RotationMilliseconds /
         LoadingIndicatorShapes.RotationMilliseconds);

    /// <summary>
    /// Shot on a polygon sequence replacement: keep the current frame's progress, quarter-turn
    /// count and remembered global rotation, restart the morph slot at index zero.
    /// </summary>
    internal void Restart(double elapsedMilliseconds)
    {
        if (elapsedMilliseconds <= 0)
        {
            Reset();
            return;
        }

        var frame = At(elapsedMilliseconds);
        _restartProgress = frame.Progress;
        _restartCompleted = frame.Completed;
        _restartRotation = frame.Rotation;
        _restartSettleMilliseconds = SpringDuration.EstimateMilliseconds(
            LoadingIndicatorShapes.MorphStiffness, LoadingIndicatorShapes.MorphDampingRatio, 0,
            frame.Progress - 1, LoadingIndicatorShapes.MorphVisibilityThreshold);
        _restartElapsed = elapsedMilliseconds;
    }

    /// <summary>Drops every remembered value, as when the animation restarts from zero.</summary>
    internal void Reset()
    {
        _restartElapsed = -1;
        _restartRotation = 0;
    }
}
