using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Theming;
using ExpressiveAvalonia.Tokens;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// An XR toolbar: a fully rounded surface pill of icon slots with an optional floating action
/// button beside (horizontal) or below (vertical) it.
/// </summary>
/// <remarks>
/// Desktop composition of <c>SpatialHorizontalFloatingToolbar</c> /
/// <c>SpatialVerticalFloatingToolbar</c>. The Orbiter and spatial elevation are dropped, but the
/// applicable 2D geometry is the pinned XrFloatingToolbarTokens values: 64 minimum container
/// cross-size, 50% container shape, 8 content padding and the 8 unit toolbar-to-FAB gap. The
/// composition reuses <see cref="MaterialFloatingToolbarLayout"/> and
/// <see cref="MaterialAppBarScrollBehavior"/>, so the leading/trailing slots expand from the
/// common layout and a floating-toolbar exit-always behavior hides/shows the whole pill.
/// <para>
/// Items are ordinary focusable controls, normally <see cref="XrIconButton"/> or
/// <see cref="XrToggleIconButton"/>. Left/Right (horizontal) or Up/Down (vertical) move focus
/// between the realized slots, while Tab, Space/Enter, and access keys stay native.
/// The source axes are <see cref="Orientation"/> and <see cref="Elevation"/>; the FAB axis is
/// <see cref="ShowFab"/>.
/// </para>
/// </remarks>
public class XrToolbar : ItemsControl
{
    /// <summary>Horizontal places the FAB after the pill; vertical places it below.</summary>
    public static readonly StyledProperty<Avalonia.Layout.Orientation> OrientationProperty =
        AvaloniaProperty.Register<XrToolbar, Avalonia.Layout.Orientation>(nameof(Orientation), Avalonia.Layout.Orientation.Horizontal);

    /// <summary>The surface tone of the pill. The FAB follows the tertiary roles.</summary>
    public static readonly StyledProperty<XrSurfaceRole> ElevationProperty =
        AvaloniaProperty.Register<XrToolbar, XrSurfaceRole>(nameof(Elevation));

    /// <summary>Whether the spatial FAB is shown.</summary>
    public static readonly StyledProperty<bool> ShowFabProperty =
        AvaloniaProperty.Register<XrToolbar, bool>(nameof(ShowFab), true);

    public static readonly StyledProperty<Geometry?> FabIconProperty =
        AvaloniaProperty.Register<XrToolbar, Geometry?>(nameof(FabIcon), XrIcons.Add);

    /// <summary>Accessible name of the FAB.</summary>
    public static readonly StyledProperty<string?> FabLabelProperty =
        AvaloniaProperty.Register<XrToolbar, string?>(nameof(FabLabel), "Create");

    public static readonly StyledProperty<ICommand?> FabCommandProperty =
        AvaloniaProperty.Register<XrToolbar, ICommand?>(nameof(FabCommand));

    public static readonly StyledProperty<object?> FabCommandParameterProperty =
        AvaloniaProperty.Register<XrToolbar, object?>(nameof(FabCommandParameter));

    /// <summary>Expanded state of the common floating-toolbar layout.</summary>
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<XrToolbar, bool>(nameof(IsExpanded), true);

    /// <summary>Content shown at the start when the toolbar is expanded (no FAB form).</summary>
    public static readonly StyledProperty<object?> LeadingContentProperty =
        AvaloniaProperty.Register<XrToolbar, object?>(nameof(LeadingContent));

    /// <summary>Content shown at the end when the toolbar is expanded (no FAB form).</summary>
    public static readonly StyledProperty<object?> TrailingContentProperty =
        AvaloniaProperty.Register<XrToolbar, object?>(nameof(TrailingContent));

    /// <summary>Content padding; the pinned XrFloatingToolbar uses 8 on every side.</summary>
    public static readonly StyledProperty<Thickness> ContentPaddingProperty =
        AvaloniaProperty.Register<XrToolbar, Thickness>(nameof(ContentPadding), new Thickness(8));

