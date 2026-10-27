using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Theming;
using ExpressiveAvalonia.Tokens;

namespace ExpressiveAvalonia.Controls;

/// <summary>A settled or target position of a <see cref="MaterialSheet"/>.</summary>
/// <remarks>Mirrors <c>SheetValue</c> in SheetDefaults.kt.</remarks>
public enum MaterialSheetValue
{
    /// <summary>The sheet is fully outside the layout viewport.</summary>
    Hidden,

    /// <summary>The sheet is visible at its partial anchor.</summary>
    PartiallyExpanded,

    /// <summary>The sheet is visible at its expanded anchor.</summary>
    Expanded,
}

/// <summary>Event data for <see cref="MaterialSheet.Opening"/>.</summary>
public sealed class SheetOpeningEventArgs : EventArgs
{
    /// <summary>Set to true to keep the sheet closed.</summary>
    public bool Cancel { get; set; }
}

/// <summary>Event data for <see cref="MaterialSheet.Closing"/>.</summary>
public sealed class SheetClosingEventArgs : EventArgs
{
    /// <summary>Set to true to keep the sheet open.</summary>
    public bool Cancel { get; set; }
}

/// <summary>Event data for <see cref="MaterialSheet.ValueChangeRequested"/>.</summary>
/// <remarks>
/// Desktop adaptation of the <c>confirmValueChange</c> callback in SheetDefaults.kt. A listener
/// can veto both programmatic and gesture-driven state changes; the sheet then settles at the
/// value it already had.
/// </remarks>
public sealed class SheetValueChangeEventArgs(MaterialSheetValue value) : EventArgs
{
    /// <summary>The value the sheet is trying to reach.</summary>
    public MaterialSheetValue Value { get; } = value;

    /// <summary>Set to true to reject the transition.</summary>
    public bool Cancel { get; set; }
}

