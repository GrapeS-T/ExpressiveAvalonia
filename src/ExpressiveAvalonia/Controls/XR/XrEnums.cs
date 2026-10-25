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
