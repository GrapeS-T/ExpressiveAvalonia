using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ExpressiveAvalonia.Controls;

namespace ExpressiveAvalonia.Catalog;

public static class NavigationRailGallery
{
    public static Control Create() => new NavigationRailPlayground();
}

internal sealed class NavigationRailPlayground : Grid, IComponentPlayground
{
    private readonly MaterialNavigationRail _rail = new() { IsExpanded = false };
    private readonly MaterialNavigationRail _collapsedRail = new()
    {
        Mode = MaterialNavigationRailMode.Wide,
        IsExpanded = false
    };
    private readonly MaterialNavigationRailHost _host;
    private readonly Border _surface;
    private readonly Grid _inline = new() { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
    private readonly Border _body;
    private readonly ContentControl _stage = new();
    private readonly TextBlock _status = new() { Text = "Выбрано: Главная", TextWrapping = TextWrapping.Wrap };
    private readonly MaterialButtonGroup _kind, _placement, _alignment, _badge;
    private readonly ToggleSwitch _labels, _menu, _fab, _header, _disabledLast, _enabled, _rtl, _dismiss, _closeOnSelection, _hideOnCollapse;
    private readonly Slider _count, _height;
    private readonly TextBox _fabText;
    private readonly TextBlock _headerContent = new() { Text = "Почта" };
    private readonly Button _open = new() { Content = "Открыть rail" };
    private readonly string[] _names = ["Главная", "Поиск", "Избранное", "Сообщения", "Архив", "Настройки"];
    private readonly string[] _icons = [
        "M12,3 L2,12 L5,12 L5,21 L10,21 L10,15 L14,15 L14,21 L19,21 L19,12 L22,12 Z",
        "M10,2 A8,8 0 1 1 4.34,15.66 A8,8 0 0 1 10,2 Z M10,4 A6,6 0 1 0 10,16 A6,6 0 0 0 10,4 Z M15,14 L22,21 L20,23 L13,16 Z",
        "M12,2 L15,8 L22,9 L17,14 L18,21 L12,18 L6,21 L7,14 L2,9 L9,8 Z",
        "M2,4 L22,4 L22,20 L2,20 Z M4,6 L12,12 L20,6 L20,8 L12,14 L4,8 Z",
        "M3,5 L21,5 L21,21 L3,21 Z M5,2 L19,2 L21,4 L3,4 Z M9,9 L15,9 L15,11 L9,11 Z",
        "M3,10 L21,10 L21,14 L3,14 Z M10,3 L14,3 L14,21 L10,21 Z"];
    public StackPanel Settings { get; } = new() { Spacing = 16 };

    internal NavigationRailPlayground()
    {
        _kind = Choice("Представление", ["Compact", "Expanded", "Modal"]);
        _placement = Choice("Поверхность", ["Docked", "Floating"]);
        _alignment = Choice("Расположение пунктов", ["Top", "Middle"]);
        _count = Number("Количество пунктов", 3, 6, 4, 1);
        _height = Number("Высота", 400, 720, 520, 20);
        _labels = Toggle("Подписи пунктов", true);
        _badge = Choice("Badge", ["Нет", "Точка", "Число"]);
        _menu = Toggle("Кнопка меню", true);
        _fab = Toggle("FAB", true);
        _header = Toggle("Заголовок", false);
        _fabText = new TextBox { Text = "Создать" };
        ExpressiveAvalonia.Theming.MaterialTextField.SetLabel(_fabText, "Текст FAB");
        Settings.Children.Add(_fabText);
        _disabledLast = Toggle("Последний пункт недоступен", false);
        _enabled = Toggle("Доступен", true);
        _rtl = Toggle("Справа налево (RTL)", false);
        _dismiss = Toggle("Закрытие по scrim", true);
        _closeOnSelection = Toggle("Закрывать при выборе", true);
        _hideOnCollapse = Toggle("Скрывать rail при сворачивании", false);
        _body = new Border { Padding = new Thickness(24), Child = new StackPanel { Spacing = 16, Children = { new TextBlock { Text = "Область приложения", FontSize = 22 }, _open, _status } } };
        _host = new MaterialNavigationRailHost
        {
            Rail = _rail,
            CollapsedRail = _collapsedRail,
            IsModal = true
        };
        _surface = new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(16), ClipToBounds = true, Child = _stage, Margin = new Thickness(24), VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Stretch };
        _surface.Bind(Border.BorderBrushProperty, this.GetResourceObservable("MaterialOutlineVariantBrush"));
        _body.Bind(Border.BackgroundProperty, this.GetResourceObservable("MaterialSurfaceContainerLowBrush"));
        Children.Add(_surface);
        _open.Click += (_, _) => _host.OpenRail();
        _host.RailClosed += (_, _) => _status.Text = "Rail закрыт · выбран пункт: " + (_rail.SelectedItem is MaterialNavigationRailItem item ? item.Content : "—");
        _rail.MenuCommand = new ActionCommand(() => { if (_kind.SelectedIndex == 2) _host.CloseRail(); else _status.Text = "Нажато меню"; });
        _collapsedRail.MenuCommand = new ActionCommand(() => { if (_kind.SelectedIndex == 2) _host.OpenRail(); else _status.Text = "Нажато меню"; });
        _rail.FabCommand = new ActionCommand(() => _status.Text = "Нажато: " + _fabText.Text);
        _collapsedRail.FabCommand = _rail.FabCommand;
        _rail.SelectionChanged += (_, _) => { if (_rail.SelectedItem is MaterialNavigationRailItem item) _status.Text = "Выбрано: " + item.Content; };
        _rail.ItemInvoked += (_, e) => _status.Text = "Открыто: " + e.Item.Content;
        foreach (var choice in new[] { _kind, _placement, _alignment, _badge }) choice.SelectionChanged += (_, _) => Apply();
        foreach (var toggle in new[] { _labels, _menu, _fab, _header, _disabledLast, _enabled, _rtl, _dismiss, _closeOnSelection, _hideOnCollapse }) toggle.IsCheckedChanged += (_, _) => Apply();
        _count.PropertyChanged += (_, e) => { if (e.Property == Slider.ValueProperty) Apply(); };
        _height.PropertyChanged += (_, e) => { if (e.Property == Slider.ValueProperty) Apply(); };
        _fabText.TextChanged += (_, _) => Apply();
        Apply();
    }

