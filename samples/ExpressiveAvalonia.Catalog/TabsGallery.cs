using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Catalog;

/// <summary>Shows every tab style, configuration, and header layout of the source family.</summary>
public static class TabsGallery
{
    private static readonly string[] Headers =
    [
        "Overview", "Activity", "Library", "Settings", "Profile", "Explore",
        "Downloads", "Favorites", "History", "Shared", "Trash", "More",
        "Reports", "Insights", "Archive", "Shortcuts"
    ];

    private static readonly Geometry[] Icons =
    [
        Geometry.Parse("M12,3 L4,10 L6,10 L6,20 L18,20 L18,10 L20,10 Z"),
        Geometry.Parse("M12,3 L14.8,8.7 L21,9.6 L16.5,14 L17.6,20.2 L12,17.3 L6.4,20.2 L7.5,14 L3,9.6 L9.2,8.7 Z"),
        Geometry.Parse("M4,4 L20,4 L20,18 L8,18 L4,22 Z M7,8 L17,8 L17,10 L7,10 Z M7,12 L15,12 L15,14 L7,14 Z"),
        Geometry.Parse("M12,8 A4,4 0 1 1 12,16 A4,4 0 1 1 12,8 M12,2 L13.5,5 L17,5.5 L14.5,8 L15.2,11.5 L12,10 L8.8,11.5 L9.5,8 L7,5.5 L10.5,5 Z"),
        Geometry.Parse("M12,4 A4,4 0 1 1 12,12 A4,4 0 1 1 12,4 M4,20 C4,16 8,14 12,14 C16,14 20,16 20,20 Z"),
        Geometry.Parse("M12,2 A10,10 0 1 1 12,22 A10,10 0 1 1 12,2 M11,10 L13,10 L13,17 L11,17 Z M11,7 L13,7 L13,9 L11,9 Z")
    ];

    public static Control Create()
    {
        var panel = new StackPanel { Spacing = 28, Margin = new Thickness(24) };

        panel.Children.Add(Note(
            "Primary and secondary tabs · fixed and scrollable headers · icon, label, and icon+label layouts. " +
            "Arrow keys, Home, and End move the selection; Enter and Space select the focused header. " +
            "Scrollable strips scroll with the wheel, focus, and selection changes."));

        panel.Children.Add(Section(
            "Primary · fixed · label only",
            CreateTabs(MaterialTabVariant.Primary, scrollable: false, MaterialTabLayout.LabelOnly, 3)));

        panel.Children.Add(Section(
            "Primary · fixed · icon and label (64px header, dot and pill badges, disabled tab)",
            CreateTabs(MaterialTabVariant.Primary, scrollable: false, MaterialTabLayout.IconAndLabel, 4,
                badgeIndex: 1, badgeTextIndex: 2, disabledIndex: 3)));

        panel.Children.Add(Section(
            "Primary · fixed · icon only (Header keeps the accessible name)",
            CreateTabs(MaterialTabVariant.Primary, scrollable: false, MaterialTabLayout.IconOnly, 4)));

        panel.Children.Add(Section(
            "Secondary · fixed · label only (full-width 2px indicator and strip divider)",
            CreateTabs(MaterialTabVariant.Secondary, scrollable: false, MaterialTabLayout.LabelOnly, 3)));

        panel.Children.Add(Section(
            "Secondary · fixed · icon and label (horizontal header)",
            CreateTabs(MaterialTabVariant.Secondary, scrollable: false, MaterialTabLayout.IconAndLabel, 4)));

        panel.Children.Add(Section(
            "Primary · scrollable · icon and label (90px headers, 16 items)",
            CreateTabs(MaterialTabVariant.Primary, scrollable: true, MaterialTabLayout.IconAndLabel, 16,
                badgeIndex: 0, badgeTextIndex: 4)));

        panel.Children.Add(Section(
            "Secondary · scrollable · label only (90px headers, 16 items)",
            CreateTabs(MaterialTabVariant.Secondary, scrollable: true, MaterialTabLayout.LabelOnly, 16)));

        return new ScrollViewer
        {
            Content = panel,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
    }

    private static TabControl CreateTabs(
        MaterialTabVariant variant,
        bool scrollable,
        MaterialTabLayout layout,
        int count,
        int badgeIndex = -1,
        int badgeTextIndex = -1,
        int disabledIndex = -1)
    {
        var tabs = new TabControl { MinHeight = 220 };
        MaterialTabs.SetVariant(tabs, variant);
        MaterialTabs.SetIsScrollable(tabs, scrollable);

        for (var index = 0; index < count; index++)
        {
            var header = Headers[index % Headers.Length];
            var item = new TabItem
            {
                Header = header,
                Content = Page(header, index)
            };

            if (layout is MaterialTabLayout.IconOnly or MaterialTabLayout.IconAndLabel)
                item.Icon = new PathIcon { Data = Icons[index % Icons.Length], Width = 24, Height = 24 };

            MaterialTabs.SetLayout(item, layout);
            if (layout == MaterialTabLayout.IconOnly)
                AutomationProperties.SetName(item, header);
            if (index == badgeIndex)
                MaterialTabs.SetShowBadge(item, true);
            if (index == badgeTextIndex)
            {
                MaterialTabs.SetShowBadge(item, true);
                MaterialTabs.SetBadgeText(item, "3");
            }
            if (index == disabledIndex)
                item.IsEnabled = false;

            tabs.Items.Add(item);
        }

        return tabs;
    }

    private static Control Page(string header, int index) =>
        new StackPanel
        {
            Spacing = 8,
            Margin = new Thickness(16),
            Children =
            {
                new TextBlock { Text = $"{header} content", FontSize = 16, FontWeight = FontWeight.Medium },
                new TextBlock
                {
                    Text = "Arrow keys typed here stay in the field; they never move the tab selection.",
                    Opacity = 0.72
                },
                new TextBox
                {
                    Text = $"Page {index + 1}",
                    Width = 240,
                    HorizontalAlignment = HorizontalAlignment.Left
                }
            }
        };

    private static Control Section(string caption, Control content) =>
        new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = caption, FontSize = 16, FontWeight = FontWeight.Medium },
                content
            }
        };

    private static Control Note(string text) =>
        new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Opacity = 0.8 };
}
