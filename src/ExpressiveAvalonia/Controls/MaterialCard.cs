using System;
using System.Diagnostics;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;

namespace ExpressiveAvalonia.Controls;

/// <summary>The container treatment of a <see cref="MaterialCard"/>.</summary>
/// <remarks>
/// The three members mirror the <c>Style</c> axis of the source card packages
/// (Stacked card <c>local-004836</c>, Horizontal card <c>local-001649</c> and the
/// <c>.Building Blocks/Card states</c> packages <c>local-000197</c>/<c>local-000203</c>/
/// <c>local-000209</c>).
/// </remarks>
public enum MaterialCardVariant
{
    /// <summary>Surface container with a 1 dp outline and no resting elevation.</summary>
    Outlined,

    /// <summary>Surface-container-low with a level-1 resting elevation.</summary>
    Elevated,

    /// <summary>Surface-container-highest with neither outline nor resting elevation.</summary>
    Filled,
}

/// <summary>The <c>Layout</c> axis of the source card packages.</summary>
public enum MaterialCardLayout
{
    /// <summary>The fixed header/media/headline/supporting-text/actions anatomy.</summary>
    MediaText,

    /// <summary>The card presents only its <see cref="ContentControl.Content"/> slot.</summary>
    Slot,
}

/// <summary>Which source card package supplies the anatomy.</summary>
public enum MaterialCardOrientation
{
    /// <summary>Stacked card: header, full-width media, then the text block.</summary>
    Stacked,

    /// <summary>Horizontal card: avatar, header text and an 80 dp square media thumbnail.</summary>
    Horizontal,
}

/// <summary>Reports the platform result of a card drag operation.</summary>
public sealed class MaterialCardDragCompletedEventArgs(DragDropEffects effects) : EventArgs
{
    /// <summary>Gets the effect the drop target reported, or <see cref="DragDropEffects.None"/>.</summary>
    public DragDropEffects Effects { get; } = effects;
}

/// <summary>
/// A Material Design 3 card with the filled, elevated and outlined containers, the stacked and
/// horizontal anatomies, the source's enabled/hovered/focused/pressed/dragged states and the
/// standard Avalonia keyboard, pointer, command and automation behavior.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Layout"/> selects between the structured <see cref="MaterialCardLayout.MediaText"/>
/// anatomy and the bare <see cref="MaterialCardLayout.Slot"/> content slot. In slot mode
/// <see cref="ContentControl.Content"/>, <see cref="ContentControl.ContentTemplate"/> and
/// <see cref="Padding"/> define the whole card surface, matching the source
/// <c>Layout=Slot</c> variants where the content slot replaces the anatomy.
/// </para>
/// <para>
/// A card is inert by default: it groups content and never takes focus. Setting
/// <see cref="IsInteractive"/> makes it a focusable, clickable surface with the Material state
/// layers, hover elevation, focus ring, Enter/Space activation and an automation Invoke pattern.
/// Nested interactive content keeps working because pointer events that a child handles never
/// reach the card.
/// </para>
/// <para>
/// Dragging is opt-in through <see cref="DragData"/> on an interactive card: pressing the card
/// and moving past the drag threshold starts a platform drag-and-drop operation, applies the
/// source's dragged state (0.16 state layer plus the raised elevation) while the operation runs
/// and reports the result through <see cref="DragCompleted"/>.
/// </para>
/// </remarks>
[PseudoClasses(":pressed", ":dragged", ":interactive")]
public class MaterialCard : ContentControl
{
    private const string PressedPseudoClass = ":pressed";
    private const string DraggedPseudoClass = ":dragged";

    // A few device-independent units of slop keep a click from becoming a drag.
    private const double DragThreshold = 4;

    /// <summary>Defines the <see cref="Variant"/> property.</summary>
    public static readonly StyledProperty<MaterialCardVariant> VariantProperty =
        AvaloniaProperty.Register<MaterialCard, MaterialCardVariant>(nameof(Variant));

