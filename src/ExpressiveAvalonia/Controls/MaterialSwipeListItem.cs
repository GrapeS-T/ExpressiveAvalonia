using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Windows.Input;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Controls;

/// <summary>The settled value of a <see cref="MaterialSwipeListItem"/> swipe box.</summary>
public enum MaterialSwipeState
{
    /// <summary>The row is in its resting position.</summary>
    Settled,

    /// <summary>The row settled toward the end edge (right in left-to-right flow).</summary>
    StartToEnd,

    /// <summary>The row settled toward the start edge (left in left-to-right flow).</summary>
    EndToStart,
}

/// <summary>
/// A native list item with the Material 3 swipe-to-dismiss behavior of
/// <c>SwipeToDismissBox</c>.
/// </summary>
/// <remarks>
/// <para>
/// Anchors are
/// <see cref="MaterialSwipeState.Settled"/> at 0 and, when enabled, one full row width in each
/// dismiss direction. Release picks the target with the source algorithm: an absolute velocity of
/// at least <see cref="VelocityThreshold"/> (125 DIP/s) selects the next anchor in the velocity
/// direction; otherwise the absolute <see cref="PositionalThreshold"/> (56 DIP) decides between
/// the two anchors around the current offset. Settling uses exponential decay when the decay can
/// reach the target and otherwise the default 300 ms fast-out-slow-in tween; reduced motion snaps.
/// A cancelled or vetoed pending state returns to the previously settled anchor.
/// </para>
/// <para>
/// <see cref="ConfirmValueChange"/> mirrors the source <c>confirmValueChange</c> callback: it runs
/// before a non-settled target is applied and can veto it. <see cref="Dismissed"/> is raised only
/// after the row has settled in a dismissed direction, mirroring <c>onDismiss</c>. The control
/// never removes or hides the item itself: the application owns the data change, so a pending
/// dismiss can never delete a row before confirmation. <see cref="Reset"/> and
/// <see cref="Dismiss"/> are the source state methods.
/// </para>
/// <para>
/// The row keeps the native ListBoxItem selection, keyboard and virtualization behavior from
/// <c>Collections.axaml</c>. <see cref="BackgroundContent"/> is the source background slot and is
/// exposed under the foreground row; it is mirrored to an attached property so the shared
/// ListBoxItem template can present it for any recycled container. Containers reused by
/// virtualization reset offset, target and animations on template application and on a
/// DataContext change, so a recycled row never keeps another item's swipe offset.
/// </para>
/// <para>
/// <c>SwipeToDismissBox</c> anchors are physical: <see cref="MaterialSwipeState.StartToEnd"/> is a
/// positive x offset in both layout directions. Avalonia mirrors the right-to-left plane once at
/// the flow-direction boundary (<c>Visual.HasMirrorTransform</c>), so the content's render
/// translation is the negation of the source offset in RTL. <c>DirectionSign</c> performs that
/// single conversion: the stored offset stays in the mirrored local space used by the pointer
/// deltas, and the rendered movement matches the source anchors exactly.
/// </para>
/// </remarks>
[TemplatePart("PART_SwipeBackground", typeof(ContentPresenter))]
[TemplatePart("PART_SwipeContent", typeof(Border))]
[PseudoClasses(":swipe-settled", ":swipe-start-to-end", ":swipe-end-to-start", ":swiping")]
public class MaterialSwipeListItem : ListBoxItem
{
    /// <summary>Defines the <see cref="BackgroundContent"/> attached property.</summary>
    public static readonly AttachedProperty<object?> BackgroundContentProperty =
        AvaloniaProperty.RegisterAttached<MaterialSwipeListItem, Control, object?>("BackgroundContent");

    /// <summary>Defines the <see cref="BackgroundTemplate"/> attached property.</summary>
    public static readonly AttachedProperty<IDataTemplate?> BackgroundTemplateProperty =
        AvaloniaProperty.RegisterAttached<MaterialSwipeListItem, Control, IDataTemplate?>("BackgroundTemplate");

    /// <summary>Enables dismissing toward the end edge.</summary>
    public static readonly StyledProperty<bool> EnableDismissFromStartToEndProperty =
        AvaloniaProperty.Register<MaterialSwipeListItem, bool>(nameof(EnableDismissFromStartToEnd), true);

