using System;
using System.Threading;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Controls;

/// <summary>How many lines of supporting text the snackbar reserves.</summary>
/// <remarks>
/// Mirrors the source component's "# of lines" axis. Text still wraps and is trimmed at the
/// configured limit; the value only fixes the vertical anatomy (48 DIP one-line, 68 DIP two-line).
/// </remarks>
public enum MaterialSnackbarLineCount { OneLine, TwoLines }

/// <summary>Where the action button sits relative to the supporting text.</summary>
/// <remarks>
/// <see cref="Inline"/> is the source's "Text &amp; action" configuration: the action shares the
/// text row. <see cref="Below"/> is the "Text &amp; longer action" configuration: the action gets
/// its own row under the text so a long label never squeezes the message.
/// </remarks>
public enum MaterialSnackbarActionPlacement { Inline, Below }

/// <summary>Why a snackbar left the screen.</summary>
public enum MaterialSnackbarCloseReason
{
    /// <summary>The automatic duration elapsed.</summary>
    Timeout,
    /// <summary>The close affordance or Escape was used.</summary>
    UserDismiss,
    /// <summary>The action button was activated (its command has already run).</summary>
    Action,
    /// <summary><see cref="MaterialSnackbar.Dismiss"/> or a host removed it.</summary>
    Programmatic,
}

/// <summary>Event data for <see cref="MaterialSnackbar.Closing"/> and <see cref="MaterialSnackbar.Closed"/>.</summary>
public sealed class MaterialSnackbarCloseEventArgs : EventArgs
{
    public MaterialSnackbarCloseEventArgs(MaterialSnackbarCloseReason reason) => Reason = reason;

    public MaterialSnackbarCloseReason Reason { get; }
}

/// <summary>
/// A Material 3 snackbar: inverse-surface message with an optional action and close affordance.
/// </summary>
/// <remarks>
/// <para>
/// The control is a plain templated control so it can be placed directly in a panel or queued by
/// <see cref="MaterialSnackbarHost"/>. It starts hidden; <see cref="Show"/> reveals it, starts the
/// entrance transition and starts the automatic-dismiss timer. <see cref="Dismiss"/> begins the
/// exit transition and raises <see cref="Closed"/> once the transition would have finished.
/// </para>
/// <para>
/// Reduced motion (through <see cref="MaterialMotion.IsReduced"/> or
/// <c>MaterialTheme.Motion="Reduced"</c>) zeroes the duration tokens, so both transitions collapse
/// and the snackbar closes synchronously. Escape dismisses an open snackbar when focus is inside
/// it, and the automatic timer is suspended while keyboard focus is inside.
/// </para>
/// </remarks>
public sealed class MaterialSnackbar : TemplatedControl
{
    /// <summary>Defines the <see cref="Text"/> property.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<MaterialSnackbar, string?>(nameof(Text));

    /// <summary>Defines the <see cref="ActionContent"/> property.</summary>
    public static readonly StyledProperty<object?> ActionContentProperty =
        AvaloniaProperty.Register<MaterialSnackbar, object?>(nameof(ActionContent));

    /// <summary>Defines the <see cref="ActionContentTemplate"/> property.</summary>
    public static readonly StyledProperty<IDataTemplate?> ActionContentTemplateProperty =
        AvaloniaProperty.Register<MaterialSnackbar, IDataTemplate?>(nameof(ActionContentTemplate));

    /// <summary>Defines the <see cref="ActionCommand"/> property.</summary>
    public static readonly StyledProperty<ICommand?> ActionCommandProperty =
        AvaloniaProperty.Register<MaterialSnackbar, ICommand?>(nameof(ActionCommand));

    /// <summary>Defines the <see cref="ActionCommandParameter"/> property.</summary>
    public static readonly StyledProperty<object?> ActionCommandParameterProperty =
        AvaloniaProperty.Register<MaterialSnackbar, object?>(nameof(ActionCommandParameter));

    /// <summary>Defines the <see cref="IsCloseButtonVisible"/> property.</summary>
    public static readonly StyledProperty<bool> IsCloseButtonVisibleProperty =
        AvaloniaProperty.Register<MaterialSnackbar, bool>(nameof(IsCloseButtonVisible));

    /// <summary>Defines the <see cref="LineCount"/> property.</summary>
    public static readonly StyledProperty<MaterialSnackbarLineCount> LineCountProperty =
        AvaloniaProperty.Register<MaterialSnackbar, MaterialSnackbarLineCount>(nameof(LineCount));

