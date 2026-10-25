using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// The horizontal item strip used by <see cref="MaterialCarousel"/>.
/// </summary>
/// <remarks>
/// Item widths follow the source carousel building blocks. The five Material layouts use a fixed
/// ladder of sizes per <see cref="MaterialCarouselContext"/>; the source frames are reproduced
/// exactly at the source content width (380 units on mobile, 568 on tablet) and the ladder is
/// scaled to the available width, so the first page always fills the strip. Slots past the ladder
/// keep the last (smallest) size, which is what the source frames show for their trailing peeks.
/// <see cref="MaterialCarouselLayout.MultiAspectRatio"/> instead derives each width from the
/// item's <see cref="MaterialCarouselItem.AspectRatio"/> and the carousel item height.
/// The panel applies the owning carousel's <see cref="Control.Padding"/>, publishes item starts
/// as scroll snap points, and implements <see cref="INavigableContainer"/> so the ListBox
/// keyboard model works without a second navigation implementation.
/// </remarks>
public sealed class MaterialCarouselPanel : Panel, IScrollSnapPointsInfo, INavigableContainer
{
    // Source sizes in DIPs. Each row plus the 8 unit gaps fills the source content width:
    // mobile hero 316 + 56 = 380, center-aligned hero 56 + 252 + 56 = 380,
    // multi-browse 188 + 120 + 56 = 380, uncontained 154 + 154 + 72 = 396 (no end padding),
    // tablet 184 + 184 + 120 + 56 = 568, tablet uncontained 163 + 163 + 163 + 72 = 584.
    private static readonly double[] MobileHeroSizes = [316, 56];
    private static readonly double[] MobileCenterAlignedHeroSizes = [56, 252, 56];
    private static readonly double[] MobileMultiBrowseSizes = [188, 120, 56];
    private static readonly double[] MobileUncontainedSizes = [154, 154, 72];
    private static readonly double[] TabletHeroSizes = [184, 184, 120, 56];
    private static readonly double[] TabletUncontainedSizes = [162.6667, 162.6667, 162.6667, 72];

    // Source content widths, used only before the carousel has been arranged.
    private const double MobileFallbackWidth = 412;
    private const double TabletFallbackWidth = 600;
    private const double FallbackHeight = 892;

    private readonly List<Control> _visible = [];
    private double[] _widths = [];
    private double[] _starts = [];
    private double[] _snapStarts = [];
    private double[] _snapWidths = [];
    private double[]? _nearPoints;
    private double[]? _centerPoints;
    private double[]? _farPoints;
    private double _itemHeight;
    private MaterialCarousel? _owner;

    /// <inheritdoc />
    public bool AreHorizontalSnapPointsRegular { get => false; set { } }

    /// <inheritdoc />
    public bool AreVerticalSnapPointsRegular { get => false; set { } }

    /// <inheritdoc />
    public event EventHandler<RoutedEventArgs> HorizontalSnapPointsChanged = delegate { };

    /// <inheritdoc />
    public event EventHandler<RoutedEventArgs> VerticalSnapPointsChanged = delegate { };

    private MaterialCarousel? Owner => _owner ??= ResolveOwner();

    /// <inheritdoc />
    public IReadOnlyList<double> GetIrregularSnapPoints(Orientation orientation, SnapPointsAlignment snapPointsAlignment)
    {
        if (orientation != Orientation.Horizontal)
            return Array.Empty<double>();

        return snapPointsAlignment switch
        {
            SnapPointsAlignment.Center => _centerPoints ??= OffsetPoints(0.5),
            SnapPointsAlignment.Far => _farPoints ??= OffsetPoints(1),
            _ => _nearPoints ??= OffsetPoints(0),
        };
    }

