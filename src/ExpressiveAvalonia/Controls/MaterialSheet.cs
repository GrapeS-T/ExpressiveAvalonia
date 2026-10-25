using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
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

namespace ExpressiveAvalonia.Controls;

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

/// <summary>
/// Shared Material sheet lifecycle: open/close state, modal scrim, focus handling, dismissal and
/// the content and action slots.
/// </summary>
/// <remarks>
/// <para>
/// A sheet fills the layout slot it is given and positions its own surface inside that slot.
/// Host it as the last child of a <see cref="Panel"/> or <see cref="Grid"/> that covers the area
/// the sheet should block or slide over (usually the page or window root). While
/// <see cref="IsOpen"/> is false the control is collapsed, so it costs no layout space.
/// </para>
/// <para>
/// A modal sheet (<see cref="IsModal"/>) adds a scrim, blocks pointer input behind the surface,
/// cycles Tab focus inside the surface and restores focus when it closes. Escape, a scrim click
/// and (for bottom sheets) a downward drag dismiss the sheet while
/// <see cref="IsDismissEnabled"/> is true. Enter and exit motion uses the Material duration
/// tokens and collapses to no motion when <see cref="MaterialMotion.GetIsReduced"/> is true.
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

    // The surface offset is a styled property on the control (a Visual) so Avalonia's transition
    // machinery can animate it; each animated value is copied into the surface's translation.
    private static readonly StyledProperty<double> SheetOffsetProperty =
        AvaloniaProperty.Register<MaterialSheet, double>("SheetOffset");

    private Panel? _root;
    private Border? _scrim;
    private Border? _surface;
    private Control? _actions;
    private TranslateTransform? _slide;
    private CancellationTokenSource? _transition;
    private TopLevel? _topLevel;
    private IInputElement? _previousFocus;
    private TaskCompletionSource<bool>? _opened;
    private TaskCompletionSource<bool>? _closed;
    private string? _appliedAccessibleName;
    private bool _templateApplied;
    private bool _suppressOpenChange;
    private bool _restoreFocus;
    private bool _openingRaised;

    protected MaterialSheet()
    {
        Focusable = false;
        AutomationProperties.SetControlTypeOverride(this, AutomationControlType.Pane);
        // Bubble so a focused control (an open drop-down, an editor) gets Escape first.
        AddHandler(KeyDownEvent, OnSheetKeyDown, RoutingStrategies.Bubble);
        SetCurrentValue(Visual.IsVisibleProperty, false);
    }

    /// <summary>Raised before the sheet opens. Set <see cref="SheetOpeningEventArgs.Cancel"/> to veto.</summary>
    public event EventHandler<SheetOpeningEventArgs>? Opening;

    /// <summary>Raised after the entrance motion completes.</summary>
    public event EventHandler? Opened;

    /// <summary>Raised before the sheet closes. Set <see cref="SheetClosingEventArgs.Cancel"/> to veto.</summary>
    public event EventHandler<SheetClosingEventArgs>? Closing;

    /// <summary>Raised after the exit motion completes and the sheet is collapsed.</summary>
    public event EventHandler? Closed;

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
    /// Whether implicit dismissal is allowed. When false, Escape, scrim clicks and drag-to-dismiss
    /// are ignored; explicit affordances and <see cref="CloseAsync"/> still close the sheet.
    /// </summary>
    public bool IsDismissEnabled
    {
        get => GetValue(IsDismissEnabledProperty);
        set => SetValue(IsDismissEnabledProperty, value);
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

    /// <summary>The sheet surface, available after the template is applied.</summary>
    protected Border? SheetSurface => _surface;

    /// <summary>The translation applied to the surface while it slides.</summary>
    protected TranslateTransform? SheetTransform => _slide;

    /// <summary>The action area container, available after the template is applied.</summary>
    protected Control? ActionsRegion => _actions;

    /// <summary>Whether the platform requests reduced motion for this control.</summary>
    protected bool IsReducedMotion => MaterialMotion.GetIsReduced(this);

    /// <summary>Opens the sheet and completes when the entrance motion has finished.</summary>
    public Task OpenAsync()
    {
        if (IsOpen)
            return _opened?.Task ?? Task.CompletedTask;

        _opened = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        SetCurrentValue(IsOpenProperty, true);
        return _opened.Task;
    }

    /// <summary>Closes the sheet and completes when the exit motion has finished.</summary>
    public Task CloseAsync()
    {
        if (!IsOpen)
            return _closed?.Task ?? Task.CompletedTask;

        _closed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        SetCurrentValue(IsOpenProperty, false);
        return _closed.Task;
    }

    /// <summary>Closes the sheet when <see cref="IsDismissEnabled"/> is true.</summary>
    public void Dismiss()
    {
        if (IsDismissEnabled)
            SetCurrentValue(IsOpenProperty, false);
    }

    /// <summary>The travel distance from the open position to the fully closed position.</summary>
    protected abstract double GetClosedOffset();

    /// <summary>Copies the scalar offset into the surface translation.</summary>
    protected abstract void ApplyOffset(double offset);

    /// <summary>The current scalar offset; 0 is open and <see cref="GetClosedOffset"/> is closed.</summary>
    protected double CurrentOffset => GetValue(SheetOffsetProperty);

    /// <summary>Places the surface at the fully closed position without a transition.</summary>
    protected void ApplyClosedOffset() => SetCurrentValue(SheetOffsetProperty, GetClosedOffset());

    /// <summary>Places the surface at the fully open position without a transition.</summary>
    protected void ApplyOpenOffset() => SetCurrentValue(SheetOffsetProperty, 0d);

    /// <summary>Sets the scalar offset; a transition, when installed, animates the change.</summary>
    protected void SetSheetOffset(double offset) => SetCurrentValue(SheetOffsetProperty, offset);

    /// <summary>Called after the template parts have been resolved.</summary>
    protected virtual void OnSheetTemplateApplied(TemplateAppliedEventArgs e) { }

    /// <summary>Called when the modal chrome of the surface may need to change.</summary>
    protected virtual void OnSheetChromeChanged() { }

    /// <summary>The accessible name derived from the sheet anatomy, or null to keep a consumer name.</summary>
    protected virtual string? GetSheetAccessibleName() => null;

    /// <summary>Enables offset transitions with the supplied duration.</summary>
    protected void SetSlideTransition(TimeSpan duration)
    {
        Transitions = duration > TimeSpan.Zero
            ? new Transitions
            {
                new DoubleTransition
                {
                    Property = SheetOffsetProperty,
                    Duration = duration,
                    Easing = CreateEasing()
                }
            }
            : null;
    }

    /// <summary>Disables offset transitions so drag tracking is immediate.</summary>
    protected void ClearSlideTransition() => Transitions = null;

    /// <summary>Enables or clears the scrim opacity transition.</summary>
    protected void SetScrimTransition(TimeSpan duration)
    {
        if (_scrim is null)
            return;

        _scrim.Transitions = duration > TimeSpan.Zero
            ? new Transitions
            {
                new DoubleTransition
                {
                    Property = Visual.OpacityProperty,
                    Duration = duration,
                    Easing = CreateEasing()
                }
            }
            : null;
    }

    /// <summary>Sets the scrim opacity immediately.</summary>
    protected void SetScrimOpacity(double opacity)
    {
        if (_scrim is not null)
            _scrim.Opacity = opacity;
    }

    /// <summary>The scrim border, available after the template is applied.</summary>
    protected Border? Scrim => _scrim;

    /// <summary>Reads the entrance duration from the Material duration tokens.</summary>
    protected TimeSpan GetOpenDuration() => GetDuration("MaterialDurationMedium4", TimeSpan.FromMilliseconds(400));

    /// <summary>Reads the exit duration from the Material duration tokens.</summary>
    protected TimeSpan GetCloseDuration() => GetDuration("MaterialDurationShort4", TimeSpan.FromMilliseconds(200));

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

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_scrim is not null)
            _scrim.PointerPressed -= OnScrimPointerPressed;

        base.OnApplyTemplate(e);

        _root = e.NameScope.Find<Panel>("PART_Root");
        _scrim = e.NameScope.Find<Border>("PART_Scrim");
        _surface = e.NameScope.Find<Border>("PART_Surface");
        _actions = e.NameScope.Find<Control>("PART_Actions");

        if (_scrim is not null)
            _scrim.PointerPressed += OnScrimPointerPressed;

        if (_surface is not null)
        {
            _slide = new TranslateTransform();
            _surface.RenderTransform = _slide;
            _surface.Focusable = true;
            KeyboardNavigation.SetIsTabStop(_surface, false);
            ApplyOffset(CurrentOffset);
        }

        _templateApplied = true;
        UpdateActionsVisibility();
        UpdateAccessibleName();
        OnSheetTemplateApplied(e);
        UpdateModalState();

        if (IsOpen)
        {
            // A re-applied template (for example after a theme change) restores the open state
            // without replaying Opening/Opened; a sheet that was opened while collapsed animates.
            if (_openingRaised)
                RestoreOpenState();
            else
                BeginOpen();
        }
        else
        {
            if (_surface is not null)
            {
                ClearSlideTransition();
                ApplyClosedOffset();
            }
            if (_root is not null)
                _root.IsHitTestVisible = false;
            SetCurrentValue(Visual.IsVisibleProperty, false);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _topLevel = TopLevel.GetTopLevel(this);

        if (!_templateApplied || !IsOpen)
            return;

        // A sheet that was open while detached resumes in its open state without replaying events.
        ClearSlideTransition();
        ApplyOpenOffset();
        if (_root is not null)
            _root.IsHitTestVisible = true;
        if (_scrim is not null)
        {
            _scrim.IsVisible = IsModal;
            _scrim.Opacity = IsModal ? ScrimOpacity : 0;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _openingRaised = false;
        CancelTransition();
        _topLevel = null;
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsOpenProperty)
            HandleIsOpenChanged(change.GetNewValue<bool>());
        else if (change.Property == SheetOffsetProperty)
            ApplyOffset(change.GetNewValue<double>());
        else if (change.Property == IsModalProperty)
            HandleIsModalChanged();
        else if (change.Property == ShowActionsProperty || change.Property == ActionsProperty ||
                 change.Property == ActionsTemplateProperty)
            UpdateActionsVisibility();
        else if (change.Property == ScrimOpacityProperty && _scrim is not null && IsOpen && IsModal)
            _scrim.Opacity = ScrimOpacity;
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
            BeginOpen();
        else
            BeginClose();
    }

    private void RestoreOpenState()
    {
        ClearSlideTransition();
        ApplyOpenOffset();
        if (_root is not null)
            _root.IsHitTestVisible = true;
        if (_scrim is not null)
        {
            _scrim.IsVisible = IsModal;
            _scrim.Opacity = IsModal ? ScrimOpacity : 0;
        }
        UpdateModalState();
    }

    private void BeginOpen()
    {
        var args = new SheetOpeningEventArgs();
        Opening?.Invoke(this, args);
        if (args.Cancel)
        {
            _suppressOpenChange = true;
            try
            {
                SetCurrentValue(IsOpenProperty, false);
            }
            finally
            {
                _suppressOpenChange = false;
            }
            SetCurrentValue(Visual.IsVisibleProperty, false);
            _openingRaised = false;
            _opened?.TrySetResult(false);
            _opened = null;
            return;
        }

        _openingRaised = true;
        CancelTransition();
        _topLevel ??= TopLevel.GetTopLevel(this);
        var cts = new CancellationTokenSource();
        _transition = cts;
        _restoreFocus = false;
        _previousFocus = _topLevel?.FocusManager?.GetFocusedElement();
        _opened ??= new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _closed?.TrySetResult(false);
        _closed = null;

        SetCurrentValue(Visual.IsVisibleProperty, true);
        if (_root is not null)
            _root.IsHitTestVisible = true;
        if (_surface is not null)
            _surface.IsVisible = true;
        if (_scrim is not null)
        {
            _scrim.IsVisible = IsModal;
            _scrim.Opacity = 0;
        }
        UpdateModalState();

        // One layout pass is needed so the travel distance is known before the entrance motion.
        Dispatcher.UIThread.Post(() => RunOpenAnimation(cts.Token), DispatcherPriority.Loaded);
    }

    private void RunOpenAnimation(CancellationToken token)
    {
        if (token.IsCancellationRequested || !IsOpen || _surface is null)
            return;

        var duration = IsReducedMotion ? TimeSpan.Zero : GetOpenDuration();

        ClearSlideTransition();
        SetScrimTransition(TimeSpan.Zero);
        ApplyClosedOffset();
        if (_scrim is not null)
        {
            _scrim.IsVisible = IsModal;
            _scrim.Opacity = 0;
        }

        if (duration > TimeSpan.Zero)
        {
            SetSlideTransition(duration);
            SetScrimTransition(duration);
        }

        ApplyOpenOffset();
        if (_scrim is not null)
            _scrim.Opacity = IsModal ? ScrimOpacity : 0;

        FocusFirst();
        _ = CompleteOpenAsync(duration, token);
    }

    private async Task CompleteOpenAsync(TimeSpan duration, CancellationToken token)
    {
        try
        {
            if (duration > TimeSpan.Zero)
                await Task.Delay(duration, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (token.IsCancellationRequested || !IsOpen)
            return;

        _opened?.TrySetResult(true);
        _opened = null;
        Opened?.Invoke(this, EventArgs.Empty);
    }

    private void BeginClose()
    {
        var args = new SheetClosingEventArgs();
        Closing?.Invoke(this, args);
        if (args.Cancel)
        {
            _suppressOpenChange = true;
            try
            {
                SetCurrentValue(IsOpenProperty, true);
            }
            finally
            {
                _suppressOpenChange = false;
            }
            _closed?.TrySetResult(false);
            _closed = null;
            return;
        }

        // Capture containment before the sheet is hidden. A light dismiss must not steal focus
        // from a control the user has just chosen outside the sheet.
        var focus = _topLevel?.FocusManager?.GetFocusedElement();
        _restoreFocus = focus is not null && _surface is not null &&
                        (ReferenceEquals(focus, _surface) ||
                         (focus is Visual visual && _surface.IsVisualAncestorOf(visual)));

        _openingRaised = false;
        CancelTransition();
        var cts = new CancellationTokenSource();
        _transition = cts;
        _closed ??= new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _opened?.TrySetResult(false);
        _opened = null;

        var duration = IsReducedMotion ? TimeSpan.Zero : GetCloseDuration();
        if (duration > TimeSpan.Zero)
        {
            SetSlideTransition(duration);
            SetScrimTransition(duration);
        }
        else
        {
            ClearSlideTransition();
            SetScrimTransition(TimeSpan.Zero);
        }

        ApplyClosedOffset();
        if (_scrim is not null)
            _scrim.Opacity = 0;

        _ = CompleteCloseAsync(duration, cts.Token);
    }

    private async Task CompleteCloseAsync(TimeSpan duration, CancellationToken token)
    {
        try
        {
            if (duration > TimeSpan.Zero)
                await Task.Delay(duration, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (token.IsCancellationRequested || IsOpen)
            return;

        if (_scrim is not null)
        {
            _scrim.IsVisible = false;
            _scrim.Opacity = 0;
        }
        if (_root is not null)
            _root.IsHitTestVisible = false;
        SetCurrentValue(Visual.IsVisibleProperty, false);

        var previous = _previousFocus;
        var restore = _restoreFocus;
        _previousFocus = null;
        _restoreFocus = false;

        _closed?.TrySetResult(true);
        _closed = null;
        Closed?.Invoke(this, EventArgs.Empty);

        if (restore && previous is Control { IsEffectivelyEnabled: true, IsEffectivelyVisible: true } control &&
            control.IsAttachedToVisualTree())
            control.Focus(NavigationMethod.Unspecified);
    }

    private void CancelTransition()
    {
        var transition = _transition;
        _transition = null;
        if (transition is null)
            return;

        transition.Cancel();
        transition.Dispose();
    }

    private void HandleIsModalChanged()
    {
        UpdateModalState();

        if (_scrim is null)
            return;

        if (IsModal && IsOpen)
        {
            _scrim.IsVisible = true;
            SetScrimTransition(GetOpenDuration());
            _scrim.Opacity = ScrimOpacity;
        }
        else
        {
            SetScrimTransition(TimeSpan.Zero);
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

        e.Handled = true;
        SetCurrentValue(IsOpenProperty, false);
    }

    private void OnScrimPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsOpen || !IsDismissEnabled)
            return;

        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed && e.Pointer.Type != PointerType.Touch)
            return;

        e.Handled = true;
        SetCurrentValue(IsOpenProperty, false);
    }

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

    private TimeSpan GetDuration(string key, TimeSpan fallback)
    {
        if (IsReducedMotion)
            return TimeSpan.Zero;

        return this.TryFindResource(key, out var value) && value is TimeSpan duration ? duration : fallback;
    }

    private static Easing CreateEasing() =>
        new SplineEasing { X1 = 0.05, Y1 = 0.7, X2 = 0.1, Y2 = 1 };
}
