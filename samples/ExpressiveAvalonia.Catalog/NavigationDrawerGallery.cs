using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Catalog;

public static class NavigationDrawerGallery
{
    public static Control Create() => new NavigationDrawerPlayground();
}

/// <summary>
/// One live navigation drawer hosted by <see cref="MaterialNavigationDrawerHost"/>: docked and
/// modal presentations, group headers, dividers, badges, disabled destinations and the real
/// open/close lifecycle (scrim, Escape, focus return, close on selection).
/// </summary>
internal sealed class NavigationDrawerPlayground : Grid, IComponentPlayground
{
    private readonly MaterialNavigationDrawer _drawer = new();
    private readonly MaterialNavigationDrawerHost _host = new();
    private readonly Border _card;
    private readonly Border _page;
    private readonly TextBlock _pageTitle = new();
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _hostState = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Button _open = new() { Content = "Открыть меню", HorizontalAlignment = HorizontalAlignment.Left };
    private readonly Button _close = new() { Content = "Закрыть", HorizontalAlignment = HorizontalAlignment.Left };
    private readonly MaterialButtonGroup _mode;
    private readonly ToggleSwitch _openToggle;
    private readonly ToggleSwitch _closeOnSelection;
    private readonly ToggleSwitch _lightDismiss;
    private readonly ToggleSwitch _headers;
    private readonly ToggleSwitch _dividers;
    private readonly ToggleSwitch _badges;
    private readonly ToggleSwitch _icons;
    private readonly ToggleSwitch _disabled;
    private readonly Slider _width;
    private readonly Slider _scrim;
    private readonly Slider _margin;
    private readonly ComboBox _selected;
    private readonly MaterialNavigationSectionHeader[] _headerItems;
    private readonly MaterialNavigationDivider[] _dividerItems;
    private readonly MaterialNavigationItem[] _destinations;
    private readonly object?[] _destinationIcons;
    private bool? _lastModal;
    private double _lastWidth = double.NaN;
    private bool _sync;

    public StackPanel Settings { get; } = new() { Spacing = 16 };

