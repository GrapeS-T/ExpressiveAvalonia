using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using ExpressiveAvalonia.Theming;
using ExpressiveAvalonia.Tokens;

namespace ExpressiveAvalonia.Controls;

/// <summary>A Material split button: a primary action plus a trailing menu anchor or second action.</summary>
/// <remarks>
/// <para>
/// Both halves are real Avalonia buttons, so pointer, Space/Enter, focus visuals, access keys,
/// command execution, <see cref="System.Windows.Input.ICommand.CanExecuteChanged"/> handling and
/// automation work natively. The primary half raises <see cref="Click"/> first and, unless a
/// handler marks it handled, lets the native button execute <see cref="Command"/>. The trailing
/// half either opens <see cref="Flyout"/> — with a click, Down, Alt+Down or F4 — or, when no
/// flyout is set, raises <see cref="TrailingClick"/> and then executes
/// <see cref="TrailingCommand"/>. A false <see cref="System.Windows.Input.ICommand.CanExecute"/>
/// disables the matching half. Escape closes the menu and returns focus to the trailing half;
/// light dismissal keeps whatever focus the user moved to.
/// </para>
/// <para>
/// Setting <see cref="IsTrailingCheckable"/> turns the trailing half into the source's independent
/// checked variant (<c>TrailingButton(checked, onCheckedChange)</c>): activation toggles
/// <see cref="IsTrailingChecked"/> after <see cref="TrailingClick"/> and before the native
/// command, raises <see cref="TrailingCheckedChanged"/>, and morphs the checked silhouette to the
/// full-round trailing shape. The checked state is not derived from <see cref="IsMenuOpen"/>; a
/// flyout still takes precedence over toggling, so the trailing half remains a menu anchor when
/// <see cref="Flyout"/> is set. Without <see cref="IsTrailingCheckable"/> the trailing half stays
/// action-only.
/// </para>
/// <para>
/// Geometry follows the pinned source: visual heights 32/40/56/96/136 inside 48/48/56/96/136
/// targets, a 2-DIP seam, full-round outer corners, inner seam corners of 4/4/4/8/12 DIP that morph
/// to 8/12/12/20/20 DIP on press with the DefaultEffects spring, a circular trailing silhouette
/// while the trailing half is checked or its menu is open, leading content padding of 12+10,
/// 16+12, 24+24, 48+48 and
/// 64+64, trailing padding of 13/13, 15/15, 29/29 and 43/43, and the source trailing icon sizes.
/// The trailing content is optically centered with the source 0.11 coefficient because its inner
/// corners are smaller than its outer corners.
/// </para>
/// <para>
/// The two halves are separate tab stops; the corners mirror in right-to-left layouts while the
/// leading half stays logically first. Reduced motion snaps every shape change.
/// </para>
/// </remarks>
[TemplatePart("PART_PrimaryButton", typeof(Button))]
[TemplatePart("PART_TrailingButton", typeof(Button))]
public class MaterialSplitButton : ContentControl
{
    /// <summary>Optional leading icon; accepts an icon control, image or any other content.</summary>
    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<MaterialSplitButton, object?>(nameof(Icon));

    /// <summary>The color treatment: Filled, Tonal, Elevated or Outlined.</summary>
    public static readonly StyledProperty<MaterialButtonVariant> VariantProperty =
        AvaloniaProperty.Register<MaterialSplitButton, MaterialButtonVariant>(
            nameof(Variant), MaterialButtonVariant.Filled);

    /// <summary>The Material Expressive size.</summary>
    public static readonly StyledProperty<MaterialControlSize> ControlSizeProperty =
        AvaloniaProperty.Register<MaterialSplitButton, MaterialControlSize>(
            nameof(ControlSize), MaterialControlSize.Small);

    /// <summary>Round uses pill silhouettes; square uses the size corner radius.</summary>
    public static readonly StyledProperty<MaterialButtonShape> ButtonShapeProperty =
        AvaloniaProperty.Register<MaterialSplitButton, MaterialButtonShape>(nameof(ButtonShape));

    /// <summary>The menu shown by the trailing half; a MenuFlyout or any other popup flyout.</summary>
    public static readonly StyledProperty<FlyoutBase?> FlyoutProperty =
        AvaloniaProperty.Register<MaterialSplitButton, FlyoutBase?>(nameof(Flyout));

