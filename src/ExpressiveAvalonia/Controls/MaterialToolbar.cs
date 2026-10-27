using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Theming;
using ExpressiveAvalonia.Tokens;

namespace ExpressiveAvalonia.Controls;

/// <summary>Whether the toolbar is docked to an edge or floats above the content.</summary>
public enum MaterialToolbarConfiguration
{
    /// <summary>Extension: square, container-filled bar that spans its slot and spreads actions.</summary>
    Docked,
    /// <summary>Floating toolbar from FloatingToolbar.kt: fully rounded, hugging surface.</summary>
    Floating
}

/// <summary>The floating toolbar color treatment.</summary>
public enum MaterialToolbarColor
{
    /// <summary>FloatingToolbarTokens.StandardContainerColor (SurfaceContainer).</summary>
    Standard,
    /// <summary>FloatingToolbarTokens.VibrantContainerColor (PrimaryContainer).</summary>
    Vibrant
}

/// <summary>FAB side for horizontal floating toolbars and edge for vertical ones.</summary>
public enum MaterialFloatingToolbarFabPosition
{
    Start,
    End,
    Top,
    Bottom
}

/// <summary>
/// Material floating toolbar with an optional adjacent FAB, plus the library's docked extension.
/// </summary>
/// <remarks>
/// <para>
/// Source mapping: <c>HorizontalFloatingToolbar</c> and <c>VerticalFloatingToolbar</c> including
/// their FAB overloads. The expanded surface is 64 units with 8-unit content padding and a fully
/// rounded shape; collapsed leading/trailing content is animated away. With a FAB, the FAB is 80
/// units when collapsed and 56 when expanded, separated by 8 units, while the toolbar content is
/// revealed from 0 to its intrinsic size. Action overflow is the adaptive AppBarRow behavior, and
/// the exit-always scroll behavior uses <see cref="MaterialAppBarScrollBehavior"/> with the
/// floating toolbar's exit direction. <see cref="ExpandOnScroll"/> ports
/// <c>floatingToolbarVerticalNestedScroll</c> with 40-unit expand/collapse thresholds.
/// </para>
/// <para>
/// Extensions beyond the pinned floating toolbar: <see cref="MaterialToolbarConfiguration.Docked"/>
/// (a square docked toolbar built from DockedToolbarTokens) and the generic action overflow list.
/// The toolbar's shadow blends the collapsed and expanded elevation tokens continuously from the
/// same FastSpatial expansion progress the source animates, instead of switching at a threshold.
/// A reversal retargets the running expansion spring, and an ExitAlways toolbar whose scroll
/// offset is non-zero leaves the tab order and automation content view until it returns to its
/// origin.
/// </para>
/// </remarks>
public class MaterialToolbar : MaterialBarBase
{
    public static readonly StyledProperty<MaterialToolbarConfiguration> ConfigurationProperty =
        AvaloniaProperty.Register<MaterialToolbar, MaterialToolbarConfiguration>(
            nameof(Configuration), MaterialToolbarConfiguration.Floating);

    /// <summary>Horizontal toolbars lay actions out in a row; vertical toolbars in a column.</summary>
    public static readonly StyledProperty<MaterialToolbarColor> ColorProperty =
        AvaloniaProperty.Register<MaterialToolbar, MaterialToolbarColor>(
            nameof(Color), MaterialToolbarColor.Standard);

    /// <summary>Expanded state; source <c>expanded</c> parameter. Docked toolbars stay expanded.</summary>
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<MaterialToolbar, bool>(nameof(IsExpanded), true);

    public static readonly DirectProperty<MaterialToolbar, double> ExpandedProgressProperty =
        AvaloniaProperty.RegisterDirect<MaterialToolbar, double>(
            nameof(ExpandedProgress), toolbar => toolbar.ExpandedProgress);

    public static readonly StyledProperty<object?> LeadingContentProperty =
        AvaloniaProperty.Register<MaterialToolbar, object?>(nameof(LeadingContent));

    public static readonly StyledProperty<object?> TrailingContentProperty =
        AvaloniaProperty.Register<MaterialToolbar, object?>(nameof(TrailingContent));

    /// <summary>Optional FAB content shown next to the floating toolbar.</summary>
    public static readonly StyledProperty<object?> FabProperty =
        AvaloniaProperty.Register<MaterialToolbar, object?>(nameof(Fab));

    public static readonly StyledProperty<MaterialFloatingToolbarFabPosition> FabPositionProperty =
        AvaloniaProperty.Register<MaterialToolbar, MaterialFloatingToolbarFabPosition>(
            nameof(FabPosition), MaterialFloatingToolbarFabPosition.End);

    /// <summary>Content padding; NaN components select the source 8-unit padding.</summary>
    public static readonly StyledProperty<Thickness> ContentPaddingProperty =
        AvaloniaProperty.Register<MaterialToolbar, Thickness>(nameof(ContentPadding), new Thickness(double.NaN));

    /// <summary>Hoisted floating scroll behavior (exit-always by default).</summary>
    public static readonly StyledProperty<MaterialAppBarScrollBehavior?> ScrollBehaviorProperty =
        AvaloniaProperty.Register<MaterialToolbar, MaterialAppBarScrollBehavior?>(nameof(ScrollBehavior));

    /// <summary>Native scroll viewer for exit behavior and threshold expansion.</summary>
    public static readonly StyledProperty<ScrollViewer?> ScrollSourceProperty =
        AvaloniaProperty.Register<MaterialToolbar, ScrollViewer?>(nameof(ScrollSource));

    /// <summary>Enables the floatingToolbarVerticalNestedScroll threshold behavior.</summary>
    public static readonly StyledProperty<bool> ExpandOnScrollProperty =
        AvaloniaProperty.Register<MaterialToolbar, bool>(nameof(ExpandOnScroll));