    /// <summary>Hoisted common floating-toolbar scroll behavior; null keeps the pill fixed.</summary>
    public static readonly StyledProperty<MaterialAppBarScrollBehavior?> ScrollBehaviorProperty =
        AvaloniaProperty.Register<XrToolbar, MaterialAppBarScrollBehavior?>(nameof(ScrollBehavior));

    /// <summary>Native scroll viewer that drives the common exit-always behavior.</summary>
    public static readonly StyledProperty<ScrollViewer?> ScrollSourceProperty =
        AvaloniaProperty.Register<XrToolbar, ScrollViewer?>(nameof(ScrollSource));

    public static readonly DirectProperty<XrToolbar, double> ExpandedProgressProperty =
        AvaloniaProperty.RegisterDirect<XrToolbar, double>(
            nameof(ExpandedProgress), toolbar => toolbar.ExpandedProgress);

    public static readonly DirectProperty<XrToolbar, MaterialFloatingToolbarFabPosition> EffectiveFabPositionProperty =
        AvaloniaProperty.RegisterDirect<XrToolbar, MaterialFloatingToolbarFabPosition>(
            nameof(EffectiveFabPosition), toolbar => toolbar.EffectiveFabPosition);

    private readonly TranslateTransform _exitTransform = new();
    private MaterialMotion.SpringAnimation? _expandAnimation;
    private MaterialBarScrollAdapter? _adapter;
    private MaterialAppBarState? _state;
    private double _expandedProgress = 1;
    private MaterialFloatingToolbarFabPosition _effectiveFabPosition =
        MaterialFloatingToolbarFabPosition.End;

    static XrToolbar()
    {
        OrientationProperty.Changed.AddClassHandler<XrToolbar>((toolbar, _) =>
        {
            toolbar.UpdateOrientation();
            toolbar.UpdateExitTransform();
        });
        IsExpandedProperty.Changed.AddClassHandler<XrToolbar>((toolbar, _) => toolbar.AnimateExpansion());
        ElevationProperty.Changed.AddClassHandler<XrToolbar>((toolbar, _) => toolbar.UpdateElevation());
        ScrollBehaviorProperty.Changed.AddClassHandler<XrToolbar>((toolbar, args) =>
            toolbar.OnScrollBehaviorChanged(args.GetOldValue<MaterialAppBarScrollBehavior?>(),
                args.GetNewValue<MaterialAppBarScrollBehavior?>()));
        ScrollSourceProperty.Changed.AddClassHandler<XrToolbar>((toolbar, _) => toolbar.UpdateAdapter());
    }

    public XrToolbar()
    {
        RenderTransform = _exitTransform;
        UpdateOrientation();
        UpdateElevation();
    }

    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public XrSurfaceRole Elevation { get => GetValue(ElevationProperty); set => SetValue(ElevationProperty, value); }
    public bool ShowFab { get => GetValue(ShowFabProperty); set => SetValue(ShowFabProperty, value); }
    public Geometry? FabIcon { get => GetValue(FabIconProperty); set => SetValue(FabIconProperty, value); }
    public string? FabLabel { get => GetValue(FabLabelProperty); set => SetValue(FabLabelProperty, value); }
    public ICommand? FabCommand { get => GetValue(FabCommandProperty); set => SetValue(FabCommandProperty, value); }
    public object? FabCommandParameter { get => GetValue(FabCommandParameterProperty); set => SetValue(FabCommandParameterProperty, value); }

    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
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

    /// <summary>Animation progress of the common floating-toolbar layout (0 collapsed, 1 expanded).</summary>
    public double ExpandedProgress
    {
        get => _expandedProgress;
        private set => SetAndRaise(ExpandedProgressProperty, ref _expandedProgress, value);
    }

