using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Theming;
using ExpressiveAvalonia.Tokens;

namespace ExpressiveAvalonia.Controls;

/// <summary>The top app bar arrangements from the pinned Material 3 source.</summary>
public enum MaterialAppBarConfiguration
{
    /// <summary>Single row, 64 units, start-aligned title and optional subtitle.</summary>
    Small,
    /// <summary>Single row, 64 units, title centred relative to the bar and clamped by the slots.</summary>
    SmallCentered,
    /// <summary>Two rows: 64-unit collapsed row and a 112-unit expanded bar with HeadlineSmall title.</summary>
    Medium,
    /// <summary>Two rows: 64-unit collapsed row and a 152-unit expanded bar with HeadlineMedium title.</summary>
    Large,
    /// <summary>Expressive medium: 112 (136 with subtitle) expanded, HeadlineMedium title.</summary>
    MediumFlexible,
    /// <summary>Expressive large: 120 (152 with subtitle) expanded, DisplaySmall title.</summary>
    LargeFlexible,
    /// <summary>Extension: 64-unit bar whose middle slot is an image thumbnail.</summary>
    SmallImage,
    /// <summary>Extension: 64-unit bar whose middle slot is a search field.</summary>
    Search
}

/// <summary>Extension: whether the bar draws a level 2 shadow once content scrolls under it.</summary>
public enum MaterialAppBarElevation
{
    /// <summary>No shadow, matching AppBar.kt's color-only container treatment.</summary>
    Flat,
    /// <summary>AppBarTokens.OnScrollContainerElevation (level 2) once the bar overlaps content.</summary>
    OnScroll
}

/// <summary>How a bar reacts to nested scroll events.</summary>
public enum MaterialBarScrollMode
{
    /// <summary>No scroll reaction.</summary>
    None,
    /// <summary>Height stays fixed; only the content overlap fraction changes.</summary>
    Pinned,
    /// <summary>Collapses on the pre-scroll pass and expands on post-scroll; height follows the gesture.</summary>
    EnterAlways,
    /// <summary>Collapses while scrolling forward and expands only when content returns to the start.</summary>
    ExitUntilCollapsed,
    /// <summary>Collapses and expands on post-scroll; used by the bottom app bar and floating toolbar.</summary>
    ExitAlways
}

/// <summary>Direction in which a floating toolbar leaves its parent.</summary>
public enum MaterialFloatingToolbarExitDirection
{
    Bottom,
    Top,
    Start,
    End
}

/// <summary>
/// Hoisted state of a top or bottom app bar, ported from <c>TopAppBarState</c> and
/// <c>BottomAppBarState</c>.
/// </summary>
public sealed class MaterialAppBarState : AvaloniaObject
{
    public static readonly StyledProperty<double> HeightOffsetLimitProperty =
        AvaloniaProperty.Register<MaterialAppBarState, double>(
            nameof(HeightOffsetLimit), -double.MaxValue);

    public static readonly StyledProperty<double> ContentOffsetProperty =
        AvaloniaProperty.Register<MaterialAppBarState, double>(nameof(ContentOffset));

    public static readonly DirectProperty<MaterialAppBarState, double> HeightOffsetProperty =
        AvaloniaProperty.RegisterDirect<MaterialAppBarState, double>(
            nameof(HeightOffset), state => state.HeightOffset,
            (state, value) => state.HeightOffset = value);

    public static readonly DirectProperty<MaterialAppBarState, double> CollapsedFractionProperty =
        AvaloniaProperty.RegisterDirect<MaterialAppBarState, double>(
            nameof(CollapsedFraction), state => state.CollapsedFraction);

    public static readonly DirectProperty<MaterialAppBarState, double> OverlappedFractionProperty =
        AvaloniaProperty.RegisterDirect<MaterialAppBarState, double>(
            nameof(OverlappedFraction), state => state.OverlappedFraction);

    private double _heightOffset;
    private double _collapsedFraction;
    private double _overlappedFraction;
    private bool _isScrollingContentAtStart = true;

    public MaterialAppBarState()
    {
        PropertyChanged += (_, _) => UpdateFractions();
    }

    /// <summary>The bar's collapse limit in DIPs, normally <c>-expandedHeight</c>.</summary>
    public double HeightOffsetLimit
    {
        get => GetValue(HeightOffsetLimitProperty);
        set => SetValue(HeightOffsetLimitProperty, value);
    }

    /// <summary>Current offset applied to the fixed height; coerced to [limit, 0].</summary>
    public double HeightOffset
    {
        get => _heightOffset;
        set
        {
            var coerced = Math.Clamp(value, HeightOffsetLimit, 0);
            if (SetAndRaise(HeightOffsetProperty, ref _heightOffset, coerced))
                UpdateFractions();
        }
    }

    /// <summary>Sum of nested scroll consumed under the bar.</summary>
    public double ContentOffset
    {
        get => GetValue(ContentOffsetProperty);
        set => SetValue(ContentOffsetProperty, value);
    }

    /// <summary>0 fully expanded, 1 fully collapsed.</summary>
    public double CollapsedFraction
    {
        get => _collapsedFraction;
        private set => SetAndRaise(CollapsedFractionProperty, ref _collapsedFraction, value);
    }

    /// <summary>Fraction of the visible bar overlapped by scrolled content.</summary>
    public double OverlappedFraction
    {
        get => _overlappedFraction;
        private set => SetAndRaise(OverlappedFractionProperty, ref _overlappedFraction, value);
    }

    /// <summary>
    /// True when the scrollable content is at its origin. Mirrors
    /// <c>TopAppBarState.isScrollingContentAtStart</c>; set by the ScrollViewer adapter.
    /// </summary>
    public bool IsScrollingContentAtStart
    {
        get => _isScrollingContentAtStart;
        set
        {
            if (_isScrollingContentAtStart == value)
                return;
            _isScrollingContentAtStart = value;
            UpdateFractions();
        }
    }

    /// <summary>Restores the expanded state.</summary>
    public void Reset()
    {
        HeightOffset = 0;
        ContentOffset = 0;
        IsScrollingContentAtStart = true;
    }

    private void UpdateFractions()
    {
        var limit = HeightOffsetLimit;
        CollapsedFraction = limit != 0 ? HeightOffset / limit : 0;
        if (!IsScrollingContentAtStart && ContentOffset == 0)
            OverlappedFraction = 1;
        else if (limit != 0)
        {
            var overlap = Math.Clamp((limit + Math.Abs(ContentOffset)) / limit, 0, 1);
            OverlappedFraction = 1 - overlap;
        }
        else
            OverlappedFraction = 0;
    }
}

/// <summary>
/// State machine behind pinned, enter-always, exit-until-collapsed and exit-always app bars.
/// Each method is a direct port of the corresponding <c>NestedScrollConnection</c> callback.
/// </summary>
public sealed class MaterialAppBarScrollBehavior
{
    private MaterialMotion.SpringAnimation? _snap;
    private MaterialMotion.DecayAnimation? _decay;
    private double _lastDecayValue;

    public MaterialAppBarScrollBehavior(MaterialBarScrollMode mode,
        MaterialAppBarState? state = null,
        MaterialFloatingToolbarExitDirection exitDirection = MaterialFloatingToolbarExitDirection.Bottom,
        MaterialSpring? snapSpring = null)
    {
        Mode = mode;
        State = state ?? new MaterialAppBarState();
        ExitDirection = exitDirection;
        // Top bars snap with TopAppBarDefaults.snapAnimationSpec (DefaultEffects); the bottom app
        // bar uses BottomAppBarDefaults.snapAnimationSpec (FastSpatial). A floating toolbar uses
        // FloatingToolbarDefaults.snapAnimationSpec (DefaultEffects) and supplies that explicitly.
        SnapSpring = snapSpring ?? (mode == MaterialBarScrollMode.ExitAlways
            ? MaterialSpring.SpatialFast
            : MaterialSpring.EffectsDefault);
    }

    /// <summary>Creates a pinned behavior (height fixed, overlap tracked).</summary>
    public static MaterialAppBarScrollBehavior Pinned(MaterialAppBarState? state = null) =>
        new(MaterialBarScrollMode.Pinned, state);

    /// <summary>Creates an enter-always behavior.</summary>
    public static MaterialAppBarScrollBehavior EnterAlways(MaterialAppBarState? state = null) =>
        new(MaterialBarScrollMode.EnterAlways, state);

    /// <summary>Creates an exit-until-collapsed behavior.</summary>
    public static MaterialAppBarScrollBehavior ExitUntilCollapsed(MaterialAppBarState? state = null) =>
        new(MaterialBarScrollMode.ExitUntilCollapsed, state);

    /// <summary>Creates the bottom app bar exit-always behavior (FastSpatial snap).</summary>
    public static MaterialAppBarScrollBehavior ExitAlways(MaterialAppBarState? state = null,
        MaterialFloatingToolbarExitDirection exitDirection = MaterialFloatingToolbarExitDirection.Bottom,
        MaterialSpring snapSpring = MaterialSpring.SpatialFast) =>
        new(MaterialBarScrollMode.ExitAlways, state, exitDirection, snapSpring);

    /// <summary>
    /// Creates the floating toolbar exit-always behavior with the pinned
    /// <c>FloatingToolbarDefaults.snapAnimationSpec</c> (DefaultEffects) instead of the bottom app
    /// bar's FastSpatial spring.
    /// </summary>
    public static MaterialAppBarScrollBehavior FloatingToolbar(MaterialAppBarState? state = null,
        MaterialFloatingToolbarExitDirection exitDirection = MaterialFloatingToolbarExitDirection.Bottom) =>
        new(MaterialBarScrollMode.ExitAlways, state, exitDirection, MaterialSpring.EffectsDefault);

    public MaterialBarScrollMode Mode { get; }

    public MaterialAppBarState State { get; }

    /// <summary>Exit edge for a floating toolbar; unused by top and bottom app bars.</summary>
    public MaterialFloatingToolbarExitDirection ExitDirection { get; }

    /// <summary>Pinned bars ignore drags and keep their height.</summary>
    public bool IsPinned => Mode == MaterialBarScrollMode.Pinned;

