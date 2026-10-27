using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Input.GestureRecognizers;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Controls;

/// <summary>The pinned carousel arrangements plus the design-source extensions.</summary>
public enum MaterialCarouselLayout
{
    /// <summary>One large card and a narrow peek of the next card (pinned hero list, start aligned).</summary>
    Hero,

    /// <summary>The pinned centered-hero arrangement: a large card with equal peeks on both sides.</summary>
    CenterAlignedHero,

    /// <summary>The pinned multi-browse arrangement: large, medium and small focal cards.</summary>
    MultiBrowse,

    /// <summary>The pinned uncontained arrangement: equal cards that bleed off both edges.</summary>
    Uncontained,

    /// <summary>
    /// Variable-width cards from <see cref="MaterialCarouselItem.AspectRatio"/> with the pinned
    /// carouselParallaxScrollEffect edge mask and content translation.
    /// </summary>
    MultiAspectRatio,

    /// <summary>
    /// Design-source extension: one full-viewport card per page, expressed as a one-keyline strategy.
    /// </summary>
    FullScreen,
}

/// <summary>
/// How a fling settles. These map the pinned Pager fling behaviors onto Avalonia's scroll snap types.
/// </summary>
public enum MaterialCarouselFlingBehavior
{
    /// <summary>At most one item per fling (pinned <c>singleAdvanceFlingBehavior</c>).</summary>
    SingleAdvance,

    /// <summary>Snap to the item nearest the velocity-based fling target (pinned <c>multiBrowseFlingBehavior</c>).</summary>
    Velocity,

    /// <summary>No snap after a fling (pinned <c>noSnapFlingBehavior</c>, used by Uncontained).</summary>
    NoSnap,
}

/// <summary>
/// A Material 3 carousel: a horizontally scrolling strip of cards laid out and masked with the
/// pinned keyline strategy, with single selection and all pinned arrangements.
/// </summary>
/// <remarks>
/// <para>
/// The layouts use the pinned <c>CarouselStrategy</c> (multi-browse, uncontained, hero and centered
/// hero) and <c>KeylineSnapPosition</c> algorithms. The old fixed width ladder and the
/// mobile/tablet "context" switch are gone: item sizes now derive from the arranged width exactly
/// as in Compose. <see cref="MultiAspectRatio"/> and <see cref="FullScreen"/> are retained as
/// design-source extensions (the pinned Pager only supports one uniform page size); they are listed
/// in the port docs as extensions.
/// </para>
/// <para>
/// The control is a <see cref="ListBox"/>: selection, disabled cards, focus, pointer selection and
/// SelectionItemPattern come from Avalonia. The strip realizes only the visible window and reuses
/// containers, publishes item snap offsets as irregular snap points, and settles flings according to
/// <see cref="FlingBehavior"/>. <see cref="CurrentItem"/> is the scroll-derived pinned
/// <c>CarouselState.currentItem</c>; selection follows it while scrolling and setting the selection
/// scrolls to the item, so the two never diverge.
/// </para>
/// <para>
/// Reduced motion (the inherited <see cref="MaterialMotion.IsReducedProperty"/> / platform
/// preference) turns animated scrolls into immediate jumps.
/// </para>
/// </remarks>
[TemplatePart("PART_ScrollViewer", typeof(ScrollViewer))]
[TemplatePart("PART_ItemsPresenter", typeof(ItemsPresenter))]
public class MaterialCarousel : ListBox
{
    private static readonly FuncTemplate<Panel?> DefaultPanel = new(() => new MaterialCarouselPanel());

    /// <summary>The arrangement.</summary>
    public static readonly StyledProperty<MaterialCarouselLayout> LayoutProperty =
        AvaloniaProperty.Register<MaterialCarousel, MaterialCarouselLayout>(nameof(Layout));

    /// <summary>Space between cards. The pinned default is 0; the design source used 8.</summary>
    public static readonly StyledProperty<double> ItemSpacingProperty =
        AvaloniaProperty.Register<MaterialCarousel, double>(nameof(ItemSpacing),
            CarouselDefaults.ItemSpacing, validate: value => double.IsFinite(value) && value >= 0);

    /// <summary>
    /// Card height. NaN (the default) fills the cross axis, as the pinned item modifier does; a
    /// positive value fixes the design-source card height.
    /// </summary>
    public static readonly StyledProperty<double> ItemHeightProperty =
        AvaloniaProperty.Register<MaterialCarousel, double>(nameof(ItemHeight), double.NaN,
            validate: value => double.IsNaN(value) || (double.IsFinite(value) && value >= 0));

    /// <summary>
    /// Preferred large item size. NaN targets the whole content area. The pinned API requires this
    /// value per call site; the control maps its absence to the container width.
    /// </summary>
    public static readonly StyledProperty<double> PreferredItemWidthProperty =
        AvaloniaProperty.Register<MaterialCarousel, double>(nameof(PreferredItemWidth), double.NaN,
            validate: value => double.IsNaN(value) || (double.IsFinite(value) && value > 0));

    /// <summary>Item size for <see cref="MaterialCarouselLayout.Uncontained"/>; NaN uses the content area.</summary>
    public static readonly StyledProperty<double> ItemWidthProperty =
        AvaloniaProperty.Register<MaterialCarousel, double>(nameof(ItemWidth), double.NaN,
            validate: value => double.IsNaN(value) || (double.IsFinite(value) && value > 0));