    /// <summary>Defines the <see cref="Layout"/> property.</summary>
    public static readonly StyledProperty<MaterialCardLayout> LayoutProperty =
        AvaloniaProperty.Register<MaterialCard, MaterialCardLayout>(nameof(Layout));

    /// <summary>Defines the <see cref="Orientation"/> property.</summary>
    public static readonly StyledProperty<MaterialCardOrientation> OrientationProperty =
        AvaloniaProperty.Register<MaterialCard, MaterialCardOrientation>(nameof(Orientation));

    /// <summary>Defines the <see cref="IsInteractive"/> property.</summary>
    public static readonly StyledProperty<bool> IsInteractiveProperty =
        AvaloniaProperty.Register<MaterialCard, bool>(nameof(IsInteractive));

    /// <summary>Defines the <see cref="ShowFocusIndicator"/> property.</summary>
    public static readonly StyledProperty<bool> ShowFocusIndicatorProperty =
        AvaloniaProperty.Register<MaterialCard, bool>(nameof(ShowFocusIndicator));

    /// <summary>Defines the <see cref="Header"/> property.</summary>
    public static readonly StyledProperty<string?> HeaderProperty =
        AvaloniaProperty.Register<MaterialCard, string?>(nameof(Header));

    /// <summary>Defines the <see cref="Subhead"/> property.</summary>
    public static readonly StyledProperty<string?> SubheadProperty =
        AvaloniaProperty.Register<MaterialCard, string?>(nameof(Subhead));

    /// <summary>Defines the <see cref="Avatar"/> property.</summary>
    public static readonly StyledProperty<object?> AvatarProperty =
        AvaloniaProperty.Register<MaterialCard, object?>(nameof(Avatar));

    /// <summary>Defines the <see cref="AvatarInitial"/> property.</summary>
    public static readonly StyledProperty<string?> AvatarInitialProperty =
        AvaloniaProperty.Register<MaterialCard, string?>(nameof(AvatarInitial));

    /// <summary>Defines the <see cref="ShowAvatar"/> property.</summary>
    public static readonly StyledProperty<bool> ShowAvatarProperty =
        AvaloniaProperty.Register<MaterialCard, bool>(nameof(ShowAvatar), true);

    /// <summary>Defines the <see cref="HeaderAction"/> property.</summary>
    public static readonly StyledProperty<object?> HeaderActionProperty =
        AvaloniaProperty.Register<MaterialCard, object?>(nameof(HeaderAction));

    /// <summary>Defines the <see cref="Media"/> property.</summary>
    public static readonly StyledProperty<object?> MediaProperty =
        AvaloniaProperty.Register<MaterialCard, object?>(nameof(Media));

    /// <summary>Defines the <see cref="Title"/> property.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<MaterialCard, string?>(nameof(Title));

    /// <summary>Defines the <see cref="Subtitle"/> property.</summary>
    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<MaterialCard, string?>(nameof(Subtitle));

    /// <summary>Defines the <see cref="SupportingText"/> property.</summary>
    public static readonly StyledProperty<string?> SupportingTextProperty =
        AvaloniaProperty.Register<MaterialCard, string?>(nameof(SupportingText));

    /// <summary>Defines the <see cref="PrimaryAction"/> property.</summary>
    public static readonly StyledProperty<object?> PrimaryActionProperty =
        AvaloniaProperty.Register<MaterialCard, object?>(nameof(PrimaryAction));

    /// <summary>Defines the <see cref="SecondaryAction"/> property.</summary>
    public static readonly StyledProperty<object?> SecondaryActionProperty =
        AvaloniaProperty.Register<MaterialCard, object?>(nameof(SecondaryAction));

    /// <summary>Defines the <see cref="ShowSecondaryAction"/> property.</summary>
    public static readonly StyledProperty<bool> ShowSecondaryActionProperty =
        AvaloniaProperty.Register<MaterialCard, bool>(nameof(ShowSecondaryAction), true);

    /// <summary>Defines the <see cref="PrimaryActionCommand"/> property.</summary>
    public static readonly StyledProperty<ICommand?> PrimaryActionCommandProperty =
        AvaloniaProperty.Register<MaterialCard, ICommand?>(nameof(PrimaryActionCommand));

