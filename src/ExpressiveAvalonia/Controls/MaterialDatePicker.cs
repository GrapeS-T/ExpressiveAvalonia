using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace ExpressiveAvalonia.Controls;

/// <summary>Whether the picker selects one date or a start/end range.</summary>
public enum MaterialDateSelectionMode
{
    Single,
    Range,
}

/// <summary>The presentation of a <see cref="MaterialDatePicker"/>.</summary>
public enum MaterialDatePickerMode
{
    /// <summary>Desktop docked surface: month/year dropdown navigation, calendar and local actions.</summary>
    Docked,
    /// <summary>Dialog surface: supporting label, large selected date, calendar and actions.</summary>
    Modal,
    /// <summary>Full-screen range surface: close/save header, sticky weekday row and scrolling months.</summary>
    FullScreen,
}

/// <summary>The content shown below the picker header.</summary>
public enum MaterialDatePickerView
{
    Day,
    Month,
    Year,
}

/// <summary>Selection change payload. <see cref="IsRangeComplete"/> is true once both endpoints exist.</summary>
public sealed class MaterialDateSelectionChangedEventArgs : EventArgs
{
    public MaterialDateSelectionChangedEventArgs(DateTimeOffset? start, DateTimeOffset? end, bool isRangeComplete)
    {
        Start = start;
        End = end;
        IsRangeComplete = isRangeComplete;
    }

    public DateTimeOffset? Start { get; }
    public DateTimeOffset? End { get; }
    public bool IsRangeComplete { get; }
}

/// <summary>
/// Material Design 3 date picker covering the docked, modal, full-screen range and text-input
/// presentations from the source design system.
/// </summary>
/// <remarks>
/// <para>
/// Dates are calendar dates: bounds, cells and navigation compare the date component of the
/// <see cref="DateTimeOffset"/> values, and selection helpers produce values at local midnight.
/// The <see cref="Culture"/> drives month and weekday names, the first day of the week, the
/// headline format and text parsing. <see cref="DateFormat"/> overrides the short date pattern
/// used by the text-input view.
/// </para>
/// <para>
/// Keyboard: arrow keys move focus, PageUp/PageDown change month (with Shift, year), Home/End
/// move to the start/end of the week, Enter or Space selects the focused day and Escape cancels.
/// In the month and year views arrows move by one choice, PageUp/PageDown by a year, and
/// Escape returns to the day view. Cells are reachable by Tab through the picker's single tab
/// stop (roving focus), and each cell exposes its full localized date to automation.
/// </para>
/// <para>
/// Transitions use the shared Material duration tokens, which collapse to zero when the theme
/// runs with reduced motion. The control owns no timers and detaching the popup or dialog
/// releases all captured state.
/// </para>
/// </remarks>
[TemplatePart("PART_Surface", typeof(Border))]
[TemplatePart("PART_Header", typeof(Border))]
[TemplatePart("PART_HeaderLabel", typeof(TextBlock))]
[TemplatePart("PART_HeaderHeadline", typeof(TextBlock))]
[TemplatePart("PART_EditButton", typeof(Button))]
[TemplatePart("PART_CloseButton", typeof(Button))]
[TemplatePart("PART_SaveButton", typeof(Button))]
[TemplatePart("PART_DockedSelectionRow", typeof(Grid))]
[TemplatePart("PART_ModalSelectionRow", typeof(Grid))]
[TemplatePart("PART_PrevMonthButton", typeof(Button))]
[TemplatePart("PART_MonthMenuButton", typeof(Button))]
[TemplatePart("PART_NextMonthButton", typeof(Button))]
[TemplatePart("PART_PrevYearButton", typeof(Button))]
[TemplatePart("PART_YearMenuButton", typeof(Button))]
[TemplatePart("PART_NextYearButton", typeof(Button))]
[TemplatePart("PART_PrevButton", typeof(Button))]
[TemplatePart("PART_MonthYearButton", typeof(Button))]
[TemplatePart("PART_NextButton", typeof(Button))]
[TemplatePart("PART_DayView", typeof(Grid))]
[TemplatePart("PART_WeekdayRow", typeof(UniformGrid))]
[TemplatePart("PART_DayGrid", typeof(UniformGrid))]
[TemplatePart("PART_ChoiceScroll", typeof(ScrollViewer))]
[TemplatePart("PART_ChoiceHost", typeof(Panel))]
[TemplatePart("PART_FullScreenView", typeof(Grid))]
[TemplatePart("PART_FullScreenWeekdayRow", typeof(UniformGrid))]
[TemplatePart("PART_MonthGroup", typeof(StackPanel))]
[TemplatePart("PART_InputPanel", typeof(Panel))]
[TemplatePart("PART_StartInput", typeof(TextBox))]
[TemplatePart("PART_EndInput", typeof(TextBox))]
[TemplatePart("PART_InputError", typeof(TextBlock))]
[TemplatePart("PART_Actions", typeof(Border))]
[TemplatePart("PART_ClearButton", typeof(Button))]
[TemplatePart("PART_CancelButton", typeof(Button))]
[TemplatePart("PART_OkButton", typeof(Button))]
public sealed class MaterialDatePicker : TemplatedControl
{
    private const int DayCellCount = 42;
    private const int MonthChoiceCount = 12;
    private const int YearChoiceCount = 18;

    public static readonly StyledProperty<MaterialDateSelectionMode> SelectionModeProperty =
        AvaloniaProperty.Register<MaterialDatePicker, MaterialDateSelectionMode>(nameof(SelectionMode));

    public static readonly StyledProperty<MaterialDatePickerMode> ModeProperty =
        AvaloniaProperty.Register<MaterialDatePicker, MaterialDatePickerMode>(
            nameof(Mode), MaterialDatePickerMode.Modal);