    /// <summary>Scroll distance used when collapsing; source default 40.</summary>
    public static readonly StyledProperty<double> CollapseScrollThresholdProperty =
        AvaloniaProperty.Register<MaterialToolbar, double>(nameof(CollapseScrollThreshold), 40d);

    /// <summary>Scroll distance used when expanding; source default 40.</summary>
    public static readonly StyledProperty<double> ExpandScrollThresholdProperty =
        AvaloniaProperty.Register<MaterialToolbar, double>(nameof(ExpandScrollThreshold), 40d);

    public static readonly DirectProperty<MaterialToolbar, bool> HasFabProperty =
        AvaloniaProperty.RegisterDirect<MaterialToolbar, bool>(
            nameof(HasFab), toolbar => toolbar.HasFab, (toolbar, value) => toolbar.HasFab = value);

    /// <summary>Resolved content padding; NaN components become 8.</summary>
    public static readonly DirectProperty<MaterialToolbar, Thickness> EffectiveContentPaddingProperty =
        AvaloniaProperty.RegisterDirect<MaterialToolbar, Thickness>(
            nameof(EffectiveContentPadding), toolbar => toolbar.EffectiveContentPadding);

    /// <summary>
    /// Shadow blended from the pinned collapsed (Level0) and expanded (Level1) elevation tokens by
    /// the floating toolbar's expansion progress; without a FAB both source endpoints are Level0.
    /// </summary>
    public static readonly DirectProperty<MaterialToolbar, BoxShadows> ToolbarShadowProperty =
        AvaloniaProperty.RegisterDirect<MaterialToolbar, BoxShadows>(
            nameof(ToolbarShadow), toolbar => toolbar.ToolbarShadow);

    /// <summary>Where the overflow button docks: trailing for horizontal, bottom for vertical.</summary>
    public static readonly DirectProperty<MaterialToolbar, Dock> OverflowDockProperty =
        AvaloniaProperty.RegisterDirect<MaterialToolbar, Dock>(
            nameof(OverflowDock), toolbar => toolbar.OverflowDock);

    private readonly TranslateTransform _exitTransform = new();
    private bool _hasFab;
    private Thickness _effectiveContentPadding = new(8);
    private BoxShadows _toolbarShadow;
    private BoxShadows? _collapsedShadow;
    private BoxShadows? _expandedShadow;
    private bool _shadowTokensLoaded;
    private Dock _overflowDock = Dock.Right;
    private double _expandedProgress = 1;
    private MaterialMotion.SpringAnimation? _expandAnimation;
    private Control? _layoutPart;
    private bool? _scrollHidden;
    private KeyboardNavigationMode _layoutTabNavigation;
    private AccessibilityView _layoutAccessibilityView;
    private bool _layoutHitTestVisible;
    private MaterialBarScrollAdapter? _exitAdapter;
    private ScrollViewer? _thresholdScroll;
    private MaterialAppBarState? _state;
    private double _thresholdOffset;
    private double _threshold;
    private double _lastThresholdOffset;
    private bool _dragging;
    private bool _draggingHorizontal;
    private double _dragLast;
    private double _dragVelocity;
    private ulong _dragLastTimestamp;

    static MaterialToolbar()
    {
        ConfigurationProperty.Changed.AddClassHandler<MaterialToolbar>((toolbar, _) => toolbar.UpdateConfiguration());
        OrientationProperty.Changed.AddClassHandler<MaterialToolbar>((toolbar, _) => toolbar.UpdateOrientation());
        ColorProperty.Changed.AddClassHandler<MaterialToolbar>((toolbar, _) => toolbar.UpdateShadow());
        IsExpandedProperty.Changed.AddClassHandler<MaterialToolbar>((toolbar, _) => toolbar.AnimateExpansion());
        FabProperty.Changed.AddClassHandler<MaterialToolbar>((toolbar, _) => toolbar.UpdateFab());
        ContentPaddingProperty.Changed.AddClassHandler<MaterialToolbar>((toolbar, _) => toolbar.UpdateConfiguration());
        ScrollBehaviorProperty.Changed.AddClassHandler<MaterialToolbar>((toolbar, args) =>
            toolbar.OnScrollBehaviorChanged(args.GetOldValue<MaterialAppBarScrollBehavior?>(),
                args.GetNewValue<MaterialAppBarScrollBehavior?>()));
        ScrollSourceProperty.Changed.AddClassHandler<MaterialToolbar>((toolbar, _) =>
        {
            toolbar.UpdateExitAdapter();
            toolbar.AttachThresholdScroll();
        });
        ExpandOnScrollProperty.Changed.AddClassHandler<MaterialToolbar>((toolbar, _) => toolbar.AttachThresholdScroll());
        ExpandScrollThresholdProperty.Changed.AddClassHandler<MaterialToolbar>((toolbar, _) => toolbar.ResetThreshold());
        CollapseScrollThresholdProperty.Changed.AddClassHandler<MaterialToolbar>((toolbar, _) => toolbar.ResetThreshold());
    }

    public MaterialToolbar()
    {
        RenderTransform = _exitTransform;
        // The elevation endpoints are theme resources: a theme variant switch or a replaced
        // resource dictionary has to re-resolve them, not only a property change.
        ActualThemeVariantChanged += (_, _) => RefreshShadowTokens();
        ResourcesChanged += (_, _) => RefreshShadowTokens();
        UpdateConfiguration();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        LayoutUpdated += OnLayoutUpdated;
        UpdateExitAdapter();
        SubscribeState();
        AttachThresholdScroll();
        // The constructor can run before the theme resources are reachable from this control.
        RefreshShadowTokens();
        UpdateExitTransform();
        UpdateScrollHiddenState();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _layoutPart = e.NameScope.Find<Control>("PART_Layout");
        // A re-applied template owns a fresh PART_Layout with its own native attached values.
        _scrollHidden = null;
        UpdateScrollHiddenState();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        LayoutUpdated -= OnLayoutUpdated;
        _exitAdapter?.Dispose();
        _exitAdapter = null;
        UnsubscribeState();
        DetachThresholdScroll();
        _expandAnimation?.Dispose();
        _expandAnimation = null;
    }

