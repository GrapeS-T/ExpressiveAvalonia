using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Catalog;

public static class ListsGallery
{
    public static Control Create() => new ListsPlayground();
}

/// <summary>
/// One live list demo. The type selector covers the plain list, the segmented list, expandable
/// rows, swipe rows, pointer reordering and the accordion; density, line count, anatomy toggles
/// and the item count rebuild the same surface while selection and expansion are preserved.
/// </summary>
internal sealed class ListsPlayground : Grid, IComponentPlayground
{
    private static readonly Geometry Person = Geometry.Parse(
        "M12,12 A4,4 0 1 0 12,4 A4,4 0 1 0 12,12 Z M4,21 C4,17 7.6,14.5 12,14.5 C16.4,14.5 20,17 20,21 Z");
    private static readonly Geometry ChevronRight = Geometry.Parse("M9,5 L16,12 L9,19");
    private static readonly Geometry Archive = Geometry.Parse("M3,4 L21,4 L21,8 L3,8 Z M5,9 L19,9 L19,21 L5,21 Z M10,12 L14,12 L14,14 L10,14 Z");
    private static readonly Geometry Edit = Geometry.Parse("M4,16.5 L16.5,4 L20,7.5 L7.5,20 L4,20 Z M14,6.5 L17.5,10");
    private static readonly Geometry Delete = Geometry.Parse("M6,6 L18,6 L17,21 L7,21 Z M4,6 L20,6 M9,3 L15,3 L15,6 L9,6 Z");
    private static readonly Geometry DragHandle = Geometry.Parse("M8,5 L16,5 M8,12 L16,12 M8,19 L16,19");

    private static readonly string[] Names =
    [
        "Входящие", "Избранное", "Архив", "Корзина", "Черновики", "Отправленные", "Спам", "Метки",
    ];

