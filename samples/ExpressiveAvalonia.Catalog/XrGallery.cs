using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Catalog;

/// <summary>
/// One live XR component chosen in the inspector: an app bar, an icon or toggle button, a
/// toolbar, a navigation bar, a navigation rail or the modal dialog.
/// </summary>
/// <remarks>
/// Every element is the real control, so pointer and keyboard activation, single selection,
/// commands, focus rings and the native modal dialog lifecycle are exercised rather than drawn.
/// The gallery does not claim spatial runtime support; it demonstrates the desktop interaction
/// model of the XR components.
/// </remarks>
public static class XrGallery
{
    public static Control Create() => new XrPlayground();
}

internal sealed class XrPlayground : Grid, IComponentPlayground
{
    private static readonly string[] ComponentNames =
        ["App bar", "Icon button", "Toggle icon button", "Toolbar", "Navigation bar", "Navigation rail", "Dialog"];

    private static readonly string[] IconNames =
        ["Меню", "Плюс", "Звезда", "Избранное", "Поиск", "Главная", "Закрыть"];

    private static readonly Geometry[] IconData =
    [
        XrIcons.Menu,
        XrIcons.Add,
        XrIcons.Star,
        XrIcons.Favorite,
        XrIcons.Search,
        XrIcons.Home,
        XrIcons.Close,
    ];

    private static readonly string[] TrailingLabels = ["Избранное", "Поиск", "Закрыть"];
    private static readonly Geometry[] TrailingIcons = [XrIcons.FavoriteOutline, XrIcons.Search, XrIcons.Close];

    private static readonly string[] BarLabels = ["Обзор", "Главная", "Сохранённое", "Обсуждения", "Обновления"];
    private static readonly Geometry[] BarIcons = [XrIcons.Explore, XrIcons.Home, XrIcons.Bookmark, XrIcons.Chat, XrIcons.History];

    private static readonly string[] RailLabels = ["Почта", "Чат", "Пространства", "Встречи", "Главная", "Сохранённое"];
    private static readonly Geometry[] RailIcons = [XrIcons.Mail, XrIcons.Chat, XrIcons.Explore, XrIcons.History, XrIcons.Home, XrIcons.Bookmark];

    private static readonly string[] ToolbarLabels = ["Избранное", "Поиск", "Главная", "Звезда", "Чат", "Почта"];
    private static readonly Geometry[] ToolbarIcons = [XrIcons.Favorite, XrIcons.Search, XrIcons.Home, XrIcons.Star, XrIcons.Chat, XrIcons.Mail];

    private readonly XrAppBar _appBar = new() { MaxWidth = 480, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Center };
    private readonly XrIconButton _iconButton = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly XrToggleIconButton _toggleButton = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly XrToolbar _toolbar = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly XrNavigationBar _navBar = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly XrNavigationRail _rail = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _dialogButton = new() { Content = "Открыть диалог", HorizontalAlignment = HorizontalAlignment.Center };
    private readonly StackPanel _dialogPanel;
    private readonly TextBlock _status = new()
    {
        Text = "XR-компонент готов к работе.",
        TextWrapping = TextWrapping.Wrap,
        TextAlignment = TextAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Center,
        Opacity = 0.8,
    };

    private readonly ComboBox _component;
    private readonly StackPanel _surfaceGroup;
    private readonly ComboBox _surface;
    private readonly StackPanel _iconGroup;
    private readonly ComboBox _icon;
    private readonly StackPanel _labelGroup;
    private readonly TextBox _label;
    private readonly ToggleSwitch _selected;
    private readonly ToggleSwitch _enabled;

    private readonly StackPanel _appBarGroup;
    private readonly ComboBox _appBarConfiguration;
    private readonly TextBox _appBarTitle;
    private readonly Slider _trailingCount;

    private readonly StackPanel _toolbarGroup;
    private readonly ComboBox _toolbarOrientation;
    private readonly Slider _toolbarItems;
    private readonly ToggleSwitch _toolbarFab;

