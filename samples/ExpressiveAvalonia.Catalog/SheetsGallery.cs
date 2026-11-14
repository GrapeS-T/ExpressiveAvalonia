using System;
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

/// <summary>
/// Live sheet coverage for N07 (F05 shared lifecycle). Source mapping:
/// hidden/partial/expanded anchors, 56 DIP positional and 125 DIP velocity thresholds, default-
/// spatial show/expand and fast-effects hide/partial-expand springs, boundary dampening,
/// vertical surface overshoot scaling, modal scrim/focus trap, dismiss veto and desktop nested
/// scroll from BottomSheet.kt, ModalBottomSheet.kt and SheetDefaults.kt. The bottom scenario is a
/// persistent BottomSheetScaffold (top bar, body, peek-height sheet and snackbar host) with a
/// modal bottom sheet opened from its body; the side sheet is the explicit non-upstream extension
/// composed from the same lifecycle.
/// </summary>
internal sealed class SheetPlayground : Grid, IComponentPlayground
{
    private readonly Grid _host = new();
    private readonly Border _surface;
    private readonly MaterialSideSheet _side;
    private readonly MaterialBottomSheet _bottom;
    private readonly MaterialBottomSheetScaffold _scaffold;
    private readonly StackPanel _sideOptions = new() { Spacing = 4 };
    private readonly StackPanel _modalItems = new() { Spacing = 4 };
    private readonly StackPanel _persistentItems = new() { Spacing = 4 };
    private readonly Button _open = new();
    private readonly WrapPanel _anchorControls = new() { Orientation = Orientation.Horizontal };
    private readonly TextBlock _readout = new()
    {
        Text = "Лист закрыт · нажмите «Открыть»",
        TextWrapping = TextWrapping.Wrap,
        MaxWidth = 460,
        FontSize = 14,
    };
    private readonly TextBlock _sideReadout = new()
    {
        Text = "Лист закрыт · нажмите «Открыть»",
        TextWrapping = TextWrapping.Wrap,
        MaxWidth = 420,
        FontSize = 14,
    };
    private readonly Button _sidePrimary;
    private readonly Button _modalPrimary;
    private readonly Button _persistentPrimary;
    private readonly MaterialButtonGroup _form;
    private readonly MaterialButtonGroup _edge;
    private readonly ToggleSwitch _modal;
    private readonly ToggleSwitch _back;
    private readonly ToggleSwitch _closeButton;
    private readonly ToggleSwitch _dragHandle;
    private readonly ToggleSwitch _dismiss;
    private readonly ToggleSwitch _actions;
    private readonly ToggleSwitch _skipHidden;
    private readonly ToggleSwitch _swipe;
    private readonly TextBox _headline;
    private readonly TextBox _actionLabel;
    private readonly Slider _sideWidth;
    private readonly Slider _options;
    private readonly Slider _height;
    private readonly Slider _peek;
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

        // Modal bottom sheet: the source default anchor set (Hidden/PartiallyExpanded/Expanded)
        // and the half-content partial offset from calculatePartiallyExpandedOffset, because
        // SheetPeekHeight is left at NaN.
        _bottom = new MaterialBottomSheet
        {
            ShowDragHandle = true,
            IsModal = true,
        };
        _bottom.Content = BuildModalContent();
        var modalActions = SheetActions(_bottom, "Готово");
        _bottom.Actions = modalActions.Actions;
        _modalPrimary = modalActions.Primary;

        // Persistent scaffold: standard (non-modal) sheet, top bar, body and snackbar host.
        _scaffold = new MaterialBottomSheetScaffold
        {
            TopBar = BuildTopBar(),
        };
        _persistentPrimary = BuildPersistentActions(out var persistentActions);
        _scaffold.Sheet.Actions = persistentActions;
        _scaffold.SheetContent = BuildPersistentContent();
        _scaffold.Content = BuildScaffoldBody();
        _sideReadout.Bind(TextBlock.TextProperty, _readout.GetObservable(TextBlock.TextProperty));

