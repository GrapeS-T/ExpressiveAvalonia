using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Catalog;

public static class NavigationBarGallery
{
    public static Control Create() => new NavigationBarPlayground();
}

/// <summary>
/// One live navigation bar with horizontal/vertical destinations, a real destination strip,
/// badges, labels, disabled destinations and native keyboard selection.
/// </summary>
internal sealed class NavigationBarPlayground : Grid, IComponentPlayground
{
    private const string HomeIcon = "M12,3 L2,12 L5,12 L5,21 L10,21 L10,15 L14,15 L14,21 L19,21 L19,12 L22,12 Z";
    private const string SearchIcon = "M15.5,14 L14.71,14 L14.43,13.73 C15.41,12.59 16,11.11 16,9.5 C16,5.91 13.09,3 9.5,3 C5.91,3 3,5.91 3,9.5 C3,13.09 5.91,16 9.5,16 C11.11,16 12.59,15.41 13.73,14.43 L14,14.71 L14,15.5 L19,20.5 L20.5,19 Z M9.5,14 C7.01,14 5,11.99 5,9.5 C5,7.01 7.01,5 9.5,5 C11.99,5 14,7.01 14,9.5 C14,11.99 11.99,14 9.5,14 Z";
    private const string StarOutlineIcon = "M22,9.24 L14.81,8.63 L12,2 L9.19,8.63 L2,9.24 L7.46,13.97 L5.82,21 L12,17.27 L18.18,21 L16.55,13.97 Z M12,15.4 L8.24,17.67 L9.24,13.39 L5.92,10.51 L10.3,10.14 L12,6.1 L13.71,10.14 L18.09,10.51 L14.77,13.39 L15.77,17.67 Z";
    private const string StarFilledIcon = "M12,17.27 L18.18,21 L16.54,13.97 L22,9.24 L14.81,8.62 L12,2 L9.19,8.62 L2,9.24 L7.46,13.97 L5.82,21 Z";
    private const string MailIcon = "M20,4 L4,4 C2.9,4 2.01,4.9 2.01,6 L2,18 C2,19.1 2.9,20 4,20 L20,20 C21.1,20 22,19.1 22,18 L22,6 C22,4.9 21.1,4 20,4 Z M20,8 L12,13 L4,8 L4,6 L12,11 L20,6 Z";
    private const string PersonIcon = "M12,12 C14.21,12 16,10.21 16,8 C16,5.79 14.21,4 12,4 C9.79,4 8,5.79 8,8 C8,10.21 9.79,12 12,12 Z M12,14 C9.33,14 4,15.34 4,18 L4,20 L20,20 L20,18 C20,15.34 14.67,14 12,14 Z";
    private const string FolderIcon = "M4,20 C3.45,20 2.98,19.8 2.59,19.41 C2.2,19.02 2,18.55 2,18 L2,6 C2,5.45 2.2,4.98 2.59,4.59 C2.98,4.2 3.45,4 4,4 L10,4 L12,6 L20,6 C20.55,6 21.02,6.2 21.41,6.59 C21.8,6.98 22,7.45 22,8 L22,18 C22,18.55 21.8,19.02 21.41,19.41 C21.02,19.8 20.55,20 20,20 Z";

    private static readonly string[] Names = ["Главная", "Поиск", "Избранное", "Почта", "Профиль", "Архив"];
    private static readonly string[] Icons =
    [
        HomeIcon, SearchIcon, StarOutlineIcon, MailIcon, PersonIcon, FolderIcon,
    ];

    private readonly MaterialNavigationBar _bar = new();
    private readonly Border _card;
    private readonly DockPanel _dock = new() { LastChildFill = true };
    private readonly Border _page;
    private readonly TextBlock _pageTitle = new();
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly MaterialButtonGroup _axis;
    private readonly MaterialButtonGroup _itemOrientation;
    private readonly MaterialButtonGroup _badge;
    private readonly ToggleSwitch _labels;
    private readonly ToggleSwitch _disabledLast;
    private readonly Slider _count;
    private readonly TextBox _badgeText;
    private readonly ComboBox _selected;
    private bool _sync;

    public StackPanel Settings { get; } = new() { Spacing = 16 };

