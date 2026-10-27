using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
using ExpressiveAvalonia.Theming;
using ExpressiveAvalonia.Tokens;

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

/// <summary>
/// The locale short-date pattern reduced to the digit-only input pattern used by the text view.
/// Port of <c>datePatternAsInputFormat</c> from <c>internal/CalendarModel.kt</c>.
/// </summary>
internal sealed class MaterialDateInputFormat
{
    private MaterialDateInputFormat(string patternWithDelimiters, char delimiter)
    {
        PatternWithDelimiters = patternWithDelimiters;
        Delimiter = delimiter;
        PatternWithoutDelimiters = patternWithDelimiters.Replace(delimiter.ToString(), string.Empty);
        DigitCount = PatternWithoutDelimiters.Length;
    }

    /// <summary>Pattern including the first locale delimiter, for example <c>dd/MM/yyyy</c>.</summary>
    public string PatternWithDelimiters { get; }

    /// <summary>The delimiter assumed by the mask; <c>'\0'</c> for patterns with no delimiter.</summary>
    public char Delimiter { get; }

    /// <summary>The pattern the user types, for example <c>ddMMyyyy</c>.</summary>
    public string PatternWithoutDelimiters { get; }

    /// <summary>Number of digit characters accepted by the mask.</summary>
    public int DigitCount { get; }

    /// <summary>Builds the input format from a culture or an explicit date pattern.</summary>
    public static MaterialDateInputFormat FromPattern(string localePattern)
    {
        var pattern = Regex.Replace(localePattern, "[^dMy/\\-.]", string.Empty);
        pattern = Regex.Replace(pattern, "d{1,2}", "dd");
        pattern = Regex.Replace(pattern, "M{1,2}", "MM");
        pattern = Regex.Replace(pattern, "y{1,4}", "yyyy");
        pattern = pattern.Replace("My", "M/y");
        if (pattern.EndsWith('.'))
            pattern = pattern[..^1];

        var match = Regex.Match(pattern, "[/\\-.]");
        var delimiter = match.Success ? match.Value[0] : '\0';
        // A culture can legitimately expose a pattern without separators (yyyyMMdd). Keep it
        // parseable instead of inventing a delimiter the locale does not use.
        return new MaterialDateInputFormat(pattern, delimiter);
    }

    /// <summary>Inserts the locale delimiter characters through the typed digit string.</summary>
    public string Format(string digits)
    {
        if (Delimiter == '\0' || digits.Length == 0)
            return digits;

        var builder = new StringBuilder(PatternWithDelimiters.Length);
        var digitIndex = 0;
        foreach (var token in PatternWithDelimiters)
        {
            if (token == Delimiter)
            {
                // Do not show a separator the user has not reached yet.
                if (digitIndex == 0 || digitIndex >= digits.Length)
                    break;
                builder.Append(token);
                continue;
            }

            if (digitIndex >= digits.Length)
                break;
            builder.Append(digits[digitIndex++]);
        }

        if (digitIndex < digits.Length)
            builder.Append(digits, digitIndex, digits.Length - digitIndex);
        return builder.ToString();
    }

    /// <summary>Maps a caret position in the formatted text to the digit offset it follows.</summary>
    public int ToDigitOffset(string formatted, int caret)
    {
        var digits = 0;
        var limit = Math.Clamp(caret, 0, formatted.Length);
        for (var i = 0; i < limit; i++)
        {
            if (char.IsDigit(formatted[i]))
                digits++;
        }
        return digits;
    }

    /// <summary>Maps a digit offset to a caret position in the formatted text.</summary>
    public int ToCaretIndex(string formatted, int digitOffset)
    {
        if (digitOffset <= 0)
            return 0;
        var digits = 0;
        for (var i = 0; i < formatted.Length; i++)
        {
            if (!char.IsDigit(formatted[i]))
                continue;
            digits++;
            if (digits == digitOffset)
                return i + 1;
        }
        return formatted.Length;
    }
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
/// Consulted by <see cref="MaterialDatePicker"/> before enabling a day or a year.
/// Port of <c>SelectableDates</c> from the pinned M3 source; the year check also gates every
/// date in that year.
/// </summary>
public class MaterialSelectableDates
{
    /// <summary>Returns true when the date may be selected. The time component is ignored.</summary>
    public virtual bool IsSelectableDate(DateTime date) => true;

    /// <summary>Returns true when the year may be selected. Defaults to all years.</summary>
    public virtual bool IsSelectableYear(int year) => true;
}

/// <summary>
/// Material Design 3 date picker covering the docked, modal, full-screen range and text-input
/// presentations from the source design system.
/// </summary>
/// <remarks>
/// <para>
/// Dates are calendar dates: bounds, cells and navigation compare the date component of the
/// <see cref="DateTimeOffset"/> values, and selection helpers produce UTC-midnight values so a
/// time-zone conversion can never shift the day.
/// The <see cref="Culture"/> drives month and weekday names, the first day of the week, the
/// headline format and text parsing. <see cref="DateFormat"/> overrides the short date pattern
/// used by the text-input view.
/// </para>
/// <para>
/// The full-screen presentation browses every month of the inclusive
/// <see cref="MinimumYear"/>..<see cref="MaximumYear"/> range in one vertically scrolling list.
/// <see cref="DisplayDate"/> is the shared anchor in both directions: setting it scrolls the list,
/// and scrolling updates it to the first visible month without changing the selection. Only the
/// visible months are materialized, and they are recycled while scrolling.
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
[TemplatePart("PART_FullScreenScroll", typeof(ScrollViewer))]
[TemplatePart("PART_MonthGroup", typeof(Canvas))]
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
    private const int DayRowCount = 6;
    private const double DayCellSize = 48;
    private const int MonthChoiceCount = 12;
    private const int YearColumns = 3;

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

    /// <summary>
    /// First selectable year, defaulting to the source <c>DatePickerDefaults.YearRange</c> start
    /// (1900). Values outside the range are disabled and cannot be committed.
    /// </summary>
    public static readonly StyledProperty<int> MinimumYearProperty =
        AvaloniaProperty.Register<MaterialDatePicker, int>(nameof(MinimumYear), 1900);

    /// <summary>Last selectable year, defaulting to the source range end (2100).</summary>
    public static readonly StyledProperty<int> MaximumYearProperty =
        AvaloniaProperty.Register<MaterialDatePicker, int>(nameof(MaximumYear), 2100);

    /// <summary>Optional <see cref="MaterialSelectableDates"/>; null enables every date.</summary>
    public static readonly StyledProperty<MaterialSelectableDates?> SelectableDatesProperty =
        AvaloniaProperty.Register<MaterialDatePicker, MaterialSelectableDates?>(nameof(SelectableDates));

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

    public static readonly StyledProperty<bool> ShowAdjacentDaysProperty =
        AvaloniaProperty.Register<MaterialDatePicker, bool>(nameof(ShowAdjacentDays));

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
    private readonly List<(IMaterialDateChoiceCell Cell, int Value)> _choiceCells = [];
    private readonly Dictionary<int, MonthBlockView> _monthBlocks = [];
    private readonly Stack<MonthBlockView> _monthPool = [];
    private double _monthItemHeight;
    private double _monthBlockWidth;
    private DateTime _monthRangeStart;
    private int _monthRangeCount;
    private int _pendingMonthIndex = -1;
    private int _lastVisibleMonthIndex = -1;
    private bool _monthScrollRequested;
    private bool _syncingMonthScroll;
    private bool _syncingMonthDisplayFromViewport;
    private bool _monthScrollRetryPosted;
    private int _monthScrollRetryAttempts;
    private bool _applied;
    private bool _settingSelection;
    private bool _normalizing;
    private bool _syncingInput;
    private string? _startSyncedText;
    private string? _endSyncedText;
    private MaterialDateInputFormat? _inputFormat;
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
    private Canvas? _monthGroup;
    private ScrollViewer? _fullScreenScroll;
    private TextBox? _startInput;
    private TextBox? _endInput;
    private TextBlock? _inputError;
    private Border? _actions;
    private Button? _clearButton;
    private Button? _cancelButton;
    private Button? _okButton;
    private string? _fullScreenSignature;
    private Grid? _dayView;
    private ScrollViewer? _choiceScroll;
    private Grid? _fullScreenView;
    private Panel? _inputPanel;
    private MaterialMotion.SpringAnimation? _enterOpacity;
    private MaterialMotion.SpringAnimation? _enterOffset;
    private MaterialMotion.SpringAnimation? _exitOpacity;
    private MaterialMotion.SpringAnimation? _exitOffset;
    private Control? _activeHost;
    private Control? _enterHost;
    private Control? _exitHost;
    private bool _lastInputMode;
    private bool _completingExit;
    private string? _lastViewState;
    private MaterialDatePickerView? _lastView;
    private MaterialDatePickerMode? _lastMode;

    // Horizontal month paging (DatePicker.kt HorizontalMonthsList). PART_DayGrid is the page of
    // the committed month; at most two neighbouring pages are composed beside it.
    private Panel? _monthPager;
    private MonthPageLayer? _livePageLayer;
    private MonthPageLayer? _pageSlotA;
    private MonthPageLayer? _pageSlotB;
    private bool _pageSlotAUsed;
    private bool _pageSlotBUsed;
    private MaterialMotion.SpringAnimation? _pageSpring;
    private MaterialMotion.DecayAnimation? _pageDecay;
    private double _pagePosition;
    private double _pageWidth;
    private double _pageDecayVelocity;
    private int _livePageIndex;
    private int _pageTargetIndex;
    private DateTime _pageOrigin;
    private bool _pageActive;
    private bool _pageNavigating;
    private bool _pageSettled;
    private bool _pagePressed;
    private bool _pageDragging;
    private bool _pageDragExceeded;
    private Point _pageDragStart;
    private double _pageDragStartPosition;
    private readonly List<(double Seconds, double X)> _pageSamples = [];
    // Source goToMonth awaits animateScrollToItem and only then moves focus; a keyboard move to
    // the neighbouring month therefore holds its target cell until the page settles.
    private DateTime? _pendingPageFocus;

    // Year picker reveal (DatePicker.kt 1672-1688): expand/shrink with DefaultEffects, fade in
    // from initialAlpha 0.6 with DefaultEffects and out with FastEffects.
    private MaterialMotion.SpringAnimation? _yearHeight;
    private MaterialMotion.SpringAnimation? _yearOpacity;
    private bool _yearRevealActive;
    private bool _yearRevealSettled;
    private double _yearHeightValue;
    private double _yearOpacityValue = 1;
    // Clip height of the fully expanded year overlay; the reveal keeps the year column
    // bottom-aligned against it (Compose expandVertically(expandFrom = Bottom)).
    private double _yearRevealTarget;
    // The year view is composed while its ScrollViewer is still hidden, so its initial source
    // scroll is applied after the first layout pass.
    private int _yearScrollRetryAttempts;

    // DatePicker.kt SwitchableDateEntryContent: parallaxTarget = -48.dp.
    private const double ParallaxOffset = 48;
    private const double ExitEpsilon = 0.0001;
    // Foundation MinFlingVelocityDp: below it the pager snaps to the closest page.
    private const double MinFlingVelocity = 400;
    // Foundation LazyLayoutScrollScope.MinimumDistance / average page size in pages.
    private const double MinPageApproach = 0.01;
    // Pointer slop before a press on a day becomes a horizontal page drag.
    private const double PageDragSlop = 4;
    private const double PageEpsilon = 0.0005;
    // DatePicker.kt fadeIn(initialAlpha = 0.6f) for the year picker overlay.
    private const double YearFadeInInitialAlpha = 0.6;

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

    /// <summary>First year allowed by the picker. Ports <c>yearRange.first</c>.</summary>
    public int MinimumYear
    {
        get => GetValue(MinimumYearProperty);
        set => SetValue(MinimumYearProperty, value);
    }

    /// <summary>Last year allowed by the picker. Ports <c>yearRange.last</c>.</summary>
    public int MaximumYear
    {
        get => GetValue(MaximumYearProperty);
        set => SetValue(MaximumYearProperty, value);
    }

    /// <summary>
    /// Date and year filter. The source calls the year predicate before the date predicate, so a
    /// non-selectable year disables every day in it.
    /// </summary>
    public MaterialSelectableDates? SelectableDates
    {
        get => GetValue(SelectableDatesProperty);
        set => SetValue(SelectableDatesProperty, value);
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

    /// <summary>
    /// Extension: renders adjacent-month days dimmed and selectable. The pinned source always
    /// uses empty leading and trailing cells, so the default is false.
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

    /// <summary>
    /// Source pickers mirror the calendar/weekday layout when <c>LocalLayoutDirection</c> is RTL.
    /// Avalonia's panels do not reverse order themselves, so the grid rows are mirrored
    /// explicitly; the logical dates and keyboard model are unchanged.
    /// </summary>
    private bool IsRightToLeft =>
        FlowDirection == FlowDirection.RightToLeft || EffectiveCulture.TextInfo.IsRightToLeft;

    private void MirrorRows(UniformGrid grid, int columns)
    {
        // Avalonia already mirrors the visual plane for an explicit RTL FlowDirection.
        // Reorder only for a culture-driven RTL calendar in an otherwise LTR tree.
        if (!IsRightToLeft || FlowDirection == FlowDirection.RightToLeft || columns <= 1 || grid.Children.Count == 0)
            return;
        var children = grid.Children.ToArray();
        grid.Children.Clear();
        for (var row = 0; row < children.Length; row += columns)
        {
            var end = Math.Min(row + columns, children.Length);
            for (var i = end - 1; i >= row; i--)
                grid.Children.Add(children[i]);
        }
    }

    /// <summary>The first day of the month currently displayed.</summary>
    private DateTime DisplayMonth
    {
        get
        {
            // Port of BaseDatePickerStateImpl: an explicit displayed month outside the year
            // range falls back to the selected date's month (already range-checked) and then to
            // the current month. The selectable-year predicate never gates the displayed month;
            // it only disables year cells, so an excluded year remains browsable.
            if (DisplayDate is { } display && IsYearInRange(display.Year))
                return ClampMonthToBounds(new DateTime(display.Year, display.Month, 1));
            if (SelectedDate is { } selected)
                return ClampMonthToBounds(new DateTime(selected.Year, selected.Month, 1));
            var today = Today;
            return new DateTime(today.Year, today.Month, 1);
        }
    }

    private string EffectiveDateFormat =>
        string.IsNullOrWhiteSpace(DateFormat) ? Format.ShortDatePattern : DateFormat;

    private MaterialDateInputFormat InputFormat =>
        _inputFormat ??= MaterialDateInputFormat.FromPattern(EffectiveDateFormat);

    /// <summary>Port of <c>yearRange</c>: the years the calendar can display and store.</summary>
    private bool IsYearInRange(int year) => year >= MinimumYear && year <= MaximumYear;

    /// <summary>
    /// Port of <c>SelectableDates.isSelectableYear</c>: the predicate disables year cells and
    /// their dates. Like the pinned state, it never prevents navigating to the year.
    /// </summary>
    private bool IsYearAllowed(int year) =>
        IsYearInRange(year) && (SelectableDates?.IsSelectableYear(year) ?? true);

    private bool IsDateSelectable(DateTime date) =>
        IsDateAllowed(date) && (SelectableDates?.IsSelectableDate(date.Date) ?? true);

    /// <summary>
    /// Converts a calendar date to the source's date-only representation. The offset is UTC
    /// midnight so converting the value in another time zone cannot move it to the previous day.
    /// </summary>
    public static DateTimeOffset ToDateTimeOffset(DateTime date)
    {
        // SpecifyKind is required: a Local or Utc DateTime arm would make the DateTimeOffset
        // constructor validate the offset and throw for UTC midnight.
        var day = DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified);
        return new DateTimeOffset(day, TimeSpan.Zero);
    }

    /// <summary>True when the date is inside the configured inclusive bounds and year range.</summary>
    public bool IsDateAllowed(DateTime date)
    {
        var value = date.Date;
        return IsYearAllowed(value.Year) &&
               (MinimumDate is not { } min || value >= min.Date) &&
               (MaximumDate is not { } max || value <= max.Date);
    }