    public MaterialToolbarConfiguration Configuration
    {
        get => GetValue(ConfigurationProperty);
        set => SetValue(ConfigurationProperty, value);
    }

    public MaterialToolbarColor Color
    {
        get => GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    /// <summary>0 collapsed, 1 expanded; animated with the FastSpatial motion spec.</summary>
    public double ExpandedProgress
    {
        get => _expandedProgress;
        private set
        {
            if (SetAndRaise(ExpandedProgressProperty, ref _expandedProgress, value))
                UpdateShadow();
        }
    }

    public object? LeadingContent
    {
        get => GetValue(LeadingContentProperty);
        set => SetValue(LeadingContentProperty, value);
    }

    public object? TrailingContent
    {
        get => GetValue(TrailingContentProperty);
        set => SetValue(TrailingContentProperty, value);
    }

    public object? Fab
    {
        get => GetValue(FabProperty);
        set => SetValue(FabProperty, value);
    }

    public MaterialFloatingToolbarFabPosition FabPosition
    {
        get => GetValue(FabPositionProperty);
        set => SetValue(FabPositionProperty, value);
    }

    public Thickness ContentPadding
    {
        get => GetValue(ContentPaddingProperty);
        set => SetValue(ContentPaddingProperty, value);
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

    public bool ExpandOnScroll
    {
        get => GetValue(ExpandOnScrollProperty);
        set => SetValue(ExpandOnScrollProperty, value);
    }

    public double CollapseScrollThreshold
    {
        get => GetValue(CollapseScrollThresholdProperty);
        set => SetValue(CollapseScrollThresholdProperty, value);
    }

    public double ExpandScrollThreshold
    {
        get => GetValue(ExpandScrollThresholdProperty);
        set => SetValue(ExpandScrollThresholdProperty, value);
    }

    public bool HasFab
    {
        get => _hasFab;
        private set
        {
            if (SetAndRaise(HasFabProperty, ref _hasFab, value))
                UpdateShadow();
        }
    }

    public Thickness EffectiveContentPadding => _effectiveContentPadding;

    public BoxShadows ToolbarShadow
    {
        get => _toolbarShadow;
        private set => SetAndRaise(ToolbarShadowProperty, ref _toolbarShadow, value);
    }

    public Dock OverflowDock
    {
        get => _overflowDock;
        private set => SetAndRaise(OverflowDockProperty, ref _overflowDock, value);
    }

    protected override PlacementMode GetOverflowPlacement() =>
        IsSet(OverflowPlacementProperty)
            ? base.GetOverflowPlacement()
            : Orientation == Avalonia.Layout.Orientation.Vertical
                ? PlacementMode.RightEdgeAlignedTop
                : PlacementMode.BottomEdgeAlignedRight;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (Configuration != MaterialToolbarConfiguration.Floating)
            return;
        if (ScrollBehavior is not { Mode: MaterialBarScrollMode.ExitAlways } behavior)
            return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        if (IsInteractiveSource(e.Source))
            return;
        _draggingHorizontal = behavior.ExitDirection is MaterialFloatingToolbarExitDirection.Start
            or MaterialFloatingToolbarExitDirection.End;
        _dragging = true;
        _dragLast = _draggingHorizontal ? e.GetPosition(this).X : e.GetPosition(this).Y;
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
        var value = _draggingHorizontal ? e.GetPosition(this).X : e.GetPosition(this).Y;
        var delta = value - _dragLast;
        _dragLast = value;
        var dt = Math.Max(1, e.Timestamp - _dragLastTimestamp) / 1000d;
        _dragLastTimestamp = e.Timestamp;
        _dragVelocity = delta / dt;
        behavior.CancelSettle();
        // DraggableState inverts the horizontal delta in RTL, then applies delta to Start/Top and
        // -delta to End/Bottom.
        if (_draggingHorizontal && FlowDirection == FlowDirection.RightToLeft)
            delta = -delta;
        var sign = behavior.ExitDirection is MaterialFloatingToolbarExitDirection.Start
            or MaterialFloatingToolbarExitDirection.Top
            ? 1
            : -1;
        behavior.State.HeightOffset += delta * sign;
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_dragging)
            return;
        _dragging = false;
        e.Pointer.Capture(null);
        // FloatingToolbar.kt's draggable calls settleFloatingToolbar with the raw gesture
        // velocity; the exit direction only affects the drag delta and layout placement.
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

    private void UpdateConfiguration()
    {
        var floating = Configuration == MaterialToolbarConfiguration.Floating;
        if (!floating)
        {
            ExpandedProgress = 1;
            _expandAnimation?.Dispose();
            _expandAnimation = null;
        }

        var padding = ContentPadding;
        _effectiveContentPadding = new Thickness(
            double.IsNaN(padding.Left) ? 8 : padding.Left,
            double.IsNaN(padding.Top) ? 8 : padding.Top,
            double.IsNaN(padding.Right) ? 8 : padding.Right,
            double.IsNaN(padding.Bottom) ? 8 : padding.Bottom);
        RaisePropertyChanged(EffectiveContentPaddingProperty, default(Thickness), _effectiveContentPadding);

        UpdateFab();
        UpdateShadow();
    }

    private void UpdateOrientation()
    {
        OverflowDock = Orientation == Avalonia.Layout.Orientation.Vertical ? Dock.Bottom : Dock.Right;
        if (FabPosition is MaterialFloatingToolbarFabPosition.Start or MaterialFloatingToolbarFabPosition.End)
            FabPosition = Orientation == Avalonia.Layout.Orientation.Vertical
                ? MaterialFloatingToolbarFabPosition.Bottom
                : MaterialFloatingToolbarFabPosition.End;
        InvalidateMeasure();
    }

    private void UpdateFab()
    {
        HasFab = Configuration == MaterialToolbarConfiguration.Floating && Fab is not null;
        InvalidateMeasure();
    }

    private void AnimateExpansion()
    {
        if (Configuration != MaterialToolbarConfiguration.Floating)
            return;
        var target = IsExpanded ? 1 : 0;
        if (_expandAnimation is { } animation)
        {
            // FloatingToolbarDefaults.animationSpec drives an updateTransition, so a reversal
            // starts from the running animation's position and velocity. Disposing it here and
            // starting a new spring from rest would visibly stall expand -> collapse -> expand.
            animation.Retarget(target);
            return;
        }

        _expandAnimation = MaterialMotion.Animate(this, ExpandedProgress, target,
            value => ExpandedProgress = value, MaterialSpring.SpatialFast, 0.001);
    }

    /// <summary>
    /// The live expansion spring. Reversing <see cref="IsExpanded"/> retargets this instance
    /// instead of replacing it, so the source transition keeps its current velocity.
    /// </summary>
    internal MaterialMotion.SpringAnimation? ExpansionAnimation => _expandAnimation;

    private void RefreshShadowTokens()
    {
        _shadowTokensLoaded = false;
        UpdateShadow();
    }

    private void UpdateShadow()
    {
        if (!_shadowTokensLoaded)
        {
            _shadowTokensLoaded = true;
            _collapsedShadow = FindElevationShadow(0);
            _expandedShadow = FindElevationShadow(1);
        }

        var collapsed = _collapsedShadow ?? default;
        if (!HasFab)
        {
            // HorizontalFloatingToolbarLayout/VerticalFloatingToolbarLayout animate
            // ContainerCollapsedElevation -> ContainerExpandedElevation, and both defaults are
            // ElevationTokens.Level0, so the shadow stays on the collapsed token. Docked toolbars
            // are a library extension and also keep Level0.
            ToolbarShadow = collapsed;
            return;
        }

        // HorizontalFloatingToolbarWithFabLayout/VerticalFloatingToolbarWithFabLayout place the
        // toolbar with shadowElevation = lerp(ContainerCollapsedElevation (Level0),
        // ContainerExpandedElevation (Level1), expandedProgress), driven by the same
        // FloatingToolbarDefaults.animationSpec() (FastSpatial) transition that expands the
        // toolbar. Blending the two token shadows from that progress keeps the elevation
        // continuous, so a reversal retargets the spring and the shadow follows it back instead of
        // flipping at a threshold. The source clamps the animated fraction at the expanded end,
        // and Avalonia cannot express an elevation below the collapsed endpoint while a spring
        // undershoots, so the fraction is clamped to the token pair.
        ToolbarShadow = LerpShadow(collapsed, _expandedShadow ?? collapsed,
            Math.Clamp(ExpandedProgress, 0d, 1d));
    }

    private BoxShadows? FindElevationShadow(int level) =>
        this.TryFindResource($"MaterialElevationLevel{level}Shadow", ActualThemeVariant, out var value)
            && value is BoxShadows shadows
            ? shadows
            : null;

    /// <summary>
    /// Avalonia exposes discrete token shadows while Compose renders a continuous elevation.
    /// Blending the two neighbouring tokens mirrors <c>MaterialElevationAnimation</c>'s
    /// interpolation, which publishes through a button-only attached property.
    /// </summary>
    private static BoxShadows LerpShadow(BoxShadows from, BoxShadows to, double progress)
    {
        if (progress <= 0)
            return from;
        if (progress >= 1)
            return to;

        var count = Math.Max(from.Count, to.Count);
        if (count == 0)
            return default;

        var first = LerpShadowAt(from, to, 0, progress);
        if (count == 1)
            return new BoxShadows(first);

        // BoxShadows retains its tail array: each published value must own it.
        var rest = new BoxShadow[count - 1];
        for (var i = 1; i < count; i++)
            rest[i - 1] = LerpShadowAt(from, to, i, progress);
        return new BoxShadows(first, rest);
    }

    private static BoxShadow LerpShadowAt(BoxShadows from, BoxShadows to, int index, double progress)
    {
        var a = index < from.Count ? from[index] : default;
        var b = index < to.Count ? to[index] : default;
        return new BoxShadow
        {
            OffsetX = Lerp(a.OffsetX, b.OffsetX, progress),
            OffsetY = Lerp(a.OffsetY, b.OffsetY, progress),
            Blur = Lerp(a.Blur, b.Blur, progress),
            Spread = Lerp(a.Spread, b.Spread, progress),
            Color = Lerp(a.Color, b.Color, progress),
            IsInset = a.IsInset,
        };
    }

    private static double Lerp(double from, double to, double progress) => from + (to - from) * progress;

    private static Avalonia.Media.Color Lerp(Avalonia.Media.Color from, Avalonia.Media.Color to, double progress) => Avalonia.Media.Color.FromArgb(
        (byte)Math.Round(Lerp(from.A, to.A, progress)),
        (byte)Math.Round(Lerp(from.R, to.R, progress)),
        (byte)Math.Round(Lerp(from.G, to.G, progress)),
        (byte)Math.Round(Lerp(from.B, to.B, progress)));

    private void OnScrollBehaviorChanged(MaterialAppBarScrollBehavior? oldValue,
        MaterialAppBarScrollBehavior? newValue)
    {
        oldValue?.CancelSettle();
        UnsubscribeState();
        UpdateExitAdapter();
        SubscribeState();
        UpdateExitTransform();
        UpdateScrollHiddenState();
    }

    private void SubscribeState()
    {
        var state = ScrollBehavior?.State;
        if (ReferenceEquals(_state, state))
            return;
        _state = state;
        if (_state is not null)
            _state.PropertyChanged += OnStatePropertyChanged;
    }

    private void UnsubscribeState()
    {
        if (_state is not null)
            _state.PropertyChanged -= OnStatePropertyChanged;
        _state = null;
    }

    private void OnStatePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == MaterialAppBarState.HeightOffsetProperty)
        {
            UpdateExitTransform();
            UpdateScrollHiddenState();
        }
        else if (e.Property == MaterialAppBarState.HeightOffsetLimitProperty)
        {
            UpdateScrollHiddenState();
        }
    }

    /// <summary>
    /// FloatingToolbar.kt gates the whole toolbar on
    /// <c>derivedStateOf { scrollBehavior?.state?.offset == 0f }</c> and applies
    /// <c>focusProperties { canFocus = false }</c> when the scroll offset is non-zero, so a
    /// scrolled-away toolbar and its action subtree are not keyboard focusable. Compose changes
    /// composition there; the desktop host keeps the template alive and removes the subtree from
    /// tab navigation, hit testing and the automation content view instead, restoring the native
    /// template values as soon as the offset returns to zero. Focus outside the toolbar and
    /// explicit caller overlays are untouched.
    /// </summary>
    private void UpdateScrollHiddenState()
    {
        if (_layoutPart is not { } layout)
        {
            _scrollHidden = null;
            return;
        }

        var hidden = ScrollBehavior is { Mode: MaterialBarScrollMode.ExitAlways } behavior
                     && behavior.State.HeightOffset != 0;

        if (_scrollHidden == hidden)
            return;

        // The first evaluation runs while the toolbar is still visible: capture the template's
        // native values there, before any write, so neither the visible pass nor a later reveal
        // overwrites the caller's template and focus policy with defaults.
        if (_scrollHidden is null)
        {
            CaptureLayoutAccessState(layout);
            if (!hidden)
            {
                _scrollHidden = false;
                return;
            }
        }
        else if (hidden)
        {
            // Visible -> hidden: preserve any native value the template adjusted in between.
            CaptureLayoutAccessState(layout);
        }
        _scrollHidden = hidden;

        KeyboardNavigation.SetTabNavigation(layout,
            hidden ? KeyboardNavigationMode.None : _layoutTabNavigation);
        AutomationProperties.SetAccessibilityView(layout,
            hidden ? AccessibilityView.Raw : _layoutAccessibilityView);
        layout.IsHitTestVisible = !hidden && _layoutHitTestVisible;

        if (!hidden)
            return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.FocusManager?.GetFocusedElement() is Visual focused
            && (ReferenceEquals(focused, this) || this.IsVisualAncestorOf(focused)))
        {
            topLevel.FocusManager.Focus(null);
        }
    }

    /// <summary>
    /// Remembers the PART_Layout's own tab, automation and hit-test policy. Captured only while
    /// the part is in its visible state so a reveal never restores painted-over values.
    /// </summary>
    private void CaptureLayoutAccessState(Control layout)
    {
        _layoutTabNavigation = KeyboardNavigation.GetTabNavigation(layout);
        _layoutAccessibilityView = AutomationProperties.GetAccessibilityView(layout);
        _layoutHitTestVisible = layout.IsHitTestVisible;
    }

    private void UpdateExitAdapter()
    {
        _exitAdapter?.Dispose();
        _exitAdapter = null;
        if (ScrollBehavior is not { } behavior)
            return;
        _exitAdapter = new MaterialBarScrollAdapter(this, behavior);
        _exitAdapter.Attach(ScrollSource);
    }

    private void UpdateExitTransform()
    {
        var behavior = ScrollBehavior;
        if (behavior is null)
        {
            _exitTransform.X = 0;
            _exitTransform.Y = 0;
            return;
        }
        var offset = behavior.State.HeightOffset;
        var rtl = FlowDirection == FlowDirection.RightToLeft;
        switch (behavior.ExitDirection)
        {
            case MaterialFloatingToolbarExitDirection.Start:
                _exitTransform.X = rtl ? -offset : offset;
                _exitTransform.Y = 0;
                break;
            case MaterialFloatingToolbarExitDirection.End:
                _exitTransform.X = rtl ? offset : -offset;
                _exitTransform.Y = 0;
                break;
            case MaterialFloatingToolbarExitDirection.Top:
                _exitTransform.X = 0;
                _exitTransform.Y = offset;
                break;
            default:
                _exitTransform.X = 0;
                _exitTransform.Y = -offset;
                break;
        }
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        UpdateExitLimit();
        UpdateExitTransform();
        UpdateScrollHiddenState();
    }

    /// <summary>
    /// Recomputes the floating toolbar's exit limit relative to its parent, porting the
    /// <c>onGloballyPositioned</c> block of FloatingToolbar.kt.
    /// </summary>
    private void UpdateExitLimit()
    {
        if (ScrollBehavior is not { } behavior)
            return;
        if (Parent is not Control parent || !parent.IsAttachedToVisualTree())
            return;
        var parentSize = parent.Bounds.Size;
        var position = Bounds.Position;
        var rtl = FlowDirection == FlowDirection.RightToLeft;
        var limit = behavior.ExitDirection switch
        {
            MaterialFloatingToolbarExitDirection.Start =>
                rtl ? parentSize.Width - position.X : Bounds.Width + position.X,
            MaterialFloatingToolbarExitDirection.End =>
                rtl ? Bounds.Width + position.X : parentSize.Width - position.X,
            MaterialFloatingToolbarExitDirection.Top => Bounds.Height + position.Y,
            _ => parentSize.Height - position.Y
        };
        behavior.State.HeightOffsetLimit = -(limit - behavior.State.HeightOffset);
    }

    private void AttachThresholdScroll()
    {
        DetachThresholdScroll();
        if (!ExpandOnScroll || ScrollSource is not { } scroller)
            return;
        _thresholdScroll = scroller;
        _lastThresholdOffset = scroller.Offset.Y;
        ResetThreshold();
        scroller.ScrollChanged += OnThresholdScrollChanged;
    }

    private void DetachThresholdScroll()
    {
        if (_thresholdScroll is not null)
            _thresholdScroll.ScrollChanged -= OnThresholdScrollChanged;
        _thresholdScroll = null;
    }

    private void ResetThreshold()
    {
        _thresholdOffset = 0;
        _threshold = IsExpanded ? -CollapseScrollThreshold : ExpandScrollThreshold;
        if (ScrollSource is { } scroller)
            _lastThresholdOffset = scroller.Offset.Y;
    }

    private void OnThresholdScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_thresholdScroll is null)
            return;
        var current = _thresholdScroll.Offset.Y;
        var delta = current - _lastThresholdOffset;
        _lastThresholdOffset = current;
        if (Math.Abs(delta) < 1e-6)
            return;
        // Source consumed.y is negative when scrolling forward.
        var consumed = -delta;
        _thresholdOffset += consumed;
        if (consumed < 0 && _thresholdOffset <= _threshold)
        {
            _threshold = _thresholdOffset + ExpandScrollThreshold;
            IsExpanded = false;
        }
        else if (consumed > 0 && _thresholdOffset >= _threshold)
        {
            _threshold = _thresholdOffset - CollapseScrollThreshold;
            IsExpanded = true;
        }
    }
}

