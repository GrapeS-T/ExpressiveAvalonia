using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Theming;
using ExpressiveAvalonia.Tokens;
using Path = Avalonia.Controls.Shapes.Path;

namespace ExpressiveAvalonia.Controls;

/// <summary>One footer action of a <see cref="MaterialDialog"/>.</summary>
/// <remarks>
/// Actions become native buttons in visual order. <see cref="IsDefault"/> makes the action
/// respond to Enter, <see cref="IsCancel"/> makes it respond to Escape and scrim dismissal.
/// <see cref="Command"/> is executed by the button itself; the dialog closes with
/// <see cref="Result"/> when <see cref="CloseOnInvoke"/> is set.
/// </remarks>
public sealed class MaterialDialogAction : AvaloniaObject
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<MaterialDialogAction, string?>(nameof(Text));

    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<MaterialDialogAction, object?>(nameof(Icon));

    public static readonly StyledProperty<MaterialButtonVariant> VariantProperty =
        AvaloniaProperty.Register<MaterialDialogAction, MaterialButtonVariant>(nameof(Variant), MaterialButtonVariant.Text);

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<MaterialDialogAction, ICommand?>(nameof(Command));

    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<MaterialDialogAction, object?>(nameof(CommandParameter));

    public static readonly StyledProperty<object?> ResultProperty =
        AvaloniaProperty.Register<MaterialDialogAction, object?>(nameof(Result));

    public static readonly StyledProperty<bool> IsDefaultProperty =
        AvaloniaProperty.Register<MaterialDialogAction, bool>(nameof(IsDefault));

    public static readonly StyledProperty<bool> IsCancelProperty =
        AvaloniaProperty.Register<MaterialDialogAction, bool>(nameof(IsCancel));

    public static readonly StyledProperty<bool> IsEnabledProperty =
        AvaloniaProperty.Register<MaterialDialogAction, bool>(nameof(IsEnabled), true);

    public static readonly StyledProperty<bool> CloseOnInvokeProperty =
        AvaloniaProperty.Register<MaterialDialogAction, bool>(nameof(CloseOnInvoke), true);

    public MaterialDialogAction()
    {
    }

    /// <summary>Creates an action with a label and the result returned by the dialog.</summary>
    public MaterialDialogAction(string? text, object? result = null)
    {
        Text = text;
        Result = result;
    }

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <summary>Optional icon content placed before the label.</summary>
    public object? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>Button emphasis; dialog actions default to <see cref="MaterialButtonVariant.Text"/>.</summary>
    public MaterialButtonVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    public ICommand? Command { get => GetValue(CommandProperty); set => SetValue(CommandProperty, value); }

    public object? CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }

    /// <summary>The dialog result produced when the action closes the dialog.</summary>
    public object? Result { get => GetValue(ResultProperty); set => SetValue(ResultProperty, value); }

    /// <summary>Activates on Enter while focus is not on a button.</summary>
    public bool IsDefault { get => GetValue(IsDefaultProperty); set => SetValue(IsDefaultProperty, value); }

    /// <summary>Activates on Escape and on scrim dismissal.</summary>
    public bool IsCancel { get => GetValue(IsCancelProperty); set => SetValue(IsCancelProperty, value); }

    public bool IsEnabled { get => GetValue(IsEnabledProperty); set => SetValue(IsEnabledProperty, value); }

    /// <summary>Closes the dialog after the action is invoked. Set false for actions that keep it open.</summary>
    public bool CloseOnInvoke { get => GetValue(CloseOnInvokeProperty); set => SetValue(CloseOnInvokeProperty, value); }
}

/// <summary>Raised after a <see cref="MaterialDialog"/> has closed.</summary>
public sealed class MaterialDialogClosedEventArgs(object? result) : EventArgs
{
    /// <summary>The result passed to <see cref="MaterialDialog.Close"/> or the action result.</summary>
    public object? Result { get; } = result;
}

/// <summary>
/// A Material modal dialog presented in the owner's overlay layer with scrim, focus trap,
/// keyboard dismissal and focus restoration.
/// </summary>
/// <remarks>
/// <para>
/// Call <see cref="ShowAsync(TopLevel)"/> to present the dialog. The dialog is added to the
/// top level's <see cref="OverlayLayer"/>, a scrim blocks pointer input to the page, focus moves
/// into the dialog and Tab/Shift+Tab cycle inside it. Escape and scrim clicks dismiss the dialog
/// unless disabled; Enter activates the action marked <see cref="MaterialDialogAction.IsDefault"/>.
/// When the topmost dialog closes, focus returns to the element that had it before it was shown.
/// A newer still-open dialog keeps focus and inherits that restoration target. An accepted close
/// makes the presentation inert until it is shown again.
/// </para>
/// <para>
/// <see cref="Content"/> is the body slot below the header and <see cref="List"/> is a full-bleed
/// slot for <see cref="MaterialDialogList"/>. <see cref="Actions"/> accepts
/// <see cref="MaterialDialogAction"/> items; the footer is hidden when the collection is empty.
/// </para>
/// <para>
/// The source design family is design-data-only, so lifecycle behavior here is standard
/// Avalonia modal semantics rather than a transcription of source scripts.
/// </para>
/// </remarks>
[PseudoClasses(":open", ":icon", ":full-screen")]
public class MaterialDialog : ContentControl, IMaterialOverlayFocusChain
{
    /// <summary>Design source identity of the icon used by the source dialog family.</summary>
    public const string IconSourceId = "local-004360";

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<MaterialDialog, string?>(nameof(Title));

