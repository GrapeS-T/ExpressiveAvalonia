using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Styling;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Catalog;

internal sealed class CatalogInspector : ScrollViewer
{
    protected override Type StyleKeyOverride => typeof(ScrollViewer);

    private readonly StackPanel _settings = new() { Spacing = 16, Margin = new Thickness(20) };
    private readonly StackPanel _pageSettings = new() { Spacing = 16 };
    internal StackPanel StudioSettings { get; } = new() { Spacing = 16, Margin = new Thickness(24) };
    private readonly StackPanel _shared = new() { Spacing = 16 };
    private bool _previewEnabled = true;
    private FlowDirection _previewFlow;
    private double _previewWidth = double.NaN;
    private double _previewHeight;
    private Control? _page;
    private MaterialFabMenu[] _menus = [];
    private readonly Dictionary<MaterialFabMenu, object[]> _originalItems = new();
    private Slider _width = null!;
    private Slider? _count;
    private CheckBox _autoWidth = null!;
    private TextBlock _feedback = new() { TextWrapping = TextWrapping.Wrap };
    private Action? _restorePage;
    private Action? _exerciseCards;
    private static readonly Geometry Star = Geometry.Parse("M12,2 L15,8 L22,9 L17,14 L18,21 L12,18 L6,21 L7,14 L2,9 L9,8 Z");

    internal CatalogInspector(MaterialTheme theme, Window window)
    {
        Content = _settings;
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        AllowAutoHide = false;
        _settings.Children.Add(new TextBlock { Text = "Настройка примера", FontSize = 22, FontWeight = FontWeight.Bold });
        _settings.Children.Add(new TextBlock { Text = "Параметры применяются только к области слева.", TextWrapping = TextWrapping.Wrap, FontSize = 13 });
        var global = Section("Оформление студии");
        _settings.Children.Remove(global);
        StudioSettings.Children.Add(global);
        StudioSettings.Children.Add(_shared);
        var dark = new ToggleSwitch { Content = "Тёмная тема", IsChecked = window.ActualThemeVariant == ThemeVariant.Dark };
        var syncingTheme = false;
        dark.IsCheckedChanged += (_, _) =>
        {
            if (!syncingTheme) window.RequestedThemeVariant = dark.IsChecked == true ? ThemeVariant.Dark : ThemeVariant.Light;
        };
        window.ActualThemeVariantChanged += (_, _) =>
        {
            syncingTheme = true;
            dark.IsChecked = window.ActualThemeVariant == ThemeVariant.Dark;
            syncingTheme = false;
        };
        global.Children.Add(dark);
        var reduced = new ToggleSwitch { Content = "Меньше движения", IsChecked = theme.Motion == MaterialMotionMode.Reduced };
        reduced.IsCheckedChanged += (_, _) => theme.Motion = reduced.IsChecked == true ? MaterialMotionMode.Reduced : MaterialMotionMode.Expressive;
        global.Children.Add(reduced);
        _settings.Children.Add(_pageSettings);
        BuildSharedSettings();
    }

    private StackPanel Section(string title, bool page = false)
    {
        var panel = new StackPanel { Spacing = 16, Margin = new Thickness(0, 12, 0, 16) };
        panel.Children.Add(new TextBlock { Text = title, FontSize = 16, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap });
        (page ? _pageSettings : _settings).Children.Add(panel);
        return panel;
    }

    private sealed record OriginalButtonSize(MaterialControlSize Size);

