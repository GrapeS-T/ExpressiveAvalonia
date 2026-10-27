using System.Globalization;
using System.Text;
using Avalonia.Media;

namespace ExpressiveAvalonia.Shapes;

/// <summary>
/// Converts polygons and morphs to Avalonia paths, preserving the source start-angle, repeat and
/// close semantics.
/// </summary>
internal static class ShapePath
{
    public static StreamGeometry ToGeometry(RoundedPolygon polygon, int startAngle = 0,
        bool repeatPath = false, bool closePath = true)
    {
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        Append(context, polygon.Cubics, startAngle, repeatPath, closePath, polygon.CenterX,
            polygon.CenterY);
        return geometry;
    }

    public static StreamGeometry ToGeometry(Morph morph, double progress, int startAngle = 0,
        bool repeatPath = false, bool closePath = true, double rotationPivotX = 0,
        double rotationPivotY = 0)
    {
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        Append(context, morph.AsCubics(progress), startAngle, repeatPath, closePath,
            rotationPivotX, rotationPivotY);
        return geometry;
    }

    /// <summary>
    /// Writes cubics to a path context. The optional start angle rotates the whole path so drawing
    /// begins at that angle; the rotation is around the origin, as in the source, and the pivot is
    /// used only to measure the angle of the first anchor.
    /// </summary>
    public static void Append(StreamGeometryContext context, IReadOnlyList<Cubic> cubics,
        int startAngle = 0, bool repeatPath = false, bool closePath = true,
        double rotationPivotX = 0, double rotationPivotY = 0)
    {
        if (cubics.Count == 0)
        {
            context.BeginFigure(new Avalonia.Point(0, 0), true);
            context.EndFigure(closePath);
            return;
        }

        context.SetFillRule(FillRule.NonZero);
        var radians = 0d;
        if (startAngle != 0)
        {
            var angleToFirstCubic = Math.Atan2(
                cubics[0].Anchor0.Y - rotationPivotY,
                cubics[0].Anchor0.X - rotationPivotX) * 180 / Math.PI;
            radians = (-angleToFirstCubic + startAngle) * Math.PI / 180;
        }

        Avalonia.Point Transform(ShapePoint point)
        {
            if (radians == 0)
                return new Avalonia.Point(point.X, point.Y);
            var cosmic = Math.Cos(radians);
            var sine = Math.Sin(radians);
            return new Avalonia.Point(point.X * cosmic - point.Y * sine,
                point.X * sine + point.Y * cosmic);
        }

        var first = true;
        foreach (var cubic in cubics)
        {
            if (first)
            {
                context.BeginFigure(Transform(cubic.Anchor0), true);
                first = false;
            }
            context.CubicBezierTo(Transform(cubic.Control0), Transform(cubic.Control1),
                Transform(cubic.Anchor1));
        }

        if (repeatPath)
        {
            context.LineTo(Transform(cubics[0].Anchor0));
            foreach (var cubic in cubics)
            {
                context.CubicBezierTo(Transform(cubic.Control0), Transform(cubic.Control1),
                    Transform(cubic.Anchor1));
            }
        }

        context.EndFigure(closePath);
    }

    /// <summary>Serialises a polygon to SVG path data in the same order the renderer uses.</summary>
    public static string ToPathData(RoundedPolygon polygon, int startAngle = 0,
        bool repeatPath = false, bool closePath = true)
    {
        var builder = new StringBuilder();
        AppendPathData(builder, polygon.Cubics, startAngle, repeatPath, closePath,
            polygon.CenterX, polygon.CenterY);
        return builder.ToString();
    }

    public static string ToPathData(Morph morph, double progress, int startAngle = 0,
        bool repeatPath = false, bool closePath = true, double rotationPivotX = 0,
        double rotationPivotY = 0)
    {
        var builder = new StringBuilder();
        AppendPathData(builder, morph.AsCubics(progress), startAngle, repeatPath, closePath,
            rotationPivotX, rotationPivotY);
        return builder.ToString();
    }

    private static void AppendPathData(StringBuilder builder, IReadOnlyList<Cubic> cubics,
        int startAngle, bool repeatPath, bool closePath, double rotationPivotX,
        double rotationPivotY)
    {
        if (cubics.Count == 0)
            return;

        var radians = 0d;
        if (startAngle != 0)
        {
            var angleToFirstCubic = Math.Atan2(
                cubics[0].Anchor0.Y - rotationPivotY,
                cubics[0].Anchor0.X - rotationPivotX) * 180 / Math.PI;
            radians = (-angleToFirstCubic + startAngle) * Math.PI / 180;
        }

        ShapePoint Transform(ShapePoint point)
        {
            if (radians == 0)
                return point;
            var cosmic = Math.Cos(radians);
            var sine = Math.Sin(radians);
            return new ShapePoint(point.X * cosmic - point.Y * sine,
                point.X * sine + point.Y * cosmic);
        }

        var first = true;
        foreach (var cubic in cubics)
        {
            if (first)
            {
                var start = Transform(cubic.Anchor0);
                AppendNumber(builder, 'M', start.X);
                builder.Append(' ');
                AppendNumber(builder, start.Y);
                first = false;
            }
            var control0 = Transform(cubic.Control0);
            var control1 = Transform(cubic.Control1);
            var end = Transform(cubic.Anchor1);
            builder.Append(" C");
            AppendNumber(builder, control0.X);
            builder.Append(' ');
            AppendNumber(builder, control0.Y);
            builder.Append(' ');
            AppendNumber(builder, control1.X);
            builder.Append(' ');
            AppendNumber(builder, control1.Y);
            builder.Append(' ');
            AppendNumber(builder, end.X);
            builder.Append(' ');
            AppendNumber(builder, end.Y);
        }

        if (repeatPath)
        {
            var start = Transform(cubics[0].Anchor0);
            builder.Append(" L");
            AppendNumber(builder, start.X);
            builder.Append(' ');
            AppendNumber(builder, start.Y);
            foreach (var cubic in cubics)
            {
                var control0 = Transform(cubic.Control0);
                var control1 = Transform(cubic.Control1);
                var end = Transform(cubic.Anchor1);
                builder.Append(" C");
                AppendNumber(builder, control0.X);
                builder.Append(' ');
                AppendNumber(builder, control0.Y);
                builder.Append(' ');
                AppendNumber(builder, control1.X);
                builder.Append(' ');
                AppendNumber(builder, control1.Y);
                builder.Append(' ');
                AppendNumber(builder, end.X);
                builder.Append(' ');
                AppendNumber(builder, end.Y);
            }
        }

        if (closePath)
            builder.Append(" Z");
    }

    private static void AppendNumber(StringBuilder builder, double value) =>
        builder.Append(value.ToString("R", CultureInfo.InvariantCulture));

    private static void AppendNumber(StringBuilder builder, char prefix, double value)
    {
        builder.Append(prefix);
        AppendNumber(builder, value);
    }
}