/// <summary>
/// Shared Material sheet lifecycle: anchored state, modal scrim, focus handling, dismissal and
/// the content and action slots.
/// </summary>
/// <remarks>
/// <para>
/// A sheet fills the layout slot it is given and positions its own surface inside that slot.
/// Host it as the last child of a <see cref="Panel"/> or <see cref="Grid"/> that covers the area
/// the sheet should block or slide over (usually the page or window root). While the sheet is
/// hidden the control is collapsed, so it costs no layout space.
/// </para>
/// <para>
/// State follows SheetDefaults.kt: <see cref="CurrentValue"/> is the settled value,
/// <see cref="TargetValue"/> the value being approached, and a drag updates the target preview.
/// A modal sheet adds a scrim, blocks pointer input behind the surface, cycles Tab focus inside
/// the surface and restores focus when it closes. A standard sheet leaves the page interactive
/// and does not move focus when it opens. Escape, a scrim tap and (for bottom sheets) a
/// downward drag request <see cref="MaterialSheetValue.Hidden"/> while
/// <see cref="IsDismissEnabled"/> is true; <see cref="Closing"/> and
/// <see cref="ValueChangeRequested"/> can veto the request.
/// </para>
/// <para>
/// Position motion uses the shared Material springs. Bottom sheets use the source's
/// default-spatial show/expand spring and fast-effects hide/partial-expand spring; this base
/// keeps position continuous when a reversal changes spec and lets the engine preserve velocity
/// when the spec stays the same.
/// </para>
/// </remarks>
public abstract class MaterialSheet : ContentControl
{
    /// <summary>Identifies the <see cref="IsOpen"/> property.</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<MaterialSheet, bool>(
            nameof(IsOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Identifies the <see cref="IsModal"/> property.</summary>
    public static readonly StyledProperty<bool> IsModalProperty =
        AvaloniaProperty.Register<MaterialSheet, bool>(nameof(IsModal));

    /// <summary>Identifies the <see cref="IsDismissEnabled"/> property.</summary>
    public static readonly StyledProperty<bool> IsDismissEnabledProperty =
        AvaloniaProperty.Register<MaterialSheet, bool>(nameof(IsDismissEnabled), true);

    /// <summary>Identifies the <see cref="SkipPartiallyExpanded"/> property.</summary>
    public static readonly StyledProperty<bool> SkipPartiallyExpandedProperty =
        AvaloniaProperty.Register<MaterialSheet, bool>(nameof(SkipPartiallyExpanded));

    /// <summary>Identifies the <see cref="ScrimOpacity"/> property.</summary>
    public static readonly StyledProperty<double> ScrimOpacityProperty =
        AvaloniaProperty.Register<MaterialSheet, double>(nameof(ScrimOpacity), 0.32,
            validate: static value => double.IsFinite(value) && value >= 0 && value <= 1);

    /// <summary>Identifies the <see cref="Actions"/> property.</summary>
    public static readonly StyledProperty<object?> ActionsProperty =
        AvaloniaProperty.Register<MaterialSheet, object?>(nameof(Actions));

    /// <summary>Identifies the <see cref="ActionsTemplate"/> property.</summary>
    public static readonly StyledProperty<IDataTemplate?> ActionsTemplateProperty =
        AvaloniaProperty.Register<MaterialSheet, IDataTemplate?>(nameof(ActionsTemplate));

    /// <summary>Identifies the <see cref="ShowActions"/> property.</summary>
    public static readonly StyledProperty<bool> ShowActionsProperty =
        AvaloniaProperty.Register<MaterialSheet, bool>(nameof(ShowActions));

    /// <summary>Identifies the <see cref="CurrentValue"/> property.</summary>
    public static readonly DirectProperty<MaterialSheet, MaterialSheetValue> CurrentValueProperty =
        AvaloniaProperty.RegisterDirect<MaterialSheet, MaterialSheetValue>(
            nameof(CurrentValue), static sheet => sheet._currentValue);

    /// <summary>Identifies the <see cref="TargetValue"/> property.</summary>
    public static readonly DirectProperty<MaterialSheet, MaterialSheetValue> TargetValueProperty =
        AvaloniaProperty.RegisterDirect<MaterialSheet, MaterialSheetValue>(
            nameof(TargetValue), static sheet => sheet._targetValue);

    private Panel? _root;
    private Border? _scrim;
    private Border? _surface;
    private Control? _actions;
    private MaterialSpringValue? _position;
    private MaterialSpringValue? _scrimAlpha;
    private TaskCompletionSource<bool>? _pendingValueCompletion;
    private TaskCompletionSource<bool>? _showCompletion;
    private MaterialSheetValue? _showTargetOverride;
    private MaterialSpring? _showSpringOverride;
    private TopLevel? _topLevel;
    private IInputElement? _previousFocus;
    private TaskCompletionSource<bool>? _opened;
    private TaskCompletionSource<bool>? _closed;
    private string? _appliedAccessibleName;
    private MaterialSheetValue _currentValue = MaterialSheetValue.Hidden;
    private MaterialSheetValue _targetValue = MaterialSheetValue.Hidden;
    private int _presentationGeneration;
    private bool _templateApplied;
    private bool _suppressOpenChange;
    private bool _openingRaised;
    private bool _presenting;
    private bool _restoreFocus;
    private bool _scrimPressed;

    protected MaterialSheet()
    {
        Focusable = false;
        AutomationProperties.SetControlTypeOverride(this, AutomationControlType.Pane);
        // Bubble so a focused control (an open drop-down, an editor) gets Escape first.
        AddHandler(KeyDownEvent, OnSheetKeyDown, RoutingStrategies.Bubble);
        SetCurrentValue(Visual.IsVisibleProperty, false);
        LayoutUpdated += OnSheetLayout;
    }

    /// <summary>Raised before the sheet opens. Set <see cref="SheetOpeningEventArgs.Cancel"/> to veto.</summary>
    public event EventHandler<SheetOpeningEventArgs>? Opening;

    /// <summary>Raised after the entrance motion completes.</summary>
    public event EventHandler? Opened;

    /// <summary>Raised before the sheet closes. Set <see cref="SheetClosingEventArgs.Cancel"/> to veto.</summary>
    public event EventHandler<SheetClosingEventArgs>? Closing;

    /// <summary>Raised after the exit motion completes and the sheet is collapsed.</summary>
    public event EventHandler? Closed;

    /// <summary>
    /// Raised for every requested state change, including drag settles. Set
    /// <see cref="SheetValueChangeEventArgs.Cancel"/> to reject it.
    /// </summary>
    public event EventHandler<SheetValueChangeEventArgs>? ValueChangeRequested;

    /// <summary>Whether the sheet is showing. Two-way by default.</summary>
    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <summary>
    /// Whether the sheet is modal. A modal sheet shows a scrim, traps Tab focus and restores the
    /// previously focused element when it closes. A standard sheet stays part of the page.
    /// </summary>
    public bool IsModal
    {
        get => GetValue(IsModalProperty);
        set => SetValue(IsModalProperty, value);
    }

    /// <summary>
    /// Whether implicit dismissal is allowed. When false, Escape, scrim taps and drag-to-dismiss
    /// are ignored; explicit affordances and <see cref="CloseAsync"/> still close the sheet.
    /// </summary>
    public bool IsDismissEnabled
    {
        get => GetValue(IsDismissEnabledProperty);
        set => SetValue(IsDismissEnabledProperty, value);
    }

    /// <summary>
    /// Omits <see cref="MaterialSheetValue.PartiallyExpanded"/> from the anchor set, like the
    /// <c>skipPartiallyExpanded</c> constructor argument in SheetDefaults.kt.
    /// </summary>
    public bool SkipPartiallyExpanded
    {
        get => GetValue(SkipPartiallyExpandedProperty);
        set => SetValue(SkipPartiallyExpandedProperty, value);
    }

    /// <summary>The scrim opacity in the range 0 to 1. Material uses 0.32.</summary>
    public double ScrimOpacity
    {
        get => GetValue(ScrimOpacityProperty);
        set => SetValue(ScrimOpacityProperty, value);
    }

    /// <summary>
    /// The action area content, normally a horizontal panel of buttons. The area is rendered only
    /// while <see cref="ShowActions"/> is true and this property is not null.
    /// </summary>
    public object? Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }

    /// <summary>The template used to render <see cref="Actions"/>.</summary>
    public IDataTemplate? ActionsTemplate
    {
        get => GetValue(ActionsTemplateProperty);
        set => SetValue(ActionsTemplateProperty, value);
    }

    /// <summary>Whether the action area is shown. A side sheet shows it by default.</summary>
    public bool ShowActions
    {
        get => GetValue(ShowActionsProperty);
        set => SetValue(ShowActionsProperty, value);
    }

    /// <summary>The settled state of the sheet.</summary>
    public MaterialSheetValue CurrentValue => _currentValue;

    /// <summary>
    /// The value being approached. During a drag this is the closest anchor, matching
    /// <c>SheetState.targetValue</c>.
    /// </summary>
    public MaterialSheetValue TargetValue => _targetValue;

    /// <summary>The sheet surface, available after the template is applied.</summary>
    protected Border? SheetSurface => _surface;

