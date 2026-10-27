using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
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
/// A Material bottom sheet with anchored hidden/partial/expanded states, drag handling and the
/// 32x4 drag handle from the Material source.
/// </summary>
/// <remarks>
/// <para>
/// Anchors follow BottomSheet.kt: <c>Hidden</c> at the surface height, <c>Expanded</c> at 0 and
/// <c>PartiallyExpanded</c> showing half of the content at most half of the host. A finite
/// <see cref="SheetPeekHeight"/> switches the partial anchor to the persistent
/// BottomSheetScaffold rule (the surface height minus the peek); <see cref="SkipHiddenState"/>
/// removes the hidden anchor and clamps gestures and close requests to the visible anchors. The
/// surface is bottom-aligned and translated by the anchor travel, so the desktop layout keeps the
/// source's "sheet is placed at the anchor offset" placement without a custom layout pass.
/// </para>
/// <para>
/// <see cref="IsModal"/> adds the scrim; the modal presentation drops the resting elevation
/// shadow, matching the source. Dragging any non-scrolling part of the surface starts after the
/// source touch slop (taps keep their native target), previews the target, applies the source's
/// 125 DIP boundary dampening near the hidden anchor, and settles with the default-spatial
/// spring. A surface overshoot above the expanded anchor scales the surface up and the content
/// back down, as in <c>verticalScaleUp</c>/<c>verticalScaleDown</c>.
/// </para>
/// <para>
/// Desktop adaptation: Compose's nested-scroll connection is implemented for both the mouse wheel
/// and touch. The ScrollGesture class handler consumes expansion at the source before the native
/// ScrollContentPresenter scrolls, and the sheet's bubble handler collapses it from the remainder
/// after the content reaches its start; mouse-wheel scrolling works the same way. The release
/// velocity of the inner scroll gesture is measured from its deltas and forwarded to the same
/// anchor settle as the source <c>onPreFling</c>/<c>onPostFling</c>, so a nested content fling
/// still picks the next anchor by velocity. Predictive back is platform specific and is not
/// simulated.
/// </para>
/// </remarks>
public sealed class MaterialBottomSheet : MaterialSheet
{
    /// <summary>Identifies the <see cref="ShowDragHandle"/> property.</summary>
    public static readonly StyledProperty<bool> ShowDragHandleProperty =
        AvaloniaProperty.Register<MaterialBottomSheet, bool>(nameof(ShowDragHandle), true);

    /// <summary>Identifies the <see cref="AreGesturesEnabled"/> property.</summary>
    public static readonly StyledProperty<bool> AreGesturesEnabledProperty =
        AvaloniaProperty.Register<MaterialBottomSheet, bool>(nameof(AreGesturesEnabled), true);

    /// <summary>Identifies the <see cref="PositionalThreshold"/> property.</summary>
    public static readonly StyledProperty<double> PositionalThresholdProperty =
        AvaloniaProperty.Register<MaterialBottomSheet, double>(nameof(PositionalThreshold), 56d,
            validate: static value => double.IsFinite(value) && value > 0);

    /// <summary>Identifies the <see cref="VelocityThreshold"/> property.</summary>
    public static readonly StyledProperty<double> VelocityThresholdProperty =
        AvaloniaProperty.Register<MaterialBottomSheet, double>(nameof(VelocityThreshold), 125d,
            validate: static value => double.IsFinite(value) && value > 0);

    /// <summary>Identifies the <see cref="SheetMaxWidth"/> property.</summary>
    public static readonly StyledProperty<double> SheetMaxWidthProperty =
        AvaloniaProperty.Register<MaterialBottomSheet, double>(nameof(SheetMaxWidth), 640d,
            validate: static value => double.IsFinite(value) && value > 0);

    /// <summary>Identifies the <see cref="SafeAreaPadding"/> property.</summary>
    public static readonly StyledProperty<Thickness> SafeAreaPaddingProperty =
        AvaloniaProperty.Register<MaterialBottomSheet, Thickness>(nameof(SafeAreaPadding));

    /// <summary>Identifies the <see cref="SheetPeekHeight"/> property.</summary>
    public static readonly StyledProperty<double> SheetPeekHeightProperty =
        AvaloniaProperty.Register<MaterialBottomSheet, double>(
            nameof(SheetPeekHeight), double.NaN,
            validate: static value =>
                double.IsNaN(value) || (double.IsFinite(value) && value >= 0));

    /// <summary>Identifies the <see cref="SkipHiddenState"/> property.</summary>
    public static readonly StyledProperty<bool> SkipHiddenStateProperty =
        AvaloniaProperty.Register<MaterialBottomSheet, bool>(nameof(SkipHiddenState));

    // SheetDefaults.kt: boundary dampening zone and AnchoredDraggable's minimum fling velocity.
    private const double BoundaryDampeningZone = 125d;
    // Android ViewConfiguration's maximumFlingVelocity in DIP/s; Avalonia has no equivalent, so
    // the desktop adapter keeps the source platform's clamp.
    private const double MaximumFlingVelocity = 8000d;
    // One mouse-wheel notch is treated as this much sheet travel. Compose receives pixel deltas.
    private const double WheelStep = 40d;
    private static readonly TimeSpan WheelSettleDelay = TimeSpan.FromMilliseconds(120);

    private Control? _dragHandle;
    private ScrollViewer? _contentScroll;
    private ScrollContentPresenter? _scrollPresenter;
    private ScaleTransform? _surfaceScale;
    private TranslateTransform? _surfaceTranslate;
    private ScaleTransform? _contentScale;
    private IDisposable? _wheelSettle;
    private double _sheetHeight;
    private double _hostHeight;
    private bool _metricsMeasured;
    private bool _dragging;
    private bool _gestureActive;
    private bool _pressCandidate;
    private bool _pointerActive;
    private bool _pressOnHandle;
    private Point _pressStart;
    private Point _dragStart;
    private double _dragStartPosition;
    private double _dragLastY;
    private ulong _dragLastTimestamp;
    private double _dragVelocity;
    private PointerType _activePointerType;
    private ScrollGestureEventArgs? _pendingGesture;
    private double _gestureOffset;
    // SheetDefaults.kt onPreFling/onPostFling receive the remaining fling velocity. Avalonia's
    // ScrollGestureEventArgs exposes only the content-offset delta (the recognizer's
    // ScrollGestureInertiaStartingEventArgs keeps its constructor internal), so the desktop
    // adapter measures the release velocity from consecutive deltas and their elapsed time, the
    // same last-segment rule the pointer drag path uses.
    private int _gestureId;
    private long _gestureTimestamp;
    private double _gestureVelocity;
    private const double MaximumVelocitySampleSeconds = 0.2;

    private const double TouchSlop = 8d;
    private const double MouseSlop = 4d;

