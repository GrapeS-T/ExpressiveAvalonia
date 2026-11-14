using System;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using ExpressiveAvalonia.Theming;
using ExpressiveAvalonia.Tokens;
using Avalonia.VisualTree;

namespace ExpressiveAvalonia.Controls;

/// <summary>How a <see cref="MaterialTimePicker"/> formats and interprets its time.</summary>
public enum MaterialTimeFormat
{
    /// <summary>1-12 with an AM/PM period selector.</summary>
    Hour12,

    /// <summary>0-23 without a period selector.</summary>
    Hour24,
}

/// <summary>Which input surface a <see cref="MaterialTimePicker"/> shows.</summary>
public enum MaterialTimeInputMode
{
    /// <summary>The clock dial with hour and minute faces.</summary>
    Dial,

    /// <summary>Direct hour and minute entry.</summary>
    Keyboard,

    /// <summary>Two vertical hour/minute wheels, ported from the source <c>TimeScroll</c>.</summary>
    Scroll,
}

/// <summary>Which source time-picker family the control presents.</summary>
public enum MaterialTimePickerVariant
{
    /// <summary>The original contained Material 3 picker (dial or input).</summary>
    Standard,

    /// <summary>
    /// The vibrant contained family: shaped 100/120 fields, a 56/120 period toggle, and the
    /// vibrant colour roles shared by <c>TimePicker(shapes)</c>, <c>TimeInput(shapes)</c> and
    /// <c>TimeScroll(shapes)</c>.
    /// </summary>
    Vibrant,

    /// <summary>
    /// The uncontained vibrant family: 136-high fields plus a side control column containing the
    /// period toggle and the input/scroll mode toggle.
    /// </summary>
    Uncontained,
}

/// <summary>Carries the committed time of a <see cref="MaterialTimePicker"/>.</summary>
public sealed class MaterialTimeConfirmedEventArgs(TimeSpan time) : EventArgs
{
    /// <summary>The time that was confirmed, rounded down to whole minutes.</summary>
    public TimeSpan Time { get; } = time;
}

/// <summary>
/// The Material time picker: headline, editable hour and minute cells, an AM/PM period selector,
/// a 12- or 24-hour clock dial, and Cancel/OK actions.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Time"/> is a live two-way value; every accepted edit updates it immediately, so the
/// control is usable as a standalone editor as well as inside a dialog. <see cref="Confirmed"/> and
/// <see cref="Cancelled"/> report the actions, and a dialog host that wants Cancel to restore the
/// previous value simply keeps its own copy.
/// </para>
/// <para>
/// Typed entries are validated instead of silently clamped: text outside the cell range raises the
/// segment's invalid state, which clears <see cref="IsValid"/> and disables OK. An optional
/// <see cref="MinTime"/>/<see cref="MaxTime"/> window produces a validation message.
/// </para>
/// <para>
/// The dial is the default surface; the icon button in the action row toggles between the dial and
/// keyboard entry, matching the source's mode toggle. Escape cancels, Enter confirms from a cell.
/// </para>
/// </remarks>
public sealed class MaterialTimePicker : TemplatedControl
{
    public static readonly StyledProperty<TimeSpan> TimeProperty =
        AvaloniaProperty.Register<MaterialTimePicker, TimeSpan>(nameof(Time), TimeSpan.Zero,
            defaultBindingMode: BindingMode.TwoWay, coerce: (_, value) => CoerceTime(value));

    public static readonly StyledProperty<MaterialTimeFormat> FormatProperty =
        AvaloniaProperty.Register<MaterialTimePicker, MaterialTimeFormat>(nameof(Format));

    /// <summary>
    /// Culture for the cells, dial and scroll wheels. Null follows
    /// <see cref="CultureInfo.CurrentCulture"/>, like the source locale <c>NumberFormat</c>.
    /// Assigning a different culture reformats the live surfaces without rebuilding drafts.
    /// </summary>
    public static readonly StyledProperty<CultureInfo?> CultureProperty =
        AvaloniaProperty.Register<MaterialTimePicker, CultureInfo?>(nameof(Culture));

    public static readonly StyledProperty<MaterialTimeInputMode> InputModeProperty =
        AvaloniaProperty.Register<MaterialTimePicker, MaterialTimeInputMode>(nameof(InputMode));

    public static readonly StyledProperty<MaterialTimePickerVariant> VariantProperty =
        AvaloniaProperty.Register<MaterialTimePicker, MaterialTimePickerVariant>(nameof(Variant));

    public static readonly StyledProperty<CornerRadius?> TimeFieldShapeProperty =
        AvaloniaProperty.Register<MaterialTimePicker, CornerRadius?>(nameof(TimeFieldShape));

    public static readonly StyledProperty<CornerRadius?> PeriodSelectorShapeProperty =
        AvaloniaProperty.Register<MaterialTimePicker, CornerRadius?>(nameof(PeriodSelectorShape));

    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<MaterialTimePicker, Orientation>(nameof(Orientation), Orientation.Vertical);

    public static readonly StyledProperty<string?> HeadlineProperty =
        AvaloniaProperty.Register<MaterialTimePicker, string?>(nameof(Headline));

    public static readonly StyledProperty<int> MinuteIncrementProperty =
        AvaloniaProperty.Register<MaterialTimePicker, int>(nameof(MinuteIncrement), 1,
            validate: value => value is > 0 and <= 60);

    public static readonly StyledProperty<bool> AutoAdvanceProperty =
        AvaloniaProperty.Register<MaterialTimePicker, bool>(nameof(AutoAdvance), true);

    public static readonly StyledProperty<TimeSpan?> MinTimeProperty =
        AvaloniaProperty.Register<MaterialTimePicker, TimeSpan?>(nameof(MinTime));

    public static readonly StyledProperty<TimeSpan?> MaxTimeProperty =
        AvaloniaProperty.Register<MaterialTimePicker, TimeSpan?>(nameof(MaxTime));

