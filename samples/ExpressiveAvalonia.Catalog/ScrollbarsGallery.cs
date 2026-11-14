using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Catalog;

public static class ScrollbarsGallery
{
    public static Control Create() => new ScrollbarsPlayground();
}

/// <summary>
/// Demonstrates the optional passive <see cref="MaterialScrollbarOverlay"/> over native
/// <see cref="ScrollViewer"/> instances. The native scrollbars stay enabled so thumb drag, page
/// clicks, wheel and keyboard scrolling continue to use Avalonia's single scroll model.
/// </summary>
internal sealed class ScrollbarsPlayground : Grid, IComponentPlayground
{
    private readonly ScrollViewer _verticalScroll;
    private readonly ScrollViewer _horizontalScroll;
    private readonly MaterialScrollbarOverlay _verticalBar;
    private readonly MaterialScrollbarOverlay _horizontalBar;
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly ToggleSwitch _fade;
    private readonly ToggleSwitch _nativeBars;
    private readonly ToggleSwitch _rtl;
    private readonly ToggleSwitch _track;
    private readonly ToggleSwitch _precisionFlag;
    private readonly ToggleSwitch _precision;
    private readonly ComboBox _thumbRole;
    private readonly Slider _thickness;
    private readonly Slider _minLength;
    private readonly Slider _maxFraction;
    private readonly Slider _mainInset;
    private readonly Slider _crossInset;
    private readonly Slider _fadeDelay;
    private readonly Slider _fadeDuration;

    public StackPanel Settings { get; } = new() { Spacing = 16 };

