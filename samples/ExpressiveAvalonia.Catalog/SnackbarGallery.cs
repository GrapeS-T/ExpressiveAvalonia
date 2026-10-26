using System;
using System.Threading;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Catalog;

/// <summary>
/// One live <see cref="MaterialSnackbar"/> queued by an embedded <see cref="MaterialSnackbarHost"/>.
/// </summary>
/// <remarks>
/// The inspector edits the message, the action label, the line count, the action placement, the
/// close affordance and the duration of the single snackbar; changes reach a snackbar that is
/// already on screen. The demo buttons exercise the real host queue, the programmatic dismissal
/// and the window overlay host.
/// </remarks>
public static class SnackbarGallery
{
    public static Control Create() => new SnackbarPlayground();
}

internal sealed class SnackbarPlayground : Grid, IComponentPlayground
{
    private const string ShortText = "Snackbar supporting text";

    private readonly MaterialSnackbarHost _host = new();
    private readonly Border _surface;
    private readonly TextBlock _status = new()
    {
        Text = "Snackbar ещё не показывался.",
        TextWrapping = TextWrapping.Wrap,
        TextAlignment = TextAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Center,
        Opacity = 0.8,
    };

    private readonly TextBox _message;
    private readonly TextBox _action;
    private readonly MaterialButtonGroup _lines;
    private readonly MaterialButtonGroup _placement;
    private readonly StackPanel _placementGroup;
    private readonly ToggleSwitch _close;
    private readonly ComboBox _duration;
    private MaterialSnackbar? _open;

    public StackPanel Settings { get; } = new() { Spacing = 16 };

    internal SnackbarPlayground()
    {
        var hint = new TextBlock
        {
            Text = "Snackbar появляется внизу этой области.",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.6,
        };
        hint.Bind(TextBlock.ForegroundProperty, hint.GetResourceObservable("MaterialOnSurfaceVariantBrush"));

        _surface = new Border
        {
            Height = 320,
            MinWidth = 360,
            MaxWidth = 640,
            CornerRadius = new CornerRadius(16),
            ClipToBounds = true,
        };
        _surface.Bind(Border.BackgroundProperty, _surface.GetResourceObservable("MaterialSurfaceContainerBrush"));
        var surface = new Grid { Children = { hint, _host } };
        _surface.Child = surface;

        var show = new Button { Content = "Показать snackbar" };
        MaterialAssist.SetButtonVariant(show, MaterialButtonVariant.Filled);
        show.Click += (_, _) => ShowEmbedded();

        var dismiss = new Button { Content = "Закрыть" };
        MaterialAssist.SetButtonVariant(dismiss, MaterialButtonVariant.Text);
        dismiss.Click += (_, _) => _host.DismissCurrent();

        var overlay = new Button { Content = "В оверлее окна" };
        MaterialAssist.SetButtonVariant(overlay, MaterialButtonVariant.Outlined);
        overlay.Click += (_, _) => ShowInOverlay(overlay);

        var buttons = new WrapPanel
        {
            ItemSpacing = 8,
            LineSpacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children = { show, dismiss, overlay },
        };

        var stage = new StackPanel
        {
            Spacing = 16,
            MaxWidth = 720,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _surface, buttons, _status },
        };
        Children.Add(stage);

        _message = new TextBox
        {
            Text = ShortText,
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = true,
            MinHeight = 88,
        };
        MaterialTextField.SetVariant(_message, MaterialTextFieldVariant.Outlined);
        MaterialTextField.SetLabel(_message, "Текст сообщения");
        AutomationProperties.SetName(_message, "Текст сообщения");
        Settings.Children.Add(_message);

        _action = new TextBox { Text = "Отменить" };
        MaterialTextField.SetVariant(_action, MaterialTextFieldVariant.Outlined);
        MaterialTextField.SetLabel(_action, "Действие (пусто — без действия)");
        AutomationProperties.SetName(_action, "Текст действия");
        Settings.Children.Add(_action);

        _lines = Choice(Settings, "Строк текста", ["1 строка", "2 строки"]);
        _placementGroup = new StackPanel { Spacing = 6 };
        _placementGroup.Children.Add(new TextBlock { Text = "Размещение действия" });
        _placement = Group(_placementGroup, ["В строке текста", "Под текстом"]);
        Settings.Children.Add(_placementGroup);

        _close = new ToggleSwitch { Content = "Кнопка закрытия", IsChecked = true };
        Settings.Children.Add(_close);

