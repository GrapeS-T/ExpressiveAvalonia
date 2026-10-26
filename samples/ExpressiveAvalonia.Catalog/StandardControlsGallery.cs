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
        pages.Items.Add(new TabItem { Header = "Buttons", Content = new DeferredGallery(ButtonsGallery.CreateClassic) });
        pages.Items.Add(new TabItem { Header = "Text fields", Content = new DeferredGallery(TextFieldsGallery.CreateClassic) });
        pages.Items.Add(new TabItem { Header = "Selection", Content = new DeferredGallery(SelectionGallery.CreateClassic) });
        pages.Items.Add(new TabItem { Header = "Ranges", Content = new DeferredGallery(Ranges) });
        pages.Items.Add(new TabItem { Header = "Collections", Content = new DeferredGallery(Collections) });
        pages.Items.Add(new TabItem { Header = "Tabs", Content = new DeferredGallery(TabsGallery.CreateClassic) });
        pages.Items.Add(new TabItem { Header = "Search", Content = new DeferredGallery(SearchGallery.CreateClassic) });
        pages.Items.Add(new TabItem { Header = "Progress", Content = new DeferredGallery(ProgressGallery.CreateClassic) });
        pages.Items.Add(new TabItem { Header = "Typography", Content = new DeferredGallery(() => Typography(theme)) });
        return pages;
    }

    private static StackPanel Panel() => new() { Spacing = 16, Margin = new Thickness(24) };
    private static StackPanel Row() => new() { Orientation = Orientation.Horizontal, Spacing = 12 };
    private static Control Scroll(Control content) => new ScrollViewer { Content = content, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };




    private static Control Ranges()
    {
        var panel = Panel();
        panel.Children.Add(new Slider { Minimum = 0, Maximum = 100, Value = 35, TickFrequency = 10, IsSnapToTickEnabled = true });
        panel.Children.Add(new MaterialRangeSlider { Minimum = 0, Maximum = 100, LowerValue = 20, UpperValue = 80, TickFrequency = 10, IsSnapToTickEnabled = true });
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
