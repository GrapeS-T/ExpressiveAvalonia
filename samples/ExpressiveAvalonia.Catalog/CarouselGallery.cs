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
        "Multi-aspect ratio / parallax",
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
    private static readonly string[] FlingBehaviors = { "Single advance", "Velocity", "No snap" };
    private readonly ComboBox _fling = new() { ItemsSource = FlingBehaviors, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBlock _current = new() { Text = "CurrentItem: —", FontSize = 13 };
    private readonly Slider _count;
    private readonly Slider _height;
    private readonly Slider _spacing;
    private readonly Slider _width;
    private readonly Slider _fullHeight;
    private readonly ToggleSwitch _text;
    private readonly ToggleSwitch _snap;
    private readonly ToggleSwitch _userScroll;
    private readonly ToggleSwitch _disableLast;
    private readonly ToggleSwitch _rtl;
    private readonly ToggleSwitch _fillHeight;
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
        var showAll = new Button { Content = "Показать все", HorizontalAlignment = HorizontalAlignment.Right };
        showAll.Click += (_, _) => ShowAllItems();
        stage.Children.Add(showAll);
        stage.Children.Add(CreateTransitionsDemo());
        Children.Add(stage);
        Grid.SetRow(_status, 1);
        _status.Margin = new Thickness(24, 0, 24, 20);
        Children.Add(_status);

        Settings.Children.Add(Caption("Layout"));
        Settings.Children.Add(_layout);
        Settings.Children.Add(Caption("Fling settling"));
        Settings.Children.Add(_fling);
        _count = Number(Settings, "Количество карточек", 0, 8, 5, 1, "");
        _height = Number(Settings, "Высота карточки", 120, 320, 204, 4, "dp");
        _spacing = Number(Settings, "Промежуток", 0, 32, 8, 1, "dp");
        _width = Number(Settings, "Ширина", 320, 900, 520, 10, "dp");
        _fullHeight = Number(Settings, "Высота full screen", 320, 760, 560, 20, "dp");
        _text = Toggle(Settings, "Текст на карточках", true);
        _snap = Toggle(Settings, "Привязка прокрутки к карточкам", true);
        _userScroll = Toggle(Settings, "Прокрутка пользователем", true);
        _disableLast = Toggle(Settings, "Последняя карточка недоступна", false);
        _rtl = Toggle(Settings, "Right-to-left", false);
        _fillHeight = Toggle(Settings, "Высота по контейнеру", false);
        Settings.Children.Add(_current);
        Settings.Children.Add(CreateMutationButtons());

        Settings.Children.Add(new TextBlock
        {
            Text = "Карточки листаются колесом, панорамированием или touch-жестом и привязываются к началу " +
                   "карточки; Left/Right меняют выбор, Home/End прыгают к краям, PageUp/PageDown листают " +
                   "страницу, Enter/Space активируют карточку. Выбор прокручивает карточку в вид; полный " +
                   "размер экрана заполняет viewport одной карточкой. Fling settling переопределяет " +
                   "поведение из Layout; Right-to-left проверяет зеркальную раскладку, кнопки ниже " +
                   "вставляют и удаляют карточку рядом с текущей, а также прыгают и анимируют к элементу. " +
                   "«Прокрутка пользователем» выключает жест, колесо, PageUp/PageDown и scroll-действия " +
                   "automation, оставляя карточки, клики, выбор и программную прокрутку.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
        });

        AutomationProperties.SetName(_layout, "Layout карусели");
        AutomationProperties.SetName(_fling, "Fling settling");
        AutomationProperties.SetName(_rtl, "Right-to-left");
        AutomationProperties.SetName(_fillHeight, "Высота по контейнеру");
        AutomationProperties.SetName(_userScroll, "Прокрутка пользователем");

        _layout.SelectionChanged += (_, _) => { if (!_rebuilding) Apply(); };
        _fling.SelectionChanged += (_, _) => { if (!_rebuilding) Apply(); };
        foreach (var slider in new[] { _count, _height, _spacing, _width, _fullHeight })
            slider.PropertyChanged += (_, e) => { if (!_rebuilding && e.Property == RangeBase.ValueProperty) Apply(); };
        foreach (var toggle in new[] { _text, _snap, _userScroll, _disableLast, _rtl, _fillHeight })
            toggle.IsCheckedChanged += (_, _) => { if (!_rebuilding) Apply(); };

        _carousel.SelectionChanged += (_, _) =>
        {
            if (_rebuilding) return;
            _status.Text = _carousel.SelectedIndex >= 0
                ? $"Выбрана карточка {_carousel.SelectedIndex + 1} из {_carousel.ItemCount}, CurrentItem: {_carousel.CurrentItem}"
                : "Selected: none";
        };
        _carousel.PropertyChanged += (_, e) =>
        {
            if (e.Property == MaterialCarousel.CurrentItemProperty)
                _current.Text = $"CurrentItem: {_carousel.CurrentItem}, SelectedIndex: {_carousel.SelectedIndex}";
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
        var fill = _fillHeight.IsChecked == true;
        _carousel.Layout = layout;
        _carousel.FlowDirection = _rtl.IsChecked == true
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;
        _carousel.ItemHeight = fill ? double.NaN : _height.Value;
        _carousel.ItemSpacing = _spacing.Value;
        _carousel.Width = _width.Value;
        _carousel.Height = full || fill ? _fullHeight.Value : double.NaN;
        _carousel.IsSnapToItems = _snap.IsChecked == true;
        _carousel.UserScrollEnabled = _userScroll.IsChecked == true;
        ApplyFlingBehavior();
        _height.IsEnabled = !full && !fill;
        _fullHeight.IsEnabled = full || fill;

        var count = (int)Math.Round(_count.Value);
        if (_carousel.ItemCount != count || _builtLayout != layout)
            RebuildItems(layout, count);
        else
            UpdateItems();

        _rebuilding = false;
        _status.Text = _carousel.SelectedIndex >= 0
            ? $"Выбрана карточка {_carousel.SelectedIndex + 1} из {_carousel.ItemCount}, CurrentItem: {_carousel.CurrentItem}"
            : $"Карточек: {_carousel.ItemCount}";
        _current.Text = $"CurrentItem: {_carousel.CurrentItem}, SelectedIndex: {_carousel.SelectedIndex}";
    }

    private async void ShowAllItems()
    {
        if (TopLevel.GetTopLevel(this) is not { } owner) return;
        var list = new StackPanel { Spacing = 8 };
        for (var i = 0; i < _carousel.ItemCount; i++)
        {
            if (_carousel.Items[i] is not MaterialCarouselItem item) continue;
            var index = i;
            var button = new Button { Content = item.Label, IsEnabled = item.IsEnabled,
                HorizontalAlignment = HorizontalAlignment.Stretch };
            button.Click += (_, _) =>
            {
                _carousel.SelectedIndex = index;
                item.Invoke();
            };
            list.Children.Add(button);
        }
        var dialog = new MaterialDialog { Title = "Все карточки", Content = list };
        dialog.AddAction(new MaterialDialogAction("Закрыть", null) { IsCancel = true });
        await dialog.ShowAsync(owner);
    }

    // Index 0 keeps the layout's own pinned default (single advance, no snap for uncontained); the
    // other entries are explicit overrides.
    private void ApplyFlingBehavior()
    {
        if (_fling.SelectedIndex <= 0)
        {
            _carousel.ClearValue(MaterialCarousel.FlingBehaviorProperty);
            return;
        }

        _carousel.FlingBehavior = (MaterialCarouselFlingBehavior)(_fling.SelectedIndex - 1);
    }

    private void RebuildItems(MaterialCarouselLayout layout, int count)
    {
        var previous = _carousel.SelectedIndex;
        _carousel.Items.Clear();
        for (var i = 0; i < count; i++)
            _carousel.Items.Add(Card(i));
        _builtLayout = layout;
        _carousel.SelectedIndex = count > 0 ? Math.Clamp(previous, 0, count - 1) : -1;
        UpdateItems();
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

    private MaterialCarouselItem Card(int index)
    {
        var showText = _text.IsChecked == true;
        var layout = (MaterialCarouselLayout)Math.Clamp(_layout.SelectedIndex, 0, Layouts.Length - 1);
        return new MaterialCarouselItem
        {
            ImageSource = SourceImage.Value,
            Label = $"Card {index + 1}",
            SupportingText = $"{Layouts[Math.Clamp(_layout.SelectedIndex, 0, Layouts.Length - 1)]} · card {index + 1}",
            ShowContent = showText,
            ShowSupportingText = showText,
            AspectRatio = layout == MaterialCarouselLayout.MultiAspectRatio ? SourceRatio(index) : 16d / 9d,
        };
    }

    /// <summary>
    /// Insert/remove next to the current card without rebuilding the collection, so the strip's
    /// mutation path (anchor + selection preservation) is exercised instead of a reset.
    /// </summary>
    private Control CreateMutationButtons()
    {
        var insert = new Button { Content = "Вставить после текущей" };
        var remove = new Button { Content = "Удалить текущую" };
        var jump = new Button { Content = "Jump к выбранной" };
        var animate = new Button { Content = "Animate к последней" };

        insert.Click += (_, _) =>
        {
            if (_carousel.ItemCount >= (int)_count.Maximum) return;
            var at = Math.Max(0, Math.Min(_carousel.CurrentItem, _carousel.ItemCount - 1)) + 1;
            _carousel.Items.Insert(at, Card(at));
            SyncCountToItems();
            UpdateItems();
            _status.Text = $"Вставлена карточка в позицию {at + 1}; карточек: {_carousel.ItemCount}";
        };

        remove.Click += (_, _) =>
        {
            if (_carousel.ItemCount == 0) return;
            var at = Math.Max(0, Math.Min(_carousel.CurrentItem, _carousel.ItemCount - 1));
            _carousel.Items.RemoveAt(at);
            SyncCountToItems();
            UpdateItems();
            _status.Text = $"Удалена карточка в позиции {at + 1}; карточек: {_carousel.ItemCount}, " +
                           $"CurrentItem: {_carousel.CurrentItem}";
        };

        jump.Click += (_, _) =>
        {
            if (_carousel.SelectedIndex >= 0)
                _carousel.ScrollToItem(_carousel.SelectedIndex);
        };

        animate.Click += (_, _) =>
        {
            if (_carousel.ItemCount > 0)
                _carousel.AnimateScrollToItem(_carousel.ItemCount - 1);
        };

        return new WrapPanel { Children = { insert, remove, jump, animate } };
    }

    // Keeps the count slider in sync with direct collection mutations without rebuilding the items.
    private void SyncCountToItems()
    {
        _rebuilding = true;
        _count.Value = Math.Clamp(_carousel.ItemCount, _count.Minimum, _count.Maximum);
        _rebuilding = false;
    }

    private static double SourceRatio(int index) => index switch
    {
        0 => 16d / 9d,
        1 => 4d / 3d,
        2 => 1d,
        3 => 3d / 4d,
        _ => 9d / 16d,
    };

    private static Control CreateTransitionsDemo()
    {
        var screens = new Control[3];
        var colors = new[] { "#6750A4", "#00696D", "#7D5260" };
        for (var i = 0; i < screens.Length; i++)
        {
            screens[i] = new Border
            {
                Background = new SolidColorBrush(Color.Parse(colors[i])),
                CornerRadius = new CornerRadius(16),
                Child = new TextBlock
                {
                    Text = $"Экран {i + 1}",
                    Foreground = Brushes.White,
                    FontSize = 24,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            };
        }

        var host = new MaterialTransitionHost
        {
            Width = 420,
            Height = 200,
            HorizontalAlignment = HorizontalAlignment.Center,
            Content = screens[0],
        };
        var status = new TextBlock { Text = "Готово", FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center };

        var family = new ComboBox
        {
            ItemsSource = new[] { "Horizontal shared axis", "Fade through" },
            SelectedIndex = 0,
            Width = 210,
        };
        family.SelectionChanged += (_, _) =>
            host.Transition = family.SelectedIndex == 1
                ? MaterialTransitionKind.FadeThrough
                : MaterialTransitionKind.HorizontalSharedAxis;

        var currentIndex = 0;
        var forward = new Button { Content = "Вперёд" };
        var back = new Button { Content = "Назад" };
        forward.Click += (_, _) =>
        {
            if (currentIndex >= screens.Length - 1) return;
            currentIndex++;
            host.Navigate(screens[currentIndex], forward: true);
            status.Text = $"Переход вперёд → экран {currentIndex + 1}";
        };
        back.Click += (_, _) =>
        {
            if (currentIndex <= 0) return;
            currentIndex--;
            host.Navigate(screens[currentIndex], forward: false);
            status.Text = $"Переход назад ← экран {currentIndex + 1}";
        };
        host.TransitionCompleted += (_, _) => status.Text = $"Завершено: экран {currentIndex + 1}";
        AutomationProperties.SetName(host, "Material transition host");
        AutomationProperties.SetName(family, "Семейство переходов");

        // Gesture preview: the pure pinned predictive-back math with the scheme scrim.
        var preview = new Panel { Width = 420, Height = 200, ClipToBounds = true };
        var current = new Border
        {
            Background = new SolidColorBrush(Color.Parse(colors[1])),
            CornerRadius = new CornerRadius(16),
            Child = new TextBlock { Text = "Текущий экран", Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
        };
        var target = new Border
        {
            Background = new SolidColorBrush(Color.Parse(colors[0])),
            CornerRadius = new CornerRadius(16),
            Child = new TextBlock { Text = "Предыдущий экран", Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
        };
        var veil = new Border { IsHitTestVisible = false, Opacity = 0 };
        preview.Children.Add(target);
        preview.Children.Add(current);
        preview.Children.Add(veil);
        var scrimColor = MaterialTransitionDefaults.TryGetScrimColor(preview, out var resolved)
            ? resolved
            : Color.FromArgb(51, 0, 0, 0);
        veil.Background = new SolidColorBrush(scrimColor);

        var progress = new Slider { Minimum = 0, Maximum = 100, Value = 0 };
        var previewSize = new Size(420, 200);
        void UpdatePreview()
        {
            var frame = MaterialTransitions.HorizontalSharedAxis.PredictiveBack(
                progress.Value / 100,
                rawYDelta: 0,
                previewSize,
                MaterialSwipeEdge.Left,
                MaterialTransitionDefaults.DisplayBoundsMargin,
                scrimColor);
            ApplyPreviewSurface(current, frame.Initial);
            ApplyPreviewSurface(target, frame.Target);
            veil.Opacity = progress.Value / 100;
        }
        progress.PropertyChanged += (_, e) =>
        {
            if (e.Property == RangeBase.ValueProperty) UpdatePreview();
        };
        UpdatePreview();
        AutomationProperties.SetName(progress, "Прогресс predictive back");

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children = { forward, back, family },
        };

        return new StackPanel
        {
            Spacing = 10,
            Children =
            {
                new TextBlock { Text = "Material transitions (S07.4)", FontSize = 16, FontWeight = FontWeight.SemiBold },
                host,
                buttons,
                status,
                new TextBlock { Text = "Predictive back: жестовая фаза (чистая математика pinned MaterialTransitions.kt)", FontSize = 13 },
                preview,
                progress,
            },
        };
    }

    private static void ApplyPreviewSurface(Control control, MaterialPredictiveBackSurface surface)
    {
        control.RenderTransformOrigin = RelativePoint.Center;
        var group = new TransformGroup();
        group.Children.Add(new ScaleTransform(surface.Scale, surface.Scale));
        group.Children.Add(new TranslateTransform(surface.OffsetX, surface.OffsetY));
        control.RenderTransform = group;
        control.Opacity = surface.Alpha;
    }

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
