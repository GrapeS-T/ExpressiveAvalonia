using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace ExpressiveAvalonia.Controls;

/// <summary>Measures leading, trailing, managed actions and the responsive search field.</summary>
public sealed class MaterialSearchAppBarRow : Panel
{
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
            ? Math.Max(0, remaining - Math.Min(328, Math.Max(0, remaining - 48)))
            : double.PositiveInfinity;
        actions.Measure(new Size(actionWidth, availableSize.Height));
        remaining = Math.Max(0, remaining - actions.DesiredSize.Width);
        var fieldWidth = double.IsFinite(remaining)
            ? Math.Min(remaining, Math.Max(312, Math.Max(0, remaining - 16) / 2) + 16)
            : double.PositiveInfinity;
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
        var fieldWidth = Math.Min(available, Math.Max(312, Math.Max(0, available - 16) / 2) + 16);
        var rtl = FlowDirection == FlowDirection.RightToLeft;
        leading.Arrange(new Rect(rtl ? finalSize.Width - leadWidth : 0, 0, leadWidth, finalSize.Height));
        trailing.Arrange(new Rect(rtl ? 0 : finalSize.Width - trailWidth, 0, trailWidth, finalSize.Height));
        actions.Arrange(new Rect(rtl ? trailWidth : finalSize.Width - trailWidth - actionWidth, 0, actionWidth, finalSize.Height));
        var start = rtl ? trailWidth + actionWidth : leadWidth;
        field.Arrange(new Rect(start + (available - fieldWidth) / 2, 0, fieldWidth, finalSize.Height));
        return finalSize;
    }
}
