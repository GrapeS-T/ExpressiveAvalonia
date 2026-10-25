using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Catalog;

/// <summary>Source menu family gallery: menu buttons, baseline/expressive menus, groups and blocks.</summary>
public static class MenusGallery
{
    private static readonly Geometry UndoGeometry = Geometry.Parse("M12.5,8C9.85,8 7.45,8.99 5.6,10.6L2,7V16H11L7.38,12.38C8.77,11.22 10.54,10.5 12.5,10.5C16.04,10.5 19.05,12.81 20.1,16L22.47,15.22C21.08,11.03 17.15,8 12.5,8Z");
    private static readonly Geometry RedoGeometry = Geometry.Parse("M18.4,10.6C16.55,8.99 14.15,8 11.5,8C6.85,8 2.92,11.03 1.54,15.22L3.9,16C4.95,12.81 7.95,10.5 11.5,10.5C13.45,10.5 15.23,11.22 16.62,12.38L13,16H22V7L18.4,10.6Z");
    private static readonly Geometry EditGeometry = Geometry.Parse("M3,17.25V21H6.75L17.81,9.94L14.06,6.19L3,17.25ZM20.71,7.04C21.1,6.65 21.1,6.02 20.71,5.63L18.37,3.29C17.98,2.9 17.35,2.9 16.96,3.29L15.13,5.12L18.88,8.87L20.71,7.04Z");
    private static readonly Geometry CopyGeometry = Geometry.Parse("M16,1H4C2.9,1 2,1.9 2,3V17H4V3H16V1ZM19,5H8C6.9,5 6,5.9 6,7V21C6,22.1 6.9,23 8,23H19C20.1,23 21,22.1 21,21V7C21,5.9 20.1,5 19,5ZM19,21H8V7H19V21Z");
    private static readonly Geometry ShareGeometry = Geometry.Parse("M18,16.08C17.24,16.08 16.56,16.38 16.04,16.85L8.91,12.7C8.96,12.47 9,12.24 9,12C9,11.76 8.96,11.53 8.91,11.3L15.96,7.19C16.5,7.69 17.21,8 18,8C19.66,8 21,6.66 21,5C21,3.34 19.66,2 18,2C16.34,2 15,3.34 15,5C15,5.24 15.04,5.47 15.09,5.7L8.04,9.81C7.5,9.31 6.79,9 6,9C4.34,9 3,10.34 3,12C3,13.66 4.34,15 6,15C6.79,15 7.5,14.69 8.04,14.19L15.16,18.34C15.11,18.55 15.08,18.77 15.08,19C15.08,20.61 16.39,21.91 18,21.91C19.61,21.91 20.92,20.61 20.92,19C20.92,17.39 19.61,16.08 18,16.08Z");
    private static readonly Geometry DeleteGeometry = Geometry.Parse("M6,19C6,20.1 6.9,21 8,21H16C17.1,21 18,20.1 18,19V7H6V19ZM19,4H15.5L14.5,3H9.5L8.5,4H5V6H19V4Z");
    private static readonly Geometry MoreGeometry = Geometry.Parse("M12,8C13.1,8 14,7.1 14,6C14,4.9 13.1,4 12,4C10.9,4 10,4.9 10,6C10,7.1 10.9,8 12,8ZM12,10C10.9,10 10,10.9 10,12C10,13.1 10.9,14 12,14C13.1,14 14,13.1 14,12C14,10.9 13.1,10 12,10ZM12,16C10.9,16 10,16.9 10,18C10,19.1 10.9,20 12,20C13.1,20 14,19.1 14,18C14,16.9 13.1,16 12,16Z");
    private static readonly Geometry StarGeometry = Geometry.Parse("M12,17.27L18.18,21L16.54,13.97L22,9.24L14.81,8.62L12,2L9.19,8.62L2,9.24L7.45,13.97L5.82,21L12,17.27Z");
    private static readonly Geometry SettingsGeometry = Geometry.Parse("M19.14,12.94C19.18,12.64 19.2,12.33 19.2,12C19.2,11.67 19.18,11.36 19.14,11.06L21.16,9.48C21.34,9.34 21.39,9.07 21.26,8.86L19.34,5.54C19.22,5.32 18.95,5.25 18.73,5.32L16.35,6.28C15.86,5.9 15.33,5.59 14.75,5.35L14.39,2.81C14.36,2.57 14.15,2.4 13.91,2.4H10.09C9.85,2.4 9.65,2.57 9.61,2.81L9.25,5.35C8.68,5.59 8.14,5.9 7.65,6.28L5.27,5.32C5.05,5.25 4.78,5.32 4.66,5.54L2.74,8.86C2.61,9.07 2.66,9.34 2.84,9.48L4.86,11.06C4.82,11.36 4.8,11.67 4.8,12C4.8,12.33 4.82,12.64 4.86,12.94L2.84,14.52C2.66,14.66 2.61,14.93 2.74,15.14L4.66,18.46C4.78,18.68 5.05,18.75 5.27,18.68L7.65,17.72C8.14,18.1 8.68,18.41 9.25,18.65L9.61,21.19C9.65,21.43 9.85,21.6 10.09,21.6H13.91C14.15,21.6 14.36,21.43 14.39,21.19L14.75,18.65C15.33,18.41 15.86,18.1 16.35,17.72L18.73,18.68C18.95,18.75 19.22,18.68 19.34,18.46L21.26,15.14C21.39,14.93 21.34,14.66 21.16,14.52L19.14,12.94ZM12,15.6C10.02,15.6 8.4,13.98 8.4,12C8.4,10.02 10.02,8.4 12,8.4C13.98,8.4 15.6,10.02 15.6,12C15.6,13.98 13.98,15.6 12,15.6Z");

