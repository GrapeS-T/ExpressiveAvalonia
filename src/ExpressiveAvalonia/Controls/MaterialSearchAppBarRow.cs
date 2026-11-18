using System;
using Avalonia;
using Avalonia.Controls;

namespace ExpressiveAvalonia.Controls;

/// <summary>Measures leading, trailing, managed actions and the responsive search field.</summary>
public sealed class MaterialSearchAppBarRow : Panel
{
    private const double MinFieldWidth = 312;
    private const double DefaultFieldWidth = 360;
    private const double MaxFieldWidth = 720;
    private const double FieldHorizontalMargins = 16;

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count < 4)
            return default;
        var leading = Children[0];
        var field = Children[1];
        var actions = Children[2];
        var trailing = Children[3];
        leading.Measure(availableSize);
        var remaining = double.IsFinite(availableSize.Width)
            ? Math.Max(0, availableSize.Width - leading.DesiredSize.Width)
            : double.PositiveInfinity;
        trailing.Measure(new Size(remaining, availableSize.Height));
        remaining = Math.Max(0, remaining - trailing.DesiredSize.Width);
        var actionWidth = double.IsFinite(remaining)
            ? Math.Max(0, remaining - Math.Min(MinFieldWidth + FieldHorizontalMargins, Math.Max(0, remaining - 48)))
            : double.PositiveInfinity;
        actions.Measure(new Size(actionWidth, availableSize.Height));
        remaining = Math.Max(0, remaining - actions.DesiredSize.Width);
        var fieldWidth = double.IsFinite(remaining)
            ? ResolveFieldSlotWidth(remaining)
            : DefaultFieldWidth + FieldHorizontalMargins;
        field.Measure(new Size(fieldWidth, availableSize.Height));
        return new Size(double.IsFinite(availableSize.Width) ? availableSize.Width :
            leading.DesiredSize.Width + field.DesiredSize.Width + actions.DesiredSize.Width + trailing.DesiredSize.Width,
            Math.Max(Math.Max(field.DesiredSize.Height, actions.DesiredSize.Height), Math.Max(leading.DesiredSize.Height, trailing.DesiredSize.Height)));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count < 4)
            return finalSize;
        var leading = Children[0];
        var field = Children[1];
        var actions = Children[2];
        var trailing = Children[3];
        var leadWidth = Math.Min(finalSize.Width, leading.DesiredSize.Width);
        var trailWidth = Math.Min(Math.Max(0, finalSize.Width - leadWidth), trailing.DesiredSize.Width);
        var actionWidth = Math.Min(Math.Max(0, finalSize.Width - leadWidth - trailWidth), actions.DesiredSize.Width);
        var available = Math.Max(0, finalSize.Width - leadWidth - trailWidth - actionWidth);
        var fieldWidth = ResolveFieldSlotWidth(available);
        leading.Arrange(new Rect(0, 0, leadWidth, finalSize.Height));
        trailing.Arrange(new Rect(finalSize.Width - trailWidth, 0, trailWidth, finalSize.Height));
        actions.Arrange(new Rect(finalSize.Width - trailWidth - actionWidth, 0, actionWidth, finalSize.Height));
        field.Arrange(new Rect(leadWidth + (available - fieldWidth) / 2, 0, fieldWidth, finalSize.Height));
        return finalSize;
    }

    private static double ResolveFieldSlotWidth(double available) =>
        Math.Min(available,
            Math.Clamp(Math.Max(0, available - FieldHorizontalMargins) / 2, MinFieldWidth, MaxFieldWidth) +
            FieldHorizontalMargins);
}
