using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Catalog;

/// <summary>One live menu: menu-button flyout, context menu or menu bar, configured in the inspector.</summary>
public static class MenusGallery
{
    public static Control Create() => new MenusPlayground();
}

internal sealed class MenusPlayground : Grid, IComponentPlayground
{
    private static readonly string[] Headers =
    {
        "Открыть", "Дублировать", "Поделиться", "Переименовать", "Переместить", "В архив", "Свойства",
    };

    private static readonly Geometry Undo = Geometry.Parse("M12.5,8C9.85,8 7.45,8.99 5.6,10.6L2,7V16H11L7.38,12.38C8.77,11.22 10.54,10.5 12.5,10.5C16.04,10.5 19.05,12.81 20.1,16L22.47,15.22C21.08,11.03 17.15,8 12.5,8Z");
    private static readonly Geometry Edit = Geometry.Parse("M3,17.25V21H6.75L17.81,9.94L14.06,6.19L3,17.25ZM20.71,7.04C21.1,6.65 21.1,6.02 20.71,5.63L18.37,3.29C17.98,2.9 17.35,2.9 16.96,3.29L15.13,5.12L18.88,8.87L20.71,7.04Z");
    private static readonly Geometry Copy = Geometry.Parse("M16,1H4C2.9,1 2,1.9 2,3V17H4V3H16V1ZM19,5H8C6.9,5 6,5.9 6,7V21C6,22.1 6.9,23 8,23H19C20.1,23 21,22.1 21,21V7C21,5.9 20.1,5 19,5ZM19,21H8V7H19V21Z");
    private static readonly Geometry Share = Geometry.Parse("M18,16.08C17.24,16.08 16.56,16.38 16.04,16.85L8.91,12.7C8.96,12.47 9,12.24 9,12C9,11.76 8.96,11.53 8.91,11.3L15.96,7.19C16.5,7.69 17.21,8 18,8C19.66,8 21,6.66 21,5C21,3.34 19.66,2 18,2C16.34,2 15,3.34 15,5C15,5.24 15.04,5.47 15.09,5.7L8.04,9.81C7.5,9.31 6.79,9 6,9C4.34,9 3,10.34 3,12C3,13.66 4.34,15 6,15C6.79,15 7.5,14.69 8.04,14.19L15.16,18.34C15.11,18.55 15.08,18.77 15.08,19C15.08,20.61 16.39,21.91 18,21.91C19.61,21.91 20.92,20.61 20.92,19C20.92,17.39 19.61,16.08 18,16.08Z");
    private static readonly Geometry Gear = Geometry.Parse("M19.14,12.94C19.18,12.64 19.2,12.33 19.2,12C19.2,11.67 19.18,11.36 19.14,11.06L21.16,9.48C21.34,9.34 21.39,9.07 21.26,8.86L19.34,5.54C19.22,5.32 18.95,5.25 18.73,5.32L16.35,6.28C15.86,5.9 15.33,5.59 14.75,5.35L14.39,2.81C14.36,2.57 14.15,2.4 13.91,2.4H10.09C9.85,2.4 9.65,2.57 9.61,2.81L9.25,5.35C8.68,5.59 8.14,5.9 7.65,6.28L5.27,5.32C5.05,5.25 4.78,5.32 4.66,5.54L2.74,8.86C2.61,9.07 2.66,9.34 2.84,9.48L4.86,11.06C4.82,11.36 4.8,11.67 4.8,12C4.8,12.33 4.82,12.64 4.86,12.94L2.84,14.52C2.66,14.66 2.61,14.93 2.74,15.14L4.66,18.46C4.78,18.68 5.05,18.75 5.27,18.68L7.65,17.72C8.14,18.1 8.68,18.41 9.25,18.65L9.61,21.19C9.65,21.43 9.85,21.6 10.09,21.6H13.91C14.15,21.6 14.36,21.43 14.39,21.19L14.75,18.65C15.33,18.41 15.86,18.1 16.35,17.72L18.73,18.68C18.95,18.75 19.22,18.68 19.34,18.46L21.26,15.14C21.39,14.93 21.34,14.66 21.16,14.52L19.14,12.94ZM12,15.6C10.02,15.6 8.4,13.98 8.4,12C8.4,10.02 10.02,8.4 12,8.4C13.98,8.4 15.6,10.02 15.6,12C15.6,13.98 13.98,15.6 12,15.6Z");
    private static readonly Geometry Chevron = Geometry.Parse("M8.59,16.59L13.17,12L8.59,7.41L10,6L16,12L10,18L8.59,16.59Z");