/// <summary>
/// Layout for the horizontal and vertical floating toolbars. With a FAB it ports
/// <c>HorizontalFloatingToolbarWithFabLayout</c> / <c>VerticalFloatingToolbarWithFabLayout</c>:
/// the FAB animates between 80 and 56 units, the toolbar reveals from zero to its intrinsic size,
/// and the pair keeps the source's 8-unit gap and alignment. Without a FAB it ports the expanding
/// leading/trailing rows with width and opacity proportional to the expansion progress.
/// </summary>
/// <remarks>
/// Children are resolved by name: PART_Leading, PART_Content, PART_Trailing, PART_Fab. The
/// content presenter must clip to its bounds for the width animation to trim its children.
/// Vertical columns order the slots leading, content, trailing, matching
/// <c>VerticalFloatingToolbarLayout</c>. A slot whose expansion progress is zero is removed from
/// tab navigation, hit testing and the automation content view; keyboard focus inside it moves
/// to the toolbar content (or is cleared if the content has no focusable action).
/// </remarks>
public sealed class MaterialFloatingToolbarLayout : Panel
{
    public static readonly StyledProperty<double> ExpandedProgressProperty =
        AvaloniaProperty.Register<MaterialFloatingToolbarLayout, double>(
            nameof(ExpandedProgress), 1d, validate: value => double.IsFinite(value));

    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<MaterialFloatingToolbarLayout, Orientation>(
            nameof(Orientation), Avalonia.Layout.Orientation.Horizontal);

