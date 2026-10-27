using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Generators;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// The virtualizing item strip used by <see cref="MaterialCarousel"/>.
/// </summary>
/// <remarks>
/// <para>
/// Items are laid out end-to-end at the arrangement's focal size and then masked and translated to
/// the interpolated keyline for the current scroll offset, exactly as
/// <c>androidx.compose.material3.carousel.Carousel</c> does. Only the items in the visible window
/// plus the layout's beyond-viewport count are realized; containers are reused from a pool keyed by
/// the generator's recycle key.
/// </para>
/// <para>
/// The carousel's <see cref="Control.Padding"/> is the pinned <c>contentPadding</c>. As in
/// <c>Carousel.kt</c>, only the cross-axis part is a physical inset: the main-axis start/end values
/// are handed to <see cref="CarouselStrategy"/> as <c>beforeContentPadding</c>/<c>afterContentPadding</c>
/// and therefore appear only through the strategy's shifted keyline steps at the start and end of
/// the list, not as extra scroll extent or as a constant offset of every item. The container itself
/// uses the full viewport for the keyline geometry, which is what
/// <c>CarouselPageSize.calculateMainAxisPageSize</c> passes to the strategy.
/// Right-to-left flow mirrors the content slots and negates the keyline translation while
/// <see cref="ScrollViewer"/> offsets stay physical.
/// </para>
/// <para>
/// <see cref="MaterialCarouselLayout.MultiAspectRatio"/> uses variable-width slots with the pinned
/// <c>carouselParallaxScrollEffect</c> edge mask and content translation rather than Pager keylines.
/// Each item keeps its aspect-ratio width and snaps by start edge.
/// </para>
/// </remarks>
public sealed class MaterialCarouselPanel : VirtualizingPanel, IScrollSnapPointsInfo
{
    private static readonly AttachedProperty<object?> RecycleKeyProperty =
        AvaloniaProperty.RegisterAttached<MaterialCarouselPanel, Control, object?>("RecycleKey");

    private static readonly object ItemIsItsOwnContainer = new();

    private readonly List<RealizedItem> _realized = [];
    private readonly Dictionary<Control, ItemVisuals> _visuals = [];
    private readonly Dictionary<object, Stack<Control>> _recyclePool = [];
    private readonly double[] _emptyPoints = [];
    private CarouselStrategy _strategy = CarouselStrategy.Empty;
    private LayoutParameters _parameters;
    private ScrollViewer? _scrollViewer;
    private MaterialCarousel? _owner;
    private double[] _aspectStarts = [];
    private double[] _aspectWidths = [];
    private double[] _nearPoints = [];
    private double[] _centerPoints = [];
    private double[] _farPoints = [];
    private SnapCacheKey _snapCacheKey;
    private double _viewportWidth;
    private double _viewportHeight;
    private double _contentWidth;
    private double _crossSize;
    private double _pendingOffset = double.NaN;
    private int _pendingAnchorItem = -1;
    private double _pendingAnchorFraction;
    private int _pendingApplyAttempts;
    private int _currentItem = -1;
    private double _currentFraction;
    private bool _hasBuilt;
    private bool _applyingPendingOffset;
    private bool _aspectMetricsDirty;

    internal void InvalidateAspectMetrics()
    {
        _aspectMetricsDirty = true;
        InvalidateMeasure();
    }

    /// <summary>Creates the carousel strip.</summary>
    public MaterialCarouselPanel()
    {
        EffectiveViewportChanged += (_, _) => InvalidateMeasure();
    }

    /// <inheritdoc />
    public bool AreHorizontalSnapPointsRegular
    {
        get => false;
        set { }
    }

    /// <inheritdoc />
    public bool AreVerticalSnapPointsRegular
    {
        get => false;
        set { }
    }

    /// <inheritdoc />
    public event EventHandler<RoutedEventArgs>? HorizontalSnapPointsChanged;

    /// <inheritdoc />
#pragma warning disable CS0067 // IScrollSnapPointsInfo requires the vertical event; the carousel is horizontal-only.
    public event EventHandler<RoutedEventArgs>? VerticalSnapPointsChanged;
#pragma warning restore CS0067

    /// <summary>Raised when arrange determines a different item is closest to its snap position.</summary>
    internal event EventHandler? CurrentItemChanged;

    /// <summary>The strategy currently in use; empty for the aspect-ratio extension.</summary>
    internal CarouselStrategy Strategy => _strategy;

    /// <summary>The item closest to its snapped position at the last arrange.</summary>
    internal int CurrentItem => _currentItem;

    /// <summary>The fraction of the last item, used to keep the anchor across resizes.</summary>
    internal double CurrentOffsetFraction => _currentFraction;

    /// <summary>The index of the first realized (windowed) item, or -1.</summary>
    internal int FirstRealizedIndex => _realized.Count == 0 ? -1 : _realized[0].Index;

    /// <summary>The index of the last realized (windowed) item, or -1.</summary>
    internal int LastRealizedIndex => _realized.Count == 0 ? -1 : _realized[^1].Index;

    /// <summary>Number of currently realized containers, exposed for diagnostics.</summary>
    internal int RealizedCount => _realized.Count;

    private int ItemCount => Items.Count;

