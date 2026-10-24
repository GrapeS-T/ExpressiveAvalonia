using Avalonia;
using Avalonia.Controls;

namespace ExpressiveAvalonia.Theming;

/// <summary>Optional Material list anatomy on native Avalonia list item containers.</summary>
/// <remarks>Content and ContentTemplate remain the headline when Headline is not set.
/// Use two lines for a headline and supporting text, or three for two supporting lines.
/// LeadingContent accepts icons, avatars or images; TrailingContent accepts metadata or controls.</remarks>
public sealed class MaterialList : AvaloniaObject
{
    public static readonly AttachedProperty<string?> HeadlineProperty =
        AvaloniaProperty.RegisterAttached<MaterialList, ListBoxItem, string?>("Headline");

    public static readonly AttachedProperty<string?> SupportingTextProperty =
        AvaloniaProperty.RegisterAttached<MaterialList, ListBoxItem, string?>("SupportingText");

    public static readonly AttachedProperty<object?> LeadingContentProperty =
        AvaloniaProperty.RegisterAttached<MaterialList, ListBoxItem, object?>("LeadingContent");

    public static readonly AttachedProperty<object?> TrailingContentProperty =
        AvaloniaProperty.RegisterAttached<MaterialList, ListBoxItem, object?>("TrailingContent");

    /// <summary>The one-, two- or three-line layout, with minimum heights of 56, 72 and 88.</summary>
    public static readonly AttachedProperty<int> LineCountProperty =
        AvaloniaProperty.RegisterAttached<MaterialList, ListBoxItem, int>("LineCount", 1,
            validate: static value => value is >= 1 and <= 3);

    public static string? GetHeadline(ListBoxItem item) => item.GetValue(HeadlineProperty);
    public static void SetHeadline(ListBoxItem item, string? value) => item.SetValue(HeadlineProperty, value);
    public static string? GetSupportingText(ListBoxItem item) => item.GetValue(SupportingTextProperty);
    public static void SetSupportingText(ListBoxItem item, string? value) => item.SetValue(SupportingTextProperty, value);
    public static object? GetLeadingContent(ListBoxItem item) => item.GetValue(LeadingContentProperty);
    public static void SetLeadingContent(ListBoxItem item, object? value) => item.SetValue(LeadingContentProperty, value);
    public static object? GetTrailingContent(ListBoxItem item) => item.GetValue(TrailingContentProperty);
    public static void SetTrailingContent(ListBoxItem item, object? value) => item.SetValue(TrailingContentProperty, value);
    public static int GetLineCount(ListBoxItem item) => item.GetValue(LineCountProperty);
    public static void SetLineCount(ListBoxItem item, int value) => item.SetValue(LineCountProperty, value);
}
