using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using ExpressiveAvalonia.Controls;

namespace ExpressiveAvalonia.Catalog;

/// <summary>
/// Navigation bar family: horizontal and vertical destinations, vertical bar layout,
/// badges, hidden labels, disabled destinations and keyboard selection.
/// </summary>
public static class NavigationBarGallery
{
    private const string HomeIcon = "M12,3 L2,12 L5,12 L5,21 L10,21 L10,15 L14,15 L14,21 L19,21 L19,12 L22,12 Z";
    private const string SearchIcon = "M15.5,14 L14.71,14 L14.43,13.73 C15.41,12.59 16,11.11 16,9.5 C16,5.91 13.09,3 9.5,3 C5.91,3 3,5.91 3,9.5 C3,13.09 5.91,16 9.5,16 C11.11,16 12.59,15.41 13.73,14.43 L14,14.71 L14,15.5 L19,20.5 L20.5,19 Z M9.5,14 C7.01,14 5,11.99 5,9.5 C5,7.01 7.01,5 9.5,5 C11.99,5 14,7.01 14,9.5 C14,11.99 11.99,14 9.5,14 Z";
    private const string StarOutlineIcon = "M22,9.24 L14.81,8.63 L12,2 L9.19,8.63 L2,9.24 L7.46,13.97 L5.82,21 L12,17.27 L18.18,21 L16.55,13.97 Z M12,15.4 L8.24,17.67 L9.24,13.39 L5.92,10.51 L10.3,10.14 L12,6.1 L13.71,10.14 L18.09,10.51 L14.77,13.39 L15.77,17.67 Z";
    private const string StarFilledIcon = "M12,17.27 L18.18,21 L16.54,13.97 L22,9.24 L14.81,8.62 L12,2 L9.19,8.62 L2,9.24 L7.46,13.97 L5.82,21 Z";
    private const string MailIcon = "M20,4 L4,4 C2.9,4 2.01,4.9 2.01,6 L2,18 C2,19.1 2.9,20 4,20 L20,20 C21.1,20 22,19.1 22,18 L22,6 C22,4.9 21.1,4 20,4 Z M20,8 L12,13 L4,8 L4,6 L12,11 L20,6 Z";
    private const string PersonIcon = "M12,12 C14.21,12 16,10.21 16,8 C16,5.79 14.21,4 12,4 C9.79,4 8,5.79 8,8 C8,10.21 9.79,12 12,12 Z M12,14 C9.33,14 4,15.34 4,18 L4,20 L20,20 L20,18 C20,15.34 14.67,14 12,14 Z";
    private const string FolderIcon = "M4,20 C3.45,20 2.98,19.8 2.59,19.41 C2.2,19.02 2,18.55 2,18 L2,6 C2,5.45 2.2,4.98 2.59,4.59 C2.98,4.2 3.45,4 4,4 L10,4 L12,6 L20,6 C20.55,6 21.02,6.2 21.41,6.59 C21.8,6.98 22,7.45 22,8 L22,18 C22,18.55 21.8,19.02 21.41,19.41 C21.02,19.8 20.55,20 20,20 Z";