    private MaterialCarousel? Owner => _owner;

    private bool IsRightToLeft => FlowDirection == Avalonia.Media.FlowDirection.RightToLeft;

    private bool IsAspectLayout => Owner?.Layout == MaterialCarouselLayout.MultiAspectRatio;

    private double PhysicalStartPadding => Owner?.Padding.Left ?? 0;

    private double PhysicalEndPadding => Owner?.Padding.Right ?? 0;

    private double BeforeContentPadding =>
        Owner is null ? 0 : IsRightToLeft ? Owner.Padding.Right : Owner.Padding.Left;

    private double AfterContentPadding =>
        Owner is null ? 0 : IsRightToLeft ? Owner.Padding.Left : Owner.Padding.Right;

    private double CrossPaddingStart => Owner?.Padding.Top ?? 0;

    private double CrossPaddingEnd => Owner?.Padding.Bottom ?? 0;

    /// <summary>
    /// Physical left edge of the first item slot. The pinned strategy layouts have no physical
    /// main-axis inset (the content padding lives in the shifted keyline steps); the aspect-ratio
    /// extension, which has no strategy, keeps the padding as a real inset.
    /// </summary>
    private double ContentStart => IsAspectLayout ? PhysicalStartPadding : 0;

    /// <summary>Scrollable length of the strip, matching the strategy's own scroll range.</summary>
    private double MainAxisExtent => IsAspectLayout
        ? PhysicalStartPadding + _contentWidth + PhysicalEndPadding
        : _contentWidth;

    /// <summary>
    /// Width the logical arrangement is mirrored around for right-to-left flow. When the strip is
    /// narrower than the viewport the empty space belongs before the content (on the left), so the
    /// viewport is the mirror; otherwise the content itself is. The aspect-ratio extension keeps a
    /// physical main-axis inset, so it mirrors inside the padded box.
    /// </summary>
    private double MirrorWidth => IsAspectLayout
        ? Math.Max(_contentWidth, Math.Max(0, _viewportWidth - PhysicalStartPadding - PhysicalEndPadding))
        : Math.Max(_contentWidth, _strategy.AvailableSpace);

    /// <summary>Maximum scroll offset of the current arrangement, never negative.</summary>
    internal double MaxScrollOffset
    {
        get
        {
            if (IsAspectLayout)
                return Math.Max(0, MainAxisExtent - _viewportWidth);
            return _strategy.GetMaxScrollOffset(ItemCount);
        }
    }

    private double BeyondViewportCount => Owner?.Layout switch
    {
        // Pinned variants pass 2 non-focal items to the pager for multi-browse/hero, 0 for
        // uncontained. Full-screen pages are a single item.
        MaterialCarouselLayout.Uncontained => 0,
        MaterialCarouselLayout.MultiAspectRatio => 0,
        MaterialCarouselLayout.FullScreen => 1,
        _ => 2,
    };

    /// <inheritdoc />
    public IReadOnlyList<double> GetIrregularSnapPoints(Orientation orientation, SnapPointsAlignment snapPointsAlignment)
    {
        if (orientation != Orientation.Horizontal)
            return _emptyPoints;

        // The pinned KeylineSnapPosition has no alignment notion; the control theme aligns Near.
        return snapPointsAlignment switch
        {
            SnapPointsAlignment.Center => _centerPoints,
            SnapPointsAlignment.Far => _farPoints,
            _ => _nearPoints,
        };
    }

    /// <inheritdoc />
    public double GetRegularSnapPoints(Orientation orientation, SnapPointsAlignment snapPointsAlignment, out double offset)
    {
        offset = 0;
        return 0;
    }

    /// <summary>Requests that the next measure relates the offset to <paramref name="itemIndex"/>.</summary>
    internal void RequestAnchor(int itemIndex, double fraction = 0)
    {
        _pendingAnchorItem = itemIndex;
        _pendingAnchorFraction = fraction;
    }

    /// <summary>Asks the owning carousel to scroll; the panel only supplies the target offset.</summary>
    internal void ScrollToIndex(int index, bool animate) => Owner?.ScrollToItemCore(index, animate);

    /// <summary>Logical target offset for an item's snapped position.</summary>
    internal double GetTargetOffset(int index)
    {
        if (IsAspectLayout)
            return index >= 0 && index < _aspectStarts.Length ? _aspectStarts[index] : 0;
        return _strategy.GetContentOffsetForItem(index, ItemCount, MaxScrollOffset);
    }

    /// <summary>Offset fraction of an item in the aspect-ratio extension.</summary>
    internal double GetAspectOffsetFraction(int index)
    {
        if (!IsAspectLayout || index < 0 || index >= _aspectStarts.Length)
            return 0;
        var logical = ResolveLogicalOffset();
        var width = Math.Max(1, _aspectWidths[index]);
        return Math.Clamp((logical - _aspectStarts[index]) / width, -0.5, 0.5);
    }

    /// <summary>Physical offset for a logical offset, mirrored for right-to-left flow.</summary>
    internal double ToPhysicalOffset(double logicalOffset)
    {
        var max = MaxScrollOffset;
        var clamped = Math.Clamp(logicalOffset, 0, max);
        return IsRightToLeft ? Math.Max(0, max - clamped) : clamped;
    }

