using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Theming;
using ExpressiveAvalonia.Tokens;

namespace ExpressiveAvalonia.Catalog;

/// <summary>Gallery for the navigation drawer family: drawer, items, headers, dividers, host and drag handle.</summary>
public static class NavigationDrawerGallery
{
    public static Control Create()
    {
        var panel = new StackPanel { Spacing = 28, Margin = new Thickness(24) };
        panel.Children.Add(Header(
            "Navigation drawer",
            "Single-selection destination list with grouped headers and dividers. The host docks the drawer inline or floats it as a modal overlay with scrim, Escape and focus trapping."));
        panel.Children.Add(DockedDemo());
        panel.Children.Add(ModalDemo());
        panel.Children.Add(ItemStatesDemo());
        panel.Children.Add(DragHandleDemo());
        return new ScrollViewer
        {
            Content = panel,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
    }

    private static Control DockedDemo()
    {
        var status = new TextBlock { Text = "Selected: Inbox" };
        MaterialTypography.SetStyle(status, MaterialTypeStyle.BodyMedium);

        var drawer = CreateMailDrawer();
        drawer.SelectionChanged += (_, e) =>
            status.Text = $"Selected: {Describe(e.AddedItem)} (index {drawer.SelectedIndex})";

        var host = new MaterialNavigationDrawerHost
        {
            Height = 420,
            IsModal = false,
            IsDrawerOpen = true,
            Drawer = drawer,
            Content = Page("Inbox", status),
        };

        return Section(
            "Inline drawer · docked and pushes content",
            "Arrow keys move focus with wrap-around, Home/End jump to the ends, Space or Enter selects. The content is inset by the drawer width.",
            host);
    }

    private static Control ModalDemo()
    {
        var status = new TextBlock { Text = "Drawer closed" };
        MaterialTypography.SetStyle(status, MaterialTypeStyle.BodyMedium);

        var drawer = CreateMailDrawer();
        var host = new MaterialNavigationDrawerHost
        {
            Height = 420,
            IsModal = true,
            Drawer = drawer,
        };

        drawer.SelectionChanged += (_, e) =>
            status.Text = $"Selected: {Describe(e.AddedItem)} — modal closed on selection";
        host.DrawerOpened += (_, _) =>
            status.Text = "Open · Escape, scrim tap or a selection closes it; Tab stays inside";
        host.DrawerClosed += (_, _) => status.Text = "Closed · focus returned to the opener";

        var open = new Button { Content = "Open modal drawer", HorizontalAlignment = HorizontalAlignment.Left };
        open.Click += (_, _) => host.OpenDrawer();

        var dismiss = new Button { Content = "Close with API", HorizontalAlignment = HorizontalAlignment.Left };
        dismiss.Click += (_, _) => host.CloseDrawer();

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            Margin = new Thickness(24, 0, 24, 24),
        };
        actions.Children.Add(open);
        actions.Children.Add(dismiss);

        var content = new DockPanel();
        DockPanel.SetDock(actions, Dock.Bottom);
        content.Children.Add(actions);
        content.Children.Add(Page("Modal drawer host", status));

        host.Content = content;

        return Section(
            "Modal drawer · scrim, light dismiss, Escape, focus trap",
            "Opening remembers focus and moves it to the selected destination; Tab and Shift+Tab cycle inside the drawer; closing restores the previous focus.",
            host);
    }