    public static Control Create()
    {
        var status = new TextBlock { Text = "Selected: Home", FontSize = 14 };
        var panel = new StackPanel { Margin = new Thickness(24), Spacing = 24 };
        panel.Children.Add(new TextBlock { Text = "Navigation bar", FontSize = 28 });
        panel.Children.Add(new TextBlock
        {
            Text = "Horizontal destinations (icon beside label in a pill) and vertical destinations (icon above label). " +
                   "Arrow keys move the selection with wrapping, Home/End jump to the first or last destination, " +
                   "and Enter/Space invokes it. Badges show counts or a dot. The bar is one tab stop.",
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(status);

        panel.Children.Add(Section("Horizontal items", HorizontalBar(status)));
        panel.Children.Add(Section("Vertical items", VerticalItemsBar(status)));
        panel.Children.Add(Section("Icon-only destinations", IconOnlyBar(status)));
        panel.Children.Add(Section("Vertical bar (extension: bar orientation axis)", SideBar(status)));
        panel.Children.Add(Section("Badges", BadgeRow()));

        return new ScrollViewer { Content = panel, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    private static Control Section(string caption, Control content)
    {
        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(new TextBlock { Text = caption, FontSize = 14, FontWeight = FontWeight.Medium });
        stack.Children.Add(content);
        return stack;
    }

    private static MaterialNavigationBar HorizontalBar(TextBlock status)
    {
        var bar = new MaterialNavigationBar { ItemOrientation = MaterialNavigationBarItemOrientation.Horizontal };
        bar.Items.Add(Item("Home", HomeIcon));
        bar.Items.Add(Item("Search", SearchIcon, badge: MaterialNavigationBarBadge.Small));
        bar.Items.Add(Item("Favorites", StarOutlineIcon, StarFilledIcon, MaterialNavigationBarBadge.Large, "3"));
        bar.Items.Add(Item("Mail", MailIcon, badge: MaterialNavigationBarBadge.Large, badgeText: "99+"));
        var person = Item("Person", PersonIcon);
        person.IsEnabled = false;
        bar.Items.Add(person);
        bar.SelectedIndex = 0;
        Wire(bar, status, "Horizontal");
        return bar;
    }

    private static MaterialNavigationBar VerticalItemsBar(TextBlock status)
    {
        var bar = new MaterialNavigationBar { ItemOrientation = MaterialNavigationBarItemOrientation.Vertical };
        bar.Items.Add(Item("Home", HomeIcon));
        bar.Items.Add(Item("Search", SearchIcon));
        var favorites = Item("Favorites", StarOutlineIcon, StarFilledIcon, MaterialNavigationBarBadge.Large, "3");
        favorites.Command = new DelegateCommand(() => status.Text = "Vertical items: Favorites command executed");
        bar.Items.Add(favorites);
        bar.Items.Add(Item("Mail", MailIcon, badge: MaterialNavigationBarBadge.Small));
        bar.SelectedIndex = 2;
        Wire(bar, status, "Vertical items");
        return bar;
    }

    private static MaterialNavigationBar IconOnlyBar(TextBlock status)
    {
        var bar = new MaterialNavigationBar();
        bar.Items.Add(Item("Home", HomeIcon, showLabel: false));
        bar.Items.Add(Item("Folder", FolderIcon, showLabel: false, badge: MaterialNavigationBarBadge.Large, badgeText: "12"));
        bar.Items.Add(Item("Person", PersonIcon, showLabel: false, badge: MaterialNavigationBarBadge.Small));
        bar.SelectedIndex = 1;
        Wire(bar, status, "Icon-only");
        return bar;
    }

    private static MaterialNavigationBar SideBar(TextBlock status)
    {
        var bar = new MaterialNavigationBar
        {
            Orientation = Orientation.Vertical,
            Width = 220,
            HorizontalAlignment = HorizontalAlignment.Left,
            CornerRadius = new CornerRadius(16)
        };
        bar.Items.Add(Item("Home", HomeIcon));
        bar.Items.Add(Item("Search", SearchIcon));
        bar.Items.Add(Item("Favorites", StarOutlineIcon, StarFilledIcon, MaterialNavigationBarBadge.Large, "3"));
        bar.Items.Add(Item("Person", PersonIcon, badge: MaterialNavigationBarBadge.Small));
        bar.SelectedIndex = 0;
        Wire(bar, status, "Vertical bar");
        return bar;
    }

    private static Control BadgeRow()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 24, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(new MaterialBadge { Size = MaterialBadgeSize.Large, Text = "3", VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(new MaterialBadge { Size = MaterialBadgeSize.Large, Text = "99+", VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(new MaterialBadge { Size = MaterialBadgeSize.Large, VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(new MaterialBadge { Size = MaterialBadgeSize.Small, VerticalAlignment = VerticalAlignment.Center });
        var caption = new TextBlock
        {
            Text = "Large with text, large without text (degrades to a dot), small dot.",
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        };
        var stack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
        stack.Children.Add(row);
        stack.Children.Add(caption);
        return stack;
    }

    private static MaterialNavigationBarItem Item(
        string label,
        string icon,
        string? selectedIcon = null,
        MaterialNavigationBarBadge badge = MaterialNavigationBarBadge.None,
        string? badgeText = null,
        bool showLabel = true)
    {
        var item = new MaterialNavigationBarItem
        {
            Content = label,
            Icon = Geometry.Parse(icon),
            Badge = badge,
            BadgeText = badgeText,
            ShowLabel = showLabel
        };
        if (selectedIcon is not null)
            item.SelectedIcon = Geometry.Parse(selectedIcon);
        return item;
    }

    private static void Wire(MaterialNavigationBar bar, TextBlock status, string name)
    {
        bar.SelectionChanged += (_, _) =>
        {
            if (bar.SelectedItem is MaterialNavigationBarItem item)
                status.Text = $"{name}: selected {item.LabelText} (index {bar.SelectedIndex})";
        };
        bar.ItemInvoked += (_, e) =>
            status.Text = $"{name}: invoked {e.Item.LabelText ?? "item"} (index {e.Index})";
    }

    private sealed class DelegateCommand : ICommand
    {
        private readonly Action _execute;
        public DelegateCommand(Action execute) => _execute = execute;
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute();
    }
}
