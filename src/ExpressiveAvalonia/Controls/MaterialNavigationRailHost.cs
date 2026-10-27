using System;
using System.Collections.Generic;
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

/// <summary>Where an open expanded rail sits relative to the hosted content.</summary>
public enum MaterialNavigationRailHostPlacement
{
    /// <summary>Pinned to the leading edge at full height; the source <c>Type=Docked</c>.</summary>
    Docked,

    /// <summary>Inset from the edges as a rounded surface; the source <c>Type=Floating</c>.</summary>
    Floating
}

/// <summary>
/// Hosts a <see cref="MaterialNavigationRail"/> as the expanded, modal navigation surface of a
/// screen: open/close lifecycle, scrim, Escape and light dismissal, focus trap and restore, and
/// the source Docked/Floating placement.
/// </summary>
/// <remarks>
/// <para>
/// The expanded rail is a modal destination picker in the source: it covers the screen behind a
/// scrim and returns to the content when a destination is chosen. This host owns that contract.
/// While open in modal mode the rail blocks pointer input to the content, Tab and Shift+Tab cycle
/// inside the rail, Escape closes it, and a primary press on the scrim closes it when
/// <see cref="IsLightDismissEnabled"/> is true. Opening remembers the focused element and focuses
/// the selected destination (or the first focusable control in the rail); closing restores focus.
/// </para>
/// <para>
/// <see cref="Placement"/> mirrors the source <c>Type</c> axis: <see cref="MaterialNavigationRailHostPlacement.Docked"/>
/// pins the rail to the leading edge and insets the content instead of covering it when
/// <see cref="IsModal"/> is false, and <see cref="MaterialNavigationRailHostPlacement.Floating"/>
/// insets it by <see cref="RailMargin"/> as a rounded surface. The host also forwards the
/// placement to the rail's <see cref="MaterialNavigationRail.IsFloating"/> property, because the
/// floating surface treatment belongs to the rail itself.
/// </para>
/// <para>
/// <see cref="HideOnCollapse"/> mirrors the source <c>ModalWideNavigationRail.hideOnCollapse</c>
/// parameter and defaults to false: a native collapsed presentation (<see cref="CollapsedRail"/>)
/// stays docked in the layout and keeps its content inset even while the expanded modal rail is
/// shown, so expanding never reflows the screen. Set it to true for the legacy dismissible
/// presentation that slides the collapsed rail away and drops the inset. When the host can share a
/// data <see cref="ItemsControl.ItemsSource"/> it creates the collapsed presentation itself; a rail
/// whose items are control instances (as the catalog does) must supply <see cref="CollapsedRail"/>,
/// because one control cannot be parented into both presentations.
/// </para>
/// <para>
/// With <see cref="HideOnCollapse"/> set, the source turns the modal surface into an
/// anchored-draggable: <c>gesturesEnabled = hideOnCollapse</c> and anchors Expanded at 0 /
/// Collapsed at -width (WideNavigationRail.kt 1595-1617; the host's full travel also includes a
/// floating margin). A horizontal drag on the rail surface
/// moves the surface with the pointer, and releasing past 50% of the travel (or with a flick of
/// at least 125 DIP/s in the dismissing direction) commits the dismissal; a shorter drag or a
/// capture loss returns it to the open anchor. With <see cref="HideOnCollapse"/> false no drag
/// exists at all, so the docked collapsed presentation and its native clicks are untouched.
/// </para>
/// <para>
/// The slide and scrim use the source modal motion: <c>DefaultSpatial</c> for the surface
/// (WideNavigationRail.kt 747-750) and <c>DefaultEffects</c> for the scrim alpha (834-850).
/// Both are frame-based <see cref="MaterialMotion"/> springs, so a rapid reversal retargets the
/// running animation with its velocity, reduced motion snaps, and detaching disposes it.
/// </para>
/// </remarks>
[PseudoClasses(":rail-open", ":modal", ":floating", ":collapsed-base", ":dragging")]
[TemplatePart("PART_Overlay", typeof(Panel))]
[TemplatePart("PART_Scrim", typeof(Border))]
[TemplatePart("PART_RailHost", typeof(Border))]
[TemplatePart("PART_CollapsedHost", typeof(Border))]
[TemplatePart("PART_ContentPresenter", typeof(ContentPresenter))]
public sealed class MaterialNavigationRailHost : ContentControl
{
    /// <summary>Defines the <see cref="Rail"/> property.</summary>
    public static readonly StyledProperty<MaterialNavigationRail?> RailProperty =
        AvaloniaProperty.Register<MaterialNavigationRailHost, MaterialNavigationRail?>(nameof(Rail));