    /// <summary>The translation applied to the surface while it slides.</summary>
    protected TranslateTransform? SheetTransform { get; set; }

    /// <summary>The action area container, available after the template is applied.</summary>
    protected Control? ActionsRegion => _actions;

    /// <summary>Whether the platform requests reduced motion for this control.</summary>
    protected bool IsReducedMotion => MaterialMotion.GetIsReduced(this);

    /// <summary>Opens the sheet and completes when the entrance motion has finished.</summary>
    public Task OpenAsync()
    {
        if (IsOpen && _currentValue != MaterialSheetValue.Hidden)
            return Task.CompletedTask;

        _opened = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        SetCurrentValue(IsOpenProperty, true);
        return _opened.Task;
    }

    /// <summary>Closes the sheet and completes when the exit motion has finished.</summary>
    public Task CloseAsync()
    {
        if (!IsOpen && _currentValue == MaterialSheetValue.Hidden)
            return Task.CompletedTask;

        var completion = _closed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        SetCurrentValue(IsOpenProperty, false);
        return completion.Task;
    }

    /// <summary>Expands the sheet to <see cref="MaterialSheetValue.Expanded"/>.</summary>
    public Task ExpandAsync() => RequestValueAsync(MaterialSheetValue.Expanded);

    /// <summary>
    /// Expands the sheet to <see cref="MaterialSheetValue.PartiallyExpanded"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// <see cref="SkipPartiallyExpanded"/> is set, like the <c>partialExpand</c> check in
    /// SheetDefaults.kt.
    /// </exception>
    public Task PartialExpandAsync()
    {
        if (SkipPartiallyExpanded)
            throw new InvalidOperationException(
                "Cannot expand to PartiallyExpanded while SkipPartiallyExpanded is set.");
        return RequestValueAsync(MaterialSheetValue.PartiallyExpanded);
    }

    /// <summary>Requests a state transition, subject to <see cref="Closing"/> and <see cref="ValueChangeRequested"/>.</summary>
    public Task RequestValueAsync(MaterialSheetValue value)
    {
        if (value == MaterialSheetValue.Hidden)
            return HideCoreAsync();

        return ShowOrChangeAsync(value);
    }

    /// <summary>Closes the sheet when <see cref="IsDismissEnabled"/> is true.</summary>
    public void Dismiss()
    {
        if (IsDismissEnabled)
            SetCurrentValue(IsOpenProperty, false);
    }

    /// <summary>Returns whether the sheet can settle at <paramref name="value"/> right now.</summary>
    protected abstract bool CanUseValue(MaterialSheetValue value);

    /// <summary>The travel distance of <paramref name="value"/>, where 0 is fully visible.</summary>
    protected abstract double GetPosition(MaterialSheetValue value);

    /// <summary>Copies the scalar travel distance into the surface transform.</summary>
    protected abstract void ApplyPosition(double position);

    /// <summary>Called after each layout pass, before the position is reconciled with anchors.</summary>
    protected virtual void OnSheetLayoutPassed() { }

    /// <summary>The value <c>show()</c> selects: partial when available, otherwise expanded.</summary>
    protected virtual MaterialSheetValue GetShowTarget() =>
        CanUseValue(MaterialSheetValue.PartiallyExpanded)
            ? MaterialSheetValue.PartiallyExpanded
            : MaterialSheetValue.Expanded;

    /// <summary>
    /// Maps a value onto the anchors that exist after a resize or an anchor-property change.
    /// </summary>
    /// <remarks>
    /// Mirrors the target reconciliation in <c>draggableAnchors</c> (BottomSheetScaffold.kt): when a
    /// preferred anchor disappears, the sheet moves to the next available one instead of animating
    /// to a position that no longer exists. The default keeps the value.
    /// </remarks>
    protected virtual MaterialSheetValue ReconcileTarget(MaterialSheetValue value) => value;

    /// <summary>The spring used when the sheet becomes visible or expands.</summary>
    protected virtual MaterialSpring ShowSpring => MaterialSpring.SpatialDefault;

    /// <summary>Whether the first presentation enters from Hidden rather than its initial anchor.</summary>
    protected virtual bool AnimateInitialPresentation => true;

    /// <summary>The spring used when the sheet hides or partially expands.</summary>
    protected virtual MaterialSpring HideSpring => MaterialSpring.EffectsFast;

    /// <summary>Whether a drag owns the surface position, so resize must not snap it.</summary>
    protected virtual bool IsDragInProgress => false;

    /// <summary>Called after the template parts have been resolved.</summary>
    protected virtual void OnSheetTemplateApplied(TemplateAppliedEventArgs e) { }

    /// <summary>Called when the modal chrome of the surface may need to change.</summary>
    protected virtual void OnSheetChromeChanged() { }

    /// <summary>The accessible name derived from the sheet anatomy, or null to keep a consumer name.</summary>
    protected virtual string? GetSheetAccessibleName() => null;

    /// <summary>Starts a new value transition or returns false when a listener vetoed it.</summary>
    protected bool ConfirmValueChange(MaterialSheetValue value)
    {
        var args = new SheetValueChangeEventArgs(value);
        ValueChangeRequested?.Invoke(this, args);
        return !args.Cancel;
    }

