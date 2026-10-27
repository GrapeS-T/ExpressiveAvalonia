using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Theming;
using ExpressiveAvalonia.Tokens;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// Hoisted state of the search app bar's scroll reaction, ported from
/// <c>SearchBarScrollState</c>.
/// </summary>
/// <remarks>
/// <see cref="ScrollOffset"/> is the bar's own displacement in DIPs and is coerced to
/// [<see cref="ScrollOffsetLimit"/>, 0]. <see cref="ContentOffset"/> only accumulates the scroll
/// that the content actually consumed, which is what <see cref="OverlappedFraction"/> reads.
/// </remarks>
public sealed class MaterialSearchBarScrollState : AvaloniaObject
{
    /// <summary>Defines the <see cref="ScrollOffsetLimit"/> property.</summary>
    public static readonly StyledProperty<double> ScrollOffsetLimitProperty =
        AvaloniaProperty.Register<MaterialSearchBarScrollState, double>(
            nameof(ScrollOffsetLimit), -double.MaxValue);

    /// <summary>Defines the <see cref="ScrollOffset"/> property.</summary>
    public static readonly DirectProperty<MaterialSearchBarScrollState, double> ScrollOffsetProperty =
        AvaloniaProperty.RegisterDirect<MaterialSearchBarScrollState, double>(
            nameof(ScrollOffset), state => state.ScrollOffset,
            (state, value) => state.ScrollOffset = value);

    /// <summary>Defines the <see cref="ContentOffset"/> property.</summary>
    public static readonly StyledProperty<double> ContentOffsetProperty =
        AvaloniaProperty.Register<MaterialSearchBarScrollState, double>(nameof(ContentOffset));

    /// <summary>Defines the <see cref="OverlappedFraction"/> property.</summary>
    public static readonly DirectProperty<MaterialSearchBarScrollState, double> OverlappedFractionProperty =
        AvaloniaProperty.RegisterDirect<MaterialSearchBarScrollState, double>(
            nameof(OverlappedFraction), state => state.OverlappedFraction);

    private double _scrollOffset;
    private double _overlappedFraction;

    public MaterialSearchBarScrollState()
    {
        PropertyChanged += (_, e) =>
        {
            if (e.Property == ScrollOffsetLimitProperty ||
                e.Property == ScrollOffsetProperty ||
                e.Property == ContentOffsetProperty)
                UpdateOverlappedFraction();
        };
    }

    public MaterialSearchBarScrollState(double scrollOffsetLimit, double scrollOffset,
        double contentOffset) : this()
    {
        ScrollOffsetLimit = scrollOffsetLimit;
        _scrollOffset = Coerce(scrollOffset, scrollOffsetLimit);
        ContentOffset = contentOffset;
        UpdateOverlappedFraction();
    }

    /// <summary>
    /// The limit the search bar may be offset by, normally the negative collapsed height.
    /// </summary>
    public double ScrollOffsetLimit
    {
        get => GetValue(ScrollOffsetLimitProperty);
        set => SetValue(ScrollOffsetLimitProperty, value);
    }

    /// <summary>Current displacement due to scrolling; coerced to the limit and 0.</summary>
    public double ScrollOffset
    {
        get => _scrollOffset;
        set
        {
            var coerced = Coerce(value, ScrollOffsetLimit);
            if (SetAndRaise(ScrollOffsetProperty, ref _scrollOffset, coerced))
                UpdateOverlappedFraction();
        }
    }

    /// <summary>How much of the content's height was consumed below the bar.</summary>
    public double ContentOffset
    {
        get => GetValue(ContentOffsetProperty);
        set => SetValue(ContentOffsetProperty, value);
    }

    /// <summary>The fraction of the bar's area that content has scrolled behind, 0..1.</summary>
    public double OverlappedFraction => _overlappedFraction;

    private void UpdateOverlappedFraction()
    {
        var fraction = ComputeOverlappedFraction();
        if (Math.Abs(fraction - _overlappedFraction) < 1e-9)
            return;
        var old = _overlappedFraction;
        _overlappedFraction = fraction;
        RaisePropertyChanged(OverlappedFractionProperty, old, fraction);
    }

    // SearchBar.kt's overlappedFraction: (limit - contentOffset) clamped into [limit, 0] over the
    // limit. The clamp is applied to the difference, not to the content offset, so a positive
    // content offset reports no overlap instead of a negative one.
    private double ComputeOverlappedFraction()
    {
        var limit = ScrollOffsetLimit;
        if (limit == 0)
            return 0;
        var lower = Math.Min(limit, 0d);
        return 1 - Math.Clamp(limit - ContentOffset, lower, 0d) / limit;
    }

    private static double Coerce(double offset, double limit) =>
        Math.Clamp(offset, Math.Min(limit, 0d), 0d);
}

/// <summary>
/// The search bar's scroll behavior, ported from
/// <c>EnterAlwaysSearchBarScrollBehavior</c>.
/// </summary>
/// <remarks>
/// The state machine itself is source-exact: the pre-scroll pass consumes a change only in
/// forward layouts, the post-scroll pass consumes a pull-down in reverse layouts and accumulates
/// content offset otherwise, and the fling settle decays with the release velocity before snapping
/// to the nearer end with the <c>DefaultEffects</c> spring. Hosts wire it to a native
/// <see cref="ScrollViewer"/> through <see cref="MaterialSearchAppBar.ScrollSource"/>; the adapter
/// translates offset deltas into the source's positive-towards-the-start convention.
/// </remarks>
public sealed class MaterialSearchBarScrollBehavior
{
    private MaterialMotion.SpringAnimation? _snap;
    private MaterialMotion.DecayAnimation? _decay;
    private double _lastDecayValue;

    public MaterialSearchBarScrollBehavior(MaterialSearchBarScrollState? state = null,
        bool reverseLayout = false, Func<bool>? canScroll = null)
    {
        State = state ?? new MaterialSearchBarScrollState();
        ReverseLayout = reverseLayout;
        CanScroll = canScroll;
    }

    /// <summary>Creates the <c>enterAlwaysSearchBarScrollBehavior</c> behavior.</summary>
    public static MaterialSearchBarScrollBehavior EnterAlways(
        MaterialSearchBarScrollState? state = null, bool reverseLayout = false,
        Func<bool>? canScroll = null) =>
        new(state, reverseLayout, canScroll);

    /// <summary>The state this behavior reads and updates.</summary>
    public MaterialSearchBarScrollState State { get; }

    /// <summary>Whether the scrollable content has a reversed direction and layout.</summary>
    public bool ReverseLayout { get; }

    /// <summary>Optional source <c>canScroll</c> callback; null means always scrollable.</summary>
    public Func<bool>? CanScroll { get; set; }

    /// <summary>
    /// The snap spec, <c>MotionSchemeKeyTokens.DefaultEffects</c>. The decay uses the library's
    /// Android spline decay, matching <c>rememberSplineBasedDecay</c>.
    /// </summary>
    public MaterialSpring SnapSpring { get; init; } = MaterialSpring.EffectsDefault;

