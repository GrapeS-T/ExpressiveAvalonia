using System;
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
/// One live app bar family member - an app bar, a bottom app bar, a toolbar, a section header or
/// a page header - selected in the inspector, with its arrangement, labels and actions exposed
/// as settings.
/// </summary>
public static class AppBarsGallery
{
    public static Control Create() => new AppBarsPlayground();
}

internal sealed class AppBarsPlayground : Grid, IComponentPlayground
{
    private static readonly string[] ComponentNames =
        ["App bar", "Bottom app bar", "Toolbar", "Section header", "Page header"];

    private static readonly string[] ActionLabels =
        ["Поиск", "Избранное", "Поделиться", "Изменить", "Настройки", "Палитра"];

    private static readonly Geometry[] ActionIcons =
    [
        Geometry.Parse("M15.5 14h-.79l-.28-.27A6.471 6.471 0 0 0 16 9.5 6.5 6.5 0 1 0 9.5 16c1.61 0 3.09-.59 4.23-1.57l.27.28v.79l5 4.99L20.49 19l-4.99-5zm-6 0C7.01 14 5 11.99 5 9.5S7.01 5 9.5 5 14 7.01 14 9.5 11.99 14 9.5 14z"),
        Geometry.Parse("M12 21.35l-1.45-1.32C5.4 15.36 2 12.28 2 8.5 2 5.42 4.42 3 7.5 3c1.74 0 3.41.81 4.5 2.09C13.09 3.81 14.76 3 16.5 3 19.58 3 22 5.42 22 8.5c0 3.78-3.4 6.86-8.55 11.54L12 21.35z"),
        Geometry.Parse("M18 16.08c-.76 0-1.44.3-1.96.77L8.91 12.7c.05-.23.09-.46.09-.7s-.04-.47-.09-.7l7.05-4.11c.54.5 1.25.81 2.04.81 1.66 0 3-1.34 3-3s-1.34-3-3-3-3 1.34-3 3c0 .24.04.47.09.7L8.04 9.81C7.5 9.31 6.79 9 6 9c-1.66 0-3 1.34-3 3s1.34 3 3 3c.79 0 1.5-.31 2.04-.81l7.12 4.16c-.05.21-.08.43-.08.65 0 1.61 1.31 2.92 2.92 2.92s2.92-1.31 2.92-2.92-1.31-2.92-2.92-2.92z"),
        Geometry.Parse("M3 17.25V21h3.75L17.81 9.94l-3.75-3.75L3 17.25zM20.71 7.04a.9959.9959 0 0 0 0-1.41l-2.34-2.34a.9959.9959 0 0 0-1.41 0l-1.83 1.83 3.75 3.75 1.83-1.83z"),
        Geometry.Parse("M19.14 12.94c.04-.3.06-.61.06-.94 0-.32-.02-.64-.07-.94l2.03-1.58c.18-.14.23-.41.12-.61l-1.92-3.32c-.12-.22-.37-.29-.59-.22l-2.39.96c-.5-.38-1.03-.7-1.62-.94l-.36-2.54c-.04-.24-.24-.41-.48-.41h-3.84c-.24 0-.43.17-.47.41l-.36 2.54c-.59.24-1.13.57-1.62.94l-2.39-.96c-.22-.08-.47 0-.59.22L2.74 8.87c-.12.21-.08.47.12.61l2.03 1.58c-.05.3-.09.63-.09.94s.02.64.07.94l-2.03 1.58c-.18.14-.23.41-.12.61l1.92 3.32c.12.22.37.29.59.22l2.39-.96c.5.38 1.03.7 1.62.94l.36 2.54c.05.24.24.41.48.41h3.84c.24 0 .44-.17.47-.41l.36-2.54c.59-.24 1.13-.56 1.62-.94l2.39.96c.22.08.47 0 .59-.22l1.92-3.32c.12-.22.07-.47-.12-.61l-2.01-1.58zM12 15.6c-1.98 0-3.6-1.62-3.6-3.6s1.62-3.6 3.6-3.6 3.6 1.62 3.6 3.6-1.62 3.6-3.6 3.6z"),
        Geometry.Parse("M12 3c-4.97 0-9 4.03-9 9s4.03 9 9 9c.83 0 1.5-.67 1.5-1.5 0-.39-.15-.74-.39-1.01-.23-.26-.38-.61-.38-.99 0-.83.67-1.5 1.5-1.5H16c2.76 0 5-2.24 5-5 0-4.42-4.03-8-9-8zm-5.5 9c-.83 0-1.5-.67-1.5-1.5S5.67 9 6.5 9 8 9.67 8 10.5 7.33 12 6.5 12zm3-4C8.67 8 8 7.33 8 6.5S8.67 5 9.5 5s1.5.67 1.5 1.5S10.33 8 9.5 8zm5 0c-.83 0-1.5-.67-1.5-1.5S13.67 5 14.5 5s1.5.67 1.5 1.5S15.33 8 14.5 8zm3 4c-.83 0-1.5-.67-1.5-1.5S16.67 9 17.5 9s1.5.67 1.5 1.5-.67 1.5-1.5 1.5z"),
    ];

