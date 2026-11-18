using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// The XR dialog: a 312 unit rounded card with an optional icon, headline, supporting text,
/// an optional divider, and up to two text actions, hosted as a real modal window.
/// </summary>
/// <remarks>
/// Desktop extension. The pinned <c>SpatialBasicAlertDialog</c> only hosts caller-provided content
/// in a SpatialDialog; it defines no card anatomy. This card is the local design composition, and
/// it uses an Avalonia modal window instead of the XR spatial dialog.
/// <para>
/// The dialog is a <see cref="Window"/> so it gets the native modal lifecycle: show it with
/// <see cref="ShowDialogAsync"/> and it returns the invoked action. Escape dismisses, the primary action is the default button for Enter, focus
/// moves to the primary action when the dialog opens, and the actions execute their commands
/// before closing. The window is borderless and transparent; the card carries the surface tone,
/// 28 unit corner radius, and elevation shadow.
/// </para>
/// </remarks>
public class XrDialog : Window
{
    public static readonly StyledProperty<string?> HeadlineProperty =
        AvaloniaProperty.Register<XrDialog, string?>(nameof(Headline));

    public static readonly StyledProperty<string?> SupportingTextProperty =
        AvaloniaProperty.Register<XrDialog, string?>(nameof(SupportingText));

    /// <summary>
    /// The dialog's leading glyph. Distinct from <see cref="Window.Icon"/>, which is the
    /// window's title-bar icon and is unrelated to the card content.
    /// </summary>
    public static readonly StyledProperty<Geometry?> ContentIconProperty =
        AvaloniaProperty.Register<XrDialog, Geometry?>(nameof(ContentIcon));

    /// <summary>Whether the icon row participates in the layout; the row also needs a <see cref="ContentIcon"/>.</summary>
    public static readonly StyledProperty<bool> ShowIconProperty =
        AvaloniaProperty.Register<XrDialog, bool>(nameof(ShowIcon), true);

    /// <summary>Whether the secondary action is shown. The primary action is always shown.</summary>
    public static readonly StyledProperty<bool> ShowSecondaryActionProperty =
        AvaloniaProperty.Register<XrDialog, bool>(nameof(ShowSecondaryAction), true);

    /// <summary>Whether a one unit divider separates the content from the actions.</summary>
    public static readonly StyledProperty<bool> ShowDividerProperty =
        AvaloniaProperty.Register<XrDialog, bool>(nameof(ShowDivider));

    public static readonly StyledProperty<XrSurfaceRole> ElevationProperty =
        AvaloniaProperty.Register<XrDialog, XrSurfaceRole>(nameof(Elevation), XrSurfaceRole.SurfaceContainerHigh);

    public static readonly StyledProperty<string?> PrimaryActionTextProperty =
        AvaloniaProperty.Register<XrDialog, string?>(nameof(PrimaryActionText), "Action 1");

    public static readonly StyledProperty<string?> SecondaryActionTextProperty =
        AvaloniaProperty.Register<XrDialog, string?>(nameof(SecondaryActionText), "Action 2");

    public static readonly StyledProperty<ICommand?> PrimaryCommandProperty =
        AvaloniaProperty.Register<XrDialog, ICommand?>(nameof(PrimaryCommand));

    public static readonly StyledProperty<object?> PrimaryCommandParameterProperty =
        AvaloniaProperty.Register<XrDialog, object?>(nameof(PrimaryCommandParameter));

    public static readonly StyledProperty<ICommand?> SecondaryCommandProperty =
        AvaloniaProperty.Register<XrDialog, ICommand?>(nameof(SecondaryCommand));

    public static readonly StyledProperty<object?> SecondaryCommandParameterProperty =
        AvaloniaProperty.Register<XrDialog, object?>(nameof(SecondaryCommandParameter));

    /// <summary>Whether invoking an action closes the dialog with the matching result.</summary>
    public static readonly StyledProperty<bool> IsCloseOnActionProperty =
        AvaloniaProperty.Register<XrDialog, bool>(nameof(IsCloseOnAction), true);

    private readonly EventHandler _primaryCanExecuteChanged;
    private readonly EventHandler _secondaryCanExecuteChanged;
    private Button? _primary;
    private Button? _secondary;
    private ICommand? _subscribedPrimaryCommand;
    private ICommand? _subscribedSecondaryCommand;
    private bool _commandsActive;
    private bool _updatingAutomationName;
    private XrDialogResult _result;

    static XrDialog()
    {
        HeadlineProperty.Changed.AddClassHandler<XrDialog>((dialog, _) => dialog.UpdateHeadline());
        ElevationProperty.Changed.AddClassHandler<XrDialog>((dialog, _) => dialog.UpdateElevation());
        ShowIconProperty.Changed.AddClassHandler<XrDialog>((dialog, _) => dialog.UpdateIconState());
        ContentIconProperty.Changed.AddClassHandler<XrDialog>((dialog, _) => dialog.UpdateIconState());
    }

