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

/// <summary>A live button group with its anatomy and selection exposed in the inspector.</summary>
public static class ButtonGroupsGallery
{
    public static Control Create() => new ButtonGroupPlayground();
}

internal sealed class ButtonGroupPlayground : Grid, IComponentPlayground
{
    // The theme loads no stock control theme, so PathIcon has no template and paints nothing.
    // MaterialIcon (used by the Buttons page) has its own template and is the glyph carrier here.
    private static readonly string[] GlyphNames = ["star", "favorite", "share", "add", "edit", "more_horiz"];
    private static readonly string[] DefaultLabels = ["Звезда", "Избранное", "Поделиться", "Добавить", "Правка", "Ещё"];
    private static readonly string[] VariantNames = ["Filled", "Tonal", "Outlined", "Elevated", "Text"];

    // The enum starts at Standard, so the combo indices cannot be cast directly.
    private static readonly MaterialButtonVariant[] Variants =
    [
        MaterialButtonVariant.Filled,
        MaterialButtonVariant.Tonal,
        MaterialButtonVariant.Outlined,
        MaterialButtonVariant.Elevated,
        MaterialButtonVariant.Text,
    ];

    private static readonly string[] SizeNames = ["ExtraSmall", "Small", "Medium", "Large", "ExtraLarge"];

