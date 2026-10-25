using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Catalog;

/// <summary>Catalog gallery for the button group family.</summary>
public static class ButtonGroupsGallery
{
    private const string Star = "M12,17.27 18.18,21 16.54,13.97 22,9.24 14.81,8.63 12,2 9.19,8.63 2,9.24 7.46,13.97 5.82,21Z";
    private const string Favorite = "M12,21.35 10.55,20.03C5.4,15.36 2,12.27 2,8.5 2,5.41 4.42,3 7.5,3 9.24,3 10.91,3.81 12,5.08 13.09,3.81 14.76,3 16.5,3 19.58,3 22,5.41 22,8.5 22,12.27 18.6,15.36 13.45,20.03Z";
    private const string Share = "M18,16.08C17.24,16.08 16.56,16.38 16.04,16.85L8.91,12.7C8.96,12.47 9,12.24 9,12C9,11.76 8.96,11.53 8.91,11.3L15.96,7.19C16.5,7.69 17.21,8 18,8A3,3 0 1,0 15,5C15,5.24 15.04,5.47 15.09,5.7L8.04,9.81C7.5,9.31 6.79,9 6,9A3,3 0 1,0 9,12C9,11.76 8.96,11.53 8.91,11.3L15.96,15.41C15.91,15.62 15.88,15.85 15.88,16.08A3,3 0 1,0 18,16.08Z";
    private const string Add = "M19,13H13V19H11V13H5V11H11V5H13V11H19V13Z";
    private const string Edit = "M3,17.25V21H6.75L17.81,9.94L14.06,6.19L3,17.25M20.71,7.04C21.1,6.65 21.1,6 20.71,5.63L18.37,3.29C18,2.9 17.35,2.9 16.96,3.29L15.13,5.12L18.88,8.87L20.71,7.04Z";
    private const string More = "M12,16A2,2 0 1,0 14,18A2,2 0 0,0 12,16M12,10A2,2 0 1,0 14,12A2,2 0 0,0 12,10M12,4A2,2 0 1,0 14,6A2,2 0 0,0 12,4Z";

    /// <summary>Builds the gallery surface for the button group family.</summary>
    public static Control Create()
    {
        var status = new TextBlock { Text = "No interaction yet.", TextWrapping = TextWrapping.Wrap };
        var panel = new StackPanel { Spacing = 24, Margin = new Thickness(24) };

        panel.Children.Add(Heading("Standard button group"));
        panel.Children.Add(Hint(
            "Separated action buttons with the 18/12/8 DIP source gaps. Each button is its own tab stop; " +
            "Space or Enter activates the focused button."));
        panel.Children.Add(StandardRow(MaterialButtonVariant.Filled, MaterialControlSize.ExtraSmall, status));
        panel.Children.Add(StandardRow(MaterialButtonVariant.Tonal, MaterialControlSize.Small, status));
        panel.Children.Add(StandardRow(MaterialButtonVariant.Outlined, MaterialControlSize.Medium, status));
        panel.Children.Add(StandardLabelRow(MaterialButtonVariant.Tonal, MaterialControlSize.Small, status));

        panel.Children.Add(Heading("Connected button group"));
        panel.Children.Add(Hint(
            "Joined segments with the 2 DIP seam and a pill-shaped selection. Tab enters at the selected " +
            "segment, arrow keys move focus with wrapping, Home/End jump to the ends, Space selects."));
        panel.Children.Add(ConnectedRow(MaterialButtonShape.Round, MaterialControlSize.ExtraSmall, false, status));
        panel.Children.Add(ConnectedRow(MaterialButtonShape.Round, MaterialControlSize.Medium, false, status));
        panel.Children.Add(ConnectedRow(MaterialButtonShape.Square, MaterialControlSize.Medium, true, status));

        panel.Children.Add(Heading("Segmented button"));
        panel.Children.Add(Hint(
            "Shared 1 DIP outline, secondary-container selection and a check mark that replaces the leading " +
            "icon. Densities 0 to -3 map to the 48/44/40/36 DIP source heights."));
        panel.Children.Add(SegmentedRow(0, false, status));
        panel.Children.Add(SegmentedRow(-1, false, status));
        panel.Children.Add(SegmentedRow(-2, true, status));
        panel.Children.Add(SegmentedRow(-3, false, status));

        panel.Children.Add(Heading("Split button"));
        panel.Children.Add(Hint(
            "Two real buttons: the leading half raises Click, the trailing half opens its menu with a click, " +
            "Down, Alt+Down or F4. The trailing state layer stays selected while the menu is open."));
        foreach (var variant in new[]
                 {
                     MaterialButtonVariant.Filled, MaterialButtonVariant.Tonal,
                     MaterialButtonVariant.Elevated, MaterialButtonVariant.Outlined
                 })
        {
            panel.Children.Add(SplitRow(variant, MaterialControlSize.Small, status));
        }
        panel.Children.Add(SplitRow(MaterialButtonVariant.Filled, MaterialControlSize.ExtraSmall, status));
        panel.Children.Add(SplitRow(MaterialButtonVariant.Tonal, MaterialControlSize.Medium, status));

        panel.Children.Add(Heading("State"));
        panel.Children.Add(status);
        panel.Children.Add(Hint("Selected items and split-button actions report here."));

        return new ScrollViewer
        {
            Content = panel,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
    }

    private static Control Heading(string text) => new TextBlock
    {
        Text = text,
        FontSize = 24,
        FontWeight = FontWeight.SemiBold
    };

    private static Control Hint(string text) => new TextBlock
    {
        Text = text,
        TextWrapping = TextWrapping.Wrap,
        MaxWidth = 760,
        Opacity = 0.7
    };

    private static PathIcon Icon(string data) => new()
    {
        Data = Geometry.Parse(data),
        Width = 24,
        Height = 24
    };

    private static Control StandardRow(
        MaterialButtonVariant variant, MaterialControlSize size, TextBlock status)
    {
        var group = new MaterialButtonGroup
        {
            Kind = MaterialButtonGroupKind.Standard,
            Variant = variant,
            ControlSize = size,
            SelectionMode = MaterialButtonGroupSelectionMode.None
        };

        foreach (var (data, name) in new[] { (Star, "Star"), (Favorite, "Favorite"), (Share, "Share"), (Add, "Add") })
        {
            var item = new MaterialButtonGroupItem { Icon = Icon(data) };
            Avalonia.Automation.AutomationProperties.SetName(item, name);
            item.Click += (_, _) => status.Text = $"Standard group: {name} pressed ({variant}, {size}).";
            group.Items.Add(item);
        }

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 16,
            Children =
            {
                new TextBlock
                {
                    Text = $"{variant} · {size}",
                    Width = 180,
                    VerticalAlignment = VerticalAlignment.Center
                },
                group
            }
        };
    }