    static MaterialBottomSheet()
    {
        // Avalonia's ScrollGesture is bubble-only. The class handler runs at the event source
        // before the native ScrollContentPresenter instance handler, so the sheet can consume the
        // pre-scroll expansion; the sheet's own bubble handler observes the post-scroll remainder
        // after the presenter has consumed native offset. This is the same adapter shape used by
        // MaterialPullToRefresh for a nested scroller.
        ScrollGestureEvent.AddClassHandler<InputElement>((source, e) =>
        {
            if (!ReferenceEquals(source, e.Source))
                return;
            for (Visual? visual = source; visual is not null; visual = visual.GetVisualParent())
            {
                if (visual is MaterialBottomSheet sheet)
                {
                    sheet.GesturePreScroll(source, e);
                    break;
                }
            }
        }, RoutingStrategies.Bubble, true);
    }

    /// <summary>Shows the drag handle from the source anatomy.</summary>
    public bool ShowDragHandle
    {
        get => GetValue(ShowDragHandleProperty);
        set => SetValue(ShowDragHandleProperty, value);
    }

    /// <summary>
    /// Whether drag and nested-scroll gestures can move the sheet. The handle's click cycle stays
    /// active, as in BottomSheet.kt where <c>clickable</c> is outside the gesture gate.
    /// </summary>
    public bool AreGesturesEnabled
    {
        get => GetValue(AreGesturesEnabledProperty);
        set => SetValue(AreGesturesEnabledProperty, value);
    }

    /// <summary>
    /// Distance, in DIP, the sheet must travel from an anchor before a slow release moves to the
    /// next anchor. The source value is 56 DIP.
    /// </summary>
    public double PositionalThreshold
    {
        get => GetValue(PositionalThresholdProperty);
        set => SetValue(PositionalThresholdProperty, value);
    }

    /// <summary>
    /// Release speed, in DIP/s, that selects the next anchor in the gesture direction without
    /// considering <see cref="PositionalThreshold"/>. The source value is 125 DIP/s.
    /// </summary>
    public double VelocityThreshold
    {
        get => GetValue(VelocityThresholdProperty);
        set => SetValue(VelocityThresholdProperty, value);
    }

    /// <summary>The maximum surface width. The source default is 640 DIP.</summary>
    public double SheetMaxWidth
    {
        get => GetValue(SheetMaxWidthProperty);
        set => SetValue(SheetMaxWidthProperty, value);
    }

    /// <summary>
    /// Desktop host insets (safe area) applied around the surface: left/right margins and a
    /// bottom margin, with the hidden travel extended by the bottom inset so the sheet still
    /// leaves the viewport. Compose receives these through <c>windowInsets</c>; Avalonia has no
    /// system inset provider, so the host application supplies the value.
    /// </summary>
    public Thickness SafeAreaPadding
    {
        get => GetValue(SafeAreaPaddingProperty);
        set => SetValue(SafeAreaPaddingProperty, value);
    }

    /// <summary>
    /// The visible height of the sheet at its partial anchor, in DIP.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A finite value selects the persistent bottom-sheet anchors from BottomSheetScaffold.kt:
    /// <c>Hidden</c> at the surface height, <c>PartiallyExpanded</c> at
    /// <c>surfaceHeight - SheetPeekHeight</c> and <c>Expanded</c> at zero. The value is clamped to
    /// the measured surface, so a peek equal to the surface height removes the partial anchor, as
    /// in the source's <c>peekHeightPx != sheetHeight</c> check.
    /// </para>
    /// <para>
    /// The default is <see cref="double.NaN"/>, which keeps the modal half-content anchor from
    /// <c>calculatePartiallyExpandedOffset</c> (half of the surface, at most half of the host).
    /// Set a finite value for a persistent sheet; <see cref="MaterialBottomSheetScaffold"/> does
    /// this and defaults to the source's 56 DIP <c>BottomSheetDefaults.SheetPeekHeight</c>.
    /// </para>
    /// </remarks>
    public double SheetPeekHeight
    {
        get => GetValue(SheetPeekHeightProperty);
        set => SetValue(SheetPeekHeightProperty, value);
    }

    /// <summary>
    /// Omits <see cref="MaterialSheetValue.Hidden"/> from the anchor set, like
    /// <c>skipHiddenState</c> in SheetDefaults.kt.
    /// </summary>
    /// <remarks>
    /// With no hidden anchor, Escape, scrim taps, <see cref="MaterialSheet.Dismiss"/> and
    /// <see cref="MaterialSheet.RequestValueAsync"/> for <c>Hidden</c> collapse the sheet to its
    /// partial (or expanded) anchor instead of closing it. Drag and wheel gestures are clamped to
    /// the remaining anchors, and an exit motion that is already running reverses when the anchor
    /// disappears. <see cref="MaterialBottomSheetScaffold"/> sets this by default, matching
    /// <c>rememberStandardBottomSheetState(skipHiddenState = true)</c>.
    /// </remarks>
    public bool SkipHiddenState
    {
        get => GetValue(SkipHiddenStateProperty);
        set => SetValue(SkipHiddenStateProperty, value);
    }

    private bool UsesPersistentAnchors => double.IsFinite(SheetPeekHeight);

    protected override bool AnimateInitialPresentation => !UsesPersistentAnchors;

    /// <summary>The caller peek, clamped to the measured surface as the source clamps its anchor.</summary>
    private double AnchoredPeekHeight()
    {
        var sheet = EffectiveSheetHeight();
        if (!UsesPersistentAnchors || sheet <= 0)
            return 0;
        return Math.Clamp(SheetPeekHeight, 0, sheet);
    }

    private bool HasPartialAnchor()
    {
        var sheet = EffectiveSheetHeight();
        if (SkipPartiallyExpanded || sheet <= 0)
            return false;

        // BottomSheetScaffold removes the partial anchor when peekHeightPx == sheetHeight (the
        // partial anchor would coincide with the expanded anchor).
        return !UsesPersistentAnchors || AnchoredPeekHeight() < sheet - 0.5;
    }

    protected override bool CanUseValue(MaterialSheetValue value) => value switch
    {
        // A zero-height sheet keeps Hidden as the only fallback anchor, as in the source's
        // "Require at least 1 anchor" branch. Otherwise skipHiddenState removes it outright.
        MaterialSheetValue.Hidden => !SkipHiddenState || EffectiveSheetHeight() <= 0,
        MaterialSheetValue.Expanded => EffectiveSheetHeight() > 0,
        MaterialSheetValue.PartiallyExpanded => HasPartialAnchor(),
        _ => false,
    };

