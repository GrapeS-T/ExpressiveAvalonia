using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Media;

namespace ExpressiveAvalonia.Controls;

/// <summary>The item kinds accepted by <see cref="MaterialBarBase.Actions"/>.</summary>
public enum MaterialBarActionKind
{
    /// <summary>A command rendered as an icon button; the overflow menu shows its label.</summary>
    Clickable,
    /// <summary>A two-state command rendered as an icon toggle button.</summary>
    Toggleable,
    /// <summary>Arbitrary inline and overflow content supplied by the application.</summary>
    Custom
}

/// <summary>
/// State of the overflow menu opened by <see cref="MaterialBarBase.ShowOverflow"/>.
/// </summary>
/// <remarks>
/// Mirrors upstream <c>AppBarMenuState</c>. Custom overflow content receives this instance and can
/// close the menu by calling <see cref="Dismiss"/>.
/// </remarks>
public sealed class MaterialAppBarMenuState : AvaloniaObject
{
    public static readonly DirectProperty<MaterialAppBarMenuState, bool> IsShowingProperty =
        AvaloniaProperty.RegisterDirect<MaterialAppBarMenuState, bool>(
            nameof(IsShowing), state => state.IsShowing);

    private bool _isShowing;

    /// <summary>True while the overflow menu is open.</summary>
    public bool IsShowing
    {
        get => _isShowing;
        private set => SetAndRaise(IsShowingProperty, ref _isShowing, value);
    }

    /// <summary>Opens the menu.</summary>
    public void Show() => IsShowing = true;

    /// <summary>Closes the menu.</summary>
    public void Dismiss() => IsShowing = false;
}

/// <summary>
/// One command or custom item surfaced by <see cref="MaterialAppBar"/>,
/// <see cref="MaterialBottomAppBar"/> and <see cref="MaterialToolbar"/>.
/// </summary>
/// <remarks>
/// <para>
/// Source mapping: <c>clickableItem(onClick, icon, label, enabled)</c> maps to
/// <see cref="MaterialBarActionKind.Clickable"/>; <c>toggleableItem(checked, onCheckedChange, …)</c>
/// maps to <see cref="MaterialBarActionKind.Toggleable"/> with <see cref="IsChecked"/> and
/// <see cref="Command"/> (the command runs after the toggle state changes);
/// <c>customItem(appbarContent, menuContent)</c> maps to
/// <see cref="MaterialBarActionKind.Custom"/> with <see cref="Content"/> and
/// <see cref="MenuContentFactory"/>.
/// </para>
/// <para>
/// The same instance is shown inline and in the overflow flyout, so state such as
/// <see cref="IsEnabled"/> and <see cref="IsChecked"/> is not duplicated. Actions are plain data
/// objects; the bar builds and reuses their controls.
/// </para>
/// <para>
/// Extension (no Compose counterpart): the overflow flyout renders the inline icon next to the
/// label, and <see cref="ToolTip"/> overrides the label tooltip.
/// </para>
/// </remarks>
public sealed class MaterialBarAction : AvaloniaObject
{
    public static readonly StyledProperty<MaterialBarActionKind> KindProperty =
        AvaloniaProperty.Register<MaterialBarAction, MaterialBarActionKind>(
            nameof(Kind), MaterialBarActionKind.Clickable);

    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<MaterialBarAction, Geometry?>(nameof(Icon));

    /// <summary>Label used for the tooltip, overflow item and the default automation name.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<MaterialBarAction, string?>(nameof(Text));

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<MaterialBarAction, ICommand?>(nameof(Command));

    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<MaterialBarAction, object?>(nameof(CommandParameter));

    public static readonly StyledProperty<bool> IsEnabledProperty =
        AvaloniaProperty.Register<MaterialBarAction, bool>(nameof(IsEnabled), true);

    /// <summary>Accessible name; falls back to <see cref="Text"/>.</summary>
    public static readonly StyledProperty<string?> AutomationNameProperty =
        AvaloniaProperty.Register<MaterialBarAction, string?>(nameof(AutomationName));

    /// <summary>Optional tooltip override; defaults to <see cref="Text"/>.</summary>
    public static readonly StyledProperty<object?> ToolTipProperty =
        AvaloniaProperty.Register<MaterialBarAction, object?>(nameof(ToolTip));

