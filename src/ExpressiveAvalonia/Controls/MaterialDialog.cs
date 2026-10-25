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
/// When the dialog closes, focus returns to the element that had it before it was shown.
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
[PseudoClasses(":open", ":icon")]
public class MaterialDialog : ContentControl
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
    private StackPanel? _actionsPanel;
    private Border? _bottomSpacer;
    private INotifyCollectionChanged? _observedActions;
    private TopLevel? _owner;
    private OverlayLayer? _overlay;
    private Panel? _host;
    private Border? _scrim;
    private IInputElement? _previousFocus;
    private TaskCompletionSource<object?>? _completion;
    private TimeSpan _exitDuration;
    private double _previousMaxHeight = double.PositiveInfinity;
    private Thickness _previousMargin;
    private HorizontalAlignment _previousHorizontalAlignment;
    private VerticalAlignment _previousVerticalAlignment;
    private bool _appliedMaxHeight;
    private bool _appliedLayout;
    private bool _isOpen;
    private bool _closing;

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
        path.Bind(Shape.FillProperty, path.GetResourceObservable("MaterialOnSurfaceVariantBrush"));
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

        var enter = GetDuration("MaterialDurationMedium2", TimeSpan.FromMilliseconds(300));
        _exitDuration = GetDuration("MaterialDurationShort4", TimeSpan.FromMilliseconds(200));
        if (enter > TimeSpan.Zero)
        {
            var easing = new SplineEasing(0.2, 0, 0, 1);
            Transitions = new Transitions
            {
                new DoubleTransition { Property = OpacityProperty, Duration = enter, Easing = easing },
            };
            scale.Transitions = new Transitions
            {
                new DoubleTransition { Property = ScaleTransform.ScaleXProperty, Duration = enter, Easing = easing },
                new DoubleTransition { Property = ScaleTransform.ScaleYProperty, Duration = enter, Easing = easing },
            };
            scrim.Transitions = new Transitions
            {
                new DoubleTransition { Property = OpacityProperty, Duration = enter, Easing = easing },
            };
        }

        owner.Closed += OnOwnerClosed;
        owner.PropertyChanged += OnOwnerPropertyChanged;
        overlay.PropertyChanged += OnOverlayPropertyChanged;

        _isOpen = true;
        RaisePropertyChanged(IsOpenProperty, false, true);
        PseudoClasses.Set(":open", true);
        UpdateAutomationName();

        var scrimOpacity = ResolveScrimOpacity();
        Dispatcher.UIThread.Post(
            () =>
            {
                if (_host is null || _closing)
                    return;
                Opacity = 1;
                scale.ScaleX = 1;
                scale.ScaleY = 1;
                scrim.Opacity = scrimOpacity;
                FocusInitial();
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
        _ = FinishCloseAsync(result);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _actionsPanel = e.NameScope.Find<StackPanel>("PART_Actions");
        _bottomSpacer = e.NameScope.Find<Border>("PART_BottomSpacer");
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

    private TimeSpan GetDuration(string key, TimeSpan fallback)
    {
        if (MaterialMotion.GetIsReduced(this))
            return TimeSpan.Zero;
        return this.TryFindResource(key, out var value) && value is TimeSpan duration ? duration : fallback;
    }

    private void UpdateConstraints(TopLevel owner)
    {
        var height = owner.ClientSize.Height;
        if (height <= 0)
            return;
        if (!_appliedMaxHeight && double.IsPositiveInfinity(MaxHeight))
        {
            _previousMaxHeight = MaxHeight;
            _appliedMaxHeight = true;
        }
        if (_appliedMaxHeight)
            MaxHeight = Math.Max(0, height - 48);
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
        if (e.Key == Key.Escape)
        {
            // A popup opened from inside the dialog (combo box, menu) owns Escape while it is up.
            if (!IsDismissible || IsInPopup(e.Source))
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
        if (!action.IsEnabled)
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
            foreach (var action in Actions)
            {
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
            if (action.CloseOnInvoke)
                Close(action.Result);
        };
        ApplyActionProperties(button, action);
        return button;
    }

    private void ApplyActionProperties(Button button, MaterialDialogAction action)
    {
        button.Content = action.Text;
        button.IsEnabled = action.IsEnabled;
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
        var hasActions = _actionsPanel is { Children.Count: > 0 };
        if (_actionsPanel is not null)
            _actionsPanel.IsVisible = hasActions;
        if (_bottomSpacer is not null)
            _bottomSpacer.IsVisible = !hasActions;
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

    private async Task FinishCloseAsync(object? result)
    {
        try
        {
            if (_isOpen)
            {
                _isOpen = false;
                RaisePropertyChanged(IsOpenProperty, true, false);
            }
            PseudoClasses.Set(":open", false);

            // Retarget the transitions to the exit duration so the surface, its scale and the
            // scrim all finish together before the host is removed.
            if (_exitDuration > TimeSpan.Zero)
            {
                var easing = new SplineEasing(0.2, 0, 0, 1);
                Transitions = new Transitions
                {
                    new DoubleTransition { Property = OpacityProperty, Duration = _exitDuration, Easing = easing },
                };
                if (RenderTransform is ScaleTransform scale)
                {
                    scale.Transitions = new Transitions
                    {
                        new DoubleTransition { Property = ScaleTransform.ScaleXProperty, Duration = _exitDuration, Easing = easing },
                        new DoubleTransition { Property = ScaleTransform.ScaleYProperty, Duration = _exitDuration, Easing = easing },
                    };
                }
                if (_scrim is not null)
                {
                    _scrim.Transitions = new Transitions
                    {
                        new DoubleTransition { Property = OpacityProperty, Duration = _exitDuration, Easing = easing },
                    };
                }
            }

            Opacity = 0;
            if (RenderTransform is ScaleTransform exitScale)
            {
                exitScale.ScaleX = 0.92;
                exitScale.ScaleY = 0.92;
            }
            if (_scrim is not null)
                _scrim.Opacity = 0;
            if (_exitDuration > TimeSpan.Zero)
                await Task.Delay(_exitDuration);
        }
        finally
        {
            Detach(restoreFocus: true);
            var completion = _completion;
            _completion = null;
            completion?.TrySetResult(result);
            if (completion is not null)
                Closed?.Invoke(this, new MaterialDialogClosedEventArgs(result));
        }
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

        _host = null;
        _overlay = null;
        _owner = null;
        _previousFocus = null;
        _scrim = null;
        _closing = false;

        if (owner is not null)
        {
            owner.Closed -= OnOwnerClosed;
            owner.PropertyChanged -= OnOwnerPropertyChanged;
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

        if (restoreFocus && previous is Control { IsEffectivelyEnabled: true, IsVisible: true } control &&
            control.IsAttachedToVisualTree())
            control.Focus(NavigationMethod.Unspecified);
    }
}