    /// <summary>Defines the <see cref="PrimaryActionCommandParameter"/> property.</summary>
    public static readonly StyledProperty<object?> PrimaryActionCommandParameterProperty =
        AvaloniaProperty.Register<MaterialCard, object?>(nameof(PrimaryActionCommandParameter));

    /// <summary>Defines the <see cref="SecondaryActionCommand"/> property.</summary>
    public static readonly StyledProperty<ICommand?> SecondaryActionCommandProperty =
        AvaloniaProperty.Register<MaterialCard, ICommand?>(nameof(SecondaryActionCommand));

    /// <summary>Defines the <see cref="SecondaryActionCommandParameter"/> property.</summary>
    public static readonly StyledProperty<object?> SecondaryActionCommandParameterProperty =
        AvaloniaProperty.Register<MaterialCard, object?>(nameof(SecondaryActionCommandParameter));

    /// <summary>Defines the <see cref="Command"/> property.</summary>
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<MaterialCard, ICommand?>(nameof(Command));

    /// <summary>Defines the <see cref="CommandParameter"/> property.</summary>
    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<MaterialCard, object?>(nameof(CommandParameter));

    /// <summary>Defines the <see cref="DragData"/> property.</summary>
    public static readonly StyledProperty<IDataTransfer?> DragDataProperty =
        AvaloniaProperty.Register<MaterialCard, IDataTransfer?>(nameof(DragData));

    /// <summary>Defines the <see cref="AllowedDragEffects"/> property.</summary>
    public static readonly StyledProperty<DragDropEffects> AllowedDragEffectsProperty =
        AvaloniaProperty.Register<MaterialCard, DragDropEffects>(nameof(AllowedDragEffects), DragDropEffects.Copy);

    /// <summary>Defines the <see cref="IsPressed"/> property.</summary>
    public static readonly DirectProperty<MaterialCard, bool> IsPressedProperty =
        AvaloniaProperty.RegisterDirect<MaterialCard, bool>(nameof(IsPressed), card => card.IsPressed);

    /// <summary>Defines the <see cref="IsDragging"/> property.</summary>
    public static readonly DirectProperty<MaterialCard, bool> IsDraggingProperty =
        AvaloniaProperty.RegisterDirect<MaterialCard, bool>(nameof(IsDragging), card => card.IsDragging);

    /// <summary>Defines the <see cref="FocusRingCornerRadius"/> property.</summary>
    public static readonly DirectProperty<MaterialCard, CornerRadius> FocusRingCornerRadiusProperty =
        AvaloniaProperty.RegisterDirect<MaterialCard, CornerRadius>(
            nameof(FocusRingCornerRadius), card => card.FocusRingCornerRadius);

    /// <summary>Defines the <see cref="HasHeader"/> property.</summary>
    public static readonly DirectProperty<MaterialCard, bool> HasHeaderProperty =
        AvaloniaProperty.RegisterDirect<MaterialCard, bool>(nameof(HasHeader), card => card.HasHeader);

    /// <summary>Defines the <see cref="HasAvatar"/> property.</summary>
    public static readonly DirectProperty<MaterialCard, bool> HasAvatarProperty =
        AvaloniaProperty.RegisterDirect<MaterialCard, bool>(nameof(HasAvatar), card => card.HasAvatar);

    /// <summary>Defines the <see cref="HasAvatarInitial"/> property.</summary>
    public static readonly DirectProperty<MaterialCard, bool> HasAvatarInitialProperty =
        AvaloniaProperty.RegisterDirect<MaterialCard, bool>(
            nameof(HasAvatarInitial), card => card.HasAvatarInitial);

    /// <summary>Defines the <see cref="HasMedia"/> property.</summary>
    public static readonly DirectProperty<MaterialCard, bool> HasMediaProperty =
        AvaloniaProperty.RegisterDirect<MaterialCard, bool>(nameof(HasMedia), card => card.HasMedia);

