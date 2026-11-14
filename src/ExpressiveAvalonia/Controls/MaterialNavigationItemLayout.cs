using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace ExpressiveAvalonia.Controls;

/// <summary>Icon position of a Material navigation destination (source <c>NavigationItemIconPosition</c>).</summary>
public enum MaterialNavigationItemIconPosition
{
    /// <summary>Follow the owning bar or rail.</summary>
    Auto,

    /// <summary>Icon above the label (source <c>Top</c>).</summary>
    Top,

    /// <summary>Icon before the label in horizontal reading order (source <c>Start</c>).</summary>
    Start
}

/// <summary>The source layout family used by a navigation item.</summary>
public enum MaterialNavigationItemLayoutKind
{
    /// <summary>Classic <c>NavigationBarItem</c>: 80 DIP slot, 56x32 indicator, icon above label.</summary>
    Bar,

    /// <summary><c>ShortNavigationBarItem</c>: 64 DIP bar, top or start icon, 56x32 top indicator.</summary>
    ShortBar,

    /// <summary>Classic <c>NavigationRailItem</c>: 80 DIP wide, 56 DIP high, icon above label.</summary>
    Rail,

    /// <summary><c>WideNavigationRailItem</c>: animates between the collapsed top and expanded start layout.</summary>
    WideRail
}

/// <summary>
/// Source-faithful navigation item anatomy shared by the navigation bar, short bar and rails.
/// </summary>
/// <remarks>
/// <para>
/// Adapted from the pinned AndroidX sources
/// <c>NavigationBar.kt</c> (<c>NavigationBarItemLayout</c>/<c>placeLabelAndIcon</c>),
/// <c>NavigationItem.kt</c> (<c>TopIconOrIconOnlyMeasurePolicy</c>,
/// <c>StartIconMeasurePolicy</c>, <c>AnimatedMeasurePolicy</c> and
/// <c>placeAnimatedLabelAndIcon</c>) and <c>WideNavigationRail.kt</c> at commit
/// <c>77b2199d6f056c2fe829ce25523c34a905e769c1</c>. Kotlin measure policies are adapted to an
/// Avalonia panel; the numeric tokens stay in the item controls and templates.
/// </para>
/// <para>
/// Child order is fixed by the item templates: indicator, state layer/ripple, icon, label slot and
/// focus ring. The panel measures the natural icon and label, then places the animated indicator
/// and the optional label movement. Text content and rich content share the label slot so custom
/// labels keep their own presenter while the source geometry stays intact.
/// </para>
/// <para>
/// The panel never animates by itself: item controls drive <see cref="IndicatorProgress"/>,
/// <see cref="IndicatorAlpha"/> and <see cref="IconPositionProgress"/> from frame-based
/// <see cref="Theming.MaterialMotion"/> springs.
/// </para>
/// </remarks>
public sealed class MaterialNavigationItemLayout : Panel
{
    public static readonly StyledProperty<MaterialNavigationItemLayoutKind> KindProperty =
        AvaloniaProperty.Register<MaterialNavigationItemLayout, MaterialNavigationItemLayoutKind>(nameof(Kind));

    public static readonly StyledProperty<MaterialNavigationItemIconPosition> IconPositionProperty =
        AvaloniaProperty.Register<MaterialNavigationItemLayout, MaterialNavigationItemIconPosition>(
            nameof(IconPosition), MaterialNavigationItemIconPosition.Top);

    /// <summary>Source <c>alwaysShowLabel</c> for classic bar/rail items.</summary>
    public static readonly StyledProperty<bool> AlwaysShowLabelProperty =
        AvaloniaProperty.Register<MaterialNavigationItemLayout, bool>(nameof(AlwaysShowLabel), true);

    public static readonly StyledProperty<double> IconPositionProgressProperty =
        AvaloniaProperty.Register<MaterialNavigationItemLayout, double>(
            nameof(IconPositionProgress), 0d,
            validate: static value => double.IsFinite(value));

    public static readonly StyledProperty<double> IndicatorProgressProperty =
        AvaloniaProperty.Register<MaterialNavigationItemLayout, double>(
            nameof(IndicatorProgress), 0d,
            validate: static value => double.IsFinite(value));

    public static readonly StyledProperty<double> IndicatorAlphaProperty =
        AvaloniaProperty.Register<MaterialNavigationItemLayout, double>(
            nameof(IndicatorAlpha), 0d,
            validate: static value => double.IsFinite(value));

