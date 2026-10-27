using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Catalog;

/// <summary>Interactive search bar, search view and AppBarWithSearch composition with live suggestions.</summary>
public static class SearchGallery
{
    public static Control Create() => new SearchPlayground();
}

internal sealed class SearchPlayground : Grid, IComponentPlayground
{
    private const string PinnedCommit = "77b2199d6f056c2fe829ce25523c34a905e769c1";

    private static readonly string[] Catalogue =
    {
        "Search bar", "Search view", "Navigation rail", "Navigation bar", "Bottom app bar",
        "Range slider", "Rich tooltip", "Circular progress", "Segmented button", "Text field",
        "Menu", "Chips",
    };

    private readonly MaterialSearchBar _bar = new()
    {
        PlaceholderText = "Search components",
        AutoFocus = false,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Top,
    };
    private readonly MaterialSearchView _view;
    private readonly MaterialSearchBar _appBarBar = new()
    {
        PlaceholderText = "Search components",
        AutoFocus = false,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Center,
    };
    private readonly MaterialSearchAppBar _appBar = new();
    private ScrollViewer _appBarScroll = null!;
    private Grid _appBarStage = null!;
    private Button _appBarSecondaryAction = null!;
    private MaterialSearchBarScrollBehavior? _appBarBehavior;
    private readonly ObservableCollection<string> _suggestions = new();
    private readonly Panel _stageChildren = new();
    private readonly Border _host = new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        ClipToBounds = true,
    };
    private readonly TextBlock _status = new()
    {
        Text = "Введите запрос",
        TextWrapping = TextWrapping.Wrap,
        HorizontalAlignment = HorizontalAlignment.Center,
    };
    private readonly MaterialButtonGroup _kind;
    private readonly MaterialButtonGroup _mode;
    private readonly MaterialButtonGroup _variant;
    private readonly MaterialButtonGroup _configuration;
    private readonly Slider _width;
    private readonly TextBox _query;
    private readonly TextBox _placeholder;
    private readonly ToggleSwitch _leading;
    private readonly ToggleSwitch _trailing;
    private readonly ToggleSwitch _secondary;
    private readonly ToggleSwitch _avatar;
    private readonly ToggleSwitch _showSuggestions;
    private readonly ToggleSwitch _scrim;
    private readonly ToggleSwitch _anchor;
    private readonly ToggleSwitch _enabled;
    private readonly ToggleSwitch _rtl;
    private readonly ToggleSwitch _scrollAppBar;
    private readonly ToggleSwitch _reverseLayout;
    private readonly ToggleSwitch _reducedMotion;
    private bool _syncing;
    private bool _applying;

    public StackPanel Settings { get; } = new() { Spacing = 16 };

    internal SearchPlayground()
    {
        _view = new MaterialSearchView
        {
            IsOpen = false,
            Variant = MaterialSearchViewVariant.Expressive,
            PlaceholderText = "Search components",
            AutoFocus = false,
            Suggestions = _suggestions,
        };

        // Both controls stay attached: the collapsed bar is the live ExpansionAnchor for the
        // view's animated bounds and the shared query/selection source, exactly like the
        // SearchBarState + TextFieldState pair in the pinned source.
        BuildSearchAppBar(out var appBarStage);
        _stageChildren.Children.Add(appBarStage);
        _stageChildren.Children.Add(_bar);
        _stageChildren.Children.Add(_view);
        _host.Child = _stageChildren;

        var stage = new StackPanel
        {
            Spacing = 24,
            Margin = new Thickness(24),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        stage.Children.Add(_host);
        stage.Children.Add(_status);
        Children.Add(stage);

        _kind = Choice("Представление", new[] { "Search bar", "Search view", "Search app bar" });
        _mode = Choice("Расположение view", new[] { "Docked", "Full-screen" });
        _variant = Choice("Вариант", new[] { "Expressive", "Baseline" });
        _configuration = Choice("Поле", new[] { "Input text", "Supporting text" });

        var widthLabel = new TextBlock { Text = "Ширина: 480 dp" };
        Settings.Children.Add(widthLabel);
        _width = new Slider
        {
            Minimum = 360,
            Maximum = 720,
            Value = 480,
            TickFrequency = 1,
            IsSnapToTickEnabled = true,
        };
        MaterialAssist.SetControlSize(_width, MaterialControlSize.ExtraSmall);
        MaterialSlider.SetShowStops(_width, false);
        AutomationProperties.SetName(_width, "Ширина поиска, dp");
        Settings.Children.Add(_width);

        _query = TextField("Query", "Search");
        _placeholder = TextField("Placeholder", "Search components");
        _leading = Toggle("Ведущая иконка строки", true);
        _trailing = Toggle("Правая иконка", true);
        _secondary = Toggle("Вторая правая иконка строки", false);
        _avatar = Toggle("Аватар в строке", false);
        _showSuggestions = Toggle("Подсказки", true);
        _scrim = Toggle("Scrim за view", true);
        _anchor = Toggle("ExpansionAnchor = search bar", true);
        _enabled = Toggle("Поиск доступен", true);
        _rtl = Toggle("Справа налево (RTL)", false);
        _scrollAppBar = Toggle("App bar уезжает при прокрутке", true);
        _reverseLayout = Toggle("Обратный layout контента", false);
        _reducedMotion = Toggle("Reduce motion", false);

        var open = new Button
        {
            Content = "Открыть Search view с текущим запросом",
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        AutomationProperties.SetName(open, "Открыть Search view");
        open.Click += (_, _) =>
        {
            ActiveBar.Text = _query.Text;
            ShowView();
        };
        Settings.Children.Add(open);

        var close = new Button
        {
            Content = "Закрыть (программно)",
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        close.Click += (_, _) =>
        {
            if (_view.IsOpen)
                _view.Dismiss(MaterialSearchDismissReason.Programmatic);
        };
        Settings.Children.Add(close);

        var clear = new Button
        {
            Content = "Очистить запрос",
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        clear.Click += (_, _) =>
        {
            _bar.Text = string.Empty;
            _appBarBar.Text = string.Empty;
            _query.Text = string.Empty;
            if (_view.IsOpen)
                _view.Clear();
            Filter();
        };
        Settings.Children.Add(clear);

        Settings.Children.Add(new TextBlock
        {
            Text =
                $"Pinned androidx.compose.material3 SearchBar.kt @ {PinnedCommit}. " +
                "Экспрессивный вариант = два контейнера (поле 28, список 12, зазор 2, scrim 32%): " +
                "ExpandedFullScreenContainedSearchBar / ExpandedDockedSearchBarWithGap. " +
                "Baseline = один контейнер с разделителем 1 dp и строками 72 dp: " +
                "ExpandedFullScreenSearchBar / ExpandedDockedSearchBar. " +
                "Search app bar = AppBarWithSearch: навигация, поле 360–720 dp и действия в одной " +
                "строке; при прокрутке контейнер уезжает вверх (снимок высоты и клип), цвета " +
                "переходят в SurfaceContainer / SurfaceContainerHighest на доле перекрытия > 0.01, " +
                "а обратный layout и reduced motion переключаются ниже. " +
                "Строка 56 dp, ширина 360–720 dp. Full-screen занимает поверхность примера, " +
                "не всё окно каталога: Android-диалог, window insets и predictive back — " +
                "платформенные ограничения, на десктопе размер задаёт вызывающий layout. " +
                "Enter выполняет поиск; Down переходит к подсказкам, Up возвращает в поле, " +
                "Escape сначала очищает запрос, затем закрывает view. " +
                "Клик вне docked-view и клик по scrim закрывают его.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
        });

        _kind.SelectionChanged += (_, _) => Apply();
        _mode.SelectionChanged += (_, _) => Apply();
        _variant.SelectionChanged += (_, _) => Apply();
        _configuration.SelectionChanged += (_, _) => Apply();
        _width.PropertyChanged += (_, e) =>
        {
            if (e.Property != Avalonia.Controls.Primitives.RangeBase.ValueProperty)
                return;
            widthLabel.Text = $"Ширина: {_width.Value:0} dp";
            Apply();
        };
        _query.TextChanged += (_, _) =>
        {
            if (_syncing)
                return;
            _syncing = true;
            _bar.Text = _query.Text;
            _appBarBar.Text = _query.Text;
            if (_view.IsOpen)
                _view.Text = _query.Text;
            _syncing = false;
            Filter();
        };
        _placeholder.TextChanged += (_, _) => Apply();
        foreach (var toggle in new[]
                 {
                     _leading, _trailing, _secondary, _avatar, _showSuggestions, _scrim,
                     _anchor, _enabled, _rtl, _scrollAppBar, _reverseLayout, _reducedMotion,
                 })
            toggle.IsCheckedChanged += (_, _) => Apply();

        _bar.PropertyChanged += (_, e) =>
        {
            if (e.Property == MaterialSearchBar.TextProperty)
                SyncQuery(fromView: false);
        };
        _appBarBar.PropertyChanged += (_, e) =>
        {
            if (e.Property == MaterialSearchBar.TextProperty)
                SyncQuery(fromView: false);
        };
        _view.QueryChanged += (_, _) => SyncQuery(fromView: true);
        _bar.ExpansionRequested += (_, _) => ShowView();
        _appBarBar.SearchRequested += (_, _) =>
            _status.Text = $"Поиск: {_appBarBar.Text}";
        _appBarBar.TrailingIconInvoked += (_, _) =>
            _status.Text = $"Поиск: {_appBarBar.Text}";
        _appBarBar.ClearRequested += (_, _) => Filter();
        _bar.SearchRequested += (_, _) =>
        {
            ShowView();
            _status.Text = $"Поиск: {_bar.Text}";
        };
        _bar.TrailingIconInvoked += (_, _) =>
        {
            ShowView();
            _status.Text = $"Поиск: {_bar.Text}";
        };
        _bar.SecondaryIconInvoked += (_, _) => _status.Text = "Нажата дополнительная иконка";
        _bar.DismissRequested += (_, _) => _status.Text = "Запрос пуст";
        _view.SearchRequested += (_, _) => _status.Text = $"Поиск: {_view.Text}";
        _view.SuggestionActivated += (_, e) => _status.Text = $"Выбрано: {e.Suggestion}";
        _view.VoiceInputRequested += (_, _) =>
            _status.Text = "Запрошен голосовой ввод (распознавание не подключено)";
        _view.Dismissed += (_, e) =>
        {
            _status.Text = $"Закрыто: {e.Reason}";
            if (_applying || e.Reason == MaterialSearchDismissReason.Programmatic)
                return;
            _applying = true;
            // A docked/full-screen view opened from the standalone bar returns to the bar; the
            // app bar demo stays on its own kind so the scroll composition remains visible.
            if (_kind.SelectedIndex == 1)
                _kind.SelectedIndex = 0;
            _applying = false;
            Apply();
        };

        Filter();
        Apply();
    }

    private TextBox TextField(string label, string text)
    {
        var field = new TextBox { Text = text };
        MaterialTextField.SetLabel(field, label);
        MaterialTextField.SetVariant(field, MaterialTextFieldVariant.Outlined);
        Settings.Children.Add(field);
        return field;
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
            SelectedIndex = 0,
        };
        foreach (var option in options)
            group.Items.Add(new MaterialButtonGroupItem { Content = option });
        Settings.Children.Add(group);
        return group;
    }

    private ToggleSwitch Toggle(string label, bool value)
    {
        var toggle = new ToggleSwitch { Content = label, IsChecked = value };
        Settings.Children.Add(toggle);
        return toggle;
    }

    private void ShowView()
    {
        if (_kind.SelectedIndex == 2)
        {
            if (!_view.IsOpen)
                _view.Open();
            return;
        }

        _kind.SelectedIndex = 1;
        if (_kind.SelectedIndex == 1 && !_view.IsOpen)
            Apply();
    }

    private MaterialSearchBar ActiveBar => _kind.SelectedIndex == 2 ? _appBarBar : _bar;

    private void SyncQuery(bool fromView)
    {
        if (_syncing)
            return;
        _syncing = true;
        var bar = ActiveBar;
        if (fromView)
        {
            bar.Text = _view.Text;
            _query.Text = _view.Text;
        }
        else
        {
            if (!_view.IsOpen)
                _view.Text = bar.Text;
            _query.Text = bar.Text;
        }

        // Keep both collapsed bars on the same query so switching presentation preserves it.
        if (ReferenceEquals(bar, _bar))
            _appBarBar.Text = _bar.Text;
        else
            _bar.Text = _appBarBar.Text;

        _syncing = false;
        Filter();
    }

    private void Filter()
    {
        var query = ActiveBar.Text?.Trim() ?? string.Empty;
        _suggestions.Clear();
        foreach (var item in Catalogue.Where(item =>
                     item.Contains(query, StringComparison.OrdinalIgnoreCase)).Take(6))
            _suggestions.Add(item);
        _status.Text = _suggestions.Count == 0
            ? "Ничего не найдено"
            : $"Подсказок: {_suggestions.Count}";
    }

    private void Apply()
    {
        if (_applying)
            return;
        _applying = true;
        try
        {
            var showView = _kind.SelectedIndex == 1;
            var showAppBar = _kind.SelectedIndex == 2;
            _mode.IsEnabled = showView || showAppBar;
            _variant.IsEnabled = showView;
            _configuration.IsEnabled = showView;
            _showSuggestions.IsEnabled = showView || showAppBar;
            _scrim.IsEnabled = showView;
            _anchor.IsEnabled = showView;
            _leading.IsEnabled = !showView;
            _trailing.IsEnabled = !showView;
            _secondary.IsEnabled = !showView;
            _avatar.IsEnabled = !showView && !showAppBar;
            _scrollAppBar.IsEnabled = showAppBar;
            _reverseLayout.IsEnabled = showAppBar;
            _reducedMotion.IsEnabled = showAppBar;

            _host.Width = _width.Value;
            _host.Height = showView || showAppBar
                ? (_mode.SelectedIndex == 1 ? 480 : 360)
                : double.NaN;

            _bar.IsVisible = !showAppBar;
            _appBarStage.IsVisible = showAppBar;

            _view.Mode = (MaterialSearchViewMode)_mode.SelectedIndex;
            _view.Variant = (MaterialSearchViewVariant)_variant.SelectedIndex;
            _view.Configuration = (MaterialSearchFieldConfiguration)_configuration.SelectedIndex;
            _view.ShowSuggestions = _showSuggestions.IsChecked == true;
            _view.ShowScrim = _scrim.IsChecked == true;
            _view.ShowTrailingIcon = _trailing.IsChecked == true;
            // The source expanded full-screen surface focuses its input on the first expansion.
            _view.AutoFocus = showAppBar;
            _view.ExpansionAnchor = showAppBar
                ? _appBarBar
                : _anchor.IsChecked == true ? _bar : null;

            _bar.ShowLeadingIcon = _leading.IsChecked == true;
            _bar.ShowTrailingIcon = _trailing.IsChecked == true;
            _bar.ShowSecondaryIcon = _secondary.IsChecked == true;
            _bar.ShowAvatar = _avatar.IsChecked == true;
            _bar.AvatarInitial = "A";
            _bar.PlaceholderText = _view.PlaceholderText = _placeholder.Text;
            _bar.Text = _query.Text;

            _appBarBar.PlaceholderText = _placeholder.Text;
            _appBarBar.Text = _query.Text;
            _appBarBar.ShowLeadingIcon = _leading.IsChecked == true;
            _appBarBar.ShowTrailingIcon = _trailing.IsChecked == true;
            _appBarBar.ShowClearButton = true;

            var reverse = _reverseLayout.IsChecked == true;
            if (showAppBar && _scrollAppBar.IsChecked == true)
            {
                if (_appBarBehavior is null || _appBarBehavior.ReverseLayout != reverse)
                    _appBarBehavior = MaterialSearchBarScrollBehavior.EnterAlways(reverseLayout: reverse);
                _appBar.ScrollBehavior = _appBarBehavior;
            }
            else
            {
                _appBar.ScrollBehavior = null;
            }

            _appBar.ScrollSource = _appBarScroll;
            _appBar.ExpandsToFullScreen = _mode.SelectedIndex == 1;
            _appBarSecondaryAction.IsVisible = _secondary.IsChecked == true;

            MaterialMotion.SetIsReduced(_host, _reducedMotion.IsChecked == true);

            _host.IsEnabled = _enabled.IsChecked == true;
            _host.FlowDirection = _rtl.IsChecked == true
                ? FlowDirection.RightToLeft
                : FlowDirection.LeftToRight;

            if (showView)
                _view.Open();
            else if (!showAppBar)
                _view.Dismiss(MaterialSearchDismissReason.Programmatic);
        }
        finally
        {
            _applying = false;
        }
    }

    private void BuildSearchAppBar(out Control stage)
    {
        _appBarScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = BuildScrollContent(),
        };

        _appBar.SearchBar = _appBarBar;
        _appBar.ScrollSource = _appBarScroll;
        _appBar.SearchView = _view;

        var navigation = Icon(MaterialSearchAppBar.NavigationForegroundProperty, MenuGeometry);
        var navigationButton = IconButton(navigation, "Меню");
        navigationButton.Click += (_, _) => _status.Text = "Меню";
        _appBar.NavigationContent = navigationButton;

        var searchAction = IconButton(
            Icon(MaterialSearchAppBar.ActionsForegroundProperty, SearchGeometry), "Поиск");
        searchAction.Click += (_, _) => _status.Text = $"Поиск: {_appBarBar.Text}";
        _appBarSecondaryAction = IconButton(
            Icon(MaterialSearchAppBar.ActionsForegroundProperty, MicGeometry), "Голосовой ввод");
        _appBarSecondaryAction.Click += (_, _) =>
            _status.Text = "Запрошен голосовой ввод (распознавание не подключено)";

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
        };
        actions.Children.Add(searchAction);
        actions.Children.Add(_appBarSecondaryAction);
        _appBar.ActionsContent = actions;

        _appBarStage = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        _appBarStage.Children.Add(_appBar);
        Grid.SetRow(_appBarScroll, 1);
        _appBarStage.Children.Add(_appBarScroll);
        _appBarStage.IsVisible = false;
        stage = _appBarStage;
    }

    private PathIcon Icon(AvaloniaProperty foregroundProperty, Geometry data)
    {
        var icon = new PathIcon { Data = data, Width = 24, Height = 24 };
        icon.Bind(PathIcon.ForegroundProperty,
            new Binding(foregroundProperty.Name) { Source = _appBar });
        return icon;
    }

    private static Button IconButton(Control content, string name)
    {
        var button = new Button { Content = content };
        MaterialAssist.SetIsIconButton(button, true);
        AutomationProperties.SetName(button, name);
        return button;
    }

    private static Control BuildScrollContent()
    {
        var panel = new StackPanel { Spacing = 12, Margin = new Thickness(16, 16, 16, 24) };
        panel.Children.Add(new TextBlock
        {
            Text = "Прокрутите содержимое: app bar уезжает вверх и возвращается. " +
                   "Цвета и поле поиска переключаются на scrolled-варианты.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
        });
        for (var i = 1; i <= 40; i++)
            panel.Children.Add(new TextBlock { Text = $"Строка {i}", FontSize = 14 });
        return panel;
    }

    private static readonly Geometry MenuGeometry = Geometry.Parse(
        "M3 18V16H21V18H3ZM3 13V11H21V13H3ZM3 8V6H21V8H3Z");
    private static readonly Geometry SearchGeometry = Geometry.Parse(
        "M19.6 21L13.3 14.7C12.8 15.1 12.225 15.4167 11.575 15.65C10.925 15.8833 10.2333 16 " +
        "9.5 16C7.68333 16 6.14583 15.3708 4.8875 14.1125C3.62917 12.8542 3 11.3167 3 9.5C3 " +
        "7.68333 3.62917 6.14583 4.8875 4.8875C6.14583 3.62917 7.68333 3 9.5 3C11.3167 3 " +
        "12.8542 3.62917 14.1125 4.8875C15.3708 6.14583 16 7.68333 16 9.5C16 10.2333 15.8833 " +
        "10.925 15.65 11.575C15.4167 12.225 15.1 12.8 14.7 13.3L21 19.6L19.6 21ZM9.5 " +
        "14C10.75 14 11.8125 13.5625 12.6875 12.6875C13.5625 11.8125 14 10.75 14 9.5C14 8.25 " +
        "13.5625 7.1875 12.6875 6.3125C11.8125 5.4375 10.75 5 9.5 5C8.25 5 7.1875 5.4375 " +
        "6.3125 6.3125C5.4375 7.1875 5 8.25 5 9.5C5 10.75 5.4375 11.8125 6.3125 12.6875C7.1875 " +
        "13.5625 8.25 14 9.5 14Z");
    private static readonly Geometry MicGeometry = Geometry.Parse(
        "M12 14C11.1667 14 10.4583 13.7083 9.875 13.125C9.29167 12.5417 9 11.8333 9 11V5C9 " +
        "4.16667 9.29167 3.45833 9.875 2.875C10.4583 2.29167 11.1667 2 12 2C12.8333 2 " +
        "13.5417 2.29167 14.125 2.875C14.7083 3.45833 15 4.16667 15 5V11C15 11.8333 14.7083 " +
        "12.5417 14.125 13.125C13.5417 13.7083 12.8333 14 12 14ZM11 21V17.925C9.26667 " +
        "17.6917 7.83333 16.9167 6.7 15.6C5.56667 14.2833 5 12.75 5 11H7C7 12.3833 7.4875 " +
        "13.5625 8.4625 14.5375C9.4375 15.5125 10.6167 16 12 16C13.3833 16 14.5625 15.5125 " +
        "15.5375 14.5375C16.5125 13.5625 17 12.3833 17 11H19C19 12.75 18.4333 14.2833 17.3 " +
        "15.6C16.1667 16.9167 14.7333 17.6917 13 17.925V21H11Z");
}