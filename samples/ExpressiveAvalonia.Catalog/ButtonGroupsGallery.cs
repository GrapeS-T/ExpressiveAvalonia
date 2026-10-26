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
    private static readonly string[] GlyphNames = { "star", "favorite", "share", "add", "edit", "more_horiz" };
    private static readonly string[] DefaultLabels = { "Звезда", "Избранное", "Поделиться", "Добавить", "Правка", "Ещё" };
    private static readonly string[] VariantNames = { "Filled", "Tonal", "Outlined", "Elevated", "Text" };

    // The enum starts at Standard, so the combo indices cannot be cast directly.
    private static readonly MaterialButtonVariant[] Variants =
    {
        MaterialButtonVariant.Filled,
        MaterialButtonVariant.Tonal,
        MaterialButtonVariant.Outlined,
        MaterialButtonVariant.Elevated,
        MaterialButtonVariant.Text,
    };

    private static readonly string[] SizeNames = { "ExtraSmall", "Small", "Medium", "Large", "ExtraLarge" };

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
    private readonly ComboBox _variant;
    private readonly ComboBox _size;
    private readonly ToggleSwitch _check;
    private readonly ToggleSwitch _replace;
    private readonly ToggleSwitch _enabled;
    private readonly ToggleSwitch _rtl;
    private readonly Slider _density;
    private readonly Slider _count;
    private readonly TextBox _labels;
    private readonly StackPanel _variantSettings = new() { Spacing = 16 };
    private readonly StackPanel _shapeSettings = new() { Spacing = 16 };
    private readonly StackPanel _selectionSettings = new() { Spacing = 16 };
    private readonly StackPanel _checkSettings = new() { Spacing = 16 };
    private readonly StackPanel _replaceSettings = new() { Spacing = 16 };
    private readonly StackPanel _densitySettings = new() { Spacing = 16 };
    private int _savedSelection;
    private bool _built;

    public StackPanel Settings { get; } = new() { Spacing = 16 };

    internal ButtonGroupPlayground()
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

        AutomationProperties.SetName(_group, "Группа кнопок");

        _kind = Choice(Settings, "Семейство", new[] { "Standard", "Connected", "Segmented" }, 0);
        _content = Choice(Settings, "Содержимое", new[] { "Текст", "Иконка", "Текст и иконка" }, 2);

        _variant = Field(_variantSettings, "Вид (Standard)", new ComboBox
        {
            ItemsSource = VariantNames,
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        });
        Settings.Children.Add(_variantSettings);

        _size = Field(Settings, "Размер", new ComboBox
        {
            ItemsSource = SizeNames,
            SelectedIndex = 2,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        });

        _shape = Choice(_shapeSettings, "Форма (Standard)", new[] { "Round", "Square" }, 0);
        Settings.Children.Add(_shapeSettings);

        _orientation = Choice(Settings, "Ориентация", new[] { "Горизонтально", "Вертикально" }, 0);

        _selection = Choice(_selectionSettings, "Выбор (Connected)", new[] { "Нет", "Один", "Несколько" }, 1);
        Settings.Children.Add(_selectionSettings);

        _check = Toggle(_checkSettings, "Галочка у выбранного", true);
        Settings.Children.Add(_checkSettings);

        _replace = Toggle(_replaceSettings, "Заменять иконку галочкой", true);
        Settings.Children.Add(_replaceSettings);

        _density = Number(_densitySettings, "Плотность (Segmented)", -3, 0, 0, 1);
        Settings.Children.Add(_densitySettings);

        _count = Number(Settings, "Количество элементов", 2, 6, 4, 1);
        _labels = Field(Settings, "Подписи через запятую", new TextBox
        {
            Text = string.Join(", ", DefaultLabels),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        });
        MaterialTextField.SetVariant(_labels, MaterialTextFieldVariant.Outlined);
        MaterialTextField.SetLabel(_labels, "Подписи");

        _enabled = Toggle(Settings, "Доступна", true);
        _rtl = Toggle(Settings, "Справа налево (RTL)", false);
        Settings.Children.Add(new TextBlock
        {
            Text = "Standard разделяет кнопки и не хранит выбор; Connected соединяет сегменты 2 dp и " +
                   "рисует выбранный сегмент пилюлей; Segmented делит общую рамку 1 dp, а плотность 0…-3 " +
                   "понижает высоту на 4 dp за шаг. Стрелки, Home/End и Space работают нативно: " +
                   "переключение вида, размера или формы сохраняет выбранный сегмент.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
        });

        AutomationProperties.SetName(_variant, "Вид группы кнопок");
        AutomationProperties.SetName(_size, "Размер группы кнопок");
        AutomationProperties.SetName(_labels, "Подписи элементов");

        foreach (var choice in new[] { _kind, _content, _shape, _selection, _orientation })
            choice.SelectionChanged += (_, _) => Apply();
        _variant.SelectionChanged += (_, _) => Apply();
        _size.SelectionChanged += (_, _) => Apply();
        foreach (var toggle in new[] { _check, _replace, _enabled, _rtl })
            toggle.IsCheckedChanged += (_, _) => Apply();
        foreach (var slider in new[] { _density, _count })
            slider.PropertyChanged += (_, e) => { if (e.Property == RangeBase.ValueProperty) Apply(); };
        _labels.TextChanged += (_, _) => Apply();
        _group.SelectionChanged += (_, e) =>
        {
            if (e.SelectedIndex >= 0) _savedSelection = e.SelectedIndex;
            UpdateReadout();
        };

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
        var mode = kind == MaterialButtonGroupKind.Standard
            ? MaterialButtonGroupSelectionMode.None
            : kind == MaterialButtonGroupKind.Segmented
                ? MaterialButtonGroupSelectionMode.Single
                : (MaterialButtonGroupSelectionMode)Math.Clamp(_selection.SelectedIndex, 0, 2);
        var vertical = _orientation.SelectedIndex == 1;
        var count = (int)Math.Round(_count.Value);
        var labels = ParseLabels(count);
        var showIcon = content != 0;
        var showText = content != 1;

        _variantSettings.IsVisible = kind == MaterialButtonGroupKind.Standard;
        _shapeSettings.IsVisible = kind == MaterialButtonGroupKind.Standard;
        _selectionSettings.IsVisible = kind == MaterialButtonGroupKind.Connected;
        _checkSettings.IsVisible = kind != MaterialButtonGroupKind.Standard;
        _densitySettings.IsVisible = kind == MaterialButtonGroupKind.Segmented;
        _replaceSettings.IsVisible = kind != MaterialButtonGroupKind.Standard &&
                                     _check.IsChecked == true && showIcon;

        _group.Kind = kind;
        _group.Variant = variant;
        _group.ControlSize = size;
        _group.ButtonShape = shape;
        _group.Orientation = vertical ? Orientation.Vertical : Orientation.Horizontal;
        _group.SelectionMode = mode;
        _group.ShowSelectedIcon = _check.IsChecked == true;
        _group.ReplaceIconWhenSelected = _replace.IsChecked == true;
        _group.Density = kind == MaterialButtonGroupKind.Segmented ? (int)Math.Round(_density.Value) : 0;
        _group.IsEnabled = _enabled.IsChecked == true;
        _group.FlowDirection = _rtl.IsChecked == true ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

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
            // The group writes a density-derived MinHeight for segmented items only; clearing the
            // local value lets the theme metrics return after switching to another family.
            if (_group.Kind != MaterialButtonGroupKind.Segmented)
                item.ClearValue(Layoutable.MinHeightProperty);
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
        // The group clears an out-of-range selection whenever items change, so the initial index
        // is applied only after every item exists.
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