    internal void SetPage(Control page, bool expressive)
    {
        _restorePage?.Invoke();
        _restorePage = null;
        _exerciseCards = null;
        foreach (var menu in _menus)
            if (_originalItems.Remove(menu, out var items))
            {
                menu.IsExpanded = false;
                menu.Items.Clear();
                foreach (var item in items) menu.Items.Add(item);
            }
        _page = page;
        _pageSettings.Children.Clear();
        _count = null;
        Offset = default;
        page.Width = _previewWidth;
        page.MinHeight = _previewHeight;
        page.IsEnabled = _previewEnabled;
        page.FlowDirection = _previewFlow;
        var descendants = page.GetLogicalDescendants().OfType<Control>().Prepend(page).ToArray();
        if (descendants.OfType<SliderPlayground>().FirstOrDefault() is { } sliders)
            _pageSettings.Children.Add(sliders.Settings);
        if (descendants.OfType<TextFieldPlayground>().FirstOrDefault() is { } fields)
            _pageSettings.Children.Add(fields.Settings);
        if (descendants.OfType<TabsPlayground>().FirstOrDefault() is { } tabs)
            _pageSettings.Children.Add(tabs.Settings);
        if (descendants.OfType<SearchPlayground>().FirstOrDefault() is { } search)
            _pageSettings.Children.Add(search.Settings);
        _menus = descendants.OfType<MaterialFabMenu>().ToArray();
        if (_menus.Length > 0) BuildMenus();
        var hasButtons = descendants.OfType<Button>().Any(b => b is not MaterialFabBase) || descendants.OfType<ViewportGallery>().Any();
        if (hasButtons && _menus.Length == 0)
        {
            var sizes = Section("Размер кнопок", true);
            var size = new ComboBox { ItemsSource = new[] { "Как в примере", "ExtraSmall", "Small", "Medium", "Large", "ExtraLarge" }, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
            var originals = new System.Runtime.CompilerServices.ConditionalWeakTable<Button, OriginalButtonSize>();
            void ApplySize()
            {
                var selected = size.SelectedIndex <= 0 ? (MaterialControlSize?)null : Enum.Parse<MaterialControlSize>((string)size.SelectedItem!);
                page.SetValue(ViewportGallery.SizeOverrideProperty, selected);
                foreach (var button in page.GetLogicalDescendants().OfType<Button>().Where(b => b is not MaterialFabBase))
                {
                    var original = originals.GetValue(button, b => new OriginalButtonSize(MaterialAssist.GetControlSize(b)));
                    MaterialAssist.SetControlSize(button, selected ?? original.Size);
                }
            }
            _restorePage = () => page.ClearValue(ViewportGallery.SizeOverrideProperty);
            size.SelectionChanged += (_, _) =>
            {
                ApplySize();
            };
            sizes.Children.Add(size);
        }
        var cards = descendants.OfType<MaterialCard>().ToArray();
        if (cards.Length > 0)
        {
            var section = Section("Размеры карточек", true);
            var originalSizes = cards.Select(card => (card.Width, card.Height)).ToArray();
            var previousRestore = _restorePage;
            _restorePage = () =>
            {
                previousRestore?.Invoke();
                for (var i = 0; i < cards.Length; i++)
                {
                    cards[i].Width = originalSizes[i].Width;
                    cards[i].Height = originalSizes[i].Height;
                }
            };
            var cardWidth = new Slider { Minimum = 200, Maximum = 600, Value = 320, TickFrequency = 20, IsSnapToTickEnabled = true };
            var cardHeight = new Slider { Minimum = 120, Maximum = 640, Value = 320, TickFrequency = 20, IsSnapToTickEnabled = true };
            var widthText = new TextBlock { Text = "Ширина блока: как в примере" };
            var heightText = new TextBlock { Text = "Высота блока: как в примере" };
            cardWidth.PropertyChanged += (_, e) =>
            {
                if (e.Property != RangeBase.ValueProperty) return;
                foreach (var card in cards) card.Width = cardWidth.Value;
                widthText.Text = $"Ширина блока: {cardWidth.Value:0} dp";
            };
            cardHeight.PropertyChanged += (_, e) =>
            {
                if (e.Property != RangeBase.ValueProperty) return;
                foreach (var card in cards) card.Height = cardHeight.Value;
                heightText.Text = $"Высота блока: {cardHeight.Value:0} dp";
            };
            section.Children.Add(widthText);
            section.Children.Add(cardWidth);
            section.Children.Add(heightText);
            section.Children.Add(cardHeight);
            _exerciseCards = () =>
            {
                cardWidth.Value = 400;
                cardHeight.Value = 360;
                if (cards.Any(card => card.Width != 400 || card.Height != 360))
                    throw new InvalidOperationException("Card dimensions did not update.");
                Console.WriteLine("INSPECTOR PASS: card blocks resized to 400 × 360 dp.");
            };
        }
    }
    private void BuildSharedSettings()
    {
        var layout = new StackPanel { Spacing = 16 };
        layout.Children.Add(new TextBlock { Text = "Область примера", FontSize = 16, FontWeight = FontWeight.Bold });
        _shared.Children.Add(layout);
        _autoWidth = new CheckBox { Content = "Автоширина", IsChecked = true };
        _width = new Slider { Minimum = 360, Maximum = 1440, Value = 840, TickFrequency = 40, IsSnapToTickEnabled = true, IsEnabled = false };
        var widthLabel = new TextBlock { Text = "Ширина: автоматически" };
        void ApplyWidth()
        {
            var auto = _autoWidth.IsChecked == true;
            _width.IsEnabled = !auto;
            _previewWidth = auto ? double.NaN : _width.Value;
            if (_page is not null) _page.Width = _previewWidth;
            widthLabel.Text = auto ? "Ширина: автоматически" : $"Ширина: {_width.Value:0} dp";
        }
        _autoWidth.IsCheckedChanged += (_, _) => ApplyWidth();
        _width.PropertyChanged += (_, e) => { if (e.Property == RangeBase.ValueProperty) ApplyWidth(); };
        layout.Children.Add(_autoWidth);
        layout.Children.Add(widthLabel);
        layout.Children.Add(_width);
        var heightLabel = new TextBlock { Text = "Мин. высота: 0 dp" };
        var height = new Slider { Minimum = 0, Maximum = 1000, TickFrequency = 50, IsSnapToTickEnabled = true };
        height.PropertyChanged += (_, e) =>
        {
            if (e.Property != RangeBase.ValueProperty) return;
            _previewHeight = height.Value;
            if (_page is not null) _page.MinHeight = _previewHeight;
            heightLabel.Text = $"Мин. высота: {height.Value:0} dp";
        };
        layout.Children.Add(heightLabel);
        layout.Children.Add(height);
        var enabled = new ToggleSwitch { Content = "Примеры доступны", IsChecked = true };
        enabled.IsCheckedChanged += (_, _) =>
        {
            _previewEnabled = enabled.IsChecked == true;
            if (_page is not null) _page.IsEnabled = _previewEnabled;
        };
        layout.Children.Add(enabled);
        var rtl = new ToggleSwitch { Content = "Справа налево (RTL)" };
        rtl.IsCheckedChanged += (_, _) =>
        {
            _previewFlow = rtl.IsChecked == true ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            if (_page is not null) _page.FlowDirection = _previewFlow;
        };
        layout.Children.Add(rtl);
    }

    private void BuildMenus()
    {
        foreach (var menu in _menus) _originalItems[menu] = menu.Items.Cast<object>().ToArray();
        var section = Section("FAB menu / действия", true);
        _feedback = new TextBlock { TextWrapping = TextWrapping.Wrap };
        var countLabel = new TextBlock { Text = "Количество: как в примере" };
        _count = new Slider { Minimum = 2, Maximum = 6, Value = 3, TickFrequency = 1, IsSnapToTickEnabled = true };
        var icons = new ToggleSwitch { Content = "Иконки действий", IsChecked = true };
        var open = new ToggleSwitch { Content = "Раскрыть меню" };
        void Populate()
        {
            var count = (int)Math.Round(_count.Value);
            countLabel.Text = $"Действий в каждом меню: {count}";
            foreach (var menu in _menus)
            {
                menu.Items.Clear();
                for (var i = 0; i < count; i++)
                {
                    var number = i + 1;
                    var segment = new MaterialFabMenuSegment { Label = $"Действие {number}", Icon = icons.IsChecked == true ? Star : null, HorizontalAlignment = HorizontalAlignment.Right };
                    segment.Click += (_, _) => _feedback.Text = $"Выполнено действие {number}";
                    menu.Items.Add(segment);
                }
                menu.IsExpanded = open.IsChecked == true;
            }
        }
        _count.PropertyChanged += (_, e) => { if (e.Property == RangeBase.ValueProperty) Populate(); };
        icons.IsCheckedChanged += (_, _) =>
        {
            foreach (var segment in _menus.SelectMany(m => m.Items.OfType<MaterialFabMenuSegment>()))
                segment.Icon = icons.IsChecked == true ? Star : null;
        };
        open.IsCheckedChanged += (_, _) => { foreach (var menu in _menus) menu.IsExpanded = open.IsChecked == true; };
        section.Children.Add(countLabel);
        section.Children.Add(_count);
        section.Children.Add(icons);
        section.Children.Add(open);
        section.Children.Add(_feedback);
        Populate();
    }

    internal void ExerciseSmoke()
    {
        if (_exerciseCards is not null) { _exerciseCards(); return; }
        if (_page is null || _menus.Length == 0 || _count is null) throw new InvalidOperationException("FAB inspector has no menus.");
        _count.Value = 6;
        if (_menus.Any(m => m.ItemCount != 6)) throw new InvalidOperationException("FAB action count did not change.");
        _autoWidth.IsChecked = false;
        _width.Value = 960;
        if (_page.Width != 960) throw new InvalidOperationException("Preview width did not change.");
        foreach (var menu in _menus) menu.IsExpanded = true;
        Console.WriteLine("INSPECTOR PASS: six real FAB actions per menu; preview width 960 dp.");
    }
}