    internal NavigationDrawerPlayground()
    {
        _pageTitle.Text = "Входящие";
        MaterialTypography.SetStyle(_pageTitle, MaterialTypeStyle.HeadlineSmall);
        var body = new TextBlock
        {
            Text = "Содержимое страницы. Встроенный drawer сдвигает его вправо; модальный — " +
                   "перекрывает затемнением и удерживает фокус внутри панели.",
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 520,
        };
        MaterialTypography.SetStyle(body, MaterialTypeStyle.BodyMedium);
        MaterialTypography.SetStyle(_hostState, MaterialTypeStyle.BodySmall);
        _hostState.Foreground = Brushes.Gray;

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        actions.Children.Add(_open);
        actions.Children.Add(_close);

        var pageStack = new StackPanel { Spacing = 12 };
        pageStack.Children.Add(_pageTitle);
        pageStack.Children.Add(body);
        pageStack.Children.Add(actions);
        pageStack.Children.Add(_status);
        pageStack.Children.Add(_hostState);
        _page = new Border { Padding = new Thickness(24), Child = pageStack };
        _page.Bind(Border.BackgroundProperty, _page.GetResourceObservable("MaterialSurfaceContainerLowestBrush"));

        _card = new Border
        {
            Height = 520,
            MaxWidth = 900,
            CornerRadius = new CornerRadius(28),
            ClipToBounds = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            BorderThickness = new Thickness(1),
        };
        _card.Bind(Border.BorderBrushProperty, _card.GetResourceObservable("MaterialOutlineVariantBrush"));
        _card.Child = _host;

        _host.Content = _page;
        _host.Drawer = _drawer;
        _host.IsModal = false;
        _host.IsDrawerOpen = true;

        var stage = new Border { Margin = new Thickness(24), Child = _card };
        Children.Add(stage);

        _mode = Choice("Режим", ["Встроенный", "Модальный"]);
        _openToggle = Toggle("Drawer открыт", true);
        _closeOnSelection = Toggle("Закрывать при выборе", true);
        _lightDismiss = Toggle("Закрытие по scrim", true);
        _headers = Toggle("Заголовки групп", true);
        _dividers = Toggle("Разделители", true);
        _badges = Toggle("Бейджи", true);
        _icons = Toggle("Иконки пунктов", true);
        _disabled = Toggle("«Корзина» недоступна", false);
        _width = Number("Ширина drawer", 280, 400, 360, 10);
        _scrim = Number("Плотность scrim", 0, 0.8, 0.32, 0.04);
        _margin = Number("Отступ модального drawer", 0, 48, 12, 4);

        Settings.Children.Add(new TextBlock { Text = "Выбранный пункт" });
        _selected = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        AutomationProperties.SetName(_selected, "Выбранный пункт");
        Settings.Children.Add(_selected);

        var (headers, dividers, destinations) = BuildDrawer();
        _headerItems = headers;
        _dividerItems = dividers;
        _destinations = destinations;
        _destinationIcons = destinations.Select(destination => destination.Icon).ToArray();
        foreach (var destination in destinations)
            _selected.Items.Add(destination.Content);
        _drawer.SelectedIndex = 0;

        _open.Click += (_, _) => _host.OpenDrawer();
        _close.Click += (_, _) => _host.CloseDrawer();
        _drawer.SelectionChanged += (_, e) =>
        {
            _status.Text = $"Выбрано: {Describe(e.AddedItem)} (индекс {_drawer.SelectedIndex})";
            if (!_sync)
                SyncSelectedChoice();
        };
        _host.DrawerOpened += (_, _) =>
        {
            _hostState.Text = "Drawer открыт · Escape, scrim или выбор пункта закрывают его";
            SetOpenVisual(true);
        };
        _host.DrawerClosed += (_, _) =>
        {
            _hostState.Text = "Drawer закрыт · фокус возвращён на элемент-открыватель";
            SetOpenVisual(false);
        };

        _mode.SelectionChanged += (_, _) => { if (!_sync) Apply(); };
        foreach (var toggle in new[] { _openToggle, _closeOnSelection, _lightDismiss, _headers, _dividers, _badges, _icons, _disabled })
            toggle.IsCheckedChanged += (_, _) => { if (!_sync) Apply(); };
        _width.PropertyChanged += (_, e) => { if (e.Property == RangeBase.ValueProperty && !_sync) Apply(); };
        _scrim.PropertyChanged += (_, e) => { if (e.Property == RangeBase.ValueProperty && !_sync) Apply(); };
        _margin.PropertyChanged += (_, e) => { if (e.Property == RangeBase.ValueProperty && !_sync) Apply(); };
        _selected.SelectionChanged += (_, _) =>
        {
            if (_sync || _selected.SelectedIndex < 0 || _selected.SelectedIndex >= _destinations.Length)
                return;
            var target = _destinations[_selected.SelectedIndex];
            if (!target.IsEnabled)
            {
                SyncSelectedChoice();
                return;
            }
            _drawer.SelectedIndex = _selected.SelectedIndex;
        };

        Apply();
    }

    private (MaterialNavigationSectionHeader[] Headers, MaterialNavigationDivider[] Dividers, MaterialNavigationItem[] Destinations) BuildDrawer()
    {
        var mail = new MaterialNavigationSectionHeader { Content = "Почта" };
        var labels = new MaterialNavigationSectionHeader { Content = "Метки" };
        var divider = new MaterialNavigationDivider();
        var trailingDivider = new MaterialNavigationDivider();

        var inbox = Item("Входящие", Icons.Inbox, "24");
        var outbox = Item("Исходящие", Icons.Send, "100+");
        var favorites = Item("Избранное", Icons.Favorite, "7");
        var trash = Item("Корзина", Icons.Delete, null);
        var label1 = Item("Метка 1", Icons.Folder, null);
        var label2 = Item("Метка 2", Icons.Folder, null);
        var label3 = Item("Длинная метка проекта", Icons.Folder, null);

        _drawer.Items.Add(mail);
        _drawer.Items.Add(inbox);
        _drawer.Items.Add(outbox);
        _drawer.Items.Add(favorites);
        _drawer.Items.Add(trash);
        _drawer.Items.Add(divider);
        _drawer.Items.Add(labels);
        _drawer.Items.Add(label1);
        _drawer.Items.Add(label2);
        _drawer.Items.Add(label3);
        _drawer.Items.Add(trailingDivider);

        return ([mail, labels], [divider, trailingDivider], [inbox, outbox, favorites, trash, label1, label2, label3]);
    }

