namespace ExpressiveAvalonia.Shapes;

/// <summary>Factories for the basic rounded polygon shapes.</summary>
public static class RoundedShapes
{
    /// <summary>
    /// A circle approximated with <paramref name="numVertices"/> rounded polygon vertices.
    /// </summary>
    public static RoundedPolygon Circle(int numVertices = 8, double radius = 1,
        double centerX = 0, double centerY = 0)
    {
        if (numVertices < 3)
            throw new ArgumentOutOfRangeException(nameof(numVertices),
                "Circle must have at least three vertices.");

        // Half the angle between adjacent vertices; the polygon radius is enlarged so the
        // rounded arcs pass through the requested circle.
        var theta = ShapeMath.Pi / numVertices;
        var polygonRadius = radius / Math.Cos(theta);
        return RoundedPolygon.FromNumVertices(numVertices, polygonRadius, centerX, centerY,
            new CornerRounding(radius));
    }

    /// <summary>A rectangle with optional per-corner rounding.</summary>
    public static RoundedPolygon Rectangle(double width = 2, double height = 2,
        CornerRounding rounding = default, IReadOnlyList<CornerRounding>? perVertexRounding = null,
        double centerX = 0, double centerY = 0)
    {
        var left = centerX - width / 2;
        var top = centerY - height / 2;
        var right = centerX + width / 2;
        var bottom = centerY + height / 2;
        return RoundedPolygon.FromVertices(
        [
            right, bottom,
            left, bottom,
            left, top,
            right, top
        ], rounding, perVertexRounding, centerX, centerY);
    }

    /// <summary>A star with alternating outer and inner radii.</summary>
    public static RoundedPolygon Star(int numVerticesPerRadius, double radius = 1,
        double innerRadius = 0.5, CornerRounding rounding = default,
        CornerRounding? innerRounding = null,
        IReadOnlyList<CornerRounding>? perVertexRounding = null, double centerX = 0,
        double centerY = 0)
    {
        if (radius <= 0 || innerRadius <= 0)
            throw new ArgumentException("Star radii must both be greater than 0.");
        if (innerRadius >= radius)
            throw new ArgumentException("innerRadius must be less than radius.");

        var roundingList = perVertexRounding;
        if (roundingList == null && innerRounding.HasValue)
        {
            var generatedRounding = new CornerRounding[numVerticesPerRadius * 2];
            for (var index = 0; index < numVerticesPerRadius; index++)
            {
                generatedRounding[index * 2] = rounding;
                generatedRounding[index * 2 + 1] = innerRounding.Value;
            }
            roundingList = generatedRounding;
        }

        return RoundedPolygon.FromVertices(
            StarVerticesFromNumVerts(numVerticesPerRadius, radius, innerRadius, centerX, centerY),
            rounding, roundingList, centerX, centerY);
    }

    /// <summary>A pill: a rectangle capped by two semicircles.</summary>
    public static RoundedPolygon Pill(double width = 2, double height = 1, double smoothing = 0,
        double centerX = 0, double centerY = 0)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentException("Pill shapes must have positive width and height.");

