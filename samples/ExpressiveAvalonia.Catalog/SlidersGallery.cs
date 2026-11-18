using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Catalog;

/// <summary>A single live slider with its variants exposed in the inspector.</summary>
public static class SlidersGallery
{
    public static Control Create() => new SliderPlayground();
}

internal sealed class SliderPlayground : Grid, IComponentPlayground
{
    private readonly Border _host = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly Slider _single = new() { Minimum = 0, Maximum = 100, Value = 50 };
    private readonly MaterialRangeSlider _range = new() { Minimum = 0, Maximum = 100, LowerValue = 20, UpperValue = 80 };
    private readonly InspectorMenuChoice _kind;
    private readonly InspectorMenuChoice _size;
    private readonly InspectorMenuChoice _orientation;
    private readonly NumericUpDown _value;
    private readonly NumericUpDown _lower;
    private readonly NumericUpDown _upper;
    private readonly NumericUpDown _length;
    private readonly NumericUpDown _points;
    private readonly TextBlock _readout = new() { HorizontalAlignment = HorizontalAlignment.Center, FontSize = 16 };
    private readonly ToggleSwitch _stops;
    private readonly ToggleSwitch _icon;
    private readonly ToggleSwitch _indicator;
    private readonly ToggleSwitch _snap;
    private readonly ToggleSwitch _enabled;
    private readonly ToggleSwitch _rtl;
    private bool _syncing;

    public StackPanel Settings { get; } = InspectorLayout.CreatePane();

    internal SliderPlayground()
    {
        RowDefinitions = new RowDefinitions("*,Auto");
        Children.Add(_host);
        Grid.SetRow(_readout, 1);
        _readout.Margin = new Thickness(0, 24, 0, 24);
        Children.Add(_readout);

        var configSection = InspectorLayout.Section(Settings, "Конфигурация", divider: false);
        _kind = InspectorLayout.Field(configSection, "Вид", new InspectorMenuChoice("Вид", ["Обычный", "От центра", "Диапазон"], 0));
        _size = InspectorLayout.Field(configSection, "Размер", new InspectorMenuChoice("Размер", Enum.GetNames<MaterialControlSize>(), 2));
        _orientation = InspectorLayout.Field(configSection, "Ориентация", new InspectorMenuChoice("Ориентация", ["Горизонтальная", "Вертикальная"], 0));

        var valuesSection = InspectorLayout.Section(Settings, "Значения и шкала");
        _length = Number(valuesSection, "Длина, dp", 120, 1440, 320, 20);
        _value = Number(valuesSection, "Значение", 0, 100, 50, 1);
        _lower = Number(valuesSection, "Начало диапазона", 0, 100, 20, 1);
        _upper = Number(valuesSection, "Конец диапазона", 0, 100, 80, 1);
        _points = Number(valuesSection, "Количество точек (включая края)", 2, 51, 11, 1, "0");

        var optionsSection = InspectorLayout.Section(Settings, "Опции и состояния");
        var switches = InspectorLayout.Switches(optionsSection);
        _snap = InspectorLayout.Switch(switches, "Дискретные значения", false);
        _stops = InspectorLayout.Switch(switches, "Показывать дискретные метки", true);
        _icon = InspectorLayout.Switch(switches, "Иконка громкости", false);
        _indicator = InspectorLayout.Switch(switches, "Подсказка значения", true);
        _enabled = InspectorLayout.Switch(switches, "Слайдер доступен", true);
        _rtl = InspectorLayout.Switch(switches, "Справа налево (RTL)", false);

        InspectorLayout.Note(
            Settings,
            "Количество точек включает оба конца шкалы; шаг привязки рассчитывается автоматически. " +
            "Иконка внутри трека — только для обычного слайдера M, L и XL. Диапазон — только горизонтальный.");

        _kind.SelectionChanged += (_, _) => Apply();
        _size.SelectionChanged += (_, _) => Apply();
        _orientation.SelectionChanged += (_, _) => Apply();
        foreach (var number in new[] { _value, _lower, _upper, _length, _points })
            number.ValueChanged += (_, _) => { if (!_syncing) Apply(); };
        foreach (var toggle in new[] { _stops, _icon, _indicator, _snap, _enabled, _rtl })
            toggle.IsCheckedChanged += (_, _) => Apply();
        _single.PropertyChanged += (_, e) => { if (e.Property == RangeBase.ValueProperty) SyncValues(); };
        _range.PropertyChanged += (_, e) =>
        {
            if (e.Property == MaterialRangeSlider.LowerValueProperty || e.Property == MaterialRangeSlider.UpperValueProperty) SyncValues();
        };
        // The range readout only claims a finished interaction when the range reports one.
        _range.ValueChangeCompleted += (_, _) => SyncValues(completed: true);
        Apply();
    }

