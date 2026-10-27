using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace ExpressiveAvalonia.Controls;

/// <summary>The source navigation bar family.</summary>
public enum MaterialNavigationBarKind
{
    /// <summary>Classic <c>NavigationBar</c>: 80 DIP tall, 8 DIP between equally weighted items.</summary>
    Classic,

    /// <summary>Expressive <c>ShortNavigationBar</c>: 64 DIP tall, equal-weight or centered items.</summary>
    Short
}

/// <summary>The two source <c>ShortNavigationBarArrangement</c> values.</summary>
public enum MaterialShortNavigationBarArrangement
{
    /// <summary>Items share the full bar width equally (small screens).</summary>
    EqualWeight,

    /// <summary>Items group in the center with the source percentage padding (medium screens).</summary>
    Centered
}

/// <summary>
/// Arranges navigation bar destinations with the pinned source measure policies.
/// </summary>
/// <remarks>
/// Classic bars use <c>Row(Arrangement.spacedBy(8.dp))</c> with weighted items
/// (<c>NavigationBar.kt</c>); the short bar uses <c>EqualWeightContentMeasurePolicy</c> or
/// <c>CenteredContentMeasurePolicy</c> from <c>ShortNavigationBar.kt</c>, including the
/// 20/15/10/5 percent side padding for three to six centered items. Vertical orientation is a
/// desktop extension and divides the available height instead of the width.
/// </remarks>
public sealed class MaterialNavigationBarPanel : Panel
{
    public static readonly StyledProperty<MaterialNavigationBarKind> KindProperty =
        AvaloniaProperty.Register<MaterialNavigationBarPanel, MaterialNavigationBarKind>(nameof(Kind));

    public static readonly StyledProperty<MaterialShortNavigationBarArrangement> ArrangementProperty =
        AvaloniaProperty.Register<MaterialNavigationBarPanel, MaterialShortNavigationBarArrangement>(
            nameof(Arrangement));

    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<MaterialNavigationBarPanel, Orientation>(nameof(Orientation));

    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<MaterialNavigationBarPanel, double>(nameof(Spacing), 8d);

    private double[]? _widths;
    private double _barPadding;

    static MaterialNavigationBarPanel()
    {
        AffectsMeasure<MaterialNavigationBarPanel>(KindProperty, ArrangementProperty, OrientationProperty, SpacingProperty);
        AffectsArrange<MaterialNavigationBarPanel>(KindProperty, ArrangementProperty, OrientationProperty, SpacingProperty);
    }

    public MaterialNavigationBarKind Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public MaterialShortNavigationBarArrangement Arrangement
    {
        get => GetValue(ArrangementProperty);
        set => SetValue(ArrangementProperty, value);
    }

    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var count = Children.Count;
        if (count == 0)
            return default;

        if (Orientation == Avalonia.Layout.Orientation.Vertical)
        {
            var boundedHeight = double.IsFinite(availableSize.Height);
            var gap = Kind == MaterialNavigationBarKind.Classic ? Spacing : 0;
            var height = boundedHeight ? Math.Max(0, (availableSize.Height - (gap * (count - 1))) / count) : double.PositiveInfinity;
            var maxWidth = 0d;
            foreach (var child in Children)
            {
                child.Measure(new Size(availableSize.Width, height));
                maxWidth = Math.Max(maxWidth, child.DesiredSize.Width);
            }
            var desiredHeight = boundedHeight ? availableSize.Height : (height * count) + (gap * (count - 1));
            return new Size(double.IsFinite(availableSize.Width) ? availableSize.Width : maxWidth, desiredHeight);
        }

        if (Kind == MaterialNavigationBarKind.Classic)
        {
            var bounded = double.IsFinite(availableSize.Width);
            var gap = Spacing;
            var itemWidth = bounded ? Math.Max(0, (availableSize.Width - (gap * (count - 1))) / count) : double.PositiveInfinity;
            var height = 0d;
            foreach (var child in Children)
            {
                child.Measure(new Size(itemWidth, availableSize.Height));
                height = Math.Max(height, child.DesiredSize.Height);
            }
            _widths = null;
            return new Size(bounded ? availableSize.Width : (itemWidth * count), height);
        }

        if (Arrangement == MaterialShortNavigationBarArrangement.EqualWeight)
        {
            var bounded = double.IsFinite(availableSize.Width);
            var itemWidth = bounded ? Math.Max(0, availableSize.Width / count) : double.PositiveInfinity;
            var height = 0d;
            foreach (var child in Children)
            {
                child.Measure(new Size(itemWidth, availableSize.Height));
                height = Math.Max(height, child.DesiredSize.Height);
            }
            _widths = null;
            return new Size(bounded ? availableSize.Width : itemWidth * count, height);
        }