    public static readonly StyledProperty<bool> ShowActionsProperty =
        AvaloniaProperty.Register<MaterialTimePicker, bool>(nameof(ShowActions), true);

    public static readonly StyledProperty<string?> EffectiveHeadlineProperty =
        AvaloniaProperty.Register<MaterialTimePicker, string?>(nameof(EffectiveHeadline));

    public static readonly StyledProperty<bool> IsValidProperty =
        AvaloniaProperty.Register<MaterialTimePicker, bool>(nameof(IsValid), true);

    public static readonly StyledProperty<string?> ValidationMessageProperty =
        AvaloniaProperty.Register<MaterialTimePicker, string?>(nameof(ValidationMessage));

    private MaterialTimeInputSegment? _hourSegment;
    private MaterialTimeInputSegment? _minuteSegment;
    private MaterialPeriodSelector? _period;
    private MaterialPeriodSelector? _sidePeriod;
    private MaterialClockDial? _dial;
    private MaterialScrollField? _hourScroll;
    private MaterialScrollField? _minuteScroll;
    private MaterialScrollFieldState? _hourScrollState;
    private MaterialScrollFieldState? _minuteScrollState;
    private StackPanel? _timeInputs;
    private StackPanel? _scrollInputs;
    private StackPanel? _sideControls;
    private Button? _ok;
    private Button? _cancel;
    private Button? _modeToggle;
    private Button? _sideModeToggle;
    private Grid? _actions;
    private MaterialTimeInputMode? _iconMode;
    private MaterialTimeInputMode? _iconReturnMode;
    private MaterialClockDialSelection _active = MaterialClockDialSelection.Hour;
    private int _hour24;
    private int _minute;
    private bool _isPm;
    private bool _syncing;
    private bool _syncingScroll;
    private MaterialTimeInputMode _keyboardReturnMode = MaterialTimeInputMode.Dial;
    private MaterialTimeInputMode? _shownMode;
    private MaterialMotion.SpringAnimation? _modeAnimation;

    /// <summary>The selected time, truncated to whole minutes and clamped to one day.</summary>
    public TimeSpan Time
    {
        get => GetValue(TimeProperty);
        set => SetValue(TimeProperty, value);
    }

    /// <summary>12-hour (with AM/PM) or 24-hour presentation.</summary>
    public MaterialTimeFormat Format
    {
        get => GetValue(FormatProperty);
        set => SetValue(FormatProperty, value);
    }

    /// <summary>Culture for the cells, dial and scroll wheels; null follows the current culture.</summary>
    public CultureInfo? Culture
    {
        get => GetValue(CultureProperty);
        set => SetValue(CultureProperty, value);
    }

    private CultureInfo EffectiveCulture => Culture ?? CultureInfo.CurrentCulture;

    /// <summary>Dial or direct keyboard entry.</summary>
    public MaterialTimeInputMode InputMode
    {
        get => GetValue(InputModeProperty);
        set => SetValue(InputModeProperty, value);
    }

