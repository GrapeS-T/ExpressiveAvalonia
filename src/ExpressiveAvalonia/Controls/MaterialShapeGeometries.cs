using System.Collections.Concurrent;
using Avalonia.Media;
using ExpressiveAvalonia.Shapes;

namespace ExpressiveAvalonia.Controls;

/// <summary>Identifies one geometry of the Material Expressive shape family.</summary>
/// <remarks>
/// The named members mirror the 35 shapes in androidx <c>MaterialShapes</c>. The local design
/// references in the comments keep the earlier asset mapping, and <see cref="Hexagon"/> plus
/// <see cref="Shape1"/>–<see cref="Shape9"/> are project extensions that are not upstream.
/// </remarks>
public enum MaterialShapeKind
{
    // MaterialShapes catalog (normalized RoundedPolygon values)
    /// <summary>Circle (local-004608).</summary>
    Circle,
    /// <summary>Square (local-004627).</summary>
    Square,
    /// <summary>Slanted (local-004624).</summary>
    Slanted,
    /// <summary>Arch (local-004603).</summary>
    Arch,
    /// <summary>Fan (local-004610).</summary>
    Fan,
    /// <summary>Arrow (local-004604).</summary>
    Arrow,
    /// <summary>Semicircle (local-004623).</summary>
    Semicircle,
    /// <summary>Oval (local-004616).</summary>
    Oval,
    /// <summary>Pill (local-004618).</summary>
    Pill,
    /// <summary>Triangle (local-004629).</summary>
    Triangle,
    /// <summary>Diamond (local-004609).</summary>
    Diamond,
    /// <summary>Hexagon (local-004615). Project extension: not in the upstream catalog.</summary>
    Hexagon,
    /// <summary>Pentagon (local-004617).</summary>
    Pentagon,
    /// <summary>Gem (local-004612).</summary>
    Gem,
    /// <summary>Very sunny (local-004630).</summary>
    VerySunny,
    /// <summary>Sunny (local-004628).</summary>
    Sunny,
    /// <summary>4-sided cookie (local-004598).</summary>
    FourSidedCookie,
    /// <summary>6-sided cookie (local-004599).</summary>
    SixSidedCookie,
    /// <summary>7-sided cookie (local-004600).</summary>
    SevenSidedCookie,
    /// <summary>9-sided cookie (local-004602).</summary>
    NineSidedCookie,
    /// <summary>12-sided cookie (local-004596).</summary>
    TwelveSidedCookie,
    /// <summary>Ghost-ish (local-004613).</summary>
    Ghostish,
    /// <summary>4-leaf clover (local-004597).</summary>
    FourLeafClover,
    /// <summary>8-leaf clover (local-004601).</summary>
    EightLeafClover,
    /// <summary>Burst (local-004607).</summary>
    Burst,
    /// <summary>Soft burst (local-004626).</summary>
    SoftBurst,
    /// <summary>Boom (local-004605).</summary>
    Boom,
    /// <summary>Soft boom (local-004625).</summary>
    SoftBoom,
    /// <summary>Flower (local-004611).</summary>
    Flower,
    /// <summary>Puffy (local-004621).</summary>
    Puffy,
    /// <summary>Puffy diamond (local-004622).</summary>
    PuffyDiamond,
    /// <summary>Pixel circle (local-004619).</summary>
    PixelCircle,
    /// <summary>Pixel triangle (local-004620).</summary>
    PixelTriangle,
    /// <summary>Bun (local-004606).</summary>
    Bun,
    /// <summary>Heart (local-004614).</summary>
    Heart,
    /// <summary>Clam-shell. Upstream <c>MaterialShapes.ClamShell</c>; absent from the earlier set.</summary>
    ClamShell,

    // .Shape (local-004585), project extensions
    /// <summary>.Shape 1 (local-004586).</summary>
    Shape1,
    /// <summary>.Shape 2 (local-004587).</summary>
    Shape2,
    /// <summary>.Shape 3 (local-004588).</summary>
    Shape3,
    /// <summary>.Shape 4 (local-004589).</summary>
    Shape4,
    /// <summary>.Shape 5 (local-004590).</summary>
    Shape5,
    /// <summary>.Shape 6 (local-004591).</summary>
    Shape6,
    /// <summary>.Shape 7 (local-004592).</summary>
    Shape7,
    /// <summary>.Shape 8 (local-004593).</summary>
    Shape8,
    /// <summary>.Shape 9 (local-004594).</summary>
    Shape9
}

/// <summary>Path data and parsed geometries for every <see cref="MaterialShapeKind"/>.</summary>
/// <remarks>
/// The 35 catalog members are generated from <c>MaterialShapes.kt</c> through the ported
/// RoundedPolygon pipeline: the same polygon definitions, normalization and cubic path conversion
/// as the source. <see cref="Hexagon"/> and the nine numbered <c>.Shape</c> vectors are project
/// extensions and keep their verbatim path data. Parsed geometries are cached and shared; callers
/// must not mutate them.
/// </remarks>
public static class MaterialShapeGeometries
{
    private static readonly ConcurrentDictionary<MaterialShapeKind, Geometry> Parsed = new();
    private static readonly ConcurrentDictionary<(MaterialShapeKind Kind, int StartAngle), Geometry>
        Angled = new();
    private static readonly ConcurrentDictionary<MaterialShapeKind, string> Data = new();

