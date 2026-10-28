using System.ComponentModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Theming;
using ExpressiveAvalonia.Tokens;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// An actionable Material rich tooltip hosted by Avalonia's native flyout lifecycle.
/// </summary>
/// <remarks>
/// Set <see cref="Title"/> to an accessible, localized heading, inherited Content to the
/// supporting content, and <see cref="Actions"/> to ordinary buttons or other focusable controls.
/// When no <see cref="Actions"/> object is supplied, <see cref="PrimaryActionText"/> and
/// <see cref="SecondaryActionText"/> create the two Material text buttons from the design, each
/// invoking its command and then closing the tooltip. Assign to Button.Flyout (click, touch and
/// keyboard activation), Control.ContextFlyout (context key and long press), or call
/// ShowAt(owner). Actionable tooltips use the Standard show mode and move focus into the content;
/// tooltips without actions use the Transient mode so they never steal focus. Escape and light
/// dismiss close them and return focus to the owner; no hover timer steals focus, and merely
/// moving the pointer away does not dismiss actionable content.
/// <para>
/// <see cref="IsPersistent"/> keeps actionable content open until dismissal. A transient tooltip
/// remains visible while its target is hovered or focused and closes 1.5 seconds after departure.
/// When unset, persistence is selected from the presence of actionable content.
/// </para>
/// <para>
/// Anatomy follows RichTooltipTokens and Tooltip.kt: 40 x 24 minimum, 320 px maximum width,
/// CornerMedium (12), surface-container background with level 2 elevation, 16 px horizontal
/// padding, 12 px above the subhead (28 px to its first baseline), 4 px between subhead and body,
/// 16 px below the body, a 36 px minimum action row and 8 px below it. The entrance and exit use
/// the source FastSpatial 0.8 → 1 scale and FastEffects fade; a re-enter during the exit
/// retargets the running springs and preserves their velocity.
/// </para>
/// </remarks>
public sealed class RichTooltip : Flyout
{
    /// <summary>The resource key of the presenter theme supplied by Themes/DesignTooltips.axaml.</summary>
    public const string PresenterThemeKey = "MaterialRichTooltipPresenter";

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<RichTooltip, string?>(nameof(Title));

    public static readonly StyledProperty<object?> ActionsProperty =
        AvaloniaProperty.Register<RichTooltip, object?>(nameof(Actions));

    public static readonly StyledProperty<IDataTemplate?> ActionsTemplateProperty =
        AvaloniaProperty.Register<RichTooltip, IDataTemplate?>(nameof(ActionsTemplate));

    public static readonly StyledProperty<bool> ShowTitleProperty =
        AvaloniaProperty.Register<RichTooltip, bool>(nameof(ShowTitle), true);

    public static readonly StyledProperty<bool> ShowActionsProperty =
        AvaloniaProperty.Register<RichTooltip, bool>(nameof(ShowActions), true);

    public static readonly StyledProperty<bool> ShowSecondaryActionProperty =
        AvaloniaProperty.Register<RichTooltip, bool>(nameof(ShowSecondaryAction), true);

    /// <summary>
    /// Adds the source RichTooltip caret: a 16 x 8 triangle on the edge that faces the owner.
    /// The source default is <c>caretShape = null</c>, so this property defaults to
    /// <see langword="false"/> and leaves the presenter anatomy untouched.
    /// </summary>
    public static readonly StyledProperty<bool> ShowCaretProperty =
        AvaloniaProperty.Register<RichTooltip, bool>(nameof(ShowCaret));

    /// <summary>Defines the <see cref="IsPersistent"/> property.</summary>
    /// <remarks>
    /// The registered default is the source <c>rememberTooltipState(isPersistent = false)</c>
    /// default. <see cref="EffectiveIsPersistent"/> resolves an unset value from the tooltip's
    /// action content, so this raw value alone does not decide the behavior.
    /// </remarks>
    public static readonly StyledProperty<bool> IsPersistentProperty =
        AvaloniaProperty.Register<RichTooltip, bool>(nameof(IsPersistent));

    public static readonly StyledProperty<string?> PrimaryActionTextProperty =
        AvaloniaProperty.Register<RichTooltip, string?>(nameof(PrimaryActionText));

    public static readonly StyledProperty<string?> SecondaryActionTextProperty =
        AvaloniaProperty.Register<RichTooltip, string?>(nameof(SecondaryActionText));