    private static readonly Geometry Back = Geometry.Parse("M20 11H7.83l5.59-5.59L12 4l-8 8 8 8 1.41-1.41L7.83 13H20v-2z");
    private static readonly Geometry Menu = Geometry.Parse("M3 18h18v-2H3v2zm0-5h18v-2H3v2zm0-7v2h18V6H3z");
    private static readonly Geometry Add = Geometry.Parse("M19 13h-6v6h-2v-6H5v-2h6V5h2v6h6v2z");
    private static readonly Geometry Person = Geometry.Parse("M12 12c2.21 0 4-1.79 4-4s-1.79-4-4-4-4 1.79-4 4 1.79 4 4 4zm0 2c-2.67 0-8 1.34-8 4v2h16v-2c0-2.66-5.33-4-8-4z");

    private readonly MaterialAppBar _appBar = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ScrollViewer _appBarScroll;
    private readonly StackPanel _appBarStage;
    private readonly MaterialBottomAppBar _bottomBar = new() { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Center };
    private readonly MaterialToolbar _toolbar = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly MaterialSectionHeader _sectionHeader = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly MaterialPageHeader _pageHeader = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly MaterialFab _bottomFab;
    private readonly Control _thumbnail;
    private readonly Control _personAvatar;
    private readonly PathIcon _sectionIcon = new() { Data = ActionIcons[5], Width = 20, Height = 20 };
    private readonly TextBlock _sectionCounter = new() { Text = "12", FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _status = new()
    {
        Text = "Действий: 0",
        TextWrapping = TextWrapping.Wrap,
        TextAlignment = TextAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Center,
        Opacity = 0.8,
    };

    private readonly ComboBox _component;

    private readonly StackPanel _appBarGroup;
    private readonly ComboBox _appBarConfiguration;
    private readonly TextBox _appBarTitle;
    private readonly TextBox _appBarSubtitle;
    private readonly ComboBox _appBarElevation;
    private readonly ComboBox _appBarAvatar;
    private readonly ToggleSwitch _appBarLeading;
    private readonly Slider _appBarActions;
    private readonly Slider _appBarVisible;

    private readonly StackPanel _bottomGroup;
    private readonly Slider _bottomActions;
    private readonly ToggleSwitch _bottomFabToggle;

    private readonly StackPanel _toolbarGroup;
    private readonly ComboBox _toolbarConfiguration;
    private readonly ComboBox _toolbarOrientation;
    private readonly ComboBox _toolbarColor;
    private readonly Slider _toolbarActions;

    private readonly StackPanel _sectionGroup;
    private readonly TextBox _sectionLabel;
    private readonly ToggleSwitch _sectionLeading;
    private readonly ToggleSwitch _sectionTrailing;

    private readonly StackPanel _pageGroup;
    private readonly TextBox _pageTitle;
    private readonly TextBox _pageDefinition;
    private readonly TextBox _pageLink;
    private readonly ToggleSwitch _pageDivider;

    private int _invocations;

    public StackPanel Settings { get; } = new() { Spacing = 16 };

    internal AppBarsPlayground()
    {
        var list = new StackPanel { Spacing = 4, Margin = new Thickness(8) };
        for (var i = 1; i <= 20; i++)
            list.Children.Add(new TextBlock { Text = $"Строка {i}", Margin = new Thickness(16, 8) });
        _appBarScroll = new ScrollViewer { Content = list, Height = 200, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        _appBarStage = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { _appBar, _appBarScroll } };
        _appBar.ScrollSource = _appBarScroll;

        _bottomFab = new MaterialFab
        {
            Content = new PathIcon { Data = Add },
            ColorRole = MaterialFabColorRole.PrimaryContainer,
            Command = Command("Создать"),
        };
        AutomationProperties.SetName(_bottomFab, "Создать");
        _bottomBar.Fab = _bottomFab;
        _thumbnail = Thumbnail("Витрина");
        _personAvatar = PersonAvatar();

        _pageHeader.Logo = new PathIcon { Data = ActionIcons[5], Width = 40, Height = 40 };
        _pageHeader.LinkCommand = Command("See design guideline");

        var stageContent = new Grid
        {
            Children = { _appBarStage, _bottomBar, _toolbar, _sectionHeader, _pageHeader },
        };
        var stage = new Border
        {
            Height = 560,
            MinWidth = 360,
            MaxWidth = 720,
            Padding = new Thickness(24),
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

        _appBarGroup = new StackPanel { Spacing = 12 };
        _appBarGroup.Children.Add(new TextBlock { Text = "App bar", FontSize = 16, FontWeight = FontWeight.Bold });
        _appBarGroup.Children.Add(new TextBlock { Text = "Конфигурация" });
        _appBarConfiguration = new ComboBox
        {
            ItemsSource = new[] { "Small", "Small-centred", "Small-image", "Search", "Medium", "Large" },
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        AutomationProperties.SetName(_appBarConfiguration, "Конфигурация app bar");
        _appBarGroup.Children.Add(_appBarConfiguration);
        _appBarTitle = Field(_appBarGroup, "Заголовок", "Входящие");
        _appBarSubtitle = Field(_appBarGroup, "Подпись", "Поддерживающий текст");
        _appBarGroup.Children.Add(new TextBlock { Text = "Тень" });
        _appBarElevation = new ComboBox
        {
            ItemsSource = new[] { "Flat", "On-scroll" },
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        AutomationProperties.SetName(_appBarElevation, "Тень app bar");
        _appBarGroup.Children.Add(_appBarElevation);
        _appBarGroup.Children.Add(new TextBlock { Text = "Аватар" });
        _appBarAvatar = new ComboBox
        {
            ItemsSource = new[] { "Нет", "Монограмма", "Аватар" },
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        AutomationProperties.SetName(_appBarAvatar, "Аватар app bar");
        _appBarGroup.Children.Add(_appBarAvatar);
        _appBarLeading = Toggle(_appBarGroup, "Ведущая иконка", true);
        _appBarActions = Number(_appBarGroup, "Действий", 0, 6, 2);
        _appBarVisible = Number(_appBarGroup, "Видимых действий", 0, 4, 3);
        Settings.Children.Add(_appBarGroup);

        _bottomGroup = new StackPanel { Spacing = 12 };
        _bottomGroup.Children.Add(new TextBlock { Text = "Bottom app bar", FontSize = 16, FontWeight = FontWeight.Bold });
        _bottomActions = Number(_bottomGroup, "Действий", 0, 6, 4);
        _bottomFabToggle = Toggle(_bottomGroup, "FAB в трейлинге", true);
        Settings.Children.Add(_bottomGroup);

        _toolbarGroup = new StackPanel { Spacing = 12 };
        _toolbarGroup.Children.Add(new TextBlock { Text = "Toolbar", FontSize = 16, FontWeight = FontWeight.Bold });
        _toolbarGroup.Children.Add(new TextBlock { Text = "Конфигурация" });
        _toolbarConfiguration = new ComboBox
        {
            ItemsSource = new[] { "Docked", "Floating" },
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        AutomationProperties.SetName(_toolbarConfiguration, "Конфигурация toolbar");
        _toolbarGroup.Children.Add(_toolbarConfiguration);
        _toolbarGroup.Children.Add(new TextBlock { Text = "Ориентация" });
        _toolbarOrientation = new ComboBox
        {
            ItemsSource = new[] { "Горизонтальная", "Вертикальная" },
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        AutomationProperties.SetName(_toolbarOrientation, "Ориентация toolbar");
        _toolbarGroup.Children.Add(_toolbarOrientation);
        _toolbarGroup.Children.Add(new TextBlock { Text = "Цвет" });
        _toolbarColor = new ComboBox
        {
            ItemsSource = new[] { "Standard", "Vibrant" },
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        AutomationProperties.SetName(_toolbarColor, "Цвет toolbar");
        _toolbarGroup.Children.Add(_toolbarColor);
        _toolbarActions = Number(_toolbarGroup, "Действий", 0, 6, 6);
        Settings.Children.Add(_toolbarGroup);

        _sectionGroup = new StackPanel { Spacing = 12 };
        _sectionGroup.Children.Add(new TextBlock { Text = "Section header", FontSize = 16, FontWeight = FontWeight.Bold });
        _sectionLabel = Field(_sectionGroup, "Заголовок", "Компоненты");
        _sectionLeading = Toggle(_sectionGroup, "Ведущая иконка", true);
        _sectionTrailing = Toggle(_sectionGroup, "Счётчик справа", true);
        Settings.Children.Add(_sectionGroup);

        _pageGroup = new StackPanel { Spacing = 12 };
        _pageGroup.Children.Add(new TextBlock { Text = "Page header", FontSize = 16, FontWeight = FontWeight.Bold });
        _pageTitle = Field(_pageGroup, "Заголовок", "Typography");
        _pageDefinition = Field(_pageGroup, "Определение",
            "Use typography to present your design and content as clearly and efficiently as possible.");
        _pageLink = Field(_pageGroup, "Текст ссылки", "See design guideline");
        _pageDivider = Toggle(_pageGroup, "Разделитель", true);
        Settings.Children.Add(_pageGroup);

        Settings.Children.Add(new TextBlock
        {
            Text = "Tab и стрелки перемещают фокус между действиями, Home/End — к краям, Enter/Space " +
                   "выполняют команду, Escape закрывает overflow. Действия сверх лимита уходят в меню " +
                   "«ещё»; On-scroll поднимает тень, когда список прокручивается под полосой.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
        });

        _appBar.SearchSubmitted += (_, _) => _status.Text = $"Поиск: «{_appBar.SearchText}»";
        _appBar.SearchDismissed += (_, _) => _status.Text = "Поиск закрыт (Escape)";

        _component.SelectionChanged += (_, _) => Apply();
        _appBarConfiguration.SelectionChanged += (_, _) => Apply();
        _appBarTitle.TextChanged += (_, _) => Apply();
        _appBarSubtitle.TextChanged += (_, _) => Apply();
        _appBarElevation.SelectionChanged += (_, _) => Apply();
        _appBarAvatar.SelectionChanged += (_, _) => Apply();
        _toolbarConfiguration.SelectionChanged += (_, _) => Apply();
        _toolbarOrientation.SelectionChanged += (_, _) => Apply();
        _toolbarColor.SelectionChanged += (_, _) => Apply();
        _sectionLabel.TextChanged += (_, _) => Apply();
        _pageTitle.TextChanged += (_, _) => Apply();
        _pageDefinition.TextChanged += (_, _) => Apply();
        _pageLink.TextChanged += (_, _) => Apply();
        foreach (var slider in new[] { _appBarActions, _appBarVisible, _bottomActions, _toolbarActions })
            slider.PropertyChanged += (_, e) => { if (e.Property == Slider.ValueProperty) Apply(); };
        foreach (var toggle in new[]
                 {
                     _appBarLeading, _bottomFabToggle, _sectionLeading, _sectionTrailing, _pageDivider,
                 })
            toggle.IsCheckedChanged += (_, _) => Apply();

        Apply();
    }

    private void Apply()
    {
        var component = Math.Clamp(_component.SelectedIndex, 0, ComponentNames.Length - 1);

        _appBarStage.IsVisible = component == 0;
        _bottomBar.IsVisible = component == 1;
        _toolbar.IsVisible = component == 2;
        _sectionHeader.IsVisible = component == 3;
        _pageHeader.IsVisible = component == 4;

        _appBarGroup.IsVisible = component == 0;
        _bottomGroup.IsVisible = component == 1;
        _toolbarGroup.IsVisible = component == 2;
        _sectionGroup.IsVisible = component == 3;
        _pageGroup.IsVisible = component == 4;

        ApplyAppBar();
        ApplyBottomBar();
        ApplyToolbar();
        ApplyHeaders();
    }

    private void ApplyAppBar()
    {
        var configuration = (MaterialAppBarConfiguration)Math.Clamp(_appBarConfiguration.SelectedIndex, 0, 5);
        var elevation = _appBarElevation.SelectedIndex == 1
            ? MaterialAppBarElevation.OnScroll
            : MaterialAppBarElevation.Flat;
        var title = string.IsNullOrWhiteSpace(_appBarTitle.Text) ? "Заголовок" : _appBarTitle.Text;
        var subtitle = string.IsNullOrWhiteSpace(_appBarSubtitle.Text) ? null : _appBarSubtitle.Text;
        var hasLeading = _appBarLeading.IsChecked == true;

        _appBar.Configuration = configuration;
        _appBar.Elevation = elevation;
        _appBar.Title = title;
        _appBar.Subtitle = subtitle;
        _appBar.SearchPlaceholder = "Поиск продукта";
        _appBar.LeadingIcon = hasLeading
            ? (configuration is MaterialAppBarConfiguration.SmallCentered or MaterialAppBarConfiguration.Search ? Menu : Back)
            : null;
        _appBar.LeadingText = configuration is MaterialAppBarConfiguration.SmallCentered or MaterialAppBarConfiguration.Search
            ? "Меню"
            : "Назад";
        _appBar.Thumbnail = _thumbnail;
        _appBar.AvatarText = _appBarAvatar.SelectedIndex == 1 ? "A" : null;
        _appBar.AvatarContent = _appBarAvatar.SelectedIndex == 2 ? _personAvatar : null;
        _appBar.MaxVisibleActions = (int)Math.Round(_appBarVisible.Value);
        FillActions(_appBar, (int)Math.Round(_appBarActions.Value));
        _appBarScroll.IsVisible = elevation == MaterialAppBarElevation.OnScroll;
    }

    private void ApplyBottomBar()
    {
        _bottomBar.IsFabVisible = _bottomFabToggle.IsChecked == true;
        FillActions(_bottomBar, (int)Math.Round(_bottomActions.Value));
    }

    private void ApplyToolbar()
    {
        var configuration = _toolbarConfiguration.SelectedIndex == 1
            ? MaterialToolbarConfiguration.Floating
            : MaterialToolbarConfiguration.Docked;
        var orientation = _toolbarOrientation.SelectedIndex == 1 ? Orientation.Vertical : Orientation.Horizontal;
        _toolbar.Configuration = configuration;
        _toolbar.Orientation = orientation;
        _toolbar.HorizontalAlignment = configuration == MaterialToolbarConfiguration.Docked && orientation == Orientation.Horizontal
            ? HorizontalAlignment.Stretch
            : HorizontalAlignment.Center;
        _toolbar.Color = _toolbarColor.SelectedIndex == 1 ? MaterialToolbarColor.Vibrant : MaterialToolbarColor.Standard;
        FillActions(_toolbar, (int)Math.Round(_toolbarActions.Value));
    }

    private void ApplyHeaders()
    {
        _sectionHeader.Label = string.IsNullOrWhiteSpace(_sectionLabel.Text) ? "Заголовок" : _sectionLabel.Text;
        _sectionHeader.LeadingContent = _sectionLeading.IsChecked == true ? _sectionIcon : null;
        _sectionHeader.TrailingContent = _sectionTrailing.IsChecked == true ? _sectionCounter : null;

        _pageHeader.Title = string.IsNullOrWhiteSpace(_pageTitle.Text) ? "Заголовок" : _pageTitle.Text;
        _pageHeader.Definition = _pageDefinition.Text;
        _pageHeader.LinkText = _pageLink.Text;
        _pageHeader.ShowDivider = _pageDivider.IsChecked == true;
    }

    private void FillActions(MaterialBarBase bar, int count)
    {
        bar.Actions.Clear();
        for (var i = 0; i < count && i < ActionLabels.Length; i++)
            bar.Actions.Add(new MaterialBarAction(ActionLabels[i], ActionIcons[i], Command(ActionLabels[i])));
    }

    private ICommand Command(string label) => new ActionCommand(() => Report(label));

    private void Report(string label)
    {
        _invocations++;
        _status.Text = $"Действий: {_invocations} · {label}";
    }

    private static Control Thumbnail(string caption) => new Border
    {
        Height = 40,
        CornerRadius = new CornerRadius(8),
        ClipToBounds = true,
        Child = new TextBlock
        {
            Text = caption,
            FontSize = 14,
            FontWeight = FontWeight.Medium,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(16, 0),
        },
        Background = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.Parse("#6750A4"), 0),
                new GradientStop(Color.Parse("#9A82DB"), 1),
            },
        },
    };

    private static Control PersonAvatar()
    {
        var avatar = new Border
        {
            Width = 32,
            Height = 32,
            CornerRadius = new CornerRadius(999),
            Child = new PathIcon { Data = Person, Width = 20, Height = 20 },
        };
        avatar.Bind(Border.BackgroundProperty, avatar.GetResourceObservable("MaterialSecondaryContainerBrush"));
        return avatar;
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
