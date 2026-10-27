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
    private readonly ComboBox _variant = new() { ItemsSource = Enum.GetNames<MaterialCardVariant>(), SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ComboBox _anatomy = new() { ItemsSource = Anatomies, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
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
    private readonly StackPanel _slotContent;
    private int _clicks;
    private int _commands;
    private int _actions;
    private int _drags;
    public StackPanel Settings { get; } = new() { Spacing = 16 };

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
        var slotButton = new Button { Content = "Действие в слоте", HorizontalAlignment = HorizontalAlignment.Left };
        slotButton.Click += (_, _) => SetStatus("Нажато действие в слоте; карточка не активировалась.");
        _slotContent.Children.Add(slotButton);

        // N01.4 badge placement: dot/labeled and RTL mirroring of the source BadgedBox.
        var badgeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 32 };
        badgeRow.Children.Add(new TextBlock { Text = "Badge", VerticalAlignment = VerticalAlignment.Center });
        badgeRow.Children.Add(BadgeSample(FlowDirection.LeftToRight, "3"));
        badgeRow.Children.Add(BadgeSample(FlowDirection.RightToLeft, "12"));
        badgeRow.Children.Add(BadgeSample(FlowDirection.LeftToRight, null));
        _slotContent.Children.Add(badgeRow);

        Settings.Children.Add(Caption("Вид контейнера"));
        Settings.Children.Add(_variant);
        Settings.Children.Add(Caption("Анатомия"));
        Settings.Children.Add(_anatomy);
        _width = Number(Settings, "Ширина", 240, 520, 360, 8, "dp");
        _radius = Number(Settings, "Радиус", 0, 32, 12, 1, "dp");

        _header = Text(Settings, "Шапка", "Заголовок");
        _title = Text(Settings, "Заголовок", "Title");
        _supporting = Text(Settings, "Поддерживающий текст", "Короткое описание карточки.");

        _avatar = Toggle(Settings, "Аватар в шапке", true);
        _headerAction = Toggle(Settings, "Действие в шапке", false);
        _secondary = Toggle(Settings, "Вторичное действие", true);
        _interactive = Toggle(Settings, "Интерактивная карточка", false);
        _focus = Toggle(Settings, "Показывать фокусное кольцо", false);
        _drag = Toggle(Settings, "Перетаскивание (DragData)", false);
        _enabled = Toggle(Settings, "Доступна", true);

        Settings.Children.Add(new TextBlock
        {
            Text = "Интерактивная карточка получает фокус, состояния hover/pressed/focused/dragged, " +
                   "активацию Enter/Space и команду. Вложенные кнопки сохраняют собственный ввод. " +
                   "Layout=Slot заменяет анатомию слотом содержимого.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
        });

        AutomationProperties.SetName(_variant, "Вид карточки");
        AutomationProperties.SetName(_anatomy, "Анатомия карточки");
        AutomationProperties.SetName(_header, "Шапка карточки");
        AutomationProperties.SetName(_title, "Заголовок карточки");
        AutomationProperties.SetName(_supporting, "Поддерживающий текст карточки");

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
        _card.Variant = (MaterialCardVariant)Math.Clamp(_variant.SelectedIndex, 0, 2);
        _card.Orientation = anatomy == 1 ? MaterialCardOrientation.Horizontal : MaterialCardOrientation.Stacked;
        _card.Layout = slot ? MaterialCardLayout.Slot : MaterialCardLayout.MediaText;
        _card.Width = _width.Value;
        _card.CornerRadius = new CornerRadius(_radius.Value);
        _card.Padding = slot ? new Thickness(16) : new Thickness(0);
        _card.Content = slot ? _slotContent : null;

        _card.Header = _header.Text;
        _card.Subhead = "Подзаголовок";
        _card.AvatarInitial = "A";
        _card.ShowAvatar = _avatar.IsChecked == true;
        _card.HeaderAction = _headerAction.IsChecked == true ? _headerButton : null;
        _card.Title = _title.Text;
        _card.Subtitle = anatomy == 1 ? null : "Subtitle";
        _card.SupportingText = _supporting.Text;
        _card.Media = slot ? null : Media(anatomy == 1 ? null : "Медиа 188");
        _card.PrimaryAction = slot ? null : "Основное";
        _card.SecondaryAction = slot ? null : "Второе";
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
        _card.IsInteractive = _interactive.IsChecked == true;
        _card.ShowFocusIndicator = _focus.IsChecked == true;
        _card.DragData = _interactive.IsChecked == true && _drag.IsChecked == true
            ? TextTransfer("Material card")
            : null;
        _card.IsEnabled = _enabled.IsChecked == true;
        _drag.IsEnabled = _interactive.IsChecked == true;
        _focus.IsEnabled = _interactive.IsChecked == true;

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

    private static TextBlock Caption(string text) => new() { Text = text };

    private static ToggleSwitch Toggle(StackPanel host, string label, bool value)
    {
        var toggle = new ToggleSwitch { Content = label, IsChecked = value };
        host.Children.Add(toggle);
        return toggle;
    }

    private static TextBox Text(StackPanel host, string label, string value)
    {
        host.Children.Add(Caption(label));
        var box = new TextBox { Text = value, HorizontalAlignment = HorizontalAlignment.Stretch };
        MaterialTextField.SetVariant(box, MaterialTextFieldVariant.Outlined);
        MaterialTextField.SetLabel(box, label);
        host.Children.Add(box);
        return box;
    }

    private static Slider Number(StackPanel host, string label, double min, double max, double value, double step, string unit)
    {
        var caption = new TextBlock { Text = $"{label}: {value:0} {unit}" };
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
        slider.PropertyChanged += (_, e) =>
        {
            if (e.Property == RangeBase.ValueProperty) caption.Text = $"{label}: {slider.Value:0} {unit}";
        };
        AutomationProperties.SetName(slider, label);
        host.Children.Add(slider);
        return slider;
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
