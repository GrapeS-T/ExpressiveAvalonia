namespace ExpressiveAvalonia.Shapes;

/// <summary>Helpers for progress values, which live in [0, 1) and wrap around.</summary>
internal static class ProgressMapping
{
    /// <summary>
    /// Whether <paramref name="progress"/> is inside the wrapped range
    /// <paramref name="from"/>..<paramref name="to"/>.
    /// </summary>
    public static bool InRange(double progress, double from, double to) =>
        to >= from
            ? progress >= from && progress <= to
            : progress >= from || progress <= to;

    /// <summary>Linearly maps between two sets of progress values, wrapping at one.</summary>
    public static double LinearMap(IReadOnlyList<double> xValues, IReadOnlyList<double> yValues,
        double x)
    {
        if (x is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(x), x, "Invalid progress.");

        var segmentStartIndex = -1;
        for (var index = 0; index < xValues.Count; index++)
        {
            if (!InRange(x, xValues[index], xValues[(index + 1) % xValues.Count]))
                continue;
            segmentStartIndex = index;
            break;
        }

        if (segmentStartIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(x), x, "Progress lies outside the mapping.");

        var segmentEndIndex = (segmentStartIndex + 1) % xValues.Count;
        var segmentSizeX = ShapeMath.PositiveModulo(
            xValues[segmentEndIndex] - xValues[segmentStartIndex], 1);
        var segmentSizeY = ShapeMath.PositiveModulo(
            yValues[segmentEndIndex] - yValues[segmentStartIndex], 1);
        var positionInSegment = segmentSizeX < 0.001
            ? 0.5
            : ShapeMath.PositiveModulo(x - xValues[segmentStartIndex], 1) / segmentSizeX;
        return ShapeMath.PositiveModulo(
            yValues[segmentStartIndex] + segmentSizeY * positionInSegment, 1);
    }

    /// <summary>
    /// Maps progress in either direction between two shapes using a finite list of
    /// representative mappings, interpolated linearly and wrapped.
    /// </summary>
    internal sealed class DoubleMapper
    {
        private readonly double[] _sourceValues;
        private readonly double[] _targetValues;

        public DoubleMapper(params (double Source, double Target)[] mappings)
        {
            _sourceValues = new double[mappings.Length];
            _targetValues = new double[mappings.Length];
            for (var index = 0; index < mappings.Length; index++)
            {
                _sourceValues[index] = mappings[index].Source;
                _targetValues[index] = mappings[index].Target;
            }

            ValidateProgress(_sourceValues);
            ValidateProgress(_targetValues);
        }

        public static DoubleMapper Identity { get; } = new((0, 0), (0.5, 0.5));

        public double Map(double x) => LinearMap(_sourceValues, _targetValues, x);

        public double MapBack(double x) => LinearMap(_targetValues, _sourceValues, x);
    }

    /// <summary>
    /// Validates monotonically increasing progress with at most one wrap, and no repeated value.
    /// </summary>
    public static void ValidateProgress(IReadOnlyList<double> progress)
    {
        if (progress.Count == 0)
            return;

        var previous = progress[progress.Count - 1];
        var wraps = 0;
        for (var index = 0; index < progress.Count; index++)
        {
            var current = progress[index];
            if (current is < 0 or >= 1)
            {
                throw new ArgumentException(
                    "FloatMapping - Progress outside of range: " + Join(progress),
                    nameof(progress));
            }

            if (ProgressDistance(current, previous) <= ShapeMath.DistanceEpsilon)
            {
                throw new ArgumentException(
                    "FloatMapping - Progress repeats a value: " + Join(progress),
                    nameof(progress));
            }

            if (current < previous)
            {
                wraps++;
                if (wraps > 1)
                {
                    throw new ArgumentException(
                        "FloatMapping - Progress wraps more than once: " + Join(progress),
                        nameof(progress));
                }
            }

            previous = current;
        }

        static string Join(IReadOnlyList<double> values) =>
            string.Join(", ", values.Select(value => value.ToString("R",
                System.Globalization.CultureInfo.InvariantCulture)));
    }

    /// <summary>Distance between two progress values, accounting for the wrap at one.</summary>
    public static double ProgressDistance(double p1, double p2)
    {
        var distance = Math.Abs(p1 - p2);
        return Math.Min(distance, 1 - distance);
    }
}
