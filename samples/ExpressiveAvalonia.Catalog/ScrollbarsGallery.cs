using System;
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
    private readonly ToggleSwitch _track;
    private readonly ToggleSwitch _rtl;
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

    public StackPanel Settings { get; } = InspectorLayout.CreatePane();

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
        stage.Children.Add(StageSection("Вертикальный", "Родной вертикальный ScrollBar остаётся интерактивным; пассивный слой повторяет смещение, размер контента и viewport."));
        stage.Children.Add(verticalHost);
        stage.Children.Add(StageSection("Горизонтальный", "Track/thumb используют viewport, inset из источника и RTL-позицию; колесо и клавиатура продолжают прокручивать."));
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

        var overlaySection = InspectorLayout.Section(Settings, "Поведение оверлея", divider: false);
        var overlaySwitches = InspectorLayout.Switches(overlaySection);
        _fade = InspectorLayout.Switch(overlaySwitches, "Fade lifecycle", true);
        _nativeBars = InspectorLayout.Switch(overlaySwitches, "Родные ScrollBar", true);
        _track = InspectorLayout.Switch(overlaySwitches, "Показать track", false);
        _rtl = InspectorLayout.Switch(overlaySwitches, "RTL", false);
        _thumbRole = InspectorLayout.Combo(
            overlaySection,
            "Цвет thumb",
            new[] { "Outline 70% (источник)", "Primary", "Error" },
            0);

        var geometrySection = InspectorLayout.Section(Settings, "Геометрия и тайминг");
        _thickness = InspectorLayout.Slider(geometrySection, "Толщина", 2, 12, 4, step: 0.5, unit: "dp", format: "0.##", snapToTick: false);
        _minLength = InspectorLayout.Slider(geometrySection, "Минимальная длина thumb", 8, 48, 24, step: 1, unit: "dp", format: "0.##", snapToTick: false);
        _maxFraction = InspectorLayout.Slider(geometrySection, "Максимальная доля thumb", 0.2, 1, 0.9, step: 0.05, unit: "", format: "0.##", snapToTick: false);
        _mainInset = InspectorLayout.Slider(geometrySection, "Main-axis inset", 0, 12, 2, step: 0.5, unit: "dp", format: "0.##", snapToTick: false);
        _crossInset = InspectorLayout.Slider(geometrySection, "Cross-axis inset", 0, 12, 0, step: 0.5, unit: "dp", format: "0.##", snapToTick: false);
        _fadeDelay = InspectorLayout.Slider(geometrySection, "Fade delay", 0, 1000, 400, step: 50, unit: "ms", format: "0", snapToTick: false);
        _fadeDuration = InspectorLayout.Slider(geometrySection, "Fade duration", 0, 500, 250, step: 25, unit: "ms", format: "0", snapToTick: false);

        var precisionSection = InspectorLayout.Section(Settings, "Precision pointer");
        var precisionSwitches = InspectorLayout.Switches(precisionSection);
        _precisionFlag = InspectorLayout.Switch(
            precisionSwitches,
            "Precision sizing flag (глобальный)",
            MaterialPrecisionPointer.IsComponentSizingEnabled);
        _precision = InspectorLayout.Switch(
            precisionSwitches,
            "Precision pointer (host adapter)",
            MaterialPrecisionPointer.ShouldUsePrecisionPointerComponentSizing);
        _precision.IsEnabled = false;

        InspectorLayout.Note(
            Settings,
            "Пассивный слой не перехватывает ввод и не заменяет Avalonia ScrollBar: page/thumb/wheel работают через родной ScrollViewer. " +
            "Precision-флаг повторяет источник (флаг + клавиатура + мышь); в Avalonia устройства наблюдаются по вводу окна. " +
            "Pen/touch не считаются мышью. При reduced motion задержка fade сохраняется, длительность становится нулевой.");

        _fade.IsCheckedChanged += (_, _) => ApplySettings();
        _nativeBars.IsCheckedChanged += (_, _) => ApplySettings();
        _track.IsCheckedChanged += (_, _) => ApplySettings();
        _rtl.IsCheckedChanged += (_, _) => ApplySettings();
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
            InspectorLayout.ApplyFlowDirection(bar, _rtl);
        }

        InspectorLayout.ApplyFlowDirection(_verticalScroll, _rtl);
        InspectorLayout.ApplyFlowDirection(_horizontalScroll, _rtl);

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

    private static TextBlock StageSection(string title, string description) => new()
    {
        Text = $"{title}\n{description}",
        TextWrapping = TextWrapping.Wrap,
        FontWeight = FontWeight.SemiBold,
    };

    private static Button Action(string label, Action action)
    {
        var button = new Button { Content = label, Padding = new Thickness(16, 8) };
        button.Click += (_, _) => action();
        return button;
    }
}
