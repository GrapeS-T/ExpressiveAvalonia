namespace ExpressiveAvalonia.Shapes;

/// <summary>Builds the progress-to-progress mapping between the features of two shapes.</summary>
internal static class FeatureMapping
{
    private static readonly (double Source, double Target)[] IdentityMapping =
        [(0, 0), (0.5, 0.5)];

    /// <summary>Maps the corners of two measured shape sequences, ignoring edges.</summary>
    public static ProgressMapping.DoubleMapper FeatureMapper(
        IReadOnlyList<ProgressableFeature> features1,
        IReadOnlyList<ProgressableFeature> features2)
    {
        var filtered1 = new List<ProgressableFeature>();
        var filtered2 = new List<ProgressableFeature>();
        foreach (var feature in features1)
        {
            if (feature.Feature is Feature.Corner)
                filtered1.Add(feature);
        }
        foreach (var feature in features2)
        {
            if (feature.Feature is Feature.Corner)
                filtered2.Add(feature);
        }

        var mapping = DoMapping(filtered1, filtered2);
        return new ProgressMapping.DoubleMapper(mapping);
    }

    /// <summary>
    /// Maps every feature of one shape to at most one feature of the other, in ascending distance
    /// order, while preventing crossings.
    /// </summary>
    public static (double Source, double Target)[] DoMapping(
        IReadOnlyList<ProgressableFeature> features1,
        IReadOnlyList<ProgressableFeature> features2)
    {
        var distanceVertices = new List<DistanceVertex>();
        foreach (var f1 in features1)
        {
            foreach (var f2 in features2)
            {
                var distance = FeatureDistSquared(f1.Feature, f2.Feature);
                if (distance != double.MaxValue)
                    distanceVertices.Add(new DistanceVertex(distance, f1, f2));
            }
        }

        distanceVertices = distanceVertices.OrderBy(vertex => vertex.Distance).ToList();

        if (distanceVertices.Count == 0)
            return IdentityMapping;
        if (distanceVertices.Count == 1)
        {
            var only = distanceVertices[0];
            return
            [
                (only.F1.Progress, only.F2.Progress),
                (ShapeMath.PositiveModulo(only.F1.Progress + 0.5, 1),
                    ShapeMath.PositiveModulo(only.F2.Progress + 0.5, 1))
            ];
        }

        var helper = new MappingHelper();
        foreach (var vertex in distanceVertices)
            helper.AddMapping(vertex.F1, vertex.F2);
        return helper.Mapping.ToArray();
    }

    private readonly record struct DistanceVertex(double Distance,
        ProgressableFeature F1, ProgressableFeature F2);

    /// <summary>Incrementally builds a non-crossing, one-to-one feature mapping.</summary>
    private sealed class MappingHelper
    {
        public List<(double Source, double Target)> Mapping { get; } = [];

        private readonly HashSet<ProgressableFeature> _usedF1 = [];
        private readonly HashSet<ProgressableFeature> _usedF2 = [];

        public void AddMapping(ProgressableFeature f1, ProgressableFeature f2)
        {
            if (_usedF1.Contains(f1) || _usedF2.Contains(f2))
                return;

            // The list is sorted by source progress; find where the new pair belongs.
            var insertionIndex = 0;
            while (insertionIndex < Mapping.Count &&
                   Mapping[insertionIndex].Source < f1.Progress)
            {
                insertionIndex++;
            }

            if (insertionIndex < Mapping.Count &&
                Mapping[insertionIndex].Source == f1.Progress)
            {
                throw new InvalidOperationException(
                    "There can't be two features with the same progress.");
            }
            var count = Mapping.Count;

            if (count >= 1)
            {
                var before = Mapping[(insertionIndex + count - 1) % count];
                var after = Mapping[insertionIndex % count];

                // Very close features make the DoubleMapper unstable.
                if (ProgressMapping.ProgressDistance(f1.Progress, before.Source) <
                        ShapeMath.DistanceEpsilon ||
                    ProgressMapping.ProgressDistance(f1.Progress, after.Source) <
                        ShapeMath.DistanceEpsilon ||
                    ProgressMapping.ProgressDistance(f2.Progress, before.Target) <
                        ShapeMath.DistanceEpsilon ||
                    ProgressMapping.ProgressDistance(f2.Progress, after.Target) <
                        ShapeMath.DistanceEpsilon)
                {
                    return;
                }

                // With two or more pairs, don't add a crossing.
                if (count > 1 && !ProgressMapping.InRange(f2.Progress, before.Target, after.Target))
                    return;
            }

            Mapping.Insert(insertionIndex, (f1.Progress, f2.Progress));
            _usedF1.Add(f1);
            _usedF2.Add(f2);
        }
    }

    /// <summary>Distance between two features, or MaxValue for mismatched concavity.</summary>
    public static double FeatureDistSquared(Feature f1, Feature f2)
    {
        if (f1 is Feature.Corner c1 && f2 is Feature.Corner c2 && c1.Convex != c2.Convex)
            return double.MaxValue;
        return (RepresentativePoint(f1) - RepresentativePoint(f2)).DistanceSquared;
    }

    /// <summary>The midpoint between the first start anchor and the last end anchor.</summary>
    public static ShapePoint RepresentativePoint(Feature feature) => new(
        (feature.Cubics[0].Anchor0.X + feature.Cubics[^1].Anchor1.X) / 2,
        (feature.Cubics[0].Anchor0.Y + feature.Cubics[^1].Anchor1.Y) / 2);
}