    public static readonly StyledProperty<double> LabelOpacityProperty =
        AvaloniaProperty.Register<MaterialNavigationItemLayout, double>(
            nameof(LabelOpacity), 1d,
            validate: static value => double.IsFinite(value));

    public static readonly StyledProperty<bool> HasLabelProperty =
        AvaloniaProperty.Register<MaterialNavigationItemLayout, bool>(nameof(HasLabel), true);

    public static readonly StyledProperty<double> IconSizeProperty =
        AvaloniaProperty.Register<MaterialNavigationItemLayout, double>(nameof(IconSize), 24d,
            validate: static value => double.IsFinite(value) && value > 0);

    public static readonly StyledProperty<double> MinimumItemHeightProperty =
        AvaloniaProperty.Register<MaterialNavigationItemLayout, double>(nameof(MinimumItemHeight), 0d,
            validate: static value => double.IsFinite(value) && value >= 0);

    private bool _measureAtEndPosition;
    private double _arrangeWidth;

    static MaterialNavigationItemLayout()
    {
        AffectsMeasure<MaterialNavigationItemLayout>(
            KindProperty, IconPositionProperty, IconPositionProgressProperty, AlwaysShowLabelProperty,
            HasLabelProperty, IconSizeProperty, MinimumItemHeightProperty);
        AffectsArrange<MaterialNavigationItemLayout>(
            KindProperty, IconPositionProperty, IconPositionProgressProperty, AlwaysShowLabelProperty,
            IndicatorProgressProperty, IndicatorAlphaProperty, LabelOpacityProperty,
            HasLabelProperty, IconSizeProperty, MinimumItemHeightProperty, FlowDirectionProperty);
    }

    public MaterialNavigationItemLayoutKind Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    /// <summary>The target icon position. <see cref="MaterialNavigationItemIconPosition.Auto"/> is resolved by the item.</summary>
    public MaterialNavigationItemIconPosition IconPosition
    {
        get => GetValue(IconPositionProperty);
        set => SetValue(IconPositionProperty, value);
    }

    /// <summary>Source <c>alwaysShowLabel</c>; when false classic icon/label move to the selected positions.</summary>
    public bool AlwaysShowLabel
    {
        get => GetValue(AlwaysShowLabelProperty);
        set => SetValue(AlwaysShowLabelProperty, value);
    }

    /// <summary>0 places the icon above the label, 1 places it at the start (wide rail expansion).</summary>
    public double IconPositionProgress
    {
        get => GetValue(IconPositionProgressProperty);
        set => SetValue(IconPositionProgressProperty, value);
    }

    /// <summary>0 collapses the active indicator to zero width, 1 shows its full token size.</summary>
    public double IndicatorProgress
    {
        get => GetValue(IndicatorProgressProperty);
        set => SetValue(IndicatorProgressProperty, value);
    }

    /// <summary>Opacity of the active indicator (the source paints alpha and size from separate progress values).</summary>
    public double IndicatorAlpha
    {
        get => GetValue(IndicatorAlphaProperty);
        set => SetValue(IndicatorAlphaProperty, value);
    }

    /// <summary>Opacity of the label slot, driven by selection or the wide-rail position crossfade.</summary>
    public double LabelOpacity
    {
        get => GetValue(LabelOpacityProperty);
        set => SetValue(LabelOpacityProperty, value);
    }

    /// <summary>Whether a label slot is present. Hiding labels does not reserve the text row.</summary>
    public bool HasLabel
    {
        get => GetValue(HasLabelProperty);
        set => SetValue(HasLabelProperty, value);
    }