    /// <summary>Logical offset for a physical <see cref="ScrollViewer"/> offset.</summary>
    internal double ToLogicalOffset(double physicalOffset)
    {
        var max = MaxScrollOffset;
        var clamped = Math.Clamp(physicalOffset, 0, max);
        return IsRightToLeft ? Math.Max(0, max - clamped) : clamped;
    }

    /// <summary>The item index closest to its snap position at the current offset.</summary>
    internal int GetCurrentItem()
    {
        var count = ItemCount;
        if (count == 0)
            return -1;
        var logical = ResolveLogicalOffset(count);
        if (IsAspectLayout)
            return GetAspectCurrentItem(count, logical);
        return _strategy.GetCurrentItem(count, logical);
    }

    internal Control? GetRealizedContainer(int index)
    {
        foreach (var item in _realized)
        {
            if (item.Index == index)
                return item.Container;
        }

        return null;
    }

    /// <summary>Re-resolves the owner after the template is applied.</summary>
    internal void RefreshOwner()
    {
        _owner = ResolveOwner();
        AttachScrollViewer();
        InvalidateMeasure();
    }

    /// <inheritdoc />
    protected override Control? ScrollIntoView(int index)
    {
        ScrollToIndex(index, animate: false);
        return GetRealizedContainer(index);
    }

    /// <inheritdoc />
    protected override Control? ContainerFromIndex(int index) => GetRealizedContainer(index);

    /// <inheritdoc />
    protected override int IndexFromContainer(Control container)
    {
        foreach (var item in _realized)
        {
            if (ReferenceEquals(item.Container, container))
                return item.Index;
        }

        return -1;
    }

    /// <inheritdoc />
    protected override IEnumerable<Control>? GetRealizedContainers()
    {
        var containers = new Control[_realized.Count];
        for (var i = 0; i < containers.Length; i++)
            containers[i] = _realized[i].Container;
        return containers;
    }

