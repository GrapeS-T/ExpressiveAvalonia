using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// A Material accordion item: the source "List item - Accordion" header row plus a body.
/// </summary>
/// <remarks>
/// <para>
/// The pinned Material3 package has no <c>Accordion.kt</c>; this is a local composition over the
/// source "List item - Accordion" header row, so it is an extension and not an official port of a
/// non-existent component.
/// </para>
/// <para>
/// Derives from <see cref="ListBoxItem"/> and keeps the list-item style key, so the shared
/// Material list anatomy (leading element, overline, headline, supporting text, trailing
/// elements, densities and state layers) applies unchanged. The trailing accordion button is
/// created by the list item template as soon as <see cref="Body"/> is set, and the body is
/// revealed with the medium-2 duration token (zero under reduced motion).
/// </para>
/// <para>
/// The header is the base content: set <see cref="Headline"/> for the title or use Content
/// directly. <see cref="IsExpanded"/> is two-way and is also available to the surrounding
/// <see cref="MaterialAccordion"/>, which collapses siblings unless it allows multiple.
/// </para>
/// </remarks>
public class MaterialAccordionItem : ListBoxItem
{
    /// <summary>Two-way expanded state of the body.</summary>
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<MaterialAccordionItem, bool>(nameof(IsExpanded),
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The content revealed below the header row.</summary>
    public static readonly StyledProperty<object?> BodyProperty =
        AvaloniaProperty.Register<MaterialAccordionItem, object?>(nameof(Body));

    /// <summary>Headline text; when null, Content is the headline.</summary>
    public static readonly StyledProperty<string?> HeadlineProperty =
        AvaloniaProperty.Register<MaterialAccordionItem, string?>(nameof(Headline));

    /// <summary>Supporting text below the headline.</summary>
    public static readonly StyledProperty<string?> SupportingTextProperty =
        AvaloniaProperty.Register<MaterialAccordionItem, string?>(nameof(SupportingText));

    /// <summary>Overline above the headline.</summary>
    public static readonly StyledProperty<string?> OverlineProperty =
        AvaloniaProperty.Register<MaterialAccordionItem, string?>(nameof(Overline));

    /// <summary>Content before the text block, normally an icon.</summary>
    public static readonly StyledProperty<object?> LeadingContentProperty =
        AvaloniaProperty.Register<MaterialAccordionItem, object?>(nameof(LeadingContent));

    static MaterialAccordionItem()
    {
        IsExpandedProperty.Changed.AddClassHandler<MaterialAccordionItem>((item, change) =>
            MaterialList.SetIsExpanded(item, change.GetNewValue<bool>()));
        // The shared list template toggles the attached property, so mirror it back to the
        // styled property and enforce single-expand from either entry point.
        MaterialList.IsExpandedProperty.Changed.AddClassHandler<MaterialAccordionItem>((item, change) =>
        {
            var expanded = change.GetNewValue<bool>();
            if (item.IsExpanded != expanded)
                item.SetCurrentValue(IsExpandedProperty, expanded);
            if (expanded)
                item.FindAncestorOfType<MaterialAccordion>()?.NotifyExpanded(item);
        });
        BodyProperty.Changed.AddClassHandler<MaterialAccordionItem>((item, change) =>
            MaterialList.SetExpandedContent(item, change.GetNewValue<object?>()));
        MaterialList.ExpandedContentProperty.Changed.AddClassHandler<MaterialAccordionItem>((item, change) =>
        {
            var body = change.GetNewValue<object?>();
            if (!ReferenceEquals(item.Body, body))
                item.SetCurrentValue(BodyProperty, body);
        });
        HeadlineProperty.Changed.AddClassHandler<MaterialAccordionItem>((item, change) =>
            MaterialList.SetHeadline(item, change.GetNewValue<string?>()));
        SupportingTextProperty.Changed.AddClassHandler<MaterialAccordionItem>((item, change) =>
            MaterialList.SetSupportingText(item, change.GetNewValue<string?>()));
        OverlineProperty.Changed.AddClassHandler<MaterialAccordionItem>((item, change) =>
            MaterialList.SetOverline(item, change.GetNewValue<string?>()));
        LeadingContentProperty.Changed.AddClassHandler<MaterialAccordionItem>((item, change) =>
            MaterialList.SetLeadingContent(item, change.GetNewValue<object?>()));
    }

    /// <summary>Reuses the shared ListBoxItem control theme and its Material list anatomy.</summary>
    protected override Type StyleKeyOverride => typeof(ListBoxItem);

    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    public object? Body
    {
        get => GetValue(BodyProperty);
        set => SetValue(BodyProperty, value);
    }

    public string? Headline
    {
        get => GetValue(HeadlineProperty);
        set => SetValue(HeadlineProperty, value);
    }

    public string? SupportingText
    {
        get => GetValue(SupportingTextProperty);
        set => SetValue(SupportingTextProperty, value);
    }

    public string? Overline
    {
        get => GetValue(OverlineProperty);
        set => SetValue(OverlineProperty, value);
    }

    public object? LeadingContent
    {
        get => GetValue(LeadingContentProperty);
        set => SetValue(LeadingContentProperty, value);
    }
}

/// <summary>
/// A Material accordion: a vertical set of <see cref="MaterialAccordionItem"/> headers.
/// </summary>
/// <remarks>
/// <para>
/// Single-expand is the default, matching the source Expandable list: opening one item closes
/// the others. Set <see cref="IsMultipleExpanded"/> to keep several bodies open.
/// </para>
/// <para>
/// Each item is a native list item, so Tab reaches the accordion button and Space/Enter toggles
/// it. Up/Down move focus between headers, Home/End jump to the first and last header, and the
/// header toggle exposes the Expand/Collapse automation name. Items added as strings get a
/// generated <see cref="MaterialAccordionItem"/> container.
/// </para>
/// </remarks>
public class MaterialAccordion : ItemsControl
{
    /// <summary>Keeps several bodies open at once; false enforces single-expand.</summary>
    public static readonly StyledProperty<bool> IsMultipleExpandedProperty =
        AvaloniaProperty.Register<MaterialAccordion, bool>(nameof(IsMultipleExpanded));

