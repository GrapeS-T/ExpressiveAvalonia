using System.ComponentModel;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace ExpressiveAvalonia.Controls;

/// <summary>How a <see cref="MaterialDivider"/> insets its line from the container edges.</summary>
/// <remarks>
/// Mirrors the source divider packages: <c>Full-width</c> spans the whole container,
/// <c>Inset</c> starts 16 dp from the leading edge, and <c>Middle-inset</c> keeps 16 dp on
/// both ends. The source uses the same 16 dp inset for horizontal and vertical dividers.
/// </remarks>
public enum MaterialDividerVariant
{
    /// <summary>No inset; the line spans the full container (source: Horizontal/Vertical Full-width).</summary>
    FullWidth,

    /// <summary>16 dp from the leading edge only (source: Horizontal/Vertical Inset).</summary>
    Inset,

    /// <summary>16 dp from both ends (source: Horizontal/Vertical Middle-inset).</summary>
    MiddleInset,
}

/// <summary>A Material divider, optionally with a leading subheader label.</summary>
/// <remarks>
/// <para>
/// The line is painted with <see cref="Brush"/> at <see cref="LineThickness"/> (1 dp in the
/// source, using the outline-variant role). <see cref="Variant"/> applies the source insets,
/// and <see cref="Subheader"/> reproduces the source "Divider with subhead" layout: the line,
/// a 4 dp gap, then a title-small label in the on-surface-variant role.
/// </para>
/// <para>
/// A divider is not interactive; it takes no focus and handles no keys. It is a templated
/// control so the line and subheader can be restyled without touching the geometry logic.
/// </para>
/// </remarks>
public sealed class MaterialDivider : TemplatedControl
{
    /// <summary>Defines the <see cref="Orientation"/> property.</summary>
    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<MaterialDivider, Orientation>(nameof(Orientation), Orientation.Horizontal);

    /// <summary>Defines the <see cref="Variant"/> property.</summary>
    public static readonly StyledProperty<MaterialDividerVariant> VariantProperty =
        AvaloniaProperty.Register<MaterialDivider, MaterialDividerVariant>(
            nameof(Variant), MaterialDividerVariant.FullWidth);

    /// <summary>Defines the <see cref="LineThickness"/> property.</summary>
    public static readonly StyledProperty<double> LineThicknessProperty =
        AvaloniaProperty.Register<MaterialDivider, double>(nameof(LineThickness), 1,
            validate: value => double.IsFinite(value) && value >= 0);

    /// <summary>Defines the <see cref="Brush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> BrushProperty =
        AvaloniaProperty.Register<MaterialDivider, IBrush?>(nameof(Brush));

    /// <summary>Defines the <see cref="Subheader"/> property.</summary>
    public static readonly StyledProperty<string?> SubheaderProperty =
        AvaloniaProperty.Register<MaterialDivider, string?>(nameof(Subheader));

    /// <summary>Defines the <see cref="HasSubheader"/> property.</summary>
    public static readonly StyledProperty<bool> HasSubheaderProperty =
        AvaloniaProperty.Register<MaterialDivider, bool>(nameof(HasSubheader));

    static MaterialDivider()
    {
        SubheaderProperty.Changed.AddClassHandler<MaterialDivider>((divider, _) =>
            divider.SetCurrentValue(HasSubheaderProperty, !string.IsNullOrEmpty(divider.Subheader)));
    }

    /// <summary>Gets or sets whether the line runs horizontally or vertically.</summary>
    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>Gets or sets the source inset variant.</summary>
    public MaterialDividerVariant Variant
    {
        get => GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    /// <summary>Gets or sets the line thickness in device-independent units.</summary>
    public double LineThickness
    {
        get => GetValue(LineThicknessProperty);
        set => SetValue(LineThicknessProperty, value);
    }

    /// <summary>Gets or sets the line brush; the theme supplies the outline-variant role.</summary>
    public IBrush? Brush
    {
        get => GetValue(BrushProperty);
        set => SetValue(BrushProperty, value);
    }

    /// <summary>Gets or sets the optional subheader text shown under a horizontal line.</summary>
    public string? Subheader
    {
        get => GetValue(SubheaderProperty);
        set => SetValue(SubheaderProperty, value);
    }

    /// <summary>
    /// Gets whether <see cref="Subheader"/> contains text. The value is kept in sync with
    /// <see cref="Subheader"/>; the template uses it to collapse the label row.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool HasSubheader => GetValue(HasSubheaderProperty);
}
