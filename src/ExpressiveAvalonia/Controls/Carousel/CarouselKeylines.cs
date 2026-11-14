namespace ExpressiveAvalonia.Controls;

/// <summary>Where a carousel's focal range sits inside its container.</summary>
internal enum CarouselAlignment
{
    Start = -1,
    Center = 0,
    End = 1,
}

/// <summary>
/// The state an item should be in when its center is co-located with <see cref="Offset"/>.
/// </summary>
/// <param name="Size">Item size on the scrolling axis at this keyline.</param>
/// <param name="Offset">Keyline position along the scrolling axis.</param>
/// <param name="UnadjustedOffset">
/// Position of the keyline in the end-to-end scrolling model, where every item is laid out at its
/// full size.
/// </param>
/// <param name="IsFocal">Whether an item at this keyline is fully visible.</param>
/// <param name="IsAnchor">Whether this keyline can be shifted between steps.</param>
/// <param name="IsPivot">Whether this keyline was used to derive the other offsets.</param>
/// <param name="Cutoff">How much the item bleeds past the container edge; 0 when fully in or out.</param>
internal readonly record struct CarouselKeyline(
    double Size,
    double Offset,
    double UnadjustedOffset,
    bool IsFocal,
    bool IsAnchor,
    bool IsPivot,
    double Cutoff);

/// <summary>A keyline list with the derived carousel ranges.</summary>
internal sealed class CarouselKeylineList : IReadOnlyList<CarouselKeyline>
{
    private readonly CarouselKeyline[] _keylines;

    public CarouselKeylineList(IReadOnlyList<CarouselKeyline> keylines)
    {
        _keylines = [.. keylines];
        var min = double.MaxValue;
        var max = 0d;
        foreach (var keyline in _keylines)
        {
            if (keyline.Size < min)
                min = keyline.Size;
            if (keyline.Size > max)
                max = keyline.Size;
        }

        MinSize = _keylines.Length == 0 ? 0d : min;
        MaxSize = max;
        PivotIndex = IndexOfFirst(static k => k.IsPivot);
        FirstNonAnchorIndex = IndexOfFirst(static k => !k.IsAnchor);
        LastNonAnchorIndex = IndexOfLast(static k => !k.IsAnchor);
        FirstFocalIndex = IndexOfFirst(static k => k.IsFocal);
        LastFocalIndex = IndexOfLast(static k => k.IsFocal);
        FocalCount = LastFocalIndex - FirstFocalIndex + 1;
    }

    public static CarouselKeylineList Empty { get; } = new([]);

    public double MinSize { get; }

    public double MaxSize { get; }

    /// <summary>Index of the keyline used to derive the other offsets, or -1.</summary>
    public int PivotIndex { get; }

    /// <summary>Index of the first non-anchor keyline, or -1.</summary>
    public int FirstNonAnchorIndex { get; }

    /// <summary>Index of the last non-anchor keyline, or -1.</summary>
    public int LastNonAnchorIndex { get; }

    /// <summary>Index of the first focal keyline, or -1.</summary>
    public int FirstFocalIndex { get; }

    /// <summary>Index of the last focal keyline, or -1.</summary>
    public int LastFocalIndex { get; }

    public int FocalCount { get; }

    public CarouselKeyline Pivot => this[PivotIndex];

    public CarouselKeyline FirstFocal => this[FirstFocalIndex];

    public CarouselKeyline LastFocal => this[LastFocalIndex];

    public int Count => _keylines.Length;

    public CarouselKeyline this[int index] => _keylines[index];

    /// <summary>
    /// True when the first focal item is visible and is the first non-anchor keyline, so the focal
    /// range cannot shift further towards the start.
    /// </summary>
    public bool IsFirstFocalAtStartOfContainer()
    {
        if (FirstFocalIndex < 0 || FirstNonAnchorIndex < 0)
            return false;

        var firstFocalLeft = FirstFocal.Offset - (FirstFocal.Size / 2);
        return firstFocalLeft >= 0 && FirstFocalIndex == FirstNonAnchorIndex;
    }