    /// <summary>Whether the trailing menu is open; can be set to open or close it.</summary>
    public static readonly StyledProperty<bool> IsMenuOpenProperty =
        AvaloniaProperty.Register<MaterialSplitButton, bool>(
            nameof(IsMenuOpen), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>
    /// Whether the trailing half is an independent checkable button instead of an action only,
    /// matching the source <c>TrailingButton(checked, onCheckedChange)</c> variant.
    /// </summary>
    /// <remarks>
    /// A flyout still takes precedence on activation; checkability is independent from
    /// <see cref="Flyout"/> and <see cref="IsMenuOpen"/>.
    /// </remarks>
    public static readonly StyledProperty<bool> IsTrailingCheckableProperty =
        AvaloniaProperty.Register<MaterialSplitButton, bool>(nameof(IsTrailingCheckable));

    /// <summary>
    /// The checked state of the checkable trailing half; toggled by native activation and
    /// bindable two-way. Coerced to false while <see cref="IsTrailingCheckable"/> is false.
    /// </summary>
    public static readonly StyledProperty<bool> IsTrailingCheckedProperty =
        AvaloniaProperty.Register<MaterialSplitButton, bool>(
            nameof(IsTrailingChecked),
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay,
            coerce: CoerceTrailingChecked);

    /// <summary>Executed when the primary half is activated.</summary>
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<MaterialSplitButton, ICommand?>(nameof(Command));

    /// <summary>The parameter passed to <see cref="Command"/>.</summary>
    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<MaterialSplitButton, object?>(nameof(CommandParameter));

    /// <summary>Turns only the primary half on or off; the two halves are independent.</summary>
    public static readonly StyledProperty<bool> IsPrimaryEnabledProperty =
        AvaloniaProperty.Register<MaterialSplitButton, bool>(nameof(IsPrimaryEnabled), true);

    /// <summary>Turns only the trailing half on or off.</summary>
    public static readonly StyledProperty<bool> IsTrailingEnabledProperty =
        AvaloniaProperty.Register<MaterialSplitButton, bool>(nameof(IsTrailingEnabled), true);

    /// <summary>
    /// Executed by the trailing half when no <see cref="Flyout"/> is set, matching the source's
    /// uncheckable trailing button variant.
    /// </summary>
    public static readonly StyledProperty<ICommand?> TrailingCommandProperty =
        AvaloniaProperty.Register<MaterialSplitButton, ICommand?>(nameof(TrailingCommand));

    /// <summary>The parameter passed to <see cref="TrailingCommand"/>.</summary>
    public static readonly StyledProperty<object?> TrailingCommandParameterProperty =
        AvaloniaProperty.Register<MaterialSplitButton, object?>(nameof(TrailingCommandParameter));

    /// <summary>The edge length of the leading icon.</summary>
    public static readonly StyledProperty<double> IconSizeProperty =
        AvaloniaProperty.Register<MaterialSplitButton, double>(nameof(IconSize), 20d);

    /// <summary>The edge length of the trailing chevron.</summary>
    public static readonly StyledProperty<double> TrailingIconSizeProperty =
        AvaloniaProperty.Register<MaterialSplitButton, double>(nameof(TrailingIconSize), 22d);

    /// <summary>The gap between the leading icon and the label.</summary>
    public static readonly StyledProperty<double> ContentSpacingProperty =
        AvaloniaProperty.Register<MaterialSplitButton, double>(nameof(ContentSpacing), 8d);

    /// <summary>The physical left inset of the trailing content, used to clamp optical centering.</summary>
    public static readonly StyledProperty<double> TrailingContentPaddingStartProperty =
        AvaloniaProperty.Register<MaterialSplitButton, double>(nameof(TrailingContentPaddingStart), 13d);

    /// <summary>The physical right inset of the trailing content, used to clamp optical centering.</summary>
    public static readonly StyledProperty<double> TrailingContentPaddingEndProperty =
        AvaloniaProperty.Register<MaterialSplitButton, double>(nameof(TrailingContentPaddingEnd), 13d);

    /// <summary>Whether the primary half is effectively enabled (own flag and inherited state).</summary>
    public static readonly StyledProperty<bool> IsPrimaryEffectivelyEnabledProperty =
        AvaloniaProperty.Register<MaterialSplitButton, bool>(nameof(IsPrimaryEffectivelyEnabled), true);

    /// <summary>Whether the trailing half is effectively enabled (own flag and inherited state).</summary>
    public static readonly StyledProperty<bool> IsTrailingEffectivelyEnabledProperty =
        AvaloniaProperty.Register<MaterialSplitButton, bool>(nameof(IsTrailingEffectivelyEnabled), true);

    /// <summary>Raised when the primary half is activated.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> ClickEvent =
        RoutedEvent.Register<MaterialSplitButton, RoutedEventArgs>(nameof(Click), RoutingStrategies.Bubble);

    /// <summary>Raised when the trailing half is activated without a flyout.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> TrailingClickEvent =
        RoutedEvent.Register<MaterialSplitButton, RoutedEventArgs>(nameof(TrailingClick), RoutingStrategies.Bubble);

    /// <summary>Raised when the checkable trailing half's checked state changes.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> TrailingCheckedChangedEvent =
        RoutedEvent.Register<MaterialSplitButton, RoutedEventArgs>(
            nameof(TrailingCheckedChanged), RoutingStrategies.Bubble);

    private Button? _primaryButton;
    private Button? _trailingButton;
    private FlyoutBase? _attachedFlyout;
    private ICommand? _subscribedPrimaryCommand;
    private ICommand? _subscribedTrailingCommand;
    private MaterialSplitButtonAutomationPeer? _peer;
    private MaterialCornerRadiusTransition? _primaryCorners;
    private MaterialCornerRadiusTransition? _trailingCorners;
    private CornerRadius _primaryResting;
    private CornerRadius _primaryPressed;
    private CornerRadius _trailingResting;
    private CornerRadius _trailingPressed;
    private bool _syncingMenu;
    private bool _syncingTrailingChecked;

    public object? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public MaterialButtonVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
    public MaterialControlSize ControlSize { get => GetValue(ControlSizeProperty); set => SetValue(ControlSizeProperty, value); }
    public MaterialButtonShape ButtonShape { get => GetValue(ButtonShapeProperty); set => SetValue(ButtonShapeProperty, value); }
    public FlyoutBase? Flyout { get => GetValue(FlyoutProperty); set => SetValue(FlyoutProperty, value); }
    public bool IsMenuOpen { get => GetValue(IsMenuOpenProperty); set => SetValue(IsMenuOpenProperty, value); }
    public bool IsTrailingCheckable { get => GetValue(IsTrailingCheckableProperty); set => SetValue(IsTrailingCheckableProperty, value); }
    public bool IsTrailingChecked { get => GetValue(IsTrailingCheckedProperty); set => SetValue(IsTrailingCheckedProperty, value); }
    public ICommand? Command { get => GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
    public object? CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }
    public bool IsPrimaryEnabled { get => GetValue(IsPrimaryEnabledProperty); set => SetValue(IsPrimaryEnabledProperty, value); }
    public bool IsTrailingEnabled { get => GetValue(IsTrailingEnabledProperty); set => SetValue(IsTrailingEnabledProperty, value); }
    public ICommand? TrailingCommand { get => GetValue(TrailingCommandProperty); set => SetValue(TrailingCommandProperty, value); }
    public object? TrailingCommandParameter { get => GetValue(TrailingCommandParameterProperty); set => SetValue(TrailingCommandParameterProperty, value); }
    public double IconSize { get => GetValue(IconSizeProperty); set => SetValue(IconSizeProperty, value); }
    public double TrailingIconSize { get => GetValue(TrailingIconSizeProperty); set => SetValue(TrailingIconSizeProperty, value); }
    public double ContentSpacing { get => GetValue(ContentSpacingProperty); set => SetValue(ContentSpacingProperty, value); }
    public double TrailingContentPaddingStart { get => GetValue(TrailingContentPaddingStartProperty); set => SetValue(TrailingContentPaddingStartProperty, value); }
    public double TrailingContentPaddingEnd { get => GetValue(TrailingContentPaddingEndProperty); set => SetValue(TrailingContentPaddingEndProperty, value); }
    public bool IsPrimaryEffectivelyEnabled { get => GetValue(IsPrimaryEffectivelyEnabledProperty); private set => SetValue(IsPrimaryEffectivelyEnabledProperty, value); }
    public bool IsTrailingEffectivelyEnabled { get => GetValue(IsTrailingEffectivelyEnabledProperty); private set => SetValue(IsTrailingEffectivelyEnabledProperty, value); }

    /// <summary>Raised when the primary half is activated by pointer, keyboard or automation.</summary>
    public event EventHandler<RoutedEventArgs>? Click
    {
        add => AddHandler(ClickEvent, value);
        remove => RemoveHandler(ClickEvent, value);
    }

    /// <summary>Raised when the trailing half is activated and has no menu flyout.</summary>
    public event EventHandler<RoutedEventArgs>? TrailingClick
    {
        add => AddHandler(TrailingClickEvent, value);
        remove => RemoveHandler(TrailingClickEvent, value);
    }

    /// <summary>Raised after the checkable trailing half's checked state changes.</summary>
    public event EventHandler<RoutedEventArgs>? TrailingCheckedChanged
    {
        add => AddHandler(TrailingCheckedChangedEvent, value);
        remove => RemoveHandler(TrailingCheckedChangedEvent, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        DetachParts();

        _primaryButton = e.NameScope.Find<Button>("PART_PrimaryButton");
        _trailingButton = e.NameScope.Find<Button>("PART_TrailingButton");

        if (_primaryButton is not null)
        {
            _primaryButton.Click += OnPrimaryClick;
            _primaryButton.PropertyChanged += OnSegmentPropertyChanged;
            _primaryCorners = new MaterialCornerRadiusTransition(_primaryButton, default,
                value => _primaryButton.CornerRadius = value, MaterialSpring.EffectsDefault);
        }

        if (_trailingButton is not null)
        {
            _trailingButton.Click += OnTrailingClick;
            _trailingButton.AddHandler(KeyDownEvent, OnTrailingKeyDown, RoutingStrategies.Tunnel);
            _trailingButton.PropertyChanged += OnSegmentPropertyChanged;
            if (_trailingButton is MaterialSplitButtonTrailingButton trailingButton)
                trailingButton.SplitButton = this;
            if (_trailingButton is ToggleButton trailingToggle)
                trailingToggle.IsCheckedChanged += OnNativeTrailingCheckedChanged;
            _trailingCorners = new MaterialCornerRadiusTransition(_trailingButton, default,
                value => _trailingButton.CornerRadius = value, MaterialSpring.EffectsDefault);
        }

        UpdateTrailingCheckedState(animate: false);
        ApplyMetrics();
        AttachFlyout();
        UpdateMenuState(Flyout?.IsOpen == true);
        SyncNativeCommands();
        UpdateEnabledFlags();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateCommandSubscriptions();
        AttachFlyout();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        DetachFlyout();
        UnsubscribeCommands();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == FlyoutProperty)
        {
            AttachFlyout();
            UpdateMenuState(Flyout?.IsOpen == true);
            UpdateMenuShape(animate: true);
            SyncNativeCommands();
            UpdateEnabledFlags();
        }
        else if (change.Property == IsTrailingCheckableProperty)
        {
            CoerceValue(IsTrailingCheckedProperty);
            UpdateTrailingCheckedState(animate: true);
        }
        else if (change.Property == IsTrailingCheckedProperty)
        {
            UpdateTrailingCheckedState(animate: true);
            RaiseEvent(new RoutedEventArgs(TrailingCheckedChangedEvent));
        }
        else if (change.Property == IsMenuOpenProperty && !_syncingMenu)
        {
            if (IsMenuOpen)
                ShowMenu();
            else
                HideMenu();
        }
        else if (change.Property == ControlSizeProperty || change.Property == ButtonShapeProperty ||
                 change.Property == VariantProperty)
        {
            ApplyMetrics();
            UpdateMenuShape(animate: false);
        }
        else if (change.Property == CommandProperty || change.Property == CommandParameterProperty ||
                 change.Property == TrailingCommandProperty ||
                 change.Property == TrailingCommandParameterProperty)
        {
            SyncNativeCommands();
            UpdateEnabledFlags();
        }
        else if (change.Property == IsEnabledProperty || change.Property == IsPrimaryEnabledProperty ||
                 change.Property == IsTrailingEnabledProperty)
        {
            UpdateEnabledFlags();
        }
        else if (change.Property == FlowDirectionProperty)
        {
            ApplyMetrics();
            UpdateMenuShape(animate: false);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && IsMenuOpen)
        {
            HideMenu();
            _trailingButton?.Focus(NavigationMethod.Tab);
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    protected override AutomationPeer OnCreateAutomationPeer() =>
        _peer = new MaterialSplitButtonAutomationPeer(this);

    private void DetachParts()
    {
        if (_primaryButton is not null)
        {
            _primaryButton.Click -= OnPrimaryClick;
            _primaryButton.PropertyChanged -= OnSegmentPropertyChanged;
        }
        if (_trailingButton is not null)
        {
            _trailingButton.Click -= OnTrailingClick;
            _trailingButton.RemoveHandler(KeyDownEvent, OnTrailingKeyDown);
            _trailingButton.PropertyChanged -= OnSegmentPropertyChanged;
            if (_trailingButton is MaterialSplitButtonTrailingButton trailingButton)
                trailingButton.SplitButton = null;
            if (_trailingButton is ToggleButton trailingToggle)
                trailingToggle.IsCheckedChanged -= OnNativeTrailingCheckedChanged;
        }
        _primaryCorners?.Dispose();
        _trailingCorners?.Dispose();
        _primaryCorners = null;
        _trailingCorners = null;
        _primaryButton = null;
        _trailingButton = null;
    }

    private void OnSegmentPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Button.IsPressedProperty)
            UpdateMenuShape(animate: true);
    }

    private void AttachFlyout()
    {
        DetachFlyout();
        if (Flyout is not { } flyout)
            return;

        _attachedFlyout = flyout;
        flyout.PropertyChanged += OnFlyoutPropertyChanged;
    }

    private void DetachFlyout()
    {
        if (_attachedFlyout is not null)
            _attachedFlyout.PropertyChanged -= OnFlyoutPropertyChanged;
        _attachedFlyout = null;
    }

    private void OnFlyoutPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != FlyoutBase.IsOpenProperty)
            return;

