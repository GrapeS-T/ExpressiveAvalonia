namespace ExpressiveAvalonia.Controls;

/// <summary>
/// Supplies the keyline list for a container, corrected for scroll offset, layout direction and
/// snapping, and derives the per-item snap and scroll offsets.
/// </summary>
/// <remarks>
/// The keyline coordinate system is the carousel's content area (the container width minus the
/// before/after content padding). Page slots are laid out end-to-end in that coordinate system;
/// the scroll offset is the distance the content area has scrolled.
/// </remarks>
internal sealed class CarouselStrategy
{
    private readonly double _startShiftDistance;
    private readonly double _endShiftDistance;
    private readonly double[] _startShiftPoints;
    private readonly double[] _endShiftPoints;
    private CarouselKeylineList[]? _lastStartAndEndSteps;

    // Last request, so a layout pass does not re-derive the same keyline list per item.
    private double _cachedScrollOffset = -1;
    private double _cachedMaxScrollOffset = -1;
    private bool _cachedRoundToNearestStep;
    private CarouselKeylineList? _cachedKeylineList;

    public CarouselStrategy(
        CarouselKeylineList defaultKeylines,
        double availableSpace,
        double itemSpacing,
        double beforeContentPadding,
        double afterContentPadding)
        : this(
            defaultKeylines,
            GetStartKeylineSteps(defaultKeylines, availableSpace, itemSpacing, beforeContentPadding),
            GetEndKeylineSteps(defaultKeylines, availableSpace, itemSpacing, afterContentPadding),
            availableSpace,
            itemSpacing,
            beforeContentPadding,
            afterContentPadding)
    {
    }

    private CarouselStrategy(
        CarouselKeylineList defaultKeylines,
        List<CarouselKeylineList> startKeylineSteps,
        List<CarouselKeylineList> endKeylineSteps,
        double availableSpace,
        double itemSpacing,
        double beforeContentPadding,
        double afterContentPadding)
    {
        DefaultKeylines = defaultKeylines;
        StartKeylineSteps = startKeylineSteps;
        EndKeylineSteps = endKeylineSteps;
        AvailableSpace = availableSpace;
        ItemSpacing = itemSpacing;
        BeforeContentPadding = beforeContentPadding;
        AfterContentPadding = afterContentPadding;

        var min = defaultKeylines.Count > 0 ? defaultKeylines.MinSize : 0;
        var max = defaultKeylines.MaxSize;
        foreach (var step in startKeylineSteps)
        {
            if (step.Count == 0)
                continue;
            if (step.MinSize < min)
                min = step.MinSize;
            if (step.MaxSize > max)
                max = step.MaxSize;
        }

        foreach (var step in endKeylineSteps)
        {
            if (step.Count == 0)
                continue;
            if (step.MinSize < min)
                min = step.MinSize;
            if (step.MaxSize > max)
                max = step.MaxSize;
        }

        MinItemSize = min;
        MaxItemSize = max;
        ItemMainAxisSize = defaultKeylines.Count > 0 ? defaultKeylines.FirstFocal.Size : 0;
        IsValid = defaultKeylines.Count > 0 && availableSpace != 0 && ItemMainAxisSize != 0;
        _startShiftDistance = GetStartShiftDistance(startKeylineSteps, beforeContentPadding);
        _endShiftDistance = GetEndShiftDistance(endKeylineSteps, afterContentPadding);
        _startShiftPoints = GetStepInterpolationPoints(_startShiftDistance, startKeylineSteps, true);
        _endShiftPoints = GetStepInterpolationPoints(_endShiftDistance, endKeylineSteps, false);
    }

    public static CarouselStrategy Empty { get; } = new(
        CarouselKeylineList.Empty,
        [],
        [],
        0,
        0,
        0,
        0);

    public CarouselKeylineList DefaultKeylines { get; }

    public IReadOnlyList<CarouselKeylineList> StartKeylineSteps { get; }

    public IReadOnlyList<CarouselKeylineList> EndKeylineSteps { get; }

    public double AvailableSpace { get; }

    public double ItemSpacing { get; }

    public double BeforeContentPadding { get; }

    public double AfterContentPadding { get; }

    public double MinItemSize { get; }

    public double MaxItemSize { get; }

    /// <summary>The size of fully unmasked items.</summary>
    public double ItemMainAxisSize { get; }

    public bool IsValid { get; }

    /// <summary>Distance between item starts when every item has the focal size.</summary>
    public double ItemSizeWithSpacing => ItemMainAxisSize + ItemSpacing;