    protected override double GetPosition(MaterialSheetValue value)
    {
        var sheet = EffectiveSheetHeight();
        if (sheet <= 0)
            return 0;

        return value switch
        {
            MaterialSheetValue.Hidden => sheet + Math.Max(0, SafeAreaPadding.Bottom),
            MaterialSheetValue.PartiallyExpanded => UsesPersistentAnchors
                ? sheet - AnchoredPeekHeight()
                : Math.Max(0, sheet - Math.Min(HostHeight() / 2d, sheet / 2d)),
            _ => 0,
        };
    }

    /// <summary>
    /// Maps a value onto the persistent anchor set after a resize, a peek change or a skip-flag
    /// change. The order follows BottomSheetScaffold.kt: a lost target prefers the next anchor
    /// below it, then the fallback anchor, instead of animating to an absent position.
    /// </summary>
    protected override MaterialSheetValue ReconcileTarget(MaterialSheetValue value)
    {
        var partial = CanUseValue(MaterialSheetValue.PartiallyExpanded);
        var expanded = CanUseValue(MaterialSheetValue.Expanded);
        var hidden = CanUseValue(MaterialSheetValue.Hidden);
        // A zero-height surface keeps Hidden only as the source's "at least one anchor" fallback.
        // It is not a real visible target: keep the current value so content that reappears
        // restores the same anchor instead of silently closing the sheet.
        var realHidden = hidden && EffectiveSheetHeight() > 0;

        return value switch
        {
            MaterialSheetValue.Hidden => hidden
                ? MaterialSheetValue.Hidden
                : partial
                    ? MaterialSheetValue.PartiallyExpanded
                    : expanded
                        ? MaterialSheetValue.Expanded
                        : value,
            MaterialSheetValue.PartiallyExpanded => partial
                ? MaterialSheetValue.PartiallyExpanded
                : expanded
                    ? MaterialSheetValue.Expanded
                    : realHidden
                        ? MaterialSheetValue.Hidden
                        : value,
            MaterialSheetValue.Expanded => expanded
                ? MaterialSheetValue.Expanded
                : partial
                    ? MaterialSheetValue.PartiallyExpanded
                    : realHidden
                        ? MaterialSheetValue.Hidden
                        : value,
            _ => value,
        };
    }

    protected override void ApplyPosition(double position)
    {
        if (_surfaceTranslate is null)
            return;

        _surfaceTranslate.Y = position;

        // verticalScaleUp: when a spring overshoots above the expanded anchor, grow the surface
        // from its top so no gap appears at the bottom edge; scale the content back down to keep
        // its aspect ratio (verticalScaleDown).
        var sheet = _sheetHeight > 0 ? _sheetHeight : EffectiveSheetHeight();
        var overflow = position < 0 ? -position : 0;
        var factor = sheet > 0 && overflow > 0 ? (sheet + overflow) / sheet : 1d;
        if (_surfaceScale is not null)
            _surfaceScale.ScaleY = factor;
        if (_contentScale is not null)
            _contentScale.ScaleY = factor > 0 ? 1 / factor : 1d;
    }

    protected override bool IsDragInProgress => _dragging || _gestureActive;

    protected override string? GetSheetAccessibleName() =>
        this.TryFindResource("MaterialBottomSheetText", out var value) && value is string text
            ? text
            : "Bottom sheet";

    protected override void OnSheetTemplateApplied(TemplateAppliedEventArgs e)
    {
        if (_dragHandle is not null)
            _dragHandle.KeyDown -= OnDragHandleKeyDown;

        RemoveHandler(PointerWheelChangedEvent, OnSheetPointerWheel);
        RemoveHandler(PointerPressedEvent, OnSheetPointerPressed);
        RemoveHandler(PointerMovedEvent, OnSheetPointerMoved);
        RemoveHandler(PointerReleasedEvent, OnSheetPointerReleased);
        RemoveHandler(PointerCaptureLostEvent, OnSheetPointerCaptureLost);
        RemoveHandler(ScrollGestureEvent, GesturePostScroll);
        RemoveHandler(ScrollGestureEndedEvent, GestureEnded);

        _dragHandle = e.NameScope.Find<Control>("PART_DragHandle");
        _contentScroll = e.NameScope.Find<ScrollViewer>("PART_ContentScroll");
        _scrollPresenter = null;

        if (SheetSurface is not null)
        {
            _surfaceScale = new ScaleTransform(1, 1);
            _surfaceTranslate = new TranslateTransform();
            SheetTransform = _surfaceTranslate;
            SheetSurface.RenderTransformOrigin = new RelativePoint(0.5, 0, RelativeUnit.Relative);
            SheetSurface.RenderTransform = new TransformGroup
            {
                Children = { _surfaceScale, _surfaceTranslate },
            };
        }

        if (_contentScroll is not null)
        {
            _contentScale = new ScaleTransform(1, 1);
            _contentScroll.RenderTransformOrigin = new RelativePoint(0.5, 0, RelativeUnit.Relative);
            _contentScroll.RenderTransform = _contentScale;
        }

        if (_dragHandle is not null)
        {
            _dragHandle.Cursor = new Cursor(StandardCursorType.SizeNorthSouth);
            _dragHandle.Focusable = true;
            KeyboardNavigation.SetIsTabStop(_dragHandle, true);
            AutomationProperties.SetName(_dragHandle, "Drag handle");
            AutomationProperties.SetHelpText(_dragHandle,
                "Drag to expand or collapse; press Enter to change state.");
            _dragHandle.KeyDown += OnDragHandleKeyDown;
        }

        AddHandler(PointerWheelChangedEvent, OnSheetPointerWheel, RoutingStrategies.Tunnel);
        // Drag candidates: a press is only observed until the pointer passes the source touch
        // slop, so taps and inner controls keep their native interactions.
        AddHandler(PointerPressedEvent, OnSheetPointerPressed, RoutingStrategies.Tunnel, true);
        AddHandler(PointerMovedEvent, OnSheetPointerMoved, RoutingStrategies.Tunnel, true);
        AddHandler(PointerReleasedEvent, OnSheetPointerReleased, RoutingStrategies.Tunnel, true);
        AddHandler(PointerCaptureLostEvent, OnSheetPointerCaptureLost, RoutingStrategies.Tunnel, true);
        AddHandler(ScrollGestureEvent, GesturePostScroll, RoutingStrategies.Bubble, true);
        AddHandler(ScrollGestureEndedEvent, GestureEnded, RoutingStrategies.Bubble, true);

        UpdateMetrics();
        UpdateContentMaxHeight();
        UpdateAccessibleName();
    }

    protected override void UpdateSurfaceMeasureCap()
    {
        if (SheetSurface is null || Bounds.Height <= 0)
            return;

        var insets = SafeAreaPadding;
        var cap = Math.Max(0,
            Bounds.Height - Math.Max(0, insets.Top) - Math.Max(0, insets.Bottom));
        if (cap > 0 && Math.Abs(SheetSurface.MaxHeight - cap) > 0.5)
            SheetSurface.MaxHeight = cap;
    }

