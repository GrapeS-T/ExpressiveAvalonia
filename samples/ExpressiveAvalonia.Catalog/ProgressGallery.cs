using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Theming;
using Avalonia.VisualTree;

namespace ExpressiveAvalonia.Catalog;

/// <summary>Live progress indicators with edition-specific options and animation controls.</summary>
public static class ProgressGallery
{
    public static Control Create() => new ProgressPlayground(true);
    public static Control CreateClassic() => new ProgressPlayground(false);
}

internal sealed class ProgressPlayground : Grid
{
    private readonly bool _expressive;
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
    private readonly Slider _lobes;
    private readonly Slider _step;
    private readonly ToggleSwitch _animate;
    private readonly ToggleSwitch _track;
    private readonly ToggleSwitch _container;
    private readonly ToggleSwitch _rtl;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1d / 30) };
    private bool _attached;
    private bool _updatingValue;
    private long _lastFrame;
    private double _completionHold;
    private readonly List<Visual> _visibilityAncestors = new();
    internal StackPanel Settings { get; } = new() { Spacing = 16 };

    internal ProgressPlayground(bool expressive)
    {
        _expressive = expressive;
        var stage = new StackPanel { Spacing = 32, Margin = new Thickness(24), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        stage.Children.Add(_host);
        stage.Children.Add(_readout);
        Children.Add(stage);
        _kind = Choice("Индикатор", expressive ? new[] { "Linear", "Circular", "Loading" } : new[] { "Linear", "Circular" });
        _behavior = Choice("Поведение", new[] { "Determinate", "Indeterminate" });
        _shape = Choice("Форма", new[] { "Flat", "Wave" });
        _animate = Toggle("Анимация", true);
        _value = Number("Прогресс", 0, 100, 50, 1, "%");
        _thickness = Number("Толщина", 4, 8, 4, 1, "dp");
        _length = Number("Длина", 120, 720, 320, 4, "dp");
        _diameter = Number("Диаметр", 48, 144, 48, 4, "dp");
        _wavelength = Number("Длина волны", 20, 80, 40, 1, "dp");
        _lobes = Number("Количество волн", 3, 12, 9, 1, "");
        _step = Number("Начальная форма Loading", 1, 7, 1, 1, "");
        _track = Toggle("Дорожка Circular", true);
        _container = Toggle("Контейнер Loading", false);
        _rtl = Toggle("Справа налево (RTL)", false);
        Settings.Children.Add(new TextBlock
        {
            Text = "MD3: Flat, 4 dp. MD3E: Flat/Wave, толщина 4–8 dp и Loading. Анимация determinate — демонстрационное заполнение 0–100%; выключите её для ручного значения. Indeterminate и Loading используют собственные циклы. Reduced motion останавливает движение; скрытая или закрытая страница не обновляется.",
            TextWrapping = TextWrapping.Wrap, FontSize = 13
        });
        foreach (var group in new[] { _kind, _behavior, _shape }) group.SelectionChanged += (_, _) => Apply();
        foreach (var slider in new[] { _value, _thickness, _length, _diameter, _wavelength, _lobes, _step })
            slider.PropertyChanged += (_, e) => { if (e.Property == RangeBase.ValueProperty && !_updatingValue) Apply(); };
        foreach (var toggle in new[] { _animate, _track, _container, _rtl }) toggle.IsCheckedChanged += (_, _) => Apply();
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
        var wave = _expressive && _shape.SelectedIndex == 1;
        ((Control)_behavior.Parent!).IsVisible = !loading;
        ((Control)_shape.Parent!).IsVisible = _expressive && !loading;
        ((Control)_value.Parent!).IsVisible = !loading && _behavior.SelectedIndex == 0;
        _value.IsEnabled = _animate.IsChecked != true || MaterialMotion.GetIsReduced(this);
        ((Control)_thickness.Parent!).IsVisible = _expressive && !loading;
        ((Control)_length.Parent!).IsVisible = !loading && !circular;
        ((Control)_diameter.Parent!).IsVisible = circular || loading;
        ((Control)_wavelength.Parent!).IsVisible = wave && !loading && !circular;
        ((Control)_lobes.Parent!).IsVisible = wave && circular;
        ((Control)_step.Parent!).IsVisible = loading;
        _track.IsVisible = circular;
        _container.IsVisible = loading;
        var type = wave ? ProgressIndicatorType.Wave : ProgressIndicatorType.Flat;
        MaterialProgress.SetIndicatorType(_linear, type);
        MaterialProgress.SetThickness(_linear, _expressive ? _thickness.Value : 4);
        MaterialProgress.SetWavelength(_linear, _wavelength.Value);
        _linear.Width = _length.Value;
        _linear.IsIndeterminate = _circular.IsIndeterminate = _behavior.SelectedIndex == 1;
        _circular.IndicatorType = type;
        _circular.StrokeThickness = _expressive ? _thickness.Value : 4;
        _circular.Width = _circular.Height = _diameter.Value;
        _circular.WaveLobes = (int)_lobes.Value;
        _circular.ShowTrack = _track.IsChecked == true;
        _loading.Width = _loading.Height = _diameter.Value;
        _loading.ShowContainer = _container.IsChecked == true;
        _loading.Step = (int)_step.Value;
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
        _readout.Text = _kind.SelectedIndex == 2 ? "Loading" : _behavior.SelectedIndex == 1 ? "Indeterminate" : $"{_value.Value:0}%";
    }

    private bool CanAnimate() => _attached && IsEffectivelyVisible && !MaterialMotion.GetIsReduced(this) && _animate.IsChecked == true && _kind.SelectedIndex != 2 && _behavior.SelectedIndex == 0;

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
