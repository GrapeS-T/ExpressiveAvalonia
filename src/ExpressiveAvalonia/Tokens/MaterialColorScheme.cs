using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Media;
using MaterialColorUtilities.DynamicColors;
using MaterialColorUtilities.HCT;
using MaterialColorUtilities.Scheme;
using MaterialColorUtilities.Utils;

namespace ExpressiveAvalonia.Tokens;

/// <summary>Generates the complete Material Design 3 color-role resource set.</summary>
public static class MaterialColorScheme
{
    /// <summary>Creates an opaque HCT-based Material Tonal Spot color scheme.</summary>
    /// <param name="seed">The source color. Alpha is ignored; generated roles are opaque.</param>
    /// <param name="dark">Whether to generate the dark scheme.</param>
    /// <param name="contrast">
    /// Material contrast level: -1 is reduced, 0 is standard, 0.5 is medium, and 1 is high.
    /// Intermediate values use the Material contrast curves.
    /// </param>
    /// <returns>All 49 MD3 roles, named <c>Material{Role}Color</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="contrast"/> is not finite or is outside [-1, 1].
    /// </exception>
    /// <remarks>
    /// Uses the explicit 2021 MD3 specification and phone role set from Material Color Utilities.
    /// Tonal Spot is a palette-generation choice, independent of expressive motion and typography.
    /// Surface tint follows Compose's <c>surfaceTint = primary</c> default rather than exposing a
    /// separate dynamic-scheme role.
    /// </remarks>
    public static IReadOnlyDictionary<string, Color> Create(Color seed, bool dark, double contrast = 0)
    {
        if (!double.IsFinite(contrast) || contrast < -1 || contrast > 1)
            throw new ArgumentOutOfRangeException(nameof(contrast), contrast,
                "Material contrast must be finite and between -1 and 1.");

        var source = Hct.From(new ArgbColor(255, seed.R, seed.G, seed.B));
        var scheme = new SchemeTonalSpot(source, dark, contrast,
            ColorSpec.SpecVersion.Spec2021, DynamicScheme.Platform.Phone);

        return new ReadOnlyDictionary<string, Color>(new Dictionary<string, Color>(49, StringComparer.Ordinal)
        {
            ["MaterialPrimaryColor"] = ToColor(scheme.Primary),
            ["MaterialOnPrimaryColor"] = ToColor(scheme.OnPrimary),
            ["MaterialPrimaryContainerColor"] = ToColor(scheme.PrimaryContainer),
            ["MaterialOnPrimaryContainerColor"] = ToColor(scheme.OnPrimaryContainer),
            ["MaterialInversePrimaryColor"] = ToColor(scheme.InversePrimary),
            ["MaterialSecondaryColor"] = ToColor(scheme.Secondary),
            ["MaterialOnSecondaryColor"] = ToColor(scheme.OnSecondary),
            ["MaterialSecondaryContainerColor"] = ToColor(scheme.SecondaryContainer),
            ["MaterialOnSecondaryContainerColor"] = ToColor(scheme.OnSecondaryContainer),
            ["MaterialTertiaryColor"] = ToColor(scheme.Tertiary),
            ["MaterialOnTertiaryColor"] = ToColor(scheme.OnTertiary),
            ["MaterialTertiaryContainerColor"] = ToColor(scheme.TertiaryContainer),
            ["MaterialOnTertiaryContainerColor"] = ToColor(scheme.OnTertiaryContainer),
            ["MaterialErrorColor"] = ToColor(scheme.Error),
            ["MaterialOnErrorColor"] = ToColor(scheme.OnError),
            ["MaterialErrorContainerColor"] = ToColor(scheme.ErrorContainer),
            ["MaterialOnErrorContainerColor"] = ToColor(scheme.OnErrorContainer),
            ["MaterialPrimaryFixedColor"] = ToColor(scheme.PrimaryFixed),
            ["MaterialPrimaryFixedDimColor"] = ToColor(scheme.PrimaryFixedDim),
            ["MaterialOnPrimaryFixedColor"] = ToColor(scheme.OnPrimaryFixed),
            ["MaterialOnPrimaryFixedVariantColor"] = ToColor(scheme.OnPrimaryFixedVariant),
            ["MaterialSecondaryFixedColor"] = ToColor(scheme.SecondaryFixed),
            ["MaterialSecondaryFixedDimColor"] = ToColor(scheme.SecondaryFixedDim),
            ["MaterialOnSecondaryFixedColor"] = ToColor(scheme.OnSecondaryFixed),
            ["MaterialOnSecondaryFixedVariantColor"] = ToColor(scheme.OnSecondaryFixedVariant),
            ["MaterialTertiaryFixedColor"] = ToColor(scheme.TertiaryFixed),
            ["MaterialTertiaryFixedDimColor"] = ToColor(scheme.TertiaryFixedDim),
            ["MaterialOnTertiaryFixedColor"] = ToColor(scheme.OnTertiaryFixed),
            ["MaterialOnTertiaryFixedVariantColor"] = ToColor(scheme.OnTertiaryFixedVariant),
            ["MaterialBackgroundColor"] = ToColor(scheme.Background),
            ["MaterialOnBackgroundColor"] = ToColor(scheme.OnBackground),
            ["MaterialSurfaceColor"] = ToColor(scheme.Surface),
            ["MaterialSurfaceDimColor"] = ToColor(scheme.SurfaceDim),
            ["MaterialSurfaceBrightColor"] = ToColor(scheme.SurfaceBright),
            ["MaterialSurfaceContainerLowestColor"] = ToColor(scheme.SurfaceContainerLowest),
            ["MaterialSurfaceContainerLowColor"] = ToColor(scheme.SurfaceContainerLow),
            ["MaterialSurfaceContainerColor"] = ToColor(scheme.SurfaceContainer),
            ["MaterialSurfaceContainerHighColor"] = ToColor(scheme.SurfaceContainerHigh),
            ["MaterialSurfaceContainerHighestColor"] = ToColor(scheme.SurfaceContainerHighest),
            ["MaterialOnSurfaceColor"] = ToColor(scheme.OnSurface),
            ["MaterialSurfaceVariantColor"] = ToColor(scheme.SurfaceVariant),
            ["MaterialOnSurfaceVariantColor"] = ToColor(scheme.OnSurfaceVariant),
            ["MaterialInverseSurfaceColor"] = ToColor(scheme.InverseSurface),
            ["MaterialInverseOnSurfaceColor"] = ToColor(scheme.InverseOnSurface),
            ["MaterialOutlineColor"] = ToColor(scheme.Outline),
            ["MaterialOutlineVariantColor"] = ToColor(scheme.OutlineVariant),
            ["MaterialShadowColor"] = ToColor(scheme.Shadow),
            ["MaterialScrimColor"] = ToColor(scheme.Scrim),
            // Compose's lightColorScheme/darkColorScheme default surfaceTint to primary
            // (ColorScheme.kt:896), not to a separate dynamic-scheme role.
            ["MaterialSurfaceTintColor"] = ToColor(scheme.Primary)
        });
    }

    private static Color ToColor(ArgbColor color) =>
        Color.FromArgb(color.Alpha, color.Red, color.Green, color.Blue);
}