    protected override void OnSheetLayoutPassed()
    {
        UpdateMetrics();

        if (SheetSurface is not null)
        {
            var insets = SafeAreaPadding;
            var margin = new Thickness(
                Math.Max(0, insets.Left), 0, Math.Max(0, insets.Right), Math.Max(0, insets.Bottom));
            if (SheetSurface.Margin != margin)
                SheetSurface.Margin = margin;
        }

        // BottomSheet.kt attaches the "Drag handle" tooltip only while the sheet has a partial
        // anchor; with SkipPartiallyExpanded the handle remains a plain affordance.
        if (_dragHandle is not null)
        {
            var tip = CanUseValue(MaterialSheetValue.PartiallyExpanded) &&
                      this.TryFindResource("MaterialSheetDragHandleText", out var text) && text is string label
                ? label
                : null;
            if (!Equals(ToolTip.GetTip(_dragHandle), tip))
                ToolTip.SetTip(_dragHandle, tip);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _wheelSettle?.Dispose();
        _wheelSettle = null;
        _dragging = false;
        _gestureActive = false;
        _pressCandidate = false;
        _pointerActive = false;
        _pendingGesture = null;
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsOpenProperty && !IsOpen)
        {
            _dragging = false;
            _gestureActive = false;
            _pressCandidate = false;
            _pointerActive = false;
            _pendingGesture = null;
            _wheelSettle?.Dispose();
            _wheelSettle = null;
        }
        else if (change.Property == ShowDragHandleProperty || change.Property == ShowActionsProperty)
        {
            UpdateContentMaxHeight();
        }
        else if (change.Property == CurrentValueProperty || change.Property == IsOpenProperty ||
                 change.Property == AreGesturesEnabledProperty)
        {
            // The embedded handle's peer reports the current anchor and disappears from the
            // ExpandCollapse pattern when the source gesture gate is off.
            (_dragHandle as SheetDragHandle)?.NotifyStateChanged();
            if (change.Property == AreGesturesEnabledProperty)
                UpdateContentMaxHeight();
        }
        else if (change.Property == SheetPeekHeightProperty ||
                 change.Property == SkipHiddenStateProperty)
        {
            // Removing the hidden anchor mid-exit must reverse the exit, not leave the sheet
            // animating into an anchor that no longer exists.
            if (change.Property == SkipHiddenStateProperty && SkipHiddenState && IsVisible &&
                (TargetValue == MaterialSheetValue.Hidden || !IsOpen))
                _ = RunTransitionAsync(AbortHideAsync());

            UpdateContentMaxHeight();
            InvalidateMeasure();
        }
    }

    private double EffectiveSheetHeight()
    {
        if (_sheetHeight > 0)
            return _sheetHeight;

        var measured = SheetSurface?.Bounds.Height ?? 0;
        if (measured > 0)
            return measured;

        // Before the first layout the surface height is unknown; HostHeight keeps the closed
        // position finite. After a layout pass a zero height is genuine and leaves Hidden as the
        // only valid anchor, as in SheetState (no expanded anchor for a zero-height sheet).
        return _metricsMeasured ? 0 : HostHeight();
    }

    private double HostHeight() =>
        _hostHeight > 0 ? _hostHeight : Math.Max(0, Bounds.Height);

    private void UpdateMetrics()
    {
        var host = Bounds.Height > 0 ? Bounds.Height : _hostHeight;
        if (host > 0)
            _hostHeight = host;

        var measured = SheetSurface?.Bounds.Height ?? 0;
        var surfaceMeasured = SheetSurface is not null && SheetSurface.IsMeasureValid;
        if (measured <= 0 && surfaceMeasured)
            measured = SheetSurface!.DesiredSize.Height;
        if (_hostHeight > 0)
            measured = Math.Min(measured, _hostHeight);
        // Before the first layout the surface has no real height; keep the host fallback. After
        // it has been measured, a zero height is genuine (empty content) and removes the expanded
        // anchor. IsMeasureValid distinguishes a re-applied template that has not laid out yet,
        // and the visibility gate keeps the last height while the closed sheet is not laid out.
        if (measured > 0 || (_metricsMeasured && surfaceMeasured && IsEffectivelyVisible))
            _sheetHeight = measured;
        if (_hostHeight > 0)
            _metricsMeasured = true;
    }

    private void UpdateContentMaxHeight()
    {
        if (_contentScroll is null || _hostHeight <= 0)
            return;

        var available = _hostHeight
                        - (_dragHandle?.IsVisible == true ? _dragHandle.Bounds.Height : 0)
                        - (ActionsRegion?.IsVisible == true ? ActionsRegion.Bounds.Height : 0);
        available = Math.Max(0, available);

        // Only write when the value changes; this runs from the layout pass.
        if (Math.Abs(_contentScroll.MaxHeight - available) > 0.5)
            _contentScroll.MaxHeight = available;
    }

    private void OnSheetPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _activePointerType = e.Pointer.Type;
        _pointerActive = true;
        _pressCandidate = false;

        if (!IsOpen || !AreGesturesEnabled || e.Source is not Visual source)
            return;

