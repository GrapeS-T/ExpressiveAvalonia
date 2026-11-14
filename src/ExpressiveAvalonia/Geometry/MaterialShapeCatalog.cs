namespace ExpressiveAvalonia.Shapes;

/// <summary>
/// The predefined Material Expressive polygon catalog. Every shape is normalized into the unit
/// square and cached; morphs between them can be built directly.
/// </summary>
internal static class MaterialShapeCatalog
{
    private static readonly CornerRounding Round15 = new(0.15);
    private static readonly CornerRounding Round20 = new(0.2);
    private static readonly CornerRounding Round30 = new(0.3);
    private static readonly CornerRounding Round50 = new(0.5);
    private static readonly CornerRounding Round100 = new(1);

    private static readonly Lazy<RoundedPolygon> LazyCircle = Create(CircleCore);
    private static readonly Lazy<RoundedPolygon> LazySquare = Create(SquareCore);
    private static readonly Lazy<RoundedPolygon> LazySlanted = Create(SlantedCore);
    private static readonly Lazy<RoundedPolygon> LazyArch = Create(ArchCore);
    private static readonly Lazy<RoundedPolygon> LazyFan = Create(FanCore);
    private static readonly Lazy<RoundedPolygon> LazyArrow = Create(ArrowCore);
    private static readonly Lazy<RoundedPolygon> LazySemiCircle = Create(SemiCircleCore);
    private static readonly Lazy<RoundedPolygon> LazyOval = Create(OvalCore);
    private static readonly Lazy<RoundedPolygon> LazyPill = Create(PillCore);
    private static readonly Lazy<RoundedPolygon> LazyTriangle = Create(TriangleCore);
    private static readonly Lazy<RoundedPolygon> LazyDiamond = Create(DiamondCore);
    private static readonly Lazy<RoundedPolygon> LazyClamShell = Create(ClamShellCore);
    private static readonly Lazy<RoundedPolygon> LazyPentagon = Create(PentagonCore);
    private static readonly Lazy<RoundedPolygon> LazyGem = Create(GemCore);
    private static readonly Lazy<RoundedPolygon> LazyVerySunny = Create(VerySunnyCore);
    private static readonly Lazy<RoundedPolygon> LazySunny = Create(SunnyCore);
    private static readonly Lazy<RoundedPolygon> LazyCookie4Sided = Create(Cookie4Core);
    private static readonly Lazy<RoundedPolygon> LazyCookie6Sided = Create(Cookie6Core);
    private static readonly Lazy<RoundedPolygon> LazyCookie7Sided = Create(Cookie7Core);
    private static readonly Lazy<RoundedPolygon> LazyCookie9Sided = Create(Cookie9Core);
    private static readonly Lazy<RoundedPolygon> LazyCookie12Sided = Create(Cookie12Core);
    private static readonly Lazy<RoundedPolygon> LazyGhostish = Create(GhostishCore);
    private static readonly Lazy<RoundedPolygon> LazyClover4Leaf = Create(Clover4Core);
    private static readonly Lazy<RoundedPolygon> LazyClover8Leaf = Create(Clover8Core);
    private static readonly Lazy<RoundedPolygon> LazyBurst = Create(BurstCore);
    private static readonly Lazy<RoundedPolygon> LazySoftBurst = Create(SoftBurstCore);
    private static readonly Lazy<RoundedPolygon> LazyBoom = Create(BoomCore);
    private static readonly Lazy<RoundedPolygon> LazySoftBoom = Create(SoftBoomCore);
    private static readonly Lazy<RoundedPolygon> LazyFlower = Create(FlowerCore);
    private static readonly Lazy<RoundedPolygon> LazyPuffy = Create(PuffyCore);
    private static readonly Lazy<RoundedPolygon> LazyPuffyDiamond = Create(PuffyDiamondCore);
    private static readonly Lazy<RoundedPolygon> LazyPixelCircle = Create(PixelCircleCore);
    private static readonly Lazy<RoundedPolygon> LazyPixelTriangle = Create(PixelTriangleCore);
    private static readonly Lazy<RoundedPolygon> LazyBun = Create(BunCore);
    private static readonly Lazy<RoundedPolygon> LazyHeart = Create(HeartCore);