    /// <summary>Returns the cached, parsed geometry for <paramref name="kind"/>.</summary>
    public static Geometry Get(MaterialShapeKind kind) => Parsed.GetOrAdd(kind, static value =>
    {
        var polygon = CatalogPolygon(value);
        return polygon != null
            ? ShapePath.ToGeometry(polygon)
            : Geometry.Parse(MaterialShapePathData.Get(value)!);
    });

    /// <summary>
    /// Returns a geometry whose path starts at <paramref name="startAngle"/> degrees, matching the
    /// source <c>toPath(startAngle)</c>. Custom extension shapes have no polygon metadata, so they
    /// ignore the angle and return their verbatim geometry.
    /// </summary>
    public static Geometry Get(MaterialShapeKind kind, int startAngle)
    {
        if (startAngle == 0 || IsCustom(kind))
            return Get(kind);

        return Angled.GetOrAdd((kind, startAngle), static key =>
            ShapePath.ToGeometry(CatalogPolygon(key.Kind)!, key.StartAngle));
    }

    /// <summary>Returns the source or generated path data for <paramref name="kind"/>.</summary>
    public static string GetPathData(MaterialShapeKind kind) => Data.GetOrAdd(kind, static value =>
    {
        var polygon = CatalogPolygon(value);
        return polygon != null
            ? ShapePath.ToPathData(polygon)
            : MaterialShapePathData.Get(value)!;
    });

    /// <summary>True for the nine numbered <c>.Shape</c> geometries (local-004585).</summary>
    public static bool IsExpressive(MaterialShapeKind kind) =>
        (int)kind >= (int)MaterialShapeKind.Shape1;

    /// <summary>True for the source catalog members; false for project extension shapes.</summary>
    public static bool IsCatalogShape(MaterialShapeKind kind) => !IsCustom(kind);

    /// <summary>True for shapes that are not part of the upstream MaterialShapes catalog.</summary>
    public static bool IsCustom(MaterialShapeKind kind) =>
        kind is MaterialShapeKind.Hexagon || IsExpressive(kind);

    /// <summary>Returns the normalized catalog polygon, or null for a custom shape.</summary>
    internal static RoundedPolygon? CatalogPolygon(MaterialShapeKind kind) => kind switch
    {
        MaterialShapeKind.Circle => MaterialShapeCatalog.Circle,
        MaterialShapeKind.Square => MaterialShapeCatalog.Square,
        MaterialShapeKind.Slanted => MaterialShapeCatalog.Slanted,
        MaterialShapeKind.Arch => MaterialShapeCatalog.Arch,
        MaterialShapeKind.Fan => MaterialShapeCatalog.Fan,
        MaterialShapeKind.Arrow => MaterialShapeCatalog.Arrow,
        MaterialShapeKind.Semicircle => MaterialShapeCatalog.SemiCircle,
        MaterialShapeKind.Oval => MaterialShapeCatalog.Oval,
        MaterialShapeKind.Pill => MaterialShapeCatalog.Pill,
        MaterialShapeKind.Triangle => MaterialShapeCatalog.Triangle,
        MaterialShapeKind.Diamond => MaterialShapeCatalog.Diamond,
        MaterialShapeKind.ClamShell => MaterialShapeCatalog.ClamShell,
        MaterialShapeKind.Pentagon => MaterialShapeCatalog.Pentagon,
        MaterialShapeKind.Gem => MaterialShapeCatalog.Gem,
        MaterialShapeKind.VerySunny => MaterialShapeCatalog.VerySunny,
        MaterialShapeKind.Sunny => MaterialShapeCatalog.Sunny,
        MaterialShapeKind.FourSidedCookie => MaterialShapeCatalog.Cookie4Sided,
        MaterialShapeKind.SixSidedCookie => MaterialShapeCatalog.Cookie6Sided,
        MaterialShapeKind.SevenSidedCookie => MaterialShapeCatalog.Cookie7Sided,
        MaterialShapeKind.NineSidedCookie => MaterialShapeCatalog.Cookie9Sided,
        MaterialShapeKind.TwelveSidedCookie => MaterialShapeCatalog.Cookie12Sided,
        MaterialShapeKind.Ghostish => MaterialShapeCatalog.Ghostish,
        MaterialShapeKind.FourLeafClover => MaterialShapeCatalog.Clover4Leaf,
        MaterialShapeKind.EightLeafClover => MaterialShapeCatalog.Clover8Leaf,
        MaterialShapeKind.Burst => MaterialShapeCatalog.Burst,
        MaterialShapeKind.SoftBurst => MaterialShapeCatalog.SoftBurst,
        MaterialShapeKind.Boom => MaterialShapeCatalog.Boom,
        MaterialShapeKind.SoftBoom => MaterialShapeCatalog.SoftBoom,
        MaterialShapeKind.Flower => MaterialShapeCatalog.Flower,
        MaterialShapeKind.Puffy => MaterialShapeCatalog.Puffy,
        MaterialShapeKind.PuffyDiamond => MaterialShapeCatalog.PuffyDiamond,
        MaterialShapeKind.PixelCircle => MaterialShapeCatalog.PixelCircle,
        MaterialShapeKind.PixelTriangle => MaterialShapeCatalog.PixelTriangle,
        MaterialShapeKind.Bun => MaterialShapeCatalog.Bun,
        MaterialShapeKind.Heart => MaterialShapeCatalog.Heart,
        _ => null
    };
}