    public static readonly StyledProperty<MaterialFloatingToolbarFabPosition> FabPositionProperty =
        AvaloniaProperty.Register<MaterialFloatingToolbarLayout, MaterialFloatingToolbarFabPosition>(
            nameof(FabPosition), MaterialFloatingToolbarFabPosition.End);

    public static readonly StyledProperty<bool> ShowFabProperty =
        AvaloniaProperty.Register<MaterialFloatingToolbarLayout, bool>(nameof(ShowFab));

    /// <summary>FloatingToolbarTokens.ContainerHeight (64).</summary>
    public static readonly StyledProperty<double> ContainerSizeProperty =
        AvaloniaProperty.Register<MaterialFloatingToolbarLayout, double>(nameof(ContainerSize), 64d);

    /// <summary>FloatingToolbarDefaults.ToolbarToFabGap (8).</summary>
    public static readonly StyledProperty<double> FabGapProperty =
        AvaloniaProperty.Register<MaterialFloatingToolbarLayout, double>(nameof(FabGap), 8d);

    /// <summary>FabBaselineTokens.ContainerWidth (56).</summary>
    public static readonly StyledProperty<double> FabMinSizeProperty =
        AvaloniaProperty.Register<MaterialFloatingToolbarLayout, double>(nameof(FabMinSize), 56d);

