using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// Shared click semantics for Material surfaces that are only interactive on demand, such as
/// <see cref="MaterialAvatar"/> and <see cref="MaterialThumbnail"/>.
/// </summary>
/// <remarks>
/// <para>
/// The design source contains no behavior, so the interaction follows native Avalonia button
/// semantics: a primary-button press inside the surface followed by a release inside it raises
/// <see cref="Click"/>; a Space or Enter press-release gesture activates it while focused; a
/// <see cref="Command"/> runs only when <see cref="Click"/> was not handled, matching
/// <see cref="Button.OnClick"/>.
/// </para>
/// <para>
/// Keyboard activation is paired like the source clickable: the initial Space or Enter press
/// starts a gesture, auto-repeat presses are ignored, and the matching release raises
/// <see cref="Click"/> once. Moving focus away, turning interactivity or the effective enabled
/// state off, or detaching the surface cancels the pending gesture, so a held key cannot fire
/// after the surface stopped accepting input. The pointer gesture is likewise tied to the pointer
/// that captured it; capture loss cancels only the pointer gesture and leaves a held key intact.
/// </para>
/// <para>
/// The surface is inert unless <see cref="IsInteractive"/> is true. An inert surface takes no tab
/// stop, shows no pointer cursor and ignores pointer and key input, so an avatar used purely as
/// decoration does not interrupt keyboard navigation. Turning interactivity on sets
/// <see cref="InputElement.Focusable"/> and <c>KeyboardNavigation.IsTabStop</c> at
/// <see cref="BindingPriority.StyleTrigger"/> priority, which overrides the inert default a control
/// theme declares while leaving an explicit consumer local value in place.
/// </para>
/// <para>
/// The <c>:pointerover</c>, <c>:focus-visible</c> and <c>:disabled</c> pseudo-classes come from
/// Avalonia's own input and focus tracking. This class adds <c>:interactive</c> and
/// <c>:pressed</c> so a control theme can render a state layer without extra code.
/// </para>
/// </remarks>
public abstract class MaterialInteractionSurface : TemplatedControl
{
    /// <summary>Defines the <see cref="IsInteractive"/> property.</summary>
    public static readonly StyledProperty<bool> IsInteractiveProperty =
        AvaloniaProperty.Register<MaterialInteractionSurface, bool>(nameof(IsInteractive));

    /// <summary>Defines the <see cref="Command"/> property.</summary>
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<MaterialInteractionSurface, ICommand?>(nameof(Command));

    /// <summary>Defines the <see cref="CommandParameter"/> property.</summary>
    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<MaterialInteractionSurface, object?>(nameof(CommandParameter));

    /// <summary>Identifies the <see cref="Click"/> routed event.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> ClickEvent =
        RoutedEvent.Register<MaterialInteractionSurface, RoutedEventArgs>(
            nameof(Click), RoutingStrategies.Bubble);

    private static readonly Cursor s_hand = new(StandardCursorType.Hand);

    // Activation keys currently held, keyed by key so auto-repeat presses do not stack gestures.
    private readonly HashSet<Key> _activationKeys = [];

    private IPointer? _pointerCapture;
    private bool _pointerPressed;

    static MaterialInteractionSurface()
    {
        IsInteractiveProperty.Changed.AddClassHandler<MaterialInteractionSurface>(
            static (surface, _) => surface.UpdateInteraction());
    }

    /// <summary>Raised when the user activates the surface with the pointer, Space or Enter.</summary>
    public event EventHandler<RoutedEventArgs>? Click
    {
        add => AddHandler(ClickEvent, value);
        remove => RemoveHandler(ClickEvent, value);
    }

    /// <summary>
    /// Whether the surface reacts to pointer and keyboard activation. Defaults to false, which
    /// keeps decorative avatars and thumbnails out of the tab order.
    /// </summary>
    public bool IsInteractive
    {
        get => GetValue(IsInteractiveProperty);
        set => SetValue(IsInteractiveProperty, value);
    }

