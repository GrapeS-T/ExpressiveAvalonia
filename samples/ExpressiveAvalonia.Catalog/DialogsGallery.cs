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
    private readonly Border _previewFrame;
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
    private readonly Slider _width;
    private readonly TextBox _title;
    private readonly TextBox _supporting;
    private readonly ToggleSwitch _icon;
    private readonly ToggleSwitch _divider;
    private readonly StackPanel _listSection;
    private readonly Slider _rows;
    private readonly ToggleSwitch _threeLine;
    private readonly ToggleSwitch _scrollable;
    private readonly TextBox _primaryText;
    private readonly TextBox _secondaryText;
    private readonly StackPanel _dismissSwitches;
    private readonly ToggleSwitch _dismissible;
    private readonly ToggleSwitch _scrim;
    private bool _built;
    private bool _syncing;

    public StackPanel Settings { get; } = InspectorLayout.CreatePane();

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
        _previewFrame = new Border
        {
            Child = _preview,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _previewFrame.Bind(Border.BorderBrushProperty, this.GetResourceObservable("MaterialOutlineVariantBrush"));

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
        stage.Children.Add(_previewFrame);
        stage.Children.Add(_show);
        stage.Children.Add(_readout);
        Children.Add(new ScrollViewer
        {
            Content = stage,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        });

        var familySection = InspectorLayout.Section(Settings, "Семейство и размеры", divider: false);
        _family = InspectorLayout.Combo(
            familySection,
            "Семейство",
            new[]
            {
                "Базовый", "С иконкой", "Список", "Прокручиваемый список", "Поле ввода", "Без закрытия", "Полный экран",
            },
            0);
        _width = InspectorLayout.Slider(familySection, "Ширина диалога", 280, 560, 312, step: 8, unit: "dp", format: "0");

        var contentSection = InspectorLayout.Section(Settings, "Содержимое");
        _title = InspectorLayout.TextField(contentSection, "Заголовок", "Заголовок диалога");
        _supporting = InspectorLayout.TextField(
            contentSection,
            "Текст поддержки",
            "Диалог появляется поверх содержимого приложения и требует решения или подтверждения.",
            multiline: true);
        var contentSwitches = InspectorLayout.Switches(contentSection);
        _icon = InspectorLayout.Switch(contentSwitches, "Иконка", false);
        _divider = InspectorLayout.Switch(contentSwitches, "Разделитель", false);

        _listSection = InspectorLayout.Section(Settings, "Параметры списка");
        _rows = InspectorLayout.Slider(_listSection, "Строк в списке", 1, 12, 3, step: 1, format: "0");
        var listSwitches = InspectorLayout.Switches(_listSection);
        _threeLine = InspectorLayout.Switch(listSwitches, "Трёхстрочные строки", false);
        _scrollable = InspectorLayout.Switch(listSwitches, "Прокручиваемый список", false);

        var actionsSection = InspectorLayout.Section(Settings, "Действия и закрытие");
        _primaryText = InspectorLayout.TextField(actionsSection, "Действие по умолчанию", "Сохранить");
        _secondaryText = InspectorLayout.TextField(actionsSection, "Действие отмены", "Отмена");
        _dismissSwitches = InspectorLayout.Switches(actionsSection);
        _dismissible = InspectorLayout.Switch(_dismissSwitches, "Закрытие по Escape", true);
        _scrim = InspectorLayout.Switch(_dismissSwitches, "Закрытие по клику на scrim", true);

        InspectorLayout.Note(
            Settings,
            "Семейство задаёт набор слотов: базовый, с иконкой и разделителем, список, " +
            "прокручиваемый список, поле ввода и диалог без закрытия. Предпросмотр — обычный " +
            "MaterialDialog: действия настоящие, строки списка переключаются мышью и Space. " +
            "Модальная копия собирается из текущих настроек и возвращает результат.");

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
            _divider.IsChecked = family is DialogFamily.Icon or DialogFamily.FullScreen;
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
        var fullScreen = family == DialogFamily.FullScreen;
        var usesList = family is DialogFamily.List or DialogFamily.ScrollableList;
        var canConfigureDismiss = family != DialogFamily.NonDismissible;
        var dismissible = canConfigureDismiss && _dismissible.IsChecked == true;

        _width.IsVisible = !fullScreen;
        _icon.IsVisible = !fullScreen;
        _listSection.IsVisible = usesList;
        _scrollable.IsVisible = family == DialogFamily.List;
        _secondaryText.IsVisible = !fullScreen;
        _dismissible.IsVisible = canConfigureDismiss;
        _scrim.IsVisible = canConfigureDismiss && !fullScreen && dismissible;
        _dismissSwitches.IsVisible = _dismissible.IsVisible || _scrim.IsVisible;

        _previewFrame.Width = fullScreen ? 360 : double.NaN;
        _previewFrame.Height = fullScreen ? 460 : double.NaN;
        _previewFrame.CornerRadius = fullScreen ? new CornerRadius(16) : default;
        _previewFrame.BorderThickness = fullScreen ? new Thickness(1) : default;
        _previewFrame.ClipToBounds = fullScreen;

        _preview.Title = string.IsNullOrWhiteSpace(_title.Text) ? "Заголовок диалога" : _title.Text;
        _preview.SupportingText = _supporting.Text;
        _preview.Icon = !fullScreen && _icon.IsChecked == true ? _dialogIcon : null;
        _preview.ShowDivider = _divider.IsChecked == true;
        _preview.IsFullScreen = fullScreen;
        _preview.Width = fullScreen ? double.NaN : _width.Value;
        _preview.HorizontalAlignment = fullScreen ? HorizontalAlignment.Stretch : HorizontalAlignment.Center;
        _preview.VerticalAlignment = fullScreen ? VerticalAlignment.Stretch : VerticalAlignment.Center;
        _preview.IsDismissible = dismissible;
        _preview.CloseOnScrimClick = _scrim.IsChecked == true;
        _preview.List = usesList ? _list : null;
        _preview.Content = family is DialogFamily.Input or DialogFamily.FullScreen ? _field : null;

        _list.IsScrollable = family == DialogFamily.ScrollableList || _scrollable.IsChecked == true;
        SyncList((int)Math.Round(_rows.Value), _threeLine.IsChecked == true);

        _secondaryAction.Text = string.IsNullOrWhiteSpace(_secondaryText.Text) ? "Отмена" : _secondaryText.Text;
        _primaryAction.Text = string.IsNullOrWhiteSpace(_primaryText.Text) ? "OK" : _primaryText.Text;
        _primaryAction.CloseOnInvoke = family is not (DialogFamily.Input or DialogFamily.FullScreen);
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
        var fullScreen = family == DialogFamily.FullScreen;
        var dialog = new MaterialDialog
        {
            Title = string.IsNullOrWhiteSpace(_title.Text) ? "Заголовок диалога" : _title.Text,
            SupportingText = _supporting.Text,
            Icon = !fullScreen && _icon.IsChecked == true ? MaterialDialog.CreateIcon() : null,
            ShowDivider = _divider.IsChecked == true,
            Width = fullScreen ? double.NaN : _width.Value,
            IsFullScreen = fullScreen,
            IsDismissible = family != DialogFamily.NonDismissible && _dismissible.IsChecked == true,
            CloseOnScrimClick = _scrim.IsChecked == true,
        };

        if (family is DialogFamily.List or DialogFamily.ScrollableList)
        {
            dialog.List = BuildList((int)Math.Round(_rows.Value), _threeLine.IsChecked == true,
                family == DialogFamily.ScrollableList || _scrollable.IsChecked == true);
        }

        TextBox? field = null;
        if (family is DialogFamily.Input or DialogFamily.FullScreen)
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