    /// <summary>Defines the <see cref="ActionPlacement"/> property.</summary>
    public static readonly StyledProperty<MaterialSnackbarActionPlacement> ActionPlacementProperty =
        AvaloniaProperty.Register<MaterialSnackbar, MaterialSnackbarActionPlacement>(nameof(ActionPlacement));

    /// <summary>Defines the <see cref="Duration"/> property.</summary>
    public static readonly StyledProperty<TimeSpan?> DurationProperty =
        AvaloniaProperty.Register<MaterialSnackbar, TimeSpan?>(nameof(Duration));

    /// <summary>Defines the <see cref="IsOpen"/> property.</summary>
    public static readonly DirectProperty<MaterialSnackbar, bool> IsOpenProperty =
        AvaloniaProperty.RegisterDirect<MaterialSnackbar, bool>(nameof(IsOpen), o => o._isOpen);

    /// <summary>Defines the <see cref="LastCloseReason"/> property.</summary>
    public static readonly DirectProperty<MaterialSnackbar, MaterialSnackbarCloseReason?> LastCloseReasonProperty =
        AvaloniaProperty.RegisterDirect<MaterialSnackbar, MaterialSnackbarCloseReason?>(
            nameof(LastCloseReason), o => o._lastCloseReason);

    private bool _isOpen;
    private bool _showOnAttach;
    private bool _entrancePending;
    private MaterialSnackbarCloseReason? _lastCloseReason;
    private IDisposable? _closeOperation;
    private DispatcherTimer? _autoDismissTimer;
    private Button? _actionButton;
    private Button? _closeButton;
    private StackPanel? _actionsPanel;

    public MaterialSnackbar()
    {
        // A snackbar is a status message: expose the container as a status bar. The message
        // TextBlock in the template is the polite live region, so assistive technology announces
        // the text when the snackbar appears without interrupting the user.
        AutomationProperties.SetControlTypeOverride(this, AutomationControlType.StatusBar);
        // The exit transition keeps the snackbar in the tree for one duration token; it must not
        // intercept pointer input during that time (or while it has never been shown).
        IsHitTestVisible = false;
        // A snackbar that was never shown must not reserve layout space in a host panel.
        IsVisible = false;
    }

    /// <summary>The supporting message. Live-announced politely when it appears.</summary>
    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>
    /// The action label (a string) or any content rendered inside the action button.
    /// When null the action button and its reserved width are omitted.
    /// </summary>
    public object? ActionContent
    {
        get => GetValue(ActionContentProperty);
        set => SetValue(ActionContentProperty, value);
    }

    public IDataTemplate? ActionContentTemplate
    {
        get => GetValue(ActionContentTemplateProperty);
        set => SetValue(ActionContentTemplateProperty, value);
    }

    /// <summary>Executed by the action button; <c>CanExecute</c> disables the button.</summary>
    public ICommand? ActionCommand
    {
        get => GetValue(ActionCommandProperty);
        set => SetValue(ActionCommandProperty, value);
    }

    public object? ActionCommandParameter
    {
        get => GetValue(ActionCommandParameterProperty);
        set => SetValue(ActionCommandParameterProperty, value);
    }

    /// <summary>Shows the 48-DIP close affordance ("Show close affordance" in the source).</summary>
    public bool IsCloseButtonVisible
    {
        get => GetValue(IsCloseButtonVisibleProperty);
        set => SetValue(IsCloseButtonVisibleProperty, value);
    }

    /// <summary>One or two lines of supporting text; also selects the 48/68 DIP anatomy.</summary>
    public MaterialSnackbarLineCount LineCount
    {
        get => GetValue(LineCountProperty);
        set => SetValue(LineCountProperty, value);
    }

    /// <summary>Whether the action shares the text row or gets its own row underneath.</summary>
    public MaterialSnackbarActionPlacement ActionPlacement
    {
        get => GetValue(ActionPlacementProperty);
        set => SetValue(ActionPlacementProperty, value);
    }

