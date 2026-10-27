using System;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;

namespace ExpressiveAvalonia.Tokens;

/// <summary>Baseline Material Design 3 non-color resources.</summary>
public static class MaterialTokens
{
    /// <summary>
    /// Creates an independent dictionary of baseline typography, shape, state, elevation,
    /// motion, and localized-string tokens. Sizes, line heights, corner radii, and elevation
    /// distances are expressed in Avalonia device-independent units; durations are
    /// <see cref="TimeSpan"/>.
    /// </summary>
    /// <remarks>
    /// Values follow the pinned AndroidX Material3 ShapeTokens, StateTokens, ElevationTokens,
    /// MotionTokens, TypeScaleTokens and TypefaceTokens. dp/sp map to Avalonia DIPs at baseline
    /// text scale. MaterialFontFamily uses bundled Roboto (SIL OFL 1.1), replaceable by the app.
    /// Easing and spring resources default to the pinned Standard motion scheme; an application
    /// opts into Expressive through <see cref="Theming.MaterialTheme.MotionScheme"/>. Spring
    /// parameters are exposed as separate stiffness/damping tokens consumed by
    /// <see cref="Theming.MaterialMotion"/>. Corner radii are Avalonia CornerRadius values, while
    /// MaterialShapeCornerValue* doubles carry the upstream CornerSize values for interpolation.
    /// Elevation distances are upstream tokens; the two-layer BoxShadow resources are an Avalonia
    /// adaptation of Material Web geometry, not the Compose tonal/shadow renderer.
    /// Text strings are resources, never literals inside templates, so an application can
    /// localize them by overriding the key in its own resource dictionary.
    /// </remarks>
    public static ResourceDictionary CreateResources()
    {
        var resources = new ResourceDictionary
        {
            ["MaterialFontFamily"] = new FontFamily("avares://ExpressiveAvalonia/Assets/Fonts#Roboto"),
            ["MaterialShapeNoneCornerRadius"] = new CornerRadius(0),
            ["MaterialShapeExtraSmallCornerRadius"] = new CornerRadius(4),
            ["MaterialShapeSmallCornerRadius"] = new CornerRadius(8),
            ["MaterialShapeMediumCornerRadius"] = new CornerRadius(12),
            ["MaterialShapeLargeCornerRadius"] = new CornerRadius(16),
            ["MaterialShapeExtraLargeCornerRadius"] = new CornerRadius(28),
            ["MaterialShapeLargeIncreasedCornerRadius"] = new CornerRadius(20),
            ["MaterialShapeExtraLargeIncreasedCornerRadius"] = new CornerRadius(32),
            ["MaterialShapeExtraExtraLargeCornerRadius"] = new CornerRadius(48),
            ["MaterialShapeExtraSmallTopCornerRadius"] = new CornerRadius(4, 4, 0, 0),
            ["MaterialShapeLargeTopCornerRadius"] = new CornerRadius(16, 16, 0, 0),
            ["MaterialShapeExtraLargeTopCornerRadius"] = new CornerRadius(28, 28, 0, 0),
            // Logical start/end corners from ShapeTokens.CornerLargeStart/CornerLargeEnd. Avalonia's
            // CornerRadius is physical, so RTL consumers swap these two keys; the token values
            // themselves remain the exact upstream radii.
            ["MaterialShapeLargeStartCornerRadius"] = new CornerRadius(16, 0, 0, 16),
            ["MaterialShapeLargeEndCornerRadius"] = new CornerRadius(0, 16, 16, 0),
            // CornerSize values, kept separately from CornerRadius for shape interpolation/clamping.
            ["MaterialShapeCornerValueNone"] = 0d,
            ["MaterialShapeCornerValueExtraSmall"] = 4d,
            ["MaterialShapeCornerValueSmall"] = 8d,
            ["MaterialShapeCornerValueMedium"] = 12d,
            ["MaterialShapeCornerValueLarge"] = 16d,
            ["MaterialShapeCornerValueLargeIncreased"] = 20d,
            ["MaterialShapeCornerValueExtraLarge"] = 28d,
            ["MaterialShapeCornerValueExtraLargeIncreased"] = 32d,
            ["MaterialShapeCornerValueExtraExtraLarge"] = 48d,
            // Material's full corner uses an oversized radius, clamped by the renderer.
            ["MaterialShapeFullCornerRadius"] = new CornerRadius(9999),
            ["MaterialHoverStateLayerOpacity"] = 0.08d,
            ["MaterialFocusStateLayerOpacity"] = 0.1d,
            ["MaterialPressedStateLayerOpacity"] = 0.1d,
            ["MaterialDraggedStateLayerOpacity"] = 0.16d,
            ["MaterialElevationLevel0"] = 0d,
            ["MaterialElevationLevel1"] = 1d,
            ["MaterialElevationLevel2"] = 3d,
            ["MaterialElevationLevel3"] = 6d,
            ["MaterialElevationLevel4"] = 8d,
            ["MaterialElevationLevel5"] = 12d,
            // Public dp distances. Shadow consumers use matching logical LevelN tokens;
            // MaterialSurface applies tonal elevation independently of the shadow axis.

            // md.sys.motion.easing. Templates inline the same curves so transitions do not
            // depend on resource lookup inside a Transitions collection.
            ["MaterialEasingStandard"] = new SplineEasing(0.2, 0, 0, 1),
            ["MaterialEasingStandardAccelerate"] = new SplineEasing(0.3, 0, 1, 1),
            ["MaterialEasingStandardDecelerate"] = new SplineEasing(0, 0, 0, 1),
            ["MaterialEasingEmphasized"] = new SplineEasing(0.2, 0, 0, 1),
            ["MaterialEasingEmphasizedAccelerate"] = new SplineEasing(0.3, 0, 0.8, 0.15),
            ["MaterialEasingEmphasizedDecelerate"] = new SplineEasing(0.05, 0.7, 0.1, 1),
            ["MaterialEasingLegacy"] = new SplineEasing(0.4, 0, 0.2, 1),
            ["MaterialEasingLegacyAccelerate"] = new SplineEasing(0.4, 0, 1, 1),
            ["MaterialEasingLegacyDecelerate"] = new SplineEasing(0, 0, 0.2, 1),
            ["MaterialEasingLinear"] = new SplineEasing(0, 0, 1, 1),

            // Template strings. Override these keys to localize built-in automation names,
            // tooltips, and context-menu commands.
            ["MaterialTextInputCutText"] = "Cut",
            ["MaterialTextInputCopyText"] = "Copy",
            ["MaterialTextInputPasteText"] = "Paste",
            ["MaterialTextInputSelectAllText"] = "Select all",
            ["MaterialPasswordShowText"] = "Show password",
            ["MaterialPasswordHideText"] = "Hide password",
            ["MaterialRangeLowerValueText"] = "Lower value",
            ["MaterialRangeUpperValueText"] = "Upper value",
            ["MaterialScrollLeftText"] = "Scroll left",
            ["MaterialScrollRightText"] = "Scroll right",
            ["MaterialScrollUpText"] = "Scroll up",
            ["MaterialScrollDownText"] = "Scroll down",
            ["MaterialPageLeftText"] = "Page left",
            ["MaterialPageRightText"] = "Page right",
            ["MaterialPageUpText"] = "Page up",
            ["MaterialPageDownText"] = "Page down",
            ["MaterialScrollPositionText"] = "Scroll position",
        };

        MaterialDurationTokens.Apply(resources, reduced: false);
        // Standard is the upstream MaterialTheme default; Expressive is opt-in per application.
        // MaterialSpringTokens.Apply is the single writer of spring and scheme resources.
        MaterialSpringTokens.Apply(resources, MaterialMotionScheme.Standard);
        // The accessibility policy is separate from the scheme; MaterialTheme overwrites this
        // when the application or platform requests reduced motion.
        resources["MaterialReducedMotion"] = false;

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

        // Tracking and emphasized weights follow AndroidX Material3 TypeScaleTokens v0_103.
        string[] roles = ["DisplayLarge", "DisplayMedium", "DisplaySmall", "HeadlineLarge", "HeadlineMedium", "HeadlineSmall", "TitleLarge", "TitleMedium", "TitleSmall", "BodyLarge", "BodyMedium", "BodySmall", "LabelLarge", "LabelMedium", "LabelSmall"];
        double[] tracking = [-0.2, 0, 0, 0, 0, 0, 0, 0.2, 0.1, 0.5, 0.2, 0.4, 0.1, 0.5, 0.5];
        double[] emphasizedTracking = [0, 0, 0, 0, 0, 0, 0, 0.15, 0.1, 0.15, 0.25, 0.4, 0.1, 0.5, 0.5];
        for (var i = 0; i < roles.Length; i++)
        {
            var prefix = $"Material{roles[i]}";
            resources[$"{prefix}LetterSpacing"] = tracking[i];
            resources[$"{prefix}EmphasizedFontSize"] = resources[$"{prefix}FontSize"];
            resources[$"{prefix}EmphasizedLineHeight"] = resources[$"{prefix}LineHeight"];
            resources[$"{prefix}EmphasizedLetterSpacing"] = emphasizedTracking[i];
            resources[$"{prefix}EmphasizedFontWeight"] = roles[i].StartsWith("Label", StringComparison.Ordinal) || roles[i] is "TitleMedium" or "TitleSmall" ? FontWeight.Bold : FontWeight.Medium;
        }
        // Two-layer ambient/key shadows. Geometry follows Material Web elevation tokens.
        resources["MaterialElevationLevel0Shadow"] = BoxShadows.Parse("0 0 0 0 #00000000");
        resources["MaterialElevationLevel1Shadow"] = BoxShadows.Parse("0 1 2 0 #4D000000, 0 1 3 1 #26000000");
        resources["MaterialElevationLevel2Shadow"] = BoxShadows.Parse("0 1 2 0 #4D000000, 0 2 6 2 #26000000");
        resources["MaterialElevationLevel3Shadow"] = BoxShadows.Parse("0 1 3 0 #4D000000, 0 4 8 3 #26000000");
        resources["MaterialElevationLevel4Shadow"] = BoxShadows.Parse("0 2 3 0 #4D000000, 0 6 10 4 #26000000");
        resources["MaterialElevationLevel5Shadow"] = BoxShadows.Parse("0 4 4 0 #4D000000, 0 8 12 6 #26000000");
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