    /// <summary>Builds the interactive menu family gallery used by the catalog.</summary>
    public static Control Create()
    {
        var status = Muted("Activate an item to see its command fire.");
        status.TextWrapping = TextWrapping.Wrap;

        var root = new StackPanel { Spacing = 24, Margin = new Thickness(24) };
        root.Children.Add(SectionTitle("Menu buttons (Menu button building block)"));
        var triggers = new WrapPanel { ItemSpacing = 16, LineSpacing = 16 };
        triggers.Children.Add(MenuTrigger("Baseline", MaterialMenuSurfaceStyle.Baseline, MaterialMenuVariant.Standard, status));
        triggers.Children.Add(MenuTrigger("Expressive standard", MaterialMenuSurfaceStyle.Expressive, MaterialMenuVariant.Standard, status));
        triggers.Children.Add(MenuTrigger("Expressive vibrant", MaterialMenuSurfaceStyle.Expressive, MaterialMenuVariant.Vibrant, status));
        root.Children.Add(triggers);

        root.Children.Add(SectionTitle("Context menus (click or right-click; arrow keys and Escape are native)"));
        var contexts = new WrapPanel { ItemSpacing = 16, LineSpacing = 16 };
        contexts.Children.Add(ContextHost("Expressive standard", MaterialMenuSurfaceStyle.Expressive, MaterialMenuVariant.Standard, MaterialMenuDensity.Default, status));
        contexts.Children.Add(ContextHost("Expressive vibrant", MaterialMenuSurfaceStyle.Expressive, MaterialMenuVariant.Vibrant, MaterialMenuDensity.Default, status));
        contexts.Children.Add(ContextHost("Baseline density 0", MaterialMenuSurfaceStyle.Baseline, MaterialMenuVariant.Standard, MaterialMenuDensity.Default, status));
        contexts.Children.Add(ContextHost("Baseline density -2", MaterialMenuSurfaceStyle.Baseline, MaterialMenuVariant.Standard, MaterialMenuDensity.Dense2, status));
        contexts.Children.Add(ContextHost("Baseline density -4", MaterialMenuSurfaceStyle.Baseline, MaterialMenuVariant.Standard, MaterialMenuDensity.Dense4, status));
        root.Children.Add(contexts);

        root.Children.Add(SectionTitle("Menu bar with nested submenus"));
        var bars = new StackPanel { Spacing = 16 };
        bars.Children.Add(MenuBar(MaterialMenuSurfaceStyle.Baseline, MaterialMenuVariant.Standard, status));
        bars.Children.Add(MenuBar(MaterialMenuSurfaceStyle.Expressive, MaterialMenuVariant.Vibrant, status));
        root.Children.Add(bars);

        root.Children.Add(SectionTitle("Grouped menu surface (Groups = 1, 2, 3)"));
        var groupRow = new WrapPanel { ItemSpacing = 24, LineSpacing = 24 };
        groupRow.Children.Add(GroupedSurface(1, MaterialMenuVariant.Standard, status));
        groupRow.Children.Add(GroupedSurface(2, MaterialMenuVariant.Standard, status));
        groupRow.Children.Add(GroupedSurface(3, MaterialMenuVariant.Vibrant, status));
        root.Children.Add(groupRow);

        root.Children.Add(SectionTitle("Menu building blocks (standard / vibrant)"));
        root.Children.Add(BuildingBlocks());

        root.Children.Add(SectionTitle("Menu item anatomy (supporting text, trailing icon, divider, active)"));
        root.Children.Add(ItemAnatomy(status));

        root.Children.Add(SectionTitle("Section labels"));
        root.Children.Add(SectionLabels());

        root.Children.Add(SectionTitle("Menu - icon button example"));
        root.Children.Add(IconButtonMenuExample(status));

        root.Children.Add(status);
        return new ScrollViewer
        {
            Content = root,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
    }

    private static MaterialMenuButton MenuTrigger(
        string label,
        MaterialMenuSurfaceStyle style,
        MaterialMenuVariant variant,
        TextBlock status)
    {
        var button = new MaterialMenuButton { Content = label };
        MaterialMenu.SetVariant(button, variant);
        button.Flyout = BuildFlyout(style, variant, status);
        return button;
    }

    private static MenuFlyout BuildFlyout(MaterialMenuSurfaceStyle style, MaterialMenuVariant variant, TextBlock status)
    {
        var flyout = new MenuFlyout();
        if (style == MaterialMenuSurfaceStyle.Expressive)
        {
            flyout.FlyoutPresenterTheme = Theme(variant == MaterialMenuVariant.Vibrant
                ? "MaterialMenuFlyoutPresenterVibrant"
                : "MaterialMenuFlyoutPresenterExpressive");
        }

        flyout.Items.Add(SectionLabel("Edit"));
        flyout.Items.Add(Item("Undo", status, new KeyGesture(Key.Z, KeyModifiers.Control), UndoGeometry));
        flyout.Items.Add(Item("Redo", status, new KeyGesture(Key.Y, KeyModifiers.Control), RedoGeometry));
        flyout.Items.Add(SeparatorItem());
        flyout.Items.Add(Item("Cut", status, new KeyGesture(Key.X, KeyModifiers.Control), null));
        flyout.Items.Add(Item("Copy", status, new KeyGesture(Key.C, KeyModifiers.Control), CopyGeometry));
        flyout.Items.Add(Item("Paste", status, new KeyGesture(Key.V, KeyModifiers.Control), null));
        flyout.Items.Add(SeparatorItem());
        flyout.Items.Add(SectionLabel("View"));
        flyout.Items.Add(Checkable("Show grid", status, isChecked: true));
        flyout.Items.Add(Checkable("Show rulers", status, isChecked: false));
        flyout.Items.Add(Item("More tools", status, trailing: ChevronRightGeometry()));
        flyout.Items.Add(SeparatorItem());
        flyout.Items.Add(Disabled(Item("Delete", status, new KeyGesture(Key.Delete, KeyModifiers.None), DeleteGeometry)));
        flyout.Items.Add(Item("Properties", status, trailing: SettingsGeometry));
        return flyout;
    }

    private static Button ContextHost(
        string label,
        MaterialMenuSurfaceStyle style,
        MaterialMenuVariant variant,
        MaterialMenuDensity density,
        TextBlock status)
    {
        var button = new Button { Content = label };
        var menu = new ContextMenu();
        MaterialMenu.SetSurfaceStyle(menu, style);
        MaterialMenu.SetVariant(menu, variant);
        MaterialMenu.SetDensity(menu, density);
        MaterialMenu.SetShowScrollBar(menu, true);
        PopulateMenu(menu, status);
        button.ContextMenu = menu;
        button.Click += (_, _) => menu.Open(button);
        return button;
    }

    private static void PopulateMenu(ItemsControl menu, TextBlock status)
    {
        menu.Items.Add(Item("Open", status, new KeyGesture(Key.O, KeyModifiers.Control), EditGeometry));
        menu.Items.Add(Item("Duplicate", status, new KeyGesture(Key.D, KeyModifiers.Control), CopyGeometry));
        menu.Items.Add(Item("Share", status, null, ShareGeometry));
        menu.Items.Add(SeparatorItem());
        menu.Items.Add(Checkable("Star", status, isChecked: true));
        menu.Items.Add(Item("Rename", status, null, EditGeometry));
        var more = new MenuItem { Header = "More" };
        more.Items.Add(Item("Move", status, null, null));
        more.Items.Add(Item("Add to album", status, null, null));
        more.Items.Add(SeparatorItem());
        more.Items.Add(Item("Archive", status, null, null));
        menu.Items.Add(more);
        menu.Items.Add(SeparatorItem());
        menu.Items.Add(Disabled(Item("Delete", status, new KeyGesture(Key.Delete, KeyModifiers.None), DeleteGeometry)));
    }

    private static Control MenuBar(MaterialMenuSurfaceStyle style, MaterialMenuVariant variant, TextBlock status)
    {
        var menu = new Menu { HorizontalAlignment = HorizontalAlignment.Left };
        MaterialMenu.SetSurfaceStyle(menu, style);
        MaterialMenu.SetVariant(menu, variant);

        var file = new MenuItem { Header = "File" };
        file.Items.Add(Item("New", status, new KeyGesture(Key.N, KeyModifiers.Control), null));
        file.Items.Add(Item("Open", status, new KeyGesture(Key.O, KeyModifiers.Control), EditGeometry));
        file.Items.Add(SeparatorItem());
        file.Items.Add(Item("Save", status, new KeyGesture(Key.S, KeyModifiers.Control), null));
        file.Items.Add(Item("Exit", status, null, null));

        var edit = new MenuItem { Header = "Edit" };
        edit.Items.Add(Item("Undo", status, new KeyGesture(Key.Z, KeyModifiers.Control), UndoGeometry));
        edit.Items.Add(Item("Redo", status, new KeyGesture(Key.Y, KeyModifiers.Control), RedoGeometry));
        edit.Items.Add(SeparatorItem());
        edit.Items.Add(Checkable("Word wrap", status, isChecked: true));

        var view = new MenuItem { Header = "View" };
        var theme = new MenuItem { Header = "Theme" };
        theme.Items.Add(Radio("Standard", status, "menu-theme", isChecked: true));
        theme.Items.Add(Radio("Vibrant", status, "menu-theme", isChecked: false));
        view.Items.Add(theme);
        view.Items.Add(Item("Settings", status, null, SettingsGeometry));

        menu.Items.Add(file);
        menu.Items.Add(edit);
        menu.Items.Add(view);
        return menu;
    }

    private static Control GroupedSurface(int groupCount, MaterialMenuVariant variant, TextBlock status)
    {
        var groups = new StackPanel { Spacing = 2 };
        for (var index = 0; index < groupCount; index++)
        {
            var group = new MaterialMenuGroup { Header = $"Group {index + 1}" };
            MaterialMenu.SetSurfaceStyle(group, MaterialMenuSurfaceStyle.Expressive);
            MaterialMenu.SetVariant(group, variant);
            group.Items.Add(GroupItem("Menu item 01", status, EditGeometry, ChevronRightGeometry()));
            group.Items.Add(GroupItem("Menu item 02", status, CopyGeometry, null));
            group.Items.Add(GroupItem("Menu item 03", status, ShareGeometry, null));
            group.Items.Add(GroupItem("Menu item 04", status, null, null));
            group.Items.Add(GroupItem("Menu item 05", status, null, null));
            group.Items.Add(GroupItem("Menu item 06", status, null, ChevronRightGeometry()));
            groups.Children.Add(group);
        }

        var surface = new Border
        {
            Width = 240,
            Padding = new Thickness(2),
            CornerRadius = new CornerRadius(16),
            Child = groups
        };
        surface.Bind(Border.BackgroundProperty, surface.GetResourceObservable(
            variant == MaterialMenuVariant.Vibrant ? "MaterialTertiaryContainerBrush" : "MaterialSurfaceContainerLowBrush"));
        return surface;
    }

    private static MenuItem GroupItem(string header, TextBlock status, Geometry? icon, Geometry? trailing)
    {
        var item = new MenuItem
        {
            Header = header,
            Icon = icon is null ? null : new PathIcon { Data = icon }
        };
        MaterialMenu.SetTrailingIcon(item, trailing is null ? null : new PathIcon { Data = trailing });
        item.Click += (_, _) => status.Text = $"Command fired: {header}";
        return item;
    }

    private static Control BuildingBlocks()
    {
        var panel = new StackPanel { Spacing = 12 };
        foreach (var variant in new[] { MaterialMenuVariant.Standard, MaterialMenuVariant.Vibrant })
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            var icon = new Button { Theme = Theme("MaterialMenuIconButton"), Content = new PathIcon { Data = MoreGeometry } };
            MaterialMenu.SetVariant(icon, variant);
            row.Children.Add(icon);

            var iconToggle = new ToggleButton
            {
                Theme = Theme("MaterialMenuIconToggleButton"),
                Content = new PathIcon { Data = StarGeometry },
                IsChecked = true
            };
            MaterialMenu.SetVariant(iconToggle, variant);
            row.Children.Add(iconToggle);

            var toggle = new ToggleButton
            {
                Theme = Theme("MaterialMenuToggleButton"),
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children =
                    {
                        new PathIcon { Data = ShareGeometry },
                        new TextBlock { Text = "Share" }
                    }
                }
            };
            MaterialMenu.SetVariant(toggle, variant);
            row.Children.Add(toggle);
            row.Children.Add(new TextBlock
            {
                Text = variant.ToString(),
                VerticalAlignment = VerticalAlignment.Center
            });
            panel.Children.Add(row);
        }

