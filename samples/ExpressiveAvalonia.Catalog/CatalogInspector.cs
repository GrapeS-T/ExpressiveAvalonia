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
    private Slider _width = null!;
    private CheckBox _autoWidth = null!;
    private Action? _restorePage;

    internal CatalogInspector(MaterialTheme theme, Window window)
    {
        Content = _settings;
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        AllowAutoHide = false;
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
        _page = page;
        _pageSettings.Children.Clear();
        Offset = default;
        page.Width = _previewWidth;
        page.MinHeight = _previewHeight;
        page.IsEnabled = _previewEnabled;
        page.FlowDirection = _previewFlow;
        var descendants = page.GetLogicalDescendants().OfType<Control>().Prepend(page).ToArray();
        if (descendants.OfType<IComponentPlayground>().FirstOrDefault() is { } playground)
        {
            _pageSettings.Children.Add(playground.Settings);
            return;
        }
        var hasButtons = descendants.OfType<Button>().Any(b => b is not MaterialFabBase) || descendants.OfType<ViewportGallery>().Any();
        if (hasButtons)
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

}