    /// <inheritdoc />
    protected override IInputElement? GetControl(NavigationDirection direction, IInputElement? from, bool wrap)
    {
        var count = ItemCount;
        if (count == 0)
            return null;

        var index = from is Control control ? IndexFromContainer(control) : -1;
        if (index < 0 && from is Visual visual)
        {
            foreach (var ancestor in visual.GetVisualAncestors())
            {
                if (ancestor is Control candidate && (index = IndexFromContainer(candidate)) >= 0)
                    break;
            }
        }

        var page = Math.Max(1, (int)Math.Round(_viewportWidth / Math.Max(1, _strategy.ItemSizeWithSpacing)));
        int? next = direction switch
        {
            NavigationDirection.First => 0,
            NavigationDirection.Last => count - 1,
            NavigationDirection.Next => index < 0 ? 0 : index + 1,
            NavigationDirection.Previous => index < 0 ? count - 1 : index - 1,
            NavigationDirection.Left => index < 0 ? (IsRightToLeft ? count - 1 : 0) : index + (IsRightToLeft ? 1 : -1),
            NavigationDirection.Right => index < 0 ? (IsRightToLeft ? 0 : count - 1) : index + (IsRightToLeft ? -1 : 1),
            NavigationDirection.PageUp => index < 0 ? 0 : index - page,
            NavigationDirection.PageDown => index < 0 ? count - 1 : index + page,
            _ => null,
        };

        if (next is not { } value)
            return null;
        if (wrap)
            value = ((value % count) + count) % count;

        value = Math.Clamp(value, 0, count - 1);
        ScrollToIndex(value, animate: false);
        return GetRealizedContainer(value);
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _owner = ResolveOwner();
        AttachScrollViewer();
        if (_owner is { } owner)
            owner.SizeChanged += OnOwnerSizeChanged;
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_owner is { } owner)
            owner.SizeChanged -= OnOwnerSizeChanged;
        DetachScrollViewer();
        _owner = null;
        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc />
    protected override void OnItemsChanged(IReadOnlyList<object?> items, NotifyCollectionChangedEventArgs e)
    {
        base.OnItemsChanged(items, e);
        Owner?.HandleItemsChangedFromPanel(e);
        RecycleAll();
        _currentItem = -1;
        _snapCacheKey = default;
        _hasBuilt = false;
        InvalidateMeasure();
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        AttachScrollViewer();
        var owner = Owner;
        if (owner is null)
        {
            RecycleAll();
            return default;
        }

        ResolveViewport(availableSize);
        _crossSize = ResolveCrossSize(owner, availableSize);
        var parameters = CaptureParameters(owner);
        if (!_hasBuilt || !parameters.Equals(_parameters))
        {
            if (_hasBuilt && _pendingAnchorItem < 0)
                CaptureAnchorBeforeMetricsChange();
            _parameters = parameters;
            _hasBuilt = true;
            BuildStrategy(owner);
        }
        else if (IsAspectLayout && _aspectMetricsDirty)
        {
            CaptureAnchorBeforeMetricsChange();
            BuildAspectMetrics(owner, _viewportWidth);
        }

        ApplyPendingOffset();

        var count = ItemCount;
        if (count == 0 || _crossSize <= 0 || _viewportWidth <= 0)
        {
            RecycleAll();
            UpdateSnapPointsCache(force: true);
            return new Size(Math.Max(0, MainAxisExtent), CrossPaddingStart + _crossSize + CrossPaddingEnd);
        }

        var logicalOffset = ResolveLogicalOffset(count);
        var window = GetRealizationWindowOrAspect(count, logicalOffset);
        RecycleOutside(window);
        for (var index = window.First; index <= window.Last; index++)
            RealizeItem(index);

        foreach (var item in _realized)
        {
            var mainSize = IsAspectLayout ? GetAspectItemWidth(item.Index) : _strategy.ItemMainAxisSize;
            item.Container.Measure(new Size(Math.Max(0, mainSize), Math.Max(0, _crossSize)));
        }

        UpdateSnapPointsCache(force: false);
        return new Size(
            Math.Max(0, MainAxisExtent),
            CrossPaddingStart + _crossSize + CrossPaddingEnd);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var count = ItemCount;
        if (count == 0 || !_strategy.IsValid && !IsAspectLayout)
            return finalSize;

        var logicalOffset = ResolveLogicalOffset(count);
        var rtl = IsRightToLeft;
        var contentStart = ContentStart;

        if (IsAspectLayout)
        {
            var current = GetAspectCurrentItem(count, logicalOffset);
            SetCurrentItem(current);
            foreach (var item in _realized)
            {
                var width = GetAspectItemWidth(item.Index);
                var start = item.Index < _aspectStarts.Length ? _aspectStarts[item.Index] : 0;
                var left = rtl ? contentStart + MirrorWidth - start - width : contentStart + start;
                item.Container.Arrange(new Rect(left, CrossPaddingStart, width, _crossSize));
                var effect = CarouselParallax.Calculate(width, _crossSize,
                    start - logicalOffset + ContentStart, 0, _viewportWidth);
                var maskStart = rtl ? width - effect.End : effect.Start;
                var mask = new Rect(maskStart, 0, Math.Max(0, effect.End - effect.Start), _crossSize);
                ApplyItemVisuals(item.Container, 0, mask, clip: mask.Width < width,
                    1 / (1 + Math.Abs(item.Index - current)),
                    item.Container is MaterialCarouselItem rounded ? rounded.CornerRadius.TopLeft : 0);
                if (item.Container is MaterialCarouselItem aspectItem)
                {
                    aspectItem.SetParallax(rtl ? -effect.Translation : effect.Translation);
                    aspectItem.UpdateItemInfo(mask.Width, width, width, mask);
                }
            }

            return finalSize;
        }

        var maxScroll = _strategy.GetMaxScrollOffset(count);
        var keylines = _strategy.GetKeylineListForScrollOffset(logicalOffset, maxScroll);
        var currentItem = _strategy.GetCurrentItem(count, logicalOffset);
        var fraction = _strategy.GetOffsetFraction(currentItem, count, logicalOffset);
        SetCurrentItem(currentItem);
        _currentFraction = fraction;

        var itemMainSize = _strategy.ItemMainAxisSize;
        var slot = _strategy.ItemSizeWithSpacing;

        foreach (var item in _realized)
        {
            var index = item.Index;
            var container = item.Container;
            // The strip is arranged end-to-end in unadjusted content coordinates; the scroll viewer
            // applies the scroll offset to the whole panel, and the translation pins each item to
            // its interpolated keyline (exactly the pinned modifier's placement + translation).
            var slotLeft = rtl
                ? contentStart + MirrorWidth - (index * slot) - itemMainSize
                : contentStart + (index * slot);

            var unadjustedCenter = (index * slot) + (itemMainSize / 2) - logicalOffset;
            var keylineBefore = keylines.GetKeylineBefore(unadjustedCenter);
            var keylineAfter = keylines.GetKeylineAfter(unadjustedCenter);
            var isOutOfKeylineBounds = keylineBefore.Equals(keylineAfter);
            var progress = isOutOfKeylineBounds
                ? 1
                : (unadjustedCenter - keylineBefore.UnadjustedOffset) /
                  (keylineAfter.UnadjustedOffset - keylineBefore.UnadjustedOffset);
            var interpolated = CarouselKeylineList.Interpolate(keylineBefore, keylineAfter, progress);

            var maskWidth = interpolated.Size;
            var maskLeft = (itemMainSize / 2) - (maskWidth / 2);
            var maskRight = (itemMainSize / 2) + (maskWidth / 2);

            var translation = interpolated.Offset - unadjustedCenter;
            if (isOutOfKeylineBounds)
            {
                // Past the first/last keyline the item keeps moving so it can leave the viewport.
                translation += (unadjustedCenter - interpolated.UnadjustedOffset) / interpolated.Size;
            }

            if (rtl)
                translation = -translation;

            container.Arrange(new Rect(slotLeft, CrossPaddingStart, itemMainSize, _crossSize));

            var clipRect = new Rect(maskLeft, 0, Math.Max(0, maskRight - maskLeft), _crossSize);
            var shouldClip = Math.Abs(maskLeft) > 0.01 || Math.Abs(maskRight - itemMainSize) > 0.01;
            ApplyItemVisuals(container, translation, clipRect, shouldClip, 1 / (1 + Math.Abs(index - currentItem)));

            if (container is MaterialCarouselItem carouselItem)
            {
                carouselItem.SetParallax(0);
                carouselItem.UpdateItemInfo(interpolated.Size, _strategy.MinItemSize, _strategy.MaxItemSize, clipRect);
            }
        }

        return finalSize;
    }

