using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace ExpressiveAvalonia.Theming;

/// <summary>
/// Port of the Material optical horizontal centering rule for asymmetric corner shapes.
/// </summary>
/// <remarks>
/// <para>
/// The source modifier adds <c>0.11 * (averageStartRadius - averageEndRadius)</c> to the start
/// padding of its content, clamped so the correction never eats more than the caller's start or
/// end padding. The correction is applied with <c>placeRelative</c>, so it mirrors in right-to-left
/// layouts. The coefficient and clamp semantics are preserved exactly.
/// </para>
/// <para>
/// Use <see cref="ComputeOffset"/> when a template arranges its own content. Otherwise wrap the
/// padded content in a <see cref="MaterialOpticalCenterDecorator"/>; unlike padding, the decorator
/// shifts the child without changing the measured size, which is what the source modifier does.
/// </para>
/// </remarks>
public static class MaterialOpticalCenter
{
    /// <summary>The source <c>CenterOpticallyCoefficient</c>.</summary>
    public const double Coefficient = 0.11;

    /// <summary>
    /// Computes the local horizontal offset for <paramref name="size"/> and its logical corner radii.
    /// </summary>
    /// <param name="size">The bounds of the shaped surface, not of the content.</param>
    /// <param name="corners">
    /// Logical start/end corner radii (<see cref="CornerRadius.TopLeft"/>/<see cref="CornerRadius.BottomLeft"/>
    /// for start, <see cref="CornerRadius.TopRight"/>/<see cref="CornerRadius.BottomRight"/> for end).
    /// </param>
    /// <param name="flowDirection">The layout flow direction; Avalonia's visual mirror transform reflects RTL once.</param>
    /// <param name="maxStartOffset">Maximum correction toward the start, in DIPs; corresponds to the caller's start padding.</param>
    /// <param name="maxEndOffset">Maximum correction toward the end, in DIPs; corresponds to the caller's end padding.</param>
    /// <returns>The local horizontal offset in DIPs, clamped to the start/end padding bounds.</returns>
    public static double ComputeOffset(
        Size size,
        CornerRadius corners,
        FlowDirection flowDirection,
        double maxStartOffset = 0,
        double maxEndOffset = 0)
    {
        _ = flowDirection;
        ArgumentOutOfRangeException.ThrowIfNegative(maxStartOffset);
        ArgumentOutOfRangeException.ThrowIfNegative(maxEndOffset);
        if (!double.IsFinite(maxStartOffset) || !double.IsFinite(maxEndOffset))
            throw new ArgumentOutOfRangeException(nameof(maxStartOffset));
        if (!double.IsFinite(size.Width) || !double.IsFinite(size.Height) || size.Width < 0 || size.Height < 0)
            return 0;

        // Avalonia's Visual.HasMirrorTransform reflects both Border.CornerRadius rendering and
        // Decorator.ArrangeOverride X coordinates once at the RTL boundary, so start/end corners
        // and the arrange offset stay in local start-to-end coordinates.
        var averageStart = (corners.TopLeft + corners.BottomLeft) / 2;
        var averageEnd = (corners.TopRight + corners.BottomRight) / 2;
        var correction = Coefficient * (averageStart - averageEnd);
        return Math.Clamp(correction, -maxStartOffset, maxEndOffset);
    }
}

/// <summary>
/// Arranges a single child with the Material optical horizontal centering correction.
/// </summary>
/// <remarks>
/// Bind <see cref="CornerRadius"/> to the shaped surface's live radius (including an animated
/// radius) and <see cref="MaxStartOffset"/>/<see cref="MaxEndOffset"/> to the content's start and
/// end padding. The child keeps its measured size; only its arrange origin moves.
/// </remarks>
public sealed class MaterialOpticalCenterDecorator : Decorator
{
    /// <summary>Defines the <see cref="CornerRadius"/> property.</summary>
    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<MaterialOpticalCenterDecorator, CornerRadius>(nameof(CornerRadius));

    /// <summary>Defines the <see cref="MaxStartOffset"/> property.</summary>
    public static readonly StyledProperty<double> MaxStartOffsetProperty =
        AvaloniaProperty.Register<MaterialOpticalCenterDecorator, double>(
            nameof(MaxStartOffset), validate: static value => double.IsFinite(value) && value >= 0);

    /// <summary>Defines the <see cref="MaxEndOffset"/> property.</summary>
    public static readonly StyledProperty<double> MaxEndOffsetProperty =
        AvaloniaProperty.Register<MaterialOpticalCenterDecorator, double>(
            nameof(MaxEndOffset), validate: static value => double.IsFinite(value) && value >= 0);

    static MaterialOpticalCenterDecorator()
    {
        AffectsArrange<MaterialOpticalCenterDecorator>(
            CornerRadiusProperty, MaxStartOffsetProperty, MaxEndOffsetProperty);
    }

    /// <summary>Gets or sets the physical corner radii of the shaped surface.</summary>
    public CornerRadius CornerRadius
    {
        get => GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    /// <summary>Gets or sets the maximum correction toward the end, normally the start padding.</summary>
    public double MaxStartOffset
    {
        get => GetValue(MaxStartOffsetProperty);
        set => SetValue(MaxStartOffsetProperty, value);
    }

    /// <summary>Gets or sets the maximum correction toward the start, normally the end padding.</summary>
    public double MaxEndOffset
    {
        get => GetValue(MaxEndOffsetProperty);
        set => SetValue(MaxEndOffsetProperty, value);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var child = Child;
        if (child is null)
            return finalSize;

        var offset = MaterialOpticalCenter.ComputeOffset(
            finalSize, CornerRadius, FlowDirection, MaxStartOffset, MaxEndOffset);
        child.Arrange(new Rect(offset, 0, finalSize.Width, finalSize.Height));
        return finalSize;
    }
}