        var open = Flyout?.IsOpen == true;
        UpdateMenuState(open);
        if (!open && TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is null)
            _trailingButton?.Focus(NavigationMethod.Tab);
    }

    private void UpdateMenuState(bool open)
    {
        _syncingMenu = true;
        SetCurrentValue(IsMenuOpenProperty, open);
        _syncingMenu = false;
        _trailingButton?.Classes.Set("menuopen", open);
        _peer?.NotifyMenuStateChanged(open);
        UpdateMenuShape(animate: true);
    }

    private void ShowMenu()
    {
        if (Flyout is not { } flyout)
            return;

        flyout.ShowAt(_trailingButton ?? (Control)this);
    }

    private void HideMenu()
    {
        if (Flyout is PopupFlyoutBase popup)
            popup.Hide();
        else if (Flyout is { } flyout)
            flyout.IsOpen = false;
    }

    private void OnPrimaryClick(object? sender, RoutedEventArgs e)
    {
        if (!IsEffectivePrimaryEnabled())
            return;

        // Raise the public event before the native button runs its command. A handler that marks
        // the public event handled also stops the native command, and the command still executes
        // exactly once when nobody handles it.
        var args = new RoutedEventArgs(ClickEvent);
        RaiseEvent(args);
        if (args.Handled)
            e.Handled = true;
    }

    private void InvokePrimaryWithoutTemplate()
    {
        if (!IsEffectivePrimaryEnabled())
            return;

        var args = new RoutedEventArgs(ClickEvent);
        RaiseEvent(args);
        if (!args.Handled && Command is { } command && command.CanExecute(CommandParameter))
            command.Execute(CommandParameter);
    }

    private void OnTrailingClick(object? sender, RoutedEventArgs e)
    {
        if (!IsEffectiveTrailingEnabled())
            return;

        if (Flyout is not null)
        {
            if (Flyout.IsOpen)
                HideMenu();
            else
                ShowMenu();
            // The flyout takes precedence over TrailingCommand, so no command may run here.
            e.Handled = true;
            return;
        }

        // Same ordering as the primary half: the public event can suppress the native command and the
        // built-in toggle. The checkable trailing variant flips its state before the native button
        // runs TrailingCommand, so the command observes the new checked state.
        var args = new RoutedEventArgs(TrailingClickEvent);
        RaiseEvent(args);
        if (args.Handled)
        {
            e.Handled = true;
            return;
        }

        if (IsTrailingCheckable)
            SetCurrentValue(IsTrailingCheckedProperty, !IsTrailingChecked);
    }

    private void OnTrailingKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled)
            return;

        var opens = e.Key == Key.F4 ||
                    e.Key == Key.Down && e.KeyModifiers is KeyModifiers.None or KeyModifiers.Alt;
        if (!opens || Flyout is null || !IsEffectiveTrailingEnabled())
            return;

        ShowMenu();
        e.Handled = true;
    }

    private bool IsEffectivePrimaryEnabled() =>
        IsEnabled && IsPrimaryEnabled && CanExecute(Command, CommandParameter);

    private bool IsEffectiveTrailingEnabled() =>
        IsEnabled && IsTrailingEnabled &&
        (Flyout is not null || CanExecute(TrailingCommand, TrailingCommandParameter));

    /// <summary>
    /// True when the trailing half may flip its checked state: the caller opted into the
    /// checkable variant, no flyout owns the activation, and the trailing half is enabled.
    /// </summary>
    internal bool CanToggleTrailing =>
        IsTrailingCheckable && Flyout is null && IsEffectiveTrailingEnabled();

    private static bool CoerceTrailingChecked(AvaloniaObject owner, bool value) =>
        value && ((MaterialSplitButton)owner).IsTrailingCheckable;

    /// <summary>
    /// Mirrors <see cref="IsTrailingChecked"/> onto the native trailing button so its checked
    /// pseudo-class and toggle automation pattern follow the control's state.
    /// </summary>
    private void UpdateTrailingCheckedState(bool animate)
    {
        _trailingButton?.Classes.Set("checked", IsTrailingChecked);
        SetNativeTrailingChecked(IsTrailingChecked);
        UpdateMenuShape(animate);
    }

    private void SetNativeTrailingChecked(bool value)
    {
        if (_trailingButton is not ToggleButton toggle || toggle.IsChecked == value)
            return;

        _syncingTrailingChecked = true;
        toggle.SetCurrentValue(ToggleButton.IsCheckedProperty, value);
        _syncingTrailingChecked = false;
    }

    private void OnNativeTrailingCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (_syncingTrailingChecked || _trailingButton is not ToggleButton toggle)
            return;

        // Direct changes to the part stay coherent with the public property; the coercion snaps
        // the native state back while the trailing half is not checkable.
        var value = toggle.IsChecked == true;
        if (IsTrailingChecked != value)
            SetCurrentValue(IsTrailingCheckedProperty, value);
        if (toggle.IsChecked != IsTrailingChecked)
            SetNativeTrailingChecked(IsTrailingChecked);
    }

    private static bool CanExecute(ICommand? command, object? parameter) =>
        command is null || command.CanExecute(parameter);

    /// <summary>
    /// Hands the public commands to the template's native buttons. The trailing half only gets a
    /// command when there is no flyout because the flyout takes precedence.
    /// </summary>
    private void SyncNativeCommands()
    {
        UpdateCommandSubscriptions();

        if (_primaryButton is not null)
        {
            _primaryButton.Command = Command;
            _primaryButton.CommandParameter = CommandParameter;
        }

        if (_trailingButton is not null)
        {
            _trailingButton.Command = Flyout is null ? TrailingCommand : null;
            _trailingButton.CommandParameter = TrailingCommandParameter;
        }
    }

    private void UpdateCommandSubscriptions()
    {
        // Subscriptions live only while the control is in a visual tree so a long-lived command
        // cannot root a detached control.
        if (VisualRoot is null)
            return;

        if (!ReferenceEquals(_subscribedPrimaryCommand, Command))
        {
            if (_subscribedPrimaryCommand is not null)
                _subscribedPrimaryCommand.CanExecuteChanged -= OnCommandCanExecuteChanged;
            _subscribedPrimaryCommand = Command;
            if (_subscribedPrimaryCommand is not null)
                _subscribedPrimaryCommand.CanExecuteChanged += OnCommandCanExecuteChanged;
        }

        if (!ReferenceEquals(_subscribedTrailingCommand, TrailingCommand))
        {
            if (_subscribedTrailingCommand is not null)
                _subscribedTrailingCommand.CanExecuteChanged -= OnCommandCanExecuteChanged;
            _subscribedTrailingCommand = TrailingCommand;
            if (_subscribedTrailingCommand is not null)
                _subscribedTrailingCommand.CanExecuteChanged += OnCommandCanExecuteChanged;
        }
    }

    private void UnsubscribeCommands()
    {
        if (_subscribedPrimaryCommand is not null)
        {
            _subscribedPrimaryCommand.CanExecuteChanged -= OnCommandCanExecuteChanged;
            _subscribedPrimaryCommand = null;
        }

        if (_subscribedTrailingCommand is not null)
        {
            _subscribedTrailingCommand.CanExecuteChanged -= OnCommandCanExecuteChanged;
            _subscribedTrailingCommand = null;
        }
    }

    private void OnCommandCanExecuteChanged(object? sender, EventArgs e) => UpdateEnabledFlags();

    private void UpdateEnabledFlags()
    {
        var primary = IsEffectivePrimaryEnabled();
        var trailing = IsEffectiveTrailingEnabled();
        if (IsPrimaryEffectivelyEnabled != primary)
            SetCurrentValue(IsPrimaryEffectivelyEnabledProperty, primary);
        if (IsTrailingEffectivelyEnabled != trailing)
            SetCurrentValue(IsTrailingEffectivelyEnabledProperty, trailing);
        if (_primaryButton is not null && _primaryButton.IsEnabled != primary)
            _primaryButton.IsEnabled = primary;
        if (_trailingButton is not null && _trailingButton.IsEnabled != trailing)
            _trailingButton.IsEnabled = trailing;
    }

    private void ApplyMetrics()
    {
        var metrics = Metrics(ControlSize, ButtonShape);
        var rtl = FlowDirection == FlowDirection.RightToLeft;

        SetCurrentValue(MinHeightProperty, metrics.TotalHeight);
        SetCurrentValue(PaddingProperty, new Thickness(0, metrics.Inset));
        SetCurrentValue(FontSizeProperty, metrics.FontSize);
        SetCurrentValue(FontWeightProperty, metrics.FontWeight);
        SetCurrentValue(CornerRadiusProperty, new CornerRadius(metrics.Outer));
        SetCurrentValue(IconSizeProperty, metrics.IconSize);
        SetCurrentValue(TrailingIconSizeProperty, metrics.TrailingIconSize);
        SetCurrentValue(ContentSpacingProperty, metrics.ContentSpacing);
        SetCurrentValue(TrailingContentPaddingStartProperty, metrics.TrailingPadding);
        SetCurrentValue(TrailingContentPaddingEndProperty, metrics.TrailingPadding);

        var dimmed = Variant != MaterialButtonVariant.Outlined;
        _primaryButton?.Classes.Set("dimmed", dimmed);
        _trailingButton?.Classes.Set("dimmed", dimmed);

        if (_primaryButton is not null)
        {
            _primaryButton.MinHeight = metrics.SegmentHeight;
            _primaryButton.Padding = rtl
                ? new Thickness(metrics.PrimaryTrailingPadding, 0, metrics.PrimaryLeadingPadding, 0)
                : new Thickness(metrics.PrimaryLeadingPadding, 0, metrics.PrimaryTrailingPadding, 0);
        }

        if (_trailingButton is not null)
        {
            _trailingButton.MinHeight = metrics.SegmentHeight;
            _trailingButton.Width = metrics.TrailingWidth;
            _trailingButton.Padding = new Thickness(metrics.TrailingPadding, 0);
        }

        // Source seam: full-round outer corners; inner corners keep the physical side of the
        // leading/trailing halves, which swaps in right-to-left layouts.
        _primaryResting = rtl
            ? new CornerRadius(metrics.Inner, metrics.Outer, metrics.Outer, metrics.Inner)
            : new CornerRadius(metrics.Outer, metrics.Inner, metrics.Inner, metrics.Outer);
        _primaryPressed = rtl
            ? new CornerRadius(metrics.PressedInner, metrics.Outer, metrics.Outer, metrics.PressedInner)
            : new CornerRadius(metrics.Outer, metrics.PressedInner, metrics.PressedInner, metrics.Outer);
        _trailingResting = rtl
            ? new CornerRadius(metrics.Outer, metrics.Inner, metrics.Inner, metrics.Outer)
            : new CornerRadius(metrics.Inner, metrics.Outer, metrics.Outer, metrics.Inner);
        _trailingPressed = rtl
            ? new CornerRadius(metrics.Outer, metrics.PressedInner, metrics.PressedInner, metrics.Outer)
            : new CornerRadius(metrics.PressedInner, metrics.Outer, metrics.Outer, metrics.PressedInner);

        UpdateMenuShape(animate: false);
        UpdateLayout();
    }

    // Source shapeByInteraction precedence: pressed, then checked (the independent trailing state
    // or the open menu), then resting. Hover does not change the split button geometry; it only
    // draws the shared state layer.
    private void UpdateMenuShape(bool animate)
    {
        var reduced = MaterialMotion.GetIsReduced(this);
        if (_primaryButton is not null && _primaryCorners is not null)
        {
            var target = _primaryButton.IsPressed ? _primaryPressed : _primaryResting;
            _primaryCorners.Set(target, animate && !reduced, MaterialSpring.EffectsDefault);
        }

        if (_trailingButton is not null && _trailingCorners is not null)
        {
            var circle = new CornerRadius(_trailingButton.MinHeight / 2);
            var target = _trailingButton.IsPressed
                ? _trailingPressed
                : IsTrailingChecked || IsMenuOpen && Flyout is not null
                    ? circle
                    : _trailingResting;
            _trailingCorners.Set(target, animate && !reduced, MaterialSpring.EffectsDefault);
        }
    }

    // Source geometry per size: container height, target inset, leading content padding start/end,
    // trailing padding and icon sizes. The label style is the Expressive button style for the
    // height (XS/S label large, M title medium, L headline small, XL headline large).
    private static SplitMetrics Metrics(MaterialControlSize size, MaterialButtonShape shape)
    {
        var (total, segment, inset, leading, trailingPad, trailingWidth, leadingPad, chevron,
            spacing, font, weight, square, inner, pressedInner) = size switch
        {
            MaterialControlSize.ExtraSmall => (48d, 32d, 8d, 12d, 10d, 48d, 13d, 22d, 8d, 14d,
                FontWeight.Medium, 12d, 4d, 8d),
            MaterialControlSize.Small => (48d, 40d, 4d, 16d, 12d, 48d, 13d, 22d, 8d, 14,
                FontWeight.Medium, 12d, 4d, 12d),
            MaterialControlSize.Medium => (56d, 56d, 0d, 24d, 24d, 56d, 15d, 26d, 8d, 16,
                FontWeight.Medium, 16d, 4d, 12d),
            MaterialControlSize.Large => (96d, 96d, 0d, 48d, 48d, 96d, 29d, 38d, 12d, 24,
                FontWeight.Normal, 28d, 8d, 20d),
            _ => (136d, 136d, 0d, 64d, 64d, 136d, 43d, 50d, 16d, 32,
                FontWeight.Normal, 28d, 12d, 20d)
        };

        var outer = shape == MaterialButtonShape.Round ? segment / 2 : square;
        var seam = shape == MaterialButtonShape.Round ? inner : Math.Min(outer, inner);
        var seamPressed = shape == MaterialButtonShape.Round ? pressedInner : Math.Min(outer, pressedInner);
        return new SplitMetrics(total, segment, inset, leading, trailingPad, trailingWidth, leadingPad,
            chevron, spacing, font, weight, outer, seam, seamPressed);
    }

    private readonly record struct SplitMetrics(
        double TotalHeight, double SegmentHeight, double Inset, double PrimaryLeadingPadding,
        double PrimaryTrailingPadding, double TrailingWidth, double TrailingPadding,
        double TrailingIconSize, double ContentSpacing, double FontSize, FontWeight FontWeight,
        double Outer, double Inner, double PressedInner)
    {
        public double IconSize => SegmentHeight switch
        {
            32d or 40d => 20d,
            56d => 24d,
            96d => 32d,
            _ => 40d
        };
    }

    private sealed class MaterialSplitButtonAutomationPeer(MaterialSplitButton owner)
        : ControlAutomationPeer(owner), IInvokeProvider, IExpandCollapseProvider
    {
        public ExpandCollapseState ExpandCollapseState =>
            owner.IsMenuOpen ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public bool ShowsMenu => owner.Flyout is not null;

        public void Invoke()
        {
            if (!owner.IsPrimaryEffectivelyEnabled)
                throw new InvalidOperationException("The split button primary action is disabled.");

            // Route through the native button so automation follows the same Click-then-command
            // ordering as pointer and keyboard activation.
            if (owner._primaryButton is { } button &&
                CreatePeerForElement(button) is IInvokeProvider invoke)
            {
                invoke.Invoke();
                return;
            }

            owner.InvokePrimaryWithoutTemplate();
        }

        public void Expand()
        {
            if (!owner.IsEffectivelyEnabled || !owner.IsTrailingEffectivelyEnabled)
                throw new InvalidOperationException("The split button is disabled.");
            owner.ShowMenu();
        }

        public void Collapse() => owner.HideMenu();

        public void NotifyMenuStateChanged(bool open) =>
            RaisePropertyChangedEvent(
                ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
                open ? ExpandCollapseState.Collapsed : ExpandCollapseState.Expanded,
                open ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed);

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;
        protected override string GetClassNameCore() => nameof(MaterialSplitButton);
    }
}

