using System;
using System.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Catalog;

/// <summary>
/// One live FAB family member - a regular FAB, an extended FAB or a FAB menu - selected in the
/// inspector, with its size, color role, icon and interaction exposed as settings.
/// </summary>
public static class FabGallery
{
    public static Control Create() => new FabPlayground();
}

internal sealed class FabPlayground : Grid, IComponentPlayground
{
    private const string PlusIcon = "M11,5 L13,5 L13,11 L19,11 L19,13 L13,13 L13,19 L11,19 L11,13 L5,13 L5,11 L11,11 Z";
    private const string EditIcon = "M3,17.25 L3,21 L6.75,21 L17.81,9.94 L14.06,6.19 L3,17.25 Z M20.71,7.04 C21.1,6.65 21.1,6.02 20.71,5.63 L18.37,3.29 C17.98,2.9 17.35,2.9 16.96,3.29 L15.13,5.12 L18.88,8.87 L20.71,7.04 Z";
    private const string ShareIcon = "M18,16.08 C17.24,16.08 16.56,16.38 16.04,16.85 L8.91,12.7 C8.96,12.47 9,12.24 9,12 C9,11.76 8.96,11.53 8.91,11.3 L15.96,7.19 C16.5,7.69 17.21,8 18,8 C19.66,8 21,6.66 21,5 C21,3.34 19.66,2 18,2 C16.34,2 15,3.34 15,5 C15,5.24 15.04,5.47 15.09,5.7 L8.04,9.81 C7.5,9.31 6.79,9 6,9 C4.34,9 3,10.34 3,12 C3,13.66 4.34,15 6,15 C6.79,15 7.5,14.69 8.04,14.19 L15.16,18.35 C15.11,18.56 15.08,18.78 15.08,19 C15.08,20.61 16.39,21.92 18,21.92 C19.61,21.92 20.92,20.61 20.92,19 C20.92,17.39 19.61,16.08 18,16.08 Z";
    private const string DeleteIcon = "M6,19 C6,20.1 6.9,21 8,21 L16,21 C17.1,21 18,20.1 18,19 L18,7 L6,7 L6,19 Z M19,4 L15.5,4 L14.5,3 L9.5,3 L8.5,4 L5,4 L5,6 L19,6 L19,4 Z";
    private const string FavoriteIcon = "M12,21.35 L10.55,20.03 C5.4,15.36 2,12.28 2,8.5 C2,5.42 4.42,3 7.5,3 C9.24,3 10.91,3.81 12,5.09 C13.09,3.81 14.76,3 16.5,3 C19.58,3 22,5.42 22,8.5 C22,12.28 18.6,15.36 13.45,20.04 L12,21.35 Z";
    private const string CameraIcon = "M12,15.2 A3.2,3.2 0 1 0 12,8.8 A3.2,3.2 0 1 0 12,15.2 Z M9,2 L7.17,4 L4,4 C2.9,4 2,4.9 2,6 L2,18 C2,19.1 2.9,20 4,20 L20,20 C21.1,20 22,19.1 22,18 L22,6 C22,4.9 21.1,4 20,4 L16.83,4 L15,2 L9,2 Z";
    private const string BookmarkIcon = "M17,3 L7,3 C5.9,3 5,3.9 5,5 L5,21 L12,18 L19,21 L19,5 C19,3.9 18.1,3 17,3 Z";

    private static readonly string[] FabRoleNames = Enum.GetNames<MaterialFabColorRole>();
    private static readonly string[] MenuRoleNames = ["Primary", "Secondary", "Tertiary"];
    private static readonly string[] IconNames = ["Плюс", "Изменить", "Поделиться", "Удалить", "Избранное", "Съёмка", "Закладка"];
    private static readonly Geometry[] IconData =
    [
        Geometry.Parse(PlusIcon),
        Geometry.Parse(EditIcon),
        Geometry.Parse(ShareIcon),
        Geometry.Parse(DeleteIcon),
        Geometry.Parse(FavoriteIcon),
        Geometry.Parse(CameraIcon),
        Geometry.Parse(BookmarkIcon),
    ];