    /// <summary>Source-backed snap spring (DefaultEffects for top bars and floating toolbars, FastSpatial for the bottom app bar).</summary>
    public MaterialSpring SnapSpring { get; }

    /// <summary>
    /// Velocity left over after the last <see cref="OnPostFling"/> settle, mirroring the
    /// <c>Velocity(0f, remainingVelocity)</c> the source returns from <c>settleAppBar</c>.
    /// </summary>
    public double RemainingVelocity { get; private set; }

    /// <summary>Pre-scroll pass. Returns the consumed Y delta.</summary>
    public double OnPreScroll(double availableY)
    {
        CancelSettle();
        switch (Mode)
        {
            case MaterialBarScrollMode.EnterAlways:
            {
                var previous = State.HeightOffset;
                State.HeightOffset += availableY;
                return previous != State.HeightOffset ? availableY : 0;
            }
            case MaterialBarScrollMode.ExitUntilCollapsed:
            {
                if (availableY > 0)
                    return 0;
                var previous = State.HeightOffset;
                State.HeightOffset += availableY;
                return previous != State.HeightOffset ? availableY : 0;
            }
            default:
                return 0;
        }
    }

    /// <summary>Post-scroll pass. Returns the consumed Y delta.</summary>
    public double OnPostScroll(double consumedY, double availableY)
    {
        switch (Mode)
        {
            case MaterialBarScrollMode.None:
                return 0;
            case MaterialBarScrollMode.Pinned:
                State.ContentOffset += consumedY;
                return 0;
            case MaterialBarScrollMode.EnterAlways:
                State.ContentOffset += consumedY;
                return 0;
            case MaterialBarScrollMode.ExitAlways:
            {
                State.ContentOffset += consumedY;
                State.HeightOffset += consumedY;
                // ExitAlwaysScrollBehavior.onPostScroll returns Offset.Zero; the content has
                // already consumed the delta, the bar only mirrors it.
                return 0;
            }
            case MaterialBarScrollMode.ExitUntilCollapsed:
            {
                State.ContentOffset += consumedY;
                if (availableY < 0 || consumedY < 0)
                {
                    var previous = State.HeightOffset;
                    State.HeightOffset += consumedY;
                    return State.HeightOffset - previous;
                }
                if (availableY > 0)
                {
                    var previous = State.HeightOffset;
                    State.HeightOffset += availableY;
                    return State.HeightOffset - previous;
                }
                return 0;
            }
            default:
                return 0;
        }
    }

    /// <summary>
    /// Settles the bar after a fling: spline decay with the release velocity, then a spring snap to
    /// the nearer end. Mirrors <c>settleAppBar</c>; detached/reduced motion jumps to the target.
    /// </summary>
    public void OnPostFling(double availableY, Control owner)
    {
        RemainingVelocity = 0;
        if (Mode == MaterialBarScrollMode.None)
            return;
        if (Mode == MaterialBarScrollMode.Pinned)
        {
            // PinnedScrollBehavior resets the precision noise but never changes its height.
            if (availableY > 0)
                State.ContentOffset = 0;
            return;
        }
        if (availableY > 0 &&
            (Mode == MaterialBarScrollMode.ExitAlways
                ? State.HeightOffset == 0 || State.HeightOffset == State.HeightOffsetLimit
                : true))
        {
            State.ContentOffset = 0;
        }
        Settle(owner, availableY);
    }

    /// <summary>Stops decay/snap animations, for example when a new drag starts.</summary>
    public void CancelSettle()
    {
        _decay?.Dispose();
        _decay = null;
        _snap?.Dispose();
        _snap = null;
    }

    private void Settle(Control owner, double velocity)
    {
        CancelSettle();
        var fraction = State.CollapsedFraction;
        if (fraction < 0.01 || fraction >= 1)
            return;

        RemainingVelocity = velocity;
        if (Math.Abs(velocity) > 1)
        {
            _lastDecayValue = 0;
            _decay = MaterialMotion.Decay(owner, 0, velocity, (value, v) => OnDecayFrame(owner, value, v));
            _decay.Completed += (_, _) =>
            {
                if (_decay is null)
                    return;
                _decay = null;
                Snap(owner);
            };
            return;
        }
        Snap(owner);
    }

    private void OnDecayFrame(Control owner, double value, double velocity)
    {
        var delta = value - _lastDecayValue;
        var initialOffset = State.HeightOffset;
        State.HeightOffset = initialOffset + delta;
        var consumed = Math.Abs(initialOffset - State.HeightOffset);
        _lastDecayValue = value;
        RemainingVelocity = velocity;
        // Stop as soon as the bar clamps; the snap picks up from the clamped state.
        if (Math.Abs(delta - consumed) > 0.5)
        {
            _decay?.Dispose();
            _decay = null;
            Snap(owner);
        }
    }

    private void Snap(Control owner)
    {
        _snap?.Dispose();
        var fraction = State.CollapsedFraction;
        if (fraction < 0.01 || fraction >= 1)
            return;
        var target = fraction < 0.5 ? 0 : State.HeightOffsetLimit;
        // settleAppBar starts the snap from the current offset without carrying the fling
        // velocity (AnimationState(initialValue = heightOffset).animateTo).
        _snap = MaterialMotion.Animate(owner, State.HeightOffset, target,
            value => State.HeightOffset = value, SnapSpring, 0.5);
    }
}

/// <summary>
/// One row of a top app bar. Ports <c>TopAppBarMeasurePolicy</c>: a leading icon slot, a title that
/// is measured against the space left by the slots and clamped so it never covers them, and a
/// trailing actions slot.
/// </summary>
/// <remarks>
/// The dotted 4-unit horizontal insets live on the child containers in AppBars.axaml, exactly as
/// upstream's navigation/title/action boxes; this panel only places them. RTL mirrors the same
/// computations, matching <c>placeRelative</c>. A title cannot overlap the slots: when the
/// centred title would cross either slot it is shifted inward, and long text trims inside the
/// measured width.
/// </remarks>
public sealed class MaterialTopAppBarRow : Panel
{
    /// <summary>Horizontal alignment of the title within the bar.</summary>
    public static readonly StyledProperty<TextAlignment> TitleAlignmentProperty =
        AvaloniaProperty.Register<MaterialTopAppBarRow, TextAlignment>(
            nameof(TitleAlignment), TextAlignment.Left);

    /// <summary>Vertical arrangement of the title: center for single/collapsed rows, bottom for expanded rows.</summary>
    public static readonly StyledProperty<VerticalAlignment> TitleVerticalArrangementProperty =
        AvaloniaProperty.Register<MaterialTopAppBarRow, VerticalAlignment>(
            nameof(TitleVerticalArrangement), VerticalAlignment.Center);

    /// <summary>Bottom padding of an expanded title, 24 for medium and 28 for large variants.</summary>
    public static readonly StyledProperty<double> TitleBottomPaddingProperty =
        AvaloniaProperty.Register<MaterialTopAppBarRow, double>(nameof(TitleBottomPadding));

    /// <summary>Upstream <c>TopAppBarTitleInset</c> (16 - 4 = 12 DIPs).</summary>
    public static readonly StyledProperty<double> TitleInsetProperty =
        AvaloniaProperty.Register<MaterialTopAppBarRow, double>(nameof(TitleInset), 12d);

    private Control? _navigation;
    private Control? _title;
    private Control? _actions;

    public MaterialTopAppBarRow()
    {
        ClipToBounds = true;
    }

    static MaterialTopAppBarRow()
    {
        AffectsMeasure<MaterialTopAppBarRow>(TitleAlignmentProperty, TitleVerticalArrangementProperty,
            TitleBottomPaddingProperty, TitleInsetProperty);
    }

    public TextAlignment TitleAlignment
    {
        get => GetValue(TitleAlignmentProperty);
        set => SetValue(TitleAlignmentProperty, value);
    }

    public VerticalAlignment TitleVerticalArrangement
    {
        get => GetValue(TitleVerticalArrangementProperty);
        set => SetValue(TitleVerticalArrangementProperty, value);
    }

    public double TitleBottomPadding
    {
        get => GetValue(TitleBottomPaddingProperty);
        set => SetValue(TitleBottomPaddingProperty, value);
    }

