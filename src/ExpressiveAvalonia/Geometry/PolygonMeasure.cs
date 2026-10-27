namespace ExpressiveAvalonia.Shapes;

/// <summary>Measures a cubic and finds the parameter at a requested measure.</summary>
internal interface IMeasurer
{
    /// <summary>Returns the cubic's size according to the implementation; never negative.</summary>
    double MeasureCubic(Cubic cubic);

    /// <summary>
    /// Returns the cubic parameter at which <paramref name="measure"/> is reached. Values above
    /// <see cref="MeasureCubic"/> are capped at the end of the curve.
    /// </summary>
    double FindCubicCutPoint(Cubic cubic, double measure);
}

/// <summary>
/// A closed polygon split into measured cubics, each with the [0..1] outline progress it covers.
/// </summary>
internal sealed class MeasuredPolygon : IReadOnlyList<MeasuredPolygon.MeasuredCubic>
{
    private readonly IMeasurer _measurer;
    private readonly List<MeasuredCubic> _cubics;

    private MeasuredPolygon(IMeasurer measurer, List<ProgressableFeature> features,
        IReadOnlyList<Cubic> cubics, IReadOnlyList<double> outlineProgress)
    {
        if (outlineProgress.Count != cubics.Count + 1)
        {
            throw new ArgumentException(
                "Outline progress size is expected to be the cubics size + 1",
                nameof(outlineProgress));
        }
        if (outlineProgress[0] != 0)
            throw new ArgumentException("First outline progress value is expected to be zero",
                nameof(outlineProgress));
        if (outlineProgress[outlineProgress.Count - 1] != 1)
            throw new ArgumentException("Last outline progress value is expected to be one",
                nameof(outlineProgress));

        _measurer = measurer;
        Features = features;

        var measuredCubics = new List<MeasuredCubic>(cubics.Count);
        var startOutlineProgress = 0d;
        for (var index = 0; index < cubics.Count; index++)
        {
            // Filter out cubics that cover no outline length.
            if (outlineProgress[index + 1] - outlineProgress[index] > ShapeMath.DistanceEpsilon)
            {
                measuredCubics.Add(new MeasuredCubic(this, cubics[index], startOutlineProgress,
                    outlineProgress[index + 1]));
                startOutlineProgress = outlineProgress[index + 1];
            }
        }

        if (measuredCubics.Count == 0)
        {
            throw new ArgumentException("A measured polygon must have at least one cubic.",
                nameof(cubics));
        }

        // Dropping empty cubics at the end must not lose the final progress value.
        measuredCubics[^1].UpdateProgressRange(endOutlineProgress: 1);
        _cubics = measuredCubics;
    }

    public List<ProgressableFeature> Features { get; }

    public int Count => _cubics.Count;

    public MeasuredCubic this[int index] => _cubics[index];

    public IEnumerator<MeasuredCubic> GetEnumerator() => _cubics.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
        GetEnumerator();

    /// <summary>
    /// Returns a new polygon that starts at <paramref name="cuttingPoint"/>; the cubic crossing
    /// the point is cut and the list is rotated so progress starts at zero again.
    /// </summary>
    public MeasuredPolygon CutAndShift(double cuttingPoint)
    {
        if (cuttingPoint is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(cuttingPoint), cuttingPoint,
                "Cutting point is expected to be between 0 and 1.");
        if (cuttingPoint < ShapeMath.DistanceEpsilon)
            return this;

        var targetIndex = -1;
        for (var index = 0; index < _cubics.Count; index++)
        {
            var cubic = _cubics[index];
            if (cuttingPoint >= cubic.StartOutlineProgress &&
                cuttingPoint <= cubic.EndOutlineProgress)
            {
                targetIndex = index;
                break;
            }
        }

        if (targetIndex < 0)
            throw new InvalidOperationException("Cutting point lies outside the measured polygon.");

        var target = _cubics[targetIndex];
        var (before, after) = target.CutAtProgress(cuttingPoint);

        // The second half of the cut cubic, everything after the target, everything before it,
        // and finally the first half of the target.
        var returnedCubics = new List<Cubic>(_cubics.Count + 1) { after.Cubic };
        for (var index = 1; index < _cubics.Count; index++)
            returnedCubics.Add(_cubics[(index + targetIndex) % _cubics.Count].Cubic);
        returnedCubics.Add(before.Cubic);

        var returnedProgress = new double[_cubics.Count + 2];
        for (var index = 0; index < returnedProgress.Length; index++)
        {
            returnedProgress[index] = index switch
            {
                0 => 0,
                _ when index == _cubics.Count + 1 => 1,
                _ => ShapeMath.PositiveModulo(
                    _cubics[(targetIndex + index - 1) % _cubics.Count].EndOutlineProgress -
                    cuttingPoint, 1)
            };
        }

        var newFeatures = new List<ProgressableFeature>(Features.Count);
        foreach (var feature in Features)
        {
            newFeatures.Add(new ProgressableFeature(
                ShapeMath.PositiveModulo(feature.Progress - cuttingPoint, 1), feature.Feature));
        }

        return new MeasuredPolygon(_measurer, newFeatures, returnedCubics, returnedProgress);
    }