        Settings.Children.Add(new TextBlock { Text = "Длительность" });
        _duration = new ComboBox
        {
            ItemsSource = new[] { "По умолчанию", "4 секунды", "10 секунд", "Бесконечно" },
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        AutomationProperties.SetName(_duration, "Длительность показа");
        Settings.Children.Add(_duration);

        Settings.Children.Add(new TextBlock
        {
            Text = "Escape закрывает snackbar с клавиатурным фокусом, а автоматическое закрытие " +
                   "приостанавливается, пока фокус внутри. Несколько вызовов встают в очередь host.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
        });

        _message.TextChanged += (_, _) => Apply();
        _action.TextChanged += (_, _) => Apply();
        _lines.SelectionChanged += (_, _) => Apply();
        _placement.SelectionChanged += (_, _) => Apply();
        _close.IsCheckedChanged += (_, _) => Apply();
        _duration.SelectionChanged += (_, _) => Apply();
        Apply();
    }

    private void Apply()
    {
        var hasAction = !string.IsNullOrWhiteSpace(_action.Text);
        _placementGroup.IsVisible = hasAction;

        if (_open is not { IsOpen: true } snackbar)
            return;

        snackbar.Text = _message.Text;
        snackbar.ActionContent = hasAction ? _action.Text : null;
        snackbar.LineCount = Lines();
        snackbar.ActionPlacement = Placement();
        snackbar.IsCloseButtonVisible = _close.IsChecked == true;
        snackbar.Duration = Duration();
    }

    private MaterialSnackbarLineCount Lines() =>
        _lines.SelectedIndex == 1 ? MaterialSnackbarLineCount.TwoLines : MaterialSnackbarLineCount.OneLine;

    private MaterialSnackbarActionPlacement Placement() =>
        _placement.SelectedIndex == 1 ? MaterialSnackbarActionPlacement.Below : MaterialSnackbarActionPlacement.Inline;

    private TimeSpan? Duration() => _duration.SelectedIndex switch
    {
        1 => TimeSpan.FromSeconds(4),
        2 => TimeSpan.FromSeconds(10),
        3 => Timeout.InfiniteTimeSpan,
        _ => null,
    };

    private MaterialSnackbar Build()
    {
        var hasAction = !string.IsNullOrWhiteSpace(_action.Text);
        return new MaterialSnackbar
        {
            Text = _message.Text,
            ActionContent = hasAction ? _action.Text : null,
            LineCount = Lines(),
            ActionPlacement = Placement(),
            IsCloseButtonVisible = _close.IsChecked == true,
            Duration = Duration(),
            Width = 344,
        };
    }

    private void ShowEmbedded()
    {
        var snackbar = Build();
        Track(snackbar);
        _open = snackbar;
        _ = _host.Show(snackbar);
        _status.Text = "Показан во встроенном host.";
    }

    private void ShowInOverlay(Control anchor)
    {
        if (TopLevel.GetTopLevel(anchor) is not { } topLevel)
        {
            _status.Text = "Окно ещё не создано; попробуйте после открытия страницы.";
            return;
        }

        var snackbar = Build();
        Track(snackbar);
        try
        {
            _ = MaterialSnackbarHost.Show(topLevel, snackbar);
            _status.Text = "Показан в оверлее окна.";
        }
        catch (InvalidOperationException exception)
        {
            _status.Text = exception.Message;
        }
    }

    private void Track(MaterialSnackbar snackbar)
    {
        snackbar.ActionInvoked += (_, _) => _status.Text = $"Действие «{snackbar.ActionContent}» выполнено.";
        snackbar.Closed += (_, e) =>
        {
            if (ReferenceEquals(_open, snackbar))
                _open = null;
            _status.Text = $"Snackbar закрыт: {Describe(e.Reason)}.";
        };
    }

    private static string Describe(MaterialSnackbarCloseReason reason) => reason switch
    {
        MaterialSnackbarCloseReason.Timeout => "истекло время",
        MaterialSnackbarCloseReason.UserDismiss => "закрыт пользователем",
        MaterialSnackbarCloseReason.Action => "после действия",
        _ => "программно",
    };

    private static MaterialButtonGroup Choice(StackPanel target, string label, string[] options)
    {
        target.Children.Add(new TextBlock { Text = label });
        var group = Group(target, options);
        return group;
    }

    private static MaterialButtonGroup Group(StackPanel target, string[] options)
    {
        var group = new MaterialButtonGroup
        {
            Kind = MaterialButtonGroupKind.Connected,
            SelectionMode = MaterialButtonGroupSelectionMode.Single,
            ShowSelectedIcon = true,
            ControlSize = MaterialControlSize.Small,
        };
        foreach (var option in options)
            group.Items.Add(new MaterialButtonGroupItem { Content = option });
        group.SelectedIndex = 0;
        target.Children.Add(group);
        return group;
    }
}
