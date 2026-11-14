using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace ExpressiveAvalonia.Controls;

/// <summary>Positions for a FAB attached to a <see cref="MaterialScaffold"/>.</summary>
public enum MaterialFabPosition
{
    Start,
    Center,
    End,
    /// <summary>End position that overlays the bottom bar instead of stacking above it.</summary>
    EndOverlay
}

/// <summary>
/// Native Material scaffold: top bar, edge-to-edge content, snackbar, bottom bar and FAB slots with
/// the pinned source's stacking, insets and content padding.
/// </summary>
/// <remarks>
/// <para>
/// Source mapping: <c>Scaffold</c> / <c>ScaffoldLayout</c> / <c>FabPosition</c>. Slots are arranged
/// with the source z-order (content, top bar, snackbar, bottom bar, FAB); the FAB is 16 units above
/// the bottom bar (or the bottom inset) and snackbars stack above the FAB or bottom bar. Content is
/// measured and arranged over the full bounds, as upstream does; bind
/// <see cref="ContentPadding"/> on your content to respect the bars. Insets are not subtracted from
/// the content and are never applied twice by this control. An unset top or bottom bar falls back to
/// that inset; a bar that is set but measures zero height still suppresses it, matching the
/// source's placeable rule.
/// </para>
/// <para>
/// Extensions: <see cref="ContentInsets"/> and <see cref="ConsumedInsets"/> make the Android window
/// insets explicit for desktop hosts; <see cref="ContainerBrush"/> exposes the scaffold background.
/// </para>
/// </remarks>
public class MaterialScaffold : ContentControl
{
    public static readonly StyledProperty<object?> TopBarProperty =
        AvaloniaProperty.Register<MaterialScaffold, object?>(nameof(TopBar));

    public static readonly StyledProperty<object?> BottomBarProperty =
        AvaloniaProperty.Register<MaterialScaffold, object?>(nameof(BottomBar));

    public static readonly StyledProperty<object?> SnackbarHostProperty =
        AvaloniaProperty.Register<MaterialScaffold, object?>(nameof(SnackbarHost));

    public static readonly StyledProperty<object?> FloatingActionButtonProperty =
        AvaloniaProperty.Register<MaterialScaffold, object?>(nameof(FloatingActionButton));

    public static readonly StyledProperty<MaterialFabPosition> FabPositionProperty =
        AvaloniaProperty.Register<MaterialScaffold, MaterialFabPosition>(
            nameof(FabPosition), MaterialFabPosition.End);

    public static readonly StyledProperty<IBrush?> ContainerBrushProperty =
        AvaloniaProperty.Register<MaterialScaffold, IBrush?>(nameof(ContainerBrush));

    /// <summary>
    /// Content window insets in physical left/top/right/bottom units (desktop host policy).
    /// <see cref="ContentPadding"/> maps them to the current flow direction.
    /// </summary>
    public static readonly StyledProperty<Thickness> ContentInsetsProperty =
        AvaloniaProperty.Register<MaterialScaffold, Thickness>(nameof(ContentInsets));

    /// <summary>Insets an ancestor has already consumed; subtracted from <see cref="ContentInsets"/>.</summary>
    public static readonly StyledProperty<Thickness> ConsumedInsetsProperty =
        AvaloniaProperty.Register<MaterialScaffold, Thickness>(nameof(ConsumedInsets));

    /// <summary>Distance between the FAB and the bottom bar or bottom inset (source 16).</summary>
    public static readonly StyledProperty<double> FabSpacingProperty =
        AvaloniaProperty.Register<MaterialScaffold, double>(nameof(FabSpacing), 16d);

    public static readonly DirectProperty<MaterialScaffold, Thickness> ContentPaddingProperty =
        AvaloniaProperty.RegisterDirect<MaterialScaffold, Thickness>(
            nameof(ContentPadding), scaffold => scaffold.ContentPadding);

    private MaterialScaffoldLayout? _layout;
    private bool _layoutSubscribed;
    private Thickness _contentPadding;

    static MaterialScaffold()
    {
        TopBarProperty.Changed.AddClassHandler<MaterialScaffold>((scaffold, _) => scaffold.InvalidateMeasure());
        BottomBarProperty.Changed.AddClassHandler<MaterialScaffold>((scaffold, _) => scaffold.InvalidateMeasure());
        SnackbarHostProperty.Changed.AddClassHandler<MaterialScaffold>((scaffold, _) => scaffold.InvalidateMeasure());
        FloatingActionButtonProperty.Changed.AddClassHandler<MaterialScaffold>((scaffold, _) => scaffold.InvalidateMeasure());
        FabPositionProperty.Changed.AddClassHandler<MaterialScaffold>((scaffold, _) => scaffold.InvalidateMeasure());
    }