    public static MeasuredPolygon MeasurePolygon(IMeasurer measurer, RoundedPolygon polygon)
    {
        var cubics = new List<Cubic>();
        var featureToCubic = new List<(Feature Feature, int CubicIndex)>();

        // Flatten the polygon and remember the middle cubic of every corner, which is the arc
        // used as that feature's representative.
        foreach (var feature in polygon.Features)
        {
            for (var cubicIndex = 0; cubicIndex < feature.Cubics.Count; cubicIndex++)
            {
                if (feature is Feature.Corner && cubicIndex == feature.Cubics.Count / 2)
                    featureToCubic.Add((feature, cubics.Count));
                cubics.Add(feature.Cubics[cubicIndex]);
            }
        }

        var measures = new double[cubics.Count + 1];
        for (var index = 0; index < cubics.Count; index++)
        {
            var measure = measurer.MeasureCubic(cubics[index]);
            if (measure < 0)
                throw new ArgumentException("Measured cubic is expected to be at least zero.");
            measures[index + 1] = measures[index] + measure;
        }

        var totalMeasure = measures[^1];
        if (totalMeasure <= 0)
            throw new InvalidOperationException("A polygon must have a non-zero outline length.");

        var outlineProgress = new double[measures.Length];
        for (var index = 0; index < measures.Length; index++)
            outlineProgress[index] = measures[index] / totalMeasure;

        var features = new List<ProgressableFeature>(featureToCubic.Count);
        foreach (var (feature, cubicIndex) in featureToCubic)
        {
            features.Add(new ProgressableFeature(
                ShapeMath.PositiveModulo(
                    (outlineProgress[cubicIndex] + outlineProgress[cubicIndex + 1]) / 2, 1),
                feature));
        }

        return new MeasuredPolygon(measurer, features, cubics, outlineProgress);
    }

    /// <summary>One cubic plus the progress range it covers on the outline.</summary>
    internal sealed class MeasuredCubic
    {
        private readonly MeasuredPolygon _owner;

        public MeasuredCubic(MeasuredPolygon owner, Cubic cubic, double startOutlineProgress,
            double endOutlineProgress)
        {
            if (endOutlineProgress < startOutlineProgress)
            {
                throw new ArgumentException(
                    "endOutlineProgress is expected to be equal or greater than " +
                    "startOutlineProgress");
            }

            _owner = owner;
            Cubic = cubic;
            StartOutlineProgress = startOutlineProgress;
            EndOutlineProgress = endOutlineProgress;
            MeasuredSize = owner._measurer.MeasureCubic(cubic);
        }

        public Cubic Cubic { get; }

        public double StartOutlineProgress { get; private set; }

        public double EndOutlineProgress { get; private set; }

        public double MeasuredSize { get; }

        public void UpdateProgressRange(double? startOutlineProgress = null,
            double? endOutlineProgress = null)
        {
            var start = startOutlineProgress ?? StartOutlineProgress;
            var end = endOutlineProgress ?? EndOutlineProgress;
            if (end < start)
            {
                throw new ArgumentException(
                    "endOutlineProgress is expected to be equal or greater than " +
                    "startOutlineProgress");
            }
            StartOutlineProgress = start;
            EndOutlineProgress = end;
        }

        /// <summary>Cuts this measured cubic at the given outline progress.</summary>
        public (MeasuredCubic Before, MeasuredCubic After) CutAtProgress(
            double cutOutlineProgress)
        {
            var bounded = Math.Clamp(cutOutlineProgress, StartOutlineProgress, EndOutlineProgress);
            var outlineProgressSize = EndOutlineProgress - StartOutlineProgress;
            var progressFromStart = bounded - StartOutlineProgress;
            var relativeProgress = progressFromStart / outlineProgressSize;
            var t = _owner._measurer.FindCubicCutPoint(Cubic, relativeProgress * MeasuredSize);
            if (t is < 0 or > 1)
            {
                throw new InvalidOperationException(
                    "Cubic cut point is expected to be between 0 and 1.");
            }

            var (before, after) = Cubic.Split(t);
            return (new MeasuredCubic(_owner, before, StartOutlineProgress, bounded),
                new MeasuredCubic(_owner, after, bounded, EndOutlineProgress));
        }

        public override string ToString() =>
            $"MeasuredCubic(outlineProgress=[{StartOutlineProgress} .. {EndOutlineProgress}], " +
            $"size={MeasuredSize}, cubic={Cubic})";
    }
}

/// <summary>
/// Approximates cubic arc length by splitting each curve into three segments. The source notes at
/// least 98.5% accuracy for a circular arc, the worst case for the Material shape set.
/// </summary>
internal sealed class LengthMeasurer : IMeasurer
{
    private const int Segments = 3;

    public double MeasureCubic(Cubic cubic) =>
        ClosestProgressTo(cubic, double.PositiveInfinity).Total;

    public double FindCubicCutPoint(Cubic cubic, double measure) =>
        ClosestProgressTo(cubic, measure).Progress;

    private static (double Progress, double Total) ClosestProgressTo(Cubic cubic,
        double threshold)
    {
        var total = 0d;
        var remainder = threshold;
        var previous = cubic.Anchor0;

        for (var index = 1; index <= Segments; index++)
        {
            var progress = (double)index / Segments;
            var point = cubic.PointOnCurve(progress);
            var segment = (point - previous).Distance;

            if (segment >= remainder)
            {
                return (progress - (1 - remainder / segment) / Segments, threshold);
            }

            remainder -= segment;
            total += segment;
            previous = point;
        }

        return (1, total);
    }
}
