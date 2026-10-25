using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.Primitives;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// The design-system page header: a logo, an oversized title, an optional guideline link, a
/// definition paragraph, a full-width divider and an optional decorative shape.
/// </summary>
/// <remarks>
/// This is the "header" building block of the design system rather than an app bar: it is a
/// static documentation masthead with a 52-unit inset and a 60-unit rhythm between the logo, the
/// title group and the divider. The link is a real button, so it is reachable with Tab and
/// activated with Enter or Space.
/// </remarks>
public class MaterialPageHeader : TemplatedControl
{
    /// <summary>Logo content hosted in the 40-unit square above the title.</summary>
    public static readonly StyledProperty<object?> LogoProperty =
        AvaloniaProperty.Register<MaterialPageHeader, object?>(nameof(Logo));

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<MaterialPageHeader, string?>(nameof(Title));

    /// <summary>Label of the guideline link. The link is hidden when empty.</summary>
    public static readonly StyledProperty<string?> LinkTextProperty =
        AvaloniaProperty.Register<MaterialPageHeader, string?>(nameof(LinkText));

    public static readonly StyledProperty<ICommand?> LinkCommandProperty =
        AvaloniaProperty.Register<MaterialPageHeader, ICommand?>(nameof(LinkCommand));

    /// <summary>Definition paragraph below the title.</summary>
    public static readonly StyledProperty<string?> DefinitionProperty =
        AvaloniaProperty.Register<MaterialPageHeader, string?>(nameof(Definition));

    public static readonly StyledProperty<bool> ShowDividerProperty =
        AvaloniaProperty.Register<MaterialPageHeader, bool>(nameof(ShowDivider), true);

    /// <summary>Decorative artwork drawn behind the header at 30% opacity.</summary>
    public static readonly StyledProperty<object?> DecorationProperty =
        AvaloniaProperty.Register<MaterialPageHeader, object?>(nameof(Decoration));

    public object? Logo
    {
        get => GetValue(LogoProperty);
        set => SetValue(LogoProperty, value);
    }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? LinkText
    {
        get => GetValue(LinkTextProperty);
        set => SetValue(LinkTextProperty, value);
    }

    public ICommand? LinkCommand
    {
        get => GetValue(LinkCommandProperty);
        set => SetValue(LinkCommandProperty, value);
    }

    public string? Definition
    {
        get => GetValue(DefinitionProperty);
        set => SetValue(DefinitionProperty, value);
    }

    public bool ShowDivider
    {
        get => GetValue(ShowDividerProperty);
        set => SetValue(ShowDividerProperty, value);
    }

    public object? Decoration
    {
        get => GetValue(DecorationProperty);
        set => SetValue(DecorationProperty, value);
    }
}
