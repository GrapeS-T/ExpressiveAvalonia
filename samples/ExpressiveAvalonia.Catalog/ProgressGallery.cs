using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Shapes;
using ExpressiveAvalonia.Theming;
using Avalonia.VisualTree;

namespace ExpressiveAvalonia.Catalog;

/// <summary>Live Expressive progress indicators with animation controls.</summary>
public static class ProgressGallery
{
    public static Control Create() => new ProgressPlayground();
}

internal sealed class ProgressPlayground : Grid, IComponentPlayground
{
    // Custom container outline for the Loading indicator. The geometry is authored in a unit box
    // and stretched to the control bounds, exactly like a Compose Shape receives the layout size.
    private static readonly Geometry s_starContainer = Geometry.Parse(
        "M0.5,0 L0.61,0.35 L0.98,0.35 L0.68,0.57 L0.79,0.91 L0.5,0.7 L0.21,0.91 L0.32,0.57 " +
        "L0.02,0.35 L0.39,0.35 Z");

    // Custom polygon sequences replace the source list while the cycle runs. Each replacement
    // restarts the morph pair at index 0 and keeps the remembered progress and quarter turn.
    private static readonly IReadOnlyList<RoundedPolygon> s_pairShapes =
    [
        RoundedPolygon.FromNumVertices(4, rounding: new CornerRounding(0.35)),
        RoundedPolygon.FromNumVertices(7, rounding: new CornerRounding(0.45)),
    ];

    private static readonly IReadOnlyList<RoundedPolygon> s_tripleShapes =
    [
        RoundedPolygon.FromNumVertices(3, rounding: new CornerRounding(0.2)),
        RoundedPolygon.FromNumVertices(6, rounding: new CornerRounding(0.5)),
        RoundedPolygon.FromNumVertices(8, rounding: new CornerRounding(0.3)),
    ];