    public static readonly StyledProperty<string?> SupportingTextProperty =
        AvaloniaProperty.Register<MaterialDialog, string?>(nameof(SupportingText));

    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<MaterialDialog, object?>(nameof(Icon));

    /// <summary>Full-bleed content slot below the header, intended for <see cref="MaterialDialogList"/>.</summary>
    public static readonly StyledProperty<object?> ListProperty =
        AvaloniaProperty.Register<MaterialDialog, object?>(nameof(List));

    public static readonly StyledProperty<bool> ShowDividerProperty =
        AvaloniaProperty.Register<MaterialDialog, bool>(nameof(ShowDivider));

    /// <summary>Requests the full-screen dialog presentation on compact windows.</summary>
    public static readonly StyledProperty<bool> IsFullScreenProperty =
        AvaloniaProperty.Register<MaterialDialog, bool>(nameof(IsFullScreen));

    public static readonly StyledProperty<IList<MaterialDialogAction>?> ActionsProperty =
        AvaloniaProperty.Register<MaterialDialog, IList<MaterialDialogAction>?>(nameof(Actions));

    public static readonly StyledProperty<bool> IsDismissibleProperty =
        AvaloniaProperty.Register<MaterialDialog, bool>(nameof(IsDismissible), true);

    public static readonly StyledProperty<bool> CloseOnScrimClickProperty =
        AvaloniaProperty.Register<MaterialDialog, bool>(nameof(CloseOnScrimClick), true);

    public static readonly DirectProperty<MaterialDialog, bool> IsOpenProperty =
        AvaloniaProperty.RegisterDirect<MaterialDialog, bool>(nameof(IsOpen), static dialog => dialog._isOpen);

    private readonly Dictionary<MaterialDialogAction, Button> _buttons = [];
    private readonly Dictionary<Button, MaterialButtonVariant> _buttonVariants = [];
    private readonly Dictionary<Button, IDisposable> _foregroundBindings = [];
    private MaterialDialogActionsPanel? _actionsPanel;
    private INotifyCollectionChanged? _observedActions;
    private TopLevel? _owner;
    private OverlayLayer? _overlay;
    private Panel? _host;
    private Border? _scrim;
    private IInputElement? _previousFocus;
    private TaskCompletionSource<object?>? _completion;
    private MaterialSpringValue? _openProgress;
    private double _progress;
    private double _previousMaxHeight = double.PositiveInfinity;
    private Thickness _previousMargin;
    private HorizontalAlignment _previousHorizontalAlignment;
    private VerticalAlignment _previousVerticalAlignment;
    private bool _appliedMaxHeight;
    private bool _appliedLayout;
    private bool _isOpen;
    private bool _closing;
    private bool _focusRestored;
    private bool _ownedFocusAtClose;
    private bool _closeCompletionPending;
    private bool _startingExitAnimation;
    private bool _inputSuppressed;
    private bool _previousIsEnabled;
    private bool _previousIsHitTestVisible;

    public MaterialDialog()
    {
        Actions = new AvaloniaList<MaterialDialogAction>();
        Focusable = true;
        AutomationProperties.SetControlTypeOverride(this, AutomationControlType.Pane);
    }

    /// <summary>Localized heading. Also used as the accessible name of the dialog.</summary>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>Supporting text below the heading, styled with the body-medium role.</summary>
    public string? SupportingText { get => GetValue(SupportingTextProperty); set => SetValue(SupportingTextProperty, value); }

    /// <summary>Icon content above the heading; use <see cref="CreateIcon"/> for the source glyph.</summary>
    public object? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>A control rendered edge to edge between the header and the footer.</summary>
    public object? List { get => GetValue(ListProperty); set => SetValue(ListProperty, value); }

    /// <summary>Shows a full-width divider between the body and the footer.</summary>
    public bool ShowDivider { get => GetValue(ShowDividerProperty); set => SetValue(ShowDividerProperty, value); }