    private static Control ItemStatesDemo()
    {
        var drawer = new MaterialNavigationDrawer { Height = 470 };
        drawer.Items.Add(new MaterialNavigationSectionHeader { Content = "States" });
        drawer.Items.Add(Item("Selected with badge", Icons.Inbox, "24", selected: true));
        drawer.Items.Add(Item("Hovered or focused here", Icons.Send, "100+"));
        drawer.Items.Add(new MaterialNavigationItem
        {
            Content = "Badge hidden (ShowBadge false)",
            Icon = IconOf(Icons.Favorite),
            BadgeText = "99+",
            ShowBadge = false,
        });
        drawer.Items.Add(new MaterialNavigationItem
        {
            Content = "No icon, longer label that trims when the drawer narrows",
            BadgeText = "New",
        });
        drawer.Items.Add(new MaterialNavigationItem
        {
            Content = "Disabled destination",
            Icon = IconOf(Icons.Delete),
            BadgeText = "12",
            IsEnabled = false,
        });
        drawer.Items.Add(new MaterialNavigationDivider());
        drawer.Items.Add(new MaterialNavigationSectionHeader { Content = "Trailing group" });
        drawer.Items.Add(Item("Third destination", Icons.Folder, "3"));
        drawer.Items.Add(Item("Fourth destination", Icons.Folder, null));

        var note = new TextBlock
        {
            Text = "Source axes covered: Selected True/False, State Enabled/Hovered/Focused/Pressed, Show icon True/False. Disabled is the standard Avalonia state extension.",
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 420,
        };
        MaterialTypography.SetStyle(note, MaterialTypeStyle.BodySmall);

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 24 };
        row.Children.Add(drawer);
        row.Children.Add(note);