    public object? TopBar
    {
        get => GetValue(TopBarProperty);
        set => SetValue(TopBarProperty, value);
    }

    public object? BottomBar
    {
        get => GetValue(BottomBarProperty);
        set => SetValue(BottomBarProperty, value);
    }

    public object? SnackbarHost
    {
        get => GetValue(SnackbarHostProperty);
        set => SetValue(SnackbarHostProperty, value);
    }

    public object? FloatingActionButton
    {
        get => GetValue(FloatingActionButtonProperty);
        set => SetValue(FloatingActionButtonProperty, value);
    }

    public MaterialFabPosition FabPosition
    {
        get => GetValue(FabPositionProperty);
        set => SetValue(FabPositionProperty, value);
    }

    public IBrush? ContainerBrush
    {
        get => GetValue(ContainerBrushProperty);
        set => SetValue(ContainerBrushProperty, value);
    }

    public Thickness ContentInsets
    {
        get => GetValue(ContentInsetsProperty);
        set => SetValue(ContentInsetsProperty, value);
    }

    public Thickness ConsumedInsets
    {
        get => GetValue(ConsumedInsetsProperty);
        set => SetValue(ConsumedInsetsProperty, value);
    }

    public double FabSpacing
    {
        get => GetValue(FabSpacingProperty);
        set => SetValue(FabSpacingProperty, value);
    }

    /// <summary>Resolved padding the content should apply for the current bars and insets.</summary>
    public Thickness ContentPadding
    {
        get => _contentPadding;
        private set => SetAndRaise(ContentPaddingProperty, ref _contentPadding, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        // Null and a real zero-height bar measure identically, but have different inset
        // semantics. The parent layout must run even when the presenter size is unchanged.
        if (change.Property == TopBarProperty || change.Property == BottomBarProperty)
            _layout?.InvalidateMeasure();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        UnsubscribeLayout();
        _layout = e.NameScope.Find<MaterialScaffoldLayout>("PART_Layout");
        SubscribeLayout();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // Avalonia retains an applied template across detach/attach, so OnApplyTemplate is not
        // guaranteed to run again. Reconnect the subscription and republish the last measured
        // padding instead of leaving ContentPadding frozen with the old listener gone.
        SubscribeLayout();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        UnsubscribeLayout();
    }

    private void SubscribeLayout()
    {
        if (_layout is null || _layoutSubscribed)
            return;
        _layout.PropertyChanged += OnLayoutPropertyChanged;
        _layoutSubscribed = true;
        ContentPadding = _layout.ContentPadding;
    }

    private void UnsubscribeLayout()
    {
        if (_layout is null || !_layoutSubscribed)
            return;
        _layout.PropertyChanged -= OnLayoutPropertyChanged;
        _layoutSubscribed = false;
    }

    private void OnLayoutPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == MaterialScaffoldLayout.ContentPaddingProperty && sender is MaterialScaffoldLayout layout)
            ContentPadding = layout.ContentPadding;
    }
}

/// <summary>
/// Scaffold slot layout. Ports <c>ScaffoldLayout</c> measurement and placement, including FAB
/// alignment (Start/Center/End/EndOverlay and RTL), snackbar stacking and content padding.
/// </summary>
/// <remarks>
/// Children are resolved by name: PART_TopBar, PART_Content, PART_Snackbar, PART_BottomBar,
/// PART_Fab. They should be declared in that order so the visual z-order matches the source's
/// z-indices without relying on arrange order.
/// </remarks>
public sealed class MaterialScaffoldLayout : Panel
{
    public static readonly StyledProperty<MaterialFabPosition> FabPositionProperty =
        AvaloniaProperty.Register<MaterialScaffoldLayout, MaterialFabPosition>(
            nameof(FabPosition), MaterialFabPosition.End);

    /// <summary>Content window insets in physical left/top/right/bottom units.</summary>
    public static readonly StyledProperty<Thickness> ContentInsetsProperty =
        AvaloniaProperty.Register<MaterialScaffoldLayout, Thickness>(nameof(ContentInsets));

    /// <summary>Insets already consumed by an ancestor.</summary>
    public static readonly StyledProperty<Thickness> ConsumedInsetsProperty =
        AvaloniaProperty.Register<MaterialScaffoldLayout, Thickness>(nameof(ConsumedInsets));