    private readonly MaterialButtonGroup _group = new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
    };
    private readonly TextBlock _readout = new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        FontSize = 16,
        TextWrapping = TextWrapping.Wrap,
        MaxWidth = 640,
    };
    private readonly List<MaterialIcon> _icons = new();
    private readonly MaterialButtonGroup _kind;
    private readonly MaterialButtonGroup _content;
    private readonly MaterialButtonGroup _shape;
    private readonly MaterialButtonGroup _selection;
    private readonly MaterialButtonGroup _orientation;
    private readonly MaterialSplitButton _split = new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        Content = "Сохранить",
    };
    private readonly MaterialSplitButton _splitToggle = new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        Content = "В избранное",
        IsTrailingCheckable = true,
    };
    private readonly MenuFlyout _splitMenu = new();
    private readonly ComboBox _variant;
    private readonly ComboBox _size;
    private readonly StackPanel _selectionSwitches;
    private readonly ToggleSwitch _check;
    private readonly ToggleSwitch _replace;
    private readonly ToggleSwitch _enabled;
    private readonly ToggleSwitch _rtl;
    private readonly ToggleSwitch _splitPrimaryEnabled;
    private readonly ToggleSwitch _splitTrailingEnabled;
    private readonly Slider _density;
    private readonly Slider _count;
    private readonly TextBox _labels;
    private int _savedSelection;
    private bool _built;
    private bool _syncing;

    public StackPanel Settings { get; } = InspectorLayout.CreatePane();

    internal ButtonGroupPlayground()
    {
        var stage = new StackPanel
        {
            Spacing = 24,
            Margin = new Thickness(24),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
        };
        stage.Children.Add(_group);
        stage.Children.Add(_readout);

        // Split button anatomy: two independent halves, a real MenuFlyout on the trailing half,
        // hover/press seam-corner morphs and RTL mirroring.
        var splitIcon = new MaterialIcon { IconName = "share", IsDecorative = true };
        _split.Icon = splitIcon;
        _split.Flyout = _splitMenu;
        _split.Click += (_, _) => _readout.Text = "Split: выполнено ведущее действие";
        foreach (var label in new[] { "Экспорт", "В архив" })
        {
            var menuItem = new MenuItem { Header = label };
            menuItem.Click += (_, _) => _readout.Text = $"Split: меню «{label}»";
            _splitMenu.Items.Add(menuItem);
        }
        stage.Children.Add(_split);

        // Checkable trailing variant without a popup: native activation toggles the trailing half,
        // morphs it to the full-round checked shape and paints the 10% content-colored layer.
        var toggleIcon = new MaterialIcon { IconName = "star", IsDecorative = true };
        _splitToggle.Icon = toggleIcon;
        _splitToggle.TrailingCheckedChanged += (_, _) =>
        {
            _readout.Text = _splitToggle.IsTrailingChecked
                ? "Split: дополнительное действие включено"
                : "Split: дополнительное действие выключено";
        };
        stage.Children.Add(_splitToggle);

        Children.Add(stage);

        AutomationProperties.SetName(_group, "Группа кнопок");
        AutomationProperties.SetName(_split, "Кнопка с меню");
        AutomationProperties.SetName(_splitToggle, "Кнопка с переключаемым действием");
        AutomationProperties.SetName(splitIcon, "Действие");
        AutomationProperties.SetName(toggleIcon, "Избранное");

        var configSection = InspectorLayout.Section(Settings, "Конфигурация", divider: false);
        _kind = InspectorLayout.Choice(configSection, "Семейство", ["Standard", "Connected", "Segmented"], 0);
        _variant = InspectorLayout.Combo(configSection, "Вид", VariantNames, 0);
        _size = InspectorLayout.Combo(configSection, "Размер", SizeNames, 2);
        _shape = InspectorLayout.Choice(configSection, "Форма", ["Round", "Square"], 0);
        _orientation = InspectorLayout.Choice(configSection, "Ориентация", ["Горизонтально", "Вертикально"], 0);
        _density = InspectorLayout.Slider(configSection, "Плотность", -3, 0, 0, 1, format: "0");

        var selectionSection = InspectorLayout.Section(Settings, "Выбор и анатомия");
        _content = InspectorLayout.Choice(selectionSection, "Содержимое", ["Текст", "Иконка", "Оба"], 2);
        _selection = InspectorLayout.Choice(selectionSection, "Выбор", ["Нет", "Один", "Несколько"], 1);
        _selectionSwitches = InspectorLayout.Switches(selectionSection);
        _check = InspectorLayout.Switch(_selectionSwitches, "Галочка у выбранного", true);
        _replace = InspectorLayout.Switch(_selectionSwitches, "Заменять иконку галочкой", true);

        var itemsSection = InspectorLayout.Section(Settings, "Элементы");
        _count = InspectorLayout.Slider(itemsSection, "Количество элементов", 2, 6, 4, 1, format: "0");
        _labels = InspectorLayout.TextField(itemsSection, "Подписи через запятую", string.Join(", ", DefaultLabels));

        var stateSection = InspectorLayout.Section(Settings, "Состояния");
        var stateSwitches = InspectorLayout.Switches(stateSection);
        _enabled = InspectorLayout.Switch(stateSwitches, "Доступна", true);
        _rtl = InspectorLayout.Switch(stateSwitches, "Справа налево (RTL)", false);
        _splitPrimaryEnabled = InspectorLayout.Switch(stateSwitches, "Split: ведущее действие доступно", true);
        _splitTrailingEnabled = InspectorLayout.Switch(stateSwitches, "Split: действие меню доступно", true);

        InspectorLayout.Note(
            Settings,
            "Standard расширяет нажатую кнопку за счёт соседних; поддерживает действия и выбор. " +
            "Connected заполняет доступную ширину, разделяет кнопки промежутком 2 dp и " +
            "меняет форму только нажатой кнопки. Оба вида поддерживают Round/Square и цвета кнопок. " +
            "Segmented — прежний вариант с общей рамкой. Стрелки, Home/End и Space работают нативно. " +
            "Split без меню переключает trailing-половину: checked даёт пилюлю и слой 10%, " +
            "а команда видит уже новое состояние.");

        AutomationProperties.SetName(_variant, "Вид группы кнопок");
        AutomationProperties.SetName(_size, "Размер группы кнопок");
        AutomationProperties.SetName(_labels, "Подписи элементов");

        foreach (var choice in new[] { _kind, _content, _shape, _selection, _orientation })
            choice.SelectionChanged += (_, _) => { if (!_syncing) Apply(); };
        _variant.SelectionChanged += (_, _) => { if (!_syncing) Apply(); };
        _size.SelectionChanged += (_, _) => { if (!_syncing) Apply(); };
        foreach (var toggle in new[]
                 {
                     _check, _replace, _enabled, _rtl,
                     _splitPrimaryEnabled, _splitTrailingEnabled,
                 })
            toggle.IsCheckedChanged += (_, _) => { if (!_syncing) Apply(); };
        foreach (var slider in new[] { _density, _count })
            slider.PropertyChanged += (_, e) => { if (e.Property == RangeBase.ValueProperty && !_syncing) Apply(); };
        _labels.TextChanged += (_, _) => { if (!_syncing) Apply(); };
        _group.SelectionChanged += (_, e) =>
        {
            if (e.SelectedIndex >= 0) _savedSelection = e.SelectedIndex;
            UpdateReadout();
        };
        _group.AttachedToVisualTree += (_, _) =>
            Avalonia.Threading.Dispatcher.UIThread.Post(UpdateReadout,
                Avalonia.Threading.DispatcherPriority.Loaded);

        _built = true;
        Apply();
    }

    private void Apply()
    {
        if (!_built) return;

        var kind = (MaterialButtonGroupKind)Math.Clamp(_kind.SelectedIndex, 0, 2);
        var content = Math.Clamp(_content.SelectedIndex, 0, 2);
        var variant = Variants[Math.Clamp(_variant.SelectedIndex, 0, Variants.Length - 1)];
        var size = (MaterialControlSize)Math.Clamp(_size.SelectedIndex, 0, 4);
        var shape = _shape.SelectedIndex == 1 ? MaterialButtonShape.Square : MaterialButtonShape.Round;
        if (_selection.Items[0] is MaterialButtonGroupItem noneOption)
            noneOption.IsVisible = kind != MaterialButtonGroupKind.Segmented;
        if (kind == MaterialButtonGroupKind.Segmented && _selection.SelectedIndex == 0)
        {
            _syncing = true;
            _selection.SelectedIndex = 1;
            _syncing = false;
        }
        var mode = (MaterialButtonGroupSelectionMode)Math.Clamp(
            _selection.SelectedIndex,
            kind == MaterialButtonGroupKind.Segmented ? 1 : 0,
            2);
        var vertical = _orientation.SelectedIndex == 1;
        var maxCount = kind == MaterialButtonGroupKind.Segmented ? 5 : 6;
        if (Math.Abs(_count.Maximum - maxCount) > 0.01)
        {
            _syncing = true;
            if (_count.Value > maxCount)
                _count.Value = maxCount;
            _count.Maximum = maxCount;
            _syncing = false;
        }
        var count = Math.Clamp((int)Math.Round(_count.Value), 2, maxCount);
        var labels = ParseLabels(count);
        var showIcon = content != 0;
        var showText = content != 1;

        _variant.IsVisible = kind != MaterialButtonGroupKind.Segmented;
        _shape.IsVisible = kind != MaterialButtonGroupKind.Segmented;
        var hasSelection = mode != MaterialButtonGroupSelectionMode.None;
        _selectionSwitches.IsVisible = hasSelection;
        _check.IsVisible = hasSelection;
        _replace.IsVisible = hasSelection && _check.IsChecked == true && showIcon;

        _group.Kind = kind;
        _group.Variant = variant;
        _group.ControlSize = size;
        _group.ButtonShape = shape;
        _group.Orientation = vertical ? Orientation.Vertical : Orientation.Horizontal;
        _group.HorizontalAlignment = kind == MaterialButtonGroupKind.Connected && !vertical
            ? HorizontalAlignment.Stretch : HorizontalAlignment.Center;
        _group.SelectionMode = mode;
        _group.ShowSelectedIcon = _check.IsChecked == true;
        _group.ReplaceIconWhenSelected = _replace.IsChecked == true;
        _group.Density = (int)Math.Round(_density.Value);
        _group.IsEnabled = _enabled.IsChecked == true;
        InspectorLayout.ApplyFlowDirection(_group, _rtl);

        InspectorLayout.ApplyFlowDirection(_split, _rtl);
        _split.IsPrimaryEnabled = _splitPrimaryEnabled.IsChecked == true;
        _split.IsTrailingEnabled = _splitTrailingEnabled.IsChecked == true;

        // The checkable variant shares the RTL and segment-enabled controls so both splits show
        // the same mirrored seam and disabled composition; it deliberately has no popup.
        InspectorLayout.ApplyFlowDirection(_splitToggle, _rtl);
        _splitToggle.IsPrimaryEnabled = _split.IsPrimaryEnabled;
        _splitToggle.IsTrailingEnabled = _split.IsTrailingEnabled;

        SyncItems(count, labels, showText, showIcon);
        RestoreSelection(mode, count);
        UpdateReadout();
    }

    private void SyncItems(int count, string[] labels, bool showText, bool showIcon)
    {
        while (_group.Items.Count > count)
        {
            _group.Items.RemoveAt(_group.Items.Count - 1);
            _icons.RemoveAt(_icons.Count - 1);
        }

        while (_group.Items.Count < count)
        {
            var index = _group.Items.Count;
            var item = new MaterialButtonGroupItem();
            // The glyph follows the item's variant/checked/disabled foreground, so it is bound to
            // the owner instead of keeping MaterialIcon's own on-surface default.
            var icon = new MaterialIcon
            {
                IconName = GlyphNames[index % GlyphNames.Length],
                IsDecorative = true,
            };
            icon.Bind(TemplatedControl.ForegroundProperty,
                item.GetObservable(TemplatedControl.ForegroundProperty));
            item.Click += (_, _) =>
            {
                _readout.Text = $"Нажато: {ItemText(item)} · выбрано: {SelectedText()}";
            };
            _icons.Add(icon);
            _group.Items.Add(item);
        }

        for (var i = 0; i < count; i++)
        {
            var item = (MaterialButtonGroupItem)_group.Items[i]!;
            item.Content = showText ? labels[i] : null;
            item.Icon = showIcon ? _icons[i] : null;
            ToolTip.SetTip(item, labels[i]);
            AutomationProperties.SetName(item, showText ? labels[i] : $"Кнопка {i + 1}");
        }
    }

    private void RestoreSelection(MaterialButtonGroupSelectionMode mode, int count)
    {
        if (count == 0) return;

        if (mode == MaterialButtonGroupSelectionMode.None)
        {
            _group.SelectedIndex = -1;
            return;
        }

        var desired = Math.Clamp(_savedSelection, 0, count - 1);
        if (mode == MaterialButtonGroupSelectionMode.Multiple)
        {
            if (_group.SelectedIndex < 0) _group.SelectedIndex = desired;
            return;
        }

        var checkedCount = _group.Items.OfType<MaterialButtonGroupItem>().Count(item => item.IsChecked == true);
        if (checkedCount == 1 && _group.SelectedIndex >= 0) return;

        // Single selection never empties through interaction, so clear and reapply the remembered
        // index; this also collapses a multiple selection left behind by the previous mode.
        _group.SelectedIndex = -1;
        _group.SelectedIndex = desired;
    }

    private void UpdateReadout()
    {
        var selected = SelectedText();
        if (selected == "—")
        {
            _readout.Text = _group.Kind == MaterialButtonGroupKind.Standard
                ? "Нажмите любую кнопку группы"
                : "Ничего не выбрано";
            return;
        }

        _readout.Text = $"Выбрано: {selected}";
    }

    private string SelectedText()
    {
        var selected = _group.Items.OfType<MaterialButtonGroupItem>()
            .Where(item => item.IsChecked == true)
            .Select(ItemText)
            .ToList();
        return selected.Count == 0 ? "—" : string.Join(", ", selected);
    }

    private static string ItemText(MaterialButtonGroupItem item) =>
        item.Content as string ?? "иконка";

    private string[] ParseLabels(int count)
    {
        var parts = (_labels.Text ?? string.Empty).Split(',',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var labels = new string[count];
        for (var i = 0; i < count; i++)
            labels[i] = i < parts.Length ? parts[i] : DefaultLabels[i % DefaultLabels.Length];
        return labels;
    }
}
