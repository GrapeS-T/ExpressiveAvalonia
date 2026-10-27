namespace ExpressiveAvalonia.Shapes;

/// <summary>Groups contiguous cubics into edges and corners for morphing.</summary>
internal static class FeatureDetector
{
    /// <summary>
    /// Converts cubics to features one-to-one, except that straight continuations merge into one
    /// edge and non-smooth joints get an empty corner between them. The first and last cubic stay
    /// separate so the shape keeps its start position.
    /// </summary>
    public static List<Feature> DetectFeatures(IReadOnlyList<Cubic> cubics)
    {
        var features = new List<Feature>();
        if (cubics.Count == 0)
            return features;

        var current = cubics[0];
        for (var i = 0; i < cubics.Count; i++)
        {
            var next = cubics[(i + 1) % cubics.Count];

            if (i < cubics.Count - 1 && AlignsIshWith(current, next))
            {
                current = Extend(current, next);
                continue;
            }

            features.Add(AsFeature(current, next));

            if (!SmoothesIntoIsh(current, next))
                features.Add(AsFeature(Cubic.Empty(current.Anchor1.X, current.Anchor1.Y), next));

            current = next;
        }

        return features;
    }

    /// <summary>
    /// A straight cubic becomes an edge; any other cubic becomes a corner whose convexity is
    /// determined by the following cubic.
    /// </summary>
    public static Feature AsFeature(this Cubic cubic, Cubic next) =>
        cubic.StraightIsh()
            ? Feature.BuildEdge(cubic)
            : new Feature.Corner([cubic], cubic.IsConvexTo(next));

    /// <summary>A cubic is straight-ish when both controls are collinear with its anchors.</summary>
    public static bool StraightIsh(this Cubic cubic) =>
        !cubic.IsZeroLength &&
        ShapeMath.CollinearIsh(cubic.Anchor0.X, cubic.Anchor0.Y, cubic.Anchor1.X, cubic.Anchor1.Y,
            cubic.Control0.X, cubic.Control0.Y, ShapeMath.RelaxedDistanceEpsilon) &&
        ShapeMath.CollinearIsh(cubic.Anchor0.X, cubic.Anchor0.Y, cubic.Anchor1.X, cubic.Anchor1.Y,
            cubic.Control1.X, cubic.Control1.Y, ShapeMath.RelaxedDistanceEpsilon);

    /// <summary>
    /// True when <paramref name="next"/> continues this curve smoothly: its first control is the
    /// reflection of this curve's second control, like an SVG S command.
    /// </summary>
    public static bool SmoothesIntoIsh(this Cubic cubic, Cubic next) =>
        ShapeMath.CollinearIsh(cubic.Control1.X, cubic.Control1.Y, next.Control0.X, next.Control0.Y,
            cubic.Anchor1.X, cubic.Anchor1.Y, ShapeMath.RelaxedDistanceEpsilon);

    /// <summary>True when all points of both curves lie on one straight line.</summary>
    public static bool AlignsIshWith(this Cubic cubic, Cubic next) =>
        cubic.StraightIsh() && next.StraightIsh() && cubic.SmoothesIntoIsh(next) ||
        cubic.IsZeroLength ||
        next.IsZeroLength;

    /// <summary>Extends <paramref name="a"/> to the second anchor of <paramref name="b"/>.</summary>
    private static Cubic Extend(Cubic a, Cubic b) =>
        a.IsZeroLength
            ? new Cubic(a.Anchor0, b.Control0, b.Control1, b.Anchor1)
            : new Cubic(a.Anchor0, a.Control0, a.Control1, b.Anchor1);
}