    /// <summary>Minimum size a strategy may choose for a small item (pinned default 40 dp).</summary>
    public static readonly StyledProperty<double> MinSmallItemWidthProperty =
        AvaloniaProperty.Register<MaterialCarousel, double>(nameof(MinSmallItemWidth),
            CarouselDefaults.MinSmallItemSize, validate: value => double.IsFinite(value) && value > 0);

    /// <summary>Maximum size a strategy may choose for a small item (pinned default 56 dp).</summary>
    public static readonly StyledProperty<double> MaxSmallItemWidthProperty =
        AvaloniaProperty.Register<MaterialCarousel, double>(nameof(MaxSmallItemWidth),
            CarouselDefaults.MaxSmallItemSize, validate: value => double.IsFinite(value) && value >= 0);

    /// <summary>Fling settling behavior; change it to override the layout default.</summary>
    public static readonly StyledProperty<MaterialCarouselFlingBehavior> FlingBehaviorProperty =
        AvaloniaProperty.Register<MaterialCarousel, MaterialCarouselFlingBehavior>(nameof(FlingBehavior));

    /// <summary>Snaps scrolling to item starts. Turn off for free scrolling (design extension).</summary>
    public static readonly StyledProperty<bool> IsSnapToItemsProperty =
        AvaloniaProperty.Register<MaterialCarousel, bool>(nameof(IsSnapToItems), true);

    /// <summary>
    /// Whether user gestures, the wheel, the paging keys and accessibility scroll actions may scroll
    /// the strip (pinned <c>userScrollEnabled</c>, default <c>true</c>).
    /// </summary>
    /// <remarks>
    /// <c>false</c> reproduces the pinned Pager: drag/fling, wheel, PageUp/PageDown and the
    /// accessibility scroll actions stop scrolling, while the cards stay composed, enabled,
    /// focusable and clickable and <see cref="ScrollToItem"/> / <see cref="AnimateScrollToItem"/>
    /// still scroll. Item activation and focus are untouched; the design-source selection scroll
    /// maps the pinned bring-into-view scroll, which the source does not gate either.
    /// </remarks>
    public static readonly StyledProperty<bool> UserScrollEnabledProperty =
        AvaloniaProperty.Register<MaterialCarousel, bool>(nameof(UserScrollEnabled), true);

    /// <summary>The scroll-derived current item (pinned <c>CarouselState.currentItem</c>).</summary>
    public static readonly DirectProperty<MaterialCarousel, int> CurrentItemProperty =
        AvaloniaProperty.RegisterDirect<MaterialCarousel, int>(nameof(CurrentItem), carousel => carousel.CurrentItem);

    private ScrollViewer? _scrollViewer;
    private ScrollContentPresenter? _scrollPresenter;
    private readonly List<GestureRecognizer> _detachedScrollGestures = [];
    private readonly List<ScrollBar> _hostScrollBars = [];
    private CarouselAutomationPeer? _automationPeer;
    private IPointer? _activeScrollPointer;
    private bool _gestureRestorePending;
    private bool _userScrollGuardApplied;
    private double? _scrollOffsetBeforeWheel;
    private MaterialCarouselPanel? _panel;
    private MaterialMotion.SpringAnimation? _offsetAnimation;
    private int _currentItem = -1;
    private object? _currentItemObject;
    private int _selectionTarget = -1;
    // Item whose snapped offset has to be applied once the strip and scroll viewer exist.
    private int _pendingItem = -1;
    private bool _syncingSelection;
    private bool _flingBehaviorExplicit;
    private bool _updatingFlingDefault;

    static MaterialCarousel()
    {
        ItemsPanelProperty.OverrideDefaultValue<MaterialCarousel>(DefaultPanel);
        LayoutProperty.Changed.AddClassHandler<MaterialCarousel>((carousel, _) => carousel.OnLayoutChanged());
        ItemSpacingProperty.Changed.AddClassHandler<MaterialCarousel>((carousel, _) => carousel.InvalidateStripMeasure());
        ItemHeightProperty.Changed.AddClassHandler<MaterialCarousel>((carousel, _) => carousel.InvalidateStripMeasure());
        PreferredItemWidthProperty.Changed.AddClassHandler<MaterialCarousel>((carousel, _) => carousel.InvalidateStripMeasure());
        ItemWidthProperty.Changed.AddClassHandler<MaterialCarousel>((carousel, _) => carousel.InvalidateStripMeasure());
        MinSmallItemWidthProperty.Changed.AddClassHandler<MaterialCarousel>((carousel, _) => carousel.InvalidateStripMeasure());
        MaxSmallItemWidthProperty.Changed.AddClassHandler<MaterialCarousel>((carousel, _) => carousel.InvalidateStripMeasure());
        FlingBehaviorProperty.Changed.AddClassHandler<MaterialCarousel>((carousel, _) => carousel.OnFlingBehaviorChanged());
        IsSnapToItemsProperty.Changed.AddClassHandler<MaterialCarousel>((carousel, _) => carousel.UpdateSnapType());
        UserScrollEnabledProperty.Changed.AddClassHandler<MaterialCarousel>((carousel, _) => carousel.OnUserScrollEnabledChanged());
        FlowDirectionProperty.Changed.AddClassHandler<MaterialCarousel>((carousel, _) => carousel.InvalidateStripMeasure());
    }

