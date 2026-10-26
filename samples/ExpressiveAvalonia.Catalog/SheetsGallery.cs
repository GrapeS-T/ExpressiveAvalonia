using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Catalog;

/// <summary>A live side or bottom sheet hosted over a demo surface, configured in the inspector.</summary>
public static class SheetsGallery
{
    public static Control Create() => new SheetPlayground();
}

internal sealed class SheetPlayground : Grid, IComponentPlayground
{
    private readonly Grid _host = new();
    private readonly Border _surface;
    private readonly MaterialSideSheet _side;
    private readonly MaterialBottomSheet _bottom;
    private readonly StackPanel _sideOptions = new() { Spacing = 4 };
    private readonly StackPanel _bottomItems = new() { Spacing = 4 };
    private readonly Button _open = new();
    private readonly TextBlock _readout = new()
    {
        Text = "Лист закрыт · нажмите «Открыть»",
        TextWrapping = TextWrapping.Wrap,
        MaxWidth = 420,
        FontSize = 14,
    };
    private readonly Button _sidePrimary;
    private readonly Button _bottomPrimary;
    private readonly MaterialButtonGroup _form;
    private readonly MaterialButtonGroup _edge;
    private readonly ToggleSwitch _modal;
    private readonly ToggleSwitch _back;
    private readonly ToggleSwitch _closeButton;
    private readonly ToggleSwitch _dragHandle;
    private readonly ToggleSwitch _dismiss;
    private readonly ToggleSwitch _actions;
    private readonly TextBox _headline;
    private readonly TextBox _actionLabel;
    private readonly Slider _sideWidth;
    private readonly Slider _options;
    private readonly Slider _height;
    private readonly StackPanel _sideSettings = new() { Spacing = 16 };
    private readonly StackPanel _bottomSettings = new() { Spacing = 16 };
    private bool _showingSide = true;
    private bool _built;

    public StackPanel Settings { get; } = new() { Spacing = 16 };

