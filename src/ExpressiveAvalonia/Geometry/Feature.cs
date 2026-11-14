namespace ExpressiveAvalonia.Shapes;

/// <summary>
/// A group of contiguous cubics that the morph mapping treats as one outline feature.
/// </summary>
/// <remarks>
/// Grouping reduces noise (rounding a rectangle adds many cubics but does not add corners) and
/// gives the mapper a stable base: convex corners match convex corners, concave match concave,
/// and edges are ignored by the default mapping.
/// </remarks>
internal abstract class Feature
{
    protected Feature(IReadOnlyList<Cubic> cubics)
    {
        Cubics = cubics;
    }

    public IReadOnlyList<Cubic> Cubics { get; }

    public abstract Feature Transformed(Func<ShapePoint, ShapePoint> transform);

    public abstract Feature Reversed();

    /// <summary>Whether the default morph mapping ignores this feature.</summary>
    public abstract bool IsIgnorableFeature { get; }

    public abstract bool IsEdge { get; }

    public abstract bool IsConvexCorner { get; }

    public abstract bool IsConcaveCorner { get; }

    public static Feature BuildIgnorableFeature(IReadOnlyList<Cubic> cubics) =>
        Validated(new Edge(cubics));

    public static Feature BuildEdge(Cubic cubic) => new Edge([cubic]);

    public static Feature BuildConvexCorner(IReadOnlyList<Cubic> cubics) =>
        Validated(new Corner(cubics, true));

    public static Feature BuildConcaveCorner(IReadOnlyList<Cubic> cubics) =>
        Validated(new Corner(cubics, false));

    private static Feature Validated(Feature feature)
    {
        if (feature.Cubics.Count == 0)
            throw new ArgumentException("Features need at least one cubic.", nameof(feature));

        if (!IsContinuous(feature))
        {
            throw new ArgumentException(
                "Feature must be continuous, with the anchor points of all cubics matching the " +
                "anchor points of the preceding and succeeding cubics.",
                nameof(feature));
        }

        return feature;
    }

    private static bool IsContinuous(Feature feature)
    {
        var previous = feature.Cubics[0];
        for (var index = 1; index < feature.Cubics.Count; index++)
        {
            var cubic = feature.Cubics[index];
            if (Math.Abs(cubic.Anchor0.X - previous.Anchor1.X) > ShapeMath.DistanceEpsilon ||
                Math.Abs(cubic.Anchor0.Y - previous.Anchor1.Y) > ShapeMath.DistanceEpsilon)
            {
                return false;
            }
            previous = cubic;
        }
        return true;
    }

    /// <summary>An edge has no inward or outward indentation and is ignored by the mapper.</summary>
    internal sealed class Edge : Feature
    {
        public Edge(IReadOnlyList<Cubic> cubics)
            : base(cubics)
        {
        }

        public override Feature Transformed(Func<ShapePoint, ShapePoint> transform)
        {
            var cubics = new Cubic[this.Cubics.Count];
            for (var index = 0; index < cubics.Length; index++)
                cubics[index] = this.Cubics[index].Transformed(transform);
            return new Edge(cubics);
        }

        public override Feature Reversed()
        {
            var cubics = new Cubic[this.Cubics.Count];
            for (var index = 0; index < cubics.Length; index++)
                cubics[index] = this.Cubics[this.Cubics.Count - 1 - index].Reverse();
            return new Edge(cubics);
        }

        public override bool IsIgnorableFeature => true;

        public override bool IsEdge => true;

        public override bool IsConvexCorner => false;

        public override bool IsConcaveCorner => false;

        public override string ToString() => "Edge";
    }

    /// <summary>A rounded or sharp corner, convex or concave.</summary>
    internal sealed class Corner : Feature
    {
        public Corner(IReadOnlyList<Cubic> cubics, bool convex = true)
            : base(cubics)
        {
            Convex = convex;
        }

        public bool Convex { get; }

        public override Feature Transformed(Func<ShapePoint, ShapePoint> transform)
        {
            var cubics = new Cubic[this.Cubics.Count];
            for (var index = 0; index < cubics.Length; index++)
                cubics[index] = this.Cubics[index].Transformed(transform);
            return new Corner(cubics, Convex);
        }

        public override Feature Reversed()
        {
            var cubics = new Cubic[this.Cubics.Count];
            for (var index = 0; index < cubics.Length; index++)
                cubics[index] = this.Cubics[this.Cubics.Count - 1 - index].Reverse();
            // The source negates the flag: reversing changes the traversal direction.
            return new Corner(cubics, !Convex);
        }

        public override bool IsIgnorableFeature => false;

        public override bool IsEdge => false;

        public override bool IsConvexCorner => Convex;

        public override bool IsConcaveCorner => !Convex;

        public override string ToString() =>
            $"Corner: cubics=[{string.Join(", ", this.Cubics)}] convex={Convex}";
    }
}

/// <summary>A feature together with its [0..1] progress along the outline.</summary>
internal sealed class ProgressableFeature
{
    public ProgressableFeature(double progress, Feature feature)
    {
        Progress = progress;
        Feature = feature;
    }

    public double Progress { get; }

    public Feature Feature { get; }
}
