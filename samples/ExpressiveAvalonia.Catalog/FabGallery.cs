using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using ExpressiveAvalonia.Controls;

namespace ExpressiveAvalonia.Catalog;

/// <summary>
/// Gallery for the FAB family: regular and extended FABs in every source size and color role,
/// a live extended FAB collapse demo, and three FAB menus with commands.
/// </summary>
public static class FabGallery
{
    private const string PlusIcon = "M11,5 L13,5 L13,11 L19,11 L19,13 L13,13 L13,19 L11,19 L11,13 L5,13 L5,11 L11,11 Z";
    private const string EditIcon = "M3,17.25 L3,21 L6.75,21 L17.81,9.94 L14.06,6.19 L3,17.25 Z M20.71,7.04 C21.1,6.65 21.1,6.02 20.71,5.63 L18.37,3.29 C17.98,2.9 17.35,2.9 16.96,3.29 L15.13,5.12 L18.88,8.87 L20.71,7.04 Z";
    private const string ShareIcon = "M18,16.08 C17.24,16.08 16.56,16.38 16.04,16.85 L8.91,12.7 C8.96,12.47 9,12.24 9,12 C9,11.76 8.96,11.53 8.91,11.3 L15.96,7.19 C16.5,7.69 17.21,8 18,8 C19.66,8 21,6.66 21,5 C21,3.34 19.66,2 18,2 C16.34,2 15,3.34 15,5 C15,5.24 15.04,5.47 15.09,5.7 L8.04,9.81 C7.5,9.31 6.79,9 6,9 C4.34,9 3,10.34 3,12 C3,13.66 4.34,15 6,15 C6.79,15 7.5,14.69 8.04,14.19 L15.16,18.35 C15.11,18.56 15.08,18.78 15.08,19 C15.08,20.61 16.39,21.92 18,21.92 C19.61,21.92 20.92,20.61 20.92,19 C20.92,17.39 19.61,16.08 18,16.08 Z";
    private const string DeleteIcon = "M6,19 C6,20.1 6.9,21 8,21 L16,21 C17.1,21 18,20.1 18,19 L18,7 L6,7 L6,19 Z M19,4 L15.5,4 L14.5,3 L9.5,3 L8.5,4 L5,4 L5,6 L19,6 L19,4 Z";
    private const string FavoriteIcon = "M12,21.35 L10.55,20.03 C5.4,15.36 2,12.28 2,8.5 C2,5.42 4.42,3 7.5,3 C9.24,3 10.91,3.81 12,5.09 C13.09,3.81 14.76,3 16.5,3 C19.58,3 22,5.42 22,8.5 C22,12.28 18.6,15.36 13.45,20.04 L12,21.35 Z";
    private const string CameraIcon = "M12,15.2 A3.2,3.2 0 1 0 12,8.8 A3.2,3.2 0 1 0 12,15.2 Z M9,2 L7.17,4 L4,4 C2.9,4 2,4.9 2,6 L2,18 C2,19.1 2.9,20 4,20 L20,20 C21.1,20 22,19.1 22,18 L22,6 C22,4.9 21.1,4 20,4 L16.83,4 L15,2 L9,2 Z";

    private static readonly Geometry Plus = Geometry.Parse(PlusIcon);
    private static readonly Geometry Edit = Geometry.Parse(EditIcon);
    private static readonly Geometry Share = Geometry.Parse(ShareIcon);
    private static readonly Geometry Delete = Geometry.Parse(DeleteIcon);
    private static readonly Geometry Favorite = Geometry.Parse(FavoriteIcon);
    private static readonly Geometry Camera = Geometry.Parse(CameraIcon);

