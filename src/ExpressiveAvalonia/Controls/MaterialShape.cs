using Avalonia;
using Avalonia.Controls.Shapes;
using Avalonia.Media;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// A decorative Material Expressive shape drawn from the source shape catalog.
/// </summary>
/// <remarks>
/// <para>
/// The control derives from <see cref="Shape"/>, so <see cref="Shape.Fill"/>,
/// <see cref="Shape.Stroke"/>, <see cref="Shape.StrokeThickness"/> and <see cref="Shape.Stretch"/>
/// behave exactly like every other Avalonia shape. The theme sets
/// <see cref="Shape.Stretch"/> to <see cref="Stretch.Uniform"/> and
/// <see cref="Shape.Fill"/> to the primary role, matching the source previews, which paint the
/// named catalog shapes with <c>M3/sys/light/primary</c>; the nine numbered extension shapes use
/// the primary container role and can be recolored by setting <see cref="Shape.Fill"/>.
/// </para>
/// <para>
/// Geometry comes from <see cref="MaterialShapeGeometries"/>. Catalog shapes are generated from
/// the ported <c>MaterialShapes</c> polygons, so the control and the loading indicator share one
/// geometry pipeline. Geometries are cached per kind and start angle and must not be mutated.
/// The control is decorative: it takes no focus and exposes no automation name of its own, so an
/// interactive host should wrap it in a control that supplies both.
/// </para>
/// </remarks>
public sealed class MaterialShape : Shape
{
    /// <summary>Defines the <see cref="Kind"/> property.</summary>
    public static readonly StyledProperty<MaterialShapeKind> KindProperty =
        AvaloniaProperty.Register<MaterialShape, MaterialShapeKind>(nameof(Kind), MaterialShapeKind.Circle);

    /// <summary>Defines the <see cref="StartAngle"/> property.</summary>
    public static readonly StyledProperty<int> StartAngleProperty =
        AvaloniaProperty.Register<MaterialShape, int>(nameof(StartAngle));

    static MaterialShape()
    {
        AffectsGeometry<MaterialShape>(KindProperty, StartAngleProperty);
    }

    /// <summary>Gets or sets which source geometry is drawn.</summary>
    public MaterialShapeKind Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    /// <summary>
    /// Gets or sets the angle, in degrees, from which the generated path starts drawing. Zero
    /// starts at the path's first anchor. Custom extension shapes ignore the angle because they
    /// keep their verbatim path data.
    /// </summary>
    public int StartAngle
    {
        get => GetValue(StartAngleProperty);
        set => SetValue(StartAngleProperty, value);
    }

    /// <inheritdoc />
    protected override Geometry? CreateDefiningGeometry() =>
        MaterialShapeGeometries.Get(Kind, StartAngle);
}