        var halfWidth = width / 2;
        var halfHeight = height / 2;
        return RoundedPolygon.FromVertices(
        [
            halfWidth + centerX, halfHeight + centerY,
            -halfWidth + centerX, halfHeight + centerY,
            -halfWidth + centerX, -halfHeight + centerY,
            halfWidth + centerX, -halfHeight + centerY
        ], new CornerRounding(Math.Min(halfWidth, halfHeight), smoothing), null, centerX, centerY);
    }

    /// <summary>
    /// A pill whose outline alternates between inner and outer radii, like a star bent around a
    /// pill. Vertex spacing selects whether the inner or outer vertices follow the end caps.
    /// </summary>
    public static RoundedPolygon PillStar(double width = 2, double height = 1,
        int numVerticesPerRadius = 8, double innerRadiusRatio = 0.5,
        CornerRounding rounding = default, CornerRounding? innerRounding = null,
        IReadOnlyList<CornerRounding>? perVertexRounding = null, double vertexSpacing = 0.5,
        double startLocation = 0, double centerX = 0, double centerY = 0)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentException("Pill shapes must have positive width and height.");
        if (innerRadiusRatio is <= 0 or > 1)
            throw new ArgumentException("innerRadius must be between 0 and 1.");

        var roundingList = perVertexRounding;
        if (roundingList == null && innerRounding.HasValue)
        {
            var generatedRounding = new CornerRounding[numVerticesPerRadius * 2];
            for (var index = 0; index < numVerticesPerRadius; index++)
            {
                generatedRounding[index * 2] = rounding;
                generatedRounding[index * 2 + 1] = innerRounding.Value;
            }
            roundingList = generatedRounding;
        }

        return RoundedPolygon.FromVertices(
            PillStarVerticesFromNumVerts(numVerticesPerRadius, width, height, innerRadiusRatio,
                vertexSpacing, startLocation, centerX, centerY),
            rounding, roundingList, centerX, centerY);
    }

    private static double[] PillStarVerticesFromNumVerts(int numVerticesPerRadius, double width,
        double height, double innerRadius, double vertexSpacing, double startLocation,
        double centerX, double centerY)
    {
        // Walk the perimeter of the underlying pill. Vertices are placed by their distance along
        // that perimeter, which may lie on a straight edge or on one of the four end-cap arcs.
        var endcapRadius = Math.Min(width, height);
        var verticalSegmentLength = Math.Max(height - width, 0);
        var horizontalSegmentLength = Math.Max(width - height, 0);
        var verticalHalf = verticalSegmentLength / 2;
        var horizontalHalf = horizontalSegmentLength / 2;

        var circlePerimeter = ShapeMath.TwoPi * endcapRadius *
                              ShapeMath.Interpolate(innerRadius, 1, vertexSpacing);
        var perimeter = 2 * horizontalSegmentLength + 2 * verticalSegmentLength + circlePerimeter;

        // Start value of each outline section, in perimeter distance.
        var sections = new double[11];
        sections[0] = 0;
        sections[1] = verticalSegmentLength / 2;
        sections[2] = sections[1] + circlePerimeter / 4;
        sections[3] = sections[2] + horizontalSegmentLength;
        sections[4] = sections[3] + circlePerimeter / 4;
        sections[5] = sections[4] + verticalSegmentLength;
        sections[6] = sections[5] + circlePerimeter / 4;
        sections[7] = sections[6] + horizontalSegmentLength;
        sections[8] = sections[7] + circlePerimeter / 4;
        sections[9] = sections[8] + verticalSegmentLength / 2;
        sections[10] = perimeter;

        var distancePerVertex = perimeter / (2 * numVerticesPerRadius);
        var inner = false;
        var currentSectionIndex = 0;
        var sectionStart = 0d;
        var sectionEnd = sections[1];
        var distance = startLocation * perimeter;

        var result = new double[numVerticesPerRadius * 4];
        var arrayIndex = 0;
        var bottomRight = new ShapePoint(horizontalHalf, verticalHalf);
        var bottomLeft = new ShapePoint(-horizontalHalf, verticalHalf);
        var topLeft = new ShapePoint(-horizontalHalf, -verticalHalf);
        var topRight = new ShapePoint(horizontalHalf, -verticalHalf);

        for (var index = 0; index < numVerticesPerRadius * 2; index++)
        {
            var boundedDistance = distance % perimeter;
            if (boundedDistance < sectionStart)
                currentSectionIndex = 0;

            while (boundedDistance >= sections[(currentSectionIndex + 1) % sections.Length])
            {
                currentSectionIndex = (currentSectionIndex + 1) % sections.Length;
                sectionStart = sections[currentSectionIndex];
                sectionEnd = sections[(currentSectionIndex + 1) % sections.Length];
            }

            var distanceInSection = boundedDistance - sectionStart;
            var proportion = distanceInSection / (sectionEnd - sectionStart);

            var currentRadius = inner ? endcapRadius * innerRadius : endcapRadius;
            var vertex = currentSectionIndex switch
            {
                0 => new ShapePoint(currentRadius, proportion * verticalHalf),
                1 => ShapeMath.RadialToCartesian(currentRadius, proportion * ShapeMath.Pi / 2,
                    bottomRight),
                2 => new ShapePoint(horizontalHalf - proportion * horizontalSegmentLength,
                    currentRadius),
                3 => ShapeMath.RadialToCartesian(currentRadius,
                    ShapeMath.Pi / 2 + proportion * ShapeMath.Pi / 2, bottomLeft),
                4 => new ShapePoint(-currentRadius,
                    verticalHalf - proportion * verticalSegmentLength),
                5 => ShapeMath.RadialToCartesian(currentRadius,
                    ShapeMath.Pi + proportion * ShapeMath.Pi / 2, topLeft),
                6 => new ShapePoint(-horizontalHalf + proportion * horizontalSegmentLength,
                    -currentRadius),
                7 => ShapeMath.RadialToCartesian(currentRadius,
                    ShapeMath.Pi * 1.5 + proportion * ShapeMath.Pi / 2, topRight),
                _ => new ShapePoint(currentRadius,
                    -verticalHalf + proportion * verticalHalf)
            };

            result[arrayIndex++] = vertex.X + centerX;
            result[arrayIndex++] = vertex.Y + centerY;
            distance += distancePerVertex;
            inner = !inner;
        }

        return result;
    }

    private static double[] StarVerticesFromNumVerts(int numVerticesPerRadius, double radius,
        double innerRadius, double centerX, double centerY)
    {
        var result = new double[numVerticesPerRadius * 4];
        var arrayIndex = 0;
        var center = new ShapePoint(centerX, centerY);
        for (var index = 0; index < numVerticesPerRadius; index++)
        {
            var vertex = ShapeMath.RadialToCartesian(radius,
                ShapeMath.Pi / numVerticesPerRadius * 2 * index, center);
            result[arrayIndex++] = vertex.X;
            result[arrayIndex++] = vertex.Y;

            vertex = ShapeMath.RadialToCartesian(innerRadius,
                ShapeMath.Pi / numVerticesPerRadius * (2 * index + 1), center);
            result[arrayIndex++] = vertex.X;
            result[arrayIndex++] = vertex.Y;
        }
        return result;
    }
}
