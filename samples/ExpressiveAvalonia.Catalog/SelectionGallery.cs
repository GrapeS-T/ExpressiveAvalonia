using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Catalog;

/// <summary>Source families: Checkboxes (local-001236), Radio buttons (local-004445), Switch (local-005081).</summary>
public static class SelectionGallery
{
    public static Control Create()
    {
        var status = new TextBlock { Text = "Состояние: ожидание", FontSize = 16 };
        var panel = new StackPanel { Spacing = 24, Margin = new Thickness(24) };

        panel.Children.Add(Heading("Checkboxes · local-001236"));
        panel.Children.Add(Note("Типы: unselected / selected / indeterminate / error × состояния enabled / disabled. " +
                                "Пробел и Enter переключают; трёхпозиционный флажок циклически проходит все три значения."));
        panel.Children.Add(Wrap(
            Check("Unselected", false, status),
            Check("Selected", true, status),
            Indeterminate(status),
            DisabledCheck("Disabled", false),
            DisabledCheck("Disabled selected", true),
            ErrorCheck("Error unselected", false),
            ErrorCheck("Error selected", true),
            ErrorCheck("Error indeterminate", null)));

        panel.Children.Add(Heading("Radio buttons · local-004445"));
        panel.Children.Add(Note("Собственная группа Avalonia: стрелки перемещают выбор внутри группы, " +
                                "Checked срабатывает только при входе в состояние."));
        var groupA = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var groupB = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        for (var i = 1; i <= 3; i++)
        {
            var radio = new RadioButton { Content = $"Вариант {i}", GroupName = "gallery-a", IsChecked = i == 1 };
            var index = i;
            radio.IsCheckedChanged += (_, _) =>
            {
                if (radio.IsChecked == true) status.Text = $"Состояние: radio A{index}";
            };
            groupA.Children.Add(radio);
        }
        for (var i = 1; i <= 3; i++)
            groupB.Children.Add(new RadioButton { Content = $"Группа B{i}", GroupName = "gallery-b", IsChecked = i == 2 });
        var disabledRadio = new RadioButton { Content = "Disabled", IsEnabled = false, GroupName = "gallery-c" };
        var disabledCheckedRadio = new RadioButton { Content = "Disabled selected", IsEnabled = false, IsChecked = true, GroupName = "gallery-d" };
        panel.Children.Add(groupA);
        panel.Children.Add(groupB);
        panel.Children.Add(Row(disabledRadio, disabledCheckedRadio));

        panel.Children.Add(Heading("Switch · local-005081"));
        panel.Children.Add(Note("Типы: selected × icon × состояние. Ручка перетаскивается указателем; " +
                                "ShowSwitchIcons включает встроенные глифы check/close из источника."));
        panel.Children.Add(Wrap(
            Switch("Off", false, false, status),
            Switch("On", true, false, status),
            Switch("Off + icons", false, true, status),
            Switch("On + icons", true, true, status)));
        panel.Children.Add(Wrap(
            DisabledSwitch("Disabled", false, false),
            DisabledSwitch("Disabled on", true, false),
            DisabledSwitch("Disabled + icons", false, true),
            DisabledSwitch("Disabled on + icons", true, true)));

        panel.Children.Add(Heading("Handle icon content"));
        panel.Children.Add(Note("OnContent/OffContent остаются штатным способом задать пользовательский глиф ручки."));
        var contentSwitch = new ToggleSwitch
        {
            IsChecked = true,
            OnContent = new PathIcon { Width = 16, Height = 16, Data = Geometry.Parse("M3.8 8.01667L0 4.21667L0.95 3.26667L3.8 6.11667L9.91667 0L10.8667 0.95L3.8 8.01667Z") },
            OffContent = new PathIcon { Width = 16, Height = 16, Data = Geometry.Parse("M0.933333 9.33333L0 8.4L3.73333 4.66667L0 0.933333L0.933333 0L4.66667 3.73333L8.4 0L9.33333 0.933333L5.6 4.66667L9.33333 8.4L8.4 9.33333L4.66667 5.6L0.933333 9.33333Z") },
        };
        panel.Children.Add(contentSwitch);

        panel.Children.Add(status);
        return new ScrollViewer
        {
            Content = panel,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
    }

    private static TextBlock Heading(string text) => new()
    {
        Text = text,
        FontSize = 22,
        FontWeight = FontWeight.Medium,
    };

    private static TextBlock Note(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap, MaxWidth = 760 };

    private static WrapPanel Wrap(params Control[] children)
    {
        var wrap = new WrapPanel { ItemSpacing = 16, LineSpacing = 8 };
        foreach (var child in children) wrap.Children.Add(child);
        return wrap;
    }

    private static StackPanel Row(params Control[] children)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
        foreach (var child in children) row.Children.Add(child);
        return row;
    }

    private static CheckBox Check(string label, bool value, TextBlock status)
    {
        var check = new CheckBox { Content = label, IsChecked = value };
        check.IsCheckedChanged += (_, _) => status.Text = $"Состояние: {label} = {check.IsChecked}";
        return check;
    }

    private static CheckBox Indeterminate(TextBlock status)
    {
        var check = new CheckBox { Content = "Indeterminate", IsThreeState = true, IsChecked = null };
        check.IsCheckedChanged += (_, _) => status.Text = $"Состояние: indeterminate = {check.IsChecked?.ToString() ?? "null"}";
        return check;
    }

    private static CheckBox DisabledCheck(string label, bool value) =>
        new() { Content = label, IsChecked = value, IsEnabled = false };

    private static CheckBox ErrorCheck(string label, bool? value)
    {
        var check = new CheckBox { Content = label, IsChecked = value, IsThreeState = value is null };
        DataValidationErrors.SetErrors(check, new object[] { "Обязательное поле" });
        return check;
    }

    private static ToggleSwitch Switch(string label, bool value, bool icons, TextBlock status)
    {
        var toggle = new ToggleSwitch { Content = label, IsChecked = value };
        MaterialSelection.SetShowSwitchIcons(toggle, icons);
        toggle.IsCheckedChanged += (_, _) => status.Text = $"Состояние: {label} = {toggle.IsChecked}";
        return toggle;
    }

    private static ToggleSwitch DisabledSwitch(string label, bool value, bool icons)
    {
        var toggle = new ToggleSwitch { Content = label, IsChecked = value, IsEnabled = false };
        MaterialSelection.SetShowSwitchIcons(toggle, icons);
        return toggle;
    }
}
