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
    private readonly ToggleSwitch _draggable;
    private readonly ToggleSwitch _enabled;
    private readonly ToggleSwitch _rtl;
    private readonly Slider _width;
    private readonly Slider _count;
    private readonly TextBox _labels;
    private readonly HashSet<int> _savedSelection = new();
    private bool _wasSelectionEnabled;
    private bool _built;
    private bool _syncing;

    public StackPanel Settings { get; } = InspectorLayout.CreatePane();

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

        var variantSection = InspectorLayout.Section(Settings, "Вариант", divider: false);
        _kind = InspectorLayout.Combo(variantSection, "Семейство", new[] { "Assist", "Filter", "Input", "Suggestion" }, 3);
        AutomationProperties.SetName(_kind, "Семейство чипа");
        _variant = InspectorLayout.Choice(variantSection, "Вид", new[] { "Outlined", "Elevated" }, 0);
        var anatomySwitches = InspectorLayout.Switches(variantSection);
        _icon = InspectorLayout.Switch(anatomySwitches, "Ведущая иконка", true);
        _avatar = InspectorLayout.Switch(anatomySwitches, "Аватар вместо иконки", false);
        _trailing = InspectorLayout.Switch(anatomySwitches, "Трейлинг-иконка (Filter)", false);
        _remove = InspectorLayout.Switch(anatomySwitches, "Удаляемый чип (Input)", true);

        var layoutSection = InspectorLayout.Section(Settings, "Выбор и раскладка");
        _selection = InspectorLayout.Choice(layoutSection, "Режим выбора", new[] { "Нет", "Один", "Несколько" }, 2);
        _layout = InspectorLayout.Choice(layoutSection, "Раскладка", new[] { "Одна строка", "Несколько строк" }, 1);
        _width = InspectorLayout.Slider(layoutSection, "Ширина группы", 200, 480, 320, 8, "dp", "0");

        var contentSection = InspectorLayout.Section(Settings, "Содержимое");
        _count = InspectorLayout.Slider(contentSection, "Количество чипов", 0, 6, 3, 1, format: "0");
        _labels = InspectorLayout.TextField(contentSection, "Подписи через запятую", string.Join(", ", DefaultLabels.Take(3)));
        AutomationProperties.SetName(_labels, "Подписи чипов");

        var statesSection = InspectorLayout.Section(Settings, "Состояния");
        var stateSwitches = InspectorLayout.Switches(statesSection);
        _draggable = InspectorLayout.Switch(stateSwitches, "Перетаскивание (IsDraggable)", false);
        _enabled = InspectorLayout.Switch(stateSwitches, "Доступны", true);
        _rtl = InspectorLayout.Switch(stateSwitches, "Справа налево (RTL)", false);

        InspectorLayout.Note(
            Settings,
            "Assist и Suggestion только выполняют действие; Filter и Input переключают IsSelected. " +
            "Tab входит в группу одним шагом, стрелки и Home/End двигают фокус, Space/Enter " +
            "переключают, Delete или крестик удаляют Input-чип. Смена семейства, вида, иконки, " +
            "ширины или раскладки сохраняет выбор и не пересоздаёт чипы.");

        foreach (var choice in new[] { _variant, _selection, _layout })
            choice.SelectionChanged += (_, _) => Apply();
        _kind.SelectionChanged += (_, _) => Apply();
        foreach (var toggle in new[] { _icon, _avatar, _trailing, _remove, _draggable, _enabled, _rtl })
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
            if (index >= 0)
            {
                var adjusted = _savedSelection
                    .Where(i => i != index)
                    .Select(i => i > index ? i - 1 : i)
                    .ToList();
                _savedSelection.Clear();
                foreach (var i in adjusted)
                    _savedSelection.Add(i);
            }
            _syncing = true;
            var remaining = _group.Items.OfType<MaterialChip>()
                .Where(chip => !ReferenceEquals(chip, e.Chip))
                .Select(chip => chip.Content?.ToString() ?? string.Empty)
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .ToList();
            _labels.Text = string.Join(", ", remaining);
            _count.Value = remaining.Count;
            _syncing = false;
            var selected = _group.Items.OfType<MaterialChip>()
                .Where(chip => !ReferenceEquals(chip, e.Chip) && chip.IsSelected)
                .Select(chip => chip.Content?.ToString() ?? "чип")
                .ToList();
            var selectedText = selected.Count == 0 ? "—" : string.Join(", ", selected);
            _readout.Text = $"Удалён чип «{label}» · выбрано: {selectedText}";
        };

        _built = true;
        Apply();
    }

    private void Apply()
    {
        if (!_built || _syncing) return;

        var kind = (MaterialChipKind)Math.Clamp(_kind.SelectedIndex, 0, 3);
        var noneOption = (MaterialButtonGroupItem)_selection.Items[0]!;
        noneOption.IsVisible = kind == MaterialChipKind.Input;
        if (kind == MaterialChipKind.Filter && _selection.SelectedIndex == 0)
        {
            _syncing = true;
            _selection.SelectedIndex = 2;
            _syncing = false;
        }

        var variant = _variant.SelectedIndex == 1 ? MaterialChipVariant.Elevated : MaterialChipVariant.Outlined;
        var selectable = kind is MaterialChipKind.Filter or MaterialChipKind.Input;
        var selectionMode = selectable
            ? (MaterialChipSelectionMode)Math.Clamp(_selection.SelectedIndex, kind == MaterialChipKind.Filter ? 1 : 0, 2)
            : MaterialChipSelectionMode.None;
        var selectionEnabled = selectable && selectionMode != MaterialChipSelectionMode.None;
        if (_wasSelectionEnabled && !selectionEnabled)
        {
            _savedSelection.Clear();
            for (var i = 0; i < _group.Items.Count; i++)
            {
                if (_group.Items[i] is MaterialChip { IsSelected: true })
                    _savedSelection.Add(i);
            }
        }

        var layout = (MaterialChipGroupLayout)Math.Clamp(_layout.SelectedIndex, 0, 1);
        var count = (int)Math.Round(_count.Value);
        var labels = ParseLabels(count);
        var showIcon = _icon.IsChecked == true;
        var showAvatar = kind == MaterialChipKind.Input && _avatar.IsChecked == true;

        _variant.IsVisible = kind != MaterialChipKind.Input;
        _avatar.IsVisible = kind == MaterialChipKind.Input;
        _trailing.IsVisible = kind == MaterialChipKind.Filter;
        _remove.IsVisible = kind == MaterialChipKind.Input;
        _selection.IsVisible = selectable;

        _group.SelectionMode = selectionMode;
        _group.Layout = layout;
        _group.Width = _width.Value;
        _group.IsEnabled = _enabled.IsChecked == true;
        InspectorLayout.ApplyFlowDirection(_group, _rtl);

        SyncChips(count, labels, kind, variant, showIcon, showAvatar);
        if (!_wasSelectionEnabled && selectionEnabled && _savedSelection.Count > 0)
        {
            foreach (var index in _savedSelection.OrderBy(i => i))
            {
                if (index >= 0 && index < _group.Items.Count && _group.Items[index] is MaterialChip chip)
                {
                    _group.SelectChip(chip, true);
                    if (selectionMode == MaterialChipSelectionMode.Single)
                        break;
                }
            }
        }
        _wasSelectionEnabled = selectionEnabled;
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

        var draggable = _draggable.IsChecked == true;
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
            chip.IsDraggable = draggable;
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
}
