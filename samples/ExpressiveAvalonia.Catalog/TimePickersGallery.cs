using System;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Catalog;

/// <summary>One live time picker with its mode, format, orientation, step and validation exposed in the inspector.</summary>
public static class TimePickersGallery
{
    public static Control Create() => new TimePickerPlayground();
}

internal sealed class TimePickerPlayground : Grid, IComponentPlayground
{
    private static readonly string[] MinuteSteps = { "1", "5", "10", "15", "30" };
    private static readonly string[] FieldShapes = { "8", "16", "24", "28" };
    private static readonly string[] PeriodShapes = { "8", "16", "999" };
    private static readonly string[] Cultures = { "Current", "en-US", "ar-EG", "fa-IR", "ru-RU" };

    private readonly MaterialTimePicker _picker = new()
    {
        Time = new TimeSpan(9, 45, 0),
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
    };
    private readonly TextBlock _status = new()
    {
        Text = "Время ещё не подтверждено.",
        HorizontalAlignment = HorizontalAlignment.Center,
        TextWrapping = TextWrapping.Wrap,
    };
    private readonly ComboBox _mode = new() { ItemsSource = Enum.GetNames<MaterialTimeInputMode>(), SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ComboBox _variant = new() { ItemsSource = Enum.GetNames<MaterialTimePickerVariant>(), SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ComboBox _fieldShape = new() { ItemsSource = FieldShapes, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ComboBox _periodShape = new() { ItemsSource = PeriodShapes, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ComboBox _format = new() { ItemsSource = Enum.GetNames<MaterialTimeFormat>(), SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ComboBox _orientation = new() { ItemsSource = Enum.GetNames<Orientation>(), SelectedIndex = 1, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ComboBox _increment = new() { ItemsSource = MinuteSteps, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ComboBox _culture = new() { ItemsSource = Cultures, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly Slider _hour;
    private readonly Slider _minute;
    private readonly Slider _width;
    private readonly ToggleSwitch _actions;
    private readonly ToggleSwitch _limit;
    private readonly ToggleSwitch _autoAdvance;
    private readonly TextBox _headline;
    private readonly Button _dialog;
    private bool _syncing;
    public StackPanel Settings { get; } = new() { Spacing = 16 };

    internal TimePickerPlayground()
    {
        RowDefinitions = new RowDefinitions("*,Auto");
        var stage = new StackPanel
        {
            Spacing = 20,
            Margin = new Thickness(24),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        stage.Children.Add(_picker);
        _dialog = new Button { Content = "Открыть модальный диалог", HorizontalAlignment = HorizontalAlignment.Center };
        MaterialAssist.SetButtonVariant(_dialog, MaterialButtonVariant.Tonal);
        MaterialAssist.SetControlSize(_dialog, MaterialControlSize.Small);
        _dialog.Click += async (_, _) =>
        {
            try
            {
                if (TopLevel.GetTopLevel(_dialog) is not Window owner) return;
                var picker = new MaterialTimePicker
                {
                    Time = _picker.Time,
                    Format = _picker.Format,
                    Culture = _picker.Culture,
                    InputMode = _picker.InputMode,
                    Variant = _picker.Variant,
                    TimeFieldShape = _picker.TimeFieldShape,
                    PeriodSelectorShape = _picker.PeriodSelectorShape,
                    Orientation = _picker.Orientation,
                    MinuteIncrement = _picker.MinuteIncrement,
                    AutoAdvance = _picker.AutoAdvance,
                    Headline = _picker.Headline,
                    MinTime = _picker.MinTime,
                    MaxTime = _picker.MaxTime,
                };
                var result = await MaterialTimePickerDialog.ShowAsync(owner, picker);
                _status.Text = result.IsConfirmed
                    ? $"Диалог подтверждён: {Format(result.Time)}"
                    : $"Диалог отменён, значение {Format(result.Time)}";
            }
            catch (Exception exception)
            {
                _status.Text = $"Диалог недоступен: {exception.Message}";
            }
        };
        stage.Children.Add(_dialog);
        Children.Add(stage);
        Grid.SetRow(_status, 1);
        _status.Margin = new Thickness(24, 0, 24, 20);
        Children.Add(_status);

        Settings.Children.Add(Caption("Режим ввода"));
        Settings.Children.Add(_mode);
        Settings.Children.Add(Caption("Вариант"));
        Settings.Children.Add(_variant);
        Settings.Children.Add(Caption("Форма поля времени"));
        Settings.Children.Add(_fieldShape);
        Settings.Children.Add(Caption("Форма AM/PM"));
        Settings.Children.Add(_periodShape);
        Settings.Children.Add(Caption("Формат"));
        Settings.Children.Add(_format);
        Settings.Children.Add(Caption("Ориентация"));
        Settings.Children.Add(_orientation);
        Settings.Children.Add(Caption("Шаг минут"));
        Settings.Children.Add(_increment);
        Settings.Children.Add(Caption("Культура цифр"));
        Settings.Children.Add(_culture);
        _hour = Number(Settings, "Часы", 0, 23, 9, 1, "ч");
        _minute = Number(Settings, "Минуты", 0, 59, 45, 1, "мин");
        _width = Number(Settings, "Ширина", 280, 720, 360, 8, "dp");
        _actions = Toggle(Settings, "Показывать действия Cancel/OK", true);
        _limit = Toggle(Settings, "Ограничить 09:00–17:00", false);
        _autoAdvance = Toggle(Settings, "Переход к минутам после часа", true);
        Settings.Children.Add(Caption("Заголовок"));
        _headline = new TextBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        MaterialTextField.SetVariant(_headline, MaterialTextFieldVariant.Outlined);
        MaterialTextField.SetLabel(_headline, "Заголовок");
        Settings.Children.Add(_headline);

        Settings.Children.Add(new TextBlock
        {
            Text = "Циферблат принимает перетаскивание указателя, ячейки — ввод, стрелки и Enter. " +
                   "Режим Scroll показывает два колеса TimeScroll; варианты Vibrant и Uncontained " +
                   "используют форму полей и AM/PM из инспектора. Культура цифр меняет ячейки, " +
                   "циферблат и колеса без потери введённого черновика. Подтверждение и отмена " +
                   "сообщаются в строке состояния; при ограничении времени выход за диапазон блокирует OK.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
        });

        AutomationProperties.SetName(_mode, "Режим ввода времени");
        AutomationProperties.SetName(_variant, "Вариант времени");
        AutomationProperties.SetName(_fieldShape, "Форма поля времени");
        AutomationProperties.SetName(_periodShape, "Форма AM/PM");
        AutomationProperties.SetName(_format, "Формат времени");
        AutomationProperties.SetName(_orientation, "Ориентация времени");
        AutomationProperties.SetName(_increment, "Шаг минут");
        AutomationProperties.SetName(_culture, "Культура цифр времени");
        AutomationProperties.SetName(_headline, "Заголовок времени");

        _mode.SelectionChanged += (_, _) => { if (!_syncing) ApplyOptions(); };
        _variant.SelectionChanged += (_, _) => { if (!_syncing) ApplyOptions(); };
        _fieldShape.SelectionChanged += (_, _) => { if (!_syncing) ApplyOptions(); };
        _periodShape.SelectionChanged += (_, _) => { if (!_syncing) ApplyOptions(); };
        _format.SelectionChanged += (_, _) => { if (!_syncing) ApplyOptions(); };
        _orientation.SelectionChanged += (_, _) => { if (!_syncing) ApplyOptions(); };
        _increment.SelectionChanged += (_, _) => { if (!_syncing) ApplyOptions(); };
        _culture.SelectionChanged += (_, _) => { if (!_syncing) ApplyOptions(); };
        _width.PropertyChanged += (_, e) => { if (!_syncing && e.Property == RangeBase.ValueProperty) ApplyOptions(); };
        _actions.IsCheckedChanged += (_, _) => { if (!_syncing) ApplyOptions(); };
        _limit.IsCheckedChanged += (_, _) => { if (!_syncing) ApplyOptions(); };
        _autoAdvance.IsCheckedChanged += (_, _) => { if (!_syncing) ApplyOptions(); };
        _headline.TextChanged += (_, _) => { if (!_syncing) ApplyOptions(); };
        _hour.PropertyChanged += (_, e) => { if (!_syncing && e.Property == RangeBase.ValueProperty) ApplyTime(); };
        _minute.PropertyChanged += (_, e) => { if (!_syncing && e.Property == RangeBase.ValueProperty) ApplyTime(); };
        _picker.PropertyChanged += (_, e) => { if (!_syncing && e.Property == MaterialTimePicker.TimeProperty) SyncTime(); };
        _picker.Confirmed += (_, e) => _status.Text = $"Подтверждено: {Format(e.Time)}";
        _picker.Cancelled += (_, _) => _status.Text = $"Отменено, значение {Format(_picker.Time)}";

        ApplyOptions();
    }

    private void ApplyOptions()
    {
        _syncing = true;
        _picker.Variant = (MaterialTimePickerVariant)Math.Clamp(_variant.SelectedIndex, 0,
            Enum.GetValues<MaterialTimePickerVariant>().Length - 1);
        _picker.InputMode = (MaterialTimeInputMode)Math.Clamp(_mode.SelectedIndex, 0,
            Enum.GetValues<MaterialTimeInputMode>().Length - 1);
        _picker.TimeFieldShape = new CornerRadius(double.Parse(
            FieldShapes[Math.Clamp(_fieldShape.SelectedIndex, 0, FieldShapes.Length - 1)]));
        _picker.PeriodSelectorShape = new CornerRadius(double.Parse(
            PeriodShapes[Math.Clamp(_periodShape.SelectedIndex, 0, PeriodShapes.Length - 1)]));
        _picker.Format = (MaterialTimeFormat)Math.Clamp(_format.SelectedIndex, 0, 1);
        _picker.Orientation = (Orientation)Math.Clamp(_orientation.SelectedIndex, 0, 1);
        _picker.MinuteIncrement = int.Parse(MinuteSteps[Math.Clamp(_increment.SelectedIndex, 0, MinuteSteps.Length - 1)]);
        var cultureIndex = Math.Clamp(_culture.SelectedIndex, 0, Cultures.Length - 1);
        _picker.Culture = cultureIndex == 0 ? null : CultureInfo.GetCultureInfo(Cultures[cultureIndex]);
        _picker.Width = _width.Value;
        _picker.ShowActions = _actions.IsChecked == true;
        _picker.AutoAdvance = _autoAdvance.IsChecked == true;
        _picker.MinTime = _limit.IsChecked == true ? TimeSpan.FromHours(9) : null;
        _picker.MaxTime = _limit.IsChecked == true ? TimeSpan.FromHours(17) : null;
        _picker.Headline = string.IsNullOrWhiteSpace(_headline.Text) ? null : _headline.Text;
        _syncing = false;
        SyncTime();
    }

    private void ApplyTime()
    {
        _syncing = true;
        var hour = (int)Math.Round(_hour.Value);
        var minute = (int)Math.Round(_minute.Value);
        _picker.Time = new TimeSpan(hour, minute, 0);
        _syncing = false;
    }

    private void SyncTime()
    {
        _syncing = true;
        _hour.Value = _picker.Time.Hours;
        _minute.Value = _picker.Time.Minutes;
        _syncing = false;
    }

    private static string Format(TimeSpan time) => $"{(int)time.TotalHours:00}:{time.Minutes:00}";

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