    private static readonly string[] SegmentLabels = ["Изменить", "Поделиться", "Удалить", "В избранное", "Съёмка", "В закладки"];
    private static readonly Geometry[] SegmentIcons = [IconData[1], IconData[2], IconData[3], IconData[4], IconData[5], IconData[6]];

    private readonly MaterialFab _fab = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly MaterialExtendedFab _extended = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly PathIcon[] _fabGlyphs = IconData.Select(data => new PathIcon { Data = data }).ToArray();
    private readonly MaterialFabMenu _menu = new() { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom };
    private readonly Grid _menuHost;
    private readonly Grid _stageContent;
    private readonly TextBlock _status = new()
    {
        Text = "Действий ещё не было.",
        TextWrapping = TextWrapping.Wrap,
        TextAlignment = TextAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Center,
        Opacity = 0.8,
    };

    private readonly MaterialButtonGroup _kind;
    private readonly MaterialButtonGroup _mode;
    private readonly ToggleSwitch _showIcon;
    private readonly MaterialButtonGroup _size;
    private readonly ComboBox _role;
    private readonly ComboBox _icon;
    private readonly TextBox _label;
    private readonly ToggleSwitch _expanded;
    private readonly ToggleSwitch _enabled;
    private readonly ToggleSwitch _rtl;
    private readonly Slider _actions;
    private readonly ToggleSwitch _openMenu;
    private int _lastKind = -1;
    private int _colorFamily;
    private bool _solidRole;
    private bool _syncing;

    public StackPanel Settings { get; } = InspectorLayout.CreatePane();

    internal FabPlayground()
    {
        _menuHost = new Grid
        {
            Width = 280,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 16),
            Children = { _menu },
        };
        _stageContent = new Grid { Children = { _fab, _extended, _menuHost } };
        var stage = new Border
        {
            Height = 480,
            MinWidth = 360,
            MaxWidth = 720,
            CornerRadius = new CornerRadius(16),
            ClipToBounds = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Child = _stageContent,
        };
        stage.Bind(Border.BackgroundProperty, stage.GetResourceObservable("MaterialSurfaceContainerBrush"));

        var stageColumn = new StackPanel
        {
            Spacing = 16,
            MaxWidth = 720,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { stage, _status },
        };
        Children.Add(stageColumn);

        var configSection = InspectorLayout.Section(Settings, "Конфигурация", divider: false);
        _kind = InspectorLayout.Choice(configSection, "Компонент", ["FAB", "Extended FAB", "FAB menu"], 0);
        _mode = InspectorLayout.Choice(configSection, "Режим Extended FAB", ["Expressive", "Classic"], 0);
        _size = InspectorLayout.Choice(configSection, "Размер", ["40", "56", "80", "96"], 1);
        _role = InspectorLayout.Combo(configSection, "Цветовая роль", FabRoleNames, 0);

        var contentSection = InspectorLayout.Section(Settings, "Содержимое");
        _icon = InspectorLayout.Combo(contentSection, "Иконка", IconNames, 0);
        _label = InspectorLayout.TextField(contentSection, "Текст extended FAB", "Создать");
        _actions = InspectorLayout.Slider(contentSection, "Действий в меню", 2, 6, 3, 1, format: "0");
        AutomationProperties.SetName(_actions, "Количество действий в меню");

        var stateSection = InspectorLayout.Section(Settings, "Состояния");
        var stateSwitches = InspectorLayout.Switches(stateSection);
        _showIcon = InspectorLayout.Switch(stateSwitches, "С иконкой", true);
        _expanded = InspectorLayout.Switch(stateSwitches, "Label развёрнут", true);
        _openMenu = InspectorLayout.Switch(stateSwitches, "Меню раскрыто", false);
        _enabled = InspectorLayout.Switch(stateSwitches, "Доступен", true);
        _rtl = InspectorLayout.Switch(stateSwitches, "Справа налево (RTL)", false);