    /// <summary>
    /// How long the snackbar stays before closing itself.
    /// </summary>
    /// <remarks>
    /// <c>null</c> (the default) selects the Material default: 4 seconds for a one-line message
    /// without an action, 10 seconds for two lines or whenever an action is present (Material's
    /// accessibility guidance keeps actionable snackbars on screen longer).
    /// <see cref="Timeout.InfiniteTimeSpan"/> keeps it open until dismissed explicitly.
    /// A value is ignored while keyboard focus is inside the snackbar; the full duration
    /// restarts when focus leaves.
    /// </remarks>
    public TimeSpan? Duration
    {
        get => GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    /// <summary>True between <see cref="Show"/> and <see cref="Dismiss"/>.</summary>
    public bool IsOpen => _isOpen;

    /// <summary>The reason supplied to the most recent <see cref="Dismiss"/> call.</summary>
    public MaterialSnackbarCloseReason? LastCloseReason => _lastCloseReason;

    /// <summary>Raised when <see cref="Show"/> transitions the snackbar from closed to open.</summary>
    public event EventHandler? Opened;

    /// <summary>Raised once when the snackbar starts closing, before the exit transition.</summary>
    public event EventHandler<MaterialSnackbarCloseEventArgs>? Closing;

    /// <summary>Raised when the exit transition has finished and the snackbar may be removed.</summary>
    public event EventHandler<MaterialSnackbarCloseEventArgs>? Closed;

    /// <summary>Raised after the action button runs its command.</summary>
    public event EventHandler? ActionInvoked;

    /// <summary>Makes the snackbar visible and starts its automatic-dismiss timer.</summary>
    public void Show()
    {
        _closeOperation?.Dispose();
        _closeOperation = null;

        var opened = false;
        if (!_isOpen)
        {
            SetAndRaise(IsOpenProperty, ref _isOpen, true);
            SetAndRaise(LastCloseReasonProperty, ref _lastCloseReason, null);
            opened = true;
        }

        SetCurrentValue(Visual.IsVisibleProperty, true);

        if (this.IsAttachedToVisualTree())
        {
            if (opened)
                BeginOpenAnimation();
            else
                UpdateOpenState(open: true);
            RestartAutoDismissTimer();
        }
        else
        {
            // Defer the class change until the control has a template and a rendered frame, so
            // a snackbar shown before it is added to a host still gets its entrance transition.
            _showOnAttach = true;
        }

        if (opened)
            Opened?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Starts the exit transition; <paramref name="reason"/> is reported to listeners.</summary>
    public void Dismiss(MaterialSnackbarCloseReason reason = MaterialSnackbarCloseReason.Programmatic)
    {
        if (!_isOpen)
            return;

        StopAutoDismissTimer();
        _closeOperation?.Dispose();
        _closeOperation = null;
        _showOnAttach = false;
        _entrancePending = false;

        SetAndRaise(IsOpenProperty, ref _isOpen, false);
        SetAndRaise(LastCloseReasonProperty, ref _lastCloseReason, reason);
        UpdateOpenState(open: false);
        Closing?.Invoke(this, new MaterialSnackbarCloseEventArgs(reason));

        var delay = this.IsAttachedToVisualTree() ? AnimationDuration : TimeSpan.Zero;
        if (delay <= TimeSpan.Zero)
            RaiseClosed(reason);
        else
            _closeOperation = DispatcherTimer.RunOnce(() => RaiseClosed(reason), delay, DispatcherPriority.Background);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_actionButton is not null)
            _actionButton.Click -= OnActionClick;
        if (_closeButton is not null)
            _closeButton.Click -= OnCloseClick;

        base.OnApplyTemplate(e);

        _actionButton = e.NameScope.Find<Button>("PART_Action");
        _closeButton = e.NameScope.Find<Button>("PART_Close");
        _actionsPanel = e.NameScope.Find<StackPanel>("PART_Actions");

        if (_actionButton is not null)
            _actionButton.Click += OnActionClick;
        if (_closeButton is not null)
            _closeButton.Click += OnCloseClick;

        UpdateVisualState();
        // A control that is already open and attached (for example after a theme change) keeps its
        // open style; a pending entrance owns the :open change so the transition can animate.
        if (!_entrancePending)
            UpdateOpenState(_isOpen);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TextProperty || change.Property == ActionContentProperty ||
            change.Property == IsCloseButtonVisibleProperty || change.Property == LineCountProperty ||
            change.Property == ActionPlacementProperty)
        {
            UpdateVisualState();
        }
        else if (change.Property == DurationProperty && _isOpen)
        {
            RestartAutoDismissTimer();
        }
        else if (change.Property == IsKeyboardFocusWithinProperty && _isOpen)
        {
            // Never yank a snackbar away while a keyboard user is inside it.
            if (IsKeyboardFocusWithin)
                StopAutoDismissTimer();
            else
                RestartAutoDismissTimer();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!_isOpen)
        {
            _showOnAttach = false;
            return;
        }

        if (_showOnAttach)
        {
            _showOnAttach = false;
            BeginOpenAnimation();
        }

        RestartAutoDismissTimer();
    }