    internal SheetPlayground()
    {
        _side = new MaterialSideSheet
        {
            Headline = "Фильтры",
            Side = MaterialSheetSide.End,
            ShowBackButton = true,
            ShowCloseButton = true,
            IsModal = true,
        };
        _side.Content = BuildSideContent();
        var sideActions = SheetActions(_side, "Сохранить");
        _side.Actions = sideActions.Actions;
        _sidePrimary = sideActions.Primary;

        _bottom = new MaterialBottomSheet
        {
            ShowDragHandle = true,
            IsModal = true,
        };
        _bottom.Content = BuildBottomContent();
        var bottomActions = SheetActions(_bottom, "Готово");
        _bottom.Actions = bottomActions.Actions;
        _bottomPrimary = bottomActions.Primary;

        var body = new StackPanel
        {
            Spacing = 16,
            Margin = new Thickness(32),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        body.Children.Add(new TextBlock { Text = "Область приложения", FontSize = 24, FontWeight = FontWeight.Medium });
        body.Children.Add(new TextBlock
        {
            Text = "Лист открывается поверх этой области. Модальный лист блокирует страницу, " +
                   "циклически проводит Tab и закрывается по Escape или клику по scrim; " +
                   "стандартный оставляет страницу интерактивной.",
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 420,
            Opacity = 0.7,
        });
        body.Children.Add(_open);
        body.Children.Add(_readout);

        MaterialAssist.SetButtonVariant(_open, MaterialButtonVariant.Filled);
        MaterialAssist.SetControlSize(_open, MaterialControlSize.Small);
        _open.Content = "Открыть боковой лист";
        AutomationProperties.SetName(_open, "Открыть боковой лист");
        _open.Click += (_, _) => OpenSheet();

        _host.Children.Add(body);
        _host.Children.Add(_side);
        _host.Children.Add(_bottom);

        _surface = new Border
        {
            Child = _host,
            Height = 520,
            Margin = new Thickness(24),
            CornerRadius = new CornerRadius(16),
            BorderThickness = new Thickness(1),
            ClipToBounds = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _surface.Bind(Border.BackgroundProperty, this.GetResourceObservable("MaterialSurfaceContainerLowBrush"));
        _surface.Bind(Border.BorderBrushProperty, this.GetResourceObservable("MaterialOutlineVariantBrush"));
        Children.Add(_surface);

        _form = Choice(Settings, "Форма", new[] { "Боковая", "Нижняя" }, 0);
        _modal = Toggle(Settings, "Модальный", true);

        _edge = Choice(_sideSettings, "Сторона", new[] { "Start", "End" }, 1);
        _headline = TextField(_sideSettings, "Заголовок", "Фильтры");
        _back = Toggle(_sideSettings, "Кнопка «Назад»", true);
        _closeButton = Toggle(_sideSettings, "Кнопка закрытия", true);
        _sideWidth = Number(_sideSettings, "Ширина листа", 240, 400, 320, 8);
        Settings.Children.Add(_sideSettings);

        _dragHandle = Toggle(_bottomSettings, "Drag handle", true);
        Settings.Children.Add(_bottomSettings);

        _dismiss = Toggle(Settings, "Закрытие по Escape и scrim", true);
        _actions = Toggle(Settings, "Кнопки действий", true);
        _actionLabel = TextField(Settings, "Основное действие", "Сохранить");
        _options = Number(Settings, "Пунктов в содержимом", 2, 10, 4, 1);
        _height = Number(Settings, "Высота области", 360, 720, 520, 20);
        Settings.Children.Add(new TextBlock
        {
            Text = "Боковой лист: заголовок, необязательные «Назад» и закрытие, прокручиваемое " +
                   "содержимое и действия. Нижний лист: drag handle, содержимое и закрытие " +
                   "перетаскиванием за порог 96 dp. «Назад» сообщает BackRequested и не закрывает лист.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
        });

        AutomationProperties.SetName(_headline, "Заголовок листа");
        AutomationProperties.SetName(_actionLabel, "Текст основного действия");

        foreach (var (sheet, name) in new (MaterialSheet Sheet, string Name)[]
                 {
                     (_side, "Боковой лист"), (_bottom, "Нижний лист"),
                 })
        {
            sheet.Opening += (_, _) => _readout.Text = $"{name}: открывается";
            sheet.Opened += (_, _) =>
                _readout.Text = $"{name}: открыт · {(sheet.IsModal ? "модальный" : "стандартный")}";
            sheet.Closing += (_, _) => _readout.Text = $"{name}: закрывается";
            sheet.Closed += (_, _) => _readout.Text = $"{name}: закрыт";
        }
        _side.BackRequested += (_, e) =>
        {
            e.Handled = true;
            _readout.Text = "«Назад» обработан внутри листа без закрытия";
        };

        foreach (var choice in new[] { _form, _edge })
            choice.SelectionChanged += (_, _) => Apply();
        foreach (var toggle in new[] { _modal, _back, _closeButton, _dragHandle, _dismiss, _actions })
            toggle.IsCheckedChanged += (_, _) => Apply();
        foreach (var slider in new[] { _sideWidth, _options, _height })
            slider.PropertyChanged += (_, e) => { if (e.Property == RangeBase.ValueProperty) Apply(); };
        foreach (var field in new[] { _headline, _actionLabel })
            field.TextChanged += (_, _) => Apply();

        _built = true;
        Apply();
    }

    private void OpenSheet()
    {
        var target = _showingSide ? (MaterialSheet)_side : _bottom;
        var other = _showingSide ? (MaterialSheet)_bottom : _side;
        other.IsOpen = false;
        target.IsOpen = true;
    }

    private void Apply()
    {
        if (!_built) return;

        var side = _form.SelectedIndex == 0;
        var modal = _modal.IsChecked == true;
        var count = (int)System.Math.Round(_options.Value);
        var actionLabel = string.IsNullOrWhiteSpace(_actionLabel.Text) ? "Сохранить" : _actionLabel.Text;

        if (side != _showingSide)
        {
            _showingSide = side;
            _side.IsOpen = false;
            _bottom.IsOpen = false;
        }

        _sideSettings.IsVisible = side;
        _bottomSettings.IsVisible = !side;

        _side.IsModal = modal;
        _side.Side = _edge.SelectedIndex == 1 ? MaterialSheetSide.End : MaterialSheetSide.Start;
        _side.Headline = string.IsNullOrWhiteSpace(_headline.Text) ? "Заголовок" : _headline.Text;
        _side.ShowBackButton = _back.IsChecked == true;
        _side.ShowCloseButton = _closeButton.IsChecked == true;
        _side.SheetWidth = _sideWidth.Value;
        _side.ShowActions = _actions.IsChecked == true;
        _side.IsDismissEnabled = _dismiss.IsChecked == true;

        _bottom.IsModal = modal;
        _bottom.ShowDragHandle = _dragHandle.IsChecked == true;
        _bottom.ShowActions = _actions.IsChecked == true;
        _bottom.IsDismissEnabled = _dismiss.IsChecked == true;

        _sidePrimary.Content = actionLabel;
        _bottomPrimary.Content = actionLabel;
        AutomationProperties.SetName(_sidePrimary, actionLabel);
        AutomationProperties.SetName(_bottomPrimary, actionLabel);

        _surface.Height = _height.Value;
        _open.Content = side ? "Открыть боковой лист" : "Открыть нижний лист";
        AutomationProperties.SetName(_open, _open.Content as string ?? "Открыть лист");

        SyncSideOptions(count);
        SyncBottomItems(count);
    }

    private Control BuildSideContent()
    {
        var panel = new StackPanel { Spacing = 16, Margin = new Thickness(24, 0, 24, 24) };
        panel.Children.Add(new TextBlock { Text = "Содержимое листа", FontSize = 20, Opacity = 0.7 });
        panel.Children.Add(new TextBlock
        {
            Text = "Содержимое прокручивается независимо от заголовка и действий. Фокус входит в лист " +
                   "и возвращается на кнопку открытия.",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.7,
        });
        var query = new TextBox { PlaceholderText = "Поиск" };
        MaterialTextField.SetVariant(query, MaterialTextFieldVariant.Outlined);
        MaterialTextField.SetLabel(query, "Запрос");
        AutomationProperties.SetName(query, "Запрос");
        panel.Children.Add(query);
        panel.Children.Add(_sideOptions);
        return panel;
    }

    private Control BuildBottomContent()
    {
        var panel = new StackPanel { Spacing = 12, Margin = new Thickness(24, 4, 24, 24) };
        panel.Children.Add(new TextBlock { Text = "Содержимое листа", FontSize = 20, Opacity = 0.7 });
        panel.Children.Add(new TextBlock
        {
            Text = "Потяните handle вниз дальше 96 dp или фликните, чтобы закрыть лист.",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.7,
        });
        panel.Children.Add(_bottomItems);
        var close = new Button { Content = "Закрыть из содержимого", HorizontalAlignment = HorizontalAlignment.Left };
        MaterialAssist.SetButtonVariant(close, MaterialButtonVariant.Text);
        MaterialAssist.SetControlSize(close, MaterialControlSize.Small);
        AutomationProperties.SetName(close, "Закрыть лист из содержимого");
        close.Click += (_, _) =>
        {
            _readout.Text = "Закрыто из содержимого листа";
            _bottom.IsOpen = false;
        };
        panel.Children.Add(close);
        return panel;
    }

    private (Control Actions, Button Primary) SheetActions(MaterialSheet sheet, string primaryLabel)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        var primary = new Button { Content = primaryLabel };
        MaterialAssist.SetButtonVariant(primary, MaterialButtonVariant.Filled);
        MaterialAssist.SetControlSize(primary, MaterialControlSize.Small);
        primary.Click += (_, _) =>
        {
            _readout.Text = $"«{primary.Content}» нажато · лист закрыт";
            sheet.IsOpen = false;
        };

        var secondary = new Button { Content = "Отмена" };
        MaterialAssist.SetButtonVariant(secondary, MaterialButtonVariant.Outlined);
        MaterialAssist.SetControlSize(secondary, MaterialControlSize.Small);
        AutomationProperties.SetName(secondary, "Отмена");
        secondary.Click += (_, _) =>
        {
            _readout.Text = "Отменено · лист закрыт";
            sheet.IsOpen = false;
        };

        row.Children.Add(primary);
        row.Children.Add(secondary);
        return (row, primary);
    }

    private void SyncSideOptions(int count)
    {
        while (_sideOptions.Children.Count > count)
            _sideOptions.Children.RemoveAt(_sideOptions.Children.Count - 1);

        while (_sideOptions.Children.Count < count)
        {
            var index = _sideOptions.Children.Count;
            var option = new CheckBox { Content = $"Опция {index + 1}" };
            AutomationProperties.SetName(option, $"Опция {index + 1}");
            _sideOptions.Children.Add(option);
        }
    }

    private void SyncBottomItems(int count)
    {
        while (_bottomItems.Children.Count > count)
            _bottomItems.Children.RemoveAt(_bottomItems.Children.Count - 1);

        while (_bottomItems.Children.Count < count)
            _bottomItems.Children.Add(new TextBlock { Text = $"Элемент списка {_bottomItems.Children.Count + 1}" });
    }

    private static MaterialButtonGroup Choice(StackPanel host, string label, string[] options, int selected)
    {
        host.Children.Add(new TextBlock { Text = label });
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

    private static TextBox TextField(StackPanel host, string label, string value)
    {
        host.Children.Add(new TextBlock { Text = label });
        var field = new TextBox { Text = value, HorizontalAlignment = HorizontalAlignment.Stretch };
        MaterialTextField.SetVariant(field, MaterialTextFieldVariant.Outlined);
        MaterialTextField.SetLabel(field, label);
        host.Children.Add(field);
        return field;
    }

    private static Slider Number(
        StackPanel host, string label, double min, double max, double value, double step)
    {
        var caption = new TextBlock { Text = $"{label}: {value:0}" };
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
        AutomationProperties.SetName(slider, label);
        slider.PropertyChanged += (_, e) =>
        {
            if (e.Property == RangeBase.ValueProperty) caption.Text = $"{label}: {slider.Value:0}";
        };
        host.Children.Add(slider);
        return slider;
    }
}