    /// <summary>Velocity left over after the last settle (<c>Velocity(0, remaining)</c>).</summary>
    public double RemainingVelocity { get; private set; }

    /// <summary>Pre-scroll pass. Returns the consumed Y delta in the source's convention.</summary>
    public double OnPreScroll(double availableY)
    {
        if (!CanScrollNow())
            return 0;
        CancelSettle();
        var previous = State.ScrollOffset;
        State.ScrollOffset += availableY;
        // The reversed layout still tracks the offset but never consumes the hide delta, so the
        // content keeps scrolling while the bar slides away.
        return !ReverseLayout && previous != State.ScrollOffset ? availableY : 0;
    }

    /// <summary>Post-scroll pass. Returns the consumed Y delta in the source's convention.</summary>
    public double OnPostScroll(double consumedY, double availableY)
    {
        if (!CanScrollNow())
            return 0;
        if (ReverseLayout && availableY > 0)
        {
            // Reversed layout: consume a pull down to reveal the bar, but never a pull up.
            State.ScrollOffset += availableY;
            State.ContentOffset += availableY;
            return availableY;
        }
        if (!ReverseLayout)
        {
            State.ScrollOffset += consumedY;
            State.ContentOffset += consumedY;
        }
        return 0;
    }

    /// <summary>
    /// Settles the bar after a drag or fling: decay with the release velocity, then snap to 0 or
    /// the limit by the scroll fraction.
    /// </summary>
    public void OnPostFling(double availableY, Control owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        RemainingVelocity = 0;
        if (!CanScrollNow())
            return;
        CancelSettle();

        var fraction = OffsetFraction();
        // The source skips settling when the bar is at rest or exactly at its limit.
        if (fraction < 0.01 || fraction == 1)
            return;

        var remainingVelocity = availableY;
        if (Math.Abs(availableY) > 1)
        {
            _lastDecayValue = 0;
            _decay = MaterialMotion.Decay(owner, 0, availableY, (value, velocity) =>
            {
                var delta = value - _lastDecayValue;
                var initialOffset = State.ScrollOffset;
                State.ScrollOffset = initialOffset + delta;
                var consumed = Math.Abs(initialOffset - State.ScrollOffset);
                _lastDecayValue = value;
                remainingVelocity = velocity;
                RemainingVelocity = velocity;
                // Stop as soon as the state clamps; the snap picks up from the clamped state.
                if (Math.Abs(delta - consumed) > 0.5)
                    FinishDecay(owner);
            });
            _decay.Completed += (_, _) => Snap(owner);
            return;
        }

        RemainingVelocity = remainingVelocity;
        Snap(owner);
    }

    /// <summary>Stops the decay/snap animations, for example when a new drag starts.</summary>
    public void CancelSettle()
    {
        _decay?.Dispose();
        _decay = null;
        _snap?.Dispose();
        _snap = null;
    }

    internal bool CanScrollNow() => CanScroll?.Invoke() ?? true;

    private double OffsetFraction()
    {
        var limit = State.ScrollOffsetLimit;
        return limit != 0 ? State.ScrollOffset / limit : 0;
    }

    private void FinishDecay(Control owner)
    {
        _decay?.Dispose();
        _decay = null;
        Snap(owner);
    }

    private void Snap(Control owner)
    {
        _snap?.Dispose();
        _snap = null;
        var fraction = OffsetFraction();
        if (fraction < 0.01 || fraction == 1)
            return;
        if (!(State.ScrollOffsetLimit < State.ScrollOffset && State.ScrollOffset < 0))
            return;

        var target = fraction < 0.5 ? 0d : State.ScrollOffsetLimit;
        // AnimationState(initialValue = scrollOffset).animateTo(...) starts from the current
        // offset without carrying the decay velocity.
        _snap = MaterialMotion.Animate(owner, State.ScrollOffset, target,
            value => State.ScrollOffset = value, SnapSpring, 0.01);
    }
}

/// <summary>
/// The pinned <c>AppBarWithSearch</c>: a surface whose navigation slot, search bar and action slot
/// are arranged in one row, with the search bar's resting/scrolled container colors and the
/// search-specific scroll-away behavior.
/// </summary>
/// <remarks>
/// <para>
/// Geometry matches SearchBar.kt: 4-unit navigation/action padding, 8 horizontal and 4 vertical
/// units around the search field, and a 360..720-unit field width that stays centered. The
/// surface is a traversal group, and the search field fades out while a full-screen search view is
/// collapsing (<c>targetValue == Collapsed</c>) and back in once it is closed, which is the
/// source's anti-flicker rule.
/// </para>
/// <para>
/// Colors: the app bar container color springs between
/// <see cref="TemplatedControl.Background"/> and <see cref="ScrolledBackground"/> with the
/// <c>DefaultEffects</c> spec, while the search bar switches between
/// <see cref="SearchBarBackground"/> and <see cref="ScrolledSearchBarBackground"/> without an
/// animation, exactly as <c>AppBarWithSearchImpl</c> animates only the app bar color. The switch
/// happens at an overlap fraction above 0.01. Tonal and shadow elevation are level 0 in the source
/// defaults and therefore have no separate surface here.
/// </para>
/// <para>
/// Scroll: assign a <see cref="ScrollBehavior"/> (normally
/// <see cref="MaterialSearchBarScrollBehavior.EnterAlways"/>) and a
/// <see cref="ScrollSource"/>; the bar translates up by the scroll offset, clips its bounds, and
/// reports the reduced height so following content moves with it. Reduced motion is honored by the
/// shared springs and by the state settling immediately.
/// </para>
/// <para>
/// The search bar is a real <see cref="MaterialSearchBar"/> so editing, selection,
/// clipboard, undo and input methods stay native and the collapsed bar and a
/// <see cref="MaterialSearchView"/> share one query and selection. When a
/// <see cref="SearchView"/> is assigned, the app bar opens it on
/// <see cref="MaterialSearchBar.ExpansionRequested"/> and mirrors the view's current/target
/// expansion; without a view the host owns <see cref="IsSearchExpanded"/> and
/// <see cref="SearchExpansionTarget"/> directly.
/// </para>
/// </remarks>
[TemplatePart("PART_Surface", typeof(Border))]
[TemplatePart("PART_SearchHost", typeof(Border))]
[TemplatePart("PART_SearchBarHost", typeof(ContentPresenter))]
[PseudoClasses(":scrolled", ":search-expanded")]
public class MaterialSearchAppBar : TemplatedControl
{
    /// <summary>Defines the <see cref="SearchBar"/> property.</summary>
    public static readonly StyledProperty<MaterialSearchBar?> SearchBarProperty =
        AvaloniaProperty.Register<MaterialSearchAppBar, MaterialSearchBar?>(nameof(SearchBar));

    /// <summary>Defines the <see cref="SearchView"/> property.</summary>
    public static readonly StyledProperty<MaterialSearchView?> SearchViewProperty =
        AvaloniaProperty.Register<MaterialSearchAppBar, MaterialSearchView?>(nameof(SearchView));