    private readonly ListBox _list = new() { Width = 480, Padding = new Thickness(0, 4), MaxHeight = 500 };
    private readonly MaterialAccordion _accordion = new() { Width = 480 };
    private readonly ScrollViewer _accordionScroll;
    private readonly ContentControl _stage = new() { HorizontalAlignment = HorizontalAlignment.Center };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, MaxWidth = 480 };
    private readonly ComboBox _type;
    private readonly ComboBox _density;
    private readonly ComboBox _lines;
    private readonly ComboBox _selectionMode;
    private readonly ComboBox _swipeActions;
    private readonly ToggleSwitch _leading;
    private readonly ToggleSwitch _trailing;
    private readonly ToggleSwitch _counter;
    private readonly ToggleSwitch _badge;
    private readonly ToggleSwitch _overline;
    private readonly ToggleSwitch _supporting;
    private readonly ToggleSwitch _dividers;
    private readonly ToggleSwitch _multiple;
    private readonly Slider _count;
    private bool _initialized;
    private bool _accordionVisited;
    private bool _sync;

    public StackPanel Settings { get; } = new() { Spacing = 16 };

    internal ListsPlayground()
    {
        _accordionScroll = new ScrollViewer
        {
            Content = _accordion,
            MaxHeight = 500,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        MaterialTypography.SetStyle(_status, MaterialTypeStyle.BodyMedium);
        _status.Foreground = Brushes.Gray;

        var card = new Border
        {
            Padding = new Thickness(20),
            CornerRadius = new CornerRadius(28),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 560,
        };
        card.Bind(Border.BackgroundProperty, card.GetResourceObservable("MaterialSurfaceContainerLowBrush"));
        card.Bind(Border.BorderBrushProperty, card.GetResourceObservable("MaterialOutlineVariantBrush"));

        var inner = new StackPanel { Spacing = 12 };
        inner.Children.Add(_stage);
        inner.Children.Add(_status);
        card.Child = inner;

        var stage = new StackPanel
        {
            Spacing = 16,
            Margin = new Thickness(24),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        stage.Children.Add(card);
        Children.Add(stage);

        _type = Combo("Тип списка", ["Обычный", "Сегменты", "Раскрывающийся", "Свайп", "Перетаскивание", "Аккордеон"]);
        _count = Number("Количество элементов", 3, 8, 5, 1);
        _density = Combo("Плотность", ["0 (по умолчанию)", "-2", "-4"]);
        _lines = Combo("Строк в элементе", ["1", "2", "3"]);
        _lines.SelectedIndex = 1;
        _selectionMode = Combo("Выбор", ["Один", "Несколько", "Переключаемый"]);
        _swipeActions = Combo("Действия свайпа", ["1", "2", "3"]);
        _swipeActions.SelectedIndex = 2;
        _leading = Toggle("Ведущая иконка", true);
        _trailing = Toggle("Стрелка справа", true);
        _counter = Toggle("Счётчик справа", true);
        _badge = Toggle("Бейдж", false);
        _overline = Toggle("Надзаголовок", false);
        _supporting = Toggle("Описание", true);
        _dividers = Toggle("Разделители", true);
        _multiple = Toggle("Несколько раскрытых", false);

        _list.SelectionChanged += (_, _) => { if (!_sync) UpdateSelectionStatus(); };
        MaterialList.AddItemMovedHandler(_list, (_, e) =>
        {
            if (!_sync)
                _status.Text = $"Перемещено: {e.OldIndex} → {e.NewIndex}";
        });

        _type.SelectionChanged += (_, _) => { if (!_sync) Apply(); };
        foreach (var toggle in new[] { _leading, _trailing, _counter, _badge, _overline, _supporting, _dividers, _multiple })
            toggle.IsCheckedChanged += (_, _) => { if (!_sync) Apply(); };
        foreach (var combo in new[] { _density, _lines, _selectionMode, _swipeActions })
            combo.SelectionChanged += (_, _) => { if (!_sync) Apply(); };
        _count.PropertyChanged += (_, e) => { if (e.Property == RangeBase.ValueProperty && !_sync) Apply(); };

        Apply();
    }

    private void Apply()
    {
        var type = _type.SelectedIndex;
        var count = (int)Math.Round(_count.Value);
        var density = _density.SelectedIndex switch
        {
            1 => -2,
            2 => -4,
            _ => 0,
        };
        var lines = Math.Clamp(_lines.SelectedIndex + 1, 1, 3);
        var selected = CaptureSelection();
        var expanded = CaptureExpanded();
        var revealed = CaptureRevealed();
        var accordionExpanded = CaptureAccordionExpanded();

        _sync = true;
        try
        {
            _lines.IsVisible = type != 5;
            _selectionMode.IsVisible = type != 5;
            _swipeActions.IsVisible = type == 3;
            _multiple.IsVisible = type == 5;

            if (type == 5)
            {
                _stage.Content = _accordionScroll;
                BuildAccordion(count, density, accordionExpanded);
            }
            else
            {
                _stage.Content = _list;
                BuildList(type, count, density, lines, selected, expanded, revealed);
            }
        }
        finally
        {
            _sync = false;
        }

        if (type == 5)
        {
            _initialized = true;
            _status.Text = $"Аккордеон · {count} элементов";
            return;
        }

        if (!_initialized && _list.ItemCount > 0)
            _list.SelectedIndex = 0;
        _initialized = true;
        UpdateSelectionStatus();
    }

    private void BuildList(int type, int count, int density, int lines, int[] selected, int[] expanded, int[] revealed)
    {
        _list.Items.Clear();
        _list.SelectionMode = _selectionMode.SelectedIndex switch
        {
            1 => SelectionMode.Multiple,
            2 => SelectionMode.Toggle,
            _ => SelectionMode.Single,
        };
        MaterialList.SetSegmented(_list, type == 1);
        MaterialList.SetShowDividers(_list, _dividers.IsChecked == true);
        MaterialList.SetAllowItemDrag(_list, type == 4);

        for (var i = 0; i < count; i++)
        {
            ListBoxItem item;
            if (type == 3)
                item = CreateSwipeRow(i, density, lines);
            else
                item = CreateRow(i, density, lines, type == 2, type == 4);

            _list.Items.Add(item);
            if (type == 2 && expanded.Contains(i))
                MaterialList.SetIsExpanded(item, true);
            if (type == 3 && item is MaterialSwipeListItem swipe && revealed.Contains(i))
                swipe.SnapTo(MaterialSwipeState.EndToStart);
        }

        if (!_initialized && selected.Length == 0)
            selected = [0];

        if (_list.SelectionMode == SelectionMode.Multiple)
        {
            if (_list.SelectedItems is { } selection)
            {
                foreach (var index in selected)
                    if (index >= 0 && index < _list.ItemCount)
                        selection.Add(_list.Items[index]!);
            }
        }
        else if (selected.Length > 0)
        {
            _list.SelectedIndex = Math.Min(selected[0], _list.ItemCount - 1);
        }
    }

    private ListBoxItem CreateRow(int index, int density, int lines, bool expandable, bool drag)
    {
        var item = new ListBoxItem();
        ConfigureRow(item, index, density, lines);
        if (expandable)
        {
            // The accordion toggle owns the trailing slot; a decorative chevron would compete.
            MaterialList.SetTrailingContent(item, null);
            MaterialList.SetExpandedContent(item, Body(
                "Тело раскрывается штатной кнопкой списка; высота анимируется токеном medium-2, " +
                "а при reduced motion меняется сразу."));
        }
        if (drag)
            MaterialList.SetTrailingContent(item, new PathIcon { Data = DragHandle, Width = 24, Height = 24 });

        if (expandable)
        {
            item.PropertyChanged += (_, e) =>
            {
                if (_sync || e.Property != MaterialList.IsExpandedProperty)
                    return;
                _status.Text = (e.GetNewValue<bool>() ? "Раскрыто: " : "Свернуто: ") +
                               (MaterialList.GetHeadline(item) ?? "элемент");
            };
        }

        return item;
    }

    private MaterialSwipeListItem CreateSwipeRow(int index, int density, int lines)
    {
        var item = new MaterialSwipeListItem();
        ConfigureRow(item, index, density, lines);
        var actionCount = Math.Clamp(_swipeActions.SelectedIndex + 1, 1, 3);
        if (actionCount >= 1)
            item.SwipeActions.Add(new MaterialSwipeAction { Label = "Архив", Icon = Archive, IsPrimary = true });
        if (actionCount >= 2)
            item.SwipeActions.Add(new MaterialSwipeAction { Label = "Правка", Icon = Edit });
        if (actionCount >= 3)
            item.SwipeActions.Add(new MaterialSwipeAction
            {
                Label = "Удаление",
                Icon = Delete,
                Background = new SolidColorBrush(Color.Parse("#B3261E")),
                Foreground = Brushes.White,
            });
        item.ActionInvoked += (_, e) =>
        {
            if (!_sync)
                _status.Text = $"{e.Action.Label} · действие {e.Index} · " +
                               (e.BySwipe ? "свайп" : "меню или кнопка");
        };
        return item;
    }

    private void ConfigureRow(ListBoxItem item, int index, int density, int lines)
    {
        var headline = Names[index % Names.Length];
        var hasText = _supporting.IsChecked == true || _overline.IsChecked == true;
        MaterialList.SetHeadline(item, headline);
        MaterialList.SetLineCount(item, hasText ? lines : 1);
        if (_overline.IsChecked == true)
            MaterialList.SetOverline(item, "Категория " + (index + 1));
        if (_supporting.IsChecked == true)
            MaterialList.SetSupportingText(item, "Вспомогательный текст " + (index + 1));
        if (_leading.IsChecked == true)
            MaterialList.SetLeadingContent(item, new PathIcon { Data = Person, Width = 24, Height = 24 });
        if (_counter.IsChecked == true)
            MaterialList.SetTrailingSupportingText(item, "100+");
        if (_trailing.IsChecked == true)
            MaterialList.SetTrailingContent(item, new PathIcon { Data = ChevronRight, Width = 24, Height = 24 });
        if (_badge.IsChecked == true)
            MaterialList.SetBadgeText(item, "New");
        AutomationProperties.SetName(item, headline);
    }

    private void BuildAccordion(int count, int density, int[] expanded)
    {
        _accordion.Items.Clear();
        _accordion.IsMultipleExpanded = _multiple.IsChecked == true;
        for (var i = 0; i < count; i++)
        {
            var index = i;
            var item = new MaterialAccordionItem
            {
                Headline = Names[index % Names.Length],
                SupportingText = _supporting.IsChecked == true ? "Вспомогательный текст " + (index + 1) : null,
                Overline = _overline.IsChecked == true ? "Категория " + (index + 1) : null,
                LeadingContent = _leading.IsChecked == true
                    ? new PathIcon { Data = Person, Width = 24, Height = 24 }
                    : null,
                Body = Body("Тело раздела раскрывается кнопкой заголовка; Space и Enter работают нативно."),
                IsExpanded = expanded.Contains(index) || (!_accordionVisited && index == 0),
            };
            if (_badge.IsChecked == true)
                MaterialList.SetBadgeText(item, "New");
            item.PropertyChanged += (_, e) =>
            {
                if (_sync || e.Property != MaterialAccordionItem.IsExpandedProperty)
                    return;
                _status.Text = (e.GetNewValue<bool>() ? "Раскрыто: " : "Свернуто: ") +
                               (MaterialList.GetHeadline(item) ?? "раздел");
            };
            _accordion.Items.Add(item);
        }
        _accordionVisited = true;
    }

    private int[] CaptureSelection()
    {
        if (_list.SelectedItems is not { } selection)
            return [];

        var indices = new List<int>();
        foreach (var entry in selection)
        {
            var index = _list.Items.IndexOf(entry);
            if (index >= 0)
                indices.Add(index);
        }
        return indices.ToArray();
    }

    private int[] CaptureRevealed()
    {
        var indices = new List<int>();
        for (var i = 0; i < _list.ItemCount; i++)
            if (_list.Items[i] is MaterialSwipeListItem swipe && swipe.SwipeState != MaterialSwipeState.Settled)
                indices.Add(i);
        return indices.ToArray();
    }

    private int[] CaptureExpanded()
    {
        var indices = new List<int>();
        for (var i = 0; i < _list.ItemCount; i++)
            if (_list.Items[i] is ListBoxItem item && MaterialList.GetIsExpanded(item))
                indices.Add(i);
        return indices.ToArray();
    }

    private int[] CaptureAccordionExpanded()
    {
        var indices = new List<int>();
        for (var i = 0; i < _accordion.ItemCount; i++)
            if (_accordion.Items[i] is MaterialAccordionItem { IsExpanded: true })
                indices.Add(i);
        return indices.ToArray();
    }

    private void UpdateSelectionStatus()
    {
        if (_list.SelectedItems is not { } selection)
        {
            _status.Text = "Ничего не выбрано";
            return;
        }

        var labels = new List<string>();
        foreach (var entry in selection)
        {
            var index = _list.Items.IndexOf(entry);
            var headline = entry is Control control ? MaterialList.GetHeadline(control) : null;
            labels.Add(headline ?? $"Элемент {index + 1}");
        }

        _status.Text = labels.Count switch
        {
            0 => "Ничего не выбрано",
            1 => "Выбрано: " + labels[0],
            _ => $"Выбрано ({labels.Count}): " + string.Join(", ", labels),
        };
    }

    private Control Body(string text) => new Border
    {
        Padding = new Thickness(0, 4, 0, 8),
        Child = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.Gray,
        },
    };

    private ComboBox Combo(string label, string[] options)
    {
        Settings.Children.Add(new TextBlock { Text = label });
        var combo = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var option in options)
            combo.Items.Add(option);
        combo.SelectedIndex = 0;
        AutomationProperties.SetName(combo, label);
        Settings.Children.Add(combo);
        return combo;
    }

    private ToggleSwitch Toggle(string label, bool value)
    {
        var toggle = new ToggleSwitch { Content = label, IsChecked = value };
        Settings.Children.Add(toggle);
        return toggle;
    }

    private Slider Number(string label, double min, double max, double value, double step)
    {
        var caption = new TextBlock { Text = $"{label}: {value:0}" };
        Settings.Children.Add(caption);
        var slider = new Slider
        {
            Minimum = min,
            Maximum = max,
            Value = value,
            TickFrequency = step,
            IsSnapToTickEnabled = true,
        };
        AutomationProperties.SetName(slider, label);
        slider.PropertyChanged += (_, e) =>
        {
            if (e.Property == RangeBase.ValueProperty)
                caption.Text = $"{label}: {slider.Value:0}";
        };
        Settings.Children.Add(slider);
        return slider;
    }
}
