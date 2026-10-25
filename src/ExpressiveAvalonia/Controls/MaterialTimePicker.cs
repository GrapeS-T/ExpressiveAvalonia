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

    public static readonly StyledProperty<MaterialTimeInputMode> InputModeProperty =
        AvaloniaProperty.Register<MaterialTimePicker, MaterialTimeInputMode>(nameof(InputMode));

    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<MaterialTimePicker, Orientation>(nameof(Orientation), Orientation.Vertical);

    public static readonly StyledProperty<string?> HeadlineProperty =
        AvaloniaProperty.Register<MaterialTimePicker, string?>(nameof(Headline));

    public static readonly StyledProperty<int> MinuteIncrementProperty =
        AvaloniaProperty.Register<MaterialTimePicker, int>(nameof(MinuteIncrement), 1,
            validate: value => value is > 0 and <= 60);

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
    private MaterialClockDial? _dial;
    private Button? _ok;
    private Button? _cancel;
    private Button? _modeToggle;
    private Grid? _actions;
    private MaterialTimeInputMode? _iconMode;
    private MaterialClockDialSelection _active = MaterialClockDialSelection.Hour;
    private int _hour24;
    private int _minute;
    private bool _isPm;
    private bool _syncing;

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

    /// <summary>Dial or direct keyboard entry.</summary>
    public MaterialTimeInputMode InputMode
    {
        get => GetValue(InputModeProperty);
        set => SetValue(InputModeProperty, value);
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
    public void FocusFirstField() => _hourSegment?.FocusEditor();

    /// <summary>Switches between the dial and keyboard surfaces.</summary>
    public void ToggleInputMode() =>
        InputMode = InputMode == MaterialTimeInputMode.Dial
            ? MaterialTimeInputMode.Keyboard
            : MaterialTimeInputMode.Dial;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        DetachParts();
        base.OnApplyTemplate(e);

        _hourSegment = e.NameScope.Find<MaterialTimeInputSegment>("PART_Hour");
        _minuteSegment = e.NameScope.Find<MaterialTimeInputSegment>("PART_Minute");
        _period = e.NameScope.Find<MaterialPeriodSelector>("PART_Period");
        _dial = e.NameScope.Find<MaterialClockDial>("PART_Dial");
        _ok = e.NameScope.Find<Button>("PART_Ok");
        _cancel = e.NameScope.Find<Button>("PART_Cancel");
        _modeToggle = e.NameScope.Find<Button>("PART_ModeToggle");
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
        if (_dial is not null)
            _dial.ValueChanged += DialValueChanged;
        if (_ok is not null)
            _ok.Click += OkClick;
        if (_cancel is not null)
            _cancel.Click += CancelClick;
        if (_modeToggle is not null)
            _modeToggle.Click += ModeToggleClick;

        _active = MaterialClockDialSelection.Hour;
        _iconMode = null;
        UpdateLayoutClasses();
        SyncFromTime();
    }

    private void UpdateLayoutClasses()
    {
        PseudoClasses.Set(":horizontal", Orientation == Avalonia.Layout.Orientation.Horizontal);
        PseudoClasses.Set(":keyboard", InputMode == MaterialTimeInputMode.Keyboard);
        PseudoClasses.Set(":dial", InputMode == MaterialTimeInputMode.Dial);
        PseudoClasses.Set(":hour12", Format == MaterialTimeFormat.Hour12);
        PseudoClasses.Set(":hour24", Format == MaterialTimeFormat.Hour24);
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
        if (_dial is not null)
            _dial.ValueChanged -= DialValueChanged;
        if (_ok is not null)
            _ok.Click -= OkClick;
        if (_cancel is not null)
            _cancel.Click -= CancelClick;
        if (_modeToggle is not null)
            _modeToggle.Click -= ModeToggleClick;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TimeProperty)
        {
            if (!_syncing) SyncFromTime();
        }
        else if (change.Property == FormatProperty || change.Property == InputModeProperty ||
                 change.Property == OrientationProperty)
        {
            UpdateLayoutClasses();
            SyncFromTime();
        }
        else if (change.Property == HeadlineProperty)
        {
            UpdateHeadline();
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
        _syncing = false;

        UpdateValidation();
    }

    private void UpdateHeadline()
    {
        EffectiveHeadline = Headline ?? (InputMode == MaterialTimeInputMode.Dial
            ? FindText("MaterialTimePickerSelectTimeText") ?? "Select time"
            : FindText("MaterialTimePickerEnterTimeText") ?? "Enter time");

        if (string.IsNullOrEmpty(AutomationProperties.GetName(this)))
            AutomationProperties.SetName(this, EffectiveHeadline);

        if (_modeToggle is null) return;

        var switchToKeyboard = InputMode == MaterialTimeInputMode.Dial;
        var next = switchToKeyboard
            ? FindText("MaterialTimePickerSwitchToKeyboardText") ?? "Switch to keyboard input"
            : FindText("MaterialTimePickerSwitchToDialText") ?? "Switch to dial input";
        AutomationProperties.SetName(_modeToggle, next);
        ToolTip.SetTip(_modeToggle, next);

        // The mode button carries the opposite surface's glyph: keyboard on the dial, clock on
        // keyboard entry. Rebuilding the icon on every external time change would allocate for
        // nothing, so it is only replaced when the mode actually changes.
        if (_iconMode == InputMode) return;
        _iconMode = InputMode;

        var geometryKey = switchToKeyboard
            ? "MaterialTimePickerKeyboardIconGeometry"
            : "MaterialTimePickerScheduleIconGeometry";
        if (this.TryFindResource(geometryKey, out var resource) && resource is Geometry geometry)
            _modeToggle.Content = new PathIcon { Data = geometry, Width = 24, Height = 24 };
    }

    private void UpdateSegmentConfiguration()
    {
        var keyboard = InputMode == MaterialTimeInputMode.Keyboard;
        var variant = keyboard ? MaterialTimeSegmentVariant.Keyboard : MaterialTimeSegmentVariant.Dial;

        if (_hourSegment is { } hour)
        {
            hour.Minimum = Format == MaterialTimeFormat.Hour12 ? 1 : 0;
            hour.Maximum = Format == MaterialTimeFormat.Hour12 ? 12 : 23;
            hour.Step = 1;
            hour.Variant = variant;
            hour.Label = FindText("MaterialTimePickerHourText") ?? "Hour";
            hour.HelpText = FormatRange(FindText("MaterialTimePickerHourHelpText")
                    ?? "Enter a value between {0} and {1}",
                hour.Minimum, hour.Maximum);
        }

        if (_minuteSegment is { } minute)
        {
            minute.Minimum = 0;
            minute.Maximum = 59;
            minute.Step = MinuteIncrement;
            minute.Variant = variant;
            minute.Label = FindText("MaterialTimePickerMinuteText") ?? "Minute";
            minute.HelpText = FormatRange(FindText("MaterialTimePickerMinuteHelpText")
                    ?? "Enter a value between {0} and {1}",
                0, 59);
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
        if (_period is null) return;
        _period.IsPm = _isPm;
        _period.IsCompact = InputMode == MaterialTimeInputMode.Keyboard;
        _period.IsVisible = Format == MaterialTimeFormat.Hour12;
        _period.Orientation = InputMode == MaterialTimeInputMode.Dial &&
                              Orientation == Avalonia.Layout.Orientation.Horizontal
            ? Avalonia.Layout.Orientation.Horizontal
            : Avalonia.Layout.Orientation.Vertical;
    }

    private void UpdateActiveSegment()
    {
        if (_hourSegment is not null)
            _hourSegment.IsSelected = _active == MaterialClockDialSelection.Hour;
        if (_minuteSegment is not null)
            _minuteSegment.IsSelected = _active == MaterialClockDialSelection.Minute;
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
        var invalidSegment = (_hourSegment?.IsInvalid ?? false) || (_minuteSegment?.IsInvalid ?? false);
        string? message = null;

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

    private static string FormatClock(TimeSpan time) =>
        $"{(int)time.TotalHours:00}:{time.Minutes:00}";

    // A missing localization key must not silently turn an out-of-range time into a valid one,
    // so the numeric fallback still carries both bounds.
    private static string FormatRange(string? pattern, object first, object second) =>
        string.Format(CultureInfo.CurrentCulture, pattern ?? "{0} - {1}", first, second);

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
        if (_syncing || _period is null) return;
        _isPm = _period.IsPm;
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