    /// <summary>Returns the keyline list for the current scroll offset.</summary>
    /// <param name="scrollOffset">Content scroll offset in the keyline coordinate system.</param>
    /// <param name="maxScrollOffset">Maximum scroll offset.</param>
    /// <param name="roundToNearestStep">True to return a complete shift step instead of a lerp.</param>
    public CarouselKeylineList GetKeylineListForScrollOffset(
        double scrollOffset,
        double maxScrollOffset,
        bool roundToNearestStep = false)
    {
        if (scrollOffset == _cachedScrollOffset &&
            maxScrollOffset == _cachedMaxScrollOffset &&
            roundToNearestStep == _cachedRoundToNearestStep &&
            _cachedKeylineList is not null)
        {
            return _cachedKeylineList;
        }

        var result = GetKeylineListForScrollOffsetInternal(scrollOffset, maxScrollOffset, roundToNearestStep);
        _cachedScrollOffset = scrollOffset;
        _cachedMaxScrollOffset = maxScrollOffset;
        _cachedRoundToNearestStep = roundToNearestStep;
        _cachedKeylineList = result;
        return result;
    }

    /// <summary>
    /// Offset needed to snap the item at <paramref name="itemIndex"/> into its focal position.
    /// </summary>
    public double GetSnapPositionOffset(int itemIndex, int itemCount)
    {
        if (!IsValid)
            return 0;

        // Default: align the first focal keyline of the default arrangement.
        var offset = RoundToInt(DefaultKeylines.FirstFocal.UnadjustedOffset - (ItemMainAxisSize / 2));

        if (itemIndex <= StartKeylineSteps.Count - 1 && StartKeylineSteps.Count > 0)
        {
            // Steps are ordered from the default arrangement outwards; index from the end.
            var stepIndex = Math.Clamp(StartKeylineSteps.Count - 1 - itemIndex, 0, StartKeylineSteps.Count - 1);
            var startKeylines = StartKeylineSteps[stepIndex];
            offset = RoundToInt(startKeylines.FirstFocal.UnadjustedOffset - (ItemMainAxisSize / 2));
        }

        var lastItemIndex = itemCount - 1;
        if (itemIndex >= lastItemIndex - (EndKeylineSteps.Count - 1) &&
            itemCount > DefaultKeylines.FocalCount &&
            EndKeylineSteps.Count > 0)
        {
            var stepIndex = Math.Clamp(EndKeylineSteps.Count - 1 - (lastItemIndex - itemIndex), 0, EndKeylineSteps.Count - 1);
            var endKeylines = EndKeylineSteps[stepIndex];
            offset = RoundToInt(endKeylines.LastFocal.UnadjustedOffset - (ItemMainAxisSize / 2));
        }

        return offset;
    }

    /// <summary>
    /// Content scroll offset that places the item at <paramref name="itemIndex"/> in its snapped
    /// position. Clamped to the scrollable range.
    /// </summary>
    public double GetContentOffsetForItem(int itemIndex, int itemCount, double maxScrollOffset)
    {
        if (itemCount <= 0)
            return 0;

        var index = Math.Clamp(itemIndex, 0, itemCount - 1);
        var snap = GetSnapPositionOffset(index, itemCount);
        return Math.Clamp((index * ItemSizeWithSpacing) - snap, 0, Math.Max(0, maxScrollOffset));
    }

