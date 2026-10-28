using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Catalog;

/// <summary>A live dialog with its family, content and actions exposed in the inspector.</summary>
public static class DialogsGallery
{
    public static Control Create() => new DialogPlayground();
}

/// <summary>
/// Live dialog coverage for N06 (F05 shared lifecycle). Source mapping: AlertDialog.kt slots,
/// icon/title/supporting-text/action anatomy, 280-560 DIP width, wrapping action flow, action
/// result/cancel, scrim and Escape dismissal, focus trap/restore and the desktop fade/scale
/// composition around the platform-window animation the source leaves to the host.
/// </summary>
internal sealed class DialogPlayground : Grid, IComponentPlayground
{
    private enum DialogFamily { Basic, Icon, List, ScrollableList, Input, NonDismissible, FullScreen }

    private readonly MaterialDialog _preview;
    private readonly MaterialDialogList _list = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBox _field = new() { PlaceholderText = "Введите имя", Margin = new Thickness(0) };
    private readonly Control _dialogIcon = MaterialDialog.CreateIcon();
    private readonly MaterialDialogAction _primaryAction;
    private readonly MaterialDialogAction _secondaryAction;
    private readonly TextBlock _readout = new()
    {
        Text = "Диалог показан встроенно. «Показать модально» открывает настоящий модальный экземпляр: " +
               "Escape и клик по scrim закрывают его, Enter активирует действие по умолчанию, " +
               "фокус возвращается на кнопку.",
        TextWrapping = TextWrapping.Wrap,
        MaxWidth = 640,
        HorizontalAlignment = HorizontalAlignment.Center,
        FontSize = 13,
    };
    private readonly Button _show = new() { HorizontalAlignment = HorizontalAlignment.Center };
    private readonly ComboBox _family;
    private readonly TextBox _title;
    private readonly TextBox _supporting;
    private readonly TextBox _primaryText;
    private readonly TextBox _secondaryText;
    private readonly ToggleSwitch _icon;
    private readonly ToggleSwitch _divider;
    private readonly ToggleSwitch _threeLine;
    private readonly ToggleSwitch _scrollable;
    private readonly ToggleSwitch _dismissible;
    private readonly ToggleSwitch _scrim;
    private readonly Slider _rows;
    private readonly Slider _width;
    private readonly StackPanel _rowsSettings = new() { Spacing = 16 };
    private readonly StackPanel _threeLineSettings = new() { Spacing = 16 };
    private readonly StackPanel _scrollableSettings = new() { Spacing = 16 };
    private readonly StackPanel _dismissibleSettings = new() { Spacing = 16 };
    private readonly StackPanel _scrimSettings = new() { Spacing = 16 };
    private bool _built;
    private bool _syncing;

    public StackPanel Settings { get; } = new() { Spacing = 16 };