    /// <summary>Defines the <see cref="NavigationContent"/> property.</summary>
    public static readonly StyledProperty<object?> NavigationContentProperty =
        AvaloniaProperty.Register<MaterialSearchAppBar, object?>(nameof(NavigationContent));

    /// <summary>Defines the <see cref="ActionsContent"/> property.</summary>
    public static readonly StyledProperty<object?> ActionsContentProperty =
        AvaloniaProperty.Register<MaterialSearchAppBar, object?>(nameof(ActionsContent));

    /// <summary>Defines the <see cref="NavigationForeground"/> property.</summary>
    public static readonly StyledProperty<IBrush?> NavigationForegroundProperty =
        AvaloniaProperty.Register<MaterialSearchAppBar, IBrush?>(nameof(NavigationForeground));

    /// <summary>Defines the <see cref="ActionsForeground"/> property.</summary>
    public static readonly StyledProperty<IBrush?> ActionsForegroundProperty =
        AvaloniaProperty.Register<MaterialSearchAppBar, IBrush?>(nameof(ActionsForeground));

    /// <summary>Defines the <see cref="SearchBarForeground"/> property.</summary>
    public static readonly StyledProperty<IBrush?> SearchBarForegroundProperty =
        AvaloniaProperty.Register<MaterialSearchAppBar, IBrush?>(nameof(SearchBarForeground));

    /// <summary>Defines the <see cref="ScrolledBackground"/> property.</summary>
    public static readonly StyledProperty<IBrush?> ScrolledBackgroundProperty =
        AvaloniaProperty.Register<MaterialSearchAppBar, IBrush?>(nameof(ScrolledBackground));

    /// <summary>Defines the <see cref="SearchBarBackground"/> property.</summary>
    public static readonly StyledProperty<IBrush?> SearchBarBackgroundProperty =
        AvaloniaProperty.Register<MaterialSearchAppBar, IBrush?>(nameof(SearchBarBackground));

    /// <summary>Defines the <see cref="ScrolledSearchBarBackground"/> property.</summary>
    public static readonly StyledProperty<IBrush?> ScrolledSearchBarBackgroundProperty =
        AvaloniaProperty.Register<MaterialSearchAppBar, IBrush?>(nameof(ScrolledSearchBarBackground));

    /// <summary>Defines the <see cref="ContentPadding"/> property.</summary>
    public static readonly StyledProperty<Thickness> ContentPaddingProperty =
        AvaloniaProperty.Register<MaterialSearchAppBar, Thickness>(nameof(ContentPadding));

    /// <summary>Defines the <see cref="SafeAreaPadding"/> property.</summary>
    public static readonly StyledProperty<Thickness> SafeAreaPaddingProperty =
        AvaloniaProperty.Register<MaterialSearchAppBar, Thickness>(nameof(SafeAreaPadding));

    /// <summary>Defines the <see cref="ScrollBehavior"/> property.</summary>
    public static readonly StyledProperty<MaterialSearchBarScrollBehavior?> ScrollBehaviorProperty =
        AvaloniaProperty.Register<MaterialSearchAppBar, MaterialSearchBarScrollBehavior?>(
            nameof(ScrollBehavior));

    /// <summary>Defines the <see cref="ScrollSource"/> property.</summary>
    public static readonly StyledProperty<ScrollViewer?> ScrollSourceProperty =
        AvaloniaProperty.Register<MaterialSearchAppBar, ScrollViewer?>(nameof(ScrollSource));

    /// <summary>Defines the <see cref="IsSearchExpanded"/> property.</summary>
    public static readonly StyledProperty<bool> IsSearchExpandedProperty =
        AvaloniaProperty.Register<MaterialSearchAppBar, bool>(nameof(IsSearchExpanded));

    /// <summary>Defines the <see cref="SearchExpansionTarget"/> property.</summary>
    public static readonly StyledProperty<bool> SearchExpansionTargetProperty =
        AvaloniaProperty.Register<MaterialSearchAppBar, bool>(nameof(SearchExpansionTarget));

    /// <summary>Defines the <see cref="ExpandsToFullScreen"/> property.</summary>
    public static readonly StyledProperty<bool> ExpandsToFullScreenProperty =
        AvaloniaProperty.Register<MaterialSearchAppBar, bool>(nameof(ExpandsToFullScreen), true);

    /// <summary>Defines the <see cref="OverlappedFraction"/> property.</summary>
    public static readonly DirectProperty<MaterialSearchAppBar, double> OverlappedFractionProperty =
        AvaloniaProperty.RegisterDirect<MaterialSearchAppBar, double>(
            nameof(OverlappedFraction), bar => bar.OverlappedFraction);

    /// <summary>Defines the <see cref="IsScrolled"/> property.</summary>
    public static readonly DirectProperty<MaterialSearchAppBar, bool> IsScrolledProperty =
        AvaloniaProperty.RegisterDirect<MaterialSearchAppBar, bool>(
            nameof(IsScrolled), bar => bar.IsScrolled);

    /// <summary>Defines the <see cref="RenderedBackground"/> property.</summary>
    public static readonly DirectProperty<MaterialSearchAppBar, IBrush?> RenderedBackgroundProperty =
        AvaloniaProperty.RegisterDirect<MaterialSearchAppBar, IBrush?>(
            nameof(RenderedBackground), bar => bar.RenderedBackground);

    private Border? _surface;
    private Border? _searchHost;
    private MaterialSearchBar? _hookedBar;
    private Control? _viewAnchor;
    private MaterialSearchView? _hookedView;
    private MaterialSearchBarScrollState? _state;
    private MaterialBarSearchScrollAdapter? _adapter;
    private MaterialColorAnimation? _containerAnimation;
    private SolidColorBrush? _containerBrush;
    private IBrush? _renderedBackground;
    private SolidColorBrush? _searchBarBrush;
    private IBrush? _searchBarForegroundBrush;
    private bool _searchBarBackgroundOwned;
    private bool _searchBarForegroundOwned;
    private bool _hideAnchorOwned;
    private bool _anchorOwned;
    private Color? _restContainerColor;
    private double _naturalHeight;
    private bool _settingLimit;
    private bool _updatingAppearance;
    private double _overlappedFraction;
    private bool _isScrolled;
    private bool _settingDerived;
    private bool _dragging;
    private double _dragVelocity;
    private double _dragLastY;
    private long _dragLastTimestamp;

    /// <summary>
    /// Raised when the search bar asks to expand (a press on the field, the first typed character,
    /// or Down while focused). When <see cref="SearchView"/> is set the app bar opens it first.
    /// </summary>
    public event EventHandler? ExpansionRequested;

    public MaterialSearchAppBar()
    {
        // SearchBar.kt's draggable/clip layout clips the offset surface to its reported bounds.
        ClipToBounds = true;
    }

    /// <summary>
    /// The search field. When not set, the app bar creates a default
    /// <see cref="Controls.MaterialSearchBar"/> so the composition is usable on its own.
    /// </summary>
    public MaterialSearchBar? SearchBar
    {
        get => GetValue(SearchBarProperty);
        set => SetValue(SearchBarProperty, value);
    }

