namespace ExpressiveAvalonia.Controls;

/// <summary>
/// Emphasized surface role used by the XR component family.
/// </summary>
/// <remarks>
/// The values map to the Material 3 surface roles; buttons accept
/// <see cref="SurfaceContainer"/>, <see cref="SurfaceContainerHigh"/>, and
/// <see cref="TertiaryContainer"/>, while bars and toolbars additionally accept
/// <see cref="SurfaceContainerHighest"/>.
/// </remarks>
public enum XrSurfaceRole
{
    /// <summary>The base surface-container tone.</summary>
    SurfaceContainer,

    /// <summary>A raised surface-container tone.</summary>
    SurfaceContainerHigh,

    /// <summary>The highest surface-container tone.</summary>
    SurfaceContainerHighest,

    /// <summary>The tertiary-container tone, used by the tertiary toolbar.</summary>
    TertiaryContainer
}

/// <summary>The optional count indicator of an XR navigation destination.</summary>
public enum XrBadgeKind
{
    /// <summary>No badge.</summary>
    None,

    /// <summary>A six unit dot over the icon.</summary>
    Small,

    /// <summary>A sixteen unit pill carrying <c>BadgeText</c>.</summary>
    Large
}

/// <summary>App bar configuration from the XR app bar component.</summary>
public enum XrAppBarConfiguration
{
    /// <summary>Headline centered between the leading and trailing slots.</summary>
    CenterAligned,

    /// <summary>Headline left-aligned after the leading slot, with room for three trailing icons.</summary>
    Small
}

/// <summary>The pinned XR navigation bar family composed by <see cref="XrNavigationBar"/>.</summary>
/// <remarks>
/// <c>SpatialNavigationBar</c> and <c>SpatialShortNavigationBar</c> are separate composables in
/// the pinned source that share a container height. The standard bar has 8 dp horizontal padding
/// and no inter-item gap; the short bar has 32 dp around its content and between destinations.
/// </remarks>
public enum XrNavigationBarVariant
{
    /// <summary><c>SpatialNavigationBar</c> geometry: 8 dp horizontal padding, no item gap.</summary>
    Standard,

    /// <summary><c>SpatialShortNavigationBar</c> geometry: 32 dp horizontal padding and item gap.</summary>
    Short
}

/// <summary>The XR navigation rail families composed by <see cref="XrNavigationRail"/>.</summary>
/// <remarks>
/// <c>SpatialNavigationRail</c> is the 96 dp rail with menu and FAB chrome;
/// <c>SpatialWideNavigationRail</c> is the 96 dp collapsed / 220–360 dp expanded rail with a
/// caller-supplied header slot.
/// </remarks>
public enum XrNavigationRailVariant
{
    /// <summary><c>SpatialNavigationRail</c>: 96 wide, 20 vertical padding, 20 destination gap.</summary>
    Standard,

    /// <summary>
    /// <c>SpatialWideNavigationRail</c>: 96 wide collapsed, 220–360 wide expanded, same
    /// circular surface, vertical padding and destination gap, plus a header slot.
    /// </summary>
    Wide
}

/// <summary>How an <see cref="XrDialog"/> was closed.</summary>
public enum XrDialogResult
{
    /// <summary>Closed without an action, for example with Escape or the window close gesture.</summary>
    None,

    /// <summary>The primary action was invoked.</summary>
    Primary,

    /// <summary>The secondary action was invoked.</summary>
    Secondary,

    /// <summary>Dismissed without invoking an action.</summary>
    Dismissed
}