    private void SetCurrentItem(int index)
    {
        if (_currentItem == index)
            return;
        _currentItem = index;
        CurrentItemChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyItemVisuals(Control container, double translation, Rect maskRect, bool clip, double zIndex, double radius = 0)
    {
        if (!_visuals.TryGetValue(container, out var visuals))
        {
            visuals = new ItemVisuals();
            _visuals[container] = visuals;
            container.RenderTransform = visuals.Translation;
        }

        visuals.Translation.X = translation;
        visuals.Translation.Y = 0;
        visuals.SetClip(clip ? maskRect : null, radius);
        container.Clip = visuals.ClipGeometry;
        // Upstream layers items by 1/(1+distance); map that ordering to integer ZIndex.
        container.ZIndex = (int)Math.Round(zIndex * 1000);
    }

    private void RealizeItem(int index)
    {
        if (index < 0 || index >= ItemCount)
            return;
        foreach (var item in _realized)
        {
            if (item.Index == index)
                return;
        }

        var generator = ItemContainerGenerator;
        if (generator is null)
            return;

        var data = Items[index];
        Control container;
        if (generator.NeedsContainer(data, index, out var recycleKey))
        {
            var poolKey = recycleKey ?? typeof(MaterialCarouselItem);
            if (TryTakeRecycled(poolKey, out var recycled))
            {
                recycled.IsVisible = true;
                generator.PrepareItemContainer(recycled, data, index);
                AddInternalChild(recycled);
                generator.ItemContainerPrepared(recycled, data, index);
                container = recycled;
            }
            else
            {
                container = generator.CreateContainer(data, index, poolKey);
                container.SetValue(RecycleKeyProperty, poolKey);
                generator.PrepareItemContainer(container, data, index);
                AddInternalChild(container);
                generator.ItemContainerPrepared(container, data, index);
            }
        }
        else if (data is Control own)
        {
            container = own;
            if (!own.IsSet(RecycleKeyProperty))
            {
                generator.PrepareItemContainer(own, own, index);
                AddInternalChild(own);
                own.SetValue(RecycleKeyProperty, ItemIsItsOwnContainer);
                generator.ItemContainerPrepared(own, own, index);
            }

            own.IsVisible = true;
        }
        else
        {
            return;
        }

        InsertRealized(new RealizedItem(index, container));
    }

    private void InsertRealized(RealizedItem item)
    {
        var index = 0;
        while (index < _realized.Count && _realized[index].Index < item.Index)
            index++;
        _realized.Insert(index, item);
    }

    private void RecycleOutside((int First, int Last) window)
    {
        for (var i = _realized.Count - 1; i >= 0; i--)
        {
            var item = _realized[i];
            if (item.Index >= window.First && item.Index <= window.Last)
                continue;
            _realized.RemoveAt(i);
            RecycleContainer(item);
        }
    }

    private void RecycleAll()
    {
        for (var i = _realized.Count - 1; i >= 0; i--)
            RecycleContainer(_realized[i]);
        _realized.Clear();
    }

    private void RecycleContainer(RealizedItem item)
    {
        var container = item.Container;
        _visuals.Remove(container);
        container.Clip = null;
        container.RenderTransform = null;

        var recycleKey = container.GetValue(RecycleKeyProperty);
        var generator = ItemContainerGenerator;
        if (generator is null)
            return;

        if (recycleKey is null)
        {
            generator.ClearItemContainer(container);
            RemoveInternalChild(container);
        }
        else if (ReferenceEquals(recycleKey, ItemIsItsOwnContainer))
        {
            container.IsVisible = false;
        }
        else
        {
            generator.ClearItemContainer(container);
            if (!_recyclePool.TryGetValue(recycleKey, out var pool))
            {
                pool = new Stack<Control>();
                _recyclePool[recycleKey] = pool;
            }

            container.IsVisible = false;
            pool.Push(container);
            RemoveInternalChild(container);
        }
    }

    private bool TryTakeRecycled(object recycleKey, out Control container)
    {
        container = null!;
        if (!_recyclePool.TryGetValue(recycleKey, out var pool) || pool.Count == 0)
            return false;
        container = pool.Pop();
        return true;
    }

    private (int First, int Last) GetRealizationWindow(int count, double logicalOffset)
    {
        var slot = _strategy.ItemSizeWithSpacing;
        if (slot <= 0)
            return (0, count - 1);

        var beyond = BeyondViewportCount * slot;
        var first = (int)Math.Floor((logicalOffset - beyond) / slot);
        var last = (int)Math.Ceiling((logicalOffset + _strategy.AvailableSpace + beyond) / slot) - 1;
        first = Math.Clamp(first, 0, count - 1);
        last = Math.Clamp(last, first, count - 1);
        return (first, last);
    }

    private (int First, int Last) GetAspectWindow(int count, double logicalOffset)
    {
        if (_aspectStarts.Length != count || _aspectWidths.Length != count)
            return (0, count - 1);

        var found = Array.BinarySearch(_aspectStarts, logicalOffset);
        var first = found >= 0 ? found : ~found - 1;
        first = Math.Clamp(first - (int)BeyondViewportCount, 0, count - 1);

        var end = logicalOffset + _viewportWidth;
        var last = first;
        while (last + 1 < count && _aspectStarts[last + 1] < end)
            last++;
        last = Math.Clamp(last + (int)BeyondViewportCount, first, count - 1);
        return (first, last);
    }

    private (int First, int Last) GetRealizationWindowOrAspect(int count, double logical) =>
        count == 0 ? (0, -1) :
        IsAspectLayout ? GetAspectWindow(count, logical) : GetRealizationWindow(count, logical);

    private int GetAspectCurrentItem(int count, double logicalOffset)
    {
        if (_aspectStarts.Length != count || count == 0)
            return 0;

        var found = Array.BinarySearch(_aspectStarts, logicalOffset);
        if (found >= 0)
            return found;

        var after = ~found;
        var before = after - 1;
        if (before < 0)
            return 0;
        if (after >= count)
            return count - 1;
        return Math.Abs(logicalOffset - _aspectStarts[before]) <= Math.Abs(_aspectStarts[after] - logicalOffset)
            ? before
            : after;
    }

    private double GetAspectItemWidth(int index) =>
        index >= 0 && index < _aspectWidths.Length ? _aspectWidths[index] : 0;

    private void BuildStrategy(MaterialCarousel owner)
    {
        // The pinned CarouselPageSize passes the full container width to the keyline factory and
        // forwards the content padding separately, so the strategy owns the main-axis padding.
        var availableSpace = Math.Max(0, _viewportWidth);
        var spacing = Math.Max(0, owner.ItemSpacing);
        var itemCount = ItemCount;
        var preferences = CapturePreferences(owner);
        var preferredSize = double.IsFinite(preferences.PreferredSize) ? preferences.PreferredSize : availableSpace;
        var itemSize = double.IsFinite(preferences.ItemSize) ? preferences.ItemSize : availableSpace;

        CarouselKeylineList keylines;
        switch (owner.Layout)
        {
            case MaterialCarouselLayout.Uncontained:
                keylines = CarouselKeylines.Uncontained(
                    availableSpace,
                    itemSize,
                    spacing,
                    preferences.MinSmallSize,
                    preferences.MaxSmallSize);
                break;
            case MaterialCarouselLayout.CenterAlignedHero:
                keylines = CarouselKeylines.Hero(
                    availableSpace,
                    preferredSize,
                    spacing,
                    itemCount,
                    isCentered: true,
                    preferences.MinSmallSize,
                    preferences.MaxSmallSize);
                break;
            case MaterialCarouselLayout.Hero:
                keylines = CarouselKeylines.Hero(
                    availableSpace,
                    preferredSize,
                    spacing,
                    itemCount,
                    isCentered: false,
                    preferences.MinSmallSize,
                    preferences.MaxSmallSize);
                break;
            case MaterialCarouselLayout.FullScreen:
                keylines = CarouselKeylines.FullScreen(availableSpace);
                break;
            case MaterialCarouselLayout.MultiAspectRatio:
                keylines = CarouselKeylineList.Empty;
                break;
            default:
                keylines = CarouselKeylines.MultiBrowse(
                    availableSpace,
                    preferredSize,
                    spacing,
                    itemCount,
                    preferences.MinSmallSize,
                    preferences.MaxSmallSize);
                break;
        }

        _strategy = new CarouselStrategy(
            keylines,
            availableSpace,
            spacing,
            BeforeContentPadding,
            AfterContentPadding);
        RecycleAll();
        _snapCacheKey = default;
        _aspectStarts = [];
        _aspectWidths = [];
        _contentWidth = _strategy.GetContentSize(itemCount);
        if (owner.Layout == MaterialCarouselLayout.MultiAspectRatio)
            BuildAspectMetrics(owner, availableSpace);
    }

    private void BuildAspectMetrics(MaterialCarousel owner, double availableSpace)
    {
        var count = ItemCount;
        var spacing = Math.Max(0, owner.ItemSpacing);
        if (_aspectStarts.Length != count) _aspectStarts = new double[count];
        if (_aspectWidths.Length != count) _aspectWidths = new double[count];
        var total = 0d;
        for (var i = 0; i < count; i++)
        {
            var card = GetRealizedContainer(i) as MaterialCarouselItem ?? Items[i] as MaterialCarouselItem;
            var ratio = owner.ResolveAspectRatio(card, i);
            var width = Math.Max(1, _crossSize > 0 ? _crossSize * ratio : availableSpace);
            _aspectStarts[i] = total;
            _aspectWidths[i] = width;
            total += width;
            if (i < count - 1)
                total += spacing;
        }

        _contentWidth = total;
        _aspectMetricsDirty = false;
        _snapCacheKey = default;
    }

    private void CaptureAnchorBeforeMetricsChange()
    {
        if (!_strategy.IsValid && !IsAspectLayout)
            return;
        if (_currentItem < 0)
            return;

        _pendingAnchorItem = _currentItem;
        _pendingAnchorFraction = IsAspectLayout ? GetAspectOffsetFraction(_currentItem) : _currentFraction;
    }

    private void ApplyPendingOffset()
    {
        if (_applyingPendingOffset || _scrollViewer is null)
            return;

        double target;
        if (_pendingAnchorItem >= 0 && ItemCount > 0)
        {
            var index = Math.Clamp(_pendingAnchorItem, 0, ItemCount - 1);
            target = IsAspectLayout
                ? GetAspectAnchorOffset(index, _pendingAnchorFraction)
                : _strategy.GetOffsetForAnchor(index, ItemCount, _pendingAnchorFraction, MaxScrollOffset);
            _pendingAnchorItem = -1;
        }
        else if (!double.IsNaN(_pendingOffset))
        {
            target = Math.Clamp(_pendingOffset, 0, MaxScrollOffset);
            _pendingOffset = double.NaN;
        }
        else
        {
            return;
        }

        target = Math.Clamp(target, 0, MaxScrollOffset);
        var physical = ToPhysicalOffset(target);
        var offset = _scrollViewer.Offset;
        if (Math.Abs(offset.X - physical) < 0.5)
        {
            _pendingApplyAttempts = 0;
            return;
        }

        _applyingPendingOffset = true;
        try
        {
            _scrollViewer.SetCurrentValue(ScrollViewer.OffsetProperty, new Vector(physical, offset.Y));
        }
        finally
        {
            _applyingPendingOffset = false;
        }

        // The extent may still describe the previous metrics, in which case the scroll viewer clamps
        // the offset; ask for another measure pass so the retry runs against the new extent.
        if (Math.Abs(_scrollViewer.Offset.X - physical) > 0.5 && _pendingApplyAttempts++ < 3)
        {
            _pendingOffset = target;
            InvalidateMeasure();
        }
        else
        {
            _pendingApplyAttempts = 0;
        }
    }

    private double GetAspectAnchorOffset(int index, double fraction)
    {
        if (index < 0 || index >= _aspectStarts.Length)
            return 0;
        var width = Math.Max(0, _aspectWidths[index]);
        return Math.Clamp(_aspectStarts[index] + (fraction * width), 0, MaxScrollOffset);
    }

    private LayoutParameters CaptureParameters(MaterialCarousel owner)
    {
        var availableSpace = Math.Max(0, _viewportWidth);
        return new LayoutParameters(
            owner.Layout,
            availableSpace,
            Math.Max(0, owner.ItemSpacing),
            BeforeContentPadding,
            AfterContentPadding,
            IsRightToLeft,
            ItemCount,
            IsAspectLayout ? _crossSize : 0,
            CapturePreferences(owner));
    }

    private static ItemPreferences CapturePreferences(MaterialCarousel owner) =>
        new(
            double.IsFinite(owner.PreferredItemWidth) ? Math.Max(0, owner.PreferredItemWidth) : double.NaN,
            double.IsFinite(owner.ItemWidth) ? Math.Max(0, owner.ItemWidth) : double.NaN,
            double.IsFinite(owner.MinSmallItemWidth) ? Math.Max(0, owner.MinSmallItemWidth) : CarouselDefaults.MinSmallItemSize,
            double.IsFinite(owner.MaxSmallItemWidth) ? Math.Max(0, owner.MaxSmallItemWidth) : CarouselDefaults.MaxSmallItemSize);

    private double ResolveCrossSize(MaterialCarousel owner, Size availableSize)
    {
        var padding = CrossPaddingStart + CrossPaddingEnd;
        var viewport = _viewportHeight;
        if (viewport <= 0 && double.IsFinite(availableSize.Height) && availableSize.Height > 0)
            viewport = availableSize.Height;
        if (viewport <= 0 && owner.Bounds.Height > 0)
            viewport = owner.Bounds.Height;

        var available = Math.Max(0, viewport - padding);
        if (double.IsFinite(owner.ItemHeight) && owner.ItemHeight > 0)
            return Math.Min(owner.ItemHeight, available <= 0 ? owner.ItemHeight : available);
        return available;
    }

    private double ResolveLogicalOffset() => ResolveLogicalOffset(ItemCount);

    private double ResolveLogicalOffset(int count)
    {
        if (_scrollViewer is null)
            return 0;

        return Math.Clamp(ToLogicalOffset(_scrollViewer.Offset.X), 0, MaxScrollOffset);
    }

    private void ResolveViewport(Size availableSize)
    {
        var width = 0d;
        var height = 0d;
        if (_scrollViewer is { } viewer)
        {
            width = viewer.Viewport.Width;
            height = viewer.Viewport.Height;
        }

        if (width <= 0 && double.IsFinite(availableSize.Width) && availableSize.Width > 0)
            width = availableSize.Width;
        if (width <= 0 && Owner is { Bounds.Width: > 0 } owner)
            width = owner.Bounds.Width;

        if (height <= 0 && double.IsFinite(availableSize.Height) && availableSize.Height > 0)
            height = availableSize.Height;
        if (height <= 0 && Owner is { Bounds.Height: > 0 } boundsOwner)
            height = boundsOwner.Bounds.Height;

        _viewportWidth = Math.Max(0, width);
        _viewportHeight = Math.Max(0, height);
    }

    private void AttachScrollViewer()
    {
        var resolved = this.FindAncestorOfType<ScrollViewer>();
        if (ReferenceEquals(resolved, _scrollViewer))
            return;

        DetachScrollViewer();
        _scrollViewer = resolved;
        if (_scrollViewer is not null)
        {
            _scrollViewer.ScrollChanged += OnScrollChanged;
            _scrollViewer.PropertyChanged += OnScrollViewerPropertyChanged;
        }
    }

    private void DetachScrollViewer()
    {
        if (_scrollViewer is null)
            return;
        _scrollViewer.ScrollChanged -= OnScrollChanged;
        _scrollViewer.PropertyChanged -= OnScrollViewerPropertyChanged;
        _scrollViewer = null;
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_applyingPendingOffset || !_hasBuilt)
            return;

        var window = GetRealizationWindowOrAspect(ItemCount, ResolveLogicalOffset());
        var windowChanged = false;
        foreach (var item in _realized)
        {
            if (item.Index < window.First || item.Index > window.Last)
            {
                windowChanged = true;
                break;
            }
        }

        if (windowChanged || _realized.Count == 0)
            InvalidateMeasure();
        else
            InvalidateArrange();
    }

