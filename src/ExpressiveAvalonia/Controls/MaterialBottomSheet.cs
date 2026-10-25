using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// A Material bottom sheet with an optional drag handle and native drag-to-dismiss.
/// </summary>
/// <remarks>
/// The source anatomy is a full-width panel with 28 rounded top corners, a 36-tall drag-handle
/// header and the scrolling content slot. <see cref="IsModal"/> adds the scrim; the modal
/// presentation drops the resting elevation shadow, matching the source. Dragging the header
/// moves the surface, fades the scrim and dismisses the sheet when the drag passes
/// <see cref="DragDismissThreshold"/> or ends with a downward fling; otherwise it springs back.
/// </remarks>
public sealed class MaterialBottomSheet : MaterialSheet
{
    /// <summary>Identifies the <see cref="ShowDragHandle"/> property.</summary>
    public static readonly StyledProperty<bool> ShowDragHandleProperty =
        AvaloniaProperty.Register<MaterialBottomSheet, bool>(nameof(ShowDragHandle), true);

    /// <summary>Identifies the <see cref="DragDismissThreshold"/> property.</summary>
    public static readonly StyledProperty<double> DragDismissThresholdProperty =
        AvaloniaProperty.Register<MaterialBottomSheet, double>(nameof(DragDismissThreshold), 96d,
            validate: static value => double.IsFinite(value) && value >= 0);

    private const double FlingVelocity = 1000;

    private Control? _dragHandle;
    private ScrollViewer? _contentScroll;
    private bool _dragging;
    private Point _dragStart;
    private double _dragStartOffset;
    private double _dragLastY;
    private ulong _dragLastTimestamp;
    private double _dragVelocity;

    public MaterialBottomSheet() => LayoutUpdated += OnSheetLayoutUpdated;

    /// <summary>Shows the 36-tall drag-handle header.</summary>
    public bool ShowDragHandle
    {
        get => GetValue(ShowDragHandleProperty);
        set => SetValue(ShowDragHandleProperty, value);
    }

    /// <summary>
    /// The downward distance, in device-independent units, at which a released drag dismisses the
    /// sheet. The default is 96. A downward fling faster than 1000 units per second also dismisses.
    /// </summary>
    public double DragDismissThreshold
    {
        get => GetValue(DragDismissThresholdProperty);
        set => SetValue(DragDismissThresholdProperty, value);
    }

    protected override double GetClosedOffset()
    {
        var height = SheetSurface?.Bounds.Height ?? 0;
        if (height <= 0)
            height = Bounds.Height > 0 ? Bounds.Height : 480;

        return height;
    }

    protected override void ApplyOffset(double offset)
    {
        if (SheetTransform is null)
            return;

        SheetTransform.X = 0;
        SheetTransform.Y = offset;
    }

    protected override string? GetSheetAccessibleName() =>
        this.TryFindResource("MaterialBottomSheetText", out var value) && value is string text
            ? text
            : "Bottom sheet";

    protected override void OnSheetTemplateApplied(TemplateAppliedEventArgs e)
    {
        if (_dragHandle is not null)
        {
            _dragHandle.PointerPressed -= OnDragHandlePointerPressed;
            _dragHandle.PointerMoved -= OnDragHandlePointerMoved;
            _dragHandle.PointerReleased -= OnDragHandlePointerReleased;
            _dragHandle.PointerCaptureLost -= OnDragHandlePointerCaptureLost;
        }

        _dragHandle = e.NameScope.Find<Control>("PART_DragHandle");
        _contentScroll = e.NameScope.Find<ScrollViewer>("PART_ContentScroll");

        if (_dragHandle is not null)
        {
            _dragHandle.Cursor = new Cursor(StandardCursorType.SizeNorthSouth);
            _dragHandle.PointerPressed += OnDragHandlePointerPressed;
            _dragHandle.PointerMoved += OnDragHandlePointerMoved;
            _dragHandle.PointerReleased += OnDragHandlePointerReleased;
            _dragHandle.PointerCaptureLost += OnDragHandlePointerCaptureLost;
        }

        UpdateContentMaxHeight();
        UpdateAccessibleName();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsOpenProperty && !IsOpen)
        {
            _dragging = false;
        }
        else if (change.Property == ShowDragHandleProperty || change.Property == ShowActionsProperty)
        {
            UpdateContentMaxHeight();
        }
    }

    private void OnSheetLayoutUpdated(object? sender, EventArgs e)
    {
        if (IsOpen)
            UpdateContentMaxHeight();
    }

    private void UpdateContentMaxHeight()
    {
        // Before the first layout pass the height is unknown; leave the scroll viewer unconstrained
        // rather than collapsing the content to zero for a frame.
        if (_contentScroll is null || Bounds.Height <= 0)
            return;

        var available = Bounds.Height - (_dragHandle?.Bounds.Height ?? 0) - (ActionsRegion?.Bounds.Height ?? 0);
        available = Math.Max(0, available);

        // Only write when the value changes; this handler runs from LayoutUpdated.
        if (Math.Abs(_contentScroll.MaxHeight - available) > 0.5)
            _contentScroll.MaxHeight = available;
    }

    private void OnDragHandlePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsOpen || !IsDismissEnabled || SheetTransform is null || _dragHandle is null)
            return;

        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed && e.Pointer.Type != PointerType.Touch)
            return;

        _dragging = true;
        _dragStart = point.Position;
        _dragStartOffset = CurrentOffset;
        _dragLastY = point.Position.Y;
        _dragLastTimestamp = e.Timestamp;
        _dragVelocity = 0;

        ClearSlideTransition();
        SetScrimTransition(TimeSpan.Zero);
        e.Pointer.Capture(_dragHandle);
        e.Handled = true;
    }

    private void OnDragHandlePointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_dragging || SheetTransform is null)
            return;

        var y = e.GetPosition(this).Y;
        var height = SheetSurface?.Bounds.Height ?? 0;
        if (height <= 0)
            height = Bounds.Height;
        if (height <= 0)
            height = 1;

        var offset = Math.Clamp(_dragStartOffset + (y - _dragStart.Y), 0, height);
        SetSheetOffset(offset);

        if (IsModal && ScrimOpacity > 0)
            SetScrimOpacity(ScrimOpacity * Math.Max(0, 1 - offset / height));

        var elapsed = e.Timestamp - _dragLastTimestamp;
        if (elapsed > 0)
            _dragVelocity = (y - _dragLastY) / (elapsed / 1000.0);
        _dragLastY = y;
        _dragLastTimestamp = e.Timestamp;
        e.Handled = true;
    }

    private void OnDragHandlePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_dragging)
            return;

        var offset = CurrentOffset;
        EndDrag(dismiss: offset >= DragDismissThreshold || _dragVelocity > FlingVelocity);
        e.Handled = true;
    }

    private void OnDragHandlePointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (!_dragging)
            return;

        EndDrag(dismiss: false);
    }

    private void EndDrag(bool dismiss)
    {
        if (!_dragging)
            return;

        _dragging = false;
        if (dismiss)
        {
            SetCurrentValue(IsOpenProperty, false);
            return;
        }

        if (SheetTransform is null)
            return;

        var duration = GetOpenDuration();
        SetSlideTransition(duration);
        SetSheetOffset(0);
        SetScrimTransition(duration);
        SetScrimOpacity(IsModal ? ScrimOpacity : 0);
    }
}