    /// <summary>
    /// True when the last focal item is visible and is the last non-anchor keyline, so the focal
    /// range cannot shift further towards the end.
    /// </summary>
    public bool IsLastFocalAtEndOfContainer(double carouselMainAxisSize)
    {
        if (LastFocalIndex < 0 || LastNonAnchorIndex < 0)
            return false;

        var lastFocalRight = LastFocal.Offset + (LastFocal.Size / 2);
        return lastFocalRight <= carouselMainAxisSize && LastFocalIndex == LastNonAnchorIndex;
    }

    /// <summary>First keyline after the focal range with <paramref name="size"/>, or the last index.</summary>
    public int FirstIndexAfterFocalRangeWithSize(double size)
    {
        var from = Math.Max(0, LastFocalIndex);
        for (var i = from; i < _keylines.Length; i++)
        {
            if (_keylines[i].Size == size)
                return i;
        }

        return _keylines.Length - 1;
    }

    /// <summary>Last keyline before the focal range with <paramref name="size"/>, or 0.</summary>
    public int LastIndexBeforeFocalRangeWithSize(double size)
    {
        for (var i = FirstFocalIndex - 1; i >= 0; i--)
        {
            if (_keylines[i].Size == size)
                return i;
        }

        return 0;
    }

    /// <summary>Last keyline whose unadjusted offset is below <paramref name="unadjustedOffset"/>.</summary>
    public CarouselKeyline GetKeylineBefore(double unadjustedOffset)
    {
        for (var i = _keylines.Length - 1; i >= 0; i--)
        {
            if (_keylines[i].UnadjustedOffset < unadjustedOffset)
                return _keylines[i];
        }

        return _keylines[0];
    }

    /// <summary>First keyline whose unadjusted offset is at or above <paramref name="unadjustedOffset"/>.</summary>
    public CarouselKeyline GetKeylineAfter(double unadjustedOffset)
    {
        foreach (var keyline in _keylines)
        {
            if (keyline.UnadjustedOffset >= unadjustedOffset)
                return keyline;
        }

        return _keylines[^1];
    }

    public static CarouselKeyline Interpolate(CarouselKeyline start, CarouselKeyline end, double fraction) =>
        new(
            Lerp(start.Size, end.Size, fraction),
            Lerp(start.Offset, end.Offset, fraction),
            Lerp(start.UnadjustedOffset, end.UnadjustedOffset, fraction),
            fraction < 0.5 ? start.IsFocal : end.IsFocal,
            fraction < 0.5 ? start.IsAnchor : end.IsAnchor,
            fraction < 0.5 ? start.IsPivot : end.IsPivot,
            Lerp(start.Cutoff, end.Cutoff, fraction));

    /// <summary>Interpolates every value of every keyline between two equal-length lists.</summary>
    public static CarouselKeylineList Interpolate(CarouselKeylineList from, CarouselKeylineList to, double fraction)
    {
        var keylines = new CarouselKeyline[from.Count];
        for (var i = 0; i < keylines.Length; i++)
            keylines[i] = Interpolate(from[i], to[i], fraction);

        return new CarouselKeylineList(keylines);
    }

    public bool KeylinesEqual(CarouselKeylineList other)
    {
        if (other.Count != Count)
            return false;
        for (var i = 0; i < Count; i++)
        {
            if (!_keylines[i].Equals(other[i]))
                return false;
        }

        return true;
    }

    private int IndexOfFirst(Predicate<CarouselKeyline> predicate)
    {
        for (var i = 0; i < _keylines.Length; i++)
        {
            if (predicate(_keylines[i]))
                return i;
        }

        return -1;
    }

    private int IndexOfLast(Predicate<CarouselKeyline> predicate)
    {
        for (var i = _keylines.Length - 1; i >= 0; i--)
        {
            if (predicate(_keylines[i]))
                return i;
        }

        return -1;
    }

    private static double Lerp(double start, double end, double fraction) =>
        start + ((end - start) * fraction);