    /// <summary>Defines the <see cref="HasHeaderAction"/> property.</summary>
    public static readonly DirectProperty<MaterialCard, bool> HasHeaderActionProperty =
        AvaloniaProperty.RegisterDirect<MaterialCard, bool>(
            nameof(HasHeaderAction), card => card.HasHeaderAction);

    /// <summary>Defines the <see cref="HasHeadline"/> property.</summary>
    public static readonly DirectProperty<MaterialCard, bool> HasHeadlineProperty =
        AvaloniaProperty.RegisterDirect<MaterialCard, bool>(nameof(HasHeadline), card => card.HasHeadline);

    /// <summary>Defines the <see cref="HasSupportingText"/> property.</summary>
    public static readonly DirectProperty<MaterialCard, bool> HasSupportingTextProperty =
        AvaloniaProperty.RegisterDirect<MaterialCard, bool>(
            nameof(HasSupportingText), card => card.HasSupportingText);

    /// <summary>Defines the <see cref="HasActions"/> property.</summary>
    public static readonly DirectProperty<MaterialCard, bool> HasActionsProperty =
        AvaloniaProperty.RegisterDirect<MaterialCard, bool>(nameof(HasActions), card => card.HasActions);

    /// <summary>Defines the <see cref="HasPrimaryAction"/> property.</summary>
    public static readonly DirectProperty<MaterialCard, bool> HasPrimaryActionProperty =
        AvaloniaProperty.RegisterDirect<MaterialCard, bool>(
            nameof(HasPrimaryAction), card => card.HasPrimaryAction);

    /// <summary>Defines the <see cref="HasSecondaryAction"/> property.</summary>
    public static readonly DirectProperty<MaterialCard, bool> HasSecondaryActionProperty =
        AvaloniaProperty.RegisterDirect<MaterialCard, bool>(
            nameof(HasSecondaryAction), card => card.HasSecondaryAction);

    /// <summary>Defines the <see cref="Click"/> event.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> ClickEvent =
        RoutedEvent.Register<MaterialCard, RoutedEventArgs>(nameof(Click), RoutingStrategies.Bubble);

    private readonly EventHandler _canExecuteChangedHandler;
    private bool _isPressed;
    private bool _isDragging;
    private bool _commandCanExecute = true;
    private Point _pressPosition;
    private PointerPressedEventArgs? _pressArgs;
    private CornerRadius _focusRingCornerRadius = new(14);
    private bool _hasHeader;
    private bool _hasAvatar;
    private bool _hasAvatarInitial;
    private bool _hasMedia;
    private bool _hasHeaderAction;
    private bool _hasHeadline;
    private bool _hasSupportingText;
    private bool _hasActions;
    private bool _hasPrimaryAction;
    private bool _hasSecondaryAction;

    /// <summary>Initializes a new instance of the <see cref="MaterialCard"/> class.</summary>
    public MaterialCard()
    {
        _canExecuteChangedHandler = (_, _) => UpdateCommandCanExecute();
        UpdateFocusRingCornerRadius();
        UpdateDerivedProperties();
    }

    /// <summary>Raised when an interactive card is clicked or activated from the keyboard.</summary>
    public event EventHandler<RoutedEventArgs>? Click
    {
        add => AddHandler(ClickEvent, value);
        remove => RemoveHandler(ClickEvent, value);
    }

    /// <summary>Raised when a drag operation starts from this card.</summary>
    public event EventHandler? DragStarted;

    /// <summary>Raised when a drag operation that started from this card completes.</summary>
    public event EventHandler<MaterialCardDragCompletedEventArgs>? DragCompleted;

