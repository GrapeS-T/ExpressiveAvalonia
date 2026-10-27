namespace ExpressiveAvalonia.Shapes;

/// <summary>
/// A closed polygon with optional rounding at its vertices, represented as contiguous features.
/// </summary>
/// <remarks>
/// Polygons are authored in normalised coordinates. A regular polygon built from a vertex count
/// has radius one, so it spans the 2 × 2 box around the origin; <see cref="Normalized"/> moves and
/// scales any polygon into the unit square, which is the form the Material shape catalog uses.
/// </remarks>
public sealed class RoundedPolygon
{
    private RoundedPolygon(IReadOnlyList<Feature> features, ShapePoint center)
    {
        Features = features;
        Center = center;
        Cubics = BuildCubics(features, center);

        // Every pair of neighbouring curves must share their anchor exactly; a gap would make a
        // morph and the renderer disagree about the outline.
        var previous = Cubics[^1];
        for (var index = 0; index < Cubics.Count; index++)
        {
            var cubic = Cubics[index];
            if (Math.Abs(cubic.Anchor0.X - previous.Anchor1.X) > ShapeMath.DistanceEpsilon ||
                Math.Abs(cubic.Anchor0.Y - previous.Anchor1.Y) > ShapeMath.DistanceEpsilon)
            {
                throw new ArgumentException(
                    "RoundedPolygon must be contiguous, with the anchor points of all curves " +
                    "matching the anchor points of the preceding and succeeding cubics.",
                    nameof(features));
            }
            previous = cubic;
        }
    }

    internal IReadOnlyList<Feature> Features { get; }

    internal ShapePoint Center { get; }

    /// <summary>The polygon's centroid in its current coordinate space.</summary>
    public double CenterX => Center.X;

    /// <summary>The polygon's centroid in its current coordinate space.</summary>
    public double CenterY => Center.Y;

    /// <summary>A flattened version of the feature list as contiguous cubics.</summary>
    internal IReadOnlyList<Cubic> Cubics { get; }

    /// <summary>Creates a regular polygon with <paramref name="numVertices"/> vertices.</summary>
    public static RoundedPolygon FromNumVertices(int numVertices, double radius = 1,
        double centerX = 0, double centerY = 0, CornerRounding rounding = default,
        IReadOnlyList<CornerRounding>? perVertexRounding = null)
    {
        if (numVertices < 3)
            throw new ArgumentOutOfRangeException(nameof(numVertices), "Polygons must have at least 3 vertices.");

        return FromVertices(VerticesFromNumVerts(numVertices, radius, centerX, centerY), rounding,
            perVertexRounding, centerX, centerY);
    }