    public IEnumerator<CarouselKeyline> GetEnumerator() =>
        ((IEnumerable<CarouselKeyline>)_keylines).GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>Adds keylines in visual order and resolves their offsets from a pivot.</summary>
internal sealed class CarouselKeylineListBuilder
{
    private readonly record struct TempKeyline(double Size, bool IsAnchor);

    private readonly List<TempKeyline> _keylines = [];
    private int _firstFocalIndex = -1;
    private double _focalItemSize;

    public void Add(double size, bool isAnchor = false)
    {
        _keylines.Add(new TempKeyline(size, isAnchor));
        // The first focal item is the first non-anchor item larger than any non-anchor seen so far.
        if (!isAnchor && size > _focalItemSize)
        {
            _firstFocalIndex = _keylines.Count - 1;
            _focalItemSize = size;
        }
    }

    public CarouselKeylineList CreateWithPivot(
        double carouselMainAxisSize,
        double itemSpacing,
        int pivotIndex,
        double pivotOffset) =>
        new(CreateKeylinesWithPivot(
            pivotIndex,
            pivotOffset,
            _firstFocalIndex,
            FindLastFocalIndex(),
            _focalItemSize,
            carouselMainAxisSize,
            itemSpacing,
            _keylines));

    public CarouselKeylineList CreateWithAlignment(
        double carouselMainAxisSize,
        double itemSpacing,
        CarouselAlignment alignment)
    {
        var lastFocalIndex = FindLastFocalIndex();
        var focalItemCount = lastFocalIndex - _firstFocalIndex;

        var pivotIndex = _firstFocalIndex;
        double pivotOffset;
        switch (alignment)
        {
            case CarouselAlignment.Center:
                // With an even number of keylines the item spacing sits in the center of the
                // container, so half of it is subtracted from the first focal center.
                var itemSpacingSplit = itemSpacing == 0 || focalItemCount % 2 == 0 ? 0 : itemSpacing / 2;
                var itemSpaceCounts = (focalItemCount / 2) * itemSpacing;
                pivotOffset = (carouselMainAxisSize / 2)
                    - ((_focalItemSize / 2) * focalItemCount)
                    - itemSpacingSplit
                    - itemSpaceCounts;
                break;
            case CarouselAlignment.End:
                pivotOffset = carouselMainAxisSize - (_focalItemSize / 2);
                break;
            default:
                pivotOffset = _focalItemSize / 2;
                break;
        }

        return new CarouselKeylineList(CreateKeylinesWithPivot(
            pivotIndex,
            pivotOffset,
            _firstFocalIndex,
            lastFocalIndex,
            _focalItemSize,
            carouselMainAxisSize,
            itemSpacing,
            _keylines));
    }

    private int FindLastFocalIndex()
    {
        if (_firstFocalIndex < 0)
            return -1;

        var lastFocalIndex = _firstFocalIndex;
        while (lastFocalIndex < _keylines.Count - 1 &&
               _keylines[lastFocalIndex + 1].Size == _focalItemSize)
        {
            lastFocalIndex++;
        }

        return lastFocalIndex;
    }

