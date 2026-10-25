using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Catalog;

/// <summary>
/// Showcase for the Material Expressive button family: labeled buttons, text, elevated,
/// outlined and tonal buttons, icon buttons with the narrow/default/wide axis, and their
/// toggle variants. Every control is a real Button/ToggleButton, so pointer, Space/Enter,
/// access keys and automation peers behave exactly as they do in an application.
/// </summary>
public static class ButtonsGallery
{
    private static readonly Geometry Star = Geometry.Parse(
        "M12,3 14.8,8.7 21,9.6 16.5,14 17.6,20.2 12,17.3 6.4,20.2 7.5,14 3,9.6 9.2,8.7 Z");

    private static readonly Geometry StarFilled = Geometry.Parse(
        "M12,2 15.1,8.4 22,9.4 17,14.3 18.2,21.2 12,17.9 5.8,21.2 7,14.3 2,9.4 8.9,8.4 Z");

    private static readonly Geometry Check = Geometry.Parse(
        "M9,16.2 4.8,12 3.4,13.4 9,19 21,7 19.6,5.6 Z");

    public static Control Create()
    {
        var panel = new StackPanel { Spacing = 24, Margin = new Thickness(24) };
        var status = new TextBlock { Text = "Clicks: 0" };
        var clicks = 0;

        void Count()
        {
            clicks++;
            status.Text = $"Clicks: {clicks}";
        }

        panel.Children.Add(new TextBlock
        {
            Text = "Buttons",
            FontSize = 28,
            FontWeight = FontWeight.Medium,
        });
        panel.Children.Add(status);

        // Labeled buttons: six color treatments across all five sizes.
        panel.Children.Add(Caption("Labeled buttons - variants x sizes (press to see the source ripple)"));
        foreach (var variant in Enum.GetValues<MaterialButtonVariant>())
        {
            var row = Row();
            row.Children.Add(new TextBlock
            {
                Text = variant.ToString(),
                Width = 92,
                VerticalAlignment = VerticalAlignment.Center,
            });
            foreach (var size in Enum.GetValues<MaterialControlSize>())
            {
                var button = Labeled("Label", variant, size);
                button.Click += (_, _) => Count();
                row.Children.Add(button);
            }
            panel.Children.Add(row);
        }

        // Shape axis: Round and Square at every size for a filled and an outlined button.
        panel.Children.Add(Caption("Shapes - Round and Square per size"));
        foreach (var variant in new[] { MaterialButtonVariant.Filled, MaterialButtonVariant.Outlined })
        {
            foreach (var shape in Enum.GetValues<MaterialButtonShape>())
            {
                var row = Row();
                row.Children.Add(new TextBlock
                {
                    Text = $"{variant} {shape}",
                    Width = 150,
                    VerticalAlignment = VerticalAlignment.Center,
                });
                foreach (var size in Enum.GetValues<MaterialControlSize>())
                {
                    row.Children.Add(Labeled("Label", variant, size, shape));
                }
                panel.Children.Add(row);
            }
        }

        // Icon buttons: the Width axis changes the container box, never the glyph.
        panel.Children.Add(Caption("Icon buttons - widths per size (Filled)"));
        foreach (var size in Enum.GetValues<MaterialControlSize>())
        {
            var row = Row();
            row.Children.Add(new TextBlock
            {
                Text = size.ToString(),
                Width = 92,
                VerticalAlignment = VerticalAlignment.Center,
            });
            foreach (var width in Enum.GetValues<MaterialIconButtonWidth>())
            {
                var button = IconButton(Star, MaterialButtonVariant.Filled, size, width);
                button.Click += (_, _) => Count();
                row.Children.Add(button);
            }
            panel.Children.Add(row);
        }

        panel.Children.Add(Caption("Icon buttons - variants (Medium, Default width)"));
        var variantsRow = Row();
        foreach (var variant in Enum.GetValues<MaterialButtonVariant>())
        {
            var button = IconButton(Star, variant, MaterialControlSize.Medium, MaterialIconButtonWidth.Default);
            button.Click += (_, _) => Count();
            variantsRow.Children.Add(button);
        }
        panel.Children.Add(variantsRow);

        // Labeled toggles: unselected and selected side by side for every treatment.
        panel.Children.Add(Caption("Labeled toggle buttons - selected / unselected"));
        foreach (var variant in Enum.GetValues<MaterialButtonVariant>())
        {
            var row = Row();
            row.Children.Add(new TextBlock
            {
                Text = variant.ToString(),
                Width = 92,
                VerticalAlignment = VerticalAlignment.Center,
            });
            foreach (var shape in Enum.GetValues<MaterialButtonShape>())
            {
                row.Children.Add(LabeledToggle("Label", variant, shape, false));
                row.Children.Add(LabeledToggle("Label", variant, shape, true));
            }
            panel.Children.Add(row);
        }

        // Icon toggles: the selected glyph is swapped through MaterialAssist.SelectedIcon.
        panel.Children.Add(Caption("Icon toggle buttons - glyph swap"));
        foreach (var variant in Enum.GetValues<MaterialButtonVariant>())
        {
            var row = Row();
            row.Children.Add(new TextBlock
            {
                Text = variant.ToString(),
                Width = 92,
                VerticalAlignment = VerticalAlignment.Center,
            });
            foreach (var shape in Enum.GetValues<MaterialButtonShape>())
            {
                row.Children.Add(IconToggle(variant, shape, false));
                row.Children.Add(IconToggle(variant, shape, true));
            }
            panel.Children.Add(row);
        }

        // Disabled states keep the container treatment and drop the state layer.
        panel.Children.Add(Caption("Disabled"));
        var disabledRow = Row();
        foreach (var variant in Enum.GetValues<MaterialButtonVariant>())
        {
            var labeled = Labeled("Label", variant, MaterialControlSize.Small);
            labeled.IsEnabled = false;
            disabledRow.Children.Add(labeled);
        }
        foreach (var variant in Enum.GetValues<MaterialButtonVariant>())
        {
            var icon = IconButton(Star, variant, MaterialControlSize.Small, MaterialIconButtonWidth.Default);
            icon.IsEnabled = false;
            disabledRow.Children.Add(icon);
        }
        panel.Children.Add(disabledRow);

        // Keyboard: access keys come from the underscore in the content, and the icon host
        // keeps the label readable. Space and Enter are handled by Button/ToggleButton.
        panel.Children.Add(Caption("Keyboard"));
        var keyboardRow = Row();
        var save = Labeled("_Save", MaterialButtonVariant.Filled, MaterialControlSize.Small);
        save.Click += (_, _) => Count();
        AutomationProperties.SetName(save, "Save");
        keyboardRow.Children.Add(save);

        var mute = new ToggleButton
        {
            Content = "Mute",
            IsChecked = true,
        };
        MaterialAssist.SetButtonVariant(mute, MaterialButtonVariant.Tonal);
        MaterialAssist.SetIcon(mute, new PathIcon { Data = Star });
        MaterialAssist.SetSelectedIcon(mute, new PathIcon { Data = Check });
        MaterialAssist.SetControlSize(mute, MaterialControlSize.Small);
        AutomationProperties.SetName(mute, "Mute");
        keyboardRow.Children.Add(mute);
        panel.Children.Add(keyboardRow);

        return new ScrollViewer
        {
            Content = panel,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
    }

    private static Button Labeled(
        string text,
        MaterialButtonVariant variant,
        MaterialControlSize size,
        MaterialButtonShape shape = MaterialButtonShape.Round)
    {
        var button = new Button
        {
            Content = text,
            VerticalAlignment = VerticalAlignment.Center,
        };
        MaterialAssist.SetButtonVariant(button, variant);
        MaterialAssist.SetControlSize(button, size);
        MaterialAssist.SetButtonShape(button, shape);
        MaterialAssist.SetIcon(button, new PathIcon { Data = Star });
        return button;
    }

    private static ToggleButton LabeledToggle(
        string text,
        MaterialButtonVariant variant,
        MaterialButtonShape shape,
        bool isChecked)
    {
        var button = new ToggleButton
        {
            Content = text,
            IsChecked = isChecked,
            VerticalAlignment = VerticalAlignment.Center,
        };
        MaterialAssist.SetButtonVariant(button, variant);
        MaterialAssist.SetControlSize(button, MaterialControlSize.Small);
        MaterialAssist.SetButtonShape(button, shape);
        MaterialAssist.SetIcon(button, new PathIcon { Data = Star });
        MaterialAssist.SetSelectedIcon(button, new PathIcon { Data = Check });
        AutomationProperties.SetName(button, $"{text} {variant} {shape} {(isChecked ? "on" : "off")}");
        return button;
    }

    private static Button IconButton(
        Geometry glyph,
        MaterialButtonVariant variant,
        MaterialControlSize size,
        MaterialIconButtonWidth width)
    {
        var button = new Button
        {
            Content = new PathIcon { Data = glyph },
            VerticalAlignment = VerticalAlignment.Center,
        };
        MaterialAssist.SetButtonVariant(button, variant);
        MaterialAssist.SetControlSize(button, size);
        MaterialAssist.SetIsIconButton(button, true);
        MaterialAssist.SetIconButtonWidth(button, width);
        AutomationProperties.SetName(button, $"Icon {variant} {size} {width}");
        return button;
    }

    private static ToggleButton IconToggle(
        MaterialButtonVariant variant,
        MaterialButtonShape shape,
        bool isChecked)
    {
        // The glyph comes from the Icon/SelectedIcon pair, so checking the button swaps the
        // outline star for the filled star exactly like the source "Icon (selected)" property.
        var button = new ToggleButton
        {
            IsChecked = isChecked,
            VerticalAlignment = VerticalAlignment.Center,
        };
        MaterialAssist.SetButtonVariant(button, variant);
        MaterialAssist.SetControlSize(button, MaterialControlSize.Medium);
        MaterialAssist.SetButtonShape(button, shape);
        MaterialAssist.SetIsIconButton(button, true);
        MaterialAssist.SetIconButtonWidth(button, MaterialIconButtonWidth.Default);
        MaterialAssist.SetIcon(button, new PathIcon { Data = Star });
        MaterialAssist.SetSelectedIcon(button, new PathIcon { Data = StarFilled });
        AutomationProperties.SetName(button, $"Favorite {variant} {shape} {(isChecked ? "on" : "off")}");
        return button;
    }

    private static TextBlock Caption(string text) => new()
    {
        Text = text,
        FontSize = 16,
        FontWeight = FontWeight.Medium,
        Margin = new Thickness(0, 8, 0, 0),
    };

    private static StackPanel Row() => new()
    {
        Orientation = Orientation.Horizontal,
        Spacing = 12,
    };
}
