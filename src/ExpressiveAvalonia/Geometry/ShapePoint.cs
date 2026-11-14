namespace ExpressiveAvalonia.Shapes;

/// <summary>A two-dimensional point in the shape normalised coordinate space.</summary>
internal readonly struct ShapePoint : IEquatable<ShapePoint>
{
    public ShapePoint(double x, double y)
    {
        X = x;
        Y = y;
    }

    public double X { get; }

    public double Y { get; }

    public static ShapePoint Zero => new(0, 0);

    public double Distance => Math.Sqrt(X * X + Y * Y);

    public double DistanceSquared => X * X + Y * Y;

    public ShapePoint Direction()
    {
        var distance = Distance;
        if (distance <= 0)
            throw new ArgumentException("Required distance greater than zero", nameof(distance));
        return new ShapePoint(X / distance, Y / distance);
    }

    public double DotProduct(ShapePoint other) => X * other.X + Y * other.Y;

    public double DotProduct(double otherX, double otherY) => X * otherX + Y * otherY;

    public bool Clockwise(ShapePoint other) => X * other.Y - Y * other.X > 0;

    public ShapePoint Rotate90() => new(-Y, X);

    public ShapePoint Lerp(ShapePoint to, double fraction) =>
        new(X + (to.X - X) * fraction, Y + (to.Y - Y) * fraction);

    public bool Equals(ShapePoint other) => X.Equals(other.X) && Y.Equals(other.Y);

    public override bool Equals(object? obj) => obj is ShapePoint other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(X, Y);

    public override string ToString() => $"({X}, {Y})";

    public static ShapePoint operator +(ShapePoint a, ShapePoint b) => new(a.X + b.X, a.Y + b.Y);

    public static ShapePoint operator -(ShapePoint a, ShapePoint b) => new(a.X - b.X, a.Y - b.Y);

    public static ShapePoint operator -(ShapePoint point) => new(-point.X, -point.Y);

    public static ShapePoint operator *(ShapePoint point, double scalar) =>
        new(point.X * scalar, point.Y * scalar);

    public static ShapePoint operator /(ShapePoint point, double scalar) =>
        new(point.X / scalar, point.Y / scalar);
}

/// <summary>Constants and helpers shared by the ported shape algorithms.</summary>
internal static class ShapeMath
{
    public const double DistanceEpsilon = 1e-4;
    public const double AngleEpsilon = 1e-6;

    /// <summary>
    /// Tolerance for collinearity and tangency checks. People read slightly broken lines as
    /// straight, and small displays amplify that, so this is deliberately looser.
    /// </summary>
    public const double RelaxedDistanceEpsilon = 5e-3;

    public const double Pi = Math.PI;
    public const double TwoPi = Math.PI * 2;

    public static double Distance(double x, double y) => Math.Sqrt(x * x + y * y);

    public static double DistanceSquared(double x, double y) => x * x + y * y;

    public static double Square(double value) => value * value;

    public static double Interpolate(double start, double stop, double fraction) =>
        (1 - fraction) * start + fraction * stop;

    /// <summary>Like the remainder operator, but the result is always in [0, mod).</summary>
    public static double PositiveModulo(double num, double mod) => (num % mod + mod) % mod;

    public static ShapePoint DirectionVector(double angleRadians) =>
        new(Math.Cos(angleRadians), Math.Sin(angleRadians));

    public static ShapePoint RadialToCartesian(double radius, double angleRadians,
        ShapePoint center) =>
        DirectionVector(angleRadians) * radius + center;

    public static bool CollinearIsh(double aX, double aY, double bX, double bY, double cX,
        double cY, double tolerance = DistanceEpsilon)
    {
        // The dot product of perpendicular vectors is zero; rotating one vector avoids an
        // atan2/degree conversion.
        var ab = new ShapePoint(bX - aX, bY - aY).Rotate90();
        var ac = new ShapePoint(cX - aX, cY - aY);
        var dotProduct = Math.Abs(ab.DotProduct(ac));
        var relativeTolerance = tolerance * ab.Distance * ac.Distance;
        return dotProduct < tolerance || dotProduct < relativeTolerance;
    }

    /// <summary>Approximates whether the corner at <paramref name="current"/> is convex.</summary>
    public static bool Convex(ShapePoint previous, ShapePoint current, ShapePoint next) =>
        (current - previous).Clockwise(next - current);

    /// <summary>Ternary search for the parameter that minimises a function.</summary>
    public static double FindMinimum(double v0, double v1, Func<double, double> function,
        double tolerance = 1e-3)
    {
        var a = v0;
        var b = v1;
        while (b - a > tolerance)
        {
            var c1 = (2 * a + b) / 3;
            var c2 = (2 * b + a) / 3;
            if (function(c1) < function(c2))
                b = c2;
            else
                a = c1;
        }
        return (a + b) / 2;
    }
}

/// <summary>Axis-aligned rectangle in shape coordinates.</summary>
internal readonly record struct ShapeBounds(double Left, double Top, double Right, double Bottom)
{
    public double Width => Right - Left;

    public double Height => Bottom - Top;

    public ShapePoint Center => new((Left + Right) / 2, (Top + Bottom) / 2);

    public static ShapeBounds Union(ShapeBounds a, ShapeBounds b) =>
        new(Math.Min(a.Left, b.Left), Math.Min(a.Top, b.Top), Math.Max(a.Right, b.Right),
            Math.Max(a.Bottom, b.Bottom));
}