    /// <summary>Formats a date using <see cref="DateFormat"/> or the culture short date pattern.</summary>
    public string FormatDate(DateTime date) => date.ToString(EffectiveDateFormat, EffectiveCulture);

    /// <summary>
    /// Parses culture-aware input, including the locale digit pattern and ISO dates, and rejects
    /// values outside the year range. The result is a date with no time component.
    /// </summary>
    public bool TryParseDate(string? text, out DateTime date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var trimmed = text.Trim();
        var format = InputFormat;
        var digits = DigitsOf(trimmed);
        var culture = EffectiveCulture;
        if (digits.Length == format.DigitCount &&
            DateTime.TryParseExact(digits, format.PatternWithoutDelimiters, culture,
                DateTimeStyles.None, out var patternDate))
        {
            date = patternDate.Date;
            return IsYearAllowed(date.Year);
        }

        string[] formats =
        [
            EffectiveDateFormat,
            culture.DateTimeFormat.ShortDatePattern,
            "yyyy-MM-dd",
            "yyyy/MM/dd",
            "yyyyMMdd",
        ];
        if (DateTime.TryParseExact(trimmed, formats, culture,
                DateTimeStyles.AllowWhiteSpaces, out var parsed) ||
            DateTime.TryParse(trimmed, culture, DateTimeStyles.AllowWhiteSpaces, out parsed))
        {
            date = parsed.Date;
            return IsYearAllowed(date.Year);
        }
        return false;
    }

    /// <summary>
    /// Selects a date. In range mode the source's <c>updateDateSelection</c> rules apply: with no
    /// active pending start (both endpoints set, or neither) the click starts a new selection;
    /// with a pending start, a date on or after it becomes the end, and an earlier date replaces
    /// the start without producing an inverted range.
    /// </summary>
    public void Select(DateTime date)
    {
        if (!IsDateSelectable(date))
            return;

        date = date.Date;
        _focusDate = date;
        _focusDateSet = true;
        _previewDate = null;

        if (SelectionMode == MaterialDateSelectionMode.Single)
        {
            ApplySelection(date, null);
            return;
        }

        var start = SelectedDate?.Date;
        var end = SelectedEndDate?.Date;
        if (start is null && end is null || start is not null && end is not null)
        {
            ApplySelection(date, null);
        }
        else if (start is { } pending && date >= pending)
        {
            ApplySelection(pending, date);
        }
        else
        {
            // The user selected an earlier date than the pending start: restart the range.
            ApplySelection(date, null);
        }
    }

    /// <summary>Removes the selection and resets the pending range endpoint.</summary>
    public void ClearSelection()
    {
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
            if (!string.IsNullOrEmpty(_validationMessage))
                return;
            // Incomplete text is not a valid draft; the source dialog keeps its confirm action
            // external, so refuse to confirm an unparsed value.
            if (_startInput is { Text.Length: > 0 } start &&
                DigitsOf(start.Text).Length < InputFormat.DigitCount)
                return;
            if (IsRange && _endInput is { Text.Length: > 0 } end &&
                DigitsOf(end.Text).Length < InputFormat.DigitCount)
                return;
        }

        if (IsRange ? SelectedDate is null || SelectedEndDate is null : SelectedDate is null)
            return;
        Confirmed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Raises <see cref="Cancelled"/> without changing the selection.</summary>
    public void Cancel() => Cancelled?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Moves the displayed month by the given amount, clamped to the bounds. In the day view this
    /// is the source's prev/next action: the neighbouring month page slides in with the default
    /// scroll spring, and a second call while it is in flight continues or reverses that motion.
    /// </summary>
    public void NavigateMonth(int delta) => AnimateMonthNavigation(delta);

    /// <summary>Moves the displayed year by the given amount, clamped to the bounds.</summary>
    public void NavigateYear(int delta) => SetDisplayMonth(AddMonthsClamped(DisplayMonth, (long)delta * 12));

    /// <summary>Shows the month of today (or the nearest allowed month).</summary>
    public void GoToToday()
    {
        var today = Today;
        SetDisplayMonth(new DateTime(today.Year, today.Month, 1));
        if (IsDateAllowed(today))
            MoveFocus(today);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // The ScrollViewer can clamp its offset to zero while detached; DisplayDate is the
        // authoritative anchor, so ask the full-screen list to scroll back to it.
        if (Mode == MaterialDatePickerMode.FullScreen)
        {
            _monthScrollRequested = true;
            if (_monthRangeCount > 0 && _monthItemHeight > 0)
            {
                var index = MonthIndexOf(DisplayMonth);
                if (index >= 0)
                {
                    _pendingMonthIndex = index;
                    _monthScrollRetryAttempts = 0;
                }
            }
        }
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
        _monthPager = e.NameScope.Find<Panel>("PART_MonthPager");
        _choiceHost = e.NameScope.Find<Panel>("PART_ChoiceHost");
        _fullScreenWeekdayRow = e.NameScope.Find<UniformGrid>("PART_FullScreenWeekdayRow");
        _monthGroup = e.NameScope.Find<Canvas>("PART_MonthGroup");
        _fullScreenScroll = e.NameScope.Find<ScrollViewer>("PART_FullScreenScroll");
        _startInput = e.NameScope.Find<TextBox>("PART_StartInput");
        _endInput = e.NameScope.Find<TextBox>("PART_EndInput");
        _inputError = e.NameScope.Find<TextBlock>("PART_InputError");
        _actions = e.NameScope.Find<Border>("PART_Actions");
        _clearButton = e.NameScope.Find<Button>("PART_ClearButton");
        _cancelButton = e.NameScope.Find<Button>("PART_CancelButton");
        _okButton = e.NameScope.Find<Button>("PART_OkButton");
        _dayView = e.NameScope.Find<Grid>("PART_DayView");
        _choiceScroll = e.NameScope.Find<ScrollViewer>("PART_ChoiceScroll");
        _fullScreenView = e.NameScope.Find<Grid>("PART_FullScreenView");
        _inputPanel = e.NameScope.Find<Panel>("PART_InputPanel");

        _dayCells.Clear();
        _choiceCells.Clear();
        _dayGrid?.Children.Clear();
        _choiceHost?.Children.Clear();
        ReleaseMonthBlocks();
        _weekdayRow?.Children.Clear();
        _fullScreenWeekdayRow?.Children.Clear();
        _fullScreenSignature = null;
        _monthScrollRequested = true;

        if (_monthGroup is not null)
        {
            _monthGroup.SizeChanged += OnMonthGroupSizeChanged;
            _monthGroup.LayoutUpdated += OnMonthGroupLayoutUpdated;
            _monthGroup.PointerExited += OnDayGridPointerExited;
        }
        if (_fullScreenScroll is not null)
            _fullScreenScroll.ScrollChanged += OnFullScreenScrollChanged;
        ActualThemeVariantChanged += OnPickerThemeVariantChanged;

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
            _startInput.TextChanged += OnStartInputTextChanged;
        }
        if (_endInput is not null)
        {
            _endInput.KeyDown += OnEndInputKeyDown;
            _endInput.LostFocus += OnEndInputLostFocus;
            _endInput.TextChanged += OnEndInputTextChanged;
        }
        if (_dayGrid is not null)
            _dayGrid.PointerExited += OnDayGridPointerExited;
        if (_monthPager is not null)
        {
            // The pages are built from day cells, which are Buttons: a press inside the grid is
            // captured by the cell and marked handled before it bubbles, exactly like Compose's
            // clickable Day inside the LazyRow. The paging surface must therefore observe
            // handled input; it only takes the pointer once the drag exceeds touch slop, which
            // cancels the day's click the same way a scroll consumes a tap in the source list.
            _monthPager.AddHandler(InputElement.PointerPressedEvent, OnMonthPagerPointerPressed,
                RoutingStrategies.Bubble, handledEventsToo: true);
            _monthPager.AddHandler(InputElement.PointerMovedEvent, OnMonthPagerPointerMoved,
                RoutingStrategies.Bubble, handledEventsToo: true);
            _monthPager.AddHandler(InputElement.PointerReleasedEvent, OnMonthPagerPointerReleased,
                RoutingStrategies.Bubble, handledEventsToo: true);
            _monthPager.AddHandler(InputElement.PointerCaptureLostEvent,
                OnMonthPagerPointerCaptureLost, RoutingStrategies.Bubble, handledEventsToo: true);
        }