    public double TitleInset
    {
        get => GetValue(TitleInsetProperty);
        set => SetValue(TitleInsetProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        ResolveParts();
        if (_navigation is null || _title is null || _actions is null)
            return default;

        _navigation.Measure(availableSize);
        _actions.Measure(availableSize);

        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : double.PositiveInfinity;
        var titleSpace = Math.Max(TitleInset, _navigation.DesiredSize.Width);
        var maxTitleWidth = double.IsInfinity(width)
            ? double.PositiveInfinity
            : Math.Max(0, width - titleSpace - _actions.DesiredSize.Width);
        _title.Measure(new Size(maxTitleWidth, availableSize.Height));

        // TopAppBarMeasurePolicy uses max(requestedHeight, titlePlaceable.height) for the layout
        // height; the navigation and action slots are placed but do not enlarge it.
        var height = _title.DesiredSize.Height;
        var desiredWidth = double.IsInfinity(width)
            ? _title.DesiredSize.Width + titleSpace + _actions.DesiredSize.Width
            : width;
        return new Size(desiredWidth, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        ResolveParts();
        if (_navigation is null || _title is null || _actions is null)
            return finalSize;

        var width = finalSize.Width;
        var height = finalSize.Height;
        var navW = _navigation.DesiredSize.Width;
        var titleW = Math.Min(_title.DesiredSize.Width, Math.Max(0, width - Math.Max(TitleInset, navW) - _actions.DesiredSize.Width));
        var titleH = _title.DesiredSize.Height;
        var actionsW = _actions.DesiredSize.Width;
        var rtl = FlowDirection == FlowDirection.RightToLeft;

        var navX = rtl ? width - navW : 0;
        var actionsX = rtl ? 0 : width - actionsW;
        _navigation.Arrange(new Rect(navX, (height - _navigation.DesiredSize.Height) / 2, navW,
            _navigation.DesiredSize.Height));
        _actions.Arrange(new Rect(actionsX, (height - _actions.DesiredSize.Height) / 2, actionsW,
            _actions.DesiredSize.Height));

        // LTR title placement, then mirror for RTL. The title is centred against the whole bar and
        // pushed inward only when it would cross a slot.
        var start = Math.Max(TitleInset, navW);
        var end = actionsW;
        var titleX = TitleAlignment switch
        {
            TextAlignment.Center => (width - titleW) / 2,
            TextAlignment.Right => width - titleW,
            _ => 0
        };
        if (titleX < start)
            titleX += start - titleX;
        else if (titleX + titleW > width - end)
            titleX += (width - end) - (titleX + titleW);

        var titleY = TitleVerticalArrangement switch
        {
            VerticalAlignment.Bottom => height - titleH - Math.Max(0, TitleBottomPadding),
            VerticalAlignment.Top => 0,
            _ => (height - titleH) / 2
        };
        titleY = Math.Max(0, titleY);
        if (rtl)
            titleX = width - titleX - titleW;
        _title.Arrange(new Rect(titleX, titleY, titleW, titleH));
        return finalSize;
    }

    private void ResolveParts()
    {
        _navigation ??= Find("PART_Navigation", 0);
        _title ??= Find("PART_Title", 1);
        _actions ??= Find("PART_Actions", 2);
    }

    private Control? Find(string name, int fallbackIndex)
    {
        foreach (var child in Children)
        {
            if (string.Equals(child.Name, name, StringComparison.Ordinal))
                return child;
        }
        return fallbackIndex < Children.Count ? Children[fallbackIndex] : null;
    }
}

/// <summary>
/// Two-row outer layout for medium/large top app bars. The first row keeps the collapsed height
/// and the second receives the remaining height, so as the bar collapses the expanded title row is
/// clipped from the top and the collapsed title takes over.
/// </summary>
public sealed class MaterialTopAppBarStack : Panel
{
    public static readonly StyledProperty<bool> IsTwoRowProperty =
        AvaloniaProperty.Register<MaterialTopAppBarStack, bool>(nameof(IsTwoRow));

    public static readonly StyledProperty<double> CollapsedHeightProperty =
        AvaloniaProperty.Register<MaterialTopAppBarStack, double>(nameof(CollapsedHeight), 64d);

    public MaterialTopAppBarStack()
    {
        ClipToBounds = true;
    }

    static MaterialTopAppBarStack()
    {
        AffectsMeasure<MaterialTopAppBarStack>(IsTwoRowProperty, CollapsedHeightProperty);
    }

    public bool IsTwoRow
    {
        get => GetValue(IsTwoRowProperty);
        set => SetValue(IsTwoRowProperty, value);
    }

    public double CollapsedHeight
    {
        get => GetValue(CollapsedHeightProperty);
        set => SetValue(CollapsedHeightProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count == 0)
            return default;
        var width = availableSize.Width;
        var first = Children[0];
        var collapsed = Math.Max(0, CollapsedHeight);
        // TopAppBarLayout's height is max(requestedHeight, titleHeight); a collapsed row with a
        // tiny requested height still grows to fit its own title.
        first.Measure(new Size(width, availableSize.Height));
        var firstHeight = Math.Max(collapsed, first.DesiredSize.Height);
        var height = firstHeight;
        if (IsTwoRow && Children.Count > 1)
        {
            var second = Children[1];
            second.Measure(new Size(width, double.PositiveInfinity));
            height += second.DesiredSize.Height;
        }
        return new Size(double.IsInfinity(width) ? Math.Max(first.DesiredSize.Width,
            Children.Count > 1 ? Children[1].DesiredSize.Width : 0) : width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count == 0)
            return finalSize;
        var collapsed = Math.Max(0, CollapsedHeight);
        if (!IsTwoRow)
        {
            Children[0].Arrange(new Rect(finalSize));
            if (Children.Count > 1)
                Children[1].Arrange(default);
            return finalSize;
        }

        var firstHeight = Math.Max(collapsed, Children[0].DesiredSize.Height);
        Children[0].Arrange(new Rect(0, 0, finalSize.Width, firstHeight));
        if (Children.Count > 1)
        {
            Children[1].Arrange(new Rect(0, firstHeight, finalSize.Width,
                Math.Max(0, finalSize.Height - firstHeight)));
        }
        return finalSize;
    }
}

/// <summary>
/// Avalonia adapter for <see cref="MaterialAppBarScrollBehavior"/> driven by a native
/// <see cref="ScrollViewer"/>.
/// </summary>
/// <remarks>
/// Compose nested scroll lets a bar consume deltas before and after the content scrolls.
/// <para>
/// <c>ScrollGesture</c> is bubble-only in Avalonia, so a class handler at the event source runs
/// before the native <see cref="ScrollContentPresenter"/> handling and performs the source
/// pre-scroll pass (<see cref="MaterialAppBarScrollBehavior.OnPreScroll"/>); when the bar consumes
/// the gesture the event is marked handled and native scrolling is preserved for the remainder.
/// A bubble handler on the ScrollViewer then observes the post-scroll event after the presenter
/// consumed its native offset, and expands <see cref="MaterialBarScrollMode.ExitUntilCollapsed"/>
/// only by the overscroll remainder at the content start.
/// </para>
/// <para>
/// Mouse wheel uses the same split through a tunnel handler on the ScrollViewer: a delta towards
/// the content end collapses/expands the bar before scrolling, while a delta towards the start is
/// expanded after the native pass by the amount beyond the scroll offset boundary.
/// </para>
/// <para>
/// <see cref="MaterialBarScrollMode.ExitUntilCollapsed"/> therefore expands only when a downward
/// gesture or wheel passes the start boundary. <see cref="ScrollChangedEventArgs"/> is still used
/// for bar-independent sources such as the scrollbar thumb, keyboard paging and programmatic
/// scrolling; that path consumes pre-scroll and rewinds the viewer because a sibling ScrollViewer
/// cannot be vetoed, and it never expands from the post-scroll slot.
/// </para>
/// <para>
/// A short idle timer converts the last content velocity into the post-fling settle call.
/// ReverseLayout/reverseScrolling overloads of the source have no Avalonia ScrollViewer equivalent;
/// hosts can set <see cref="MaterialAppBarState.IsScrollingContentAtStart"/> directly.
/// </para>
/// </remarks>
internal sealed class MaterialBarScrollAdapter : IDisposable
{
    private const double WheelStep = 40d;

    private static readonly ConditionalWeakTable<ScrollViewer, MaterialBarScrollAdapter> Adapters = new();

    static MaterialBarScrollAdapter()
    {
        InputElement.ScrollGestureEvent.AddClassHandler<InputElement>((source, e) =>
        {
            if (!ReferenceEquals(source, e.Source))
                return;
            for (Visual? visual = source; visual is not null; visual = visual.GetVisualParent())
            {
                if (visual is ScrollViewer viewer && Adapters.TryGetValue(viewer, out var adapter))
                {
                    adapter.OnGesturePre(e);
                    break;
                }
            }
        }, RoutingStrategies.Bubble, true);
    }

    private readonly Control _owner;
    private readonly MaterialAppBarScrollBehavior _behavior;
    private readonly DispatcherTimer _flingTimer;
    private ScrollViewer? _scroller;
    private ScrollContentPresenter? _presenter;
    private ScrollGestureEventArgs? _pendingGesture;
    private double _gestureStartOffset;
    private double _lastOffset;
    private double _velocity;
    private long _lastScrollTimestamp;
    private bool _suppress;

    public MaterialBarScrollAdapter(Control owner, MaterialAppBarScrollBehavior behavior)
    {
        _owner = owner;
        _behavior = behavior;
        _flingTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(120),
            DispatcherPriority.Background, OnFlingTimer);
        _flingTimer.Stop();
    }

    public void Attach(ScrollViewer? scroller)
    {
        if (_scroller is not null)
        {
            _scroller.ScrollChanged -= OnScrollChanged;
            _scroller.RemoveHandler(InputElement.ScrollGestureEvent, OnGesturePost);
            _scroller.RemoveHandler(InputElement.ScrollGestureEndedEvent, OnGestureEnded);
            _scroller.RemoveHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelPre);
            Adapters.Remove(_scroller);
        }

        _scroller = scroller;
        _presenter = null;
        _pendingGesture = null;
        _flingTimer.Stop();
        if (scroller is null)
            return;

        scroller.ScrollChanged += OnScrollChanged;
        scroller.AddHandler(InputElement.ScrollGestureEvent, OnGesturePost,
            RoutingStrategies.Bubble, true);
        scroller.AddHandler(InputElement.ScrollGestureEndedEvent, OnGestureEnded,
            RoutingStrategies.Bubble, true);
        scroller.AddHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelPre,
            RoutingStrategies.Tunnel, true);
        Adapters.Remove(scroller);
        Adapters.Add(scroller, this);