        return panel;
    }

    private static Control ItemAnatomy(TextBlock status)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 24 };
        foreach (var variant in new[] { MaterialMenuVariant.Standard, MaterialMenuVariant.Vibrant })
        {
            var group = new MaterialMenuGroup();
            MaterialMenu.SetSurfaceStyle(group, MaterialMenuSurfaceStyle.Expressive);
            MaterialMenu.SetVariant(group, variant);

            var supporting = Item("With supporting text", status, null, EditGeometry);
            MaterialMenu.SetSupportingText(supporting, "Supporting text");
            group.Items.Add(supporting);

            var trailing = Item("With trailing icon", status, null, null, SettingsGeometry);
            group.Items.Add(trailing);

            var divider = Item("Divider below", status, null, null);
            MaterialMenu.SetShowDivider(divider, true);
            group.Items.Add(divider);

            var active = Item("Active destination", status, null, StarGeometry);
            MaterialMenu.SetIsActive(active, true);
            group.Items.Add(active);

            var disabled = Disabled(Item("Disabled item", status, null, DeleteGeometry));
            group.Items.Add(disabled);

            var surface = new Border
            {
                Width = 260,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(16),
                Child = group
            };
            surface.Bind(Border.BackgroundProperty, surface.GetResourceObservable(
                variant == MaterialMenuVariant.Vibrant ? "MaterialTertiaryContainerBrush" : "MaterialSurfaceContainerLowBrush"));
            row.Children.Add(surface);
        }

        return row;
    }

    private static Control SectionLabels()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
        foreach (var variant in new[] { MaterialMenuVariant.Standard, MaterialMenuVariant.Vibrant })
        {
            var label = new ContentControl
            {
                Theme = Theme("MaterialMenuLabel"),
                Content = $"{variant} section label"
            };
            MaterialMenu.SetVariant(label, variant);
            row.Children.Add(label);
        }

        return row;
    }

    private static Control IconButtonMenuExample(TextBlock status)
    {
        var iconButton = new Button
        {
            Content = new PathIcon { Data = MoreGeometry },
            VerticalAlignment = VerticalAlignment.Center
        };
        MaterialAssist.SetButtonVariant(iconButton, MaterialButtonVariant.Filled);
        MaterialAssist.SetIsIconButton(iconButton, true);
        MaterialAssist.SetControlSize(iconButton, MaterialControlSize.Small);

        var menu = new ContextMenu();
        PopulateMenu(menu, status);
        iconButton.ContextMenu = menu;
        iconButton.Click += (_, _) => menu.Open(iconButton);

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        row.Children.Add(iconButton);
        row.Children.Add(new TextBlock
        {
            Text = "Filled icon button opens the baseline menu.",
            VerticalAlignment = VerticalAlignment.Center
        });
        return row;
    }

    private static MenuItem Item(
        string header,
        TextBlock status,
        KeyGesture? gesture = null,
        Geometry? icon = null,
        Geometry? trailing = null)
    {
        var item = new MenuItem
        {
            Header = header,
            InputGesture = gesture,
            Icon = icon is null ? null : new PathIcon { Data = icon }
        };
        MaterialMenu.SetTrailingIcon(item, trailing is null ? null : new PathIcon { Data = trailing });
        item.Click += (_, _) => status.Text = $"Command fired: {header}";
        return item;
    }

    private static MenuItem Checkable(string header, TextBlock status, bool isChecked)
    {
        var item = new MenuItem
        {
            Header = header,
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = isChecked
        };
        item.Click += (_, _) => status.Text = $"{header}: {(item.IsChecked ? "on" : "off")}";
        return item;
    }

    private static MenuItem Radio(string header, TextBlock status, string groupName, bool isChecked)
    {
        var item = new MenuItem
        {
            Header = header,
            ToggleType = MenuItemToggleType.Radio,
            GroupName = groupName,
            IsChecked = isChecked
        };
        item.Click += (_, _) => status.Text = $"Theme: {header}";
        return item;
    }

    private static MenuItem SectionLabel(string header)
    {
        var item = new MenuItem
        {
            Header = header,
            IsEnabled = false,
            Focusable = false
        };
        MaterialMenu.SetIsSectionLabel(item, true);
        return item;
    }

    private static MenuItem SeparatorItem() => new() { Header = "-" };

    private static MenuItem Disabled(MenuItem item)
    {
        item.IsEnabled = false;
        return item;
    }

    private static Geometry ChevronRightGeometry() =>
        Geometry.Parse("M8.59,16.59L13.17,12L8.59,7.41L10,6L16,12L10,18L8.59,16.59Z");

    private static TextBlock SectionTitle(string text) => new()
    {
        Text = text,
        FontSize = 20,
        FontWeight = FontWeight.Medium
    };

    private static TextBlock Muted(string text)
    {
        var block = new TextBlock { Text = text };
        block.Bind(TextBlock.ForegroundProperty, block.GetResourceObservable("MaterialOnSurfaceVariantBrush"));
        return block;
    }

    private static ControlTheme Theme(string key) =>
        (ControlTheme)Application.Current!.FindResource(key)!;
}
