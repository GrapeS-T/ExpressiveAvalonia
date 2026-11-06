using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Catalog;

/// <summary>One live button or toggle button with its anatomy and state exposed in the inspector.</summary>
public static class ButtonsGallery
{
    public static Control Create() => new ButtonPlayground();
}

internal sealed class ButtonPlayground : Grid, IComponentPlayground
{
    private const string StarIcon = "star";
    private const string StarFilledIcon = "star_filled";

    private static readonly MaterialButtonVariant[] Variants =
    {
        MaterialButtonVariant.Filled,
        MaterialButtonVariant.Tonal,
        MaterialButtonVariant.Outlined,
        MaterialButtonVariant.Elevated,
        MaterialButtonVariant.Text,
    };
    private static readonly MaterialButtonVariant[] ToggleVariants =
        [MaterialButtonVariant.Filled, MaterialButtonVariant.Tonal, MaterialButtonVariant.Outlined, MaterialButtonVariant.Elevated];
    private static readonly MaterialButtonVariant[] IconVariants =
        [MaterialButtonVariant.Filled, MaterialButtonVariant.Tonal, MaterialButtonVariant.Outlined, MaterialButtonVariant.Standard];

    private readonly Button _button = new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
    };
    private readonly ToggleButton _toggle = new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
    };
    private readonly Border _host = new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
    };
    private readonly TextBlock _readout = new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        FontSize = 16,
        TextWrapping = TextWrapping.Wrap,
    };
    private readonly MaterialButtonGroup _kind;
    private readonly MaterialButtonGroup _content;
    private readonly MaterialButtonGroup _shape;
    private readonly MaterialButtonGroup _iconWidth;
    private readonly ComboBox _variant;
    private readonly ComboBox _size;
    private readonly StackPanel _iconWidthSettings = new() { Spacing = 16 };
    private readonly StackPanel _labelSettings = new() { Spacing = 16 };
    private readonly StackPanel _leadingSettings = new() { Spacing = 16 };
    private readonly StackPanel _selectedSettings = new() { Spacing = 16 };
    private readonly MaterialIcon _buttonGlyph;
    private readonly MaterialIcon _buttonContentGlyph;
    private readonly MaterialIcon _toggleGlyph;
    private readonly MaterialIcon _toggleSelectedGlyph;
    private readonly TextBox _label;
    private readonly ToggleSwitch _leading;
    private readonly ToggleSwitch _selected;
    private readonly ToggleSwitch _enabled;
    private readonly ToggleSwitch _rtl;
    private int _clicks;
    private bool _syncing;
    public StackPanel Settings { get; } = new() { Spacing = 16 };

    internal ButtonPlayground()
    {
        _buttonGlyph = Glyph();
        _buttonContentGlyph = Glyph();
        _toggleGlyph = Glyph();
        _toggleSelectedGlyph = new MaterialIcon { IconName = StarFilledIcon, IsDecorative = true };
        var stage = new StackPanel
        {
            Spacing = 24,
            Margin = new Thickness(24),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        stage.Children.Add(_host);
        stage.Children.Add(_readout);
        Children.Add(stage);

        _kind = Choice(Settings, "Тип", new[] { "Button", "ToggleButton" }, 0);
        _content = Choice(Settings, "Содержимое", new[] { "Текст", "Иконка" }, 0);
        _variant = Field(Settings, "Вид", new ComboBox
        {
            ItemsSource = Variants,
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        });
        _size = Field(Settings, "Размер", new ComboBox
        {
            ItemsSource = new[] { "ExtraSmall", "Small", "Medium", "Large", "ExtraLarge" },
            SelectedIndex = 2,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        });
        _shape = Choice(Settings, "Форма", new[] { "Round", "Square" }, 0);
        _iconWidth = Choice(_iconWidthSettings, "Ширина контейнера", new[] { "Narrow", "Default", "Wide" }, 1);
        Settings.Children.Add(_iconWidthSettings);

        _label = new TextBox { Text = "Label", HorizontalAlignment = HorizontalAlignment.Stretch };
        MaterialTextField.SetVariant(_label, MaterialTextFieldVariant.Outlined);
        MaterialTextField.SetLabel(_label, "Текст");
        _labelSettings.Children.Add(_label);
        _leading = Toggle(_leadingSettings, "Ведущая иконка", true);
        Settings.Children.Add(_labelSettings);
        Settings.Children.Add(_leadingSettings);

        _selected = Toggle(_selectedSettings, "Выбрано (IsChecked)", false);
        Settings.Children.Add(_selectedSettings);
        _enabled = Toggle(Settings, "Доступна", true);
        _rtl = Toggle(Settings, "Справа налево (RTL)", false);
        Settings.Children.Add(new TextBlock
        {
            Text = "MD3 Expressive: пять размеров и формы Round/Square. " +
                   "Icon-only: Filled/Tonal/Outlined/Standard; текстовый ToggleButton не использует Text. " +
                   "Narrow/Default/Wide меняет контейнер, но не глиф. Клик, Space и Enter — " +
                   "настоящие состояния; выбранность синхронизирована с IsChecked.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
        });

        AutomationProperties.SetName(_variant, "Вид кнопки");
        AutomationProperties.SetName(_size, "Размер кнопки");
        AutomationProperties.SetName(_label, "Текст кнопки");

        _kind.SelectionChanged += (_, _) => { if (!_syncing) Apply(); };
        _content.SelectionChanged += (_, _) => { if (!_syncing) Apply(); };
        _shape.SelectionChanged += (_, _) => { if (!_syncing) Apply(); };
        _iconWidth.SelectionChanged += (_, _) => { if (!_syncing) Apply(); };
        _variant.SelectionChanged += (_, _) => { if (!_syncing) Apply(); };
        _size.SelectionChanged += (_, _) => { if (!_syncing) Apply(); };
        _label.TextChanged += (_, _) => { if (!_syncing) Apply(); };
        _leading.IsCheckedChanged += (_, _) => { if (!_syncing) Apply(); };
        _enabled.IsCheckedChanged += (_, _) => { if (!_syncing) Apply(); };
        _rtl.IsCheckedChanged += (_, _) => { if (!_syncing) Apply(); };

        _button.Click += (_, _) => Count();
        _toggle.Click += (_, _) => Count();
        _toggle.IsCheckedChanged += (_, _) =>
        {
            if (_syncing) return;
            _syncing = true;
            _selected.IsChecked = _toggle.IsChecked == true;
            _syncing = false;
            UpdateReadout();
        };
        _selected.IsCheckedChanged += (_, _) =>
        {
            if (_syncing) return;
            _syncing = true;
            _toggle.IsChecked = _selected.IsChecked == true;
            _syncing = false;
            UpdateReadout();
        };

        Apply();
    }

    private void Count()
    {
        _clicks++;
        UpdateReadout();
    }

    private void Apply()
    {
        var iconMode = _content.SelectedIndex == 1;
        var toggleMode = _kind.SelectedIndex == 1;
        var variants = iconMode ? IconVariants : toggleMode ? ToggleVariants : Variants;
        var variant = _variant.SelectedItem is MaterialButtonVariant selectedVariant
            && Array.IndexOf(variants, selectedVariant) >= 0 ? selectedVariant : MaterialButtonVariant.Filled;
        _syncing = true;
        _variant.ItemsSource = variants;
        _variant.SelectedItem = variant;
        _syncing = false;
        var size = (MaterialControlSize)Math.Clamp(_size.SelectedIndex, 0, 4);
        var shape = (MaterialButtonShape)Math.Clamp(_shape.SelectedIndex, 0, 1);
        var width = (MaterialIconButtonWidth)Math.Clamp(_iconWidth.SelectedIndex, 0, 2);
        var text = _label.Text ?? "";
        var leading = !iconMode && _leading.IsChecked == true;
        var enabled = _enabled.IsChecked == true;

        Configure(_button, iconMode, variant, size, shape, width, text, leading, enabled);
        Configure(_toggle, iconMode, variant, size, shape, width, text, leading, enabled);

        var current = toggleMode ? (Control)_toggle : _button;
        if (!ReferenceEquals(_host.Child, current))
            _host.Child = current;
        _host.FlowDirection = _rtl.IsChecked == true ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

        _iconWidthSettings.IsVisible = iconMode;
        _labelSettings.IsVisible = !iconMode;
        _leadingSettings.IsVisible = !iconMode;
        _selectedSettings.IsVisible = toggleMode;

        UpdateReadout();
    }

    private void Configure(
        Button button,
        bool iconMode,
        MaterialButtonVariant variant,
        MaterialControlSize size,
        MaterialButtonShape shape,
        MaterialIconButtonWidth width,
        string text,
        bool leading,
        bool enabled)
    {
        MaterialAssist.SetButtonVariant(button, variant);
        MaterialAssist.SetControlSize(button, size);
        MaterialAssist.SetButtonShape(button, shape);
        MaterialAssist.SetIsIconButton(button, iconMode);
        MaterialAssist.SetIconButtonWidth(button, width);
        button.IsEnabled = enabled;

        var toggle = button as ToggleButton;
        var glyph = toggle is null ? _buttonGlyph : _toggleGlyph;

        if (iconMode)
        {
            // An icon button's Content is the glyph itself; a toggle instead uses the
            // Icon/SelectedIcon pair so checking swaps the outline star for the filled one.
            button.Content = toggle is null ? _buttonContentGlyph : null;
            MaterialAssist.SetIcon(button, toggle is null ? null : glyph);
            MaterialAssist.SetSelectedIcon(button, toggle is null ? null : _toggleSelectedGlyph);
            MaterialAssist.SetShowIcon(button, toggle is not null);
        }
        else
        {
            button.Content = text;
            MaterialAssist.SetIcon(button, leading ? glyph : null);
            MaterialAssist.SetSelectedIcon(button, leading && toggle is not null ? _toggleSelectedGlyph : null);
            MaterialAssist.SetShowIcon(button, leading);
        }

        AutomationProperties.SetName(button, iconMode
            ? "Icon button"
            : string.IsNullOrWhiteSpace(text) ? "Button" : text);
    }

    private static MaterialIcon Glyph() => new() { IconName = StarIcon, IsDecorative = true };

    private void UpdateReadout()
    {
        var toggle = _toggle.IsChecked == true ? "выбрано" : "не выбрано";
        _readout.Text = _kind.SelectedIndex == 1
            ? $"Нажатий: {_clicks} · ToggleButton: {toggle}"
            : $"Нажатий: {_clicks}";
    }

    private static T Field<T>(StackPanel host, string label, T control) where T : Control
    {
        host.Children.Add(new TextBlock { Text = label });
        host.Children.Add(control);
        return control;
    }

    private static MaterialButtonGroup Choice(StackPanel host, string label, string[] options, int selected)
    {
        var group = Field(host, label, new MaterialButtonGroup
        {
            Kind = MaterialButtonGroupKind.Connected,
            ButtonShape = MaterialButtonShape.Round,
            ControlSize = MaterialControlSize.Small,
            SelectionMode = MaterialButtonGroupSelectionMode.Single,
            ShowSelectedIcon = true,
        });
        foreach (var option in options) group.Items.Add(new MaterialButtonGroupItem { Content = option });
        // The group clears an out-of-range selection whenever items change, so the initial
        // index is applied only once every item exists.
        group.SelectedIndex = selected;
        return group;
    }

    private static ToggleSwitch Toggle(StackPanel host, string label, bool value)
    {
        var toggle = new ToggleSwitch { Content = label, IsChecked = value };
        host.Children.Add(toggle);
        return toggle;
    }
}
