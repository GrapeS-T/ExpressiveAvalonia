using System;

namespace ExpressiveAvalonia.Controls;

internal static class CarouselParallax
{
    internal static (double Start, double End, double Translation) Calculate(
        double size, double crossSize, double offset, double viewportStart, double viewportEnd)
    {
        if (size <= 0) return default;
        var aspect = size / crossSize;
        // The strict boundaries (including the discontinuities at 1 and 16/9) are upstream.
        var intensity = aspect > 16d / 9 ? 0.5
            : aspect > 1 && aspect < 16d / 9 ? 1d / 3 + (aspect - 1) / (16d / 9 - 1) / 6
            : aspect > 9d / 16 && aspect < 1 ? 0.25 + (aspect - 9d / 16) / (1 - 9d / 16) / 12
            : 0.25;
        if (offset < viewportStart)
        {
            var progress = Math.Clamp((viewportStart - offset) / size, 0, 1);
            return (size * (1 - intensity) * progress, size, size * intensity * progress);
        }
        if (offset > viewportEnd - size)
        {
            var progress = Math.Clamp((offset + size - viewportEnd) / size, 0, 1);
            return (0, size + (size * intensity - size) * progress, -size * intensity * progress);
        }
        return (0, size, 0);
    }
}