        _lastOffset = scroller.Offset.Y;
        _behavior.State.IsScrollingContentAtStart = _lastOffset <= 0.001;
        _velocity = 0;
        _lastScrollTimestamp = 0;
    }

    public void Dispose()
    {
        Attach(null);
        _flingTimer.Stop();
    }

    /// <summary>
    /// Pre-scroll half of the gesture adapter. Runs from the class handler at the event source
    /// before the native ScrollContentPresenter consumes the gesture.
    /// </summary>
    private void OnGesturePre(ScrollGestureEventArgs e)
    {
        _pendingGesture = null;
        if (_scroller is null || Math.Abs(e.Delta.Y) < 1e-6)
            return;

        // Compose nested-scroll Y is positive towards the start of the content; Avalonia's
        // gesture Delta.Y grows the offset (towards the end) for positive values.
        var available = -e.Delta.Y;
        if (_behavior.Mode is MaterialBarScrollMode.None or MaterialBarScrollMode.Pinned)
        {
            _gestureStartOffset = _scroller.Offset.Y;
            _pendingGesture = e;
            return;
        }

        var pre = _behavior.OnPreScroll(available);
        if (Math.Abs(pre) < 1e-6)
        {
            _gestureStartOffset = _scroller.Offset.Y;
            _pendingGesture = e;
            return;
        }

        // OnPreScroll consumes the full available delta whenever the height changed, matching the
        // source; the native presenter must not apply it a second time.
        e.Handled = true;
        _lastOffset = _scroller.Offset.Y;
        _velocity = pre * 60;
        StartFlingTimer();
    }

    /// <summary>
    /// Post-scroll half. The native presenter has applied its offset; the remainder beyond the
    /// content start expands ExitUntilCollapsed, matching <c>onPostScroll</c>.
    /// </summary>
    private void OnGesturePost(object? sender, ScrollGestureEventArgs e)
    {
        if (!ReferenceEquals(_pendingGesture, e))
            return;
        _pendingGesture = null;
        if (_scroller is null || _behavior.Mode != MaterialBarScrollMode.ExitUntilCollapsed)
            return;

        var available = -e.Delta.Y;
        if (available <= 0)
            return;

        var nativeConsumed = Math.Max(0, (_gestureStartOffset - _scroller.Offset.Y) * UnitsPerDip());
        var leftover = Math.Max(0, available - nativeConsumed);
        if (leftover <= 0.001)
            return;

        _behavior.State.HeightOffset += leftover;
        _velocity = leftover * 60;
        StartFlingTimer();
        e.Handled = true;
    }

    private void OnGestureEnded(object? sender, ScrollGestureEndedEventArgs e) => _pendingGesture = null;

    /// <summary>
    /// Wheel pre/post split. A delta towards the content end is consumed by the bar before the
    /// native viewer scrolls; a delta towards the start is left to the viewer and the overscroll
    /// remainder is applied after it (deferred to the dispatcher so the native offset is final).
    /// </summary>
    private void OnPointerWheelPre(object? sender, PointerWheelEventArgs e)
    {
        if (_scroller is null || e.Delta.Y == 0)
            return;
        if (e.Source is not Visual source ||
            (!ReferenceEquals(source, _scroller) && !_scroller.IsVisualAncestorOf(source)))
            return;
        if (_behavior.Mode is MaterialBarScrollMode.None or MaterialBarScrollMode.Pinned)
            return;

        var available = e.Delta.Y * WheelStep;
        if (_behavior.Mode == MaterialBarScrollMode.ExitUntilCollapsed && available > 0)
        {
            // Post-scroll expansion: only the part past the start boundary reaches the bar.
            var startOffset = _scroller.Offset.Y;
            var captured = available;
            Dispatcher.UIThread.Post(() => CompleteWheelExpansion(startOffset, captured),
                DispatcherPriority.Background);
            return;
        }

        var pre = _behavior.OnPreScroll(available);
        if (Math.Abs(pre) < 1e-6)
            return;

        e.Handled = true;
        _lastOffset = _scroller.Offset.Y;
        _velocity = pre * 60;
        StartFlingTimer();
    }

    private void CompleteWheelExpansion(double startOffset, double available)
    {
        if (_scroller is null || _behavior.Mode != MaterialBarScrollMode.ExitUntilCollapsed)
            return;
        var nativeConsumed = Math.Max(0, (startOffset - _scroller.Offset.Y) * UnitsPerDip());
        var leftover = Math.Max(0, available - nativeConsumed);
        if (leftover <= 0.001)
            return;
        _behavior.State.HeightOffset += leftover;
        _velocity = leftover * 60;
        StartFlingTimer();
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_scroller is null)
            return;
        var current = _scroller.Offset.Y;
        if (_suppress)
        {
            _lastOffset = current;
            return;
        }

        var delta = current - _lastOffset;
        if (Math.Abs(delta) < 1e-6)
        {
            _lastOffset = current;
            return;
        }

        // Compose nested-scroll Y is positive towards the start of the content; Avalonia's
        // ScrollViewer offset grows in the opposite direction. Offset units become DIPs before
        // reaching the behavior because the source state and springs are density-independent.
        var unitsPerDip = UnitsPerDip();
        var available = -delta * unitsPerDip;
        _behavior.CancelSettle();
        // During a native gesture the pre-scroll pass already ran at the source. ScrollChanged
        // then only reports the child-consumed delta; the post-scroll available slot is zero so
        // ExitUntilCollapsed never expands here (overscroll is handled by the gesture/wheel pass).
        var pre = _pendingGesture is null ? _behavior.OnPreScroll(available) : 0;
        var applied = delta + pre / unitsPerDip;
        var consumed = -applied * unitsPerDip;

        if (Math.Abs(applied - delta) > 1e-6)
        {
            _suppress = true;
            try
            {
                _scroller.Offset = new Vector(_scroller.Offset.X, _lastOffset + applied);
            }
            finally
            {
                _suppress = false;
            }
        }

        _lastOffset += applied;
        _behavior.OnPostScroll(consumed, 0);
        _behavior.State.IsScrollingContentAtStart = _lastOffset <= 0.001;

        // Track the content velocity for the settle. ScrollChanged carries no timestamp, so the
        // elapsed time between consecutive offset changes (Stopwatch) supplies dt.
        var now = System.Diagnostics.Stopwatch.GetTimestamp();
        var elapsed = _lastScrollTimestamp == 0
            ? 1d / 60
            : Math.Max(1d / 240, (now - _lastScrollTimestamp)
                / (double)System.Diagnostics.Stopwatch.Frequency);
        _lastScrollTimestamp = now;
        _velocity = consumed / elapsed;
        StartFlingTimer();
    }

    private void StartFlingTimer()
    {
        _flingTimer.Stop();
        _flingTimer.Start();
    }

    private void OnFlingTimer(object? sender, EventArgs e)
    {
        _flingTimer.Stop();
        var velocity = _velocity;
        _velocity = 0;
        _behavior.OnPostFling(velocity, _owner);
    }

    /// <summary>
    /// DIP per ScrollViewer offset unit. Gestures travel in DIPs while a ScrollContentPresenter's
    /// Offset is expressed in its own scroll units.
    /// </summary>
    private double UnitsPerDip()
    {
        if (_scroller is null)
            return 1;
        _presenter ??= _scroller.GetVisualDescendants()
            .OfType<ScrollContentPresenter>()
            .FirstOrDefault();
        if (_presenter is { } presenter && presenter.Viewport.Height > 0
            && presenter.Bounds.Height > 0)
            return presenter.Bounds.Height / presenter.Viewport.Height;
        return 1;
    }
}

/// <summary>
/// A Material top app bar with small, small-centred, medium, large and the two expressive
/// flexible arrangements, plus the library's image and search extensions.
/// </summary>
/// <remarks>
/// Source mapping: <c>TopAppBar</c> (small + subtitle), <c>CenterAlignedTopAppBar</c>,
/// <c>MediumTopAppBar</c>, <c>LargeTopAppBar</c>, <c>MediumFlexibleTopAppBar</c>,
/// <c>LargeFlexibleTopAppBar</c> and <c>TwoRowsTopAppBar</c>. Heights, typography and padding come
/// from AppBarSmall/Medium/Large(*Flexible)Tokens; container colors from AppBarTokens; title alpha
/// uses <c>TopTitleAlphaEasing</c> and scroll geometry uses the exact collapse/overlap fractions.
/// <para>
/// Extensions without a Compose counterpart: <see cref="MaterialAppBarConfiguration.SmallImage"/>
/// and <see cref="MaterialAppBarConfiguration.Search"/> (plus their thumbnail/avatar/search
/// properties and search events), <see cref="Elevation"/>'s optional level 2 shadow, and
/// <see cref="SafeAreaPadding"/> for desktop host insets. The pinned <c>AppBarWithSearch</c>
/// composition, including its search-specific scroll state and resting/scrolled colors, is
/// <see cref="MaterialSearchAppBar"/>; this bar's <c>Search</c> configuration remains a
/// self-contained field extension.
/// </para>
/// </remarks>
public partial class MaterialAppBar : MaterialBarBase
{
    public static readonly StyledProperty<MaterialAppBarConfiguration> ConfigurationProperty =
        AvaloniaProperty.Register<MaterialAppBar, MaterialAppBarConfiguration>(
            nameof(Configuration), MaterialAppBarConfiguration.Small);

    public static readonly StyledProperty<MaterialAppBarElevation> ElevationProperty =
        AvaloniaProperty.Register<MaterialAppBar, MaterialAppBarElevation>(
            nameof(Elevation), MaterialAppBarElevation.Flat);

    public static readonly StyledProperty<object?> TitleProperty =
        AvaloniaProperty.Register<MaterialAppBar, object?>(nameof(Title));

    public static readonly StyledProperty<object?> SubtitleProperty =
        AvaloniaProperty.Register<MaterialAppBar, object?>(nameof(Subtitle));

    /// <summary>Text alignment of the title; Left/Center map to upstream Start/Center.</summary>
    public static readonly StyledProperty<TextAlignment> TitleAlignmentProperty =
        AvaloniaProperty.Register<MaterialAppBar, TextAlignment>(nameof(TitleAlignment), TextAlignment.Left);

    /// <summary>Expanded height in DIPs; NaN selects the configuration token height.</summary>
    public static readonly StyledProperty<double> ExpandedHeightProperty =
        AvaloniaProperty.Register<MaterialAppBar, double>(nameof(ExpandedHeight), double.NaN);

    /// <summary>Collapsed height of a two-row bar in DIPs; NaN selects 64 (AppBarSmallTokens).</summary>
    public static readonly StyledProperty<double> CollapsedHeightProperty =
        AvaloniaProperty.Register<MaterialAppBar, double>(nameof(CollapsedHeight), double.NaN);

    /// <summary>Desktop safe-area padding; the equivalent of window insets for the top bar.</summary>
    public static readonly StyledProperty<Thickness> SafeAreaPaddingProperty =
        AvaloniaProperty.Register<MaterialAppBar, Thickness>(nameof(SafeAreaPadding));

    public static readonly StyledProperty<object?> LeadingContentProperty =
        AvaloniaProperty.Register<MaterialAppBar, object?>(nameof(LeadingContent));

    public static readonly StyledProperty<Geometry?> LeadingIconProperty =
        AvaloniaProperty.Register<MaterialAppBar, Geometry?>(nameof(LeadingIcon));