    public static RoundedPolygon Circle => LazyCircle.Value;
    public static RoundedPolygon Square => LazySquare.Value;
    public static RoundedPolygon Slanted => LazySlanted.Value;
    public static RoundedPolygon Arch => LazyArch.Value;
    public static RoundedPolygon Fan => LazyFan.Value;
    public static RoundedPolygon Arrow => LazyArrow.Value;
    public static RoundedPolygon SemiCircle => LazySemiCircle.Value;
    public static RoundedPolygon Oval => LazyOval.Value;
    public static RoundedPolygon Pill => LazyPill.Value;
    public static RoundedPolygon Triangle => LazyTriangle.Value;
    public static RoundedPolygon Diamond => LazyDiamond.Value;
    public static RoundedPolygon ClamShell => LazyClamShell.Value;
    public static RoundedPolygon Pentagon => LazyPentagon.Value;
    public static RoundedPolygon Gem => LazyGem.Value;
    public static RoundedPolygon Sunny => LazySunny.Value;
    public static RoundedPolygon VerySunny => LazyVerySunny.Value;
    public static RoundedPolygon Cookie4Sided => LazyCookie4Sided.Value;
    public static RoundedPolygon Cookie6Sided => LazyCookie6Sided.Value;
    public static RoundedPolygon Cookie7Sided => LazyCookie7Sided.Value;
    public static RoundedPolygon Cookie9Sided => LazyCookie9Sided.Value;
    public static RoundedPolygon Cookie12Sided => LazyCookie12Sided.Value;
    public static RoundedPolygon Ghostish => LazyGhostish.Value;
    public static RoundedPolygon Clover4Leaf => LazyClover4Leaf.Value;
    public static RoundedPolygon Clover8Leaf => LazyClover8Leaf.Value;
    public static RoundedPolygon Burst => LazyBurst.Value;
    public static RoundedPolygon SoftBurst => LazySoftBurst.Value;
    public static RoundedPolygon Boom => LazyBoom.Value;
    public static RoundedPolygon SoftBoom => LazySoftBoom.Value;
    public static RoundedPolygon Flower => LazyFlower.Value;
    public static RoundedPolygon Puffy => LazyPuffy.Value;
    public static RoundedPolygon PuffyDiamond => LazyPuffyDiamond.Value;
    public static RoundedPolygon PixelCircle => LazyPixelCircle.Value;
    public static RoundedPolygon PixelTriangle => LazyPixelTriangle.Value;
    public static RoundedPolygon Bun => LazyBun.Value;
    public static RoundedPolygon Heart => LazyHeart.Value;

    private static Lazy<RoundedPolygon> Create(Func<RoundedPolygon> factory) =>
        new(() => factory().Normalized(), LazyThreadSafetyMode.ExecutionAndPublication);

    private static RoundedPolygon CircleCore() => RoundedShapes.Circle(10);

    private static RoundedPolygon SquareCore() =>
        RoundedShapes.Rectangle(1, 1, Round30);

    private static RoundedPolygon SlantedCore() => CustomPolygon(
    [
        new PointNRound(new ShapePoint(0.926, 0.970), new CornerRounding(0.189, 0.811)),
        new PointNRound(new ShapePoint(-0.021, 0.967), new CornerRounding(0.187, 0.057))
    ], 2);

    private static RoundedPolygon ArchCore() => RoundedPolygon.FromNumVertices(4,
        perVertexRounding: [Round100, Round100, Round20, Round20]).Transformed(Rotate(-135));

    private static RoundedPolygon FanCore() => CustomPolygon(
    [
        new PointNRound(new ShapePoint(1.004, 1.000), new CornerRounding(0.148, 0.417)),
        new PointNRound(new ShapePoint(0.000, 1.000), new CornerRounding(0.151)),
        new PointNRound(new ShapePoint(0.000, -0.003), new CornerRounding(0.148)),
        new PointNRound(new ShapePoint(0.978, 0.020), new CornerRounding(0.803))
    ], 1);

    private static RoundedPolygon ArrowCore() => CustomPolygon(
    [
        new PointNRound(new ShapePoint(0.500, 0.892), new CornerRounding(0.313)),
        new PointNRound(new ShapePoint(-0.216, 1.050), new CornerRounding(0.207)),
        new PointNRound(new ShapePoint(0.499, -0.160), new CornerRounding(0.215, 1.000)),
        new PointNRound(new ShapePoint(1.225, 1.060), new CornerRounding(0.211))
    ], 1);

    private static RoundedPolygon SemiCircleCore() => RoundedShapes.Rectangle(1.6, 1,
        perVertexRounding: [Round20, Round20, Round100, Round100]);

    private static RoundedPolygon OvalCore() => RoundedShapes.Circle()
        .Transformed(Scale(1, 0.64))
        .Transformed(Rotate(-45));

    private static RoundedPolygon PillCore() => CustomPolygon(
    [
        new PointNRound(new ShapePoint(0.961, 0.039), new CornerRounding(0.426)),
        new PointNRound(new ShapePoint(1.001, 0.428)),
        new PointNRound(new ShapePoint(1.000, 0.609), new CornerRounding(1.000))
    ], 2, mirroring: true);