    /// <summary>Creates a polygon from an ordered list of x/y vertex pairs.</summary>
    public static RoundedPolygon FromVertices(IReadOnlyList<double> vertices,
        CornerRounding rounding = default, IReadOnlyList<CornerRounding>? perVertexRounding = null,
        double? centerX = null, double? centerY = null)
    {
        if (vertices.Count < 6)
            throw new ArgumentException("Polygons must have at least 3 vertices.", nameof(vertices));
        if (vertices.Count % 2 != 0)
            throw new ArgumentException("The vertices array should have even size.", nameof(vertices));

        var numVertices = vertices.Count / 2;
        if (perVertexRounding != null && perVertexRounding.Count != numVertices)
        {
            throw new ArgumentException(
                "perVertexRounding list should be either null or the same size as the number of " +
                "vertices (vertices.size / 2).",
                nameof(perVertexRounding));
        }

        var roundedCorners = new List<RoundedCorner>(numVertices);
        for (var index = 0; index < numVertices; index++)
        {
            var roundingForVertex = perVertexRounding?[index] ?? rounding;
            var previousIndex = ((index + numVertices - 1) % numVertices) * 2;
            var nextIndex = ((index + 1) % numVertices) * 2;
            roundedCorners.Add(new RoundedCorner(
                new ShapePoint(vertices[previousIndex], vertices[previousIndex + 1]),
                new ShapePoint(vertices[index * 2], vertices[index * 2 + 1]),
                new ShapePoint(vertices[nextIndex], vertices[nextIndex + 1]),
                roundingForVertex));
        }

        // Each side must fit the rounding required by its two corners, then smoothing with any
        // space that is left. A side stores (roundCutRatio, cutRatio).
        var cutAdjusts = new (double RoundCutRatio, double CutRatio)[numVertices];
        for (var index = 0; index < numVertices; index++)
        {
            var expectedRoundCut = roundedCorners[index].ExpectedRoundCut +
                                   roundedCorners[(index + 1) % numVertices].ExpectedRoundCut;
            var expectedCut = roundedCorners[index].ExpectedCut +
                              roundedCorners[(index + 1) % numVertices].ExpectedCut;
            var vertexX = vertices[index * 2];
            var vertexY = vertices[index * 2 + 1];
            var nextVertexX = vertices[((index + 1) % numVertices) * 2];
            var nextVertexY = vertices[((index + 1) % numVertices) * 2 + 1];
            var sideSize = ShapeMath.Distance(vertexX - nextVertexX, vertexY - nextVertexY);

            cutAdjusts[index] = expectedRoundCut > sideSize
                ? (sideSize / expectedRoundCut, 0)
                : expectedCut > sideSize
                    ? (1, (sideSize - expectedRoundCut) / (expectedCut - expectedRoundCut))
                    : (1, 1);
        }

        var corners = new List<IReadOnlyList<Cubic>>(numVertices);
        for (var index = 0; index < numVertices; index++)
        {
            var allowedCuts = new double[2];
            for (var delta = 0; delta <= 1; delta++)
            {
                var (roundCutRatio, cutRatio) =
                    cutAdjusts[(index + numVertices - 1 + delta) % numVertices];
                allowedCuts[delta] =
                    roundedCorners[index].ExpectedRoundCut * roundCutRatio +
                    (roundedCorners[index].ExpectedCut -
                     roundedCorners[index].ExpectedRoundCut) * cutRatio;
            }

            corners.Add(roundedCorners[index].GetCubics(allowedCuts[0], allowedCuts[1]));
        }

        var features = new List<Feature>(numVertices * 2);
        for (var index = 0; index < numVertices; index++)
        {
            var previousVertexIndex = (index + numVertices - 1) % numVertices;
            var nextVertexIndex = (index + 1) % numVertices;
            var currentVertex = new ShapePoint(vertices[index * 2], vertices[index * 2 + 1]);
            var previousVertex = new ShapePoint(vertices[previousVertexIndex * 2],
                vertices[previousVertexIndex * 2 + 1]);
            var nextVertex = new ShapePoint(vertices[nextVertexIndex * 2],
                vertices[nextVertexIndex * 2 + 1]);
            var convex = ShapeMath.Convex(previousVertex, currentVertex, nextVertex);

            features.Add(new Feature.Corner(corners[index], convex));
            features.Add(new Feature.Edge(
            [
                Cubic.StraightLine(corners[index][^1].Anchor1, corners[(index + 1) % numVertices][0].Anchor0)
            ]));
        }

        var (calculatedCenterX, calculatedCenterY) =
            centerX.HasValue && centerY.HasValue
                ? (centerX.Value, centerY.Value)
                : CalculateCenter(vertices);
        return new RoundedPolygon(features, new ShapePoint(calculatedCenterX, calculatedCenterY));
    }

    /// <summary>Creates a polygon from an explicit feature sequence.</summary>
    internal static RoundedPolygon FromFeatures(IReadOnlyList<Feature> features, double? centerX = null,
        double? centerY = null)
    {
        if (features.Count < 2)
            throw new ArgumentException("Polygons must have at least 2 features.", nameof(features));

        var vertices = new List<double>();
        foreach (var feature in features)
        {
            foreach (var cubic in feature.Cubics)
            {
                vertices.Add(cubic.Anchor0.X);
                vertices.Add(cubic.Anchor0.Y);
            }
        }

        var (calculatedCenterX, calculatedCenterY) =
            centerX.HasValue && centerY.HasValue
                ? (centerX.Value, centerY.Value)
                : CalculateCenter(vertices);
        return new RoundedPolygon(features, new ShapePoint(calculatedCenterX, calculatedCenterY));
    }

