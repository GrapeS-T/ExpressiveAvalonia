using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Catalog;

/// <summary>A live checkbox, radio group or switch with its anatomy exposed in the inspector.</summary>
public static class SelectionGallery
{
    public static Control Create() => new SelectionPlayground();
}

internal sealed class SelectionPlayground : Grid, IComponentPlayground
{
    private static int _instances;
    private readonly Border _host = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _readout = new() { HorizontalAlignment = HorizontalAlignment.Center, FontSize = 16, TextWrapping = TextWrapping.Wrap };
    private readonly CheckBox _check = new() { IsChecked = true };
    private readonly RadioButton[] _radios;
    private readonly StackPanel _radioGroup = new() { Spacing = 4 };
    private readonly ToggleSwitch _switch = new() { IsChecked = true };
    private readonly string[] _radioLabels = { "Доставка курьером", "Самовывоз из магазина", "Пункт выдачи" };
    private readonly MaterialButtonGroup _kind;
    private readonly MaterialButtonGroup _checkState;
    private readonly MaterialButtonGroup _switchState;
    private readonly ComboBox _radioChoice;
    private readonly StackPanel _checkSettings;
    private readonly StackPanel _radioSettings;
    private readonly StackPanel _switchSettings;
    private readonly TextBox _label;
    private readonly ToggleSwitch _threeState;
    private readonly ToggleSwitch _error;
    private readonly ToggleSwitch _icons;
    private readonly ToggleSwitch _contentIcons;
    private readonly ToggleSwitch _enabled;
    private readonly ToggleSwitch _rtl;
    private string _checkLabel = "Согласен с условиями";
    private string _switchLabel = "Тёмная тема";
    private int _type;
    private bool _syncing;
    public StackPanel Settings { get; } = new() { Spacing = 16 };