    private static RoundedPolygon TriangleCore() => RoundedPolygon.FromNumVertices(3,
        rounding: Round20).Transformed(Rotate(-90));

    private static RoundedPolygon DiamondCore() => CustomPolygon(
    [
        new PointNRound(new ShapePoint(0.500, 1.096), new CornerRounding(0.151, 0.524)),
        new PointNRound(new ShapePoint(0.040, 0.500), new CornerRounding(0.159))
    ], 2);

    private static RoundedPolygon ClamShellCore() => CustomPolygon(
    [
        new PointNRound(new ShapePoint(0.171, 0.841), new CornerRounding(0.159)),
        new PointNRound(new ShapePoint(-0.020, 0.500), new CornerRounding(0.140)),
        new PointNRound(new ShapePoint(0.170, 0.159), new CornerRounding(0.159))
    ], 2);

    private static RoundedPolygon PentagonCore() => CustomPolygon(
    [
        new PointNRound(new ShapePoint(0.500, -0.009), new CornerRounding(0.172)),
        new PointNRound(new ShapePoint(1.030, 0.365), new CornerRounding(0.164)),
        new PointNRound(new ShapePoint(0.828, 0.970), new CornerRounding(0.169))
    ], 1, mirroring: true);

    private static RoundedPolygon GemCore() => CustomPolygon(
    [
        new PointNRound(new ShapePoint(0.499, 1.023), new CornerRounding(0.241, 0.778)),
        new PointNRound(new ShapePoint(-0.005, 0.792), new CornerRounding(0.208)),
        new PointNRound(new ShapePoint(0.073, 0.258), new CornerRounding(0.228)),
        new PointNRound(new ShapePoint(0.433, -0.000), new CornerRounding(0.491))
    ], 1, mirroring: true);

    private static RoundedPolygon SunnyCore() =>
        RoundedShapes.Star(8, innerRadius: 0.8, rounding: Round15);

    private static RoundedPolygon VerySunnyCore() => CustomPolygon(
    [
        new PointNRound(new ShapePoint(0.500, 1.080), new CornerRounding(0.085)),
        new PointNRound(new ShapePoint(0.358, 0.843), new CornerRounding(0.085))
    ], 8);

    private static RoundedPolygon Cookie4Core() => CustomPolygon(
    [
        new PointNRound(new ShapePoint(1.237, 1.236), new CornerRounding(0.258)),
        new PointNRound(new ShapePoint(0.500, 0.918), new CornerRounding(0.233))
    ], 4);

    private static RoundedPolygon Cookie6Core() => CustomPolygon(
    [
        new PointNRound(new ShapePoint(0.723, 0.884), new CornerRounding(0.394)),
        new PointNRound(new ShapePoint(0.500, 1.099), new CornerRounding(0.398))
    ], 6);

    private static RoundedPolygon Cookie7Core() =>
        RoundedShapes.Star(7, innerRadius: 0.75, rounding: Round50).Transformed(Rotate(-90));

    private static RoundedPolygon Cookie9Core() =>
        RoundedShapes.Star(9, innerRadius: 0.8, rounding: Round50).Transformed(Rotate(-90));

    private static RoundedPolygon Cookie12Core() =>
        RoundedShapes.Star(12, innerRadius: 0.8, rounding: Round50).Transformed(Rotate(-90));

    private static RoundedPolygon GhostishCore() => CustomPolygon(
    [
        new PointNRound(new ShapePoint(0.500, 0), new CornerRounding(1.000)),
        new PointNRound(new ShapePoint(1, 0), new CornerRounding(1.000)),
        new PointNRound(new ShapePoint(1, 1.140), new CornerRounding(0.254, 0.106)),
        new PointNRound(new ShapePoint(0.575, 0.906), new CornerRounding(0.253))
    ], 1, mirroring: true);

    private static RoundedPolygon Clover4Core() => CustomPolygon(
    [
        new PointNRound(new ShapePoint(0.500, 0.074)),
        new PointNRound(new ShapePoint(0.725, -0.099), new CornerRounding(0.476))
    ], 4, mirroring: true);

    private static RoundedPolygon Clover8Core() => CustomPolygon(
    [
        new PointNRound(new ShapePoint(0.500, 0.036)),
        new PointNRound(new ShapePoint(0.758, -0.101), new CornerRounding(0.209))
    ], 8);

    private static RoundedPolygon BurstCore() => CustomPolygon(
    [
        new PointNRound(new ShapePoint(0.500, -0.006), new CornerRounding(0.006)),
        new PointNRound(new ShapePoint(0.592, 0.158), new CornerRounding(0.006))
    ], 12);

