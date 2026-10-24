using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Controls;

namespace ExpressiveAvalonia.Tokens;

/// <summary>The md.sys.motion duration tokens, keyed by their resource name.</summary>
/// <remarks>
/// Kept as a table rather than as literals so <see cref="Theming.MaterialTheme"/> can zero every
/// duration when reduced motion is requested, which also collapses the state transitions that
/// reference these keys.
/// </remarks>
public static class MaterialDurationTokens
{
    private static readonly Dictionary<string, TimeSpan> Base = new(StringComparer.Ordinal)
    {
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

    /// <summary>All duration resources with their baseline values.</summary>
    public static IReadOnlyDictionary<string, TimeSpan> All { get; } = new ReadOnlyDictionary<string, TimeSpan>(Base);

    /// <summary>Writes the durations into <paramref name="resources"/>, zeroing them when reduced.</summary>
    public static void Apply(ResourceDictionary resources, bool reduced)
    {
        foreach (var (key, duration) in Base)
            resources[key] = reduced ? TimeSpan.Zero : duration;
    }
}
