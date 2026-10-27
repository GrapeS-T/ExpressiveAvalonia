using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using ExpressiveAvalonia.Controls;

namespace ExpressiveAvalonia.Catalog;

/// <summary>Live Material 3 Expressive scroll fields with hoisted state and custom formatting.</summary>
public static class ScrollFieldGallery
{
    public static Control Create() => new ScrollFieldPlayground();
}

internal sealed class ScrollFieldPlayground : Grid, IComponentPlayground
{
    private readonly MaterialScrollFieldState _numberState = new(1001, 0);
    private readonly MaterialScrollFieldState _hourState = new(24, 12);
    private readonly MaterialScrollFieldState _minuteState = new(60, 30);
    private readonly MaterialScrollFieldState _unitState = new(2, 0);
    private readonly MaterialScrollField _number;
    private readonly MaterialScrollField _hour;
    private readonly MaterialScrollField _minute;
    private readonly MaterialScrollField _unit;
    private readonly TextBlock _readout = new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        FontSize = 16,
        TextWrapping = TextWrapping.Wrap
    };

    public StackPanel Settings { get; } = new() { Spacing = 16 };

    internal ScrollFieldPlayground()
    {
        _number = Field(_numberState, 192, "Выбор значения от 1000 до 2000");
        _number.Formatter = index => (1000 + index).ToString("0");
        _number.FieldAccessibilityDescription = index => (1000 + index).ToString("0");

        _hour = Field(_hourState, 100, "Выбор часа");
        _hour.Formatter = index => index.ToString("D2");
        _hour.FieldAccessibilityDescription = index =>
            index == 1 ? $"{index} час" : $"{index} часов";

        _minute = Field(_minuteState, 100, "Выбор минут");
        _minute.Formatter = index => index.ToString("D2");
        _minute.FieldAccessibilityDescription = index =>
            index == 1 ? $"{index} минута" : $"{index} минут";

        _unit = Field(_unitState, 120, "Выбор единицы измерения");
        _unit.Formatter = index => index == 0 ? "L" : "oz";
        _unit.FieldAccessibilityDescription = index => index == 0 ? "литры" : "унции";
        _unit.ItemTemplate = new FuncDataTemplate<MaterialScrollFieldItem>((item, _) =>
        {
            var text = new TextBlock
            {
                FontSize = 24,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            text.Bind(TextBlock.TextProperty, new Binding(nameof(MaterialScrollFieldItem.Text)));
            return text;
        });

        var numberBlock = Labelled("Значение", _number);
        var hourBlock = Labelled("Часы", _hour);
        var minuteBlock = Labelled("Минуты", _minute);
        var unitBlock = Labelled("Единица", _unit);

        var clock = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        clock.Children.Add(hourBlock);
        clock.Children.Add(new TextBlock
        {
            Text = ":",
            FontSize = 45,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 8)
        });
        clock.Children.Add(minuteBlock);

        var wheels = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 24,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        wheels.Children.Add(numberBlock);
        wheels.Children.Add(clock);
        wheels.Children.Add(unitBlock);

        var stage = new StackPanel
        {
            Spacing = 24,
            Margin = new Thickness(24),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        stage.Children.Add(wheels);
        stage.Children.Add(_readout);
        Children.Add(stage);

        var enabled = new ToggleSwitch { Content = "Доступно", IsChecked = true };
        enabled.IsCheckedChanged += (_, _) => SetEnabled(enabled.IsChecked == true);
        Settings.Children.Add(enabled);

        var next = new Button { Content = "Следующее значение", HorizontalAlignment = HorizontalAlignment.Stretch };
        next.Click += (_, _) => _numberState.AnimateScrollToOption(
            Mod(_numberState.SelectedOption + 1, _numberState.ItemCount));
        Settings.Children.Add(next);

        var previous = new Button { Content = "Предыдущее значение", HorizontalAlignment = HorizontalAlignment.Stretch };
        previous.Click += (_, _) => _numberState.AnimateScrollToOption(
            Mod(_numberState.SelectedOption - 1, _numberState.ItemCount));
        Settings.Children.Add(previous);

        Settings.Children.Add(new TextBlock
        {
            Text =
                "ScrollField — колесо выбора на бесконечном pager, а не TextBox. " +
                "Высота 120 dp, слот 52 dp; выбранный слот анимирован fastSpatial из DisplayMedium " +
                "в DisplayLargeEmphasized, цвет — из outline в onSurface. Прокрутите колесом, " +
                "потяните мышью или пальцем, щёлкните слот, используйте ↑/↓ после фокуса. " +
                "Fling ограничен одним слотом; автоматизация страниц имеет диапазон 0–99 999. " +
                "Живая подпись показывает selectedOption и targetOption, которые различаются до завершения settle.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13
        });

        foreach (var state in new[] { _numberState, _hourState, _minuteState, _unitState })
            state.PropertyChanged += (_, e) =>
            {
                if (e.Property == MaterialScrollFieldState.SelectedOptionProperty ||
                    e.Property == MaterialScrollFieldState.TargetOptionProperty ||
                    e.Property == MaterialScrollFieldState.IsScrollInProgressProperty)
                {
                    UpdateReadout();
                }
            };
        UpdateReadout();
    }

    private static MaterialScrollField Field(MaterialScrollFieldState state, double width,
        string description)
    {
        var field = new MaterialScrollField
        {
            State = state,
            Width = width,
            Height = 120,
            ContentDescription = description
        };
        AutomationProperties.SetName(field, description);
        return field;
    }

    private static Control Labelled(string label, MaterialScrollField field)
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock
        {
            Text = label,
            HorizontalAlignment = HorizontalAlignment.Center,
            FontSize = 12
        });
        panel.Children.Add(field);
        return panel;
    }

    private void SetEnabled(bool enabled)
    {
        _number.IsEnabled = enabled;
        _hour.IsEnabled = enabled;
        _minute.IsEnabled = enabled;
        _unit.IsEnabled = enabled;
        UpdateReadout();
    }

    private void UpdateReadout()
    {
        _readout.Text =
            $"Значение: selected {_numberState.SelectedOption}, target {_numberState.TargetOption}, " +
            $"scroll {(_numberState.IsScrollInProgress ? "идёт" : "завершён")};   " +
            $"Время: {_hourState.SelectedOption:D2}:{_minuteState.SelectedOption:D2}, " +
            $"target {_hourState.TargetOption:D2}:{_minuteState.TargetOption:D2};   " +
            $"Единица: {_unitState.SelectedOption}";
    }

    private static int Mod(int value, int count) => (value % count + count) % count;
}