    public static readonly StyledProperty<ICommand?> PrimaryActionCommandProperty =
        AvaloniaProperty.Register<RichTooltip, ICommand?>(nameof(PrimaryActionCommand));

    public static readonly StyledProperty<ICommand?> SecondaryActionCommandProperty =
        AvaloniaProperty.Register<RichTooltip, ICommand?>(nameof(SecondaryActionCommand));

    public static readonly StyledProperty<object?> PrimaryActionCommandParameterProperty =
        AvaloniaProperty.Register<RichTooltip, object?>(nameof(PrimaryActionCommandParameter));

    public static readonly StyledProperty<object?> SecondaryActionCommandParameterProperty =
        AvaloniaProperty.Register<RichTooltip, object?>(nameof(SecondaryActionCommandParameter));

    private TextBlock? _titlePresenter;
    private TextBlock? _bodyText;
    private ContentPresenter? _bodyPresenter;
    private Border? _contentPanel;
    private MaterialRichTooltipPresenter? _presenter;
    private ContentControl? _actionsPresenter;
    private IInputElement? _previousFocus;
    private Control? _keyboardOwner;
    private Control? _target;
    private bool _restoreFocus;
    private ScaleTransform? _scale;
    private MaterialMotion.SpringAnimation? _opacityAnimation;
    private MaterialMotion.SpringAnimation? _scaleAnimation;
    private ScaleTransform? _exitScale;
    private MaterialMotion.SpringAnimation? _exitOpacityAnimation;
    private MaterialMotion.SpringAnimation? _exitScaleAnimation;
    private DispatcherTimer? _exitTimer;
    private DispatcherTimer? _durationTimer;
    private bool _exiting;
    private bool _skipExit;

    // Tooltip.kt BasicTooltipDefaults.TooltipDuration: how long a non-persistent tooltip stays.
    private const int TooltipDurationMilliseconds = 1500;

    static RichTooltip()
    {
        IsPersistentProperty.Changed.AddClassHandler<RichTooltip>((tooltip, _) => tooltip.SyncDurationTimer());
    }

    public RichTooltip()
    {
        Placement = PlacementMode.Bottom;
        ShowMode = FlyoutShowMode.Standard;
        FlyoutPresenterClasses.Add("material-rich-tooltip");
    }

