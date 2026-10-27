using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Theming;
using ExpressiveAvalonia.Tokens;

namespace ExpressiveAvalonia.Controls;

/// <summary>The three source navigation drawer presentations.</summary>
public enum MaterialNavigationDrawerPresentation
{
    /// <summary><c>PermanentNavigationDrawer</c>: always visible, content is inset, no scrim.</summary>
    Permanent,

    /// <summary><c>DismissibleNavigationDrawer</c>: starts closed, pushes content while open.</summary>
    Dismissible,

    /// <summary><c>ModalNavigationDrawer</c>: overlays content with a scrim and traps focus.</summary>
    Modal
}

/// <summary>Why a navigation drawer close was requested.</summary>
public enum MaterialNavigationDrawerCloseReason
{
    Programmatic,
    Scrim,
    Escape,
    Gesture,
    Selection
}

/// <summary>Cancellable close notification for <see cref="MaterialNavigationDrawerHost"/>.</summary>
public sealed class MaterialNavigationDrawerClosingEventArgs : CancelEventArgs
{
    public MaterialNavigationDrawerClosingEventArgs(MaterialNavigationDrawerCloseReason reason) => Reason = reason;

    public MaterialNavigationDrawerCloseReason Reason { get; }
}

/// <summary>
/// Hosts the source <c>PermanentNavigationDrawer</c>, <c>DismissibleNavigationDrawer</c> and
/// <c>ModalNavigationDrawer</c> presentations around a <see cref="MaterialNavigationDrawer"/>.
/// </summary>
/// <remarks>
/// <para>
/// Adapted from <c>NavigationDrawer.kt</c> at the pinned commit. Presentation drives the source
/// layout contracts: permanent drawers are always visible and inset the content; dismissible
/// drawers start closed and displace the content by the open fraction; modal drawers overlay the
/// content with a scrim whose alpha is the same fraction. The drawer sheet takes the standard or
/// modal variant (surface vs surface-container-low, square vs <c>CornerLargeEnd</c>).
/// </para>
/// <para>
/// Motion is frame-based: opening uses <c>DefaultSpatial</c>, closing <c>FastEffects</c>, and
/// drag settling <c>DefaultSpatial</c>, matching <c>DrawerState</c>. The positional threshold is
/// 50% and a release velocity of at least 400 DIP/s selects its own direction; otherwise the
/// nearest anchor wins. Escape, scrim dismissal and selection can close a non-permanent drawer;
/// <see cref="DrawerClosing"/> can cancel any close before the animation starts, matching
/// <c>confirmStateChange</c>.
/// </para>
/// <para>
/// The source installs the horizontal drag on the whole host with <c>gesturesEnabled</c>, so a
/// closed modal or dismissible drawer can be swiped open anywhere in the host. Clicks remain
/// untouched until the horizontal slop is exceeded. A permanent drawer never drags. Dismissible
/// page content is measured with the unchanged host constraints and translated by the exposed
/// drawer width instead of being narrowed; only the permanent presentation reserves width.
/// </para>
/// <para>
/// Avalonia mirrors the right-to-left plane once at the flow-direction boundary
/// (<c>Visual.HasMirrorTransform</c>), so the sheet, its slide translation, the page translation
/// and the pointer deltas all stay in the host's logical coordinates. The sheet is anchored on
/// the local leading edge with the closed offset <c>-width</c> and a positive swipe delta opens
/// in both flow directions; the mirror renders the result on the physical right for RTL.
/// </para>
/// <para>
/// Opening a dismissible or modal drawer remembers focus and requests the first drawer element;
/// modal drawers trap Tab/Shift+Tab and restore focus when closed. <see cref="IsLightDismissEnabled"/>
/// corresponds to the source <c>gesturesEnabled</c> flag for both scrim dismissal and dragging.
/// </para>
/// </remarks>
[PseudoClasses(":drawer-open", ":modal", ":dismissible", ":permanent", ":dragging")]
[TemplatePart("PART_Overlay", typeof(Panel))]
[TemplatePart("PART_Scrim", typeof(Border))]
[TemplatePart("PART_DrawerHost", typeof(Border))]
[TemplatePart("PART_ContentPresenter", typeof(ContentPresenter))]
public sealed class MaterialNavigationDrawerHost : ContentControl
{
    public static readonly StyledProperty<MaterialNavigationDrawer?> DrawerProperty =
        AvaloniaProperty.Register<MaterialNavigationDrawerHost, MaterialNavigationDrawer?>(nameof(Drawer));

