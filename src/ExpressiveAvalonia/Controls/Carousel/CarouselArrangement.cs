namespace ExpressiveAvalonia.Controls;

/// <summary>
/// A candidate combination of small, medium and large items with the sizes adjusted to fit the
/// available space, and a heuristic cost used to pick the most desirable arrangement.
/// </summary>
internal sealed class CarouselArrangement(
    int priority,
    double smallSize,
    int smallCount,
    double mediumSize,
    int mediumCount,
    double largeSize,
    int largeCount)
{
    // Percentage by which a medium item can flex to help an arrangement fit.
    private const double MediumItemFlexPercentage = 0.1;

    public int Priority { get; } = priority;
    public double SmallSize { get; } = smallSize;
    public int SmallCount { get; } = smallCount;
    public double MediumSize { get; } = mediumSize;
    public int MediumCount { get; } = mediumCount;
    public double LargeSize { get; } = largeSize;
    public int LargeCount { get; } = largeCount;

    public int ItemCount => LargeCount + MediumCount + SmallCount;

    private bool IsValid()
    {
        if (LargeCount > 0 && SmallCount > 0 && MediumCount > 0)
            return LargeSize > MediumSize && MediumSize > SmallSize;
        if (LargeCount > 0 && SmallCount > 0)
            return LargeSize > SmallSize;
        return true;
    }

    /// <summary>
    /// Lower is better: arrangements closer to the target large size, generated earlier (lower
    /// priority), win.
    /// </summary>
    public double Cost(double targetLargeSize) =>
        IsValid() ? Math.Abs(targetLargeSize - LargeSize) * Priority : double.MaxValue;

    /// <summary>
    /// Enumerates the permutations of the given counts in priority order, fits each into the
    /// available space, and returns the one with the lowest cost.
    /// </summary>
    public static CarouselArrangement? FindLowestCost(
        double availableSpace,
        double itemSpacing,
        double targetSmallSize,
        double minSmallSize,
        double maxSmallSize,
        int[] smallCounts,
        double targetMediumSize,
        int[] mediumCounts,
        double targetLargeSize,
        int[] largeCounts)
    {
        if (largeCounts.Length == 0 || mediumCounts.Length == 0 || smallCounts.Length == 0)
            return null;

        CarouselArrangement? lowestCostArrangement = null;
        var priority = 1;
        foreach (var largeCount in largeCounts)
        {
            foreach (var mediumCount in mediumCounts)
            {
                foreach (var smallCount in smallCounts)
                {
                    var arrangement = Fit(
                        priority,
                        availableSpace,
                        itemSpacing,
                        smallCount,
                        targetSmallSize,
                        minSmallSize,
                        maxSmallSize,
                        mediumCount,
                        targetMediumSize,
                        largeCount,
                        targetLargeSize);

                    if (lowestCostArrangement is null ||
                        arrangement.Cost(targetLargeSize) < lowestCostArrangement.Cost(targetLargeSize))
                    {
                        lowestCostArrangement = arrangement;
                        // A cost of 0 means the large size did not have to change. Permutations are
                        // generated in priority order, so nothing later can be better.
                        if (lowestCostArrangement.Cost(targetLargeSize) == 0)
                            return lowestCostArrangement;
                    }

                    priority++;
                }
            }
        }

        return lowestCostArrangement;
    }

    /// <summary>
    /// Fits the item counts into the available space by resizing small items first (within their
    /// range), then medium items within their flex range, and finally large items.
    /// </summary>
    private static CarouselArrangement Fit(
        int priority,
        double availableSpace,
        double itemSpacing,
        int smallCount,
        double smallSize,
        double minSmallSize,
        double maxSmallSize,
        int mediumCount,
        double mediumSize,
        int largeCount,
        double largeSize)
    {
        var totalItemCount = largeCount + mediumCount + smallCount;
        var availableSpaceWithoutSpacing = availableSpace - ((totalItemCount - 1) * itemSpacing);
        var arrangedSmallSize = Math.Clamp(smallSize, minSmallSize, maxSmallSize);
        var arrangedMediumSize = mediumSize;
        var arrangedLargeSize = largeSize;

        var totalSpaceTakenByArrangement =
            (arrangedLargeSize * largeCount) +
            (arrangedMediumSize * mediumCount) +
            (arrangedSmallSize * smallCount);
        var delta = availableSpaceWithoutSpacing - totalSpaceTakenByArrangement;

        if (smallCount > 0 && delta > 0)
        {
            // Grow the small items.
            arrangedSmallSize += Math.Min(delta / smallCount, maxSmallSize - arrangedSmallSize);
        }
        else if (smallCount > 0 && delta < 0)
        {
            // Shrink the small items.
            arrangedSmallSize += Math.Max(delta / smallCount, minSmallSize - arrangedSmallSize);
        }

        if (smallCount <= 0)
            arrangedSmallSize = 0;

        arrangedLargeSize = CalculateLargeSize(
            availableSpaceWithoutSpacing,
            smallCount,
            arrangedSmallSize,
            mediumCount,
            largeCount);
        arrangedMediumSize = (arrangedLargeSize + arrangedSmallSize) / 2;

        // Counter any large-size change by adjusting the medium items within their flex range.
        if (mediumCount > 0 && arrangedLargeSize != largeSize)
        {
            var targetAdjustment = (largeSize - arrangedLargeSize) * largeCount;
            var availableMediumFlex = arrangedMediumSize * MediumItemFlexPercentage * mediumCount;
            var distribute = Math.Min(Math.Abs(targetAdjustment), availableMediumFlex);
            if (targetAdjustment > 0)
            {
                arrangedMediumSize -= distribute / mediumCount;
                arrangedLargeSize += distribute / largeCount;
            }
            else
            {
                arrangedMediumSize += distribute / mediumCount;
                arrangedLargeSize -= distribute / largeCount;
            }
        }

        return new CarouselArrangement(
            priority,
            arrangedSmallSize,
            smallCount,
            arrangedMediumSize,
            mediumCount,
            arrangedLargeSize,
            largeCount);
    }

    /// <summary>
    /// Solves
    /// <c>space = large*largeCount + ((large + small)/2)*mediumCount + small*smallCount</c>
    /// for the large item size.
    /// </summary>
    private static double CalculateLargeSize(
        double availableSpace,
        int smallCount,
        double smallSize,
        int mediumCount,
        int largeCount) =>
        (availableSpace - ((smallCount + (mediumCount / 2d)) * smallSize)) /
        (largeCount + (mediumCount / 2d));
}