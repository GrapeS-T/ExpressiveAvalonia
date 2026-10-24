using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Catalog;

internal static class StandardControlsGallery
{
    public static Control Create(MaterialTheme theme)
    {
        var pages = new TabControl();
        pages.Items.Add(new TabItem { Header = "Buttons", Content = Buttons() });
        pages.Items.Add(new TabItem { Header = "Input", Content = Input() });
        pages.Items.Add(new TabItem { Header = "Selection", Content = Selection() });
        pages.Items.Add(new TabItem { Header = "Ranges", Content = Ranges() });
        pages.Items.Add(new TabItem { Header = "Collections", Content = Collections() });
        pages.Items.Add(new TabItem { Header = "Typography", Content = Typography(theme) });
        return pages;
    }

    private static StackPanel Panel() => new() { Spacing = 16, Margin = new Thickness(24) };
    private static StackPanel Row() => new() { Orientation = Orientation.Horizontal, Spacing = 12 };
    private static Control Scroll(Control content) => new ScrollViewer { Content = content, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };

    private static Control Buttons()
    {
        var panel = Panel();
        foreach (var variant in Enum.GetValues<MaterialButtonVariant>())
        {
            panel.Children.Add(new TextBlock { Text = variant.ToString(), FontSize = 20 });
            var row = new WrapPanel { ItemSpacing = 16, LineSpacing = 16 };
            foreach (var size in Enum.GetValues<MaterialControlSize>())
            {
                var button = new Button { Content = "Label", VerticalAlignment = VerticalAlignment.Center };
                MaterialAssist.SetButtonVariant(button, variant);
                MaterialAssist.SetControlSize(button, size);
                row.Children.Add(button);
            }
            panel.Children.Add(row);
        }
        var toggles = Row();
        foreach (var variant in Enum.GetValues<MaterialButtonVariant>())
        {
            var button = new ToggleButton { Content = new PathIcon { Data = Avalonia.Media.Geometry.Parse("M12,3 14.8,8.7 21,9.6 16.5,14 17.6,20.2 12,17.3 6.4,20.2 7.5,14 3,9.6 9.2,8.7 Z"), Width = 24, Height = 24 }, IsChecked = true };
            MaterialAssist.SetButtonVariant(button, variant);
            MaterialAssist.SetIsIconButton(button, true);
            Avalonia.Automation.AutomationProperties.SetName(button, $"Favorite {variant}");
            toggles.Children.Add(button);
        }
        panel.Children.Add(toggles);
        return Scroll(panel);
    }

    private static Control Input()
    {
        var panel = Panel();
        foreach (var variant in Enum.GetValues<MaterialTextFieldVariant>())
        {
            var text = new TextBox { Width = 440, HorizontalAlignment = HorizontalAlignment.Left };
            MaterialTextField.SetVariant(text, variant);
            MaterialTextField.SetLabel(text, $"{variant}: имя");
            MaterialTextField.SetSupportingText(text, "Введите текст; проверьте выделение и переключение темы");
            MaterialTextField.SetPrefix(text, "@");
            panel.Children.Add(text);
            var populated = new TextBox { Text = "user@example.com", Width = 440, HorizontalAlignment = HorizontalAlignment.Left };
            MaterialTextField.SetVariant(populated, variant);
            MaterialTextField.SetLabel(populated, "Email");
            MaterialTextField.SetSupportingText(populated, "Поддерживающий текст");
            panel.Children.Add(populated);
            var invalid = new TextBox { Text = "Неверный адрес", Width = 440, HorizontalAlignment = HorizontalAlignment.Left };
            MaterialTextField.SetVariant(invalid, variant);
            MaterialTextField.SetLabel(invalid, "Email");
            DataValidationErrors.SetErrors(invalid, new object[] { "Введите корректный email" });
            panel.Children.Add(invalid);
            var disabled = new TextBox { Text = "Недоступное поле", IsEnabled = false, Width = 440, HorizontalAlignment = HorizontalAlignment.Left };
            MaterialTextField.SetVariant(disabled, variant);
            MaterialTextField.SetLabel(disabled, "Email");
            panel.Children.Add(disabled);
        }
        var password = new TextBox { PasswordChar = '●', Width = 440, HorizontalAlignment = HorizontalAlignment.Left };
        MaterialTextField.SetLabel(password, "Пароль");
        MaterialTextField.SetShowPasswordReveal(password, true);
        panel.Children.Add(password);
        return Scroll(panel);
    }

    private static Control Selection()
    {
        var panel = Panel();
        panel.Children.Add(new CheckBox { Content = "Three states", IsThreeState = true, IsChecked = null });
        panel.Children.Add(new CheckBox { Content = "Checked", IsChecked = true });
        panel.Children.Add(new CheckBox { Content = "Disabled", IsEnabled = false });
        panel.Children.Add(new RadioButton { Content = "Первый", GroupName = "demo", IsChecked = true });
        panel.Children.Add(new RadioButton { Content = "Второй", GroupName = "demo" });
        panel.Children.Add(new ToggleSwitch { Content = "Уведомления", IsChecked = true });
        panel.Children.Add(new ToggleSwitch { Content = "Без иконок", IsChecked = false });
        panel.Children.Add(new ToggleSwitch { Content = "Недоступно", IsEnabled = false, IsChecked = true });
        return Scroll(panel);
    }