    /// <summary>Enables dismissing toward the start edge.</summary>
    public static readonly StyledProperty<bool> EnableDismissFromEndToStartProperty =
        AvaloniaProperty.Register<MaterialSwipeListItem, bool>(nameof(EnableDismissFromEndToStart), true);

    /// <summary>Whether pointer gestures are accepted.</summary>
    public static readonly StyledProperty<bool> GesturesEnabledProperty =
        AvaloniaProperty.Register<MaterialSwipeListItem, bool>(nameof(GesturesEnabled), true);

    /// <summary>Absolute distance from the previous anchor that commits the next anchor.</summary>
    public static readonly StyledProperty<double> PositionalThresholdProperty =
        AvaloniaProperty.Register<MaterialSwipeListItem, double>(nameof(PositionalThreshold), 56,
            validate: static value => double.IsFinite(value) && value >= 0);

    /// <summary>Absolute release velocity that commits the next anchor, in DIP per second.</summary>
    public static readonly StyledProperty<double> VelocityThresholdProperty =
        AvaloniaProperty.Register<MaterialSwipeListItem, double>(nameof(VelocityThreshold), 125,
            validate: static value => double.IsFinite(value) && value >= 0);

    /// <summary>Vetoes a pending state change; mirrors the source <c>confirmValueChange</c>.</summary>
    public static readonly StyledProperty<Func<MaterialSwipeState, bool>?> ConfirmValueChangeProperty =
        AvaloniaProperty.Register<MaterialSwipeListItem, Func<MaterialSwipeState, bool>?>(nameof(ConfirmValueChange));

    /// <summary>The settled swipe state.</summary>
    public static readonly StyledProperty<MaterialSwipeState> SwipeStateProperty =
        AvaloniaProperty.Register<MaterialSwipeListItem, MaterialSwipeState>(nameof(SwipeState));

    /// <summary>The state the row is currently settling toward.</summary>
    public static readonly StyledProperty<MaterialSwipeState> TargetSwipeStateProperty =
        AvaloniaProperty.Register<MaterialSwipeListItem, MaterialSwipeState>(nameof(TargetSwipeState));

    private static readonly Func<double, double> SnapEasing = new SplineEasing(0.4, 0, 0.2, 1).Ease;

    /// <summary>
    /// <c>AnchoredDraggableDefaults.SnapAnimationSpec</c> is a default <c>tween()</c>, i.e. 300 ms
    /// with the FastOutSlowIn easing curve that <see cref="SnapEasing"/> reproduces.
    /// </summary>
    private const double SettleSeconds = 0.3;

    private readonly List<(double Seconds, double X)> _samples = new(8);
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly AvaloniaList<MaterialSwipeAction> _actions = [];
    private Control? _actionsBackground;
    private Border? _content;
    private TranslateTransform? _translate;
    private MaterialMotion.TweenAnimation? _tween;
    private MaterialMotion.DecayAnimation? _decay;
    private bool _tracking;
    private bool _dragging;
    private double _pressX;
    private double _pressOffset;
    private double _offset;
    private double _width;
    private double _velocity;
    private bool _keyboardActionsRevealed;