    private static List<CarouselKeyline> CreateKeylinesWithPivot(
        int pivotIndex,
        double pivotOffset,
        int firstFocalIndex,
        int lastFocalIndex,
        double itemMainAxisSize,
        double carouselMainAxisSize,
        double itemSpacing,
        List<TempKeyline> tempKeylines)
    {
        var keylines = new List<CarouselKeyline>();
        if (tempKeylines.Count == 0 || pivotIndex < 0 || pivotIndex >= tempKeylines.Count)
            return keylines;

        var pivot = tempKeylines[pivotIndex];
        var pivotCutoff = IsCutoffLeft(pivot.Size, pivotOffset)
            ? pivotOffset - (pivot.Size / 2)
            : IsCutoffRight(pivot.Size, pivotOffset, carouselMainAxisSize)
                ? (pivotOffset + (pivot.Size / 2)) - carouselMainAxisSize
                : 0;

        keylines.Add(new CarouselKeyline(
            pivot.Size,
            pivotOffset,
            pivotOffset,
            pivotIndex >= firstFocalIndex && pivotIndex <= lastFocalIndex,
            pivot.IsAnchor,
            true,
            pivotCutoff));

        var offset = pivotOffset - (itemMainAxisSize / 2) - itemSpacing;
        var unadjustedOffset = pivotOffset - (itemMainAxisSize / 2) - itemSpacing;
        for (var originalIndex = pivotIndex - 1; originalIndex >= 0; originalIndex--)
        {
            var temp = tempKeylines[originalIndex];
            var tempOffset = offset - (temp.Size / 2);
            var tempUnadjustedOffset = unadjustedOffset - (itemMainAxisSize / 2);
            var cutoff = IsCutoffLeft(temp.Size, tempOffset) ? Math.Abs(tempOffset - (temp.Size / 2)) : 0;
            keylines.Insert(0, new CarouselKeyline(
                temp.Size,
                tempOffset,
                tempUnadjustedOffset,
                originalIndex >= firstFocalIndex && originalIndex <= lastFocalIndex,
                temp.IsAnchor,
                false,
                cutoff));

            offset -= temp.Size + itemSpacing;
            unadjustedOffset -= itemMainAxisSize + itemSpacing;
        }

        offset = pivotOffset + (itemMainAxisSize / 2) + itemSpacing;
        unadjustedOffset = pivotOffset + (itemMainAxisSize / 2) + itemSpacing;
        for (var originalIndex = pivotIndex + 1; originalIndex < tempKeylines.Count; originalIndex++)
        {
            var temp = tempKeylines[originalIndex];
            var tempOffset = offset + (temp.Size / 2);
            var tempUnadjustedOffset = unadjustedOffset + (itemMainAxisSize / 2);
            var cutoff = IsCutoffRight(temp.Size, tempOffset, carouselMainAxisSize)
                ? (tempOffset + (temp.Size / 2)) - carouselMainAxisSize
                : 0;
            keylines.Add(new CarouselKeyline(
                temp.Size,
                tempOffset,
                tempUnadjustedOffset,
                originalIndex >= firstFocalIndex && originalIndex <= lastFocalIndex,
                temp.IsAnchor,
                false,
                cutoff));

            offset += temp.Size + itemSpacing;
            unadjustedOffset += itemMainAxisSize + itemSpacing;
        }

        return keylines;
    }

    /// <summary>
    /// True when an item of <paramref name="size"/> centered at <paramref name="offset"/> straddles
    /// the container's start edge.
    /// </summary>
    private static bool IsCutoffLeft(double size, double offset) =>
        offset - (size / 2) < 0 && offset + (size / 2) > 0;

    /// <summary>
    /// True when an item of <paramref name="size"/> centered at <paramref name="offset"/> straddles
    /// the container's end edge.
    /// </summary>
    private static bool IsCutoffRight(double size, double offset, double carouselMainAxisSize) =>
        offset - (size / 2) < carouselMainAxisSize && offset + (size / 2) > carouselMainAxisSize;
}

/// <summary>Pinned carousel constants. Values are the upstream dp values, used directly as DIPs.</summary>
internal static class CarouselDefaults
{
    /// <summary>Minimum size a strategy may choose for a small item.</summary>
    public const double MinSmallItemSize = 40d;

    /// <summary>Maximum size a strategy may choose for a small item.</summary>
    public const double MaxSmallItemSize = 56d;

    /// <summary>Default space between items.</summary>
    public const double ItemSpacing = 0d;

    /// <summary>Size of the off-screen anchor keylines that keep motion at the edges.</summary>
    public const double AnchorSize = 10d;

    /// <summary>Above this fraction of the large size a medium item is too similar to a large one.</summary>
    public const double MediumLargeItemDiffThreshold = 0.85d;
}

/// <summary>Strategy-specific keyline factories ported from the pinned source.</summary>
internal static class CarouselKeylines
{
    public static CarouselKeylineList Empty => CarouselKeylineList.Empty;