    /// <summary>Footer actions in visual order; the footer is hidden while the collection is empty.</summary>
    public IList<MaterialDialogAction>? Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }

    /// <summary>Allows Escape and scrim clicks to dismiss the dialog.</summary>
    public bool IsDismissible { get => GetValue(IsDismissibleProperty); set => SetValue(IsDismissibleProperty, value); }

    /// <summary>Allows a scrim click to dismiss the dialog. Requires <see cref="IsDismissible"/>.</summary>
    public bool CloseOnScrimClick { get => GetValue(CloseOnScrimClickProperty); set => SetValue(CloseOnScrimClickProperty, value); }

    /// <summary>Uses the full-screen presentation on compact windows and a basic dialog on larger windows.</summary>
    public bool IsFullScreen { get => GetValue(IsFullScreenProperty); set => SetValue(IsFullScreenProperty, value); }

    /// <summary>Whether the dialog is currently presented modally.</summary>
    public bool IsOpen => _isOpen;

    /// <summary>Raised before closing; set <see cref="CancelEventArgs.Cancel"/> to keep the dialog open.</summary>
    public event EventHandler<CancelEventArgs>? Closing;

    /// <summary>Raised after the dialog has been removed from its host.</summary>
    public event EventHandler<MaterialDialogClosedEventArgs>? Closed;

    /// <summary>Raised after the dialog has been presented and received initial focus.</summary>
    public event EventHandler? Opened;

    /// <summary>Adds a footer action, creating the collection when needed.</summary>
    public MaterialDialogAction AddAction(MaterialDialogAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var actions = Actions;
        if (actions is null)
        {
            actions = new AvaloniaList<MaterialDialogAction>();
            Actions = actions;
        }
        actions.Add(action);
        return action;
    }

    /// <summary>Creates an icon control for <see cref="Icon"/> using the theme's on-surface-variant role.</summary>
    /// <param name="geometry">
    /// Glyph geometry in a 24 unit box. Defaults to the glyph the source dialog family uses,
    /// resolved by <see cref="IconSourceId"/> from <see cref="MaterialIcons"/>.
    /// </param>
    public static Control CreateIcon(Geometry? geometry = null)
    {
        // Stretch.None keeps the source coordinates inside the 24 unit box, so a glyph that the
        // source draws from 5 to 20 stays where the design places it.
        var path = new Path
        {
            Data = geometry ?? MaterialIcons.Get(IconSourceId).Geometry,
            Stretch = Stretch.None,
            Width = 24,
            Height = 24,
        };
        // DialogTokens.IconColor is the secondary role, not on-surface-variant.
        path.Bind(Shape.FillProperty, path.GetResourceObservable("MaterialSecondaryBrush"));
        return path;
    }

    /// <summary>Presents <paramref name="dialog"/> modally over <paramref name="owner"/>.</summary>
    /// <returns>A task that completes with the dialog result once it has closed.</returns>
    public static Task<object?> ShowAsync(TopLevel owner, MaterialDialog dialog)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        return dialog.ShowAsync(owner);
    }

    /// <summary>Presents this dialog modally over <paramref name="owner"/>.</summary>
    /// <returns>A task that completes with the dialog result once it has closed.</returns>
    /// <exception cref="InvalidOperationException">
    /// The dialog is already open, already has a parent, or the owner has no overlay layer.
    /// </exception>
    public Task<object?> ShowAsync(TopLevel owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (_host is not null)
            throw new InvalidOperationException("This dialog is already open.");
        if (Parent is not null)
            throw new InvalidOperationException(
                "A dialog cannot be shown modally while it already has a parent. Create a separate instance for each presentation.");

        var overlay = FindOverlay(owner)
            ?? throw new InvalidOperationException(
                "MaterialDialog needs an OverlayLayer. The TopLevel template must contain a VisualLayerManager; " +
                "ExpressiveAvalonia's MaterialTheme provides one.");

        _owner = owner;
        _overlay = overlay;
        _previousFocus = owner.FocusManager?.GetFocusedElement();
        _completion = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _closing = false;
        _focusRestored = false;
        _ownedFocusAtClose = false;
        _closeCompletionPending = false;
        // A previous accepted close left the presentation inert until it is shown again.
        if (_inputSuppressed)
            RestoreInputState();

        var host = new Panel { ClipToBounds = false };
        _host = host;
        KeyboardNavigation.SetTabNavigation(host, KeyboardNavigationMode.Cycle);
        host.Children.Add(this);

        _previousMargin = Margin;
        _previousHorizontalAlignment = HorizontalAlignment;
        _previousVerticalAlignment = VerticalAlignment;
        _appliedLayout = true;
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        Margin = new Thickness(24);
        UpdateConstraints(owner);

        // Attach first so resource lookups resolve through the owner's theme, then build the
        // scrim and transitions from the actual tokens of that theme.
        host.AddHandler(InputElement.KeyDownEvent, OnHostKeyDown, RoutingStrategies.Tunnel);
        host.AddHandler(InputElement.KeyUpEvent, OnHostKeyUp, RoutingStrategies.Tunnel);
        host.AddHandler(InputElement.TextInputEvent, OnHostTextInput, RoutingStrategies.Tunnel);
        host.AddHandler(InputElement.PointerPressedEvent, OnHostPointerPressed, RoutingStrategies.Tunnel);
        overlay.Children.Add(host);
        UpdateHostSize();

        var scrim = new Border
        {
            Background = ResolveScrimBrush(),
            Opacity = 0,
            IsHitTestVisible = true,
        };
        _scrim = scrim;
        scrim.PointerPressed += OnScrimPointerPressed;
        host.Children.Insert(0, scrim);

        var scale = new ScaleTransform(0.92, 0.92);
        RenderTransform = scale;
        RenderTransformOrigin = RelativePoint.Center;
        Opacity = 0;
        scrim.Opacity = 0;
        _progress = 0;
        _openProgress = new MaterialSpringValue(this, 0, ApplyOpenProgress);

        owner.Closed += OnOwnerClosed;
        owner.PropertyChanged += OnOwnerPropertyChanged;
        overlay.PropertyChanged += OnOverlayPropertyChanged;
        MaterialOverlayLifecycle.Register(owner, this);

        _isOpen = true;
        RaisePropertyChanged(IsOpenProperty, false, true);
        PseudoClasses.Set(":open", true);
        UpdateAutomationName();

        Dispatcher.UIThread.Post(
            () =>
            {
                if (_host is null || _closing)
                    return;
                FocusInitial();
                // N06.3: AlertDialog.kt has no shared composable transition; Android animates the
                // dialog window. This fade/scale is an explicitly marked desktop composition that
                // uses the shared default-spatial spring, not an upstream animation spec.
                _openProgress?.AnimateTo(1, MaterialSpring.SpatialDefault, null, 0.01);
                Opened?.Invoke(this, EventArgs.Empty);
            },
            DispatcherPriority.Loaded);

        return _completion.Task;
    }

    /// <summary>Closes the dialog and completes <see cref="ShowAsync(TopLevel)"/> with <paramref name="result"/>.</summary>
    public void Close(object? result = null)
    {
        if (_host is null || _closing)
            return;
        var args = new CancelEventArgs();
        Closing?.Invoke(this, args);
        if (args.Cancel)
            return;
        _closing = true;
        // Disabling the presentation for the exit animation also takes keyboard focus away from
        // whatever inside it was focused, so remember that this layer owned focus before the
        // suppression, otherwise the saved restoration target would look orphaned on detach.
        _ownedFocusAtClose = _owner is { } focusOwner && OwnsFocus(focusOwner);
        // An accepted close must not leave native action commands reachable while the exit
        // animation runs. Make the presentation inert now, but keep the scrim intercepting
        // pointers so a closing dialog cannot leak clicks to the page below it.
        SuppressInput();
        if (_isOpen)
        {
            _isOpen = false;
            RaisePropertyChanged(IsOpenProperty, true, false);
        }
        PseudoClasses.Set(":open", false);

        // The exit spring may already be settled when the close is accepted (reduced motion or
        // no progress to animate); only that inline settle removes the host right away. A later
        // settle is finished on its own dispatcher turn, because it can arrive from an inherited
        // property notification that is still walking the visual tree.
        _startingExitAnimation = true;
        try
        {
            if (_openProgress is null)
                CompleteClose(result);
            else
                // The same spring retargets the current progress, so close-during-open keeps both
                // the sampled position and velocity instead of restarting from a static duration.
                _openProgress.AnimateTo(0, MaterialSpring.SpatialDefault, () => CompleteClose(result), 0.01);
        }
        finally
        {
            _startingExitAnimation = false;
        }
    }

    private void SuppressInput()
    {
        if (_inputSuppressed)
            return;
        _inputSuppressed = true;
        _previousIsEnabled = IsEnabled;
        _previousIsHitTestVisible = IsHitTestVisible;
        // SetCurrentValue changes the effective presentation state without replacing a consumer's
        // binding. Disabled controls lose native command execution and expose disabled automation
        // peers; non-hit-testable content leaves the still-visible scrim as the pointer target.
        SetCurrentValue(InputElement.IsEnabledProperty, false);
        SetCurrentValue(InputElement.IsHitTestVisibleProperty, false);
        foreach (var button in _buttons.Values)
            button.IsEnabled = false;
    }

    private void RestoreInputState()
    {
        if (!_inputSuppressed)
            return;
        _inputSuppressed = false;
        SetCurrentValue(InputElement.IsEnabledProperty, _previousIsEnabled);
        SetCurrentValue(InputElement.IsHitTestVisibleProperty, _previousIsHitTestVisible);
        foreach (var pair in _buttons)
            pair.Value.IsEnabled = pair.Key.IsEnabled;
    }

    IInputElement? IMaterialOverlayFocusChain.RestoreFocusTarget
    {
        get => _previousFocus;
        set => _previousFocus = value;
    }

    private bool OwnsFocus(TopLevel owner) =>
        owner.FocusManager?.GetFocusedElement() is { } focused &&
        (ReferenceEquals(focused, this) || (focused is Visual visual && this.IsVisualAncestorOf(visual)));

    private void ApplyOpenProgress(double value)
    {
        _progress = Math.Clamp(value, 0, 1);
        Opacity = _progress;
        if (RenderTransform is ScaleTransform scale)
        {
            var factor = 0.92 + (0.08 * _progress);
            scale.ScaleX = factor;
            scale.ScaleY = factor;
        }
        if (_scrim is not null)
            _scrim.Opacity = ResolveScrimOpacity() * _progress;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _actionsPanel = e.NameScope.Find<MaterialDialogActionsPanel>("PART_Actions");
        if (e.NameScope.Find<Button>("PART_CloseButton") is { } close)
            close.Click += (_, _) => InvokeCancel();
        UpdateActions();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ActionsProperty)
        {
            if (_observedActions is not null)
                _observedActions.CollectionChanged -= OnActionsCollectionChanged;
            _observedActions = Actions as INotifyCollectionChanged;
            if (_observedActions is not null)
                _observedActions.CollectionChanged += OnActionsCollectionChanged;
            UpdateActions();
        }
        else if (change.Property == TitleProperty)
        {
            UpdateAutomationName();
        }
        else if (change.Property == IconProperty)
        {
            UpdateIconState();
        }
        else if (change.Property == IsFullScreenProperty && _owner is not null)
        {
            UpdateConstraints(_owner);
        }
    }

    private void UpdateIconState() => PseudoClasses.Set(":icon", Icon is not null);

    private static OverlayLayer? FindOverlay(TopLevel owner)
    {
        // The window template owns a VisualLayerManager. Overlay hosting is opt-in on that
        // manager, so enable it before asking for the layer instead of failing on a stock template.
        foreach (var manager in owner.GetVisualDescendants().OfType<VisualLayerManager>())
            manager.EnableOverlayLayer = true;
        return OverlayLayer.GetOverlayLayer(owner);
    }

    private IBrush ResolveScrimBrush() =>
        this.TryFindResource("MaterialScrimBrush", out var value) && value is IBrush brush
            ? brush
            : Brushes.Black;

    private double ResolveScrimOpacity() =>
        this.TryFindResource("MaterialDialogScrimOpacity", out var value) && value is double opacity
            ? opacity
            : 0.32;

    private void UpdateConstraints(TopLevel owner)
    {
        var height = owner.ClientSize.Height;
        if (height <= 0)
            return;
        var fullScreen = IsFullScreen && owner.ClientSize.Width < 600;
        PseudoClasses.Set(":full-screen", fullScreen);
        HorizontalAlignment = fullScreen ? HorizontalAlignment.Stretch : HorizontalAlignment.Center;
        VerticalAlignment = fullScreen ? VerticalAlignment.Stretch : VerticalAlignment.Center;
        Margin = fullScreen ? default : new Thickness(24);
        if (!_appliedMaxHeight && double.IsPositiveInfinity(MaxHeight))
        {
            _previousMaxHeight = MaxHeight;
            _appliedMaxHeight = true;
        }
        if (_appliedMaxHeight)
            MaxHeight = fullScreen ? height : Math.Max(0, height - 48);
    }

    private void UpdateHostSize()
    {
        if (_host is null || _overlay is null)
            return;
        var size = _overlay.Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0)
            size = _owner?.ClientSize ?? size;
        _host.Width = size.Width;
        _host.Height = size.Height;
    }

    private void OnOwnerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TopLevel.ClientSizeProperty)
        {
            UpdateHostSize();
            if (_owner is { } owner)
                UpdateConstraints(owner);
        }
    }

    private void OnOverlayPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == BoundsProperty)
            UpdateHostSize();
    }

    private void OnOwnerClosed(object? sender, EventArgs e) => Abort();

    private void OnHostKeyDown(object? sender, KeyEventArgs e)
    {
        if (_inputSuppressed)
        {
            // Suppression applies from the focused element's tunnel through the host, before any
            // native button or editor can act on the key.
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Escape)
        {
            // A popup opened from inside the dialog (combo box, menu) owns Escape while it is up.
            if (!IsDismissible || IsInPopup(e.Source))
                return;
            // A newer overlay (another dialog, a sheet above this one) owns Escape.
            if (_owner is null || !MaterialOverlayLifecycle.IsTopmost(_owner, this))
                return;
            InvokeCancel();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            // A focused button already activates itself, a list row toggles its own selection
            // with Enter, and a multiline field needs the key for a line break. Only supply the
            // dialog default in the remaining cases.
            if (e.Source is Button or ListBoxItem or TextBox { AcceptsReturn: true })
                return;
            var action = FindAction(static candidate => candidate.IsDefault);
            if (action is not null)
            {
                Invoke(action, executeCommand: true);
                e.Handled = true;
            }
        }
    }

    private void OnHostKeyUp(object? sender, KeyEventArgs e)
    {
        if (_inputSuppressed)
            e.Handled = true;
    }

    private void OnHostTextInput(object? sender, TextInputEventArgs e)
    {
        if (_inputSuppressed)
            e.Handled = true;
    }

    private void OnHostPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_inputSuppressed)
            e.Handled = true;
    }

    private void OnScrimPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsDismissible || !CloseOnScrimClick)
            return;
        if (_scrim is null || !e.GetCurrentPoint(_scrim).Properties.IsLeftButtonPressed)
            return;
        e.Handled = true;
        InvokeCancel();
    }

    private bool IsInPopup(object? source) =>
        source is Visual visual && _owner is not null && TopLevel.GetTopLevel(visual) is { } topLevel &&
        !ReferenceEquals(topLevel, _owner);

    private void InvokeCancel()
    {
        var action = FindAction(static candidate => candidate.IsCancel);
        if (action is not null)
            Invoke(action, executeCommand: true);
        else
            Close(null);
    }

    private void Invoke(MaterialDialogAction action, bool executeCommand)
    {
        if (_closing || !action.IsEnabled)
            return;
        if (executeCommand && action.Command is { } command && command.CanExecute(action.CommandParameter))
            command.Execute(action.CommandParameter);
        if (action.CloseOnInvoke)
            Close(action.Result);
    }

    private MaterialDialogAction? FindAction(Func<MaterialDialogAction, bool> predicate)
    {
        if (Actions is null)
            return null;
        foreach (var action in Actions)
            if (predicate(action))
                return action;
        return null;
    }

    private void UpdateActions()
    {
        foreach (var action in _buttons.Keys)
            action.PropertyChanged -= OnActionPropertyChanged;
        foreach (var binding in _foregroundBindings.Values)
            binding.Dispose();
        _foregroundBindings.Clear();
        _buttons.Clear();
        _buttonVariants.Clear();

        if (_actionsPanel is null)
            return;

        _actionsPanel.Children.Clear();
        if (Actions is not null)
        {
            // AlertDialogFlowRow lays the source's [confirm, dismiss] items out with a flipped
            // direction: horizontally that puts dismiss to the left, vertically it puts confirm
            // on top. Our Actions collection is documented in visual order, so feeding it to the
            // flipped panel in reverse reproduces both arrangements (and reverse tab order, which
            // is also the source's composition order).
            for (var i = Actions.Count - 1; i >= 0; i--)
            {
                var action = Actions[i];
                var button = CreateActionButton(action);
                _buttons[action] = button;
                _actionsPanel.Children.Add(button);
                action.PropertyChanged += OnActionPropertyChanged;
            }
        }
        UpdateActionLayout();
    }

    private Button CreateActionButton(MaterialDialogAction action)
    {
        // The button theme already supplies the 48 unit touch target with a 40 unit surface,
        // the label-large type role and the full corner radius.
        var button = new Button { VerticalAlignment = VerticalAlignment.Center };
        button.Click += (_, _) =>
        {
            if (!_closing && action.CloseOnInvoke)
                Close(action.Result);
        };
        ApplyActionProperties(button, action);
        return button;
    }

    private void ApplyActionProperties(Button button, MaterialDialogAction action)
    {
        button.Content = action.Text;
        button.IsEnabled = !_inputSuppressed && action.IsEnabled;
        button.Command = action.Command;
        button.CommandParameter = action.CommandParameter;
        MaterialAssist.SetIcon(button, action.Icon);
        MaterialAssist.SetShowIcon(button, action.Icon is not null);
        MaterialAssist.SetButtonVariant(button, action.Variant);
        if (!_buttonVariants.TryGetValue(button, out var applied) || applied != action.Variant)
        {
            _buttonVariants[button] = action.Variant;
            if (_foregroundBindings.Remove(button, out var previous))
                previous.Dispose();
            // Dialog actions use the primary role for text buttons, as in the source family;
            // filled and tonal variants keep the foreground their own variant defines.
            if (action.Variant is MaterialButtonVariant.Text or MaterialButtonVariant.Standard)
                _foregroundBindings[button] = button.Bind(
                    Button.ForegroundProperty,
                    button.GetResourceObservable("MaterialPrimaryBrush"));
            else
                button.ClearValue(Button.ForegroundProperty);
        }
        if (!string.IsNullOrEmpty(action.Text))
            AutomationProperties.SetName(button, action.Text);
    }

    private void OnActionPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is MaterialDialogAction action && _buttons.TryGetValue(action, out var button))
            ApplyActionProperties(button, action);
    }

    private void OnActionsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateActions();

    private void UpdateActionLayout()
    {
        if (_actionsPanel is not null)
            _actionsPanel.IsVisible = _actionsPanel.Children.Count > 0;
    }

    private void FocusInitial()
    {
        // Material dialogs move focus to the first focusable element: a field, the first list
        // row, or the first action. The dialog itself is the fallback for text-only dialogs.
        foreach (var control in this.GetVisualDescendants().OfType<Control>())
        {
            if (control.Focusable && control.IsEffectivelyVisible && control.IsEffectivelyEnabled && control.Focus())
                return;
        }
        Focus();
    }

    private void UpdateAutomationName()
    {
        var fallback = this.TryFindResource("MaterialDialogAccessibleName", out var value) && value is string text
            ? text
            : "Dialog";
        AutomationProperties.SetName(this, string.IsNullOrWhiteSpace(Title) ? fallback : Title);
    }

    private void CompleteClose(object? result)
    {
        if (_host is null)
            return;

        // A spring can settle from a frame or from an inherited property notification (for example
        // when reduced motion is enabled while the exit runs). Detaching the host there would
        // restructure the visual tree that is still being traversed, so an exit that settles
        // outside the close request finishes on its own dispatcher turn instead.
        if (_startingExitAnimation)
        {
            FinishClose(result);
            return;
        }

        if (_closeCompletionPending)
            return;
        _closeCompletionPending = true;
        var completion = _completion;
        Dispatcher.UIThread.Post(
            () =>
            {
                // Abort or a new presentation may have finished this one before the turn ran.
                if (!ReferenceEquals(_completion, completion))
                    return;
                _closeCompletionPending = false;
                FinishClose(result);
            },
            DispatcherPriority.Loaded);
    }

    private void FinishClose(object? result)
    {
        if (_host is null)
            return;

        if (_isOpen)
        {
            _isOpen = false;
            RaisePropertyChanged(IsOpenProperty, true, false);
        }
        PseudoClasses.Set(":open", false);

        // The spring has already applied its final frame; stop it before removing the host so
        // detaching the control cannot replay a frame.
        _openProgress?.Dispose();
        _openProgress = null;
        _progress = 0;

        Detach(restoreFocus: true);
        var completion = _completion;
        _completion = null;
        completion?.TrySetResult(result);
        if (completion is not null)
            Closed?.Invoke(this, new MaterialDialogClosedEventArgs(result));
    }

    private void Abort()
    {
        if (_host is null)
            return;
        _closing = true;
        if (_isOpen)
        {
            _isOpen = false;
            RaisePropertyChanged(IsOpenProperty, true, false);
        }
        PseudoClasses.Set(":open", false);
        _openProgress?.Dispose();
        _openProgress = null;
        _progress = 0;
        Detach(restoreFocus: false);
        var completion = _completion;
        _completion = null;
        completion?.TrySetResult(null);
        Closed?.Invoke(this, new MaterialDialogClosedEventArgs(null));
    }

    private void Detach(bool restoreFocus)
    {
        var host = _host;
        var overlay = _overlay;
        var owner = _owner;
        var previous = _previousFocus;
        // Decide ownership before the layer leaves the registry and before its visual tree is
        // detached. A lower dialog must never restore over the modal that is still above it.
        var wasTopmost = owner is null || MaterialOverlayLifecycle.IsTopmost(owner, this);
        // Suppressing input for the exit animation clears focus from inside the layer, so a
        // dialog that owned focus when its close was accepted must still restore the target.
        var ownsFocus = _ownedFocusAtClose || (owner is not null && OwnsFocus(owner));
        _ownedFocusAtClose = false;

        _host = null;
        _overlay = null;
        _owner = null;
        _previousFocus = null;
        _scrim = null;
        _closing = false;

        if (owner is not null)
        {
            if (restoreFocus)
                MaterialOverlayLifecycle.RebaseFocusTargets(owner, this, previous);
            owner.Closed -= OnOwnerClosed;
            owner.PropertyChanged -= OnOwnerPropertyChanged;
            MaterialOverlayLifecycle.Unregister(owner, this);
        }
        if (overlay is not null)
        {
            overlay.PropertyChanged -= OnOverlayPropertyChanged;
            if (host is not null)
                overlay.Children.Remove(host);
        }
        if (host is not null)
        {
            host.RemoveHandler(InputElement.KeyDownEvent, OnHostKeyDown);
            host.RemoveHandler(InputElement.KeyUpEvent, OnHostKeyUp);
            host.RemoveHandler(InputElement.TextInputEvent, OnHostTextInput);
            host.RemoveHandler(InputElement.PointerPressedEvent, OnHostPointerPressed);
            host.Children.Remove(this);
        }

        Transitions = null;
        RenderTransform = null;
        Opacity = 1;
        if (_appliedLayout)
        {
            Margin = _previousMargin;
            HorizontalAlignment = _previousHorizontalAlignment;
            VerticalAlignment = _previousVerticalAlignment;
            _appliedLayout = false;
        }
        if (_appliedMaxHeight)
        {
            MaxHeight = _previousMaxHeight;
            _previousMaxHeight = double.PositiveInfinity;
            _appliedMaxHeight = false;
        }

        if (!_focusRestored && restoreFocus && wasTopmost && ownsFocus &&
            previous is Control { IsEffectivelyEnabled: true, IsVisible: true } control &&
            control.IsAttachedToVisualTree() &&
            owner is not null && ReferenceEquals(TopLevel.GetTopLevel(control), owner))
            control.Focus(NavigationMethod.Unspecified);
        _focusRestored = true;
    }
}