    /// <summary>
    /// The expanded search surface paired with this bar. The app bar opens it on expansion,
    /// mirrors its current/target expansion for the field's fade rule, and anchors its expansion
    /// to <see cref="SearchBar"/> when the view has no anchor yet.
    /// </summary>
    public MaterialSearchView? SearchView
    {
        get => GetValue(SearchViewProperty);
        set => SetValue(SearchViewProperty, value);
    }

    /// <summary>The content before the search field, normally one navigation icon button.</summary>
    public object? NavigationContent
    {
        get => GetValue(NavigationContentProperty);
        set => SetValue(NavigationContentProperty, value);
    }

    /// <summary>The content after the search field, normally icon buttons in a horizontal row.</summary>
    public object? ActionsContent
    {
        get => GetValue(ActionsContentProperty);
        set => SetValue(ActionsContentProperty, value);
    }

    /// <summary>Content color of <see cref="NavigationContent"/> (<c>AppBarTokens.LeadingIconColor</c>).</summary>
    public IBrush? NavigationForeground
    {
        get => GetValue(NavigationForegroundProperty);
        set => SetValue(NavigationForegroundProperty, value);
    }

    /// <summary>Content color of <see cref="ActionsContent"/> (<c>AppBarTokens.TrailingIconColor</c>).</summary>
    public IBrush? ActionsForeground
    {
        get => GetValue(ActionsForegroundProperty);
        set => SetValue(ActionsForegroundProperty, value);
    }

    /// <summary>
    /// Content color of the search field, <c>contentColorFor(searchBarColors.containerColor)</c>.
    /// The default is OnSurface.
    /// </summary>
    public IBrush? SearchBarForeground
    {
        get => GetValue(SearchBarForegroundProperty);
        set => SetValue(SearchBarForegroundProperty, value);
    }

    /// <summary>
    /// App bar container color while content overlaps it (<c>AppBarTokens.OnScrollContainerColor</c>).
    /// </summary>
    public IBrush? ScrolledBackground
    {
        get => GetValue(ScrolledBackgroundProperty);
        set => SetValue(ScrolledBackgroundProperty, value);
    }

    /// <summary>
    /// Search field container color at rest (<c>SearchBarTokens.ContainerColor</c>). When null the
    /// bar keeps its own theme background.
    /// </summary>
    public IBrush? SearchBarBackground
    {
        get => GetValue(SearchBarBackgroundProperty);
        set => SetValue(SearchBarBackgroundProperty, value);
    }

    /// <summary>
    /// Search field container color while content overlaps the app bar
    /// (<c>ColorSchemeKeyTokens.SurfaceContainerHighest</c>).
    /// </summary>
    public IBrush? ScrolledSearchBarBackground
    {
        get => GetValue(ScrolledSearchBarBackgroundProperty);
        set => SetValue(ScrolledSearchBarBackgroundProperty, value);
    }

    /// <summary>Inner padding of the row; the source default is zero (AppBarContentPadding).</summary>
    public Thickness ContentPadding
    {
        get => GetValue(ContentPaddingProperty);
        set => SetValue(ContentPaddingProperty, value);
    }

    /// <summary>Desktop window-inset equivalent, applied outside the content row.</summary>
    public Thickness SafeAreaPadding
    {
        get => GetValue(SafeAreaPaddingProperty);
        set => SetValue(SafeAreaPaddingProperty, value);
    }

    /// <summary>The scroll state machine. Null disables the scroll reaction and scrolled colors.</summary>
    public MaterialSearchBarScrollBehavior? ScrollBehavior
    {
        get => GetValue(ScrollBehaviorProperty);
        set => SetValue(ScrollBehaviorProperty, value);
    }

    /// <summary>The native scroller whose offset drives <see cref="ScrollBehavior"/>.</summary>
    public ScrollViewer? ScrollSource
    {
        get => GetValue(ScrollSourceProperty);
        set => SetValue(ScrollSourceProperty, value);
    }

    /// <summary>
    /// Whether the paired search surface is currently expanded. Read from the
    /// <see cref="SearchView"/> when one is assigned; otherwise set it from the host exactly like
    /// the source's hoisted <c>SearchBarState.currentValue</c>.
    /// </summary>
    public bool IsSearchExpanded
    {
        get => GetValue(IsSearchExpandedProperty);
        set => SetValue(IsSearchExpandedProperty, value);
    }

    /// <summary>
    /// Whether the paired search surface is expanding (its target is Expanded). Used with
    /// <see cref="IsSearchExpanded"/> to keep the field visible while expanding and hidden while
    /// collapsing.
    /// </summary>
    public bool SearchExpansionTarget
    {
        get => GetValue(SearchExpansionTargetProperty);
        set => SetValue(SearchExpansionTargetProperty, value);
    }

    /// <summary>
    /// Whether the paired search surface expands over the whole window. The docked package sets
    /// false, so the field stays visible behind the dropdown.
    /// </summary>
    public bool ExpandsToFullScreen
    {
        get => GetValue(ExpandsToFullScreenProperty);
        set => SetValue(ExpandsToFullScreenProperty, value);
    }

    /// <summary>Fraction of the bar's height that content has scrolled behind.</summary>
    public double OverlappedFraction => _overlappedFraction;

    /// <summary>True when <see cref="OverlappedFraction"/> is above the source's 0.01 threshold.</summary>
    public bool IsScrolled => _isScrolled;

    /// <summary>
    /// The brush the surface actually paints. It equals <see cref="TemplatedControl.Background"/>
    /// unless the scroll reaction is springing between the resting and scrolled container colors,
    /// in which case this is the animated brush. The template binds this property so
    /// <see cref="TemplatedControl.Background"/> remains the themable/animatable target and keeps
    /// following live theme changes.
    /// </summary>
    public IBrush? RenderedBackground => _renderedBackground;

    /// <summary>Moves keyboard focus into the search field.</summary>
    public void FocusInput() => SearchBar?.FocusInput();

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        // SearchBar.kt's behavior modifier is draggable: the bar itself can be dragged down to
        // reveal and up to hide. Interactive content (the field, the slots) keeps its own input.
        if (e.Handled || ScrollBehavior is not { } behavior || !behavior.CanScrollNow() ||
            IsInteractiveSource(e.Source))
            return;

        behavior.CancelSettle();
        _dragging = true;
        _dragVelocity = 0;
        _dragLastY = e.GetPosition(this).Y;
        _dragLastTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
        e.Pointer.Capture(this);
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (!_dragging || ScrollBehavior is not { } behavior)
            return;

        var y = e.GetPosition(this).Y;
        var delta = y - _dragLastY;
        _dragLastY = y;
        if (Math.Abs(delta) < 1e-6)
            return;

        behavior.State.ScrollOffset += delta;