        // CenteredContentMeasurePolicy: group the items with the source side padding and let an
        // item grow up to the equal-weight width when its intrinsic label needs more room.
        if (!double.IsFinite(availableSize.Width))
        {
            var height = 0d;
            var widths = new double[count];
            for (var i = 0; i < count; i++)
            {
                Children[i].Measure(new Size(double.PositiveInfinity, availableSize.Height));
                widths[i] = Children[i].DesiredSize.Width;
                height = Math.Max(height, Children[i].DesiredSize.Height);
            }
            _widths = widths;
            _barPadding = 0;
            return new Size(Sum(widths), height);
        }

        var barWidth = availableSize.Width;
        var padding = CenteredPadding(count, barWidth);
        var itemMinWidth = Math.Max(0, (barWidth - (padding * 2)) / count);
        var itemMaxWidth = barWidth / count;
        var itemWidths = new double[count];
        var measuredHeight = 0d;
        for (var i = 0; i < count; i++)
        {
            var child = Children[i];
            child.Measure(new Size(itemMaxWidth, availableSize.Height));
            var currentWidth = Math.Max(itemMinWidth, Math.Min(itemMaxWidth, child.DesiredSize.Width));
            itemWidths[i] = currentWidth;
            padding -= (currentWidth - itemMinWidth) / 2;
            measuredHeight = Math.Max(measuredHeight, child.DesiredSize.Height);
        }

        _widths = itemWidths;
        _barPadding = Math.Max(0, padding);
        return new Size(barWidth, measuredHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var count = Children.Count;
        if (count == 0)
            return finalSize;

        var mirrored = FlowDirection == Avalonia.Media.FlowDirection.RightToLeft;
        if (Orientation == Avalonia.Layout.Orientation.Vertical)
        {
            var gap = Kind == MaterialNavigationBarKind.Classic ? Spacing : 0;
            var height = Math.Max(0, (finalSize.Height - (gap * (count - 1))) / count);
            var y = 0d;
            foreach (var child in Children)
            {
                child.Arrange(new Rect(0, y, finalSize.Width, height));
                y += height + gap;
            }
            return finalSize;
        }

        if (Kind == MaterialNavigationBarKind.Classic)
        {
            var gap = Spacing;
            var width = Math.Max(0, (finalSize.Width - (gap * (count - 1))) / count);
            var x = 0d;
            foreach (var child in Children)
            {
                Arrange(child, x, 0, width, finalSize.Height, mirrored, finalSize.Width);
                x += width + gap;
            }
            return finalSize;
        }

        if (Arrangement == MaterialShortNavigationBarArrangement.EqualWeight)
        {
            var width = Math.Max(0, finalSize.Width / count);
            var x = 0d;
            foreach (var child in Children)
            {
                Arrange(child, x, 0, width, finalSize.Height, mirrored, finalSize.Width);
                x += width;
            }
            return finalSize;
        }

        var widths = _widths;
        if (widths is null || widths.Length != count)
        {
            widths = new double[count];
            var itemWidth = Math.Max(0, finalSize.Width / count);
            for (var i = 0; i < count; i++)
                widths[i] = itemWidth;
        }

        var cursor = _barPadding;
        for (var i = 0; i < count; i++)
        {
            Arrange(Children[i], cursor, 0, widths[i], finalSize.Height, mirrored, finalSize.Width);
            cursor += widths[i];
        }
        return finalSize;
    }

    private static void Arrange(Control child, double x, double y, double width, double height,
        bool mirrored, double finalWidth)
    {
        var physicalX = mirrored ? Math.Max(0, finalWidth - x - width) : x;
        child.Arrange(new Rect(physicalX, y, Math.Max(0, width), Math.Max(0, height)));
    }

    private static double CenteredPadding(int itemsCount, double barWidth)
    {
        if (itemsCount > 6)
            return 0;
        // Source formula: ((100 - 10 * (itemsCount + 3)) / 2f) / 100 * barWidth.
        var percentage = ((100 - (10 * (itemsCount + 3))) / 2d) / 100d;
        return percentage * barWidth;
    }

    private static double Sum(double[] values)
    {
        var result = 0d;
        foreach (var value in values)
            result += value;
        return result;
    }
}