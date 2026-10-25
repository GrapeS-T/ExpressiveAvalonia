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
/// XR family gallery: app bars, the surface / surface-high / tertiary-container button building
/// blocks, toolbars, navigation bars, navigation rails, and the modal XR dialog.
/// </summary>
/// <remarks>
/// Every element is the real control: pointer and keyboard activation, single selection,
/// commands, focus rings, disabled handling, reduced-motion transitions, and the native modal
/// dialog lifecycle are exercised rather than drawn. The gallery does not claim spatial runtime
/// support; it demonstrates the desktop interaction model of the XR components.
/// </remarks>
public static class XrGallery
{
    public static Control Create()
    {
        var status = new TextBlock { Text = "XR family ready", FontSize = 14, TextWrapping = TextWrapping.Wrap };
        var panel = new StackPanel { Margin = new Thickness(24), Spacing = 24 };

        panel.Children.Add(new TextBlock { Text = "XR components", FontSize = 28, FontWeight = FontWeight.Medium });
        panel.Children.Add(new TextBlock
        {
            Text = "Desktop interactions: buttons toggle with pointer or Space/Enter, navigation bars and rails "
                 + "use native single selection with arrow keys, and the dialog opens as a modal window that "
                 + "returns its action. No spatial runtime is implied.",
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(status);

        panel.Children.Add(Section("App bars - 3 elevations x 2 configurations", AppBars(status)));
        panel.Children.Add(Section("Surface, surface-high, and tertiary-container buttons", ButtonBlocks(status)));
        panel.Children.Add(Section("Toolbars - 4 elevations x 2 orientations", Toolbars(status)));
        panel.Children.Add(Section("Navigation bars - 3, 4, and 5 segments", NavigationBars(status)));
        panel.Children.Add(Section("Navigation rails - embedded FAB and no FAB", NavigationRails(status)));
        panel.Children.Add(Section("Dialog - modal window lifecycle", DialogSection(status)));

        return new ScrollViewer
        {
            Content = panel,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
    }

    private static Control Section(string caption, Control content)
    {
        var stack = new StackPanel { Spacing = 12 };
        stack.Children.Add(new TextBlock { Text = caption, FontSize = 20, FontWeight = FontWeight.Medium });
        stack.Children.Add(content);
        return stack;
    }

    private static WrapPanel Row(double spacing = 16) => new() { ItemSpacing = spacing, LineSpacing = spacing };

    private static XrIconButton IconButton(Geometry icon, string label, XrSurfaceRole surface = XrSurfaceRole.SurfaceContainer, bool enabled = true) =>
        new()
        {
            Icon = icon,
            Label = label,
            Surface = surface,
            IsEnabled = enabled
        };

    private static Control AppBars(TextBlock status)
    {
        var column = new StackPanel { Spacing = 12 };
        foreach (var elevation in new[] { XrSurfaceRole.SurfaceContainer, XrSurfaceRole.SurfaceContainerHigh, XrSurfaceRole.SurfaceContainerHighest })
        {
            foreach (var configuration in new[] { XrAppBarConfiguration.CenterAligned, XrAppBarConfiguration.Small })
            {
                var leading = IconButton(XrIcons.Menu, "Menu");
                leading.Click += (_, _) => status.Text = $"App bar menu ({elevation}, {configuration})";

                var trailing = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 0 };
                var first = IconButton(XrIcons.FavoriteOutline, "Favorite");
                first.Click += (_, _) => status.Text = "App bar favorite";
                trailing.Children.Add(first);

                if (configuration == XrAppBarConfiguration.Small)
                {
                    var second = IconButton(XrIcons.Search, "Search");
                    second.Click += (_, _) => status.Text = "App bar search";
                    var third = IconButton(XrIcons.Close, "Close");
                    third.Click += (_, _) => status.Text = "App bar close";
                    trailing.Children.Add(second);
                    trailing.Children.Add(third);
                }

                var bar = new XrAppBar
                {
                    Width = 480,
                    Headline = configuration == XrAppBarConfiguration.CenterAligned ? "Center-aligned" : "Small",
                    Configuration = configuration,
                    Elevation = elevation,
                    LeadingContent = leading,
                    TrailingContent = trailing
                };
                column.Children.Add(bar);
            }
        }

        return column;
    }

    private static Control ButtonBlocks(TextBlock status)
    {
        var column = new StackPanel { Spacing = 16 };
        foreach (var surface in new[] { XrSurfaceRole.SurfaceContainer, XrSurfaceRole.SurfaceContainerHigh, XrSurfaceRole.TertiaryContainer })
        {
            var row = Row();
            row.Children.Add(new TextBlock
            {
                Text = surface.ToString(),
                Width = 168,
                VerticalAlignment = VerticalAlignment.Center
            });

            row.Children.Add(IconButton(XrIcons.Favorite, "Favorite", surface));

            var disabled = IconButton(XrIcons.Favorite, "Favorite disabled", surface, enabled: false);
            row.Children.Add(disabled);

            var toggle = new XrToggleIconButton
            {
                Icon = XrIcons.FavoriteOutline,
                SelectedIcon = XrIcons.Favorite,
                Label = "Favorite toggle",
                Surface = surface
            };
            toggle.IsCheckedChanged += (_, _) => status.Text = $"Favorite toggle: {toggle.IsChecked}";
            row.Children.Add(toggle);

            var selected = new XrToggleIconButton
            {
                Icon = XrIcons.StarOutline,
                SelectedIcon = XrIcons.Star,
                Label = "Star selected",
                Surface = surface,
                IsChecked = true
            };
            selected.IsCheckedChanged += (_, _) => status.Text = $"Star toggle: {selected.IsChecked}";
            row.Children.Add(selected);

            var disabledSelected = new XrToggleIconButton
            {
                Icon = XrIcons.StarOutline,
                SelectedIcon = XrIcons.Star,
                Label = "Star disabled",
                Surface = surface,
                IsChecked = true,
                IsEnabled = false
            };
            row.Children.Add(disabledSelected);

            var labeled = new XrToggleButton
            {
                Icon = XrIcons.FavoriteOutline,
                SelectedIcon = XrIcons.Favorite,
                Label = "Favorite",
                Surface = surface
            };
            labeled.IsCheckedChanged += (_, _) => status.Text = $"Labeled toggle: {labeled.IsChecked}";
            row.Children.Add(labeled);

            var labeledSelected = new XrToggleButton
            {
                Icon = XrIcons.Check,
                Label = "Selected",
                Surface = surface,
                IsChecked = true
            };
            row.Children.Add(labeledSelected);

            var iconless = new XrToggleButton
            {
                Label = "No icon",
                ShowIcon = false,
                Surface = surface,
                IsChecked = true
            };
            row.Children.Add(iconless);

            column.Children.Add(row);
        }

        return column;
    }

    private static XrToggleIconButton Slot(XrSurfaceRole surface, Geometry icon, string label, bool selected)
    {
        var slot = new XrToggleIconButton
        {
            Icon = icon,
            Label = label,
            Surface = surface,
            IsChecked = selected
        };
        return slot;
    }

    private static XrToolbar Toolbar(XrSurfaceRole elevation, Orientation orientation, TextBlock status)
    {
        var itemSurface = elevation switch
        {
            XrSurfaceRole.TertiaryContainer => XrSurfaceRole.TertiaryContainer,
            XrSurfaceRole.SurfaceContainerHigh => XrSurfaceRole.SurfaceContainerHigh,
            XrSurfaceRole.SurfaceContainerHighest => XrSurfaceRole.SurfaceContainerHigh,
            _ => XrSurfaceRole.SurfaceContainer
        };

        var toolbar = new XrToolbar
        {
            Orientation = orientation,
            Elevation = elevation,
            FabIcon = XrIcons.Add,
            FabLabel = "Add",
            FabCommand = new DelegateCommand(() => status.Text = $"Toolbar FAB ({elevation}, {orientation})")
        };

        toolbar.Items.Add(Slot(itemSurface, XrIcons.Favorite, "Favorite", true));
        toolbar.Items.Add(Slot(itemSurface, XrIcons.Search, "Search", false));
        toolbar.Items.Add(Slot(itemSurface, XrIcons.Home, "Home", false));
        toolbar.Items.Add(Slot(itemSurface, XrIcons.Star, "Star", false));
        toolbar.Items.Add(Slot(itemSurface, XrIcons.Chat, "Chat", false));
        toolbar.Items.Add(Slot(itemSurface, XrIcons.Mail, "Mail", false));
        return toolbar;
    }

    private static Control Toolbars(TextBlock status)
    {
        var column = new StackPanel { Spacing = 16 };
        foreach (var elevation in new[] { XrSurfaceRole.SurfaceContainer, XrSurfaceRole.SurfaceContainerHigh, XrSurfaceRole.SurfaceContainerHighest, XrSurfaceRole.TertiaryContainer })
        {
            var row = Row();
            row.Children.Add(new TextBlock
            {
                Text = elevation.ToString(),
                Width = 168,
                VerticalAlignment = VerticalAlignment.Center
            });
            row.Children.Add(Toolbar(elevation, Orientation.Horizontal, status));
            row.Children.Add(Toolbar(elevation, Orientation.Vertical, status));
            column.Children.Add(row);
        }

        return column;
    }

    private static XrNavigationBarItem BarItem(Geometry icon, string label, XrBadgeKind badge, string? badgeText) =>
        new()
        {
            Icon = icon,
            SelectedIcon = icon,
            Label = label,
            Badge = badge,
            BadgeText = badgeText
        };

    private static Control NavigationBars(TextBlock status)
    {
        var column = new StackPanel { Spacing = 16 };
        var three = new XrNavigationBarItem[]
        {
            BarItem(XrIcons.Explore, "Explore", XrBadgeKind.None, null),
            BarItem(XrIcons.Bookmark, "Saved", XrBadgeKind.Large, "3"),
            BarItem(XrIcons.History, "Updates", XrBadgeKind.Small, null)
        };
        var four = new XrNavigationBarItem[]
        {
            BarItem(XrIcons.Explore, "Explore", XrBadgeKind.None, null),
            BarItem(XrIcons.Home, "Home", XrBadgeKind.Small, null),
            BarItem(XrIcons.Bookmark, "Saved", XrBadgeKind.Large, "12"),
            BarItem(XrIcons.History, "Updates", XrBadgeKind.None, null)
        };
        var five = new XrNavigationBarItem[]
        {
            BarItem(XrIcons.Explore, "Explore", XrBadgeKind.None, null),
            BarItem(XrIcons.Home, "Home", XrBadgeKind.None, null),
            BarItem(XrIcons.Bookmark, "Saved", XrBadgeKind.Large, "3"),
            BarItem(XrIcons.Chat, "Contribute", XrBadgeKind.Small, null),
            BarItem(XrIcons.History, "Updates", XrBadgeKind.None, null)
        };
        var definitions = new (string Caption, XrNavigationBarItem[] Items)[]
        {
            ("3 segments", three),
            ("4 segments", four),
            ("5 segments", five)
        };

        foreach (var (caption, items) in definitions)
        {
            var row = Row();
            row.Children.Add(new TextBlock { Text = caption, Width = 110, VerticalAlignment = VerticalAlignment.Center });
            var bar = new XrNavigationBar();
            foreach (var item in items)
                bar.Items.Add(item);
            bar.SelectedIndex = 0;
            bar.SelectionChanged += (_, _) =>
            {
                if (bar.SelectedItem is XrNavigationBarItem selected)
                    status.Text = $"Navigation bar: {selected.Label} (index {bar.SelectedIndex})";
            };
            row.Children.Add(bar);
            column.Children.Add(row);
        }

        return column;
    }

    private static XrNavigationRailItem RailItem(Geometry icon, string label, bool showLabel, XrBadgeKind badge, string? badgeText) =>
        new()
        {
            Icon = icon,
            SelectedIcon = icon,
            Label = label,
            ShowLabel = showLabel,
            Badge = badge,
            BadgeText = badgeText
        };

    private static Control NavigationRails(TextBlock status)
    {
        var row = Row(32);
        row.Children.Add(Rail(status, "Embedded FAB", showFab: true));
        row.Children.Add(Rail(status, "No FAB", showFab: false));
        row.Children.Add(Rail(status, "Labels + FAB", showFab: true, showLabels: true));
        return row;
    }

    private static Control Rail(TextBlock status, string caption, bool showFab, bool showLabels = false)
    {
        var rail = new XrNavigationRail
        {
            ShowFab = showFab,
            FabIcon = XrIcons.Add,
            FabLabel = "Compose",
            MenuCommand = new DelegateCommand(() => status.Text = "Rail menu"),
            FabCommand = new DelegateCommand(() => status.Text = "Rail FAB")
        };
        rail.Items.Add(RailItem(XrIcons.Mail, "Mail", showLabels, XrBadgeKind.Large, "3"));
        rail.Items.Add(RailItem(XrIcons.Chat, "Chat", showLabels, XrBadgeKind.None, null));
        rail.Items.Add(RailItem(XrIcons.Explore, "Spaces", showLabels, XrBadgeKind.Small, null));
        rail.Items.Add(RailItem(XrIcons.History, "Meet", showLabels, XrBadgeKind.None, null));
        rail.Items.Add(RailItem(XrIcons.Home, "Home", showLabels, XrBadgeKind.None, null));
        rail.Items.Add(RailItem(XrIcons.Star, "Starred", showLabels, XrBadgeKind.None, null));
        rail.Items.Add(RailItem(XrIcons.Bookmark, "Saved", showLabels, XrBadgeKind.None, null));
        rail.SelectedIndex = 0;
        rail.SelectionChanged += (_, _) =>
        {
            if (rail.SelectedItem is XrNavigationRailItem selected)
                status.Text = $"Rail: {selected.Label} (index {rail.SelectedIndex})";
        };

        var column = new StackPanel { Spacing = 8 };
        column.Children.Add(new TextBlock { Text = caption, FontSize = 14, FontWeight = FontWeight.Medium });
        column.Children.Add(rail);
        return column;
    }

    private static Control DialogSection(TextBlock status)
    {
        var row = Row();

        var standard = new Button { Content = "Open dialog" };
        standard.Click += async (_, _) => await OpenDialog(standard, status, highest: false, divider: true);
        row.Children.Add(standard);

        var highest = new Button { Content = "Highest, no icon, one action" };
        highest.Click += async (_, _) => await OpenDialog(highest, status, highest: true, divider: false);
        row.Children.Add(highest);

        return row;
    }

    private static async System.Threading.Tasks.Task OpenDialog(Control source, TextBlock status, bool highest, bool divider)
    {
        if (TopLevel.GetTopLevel(source) is not Window owner)
            return;

        var dialog = new XrDialog
        {
            Headline = "Basic dialog title",
            SupportingText = "A dialog is a type of modal window that appears in front of app content to provide "
                           + "critical information, or prompt for a decision to be made.",
            ContentIcon = XrIcons.FavoriteOutline,
            ShowIcon = !highest,
            ShowDivider = divider,
            ShowSecondaryAction = !highest,
            Elevation = highest ? XrSurfaceRole.SurfaceContainerHighest : XrSurfaceRole.SurfaceContainerHigh,
            PrimaryActionText = "Action 1",
            SecondaryActionText = "Action 2"
        };
        dialog.PrimaryInvoked += (_, _) => status.Text = "Dialog: primary invoked";
        dialog.SecondaryInvoked += (_, _) => status.Text = "Dialog: secondary invoked";

        var result = await dialog.ShowDialogAsync(owner);
        status.Text = $"Dialog result: {result}";
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