        var now = System.Diagnostics.Stopwatch.GetTimestamp();
        var elapsed = (now - _dragLastTimestamp)
            / (double)System.Diagnostics.Stopwatch.Frequency;
        _dragLastTimestamp = now;
        if (elapsed > 0)
            _dragVelocity = delta / elapsed;
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (!_dragging)
            return;

        _dragging = false;
        e.Pointer.Capture(null);
        // A pause before release means no fling (the source's draggable reports the current
        // velocity, not the last movement's).
        var idle = (System.Diagnostics.Stopwatch.GetTimestamp() - _dragLastTimestamp)
            / (double)System.Diagnostics.Stopwatch.Frequency;
        if (idle > 0.1)
            _dragVelocity = 0;
        ScrollBehavior?.OnPostFling(_dragVelocity, this);
        _dragVelocity = 0;
        e.Handled = true;
    }

    /// <inheritdoc />
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
                or Avalonia.Controls.Primitives.RangeBase or ScrollBar or MaterialSearchBar)
                return true;
            visual = visual.GetVisualParent();
        }
        return false;
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _surface = e.NameScope.Find<Border>("PART_Surface");
        _searchHost = e.NameScope.Find<Border>("PART_SearchHost");

        EnsureSearchBar();
        HookSearchBar();
        HookSearchView();
        UpdateSearchSlot();
        UpdateSearchAppearance();
        InvalidateMeasure();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (Background is ISolidColorBrush attachedBrush)
            _restContainerColor = attachedBrush.Color;
        else
            _restContainerColor = null;
        SyncRenderedBackground();
        EnsureSearchBar();
        HookSearchBar();
        HookSearchView();
        SubscribeState();
        UpdateAdapter();
        UpdateSearchAppearance();
        InvalidateMeasure();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _adapter?.Dispose();
        _adapter = null;
        // Settles are driven by the control's dispatcher frames, not only by the adapter; a drag
        // or snap must not keep running once the bar leaves the tree even without a ScrollSource.
        ScrollBehavior?.CancelSettle();
        _containerAnimation?.Dispose();
        _containerAnimation = null;
        _containerBrush = null;
        SyncRenderedBackground();
        UnhookSearchBar();
        UnhookSearchView();
        UnsubscribeState();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SearchBarProperty)
        {
            HookSearchBar();
            RetargetViewAnchor();
            UpdateSearchAppearance();
        }
        else if (change.Property == SearchViewProperty)
        {
            HookSearchView();
            UpdateSearchSlot();
        }
        else if (change.Property == ScrollBehaviorProperty)
        {
            change.GetOldValue<MaterialSearchBarScrollBehavior?>()?.CancelSettle();
            UnsubscribeState();
            var behavior = ScrollBehavior;
            behavior?.CancelSettle();
            SubscribeState();
            UpdateAdapter();
            UpdateSearchAppearance();
            InvalidateMeasure();
        }
        else if (change.Property == ScrollSourceProperty)
        {
            UpdateAdapter();
        }
        else if (change.Property == BackgroundProperty)
        {
            // Background remains the real target: a theme or the host writes it and the resting
            // container color re-reads from it, so a live seed/dark change is picked up even while
            // the color spring runs. The rendered brush is a separate template channel.
            if (Background is ISolidColorBrush brush)
                _restContainerColor = brush.Color;
            else
                _restContainerColor = null;
            SyncRenderedBackground();
            UpdateSearchAppearance();
        }
        else if (change.Property == ScrolledBackgroundProperty ||
                 change.Property == SearchBarBackgroundProperty ||
                 change.Property == ScrolledSearchBarBackgroundProperty ||
                 change.Property == SearchBarForegroundProperty)
        {
            UpdateSearchAppearance();
        }
        else if (change.Property == IsSearchExpandedProperty)
        {
            PseudoClasses.Set(":search-expanded", change.GetNewValue<bool>());
            UpdateSearchSlot();
        }
        else if (change.Property == SearchExpansionTargetProperty ||
                 change.Property == ExpandsToFullScreenProperty)
        {
            UpdateSearchSlot();
        }
        else if (change.Property == ContentPaddingProperty ||
                 change.Property == SafeAreaPaddingProperty)
        {
            InvalidateMeasure();
        }
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        if (_surface is null)
            return base.MeasureOverride(availableSize);

        _surface.Measure(availableSize);
        var natural = _surface.DesiredSize.Height;
        _naturalHeight = natural;
        ApplyMeasuredLimit();

        var width = double.IsFinite(availableSize.Width)
            ? availableSize.Width
            : _surface.DesiredSize.Width;
        var scrollOffset = _state?.ScrollOffset ?? 0d;
        var height = Math.Max(0, natural + scrollOffset);
        if (double.IsFinite(availableSize.Height))
            height = Math.Min(height, availableSize.Height);
        return new Size(width, height);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_surface is null)
            return base.ArrangeOverride(finalSize);

        // placeWithLayer(0, scrollOffset) inside a layout whose height is the clipped remainder:
        // the bar keeps its natural height and its top edge leaves the reported bounds.
        var offset = _state?.ScrollOffset ?? 0;
        var height = _naturalHeight > 0 ? _naturalHeight : finalSize.Height;
        _surface.Arrange(new Rect(0, offset, finalSize.Width, height));
        return finalSize;
    }

    private void ApplyMeasuredLimit()
    {
        if (_state is null || _naturalHeight <= 0)
            return;
        var limit = -_naturalHeight;
        if (Math.Abs(_state.ScrollOffsetLimit - limit) < 1e-6)
            return;
        _settingLimit = true;
        try
        {
            _state.ScrollOffsetLimit = limit;
        }
        finally
        {
            _settingLimit = false;
        }
    }

    private void EnsureSearchBar()
    {
        if (SearchBar is not null)
            return;
        var bar = new MaterialSearchBar
        {
            AutoFocus = false,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        };
        SetCurrentValue(SearchBarProperty, bar);
    }

    private void HookSearchBar()
    {
        var bar = SearchBar;
        if (ReferenceEquals(bar, _hookedBar))
            return;
        UnhookSearchBar();
        _hookedBar = bar;
        if (_hookedBar is not null)
        {
            _hookedBar.ExpansionRequested += OnSearchBarExpansionRequested;
            // A ControlTheme applied when the bar enters the tree can reassert its own colors
            // after the first current-value override; put the app bar's field channel back.
            _hookedBar.PropertyChanged += OnSearchBarPropertyChanged;
        }
    }

    private void UnhookSearchBar()
    {
        if (_hookedBar is not { } bar)
            return;
        bar.ExpansionRequested -= OnSearchBarExpansionRequested;
        bar.PropertyChanged -= OnSearchBarPropertyChanged;
        RestoreSearchBarChannels(bar);
        _hookedBar = null;
    }

    private void OnSearchBarPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_hookedBar is not { } bar || !ReferenceEquals(sender, bar))
            return;
        if (e.Property == BackgroundProperty && _searchBarBackgroundOwned)
        {
            if (!ReferenceEquals(bar.Background, _searchBarBrush))
                bar.SetCurrentValue(BackgroundProperty, _searchBarBrush);
        }
        else if (e.Property == ForegroundProperty && _searchBarForegroundOwned)
        {
            if (!ReferenceEquals(bar.Foreground, _searchBarForegroundBrush))
                bar.SetCurrentValue(ForegroundProperty, _searchBarForegroundBrush);
        }
    }

    private void RestoreSearchBarChannels(MaterialSearchBar bar)
    {
        // SetCurrentValue above only overrode the effective value; clearing it drops exactly that
        // current value so the bar's own theme or a host binding keeps working. Drop ownership
        // first so the resulting property change is not treated as a theme reassert.
        var backgroundOwned = _searchBarBackgroundOwned;
        _searchBarBackgroundOwned = false;
        if (backgroundOwned && ReferenceEquals(bar.Background, _searchBarBrush))
            bar.ClearValue(BackgroundProperty);

        var foregroundOwned = _searchBarForegroundOwned;
        _searchBarForegroundOwned = false;
        if (foregroundOwned && ReferenceEquals(bar.Foreground, _searchBarForegroundBrush))
            bar.ClearValue(ForegroundProperty);
        _searchBarForegroundBrush = null;
    }

    private void OnSearchBarExpansionRequested(object? sender, EventArgs e)
    {
        if (SearchView is not { } view)
        {
            ExpansionRequested?.Invoke(this, EventArgs.Empty);
            return;
        }
        if (!view.IsOpen)
            view.Open();
        ExpansionRequested?.Invoke(this, EventArgs.Empty);
    }

    private void HookSearchView()
    {
        var view = SearchView;
        if (ReferenceEquals(view, _hookedView))
            return;
        UnhookSearchView();
        _hookedView = view;
        if (view is null)
            return;

        view.PropertyChanged += OnSearchViewPropertyChanged;
        // The app bar keeps the collapsed field in its row and controls its reveal with the
        // source's opacity rule, so the view must not remove the anchor from layout. The
        // current-value override is dropped again when the pairing ends; SetCurrentValue keeps a
        // caller's binding/style in place.
        _hideAnchorOwned = false;
        if (view.HideAnchorWhenExpanded)
        {
            view.SetCurrentValue(MaterialSearchView.HideAnchorWhenExpandedProperty, false);
            _hideAnchorOwned = !view.HideAnchorWhenExpanded;
        }

        var searchBar = SearchBar;
        if (view.ExpansionAnchor is null && searchBar is not null)
        {
            _viewAnchor = searchBar;
            view.SetCurrentValue(MaterialSearchView.ExpansionAnchorProperty, searchBar);
            _anchorOwned = ReferenceEquals(view.ExpansionAnchor, searchBar);
        }
        else if (view.ExpansionAnchor is not null)
        {
            _viewAnchor = view.ExpansionAnchor;
            _anchorOwned = false;
        }
        else
        {
            _viewAnchor = null;
            _anchorOwned = false;
        }

        SetCurrentValue(IsSearchExpandedProperty, view.IsExpanded);
        SetCurrentValue(SearchExpansionTargetProperty, view.IsExpansionTargetExpanded);
    }

    private void UnhookSearchView()
    {
        if (_hookedView is not { } view)
            return;
        view.PropertyChanged -= OnSearchViewPropertyChanged;
        if (_anchorOwned)
        {
            if (ReferenceEquals(view.ExpansionAnchor, _viewAnchor))
                view.ClearValue(MaterialSearchView.ExpansionAnchorProperty);
            _anchorOwned = false;
            _viewAnchor = null;
        }
        if (_hideAnchorOwned)
        {
            // Drop only our current-value override; the view's own style or a host binding
            // becomes effective again.
            if (!view.HideAnchorWhenExpanded)
                view.ClearValue(MaterialSearchView.HideAnchorWhenExpandedProperty);
            _hideAnchorOwned = false;
        }
        _hookedView = null;
    }

    private void RetargetViewAnchor()
    {
        if (_hookedView is not { } view)
            return;
        var current = view.ExpansionAnchor;
        if (current is not null &&
            !(_anchorOwned && ReferenceEquals(current, _viewAnchor)))
            return;

        _viewAnchor = SearchBar;
        view.SetCurrentValue(MaterialSearchView.ExpansionAnchorProperty, SearchBar);
        _anchorOwned = SearchBar is not null && ReferenceEquals(view.ExpansionAnchor, SearchBar);
    }

    private void OnSearchViewPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_hookedView is not { } view)
            return;
        if (e.Property == MaterialSearchView.IsExpandedProperty)
            SetCurrentValue(IsSearchExpandedProperty, view.IsExpanded);
        else if (e.Property == MaterialSearchView.IsExpansionTargetExpandedProperty)
            SetCurrentValue(SearchExpansionTargetProperty, view.IsExpansionTargetExpanded);
        else if (e.Property == MaterialSearchView.ModeProperty ||
                 e.Property == MaterialSearchView.IsOpenProperty)
            UpdateSearchSlot();
    }

    private bool EffectiveExpandsToFullScreen =>
        SearchView is { } view
            ? view.Mode == MaterialSearchViewMode.FullScreen
            : ExpandsToFullScreen;

    private void UpdateSearchSlot()
    {
        if (_searchHost is null)
            return;
        // AppBarWithSearch: !state.expandsToFullScreen || !state.isExpanded ||
        // state.targetValue == Expanded. The target clause keeps the field visible while the
        // full-screen surface expands, and hides it as soon as the collapse starts.
        var visible = !EffectiveExpandsToFullScreen || !IsSearchExpanded || SearchExpansionTarget;
        _searchHost.Opacity = visible ? 1 : 0;
    }

    private void SubscribeState()
    {
        var state = ScrollBehavior?.State;
        if (ReferenceEquals(_state, state))
            return;
        if (_state is not null)
            _state.PropertyChanged -= OnScrollStateChanged;
        _state = state;
        if (_state is not null)
            _state.PropertyChanged += OnScrollStateChanged;
    }

    private void UnsubscribeState()
    {
        if (_state is not null)
            _state.PropertyChanged -= OnScrollStateChanged;
        _state = null;
    }

    private void OnScrollStateChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != MaterialSearchBarScrollState.ScrollOffsetProperty &&
            e.Property != MaterialSearchBarScrollState.ScrollOffsetLimitProperty &&
            e.Property != MaterialSearchBarScrollState.ContentOffsetProperty &&
            e.Property != MaterialSearchBarScrollState.OverlappedFractionProperty)
            return;

        UpdateSearchAppearance();
        // The limit is installed from measurement; invalidating there would re-enter layout.
        if (!_settingLimit)
            InvalidateMeasure();
    }

    private void UpdateAdapter()
    {
        _adapter?.Dispose();
        _adapter = null;
        if (ScrollBehavior is not { } behavior || ScrollSource is not { } source)
            return;
        _adapter = new MaterialBarSearchScrollAdapter(this, behavior);
        _adapter.Attach(source);
    }

    private void UpdateSearchAppearance()
    {
        if (_updatingAppearance)
            return;
        _updatingAppearance = true;
        try
        {
            UpdateSearchAppearanceCore();
        }
        finally
        {
            _updatingAppearance = false;
        }
    }

    private void UpdateSearchAppearanceCore()
    {
        var fraction = ScrollBehavior?.State.OverlappedFraction ?? 0;
        SetDerived(fraction, fraction > 0.01);

        var rest = _restContainerColor
                   ?? SolidColor(Background)
                   ?? ResolveColor("MaterialSurfaceColor", null);
        if (rest is not { } restColor)
        {
            // The control theme has not supplied a container brush yet (this can run from a
            // property assignment before attachment). Leave the styled Background untouched; the
            // Background property change applies the colors once the theme value arrives.
            SyncRenderedBackground();
            UpdateSearchBarColors();
            return;
        }

        var scrolled = SolidColor(ScrolledBackground)
                       ?? ResolveColor("MaterialSurfaceContainerColor", restColor)
                       ?? restColor;
        var target = IsScrolled ? scrolled : restColor;

        if (ScrollBehavior is null)
        {
            _containerAnimation?.Dispose();
            _containerAnimation = null;
            // Without a scroll behavior the app bar has one static container color: paint the
            // themable Background directly so it keeps following color-scheme changes.
            _containerBrush = null;
            _restContainerColor = null;
            SyncRenderedBackground();
        }
        else if (_containerAnimation is null)
        {
            ApplyContainerColor(restColor);
            _containerAnimation = new MaterialColorAnimation(this, restColor, ApplyContainerColor,
                MaterialSpring.EffectsDefault);
            _containerAnimation.Retarget(target);
        }
        else
        {
            _containerAnimation.Retarget(target);
        }

        SyncRenderedBackground();
        UpdateSearchBarColors();
    }

    private void UpdateSearchBarColors()
    {
        if (SearchBar is not { } bar)
            return;

        if (SearchBarBackground is not null || ScrolledSearchBarBackground is not null)
        {
            var rest = SolidColor(SearchBarBackground)
                       ?? ResolveColor("MaterialSurfaceContainerHighColor",
                           SolidColor(Background) ?? Colors.Transparent)
                       ?? Colors.Transparent;
            var scrolled = SolidColor(ScrolledSearchBarBackground)
                           ?? ResolveColor("MaterialSurfaceContainerHighestColor", rest)
                           ?? rest;
            var target = IsScrolled ? scrolled : rest;
            _searchBarBrush ??= new SolidColorBrush(target);
            _searchBarBrush.Color = target;
            // Preserve a caller's source on the bar: SetCurrentValue overrides the effective
            // value without detaching a binding, and the unhook path clears only that override.
            if (!_searchBarBackgroundOwned ||
                !ReferenceEquals(bar.Background, _searchBarBrush))
            {
                bar.SetCurrentValue(BackgroundProperty, _searchBarBrush);
                _searchBarBackgroundOwned = true;
            }
        }
        else if (_searchBarBackgroundOwned)
        {
            _searchBarBackgroundOwned = false;
            if (ReferenceEquals(bar.Background, _searchBarBrush))
                bar.ClearValue(BackgroundProperty);
        }

        if (SearchBarForeground is { } foreground)
        {
            _searchBarForegroundBrush = foreground;
            if (!_searchBarForegroundOwned ||
                !ReferenceEquals(bar.Foreground, foreground))
            {
                bar.SetCurrentValue(ForegroundProperty, foreground);
                _searchBarForegroundOwned = true;
            }
        }
        else if (_searchBarForegroundOwned)
        {
            _searchBarForegroundOwned = false;
            if (ReferenceEquals(bar.Foreground, _searchBarForegroundBrush))
                bar.ClearValue(ForegroundProperty);
            _searchBarForegroundBrush = null;
        }
    }

    private void ApplyContainerColor(Color color)
    {
        if (_containerBrush is null)
            _containerBrush = new SolidColorBrush(color);
        else
            _containerBrush.Color = color;
        // Render through the template channel instead of assigning Background: a local Background
        // would mask the theme's dynamic resource and its live seed/dark updates.
        SyncRenderedBackground();
    }

    private void SyncRenderedBackground()
    {
        var value = _containerBrush ?? Background;
        if (!ReferenceEquals(_renderedBackground, value))
            SetAndRaise(RenderedBackgroundProperty, ref _renderedBackground, value);
    }

    private void SetDerived(double fraction, bool scrolled)
    {
        if (_settingDerived)
            return;
        _settingDerived = true;
        try
        {
            if (Math.Abs(_overlappedFraction - fraction) > 1e-9)
            {
                var old = _overlappedFraction;
                _overlappedFraction = fraction;
                RaisePropertyChanged(OverlappedFractionProperty, old, fraction);
            }
            if (_isScrolled != scrolled)
            {
                var old = _isScrolled;
                _isScrolled = scrolled;
                PseudoClasses.Set(":scrolled", scrolled);
                RaisePropertyChanged(IsScrolledProperty, old, scrolled);
            }
        }
        finally
        {
            _settingDerived = false;
        }
    }

    private Color? ResolveColor(string key, Color? fallback)
    {
        if (this.TryFindResource(key, out var value) && value is Color color)
            return color;
        return fallback;
    }

    private static Color? SolidColor(IBrush? brush) => (brush as ISolidColorBrush)?.Color;
}