    public MaterialSwipeListItem()
    {
        _actions.CollectionChanged += (_, _) => RebuildActionsBackground();
        AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerCaptureLostEvent, OnPointerCaptureLost, RoutingStrategies.Tunnel, handledEventsToo: true);
        UpdatePseudoClasses(MaterialSwipeState.Settled);
    }

    /// <summary>Raised after the row settles in a dismissed direction.</summary>
    public event EventHandler<MaterialSwipeDismissedEventArgs>? Dismissed;

    /// <summary>Reuses the shared ListBoxItem theme and selection model.</summary>
    protected override Type StyleKeyOverride => typeof(ListBoxItem);

    /// <summary>Background slot exposed under the swiping foreground row.</summary>
    public object? BackgroundContent
    {
        get => GetValue(BackgroundContentProperty);
        set => SetValue(BackgroundContentProperty, value);
    }

    /// <summary>Template for <see cref="BackgroundContent"/>.</summary>
    public IDataTemplate? BackgroundTemplate
    {
        get => GetValue(BackgroundTemplateProperty);
        set => SetValue(BackgroundTemplateProperty, value);
    }

    public static object? GetBackgroundContent(Control control) => control.GetValue(BackgroundContentProperty);

    public static void SetBackgroundContent(Control control, object? value) =>
        control.SetValue(BackgroundContentProperty, value);

    public static IDataTemplate? GetBackgroundTemplate(Control control) => control.GetValue(BackgroundTemplateProperty);

    public static void SetBackgroundTemplate(Control control, IDataTemplate? value) =>
        control.SetValue(BackgroundTemplateProperty, value);

    public bool EnableDismissFromStartToEnd
    {
        get => GetValue(EnableDismissFromStartToEndProperty);
        set => SetValue(EnableDismissFromStartToEndProperty, value);
    }

    public bool EnableDismissFromEndToStart
    {
        get => GetValue(EnableDismissFromEndToStartProperty);
        set => SetValue(EnableDismissFromEndToStartProperty, value);
    }

    public bool GesturesEnabled
    {
        get => GetValue(GesturesEnabledProperty);
        set => SetValue(GesturesEnabledProperty, value);
    }

    public double PositionalThreshold
    {
        get => GetValue(PositionalThresholdProperty);
        set => SetValue(PositionalThresholdProperty, value);
    }

    public double VelocityThreshold
    {
        get => GetValue(VelocityThresholdProperty);
        set => SetValue(VelocityThresholdProperty, value);
    }

    public Func<MaterialSwipeState, bool>? ConfirmValueChange
    {
        get => GetValue(ConfirmValueChangeProperty);
        set => SetValue(ConfirmValueChangeProperty, value);
    }

    /// <summary>The settled state.</summary>
    public MaterialSwipeState SwipeState
    {
        get => GetValue(SwipeStateProperty);
        private set => SetValue(SwipeStateProperty, value);
    }

    /// <summary>The state the row is settling toward.</summary>
    public MaterialSwipeState TargetSwipeState
    {
        get => GetValue(TargetSwipeStateProperty);
        private set => SetValue(TargetSwipeStateProperty, value);
    }

    /// <summary>
    /// Current translation in DIP, applied inside the mirrored coordinate space: a positive value
    /// moves toward the physical right in left-to-right flow and toward the physical left in
    /// right-to-left flow, because Avalonia negates the whole plane once for RTL.
    /// </summary>
    public double SwipeOffset => _offset;

    /// <summary>
    /// Current dismiss direction based on the offset, like <c>dismissDirection</c>. The source
    /// anchors are physical, so <see cref="MaterialSwipeState.StartToEnd"/> is the positive
    /// source offset, i.e. the negated local translation under the RTL mirror.
    /// </summary>
    public MaterialSwipeState DismissDirection =>
        Math.Abs(_offset) < 0.01 ? MaterialSwipeState.Settled
        : _offset * DirectionSign > 0 ? MaterialSwipeState.StartToEnd
        : MaterialSwipeState.EndToStart;

    /// <summary>
    /// Extension: actions rendered in the source background slot. Mutations rebuild the
    /// background composition; an explicit <see cref="BackgroundContent"/> is never overwritten.
    /// This is presentation only, not a second swipe state machine.
    /// </summary>
    public IList<MaterialSwipeAction> SwipeActions => _actions;

    /// <summary>Extension: raised when a background action button is invoked.</summary>
    public event EventHandler<MaterialSwipeActionInvokedEventArgs>? ActionInvoked;

    /// <summary>Resets to the settled anchor with the source animation.</summary>
    public void Reset() => AnimateTo(MaterialSwipeState.Settled, velocity: 0);

    /// <summary>Dismisses toward <paramref name="direction"/> with the source animation.</summary>
    public void Dismiss(MaterialSwipeState direction)
    {
        if (direction == MaterialSwipeState.Settled || !IsDirectionEnabled(direction))
            return;
        if (!Confirm(direction))
            return;
        AnimateTo(direction, velocity: 0);
    }

    /// <summary>Jumps to an anchor without animation, as <c>snapTo</c>.</summary>
    public void SnapTo(MaterialSwipeState state)
    {
        StopAnimations();
        SetOffsetImmediate(OffsetFor(state));
        SetSettled(state, raise: false);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        StopAnimations();
        _content = e.NameScope.Find<Border>("PART_SwipeContent");
        _translate = new TranslateTransform();
        if (_content is not null)
            _content.RenderTransform = _translate;
        _width = Bounds.Width;
        if (_dragging)
            SetOffsetImmediate(ClampToAnchors(_offset));
        else
            CompleteInterruptedSettle();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty)
        {
            var width = Bounds.Width;
            // The source rebuilds DraggableAnchors from the measured width on every layout pass,
            // including a collapse to zero, and reconciles ongoing settle work against the new
            // target. Updating the offset alone would let the old animation finish at the old width.
            if (Math.Abs(width - _width) > 0.01)
            {
                _width = width;
                ReconcileAnchors();
            }
        }
        else if (change.Property == DataContextProperty || change.Property == ContentProperty)
        {
            // Recycled containers must not keep another item's offset, target or animation.
            ResetForReuse();
        }
        else if (change.Property == FlowDirectionProperty)
        {
            // Every logical anchor mirrors with the flow direction; keep an in-flight settle on
            // the mirrored position of its target instead of the stale captured endpoint.
            if (_keyboardActionsRevealed && _actionsBackground is StackPanel actionsPanel)
                SetOffsetImmediate((EnableDismissFromEndToStart ? -1 : 1) * DirectionSign * Math.Min(Bounds.Width, actionsPanel.Bounds.Width));
            else if (!_dragging)
                ReconcileAnchors();
        }
        else if (change.Property == EnableDismissFromStartToEndProperty ||
                 change.Property == EnableDismissFromEndToStartProperty)
        {
            // The source recomposes draggableAnchors when the flags change; a settled or settling
            // direction that no longer exists falls back to the settled anchor.
            if (!_dragging &&
                ((SwipeState != MaterialSwipeState.Settled && !IsDirectionEnabled(SwipeState)) ||
                 (TargetSwipeState != MaterialSwipeState.Settled && !IsDirectionEnabled(TargetSwipeState))))
                SnapTo(MaterialSwipeState.Settled);
            if (_actionsBackground is StackPanel panel && ReferenceEquals(BackgroundContent, panel))
                panel.HorizontalAlignment = EnableDismissFromEndToStart
                    ? Avalonia.Layout.HorizontalAlignment.Right
                    : Avalonia.Layout.HorizontalAlignment.Left;
            if (_keyboardActionsRevealed && _actionsBackground is StackPanel actionsPanel)
                SetOffsetImmediate((EnableDismissFromEndToStart ? -1 : 1) * DirectionSign * Math.Min(Bounds.Width, actionsPanel.Bounds.Width));
        }
        else if (change.Property == IsEnabledProperty && !change.GetNewValue<bool>())
        {
            SnapTo(MaterialSwipeState.Settled);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // A container that was removed mid-transition reattaches with resolved anchors, not with a
        // partial offset left behind by the cancelled frame source.
        if (_dragging)
            SetOffsetImmediate(ClampToAnchors(_offset));
        else
            ReconcileAnchors();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _tracking = false;
        _dragging = false;
        PseudoClasses.Set(":swiping", false);
        _samples.Clear();
        _velocity = 0;
        _pressX = 0;
        _pressOffset = 0;
        // Removal leaves no frame source; carry an interrupted settle to its target anchor so a
        // retained container never resumes from a partial offset.
        CompleteInterruptedSettle();
    }

    private void RebuildActionsBackground()
    {
        if (_actions.Count == 0)
        {
            if (ReferenceEquals(BackgroundContent, _actionsBackground))
                BackgroundContent = null;
            _actionsBackground = null;
            return;
        }

        var alignEnd = EnableDismissFromEndToStart;
        var panel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = alignEnd
                ? Avalonia.Layout.HorizontalAlignment.Right
                : Avalonia.Layout.HorizontalAlignment.Left,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
            Spacing = 2,
        };

        for (var index = 0; index < _actions.Count; index++)
        {
            var action = _actions[index];
            var button = new Button
            {
                Width = action.IsPrimary ? 120 : 80,
                MinWidth = 0,
                MinHeight = 0,
                Padding = default,
                BorderThickness = default,
                CornerRadius = default,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
                Background = action.Background
                    ?? ResolveBrush("MaterialErrorContainerBrush", Colors.DarkRed),
                Foreground = action.Foreground
                    ?? ResolveBrush("MaterialOnErrorContainerBrush", Colors.White),
                Content = action.Icon is null
                    ? null
                    : new PathIcon { Data = action.Icon, Width = 24, Height = 24 },
                Tag = index,
            };
            AutomationProperties.SetName(button, action.Label ?? "Action");
            button.Click += OnActionClick;
            button.GotFocus += (_, _) =>
            {
                if (SwipeState == MaterialSwipeState.Settled && Math.Abs(_offset) < 0.01)
                {
                    _keyboardActionsRevealed = true;
                    var width = Math.Min(Bounds.Width, panel.Bounds.Width);
                    SetOffsetImmediate((EnableDismissFromEndToStart ? -1 : 1) * DirectionSign * width);
                }
            };
            button.LostFocus += (_, _) =>
            {
                if (_keyboardActionsRevealed)
                {
                    _keyboardActionsRevealed = false;
                    SetOffsetImmediate(0);
                }
            };
            panel.Children.Add(button);
        }

        _actionsBackground = panel;
        BackgroundContent = panel;
    }

    private void OnActionClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: int index } || index < 0 || index >= _actions.Count)
            return;
        var action = _actions[index];
        if (action.Command?.CanExecute(action.CommandParameter) == true)
            action.Command.Execute(action.CommandParameter);
        ActionInvoked?.Invoke(this, new MaterialSwipeActionInvokedEventArgs(action, index, bySwipe: false));
        e.Handled = true;
    }

    private IBrush ResolveBrush(string key, Color fallback) =>
        this.TryFindResource(key, out var value) && value is IBrush brush
            ? brush
            : new SolidColorBrush(fallback);

    private void ResetForReuse()
    {
        StopAnimations();
        _tracking = false;
        _dragging = false;
        PseudoClasses.Set(":swiping", false);
        _samples.Clear();
        _velocity = 0;
        _pressX = 0;
        _pressOffset = 0;
        SetOffsetImmediate(0);
        TargetSwipeState = MaterialSwipeState.Settled;
        SetSettled(MaterialSwipeState.Settled, raise: false);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // The source installs the drag modifier only while the box is settled; a dismissed row
        // must be reset before it accepts another gesture. Programmatic Reset/Dismiss stay usable.
        if (!GesturesEnabled || SwipeState != MaterialSwipeState.Settled ||
            !IsEffectivelyEnabled || _content is null)
            return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        if (e.Source is Visual source && FindInteractive(source) is not null)
            return;

        StopAnimations();
        _tracking = true;
        _dragging = false;
        _pressX = e.GetPosition(this).X;
        _pressOffset = _offset;
        _samples.Clear();
        AddSample(_pressX);
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_tracking || _content is null)
            return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _tracking = false;
            return;
        }

        var point = e.GetPosition(this);
        var dx = point.X - _pressX;
        if (!_dragging)
        {
            if (Math.Abs(dx) < 3)
                return;
            _dragging = true;
            PseudoClasses.Set(":swiping", true);
            e.Pointer.Capture(this);
        }

        AddSample(point.X);
        // The pointer delta is already in the coordinate space of the translated content, so the
        // logical directions (and their RTL mirroring) are applied exactly once, by OffsetFor.
        var raw = _pressOffset + dx;
        // Anchors are clamped with no overscroll, like DraggableAnchors.
        SetOffsetImmediate(ClampToAnchors(raw));
        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_tracking)
            return;
        var dragged = _dragging;
        _tracking = false;
        _dragging = false;
        PseudoClasses.Set(":swiping", false);
        if (!dragged)
            return;

        e.Pointer.Capture(null);
        e.Handled = true;

        _velocity = ComputeVelocity();
        var target = ComputeTarget(_offset, _velocity);
        if (target != MaterialSwipeState.Settled && !Confirm(target))
            target = SwipeState;
        AnimateTo(target, _velocity);
    }

    private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (!_tracking && !_dragging)
            return;
        _tracking = false;
        _dragging = false;
        PseudoClasses.Set(":swiping", false);
        // An interrupted drag returns to the settled anchor rather than keeping a foreign offset.
        AnimateTo(MaterialSwipeState.Settled, velocity: 0);
    }

    private bool Confirm(MaterialSwipeState state) =>
        ConfirmValueChange?.Invoke(state) ?? true;

    private bool IsDirectionEnabled(MaterialSwipeState state) => state switch
    {
        MaterialSwipeState.StartToEnd => EnableDismissFromStartToEnd,
        MaterialSwipeState.EndToStart => EnableDismissFromEndToStart,
        _ => true,
    };

    private double DirectionSign => FlowDirection == FlowDirection.RightToLeft ? -1 : 1;

    private double OffsetFor(MaterialSwipeState state)
    {
        var width = _width > 0 ? _width : Bounds.Width;
        return state switch
        {
            MaterialSwipeState.StartToEnd => DirectionSign * width,
            MaterialSwipeState.EndToStart => -DirectionSign * width,
            _ => 0,
        };
    }

    /// <summary>
    /// The draggable interval built from the same anchors as <see cref="OffsetFor"/> and
    /// <see cref="ComputeTarget"/>, so a gesture can reach every enabled anchor in either flow
    /// direction.
    /// </summary>
    private (double Min, double Max) DragInterval()
    {
        var min = 0d;
        var max = 0d;
        if (EnableDismissFromStartToEnd)
        {
            var anchor = OffsetFor(MaterialSwipeState.StartToEnd);
            min = Math.Min(min, anchor);
            max = Math.Max(max, anchor);
        }
        if (EnableDismissFromEndToStart)
        {
            var anchor = OffsetFor(MaterialSwipeState.EndToStart);
            min = Math.Min(min, anchor);
            max = Math.Max(max, anchor);
        }
        return (min, max);
    }

    private double ClampToAnchors(double offset)
    {
        var (min, max) = DragInterval();
        return Math.Clamp(offset, min, max);
    }

    /// <summary>
    /// Rebuilds the anchors from the current measured width and reconciles the row with the source
    /// <c>DraggableAnchors</c> update rules: snap when nothing owns the offset, otherwise retarget
    /// or restart the ongoing settle so its completion uses the latest anchor, including zero
    /// width. Without this the old animation would finish at the width captured before the resize.
    /// </summary>
    private void ReconcileAnchors()
    {
        var target = TargetSwipeState;
        if (_dragging)
        {
            SetOffsetImmediate(ClampToAnchors(_offset));
            return;
        }

        if (target != MaterialSwipeState.Settled && !IsDirectionEnabled(target))
        {
            SnapTo(MaterialSwipeState.Settled);
            return;
        }

        if (_tween is not null)
        {
            // The tween callbacks read the anchor on every frame, so retargeting reconciles both
            // the position being animated and the value that marks completion.
            _tween.Retarget(OffsetFor(target));
            return;
        }

        if (_decay is not null)
        {
            // A decay keeps the trajectory it started with; restart the settle against the new
            // anchors from the current offset unless the target is the fixed settled anchor.
            if (target == MaterialSwipeState.Settled)
                return;
            AnimateTo(target, _velocity);
            return;
        }

        SetOffsetImmediate(OffsetFor(target));
    }

    /// <summary>Carries an interrupted transition to its target anchor without raising events.</summary>
    private void CompleteInterruptedSettle()
    {
        StopAnimations();
        var target = TargetSwipeState;
        if (target != MaterialSwipeState.Settled && !IsDirectionEnabled(target))
            target = MaterialSwipeState.Settled;
        SetOffsetImmediate(OffsetFor(target));
        // A removed row is not a new settle for the application, so Dismissed stays silent.
        SetSettled(target, raise: false);
    }

    private void SetOffsetImmediate(double offset)
    {
        _offset = offset;
        if (_translate is not null)
            _translate.X = offset;
    }

    private void AddSample(double x)
    {
        var seconds = _clock.Elapsed.TotalSeconds;
        _samples.Add((seconds, x));
        while (_samples.Count > 2 && seconds - _samples[0].Seconds > 0.1)
            _samples.RemoveAt(0);
    }

    /// <summary>Velocity from the last 100 ms of samples, in DIP per second.</summary>
    private double ComputeVelocity()
    {
        if (_samples.Count < 2)
            return 0;
        var first = _samples[0];
        var last = _samples[^1];
        var dt = last.Seconds - first.Seconds;
        return dt > 0.001 ? (last.X - first.X) / dt : 0;
    }

    /// <summary>
    /// The source <c>DraggableAnchors.computeTarget</c>: velocity above the threshold chooses the
    /// neighbouring anchor in the velocity direction; otherwise the positional threshold measured
    /// from the anchor behind the current position decides.
    /// </summary>
    private MaterialSwipeState ComputeTarget(double offset, double velocity)
    {
        var width = _width > 0 ? _width : Bounds.Width;
        if (width <= 0)
            return MaterialSwipeState.Settled;

        var anchors = new List<(MaterialSwipeState State, double Position)>(3)
        {
            (MaterialSwipeState.Settled, 0),
        };
        if (EnableDismissFromStartToEnd)
            anchors.Add((MaterialSwipeState.StartToEnd, DirectionSign * width));
        if (EnableDismissFromEndToStart)
            anchors.Add((MaterialSwipeState.EndToStart, -DirectionSign * width));
        anchors.Sort(static (a, b) => a.Position.CompareTo(b.Position));

        if (Math.Abs(velocity) < 0.001)
            return ClosestAnchor(anchors, offset, upwards: null);

        if (Math.Abs(velocity) >= VelocityThreshold)
            return ClosestAnchor(anchors, offset, upwards: velocity > 0);

        var left = ClosestAnchor(anchors, offset, upwards: false);
        var right = ClosestAnchor(anchors, offset, upwards: true);
        var leftPosition = PositionOf(anchors, left);
        var rightPosition = PositionOf(anchors, right);
        var relativeThreshold = Math.Abs(PositionalThreshold);
        var closestFromStart = velocity > 0 ? leftPosition : rightPosition;
        var relativePosition = Math.Abs(closestFromStart - offset);
        if (relativePosition >= relativeThreshold)
            return velocity > 0 ? right : left;
        return velocity > 0 ? left : right;
    }

    private static MaterialSwipeState ClosestAnchor(
        List<(MaterialSwipeState State, double Position)> anchors,
        double offset,
        bool? upwards)
    {
        if (upwards == true)
        {
            foreach (var anchor in anchors)
            {
                if (anchor.Position >= offset)
                    return anchor.State;
            }
            return anchors[^1].State;
        }

        if (upwards == false)
        {
            for (var i = anchors.Count - 1; i >= 0; i--)
            {
                if (anchors[i].Position <= offset)
                    return anchors[i].State;
            }
            return anchors[0].State;
        }

        var best = anchors[0];
        var bestDistance = Math.Abs(best.Position - offset);
        foreach (var anchor in anchors)
        {
            var distance = Math.Abs(anchor.Position - offset);
            if (distance < bestDistance)
            {
                best = anchor;
                bestDistance = distance;
            }
        }
        return best.State;
    }

    private static double PositionOf(
        List<(MaterialSwipeState State, double Position)> anchors,
        MaterialSwipeState state)
    {
        foreach (var anchor in anchors)
        {
            if (anchor.State == state)
                return anchor.Position;
        }
        return 0;
    }

    private void AnimateTo(MaterialSwipeState state, double velocity)
    {
        StopAnimations();
        var targetOffset = OffsetFor(state);
        TargetSwipeState = state;

        if (MaterialMotion.GetIsReduced(this) || Math.Abs(targetOffset - _offset) < 0.01 || !this.IsAttachedToVisualTree())
        {
            SetOffsetImmediate(targetOffset);
            SetSettled(state, raise: true);
            return;
        }

        // animateToWithDecay: if the velocity points at the target, settle with the default
        // exponential decay when it can reach the anchor; otherwise (or when the velocity points
        // away, e.g. after a veto) settle with the default snap tween.
        var direction = Math.Sign(targetOffset - _offset);
        if (Math.Abs(velocity) > 0.001 && velocity * (targetOffset - _offset) >= 0)
        {
            MaterialMotion.DecayAnimation? handle = null;
            var decay = MaterialMotion.ExponentialDecay(this, _offset, velocity, (value, _) =>
            {
                var anchor = OffsetFor(state);
                if (direction > 0 ? value >= anchor : value <= anchor)
                {
                    // Clamp at the anchor; the natural completion below finishes the settle.
                    SetOffsetImmediate(anchor);
                    if (handle is not null && ReferenceEquals(_decay, handle))
                        handle.Finish();
                    return;
                }
                SetOffsetImmediate(value);
            });
            var canDecay = direction > 0
                ? decay.TargetValue >= targetOffset
                : decay.TargetValue <= targetOffset;
            if (canDecay)
            {
                handle = decay;
                _decay = decay;
                decay.Completed += (_, _) =>
                {
                    if (!ReferenceEquals(_decay, decay))
                        return;
                    _decay = null;
                    SetOffsetImmediate(OffsetFor(state));
                    SetSettled(state, raise: true);
                };
                return;
            }
            decay.Dispose();
        }

        // AnchoredDraggableDefaults.SnapAnimationSpec: tween() with the default
        // FastOutSlowInEasing over 300 ms, started from the current offset.
        var completed = false;
        MaterialMotion.TweenAnimation? tweenHandle = null;
        var settle = MaterialMotion.Tween(this, _offset, targetOffset, SettleSeconds, 0, value =>
        {
            if (completed)
                return;
            var settledOffset = OffsetFor(state);
            // TweenAnimation.Finish applies the exact target value, so equality with the latest
            // anchor marks the end of the settle even after a resize retargeted the tween.
            if (value == settledOffset)
            {
                completed = true;
                if (tweenHandle is not null && ReferenceEquals(_tween, tweenHandle))
                    _tween = null;
                SetOffsetImmediate(settledOffset);
                SetSettled(state, raise: true);
                return;
            }
            SetOffsetImmediate(value);
        }, SnapEasing);
        if (completed)
        {
            settle.Dispose();
            return;
        }
        tweenHandle = settle;
        _tween = settle;
    }

    private void SetSettled(MaterialSwipeState state, bool raise)
    {
        SwipeState = state;
        TargetSwipeState = state;
        UpdatePseudoClasses(state);
        if (raise && state != MaterialSwipeState.Settled)
            Dismissed?.Invoke(this, new MaterialSwipeDismissedEventArgs(state));
    }

    private void UpdatePseudoClasses(MaterialSwipeState state)
    {
        PseudoClasses.Set(":swipe-settled", state == MaterialSwipeState.Settled);
        PseudoClasses.Set(":swipe-start-to-end", state == MaterialSwipeState.StartToEnd);
        PseudoClasses.Set(":swipe-end-to-start", state == MaterialSwipeState.EndToStart);
    }

    private void StopAnimations()
    {
        _tween?.Dispose();
        _tween = null;
        _decay?.Dispose();
        _decay = null;
    }

    private static Control? FindInteractive(Visual source)
    {
        for (Visual? current = source; current is not null; current = current.GetVisualParent())
        {
            if (current is Button or TextBox or Slider or ComboBox or ToggleButton)
                return current as Control;
            if (current is MaterialSwipeListItem)
                break;
        }
        return null;
    }
}

