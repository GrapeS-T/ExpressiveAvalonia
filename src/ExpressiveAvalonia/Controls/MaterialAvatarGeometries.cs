using System;
using Avalonia.Media;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// Vector artwork used by <see cref="MaterialAvatar"/>, kept verbatim from the source packages
/// <c>Generic avatar</c> (local-001642) and <c>.Building Blocks/Monogram</c> (local-000379).
/// </summary>
/// <remarks>
/// The source draws every part in a 40 x 40 component box. The placeholder glyph is 28.184 x
/// 25.632 at (5.908, 10); the check glyph is 17.743 x 13.526 at (11.44, 13.639). Parsed
/// geometries are shared and must not be mutated.
/// </remarks>
public static class MaterialAvatarGeometries
{
    private const string PlaceholderData =
        "M20.092 6C20.092 9.31371 17.4058 12 14.092 12C10.7783 12 8.09204 9.31371 8.09204 6" +
        "C8.09204 2.68629 10.7783 0 14.092 0C17.4058 0 20.092 2.68629 20.092 6Z" +
        "M18.092 6C18.092 8.20914 16.3012 10 14.092 10C11.8829 10 10.092 8.20914 10.092 6" +
        "C10.092 3.79086 11.8829 2 14.092 2C16.3012 2 18.092 3.79086 18.092 6Z" +
        "M14.092 15C7.61766 15 2.10132 18.8284 0 24.192C0.511895 24.7004 1.05114 25.1812 1.61533 25.6321" +
        "C3.18007 20.7077 8.08876 17 14.092 17C20.0953 17 25.004 20.7077 26.5688 25.6321" +
        "C27.133 25.1812 27.6722 24.7004 28.1841 24.1921C26.0828 18.8284 20.5664 15 14.092 15Z";

    private const string CheckData =
        "M5.63856 10.6719L1.43234 6.46569L0 7.88794L5.63856 13.5265L17.7428 1.42225L16.3206 0L5.63856 10.6719Z";

    private static readonly Lazy<Geometry> s_placeholder = new(() => Geometry.Parse(PlaceholderData));
    private static readonly Lazy<Geometry> s_check = new(() => Geometry.Parse(CheckData));

    /// <summary>The "Avatar" style person glyph: a head and shoulders, 28.184 x 25.632.</summary>
    public static Geometry Placeholder => s_placeholder.Value;

    /// <summary>The "Check" style tick, 17.743 x 13.526.</summary>
    public static Geometry Check => s_check.Value;

    /// <summary>Horizontal offset of <see cref="Placeholder"/> inside the 40 x 40 source box.</summary>
    public const double PlaceholderLeft = 5.908;

    /// <summary>Vertical offset of <see cref="Placeholder"/> inside the 40 x 40 source box.</summary>
    public const double PlaceholderTop = 10;

    /// <summary>Horizontal offset of <see cref="Check"/> inside the 40 x 40 source box.</summary>
    public const double CheckLeft = 11.4396;

    /// <summary>Vertical offset of <see cref="Check"/> inside the 40 x 40 source box.</summary>
    public const double CheckTop = 13.6385;
}
