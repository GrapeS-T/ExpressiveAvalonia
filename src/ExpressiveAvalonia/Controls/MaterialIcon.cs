using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// One glyph exported from the design source. <see cref="SourceId"/> is the design node id and
/// stays unique even when two packages share a display name.
/// </summary>
public sealed class MaterialIconInfo
{
    private readonly Lazy<Geometry> _geometry;

    internal MaterialIconInfo(MaterialIconRecord record)
    {
        SourceId = record.Id;
        Name = record.Name;
        FileName = record.File;
        ViewBox = new Rect(0, 0, record.Width, record.Height);
        ResourceUri = new Uri($"avares://ExpressiveAvalonia/Assets/DesignSystem/Icons/{record.File}");
        _geometry = new Lazy<Geometry>(() => Geometry.Parse(record.PathData));
    }

    /// <summary>Design source node id, for example <c>local-000035</c>.</summary>
    public string SourceId { get; }

    /// <summary>Source layer name. Not unique: see <see cref="MaterialIcons.GetByName"/>.</summary>
    public string Name { get; }

    /// <summary>Embedded SVG file name below <c>Assets/DesignSystem/Icons</c>.</summary>
    public string FileName { get; }

    /// <summary><c>avares://</c> URI of the embedded SVG.</summary>
    public Uri ResourceUri { get; }

    /// <summary>Source vector bounds; every exported glyph uses a 24 x 24 box.</summary>
    public Rect ViewBox { get; }

    /// <summary>Parsed fill geometry. The instance is shared and never mutated.</summary>
    public Geometry Geometry => _geometry.Value;

    public override string ToString() => $"{Name} ({SourceId})";
}

/// <summary>Icon lookup keyed by design source identity, with name lookups that keep duplicates.</summary>
/// <remarks>
/// The design source contains eighteen names exported twice (for example <c>alarm</c>,
/// <c>settings</c> and <c>check_box</c>) with different geometry. Both identities are kept;
/// name lookups return every match in source order instead of collapsing them.
/// </remarks>
public static class MaterialIcons
{
    private static readonly ReadOnlyCollection<MaterialIconInfo> s_all;
    private static readonly Dictionary<string, MaterialIconInfo> s_byId = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, List<MaterialIconInfo>> s_byName = new(StringComparer.Ordinal);
    private static readonly ReadOnlyCollection<string> s_names;

    static MaterialIcons()
    {
        var all = new List<MaterialIconInfo>(MaterialIconData.Icons.Length);
        var names = new List<string>();
        foreach (var record in MaterialIconData.Icons)
        {
            var info = new MaterialIconInfo(record);
            all.Add(info);
            s_byId[info.SourceId] = info;
            if (!s_byName.TryGetValue(info.Name, out var list))
            {
                s_byName[info.Name] = list = new List<MaterialIconInfo>(1);
                names.Add(info.Name);
            }
            list.Add(info);
        }
        s_all = new ReadOnlyCollection<MaterialIconInfo>(all);
        s_names = new ReadOnlyCollection<string>(names);
    }

    /// <summary>Every exported glyph, in source order.</summary>
    public static IReadOnlyList<MaterialIconInfo> All => s_all;

    /// <summary>Distinct source layer names, in source order.</summary>
    public static IReadOnlyList<string> Names => s_names;

    /// <summary>Gets a glyph by its unique design source id.</summary>
    /// <exception cref="KeyNotFoundException">No glyph uses <paramref name="sourceId"/>.</exception>
    public static MaterialIconInfo Get(string sourceId)
    {
        ArgumentNullException.ThrowIfNull(sourceId);
        return s_byId.TryGetValue(sourceId, out var info)
            ? info
            : throw new KeyNotFoundException($"The design source has no icon '{sourceId}'.");
    }

    /// <summary>Looks a glyph up by its unique design source id.</summary>
    public static bool TryGet(string sourceId, [NotNullWhen(true)] out MaterialIconInfo? info)
    {
        ArgumentNullException.ThrowIfNull(sourceId);
        return s_byId.TryGetValue(sourceId, out info);
    }

    /// <summary>Gets every glyph exported under <paramref name="name"/>; duplicates are preserved.</summary>
    public static IReadOnlyList<MaterialIconInfo> GetByName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return s_byName.TryGetValue(name, out var list) ? list : Array.Empty<MaterialIconInfo>();
    }

    /// <summary>Gets the first glyph exported under <paramref name="name"/> in source order.</summary>
    public static bool TryGetByName(string name, [NotNullWhen(true)] out MaterialIconInfo? info)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (s_byName.TryGetValue(name, out var list))
        {
            info = list[0];
            return true;
        }
        info = null;
        return false;
    }

    /// <summary>Whether a display name is shared by more than one source identity.</summary>
    public static bool IsAmbiguousName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return s_byName.TryGetValue(name, out var list) && list.Count > 1;
    }
}

