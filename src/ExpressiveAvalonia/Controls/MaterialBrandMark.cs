using System;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace ExpressiveAvalonia.Controls;

/// <summary>The two logo building blocks of the design source.</summary>
/// <remarks>
/// <c>.Building Blocks/Colourful logo</c> (local-000217) is the 18 x 18 rainbow ring;
/// <c>.Building Blocks/Favicon</c> (local-000257) is the 18 x 18 circle with the exported symbol.
/// </remarks>
public enum MaterialBrandMarkKind
{
    /// <summary>18 dp rainbow ring drawn from the source band geometry.</summary>
    ColourfulLogo,

    /// <summary>18 dp circular favicon: an on-surface disc with the exported symbol.</summary>
    Favicon,
}

/// <summary>A small brand mark: the source colourful logo or a favicon.</summary>
/// <remarks>
/// <para>
/// The marks are drawn in the source's 18 x 18 space and scaled uniformly to <see cref="Size"/>.
/// The colourful logo keeps the six exported band geometries and their literal brand colors,
/// clipped to the ring the source mask produces: outer radius 9, inner radius 4. The favicon fills
/// a 9 dp circle with <see cref="TemplatedControl.Background"/> (on-surface in the source) and draws
/// the exported symbol image into <see cref="SymbolImageRect"/> inside <see cref="SymbolRect"/>.
/// </para>
/// <para>
/// Brand marks are decorative: they take no focus, expose no activation and report the Image
/// automation type. Give the mark an accessible name with <c>AutomationProperties.Name</c> when it
/// is the only content of a link or button.
/// </para>
/// </remarks>
public sealed class MaterialBrandMark : TemplatedControl
{
    /// <summary>The source component box. All geometry below is expressed in this space.</summary>
    public const double SourceSize = 18;

    /// <summary>Outer radius of the colourful-logo ring in the source space.</summary>
    public const double LogoOuterRadius = 9;

    /// <summary>Inner radius of the colourful-logo ring, produced by the source mask's 10 dp stroke.</summary>
    public const double LogoInnerRadius = 4;

    /// <summary>Defines the <see cref="Kind"/> property.</summary>
    public static readonly StyledProperty<MaterialBrandMarkKind> KindProperty =
        AvaloniaProperty.Register<MaterialBrandMark, MaterialBrandMarkKind>(nameof(Kind));

    /// <summary>Defines the <see cref="Size"/> property.</summary>
    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<MaterialBrandMark, double>(nameof(Size), SourceSize,
            validate: value => double.IsFinite(value) && value > 0);

    /// <summary>Defines the <see cref="Source"/> property.</summary>
    public static readonly StyledProperty<IImage?> SourceProperty =
        AvaloniaProperty.Register<MaterialBrandMark, IImage?>(nameof(Source));

    /// <summary>Defines the <see cref="SymbolRect"/> property.</summary>
    public static readonly StyledProperty<Rect> SymbolRectProperty =
        AvaloniaProperty.Register<MaterialBrandMark, Rect>(nameof(SymbolRect), new Rect(3, 2, 12, 14));

    /// <summary>Defines the <see cref="SymbolImageRect"/> property.</summary>
    public static readonly StyledProperty<Rect> SymbolImageRectProperty =
        AvaloniaProperty.Register<MaterialBrandMark, Rect>(
            nameof(SymbolImageRect), DefaultSymbolImageRect);

    // Source band geometry, verbatim from local-000217 in the order the export paints them.
    private static readonly (Geometry Geometry, IBrush Brush)[] s_bands =
    [
        (Parse("M0 12L9.50002 10.5L18 11.5L18 15L9.00002 17L0 15V12Z"), Brush("#F5A61D")),
        (Parse("M3.79704e-07 2.99998C3.79704e-07 2.99998 5.00002 4.00006 8.50002 2.50002" +
               "C12 0.999972 18 2.99998 18 2.99998V5.99998C18 5.99998 12.5 7.50004 9 5.99998" +
               "C5.5 4.49992 4.5 7.00004 3.79704e-07 5.99998C-4.7463e-07 5.00004 3.79704e-07 2.99998 3.79704e-07 2.99998Z"),
            Brush("#8F3A98")),
        (Parse("M1.56983e-07 5.99997C7 3.99998 18 5.99997 18 5.99997V8.99997C18 8.99997 12.5 10.5 9 8.99997" +
               "C5.5 7.49993 1.56983e-07 8.99997 1.56983e-07 8.99997C1.56983e-07 8.99997 -1.96229e-07 6.99992 1.56983e-07 5.99997Z"),
            Brush("#E6359F")),
        (Parse("M0 8.99992C0.736915 7.28046 7.67312 5.32998 9.33657 7.66492C11 9.99986 18 8.99992 18 8.99992" +
               "C18 8.99992 18 10.9999 18 11.9999C11.9845 10.6631 7.50001 10.4999 5.76495 11.5589" +
               "C4.02989 12.618 2.33101 11.9999 0 11.9999C0 11.9999 1.52588e-05 9.99988 0 8.99992Z"),
            Brush("#F15E2B")),
        (Parse("M0 15C1.11719 11.4622 7.50002 16.5 9.50002 15C11.5 13.5 18 15 18 15V18H0C0 18 1.49541e-05 16 0 15Z"),
            Brush("#FFDC5F")),
        (Parse("M0 0H18V3C18 3 9.5 2.5 7 3.5C4.5 4.5 0 3 0 3V0Z"), Brush("#09ADEA")),
    ];