    /// <summary>Runs one frame of the layout phase without a duration timer.</summary>
    protected static Task WaitForLayoutAsync()
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Dispatcher.UIThread.Post(() => completion.TrySetResult(true), DispatcherPriority.Loaded);
        return completion.Task;
    }

    /// <summary>Requests the fallback transition after a gestured target was vetoed.</summary>
    protected void SettleAtCurrentValue()
    {
        if (_position is null || _currentValue == MaterialSheetValue.Hidden)
            return;
        SetTargetValue(_currentValue);
        SetScrimTarget(IsModal ? 1 : 0);
        _position.AnimateTo(GetPosition(_currentValue), MaterialSpring.SpatialDefault, null, 0.5);
    }

    /// <summary>
    /// Cancels a hide that became invalid because its anchor disappeared, and returns to a
    /// visible anchor.
    /// </summary>
    /// <remarks>
    /// Used when the hidden anchor is removed while the exit motion is already running. A hide
    /// guard in <see cref="HideCoreAsync"/> covers new close requests; this covers the transition
    /// that was requested before the anchor set changed.
    /// </remarks>
    protected Task<bool> AbortHideAsync()
    {
        SetIsOpenSilently(true);
        if (IsModal && _scrim is not null)
            SetScrimTarget(1);
        return ShowOrChangeAsync(GetShowTarget(), HideSpring);
    }

    /// <summary>
    /// Settles a drag/fling at <paramref name="value"/> with the anchored-draggable spring
    /// (default spatial), regardless of whether the value is <c>Hidden</c>.
    /// </summary>
    protected Task<bool> SettleFromGestureAsync(MaterialSheetValue value) =>
        value == MaterialSheetValue.Hidden
            ? HideCoreAsync(MaterialSpring.SpatialDefault)
            : ShowOrChangeAsync(value, MaterialSpring.SpatialDefault);

    /// <summary>Moves the surface while a drag owns the state, updating the target preview.</summary>
    protected void MoveDragTo(double position)
    {
        _position?.JumpTo(position);
    }

    /// <summary>The current surface travel distance.</summary>
    protected double SheetPosition => _position?.Value ?? 0;

    /// <summary>Updates <see cref="TargetValue"/> without starting motion (drag preview).</summary>
    protected void SetDragPreview(MaterialSheetValue value)
    {
        if (_targetValue == value)
            return;
        var old = _targetValue;
        _targetValue = value;
        RaisePropertyChanged(TargetValueProperty, old, value);
        SetScrimTarget(IsModal && value != MaterialSheetValue.Hidden ? 1 : 0);
    }

    /// <summary>Removes the overlay from the topmost-Escape registry when it is no longer visible.</summary>
    protected void UnregisterOverlay() => MaterialOverlayLifecycle.Unregister(_topLevel, this);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_scrim is not null)
        {
            _scrim.PointerPressed -= OnScrimPointerPressed;
            _scrim.PointerReleased -= OnScrimPointerReleased;
            _scrim.PointerCaptureLost -= OnScrimPointerCaptureLost;
        }

        base.OnApplyTemplate(e);

        _root = e.NameScope.Find<Panel>("PART_Root");
        _scrim = e.NameScope.Find<Border>("PART_Scrim");
        _surface = e.NameScope.Find<Border>("PART_Surface");
        _actions = e.NameScope.Find<Control>("PART_Actions");

        if (_scrim is not null)
        {
            _scrim.PointerPressed += OnScrimPointerPressed;
            _scrim.PointerReleased += OnScrimPointerReleased;
            _scrim.PointerCaptureLost += OnScrimPointerCaptureLost;
        }

        if (_surface is not null)
        {
            _surface.Focusable = true;
            KeyboardNavigation.SetIsTabStop(_surface, false);
        }

        _templateApplied = true;
        UpdateActionsVisibility();
        UpdateAccessibleName();
        OnSheetTemplateApplied(e);

        _position?.Dispose();
        _position = new MaterialSpringValue(this, GetPosition(MaterialSheetValue.Hidden), ApplyPosition);
        _scrimAlpha?.Dispose();
        _scrimAlpha = new MaterialSpringValue(this, 0, ApplyScrimOpacity);

        UpdateModalState();

        if (IsOpen)
        {
            // A re-applied template (for example after a theme change) restores the open state
            // without replaying Opening/Opened.
            if (_openingRaised || _currentValue != MaterialSheetValue.Hidden)
                RestoreOpenState();
            else
                _ = GuardedAsync(ShowCoreAsync(null, ShowSpring));
        }
        else
        {
            _presenting = false;
            _position.JumpTo(GetPosition(MaterialSheetValue.Hidden));
            if (_scrim is not null)
            {
                _scrim.IsVisible = false;
                _scrim.Opacity = 0;
            }
            if (_root is not null)
                _root.IsHitTestVisible = false;
            SetCurrentValue(Visual.IsVisibleProperty, false);
        }

        UpdatePositionFromState();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _topLevel = TopLevel.GetTopLevel(this);

        if (_templateApplied && _position is null)
        {
            _position = new MaterialSpringValue(this, GetPosition(MaterialSheetValue.Hidden), ApplyPosition);
            _scrimAlpha = new MaterialSpringValue(this, 0, ApplyScrimOpacity);
        }

        if (!_templateApplied || !IsOpen)
            return;

        if (!_openingRaised && _currentValue == MaterialSheetValue.Hidden)
        {
            // Opening was requested before the sheet joined a visual tree; start the real
            // entrance now that layout and focus are available.
            _ = GuardedAsync(ShowCoreAsync(null, ShowSpring));
            return;
        }

        // A sheet that was open while detached resumes in its open state without replaying events.
        RestoreOpenState();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _presentationGeneration++;
        _position?.Dispose();
        _position = null;
        _scrimAlpha?.Dispose();
        _scrimAlpha = null;
        UnregisterOverlay();
        _topLevel = null;
        _presenting = false;
        _openingRaised = false;
        _scrimPressed = false;

        CancelValueCompletion();
        var stagedShow = _showCompletion;
        _showCompletion = null;
        _showTargetOverride = null;
        _showSpringOverride = null;
        stagedShow?.TrySetResult(false);
        CompleteOpened(false);
        CompleteClosed(false);
        _previousFocus = null;
        _restoreFocus = false;

        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsOpenProperty)
            HandleIsOpenChanged(change.GetNewValue<bool>());
        else if (change.Property == IsModalProperty)
            HandleIsModalChanged();
        else if (change.Property == ScrimOpacityProperty &&
                 _scrim is not null && IsOpen && IsModal && _scrimAlpha is not null)
            _scrimAlpha.JumpTo(ScrimOpacity);
        else if (change.Property == ShowActionsProperty || change.Property == ActionsProperty ||
                 change.Property == ActionsTemplateProperty)
            UpdateActionsVisibility();
        else if (change.Property == SkipPartiallyExpandedProperty &&
                 _targetValue == MaterialSheetValue.PartiallyExpanded &&
                 !CanUseValue(MaterialSheetValue.PartiallyExpanded))
            _ = GuardedAsync(ShowOrChangeAsync(GetShowTarget()));
    }

    private void OnSheetLayout(object? sender, EventArgs e)
    {
        if (!_templateApplied)
            return;

        UpdateSurfaceMeasureCap();
        OnSheetLayoutPassed();
        UpdatePositionFromState();
    }

    /// <summary>Sets the surface measure cap for the current host size.</summary>
    protected virtual void UpdateSurfaceMeasureCap()
    {
        if (_surface is not null && Bounds.Height > 0 &&
            Math.Abs(_surface.MaxHeight - Bounds.Height) > 0.5)
            _surface.MaxHeight = Bounds.Height;
    }

    private void HandleIsOpenChanged(bool open)
    {
        if (_suppressOpenChange)
            return;

        if (!_templateApplied)
        {
            // Visibility must be true for the template to be applied in the first place.
            SetCurrentValue(Visual.IsVisibleProperty, open);
            return;
        }

        if (open)
        {
            if (_currentValue == MaterialSheetValue.Hidden &&
                _targetValue == MaterialSheetValue.Hidden && !_openingRaised)
                _ = GuardedAsync(ShowCoreAsync(null, ShowSpring));
        }
        else
        {
            _ = GuardedAsync(HideCoreAsync());
        }
    }

    private async Task<bool> ShowCoreAsync(MaterialSheetValue? explicitTarget, MaterialSpring spring)
    {
        if (_currentValue != MaterialSheetValue.Hidden || _targetValue != MaterialSheetValue.Hidden || _openingRaised)
        {
            return explicitTarget is null
                ? true
                : await ShowOrChangeAsync(explicitTarget.Value);
        }

        var opening = new SheetOpeningEventArgs();
        Opening?.Invoke(this, opening);
        if (opening.Cancel)
        {
            SetIsOpenSilently(false);
            CompleteOpened(false);
            return false;
        }

        var generation = ++_presentationGeneration;
        var top = _topLevel ?? TopLevel.GetTopLevel(this);
        if (top is null)
        {
            // The sheet is not in a visual tree yet. Keep IsOpen pending; Attached starts the
            // entrance once there is a top level to register with and a layout pass to measure.
            _openingRaised = false;
            _presenting = false;
            return false;
        }
        _topLevel = top;

        _previousFocus = top.FocusManager?.GetFocusedElement();
        _restoreFocus = false;
        _openingRaised = true;
        _presenting = true;
        _showCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        SetCurrentValue(Visual.IsVisibleProperty, true);
        if (_root is not null)
            _root.IsHitTestVisible = true;
        if (_scrim is not null)
            _scrim.IsVisible = IsModal;
        SetScrimTarget(0);
        UpdateModalState();
        MaterialOverlayLifecycle.Register(_topLevel, this);

        // The content must measure before the partial anchor and the sheet travel distance exist.
        await WaitForLayoutAsync();
        if (generation != _presentationGeneration || !IsOpen)
            return false;

        OnSheetLayoutPassed();
        var target = _showTargetOverride ?? explicitTarget ?? GetShowTarget();
        var effectiveSpring = _showSpringOverride ?? spring;
        _showTargetOverride = null;
        _showSpringOverride = null;
        if (!CanUseValue(target))
            target = MaterialSheetValue.Expanded;
        if (!CanUseValue(target))
        {
            // No measured surface yet (for example an empty template): stay closed.
            _openingRaised = false;
            SetIsOpenSilently(false);
            FinalizeHidden(restoreFocus: false);
            CompleteOpened(false);
            return false;
        }

        if (!ConfirmValueChange(target))
        {
            _openingRaised = false;
            SetIsOpenSilently(false);
            FinalizeHidden(restoreFocus: false);
            CompleteOpened(false);
            return false;
        }

        SetTargetValue(target);
        SetIsOpenSilently(true);

        // Modal sheets enter from Hidden; a persistent scaffold starts at its initial anchor.
        if (_position is not null)
            _position.JumpTo(GetPosition(AnimateInitialPresentation
                ? MaterialSheetValue.Hidden : target));

        // A modal sheet takes focus for its trap. A standard/persistent sheet is part of the
        // page and must not move focus away from the user's current control on open.
        if (IsModal)
            FocusFirst();
        SetScrimTarget(IsModal ? 1 : 0);

        await AnimateValueAsync(target, effectiveSpring);
        return _currentValue == target;
    }

    private async Task<bool> ShowOrChangeAsync(MaterialSheetValue value, MaterialSpring? springOverride = null)
    {
        if (value == MaterialSheetValue.Hidden)
            return await HideCoreAsync(springOverride);

        if (!CanUseValue(value))
        {
            // PartiallyExpanded may become available only after measurement. If the caller asked
            // for it while the surface is not measured, present the sheet and re-evaluate.
            if (value == MaterialSheetValue.PartiallyExpanded && !SkipPartiallyExpanded)
                return await ShowCoreAsync(value, springOverride ??
                    (value == MaterialSheetValue.PartiallyExpanded ? HideSpring : ShowSpring));
            return false;
        }

        var spring = springOverride ??
                     (value == MaterialSheetValue.PartiallyExpanded ? HideSpring : ShowSpring);

        if (_currentValue == MaterialSheetValue.Hidden && _openingRaised)
        {
            // The entrance is staged but the layout pass has not resolved its target yet.
            // Publish the new target to that entrance instead of starting a recursive show;
            // the show confirms the final target once, after layout.
            _showTargetOverride = value;
            _showSpringOverride = spring;
            var staged = _showCompletion;
            if (staged is not null)
                await staged.Task;
            return _currentValue == value;
        }

        if (_currentValue == MaterialSheetValue.Hidden)
            return await ShowCoreAsync(value, spring);

        if (!ConfirmValueChange(value))
        {
            SetTargetValue(_currentValue);
            SetScrimTarget(IsModal ? 1 : 0);
            return false;
        }

        SetTargetValue(value);
        await AnimateValueAsync(value, spring);
        return _currentValue == value;
    }

    private async Task<bool> HideCoreAsync(MaterialSpring? springOverride = null)
    {
        if (_currentValue == MaterialSheetValue.Hidden && !_presenting)
        {
            SetIsOpenSilently(false);
            CompleteClosed(true);
            return true;
        }

        // The hidden anchor is part of the state contract (SheetState.hide checks
        // skipHiddenState). When it is absent, a close request is a collapse to the lowest
        // remaining visible anchor instead of an exit: Closing/Closed are not raised and the
        // sheet stays open.
        if (!CanUseValue(MaterialSheetValue.Hidden))
        {
            SetIsOpenSilently(true);
            var fallback = GetShowTarget();
            SetTargetValue(_currentValue);
            if (_presenting && fallback != _currentValue && CanUseValue(fallback))
                await ShowOrChangeAsync(fallback, springOverride ?? HideSpring);
            CompleteClosed(false);
            return false;
        }

        var closing = new SheetClosingEventArgs();
        Closing?.Invoke(this, closing);
        if (closing.Cancel)
        {
            SetIsOpenSilently(true);
            SetTargetValue(_currentValue);
            CompleteClosed(false);
            return false;
        }

        if (!ConfirmValueChange(MaterialSheetValue.Hidden))
        {
            SetIsOpenSilently(true);
            SetTargetValue(_currentValue);
            SetScrimTarget(IsModal && _currentValue != MaterialSheetValue.Hidden ? 1 : 0);
            CompleteClosed(false);
            return false;
        }

        _presentationGeneration++;
        SetTargetValue(MaterialSheetValue.Hidden);
        SetIsOpenSilently(false);
        SetScrimTarget(0);

        // Capture containment before the sheet is hidden. A light dismiss must not steal focus
        // from a control the user has just chosen outside the sheet.
        var focus = _topLevel?.FocusManager?.GetFocusedElement();
        _restoreFocus = focus is not null && _surface is not null &&
                        (ReferenceEquals(focus, _surface) ||
                         (focus is Visual visual &&
                          (_surface.IsVisualAncestorOf(visual) ||
                           (_scrim is not null && (ReferenceEquals(focus, _scrim) || _scrim.IsVisualAncestorOf(visual))))));

        await AnimateValueAsync(MaterialSheetValue.Hidden, springOverride ?? HideSpring);
        var hidden = _currentValue == MaterialSheetValue.Hidden;
        if (!hidden)
            CompleteClosed(false);
        return hidden;
    }

    private Task AnimateValueAsync(MaterialSheetValue value, MaterialSpring spring)
    {
        // A new transition supersedes the previous task; never leave a caller awaiting a value
        // that is no longer the target.
        _pendingValueCompletion?.TrySetResult(false);

        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingValueCompletion = completion;

        void Settle()
        {
            if (ReferenceEquals(_pendingValueCompletion, completion))
                _pendingValueCompletion = null;
            OnValueSettled(value);
            completion.TrySetResult(true);
        }

        if (_position is null)
        {
            Settle();
            return completion.Task;
        }

        var target = GetPosition(value);
        if (IsReducedMotion || Math.Abs(target - _position.Value) < 0.5)
        {
            _position.JumpTo(target);
            Settle();
            return completion.Task;
        }

        _position.AnimateTo(target, spring, Settle, 0.5);
        return completion.Task;
    }

    private void CancelValueCompletion()
    {
        var completion = _pendingValueCompletion;
        _pendingValueCompletion = null;
        completion?.TrySetResult(false);
    }

    private void OnValueSettled(MaterialSheetValue value)
    {
        if (_currentValue != value)
        {
            var old = _currentValue;
            _currentValue = value;
            RaisePropertyChanged(CurrentValueProperty, old, value);
        }

        if (value == MaterialSheetValue.Hidden)
        {
            FinalizeHidden(restoreFocus: true);
            return;
        }

        if (value != MaterialSheetValue.Hidden && _openingRaised)
        {
            // The entrance may have been superseded by expand/partial; completing it on the
            // first non-hidden settle still gives OpenAsync and Opened a stable notification.
            _openingRaised = false;
            CompleteOpened(true);
            Opened?.Invoke(this, EventArgs.Empty);
        }

        if (value != MaterialSheetValue.Hidden)
        {
            var show = _showCompletion;
            _showCompletion = null;
            show?.TrySetResult(true);
        }
    }

    private void FinalizeHidden(bool restoreFocus)
    {
        if (!_presenting)
            return;

        _presenting = false;
        _openingRaised = false;
        CancelValueCompletion();
        var staged = _showCompletion;
        _showCompletion = null;
        _showTargetOverride = null;
        _showSpringOverride = null;
        staged?.TrySetResult(false);
        UnregisterOverlay();
        _scrimAlpha?.JumpTo(0);

        if (_scrim is not null)
        {
            _scrim.IsVisible = false;
            _scrim.Opacity = 0;
        }
        if (_root is not null)
            _root.IsHitTestVisible = false;
        SetCurrentValue(Visual.IsVisibleProperty, false);

        if (_position is not null)
            _position.JumpTo(GetPosition(MaterialSheetValue.Hidden));

        var previous = _previousFocus;
        var shouldRestore = restoreFocus && _restoreFocus;
        _previousFocus = null;
        _restoreFocus = false;

        CompleteOpened(false);
        CompleteClosed(true);
        Closed?.Invoke(this, EventArgs.Empty);

        if (shouldRestore && previous is Control { IsEffectivelyEnabled: true, IsEffectivelyVisible: true } control &&
            control.IsAttachedToVisualTree())
            control.Focus(NavigationMethod.Unspecified);
    }

    private void RestoreOpenState()
    {
        _presenting = true;
        SetCurrentValue(Visual.IsVisibleProperty, true);
        if (_root is not null)
            _root.IsHitTestVisible = true;
        if (_surface is not null)
            _surface.IsVisible = true;

        var value = _targetValue == MaterialSheetValue.Hidden ? _currentValue : _targetValue;
        if (value == MaterialSheetValue.Hidden)
            value = GetShowTarget();
        if (!CanUseValue(value))
            value = MaterialSheetValue.Expanded;
        SetTargetValue(value);
        _position?.JumpTo(GetPosition(value));

        if (_scrim is not null)
        {
            _scrim.IsVisible = IsModal;
            SetScrimTarget(IsModal ? 1 : 0);
        }

        UpdateModalState();
        MaterialOverlayLifecycle.Register(_topLevel, this);

        if (_openingRaised)
        {
            _openingRaised = false;
            CompleteOpened(true);
        }

        UpdatePositionFromState();
    }

    private void UpdatePositionFromState()
    {
        if (_position is null || !_templateApplied)
            return;

        if (IsDragInProgress)
            return;

        // ShowCoreAsync chooses the initial anchor after the first layout. Hidden here is
        // staging, not a settled state to reconcile against a transient zero-size surface.
        if (_openingRaised && _currentValue == MaterialSheetValue.Hidden &&
            _targetValue == MaterialSheetValue.Hidden)
            return;

        if (!_presenting)
        {
            if (_currentValue == MaterialSheetValue.Hidden)
                _position.JumpTo(GetPosition(MaterialSheetValue.Hidden));
            return;
        }

        // While a transition runs, keep its target on an anchor that still exists. A resize or a
        // skip-flag change can remove the target anchor mid-flight (BottomSheetScaffold's
        // newAnchors/ newTarget reconciliation).
        if (_position.IsRunning)
        {
            var target = ReconcileTarget(_targetValue);
            if (target != _targetValue)
            {
                SetTargetValue(target);
                if (target != MaterialSheetValue.Hidden)
                    SetIsOpenSilently(true);
            }
            _position.RetargetTo(GetPosition(target));
            return;
        }

        // A staged open/restore already named the anchor to present; adopt it without motion.
        if (_currentValue == MaterialSheetValue.Hidden && _targetValue != MaterialSheetValue.Hidden)
        {
            var staged = ReconcileTarget(_targetValue);
            SetCurrentAnchorValue(staged);
            SetTargetValue(staged);
            _position.JumpTo(GetPosition(staged));
            return;
        }

        var previous = _currentValue;
        var current = ReconcileTarget(previous);
        SetCurrentAnchorValue(current);
        if (current == MaterialSheetValue.Hidden && previous != MaterialSheetValue.Hidden)
        {
            // A resize removed every visible anchor: close coherently instead of leaving the
            // sheet logically open at the hidden position.
            SetIsOpenSilently(false);
            if (_presenting)
                FinalizeHidden(restoreFocus: true);
            _position.JumpTo(GetPosition(MaterialSheetValue.Hidden));
            return;
        }
        if (_targetValue != current)
        {
            SetTargetValue(current);
            if (current != MaterialSheetValue.Hidden)
                SetIsOpenSilently(true);
        }
        _position.JumpTo(GetPosition(current));
    }

    /// <summary>Publishes a settled anchor without running the settle notifications.</summary>
    private void SetCurrentAnchorValue(MaterialSheetValue value)
    {
        if (_currentValue == value)
            return;
        var old = _currentValue;
        _currentValue = value;
        RaisePropertyChanged(CurrentValueProperty, old, value);
    }

    private void SetScrimTarget(double target)
    {
        if (_scrim is null)
            return;

        if (!IsModal)
        {
            _scrimAlpha?.JumpTo(0);
            _scrim.IsVisible = false;
            return;
        }

        if (target > 0)
            _scrim.IsVisible = true;

        if (_scrimAlpha is null)
        {
            _scrim.Opacity = target <= 0 && _targetValue == MaterialSheetValue.Hidden ? 0 : ScrimOpacity;
            return;
        }

        _scrimAlpha.AnimateTo(target <= 0 ? 0 : ScrimOpacity, MaterialSpring.EffectsDefault,
            () =>
            {
                if (_scrim is not null && _targetValue == MaterialSheetValue.Hidden && target <= 0)
                {
                    _scrim.Opacity = 0;
                    _scrim.IsVisible = false;
                }
            }, 0.01);
    }

    private void ApplyScrimOpacity(double value)
    {
        if (_scrim is not null)
            _scrim.Opacity = Math.Clamp(value, 0, 1);
    }

    private void HandleIsModalChanged()
    {
        UpdateModalState();

        if (_scrim is null)
            return;

        if (IsModal && _presenting)
        {
            _scrim.IsVisible = true;
            SetScrimTarget(_targetValue == MaterialSheetValue.Hidden ? 0 : 1);
        }
        else
        {
            _scrimAlpha?.JumpTo(0);
            _scrim.Opacity = 0;
            _scrim.IsVisible = false;
        }
    }

    private void UpdateModalState()
    {
        if (_surface is not null)
        {
            KeyboardNavigation.SetTabNavigation(_surface,
                IsModal ? KeyboardNavigationMode.Cycle : KeyboardNavigationMode.Continue);
        }

        OnSheetChromeChanged();
    }

    private void UpdateActionsVisibility()
    {
        if (_actions is not null)
            _actions.IsVisible = ShowActions && Actions is not null;
    }

    private void OnSheetKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.Key != Key.Escape || !IsOpen || !IsDismissEnabled)
            return;

        if (!MaterialOverlayLifecycle.IsTopmost(_topLevel, this))
            return;

        // A popup opened from inside the sheet (combo box, menu) owns Escape while it is up.
        if (e.Source is Visual visual && _topLevel is not null &&
            TopLevel.GetTopLevel(visual) is { } sourceTop && !ReferenceEquals(sourceTop, _topLevel))
            return;

        e.Handled = true;
        SetCurrentValue(IsOpenProperty, false);
    }

    private void OnScrimPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsOpen || !IsDismissEnabled || _scrim is null)
            return;

        var point = e.GetCurrentPoint(_scrim);
        if (!point.Properties.IsLeftButtonPressed && e.Pointer.Type != PointerType.Touch)
            return;

        _scrimPressed = true;
        e.Pointer.Capture(_scrim);
        e.Handled = true;
    }

    private void OnScrimPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_scrimPressed)
            return;

        _scrimPressed = false;
        e.Pointer.Capture(null);
        if (!IsOpen || !IsDismissEnabled || _scrim is null)
            return;
        if (!new Rect(_scrim.Bounds.Size).Contains(e.GetPosition(_scrim)))
            return;

        e.Handled = true;
        SetCurrentValue(IsOpenProperty, false);
    }

    private void OnScrimPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e) =>
        _scrimPressed = false;

    private void FocusFirst()
    {
        if (_surface is null)
            return;

        var target = FindFirstFocusable(_surface) ?? _surface;
        target.Focus(NavigationMethod.Unspecified);
    }

    private static Control? FindFirstFocusable(Visual root)
    {
        foreach (var visual in root.GetVisualDescendants())
        {
            if (visual is Control
                {
                    Focusable: true,
                    IsEffectivelyEnabled: true,
                    IsEffectivelyVisible: true
                } control && KeyboardNavigation.GetIsTabStop(control))
                return control;
        }

        return null;
    }

    private void SetIsOpenSilently(bool value)
    {
        if (IsOpen == value)
            return;

        _suppressOpenChange = true;
        try
        {
            SetCurrentValue(IsOpenProperty, value);
        }
        finally
        {
            _suppressOpenChange = false;
        }
    }

    private void SetTargetValue(MaterialSheetValue value)
    {
        if (_targetValue == value)
            return;
        var old = _targetValue;
        _targetValue = value;
        RaisePropertyChanged(TargetValueProperty, old, value);
    }

    private void CompleteOpened(bool result)
    {
        var completion = _opened;
        _opened = null;
        completion?.TrySetResult(result);
    }

    private void CompleteClosed(bool result)
    {
        var completion = _closed;
        _closed = null;
        completion?.TrySetResult(result);
    }

    /// <summary>Runs a transition without exposing its task, reporting failures on the dispatcher.</summary>
    protected void RunTransition(Task task) => _ = GuardedAsync(task);

    /// <summary>Runs a transition and returns its task for callers that compose several steps.</summary>
    protected Task RunTransitionAsync(Task task)
    {
        _ = GuardedAsync(task);
        return task;
    }

    private static async Task GuardedAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (Exception exception)
        {
            // Overlay work is driven by layout and gesture callbacks; there is no caller to
            // observe an exception. Report it on the dispatcher without breaking the input loop.
            var captured = System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception);
            Dispatcher.UIThread.Post(captured.Throw);
        }
    }

    /// <summary>Updates the accessible name from <see cref="GetSheetAccessibleName"/>.</summary>
    protected void UpdateAccessibleName()
    {
        var name = GetSheetAccessibleName();
        var current = AutomationProperties.GetName(this);

        // A name supplied by the consumer wins; only replace a name this control applied.
        if (!string.IsNullOrEmpty(current) && current != _appliedAccessibleName)
            return;
        if (string.IsNullOrWhiteSpace(name))
            return;

        AutomationProperties.SetName(this, name);
        _appliedAccessibleName = name;
    }
}