    public static CarouselKeylineList Of(
        double carouselMainAxisSize,
        double itemSpacing,
        CarouselAlignment alignment,
        Action<CarouselKeylineListBuilder> keylines)
    {
        var builder = new CarouselKeylineListBuilder();
        keylines(builder);
        return builder.CreateWithAlignment(carouselMainAxisSize, itemSpacing, alignment);
    }

    public static CarouselKeylineList Of(
        double carouselMainAxisSize,
        double itemSpacing,
        int pivotIndex,
        double pivotOffset,
        Action<CarouselKeylineListBuilder> keylines)
    {
        var builder = new CarouselKeylineListBuilder();
        keylines(builder);
        return builder.CreateWithPivot(carouselMainAxisSize, itemSpacing, pivotIndex, pivotOffset);
    }

    /// <summary>
    /// Multi-browse arrangement: large, medium and small items sized to fill the container with the
    /// smallest change to the preferred large size.
    /// </summary>
    public static CarouselKeylineList MultiBrowse(
        double carouselMainAxisSize,
        double preferredItemSize,
        double itemSpacing,
        int itemCount,
        double minSmallItemSize = CarouselDefaults.MinSmallItemSize,
        double maxSmallItemSize = CarouselDefaults.MaxSmallItemSize)
    {
        if (carouselMainAxisSize == 0 || preferredItemSize == 0)
            return Empty;

        int[] smallCounts = [1];
        int[] mediumCounts = [1, 0];

        var targetLargeSize = Math.Min(preferredItemSize, carouselMainAxisSize);
        // A balanced arrangement aims for a small item one third of the large item, clamped.
        var targetSmallSize = Math.Clamp(targetLargeSize / 3, minSmallItemSize, maxSmallItemSize);
        var targetMediumSize = (targetLargeSize + targetSmallSize) / 2;

        if (carouselMainAxisSize < minSmallItemSize * 2)
        {
            // Too narrow to fit a large item next to a smaller one: allow no small items.
            smallCounts = [0];
        }

        var minAvailableLargeSpace = carouselMainAxisSize
            - (targetMediumSize * Max(mediumCounts))
            - (maxSmallItemSize * Max(smallCounts));
        var minLargeCount = Math.Max(1, (int)Math.Floor(minAvailableLargeSpace / targetLargeSize));
        var maxLargeCount = (int)Math.Ceiling(carouselMainAxisSize / targetLargeSize);
        var largeCounts = DescendingCounts(minLargeCount, maxLargeCount);

        var arrangement = CarouselArrangement.FindLowestCost(
            carouselMainAxisSize,
            itemSpacing,
            targetSmallSize,
            minSmallItemSize,
            maxSmallItemSize,
            smallCounts,
            targetMediumSize,
            mediumCounts,
            targetLargeSize,
            largeCounts);

        if (arrangement is { } found && found.ItemCount > itemCount)
        {
            var keylineSurplus = found.ItemCount - itemCount;
            var smallCount = found.SmallCount;
            var mediumCount = found.MediumCount;
            while (keylineSurplus > 0)
            {
                if (smallCount > 0)
                    smallCount--;
                else if (mediumCount > 1)
                    mediumCount--; // Keep one medium so large items do not fill the carousel.
                // Large items are never removed: they are already fully unmasked.
                keylineSurplus--;
            }

            arrangement = CarouselArrangement.FindLowestCost(
                carouselMainAxisSize,
                itemSpacing,
                targetSmallSize,
                minSmallItemSize,
                maxSmallItemSize,
                [smallCount],
                targetMediumSize,
                [mediumCount],
                targetLargeSize,
                largeCounts);
        }

        if (arrangement is not { } result)
            return Empty;

        return CreateLeftAligned(
            carouselMainAxisSize,
            itemSpacing,
            CarouselDefaults.AnchorSize,
            CarouselDefaults.AnchorSize,
            result);
    }