    internal SelectionPlayground()
    {
        var groupName = $"selection-demo-{Interlocked.Increment(ref _instances)}";
        _radios = new RadioButton[_radioLabels.Length];
        for (var i = 0; i < _radios.Length; i++)
        {
            var index = i;
            var radio = new RadioButton { Content = _radioLabels[i], GroupName = groupName, IsChecked = i == 0 };
            AutomationProperties.SetName(radio, _radioLabels[i]);
            radio.IsCheckedChanged += (_, _) => RadioChanged(index);
            _radios[i] = radio;
            _radioGroup.Children.Add(radio);
        }
        _check.Content = _checkLabel;
        _switch.Content = _switchLabel;
        AutomationProperties.SetName(_check, _checkLabel);
        AutomationProperties.SetName(_switch, _switchLabel);

        var stage = new StackPanel
        {
            Spacing = 24,
            Margin = new Thickness(24),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        stage.Children.Add(_host);
        stage.Children.Add(_readout);
        Children.Add(stage);

        _kind = Choice(Settings, "Тип", new[] { "Checkbox", "Radio", "Switch" }, 0);
        _label = new TextBox { Text = _checkLabel, HorizontalAlignment = HorizontalAlignment.Stretch };
        MaterialTextField.SetVariant(_label, MaterialTextFieldVariant.Outlined);
        MaterialTextField.SetLabel(_label, "Подпись");
        AutomationProperties.SetName(_label, "Подпись");
        Settings.Children.Add(_label);

        _checkSettings = new StackPanel { Spacing = 16 };
        _checkState = Choice(_checkSettings, "Состояние", new[] { "Off", "On", "Indeterminate" }, 1);
        _threeState = Toggle(_checkSettings, "Трёхпозиционный флажок", false);
        _error = Toggle(_checkSettings, "Ошибка валидации", false);
        Settings.Children.Add(_checkSettings);

        _radioSettings = new StackPanel { Spacing = 16 };
        _radioSettings.Children.Add(new TextBlock { Text = "Выбранный вариант" });
        _radioChoice = new ComboBox
        {
            ItemsSource = _radioLabels.ToArray(),
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        AutomationProperties.SetName(_radioChoice, "Выбранный вариант");
        _radioSettings.Children.Add(_radioChoice);
        Settings.Children.Add(_radioSettings);

        _switchSettings = new StackPanel { Spacing = 16 };
        _switchState = Choice(_switchSettings, "Состояние", new[] { "Off", "On" }, 1);
        _icons = Toggle(_switchSettings, "Встроенные иконки (ShowSwitchIcons)", true);
        _contentIcons = Toggle(_switchSettings, "Иконки контента (OnContent/OffContent)", false);
        Settings.Children.Add(_switchSettings);

        _enabled = Toggle(Settings, "Доступен", true);
        _rtl = Toggle(Settings, "Справа налево (RTL)", false);
        Settings.Children.Add(new TextBlock
        {
            Text = "MD3 Expressive использует шаблон Selection: контейнер флажка 20 dp, " +
                   "глиф радиокнопки 20 dp, дорожка переключателя 52 × 32 dp. Клик, Space, Enter и стрелки — настоящие состояния; " +
                   "трёхпозиционный флажок циклически проходит Off → On → Indeterminate. Подпись меняется справа, " +
                   "а состояние примера всегда отражается в инспекторе. Иконки контента — это нативные OnContent/OffContent; " +
                   "они так же расширяют ручку до 24 dp.",
            TextWrapping = TextWrapping.Wrap, FontSize = 13
        });

        _kind.SelectionChanged += (_, _) => { if (!_syncing) SelectType(_kind.SelectedIndex); };
        _label.TextChanged += (_, _) => { if (!_syncing) ApplyLabel(); };
        _checkState.SelectionChanged += (_, _) =>
        {
            if (_syncing) return;
            _syncing = true;
            _check.IsChecked = _checkState.SelectedIndex switch { 0 => false, 1 => true, _ => (bool?)null };
            _syncing = false;
            UpdateReadout();
        };
        _check.IsCheckedChanged += (_, _) =>
        {
            if (_syncing) return;
            _syncing = true;
            _checkState.SelectedIndex = _check.IsChecked switch { null => 2, true => 1, false => 0 };
            _syncing = false;
            UpdateReadout();
        };
        _switchState.SelectionChanged += (_, _) =>
        {
            if (_syncing) return;
            _syncing = true;
            _switch.IsChecked = _switchState.SelectedIndex == 1;
            _syncing = false;
            UpdateReadout();
        };
        _switch.IsCheckedChanged += (_, _) =>
        {
            if (_syncing) return;
            _syncing = true;
            _switchState.SelectedIndex = _switch.IsChecked == true ? 1 : 0;
            _syncing = false;
            UpdateReadout();
        };
        _radioChoice.SelectionChanged += (_, _) =>
        {
            if (_syncing || _radioChoice.SelectedIndex < 0) return;
            var index = _radioChoice.SelectedIndex;
            _syncing = true;
            _radios[index].IsChecked = true;
            _label.Text = _radioLabels[index];
            _syncing = false;
            UpdateReadout();
        };
        _threeState.IsCheckedChanged += (_, _) => _check.IsThreeState = _threeState.IsChecked == true;
        _error.IsCheckedChanged += (_, _) => DataValidationErrors.SetErrors(_check,
            _error.IsChecked == true ? new object[] { "Обязательное поле" } : null);
        _icons.IsCheckedChanged += (_, _) => MaterialSelection.SetShowSwitchIcons(_switch, _icons.IsChecked == true);
        _contentIcons.IsCheckedChanged += (_, _) => ApplySwitchContent();
        _enabled.IsCheckedChanged += (_, _) => _host.IsEnabled = _enabled.IsChecked == true;
        _rtl.IsCheckedChanged += (_, _) =>
            _host.FlowDirection = _rtl.IsChecked == true ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

        MaterialSelection.SetShowSwitchIcons(_switch, _icons.IsChecked == true);
        SelectType(0);
    }

    private void SelectType(int index)
    {
        _type = Math.Clamp(index, 0, 2);
        _host.Child = _type switch { 1 => _radioGroup, 2 => _switch, _ => _check };
        _checkSettings.IsVisible = _type == 0;
        _radioSettings.IsVisible = _type == 1;
        _switchSettings.IsVisible = _type == 2;
        _syncing = true;
        _label.Text = CurrentLabel();
        _syncing = false;
        UpdateReadout();
    }

    private string CurrentLabel() => _type switch
    {
        1 => _radioLabels[Math.Max(0, _radioChoice.SelectedIndex)],
        2 => _switchLabel,
        _ => _checkLabel
    };

    private void ApplyLabel()
    {
        var text = _label.Text ?? "";
        switch (_type)
        {
            case 1:
                var index = Math.Max(0, _radioChoice.SelectedIndex);
                _radioLabels[index] = text;
                _radios[index].Content = text;
                AutomationProperties.SetName(_radios[index], RadioLabel(index));
                // The choice list mirrors the edited labels; restore the same selection afterwards.
                var previous = _syncing;
                _syncing = true;
                _radioChoice.ItemsSource = _radioLabels.ToArray();
                _radioChoice.SelectedIndex = index;
                _syncing = previous;
                break;
            case 2:
                _switchLabel = text;
                _switch.Content = text;
                AutomationProperties.SetName(_switch, string.IsNullOrWhiteSpace(text) ? "Switch" : text);
                break;
            default:
                _checkLabel = text;
                _check.Content = text;
                AutomationProperties.SetName(_check, string.IsNullOrWhiteSpace(text) ? "Checkbox" : text);
                break;
        }
        UpdateReadout();
    }

    private void ApplySwitchContent()
    {
        var show = _contentIcons.IsChecked == true;
        _switch.OnContent = show ? SwitchIcon(
            "M3.8 8.01667L0 4.21667L0.95 3.26667L3.8 6.11667L9.91667 0L10.8667 0.95L3.8 8.01667Z") : null;
        _switch.OffContent = show ? SwitchIcon(
            "M0.933333 9.33333L0 8.4L3.73333 4.66667L0 0.933333L0.933333 0L4.66667 3.73333L8.4 0L9.33333 0.933333L5.6 4.66667L9.33333 8.4L8.4 9.33333L4.66667 5.6L0.933333 9.33333Z") : null;
    }

    private static PathIcon SwitchIcon(string data) =>
        new() { Data = Geometry.Parse(data), Width = 16, Height = 16 };

    private void RadioChanged(int index)
    {
        if (_syncing || _radios[index].IsChecked != true) return;
        _syncing = true;
        _radioChoice.SelectedIndex = index;
        _label.Text = _radioLabels[index];
        _syncing = false;
        UpdateReadout();
    }

    private void UpdateReadout()
    {
        _readout.Text = _type switch
        {
            1 => $"Выбрано: {RadioLabel(Math.Max(0, _radioChoice.SelectedIndex))}",
            2 => _switch.IsChecked == true ? "Состояние: включено" : "Состояние: выключено",
            _ => _check.IsChecked switch
            {
                null => "Состояние: неопределённое",
                true => "Состояние: включено",
                false => "Состояние: выключено"
            }
        };
    }

    private string RadioLabel(int index) =>
        string.IsNullOrWhiteSpace(_radioLabels[index]) ? $"Вариант {index + 1}" : _radioLabels[index];

    private static MaterialButtonGroup Choice(StackPanel host, string label, string[] options, int selected)
    {
        host.Children.Add(new TextBlock { Text = label });
        var group = new MaterialButtonGroup
        {
            Kind = MaterialButtonGroupKind.Connected,
            ButtonShape = MaterialButtonShape.Round,
            ControlSize = MaterialControlSize.Small,
            SelectionMode = MaterialButtonGroupSelectionMode.Single,
            ShowSelectedIcon = true
        };
        foreach (var option in options) group.Items.Add(new MaterialButtonGroupItem { Content = option });
        // Items first: adding items clamps a selection that points past the current item count.
        group.SelectedIndex = selected;
        host.Children.Add(group);
        return group;
    }

    private static ToggleSwitch Toggle(StackPanel host, string label, bool value)
    {
        var toggle = new ToggleSwitch { Content = label, IsChecked = value };
        host.Children.Add(toggle);
        return toggle;
    }
}