    internal ScrollbarsPlayground()
    {
        var verticalContent = new StackPanel { Spacing = 6, Margin = new Thickness(8) };
        for (var i = 1; i <= 36; i++)
        {
            verticalContent.Children.Add(new Border
            {
                Height = 32,
                Padding = new Thickness(12, 0),
                CornerRadius = new CornerRadius(8),
                Child = new TextBlock
                {
                    Text = $"Строка {i:00}",
                    VerticalAlignment = VerticalAlignment.Center,
                },
            });
        }

        _verticalScroll = new ScrollViewer
        {
            Content = verticalContent,
            Height = 280,
            Width = 440,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        _verticalBar = new MaterialScrollbarOverlay
        {
            TargetScrollViewer = _verticalScroll,
            Orientation = Orientation.Vertical,
            ZIndex = 1,
        };

        var horizontalContent = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(8),
        };
        for (var i = 1; i <= 24; i++)
        {
            var card = new Border
            {
                Width = 88,
                Height = 116,
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(10),
                Child = new TextBlock
                {
                    Text = $"Карточка\n{i:00}",
                    TextWrapping = TextWrapping.Wrap,
                },
            };
            card.Bind(Border.BackgroundProperty, card.GetResourceObservable("MaterialSurfaceContainerHighBrush"));
            horizontalContent.Children.Add(card);
        }

        _horizontalScroll = new ScrollViewer
        {
            Content = horizontalContent,
            Height = 156,
            Width = 440,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        _horizontalBar = new MaterialScrollbarOverlay
        {
            TargetScrollViewer = _horizontalScroll,
            Orientation = Orientation.Horizontal,
            ZIndex = 1,
        };

        var verticalHost = new Grid();
        verticalHost.Children.Add(_verticalScroll);
        verticalHost.Children.Add(_verticalBar);

        var horizontalHost = new Grid();
        horizontalHost.Children.Add(_horizontalScroll);
        horizontalHost.Children.Add(_horizontalBar);

        var stage = new StackPanel
        {
            Spacing = 20,
            Margin = new Thickness(24),
            MaxWidth = 520,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        stage.Children.Add(Section("Вертикальный", "Родной вертикальный ScrollBar остаётся интерактивным; пассивный слой повторяет смещение, размер контента и viewport."));
        stage.Children.Add(verticalHost);
        stage.Children.Add(Section("Горизонтальный", "Track/thumb используют viewport, inset из источника и RTL-позицию; колесо и клавиатура продолжают прокручивать."));
        stage.Children.Add(horizontalHost);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        buttons.Children.Add(Action("В начало", () => { _verticalScroll.ScrollToHome(); _horizontalScroll.ScrollToHome(); }));
        buttons.Children.Add(Action("Середина", ScrollToMiddle));
        buttons.Children.Add(Action("В конец", () => { _verticalScroll.ScrollToEnd(); _horizontalScroll.ScrollToEnd(); }));
        stage.Children.Add(buttons);
        stage.Children.Add(_status);

        Children.Add(stage);

        _fade = Toggle("Fade lifecycle", true);
        _nativeBars = Toggle("Родные ScrollBar", true);
        _rtl = Toggle("RTL", false);
        _track = Toggle("Показать track", false);
        _precisionFlag = Toggle("Precision sizing flag (глобальный)", MaterialPrecisionPointer.IsComponentSizingEnabled);
        _precision = new ToggleSwitch
        {
            Content = "Precision pointer (host adapter)",
            IsChecked = MaterialPrecisionPointer.ShouldUsePrecisionPointerComponentSizing,
            IsEnabled = false,
        };
        var (thumbRoleHost, thumbRole) = Combo("Цвет thumb", ["Outline 70% (источник)", "Primary", "Error"]);
        _thumbRole = thumbRole;
        _thickness = NumberSlider(2, 12, 4);
        _minLength = NumberSlider(8, 48, 24);
        _maxFraction = NumberSlider(0.2, 1, 0.9);
        _mainInset = NumberSlider(0, 12, 2);
        _crossInset = NumberSlider(0, 12, 0);
        _fadeDelay = NumberSlider(0, 1000, 400);
        _fadeDuration = NumberSlider(0, 500, 250);

        Settings.Children.Add(_fade);
        Settings.Children.Add(_nativeBars);
        Settings.Children.Add(_rtl);
        Settings.Children.Add(_track);
        Settings.Children.Add(thumbRoleHost);
        Settings.Children.Add(Number("Толщина", _thickness));
        Settings.Children.Add(Number("Минимальная длина thumb", _minLength));
        Settings.Children.Add(Number("Максимальная доля thumb", _maxFraction));
        Settings.Children.Add(Number("Main-axis inset", _mainInset));
        Settings.Children.Add(Number("Cross-axis inset", _crossInset));
        Settings.Children.Add(Number("Fade delay, ms", _fadeDelay));
        Settings.Children.Add(Number("Fade duration, ms", _fadeDuration));
        Settings.Children.Add(_precisionFlag);
        Settings.Children.Add(_precision);
        Settings.Children.Add(new TextBlock
        {
            Text = "Пассивный слой не перехватывает ввод и не заменяет Avalonia ScrollBar: page/thumb/wheel работают через родной ScrollViewer. " +
                   "Precision-флаг повторяет источник (флаг + клавиатура + мышь); в Avalonia устройства наблюдаются по вводу окна. " +
                   "Pen/touch не считаются мышью. При reduced motion задержка fade сохраняется, длительность становится нулевой.",
            TextWrapping = TextWrapping.Wrap,
        });

        _fade.IsCheckedChanged += (_, _) => ApplySettings();
        _nativeBars.IsCheckedChanged += (_, _) => ApplySettings();
        _rtl.IsCheckedChanged += (_, _) => ApplySettings();
        _track.IsCheckedChanged += (_, _) => ApplySettings();
        _precisionFlag.IsCheckedChanged += (_, _) =>
        {
            MaterialPrecisionPointer.IsComponentSizingEnabled = _precisionFlag.IsChecked == true;
            UpdateStatus();
        };
        _thumbRole.SelectionChanged += (_, _) => ApplySettings();
        foreach (var slider in new[] { _thickness, _minLength, _maxFraction, _mainInset, _crossInset, _fadeDelay, _fadeDuration })
            slider.PropertyChanged += (_, e) =>
            {
                if (e.Property == RangeBase.ValueProperty)
                    ApplySettings();
            };

        AttachedToVisualTree += (_, _) =>
        {
            MaterialPrecisionPointer.Changed -= OnPrecisionPointerChanged;
            MaterialPrecisionPointer.Changed += OnPrecisionPointerChanged;
            MaterialPrecisionPointer.EnsureListenersRegistered(this);
            // Theme resources and the target viewports are available only now.
            ApplySettings();
            UpdateStatus();
        };
        DetachedFromVisualTree += (_, _) =>
            MaterialPrecisionPointer.Changed -= OnPrecisionPointerChanged;

        ApplySettings();
        UpdateStatus();
    }

    private void ApplySettings()
    {
        var fade = _fade.IsChecked == true;
        var thickness = _thickness.Value;
        var minLength = _minLength.Value;
        var maxFraction = _maxFraction.Value;
        var mainInset = _mainInset.Value;
        var crossInset = _crossInset.Value;

        foreach (var bar in new[] { _verticalBar, _horizontalBar })
        {
            bar.IsFadeEnabled = fade;
            bar.Thickness = thickness;
            bar.ThumbMinLength = minLength;
            bar.ThumbMaxLengthFraction = maxFraction;
            bar.MainAxisTrackInset = mainInset;
            bar.CrossAxisTrackInset = crossInset;
            bar.FadeDelayMillis = (int)Math.Round(_fadeDelay.Value);
            bar.FadeDurationMillis = (int)Math.Round(_fadeDuration.Value);
            bar.ThumbColor = ResolveThumbColor();
            bar.TrackColor = _track.IsChecked == true
                ? ResolveColor("MaterialOutlineVariantColor") ?? Colors.Transparent
                : Colors.Transparent;
            bar.FlowDirection = _rtl.IsChecked == true ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        }

        _verticalScroll.FlowDirection = _verticalBar.FlowDirection;
        _horizontalScroll.FlowDirection = _horizontalBar.FlowDirection;

        var native = _nativeBars.IsChecked == true
            ? ScrollBarVisibility.Auto
            : ScrollBarVisibility.Disabled;
        _verticalScroll.VerticalScrollBarVisibility = native;
        _horizontalScroll.HorizontalScrollBarVisibility = native;

        // A native visibility change can precede the metric update; force one invalidate.
        _verticalBar.InvalidateVisual();
        _horizontalBar.InvalidateVisual();
        UpdateStatus();
    }

    private Color? ResolveThumbColor() => _thumbRole.SelectedIndex switch
    {
        1 => ResolveColor("MaterialPrimaryColor"),
        2 => ResolveColor("MaterialErrorColor"),
        _ => null,
    };

    private Color? ResolveColor(string key) =>
        this.TryFindResource(key, out var value) && value is Color color ? color : null;

    private void OnPrecisionPointerChanged(object? sender, EventArgs e) => UpdateStatus();

    private void UpdateStatus()
    {
        _precision.IsChecked = MaterialPrecisionPointer.ShouldUsePrecisionPointerComponentSizing;
        _status.Text =
            $"Precision: флаг {YesNo(MaterialPrecisionPointer.IsComponentSizingEnabled)}, " +
            $"клавиатура {YesNo(MaterialPrecisionPointer.IsKeyboardPresent)}, " +
            $"мышь {YesNo(MaterialPrecisionPointer.IsMousePresent)}, " +
            $"dense {YesNo(MaterialPrecisionPointer.ShouldUsePrecisionPointerComponentSizing)}. " +
            $"Overlay: fade {YesNo(_fade.IsChecked == true)}, " +
            $"родные scrollbars {YesNo(_nativeBars.IsChecked == true)}, " +
            $"RTL {YesNo(_rtl.IsChecked == true)}.";
    }

    private void ScrollToMiddle()
    {
        _verticalScroll.Offset = new Vector(
            _verticalScroll.Offset.X,
            Math.Max(0, (_verticalScroll.Extent.Height - _verticalScroll.Viewport.Height) / 2));
        _horizontalScroll.Offset = new Vector(
            Math.Max(0, (_horizontalScroll.Extent.Width - _horizontalScroll.Viewport.Width) / 2),
            _horizontalScroll.Offset.Y);
    }

    private static string YesNo(bool value) => value ? "да" : "нет";

    private static TextBlock Section(string title, string description) => new()
    {
        Text = $"{title}\n{description}",
        TextWrapping = TextWrapping.Wrap,
        FontWeight = FontWeight.SemiBold,
    };

    private static ToggleSwitch Toggle(string label, bool value) =>
        new() { Content = label, IsChecked = value, HorizontalAlignment = HorizontalAlignment.Stretch };

    private static (Control Host, ComboBox Combo) Combo(string label, string[] items)
    {
        var combo = new ComboBox { ItemsSource = items, SelectedIndex = 0 };
        var host = new StackPanel { Spacing = 4 };
        host.Children.Add(new TextBlock { Text = label });
        host.Children.Add(combo);
        return (host, combo);
    }

    private static Slider NumberSlider(double min, double max, double value) => new()
    {
        Minimum = min,
        Maximum = max,
        Value = value,
        TickFrequency = Math.Max((max - min) / 20, 0.01),
        IsSnapToTickEnabled = false,
    };

    private static Control Number(string label, Slider slider)
    {
        var value = new TextBlock { Text = slider.Value.ToString("0.##"), HorizontalAlignment = HorizontalAlignment.Right };
        slider.PropertyChanged += (_, e) =>
        {
            if (e.Property == RangeBase.ValueProperty)
                value.Text = slider.Value.ToString("0.##");
        };
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        header.Children.Add(new TextBlock { Text = label });
        Grid.SetColumn(value, 1);
        header.Children.Add(value);
        var host = new StackPanel { Spacing = 4 };
        host.Children.Add(header);
        host.Children.Add(slider);
        return host;
    }

    private static Button Action(string label, Action action)
    {
        var button = new Button { Content = label, Padding = new Thickness(16, 8) };
        button.Click += (_, _) => action();
        return button;
    }
}