    internal NavigationBarPlayground()
    {
        var stage = new StackPanel
        {
            Spacing = 16,
            Margin = new Thickness(24),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        _pageTitle.FontSize = 24;
        MaterialTypography.SetStyle(_pageTitle, MaterialTypeStyle.HeadlineSmall);
        var body = new TextBlock
        {
            Text = "Содержимое выбранного пункта.",
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 360,
        };
        MaterialTypography.SetStyle(body, MaterialTypeStyle.BodyMedium);
        _status.FontSize = 14;

        var pageStack = new StackPanel { Spacing = 12 };
        pageStack.Children.Add(_pageTitle);
        pageStack.Children.Add(body);
        pageStack.Children.Add(_status);
        _page = new Border { Padding = new Thickness(24), Child = pageStack };

        _card = new Border
        {
            CornerRadius = new CornerRadius(28),
            ClipToBounds = true,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _card.Bind(Border.BackgroundProperty, _card.GetResourceObservable("MaterialSurfaceContainerLowBrush"));
        _card.BorderThickness = new Thickness(1);
        _card.Bind(Border.BorderBrushProperty, _card.GetResourceObservable("MaterialOutlineVariantBrush"));

        _dock.Children.Add(_bar);
        _dock.Children.Add(_page);
        _card.Child = _dock;
        stage.Children.Add(_card);
        Children.Add(stage);

        _axis = Choice("Ось полосы", ["Горизонтальная", "Вертикальная"]);
        _itemOrientation = Choice("Расположение пунктов", ["Авто", "В ряд", "В столбец"]);
        _count = Number("Количество пунктов", 3, 6, 4, 1);
        _labels = Toggle("Подписи пунктов", true);
        _badge = Choice("Badge «Избранное»", ["Нет", "Точка", "Число"]);
        _badgeText = new TextBox { Text = "3", HorizontalAlignment = HorizontalAlignment.Stretch };
        MaterialTextField.SetLabel(_badgeText, "Текст badge");
        MaterialTextField.SetVariant(_badgeText, MaterialTextFieldVariant.Outlined);
        AutomationProperties.SetName(_badgeText, "Текст badge");
        Settings.Children.Add(_badgeText);
        _disabledLast = Toggle("Последний пункт недоступен", false);

        Settings.Children.Add(new TextBlock { Text = "Выбранный пункт" });
        _selected = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        AutomationProperties.SetName(_selected, "Выбранный пункт");
        Settings.Children.Add(_selected);

        foreach (var group in new[] { _axis, _itemOrientation, _badge })
            group.SelectionChanged += (_, _) => Apply();
        foreach (var toggle in new[] { _labels, _disabledLast })
            toggle.IsCheckedChanged += (_, _) => Apply();
        _count.PropertyChanged += (_, e) => { if (e.Property == RangeBase.ValueProperty) Apply(); };
        _badgeText.TextChanged += (_, _) => Apply();
        _selected.SelectionChanged += (_, _) =>
        {
            if (_sync || _selected.SelectedIndex < 0 || _selected.SelectedIndex >= _bar.ItemCount)
                return;
            _bar.SelectedIndex = _selected.SelectedIndex;
        };

        _bar.SelectionChanged += (_, _) =>
        {
            if (_sync)
                return;
            SyncSelectedChoice();
            ShowSelection("Выбрано");
        };
        _bar.ItemInvoked += (_, e) =>
        {
            _status.Text = $"Открыто: {e.Item.Content} (индекс {e.Index})";
        };

        Apply();
    }

    private void Apply()
    {
        var vertical = _axis.SelectedIndex == 1;
        _sync = true;
        try
        {
            _bar.Orientation = vertical ? Orientation.Vertical : Orientation.Horizontal;
            _bar.ItemOrientation = _itemOrientation.SelectedIndex switch
            {
                1 => MaterialNavigationBarItemOrientation.Horizontal,
                2 => MaterialNavigationBarItemOrientation.Vertical,
                _ => MaterialNavigationBarItemOrientation.Auto,
            };
            DockPanel.SetDock(_bar, vertical ? Dock.Left : Dock.Bottom);
            _bar.Width = vertical ? 240 : double.NaN;
            _card.Width = vertical ? 620 : 560;
            _card.Height = 440;

            var count = (int)Math.Round(_count.Value);
            EnsureItems(count);
            ApplyItemProperties(count);
            _badgeText.IsVisible = _badge.SelectedIndex == 2;

            if (_bar.SelectedIndex < 0 || _bar.SelectedIndex >= count)
                _bar.SelectedIndex = 0;
            else if (_bar.Items[_bar.SelectedIndex] is MaterialNavigationBarItem { IsEnabled: false })
                _bar.SelectedIndex = 0;

            SyncSelectedChoice();
        }
        finally
        {
            _sync = false;
        }

        ShowSelection("Выбрано");
    }

    private void EnsureItems(int count)
    {
        while (_bar.Items.Count > count)
        {
            _bar.Items.RemoveAt(_bar.Items.Count - 1);
            _selected.Items.RemoveAt(_selected.Items.Count - 1);
        }
        while (_bar.Items.Count < count)
        {
            var index = _bar.Items.Count;
            _bar.Items.Add(new MaterialNavigationBarItem
            {
                Content = Names[index],
                Icon = Geometry.Parse(Icons[index]),
            });
            _selected.Items.Add(Names[index]);
        }
    }

    private void ApplyItemProperties(int count)
    {
        var badge = _badge.SelectedIndex switch
        {
            1 => MaterialNavigationBarBadge.Small,
            2 => MaterialNavigationBarBadge.Large,
            _ => MaterialNavigationBarBadge.None,
        };
        var showLabels = _labels.IsChecked == true;
        for (var i = 0; i < count; i++)
        {
            if (_bar.Items[i] is not MaterialNavigationBarItem item)
                continue;
            item.ShowLabel = showLabels;
            item.IsEnabled = !(i == count - 1 && _disabledLast.IsChecked == true);
            item.SelectedIcon = i == 2 ? Geometry.Parse(StarFilledIcon) : null;
            item.Badge = i == 2 ? badge : MaterialNavigationBarBadge.None;
            item.BadgeText = i == 2 ? _badgeText.Text : null;
        }
    }

    private void SyncSelectedChoice()
    {
        var index = Math.Clamp(_bar.SelectedIndex, 0, Math.Max(0, _selected.ItemCount - 1));
        if (index >= 0 && _selected.SelectedIndex != index)
            _selected.SelectedIndex = index;
    }

    private void ShowSelection(string prefix)
    {
        if (_bar.SelectedItem is MaterialNavigationBarItem item)
        {
            _pageTitle.Text = item.Content?.ToString() ?? "—";
            _status.Text = $"{prefix}: {item.Content} (индекс {_bar.SelectedIndex})";
        }
        else
        {
            _status.Text = "Ничего не выбрано";
        }
    }

    private MaterialButtonGroup Choice(string label, string[] options)
    {
        Settings.Children.Add(new TextBlock { Text = label });
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
        group.SelectedIndex = 0;
        Settings.Children.Add(group);
        return group;
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
