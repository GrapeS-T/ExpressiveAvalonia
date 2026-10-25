using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;

namespace ExpressiveAvalonia.Controls;

/// <summary>A rich checkbox row of a <see cref="MaterialDialogList"/>.</summary>
/// <remarks>
/// The source list dialog row anatomy is a 40 unit monogram, an optional overline, headline and
/// supporting line, an optional trailing text, a checkbox and a middle-inset divider. The source
/// instance shows overline and supporting text hidden, so both are optional here; empty fields
/// collapse. Row height and vertical padding follow
/// <see cref="ExpressiveAvalonia.Theming.MaterialList.LineCount"/>
/// (one, two or three lines), the same attached property used by other Material list items.
/// Selection is the checkbox state: the theme binds the checkbox to
/// <see cref="ListBoxItem.IsSelected"/> two-way, and the list toggles it with pointer or Space.
/// </remarks>
public class MaterialDialogListItem : ListBoxItem
{
    /// <summary>Monogram text; the source family uses a single initial letter.</summary>
    public static readonly StyledProperty<string?> InitialProperty =
        AvaloniaProperty.Register<MaterialDialogListItem, string?>(nameof(Initial));

    /// <summary>Optional overline above the headline (label-medium role).</summary>
    public static readonly StyledProperty<string?> OverlineProperty =
        AvaloniaProperty.Register<MaterialDialogListItem, string?>(nameof(Overline));

    /// <summary>The row headline. Falls back to Content when empty.</summary>
    public static readonly StyledProperty<string?> HeadlineProperty =
        AvaloniaProperty.Register<MaterialDialogListItem, string?>(nameof(Headline));

    /// <summary>Optional supporting line below the headline (body-medium role).</summary>
    public static readonly StyledProperty<string?> SupportingTextProperty =
        AvaloniaProperty.Register<MaterialDialogListItem, string?>(nameof(SupportingText));

    /// <summary>Optional metadata before the checkbox, for example a count.</summary>
    public static readonly StyledProperty<string?> TrailingTextProperty =
        AvaloniaProperty.Register<MaterialDialogListItem, string?>(nameof(TrailingText));

    /// <summary>Shows the middle-inset divider at the bottom of the row.</summary>
    public static readonly StyledProperty<bool> ShowDividerProperty =
        AvaloniaProperty.Register<MaterialDialogListItem, bool>(nameof(ShowDivider), true);

    /// <summary>Shows the trailing checkbox. Disable it for navigation-only rows.</summary>
    public static readonly StyledProperty<bool> ShowCheckBoxProperty =
        AvaloniaProperty.Register<MaterialDialogListItem, bool>(nameof(ShowCheckBox), true);

    public MaterialDialogListItem()
    {
        UpdateAutomationName();
    }

    public string? Initial { get => GetValue(InitialProperty); set => SetValue(InitialProperty, value); }

    public string? Overline { get => GetValue(OverlineProperty); set => SetValue(OverlineProperty, value); }

    public string? Headline { get => GetValue(HeadlineProperty); set => SetValue(HeadlineProperty, value); }

    public string? SupportingText { get => GetValue(SupportingTextProperty); set => SetValue(SupportingTextProperty, value); }

    public string? TrailingText { get => GetValue(TrailingTextProperty); set => SetValue(TrailingTextProperty, value); }

    public bool ShowDivider { get => GetValue(ShowDividerProperty); set => SetValue(ShowDividerProperty, value); }

    public bool ShowCheckBox { get => GetValue(ShowCheckBoxProperty); set => SetValue(ShowCheckBoxProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HeadlineProperty || change.Property == ContentProperty)
            UpdateAutomationName();
    }

    private void UpdateAutomationName()
    {
        var name = !string.IsNullOrWhiteSpace(Headline) ? Headline : Content?.ToString();
        if (!string.IsNullOrWhiteSpace(name))
            AutomationProperties.SetName(this, name);
    }
}