    public static readonly StyledProperty<ICommand?> LeadingCommandProperty =
        AvaloniaProperty.Register<MaterialAppBar, ICommand?>(nameof(LeadingCommand));

    public static readonly StyledProperty<string?> LeadingTextProperty =
        AvaloniaProperty.Register<MaterialAppBar, string?>(nameof(LeadingText));

    public static readonly StyledProperty<object?> TrailingContentProperty =
        AvaloniaProperty.Register<MaterialAppBar, object?>(nameof(TrailingContent));

    public static readonly StyledProperty<object?> ThumbnailProperty =
        AvaloniaProperty.Register<MaterialAppBar, object?>(nameof(Thumbnail));

    public static readonly StyledProperty<object?> ImageContentProperty =
        AvaloniaProperty.Register<MaterialAppBar, object?>(nameof(ImageContent));

    public static readonly StyledProperty<object?> AvatarContentProperty =
        AvaloniaProperty.Register<MaterialAppBar, object?>(nameof(AvatarContent));

    public static readonly StyledProperty<string?> AvatarTextProperty =
        AvaloniaProperty.Register<MaterialAppBar, string?>(nameof(AvatarText));

    public static readonly StyledProperty<object?> SearchContentProperty =
        AvaloniaProperty.Register<MaterialAppBar, object?>(nameof(SearchContent));

    public static readonly StyledProperty<string> SearchPlaceholderProperty =
        AvaloniaProperty.Register<MaterialAppBar, string>(nameof(SearchPlaceholder), "Search");