    /// <summary>
    /// Uncontained arrangement: as many full items as fit, plus one cut-off item whose size keeps
    /// enough motion while scrolling off the edge.
    /// </summary>
    public static CarouselKeylineList Uncontained(
        double carouselMainAxisSize,
        double itemSize,
        double itemSpacing,
        double minSmallItemSize = CarouselDefaults.MinSmallItemSize,
        double maxSmallItemSize = CarouselDefaults.MaxSmallItemSize)
    {
        if (carouselMainAxisSize == 0 || itemSize == 0)
            return Empty;

        var largeItemSize = Math.Min(itemSize + itemSpacing, carouselMainAxisSize);
        // Space left after squeezing in as many large items as possible becomes a cut-off item.
        var largeCount = Math.Max(1, (int)Math.Floor(carouselMainAxisSize / largeItemSize));
        var remainingSpace = carouselMainAxisSize - (largeCount * largeItemSize);
        var mediumCount = remainingSpace > 0 ? 1 : 0;

        var defaultAnchorSize = CarouselDefaults.AnchorSize;
        var mediumItemSize = CalculateMediumChildSize(defaultAnchorSize, largeItemSize, remainingSpace);
        var arrangement = new CarouselArrangement(
            0,
            0,
            0,
            mediumItemSize,
            mediumCount,
            largeItemSize,
            largeCount);

        var xSmallSize = Math.Min(defaultAnchorSize, itemSize);
        // Half the cut-off item size keeps motion at the left closer to the right cut off.
        var leftAnchorSize = Math.Max(xSmallSize, mediumItemSize * 0.5);
        return CreateLeftAligned(
            carouselMainAxisSize,
            itemSpacing,
            leftAnchorSize,
            defaultAnchorSize,
            arrangement);
    }

    /// <summary>
    /// Hero arrangement. <paramref name="preferredItemSize"/> null targets the whole container,
    /// fitting one large item besides the required small items.
    /// </summary>
    public static CarouselKeylineList Hero(
        double carouselMainAxisSize,
        double? preferredItemSize,
        double itemSpacing,
        int itemCount,
        bool isCentered = false,
        double minSmallItemSize = CarouselDefaults.MinSmallItemSize,
        double maxSmallItemSize = CarouselDefaults.MaxSmallItemSize)
    {
        if (carouselMainAxisSize == 0)
            return Empty;

        // Fewer than three items cannot be centered.
        var shouldCenter = isCentered && itemCount >= 3;

        int[] smallCounts = itemCount <= 1
            ? [0]
            : shouldCenter ? [2] : [1];

        var targetLargeSize = Math.Min(preferredItemSize ?? carouselMainAxisSize, carouselMainAxisSize);
        var targetSmallSize = Math.Clamp(targetLargeSize / 3, minSmallItemSize, maxSmallItemSize);

        // A large item has to be at least 25% larger than the small items beside it.
        var fullscreenThreshold = (minSmallItemSize * Max(smallCounts)) + (minSmallItemSize * 1.25);
        if (carouselMainAxisSize < fullscreenThreshold)
            smallCounts = [0];

        var minAvailableLargeSpace = carouselMainAxisSize - (minSmallItemSize * Max(smallCounts));
        var minLargeCount = Math.Max(1, (int)Math.Floor(minAvailableLargeSpace / targetLargeSize));
        var maxLargeCount = (int)Math.Ceiling(carouselMainAxisSize / targetLargeSize);
        var largeCounts = DescendingCounts(minLargeCount, maxLargeCount);

        var arrangement = CarouselArrangement.FindLowestCost(
            carouselMainAxisSize,
            itemSpacing,
            targetSmallSize,
            minSmallItemSize,
            maxSmallItemSize,
            smallCounts,
            0,
            [0],
            targetLargeSize,
            largeCounts);

        if (arrangement is not { } result)
            return Empty;

        if (shouldCenter && itemCount >= result.ItemCount)
        {
            return CreateCenterAligned(
                carouselMainAxisSize,
                itemSpacing,
                CarouselDefaults.AnchorSize,
                CarouselDefaults.AnchorSize,
                result);
        }

        return CreateLeftAligned(
            carouselMainAxisSize,
            itemSpacing,
            CarouselDefaults.AnchorSize,
            CarouselDefaults.AnchorSize,
            result);
    }

