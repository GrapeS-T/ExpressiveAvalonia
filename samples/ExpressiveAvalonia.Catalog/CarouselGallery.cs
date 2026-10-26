using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Catalog;

/// <summary>One live carousel with its layout, ladder, card count and sizing exposed in the inspector.</summary>
public static class CarouselGallery
{
    public static Control Create() => new CarouselPlayground();
}

internal sealed class CarouselPlayground : Grid, IComponentPlayground
{
    private static readonly Uri SourceImageUri =
        new("avares://ExpressiveAvalonia/Assets/DesignSystem/Carousel/carousel-item.png");

    private static readonly string[] Layouts =
    {
        "Hero",
        "Center-aligned hero",
        "Multi-browse",
        "Uncontained",
        "Multi-aspect ratio",
        "Full screen",
    };

    private static readonly Lazy<Bitmap> SourceImage = new(() => new Bitmap(AssetLoader.Open(SourceImageUri)));

    private readonly MaterialCarousel _carousel = new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        SelectedIndex = 0,
    };
    private readonly TextBlock _status = new()
    {
        Text = "Selected: none",
        HorizontalAlignment = HorizontalAlignment.Center,
        TextWrapping = TextWrapping.Wrap,
    };
    private readonly ComboBox _layout = new() { ItemsSource = Layouts, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ComboBox _context = new() { ItemsSource = Enum.GetNames<MaterialCarouselContext>(), SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly Slider _count;
    private readonly Slider _height;
    private readonly Slider _spacing;
    private readonly Slider _width;
    private readonly Slider _fullHeight;
    private readonly ToggleSwitch _fourth;
    private readonly ToggleSwitch _fifth;
    private readonly ToggleSwitch _text;
    private readonly ToggleSwitch _snap;
    private readonly ToggleSwitch _disableLast;
    private MaterialCarouselLayout _builtLayout = (MaterialCarouselLayout)(-1);
    private bool _rebuilding;
    public StackPanel Settings { get; } = new() { Spacing = 16 };

    internal CarouselPlayground()
    {
        RowDefinitions = new RowDefinitions("*,Auto");
        var stage = new StackPanel
        {
            Spacing = 20,
            Margin = new Thickness(24),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        stage.Children.Add(_carousel);
        Children.Add(stage);
        Grid.SetRow(_status, 1);
        _status.Margin = new Thickness(24, 0, 24, 20);
        Children.Add(_status);

        Settings.Children.Add(Caption("Layout"));
        Settings.Children.Add(_layout);
        Settings.Children.Add(Caption("Context"));
        Settings.Children.Add(_context);
        _count = Number(Settings, "Количество карточек", 1, 8, 5, 1, "");
        _height = Number(Settings, "Высота карточки", 120, 320, 204, 4, "dp");
        _spacing = Number(Settings, "Промежуток", 0, 32, 8, 1, "dp");
        _width = Number(Settings, "Ширина", 320, 900, 520, 10, "dp");
        _fullHeight = Number(Settings, "Высота full screen", 320, 760, 560, 20, "dp");
        _fourth = Toggle(Settings, "Показывать 4-ю карточку", true);
        _fifth = Toggle(Settings, "Показывать 5-ю карточку", true);
        _text = Toggle(Settings, "Текст на карточках", true);
        _snap = Toggle(Settings, "Привязка прокрутки к карточкам", true);
        _disableLast = Toggle(Settings, "Последняя карточка недоступна", false);

        Settings.Children.Add(new TextBlock
        {
            Text = "Карточки листаются колесом или панорамированием и привязываются к началу карточки; " +
                   "Left/Right меняют выбор, Home/End прыгают к краям, Enter/Space активируют карточку. " +
                   "Выбор прокручивает карточку в вид. Полноэкранный layout заполняет viewport одной карточкой.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
        });

        AutomationProperties.SetName(_layout, "Layout карусели");
        AutomationProperties.SetName(_context, "Context карусели");

        _layout.SelectionChanged += (_, _) => { if (!_rebuilding) Apply(); };
        _context.SelectionChanged += (_, _) => { if (!_rebuilding) Apply(); };
        foreach (var slider in new[] { _count, _height, _spacing, _width, _fullHeight })
            slider.PropertyChanged += (_, e) => { if (!_rebuilding && e.Property == RangeBase.ValueProperty) Apply(); };
        foreach (var toggle in new[] { _fourth, _fifth, _text, _snap, _disableLast })
            toggle.IsCheckedChanged += (_, _) => { if (!_rebuilding) Apply(); };

        _carousel.SelectionChanged += (_, _) =>
        {
            if (_rebuilding) return;
            _status.Text = _carousel.SelectedIndex >= 0
                ? $"Выбрана карточка {_carousel.SelectedIndex + 1} из {_carousel.ItemCount}"
                : "Selected: none";
        };
        _carousel.ItemInvoked += (_, e) =>
            _status.Text = $"Активирована {e.Item.Label} (index {e.Index})";

        Apply();
    }

    private void Apply()
    {
        _rebuilding = true;
        var layout = (MaterialCarouselLayout)Math.Clamp(_layout.SelectedIndex, 0, Layouts.Length - 1);
        var full = layout == MaterialCarouselLayout.FullScreen;
        _carousel.Layout = layout;
        _carousel.Context = (MaterialCarouselContext)Math.Clamp(_context.SelectedIndex, 0, 1);
        _carousel.ItemHeight = _height.Value;
        _carousel.ItemSpacing = _spacing.Value;
        _carousel.Width = _width.Value;
        _carousel.Height = full ? _fullHeight.Value : double.NaN;
        _carousel.ShowFourthItem = _fourth.IsChecked == true;
        _carousel.ShowFifthItem = _fifth.IsChecked == true;
        _carousel.IsSnapToItems = _snap.IsChecked == true;
        _context.IsEnabled = !full;
        _height.IsEnabled = !full;
        _fullHeight.IsEnabled = full;

        var count = (int)Math.Round(_count.Value);
        if (_carousel.ItemCount != count || _builtLayout != layout)
            RebuildItems(layout, count);
        else
            UpdateItems();

        _rebuilding = false;
        _status.Text = _carousel.SelectedIndex >= 0
            ? $"Выбрана карточка {_carousel.SelectedIndex + 1} из {_carousel.ItemCount}"
            : $"Карточек: {_carousel.ItemCount}";
    }

    private void RebuildItems(MaterialCarouselLayout layout, int count)
    {
        var previous = _carousel.SelectedIndex;
        _carousel.Items.Clear();
        for (var i = 0; i < count; i++)
        {
            var ratio = layout == MaterialCarouselLayout.MultiAspectRatio ? SourceRatio(i) : 16d / 9d;
            _carousel.Items.Add(Card(i, ratio));
        }
        _builtLayout = layout;
        _carousel.SelectedIndex = count > 0 ? Math.Clamp(previous, 0, count - 1) : -1;
    }

    private void UpdateItems()
    {
        var showText = _text.IsChecked == true;
        var disableLast = _disableLast.IsChecked == true;
        for (var i = 0; i < _carousel.ItemCount; i++)
        {
            if (_carousel.Items[i] is not MaterialCarouselItem item) continue;
            item.ShowContent = showText;
            item.ShowSupportingText = showText;
            item.IsEnabled = !(disableLast && i == _carousel.ItemCount - 1);
        }
    }

    private MaterialCarouselItem Card(int index, double ratio)
    {
        var showText = _text.IsChecked == true;
        var item = new MaterialCarouselItem
        {
            ImageSource = SourceImage.Value,
            Label = $"Card {index + 1}",
            SupportingText = $"{Layouts[Math.Clamp(_layout.SelectedIndex, 0, Layouts.Length - 1)]} · card {index + 1}",
            ShowContent = showText,
            ShowSupportingText = showText,
            AspectRatio = ratio,
        };
        if (_disableLast.IsChecked == true && index == (int)Math.Round(_count.Value) - 1)
            item.IsEnabled = false;
        return item;
    }

    private static double SourceRatio(int index) => index switch
    {
        0 => 16d / 9d,
        1 => 4d / 3d,
        2 => 1d,
        3 => 3d / 4d,
        _ => 9d / 16d,
    };

    private static TextBlock Caption(string text) => new() { Text = text };

    private static ToggleSwitch Toggle(StackPanel host, string label, bool value)
    {
        var toggle = new ToggleSwitch { Content = label, IsChecked = value };
        host.Children.Add(toggle);
        return toggle;
    }

    private static Slider Number(StackPanel host, string label, double min, double max, double value, double step, string unit)
    {
        var caption = new TextBlock { Text = $"{label}: {value:0} {unit}" };
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
            if (e.Property == RangeBase.ValueProperty) caption.Text = $"{label}: {slider.Value:0} {unit}";
        };
        AutomationProperties.SetName(slider, label);
        host.Children.Add(slider);
        return slider;
    }
}
