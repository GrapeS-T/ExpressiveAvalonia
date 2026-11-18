using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Catalog;

/// <summary>A single editable text field with its anatomy exposed in the inspector.</summary>
public static class TextFieldsGallery
{
    public static Control Create() => new TextFieldPlayground();
}

internal sealed class TextFieldPlayground : Grid, IComponentPlayground
{
    private readonly TextBox _field = new() { Width = 320, Text = "Input text", VerticalAlignment = VerticalAlignment.Center };
    private readonly MaterialButtonGroup _variant;
    private readonly MaterialButtonGroup _labelPosition;
    private readonly MaterialButtonGroup _labelAlignment;
    private readonly ToggleSwitch _alwaysMinimized;
    private readonly TextBox _label;
    private readonly TextBox _placeholder;
    private readonly TextBox _supporting;
    private readonly TextBox _prefix;
    private readonly TextBox _suffix;
    private readonly Slider _width;
    private readonly ToggleSwitch _leading;
    private readonly ToggleSwitch _trailing;
    private readonly ToggleSwitch _clear;
    private readonly ToggleSwitch _password;
    private readonly ToggleSwitch _multiline;
    private readonly ToggleSwitch _error;
    private readonly ToggleSwitch _enabled;
    private readonly ToggleSwitch _readOnly;
    private readonly ToggleSwitch _rtl;

    public StackPanel Settings { get; } = InspectorLayout.CreatePane();

    internal TextFieldPlayground()
    {
        var host = new Border
        {
            Child = _field,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(24),
        };
        Children.Add(host);

        var configSection = InspectorLayout.Section(Settings, "Конфигурация", divider: false);
        _variant = InspectorLayout.Choice(configSection, "Вид", ["Filled", "Outlined"], 1);
        _labelPosition = InspectorLayout.Choice(configSection, "Положение подписи", ["Авто", "Внутри", "Вырез", "Сверху"], 0);
        _labelAlignment = InspectorLayout.Choice(configSection, "Выравнивание подписи", ["Старт", "Центр", "Конец"], 0);
        var configSwitches = InspectorLayout.Switches(configSection);
        _alwaysMinimized = InspectorLayout.Switch(configSwitches, "Подпись всегда минимизирована", false);
        _width = InspectorLayout.Slider(configSection, "Ширина", 240, 560, 320, 1, "dp", "0");
        AutomationProperties.SetName(_width, "Ширина поля, dp");
        _width.PropertyChanged += (_, e) =>
        {
            if (e.Property == RangeBase.ValueProperty)
                _field.Width = _width.Value;
        };

        var textSection = InspectorLayout.Section(Settings, "Текст и аффиксы");
        _label = InspectorLayout.TextField(textSection, "Подпись", "Label");
        _placeholder = InspectorLayout.TextField(textSection, "Placeholder", "Введите текст");
        _supporting = InspectorLayout.TextField(textSection, "Поддерживающий текст", "Supporting text");
        _prefix = InspectorLayout.TextField(textSection, "Префикс", "");
        _suffix = InspectorLayout.TextField(textSection, "Суффикс", "");

        var modeSection = InspectorLayout.Section(Settings, "Иконки и режим");
        var modeSwitches = InspectorLayout.Switches(modeSection);
        _leading = InspectorLayout.Switch(modeSwitches, "Иконка поиска слева", false);
        _trailing = InspectorLayout.Switch(modeSwitches, "Иконка справа", false);
        _clear = InspectorLayout.Switch(modeSwitches, "Кнопка очистки", false);
        _password = InspectorLayout.Switch(modeSwitches, "Пароль и кнопка показа", false);
        _multiline = InspectorLayout.Switch(modeSwitches, "Многострочный ввод", false);

        var stateSection = InspectorLayout.Section(Settings, "Состояния");
        var stateSwitches = InspectorLayout.Switches(stateSection);
        _error = InspectorLayout.Switch(stateSwitches, "Ошибка валидации", false);
        _enabled = InspectorLayout.Switch(stateSwitches, "Поле доступно", true);
        _readOnly = InspectorLayout.Switch(stateSwitches, "Только чтение", false);
        _rtl = InspectorLayout.Switch(stateSwitches, "Справа налево (RTL)", false);
        InspectorLayout.ActionButton(stateSection, "Восстановить текст", () => _field.Text = "Input text");

        InspectorLayout.Note(
            Settings,
            "Редактируйте текст прямо в поле. Наведение и фокус — настоящие состояния; " +
            "Tab переводит фокус в поле. MD3 и MD3E используют общий шаблон Filled / Outlined.");

        _variant.SelectionChanged += (_, _) => Apply();
        _labelPosition.SelectionChanged += (_, _) => Apply();
        _labelAlignment.SelectionChanged += (_, _) => Apply();
        foreach (var editor in new[] { _label, _placeholder, _supporting, _prefix, _suffix })
            editor.TextChanged += (_, _) => Apply();
        foreach (var toggle in new[] { _leading, _trailing, _clear, _password, _multiline, _error, _enabled, _readOnly, _rtl, _alwaysMinimized })
            toggle.IsCheckedChanged += (_, _) => Apply();
        Apply();
    }