    /// <summary>Standard, contained vibrant, or uncontained vibrant presentation.</summary>
    public MaterialTimePickerVariant Variant
    {
        get => GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    /// <summary>
    /// Corner radius of the hour and minute selectors. Null uses the source default for the
    /// variant: 8 for the standard picker and 16 for the vibrant families.
    /// </summary>
    public CornerRadius? TimeFieldShape
    {
        get => GetValue(TimeFieldShapeProperty);
        set => SetValue(TimeFieldShapeProperty, value);
    }

    /// <summary>
    /// Corner radius of the AM/PM selector. Null uses the source default for the variant: 8 for
    /// the standard picker and a fully rounded toggle for the vibrant families.
    /// </summary>
    public CornerRadius? PeriodSelectorShape
    {
        get => GetValue(PeriodSelectorShapeProperty);
        set => SetValue(PeriodSelectorShapeProperty, value);
    }

    /// <summary>
    /// Vertical puts the dial below the inputs; horizontal puts it beside them and moves the
    /// period selector under the inputs.
    /// </summary>
    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>The headline. Null uses the mode's localized default ("Select time" / "Enter time").</summary>
    public string? Headline
    {
        get => GetValue(HeadlineProperty);
        set => SetValue(HeadlineProperty, value);
    }

    /// <summary>The minute grid the dial snaps to and the step of the minute cell.</summary>
    public int MinuteIncrement
    {
        get => GetValue(MinuteIncrementProperty);
        set => SetValue(MinuteIncrementProperty, value);
    }

    /// <summary>
    /// Advances the active segment from hour to minute after a pointer selection on the dial or a
    /// completed keyboard hour entry, mirroring the source <c>autoSwitchToMinute</c> behavior.
    /// Avalonia has no cross-platform screen-reader-enabled signal, so this public flag is the
    /// explicit equivalent of the source's accessibility suppression; explicit Enter always
    /// advances, exactly like the source's IME Next action.
    /// </summary>
    public bool AutoAdvance
    {
        get => GetValue(AutoAdvanceProperty);
        set => SetValue(AutoAdvanceProperty, value);
    }

    /// <summary>Earliest acceptable time; out-of-range times disable OK and show a message.</summary>
    public TimeSpan? MinTime
    {
        get => GetValue(MinTimeProperty);
        set => SetValue(MinTimeProperty, value);
    }

    /// <summary>Latest acceptable time; out-of-range times disable OK and show a message.</summary>
    public TimeSpan? MaxTime
    {
        get => GetValue(MaxTimeProperty);
        set => SetValue(MaxTimeProperty, value);
    }

    /// <summary>Whether the Cancel/OK action row is shown.</summary>
    public bool ShowActions
    {
        get => GetValue(ShowActionsProperty);
        set => SetValue(ShowActionsProperty, value);
    }

    /// <summary>The headline actually rendered, after the mode default is applied.</summary>
    public string? EffectiveHeadline
    {
        get => GetValue(EffectiveHeadlineProperty);
        private set => SetValue(EffectiveHeadlineProperty, value);
    }

    /// <summary>False while a cell holds unparsable or out-of-range text, or the time is out of range.</summary>
    public bool IsValid
    {
        get => GetValue(IsValidProperty);
        private set => SetValue(IsValidProperty, value);
    }

    /// <summary>The localized range message shown while <see cref="IsValid"/> is false.</summary>
    public string? ValidationMessage
    {
        get => GetValue(ValidationMessageProperty);
        private set => SetValue(ValidationMessageProperty, value);
    }

    /// <summary>Raised by the OK action when the current time is valid.</summary>
    public event EventHandler<MaterialTimeConfirmedEventArgs>? Confirmed;

    /// <summary>Raised by the Cancel action, Escape, or the dialog's light dismiss.</summary>
    public event EventHandler? Cancelled;

    /// <summary>Commits the current time; does nothing while <see cref="IsValid"/> is false.</summary>
    public void Confirm()
    {
        if (!IsValid) return;
        Confirmed?.Invoke(this, new MaterialTimeConfirmedEventArgs(Time));
    }

    /// <summary>Requests cancellation.</summary>
    public void Cancel() => Cancelled?.Invoke(this, EventArgs.Empty);

    /// <summary>Moves keyboard focus into the hour cell, for dialog opening.</summary>
    public void FocusFirstField()
    {
        if (InputMode == MaterialTimeInputMode.Scroll && _hourScroll is not null)
            _hourScroll.Focus(NavigationMethod.Tab);
        else
            _hourSegment?.FocusEditor();
    }

    /// <summary>
    /// Discards invalid drafts by rebuilding the cells from <see cref="Time"/>. Used by the modal
    /// host when a cancelled session restores its snapshot.
    /// </summary>
    public void ResetDrafts() => SyncFromTime();

    /// <summary>
    /// Switches to direct entry. From direct entry it returns to the surface the user came from,
    /// matching the source's separate clock/keyboard and scroll/keyboard mode toggles.
    /// </summary>
    public void ToggleInputMode()
    {
        if (InputMode == MaterialTimeInputMode.Keyboard)
        {
            InputMode = _keyboardReturnMode;
            return;
        }

        _keyboardReturnMode = InputMode;
        InputMode = MaterialTimeInputMode.Keyboard;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        DetachParts();
        base.OnApplyTemplate(e);

        _hourSegment = e.NameScope.Find<MaterialTimeInputSegment>("PART_Hour");
        _minuteSegment = e.NameScope.Find<MaterialTimeInputSegment>("PART_Minute");
        _period = e.NameScope.Find<MaterialPeriodSelector>("PART_Period");
        _sidePeriod = e.NameScope.Find<MaterialPeriodSelector>("PART_SidePeriod");
        if (_sidePeriod is not null && !_sidePeriod.Classes.Contains("side"))
            _sidePeriod.Classes.Add("side");
        _dial = e.NameScope.Find<MaterialClockDial>("PART_Dial");
        _hourScroll = e.NameScope.Find<MaterialScrollField>("PART_HourScroll");
        _minuteScroll = e.NameScope.Find<MaterialScrollField>("PART_MinuteScroll");
        _timeInputs = e.NameScope.Find<StackPanel>("TimeInputs");
        _scrollInputs = e.NameScope.Find<StackPanel>("ScrollInputs");
        _sideControls = e.NameScope.Find<StackPanel>("SideControls");
        _ok = e.NameScope.Find<Button>("PART_Ok");
        _cancel = e.NameScope.Find<Button>("PART_Cancel");
        _modeToggle = e.NameScope.Find<Button>("PART_ModeToggle");
        _sideModeToggle = e.NameScope.Find<Button>("PART_SideModeToggle");
        _actions = e.NameScope.Find<Grid>("Actions");
        if (_actions is not null)
            _actions.IsVisible = ShowActions;

        if (_hourSegment is not null)
        {
            _hourSegment.SelectionRequested += HourSelectionRequested;
            _hourSegment.ValueChanged += HourValueChanged;
            _hourSegment.EntryCompleted += HourEntryCompleted;
            _hourSegment.ValidityChanged += SegmentValidityChanged;
            _hourSegment.CancelRequested += EntryCancelRequested;
        }

        if (_minuteSegment is not null)
        {
            _minuteSegment.SelectionRequested += MinuteSelectionRequested;
            _minuteSegment.ValueChanged += MinuteValueChanged;
            _minuteSegment.EntryCompleted += MinuteEntryCompleted;
            _minuteSegment.ValidityChanged += SegmentValidityChanged;
            _minuteSegment.CancelRequested += EntryCancelRequested;
        }

        if (_period is not null)
            _period.IsPmChanged += PeriodChanged;
        if (_sidePeriod is not null)
            _sidePeriod.IsPmChanged += PeriodChanged;
        if (_dial is not null)
        {
            _dial.ValueChanged += DialValueChanged;
            _dial.PointerSelectionCompleted += DialPointerSelectionCompleted;
        }
        if (_ok is not null)
            _ok.Click += OkClick;
        if (_cancel is not null)
            _cancel.Click += CancelClick;
        if (_modeToggle is not null)
            _modeToggle.Click += ModeToggleClick;
        if (_sideModeToggle is not null)
            _sideModeToggle.Click += ModeToggleClick;

        _active = MaterialClockDialSelection.Hour;
        _iconMode = null;
        _iconReturnMode = null;
        _shownMode = InputMode;
        UpdateCultureSurfaces();
        UpdateLayoutClasses();
        UpdateShapes();
        RebuildScrollStates();
        SyncFromTime();
    }

    private void UpdateLayoutClasses()
    {
        PseudoClasses.Set(":horizontal", Orientation == Avalonia.Layout.Orientation.Horizontal);
        PseudoClasses.Set(":vertical", Orientation == Avalonia.Layout.Orientation.Vertical);
        PseudoClasses.Set(":keyboard", InputMode == MaterialTimeInputMode.Keyboard);
        PseudoClasses.Set(":dial", InputMode == MaterialTimeInputMode.Dial);
        PseudoClasses.Set(":scroll", InputMode == MaterialTimeInputMode.Scroll);
        PseudoClasses.Set(":hour12", Format == MaterialTimeFormat.Hour12);
        PseudoClasses.Set(":hour24", Format == MaterialTimeFormat.Hour24);
        PseudoClasses.Set(":standard", Variant == MaterialTimePickerVariant.Standard);
        PseudoClasses.Set(":vibrant", Variant == MaterialTimePickerVariant.Vibrant);
        PseudoClasses.Set(":uncontained", Variant == MaterialTimePickerVariant.Uncontained);
    }

    private void DetachParts()
    {
        if (_hourSegment is not null)
        {
            _hourSegment.SelectionRequested -= HourSelectionRequested;
            _hourSegment.ValueChanged -= HourValueChanged;
            _hourSegment.EntryCompleted -= HourEntryCompleted;
            _hourSegment.ValidityChanged -= SegmentValidityChanged;
            _hourSegment.CancelRequested -= EntryCancelRequested;
        }

        if (_minuteSegment is not null)
        {
            _minuteSegment.SelectionRequested -= MinuteSelectionRequested;
            _minuteSegment.ValueChanged -= MinuteValueChanged;
            _minuteSegment.EntryCompleted -= MinuteEntryCompleted;
            _minuteSegment.ValidityChanged -= SegmentValidityChanged;
            _minuteSegment.CancelRequested -= EntryCancelRequested;
        }

        if (_period is not null)
            _period.IsPmChanged -= PeriodChanged;
        if (_sidePeriod is not null)
            _sidePeriod.IsPmChanged -= PeriodChanged;
        if (_dial is not null)
        {
            _dial.ValueChanged -= DialValueChanged;
            _dial.PointerSelectionCompleted -= DialPointerSelectionCompleted;
        }
        if (_ok is not null)
            _ok.Click -= OkClick;
        if (_cancel is not null)
            _cancel.Click -= CancelClick;
        if (_modeToggle is not null)
            _modeToggle.Click -= ModeToggleClick;
        if (_sideModeToggle is not null)
            _sideModeToggle.Click -= ModeToggleClick;
        DetachScrollStateHandlers();
        _modeAnimation?.Dispose();
        _modeAnimation = null;
        _shownMode = null;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TimeProperty)
        {
            if (!_syncing) SyncFromTime();
        }
        else if (change.Property == FormatProperty)
        {
            UpdateLayoutClasses();
            // The hour cell range and the wheel item count both change with the format, so this
            // is an explicit draft-replacing update like an external Time assignment.
            RebuildScrollStates();
            SyncFromTime();
        }
        else if (change.Property == InputModeProperty)
        {
            var previousMode = _shownMode;
            if (InputMode != MaterialTimeInputMode.Keyboard)
                _keyboardReturnMode = InputMode;
            UpdateLayoutClasses();
            SyncLayout();
            if (previousMode is { } previous && previous != InputMode)
                AnimateModeChange(InputMode);
            _shownMode = InputMode;
        }
        else if (change.Property == OrientationProperty || change.Property == VariantProperty)
        {
            UpdateLayoutClasses();
            if (change.Property == VariantProperty)
                UpdateShapes();
            SyncLayout();
        }
        else if (change.Property == TimeFieldShapeProperty ||
                 change.Property == PeriodSelectorShapeProperty)
        {
            UpdateShapes();
        }
        else if (change.Property == HeadlineProperty)
        {
            UpdateHeadline();
        }
        else if (change.Property == CultureProperty)
        {
            // Live culture change: existing drafts stay as the user typed them, while every
            // displayed number and cached wheel label reformats.
            UpdateCultureSurfaces();
            UpdateSegmentConfiguration();
            UpdateScrollConfiguration();
            UpdateValidation();
        }
        else if (change.Property == MinuteIncrementProperty)
        {
            if (_minuteSegment is not null) _minuteSegment.Step = MinuteIncrement;
            UpdateDial();
        }
        else if (change.Property == MinTimeProperty || change.Property == MaxTimeProperty)
        {
            UpdateValidation();
        }
        else if (change.Property == ShowActionsProperty)
        {
            if (_actions is not null) _actions.IsVisible = ShowActions;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || !IsEffectivelyEnabled) return;
        switch (e.Key)
        {
            case Key.Escape:
                Cancel();
                e.Handled = true;
                break;
            case Key.Enter:
                Confirm();
                e.Handled = true;
                break;
        }
    }