    public static readonly StyledProperty<MaterialDatePickerView> ViewProperty =
        AvaloniaProperty.Register<MaterialDatePicker, MaterialDatePickerView>(
            nameof(View), MaterialDatePickerView.Day, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<DateTimeOffset?> SelectedDateProperty =
        AvaloniaProperty.Register<MaterialDatePicker, DateTimeOffset?>(
            nameof(SelectedDate), defaultBindingMode: BindingMode.TwoWay, coerce: CoerceDate);

    public static readonly StyledProperty<DateTimeOffset?> SelectedEndDateProperty =
        AvaloniaProperty.Register<MaterialDatePicker, DateTimeOffset?>(
            nameof(SelectedEndDate), defaultBindingMode: BindingMode.TwoWay, coerce: CoerceDate);

    public static readonly StyledProperty<DateTimeOffset?> DisplayDateProperty =
        AvaloniaProperty.Register<MaterialDatePicker, DateTimeOffset?>(
            nameof(DisplayDate), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<DateTimeOffset?> MinimumDateProperty =
        AvaloniaProperty.Register<MaterialDatePicker, DateTimeOffset?>(nameof(MinimumDate));

    public static readonly StyledProperty<DateTimeOffset?> MaximumDateProperty =
        AvaloniaProperty.Register<MaterialDatePicker, DateTimeOffset?>(nameof(MaximumDate));

    public static readonly StyledProperty<CultureInfo?> CultureProperty =
        AvaloniaProperty.Register<MaterialDatePicker, CultureInfo?>(nameof(Culture));

    public static readonly StyledProperty<bool> IsInputModeProperty =
        AvaloniaProperty.Register<MaterialDatePicker, bool>(nameof(IsInputMode));

    public static readonly StyledProperty<bool> ShowActionsProperty =
        AvaloniaProperty.Register<MaterialDatePicker, bool>(nameof(ShowActions), true);

    public static readonly StyledProperty<bool> ShowClearButtonProperty =
        AvaloniaProperty.Register<MaterialDatePicker, bool>(nameof(ShowClearButton), true);

    public static readonly StyledProperty<bool> AllowInputModeProperty =
        AvaloniaProperty.Register<MaterialDatePicker, bool>(nameof(AllowInputMode), true);

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<MaterialDatePicker, string?>(nameof(Label));

    public static readonly StyledProperty<string?> DateFormatProperty =
        AvaloniaProperty.Register<MaterialDatePicker, string?>(nameof(DateFormat));

    public static readonly StyledProperty<int> MonthCountProperty =
        AvaloniaProperty.Register<MaterialDatePicker, int>(nameof(MonthCount), 1,
            validate: value => value >= 1);

    public static readonly StyledProperty<bool> ShowAdjacentDaysProperty =
        AvaloniaProperty.Register<MaterialDatePicker, bool>(nameof(ShowAdjacentDays), true);

    public static readonly StyledProperty<DateTime?> TodayOverrideProperty =
        AvaloniaProperty.Register<MaterialDatePicker, DateTime?>(nameof(TodayOverride));

    public static readonly DirectProperty<MaterialDatePicker, string> HeadlineProperty =
        AvaloniaProperty.RegisterDirect<MaterialDatePicker, string>(nameof(Headline), o => o.Headline);

    public static readonly DirectProperty<MaterialDatePicker, string> EffectiveLabelProperty =
        AvaloniaProperty.RegisterDirect<MaterialDatePicker, string>(nameof(EffectiveLabel), o => o.EffectiveLabel);

    public static readonly DirectProperty<MaterialDatePicker, string> MonthYearTextProperty =
        AvaloniaProperty.RegisterDirect<MaterialDatePicker, string>(nameof(MonthYearText), o => o.MonthYearText);

    public static readonly DirectProperty<MaterialDatePicker, string> PlaceholderProperty =
        AvaloniaProperty.RegisterDirect<MaterialDatePicker, string>(nameof(Placeholder), o => o.Placeholder);

    public static readonly DirectProperty<MaterialDatePicker, string> AutomationValueProperty =
        AvaloniaProperty.RegisterDirect<MaterialDatePicker, string>(nameof(AutomationValue), o => o.AutomationValue);

    public static readonly DirectProperty<MaterialDatePicker, string?> ValidationMessageProperty =
        AvaloniaProperty.RegisterDirect<MaterialDatePicker, string?>(nameof(ValidationMessage), o => o.ValidationMessage);

    public static readonly DirectProperty<MaterialDatePicker, bool> HasSelectionProperty =
        AvaloniaProperty.RegisterDirect<MaterialDatePicker, bool>(nameof(HasSelection), o => o.HasSelection);

    public static readonly DirectProperty<MaterialDatePicker, bool> IsRangeProperty =
        AvaloniaProperty.RegisterDirect<MaterialDatePicker, bool>(nameof(IsRange), o => o.IsRange);

    private readonly List<MaterialDateCell> _dayCells = [];
    private readonly List<MaterialDateCell> _fullScreenCells = [];
    private readonly List<(IMaterialDateChoiceCell Cell, int Value)> _choiceCells = [];
    private bool _applied;
    private bool _settingSelection;
    private bool _normalizing;
    private DateTime _focusDate;
    private bool _focusDateSet;
    private DateTime? _rangeAnchor;
    private bool _awaitingRangeEnd;
    private DateTime? _previewDate;
    private string _headline = string.Empty;
    private string _effectiveLabel = string.Empty;
    private string _monthYearText = string.Empty;
    private string _placeholder = string.Empty;
    private string _automationValue = string.Empty;
    private string? _validationMessage;
    private bool _hasSelection;
    private bool _isRange;
    private DatePickerPeer? _peer;

    private TextBlock? _headerLabel;
    private TextBlock? _headerHeadline;
    private Button? _editButton;
    private Button? _closeButton;
    private Button? _saveButton;
    private Button? _prevMonthButton;
    private Button? _monthMenuButton;
    private Button? _nextMonthButton;
    private Button? _prevYearButton;
    private Button? _yearMenuButton;
    private Button? _nextYearButton;
    private Button? _prevButton;
    private Button? _monthYearButton;
    private Button? _nextButton;
    private UniformGrid? _weekdayRow;
    private UniformGrid? _dayGrid;
    private Panel? _choiceHost;
    private UniformGrid? _fullScreenWeekdayRow;
    private StackPanel? _monthGroup;
    private TextBox? _startInput;
    private TextBox? _endInput;
    private TextBlock? _inputError;
    private Border? _actions;
    private Button? _clearButton;
    private Button? _cancelButton;
    private Button? _okButton;
    private string? _fullScreenSignature;

    /// <summary>Raised whenever the selection changes, including intermediate range endpoints.</summary>
    public event EventHandler<MaterialDateSelectionChangedEventArgs>? SelectionChanged;

    /// <summary>Raised by the OK/Save action after the input text has been committed.</summary>
    public event EventHandler? Confirmed;

    /// <summary>Raised by Cancel, Escape or the close action. The owner decides whether to revert.</summary>
    public event EventHandler? Cancelled;

    /// <summary>Raised by the clear action after the selection has been removed.</summary>
    public event EventHandler? Cleared;

    public MaterialDateSelectionMode SelectionMode
    {
        get => GetValue(SelectionModeProperty);
        set => SetValue(SelectionModeProperty, value);
    }

    public MaterialDatePickerMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    /// <summary>The content below the header. Setting it navigates without changing the selection.</summary>
    public MaterialDatePickerView View
    {
        get => GetValue(ViewProperty);
        set => SetValue(ViewProperty, value);
    }

    public DateTimeOffset? SelectedDate
    {
        get => GetValue(SelectedDateProperty);
        set => SetValue(SelectedDateProperty, value);
    }

    /// <summary>The range end. Ignored when <see cref="SelectionMode"/> is single.</summary>
    public DateTimeOffset? SelectedEndDate
    {
        get => GetValue(SelectedEndDateProperty);
        set => SetValue(SelectedEndDateProperty, value);
    }

    /// <summary>The month shown by the calendar. Defaults to the selection, then today.</summary>
    public DateTimeOffset? DisplayDate
    {
        get => GetValue(DisplayDateProperty);
        set => SetValue(DisplayDateProperty, value);
    }

    /// <summary>Inclusive lower bound. Values outside the bound are disabled and selection is clamped.</summary>
    public DateTimeOffset? MinimumDate
    {
        get => GetValue(MinimumDateProperty);
        set => SetValue(MinimumDateProperty, value);
    }

    /// <summary>Inclusive upper bound. Values outside the bound are disabled and selection is clamped.</summary>
    public DateTimeOffset? MaximumDate
    {
        get => GetValue(MaximumDateProperty);
        set => SetValue(MaximumDateProperty, value);
    }

    /// <summary>Culture for names, week start, headline and input parsing. Defaults to the current culture.</summary>
    public CultureInfo? Culture
    {
        get => GetValue(CultureProperty);
        set => SetValue(CultureProperty, value);
    }

    /// <summary>Shows editable date fields instead of the calendar.</summary>
    public bool IsInputMode
    {
        get => GetValue(IsInputModeProperty);
        set => SetValue(IsInputModeProperty, value);
    }

    public bool ShowActions
    {
        get => GetValue(ShowActionsProperty);
        set => SetValue(ShowActionsProperty, value);
    }

    /// <summary>Whether the clear action may be shown. It is additionally hidden with no selection.</summary>
    public bool ShowClearButton
    {
        get => GetValue(ShowClearButtonProperty);
        set => SetValue(ShowClearButtonProperty, value);
    }

    /// <summary>Shows the header edit button that switches to <see cref="IsInputMode"/>.</summary>
    public bool AllowInputMode
    {
        get => GetValue(AllowInputModeProperty);
        set => SetValue(AllowInputModeProperty, value);
    }

    /// <summary>Supporting label. Defaults to the localized "Select date" resource.</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Overrides the culture short date pattern for text input and the headline.</summary>
    public string? DateFormat
    {
        get => GetValue(DateFormatProperty);
        set => SetValue(DateFormatProperty, value);
    }

    /// <summary>Number of consecutive months rendered by the full-screen presentation.</summary>
    public int MonthCount
    {
        get => GetValue(MonthCountProperty);
        set => SetValue(MonthCountProperty, value);
    }

    /// <summary>
    /// Renders adjacent-month days dimmed and selectable. When false the leading and trailing
    /// cells are empty placeholders (the source "Null" calendar cell type).
    /// </summary>
    public bool ShowAdjacentDays
    {
        get => GetValue(ShowAdjacentDaysProperty);
        set => SetValue(ShowAdjacentDaysProperty, value);
    }

    /// <summary>Overrides today, useful for deterministic tests and previews.</summary>
    public DateTime? TodayOverride
    {
        get => GetValue(TodayOverrideProperty);
        set => SetValue(TodayOverrideProperty, value);
    }

    /// <summary>The localized large date shown in the header.</summary>
    public string Headline => _headline;

    /// <summary><see cref="Label"/> or the localized default.</summary>
    public string EffectiveLabel => _effectiveLabel;

    /// <summary>The month and year caption of the navigation button.</summary>
    public string MonthYearText => _monthYearText;

    /// <summary>The localized short-date pattern shown as the input placeholder.</summary>
    public string Placeholder => _placeholder;

    /// <summary>The selection formatted for automation clients.</summary>
    public string AutomationValue => _automationValue;

    /// <summary>Text-input validation message; null when the input is valid.</summary>
    public string? ValidationMessage => _validationMessage;

    public bool HasSelection => _hasSelection;

    public bool IsRange => _isRange;

    private CultureInfo EffectiveCulture => Culture ?? CultureInfo.CurrentCulture;

    private DateTimeFormatInfo Format => EffectiveCulture.DateTimeFormat;

    private DayOfWeek FirstDayOfWeek => Format.FirstDayOfWeek;

    private DateTime Today => TodayOverride?.Date ?? DateTime.Today;

    /// <summary>The first day of the month currently displayed.</summary>
    private DateTime DisplayMonth
    {
        get
        {
            var value = DisplayDate?.Date ?? SelectedDate?.Date ?? Today;
            return ClampMonthToBounds(new DateTime(value.Year, value.Month, 1));
        }
    }

    private string EffectiveDateFormat =>
        string.IsNullOrWhiteSpace(DateFormat) ? Format.ShortDatePattern : DateFormat;

    /// <summary>Converts a calendar date to a local-midnight <see cref="DateTimeOffset"/>.</summary>
    public static DateTimeOffset ToDateTimeOffset(DateTime date) =>
        new(DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified), TimeZoneInfo.Local.GetUtcOffset(date.Date));

    /// <summary>True when the date is inside the configured inclusive bounds.</summary>
    public bool IsDateAllowed(DateTime date)
    {
        var value = date.Date;
        return (MinimumDate is not { } min || value >= min.Date) &&
               (MaximumDate is not { } max || value <= max.Date);
    }

    /// <summary>Formats a date using <see cref="DateFormat"/> or the culture short date pattern.</summary>
    public string FormatDate(DateTime date) => date.ToString(EffectiveDateFormat, EffectiveCulture);

    /// <summary>Parses culture-aware input, including ISO dates, and rejects out-of-bounds values.</summary>
    public bool TryParseDate(string? text, out DateTime date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;
        var culture = EffectiveCulture;
        string[] formats =
        [
            EffectiveDateFormat,
            culture.DateTimeFormat.ShortDatePattern,
            "yyyy-MM-dd",
            "yyyy/MM/dd",
        ];
        if (DateTime.TryParseExact(text.Trim(), formats, culture, DateTimeStyles.AllowWhiteSpaces, out var parsed) ||
            DateTime.TryParse(text.Trim(), culture, DateTimeStyles.AllowWhiteSpaces, out parsed))
        {
            date = parsed.Date;
            return true;
        }
        return false;
    }

    /// <summary>Selects a date. In range mode the first call sets the start and the second the end.</summary>
    public void Select(DateTime date)
    {
        if (!IsDateAllowed(date))
            return;

        date = date.Date;
        _focusDate = date;
        _focusDateSet = true;
        _previewDate = null;

        if (SelectionMode == MaterialDateSelectionMode.Single)
        {
            ApplySelection(date, null);
            _rangeAnchor = null;
            _awaitingRangeEnd = false;
            return;
        }

        if (_awaitingRangeEnd && _rangeAnchor is { } anchor)
        {
            var start = date < anchor ? date : anchor;
            var end = date < anchor ? anchor : date;
            ApplySelection(start, end);
            _rangeAnchor = null;
            _awaitingRangeEnd = false;
        }
        else
        {
            _rangeAnchor = date;
            _awaitingRangeEnd = true;
            ApplySelection(date, null);
        }
    }

    /// <summary>Removes the selection and resets the pending range endpoint.</summary>
    public void ClearSelection()
    {
        _rangeAnchor = null;
        _awaitingRangeEnd = false;
        _previewDate = null;
        _validationMessage = null;
        ApplySelection(null, null);
        Cleared?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Commits text input (when visible) and raises <see cref="Confirmed"/>.</summary>
    public void Commit()
    {
        if (IsInputMode)
        {
            var box = _endInput is { IsVisible: true, IsFocused: true } ? _endInput : _startInput;
            if (box is not null && !CommitInput(box, ReferenceEquals(box, _endInput)))
                return;
        }
        Confirmed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Raises <see cref="Cancelled"/> without changing the selection.</summary>
    public void Cancel() => Cancelled?.Invoke(this, EventArgs.Empty);

    /// <summary>Moves the displayed month by the given amount, clamped to the bounds.</summary>
    public void NavigateMonth(int delta) => SetDisplayMonth(DisplayMonth.AddMonths(delta));

    /// <summary>Moves the displayed year by the given amount, clamped to the bounds.</summary>
    public void NavigateYear(int delta) => SetDisplayMonth(DisplayMonth.AddMonths(delta * 12));

    /// <summary>Shows the month of today (or the nearest allowed month).</summary>
    public void GoToToday()
    {
        var today = Today;
        SetDisplayMonth(new DateTime(today.Year, today.Month, 1));
        if (IsDateAllowed(today))
            MoveFocus(today);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        DetachParts();
        base.OnApplyTemplate(e);

        _headerLabel = e.NameScope.Find<TextBlock>("PART_HeaderLabel");
        _headerHeadline = e.NameScope.Find<TextBlock>("PART_HeaderHeadline");
        _editButton = e.NameScope.Find<Button>("PART_EditButton");
        _closeButton = e.NameScope.Find<Button>("PART_CloseButton");
        _saveButton = e.NameScope.Find<Button>("PART_SaveButton");
        _prevMonthButton = e.NameScope.Find<Button>("PART_PrevMonthButton");
        _monthMenuButton = e.NameScope.Find<Button>("PART_MonthMenuButton");
        _nextMonthButton = e.NameScope.Find<Button>("PART_NextMonthButton");
        _prevYearButton = e.NameScope.Find<Button>("PART_PrevYearButton");
        _yearMenuButton = e.NameScope.Find<Button>("PART_YearMenuButton");
        _nextYearButton = e.NameScope.Find<Button>("PART_NextYearButton");
        _prevButton = e.NameScope.Find<Button>("PART_PrevButton");
        _monthYearButton = e.NameScope.Find<Button>("PART_MonthYearButton");
        _nextButton = e.NameScope.Find<Button>("PART_NextButton");
        _weekdayRow = e.NameScope.Find<UniformGrid>("PART_WeekdayRow");
        _dayGrid = e.NameScope.Find<UniformGrid>("PART_DayGrid");
        _choiceHost = e.NameScope.Find<Panel>("PART_ChoiceHost");
        _fullScreenWeekdayRow = e.NameScope.Find<UniformGrid>("PART_FullScreenWeekdayRow");
        _monthGroup = e.NameScope.Find<StackPanel>("PART_MonthGroup");
        _startInput = e.NameScope.Find<TextBox>("PART_StartInput");
        _endInput = e.NameScope.Find<TextBox>("PART_EndInput");
        _inputError = e.NameScope.Find<TextBlock>("PART_InputError");
        _actions = e.NameScope.Find<Border>("PART_Actions");
        _clearButton = e.NameScope.Find<Button>("PART_ClearButton");
        _cancelButton = e.NameScope.Find<Button>("PART_CancelButton");
        _okButton = e.NameScope.Find<Button>("PART_OkButton");

        _dayCells.Clear();
        _fullScreenCells.Clear();
        _choiceCells.Clear();
        _dayGrid?.Children.Clear();
        _choiceHost?.Children.Clear();
        _monthGroup?.Children.Clear();
        _weekdayRow?.Children.Clear();
        _fullScreenWeekdayRow?.Children.Clear();
        _fullScreenSignature = null;

        if (_editButton is not null)
            _editButton.Click += OnEditButtonClick;
        if (_closeButton is not null)
            _closeButton.Click += OnCloseButtonClick;
        if (_saveButton is not null)
            _saveButton.Click += OnSaveButtonClick;
        if (_prevMonthButton is not null)
            _prevMonthButton.Click += OnPreviousMonthClick;
        if (_nextMonthButton is not null)
            _nextMonthButton.Click += OnNextMonthClick;
        if (_prevYearButton is not null)
            _prevYearButton.Click += OnPreviousYearClick;
        if (_nextYearButton is not null)
            _nextYearButton.Click += OnNextYearClick;
        if (_monthMenuButton is not null)
            _monthMenuButton.Click += OnMonthMenuClick;
        if (_yearMenuButton is not null)
            _yearMenuButton.Click += OnYearMenuClick;
        if (_prevButton is not null)
            _prevButton.Click += OnPreviousMonthClick;
        if (_nextButton is not null)
            _nextButton.Click += OnNextMonthClick;
        if (_monthYearButton is not null)
            _monthYearButton.Click += OnMonthYearButtonClick;
        if (_clearButton is not null)
            _clearButton.Click += OnClearButtonClick;
        if (_cancelButton is not null)
            _cancelButton.Click += OnCancelButtonClick;
        if (_okButton is not null)
            _okButton.Click += OnOkButtonClick;
        if (_startInput is not null)
        {
            _startInput.KeyDown += OnStartInputKeyDown;
            _startInput.LostFocus += OnStartInputLostFocus;
        }
        if (_endInput is not null)
        {
            _endInput.KeyDown += OnEndInputKeyDown;
            _endInput.LostFocus += OnEndInputLostFocus;
        }
        if (_dayGrid is not null)
            _dayGrid.PointerExited += OnDayGridPointerExited;

        _applied = true;
        EnsureFocusDate();
        RefreshVisualState();
        BuildStructure();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == MinimumDateProperty || change.Property == MaximumDateProperty)
        {
            CoerceValue(SelectedDateProperty);
            CoerceValue(SelectedEndDateProperty);
            _fullScreenSignature = null;
        }

        if (change.Property == SelectedDateProperty || change.Property == SelectedEndDateProperty)
        {
            if (_settingSelection || _normalizing)
                return;
            NormalizeSelection();
            UpdateFocusFromSelection();
            RefreshVisualState();
            RaiseSelectionChanged();
            return;
        }

        if (change.Property == SelectionModeProperty || change.Property == ModeProperty ||
            change.Property == ViewProperty || change.Property == DisplayDateProperty ||
            change.Property == CultureProperty || change.Property == MonthCountProperty ||
            change.Property == MinimumDateProperty || change.Property == MaximumDateProperty ||
            change.Property == IsInputModeProperty)
        {
            NormalizeSelection();
            _fullScreenSignature = null;
            EnsureFocusDate();
            BuildStructure();
            return;
        }

        if (change.Property == LabelProperty || change.Property == ShowActionsProperty ||
            change.Property == ShowClearButtonProperty || change.Property == AllowInputModeProperty ||
            change.Property == TodayOverrideProperty || change.Property == ShowAdjacentDaysProperty)
        {
            RefreshVisualState();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
            return;

        if (e.Key == Key.Escape)
        {
            if (View != MaterialDatePickerView.Day)
                SetCurrentValue(ViewProperty, MaterialDatePickerView.Day);
            else
                Cancel();
            e.Handled = true;
            return;
        }

        if (IsInputMode || e.Source is TextBox)
            return;

        var handled = View switch
        {
            MaterialDatePickerView.Day => HandleDayKey(e),
            MaterialDatePickerView.Month => HandleMonthKey(e),
            MaterialDatePickerView.Year => HandleYearKey(e),
            _ => false,
        };
        if (handled)
            e.Handled = true;
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        if (e.Source == this)
            FocusActiveCell();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => _peer = new DatePickerPeer(this);

    private static DateTimeOffset? CoerceDate(AvaloniaObject instance, DateTimeOffset? value)
    {
        if (value is not { } date)
            return null;
        var picker = (MaterialDatePicker)instance;
        var day = date.Date;
        if (picker.MinimumDate is { } min && day < min.Date)
            return min;
        if (picker.MaximumDate is { } max && day > max.Date)
            return max;
        return date;
    }

    private void DetachParts()
    {
        if (_editButton is not null) _editButton.Click -= OnEditButtonClick;
        if (_closeButton is not null) _closeButton.Click -= OnCloseButtonClick;
        if (_saveButton is not null) _saveButton.Click -= OnSaveButtonClick;
        if (_prevMonthButton is not null) _prevMonthButton.Click -= OnPreviousMonthClick;
        if (_nextMonthButton is not null) _nextMonthButton.Click -= OnNextMonthClick;
        if (_prevYearButton is not null) _prevYearButton.Click -= OnPreviousYearClick;
        if (_nextYearButton is not null) _nextYearButton.Click -= OnNextYearClick;
        if (_monthMenuButton is not null) _monthMenuButton.Click -= OnMonthMenuClick;
        if (_yearMenuButton is not null) _yearMenuButton.Click -= OnYearMenuClick;
        if (_prevButton is not null) _prevButton.Click -= OnPreviousMonthClick;
        if (_nextButton is not null) _nextButton.Click -= OnNextMonthClick;
        if (_monthYearButton is not null) _monthYearButton.Click -= OnMonthYearButtonClick;
        if (_clearButton is not null) _clearButton.Click -= OnClearButtonClick;
        if (_cancelButton is not null) _cancelButton.Click -= OnCancelButtonClick;
        if (_okButton is not null) _okButton.Click -= OnOkButtonClick;
        if (_startInput is not null)
        {
            _startInput.KeyDown -= OnStartInputKeyDown;
            _startInput.LostFocus -= OnStartInputLostFocus;
        }
        if (_endInput is not null)
        {
            _endInput.KeyDown -= OnEndInputKeyDown;
            _endInput.LostFocus -= OnEndInputLostFocus;
        }
        if (_dayGrid is not null)
            _dayGrid.PointerExited -= OnDayGridPointerExited;
        _dayCells.Clear();
        _fullScreenCells.Clear();
        _choiceCells.Clear();
    }

    private void ApplySelection(DateTime? start, DateTime? end)
    {
        _settingSelection = true;
        try
        {
            SetCurrentValue(SelectedDateProperty, start is { } s ? ToDateTimeOffset(s) : null);
            SetCurrentValue(SelectedEndDateProperty, end is { } e ? ToDateTimeOffset(e) : null);
        }
        finally
        {
            _settingSelection = false;
        }
        NormalizeSelection();
        RefreshVisualState();
        RaiseSelectionChanged();
    }

    private void NormalizeSelection()
    {
        if (_normalizing)
            return;
        _normalizing = true;
        try
        {
            var start = SelectedDate;
            var end = SelectedEndDate;
            if (SelectionMode == MaterialDateSelectionMode.Single)
            {
                end = null;
            }
            else if (start is null && end is not null)
            {
                start = end;
                end = null;
            }
            else if (start is not null && end is not null && end.Value.Date < start.Value.Date)
            {
                (start, end) = (end, start);
            }

            if (start != SelectedDate)
                SetCurrentValue(SelectedDateProperty, start);
            if (end != SelectedEndDate)
                SetCurrentValue(SelectedEndDateProperty, end);
        }
        finally
        {
            _normalizing = false;
        }
    }

    private void UpdateFocusFromSelection()
    {
        if (SelectedDate is { } selected)
        {
            _focusDate = selected.Date;
            _focusDateSet = true;
        }
    }

    private void EnsureFocusDate()
    {
        if (!_focusDateSet)
        {
            _focusDate = ClampToBounds(DisplayMonth) ?? DisplayMonth;
            _focusDateSet = true;
        }
        else if (!IsDateAllowed(_focusDate))
        {
            _focusDate = ClampToBounds(_focusDate) ?? DisplayMonth;
        }
    }

    private void RaiseSelectionChanged()
    {
        var complete = SelectionMode == MaterialDateSelectionMode.Range &&
                       SelectedDate is not null && SelectedEndDate is not null;
        SelectionChanged?.Invoke(this, new MaterialDateSelectionChangedEventArgs(
            SelectedDate, SelectedEndDate, complete));

        var value = ComputeAutomationValue();
        if (value != _automationValue)
        {
            var old = _automationValue;
            SetAndRaise(AutomationValueProperty, ref _automationValue, value);
            _peer?.NotifyValueChanged(old, value);
        }
    }

    private string ComputeAutomationValue()
    {
        if (SelectedDate is not { } start)
            return string.Empty;
        var startDate = start.Date;
        if (SelectionMode == MaterialDateSelectionMode.Range && SelectedEndDate is { } end)
            return $"{FormatDate(startDate)} – {FormatDate(end.Date)}";
        return FormatDate(startDate);
    }

    private void BuildStructure()
    {
        if (!_applied)
            return;

        BuildWeekdayRow(_weekdayRow);
        BuildWeekdayRow(_fullScreenWeekdayRow);
        BuildDayGrid();
        BuildFullScreenMonths();
        BuildChoiceHost();
        UpdateNavigationState();
        SyncInputText(force: true);
        RefreshVisualState();
    }

    private void BuildWeekdayRow(UniformGrid? row)
    {
        if (row is null)
            return;
        var signature = EffectiveCulture.Name;
        if (row.Tag as string == signature && row.Children.Count == 7)
            return;
        row.Tag = signature;
        row.Children.Clear();
        var names = Format.ShortestDayNames;
        var full = Format.DayNames;
        var first = (int)FirstDayOfWeek;
        for (var i = 0; i < 7; i++)
        {
            var index = (first + i) % 7;
            var label = new TextBlock
            {
                Text = names[index],
                Width = 48,
                Height = 48,
                FontSize = 16,
                LineHeight = 24,
                LetterSpacing = 0.5,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            if (GetBrush("MaterialOnSurfaceBrush") is { } brush)
                label.Foreground = brush;
            AutomationProperties.SetName(label, full[index]);
            AutomationProperties.SetControlTypeOverride(label, AutomationControlType.Header);
            row.Children.Add(label);
        }
    }

    private void BuildDayGrid()
    {
        if (_dayGrid is null)
            return;

        var display = DisplayMonth;
        var first = new DateTime(display.Year, display.Month, 1);
        var offset = ((int)first.DayOfWeek - (int)FirstDayOfWeek + 7) % 7;
        var start = first.AddDays(-offset);
        var culture = EffectiveCulture;

        if (_dayCells.Count != DayCellCount)
        {
            _dayCells.Clear();
            _dayGrid.Children.Clear();
            for (var i = 0; i < DayCellCount; i++)
            {
                var cell = new MaterialDateCell();
                cell.Click += OnDayCellClick;
                cell.GotFocus += (_, _) =>
                {
                    if (cell.Tag is DateTime focused)
                        _focusDate = focused;
                };
                cell.PointerEntered += (_, _) =>
                {
                    if (cell.Tag is DateTime hovered)
                        SetPreview(hovered);
                };
                _dayCells.Add(cell);
                _dayGrid.Children.Add(cell);
            }
        }

        for (var i = 0; i < DayCellCount; i++)
        {
            var date = start.AddDays(i);
            var cell = _dayCells[i];
            cell.Tag = date;
            cell.ContextMonth = display;
            cell.Content = date.Day.ToString(culture);
        }
    }

    private void BuildFullScreenMonths()
    {
        if (_monthGroup is null)
            return;
        if (Mode != MaterialDatePickerMode.FullScreen)
        {
            if (_fullScreenCells.Count > 0 || _monthGroup.Children.Count > 0)
            {
                _monthGroup.Children.Clear();
                _fullScreenCells.Clear();
                _fullScreenSignature = null;
            }
            return;
        }

        var display = DisplayMonth;
        var count = Math.Max(1, MonthCount);
        var signature = FormattableString.Invariant(
            $"{display:yyyyMM}|{count}|{EffectiveCulture.Name}|{MinimumDate?.Date:yyyyMMdd}|{MaximumDate?.Date:yyyyMMdd}");
        if (_fullScreenSignature == signature)
            return;

        _fullScreenSignature = signature;
        _monthGroup.Children.Clear();
        _fullScreenCells.Clear();
        var culture = EffectiveCulture;

        for (var index = 0; index < count; index++)
        {
            var month = display.AddMonths(index);
            var block = new StackPanel();
            var header = new TextBlock
            {
                Text = month.ToString("MMMM yyyy", culture),
                Margin = new Thickness(24, 16, 24, 12),
                FontSize = 16,
                LineHeight = 24,
                FontWeight = FontWeight.Medium,
            };
            if (GetBrush("MaterialOnSurfaceBrush") is { } brush)
                header.Foreground = brush;
            AutomationProperties.SetName(header, month.ToString("MMMM yyyy", culture));
            AutomationProperties.SetControlTypeOverride(header, AutomationControlType.Header);
            block.Children.Add(header);

            var grid = new UniformGrid { Columns = 7, HorizontalAlignment = HorizontalAlignment.Center };
            var first = new DateTime(month.Year, month.Month, 1);
            var offset = ((int)first.DayOfWeek - (int)FirstDayOfWeek + 7) % 7;
            var start = first.AddDays(-offset);
            for (var i = 0; i < DayCellCount; i++)
            {
                var date = start.AddDays(i);
                var cell = new MaterialDateCell
                {
                    Tag = date,
                    ContextMonth = month,
                    Content = date.Day.ToString(culture),
                };
                cell.Click += OnDayCellClick;
                cell.GotFocus += (_, _) =>
                {
                    if (cell.Tag is DateTime focused)
                        _focusDate = focused;
                };
                cell.PointerEntered += (_, _) =>
                {
                    if (cell.Tag is DateTime hovered)
                        SetPreview(hovered);
                };
                _fullScreenCells.Add(cell);
                grid.Children.Add(cell);
            }
            block.Children.Add(grid);
            _monthGroup.Children.Add(block);
        }
    }

    private void BuildChoiceHost()
    {
        if (_choiceHost is null)
            return;
        _choiceHost.Children.Clear();
        _choiceCells.Clear();

        if (View == MaterialDatePickerView.Day)
            return;

        var display = DisplayMonth;
        var list = Mode == MaterialDatePickerMode.Docked;

        if (View == MaterialDatePickerView.Month)
        {
            var months = list ? Format.MonthNames : Format.AbbreviatedMonthNames;
            if (list)
            {
                var stack = new StackPanel();
                for (var month = 1; month <= MonthChoiceCount; month++)
                {
                    var row = new MaterialDateChoiceRow
                    {
                        Content = months[month - 1],
                        Tag = month,
                    };
                    row.Click += OnMonthChoiceClick;
                    _choiceCells.Add(((IMaterialDateChoiceCell)row, month));
                    stack.Children.Add(row);
                }
                _choiceHost.Children.Add(stack);
            }
            else
            {
                var grid = new UniformGrid { Columns = 3 };
                for (var month = 1; month <= MonthChoiceCount; month++)
                {
                    var chip = new MaterialDateChoiceChip
                    {
                        Content = months[month - 1],
                        Tag = month,
                    };
                    chip.Click += OnMonthChoiceClick;
                    _choiceCells.Add(((IMaterialDateChoiceCell)chip, month));
                    grid.Children.Add(chip);
                }
                _choiceHost.Children.Add(grid);
            }
            return;
        }

        var startYear = display.Year - 5;
        if (list)
        {
            var stack = new StackPanel();
            for (var i = 0; i < YearChoiceCount; i++)
            {
                var year = startYear + i;
                var row = new MaterialDateChoiceRow
                {
                    Content = year.ToString(EffectiveCulture),
                    Tag = year,
                };
                row.Click += OnYearChoiceClick;
                _choiceCells.Add(((IMaterialDateChoiceCell)row, year));
                stack.Children.Add(row);
            }
            _choiceHost.Children.Add(stack);
        }
        else
        {
            var grid = new UniformGrid { Columns = 3 };
            for (var i = 0; i < YearChoiceCount; i++)
            {
                var year = startYear + i;
                var chip = new MaterialDateChoiceChip
                {
                    Content = year.ToString(EffectiveCulture),
                    Tag = year,
                };
                chip.Click += OnYearChoiceClick;
                _choiceCells.Add(((IMaterialDateChoiceCell)chip, year));
                grid.Children.Add(chip);
            }
            _choiceHost.Children.Add(grid);
        }
    }

    private void RefreshVisualState()
    {
        UpdateEffectiveLabel();
        UpdateHeadline();
        UpdateMonthYearText();
        UpdatePlaceholder();

        var hasSelection = SelectedDate is not null;
        if (hasSelection != _hasSelection)
            SetAndRaise(HasSelectionProperty, ref _hasSelection, hasSelection);
        var isRange = SelectionMode == MaterialDateSelectionMode.Range;
        if (isRange != _isRange)
            SetAndRaise(IsRangeProperty, ref _isRange, isRange);

        if (!_applied)
            return;

        UpdatePseudoClasses();
        ApplyCellStates(_dayCells);
        ApplyCellStates(_fullScreenCells);
        RefreshChoiceStates();
        UpdateNavigationState();
        UpdateActionVisibility();
        SyncInputText(force: false);

        if (_headerLabel is not null)
            _headerLabel.Text = _effectiveLabel;
        if (_headerHeadline is not null)
            _headerHeadline.Text = _headline;
        if (_monthYearButton is not null)
            _monthYearButton.Content = _monthYearText;
        if (_monthMenuButton is not null)
            _monthMenuButton.Content = Format.AbbreviatedMonthNames[DisplayMonth.Month - 1];
        if (_yearMenuButton is not null)
            _yearMenuButton.Content = DisplayMonth.Year.ToString(EffectiveCulture);
        if (_editButton is not null)
        {
            var editName = IsInputMode
                ? GetText("MaterialDatePickerSwitchToCalendarText", "Switch to calendar")
                : GetText("MaterialDatePickerEditDateText", "Switch to text input");
            AutomationProperties.SetName(_editButton, editName);
            ToolTip.SetTip(_editButton, editName);
        }
        if (_inputError is not null)
        {
            _inputError.Text = _validationMessage;
            _inputError.IsVisible = !string.IsNullOrEmpty(_validationMessage);
        }
        if (_endInput is not null)
            _endInput.IsVisible = isRange;
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(":docked", Mode == MaterialDatePickerMode.Docked);
        PseudoClasses.Set(":modal", Mode == MaterialDatePickerMode.Modal);
        PseudoClasses.Set(":fullscreen", Mode == MaterialDatePickerMode.FullScreen);
        PseudoClasses.Set(":text-input", IsInputMode);
        PseudoClasses.Set(":range", SelectionMode == MaterialDateSelectionMode.Range);
        PseudoClasses.Set(":view-day", View == MaterialDatePickerView.Day);
        PseudoClasses.Set(":view-month", View == MaterialDatePickerView.Month);
        PseudoClasses.Set(":view-year", View == MaterialDatePickerView.Year);
        PseudoClasses.Set(":has-selection", HasSelection);
        PseudoClasses.Set(":no-actions", !ShowActions);
        PseudoClasses.Set(":no-input-mode", !AllowInputMode);
    }

    private void ApplyCellStates(IReadOnlyList<MaterialDateCell> cells)
    {
        if (cells.Count == 0)
            return;

        var start = SelectedDate?.Date;
        var end = SelectedEndDate?.Date;
        var range = SelectionMode == MaterialDateSelectionMode.Range;
        var completedRange = range && start is not null && end is not null && start != end;
        var today = Today;
        var culture = EffectiveCulture;

        DateTime? previewStart = null;
        DateTime? previewEnd = null;
        if (range && _awaitingRangeEnd && _rangeAnchor is { } anchor)
        {
            var hover = _previewDate ?? anchor;
            previewStart = hover < anchor ? hover : anchor;
            previewEnd = hover < anchor ? anchor : hover;
        }
        var hasPreview = previewStart is not null && previewEnd > previewStart;

        for (var i = 0; i < cells.Count; i++)
        {
            var cell = cells[i];
            if (cell.Tag is not DateTime date)
                continue;

            var isStart = start == date;
            var isEnd = end == date;
            var selected = isStart || (isEnd && end is not null);
            var middle = completedRange && start < date && date < end;
            var outside = date.Month != cell.ContextMonth.Month || date.Year != cell.ContextMonth.Year;
            var hidden = outside && !ShowAdjacentDays;

            cell.SetState(":selected", selected);
            cell.SetState(":today", date == today);
            cell.SetState(":outside", outside);
            cell.SetState(":empty", hidden);
            cell.SetState(":range-start", completedRange && isStart);
            cell.SetState(":range-end", completedRange && isEnd);
            cell.SetState(":range-middle", middle);
            cell.SetState(":range-preview-start", hasPreview && previewStart == date);
            cell.SetState(":range-preview-end", hasPreview && previewEnd == date);
            cell.SetState(":range-preview-middle",
                hasPreview && previewStart < date && date < previewEnd);
            cell.Content = hidden ? null : date.Day.ToString(culture);
            cell.IsEnabled = !hidden && IsDateAllowed(date);

            AutomationProperties.SetName(cell, hidden ? string.Empty : date.ToString("D", culture));
            var help = hidden
                ? null
                : outside
                    ? GetText("MaterialDatePickerOutsideMonthText", "Outside month")
                    : middle
                        ? GetText("MaterialDatePickerInRangeText", "In selected range")
                        : selected && range && end is not null
                            ? GetText("MaterialDatePickerRangeEndpointText", "Selected range endpoint")
                            : date == today
                                ? GetText("MaterialDatePickerTodayText", "Today")
                                : null;
            AutomationProperties.SetHelpText(cell, help);
            AutomationProperties.SetControlTypeOverride(cell, AutomationControlType.Button);
        }
    }

    private void RefreshChoiceStates()
    {
        if (_choiceCells.Count == 0)
            return;
        var start = SelectedDate?.Date;
        var end = SelectedEndDate?.Date;
        var display = DisplayMonth;
        foreach (var (cell, value) in _choiceCells)
        {
            var selected = View == MaterialDatePickerView.Month
                ? IsMonthSelected(display.Year, value, start, end)
                : IsYearSelected(value, start, end);
            cell.SetSelected(selected);
        }
    }

    private static bool IsMonthSelected(int year, int month, DateTime? start, DateTime? end)
    {
        if (start is not { } s)
            return false;
        var first = new DateTime(year, month, 1);
        var last = first.AddMonths(1).AddDays(-1);
        var stop = end ?? s;
        return s <= last && stop >= first;
    }

    private static bool IsYearSelected(int year, DateTime? start, DateTime? end)
    {
        if (start is not { } s)
            return false;
        var first = new DateTime(year, 1, 1);
        var last = new DateTime(year, 12, 31);
        var stop = end ?? s;
        return s <= last && stop >= first;
    }

    private void UpdateNavigationState()
    {
        var display = DisplayMonth;
        var canPreviousMonth = CanShowMonth(display.AddMonths(-1));
        var canNextMonth = CanShowMonth(display.AddMonths(1));
        var canPreviousYear = CanShowMonth(display.AddMonths(-12));
        var canNextYear = CanShowMonth(display.AddMonths(12));

        if (_prevMonthButton is not null) _prevMonthButton.IsEnabled = canPreviousMonth;
        if (_nextMonthButton is not null) _nextMonthButton.IsEnabled = canNextMonth;
        if (_prevButton is not null) _prevButton.IsEnabled = canPreviousMonth;
        if (_nextButton is not null) _nextButton.IsEnabled = canNextMonth;
        if (_prevYearButton is not null) _prevYearButton.IsEnabled = canPreviousYear;
        if (_nextYearButton is not null) _nextYearButton.IsEnabled = canNextYear;
        if (_editButton is not null) _editButton.IsVisible = AllowInputMode && Mode != MaterialDatePickerMode.FullScreen;
    }

    private bool CanShowMonth(DateTime month)
    {
        var first = new DateTime(month.Year, month.Month, 1);
        var last = first.AddMonths(1).AddDays(-1);
        return (MinimumDate is not { } min || last.Date >= min.Date) &&
               (MaximumDate is not { } max || first.Date <= max.Date);
    }

    private void UpdateActionVisibility()
    {
        var showActions = ShowActions && Mode != MaterialDatePickerMode.FullScreen;
        if (_actions is not null)
            _actions.IsVisible = showActions;
        if (_clearButton is not null)
            _clearButton.IsVisible = ShowClearButton && HasSelection && showActions;
        if (_cancelButton is not null)
            _cancelButton.IsVisible = showActions;
        if (_okButton is not null)
            _okButton.IsVisible = showActions;
    }

    private void UpdateEffectiveLabel()
    {
        var value = string.IsNullOrWhiteSpace(Label)
            ? GetText("MaterialDatePickerSelectDateText", "Select date")
            : Label!;
        if (value != _effectiveLabel)
            SetAndRaise(EffectiveLabelProperty, ref _effectiveLabel, value);
    }

    private void UpdateHeadline()
    {
        var value = ComputeHeadline();
        if (value != _headline)
            SetAndRaise(HeadlineProperty, ref _headline, value);
    }

    private string ComputeHeadline()
    {
        var culture = EffectiveCulture;
        if (IsInputMode && SelectedDate is null)
            return GetText("MaterialDatePickerEnterDateText", "Enter date");
        if (SelectedDate is not { } start)
            return GetText("MaterialDatePickerNoDateText", "No date selected");
        var startDate = start.Date;
        if (SelectionMode == MaterialDateSelectionMode.Range && SelectedEndDate is { } end)
        {
            var endDate = end.Date;
            var pattern = startDate.Year == endDate.Year ? "MMM d" : "MMM d, yyyy";
            return $"{startDate.ToString(pattern, culture)} – {endDate.ToString(pattern, culture)}";
        }
        return startDate.ToString("ddd, MMM d", culture);
    }

    private void UpdateMonthYearText()
    {
        var value = DisplayMonth.ToString(Format.YearMonthPattern, EffectiveCulture);
        if (value != _monthYearText)
            SetAndRaise(MonthYearTextProperty, ref _monthYearText, value);
    }

    private void UpdatePlaceholder()
    {
        var value = EffectiveDateFormat.ToUpperInvariant();
        if (value != _placeholder)
            SetAndRaise(PlaceholderProperty, ref _placeholder, value);
    }

    private void SyncInputText(bool force)
    {
        if (_startInput is not null && (force || !_startInput.IsFocused))
            _startInput.Text = SelectedDate is { } start ? FormatDate(start.Date) : string.Empty;
        if (_endInput is not null && (force || !_endInput.IsFocused))
            _endInput.Text = SelectedEndDate is { } end ? FormatDate(end.Date) : string.Empty;
    }

    private void SetPreview(DateTime date)
    {
        if (SelectionMode != MaterialDateSelectionMode.Range || !_awaitingRangeEnd)
            return;
        if (_previewDate == date)
            return;
        _previewDate = date;
        ApplyCellStates(_dayCells);
        ApplyCellStates(_fullScreenCells);
    }

    private void OnDayGridPointerExited(object? sender, PointerEventArgs e)
    {
        if (_previewDate is null)
            return;
        _previewDate = null;
        ApplyCellStates(_dayCells);
        ApplyCellStates(_fullScreenCells);
    }

    private void OnDayCellClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not MaterialDateCell { Tag: DateTime date })
            return;
        var display = DisplayMonth;
        if (Mode != MaterialDatePickerMode.FullScreen &&
            (date.Year != display.Year || date.Month != display.Month) &&
            CanShowMonth(date))
        {
            SetDisplayMonth(new DateTime(date.Year, date.Month, 1));
        }
        Select(date);
    }

    private void OnMonthChoiceClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Control { Tag: int month })
            return;
        var target = new DateTime(DisplayMonth.Year, month, 1);
        SetDisplayMonth(target);
        SetCurrentValue(ViewProperty, MaterialDatePickerView.Day);
        var day = Math.Min(_focusDate.Day, DateTime.DaysInMonth(target.Year, target.Month));
        MoveFocus(new DateTime(target.Year, target.Month, day));
    }

    private void OnYearChoiceClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Control { Tag: int year })
            return;
        var target = new DateTime(year, DisplayMonth.Month, 1);
        SetDisplayMonth(target);
        SetCurrentValue(ViewProperty, MaterialDatePickerView.Day);
        var day = Math.Min(_focusDate.Day, DateTime.DaysInMonth(target.Year, target.Month));
        MoveFocus(new DateTime(target.Year, target.Month, day));
    }

    private void OnPreviousMonthClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => NavigateMonth(-1);

    private void OnNextMonthClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => NavigateMonth(1);

    private void OnPreviousYearClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => NavigateYear(-1);

    private void OnNextYearClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => NavigateYear(1);

    private void OnMonthMenuClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        SetCurrentValue(ViewProperty, MaterialDatePickerView.Month);

    private void OnYearMenuClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        SetCurrentValue(ViewProperty, MaterialDatePickerView.Year);

    private void OnMonthYearButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SetCurrentValue(ViewProperty, View switch
        {
            MaterialDatePickerView.Day => MaterialDatePickerView.Month,
            MaterialDatePickerView.Month => MaterialDatePickerView.Year,
            _ => MaterialDatePickerView.Day,
        });
    }

    private void OnEditButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var toInput = !IsInputMode;
        SetCurrentValue(IsInputModeProperty, toInput);
        if (!toInput)
        {
            Dispatcher.UIThread.Post(FocusActiveCell, DispatcherPriority.Background);
            return;
        }
        Dispatcher.UIThread.Post(() =>
        {
            var box = _startInput;
            if (box is { IsEffectivelyEnabled: true } && box.IsAttachedToVisualTree())
            {
                box.Focus(NavigationMethod.Tab);
                box.SelectAll();
            }
        }, DispatcherPriority.Background);
    }

    private void OnCloseButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Cancel();

    private void OnSaveButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Commit();

    private void OnClearButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => ClearSelection();

    private void OnCancelButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Cancel();

    private void OnOkButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Commit();

    private void OnStartInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _startInput is not null)
        {
            if (CommitInput(_startInput, false) && !IsRange)
                Commit();
            e.Handled = true;
        }
    }

    private void OnEndInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _endInput is not null)
        {
            if (CommitInput(_endInput, true))
                Commit();
            e.Handled = true;
        }
    }

    private void OnStartInputLostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_startInput is not null && !string.IsNullOrWhiteSpace(_startInput.Text))
            CommitInput(_startInput, false);
    }

    private void OnEndInputLostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_endInput is not null && !string.IsNullOrWhiteSpace(_endInput.Text))
            CommitInput(_endInput, true);
    }

    /// <summary>Parses a text field and applies the value. Returns false when validation fails.</summary>
    private bool CommitInput(TextBox box, bool isEnd)
    {
        var text = box.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            _validationMessage = null;
            if (isEnd)
                ApplySelection(SelectedDate?.Date, null);
            else
                ApplySelection(null, null);
            RefreshVisualState();
            return true;
        }

        if (!TryParseDate(text, out var date))
        {
            SetValidationMessage(GetText("MaterialDatePickerInvalidDateText", "Enter a valid date"));
            return false;
        }
        if (!IsDateAllowed(date))
        {
            SetValidationMessage(GetText("MaterialDatePickerOutOfRangeText", "Date is outside the allowed range"));
            return false;
        }

        _validationMessage = null;
        var start = SelectedDate?.Date;
        var end = SelectedEndDate?.Date;
        if (SelectionMode == MaterialDateSelectionMode.Single)
        {
            ApplySelection(date, null);
        }
        else if (isEnd)
        {
            var anchor = start ?? date;
            var from = date < anchor ? date : anchor;
            var to = date < anchor ? anchor : date;
            _rangeAnchor = from;
            _awaitingRangeEnd = false;
            ApplySelection(from, to);
        }
        else
        {
            // Typing the start keeps an end that is still on or after it; otherwise the range
            // restarts and the calendar previews the pending end.
            var keepEnd = end is { } existing && date <= existing;
            _rangeAnchor = date;
            _awaitingRangeEnd = !keepEnd;
            ApplySelection(date, keepEnd ? end : null);
        }
        RefreshVisualState();
        return true;
    }

    private void SetValidationMessage(string? message)
    {
        if (message == _validationMessage)
            return;
        var old = _validationMessage;
        _validationMessage = message;
        RaisePropertyChanged(ValidationMessageProperty, old, message);
        if (_inputError is not null)
        {
            _inputError.Text = message;
            _inputError.IsVisible = !string.IsNullOrEmpty(message);
        }
    }

    private void SetDisplayMonth(DateTime month)
    {
        var target = ClampMonthToBounds(new DateTime(month.Year, month.Month, 1));
        if (target == DisplayMonth && DisplayDate is not null)
            return;
        SetCurrentValue(DisplayDateProperty, ToDateTimeOffset(target));
    }

    private DateTime ClampMonthToBounds(DateTime month)
    {
        if (MinimumDate is { } min)
        {
            var first = new DateTime(min.Date.Year, min.Date.Month, 1);
            if (month < first)
                month = first;
        }
        if (MaximumDate is { } max)
        {
            var last = new DateTime(max.Date.Year, max.Date.Month, 1);
            if (month > last)
                month = last;
        }
        return month;
    }

    private DateTime? ClampToBounds(DateTime date)
    {
        if (MinimumDate is { } min && date.Date < min.Date)
            return min.Date;
        if (MaximumDate is { } max && date.Date > max.Date)
            return max.Date;
        return date;
    }

    private MaterialDateCell? FindDayCell(DateTime date)
    {
        foreach (var cell in _dayCells)
        {
            if (cell.Tag is DateTime value && value == date && cell.IsEffectivelyVisible)
                return cell;
        }
        foreach (var cell in _fullScreenCells)
        {
            if (cell.Tag is DateTime value && value == date && cell.IsEffectivelyVisible)
                return cell;
        }
        return null;
    }

    private void FocusActiveCell()
    {
        if (View == MaterialDatePickerView.Day)
        {
            var date = _focusDate;
            if (date.Year != DisplayMonth.Year || date.Month != DisplayMonth.Month)
                date = ClampToBounds(DisplayMonth) ?? DisplayMonth;
            var cell = FindDayCell(date);
            if (cell is null)
            {
                foreach (var candidate in _dayCells)
                {
                    if (candidate.IsEffectivelyVisible && candidate.IsEnabled)
                    {
                        cell = candidate;
                        break;
                    }
                }
            }
            cell?.Focus(NavigationMethod.Directional);
            return;
        }

        foreach (var (choice, value) in _choiceCells)
        {
            if (choice is Control control && control.IsEffectivelyVisible)
            {
                var matches = View == MaterialDatePickerView.Month
                    ? value == _focusDate.Month
                    : value == _focusDate.Year;
                if (matches)
                {
                    control.Focus(NavigationMethod.Directional);
                    return;
                }
            }
        }
        if (_choiceCells.Count > 0 && _choiceCells[0].Cell is Control first && first.IsEffectivelyVisible)
            first.Focus(NavigationMethod.Directional);
    }

    private void MoveFocus(DateTime date)
    {
        if (!IsDateAllowed(date))
            return;
        _focusDate = date;
        _focusDateSet = true;
        if (date.Year != DisplayMonth.Year || date.Month != DisplayMonth.Month)
            SetDisplayMonth(new DateTime(date.Year, date.Month, 1));
        Dispatcher.UIThread.Post(() =>
        {
            var cell = FindDayCell(date);
            cell?.Focus(NavigationMethod.Directional);
        }, DispatcherPriority.Background);
    }

    private bool HandleDayKey(KeyEventArgs e)
    {
        var date = _focusDate;
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var delta = e.Key switch
        {
            Key.Left => -1,
            Key.Right => 1,
            Key.Up => -7,
            Key.Down => 7,
            _ => 0,
        };

        if (delta != 0)
        {
            var next = ClampToBounds(date.AddDays(delta));
            if (next is { } value)
                MoveFocus(value);
            return true;
        }

        switch (e.Key)
        {
            case Key.PageUp:
                MoveFocus(ClampToBounds(date.AddMonths(shift ? -12 : -1)) ?? date);
                return true;
            case Key.PageDown:
                MoveFocus(ClampToBounds(date.AddMonths(shift ? 12 : 1)) ?? date);
                return true;
            case Key.Home:
                MoveFocus(StartOfWeek(date));
                return true;
            case Key.End:
                MoveFocus(StartOfWeek(date).AddDays(6));
                return true;
            case Key.Enter:
            case Key.Space:
                if (ReferenceEquals(e.Source, this))
                {
                    Select(date);
                    return true;
                }
                return false;
            default:
                return false;
        }
    }

    private bool HandleMonthKey(KeyEventArgs e)
    {
        var date = _focusDate;
        switch (e.Key)
        {
            case Key.Left:
                date = date.AddMonths(-1);
                break;
            case Key.Right:
                date = date.AddMonths(1);
                break;
            case Key.Up:
                date = date.AddMonths(-3);
                break;
            case Key.Down:
                date = date.AddMonths(3);
                break;
            case Key.PageUp:
                date = date.AddYears(-1);
                break;
            case Key.PageDown:
                date = date.AddYears(1);
                break;
            case Key.Home:
                date = new DateTime(DisplayMonth.Year, 1, 1);
                break;
            case Key.End:
                date = new DateTime(DisplayMonth.Year, 12, 1);
                break;
            default:
                return false;
        }

        _focusDate = date;
        if (date.Year != DisplayMonth.Year || date.Month != DisplayMonth.Month)
            SetDisplayMonth(new DateTime(date.Year, date.Month, 1));
        Dispatcher.UIThread.Post(FocusActiveCell, DispatcherPriority.Background);
        return true;
    }

    private bool HandleYearKey(KeyEventArgs e)
    {
        var year = _focusDate.Year;
        switch (e.Key)
        {
            case Key.Left:
                year--;
                break;
            case Key.Right:
                year++;
                break;
            case Key.Up:
                year -= 3;
                break;
            case Key.Down:
                year += 3;
                break;
            case Key.PageUp:
                year -= YearChoiceCount;
                break;
            case Key.PageDown:
                year += YearChoiceCount;
                break;
            case Key.Home:
                year = DisplayMonth.Year - 5;
                break;
            case Key.End:
                year = DisplayMonth.Year - 5 + YearChoiceCount - 1;
                break;
            default:
                return false;
        }

        _focusDate = new DateTime(year, _focusDate.Month, 1);
        if (year != DisplayMonth.Year)
            SetDisplayMonth(new DateTime(year, DisplayMonth.Month, 1));
        Dispatcher.UIThread.Post(FocusActiveCell, DispatcherPriority.Background);
        return true;
    }

    private DateTime StartOfWeek(DateTime date) =>
        date.AddDays(-(((int)date.DayOfWeek - (int)FirstDayOfWeek + 7) % 7));

    private string GetText(string key, string fallback) =>
        this.TryFindResource(key, out var value) && value is string text ? text : fallback;

    private IBrush? GetBrush(string key) =>
        this.TryFindResource(key, out var value) ? value as IBrush : null;

    private sealed class DatePickerPeer(MaterialDatePicker owner) : ControlAutomationPeer(owner), IValueProvider
    {
        private readonly MaterialDatePicker _owner = owner;

        public bool IsReadOnly => !_owner.IsEffectivelyEnabled;

        public string Value => _owner.AutomationValue;

        public void SetValue(string? value)
        {
            if (IsReadOnly)
                throw new InvalidOperationException("The date picker is disabled.");
            if (!_owner.TryParseDate(value, out var date) || !_owner.IsDateAllowed(date))
                throw new ArgumentException("The value is not a valid date inside the allowed range.", nameof(value));
            _owner.Select(date);
        }

        internal void NotifyValueChanged(string oldValue, string newValue) =>
            RaisePropertyChangedEvent(ValuePatternIdentifiers.ValueProperty, oldValue, newValue);

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Calendar;

        protected override string GetClassNameCore() => nameof(MaterialDatePicker);

        protected override string GetNameCore()
        {
            var name = base.GetNameCore();
            return string.IsNullOrEmpty(name) ? _owner.EffectiveLabel : name;
        }

        protected override string GetHelpTextCore()
        {
            var help = base.GetHelpTextCore();
            return string.IsNullOrEmpty(help) ? _owner.Headline : help;
        }
    }
}
