using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using ExpressiveAvalonia.Theming;
using Avalonia.VisualTree;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// An actionable Material tooltip hosted by Avalonia's native flyout lifecycle.
/// </summary>
/// <remarks>
/// Set <see cref="Title"/> to an accessible, localized heading, inherited Content to the
/// supporting content, and <see cref="Actions"/> to ordinary buttons or other focusable controls.
/// Assign to Button.Flyout (click, touch and keyboard activation), Control.ContextFlyout
/// (context key and long press), or call ShowAt(owner). Unlike a plain ToolTip, opening in
/// the default Standard show mode moves focus into the content. Escape and light dismiss
/// close it; actions can call Hide after their command completes. No hover timer steals
/// focus, and merely moving the pointer away does not dismiss actionable content.
/// </remarks>
public sealed class RichTooltip : Flyout
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<RichTooltip, string?>(nameof(Title));

    public static readonly StyledProperty<object?> ActionsProperty =
        AvaloniaProperty.Register<RichTooltip, object?>(nameof(Actions));

    public static readonly StyledProperty<IDataTemplate?> ActionsTemplateProperty =
        AvaloniaProperty.Register<RichTooltip, IDataTemplate?>(nameof(ActionsTemplate));

    private TextBlock? _titlePresenter;
    private ContentControl? _bodyPresenter;
    private FlyoutPresenter? _presenter;
    private ContentControl? _actionsPresenter;
    private IInputElement? _previousFocus;
    private Control? _keyboardOwner;
    private bool _restoreFocus;

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

    protected override Control CreatePresenter()
    {
        _titlePresenter = new TextBlock
        {
            LineHeight = 20,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 4),
            IsVisible = !string.IsNullOrEmpty(Title),
            [!TextBlock.TextProperty] = this[!TitleProperty]
        };
        MaterialTypography.SetStyle(_titlePresenter, MaterialTypeStyle.TitleSmall);
        AutomationProperties.SetHeadingLevel(_titlePresenter, 2);

        var body = new ContentControl
        {
            [!ContentControl.ContentProperty] = this[!ContentProperty],
            [!ContentControl.ContentTemplateProperty] = this[!ContentTemplateProperty]
        };
        MaterialTypography.SetStyle(body, MaterialTypeStyle.BodyMedium);
        _bodyPresenter = body;
        _actionsPresenter = new ContentControl
        {
            MinHeight = 36,
            Name = "Actions",
            Padding = new Thickness(0, 0, 0, 8),
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            IsVisible = Actions is not null,
            [!ContentControl.ContentProperty] = this[!ActionsProperty],
            [!ContentControl.ContentTemplateProperty] = this[!ActionsTemplateProperty]
        };
        MaterialTypography.SetStyle(_actionsPresenter, MaterialTypeStyle.LabelLarge);
        _actionsPresenter.Bind(ContentControl.ForegroundProperty,
            _actionsPresenter.GetResourceObservable("MaterialPrimaryBrush"));
        var panel = new StackPanel();
        panel.Children.Add(_titlePresenter);
        panel.Children.Add(body);
        panel.Children.Add(_actionsPresenter);

        var presenter = new FlyoutPresenter { Content = panel };
        _presenter = presenter;
        UpdateContentLayout();
        presenter[!AutomationProperties.NameProperty] = this[!TitleProperty];
        AutomationProperties.SetControlTypeOverride(presenter, AutomationControlType.Pane);
        presenter.AddHandler(InputElement.KeyDownEvent, OnPresenterKeyDown, RoutingStrategies.Tunnel);
        return presenter;
    }

    protected override void OnOpening(CancelEventArgs args)
    {
        base.OnOpening(args);
        if (!args.Cancel && Target is { } target)
        {
            _previousFocus = TopLevel.GetTopLevel(target)?.FocusManager?.GetFocusedElement() ?? target;
            _keyboardOwner = target;
            target.AddHandler(InputElement.KeyDownEvent, OnPresenterKeyDown, RoutingStrategies.Tunnel);
        }
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
            owner.RemoveHandler(InputElement.KeyDownEvent, OnPresenterKeyDown);
        _keyboardOwner = null;
        _previousFocus = null;
        _restoreFocus = false;
        base.OnClosed();
        if (restore && previous is Control { IsEffectivelyEnabled: true, IsVisible: true } control &&
            control.IsAttachedToVisualTree())
            control.Focus(NavigationMethod.Unspecified);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TitleProperty && _titlePresenter is not null)
            _titlePresenter.IsVisible = !string.IsNullOrEmpty(Title);
        else if (change.Property == ActionsProperty && _actionsPresenter is not null)
            _actionsPresenter.IsVisible = Actions is not null;
        if (change.Property == TitleProperty || change.Property == ActionsProperty)
            UpdateContentLayout();
    }

    private void UpdateContentLayout()
    {
        if (_presenter is null || _bodyPresenter is null)
            return;

        var structured = !string.IsNullOrEmpty(Title) || Actions is not null;
        _presenter.Padding = structured ? new Thickness(16, 12, 16, 0) : new Thickness(16, 4);
        _bodyPresenter.Margin = structured ? new Thickness(0, 0, 0, 16) : default;
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
