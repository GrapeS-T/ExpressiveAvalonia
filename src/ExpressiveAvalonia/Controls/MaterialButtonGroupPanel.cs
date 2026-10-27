using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// Lays out Material button group items with the source's spacing rules and press expansion.
/// </summary>
/// <remarks>
/// <para>
/// Horizontal layout reproduces the AndroidX <c>ButtonGroup</c> measure policy: every item is
/// measured at its intrinsic width, an item with a press-expansion progress grows by the source
/// <c>expandedRatio</c> of its width and compresses its neighbours, bounded both by the ratio and by
/// the neighbours' content padding (their <c>compressionLimit</c>). Growth is taken from the
/// neighbours, so the group's total width does not change while pressing.
/// </para>
/// <para>
/// Segmented groups equalise item widths, matching the row's <c>Modifier.weight(1f)</c> children.
/// Negative spacing reproduces the 1-DIP outline overlap of the segmented row. Right-to-left
/// layout mirrors the physical placement while the item order stays logical.
/// </para>
/// <para>
/// Vertical groups stack items with the same spacing. The source button group is horizontal only;
/// the vertical arrangement is an Avalonia extension and therefore does not expand on press.
/// </para>
/// </remarks>
public sealed class MaterialButtonGroupPanel : Panel
{
    /// <summary>Defines the <see cref="Orientation"/> property.</summary>
    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<MaterialButtonGroupPanel, Orientation>(nameof(Orientation));

    /// <summary>Defines the <see cref="Spacing"/> property.</summary>
    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<MaterialButtonGroupPanel, double>(nameof(Spacing));

    /// <summary>Defines the <see cref="ExpandedRatio"/> property.</summary>
    public static readonly StyledProperty<double> ExpandedRatioProperty =
        AvaloniaProperty.Register<MaterialButtonGroupPanel, double>(nameof(ExpandedRatio), 0.15);

    /// <summary>Defines the <see cref="EqualizeWidths"/> property.</summary>
    public static readonly StyledProperty<bool> EqualizeWidthsProperty =
        AvaloniaProperty.Register<MaterialButtonGroupPanel, bool>(nameof(EqualizeWidths));

    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>The gap between items. Negative values overlap the neighbouring outlines.</summary>
    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    /// <summary>The source <c>ButtonGroupDefaults.ExpandedRatio</c>.</summary>
    public double ExpandedRatio
    {
        get => GetValue(ExpandedRatioProperty);
        set => SetValue(ExpandedRatioProperty, value);
    }

    /// <summary>Equalises every item to the widest intrinsic width (segmented rows).</summary>
    public bool EqualizeWidths
    {
        get => GetValue(EqualizeWidthsProperty);
        set => SetValue(EqualizeWidthsProperty, value);
    }