    private static TimeSpan CoerceTime(TimeSpan value)
    {
        var total = value.TotalMinutes;
        if (!double.IsFinite(total)) return TimeSpan.Zero;
        return TimeSpan.FromMinutes(Math.Clamp(Math.Floor(total), 0, 1439));
    }

    private static int DisplayHour(int hour24) => hour24 % 12 == 0 ? 12 : hour24 % 12;

    /// <summary>
    /// Replaces both drafts from the committed <see cref="Time"/>. This is the deliberate
    /// external-value path (binding, wheel scroll, cancel restore), not a focus or layout change.
    /// </summary>
    private void SyncFromTime()
    {
        _syncing = true;
        _hour24 = Time.Hours;
        _minute = Time.Minutes;
        _isPm = _hour24 >= 12;

        UpdateHeadline();
        UpdateSegmentConfiguration();
        UpdateSegmentValues();
        UpdatePeriod();
        UpdateActiveSegment();
        UpdateDial();
        UpdateSurfaceVisibility();
        SyncScrollFromTime();
        _syncing = false;

        UpdateValidation();
    }

    /// <summary>
    /// Reapplies mode/layout-dependent presentation without touching either draft. Invalid typed
    /// text therefore survives orientation, input-mode and variant changes, as the source's
    /// independent hourInput/minuteInput state does.
    /// </summary>
    private void SyncLayout()
    {
        _syncing = true;
        UpdateHeadline();
        UpdateSegmentConfiguration();
        UpdatePeriod();
        UpdateActiveSegment();
        UpdateDial();
        UpdateSurfaceVisibility();
        UpdateScrollConfiguration();
        if (InputMode == MaterialTimeInputMode.Scroll)
        {
            // Scroll mode has no text editor in which to repair an invalid draft; entering it
            // resolves the cells from the committed time before driving the wheels.
            _hourSegment?.NormalizeText();
            _minuteSegment?.NormalizeText();
            SyncScrollFromTime();
        }
        _syncing = false;
        UpdateValidation();
    }

