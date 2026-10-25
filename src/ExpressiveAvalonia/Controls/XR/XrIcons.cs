using Avalonia.Media;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// Material Symbols outline paths used by the XR defaults and by the family gallery.
/// </summary>
/// <remarks>
/// Paths are authored on a 24 by 24 view box and are filled with the control foreground,
/// so they follow the selected, unselected, and disabled roles instead of freezing a color.
/// </remarks>
public static class XrIcons
{
    /// <summary>The three-line menu glyph used by the navigation rail.</summary>
    public static Geometry Menu { get; } =
        Geometry.Parse("M3,6H21V8H3V6M3,11H21V13H3V11M3,16H21V18H3V16Z");

    /// <summary>A plus glyph, the default toolbar and rail FAB icon.</summary>
    public static Geometry Add { get; } =
        Geometry.Parse("M19,13H13V19H11V13H5V11H11V5H13V11H19V13Z");

    /// <summary>A filled star.</summary>
    public static Geometry Star { get; } =
        Geometry.Parse("M12,17.27L18.18,21L16.54,13.97L22,9.24L14.81,8.62L12,2L9.19,8.62L2,9.24L7.45,13.97L5.82,21L12,17.27Z");

    /// <summary>An outlined star; pairs with <see cref="Star"/> as the selected glyph.</summary>
    public static Geometry StarOutline { get; } =
        Geometry.Parse("M22,9.24L14.81,8.62L12,2L9.19,8.62L2,9.24L7.45,13.97L5.82,21L12,17.27L18.18,21L16.54,13.97L22,9.24M12,15.4L8.24,17.67L9.24,13.39L5.92,10.51L10.3,10.13L12,6.1L13.71,10.14L18.09,10.52L14.77,13.4L15.77,17.68L12,15.4Z");

    /// <summary>A filled heart.</summary>
    public static Geometry Favorite { get; } =
        Geometry.Parse("M12,21.35L10.55,20.03C5.4,15.36 2,12.27 2,8.5C2,5.41 4.42,3 7.5,3C9.24,3 10.91,3.81 12,5.08C13.09,3.81 14.76,3 16.5,3C19.58,3 22,5.41 22,8.5C22,12.27 18.6,15.36 13.45,20.03L12,21.35Z");

    /// <summary>An outlined heart; pairs with <see cref="Favorite"/>.</summary>
    public static Geometry FavoriteOutline { get; } =
        Geometry.Parse("M16.5,3C14.76,3 13.09,3.81 12,5.08C10.91,3.81 9.24,3 7.5,3C4.42,3 2,5.41 2,8.5C2,12.27 5.4,15.36 10.55,20.03L12,21.35L13.45,20.03C18.6,15.36 22,12.27 22,8.5C22,5.41 19.58,3 16.5,3M12,18.09L11.55,17.68C7.1,13.64 4,10.83 4,8.5C4,6.63 5.63,5 7.5,5C8.96,5 10.36,5.9 11.03,7.14H12.97C13.64,5.9 15.04,5 16.5,5C18.37,5 20,6.63 20,8.5C20,10.83 16.9,13.64 12.45,17.68L12,18.09Z");

    /// <summary>A magnifier.</summary>
    public static Geometry Search { get; } =
        Geometry.Parse("M9.5,3A6.5,6.5 0 0,1 16,9.5C16,11.11 15.41,12.59 14.44,13.73L14.71,14H15.5L20.5,19L19,20.5L14,15.5V14.71L13.73,14.44C12.59,15.41 11.11,16 9.5,16A6.5,6.5 0 0,1 3,9.5A6.5,6.5 0 0,1 9.5,3M9.5,5C7,5 5,7 5,9.5C5,12 7,14 9.5,14C12,14 14,12 14,9.5C14,7 12,5 9.5,5Z");

    /// <summary>A house.</summary>
    public static Geometry Home { get; } =
        Geometry.Parse("M10,20V14H14V20H19V12H22L12,3L2,12H5V20H10Z");

    /// <summary>A compass; the "Explore" destination.</summary>
    public static Geometry Explore { get; } =
        Geometry.Parse("M12,10.9C11.39,10.9 10.9,11.39 10.9,12C10.9,12.61 11.39,13.1 12,13.1C12.61,13.1 13.1,12.61 13.1,12C13.1,11.39 12.61,10.9 12,10.9M12,2A10,10 0 0,1 22,12A10,10 0 0,1 12,22A10,10 0 0,1 2,12A10,10 0 0,1 12,2M16.19,7.81L14.43,14.43L7.81,16.19L9.57,9.57L16.19,7.81Z");

    /// <summary>A bookmark; the "Saved" destination.</summary>
    public static Geometry Bookmark { get; } =
        Geometry.Parse("M17,3H7A2,2 0 0,0 5,5V21L12,18L19,21V5C19,3.89 18.1,3 17,3Z");

    /// <summary>A clock with a circular arrow; the "Updates" destination.</summary>
    public static Geometry History { get; } =
        Geometry.Parse("M13,3C8.03,3 4,7.03 4,12H1L4.89,15.89L4.96,16.03L9,12H6C6,8.13 9.13,5 13,5C16.87,5 20,8.13 20,12C20,15.87 16.87,19 13,19C11.07,19 9.32,18.21 8.06,16.94L6.64,18.36C8.27,19.99 10.51,21 13,21C17.97,21 22,16.97 22,12C22,7.03 17.97,3 13,3M12,8V13L16.28,15.54L17,14.33L13.5,12.25V8H12Z");

    /// <summary>A speech bubble.</summary>
    public static Geometry Chat { get; } =
        Geometry.Parse("M20,2H4A2,2 0 0,0 2,4V22L6,18H20A2,2 0 0,0 22,16V4A2,2 0 0,0 20,2Z");

    /// <summary>An envelope.</summary>
    public static Geometry Mail { get; } =
        Geometry.Parse("M20,8L12,13L4,8V6L12,11L20,6M20,4H4C2.89,4 2,4.89 2,6V18A2,2 0 0,0 4,20H20A2,2 0 0,0 22,18V6C22,4.89 21.1,4 20,4Z");

    /// <summary>A close cross.</summary>
    public static Geometry Close { get; } =
        Geometry.Parse("M19,6.41L17.59,5L12,10.59L6.41,5L5,6.41L10.59,12L5,17.59L6.41,19L12,13.41L17.59,19L19,17.59L13.41,12L19,6.41Z");

    /// <summary>A check mark.</summary>
    public static Geometry Check { get; } =
        Geometry.Parse("M21,7L9,19L3.5,13.5L4.91,12.09L9,16.17L19.59,5.59L21,7Z");
}
