using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Catalog;

/// <summary>One live tooltip owner: a plain tooltip or an actionable rich tooltip.</summary>
public static class TooltipsGallery
{
    public static Control Create() => new TooltipsPlayground();
}

internal sealed class TooltipsPlayground : Grid, IComponentPlayground
{
    private const string LongText =
        "Обычная подсказка переносит строку на 200 DIP и растёт до двух и более строк, а короткая — облегает текст.";

    private readonly ContentControl _host = new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
    };
    private readonly Button _plainOwner = new() { Content = "Сохранить" };
    private readonly Button _richOwner = new() { Content = "Опубликовать…" };
    private readonly TextBlock _readout = new()
    {
        Text = "Действие ещё не выполнялось.",
        HorizontalAlignment = HorizontalAlignment.Center,
        FontSize = 14,
        TextWrapping = TextWrapping.Wrap,
        TextAlignment = TextAlignment.Center,
        MaxWidth = 420,
    };
    private readonly TextBlock _hint = new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        FontSize = 12,
        TextWrapping = TextWrapping.Wrap,
        TextAlignment = TextAlignment.Center,
        MaxWidth = 420,
    };
    private readonly MaterialButtonGroup _kind;
    private readonly ComboBox _plainType;
    private readonly TextBox _plainText;
    private readonly Slider _delay;
    private readonly TextBlock _delayCaption = new();
    private readonly ToggleSwitch _longPress;
    private readonly ToggleSwitch _showOnDisabled;
    private readonly ToggleSwitch _ownerEnabled;
    private readonly ToggleSwitch _exposeHelp;
    private readonly TextBox _title;
    private readonly TextBox _body;
    private readonly ComboBox _actions;
    private readonly TextBox _primary;
    private readonly TextBox _secondary;
    private readonly ToggleSwitch _showTitle;
    private readonly ToggleSwitch _showActions;
    private readonly ToggleSwitch _showSecondary;
    private readonly ComboBox _mode;
    private readonly StackPanel _plainPanel = new() { Spacing = 16 };
    private readonly StackPanel _richPanel = new() { Spacing = 16 };
    private readonly StackPanel _primaryPanel = new() { Spacing = 8 };
    private readonly StackPanel _secondaryPanel = new() { Spacing = 8 };
    private bool _syncing;
    public StackPanel Settings { get; } = new() { Spacing = 16 };

    internal TooltipsPlayground()
    {
        MaterialAssist.SetButtonVariant(_plainOwner, MaterialButtonVariant.Filled);
        MaterialAssist.SetButtonVariant(_richOwner, MaterialButtonVariant.Tonal);
        AutomationProperties.SetName(_plainOwner, "Кнопка с обычной подсказкой");
        AutomationProperties.SetName(_richOwner, "Кнопка с расширенной подсказкой");
        MaterialToolTip.SetKeyboardEnabled(_plainOwner, true);
        MaterialToolTip.SetKeyboardEnabled(_richOwner, true);

        var card = new Border
        {
            Width = 520,
            MinHeight = 280,
            Padding = new Thickness(24),
            CornerRadius = new CornerRadius(24),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = _host,
        };
        card.Bind(Border.BackgroundProperty, card.GetResourceObservable("MaterialSurfaceContainerLowBrush"));

        var stage = new StackPanel
        {
            Spacing = 16,
            Margin = new Thickness(24),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        stage.Children.Add(card);
        stage.Children.Add(_hint);
        stage.Children.Add(_readout);
        Children.Add(stage);

        _kind = Choice("Тип подсказки", new[] { "Plain", "Rich" }, 0);

        _plainPanel.Children.Add(new TextBlock { Text = "Макет" });
        _plainType = new ComboBox
        {
            ItemsSource = new[] { "Single line (200 DIP)", "Multi line (200 DIP)" },
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        AutomationProperties.SetName(_plainType, "Макет обычной подсказки");
        _plainPanel.Children.Add(_plainType);

        _plainText = TextField("Текст подсказки", LongText, acceptsReturn: true);
        _plainPanel.Children.Add(_plainText);

        _plainPanel.Children.Add(_delayCaption);
        _delay = new Slider { Minimum = 0, Maximum = 2000, Value = 500, TickFrequency = 100, IsSnapToTickEnabled = true };
        MaterialAssist.SetControlSize(_delay, MaterialControlSize.ExtraSmall);
        MaterialSlider.SetShowStops(_delay, false);
        AutomationProperties.SetName(_delay, "Задержка появления");
        _plainPanel.Children.Add(_delay);

        _longPress = Toggle(_plainPanel, "Открывать долгим нажатием (500 мс)", true);
        _showOnDisabled = Toggle(_plainPanel, "Показывать у недоступного владельца", false);
        _ownerEnabled = Toggle(_plainPanel, "Владелец доступен", true);
        _exposeHelp = Toggle(_plainPanel, "Текст помощи для скринридера", true);
        Settings.Children.Add(_plainPanel);

        _richPanel.Children.Add(new TextBlock { Text = "Открытие" });
        _mode = new ComboBox
        {
            ItemsSource = new[] { "Кнопка с Flyout", "Context flyout + ShowAt" },
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        AutomationProperties.SetName(_mode, "Способ открытия расширенной подсказки");
        _richPanel.Children.Add(_mode);

        _title = TextField("Заголовок (Title)", "Опубликовать изменения");
        _richPanel.Children.Add(_title);
        _body = TextField("Текст (Content)", "Публикация делает ревизию видимой всем, у кого есть доступ к библиотеке.", acceptsReturn: true);
        _richPanel.Children.Add(_body);

        _richPanel.Children.Add(new TextBlock { Text = "Действия" });
        _actions = new ComboBox
        {
            ItemsSource = new[] { "Две кнопки", "Свой набор (оценка)", "Без действий" },
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        AutomationProperties.SetName(_actions, "Действия расширенной подсказки");
        _richPanel.Children.Add(_actions);

        _primaryPanel.Children.Add(new TextBlock { Text = "Основное действие" });
        _primary = new TextBox { Text = "Опубликовать", HorizontalAlignment = HorizontalAlignment.Stretch };
        MaterialTextField.SetVariant(_primary, MaterialTextFieldVariant.Outlined);
        _primaryPanel.Children.Add(_primary);
        _richPanel.Children.Add(_primaryPanel);

        _secondaryPanel.Children.Add(new TextBlock { Text = "Второе действие" });
        _secondary = new TextBox { Text = "Отмена", HorizontalAlignment = HorizontalAlignment.Stretch };
        MaterialTextField.SetVariant(_secondary, MaterialTextFieldVariant.Outlined);
        _secondaryPanel.Children.Add(_secondary);
        _richPanel.Children.Add(_secondaryPanel);

        _showTitle = Toggle(_richPanel, "Показывать заголовок", true);
        _showActions = Toggle(_richPanel, "Показывать действия (ShowActions)", true);
        _showSecondary = Toggle(_richPanel, "Показывать второе действие", true);
        Settings.Children.Add(_richPanel);

        Settings.Children.Add(new TextBlock
        {
            Text = "Plain использует настоящий ToolTip: наведение, задержка, фокус с клавиатуры, Escape и " +
                   "долгое нажатие. Rich — RichTooltip: Flyout или ContextFlyout, заголовок, поддерживающий " +
                   "текст и действия; обе кнопки закрывают подсказку после команды и возвращают фокус владельцу.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
        });

        _kind.SelectionChanged += (_, _) => { if (!_syncing) Apply(); };
        _plainType.SelectionChanged += (_, _) => { if (!_syncing) Apply(); };
        _plainText.TextChanged += (_, _) => { if (!_syncing) Apply(); };
        _delay.PropertyChanged += (_, e) => { if (e.Property == RangeBase.ValueProperty && !_syncing) Apply(); };
        _actions.SelectionChanged += (_, _) => { if (!_syncing) Apply(); };
        _mode.SelectionChanged += (_, _) => { if (!_syncing) Apply(); };
        foreach (var box in new[] { _title, _body, _primary, _secondary })
            box.TextChanged += (_, _) => { if (!_syncing) Apply(); };
        foreach (var toggle in new[] { _longPress, _showOnDisabled, _ownerEnabled, _exposeHelp, _showTitle, _showActions, _showSecondary })
            toggle.IsCheckedChanged += (_, _) => { if (!_syncing) Apply(); };

        // Registered after the settings controls exist: the handler reads the open-mode choice.
        _richOwner.Click += (_, _) =>
        {
            if (_mode.SelectedIndex == 1 && _richOwner.ContextFlyout is { } flyout)
                flyout.ShowAt(_richOwner);
        };

        Apply();
    }

    private void Apply()
    {
        _syncing = true;
        CloseTooltips();

        var plain = _kind.SelectedIndex == 0;
        _plainPanel.IsVisible = plain;
        _richPanel.IsVisible = !plain;
        _delayCaption.Text = $"Задержка появления: {(int)Math.Round(_delay.Value)} мс";
        _primaryPanel.IsVisible = _actions.SelectedIndex == 0;
        _secondaryPanel.IsVisible = _actions.SelectedIndex == 0 && _showSecondary.IsChecked == true;
        _showSecondary.IsVisible = _actions.SelectedIndex == 0;

        if (plain)
        {
            ApplyPlain();
            _host.Content = _plainOwner;
            _hint.Text = "Наведите указатель или сфокусируйте кнопку (Tab). Escape закрывает подсказку.";
        }
        else
        {
            ApplyRich();
            _host.Content = _richOwner;
            _hint.Text = _mode.SelectedIndex == 0
                ? "Нажмите кнопку: откроется RichTooltip с действиями."
                : "Нажмите кнопку, правую кнопку мыши или клавишу контекстного меню.";
        }

        _syncing = false;
    }

    private void ApplyPlain()
    {
        var text = _plainText.Text ?? string.Empty;
        var type = _plainType.SelectedIndex == 1
            ? MaterialPlainToolTipType.MultiLine
            : MaterialPlainToolTipType.SingleLine;
        ToolTip.SetTip(_plainOwner, MaterialToolTip.CreateContent(text, type));
        ToolTip.SetShowDelay(_plainOwner, (int)Math.Round(_delay.Value));
        ToolTip.SetShowOnDisabled(_plainOwner, _showOnDisabled.IsChecked == true);
        MaterialToolTip.SetLongPressEnabled(_plainOwner, _longPress.IsChecked == true);
        MaterialToolTip.SetExposeHelpText(_plainOwner, _exposeHelp.IsChecked == true);
        _plainOwner.IsEnabled = _ownerEnabled.IsChecked == true;
    }

    private void ApplyRich()
    {
        _richOwner.Flyout = null;
        _richOwner.ContextFlyout = null;
        if (_mode.SelectedIndex != 1)
            _richOwner.Flyout = BuildRichTooltip();
        else
            _richOwner.ContextFlyout = BuildRichTooltip();
    }

    private RichTooltip BuildRichTooltip()
    {
        var tooltip = new RichTooltip
        {
            Title = _title.Text,
            Content = _body.Text,
            ShowTitle = _showTitle.IsChecked == true,
            ShowActions = _showActions.IsChecked == true,
        };

        switch (_actions.SelectedIndex)
        {
            case 0:
                tooltip.PrimaryActionText = _primary.Text;
                tooltip.SecondaryActionText = _secondary.Text;
                tooltip.ShowSecondaryAction = _showSecondary.IsChecked == true;
                tooltip.PrimaryActionCommand = new DelegateCommand(() =>
                    Report($"Основное действие «{_primary.Text}» выполнено, подсказка закрыта."));
                tooltip.SecondaryActionCommand = new DelegateCommand(() =>
                    Report($"Второе действие «{_secondary.Text}» выполнено, подсказка закрыта."));
                break;
            case 1:
                tooltip.Actions = BuildRatingActions(tooltip);
                break;
        }

        return tooltip;
    }

    private StackPanel BuildRatingActions(RichTooltip tooltip)
    {
        var stars = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        for (var index = 1; index <= 5; index++)
        {
            var value = index;
            var star = new Button { Content = $"{value}★", MinHeight = 40, Padding = new Thickness(12, 10) };
            MaterialAssist.SetButtonVariant(star, MaterialButtonVariant.Text);
            MaterialAssist.SetContainerInset(star, default);
            AutomationProperties.SetName(star, $"Оценка {value} из 5");
            star.Click += (_, _) =>
            {
                Report($"Оценка {value} из 5 сохранена, подсказка закрыта.");
                tooltip.Hide();
            };
            stars.Children.Add(star);
        }

        return stars;
    }

    private void CloseTooltips()
    {
        if (_plainOwner.GetValue(ToolTip.IsOpenProperty))
            ToolTip.SetIsOpen(_plainOwner, false);
        if (_richOwner.Flyout is PopupFlyoutBase { IsOpen: true } flyout)
            flyout.Hide();
        if (_richOwner.ContextFlyout is PopupFlyoutBase { IsOpen: true } context)
            context.Hide();
    }

    private void Report(string message) => _readout.Text = message;

    private TextBox TextField(string label, string text, bool acceptsReturn = false)
    {
        var box = new TextBox
        {
            Text = text,
            AcceptsReturn = acceptsReturn,
            TextWrapping = acceptsReturn ? TextWrapping.Wrap : TextWrapping.NoWrap,
            MinHeight = acceptsReturn ? 88 : 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        MaterialTextField.SetVariant(box, MaterialTextFieldVariant.Outlined);
        MaterialTextField.SetLabel(box, label);
        AutomationProperties.SetName(box, label);
        return box;
    }

    private static ToggleSwitch Toggle(StackPanel host, string label, bool value)
    {
        var toggle = new ToggleSwitch { Content = label, IsChecked = value };
        host.Children.Add(toggle);
        return toggle;
    }

    private MaterialButtonGroup Choice(string label, string[] options, int selected)
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = label });
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
        panel.Children.Add(group);
        Settings.Children.Add(panel);
        return group;
    }

    private sealed class DelegateCommand(Action execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => execute();
    }
}