    private void UpdateHeadline()
    {
        var enter = InputMode == MaterialTimeInputMode.Keyboard;
        EffectiveHeadline = Headline ?? (enter
            ? FindText("MaterialTimePickerEnterTimeText") ?? "Enter time"
            : FindText("MaterialTimePickerSelectTimeText") ?? "Select time");

        if (string.IsNullOrEmpty(AutomationProperties.GetName(this)))
            AutomationProperties.SetName(this, EffectiveHeadline);

        UpdateModeTogglePresentation();
    }

    private void UpdateModeTogglePresentation()
    {
        if (_modeToggle is null && _sideModeToggle is null) return;

        var target = InputMode == MaterialTimeInputMode.Keyboard
            ? _keyboardReturnMode
            : MaterialTimeInputMode.Keyboard;
        var next = target switch
        {
            MaterialTimeInputMode.Dial =>
                FindText("MaterialTimePickerSwitchToDialText") ?? "Switch to clock mode",
            MaterialTimeInputMode.Scroll =>
                FindText("MaterialTimePickerSwitchToScrollText") ?? "Switch to scroll mode",
            _ => FindText("MaterialTimePickerSwitchToKeyboardText")
                 ?? "Switch to text input mode",
        };
        SetModeToggle(_modeToggle);
        SetModeToggle(_sideModeToggle);

        // The mode button carries the destination surface's glyph: keyboard for a text target,
        // clock for the dial and a vertical swipe for the scroll wheel. Rebuilding the icon on
        // every external time change would allocate for nothing, so it is only replaced when the
        // resolved target changes.
        if (_iconMode == InputMode && _iconReturnMode == _keyboardReturnMode) return;
        _iconMode = InputMode;
        _iconReturnMode = _keyboardReturnMode;

        var geometryKey = target switch
        {
            MaterialTimeInputMode.Dial => "MaterialTimePickerScheduleIconGeometry",
            MaterialTimeInputMode.Scroll => "MaterialTimePickerSwipeVerticalIconGeometry",
            _ => "MaterialTimePickerKeyboardIconGeometry",
        };
        if (!this.TryFindResource(geometryKey, out var resource) || resource is not Geometry geometry)
            return;
        if (_modeToggle is not null) _modeToggle.Content = Icon(geometry);
        if (_sideModeToggle is not null) _sideModeToggle.Content = Icon(geometry);

        void SetModeToggle(Button? button)
        {
            if (button is null) return;
            AutomationProperties.SetName(button, next);
            ToolTip.SetTip(button, next);
        }

        static PathIcon Icon(Geometry data) => new() { Data = data, Width = 24, Height = 24 };
    }

    private void UpdateSegmentConfiguration()
    {
        var vibrant = Variant != MaterialTimePickerVariant.Standard;
        var variant = Variant == MaterialTimePickerVariant.Uncontained
            ? MaterialTimeSegmentVariant.Uncontained
            : vibrant
                ? MaterialTimeSegmentVariant.Vibrant
                : InputMode == MaterialTimeInputMode.Keyboard
                    ? MaterialTimeSegmentVariant.Keyboard
                    : MaterialTimeSegmentVariant.Dial;

        if (_hourSegment is { } hour)
        {
            var is12Hour = Format == MaterialTimeFormat.Hour12;
            hour.Minimum = is12Hour ? 1 : 0;
            hour.Maximum = is12Hour ? 12 : 23;
            hour.Step = 1;
            hour.Variant = variant;
            hour.EmptyValue = is12Hour ? 12 : 0;
            hour.AutoAdvanceSingleDigit = is12Hour;
            hour.Label = FindText("MaterialTimePickerHourText") ?? "Hour";
            hour.HelpText = FormatRange(FindText("MaterialTimePickerHourHelpText")
                    ?? "Enter a value between {0} and {1}",
                MaterialTimeNumerals.Format(hour.Minimum, 1, EffectiveCulture),
                MaterialTimeNumerals.Format(hour.Maximum, 1, EffectiveCulture));
        }

        if (_minuteSegment is { } minute)
        {
            minute.Minimum = 0;
            minute.Maximum = 59;
            minute.Step = MinuteIncrement;
            minute.Variant = variant;
            minute.EmptyValue = 0;
            minute.AutoAdvanceSingleDigit = false;
            minute.Label = FindText("MaterialTimePickerMinuteText") ?? "Minute";
            minute.HelpText = FormatRange(FindText("MaterialTimePickerMinuteHelpText")
                    ?? "Enter a value between {0} and {1}",
                MaterialTimeNumerals.Format(0, 1, EffectiveCulture),
                MaterialTimeNumerals.Format(59, 1, EffectiveCulture));
        }
    }

