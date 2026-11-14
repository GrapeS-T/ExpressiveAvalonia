using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;

namespace ExpressiveAvalonia.Theming;

/// <summary>Discrete native-text sizing in DIP. Explicit caller FontSize values remain authoritative.</summary>
public sealed class MaterialTextAutoSize
{
    public double MinFontSize { get; }
    public double MaxFontSize { get; }
    public double StepSize { get; }

    private MaterialTextAutoSize(double min, double max, double step)
    {
        if (!double.IsFinite(min) || min < 0) throw new ArgumentOutOfRangeException(nameof(min));
        if (!double.IsFinite(max) || max < 0) throw new ArgumentOutOfRangeException(nameof(max));
        if (!double.IsFinite(step) || step < .0001) throw new ArgumentOutOfRangeException(nameof(step));
        MinFontSize = Math.Min(min, max);
        MaxFontSize = max;
        StepSize = step;
    }

    public static MaterialTextAutoSize StepBased(double minFontSize = 12, double maxFontSize = 112, double stepSize = .25) =>
        new(minFontSize, maxFontSize, stepSize);

    internal double FindSize(TextBlock text, Size constraint)
    {
        var min = MinFontSize;
        var max = MaxFontSize;
        var lineLimit = text.MaxLines;
        bool Fits(double size)
        {
            // Animation priority is temporary measurement only. Disposal restores every original binding.
            using var font = text.SetValue(TextBlock.FontSizeProperty, Math.Max(size, double.Epsilon), BindingPriority.Animation);
            using var height = text.SetValue(Control.HeightProperty, double.NaN, BindingPriority.Animation);
            using var maxHeight = text.SetValue(Control.MaxHeightProperty, double.PositiveInfinity, BindingPriority.Animation);
            using var lines = text.SetValue(TextBlock.MaxLinesProperty, 0, BindingPriority.Animation);
            text.InvalidateMeasure();
            text.Measure(new Size(constraint.Width + text.Padding.Left + text.Padding.Right, double.PositiveInfinity));
            var layout = text.TextLayout;
            if (layout.WidthIncludingTrailingWhitespace > constraint.Width + .000001 || layout.Height > constraint.Height + .000001)
                return false;
            if (lineLimit > 0 && layout.TextLines.Count > lineLimit)
                return false;
            foreach (var line in layout.TextLines)
                if (line.HasCollapsed || line.HasOverflowed) return false;
            return true;
        }
        // The source searches continuously, snaps to the discrete grid, then tries one step up.
        while (max - min >= StepSize)
        {
            var middle = (min + max) / 2;
            if (Fits(middle)) min = middle; else max = middle;
        }
        var result = Math.Floor((min - MinFontSize) / StepSize) * StepSize + MinFontSize;
        if (result + StepSize <= MaxFontSize && Fits(result + StepSize)) result += StepSize;
        text.InvalidateMeasure();
        return Math.Max(result, double.Epsilon);
    }
}