        // Only the sheet surface can start a drag; the scrim keeps its own tap-to-dismiss.
        if (SheetSurface is null ||
            (!ReferenceEquals(source, SheetSurface) && !SheetSurface.IsVisualAncestorOf(source)))
            return;

        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed && e.Pointer.Type != PointerType.Touch)
            return;

        // The scrolling content owns its own touch pan; its nested-scroll adapter moves the
        // sheet instead (see GesturePreScroll/GesturePostScroll).
        if (_contentScroll is not null &&
            (ReferenceEquals(source, _contentScroll) || _contentScroll.IsVisualAncestorOf(source)))
            return;

        // Preserve native interactions in the action row and any embedded controls. The drag
        // starts only after the source touch slop, so a tap still reaches its button.
        if (IsInteractiveSource(source))
            return;

        _pressCandidate = true;
        _pressOnHandle = _dragHandle is not null &&
                         (ReferenceEquals(source, _dragHandle) || _dragHandle.IsVisualAncestorOf(source));
        _pressStart = point.Position;
        _dragStartPosition = SheetPosition;
        _dragVelocity = 0;

        // Stop a wheel settle so the drag starts from the visible position.
        _wheelSettle?.Dispose();
        _wheelSettle = null;
    }

    private void OnSheetPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_pressCandidate)
            return;

        var point = e.GetPosition(this);
        if (!_dragging)
        {
            var slop = e.Pointer.Type == PointerType.Mouse ? MouseSlop : TouchSlop;
            var dx = point.X - _pressStart.X;
            var dy = point.Y - _pressStart.Y;
            if (Math.Abs(dx) > Math.Abs(dy))
            {
                _pressCandidate = false;
                return;
            }
            if (Math.Abs(dy) < slop)
                return;

            // Source's awaitTouchSlopOrCancellation consumes the slop, then drags from there.
            _dragging = true;
            _dragStart = point;
            _dragStartPosition = SheetPosition;
            _dragLastY = point.Y;
            _dragLastTimestamp = e.Timestamp;
            MoveDragTo(SheetPosition);
            e.Pointer.Capture(this);
            e.PreventGestureRecognition();
        }

        var y = point.Y;
        var (minPosition, maxPosition) = AnchorRange();
        var position = Math.Clamp(
            _dragStartPosition + (y - _dragStart.Y), minPosition, Math.Max(minPosition, maxPosition));
        MoveDragTo(position);
        SetDragPreview(ClosestValue(position));

        var elapsed = e.Timestamp - _dragLastTimestamp;
        if (elapsed > 0)
            _dragVelocity = (y - _dragLastY) / (elapsed / 1000d);
        _dragLastY = y;
        _dragLastTimestamp = e.Timestamp;
        e.Handled = true;
    }

    private void OnSheetPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _pointerActive = false;
        if (!_pressCandidate)
            return;

        _pressCandidate = false;
        if (!_dragging)
        {
            // A tap on the source handle cycles hidden -> partial -> expanded -> hidden.
            if (_pressOnHandle)
            {
                CycleFromHandle();
                e.Handled = true;
            }
            return;
        }

        var velocity = _dragVelocity;
        _dragging = false;
        e.Pointer.Capture(null);
        e.Handled = true;
        SettleGesture(velocity);
    }

    private void OnSheetPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _pointerActive = false;
        if (!_dragging)
        {
            _pressCandidate = false;
            return;
        }

        _dragging = false;
        SettleGesture(0);
    }

    private static bool IsInteractiveSource(Visual source)
    {
        for (Visual? visual = source; visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is Button or TextBox or ComboBox or Slider or ScrollBar or ToggleButton or
                SelectingItemsControl or ListBoxItem)
                return true;
            if (visual is Border { Name: "PART_Surface" })
                break;
        }

        return false;
    }

    private bool IsWithinContent(InputElement? source) =>
        source is Visual visual && _contentScroll is not null &&
        (ReferenceEquals(visual, _contentScroll) || _contentScroll.IsVisualAncestorOf(visual));

    private double ScrollUnitPixels()
    {
        // ScrollViewer offsets are in scroll units; gestures and sheet travel are in DIPs.
        _scrollPresenter ??= _contentScroll?.GetVisualDescendants()
            .OfType<ScrollContentPresenter>()
            .FirstOrDefault();
        if (_scrollPresenter is { } presenter && presenter.Viewport.Height > 0)
            return presenter.Bounds.Height / presenter.Viewport.Height;
        return 1;
    }

    /// <summary>
    /// Pre-scroll half of the nested-scroll adapter. Called from the ScrollGesture class handler
    /// at the source, before the native ScrollContentPresenter instance handler.
    /// </summary>
    private void GesturePreScroll(InputElement source, ScrollGestureEventArgs e)
    {
        _pendingGesture = null;
        if (!IsWithinContent(source))
            return;

        // Track the gesture even while the sheet itself consumes nothing: the velocity at the
        // nested-scroll end is what the source's onPreFling/onPostFling forward to the sheet.
        TrackGestureVelocity(e);

        if (!AreGesturesEnabled || !IsOpen || _dragging)
            return;
        if (!_pointerActive || _activePointerType == PointerType.Mouse)
            return;
        if (_contentScroll is null)
            return;

        _pendingGesture = e;
        _gestureOffset = _contentScroll.Offset.Y;

        // Avalonia's gesture Delta.Y is the content offset delta: positive scrolls the content
        // towards later items. Compose's pre-scroll expands the sheet before the content moves,
        // so consume a positive delta for the sheet first.
        if (e.Delta.Y > 0 && SheetPosition > 0.5)
        {
            var consumed = Math.Min(e.Delta.Y, SheetPosition);
            var residual = e.Delta.Y - consumed;
            _gestureActive = true;
            var position = SheetPosition - consumed;
            MoveDragTo(position);
            SetDragPreview(ClosestValue(position));
            if (residual > 0)
            {
                var units = ScrollUnitPixels();
                _contentScroll.SetCurrentValue(ScrollViewer.OffsetProperty,
                    new Vector(_contentScroll.Offset.X,
                        _contentScroll.Offset.Y + (residual / units)));
            }
            e.Handled = true;
        }
    }

    /// <summary>
    /// Post-scroll half: the native presenter has consumed its scroll offset; any downward
    /// remainder at the content start collapses the sheet, matching BottomSheet's postScroll.
    /// </summary>
    private void GesturePostScroll(object? sender, ScrollGestureEventArgs e)
    {
        if (!ReferenceEquals(_pendingGesture, e))
            return;
        _pendingGesture = null;

        if (!AreGesturesEnabled || !IsOpen || _dragging || _contentScroll is null)
            return;
        if (e.Delta.Y >= 0)
            return;

        var units = ScrollUnitPixels();
        var nativeConsumed = Math.Max(0, _gestureOffset - _contentScroll.Offset.Y) * units;
        var remaining = Math.Max(0, -e.Delta.Y - nativeConsumed);
        if (remaining <= 0)
            return;

        var (minPosition, maxPosition) = AnchorRange();
        var position = Math.Clamp(
            SheetPosition + remaining, minPosition, Math.Max(minPosition, maxPosition));
        if (position <= SheetPosition + 0.5)
            return;

        _gestureActive = true;
        MoveDragTo(position);
        SetDragPreview(ClosestValue(position));
        e.Handled = true;
    }

    private void GestureEnded(object? sender, ScrollGestureEndedEventArgs e)
    {
        _pendingGesture = null;
        var contentVelocity = _gestureVelocity;
        _gestureVelocity = 0;
        _gestureTimestamp = 0;
        // A different gesture already started (for example after capture loss): its velocity
        // belongs to the newer event stream, not to this end.
        var stale = _gestureId != 0 && e.Id != _gestureId;
        _gestureId = 0;
        if (stale)
            return;

        // Source onPostFling: the remaining fling is handed to the anchored draggable, including
        // when the gesture never moved the sheet but the content had no room left.
        ReleaseNestedGesture(contentVelocity);
    }

    /// <summary>
    /// Records the content-offset velocity of the current scroll gesture. Consecutive events are
    /// the same samples Avalonia's own VelocityTracker uses; a gap longer than the recognizer's
    /// 200 ms fling window is treated as a release without velocity.
    /// </summary>
    private void TrackGestureVelocity(ScrollGestureEventArgs e)
    {
        var now = Stopwatch.GetTimestamp();
        if (e.Id != _gestureId)
        {
            // A new gesture starts fresh even if the previous one ended without its event
            // (capture loss, content rebuild). Inertia frames keep the same id on purpose.
            _gestureId = e.Id;
            _gestureTimestamp = now;
            _gestureVelocity = 0;
            _gestureActive = false;
            return;
        }

        var seconds = (now - _gestureTimestamp) / (double)Stopwatch.Frequency;
        _gestureTimestamp = now;
        if (seconds <= 0 || seconds > MaximumVelocitySampleSeconds)
        {
            _gestureVelocity = 0;
            return;
        }

        _gestureVelocity = e.Delta.Y / seconds;
    }

    /// <summary>
    /// SheetDefaults.kt nested connection: content offsets and the sheet's drag position run in
    /// opposite directions, so a content velocity that scrolls forward (towards later items)
    /// expands the sheet. This is the sign used by both the drag path and <see cref="SettleGesture"/>.
    /// </summary>
    internal static double ToSheetVelocity(double contentVelocity) => -contentVelocity;

    /// <summary>
    /// Applies the release velocity of a nested content scroll gesture through the normal drag
    /// settle, the desktop equivalent of <c>onPreFling</c>/<c>onPostFling</c>. Returns whether a
    /// settle was started.
    /// </summary>
    internal bool ReleaseNestedGesture(double contentVelocity)
    {
        if (!AreGesturesEnabled || !IsOpen || _contentScroll is null)
            return false;
        if (!_gestureActive && !IsContentAtBoundaryFor(contentVelocity))
            return false;

        _gestureActive = false;
        SettleGesture(ToSheetVelocity(contentVelocity));
        return true;
    }

    /// <summary>
    /// Whether the content is already at the edge the fling would scroll towards, so the remaining
    /// velocity belongs to the sheet rather than to the inner scroller.
    /// </summary>
    private bool IsContentAtBoundaryFor(double contentVelocity)
    {
        if (_contentScroll is null || contentVelocity == 0)
            return false;

        if (contentVelocity > 0)
        {
            // Forward fling expands the sheet: it engages at the content's end.
            var units = ScrollUnitPixels();
            var maxOffset = (_contentScroll.Extent.Height - _contentScroll.Viewport.Height) / units;
            return _contentScroll.Offset.Y >= maxOffset - 0.5;
        }

        // Backward fling collapses the sheet: it engages at the content's start.
        return _contentScroll.Offset.Y <= 0.5;
    }

    private void OnDragHandleKeyDown(object? sender, KeyEventArgs e)
    {
        if (!IsOpen)
            return;

        switch (e.Key)
        {
            case Key.Enter or Key.Space:
                CycleFromHandle();
                e.Handled = true;
                break;
            case Key.Up or Key.PageUp:
                _ = RunTransitionAsync(RequestValueAsync(MaterialSheetValue.Expanded));
                e.Handled = true;
                break;
            case Key.Down or Key.PageDown:
                if (CanUseValue(MaterialSheetValue.PartiallyExpanded))
                    _ = RunTransitionAsync(RequestValueAsync(MaterialSheetValue.PartiallyExpanded));
                else if (CanUseValue(MaterialSheetValue.Hidden))
                    _ = RunTransitionAsync(RequestValueAsync(MaterialSheetValue.Hidden));
                e.Handled = true;
                break;
        }
    }

    private void CycleFromHandle()
    {
        switch (CurrentValue)
        {
            case MaterialSheetValue.Expanded:
                // StandardBottomSheet: with the hidden anchor removed the handle collapses to
                // partial instead of closing.
                if (CanUseValue(MaterialSheetValue.Hidden))
                    Dismiss();
                else if (CanUseValue(MaterialSheetValue.PartiallyExpanded))
                    _ = RunTransitionAsync(RequestValueAsync(MaterialSheetValue.PartiallyExpanded));
                break;
            case MaterialSheetValue.PartiallyExpanded:
                _ = RunTransitionAsync(RequestValueAsync(MaterialSheetValue.Expanded));
                break;
            default:
                _ = RunTransitionAsync(OpenAsync());
                break;
        }
    }

    /// <summary>
    /// The current anchor as the embedded handle's expand/collapse automation peer reports it.
    /// </summary>
    internal ExpandCollapseState AutomationExpandCollapseState =>
        !IsOpen || CurrentValue == MaterialSheetValue.Hidden
            ? ExpandCollapseState.Collapsed
            : CurrentValue == MaterialSheetValue.PartiallyExpanded
                ? ExpandCollapseState.PartiallyExpanded
                : ExpandCollapseState.Expanded;

    /// <summary>
    /// The source only adds dismiss/expand/collapse semantics while gestures are enabled and the
    /// sheet is presented; the handle's click action stays outside that gate.
    /// </summary>
    internal bool IsAutomationExpandCollapseAvailable => AreGesturesEnabled && IsOpen;

    /// <summary>Runs the handle's tap cycle from Invoke, exactly like its keyboard activation.</summary>
    /// <remarks>
    /// The keyboard path ignores Enter/Space while the sheet is closed; this mirror returns
    /// <see langword="false"/> then, and the peer reports <see cref="ElementNotEnabledException"/>
    /// to automation clients. Once presented, the cycle is available even when
    /// <see cref="AreGesturesEnabled"/> is false, matching Compose's clickable being outside the
    /// semantics gesture gate.
    /// </remarks>
    internal bool InvokeFromHandle()
    {
        if (!IsOpen)
            return false;
        CycleFromHandle();
        return true;
    }

    /// <summary>
    /// Expands to the available expanded anchor. The transition goes through
    /// <see cref="MaterialSheet.RequestValueAsync"/> so <see cref="MaterialSheet.ValueChangeRequested"/>
    /// can veto it.
    /// </summary>
    internal bool ExpandFromHandle()
    {
        if (!AreGesturesEnabled || !IsOpen || !CanUseValue(MaterialSheetValue.Expanded))
            return false;
        if (CurrentValue != MaterialSheetValue.Expanded)
            _ = RunTransitionAsync(RequestValueAsync(MaterialSheetValue.Expanded));
        return true;
    }

    /// <summary>
    /// Collapses the source way: expanded moves to the partial anchor when one exists, otherwise
    /// it dismisses; partial dismisses. Missing anchors and dismissed-vetoes simply leave the
    /// current state in place.
    /// </summary>
    internal bool CollapseFromHandle()
    {
        if (!AreGesturesEnabled || !IsOpen)
            return false;

        switch (CurrentValue)
        {
            case MaterialSheetValue.Expanded:
                if (CanUseValue(MaterialSheetValue.PartiallyExpanded))
                {
                    _ = RunTransitionAsync(RequestValueAsync(MaterialSheetValue.PartiallyExpanded));
                    return true;
                }
                return DismissFromHandle();
            case MaterialSheetValue.PartiallyExpanded:
                return DismissFromHandle();
            default:
                return true;
        }
    }

    /// <summary>
    /// Dismisses through <see cref="MaterialSheet.Dismiss"/> so <see cref="MaterialSheet.IsDismissEnabled"/>,
    /// <see cref="MaterialSheet.Closing"/> and the hidden-anchor fallback keep their existing
    /// semantics.
    /// </summary>
    internal bool DismissFromHandle()
    {
        if (!IsOpen || !IsDismissEnabled)
            return false;
        Dismiss();
        return true;
    }

    private void SettleGesture(double velocity)
    {
        var sheet = EffectiveSheetHeight();
        if (sheet <= 0)
            return;

        if (velocity > 0)
        {
            // BottomSheet.kt dampens the fling near the hidden anchor so spring overshoot cannot
            // bounce the sheet visibly below the screen edge. When skipHiddenState removed that
            // anchor, the same dampening applies at the lowest available anchor.
            var boundary = CanUseValue(MaterialSheetValue.Hidden)
                ? GetPosition(MaterialSheetValue.Hidden)
                : AnchorRange().Max;
            var distanceToBoundary = Math.Max(0, boundary - SheetPosition);
            if (distanceToBoundary < BoundaryDampeningZone)
            {
                var initial = velocity;
                var factor = distanceToBoundary / BoundaryDampeningZone;
                velocity *= factor * factor;
                if (initial >= VelocityThreshold)
                    velocity = Math.Max(velocity, VelocityThreshold);
            }
        }

        velocity = Math.Clamp(velocity, -MaximumFlingVelocity, MaximumFlingVelocity);
        var target = ComputeGestureTarget(SheetPosition, velocity);
        if (!CanUseValue(target))
            target = LowestAvailableValue();

        // IsDismissEnabled disables implicit dismissal for drag and wheel gestures too.
        if (target == MaterialSheetValue.Hidden && !IsDismissEnabled)
            target = LowestVisibleValue();

        _ = RunTransitionAsync(SettleGestureAndRecoverAsync(target));
    }

    private MaterialSheetValue LowestVisibleValue() =>
        CanUseValue(MaterialSheetValue.PartiallyExpanded)
            ? MaterialSheetValue.PartiallyExpanded
            : CanUseValue(MaterialSheetValue.Expanded)
                ? MaterialSheetValue.Expanded
                : MaterialSheetValue.Hidden;

    private MaterialSheetValue LowestAvailableValue() =>
        CanUseValue(MaterialSheetValue.Hidden)
            ? MaterialSheetValue.Hidden
            : CanUseValue(MaterialSheetValue.PartiallyExpanded)
                ? MaterialSheetValue.PartiallyExpanded
                : MaterialSheetValue.Expanded;

    private async System.Threading.Tasks.Task SettleGestureAndRecoverAsync(MaterialSheetValue target)
    {
        if (!await SettleFromGestureAsync(target))
            SettleAtCurrentValue();
    }

    /// <summary>
    /// Desktop translation of the nested-scroll connection in SheetDefaults.kt. Wheel-down
    /// (negative Compose delta) expands the sheet before the content scrolls; wheel-up only moves
    /// the sheet once the content is already at its start.
    /// </summary>
    private void OnSheetPointerWheel(object? sender, PointerWheelEventArgs e)
    {
        if (!AreGesturesEnabled || !IsOpen || _dragging || _contentScroll is null)
            return;

        if (e.Source is not Visual source ||
            (!ReferenceEquals(source, _contentScroll) && !_contentScroll.IsVisualAncestorOf(source)))
            return;

        var delta = e.Delta.Y * WheelStep;
        var (minPosition, maxPosition) = AnchorRange();
        if (!double.IsFinite(minPosition) || maxPosition < minPosition)
            return;

        if (delta < 0)
        {
            // Pre-scroll: the sheet expands before the inner scroller consumes the gesture.
            if (SheetPosition > minPosition + 0.5)
            {
                MoveBy(delta);
                e.Handled = true;
            }
        }
        else if (delta > 0 && !CanContentScrollUp())
        {
            // Post-scroll: only after the content reaches its start does the sheet collapse.
            if (SheetPosition < maxPosition - 0.5)
            {
                MoveBy(delta);
                e.Handled = true;
            }
        }
    }

    private bool CanContentScrollUp() =>
        _contentScroll is not null && _contentScroll.Offset.Y > 0.5;

    private void MoveBy(double delta)
    {
        var (minPosition, maxPosition) = AnchorRange();
        var position = Math.Clamp(
            SheetPosition + delta, minPosition, Math.Max(minPosition, maxPosition));
        MoveDragTo(position);
        SetDragPreview(ClosestValue(position));
        ScheduleWheelSettle();
    }

    private void ScheduleWheelSettle()
    {
        _wheelSettle?.Dispose();
        _wheelSettle = DispatcherTimer.RunOnce(
            () =>
            {
                _wheelSettle = null;
                if (_dragging || !IsOpen)
                    return;
                SettleGesture(0);
            },
            WheelSettleDelay,
            DispatcherPriority.Background);
    }

    /// <summary>
    /// AnchoredDraggable's <c>computeTarget</c>: velocity above the threshold picks the next
    /// anchor in the gesture direction; otherwise the positional threshold decides.
    /// </summary>
    private MaterialSheetValue ComputeGestureTarget(double position, double velocity)
    {
        var anchors = AvailableAnchors();
        if (anchors.Count == 0)
            return MaterialSheetValue.Hidden;

        if (velocity == 0)
            return ClosestValue(position);

        var forward = velocity > 0;
        if (Math.Abs(velocity) >= VelocityThreshold)
            return forward ? NextAnchorBelow(anchors, position) : NextAnchorAbove(anchors, position);

        var above = NextAnchorAbove(anchors, position);
        var below = NextAnchorBelow(anchors, position);
        var abovePosition = PositionOf(anchors, above);
        var belowPosition = PositionOf(anchors, below);
        var threshold = PositionalThreshold;
        var start = forward ? abovePosition : belowPosition;
        var relative = Math.Abs(start - position);

        if (relative >= threshold)
            return forward ? below : above;
        return forward ? above : below;
    }

    /// <summary>The closest anchor at or below <paramref name="position"/> (larger travel).</summary>
    private static MaterialSheetValue NextAnchorBelow(
        List<(MaterialSheetValue Value, double Position)> anchors, double position)
    {
        var best = anchors[0].Value;
        var bestPosition = double.PositiveInfinity;
        foreach (var (value, anchor) in anchors)
        {
            if (anchor >= position && anchor < bestPosition)
            {
                best = value;
                bestPosition = anchor;
            }
        }

        // No anchor below the pointer: clamp to the lowest anchor (Expanded).
        if (double.IsPositiveInfinity(bestPosition))
        {
            var lowest = anchors[0];
            foreach (var anchor in anchors)
                if (anchor.Position < lowest.Position)
                    lowest = anchor;
            return lowest.Value;
        }
        return best;
    }

    /// <summary>The closest anchor at or above <paramref name="position"/> (smaller travel).</summary>
    private static MaterialSheetValue NextAnchorAbove(
        List<(MaterialSheetValue Value, double Position)> anchors, double position)
    {
        var best = anchors[0].Value;
        var bestPosition = double.NegativeInfinity;
        foreach (var (value, anchor) in anchors)
        {
            if (anchor <= position && anchor > bestPosition)
            {
                best = value;
                bestPosition = anchor;
            }
        }

        // No anchor above the pointer: clamp to the highest anchor (Hidden).
        if (double.IsNegativeInfinity(bestPosition))
        {
            var highest = anchors[0];
            foreach (var anchor in anchors)
                if (anchor.Position > highest.Position)
                    highest = anchor;
            return highest.Value;
        }
        return best;
    }

    private static double PositionOf(
        List<(MaterialSheetValue Value, double Position)> anchors, MaterialSheetValue value)
    {
        foreach (var (anchorValue, position) in anchors)
            if (anchorValue == value)
                return position;
        return 0;
    }

    private List<(MaterialSheetValue Value, double Position)> AvailableAnchors()
    {
        // Insertion order matches SheetState's hidden/partial/expanded anchor order, which decides
        // ties in closestAnchor (later anchor wins an equal distance). A removed anchor is absent,
        // so gestures cannot settle on it.
        var anchors = new List<(MaterialSheetValue Value, double Position)>(3);
        if (CanUseValue(MaterialSheetValue.Hidden))
            anchors.Add((MaterialSheetValue.Hidden, GetPosition(MaterialSheetValue.Hidden)));
        if (CanUseValue(MaterialSheetValue.PartiallyExpanded))
            anchors.Add((MaterialSheetValue.PartiallyExpanded, GetPosition(MaterialSheetValue.PartiallyExpanded)));
        if (CanUseValue(MaterialSheetValue.Expanded))
            anchors.Add((MaterialSheetValue.Expanded, GetPosition(MaterialSheetValue.Expanded)));
        return anchors;
    }

    /// <summary>The smallest (expanded) and largest (hidden) travel of the existing anchors.</summary>
    private (double Min, double Max) AnchorRange()
    {
        var min = double.PositiveInfinity;
        var max = double.NegativeInfinity;
        foreach (var (_, position) in AvailableAnchors())
        {
            if (position < min)
                min = position;
            if (position > max)
                max = position;
        }

        if (!double.IsFinite(min) || !double.IsFinite(max))
            return (0, Math.Max(0, EffectiveSheetHeight()));
        return (min, max);
    }

    private MaterialSheetValue ClosestValue(double position)
    {
        var best = MaterialSheetValue.Hidden;
        var bestDistance = double.PositiveInfinity;
        foreach (var (value, anchor) in AvailableAnchors())
        {
            var distance = Math.Abs(position - anchor);
            if (distance <= bestDistance)
            {
                best = value;
                bestDistance = distance;
            }
        }
        return best;
    }
}