    private void Apply()
    {
        var modal = _mode.SelectedIndex == 1;
        _sync = true;
        try
        {
            if (_lastModal != modal)
            {
                _lastModal = modal;
                _host.IsModal = modal;
                // Switching presentation starts from the natural state: docked is always open,
                // modal starts closed so the opener and the focus lifecycle stay observable.
                _host.IsDrawerOpen = !modal;
                _hostState.Text = modal
                    ? "Модальный режим · откройте drawer кнопкой; Escape, scrim или выбор закроют его"
                    : "Встроенный режим · открытый drawer сдвигает содержимое страницы";
            }
            else if (_openToggle.IsChecked != _host.IsDrawerOpen)
            {
                _host.IsDrawerOpen = _openToggle.IsChecked == true;
            }

            _host.IsLightDismissEnabled = _lightDismiss.IsChecked == true;
            _host.CloseOnSelection = _closeOnSelection.IsChecked == true;
            _host.ScrimOpacity = _scrim.Value;
            _host.DrawerMargin = new Thickness(_margin.Value);
            var width = _width.Value;
            _drawer.Width = width;
            if (!_lastWidth.Equals(width))
            {
                _lastWidth = width;
                // The host measures the docked inset when the drawer is presented, so a width
                // change while docked needs a fresh presentation pass. Re-applying the current
                // modal flag does that without detaching the drawer (which would reset focus).
                var modalNow = _host.IsModal;
                _host.IsModal = !modalNow;
                _host.IsModal = modalNow;
            }

            foreach (var header in _headerItems)
                header.IsVisible = _headers.IsChecked == true;
            foreach (var divider in _dividerItems)
                divider.IsVisible = _dividers.IsChecked == true;

            var showBadges = _badges.IsChecked == true;
            foreach (var destination in _destinations)
                destination.ShowBadge = showBadges;

            var showIcons = _icons.IsChecked == true;
            for (var i = 0; i < _destinations.Length; i++)
                _destinations[i].Icon = showIcons ? _destinationIcons[i] : null;

            _destinations[3].IsEnabled = _disabled.IsChecked != true;
            if (!_destinations[3].IsEnabled && _drawer.SelectedIndex == 3)
                _drawer.SelectedIndex = 0;

            _closeOnSelection.IsVisible = modal;
            _lightDismiss.IsVisible = modal;
            _scrim.IsVisible = modal;
            _margin.IsVisible = modal;
            _openToggle.IsChecked = _host.IsDrawerOpen;
            _open.IsVisible = modal && !_host.IsDrawerOpen;
            _close.IsVisible = modal && _host.IsDrawerOpen;
            SyncSelectedChoice();
        }
        finally
        {
            _sync = false;
        }
    }

    private void SetOpenVisual(bool open)
    {
        var previous = _sync;
        _sync = true;
        try
        {
            _openToggle.IsChecked = open;
            SyncSelectedChoice();
        }
        finally
        {
            _sync = previous;
        }

        _open.IsVisible = _host.IsModal && !open;
        _close.IsVisible = _host.IsModal && open;
    }

    private void SyncSelectedChoice()
    {
        var index = _drawer.SelectedIndex;
        if (index >= 0 && index < _selected.ItemCount && _selected.SelectedIndex != index)
            _selected.SelectedIndex = index;
    }

    private static string Describe(MaterialNavigationItem? item) => item?.Content?.ToString() ?? "нет";

    private static MaterialNavigationItem Item(string label, string iconId, string? badge) => new()
    {
        Content = label,
        Icon = IconOf(iconId),
        BadgeText = badge,
    };

    private static MaterialIcon IconOf(string sourceId) => new()
    {
        SourceId = sourceId,
        Width = 20,
        Height = 20,
        IsDecorative = true,
    };

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
        var caption = new TextBlock { Text = $"{label}: {value:0.##}" };
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
                caption.Text = $"{label}: {slider.Value:0.##}";
        };
        Settings.Children.Add(slider);
        return slider;
    }

    private static class Icons
    {
        public const string Inbox = "local-003470";
        public const string Send = "local-004582";
        public const string Favorite = "local-001531";
        public const string Delete = "local-001329";
        public const string Folder = "local-001631";
    }
}