    public MaterialCarousel()
    {
        SelectionChanged += HandleSelectionChanged;
        _updatingFlingDefault = true;
        SetCurrentValue(FlingBehaviorProperty, DefaultFlingBehavior(Layout));
        _updatingFlingDefault = false;
    }

    /// <summary>Raised when a card is activated by tap, Enter, or Space.</summary>
    public event EventHandler<MaterialCarouselItemInvokedEventArgs>? ItemInvoked;

    public MaterialCarouselLayout Layout
    {
        get => GetValue(LayoutProperty);
        set => SetValue(LayoutProperty, value);
    }

    public double ItemSpacing
    {
        get => GetValue(ItemSpacingProperty);
        set => SetValue(ItemSpacingProperty, value);
    }

    public double ItemHeight
    {
        get => GetValue(ItemHeightProperty);
        set => SetValue(ItemHeightProperty, value);
    }

    public double PreferredItemWidth
    {
        get => GetValue(PreferredItemWidthProperty);
        set => SetValue(PreferredItemWidthProperty, value);
    }

    public double ItemWidth
    {
        get => GetValue(ItemWidthProperty);
        set => SetValue(ItemWidthProperty, value);
    }

    public double MinSmallItemWidth
    {
        get => GetValue(MinSmallItemWidthProperty);
        set => SetValue(MinSmallItemWidthProperty, value);
    }

    public double MaxSmallItemWidth
    {
        get => GetValue(MaxSmallItemWidthProperty);
        set => SetValue(MaxSmallItemWidthProperty, value);
    }

    public MaterialCarouselFlingBehavior FlingBehavior
    {
        get => GetValue(FlingBehaviorProperty);
        set => SetValue(FlingBehaviorProperty, value);
    }

    public bool IsSnapToItems
    {
        get => GetValue(IsSnapToItemsProperty);
        set => SetValue(IsSnapToItemsProperty, value);
    }

    /// <summary>Whether user input and accessibility actions may scroll the strip.</summary>
    public bool UserScrollEnabled
    {
        get => GetValue(UserScrollEnabledProperty);
        set => SetValue(UserScrollEnabledProperty, value);
    }

    /// <summary>The item closest to its snapped position while scrolling.</summary>
    public int CurrentItem
    {
        get => _currentItem;
        private set => SetAndRaise(CurrentItemProperty, ref _currentItem, value);
    }

    /// <summary>The strip that arranges the cards, once the template has been applied.</summary>
    public Panel? ItemPanel => _panel;

    /// <summary>Number of items currently realized by the strip (window size), for diagnostics.</summary>
    public int RealizedItemCount => _panel?.RealizedCount ?? 0;

    /// <summary>Jumps to the item's snapped position (pinned <c>scrollToItem</c>).</summary>
    public void ScrollToItem(int itemIndex) => ScrollToItemCore(itemIndex, animate: false);

    /// <summary>
    /// Animates to the item's snapped position with the pinned Pager spring (stiffness 400,
    /// damping 1, i.e. <c>Spring.StiffnessMediumLow</c> / no bounce). Far jumps pre-jump to one
    /// viewport away, as the pinned source does.
    /// </summary>
    public void AnimateScrollToItem(int itemIndex) => ScrollToItemCore(itemIndex, animate: true);

    internal void ScrollToItemCore(int itemIndex, bool animate)
    {
        if (_panel is null || _scrollViewer is null)
        {
            // The strip or the scroll viewer is created by the first layout pass; remember the
            // request so AttachPanel can apply it through the panel's anchor mechanism.
            if (itemIndex >= 0)
                _pendingItem = itemIndex;
            return;
        }

        var count = ItemCount;
        if (count == 0)
            return;

        var index = Math.Clamp(itemIndex, 0, count - 1);
        _selectionTarget = index;
        _offsetAnimation?.Dispose();
        _offsetAnimation = null;

        var targetLogical = _panel.GetTargetOffset(index);
        var viewer = _scrollViewer;
        var animateThisScroll =
            animate &&
            !MaterialMotion.GetIsReduced(this) &&
            this.IsAttachedToVisualTree() &&
            Math.Abs(_panel.ToPhysicalOffset(targetLogical) - viewer.Offset.X) >= 0.5;

        if (!animateThisScroll)
        {
            // Preserve this explicit request across a pending metrics/direction rebuild.
            _panel.RequestAnchor(index);
            _panel.InvalidateMeasure();
            ApplyOffset(viewer, _panel.ToPhysicalOffset(targetLogical));
            return;
        }

        TryPreJump(index, viewer);

        var from = viewer.Offset.X;
        var to = _panel.ToPhysicalOffset(targetLogical);
        if (Math.Abs(to - from) < 0.5)
        {
            ApplyOffset(viewer, to);
            return;
        }

        _offsetAnimation = MaterialMotion.Animate(
            this,
            from,
            to,
            value => ApplyOffset(viewer, value),
            stiffness: 400,
            dampingRatio: 1,
            visibilityThreshold: 0.01);
    }