/// <summary>
/// The drag-handle header of <see cref="MaterialBottomSheet"/>. It keeps the template's Border
/// anatomy but owns the native automation peer that exposes the source semantics actions.
/// </summary>
internal sealed class SheetDragHandle : Border
{
    private SheetDragHandlePeer? _peer;

    private MaterialBottomSheet? Sheet => TemplatedParent as MaterialBottomSheet;

    internal ExpandCollapseState ExpandCollapseState =>
        Sheet?.AutomationExpandCollapseState ?? ExpandCollapseState.Collapsed;

    internal bool IsAutomationStateAvailable =>
        Sheet?.IsAutomationExpandCollapseAvailable == true;

    internal bool Invoke() => Sheet?.InvokeFromHandle() == true;

    internal bool Expand() => Sheet?.ExpandFromHandle() == true;

    internal bool Collapse() => Sheet?.CollapseFromHandle() == true;

    internal void NotifyStateChanged() => _peer?.NotifyStateChanged();

    protected override AutomationPeer OnCreateAutomationPeer() =>
        _peer = new SheetDragHandlePeer(this);

    private sealed class SheetDragHandlePeer : ControlAutomationPeer, IInvokeProvider,
        IExpandCollapseProvider
    {
        private readonly SheetDragHandle _handle;
        private ExpandCollapseState _lastState;
        private bool _hasState;

        public SheetDragHandlePeer(SheetDragHandle owner) : base(owner)
        {
            _handle = owner;
            _lastState = owner.ExpandCollapseState;
            _hasState = true;
        }

        public ExpandCollapseState ExpandCollapseState => _handle.ExpandCollapseState;

        public bool ShowsMenu => false;

        protected override AutomationControlType GetAutomationControlTypeCore() =>
            AutomationControlType.Button;

        protected override string GetClassNameCore() => nameof(SheetDragHandle);

        protected override object? GetProviderCore(Type providerType)
        {
            // BottomSheet.kt wraps dismiss/expand/collapse semantics in `if (gesturesEnabled)`;
            // the clickable cycle itself deliberately stays available outside that gate.
            if (providerType == typeof(IExpandCollapseProvider) &&
                !_handle.IsAutomationStateAvailable)
                return null;

            return base.GetProviderCore(providerType);
        }

        // Invoke is unavailable while the sheet is closed (the keyboard path also ignores
        // activation then) or while the handle is disabled; both surface as
        // ElementNotEnabledException to automation clients.
        public void Invoke()
        {
            if (!_handle.IsEffectivelyEnabled || !_handle.Invoke())
                throw new ElementNotEnabledException();
        }

        public void Expand()
        {
            if (!_handle.IsEffectivelyEnabled || !_handle.Expand())
                throw new ElementNotEnabledException();
        }

        public void Collapse()
        {
            if (!_handle.IsEffectivelyEnabled || !_handle.Collapse())
                throw new ElementNotEnabledException();
        }

        internal void NotifyStateChanged()
        {
            var state = _handle.ExpandCollapseState;
            if (_hasState && state == _lastState)
                return;

            RaisePropertyChangedEvent(
                ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
                _hasState ? _lastState : state,
                state);
            _lastState = state;
            _hasState = true;
        }
    }
}