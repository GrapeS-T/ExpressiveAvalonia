using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Catalog;

/// <summary>A live chip group with its family, style axes and selection exposed in the inspector.</summary>
public static class ChipsGallery
{
    public static Control Create() => new ChipPlayground();
}

internal sealed class ChipPlayground : Grid, IComponentPlayground
{
    private const string Star = "M12,2 L15,8 L22,9 L17,14 L18,21 L12,18 L6,21 L7,14 L2,9 L9,8 Z";

    private static readonly string[] DefaultLabels = { "Чип 1", "Чип 2", "Чип 3", "Чип 4", "Чип 5", "Чип 6" };

    private readonly MaterialChipGroup _group = new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
    };
    private readonly TextBlock _readout = new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        FontSize = 16,
        TextWrapping = TextWrapping.Wrap,
        MaxWidth = 560,
    };
    private readonly Dictionary<MaterialChip, PathIcon> _icons = new();
    private readonly Dictionary<MaterialChip, Border> _avatars = new();
    private readonly ComboBox _kind;
    private readonly MaterialButtonGroup _variant;
    private readonly MaterialButtonGroup _selection;
    private readonly MaterialButtonGroup _layout;
    private readonly ToggleSwitch _icon;
    private readonly ToggleSwitch _avatar;
    private readonly ToggleSwitch _trailing;
    private readonly ToggleSwitch _remove;
    private readonly ToggleSwitch _enabled;
    private readonly ToggleSwitch _rtl;
    private readonly Slider _width;
    private readonly Slider _count;
    private readonly TextBox _labels;
    private readonly StackPanel _variantSettings = new() { Spacing = 16 };
    private readonly StackPanel _avatarSettings = new() { Spacing = 16 };
    private readonly StackPanel _trailingSettings = new() { Spacing = 16 };
    private readonly StackPanel _removeSettings = new() { Spacing = 16 };
    private readonly StackPanel _selectionSettings = new() { Spacing = 16 };
    private bool _built;
    private bool _syncing;

    public StackPanel Settings { get; } = new() { Spacing = 16 };

    internal ChipPlayground()
    {
        var stage = new StackPanel
        {
            Spacing = 24,
            Margin = new Thickness(24),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        stage.Children.Add(_group);
        stage.Children.Add(_readout);
        Children.Add(stage);

        AutomationProperties.SetName(_group, "Группа чипов");

        _kind = Field(Settings, "Семейство", new ComboBox
        {
            ItemsSource = new[] { "Assist", "Filter", "Input", "Suggestion" },
            SelectedIndex = 3,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        });
        AutomationProperties.SetName(_kind, "Семейство чипа");
        _variant = Choice(_variantSettings, "Вид", new[] { "Outlined", "Elevated" }, 0);
        Settings.Children.Add(_variantSettings);
        _icon = Toggle(Settings, "Ведущая иконка", true);

        _avatar = Toggle(_avatarSettings, "Аватар вместо иконки", false);
        Settings.Children.Add(_avatarSettings);

        _trailing = Toggle(_trailingSettings, "Трейлинг-иконка (Filter)", false);
        Settings.Children.Add(_trailingSettings);

        _remove = Toggle(_removeSettings, "Удаляемый чип (Input)", true);
        Settings.Children.Add(_removeSettings);

        _selection = Choice(_selectionSettings, "Режим выбора", new[] { "Нет", "Один", "Несколько" }, 2);
        Settings.Children.Add(_selectionSettings);

        _layout = Choice(Settings, "Раскладка", new[] { "Одна строка", "Несколько строк" }, 1);
        _width = Number(Settings, "Ширина группы", 200, 480, 320, 8);
        _count = Number(Settings, "Количество чипов", 0, 6, 3, 1);
        _labels = Field(Settings, "Подписи через запятую", new TextBox
        {
            Text = string.Join(", ", DefaultLabels.Take(3)),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        });
        MaterialTextField.SetVariant(_labels, MaterialTextFieldVariant.Outlined);
        MaterialTextField.SetLabel(_labels, "Подписи");
        _enabled = Toggle(Settings, "Доступны", true);
        _rtl = Toggle(Settings, "Справа налево (RTL)", false);
        Settings.Children.Add(new TextBlock
        {
            Text = "Assist только выполняет действие; Filter, Input и Suggestion переключают IsSelected. " +
                   "Tab входит в группу одним шагом, стрелки и Home/End двигают фокус, Space/Enter " +
                   "переключают, Delete или крестик удаляют Input-чип. Смена вида, иконки, ширины или " +
                   "раскладки не сбрасывает выбор и не пересоздаёт чипы; Assist не хранит выбор.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
        });

        AutomationProperties.SetName(_labels, "Подписи чипов");

        foreach (var choice in new[] { _variant, _selection, _layout })
            choice.SelectionChanged += (_, _) => Apply();
        _kind.SelectionChanged += (_, _) => Apply();
        foreach (var toggle in new[] { _icon, _avatar, _trailing, _remove, _enabled, _rtl })
            toggle.IsCheckedChanged += (_, _) => Apply();
        foreach (var slider in new[] { _width, _count })
            slider.PropertyChanged += (_, e) => { if (e.Property == RangeBase.ValueProperty) Apply(); };
        _labels.TextChanged += (_, _) => Apply();
        _group.SelectionChanged += (_, _) => UpdateReadout();
        _group.ChipRemoved += (_, e) =>
        {
            var label = e.Chip.Content as string ?? string.Empty;
            var index = _group.Items.IndexOf(e.Chip);
            _icons.Remove(e.Chip);
            _avatars.Remove(e.Chip);
            _syncing = true;
            var remaining = (_labels.Text ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
            if (index >= 0 && index < remaining.Count)
                remaining.RemoveAt(index);
            _labels.Text = string.Join(", ", remaining);
            _count.Value = Math.Max(0, _group.Items.Count - 1);
            _syncing = false;
            _readout.Text = $"Удалён чип «{label}» · выбрано: {SelectedText()}";
        };

        _built = true;
        Apply();
    }

    private void Apply()
    {
        if (!_built || _syncing) return;

        var kind = (MaterialChipKind)Math.Clamp(_kind.SelectedIndex, 0, 3);
        var variant = _variant.SelectedIndex == 1 ? MaterialChipVariant.Elevated : MaterialChipVariant.Outlined;
        var selectionMode = kind == MaterialChipKind.Assist
            ? MaterialChipSelectionMode.None
            : (MaterialChipSelectionMode)Math.Clamp(_selection.SelectedIndex, 0, 2);
        var layout = (MaterialChipGroupLayout)Math.Clamp(_layout.SelectedIndex, 0, 1);
        var count = (int)Math.Round(_count.Value);
        var labels = ParseLabels(count);
        var showIcon = _icon.IsChecked == true;
        var showAvatar = kind == MaterialChipKind.Input && _avatar.IsChecked == true;

        _variantSettings.IsVisible = kind != MaterialChipKind.Input;
        _avatarSettings.IsVisible = kind == MaterialChipKind.Input;
        _trailingSettings.IsVisible = kind == MaterialChipKind.Filter;
        _removeSettings.IsVisible = kind == MaterialChipKind.Input;
        _selectionSettings.IsVisible = kind != MaterialChipKind.Assist;

        _group.SelectionMode = selectionMode;
        _group.Layout = layout;
        _group.Width = _width.Value;
        _group.IsEnabled = _enabled.IsChecked == true;
        _group.FlowDirection = _rtl.IsChecked == true ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

        SyncChips(count, labels, kind, variant, showIcon, showAvatar);
        UpdateReadout();
    }

    private void SyncChips(
        int count,
        string[] labels,
        MaterialChipKind kind,
        MaterialChipVariant variant,
        bool showIcon,
        bool showAvatar)
    {
        while (_group.Items.Count > count)
        {
            var removed = (MaterialChip)_group.Items[_group.Items.Count - 1]!;
            _icons.Remove(removed);
            _avatars.Remove(removed);
            _group.Items.RemoveAt(_group.Items.Count - 1);
        }

        while (_group.Items.Count < count)
            AddChip();

        for (var i = 0; i < count; i++)
        {
            var chip = (MaterialChip)_group.Items[i]!;
            if (!_icons.TryGetValue(chip, out var icon))
            {
                icon = new PathIcon { Data = Geometry.Parse(Star), Width = 18, Height = 18 };
                _icons[chip] = icon;
            }
            if (!_avatars.TryGetValue(chip, out var avatar))
            {
                avatar = Avatar("A");
                _avatars[chip] = avatar;
            }
            chip.Content = labels[i];
            chip.Kind = kind;
            chip.Variant = variant;
            chip.Icon = showIcon ? icon : null;
            chip.Avatar = showAvatar ? avatar : null;
            chip.ShowIcon = showIcon;
            chip.ShowTrailingIcon = kind == MaterialChipKind.Filter && _trailing.IsChecked == true;
            chip.IsRemovable = kind == MaterialChipKind.Input && _remove.IsChecked == true;
            chip.RemoveButtonAccessibleName = $"Удалить {labels[i]}";
            AutomationProperties.SetName(chip, labels[i]);
        }
    }

    private void AddChip()
    {
        var chip = new MaterialChip();
        chip.Click += (_, _) =>
        {
            _readout.Text = $"{chip.Content}: нажат · выбрано: {SelectedText()}";
        };
        _group.Items.Add(chip);
    }

    private void UpdateReadout() => _readout.Text = $"Выбрано: {SelectedText()}";

    private string SelectedText()
    {
        var selected = _group.Items.OfType<MaterialChip>()
            .Where(chip => chip.IsSelected)
            .Select(chip => chip.Content?.ToString() ?? "чип")
            .ToList();
        return selected.Count == 0 ? "—" : string.Join(", ", selected);
    }

    private string[] ParseLabels(int count)
    {
        var parts = (_labels.Text ?? string.Empty).Split(',',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var labels = new string[count];
        for (var i = 0; i < count; i++)
            labels[i] = i < parts.Length ? parts[i] : DefaultLabels[i % DefaultLabels.Length];
        return labels;
    }

    private static Border Avatar(string initials)
    {
        var text = new TextBlock
        {
            Text = initials,
            FontSize = 12,
            FontWeight = FontWeight.Medium,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        text.Bind(TextBlock.ForegroundProperty, text.GetResourceObservable("MaterialOnTertiaryContainerBrush"));
        var avatar = new Border { Child = text };
        avatar.Bind(Border.BackgroundProperty, avatar.GetResourceObservable("MaterialTertiaryContainerBrush"));
        return avatar;
    }

    private static T Field<T>(StackPanel host, string label, T control) where T : Control
    {
        host.Children.Add(new TextBlock { Text = label });
        host.Children.Add(control);
        return control;
    }

    private static MaterialButtonGroup Choice(StackPanel host, string label, string[] options, int selected)
    {
        host.Children.Add(new TextBlock { Text = label });
        var group = new MaterialButtonGroup
        {
            Kind = MaterialButtonGroupKind.Connected,
            ButtonShape = MaterialButtonShape.Round,
            ControlSize = MaterialControlSize.Small,
            SelectionMode = MaterialButtonGroupSelectionMode.Single,
            ShowSelectedIcon = true,
        };
        foreach (var option in options)
            group.Items.Add(new MaterialButtonGroupItem { Content = option });
        group.SelectedIndex = selected;
        host.Children.Add(group);
        return group;
    }

    private static ToggleSwitch Toggle(StackPanel host, string label, bool value)
    {
        var toggle = new ToggleSwitch { Content = label, IsChecked = value };
        host.Children.Add(toggle);
        return toggle;
    }

    private static Slider Number(
        StackPanel host, string label, double min, double max, double value, double step)
    {
        var caption = new TextBlock { Text = $"{label}: {value:0}" };
        host.Children.Add(caption);
        var slider = new Slider
        {
            Minimum = min,
            Maximum = max,
            Value = value,
            TickFrequency = step,
            IsSnapToTickEnabled = true,
        };
        MaterialAssist.SetControlSize(slider, MaterialControlSize.ExtraSmall);
        MaterialSlider.SetShowStops(slider, false);
        AutomationProperties.SetName(slider, label);
        slider.PropertyChanged += (_, e) =>
        {
            if (e.Property == RangeBase.ValueProperty) caption.Text = $"{label}: {slider.Value:0}";
        };
        host.Children.Add(slider);
        return slider;
    }
}