    private readonly ContentControl _host = new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
    };
    private readonly TextBlock _readout = new()
    {
        Text = "Команда ещё не выполнялась.",
        HorizontalAlignment = HorizontalAlignment.Center,
        FontSize = 14,
        TextWrapping = TextWrapping.Wrap,
        TextAlignment = TextAlignment.Center,
    };
    private readonly TextBlock _hint = new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        FontSize = 12,
        TextWrapping = TextWrapping.Wrap,
        TextAlignment = TextAlignment.Center,
        MaxWidth = 420,
    };
    private readonly ComboBox _type;
    private readonly MaterialButtonGroup _surface;
    private readonly StackPanel _surfacePanel = new() { Spacing = 8 };
    private readonly ComboBox _variant;
    private readonly ComboBox _density;
    private readonly ToggleSwitch _scroll;
    private readonly ToggleSwitch _icons;
    private readonly ToggleSwitch _supporting;
    private readonly ToggleSwitch _divider;
    private readonly ToggleSwitch _active;
    private readonly ToggleSwitch _labels;
    private readonly Slider _count;
    private readonly StackPanel _variantPanel = new() { Spacing = 8 };
    private readonly StackPanel _densityPanel = new() { Spacing = 8 };
    private readonly StackPanel _countPanel = new() { Spacing = 8 };
    private readonly TextBlock _countCaption = new();
    private Control? _exposedPanel;
    private Border? _groupsCard;
    private readonly MaterialMenuGroup[] _groups = new MaterialMenuGroup[3];
    private MaterialMenuButton? _menuButton;
    private MaterialMenuFlyout? _flyout;
    private Button? _contextButton;
    private ContextMenu? _contextMenu;
    private Menu? _menuBar;
    private bool _showGridChecked = true;
    private bool _menuBarVibrantChecked;
    private bool _syncing;
    public StackPanel Settings { get; } = new() { Spacing = 16 };

    internal MenusPlayground()
    {
        var card = new Border
        {
            Width = 520,
            MinHeight = 300,
            Padding = new Thickness(24),
            CornerRadius = new CornerRadius(24),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = _host,
        };
        card.Bind(Border.BackgroundProperty, card.GetResourceObservable("MaterialSurfaceContainerLowBrush"));

        var stage = new StackPanel
        {
            Spacing = 16,
            Margin = new Thickness(24),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        stage.Children.Add(card);
        stage.Children.Add(_hint);
        stage.Children.Add(_readout);
        Children.Add(stage);

        Settings.Children.Add(new TextBlock { Text = "Тип меню" });
        _type = new ComboBox
        {
            ItemsSource = new[] { "Menu button (flyout)", "Context menu", "Menu bar", "Exposed dropdown / search", "Groups (standalone)" },
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        AutomationProperties.SetName(_type, "Тип меню");
        Settings.Children.Add(_type);
        _surface = Choice(_surfacePanel, "Поверхность", new[] { "Baseline", "Expressive" }, 1);
        Settings.Children.Add(_surfacePanel);

        _variantPanel.Children.Add(new TextBlock { Text = "Вариант" });
        _variant = new ComboBox
        {
            ItemsSource = new[] { "Standard", "Vibrant" },
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        AutomationProperties.SetName(_variant, "Вариант меню");
        _variantPanel.Children.Add(_variant);
        Settings.Children.Add(_variantPanel);

        _densityPanel.Children.Add(new TextBlock { Text = "Плотность (только Baseline)" });
        _density = new ComboBox
        {
            ItemsSource = new[] { "Default (56 dp)", "Dense2 (48 dp)", "Dense4 (40 dp)" },
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        AutomationProperties.SetName(_density, "Плотность меню");
        _densityPanel.Children.Add(_density);
        Settings.Children.Add(_densityPanel);

        _scroll = Toggle(Settings, "Полоса прокрутки при переполнении", false);
        _icons = Toggle(Settings, "Ведущие иконки", true);
        _supporting = Toggle(Settings, "Поддерживающий текст у первого пункта", false);
        _divider = Toggle(Settings, "Разделитель под первым пунктом", false);
        _active = Toggle(Settings, "Активный пункт (IsActive)", false);
        _labels = Toggle(Settings, "Заголовки секций", true);

        _countPanel.Children.Add(_countCaption);
        _count = new Slider
        {
            Minimum = 3,
            Maximum = 8,
            Value = 5,
            TickFrequency = 1,
            IsSnapToTickEnabled = true,
        };
        MaterialAssist.SetControlSize(_count, MaterialControlSize.ExtraSmall);
        MaterialSlider.SetShowStops(_count, false);
        AutomationProperties.SetName(_count, "Количество пунктов меню");
        _countPanel.Children.Add(_count);
        Settings.Children.Add(_countPanel);

        Settings.Children.Add(new TextBlock
        {
            Text = "Один живой экземпляр меню. Menu button открывает MenuFlyout (клик, Enter, Space, ↓), " +
                   "Context menu — нативный ContextMenu (клик или правая кнопка), Menu bar — Menu с вложенным " +
                   "подменю, Exposed dropdown / search — keyed ComboBox и поле поиска, Groups — три " +
                   "MaterialMenuGroup с формами Leading/Middle/Trailing. Стрелки, Home/End, Escape и " +
                   "доступные клавиши обрабатывает Avalonia. Поверхность Baseline использует плотность " +
                   "56/48/40 dp, Expressive — 16 dp радиус, формы пунктов и варианты Standard/Vibrant.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
        });

        _type.SelectionChanged += (_, _) => { if (!_syncing) Apply(); };
        _surface.SelectionChanged += (_, _) => { if (!_syncing) Apply(); };
        _variant.SelectionChanged += (_, _) => { if (!_syncing) Apply(); };
        _density.SelectionChanged += (_, _) => { if (!_syncing) Apply(); };
        foreach (var toggle in new[] { _scroll, _icons, _supporting, _divider, _active, _labels })
            toggle.IsCheckedChanged += (_, _) => { if (!_syncing) Apply(); };
        _count.PropertyChanged += (_, e) =>
        {
            if (e.Property == RangeBase.ValueProperty && !_syncing)
                Apply();
        };

        Apply();
    }

    private MaterialMenuSurfaceStyle Surface =>
        _surface.SelectedIndex == 0 ? MaterialMenuSurfaceStyle.Baseline : MaterialMenuSurfaceStyle.Expressive;

    private MaterialMenuVariant Variant =>
        Surface == MaterialMenuSurfaceStyle.Expressive && _variant.SelectedIndex == 1
            ? MaterialMenuVariant.Vibrant
            : MaterialMenuVariant.Standard;

    private MaterialMenuDensity Density => Surface != MaterialMenuSurfaceStyle.Baseline
        ? MaterialMenuDensity.Default
        : _density.SelectedIndex switch
        {
            1 => MaterialMenuDensity.Dense2,
            2 => MaterialMenuDensity.Dense4,
            _ => MaterialMenuDensity.Default,
        };

    private void Apply()
    {
        _syncing = true;
        CloseMenus();

        var type = Math.Clamp(_type.SelectedIndex, 0, 4);
        var isPopupMenu = type is 0 or 1 or 2;
        var isGroups = type == 4;
        var usesSurface = isPopupMenu || isGroups;

        var surface = Surface;
        var expressive = surface == MaterialMenuSurfaceStyle.Expressive;

        _surfacePanel.IsVisible = usesSurface;
        _variantPanel.IsVisible = usesSurface && expressive;
        _densityPanel.IsVisible = usesSurface && !expressive;
        _scroll.IsVisible = isPopupMenu;
        _icons.IsVisible = isPopupMenu || isGroups;
        _supporting.IsVisible = isPopupMenu || isGroups;
        _divider.IsVisible = isPopupMenu;
        _active.IsVisible = isPopupMenu;
        _labels.IsVisible = isPopupMenu || isGroups;
        _countPanel.IsVisible = isPopupMenu;
        _countCaption.Text = $"Пунктов в меню: {(int)Math.Round(_count.Value)}";

        switch (type)
        {
            case 1:
                _host.Content = EnsureContextMenu();
                _hint.Text = "Нажмите кнопку или откройте контекстное меню правой кнопкой / клавишей меню.";
                break;
            case 2:
                _host.Content = EnsureMenuBar();
                _hint.Text = "Откройте «Файл» или «Вид»: подменю и радиопункты сохраняют выбранное состояние.";
                break;
            case 3:
                _host.Content = EnsureExposedDropdown();
                _hint.Text = "Exposed dropdown сохраняет выбор ComboBox и IME; поле поиска фильтрует строки, ↑/↓/Enter/Escape работают из поля.";
                break;
            case 4:
                _host.Content = EnsureGroups();
                _hint.Text = "Три группы: Leading 16/8, Middle 8 и Trailing 8/16. Наведите и уведите курсор — углы переходят в inactive 8.";
                break;
            default:
                _host.Content = EnsureMenuButton();
                _hint.Text = "Нажмите кнопку, Enter, Space или ↓ — меню откроется как MenuFlyout.";
                break;
        }

        _syncing = false;
    }

    private void CloseMenus()
    {
        switch (_host.Content)
        {
            case MaterialMenuButton { Flyout: PopupFlyoutBase { IsOpen: true } flyout }:
                flyout.Hide();
                break;
            case Button { ContextMenu: { IsOpen: true } menu }:
                menu.Close();
                break;
        }

        foreach (var combo in _host.GetVisualDescendants().OfType<ComboBox>())
            combo.IsDropDownOpen = false;
    }

    private Control EnsureExposedDropdown()
    {
        if (_exposedPanel is not null)
            return _exposedPanel;

        var panel = new StackPanel { Spacing = 16, MinWidth = 260 };

        var dropdown = new ComboBox
        {
            Theme = FindTheme("MaterialExposedDropdownMenu"),
            ItemsSource = new[] { "Автоматически", "Светлая", "Тёмная" },
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        AutomationProperties.SetName(dropdown, "Тема оформления");
        panel.Children.Add(dropdown);

        var search = new MaterialMenuTextField
        {
            Label = "Поиск команды",
            PlaceholderText = "Введите название",
            SupportingText = "Стрелки и Enter работают, не покидая поле",
            MaxMenuHeight = 220,
        };
        search.Items.Add(new MaterialMenuTextFieldItem("Копировать", "Ctrl+C", new PathIcon { Data = Copy }));
        search.Items.Add(new MaterialMenuTextFieldItem("Вставить", "Ctrl+V"));
        search.Items.Add(new MaterialMenuTextFieldItem("Удалить", "Необратимо"));
        search.Items.Add(new MaterialMenuTextFieldItem("Свойства"));
        search.ItemInvoked += (_, args) => Report($"Поле меню: {args.Item.Label}");
        panel.Children.Add(search);

        // Reuse the same instance across inspector updates so search text and dropdown selection persist.
        _exposedPanel = panel;
        return panel;
    }

    private Control EnsureGroups()
    {
        var labels = new[] { "Первая", "Средняя", "Последняя" };
        if (_groupsCard is null)
        {
            var card = new Border
            {
                Padding = new Thickness(8),
                CornerRadius = new CornerRadius(16),
                MinWidth = 240,
            };
            card.Bind(Border.BackgroundProperty, card.GetResourceObservable("MaterialSurfaceContainerBrush"));

            var stack = new StackPanel { Spacing = 0 };
            var positions = new[]
            {
                MaterialMenuGroupPosition.Leading,
                MaterialMenuGroupPosition.Middle,
                MaterialMenuGroupPosition.Trailing,
            };
            for (var index = 0; index < positions.Length; index++)
            {
                var label = labels[index];
                var group = new MaterialMenuGroup { Header = label, Position = positions[index] };
                for (var row = 1; row <= 2; row++)
                {
                    var item = new MenuItem { Header = $"{label}: пункт {row}" };
                    MaterialMenu.SetItemPosition(item,
                        row == 1 ? MaterialMenuItemPosition.Leading : MaterialMenuItemPosition.Trailing);
                    item.Click += (_, _) => Report($"Группа: {label}");
                    group.Items.Add(item);
                }
                _groups[index] = group;
                stack.Children.Add(group);
            }
            card.Child = stack;
            _groupsCard = card;
        }

        Geometry[] groupIcons = [Edit, Copy, Share];
        for (var index = 0; index < _groups.Length; index++)
        {
            var group = _groups[index];
            MaterialMenu.SetSurfaceStyle(group, Surface);
            MaterialMenu.SetVariant(group, Variant);
            MaterialMenu.SetDensity(group, Density);
            group.Header = _labels.IsChecked == true ? labels[index] : null;
            var rowIndex = 0;
            foreach (var item in group.Items.OfType<MenuItem>())
            {
                item.Icon = _icons.IsChecked == true && rowIndex == 0
                    ? new PathIcon { Data = groupIcons[index % groupIcons.Length] }
                    : null;
                MaterialMenu.SetSupportingText(item,
                    _supporting.IsChecked == true && index == 0 && rowIndex == 0 ? "Второй строкой" : null);
                rowIndex++;
            }
        }
        return _groupsCard;
    }

    private Control EnsureMenuButton()
    {
        if (_menuButton is null || _flyout is null)
        {
            _flyout = new MaterialMenuFlyout
            {
                Placement = PlacementMode.Bottom,
                PlacementConstraintAdjustment = PopupPositionerConstraintAdjustment.FlipX
                    | PopupPositionerConstraintAdjustment.FlipY
                    | PopupPositionerConstraintAdjustment.SlideX
                    | PopupPositionerConstraintAdjustment.SlideY,
            };
            _menuButton = new MaterialMenuButton { Content = "Открыть меню", Flyout = _flyout };
            AutomationProperties.SetName(_menuButton, "Открыть меню");
        }

        MaterialMenu.SetVariant(_menuButton, Variant);
        _flyout.FlyoutPresenterTheme = FlyoutPresenterTheme();
        _flyout.Items.Clear();
        Populate(item => _flyout.Items.Add(item));
        return _menuButton;
    }

    private Control EnsureContextMenu()
    {
        if (_contextButton is null || _contextMenu is null)
        {
            _contextButton = new Button { Content = "Открыть контекстное меню" };
            MaterialAssist.SetButtonVariant(_contextButton, MaterialButtonVariant.Tonal);
            _contextMenu = new ContextMenu();
            MaterialMenu.SetAnimateContextMenu(_contextMenu, true);
            _contextButton.ContextMenu = _contextMenu;
            _contextButton.Click += (_, _) => _contextMenu.Open(_contextButton);
            AutomationProperties.SetName(_contextButton, "Открыть контекстное меню");
        }

        MaterialMenu.SetSurfaceStyle(_contextMenu, Surface);
        MaterialMenu.SetVariant(_contextMenu, Variant);
        MaterialMenu.SetDensity(_contextMenu, Density);
        MaterialMenu.SetShowScrollBar(_contextMenu, _scroll.IsChecked == true);
        _contextMenu.Items.Clear();
        Populate(item => _contextMenu.Items.Add(item));
        return _contextButton;
    }

    private Control EnsureMenuBar()
    {
        _menuBar ??= new Menu { HorizontalAlignment = HorizontalAlignment.Center };
        MaterialMenu.SetSurfaceStyle(_menuBar, Surface);
        MaterialMenu.SetVariant(_menuBar, Variant);
        MaterialMenu.SetDensity(_menuBar, Density);
        MaterialMenu.SetShowScrollBar(_menuBar, _scroll.IsChecked == true);

        var file = new MenuItem { Header = "Файл" };
        Populate(item => file.Items.Add(item));
        var view = new MenuItem { Header = "Вид" };
        var theme = new MenuItem { Header = "Тема" };
        MaterialMenu.SetItemPosition(theme, MaterialMenuItemPosition.Leading);
        var standard = new MenuItem
        {
            Header = "Standard",
            ToggleType = MenuItemToggleType.Radio,
            GroupName = "menu-bar-theme",
            IsChecked = !_menuBarVibrantChecked,
        };
        MaterialMenu.SetItemPosition(standard, MaterialMenuItemPosition.Leading);
        standard.Click += (_, _) =>
        {
            _menuBarVibrantChecked = false;
            Report("Тема: Standard");
        };
        var vibrant = new MenuItem
        {
            Header = "Vibrant",
            ToggleType = MenuItemToggleType.Radio,
            GroupName = "menu-bar-theme",
            IsChecked = _menuBarVibrantChecked,
        };
        MaterialMenu.SetItemPosition(vibrant, MaterialMenuItemPosition.Trailing);
        vibrant.Click += (_, _) =>
        {
            _menuBarVibrantChecked = true;
            Report("Тема: Vibrant");
        };
        theme.Items.Add(standard);
        theme.Items.Add(vibrant);
        view.Items.Add(theme);
        var viewProperties = Item("Свойства", null, null, Chevron);
        MaterialMenu.SetItemPosition(viewProperties, MaterialMenuItemPosition.Trailing);
        view.Items.Add(viewProperties);

        _menuBar.Items.Clear();
        _menuBar.Items.Add(file);
        _menuBar.Items.Add(view);
        return _menuBar;
    }

    /// <summary>
    /// The keyed presenter themes carry the surface anatomy; the derived theme adds the options
    /// that only exist per instance (density, scroll bar and the baseline/expressive switch).
    /// </summary>
    private ControlTheme FlyoutPresenterTheme()
    {
        var baseTheme = FindTheme(Variant == MaterialMenuVariant.Vibrant
            ? "MaterialMenuFlyoutPresenterVibrant"
            : "MaterialMenuFlyoutPresenterExpressive");
        var theme = new ControlTheme(typeof(MenuFlyoutPresenter)) { BasedOn = baseTheme };
        theme.Setters.Add(new Setter(MaterialMenu.SurfaceStyleProperty, Surface));
        theme.Setters.Add(new Setter(MaterialMenu.VariantProperty, Variant));
        theme.Setters.Add(new Setter(MaterialMenu.DensityProperty, Density));
        theme.Setters.Add(new Setter(MaterialMenu.ShowScrollBarProperty, _scroll.IsChecked == true));
        return theme;
    }

    private void Populate(Action<MenuItem> add)
    {
        if (_labels.IsChecked == true)
            add(SectionLabel("Действия"));

        var count = Math.Clamp((int)Math.Round(_count.Value), 3, 8);
        for (var index = 0; index < count; index++)
        {
            var header = Headers[index % Headers.Length];
            Geometry? icon = index switch
            {
                0 => Edit,
                1 => Copy,
                2 => Share,
                3 => Undo,
                4 => Chevron,
                5 => Gear,
                _ => null,
            };
            KeyGesture? gesture = index == 0 ? new KeyGesture(Key.O, KeyModifiers.Control) : null;
            var item = Item(header, gesture, _icons.IsChecked == true ? icon : null, null);
            MaterialMenu.SetItemPosition(item, count == 1
                ? MaterialMenuItemPosition.Standalone
                : index == 0
                    ? MaterialMenuItemPosition.Leading
                    : index == count - 1
                        ? MaterialMenuItemPosition.Trailing
                        : MaterialMenuItemPosition.Middle);
            if (_supporting.IsChecked == true && index == 0)
                MaterialMenu.SetSupportingText(item, "Второй строкой");
            if (_divider.IsChecked == true && index == 0)
                MaterialMenu.SetShowDivider(item, true);
            if (_active.IsChecked == true && index == 1)
                MaterialMenu.SetIsActive(item, true);
            add(item);
        }

        add(SeparatorItem());

        if (_labels.IsChecked == true)
            add(SectionLabel("Вид"));
        var grid = new MenuItem
        {
            Header = "Показать сетку",
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = _showGridChecked,
        };
        MaterialMenu.SetDensity(grid, Density);
        MaterialMenu.SetItemPosition(grid, MaterialMenuItemPosition.Leading);
        grid.Click += (_, _) =>
        {
            _showGridChecked = grid.IsChecked;
            Report($"Показать сетку: {(grid.IsChecked ? "вкл" : "выкл")}");
        };
        add(grid);

        var more = new MenuItem { Header = "Ещё" };
        MaterialMenu.SetDensity(more, Density);
        MaterialMenu.SetItemPosition(more, MaterialMenuItemPosition.Middle);
        var move = Item("Переместить", null, null, null);
        MaterialMenu.SetItemPosition(move, MaterialMenuItemPosition.Leading);
        var archive = Item("В архив", null, null, null);
        MaterialMenu.SetItemPosition(archive, MaterialMenuItemPosition.Trailing);
        var delete = Item("Удалить", null, null, null);
        MaterialMenu.SetItemPosition(delete, MaterialMenuItemPosition.Trailing);
        more.Items.Add(move);
        more.Items.Add(archive);
        more.Items.Add(SeparatorItem());
        more.Items.Add(delete);
        add(more);

        var properties = Item("Свойства", null, null, Chevron);
        MaterialMenu.SetItemPosition(properties, MaterialMenuItemPosition.Trailing);
        add(properties);
    }

    private MenuItem Item(string header, KeyGesture? gesture, Geometry? icon, Geometry? trailing)
    {
        var item = new MenuItem
        {
            Header = header,
            InputGesture = gesture,
            Icon = icon is null ? null : new PathIcon { Data = icon },
        };
        MaterialMenu.SetDensity(item, Density);
        MaterialMenu.SetTrailingIcon(item, trailing is null ? null : new PathIcon { Data = trailing });
        item.Click += (_, _) => Report($"Команда выполнена: {header}");
        return item;
    }

    private static MenuItem SectionLabel(string header)
    {
        var item = new MenuItem { Header = header, IsEnabled = false, Focusable = false };
        MaterialMenu.SetIsSectionLabel(item, true);
        return item;
    }

    private static MenuItem SeparatorItem() => new() { Header = "-" };

    private void Report(string message) => _readout.Text = message;

    private static ToggleSwitch Toggle(StackPanel host, string label, bool value)
    {
        var toggle = new ToggleSwitch { Content = label, IsChecked = value };
        host.Children.Add(toggle);
        return toggle;
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

    private static ControlTheme FindTheme(string key) =>
        (ControlTheme)Application.Current!.FindResource(key)!;
}