        var sideBody = new StackPanel
        {
            Spacing = 16,
            Margin = new Thickness(32),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        sideBody.Children.Add(new TextBlock { Text = "Область приложения", FontSize = 24, FontWeight = FontWeight.Medium });
        sideBody.Children.Add(new TextBlock
        {
            Text = "Лист открывается поверх этой области. Модальный лист блокирует страницу, " +
                   "циклически проводит Tab и закрывается по Escape или клику по scrim; " +
                   "стандартный оставляет страницу интерактивной.",
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 420,
            Opacity = 0.7,
        });
        sideBody.Children.Add(_open);
        sideBody.Children.Add(_sideReadout);

        MaterialAssist.SetButtonVariant(_open, MaterialButtonVariant.Filled);
        MaterialAssist.SetControlSize(_open, MaterialControlSize.Small);
        _open.Content = "Открыть боковой лист";
        AutomationProperties.SetName(_open, "Открыть боковой лист");
        _open.Click += (_, _) => OpenSideSheet();

        _host.Children.Add(sideBody);
        _host.Children.Add(_scaffold);
        _host.Children.Add(_side);
        // The modal sheet is the last child so its scrim covers the scaffold and side sheet.
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

        _form = Choice(Settings, "Форма", new[] { "Боковая", "Persistent scaffold" }, 0);
        _modal = Toggle(Settings, "Модальный", true);

        _edge = Choice(_sideSettings, "Сторона", new[] { "Start", "End" }, 1);
        _headline = TextField(_sideSettings, "Заголовок", "Фильтры");
        _back = Toggle(_sideSettings, "Кнопка «Назад»", true);
        _closeButton = Toggle(_sideSettings, "Кнопка закрытия", true);
        _sideWidth = Number(_sideSettings, "Ширина листа", 240, 400, 320, 8);
        Settings.Children.Add(_sideSettings);

        _dragHandle = Toggle(_bottomSettings, "Drag handle", true);
        _peek = Number(_bottomSettings, "Peek height", 0, 240, 56, 8);
        _skipHidden = Toggle(_bottomSettings, "Пропускать Hidden anchor", true);
        _swipe = Toggle(_bottomSettings, "Свайп и drag", true);
        Settings.Children.Add(_bottomSettings);

        _dismiss = Toggle(Settings, "Закрытие по Escape и scrim", true);
        _actions = Toggle(Settings, "Кнопки действий", true);
        _actionLabel = TextField(Settings, "Основное действие", "Сохранить");
        _options = Number(Settings, "Пунктов в содержимом", 2, 10, 4, 1);
        _height = Number(Settings, "Высота области", 360, 720, 520, 20);
        Settings.Children.Add(new TextBlock
        {
            Text = "Persistent scaffold: верхняя панель, тело с нижним inset = peek height, " +
                   "стандартный нижний лист и snackbar host над частичным листом. Peek задаёт " +
                   "высоту частичного anchor; «Пропускать Hidden» убирает закрытое состояние, " +
                   "поэтому Escape, drag и «Скрыть» сворачивают лист в partial. Кнопка в теле " +
                   "открывает отдельный модальный лист с исходными anchors и scrim.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
        });

        AutomationProperties.SetName(_headline, "Заголовок листа");
        AutomationProperties.SetName(_actionLabel, "Текст основного действия");

        foreach (var (sheet, name) in new (MaterialSheet Sheet, string Name)[]
                 {
                     (_side, "Боковой лист"),
                     (_bottom, "Модальный нижний лист"),
                     (_scaffold.Sheet, "Persistent лист"),
                 })
        {
            var persistent = ReferenceEquals(sheet, _scaffold.Sheet);
            sheet.Opening += (_, _) =>
            {
                if (!persistent || !_showingSide)
                    _readout.Text = $"{name}: открывается";
            };
            sheet.Opened += (_, _) =>
            {
                if (!persistent || !_showingSide)
                    _readout.Text = $"{name}: открыт · {(sheet.IsModal ? "модальный" : "стандартный")}";
            };
            sheet.Closing += (_, _) =>
            {
                if (!persistent || !_showingSide)
                    _readout.Text = $"{name}: закрывается";
            };
            sheet.Closed += (_, _) =>
            {
                if (!persistent || !_showingSide)
                    _readout.Text = $"{name}: закрыт";
            };
            sheet.ValueChangeRequested += (_, e) =>
            {
                if (e.Cancel && (!persistent || !_showingSide))
                    _readout.Text = $"{name}: переход в {e.Value} отклонён";
            };
        }
        _scaffold.Sheet.PropertyChanged += (_, e) =>
        {
            if (e.Property == MaterialSheet.CurrentValueProperty && !_showingSide)
                _readout.Text =
                    $"Persistent лист: {Describe(_scaffold.Sheet.CurrentValue)} · " +
                    $"peek {_scaffold.SheetPeekHeight:0} · snackbar inset {_scaffold.SnackbarBottomInset:0}";
        };
        _side.BackRequested += (_, e) =>
        {
            e.Handled = true;
            _readout.Text = "«Назад» обработан внутри листа без закрытия";
        };

        foreach (var choice in new[] { _form, _edge })
            choice.SelectionChanged += (_, _) => Apply();
        foreach (var toggle in new[]
                 {
                     _modal, _back, _closeButton, _dragHandle, _dismiss, _actions, _skipHidden, _swipe,
                 })
            toggle.IsCheckedChanged += (_, _) => Apply();
        foreach (var slider in new[] { _sideWidth, _options, _height, _peek })
            slider.PropertyChanged += (_, e) => { if (e.Property == RangeBase.ValueProperty) Apply(); };
        foreach (var field in new[] { _headline, _actionLabel })
            field.TextChanged += (_, _) => Apply();

        _built = true;
        Apply();
    }

