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
/// ShowAt(owner). Unlike a plain ToolTip, opening in the default Standard show mode moves focus
/// into the content. Escape and light dismiss close it and return focus to the owner; no hover
/// timer steals focus, and merely moving the pointer away does not dismiss actionable content.
/// <para>
/// Anatomy follows the source component: 312 px maximum width, 12 px corner radius,
/// surface-container background with the level 2 elevation, 16/12 content padding, 4 px
/// title-to-body spacing, an 8 px gap above the actions, and 4 px action insets.
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
    private FlyoutPresenter? _presenter;
    private ContentControl? _actionsPresenter;
    private IInputElement? _previousFocus;
    private Control? _keyboardOwner;
    private Control? _target;
    private bool _restoreFocus;
    private ScaleTransform? _scale;
    private MaterialMotion.SpringAnimation? _opacityAnimation;
    private MaterialMotion.SpringAnimation? _scaleAnimation;

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

        var presenter = new FlyoutPresenter { Content = root };
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
        _target = Target;
        if (FlyoutPresenterTheme is null && _target is { } resourceHost &&
            resourceHost.TryFindResource(PresenterThemeKey, out var value) && value is ControlTheme theme)
            FlyoutPresenterTheme = theme;

        base.OnOpening(args);
        if (args.Cancel) return;

        if (_target is { } target)
        {
            _previousFocus = TopLevel.GetTopLevel(target)?.FocusManager?.GetFocusedElement() ?? target;
            _keyboardOwner = target;
            target.RemoveHandler(InputElement.KeyDownEvent, OnPresenterKeyDown);
            target.AddHandler(InputElement.KeyDownEvent, OnPresenterKeyDown, RoutingStrategies.Tunnel);
            target.DetachedFromVisualTree -= OnTargetDetached;
            target.DetachedFromVisualTree += OnTargetDetached;
        }
        PrepareEntrance();
    }

    protected override void OnOpened()
    {
        base.OnOpened();
        StartEntrance();
    }

    protected override bool HideCore(bool canCancel = true)
    {
        // Capture containment before the popup is removed from its visual tree. A light
        // dismiss must not steal focus from a different control the user has just chosen.
        var focus = Target is { } target
            ? TopLevel.GetTopLevel(target)?.FocusManager?.GetFocusedElement()
            : null;
        _restoreFocus |= focus is Visual visual && Popup.Child is { } child &&
                         (visual == child || child.IsVisualAncestorOf(visual));
        return base.HideCore(canCancel);
    }

    protected override void OnClosed()
    {
        var previous = _previousFocus;
        var restore = _restoreFocus;
        if (_keyboardOwner is { } owner)
        {
            owner.RemoveHandler(InputElement.KeyDownEvent, OnPresenterKeyDown);
            owner.DetachedFromVisualTree -= OnTargetDetached;
        }
        _keyboardOwner = null;
        _target = null;
        _previousFocus = null;
        _restoreFocus = false;
        StopEntrance();
        base.OnClosed();
        if (restore && previous is Control { IsEffectivelyEnabled: true, IsVisible: true } control &&
            control.IsAttachedToVisualTree())
            control.Focus(NavigationMethod.Unspecified);
    }

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
            // Source actions are 40 px high with 16/10 padding inside a full-radius container.
            MinHeight = 40,
            Padding = new Thickness(16, 10),
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
        _actionsPresenter.IsVisible = ShowActions && _actionsPresenter.Content is not null;
        UpdateContentLayout();
        if (_actionsPresenter.IsAttachedToVisualTree())
            Dispatcher.UIThread.Post(ApplyActionStyling, DispatcherPriority.Loaded);
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
        _presenter.Padding = new Thickness(0, 0, 0, hasActions ? 8 : 0);
        _contentPanel.Padding = hasActions ? new Thickness(16, 12, 16, 4) : new Thickness(16, 12, 16, 12);
        _actionsPresenter.Padding = new Thickness(4, 0, 4, 0);

        var hasTitle = !string.IsNullOrWhiteSpace(Title);
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
        // The popup is not attached yet, so start from the hidden state and animate in OnOpened.
        _presenter.Opacity = 0;
        _scale = new ScaleTransform(0.96, 0.96);
        _presenter.RenderTransform = _scale;
        _presenter.RenderTransformOrigin = RelativePoint.Center;
    }

    private void StartEntrance()
    {
        if (_presenter is not { } presenter || _scale is not { } scale) return;
        _opacityAnimation = MaterialMotion.Animate(presenter, 0, 1, value => presenter.Opacity = value,
            MaterialSpring.EffectsFast);
        _scaleAnimation = MaterialMotion.Animate(presenter, 0.96, 1, value =>
        {
            scale.ScaleX = value;
            scale.ScaleY = value;
        }, MaterialSpring.SpatialFast);
    }

    private void StopEntrance()
    {
        _opacityAnimation?.Dispose();
        _opacityAnimation = null;
        _scaleAnimation?.Dispose();
        _scaleAnimation = null;
        if (_presenter is not null)
        {
            _presenter.Opacity = 1;
            _presenter.RenderTransform = null;
        }
        _scale = null;
    }

    // Popup light dismissal already closes the flyout when the owning window deactivates
    // (Popup.WindowDeactivated with IsLightDismissEnabled), so no extra window handler is needed.
    private void OnTargetDetached(object? sender, VisualTreeAttachmentEventArgs args) => Hide();

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
