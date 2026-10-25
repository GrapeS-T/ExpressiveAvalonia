using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace ExpressiveAvalonia.Controls;

/// <summary>One avatar photograph exported from the design source.</summary>
/// <remarks>
/// <see cref="SourceId"/> is the design node id of the package that exported the image
/// (<c>local-000001</c> for "3D Avatars / 1"), so it stays unique even though the display names
/// are numbered per package. <see cref="Image"/> is decoded once per process on first use and is
/// shared by every control that asks for it; callers must not dispose it.
/// </remarks>
public sealed class MaterialAvatarInfo
{
    private readonly Lazy<IImage> _image;

    internal MaterialAvatarInfo(MaterialAvatarRecord record)
    {
        SourceId = record.Id;
        Name = record.Name;
        Index = record.Index;
        FileName = record.File;
        ResourceUri = new Uri($"avares://ExpressiveAvalonia/Assets/Avatars/{record.File}");
        _image = new Lazy<IImage>(() => MaterialAvatarAssets.Load(ResourceUri));
    }

    /// <summary>Design source node id, for example <c>local-000001</c>.</summary>
    public string SourceId { get; }

    /// <summary>Source package name, for example <c>3D Avatars / 1</c>.</summary>
    public string Name { get; }

    /// <summary>One-based source numbering of the avatar package.</summary>
    public int Index { get; }

    /// <summary>Embedded PNG file name below <c>Assets/Avatars</c>.</summary>
    public string FileName { get; }

    /// <summary><c>avares://</c> URI of the embedded PNG.</summary>
    public Uri ResourceUri { get; }

    /// <summary>The decoded photograph. Shared and never disposed by the library.</summary>
    public IImage Image => _image.Value;

    /// <inheritdoc />
    public override string ToString() => $"{Name} ({SourceId})";
}

/// <summary>Every avatar photograph exported from the design source, keyed by source identity.</summary>
/// <remarks>
/// The thirty "3D Avatars / n" packages each export one 1080 x 1080 PNG. The embedded copies are
/// byte-for-byte copies of those exports, so the original visual data, alpha channel and color
/// profile are preserved without resampling or recompression; the whole family costs about 39 MB
/// of resources. See <c>tools/DesignAssets/generate-avatars.mjs</c>.
/// </remarks>
public static class MaterialAvatars
{
    private static readonly ReadOnlyCollection<MaterialAvatarInfo> s_all;
    private static readonly Dictionary<string, MaterialAvatarInfo> s_byId = new(StringComparer.Ordinal);
    private static readonly Dictionary<int, MaterialAvatarInfo> s_byIndex = new();

    static MaterialAvatars()
    {
        var all = new List<MaterialAvatarInfo>(MaterialAvatarData.Avatars.Length);
        foreach (var record in MaterialAvatarData.Avatars)
        {
            var info = new MaterialAvatarInfo(record);
            all.Add(info);
            s_byId[info.SourceId] = info;
            s_byIndex[info.Index] = info;
        }
        s_all = new ReadOnlyCollection<MaterialAvatarInfo>(all);
    }

    /// <summary>Every exported avatar, in source order.</summary>
    public static IReadOnlyList<MaterialAvatarInfo> All => s_all;

    /// <summary>Gets an avatar by the design node id of its package.</summary>
    /// <exception cref="KeyNotFoundException">No avatar uses <paramref name="sourceId"/>.</exception>
    public static MaterialAvatarInfo Get(string sourceId)
    {
        ArgumentNullException.ThrowIfNull(sourceId);
        return s_byId.TryGetValue(sourceId, out var info)
            ? info
            : throw new KeyNotFoundException($"The design source has no avatar '{sourceId}'.");
    }

    /// <summary>Looks an avatar up by the design node id of its package.</summary>
    public static bool TryGet(string sourceId, [NotNullWhen(true)] out MaterialAvatarInfo? info)
    {
        ArgumentNullException.ThrowIfNull(sourceId);
        return s_byId.TryGetValue(sourceId, out info);
    }

    /// <summary>Gets an avatar by its one-based source number, for example 1 for "3D Avatars / 1".</summary>
    /// <exception cref="ArgumentOutOfRangeException">No avatar uses <paramref name="index"/>.</exception>
    public static MaterialAvatarInfo GetByIndex(int index) =>
        s_byIndex.TryGetValue(index, out var info)
            ? info
            : throw new ArgumentOutOfRangeException(nameof(index), index,
                $"The design source exports avatars 1..{s_all.Count}.");

    /// <summary>Looks an avatar up by its one-based source number.</summary>
    public static bool TryGetByIndex(int index, [NotNullWhen(true)] out MaterialAvatarInfo? info) =>
        s_byIndex.TryGetValue(index, out info);
}

/// <summary>
/// The two images the source uses as building-block content: the thumbnail placeholder and the
/// favicon symbol.
/// </summary>
/// <remarks>
/// Both are the exported PNGs from the source package's image fills, embedded unchanged. They are
/// decoded once per process and shared; callers must not dispose them.
/// </remarks>
public static class MaterialAvatarAssets
{
    private static readonly Uri s_placeholderUri =
        new($"avares://ExpressiveAvalonia/Assets/Avatars/{MaterialAvatarData.ThumbnailPlaceholderFile}");
    private static readonly Uri s_faviconUri =
        new($"avares://ExpressiveAvalonia/Assets/Avatars/{MaterialAvatarData.FaviconSymbolFile}");
    private static readonly Lazy<IImage> s_placeholder = new(() => Load(s_placeholderUri));
    private static readonly Lazy<IImage> s_favicon = new(() => Load(s_faviconUri));

    /// <summary>
    /// The source placeholder image: three grey shapes on a transparent 700 x 700 canvas. Both
    /// thumbnail building blocks draw it over the surface container color.
    /// </summary>
    public static IImage ThumbnailPlaceholder => s_placeholder.Value;

    /// <summary>Resource URI of <see cref="ThumbnailPlaceholder"/>.</summary>
    public static Uri ThumbnailPlaceholderUri => s_placeholderUri;

    /// <summary>The source favicon symbol: the exported 991 x 1801 logo used by the favicon block.</summary>
    public static IImage FaviconSymbol => s_favicon.Value;

    /// <summary>Resource URI of <see cref="FaviconSymbol"/>.</summary>
    public static Uri FaviconSymbolUri => s_faviconUri;

    internal static IImage Load(Uri uri)
    {
        using var stream = AssetLoader.Open(uri);
        return new Bitmap(stream);
    }
}
