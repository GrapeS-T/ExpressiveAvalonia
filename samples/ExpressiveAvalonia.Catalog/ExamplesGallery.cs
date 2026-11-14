using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Catalog;

/// <summary>
/// One selected example from the source screens, the adaptive layout grid, the keyboard, the
/// building blocks, the device chrome or the color schematic, with its configuration in the inspector.
/// </summary>
public static class ExamplesGallery
{
    public static Control Create() => new ExamplesPlayground();
}

internal sealed class ExamplesPlayground : Grid, IComponentPlayground
{
    private static readonly string[] ScreenNames =
    {
        "Home", "Upcoming", "Detailed view", "Reviews", "Library", "Gallery", "Messaging",
    };

    private static readonly string[] SizeClassNames =
    {
        "Compact 0–599", "Medium 600–839", "Expanded 840–1199", "Large 1200–1599", "Extra-large 1600+",
    };

    private static readonly string[] NavigationNames =
    {
        "None", "Navigation region", "Navigation expanded",
    };

    private static readonly string[] BlockNames =
    {
        "Content (local-000221)",
        ".Building Blocks/Content (local-000218)",
        "Slot (local-004633)",
        "General item (local-000261)",
        "Label on slider (Label.kt)",
        "Native rich text / autosize / links",
    };

    private static readonly string[] ChromeNames =
    {
        "Status bar (local-000697)", "Device frame (local-001331)",
    };

    private readonly ContentControl _stage = new()
    {
        Margin = new Thickness(24),
        HorizontalContentAlignment = HorizontalAlignment.Center,
        VerticalContentAlignment = VerticalAlignment.Center,
    };
    private readonly TextBlock _status = new()
    {
        Text = "Выберите пример в инспекторе.",
        HorizontalAlignment = HorizontalAlignment.Center,
        TextWrapping = TextWrapping.Wrap,
    };
    private readonly ComboBox _category = new()
    {
        ItemsSource = new[] { "Screens", "Layout grid", "Keyboard", "Building blocks", "Device chrome", "Color schematic" },
        SelectedIndex = 0,
        HorizontalAlignment = HorizontalAlignment.Stretch,
    };
    private readonly StackPanel _screensSettings = new() { Spacing = 16 };
    private readonly StackPanel _layoutSettings = new() { Spacing = 16 };
    private readonly StackPanel _keyboardSettings = new() { Spacing = 16 };
    private readonly StackPanel _blocksSettings = new() { Spacing = 16 };
    private readonly StackPanel _chromeSettings = new() { Spacing = 16 };
    private readonly StackPanel _schematicSettings = new() { Spacing = 16 };