/// <summary>Renders one design-source glyph as a tinted vector.</summary>
/// <remarks>
/// <para>
/// The glyph is resolved from <see cref="SourceId"/> (unique) or, when that is empty, from
/// <see cref="IconName"/> (first match in source order). Unknown values render no geometry
/// instead of substituting a different icon. Use <see cref="MaterialIcons.Get"/> when a miss
/// must be an error.
/// </para>
/// <para>
/// Set <see cref="IsDecorative"/> when surrounding text already conveys the meaning; the icon is
/// then hidden from the accessibility tree. Otherwise it exposes the source layer name as its
/// automation name and reports an Image control type.
/// </para>
/// </remarks>
public sealed class MaterialIcon : TemplatedControl
{
    /// <summary>Design source node id; takes precedence over <see cref="IconName"/>.</summary>
    public static readonly StyledProperty<string?> SourceIdProperty =
        AvaloniaProperty.Register<MaterialIcon, string?>(nameof(SourceId));

    /// <summary>Source layer name. Duplicate names resolve to the first exported glyph.</summary>
    public static readonly StyledProperty<string?> IconNameProperty =
        AvaloniaProperty.Register<MaterialIcon, string?>(nameof(IconName));

    /// <summary>How the glyph fills the control bounds.</summary>
    public static readonly StyledProperty<Stretch> StretchProperty =
        AvaloniaProperty.Register<MaterialIcon, Stretch>(nameof(Stretch), Stretch.Uniform);

    /// <summary>Hides the icon from the accessibility tree when the surrounding text is enough.</summary>
    public static readonly StyledProperty<bool> IsDecorativeProperty =
        AvaloniaProperty.Register<MaterialIcon, bool>(nameof(IsDecorative));

    public static readonly DirectProperty<MaterialIcon, MaterialIconInfo?> IconProperty =
        AvaloniaProperty.RegisterDirect<MaterialIcon, MaterialIconInfo?>(nameof(Icon), o => o.Icon);

    public static readonly DirectProperty<MaterialIcon, Geometry?> GeometryProperty =
        AvaloniaProperty.RegisterDirect<MaterialIcon, Geometry?>(nameof(Geometry), o => o.Geometry);

    private MaterialIconInfo? _icon;
    private Geometry? _geometry;

    static MaterialIcon()
    {
        AffectsMeasure<MaterialIcon>(StretchProperty);
        SourceIdProperty.Changed.AddClassHandler<MaterialIcon>((icon, _) => icon.Resolve());
        IconNameProperty.Changed.AddClassHandler<MaterialIcon>((icon, _) => icon.Resolve());
    }

    public string? SourceId { get => GetValue(SourceIdProperty); set => SetValue(SourceIdProperty, value); }

    public string? IconName { get => GetValue(IconNameProperty); set => SetValue(IconNameProperty, value); }

    public Stretch Stretch { get => GetValue(StretchProperty); set => SetValue(StretchProperty, value); }

    public bool IsDecorative { get => GetValue(IsDecorativeProperty); set => SetValue(IsDecorativeProperty, value); }

    /// <summary>The resolved glyph, or <c>null</c> when the requested identity is unknown.</summary>
    public MaterialIconInfo? Icon => _icon;

    /// <summary>The parsed source geometry, or <c>null</c> when the requested identity is unknown.</summary>
    public Geometry? Geometry => _geometry;

    /// <summary>Creates an icon bound to a unique source id.</summary>
    /// <exception cref="KeyNotFoundException">No glyph uses <paramref name="sourceId"/>.</exception>
    public static MaterialIcon For(string sourceId) =>
        new() { SourceId = MaterialIcons.Get(sourceId).SourceId };

    protected override AutomationPeer OnCreateAutomationPeer() => new MaterialIconAutomationPeer(this);

    private void Resolve()
    {
        MaterialIconInfo? info = null;
        var id = SourceId;
        if (!string.IsNullOrWhiteSpace(id))
            MaterialIcons.TryGet(id.Trim(), out info);
        else if (!string.IsNullOrWhiteSpace(IconName))
            MaterialIcons.TryGetByName(IconName.Trim(), out info);

        if (ReferenceEquals(info, _icon))
            return;

        SetAndRaise(IconProperty, ref _icon, info);
        SetAndRaise(GeometryProperty, ref _geometry, info?.Geometry);
    }

    private sealed class MaterialIconAutomationPeer(MaterialIcon owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;

        protected override string GetClassNameCore() => nameof(MaterialIcon);

        protected override string? GetNameCore() =>
            AutomationProperties.GetName(owner) is { Length: > 0 } name
                ? name
                : owner.Icon?.Name ?? base.GetNameCore();

        protected override bool IsControlElementCore() => !owner.IsDecorative && base.IsControlElementCore();

        protected override bool IsContentElementCore() => !owner.IsDecorative && base.IsContentElementCore();
    }
}