    private void OpenSideSheet()
    {
        _bottom.IsOpen = false;
        _side.IsOpen = true;
    }

    private void ShowSnackbar()
    {
        var snackbar = new MaterialSnackbar
        {
            Text = "Черновик сохранён",
            ActionContent = "Отменить",
            Duration = TimeSpan.FromSeconds(4),
        };
        _ = _scaffold.SnackbarHost.Show(snackbar);
        _readout.Text =
            $"Snackbar показан · над частичным листом ({_scaffold.SnackbarBottomInset:0} DIP)";
    }

    private Control BuildTopBar()
    {
        var bar = new Border
        {
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = new StackPanel
            {
                Margin = new Thickness(24, 12, 24, 12),
                Spacing = 2,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Каталог · Листы",
                        FontSize = 20,
                        FontWeight = FontWeight.Medium,
                    },
                    new TextBlock
                    {
                        Text = "Persistent BottomSheetScaffold",
                        FontSize = 12,
                        Opacity = 0.7,
                    },
                },
            },
        };
        bar.Bind(Border.BorderBrushProperty, this.GetResourceObservable("MaterialOutlineVariantBrush"));
        return bar;
    }

    private Control BuildScaffoldBody()
    {
        var body = new StackPanel
        {
            Spacing = 12,
            Margin = new Thickness(24),
        };
        body.Children.Add(new TextBlock
        {
            Text = "Область приложения",
            FontSize = 24,
            FontWeight = FontWeight.Medium,
        });
        body.Children.Add(new TextBlock
        {
            Text = "Тело остаётся интерактивным: стандартный лист не блокирует указатель и не " +
                   "запирает фокус. Нижний inset тела равен peek height, поэтому содержимое не " +
                   "уходит под свёрнутый лист.",
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 460,
            Opacity = 0.7,
        });

        var snackbar = new Button { Content = "Показать snackbar" };
        MaterialAssist.SetButtonVariant(snackbar, MaterialButtonVariant.Tonal);
        MaterialAssist.SetControlSize(snackbar, MaterialControlSize.Small);
        AutomationProperties.SetName(snackbar, "Показать snackbar");
        snackbar.Click += (_, _) => ShowSnackbar();

        var modal = new Button { Content = "Открыть модальный лист" };
        MaterialAssist.SetButtonVariant(modal, MaterialButtonVariant.Outlined);
        MaterialAssist.SetControlSize(modal, MaterialControlSize.Small);
        AutomationProperties.SetName(modal, "Открыть модальный нижний лист");
        modal.Click += (_, _) =>
        {
            _readout.Text = "Модальный нижний лист: открывается";
            _bottom.IsOpen = true;
        };

        var row = new WrapPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(snackbar);
        row.Children.Add(modal);
        body.Children.Add(row);

        BuildAnchorControls();
        body.Children.Add(_anchorControls);
        body.Children.Add(_readout);

        var note = new TextBlock
        {
            Text = "Модальный лист сохраняет исходное поведение ModalBottomSheet: Hidden остаётся " +
                   "anchor, partial — половина содержимого, scrim и Tab-цикл только у модального " +
                   "представления. Persistent лист получает anchors из SheetPeekHeight.",
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 460,
            FontSize = 13,
            Opacity = 0.7,
        };
        body.Children.Add(note);

        // The scaffold supplies the peek inset; the body scrolls inside the remaining area so
        // tiny body heights still expose the demo controls without double padding.
        return new ScrollViewer
        {
            Content = body,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
    }

    private Control BuildPersistentContent()
    {
        var panel = new StackPanel { Spacing = 12, Margin = new Thickness(24, 4, 24, 0) };
        panel.Children.Add(new TextBlock
        {
            Text = "Панель настроек",
            FontSize = 20,
            Opacity = 0.7,
        });
        panel.Children.Add(new TextBlock
        {
            Text = "Потяните handle вверх, чтобы развернуть лист, или вниз — до частичного " +
                   "anchor. При включённом «Пропускать Hidden» лист нельзя увести ниже peek.",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.7,
        });
        panel.Children.Add(_persistentItems);
        return panel;
    }

    private Control BuildModalContent()
    {
        var panel = new StackPanel { Spacing = 12, Margin = new Thickness(24, 4, 24, 24) };
        panel.Children.Add(new TextBlock { Text = "Модальный лист", FontSize = 20, Opacity = 0.7 });
        panel.Children.Add(new TextBlock
        {
            Text = "Модальный вариант использует исходные anchors: Hidden, половина содержимого и " +
                   "Expanded. Потяните handle вниз дальше порога или нажмите Escape.",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.7,
        });
        panel.Children.Add(_modalItems);
        var close = new Button { Content = "Закрыть из содержимого", HorizontalAlignment = HorizontalAlignment.Left };
        MaterialAssist.SetButtonVariant(close, MaterialButtonVariant.Text);
        MaterialAssist.SetControlSize(close, MaterialControlSize.Small);
        AutomationProperties.SetName(close, "Закрыть модальный лист из содержимого");
        close.Click += (_, _) =>
        {
            _readout.Text = "Закрыто из содержимого модального листа";
            _bottom.IsOpen = false;
        };
        panel.Children.Add(close);
        return panel;
    }

    private Button BuildPersistentActions(out StackPanel row)
    {
        row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        var primary = new Button { Content = "Сохранить" };
        MaterialAssist.SetButtonVariant(primary, MaterialButtonVariant.Filled);
        MaterialAssist.SetControlSize(primary, MaterialControlSize.Small);
        primary.Click += async (_, _) =>
        {
            // A persistent scaffold has no implicit hidden state; confirm and return to the peek
            // anchor instead of removing the sheet.
            await _scaffold.Sheet.RequestValueAsync(
                _scaffold.SkipPartiallyExpanded
                    ? MaterialSheetValue.Expanded
                    : MaterialSheetValue.PartiallyExpanded);
            _readout.Text =
                $"«{primary.Content}» нажато · лист на {Describe(_scaffold.Sheet.CurrentValue)}";
        };

        var expand = new Button { Content = "Развернуть" };
        MaterialAssist.SetButtonVariant(expand, MaterialButtonVariant.Outlined);
        MaterialAssist.SetControlSize(expand, MaterialControlSize.Small);
        AutomationProperties.SetName(expand, "Развернуть persistent лист");
        expand.Click += async (_, _) =>
        {
            await _scaffold.Sheet.ExpandAsync();
            _readout.Text = "Persistent лист: развёрнут";
        };

        row.Children.Add(primary);
        row.Children.Add(expand);
        return primary;
    }

    /// <summary>
    /// Direct anchor coverage: partial/expanded/hidden transitions that the source exposes through
    /// SheetState. The persistent scaffold is used because the side sheet extension has no partial
    /// anchor.
    /// </summary>
    private void BuildAnchorControls()
    {
        if (_anchorControls.Children.Count > 0)
            return;

        _anchorControls.Children.Add(StateButton("Частично", MaterialSheetValue.PartiallyExpanded));
        _anchorControls.Children.Add(StateButton("Развернуть", MaterialSheetValue.Expanded));
        _anchorControls.Children.Add(StateButton("Скрыть", MaterialSheetValue.Hidden));
    }

    private Button StateButton(string label, MaterialSheetValue value)
    {
        var button = new Button
        {
            Content = label,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        MaterialAssist.SetButtonVariant(button, MaterialButtonVariant.Tonal);
        MaterialAssist.SetControlSize(button, MaterialControlSize.Small);
        AutomationProperties.SetName(button, $"Persistent лист: {label}");
        button.Click += async (_, _) =>
        {
            try
            {
                switch (value)
                {
                    case MaterialSheetValue.PartiallyExpanded:
                        await _scaffold.Sheet.PartialExpandAsync();
                        _readout.Text = "Persistent лист: частичный anchor";
                        break;
                    case MaterialSheetValue.Expanded:
                        await _scaffold.Sheet.ExpandAsync();
                        _readout.Text = "Persistent лист: развёрнут";
                        break;
                    default:
                        await _scaffold.Sheet.RequestValueAsync(MaterialSheetValue.Hidden);
                        _readout.Text = _scaffold.Sheet.CurrentValue == MaterialSheetValue.Hidden
                            ? "Persistent лист: скрыт (Hidden anchor включён)"
                            : "Persistent лист: Hidden anchor пропущен — сворачивание в partial";
                        break;
                }
            }
            catch (InvalidOperationException)
            {
                _readout.Text = "Частичный anchor отключён (SkipPartiallyExpanded)";
            }
        };
        return button;
    }

    private void Apply()
    {
        if (!_built) return;

        var side = _form.SelectedIndex == 0;
        var modal = _modal.IsChecked == true;
        var count = (int)Math.Round(_options.Value);
        var actionLabel = string.IsNullOrWhiteSpace(_actionLabel.Text) ? "Сохранить" : _actionLabel.Text;

        var modeChanged = side != _showingSide;
        if (modeChanged)
        {
            _showingSide = side;
            _side.IsOpen = false;
            _bottom.IsOpen = false;
        }

        _sideSettings.IsVisible = side;
        _bottomSettings.IsVisible = !side;
        _anchorControls.IsVisible = !side;
        _scaffold.IsVisible = !side;

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

        _scaffold.SheetPeekHeight = _peek.Value;
        _scaffold.SkipHiddenState = _skipHidden.IsChecked == true;
        _scaffold.ShowDragHandle = _dragHandle.IsChecked == true;
        _scaffold.SheetSwipeEnabled = _swipe.IsChecked == true;
        _scaffold.Sheet.ShowActions = _actions.IsChecked == true;
        _scaffold.Sheet.IsDismissEnabled = _dismiss.IsChecked == true;

        _sidePrimary.Content = actionLabel;
        _modalPrimary.Content = actionLabel;
        _persistentPrimary.Content = actionLabel;
        AutomationProperties.SetName(_sidePrimary, actionLabel);
        AutomationProperties.SetName(_modalPrimary, actionLabel);
        AutomationProperties.SetName(_persistentPrimary, actionLabel);

        _surface.Height = _height.Value;
        _open.Content = "Открыть боковой лист";
        AutomationProperties.SetName(_open, "Открыть боковой лист");

        SyncSideOptions(count);
        SyncModalItems(count);
        SyncPersistentItems(count);

        if (modeChanged)
            _readout.Text = side
                ? "Лист закрыт · нажмите «Открыть»"
                : $"Persistent лист: {Describe(_scaffold.Sheet.CurrentValue)} · " +
                  $"peek {_scaffold.SheetPeekHeight:0}";
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
        primary.Click += async (_, _) =>
        {
            _readout.Text = $"«{primary.Content}» нажато · лист закрыт";
            await sheet.CloseAsync();
        };

        var secondary = new Button { Content = "Отмена" };
        MaterialAssist.SetButtonVariant(secondary, MaterialButtonVariant.Outlined);
        MaterialAssist.SetControlSize(secondary, MaterialControlSize.Small);
        AutomationProperties.SetName(secondary, "Отмена");
        secondary.Click += async (_, _) =>
        {
            _readout.Text = "Отменено · лист закрыт";
            await sheet.CloseAsync();
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

    private void SyncModalItems(int count)
    {
        while (_modalItems.Children.Count > count)
            _modalItems.Children.RemoveAt(_modalItems.Children.Count - 1);

        while (_modalItems.Children.Count < count)
            _modalItems.Children.Add(new TextBlock
            {
                Text = $"Элемент списка {_modalItems.Children.Count + 1}",
            });
    }

    private void SyncPersistentItems(int count)
    {
        while (_persistentItems.Children.Count > count)
            _persistentItems.Children.RemoveAt(_persistentItems.Children.Count - 1);

        while (_persistentItems.Children.Count < count)
            _persistentItems.Children.Add(new TextBlock
            {
                Text = $"Настройка {_persistentItems.Children.Count + 1}",
            });
    }

    private static string Describe(MaterialSheetValue value) => value switch
    {
        MaterialSheetValue.Hidden => "скрыт",
        MaterialSheetValue.PartiallyExpanded => "частично",
        _ => "развёрнут",
    };

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