    /// <summary>A localized heading, also used as the accessible name of the popup.</summary>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Focusable action content, normally a horizontal panel of native buttons.</summary>
    public object? Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }

    public IDataTemplate? ActionsTemplate
    {
        get => GetValue(ActionsTemplateProperty);
        set => SetValue(ActionsTemplateProperty, value);
    }

    /// <summary>Shows the subhead. Hiding it keeps the text available as the accessible name.</summary>
    public bool ShowTitle
    {
        get => GetValue(ShowTitleProperty);
        set => SetValue(ShowTitleProperty, value);
    }

    /// <summary>Shows the action row. Ignored when no action content exists.</summary>
    public bool ShowActions
    {
        get => GetValue(ShowActionsProperty);
        set => SetValue(ShowActionsProperty, value);
    }

    /// <summary>Shows the generated secondary action button.</summary>
    public bool ShowSecondaryAction
    {
        get => GetValue(ShowSecondaryActionProperty);
        set => SetValue(ShowSecondaryActionProperty, value);
    }

    /// <summary>Shows the optional 16 x 8 caret; the default matches the source null caret.</summary>
    public bool ShowCaret
    {
        get => GetValue(ShowCaretProperty);
        set => SetValue(ShowCaretProperty, value);
    }

    /// <summary>
    /// Whether the tooltip remains visible until it is dismissed explicitly, mirroring
    /// <c>TooltipState.isPersistent</c> in Tooltip.kt.
    /// </summary>
    /// <remarks>
    /// An unset value is resolved by <see cref="EffectiveIsPersistent"/>: the source recommends
    /// <c>isPersistent = true</c> "when there is actionable content being displayed within a
    /// tooltip", and every RichTooltip sample passes it, so an actionable tooltip is persistent and
    /// an actionless one is non-persistent. A non-persistent tooltip dismisses 1.5 seconds after
    /// the pointer and keyboard focus leave its target.
    /// </remarks>
    public bool IsPersistent
    {
        get => GetValue(IsPersistentProperty);
        set => SetValue(IsPersistentProperty, value);
    }

    /// <summary>
    /// The persistence actually used: an explicit <see cref="IsPersistent"/> value wins, otherwise
    /// the tooltip is persistent only when it has actionable content.
    /// </summary>
    public bool EffectiveIsPersistent => IsSet(IsPersistentProperty) ? IsPersistent : HasActions();

    /// <summary>
    /// Label of the primary text action. Ignored when <see cref="Actions"/> is set. Empty removes
    /// the button, mirroring the source "Show actions" and "Show secondary button" properties.
    /// </summary>
    public string? PrimaryActionText
    {
        get => GetValue(PrimaryActionTextProperty);
        set => SetValue(PrimaryActionTextProperty, value);
    }

    /// <summary>Label of the secondary text action; empty hides the second button.</summary>
    public string? SecondaryActionText
    {
        get => GetValue(SecondaryActionTextProperty);
        set => SetValue(SecondaryActionTextProperty, value);
    }

    public ICommand? PrimaryActionCommand
    {
        get => GetValue(PrimaryActionCommandProperty);
        set => SetValue(PrimaryActionCommandProperty, value);
    }

    public ICommand? SecondaryActionCommand
    {
        get => GetValue(SecondaryActionCommandProperty);
        set => SetValue(SecondaryActionCommandProperty, value);
    }

    public object? PrimaryActionCommandParameter
    {
        get => GetValue(PrimaryActionCommandParameterProperty);
        set => SetValue(PrimaryActionCommandParameterProperty, value);
    }

    public object? SecondaryActionCommandParameter
    {
        get => GetValue(SecondaryActionCommandParameterProperty);
        set => SetValue(SecondaryActionCommandParameterProperty, value);
    }

    protected override Control CreatePresenter()
    {
        _titlePresenter = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            IsVisible = ShowTitle && !string.IsNullOrEmpty(Title),
            [!TextBlock.TextProperty] = this[!TitleProperty]
        };
        MaterialTypography.SetStyle(_titlePresenter, MaterialTypeStyle.TitleSmall);
        AutomationProperties.SetHeadingLevel(_titlePresenter, 2);

        // String content is presented by a TextBlock of our own: ContentPresenter's
        // TextWrapping does not reach the TextBlock it generates for strings, and a ControlTheme
        // cannot reach that child with a descendant selector. Non-string content keeps the
        // ContentPresenter so a control or a data template renders unchanged.
        _bodyText = new TextBlock { TextWrapping = TextWrapping.Wrap };
        MaterialTypography.SetStyle(_bodyText, MaterialTypeStyle.BodyMedium);
        _bodyPresenter = new ContentPresenter
        {
            [!ContentPresenter.ContentTemplateProperty] = this[!ContentTemplateProperty]
        };
        MaterialTypography.SetStyle(_bodyPresenter, MaterialTypeStyle.BodyMedium);

        var contentStack = new StackPanel { Spacing = 4 };
        contentStack.Children.Add(_titlePresenter);
        contentStack.Children.Add(_bodyText);
        contentStack.Children.Add(_bodyPresenter);
        // Border supplies the content padding; StackPanel has no Padding property.
        _contentPanel = new Border { Child = contentStack };

        _actionsPresenter = new ContentControl
        {
            Name = "Actions",
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            IsVisible = false
        };
        MaterialTypography.SetStyle(_actionsPresenter, MaterialTypeStyle.LabelLarge);
        _actionsPresenter.Bind(TemplatedControl.ForegroundProperty,
            _actionsPresenter.GetResourceObservable("MaterialPrimaryBrush"));
        // A ControlTheme cannot hold descendant selectors, so consumer-supplied action buttons
        // are switched to the tooltip's text-button variant here, after they are attached.
        _actionsPresenter.AttachedToVisualTree += (_, _) =>
            Dispatcher.UIThread.Post(ApplyActionStyling, DispatcherPriority.Loaded);

        var root = new StackPanel { Spacing = 8 };
        root.Children.Add(_contentPanel);
        root.Children.Add(_actionsPresenter);

        var presenter = new MaterialRichTooltipPresenter { Content = root };
        presenter[!MaterialRichTooltipPresenter.ShowCaretProperty] = this[!ShowCaretProperty];
        _presenter = presenter;
        AutomationProperties.SetControlTypeOverride(presenter, AutomationControlType.Pane);
        presenter.AddHandler(InputElement.KeyDownEvent, OnPresenterKeyDown, RoutingStrategies.Tunnel);
        UpdateBodyContent();
        UpdateActions();
        UpdateContentLayout();
        return presenter;
    }

    protected override void OnOpening(CancelEventArgs args)
    {
        CancelExit();
        _target = Target;
        if (FlyoutPresenterTheme is null && _target is { } resourceHost &&
            resourceHost.TryFindResource(PresenterThemeKey, out var value) && value is ControlTheme theme)
            FlyoutPresenterTheme = theme;

        base.OnOpening(args);
        if (args.Cancel) return;

        // Source TooltipBox: only an actionable tooltip takes focus and traps Tab; a tooltip
        // without actions must never steal focus from the owning control.
        var actionable = _actionsPresenter?.IsVisible == true;
        ShowMode = actionable ? FlyoutShowMode.Standard : FlyoutShowMode.Transient;
        if (_presenter is not null)
            _presenter.Focusable = actionable;
        if (_target is { } target)
        {
            _previousFocus = TopLevel.GetTopLevel(target)?.FocusManager?.GetFocusedElement() ?? target;
            _keyboardOwner = target;
            target.RemoveHandler(InputElement.KeyDownEvent, OnPresenterKeyDown);
            target.AddHandler(InputElement.KeyDownEvent, OnPresenterKeyDown, RoutingStrategies.Tunnel);
            target.DetachedFromVisualTree -= OnTargetDetached;
            target.DetachedFromVisualTree += OnTargetDetached;
            target.PointerEntered -= OnTargetPointerEntered;
            target.PointerEntered += OnTargetPointerEntered;
            target.PointerExited += OnTargetPointerExited;
            target.GotFocus += OnTargetFocusChanged;
            target.LostFocus += OnTargetFocusChanged;
        }
        if (_presenter is { } presenter)
        {
            presenter.Anchor = _target;
            presenter.Placement = Placement;
        }
        PrepareEntrance();
    }

    protected override void OnOpened()
    {
        base.OnOpened();
        StartEntrance();
        // OnOpened is the authoritative "now visible" point for the source duration.
        StopDurationTimer();
        if (!EffectiveIsPersistent)
            SyncDurationTimer();
    }

    protected override bool HideCore(bool canCancel = true)
    {
        StopDurationTimer();
        // Capture containment before the popup is removed from its visual tree. A light
        // dismiss must not steal focus from a different control the user has just chosen.
        var focus = Target is { } target
            ? TopLevel.GetTopLevel(target)?.FocusManager?.GetFocusedElement()
            : null;
        _restoreFocus |= focus is Visual visual && Popup.Child is { } child &&
                         (visual == child || child.IsVisualAncestorOf(visual));

        if (_exiting)
            return true;

        if (_skipExit || _presenter is not { } presenter || MaterialMotion.GetIsReduced(presenter) ||
            !presenter.IsAttachedToVisualTree())
        {
            StopExit();
            return base.HideCore(canCancel);
        }

        // Source TooltipBox close: scale 1 → 0.8 (FastSpatial) and alpha 1 → 0 (FastEffects).
        // The popup stays open for the duration of the springs, so the hide is deferred until they
        // settle; it is made non-hit-testable immediately so the closing tooltip cannot take a
        // click that belongs to the application.
        _exiting = true;
        var currentScale = _scale?.ScaleX ?? 1;
        var currentOpacity = presenter.Opacity;
        StopEntrance(keepVisualState: true);
        presenter.IsHitTestVisible = false;

        _exitScale = new ScaleTransform(currentScale, currentScale);
        presenter.RenderTransform = _exitScale;
        presenter.RenderTransformOrigin = RelativePoint.Center;
        _exitOpacityAnimation = MaterialMotion.Animate(presenter, currentOpacity, 0,
            value => presenter.Opacity = value, MaterialSpring.EffectsFast);
        _exitScaleAnimation = MaterialMotion.Animate(presenter, currentScale, 0.8, value =>
        {
            if (_exitScale is { } scale)
            {
                scale.ScaleX = value;
                scale.ScaleY = value;
            }
        }, MaterialSpring.SpatialFast);

        _exitTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, (_, _) =>
        {
            if (_exitOpacityAnimation is { IsRunning: true } || _exitScaleAnimation is { IsRunning: true })
                return;
            _exitTimer?.Stop();
            _exitTimer = null;
            if (!_exiting) return;
            _exiting = false;
            StopExit();
            base.HideCore(canCancel);
        });
        _exitTimer.Start();
        return true;
    }

    protected override void OnClosed()
    {
        StopDurationTimer();
        var previous = _previousFocus;
        var restore = _restoreFocus;
        if (_keyboardOwner is { } owner)
        {
            owner.RemoveHandler(InputElement.KeyDownEvent, OnPresenterKeyDown);
            owner.DetachedFromVisualTree -= OnTargetDetached;
            owner.PointerEntered -= OnTargetPointerEntered;
            owner.PointerExited -= OnTargetPointerExited;
            owner.GotFocus -= OnTargetFocusChanged;
            owner.LostFocus -= OnTargetFocusChanged;
        }
        _keyboardOwner = null;
        _target = null;
        _previousFocus = null;
        _restoreFocus = false;
        _exiting = false;
        if (_presenter is { } presenter) presenter.Anchor = null;
        StopExit();
        StopEntrance();
        base.OnClosed();
        if (restore && previous is Control { IsEffectivelyEnabled: true, IsVisible: true } control &&
            control.IsAttachedToVisualTree())
            control.Focus(NavigationMethod.Unspecified);
    }

    /// <summary>
    /// Re-entering the owner during the exit retargets the running springs to the visible state.
    /// The springs preserve their velocity, so the tooltip reverses without snapping.
    /// </summary>
    private void OnTargetPointerEntered(object? sender, PointerEventArgs args)
    {
        StopDurationTimer();
        if (!_exiting)
            return;

        _exitTimer?.Stop();
        _exitTimer = null;
        _exiting = false;
        if (_presenter is { } presenter)
            presenter.IsHitTestVisible = true;

        // Ownership of the running springs transfers back to the entrance pair: retargeting keeps
        // their current position and velocity, so the tooltip reverses without a snap.
        _exitOpacityAnimation?.Retarget(1);
        _exitScaleAnimation?.Retarget(1);
        _opacityAnimation = _exitOpacityAnimation;
        _scaleAnimation = _exitScaleAnimation;
        _scale = _exitScale;
        _exitOpacityAnimation = null;
        _exitScaleAnimation = null;
        _exitScale = null;

        // The tooltip is visible again: a non-persistent one gets a fresh source duration instead
        // of staying on screen without a dismissal path.
        SyncDurationTimer();
    }

    private void OnTargetPointerExited(object? sender, PointerEventArgs args) =>
        Dispatcher.UIThread.Post(SyncDurationTimer, DispatcherPriority.Input);

    private void OnTargetFocusChanged(object? sender, FocusChangedEventArgs args) =>
        Dispatcher.UIThread.Post(SyncDurationTimer, DispatcherPriority.Input);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TitleProperty || change.Property == ShowTitleProperty)
        {
            if (_titlePresenter is not null)
                _titlePresenter.IsVisible = ShowTitle && !string.IsNullOrEmpty(Title);
            UpdateContentLayout();
        }
        else if (change.Property == ContentProperty || change.Property == ContentTemplateProperty)
        {
            UpdateBodyContent();
            UpdateContentLayout();
        }
        else if (change.Property == ActionsProperty || change.Property == ActionsTemplateProperty ||
                 change.Property == ShowActionsProperty || change.Property == ShowSecondaryActionProperty ||
                 change.Property == PrimaryActionTextProperty || change.Property == SecondaryActionTextProperty ||
                 change.Property == PrimaryActionCommandProperty || change.Property == SecondaryActionCommandProperty ||
                 change.Property == PrimaryActionCommandParameterProperty ||
                 change.Property == SecondaryActionCommandParameterProperty)
        {
            UpdateActions();
        }
    }

    /// <summary>
    /// Builds the design's two text actions when no <see cref="Actions"/> object is supplied.
    /// </summary>
    private Control? BuildDefaultActions()
    {
        var panel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8 };
        if (!string.IsNullOrEmpty(PrimaryActionText))
            panel.Children.Add(CreateActionButton(PrimaryActionText, PrimaryActionCommand, PrimaryActionCommandParameter));
        if (ShowSecondaryAction && !string.IsNullOrEmpty(SecondaryActionText))
            panel.Children.Add(CreateActionButton(SecondaryActionText, SecondaryActionCommand, SecondaryActionCommandParameter));
        return panel.Children.Count > 0 ? panel : null;
    }

    private Button CreateActionButton(string text, ICommand? command, object? parameter)
    {
        var button = new Button
        {
            Content = text,
            Command = command,
            CommandParameter = parameter,
            // Tooltip.kt ActionLabelMinHeight = 36; the text button supplies a 20 px label.
            MinHeight = 36,
            Padding = new Thickness(16, 8),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        // The Text variant supplies the primary label color and transparent container; the
        // tooltip's own action row does not need the 48 px touch-target inset.
        MaterialAssist.SetButtonVariant(button, MaterialButtonVariant.Text);
        MaterialAssist.SetContainerInset(button, default);
        AutomationProperties.SetName(button, text);
        button.Click += OnActionClick;
        return button;
    }

    private void OnActionClick(object? sender, RoutedEventArgs args)
    {
        _restoreFocus = true;
        Hide();
    }

    private void UpdateActions()
    {
        if (_actionsPresenter is null) return;
        if (Actions is not null)
        {
            _actionsPresenter.Content = Actions;
            _actionsPresenter.ContentTemplate = ActionsTemplate;
        }
        else
        {
            _actionsPresenter.Content = BuildDefaultActions();
            _actionsPresenter.ContentTemplate = null;
        }
        _actionsPresenter.IsVisible = HasActions();
        UpdateContentLayout();
        // Action content decides the default persistence, so an open tooltip re-evaluates the
        // source duration instead of closing a tooltip that just became actionable.
        SyncDurationTimer();
        if (_actionsPresenter.IsAttachedToVisualTree())
            Dispatcher.UIThread.Post(ApplyActionStyling, DispatcherPriority.Loaded);
    }

    /// <summary>
    /// Whether the tooltip offers actionable content, using the same rule
    /// <see cref="UpdateActions"/> applies to the action presenter.
    /// </summary>
    private bool HasActions()
    {
        if (!ShowActions)
            return false;
        if (Actions is not null)
            return true;
        return !string.IsNullOrEmpty(PrimaryActionText) ||
               (ShowSecondaryAction && !string.IsNullOrEmpty(SecondaryActionText));
    }

    /// <summary>
    /// Shows string content through the wrapping <see cref="_bodyText"/> and everything else
    /// through the <see cref="_bodyPresenter"/>.
    /// </summary>
    private void UpdateBodyContent()
    {
        // A consumer template always wins, even for string content.
        var text = ContentTemplate is null ? Content as string : null;
        var showText = !string.IsNullOrEmpty(text);
        if (_bodyText is not null)
        {
            _bodyText.Text = showText ? text : null;
            _bodyText.IsVisible = showText;
        }
        if (_bodyPresenter is not null)
        {
            _bodyPresenter.Content = showText ? null : Content;
            _bodyPresenter.IsVisible = !showText && Content is not null;
        }
    }

    /// <summary>
    /// Applies the rich tooltip's text-button variant to action buttons the consumer supplied.
    /// Buttons that already carry an explicit <see cref="MaterialAssist.ButtonVariantProperty"/>
    /// value keep it, so authored styling always wins.
    /// </summary>
    private void ApplyActionStyling()
    {
        if (_actionsPresenter is null) return;
        foreach (var button in _actionsPresenter.GetVisualDescendants().OfType<Button>())
        {
            if (button.IsSet(MaterialAssist.ButtonVariantProperty)) continue;
            MaterialAssist.SetButtonVariant(button, MaterialButtonVariant.Text);
        }
    }

    private void UpdateContentLayout()
    {
        if (_presenter is null || _contentPanel is null || _actionsPresenter is null)
            return;

        var hasActions = _actionsPresenter.IsVisible;
        var hasTitle = !string.IsNullOrWhiteSpace(Title);
        // Tooltip.kt RichTooltipContent: 16 px horizontal; with a subhead or actions the body ends
        // 16 px above the container bottom (and the subhead starts 28 px to its first baseline,
        // ≈12 px box inset), otherwise the text has 4 px vertical padding.
        _presenter.Padding = new Thickness(0, 0, 0, hasActions ? 8 : 0);
        _contentPanel.Padding = hasTitle || hasActions
            ? new Thickness(16, 12, 16, 16)
            : new Thickness(16, 4, 16, 4);
        _actionsPresenter.Padding = default;

        var body = Content as string;
        AutomationProperties.SetName(_presenter, hasTitle ? Title : body);
        // With no subhead the body already is the name; only add help text when it is distinct.
        AutomationProperties.SetHelpText(_presenter,
            hasTitle && !string.IsNullOrWhiteSpace(body) ? body : null);
    }

    private void PrepareEntrance()
    {
        if (_presenter is null) return;
        StopEntrance();
        var reduced = MaterialMotion.GetIsReduced(_presenter) ||
                      (_target is { } target && MaterialMotion.GetIsReduced(target));
        if (reduced)
        {
            _presenter.Opacity = 1;
            _presenter.RenderTransform = null;
            _scale = null;
            return;
        }
        // The popup is not attached yet, so start from the closed state and animate in OnOpened.
        _presenter.Opacity = 0;
        _presenter.RenderTransformOrigin = RelativePoint.Center;
        _scale = new ScaleTransform(0.8, 0.8);
        _presenter.RenderTransform = _scale;
    }

    private void StartEntrance(bool restart = true)
    {
        if (_presenter is not { } presenter) return;
        if (restart && _scale is null)
            _scale = new ScaleTransform(0.8, 0.8);
        if (_scale is not { } scale) return;
        if (restart)
            presenter.RenderTransform = scale;
        presenter.RenderTransformOrigin = RelativePoint.Center;

        _opacityAnimation?.Dispose();
        _scaleAnimation?.Dispose();
        _opacityAnimation = MaterialMotion.Animate(presenter, presenter.Opacity, 1,
            value => presenter.Opacity = value, MaterialSpring.EffectsFast);
        _scaleAnimation = MaterialMotion.Animate(presenter, scale.ScaleX, 1, value =>
        {
            scale.ScaleX = value;
            scale.ScaleY = value;
        }, MaterialSpring.SpatialFast);
    }

    private void StopEntrance(bool keepVisualState = false)
    {
        _opacityAnimation?.Dispose();
        _opacityAnimation = null;
        _scaleAnimation?.Dispose();
        _scaleAnimation = null;
        if (_presenter is not null && !keepVisualState)
        {
            _presenter.Opacity = 1;
            _presenter.RenderTransform = null;
            _presenter.IsHitTestVisible = true;
        }
        if (!keepVisualState)
            _scale = null;
    }

    /// <summary>
    /// The source duration currently scheduled for a non-persistent tooltip, or
    /// <see langword="null"/> when the tooltip is persistent or closed.
    /// </summary>
    internal TimeSpan? ScheduledDismissalInterval => _durationTimer?.Interval;

    /// <summary>
    /// Starts, restarts or stops the source duration according to the current persistence.
    /// </summary>
    private void SyncDurationTimer()
    {
        StopDurationTimer();
        if (!IsOpen || EffectiveIsPersistent ||
            _target is { } target && (target.IsPointerOver || target.IsKeyboardFocusWithin))
            return;
        _durationTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(TooltipDurationMilliseconds),
            DispatcherPriority.Input,
            (_, _) => DismissAfterDuration());
        _durationTimer.Start();
    }

    private void DismissAfterDuration()
    {
        StopDurationTimer();
        if (IsOpen)
            Hide();
    }

    private void StopDurationTimer()
    {
        if (_durationTimer is null) return;
        _durationTimer.Stop();
        _durationTimer = null;
    }

    /// <summary>Cancels a deferred close when the tooltip is opened again before it settles.</summary>
    private void CancelExit()
    {
        if (!_exiting && _exitTimer is null)
            return;
        _exitTimer?.Stop();
        _exitTimer = null;
        _exitOpacityAnimation?.Dispose();
        _exitOpacityAnimation = null;
        _exitScaleAnimation?.Dispose();
        _exitScaleAnimation = null;
        _exiting = false;
        StopExit();
    }

    private void StopExit()
    {
        _exitTimer?.Stop();
        _exitTimer = null;
        _exitOpacityAnimation?.Dispose();
        _exitOpacityAnimation = null;
        _exitScaleAnimation?.Dispose();
        _exitScaleAnimation = null;
        _exitScale = null;
        if (_presenter is not null)
        {
            _presenter.Opacity = 1;
            _presenter.RenderTransform = null;
            _presenter.IsHitTestVisible = true;
        }
    }

    // Popup light dismissal already closes the flyout when the owning window deactivates
    // (Popup.WindowDeactivated with IsLightDismissEnabled), so no extra window handler is needed.
    private void OnTargetDetached(object? sender, VisualTreeAttachmentEventArgs args)
    {
        // The owner is gone: close immediately instead of leaving a 150 ms exit timer behind.
        _skipExit = true;
        try
        {
            Hide();
        }
        finally
        {
            _skipExit = false;
        }
    }

    private void OnPresenterKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && !e.Handled)
        {
            _restoreFocus = true;
            Hide();
            e.Handled = true;
        }
    }
}