    /// <summary>
    /// Full-screen extension (design-source layout with no pinned Compose Carousel algorithm):
    /// every item fills the container, so each item is focal on its own.
    /// </summary>
    public static CarouselKeylineList FullScreen(double carouselMainAxisSize)
    {
        if (carouselMainAxisSize <= 0)
            return Empty;

        return Of(carouselMainAxisSize, 0, 0, carouselMainAxisSize / 2, builder => builder.Add(carouselMainAxisSize));
    }

    public static CarouselKeylineList CreateLeftAligned(
        double carouselMainAxisSize,
        double itemSpacing,
        double leftAnchorSize,
        double rightAnchorSize,
        CarouselArrangement arrangement) =>
        Of(carouselMainAxisSize, itemSpacing, CarouselAlignment.Start, builder =>
        {
            builder.Add(leftAnchorSize, isAnchor: true);
            for (var i = 0; i < arrangement.LargeCount; i++)
                builder.Add(arrangement.LargeSize);
            for (var i = 0; i < arrangement.MediumCount; i++)
                builder.Add(arrangement.MediumSize);
            for (var i = 0; i < arrangement.SmallCount; i++)
                builder.Add(arrangement.SmallSize);
            builder.Add(rightAnchorSize, isAnchor: true);
        });

    public static CarouselKeylineList CreateCenterAligned(
        double carouselMainAxisSize,
        double itemSpacing,
        double leftAnchorSize,
        double rightAnchorSize,
        CarouselArrangement arrangement) =>
        Of(carouselMainAxisSize, itemSpacing, CarouselAlignment.Center, builder =>
        {
            builder.Add(leftAnchorSize, isAnchor: true);
            for (var i = 0; i < arrangement.SmallCount / 2; i++)
                builder.Add(arrangement.SmallSize);
            for (var i = 0; i < arrangement.MediumCount / 2; i++)
                builder.Add(arrangement.MediumSize);
            for (var i = 0; i < arrangement.LargeCount; i++)
                builder.Add(arrangement.LargeSize);
            for (var i = 0; i < arrangement.MediumCount / 2; i++)
                builder.Add(arrangement.MediumSize);
            for (var i = 0; i < arrangement.SmallCount / 2; i++)
                builder.Add(arrangement.SmallSize);
            builder.Add(rightAnchorSize, isAnchor: true);
        });

    /// <summary>
    /// Sizes the cut-off medium item so roughly a third (or a fifth above the large-size threshold)
    /// is clipped, keeping a visible size difference from the large item.
    /// </summary>
    private static double CalculateMediumChildSize(double minimumMediumSize, double largeItemSize, double remainingSpace)
    {
        var mediumItemSize = Math.Max(remainingSpace * 1.5, minimumMediumSize);
        var largeItemThreshold = largeItemSize * CarouselDefaults.MediumLargeItemDiffThreshold;
        if (mediumItemSize > largeItemThreshold)
        {
            var sizeWithFifthCutOff = remainingSpace * 1.2;
            mediumItemSize = Math.Min(Math.Max(largeItemThreshold, sizeWithFifthCutOff), largeItemSize);
        }

        return mediumItemSize;
    }

    private static int[] DescendingCounts(int minLargeCount, int maxLargeCount)
    {
        var count = maxLargeCount - minLargeCount + 1;
        if (count <= 0)
            return [];

        var counts = new int[count];
        for (var i = 0; i < count; i++)
            counts[i] = maxLargeCount - i;
        return counts;
    }

    private static int Max(int[] values)
    {
        var max = values[0];
        foreach (var value in values)
        {
            if (value > max)
                max = value;
        }
        return max;
    }
}