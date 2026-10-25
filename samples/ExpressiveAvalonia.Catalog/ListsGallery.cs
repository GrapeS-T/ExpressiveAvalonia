using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Catalog;

/// <summary>
/// Showcase for the Material list family: the three source densities, leading and trailing
/// building blocks, list item states, the segmented and expandable list types, the accordion,
/// swipe rows and pointer reordering. Every element is a real Avalonia control, so pointer,
/// keyboard, automation and reduced-motion behavior are the same as in an application.
/// </summary>
public static class ListsGallery
{
    private static readonly Geometry Person = Geometry.Parse(
        "M12,12 A4,4 0 1 0 12,4 A4,4 0 1 0 12,12 Z M4,21 C4,17 7.6,14.5 12,14.5 C16.4,14.5 20,17 20,21 Z");
    private static readonly Geometry ChevronRight = Geometry.Parse("M9,5 L16,12 L9,19");
    private static readonly Geometry Archive = Geometry.Parse("M3,4 L21,4 L21,8 L3,8 Z M5,9 L19,9 L19,21 L5,21 Z M10,12 L14,12 L14,14 L10,14 Z");
    private static readonly Geometry Edit = Geometry.Parse("M4,16.5 L16.5,4 L20,7.5 L7.5,20 L4,20 Z M14,6.5 L17.5,10");
    private static readonly Geometry Delete = Geometry.Parse("M6,6 L18,6 L17,21 L7,21 Z M4,6 L20,6 M9,3 L15,3 L15,6 L9,6 Z");
    private static readonly Geometry DragHandle = Geometry.Parse("M8,5 L16,5 M8,12 L16,12 M8,19 L16,19");
    private static readonly Geometry More = Geometry.Parse("M12,6 A2,2 0 1 0 12,10 A2,2 0 1 0 12,6 Z M12,16 A2,2 0 1 0 12,20 A2,2 0 1 0 12,16 Z");

    public static Control Create()
    {
        var panel = new StackPanel { Spacing = 28, Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = "Lists", FontSize = 28, FontWeight = FontWeight.Medium });
        panel.Children.Add(Note("Source list, list item, accordion and swipe packages. Densities are MaterialList.Density values 0, -2 and -4; swipe rows accept one to three actions."));
        panel.Children.Add(Section("Densities", Densities()));
        panel.Children.Add(Section("Leading and trailing building blocks", Anatomy()));
        panel.Children.Add(Section("States", States()));
        panel.Children.Add(Section("Segmented (filled) list", Segmented()));
        panel.Children.Add(Section("Expandable list items", Expandable()));
        panel.Children.Add(Section("Accordion", Accordions()));
        panel.Children.Add(Section("Swipe list items", Swipe()));
        panel.Children.Add(Section("Draggable list", Draggable()));
        panel.Children.Add(Section("Headline", Headline()));
        return new ScrollViewer
        {
            Content = panel,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
    }

