using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace ExpressiveAvalonia.Tokens;

/// <summary>Baseline Material Design 3 non-color resources.</summary>
public static class MaterialTokens
{
    /// <summary>
    /// Creates an independent dictionary of baseline typography, shape, state, elevation,
    /// and duration tokens. Sizes, line heights, corner radii, and elevation distances
    /// are expressed in Avalonia device-independent units; durations are <see cref="TimeSpan"/>.
    /// </summary>
    /// <remarks>
    /// Values follow Google's Material Web v0.192 system tokens:
    /// https://github.com/material-components/material-web/tree/main/tokens/versions/v0_192
    /// (md-sys-typescale, md-ref-typeface, md-sys-shape, md-sys-state,
    /// md-sys-elevation, and md-sys-motion).
    /// The Web type scale is converted from rem at its baseline of 16 pixels.
    /// MaterialFontFamily intentionally uses the platform default, not bundled Roboto;
    /// applications can override it with an installed or application-provided font.
    /// Elevation tokens are distances, not shadow definitions. Duration tokens are
    /// baseline MD3 timing values, not Expressive spring or physics parameters.
    /// </remarks>
    public static ResourceDictionary CreateResources()
    {
        var resources = new ResourceDictionary
        {
            ["MaterialFontFamily"] = FontFamily.Default,
            ["MaterialShapeNoneCornerRadius"] = new CornerRadius(0),
            ["MaterialShapeExtraSmallCornerRadius"] = new CornerRadius(4),
            ["MaterialShapeSmallCornerRadius"] = new CornerRadius(8),
            ["MaterialShapeMediumCornerRadius"] = new CornerRadius(12),
            ["MaterialShapeLargeCornerRadius"] = new CornerRadius(16),
            ["MaterialShapeExtraLargeCornerRadius"] = new CornerRadius(28),
            // Material's full corner uses an oversized radius, clamped by the renderer.
            ["MaterialShapeFullCornerRadius"] = new CornerRadius(9999),
            ["MaterialHoverStateLayerOpacity"] = 0.08d,
            ["MaterialFocusStateLayerOpacity"] = 0.12d,
            ["MaterialPressedStateLayerOpacity"] = 0.12d,
            ["MaterialDraggedStateLayerOpacity"] = 0.16d,
            ["MaterialElevationLevel0"] = 0d,
            ["MaterialElevationLevel1"] = 1d,
            ["MaterialElevationLevel2"] = 3d,
            ["MaterialElevationLevel3"] = 6d,
            ["MaterialElevationLevel4"] = 8d,
            ["MaterialElevationLevel5"] = 12d,
            ["MaterialDurationShort1"] = TimeSpan.FromMilliseconds(50),
            ["MaterialDurationShort2"] = TimeSpan.FromMilliseconds(100),
            ["MaterialDurationShort3"] = TimeSpan.FromMilliseconds(150),
            ["MaterialDurationShort4"] = TimeSpan.FromMilliseconds(200),
            ["MaterialDurationMedium1"] = TimeSpan.FromMilliseconds(250),
            ["MaterialDurationMedium2"] = TimeSpan.FromMilliseconds(300),
            ["MaterialDurationMedium3"] = TimeSpan.FromMilliseconds(350),
            ["MaterialDurationMedium4"] = TimeSpan.FromMilliseconds(400),
            ["MaterialDurationLong1"] = TimeSpan.FromMilliseconds(450),
            ["MaterialDurationLong2"] = TimeSpan.FromMilliseconds(500),
            ["MaterialDurationLong3"] = TimeSpan.FromMilliseconds(550),
            ["MaterialDurationLong4"] = TimeSpan.FromMilliseconds(600),
            ["MaterialDurationExtraLong1"] = TimeSpan.FromMilliseconds(700),
            ["MaterialDurationExtraLong2"] = TimeSpan.FromMilliseconds(800),
            ["MaterialDurationExtraLong3"] = TimeSpan.FromMilliseconds(900),
            ["MaterialDurationExtraLong4"] = TimeSpan.FromMilliseconds(1000),
        };

        AddTypography(resources, "DisplayLarge", 57, 64, FontWeight.Normal);
        AddTypography(resources, "DisplayMedium", 45, 52, FontWeight.Normal);
        AddTypography(resources, "DisplaySmall", 36, 44, FontWeight.Normal);
        AddTypography(resources, "HeadlineLarge", 32, 40, FontWeight.Normal);
        AddTypography(resources, "HeadlineMedium", 28, 36, FontWeight.Normal);
        AddTypography(resources, "HeadlineSmall", 24, 32, FontWeight.Normal);
        AddTypography(resources, "TitleLarge", 22, 28, FontWeight.Normal);
        AddTypography(resources, "TitleMedium", 16, 24, FontWeight.Medium);
        AddTypography(resources, "TitleSmall", 14, 20, FontWeight.Medium);
        AddTypography(resources, "BodyLarge", 16, 24, FontWeight.Normal);
        AddTypography(resources, "BodyMedium", 14, 20, FontWeight.Normal);
        AddTypography(resources, "BodySmall", 12, 16, FontWeight.Normal);
        AddTypography(resources, "LabelLarge", 14, 20, FontWeight.Medium);
        AddTypography(resources, "LabelMedium", 12, 16, FontWeight.Medium);
        AddTypography(resources, "LabelSmall", 11, 16, FontWeight.Medium);

        return resources;
    }

    private static void AddTypography(
        ResourceDictionary resources,
        string role,
        double fontSize,
        double lineHeight,
        FontWeight fontWeight)
    {
        resources[$"Material{role}FontSize"] = fontSize;
        resources[$"Material{role}LineHeight"] = lineHeight;
        resources[$"Material{role}FontWeight"] = fontWeight;
    }
}