    private void UpdateSegmentValues()
    {
        if (_hourSegment is { } hour)
        {
            hour.Value = Format == MaterialTimeFormat.Hour12 ? DisplayHour(_hour24) : _hour24;
            hour.NormalizeText();
        }

        if (_minuteSegment is { } minute)
        {
            minute.Value = _minute;
            minute.NormalizeText();
        }
    }

    private void UpdatePeriod()
    {
        var visible = Format == MaterialTimeFormat.Hour12;
        var horizontal = InputMode == MaterialTimeInputMode.Dial &&
                         Orientation == Avalonia.Layout.Orientation.Horizontal;

        if (_period is not null)
        {
            _period.IsPm = _isPm;
            _period.IsCompact = Variant == MaterialTimePickerVariant.Standard &&
                                InputMode == MaterialTimeInputMode.Keyboard;
            _period.IsVibrant = Variant != MaterialTimePickerVariant.Standard;
            _period.IsVisible = visible && Variant != MaterialTimePickerVariant.Uncontained;
            _period.Orientation = horizontal
                ? Avalonia.Layout.Orientation.Horizontal
                : Avalonia.Layout.Orientation.Vertical;
            AutomationProperties.SetName(_period,
                FindText("MaterialTimePickerPeriodToggleText") ?? "Select AM or PM");
        }

        if (_sidePeriod is not null)
        {
            _sidePeriod.IsPm = _isPm;
            _sidePeriod.IsCompact = false;
            _sidePeriod.IsVibrant = true;
            _sidePeriod.IsVisible = visible && Variant == MaterialTimePickerVariant.Uncontained;
            _sidePeriod.Orientation = Avalonia.Layout.Orientation.Vertical;
            AutomationProperties.SetName(_sidePeriod,
                FindText("MaterialTimePickerPeriodToggleText") ?? "Select AM or PM");
        }

        if (_sideModeToggle is not null)
        {
            _sideModeToggle.IsVisible = Variant == MaterialTimePickerVariant.Uncontained;
            // The source aligns a toggle-only side column to the bottom of the 120-high number
            // frame (24-high visual, top at 96; the 48-high target starts at 84).
            _sideModeToggle.Margin = !visible
                ? new Thickness(0, 84, 0, 0)
                : default;
        }

        if (_modeToggle is not null)
            _modeToggle.IsVisible = Variant != MaterialTimePickerVariant.Uncontained;
    }

    private void UpdateSurfaceVisibility()
    {
        if (_timeInputs is not null)
            _timeInputs.IsVisible = InputMode != MaterialTimeInputMode.Scroll;
        if (_scrollInputs is not null)
            _scrollInputs.IsVisible = InputMode == MaterialTimeInputMode.Scroll;
        if (_sideControls is not null)
            _sideControls.IsVisible = Variant == MaterialTimePickerVariant.Uncontained;
    }

    private void UpdateShapes()
    {
        var field = TimeFieldShape ??
                    new CornerRadius(Variant == MaterialTimePickerVariant.Standard ? 8 : 16);
        var period = PeriodSelectorShape ??
                     new CornerRadius(Variant == MaterialTimePickerVariant.Standard ? 8 : 999);

        if (_hourSegment is not null) _hourSegment.CornerRadius = field;
        if (_minuteSegment is not null) _minuteSegment.CornerRadius = field;
        var wheelShape = TimeFieldShape ?? new CornerRadius(16);
        if (_hourScroll is not null) _hourScroll.CornerRadius = wheelShape;
        if (_minuteScroll is not null) _minuteScroll.CornerRadius = wheelShape;
        if (_period is not null) _period.CornerRadius = period;
        if (_sidePeriod is not null) _sidePeriod.CornerRadius = period;
    }

    private void UpdateCultureSurfaces()
    {
        if (_hourSegment is not null) _hourSegment.Culture = Culture;
        if (_minuteSegment is not null) _minuteSegment.Culture = Culture;
        if (_dial is not null) _dial.Culture = Culture;
    }

    private int HourWheelIndex(int hour24) => Format == MaterialTimeFormat.Hour12
        ? hour24 % 12 == 0 ? 11 : hour24 % 12 - 1
        : hour24;

    private int HourFromWheel(int index) => Format == MaterialTimeFormat.Hour12
        ? (index + 1) % 12 + (_isPm ? 12 : 0)
        : index;

    private void RebuildScrollStates()
    {
        DetachScrollStateHandlers();

        _hourScrollState = new MaterialScrollFieldState(
            Format == MaterialTimeFormat.Hour12 ? 12 : 24, HourWheelIndex(Time.Hours));
        _minuteScrollState = new MaterialScrollFieldState(60, Time.Minutes);
        _hourScrollState.PropertyChanged += HourScrollStateChanged;
        _minuteScrollState.PropertyChanged += MinuteScrollStateChanged;

        _syncingScroll = true;
        if (_hourScroll is not null) _hourScroll.State = _hourScrollState;
        if (_minuteScroll is not null) _minuteScroll.State = _minuteScrollState;
        _syncingScroll = false;

        UpdateScrollConfiguration();
    }

    private void DetachScrollStateHandlers()
    {
        if (_hourScrollState is not null)
            _hourScrollState.PropertyChanged -= HourScrollStateChanged;
        if (_minuteScrollState is not null)
            _minuteScrollState.PropertyChanged -= MinuteScrollStateChanged;
    }