    /// <summary>Gets or sets the container treatment.</summary>
    public MaterialCardVariant Variant
    {
        get => GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    /// <summary>Gets or sets whether the card shows the media-and-text anatomy or only its slot.</summary>
    public MaterialCardLayout Layout
    {
        get => GetValue(LayoutProperty);
        set => SetValue(LayoutProperty, value);
    }

    /// <summary>Gets or sets which source card anatomy is used.</summary>
    public MaterialCardOrientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>Gets or sets whether the card behaves as a single clickable surface.</summary>
    public bool IsInteractive
    {
        get => GetValue(IsInteractiveProperty);
        set => SetValue(IsInteractiveProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the 3 dp focus indicator is drawn regardless of keyboard focus.
    /// </summary>
    /// <remarks>
    /// The source card-state building blocks expose the same switch as
    /// <c>Show focus indicator</c>; it exists so a preview can show the focused swatch without
    /// moving real focus. Normal focus behavior is unaffected when it is false.
    /// </remarks>
    public bool ShowFocusIndicator
    {
        get => GetValue(ShowFocusIndicatorProperty);
        set => SetValue(ShowFocusIndicatorProperty, value);
    }

    /// <summary>Gets or sets the header line of the anatomy's header block.</summary>
    public string? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    /// <summary>Gets or sets the supporting line under <see cref="Header"/>.</summary>
    public string? Subhead
    {
        get => GetValue(SubheadProperty);
        set => SetValue(SubheadProperty, value);
    }

    /// <summary>Gets or sets the 40 dp leading avatar content; a control is presented as-is.</summary>
    public object? Avatar
    {
        get => GetValue(AvatarProperty);
        set => SetValue(AvatarProperty, value);
    }

    /// <summary>Gets or sets the initial shown inside the avatar circle when no content is supplied.</summary>
    public string? AvatarInitial
    {
        get => GetValue(AvatarInitialProperty);
        set => SetValue(AvatarInitialProperty, value);
    }

    /// <summary>Gets or sets whether the avatar circle is part of the header.</summary>
    public bool ShowAvatar
    {
        get => GetValue(ShowAvatarProperty);
        set => SetValue(ShowAvatarProperty, value);
    }

    /// <summary>Gets or sets the trailing header action, typically a 48 dp icon button.</summary>
    public object? HeaderAction
    {
        get => GetValue(HeaderActionProperty);
        set => SetValue(HeaderActionProperty, value);
    }

    /// <summary>Gets or sets the media content; the container supplies the surface tint.</summary>
    public object? Media
    {
        get => GetValue(MediaProperty);
        set => SetValue(MediaProperty, value);
    }

    /// <summary>Gets or sets the headline under the media.</summary>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Gets or sets the supporting line under <see cref="Title"/>.</summary>
    public string? Subtitle
    {
        get => GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    /// <summary>Gets or sets the wrapping supporting paragraph.</summary>
    public string? SupportingText
    {
        get => GetValue(SupportingTextProperty);
        set => SetValue(SupportingTextProperty, value);
    }

    /// <summary>Gets or sets the primary action content; the template supplies a filled button.</summary>
    public object? PrimaryAction
    {
        get => GetValue(PrimaryActionProperty);
        set => SetValue(PrimaryActionProperty, value);
    }

    /// <summary>Gets or sets the secondary action content; the template supplies an outlined button.</summary>
    public object? SecondaryAction
    {
        get => GetValue(SecondaryActionProperty);
        set => SetValue(SecondaryActionProperty, value);
    }

    /// <summary>Gets or sets whether the secondary action is part of the action row.</summary>
    public bool ShowSecondaryAction
    {
        get => GetValue(ShowSecondaryActionProperty);
        set => SetValue(ShowSecondaryActionProperty, value);
    }

    /// <summary>Gets or sets the command executed by the primary action button.</summary>
    public ICommand? PrimaryActionCommand
    {
        get => GetValue(PrimaryActionCommandProperty);
        set => SetValue(PrimaryActionCommandProperty, value);
    }

    /// <summary>Gets or sets the parameter passed to <see cref="PrimaryActionCommand"/>.</summary>
    public object? PrimaryActionCommandParameter
    {
        get => GetValue(PrimaryActionCommandParameterProperty);
        set => SetValue(PrimaryActionCommandParameterProperty, value);
    }

    /// <summary>Gets or sets the command executed by the secondary action button.</summary>
    public ICommand? SecondaryActionCommand
    {
        get => GetValue(SecondaryActionCommandProperty);
        set => SetValue(SecondaryActionCommandProperty, value);
    }

    /// <summary>Gets or sets the parameter passed to <see cref="SecondaryActionCommand"/>.</summary>
    public object? SecondaryActionCommandParameter
    {
        get => GetValue(SecondaryActionCommandParameterProperty);
        set => SetValue(SecondaryActionCommandParameterProperty, value);
    }

    /// <summary>Gets or sets the command invoked when an interactive card is clicked.</summary>
    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    /// <summary>Gets or sets the parameter passed to <see cref="Command"/>.</summary>
    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    /// <summary>
    /// Gets or sets the transfer object started when an interactive card is dragged. A null value
    /// keeps the card from starting drag operations.
    /// </summary>
    public IDataTransfer? DragData
    {
        get => GetValue(DragDataProperty);
        set => SetValue(DragDataProperty, value);
    }

    /// <summary>Gets or sets the effects offered to drag targets.</summary>
    public DragDropEffects AllowedDragEffects
    {
        get => GetValue(AllowedDragEffectsProperty);
        set => SetValue(AllowedDragEffectsProperty, value);
    }

    /// <summary>Gets whether the pointer or keyboard currently holds the card down.</summary>
    public bool IsPressed => _isPressed;

    /// <summary>Gets whether a drag operation that started from this card is running.</summary>
    public bool IsDragging => _isDragging;

    /// <summary>Gets the corner radius of the focus ring: the card radius plus the 2 dp offset.</summary>
    public CornerRadius FocusRingCornerRadius => _focusRingCornerRadius;

    /// <summary>Gets whether the header row has anything to show.</summary>
    public bool HasHeader => _hasHeader;

    /// <summary>Gets whether the avatar circle is shown.</summary>
    public bool HasAvatar => _hasAvatar;

    /// <summary>Gets whether the avatar shows its text initial instead of supplied content.</summary>
    public bool HasAvatarInitial => _hasAvatarInitial;

    /// <summary>Gets whether the media area is shown.</summary>
    public bool HasMedia => _hasMedia;

    /// <summary>Gets whether the header shows a trailing action.</summary>
    public bool HasHeaderAction => _hasHeaderAction;

    /// <summary>Gets whether the title block has anything to show.</summary>
    public bool HasHeadline => _hasHeadline;

    /// <summary>Gets whether the supporting paragraph is shown.</summary>
    public bool HasSupportingText => _hasSupportingText;

    /// <summary>Gets whether the action row has at least one button.</summary>
    public bool HasActions => _hasActions;

    /// <summary>Gets whether the primary action button is shown.</summary>
    public bool HasPrimaryAction => _hasPrimaryAction;

    /// <summary>Gets whether the secondary action button is shown.</summary>
    public bool HasSecondaryAction => _hasSecondaryAction;

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new MaterialCardAutomationPeer(this);

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        if (Command is { } command)
            command.CanExecuteChanged += _canExecuteChangedHandler;
        UpdateCommandCanExecute();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromLogicalTree(e);
        if (Command is { } command)
            command.CanExecuteChanged -= _canExecuteChangedHandler;
        SetPressed(false);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        SetPressed(false);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == CommandProperty)
        {
            var (oldCommand, newCommand) = change.GetOldAndNewValue<ICommand?>();
            if (((ILogical)this).IsAttachedToLogicalTree)
            {
                if (oldCommand is not null)
                    oldCommand.CanExecuteChanged -= _canExecuteChangedHandler;
                if (newCommand is not null)
                    newCommand.CanExecuteChanged += _canExecuteChangedHandler;
            }
            UpdateCommandCanExecute();
        }
        else if (change.Property == CommandParameterProperty)
        {
            UpdateCommandCanExecute();
        }
        else if (change.Property == IsInteractiveProperty)
        {
            var interactive = change.GetNewValue<bool>();
            PseudoClasses.Set(":interactive", interactive);
            if (interactive)
            {
                // The theme also sets these; setting them here keeps the card keyboard reachable
                // under a consumer-supplied ControlTheme. SetCurrentValue preserves an explicit
                // consumer value.
                SetCurrentValue(FocusableProperty, true);
                SetCurrentValue(KeyboardNavigation.IsTabStopProperty, true);
            }
            else
            {
                SetPressed(false);
            }
        }
        else if (change.Property == CornerRadiusProperty)
        {
            UpdateFocusRingCornerRadius();
        }
        else if (change.Property == HeaderProperty ||
                 change.Property == SubheadProperty ||
                 change.Property == AvatarProperty ||
                 change.Property == AvatarInitialProperty ||
                 change.Property == ShowAvatarProperty ||
                 change.Property == HeaderActionProperty ||
                 change.Property == MediaProperty ||
                 change.Property == TitleProperty ||
                 change.Property == SubtitleProperty ||
                 change.Property == SupportingTextProperty ||
                 change.Property == PrimaryActionProperty ||
                 change.Property == SecondaryActionProperty ||
                 change.Property == ShowSecondaryActionProperty)
        {
            UpdateDerivedProperties();
        }
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Handled || !IsInteractive || !IsEffectivelyEnabled || e.KeyModifiers != KeyModifiers.None)
            return;

        if (e.Key == Key.Enter)
        {
            OnClick();
            e.Handled = true;
        }
        else if (e.Key == Key.Space && IsFocused)
        {
            // Only the focused card claims Space; a nested text box keeps it for input.
            SetPressed(true);
            e.Handled = true;
        }
    }

    /// <inheritdoc />
    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);