    /// <summary>Transforms the shape and its center with the given point function.</summary>
    internal RoundedPolygon Transformed(Func<ShapePoint, ShapePoint> transform)
    {
        var features = new Feature[Features.Count];
        for (var index = 0; index < features.Length; index++)
            features[index] = Features[index].Transformed(transform);
        return new RoundedPolygon(features, transform(Center));
    }

    /// <summary>
    /// Moves and resizes this polygon into the unit square, centering the shorter dimension.
    /// </summary>
    public RoundedPolygon Normalized()
    {
        var bounds = CalculateBounds();
        var width = bounds.Width;
        var height = bounds.Height;
        var side = Math.Max(width, height);
        var offsetX = (side - width) / 2 - bounds.Left;
        var offsetY = (side - height) / 2 - bounds.Top;
        return Transformed(point => new ShapePoint((point.X + offsetX) / side,
            (point.Y + offsetY) / side));
    }

    /// <summary>
    /// Returns the square, centered on <see cref="Center"/>, that can hold this shape in any
    /// rotation. The radius is the farthest anchor or curve midpoint from the center.
    /// </summary>
    internal ShapeBounds CalculateMaxBounds()
    {
        var maxDistanceSquared = 0d;
        foreach (var cubic in Cubics)
        {
            var anchorDistance = ShapeMath.DistanceSquared(cubic.Anchor0.X - CenterX,
                cubic.Anchor0.Y - CenterY);
            var midpoint = cubic.PointOnCurve(0.5);
            var middleDistance = ShapeMath.DistanceSquared(midpoint.X - CenterX,
                midpoint.Y - CenterY);
            maxDistanceSquared = Math.Max(maxDistanceSquared,
                Math.Max(anchorDistance, middleDistance));
        }

        var distance = Math.Sqrt(maxDistanceSquared);
        return new ShapeBounds(CenterX - distance, CenterY - distance, CenterX + distance,
            CenterY + distance);
    }

    /// <summary>
    /// Returns tight axis-aligned bounds. <paramref name="approximate"/> uses the control-point
    /// hull, which is cheaper and is what the source uses by default.
    /// </summary>
    internal ShapeBounds CalculateBounds(bool approximate = true)
    {
        var bounds = new ShapeBounds(double.MaxValue, double.MaxValue, double.MinValue,
            double.MinValue);
        foreach (var cubic in Cubics)
        {
            var cubicBounds = cubic.CalculateBounds(approximate);
            bounds = ShapeBounds.Union(bounds, cubicBounds);
        }
        return bounds;
    }

    private static List<Cubic> BuildCubics(IReadOnlyList<Feature> features, ShapePoint center)
    {
        var cubics = new List<Cubic>();
        var firstCubic = (Cubic?)null;
        var lastCubic = (Cubic?)null;
        IReadOnlyList<Cubic>? firstFeatureSplitStart = null;
        IReadOnlyList<Cubic>? firstFeatureSplitEnd = null;

        // Splitting the middle curve of the first feature keeps the start point of a three-curve
        // rounded corner aligned with the closure curve.
        if (features.Count > 0 && features[0].Cubics.Count == 3)
        {
            var centerCubic = features[0].Cubics[1];
            var (start, end) = centerCubic.Split(0.5);
            firstFeatureSplitStart = new[] { features[0].Cubics[0], start };
            firstFeatureSplitEnd = new[] { end, features[0].Cubics[2] };
        }

        // Iterating one past the feature count inserts the initial split when it exists.
        for (var featureIndex = 0; featureIndex <= features.Count; featureIndex++)
        {
            IReadOnlyList<Cubic> featureCubics;
            if (featureIndex == 0 && firstFeatureSplitEnd != null)
            {
                featureCubics = firstFeatureSplitEnd;
            }
            else if (featureIndex == features.Count)
            {
                if (firstFeatureSplitStart == null)
                    break;
                featureCubics = firstFeatureSplitStart;
            }
            else
            {
                featureCubics = features[featureIndex].Cubics;
            }

            foreach (var cubic in featureCubics)
            {
                if (!cubic.IsZeroLength)
                {
                    if (lastCubic is { } previous)
                        cubics.Add(previous);
                    lastCubic = cubic;
                    firstCubic ??= cubic;
                }
                else if (lastCubic is { } previous)
                {
                    // Dropping several zero-length curves can leave enough discontinuity to
                    // fail validation, so the previous curve always adopts the latest anchor.
                    lastCubic = new Cubic(previous.Anchor0, previous.Control0, previous.Control1,
                        cubic.Anchor1);
                }
            }
        }

        if (lastCubic is { } final && firstCubic is { } first)
        {
            // The closure curve makes the last anchor exactly match the first anchor.
            cubics.Add(new Cubic(final.Anchor0, final.Control0, final.Control1, first.Anchor0));
        }
        else
        {
            cubics.Add(Cubic.Empty(center.X, center.Y));
        }

        return cubics;
    }