    private void Apply()
    {
        var modal = _kind.SelectedIndex == 2;
        if (!ReferenceEquals(_stage.Content, modal ? (Control)_host : _inline))
        {
            _host.CloseRail();
            _stage.Content = null;
            _host.Content = null;
            _host.Rail = null;
            _inline.Children.Clear();
            if (modal)
            {
                _host.Content = _body;
                _host.Rail = _rail;
                _stage.Content = _host;
            }
            else
            {
                _inline.Children.Add(_rail);
                Grid.SetColumn(_body, 1);
                _inline.Children.Add(_body);
                _stage.Content = _inline;
            }
        }
        _rail.Mode = _kind.SelectedIndex == 0
            ? MaterialNavigationRailMode.Compact
            : MaterialNavigationRailMode.Wide;
        _rail.IsExpanded = _kind.SelectedIndex != 0;
        _host.Placement = _placement.SelectedIndex == 1 ? MaterialNavigationRailHostPlacement.Floating : MaterialNavigationRailHostPlacement.Docked;
        _rail.IsFloating = _placement.SelectedIndex == 1;
        _rail.RailAlignment = _alignment.SelectedIndex == 1 ? MaterialNavigationRailAlignment.Middle : MaterialNavigationRailAlignment.Top;
        _rail.ShowMenuButton = _menu.IsChecked == true;
        _rail.ShowFab = _fab.IsChecked == true;
        _rail.Header = _header.IsChecked == true ? _headerContent : null;
        _rail.FabText = _fabText.Text;
        _fabText.IsVisible = _rail.IsExpanded && _rail.ShowFab;
        _open.IsVisible = modal;
        _dismiss.IsVisible = _closeOnSelection.IsVisible = _hideOnCollapse.IsVisible = modal;
        _host.IsLightDismissEnabled = _dismiss.IsChecked == true;
        _host.CloseOnSelection = _closeOnSelection.IsChecked == true;
        _host.HideOnCollapse = _hideOnCollapse.IsChecked == true;
        _surface.Height = _height.Value;
        _rail.Height = modal ? double.NaN : _height.Value;
        _rail.IsEnabled = _enabled.IsChecked == true;
        _surface.FlowDirection = _rtl.IsChecked == true ? Avalonia.Media.FlowDirection.RightToLeft : Avalonia.Media.FlowDirection.LeftToRight;

        // The source hideOnCollapse=false presentation is a real collapsed rail, not a stub:
        // it owns native items, forwards activation to the expanded rail and shares selection.
        _collapsedRail.Mode = MaterialNavigationRailMode.Wide;
        _collapsedRail.IsExpanded = false;
        _collapsedRail.RailAlignment = _rail.RailAlignment;
        _collapsedRail.ShowMenuButton = _rail.ShowMenuButton;
        _collapsedRail.ShowFab = _rail.ShowFab;
        _collapsedRail.Header = _rail.Header;
        _collapsedRail.FabText = _rail.FabText;
        _collapsedRail.Height = _height.Value;
        _collapsedRail.IsEnabled = _rail.IsEnabled;
        _collapsedRail.FlowDirection = _surface.FlowDirection;

        var count = (int)_count.Value;
        SyncItems(_rail, count);
        SyncItems(_collapsedRail, count);
        if (_rail.SelectedIndex < 0 || _rail.SelectedIndex >= count ||
            _rail.SelectedItem is MaterialNavigationRailItem { IsEnabled: false })
            _rail.SelectedIndex = 0;
        _collapsedRail.SelectedIndex = _rail.SelectedIndex;
    }