/// <summary>
/// Presenter used by <see cref="RichTooltip"/>. It draws the optional 16 x 8 caret in the margin
/// reserved around the rich surface, using the real popup and anchor screen bounds so a flipped or
/// window-clamped placement still points at the owner.
/// </summary>
public sealed class MaterialRichTooltipPresenter : FlyoutPresenter
{
    /// <summary>Enables the caret; false is the source <c>caretShape = null</c> default.</summary>
    public static readonly StyledProperty<bool> ShowCaretProperty =
        AvaloniaProperty.Register<MaterialRichTooltipPresenter, bool>(nameof(ShowCaret));

    /// <summary>The control the caret points at.</summary>
    public static readonly StyledProperty<Control?> AnchorProperty =
        AvaloniaProperty.Register<MaterialRichTooltipPresenter, Control?>(nameof(Anchor));

    /// <summary>The space around the surface reserved for the caret on one edge.</summary>
    public static readonly StyledProperty<Thickness> SurfaceMarginProperty =
        AvaloniaProperty.Register<MaterialRichTooltipPresenter, Thickness>(nameof(SurfaceMargin));

    /// <summary>The requested placement, used before the popup has resolved its actual bounds.</summary>
    public static readonly StyledProperty<PlacementMode> PlacementProperty =
        AvaloniaProperty.Register<MaterialRichTooltipPresenter, PlacementMode>(nameof(Placement), PlacementMode.Bottom);