        return Section(
            "Item anatomy and states",
            "56 DIP full-radius pills, 24 DIP icon slot, 12 DIP gaps, emphasized label and secondary-container fill when selected.",
            row);
    }

    private static Control DragHandleDemo()
    {
        var transform = new TranslateTransform();
        var status = new TextBlock { Text = "Drag the handle down; release past 96 DIP dismisses the surface." };
        MaterialTypography.SetStyle(status, MaterialTypeStyle.BodyMedium);

        var handle = new MaterialDragHandle();

        var sheetStack = new StackPanel { Spacing = 8 };
        sheetStack.Children.Add(handle);
        sheetStack.Children.Add(new TextBlock { Text = "Navigation surface", FontSize = 20 });
        sheetStack.Children.Add(new TextBlock
        {
            Text = "Building Blocks/navigation handle: 108x4 DIP pill in a 24 DIP grab area.",
            TextWrapping = TextWrapping.Wrap,
        });

        var sheet = new Border
        {
            Child = sheetStack,
            Padding = new Thickness(16, 8, 16, 16),
            CornerRadius = new CornerRadius(28, 28, 0, 0),
            VerticalAlignment = VerticalAlignment.Bottom,
            RenderTransform = transform,
        };
        sheet.Bind(Border.BackgroundProperty, sheet.GetResourceObservable("MaterialSurfaceContainerHighBrush"));

        var restore = new Button
        {
            Content = "Restore surface",
            IsVisible = false,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 12, 0, 0),
        };
        restore.Click += (_, _) =>
        {
            transform.Y = 0;
            sheet.IsVisible = true;
            restore.IsVisible = false;
            status.Text = "Surface restored.";
        };

        handle.DragDelta += (_, e) => transform.Y = Math.Max(0, e.Offset.Y);
        handle.DragCompleted += (_, e) =>
        {
            if (!e.IsCanceled && e.Offset.Y >= 96)
            {
                transform.Y = 0;
                sheet.IsVisible = false;
                restore.IsVisible = true;
                status.Text = $"Dismissed after {e.Offset.Y:0} DIP.";
                return;
            }

            MaterialMotion.Animate(sheet, transform.Y, 0, value => transform.Y = value, MaterialSpring.SpatialDefault);
            status.Text = e.IsCanceled
                ? "Pointer capture lost - surface returned."
                : $"Returned from {e.Offset.Y:0} DIP.";
        };

        var stage = new Panel { Height = 240, ClipToBounds = true };
        stage.Children.Add(sheet);

        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(status);
        stack.Children.Add(restore);
        stack.Children.Add(stage);

        return Section(
            "Drag handle",
            "MaterialDragHandle reports DragStarted, DragDelta and DragCompleted; the surface decides what a drag means. Snap-back uses the Material spring, which collapses under reduced motion.",
            stack);
    }

    private static MaterialNavigationDrawer CreateMailDrawer()
    {
        var drawer = new MaterialNavigationDrawer();

        drawer.Items.Add(new MaterialNavigationSectionHeader { Content = "Mail" });
        drawer.Items.Add(Item("Inbox", Icons.Inbox, "24", selected: true));
        drawer.Items.Add(Item("Outbox", Icons.Send, "100+"));
        drawer.Items.Add(Item("Favorites", Icons.Favorite, "7"));
        drawer.Items.Add(Item("Trash", Icons.Delete, null));
        drawer.Items.Add(new MaterialNavigationDivider());

        drawer.Items.Add(new MaterialNavigationSectionHeader { Content = "Labels" });
        drawer.Items.Add(Item("Label", Icons.Folder, null));
        drawer.Items.Add(Item("Label", Icons.Folder, null));
        drawer.Items.Add(Item("Label", Icons.Folder, null));
        drawer.Items.Add(new MaterialNavigationDivider());

        drawer.Items.Add(new MaterialNavigationSectionHeader { Content = "Labels" });
        drawer.Items.Add(Item("Label", Icons.Folder, null));
        drawer.Items.Add(Item("Label", Icons.Folder, null));
        drawer.Items.Add(Item("Label", Icons.Folder, null));

        return drawer;
    }

    private static MaterialNavigationItem Item(string label, string iconId, string? badge, bool selected = false) => new()
    {
        Content = label,
        Icon = IconOf(iconId),
        BadgeText = badge,
        IsSelected = selected,
    };

    // Icons are the exact glyphs the source drawer instances use (inbox, send, favorite,
    // delete and folder), addressed by their design source ids.
    private static MaterialIcon IconOf(string sourceId) => new()
    {
        SourceId = sourceId,
        Width = 20,
        Height = 20,
        IsDecorative = true,
    };

    private static string Describe(MaterialNavigationItem? item) => item?.Content?.ToString() ?? "none";

    private static Control Header(string title, string description)
    {
        var stack = new StackPanel { Spacing = 6 };
        stack.Children.Add(new TextBlock { Text = title, FontSize = 28, FontWeight = FontWeight.SemiBold });
        var text = new TextBlock { Text = description, TextWrapping = TextWrapping.Wrap, MaxWidth = 720 };
        MaterialTypography.SetStyle(text, MaterialTypeStyle.BodyMedium);
        stack.Children.Add(text);
        return stack;
    }

    private static Control Section(string title, string description, Control content)
    {
        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(new TextBlock { Text = title, FontSize = 20, FontWeight = FontWeight.SemiBold });
        var text = new TextBlock { Text = description, TextWrapping = TextWrapping.Wrap, MaxWidth = 720 };
        MaterialTypography.SetStyle(text, MaterialTypeStyle.BodySmall);
        stack.Children.Add(text);
        stack.Children.Add(content);
        return stack;
    }

    private static Control Page(string heading, TextBlock status)
    {
        var stack = new StackPanel { Spacing = 12, Margin = new Thickness(24) };
        stack.Children.Add(new TextBlock { Text = heading, FontSize = 26 });
        stack.Children.Add(new TextBlock
        {
            Text = "Hosted content. The drawer surface is a separate control; this page never overlaps it while docked.",
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 420,
        });
        stack.Children.Add(status);

        var border = new Border { Child = stack };
        border.Bind(Border.BackgroundProperty, border.GetResourceObservable("MaterialSurfaceContainerLowestBrush"));
        return border;
    }

    private static class Icons
    {
        public const string Inbox = "local-003470";
        public const string Send = "local-004582";
        public const string Favorite = "local-001531";
        public const string Delete = "local-001329";
        public const string Folder = "local-001631";
    }
}
