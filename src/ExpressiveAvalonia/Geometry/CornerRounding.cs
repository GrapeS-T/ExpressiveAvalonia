namespace ExpressiveAvalonia.Shapes;

/// <summary>
/// The amount and quality of the rounding around one polygon vertex.
/// </summary>
/// <param name="Radius">
/// Radius of the circle that forms the rounding. Zero leaves the corner sharp. The radius is in
/// the polygon's coordinate space, so it scales with the polygon.
/// </param>
/// <param name="Smoothing">
/// How far the two flanking curves extend from the inner circular arc towards the edges. Zero
/// keeps a pure circular arc; one removes the arc and lets the flanking curves meet.
/// </param>
public readonly record struct CornerRounding(double Radius = 0, double Smoothing = 0)
{
    /// <summary>Zero radius: a sharp corner at the vertex.</summary>
    public static CornerRounding Unrounded => new();
}