    /// <summary>Creates the FAB gallery.</summary>
    public static Control Create()
    {
        var status = new TextBlock { Text = "No action yet.", TextWrapping = TextWrapping.Wrap };
        var panel = new StackPanel { Margin = new Thickness(24), Spacing = 20 };

        panel.Children.Add(new TextBlock
        {
            Text = "FAB family",
            FontSize = 32,
            FontWeight = FontWeight.Bold
        });
        panel.Children.Add(new TextBlock
        {
            Text = "Every source size and color role. Tab to a FAB and press Enter or Space; " +
                   "the state layer and focus ring follow hover, focus and press.",
            TextWrapping = TextWrapping.Wrap
        });

        panel.Children.Add(Heading("FAB menu — primary / secondary / tertiary containers"));
        panel.Children.Add(MenuRow(status));

        panel.Children.Add(new TextBlock
        {
            Text = "Menu keys: Up/Down open and move, Home/End jump, Escape closes and returns focus, " +
                   "outside press or focus leaving the menu closes it.",
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(status);
        panel.Children.Add(Heading("FAB — 56 / 80 / 96, six roles"));
        panel.Children.Add(new ViewportGallery(FabGrid, 280));

        panel.Children.Add(Heading("Extended FAB — Small / Medium / Large, six roles"));
        panel.Children.Add(new ViewportGallery(ExtendedGrid, 280));

        panel.Children.Add(Heading("Extended FAB — expand and collapse"));
        panel.Children.Add(new ViewportGallery(ExtendedCollapseDemo, 80));


        return new ScrollViewer
        {
            Content = panel,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
    }

    private static TextBlock Heading(string text) => new()
    {
        Text = text,
        FontSize = 20,
        FontWeight = FontWeight.Medium
    };

    private static Control FabGrid()
    {
        var panel = new StackPanel { Spacing = 12 };
        foreach (var size in Enum.GetValues<MaterialFabSize>())
        {
            var row = new WrapPanel { ItemSpacing = 16, LineSpacing = 16 };
            foreach (var role in Enum.GetValues<MaterialFabColorRole>())
            {
                var fab = new MaterialFab
                {
                    Size = size,
                    ColorRole = role,
                    Content = new PathIcon { Data = role is MaterialFabColorRole.Primary ? Plus : Edit },
                    VerticalAlignment = VerticalAlignment.Center
                };
                AutomationProperties.SetName(fab, $"{role} FAB, {size}");
                row.Children.Add(fab);
            }

            var disabled = new MaterialFab
            {
                Size = size,
                ColorRole = MaterialFabColorRole.PrimaryContainer,
                Content = new PathIcon { Data = Favorite },
                IsEnabled = false,
                VerticalAlignment = VerticalAlignment.Center
            };
            AutomationProperties.SetName(disabled, $"Disabled FAB, {size}");
            row.Children.Add(disabled);
            panel.Children.Add(row);
        }

        return panel;
    }

    private static Control ExtendedGrid()
    {
        var panel = new StackPanel { Spacing = 12 };
        foreach (var size in Enum.GetValues<MaterialExtendedFabSize>())
        {
            var row = new WrapPanel { ItemSpacing = 16, LineSpacing = 16 };
            foreach (var role in Enum.GetValues<MaterialFabColorRole>())
            {
                var fab = new MaterialExtendedFab
                {
                    Size = size,
                    ColorRole = role,
                    Label = role.ToString(),
                    Icon = role is MaterialFabColorRole.Primary ? Plus : Camera,
                    VerticalAlignment = VerticalAlignment.Center
                };
                row.Children.Add(fab);
            }

            panel.Children.Add(row);
        }

        return panel;
    }

    private static Control ExtendedCollapseDemo()
    {
        var fab = new MaterialExtendedFab
        {
            Size = MaterialExtendedFabSize.Medium,
            ColorRole = MaterialFabColorRole.TertiaryContainer,
            Label = "Compose",
            Icon = Edit,
            VerticalAlignment = VerticalAlignment.Center
        };
        var toggle = new ToggleButton
        {
            Content = "Expanded",
            IsChecked = true,
            VerticalAlignment = VerticalAlignment.Center
        };
        toggle.IsCheckedChanged += (_, _) => fab.IsExpanded = toggle.IsChecked == true;

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 16,
            Children = { fab, toggle }
        };
        return row;
    }

    private static Control MenuRow(TextBlock status)
    {
        var row = new WrapPanel { ItemSpacing = 24, LineSpacing = 24 };

        row.Children.Add(Menu(MaterialFabMenuColorRole.PrimaryContainer, "Primary", status, 3));
        row.Children.Add(Menu(MaterialFabMenuColorRole.SecondaryContainer, "Secondary", status, 4));
        row.Children.Add(Menu(MaterialFabMenuColorRole.TertiaryContainer, "Tertiary", status, 5));
        return row;
    }

    private static Control Menu(MaterialFabMenuColorRole role, string name, TextBlock status, int segments)
    {
        var menu = new MaterialFabMenu
        {
            ColorRole = role,
            VerticalAlignment = VerticalAlignment.Bottom,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        menu.Expanded += (_, _) => status.Text = $"{name} menu expanded.";
        menu.Collapsed += (_, _) => status.Text = $"{name} menu collapsed.";
        menu.Items.Add(Segment("Edit", Edit, name, status));
        menu.Items.Add(Segment("Share", Share, name, status));
        menu.Items.Add(Segment("Delete", Delete, name, status));
        if (segments > 3)
            menu.Items.Add(Segment("Favorite", Favorite, name, status));
        if (segments > 4)
            menu.Items.Add(Segment("Camera", Camera, name, status));

        var open = new CheckBox { Content = "Open", VerticalAlignment = VerticalAlignment.Bottom };
        open[!ToggleButton.IsCheckedProperty] = new Binding(nameof(MaterialFabMenu.IsExpanded))
        {
            Source = menu,
            Mode = BindingMode.TwoWay
        };

        var menuHost = new Panel { Height = 480, Children = { menu } };
        var column = new StackPanel
        {
            Spacing = 8,
            Width = 180,
            Children = { open, menuHost }
        };
        return column;
    }

    private static MaterialFabMenuSegment Segment(string label, Geometry icon, string menuName, TextBlock status) => new()
    {
        Label = label,
        Icon = icon,
        Command = new ActionCommand(() => status.Text = $"{menuName} menu: {label} invoked."),
        HorizontalAlignment = HorizontalAlignment.Right
    };

    private sealed class ActionCommand(Action action) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => action();
    }
}