    private static readonly Lazy<Geometry> s_ring = new(CreateRing);

    static MaterialBrandMark()
    {
        AffectsMeasure<MaterialBrandMark>(SizeProperty);
        AffectsRender<MaterialBrandMark>(KindProperty, TemplatedControl.BackgroundProperty, SourceProperty,
            SymbolRectProperty, SymbolImageRectProperty);
    }

    /// <summary>Which building block is drawn.</summary>
    public MaterialBrandMarkKind Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    /// <summary>Edge length in device-independent units. The source component is 18.</summary>
    public double Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    /// <summary>
    /// Favicon symbol override. When null the exported symbol
    /// (<see cref="MaterialAvatarAssets.FaviconSymbol"/>) is drawn.
    /// </summary>
    public IImage? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>Destination rectangle of the favicon symbol in the 18 x 18 source space.</summary>
    public Rect SymbolRect
    {
        get => GetValue(SymbolRectProperty);
        set => SetValue(SymbolRectProperty, value);
    }

    /// <summary>
    /// Destination of the symbol image inside <see cref="SymbolRect"/>, in the 18 x 18 source
    /// space. The source places the exported image with the transform
    /// <c>matrix(0.908678 0 0 0.583333 0.054086 0.204778)</c> relative to the 12 x 14 symbol
    /// rectangle, which is what <see cref="DefaultSymbolImageRect"/> encodes.
    /// </summary>
    public Rect SymbolImageRect
    {
        get => GetValue(SymbolImageRectProperty);
        set => SetValue(SymbolImageRectProperty, value);
    }

    /// <summary>The source image placement: the whole image squeezed into the 12 x 14 symbol slot.</summary>
    public static Rect DefaultSymbolImageRect
    {
        get
        {
            const double left = 3, top = 2, width = 12, height = 14;
            return new Rect(
                left + width * 0.054086,
                top + height * 0.204778,
                width * 0.908678,
                height * 0.583333);
        }
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        base.MeasureOverride(new Size(Size, Size));
        return new Size(Size, Size);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new BrandMarkPeer(this);

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var scale = Math.Min(Bounds.Width, Bounds.Height) / SourceSize;
        if (!(scale > 0))
            return;

        using (context.PushTransform(
                   Matrix.CreateScale(scale, scale) *
                   Matrix.CreateTranslation((Bounds.Width - SourceSize * scale) / 2,
                       (Bounds.Height - SourceSize * scale) / 2)))
        {
            if (Kind == MaterialBrandMarkKind.Favicon)
                DrawFavicon(context);
            else
                DrawColourfulLogo(context);
        }
    }

    private void DrawFavicon(DrawingContext context)
    {
        if (Background is { } background)
            context.DrawEllipse(background, null, new Point(9, 9), 9, 9);

        var symbol = Source ?? MaterialAvatarAssets.FaviconSymbol;
        context.DrawImage(symbol, SymbolImageRect);
    }

    private static void DrawColourfulLogo(DrawingContext context)
    {
        using (context.PushGeometryClip(s_ring.Value))
        {
            foreach (var (geometry, brush) in s_bands)
                context.DrawGeometry(brush, null, geometry);
        }
    }

    // The source masks the bands with a 10 dp stroke on the 9 dp circle, leaving a 4 dp hole.
    // Drawing the ring as an even-odd clip reproduces that mask without a bitmap mask.
    private static Geometry CreateRing()
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            // The winding rule must be set before the first figure is begun.
            ctx.SetFillRule(FillRule.EvenOdd);
            const double outer = LogoOuterRadius;
            const double inner = LogoInnerRadius;
            var center = new Point(outer, outer);

            ctx.BeginFigure(new Point(center.X, center.Y - outer), true);
            ctx.ArcTo(new Point(center.X + outer, center.Y), new Size(outer, outer), 0, false, SweepDirection.Clockwise);
            ctx.ArcTo(new Point(center.X, center.Y + outer), new Size(outer, outer), 0, false, SweepDirection.Clockwise);
            ctx.ArcTo(new Point(center.X - outer, center.Y), new Size(outer, outer), 0, false, SweepDirection.Clockwise);
            ctx.ArcTo(new Point(center.X, center.Y - outer), new Size(outer, outer), 0, false, SweepDirection.Clockwise);
            ctx.EndFigure(true);

            ctx.BeginFigure(new Point(center.X + inner, center.Y), true);
            ctx.ArcTo(new Point(center.X, center.Y + inner), new Size(inner, inner), 0, false, SweepDirection.CounterClockwise);
            ctx.ArcTo(new Point(center.X - inner, center.Y), new Size(inner, inner), 0, false, SweepDirection.CounterClockwise);
            ctx.ArcTo(new Point(center.X, center.Y - inner), new Size(inner, inner), 0, false, SweepDirection.CounterClockwise);
            ctx.ArcTo(new Point(center.X + inner, center.Y), new Size(inner, inner), 0, false, SweepDirection.CounterClockwise);
            ctx.EndFigure(true);
        }
        return geometry;
    }

    private static Geometry Parse(string data) => Geometry.Parse(data);

    private static IBrush Brush(string color) =>
        new Avalonia.Media.Immutable.ImmutableSolidColorBrush(Color.Parse(color));

    private sealed class BrandMarkPeer(MaterialBrandMark owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;

        protected override string GetClassNameCore() => nameof(MaterialBrandMark);
    }
}
