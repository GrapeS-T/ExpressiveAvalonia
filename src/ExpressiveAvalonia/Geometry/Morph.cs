using Avalonia.Media;

namespace ExpressiveAvalonia.Shapes;

/// <summary>
/// Animates between two <see cref="RoundedPolygon"/> shapes by matching and, where necessary,
/// splitting their curves. Interpolating a morph is a point-wise interpolation of matched cubics;
/// no cross-fade or uniform point resampling is involved.
/// </summary>
internal sealed class Morph
{
    private readonly (Cubic Start, Cubic End)[] _morphMatch;

    public Morph(RoundedPolygon start, RoundedPolygon end)
    {
        Start = start;
        End = end;
        _morphMatch = Match(start, end);
    }

    public RoundedPolygon Start { get; }

    public RoundedPolygon End { get; }

    /// <summary>The matched start/end curve pairs produced by <see cref="Match"/>.</summary>
    public IReadOnlyList<(Cubic Start, Cubic End)> MatchPairs => _morphMatch;

    /// <summary>Bounds of both endpoint shapes, not of an intermediate morph.</summary>
    public ShapeBounds CalculateBounds(bool approximate = true) =>
        ShapeBounds.Union(Start.CalculateBounds(approximate), End.CalculateBounds(approximate));

    /// <summary>Square that can hold either endpoint in any rotation.</summary>
    public ShapeBounds CalculateMaxBounds() => ShapeBounds.Union(
        Start.CalculateMaxBounds(), End.CalculateMaxBounds());

    /// <summary>
    /// Returns the interpolated outline at <paramref name="progress"/>. Progress outside [0, 1]
    /// is valid and extrapolates, which the loading spring deliberately uses for overshoot.
    /// </summary>
    public List<Cubic> AsCubics(double progress)
    {
        var cubics = new List<Cubic>(_morphMatch.Length + 1);
        Cubic? firstCubic = null;
        Cubic? lastCubic = null;
        foreach (var (start, end) in _morphMatch)
        {
            var cubic = start.Lerp(end, progress);
            firstCubic ??= cubic;
            if (lastCubic is { } previous)
                cubics.Add(previous);
            lastCubic = cubic;
        }

        if (lastCubic is { } last && firstCubic is { } first)
        {
            cubics.Add(new Cubic(last.Anchor0, last.Control0, last.Control1, first.Anchor0));
        }

        return cubics;
    }

    /// <summary>
    /// Bounds of the control points of the outline that <c>AppendTo</c> emits at
    /// <paramref name="progress"/>, including the corrected final anchor. This mirrors the source
    /// <c>Path.getBounds()</c> after <c>Morph.toPath</c>, which is the rectangle the source
    /// <c>processPath</c> centers the scaled path on.
    /// </summary>
    /// <remarks>
    /// The hull covers every emitted anchor and control point, so it is larger than the painted
    /// outline wherever a cubic bends inside its control polygon. A cubic whose anchors coincide is
    /// not special-cased: its control points are still written to the path, so the source bounds
    /// include them as well.
    /// </remarks>
    public ShapeBounds CalculateBoundsAt(double progress)
    {
        var bounds = new ShapeBounds(double.MaxValue, double.MaxValue, double.MinValue,
            double.MinValue);
        var firstAnchor = _morphMatch[0].Start.Anchor0.Lerp(_morphMatch[0].End.Anchor0, progress);
        for (var index = 0; index < _morphMatch.Length; index++)
        {
            var (start, end) = _morphMatch[index];
            var cubic = start.Lerp(end, progress);
            if (index == _morphMatch.Length - 1)
                cubic = new Cubic(cubic.Anchor0, cubic.Control0, cubic.Control1, firstAnchor);
            bounds = ShapeBounds.Union(bounds, ControlBounds(cubic));
        }
        return bounds;
    }

    /// <summary>
    /// Control-point hull of a single emitted cubic. Unlike <see cref="Cubic.CalculateBounds"/>
    /// this neither computes extrema nor drops curves: path bounds are the bounds of the path's
    /// points, so coincident anchors never remove the control points that follow them.
    /// </summary>
    private static ShapeBounds ControlBounds(Cubic cubic) => new(
        Math.Min(Math.Min(cubic.Anchor0.X, cubic.Anchor1.X),
            Math.Min(cubic.Control0.X, cubic.Control1.X)),
        Math.Min(Math.Min(cubic.Anchor0.Y, cubic.Anchor1.Y),
            Math.Min(cubic.Control0.Y, cubic.Control1.Y)),
        Math.Max(Math.Max(cubic.Anchor0.X, cubic.Anchor1.X),
            Math.Max(cubic.Control0.X, cubic.Control1.X)),
        Math.Max(Math.Max(cubic.Anchor0.Y, cubic.Anchor1.Y),
            Math.Max(cubic.Control0.Y, cubic.Control1.Y)));

