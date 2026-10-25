using System.Windows.Input;
using Avalonia;
using Avalonia.Media;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// A command surfaced by <see cref="MaterialAppBar"/>, <see cref="MaterialBottomAppBar"/> and
/// <see cref="MaterialToolbar"/> as an icon button, and by their overflow menu as an icon and label.
/// </summary>
/// <remarks>
/// Actions are plain data objects, so the same instance can be shown inline and in the overflow
/// flyout without duplicating command state. <see cref="IsEnabled"/> participates in the
/// button's disabled state; <see cref="AutomationName"/> falls back to <see cref="Text"/>.
/// </remarks>
public sealed class MaterialBarAction : AvaloniaObject
{
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<MaterialBarAction, Geometry?>(nameof(Icon));

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<MaterialBarAction, string?>(nameof(Text));

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<MaterialBarAction, ICommand?>(nameof(Command));

    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<MaterialBarAction, object?>(nameof(CommandParameter));

    public static readonly StyledProperty<bool> IsEnabledProperty =
        AvaloniaProperty.Register<MaterialBarAction, bool>(nameof(IsEnabled), true);

    /// <summary>Accessible name. When null, <see cref="Text"/> is announced instead.</summary>
    public static readonly StyledProperty<string?> AutomationNameProperty =
        AvaloniaProperty.Register<MaterialBarAction, string?>(nameof(AutomationName));

    /// <summary>Tooltip content. When null, <see cref="Text"/> is used.</summary>
    public static readonly StyledProperty<object?> ToolTipProperty =
        AvaloniaProperty.Register<MaterialBarAction, object?>(nameof(ToolTip));

    public static readonly DirectProperty<MaterialBarAction, string?> EffectiveAutomationNameProperty =
        AvaloniaProperty.RegisterDirect<MaterialBarAction, string?>(
            nameof(EffectiveAutomationName), action => action.EffectiveAutomationName);

    private string? _effectiveAutomationName;

    public MaterialBarAction()
    {
    }

    public MaterialBarAction(string text, Geometry icon, ICommand? command = null)
    {
        Text = text;
        Icon = icon;
        Command = command;
    }

    /// <summary>Vector icon drawn at 24 device-independent units inside the 48-unit target.</summary>
    public Geometry? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>Short label. Shown in the overflow menu, used as the tooltip and as the
    /// accessible name when <see cref="AutomationName"/> is unset.</summary>
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

    /// <summary>The name announced by assistive technology; never empty when either text field is set.</summary>
    public string? EffectiveAutomationName
    {
        get => _effectiveAutomationName;
        private set => SetAndRaise(EffectiveAutomationNameProperty, ref _effectiveAutomationName, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty || change.Property == AutomationNameProperty)
            EffectiveAutomationName = string.IsNullOrEmpty(AutomationName) ? Text : AutomationName;
    }
}