    public static readonly StyledProperty<bool> IsDrawerOpenProperty =
        AvaloniaProperty.Register<MaterialNavigationDrawerHost, bool>(
            nameof(IsDrawerOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The source presentation (permanent, dismissible or modal).</summary>
    public static readonly StyledProperty<MaterialNavigationDrawerPresentation> PresentationProperty =
        AvaloniaProperty.Register<MaterialNavigationDrawerHost, MaterialNavigationDrawerPresentation>(
            nameof(Presentation), MaterialNavigationDrawerPresentation.Modal);

    public static readonly StyledProperty<bool> IsLightDismissEnabledProperty =
        AvaloniaProperty.Register<MaterialNavigationDrawerHost, bool>(nameof(IsLightDismissEnabled), true);

    /// <summary>Extension: close after a destination is selected. The source requires the caller to close.</summary>
    public static readonly StyledProperty<bool> CloseOnSelectionProperty =
        AvaloniaProperty.Register<MaterialNavigationDrawerHost, bool>(nameof(CloseOnSelection));

    /// <summary>Extension: outer inset for an edge drawer. Defaults to zero (source full-height sheet).</summary>
    public static readonly StyledProperty<Thickness> DrawerMarginProperty =
        AvaloniaProperty.Register<MaterialNavigationDrawerHost, Thickness>(nameof(DrawerMargin));

    public static readonly StyledProperty<double> ScrimOpacityProperty =
        AvaloniaProperty.Register<MaterialNavigationDrawerHost, double>(
            nameof(ScrimOpacity), 0.32d,
            validate: static value => double.IsFinite(value) && value >= 0 && value <= 1);

    private const double PositionalThreshold = 0.5;
    private const double VelocityThreshold = 400;

    // The source velocity tracker drops samples after a pause, so a release that follows a held
    // pointer settles positionally instead of reusing stale motion.
    private const ulong VelocityResetMilliseconds = 100;

    private Panel? _overlay;
    private Border? _scrim;
    private Border? _drawerHost;
    private ContentPresenter? _contentPresenter;
    private TranslateTransform? _slide;
    private TranslateTransform? _contentSlide;
    private MaterialMotion.SpringAnimation? _progressSpring;
    private InputElement? _previousFocus;
    private MaterialNavigationDrawer? _subscribedDrawer;
    private TopLevel? _overlayOwner;
    private bool _overlayRegistered;
    private double _progress;
    private bool _syncing;
    private bool _dragging;
    private bool _dragCandidate;
    private double _dragStartX;
    private double _dragStartProgress;
    private double _lastX;
    private ulong _lastTime;
    private double _openingVelocity;

    static MaterialNavigationDrawerHost()
    {
        DrawerProperty.Changed.AddClassHandler<MaterialNavigationDrawerHost>(
            static (host, _) => host.OnDrawerChanged());
        IsDrawerOpenProperty.Changed.AddClassHandler<MaterialNavigationDrawerHost>(
            static (host, _) => host.OnOpenChanged());
        PresentationProperty.Changed.AddClassHandler<MaterialNavigationDrawerHost>(
            static (host, _) => host.OnPresentationChanged());
        DrawerMarginProperty.Changed.AddClassHandler<MaterialNavigationDrawerHost>(
            static (host, _) => host.ApplyProgress());
        ScrimOpacityProperty.Changed.AddClassHandler<MaterialNavigationDrawerHost>(
            static (host, _) => host.ApplyProgress());
        IsLightDismissEnabledProperty.Changed.AddClassHandler<MaterialNavigationDrawerHost>(
            static (host, _) => host.UpdatePseudoClasses());
    }

    public MaterialNavigationDrawerHost()
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        UpdatePseudoClasses();
        AddHandler(KeyDownEvent, OnHostKeyDownTunnel, RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, OnHostKeyDownBubble, RoutingStrategies.Bubble);
        AddHandler(PointerPressedEvent, OnHostPointerPressed, RoutingStrategies.Tunnel);
        AddHandler(PointerMovedEvent, OnHostPointerMoved, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, OnHostPointerReleased, RoutingStrategies.Tunnel);
        AddHandler(PointerCaptureLostEvent, OnHostPointerCaptureLost, RoutingStrategies.Tunnel);
    }

    public event EventHandler? DrawerOpened;
    public event EventHandler? DrawerClosed;

    /// <summary>Cancellable close request; set <see cref="CancelEventArgs.Cancel"/> to veto.</summary>
    public event EventHandler<MaterialNavigationDrawerClosingEventArgs>? DrawerClosing;

    public MaterialNavigationDrawer? Drawer
    {
        get => GetValue(DrawerProperty);
        set => SetValue(DrawerProperty, value);
    }

    /// <summary>Whether the drawer is open. Two-way by default.</summary>
    public bool IsDrawerOpen
    {
        get => GetValue(IsDrawerOpenProperty);
        set => SetValue(IsDrawerOpenProperty, value);
    }

    public MaterialNavigationDrawerPresentation Presentation
    {
        get => GetValue(PresentationProperty);
        set => SetValue(PresentationProperty, value);
    }

    /// <summary>Enables scrim dismissal and horizontal drag for dismissible/modal drawers.</summary>
    public bool IsLightDismissEnabled
    {
        get => GetValue(IsLightDismissEnabledProperty);
        set => SetValue(IsLightDismissEnabledProperty, value);
    }

    public bool CloseOnSelection
    {
        get => GetValue(CloseOnSelectionProperty);
        set => SetValue(CloseOnSelectionProperty, value);
    }

    public Thickness DrawerMargin
    {
        get => GetValue(DrawerMarginProperty);
        set => SetValue(DrawerMarginProperty, value);
    }

    public double ScrimOpacity
    {
        get => GetValue(ScrimOpacityProperty);
        set => SetValue(ScrimOpacityProperty, value);
    }

    /// <summary>Requests a close that can be vetoed by <see cref="DrawerClosing"/>.</summary>
    public bool CloseDrawer(MaterialNavigationDrawerCloseReason reason = MaterialNavigationDrawerCloseReason.Programmatic) =>
        RequestClose(reason);

    public void OpenDrawer()
    {
        if (Presentation == MaterialNavigationDrawerPresentation.Permanent)
            return;
        SetCurrentValue(IsDrawerOpenProperty, true);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_scrim is not null)
            _scrim.PointerPressed -= OnScrimPointerPressed;
        if (_drawerHost is not null)
            _drawerHost.SizeChanged -= OnDrawerHostSizeChanged;

        base.OnApplyTemplate(e);

        _overlay = e.NameScope.Find<Panel>("PART_Overlay");
        _scrim = e.NameScope.Find<Border>("PART_Scrim");
        _drawerHost = e.NameScope.Find<Border>("PART_DrawerHost");
        _contentPresenter = e.NameScope.Find<ContentPresenter>("PART_ContentPresenter");

        if (_contentPresenter is not null)
        {
            // Dismissible drawers translate the page instead of narrowing it, so the presenter
            // owns a transform rather than a leading margin.
            _contentSlide = new TranslateTransform();
            _contentPresenter.RenderTransform = _contentSlide;
        }

        if (_drawerHost is not null)
        {
            _slide = _drawerHost.RenderTransform as TranslateTransform ?? new TranslateTransform();
            _drawerHost.RenderTransform = _slide;
            _drawerHost.SizeChanged += OnDrawerHostSizeChanged;
        }
        if (_scrim is not null)
            _scrim.PointerPressed += OnScrimPointerPressed;

        ApplyPresentation();
        // A host declared open in XAML has no open transition after the template is applied.
        if (IsOpenTarget && Presentation != MaterialNavigationDrawerPresentation.Permanent && _progress <= 0)
            _progress = 1;
        ApplyProgress();
        if (IsOpenTarget && Presentation != MaterialNavigationDrawerPresentation.Permanent)
        {
            _previousFocus = FocusManager()?.GetFocusedElement() as InputElement;
            if (Presentation == MaterialNavigationDrawerPresentation.Modal)
                RegisterOverlay();
            Dispatcher.UIThread.Post(FocusInitialDrawerElement, DispatcherPriority.Loaded);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateDrawerSubscription();
        ApplyPresentation();
        if (IsOpenTarget && Presentation == MaterialNavigationDrawerPresentation.Modal)
            RegisterOverlay();
        ApplyProgress();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _progressSpring?.Dispose();
        _progressSpring = null;
        UnregisterOverlay();
        UpdateDrawerSubscription();
        base.OnDetachedFromVisualTree(e);
    }

    private void OnDrawerChanged()
    {
        UpdateDrawerSubscription();
        ApplyPresentation();
        ApplyProgress();
    }

    private void UpdateDrawerSubscription()
    {
        var target = this.IsAttachedToVisualTree() ? Drawer : null;
        if (ReferenceEquals(_subscribedDrawer, target))
            return;

        if (_subscribedDrawer is { } previous)
        {
            previous.SelectionChanged -= OnDrawerSelectionChanged;
            previous.PropertyChanged -= OnDrawerPropertyChanged;
        }

        _subscribedDrawer = target;
        if (target is not null)
        {
            target.SelectionChanged += OnDrawerSelectionChanged;
            target.PropertyChanged += OnDrawerPropertyChanged;
        }
    }

    private void OnDrawerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Visual.BoundsProperty || e.Property == WidthProperty)
            ApplyProgress();
    }

    private void OnDrawerSelectionChanged(object? sender, MaterialNavigationSelectionChangedEventArgs e)
    {
        if (CloseOnSelection && IsOpenTarget && e.AddedItem is not null &&
            Presentation != MaterialNavigationDrawerPresentation.Permanent)
        {
            RequestClose(MaterialNavigationDrawerCloseReason.Selection);
        }
    }

    private void OnPresentationChanged()
    {
        if (Presentation == MaterialNavigationDrawerPresentation.Permanent)
        {
            SetOpenSilently(true);
            UnregisterOverlay();
        }
        else if (Presentation == MaterialNavigationDrawerPresentation.Modal && IsOpenTarget)
        {
            RegisterOverlay();
        }
        else
        {
            UnregisterOverlay();
        }
        ApplyPresentation();
        ApplyProgress();
    }

    private void RegisterOverlay()
    {
        if (_overlayRegistered)
            return;
        _overlayOwner = TopLevel.GetTopLevel(this);
        if (_overlayOwner is null)
            return;
        MaterialOverlayLifecycle.Register(_overlayOwner, this);
        _overlayRegistered = true;
    }

    private void OnOpenChanged()
    {
        if (_syncing)
            return;

        if (Presentation == MaterialNavigationDrawerPresentation.Permanent)
        {
            SetOpenSilently(true);
            return;
        }

        if (IsDrawerOpen)
        {
            BeginOpen();
            return;
        }

        var args = new MaterialNavigationDrawerClosingEventArgs(MaterialNavigationDrawerCloseReason.Programmatic);
        DrawerClosing?.Invoke(this, args);
        if (args.Cancel)
        {
            SetOpenSilently(true);
            return;
        }

        BeginClose();
    }

    private void BeginOpen()
    {
        PseudoClasses.Set(":drawer-open", true);
        _previousFocus = FocusManager()?.GetFocusedElement() as InputElement;
        if (Presentation == MaterialNavigationDrawerPresentation.Modal)
            RegisterOverlay();
        AnimateProgress(1, opening: true);
        Dispatcher.UIThread.Post(FocusInitialDrawerElement, DispatcherPriority.Loaded);
        DrawerOpened?.Invoke(this, EventArgs.Empty);
    }

    private void BeginClose()
    {
        PseudoClasses.Set(":drawer-open", false);
        UnregisterOverlay();
        AnimateProgress(0, opening: false);
        RestoreFocus();
        DrawerClosed?.Invoke(this, EventArgs.Empty);
    }

    private void UnregisterOverlay()
    {
        if (!_overlayRegistered)
            return;
        _overlayRegistered = false;
        MaterialOverlayLifecycle.Unregister(_overlayOwner, this);
        _overlayOwner = null;
    }

    private bool RequestClose(MaterialNavigationDrawerCloseReason reason)
    {
        if (!IsOpenTarget || Presentation == MaterialNavigationDrawerPresentation.Permanent)
            return false;

        var args = new MaterialNavigationDrawerClosingEventArgs(reason);
        DrawerClosing?.Invoke(this, args);
        if (args.Cancel)
            return false;

        SetOpenSilently(false);
        BeginClose();
        return true;
    }

    private void SetOpenSilently(bool open)
    {
        _syncing = true;
        try
        {
            SetCurrentValue(IsDrawerOpenProperty, open);
        }
        finally
        {
            _syncing = false;
        }
        PseudoClasses.Set(":drawer-open", open || Presentation == MaterialNavigationDrawerPresentation.Permanent);
    }

    private bool IsOpenTarget =>
        Presentation == MaterialNavigationDrawerPresentation.Permanent || IsDrawerOpen;

    private void ApplyPresentation()
    {
        UpdatePseudoClasses();
        if (_drawerHost is null)
            return;

        // Avalonia mirrors the right-to-left plane once at the flow-direction boundary
        // (Visual.HasMirrorTransform), so every visual inside the host - the template, the sheet
        // and its slide translation - stays in logical coordinates. The source expresses the
        // drawer on the leading edge with placeRelative, so local left is the start edge in both
        // flows and the mirror renders it on the physical right for RTL.
        _drawerHost.HorizontalAlignment = HorizontalAlignment.Left;
        _drawerHost.Margin = DrawerMargin;
        if (Drawer is { } drawer)
            drawer.Variant = Presentation == MaterialNavigationDrawerPresentation.Modal
                ? MaterialNavigationDrawerVariant.Modal
                : MaterialNavigationDrawerVariant.Standard;
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(":permanent", Presentation == MaterialNavigationDrawerPresentation.Permanent);
        PseudoClasses.Set(":dismissible", Presentation == MaterialNavigationDrawerPresentation.Dismissible);
        PseudoClasses.Set(":modal", Presentation == MaterialNavigationDrawerPresentation.Modal);
        PseudoClasses.Set(":drawer-open", IsOpenTarget);
        PseudoClasses.Set(":dragging", _dragging);
    }

    private void AnimateProgress(double target, bool opening)
    {
        _progressSpring?.Dispose();
        _progressSpring = null;
        if (!this.IsAttachedToVisualTree() || MaterialMotion.GetIsReduced(this))
        {
            _progress = target;
            ApplyProgress();
            return;
        }

        var spring = opening ? MaterialSpring.SpatialDefault : MaterialSpring.EffectsFast;
        _progressSpring = MaterialMotion.Animate(this, _progress, target,
            value => { _progress = value; ApplyProgress(); }, spring, 0.001);
    }

    private void ApplyProgress()
    {
        if (_drawerHost is null || _overlay is null || _scrim is null)
            return;

        var permanent = Presentation == MaterialNavigationDrawerPresentation.Permanent;
        var modal = Presentation == MaterialNavigationDrawerPresentation.Modal;
        var progress = permanent ? 1 : Math.Clamp(_progress, 0, 1);
        var width = DrawerWidth();
        var visible = permanent || progress > 0.0001;

        _drawerHost.IsVisible = visible;
        if (_slide is not null)
        {
            // DrawerState's closed anchor is -width and its open anchor is 0 on the relative
            // (leading) axis; the mirror produces the physical side, so the sign never flips.
            _slide.X = -width * (1 - progress) * (permanent ? 0 : 1);
        }

        _drawerHost.Margin = DrawerMargin;
        _overlay.IsVisible = modal && visible;
        _scrim.Opacity = modal ? progress * ScrimOpacity : 0;

        // Source DismissibleNavigationDrawer measures the page with the unchanged host
        // constraints and places it at the exposed drawer width, so the page slides under the
        // clipped host instead of reflowing. Only a permanent drawer reserves width in layout;
        // a modal drawer never displaces the page.
        if (permanent)
            SetContentOffset(width, reserveWidth: true);
        else if (Presentation == MaterialNavigationDrawerPresentation.Dismissible)
            SetContentOffset(width * progress, reserveWidth: false);
        else
            SetContentOffset(0, reserveWidth: false);
    }

    private IFocusManager? FocusManager() => TopLevel.GetTopLevel(this)?.FocusManager;

    private void SetContentOffset(double leading, bool reserveWidth)
    {
        if (_contentPresenter is null)
            return;
        // DismissibleNavigationDrawer places the page at `sheetWidth + offset` with
        // placeRelative, i.e. on the logical trailing side; the presenters live inside the
        // mirror boundary, so these offsets need no flow-direction branch.
        if (reserveWidth)
        {
            _contentPresenter.Margin = new Thickness(leading, 0, 0, 0);
            if (_contentSlide is not null)
                _contentSlide.X = 0;
            return;
        }

        _contentPresenter.Margin = default;
        if (_contentSlide is not null)
            _contentSlide.X = leading;
    }

    private double DrawerWidth()
    {
        if (_drawerHost is { Bounds.Width: > 0 } host)
            return host.Bounds.Width;
        var declared = Drawer?.Width ?? double.NaN;
        if (double.IsFinite(declared) && declared > 0)
            return declared;
        return 360;
    }

    private void OnDrawerHostSizeChanged(object? sender, SizeChangedEventArgs e) => ApplyProgress();

    private void OnScrimPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsOpenTarget || Presentation != MaterialNavigationDrawerPresentation.Modal ||
            !IsLightDismissEnabled || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        if (!MaterialOverlayLifecycle.IsTopmost(TopLevel.GetTopLevel(this), this))
            return;

        if (RequestClose(MaterialNavigationDrawerCloseReason.Scrim))
            e.Handled = true;
    }

    private void OnHostKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        if (e.Handled || !IsOpenTarget || Presentation != MaterialNavigationDrawerPresentation.Modal)
            return;
        if (!MaterialOverlayLifecycle.IsTopmost(TopLevel.GetTopLevel(this), this))
            return;
        if (e.Key == Key.Tab)
            TrapFocus(e);
    }

    private void OnHostKeyDownBubble(object? sender, KeyEventArgs e)
    {
        if (e.Handled || !IsOpenTarget || Presentation == MaterialNavigationDrawerPresentation.Permanent)
            return;
        if (!MaterialOverlayLifecycle.IsTopmost(TopLevel.GetTopLevel(this), this))
            return;

        if (e.Key == Key.Escape)
        {
            if (RequestClose(MaterialNavigationDrawerCloseReason.Escape))
                e.Handled = true;
        }
    }

    private void TrapFocus(KeyEventArgs e)
    {
        var focusables = GetDrawerFocusables();
        if (focusables.Count == 0)
        {
            e.Handled = true;
            return;
        }

        var focused = FocusManager()?.GetFocusedElement() as Control;
        var index = focused is null ? -1 : focusables.IndexOf(focused);
        if (index < 0)
        {
            focusables[0].Focus(NavigationMethod.Tab, e.KeyModifiers);
            e.Handled = true;
            return;
        }

        var backwards = (e.KeyModifiers & KeyModifiers.Shift) == KeyModifiers.Shift;
        var next = index + (backwards ? -1 : 1);
        if (next >= focusables.Count)
            next = 0;
        else if (next < 0)
            next = focusables.Count - 1;
        focusables[next].Focus(NavigationMethod.Tab, e.KeyModifiers);
        e.Handled = true;
    }

    private void FocusInitialDrawerElement()
    {
        if (!IsOpenTarget || Presentation == MaterialNavigationDrawerPresentation.Permanent)
            return;
        var target = Drawer?.SelectedItem ?? GetDrawerFocusables().FirstOrDefault();
        target?.Focus(NavigationMethod.Tab);
    }

    private void RestoreFocus()
    {
        if (_previousFocus is null)
            return;

        var focused = FocusManager()?.GetFocusedElement();
        if (focused is null || ReferenceEquals(focused, this) || IsWithinDrawer(focused as Visual))
            _previousFocus.Focus(NavigationMethod.Tab);
        _previousFocus = null;
    }

    private bool IsWithinDrawer(Visual? visual) =>
        visual is not null && (ReferenceEquals(visual, _drawerHost) || _drawerHost?.IsVisualAncestorOf(visual) == true);

    private List<Control> GetDrawerFocusables()
    {
        var result = new List<Control>();
        if (Drawer is not { } drawer)
            return result;

        foreach (var control in drawer.GetVisualDescendants().OfType<Control>())
        {
            if (control.Focusable && control.IsEffectivelyEnabled && control.IsEffectivelyVisible &&
                control.IsTabStop)
                result.Add(control);
        }
        return result;
    }

    private void OnHostPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsLightDismissEnabled ||
            Presentation == MaterialNavigationDrawerPresentation.Permanent ||
            !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        if (e.Source is not Visual source)
            return;

        // The source attaches the horizontal drag to the whole host with gesturesEnabled,
        // independent of the open state, so a closed drawer can be swiped open anywhere.
        // The scrim keeps its press-to-dismiss policy, and an already open drawer is dragged
        // from its sheet so page content keeps its own pointer interactions.
        if (_overlay is { IsVisible: true } overlay && overlay.IsVisualAncestorOf(source))
            return;
        if (IsOpenTarget && !IsWithinDrawer(source))
            return;

        _dragCandidate = true;
        _dragging = false;
        _dragStartX = _lastX = e.GetPosition(this).X;
        _dragStartProgress = _progress;
        _lastTime = e.Timestamp;
        _openingVelocity = 0;
    }

    private void OnHostPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_dragCandidate && !_dragging)
            return;

        var x = e.GetPosition(this).X;
        var dx = x - _dragStartX;
        if (!_dragging)
        {
            if (Math.Abs(dx) < 6)
                return;
            _dragging = true;
            _progressSpring?.Dispose();
            _progressSpring = null;
            PseudoClasses.Set(":dragging", true);
            e.Pointer.Capture(this);
        }

        var delta = e.Timestamp > _lastTime ? e.Timestamp - _lastTime : 0;
        if (delta > 0)
            _openingVelocity = (x - _lastX) * 1000d / delta;
        _lastX = x;
        _lastTime = e.Timestamp;

        var width = Math.Max(1, DrawerWidth());
        // The pointer delta is already in the host's logical coordinate space: Avalonia's RTL
        // mirror is applied exactly once at the flow-direction boundary, so it needs no second
        // reversal here (Compose reverses its physical deltas, this control never sees them).
        _progress = Math.Clamp(_dragStartProgress + dx / width, 0, 1);
        ApplyProgress();
        e.Handled = true;
    }

    private void OnHostPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_dragCandidate && !_dragging)
            return;

        _dragCandidate = false;
        if (!_dragging)
            return;

        _dragging = false;
        PseudoClasses.Set(":dragging", false);
        e.Pointer.Capture(null);
        e.Handled = true;

        if (e.Timestamp > _lastTime && e.Timestamp - _lastTime > VelocityResetMilliseconds)
            _openingVelocity = 0;

        // Signed velocity selects its own anchor in either direction once it reaches the
        // source threshold; otherwise the positional threshold decides.
        var open = Math.Abs(_openingVelocity) >= VelocityThreshold
            ? _openingVelocity > 0
            : _progress >= PositionalThreshold;
        SettleDrag(open);
    }

    private void SettleDrag(bool open)
    {
        if (open)
        {
            if (!IsDrawerOpen)
            {
                SetCurrentValue(IsDrawerOpenProperty, true);
                return;
            }
            AnimateProgress(1, opening: true);
            return;
        }

        if (IsDrawerOpen)
        {
            if (!RequestClose(MaterialNavigationDrawerCloseReason.Gesture))
                AnimateProgress(1, opening: true);
            return;
        }

        SetOpenSilently(false);
        AnimateProgress(0, opening: false);
    }

    private void OnHostPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (!_dragging)
            return;
        _dragging = false;
        _dragCandidate = false;
        PseudoClasses.Set(":dragging", false);
        if (IsOpenTarget)
            AnimateProgress(1, opening: true);
        else
        {
            SetOpenSilently(false);
            AnimateProgress(0, opening: false);
        }
    }
}