    public static readonly StyledProperty<double> FabSpacingProperty =
        AvaloniaProperty.Register<MaterialScaffoldLayout, double>(nameof(FabSpacing), 16d);

    public static readonly DirectProperty<MaterialScaffoldLayout, Thickness> ContentPaddingProperty =
        AvaloniaProperty.RegisterDirect<MaterialScaffoldLayout, Thickness>(
            nameof(ContentPadding), layout => layout.ContentPadding);

    private double _topBarHeight;
    private double _bottomBarHeight;
    private double _snackbarWidth;
    private double _snackbarHeight;
    private double _snackbarOffsetFromBottom;
    private double _leftInset;
    private double _rightInset;
    private double _fabLeft;
    private double _fabHeight;
    private double _fabWidth;
    private double _fabOffsetFromBottom;
    private bool _hasFab;
    private bool _hasTopBar;
    private bool _hasBottomBar;
    private Thickness _contentPadding;

    static MaterialScaffoldLayout()
    {
        AffectsMeasure<MaterialScaffoldLayout>(FabPositionProperty, ContentInsetsProperty,
            ConsumedInsetsProperty, FabSpacingProperty);
    }

    public MaterialFabPosition FabPosition
    {
        get => GetValue(FabPositionProperty);
        set => SetValue(FabPositionProperty, value);
    }

    public Thickness ContentInsets
    {
        get => GetValue(ContentInsetsProperty);
        set => SetValue(ContentInsetsProperty, value);
    }

    public Thickness ConsumedInsets
    {
        get => GetValue(ConsumedInsetsProperty);
        set => SetValue(ConsumedInsetsProperty, value);
    }

    public double FabSpacing
    {
        get => GetValue(FabSpacingProperty);
        set => SetValue(FabSpacingProperty, value);
    }