    public XrDialog()
    {
        _primaryCanExecuteChanged = (_, _) => UpdateActionEnabledStates();
        _secondaryCanExecuteChanged = (_, _) => UpdateActionEnabledStates();
        WindowDecorations = Avalonia.Controls.WindowDecorations.None;
        CanResize = false;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        Background = Brushes.Transparent;
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
        UpdateElevation();
        UpdateIconState();
    }

    /// <summary>
    /// Styled as <see cref="XrDialog"/> rather than as <see cref="Window"/>: the base class
    /// maps every window to the generic window theme, which would bypass the dialog card.
    /// </summary>
    protected override Type StyleKeyOverride => typeof(XrDialog);

    /// <summary>Raised after the primary action's command has executed.</summary>
    public event EventHandler? PrimaryInvoked;

    /// <summary>Raised after the secondary action's command has executed.</summary>
    public event EventHandler? SecondaryInvoked;

    public string? Headline { get => GetValue(HeadlineProperty); set => SetValue(HeadlineProperty, value); }
    public string? SupportingText { get => GetValue(SupportingTextProperty); set => SetValue(SupportingTextProperty, value); }
    public Geometry? ContentIcon { get => GetValue(ContentIconProperty); set => SetValue(ContentIconProperty, value); }
    public bool ShowIcon { get => GetValue(ShowIconProperty); set => SetValue(ShowIconProperty, value); }
    public bool ShowSecondaryAction { get => GetValue(ShowSecondaryActionProperty); set => SetValue(ShowSecondaryActionProperty, value); }
    public bool ShowDivider { get => GetValue(ShowDividerProperty); set => SetValue(ShowDividerProperty, value); }
    public XrSurfaceRole Elevation { get => GetValue(ElevationProperty); set => SetValue(ElevationProperty, value); }
    public string? PrimaryActionText { get => GetValue(PrimaryActionTextProperty); set => SetValue(PrimaryActionTextProperty, value); }
    public string? SecondaryActionText { get => GetValue(SecondaryActionTextProperty); set => SetValue(SecondaryActionTextProperty, value); }
    public ICommand? PrimaryCommand { get => GetValue(PrimaryCommandProperty); set => SetValue(PrimaryCommandProperty, value); }
    public object? PrimaryCommandParameter { get => GetValue(PrimaryCommandParameterProperty); set => SetValue(PrimaryCommandParameterProperty, value); }
    public ICommand? SecondaryCommand { get => GetValue(SecondaryCommandProperty); set => SetValue(SecondaryCommandProperty, value); }
    public object? SecondaryCommandParameter { get => GetValue(SecondaryCommandParameterProperty); set => SetValue(SecondaryCommandParameterProperty, value); }
    public bool IsCloseOnAction { get => GetValue(IsCloseOnActionProperty); set => SetValue(IsCloseOnActionProperty, value); }

    /// <summary>Shows the dialog modally over <paramref name="owner"/> and returns the invoked action.</summary>
    /// <remarks>
    /// Uses the non-generic modal API and tracks the result locally: a window closed by the
    /// platform (Alt+F4, owner shutdown) produces no dialog result, so the generic
    /// <c>ShowDialog&lt;T&gt;</c> overload would fail the cast instead of reporting a dismissal.
    /// </remarks>
    public async Task<XrDialogResult> ShowDialogAsync(Window owner)
    {
        _result = XrDialogResult.None;
        await ShowDialog(owner);
        return _result == XrDialogResult.None ? XrDialogResult.Dismissed : _result;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_primary is not null)
            _primary.Click -= OnPrimaryClick;
        if (_secondary is not null)
            _secondary.Click -= OnSecondaryClick;

        base.OnApplyTemplate(e);
        _primary = e.NameScope.Find<Button>("PART_Primary");
        _secondary = e.NameScope.Find<Button>("PART_Secondary");
        if (_primary is not null)
            _primary.Click += OnPrimaryClick;
        if (_secondary is not null)
            _secondary.Click += OnSecondaryClick;