    public double IconSize
    {
        get => GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    /// <summary>Animated item minimum height (wide rail uses 64 collapsed and 48 expanded).</summary>
    public double MinimumItemHeight
    {
        get => GetValue(MinimumItemHeightProperty);
        set => SetValue(MinimumItemHeightProperty, value);
    }

    // The rail asks for the fully expanded width before its width spring retargets.
    internal Size MeasureExpanded(Size availableSize)
    {
        _measureAtEndPosition = true;
        try
        {
            Measure(availableSize);
            return DesiredSize;
        }
        finally
        {
            _measureAtEndPosition = false;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var iconSize = IconSize;
        var icon = Child(2);
        icon?.Measure(new Size(iconSize, iconSize));

        var hasLabel = HasLabel;
        var label = Child(3);
        var labelWidth = 0d;
        var labelHeight = 0d;
        var positionProgress = _measureAtEndPosition ? 1d : PositionProgress();
        if (hasLabel && label is not null)
        {
            var labelConstraint = double.PositiveInfinity;
            if (Kind is MaterialNavigationItemLayoutKind.WideRail && double.IsFinite(availableSize.Width))
                labelConstraint = IconPosition == MaterialNavigationItemIconPosition.Top && positionProgress < 0.5
                    ? 56d
                    : Math.Max(136d, availableSize.Width - iconSize - 60d);
            else if (Kind is MaterialNavigationItemLayoutKind.Rail && double.IsFinite(availableSize.Width))
                labelConstraint = Math.Max(0, availableSize.Width);
            else if (Kind is MaterialNavigationItemLayoutKind.Bar or MaterialNavigationItemLayoutKind.ShortBar &&
                     IconPosition != MaterialNavigationItemIconPosition.Start && double.IsFinite(availableSize.Width))
                labelConstraint = Math.Max(0, availableSize.Width);
            label.Measure(new Size(labelConstraint, double.PositiveInfinity));
            labelWidth = label.DesiredSize.Width;
            labelHeight = label.DesiredSize.Height;
        }

        var measured = Kind is MaterialNavigationItemLayoutKind.WideRail
            ? MeasureWide(availableSize, iconSize, labelWidth, labelHeight, positionProgress)
            : MeasureClassic(availableSize, iconSize, labelWidth, labelHeight);

        MeasureIndicator(measured.IndicatorWidth, measured.IndicatorHeight);
        var width = double.IsPositiveInfinity(availableSize.Width)
            ? measured.Width
            : Math.Min(measured.Width, availableSize.Width);
        return new Size(Math.Max(0, width), measured.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _arrangeWidth = finalSize.Width;
        var iconSize = IconSize;
        var hasLabel = HasLabel && Child(3) is { DesiredSize.Height: > 0 };
        var labelSize = hasLabel ? Child(3)!.DesiredSize : default;

        if (Kind is MaterialNavigationItemLayoutKind.WideRail)
            ArrangeWide(finalSize, iconSize, labelSize, PositionProgress());
        else if (hasLabel && IconPosition == MaterialNavigationItemIconPosition.Start)
            ArrangeStart(finalSize, iconSize, labelSize);
        else
            ArrangeTop(finalSize, iconSize, labelSize, hasLabel);

        return finalSize;
    }

    private (double Width, double Height, double IndicatorWidth, double IndicatorHeight) MeasureClassic(
        Size available, double iconSize, double labelWidth, double labelHeight)
    {
        var hasLabel = HasLabel && labelHeight > 0;
        var topLayout = IconPosition != MaterialNavigationItemIconPosition.Start;
        if (topLayout)
        {
            var verticalPadding = hasLabel ? 4d : Kind is MaterialNavigationItemLayoutKind.Rail ? 16d : 4d;
            var indicatorWidth = iconSize + 32d;
            var indicatorHeight = iconSize + (verticalPadding * 2);
            if (!hasLabel)
            {
                var widthNoLabel = Kind is MaterialNavigationItemLayoutKind.Bar
                    ? (double.IsPositiveInfinity(available.Width) ? indicatorWidth : Math.Max(indicatorWidth, available.Width))
                    : Math.Max(indicatorWidth, MinWidthForKind());
                return (widthNoLabel, Math.Max(indicatorHeight, MinimumItemHeight), indicatorWidth, indicatorHeight);
            }

            var contentHeight = iconSize + verticalPadding + 4d + labelHeight;
            var itemPadding = Kind is MaterialNavigationItemLayoutKind.ShortBar ? 6d : 0d;
            var height = Math.Max(contentHeight + (itemPadding * 2), MinimumItemHeight);
            var width = Kind is MaterialNavigationItemLayoutKind.Bar
                ? (double.IsPositiveInfinity(available.Width) ? Math.Max(labelWidth, indicatorWidth) : Math.Max(available.Width, labelWidth))
                : Math.Max(Math.Max(labelWidth, indicatorWidth), MinWidthForKind());
            if (Kind is MaterialNavigationItemLayoutKind.ShortBar)
                width = Math.Max(labelWidth, indicatorWidth);
            return (width, height, indicatorWidth, indicatorHeight);
        }

        // Start icon: 40 DIP indicator height, 16 DIP horizontal indicator padding, 4 DIP
        // icon-to-label space (NavigationBarHorizontalItemTokens / StartIconMeasurePolicy).
        if (!hasLabel)
        {
            var indicatorWidth = iconSize + 32d;
            return (Math.Max(indicatorWidth, MinWidthForKind()),
                Math.Max(iconSize + 32d, MinimumItemHeight), indicatorWidth, iconSize + 32d);
        }

        var totalWidth = iconSize + labelWidth + 4d + 32d;
        var indicatorHeight2 = Math.Max(iconSize, labelHeight) + 16d;
        var itemHeight = Math.Max(indicatorHeight2, MinimumItemHeight);
        return (Math.Max(totalWidth, MinWidthForKind()), itemHeight, totalWidth, indicatorHeight2);
    }

    private (double Width, double Height, double IndicatorWidth, double IndicatorHeight) MeasureWide(
        Size available, double iconSize, double labelWidth, double labelHeight, double positionProgress)
    {
        var hasLabel = HasLabel && labelHeight > 0;
        const double startPadding = 20d;
        // WideNavigationRailItem picks the content padding from the target icon position
        // (PaddingValues(horizontal = 20) for Top, PaddingValues(start = 20) for Start).
        var endPadding = !hasLabel || IconPosition != MaterialNavigationItemIconPosition.Start ? startPadding : 0d;
        const double horizontalIndicatorPadding = 32d; // FullWidthLeadingSpace + trailing space.
        var verticalIndicatorPadding = Lerp(4d, 16d, positionProgress);
        const double iconLabelSpace = 8d; // NavigationRailHorizontalItemTokens.IconLabelSpace.

        if (!hasLabel)
        {
            // AnimatedNavigationItem label == null: WNRItemNoLabelIndicatorPadding (16) on every
            // side gives a 56 circle inside the 20 DIP content padding.
            var size = iconSize + 32d;
            var width = size + startPadding + endPadding;
            return (width, Math.Max(size, MinimumItemHeight), size, size);
        }

        // AnimatedMeasurePolicy: indicator width/height interpolate between the top layout
        // (icon-sized indicator) and the start layout (icon + label + icon-label space).
        var indicatorWidth = Lerp(iconSize, iconSize + labelWidth + iconLabelSpace, positionProgress) +
            horizontalIndicatorPadding;
        var indicatorHeight = Lerp(iconSize, Math.Max(iconSize, labelHeight), positionProgress) +
            (verticalIndicatorPadding * 2);

        // widthTopIcon uses the top indicator width plus both content paddings; widthStartIcon
        // uses the interpoted indicator ripple width. The item width is their interpolation.
        var widthTop = Math.Max(labelWidth + startPadding + endPadding,
            iconSize + startPadding + endPadding + horizontalIndicatorPadding);
        var widthStart = indicatorWidth + startPadding + endPadding;
        if (double.IsFinite(available.Width))
        {
            widthTop = Math.Min(widthTop, available.Width);
            widthStart = Math.Min(widthStart, available.Width);
        }
        var widthResult = Lerp(widthTop, widthStart, positionProgress);
        var heightTop = Math.Max(indicatorHeight + 4d + labelHeight, MinimumItemHeight);
        var heightStart = Math.Max(indicatorHeight, MinimumItemHeight);
        return (widthResult, Lerp(heightTop, heightStart, positionProgress),
            indicatorWidth, indicatorHeight);
    }

    private void ArrangeTop(Size finalSize, double iconSize, Size labelSize, bool hasLabel)
    {
        var verticalPadding = hasLabel ? 4d : Kind is MaterialNavigationItemLayoutKind.Rail ? 16d : 4d;
        var totalIndicatorWidth = iconSize + 32d;
        var indicatorHeight = iconSize + (verticalPadding * 2);
        var progress = Clamp01(IndicatorProgress);
        var indicatorWidth = totalIndicatorWidth * progress;

        double iconX, iconY, labelY = 0, indicatorY;
        if (!hasLabel)
        {
            iconX = (finalSize.Width - iconSize) / 2;
            iconY = (finalSize.Height - iconSize) / 2;
            indicatorY = (finalSize.Height - indicatorHeight) / 2;
        }
        else
        {
            var shortBar = Kind is MaterialNavigationItemLayoutKind.ShortBar;
            var contentHeight = iconSize + verticalPadding + 4d + labelSize.Height;
            var screenPadding = shortBar ? 6d : Math.Max((finalSize.Height - contentHeight) / 2, verticalPadding);
            var selectedIconY = screenPadding;
            var unselectedIconY = AlwaysShowLabel
                ? selectedIconY
                : (finalSize.Height - iconSize) / 2;
            var offset = (unselectedIconY - selectedIconY) * (1 - progress);
            iconX = (finalSize.Width - iconSize) / 2;
            iconY = selectedIconY + offset;
            indicatorY = Math.Max(0, selectedIconY - verticalPadding + offset);
            labelY = selectedIconY + iconSize + verticalPadding + 4d + offset;
            Place(Child(3), (finalSize.Width - Math.Min(labelSize.Width, finalSize.Width)) / 2, labelY,
                Math.Min(labelSize.Width, finalSize.Width), labelSize.Height);
            Child(3)?.SetValue(OpacityProperty, Clamp01(LabelOpacity));
        }

        PlaceIndicator(Child(0), (finalSize.Width - indicatorWidth) / 2, indicatorY, indicatorWidth, indicatorHeight);
        Place(Child(1), (finalSize.Width - totalIndicatorWidth) / 2, (finalSize.Height - indicatorHeight) / 2,
            totalIndicatorWidth, indicatorHeight);
        Place(Child(2), iconX, iconY, iconSize, iconSize);
        Place(Child(4), (finalSize.Width - totalIndicatorWidth) / 2, (finalSize.Height - indicatorHeight) / 2,
            totalIndicatorWidth, indicatorHeight);
        Child(0)?.SetValue(OpacityProperty, Clamp01(IndicatorAlpha));
        if (!hasLabel)
            Place(Child(3), 0, 0, 0, 0);
    }

    private void ArrangeStart(Size finalSize, double iconSize, Size labelSize)
    {
        var indicatorHeight = Math.Max(iconSize, labelSize.Height) + 16d;
        var indicatorWidthTotal = iconSize + labelSize.Width + 4d + 32d;
        var progress = Clamp01(IndicatorProgress);
        var indicatorWidth = indicatorWidthTotal * progress;
        var rowWidth = iconSize + 4d + labelSize.Width;
        var rowX = (finalSize.Width - rowWidth) / 2;
        var rowY = (finalSize.Height - Math.Max(iconSize, labelSize.Height)) / 2;

        PlaceIndicator(Child(0), (finalSize.Width - indicatorWidth) / 2,
            (finalSize.Height - indicatorHeight) / 2, indicatorWidth, indicatorHeight);
        Place(Child(1), (finalSize.Width - indicatorWidthTotal) / 2,
            (finalSize.Height - indicatorHeight) / 2, indicatorWidthTotal, indicatorHeight);
        Place(Child(2), rowX, rowY, iconSize, iconSize);
        Place(Child(3), rowX + iconSize + 4d, rowY, Math.Min(labelSize.Width, Math.Max(0, finalSize.Width - rowX)), labelSize.Height);
        Place(Child(4), (finalSize.Width - indicatorWidthTotal) / 2,
            (finalSize.Height - indicatorHeight) / 2, indicatorWidthTotal, indicatorHeight);
        Child(0)?.SetValue(OpacityProperty, Clamp01(IndicatorAlpha));
        Child(3)?.SetValue(OpacityProperty, Clamp01(LabelOpacity));
    }

    private void ArrangeWide(Size finalSize, double iconSize, Size labelSize, double positionProgress)
    {
        var hasLabel = HasLabel && labelSize.Height > 0;
        const double startPadding = 20d;
        var endPadding = !hasLabel || IconPosition != MaterialNavigationItemIconPosition.Start ? startPadding : 0d;
        const double horizontalIndicatorPadding = 16d;
        var verticalIndicatorPadding = Lerp(4d, 16d, positionProgress);
        var indicatorProgress = Math.Max(0d, IndicatorProgress);

        if (!hasLabel)
        {
            var total = iconSize + 32d;
            var x = (finalSize.Width - total) / 2;
            var y = (finalSize.Height - total) / 2;
            Place(Child(0), x, y, total * indicatorProgress, total);
            Place(Child(1), x, y, total, total);
            Place(Child(2), (finalSize.Width - iconSize) / 2, (finalSize.Height - iconSize) / 2, iconSize, iconSize);
            Place(Child(3), 0, 0, 0, 0);
            Place(Child(4), x, y, total, total);
            Child(0)?.SetValue(OpacityProperty, Clamp01(IndicatorAlpha));
            Child(3)?.SetValue(OpacityProperty, 0d);
            return;
        }

        var topIconIndicatorWidth = iconSize;
        var startIconIndicatorWidth = iconSize + labelSize.Width + 8d;
        var topIconIndicatorHeight = iconSize;
        var startIconIndicatorHeight = Math.Max(iconSize, labelSize.Height);
        var indicatorWidthProgress = Lerp(topIconIndicatorWidth, startIconIndicatorWidth, positionProgress) +
            (horizontalIndicatorPadding * 2);
        var indicatorHeightProgress = Lerp(topIconIndicatorHeight, startIconIndicatorHeight, positionProgress) +
            (verticalIndicatorPadding * 2);
        var indicatorWidth = indicatorWidthProgress * indicatorProgress;
        // NavigationItem.placeAnimatedLabelAndIcon: the icon stays at the indicator's
        // leading inset while the label switches between top/start placements at mid-morph.
        var iconX = startPadding + horizontalIndicatorPadding;
        var iconYStart = (finalSize.Height - iconSize) / 2 - verticalIndicatorPadding;
        var iconY = Lerp(0, iconYStart, positionProgress) + verticalIndicatorPadding;
        var labelXTop = (iconSize + horizontalIndicatorPadding * 2 - labelSize.Width) / 2;
        var labelXOffset = IconPosition == MaterialNavigationItemIconPosition.Top && positionProgress > 0
            ? 0 : startPadding * (1 - positionProgress);
        var labelXStart = horizontalIndicatorPadding + iconSize + 8d - labelXOffset;
        var labelX = startPadding + (positionProgress < 0.5 ? labelXTop : labelXStart * positionProgress);
        var labelY = positionProgress < 0.5
            ? iconY + iconSize + verticalIndicatorPadding + 4d
            : (finalSize.Height - labelSize.Height) / 2;

        var indicatorY = Lerp(0, (finalSize.Height - indicatorHeightProgress) / 2, positionProgress);
        Place(Child(0), startPadding, indicatorY, indicatorWidth, indicatorHeightProgress);
        Place(Child(1), startPadding, indicatorY, indicatorWidthProgress, indicatorHeightProgress);
        Place(Child(2), iconX, iconY, iconSize, iconSize);
        // Keep the measured text box intact; the item root clips the moving label.
        // Re-arranging it to the shrinking viewport makes TextBlock reflow mid-frame.
        Place(Child(3), labelX, labelY, labelSize.Width, labelSize.Height);
        Place(Child(4), startPadding, indicatorY, indicatorWidthProgress, indicatorHeightProgress);
        Child(0)?.SetValue(OpacityProperty, Clamp01(IndicatorAlpha));
        Child(3)?.SetValue(OpacityProperty, Clamp01(LabelOpacity));
    }

    private void MeasureIndicator(double width, double height)
    {
        var fullWidth = Math.Max(width, 0);
        var fullHeight = Math.Max(height, 0);
        Child(0)?.Measure(new Size(fullWidth * Clamp01(IndicatorProgress), fullHeight));
        Child(1)?.Measure(new Size(fullWidth, fullHeight));
        Child(4)?.Measure(new Size(fullWidth, fullHeight));
    }

    private double PositionProgress() => Kind is MaterialNavigationItemLayoutKind.WideRail
        ? Math.Max(0d, IconPositionProgress)
        : IconPosition == MaterialNavigationItemIconPosition.Start ? 1d : 0d;

    private double MinWidthForKind() => Kind is MaterialNavigationItemLayoutKind.Rail ? 80d : 0d;

    private bool IsMirrored => FlowDirection == Avalonia.Media.FlowDirection.RightToLeft;

    private Control? Child(int index) => Children.Count > index ? Children[index] : null;

    private void Place(Control? child, double x, double y, double width, double height)
    {
        if (child is null)
            return;

        width = Math.Max(0, width);
        height = Math.Max(0, height);
        var physicalX = IsMirrored ? Math.Max(0, _arrangeWidth - x - width) : x;
        child.Arrange(new Rect(physicalX, y, width, height));
    }

    private static void PlaceIndicator(Control? child, double x, double y, double width, double height)
    {
        if (child is null)
            return;
        child.Arrange(new Rect(x, y, Math.Max(0, width), Math.Max(0, height)));
    }

    private static double Lerp(double from, double to, double progress) =>
        from + ((to - from) * progress);

    private static double Clamp01(double value) => Math.Clamp(value, 0d, 1d);
}