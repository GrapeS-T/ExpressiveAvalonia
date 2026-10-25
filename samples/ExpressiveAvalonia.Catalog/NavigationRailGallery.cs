using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using ExpressiveAvalonia.Controls;

namespace ExpressiveAvalonia.Catalog;

/// <summary>Navigation rail family: compact/expanded, docked/floating, top/middle, badges and keyboard selection.</summary>
public static class NavigationRailGallery
{
    private const string HomeIcon = "M12,3 L2,12 L5,12 L5,21 L10,21 L10,15 L14,15 L14,21 L19,21 L19,12 L22,12 Z";
    private const string SearchIcon = "M15.5,14 L14.71,14 L14.43,13.73 C15.41,12.59 16,11.11 16,9.5 C16,5.91 13.09,3 9.5,3 C5.91,3 3,5.91 3,9.5 C3,13.09 5.91,16 9.5,16 C11.11,16 12.59,15.41 13.73,14.43 L14,14.71 L14,15.5 L19,20.5 L20.5,19 Z M9.5,14 C7.01,14 5,11.99 5,9.5 C5,7.01 7.01,5 9.5,5 C11.99,5 14,7.01 14,9.5 C14,11.99 11.99,14 9.5,14 Z";
    private const string StarOutlineIcon = "M22,9.24 L14.81,8.63 L12,2 L9.19,8.63 L2,9.24 L7.46,13.97 L5.82,21 L12,17.27 L18.18,21 L16.55,13.97 Z M12,15.4 L8.24,17.67 L9.24,13.39 L5.92,10.51 L10.3,10.14 L12,6.1 L13.71,10.14 L18.09,10.51 L14.77,13.39 L15.77,17.67 Z";
    private const string StarFilledIcon = "M12,17.27 L18.18,21 L16.54,13.97 L22,9.24 L14.81,8.62 L12,2 L9.19,8.62 L2,9.24 L7.46,13.97 L5.82,21 Z";
    private const string SettingsIcon = "M19.14,12.94 C19.18,12.64 19.2,12.33 19.2,12 C19.2,11.68 19.18,11.36 19.13,11.06 L21.16,9.48 C21.34,9.34 21.39,9.07 21.28,8.87 L19.36,5.55 C19.24,5.33 18.99,5.26 18.77,5.33 L16.38,6.29 C15.88,5.91 15.35,5.59 14.76,5.35 L14.4,2.81 C14.36,2.57 14.16,2.4 13.92,2.4 L10.08,2.4 C9.84,2.4 9.65,2.57 9.61,2.81 L9.25,5.35 C8.66,5.59 8.12,5.92 7.63,6.29 L5.24,5.33 C5.02,5.25 4.77,5.33 4.65,5.55 L2.74,8.87 C2.62,9.08 2.66,9.34 2.86,9.48 L4.89,11.06 C4.84,11.36 4.8,11.69 4.8,12 C4.8,12.31 4.82,12.64 4.87,12.94 L2.84,14.52 C2.66,14.66 2.61,14.93 2.72,15.13 L4.64,18.45 C4.76,18.67 5.01,18.74 5.23,18.67 L7.62,17.71 C8.12,18.09 8.65,18.41 9.24,18.65 L9.6,21.19 C9.65,21.43 9.84,21.6 10.08,21.6 L13.92,21.6 C14.16,21.6 14.35,21.43 14.39,21.19 L14.75,18.65 C15.34,18.41 15.88,18.09 16.37,17.71 L18.76,18.67 C18.98,18.75 19.23,18.67 19.35,18.45 L21.27,15.13 C21.39,14.91 21.34,14.66 21.14,14.52 Z M12,15.6 C10.02,15.6 8.4,13.98 8.4,12 C8.4,10.02 10.02,8.4 12,8.4 C13.98,8.4 15.6,10.02 15.6,12 C15.6,13.98 13.98,15.6 12,15.6 Z";
    private const string MailIcon = "M20,4 L4,4 C2.9,4 2.01,4.9 2.01,6 L2,18 C2,19.1 2.9,20 4,20 L20,20 C21.1,20 22,19.1 22,18 L22,6 C22,4.9 21.1,4 20,4 Z M20,8 L12,13 L4,8 L4,6 L12,11 L20,6 Z";
    private const string PersonIcon = "M12,12 C14.21,12 16,10.21 16,8 C16,5.79 14.21,4 12,4 C9.79,4 8,5.79 8,8 C8,10.21 9.79,12 12,12 Z M12,14 C9.33,14 4,15.34 4,18 L4,20 L20,20 L20,18 C20,15.34 14.67,14 12,14 Z";
    private const string PlusIcon = "M11,5 L13,5 L13,11 L19,11 L19,13 L13,13 L13,19 L11,19 L11,13 L5,13 L5,11 L11,11 Z";
    private const string ArchiveIcon = "M20.54,5.23 L19.15,3.55 C18.88,3.21 18.47,3 18,3 L6,3 C5.53,3 5.12,3.21 4.84,3.55 L3.46,5.23 C3.17,5.57 3,6.02 3,6.5 L3,19 C3,20.1 3.9,21 5,21 L19,21 C20.1,21 21,20.1 21,19 L21,6.5 C21,6.02 20.83,5.57 20.54,5.23 Z M12,17.5 L6.5,12 L10,12 L10,10 L14,10 L14,12 L17.5,12 L12,17.5 Z M5.12,5 L5.93,4 L17.93,4 L18.87,5 L5.12,5 Z";