    /// <summary>FabMediumTokens.ContainerWidth (80).</summary>
    public static readonly StyledProperty<double> FabMaxSizeProperty =
        AvaloniaProperty.Register<MaterialFloatingToolbarLayout, double>(nameof(FabMaxSize), 80d);

    private double _intrinsicContent;
    private double _intrinsicLeading;
    private double _intrinsicTrailing;
    private readonly SlotAccessState _leadingAccess = new();
    private readonly SlotAccessState _trailingAccess = new();

    /// <summary>
    /// Native values of a leading/trailing template slot before the layout suppressed it.
    /// Restoring them keeps a caller-provided template's own tab and automation policy intact.
    /// </summary>
    private sealed class SlotAccessState
    {
        public bool? Hidden;
        public KeyboardNavigationMode TabNavigation;
        public AccessibilityView AccessibilityView;
        public bool HitTestVisible;
    }

    static MaterialFloatingToolbarLayout()
    {
        AffectsMeasure<MaterialFloatingToolbarLayout>(ExpandedProgressProperty, OrientationProperty,
            FabPositionProperty, ShowFabProperty, ContainerSizeProperty, FabGapProperty,
            FabMinSizeProperty, FabMaxSizeProperty);
    }

    public MaterialFloatingToolbarLayout()
    {
        ClipToBounds = false;
    }

    public double ExpandedProgress
    {
        get => GetValue(ExpandedProgressProperty);
        set => SetValue(ExpandedProgressProperty, value);
    }

    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public MaterialFloatingToolbarFabPosition FabPosition
    {
        get => GetValue(FabPositionProperty);
        set => SetValue(FabPositionProperty, value);
    }

    public bool ShowFab
    {
        get => GetValue(ShowFabProperty);
        set => SetValue(ShowFabProperty, value);
    }