    private readonly StackPanel _navBarGroup;
    private readonly Slider _navBarCount;

    private readonly StackPanel _railGroup;
    private readonly ToggleSwitch _railFab;
    private readonly ToggleSwitch _railLabels;
    private readonly ToggleSwitch _railMenu;

    private readonly StackPanel _dialogGroup;
    private readonly TextBox _dialogTitle;
    private readonly TextBox _dialogText;
    private readonly ToggleSwitch _dialogIcon;
    private readonly ToggleSwitch _dialogDivider;
    private readonly ToggleSwitch _dialogSecondary;

    private bool _syncing;

    public StackPanel Settings { get; } = new() { Spacing = 16 };

    internal XrPlayground()
    {
        _dialogPanel = new StackPanel
        {
            Spacing = 16,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock
                {
                    Text = "Диалог открывается модальным окном над каталогом и возвращает результат действия.",
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center,
                    MaxWidth = 420,
                },
                _dialogButton,
            },
        };

        var stageContent = new Grid
        {
            Children = { _appBar, _iconButton, _toggleButton, _toolbar, _navBar, _rail, _dialogPanel },
        };
        var stage = new Border
        {
            Height = 620,
            MinWidth = 360,
            MaxWidth = 720,
            Padding = new Thickness(16),
            CornerRadius = new CornerRadius(16),
            ClipToBounds = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Child = stageContent,
        };
        stage.Bind(Border.BackgroundProperty, stage.GetResourceObservable("MaterialSurfaceContainerLowBrush"));

        var stageColumn = new StackPanel
        {
            Spacing = 16,
            MaxWidth = 720,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { stage, _status },
        };
        Children.Add(stageColumn);

        _component = Select(Settings, "Компонент", ComponentNames, 0);