    public static readonly StyledProperty<string?> SearchTextProperty =
        AvaloniaProperty.Register<MaterialAppBar, string?>(
            nameof(SearchText), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Hoisted scroll behavior; null keeps the bar fixed with no scroll reaction.</summary>
    public static readonly StyledProperty<MaterialAppBarScrollBehavior?> ScrollBehaviorProperty =
        AvaloniaProperty.Register<MaterialAppBar, MaterialAppBarScrollBehavior?>(nameof(ScrollBehavior));

    /// <summary>Native scroll viewer that drives the behavior's nested-scroll callbacks.</summary>
    public static readonly StyledProperty<ScrollViewer?> ScrollSourceProperty =
        AvaloniaProperty.Register<MaterialAppBar, ScrollViewer?>(nameof(ScrollSource));

    public static readonly DirectProperty<MaterialAppBar, bool> IsScrolledProperty =
        AvaloniaProperty.RegisterDirect<MaterialAppBar, bool>(
            nameof(IsScrolled), bar => bar.IsScrolled, (bar, value) => bar.IsScrolled = value);

    public static readonly DirectProperty<MaterialAppBar, bool> IsSingleRowLayoutProperty =
        AvaloniaProperty.RegisterDirect<MaterialAppBar, bool>(
            nameof(IsSingleRowLayout), bar => bar.IsSingleRowLayout, (bar, value) => bar.IsSingleRowLayout = value);

    public static readonly DirectProperty<MaterialAppBar, bool> IsTwoRowLayoutProperty =
        AvaloniaProperty.RegisterDirect<MaterialAppBar, bool>(
            nameof(IsTwoRowLayout), bar => bar.IsTwoRowLayout, (bar, value) => bar.IsTwoRowLayout = value);

    public static readonly DirectProperty<MaterialAppBar, bool> IsFlexibleLayoutProperty =
        AvaloniaProperty.RegisterDirect<MaterialAppBar, bool>(
            nameof(IsFlexibleLayout), bar => bar.IsFlexibleLayout, (bar, value) => bar.IsFlexibleLayout = value);

    public static readonly DirectProperty<MaterialAppBar, bool> HasFlexibleTextTitleProperty =
        AvaloniaProperty.RegisterDirect<MaterialAppBar, bool>(nameof(HasFlexibleTextTitle), bar => bar.HasFlexibleTextTitle);

    public static readonly DirectProperty<MaterialAppBar, bool> HasSmallTitleProperty =
        AvaloniaProperty.RegisterDirect<MaterialAppBar, bool>(
            nameof(HasSmallTitle), bar => bar.HasSmallTitle, (bar, value) => bar.HasSmallTitle = value);

    public static readonly DirectProperty<MaterialAppBar, bool> IsSearchLayoutProperty =
        AvaloniaProperty.RegisterDirect<MaterialAppBar, bool>(
            nameof(IsSearchLayout), bar => bar.IsSearchLayout, (bar, value) => bar.IsSearchLayout = value);

    public static readonly DirectProperty<MaterialAppBar, bool> HasSubtitleProperty =
        AvaloniaProperty.RegisterDirect<MaterialAppBar, bool>(
            nameof(HasSubtitle), bar => bar.HasSubtitle, (bar, value) => bar.HasSubtitle = value);

    public static readonly DirectProperty<MaterialAppBar, bool> ShowCollapsedSubtitleProperty =
        AvaloniaProperty.RegisterDirect<MaterialAppBar, bool>(
            nameof(ShowCollapsedSubtitle), bar => bar.ShowCollapsedSubtitle,
            (bar, value) => bar.ShowCollapsedSubtitle = value);

    /// <summary>True when the single/collapsed title row shows a subtitle.</summary>
    public static readonly DirectProperty<MaterialAppBar, bool> ShowTopSubtitleProperty =
        AvaloniaProperty.RegisterDirect<MaterialAppBar, bool>(
            nameof(ShowTopSubtitle), bar => bar.ShowTopSubtitle,
            (bar, value) => bar.ShowTopSubtitle = value);

    /// <summary>Top padding below the expanded title: 24 for medium, 28 for large variants.</summary>
    public static readonly DirectProperty<MaterialAppBar, double> ExpandedTitleBottomPaddingProperty =
        AvaloniaProperty.RegisterDirect<MaterialAppBar, double>(
            nameof(ExpandedTitleBottomPadding), bar => bar.ExpandedTitleBottomPadding,
            (bar, value) => bar.ExpandedTitleBottomPadding = value);

    public static readonly DirectProperty<MaterialAppBar, bool> ShowExpandedSubtitleProperty =
        AvaloniaProperty.RegisterDirect<MaterialAppBar, bool>(
            nameof(ShowExpandedSubtitle), bar => bar.ShowExpandedSubtitle,
            (bar, value) => bar.ShowExpandedSubtitle = value);

    public static readonly DirectProperty<MaterialAppBar, bool> HasThumbnailProperty =
        AvaloniaProperty.RegisterDirect<MaterialAppBar, bool>(
            nameof(HasThumbnail), bar => bar.HasThumbnail, (bar, value) => bar.HasThumbnail = value);

    public static readonly DirectProperty<MaterialAppBar, bool> HasAvatarProperty =
        AvaloniaProperty.RegisterDirect<MaterialAppBar, bool>(
            nameof(HasAvatar), bar => bar.HasAvatar, (bar, value) => bar.HasAvatar = value);

    public static readonly DirectProperty<MaterialAppBar, bool> HasLeadingIconProperty =
        AvaloniaProperty.RegisterDirect<MaterialAppBar, bool>(
            nameof(HasLeadingIcon), bar => bar.HasLeadingIcon, (bar, value) => bar.HasLeadingIcon = value);

    public static readonly DirectProperty<MaterialAppBar, double> EffectiveExpandedHeightProperty =
        AvaloniaProperty.RegisterDirect<MaterialAppBar, double>(
            nameof(EffectiveExpandedHeight), bar => bar.EffectiveExpandedHeight);

    public static readonly DirectProperty<MaterialAppBar, double> EffectiveCollapsedHeightProperty =
        AvaloniaProperty.RegisterDirect<MaterialAppBar, double>(
            nameof(EffectiveCollapsedHeight), bar => bar.EffectiveCollapsedHeight);

    public static readonly DirectProperty<MaterialAppBar, double> CollapsedFractionProperty =
        AvaloniaProperty.RegisterDirect<MaterialAppBar, double>(
            nameof(CollapsedFraction), bar => bar.CollapsedFraction);

    public static readonly DirectProperty<MaterialAppBar, double> OverlappedFractionProperty =
        AvaloniaProperty.RegisterDirect<MaterialAppBar, double>(
            nameof(OverlappedFraction), bar => bar.OverlappedFraction);

    public static readonly DirectProperty<MaterialAppBar, double> TopTitleOpacityProperty =
        AvaloniaProperty.RegisterDirect<MaterialAppBar, double>(
            nameof(TopTitleOpacity), bar => bar.TopTitleOpacity);

    public static readonly DirectProperty<MaterialAppBar, double> BottomTitleOpacityProperty =
        AvaloniaProperty.RegisterDirect<MaterialAppBar, double>(
            nameof(BottomTitleOpacity), bar => bar.BottomTitleOpacity);

    public static readonly DirectProperty<MaterialAppBar, IBrush?> RenderedBackgroundProperty =
        AvaloniaProperty.RegisterDirect<MaterialAppBar, IBrush?>(nameof(RenderedBackground), bar => bar.RenderedBackground);

    public static readonly DirectProperty<MaterialAppBar, object?> CollapsedTitleContentProperty =
        AvaloniaProperty.RegisterDirect<MaterialAppBar, object?>(nameof(CollapsedTitleContent), bar => bar.CollapsedTitleContent);
    public static readonly DirectProperty<MaterialAppBar, object?> ExpandedTitleContentProperty =
        AvaloniaProperty.RegisterDirect<MaterialAppBar, object?>(nameof(ExpandedTitleContent), bar => bar.ExpandedTitleContent);

    private bool _isScrolled;
    private bool _isSingleRowLayout = true;
    private bool _isTwoRowLayout;
    private bool _isFlexibleLayout;
    private bool _hasFlexibleTextTitle;
    private object? _collapsedTitleContent;
    private object? _expandedTitleContent;
    private bool _hasSmallTitle = true;
    private bool _isSearchLayout;
    private bool _hasSubtitle;
    private bool _showCollapsedSubtitle;
    private bool _showExpandedSubtitle;
    private bool _showTopSubtitle;
    private double _expandedTitleBottomPadding = 24;
    private bool _hasThumbnail;
    private bool _hasAvatar;
    private bool _hasLeadingIcon;
    private double _effectiveExpandedHeight = 64;
    private double _effectiveCollapsedHeight = 64;
    private double _measuredExpandedPart = 64;
    private double _measuredCollapsedPart = 64;
    private double _collapsedFraction;
    private double _overlappedFraction;
    private double _topTitleOpacity = 1;
    private double _bottomTitleOpacity;
    private Panel? _searchHost;
    private MaterialTopAppBarStack? _stack;
    private Border? _searchField;
    private TextBox? _searchBox;
    private IDisposable? _searchBackgroundBinding;
    private MaterialBarScrollAdapter? _adapter;
    private MaterialAppBarState? _state;
    private MaterialColorAnimation? _containerAnimation;
    private SolidColorBrush? _containerBrush;
    private IBrush? _renderedBackground;
    private IDisposable? _scrollColorSubscription;
    private sealed class ScrollColorObserver(MaterialAppBar owner) : IObserver<object?>
    {
        public void OnNext(object? value) => owner.UpdateScrollAppearance();
        public void OnError(Exception error) { }
        public void OnCompleted() { }
    }
    private Control? _topTitlePresenter;
    private Control? _bottomTitlePresenter;
    private bool _dragging;
    private double _dragLastY;
    private double _dragVelocity;
    private ulong _dragLastTimestamp;

    static MaterialAppBar()
    {
        ConfigurationProperty.Changed.AddClassHandler<MaterialAppBar>((bar, _) => bar.UpdateGeometry());
        SubtitleProperty.Changed.AddClassHandler<MaterialAppBar>((bar, _) => bar.UpdateLayoutFlags());
        TitleProperty.Changed.AddClassHandler<MaterialAppBar>((bar, _) => bar.UpdateLayoutFlags());
        ThumbnailProperty.Changed.AddClassHandler<MaterialAppBar>((bar, _) => bar.UpdateLayoutFlags());
        AvatarContentProperty.Changed.AddClassHandler<MaterialAppBar>((bar, _) => bar.UpdateLayoutFlags());
        AvatarTextProperty.Changed.AddClassHandler<MaterialAppBar>((bar, _) => bar.UpdateLayoutFlags());
        LeadingIconProperty.Changed.AddClassHandler<MaterialAppBar>((bar, _) => bar.UpdateLayoutFlags());
        LeadingContentProperty.Changed.AddClassHandler<MaterialAppBar>((bar, _) => bar.UpdateLayoutFlags());
        ExpandedHeightProperty.Changed.AddClassHandler<MaterialAppBar>((bar, _) => bar.UpdateGeometry());
        CollapsedHeightProperty.Changed.AddClassHandler<MaterialAppBar>((bar, _) => bar.UpdateGeometry());
        SafeAreaPaddingProperty.Changed.AddClassHandler<MaterialAppBar>((bar, _) => bar.UpdateGeometry());
        ScrollBehaviorProperty.Changed.AddClassHandler<MaterialAppBar>((bar, args) =>
            bar.OnScrollBehaviorChanged(args.GetOldValue<MaterialAppBarScrollBehavior?>(),
                args.GetNewValue<MaterialAppBarScrollBehavior?>()));
        ScrollSourceProperty.Changed.AddClassHandler<MaterialAppBar>((bar, _) => bar.UpdateAdapter());
        ElevationProperty.Changed.AddClassHandler<MaterialAppBar>((bar, _) => bar.UpdateSearchBackground());
    }

    /// <summary>Raised when Enter is pressed in the extension's built-in search field.</summary>
    public event EventHandler? SearchSubmitted;

    /// <summary>Raised when Escape is pressed in an empty extension search field.</summary>
    public event EventHandler? SearchDismissed;

    public MaterialAppBar() => UpdateGeometry();

    public MaterialAppBarConfiguration Configuration
    {
        get => GetValue(ConfigurationProperty);
        set => SetValue(ConfigurationProperty, value);
    }

    public MaterialAppBarElevation Elevation
    {
        get => GetValue(ElevationProperty);
        set => SetValue(ElevationProperty, value);
    }

    public object? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public object? Subtitle
    {
        get => GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public TextAlignment TitleAlignment
    {
        get => GetValue(TitleAlignmentProperty);
        set => SetValue(TitleAlignmentProperty, value);
    }

    public double ExpandedHeight
    {
        get => GetValue(ExpandedHeightProperty);
        set => SetValue(ExpandedHeightProperty, value);
    }

    public double CollapsedHeight
    {
        get => GetValue(CollapsedHeightProperty);
        set => SetValue(CollapsedHeightProperty, value);
    }

    public Thickness SafeAreaPadding
    {
        get => GetValue(SafeAreaPaddingProperty);
        set => SetValue(SafeAreaPaddingProperty, value);
    }

    public object? LeadingContent
    {
        get => GetValue(LeadingContentProperty);
        set => SetValue(LeadingContentProperty, value);
    }

    public Geometry? LeadingIcon
    {
        get => GetValue(LeadingIconProperty);
        set => SetValue(LeadingIconProperty, value);
    }

    public ICommand? LeadingCommand
    {
        get => GetValue(LeadingCommandProperty);
        set => SetValue(LeadingCommandProperty, value);
    }

    public string? LeadingText
    {
        get => GetValue(LeadingTextProperty);
        set => SetValue(LeadingTextProperty, value);
    }

    public object? TrailingContent
    {
        get => GetValue(TrailingContentProperty);
        set => SetValue(TrailingContentProperty, value);
    }

    public object? Thumbnail
    {
        get => GetValue(ThumbnailProperty);
        set => SetValue(ThumbnailProperty, value);
    }

    public object? ImageContent
    {
        get => GetValue(ImageContentProperty);
        set => SetValue(ImageContentProperty, value);
    }

    public object? AvatarContent
    {
        get => GetValue(AvatarContentProperty);
        set => SetValue(AvatarContentProperty, value);
    }

    public string? AvatarText
    {
        get => GetValue(AvatarTextProperty);
        set => SetValue(AvatarTextProperty, value);
    }

    public object? SearchContent
    {
        get => GetValue(SearchContentProperty);
        set => SetValue(SearchContentProperty, value);
    }

    public string SearchPlaceholder
    {
        get => GetValue(SearchPlaceholderProperty);
        set => SetValue(SearchPlaceholderProperty, value);
    }

    public string? SearchText
    {
        get => GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    public MaterialAppBarScrollBehavior? ScrollBehavior
    {
        get => GetValue(ScrollBehaviorProperty);
        set => SetValue(ScrollBehaviorProperty, value);
    }

    public ScrollViewer? ScrollSource
    {
        get => GetValue(ScrollSourceProperty);
        set => SetValue(ScrollSourceProperty, value);
    }

    /// <summary>True once content has scrolled under the bar (overlapped fraction above 1%).</summary>
    public bool IsScrolled
    {
        get => _isScrolled;
        private set
        {
            if (SetAndRaise(IsScrolledProperty, ref _isScrolled, value))
                PseudoClasses.Set(":scrolled", value);
        }
    }

    public bool IsSingleRowLayout
    {
        get => _isSingleRowLayout;
        private set => SetAndRaise(IsSingleRowLayoutProperty, ref _isSingleRowLayout, value);
    }

    public bool IsTwoRowLayout
    {
        get => _isTwoRowLayout;
        private set => SetAndRaise(IsTwoRowLayoutProperty, ref _isTwoRowLayout, value);
    }

    public bool IsFlexibleLayout
    {
        get => _isFlexibleLayout;
        private set => SetAndRaise(IsFlexibleLayoutProperty, ref _isFlexibleLayout, value);
    }

    public bool HasFlexibleTextTitle
    {
        get => _hasFlexibleTextTitle;
        private set => SetAndRaise(HasFlexibleTextTitleProperty, ref _hasFlexibleTextTitle, value);
    }

    public bool HasSmallTitle
    {
        get => _hasSmallTitle;
        private set => SetAndRaise(HasSmallTitleProperty, ref _hasSmallTitle, value);
    }

    public bool IsSearchLayout
    {
        get => _isSearchLayout;
        private set => SetAndRaise(IsSearchLayoutProperty, ref _isSearchLayout, value);
    }

    public bool HasSubtitle
    {
        get => _hasSubtitle;
        private set => SetAndRaise(HasSubtitleProperty, ref _hasSubtitle, value);
    }

    /// <summary>Collapsed-row subtitle: flexible variants only, matching upstream smallSubtitle.</summary>
    public bool ShowCollapsedSubtitle
    {
        get => _showCollapsedSubtitle;
        private set => SetAndRaise(ShowCollapsedSubtitleProperty, ref _showCollapsedSubtitle, value);
    }

    public bool ShowTopSubtitle
    {
        get => _showTopSubtitle;
        private set => SetAndRaise(ShowTopSubtitleProperty, ref _showTopSubtitle, value);
    }

    public double ExpandedTitleBottomPadding
    {
        get => _expandedTitleBottomPadding;
        private set => SetAndRaise(ExpandedTitleBottomPaddingProperty, ref _expandedTitleBottomPadding, value);
    }

    /// <summary>Expanded-row subtitle: flexible variants only.</summary>
    public bool ShowExpandedSubtitle
    {
        get => _showExpandedSubtitle;
        private set => SetAndRaise(ShowExpandedSubtitleProperty, ref _showExpandedSubtitle, value);
    }

    public bool HasThumbnail
    {
        get => _hasThumbnail;
        private set => SetAndRaise(HasThumbnailProperty, ref _hasThumbnail, value);
    }

    public bool HasAvatar
    {
        get => _hasAvatar;
        private set => SetAndRaise(HasAvatarProperty, ref _hasAvatar, value);
    }

    public bool HasLeadingIcon
    {
        get => _hasLeadingIcon;
        private set => SetAndRaise(HasLeadingIconProperty, ref _hasLeadingIcon, value);
    }

    /// <summary>Resolved expanded height in DIPs (token or <see cref="ExpandedHeight"/>).</summary>
    public double EffectiveExpandedHeight => _effectiveExpandedHeight;

    /// <summary>Resolved collapsed height in DIPs (64 or <see cref="CollapsedHeight"/>).</summary>
    public double EffectiveCollapsedHeight => _effectiveCollapsedHeight;

    public double CollapsedFraction
    {
        get => _collapsedFraction;
        private set => SetAndRaise(CollapsedFractionProperty, ref _collapsedFraction, value);
    }

    public double OverlappedFraction
    {
        get => _overlappedFraction;
        private set => SetAndRaise(OverlappedFractionProperty, ref _overlappedFraction, value);
    }

    /// <summary>Alpha of the collapsed-row title, eased with TopTitleAlphaEasing.</summary>
    public double TopTitleOpacity
    {
        get => _topTitleOpacity;
        private set => SetAndRaise(TopTitleOpacityProperty, ref _topTitleOpacity, value);
    }

    /// <summary>Alpha of the expanded-row title: 1 - collapsedFraction.</summary>
    public double BottomTitleOpacity
    {
        get => _bottomTitleOpacity;
        private set => SetAndRaise(BottomTitleOpacityProperty, ref _bottomTitleOpacity, value);
    }

    public IBrush? RenderedBackground => _renderedBackground;
    public object? CollapsedTitleContent => _collapsedTitleContent;
    public object? ExpandedTitleContent => _expandedTitleContent;

    /// <summary>
    /// Vertical drag on the bar itself, ported from the source's <c>appBarDragModifier</c>. The
    /// bar expands when dragged down and collapses when dragged up; a release settles through
    /// <see cref="MaterialAppBarScrollBehavior.OnPostFling"/>.
    /// </summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (ScrollBehavior is not { } behavior || behavior.IsPinned)
            return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        if (IsInteractiveSource(e.Source))
            return;
        _dragging = true;
        _dragLastY = e.GetPosition(this).Y;
        _dragVelocity = 0;
        _dragLastTimestamp = e.Timestamp;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_dragging || ScrollBehavior is not { } behavior)
            return;
        var y = e.GetPosition(this).Y;
        var delta = y - _dragLastY;
        _dragLastY = y;
        var dt = Math.Max(1, e.Timestamp - _dragLastTimestamp) / 1000d;
        _dragLastTimestamp = e.Timestamp;
        _dragVelocity = delta / dt;
        behavior.CancelSettle();
        // rememberDraggableState { delta -> state.heightOffset += delta }
        behavior.State.HeightOffset += delta;
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_dragging)
            return;
        _dragging = false;
        e.Pointer.Capture(null);
        ScrollBehavior?.OnPostFling(_dragVelocity, this);
        _dragVelocity = 0;
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _dragging = false;
    }