    /// <summary>
    /// The item closest to its snapped position for <paramref name="scrollOffset"/>, matching the
    /// Pager's current page.
    /// </summary>
    public int GetCurrentItem(int itemCount, double scrollOffset)
    {
        if (itemCount <= 0)
            return -1;

        var bestIndex = 0;
        var bestDistance = double.MaxValue;
        for (var i = 0; i < itemCount; i++)
        {
            var distance = Math.Abs((i * ItemSizeWithSpacing) - GetSnapPositionOffset(i, itemCount) - scrollOffset);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    /// <summary>
    /// Fraction of an item between two adjacent snap positions, in [-0.5, 0.5], used to keep the
    /// anchor when the container resizes.
    /// </summary>
    public double GetOffsetFraction(int itemIndex, int itemCount, double scrollOffset)
    {
        if (!IsValid || ItemSizeWithSpacing == 0)
            return 0;

        var fraction =
            (scrollOffset + GetSnapPositionOffset(itemIndex, itemCount) - (itemIndex * ItemSizeWithSpacing)) /
            ItemSizeWithSpacing;
        return Math.Clamp(fraction, -0.5, 0.5);
    }

    /// <summary>Scroll offset that preserves <paramref name="itemIndex"/> and its fraction.</summary>
    public double GetOffsetForAnchor(int itemIndex, int itemCount, double fraction, double maxScrollOffset)
    {
        if (itemCount <= 0)
            return 0;

        var index = Math.Clamp(itemIndex, 0, itemCount - 1);
        var offset = (index * ItemSizeWithSpacing) +
            (Math.Clamp(fraction, -0.5, 0.5) * ItemSizeWithSpacing) -
            GetSnapPositionOffset(index, itemCount);
        return Math.Clamp(offset, 0, Math.Max(0, maxScrollOffset));
    }

    /// <summary>Total content length of all items and the spacing between them.</summary>
    public double GetContentSize(int itemCount) =>
        itemCount <= 0 ? 0 : (ItemMainAxisSize * itemCount) + (ItemSpacing * (itemCount - 1));

    /// <summary>Maximum scroll offset, never negative.</summary>
    public double GetMaxScrollOffset(int itemCount) =>
        Math.Max(0, GetContentSize(itemCount) - AvailableSpace);

    private CarouselKeylineList GetKeylineListForScrollOffsetInternal(
        double scrollOffset,
        double maxScrollOffset,
        bool roundToNearestStep)
    {
        // Rounding can produce a slightly negative offset.
        var positiveScrollOffset = Math.Max(0, scrollOffset);
        var startShiftOffset = _startShiftDistance;
        var endShiftOffset = Math.Max(0, maxScrollOffset - _endShiftDistance);

        if (positiveScrollOffset >= startShiftOffset && positiveScrollOffset <= endShiftOffset)
            return DefaultKeylines;

        var interpolation = LerpClamp(1, 0, 0, startShiftOffset, positiveScrollOffset);
        var shiftPoints = _startShiftPoints;
        IReadOnlyList<CarouselKeylineList> steps = StartKeylineSteps;

        if (positiveScrollOffset > endShiftOffset)
        {
            interpolation = LerpClamp(0, 1, endShiftOffset, maxScrollOffset, positiveScrollOffset);
            shiftPoints = _endShiftPoints;
            steps = EndKeylineSteps;

            // If end shifting begins at offset 0, interpolate directly from the last start step to
            // the last end step and skip the default arrangement in between.
            if (endShiftOffset < 0.01 &&
                StartKeylineSteps.Count == 2 &&
                EndKeylineSteps.Count == 2)
            {
                _lastStartAndEndSteps ??=
                [
                    StartKeylineSteps[^1],
                    EndKeylineSteps[^1],
                ];
                steps = _lastStartAndEndSteps;
            }
        }

        if (steps.Count == 0)
            return DefaultKeylines;

        var fromStepIndex = 0;
        var toStepIndex = 0;
        var steppedInterpolation = 0d;
        var lowerBounds = shiftPoints[0];
        for (var i = 1; i < steps.Count; i++)
        {
            var upperBounds = shiftPoints[i];
            if (interpolation <= upperBounds)
            {
                fromStepIndex = i - 1;
                toStepIndex = i;
                steppedInterpolation = LerpClamp(0, 1, lowerBounds, upperBounds, interpolation);
                break;
            }

            lowerBounds = upperBounds;
        }

        if (roundToNearestStep)
            return steps[steppedInterpolation >= 0.5 ? toStepIndex : fromStepIndex];

        return CarouselKeylineList.Interpolate(steps[fromStepIndex], steps[toStepIndex], steppedInterpolation);
    }

    private static double GetStartShiftDistance(List<CarouselKeylineList> startKeylineSteps, double beforeContentPadding)
    {
        if (startKeylineSteps.Count == 0)
            return 0;

        return Math.Max(
            startKeylineSteps[^1][0].UnadjustedOffset - startKeylineSteps[0][0].UnadjustedOffset,
            beforeContentPadding);
    }

    private static double GetEndShiftDistance(List<CarouselKeylineList> endKeylineSteps, double afterContentPadding)
    {
        if (endKeylineSteps.Count == 0)
            return 0;

        return Math.Max(
            endKeylineSteps[0][^1].UnadjustedOffset - endKeylineSteps[^1][^1].UnadjustedOffset,
            afterContentPadding);
    }

    /// <summary>
    /// Generates the steps that move the focal range one keyline at a time until it reaches the
    /// start of the container. The first step is always the default arrangement.
    /// </summary>
    private static List<CarouselKeylineList> GetStartKeylineSteps(
        CarouselKeylineList defaultKeylines,
        double carouselMainAxisSize,
        double itemSpacing,
        double beforeContentPadding)
    {
        var steps = new List<CarouselKeylineList>();
        if (defaultKeylines.Count == 0)
            return steps;

        steps.Add(defaultKeylines);

        if (defaultKeylines.IsFirstFocalAtStartOfContainer())
        {
            if (beforeContentPadding != 0)
            {
                steps.Add(CreateShiftedKeylineListForContentPadding(
                    defaultKeylines,
                    carouselMainAxisSize,
                    itemSpacing,
                    beforeContentPadding,
                    defaultKeylines.FirstFocal,
                    defaultKeylines.FirstFocalIndex));
            }

            return steps;
        }

        var startIndex = defaultKeylines.FirstNonAnchorIndex;
        var endIndex = defaultKeylines.FirstFocalIndex;
        var numberOfSteps = endIndex - startIndex;

        if (numberOfSteps <= 0 && defaultKeylines.FirstFocal.Cutoff > 0)
        {
            steps.Add(MoveKeylineAndCreateShiftedKeylineList(
                defaultKeylines,
                0,
                0,
                carouselMainAxisSize,
                itemSpacing));
            return steps;
        }

        for (var i = 0; i < numberOfSteps; i++)
        {
            var previousStep = steps[^1];
            var originalItemIndex = startIndex + i;
            var destinationIndex = defaultKeylines.Count - 1;
            if (originalItemIndex > 0)
            {
                var originalNeighborBeforeSize = defaultKeylines[originalItemIndex - 1].Size;
                destinationIndex = previousStep.FirstIndexAfterFocalRangeWithSize(originalNeighborBeforeSize) - 1;
            }

            steps.Add(MoveKeylineAndCreateShiftedKeylineList(
                previousStep,
                defaultKeylines.FirstNonAnchorIndex,
                destinationIndex,
                carouselMainAxisSize,
                itemSpacing));
        }

        if (beforeContentPadding != 0)
        {
            var last = steps[^1];
            steps[^1] = CreateShiftedKeylineListForContentPadding(
                last,
                carouselMainAxisSize,
                itemSpacing,
                beforeContentPadding,
                last.FirstFocal,
                last.FirstFocalIndex);
        }

        return steps;
    }

    /// <summary>
    /// Generates the steps that move the focal range one keyline at a time until it reaches the
    /// end of the container. The first step is always the default arrangement.
    /// </summary>
    private static List<CarouselKeylineList> GetEndKeylineSteps(
        CarouselKeylineList defaultKeylines,
        double carouselMainAxisSize,
        double itemSpacing,
        double afterContentPadding)
    {
        var steps = new List<CarouselKeylineList>();
        if (defaultKeylines.Count == 0)
            return steps;

        steps.Add(defaultKeylines);

        if (defaultKeylines.IsLastFocalAtEndOfContainer(carouselMainAxisSize))
        {
            if (afterContentPadding != 0)
            {
                steps.Add(CreateShiftedKeylineListForContentPadding(
                    defaultKeylines,
                    carouselMainAxisSize,
                    itemSpacing,
                    -afterContentPadding,
                    defaultKeylines.LastFocal,
                    defaultKeylines.LastFocalIndex));
            }

            return steps;
        }

        var startIndex = defaultKeylines.LastFocalIndex;
        var endIndex = defaultKeylines.LastNonAnchorIndex;
        var numberOfSteps = endIndex - startIndex;

        if (numberOfSteps <= 0 && defaultKeylines.LastFocal.Cutoff > 0)
        {
            steps.Add(MoveKeylineAndCreateShiftedKeylineList(
                defaultKeylines,
                0,
                0,
                carouselMainAxisSize,
                itemSpacing));
            return steps;
        }

        for (var i = 0; i < numberOfSteps; i++)
        {
            var previousStep = steps[^1];
            var originalItemIndex = endIndex - i;
            var destinationIndex = 0;

            if (originalItemIndex < defaultKeylines.Count - 1)
            {
                var originalNeighborAfterSize = defaultKeylines[originalItemIndex + 1].Size;
                destinationIndex = previousStep.LastIndexBeforeFocalRangeWithSize(originalNeighborAfterSize) + 1;
            }

            steps.Add(MoveKeylineAndCreateShiftedKeylineList(
                previousStep,
                defaultKeylines.LastNonAnchorIndex,
                destinationIndex,
                carouselMainAxisSize,
                itemSpacing));
        }

        if (afterContentPadding != 0)
        {
            var last = steps[^1];
            steps[^1] = CreateShiftedKeylineListForContentPadding(
                last,
                carouselMainAxisSize,
                itemSpacing,
                -afterContentPadding,
                last.LastFocal,
                last.LastFocalIndex);
        }

        return steps;
    }

    /// <summary>
    /// Returns a copy of <paramref name="from"/> with every keyline offset shifted by
    /// <paramref name="contentPadding"/> and item sizes reduced to make room for it.
    /// </summary>
    private static CarouselKeylineList CreateShiftedKeylineListForContentPadding(
        CarouselKeylineList from,
        double carouselMainAxisSize,
        double itemSpacing,
        double contentPadding,
        CarouselKeyline pivot,
        int pivotIndex)
    {
        var numberOfNonAnchorKeylines = 0;
        for (var i = 0; i < from.Count; i++)
        {
            if (!from[i].IsAnchor)
                numberOfNonAnchorKeylines++;
        }

        if (numberOfNonAnchorKeylines == 0)
            return from;

        var sizeReduction = contentPadding / numberOfNonAnchorKeylines;
        var newKeylines = CarouselKeylines.Of(
            carouselMainAxisSize,
            itemSpacing,
            pivotIndex,
            pivot.Offset - (sizeReduction / 2) + contentPadding,
            builder =>
            {
                for (var i = 0; i < from.Count; i++)
                    builder.Add(from[i].Size - Math.Abs(sizeReduction), from[i].IsAnchor);
            });

        // Reset the unadjusted offsets: the Pager still lays items out end-to-end at their
        // original page size.
        var reset = new CarouselKeyline[newKeylines.Count];
        for (var i = 0; i < reset.Length; i++)
            reset[i] = newKeylines[i] with { UnadjustedOffset = from[i].UnadjustedOffset };
        return new CarouselKeylineList(reset);
    }

    /// <summary>
    /// Moves one keyline to <paramref name="destinationIndex"/> and recalculates the offsets and
    /// pivot to reflect the resulting focal shift.
    /// </summary>
    private static CarouselKeylineList MoveKeylineAndCreateShiftedKeylineList(
        CarouselKeylineList from,
        int sourceIndex,
        int destinationIndex,
        double carouselMainAxisSize,
        double itemSpacing)
    {
        // -1 when the pivot shifts towards the start, 1 when it shifts towards the end.
        var pivotDirection = sourceIndex > destinationIndex ? 1 : -1;
        var pivotDelta =
            (from[sourceIndex].Size - from[sourceIndex].Cutoff + itemSpacing) * pivotDirection;
        var newPivotIndex = from.PivotIndex + pivotDirection;
        var newPivotOffset = from.Pivot.Offset + pivotDelta;

        var entries = new List<CarouselKeyline>(from.Count);
        for (var i = 0; i < from.Count; i++)
            entries.Add(from[i]);

        var moved = entries[sourceIndex];
        entries.RemoveAt(sourceIndex);
        entries.Insert(destinationIndex, moved);

        return CarouselKeylines.Of(carouselMainAxisSize, itemSpacing, newPivotIndex, newPivotOffset, builder =>
        {
            foreach (var keyline in entries)
                builder.Add(keyline.Size, keyline.IsAnchor);
        });
    }

    /// <summary>
    /// Points between 0 and 1 marking where each step becomes active while interpolating through
    /// the shift.
    /// </summary>
    private static double[] GetStepInterpolationPoints(
        double totalShiftDistance,
        List<CarouselKeylineList> steps,
        bool isShiftingLeft)
    {
        if (totalShiftDistance == 0 || steps.Count == 0)
            return [0];

        var points = new double[steps.Count];
        points[0] = 0;
        for (var i = 1; i < steps.Count; i++)
        {
            var previousKeylines = steps[i - 1];
            var currentKeylines = steps[i];
            var distanceShifted = isShiftingLeft
                ? currentKeylines[0].UnadjustedOffset - previousKeylines[0].UnadjustedOffset
                : previousKeylines[^1].UnadjustedOffset - currentKeylines[^1].UnadjustedOffset;
            var stepPercentage = distanceShifted / totalShiftDistance;
            points[i] = i == steps.Count - 1 ? 1 : points[i - 1] + stepPercentage;
        }

        return points;
    }

    private static double LerpClamp(double outputMin, double outputMax, double inputMin, double inputMax, double value)
    {
        if (value <= inputMin)
            return outputMin;
        if (value >= inputMax)
            return outputMax;
        return outputMin + ((outputMax - outputMin) * ((value - inputMin) / (inputMax - inputMin)));
    }

    private static int RoundToInt(double value) => (int)Math.Floor(value + 0.5);
}