    /// <summary>Checked state of a <see cref="MaterialBarActionKind.Toggleable"/> item.</summary>
    public static readonly StyledProperty<bool> IsCheckedProperty =
        AvaloniaProperty.Register<MaterialBarAction, bool>(
            nameof(IsChecked), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>Inline content of a <see cref="MaterialBarActionKind.Custom"/> item.</summary>
    public static readonly StyledProperty<object?> ContentProperty =
        AvaloniaProperty.Register<MaterialBarAction, object?>(nameof(Content));

    /// <summary>Overflow content of a <see cref="MaterialBarActionKind.Custom"/> item.</summary>
    public static readonly StyledProperty<object?> MenuContentProperty =
        AvaloniaProperty.Register<MaterialBarAction, object?>(nameof(MenuContent));

    /// <summary>
    /// Builds <see cref="MenuContent"/> when the overflow opens, so the content can read the
    /// current <see cref="MaterialAppBarMenuState"/> and close the menu. Takes precedence over
    /// <see cref="MenuContent"/>.
    /// </summary>
    public static readonly StyledProperty<Func<MaterialAppBarMenuState, object?>?> MenuContentFactoryProperty =
        AvaloniaProperty.Register<MaterialBarAction, Func<MaterialAppBarMenuState, object?>?>(
            nameof(MenuContentFactory));

    public static readonly DirectProperty<MaterialBarAction, string?> EffectiveAutomationNameProperty =
        AvaloniaProperty.RegisterDirect<MaterialBarAction, string?>(
            nameof(EffectiveAutomationName), action => action.EffectiveAutomationName);

    /// <summary>Creates an empty clickable action; use the initializer or a factory method.</summary>
    public MaterialBarAction()
    {
        PropertyChanged += (_, args) =>
        {
            if (args.Property == TextProperty || args.Property == AutomationNameProperty)
                RaisePropertyChanged(EffectiveAutomationNameProperty,
                    args.GetOldValue<string?>(), EffectiveAutomationName);
        };
    }

    /// <summary>Creates a clickable action from a label, icon and optional command.</summary>
    public MaterialBarAction(string text, Geometry icon, ICommand? command = null)
        : this()
    {
        Text = text;
        Icon = icon;
        Command = command;
    }

    public MaterialBarActionKind Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public Geometry? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public bool IsEnabled
    {
        get => GetValue(IsEnabledProperty);
        set => SetValue(IsEnabledProperty, value);
    }

    public string? AutomationName
    {
        get => GetValue(AutomationNameProperty);
        set => SetValue(AutomationNameProperty, value);
    }

    public object? ToolTip
    {
        get => GetValue(ToolTipProperty);
        set => SetValue(ToolTipProperty, value);
    }

    public bool IsChecked
    {
        get => GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    public object? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    public object? MenuContent
    {
        get => GetValue(MenuContentProperty);
        set => SetValue(MenuContentProperty, value);
    }

    public Func<MaterialAppBarMenuState, object?>? MenuContentFactory
    {
        get => GetValue(MenuContentFactoryProperty);
        set => SetValue(MenuContentFactoryProperty, value);
    }

    public string? EffectiveAutomationName =>
        string.IsNullOrEmpty(AutomationName) ? Text : AutomationName;

    /// <summary>Creates a clickable item.</summary>
    public static MaterialBarAction Clickable(string label, Geometry? icon, ICommand? command,
        bool enabled = true) => new()
    {
        Kind = MaterialBarActionKind.Clickable,
        Text = label,
        Icon = icon,
        Command = command,
        IsEnabled = enabled
    };

    /// <summary>Creates a toggleable item.</summary>
    public static MaterialBarAction Toggleable(string label, Geometry? icon, bool isChecked,
        ICommand? command, bool enabled = true) => new()
    {
        Kind = MaterialBarActionKind.Toggleable,
        Text = label,
        Icon = icon,
        IsChecked = isChecked,
        Command = command,
        IsEnabled = enabled
    };

    /// <summary>Creates a custom item with inline and overflow content.</summary>
    public static MaterialBarAction Custom(object? content, object? menuContent = null) => new()
    {
        Kind = MaterialBarActionKind.Custom,
        Content = content,
        MenuContent = menuContent
    };
}