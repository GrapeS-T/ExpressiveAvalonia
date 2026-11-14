namespace ExpressiveAvalonia.Shapes;

/// <summary>
/// Anchor and control point data for one cubic Bézier curve.
/// </summary>
/// <remarks>
/// A closed shape is an ordered list of contiguous cubics, so this type is intentionally a
/// value: interpolation and cutting allocate nothing and the caller owns any list it builds.
/// </remarks>
internal readonly struct Cubic : IEquatable<Cubic>
{
    public Cubic(ShapePoint anchor0, ShapePoint control0, ShapePoint control1, ShapePoint anchor1)
    {
        Anchor0 = anchor0;
        Control0 = control0;
        Control1 = control1;
        Anchor1 = anchor1;
    }

    public ShapePoint Anchor0 { get; }

    public ShapePoint Control0 { get; }

    public ShapePoint Control1 { get; }

    public ShapePoint Anchor1 { get; }

    public bool IsZeroLength =>
        Math.Abs(Anchor0.X - Anchor1.X) < ShapeMath.DistanceEpsilon &&
        Math.Abs(Anchor0.Y - Anchor1.Y) < ShapeMath.DistanceEpsilon;

    public ShapePoint PointOnCurve(double t)
    {
        var u = 1 - t;
        return new ShapePoint(
            Anchor0.X * (u * u * u) +
            Control0.X * (3 * t * u * u) +
            Control1.X * (3 * t * t * u) +
            Anchor1.X * (t * t * t),
            Anchor0.Y * (u * u * u) +
            Control0.Y * (3 * t * u * u) +
            Control1.Y * (3 * t * t * u) +
            Anchor1.Y * (t * t * t));
    }

    public bool IsConvexTo(Cubic next) =>
        ShapeMath.Convex(Anchor0, Anchor1, next.Anchor1);

    /// <summary>
    /// Returns the true axis-aligned bounds of this curve. When
    /// <paramref name="approximate"/> is true the bounds of all points are used instead, which is
    /// cheaper but can be larger than the painted curve.
    /// </summary>
    public ShapeBounds CalculateBounds(bool approximate = false)
    {
        if (IsZeroLength)
            return new ShapeBounds(Anchor0.X, Anchor0.Y, Anchor0.X, Anchor0.Y);

        var minX = Math.Min(Anchor0.X, Anchor1.X);
        var minY = Math.Min(Anchor0.Y, Anchor1.Y);
        var maxX = Math.Max(Anchor0.X, Anchor1.X);
        var maxY = Math.Max(Anchor0.Y, Anchor1.Y);

        if (approximate)
        {
            return new ShapeBounds(
                Math.Min(minX, Math.Min(Control0.X, Control1.X)),
                Math.Min(minY, Math.Min(Control0.Y, Control1.Y)),
                Math.Max(maxX, Math.Max(Control0.X, Control1.X)),
                Math.Max(maxY, Math.Max(Control0.Y, Control1.Y)));
        }

        IncludeExtrema(Anchor0.X, Control0.X, Control1.X, Anchor1.X, ref minX, ref maxX);
        IncludeExtrema(Anchor0.Y, Control0.Y, Control1.Y, Anchor1.Y, ref minY, ref maxY);
        return new ShapeBounds(minX, minY, maxX, maxY);

        static void IncludeExtrema(double a0, double c0, double c1, double a1, ref double minimum,
            ref double maximum)
        {
            // The derivative is a quadratic Bézier; solve it with the quadratic formula.
            var qa = -a0 + 3 * c0 - 3 * c1 + a1;
            var qb = 2 * a0 - 4 * c0 + 2 * c1;
            var qc = -a0 + c0;

            if (Math.Abs(qa) < ShapeMath.DistanceEpsilon)
            {
                if (qb != 0)
                {
                    var t = 2 * qc / (-2 * qb);
                    if (t is >= 0 and <= 1)
                        Include(t, a0, c0, c1, a1, ref minimum, ref maximum);
                }
            }
            else
            {
                var discriminant = qb * qb - 4 * qa * qc;
                if (discriminant >= 0)
                {
                    var root = Math.Sqrt(discriminant);
                    var t1 = (-qb + root) / (2 * qa);
                    if (t1 is >= 0 and <= 1)
                        Include(t1, a0, c0, c1, a1, ref minimum, ref maximum);
                    var t2 = (-qb - root) / (2 * qa);
                    if (t2 is >= 0 and <= 1)
                        Include(t2, a0, c0, c1, a1, ref minimum, ref maximum);
                }
            }

            static void Include(double t, double a0, double c0, double c1, double a1,
                ref double minimum, ref double maximum)
            {
                var u = 1 - t;
                var value = a0 * u * u * u + 3 * c0 * t * u * u + 3 * c1 * t * t * u + a1 * t * t * t;
                minimum = Math.Min(minimum, value);
                maximum = Math.Max(maximum, value);
            }
        }
    }

    /// <summary>Splits this curve at <paramref name="t"/> into two contiguous cubics.</summary>
    public (Cubic Start, Cubic End) Split(double t)
    {
        var u = 1 - t;
        var pointOnCurve = PointOnCurve(t);
        const double two = 2;
        return (
            new Cubic(
                Anchor0,
                new ShapePoint(Anchor0.X * u + Control0.X * t, Anchor0.Y * u + Control0.Y * t),
                new ShapePoint(
                    Anchor0.X * (u * u) + Control0.X * (two * u * t) + Control1.X * (t * t),
                    Anchor0.Y * (u * u) + Control0.Y * (two * u * t) + Control1.Y * (t * t)),
                pointOnCurve),
            new Cubic(
                pointOnCurve,
                new ShapePoint(
                    Control0.X * (u * u) + Control1.X * (two * u * t) + Anchor1.X * (t * t),
                    Control0.Y * (u * u) + Control1.Y * (two * u * t) + Anchor1.Y * (t * t)),
                new ShapePoint(Control1.X * u + Anchor1.X * t, Control1.Y * u + Anchor1.Y * t),
                Anchor1));
    }

    public Cubic Reverse() => new(Anchor1, Control1, Control0, Anchor0);

    public Cubic Lerp(Cubic other, double progress) => new(
        Anchor0.Lerp(other.Anchor0, progress),
        Control0.Lerp(other.Control0, progress),
        Control1.Lerp(other.Control1, progress),
        Anchor1.Lerp(other.Anchor1, progress));

    public Cubic Transformed(Func<ShapePoint, ShapePoint> transform) => new(
        transform(Anchor0),
        transform(Control0),
        transform(Control1),
        transform(Anchor1));

    public Cubic Plus(Cubic other) => new(
        Anchor0 + other.Anchor0,
        Control0 + other.Control0,
        Control1 + other.Control1,
        Anchor1 + other.Anchor1);

    public Cubic Times(double scalar) => new(
        Anchor0 * scalar,
        Control0 * scalar,
        Control1 * scalar,
        Anchor1 * scalar);

    /// <summary>A cubic that is a straight line between the anchors.</summary>
    public static Cubic StraightLine(double x0, double y0, double x1, double y1) => new(
        new ShapePoint(x0, y0),
        new ShapePoint(ShapeMath.Interpolate(x0, x1, 1d / 3), ShapeMath.Interpolate(y0, y1, 1d / 3)),
        new ShapePoint(ShapeMath.Interpolate(x0, x1, 2d / 3), ShapeMath.Interpolate(y0, y1, 2d / 3)),
        new ShapePoint(x1, y1));

    public static Cubic StraightLine(ShapePoint from, ShapePoint to) =>
        StraightLine(from.X, from.Y, to.X, to.Y);

    /// <summary>
    /// Approximates the smallest circular arc between two points that share a centre.
    /// Arcs larger than 180 degrees need more than one curve.
    /// </summary>
    public static Cubic CircularArc(double centerX, double centerY, double x0, double y0,
        double x1, double y1)
    {
        var p0d = new ShapePoint(x0 - centerX, y0 - centerY).Direction();
        var p1d = new ShapePoint(x1 - centerX, y1 - centerY).Direction();
        var rotatedP0 = p0d.Rotate90();
        var rotatedP1 = p1d.Rotate90();
        var clockwise = rotatedP0.DotProduct(x1 - centerX, y1 - centerY) >= 0;
        var cosa = p0d.DotProduct(p1d);
        if (cosa > 0.999)
            return StraightLine(x0, y0, x1, y1);

        var k = ShapeMath.Distance(x0 - centerX, y0 - centerY) * 4 / 3 *
                (Math.Sqrt(2 * (1 - cosa)) - Math.Sqrt(1 - cosa * cosa)) / (1 - cosa) *
                (clockwise ? 1 : -1);
        return new Cubic(
            new ShapePoint(x0, y0),
            new ShapePoint(x0 + rotatedP0.X * k, y0 + rotatedP0.Y * k),
            new ShapePoint(x1 - rotatedP1.X * k, y1 - rotatedP1.Y * k),
            new ShapePoint(x1, y1));
    }

    public static Cubic Empty(double x0, double y0)
    {
        var point = new ShapePoint(x0, y0);
        return new Cubic(point, point, point, point);
    }

    public bool Equals(Cubic other) =>
        Anchor0.Equals(other.Anchor0) && Control0.Equals(other.Control0) &&
        Control1.Equals(other.Control1) && Anchor1.Equals(other.Anchor1);

    public override bool Equals(object? obj) => obj is Cubic other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(Anchor0, Control0, Control1, Anchor1);

    public override string ToString() =>
        $"anchor0: {Anchor0} control0: {Control0}, control1: {Control1}, anchor1: {Anchor1}";
}