    /// <summary>
    /// Applies the open style after the first frame, so the hidden initial style is the starting
    /// point of the entrance transition instead of being replaced in the same layout pass.
    /// </summary>
    private void BeginOpenAnimation()
    {
        if (_entrancePending)
            return;

        _entrancePending = true;
        // Background runs after the render pass, so the zero-opacity frame is committed first.
        Dispatcher.UIThread.Post(
            () =>
            {
                _entrancePending = false;
                if (_isOpen)
                    UpdateOpenState(open: true);
            },
            DispatcherPriority.Background);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        StopAutoDismissTimer();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key == Key.Escape && _isOpen)
        {
            Dismiss(MaterialSnackbarCloseReason.UserDismiss);
            e.Handled = true;
        }
    }

    private void OnActionClick(object? sender, RoutedEventArgs e)
    {
        ActionInvoked?.Invoke(this, EventArgs.Empty);
        // The button runs ActionCommand itself; the snackbar then leaves like Material does.
        Dismiss(MaterialSnackbarCloseReason.Action);
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) =>
        Dismiss(MaterialSnackbarCloseReason.UserDismiss);

    private void UpdateVisualState()
    {
        var hasAction = ActionContent is not null;
        PseudoClasses.Set(":two-line", LineCount == MaterialSnackbarLineCount.TwoLines);
        PseudoClasses.Set(":with-action", hasAction);
        PseudoClasses.Set(":with-close", IsCloseButtonVisible);
        PseudoClasses.Set(":stacked-action", hasAction && ActionPlacement == MaterialSnackbarActionPlacement.Below);

        if (_actionButton is not null)
            _actionButton.IsVisible = hasAction;
        if (_actionsPanel is not null)
            _actionsPanel.IsVisible = hasAction || IsCloseButtonVisible;
    }

    private void UpdateOpenState(bool open)
    {
        PseudoClasses.Set(":open", open);
        // The exit transition is short; keep hit-testing off while it plays out.
        IsHitTestVisible = open;
    }

    private TimeSpan ResolveDuration()
    {
        if (Duration is { } configured)
            return configured;

        // Material keeps actionable snackbars visible longer than a plain status message.
        if (ActionContent is not null)
            return TimeSpan.FromSeconds(10);

        return LineCount == MaterialSnackbarLineCount.TwoLines
            ? TimeSpan.FromSeconds(10)
            : TimeSpan.FromSeconds(4);
    }

    private TimeSpan AnimationDuration
    {
        get
        {
            if (MaterialMotion.GetIsReduced(this))
                return TimeSpan.Zero;
            return this.TryFindResource("MaterialDurationShort4", out var value) && value is TimeSpan duration
                ? duration
                : TimeSpan.FromMilliseconds(200);
        }
    }

    private void RestartAutoDismissTimer()
    {
        StopAutoDismissTimer();
        if (!_isOpen || !this.IsAttachedToVisualTree() || IsKeyboardFocusWithin)
            return;

        var duration = ResolveDuration();
        if (duration == Timeout.InfiniteTimeSpan)
            return;

        if (duration <= TimeSpan.Zero)
        {
            Dismiss(MaterialSnackbarCloseReason.Timeout);
            return;
        }

        _autoDismissTimer = new DispatcherTimer { Interval = duration };
        _autoDismissTimer.Tick += OnAutoDismissTick;
        _autoDismissTimer.Start();
    }

    private void OnAutoDismissTick(object? sender, EventArgs e)
    {
        StopAutoDismissTimer();
        Dismiss(MaterialSnackbarCloseReason.Timeout);
    }

    private void StopAutoDismissTimer()
    {
        if (_autoDismissTimer is null)
            return;
        _autoDismissTimer.Stop();
        _autoDismissTimer.Tick -= OnAutoDismissTick;
        _autoDismissTimer = null;
    }

    private void RaiseClosed(MaterialSnackbarCloseReason reason)
    {
        _closeOperation = null;
        if (_isOpen)
            return; // Shown again while the exit transition was running.
        SetCurrentValue(Visual.IsVisibleProperty, false);
        Closed?.Invoke(this, new MaterialSnackbarCloseEventArgs(reason));
    }
}