/// <summary>
/// Avalonia adapter for <see cref="MaterialSearchBarScrollBehavior"/> driven by a native
/// <see cref="ScrollViewer"/>.
/// </summary>
/// <remarks>
/// Compose nested scroll lets the bar consume deltas before the content scrolls. Wheel and touch
/// gestures are intercepted before the presenter, matching the source pre-scroll pass; the
/// <see cref="ScrollViewer.ScrollChanged"/> path covers scrollbar, keyboard and programmatic
/// scrolling, where the native offset cannot be vetoed, so the consumed part is rewound. In a
/// reversed layout the hide delta is never consumed (the content keeps scrolling) and a pull down
/// is consumed in the post-scroll slot, as the pinned behavior specifies.
/// </remarks>
internal sealed class MaterialBarSearchScrollAdapter : IDisposable
{
    private const double WheelStep = 50d;

    private static readonly ConditionalWeakTable<ScrollViewer, MaterialBarSearchScrollAdapter> Adapters = new();

    static MaterialBarSearchScrollAdapter()
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
    private readonly MaterialSearchBarScrollBehavior _behavior;
    private readonly DispatcherTimer _flingTimer;
    private ScrollViewer? _scroller;
    private ScrollContentPresenter? _presenter;
    private double _lastOffset;
    private double _velocity;
    private long _lastScrollTimestamp;
    private bool _suppress;
    private bool _nativePassPending;
    private bool _nativePassScheduled;
    private double _nativePassStartOffset;
    private double _nativePassAvailable;