    /// <summary>Defines the <see cref="IsRailOpen"/> property.</summary>
    public static readonly StyledProperty<bool> IsRailOpenProperty =
        AvaloniaProperty.Register<MaterialNavigationRailHost, bool>(
            nameof(IsRailOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Defines the <see cref="IsModal"/> property.</summary>
    public static readonly StyledProperty<bool> IsModalProperty =
        AvaloniaProperty.Register<MaterialNavigationRailHost, bool>(nameof(IsModal), true);

    /// <summary>Defines the <see cref="HideOnCollapse"/> property.</summary>
    public static readonly StyledProperty<bool> HideOnCollapseProperty =
        AvaloniaProperty.Register<MaterialNavigationRailHost, bool>(nameof(HideOnCollapse));

    /// <summary>Defines the <see cref="CollapsedRail"/> property.</summary>
    public static readonly StyledProperty<MaterialNavigationRail?> CollapsedRailProperty =
        AvaloniaProperty.Register<MaterialNavigationRailHost, MaterialNavigationRail?>(nameof(CollapsedRail));

    /// <summary>Defines the read-only <see cref="CollapsedPresentation"/> property.</summary>
    public static readonly DirectProperty<MaterialNavigationRailHost, MaterialNavigationRail?> CollapsedPresentationProperty =
        AvaloniaProperty.RegisterDirect<MaterialNavigationRailHost, MaterialNavigationRail?>(
            nameof(CollapsedPresentation), host => host.CollapsedPresentation);

    /// <summary>Defines the <see cref="Placement"/> property.</summary>
    public static readonly StyledProperty<MaterialNavigationRailHostPlacement> PlacementProperty =
        AvaloniaProperty.Register<MaterialNavigationRailHost, MaterialNavigationRailHostPlacement>(nameof(Placement));

    /// <summary>Defines the <see cref="IsLightDismissEnabled"/> property.</summary>
    public static readonly StyledProperty<bool> IsLightDismissEnabledProperty =
        AvaloniaProperty.Register<MaterialNavigationRailHost, bool>(nameof(IsLightDismissEnabled), true);

    /// <summary>Defines the <see cref="CloseOnSelection"/> property.</summary>
    public static readonly StyledProperty<bool> CloseOnSelectionProperty =
        AvaloniaProperty.Register<MaterialNavigationRailHost, bool>(nameof(CloseOnSelection), true);

    /// <summary>Defines the <see cref="RailMargin"/> property.</summary>
    public static readonly StyledProperty<Thickness> RailMarginProperty =
        AvaloniaProperty.Register<MaterialNavigationRailHost, Thickness>(nameof(RailMargin), new Thickness(12));

    /// <summary>Defines the <see cref="ScrimOpacity"/> property.</summary>
    public static readonly StyledProperty<double> ScrimOpacityProperty =
        AvaloniaProperty.Register<MaterialNavigationRailHost, double>(
            nameof(ScrimOpacity), 0.32d,
            validate: static value => double.IsFinite(value) && value >= 0 && value <= 1);

    private Panel? _overlay;
    private Border? _scrim;
    private Border? _railHost;
    private Border? _collapsedHost;
    private ContentPresenter? _contentPresenter;
    private TranslateTransform? _slide;
    private MaterialMotion.SpringAnimation? _slideSpring;
    private MaterialMotion.SpringAnimation? _scrimSpring;
    private MaterialNavigationRail? _generatedCollapsedRail;
    private MaterialNavigationRail? _collapsedPresentation;
    private MaterialNavigationRail? _subscribedCollapsedRail;
    private IDisposable? _collapsedSelectionBinding;
    private InputElement? _previousFocus;
    private TopLevel? _overlayOwner;
    private bool _overlayRegistered;
    private bool _closing;
    private double _slideTarget;
    private bool _creatingSlideSpring;

    // AnchoredDraggable's fling policy: 50% of the anchor distance, and 125 dp/s as the
    // minimum fling velocity (AnchoredDraggable.kt computeTarget / AnchoredDraggableDefaults).
    private const double RailPositionalThreshold = 0.5;
    private const double RailVelocityThreshold = 125;
    // Native touch slop. Until it is crossed the press stays a normal child input.
    private const double RailDragSlop = 6;

    private bool _railDragCandidate;
    private bool _railDragging;
    private double _railDragStartX;
    private double _railDragStartProgress;
    private double _railDragDistance;
    private double _railDragProgress;
    private double _railDragLastX;
    private ulong _railDragLastTimestamp;
    private double _railDragVelocity;
    private bool _railDragDismissPending;

    static MaterialNavigationRailHost()
    {
        RailProperty.Changed.AddClassHandler<MaterialNavigationRailHost>(
            static (host, change) => host.OnRailChanged(change));
        IsRailOpenProperty.Changed.AddClassHandler<MaterialNavigationRailHost>(
            static (host, _) => host.OnOpenChanged());
        IsModalProperty.Changed.AddClassHandler<MaterialNavigationRailHost>(
            static (host, _) => host.OnModalChanged());
        HideOnCollapseProperty.Changed.AddClassHandler<MaterialNavigationRailHost>(
            static (host, _) => host.UpdatePresentation(animate: false));
        CollapsedRailProperty.Changed.AddClassHandler<MaterialNavigationRailHost>(
            static (host, _) => host.OnCollapsedRailChanged());
        PlacementProperty.Changed.AddClassHandler<MaterialNavigationRailHost>(
            static (host, _) => host.OnPlacementChanged());
        RailMarginProperty.Changed.AddClassHandler<MaterialNavigationRailHost>(
            static (host, _) => host.UpdatePresentation(animate: false));
        ScrimOpacityProperty.Changed.AddClassHandler<MaterialNavigationRailHost>(
            static (host, _) => host.UpdateScrim());
    }

    public MaterialNavigationRailHost()
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        PseudoClasses.Set(":modal", IsModal);
        AddHandler(KeyDownEvent, OnHostKeyDownTunnel, RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, OnHostKeyDownBubble, RoutingStrategies.Bubble);
        AddHandler(PointerPressedEvent, OnHostPointerPressed, RoutingStrategies.Tunnel);
        AddHandler(PointerMovedEvent, OnHostPointerMoved, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, OnHostPointerReleased, RoutingStrategies.Tunnel);
        AddHandler(PointerCaptureLostEvent, OnHostPointerCaptureLost, RoutingStrategies.Direct);
    }

    /// <summary>Raised after the rail has been opened.</summary>
    public event EventHandler? RailOpened;

    /// <summary>Raised after the rail has been closed.</summary>
    public event EventHandler? RailClosed;

    /// <summary>The expanded rail shown by this host.</summary>
    public MaterialNavigationRail? Rail
    {
        get => GetValue(RailProperty);
        set => SetValue(RailProperty, value);
    }

    /// <summary>Whether the rail is open. Two-way by default.</summary>
    public bool IsRailOpen
    {
        get => GetValue(IsRailOpenProperty);
        set => SetValue(IsRailOpenProperty, value);
    }

    /// <summary>
    /// Whether the rail covers the content behind a scrim and traps focus. The source expanded
    /// rail is modal, so this defaults to true.
    /// </summary>
    public bool IsModal
    {
        get => GetValue(IsModalProperty);
        set => SetValue(IsModalProperty, value);
    }

    /// <summary>
    /// Whether the collapsed rail slides away when the modal rail is closed. The source
    /// <c>hideOnCollapse</c> defaults to false, which keeps a collapsed, non-modal rail in the
    /// layout (and its content inset) even while the expanded modal rail is shown.
    /// </summary>
    public bool HideOnCollapse
    {
        get => GetValue(HideOnCollapseProperty);
        set => SetValue(HideOnCollapseProperty, value);
    }

    /// <summary>
    /// Native collapsed presentation kept in the layout while <see cref="HideOnCollapse"/> is
    /// false. Supply one when <see cref="Rail"/> contains control items, because a control cannot
    /// be parented into both presentations; data-driven rails get a generated presentation that
    /// shares <see cref="ItemsControl.ItemsSource"/> and selection.
    /// </summary>
    public MaterialNavigationRail? CollapsedRail
    {
        get => GetValue(CollapsedRailProperty);
        set => SetValue(CollapsedRailProperty, value);
    }

    /// <summary>
    /// The collapsed rail currently parented by the host: <see cref="CollapsedRail"/> when
    /// supplied, otherwise the generated data-driven presentation, or null while
    /// <see cref="HideOnCollapse"/> is true.
    /// </summary>
    public MaterialNavigationRail? CollapsedPresentation => _collapsedPresentation;

    /// <summary>The source <c>Type</c> axis: docked to the leading edge, or floating with margins.</summary>
    public MaterialNavigationRailHostPlacement Placement
    {
        get => GetValue(PlacementProperty);
        set => SetValue(PlacementProperty, value);
    }

    /// <summary>Whether tapping the scrim dismisses a modal rail. Escape always dismisses.</summary>
    public bool IsLightDismissEnabled
    {
        get => GetValue(IsLightDismissEnabledProperty);
        set => SetValue(IsLightDismissEnabledProperty, value);
    }

    /// <summary>
    /// Whether activating a destination (pointer release, Enter, or Space) dismisses an open
    /// modal rail. Arrow-key browsing only moves the selection and never dismisses it.
    /// </summary>
    public bool CloseOnSelection
    {
        get => GetValue(CloseOnSelectionProperty);
        set => SetValue(CloseOnSelectionProperty, value);
    }

    /// <summary>Inset around a floating rail. Ignored while docked.</summary>
    public Thickness RailMargin
    {
        get => GetValue(RailMarginProperty);
        set => SetValue(RailMarginProperty, value);
    }

    /// <summary>Target scrim opacity while the modal rail is open, from 0 to 1.</summary>
    public double ScrimOpacity
    {
        get => GetValue(ScrimOpacityProperty);
        set => SetValue(ScrimOpacityProperty, value);
    }

    /// <summary>Opens the rail and starts the modal lifecycle.</summary>
    public void OpenRail() => SetCurrentValue(IsRailOpenProperty, true);

    /// <summary>Closes the rail as if the user had dismissed it.</summary>
    public void CloseRail() => SetCurrentValue(IsRailOpenProperty, false);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_scrim is not null)
            _scrim.PointerPressed -= OnScrimPointerPressed;