    private static NumericUpDown Number(
        StackPanel host,
        string label,
        decimal min,
        decimal max,
        decimal value,
        decimal increment,
        string format = "0.##")
    {
        var input = new NumericUpDown
        {
            Minimum = min,
            Maximum = max,
            Value = value,
            Increment = increment,
            FormatString = format,
            AllowSpin = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        // NumericUpDown has no implicit theme in this catalog; give its native parser a visible editor.
        input.Template = new FuncControlTemplate<NumericUpDown>((owner, scope) =>
        {
            var editor = new TextBox
            {
                Name = "PART_TextBox",
                MinHeight = 56,
                Margin = new Thickness(0, 8, 0, 0),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Center,
            };
            MaterialTextField.SetVariant(editor, MaterialTextFieldVariant.Outlined);
            MaterialTextField.SetLabel(editor, label);
            scope.Register("PART_TextBox", editor);
            editor.Bind(TextBox.TextProperty, new Binding(nameof(NumericUpDown.Text))
            {
                Source = owner,
                Mode = BindingMode.TwoWay,
            });
            AutomationProperties.SetName(editor, label);
            return editor;
        });
        AutomationProperties.SetName(input, label);
        host.Children.Add(input);
        return input;
    }

    private void Apply()
    {
        if (_syncing) return;
        _syncing = true;
        var range = _kind.SelectedIndex == 2;
        var centered = _kind.SelectedIndex == 1;
        _orientation.IsEnabled = !range;
        var discrete = _snap.IsChecked == true;
        _stops.IsEnabled = discrete;
        _points.IsVisible = discrete;
        _points.Value = decimal.Truncate(_points.Value ?? 11);
        var frequency = (centered ? 200d : 100d) / ((double)(_points.Value ?? 11) - 1);
        var allowIcon = !range && !centered && _size.SelectedIndex >= 2;
        _icon.IsEnabled = allowIcon;
        _value.IsVisible = !range;
        _lower.IsVisible = range;
        _upper.IsVisible = range;
        _value.Minimum = centered ? -100 : 0;
        var vertical = !range && _orientation.SelectedIndex == 1;
        var control = range ? (Control)_range : _single;
        _host.Child = control;
        control.Width = vertical ? double.NaN : (double)(_length.Value ?? 320);
        control.Height = vertical ? (double)(_length.Value ?? 320) : double.NaN;
        control.IsEnabled = _enabled.IsChecked == true;
        InspectorLayout.ApplyFlowDirection(_single, _rtl);
        InspectorLayout.ApplyFlowDirection(_range, _rtl);
        MaterialAssist.SetControlSize(control, (MaterialControlSize)_size.SelectedIndex);
        MaterialSlider.SetShowStops(control, discrete && _stops.IsChecked == true);
        MaterialSlider.SetShowIcon(control, allowIcon && _icon.IsChecked == true);
        MaterialSlider.SetShowValueIndicator(control, _indicator.IsChecked == true);
        MaterialSlider.SetValueFormat(control, "0.##");
        if (range)
        {
            _range.TickFrequency = discrete ? frequency : 0;
            _range.IsSnapToTickEnabled = discrete;
            _range.SmallChange = discrete ? _range.TickFrequency : 1;
            _range.LargeChange = Math.Max(10, _range.SmallChange * 2);
            // Expand first, then clamp the lower endpoint to the requested upper endpoint.
            _range.UpperValue = Math.Max(_range.LowerValue, (double)(_upper.Value ?? 80));
            _range.LowerValue = Math.Min((double)(_lower.Value ?? 20), (double)(_upper.Value ?? 80));
            _range.UpperValue = (double)(_upper.Value ?? 80);
        }
        else
        {
            _single.Minimum = centered ? -100 : 0;
            MaterialSlider.SetIsCentered(_single, centered);
            _single.Orientation = vertical ? Orientation.Vertical : Orientation.Horizontal;
            _single.IsDirectionReversed = false;
            _single.TickFrequency = discrete ? frequency : 0;
            _single.IsSnapToTickEnabled = discrete;
            _single.SmallChange = discrete ? _single.TickFrequency : 1;
            _single.LargeChange = Math.Max(10, _single.SmallChange * 2);
            _single.Value = (double)(_value.Value ?? 50);
        }
        _syncing = false;
        SyncValues();
    }

    private void SyncValues(bool completed = false)
    {
        if (_syncing) return;
        _syncing = true;
        _value.Value = (decimal)_single.Value;
        _lower.Value = (decimal)_range.LowerValue;
        _upper.Value = (decimal)_range.UpperValue;
        _readout.Text = _kind.SelectedIndex == 2
            ? $"Диапазон: {_range.LowerValue:0.##} — {_range.UpperValue:0.##}" +
              (completed ? " · изменение завершено" : "")
            : $"Значение: {_single.Value:0.##}";
        _syncing = false;
    }

    private sealed class InspectorMenuChoice : MaterialSplitButton
    {
        protected override Type StyleKeyOverride => typeof(MaterialSplitButton);
        internal int SelectedIndex { get; private set; }
        internal event EventHandler? SelectionChanged;

        internal InspectorMenuChoice(string label, string[] options, int selected)
        {
            SelectedIndex = selected;
            HorizontalAlignment = HorizontalAlignment.Stretch;
            ControlSize = MaterialControlSize.Small;
            ButtonShape = MaterialButtonShape.Round;
            Variant = MaterialButtonVariant.Filled;
            var icon = Geometry.Parse("M12,2 A10,10 0 1 1 11.99,2 Z M12,5 L10.2,9 L6,9.5 L9,12.5 L8.3,17 L12,14.8 L15.7,17 L15,12.5 L18,9.5 L13.8,9 Z");
            var arrow = Geometry.Parse("M9,6 L15,12 L9,18 Z");
            Icon = new PathIcon { Data = icon, Width = 20, Height = 20 };
            Content = options[selected];
            Click += (_, _) => IsMenuOpen = true;
            var flyout = new MenuFlyout
            {
                Placement = PlacementMode.BottomEdgeAlignedLeft,
                FlyoutPresenterTheme = (ControlTheme)Application.Current!.FindResource("MaterialMenuFlyoutPresenterExpressive")!,
            };
            var entries = new MenuItem[options.Length];
            for (var i = 0; i < options.Length; i++)
            {
                var index = i;
                var item = new MenuItem
                {
                    Header = options[i],
                    MinWidth = 240,
                    Icon = new PathIcon { Data = icon, Width = 24, Height = 24 },
                };
                MaterialMenu.SetSurfaceStyle(item, MaterialMenuSurfaceStyle.Expressive);
                MaterialMenu.SetTrailingIcon(item, new PathIcon { Data = arrow, Width = 18, Height = 18 });
                MaterialMenu.SetIsActive(item, i == selected);
                item.Click += (_, _) =>
                {
                    SelectedIndex = index;
                    Content = options[index];
                    AutomationProperties.SetName(this, $"{label}: {options[index]}");
                    for (var n = 0; n < entries.Length; n++) MaterialMenu.SetIsActive(entries[n], n == index);
                    SelectionChanged?.Invoke(this, EventArgs.Empty);
                };
                entries[i] = item;
                flyout.Items.Add(item);
            }
            Flyout = flyout;
            AutomationProperties.SetName(this, $"{label}: {options[selected]}");
        }
    }
}