    private static bool IsInteractiveSource(object? source)
    {
        var visual = source as Visual;
        while (visual is not null)
        {
            if (visual is Button or ToggleButton or TextBox or ComboBox
                or Avalonia.Controls.Primitives.RangeBase or ScrollBar)
                return true;
            visual = visual.GetVisualParent();
        }
        return false;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _searchHost = e.NameScope.Find<Panel>("PART_DefaultSearchHost");
        _stack = e.NameScope.Find<MaterialTopAppBarStack>("PART_Stack");
        if (_searchHost is not null)
        {
            var field = EnsureSearchField();
            if (!_searchHost.Children.Contains(field))
            {
                _searchHost.Children.Clear();
                _searchHost.Children.Add(field);
            }
            UpdateSearchBackground();
        }

        _topTitlePresenter = e.NameScope.Find<Control>("PART_Title");
        _bottomTitlePresenter = e.NameScope.Find<Control>("PART_ExpandedTitle");
        UpdateTitleAccessibility();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _scrollColorSubscription = this.GetResourceObservable("MaterialSurfaceContainerColor")
            .Subscribe(new ScrollColorObserver(this));
        UpdateAdapter();
        SubscribeState();
        UpdateScrollDerived();
        UpdateScrollAppearance();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _scrollColorSubscription?.Dispose();
        _scrollColorSubscription = null;
        _adapter?.Dispose();
        _adapter = null;
        UnsubscribeState();
        _containerAnimation?.Dispose();
        _containerAnimation = null;
        _containerBrush = null;
        SyncRenderedBackground();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var baseSize = base.MeasureOverride(availableSize);
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : baseSize.Width;
        var insets = SafeAreaPadding.Top + SafeAreaPadding.Bottom;

        // TopAppBarLayout uses max(height, titleHeight) + padding as its layout height, so a bar
        // whose requested height cannot fit its content grows to the content height and its
        // collapse limit covers that measured size (onSizeChanged -> adjustHeightOffsetLimit).
        var stack = _stack;
        var firstContent = 0d;
        var secondContent = 0d;
        if (stack is not null)
        {
            if (stack.Children.Count > 0)
                firstContent = stack.Children[0].DesiredSize.Height;
            if (stack.Children.Count > 1)
                secondContent = stack.Children[1].DesiredSize.Height;
        }

        if (IsTwoRowLayout)
        {
            _measuredCollapsedPart = Math.Max(_effectiveCollapsedHeight, firstContent);
            _measuredExpandedPart = Math.Max(
                Math.Max(0, _effectiveExpandedHeight - _effectiveCollapsedHeight),
                secondContent + ExpandedTitleBottomPadding);
        }
        else
        {
            var content = stack is not null
                ? firstContent
                : Math.Max(0, baseSize.Height - insets);
            _measuredCollapsedPart = Math.Max(_effectiveCollapsedHeight, content);
            _measuredExpandedPart = Math.Max(_effectiveExpandedHeight, content) + insets;
        }
        ApplyMeasuredLimit();

        var height = GetBarHeight();
        if (double.IsFinite(availableSize.Height))
            height = Math.Min(height, availableSize.Height);
        return new Size(width, height);
    }

    private void ApplyMeasuredLimit()
    {
        if (_state is null)
            return;
        var limit = -_measuredExpandedPart;
        if (Math.Abs(_state.HeightOffsetLimit - limit) < 1e-6)
            return;
        _state.HeightOffsetLimit = limit;
        _state.HeightOffset = _state.HeightOffset;
    }

    private double GetBarHeight()
    {
        var offset = ScrollBehavior?.State.HeightOffset ?? 0;
        var insets = SafeAreaPadding.Top + SafeAreaPadding.Bottom;
        if (IsTwoRowLayout)
        {
            // TwoRowsTopAppBar: the collapsed row never collapses and the expanded row is
            // expandedHeight - collapsedHeight tall. The expanded row's layout height is reduced
            // by the negative offset; the inset area stays part of the collapsed row.
            var expandedPart = Math.Max(0, _measuredExpandedPart + offset);
            return _measuredCollapsedPart + expandedPart + insets;
        }

        // TopAppBarLayout measures through the window-inset padding and then adds the height
        // offset, so the whole bar including its insets can collapse to zero.
        return Math.Max(0, _measuredExpandedPart + offset);
    }

    private void UpdateGeometry()
    {
        UpdateLayoutFlags();
        UpdateScrollLimit();
        UpdateScrollDerived();
        UpdateScrollAppearance();
        InvalidateMeasure();
    }

    /// <summary>
    /// Sets the collapse limit exactly like the source: a single-row bar collapses its full
    /// measured height (including window insets), while a two-row bar only collapses the expanded
    /// row (<c>expandedHeight - collapsedHeight</c>) and keeps the collapsed row visible.
    /// </summary>
    private void UpdateScrollLimit()
    {
        if (_state is null)
            return;
        // Same limit the source installs from onSizeChanged: the measured single-row height or
        // the measured expanded row of a two-row bar.
        _state.HeightOffsetLimit = -_measuredExpandedPart;
        _state.HeightOffset = _state.HeightOffset;
    }

    private void UpdateLayoutFlags()
    {
        var configuration = Configuration;
        IsSingleRowLayout = configuration is MaterialAppBarConfiguration.Small
            or MaterialAppBarConfiguration.SmallCentered
            or MaterialAppBarConfiguration.SmallImage
            or MaterialAppBarConfiguration.Search;
        IsTwoRowLayout = configuration is MaterialAppBarConfiguration.Medium
            or MaterialAppBarConfiguration.Large
            or MaterialAppBarConfiguration.MediumFlexible
            or MaterialAppBarConfiguration.LargeFlexible;
        IsFlexibleLayout = configuration is MaterialAppBarConfiguration.MediumFlexible
            or MaterialAppBarConfiguration.LargeFlexible;
        HasFlexibleTextTitle = IsFlexibleLayout && Title is string;
        UpdateTitleContents();
        IsSearchLayout = configuration == MaterialAppBarConfiguration.Search;
        HasThumbnail = configuration == MaterialAppBarConfiguration.SmallImage && Thumbnail is not null;
        HasSmallTitle = (IsSingleRowLayout && !HasThumbnail && !IsSearchLayout) || IsTwoRowLayout;
        HasSubtitle = Subtitle is not null;
        ShowCollapsedSubtitle = IsFlexibleLayout && HasSubtitle;
        ShowExpandedSubtitle = IsFlexibleLayout && HasSubtitle;
        ShowTopSubtitle = IsSingleRowLayout && HasSubtitle || ShowCollapsedSubtitle;
        ExpandedTitleBottomPadding = configuration is MaterialAppBarConfiguration.Large
            or MaterialAppBarConfiguration.LargeFlexible
            ? 28
            : 24;
        HasAvatar = AvatarContent is not null || !string.IsNullOrEmpty(AvatarText);
        HasLeadingIcon = LeadingIcon is not null && LeadingContent is null;

        _effectiveCollapsedHeight = double.IsFinite(CollapsedHeight)
            ? Math.Max(0, CollapsedHeight)
            : 64;
        var defaultExpanded = configuration switch
        {
            MaterialAppBarConfiguration.Medium => 112,
            MaterialAppBarConfiguration.Large => 152,
            MaterialAppBarConfiguration.MediumFlexible => HasSubtitle ? 136 : 112,
            MaterialAppBarConfiguration.LargeFlexible => HasSubtitle ? 152 : 120,
            _ => 64
        };
        _effectiveExpandedHeight = double.IsFinite(ExpandedHeight)
            ? Math.Max(0, ExpandedHeight)
            : defaultExpanded;
        if (!IsTwoRowLayout)
            _effectiveCollapsedHeight = _effectiveExpandedHeight;
        if (_effectiveExpandedHeight < _effectiveCollapsedHeight)
        {
            throw new ArgumentOutOfRangeException(nameof(ExpandedHeight),
                "The expanded height is expected to be greater or equal to the collapsed height.");
        }

        _measuredCollapsedPart = _effectiveCollapsedHeight;
        _measuredExpandedPart = IsTwoRowLayout
            ? _effectiveExpandedHeight - _effectiveCollapsedHeight
            : _effectiveExpandedHeight + SafeAreaPadding.Top + SafeAreaPadding.Bottom;

        RaisePropertyChanged(EffectiveExpandedHeightProperty, 0d, _effectiveExpandedHeight);
        RaisePropertyChanged(EffectiveCollapsedHeightProperty, 0d, _effectiveCollapsedHeight);
        InvalidateMeasure();
    }

