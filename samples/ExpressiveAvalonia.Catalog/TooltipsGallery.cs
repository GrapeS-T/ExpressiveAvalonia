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
    private readonly ComboBox _placement;
    private readonly StackPanel _plainPanel;
    private readonly ComboBox _plainType;
    private readonly TextBox _plainText;
    private readonly Slider _delay;
    private readonly ToggleSwitch _longPress;
    private readonly ToggleSwitch _showOnDisabled;
    private readonly ToggleSwitch _ownerEnabled;
    private readonly ToggleSwitch _exposeHelp;
    private readonly ToggleSwitch _plainCaret;
    private readonly StackPanel _richPanel;
    private readonly ComboBox _mode;
    private readonly TextBox _title;
    private readonly TextBox _body;
    private readonly ComboBox _actions;
    private readonly TextBox _primary;
    private readonly TextBox _secondary;
    private readonly ComboBox _persistence;
    private readonly ToggleSwitch _showTitle;
    private readonly ToggleSwitch _showActions;
    private readonly ToggleSwitch _showSecondary;
    private readonly ToggleSwitch _richCaret;
    private bool _syncing;

    public StackPanel Settings { get; } = InspectorLayout.CreatePane();

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

        var typeSection = InspectorLayout.Section(Settings, "Тип и размещение", divider: false);
        _kind = InspectorLayout.Choice(typeSection, "Тип подсказки", new[] { "Plain", "Rich" }, 0);
        _placement = InspectorLayout.Combo(
            typeSection,
            "Размещение (Placement)",
            new[] { "По умолчанию (Auto)", "Top", "Bottom", "Left", "Right" },
            0);
        AutomationProperties.SetName(_placement, "Размещение подсказки");

        _plainPanel = InspectorLayout.Section(Settings, "Обычная подсказка (Plain)");
        _plainType = InspectorLayout.Combo(
            _plainPanel,
            "Макет",
            new[] { "Single line (200 DIP)", "Multi line (200 DIP)" },
            0);
        AutomationProperties.SetName(_plainType, "Макет обычной подсказки");
        _plainText = InspectorLayout.TextField(_plainPanel, "Текст подсказки", LongText, multiline: true);
        _delay = InspectorLayout.Slider(_plainPanel, "Задержка появления", 0, 2000, 500, step: 100, unit: "мс", format: "0");

        var plainSwitches = InspectorLayout.Switches(_plainPanel);
        _longPress = InspectorLayout.Switch(plainSwitches, "Открывать долгим нажатием (500 мс)", true);
        _showOnDisabled = InspectorLayout.Switch(plainSwitches, "Показывать у недоступного владельца", false);
        _ownerEnabled = InspectorLayout.Switch(plainSwitches, "Владелец доступен", true);
        _exposeHelp = InspectorLayout.Switch(plainSwitches, "Текст помощи для скринридера", true);
        _plainCaret = InspectorLayout.Switch(plainSwitches, "Карат 16×8 (caretShape)", false);

        _richPanel = InspectorLayout.Section(Settings, "Расширенная подсказка (Rich)");
        _mode = InspectorLayout.Combo(
            _richPanel,
            "Открытие",
            new[] { "Кнопка с Flyout", "Context flyout + ShowAt" },
            0);
        AutomationProperties.SetName(_mode, "Способ открытия расширенной подсказки");
        _title = InspectorLayout.TextField(_richPanel, "Заголовок (Title)", "Опубликовать изменения");
        _body = InspectorLayout.TextField(
            _richPanel,
            "Текст (Content)",
            "Публикация делает ревизию видимой всем, у кого есть доступ к библиотеке.",
            multiline: true);

        _actions = InspectorLayout.Combo(
            _richPanel,
            "Действия",
            new[] { "Две кнопки", "Свой набор (оценка)", "Без действий" },
            0);
        AutomationProperties.SetName(_actions, "Действия расширенной подсказки");

        _primary = InspectorLayout.TextField(_richPanel, "Основное действие", "Опубликовать");
        _secondary = InspectorLayout.TextField(_richPanel, "Второе действие", "Отмена");

        _persistence = InspectorLayout.Combo(
            _richPanel,
            "Удержание (IsPersistent)",
            new[]
            {
                "По умолчанию (от действий)",
                "Постоянная (IsPersistent = true)",
                "Временная 1.5 с (IsPersistent = false)",
            },
            0);
        AutomationProperties.SetName(_persistence, "Удержание расширенной подсказки");

        var richSwitches = InspectorLayout.Switches(_richPanel);
        _showTitle = InspectorLayout.Switch(richSwitches, "Показывать заголовок", true);
        _showActions = InspectorLayout.Switch(richSwitches, "Показывать действия (ShowActions)", true);
        _showSecondary = InspectorLayout.Switch(richSwitches, "Показывать второе действие", true);
        _richCaret = InspectorLayout.Switch(richSwitches, "Карат 16×8 (caretShape)", false);

        InspectorLayout.Note(
            Settings,
            "Plain использует настоящий ToolTip: наведение, задержка, фокус с клавиатуры, Escape и " +
            "долгое нажатие. Rich — RichTooltip: Flyout или ContextFlyout, заголовок, поддерживающий " +
            "текст и действия; обе кнопки закрывают подсказку после команды и возвращают фокус владельцу.");

        _kind.SelectionChanged += (_, _) => { if (!_syncing) Apply(); };
        _placement.SelectionChanged += (_, _) => { if (!_syncing) Apply(); };
        _plainType.SelectionChanged += (_, _) => { if (!_syncing) Apply(); };
        _plainText.TextChanged += (_, _) => { if (!_syncing) Apply(); };
        _delay.PropertyChanged += (_, e) => { if (e.Property == RangeBase.ValueProperty && !_syncing) Apply(); };
        _actions.SelectionChanged += (_, _) => { if (!_syncing) Apply(); };
        _persistence.SelectionChanged += (_, _) => { if (!_syncing) Apply(); };
        _mode.SelectionChanged += (_, _) => { if (!_syncing) Apply(); };
        foreach (var box in new[] { _title, _body, _primary, _secondary })
            box.TextChanged += (_, _) => { if (!_syncing) Apply(); };
        foreach (var toggle in new[] { _longPress, _showOnDisabled, _ownerEnabled, _exposeHelp, _plainCaret, _showTitle, _showActions, _showSecondary, _richCaret })
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
        _primary.IsVisible = _actions.SelectedIndex == 0;
        _secondary.IsVisible = _actions.SelectedIndex == 0 && _showSecondary.IsChecked == true;
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
        if (ResolvePlacement() is { } explicitPlacement)
            ToolTip.SetPlacement(_plainOwner, explicitPlacement);
        else
            _plainOwner.ClearValue(ToolTip.PlacementProperty);
        MaterialToolTip.SetShowCaret(_plainOwner, _plainCaret.IsChecked == true);
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
            Placement = ResolvePlacement() ?? PlacementMode.Bottom,
            ShowTitle = _showTitle.IsChecked == true,
            ShowActions = _showActions.IsChecked == true,
            ShowCaret = _richCaret.IsChecked == true,
        };

        switch (_persistence.SelectedIndex)
        {
            case 1:
                tooltip.IsPersistent = true;
                break;
            case 2:
                tooltip.IsPersistent = false;
                break;
        }

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

    private PlacementMode? ResolvePlacement() => _placement.SelectedIndex switch
    {
        1 => PlacementMode.Top,
        2 => PlacementMode.Bottom,
        3 => PlacementMode.Left,
        4 => PlacementMode.Right,
        _ => null,
    };

    private StackPanel BuildRatingActions(RichTooltip tooltip)
    {
        var stars = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, ClipToBounds = false };
        for (var index = 1; index <= 5; index++)
        {
            var value = index;
            var star = new Button
            {
                Content = $"{value}★",
                MinWidth = 48,
                MinHeight = 48,
                Margin = new Thickness(0, -6, 0, -6),
                Padding = new Thickness(8, 8),
                ClipToBounds = false,
            };
            MaterialAssist.SetButtonVariant(star, MaterialButtonVariant.Text);
            MaterialAssist.SetContainerInset(star, new Thickness(0, 6));
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

    private sealed class DelegateCommand(Action execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => execute();
    }
}