/// <summary>The trailing half of a <see cref="MaterialSplitButton"/>.</summary>
/// <remarks>
/// <para>
/// Derives from <see cref="ToggleButton"/> to keep the native checked state, checked pseudo-class
/// and Toggle automation pattern, while the split button owns when a toggle is allowed.
/// <see cref="ToggleButton.Toggle"/> is deliberately inert: pointer, keyboard, access key and
/// automation activation all run through the native button <c>Click</c>, and
/// <see cref="MaterialSplitButton"/> updates <see cref="MaterialSplitButton.IsTrailingChecked"/>
/// after the public <see cref="MaterialSplitButton.TrailingClick"/> event. A handler that marks
/// that event handled therefore suppresses both the native
/// <see cref="MaterialSplitButton.TrailingCommand"/> and the toggle, matching the action-only
/// contract. The checkable state is independent from the flyout and the open menu.
/// </para>
/// <para>
/// The half stays a real button: commands, access keys, focus, invoke automation, disabled state
/// and flyout anchoring are native. The theme uses this type for <c>PART_TrailingButton</c>; a
/// custom template can still use a plain <see cref="Button"/>, which keeps every behavior except
/// the toggle automation pattern.
/// </para>
/// </remarks>
public class MaterialSplitButtonTrailingButton : ToggleButton
{
    internal MaterialSplitButton? SplitButton { get; set; }

