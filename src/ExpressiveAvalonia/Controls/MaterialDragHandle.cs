using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Controls;

/// <summary>The drag payload reported by <see cref="MaterialDragHandle"/>.</summary>
public sealed class MaterialDragEventArgs : RoutedEventArgs
{
    internal MaterialDragEventArgs(RoutedEvent? routedEvent, Vector offset, Vector delta, bool isCanceled)
        : base(routedEvent)
    {
        Offset = offset;
        Delta = delta;
        IsCanceled = isCanceled;
    }

    /// <summary>Total displacement from the pointer press position.</summary>
    public Vector Offset { get; }

    /// <summary>Displacement since the previous drag event.</summary>
    public Vector Delta { get; }

    /// <summary>Whether the gesture ended because the pointer was lost rather than released.</summary>
    public bool IsCanceled { get; }
}

/// <summary>The standalone vertical drag handle from DragHandle.kt.</summary>
/// <remarks>
/// <para>
/// Source-derived geometry: the pill is 4 by 48 DIP in the resting outline color, and 12 by 52
/// DIP in the on-surface color with the medium corner while pressed or dragged. The 22 DIP
/// vertical padding around the pill is part of the source composable's touch affordance; hosts
/// that place this control directly should provide that padding in their layout. The control owns
/// the pointer gesture and reports it; it never moves or dismisses a surface itself, so hosts
/// decide whether a drag dismisses them. Press, move and release are captured by the handle, and a
/// lost capture raises <see cref="DragCompleted"/> with
/// <see cref="MaterialDragEventArgs.IsCanceled"/> set.
/// </para>
/// <para>
/// Interaction follows the source's <c>.hoverable(interactionSource)</c> and
/// <c>.indication(interactionSource, ripple())</c>: the shared <see cref="MaterialRipple"/> paints
/// the hover/focus/drag state layer and the bounded press ripple. Its bounds and corner radius are
/// updated with the same snapped silhouette as the pill, so the indication is always clipped to
/// the current 4x48, 12x52 or corner-medium shape. Like the source handle, geometry snaps between
/// those states; no geometry spring is added here.
/// </para>
/// <para>
/// This is the generic handle, not the 32 by 4 handle embedded in the bottom-sheet header (see
/// <see cref="MaterialBottomSheet"/> and SheetBottomTokens). It is decorative for assistive
/// technology (<see cref="AccessibilityView.Raw"/>); hosts must offer a keyboard or button
/// equivalent for dismissal and expansion, as the bottom sheet does on its embedded handle.
/// </para>
/// </remarks>
[PseudoClasses(":dragging", ":pressed")]
public sealed class MaterialDragHandle : TemplatedControl
{
    /// <summary>Raised when a drag gesture starts.</summary>
    public static readonly RoutedEvent<MaterialDragEventArgs> DragStartedEvent =
        RoutedEvent.Register<MaterialDragHandle, MaterialDragEventArgs>(
            nameof(DragStarted), RoutingStrategies.Bubble);

    /// <summary>Raised for every pointer move while dragging.</summary>
    public static readonly RoutedEvent<MaterialDragEventArgs> DragDeltaEvent =
        RoutedEvent.Register<MaterialDragHandle, MaterialDragEventArgs>(
            nameof(DragDelta), RoutingStrategies.Bubble);

    /// <summary>Raised when the drag gesture ends.</summary>
    public static readonly RoutedEvent<MaterialDragEventArgs> DragCompletedEvent =
        RoutedEvent.Register<MaterialDragHandle, MaterialDragEventArgs>(
            nameof(DragCompleted), RoutingStrategies.Bubble);

    private Point _origin;
    private Point _last;
    private bool _dragging;
    private Border? _handle;
    private MaterialRipple? _stateLayer;
    private IDisposable? _backgroundBinding;

    public MaterialDragHandle()
    {
        Focusable = false;
        IsTabStop = false;
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
        AutomationProperties.SetControlTypeOverride(this, AutomationControlType.Custom);
        HorizontalAlignment = HorizontalAlignment.Stretch;
        MinHeight = 48;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _handle = e.NameScope.Find<Border>("PART_Handle");
        _stateLayer = e.NameScope.Find<MaterialRipple>("PART_StateLayer");
        ApplyHandleState(pressed: false);
    }

    private void ApplyHandleState(bool pressed)
    {
        if (_handle is null)
            return;

        _handle.Width = pressed ? 12 : 4;
        _handle.Height = pressed ? 52 : 48;
        _handle.CornerRadius = new CornerRadius(pressed ? 12 : _handle.Height / 2);

        // The source indication is bounded by the same node shape that clips the pill. Keep the
        // ripple's silhouette in lockstep with the snapped size and corner so the press ripple
        // never paints outside the current handle.
        if (_stateLayer is not null)
        {
            _stateLayer.Width = _handle.Width;
            _stateLayer.Height = _handle.Height;
            _stateLayer.CornerRadius = _handle.CornerRadius;
        }

        _backgroundBinding?.Dispose();
        _backgroundBinding = _handle.Bind(Border.BackgroundProperty,
            _handle.GetResourceObservable(pressed
                ? "MaterialOnSurfaceBrush"
                : "MaterialOutlineBrush"));
    }

    /// <summary>Raised when a drag gesture starts.</summary>
    public event EventHandler<MaterialDragEventArgs>? DragStarted
    {
        add => AddHandler(DragStartedEvent, value);
        remove => RemoveHandler(DragStartedEvent, value);
    }

    /// <summary>Raised for every pointer move while dragging.</summary>
    public event EventHandler<MaterialDragEventArgs>? DragDelta
    {
        add => AddHandler(DragDeltaEvent, value);
        remove => RemoveHandler(DragDeltaEvent, value);
    }

    /// <summary>Raised when the drag gesture ends.</summary>
    public event EventHandler<MaterialDragEventArgs>? DragCompleted
    {
        add => AddHandler(DragCompletedEvent, value);
        remove => RemoveHandler(DragCompletedEvent, value);
    }

    /// <summary>Whether a pointer drag is in progress.</summary>
    public bool IsDragging => _dragging;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Handled || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        _origin = _last = e.GetPosition(this);
        _dragging = true;
        PseudoClasses.Set(":pressed", true);
        PseudoClasses.Set(":dragging", true);
        ApplyHandleState(pressed: true);
        if (_stateLayer is not null)
            _stateLayer.IsDragging = true;
        e.Pointer.Capture(this);
        RaiseEvent(new MaterialDragEventArgs(DragStartedEvent, default, default, false));
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_dragging)
            return;

        var position = e.GetPosition(this);
        var delta = position - _last;
        _last = position;
        RaiseEvent(new MaterialDragEventArgs(DragDeltaEvent, position - _origin, delta, false));
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_dragging)
            return;

        EndDrag(e.GetPosition(this) - _origin, canceled: false);
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (!_dragging)
            return;

        EndDrag(_last - _origin, canceled: true);
    }

    private void EndDrag(Vector offset, bool canceled)
    {
        _dragging = false;
        PseudoClasses.Set(":dragging", false);
        PseudoClasses.Set(":pressed", false);
        if (_stateLayer is not null)
            _stateLayer.IsDragging = false;
        ApplyHandleState(pressed: false);
        RaiseEvent(new MaterialDragEventArgs(DragCompletedEvent, offset, default, canceled));
    }
}
