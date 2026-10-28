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
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Theming;
using ExpressiveAvalonia.Tokens;

namespace ExpressiveAvalonia.Controls;

/// <summary>How the snackbar reserves lines of supporting text.</summary>
/// <remarks>
/// <para>
/// <see cref="Auto"/> is the source behavior: Snackbar.kt measures the supporting text with
/// unlimited lines, wraps it and grows the container beyond the 48 DIP single-line minimum, so a
/// long message is never clipped or ellipsized. The source has no line cap at all.
/// </para>
/// <para>
/// <see cref="OneLine"/> and <see cref="TwoLines"/> are library extensions kept for the catalog's
/// explicit anatomy controls. They cap the text (one line ellipsizes; two lines trim) and reserve
/// the fixed 48/68 DIP heights from SnackbarTokens.kt. Use them only when the design requires a
/// fixed cap; the default <see cref="Auto"/> follows the measured source layout.
/// </para>
/// </remarks>
public enum MaterialSnackbarLineCount { Auto, OneLine, TwoLines }

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
/// entrance springs and starts the automatic-dismiss timer. <see cref="Dismiss"/> starts the exit
/// springs and raises <see cref="Closed"/> once both have settled.
/// </para>
/// <para>
/// Durations follow SnackbarHostState: 4 seconds by default, indefinite whenever an action is
/// present (the source default for <c>actionLabel != null</c>), or the explicit
/// <see cref="Duration"/>. Android's <c>AccessibilityManager.calculateRecommendedTimeoutMillis</c>
/// has no Avalonia equivalent; set <see cref="Duration"/> from the application's accessibility
/// policy instead. Keeping the snackbar open while keyboard focus is inside it is an explicitly
/// marked desktop extension.
/// </para>
/// <para>
/// The supporting text follows Snackbar.kt's measured layout: it wraps without a line cap and the
/// container grows past the 48 DIP single-line minimum, so a long message is shown in full. The
/// explicit <see cref="LineCount"/> caps are library extensions; <see cref="MaterialSnackbarLineCount.Auto"/>
/// is the default and preserves the source behavior.
/// </para>
/// <para>
/// Reduced motion (through <see cref="MaterialMotion.IsReduced"/> or
/// <c>MaterialTheme.IsReducedMotion="True"</c>) makes both springs settle immediately.
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
        AvaloniaProperty.Register<MaterialSnackbar, MaterialSnackbarLineCount>(
            nameof(LineCount), MaterialSnackbarLineCount.Auto);

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
    private bool _detaching;
    private int _closeGeneration;
    private int _exitPartsRemaining;
    private MaterialSnackbarCloseReason? _lastCloseReason;
    private MaterialSnackbarCloseReason _exitReason;
    private DispatcherTimer? _autoDismissTimer;
    private Button? _actionButton;
    private Button? _closeButton;
    private StackPanel? _actionsPanel;
    private ScaleTransform? _scaleTransform;
    private MaterialSpringValue? _alpha;
    private MaterialSpringValue? _scale;

    public MaterialSnackbar()
    {
        // A snackbar is a status message: expose the container as a status bar. The message
        // TextBlock in the template is the polite live region, so assistive technology announces
        // the text when the snackbar appears without interrupting the user.
        AutomationProperties.SetControlTypeOverride(this, AutomationControlType.StatusBar);
        // The exit springs keep the snackbar in the tree briefly; it must not intercept pointer
        // input during that time (or while it has never been shown).
        IsHitTestVisible = false;
        // A snackbar that was never shown must not reserve layout space in a host panel.
        IsVisible = false;
        RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
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

    /// <summary>
    /// How the supporting text is measured: source auto sizing by default, or the explicit
    /// one/two-line cap extension.
    /// </summary>
    /// <remarks>
    /// <see cref="MaterialSnackbarLineCount.Auto"/> wraps the message and lets the container grow,
    /// matching Snackbar.kt's measured layout. <see cref="MaterialSnackbarLineCount.OneLine"/> and
    /// <see cref="MaterialSnackbarLineCount.TwoLines"/> cap the text and reserve the fixed
    /// 48/68 DIP heights from SnackbarTokens.kt.
    /// </remarks>
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
    /// <c>null</c> (the default) selects the source default: 4 seconds without an action and
    /// <see cref="Timeout.InfiniteTimeSpan"/> when an action is present. An explicit value
    /// overrides both and is the supported accessibility-timeout adaptation.
    /// A value is ignored while keyboard focus is inside the snackbar; the full duration restarts
    /// when focus leaves (desktop extension).
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

    /// <summary>Raised once when the snackbar starts closing, before the exit springs.</summary>
    public event EventHandler<MaterialSnackbarCloseEventArgs>? Closing;

    /// <summary>Raised when the exit springs have finished and the snackbar may be removed.</summary>
    public event EventHandler<MaterialSnackbarCloseEventArgs>? Closed;

    /// <summary>Raised after the action button runs its command.</summary>
    public event EventHandler? ActionInvoked;

    /// <summary>Makes the snackbar visible and starts its automatic-dismiss timer.</summary>
    public void Show()
    {
        _closeGeneration++;
        _exitPartsRemaining = 0;
        _detaching = false;

        var opened = false;
        if (!_isOpen)
        {
            SetAndRaise(IsOpenProperty, ref _isOpen, true);
            SetAndRaise(LastCloseReasonProperty, ref _lastCloseReason, null);
            opened = true;
        }

        SetCurrentValue(Visual.IsVisibleProperty, true);
        IsHitTestVisible = true;

        if (this.IsAttachedToVisualTree())
        {
            _showOnAttach = false;
            if (opened)
                BeginOpenAnimation(restart: true);
            else
                AnimateOpen();
            RestartAutoDismissTimer();
        }
        else
        {
            // Defer the class change until the control has a template and a rendered frame, so
            // a snackbar shown before it is added to a host still gets its entrance springs.
            _showOnAttach = true;
        }

        if (opened)
            Opened?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Starts the exit springs; <paramref name="reason"/> is reported to listeners.</summary>
    public void Dismiss(MaterialSnackbarCloseReason reason = MaterialSnackbarCloseReason.Programmatic)
    {
        if (!_isOpen)
            return;

        StopAutoDismissTimer();
        _showOnAttach = false;
        _entrancePending = false;

        SetAndRaise(IsOpenProperty, ref _isOpen, false);
        SetAndRaise(LastCloseReasonProperty, ref _lastCloseReason, reason);
        IsHitTestVisible = false;
        Closing?.Invoke(this, new MaterialSnackbarCloseEventArgs(reason));

        var generation = ++_closeGeneration;
        _exitReason = reason;

        if (!this.IsAttachedToVisualTree() || MaterialMotion.GetIsReduced(this) || _alpha is null || _scale is null)
        {
            _alpha?.JumpTo(0);
            _scale?.JumpTo(0);
            RaiseClosed(reason, generation);
            return;
        }

        _exitPartsRemaining = 2;
        _alpha.AnimateTo(0, MaterialSpring.EffectsFast,
            () => ExitPartSettled(generation), 0.01);
        _scale.AnimateTo(0, MaterialSpring.SpatialFast,
            () => ExitPartSettled(generation), 0.01);
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

        _scaleTransform = new ScaleTransform(1, 1);
        RenderTransform = _scaleTransform;
        EnsureDrivers();
        JumpToOpenState(_showOnAttach ? false : _isOpen);

        UpdateVisualState();
        if (_isOpen && !_showOnAttach && !_entrancePending)
            AnimateOpen();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TextProperty || change.Property == ActionContentProperty ||
            change.Property == IsCloseButtonVisibleProperty || change.Property == LineCountProperty ||
            change.Property == ActionPlacementProperty)
        {
            UpdateVisualState();
            if (change.Property == ActionContentProperty && _isOpen)
                RestartAutoDismissTimer();
        }
        else if (change.Property == DurationProperty && _isOpen)
        {
            RestartAutoDismissTimer();
        }
        else if (change.Property == IsKeyboardFocusWithinProperty && _isOpen)
        {
            // Desktop extension: never yank a snackbar away while a keyboard user is inside it.
            if (IsKeyboardFocusWithin)
                StopAutoDismissTimer();
            else
                RestartAutoDismissTimer();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _detaching = false;
        if (_scaleTransform is null)
        {
            _scaleTransform = new ScaleTransform(1, 1);
            RenderTransform = _scaleTransform;
        }
        EnsureDrivers();

        if (!_isOpen)
        {
            _showOnAttach = false;
            JumpToOpenState(false);
            return;
        }

        if (_showOnAttach)
        {
            _showOnAttach = false;
            BeginOpenAnimation(restart: true);
        }
        else
        {
            JumpToOpenState(true);
        }

        RestartAutoDismissTimer();
    }

    /// <summary>
    /// Applies the open style after the first frame, so the hidden initial style is the starting
    /// point of the entrance springs instead of being replaced in the same layout pass.
    /// </summary>
    private void BeginOpenAnimation(bool restart)
    {
        if (_entrancePending)
            return;

        _entrancePending = true;
        if (restart)
            JumpToOpenState(false);

        // Background runs after the render pass, so the hidden state is committed first.
        Dispatcher.UIThread.Post(
            () =>
            {
                _entrancePending = false;
                if (_isOpen)
                    AnimateOpen();
            },
            DispatcherPriority.Background);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _detaching = true;
        StopAutoDismissTimer();
        _alpha?.Dispose();
        _alpha = null;
        _scale?.Dispose();
        _scale = null;
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

    private void EnsureDrivers()
    {
        if (_scaleTransform is null)
            return;

        var initial = _isOpen && !_entrancePending ? 1 : 0;
        if (_alpha is null)
            _alpha = new MaterialSpringValue(this, initial,
                value => Opacity = Math.Clamp(value, 0, 1));
        if (_scale is null)
            _scale = new MaterialSpringValue(this, initial, ApplyScale);
    }

    /// <summary>Scale 0 is 0.8 and scale 1 is 1.0, matching animatedScale in SnackbarHost.kt.</summary>
    private void ApplyScale(double value)
    {
        if (_scaleTransform is null)
            return;
        var scale = 0.8 + (0.2 * Math.Clamp(value, 0, 1));
        _scaleTransform.ScaleX = scale;
        _scaleTransform.ScaleY = scale;
    }

    private void JumpToOpenState(bool open)
    {
        _alpha?.JumpTo(open ? 1 : 0);
        _scale?.JumpTo(open ? 1 : 0);
        IsHitTestVisible = open;
    }

    private void AnimateOpen()
    {
        if (!_isOpen)
            return;
        _alpha?.AnimateTo(1, MaterialSpring.EffectsFast, null, 0.01);
        _scale?.AnimateTo(1, MaterialSpring.SpatialFast, null, 0.01);
    }

    private void ExitPartSettled(int generation)
    {
        if (generation != _closeGeneration || _isOpen)
            return;

        _exitPartsRemaining--;
        if (_exitPartsRemaining <= 0)
            RaiseClosed(_exitReason, generation);
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
        var lineCount = LineCount;
        // Auto (the source measured layout) and the explicit TwoLines extension share the
        // wrapping anatomy and its padding matrix; only the explicit caps change the text limit.
        PseudoClasses.Set(":one-line", lineCount == MaterialSnackbarLineCount.OneLine);
        PseudoClasses.Set(":two-line", lineCount == MaterialSnackbarLineCount.TwoLines);
        PseudoClasses.Set(":multi-line", lineCount != MaterialSnackbarLineCount.OneLine);
        PseudoClasses.Set(":with-action", hasAction);
        PseudoClasses.Set(":with-close", IsCloseButtonVisible);
        PseudoClasses.Set(":stacked-action", hasAction && ActionPlacement == MaterialSnackbarActionPlacement.Below);

        if (_actionButton is not null)
            _actionButton.IsVisible = hasAction;
        if (_actionsPanel is not null)
            _actionsPanel.IsVisible = hasAction || IsCloseButtonVisible;
    }

    private TimeSpan ResolveDuration()
    {
        if (Duration is { } configured)
            return configured;

        // SnackbarHostState.showSnackbar: Indefinite when an action is present, Short otherwise.
        if (ActionContent is not null)
            return Timeout.InfiniteTimeSpan;

        return TimeSpan.FromSeconds(4);
    }

    private void RestartAutoDismissTimer()
    {
        StopAutoDismissTimer();
        if (!_isOpen || !this.IsAttachedToVisualTree() || IsKeyboardFocusWithin || _detaching)
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

    private void RaiseClosed(MaterialSnackbarCloseReason reason, int generation)
    {
        if (generation != _closeGeneration || _isOpen)
            return; // Shown again while the exit springs were running.

        _exitPartsRemaining = 0;
        SetCurrentValue(Visual.IsVisibleProperty, false);
        Closed?.Invoke(this, new MaterialSnackbarCloseEventArgs(reason));
    }
}