    private Control? _surface;
    private TooltipCaretRenderer? _renderer;

    public bool ShowCaret
    {
        get => GetValue(ShowCaretProperty);
        set => SetValue(ShowCaretProperty, value);
    }

    public Control? Anchor
    {
        get => GetValue(AnchorProperty);
        set => SetValue(AnchorProperty, value);
    }

    public Thickness SurfaceMargin
    {
        get => GetValue(SurfaceMarginProperty);
        set => SetValue(SurfaceMarginProperty, value);
    }

    public PlacementMode Placement
    {
        get => GetValue(PlacementProperty);
        set => SetValue(PlacementProperty, value);
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _surface = e.NameScope.Find<Control>("PART_Surface");
        _renderer = e.NameScope.Find<TooltipCaretRenderer>("PART_Caret");
        if (_renderer is not null) _renderer.Surface = _surface;
        UpdateCaret();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        LayoutUpdated += OnLayoutUpdated;
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        LayoutUpdated -= OnLayoutUpdated;
        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ShowCaretProperty || change.Property == AnchorProperty ||
            change.Property == PlacementProperty)
            UpdateCaret();
    }

    private void OnLayoutUpdated(object? sender, EventArgs e) => UpdateCaret();

    private void UpdateCaret()
    {
        if (_renderer is null) return;
        _renderer.Anchor = Anchor;
        _renderer.ShowCaret = ShowCaret;
        _renderer.Placement = Placement;
        _renderer.Fill = Background;
        var side = _renderer.ResolveSide() ?? GuessSide(Placement);
        var margin = ShowCaret ? TooltipCaretRenderer.MarginFor(side) : default;
        if (SurfaceMargin != margin) SurfaceMargin = margin;
        _renderer.InvalidateVisual();
    }

    private static TooltipCaretSide GuessSide(PlacementMode placement) => placement switch
    {
        PlacementMode.Top => TooltipCaretSide.Bottom,
        PlacementMode.Left => TooltipCaretSide.Right,
        PlacementMode.Right => TooltipCaretSide.Left,
        _ => TooltipCaretSide.Top,
    };
}