    public double ContainerSize
    {
        get => GetValue(ContainerSizeProperty);
        set => SetValue(ContainerSizeProperty, value);
    }

    public double FabGap
    {
        get => GetValue(FabGapProperty);
        set => SetValue(FabGapProperty, value);
    }

    public double FabMinSize
    {
        get => GetValue(FabMinSizeProperty);
        set => SetValue(FabMinSizeProperty, value);
    }

    public double FabMaxSize
    {
        get => GetValue(FabMaxSizeProperty);
        set => SetValue(FabMaxSizeProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var leading = Find("PART_Leading");
        var content = Find("PART_Content");
        var trailing = Find("PART_Trailing");
        var fab = Find("PART_Fab");
        var background = Find("PART_Background");
        if (content is null)
            return default;

        var progress = Math.Clamp(ExpandedProgress, 0, 1);
        var horizontal = Orientation == Orientation.Horizontal;
        var size = ContainerSize;
        background?.Measure(availableSize);
        if (ShowFab && fab is not null)
        {
            var fabSize = FabMaxSize + (FabMinSize - FabMaxSize) * progress;
            fab.Measure(new Size(fabSize, fabSize));

            var intrinsic = MeasureIntrinsic(content, horizontal, size);
            _intrinsicContent = intrinsic;
            var targetMain = Math.Max(0, intrinsic * progress);
            content.Measure(horizontal
                ? new Size(targetMain, size)
                : new Size(size, targetMain));
            if (leading is not null)
                leading.Measure(default);
            if (trailing is not null)
                trailing.Measure(default);

            var overallMain = intrinsic + FabGap + FabMinSize;
            var overallCross = FitCross(availableSize, Math.Max(FabMaxSize, size), horizontal);
            return horizontal
                ? new Size(overallMain, overallCross)
                : new Size(overallCross, overallMain);
        }

        var leadingIntrinsic = leading is null ? 0 : MainIntrinsic(leading, horizontal);
        var trailingIntrinsic = trailing is null ? 0 : MainIntrinsic(trailing, horizontal);
        var contentIntrinsic = MainIntrinsic(content, horizontal);
        _intrinsicLeading = leadingIntrinsic;
        _intrinsicTrailing = trailingIntrinsic;
        _intrinsicContent = contentIntrinsic;
        var leadingMain = leadingIntrinsic * progress;
        var trailingMain = trailingIntrinsic * progress;
        var contentMain = Math.Max(0, contentIntrinsic);
        content.Measure(horizontal
            ? new Size(contentMain, availableSize.Height)
            : new Size(availableSize.Width, contentMain));
        if (leading is not null)
            leading.Measure(horizontal
                ? new Size(leadingIntrinsic, availableSize.Height)
                : new Size(availableSize.Width, leadingIntrinsic));
        if (trailing is not null)
            trailing.Measure(horizontal
                ? new Size(trailingIntrinsic, availableSize.Height)
                : new Size(availableSize.Width, trailingIntrinsic));
        if (fab is not null)
            fab.Measure(default);

        var totalMain = leadingMain + contentMain + trailingMain;
        var cross = Math.Max(size, horizontal ? content.DesiredSize.Height : content.DesiredSize.Width);
        if (horizontal)
            return new Size(totalMain, double.IsFinite(availableSize.Height) ? Math.Max(cross, size) : cross);
        return new Size(double.IsFinite(availableSize.Width) ? Math.Max(cross, size) : cross, totalMain);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var leading = Find("PART_Leading");
        var content = Find("PART_Content");
        var trailing = Find("PART_Trailing");
        var fab = Find("PART_Fab");
        var background = Find("PART_Background");
        if (content is null)
            return finalSize;

        var progress = Math.Clamp(ExpandedProgress, 0, 1);
        var horizontal = Orientation == Orientation.Horizontal;
        var rtl = FlowDirection == FlowDirection.RightToLeft;

        if (ShowFab && fab is not null)
        {
            var fabSize = FabMaxSize + (FabMinSize - FabMaxSize) * progress;
            var intrinsic = _intrinsicContent;
            var toolbarMain = content.DesiredSize.Width;
            var toolbarCross = content.DesiredSize.Height;
            if (!horizontal)
                (toolbarMain, toolbarCross) = (content.DesiredSize.Height, content.DesiredSize.Width);

            if (horizontal)
            {
                var toolbarX = FabPosition == MaterialFloatingToolbarFabPosition.End
                    ? intrinsic - toolbarMain
                    : finalSize.Width - intrinsic;
                var fabX = FabPosition == MaterialFloatingToolbarFabPosition.End
                    ? finalSize.Width - fabSize
                    : 0;
                if (rtl)
                    fabX = finalSize.Width - fabX - fabSize;
                background?.Arrange(new Rect(toolbarX, (finalSize.Height - ContainerSize) / 2,
                    toolbarMain, ContainerSize));
                content.Arrange(new Rect(toolbarX, (finalSize.Height - toolbarCross) / 2,
                    toolbarMain, toolbarCross));
                fab.Arrange(new Rect(fabX, (finalSize.Height - fabSize) / 2, fabSize, fabSize));
            }
            else
            {
                var toolbarY = FabPosition == MaterialFloatingToolbarFabPosition.Bottom
                    ? intrinsic - toolbarMain
                    : finalSize.Height - intrinsic;
                var fabY = FabPosition == MaterialFloatingToolbarFabPosition.Bottom
                    ? finalSize.Height - fabSize
                    : 0;
                background?.Arrange(new Rect((finalSize.Width - ContainerSize) / 2, toolbarY,
                    ContainerSize, toolbarMain));
                content.Arrange(new Rect((finalSize.Width - toolbarCross) / 2, toolbarY,
                    toolbarCross, toolbarMain));
                fab.Arrange(new Rect((finalSize.Width - fabSize) / 2, fabY, fabSize, fabSize));
            }
            if (leading is not null)
            {
                leading.Opacity = 0;
                leading.Arrange(default);
            }
            if (trailing is not null)
            {
                trailing.Opacity = 0;
                trailing.Arrange(default);
            }
            // VerticalFloatingToolbarWithFabLayout/HorizontalFloatingToolbarWithFabLayout have
            // no leading/trailing slots, so the template parts stay out of the focus order.
            UpdateSlotAccessibility(leading, _leadingAccess, hidden: true, content);
            UpdateSlotAccessibility(trailing, _trailingAccess, hidden: true, content);
            return finalSize;
        }

        if (horizontal)
        {
            var leadingW = (leading?.DesiredSize.Width ?? 0) * progress;
            var trailingW = (trailing?.DesiredSize.Width ?? 0) * progress;
            var contentW = Math.Max(0, finalSize.Width - leadingW - trailingW);
            var leadingX = rtl ? finalSize.Width - leadingW : 0;
            var trailingX = rtl ? 0 : finalSize.Width - trailingW;
            background?.Arrange(new Rect(finalSize));
            if (leading is not null)
            {
                leading.Opacity = progress;
                leading.Arrange(new Rect(leadingX, 0, leadingW, finalSize.Height));
            }
            if (trailing is not null)
            {
                trailing.Opacity = progress;
                trailing.Arrange(new Rect(trailingX, 0, trailingW, finalSize.Height));
            }
            var contentX = rtl ? trailingW : leadingW;
            content.Arrange(new Rect(contentX, 0, contentW, finalSize.Height));
        }
        else
        {
            background?.Arrange(new Rect(finalSize));
            var leadingH = (leading?.DesiredSize.Height ?? 0) * progress;
            var trailingH = (trailing?.DesiredSize.Height ?? 0) * progress;
            var contentH = Math.Max(0, finalSize.Height - leadingH - trailingH);
            // VerticalFloatingToolbarLayout is a Column: leading, content, trailing. Both
            // animated slots keep their template layout; the leading band occupies the top of
            // the column and the trailing band the bottom, matching the source child order.
            if (leading is not null)
            {
                leading.Opacity = progress;
                leading.Arrange(new Rect(0, 0, finalSize.Width, leadingH));
            }
            if (trailing is not null)
            {
                trailing.Opacity = progress;
                trailing.Arrange(new Rect(0, finalSize.Height - trailingH, finalSize.Width, trailingH));
            }
            content.Arrange(new Rect(0, leadingH, finalSize.Width, contentH));
        }

        // AnimatedVisibility removes fully collapsed leading/trailing content from composition;
        // here the parts stay measured, so their tab stops, automation subtree and hit testing
        // are suppressed while the expansion progress is zero.
        var slotsHidden = progress <= 0;
        UpdateSlotAccessibility(leading, _leadingAccess, slotsHidden, content);
        UpdateSlotAccessibility(trailing, _trailingAccess, slotsHidden, content);

        if (fab is not null)
            fab.Arrange(default);
        return finalSize;
    }

    /// <summary>
    /// Hides or restores one animated leading/trailing slot. A focused child of a slot that is
    /// leaving the composition would otherwise stay keyboard focus while invisible, so focus
    /// moves into the toolbar content (or is cleared when the content has no focusable action).
    /// </summary>
    private void UpdateSlotAccessibility(Control? slot, SlotAccessState state, bool hidden,
        Control? content)
    {
        if (slot is null)
        {
            state.Hidden = null;
            return;
        }
        if (state.Hidden == hidden)
            return;

        if (state.Hidden is null)
        {
            CaptureSlotState(slot, state);
            if (!hidden)
            {
                state.Hidden = false;
                return;
            }
        }
        else if (hidden)
        {
            // Visible -> hidden: preserve any native value the template adjusted in between.
            CaptureSlotState(slot, state);
        }
        state.Hidden = hidden;

        KeyboardNavigation.SetTabNavigation(slot,
            hidden ? KeyboardNavigationMode.None : state.TabNavigation);
        AutomationProperties.SetAccessibilityView(slot,
            hidden ? AccessibilityView.Raw : state.AccessibilityView);
        slot.IsHitTestVisible = !hidden && state.HitTestVisible;

        if (!hidden)
            return;

        var focusManager = TopLevel.GetTopLevel(this)?.FocusManager;
        if (focusManager?.GetFocusedElement() is not Visual focused)
            return;
        if (!ReferenceEquals(focused, slot) && !slot.IsVisualAncestorOf(focused))
            return;

        if (FindFocusable(content) is { } target)
            target.Focus(NavigationMethod.Directional);
        else
            focusManager.Focus(null);
    }

    private static void CaptureSlotState(Control slot, SlotAccessState state)
    {
        state.TabNavigation = KeyboardNavigation.GetTabNavigation(slot);
        state.AccessibilityView = AutomationProperties.GetAccessibilityView(slot);
        state.HitTestVisible = slot.IsHitTestVisible;
    }

    private static InputElement? FindFocusable(Control? root)
    {
        if (root is null)
            return null;
        foreach (var visual in root.GetSelfAndVisualDescendants())
        {
            if (visual is InputElement { Focusable: true, IsEffectivelyEnabled: true,
                    IsEffectivelyVisible: true } input
                && input.IsAttachedToVisualTree())
                return input;
        }
        return null;
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

    private double MeasureIntrinsic(Control control, bool horizontal, double containerSize)
    {
        control.Measure(horizontal
            ? new Size(double.PositiveInfinity, containerSize)
            : new Size(containerSize, double.PositiveInfinity));
        return horizontal ? control.DesiredSize.Width : control.DesiredSize.Height;
    }

    private static double MainIntrinsic(Control control, bool horizontal)
    {
        control.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return horizontal ? control.DesiredSize.Width : control.DesiredSize.Height;
    }

    private static double FitCross(Size available, double desired, bool horizontal) => desired;
}