    public Thickness ContentPadding
    {
        get => _contentPadding;
        private set => SetAndRaise(ContentPaddingProperty, ref _contentPadding, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 0;
        var height = double.IsFinite(availableSize.Height) ? availableSize.Height : 0;
        var loose = new Size(availableSize.Width, availableSize.Height);

        var topBar = Find("PART_TopBar");
        var content = Find("PART_Content");
        var snackbar = Find("PART_Snackbar");
        var bottomBar = Find("PART_BottomBar");
        var fab = Find("PART_Fab");

        // Source subcompose: a slot exists only when it emitted a placeable. A template that always
        // declares PART_TopBar/PART_BottomBar with no content is the source's default empty bar
        // (`topBar = {}` / `bottomBar = {}`) and must fall back to the window inset. A bar that is
        // present but measures zero is not empty and still suppresses the inset
        // (ScaffoldTest.scaffold_providesInsets_respectCollapsedTopAppBar).
        var hasTopBar = _hasTopBar = HasSlotContent(topBar);
        var hasBottomBar = _hasBottomBar = HasSlotContent(bottomBar);

        var insets = EffectiveInsets(out var startInset, out var endInset);
        _leftInset = insets.Left;
        _rightInset = insets.Right;

        topBar?.Measure(loose);
        _topBarHeight = topBar?.DesiredSize.Height ?? 0;

        var snackbarConstraint = new Size(
            Math.Max(0, availableSize.Width - startInset - endInset),
            Math.Max(0, availableSize.Height - insets.Bottom));
        snackbar?.Measure(snackbarConstraint);
        _snackbarHeight = snackbar?.DesiredSize.Height ?? 0;
        _snackbarWidth = snackbar?.DesiredSize.Width ?? 0;

        fab?.Measure(new Size(
            Math.Max(0, availableSize.Width - startInset - endInset),
            Math.Max(0, availableSize.Height - insets.Bottom)));
        var fabMeasured = fab is not null && fab.DesiredSize.Width > 0 && fab.DesiredSize.Height > 0;
        _hasFab = fabMeasured;
        _fabWidth = fabMeasured ? fab!.DesiredSize.Width : 0;
        _fabHeight = fabMeasured ? fab!.DesiredSize.Height : 0;

        bottomBar?.Measure(loose);
        _bottomBarHeight = bottomBar?.DesiredSize.Height ?? 0;

        var rtl = FlowDirection == FlowDirection.RightToLeft;
        if (_hasFab)
        {
            _fabLeft = FabPosition switch
            {
                MaterialFabPosition.Start => rtl
                    ? width - FabSpacing - _fabWidth - insets.Right
                    : FabSpacing + insets.Left,
                MaterialFabPosition.Center => (width - _fabWidth + insets.Left - insets.Right) / 2,
                _ => rtl
                    ? FabSpacing + insets.Left
                    : width - FabSpacing - _fabWidth - insets.Right
            };
            _fabOffsetFromBottom = !hasBottomBar || FabPosition == MaterialFabPosition.EndOverlay
                ? _fabHeight + FabSpacing + insets.Bottom
                : _bottomBarHeight + _fabHeight + FabSpacing;
        }
        else
        {
            _fabLeft = 0;
            _fabOffsetFromBottom = 0;
        }

        _snackbarOffsetFromBottom = _snackbarHeight != 0
            ? _snackbarHeight + (_hasFab
                ? _fabOffsetFromBottom
                : hasBottomBar ? _bottomBarHeight : insets.Bottom)
            : 0;

        var top = hasTopBar ? _topBarHeight : insets.Top;
        var bottom = hasBottomBar ? _bottomBarHeight : insets.Bottom;
        ContentPadding = rtl
            ? new Thickness(endInset, top, startInset, bottom)
            : new Thickness(startInset, top, endInset, bottom);

        content?.Measure(loose);
        var desiredWidth = double.IsFinite(availableSize.Width)
            ? availableSize.Width
            : Math.Max(0, content?.DesiredSize.Width ?? 0);
        var desiredHeight = double.IsFinite(availableSize.Height)
            ? availableSize.Height
            : Math.Max(0, Math.Max(content?.DesiredSize.Height ?? 0,
                Math.Max(_topBarHeight + _bottomBarHeight,
                    _snackbarHeight + _fabHeight)));
        return new Size(desiredWidth, desiredHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var topBar = Find("PART_TopBar");
        var content = Find("PART_Content");
        var snackbar = Find("PART_Snackbar");
        var bottomBar = Find("PART_BottomBar");
        var fab = Find("PART_Fab");
        var width = finalSize.Width;
        var height = finalSize.Height;

        if (topBar is not null)
        {
            if (_hasTopBar)
                topBar.Arrange(new Rect(0, 0, width, _topBarHeight));
            else
                topBar.Arrange(default);
        }
        content?.Arrange(new Rect(0, 0, width, height));
        if (snackbar is not null)
        {
            if (_snackbarHeight > 0)
            {
                // ScaffoldLayout: (layoutWidth - snackbarWidth + leftInset - rightInset) / 2,
                // where the source insets are physical left/right.
                var x = (width - _snackbarWidth + _leftInset - _rightInset) / 2;
                snackbar.Arrange(new Rect(x, height - _snackbarOffsetFromBottom,
                    _snackbarWidth, _snackbarHeight));
            }
            else
            {
                snackbar.Arrange(default);
            }
        }
        if (bottomBar is not null)
        {
            if (_hasBottomBar)
                bottomBar.Arrange(new Rect(0, height - _bottomBarHeight, width, _bottomBarHeight));
            else
                bottomBar.Arrange(default);
        }
        if (fab is not null)
        {
            if (_hasFab)
                fab.Arrange(new Rect(_fabLeft, height - _fabOffsetFromBottom, _fabWidth, _fabHeight));
            else
                fab.Arrange(default);
        }
        return finalSize;
    }

    private Thickness EffectiveInsets(out double startInset, out double endInset)
    {
        var insets = ContentInsets;
        var consumed = ConsumedInsets;
        var effective = new Thickness(
            Math.Max(0, insets.Left - consumed.Left),
            Math.Max(0, insets.Top - consumed.Top),
            Math.Max(0, insets.Right - consumed.Right),
            Math.Max(0, insets.Bottom - consumed.Bottom));
        var rtl = FlowDirection == FlowDirection.RightToLeft;
        startInset = rtl ? effective.Right : effective.Left;
        endInset = rtl ? effective.Left : effective.Right;
        return effective;
    }

    /// <summary>
    /// Matches the source's empty-result rule: a slot that produced no placeable is absent. The
    /// built-in template always declares the bar presenters, so an unset bar appears as a presenter
    /// without content; that is the source's default <c>{}</c> lambda, not a zero-height bar.
    /// </summary>
    private static bool HasSlotContent(Control? slot)
    {
        if (slot is null)
            return false;
        if (slot is ContentPresenter presenter)
            return presenter.Content is not null;
        if (slot is ContentControl contentControl)
            return contentControl.Content is not null;
        return true;
    }

    private Control? Find(string name)
    {
        foreach (var child in Children)
        {
            if (string.Equals(child.Name, name, StringComparison.Ordinal))
                return child;
        }
        return null;
    }
}