        if (e.Handled || !IsInteractive || !IsEffectivelyEnabled || e.Key != Key.Space || !IsFocused || !_isPressed)
            return;

        SetPressed(false);
        OnClick();
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!IsInteractive || !IsEffectivelyEnabled)
            return;

        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed && e.Pointer.Type != PointerType.Touch)
            return;

        _pressPosition = e.GetPosition(this);
        _pressArgs = e;
        SetPressed(true);
        // Capture so a drag that leaves the card is still detected and a release outside the
        // bounds does not report a click.
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (!_isPressed || _isDragging || !IsInteractive)
            return;

        if (DragData is not { } data || _pressArgs is not { } trigger)
            return;

        var offset = e.GetPosition(this) - _pressPosition;
        if (Math.Abs(offset.X) < DragThreshold && Math.Abs(offset.Y) < DragThreshold)
            return;

        // The gesture became a drag: it must not also report a click on release.
        SetPressed(false);
        _pressArgs = null;
        BeginDrag(trigger, data);
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (!_isPressed)
            return;

        SetPressed(false);
        e.Pointer.Capture(null);
        e.Handled = true;

        if (!_isDragging && new Rect(Bounds.Size).Contains(e.GetPosition(this)))
            OnClick();
    }

    /// <inheritdoc />
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        SetPressed(false);
    }

    /// <inheritdoc />
    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        SetPressed(false);
    }

    /// <summary>Raises <see cref="Click"/> and executes <see cref="Command"/>.</summary>
    protected virtual void OnClick()
    {
        if (!IsInteractive || !IsEffectivelyEnabled || !_commandCanExecute)
            return;

        var click = new RoutedEventArgs(ClickEvent, this);
        RaiseEvent(click);

        if (!click.Handled && Command is { } command && command.CanExecute(CommandParameter))
            command.Execute(CommandParameter);
    }

    private async void BeginDrag(PointerPressedEventArgs trigger, IDataTransfer data)
    {
        // The platform drag source takes over pointer handling; holding capture would fight it.
        trigger.Pointer.Capture(null);
        SetDragging(true);
        PseudoClasses.Set(DraggedPseudoClass, true);
        DragStarted?.Invoke(this, EventArgs.Empty);

        var effects = DragDropEffects.None;
        try
        {
            effects = await DragDrop.DoDragDropAsync(trigger, data, AllowedDragEffects);
        }
        catch (Exception exception)
        {
            // A platform without a drag source completes with None instead; surface real
            // failures without tearing down the UI thread.
            Trace.WriteLine($"MaterialCard drag operation failed: {exception}");
        }
        finally
        {
            SetDragging(false);
            PseudoClasses.Set(DraggedPseudoClass, false);
            DragCompleted?.Invoke(this, new MaterialCardDragCompletedEventArgs(effects));
        }
    }

    private void SetDragging(bool dragging)
    {
        if (_isDragging == dragging)
            return;

        SetAndRaise(IsDraggingProperty, ref _isDragging, dragging);
    }

    private void SetPressed(bool pressed)
    {
        if (_isPressed == pressed)
            return;

        SetAndRaise(IsPressedProperty, ref _isPressed, pressed);
        PseudoClasses.Set(PressedPseudoClass, pressed);
        if (!pressed)
            _pressArgs = null;
    }

    private void UpdateCommandCanExecute()
    {
        _commandCanExecute = Command?.CanExecute(CommandParameter) ?? true;
    }

    private void UpdateFocusRingCornerRadius()
    {
        var radius = CornerRadius;
        var ring = new CornerRadius(
            radius.TopLeft + 2,
            radius.TopRight + 2,
            radius.BottomRight + 2,
            radius.BottomLeft + 2);
        SetAndRaise(FocusRingCornerRadiusProperty, ref _focusRingCornerRadius, ring);
    }

    private void UpdateDerivedProperties()
    {
        var hasAvatar = ShowAvatar && (Avatar is not null || !string.IsNullOrWhiteSpace(AvatarInitial));
        SetAndRaise(HasAvatarProperty, ref _hasAvatar, hasAvatar);
        SetAndRaise(HasAvatarInitialProperty, ref _hasAvatarInitial,
            hasAvatar && Avatar is null && !string.IsNullOrWhiteSpace(AvatarInitial));
        SetAndRaise(HasHeaderProperty, ref _hasHeader,
            hasAvatar ||
            HeaderAction is not null ||
            !string.IsNullOrWhiteSpace(Header) ||
            !string.IsNullOrWhiteSpace(Subhead));
        SetAndRaise(HasMediaProperty, ref _hasMedia, Media is not null);
        SetAndRaise(HasHeaderActionProperty, ref _hasHeaderAction, HeaderAction is not null);
        SetAndRaise(HasHeadlineProperty, ref _hasHeadline,
            !string.IsNullOrWhiteSpace(Title) || !string.IsNullOrWhiteSpace(Subtitle));
        SetAndRaise(HasSupportingTextProperty, ref _hasSupportingText,
            !string.IsNullOrWhiteSpace(SupportingText));
        var hasPrimaryAction = PrimaryAction is not null;
        var hasSecondaryAction = ShowSecondaryAction && SecondaryAction is not null;
        SetAndRaise(HasPrimaryActionProperty, ref _hasPrimaryAction, hasPrimaryAction);
        SetAndRaise(HasSecondaryActionProperty, ref _hasSecondaryAction, hasSecondaryAction);
        SetAndRaise(HasActionsProperty, ref _hasActions, hasPrimaryAction || hasSecondaryAction);
    }

    private sealed class MaterialCardAutomationPeer(MaterialCard owner) : ContentControlAutomationPeer(owner),
        IInvokeProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() =>
            owner.IsInteractive ? AutomationControlType.Button : base.GetAutomationControlTypeCore();

        protected override string GetClassNameCore() => nameof(MaterialCard);

        protected override string? GetNameCore()
        {
            var name = base.GetNameCore();
            if (!string.IsNullOrWhiteSpace(name))
                return name;

            // The structured anatomy is plain text, so fall back to its most specific line.
            return FirstNonEmpty(owner.Header, owner.Title, owner.Subtitle, owner.SupportingText)
                ?? FirstNonEmpty(owner.Content as string);
        }

        protected override bool IsControlElementCore() => owner.IsInteractive;

        protected override bool IsContentElementCore() => owner.IsInteractive;

        protected override object? GetProviderCore(Type providerType) =>
            providerType == typeof(IInvokeProvider) && !owner.IsInteractive
                ? null
                : base.GetProviderCore(providerType);

        public void Invoke()
        {
            if (!owner.IsInteractive)
                throw new ElementNotEnabledException();
            owner.OnClick();
        }

        private static string? FirstNonEmpty(params string?[] values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }
            return null;
        }
    }
}
