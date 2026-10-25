using System;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using ExpressiveAvalonia.Controls;

namespace ExpressiveAvalonia.Catalog;

/// <summary>Catalog page for the Material assist, filter, input and suggestion chip families.</summary>
public static class ChipsGallery
{
    public static Control Create()
    {
        var panel = new StackPanel { Spacing = 20, Margin = new Thickness(24) };
        var status = new TextBlock { Text = "Selection: none", TextWrapping = TextWrapping.Wrap };

        panel.Children.Add(new TextBlock
        {
            Text = "Chips",
            FontSize = 28,
            FontWeight = FontWeight.SemiBold
        });
        panel.Children.Add(new TextBlock
        {
            Text = "Assist chips act; filter, input and suggestion chips select. " +
                   "Tab enters a group once, arrows move focus, Space/Enter toggle, Delete removes an input chip.",
            TextWrapping = TextWrapping.Wrap
        });

        panel.Children.Add(Section("Assistive chips · action-only, outlined and elevated"));
        var assistRow = Row();
        assistRow.Children.Add(Chip("Label", MaterialChipKind.Assist));
        assistRow.Children.Add(Chip("Label", MaterialChipKind.Assist, icon: Icon("M 12,3 14.8,8.7 21,9.6 16.5,14 17.6,20.2 12,17.3 6.4,20.2 7.5,14 3,9.6 9.2,8.7 Z")));
        assistRow.Children.Add(Chip("Label", MaterialChipKind.Assist, MaterialChipVariant.Elevated,
            Icon("M 12,3 14.8,8.7 21,9.6 16.5,14 17.6,20.2 12,17.3 6.4,20.2 7.5,14 3,9.6 9.2,8.7 Z")));
        assistRow.Children.Add(Chip("Brand", MaterialChipKind.Assist, icon: BrandIcon()));
        var disabledAssist = Chip("Disabled", MaterialChipKind.Assist, icon: Icon("M 12,3 14.8,8.7 21,9.6 16.5,14 17.6,20.2 12,17.3 6.4,20.2 7.5,14 3,9.6 9.2,8.7 Z"));
        disabledAssist.IsEnabled = false;
        assistRow.Children.Add(disabledAssist);
        panel.Children.Add(assistRow);

        panel.Children.Add(Section("Filter chips · multi-selection, leading icon and trailing caret"));
        var filters = new MaterialChipGroup { SelectionMode = MaterialChipSelectionMode.Multiple, HorizontalAlignment = HorizontalAlignment.Left };
        var selectedFilter = Chip("Selected", MaterialChipKind.Filter, MaterialChipVariant.Elevated, isSelected: true);
        filters.Items.Add(selectedFilter);
        filters.Items.Add(Chip("Leading icon", MaterialChipKind.Filter, icon: Icon("M 12,2 A 10,10 0 1 1 11.99,2 Z")));
        filters.Items.Add(Chip("Trailing", MaterialChipKind.Filter, trailing: true));
        filters.Items.Add(Chip("Both", MaterialChipKind.Filter, icon: Icon("M 12,2 A 10,10 0 1 1 11.99,2 Z"), trailing: true));
        var disabledFilter = Chip("Disabled", MaterialChipKind.Filter);
        disabledFilter.IsEnabled = false;
        filters.Items.Add(disabledFilter);
        panel.Children.Add(filters);

        panel.Children.Add(Section("Suggestion chips · single selection, icon axis"));
        var suggestions = new MaterialChipGroup { SelectionMode = MaterialChipSelectionMode.Single, HorizontalAlignment = HorizontalAlignment.Left };
        suggestions.Items.Add(Chip("Outlined", MaterialChipKind.Suggestion, icon: Icon("M 4,4 L 20,4 L 20,20 L 4,20 Z")));
        suggestions.Items.Add(Chip("Elevated", MaterialChipKind.Suggestion, MaterialChipVariant.Elevated,
            Icon("M 4,4 L 20,4 L 20,20 L 4,20 Z"), isSelected: true));
        suggestions.Items.Add(Chip("No icon", MaterialChipKind.Suggestion, showIcon: false));
        panel.Children.Add(suggestions);

        panel.Children.Add(Section("Input chips · avatar/icon configurations, closing icon and Delete key"));
        var inputs = new MaterialChipGroup
        {
            SelectionMode = MaterialChipSelectionMode.Multiple,
            Layout = MaterialChipGroupLayout.MultipleRows,
            Width = 360,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        inputs.Items.Add(Chip("Alice", MaterialChipKind.Input, avatar: Avatar("A"), removable: true, isSelected: true));
        inputs.Items.Add(Chip("Bob", MaterialChipKind.Input, icon: Icon("M 12,12 A 5,5 0 1 0 11.99,12 Z M 4,21 A 8,8 0 0 1 20,21 Z"),
            removable: true, draggable: true));
        inputs.Items.Add(Chip("Carol", MaterialChipKind.Input, removable: true));
        inputs.Items.Add(Chip("Disabled", MaterialChipKind.Input, removable: true, isEnabled: false));
        panel.Children.Add(inputs);

        panel.Children.Add(Section("Chip groups · single row (scrollable) and multiple rows (overflow)"));
        panel.Children.Add(new TextBlock { Text = "Single row - scrollable, 360 DIP", FontSize = 14 });
        var singleRow = new MaterialChipGroup
        {
            Layout = MaterialChipGroupLayout.SingleRow,
            Width = 360,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        for (var i = 1; i <= 9; i++)
            singleRow.Items.Add(Chip($"Chip {i:00}", MaterialChipKind.Suggestion));
        panel.Children.Add(singleRow);

        panel.Children.Add(new TextBlock { Text = "Multiple rows - overflow, 360 DIP", FontSize = 14 });
        var multiRow = new MaterialChipGroup
        {
            Layout = MaterialChipGroupLayout.MultipleRows,
            Width = 360,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        for (var i = 1; i <= 9; i++)
            multiRow.Items.Add(Chip($"Chip {i:00}", MaterialChipKind.Suggestion));
        panel.Children.Add(multiRow);

        void Describe(MaterialChipGroup group, string name)
        {
            group.SelectionChanged += (_, e) =>
            {
                var selected = group.Items.OfType<MaterialChip>()
                    .Where(chip => chip.IsSelected)
                    .Select(chip => chip.Content?.ToString());
                status.Text = $"{name}: [{string.Join(", ", selected)}]";
            };
            group.ChipRemoved += (_, e) =>
            {
                status.Text = $"{name}: removed '{e.Chip.Content}'";
            };
        }

        Describe(filters, "Filter selection");
        Describe(suggestions, "Suggestion selection");
        Describe(inputs, "Input selection");
        panel.Children.Add(status);

        return new ScrollViewer
        {
            Content = panel,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
    }

    private static TextBlock Section(string text) => new()
    {
        Text = text,
        FontSize = 18,
        FontWeight = FontWeight.Medium,
        Margin = new Thickness(0, 8, 0, 0)
    };

    private static StackPanel Row() => new() { Orientation = Orientation.Horizontal, Spacing = 8 };

    private static MaterialChip Chip(string label, MaterialChipKind kind,
        MaterialChipVariant variant = MaterialChipVariant.Outlined,
        object? icon = null, object? avatar = null, bool showIcon = true,
        bool trailing = false, bool removable = false, bool isSelected = false,
        bool isEnabled = true, bool draggable = false)
    {
        var chip = new MaterialChip
        {
            Content = label,
            Kind = kind,
            Variant = variant,
            Icon = icon,
            Avatar = avatar,
            ShowIcon = showIcon,
            ShowTrailingIcon = trailing,
            IsRemovable = removable,
            IsSelected = isSelected,
            IsDraggable = draggable
        };
        chip.IsEnabled = isEnabled;
        AutomationProperties.SetName(chip, label);
        if (removable)
            chip.RemoveButtonAccessibleName = $"Remove {label}";
        return chip;
    }

    private static PathIcon Icon(string data) => new() { Data = Geometry.Parse(data), Width = 18, Height = 18 };

    private static Control BrandIcon()
    {
        var ring = new Border
        {
            Width = 18,
            Height = 18,
            CornerRadius = new CornerRadius(9),
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.Parse("#09ADEA"), 0),
                    new GradientStop(Color.Parse("#E6359F"), 0.5),
                    new GradientStop(Color.Parse("#F5A61D"), 1)
                }
            }
        };
        return ring;
    }

    private static Control Avatar(string initials)
    {
        var text = new TextBlock
        {
            Text = initials,
            FontSize = 12,
            FontWeight = FontWeight.Medium,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        text.Bind(TextBlock.ForegroundProperty, text.GetResourceObservable("MaterialOnTertiaryContainerBrush"));
        var avatar = new Border { Child = text };
        avatar.Bind(Border.BackgroundProperty, avatar.GetResourceObservable("MaterialTertiaryContainerBrush"));
        return avatar;
    }
}