    internal void HandleItemsChangedFromPanel(System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        _offsetAnimation?.Dispose();
        _offsetAnimation = null;
        _selectionTarget = -1;

        var count = ItemCount;
        var index = _currentItemObject is not null ? Items.IndexOf(_currentItemObject) : -1;
        if (index < 0)
            index = count == 0 ? -1 : Math.Clamp(_currentItem, 0, count - 1);

        CurrentItem = index;
        _currentItemObject = index >= 0 ? Items[index] : null;
        if (index >= 0)
        {
            _panel?.RequestAnchor(index, 0);
            SyncSelection(index);
        }
    }

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) =>
        new MaterialCarouselItem();

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey) =>
        NeedsContainer<MaterialCarouselItem>(item, out recycleKey);

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        if (container is not MaterialCarouselItem card)
            return;

        card.Invoked -= OnCardInvoked;
        card.Invoked += OnCardInvoked;
        ApplyDefaultAspectRatio(card, index);
        card.PropertyChanged -= OnCardPropertyChanged;
        card.PropertyChanged += OnCardPropertyChanged;
    }

    protected override void ClearContainerForItemOverride(Control container)
    {
        if (container is MaterialCarouselItem card)
        {
            card.Invoked -= OnCardInvoked;
            card.PropertyChanged -= OnCardPropertyChanged;
            if (_inheritedAspectCards.Remove(card))
                card.ClearValue(MaterialCarouselItem.AspectRatioProperty);
        }

        base.ClearContainerForItemOverride(container);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_panel is not null)
        {
            _panel.CurrentItemChanged -= OnPanelCurrentItemChanged;
            _panel = null;
        }

        base.OnApplyTemplate(e);
        SetScrollViewer(e.NameScope.Find<ScrollViewer>("PART_ScrollViewer"));
        // The items panel is created by the ItemsPresenter during the first layout pass, so it is
        // normally not available yet; AttachPanel retries on LayoutUpdated until it exists.
        AttachPanel();
        UpdateVisualClasses();
        UpdateSnapType();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ResolveScrollPresenter();
        AttachPanel();
        UpdateVisualClasses();
        UpdateSnapType();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        LayoutUpdated -= OnLayoutUpdatedForPanel;
        _offsetAnimation?.Dispose();
        _offsetAnimation = null;
        base.OnDetachedFromVisualTree(e);
    }

    // The pinned Carousel only forwards userScrollEnabled to the Pager, whose scrollable input,
    // paging keys and scroll semantics are dropped while the content stays composed and clickable.
    // Avalonia keeps those inputs in the native ScrollViewer, so the guard works on the template
    // scroll host instead of disabling the strip.

    private void SetScrollViewer(ScrollViewer? viewer)
    {
        if (ReferenceEquals(_scrollViewer, viewer))
        {
            ResolveScrollPresenter();
            return;
        }

        if (_scrollViewer is not null)
        {
            _scrollViewer.RemoveHandler(InputElement.PointerWheelChangedEvent,
                (EventHandler<PointerWheelEventArgs>)OnScrollHostPointerWheelTunnel);
            _scrollViewer.RemoveHandler(InputElement.PointerWheelChangedEvent,
                (EventHandler<PointerWheelEventArgs>)OnScrollHostPointerWheelBubble);
            _scrollViewer.RemoveHandler(InputElement.PointerPressedEvent,
                (EventHandler<PointerPressedEventArgs>)OnScrollHostPointerPressed);
            _scrollViewer.RemoveHandler(InputElement.PointerReleasedEvent,
                (EventHandler<PointerReleasedEventArgs>)OnScrollHostPointerReleased);
            _scrollViewer.TemplateApplied -= OnScrollHostTemplateApplied;
        }

        _scrollViewer = viewer;
        _scrollPresenter = null;
        _detachedScrollGestures.Clear();
        _hostScrollBars.Clear();
        _activeScrollPointer = null;
        _gestureRestorePending = false;
        _scrollOffsetBeforeWheel = null;
        _userScrollGuardApplied = false;

        if (_scrollViewer is not null)
        {
            // The native wheel handling is a class handler on ScrollContentPresenter, so only the
            // tunnel phase reaches it first; the bubble phase runs after it and can undo the scroll.
            _scrollViewer.AddHandler(InputElement.PointerWheelChangedEvent,
                (EventHandler<PointerWheelEventArgs>)OnScrollHostPointerWheelTunnel, RoutingStrategies.Tunnel);
            _scrollViewer.AddHandler(InputElement.PointerWheelChangedEvent,
                (EventHandler<PointerWheelEventArgs>)OnScrollHostPointerWheelBubble, RoutingStrategies.Bubble,
                handledEventsToo: true);
            _scrollViewer.AddHandler(InputElement.PointerPressedEvent,
                (EventHandler<PointerPressedEventArgs>)OnScrollHostPointerPressed, RoutingStrategies.Tunnel);
            _scrollViewer.AddHandler(InputElement.PointerReleasedEvent,
                (EventHandler<PointerReleasedEventArgs>)OnScrollHostPointerReleased, RoutingStrategies.Tunnel,
                handledEventsToo: true);
            _scrollViewer.TemplateApplied += OnScrollHostTemplateApplied;
            ResolveScrollPresenter();
        }
    }

    private void OnScrollHostTemplateApplied(object? sender, TemplateAppliedEventArgs e) => ResolveScrollPresenter();

    // The pan/inertia recognizer is declared by the ScrollViewer theme on its ScrollContentPresenter
    // and has no ScrollViewer-level switch; dropping it is the native equivalent of the pinned
    // scrollable(enabled = false).
    private void ResolveScrollPresenter()
    {
        ScrollContentPresenter? presenter = null;
        if (_scrollViewer is not null)
        {
            foreach (var visual in _scrollViewer.GetVisualDescendants())
            {
                if (visual is ScrollContentPresenter candidate)
                {
                    presenter = candidate;
                    break;
                }
            }
        }

        if (!ReferenceEquals(presenter, _scrollPresenter))
        {
            // A re-templated host brings its own recognizers and bars; the references from the old
            // template cannot be restored.
            _scrollPresenter = presenter;
            _detachedScrollGestures.Clear();
            _hostScrollBars.Clear();
            _gestureRestorePending = false;
            _userScrollGuardApplied = false;
            ResolveHostScrollBars();
        }

        UpdateUserScrollGuard();
    }

    private void ResolveHostScrollBars()
    {
        if (_scrollViewer is null)
            return;

        foreach (var visual in _scrollViewer.GetVisualDescendants())
        {
            if (visual is ScrollBar bar && ReferenceEquals(bar.TemplatedParent, _scrollViewer))
                _hostScrollBars.Add(bar);
        }
    }

    private void OnUserScrollEnabledChanged()
    {
        UpdateUserScrollGuard();
        _automationPeer?.InvalidateScrollStructure();
    }

    private void UpdateUserScrollGuard()
    {
        var presenter = _scrollPresenter;
        if (presenter is null)
            return;

        if (!UserScrollEnabled)
        {
            if (_userScrollGuardApplied)
                return;

            _userScrollGuardApplied = true;
            _gestureRestorePending = false;
            _detachedScrollGestures.Clear();
            foreach (var recognizer in presenter.GestureRecognizers.ToArray())
            {
                if (recognizer is ScrollGestureRecognizer && presenter.GestureRecognizers.Remove(recognizer))
                    _detachedScrollGestures.Add(recognizer);
            }

            SetHostScrollBarsHitTest(enabled: false);
            return;
        }

        if (!_userScrollGuardApplied)
            return;

        // scrollable(enabled = false) cancels the gesture in place. A contact that is still down
        // must not resume from the recognizer's tracked point, so the restore waits for its release
        // (or for the next press, which re-initializes the recognizer).
        if (_activeScrollPointer is not null)
        {
            _gestureRestorePending = true;
            return;
        }

        RestoreUserScroll(presenter);
    }

    private void RestoreUserScroll(ScrollContentPresenter presenter)
    {
        _userScrollGuardApplied = false;
        _gestureRestorePending = false;
        foreach (var recognizer in _detachedScrollGestures)
        {
            if (!presenter.GestureRecognizers.Contains(recognizer))
                presenter.GestureRecognizers.Add(recognizer);
        }

        _detachedScrollGestures.Clear();
        SetHostScrollBarsHitTest(enabled: true);
    }

    private void SetHostScrollBarsHitTest(bool enabled)
    {
        foreach (var bar in _hostScrollBars)
        {
            if (enabled)
                bar.ClearValue(InputElement.IsHitTestVisibleProperty);
            else
                bar.SetCurrentValue(InputElement.IsHitTestVisibleProperty, false);
        }
    }

    private void OnScrollHostPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_gestureRestorePending && _scrollPresenter is { } presenter)
            RestoreUserScroll(presenter);

        if (_activeScrollPointer is null && _scrollViewer is not null &&
            e.Pointer.Type is PointerType.Touch or PointerType.Pen &&
            e.GetCurrentPoint(_scrollViewer).Properties.IsLeftButtonPressed)
        {
            _activeScrollPointer = e.Pointer;
        }
    }

    private void OnScrollHostPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!ReferenceEquals(e.Pointer, _activeScrollPointer))
            return;

        _activeScrollPointer = null;
        if (_gestureRestorePending && _scrollPresenter is { } presenter)
            RestoreUserScroll(presenter);
    }

    private void OnScrollHostPointerWheelTunnel(object? sender, PointerWheelEventArgs e)
    {
        // Remember where the wheel started so a scroll that the source would not perform can be
        // undone. A scrollable or other wheel consumer inside a card handles the event first and is
        // left untouched.
        _scrollOffsetBeforeWheel = UserScrollEnabled || e.Handled ? null : _scrollViewer?.Offset.X;
    }

    private void OnScrollHostPointerWheelBubble(object? sender, PointerWheelEventArgs e)
    {
        var before = _scrollOffsetBeforeWheel;
        _scrollOffsetBeforeWheel = null;
        if (before is null || UserScrollEnabled || _scrollViewer is null)
            return;

        var offset = _scrollViewer.Offset;
        if (Math.Abs(offset.X - before.Value) < 0.5)
            return;

        _scrollViewer.SetCurrentValue(ScrollViewer.OffsetProperty, new Vector(before.Value, offset.Y));
        // The native presenter consumed an event it no longer acted on; let a scrollable around the
        // carousel keep the wheel exactly as the pinned disabled pager does.
        e.Handled = false;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => _automationPeer = new CarouselAutomationPeer(this);

    // The pinned LazyLayoutSemantics/PagerSemantics publish scroll actions only while
    // userScrollEnabled is true. ItemsControlAutomationPeer's own IScrollProvider is inert (it never
    // resolves its inner scroller), so the carousel forwards the native scroll viewer peer instead
    // and withdraws the whole scroll host - ScrollViewer and its bars - from the automation tree
    // while user scrolling is off. The cards themselves stay in the tree.
    private sealed class CarouselAutomationPeer(MaterialCarousel owner)
        : ListBoxAutomationPeer(owner), IScrollProvider
    {
        internal void InvalidateScrollStructure() => InvalidateChildren();

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore()
        {
            if (Carousel.UserScrollEnabled)
                return base.GetChildrenCore();

            var peers = new List<AutomationPeer>();
            foreach (var container in Carousel.GetRealizedContainers())
            {
                if (container.IsVisible)
                    peers.Add(GetOrCreate(container));
            }

            return peers;
        }

        private MaterialCarousel Carousel => (MaterialCarousel)Owner;

        private IScrollProvider? NativeScroller
        {
            get
            {
                if (!Carousel.UserScrollEnabled || Carousel._scrollViewer is not { } viewer)
                    return null;

                return ControlAutomationPeer.CreatePeerForElement(viewer) as IScrollProvider;
            }
        }

        bool IScrollProvider.HorizontallyScrollable => NativeScroller?.HorizontallyScrollable ?? false;

        double IScrollProvider.HorizontalScrollPercent =>
            NativeScroller?.HorizontalScrollPercent ?? ScrollPatternIdentifiers.NoScroll;

        double IScrollProvider.HorizontalViewSize => NativeScroller?.HorizontalViewSize ?? 0;

        bool IScrollProvider.VerticallyScrollable => NativeScroller?.VerticallyScrollable ?? false;

        double IScrollProvider.VerticalScrollPercent =>
            NativeScroller?.VerticalScrollPercent ?? ScrollPatternIdentifiers.NoScroll;

        double IScrollProvider.VerticalViewSize => NativeScroller?.VerticalViewSize ?? 0;

        void IScrollProvider.Scroll(ScrollAmount horizontalAmount, ScrollAmount verticalAmount) =>
            NativeScroller?.Scroll(horizontalAmount, verticalAmount);

        void IScrollProvider.SetScrollPercent(double horizontalPercent, double verticalPercent) =>
            NativeScroller?.SetScrollPercent(horizontalPercent, verticalPercent);
    }

    // The ItemsPresenter instantiates ItemsPanel after the carousel template is applied, so the
    // strip has to be picked up from a layout pass instead of from OnApplyTemplate.
    private void OnLayoutUpdatedForPanel(object? sender, EventArgs e) => AttachPanel();

    private void AttachPanel()
    {
        var panel = FindPanel();
        if (!ReferenceEquals(panel, _panel))
        {
            if (_panel is not null)
                _panel.CurrentItemChanged -= OnPanelCurrentItemChanged;

            _panel = panel;
            if (_panel is not null)
            {
                _panel.CurrentItemChanged += OnPanelCurrentItemChanged;
                _panel.RefreshOwner();
                // The first arrange may precede discovery by LayoutUpdated.
                OnPanelCurrentItemChanged(_panel, EventArgs.Empty);
                if (_pendingItem >= 0)
                {
                    // The offset is resolved by the next measure, once the strategy exists.
                    _panel.RequestAnchor(_pendingItem, 0);
                    _panel.InvalidateMeasure();
                    _pendingItem = -1;
                }
            }
        }

        LayoutUpdated -= OnLayoutUpdatedForPanel;
        if (_panel is null)
            LayoutUpdated += OnLayoutUpdatedForPanel;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty)
            InvalidateStripMeasure();
        else if (change.Property == PaddingProperty)
            InvalidateStripMeasure();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Handled || e.KeyModifiers != KeyModifiers.None)
        {
            base.OnKeyDown(e);
            return;
        }

        var card = FindCard(e.Source);
        if (card is null && !ReferenceEquals(e.Source, this))
        {
            base.OnKeyDown(e);
            return;
        }

        if (!UserScrollEnabled && e.Key is Key.PageUp or Key.PageDown)
        {
            // The pinned ScrollableNode.onKeyEvent only pages while scrolling is enabled.
            e.Handled = true;
            return;
        }

        switch (e.Key)
        {
            case Key.Home:
                SelectAndFocus(FindEnabled(0, 1));
                e.Handled = true;
                return;
            case Key.End:
                SelectAndFocus(FindEnabled(ItemCount - 1, -1));
                e.Handled = true;
                return;
            case Key.Enter:
            case Key.Space:
                if (card is { IsEffectivelyEnabled: true })
                {
                    var index = IndexFromContainer(card);
                    SelectAndFocus(index);
                    card.Invoke();
                    e.Handled = true;
                    return;
                }

                break;
            case Key.Left:
            case Key.Right:
            {
                var direction = e.Key == Key.Right ? 1 : -1;
                if (FlowDirection == Avalonia.Media.FlowDirection.RightToLeft)
                    direction = -direction;
                var start = SelectedIndex >= 0 ? SelectedIndex : CurrentItem;
                SelectAndFocus(FindEnabled(start + direction, direction));
                e.Handled = true;
                return;
            }

            case Key.PageDown:
            case Key.PageUp:
            {
                var direction = e.Key == Key.PageDown ? 1 : -1;
                MoveByPage(direction);
                e.Handled = true;
                return;
            }
        }

        base.OnKeyDown(e);
    }

    private void MoveByPage(int direction)
    {
        if (ItemCount == 0)
            return;

        var first = _panel?.FirstRealizedIndex ?? 0;
        var last = _panel?.LastRealizedIndex ?? ItemCount - 1;
        var page = Math.Max(1, last - first + 1);

        var start = SelectedIndex >= 0 ? SelectedIndex : CurrentItem;
        if (start < 0)
            start = direction > 0 ? -1 : ItemCount;

        var index = start;
        for (var step = 0; step < page; step++)
        {
            var next = FindEnabled(index + direction, direction);
            if (next < 0)
                break;
            index = next;
        }

        if (index != start)
            SelectAndFocus(index);
    }

    private void SelectAndFocus(int index)
    {
        if (index < 0 || index >= ItemCount)
            return;

        SelectedIndex = index;
        _selectionTarget = index;
        if (ContainerFromIndex(index) is Control realized)
            realized.Focus(NavigationMethod.Directional);
        else
            Focus(NavigationMethod.Directional);
    }

    private int FindEnabled(int start, int delta)
    {
        var count = ItemCount;
        if (count == 0)
            return -1;

        var index = Math.Clamp(start, 0, count - 1);
        for (var visited = 0; visited < count; visited++)
        {
            if (IsCardEnabled(index))
                return index;
            index += delta;
            if (index < 0 || index >= count)
                return -1;
        }

        return -1;
    }

    private bool IsCardEnabled(int index) =>
        ContainerFromIndex(index) is not Control container ||
        (container.IsEffectivelyEnabled && container.IsVisible);

    private MaterialCarouselItem? FindCard(object? source)
    {
        for (var visual = source as Visual; visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is MaterialCarouselItem card && ItemsControlFromItemContainer(card) == this)
                return card;
            if (ReferenceEquals(visual, this))
                return null;
        }

        return null;
    }

    private void HandleSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncingSelection)
            return;

        var index = SelectedIndex;
        if (index < 0)
            return;

        _currentItemObject = index < ItemCount ? Items[index] : null;
        _selectionTarget = index;
        if (_panel is null)
        {
            // The strip is created by the first layout pass, so an initial SelectedIndex has no
            // offset to scroll to yet. Keep the intent and apply it once the panel exists.
            _pendingItem = index;
            return;
        }

        _pendingItem = -1;
        // Selection is the explicit navigation intent: animate to its snapped position.
        ScrollToItemCore(index, animate: true);
    }

    private void OnPanelCurrentItemChanged(object? sender, EventArgs e)
    {
        if (_panel is null || _panel.CurrentItem < 0)
            return;

        var index = _panel.CurrentItem;
        // The mutation anchor is the visible current item, not the recorded selection intent.
        // CarouselState.currentItem follows the pager, so refresh the identity even while
        // selection synchronization is suppressed for an in-flight explicit selection.
        _currentItemObject = index < ItemCount ? Items[index] : null;
        SetAndRaise(CurrentItemProperty, ref _currentItem, index);
        if (_syncingSelection)
            return;

        if (_selectionTarget >= 0)
        {
            if (index == _selectionTarget)
                _selectionTarget = -1;
            else
                return;
        }

        SyncSelection(index);
    }

    private void SyncSelection(int index)
    {
        if (index < 0 || index >= ItemCount || SelectedIndex == index)
            return;

        _syncingSelection = true;
        try
        {
            SelectedIndex = index;
        }
        finally
        {
            _syncingSelection = false;
        }
    }

    private void OnCardInvoked(object? sender, EventArgs e)
    {
        if (sender is not MaterialCarouselItem card)
            return;

        var index = IndexFromContainer(card);
        if (index < 0)
            return;

        if (SelectedIndex != index)
            SelectedIndex = index;
        ItemInvoked?.Invoke(this, new MaterialCarouselItemInvokedEventArgs(card, index));
    }

    private void ApplyOffset(ScrollViewer viewer, double physicalOffset) =>
        viewer.SetCurrentValue(ScrollViewer.OffsetProperty, new Vector(physicalOffset, viewer.Offset.Y));

    private void TryPreJump(int targetIndex, ScrollViewer viewer)
    {
        if (_panel is null)
            return;

        var first = _panel.FirstRealizedIndex;
        var last = _panel.LastRealizedIndex;
        if (first < 0 || last < 0)
            return;

        var forward = targetIndex > first;
        var outsideVisible = forward ? targetIndex > last : targetIndex < first;
        if (!outsideVisible || Math.Abs(targetIndex - first) < 3)
            return;

        var visiblePages = last - first + 1;
        var preJumpIndex = forward
            ? Math.Max(first, targetIndex - visiblePages)
            : Math.Min(first, targetIndex + visiblePages);
        preJumpIndex = Math.Clamp(preJumpIndex, 0, ItemCount - 1);
        ApplyOffset(viewer, _panel.ToPhysicalOffset(_panel.GetTargetOffset(preJumpIndex)));
    }

    private void OnLayoutChanged()
    {
        if (!_flingBehaviorExplicit)
        {
            _updatingFlingDefault = true;
            SetCurrentValue(FlingBehaviorProperty, DefaultFlingBehavior(Layout));
            _updatingFlingDefault = false;
        }

        UpdateVisualClasses();
        UpdateSnapType();
        InvalidateStripMeasure();
    }

    private void OnFlingBehaviorChanged()
    {
        if (!_updatingFlingDefault)
            _flingBehaviorExplicit = true;
        UpdateSnapType();
        UpdateVisualClasses();
    }

    private static MaterialCarouselFlingBehavior DefaultFlingBehavior(MaterialCarouselLayout layout) =>
        layout == MaterialCarouselLayout.Uncontained
            ? MaterialCarouselFlingBehavior.NoSnap
            : MaterialCarouselFlingBehavior.SingleAdvance;

    private void UpdateSnapType()
    {
        var type = !IsSnapToItems
            ? SnapPointsType.None
            : FlingBehavior switch
            {
                // Pinned atMost(1) snap distance.
                MaterialCarouselFlingBehavior.SingleAdvance => SnapPointsType.MandatorySingle,
                // Pinned velocity-based multi-browse snap distance: any item can be the target.
                MaterialCarouselFlingBehavior.Velocity => SnapPointsType.Mandatory,
                // Pinned noSnapFlingBehavior: decay only, no post-fling snap.
                _ => SnapPointsType.None,
            };

        SetCurrentValue(ScrollViewer.HorizontalSnapPointsTypeProperty, type);
        SetCurrentValue(ScrollViewer.HorizontalSnapPointsAlignmentProperty, SnapPointsAlignment.Near);
        PseudoClasses.Set(":snap-single", type == SnapPointsType.MandatorySingle);
        PseudoClasses.Set(":snap-velocity", type == SnapPointsType.Mandatory);
        PseudoClasses.Set(":no-snap", type == SnapPointsType.None);
    }

    private MaterialCarouselPanel? FindPanel()
    {
        foreach (var visual in this.GetVisualDescendants())
        {
            if (visual is MaterialCarouselPanel panel)
                return panel;
        }

        return null;
    }

    private void InvalidateStripMeasure() => _panel?.InvalidateMeasure();

    // The design-source multi-aspect layout orders 16:9, 4:3, 1:1, 3:4, 9:16. A card that does not
    // set its own ratio inherits that sequence, so the layout is correct without extra markup.
    private static readonly double[] SourceRatios = [16d / 9d, 4d / 3d, 1d, 3d / 4d, 9d / 16d];

    internal double ResolveAspectRatio(MaterialCarouselItem? card, int index) =>
        card is not null && card.IsSet(MaterialCarouselItem.AspectRatioProperty) && !_inheritedAspectCards.Contains(card)
            ? card.AspectRatio : SourceRatios[index % SourceRatios.Length];

    private bool _assigningAspectRatio;

    private void OnCardPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != MaterialCarouselItem.AspectRatioProperty || _assigningAspectRatio ||
            sender is not MaterialCarouselItem card)
            return;
        _inheritedAspectCards.Remove(card);
        _panel?.InvalidateAspectMetrics();
    }

    // Cards whose ratio this control assigned. A recycled container must take the ratio of its new
    // index instead of keeping the previous one, and the value has to be cleared when the layout
    // stops using the aspect sequence so the card falls back to the property default.
    private readonly HashSet<MaterialCarouselItem> _inheritedAspectCards = [];

    private void ApplyDefaultAspectRatio(MaterialCarouselItem card, int index)
    {
        if (Layout != MaterialCarouselLayout.MultiAspectRatio)
        {
            if (_inheritedAspectCards.Remove(card))
                card.ClearValue(MaterialCarouselItem.AspectRatioProperty);
            return;
        }

        if (card.IsSet(MaterialCarouselItem.AspectRatioProperty) && !_inheritedAspectCards.Contains(card))
            return;

        _assigningAspectRatio = true;
        try
        {
            var ratio = SourceRatios[index % SourceRatios.Length];
            if (card.AspectRatio != ratio)
                card.AspectRatio = ratio;
            _inheritedAspectCards.Add(card);
        }
        finally { _assigningAspectRatio = false; }
    }

    private void UpdateVisualClasses()
    {
        PseudoClasses.Set(":hero", Layout == MaterialCarouselLayout.Hero);
        PseudoClasses.Set(":center-aligned-hero", Layout == MaterialCarouselLayout.CenterAlignedHero);
        PseudoClasses.Set(":multi-browse", Layout == MaterialCarouselLayout.MultiBrowse);
        PseudoClasses.Set(":uncontained", Layout == MaterialCarouselLayout.Uncontained);
        PseudoClasses.Set(":multi-aspect-ratio", Layout == MaterialCarouselLayout.MultiAspectRatio);
        PseudoClasses.Set(":full-screen", Layout == MaterialCarouselLayout.FullScreen);

        foreach (var container in GetRealizedContainers())
        {
            if (container is not MaterialCarouselItem card)
                continue;
            var index = IndexFromContainer(card);
            if (index >= 0)
                ApplyDefaultAspectRatio(card, index);
        }
    }
}

/// <summary>The single-card, full-bleed carousel (design-source "Carousel - Full screen").</summary>
/// <remarks>
/// There is no pinned Compose Carousel full-screen composable; this is an extension expressed as a
/// one-keyline strategy (each card fills the content area). It shares the control theme.
/// </remarks>
public sealed class MaterialCarouselFullScreen : MaterialCarousel
{
    public MaterialCarouselFullScreen()
    {
        Layout = MaterialCarouselLayout.FullScreen;
    }

    /// <summary>Both carousels share the control theme; this class only changes the defaults.</summary>
    protected override Type StyleKeyOverride => typeof(MaterialCarousel);
}

/// <summary>Describes the card activated by tap, Enter, or Space.</summary>
public sealed class MaterialCarouselItemInvokedEventArgs : EventArgs
{
    public MaterialCarouselItemInvokedEventArgs(MaterialCarouselItem item, int index)
    {
        Item = item;
        Index = index;
    }

    public MaterialCarouselItem Item { get; }
    public int Index { get; }
}