        InspectorLayout.Note(
            Settings,
            "FAB — обычная кнопка: Enter, Space и указатель дают настоящие состояния. " +
            "Классический Extended FAB — 56 в высоту с иконкой 24 и минимумом 80; " +
            "без иконки это text-only вариант, который не сворачивается. " +
            "У FAB menu стрелки открывают список и перемещают фокус, Escape закрывает меню " +
            "и возвращает фокус на триггер, а пункты выполняют команды.");

        _fab.Click += (_, _) => _status.Text = $"FAB нажат: {_fab.ColorRole}, {_fab.Size}.";
        _extended.Click += (_, _) => _status.Text = $"Extended FAB нажат: {_extended.Label}.";
        _menu.Expanded += (_, _) => _status.Text = "Меню раскрыто.";
        _menu.Collapsed += (_, _) => _status.Text = "Меню свёрнуто.";
        _menu.PropertyChanged += (_, e) =>
        {
            if (e.Property != MaterialFabMenu.IsExpandedProperty || _syncing)
                return;
            _syncing = true;
            _openMenu.IsChecked = _menu.IsExpanded;
            _syncing = false;
        };

        foreach (var choice in new[] { _kind, _mode, _size })
            choice.SelectionChanged += (_, _) => { if (!_syncing) Apply(); };
        _role.SelectionChanged += (_, _) => { if (!_syncing) Apply(); };
        _icon.SelectionChanged += (_, _) => { if (!_syncing) Apply(); };
        _label.TextChanged += (_, _) => { if (!_syncing) Apply(); };
        _actions.PropertyChanged += (_, e) =>
        {
            if (e.Property == RangeBase.ValueProperty && !_syncing)
                Apply();
        };
        foreach (var toggle in new[] { _expanded, _showIcon, _enabled, _rtl, _openMenu })
            toggle.IsCheckedChanged += (_, _) => { if (!_syncing) Apply(); };