    private void OnScrollViewerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ScrollViewer.ViewportProperty && _scrollViewer is { } viewer &&
            (viewer.Viewport.Width != _viewportWidth || viewer.Viewport.Height != _viewportHeight))
        {
            InvalidateMeasure();
        }
    }

    private void OnOwnerSizeChanged(object? sender, SizeChangedEventArgs e) => InvalidateMeasure();

    private void UpdateSnapPointsCache(bool force)
    {
        var count = ItemCount;
        var maxScroll = MaxScrollOffset;
        var key = new SnapCacheKey(
            IsAspectLayout ? null : _strategy,
            count,
            IsRightToLeft,
            maxScroll,
            Owner?.Layout ?? MaterialCarouselLayout.Hero,
            _strategy.AvailableSpace,
            _strategy.ItemSizeWithSpacing);
        if (!force && _nearPoints.Length == count && key.Equals(_snapCacheKey))
            return;

        var nextNear = new double[count];
        var nextCenter = new double[count];
        var nextFar = new double[count];
        var itemSize = _strategy.ItemMainAxisSize;
        for (var i = 0; i < count; i++)
        {
            double logical;
            double size;
            if (IsAspectLayout)
            {
                logical = i < _aspectStarts.Length ? _aspectStarts[i] : 0;
                size = i < _aspectWidths.Length ? _aspectWidths[i] : 0;
            }
            else if (_strategy.IsValid)
            {
                logical = Math.Clamp((i * _strategy.ItemSizeWithSpacing) - _strategy.GetSnapPositionOffset(i, count), 0, maxScroll);
                size = itemSize;
            }
            else
            {
                logical = 0;
                size = 0;
            }

            var point = IsRightToLeft ? Math.Max(0, maxScroll - logical) : logical;
            nextNear[i] = point;
            nextCenter[i] = point + (size / 2);
            nextFar[i] = point + size;
        }

        var changed = !PointsEqual(_nearPoints, nextNear);
        _nearPoints = nextNear;
        _centerPoints = nextCenter;
        _farPoints = nextFar;
        _snapCacheKey = key;
        if (changed)
            HorizontalSnapPointsChanged?.Invoke(this, new RoutedEventArgs());
    }

    private static bool PointsEqual(double[] left, double[] right)
    {
        if (left.Length != right.Length)
            return false;
        for (var i = 0; i < left.Length; i++)
        {
            if (Math.Abs(left[i] - right[i]) > 0.001)
                return false;
        }

        return true;
    }

    private MaterialCarousel? ResolveOwner()
    {
        if (ItemsControl is MaterialCarousel direct)
            return direct;

        foreach (var ancestor in this.GetVisualAncestors())
        {
            if (ancestor is MaterialCarousel carousel)
                return carousel;
        }

        return null;
    }

    private readonly record struct RealizedItem(int Index, Control Container);

    private readonly record struct ItemPreferences(
        double PreferredSize,
        double ItemSize,
        double MinSmallSize,
        double MaxSmallSize);

    private readonly record struct LayoutParameters(
        MaterialCarouselLayout Layout,
        double AvailableSpace,
        double ItemSpacing,
        double BeforePadding,
        double AfterPadding,
        bool IsRightToLeft,
        int ItemCount,
        double AspectCrossSize,
        ItemPreferences Preferences);

    private readonly record struct SnapCacheKey(
        CarouselStrategy? Strategy,
        int ItemCount,
        bool IsRightToLeft,
        double MaxScroll,
        MaterialCarouselLayout Layout,
        double AvailableSpace,
        double ItemSizeWithSpacing);

    private sealed class ItemVisuals
    {
        public TranslateTransform Translation { get; } = new();
        public RectangleGeometry? ClipGeometry { get; private set; }

        public void SetClip(Rect? rect, double radius)
        {
            if (rect is null)
            {
                ClipGeometry = null;
                return;
            }

            ClipGeometry ??= new RectangleGeometry();
            ClipGeometry.Rect = rect.Value;
            ClipGeometry.RadiusX = ClipGeometry.RadiusY = Math.Min(radius,
                Math.Min(rect.Value.Width, rect.Value.Height) / 2);
        }
    }
}