        base.OnApplyTemplate(e);

        _slideSpring?.Dispose();
        _slideSpring = null;
        _scrimSpring?.Dispose();
        _scrimSpring = null;

        _overlay = e.NameScope.Find<Panel>("PART_Overlay");
        _scrim = e.NameScope.Find<Border>("PART_Scrim");
        _railHost = e.NameScope.Find<Border>("PART_RailHost");
        _collapsedHost = e.NameScope.Find<Border>("PART_CollapsedHost");
        _contentPresenter = e.NameScope.Find<ContentPresenter>("PART_ContentPresenter");
        _slide = _railHost?.RenderTransform as TranslateTransform;

        if (_scrim is not null)
            _scrim.PointerPressed += OnScrimPointerPressed;

        UpdatePresentation(animate: false);

        // A rail opened in XAML has no OnOpenChanged transition after the template is applied.
        if (IsRailOpen && IsModal)
        {
            _previousFocus = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as InputElement;
            RegisterOverlay();
            Dispatcher.UIThread.Post(FocusInitialRailElement, DispatcherPriority.Loaded);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _slideSpring?.Dispose();
        _slideSpring = null;
        _scrimSpring?.Dispose();
        _scrimSpring = null;
        _closing = false;
        ResetRailDrag();
        UnregisterOverlay();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdatePresentation(animate: false);
    }

