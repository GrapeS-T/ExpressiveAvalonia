using Avalonia;
using Avalonia.Controls.Primitives;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// A Material section header: a fully rounded, low-emphasis label used to open a group of
/// components or settings.
/// </summary>
/// <remarks>
/// The pill is 56 units tall with 16/18 padding and a 14-unit medium label in the
/// on-surface-variant role. Optional leading and trailing content share the 10-unit gap, so an
/// icon, badge or action can sit beside the label without a second layout.
/// </remarks>
public class MaterialSectionHeader : TemplatedControl
{
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<MaterialSectionHeader, string?>(nameof(Label));

    /// <summary>Content before the label, for example a category icon.</summary>
    public static readonly StyledProperty<object?> LeadingContentProperty =
        AvaloniaProperty.Register<MaterialSectionHeader, object?>(nameof(LeadingContent));

    /// <summary>Content after the label, for example a count badge or text action.</summary>
    public static readonly StyledProperty<object?> TrailingContentProperty =
        AvaloniaProperty.Register<MaterialSectionHeader, object?>(nameof(TrailingContent));

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public object? LeadingContent
    {
        get => GetValue(LeadingContentProperty);
        set => SetValue(LeadingContentProperty, value);
    }

    public object? TrailingContent
    {
        get => GetValue(TrailingContentProperty);
        set => SetValue(TrailingContentProperty, value);
    }
}