    private void SyncItems(MaterialNavigationRail rail, int count)
    {
        while (rail.Items.Count > count)
            rail.Items.RemoveAt(rail.Items.Count - 1);
        while (rail.Items.Count < count)
        {
            var i = rail.Items.Count;
            rail.Items.Add(new MaterialNavigationRailItem { Content = _names[i], Icon = Geometry.Parse(_icons[i]) });
        }
        for (var i = 0; i < count; i++)
        {
            var item = (MaterialNavigationRailItem)rail.Items[i]!;
            item.ShowLabel = _labels.IsChecked == true;
            item.IsEnabled = i != count - 1 || _disabledLast.IsChecked != true;
            item.Badge = i == 2 ? (MaterialNavigationRailBadge)_badge.SelectedIndex : MaterialNavigationRailBadge.None;
            item.BadgeText = "3";
        }
    }

    private MaterialButtonGroup Choice(string label, string[] options)
    {
        Settings.Children.Add(new TextBlock { Text = label });
        var group = new MaterialButtonGroup { Kind = MaterialButtonGroupKind.Connected, SelectionMode = MaterialButtonGroupSelectionMode.Single, ShowSelectedIcon = true, ControlSize = ExpressiveAvalonia.Theming.MaterialControlSize.Small };
        foreach (var option in options) group.Items.Add(new MaterialButtonGroupItem { Content = option });
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
        var slider = new Slider { Minimum = min, Maximum = max, Value = value, TickFrequency = step, IsSnapToTickEnabled = true };
        Avalonia.Automation.AutomationProperties.SetName(slider, label);
        slider.PropertyChanged += (_, e) => { if (e.Property == Slider.ValueProperty) caption.Text = $"{label}: {slider.Value:0}"; };
        Settings.Children.Add(slider);
        return slider;
    }
    private sealed class ActionCommand(Action execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => execute();
    }
}