    private void OnRailChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.GetOldValue<MaterialNavigationRail?>() is { } oldRail)
        {
            oldRail.ItemInvoked -= OnRailItemInvoked;
            oldRail.PropertyChanged -= OnRailPropertyChanged;
        }

        if (change.GetNewValue<MaterialNavigationRail?>() is { } newRail)
        {
            newRail.ItemInvoked += OnRailItemInvoked;
            newRail.PropertyChanged += OnRailPropertyChanged;
            // The floating surface treatment lives on the rail; the host owns placement.
            newRail.IsFloating = Placement == MaterialNavigationRailHostPlacement.Floating;
            ApplyRailMotion(newRail);
        }

        RebuildGeneratedCollapsedRail();
        RebindCollapsedRail();
        UpdatePresentation(animate: false);
    }

    private void OnRailPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != ItemsControl.ItemsSourceProperty)
            return;
        RebuildGeneratedCollapsedRail();
        RebindCollapsedRail();
        UpdatePresentation(animate: false);
    }

    private void OnCollapsedRailChanged()
    {
        RebuildGeneratedCollapsedRail();
        RebindCollapsedRail();
        UpdatePresentation(animate: false);
    }

    /// <summary>
    /// Builds the source collapsed presentation for data-driven rails only. Control items cannot
    /// be shared by two visual trees, so those callers supply <see cref="CollapsedRail"/>.
    /// </summary>
    private void RebuildGeneratedCollapsedRail()
    {
        var itemsSource = Rail?.ItemsSource;
        if (CollapsedRail is not null || Rail is not { } source ||
            itemsSource is null || !CanShareItems(itemsSource))
        {
            _generatedCollapsedRail = null;
            return;
        }

        var clone = new MaterialNavigationRail
        {
            Mode = MaterialNavigationRailMode.Wide,
            IsExpanded = false,
            IsFloating = false,
            SelectionMode = SelectionMode.Single,
            Focusable = source.Focusable,
        };
        clone.Bind(ItemsControl.ItemsSourceProperty,
            new Binding(nameof(ItemsControl.ItemsSource)) { Source = source });
        clone.Bind(MaterialNavigationRail.RailAlignmentProperty,
            new Binding(nameof(MaterialNavigationRail.RailAlignment)) { Source = source });
        clone.Bind(MaterialNavigationRail.ItemIconPositionProperty,
            new Binding(nameof(MaterialNavigationRail.ItemIconPosition)) { Source = source });
        clone.Bind(MaterialNavigationRail.ShowMenuButtonProperty,
            new Binding(nameof(MaterialNavigationRail.ShowMenuButton)) { Source = source });
        clone.Bind(MaterialNavigationRail.ShowFabProperty,
            new Binding(nameof(MaterialNavigationRail.ShowFab)) { Source = source });
        clone.Bind(MaterialNavigationRail.MenuIconProperty,
            new Binding(nameof(MaterialNavigationRail.MenuIcon)) { Source = source });
        clone.Bind(MaterialNavigationRail.FabIconProperty,
            new Binding(nameof(MaterialNavigationRail.FabIcon)) { Source = source });
        clone.Bind(MaterialNavigationRail.FabTextProperty,
            new Binding(nameof(MaterialNavigationRail.FabText)) { Source = source });
        clone.Bind(MaterialNavigationRail.MenuCommandProperty,
            new Binding(nameof(MaterialNavigationRail.MenuCommand)) { Source = source });
        clone.Bind(MaterialNavigationRail.MenuCommandParameterProperty,
            new Binding(nameof(MaterialNavigationRail.MenuCommandParameter)) { Source = source });
        clone.Bind(MaterialNavigationRail.FabCommandProperty,
            new Binding(nameof(MaterialNavigationRail.FabCommand)) { Source = source });
        clone.Bind(MaterialNavigationRail.FabCommandParameterProperty,
            new Binding(nameof(MaterialNavigationRail.FabCommandParameter)) { Source = source });
        clone.Bind(MaterialNavigationRail.HeaderProperty,
            new Binding(nameof(MaterialNavigationRail.Header)) { Source = source });
        clone.Bind(MaterialNavigationRail.HeaderTemplateProperty,
            new Binding(nameof(MaterialNavigationRail.HeaderTemplate)) { Source = source });
        clone.Bind(ItemsControl.ItemTemplateProperty,
            new Binding(nameof(ItemsControl.ItemTemplate)) { Source = source });
        clone.Bind(ItemsControl.ItemContainerThemeProperty,
            new Binding(nameof(ItemsControl.ItemContainerTheme)) { Source = source });
        clone.Bind(IsEnabledProperty,
            new Binding(nameof(IsEnabled)) { Source = source });
        clone.Bind(Visual.FlowDirectionProperty,
            new Binding(nameof(FlowDirection)) { Source = source });
        _generatedCollapsedRail = clone;
    }

    private MaterialNavigationRail? EffectiveCollapsedRail => CollapsedRail ?? _generatedCollapsedRail;

    private void SetCollapsedPresentation(MaterialNavigationRail? value)
    {
        if (ReferenceEquals(_collapsedPresentation, value))
            return;
        var previous = _collapsedPresentation;
        _collapsedPresentation = value;
        RaisePropertyChanged(CollapsedPresentationProperty, previous, value);
    }

    // A control item can only have one visual parent. Data sources are safe to share; an
    // explicit CollapsedRail covers the (rare) control-typed ItemsSource case.
    private static bool CanShareItems(System.Collections.IEnumerable source)
    {
        if (source is not System.Collections.ICollection collection)
            return true;
        foreach (var item in collection)
        {
            if (item is Control)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Binds the collapsed presentation to the same selection as the expanded rail and forwards
    /// its item activations, so both native presentations share state without a stub list.
    /// </summary>
    private void RebindCollapsedRail()
    {
        _collapsedSelectionBinding?.Dispose();
        _collapsedSelectionBinding = null;
        if (_subscribedCollapsedRail is not null)
        {
            _subscribedCollapsedRail.ItemInvoked -= OnCollapsedItemInvoked;
            _subscribedCollapsedRail = null;
        }

        if (EffectiveCollapsedRail is not { } collapsed)
            return;

        collapsed.ItemInvoked += OnCollapsedItemInvoked;
        _subscribedCollapsedRail = collapsed;
        if (Rail is { } rail)
        {
            _collapsedSelectionBinding = collapsed.Bind(ListBox.SelectedIndexProperty,
                new Binding(nameof(ListBox.SelectedIndex))
                {
                    Source = rail,
                    Mode = BindingMode.TwoWay
                });
        }
    }

    private void OnCollapsedItemInvoked(object? sender, MaterialNavigationRailItemInvokedEventArgs e)
    {
        // Selection already flows through the binding; forward the activation to the expanded
        // rail so the application's ItemInvoked contract sees one event from either presentation.
        if (Rail is { } rail)
            rail.InvokeItemAt(e.Index, rail.ContainerFromIndex(e.Index) as MaterialNavigationRailItem ?? e.Item);
    }

    private void OnModalChanged()
    {
        if (Rail is { } rail)
            ApplyRailMotion(rail);
        UpdatePresentation(animate: false);
    }

    // The modal rail uses the source FastSpatial expansion spring; a permanent rail uses
    // DefaultSpatial (WideNavigationRail.kt animationSpec/modalAnimationSpec).
    private void ApplyRailMotion(MaterialNavigationRail rail) =>
        rail.ExpansionSpring = IsModal ? MaterialSpring.SpatialFast : MaterialSpring.SpatialDefault;

    private void OnPlacementChanged()
    {
        PseudoClasses.Set(":floating", Placement == MaterialNavigationRailHostPlacement.Floating);
        if (Rail is { } rail)
            rail.IsFloating = Placement == MaterialNavigationRailHostPlacement.Floating;
        UpdatePresentation(animate: false);
    }

    /// <summary>
    /// Closes on a destination activation, not on every selection change: arrow navigation in a
    /// rail moves the selection continuously, and dismissing the surface on each step would make
    /// keyboard browsing impossible.
    /// </summary>
    private void OnRailItemInvoked(object? sender, MaterialNavigationRailItemInvokedEventArgs e)
    {
        if (CloseOnSelection && IsModal && IsRailOpen)
            CloseRail();
    }

    private void OnOpenChanged()
    {
        var open = IsRailOpen;
        PseudoClasses.Set(":rail-open", open);
        UpdatePresentation(animate: true);

        if (open)
        {
            if (IsModal)
            {
                _previousFocus = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as InputElement;
                RegisterOverlay();
                Dispatcher.UIThread.Post(FocusInitialRailElement, DispatcherPriority.Loaded);
            }

            RailOpened?.Invoke(this, EventArgs.Empty);
            return;
        }

        UnregisterOverlay();
        ResetRailDrag();
        RestoreFocus();
        RailClosed?.Invoke(this, EventArgs.Empty);
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

    private void UnregisterOverlay()
    {
        if (!_overlayRegistered)
            return;
        _overlayRegistered = false;
        MaterialOverlayLifecycle.Unregister(_overlayOwner, this);
        _overlayOwner = null;
    }

    private void UpdatePresentation(bool animate)
    {
        if (IsModal && IsRailOpen)
            RegisterOverlay();
        else
            UnregisterOverlay();

        if (_railHost is null || _overlay is null)
            return;

        var open = IsRailOpen;
        var floating = Placement == MaterialNavigationRailHostPlacement.Floating;
        var collapsed = IsModal && !HideOnCollapse ? EffectiveCollapsedRail : null;
        SetCollapsedPresentation(collapsed);
        PseudoClasses.Set(":modal", IsModal);
        PseudoClasses.Set(":floating", floating);
        PseudoClasses.Set(":collapsed-base", collapsed is not null);

        // Avalonia mirrors the whole RTL plane (Visual.HasMirrorTransform), so layout stays on
        // the leading edge in local coordinates and the renderer places it on the right in RTL.
        _railHost.HorizontalAlignment = HorizontalAlignment.Left;
        _railHost.VerticalAlignment = VerticalAlignment.Stretch;
        _railHost.Margin = floating ? RailMargin : default;

        if (_collapsedHost is not null)
        {
            _collapsedHost.HorizontalAlignment = HorizontalAlignment.Left;
            _collapsedHost.IsVisible = collapsed is not null;
            _collapsedHost.Margin = floating ? RailMargin : default;
        }
        if (collapsed is not null)
            collapsed.IsFloating = floating;

        if (!IsModal)
        {
            _closing = false;
            _slideSpring?.Dispose();
            _slideSpring = null;
            _scrimSpring?.Dispose();
            _scrimSpring = null;
            _overlay.IsVisible = false;
            _overlay.IsHitTestVisible = false;
            _railHost.IsVisible = open;
            if (_slide is not null)
                _slide.X = 0;
            SetContentInset(open && !floating ? RailWidth() : 0);
            return;
        }

        // hideOnCollapse=false keeps the collapsed rail in the layout grid even while the
        // expanded modal rail is shown, so the content inset never changes with expansion.
        SetContentInset(collapsed is null
            ? 0
            : CollapsedWidth() + (floating ? RailMargin.Left + RailMargin.Right : 0));

        if (!open)
        {
            HideModal(animate);
            return;
        }

        ShowModal(animate);
    }

    private void ShowModal(bool animate)
    {
        if (_railHost is null || _overlay is null || _scrim is null)
            return;

        var wasVisible = _overlay.IsVisible && _railHost.IsVisible;
        _overlay.IsVisible = true;
        _overlay.IsHitTestVisible = true;
        _railHost.IsVisible = true;
        _closing = false;

        if (_slide is not null && !wasVisible)
            _slide.X = ClosedOffset();

        if (!animate || MaterialMotion.GetIsReduced(this) || _slide is null)
        {
            SnapModalOpen();
            return;
        }

        AnimateSlideTo(0);
        AnimateScrimTo(ScrimOpacity);
    }

    private void HideModal(bool animate)
    {
        if (_railHost is null || _overlay is null || _scrim is null)
            return;

        if (!_overlay.IsVisible)
            return;

        if (!animate || !_railHost.IsVisible || _slide is null || MaterialMotion.GetIsReduced(this))
        {
            HideModalNow();
            return;
        }

        _closing = true;
        // The closing surface is not hit-testable; only the settled hidden state removes it.
        _overlay.IsHitTestVisible = false;
        AnimateSlideTo(ClosedOffset());
        AnimateScrimTo(0);
    }

    private void SnapModalOpen()
    {
        _closing = false;
        _railDragDismissPending = false;
        _slideSpring?.Dispose();
        _slideSpring = null;
        _scrimSpring?.Dispose();
        _scrimSpring = null;
        if (_slide is not null)
            _slide.X = 0;
        if (_scrim is not null)
            _scrim.Opacity = ScrimOpacity;
    }

    private void HideModalNow()
    {
        _closing = false;
        _railDragDismissPending = false;
        _slideSpring?.Dispose();
        _slideSpring = null;
        _scrimSpring?.Dispose();
        _scrimSpring = null;
        if (_railHost is not null)
            _railHost.IsVisible = false;
        if (_overlay is not null)
        {
            _overlay.IsVisible = false;
            _overlay.IsHitTestVisible = false;
        }
        if (_scrim is not null)
            _scrim.Opacity = 0;
        if (_slide is not null)
            _slide.X = 0;
    }

    // Source modal motion: the modal surface uses DefaultSpatial (WideNavigationRail.kt
    // 747-750); the scrim alpha uses DefaultEffects (834-850). Both springs retarget with the
    // current velocity, snap under reduced motion and finish when the owner detaches.
    private void AnimateSlideTo(double target, double initialVelocity = 0)
    {
        if (_railHost is null || _slide is null)
            return;

        _slideTarget = target;
        if (_slideSpring is null)
        {
            var start = _slide.X;
            _creatingSlideSpring = true;
            _slideSpring = MaterialMotion.Animate(
                _railHost, start, start,
                value =>
                {
                    if (_slide is not null)
                        _slide.X = value;
                    if (!_creatingSlideSpring && _closing && Math.Abs(value - _slideTarget) <= 0.5)
                    {
                        // A committed swipe closes the shared rail state only after the
                        // source's fling has settled at the collapsed anchor.
                        var commitDragDismiss = _railDragDismissPending;
                        _railDragDismissPending = false;
                        HideModalNow();
                        if (commitDragDismiss)
                            SetCurrentValue(IsRailOpenProperty, false);
                    }
                },
                MaterialSpring.SpatialDefault, 0.5, initialVelocity);
            _creatingSlideSpring = false;
        }

        _slideTarget = target;
        _slideSpring.Retarget(target);
    }

    private void AnimateScrimTo(double target)
    {
        if (_scrim is null)
            return;

        if (_scrimSpring is null)
        {
            _scrimSpring = MaterialMotion.Animate(
                _scrim, _scrim.Opacity, _scrim.Opacity,
                value =>
                {
                    if (_scrim is not null)
                        _scrim.Opacity = value;
                },
                MaterialSpring.EffectsDefault, 0.01);
        }

        _scrimSpring.Retarget(target);
    }

    private void UpdateScrim()
    {
        if (_scrim is null || !IsModal || !IsRailOpen)
            return;
        if (_scrimSpring is not null)
            _scrimSpring.Retarget(ScrimOpacity);
        else
            _scrim.Opacity = ScrimOpacity;
    }

    private void SetContentInset(double leading)
    {
        if (_contentPresenter is null)
            return;
        // The presenter lives inside the mirror boundary, so a leading-edge inset becomes a
        // trailing-edge inset visually when the host is RTL.
        _contentPresenter.Margin = new Thickness(leading, 0, 0, 0);
    }

    private bool IsRightToLeft =>
        FlowDirection == Avalonia.Media.FlowDirection.RightToLeft;

    private double SlideDistance()
    {
        if (_railHost is not { } host)
            return RailWidth();

        var width = host.Bounds.Width > 0 ? host.Bounds.Width : RailWidth();
        return width + host.Margin.Left + host.Margin.Right;
    }

    // The closed anchor in surface translation units: the rail leaves through the local leading
    // edge (WideNavigationRail.kt anchors Collapsed at -width). Avalonia's RTL mirror renders the
    // same local translation on the visual right edge, so the sign does not flip with direction.
    // The host travels its full slide distance so a floating surface clears its margin as well.
    private double ClosedOffset() => -SlideDistance();

    private double RailWidth()
    {
        if (Rail is { } rail && double.IsFinite(rail.Width) && rail.Width > 0)
            return rail.Width;
        if (_railHost is { Bounds.Width: > 0 } host)
            return host.Bounds.Width;
        // The expanded rail is 220 wide; the compact one is 96.
        return Rail?.IsExpanded == false ? 96 : 220;
    }

    private double CollapsedWidth()
    {
        if (EffectiveCollapsedRail is { } rail && double.IsFinite(rail.Width) && rail.Width > 0)
            return rail.Width;
        if (_collapsedHost is { Bounds.Width: > 0 } host)
            return host.Bounds.Width;
        // The source collapsed wide rail is 96; a compact rail is 80.
        return EffectiveCollapsedRail?.Mode == MaterialNavigationRailMode.Compact ? 80 : 96;
    }

    private void OnScrimPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsModal || !IsLightDismissEnabled || !IsRailOpen)
            return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        if (!MaterialOverlayLifecycle.IsTopmost(TopLevel.GetTopLevel(this), this))
            return;

        CloseRail();
        e.Handled = true;
    }

    private bool CanDragRail(PointerEventArgs e, bool requirePress)
    {
        // Source: gesturesEnabled = hideOnCollapse (WideNavigationRail.kt 1595-1617). A rail that
        // keeps its collapsed presentation never becomes draggable.
        if (!IsModal || !IsRailOpen || !HideOnCollapse)
            return false;
        if (_railHost is null || _slide is null)
            return false;
        if (requirePress && !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return false;
        return MaterialOverlayLifecycle.IsTopmost(TopLevel.GetTopLevel(this), this);
    }

    private bool IsRailSurface(Visual? source) =>
        source is not null && _railHost is not null &&
        (ReferenceEquals(source, _railHost) || _railHost.IsVisualAncestorOf(source));

    // The host is an RTL mirror boundary, so local coordinates are horizontally mirrored while
    // pointer samples arrive in unmirrored root coordinates. Gesture math stays in root space.
    private double RootX(PointerEventArgs e) =>
        e.GetPosition(TopLevel.GetTopLevel(this)).X;

    private void OnHostPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!CanDragRail(e, requirePress: true) || e.Source is not Visual source || !IsRailSurface(source))
            return;

        // The candidate records the press without handling it: a tap still reaches a rail item,
        // a button or the header until the source touch slop is crossed.
        _railDragCandidate = true;
        _railDragging = false;
        _railDragStartX = RootX(e);
        _railDragLastX = _railDragStartX;
        _railDragLastTimestamp = e.Timestamp;
        _railDragVelocity = 0;
    }

    private void OnHostPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_railDragCandidate && !_railDragging)
            return;

        var x = RootX(e);
        if (!_railDragging)
        {
            if (Math.Abs(x - _railDragStartX) < RailDragSlop)
                return;

            // awaitTouchSlopOrCancellation consumes the slop, so the drag starts from this
            // sample; the surface begins at the position it is currently showing.
            _railDragging = true;
            _railDragStartX = x;
            _railDragLastX = x;
            _railDragLastTimestamp = e.Timestamp;
            _railDragVelocity = 0;
            _railDragDistance = Math.Max(1, SlideDistance());
            _railDragStartProgress = _slide is null
                ? 0
                : Math.Clamp(Math.Abs(_slide.X) / _railDragDistance, 0, 1);
            _railDragProgress = _railDragStartProgress;
            _slideSpring?.Dispose();
            _slideSpring = null;
            _railDragDismissPending = false;
            PseudoClasses.Set(":dragging", true);
            e.Pointer.Capture(this);
        }

        var elapsed = e.Timestamp > _railDragLastTimestamp ? e.Timestamp - _railDragLastTimestamp : 0;
        if (elapsed > 0)
        {
            var rootVelocity = (x - _railDragLastX) / (elapsed / 1000d);
            // Source offset axis: Expanded=0, Collapsed=-D. RTL reverses horizontal deltas.
            _railDragVelocity = IsRightToLeft ? -rootVelocity : rootVelocity;
        }
        _railDragLastX = x;
        _railDragLastTimestamp = e.Timestamp;

        // AnchoredDraggable reverses horizontal deltas in RTL. Offset is always measured along
        // the local leading edge (LTR left / RTL right after Avalonia's mirror); the local
        // translation keeps the same negative sign in both directions.
        var travel = (IsRightToLeft ? 1 : -1) * (x - _railDragStartX) / _railDragDistance;
        _railDragProgress = Math.Clamp(_railDragStartProgress + travel, 0, 1);
        if (_slide is not null)
            _slide.X = -_railDragDistance * _railDragProgress;
        e.Handled = true;
    }

    private void OnHostPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_railDragCandidate && !_railDragging)
            return;

        _railDragCandidate = false;
        if (!_railDragging)
            return;

        _railDragging = false;
        PseudoClasses.Set(":dragging", false);
        e.Pointer.Capture(null);
        e.Handled = true;
        SettleRailDrag(cancelled: false);
    }

    private void OnHostPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _railDragCandidate = false;
        if (!_railDragging)
            return;

        // Losing capture (alt-tab, another gesture, owner removal) is a cancellation: settle
        // back to the open anchor instead of guessing a dismissal target.
        _railDragging = false;
        PseudoClasses.Set(":dragging", false);
        SettleRailDrag(cancelled: true);
    }

    /// <summary>
    /// Source <c>computeTarget</c>: a fling past AnchoredDraggable's 125 dp/s picks the next
    /// anchor in its direction; below it, 50% of the anchor distance decides.
    /// </summary>
    private void SettleRailDrag(bool cancelled)
    {
        if (_slide is null)
            return;

        // Velocity is already in the anchor/local translation axis (negative = toward Collapsed),
        // with RTL horizontal reversal applied while sampling.
        var anchorVelocity = _railDragVelocity;
        var dismiss = !cancelled &&
                      (Math.Abs(anchorVelocity) >= RailVelocityThreshold
                          ? anchorVelocity < 0
                          : _railDragProgress >= RailPositionalThreshold);

        if (!dismiss)
        {
            AnimateSlideTo(0, _railDragVelocity);
            return;
        }

        // The shared rail state collapses after the surface settles, matching the source
        // railFlingBehavior's finally { modalAnimateToDismiss() }.
        _railDragDismissPending = true;
        _closing = true;
        if (_overlay is not null)
            _overlay.IsHitTestVisible = false;
        AnimateScrimTo(0);
        AnimateSlideTo(ClosedOffset(), _railDragVelocity);
    }

    private void ResetRailDrag()
    {
        _railDragCandidate = false;
        _railDragging = false;
        _railDragDismissPending = false;
        PseudoClasses.Set(":dragging", false);
    }

    private void OnHostKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        if (e.Handled || !IsModal || !IsRailOpen)
            return;
        if (!MaterialOverlayLifecycle.IsTopmost(TopLevel.GetTopLevel(this), this))
            return;

        if (e.Key == Key.Tab)
            TrapFocus(e);
    }

    private void OnHostKeyDownBubble(object? sender, KeyEventArgs e)
    {
        if (e.Handled || !IsModal || !IsRailOpen)
            return;
        if (!MaterialOverlayLifecycle.IsTopmost(TopLevel.GetTopLevel(this), this))
            return;

        if (e.Key == Key.Escape)
        {
            CloseRail();
            e.Handled = true;
        }
    }

    private void TrapFocus(KeyEventArgs e)
    {
        var focusables = GetRailFocusables();
        if (focusables.Count == 0)
        {
            e.Handled = true;
            return;
        }

        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;
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

    private void FocusInitialRailElement()
    {
        if (!IsModal || !IsRailOpen || Rail is not { } rail)
            return;

        var target = rail.SelectedItem as Control ?? rail.ContainerFromIndex(rail.SelectedIndex) ??
                     GetRailFocusables().FirstOrDefault();
        target?.Focus(NavigationMethod.Tab);
    }

    private List<Control> GetRailFocusables()
    {
        var result = new List<Control>();
        if (Rail is not { } rail)
            return result;

        if (rail.Focusable)
            result.Add(rail);

        foreach (var control in rail.GetVisualDescendants().OfType<Control>())
        {
            if (control.Focusable && control.IsEffectivelyEnabled && control.IsEffectivelyVisible)
                result.Add(control);
        }

        return result;
    }

    private void RestoreFocus()
    {
        if (!IsModal)
            return;

        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual;
        var focusInRail = focused is not null && Rail is { } rail &&
            (ReferenceEquals(focused, rail) || rail.IsVisualAncestorOf(focused));

        if (focusInRail || focused is null)
            _previousFocus?.Focus();
        _previousFocus = null;
    }
}