/// <summary>Reports that a swipe box settled in a dismissed direction.</summary>
public sealed class MaterialSwipeDismissedEventArgs : EventArgs
{
    public MaterialSwipeDismissedEventArgs(MaterialSwipeState direction) => Direction = direction;

    /// <summary>The dismissed direction.</summary>
    public MaterialSwipeState Direction { get; }
}

/// <summary>
/// Extension: one action shown in the background slot of a <see cref="MaterialSwipeListItem"/>.
/// This is presentation only; gestures and state remain the source swipe-to-dismiss algorithm.
/// </summary>
public sealed class MaterialSwipeAction
{
    /// <summary>Accessible name of the action button.</summary>
    public string? Label { get; set; }

    /// <summary>Icon geometry painted at 24 DIP.</summary>
    public Geometry? Icon { get; set; }

    /// <summary>Optional override for the action container.</summary>
    public IBrush? Background { get; set; }

    /// <summary>Optional override for the icon color.</summary>
    public IBrush? Foreground { get; set; }

    /// <summary>The command executed when the action is tapped.</summary>
    public ICommand? Command { get; set; }

    public object? CommandParameter { get; set; }

    /// <summary>Marks the wider 120 DIP primary action.</summary>
    public bool IsPrimary { get; set; }
}

/// <summary>Extension: raised when a swipe background action is invoked.</summary>
public sealed class MaterialSwipeActionInvokedEventArgs : EventArgs
{
    public MaterialSwipeActionInvokedEventArgs(MaterialSwipeAction action, int index, bool bySwipe)
    {
        Action = action;
        Index = index;
        BySwipe = bySwipe;
    }

    public MaterialSwipeAction Action { get; }

    public int Index { get; }

    public bool BySwipe { get; }
}