    public MaterialBarSearchScrollAdapter(Control owner, MaterialSearchBarScrollBehavior behavior)
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
            _scroller.RemoveHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelPre);
            Adapters.Remove(_scroller);
        }

        _scroller = scroller;
        _presenter = null;
        _flingTimer.Stop();
        _velocity = 0;
        _lastScrollTimestamp = 0;
        _nativePassPending = false;
        _nativePassScheduled = false;
        _nativePassAvailable = 0;
        if (scroller is null)
            return;

        scroller.ScrollChanged += OnScrollChanged;
        scroller.AddHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelPre,
            RoutingStrategies.Tunnel, true);
        Adapters.Remove(scroller);
        Adapters.Add(scroller, this);
        _lastOffset = scroller.Offset.Y;
    }

    public void Dispose()
    {
        Attach(null);
        _flingTimer.Stop();
        _behavior.CancelSettle();
    }

    /// <summary>
    /// Touch/wheel pre-scroll pass before the native presenter applies the delta.
    /// </summary>
    private void OnGesturePre(ScrollGestureEventArgs e)
    {
        if (_scroller is null || Math.Abs(e.Delta.Y) < 1e-6)
            return;
        var available = -e.Delta.Y;
        if (!_behavior.CanScrollNow())
            return;

        // The source's pre-scroll pass always updates the offset, even in a reversed layout where
        // it consumes nothing, so a negative (hide) delta must still reach the behavior here.
        var pre = _behavior.OnPreScroll(available);
        if (Math.Abs(pre) < 1e-6)
        {
            // Nothing was consumed before the presenter: let the content scroll and complete the
            // child-consumed/post half once after the native scroll is known.
            BeginNativePass(available);
            return;
        }

        // The source consumes the whole available delta whenever the offset changed, so the
        // presenter must not apply it again.
        e.Handled = true;
        _lastOffset = _scroller.Offset.Y;
        _velocity = pre * 60;
        StartFlingTimer();
    }

    private void OnPointerWheelPre(object? sender, PointerWheelEventArgs e)
    {
        if (_scroller is null || e.Delta.Y == 0)
            return;
        if (e.Source is not Visual source ||
            (!ReferenceEquals(source, _scroller) && !_scroller.IsVisualAncestorOf(source)))
            return;
        if (!_behavior.CanScrollNow())
            return;

        var units = UnitsPerDip();
        var step = _presenter?.Child is ILogicalScrollable { IsLogicalScrollEnabled: true } logical
            ? logical.ScrollSize.Height * units
            : WheelStep;
        var available = e.Delta.Y * step;
        // The source's pre-scroll pass always updates the offset, even in a reversed layout where
        // it consumes nothing, so a negative (hide) delta must still reach the behavior here.
        var pre = _behavior.OnPreScroll(available);
        if (Math.Abs(pre) < 1e-6)
        {
            // Nothing was consumed before the presenter: let the content scroll and complete the
            // child-consumed/post half once after the native scroll is known.
            BeginNativePass(available);
            return;
        }

        e.Handled = true;
        _lastOffset = _scroller.Offset.Y;
        _velocity = pre * 60;
        StartFlingTimer();
    }

    /// <summary>
    /// Records a pre-scroll delta that the bar did not consume and lets the native viewer apply
    /// it. The post-scroll half runs once the input event has finished, when the content's actual
    /// consumption is known; ScrollChanged suppresses its own pre pass for the same delta.
    /// </summary>
    private void BeginNativePass(double available)
    {
        if (_scroller is null)
            return;
        if (!_nativePassPending)
        {
            _nativePassPending = true;
            _nativePassStartOffset = _scroller.Offset.Y;
            _nativePassAvailable = 0;
        }
        _nativePassAvailable += available;
        if (_nativePassScheduled)
            return;
        _nativePassScheduled = true;
        Dispatcher.UIThread.Post(CompleteNativePass, DispatcherPriority.Background);
    }

    private void CompleteNativePass()
    {
        _nativePassScheduled = false;
        if (!_nativePassPending || _scroller is null)
            return;

        var available = _nativePassAvailable;
        var startOffset = _nativePassStartOffset;
        _nativePassPending = false;
        _nativePassAvailable = 0;
        // ScrollChanged is delivered after layout and may arrive after this callback. This
        // native movement has already participated in pre/post; do not replay it as a new pass.
        _lastOffset = _scroller.Offset.Y;

        var units = UnitsPerDip();
        // Compose nested-scroll Y is positive towards the content start. The child consumed the
        // native delta; a reversed layout can additionally consume a pull-down remainder in post.
        var nativeConsumed = (startOffset - _scroller.Offset.Y) * units;
        if (_behavior.ReverseLayout)
        {
            var leftover = available - nativeConsumed;
            if (leftover > 0)
            {
                _behavior.OnPostScroll(0, leftover);
                _velocity = leftover * 60;
                StartFlingTimer();
            }
        }
        else
        {
            // Forward layouts accumulate the child-consumed delta, including the content that
            // scrolled while the bar was already at rest or at its limit.
            _behavior.OnPostScroll(nativeConsumed, 0);
        }
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

        var units = UnitsPerDip();
        // Compose nested-scroll Y is positive towards the content start; a native offset grows in
        // the opposite direction.
        var available = -delta * units;
        _behavior.CancelSettle();

        var velocityDelta = delta;
        if (_nativePassPending)
        {
            // The wheel/gesture pre pass already ran OnPreScroll for this input; this native
            // change is the child consuming it. The post half is completed once per input pass.
            _lastOffset = current;
        }
        else if (_behavior.ReverseLayout)
        {
            // Scrollbar, keyboard and programmatic paths: run the pre pass (it tracks the offset
            // but never consumes in a reversed layout) and let the native offset stand. The child
            // consumed `available`, so no pull-down remainder reaches the post half.
            _behavior.OnPreScroll(available);
            _lastOffset = current;
        }
        else
        {
            // Forward layouts consume the pre pass before the content. A native scroll cannot be
            // vetoed up front here, so rewind the part the bar took and report the rest as the
            // child-consumed delta in post.
            var pre = _behavior.OnPreScroll(available);
            var applied = delta + pre / units;
            var consumed = -applied * units;
            if (Math.Abs(applied - delta) > 1e-6)
            {
                var targetOffset = _lastOffset + applied;
                _suppress = true;
                try
                {
                    _scroller.Offset = new Vector(_scroller.Offset.X, targetOffset);
                }
                finally
                {
                    _suppress = false;
                }

                // ScrollChanged may have run synchronously inside the setter; assigning the
                // absolute position keeps the bookkeeping the same either way.
                _lastOffset = targetOffset;
            }
            else
            {
                _lastOffset += applied;
            }

            _behavior.OnPostScroll(consumed, 0);
            velocityDelta = applied;
        }

        var now = System.Diagnostics.Stopwatch.GetTimestamp();
        var elapsed = _lastScrollTimestamp == 0
            ? 1d / 60
            : Math.Max(1d / 240, (now - _lastScrollTimestamp)
                / (double)System.Diagnostics.Stopwatch.Frequency);
        _lastScrollTimestamp = now;
        _velocity = -velocityDelta * units / elapsed;
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