    private void OnScrollBehaviorChanged(MaterialAppBarScrollBehavior? oldValue,
        MaterialAppBarScrollBehavior? newValue)
    {
        if (oldValue is not null)
            oldValue.CancelSettle();
        UnsubscribeState();
        UpdateAdapter();
        SubscribeState();
        UpdateScrollDerived();
        UpdateScrollAppearance();
        InvalidateMeasure();
    }

    private void SubscribeState()
    {
        var state = ScrollBehavior?.State;
        if (ReferenceEquals(_state, state))
            return;
        _state = state;
        if (_state is not null)
            _state.PropertyChanged += OnStatePropertyChanged;
        UpdateGeometry();
    }

    private void UnsubscribeState()
    {
        if (_state is not null)
            _state.PropertyChanged -= OnStatePropertyChanged;
        _state = null;
    }

    private void OnStatePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == MaterialAppBarState.HeightOffsetProperty
            || e.Property == MaterialAppBarState.HeightOffsetLimitProperty
            || e.Property == MaterialAppBarState.ContentOffsetProperty
            || e.Property == MaterialAppBarState.OverlappedFractionProperty)
        {
            UpdateScrollDerived();
            UpdateScrollAppearance();
            InvalidateMeasure();
        }
    }

    private void UpdateAdapter()
    {
        _adapter?.Dispose();
        _adapter = null;
        if (ScrollBehavior is not { } behavior)
            return;
        _adapter = new MaterialBarScrollAdapter(this, behavior);
        _adapter.Attach(ScrollSource);
        // Attachment feeds a pre-scrolled source into the state, which recomputes the derived
        // fractions. Refresh here as well so rebinding reconciles the bar even in the window
        // where the state subscription is being replaced.
        UpdateScrollDerived();
    }

    private void UpdateScrollDerived()
    {
        var state = ScrollBehavior?.State;
        CollapsedFraction = state?.CollapsedFraction ?? 0;
        OverlappedFraction = state?.OverlappedFraction ?? 0;
        IsScrolled = OverlappedFraction > 0.01;

        if (IsTwoRowLayout)
        {
            TopTitleOpacity = Easing.TopTitleAlpha(CollapsedFraction);
            BottomTitleOpacity = 1 - CollapsedFraction;
        }
        else
        {
            TopTitleOpacity = 1;
            BottomTitleOpacity = 0;
        }
        UpdateTitleContents();
        UpdateTitleAccessibility();
    }

    private void UpdateTitleContents()
    {
        var title = Title;
        var top = title is not Control || !IsTwoRowLayout || CollapsedFraction >= 0.5;
        var expanded = title is not Control || IsTwoRowLayout && CollapsedFraction < 0.5;
        if (!top)
            SetAndRaise(CollapsedTitleContentProperty, ref _collapsedTitleContent, null);
        if (!expanded)
            SetAndRaise(ExpandedTitleContentProperty, ref _expandedTitleContent, null);
        if (top)
            SetAndRaise(CollapsedTitleContentProperty, ref _collapsedTitleContent, title);
        if (expanded)
            SetAndRaise(ExpandedTitleContentProperty, ref _expandedTitleContent, title);
    }

    private void UpdateTitleAccessibility()
    {
        var fraction = CollapsedFraction;
        var topActive = !IsTwoRowLayout || fraction >= 0.5;
        var bottomActive = IsTwoRowLayout && fraction < 0.5;
        if (_topTitlePresenter is not null)
            AutomationProperties.SetAccessibilityView(_topTitlePresenter,
                topActive ? AccessibilityView.Content : AccessibilityView.Raw);
        if (_bottomTitlePresenter is not null)
            AutomationProperties.SetAccessibilityView(_bottomTitlePresenter,
                bottomActive ? AccessibilityView.Content : AccessibilityView.Raw);
    }

    private void UpdateScrollAppearance()
    {
        if (ScrollBehavior is not { } behavior)
        {
            _containerAnimation?.Dispose();
            _containerAnimation = null;
            _containerBrush = null;
            SyncRenderedBackground();
            return;
        }

        var surface = (Background as ISolidColorBrush)?.Color ?? ResolveColor("MaterialSurfaceColor", Colors.Transparent);
        var scrolled = ResolveColor("MaterialSurfaceContainerColor", surface);
        if (IsTwoRowLayout)
        {
            _containerAnimation?.Dispose();
            _containerAnimation = null;
            var fraction = Math.Max(Easing.FastOutLinearIn(CollapsedFraction), behavior.State.OverlappedFraction > 0.01 ? 1 : 0);
            ApplyContainerColor(Lerp(surface, scrolled, fraction));
            return;
        }

        var overlapTarget = behavior.State.OverlappedFraction > 0.01 ? scrolled : surface;
        if (_containerAnimation is null)
        {
            var initial = surface;
            _containerBrush = new SolidColorBrush(initial);
            SyncRenderedBackground();
            _containerAnimation = new MaterialColorAnimation(this, initial, ApplyContainerColor,
                MaterialSpring.EffectsDefault);
        }
        _containerAnimation.Retarget(overlapTarget);
    }

    private void ApplyContainerColor(Color color)
    {
        if (_containerBrush is null)
        {
            _containerBrush = new SolidColorBrush(color);
            SyncRenderedBackground();
        }
        else
        {
            _containerBrush.Color = color;
        }
    }

    private void SyncRenderedBackground() => SetAndRaise(RenderedBackgroundProperty, ref _renderedBackground, _containerBrush ?? Background);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BackgroundProperty)
        {
            _containerAnimation?.Dispose();
            _containerAnimation = null;
            _containerBrush = null;
            UpdateScrollAppearance();
        }
    }

    private Color ResolveColor(string key, Color fallback)
    {
        if (this.TryFindResource(key, out var value) && value is Color color)
            return color;
        return Background is ISolidColorBrush brush ? brush.Color : fallback;
    }

    private static Color Lerp(Color from, Color to, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color.FromArgb(
            (byte)Math.Round(from.A + (to.A - from.A) * t),
            (byte)Math.Round(from.R + (to.R - from.R) * t),
            (byte)Math.Round(from.G + (to.G - from.G) * t),
            (byte)Math.Round(from.B + (to.B - from.B) * t));
    }

    private Control EnsureSearchField()
    {
        if (_searchField is not null)
            return _searchField;

        _searchBox = new TextBox
        {
            MinWidth = 0,
            MinHeight = 56,
            Padding = new Thickness(20, 16),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(0),
            VerticalContentAlignment = VerticalAlignment.Center
        };
        _searchBox.Bind(TextBox.TextProperty, new Binding(nameof(SearchText))
        {
            Source = this,
            Mode = BindingMode.TwoWay
        });
        _searchBox.Bind(TextBox.PlaceholderTextProperty, new Binding(nameof(SearchPlaceholder)) { Source = this });
        _searchBox.Bind(AutomationProperties.NameProperty, new Binding(nameof(SearchPlaceholder)) { Source = this });
        _searchBox.KeyDown += OnSearchKeyDown;

        _searchField = new Border
        {
            Height = 56,
            CornerRadius = new CornerRadius(28),
            ClipToBounds = true,
            Child = _searchBox
        };
        return _searchField;
    }

    private void UpdateSearchBackground()
    {
        if (_searchField is null)
            return;
        // Extension: flat search sits on the surface container; the optional elevated variant
        // uses the next container tone.
        var key = Elevation == MaterialAppBarElevation.OnScroll
            ? "MaterialSurfaceContainerHighBrush"
            : "MaterialSurfaceContainerBrush";
        _searchBackgroundBinding?.Dispose();
        _searchBackgroundBinding = _searchField.Bind(Border.BackgroundProperty,
            _searchField.GetResourceObservable(key));
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SearchSubmitted?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            if (!string.IsNullOrEmpty(SearchText))
                SearchText = string.Empty;
            else
                SearchDismissed?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }
}

/// <summary>Cubic-bezier helpers used by app bar title alpha and container color transitions.</summary>
internal static class Easing
{
    /// <summary>Compose's TopTitleAlphaEasing: CubicBezierEasing(.8f, 0f, .8f, .15f).</summary>
    internal static double TopTitleAlpha(double fraction) =>
        CubicBezier(0.8, 0, 0.8, 0.15, fraction);

    /// <summary>Compose's FastOutLinearInEasing: CubicBezierEasing(0.4f, 0f, 1f, 1f).</summary>
    internal static double FastOutLinearIn(double fraction) =>
        CubicBezier(0.4, 0, 1, 1, fraction);

    internal static double CubicBezier(double x1, double y1, double x2, double y2, double x)
    {
        x = Math.Clamp(x, 0, 1);
        if (x <= 0) return 0;
        if (x >= 1) return 1;
        static double Sample(double t, double p1, double p2) =>
            3 * (1 - t) * (1 - t) * t * p1 + 3 * (1 - t) * t * t * p2 + t * t * t;
        var lo = 0d;
        var hi = 1d;
        for (var i = 0; i < 24; i++)
        {
            var mid = (lo + hi) / 2;
            if (Sample(mid, x1, x2) < x) lo = mid;
            else hi = mid;
        }
        return Sample((lo + hi) / 2, y1, y2);
    }
}