    private static Control StandardLabelRow(
        MaterialButtonVariant variant, MaterialControlSize size, TextBlock status)
    {
        var group = new MaterialButtonGroup
        {
            Kind = MaterialButtonGroupKind.Standard,
            Variant = variant,
            ControlSize = size
        };

        foreach (var label in new[] { "Add", "Edit", "More" })
            group.Items.Add(label);

        group.AddHandler(Button.ClickEvent, (_, _) => status.Text = $"Standard group: label pressed ({variant}, {size}).");
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 16,
            Children =
            {
                new TextBlock { Text = $"Labels · {variant} · {size}", Width = 180, VerticalAlignment = VerticalAlignment.Center },
                group
            }
        };
    }

    private static Control ConnectedRow(
        MaterialButtonShape shape, MaterialControlSize size, bool multiSelect, TextBlock status)
    {
        var group = new MaterialButtonGroup
        {
            Kind = MaterialButtonGroupKind.Connected,
            ButtonShape = shape,
            ControlSize = size,
            ShowSelectedIcon = false,
            SelectionMode = multiSelect
                ? MaterialButtonGroupSelectionMode.Multiple
                : MaterialButtonGroupSelectionMode.Single,
            SelectedIndex = 0
        };

        foreach (var (data, label) in new[] { (Star, "Star"), (Favorite, "Save"), (Share, "Share"), (More, "More") })
        {
            group.Items.Add(new MaterialButtonGroupItem { Icon = Icon(data), Content = label });
        }

        group.SelectionChanged += (_, e) =>
            status.Text = $"Connected group: selected {string.Join(", ", e.SelectedItems)}.";
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 16,
            Children =
            {
                new TextBlock
                {
                    Text = $"{shape} · {size} · {(multiSelect ? "multiple" : "single")}",
                    Width = 180,
                    VerticalAlignment = VerticalAlignment.Center
                },
                group
            }
        };
    }

    private static Control SegmentedRow(int density, bool withIcons, TextBlock status)
    {
        var segmented = new MaterialSegmentedButton { Density = density };

        foreach (var (label, icon) in new[]
                 {
                     ("Day", Star), ("Week", Favorite), ("Month", Share), ("Year", Add)
                 })
        {
            var item = new MaterialButtonGroupItem { Content = label };
            if (withIcons)
                item.Icon = Icon(icon);
            segmented.Items.Add(item);
        }

        segmented.SelectionChanged += (_, e) =>
            status.Text = $"Segmented: density {density}, selected {e.SelectedItem}.";

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 16,
            Children =
            {
                new TextBlock
                {
                    Text = $"Density {density}{(withIcons ? " · icons" : string.Empty)}",
                    Width = 180,
                    VerticalAlignment = VerticalAlignment.Center
                },
                segmented
            }
        };
    }

    private static Control SplitRow(
        MaterialButtonVariant variant, MaterialControlSize size, TextBlock status)
    {
        var menu = new MenuFlyout
        {
            Placement = PlacementMode.BottomEdgeAlignedRight,
            ItemsSource = new object[]
            {
                new MenuItem { Header = "Save draft" },
                new MenuItem { Header = "Save and close" },
                new MenuItem { Header = "Discard" }
            }
        };

        var split = new MaterialSplitButton
        {
            Content = "Save",
            Icon = Icon(Edit),
            Variant = variant,
            ControlSize = size,
            Flyout = menu
        };

        split.Click += (_, _) => status.Text = $"Split button: primary action ({variant}, {size}).";
        split.PropertyChanged += (_, e) =>
        {
            if (e.Property == MaterialSplitButton.IsMenuOpenProperty)
                status.Text = $"Split button menu {(split.IsMenuOpen ? "opened" : "closed")} ({variant}, {size}).";
        };

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 16,
            Children =
            {
                new TextBlock
                {
                    Text = $"{variant} · {size}",
                    Width = 180,
                    VerticalAlignment = VerticalAlignment.Center
                },
                split
            }
        };
    }
}