/// <summary>
/// Right-aligned, wrapping action row for <see cref="MaterialDialog"/>.
/// </summary>
/// <remarks>
/// Desktop equivalent of <c>AlertDialogFlowRow</c> in AlertDialog.kt. Items are packed from the
/// trailing edge with 8 DIP between them and wrap onto new right-aligned lines. The dialog adds
/// its actions in reverse collection order, and the flipped packing then reproduces the source's
/// horizontal [dismiss, confirm] and stacked [confirm above dismiss] arrangements.
/// </remarks>
public sealed class MaterialDialogActionsPanel : Panel
{
    private const double ItemSpacing = 8d;

    protected override Size MeasureOverride(Size availableSize)
    {
        var maxWidth = double.IsPositiveInfinity(availableSize.Width)
            ? double.PositiveInfinity
            : Math.Max(0, availableSize.Width);

        var childConstraintWidth = double.IsPositiveInfinity(maxWidth)
            ? double.PositiveInfinity
            : Math.Max(0, maxWidth);
        foreach (var child in Children)
            child.Measure(new Size(childConstraintWidth, availableSize.Height));

        var lines = BuildLines(maxWidth);
        var width = 0d;
        var height = 0d;
        foreach (var line in lines)
        {
            var lineWidth = 0d;
            var lineHeight = 0d;
            for (var i = 0; i < line.Count; i++)
            {
                lineWidth += line[i].DesiredSize.Width;
                lineHeight = Math.Max(lineHeight, line[i].DesiredSize.Height);
                if (i > 0)
                    lineWidth += ItemSpacing;
            }
            width = Math.Max(width, lineWidth);
            height += lineHeight;
        }
        if (lines.Count > 1)
            height += ItemSpacing * (lines.Count - 1);
        if (!double.IsPositiveInfinity(maxWidth))
            width = Math.Min(width, maxWidth);

        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var lines = BuildLines(finalSize.Width);
        var y = 0d;
        foreach (var line in lines)
        {
            var lineWidth = 0d;
            var lineHeight = 0d;
            for (var i = 0; i < line.Count; i++)
            {
                lineWidth += line[i].DesiredSize.Width;
                lineHeight = Math.Max(lineHeight, line[i].DesiredSize.Height);
                if (i > 0)
                    lineWidth += ItemSpacing;
            }

            // Pack from the trailing edge: the first composition item sits rightmost.
            var cursor = finalSize.Width;
            foreach (var child in line)
            {
                cursor -= child.DesiredSize.Width;
                child.Arrange(new Rect(cursor, y, child.DesiredSize.Width, lineHeight));
                cursor -= ItemSpacing;
            }

            y += lineHeight + ItemSpacing;
        }

        return finalSize;
    }

    private List<List<Control>> BuildLines(double maxWidth)
    {
        var lines = new List<List<Control>>();
        var line = new List<Control>();
        var lineWidth = 0d;

        foreach (var child in Children)
        {
            var childWidth = child.DesiredSize.Width;
            var nextWidth = line.Count == 0 ? childWidth : lineWidth + ItemSpacing + childWidth;
            if (line.Count > 0 && !double.IsPositiveInfinity(maxWidth) && nextWidth > maxWidth)
            {
                lines.Add(line);
                line = new List<Control>();
                lineWidth = 0;
                nextWidth = childWidth;
            }

            line.Add(child);
            lineWidth = nextWidth;
        }

        if (line.Count > 0)
            lines.Add(line);
        return lines;
    }
}