    private void HourScrollStateChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_syncing || _syncingScroll || e.Property != MaterialScrollFieldState.SelectedOptionProperty ||
            _hourScrollState is null)
            return;

        _hour24 = HourFromWheel(_hourScrollState.SelectedOption);
        UpdateSegmentValues();
        CommitTime();
    }

    private void MinuteScrollStateChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_syncing || _syncingScroll || e.Property != MaterialScrollFieldState.SelectedOptionProperty ||
            _minuteScrollState is null)
            return;

        _minute = _minuteScrollState.SelectedOption;
        UpdateSegmentValues();
        CommitTime();
    }

    /// <summary>
    /// Mirrors the source's two <c>LaunchedEffect(state.hour/minute)</c> blocks: the hour wheel is
    /// retargeted immediately, while an active minute scroll is allowed to finish first.
    /// </summary>
    private void SyncScrollFromTime()
    {
        if (_hourScrollState is null && _minuteScrollState is null)
            return;

        _syncingScroll = true;
        if (_hourScrollState is { } hourState)
            hourState.ScrollToOption(HourWheelIndex(_hour24));
        if (_minuteScrollState is { } minuteState &&
            minuteState.SelectedOption != _minute && !minuteState.IsScrollInProgress)
            minuteState.ScrollToOption(_minute);
        _syncingScroll = false;
    }

    private void UpdateScrollConfiguration()
    {
        var hourSuffixKey = Format == MaterialTimeFormat.Hour12
            ? "MaterialTimePickerHourSuffixText"
            : "MaterialTimePickerHour24SuffixText";
        var hourSuffix = FindText(hourSuffixKey) ??
                         (Format == MaterialTimeFormat.Hour12 ? "{0} o'clock" : "{0} hours");
        var minuteSuffix = FindText("MaterialTimePickerMinuteSuffixText") ?? "{0} minutes";
        var hourDescription = FindText("MaterialTimePickerHourSelectionText") ?? "Select hour";
        var minuteDescription = FindText("MaterialTimePickerMinuteSelectionText") ?? "Select minutes";
        var itemHeight = Variant == MaterialTimePickerVariant.Uncontained ? 60d : 52d;
        var fieldHeight = Variant == MaterialTimePickerVariant.Uncontained ? 136d : 120d;
        var fieldShape = TimeFieldShape ?? new CornerRadius(16);
        var culture = EffectiveCulture;

        if (_hourScroll is not null)
        {
            _hourScroll.ItemHeight = itemHeight;
            _hourScroll.Height = fieldHeight;
            _hourScroll.MinimumDigits = 2;
            _hourScroll.CornerRadius = fieldShape;
            _hourScroll.ContentDescription = hourDescription;
            _hourScroll.Formatter = index => MaterialTimeNumerals.Format(
                Format == MaterialTimeFormat.Hour12 ? index + 1 : index, 2, culture);
            _hourScroll.FieldAccessibilityDescription = index => string.Format(
                culture, hourSuffix,
                MaterialTimeNumerals.Format(
                    Format == MaterialTimeFormat.Hour12 ? index + 1 : index, 1, culture));
        }

        if (_minuteScroll is not null)
        {
            _minuteScroll.ItemHeight = itemHeight;
            _minuteScroll.Height = fieldHeight;
            _minuteScroll.MinimumDigits = 2;
            _minuteScroll.CornerRadius = fieldShape;
            _minuteScroll.ContentDescription = minuteDescription;
            _minuteScroll.Formatter = index =>
                MaterialTimeNumerals.Format(index, 2, culture);
            _minuteScroll.FieldAccessibilityDescription = index =>
                string.Format(culture, minuteSuffix,
                    MaterialTimeNumerals.Format(index, 1, culture));
        }
    }

    private void UpdateActiveSegment()
    {
        if (_hourSegment is not null)
            _hourSegment.IsSelected = _active == MaterialClockDialSelection.Hour;
        if (_minuteSegment is not null)
            _minuteSegment.IsSelected = _active == MaterialClockDialSelection.Minute;
    }

    private void AnimateModeChange(MaterialTimeInputMode mode)
    {
        Control? target = mode switch
        {
            MaterialTimeInputMode.Dial => _dial,
            MaterialTimeInputMode.Scroll => _scrollInputs,
            _ => _hourSegment is not null ? _hourSegment : _period,
        };
        if (target is null)
            return;
        _modeAnimation?.Dispose();
        target.Opacity = 0;
        _modeAnimation = MaterialMotion.Animate(target, 0, 1, value => target.Opacity = value,
            MaterialSpring.EffectsFast);
    }

    private void UpdateDial()
    {
        if (_dial is null) return;
        // Keyboard entry has no clock face in the source.
        _dial.IsVisible = InputMode == MaterialTimeInputMode.Dial;
        _dial.Selection = _active;
        _dial.Is24Hour = Format == MaterialTimeFormat.Hour24;
        _dial.MinuteIncrement = MinuteIncrement;
        _dial.Value = _active == MaterialClockDialSelection.Hour
            ? Format == MaterialTimeFormat.Hour12 ? DisplayHour(_hour24) : _hour24
            : _minute;
    }

    private void UpdateValidation()
    {
        var hourInvalid = _hourSegment?.IsInvalid ?? false;
        var minuteInvalid = _minuteSegment?.IsInvalid ?? false;
        var invalidSegment = hourInvalid || minuteInvalid;
        string? message = null;

        if (hourInvalid)
        {
            message = Format == MaterialTimeFormat.Hour24
                ? FindText("MaterialTimePickerHourError24Text") ?? "Hour must be 0-23"
                : FindText("MaterialTimePickerHourErrorText") ?? "Hour must be 1-12";
        }
        else if (minuteInvalid)
        {
            message = FindText("MaterialTimePickerMinuteErrorText") ?? "Minute must be 0-59";
        }

        var lower = MinTime;
        var upper = MaxTime;
        // An inverted window is a configuration mistake, not a value error; it is ignored.
        if (lower is { } low && upper is { } high && low > high)
        {
            lower = null;
            upper = null;
        }

        if (!invalidSegment)
        {
            var belowLower = lower is { } min && Time < min;
            var aboveUpper = upper is { } max && Time > max;
            if (belowLower || aboveUpper)
            {
                message = FormatRange(FindText("MaterialTimePickerOutOfRangeText"),
                    FormatClock(lower ?? TimeSpan.Zero),
                    FormatClock(upper ?? TimeSpan.FromHours(24) - TimeSpan.FromMinutes(1)));
            }
        }

        ValidationMessage = message;
        IsValid = !invalidSegment && message is null;
        if (_ok is not null)
            _ok.IsEnabled = IsValid;
    }

    private string FormatClock(TimeSpan time) =>
        $"{MaterialTimeNumerals.Format((int)time.TotalHours, 2, EffectiveCulture)}:" +
        $"{MaterialTimeNumerals.Format(time.Minutes, 2, EffectiveCulture)}";

    // A missing localization key must not silently turn an out-of-range time into a valid one,
    // so the numeric fallback still carries both bounds.
    private string FormatRange(string? pattern, object first, object second) =>
        string.Format(EffectiveCulture, pattern ?? "{0} - {1}", first, second);

    private string? FindText(string key) =>
        this.TryFindResource(key, out var value) ? value as string : null;

    private void SetActiveSegment(MaterialClockDialSelection selection)
    {
        if (_active != selection)
            _active = selection;

        // Changing the dial's selection can coerce its value (an hour above 23 is not a valid
        // minute), which would otherwise echo back into the cells.
        _syncing = true;
        UpdateActiveSegment();
        UpdateDial();
        _syncing = false;
    }

    private void CommitTime()
    {
        _syncing = true;
        SetCurrentValue(TimeProperty, new TimeSpan(_hour24, _minute, 0));
        UpdatePeriod();
        UpdateDial();
        _syncing = false;
        UpdateValidation();
    }

    private void HourSelectionRequested(object? sender, EventArgs e) => SetActiveSegment(MaterialClockDialSelection.Hour);

    private void MinuteSelectionRequested(object? sender, EventArgs e) => SetActiveSegment(MaterialClockDialSelection.Minute);

    private void HourValueChanged(object? sender, EventArgs e)
    {
        if (_syncing || _hourSegment is null) return;
        var value = _hourSegment.Value;

        if (Format == MaterialTimeFormat.Hour12)
        {
            // 12 and 12-hour values share the modulo mapping: 12 AM is 0, 12 PM is 12.
            _hour24 = value % 12 + (_isPm ? 12 : 0);
        }
        else
        {
            _hour24 = value;
            _isPm = _hour24 >= 12;
        }

        CommitTime();
    }

    private void MinuteValueChanged(object? sender, EventArgs e)
    {
        if (_syncing || _minuteSegment is null) return;
        _minute = _minuteSegment.Value;
        CommitTime();
    }

    private void HourEntryCompleted(object? sender, MaterialTimeEntryEventArgs e)
    {
        if (_syncing) return;
        // A completed hour only steals focus when automatic advance is enabled. Enter is explicit
        // intent, so it still moves even with AutoAdvance off (source ImeAction.Next semantics).
        if (!e.IsExplicit && !AutoAdvance) return;
        // This runs inside the editor's TextChanged; switching focus synchronously would re-enter
        // the text pipeline through LostFocus.
        Dispatcher.UIThread.Post(() =>
        {
            SetActiveSegment(MaterialClockDialSelection.Minute);
            _minuteSegment?.FocusEditor();
        });
    }

    private void MinuteEntryCompleted(object? sender, MaterialTimeEntryEventArgs e)
    {
        // Typing a complete minute should not confirm the picker; only Enter does.
        if (e.IsExplicit) Confirm();
    }

    private void SegmentValidityChanged(object? sender, EventArgs e) => UpdateValidation();

    private void EntryCancelRequested(object? sender, EventArgs e) => Cancel();

    private void PeriodChanged(object? sender, EventArgs e)
    {
        if (_syncing) return;

        var requested = ReferenceEquals(sender, _sidePeriod)
            ? _sidePeriod?.IsPm ?? _isPm
            : _period?.IsPm ?? _isPm;
        if (requested == _isPm)
            return;

        // Source ToggleItem actions are guarded by isHourInputValid: an invalid hour draft keeps
        // both the valid hour and the selected period unchanged, and the visual snaps back.
        if (_hourSegment?.IsInvalid == true)
        {
            _syncing = true;
            if (_period is not null) _period.IsPm = _isPm;
            if (_sidePeriod is not null) _sidePeriod.IsPm = _isPm;
            _syncing = false;
            return;
        }

        _isPm = requested;
        _hour24 = _hour24 % 12 + (_isPm ? 12 : 0);
        CommitTime();
    }

    private void DialValueChanged(object? sender, EventArgs e)
    {
        if (_syncing || _dial is null) return;
        if (_dial.Selection == MaterialClockDialSelection.Hour)
        {
            if (_hourSegment is not null) _hourSegment.Value = _dial.Value;
        }
        else if (_minuteSegment is not null)
        {
            _minuteSegment.Value = _dial.Value;
        }
    }

    private async void DialPointerSelectionCompleted(object? sender, MaterialClockDialSelectionEventArgs e)
    {
        var dial = _dial;
        if (!AutoAdvance || _syncing || dial is null || dial.Selection != MaterialClockDialSelection.Hour)
            return;
        if (e.IsTap && !await dial.WaitForSelectionAsync()) return;
        if (!AutoAdvance || !ReferenceEquals(_dial, dial) || !this.IsAttachedToVisualTree() ||
            dial.Selection != MaterialClockDialSelection.Hour || InputMode != MaterialTimeInputMode.Dial)
            return;
        _active = MaterialClockDialSelection.Minute;
        UpdateActiveSegment();
        UpdateDial();
        _minuteSegment?.Focus();
    }

    private void OkClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Confirm();

    private void CancelClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Cancel();

    private void ModeToggleClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => ToggleInputMode();

    protected override AutomationPeer OnCreateAutomationPeer() => new MaterialTimePickerAutomationPeer(this);

    private sealed class MaterialTimePickerAutomationPeer(MaterialTimePicker owner) : ControlAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(MaterialTimePicker);

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
    }
}