        _surfaceGroup = new StackPanel { Spacing = 6 };
        _surfaceGroup.Children.Add(new TextBlock { Text = "Поверхность" });
        _surface = new ComboBox
        {
            ItemsSource = Enum.GetNames<XrSurfaceRole>(),
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        AutomationProperties.SetName(_surface, "Поверхность");
        _surfaceGroup.Children.Add(_surface);
        Settings.Children.Add(_surfaceGroup);

        _iconGroup = new StackPanel { Spacing = 6 };
        _iconGroup.Children.Add(new TextBlock { Text = "Иконка" });
        _icon = new ComboBox { ItemsSource = IconNames, SelectedIndex = 3, HorizontalAlignment = HorizontalAlignment.Stretch };
        AutomationProperties.SetName(_icon, "Иконка");
        _iconGroup.Children.Add(_icon);
        Settings.Children.Add(_iconGroup);

        _labelGroup = new StackPanel { Spacing = 6 };
        _label = new TextBox { Text = "Избранное" };
        MaterialTextField.SetVariant(_label, MaterialTextFieldVariant.Outlined);
        MaterialTextField.SetLabel(_label, "Подпись кнопки");
        AutomationProperties.SetName(_label, "Подпись кнопки");
        _labelGroup.Children.Add(_label);
        Settings.Children.Add(_labelGroup);

        _selected = new ToggleSwitch { Content = "Выбрана (IsChecked)" };
        Settings.Children.Add(_selected);
        _enabled = new ToggleSwitch { Content = "Доступна", IsChecked = true };
        Settings.Children.Add(_enabled);

        _appBarGroup = new StackPanel { Spacing = 12 };
        _appBarGroup.Children.Add(new TextBlock { Text = "App bar", FontSize = 16, FontWeight = FontWeight.Bold });
        _appBarGroup.Children.Add(new TextBlock { Text = "Конфигурация" });
        _appBarConfiguration = new ComboBox
        {
            ItemsSource = new[] { "Center-aligned", "Small" },
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        AutomationProperties.SetName(_appBarConfiguration, "Конфигурация app bar");
        _appBarGroup.Children.Add(_appBarConfiguration);
        _appBarTitle = Field(_appBarGroup, "Заголовок", "Заголовок");
        _trailingCount = Number(_appBarGroup, "Иконок справа", 0, 3, 1);
        Settings.Children.Add(_appBarGroup);

        _toolbarGroup = new StackPanel { Spacing = 12 };
        _toolbarGroup.Children.Add(new TextBlock { Text = "Toolbar", FontSize = 16, FontWeight = FontWeight.Bold });
        _toolbarGroup.Children.Add(new TextBlock { Text = "Ориентация" });
        _toolbarOrientation = new ComboBox
        {
            ItemsSource = new[] { "Горизонтальная", "Вертикальная" },
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        AutomationProperties.SetName(_toolbarOrientation, "Ориентация toolbar");
        _toolbarGroup.Children.Add(_toolbarOrientation);
        _toolbarItems = Number(_toolbarGroup, "Действий", 3, 6, 6);
        _toolbarFab = Toggle(_toolbarGroup, "FAB в toolbar", true);
        Settings.Children.Add(_toolbarGroup);

        _navBarGroup = new StackPanel { Spacing = 12 };
        _navBarGroup.Children.Add(new TextBlock { Text = "Navigation bar", FontSize = 16, FontWeight = FontWeight.Bold });
        _navBarCount = Number(_navBarGroup, "Пунктов", 3, 5, 4);
        Settings.Children.Add(_navBarGroup);

        _railGroup = new StackPanel { Spacing = 12 };
        _railGroup.Children.Add(new TextBlock { Text = "Navigation rail", FontSize = 16, FontWeight = FontWeight.Bold });
        _railFab = Toggle(_railGroup, "FAB в rail", true);
        _railLabels = Toggle(_railGroup, "Подписи пунктов", false);
        _railMenu = Toggle(_railGroup, "Кнопка меню", true);
        Settings.Children.Add(_railGroup);

        _dialogGroup = new StackPanel { Spacing = 12 };
        _dialogGroup.Children.Add(new TextBlock { Text = "Dialog", FontSize = 16, FontWeight = FontWeight.Bold });
        _dialogTitle = Field(_dialogGroup, "Заголовок", "Basic dialog title");
        _dialogText = Field(_dialogGroup, "Поддерживающий текст",
            "A dialog is a type of modal window that appears in front of app content to provide critical information.");
        _dialogIcon = Toggle(_dialogGroup, "Иконка в диалоге", true);
        _dialogDivider = Toggle(_dialogGroup, "Разделитель", true);
        _dialogSecondary = Toggle(_dialogGroup, "Второе действие", true);
        Settings.Children.Add(_dialogGroup);

        Settings.Children.Add(new TextBlock
        {
            Text = "Кнопки и пункты активируются указателем, Enter и Space; навигационные бары и rail " +
                   "используют нативное одиночное выделение со стрелками, Home и End. Пространственный " +
                   "runtime не подразумевается.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
        });

        _appBar.LeadingContent = LeadingButton();
        _appBar.TrailingContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 0 };

        BuildNavigationBar();
        BuildRail();
        _toolbar.FabIcon = XrIcons.Add;
        _toolbar.FabLabel = "Создать";

        _iconButton.Click += (_, _) => _status.Text = $"Icon button: {_iconButton.Label}.";
        _toggleButton.IsCheckedChanged += (_, _) => _status.Text = $"Toggle icon button: {_toggleButton.IsChecked}.";
        _toolbar.FabCommand = new ActionCommand(() => _status.Text = "Toolbar FAB.");
        _navBar.SelectionChanged += (_, _) =>
        {
            if (_navBar.SelectedItem is XrNavigationBarItem item)
                _status.Text = $"Navigation bar: {item.Label} (индекс {_navBar.SelectedIndex}).";
        };
        _rail.SelectionChanged += (_, _) =>
        {
            if (_rail.SelectedItem is XrNavigationRailItem item)
                _status.Text = $"Navigation rail: {item.Label} (индекс {_rail.SelectedIndex}).";
        };
        _rail.MenuCommand = new ActionCommand(() => _status.Text = "Rail: меню.");
        _rail.FabCommand = new ActionCommand(() => _status.Text = "Rail: FAB.");
        _dialogButton.Click += async (_, _) => await OpenDialogAsync();

        _component.SelectionChanged += (_, _) => Apply();
        _surface.SelectionChanged += (_, _) => Apply();
        _icon.SelectionChanged += (_, _) => Apply();
        _label.TextChanged += (_, _) => Apply();
        _appBarConfiguration.SelectionChanged += (_, _) => Apply();
        _appBarTitle.TextChanged += (_, _) => Apply();
        _trailingCount.PropertyChanged += (_, e) => { if (e.Property == Slider.ValueProperty) Apply(); };
        _toolbarOrientation.SelectionChanged += (_, _) => Apply();
        _toolbarItems.PropertyChanged += (_, e) => { if (e.Property == Slider.ValueProperty) Apply(); };
        _navBarCount.PropertyChanged += (_, e) => { if (e.Property == Slider.ValueProperty) Apply(); };
        _dialogTitle.TextChanged += (_, _) => Apply();
        _dialogText.TextChanged += (_, _) => Apply();
        foreach (var toggle in new[]
                 {
                     _selected, _enabled, _toolbarFab, _railFab, _railLabels, _railMenu,
                     _dialogIcon, _dialogDivider, _dialogSecondary,
                 })
            toggle.IsCheckedChanged += (_, _) => { if (!_syncing) Apply(); };

        Apply();
    }

    private void Apply()
    {
        var component = Math.Clamp(_component.SelectedIndex, 0, ComponentNames.Length - 1);
        var surface = (XrSurfaceRole)Math.Clamp(_surface.SelectedIndex, 0, 3);
        var icon = IconData[Math.Clamp(_icon.SelectedIndex, 0, IconData.Length - 1)];
        var label = string.IsNullOrWhiteSpace(_label.Text) ? "Действие" : _label.Text!;

        _appBar.IsVisible = component == 0;
        _iconButton.IsVisible = component == 1;
        _toggleButton.IsVisible = component == 2;
        _toolbar.IsVisible = component == 3;
        _navBar.IsVisible = component == 4;
        _rail.IsVisible = component == 5;
        _dialogPanel.IsVisible = component == 6;

        _surfaceGroup.IsVisible = component <= 3;
        _iconGroup.IsVisible = component is 1 or 2;
        _labelGroup.IsVisible = component is 1 or 2;
        _selected.IsVisible = component == 2;
        _enabled.IsVisible = component is 1 or 2;
        _appBarGroup.IsVisible = component == 0;
        _toolbarGroup.IsVisible = component == 3;
        _navBarGroup.IsVisible = component == 4;
        _railGroup.IsVisible = component == 5;
        _dialogGroup.IsVisible = component == 6;

        _iconButton.Icon = icon;
        _iconButton.Label = label;
        _iconButton.Surface = surface;
        _iconButton.IsEnabled = _enabled.IsChecked == true;

        _toggleButton.Icon = icon;
        _toggleButton.SelectedIcon = icon;
        _toggleButton.Label = label;
        _toggleButton.Surface = surface;
        _toggleButton.IsEnabled = _enabled.IsChecked == true;
        if (!_syncing && _toggleButton.IsChecked != (_selected.IsChecked == true))
        {
            _syncing = true;
            _toggleButton.IsChecked = _selected.IsChecked == true;
            _syncing = false;
        }

        _appBar.Configuration = _appBarConfiguration.SelectedIndex == 1
            ? XrAppBarConfiguration.Small
            : XrAppBarConfiguration.CenterAligned;
        _appBar.Headline = string.IsNullOrWhiteSpace(_appBarTitle.Text) ? "Заголовок" : _appBarTitle.Text;
        // The app bar carries three surface tones; the tertiary role maps onto the highest one.
        _appBar.Elevation = surface == XrSurfaceRole.TertiaryContainer ? XrSurfaceRole.SurfaceContainerHighest : surface;
        var trailing = (int)Math.Round(_trailingCount.Value);
        var panel = (StackPanel)_appBar.TrailingContent!;
        while (panel.Children.Count > trailing)
            panel.Children.RemoveAt(panel.Children.Count - 1);
        while (panel.Children.Count < trailing)
            panel.Children.Add(TrailingButton(panel.Children.Count, surface));
        for (var i = 0; i < panel.Children.Count; i++)
            if (panel.Children[i] is XrIconButton button)
                button.Surface = surface;

        _toolbar.Orientation = _toolbarOrientation.SelectedIndex == 1 ? Orientation.Vertical : Orientation.Horizontal;
        _toolbar.Elevation = surface;
        _toolbar.ShowFab = _toolbarFab.IsChecked == true;
        SyncToolbar(surface);

        _rail.ShowFab = _railFab.IsChecked == true;
        _rail.ShowMenu = _railMenu.IsChecked == true;
        foreach (var item in _rail.Items.OfType<XrNavigationRailItem>())
            item.ShowLabel = _railLabels.IsChecked == true;

        if (component == 4)
            BuildNavigationBar();
    }

    private void SyncToolbar(XrSurfaceRole surface)
    {
        var itemSurface = surface switch
        {
            XrSurfaceRole.TertiaryContainer => XrSurfaceRole.TertiaryContainer,
            XrSurfaceRole.SurfaceContainerHigh => XrSurfaceRole.SurfaceContainerHigh,
            XrSurfaceRole.SurfaceContainerHighest => XrSurfaceRole.SurfaceContainerHigh,
            _ => XrSurfaceRole.SurfaceContainer,
        };
        var count = (int)Math.Round(_toolbarItems.Value);
        while (_toolbar.Items.Count > count)
            _toolbar.Items.RemoveAt(_toolbar.Items.Count - 1);
        while (_toolbar.Items.Count < count)
        {
            var index = _toolbar.Items.Count;
            var slot = new XrToggleIconButton
            {
                Icon = ToolbarIcons[index],
                Label = ToolbarLabels[index],
            };
            slot.IsCheckedChanged += (_, _) => _status.Text = $"Toolbar: {slot.Label} — {slot.IsChecked}.";
            _toolbar.Items.Add(slot);
        }

        for (var i = 0; i < count; i++)
        {
            var slot = (XrToggleIconButton)_toolbar.Items[i]!;
            slot.Surface = itemSurface;
            slot.Icon = ToolbarIcons[i];
            slot.Label = ToolbarLabels[i];
        }
    }

    private void BuildNavigationBar()
    {
        var count = (int)Math.Round(_navBarCount.Value);
        while (_navBar.Items.Count > count)
            _navBar.Items.RemoveAt(_navBar.Items.Count - 1);
        while (_navBar.Items.Count < count)
            _navBar.Items.Add(new XrNavigationBarItem());

        for (var i = 0; i < count; i++)
        {
            var item = (XrNavigationBarItem)_navBar.Items[i]!;
            item.Label = BarLabels[i];
            item.Icon = BarIcons[i];
            item.SelectedIcon = BarIcons[i];
            item.ShowLabel = true;
            item.Badge = i switch
            {
                2 => XrBadgeKind.Large,
                3 => XrBadgeKind.Small,
                _ => XrBadgeKind.None,
            };
            item.BadgeText = "3";
        }

        if (_navBar.SelectedIndex < 0 || _navBar.SelectedIndex >= count)
            _navBar.SelectedIndex = 0;
    }

    private void BuildRail()
    {
        for (var i = 0; i < RailLabels.Length; i++)
        {
            _rail.Items.Add(new XrNavigationRailItem
            {
                Icon = RailIcons[i],
                SelectedIcon = RailIcons[i],
                Label = RailLabels[i],
                ShowLabel = _railLabels.IsChecked == true,
                Badge = i switch
                {
                    0 => XrBadgeKind.Large,
                    2 => XrBadgeKind.Small,
                    _ => XrBadgeKind.None,
                },
                BadgeText = "3",
            });
        }

        _rail.SelectedIndex = 0;
    }

    private XrIconButton LeadingButton()
    {
        var leading = new XrIconButton { Icon = XrIcons.Menu, Label = "Меню" };
        leading.Click += (_, _) => _status.Text = "App bar: меню.";
        return leading;
    }

    private XrIconButton TrailingButton(int index, XrSurfaceRole surface)
    {
        var button = new XrIconButton
        {
            Icon = TrailingIcons[index],
            Label = TrailingLabels[index],
            Surface = surface,
        };
        button.Click += (_, _) => _status.Text = $"App bar: {button.Label}.";
        return button;
    }

    private async Task OpenDialogAsync()
    {
        if (TopLevel.GetTopLevel(_dialogButton) is not Window owner)
        {
            _status.Text = "Окно каталога ещё не создано.";
            return;
        }

        var dialog = new XrDialog
        {
            Headline = string.IsNullOrWhiteSpace(_dialogTitle.Text) ? "Dialog" : _dialogTitle.Text,
            SupportingText = _dialogText.Text,
            ContentIcon = XrIcons.FavoriteOutline,
            ShowIcon = _dialogIcon.IsChecked == true,
            ShowDivider = _dialogDivider.IsChecked == true,
            ShowSecondaryAction = _dialogSecondary.IsChecked == true,
            Elevation = (XrSurfaceRole)Math.Clamp(_surface.SelectedIndex, 0, 3),
            PrimaryActionText = "Action 1",
            SecondaryActionText = "Action 2",
        };
        dialog.PrimaryInvoked += (_, _) => _status.Text = "Dialog: основное действие.";
        dialog.SecondaryInvoked += (_, _) => _status.Text = "Dialog: второе действие.";

        try
        {
            var result = await dialog.ShowDialogAsync(owner);
            _status.Text = $"Dialog закрыт: {result}.";
        }
        catch (Exception exception)
        {
            // A modal window can fail to open (for example when the owner is closing); report it
            // instead of letting an async void handler take the process down.
            _status.Text = exception.Message;
        }
    }

    private static ComboBox Select(StackPanel target, string label, string[] options, int selected)
    {
        target.Children.Add(new TextBlock { Text = label });
        var combo = new ComboBox { ItemsSource = options, SelectedIndex = selected, HorizontalAlignment = HorizontalAlignment.Stretch };
        AutomationProperties.SetName(combo, label);
        target.Children.Add(combo);
        return combo;
    }

    private static TextBox Field(StackPanel target, string label, string text)
    {
        var field = new TextBox { Text = text, TextWrapping = TextWrapping.Wrap };
        MaterialTextField.SetVariant(field, MaterialTextFieldVariant.Outlined);
        MaterialTextField.SetLabel(field, label);
        AutomationProperties.SetName(field, label);
        target.Children.Add(field);
        return field;
    }

    private static ToggleSwitch Toggle(StackPanel target, string label, bool value)
    {
        var toggle = new ToggleSwitch { Content = label, IsChecked = value };
        target.Children.Add(toggle);
        return toggle;
    }

    private static Slider Number(StackPanel target, string label, double min, double max, double value)
    {
        var caption = new TextBlock { Text = $"{label}: {value:0}" };
        target.Children.Add(caption);
        var slider = new Slider
        {
            Minimum = min,
            Maximum = max,
            Value = value,
            TickFrequency = 1,
            IsSnapToTickEnabled = true,
        };
        AutomationProperties.SetName(slider, label);
        slider.PropertyChanged += (_, e) =>
        {
            if (e.Property == Slider.ValueProperty)
                caption.Text = $"{label}: {slider.Value:0}";
        };
        target.Children.Add(slider);
        return slider;
    }

    private sealed class ActionCommand(Action execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => execute();
    }
}