    /// <summary>Reuses the ItemsControl control theme (ItemsPresenter host).</summary>
    protected override Type StyleKeyOverride => typeof(ItemsControl);

    public bool IsMultipleExpanded
    {
        get => GetValue(IsMultipleExpandedProperty);
        set => SetValue(IsMultipleExpandedProperty, value);
    }

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey) =>
        NeedsContainer<MaterialAccordionItem>(item, out recycleKey);

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) =>
        new MaterialAccordionItem();

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        // The base implementation already ignores self-container items.
        base.PrepareContainerForItemOverride(container, item, index);
        // Initializer-set expanded items have no ancestor yet, so enforce single-expand once
        // the container is realized.
        if (container is MaterialAccordionItem { IsExpanded: true } accordion)
            Dispatcher.UIThread.Post(() => NotifyExpanded(accordion), DispatcherPriority.Loaded);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (IsMultipleExpanded)
            return;
        // Self-container items skip the generator path; enforce the initial single-expand here.
        Dispatcher.UIThread.Post(() =>
        {
            var expanded = this.GetVisualDescendants().OfType<MaterialAccordionItem>()
                .LastOrDefault(item => item.IsExpanded);
            if (expanded is not null)
                NotifyExpanded(expanded);
        }, DispatcherPriority.Loaded);
    }

    /// <summary>Collapses every other item when single-expand mode is active.</summary>
    internal void NotifyExpanded(MaterialAccordionItem expanded)
    {
        if (IsMultipleExpanded)
            return;

        foreach (var item in this.GetVisualDescendants().OfType<MaterialAccordionItem>())
        {
            if (!ReferenceEquals(item, expanded) && item.IsExpanded)
                item.IsExpanded = false;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key is Key.Up or Key.Down or Key.Home or Key.End)
        {
            var items = this.GetVisualDescendants().OfType<MaterialAccordionItem>().ToList();
            if (items.Count > 0)
            {
                var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual;
                var current = focused as MaterialAccordionItem
                    ?? focused?.FindAncestorOfType<MaterialAccordionItem>();
                if (current is not null || focused is null)
                {
                    var index = current is null ? -1 : items.IndexOf(current);
                    var target = e.Key switch
                    {
                        Key.Home => 0,
                        Key.End => items.Count - 1,
                        Key.Up => Math.Max(0, index - 1),
                        _ => Math.Min(items.Count - 1, index + 1),
                    };
                    FocusHeader(items[target]);
                    e.Handled = true;
                }
            }
        }
        base.OnKeyDown(e);
    }

    private static void FocusHeader(MaterialAccordionItem item)
    {
        var toggle = item.GetVisualDescendants().OfType<ToggleButton>().FirstOrDefault();
        (toggle as Control ?? item).Focus();
    }
}