    /// <summary>
    /// The split button owns the checkable decision, so the native toggle stays inert and all
    /// activation flows through the native <c>Click</c> path.
    /// </summary>
    protected override void Toggle()
    {
    }

    internal void PerformActivation() => OnClick();

    protected override AutomationPeer OnCreateAutomationPeer() =>
        new MaterialSplitButtonTrailingAutomationPeer(this);

    private sealed class MaterialSplitButtonTrailingAutomationPeer
        : ButtonAutomationPeer, IToggleProvider
    {
        private readonly MaterialSplitButtonTrailingButton _owner;

        public MaterialSplitButtonTrailingAutomationPeer(MaterialSplitButtonTrailingButton owner)
            : base(owner)
        {
            _owner = owner;
            owner.PropertyChanged += OnOwnerPropertyChanged;
        }

        public ToggleState ToggleState =>
            _owner.IsChecked == true ? ToggleState.On : ToggleState.Off;

        public void Toggle()
        {
            EnsureEnabled();
            // The default trailing half is action-only: the pattern reports Off and does nothing
            // until the caller opts into the checkable variant without a flyout.
            if (_owner.SplitButton is { CanToggleTrailing: true })
                _owner.PerformActivation();
        }

        private void OnOwnerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property != ToggleButton.IsCheckedProperty)
                return;

            RaisePropertyChangedEvent(
                TogglePatternIdentifiers.ToggleStateProperty,
                ToToggleState(e.OldValue as bool?),
                ToToggleState(e.NewValue as bool?));
        }

        private static ToggleState ToToggleState(bool? value) => value switch
        {
            true => ToggleState.On,
            false => ToggleState.Off,
            _ => ToggleState.Indeterminate
        };

        protected override AutomationControlType GetAutomationControlTypeCore() =>
            AutomationControlType.Button;

        protected override string GetClassNameCore() => nameof(MaterialSplitButtonTrailingButton);
    }
}