    private static (double X, double Y) CalculateCenter(IReadOnlyList<double> vertices)
    {
        var cumulativeX = 0d;
        var cumulativeY = 0d;
        for (var index = 0; index < vertices.Count; index += 2)
        {
            cumulativeX += vertices[index];
            cumulativeY += vertices[index + 1];
        }
        return (cumulativeX / (vertices.Count / 2), cumulativeY / (vertices.Count / 2));
    }

    private static double[] VerticesFromNumVerts(int numVertices, double radius, double centerX,
        double centerY)
    {
        var result = new double[numVertices * 2];
        var arrayIndex = 0;
        var center = new ShapePoint(centerX, centerY);
        for (var index = 0; index < numVertices; index++)
        {
            var vertex = ShapeMath.RadialToCartesian(radius,
                ShapeMath.Pi / numVertices * 2 * index, center);
            result[arrayIndex++] = vertex.X;
            result[arrayIndex++] = vertex.Y;
        }
        return result;
    }

    /// <summary>
    /// Holds one corner's geometry. Two steps are needed: first decide how much to cut for the
    /// requested rounding, then learn how much room the neighbouring sides actually allow.
    /// </summary>
    private sealed class RoundedCorner
    {
        private readonly ShapePoint _p0;
        private readonly ShapePoint _p1;
        private readonly ShapePoint _p2;
        private readonly double _cornerRadius;
        private readonly double _smoothing;

        public RoundedCorner(ShapePoint p0, ShapePoint p1, ShapePoint p2, CornerRounding rounding)
        {
            _p0 = p0;
            _p1 = p1;
            _p2 = p2;

            var v01 = p0 - p1;
            var v21 = p2 - p1;
            var d01 = v01.Distance;
            var d21 = v21.Distance;
            if (d01 > 0 && d21 > 0)
            {
                D1 = v01 / d01;
                D2 = v21 / d21;
                _cornerRadius = rounding.Radius;
                _smoothing = rounding.Smoothing;

                CosAngle = D1.DotProduct(D2);
                SinAngle = Math.Sqrt(1 - ShapeMath.Square(CosAngle));

                // tan(A/2) = sinA / (1 + cosA) gives the distance along an edge at which the
                // rounding circle touches it.
                ExpectedRoundCut = SinAngle > 1e-3
                    ? _cornerRadius * (CosAngle + 1) / SinAngle
                    : 0;
            }
            else
            {
                D1 = ShapePoint.Zero;
                D2 = ShapePoint.Zero;
                _cornerRadius = 0;
                _smoothing = 0;
                CosAngle = 0;
                SinAngle = 0;
                ExpectedRoundCut = 0;
            }
        }

        public ShapePoint D1 { get; }

        public ShapePoint D2 { get; }

        public double CosAngle { get; }

        public double SinAngle { get; }

        public double ExpectedRoundCut { get; }

        /// <summary>Smoothing doubles the cut: zero keeps the circular cut, one doubles it.</summary>
        public double ExpectedCut => (1 + _smoothing) * ExpectedRoundCut;

        /// <summary>The center of the rounding circle, or the vertex when there is no rounding.</summary>
        public ShapePoint Center { get; private set; }

