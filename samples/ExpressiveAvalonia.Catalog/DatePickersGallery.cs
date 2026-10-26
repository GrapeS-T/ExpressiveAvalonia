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

public static class DatePickersGallery
{
    public static Control Create() => new DatePickersPlayground();
}

/// <summary>
/// One live date picker demo. The presentation selector swaps the docked field, the docked
/// calendar, the modal surface and the full-screen range surface; selection, range endpoints,
/// culture, bounds, typed input and the calendar itself stay real.
/// </summary>
internal sealed class DatePickersPlayground : Grid, IComponentPlayground
{
    private static readonly DateTime Today = DateTime.Today;

    private static readonly (string Name, CultureInfo Culture)[] Cultures =
    [
        ("English (US)", new CultureInfo("en-US")),
        ("Русский", new CultureInfo("ru-RU")),
        ("Deutsch", new CultureInfo("de-DE")),
        ("日本語", new CultureInfo("ja-JP")),
        ("العربية", new CultureInfo("ar-SA")),
    ];

    private readonly MaterialDatePicker _picker;
    private readonly MaterialDatePickerField _field;
    private readonly Border _card;
    private readonly ContentControl _stage = new() { HorizontalAlignment = HorizontalAlignment.Center };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, MaxWidth = 460 };
    private readonly MaterialButtonGroup _presentation;
    private readonly MaterialButtonGroup _selection;
    private readonly ToggleSwitch _input;
    private readonly ToggleSwitch _bounds;
    private readonly ToggleSwitch _actions;
    private readonly ToggleSwitch _clear;
    private readonly ToggleSwitch _adjacent;
    private readonly ComboBox _culture;
    private readonly Slider _months;
    private readonly TextBox _label;
    private readonly TextBlock _cultureSample = new() { TextWrapping = TextWrapping.Wrap };
    private int _lastMode = -1;
    private bool? _lastRange;
    private bool _sync;

    public StackPanel Settings { get; } = new() { Spacing = 16 };

    internal DatePickersPlayground()
    {
        _picker = new MaterialDatePicker
        {
            Mode = MaterialDatePickerMode.Modal,
            SelectionMode = MaterialDateSelectionMode.Single,
            Label = "Дата",
            SelectedDate = MaterialDatePicker.ToDateTimeOffset(Today.AddDays(9)),
            SelectedEndDate = MaterialDatePicker.ToDateTimeOffset(Today.AddDays(16)),
            DisplayDate = MaterialDatePicker.ToDateTimeOffset(Today.AddDays(9)),
            MonthCount = 6,
            AllowInputMode = true,
            Width = 360,
        };
        _field = new MaterialDatePickerField
        {
            Label = "Дата",
            SupportingText = "Введите дату или откройте календарь",
            SelectedDate = _picker.SelectedDate,
            Width = 360,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        _card = new Border
        {
            Padding = new Thickness(24),
            CornerRadius = new CornerRadius(28),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _card.Bind(Border.BackgroundProperty, _card.GetResourceObservable("MaterialSurfaceContainerLowBrush"));
        _card.Bind(Border.BorderBrushProperty, _card.GetResourceObservable("MaterialOutlineVariantBrush"));
        MaterialTypography.SetStyle(_status, MaterialTypeStyle.BodyMedium);
        _status.Foreground = Brushes.Gray;

        var inner = new StackPanel { Spacing = 12 };
        inner.Children.Add(_stage);
        inner.Children.Add(_status);
        _card.Child = inner;

        var stage = new StackPanel
        {
            Spacing = 16,
            Margin = new Thickness(24),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        stage.Children.Add(_card);
        Children.Add(stage);

        _presentation = Choice("Представление", ["Поле", "Dock", "Modal", "Full"]);
        _presentation.SelectedIndex = 2;
        _selection = Choice("Выбор", ["Один день", "Диапазон"]);
        _input = Toggle("Текстовый ввод", false);
        _bounds = Toggle("Ограничить 14 днями", false);
        _actions = Toggle("Кнопки OK / Cancel", true);
        _clear = Toggle("Кнопка очистки", true);
        _adjacent = Toggle("Соседние дни", true);

        Settings.Children.Add(new TextBlock { Text = "Культура" });
        _culture = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var (name, _) in Cultures)
            _culture.Items.Add(name);
        _culture.SelectedIndex = 1;
        AutomationProperties.SetName(_culture, "Культура");
        Settings.Children.Add(_culture);
        _cultureSample.FontSize = 12;
        _cultureSample.Foreground = Brushes.Gray;
        Settings.Children.Add(_cultureSample);

        _months = Number("Месяцев на экране", 1, 6, 6, 1);

        _label = new TextBox { Text = "Дата", HorizontalAlignment = HorizontalAlignment.Stretch };
        MaterialTextField.SetLabel(_label, "Заголовок");
        MaterialTextField.SetVariant(_label, MaterialTextFieldVariant.Outlined);
        AutomationProperties.SetName(_label, "Заголовок");
        Settings.Children.Add(_label);

        var actions = new WrapPanel { ItemSpacing = 8, LineSpacing = 8 };
        actions.Children.Add(ActionButton("Сегодня", () => _picker.GoToToday()));
        actions.Children.Add(ActionButton("Очистить", ClearSelection));
        actions.Children.Add(ActionButton("Диалог…", OpenDialog));
        Settings.Children.Add(actions);

        _picker.SelectionChanged += (_, _) =>
        {
            if (_sync)
                return;
            if (_field.SelectedDate != _picker.SelectedDate)
            {
                _sync = true;
                try
                {
                    _field.SelectedDate = _picker.SelectedDate;
                }
                finally
                {
                    _sync = false;
                }
            }
            ShowStatus();
        };
        _picker.Confirmed += (_, _) => ShowStatus("Подтверждено");
        _picker.Cancelled += (_, _) => _status.Text = "Отменено без сохранения";
        _picker.Cleared += (_, _) => _status.Text = "Выбор очищен";
        _field.SelectionCommitted += (_, _) =>
        {
            _sync = true;
            try
            {
                _picker.SelectedDate = _field.SelectedDate;
            }
            finally
            {
                _sync = false;
            }
            _status.Text = "Поле: " + DescribeField();
        };
        _field.Opened += (_, _) => _status.Text = "Календарь поля открыт";
        _field.Closed += (_, _) => _status.Text = "Поле: " + DescribeField();

        _presentation.SelectionChanged += (_, _) => { if (!_sync) Apply(); };
        _selection.SelectionChanged += (_, _) => { if (!_sync) Apply(); };
        foreach (var toggle in new[] { _input, _bounds, _actions, _clear, _adjacent })
            toggle.IsCheckedChanged += (_, _) => { if (!_sync) Apply(); };
        _months.PropertyChanged += (_, e) => { if (e.Property == RangeBase.ValueProperty && !_sync) Apply(); };
        _label.TextChanged += (_, _) => { if (!_sync) Apply(); };
        _culture.SelectionChanged += (_, _) => { if (!_sync) Apply(); };

        Apply();
    }

    private void Apply()
    {
        var mode = _presentation.SelectedIndex;
        var culture = Cultures[Math.Clamp(_culture.SelectedIndex, 0, Cultures.Length - 1)].Culture;
        var bounds = _bounds.IsChecked == true;
        var label = string.IsNullOrWhiteSpace(_label.Text) ? "Дата" : _label.Text;

        _sync = true;
        try
        {
            if (mode != _lastMode)
            {
                if (_lastMode == 0)
                {
                    if (_field.SelectedDate != _picker.SelectedDate)
                        _picker.SelectedDate = _field.SelectedDate;
                }
                else if (_field.SelectedDate != _picker.SelectedDate)
                {
                    _field.SelectedDate = _picker.SelectedDate;
                }
                _lastMode = mode;
            }

            var range = _selection.SelectedIndex == 1;
            if (range && _lastRange != true && _picker.SelectedEndDate is null && _picker.SelectedDate is { } start)
                _picker.SelectedEndDate = start.AddDays(7);
            _lastRange = range;

            _picker.SelectionMode = range ? MaterialDateSelectionMode.Range : MaterialDateSelectionMode.Single;
            _picker.Mode = mode switch
            {
                1 => MaterialDatePickerMode.Docked,
                3 => MaterialDatePickerMode.FullScreen,
                _ => MaterialDatePickerMode.Modal,
            };
            _picker.MonthCount = (int)Math.Round(_months.Value);
            _picker.ShowActions = _actions.IsChecked == true;
            _picker.ShowClearButton = _clear.IsChecked == true;
            _picker.ShowAdjacentDays = _adjacent.IsChecked == true;
            // The full-screen surface is a scrolling month range; the typed input presentation
            // belongs to the docked and modal surfaces.
            _picker.IsInputMode = _input.IsChecked == true && mode != 3;
            _picker.AllowInputMode = true;
            _picker.Label = label;
            _picker.Culture = culture;
            _picker.MinimumDate = bounds ? MaterialDatePicker.ToDateTimeOffset(Today) : null;
            _picker.MaximumDate = bounds ? MaterialDatePicker.ToDateTimeOffset(Today.AddDays(14)) : null;
            _picker.Width = mode == 3 ? 420 : 360;
            _picker.Height = mode == 3 ? 560 : double.NaN;

            _field.Label = label;
            _field.Culture = culture;
            _field.IsEditable = _input.IsChecked == true;
            _field.MinimumDate = _picker.MinimumDate;
            _field.MaximumDate = _picker.MaximumDate;
            _field.SelectedDate = _picker.SelectedDate;

            _stage.Content = mode == 0 ? _field : _picker;
            _cultureSample.Text = "Неделя начинается с " + culture.DateTimeFormat.FirstDayOfWeek +
                                  "; короткий формат " + culture.DateTimeFormat.ShortDatePattern;
        }
        finally
        {
            _sync = false;
        }

        ShowStatus();
    }

    private void ShowStatus(string? prefix = null)
    {
        var text = ReferenceEquals(_stage.Content, _field) ? DescribeField() : DescribePicker();
        _status.Text = prefix is null ? text : $"{prefix}: {text}";
    }

    private string DescribePicker()
    {
        if (_picker.SelectedDate is not { } start)
            return "Дата не выбрана";
        if (_picker.SelectionMode == MaterialDateSelectionMode.Range && _picker.SelectedEndDate is { } end)
            return $"Диапазон {Format(start)} — {Format(end)}";
        return "Выбрано: " + Format(start);
    }

    private string DescribeField() =>
        _field.SelectedDate is { } value ? "Выбрано: " + Format(value) : "Дата не выбрана";

    private string Format(DateTimeOffset value) => value.Date.ToString("d", CurrentCulture);

    private CultureInfo CurrentCulture =>
        Cultures[Math.Clamp(_culture.SelectedIndex, 0, Cultures.Length - 1)].Culture;

    private async void OpenDialog()
    {
        try
        {
            if (TopLevel.GetTopLevel(this) is not Window owner)
            {
                _status.Text = "Для диалога нужно окно-владелец";
                return;
            }

            var picker = new MaterialDatePicker
            {
                Mode = MaterialDatePickerMode.Modal,
                SelectionMode = _picker.SelectionMode,
                Label = _picker.Label,
                SelectedDate = _picker.SelectedDate,
                SelectedEndDate = _picker.SelectedEndDate,
                MinimumDate = _picker.MinimumDate,
                MaximumDate = _picker.MaximumDate,
                Culture = _picker.Culture,
                AllowInputMode = true,
            };
            var result = await MaterialDatePickerDialog.ShowAsync(owner, picker);
            if (!result.IsConfirmed)
            {
                _status.Text = "Диалог отменён";
                return;
            }
            var start = result.Start is { } value ? Format(value) : "нет даты";
            var end = result.End is { } last ? " — " + Format(last) : string.Empty;
            _status.Text = "Диалог: " + start + end;
        }
        catch (Exception exception)
        {
            _status.Text = "Диалог недоступен: " + exception.Message;
        }
    }

    private void ClearSelection()
    {
        _picker.ClearSelection();
        _field.SelectedDate = null;
        _status.Text = "Выбор очищен";
    }

    private Button ActionButton(string content, Action action)
    {
        var button = new Button { Content = content };
        button.Click += (_, _) => action();
        return button;
    }

    private MaterialButtonGroup Choice(string label, string[] options)
    {
        Settings.Children.Add(new TextBlock { Text = label });
        var group = new MaterialButtonGroup
        {
            Kind = MaterialButtonGroupKind.Connected,
            ButtonShape = MaterialButtonShape.Round,
            ControlSize = MaterialControlSize.Small,
            SelectionMode = MaterialButtonGroupSelectionMode.Single,
            ShowSelectedIcon = true,
        };
        foreach (var option in options)
            group.Items.Add(new MaterialButtonGroupItem { Content = option });
        group.SelectedIndex = 0;
        Settings.Children.Add(group);
        return group;
    }

    private ToggleSwitch Toggle(string label, bool value)
    {
        var toggle = new ToggleSwitch { Content = label, IsChecked = value };
        Settings.Children.Add(toggle);
        return toggle;
    }

    private Slider Number(string label, double min, double max, double value, double step)
    {
        var caption = new TextBlock { Text = $"{label}: {value:0}" };
        Settings.Children.Add(caption);
        var slider = new Slider
        {
            Minimum = min,
            Maximum = max,
            Value = value,
            TickFrequency = step,
            IsSnapToTickEnabled = true,
        };
        AutomationProperties.SetName(slider, label);
        slider.PropertyChanged += (_, e) =>
        {
            if (e.Property == RangeBase.ValueProperty)
                caption.Text = $"{label}: {slider.Value:0}";
        };
        Settings.Children.Add(slider);
        return slider;
    }
}