        _commandsActive = _primary is not null || _secondary is not null;
        SyncCommandSubscriptions();
        UpdateActionEnabledStates();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _commandsActive = true;
        SyncCommandSubscriptions();
        UpdateActionEnabledStates();
        Control? focusTarget = _primary is { IsVisible: true, IsEffectivelyEnabled: true } primary
            ? primary
            : _secondary is { IsVisible: true, IsEffectivelyEnabled: true } secondary
                ? secondary
                : null;
        (focusTarget ?? this).Focus(NavigationMethod.Tab);
    }

    protected override void OnClosed(EventArgs e)
    {
        _commandsActive = false;
        SyncCommandSubscriptions();
        if (_primary is not null)
            _primary.Click -= OnPrimaryClick;
        if (_secondary is not null)
            _secondary.Click -= OnSecondaryClick;
        base.OnClosed(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PrimaryCommandProperty || change.Property == SecondaryCommandProperty)
        {
            SyncCommandSubscriptions();
            UpdateActionEnabledStates();
        }
        else if (change.Property == PrimaryCommandParameterProperty || change.Property == SecondaryCommandParameterProperty)
        {
            UpdateActionEnabledStates();
        }
        else if (change.Property == AutomationProperties.NameProperty && !_updatingAutomationName)
        {
            _appliedName = null;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!e.Handled && e.Key == Key.Escape)
        {
            e.Handled = true;
            Dismiss(XrDialogResult.Dismissed);
            return;
        }

        base.OnKeyDown(e);
    }

    private void OnPrimaryClick(object? sender, RoutedEventArgs e)
    {
        var command = PrimaryCommand;
        var parameter = PrimaryCommandParameter;
        if (command is not null)
        {
            if (!command.CanExecute(parameter))
                return;
            command.Execute(parameter);
        }

        PrimaryInvoked?.Invoke(this, EventArgs.Empty);
        if (IsCloseOnAction)
            Dismiss(XrDialogResult.Primary);
    }

    private void OnSecondaryClick(object? sender, RoutedEventArgs e)
    {
        var command = SecondaryCommand;
        var parameter = SecondaryCommandParameter;
        if (command is not null)
        {
            if (!command.CanExecute(parameter))
                return;
            command.Execute(parameter);
        }

        SecondaryInvoked?.Invoke(this, EventArgs.Empty);
        if (IsCloseOnAction)
            Dismiss(XrDialogResult.Secondary);
    }

    private void SyncCommandSubscriptions()
    {
        var desiredPrimary = _commandsActive ? PrimaryCommand : null;
        if (!ReferenceEquals(_subscribedPrimaryCommand, desiredPrimary))
        {
            if (_subscribedPrimaryCommand is not null)
                _subscribedPrimaryCommand.CanExecuteChanged -= _primaryCanExecuteChanged;
            _subscribedPrimaryCommand = desiredPrimary;
            if (_subscribedPrimaryCommand is not null)
                _subscribedPrimaryCommand.CanExecuteChanged += _primaryCanExecuteChanged;
        }

        var desiredSecondary = _commandsActive ? SecondaryCommand : null;
        if (!ReferenceEquals(_subscribedSecondaryCommand, desiredSecondary))
        {
            if (_subscribedSecondaryCommand is not null)
                _subscribedSecondaryCommand.CanExecuteChanged -= _secondaryCanExecuteChanged;
            _subscribedSecondaryCommand = desiredSecondary;
            if (_subscribedSecondaryCommand is not null)
                _subscribedSecondaryCommand.CanExecuteChanged += _secondaryCanExecuteChanged;
        }
    }

    private void UpdateActionEnabledStates()
    {
        if (_primary is not null)
            _primary.IsEnabled = PrimaryCommand?.CanExecute(PrimaryCommandParameter) ?? true;
        if (_secondary is not null)
            _secondary.IsEnabled = SecondaryCommand?.CanExecute(SecondaryCommandParameter) ?? true;
    }

    private void Dismiss(XrDialogResult result)
    {
        _result = result;
        Close(result);
    }

    private string? _appliedName;

    private void UpdateHeadline()
    {
        var headline = Headline;
        Title = headline ?? string.Empty;
        var current = AutomationProperties.GetName(this);
        _updatingAutomationName = true;
        try
        {
            if (!string.IsNullOrEmpty(headline))
            {
                if (string.IsNullOrEmpty(current) || current == _appliedName)
                {
                    AutomationProperties.SetName(this, headline);
                    _appliedName = headline;
                }
            }
            else if (_appliedName is not null && current == _appliedName)
            {
                ClearValue(AutomationProperties.NameProperty);
                _appliedName = null;
            }
        }
        finally
        {
            _updatingAutomationName = false;
        }
    }

    private void UpdateElevation() =>
        PseudoClasses.Set(":elevation-highest", Elevation == XrSurfaceRole.SurfaceContainerHighest);

    // The icon row collapses entirely when there is no icon, so the source's "Show icon"
    // switch and the icon instance compose without leaving an empty 24 unit gap.
    private void UpdateIconState() =>
        PseudoClasses.Set(":with-icon", ShowIcon && ContentIcon is not null);
}