    private readonly ProgressBar _linear = new() { Value = 50, Width = 320 };
    private readonly CircularProgressIndicator _circular = new() { Value = 50, Width = 48, Height = 48 };
    private readonly LoadingIndicator _loading = new();
    private readonly Border _host = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _readout = new() { HorizontalAlignment = HorizontalAlignment.Center };
    private readonly MaterialButtonGroup _kind;
    private readonly MaterialButtonGroup _behavior;
    private readonly MaterialButtonGroup _shape;
    private readonly Slider _value;
    private readonly Slider _thickness;
    private readonly Slider _length;
    private readonly Slider _diameter;
    private readonly Slider _wavelength;
    private readonly Slider _indeterminateWavelength;
    private readonly Slider _circularWavelength;
    private readonly ToggleSwitch _animate;
    private readonly ToggleSwitch _track;
    private readonly ToggleSwitch _container;
    private readonly ToggleSwitch _containerShape;
    private readonly MaterialButtonGroup _sequence;
    private readonly ToggleSwitch _rtl;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1d / 30) };
    private bool _attached;
    private bool _updatingValue;
    private long _lastFrame;
    private double _completionHold;
    private readonly List<Visual> _visibilityAncestors = new();
    public StackPanel Settings { get; } = new() { Spacing = 16 };

    internal ProgressPlayground()
    {
        var stage = new StackPanel { Spacing = 32, Margin = new Thickness(24), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        stage.Children.Add(_host);
        stage.Children.Add(_readout);
        Children.Add(stage);
        _kind = Choice("Индикатор", new[] { "Linear", "Circular", "Loading" });
        _behavior = Choice("Поведение", new[] { "Determinate", "Indeterminate" });
        _shape = Choice("Форма", new[] { "Flat", "Wave" });
        _animate = Toggle("Анимация", true);
        _value = Number("Прогресс", 0, 100, 50, 1, "%");
        _thickness = Number("Толщина", 4, 8, 4, 1, "dp");
        _length = Number("Длина", 120, 720, 320, 4, "dp");
        _diameter = Number("Диаметр", 48, 144, 48, 4, "dp");
        _wavelength = Number("Длина волны linear", 10, 80, 40, 1, "dp");
        _indeterminateWavelength = Number("Длина волны linear indeterminate", 10, 80, 20, 1, "dp");
        _circularWavelength = Number("Длина волны circular", 8, 40, 15, 1, "dp");
        _track = Toggle("Дорожка Circular", true);
        _container = Toggle("Контейнер Loading", false);
        _containerShape = Toggle("Своя форма контейнера", false);
        _sequence = Choice("Последовательность форм", new[] { "Стандарт", "Пара", "Три" });
        _rtl = Toggle("Справа налево (RTL)", false);
        Settings.Children.Add(new TextBlock
        {
            Text = "MD3: Flat, 4 dp. MD3E: Flat/Wave, толщина 4–8 dp и Loading. Анимация determinate — демонстрационное заполнение 0–100%; выключите её для ручного значения. Indeterminate и Loading используют собственные циклы. Reduced motion останавливает движение; скрытая или закрытая страница не обновляется. Смена последовательности Loading на ходу заменяет список полигонов: форма начинается с первой пары новой последовательности, а текущий морф и запомненный угол поворота сохраняются (бесконечный твин поворота после замены повторяется от этого значения). Своя форма контейнера — произвольная Geometry, растянутая в границы и используемая и для заливки, и для обрезки.",
            TextWrapping = TextWrapping.Wrap, FontSize = 13
        });
        foreach (var group in new[] { _kind, _behavior, _shape, _sequence }) group.SelectionChanged += (_, _) => Apply();
        foreach (var slider in new[] { _value, _thickness, _length, _diameter, _wavelength, _indeterminateWavelength, _circularWavelength })
            slider.PropertyChanged += (_, e) => { if (e.Property == RangeBase.ValueProperty && !_updatingValue) Apply(); };
        foreach (var toggle in new[] { _animate, _track, _container, _containerShape, _rtl }) toggle.IsCheckedChanged += (_, _) => Apply();
        _timer.Tick += (_, _) =>
        {
            if (!CanAnimate()) { _timer.Stop(); return; }
            var now = System.Diagnostics.Stopwatch.GetTimestamp();
            var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(_lastFrame, now).TotalSeconds;
            _lastFrame = now;
            _updatingValue = true;
            if (_value.Value >= 100)
            {
                _completionHold += elapsed;
                if (_completionHold >= 1)
                {
                    _value.Value = 0;
                    _completionHold = 0;
                }
            }
            else
                _value.Value = Math.Min(100, _value.Value + elapsed * 25);
            _updatingValue = false;
            UpdateValue();
        };
        AttachedToVisualTree += (_, _) =>
        {
            _attached = true;
            MaterialMotion.ReducedMotionChanged += MotionChanged;
            foreach (var ancestor in this.GetVisualAncestors())
            {
                _visibilityAncestors.Add(ancestor);
                ancestor.PropertyChanged += AncestorChanged;
            }
            UpdateTimer();
        };
        DetachedFromVisualTree += (_, _) =>
        {
            _attached = false;
            _timer.Stop();
            MaterialMotion.ReducedMotionChanged -= MotionChanged;
            foreach (var ancestor in _visibilityAncestors) ancestor.PropertyChanged -= AncestorChanged;
            _visibilityAncestors.Clear();
        };
        PropertyChanged += (_, e) =>
        {
            if (e.Property == MaterialMotion.IsReducedProperty || e.Property == IsVisibleProperty) UpdateTimer();
        };
        Apply();
    }

    private MaterialButtonGroup Choice(string label, string[] options)
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = label });
        var group = new MaterialButtonGroup { Kind = MaterialButtonGroupKind.Connected, ButtonShape = MaterialButtonShape.Round, ControlSize = MaterialControlSize.Small, SelectionMode = MaterialButtonGroupSelectionMode.Single, SelectedIndex = 0, ShowSelectedIcon = true };
        foreach (var option in options) group.Items.Add(new MaterialButtonGroupItem { Content = option });
        panel.Children.Add(group);
        Settings.Children.Add(panel);
        return group;
    }

    private Slider Number(string label, double min, double max, double value, double step, string unit)
    {
        var panel = new StackPanel { Spacing = 8 };
        var caption = new TextBlock { Text = $"{label}: {value:0} {unit}" };
        var slider = new Slider { Minimum = min, Maximum = max, Value = value, TickFrequency = step, IsSnapToTickEnabled = true };
        MaterialAssist.SetControlSize(slider, MaterialControlSize.ExtraSmall);
        MaterialSlider.SetShowStops(slider, false);
        Avalonia.Automation.AutomationProperties.SetName(slider, label);
        slider.PropertyChanged += (_, e) => { if (e.Property == RangeBase.ValueProperty) caption.Text = $"{label}: {slider.Value:0} {unit}"; };
        panel.Children.Add(caption);
        panel.Children.Add(slider);
        Settings.Children.Add(panel);
        return slider;
    }

    private ToggleSwitch Toggle(string label, bool value)
    {
        var toggle = new ToggleSwitch { Content = label, IsChecked = value };
        Settings.Children.Add(toggle);
        return toggle;
    }

    private void Apply()
    {
        var loading = _kind.SelectedIndex == 2;
        var circular = _kind.SelectedIndex == 1;
        var wave = _shape.SelectedIndex == 1;
        ((Control)_shape.Parent!).IsVisible = !loading;
        ((Control)_value.Parent!).IsVisible = _behavior.SelectedIndex == 0;
        _value.IsEnabled = _animate.IsChecked != true || MaterialMotion.GetIsReduced(this);
        ((Control)_thickness.Parent!).IsVisible = !loading;
        ((Control)_length.Parent!).IsVisible = !loading && !circular;
        ((Control)_diameter.Parent!).IsVisible = circular || loading;
        ((Control)_wavelength.Parent!).IsVisible = wave && !loading && !circular && _behavior.SelectedIndex == 0;
        ((Control)_indeterminateWavelength.Parent!).IsVisible = wave && !loading && !circular && _behavior.SelectedIndex == 1;
        ((Control)_circularWavelength.Parent!).IsVisible = wave && circular;
        _track.IsVisible = circular;
        _container.IsVisible = loading;
        _containerShape.IsVisible = loading;
        ((Control)_sequence.Parent!).IsVisible = loading;
        // Replace the list only when the selection changes: a new instance restarts the multi-shape
        // sequence at its first pair, which is exactly the live replacement demonstrated here.
        var sequence = _sequence.SelectedIndex switch
        {
            1 => s_pairShapes,
            2 => s_tripleShapes,
            _ => (IReadOnlyList<RoundedPolygon>?)null
        };
        if (!ReferenceEquals(_loading.Polygons, sequence)) _loading.Polygons = sequence;
        _loading.ContainerShape = _containerShape.IsChecked == true ? s_starContainer : null;
        _loading.ContainerShapeStretch = Stretch.Fill;
        var type = wave ? ProgressIndicatorType.Wave : ProgressIndicatorType.Flat;
        MaterialProgress.SetIndicatorType(_linear, type);
        MaterialProgress.SetThickness(_linear, _thickness.Value);
        MaterialProgress.SetTrackThickness(_linear, _thickness.Value);
        // Source thick wavy sample: 8 dp stroke in a 14 dp band, i.e. 2 × ActiveWaveAmplitude (3)
        // beyond the stroke. The default 4 dp stroke therefore uses the 10 dp token height.
        MaterialProgress.SetWaveHeight(_linear, _thickness.Value + 6);
        MaterialProgress.SetWavelength(_linear, _wavelength.Value);
        MaterialProgress.SetIndeterminateWavelength(_linear, _indeterminateWavelength.Value);
        _linear.Width = _length.Value;
        _linear.IsIndeterminate = _circular.IsIndeterminate = _loading.IsIndeterminate = _behavior.SelectedIndex == 1;
        _circular.IndicatorType = type;
        _circular.StrokeThickness = _thickness.Value;
        _circular.TrackThickness = _thickness.Value;
        _circular.Width = _circular.Height = _diameter.Value;
        _circular.Wavelength = _circularWavelength.Value;
        _circular.ShowTrack = _track.IsChecked == true;
        _loading.Width = _loading.Height = _diameter.Value;
        _loading.ShowContainer = _container.IsChecked == true;
        _loading.IsAnimated = _animate.IsChecked == true;
        // A local pause must not override the inherited accessibility preference when running.
        foreach (var control in new Control[] { _linear, _circular, _loading })
        {
            if (_animate.IsChecked == true) control.ClearValue(MaterialMotion.IsReducedProperty);
            else MaterialMotion.SetIsReduced(control, true);
        }
        _host.FlowDirection = _rtl.IsChecked == true ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        _host.Child = loading ? _loading : circular ? _circular : _linear;
        UpdateValue();
        UpdateTimer();
    }

    private void UpdateValue()
    {
        _linear.Value = _circular.Value = _value.Value;
        _loading.Progress = _value.Value / 100;
        _readout.Text = _behavior.SelectedIndex == 1 ? "Indeterminate" : $"{_value.Value:0}%";
    }

    private bool CanAnimate() => _attached && IsEffectivelyVisible && !MaterialMotion.GetIsReduced(this) && _animate.IsChecked == true && _behavior.SelectedIndex == 0;

    private void UpdateTimer()
    {
        _value.IsEnabled = _animate.IsChecked != true || MaterialMotion.GetIsReduced(this);
        if (CanAnimate() && !_timer.IsEnabled)
        {
            _lastFrame = System.Diagnostics.Stopwatch.GetTimestamp();
            _timer.Start();
        }
        else if (!CanAnimate()) _timer.Stop();
    }

    private void MotionChanged(object? sender, EventArgs e) => UpdateTimer();

    private void AncestorChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == IsVisibleProperty) UpdateTimer();
    }
}