    private static Control Ranges()
    {
        var panel = Panel();
        panel.Children.Add(new Slider { Minimum = 0, Maximum = 100, Value = 35, TickFrequency = 10, IsSnapToTickEnabled = true });
        panel.Children.Add(new MaterialRangeSlider { Minimum = 0, Maximum = 100, LowerValue = 20, UpperValue = 80, TickFrequency = 10, IsSnapToTickEnabled = true });
        panel.Children.Add(new ProgressBar { Minimum = 0, Maximum = 100, Value = 65, ShowProgressText = true });
        panel.Children.Add(new ProgressBar { IsIndeterminate = true });
        var row = Row();
        row.Children.Add(new CircularProgressIndicator { Value = 65 });
        row.Children.Add(new CircularProgressIndicator { IsIndeterminate = true });
        panel.Children.Add(row);
        var sliders = Row();
        sliders.Children.Add(new Slider { Orientation = Orientation.Vertical, Height = 150, Minimum = 0, Maximum = 100, Value = 60, TickFrequency = 20, IsSnapToTickEnabled = true });
        sliders.Children.Add(new Slider { Width = 240, Minimum = 0, Maximum = 100, Value = 60, IsEnabled = false });
        sliders.Children.Add(new Slider { Width = 240, Minimum = 0, Maximum = 100, Value = 0 });
        sliders.Children.Add(new Slider { Width = 240, Minimum = 0, Maximum = 100, Value = 100 });
        panel.Children.Add(sliders);
        return Scroll(panel);
    }

    private static Control Collections()
    {
        var panel = Panel();
        var list = new ListBox { SelectionMode = SelectionMode.Multiple };
        for (var lines = 1; lines <= 3; lines++)
        {
            var item = new ListBoxItem();
            MaterialList.SetHeadline(item, "List item headline");
            MaterialList.SetLineCount(item, lines);
            if (lines > 1) MaterialList.SetSupportingText(item, lines == 2 ? "Supporting text" : "Supporting text that wraps over two lines to demonstrate the three-line list layout.");
            MaterialList.SetLeadingContent(item, new PathIcon { Data = Avalonia.Media.Geometry.Parse("M3,5 L21,5 L21,19 L3,19 Z M3,5 L12,12 L21,5"), Width = 24, Height = 24 });
            MaterialList.SetTrailingContent(item, new TextBlock { Text = "100+" });
            list.Items.Add(item);
        }
        panel.Children.Add(list);
        var secondary = new TabControl { Height = 160 };
        MaterialTabs.SetVariant(secondary, MaterialTabVariant.Secondary);
        secondary.Items.Add(new TabItem { Header = "First", Content = "Первый раздел" });
        secondary.Items.Add(new TabItem { Header = "Second", Content = "Второй раздел" });
        panel.Children.Add(secondary);
        var menu = new Menu();
        var file = new MenuItem { Header = "_Файл" };
        file.Items.Add(new MenuItem { Header = "_Открыть", InputGesture = new Avalonia.Input.KeyGesture(Avalonia.Input.Key.O, Avalonia.Input.KeyModifiers.Control) });
        var nested = new MenuItem { Header = "Вложенное" };
        nested.Items.Add(new MenuItem { Header = "Действие" });
        file.Items.Add(nested);
        menu.Items.Add(file);
        panel.Children.Add(menu);
        var tooltip = new Button { Content = "Plain tooltip" };
        ToolTip.SetTip(tooltip, "Подсказка с клавиатуры и мыши");
        panel.Children.Add(tooltip);
        var rich = new RichTooltip { Title = "Rich tooltip", Content = "Доступное действие внутри подсказки" };
        var dismiss = new Button { Content = "Понятно" };
        dismiss.Click += (_, _) => rich.Hide();
        rich.Actions = dismiss;
        panel.Children.Add(new Button { Content = "Открыть rich tooltip", Flyout = rich });
        panel.Children.Add(new ComboBox { ItemsSource = new[] { "Первый", "Второй", "Третий" }, SelectedIndex = 0, Width = 240 });
        panel.Children.Add(new Calendar());
        return Scroll(panel);
    }

    private static Control Typography(MaterialTheme theme)
    {
        var panel = Panel();
        var reduced = new ToggleSwitch { Content = "Reduced motion" };
        reduced.IsCheckedChanged += (_, _) => theme.Motion = reduced.IsChecked == true ? MaterialMotionMode.Reduced : MaterialMotionMode.Expressive;
        panel.Children.Add(reduced);
        foreach (var style in Enum.GetValues<MaterialTypeStyle>())
        {
            var text = new TextBlock { Text = $"{style}: Material Design\nВторая строка" };
            MaterialTypography.SetStyle(text, style);
            panel.Children.Add(text);
            var emphasized = new TextBlock { Text = $"{style} Emphasized" };
            MaterialTypography.SetStyle(emphasized, style);
            MaterialTypography.SetEmphasized(emphasized, true);
            panel.Children.Add(emphasized);
        }
        return Scroll(panel);
    }
}