    /// <summary>Command executed when the surface is activated and <see cref="Click"/> is not handled.</summary>
    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    /// <summary>Parameter passed to <see cref="Command"/>.</summary>
    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    /// <summary>Invokes the surface exactly as a completed pointer or Space/Enter gesture would.</summary>
    /// <remarks>An inert or disabled surface does nothing.</remarks>
    public void Invoke()
    {
        if (!IsInteractive || !IsEffectivelyEnabled)
            return;

        var args = new RoutedEventArgs(ClickEvent, this);
        RaiseEvent(args);
        if (!args.Handled && Command is { } command && command.CanExecute(CommandParameter))
            command.Execute(CommandParameter);
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!IsInteractive || !IsEffectivelyEnabled || _pointerPressed ||
            !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        _pointerPressed = true;
        UpdatePressedClass();
        _pointerCapture = e.Pointer;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_pointerPressed || e.Pointer != _pointerCapture)
            return;

        // A drag that ends outside the surface cancels the activation, like a native button.
        var activate = IsInteractive && IsEffectivelyEnabled &&
                       e.InitialPressMouseButton == MouseButton.Left &&
                       new Rect(Bounds.Size).Contains(e.GetPosition(this));
        _pointerPressed = false;
        _pointerCapture = null;
        UpdatePressedClass();
        e.Pointer.Capture(null);
        if (!activate)
            return;

        e.Handled = true;
        Invoke();
    }

    /// <inheritdoc />
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        // Losing capture ends the pointer gesture but must not cancel a held activation key.
        _pointerCapture = null;
        _pointerPressed = false;
        UpdatePressedClass();
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || !IsInteractive || !IsEffectivelyEnabled || !IsActivationKey(e.Key))
            return;

        // Auto-repeat sends more downs for the key already held; the source ignores them, so the
        // gesture only starts once and still completes on the matching release.
        _activationKeys.Add(e.Key);
        UpdatePressedClass();
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (!_activationKeys.Remove(e.Key))
            return;

        UpdatePressedClass();
        e.Handled = true;
        if (!IsInteractive || !IsEffectivelyEnabled)
            return;

        Invoke();
    }

    /// <inheritdoc />
    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        // Focus left before the matching release, so the press can no longer complete.
        CancelKeyboardPress();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        // Disabling the surface or any ancestor cancels the pending gesture, including capture.
        if (change.Property == InputElement.IsEffectivelyEnabledProperty && !IsEffectivelyEnabled)
            CancelInteraction();
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        MaterialSurface.Connect(this, e.NameScope.Find<Border>("PART_Container"));
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // A recycled surface must not stay visually pressed, activate a stale key gesture or keep
        // the pointer captured.
        CancelInteraction();
    }

    private static bool IsActivationKey(Key key) => key is Key.Space or Key.Enter;

    private void UpdatePressedClass() =>
        PseudoClasses.Set(":pressed", _pointerPressed || _activationKeys.Count > 0);

    private void CancelKeyboardPress()
    {
        if (_activationKeys.Count == 0)
            return;

        _activationKeys.Clear();
        UpdatePressedClass();
    }

    private void CancelInteraction()
    {
        CancelKeyboardPress();
        var capture = _pointerCapture;
        _pointerCapture = null;
        _pointerPressed = false;
        UpdatePressedClass();
        if (capture?.Captured == this)
            capture.Capture(null);
    }

    private void UpdateInteraction()
    {
        var interactive = IsInteractive;
        PseudoClasses.Set(":interactive", interactive);
        Cursor = interactive ? s_hand : null;
        // A control theme may declare the inert default (the avatar theme sets Focusable=False).
        // IsInteractive is usually set in a consumer initializer, before that theme is applied, and
        // SetCurrentValue keeps the priority the property had at that moment - which is Unset, so
        // the later theme setter wins and an interactive surface never becomes focusable. Writing at
        // StyleTrigger outranks theme and template values independent of that ordering while an
        // explicit consumer local value still wins.
        SetValue(FocusableProperty, interactive, BindingPriority.StyleTrigger);
        SetValue(KeyboardNavigation.IsTabStopProperty, interactive, BindingPriority.StyleTrigger);
        if (!interactive)
            CancelInteraction();
    }
}