    static MaterialButtonGroupPanel()
    {
        AffectsMeasure<MaterialButtonGroupPanel>(OrientationProperty, SpacingProperty,
            EqualizeWidthsProperty);
        AffectsArrange<MaterialButtonGroupPanel>(ExpandedRatioProperty);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var horizontal = Orientation == Orientation.Horizontal;
        var spacing = Spacing;
        var count = 0;
        var total = 0d;
        var maxWidth = 0d;
        var maxHeight = 0d;

        foreach (var child in Children)
        {
            if (!child.IsVisible)
                continue;
            var constraint = horizontal
                ? new Size(double.PositiveInfinity, availableSize.Height)
                : new Size(availableSize.Width, double.PositiveInfinity);
            child.Measure(constraint);
            var size = child.DesiredSize;
            maxWidth = Math.Max(maxWidth, size.Width);
            maxHeight = Math.Max(maxHeight, size.Height);
            total += horizontal ? size.Width : size.Height;
            count++;
        }

        if (count == 0)
            return default;

        if (!horizontal)
            return new Size(maxWidth, total + spacing * (count - 1));

        if (EqualizeWidths)
        {
            var equal = maxWidth;
            foreach (var child in Children)
            {
                if (!child.IsVisible)
                    continue;
                child.Measure(new Size(equal, availableSize.Height));
            }
            total = equal * count;
            maxWidth = equal;
        }

        total += spacing * (count - 1);
        return new Size(Math.Max(0, total), maxHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var horizontal = Orientation == Orientation.Horizontal;
        var spacing = Spacing;
        var children = new List<Control>();
        foreach (var child in Children)
        {
            if (child.IsVisible)
                children.Add(child);
        }

        if (children.Count == 0)
            return finalSize;

        if (!horizontal)
        {
            var y = 0d;
            foreach (var child in children)
            {
                child.Arrange(new Rect(0, y, finalSize.Width, child.DesiredSize.Height));
                y += child.DesiredSize.Height + spacing;
            }
            return finalSize;
        }

        var widths = new double[children.Count];
        for (var i = 0; i < children.Count; i++)
            widths[i] = children[i].DesiredSize.Width;

        if (EqualizeWidths)
        {
            var equal = 0d;
            foreach (var width in widths)
                equal = Math.Max(equal, width);
            for (var i = 0; i < widths.Length; i++)
                widths[i] = equal;
        }

        ApplyPressExpansion(children, widths);

        var positions = new double[children.Count];
        var x = 0d;
        for (var i = 0; i < children.Count; i++)
        {
            positions[i] = x;
            x += widths[i] + spacing;
        }

        var totalWidth = Math.Max(0, x - (children.Count > 0 ? spacing : 0));
        var rtl = FlowDirection == FlowDirection.RightToLeft;
        for (var i = 0; i < children.Count; i++)
        {
            var left = rtl ? totalWidth - positions[i] - widths[i] : positions[i];
            children[i].Arrange(new Rect(left, 0, widths[i], finalSize.Height));
        }

        return finalSize;
    }

    // Source measure policy: a pressed item grows by expandedRatio of its width and that growth is
    // taken out of its horizontal neighbours, bounded by each neighbour's content padding
    // (compressionLimit). Middle items compress both sides; edges only their inner neighbour.
    private void ApplyPressExpansion(List<Control> children, double[] widths)
    {
        if (children.Count < 2)
            return;

        var last = children.Count - 1;
        for (var i = 0; i <= last; i++)
        {
            if (children[i] is not MaterialButtonGroupItem item || item.PressExpansion == 0)
                continue;

            var progress = item.PressExpansion;
            if (i > 0 && i < last)
            {
                var limitPrevious = ((MaterialButtonGroupItem)children[i - 1]).CompressionLimit;
                var limitNext = ((MaterialButtonGroupItem)children[i + 1]).CompressionLimit;
                var growth = Math.Floor(progress * Math.Min(ExpandedRatio * widths[i] / 2,
                    Math.Min(limitPrevious, limitNext)) + 0.5);
                var growthLeft = Math.Min(growth, widths[i - 1]);
                var growthRight = Math.Min(growth, widths[i + 1]);
                widths[i - 1] -= growthLeft;
                widths[i + 1] -= growthRight;
                widths[i] += growthLeft + growthRight;
            }
            else if (i == 0)
            {
                var limitNext = ((MaterialButtonGroupItem)children[1]).CompressionLimit;
                var growth = Math.Floor(progress * Math.Min(ExpandedRatio * widths[i], limitNext) + 0.5);
                var growthRight = Math.Min(growth, widths[1]);
                widths[1] -= growthRight;
                widths[0] += growthRight;
            }
            else
            {
                var limitPrevious = ((MaterialButtonGroupItem)children[i - 1]).CompressionLimit;
                var growth = Math.Floor(progress * Math.Min(ExpandedRatio * widths[i], limitPrevious) + 0.5);
                var growthLeft = Math.Min(growth, widths[i - 1]);
                widths[i - 1] -= growthLeft;
                widths[i] += growthLeft;
            }
        }
    }
}