    private static PathIcon Icon(string geometry) => new() { Data = Geometry.Parse(geometry), Width = 24, Height = 24 };

    private void Apply()
    {
        MaterialTextField.SetVariant(_field, (MaterialTextFieldVariant)_variant.SelectedIndex);
        _field.Width = _width.Value;
        MaterialTextField.SetLabel(_field, _label.Text);
        var position = (MaterialTextFieldLabelPosition)_labelPosition.SelectedIndex;
        MaterialTextField.SetLabelPosition(_field, position);
        // Above is always minimized in the source; the other positions expose the flag.
        _alwaysMinimized.IsEnabled = position != MaterialTextFieldLabelPosition.Above;
        MaterialTextField.SetIsLabelAlwaysMinimized(_field,
            position != MaterialTextFieldLabelPosition.Above && _alwaysMinimized.IsChecked == true);
        // Source label alignment: minimized/expanded for Inside/Cutout, a single value for Above.
        var alignment = (MaterialTextFieldLabelAlignment)_labelAlignment.SelectedIndex;
        MaterialTextField.SetExpandedLabelAlignment(_field, alignment);
        MaterialTextField.SetMinimizedLabelAlignment(_field, alignment);
        MaterialTextField.SetAboveLabelAlignment(_field, alignment);
        if (string.IsNullOrEmpty(_label.Text))
            _field.ClearValue(AutomationProperties.NameProperty);
        else
            AutomationProperties.SetName(_field, _label.Text);
        _field.PlaceholderText = _placeholder.Text;
        MaterialTextField.SetSupportingText(_field, _supporting.Text);
        MaterialTextField.SetPrefix(_field, _prefix.Text);
        MaterialTextField.SetSuffix(_field, _suffix.Text);
        if (_leading.IsChecked == true)
        {
            if (MaterialTextField.GetLeadingContent(_field) is null)
                MaterialTextField.SetLeadingContent(_field, Icon("M10,2 A8,8 0 1 1 4.34,15.66 A8,8 0 0 1 10,2 Z M10,4 A6,6 0 1 0 10,16 A6,6 0 0 0 10,4 Z M15,14 L22,21 L20,23 L13,16 Z"));
        }
        else MaterialTextField.SetLeadingContent(_field, null);
        if (_trailing.IsChecked == true)
        {
            if (MaterialTextField.GetTrailingContent(_field) is null)
                MaterialTextField.SetTrailingContent(_field, Icon("M12,3 L14.8,8.7 L21,9.6 L16.5,14 L17.6,20.2 L12,17.3 L6.4,20.2 L7.5,14 L3,9.6 L9.2,8.7 Z"));
        }
        else MaterialTextField.SetTrailingContent(_field, null);
        var password = _password.IsChecked == true;
        _multiline.IsEnabled = !password;
        _field.PasswordChar = password ? '\u2022' : '\0';
        if (!password) _field.RevealPassword = false;
        MaterialTextField.SetShowPasswordReveal(_field, password);
        MaterialTextField.SetShowClearButton(_field, _clear.IsChecked == true);
        _field.AcceptsReturn = !password && _multiline.IsChecked == true;
        _field.TextWrapping = _field.AcceptsReturn ? TextWrapping.Wrap : TextWrapping.NoWrap;
        _field.MinHeight = _field.AcceptsReturn ? 120 : 56;
        _field.IsEnabled = _enabled.IsChecked == true;
        _field.IsReadOnly = _readOnly.IsChecked == true;
        InspectorLayout.ApplyFlowDirection(_field, _rtl);
        DataValidationErrors.SetErrors(_field, _error.IsChecked == true ? new object[] { "Введите корректное значение" } : null);
    }
}