    public static Control Create()
    {
        var status = new TextBlock { Text = "Selected: Home", FontSize = 14 };
        var panel = new StackPanel { Margin = new Thickness(24), Spacing = 20 };
        panel.Children.Add(new TextBlock { Text = "Navigation rail", FontSize = 28 });
        panel.Children.Add(new TextBlock
        {
            Text = "Compact and expanded rails. Arrow keys move selection (wrapping), Home/End jump, Enter/Space invoke. Menu and FAB are native buttons.",
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(status);

        var row = new WrapPanel { ItemSpacing = 32, LineSpacing = 32 };
        row.Children.Add(Card("Compact · top · badges", CompactTop(status)));
        row.Children.Add(Card("Compact · middle · icon only", CompactMiddle(status)));
        row.Children.Add(Card("Expanded · docked", ExpandedDocked(status)));
        row.Children.Add(Card("Expanded · floating · middle", ExpandedFloating(status)));
        panel.Children.Add(row);
        panel.Children.Add(HostDemo(status));

        return new ScrollViewer { Content = panel, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    /// <summary>
    /// Modal expanded rail host: open/close buttons for the docked and floating placements,
    /// scrim, Escape, light dismiss, focus trap and focus restore are exercised here.
    /// </summary>
    private static Control HostDemo(TextBlock status)
    {
        var section = new StackPanel { Spacing = 12 };
        section.Children.Add(new TextBlock { Text = "Expanded rail host (modal)", FontSize = 20 });
        section.Children.Add(new TextBlock
        {
            Text = "The source expanded rail is modal: it opens over a scrim, traps Tab, closes on Escape or a scrim press, and restores focus to the opener. Choosing a destination also closes it.",
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap
        });

        var openDocked = new Button { Content = "Open docked rail" };
        var openFloating = new Button { Content = "Open floating rail" };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        buttons.Children.Add(openDocked);
        buttons.Children.Add(openFloating);

        var body = new Border
        {
            Background = Avalonia.Media.Brushes.Transparent,
            Padding = new Thickness(24),
            MinHeight = 220,
            Child = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = "Content behind the rail", FontSize = 16 },
                    new TextBlock { Text = "Press Escape or click the scrim to dismiss the open rail.", FontSize = 14 }
                }
            }
        };

        var dockedRail = ExpandedDocked(status);
        dockedRail.Height = double.NaN;
        dockedRail.VerticalAlignment = VerticalAlignment.Stretch;
        dockedRail.IsExpanded = true;

        var floatingRail = ExpandedFloating(status);
        floatingRail.Height = double.NaN;
        floatingRail.VerticalAlignment = VerticalAlignment.Stretch;
        floatingRail.IsExpanded = true;

        var host = new MaterialNavigationRailHost
        {
            Content = body,
            Rail = dockedRail,
            Placement = MaterialNavigationRailHostPlacement.Docked,
            IsModal = true,
            Height = 420,
            BorderBrush = Avalonia.Media.Brushes.Gray,
            BorderThickness = new Thickness(1)
        };

        openDocked.Click += (_, _) =>
        {
            host.Rail = dockedRail;
            host.Placement = MaterialNavigationRailHostPlacement.Docked;
            host.OpenRail();
            status.Text = "Docked modal rail opened: Tab traps, Escape/scrim closes";
        };
        openFloating.Click += (_, _) =>
        {
            host.Rail = floatingRail;
            host.Placement = MaterialNavigationRailHostPlacement.Floating;
            host.OpenRail();
            status.Text = "Floating modal rail opened: Tab traps, Escape/scrim closes";
        };
        host.RailClosed += (_, _) => status.Text = "Rail closed; focus restored to the opener";

        section.Children.Add(buttons);
        section.Children.Add(host);
        return section;
    }

    private static Control Card(string caption, Control rail)
    {
        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(new TextBlock { Text = caption, FontSize = 14, FontWeight = FontWeight.Medium });
        stack.Children.Add(rail);
        return stack;
    }

    private static MaterialNavigationRail CompactTop(TextBlock status)
    {
        var rail = new MaterialNavigationRail
        {
            Height = 480,
            VerticalAlignment = VerticalAlignment.Top,
            FabIcon = Geometry.Parse(PlusIcon),
            MenuCommand = new DelegateCommand(() => status.Text = "Menu invoked"),
            FabCommand = new DelegateCommand(() => status.Text = "FAB invoked")
        };
        rail.Items.Add(Item("Home", HomeIcon));
        rail.Items.Add(Item("Search", SearchIcon));
        rail.Items.Add(Item("Favorites", StarOutlineIcon, StarFilledIcon, MaterialNavigationRailBadge.Large, "3"));
        rail.Items.Add(Item("Settings", SettingsIcon, badge: MaterialNavigationRailBadge.Small));
        rail.SelectedIndex = 0;
        Wire(rail, status);
        return rail;
    }

    private static MaterialNavigationRail CompactMiddle(TextBlock status)
    {
        var rail = new MaterialNavigationRail
        {
            Height = 600,
            RailAlignment = MaterialNavigationRailAlignment.Middle,
            ShowMenuButton = false,
            ShowFab = false,
            VerticalAlignment = VerticalAlignment.Top
        };
        rail.Items.Add(Item("Home", HomeIcon, showLabel: false));
        rail.Items.Add(Item("Mail", MailIcon, showLabel: false, badge: MaterialNavigationRailBadge.Large, badgeText: "12"));
        rail.Items.Add(Item("Person", PersonIcon, showLabel: false));
        rail.SelectedIndex = 1;
        Wire(rail, status);
        return rail;
    }

    private static MaterialNavigationRail ExpandedDocked(TextBlock status)
    {
        var rail = new MaterialNavigationRailExpanded
        {
            Height = 480,
            VerticalAlignment = VerticalAlignment.Top,
            FabIcon = Geometry.Parse(PlusIcon),
            FabText = "New",
            MenuCommand = new DelegateCommand(() => status.Text = "Menu invoked"),
            FabCommand = new DelegateCommand(() => status.Text = "New invoked")
        };
        rail.Items.Add(Item("Home", HomeIcon));
        rail.Items.Add(Item("Search", SearchIcon));
        rail.Items.Add(Item("Favorites", StarOutlineIcon, StarFilledIcon, MaterialNavigationRailBadge.Large, "3"));
        rail.Items.Add(Item("Settings", SettingsIcon));
        var archive = Item("Archive", ArchiveIcon);
        archive.IsEnabled = false;
        rail.Items.Add(archive);
        rail.SelectedIndex = 0;
        Wire(rail, status);
        return rail;
    }

    private static MaterialNavigationRail ExpandedFloating(TextBlock status)
    {
        var rail = new MaterialNavigationRailExpanded
        {
            Height = 600,
            IsFloating = true,
            RailAlignment = MaterialNavigationRailAlignment.Middle,
            ShowFab = false,
            VerticalAlignment = VerticalAlignment.Top,
            MenuCommand = new DelegateCommand(() => status.Text = "Menu invoked")
        };
        rail.Items.Add(Item("Home", HomeIcon));
        rail.Items.Add(Item("Mail", MailIcon, badge: MaterialNavigationRailBadge.Small));
        rail.Items.Add(Item("Person", PersonIcon));
        rail.SelectedIndex = 2;
        Wire(rail, status);
        return rail;
    }

    private static MaterialNavigationRailItem Item(
        string label,
        string icon,
        string? selectedIcon = null,
        MaterialNavigationRailBadge badge = MaterialNavigationRailBadge.None,
        string? badgeText = null,
        bool showLabel = true)
    {
        var item = new MaterialNavigationRailItem
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

    private static void Wire(MaterialNavigationRail rail, TextBlock status)
    {
        rail.SelectionChanged += (_, _) =>
        {
            if (rail.SelectedItem is MaterialNavigationRailItem item)
                status.Text = $"Selected: {item.Content}";
        };
        rail.ItemInvoked += (_, e) => status.Text = $"Invoked: {e.Item.Content} (index {e.Index})";
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