        _applied = true;
        EnsureFocusDate();
        InitializeMonthPager();
        RefreshVisualState();
        BuildStructure();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == MinimumDateProperty || change.Property == MaximumDateProperty ||
            change.Property == MinimumYearProperty || change.Property == MaximumYearProperty ||
            change.Property == SelectableDatesProperty)
        {
            CoerceValue(SelectedDateProperty);
            CoerceValue(SelectedEndDateProperty);
        }

        if (change.Property == CultureProperty || change.Property == DateFormatProperty)
        {
            _inputFormat = null;
            _validationMessage = null;
            BuildWeekdayRow(_weekdayRow);
            BuildWeekdayRow(_fullScreenWeekdayRow);
            UpdatePlaceholder();
            SyncInputText(force: true);
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
            change.Property == CultureProperty ||
            change.Property == MinimumDateProperty || change.Property == MaximumDateProperty ||
            change.Property == MinimumYearProperty || change.Property == MaximumYearProperty ||
            change.Property == SelectableDatesProperty ||
            change.Property == IsInputModeProperty || change.Property == FlowDirectionProperty)
        {
            // Scrolling the full-screen month list only moves the shared DisplayDate; the
            // surrounding calendar/header do not consume it while the list itself is the view.
            if (change.Property == DisplayDateProperty && _syncingMonthDisplayFromViewport)
                return;
            // DatePickerStateImpl/DateRangePickerStateImpl displayMode setter: any mode change
            // re-targets the displayed month to the selected start so returning to the calendar
            // shows the committed draft. The public property and the built-in toggle share it.
            if (change.Property == IsInputModeProperty && !_settingSelection && !_normalizing &&
                SelectedDate is { } selected)
                SetDisplayMonth(new DateTime(selected.Year, selected.Month, 1));
            // Any other state change invalidates composed month pages: a bound, culture or mode
            // change while a page is in flight converges on the committed month instead of
            // leaving a stale neighbour page behind.
            if (_pageActive && !_pageNavigating)
                ResetMonthPager();
            if (change.Property == DisplayDateProperty ||
                change.Property == ModeProperty && Mode == MaterialDatePickerMode.FullScreen)
                _monthScrollRequested = true;
            NormalizeSelection();
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
            {
                SetCurrentValue(ViewProperty, MaterialDatePickerView.Day);
                Dispatcher.UIThread.Post(FocusActiveCell, DispatcherPriority.Background);
            }
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
        if (e.Source != this)
            return;
        if (IsInputMode)
        {
            FocusTextInput();
            return;
        }
        FocusActiveCell();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => _peer = new DatePickerPeer(this);

    private static DateTimeOffset? CoerceDate(AvaloniaObject instance, DateTimeOffset? value)
    {
        if (value is not { } date)
            return null;
        var picker = (MaterialDatePicker)instance;
        var day = date.Date;
        // Port of DatePickerStateImpl: a date outside the year range is not stored. The
        // SelectableDates predicate only disables UI cells; it does not reject bound state.
        if (day.Year < picker.MinimumYear || day.Year > picker.MaximumYear)
            return null;
        if (picker.MinimumDate is { } min && day < min.Date)
            return min;
        if (picker.MaximumDate is { } max && day > max.Date)
            return max;
        return ToDateTimeOffset(day);
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
            _startInput.TextChanged -= OnStartInputTextChanged;
            _startSyncedText = null;
        }
        if (_endInput is not null)
        {
            _endInput.KeyDown -= OnEndInputKeyDown;
            _endInput.LostFocus -= OnEndInputLostFocus;
            _endInput.TextChanged -= OnEndInputTextChanged;
            _endSyncedText = null;
        }
        if (_dayGrid is not null)
            _dayGrid.PointerExited -= OnDayGridPointerExited;
        if (_monthPager is not null)
        {
            _monthPager.RemoveHandler(InputElement.PointerPressedEvent, OnMonthPagerPointerPressed);
            _monthPager.RemoveHandler(InputElement.PointerMovedEvent, OnMonthPagerPointerMoved);
            _monthPager.RemoveHandler(InputElement.PointerReleasedEvent, OnMonthPagerPointerReleased);
            _monthPager.RemoveHandler(InputElement.PointerCaptureLostEvent,
                OnMonthPagerPointerCaptureLost);
        }
        StopContentAnimations();
        ResetMonthPager();
        ResetYearReveal();
        _monthPager = null;
        _livePageLayer = null;
        _pageSlotA = null;
        _pageSlotB = null;
        _activeHost = null;
        _completingExit = false;
        _lastViewState = null;
        _lastView = null;
        _lastMode = null;
        _lastInputMode = false;
        _dayCells.Clear();
        _choiceCells.Clear();
        ReleaseMonthBlocks();
        if (_monthGroup is not null)
        {
            _monthGroup.SizeChanged -= OnMonthGroupSizeChanged;
            _monthGroup.LayoutUpdated -= OnMonthGroupLayoutUpdated;
            _monthGroup.PointerExited -= OnDayGridPointerExited;
        }
        if (_fullScreenScroll is not null)
            _fullScreenScroll.ScrollChanged -= OnFullScreenScrollChanged;
        ActualThemeVariantChanged -= OnPickerThemeVariantChanged;
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

    /// <summary>
    /// Applies a captured pair for the modal dialog's cancel restore. The endpoints are written
    /// behind the same batch guard as a user selection so a valid snapshot cannot be split into
    /// an intermediate inverted range, which would clear it permanently.
    /// </summary>
    internal void RestoreSelection(DateTimeOffset? start, DateTimeOffset? end)
    {
        ApplySelection(start?.Date, end?.Date);
        UpdateFocusFromSelection();
    }

    private void NormalizeSelection()
    {
        if (_normalizing)
            return;
        _normalizing = true;
        try
        {
            var start = SelectedDate?.Date;
            var end = SelectedEndDate?.Date;
            if (SelectionMode == MaterialDateSelectionMode.Single)
            {
                end = null;
                // Single-selection state has no end property in the source; drop a stale bound
                // end instead of leaving it visible to consumers and dialogs.
                if (SelectedEndDate is not null)
                    SetCurrentValue(SelectedEndDateProperty, null);
            }
            else if (start is null || (end is { } finish && finish < start))
            {
                // DateRangePickerStateImpl.setSelection: an end without a start, or an end
                // before the start, invalidates the whole selection instead of reordering it.
                start = null;
                end = null;
            }

            _rangeAnchor = start;
            _awaitingRangeEnd = SelectionMode == MaterialDateSelectionMode.Range &&
                                start is not null && end is null;

            DateTimeOffset? startValue = start is { } s ? ToDateTimeOffset(s) : null;
            DateTimeOffset? endValue = end is { } e ? ToDateTimeOffset(e) : null;
            if (startValue != SelectedDate)
                SetCurrentValue(SelectedDateProperty, startValue);
            if (endValue != SelectedEndDate)
                SetCurrentValue(SelectedEndDateProperty, endValue);
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
            var month = DisplayMonth;
            var year = Math.Clamp(month.Year, Math.Min(MinimumYear, MaximumYear),
                Math.Max(MinimumYear, MaximumYear));
            var candidate = new DateTime(year, month.Month, 1);
            candidate = ClampToBounds(candidate) ?? candidate;
            if (!IsDateSelectable(candidate))
                candidate = SelectedDate?.Date ?? FindFirstSelectable(candidate);
            _focusDate = candidate;
            _focusDateSet = true;
        }
        else if (!IsDateSelectable(_focusDate))
        {
            _focusDate = ClampToBounds(_focusDate) ?? DisplayMonth;
        }
    }

    private DateTime FindFirstSelectable(DateTime monthStart)
    {
        var start = new DateTime(monthStart.Year, monthStart.Month, 1);
        var days = DateTime.DaysInMonth(start.Year, start.Month);
        for (var i = 0; i < days; i++)
        {
            var day = start.AddDays(i);
            if (IsDateSelectable(day))
                return day;
        }
        return start;
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
        var signature = $"{EffectiveCulture.Name}|{(IsRightToLeft ? "rtl" : "ltr")}";
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
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            // DatePickerModalTokens.WeekdaysLabelTextFont is BodyLarge and its color is OnSurface.
            // Bind both live: the row is generated in code, so snapshotting the brush or font here
            // would freeze it across seed/theme changes.
            MaterialTypography.SetStyle(label, MaterialTypeStyle.BodyLarge);
            label.Bind(TextBlock.ForegroundProperty, label.GetResourceObservable("MaterialOnSurfaceBrush"));
            AutomationProperties.SetName(label, full[index]);
            AutomationProperties.SetControlTypeOverride(label, AutomationControlType.Header);
            row.Children.Add(label);
        }
        MirrorRows(row, 7);
    }

    private void BuildDayGrid()
    {
        if (_dayGrid is null)
            return;

        var display = DisplayMonth;
        var first = new DateTime(display.Year, display.Month, 1);
        var offset = ((int)first.DayOfWeek - (int)FirstDayOfWeek + 7) % 7;
        var startDay = first.Ticks / TimeSpan.TicksPerDay - offset;
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
            var day = startDay + i;
            var date = day >= 0 && day <= DateTime.MaxValue.Ticks / TimeSpan.TicksPerDay
                ? new DateTime(day * TimeSpan.TicksPerDay) : (DateTime?)null;
            var cell = _dayCells[i];
            cell.Tag = date;
            cell.ContextMonth = display;
            cell.Content = date?.Day.ToString(culture);
        }

        _dayGrid.Children.Clear();
        foreach (var cell in _dayCells)
            _dayGrid.Children.Add(cell);
        MirrorRows(_dayGrid, 7);
    }

    private void BuildFullScreenMonths()
    {
        if (_monthGroup is null)
            return;

        if (Mode != MaterialDatePickerMode.FullScreen)
        {
            if (_monthRangeCount > 0 || _monthBlocks.Count > 0 || _monthGroup.Children.Count > 0)
            {
                ReleaseMonthBlocks();
                _monthGroup.Height = double.NaN;
                _monthRangeCount = 0;
                _monthItemHeight = 0;
                _pendingMonthIndex = -1;
                _lastVisibleMonthIndex = -1;
                _fullScreenSignature = null;
            }
            return;
        }

        // DateRangePicker.kt: numberOfMonthsInRange(yearRange) =
        // (yearRange.last - yearRange.first + 1) * 12. The whole permitted range is addressable;
        // only the viewport window is materialized below.
        var firstYear = Math.Min(MinimumYear, MaximumYear);
        var lastYear = Math.Max(MinimumYear, MaximumYear);
        var signature = FormattableString.Invariant(
            $"{firstYear}|{lastYear}|{EffectiveCulture.Name}|{(int)FirstDayOfWeek}|{(IsRightToLeft ? "rtl" : "ltr")}");
        if (_fullScreenSignature != signature)
        {
            _fullScreenSignature = signature;
            ReleaseMonthBlocks();
            _monthRangeStart = new DateTime(firstYear, 1, 1);
            _monthRangeCount = (lastYear - firstYear + 1) * 12;
            _monthItemHeight = 0;
            _pendingMonthIndex = -1;
            _lastVisibleMonthIndex = -1;
            _monthGroup.Height = double.NaN;
            _monthScrollRequested = true;
        }

        if (_monthRangeCount <= 0)
            return;

        if (_monthItemHeight <= 0)
        {
            EnsureMonthProbe();
            return;
        }

        PrepareVisibleMonths();
        if (_monthScrollRequested)
        {
            _monthScrollRequested = false;
            RequestScrollToDisplayMonth();
        }
    }

    /// <summary>
    /// Creates the first realized month so its styled DesiredSize supplies the uniform row height
    /// used as the virtual list extent. The month is reused by the realization window afterwards.
    /// </summary>
    private void EnsureMonthProbe()
    {
        if (_monthGroup is null || _monthRangeCount <= 0 || _monthBlocks.Count > 0)
            return;
        var block = RentMonthBlock();
        PrepareMonthBlock(block, _monthRangeStart);
        var width = MonthBlockWidth;
        if (width > 0)
        {
            _monthBlockWidth = width;
            block.Root.Width = width;
        }
        _monthBlocks.Add(0, block);
        _monthGroup.Children.Add(block.Root);
        // The row height is measured from the styled, arranged block in
        // OnMonthGroupLayoutUpdated. The post covers templates applied before the visual root
        // has run a layout pass.
        ScheduleMonthScrollRetry();
    }

    private void TryMeasureMonthBlock()
    {
        if (_monthItemHeight > 0 || _monthBlocks.Count == 0)
            return;
        var width = MonthBlockWidth;
        if (width > 0)
            _monthBlockWidth = width;
        foreach (var block in _monthBlocks.Values)
        {
            if (_monthBlockWidth > 0)
                block.Root.Width = _monthBlockWidth;
            var height = block.Root.Bounds.Height;
            if (height <= 0 && block.Root.IsAttachedToVisualTree())
            {
                block.Root.Measure(new Size(Math.Max(_monthBlockWidth, 1), double.PositiveInfinity));
                height = block.Root.DesiredSize.Height;
            }
            if (height > 0)
            {
                SetMonthItemHeight(height);
                return;
            }
        }
    }

    private void SetMonthItemHeight(double height)
    {
        if (height <= 0 || _monthGroup is null)
            return;
        var changed = Math.Abs(height - _monthItemHeight) > 0.01;
        _monthItemHeight = height;
        _monthGroup.Height = _monthRangeCount * height;
        foreach (var (index, block) in _monthBlocks)
        {
            block.Root.Height = height;
            if (_monthBlockWidth > 0)
                block.Root.Width = _monthBlockWidth;
            Canvas.SetTop(block.Root, index * height);
        }

        // DateRangePicker's LazyListState keeps its first visible item index across layout
        // invalidation; keep the same month anchored when the row height changes under it.
        if (changed && _lastVisibleMonthIndex >= 0 && _pendingMonthIndex < 0 &&
            _fullScreenScroll is not null)
        {
            var target = Math.Min(
                _lastVisibleMonthIndex * height,
                Math.Max(0, _monthGroup.Height - _fullScreenScroll.Viewport.Height));
            _syncingMonthScroll = true;
            try
            {
                _fullScreenScroll.Offset = new Vector(_fullScreenScroll.Offset.X, target);
            }
            finally
            {
                _syncingMonthScroll = false;
            }
        }
    }

    private void PrepareVisibleMonths()
    {
        if (_monthGroup is null || _monthItemHeight <= 0 || _monthRangeCount <= 0)
            return;
        var width = MonthBlockWidth;
        if (width > 0)
            _monthBlockWidth = width;

        var offset = _fullScreenScroll?.Offset.Y ?? 0;
        var viewport = _fullScreenScroll?.Viewport.Height ?? 0;
        if (viewport <= 0)
            viewport = _monthItemHeight;
        var first = Math.Max(0,
            (int)Math.Floor(offset / _monthItemHeight) - 1);
        var last = Math.Min(_monthRangeCount,
            (int)Math.Ceiling((offset + viewport) / _monthItemHeight) + 1);

        List<int>? stale = null;
        foreach (var index in _monthBlocks.Keys)
            if (index < first || index >= last)
                (stale ??= []).Add(index);
        if (stale is not null)
            foreach (var index in stale)
                ReturnMonthBlock(index);

        for (var index = first; index < last; index++)
        {
            if (_monthBlocks.ContainsKey(index))
                continue;
            var block = RentMonthBlock();
            PrepareMonthBlock(block, _monthRangeStart.AddMonths(index));
            block.Root.Height = _monthItemHeight;
            if (_monthBlockWidth > 0)
                block.Root.Width = _monthBlockWidth;
            Canvas.SetTop(block.Root, index * _monthItemHeight);
            _monthBlocks.Add(index, block);
            _monthGroup.Children.Add(block.Root);
        }
    }

    private MonthBlockView RentMonthBlock()
    {
        if (_monthPool.Count > 0)
            return _monthPool.Pop();

        var header = new TextBlock
        {
            // DateRangePicker.kt CalendarMonthSubheadPadding: start 24, top 20, bottom 8.
            Margin = new Thickness(24, 20, 0, 8),
            MaxLines = 1,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        // DatePickerModalTokens.RangeSelectionMonthSubheadFont is TitleSmall with the
        // OnSurfaceVariant color. Bind dynamically so a theme/typography update reaches the
        // recycled header without a rebuild.
        MaterialTypography.SetStyle(header, MaterialTypeStyle.TitleSmall);
        header.Bind(TextBlock.ForegroundProperty,
            header.GetResourceObservable("MaterialOnSurfaceVariantBrush"));
        AutomationProperties.SetControlTypeOverride(header, AutomationControlType.Header);

        var grid = new UniformGrid
        {
            Columns = 7,
            Rows = DayRowCount,
            Width = DayCellSize * 7,
            Height = DayCellSize * DayRowCount,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        var cells = new MaterialDateCell[DayCellCount];
        for (var i = 0; i < DayCellCount; i++)
        {
            var cell = new MaterialDateCell();
            cell.Click += OnDayCellClick;
            cell.GotFocus += OnFullScreenCellGotFocus;
            cell.PointerEntered += OnFullScreenCellPointerEntered;
            cells[i] = cell;
            grid.Children.Add(cell);
        }
        MirrorRows(grid, 7);

        // DatePicker.kt Month draws the range container in drawWithContent, behind the day
        // content, across the full row width including leading/trailing Spacer cells.
        var backdrop = new MonthRangeBackdrop();
        backdrop.IsHitTestVisible = false;
        backdrop.Bind(MonthRangeBackdrop.FillProperty,
            backdrop.GetResourceObservable("MaterialSecondaryContainerBrush"));
        var dayLayer = new Panel
        {
            Width = DayCellSize * 7,
            Height = DayCellSize * DayRowCount,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        dayLayer.Children.Add(backdrop);
        dayLayer.Children.Add(grid);

        var root = new StackPanel();
        root.Children.Add(header);
        root.Children.Add(dayLayer);
        return new MonthBlockView(root, header, cells, backdrop);
    }

    private void PrepareMonthBlock(MonthBlockView block, DateTime month)
    {
        var culture = EffectiveCulture;
        var headerText = month.ToString("Y", culture);
        block.Header.Text = headerText;
        AutomationProperties.SetName(block.Header, headerText);

        var first = new DateTime(month.Year, month.Month, 1);
        var offset = ((int)first.DayOfWeek - (int)FirstDayOfWeek + 7) % 7;
        block.Month = month;
        block.MonthOffset = offset;
        for (var i = 0; i < DayCellCount; i++)
        {
            var day = first.Ticks / TimeSpan.TicksPerDay + i - offset;
            var date = day >= 0 && day <= DateTime.MaxValue.Ticks / TimeSpan.TicksPerDay
                ? new DateTime(day * TimeSpan.TicksPerDay) : (DateTime?)null;
            var cell = block.Cells[i];
            if (date is { } validDate && validDate.Year == month.Year && validDate.Month == month.Month)
            {
                cell.Tag = validDate;
                cell.ContextMonth = month;
                cell.Content = validDate.Day.ToString(culture);
                cell.Opacity = 1;
                cell.IsHitTestVisible = true;
            }
            else
            {
                // DatePicker.kt Month renders Spacers outside the month in the full-screen
                // range list, unlike the selectable adjacent days of the day view.
                cell.Tag = null;
                cell.Content = null;
                // UniformGrid omits collapsed children; retain this spacer's layout slot.
                cell.Opacity = 0;
                cell.IsHitTestVisible = false;
                cell.IsEnabled = false;
            }
        }
        ApplyCellStates(block.Cells);
        UpdateMonthRangeBackdrop(block);
    }

    private void ReturnMonthBlock(int index)
    {
        if (!_monthBlocks.TryGetValue(index, out var block))
            return;
        _monthBlocks.Remove(index);
        _monthGroup?.Children.Remove(block.Root);
        if (_monthPool.Count < 4)
            _monthPool.Push(block);
    }

    private void ReleaseMonthBlocks()
    {
        _monthBlocks.Clear();
        _monthPool.Clear();
        _monthGroup?.Children.Clear();
    }

    private double MonthBlockWidth
    {
        get
        {
            var width = _fullScreenScroll?.Viewport.Width ?? 0;
            if (width > 0)
                return width;
            width = _monthGroup?.Bounds.Width ?? 0;
            if (width > 0)
                return width;
            width = _fullScreenView?.Bounds.Width ?? 0;
            return width > 24 ? width - 24 : 0;
        }
    }

    private int MonthIndexOf(DateTime month)
    {
        if (_monthRangeCount <= 0)
            return -1;
        var index = (month.Year - _monthRangeStart.Year) * 12 + month.Month - 1;
        return Math.Clamp(index, 0, _monthRangeCount - 1);
    }

    private int FirstVisibleMonthIndex()
    {
        if (_monthItemHeight <= 0 || _monthRangeCount <= 0 || _fullScreenScroll is null)
            return -1;
        var index = (int)Math.Floor(
            _fullScreenScroll.Offset.Y / _monthItemHeight + 0.0001);
        return Math.Clamp(index, 0, _monthRangeCount - 1);
    }

    private void RequestScrollToDisplayMonth()
    {
        if (_monthRangeCount <= 0)
            return;
        var index = MonthIndexOf(DisplayMonth);
        if (index < 0)
            return;
        // DateRangePicker.kt scrollToItem runs only when the list's first visible item is not
        // already the displayed month. A viewport-driven DisplayDate update therefore does not
        // fight the user's scroll.
        if (_pendingMonthIndex < 0 && _lastVisibleMonthIndex == index &&
            FirstVisibleMonthIndex() == index)
            return;
        _pendingMonthIndex = index;
        _monthScrollRetryAttempts = 0;
        TryApplyPendingMonthScroll();
    }

    private void TryApplyPendingMonthScroll()
    {
        if (_pendingMonthIndex < 0 || _monthGroup is null ||
            _fullScreenScroll is null || _monthItemHeight <= 0)
            return;

        var viewport = _fullScreenScroll.Viewport.Height;
        var content = _monthGroup.Height;
        if (content > viewport && _fullScreenScroll.Extent.Height + 0.5 < content)
        {
            // The ScrollViewer has not picked up the new virtual extent yet.
            ScheduleMonthScrollRetry();
            return;
        }

        var target = Math.Min(
            _pendingMonthIndex * _monthItemHeight,
            Math.Max(0, content - viewport));
        _syncingMonthScroll = true;
        try
        {
            _fullScreenScroll.Offset = new Vector(_fullScreenScroll.Offset.X, target);
        }
        finally
        {
            _syncingMonthScroll = false;
        }
        PrepareVisibleMonths();
        if (Math.Abs(_fullScreenScroll.Offset.Y - target) <= 0.5)
        {
            _lastVisibleMonthIndex = Math.Clamp(
                (int)Math.Floor(_fullScreenScroll.Offset.Y / _monthItemHeight + 0.0001),
                0, _monthRangeCount - 1);
            _pendingMonthIndex = -1;
            _monthScrollRetryAttempts = 0;
        }
        else
        {
            ScheduleMonthScrollRetry();
        }
    }

    private void ScheduleMonthScrollRetry()
    {
        if (_monthScrollRetryPosted || _monthScrollRetryAttempts >= 32)
            return;
        _monthScrollRetryAttempts++;
        _monthScrollRetryPosted = true;
        Dispatcher.UIThread.Post(() =>
        {
            _monthScrollRetryPosted = false;
            if (Mode != MaterialDatePickerMode.FullScreen)
                return;
            if (_monthItemHeight <= 0)
                TryMeasureMonthBlock();
            if (_pendingMonthIndex >= 0)
                TryApplyPendingMonthScroll();
            PrepareVisibleMonths();
        }, DispatcherPriority.Loaded);
    }

    private void OnFullScreenScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (Mode != MaterialDatePickerMode.FullScreen || _syncingMonthScroll ||
            !this.IsAttachedToVisualTree())
            return;
        if (_monthItemHeight <= 0)
        {
            TryMeasureMonthBlock();
            return;
        }

        PrepareVisibleMonths();
        if (_pendingMonthIndex >= 0)
        {
            TryApplyPendingMonthScroll();
            return;
        }

        // DateRangePicker.kt updateDisplayedMonth: firstVisibleItemIndex / 12 is the year offset,
        // index % 12 + 1 the month inside yearRange.first. Scrolling therefore moves the shared
        // DisplayDate state without changing the selection.
        var index = FirstVisibleMonthIndex();
        if (index < 0 || index == _lastVisibleMonthIndex)
            return;
        _lastVisibleMonthIndex = index;
        _syncingMonthDisplayFromViewport = true;
        try
        {
            SetDisplayMonth(_monthRangeStart.AddMonths(index));
        }
        finally
        {
            _syncingMonthDisplayFromViewport = false;
        }
    }

    private void OnMonthGroupLayoutUpdated(object? sender, EventArgs e) =>
        AdvanceFullScreenMonthList();

    private void OnMonthGroupSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (Mode != MaterialDatePickerMode.FullScreen)
            return;
        if (_monthItemHeight > 0)
        {
            var width = MonthBlockWidth;
            if (width > 0)
            {
                _monthBlockWidth = width;
                foreach (var block in _monthBlocks.Values)
                    block.Root.Width = width;

                if (Math.Abs(e.NewSize.Width - e.PreviousSize.Width) > 0.01)
                {
                    var measured = MeasureRealizedMonth();
                    if (measured > 0 && Math.Abs(measured - _monthItemHeight) > 0.01)
                        SetMonthItemHeight(measured);
                }
            }
        }
        AdvanceFullScreenMonthList();
    }

    private void AdvanceFullScreenMonthList()
    {
        if (Mode != MaterialDatePickerMode.FullScreen)
            return;
        if (_monthItemHeight <= 0)
            TryMeasureMonthBlock();
        if (_monthItemHeight <= 0)
            return;
        if (_monthScrollRequested)
        {
            _monthScrollRequested = false;
            RequestScrollToDisplayMonth();
        }
        if (_pendingMonthIndex >= 0)
            TryApplyPendingMonthScroll();
        PrepareVisibleMonths();
    }

    private double MeasureRealizedMonth()
    {
        foreach (var block in _monthBlocks.Values)
        {
            if (_monthBlockWidth <= 0)
                return 0;
            var saved = block.Root.Height;
            block.Root.Height = double.NaN;
            block.Root.Measure(new Size(_monthBlockWidth, double.PositiveInfinity));
            var height = block.Root.DesiredSize.Height;
            block.Root.Height = saved;
            if (height > 0)
                return height;
        }
        return 0;
    }

    private void OnPickerThemeVariantChanged(object? sender, EventArgs e)
    {
        if (Mode != MaterialDatePickerMode.FullScreen || _monthItemHeight <= 0)
            return;
        var measured = MeasureRealizedMonth();
        if (measured > 0 && Math.Abs(measured - _monthItemHeight) > 0.01)
            SetMonthItemHeight(measured);
        ApplyFullScreenCellStates();
        PrepareVisibleMonths();
    }

    private void ApplyFullScreenCellStates()
    {
        foreach (var block in _monthBlocks.Values)
        {
            ApplyCellStates(block.Cells);
            UpdateMonthRangeBackdrop(block);
        }
    }

    private void UpdateMonthRangeBackdrop(MonthBlockView block)
    {
        var start = SelectedDate?.Date;
        var end = SelectedEndDate?.Date;
        if (SelectionMode != MaterialDateSelectionMode.Range ||
            start is null || end is null || end < start)
        {
            block.Backdrop.ClearRange();
            return;
        }
        // DateRangePicker.kt SelectedRangeInfo.calculateRangeInfo + drawRangeBackground.
        block.Backdrop.SetRange(block.Month, block.MonthOffset, start.Value, end.Value,
            IsRightToLeft && FlowDirection != FlowDirection.RightToLeft);
    }

    private void OnFullScreenCellGotFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is MaterialDateCell { Tag: DateTime focused })
            _focusDate = focused;
    }

    private void OnFullScreenCellPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is MaterialDateCell { Tag: DateTime hovered })
            SetPreview(hovered);
    }

    private void BuildChoiceHost()
    {
        if (_choiceHost is null)
            return;
        _choiceHost.Children.Clear();
        _choiceCells.Clear();

        if (View == MaterialDatePickerView.Day)
            return;

        var list = Mode == MaterialDatePickerMode.Docked;

        if (View == MaterialDatePickerView.Month)
        {
            var months = Format.MonthNames;
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
                    row.GotFocus += OnChoiceGotFocus;
                    _choiceCells.Add(((IMaterialDateChoiceCell)row, month));
                    stack.Children.Add(row);
                }
                _choiceHost.Children.Add(stack);
            }
            else
            {
                var grid = new UniformGrid { Columns = YearColumns };
                for (var month = 1; month <= MonthChoiceCount; month++)
                {
                    var chip = new MaterialDateChoiceChip
                    {
                        Content = months[month - 1],
                        Tag = month,
                    };
                    chip.Click += OnMonthChoiceClick;
                    chip.GotFocus += OnChoiceGotFocus;
                    _choiceCells.Add(((IMaterialDateChoiceCell)chip, month));
                    grid.Children.Add(chip);
                }
                MirrorRows(grid, YearColumns);
                _choiceHost.Children.Add(grid);
            }
            RefreshChoiceStates();
            SyncChoiceTabStop();
            return;
        }

        // The pinned YearPicker lists the whole yearRange in three columns, disabled years
        // included. Its initial scroll lands three years above the displayed year.
        var firstYear = Math.Min(MinimumYear, MaximumYear);
        var lastYear = Math.Max(MinimumYear, MaximumYear);
        if (list)
        {
            var stack = new StackPanel();
            for (var year = firstYear; year <= lastYear; year++)
            {
                var row = new MaterialDateChoiceRow
                {
                    Content = year.ToString(EffectiveCulture),
                    Tag = year,
                };
                row.Click += OnYearChoiceClick;
                row.GotFocus += OnChoiceGotFocus;
                _choiceCells.Add(((IMaterialDateChoiceCell)row, year));
                stack.Children.Add(row);
            }
            _choiceHost.Children.Add(stack);
        }
        else
        {
            var grid = new UniformGrid { Columns = YearColumns };
            for (var year = firstYear; year <= lastYear; year++)
            {
                var chip = new MaterialDateChoiceChip
                {
                    Content = year.ToString(EffectiveCulture),
                    Tag = year,
                };
                chip.Click += OnYearChoiceClick;
                chip.GotFocus += OnChoiceGotFocus;
                _choiceCells.Add(((IMaterialDateChoiceCell)chip, year));
                grid.Children.Add(chip);
            }
            MirrorRows(grid, YearColumns);
            _choiceHost.Children.Add(grid);
        }
        RefreshChoiceStates();
        SyncChoiceTabStop();
        RequestYearListPosition();
    }

    /// <summary>
    /// DatePicker.kt YearPicker opens its grid at
    /// <c>rememberLazyGridState(initialFirstVisibleItemIndex = max(0, displayedYear -
    /// yearRange.first - YearsInRow))</c>, i.e. one row above the displayed year. The port keeps
    /// one ScrollViewer for the month and year lists, so the position is applied when the year
    /// view is composed, once the chips have been measured.
    /// </summary>
    private void RequestYearListPosition()
    {
        if (View != MaterialDatePickerView.Year || _choiceScroll is null || _choiceCells.Count == 0)
            return;
        _yearScrollRetryAttempts = 0;
        Dispatcher.UIThread.Post(ScrollYearListToDisplayedYear, DispatcherPriority.Loaded);
    }

    private void ScrollYearListToDisplayedYear()
    {
        if (View != MaterialDatePickerView.Year || _choiceScroll is null || _choiceCells.Count == 0)
            return;
        // max(0, displayedYear - yearRange.first - YearsInRow): the anchor year is one grid row
        // above the displayed year, clamped into the year range.
        var minYear = Math.Min(MinimumYear, MaximumYear);
        var maxYear = Math.Max(MinimumYear, MaximumYear);
        var year = Math.Clamp(DisplayMonth.Year - YearColumns, minYear, maxYear);
        Control? anchor = null;
        foreach (var (cell, value) in _choiceCells)
        {
            if (value == year && cell is Control control)
            {
                anchor = control;
                break;
            }
        }
        if (anchor is null || anchor.Bounds.Height <= 0)
        {
            // The overlay has not been laid out yet: the year view becomes visible in the same
            // dispatcher turn that composes it.
            if (_yearScrollRetryAttempts++ < 4)
                Dispatcher.UIThread.Post(ScrollYearListToDisplayedYear, DispatcherPriority.Loaded);
            return;
        }
        _yearScrollRetryAttempts = 0;
        var viewport = _choiceScroll.Viewport.Height;
        if (!(viewport > 1))
            viewport = _yearRevealTarget > 1 ? _yearRevealTarget : _choiceScroll.Bounds.Height;
        var maxOffset = Math.Max(0, _choiceScroll.Extent.Height - viewport);
        var offset = Math.Clamp(anchor.Bounds.Y, 0, maxOffset);
        if (Math.Abs(_choiceScroll.Offset.Y - offset) > 0.5)
            _choiceScroll.Offset = new Vector(_choiceScroll.Offset.X, offset);
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
        ApplyFullScreenCellStates();
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

        AnimateViewTransition();
    }

    private void AnimateViewTransition()
    {
        var state = FormattableString.Invariant($"{Mode}|{View}|{IsInputMode}");
        var host = ResolveContentHost();
        if (host is null)
            return;

        if (_lastViewState is null || _lastViewState == state)
        {
            _lastViewState = state;
            _lastInputMode = IsInputMode;
            _lastView = View;
            _lastMode = Mode;
            if (_activeHost is null)
            {
                _activeHost = host;
                SettleContentHost(host);
            }
            return;
        }

        var outgoing = _activeHost;
        var previousView = _lastView;
        var previousMode = _lastMode;
        var inputChanged = _lastInputMode != IsInputMode;
        _lastViewState = state;
        _lastInputMode = IsInputMode;
        _lastView = View;
        _lastMode = Mode;

        // DatePicker.kt 1672-1688: the year picker is an overlay over the still-composed
        // calendar, so opening and closing it is a vertical expand/shrink of the year column
        // rather than a content crossfade. Month<->year switches and mode changes keep the
        // desktop crossfade below. The full-screen presentation composes the range-style
        // vertical browser instead of the day view, so it has no overlay to reveal.
        var overlayHost = Mode != MaterialDatePickerMode.FullScreen;
        var yearOpening = overlayHost && View == MaterialDatePickerView.Year &&
                          previousView == MaterialDatePickerView.Day;
        var yearClosing = overlayHost && previousView == MaterialDatePickerView.Year &&
                          View == MaterialDatePickerView.Day;
        if (!inputChanged && previousMode == Mode && (yearOpening || yearClosing))
        {
            if (_exitHost is { } pendingExit)
                CompleteExit(pendingExit);
            StopContentAnimations();
            if (_dayView is not null)
                SettleContentHost(_dayView);
            _activeHost = host;
            AnimateYearReveal(yearOpening);
            return;
        }

        // A mode, input or month<->year switch ends the year overlay's geometry; its local
        // height/opacity are released so the incoming host transition owns the content again.
        if (_yearRevealActive)
            ResetYearReveal();

        _activeHost = host;

        // An interrupted exit retargets from its current opacity/offset/velocity, the way
        // AnimatedContent continues a reversed transition instead of restarting from the spec's
        // initial value.
        var priorExit = _exitHost;
        var reversing = ReferenceEquals(host, priorExit);
        var reversingVelocity = reversing ? _exitOpacity?.Velocity ?? 0 : 0;
        var reversingOffsetVelocity = reversing ? _exitOffset?.Velocity ?? 0 : 0;
        var outgoingVelocity = ReferenceEquals(outgoing, _enterHost) ? _enterOpacity?.Velocity ?? 0 : 0;
        var outgoingOffsetVelocity = ReferenceEquals(outgoing, _enterHost)
            ? _enterOffset?.Velocity ?? 0
            : 0;

        // A third state in quick succession leaves an older host exiting; finish it now so it
        // cannot stay hit-testable behind the new content.
        if (priorExit is { } stale &&
            !ReferenceEquals(stale, host) &&
            !ReferenceEquals(stale, outgoing))
        {
            CompleteExit(stale);
        }

        StopContentAnimations();

        if (outgoing is null || ReferenceEquals(outgoing, host))
        {
            SettleContentHost(host);
            return;
        }

        // The template pseudo-classes hide the outgoing host as soon as the new state applies,
        // which would make MaterialMotion snap its spring to the target. Keep it visible locally
        // for the exit; CompleteExit restores style-driven visibility.
        outgoing.IsVisible = true;

        var hostOffset = EnsureOffset(host);
        var outgoingOffset = EnsureOffset(outgoing);
        var range = SelectionMode == MaterialDateSelectionMode.Range;

        if (range)
        {
            // DateRangePicker.kt SwitchableDateEntryContent uses Crossfade(FastEffects).
            host.Opacity = reversing ? host.Opacity : 0;
            hostOffset.X = 0;
            hostOffset.Y = 0;
            outgoingOffset.X = 0;
            _enterHost = host;
            _enterOpacity = MaterialMotion.Animate(host, host.Opacity, 1, value => host.Opacity = value,
                MaterialSpring.EffectsFast, initialVelocity: reversingVelocity);
            StartExit(outgoing, outgoingVelocity);
            return;
        }

        if (inputChanged)
        {
            // DatePicker.kt 1486-1540: the text field slides in from the full content height with
            // DefaultSpatial and fades in with DefaultEffects while the calendar fades out with
            // FastEffects and slides out to the -48 dp parallax target. Returning to the picker
            // reverses both directions.
            var fullHeight = ResolveContentHeight(host);
            host.Opacity = reversing ? host.Opacity : 0;
            hostOffset.X = 0;
            outgoingOffset.X = 0;
            var hostFrom = reversing ? hostOffset.Y : IsInputMode ? fullHeight : -ParallaxOffset;
            var outgoingTo = IsInputMode ? -ParallaxOffset : fullHeight;
            _enterHost = host;
            _enterOffset = MaterialMotion.Animate(host, hostFrom, 0, value => hostOffset.Y = value,
                MaterialSpring.SpatialDefault, initialVelocity: reversingOffsetVelocity);
            _enterOpacity = MaterialMotion.Animate(host, host.Opacity, 1, value => host.Opacity = value,
                MaterialSpring.EffectsDefault, initialVelocity: reversingVelocity);
            StartExit(outgoing, outgoingVelocity, outgoingOffset, outgoingTo,
                outgoingOffsetVelocity);
            return;
        }

        // Desktop composition for month/year views and docked/modal/full-screen changes: the
        // pinned year picker only crossfades (DefaultEffects in, FastEffects out) around its
        // expand/shrink, so the overlaid desktop hosts get no directional offset.
        host.Opacity = reversing ? host.Opacity : 0;
        hostOffset.X = 0;
        hostOffset.Y = 0;
        outgoingOffset.X = 0;
        _enterHost = host;
        _enterOpacity = MaterialMotion.Animate(host, host.Opacity, 1, value => host.Opacity = value,
            MaterialSpring.EffectsDefault, initialVelocity: reversingVelocity);
        StartExit(outgoing, outgoingVelocity);
    }

    private Control? ResolveContentHost() =>
        IsInputMode ? _inputPanel : Mode == MaterialDatePickerMode.FullScreen
            ? _fullScreenView
            : View == MaterialDatePickerView.Day ? _dayView : _choiceScroll;

    private void StartExit(Control host, double opacityVelocity)
    {
        _exitHost = host;
        _exitOpacity = MaterialMotion.Animate(host, host.Opacity, 0,
            value =>
            {
                host.Opacity = value;
                if (value <= ExitEpsilon)
                    CompleteExit(host);
            },
            MaterialSpring.EffectsFast, initialVelocity: opacityVelocity);
    }

    private void StartExit(Control host, double opacityVelocity, TranslateTransform offset,
        double targetY, double offsetVelocity)
    {
        _exitHost = host;
        // The offset spring is created first so a synchronously finished opacity spring (reduced
        // motion or a detached host) can dispose it.
        _exitOffset = MaterialMotion.Animate(host, offset.Y, targetY, value => offset.Y = value,
            MaterialSpring.SpatialDefault, initialVelocity: offsetVelocity);
        _exitOpacity = MaterialMotion.Animate(host, host.Opacity, 0,
            value =>
            {
                host.Opacity = value;
                if (value <= ExitEpsilon)
                    CompleteExit(host);
            },
            MaterialSpring.EffectsFast, initialVelocity: opacityVelocity);
    }

    private void CompleteExit(Control host)
    {
        if (_completingExit)
            return;
        _completingExit = true;
        try
        {
            _exitOffset?.Dispose();
            _exitOffset = null;
            host.Opacity = 0;
            ResetOffset(host);
            host.ClearValue(Visual.IsVisibleProperty);
            _exitHost = null;
        }
        finally
        {
            _completingExit = false;
        }
    }

    private void SettleContentHost(Control host)
    {
        host.Opacity = 1;
        ResetOffset(host);
        host.ClearValue(Visual.IsVisibleProperty);
    }

    private static TranslateTransform EnsureOffset(Control host)
    {
        if (host.RenderTransform is TranslateTransform existing)
            return existing;
        var offset = new TranslateTransform();
        host.RenderTransform = offset;
        return offset;
    }

    private static void ResetOffset(Control host)
    {
        if (host.RenderTransform is not TranslateTransform offset)
            return;
        offset.X = 0;
        offset.Y = 0;
    }

    private double ResolveContentHeight(Control host)
    {
        // The four content hosts share one content grid; its laid-out height is the full-height
        // slide distance AnimatedContent would pass to slideInVertically.
        var height = (host.Parent as Control)?.Bounds.Height ?? 0;
        if (height <= 0)
            height = Bounds.Height;
        return double.IsFinite(height) && height > 0 ? height : 1;
    }

    private void StopContentAnimations()
    {
        _enterOpacity?.Dispose();
        _enterOffset?.Dispose();
        _exitOpacity?.Dispose();
        _exitOffset?.Dispose();
        _enterOpacity = null;
        _enterOffset = null;
        _exitOpacity = null;
        _exitOffset = null;
        _enterHost = null;
        _exitHost = null;
    }

    // ---------------------------------------------------------------------------------------
    // Horizontal month paging: DatePicker.kt 1605-1641 (previous/next animateScrollToItem) and
    // 1808-1860 (LazyRow of full-width month pages with a snap fling, firstVisibleItemIndex
    // feeding displayedMonth). PART_DayGrid is the page of the committed month; at most two
    // neighbouring pages are composed beside it, exactly as the LazyRow composes only the pages
    // near the viewport.
    // ---------------------------------------------------------------------------------------

    private void InitializeMonthPager()
    {
        ResetMonthPager();
        if (_monthPager is null || _dayGrid is null)
            return;
        _livePageLayer = new MonthPageLayer(_dayGrid, _dayCells, EnsureOffset(_dayGrid));
        _pageOrigin = DisplayMonth;
    }

    /// <summary>
    /// Render-coordinate direction of the following month. The LTR calendar and an explicitly
    /// mirrored RTL tree place the next page towards +X; a culture-driven RTL calendar, whose rows
    /// are mirrored in code, runs the other way.
    /// </summary>
    private double PageCoordinateSign =>
        IsRightToLeft && FlowDirection != FlowDirection.RightToLeft ? -1 : 1;

    /// <summary>Screen-space sign of "forward": the content moves left in LTR, right in RTL.</summary>
    private double ForwardDragSign => IsRightToLeft ? 1 : -1;

    private static int MonthsBetween(DateTime from, DateTime to) =>
        (to.Year - from.Year) * 12 + to.Month - from.Month;

    /// <summary>
    /// Moves the displayed month by <paramref name="delta"/> months, animating the horizontal
    /// page when the pager can. The shared DisplayDate follows the target month immediately,
    /// like the picker's displayedMonth state; the page animation keeps the previous month
    /// visible beside it until the spring settles. Returns whether a page transition started.
    /// </summary>
    private bool AnimateMonthNavigation(int delta)
    {
        if (delta == 0)
            return false;
        var current = DisplayMonth;
        var target = ClampMonthToBounds(AddMonthsClamped(current, delta));
        // Same guard as SetDisplayMonth: months outside the year range are not addressable.
        if (!IsYearInRange(target.Year))
            return false;
        var steps = MonthsBetween(current, target);
        if (steps == 0)
            return false;

        if (TryOpenMonthPage(steps))
        {
            _pageNavigating = true;
            try
            {
                SetDisplayMonth(target);
            }
            finally
            {
                _pageNavigating = false;
            }
            return true;
        }

        SetDisplayMonth(target);
        return false;
    }

    private bool TryOpenMonthPage(int steps)
    {
        if (!CanAnimateMonthPages())
            return false;
        var width = _monthPager!.Bounds.Width;
        if (!(width > 1) || _livePageLayer is null)
            return false;

        // A fresh gesture starts from the committed month; an in-flight track keeps its origin and
        // position so a second arrow continues, or reverses, the same scroll.
        if (!_pageActive || _pageOrigin.AddMonths(_pageTargetIndex) != DisplayMonth)
            ResetMonthPagerTrack(DisplayMonth);

        _pageActive = true;
        _pageWidth = width;
        _pageTargetIndex = ClampPageIndex(_pageTargetIndex + steps);
        _livePageIndex = _pageTargetIndex;

        var velocity = _pageSpring?.Velocity ?? _pageDecayVelocity;
        StopPageAnimations();
        StartPageSpring(_pageTargetIndex, velocity);
        return true;
    }

    /// <summary>
    /// The pager owns the day view only: the text view, the month/year lists and reduced motion
    /// change the month without a page transition. The full-screen presentation composites the
    /// shared vertical month browser (the range architecture), so its arrows keep changing
    /// DisplayDate directly; the source's full-screen single-date picker reuses the same
    /// horizontal list, which would need the full-screen view to become a day view to reach.
    /// </summary>
    private bool CanAnimateMonthPages() =>
        _applied && _monthPager is { Bounds: { Width: > 1 } } && _livePageLayer is not null &&
        _dayGrid is not null && View == MaterialDatePickerView.Day && !IsInputMode &&
        Mode != MaterialDatePickerMode.FullScreen && this.IsAttachedToVisualTree() &&
        !MaterialMotion.GetIsReduced(this);

    private void ApplyPagePositionFrame()
    {
        var width = _pageWidth;
        if (!(width > 0) || _monthPager is null)
            return;
        var sign = PageCoordinateSign;
        if (_livePageLayer is not null)
            _livePageLayer.Translate.X = (_livePageIndex - _pagePosition) * width * sign;
        UpdateTransientPages(width, sign);
    }

    private void ApplyPagePositionValue(double value)
    {
        _pagePosition = value;
        ApplyPagePositionFrame();
        if (!_pageDragging && Math.Abs(value - _pageTargetIndex) <= PageEpsilon)
            CompletePageTransition();
    }

    private void StartPageSpring(int targetIndex, double initialVelocity)
    {
        if (_monthPager is null)
            return;
        _pageDecayVelocity = 0;
        _pageTargetIndex = targetIndex;
        if (Math.Abs(_pagePosition - targetIndex) <= PageEpsilon &&
            Math.Abs(initialVelocity) <= 0.001)
        {
            CompletePageTransition();
            return;
        }

        _pageSettled = false;
        // AnimationState.animateTo with the default spring(): stiffness 1500, no bounce. Passing
        // the current/release velocity keeps a reversed arrow continuous instead of restarting.
        var animation = MaterialMotion.Animate(_monthPager, _pagePosition, targetIndex,
            ApplyPagePositionValue, 1500, 1, PageEpsilon, initialVelocity);
        _pageSpring = animation;
        if (_pageSettled)
        {
            animation.Dispose();
            if (ReferenceEquals(_pageSpring, animation))
                _pageSpring = null;
        }
    }

    private void StartPageApproach(int targetIndex, double velocityPages, double approachPages)
    {
        if (_monthPager is null)
            return;
        _pageTargetIndex = targetIndex;
        var start = _pagePosition;
        var direction = Math.Sign(approachPages);
        var end = start + approachPages;
        // LazyListSnapLayoutInfoProvider bounds the approach by the neighbouring snap offsets.
        var bound = direction > 0
            ? Math.Floor(start + PageEpsilon) + 1
            : Math.Ceiling(start - PageEpsilon) - 1;
        if (direction > 0 ? end > bound : end < bound)
            end = bound;
        end = ClampPagePosition(end);

        var lower = Math.Min(start, end);
        var upper = Math.Max(start, end);
        _pageDecayVelocity = velocityPages;
        _pageDecay = MaterialMotion.Decay(_monthPager, 0, velocityPages, (value, velocity) =>
        {
            _pageDecayVelocity = velocity;
            var clamped = Math.Clamp(start + value, lower, upper);
            _pagePosition = clamped;
            ApplyPagePositionFrame();
            var reached = direction > 0
                ? clamped >= end - PageEpsilon
                : clamped <= end + PageEpsilon;
            if (!reached)
                return;
            _pageDecay?.Dispose();
            _pageDecay = null;
            // SnapFlingBehavior snaps from the approach's remaining velocity.
            StartPageSpring(targetIndex, velocity);
        });
    }

    /// <summary>
    /// SnapFlingBehavior/rememberSnapFlingBehavior: a high-velocity release decays for the spline
    /// distance minus one page and then snaps; otherwise it snaps to the closest page.
    /// </summary>
    private void SnapPageTo(int targetIndex, double velocityPages, double width)
    {
        var screenVelocity = velocityPages * width;
        if (Math.Abs(screenVelocity) >= MinFlingVelocity)
        {
            var decayPages = new SplineDecay(0, screenVelocity).TargetValue / width;
            var approach = Math.Max(Math.Abs(decayPages) - 1, 0) * Math.Sign(decayPages);
            if (Math.Abs(approach) > MinPageApproach)
            {
                StartPageApproach(targetIndex, velocityPages, approach);
                return;
            }
        }
        StartPageSpring(targetIndex, velocityPages);
    }

    private void CompletePageTransition()
    {
        if (!_pageActive)
            return;
        _pageSettled = true;
        var targetMonth = _pageOrigin.AddMonths(_pageTargetIndex);
        StopPageAnimations();
        _pageActive = false;
        _pagePosition = 0;
        _pageOrigin = targetMonth;
        _livePageIndex = 0;
        _pageTargetIndex = 0;
        _pageWidth = 0;
        _pageDecayVelocity = 0;
        if (_pageSlotA is not null)
        {
            _pageSlotA.Grid.IsVisible = false;
            _pageSlotA.HasMonth = false;
        }
        if (_pageSlotB is not null)
        {
            _pageSlotB.Grid.IsVisible = false;
            _pageSlotB.HasMonth = false;
        }
        if (_livePageLayer is not null)
            _livePageLayer.Translate.X = 0;

        // A swipe commits the month it lands on; an arrow already committed its target.
        if (DisplayMonth != targetMonth)
        {
            _pageNavigating = true;
            try
            {
                SetDisplayMonth(targetMonth);
            }
            finally
            {
                _pageNavigating = false;
            }
        }

        // goToMonth: move focus only after animateScrollToItem has finished, so the ring never
        // sits on a cell that is still clipped by the pager.
        if (_pendingPageFocus is { } pending)
        {
            _pendingPageFocus = null;
            PostMoveFocus(pending);
        }
    }

    private void ResetMonthPagerTrack(DateTime month)
    {
        StopPageAnimations();
        _pageSettled = false;
        _pageActive = false;
        _pagePosition = 0;
        _livePageIndex = 0;
        _pageTargetIndex = 0;
        _pageOrigin = month;
        _pageWidth = 0;
        _pageDecayVelocity = 0;
        _pageSamples.Clear();
        // A keyboard month move that never settles must not steal focus on a later page.
        _pendingPageFocus = null;
        if (_pageSlotA is not null)
        {
            _pageSlotA.Grid.IsVisible = false;
            _pageSlotA.HasMonth = false;
        }
        if (_pageSlotB is not null)
        {
            _pageSlotB.Grid.IsVisible = false;
            _pageSlotB.HasMonth = false;
        }
        if (_livePageLayer is not null)
            _livePageLayer.Translate.X = 0;
    }

    private void ResetMonthPager()
    {
        _pagePressed = false;
        _pageDragging = false;
        _pageDragExceeded = false;
        ResetMonthPagerTrack(DisplayMonth);
    }

    private void StopPageAnimations()
    {
        _pageDecay?.Dispose();
        _pageDecay = null;
        _pageSpring?.Dispose();
        _pageSpring = null;
    }

    private void UpdateTransientPages(double width, double sign)
    {
        _pageSlotAUsed = false;
        _pageSlotBUsed = false;
        var low = (int)Math.Floor(_pagePosition + PageEpsilon);
        var high = (int)Math.Ceiling(_pagePosition - PageEpsilon);
        PlaceTransientPage(low, width, sign);
        if (high != low)
            PlaceTransientPage(high, width, sign);
        if (_pageSlotA is { } slotA && !_pageSlotAUsed)
            slotA.Grid.IsVisible = false;
        if (_pageSlotB is { } slotB && !_pageSlotBUsed)
            slotB.Grid.IsVisible = false;
    }

    private void PlaceTransientPage(int pageIndex, double width, double sign)
    {
        if (pageIndex == _livePageIndex)
            return;
        var monthIndex = (long)(_pageOrigin.Year - 1) * 12 + _pageOrigin.Month - 1 + pageIndex;
        if (monthIndex < 0 || monthIndex >= 9999 * 12)
            return;
        var month = _pageOrigin.AddMonths(pageIndex);
        MonthPageLayer? slot = null;
        if (_pageSlotA is { HasMonth: true } slotA && slotA.Month == month)
            slot = slotA;
        else if (_pageSlotB is { HasMonth: true } slotB && slotB.Month == month)
            slot = slotB;

        if (slot is null)
        {
            if (!_pageSlotAUsed)
                slot = _pageSlotA ??= CreatePageLayer();
            else if (!_pageSlotBUsed)
                slot = _pageSlotB ??= CreatePageLayer();
            else
                slot = _pageSlotA!;
            if (!slot.HasMonth || slot.Month != month)
                PreparePageLayer(slot, month);
        }

        slot.Grid.IsVisible = true;
        slot.Translate.X = (pageIndex - _pagePosition) * width * sign;
        if (ReferenceEquals(slot, _pageSlotA))
            _pageSlotAUsed = true;
        else if (ReferenceEquals(slot, _pageSlotB))
            _pageSlotBUsed = true;
    }

    private MonthPageLayer CreatePageLayer()
    {
        var grid = new UniformGrid
        {
            Columns = 7,
            IsVisible = false,
            IsHitTestVisible = false,
        };
        // Composed neighbours are decoration: they never take focus or appear to automation.
        AutomationProperties.SetAccessibilityView(grid, AccessibilityView.Raw);
        var cells = new MaterialDateCell[DayCellCount];
        for (var i = 0; i < cells.Length; i++)
        {
            var cell = new MaterialDateCell { Focusable = false, IsTabStop = false };
            cells[i] = cell;
            grid.Children.Add(cell);
        }
        var translate = new TranslateTransform();
        grid.RenderTransform = translate;
        _monthPager?.Children.Add(grid);
        return new MonthPageLayer(grid, cells, translate);
    }

    private void PreparePageLayer(MonthPageLayer layer, DateTime month)
    {
        var first = new DateTime(month.Year, month.Month, 1);
        var offset = ((int)first.DayOfWeek - (int)FirstDayOfWeek + 7) % 7;
        var startDay = first.Ticks / TimeSpan.TicksPerDay - offset;
        layer.Month = month;
        layer.HasMonth = true;
        for (var i = 0; i < DayCellCount; i++)
        {
            var cell = layer.Cells[i];
            var day = startDay + i;
            cell.Tag = day >= 0 && day <= DateTime.MaxValue.Ticks / TimeSpan.TicksPerDay
                ? new DateTime(day * TimeSpan.TicksPerDay) : null;
            cell.ContextMonth = month;
        }
        ApplyCellStates(layer.Cells);
        layer.Grid.Children.Clear();
        foreach (var cell in layer.Cells)
            layer.Grid.Children.Add(cell);
        MirrorRows(layer.Grid, 7);
    }

    private void AddPageSample(double x)
    {
        var seconds = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
        _pageSamples.Add((seconds, x));
        while (_pageSamples.Count > 2 && seconds - _pageSamples[0].Seconds > 0.1)
            _pageSamples.RemoveAt(0);
    }

    private double ComputePageScreenVelocity()
    {
        if (_pageSamples.Count < 2)
            return 0;
        var first = _pageSamples[0];
        var last = _pageSamples[^1];
        var seconds = last.Seconds - first.Seconds;
        return seconds > 0.001 ? (last.X - first.X) / seconds : 0;
    }

    private int MinimumPageIndex => MonthsBetween(_pageOrigin, FirstPagerMonth);

    private int MaximumPageIndex => MonthsBetween(_pageOrigin, LastPagerMonth);

    private DateTime FirstPagerMonth
    {
        get
        {
            var first = new DateTime(Math.Min(MinimumYear, MaximumYear), 1, 1);
            if (MinimumDate is { } min)
            {
                var month = new DateTime(min.Date.Year, min.Date.Month, 1);
                if (month > first)
                    first = month;
            }
            return first;
        }
    }

    private DateTime LastPagerMonth
    {
        get
        {
            var last = new DateTime(Math.Max(MinimumYear, MaximumYear), 12, 1);
            if (MaximumDate is { } max)
            {
                var month = new DateTime(max.Date.Year, max.Date.Month, 1);
                if (month < last)
                    last = month;
            }
            return last;
        }
    }

    private int ClampPageIndex(int index)
    {
        var min = MinimumPageIndex;
        var max = MaximumPageIndex;
        return min > max ? 0 : Math.Clamp(index, min, max);
    }

    private double ClampPagePosition(double position)
    {
        var min = MinimumPageIndex;
        var max = MaximumPageIndex;
        return min > max ? 0 : Math.Clamp(position, min, max);
    }

    private void OnMonthPagerPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_monthPager is null || !CanAnimateMonthPages())
            return;
        if (!e.GetCurrentPoint(_monthPager).Properties.IsLeftButtonPressed)
            return;
        _pagePressed = true;
        _pageDragExceeded = false;
        _pageDragging = false;
        _pageDragStart = e.GetPosition(_monthPager);
        // A press stops an in-flight page the way a touch-down stops a scroll animation.
        if (_pageActive)
            StopPageAnimations();
        _pageDragStartPosition = _pagePosition;
        _pageSamples.Clear();
        AddPageSample(_pageDragStart.X);
    }

    private void OnMonthPagerPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_pagePressed || _monthPager is null)
            return;
        var point = e.GetPosition(_monthPager);
        if (!_pageDragExceeded)
        {
            if (Math.Abs(point.X - _pageDragStart.X) < PageDragSlop)
                return;
            _pageDragExceeded = true;
            BeginPageDrag(e);
            if (!_pageDragging)
                return;
        }
        if (!_pageDragging || _monthPager is null)
            return;
        var width = _pageWidth > 0 ? _pageWidth : _monthPager.Bounds.Width;
        if (!(width > 0))
            return;
        var position = _pageDragStartPosition +
                       (point.X - _pageDragStart.X) / (ForwardDragSign * width);
        _pagePosition = ClampPagePosition(position);
        AddPageSample(point.X);
        ApplyPagePositionFrame();
        e.Handled = true;
    }

    private void BeginPageDrag(PointerEventArgs e)
    {
        if (_monthPager is null)
            return;
        if (!_pageActive)
        {
            // The committed month is the anchor until the drag settles on another page.
            _pageOrigin = DisplayMonth;
            _pagePosition = 0;
            _livePageIndex = 0;
            _pageTargetIndex = 0;
            _pageDragStartPosition = 0;
        }
        _pageActive = true;
        _pageWidth = _monthPager.Bounds.Width;
        _pageDecayVelocity = 0;
        _pageDragging = true;
        e.Pointer.Capture(_monthPager);
    }

    private void OnMonthPagerPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_pagePressed || _monthPager is null)
            return;
        var dragged = _pageDragExceeded;
        _pagePressed = false;
        _pageDragExceeded = false;
        _pageDragging = false;
        if (dragged)
        {
            AddPageSample(e.GetPosition(_monthPager).X);
            e.Pointer.Capture(null);
            CompletePageDrag();
            e.Handled = true;
            return;
        }
        // A press that stopped an in-flight page settles it onto the nearest page.
        if (_pageActive)
            CompletePageDrag();
    }

    private void OnMonthPagerPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (!_pageDragging)
            return;
        // An interrupted drag returns to the nearest page instead of keeping a foreign offset.
        _pagePressed = false;
        _pageDragExceeded = false;
        _pageDragging = false;
        StartPageSpring(ClampPageIndex((int)Math.Round(_pagePosition, MidpointRounding.AwayFromZero)), 0);
    }

    private void CompletePageDrag()
    {
        _pageDragging = false;
        var width = _pageWidth > 0 ? _pageWidth : _monthPager?.Bounds.Width ?? 0;
        if (!(width > 0) || !_pageActive)
        {
            CompletePageTransition();
            return;
        }
        _pageWidth = width;
        var screenVelocity = ComputePageScreenVelocity();
        var velocityPages = screenVelocity / (ForwardDragSign * width);
        // LazyListSnapLayoutInfoProvider.calculateFinalSnappingItem: past MinFlingVelocity the
        // page in the fling direction wins, otherwise the closest page.
        var target = Math.Abs(screenVelocity) >= MinFlingVelocity
            ? velocityPages > 0
                ? (int)Math.Floor(_pagePosition + PageEpsilon) + 1
                : (int)Math.Ceiling(_pagePosition - PageEpsilon) - 1
            : (int)Math.Round(_pagePosition, MidpointRounding.AwayFromZero);
        target = ClampPageIndex(target);
        SnapPageTo(target, velocityPages, width);
    }

    // ---------------------------------------------------------------------------------------
    // Year picker reveal: DatePicker.kt 1672-1688 expandVertically(DefaultEffects) +
    // fadeIn(DefaultEffects, initialAlpha = 0.6) / shrinkVertically(DefaultEffects) +
    // fadeOut(FastEffects).
    // ---------------------------------------------------------------------------------------

    private void AnimateYearReveal(bool entering)
    {
        if (_choiceScroll is null)
            return;
        // The full-screen presentation composes the vertical browser instead of the day view, so
        // the year overlay has no host geometry to expand from.
        if (Mode == MaterialDatePickerMode.FullScreen)
        {
            ResetYearReveal();
            return;
        }
        var reduced = MaterialMotion.GetIsReduced(this) || !this.IsAttachedToVisualTree();
        if (entering)
        {
            if (reduced)
            {
                ResetYearReveal();
                return;
            }
            var target = ResolveYearOverlayHeight();
            var heightFrom = _yearRevealActive ? _yearHeightValue : 0;
            var opacityFrom = _yearRevealActive ? _yearOpacityValue : YearFadeInInitialAlpha;
            var heightVelocity = _yearHeight?.Velocity ?? 0;
            var opacityVelocity = _yearOpacity?.Velocity ?? 0;
            StopYearAnimations();
            PrepareYearRevealHost(heightFrom, opacityFrom);
            StartYearHeight(target, heightVelocity, entering: true);
            StartYearOpacity(1, MaterialSpring.EffectsDefault, opacityVelocity);
            return;
        }

        var natural = _choiceScroll.Bounds.Height;
        if (!_yearRevealActive && natural <= 0.5)
        {
            ResetYearReveal();
            return;
        }
        if (reduced)
        {
            ResetYearReveal();
            return;
        }
        var from = _yearRevealActive ? _yearHeightValue : natural;
        var fromOpacity = _yearRevealActive ? _yearOpacityValue : _choiceScroll.Opacity;
        var exitHeightVelocity = _yearHeight?.Velocity ?? 0;
        var exitOpacityVelocity = _yearOpacity?.Velocity ?? 0;
        StopYearAnimations();
        PrepareYearRevealHost(from, fromOpacity);
        StartYearHeight(0, exitHeightVelocity, entering: false);
        StartYearOpacity(0, MaterialSpring.EffectsFast, exitOpacityVelocity);
    }

    private void PrepareYearRevealHost(double height, double opacity)
    {
        if (_choiceScroll is null)
            return;
        _yearRevealActive = true;
        _yearRevealTarget = ResolveYearOverlayHeight();
        _choiceScroll.IsVisible = true;
        // DatePicker.kt AnimatedVisibility(enter = expandVertically(), exit = shrinkVertically())
        // uses Compose's default expandFrom/shrinkTowards = Alignment.Bottom: the clip bounds grow
        // downward from the top of the calendar area while the year column stays bottom-aligned
        // inside those bounds, so the content is revealed from its bottom edge and slides down
        // into place. The host therefore grows from the top and its content is shifted up by the
        // still-clipped distance.
        _choiceScroll.VerticalAlignment = VerticalAlignment.Top;
        ApplyYearRevealFrame(height, opacity);
    }

    private void ApplyYearRevealFrame(double height, double opacity)
    {
        _yearHeightValue = height;
        _yearOpacityValue = opacity;
        if (_choiceScroll is null)
            return;
        _choiceScroll.Height = height;
        _choiceScroll.Opacity = opacity;
        if (_choiceHost is not null)
            EnsureOffset(_choiceHost).Y = Math.Min(0, height - _yearRevealTarget);
    }

    private void StartYearHeight(double target, double velocity, bool entering)
    {
        if (_choiceScroll is null)
            return;
        _yearRevealSettled = false;
        var animation = MaterialMotion.Animate(_choiceScroll, _yearHeightValue, target,
            value =>
            {
                ApplyYearRevealFrame(value, _yearOpacityValue);
                if (entering ? value >= target - 0.5 : value <= 0.5)
                {
                    if (entering)
                        FinishYearEnter();
                    else
                        FinishYearExit();
                }
            }, MaterialSpring.EffectsDefault, 0.5, velocity);
        _yearHeight = animation;
        if (_yearRevealSettled)
        {
            animation.Dispose();
            if (ReferenceEquals(_yearHeight, animation))
                _yearHeight = null;
        }
    }

    private void StartYearOpacity(double target, MaterialSpring spring, double velocity)
    {
        if (_choiceScroll is null)
            return;
        var animation = MaterialMotion.Animate(_choiceScroll, _yearOpacityValue, target,
            value => ApplyYearRevealFrame(_yearHeightValue, value), spring, 0.001, velocity);
        _yearOpacity = animation;
        if (_yearRevealSettled)
        {
            animation.Dispose();
            if (ReferenceEquals(_yearOpacity, animation))
                _yearOpacity = null;
        }
    }

    private void FinishYearEnter()
    {
        if (!_yearRevealActive)
            return;
        _yearRevealActive = false;
        _yearRevealSettled = true;
        StopYearAnimations();
        if (_choiceScroll is null)
            return;
        _yearHeightValue = ResolveYearOverlayHeight();
        _yearOpacityValue = 1;
        if (_choiceHost is not null)
            ResetOffset(_choiceHost);
        _choiceScroll.ClearValue(Layoutable.HeightProperty);
        _choiceScroll.ClearValue(Layoutable.VerticalAlignmentProperty);
        _choiceScroll.ClearValue(Visual.OpacityProperty);
        _choiceScroll.ClearValue(Visual.IsVisibleProperty);
    }

    private void FinishYearExit()
    {
        if (!_yearRevealActive)
            return;
        _yearRevealActive = false;
        _yearRevealSettled = true;
        StopYearAnimations();
        if (_choiceScroll is null)
            return;
        _yearHeightValue = 0;
        _yearOpacityValue = 1;
        if (_choiceHost is not null)
            ResetOffset(_choiceHost);
        _choiceScroll.ClearValue(Layoutable.HeightProperty);
        _choiceScroll.ClearValue(Layoutable.VerticalAlignmentProperty);
        _choiceScroll.ClearValue(Visual.OpacityProperty);
        _choiceScroll.ClearValue(Visual.IsVisibleProperty);
    }

    private void ResetYearReveal()
    {
        _yearRevealActive = false;
        _yearRevealSettled = false;
        StopYearAnimations();
        _yearHeightValue = 0;
        _yearOpacityValue = 1;
        if (_choiceHost is not null)
            ResetOffset(_choiceHost);
        if (_choiceScroll is null)
            return;
        _choiceScroll.ClearValue(Layoutable.HeightProperty);
        _choiceScroll.ClearValue(Layoutable.VerticalAlignmentProperty);
        _choiceScroll.ClearValue(Visual.OpacityProperty);
        _choiceScroll.ClearValue(Visual.IsVisibleProperty);
    }

    private void StopYearAnimations()
    {
        _yearHeight?.Dispose();
        _yearHeight = null;
        _yearOpacity?.Dispose();
        _yearOpacity = null;
    }

    private double ResolveYearOverlayHeight()
    {
        // DatePicker.kt: RequiredSizeForAccessibility * (MaxCalendarRows + 1) minus the divider,
        // i.e. the weekday row plus the six calendar rows the overlay covers.
        var height = _dayView?.Bounds.Height ?? 0;
        if (height <= 1)
            height = _choiceScroll?.Bounds.Height ?? 0;
        if (height <= 1)
            height = DayCellSize * (DayRowCount + 1);
        return height;
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
        PseudoClasses.Set(":invalid", !string.IsNullOrEmpty(_validationMessage));
        PseudoClasses.Set(":no-actions", !ShowActions);
        PseudoClasses.Set(":no-input-mode", !AllowInputMode);

        // DatePicker.kt YearPickerMenuButton(expanded = yearPickerVisible): the caption's dropdown
        // chevron rotates while the year overlay it owns is open. These are native Buttons, so use
        // a regular styling class; Avalonia reserves pseudo-class mutation for the owner control.
        var expanded = View == MaterialDatePickerView.Year;
        _monthYearButton?.Classes.Set("expanded", expanded);
        _yearMenuButton?.Classes.Set("expanded", expanded);
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
            {
                cell.Content = null;
                cell.IsEnabled = false;
                cell.Opacity = 0;
                cell.IsHitTestVisible = false;
                AutomationProperties.SetName(cell, string.Empty);
                AutomationProperties.SetHelpText(cell, null);
                continue;
            }
            cell.Opacity = 1;
            cell.IsHitTestVisible = true;

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
            cell.SetState(":rtl", IsRightToLeft);
            cell.SetState(":range-start", completedRange && isStart);
            cell.SetState(":range-end", completedRange && isEnd);
            cell.SetState(":range-middle", middle);
            cell.SetState(":range-preview-start", hasPreview && previewStart == date);
            cell.SetState(":range-preview-end", hasPreview && previewEnd == date);
            cell.SetState(":range-preview-middle",
                hasPreview && previewStart < date && date < previewEnd);
            cell.Content = hidden ? null : date.Day.ToString(culture);
            cell.IsEnabled = !hidden && IsYearAllowed(cell.ContextMonth.Year) && IsDateSelectable(date);

            AutomationProperties.SetName(cell, hidden ? string.Empty : date.ToString("D", culture));
            string? help = null;
            if (!hidden)
            {
                if (outside)
                {
                    help = GetText("MaterialDatePickerOutsideMonthText", "Outside month");
                }
                else
                {
                    // Port of dayContentDescription: range role first, then Today, then the
                    // localized full date (exposed as the automation name).
                    var builder = new StringBuilder();
                    if (range && end is not null)
                    {
                        if (isStart)
                            builder.Append(GetText("MaterialDatePickerStartDateText", "Start date"));
                        else if (isEnd)
                            builder.Append(GetText("MaterialDatePickerEndDateText", "End date"));
                        else if (middle)
                            builder.Append(GetText("MaterialDatePickerInRangeText", "In range"));
                    }
                    if (date == today)
                    {
                        if (builder.Length > 0)
                            builder.Append(", ");
                        builder.Append(GetText("MaterialDatePickerTodayText", "Today"));
                    }
                    if (builder.Length > 0)
                        help = builder.ToString();
                }
            }
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
        var currentYear = Today.Year;
        foreach (var (cell, value) in _choiceCells)
        {
            var isYearView = View == MaterialDatePickerView.Year;
            var selected = isYearView
                ? IsYearSelected(value, start, end)
                : IsMonthSelected(display.Year, value, start, end);
            cell.SetSelected(selected);
            cell.SetCurrent(isYearView
                ? value == currentYear
                : display.Year == currentYear && value == Today.Month);
            if (cell is Control control)
            {
                control.IsEnabled = isYearView
                    ? IsYearAllowed(value)
                    : CanShowMonth(new DateTime(display.Year, value, 1));
            }
        }
    }

    private static bool IsMonthSelected(int year, int month, DateTime? start, DateTime? end)
    {
        if (start is not { } s)
            return false;
        var first = new DateTime(year, month, 1);
        var last = new DateTime(year, month, DateTime.DaysInMonth(year, month));
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
        var canPreviousMonth = display > DateTime.MinValue && CanShowMonth(display.AddMonths(-1));
        var canNextMonth = (display.Year < 9999 || display.Month < 12) && CanShowMonth(display.AddMonths(1));
        var canPreviousYear = display.Year > 1 && CanShowMonth(display.AddMonths(-12));
        var canNextYear = display.Year < 9999 && CanShowMonth(display.AddMonths(12));

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
        // MonthsNavigation in the pinned source only checks the year range; the selectable
        // predicate does not stop the calendar from scrolling into an excluded year.
        if (!IsYearInRange(month.Year))
            return false;
        var first = new DateTime(month.Year, month.Month, 1);
        var last = new DateTime(month.Year, month.Month, DateTime.DaysInMonth(month.Year, month.Month));
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
        {
            _okButton.IsVisible = showActions;
            _okButton.IsEnabled = IsRange
                ? SelectedDate is not null && SelectedEndDate is not null
                : SelectedDate is not null;
        }
    }

    private void UpdateEffectiveLabel()
    {
        var value = !string.IsNullOrWhiteSpace(Label)
            ? Label!
            : GetText("MaterialDatePickerSelectDateText", "Select date");
        // Port of DatePickerDefaults.DatePickerTitle: the text view uses DateInputTitle.
        if (IsInputMode && string.IsNullOrWhiteSpace(Label))
            value = GetText("MaterialDatePickerEnterDateText", "Enter date");
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
        // Port of DatePickerDefaults.DatePickerHeadline / DateRangePicker headline: the selected
        // date uses the yMMMd skeleton (month, day, year), and the empty value depends on the
        // display mode.
        var culture = EffectiveCulture;
        if (SelectedDate is not { } start)
            return IsInputMode
                ? GetText("MaterialDatePickerEnterDateText", "Enter date")
                : GetText("MaterialDatePickerSelectDateText", "Select date");
        var startDate = start.Date;
        if (SelectionMode == MaterialDateSelectionMode.Range)
        {
            if (SelectedEndDate is not { } end)
                return startDate.ToString("MMM d, yyyy", culture);
            return $"{startDate.ToString("MMM d, yyyy", culture)} – " +
                   $"{end.Date.ToString("MMM d, yyyy", culture)}";
        }
        return startDate.ToString("MMM d, yyyy", culture);
    }

    private void UpdateMonthYearText()
    {
        var value = DisplayMonth.ToString("Y", EffectiveCulture);
        if (value != _monthYearText)
            SetAndRaise(MonthYearTextProperty, ref _monthYearText, value);
    }

    private void UpdatePlaceholder()
    {
        var value = InputFormat.PatternWithDelimiters.ToUpperInvariant();
        if (value != _placeholder)
            SetAndRaise(PlaceholderProperty, ref _placeholder, value);
    }

    private void SyncInputText(bool force)
    {
        _syncingInput = true;
        try
        {
            if (_startInput is not null && (force || !_startInput.IsFocused))
            {
                var text = SelectedDate is { } start
                    ? InputFormat.Format(SelectedDateDigits(start.Date))
                    : string.Empty;
                _startInput.Text = text;
                // The echoed TextChanged must not be mistaken for a user edit of this value.
                RecordSyncedInputText(isEnd: false, text);
            }
            if (_endInput is not null && (force || !_endInput.IsFocused))
            {
                var text = SelectedEndDate is { } end
                    ? InputFormat.Format(SelectedDateDigits(end.Date))
                    : string.Empty;
                _endInput.Text = text;
                RecordSyncedInputText(isEnd: true, text);
            }
        }
        finally
        {
            _syncingInput = false;
        }
    }

    private string SelectedDateDigits(DateTime date) =>
        date.ToString(InputFormat.PatternWithoutDelimiters, EffectiveCulture);

    private string CurrentInputDigits(bool isEnd)
    {
        var value = isEnd ? SelectedEndDate : SelectedDate;
        return value is { } date ? SelectedDateDigits(date.Date) : string.Empty;
    }

    private void FocusTextInput()
    {
        var box = IsRange && SelectedDate is not null && SelectedEndDate is null ? _endInput : _startInput;
        box?.Focus();
        box?.SelectAll();
    }

    private void SetPreview(DateTime date)
    {
        if (SelectionMode != MaterialDateSelectionMode.Range || !_awaitingRangeEnd)
            return;
        if (_previewDate == date)
            return;
        _previewDate = date;
        ApplyCellStates(_dayCells);
        ApplyFullScreenCellStates();
    }

    private void OnDayGridPointerExited(object? sender, PointerEventArgs e)
    {
        if (_previewDate is null)
            return;
        _previewDate = null;
        ApplyCellStates(_dayCells);
        ApplyFullScreenCellStates();
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
        if (sender is not Control { Tag: int month, IsEnabled: true })
            return;
        var target = new DateTime(DisplayMonth.Year, month, 1);
        if (!CanShowMonth(target))
            return;
        SetDisplayMonth(target);
        SetCurrentValue(ViewProperty, MaterialDatePickerView.Day);
        var display = DisplayMonth;
        MoveFocus(SelectableDayInMonth(display.Year, display.Month, _focusDate.Day));
    }

    /// <summary>
    /// Picks the preferred day of the target month, or the nearest selectable day when a
    /// <see cref="SelectableDates"/> predicate excludes it, so returning from the month/year view
    /// never focuses a disabled cell.
    /// </summary>
    private DateTime SelectableDayInMonth(int year, int month, int preferredDay)
    {
        var days = DateTime.DaysInMonth(year, month);
        var preferred = Math.Clamp(preferredDay, 1, days);
        for (var offset = 0; offset < days; offset++)
        {
            var after = preferred + offset;
            if (after <= days && IsDateSelectable(new DateTime(year, month, after)))
                return new DateTime(year, month, after);
            var before = preferred - offset;
            if (offset > 0 && before >= 1 && IsDateSelectable(new DateTime(year, month, before)))
                return new DateTime(year, month, before);
        }
        return new DateTime(year, month, preferred);
    }

    private void OnYearChoiceClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Control { Tag: int year, IsEnabled: true } || !IsYearAllowed(year))
            return;
        var target = new DateTime(year, DisplayMonth.Month, 1);
        SetDisplayMonth(target);
        SetCurrentValue(ViewProperty, MaterialDatePickerView.Day);
        var display = DisplayMonth;
        MoveFocus(SelectableDayInMonth(display.Year, display.Month, _focusDate.Day));
    }

    private void OnPreviousMonthClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => NavigateMonth(-1);

    private void OnNextMonthClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => NavigateMonth(1);

    private void OnPreviousYearClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => NavigateYear(-1);

    private void OnNextYearClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => NavigateYear(1);

    private void OnMonthMenuClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SetCurrentValue(ViewProperty, MaterialDatePickerView.Month);
        Dispatcher.UIThread.Post(FocusActiveCell, DispatcherPriority.Background);
    }

    private void OnYearMenuClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SetCurrentValue(ViewProperty, MaterialDatePickerView.Year);
        Dispatcher.UIThread.Post(FocusActiveCell, DispatcherPriority.Background);
    }

    private void OnMonthYearButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var view = View switch
        {
            MaterialDatePickerView.Day => MaterialDatePickerView.Month,
            MaterialDatePickerView.Month => MaterialDatePickerView.Year,
            _ => MaterialDatePickerView.Day,
        };
        SetCurrentValue(ViewProperty, view);
        Dispatcher.UIThread.Post(FocusActiveCell, DispatcherPriority.Background);
    }

    private void OnEditButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var toInput = !IsInputMode;
        // The IsInputMode transition owns the source displayMode rule that re-targets the
        // displayed month to the selected start.
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

    private void OnStartInputKeyDown(object? sender, KeyEventArgs e) => HandleInputKey(e, isEnd: false);

    private void OnEndInputKeyDown(object? sender, KeyEventArgs e) => HandleInputKey(e, isEnd: true);

    private void HandleInputKey(KeyEventArgs e, bool isEnd)
    {
        if (e.Key != Key.Enter)
            return;
        e.Handled = true;
        var box = isEnd ? _endInput : _startInput;
        if (box is null || !string.IsNullOrEmpty(_validationMessage))
            return;
        var digits = DigitsOf(box.Text);
        if (digits.Length != InputFormat.DigitCount)
        {
            // Incomplete text stays editable and is never confirmed.
            return;
        }
        if (!IsRange || isEnd)
            Commit();
    }

    private void OnStartInputTextChanged(object? sender, TextChangedEventArgs e) =>
        ApplyMaskedInput(_startInput, isEnd: false);

    private void OnEndInputTextChanged(object? sender, TextChangedEventArgs e) =>
        ApplyMaskedInput(_endInput, isEnd: true);

    private void OnStartInputLostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // The source date input updates its draft while typing; losing focus is not a commit
        // point. Restore the last valid value when the buffer is not a complete date.
        NormalizeInputOnBlur(_startInput, isEnd: false);
    }

    private void OnEndInputLostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        NormalizeInputOnBlur(_endInput, isEnd: true);
    }

    private void NormalizeInputOnBlur(TextBox? box, bool isEnd)
    {
        if (box is null)
            return;
        var digits = DigitsOf(box.Text);
        if (digits.Length == 0)
            return;
        if (digits.Length != InputFormat.DigitCount)
        {
            SetValidationMessage(null);
            return;
        }
        // A complete but invalid value keeps its error; a valid one is left as typed.
        ApplyMaskedInput(box, isEnd, force: true);
    }

    private string DigitsOf(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;
        var builder = new StringBuilder(InputFormat.DigitCount);
        foreach (var c in text)
        {
            if (char.IsDigit(c))
                builder.Append((char)('0' + (int)char.GetNumericValue(c)));
        }
        return builder.ToString();
    }

    /// <summary>
    /// Enforces the source's digit-only input transformation and output mask: only digits are
    /// accepted, the locale delimiter is inserted automatically, and the draft selection changes
    /// as soon as the text forms a complete date. The <see cref="TextBox"/> remains the native
    /// editing surface (selection, caret, clipboard, undo and IME).
    /// </summary>
    private void ApplyMaskedInput(TextBox? box, bool isEnd, bool force = false)
    {
        if (box is null || _syncingInput)
            return;
        if (!force && !box.IsFocused)
            return;

        var format = InputFormat;
        var raw = box.Text ?? string.Empty;
        // TextBox raises TextChanged asynchronously, so both the mask's rewrite and the selection
        // sync come back as notifications that carry no new user edit. Re-processing them would
        // apply the same draft twice and would resurrect a draft that the first pass rejected.
        if (!force && string.Equals(raw, SyncedInputText(isEnd), StringComparison.Ordinal))
            return;
        var caret = box.CaretIndex;
        var digitOffset = format.ToDigitOffset(raw, caret);
        var digits = DigitsOf(raw);

        // Reject non-digit input and overflow exactly as DateInputTransformation reverts a
        // change: restore the previous formatted value instead of silently deleting characters.
        var invalidCharacter = false;
        foreach (var c in raw)
        {
            // DateInputTransformation accepts digits only; the visible delimiters are supplied by
            // the output mask, so any other character (including a pasted foreign delimiter) is
            // rejected and the previous value restored.
            if (!char.IsDigit(c) && c != format.Delimiter)
            {
                invalidCharacter = true;
                break;
            }
        }

        if (invalidCharacter || digits.Length > format.DigitCount)
        {
            var current = CurrentInputDigits(isEnd);
            var maskedCurrent = format.Format(current);
            SetInputText(box, isEnd, maskedCurrent, maskedCurrent.Length);
            return;
        }

        var masked = format.Format(digits);
        var newCaret = format.ToCaretIndex(masked, Math.Min(digitOffset, digits.Length));
        if (!string.Equals(raw, masked, StringComparison.Ordinal) || box.CaretIndex != newCaret)
            SetInputText(box, isEnd, masked, newCaret);

        ApplyTypedDraft(digits, isEnd);
    }

    private string? SyncedInputText(bool isEnd) => isEnd ? _endSyncedText : _startSyncedText;

    private void RecordSyncedInputText(bool isEnd, string text)
    {
        if (isEnd)
            _endSyncedText = text;
        else
            _startSyncedText = text;
    }

    private void SetInputText(TextBox box, bool isEnd, string text, int caret)
    {
        _syncingInput = true;
        try
        {
            if (!string.Equals(box.Text, text, StringComparison.Ordinal))
                box.Text = text;
            var index = Math.Clamp(caret, 0, text.Length);
            if (box.CaretIndex != index)
                box.CaretIndex = index;
        }
        finally
        {
            _syncingInput = false;
        }
        RecordSyncedInputText(isEnd, text);
    }

    private void ApplyTypedDraft(string digits, bool isEnd)
    {
        var format = InputFormat;
        if (digits.Length < format.DigitCount)
        {
            // DateInputTransformation: an incomplete buffer reports no date. For an end field the
            // start date stays pending; clearing the start clears the whole range.
            SetValidationMessage(null);
            if (isEnd)
                ApplySelection(SelectedDate?.Date, null);
            else
                ApplySelection(null, null);
            return;
        }

        var start = SelectedDate?.Date;
        var end = SelectedEndDate?.Date;
        if (!DateTime.TryParseExact(digits, format.PatternWithoutDelimiters, EffectiveCulture,
                DateTimeStyles.None, out var parsed))
        {
            SetValidationMessage(GetText("MaterialDatePickerInvalidDateText", "Enter a valid date"));
            if (isEnd)
                ApplySelection(start, null);
            else
                ApplySelection(null, null);
            return;
        }

        var date = parsed.Date;
        if (!IsYearAllowed(date.Year))
        {
            SetValidationMessage(FormatRangeError());
            if (isEnd)
                ApplySelection(start, null);
            else
                ApplySelection(null, null);
            return;
        }

        if ((SelectableDates is { } selectable &&
             (!selectable.IsSelectableYear(date.Year) || !selectable.IsSelectableDate(date))) ||
            (MinimumDate is { } min && date < min.Date) ||
            (MaximumDate is { } max && date > max.Date))
        {
            SetValidationMessage(GetText("MaterialDatePickerDateNotAllowedText",
                "This date cannot be selected"));
            if (isEnd)
                ApplySelection(start, null);
            else
                ApplySelection(null, null);
            return;
        }

        if (SelectionMode == MaterialDateSelectionMode.Single)
        {
            SetValidationMessage(null);
            ApplySelection(date, null);
            return;
        }

        if (isEnd)
        {
            if (start is { } pending && date < pending)
            {
                SetValidationMessage(GetText("MaterialDatePickerRangeOrderText",
                    "The end date must be on or after the start date"));
                ApplySelection(pending, null);
                return;
            }
            SetValidationMessage(null);
            ApplySelection(start, date);
            return;
        }

        // DateInput reports an invalid start as a null selection; DateRangePicker's setSelection
        // then clears both endpoints (`(null, existingEnd)` fails the start-with-an-end rule).
        // The native editor keeps the rejected text and its validation message.
        if (end is { } currentEnd && date > currentEnd)
        {
            SetValidationMessage(GetText("MaterialDatePickerRangeOrderText",
                "The end date must be on or after the start date"));
            ApplySelection(null, null);
            return;
        }
        SetValidationMessage(null);
        ApplySelection(date, end);
    }

    private string FormatRangeError() =>
        string.Format(EffectiveCulture,
            GetText("MaterialDatePickerOutOfRangeText",
                "Enter a date between {0} and {1}"),
            MinimumYear.ToString(EffectiveCulture),
            MaximumYear.ToString(EffectiveCulture));

    private void SetValidationMessage(string? message)
    {
        if (message == _validationMessage)
            return;
        var old = _validationMessage;
        _validationMessage = message;
        RaisePropertyChanged(ValidationMessageProperty, old, message);
        PseudoClasses.Set(":invalid", !string.IsNullOrEmpty(message));
        if (_inputError is not null)
        {
            _inputError.Text = message;
            _inputError.IsVisible = !string.IsNullOrEmpty(message);
        }
    }

    private void SetDisplayMonth(DateTime month)
    {
        var target = ClampMonthToBounds(new DateTime(month.Year, month.Month, 1));
        // BaseDatePickerStateImpl.displayedMonthMillis ignores months outside the year range;
        // SelectableDates only disables selection, so browsing must still reach the month.
        if (!IsYearInRange(target.Year))
            return;
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
        date = date.Date;
        var minYear = Math.Min(MinimumYear, MaximumYear);
        var maxYear = Math.Max(MinimumYear, MaximumYear);
        if (date.Year < minYear)
            date = new DateTime(minYear, 1, 1);
        else if (date.Year > maxYear)
            date = new DateTime(maxYear, 12, 31);
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
        foreach (var block in _monthBlocks.Values)
        {
            foreach (var cell in block.Cells)
            {
                if (cell.Tag is DateTime value && value == date && cell.IsEffectivelyVisible)
                    return cell;
            }
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
                if (matches && control.IsEffectivelyEnabled)
                {
                    control.Focus(NavigationMethod.Directional);
                    SyncChoiceTabStop(control);
                    return;
                }
            }
        }

        // The desired month/year may be disabled by the year range or bounds. Fall back to the
        // nearest enabled choice so Home/End and reopening never strand keyboard focus.
        if (FindNearestChoice() is { } fallback)
        {
            fallback.Focus(NavigationMethod.Directional);
            SyncChoiceTabStop(fallback);
        }
    }

    private Control? FindNearestChoice()
    {
        var preferred = View == MaterialDatePickerView.Month ? _focusDate.Month : _focusDate.Year;
        Control? best = null;
        var bestDistance = int.MaxValue;
        foreach (var (choice, value) in _choiceCells)
        {
            if (choice is not Control { IsEffectivelyEnabled: true } control)
                continue;
            var distance = Math.Abs(value - preferred);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = control;
            }
        }
        return best;
    }

    private void OnChoiceGotFocus(object? sender, FocusChangedEventArgs e)
    {
        if (sender is not Control { Tag: int value } control)
            return;
        // Track roving focus so Home/End and Tab agree on the active month/year.
        if (View == MaterialDatePickerView.Month)
            _focusDate = new DateTime(DisplayMonth.Year, value, 1);
        else
            _focusDate = new DateTime(value, _focusDate.Month, 1);
        _focusDateSet = true;
        SyncChoiceTabStop(control);
    }

    /// <summary>
    /// Month and year lists are one tab stop: the active choice is the only tabbable element and
    /// arrow/Home/End keys move that roving stop, like <see cref="MaterialChipGroup"/>.
    /// </summary>
    private void SyncChoiceTabStop(Control? active = null)
    {
        if (_choiceHost is null || _choiceCells.Count == 0)
            return;
        active ??= FindChoice(_focusDate) ?? FindNearestChoice();
        KeyboardNavigation.SetTabNavigation(_choiceHost, KeyboardNavigationMode.Once);
        foreach (var (cell, _) in _choiceCells)
        {
            if (cell is Control control)
                KeyboardNavigation.SetIsTabStop(control, ReferenceEquals(control, active));
        }
        if (active is not null)
        {
            KeyboardNavigation.SetTabOnceActiveElement(_choiceHost, active);
            // The docked year list can hold two centuries; keep the roving stop visible after
            // Home/End, arrow navigation and reopening the view.
            active.BringIntoView();
        }
    }

    private Control? FindChoice(DateTime date)
    {
        var value = View == MaterialDatePickerView.Month ? date.Month : date.Year;
        foreach (var (choice, candidate) in _choiceCells)
        {
            if (candidate == value && choice is Control control && control.IsEffectivelyEnabled)
                return control;
        }
        return null;
    }

    /// <summary>
    /// Moves roving focus to <paramref name="date"/>, changing the displayed month when the date
    /// falls outside it. <paramref name="animateMonth"/> is DatePicker.kt <c>goToMonth</c>: a
    /// horizontal move across the month boundary animates the month page and moves focus only
    /// once the page has settled. Vertical moves, Home/End and the page keys update the month
    /// immediately, matching the port's non-source keyboard extensions.
    /// </summary>
    private void MoveFocus(DateTime date, bool animateMonth = false)
    {
        if (!IsDateAllowed(date))
            return;
        _focusDate = date;
        _focusDateSet = true;
        if (date.Year != DisplayMonth.Year || date.Month != DisplayMonth.Month)
        {
            var target = new DateTime(date.Year, date.Month, 1);
            var steps = MonthsBetween(DisplayMonth, target);
            var paging = animateMonth && AnimateMonthNavigation(steps);
            if (!paging)
                SetDisplayMonth(target);
            if (paging && _pageActive)
            {
                // goToMonth awaits animateScrollToItem and then calls moveFocus.
                _pendingPageFocus = date;
                return;
            }
        }
        PostMoveFocus(date);
    }

    private void PostMoveFocus(DateTime date) =>
        Dispatcher.UIThread.Post(() =>
        {
            if (View != MaterialDatePickerView.Day)
                return;
            var cell = FindDayCell(date);
            cell?.Focus(NavigationMethod.Directional);
        }, DispatcherPriority.Background);

    private bool HandleDayKey(KeyEventArgs e)
    {
        var date = _focusDate;
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var rtl = IsRightToLeft;
        var horizontal = e.Key is Key.Left or Key.Right;
        var delta = e.Key switch
        {
            Key.Left => rtl ? 1 : -1,
            Key.Right => rtl ? -1 : 1,
            Key.Up => -7,
            Key.Down => 7,
            _ => 0,
        };

        if (delta != 0)
        {
            // DatePicker.kt dayOnKeyEvent: while the month list is scrolling, the first/last day
            // guards swallow left/right so a fast repeat cannot desynchronize focus from the page
            // transition that is still in flight.
            if (horizontal && _pageActive)
                return true;
            var next = ClampToBounds(AddDaysClamped(date, delta));
            if (next is { } value)
                MoveFocus(value, animateMonth: horizontal);
            return true;
        }

        switch (e.Key)
        {
            case Key.PageUp:
                MoveFocus(ClampToBounds(AddMonthsClamped(date, shift ? -12 : -1)) ?? date);
                return true;
            case Key.PageDown:
                MoveFocus(ClampToBounds(AddMonthsClamped(date, shift ? 12 : 1)) ?? date);
                return true;
            case Key.Home:
                MoveFocus(StartOfWeek(date));
                return true;
            case Key.End:
                MoveFocus(AddDaysClamped(date, 6 - (((int)date.DayOfWeek - (int)FirstDayOfWeek + 7) % 7)));
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
        var rtl = IsRightToLeft;
        switch (e.Key)
        {
            case Key.Left:
                date = AddMonthsClamped(date, rtl ? 1 : -1);
                break;
            case Key.Right:
                date = AddMonthsClamped(date, rtl ? -1 : 1);
                break;
            case Key.Up:
                date = AddMonthsClamped(date, -3);
                break;
            case Key.Down:
                date = AddMonthsClamped(date, 3);
                break;
            case Key.PageUp:
                date = AddMonthsClamped(date, -12);
                break;
            case Key.PageDown:
                date = AddMonthsClamped(date, 12);
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

        date = new DateTime(Math.Clamp(date.Year, MinimumYear, MaximumYear), date.Month, 1);
        _focusDate = date;
        if (date.Year != DisplayMonth.Year || date.Month != DisplayMonth.Month)
            SetDisplayMonth(new DateTime(date.Year, date.Month, 1));
        Dispatcher.UIThread.Post(FocusActiveCell, DispatcherPriority.Background);
        return true;
    }

    private bool HandleYearKey(KeyEventArgs e)
    {
        var year = _focusDate.Year;
        var rtl = IsRightToLeft;
        switch (e.Key)
        {
            case Key.Left:
                year += rtl ? 1 : -1;
                break;
            case Key.Right:
                year += rtl ? -1 : 1;
                break;
            case Key.Up:
                year -= 3;
                break;
            case Key.Down:
                year += 3;
                break;
            case Key.PageUp:
                year -= 30;
                break;
            case Key.PageDown:
                year += 30;
                break;
            case Key.Home:
                year = MinimumYear;
                break;
            case Key.End:
                year = MaximumYear;
                break;
            default:
                return false;
        }

        year = Math.Clamp(year, Math.Min(MinimumYear, MaximumYear), Math.Max(MinimumYear, MaximumYear));
        _focusDate = new DateTime(year, _focusDate.Month, 1);
        if (year != DisplayMonth.Year)
            SetDisplayMonth(new DateTime(year, DisplayMonth.Month, 1));
        Dispatcher.UIThread.Post(FocusActiveCell, DispatcherPriority.Background);
        return true;
    }

    private static DateTime AddMonthsClamped(DateTime date, long months)
    {
        var index = Math.Clamp((long)(date.Year - 1) * 12 + date.Month - 1 + months, 0, 9999 * 12 - 1);
        var year = (int)(index / 12) + 1;
        var month = (int)(index % 12) + 1;
        return new DateTime(year, month, Math.Min(date.Day, DateTime.DaysInMonth(year, month)));
    }

    private static DateTime AddDaysClamped(DateTime date, int days)
    {
        var day = Math.Clamp(date.Ticks / TimeSpan.TicksPerDay + days, 0,
            DateTime.MaxValue.Ticks / TimeSpan.TicksPerDay);
        return new DateTime(day * TimeSpan.TicksPerDay);
    }

    private DateTime StartOfWeek(DateTime date) =>
        AddDaysClamped(date, -(((int)date.DayOfWeek - (int)FirstDayOfWeek + 7) % 7));

    private string GetText(string key, string fallback) =>
        this.TryFindResource(key, out var value) && value is string text ? text : fallback;

    /// <summary>One recycled full-screen month row: header plus the fixed 6x7 cell grid.</summary>
    private sealed class MonthBlockView(
        StackPanel root, TextBlock header, MaterialDateCell[] cells, MonthRangeBackdrop backdrop)
    {
        public StackPanel Root { get; } = root;

        public TextBlock Header { get; } = header;

        public MaterialDateCell[] Cells { get; } = cells;

        public MonthRangeBackdrop Backdrop { get; } = backdrop;

        public DateTime Month { get; set; }

        public int MonthOffset { get; set; }
    }

    /// <summary>
    /// One composed horizontal month page: the live <c>PART_DayGrid</c> cell list or a recycled
    /// neighbouring page. Only the pages around the viewport are composed, like the source
    /// LazyRow; the translate carries the paging offset.
    /// </summary>
    private sealed class MonthPageLayer(
        UniformGrid grid, IReadOnlyList<MaterialDateCell> cells, TranslateTransform translate)
    {
        public UniformGrid Grid { get; } = grid;

        public IReadOnlyList<MaterialDateCell> Cells { get; } = cells;

        public TranslateTransform Translate { get; } = translate;

        public DateTime Month { get; set; }

        public bool HasMonth { get; set; }
    }

    /// <summary>
    /// Draws the source range container for a month before its day cells. Port of
    /// <c>DateRangePicker.kt</c> <c>drawRangeBackground</c>: a first-row continuation runs from its
    /// start coordinate to the row's right edge, middle rows span the full width, and a final
    /// continuation row runs from the row's left edge to its end coordinate. Half-cell insets are
    /// applied only in the month containing the actual range start/end (SelectedRangeInfo).
    /// </summary>
    private sealed class MonthRangeBackdrop : Control
    {
        public static readonly StyledProperty<IBrush?> FillProperty =
            AvaloniaProperty.Register<MonthRangeBackdrop, IBrush?>(nameof(Fill));

        private static readonly double Cell = DayCellSize;
        private static readonly double RowHeight = DayCellSize;
        private static readonly double StateLayerHeight = 40;
        private static readonly double StateLayerInset = (DayCellSize - StateLayerHeight) / 2;

        private DateTime _first;
        private int _offset;
        private int _days;
        private DateTime _start;
        private DateTime _end;
        private bool _hasRange;
        private bool _rightToLeft;
        private Rect[] _rects = [];

        public IBrush? Fill
        {
            get => GetValue(FillProperty);
            set => SetValue(FillProperty, value);
        }

        protected override Size MeasureOverride(Size availableSize) =>
            new(Cell * 7, RowHeight * DayRowCount);

        public void SetRange(
            DateTime month, int offset, DateTime start, DateTime end, bool rightToLeft)
        {
            _first = new DateTime(month.Year, month.Month, 1);
            _days = DateTime.DaysInMonth(month.Year, month.Month);
            _offset = offset;
            _start = start;
            _end = end;
            _rightToLeft = rightToLeft;
            _hasRange = !(_start > _first.AddDays(_days - 1) || _end < _first);
            _rects = _hasRange ? BuildRects() : [];
            InvalidateVisual();
        }

        public void ClearRange()
        {
            _hasRange = false;
            _rects = [];
            InvalidateVisual();
        }

        private Rect[] BuildRects()
        {
            var monthEnd = _first.AddDays(_days - 1);
            var firstIsSelectionStart = _start >= _first;
            var lastIsSelectionEnd = _end <= monthEnd;
            var startOffset = firstIsSelectionStart ? _offset + _start.Day - 1 : _offset;
            var endOffset = lastIsSelectionEnd ? _offset + _end.Day - 1 : _offset + _days - 1;
            var startX = (startOffset % 7) * Cell + (firstIsSelectionStart ? Cell / 2 : 0);
            var endX = (endOffset % 7) * Cell + (lastIsSelectionEnd ? Cell / 2 : Cell);
            var startRow = startOffset / 7;
            var endRow = endOffset / 7;
            var width = Cell * 7;
            var rects = new List<Rect>(endRow - startRow + 1);

            for (var row = startRow; row <= endRow; row++)
            {
                var logicalStart = row == startRow ? startX : 0;
                var logicalEnd = row == endRow ? endX : width;
                var a = _rightToLeft ? width - logicalStart : logicalStart;
                var b = _rightToLeft ? width - logicalEnd : logicalEnd;
                var left = Math.Min(a, b);
                var right = Math.Max(a, b);
                if (right - left <= 0.01)
                    continue;
                rects.Add(new Rect(left, row * RowHeight + StateLayerInset,
                    right - left, StateLayerHeight));
            }
            return [.. rects];
        }

        public override void Render(DrawingContext context)
        {
            if (!_hasRange || Fill is not { } fill || _rects.Length == 0)
                return;
            foreach (var rect in _rects)
                context.FillRectangle(fill, rect);
        }
    }

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