    /// <inheritdoc />
    public double GetRegularSnapPoints(Orientation orientation, SnapPointsAlignment snapPointsAlignment, out double offset)
    {
        offset = 0;
        return 0;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _owner = ResolveOwner();
        if (_owner is { } owner)
            owner.SizeChanged += OnOwnerSizeChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_owner is { } owner)
            owner.SizeChanged -= OnOwnerSizeChanged;
        _owner = null;
        base.OnDetachedFromVisualTree(e);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var owner = Owner;
        var padding = owner?.Padding ?? default;
        var gap = owner?.ItemSpacing ?? 8;
        var layout = owner?.Layout ?? MaterialCarouselLayout.Hero;
        var context = owner?.Context ?? MaterialCarouselContext.Mobile;
        var itemHeight = ResolveItemHeight(layout, availableSize, owner);
        _itemHeight = itemHeight;
        var contentWidth = ResolveContentWidth(availableSize, owner, context);
        var usable = Math.Max(0, contentWidth - padding.Left - padding.Right);

        _visible.Clear();
        foreach (var child in Children)
            if (child.IsVisible)
                _visible.Add(child);

        if (_widths.Length < _visible.Count)
            _widths = new double[_visible.Count];

        var ladder = layout is MaterialCarouselLayout.FullScreen or MaterialCarouselLayout.MultiAspectRatio
            ? null
            : GetLadder(layout, context);
        var slots = ladder is null ? 0 : Math.Min(ladder.Length, _visible.Count);
        var ladderTotal = 0d;
        for (var i = 0; i < slots; i++)
            ladderTotal += ladder![i];
        var ladderUsable = Math.Max(0, usable - gap * Math.Max(0, slots - 1));

        var width = padding.Left + padding.Right;
        var smallest = 0d;
        for (var i = 0; i < _visible.Count; i++)
        {
            var child = _visible[i];
            double itemWidth;
            if (layout == MaterialCarouselLayout.FullScreen)
            {
                itemWidth = usable;
            }
            else if (layout == MaterialCarouselLayout.MultiAspectRatio)
            {
                var ratio = child is MaterialCarouselItem item ? item.AspectRatio : 16d / 9d;
                itemWidth = Math.Max(0, itemHeight * ratio);
            }
            else if (i < slots && ladderTotal > 0)
            {
                itemWidth = ladderUsable * ladder![i] / ladderTotal;
            }
            else
            {
                itemWidth = smallest;
            }

            _widths[i] = itemWidth;
            smallest = itemWidth;
            child.Measure(new Size(itemWidth, itemHeight));
            width += itemWidth;
            if (i < _visible.Count - 1)
                width += gap;
        }

        return new Size(Math.Max(0, width), itemHeight + padding.Top + padding.Bottom);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var owner = Owner;
        var padding = owner?.Padding ?? default;
        var gap = owner?.ItemSpacing ?? 8;
        var count = Math.Min(_visible.Count, _widths.Length);
        if (_starts.Length < count)
            _starts = new double[count];

        // Cards keep the measured height so a stretched carousel does not stretch the source
        // 204 unit card; the full-screen layout measures to the viewport, so the minimum wins.
        var available = Math.Max(0, finalSize.Height - padding.Top - padding.Bottom);
        var itemHeight = _itemHeight > 0 ? Math.Min(_itemHeight, available) : available;
        var x = padding.Left;
        for (var i = 0; i < count; i++)
        {
            var child = _visible[i];
            var width = Math.Max(0, _widths[i]);
            _starts[i] = x;
            child.Arrange(new Rect(x, padding.Top, width, itemHeight));
            x += width + gap;
        }