    /// <summary>
    /// Writes the interpolated closed outline to an open path context without allocating a cubic
    /// list. Every point is scaled and offset before it reaches the graphics backend.
    /// </summary>
    public void AppendTo(StreamGeometryContext context, double progress, double scale = 1,
        double offsetX = 0, double offsetY = 0) =>
        AppendTo(context, progress, scale, scale, offsetX, offsetY);

    /// <summary>Writes the outline with independent X/Y scale and an offset.</summary>
    public void AppendTo(StreamGeometryContext context, double progress, double scaleX,
        double scaleY, double offsetX, double offsetY)
    {
        context.SetFillRule(FillRule.NonZero);
        var firstAnchor = _morphMatch[0].Start.Anchor0.Lerp(_morphMatch[0].End.Anchor0, progress);
        context.BeginFigure(Transform(firstAnchor), true);
        for (var index = 0; index < _morphMatch.Length; index++)
        {
            var (start, end) = _morphMatch[index];
            var cubic = start.Lerp(end, progress);
            var anchor = index == _morphMatch.Length - 1 ? firstAnchor : cubic.Anchor1;
            context.CubicBezierTo(Transform(cubic.Control0), Transform(cubic.Control1), Transform(anchor));
        }

        context.EndFigure(true);

        Avalonia.Point Transform(ShapePoint point) =>
            new(point.X * scaleX + offsetX, point.Y * scaleY + offsetY);
    }

    private static (Cubic Start, Cubic End)[] Match(RoundedPolygon first, RoundedPolygon second)
    {
        var measured1 = MeasuredPolygon.MeasurePolygon(new LengthMeasurer(), first);
        var measured2 = MeasuredPolygon.MeasurePolygon(new LengthMeasurer(), second);

        var doubleMapper = FeatureMapping.FeatureMapper(measured1.Features, measured2.Features);

        // The point on the second shape that corresponds to progress zero on the first.
        var secondCutPoint = doubleMapper.Map(0);
        var shapes1 = measured1;
        var shapes2 = measured2.CutAndShift(secondCutPoint);

        var result = new List<(Cubic, Cubic)>();
        var index1 = 0;
        var index2 = 0;
        var cubic1 = AtOrNull(shapes1, index1++);
        var cubic2 = AtOrNull(shapes2, index2++);

        while (cubic1 != null && cubic2 != null)
        {
            // Progress values are expressed in the first shape's space.
            var end1 = index1 == shapes1.Count ? 1 : cubic1.EndOutlineProgress;
            var end2 = index2 == shapes2.Count
                ? 1
                : doubleMapper.MapBack(ShapeMath.PositiveModulo(
                    cubic2.EndOutlineProgress + secondCutPoint, 1));
            var minimumEnd = Math.Min(end1, end2);

            MeasuredPolygon.MeasuredCubic segment1;
            MeasuredPolygon.MeasuredCubic? next1;
            if (end1 > minimumEnd + ShapeMath.AngleEpsilon)
            {
                (segment1, next1) = cubic1.CutAtProgress(minimumEnd);
            }
            else
            {
                segment1 = cubic1;
                next1 = AtOrNull(shapes1, index1++);
            }

            MeasuredPolygon.MeasuredCubic segment2;
            MeasuredPolygon.MeasuredCubic? next2;
            if (end2 > minimumEnd + ShapeMath.AngleEpsilon)
            {
                (segment2, next2) = cubic2.CutAtProgress(
                    ShapeMath.PositiveModulo(doubleMapper.Map(minimumEnd) - secondCutPoint, 1));
            }
            else
            {
                segment2 = cubic2;
                next2 = AtOrNull(shapes2, index2++);
            }

            result.Add((segment1.Cubic, segment2.Cubic));
            cubic1 = next1;
            cubic2 = next2;
        }

        if (cubic1 != null || cubic2 != null)
        {
            throw new InvalidOperationException(
                "Expected both polygons' cubics to be fully matched.");
        }

        return result.ToArray();
    }

    private static MeasuredPolygon.MeasuredCubic? AtOrNull(MeasuredPolygon polygon, int index) =>
        index < polygon.Count ? polygon[index] : null;
}