        Apply();
    }

    private void Apply()
    {
        var kind = Math.Clamp(_kind.SelectedIndex, 0, 2);
        var iconIndex = Math.Clamp(_icon.SelectedIndex, 0, IconData.Length - 1);
        var icon = IconData[iconIndex];
        var label = string.IsNullOrWhiteSpace(_label.Text) ? "Действие" : _label.Text!;
        var enabled = _enabled.IsChecked == true;
        var mode = _mode.SelectedIndex == 1 ? MaterialExtendedFabMode.Classic : MaterialExtendedFabMode.Expressive;
        var hasIcon = _showIcon.IsChecked == true;

        if (kind != _lastKind)
        {
            var wasMenu = _lastKind == 2;
            var isMenu = kind == 2;
            if (_lastKind >= 0 && wasMenu != isMenu)
            {
                _syncing = true;
                if (isMenu)
                {
                    var selectedRole = Math.Clamp(_role.SelectedIndex, 0, 5);
                    _colorFamily = selectedRole % 3;
                    _solidRole = selectedRole >= 3;
                    _role.ItemsSource = MenuRoleNames;
                    _role.SelectedIndex = _colorFamily;
                }
                else
                {
                    _colorFamily = Math.Clamp(_role.SelectedIndex, 0, 2);
                    _role.ItemsSource = FabRoleNames;
                    _role.SelectedIndex = (_solidRole ? 3 : 0) + _colorFamily;
                }
                _syncing = false;
            }

            // The regular FAB has four source sizes (40/56/80/96); the extended FAB and FAB menu
            // triggers use the three Expressive heights (56/80/96). Selecting a family keeps the
            // closest source height instead of reusing a numeric index.
            var previousHeight = _lastKind switch
            {
                0 => _size.SelectedIndex switch { 0 => 40, 1 => 56, 2 => 80, _ => 96 },
                1 or 2 => _size.SelectedIndex switch { 0 => 56, 1 => 80, _ => 96 },
                _ => 56,
            };
            var labels = kind == 0 ? new[] { "40", "56", "80", "96" } : new[] { "56", "80", "96" };
            for (var i = 0; i < _size.Items.Count; i++)
            {
                if (_size.Items[i] is not MaterialButtonGroupItem item)
                    continue;
                item.Content = i < labels.Length ? labels[i] : null;
                item.IsVisible = i < labels.Length;
            }

            int mapped;
            if (_lastKind < 0)
                mapped = Math.Clamp(_size.SelectedIndex, 0, labels.Length - 1);
            else if (kind != 0)
                mapped = previousHeight <= 56 ? 0 : previousHeight <= 80 ? 1 : 2;
            else
                mapped = previousHeight <= 40 ? 0
                    : previousHeight <= 56 ? 1
                    : previousHeight <= 80 ? 2
                    : 3;
            _lastKind = kind;
            _syncing = true;
            _size.SelectedIndex = mapped;
            _syncing = false;
        }

        MaterialFabColorRole role;
        if (kind == 2)
        {
            _colorFamily = Math.Clamp(_role.SelectedIndex, 0, 2);
            role = (MaterialFabColorRole)((_solidRole ? 3 : 0) + _colorFamily);
        }
        else
        {
            var selectedRole = Math.Clamp(_role.SelectedIndex, 0, 5);
            _colorFamily = selectedRole % 3;
            _solidRole = selectedRole >= 3;
            role = (MaterialFabColorRole)selectedRole;
        }

        var size = Math.Clamp(_size.SelectedIndex, 0, kind == 0 ? 3 : 2);

        InspectorLayout.ApplyFlowDirection(_stageContent, _rtl);
        _fab.IsVisible = kind == 0;
        _extended.IsVisible = kind == 1;
        _menuHost.IsVisible = kind == 2;
        _size.IsVisible = !(kind == 1 && mode == MaterialExtendedFabMode.Classic);
        _mode.IsVisible = kind == 1;
        _label.IsVisible = kind == 1;
        _expanded.IsVisible = kind == 1 && hasIcon;
        _showIcon.IsVisible = kind == 1;
        _actions.IsVisible = kind == 2;
        _openMenu.IsVisible = kind == 2;

        _fab.Size = (MaterialFabSize)size;
        _fab.ColorRole = role;
        _fab.Content = _fabGlyphs[iconIndex];
        _fab.IsEnabled = enabled;
        AutomationProperties.SetName(_fab, $"FAB, {role}, {_fab.Size}");

        _extended.Mode = mode;
        _extended.Size = (MaterialExtendedFabSize)size;
        _extended.ColorRole = role;
        _extended.Icon = hasIcon ? icon : null;
        _extended.Label = label;
        _extended.IsExpanded = hasIcon && _expanded.IsChecked == true;
        _extended.IsEnabled = enabled;

        _menu.ColorRole = (MaterialFabMenuColorRole)_colorFamily;
        _menu.TriggerSize = size switch
        {
            1 => MaterialFabSize.Medium,
            2 => MaterialFabSize.Large,
            _ => MaterialFabSize.Default,
        };
        _menu.TriggerIcon = icon;
        _menu.IsEnabled = enabled;
        SyncMenu();
        if (!_syncing)
        {
            _syncing = true;
            if (_menu.IsExpanded != (_openMenu.IsChecked == true))
                _menu.IsExpanded = _openMenu.IsChecked == true;
            _syncing = false;
        }
    }

    private void SyncMenu()
    {
        var count = (int)Math.Round(_actions.Value);
        while (_menu.Items.Count > count)
            _menu.Items.RemoveAt(_menu.Items.Count - 1);
        while (_menu.Items.Count < count)
        {
            var segment = new MaterialFabMenuSegment { HorizontalAlignment = HorizontalAlignment.Right };
            segment.Command = new ActionCommand(() => _status.Text = $"Меню: {segment.Label} выполнено.");
            _menu.Items.Add(segment);
        }

        for (var i = 0; i < count; i++)
        {
            var segment = (MaterialFabMenuSegment)_menu.Items[i]!;
            segment.Label = SegmentLabels[i];
            segment.Icon = SegmentIcons[i];
        }
    }

    private sealed class ActionCommand(Action action) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => action();
    }
}
