using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Catalog;

/// <summary>One live card with its family, anatomy, actions and dimensions exposed in the inspector.</summary>
public static class CardsGallery
{
    public static Control Create() => new CardsPlayground();
}

internal sealed class CardsPlayground : Grid, IComponentPlayground
{
    private static readonly string[] Anatomies = { "Stacked", "Horizontal", "Slot" };

    private readonly MaterialCard _card = new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
    };
    private readonly TextBlock _status = new()
    {
        Text = "Нажатий: 0 · команд карточки: 0 · команд действий: 0 · перетаскиваний: 0",
        HorizontalAlignment = HorizontalAlignment.Center,
        TextWrapping = TextWrapping.Wrap,
    };
    private readonly ComboBox _variant;
    private readonly ComboBox _anatomy;
    private readonly Slider _width;
    private readonly Slider _radius;
    private readonly ToggleSwitch _avatar;
    private readonly ToggleSwitch _headerAction;
    private readonly ToggleSwitch _secondary;
    private readonly ToggleSwitch _interactive;
    private readonly ToggleSwitch _focus;
    private readonly ToggleSwitch _drag;
    private readonly ToggleSwitch _enabled;
    private readonly TextBox _header;
    private readonly TextBox _title;
    private readonly TextBox _supporting;
    private readonly Button _headerButton;
    private readonly Button _slotButton;
    private readonly StackPanel _slotContent;
    private int _clicks;
    private int _commands;
    private int _actions;
    private int _drags;
    public StackPanel Settings { get; } = InspectorLayout.CreatePane();

    internal CardsPlayground()
    {
        RowDefinitions = new RowDefinitions("*,Auto");
        var stage = new StackPanel
        {
            Spacing = 24,
            Margin = new Thickness(24),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        stage.Children.Add(_card);
        Children.Add(stage);
        Grid.SetRow(_status, 1);
        _status.Margin = new Thickness(24, 0, 24, 20);
        Children.Add(_status);

        _headerButton = new Button
        {
            Content = new MaterialIcon { IconName = "more_vert", IsDecorative = true },
            Width = 48,
            Height = 48,
        };
        MaterialAssist.SetIsIconButton(_headerButton, true);
        MaterialAssist.SetButtonVariant(_headerButton, MaterialButtonVariant.Standard);
        AutomationProperties.SetName(_headerButton, "Дополнительно");
        _headerButton.Click += (_, _) => SetStatus("Нажато действие в шапке.");

        _slotContent = new StackPanel { Spacing = 12 };
        _slotContent.Children.Add(new TextBlock { Text = "Слот содержимого", FontSize = 16, FontWeight = FontWeight.Medium });
        _slotContent.Children.Add(new TextBlock
        {
            Text = "Анатомия скрыта: содержимое задаёт всю поверхность карточки.",
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap,
        });
        _slotButton = new Button { Content = "Действие в слоте", HorizontalAlignment = HorizontalAlignment.Left };
        _slotButton.Click += (_, _) => SetStatus("Нажато действие в слоте; карточка не активировалась.");
        _slotContent.Children.Add(_slotButton);

        // N01.4 badge placement: dot/labeled and RTL mirroring of the source BadgedBox.
        var badgeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 32 };
        badgeRow.Children.Add(new TextBlock { Text = "Badge", VerticalAlignment = VerticalAlignment.Center });
        badgeRow.Children.Add(BadgeSample(FlowDirection.LeftToRight, "3"));
        badgeRow.Children.Add(BadgeSample(FlowDirection.RightToLeft, "12"));
        badgeRow.Children.Add(BadgeSample(FlowDirection.LeftToRight, null));
        _slotContent.Children.Add(badgeRow);

        var containerSection = InspectorLayout.Section(Settings, "Контейнер", divider: false);
        _variant = InspectorLayout.Combo(containerSection, "Вид контейнера", Enum.GetNames<MaterialCardVariant>(), 0);
        AutomationProperties.SetName(_variant, "Вид карточки");
        _anatomy = InspectorLayout.Combo(containerSection, "Анатомия", Anatomies, 0);
        AutomationProperties.SetName(_anatomy, "Анатомия карточки");
        _width = InspectorLayout.Slider(containerSection, "Ширина", 240, 520, 360, 8, "dp", "0");
        _radius = InspectorLayout.Slider(containerSection, "Радиус", 0, 32, 12, 1, "dp", "0");

        var contentSection = InspectorLayout.Section(Settings, "Содержимое");
        _header = InspectorLayout.TextField(contentSection, "Шапка", "Заголовок");
        AutomationProperties.SetName(_header, "Шапка карточки");
        _title = InspectorLayout.TextField(contentSection, "Заголовок", "Title");
        AutomationProperties.SetName(_title, "Заголовок карточки");
        _supporting = InspectorLayout.TextField(contentSection, "Поддерживающий текст", "Короткое описание карточки.");
        AutomationProperties.SetName(_supporting, "Поддерживающий текст карточки");

        var behaviorSection = InspectorLayout.Section(Settings, "Поведение и состояния");
        var switches = InspectorLayout.Switches(behaviorSection);
        _avatar = InspectorLayout.Switch(switches, "Аватар в шапке", true);
        _headerAction = InspectorLayout.Switch(switches, "Действие в шапке", false);
        _secondary = InspectorLayout.Switch(switches, "Вторичное действие", true);
        _interactive = InspectorLayout.Switch(switches, "Интерактивная карточка", false);
        _focus = InspectorLayout.Switch(switches, "Показывать фокусное кольцо", false);
        _drag = InspectorLayout.Switch(switches, "Перетаскивание (DragData)", false);
        _enabled = InspectorLayout.Switch(switches, "Доступна", true);

        InspectorLayout.Note(
            Settings,
            "Интерактивная карточка получает фокус, состояния hover/pressed/focused/dragged, " +
            "активацию Enter/Space и команду без вложенных кнопок действий (по гайдлайнам M3). " +
            "В неинтерактивном режиме карточка служит контейнером для самостоятельных кнопок. " +
            "Layout=Slot заменяет анатомию слотом содержимого.");

        _variant.SelectionChanged += (_, _) => Apply();
        _anatomy.SelectionChanged += (_, _) => Apply();
        foreach (var slider in new[] { _width, _radius })
            slider.PropertyChanged += (_, e) => { if (e.Property == RangeBase.ValueProperty) Apply(); };
        foreach (var toggle in new[] { _avatar, _headerAction, _secondary, _interactive, _focus, _drag, _enabled })
            toggle.IsCheckedChanged += (_, _) => Apply();
        foreach (var text in new[] { _header, _title, _supporting })
            text.TextChanged += (_, _) => Apply();

        _card.Click += (_, _) =>
        {
            _clicks++;
            UpdateStatus();
        };
        _card.Command = new DelegateCommand(_ =>
        {
            _commands++;
            UpdateStatus();
        });
        _card.DragStarted += (_, _) => SetStatus("Перетаскивание началось.");
        _card.DragCompleted += (_, e) =>
        {
            _drags++;
            UpdateStatus($"Перетаскиваний: {_drags} · результат {e.Effects}");
        };

        Apply();
    }

    private void Apply()
    {
        var anatomy = _anatomy.SelectedIndex;
        var slot = anatomy == 2;
        var interactive = _interactive.IsChecked == true;
        _card.Variant = (MaterialCardVariant)Math.Clamp(_variant.SelectedIndex, 0, 2);
        _card.Orientation = anatomy == 1 ? MaterialCardOrientation.Horizontal : MaterialCardOrientation.Stacked;
        _card.Layout = slot ? MaterialCardLayout.Slot : MaterialCardLayout.MediaText;
        _card.Width = _width.Value;
        _card.CornerRadius = new CornerRadius(_radius.Value);
        _card.Padding = slot ? new Thickness(16) : new Thickness(0);
        _card.Content = slot ? _slotContent : null;
        _slotButton.IsVisible = !interactive;

        _card.Header = _header.Text;
        _card.Subhead = "Подзаголовок";
        _card.AvatarInitial = "A";
        _card.ShowAvatar = _avatar.IsChecked == true;
        _card.HeaderAction = !interactive && _headerAction.IsChecked == true ? _headerButton : null;
        _card.Title = _title.Text;
        _card.Subtitle = anatomy == 1 ? null : "Subtitle";
        _card.SupportingText = _supporting.Text;
        _card.Media = slot ? null : Media(anatomy == 1 ? null : "Медиа 188");
        _card.PrimaryAction = slot || interactive ? null : "Основное";
        _card.SecondaryAction = slot || interactive ? null : "Второе";
        _card.ShowSecondaryAction = _secondary.IsChecked == true;
        _card.PrimaryActionCommand = new DelegateCommand(_ =>
        {
            _actions++;
            UpdateStatus();
        });
        _card.SecondaryActionCommand = new DelegateCommand(_ =>
        {
            _actions++;
            UpdateStatus();
        });
        _card.IsInteractive = interactive;
        _card.ShowFocusIndicator = _focus.IsChecked == true;
        _card.DragData = interactive && _drag.IsChecked == true
            ? TextTransfer("Material card")
            : null;
        _card.IsEnabled = _enabled.IsChecked == true;
        _headerAction.IsEnabled = !slot && !interactive;
        _secondary.IsEnabled = !slot && !interactive;
        _drag.IsEnabled = interactive;
        _focus.IsEnabled = interactive;

        var name = slot ? "Slot" : anatomy == 1 ? "Horizontal card" : "Stacked card";
        AutomationProperties.SetName(_card, name);
    }

    private void UpdateStatus(string? prefix = null)
    {
        _status.Text = prefix ?? string.Empty;
        if (_status.Text.Length > 0) _status.Text += " · ";
        _status.Text += $"Нажатий: {_clicks} · команд карточки: {_commands} · команд действий: {_actions} · перетаскиваний: {_drags}";
    }

    private void SetStatus(string text) => _status.Text = text;

    private static Control Media(string? caption)
    {
        var panel = new Panel();
        panel.Children.Add(new Border
        {
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.Parse("#6750A4"), 0),
                    new GradientStop(Color.Parse("#9A82DB"), 1),
                },
            },
        });
        panel.Children.Add(new MaterialIcon
        {
            IconName = "sticker",
            Width = 48,
            Height = 48,
            IsDecorative = true,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        });
        if (caption is not null)
        {
            panel.Children.Add(new TextBlock
            {
                Text = caption,
                Foreground = Brushes.White,
                FontSize = 12,
                Margin = new Thickness(12),
                VerticalAlignment = VerticalAlignment.Bottom,
            });
        }
        return new Border { Child = panel };
    }

    private static IDataTransfer TextTransfer(string text)
    {
        var transfer = new DataTransfer();
        transfer.Add(DataTransferItem.CreateText(text));
        return transfer;
    }

    private static Control BadgeSample(FlowDirection direction, string? text)
    {
        var anchor = new Border
        {
            Width = 40,
            Height = 40,
            CornerRadius = new CornerRadius(12),
            Background = new SolidColorBrush(Color.Parse("#FF6750A4")),
        };
        var box = new MaterialBadgedBox
        {
            FlowDirection = direction,
            Content = anchor,
            Badge = new MaterialBadge { Text = text },
        };
        AutomationProperties.SetName(box, text is null ? "Badge dot" : $"Badge {text}");
        return box;
    }

    private sealed class DelegateCommand : ICommand
    {
        private readonly Action<object?> _execute;

        public DelegateCommand(Action<object?> execute) => _execute = execute;

        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => _execute(parameter);
    }
}
