using Avalonia;
using Avalonia.Controls.Shapes;
using Avalonia.Media;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// A decorative Material Expressive shape drawn from the source shape family.
/// </summary>
/// <remarks>
/// <para>
/// The control derives from <see cref="Shape"/>, so <see cref="Shape.Fill"/>,
/// <see cref="Shape.Stroke"/>, <see cref="Shape.StrokeThickness"/> and
/// <see cref="Shape.Stretch"/> behave exactly like every other Avalonia shape. The theme sets
/// <see cref="Shape.Stretch"/> to <see cref="Stretch.Uniform"/> and
/// <see cref="Shape.Fill"/> to the primary role, matching the source previews, which paint the
/// 35 named shapes with <c>M3/sys/light/primary</c>; the nine numbered shapes use the primary
/// container role in the source and can be recolored by setting <see cref="Shape.Fill"/>.
/// </para>
/// <para>
/// Geometry comes from <see cref="MaterialShapeGeometries"/>, which keeps the source path data
/// verbatim. The control is decorative: it takes no focus and exposes no automation name of its
/// own, so an interactive host should wrap it in a control that supplies both.
/// </para>
/// </remarks>
public sealed class MaterialShape : Shape
{
    /// <summary>Defines the <see cref="Kind"/> property.</summary>
    public static readonly StyledProperty<MaterialShapeKind> KindProperty =
        AvaloniaProperty.Register<MaterialShape, MaterialShapeKind>(nameof(Kind), MaterialShapeKind.Circle);

    static MaterialShape()
    {
        AffectsGeometry<MaterialShape>(KindProperty);
    }

    /// <summary>Gets or sets which source geometry is drawn.</summary>
    public MaterialShapeKind Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    /// <inheritdoc />
    protected override Geometry? CreateDefiningGeometry() => MaterialShapeGeometries.Get(Kind);
}