        public List<Cubic> GetCubics(double allowedCut0, double? allowedCut1 = null)
        {
            var secondAllowedCut = allowedCut1 ?? allowedCut0;
            var allowedCut = Math.Min(allowedCut0, secondAllowedCut);

            if (ExpectedRoundCut < ShapeMath.DistanceEpsilon ||
                allowedCut < ShapeMath.DistanceEpsilon ||
                _cornerRadius < ShapeMath.DistanceEpsilon)
            {
                Center = _p1;
                return [Cubic.StraightLine(_p1, _p1)];
            }

            var actualRoundCut = Math.Min(allowedCut, ExpectedRoundCut);
            var actualSmoothing0 = ActualSmoothingValue(allowedCut0);
            var actualSmoothing1 = ActualSmoothingValue(secondAllowedCut);

            var actualRadius = _cornerRadius * actualRoundCut / ExpectedRoundCut;
            var centerDistance = Math.Sqrt(ShapeMath.Square(actualRadius) +
                                           ShapeMath.Square(actualRoundCut));
            Center = _p1 + ((D1 + D2) / 2).Direction() * centerDistance;

            var circleIntersection0 = _p1 + D1 * actualRoundCut;
            var circleIntersection2 = _p1 + D2 * actualRoundCut;

            var flanking0 = ComputeFlankingCurve(actualRoundCut, actualSmoothing0, _p1, _p0,
                circleIntersection0, circleIntersection2, Center, actualRadius);
            var flanking2 = ComputeFlankingCurve(actualRoundCut, actualSmoothing1, _p1, _p2,
                circleIntersection2, circleIntersection0, Center, actualRadius).Reverse();

            return
            [
                flanking0,
                Cubic.CircularArc(Center.X, Center.Y, flanking0.Anchor1.X, flanking0.Anchor1.Y,
                    flanking2.Anchor0.X, flanking2.Anchor0.Y),
                flanking2
            ];
        }

        private double ActualSmoothingValue(double allowedCut) =>
            allowedCut > ExpectedCut
                ? _smoothing
                : allowedCut > ExpectedRoundCut
                    ? _smoothing * (allowedCut - ExpectedRoundCut) /
                      (ExpectedCut - ExpectedRoundCut)
                    : 0;

        /// <summary>
        /// Connects a point on the cut straight side to the circle with one cubic, tangent to both.
        /// </summary>
        private static Cubic ComputeFlankingCurve(double actualRoundCut,
            double actualSmoothingValue, ShapePoint corner, ShapePoint sideStart,
            ShapePoint circleSegmentIntersection, ShapePoint otherCircleSegmentIntersection,
            ShapePoint circleCenter, double actualRadius)
        {
            var sideDirection = (sideStart - corner).Direction();
            var curveStart = corner + sideDirection * actualRoundCut * (1 + actualSmoothingValue);

            // Approximate the portion of the circular section to keep: full at smoothing zero,
            // none at smoothing one.
            var point = circleSegmentIntersection.Lerp(
                (circleSegmentIntersection + otherCircleSegmentIntersection) / 2,
                actualSmoothingValue);
            var curveEnd = circleCenter +
                           new ShapePoint(point.X - circleCenter.X, point.Y - circleCenter.Y)
                               .Direction() * actualRadius;

            // The cubic's other anchor lies where the circle tangent meets the straight side.
            var circleTangent = (curveEnd - circleCenter).Rotate90();
            var anchorEnd = LineIntersection(sideStart, sideDirection, curveEnd, circleTangent) ??
                            circleSegmentIntersection;
            var anchorStart = (curveStart + anchorEnd * 2) / 3;
            return new Cubic(curveStart, anchorStart, anchorEnd, curveEnd);
        }

        /// <summary>Intersection of the lines d0 through p0 and d1 through p1, if they meet.</summary>
        private static ShapePoint? LineIntersection(ShapePoint p0, ShapePoint d0, ShapePoint p1,
            ShapePoint d1)
        {
            var rotatedD1 = d1.Rotate90();
            var denominator = d0.DotProduct(rotatedD1);
            if (Math.Abs(denominator) < ShapeMath.DistanceEpsilon)
                return null;

            var numerator = (p1 - p0).DotProduct(rotatedD1);
            // Equivalent to abs(denominator / numerator) < epsilon, without dividing.
            if (Math.Abs(denominator) < ShapeMath.DistanceEpsilon * Math.Abs(numerator))
                return null;
            return p0 + d0 * (numerator / denominator);
        }
    }
}