    private readonly ComboBox _screen = new() { ItemsSource = ScreenNames, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ComboBox _form = new() { ItemsSource = new[] { "Phone", "Browser" }, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly Slider _screenScale;
    private Viewbox? _screenViewbox;
    private double _screenNativeWidth = 428;
    private double _screenNativeHeight = 960;

    private readonly ComboBox _layoutKind = new() { ItemsSource = new[] { "Адаптивная сетка", "Вариант источника" }, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ComboBox _sizeClass = new() { ItemsSource = SizeClassNames, SelectedIndex = 2, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ComboBox _navigation = new() { ItemsSource = NavigationNames, SelectedIndex = 1, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly Slider _layoutWidth;
    private readonly ToggleSwitch _autoNavigation;

    private readonly MaterialKeyboard _keyboard = new() { Width = 412 };
    private Viewbox? _keyboardStage;
    private readonly ComboBox _keyboardConfig = new() { ItemsSource = Enum.GetNames<MaterialKeyboardConfiguration>(), SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ComboBox _keyboardStyle = new() { ItemsSource = Enum.GetNames<MaterialKeyboardStyle>(), SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ComboBox _keyboardLayer = new() { ItemsSource = Enum.GetNames<MaterialKeyboardLayer>(), SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ToggleSwitch _shift;
    private readonly ToggleSwitch _caps;
    private readonly ToggleSwitch _chromeBar;
    private readonly ToggleSwitch _bottomBar;
    private readonly ToggleSwitch _keyboardOpen;

    private readonly ComboBox _block = new() { ItemsSource = BlockNames, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ToggleSwitch _overline;
    private readonly ToggleSwitch _blockSupporting;
    private readonly ToggleSwitch _slotFilled;
    private readonly ToggleSwitch _tileSelected;
    private readonly ToggleSwitch _tileText;

    private readonly ComboBox _chrome = new() { ItemsSource = ChromeNames, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ToggleSwitch _barCutout;
    private readonly ToggleSwitch _barClock;
    private readonly TextBox _barTime;
    private readonly ToggleSwitch _frameStatus;
    private readonly ToggleSwitch _frameGesture;
    private readonly ToggleSwitch _frameCutout;
    private readonly ToggleSwitch _frameScroll;
    private readonly TextBox _frameTime;

    private readonly MaterialSchematicGroup _schematic = new() { ShowDarkPreview = true, ShowFixedColors = true, SwatchWidth = 180 };
    private readonly ScrollViewer _schematicStage = new()
    {
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
    };
    private readonly ToggleSwitch _darkPreview;
    private readonly ToggleSwitch _fixedColors;
    private readonly Slider _swatchWidth;

    private bool _syncing;
    public StackPanel Settings { get; } = new() { Spacing = 16 };

    internal ExamplesPlayground()
    {
        RowDefinitions = new RowDefinitions("*,Auto");
        Children.Add(_stage);
        Grid.SetRow(_status, 1);
        _status.Margin = new Thickness(24, 0, 24, 20);
        Children.Add(_status);

        Settings.Children.Add(Caption("Семейство"));
        Settings.Children.Add(_category);

        // ---- screens ----
        _screensSettings.Children.Add(Caption("Экран"));
        _screensSettings.Children.Add(_screen);
        _screensSettings.Children.Add(Caption("Форм-фактор"));
        _screensSettings.Children.Add(_form);
        _screenScale = Number(_screensSettings, "Масштаб", 0.3, 1.0, 0.5, 0.05, "×");
        Settings.Children.Add(_screensSettings);

        // ---- layout ----
        _layoutSettings.Children.Add(Caption("Пример"));
        _layoutSettings.Children.Add(_layoutKind);
        _layoutWidth = Number(_layoutSettings, "Ширина окна", 320, 1680, 900, 20, "dp");
        _layoutSettings.Children.Add(Caption("Класс размера (вариант источника)"));
        _layoutSettings.Children.Add(_sizeClass);
        _layoutSettings.Children.Add(Caption("Navigation region"));
        _layoutSettings.Children.Add(_navigation);
        _autoNavigation = Toggle(_layoutSettings, "Адаптивная навигация", true);
        Settings.Children.Add(_layoutSettings);

        // ---- keyboard ----
        _keyboardSettings.Children.Add(Caption("Конфигурация"));
        _keyboardSettings.Children.Add(_keyboardConfig);
        _keyboardSettings.Children.Add(Caption("Presentation"));
        _keyboardSettings.Children.Add(_keyboardStyle);
        _keyboardSettings.Children.Add(Caption("Слой"));
        _keyboardSettings.Children.Add(_keyboardLayer);
        _shift = Toggle(_keyboardSettings, "Shift", false);
        _caps = Toggle(_keyboardSettings, "Caps lock", false);
        _chromeBar = Toggle(_keyboardSettings, "Chrome bar", true);
        _bottomBar = Toggle(_keyboardSettings, "Bottom bar", true);
        _keyboardOpen = Toggle(_keyboardSettings, "Клавиатура открыта", true);
        Settings.Children.Add(_keyboardSettings);

        // ---- building blocks ----
        _blocksSettings.Children.Add(Caption("Building block"));
        _blocksSettings.Children.Add(_block);
        _overline = Toggle(_blocksSettings, "Overline", true);
        _blockSupporting = Toggle(_blocksSettings, "Поддерживающий текст", true);
        _slotFilled = Toggle(_blocksSettings, "Заполнить слот", false);
        _tileSelected = Toggle(_blocksSettings, "Выбранная плитка", false);
        _tileText = Toggle(_blocksSettings, "Текст на плитке", true);
        Settings.Children.Add(_blocksSettings);

        // ---- chrome ----
        _chromeSettings.Children.Add(Caption("Chrome"));
        _chromeSettings.Children.Add(_chrome);
        _barCutout = Toggle(_chromeSettings, "Status bar: камера-вырез", true);
        _barClock = Toggle(_chromeSettings, "Status bar: живые часы", true);
        _barTime = Text(_chromeSettings, "Status bar: время", "9:41");
        _frameStatus = Toggle(_chromeSettings, "Frame: status bar", true);
        _frameGesture = Toggle(_chromeSettings, "Frame: gesture bar", true);
        _frameCutout = Toggle(_chromeSettings, "Frame: камера-вырез", true);
        _frameScroll = Toggle(_chromeSettings, "Frame: прокрутка содержимого", true);
        _frameTime = Text(_chromeSettings, "Frame: время", "9:41");
        Settings.Children.Add(_chromeSettings);

        // ---- schematic ----
        _darkPreview = Toggle(_schematicSettings, "Тёмный предпросмотр", true);
        _fixedColors = Toggle(_schematicSettings, "Fixed colors", true);
        _swatchWidth = Number(_schematicSettings, "Ширина образца", 120, 240, 180, 10, "dp");
        Settings.Children.Add(_schematicSettings);

        Settings.Children.Add(new TextBlock
        {
            Text = "Экраны собраны из тех же контролов, что использует приложение: они масштабируются, " +
                   "фокусируются и прокручиваются. Сетка меряется по реальным breakpoint'ам 600/840/1200/1600, " +
                   "клавиатура вводит текст в собственный буфер, схема копирует hex по нажатию.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
        });

        AutomationProperties.SetName(_category, "Семейство примеров");
        AutomationProperties.SetName(_screen, "Экран");
        AutomationProperties.SetName(_form, "Форм-фактор экрана");
        AutomationProperties.SetName(_layoutKind, "Пример сетки");
        AutomationProperties.SetName(_sizeClass, "Класс размера");
        AutomationProperties.SetName(_navigation, "Navigation region");
        AutomationProperties.SetName(_keyboardConfig, "Конфигурация клавиатуры");
        AutomationProperties.SetName(_keyboardStyle, "Presentation клавиатуры");
        AutomationProperties.SetName(_keyboardLayer, "Слой клавиатуры");
        AutomationProperties.SetName(_block, "Building block");
        AutomationProperties.SetName(_chrome, "Chrome пример");
        AutomationProperties.SetName(_barTime, "Время status bar");
        AutomationProperties.SetName(_frameTime, "Время device frame");

        _category.SelectionChanged += (_, _) =>
        {
            if (_syncing) return;
            UpdateCategory();
            Render();
        };
        _screen.SelectionChanged += (_, _) => { if (!_syncing && _screensSettings.IsVisible) RenderScreens(); };
        _form.SelectionChanged += (_, _) => { if (!_syncing && _screensSettings.IsVisible) RenderScreens(); };
        _screenScale.PropertyChanged += (_, e) => { if (!_syncing && e.Property == RangeBase.ValueProperty && _screensSettings.IsVisible) UpdateScreenScale(); };
        _layoutKind.SelectionChanged += (_, _) => { if (!_syncing && _layoutSettings.IsVisible) RenderLayout(); };
        _sizeClass.SelectionChanged += (_, _) => { if (!_syncing && _layoutSettings.IsVisible) RenderLayout(); };
        _navigation.SelectionChanged += (_, _) => { if (!_syncing && _layoutSettings.IsVisible) RenderLayout(); };
        _layoutWidth.PropertyChanged += (_, e) => { if (!_syncing && e.Property == RangeBase.ValueProperty && _layoutSettings.IsVisible) RenderLayout(); };
        _autoNavigation.IsCheckedChanged += (_, _) => { if (!_syncing && _layoutSettings.IsVisible) RenderLayout(); };
        _keyboardConfig.SelectionChanged += (_, _) => { if (!_syncing && _keyboardSettings.IsVisible) ApplyKeyboard(); };
        _keyboardStyle.SelectionChanged += (_, _) => { if (!_syncing && _keyboardSettings.IsVisible) ApplyKeyboard(); };
        _keyboardLayer.SelectionChanged += (_, _) => { if (!_syncing && _keyboardSettings.IsVisible) ApplyKeyboard(); };
        foreach (var toggle in new[] { _shift, _caps, _chromeBar, _bottomBar, _keyboardOpen })
            toggle.IsCheckedChanged += (_, _) => { if (!_syncing && _keyboardSettings.IsVisible) ApplyKeyboard(); };
        _block.SelectionChanged += (_, _) => { if (!_syncing && _blocksSettings.IsVisible) RenderBlocks(); };
        foreach (var toggle in new[] { _overline, _blockSupporting, _slotFilled, _tileSelected, _tileText })
            toggle.IsCheckedChanged += (_, _) => { if (!_syncing && _blocksSettings.IsVisible) RenderBlocks(); };
        _chrome.SelectionChanged += (_, _) => { if (!_syncing && _chromeSettings.IsVisible) RenderChrome(); };
        foreach (var toggle in new[] { _barCutout, _barClock, _frameStatus, _frameGesture, _frameCutout, _frameScroll })
            toggle.IsCheckedChanged += (_, _) => { if (!_syncing && _chromeSettings.IsVisible) RenderChrome(); };
        foreach (var text in new[] { _barTime, _frameTime })
            text.TextChanged += (_, _) => { if (!_syncing && _chromeSettings.IsVisible) RenderChrome(); };
        foreach (var toggle in new[] { _darkPreview, _fixedColors })
            toggle.IsCheckedChanged += (_, _) => { if (!_syncing && _schematicSettings.IsVisible) RenderSchematic(); };
        _swatchWidth.PropertyChanged += (_, e) => { if (!_syncing && e.Property == RangeBase.ValueProperty && _schematicSettings.IsVisible) RenderSchematic(); };

        _keyboard.TextEntered += (_, e) => _status.Text = $"Введено {Describe(e.Text)}";
        _keyboard.ReturnPressed += (_, _) => _status.Text = "Return: буфер не принимает переводы строк";
        _keyboard.Dismissed += (_, _) =>
        {
            _status.Text = "Клавиатура скрыта";
            _syncing = true;
            _keyboardOpen.IsChecked = false;
            _syncing = false;
        };
        _keyboard.ChromeInvoked += (_, e) => _status.Text = $"Chrome: {e.Action}";
        _keyboard.PropertyChanged += (_, e) =>
        {
            if (e.Property != MaterialKeyboard.IsOpenProperty || _syncing) return;
            _syncing = true;
            _keyboardOpen.IsChecked = _keyboard.IsOpen;
            _syncing = false;
        };

        _schematic.SwatchCopied += (_, e) => _status.Text = $"Скопирован {e.Role} ({e.Token}) — {e.Hex}";
        _schematicStage.Content = _schematic;

        UpdateCategory();
        Render();
    }

    private void UpdateCategory()
    {
        var category = Math.Clamp(_category.SelectedIndex, 0, 5);
        _screensSettings.IsVisible = category == 0;
        _layoutSettings.IsVisible = category == 1;
        _keyboardSettings.IsVisible = category == 2;
        _blocksSettings.IsVisible = category == 3;
        _chromeSettings.IsVisible = category == 4;
        _schematicSettings.IsVisible = category == 5;
        _stage.HorizontalContentAlignment = category == 5 ? HorizontalAlignment.Stretch : HorizontalAlignment.Center;
        _stage.VerticalContentAlignment = category == 5 ? VerticalAlignment.Stretch : VerticalAlignment.Center;
    }

    private void Render()
    {
        switch (Math.Clamp(_category.SelectedIndex, 0, 5))
        {
            case 1:
                RenderLayout();
                break;
            case 2:
                RenderKeyboard();
                break;
            case 3:
                RenderBlocks();
                break;
            case 4:
                RenderChrome();
                break;
            case 5:
                RenderSchematic();
                break;
            default:
                RenderScreens();
                break;
        }
    }

    // ---- screens ------------------------------------------------------------------------

    private void RenderScreens()
    {
        var form = Math.Clamp(_form.SelectedIndex, 0, 1);
        var index = Math.Clamp(_screen.SelectedIndex, 0, ScreenNames.Length - 1);
        Control content;
        if (form == 0)
        {
            content = index switch
            {
                0 => ExamplesScreens.HomeMobile(),
                1 => ExamplesScreens.UpcomingMobile(),
                2 => ExamplesScreens.DetailedMobile(),
                3 => ExamplesScreens.ReviewsMobile(),
                4 => ExamplesScreens.LibraryMobile(),
                5 => ExamplesScreens.GalleryMobile(),
                _ => ExamplesScreens.MessagingMobile(),
            };
            _screenNativeWidth = 428;
            _screenNativeHeight = double.IsFinite(content.Height) ? content.Height : 960;
        }
        else
        {
            content = index switch
            {
                0 => ExamplesScreens.HomeWeb(),
                1 => ExamplesScreens.UpcomingWeb(),
                2 => ExamplesScreens.DetailedWeb(),
                3 => ExamplesScreens.ReviewsWeb(),
                4 => ExamplesScreens.LibraryWeb(),
                5 => ExamplesScreens.GalleryWeb(),
                _ => ExamplesScreens.MessagingWeb(),
            };
            _screenNativeWidth = 905;
            _screenNativeHeight = 680;
            content = new Border { Width = 905, Height = 680, ClipToBounds = true, Child = content };
        }

        _screenViewbox = new Viewbox
        {
            Stretch = Stretch.Uniform,
            Child = content,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        UpdateScreenScale();
        _stage.Content = _screenViewbox;
        _status.Text = $"{ScreenNames[index]} · {(form == 0 ? "Phone 428 dp" : "Browser 905 dp")}";
    }

    private void UpdateScreenScale()
    {
        if (_screenViewbox is null) return;
        var scale = _screenScale.Value;
        _screenViewbox.Width = _screenNativeWidth * scale;
        _screenViewbox.Height = _screenNativeHeight * scale;
        _status.Text = $"{ScreenNames[Math.Clamp(_screen.SelectedIndex, 0, ScreenNames.Length - 1)]} · масштаб {scale:0.00}×";
    }

    // ---- layout grid --------------------------------------------------------------------

    private void RenderLayout()
    {
        var adaptive = _layoutKind.SelectedIndex == 0;
        var width = adaptive ? _layoutWidth.Value : SizeClassWidth(Math.Clamp(_sizeClass.SelectedIndex, 0, 4));
        var region = (MaterialNavigationRegion)Math.Clamp(_navigation.SelectedIndex, 0, 2);
        var grid = new MaterialLayoutGrid
        {
            Width = width,
            Height = 320,
            AutoNavigation = adaptive && _autoNavigation.IsChecked == true,
            RequestedNavigation = region,
            NavigationContent = CompactNavigation(),
            ExpandedNavigationContent = ExpandedNavigation(),
            Content = VariantContent(),
        };
        grid.SizeClassChanged += (_, e) =>
            _status.Text = $"{Describe(e.NewClass)} · navigation: {grid.NavigationRegion}";
        grid.NavigationRegionChanged += (_, _) =>
            _status.Text = $"Ширина {width:0} dp · navigation: {grid.NavigationRegion}";
        _stage.Content = new Viewbox
        {
            Width = 720,
            Height = 360,
            Stretch = Stretch.Uniform,
            Child = grid,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _autoNavigation.IsEnabled = adaptive;
        _layoutWidth.IsEnabled = adaptive;
        _sizeClass.IsEnabled = !adaptive;
        _status.Text = $"Ширина {width:0} dp · {Describe(MaterialLayoutGrid.FromWidth(width))} · " +
                       (adaptive ? "адаптивная навигация" : "вариант источника");
    }

    private static double SizeClassWidth(int index) => index switch
    {
        0 => 360,
        1 => 700,
        2 => 900,
        3 => 1300,
        _ => 1700,
    };

    private static string Describe(MaterialWindowSizeClass sizeClass) => sizeClass switch
    {
        MaterialWindowSizeClass.Compact => "Compact 0–599 dp",
        MaterialWindowSizeClass.Medium => "Medium 600–839 dp",
        MaterialWindowSizeClass.Expanded => "Expanded 840–1199 dp",
        MaterialWindowSizeClass.Large => "Large 1200–1599 dp",
        _ => "Extra-large 1600+ dp",
    };

    private static Control CompactNavigation()
    {
        var panel = new StackPanel { Spacing = 8, Margin = new Thickness(8), VerticalAlignment = VerticalAlignment.Center };
        foreach (var (label, icon) in new[] { ("Home", "stars"), ("Search", "search"), ("Library", "folder") })
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            row.Children.Add(ExamplesScreens.Glyph(icon, 20));
            row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
            panel.Children.Add(row);
        }
        panel.Bind(Avalonia.Controls.Panel.BackgroundProperty, panel.GetResourceObservable("MaterialSurfaceContainerBrush"));
        return panel;
    }

    private static Control ExpandedNavigation()
    {
        var panel = new StackPanel { Spacing = 8, Margin = new Thickness(16) };
        var title = new TextBlock { Text = "Navigation drawer" };
        MaterialTypography.SetStyle(title, MaterialTypeStyle.TitleMedium);
        panel.Children.Add(title);
        foreach (var label in new[] { "Home", "Upcoming", "Library", "Gallery", "Messaging" })
        {
            var item = new TextBlock { Text = label, Margin = new Thickness(0, 6) };
            MaterialTypography.SetStyle(item, MaterialTypeStyle.LabelLarge);
            panel.Children.Add(item);
        }
        panel.Bind(Avalonia.Controls.Panel.BackgroundProperty, panel.GetResourceObservable("MaterialSurfaceContainerBrush"));
        return panel;
    }

    private static Control VariantContent()
    {
        var panel = new StackPanel { Spacing = 8, Margin = new Thickness(12) };
        var title = new TextBlock { Text = "Content region" };
        MaterialTypography.SetStyle(title, MaterialTypeStyle.TitleSmall);
        panel.Children.Add(title);
        for (var i = 0; i < 4; i++)
        {
            var bar = new Border { Height = 24, CornerRadius = new CornerRadius(6) };
            bar.Bind(Border.BackgroundProperty, bar.GetResourceObservable("MaterialSurfaceContainerHighestBrush"));
            panel.Children.Add(bar);
        }
        panel.Bind(Avalonia.Controls.Panel.BackgroundProperty, panel.GetResourceObservable("MaterialSurfaceBrush"));
        return panel;
    }

    // ---- keyboard -----------------------------------------------------------------------

    private void RenderKeyboard()
    {
        _keyboardStage ??= new Viewbox
        {
            Width = 600,
            Height = 340,
            Stretch = Stretch.Uniform,
            Child = _keyboard,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _stage.Content = _keyboardStage;
        ApplyKeyboard();
    }

    private void ApplyKeyboard()
    {
        _keyboard.Configuration = (MaterialKeyboardConfiguration)Math.Clamp(_keyboardConfig.SelectedIndex, 0, 3);
        _keyboard.Presentation = (MaterialKeyboardStyle)Math.Clamp(_keyboardStyle.SelectedIndex, 0, 2);
        _keyboard.Layer = (MaterialKeyboardLayer)Math.Clamp(_keyboardLayer.SelectedIndex, 0, 2);
        _keyboard.IsShifted = _shift.IsChecked == true;
        _keyboard.IsCapsLocked = _caps.IsChecked == true;
        _keyboard.ShowChromeBar = _chromeBar.IsChecked == true;
        _keyboard.ShowBottomBar = _bottomBar.IsChecked == true;
        _keyboard.IsOpen = _keyboardOpen.IsChecked == true;
        _keyboard.Width = _keyboard.Presentation switch
        {
            MaterialKeyboardStyle.Landscape => 868,
            MaterialKeyboardStyle.Floating => 342,
            _ => 412,
        };
        _status.Text = $"{_keyboard.Configuration} · {_keyboard.Presentation} · {_keyboard.Layer}";
    }

    private static string Describe(string text) => text switch
    {
        " " => "пробел",
        "\n" or "\r\n" => "перевод строки",
        _ => $"\"{text}\"",
    };

    // ---- building blocks ----------------------------------------------------------------

    private void RenderBlocks()
    {
        var block = Math.Clamp(_block.SelectedIndex, 0, 5);
        _overline.IsEnabled = block <= 1;
        _blockSupporting.IsEnabled = block != 2 && block != 4;
        _slotFilled.IsEnabled = block == 2;
        _tileSelected.IsEnabled = block == 3;
        _tileText.IsEnabled = block == 3;
        Control content;
        switch (block)
        {
            case 1:
            {
                content = new MaterialContentBlock
                {
                    Label = "Label",
                    SupportingText = "Supporting text",
                    ShowOverline = false,
                    ShowSupportingText = _blockSupporting.IsChecked == true,
                    Width = 320,
                    HorizontalAlignment = HorizontalAlignment.Left,
                };
                _status.Text = ".Building Blocks/Content: label и supporting text без overline.";
                break;
            }
            case 2:
            {
                var slot = new MaterialSlotHost
                {
                    Width = 360,
                    Height = 240,
                    HorizontalAlignment = HorizontalAlignment.Left,
                };
                if (_slotFilled.IsChecked == true)
                    slot.Content = new Border { CornerRadius = new CornerRadius(12), Child = ExamplesScreens.Artwork() };
                content = slot;
                _status.Text = _slotFilled.IsChecked == true
                    ? "Слот заполнен: подсказка пустого состояния скрыта."
                    : "Пустой слот: safety zone и подсказка замены.";
                break;
            }
            case 3:
            {
                var tile = ExamplesScreens.Tile("Label", 316, 204,
                    _blockSupporting.IsChecked == true ? "Supporting Text" : null, _tileSelected.IsChecked == true);
                tile.ShowCarouselText = _tileText.IsChecked == true;
                tile.Invoked += (_, _) => _status.Text = "Плитка активирована.";
                content = tile;
                _status.Text = "General item: активируется указателем, Enter и Space.";
                break;
            }
            case 4:
            {
                content = BuildLabelBlock();
                _status.Text = "Label.kt: постоянный label над значением и label по наведению/нажатию. " +
                               "В source Label оборачивает thumb; здесь label привязан к Slider сверху, " +
                               "а следование за thumb — обёртка thumb в шаблоне Slider.";
                break;
            }
            case 5:
            {
                var single = new TextBlock { Text = "Native text autosize", Width = 320, Height = 60 };
                MaterialTypography.SetAutoSize(single, MaterialTextAutoSize.StepBased(12, 48, 1));
                var multiple = new TextBlock { Text = "Resize-aware native multiline text, shaped and measured by Avalonia.", Width = 320, Height = 100, TextWrapping = TextWrapping.Wrap, MaxLines = 2 };
                MaterialTypography.SetAutoSize(multiple, MaterialTextAutoSize.StepBased(12, 36, 1));
                var rich = new TextBlock { Width = 320, TextWrapping = TextWrapping.Wrap };
                rich.Inlines!.Add(new Run("Native inline "));
                rich.Inlines.Add(new Bold { Inlines = { new Run("content") } });
                rich.Inlines.Add(new Run(" and "));
                rich.Inlines.Add(MaterialTypography.CreateLink("action link", () => _status.Text = "Native link activated."));
                content = new StackPanel { Spacing = 16, Children = { single, multiple, rich } };
                _status.Text = "Native TextBlock/Inlines: discrete autosize; inline HyperlinkButton preserves keyboard action.";
                break;
            }
            default:
            {
                content = new MaterialContentBlock
                {
                    Overline = "Overline",
                    Label = "Label text",
                    SupportingText = "Supporting line text, lorem ipsum dolor",
                    ShowOverline = _overline.IsChecked == true,
                    ShowSupportingText = _blockSupporting.IsChecked == true,
                    Width = 400,
                    HorizontalAlignment = HorizontalAlignment.Left,
                };
                _status.Text = "Content: overline, label и supporting text.";
                break;
            }
        }
        _stage.Content = content;
    }

    private Control BuildLabelBlock()
    {
        // Label.kt anchors a plain tooltip above its content. The persistent variant binds to the
        // slider value; the interactive variant appears on hover or press. The label never owns
        // the value: the native Slider keeps its own editing, keyboard and automation contract.
        var persistent = new Slider { Minimum = 0, Maximum = 100, Value = 40, Width = 320 };
        var persistentLabel = new MaterialLabel
        {
            Content = persistent,
            IsPersistent = true,
            Label = FormatSliderValue(persistent.Value),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 32, 0, 0),
        };
        persistent.PropertyChanged += (_, change) =>
        {
            if (change.Property == RangeBase.ValueProperty)
                persistentLabel.Label = FormatSliderValue(persistent.Value);
        };

        var interactive = new Slider { Minimum = 0, Maximum = 100, Value = 65, Width = 320 };
        var interactiveLabel = new MaterialLabel
        {
            Content = interactive,
            Label = FormatSliderValue(interactive.Value),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 32, 0, 0),
        };
        interactive.PropertyChanged += (_, change) =>
        {
            if (change.Property == RangeBase.ValueProperty)
                interactiveLabel.Label = FormatSliderValue(interactive.Value);
        };

        var panel = new StackPanel { Spacing = 12, HorizontalAlignment = HorizontalAlignment.Left };
        panel.Children.Add(Caption("Постоянный label"));
        panel.Children.Add(persistentLabel);
        panel.Children.Add(Caption("Label по наведению и нажатию"));
        panel.Children.Add(interactiveLabel);
        return panel;

        static TextBlock Caption(string text)
        {
            var caption = new TextBlock { Text = text, FontSize = 13 };
            caption.Bind(TextBlock.ForegroundProperty, caption.GetResourceObservable("MaterialOnSurfaceVariantBrush"));
            return caption;
        }
    }

    private static string FormatSliderValue(double value) => value.ToString("0");

    // ---- chrome -------------------------------------------------------------------------

    private void RenderChrome()
    {
        if (_chrome.SelectedIndex == 1)
        {
            var frame = new MaterialDeviceFrame
            {
                Width = 280,
                Height = 460,
                ShowStatusBar = _frameStatus.IsChecked == true,
                ShowGestureBar = _frameGesture.IsChecked == true,
                ShowCameraCutout = _frameCutout.IsChecked == true,
                TimeText = _frameTime.Text,
                IsScrollable = _frameScroll.IsChecked == true,
                TopContent = new TextBlock { Text = "Pinned top content", Margin = new Thickness(16, 12) },
                BottomContent = new TextBlock { Text = "Pinned bottom content", Margin = new Thickness(16, 12) },
                Content = Filler(),
            };
            _barCutout.IsEnabled = false;
            _barClock.IsEnabled = false;
            _barTime.IsEnabled = false;
            _frameStatus.IsEnabled = true;
            _frameGesture.IsEnabled = true;
            _frameCutout.IsEnabled = true;
            _frameScroll.IsEnabled = true;
            _frameTime.IsEnabled = true;
            _stage.Content = frame;
            _status.Text = "Device frame: 8 dp контур, pinned top/bottom, прокручиваемый Content.";
            return;
        }

        var bar = new MaterialStatusBar
        {
            Width = 412,
            ShowCameraCutout = _barCutout.IsChecked == true,
            ShowClock = _barClock.IsChecked == true,
            TimeText = _barClock.IsChecked == true ? null : _barTime.Text,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _barCutout.IsEnabled = true;
        _barClock.IsEnabled = true;
        _barTime.IsEnabled = _barClock.IsChecked != true;
        _frameStatus.IsEnabled = false;
        _frameGesture.IsEnabled = false;
        _frameCutout.IsEnabled = false;
        _frameScroll.IsEnabled = false;
        _frameTime.IsEnabled = false;
        _stage.Content = bar;
        _status.Text = _barClock.IsChecked == true
            ? "Status bar: часы тикают, пока бар на экране."
            : $"Status bar: фиксированное время {_barTime.Text}.";
    }

    private static Control Filler()
    {
        var panel = new StackPanel { Spacing = 8, Margin = new Thickness(16) };
        for (var i = 0; i < 12; i++)
        {
            var row = new Border { Height = 40, CornerRadius = new CornerRadius(8) };
            row.Bind(Border.BackgroundProperty, row.GetResourceObservable("MaterialSurfaceContainerHighestBrush"));
            panel.Children.Add(row);
        }
        return panel;
    }

    // ---- schematic ----------------------------------------------------------------------

    private void RenderSchematic()
    {
        _schematic.ShowDarkPreview = _darkPreview.IsChecked == true;
        _schematic.ShowFixedColors = _fixedColors.IsChecked == true;
        _schematic.SwatchWidth = _swatchWidth.Value;
        _stage.Content = _schematicStage;
        _status.Text = "Color-role schematic: нажатие на образец копирует hex.";
    }

    // ---- helpers ------------------------------------------------------------------------

    private static TextBlock Caption(string text) => new() { Text = text };

    private static ToggleSwitch Toggle(StackPanel host, string label, bool value)
    {
        var toggle = new ToggleSwitch { Content = label, IsChecked = value };
        host.Children.Add(toggle);
        return toggle;
    }

    private static TextBox Text(StackPanel host, string label, string value)
    {
        host.Children.Add(Caption(label));
        var box = new TextBox { Text = value, HorizontalAlignment = HorizontalAlignment.Stretch };
        MaterialTextField.SetVariant(box, MaterialTextFieldVariant.Outlined);
        MaterialTextField.SetLabel(box, label);
        host.Children.Add(box);
        return box;
    }

    private static Slider Number(StackPanel host, string label, double min, double max, double value, double step, string unit)
    {
        var caption = new TextBlock { Text = $"{label}: {value:0.##} {unit}" };
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
        slider.PropertyChanged += (_, e) =>
        {
            if (e.Property == RangeBase.ValueProperty) caption.Text = $"{label}: {slider.Value:0.##} {unit}";
        };
        AutomationProperties.SetName(slider, label);
        host.Children.Add(slider);
        return slider;
    }
}