    internal DialogPlayground()
    {
        MaterialTextField.SetVariant(_field, MaterialTextFieldVariant.Outlined);
        MaterialTextField.SetLabel(_field, "Имя");
        AutomationProperties.SetName(_field, "Имя");

        // The command captures the action instance, so it is created first and the command is
        // attached afterwards; a lambda inside the initializer would read the field before its
        // own assignment and the compiler flags the dereference as possibly null.
        var secondaryAction = new MaterialDialogAction("Отмена", "cancelled") { IsCancel = true };
        secondaryAction.Command = new DelegateCommand(() =>
            _readout.Text = $"Вызвано «{secondaryAction.Text}» · результат 'cancelled'");
        _secondaryAction = secondaryAction;

        var primaryAction = new MaterialDialogAction("Сохранить", "saved") { IsDefault = true };
        primaryAction.Command = new DelegateCommand(() =>
            _readout.Text = CurrentFamily == DialogFamily.Input
                ? $"Сохранено «{_field.Text}»"
                : $"Вызвано «{primaryAction.Text}» · результат 'saved'");
        _primaryAction = primaryAction;
        _preview = new MaterialDialog
        {
            Width = 312,
            HorizontalAlignment = HorizontalAlignment.Center,
            Actions = new AvaloniaList<MaterialDialogAction> { _secondaryAction, _primaryAction },
        };
        AutomationProperties.SetName(_preview, "Предпросмотр диалога");

        MaterialAssist.SetButtonVariant(_show, MaterialButtonVariant.Filled);
        MaterialAssist.SetControlSize(_show, MaterialControlSize.Small);
        _show.Content = "Показать модально";
        AutomationProperties.SetName(_show, "Показать модальный диалог");
        _show.Click += (_, _) => ShowModal();

        var stage = new StackPanel
        {
            Spacing = 20,
            Margin = new Thickness(24),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        stage.Children.Add(_preview);
        stage.Children.Add(_show);
        stage.Children.Add(_readout);
        Children.Add(new ScrollViewer
        {
            Content = stage,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        });

        _family = Field(Settings, "Семейство", new ComboBox
        {
            ItemsSource = new[]
            {
                "Базовый", "С иконкой", "Список", "Прокручиваемый список", "Поле ввода", "Без закрытия", "Полный экран",
            },
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        });

        _title = TextField(Settings, "Заголовок", "Заголовок диалога");
        _supporting = TextField(Settings, "Текст поддержки",
            "Диалог появляется поверх содержимого приложения и требует решения или подтверждения.");
        _icon = Toggle(Settings, "Иконка", false);
        _divider = Toggle(Settings, "Разделитель", false);

        _rows = Number(_rowsSettings, "Строк в списке", 1, 12, 3, 1);
        Settings.Children.Add(_rowsSettings);

        _threeLine = Toggle(_threeLineSettings, "Трёхстрочные строки", false);
        Settings.Children.Add(_threeLineSettings);

        _scrollable = Toggle(_scrollableSettings, "Прокручиваемый список", false);
        Settings.Children.Add(_scrollableSettings);

        _width = Number(Settings, "Ширина диалога", 280, 560, 312, 8);
        _primaryText = TextField(Settings, "Действие по умолчанию", "Сохранить");
        _secondaryText = TextField(Settings, "Действие отмены", "Отмена");

        _dismissible = Toggle(_dismissibleSettings, "Закрытие по Escape", true);
        Settings.Children.Add(_dismissibleSettings);

        _scrim = Toggle(_scrimSettings, "Закрытие по клику на scrim", true);
        Settings.Children.Add(_scrimSettings);

        Settings.Children.Add(new TextBlock
        {
            Text = "Семейство задаёт набор слотов: базовый, с иконкой и разделителем, список, " +
                   "прокручиваемый список, поле ввода и диалог без закрытия. Предпросмотр — обычный " +
                   "MaterialDialog: действия настоящие, строки списка переключаются мышью и Space. " +
                   "Модальная копия собирается из текущих настроек и возвращает результат.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
        });

        AutomationProperties.SetName(_title, "Заголовок диалога");
        AutomationProperties.SetName(_supporting, "Текст поддержки диалога");
        AutomationProperties.SetName(_primaryText, "Текст основного действия");
        AutomationProperties.SetName(_secondaryText, "Текст действия отмены");

        _list.SelectionChanged += (_, _) =>
        {
            if (!_syncing) _readout.Text = $"Выбрано строк: {_list.SelectedItems?.Count ?? 0}";
        };

        _family.SelectionChanged += (_, _) =>
        {
            if (_syncing) return;
            // The family picks the preset of the optional slots; each one stays editable afterwards.
            _syncing = true;
            var family = CurrentFamily;
            _icon.IsChecked = family is DialogFamily.Icon or DialogFamily.List or DialogFamily.ScrollableList;
            _divider.IsChecked = family == DialogFamily.Icon;
            _syncing = false;
            Apply();
        };
        foreach (var toggle in new[] { _icon, _divider, _threeLine, _scrollable, _dismissible, _scrim })
            toggle.IsCheckedChanged += (_, _) => Apply();
        foreach (var slider in new[] { _rows, _width })
            slider.PropertyChanged += (_, e) => { if (e.Property == RangeBase.ValueProperty) Apply(); };
        foreach (var field in new[] { _title, _supporting, _primaryText, _secondaryText })
            field.TextChanged += (_, _) => Apply();

        _built = true;
        Apply();
    }

    private DialogFamily CurrentFamily =>
        (DialogFamily)Math.Clamp(_family.SelectedIndex, 0, 6);

    private void Apply()
    {
        if (!_built || _syncing) return;

        var family = CurrentFamily;
        var usesList = family is DialogFamily.List or DialogFamily.ScrollableList;
        var dismissible = family != DialogFamily.NonDismissible && _dismissible.IsChecked == true;

        _rowsSettings.IsVisible = usesList;
        _threeLineSettings.IsVisible = usesList;
        _scrollableSettings.IsVisible = family == DialogFamily.List;
        _dismissibleSettings.IsVisible = family != DialogFamily.NonDismissible;
        _scrimSettings.IsVisible = family != DialogFamily.NonDismissible && dismissible;

        _preview.Title = string.IsNullOrWhiteSpace(_title.Text) ? "Заголовок диалога" : _title.Text;
        _preview.SupportingText = _supporting.Text;
        _preview.Icon = _icon.IsChecked == true ? _dialogIcon : null;
        _preview.ShowDivider = _divider.IsChecked == true;
        _preview.Width = _width.Value;
        _preview.IsDismissible = dismissible;
        _preview.CloseOnScrimClick = _scrim.IsChecked == true;
        _preview.List = usesList ? _list : null;
        _preview.Content = family == DialogFamily.Input ? _field : null;

        _list.IsScrollable = family == DialogFamily.ScrollableList || _scrollable.IsChecked == true;
        SyncList((int)Math.Round(_rows.Value), _threeLine.IsChecked == true);

        _secondaryAction.Text = string.IsNullOrWhiteSpace(_secondaryText.Text) ? "Отмена" : _secondaryText.Text;
        _primaryAction.Text = string.IsNullOrWhiteSpace(_primaryText.Text) ? "OK" : _primaryText.Text;
        _primaryAction.CloseOnInvoke = family != DialogFamily.Input;
    }

    private void SyncList(int count, bool threeLine)
    {
        while (_list.Items.Count > count)
            _list.Items.RemoveAt(_list.Items.Count - 1);

        while (_list.Items.Count < count)
        {
            var index = _list.Items.Count;
            var item = new MaterialDialogListItem
            {
                Initial = "A",
                Headline = $"Элемент {index + 1}",
                TrailingText = "100+",
                IsSelected = index < 2,
            };
            AutomationProperties.SetName(item, $"Элемент {index + 1}");
            _list.Items.Add(item);
        }

        for (var i = 0; i < _list.Items.Count; i++)
        {
            var item = (MaterialDialogListItem)_list.Items[i]!;
            item.Overline = threeLine ? "Надзаголовок" : null;
            item.SupportingText = threeLine ? "Поддерживающая строка" : null;
            MaterialList.SetLineCount(item, threeLine ? 3 : 1);
        }
    }

    private MaterialDialogList BuildList(int count, bool threeLine, bool scrollable)
    {
        var list = new MaterialDialogList { IsScrollable = scrollable };
        for (var index = 0; index < count; index++)
        {
            var item = new MaterialDialogListItem
            {
                Initial = "A",
                Overline = threeLine ? "Надзаголовок" : null,
                Headline = $"Элемент {index + 1}",
                SupportingText = threeLine ? "Поддерживающая строка" : null,
                TrailingText = "100+",
            };
            MaterialList.SetLineCount(item, threeLine ? 3 : 1);
            list.Items.Add(item);
        }
        return list;
    }

    private MaterialDialog BuildModalDialog()
    {
        var family = CurrentFamily;
        var dialog = new MaterialDialog
        {
            Title = string.IsNullOrWhiteSpace(_title.Text) ? "Заголовок диалога" : _title.Text,
            SupportingText = _supporting.Text,
            Icon = _icon.IsChecked == true ? MaterialDialog.CreateIcon() : null,
            ShowDivider = _divider.IsChecked == true,
            Width = family == DialogFamily.FullScreen ? double.NaN : _width.Value,
            IsFullScreen = family == DialogFamily.FullScreen,
            IsDismissible = family != DialogFamily.NonDismissible && _dismissible.IsChecked == true,
            CloseOnScrimClick = _scrim.IsChecked == true,
        };

        if (family is DialogFamily.List or DialogFamily.ScrollableList)
        {
            dialog.List = BuildList((int)Math.Round(_rows.Value), _threeLine.IsChecked == true,
                family == DialogFamily.ScrollableList || _scrollable.IsChecked == true);
        }

        TextBox? field = null;
        if (family == DialogFamily.Input)
        {
            field = new TextBox { PlaceholderText = "Введите имя", Text = _field.Text };
            MaterialTextField.SetVariant(field, MaterialTextFieldVariant.Outlined);
            MaterialTextField.SetLabel(field, "Имя");
            dialog.Content = field;
        }

        var secondaryText = string.IsNullOrWhiteSpace(_secondaryText.Text) ? "Отмена" : _secondaryText.Text;
        var primaryText = string.IsNullOrWhiteSpace(_primaryText.Text) ? "OK" : _primaryText.Text;

        dialog.AddAction(new MaterialDialogAction(secondaryText, "cancelled")
        {
            IsCancel = true,
            Command = new DelegateCommand(() => _readout.Text = $"Вызвано «{secondaryText}» · результат 'cancelled'"),
        });

        var primary = new MaterialDialogAction(primaryText, "saved") { IsDefault = true };
        if (field is not null)
        {
            // The command owns the close so the result carries the edited text.
            primary.CloseOnInvoke = false;
            primary.Command = new DelegateCommand(() =>
            {
                _readout.Text = $"Сохранено «{field.Text}»";
                dialog.Close($"saved '{field.Text}'");
            });
        }
        else
        {
            primary.Command = new DelegateCommand(() =>
                _readout.Text = $"Вызвано «{primaryText}» · результат 'saved'");
        }
        dialog.AddAction(primary);
        return dialog;
    }

    private async void ShowModal()
    {
        try
        {
            if (TopLevel.GetTopLevel(this) is not { } owner)
                return;
            var dialog = BuildModalDialog();
            _readout.Text = "Диалог открыт";
            var result = await dialog.ShowAsync(owner);
            _readout.Text = $"Результат: {result ?? "(закрыт)"}";
        }
        catch (Exception exception)
        {
            _readout.Text = "Не удалось открыть диалог: " + exception.Message;
        }
    }

    private static T Field<T>(StackPanel host, string label, T control) where T : Control
    {
        host.Children.Add(new TextBlock { Text = label });
        host.Children.Add(control);
        return control;
    }

    private static TextBox TextField(StackPanel host, string label, string value)
    {
        var field = Field(host, label, new TextBox
        {
            Text = value,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        });
        MaterialTextField.SetVariant(field, MaterialTextFieldVariant.Outlined);
        MaterialTextField.SetLabel(field, label);
        return field;
    }

    private static ToggleSwitch Toggle(StackPanel host, string label, bool value)
    {
        var toggle = new ToggleSwitch { Content = label, IsChecked = value };
        host.Children.Add(toggle);
        return toggle;
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

    private sealed class DelegateCommand(Action execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => execute();
    }
}