        UpdateSnapPoints(count);
        return finalSize;
    }

    IInputElement? INavigableContainer.GetControl(NavigationDirection direction, IInputElement? from, bool wrap)
    {
        var count = Children.Count;
        if (count == 0)
            return null;

        var rtl = FlowDirection == Avalonia.Media.FlowDirection.RightToLeft;
        var index = from is Control control ? Children.IndexOf(control) : -1;
        int? next = direction switch
        {
            NavigationDirection.First => 0,
            NavigationDirection.Last => count - 1,
            NavigationDirection.Next => index < 0 ? 0 : index + 1,
            NavigationDirection.Previous => index < 0 ? count - 1 : index - 1,
            NavigationDirection.Left => index < 0 ? 0 : index + (rtl ? 1 : -1),
            NavigationDirection.Right => index < 0 ? count - 1 : index + (rtl ? -1 : 1),
            NavigationDirection.PageUp => index < 0 ? 0 : Math.Max(0, index - 1),
            NavigationDirection.PageDown => index < 0 ? 0 : Math.Min(count - 1, index + 1),
            // The strip is horizontal: vertical arrows are not a navigation axis.
            _ => null,
        };

        if (next is not { } value)
            return null;
        if (wrap)
            value = ((value % count) + count) % count;

        return value >= 0 && value < count ? Children[value] : null;
    }

    /// <summary>Returns the leading edge of a realized child in panel coordinates, or null.</summary>
    internal double? GetItemStart(Control container)
    {
        var index = Children.IndexOf(container);
        return index >= 0 && index < _starts.Length ? _starts[index] : null;
    }

    private MaterialCarousel? ResolveOwner()
    {
        if (TemplatedParent is MaterialCarousel direct)
            return direct;

        for (var current = this.GetVisualParent(); current is not null; current = current.GetVisualParent())
            if (current is MaterialCarousel carousel)
                return carousel;

        return null;
    }

    private void OnOwnerSizeChanged(object? sender, SizeChangedEventArgs e) => InvalidateMeasure();

    private static double ResolveItemHeight(MaterialCarouselLayout layout, Size availableSize, MaterialCarousel? owner)
    {
        if (layout != MaterialCarouselLayout.FullScreen)
            return owner?.ItemHeight ?? 204;

        if (double.IsFinite(availableSize.Height) && availableSize.Height > 0)
            return Math.Max(0, availableSize.Height - (owner?.Padding.Top ?? 0) - (owner?.Padding.Bottom ?? 0));
        if (owner is { Bounds.Height: > 0 })
            return Math.Max(0, owner.Bounds.Height - owner.Padding.Top - owner.Padding.Bottom);
        return FallbackHeight;
    }

    private static double ResolveContentWidth(Size availableSize, MaterialCarousel? owner, MaterialCarouselContext context)
    {
        // A horizontally scrolling presenter measures its child with an infinite width, so the
        // strip falls back to the arranged carousel width (padding is applied by this panel).
        if (owner is { Bounds.Width: > 0 })
            return owner.Bounds.Width;

        if (double.IsFinite(availableSize.Width) && availableSize.Width > 0)
            return availableSize.Width;

        return context == MaterialCarouselContext.Tablet ? TabletFallbackWidth : MobileFallbackWidth;
    }

    private static double[] GetLadder(MaterialCarouselLayout layout, MaterialCarouselContext context)
    {
        var tablet = context == MaterialCarouselContext.Tablet;
        return layout switch
        {
            MaterialCarouselLayout.CenterAlignedHero => tablet ? TabletHeroSizes : MobileCenterAlignedHeroSizes,
            MaterialCarouselLayout.MultiBrowse => tablet ? TabletHeroSizes : MobileMultiBrowseSizes,
            MaterialCarouselLayout.Uncontained => tablet ? TabletUncontainedSizes : MobileUncontainedSizes,
            // The tablet hero, center-aligned hero, and multi-browse frames share one ladder in
            // the source; only the mobile context distinguishes them.
            _ => tablet ? TabletHeroSizes : MobileHeroSizes,
        };
    }

    private void UpdateSnapPoints(int count)
    {
        var changed = _snapStarts.Length != count;
        for (var i = 0; !changed && i < count; i++)
            changed = _snapStarts[i] != _starts[i] || _snapWidths[i] != _widths[i];
        if (!changed)
            return;

        _snapStarts = _starts[..count];
        _snapWidths = _widths[..count];
        _nearPoints = null;
        _centerPoints = null;
        _farPoints = null;
        HorizontalSnapPointsChanged(this, new RoutedEventArgs());
    }

    private double[] OffsetPoints(double fraction)
    {
        // Snap points are offset by the leading padding so a snapped item keeps the same inset
        // as the initial resting position (offset 0), which is the source's 16 unit start padding.
        var inset = Owner?.Padding.Left ?? 0;
        var points = new double[_snapStarts.Length];
        for (var i = 0; i < points.Length; i++)
            points[i] = Math.Max(0, _snapStarts[i] - inset + _snapWidths[i] * fraction);
        return points;
    }
}