    private static RoundedPolygon SoftBurstCore() => CustomPolygon(
    [
        new PointNRound(new ShapePoint(0.193, 0.277), new CornerRounding(0.053)),
        new PointNRound(new ShapePoint(0.176, 0.055), new CornerRounding(0.053))
    ], 10);

    private static RoundedPolygon BoomCore() => CustomPolygon(
    [
        new PointNRound(new ShapePoint(0.457, 0.296), new CornerRounding(0.007)),
        new PointNRound(new ShapePoint(0.500, -0.051), new CornerRounding(0.007))
    ], 15);

    private static RoundedPolygon SoftBoomCore() => CustomPolygon(
    [
        new PointNRound(new ShapePoint(0.733, 0.454)),
        new PointNRound(new ShapePoint(0.839, 0.437), new CornerRounding(0.532)),
        new PointNRound(new ShapePoint(0.949, 0.449), new CornerRounding(0.439, 1.000)),
        new PointNRound(new ShapePoint(0.998, 0.478), new CornerRounding(0.174))
    ], 16, mirroring: true);

    private static RoundedPolygon FlowerCore() => CustomPolygon(
    [
        new PointNRound(new ShapePoint(0.370, 0.187)),
        new PointNRound(new ShapePoint(0.416, 0.049), new CornerRounding(0.381)),
        new PointNRound(new ShapePoint(0.479, 0.001), new CornerRounding(0.095))
    ], 8, mirroring: true);

    private static RoundedPolygon PuffyCore() => CustomPolygon(
    [
        new PointNRound(new ShapePoint(0.500, 0.053)),
        new PointNRound(new ShapePoint(0.545, -0.040), new CornerRounding(0.405)),
        new PointNRound(new ShapePoint(0.670, -0.035), new CornerRounding(0.426)),
        new PointNRound(new ShapePoint(0.717, 0.066), new CornerRounding(0.574)),
        new PointNRound(new ShapePoint(0.722, 0.128)),
        new PointNRound(new ShapePoint(0.777, 0.002), new CornerRounding(0.360)),
        new PointNRound(new ShapePoint(0.914, 0.149), new CornerRounding(0.660)),
        new PointNRound(new ShapePoint(0.926, 0.289), new CornerRounding(0.660)),
        new PointNRound(new ShapePoint(0.881, 0.346)),
        new PointNRound(new ShapePoint(0.940, 0.344), new CornerRounding(0.126)),
        new PointNRound(new ShapePoint(1.003, 0.437), new CornerRounding(0.255))
    ], 2, mirroring: true).Transformed(Scale(1, 0.742));

    private static RoundedPolygon PuffyDiamondCore() => CustomPolygon(
    [
        new PointNRound(new ShapePoint(0.870, 0.130), new CornerRounding(0.146)),
        new PointNRound(new ShapePoint(0.818, 0.357)),
        new PointNRound(new ShapePoint(1.000, 0.332), new CornerRounding(0.853))
    ], 4, mirroring: true);

    private static RoundedPolygon PixelCircleCore() => CustomPolygon(
    [
        new PointNRound(new ShapePoint(0.500, 0.000)),
        new PointNRound(new ShapePoint(0.704, 0.000)),
        new PointNRound(new ShapePoint(0.704, 0.065)),
        new PointNRound(new ShapePoint(0.843, 0.065)),
        new PointNRound(new ShapePoint(0.843, 0.148)),
        new PointNRound(new ShapePoint(0.926, 0.148)),
        new PointNRound(new ShapePoint(0.926, 0.296)),
        new PointNRound(new ShapePoint(1.000, 0.296))
    ], 2, mirroring: true);

    private static RoundedPolygon PixelTriangleCore() => CustomPolygon(
    [
        new PointNRound(new ShapePoint(0.110, 0.500)),
        new PointNRound(new ShapePoint(0.113, 0.000)),
        new PointNRound(new ShapePoint(0.287, 0.000)),
        new PointNRound(new ShapePoint(0.287, 0.087)),
        new PointNRound(new ShapePoint(0.421, 0.087)),
        new PointNRound(new ShapePoint(0.421, 0.170)),
        new PointNRound(new ShapePoint(0.560, 0.170)),
        new PointNRound(new ShapePoint(0.560, 0.265)),
        new PointNRound(new ShapePoint(0.674, 0.265)),
        new PointNRound(new ShapePoint(0.675, 0.344)),
        new PointNRound(new ShapePoint(0.789, 0.344)),
        new PointNRound(new ShapePoint(0.789, 0.439)),
        new PointNRound(new ShapePoint(0.888, 0.439))
    ], 1, mirroring: true);

