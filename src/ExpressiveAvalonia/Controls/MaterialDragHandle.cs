using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;

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

/// <summary>The grabber used to drag a navigation surface such as a modal bottom sheet.</summary>
/// <remarks>
/// Matches the source building block: a 108 by 4 DIP pill with a 12 DIP radius centered in a
/// 24 DIP grab area. The control owns the pointer gesture and reports it; it never moves or
/// dismisses a surface itself, so hosts decide whether a drag dismisses them. Press, move and
/// release are captured by the handle, and a lost capture raises
/// <see cref="DragCompleted"/> with <see cref="MaterialDragEventArgs.IsCanceled"/> set.
/// The handle is decorative for assistive technology: hosts should offer a button equivalent
/// for dismissal.
/// </remarks>
[PseudoClasses(":dragging")]
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

    public MaterialDragHandle()
    {
        Focusable = false;
        IsTabStop = false;
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
        AutomationProperties.SetControlTypeOverride(this, AutomationControlType.Custom);
        HorizontalAlignment = HorizontalAlignment.Stretch;
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
        PseudoClasses.Set(":dragging", true);
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
        RaiseEvent(new MaterialDragEventArgs(DragCompletedEvent, offset, default, canceled));
    }
}
