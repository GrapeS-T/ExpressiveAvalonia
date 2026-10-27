using Avalonia.Controls;

namespace ExpressiveAvalonia.Tokens;

/// <summary>Physical motion scheme, independent of the reduced-motion accessibility policy.</summary>
public enum MaterialMotionScheme { Standard, Expressive }

/// <summary>Semantic spatial (bounds/shape) or effects (color/alpha) spring.</summary>
public enum MaterialSpring
{
    SpatialDefault,
    SpatialFast,
    SpatialSlow,
    EffectsDefault,
    EffectsFast,
    EffectsSlow,
}

public static class MaterialSpringTokens
{
    /// <summary>
    /// Returns the stiffness and damping ratio of <paramref name="spring"/> for
    /// <paramref name="scheme"/>. The default is <see cref="MaterialMotionScheme.Standard"/>,
    /// matching the upstream <c>MaterialTheme.Values.motionScheme</c> default; Expressive must be
    /// selected explicitly by the application.
    /// </summary>
    public static (double Stiffness, double DampingRatio) Get(MaterialSpring spring,
        MaterialMotionScheme scheme = MaterialMotionScheme.Standard)
    {
        if (scheme is not (MaterialMotionScheme.Standard or MaterialMotionScheme.Expressive))
            throw new ArgumentOutOfRangeException(nameof(scheme));
        var standard = scheme == MaterialMotionScheme.Standard;
        return spring switch
        {
            MaterialSpring.SpatialDefault => standard ? (700d, 0.9d) : (380d, 0.8d),
            MaterialSpring.SpatialFast => standard ? (1400d, 0.9d) : (800d, 0.6d),
            MaterialSpring.SpatialSlow => standard ? (300d, 0.9d) : (200d, 0.8d),
            MaterialSpring.EffectsDefault => (1600d, 1d),
            MaterialSpring.EffectsFast => (3800d, 1d),
            MaterialSpring.EffectsSlow => (800d, 1d),
            _ => throw new ArgumentOutOfRangeException(nameof(spring)),
        };
    }

    public static string ResourcePrefix(MaterialSpring spring) => "MaterialSpring" + spring;

    internal static void Apply(IResourceDictionary resources, MaterialMotionScheme scheme)
    {
        resources["MaterialMotionScheme"] = scheme;
        foreach (var spring in Enum.GetValues<MaterialSpring>())
        {
            var (stiffness, damping) = Get(spring, scheme);
            var prefix = ResourcePrefix(spring);
            resources[prefix + "Stiffness"] = stiffness;
            resources[prefix + "Damping"] = damping;
        }
    }
}