    private static RoundedPolygon BunCore() => CustomPolygon(
    [
        new PointNRound(new ShapePoint(0.796, 0.500)),
        new PointNRound(new ShapePoint(0.853, 0.518), new CornerRounding(1)),
        new PointNRound(new ShapePoint(0.992, 0.631), new CornerRounding(1)),
        new PointNRound(new ShapePoint(0.968, 1.000), new CornerRounding(1))
    ], 2, mirroring: true);

    private static RoundedPolygon HeartCore() => CustomPolygon(
    [
        new PointNRound(new ShapePoint(0.500, 0.268), new CornerRounding(0.016)),
        new PointNRound(new ShapePoint(0.792, -0.066), new CornerRounding(0.958)),
        new PointNRound(new ShapePoint(1.064, 0.276), new CornerRounding(1.000)),
        new PointNRound(new ShapePoint(0.501, 0.946), new CornerRounding(0.129))
    ], 1, mirroring: true);

    private static List<PointNRound> DoRepeat(IReadOnlyList<PointNRound> points, int repetitions,
        ShapePoint center, bool mirroring)
    {
        var result = new List<PointNRound>();
        if (mirroring)
        {
            var angles = new double[points.Count];
            var distances = new double[points.Count];
            for (var index = 0; index < points.Count; index++)
            {
                var offset = points[index].Offset - center;
                angles[index] = Math.Atan2(offset.Y, offset.X) * 180 / Math.PI;
                distances[index] = offset.Distance;
            }

            var actualRepetitions = repetitions * 2;
            var sectionAngle = 360d / actualRepetitions;
            for (var repetition = 0; repetition < actualRepetitions; repetition++)
            {
                for (var index = 0; index < points.Count; index++)
                {
                    var i = repetition % 2 == 0 ? index : points.Count - 1 - index;
                    if (i > 0 || repetition % 2 == 0)
                    {
                        var angle =
                            (sectionAngle * repetition +
                             (repetition % 2 == 0
                                 ? angles[i]
                                 : sectionAngle - angles[i] + 2 * angles[0])) *
                            Math.PI / 180;
                        var finalPoint = new ShapePoint(Math.Cos(angle), Math.Sin(angle)) *
                                         distances[i] + center;
                        result.Add(new PointNRound(finalPoint, points[i].Rounding));
                    }
                }
            }
        }
        else
        {
            for (var index = 0; index < points.Count * repetitions; index++)
            {
                var point = RotateDegrees(points[index % points.Count].Offset,
                    index / points.Count * 360d / repetitions, center);
                result.Add(new PointNRound(point, points[index % points.Count].Rounding));
            }
        }

        return result;
    }

    private static RoundedPolygon CustomPolygon(IReadOnlyList<PointNRound> points, int repetitions,
        ShapePoint? center = null, bool mirroring = false)
    {
        var actualCenter = center ?? new ShapePoint(0.5, 0.5);
        var actualPoints = DoRepeat(points, repetitions, actualCenter, mirroring);
        var vertices = new double[actualPoints.Count * 2];
        var rounding = new CornerRounding[actualPoints.Count];
        for (var index = 0; index < actualPoints.Count; index++)
        {
            vertices[index * 2] = actualPoints[index].Offset.X;
            vertices[index * 2 + 1] = actualPoints[index].Offset.Y;
            rounding[index] = actualPoints[index].Rounding;
        }

        return RoundedPolygon.FromVertices(vertices, perVertexRounding: rounding,
            centerX: actualCenter.X, centerY: actualCenter.Y);
    }

    private static ShapePoint RotateDegrees(ShapePoint point, double degrees, ShapePoint center)
    {
        var angle = degrees * Math.PI / 180;
        var offset = point - center;
        return new ShapePoint(offset.X * Math.Cos(angle) - offset.Y * Math.Sin(angle),
            offset.X * Math.Sin(angle) + offset.Y * Math.Cos(angle)) + center;
    }

    private static Func<ShapePoint, ShapePoint> Rotate(double degrees)
    {
        var angle = degrees * Math.PI / 180;
        return point => new ShapePoint(point.X * Math.Cos(angle) - point.Y * Math.Sin(angle),
            point.X * Math.Sin(angle) + point.Y * Math.Cos(angle));
    }

    private static Func<ShapePoint, ShapePoint> Scale(double x, double y) =>
        point => new ShapePoint(point.X * x, point.Y * y);

    private readonly record struct PointNRound(ShapePoint Offset, CornerRounding Rounding = default);
}
