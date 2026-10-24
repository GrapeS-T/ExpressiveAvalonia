namespace ExpressiveAvalonia.Tokens;

/// <summary>An AndroidX Material3 Expressive spring from ExpressiveMotionTokens.</summary>
/// <remarks>
/// The spatial set moves position, size, and shape; the effects set fades and colors.
/// Material Web's md.sys.motion.spring.fast.spatial uses a different pair (1400 / 0.9), which
/// remains the default of the parameterless <see cref="Theming.MaterialMotion.Animate"/> overload.
/// </remarks>
public enum MaterialSpring
{
    SpatialDefault,
    SpatialFast,
    SpatialSlow,
    EffectsDefault,
    EffectsFast,
    EffectsSlow,
}

/// <summary>Spring parameters from AndroidX Material3 ExpressiveMotionTokens.</summary>
public static class MaterialSpringTokens
{
    /// <summary>Returns the stiffness and damping ratio of <paramref name="spring"/>.</summary>
    public static (double Stiffness, double DampingRatio) Get(MaterialSpring spring) => spring switch
    {
        MaterialSpring.SpatialDefault => (380d, 0.8d),
        MaterialSpring.SpatialFast => (800d, 0.6d),
        MaterialSpring.SpatialSlow => (200d, 0.8d),
        MaterialSpring.EffectsDefault => (1600d, 1d),
        MaterialSpring.EffectsFast => (3800d, 1d),
        MaterialSpring.EffectsSlow => (800d, 1d),
        _ => throw new System.ArgumentOutOfRangeException(nameof(spring), spring, null),
    };

    /// <summary>The resource key prefix of <paramref name="spring"/>, for example "MaterialSpringSpatialFast".</summary>
    public static string ResourcePrefix(MaterialSpring spring) => "MaterialSpring" + spring;
}