    /// <summary>FAB edge derived from the toolbar axis: End when horizontal, Bottom when vertical.</summary>
    public MaterialFloatingToolbarFabPosition EffectiveFabPosition
    {
        get => _effectiveFabPosition;
        private set => SetAndRaise(EffectiveFabPositionProperty, ref _effectiveFabPosition, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        LayoutUpdated += OnLayoutUpdated;
        UpdateAdapter();
        SubscribeState();
        UpdateExitTransform();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        LayoutUpdated -= OnLayoutUpdated;
        _adapter?.Dispose();
        _adapter = null;
        UnsubscribeState();
        _expandAnimation?.Dispose();
        _expandAnimation = null;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!TryMoveFocus(e))
            base.OnKeyDown(e);
    }

    private void UpdateOrientation()
    {
        var orientation = Orientation;
        ItemsPanel = new FuncTemplate<Panel?>(() => new StackPanel
        {
            Orientation = orientation,
            Spacing = 4
        });
        PseudoClasses.Set(":vertical", orientation == Avalonia.Layout.Orientation.Vertical);
        EffectiveFabPosition = orientation == Avalonia.Layout.Orientation.Vertical
            ? MaterialFloatingToolbarFabPosition.Bottom
            : MaterialFloatingToolbarFabPosition.End;
        InvalidateMeasure();
    }

    private void UpdateElevation()
    {
        PseudoClasses.Set(":elevation-high", Elevation == XrSurfaceRole.SurfaceContainerHigh);
        PseudoClasses.Set(":elevation-highest", Elevation == XrSurfaceRole.SurfaceContainerHighest);
        PseudoClasses.Set(":tertiary-container", Elevation == XrSurfaceRole.TertiaryContainer);
    }

    private void AnimateExpansion()
    {
        _expandAnimation?.Dispose();
        var target = IsExpanded ? 1 : 0;
        _expandAnimation = MaterialMotion.Animate(this, ExpandedProgress, target,
            value => ExpandedProgress = value, MaterialSpring.SpatialFast, 0.001);
    }

    private void OnScrollBehaviorChanged(MaterialAppBarScrollBehavior? oldValue,
        MaterialAppBarScrollBehavior? newValue)
    {
        oldValue?.CancelSettle();
        UnsubscribeState();
        UpdateAdapter();
        SubscribeState();
        UpdateExitTransform();
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
            UpdateExitTransform();
    }

    private void UpdateAdapter()
    {
        _adapter?.Dispose();
        _adapter = null;
        if (ScrollBehavior is not { } behavior)
            return;
        _adapter = new MaterialBarScrollAdapter(this, behavior);
        _adapter.Attach(ScrollSource);
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        UpdateExitLimit();
        UpdateExitTransform();
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

    /// <summary>Moves focus between slots along the toolbar axis, wrapping at the ends.</summary>
    private bool TryMoveFocus(KeyEventArgs e)
    {
        if (e.Handled || e.KeyModifiers != KeyModifiers.None)
            return false;

        var delta = Orientation == Avalonia.Layout.Orientation.Horizontal
            ? e.Key switch { Key.Left => -1, Key.Right => 1, _ => 0 }
            : e.Key switch { Key.Up => -1, Key.Down => 1, _ => 0 };
        if (delta == 0)
            return false;

        // ItemsControl wraps items in non-focusable ContentPresenters, so resolve the first
        // focusable control inside each realized container before moving focus.
        var slots = new List<Control>();
        foreach (var container in GetRealizedContainers())
        {
            var target = container.Focusable
                ? container
                : container.GetVisualDescendants().OfType<Control>().FirstOrDefault(candidate => candidate.Focusable);
            if (target is { IsEffectivelyEnabled: true })
                slots.Add(target);
        }

        if (slots.Count == 0)
            return false;

        var current = slots.FindIndex(slot => IsAncestorOf(slot, e.Source as Visual));
        var next = current < 0 ? (delta > 0 ? 0 : slots.Count - 1) : (current + delta + slots.Count) % slots.Count;
        slots[next].Focus(NavigationMethod.Directional);
        e.Handled = true;
        return true;
    }

    private static bool IsAncestorOf(Control candidate, Visual? source)
    {
        for (var visual = source; visual is not null; visual = visual.GetVisualParent())
        {
            if (ReferenceEquals(visual, candidate))
                return true;
        }

        return false;
    }
}
