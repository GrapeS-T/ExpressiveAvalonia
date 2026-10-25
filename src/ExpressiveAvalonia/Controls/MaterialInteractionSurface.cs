using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// Shared click semantics for Material surfaces that are only interactive on demand, such as
/// <see cref="MaterialAvatar"/> and <see cref="MaterialThumbnail"/>.
/// </summary>
/// <remarks>
/// <para>
/// The design source contains no behavior, so the interaction follows native Avalonia button
/// semantics: a primary-button press inside the surface followed by a release inside it raises
/// <see cref="Click"/>; Space and Enter activate it while focused; a <see cref="Command"/> runs
/// only when <see cref="Click"/> was not handled, matching <see cref="Button.OnClick"/>.
/// </para>
/// <para>
/// The surface is inert unless <see cref="IsInteractive"/> is true. An inert surface takes no tab
/// stop, shows no pointer cursor and ignores pointer and key input, so an avatar used purely as
/// decoration does not interrupt keyboard navigation. Turning interactivity on sets
/// <see cref="InputElement.Focusable"/> and <c>KeyboardNavigation.IsTabStop</c> through
/// <c>SetCurrentValue</c>, which leaves an explicit consumer value in place.
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

    private bool _pressed;

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

    /// <summary>Invokes the surface exactly as a pointer release or Space/Enter would.</summary>
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
        if (!IsInteractive || !IsEffectivelyEnabled || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        _pressed = true;
        PseudoClasses.Set(":pressed", true);
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_pressed)
            return;

        _pressed = false;
        PseudoClasses.Set(":pressed", false);
        e.Pointer.Capture(null);
        if (!IsInteractive || !IsEffectivelyEnabled || e.InitialPressMouseButton != MouseButton.Left)
            return;
        // A drag that ends outside the surface cancels the activation, like a native button.
        if (!new Rect(Bounds.Size).Contains(e.GetPosition(this)))
            return;

        e.Handled = true;
        Invoke();
    }

    /// <inheritdoc />
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _pressed = false;
        PseudoClasses.Set(":pressed", false);
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!IsInteractive || !IsEffectivelyEnabled || e.Handled)
            return;
        if (e.Key is not (Key.Space or Key.Enter))
            return;

        e.Handled = true;
        Invoke();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // A recycled surface must not stay visually pressed or keep the pointer captured.
        _pressed = false;
        PseudoClasses.Set(":pressed", false);
    }

    private void UpdateInteraction()
    {
        var interactive = IsInteractive;
        PseudoClasses.Set(":interactive", interactive);
        Cursor = interactive ? s_hand : null;
        SetCurrentValue(FocusableProperty, interactive);
        SetCurrentValue(KeyboardNavigation.IsTabStopProperty, interactive);
        if (!interactive)
        {
            _pressed = false;
            PseudoClasses.Set(":pressed", false);
        }
    }
}