    private static Control Densities()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 24 };
        foreach (var density in new[] { 0, -2, -4 })
        {
            var list = new ListBox { Width = 380, Padding = new Thickness(0, 4) };
            MaterialList.SetShowDividers(list, true);

            var one = Item("One-line item", leading: Person, trailingText: "100+", trailing: ChevronRight);
            MaterialList.SetDensity(one, density);
            var two = Item("Two-line item", overline: "Overline", supporting: "Supporting line text",
                leading: Person, trailingText: "100+", trailing: ChevronRight, lines: 2);
            MaterialList.SetDensity(two, density);
            var three = Item("Three-line item", overline: "Overline",
                supporting: "Supporting line text that wraps to a second line in the three-line layout.",
                leading: Person, trailing: ChevronRight, lines: 3);
            MaterialList.SetDensity(three, density);

            list.Items.Add(one);
            list.Items.Add(two);
            list.Items.Add(three);
            row.Children.Add(new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    new TextBlock { Text = $"Density {density}", FontWeight = FontWeight.Medium },
                    list,
                },
            });
        }

        return new ScrollViewer
        {
            Content = row,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
    }

    private static Control Anatomy()
    {
        var list = new ListBox { Width = 480, Padding = new Thickness(0, 4) };
        MaterialList.SetShowDividers(list, true);

        list.Items.Add(Item("Leading icon", leading: Person, trailingText: "⌘C", trailing: ChevronRight));

        var indent = Item("Leading indent", leading: Person);
        MaterialList.SetLeadingIndent(indent, 20);
        list.Items.Add(indent);

        var monogram = Item("Monogram", overline: "Overline", supporting: "40 DIP primary-container circle",
            lines: 2);
        MaterialList.SetLeadingMonogramText(monogram, "AL");
        list.Items.Add(monogram);

        var thumbnail = Item("Image thumbnail", supporting: "56 DIP with the selected check", lines: 2);
        MaterialList.SetLeadingImage(thumbnail, Thumbnail());
        MaterialList.SetIsLeadingSelected(thumbnail, true);
        list.Items.Add(thumbnail);

        var avatar = Item("Avatar", supporting: "40 DIP circle", lines: 2);
        MaterialList.SetLeadingAvatar(avatar, Thumbnail());
        list.Items.Add(avatar);

        var video = Item("Video thumbnail", supporting: "114x64 preview", lines: 2);
        MaterialList.SetLeadingVideoThumbnail(video, Thumbnail());
        list.Items.Add(video);

        var topAligned = Item("Top-aligned", overline: "Alignment=Top-aligned",
            supporting: "Content hugs the top edge in this row", lines: 3);
        MaterialList.SetIsTopAligned(topAligned, true);
        MaterialList.SetLeadingContent(topAligned, new PathIcon { Data = Person, Width = 24, Height = 24 });
        list.Items.Add(topAligned);

        var checkbox = Item("Leading checkbox", supporting: "Selection control in the leading slot", lines: 2);
        MaterialList.SetLeadingContent(checkbox, new CheckBox
        {
            IsChecked = true,
            VerticalAlignment = VerticalAlignment.Center,
        });
        list.Items.Add(checkbox);

        var toggle = Item("Leading switch", supporting: "Toggle in the leading slot", lines: 2);
        MaterialList.SetLeadingContent(toggle, new ToggleSwitch
        {
            IsChecked = true,
            VerticalAlignment = VerticalAlignment.Center,
        });
        list.Items.Add(toggle);

        list.Items.Add(Item("Trailing badge", trailingText: "100+", trailing: ChevronRight, badge: "New"));

        var trailingSwitch = Item("Trailing switch", supporting: "The switch keeps its own gesture", lines: 2);
        MaterialList.SetTrailingContent(trailingSwitch, new ToggleSwitch { IsChecked = true });
        list.Items.Add(trailingSwitch);

        var trailingButton = Item("Trailing icon button");
        var iconButton = new Button { Content = new PathIcon { Data = More, Width = 24, Height = 24 } };
        MaterialAssist.SetIsIconButton(iconButton, true);
        MaterialList.SetTrailingContent(trailingButton, iconButton);
        list.Items.Add(trailingButton);

        return list;
    }

    private static Control States()
    {
        var list = new ListBox { Width = 480, Padding = new Thickness(0, 4), SelectionMode = SelectionMode.Single };

        var selected = Item("Selected item", supporting: "Secondary container, 16 DIP radius", lines: 2,
            leading: Person, trailing: ChevronRight);
        list.Items.Add(selected);
        selected.IsSelected = true;

        var disabled = Item("Disabled item", supporting: "38% content", lines: 2, leading: Person);
        disabled.IsEnabled = false;
        list.Items.Add(disabled);

        var dragged = Item("Dragged state preview", supporting: "Raised secondary container, elevation 3", lines: 2,
            leading: Person);
        MaterialList.SetIsDragged(dragged, true);
        list.Items.Add(dragged);

        var stacked = new StackPanel { Spacing = 8 };
        stacked.Children.Add(list);
        stacked.Children.Add(Note("Hover, focus and pressed are live: move the pointer or Tab through the rows."));
        return stacked;
    }

    private static Control Segmented()
    {
        var list = new ListBox { Width = 440, SelectionMode = SelectionMode.Single };
        MaterialList.SetSegmented(list, true);
        list.Items.Add(Item("Inbox", leading: Person));
        list.Items.Add(Item("Starred", leading: Person));
        list.Items.Add(Item("Archive", leading: Person));
        list.SelectedIndex = 0;
        return list;
    }

    private static Control Expandable()
    {
        var list = new ListBox { Width = 480, Padding = new Thickness(0, 4) };
        MaterialList.SetShowDividers(list, true);

        var collapsed = Item("Expandable item", overline: "Accordion", supporting: "Tap the trailing button",
            lines: 2, leading: Person);
        MaterialList.SetExpandedContent(collapsed, Body(
            "The body is revealed with the medium-2 duration token. Under reduced motion the height changes immediately."));
        list.Items.Add(collapsed);

        var expanded = Item("Expanded by default", overline: "Accordion", supporting: "Body visible on load",
            lines: 2, leading: Person);
        MaterialList.SetExpandedContent(expanded, Body(
            "Expanded content is measured and animated from zero height, so the divider and padding stay correct."));
        MaterialList.SetIsExpanded(expanded, true);
        list.Items.Add(expanded);

        return list;
    }

    private static Control Accordions()
    {
        var panel = new StackPanel { Spacing = 12 };

        var single = new MaterialAccordion { Width = 520 };
        single.Items.Add(AccordionItem("Delivery", "Arrives in 2-4 business days", "Shipping",
            "Single-expand is the default: opening one item closes the others. Up/Down move focus between headers."));
        single.Items.Add(AccordionItem("Returns", "30 days, free", "Policy",
            "Each header is a native toggle, so Space and Enter work, and the context menu is not required."));
        single.Items.Add(AccordionItem("Payment", "Visa ending 4242", "Billing",
            "Reduced motion removes the height animation while keeping the same states."));

        var multiple = new MaterialAccordion { Width = 520, IsMultipleExpanded = true };
        multiple.Items.Add(AccordionItem("Notifications", "Enabled", "Preferences",
            "IsMultipleExpanded keeps every opened body visible.", expanded: true));
        multiple.Items.Add(AccordionItem("Privacy", "Manage", "Preferences",
            "Set IsExpanded on any MaterialAccordionItem, including from a binding.", expanded: true));

        panel.Children.Add(new TextBlock { Text = "Single expand (default)", FontWeight = FontWeight.Medium });
        panel.Children.Add(single);
        panel.Children.Add(new TextBlock { Text = "IsMultipleExpanded = true", FontWeight = FontWeight.Medium });
        panel.Children.Add(multiple);
        return panel;
    }

    private static Control Swipe()
    {
        var status = new TextBlock
        {
            Text = "Drag a row horizontally, or focus it and press Left / Right. Every action is also in the context menu.",
            TextWrapping = TextWrapping.Wrap,
        };

        var list = new ListBox { Width = 480, Padding = new Thickness(0, 4) };

        var three = SwipeRow("Three actions", "Primary action expands into a 120 DIP pill", Person);
        three.SwipeActions.Add(Action("Archive", Archive, primary: true));
        three.SwipeActions.Add(Action("Edit", Edit));
        three.SwipeActions.Add(Action("Delete", Delete,
            background: new SolidColorBrush(Color.Parse("#B3261E")), foreground: Brushes.White));
        three.ActionInvoked += (_, e) => status.Text = Describe(e);
        list.Items.Add(three);

        var two = SwipeRow("Two actions", "Peek shows 8 DIP bars, Revealed shows 48 DIP circles", Person);
        two.SwipeActions.Add(Action("Edit", Edit, primary: true));
        two.SwipeActions.Add(Action("Archive", Archive));
        two.ActionInvoked += (_, e) => status.Text = Describe(e);
        list.Items.Add(two);

        var one = SwipeRow("One action", "A single action is primary automatically", Person);
        one.SwipeActions.Add(Action("Delete", Delete,
            background: new SolidColorBrush(Color.Parse("#B3261E")), foreground: Brushes.White));
        one.ActionInvoked += (_, e) => status.Text = Describe(e);
        list.Items.Add(one);

        var revealed = SwipeRow("Revealed state", "IsRevealed = true on load", Person);
        revealed.SwipeActions.Add(Action("Archive", Archive, primary: true));
        revealed.SwipeActions.Add(Action("Edit", Edit));
        revealed.ActionInvoked += (_, e) => status.Text = Describe(e);
        revealed.IsRevealed = true;
        list.Items.Add(revealed);

        var guarded = SwipeRow("Switch inside a swipe row", "Interactive trailing content keeps its gesture", Person);
        guarded.SwipeActions.Add(Action("Archive", Archive, primary: true));
        MaterialList.SetTrailingContent(guarded, new ToggleSwitch { IsChecked = true });
        guarded.ActionInvoked += (_, e) => status.Text = Describe(e);
        list.Items.Add(guarded);

        var segmented = new ListBox { Width = 480, SelectionMode = SelectionMode.Single };
        MaterialList.SetSegmented(segmented, true);
        var segment = SwipeRow("Swipable - segmented", "Segmented container with swipe rows", Person);
        segment.SwipeActions.Add(Action("Archive", Archive, primary: true));
        segment.SwipeActions.Add(Action("Edit", Edit));
        segment.ActionInvoked += (_, e) => status.Text = Describe(e);
        segmented.Items.Add(segment);

        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(status);
        panel.Children.Add(list);
        panel.Children.Add(new TextBlock { Text = "Swipable - segmented", FontWeight = FontWeight.Medium });
        panel.Children.Add(segmented);
        return panel;
    }

    private static Control Draggable()
    {
        var status = new TextBlock { Text = "Drag a row vertically to reorder it." };
        var list = new ListBox { Width = 480, Padding = new Thickness(0, 4) };
        MaterialList.SetAllowItemDrag(list, true);
        MaterialList.SetShowDividers(list, true);

        for (var i = 1; i <= 5; i++)
        {
            var item = Item($"Item {i}", supporting: "Drag to reorder", lines: 2, leading: Person);
            MaterialList.SetTrailingContent(item, new PathIcon { Data = DragHandle, Width = 24, Height = 24 });
            list.Items.Add(item);
        }

        MaterialList.AddItemMovedHandler(list, (_, e) =>
            status.Text = $"Moved index {e.OldIndex} to {e.NewIndex}.");

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(status);
        panel.Children.Add(list);
        return panel;
    }

    private static Control Headline()
    {
        var headline = new ContentControl { Content = "Settings" };
        if (ThemeResource("MaterialHeadline") is { } theme)
            headline.Theme = theme;

        var list = new ListBox { Width = 480, Padding = new Thickness(0, 0, 0, 4) };
        list.Items.Add(Item("Notifications", supporting: "Enabled", lines: 2, leading: Person));
        list.Items.Add(Item("Privacy", supporting: "Manage", lines: 2, leading: Person));
        list.Items.Add(Item("Storage", supporting: "3.2 GB used", lines: 2, leading: Person));

        var panel = new StackPanel();
        panel.Children.Add(headline);
        panel.Children.Add(list);
        return panel;
    }

    private static ListBoxItem Item(string headline, string? overline = null, string? supporting = null,
        Geometry? leading = null, string? trailingText = null, Geometry? trailing = null, int lines = 1,
        string? badge = null)
    {
        var item = new ListBoxItem();
        ConfigureRow(item, headline, overline, supporting, leading, trailingText, trailing, lines, badge);
        return item;
    }

    private static MaterialSwipeListItem SwipeRow(string headline, string? supporting, Geometry? leading)
    {
        var item = new MaterialSwipeListItem();
        ConfigureRow(item, headline, null, supporting, leading, null, null, supporting is null ? 1 : 2, null);
        return item;
    }

    private static void ConfigureRow(ListBoxItem item, string headline, string? overline, string? supporting,
        Geometry? leading, string? trailingText, Geometry? trailing, int lines, string? badge)
    {
        MaterialList.SetHeadline(item, headline);
        MaterialList.SetLineCount(item, lines);
        if (overline is not null)
            MaterialList.SetOverline(item, overline);
        if (supporting is not null)
            MaterialList.SetSupportingText(item, supporting);
        if (leading is not null)
            MaterialList.SetLeadingContent(item, new PathIcon { Data = leading, Width = 24, Height = 24 });
        if (trailingText is not null)
            MaterialList.SetTrailingSupportingText(item, trailingText);
        if (trailing is not null)
            MaterialList.SetTrailingContent(item, new PathIcon { Data = trailing, Width = 24, Height = 24 });
        if (badge is not null)
            MaterialList.SetBadgeText(item, badge);
    }

    private static MaterialAccordionItem AccordionItem(string headline, string supporting, string overline,
        string body, bool expanded = false) => new()
    {
        Headline = headline,
        SupportingText = supporting,
        Overline = overline,
        LeadingContent = new PathIcon { Data = Person, Width = 24, Height = 24 },
        Body = Body(body),
        IsExpanded = expanded,
    };

    private static MaterialSwipeAction Action(string label, Geometry icon, bool primary = false,
        IBrush? background = null, IBrush? foreground = null) => new()
    {
        Label = label,
        Icon = icon,
        IsPrimary = primary,
        Background = background,
        Foreground = foreground,
    };

    private static string Describe(MaterialSwipeActionInvokedEventArgs e) =>
        $"{e.Action.Label} invoked{(e.BySwipe ? " by swipe" : "")} at index {e.Index}.";

    private static Control Body(string text) => new Border
    {
        Padding = new Thickness(0, 4, 0, 8),
        Child = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.Gray,
        },
    };

    private static IImage Thumbnail() => new DrawingImage
    {
        Drawing = new GeometryDrawing
        {
            Brush = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.Parse("#6750A4"), 0),
                    new GradientStop(Color.Parse("#EADDFF"), 1),
                },
            },
            Geometry = Geometry.Parse("M 0,0 L 56,0 L 56,56 L 0,56 Z"),
        },
    };

    private static ControlTheme? ThemeResource(string key)
    {
        // Application lookup walks Styles.Resources, which is where MaterialTheme merges this
        // dictionary, so the same lookup an application XAML would use is exercised here.
        if (Application.Current is { } app && app.TryFindResource(key, out var value) && value is ControlTheme theme)
            return theme;
        return null;
    }

    private static Control Section(string title, Control content) => new StackPanel
    {
        Spacing = 8,
        Children =
        {
            new TextBlock { Text = title, FontSize = 20, FontWeight = FontWeight.Medium },
            content,
        },
    };

    private static Control Note(string text) => new TextBlock
    {
        Text = text,
        TextWrapping = TextWrapping.Wrap,
        MaxWidth = 900,
        Opacity = 0.8,
    };
}
