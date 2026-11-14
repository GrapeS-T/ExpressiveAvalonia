using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// Docked desktop date picker: an editable Material text field with a calendar icon that opens
/// the docked <see cref="MaterialDatePicker"/> surface in a light-dismiss popup.
/// </summary>
/// <remarks>
/// <para>
/// Typing accepts the culture short date pattern (or <see cref="DateFormat"/>) and ISO dates.
/// Enter commits, Escape and light dismissal restore the value captured when the popup opened,
/// and the picker's OK action keeps the live selection. Focus returns to the text field when
/// the popup closes, including light dismiss.
/// </para>
/// <para>
/// The popup picker is created by the template and owns no timers. Detaching the control closes
/// the popup and releases the picker so the control can be moved between windows.
/// </para>
/// </remarks>
[TemplatePart("PART_Input", typeof(TextBox))]
[TemplatePart("PART_OpenButton", typeof(Button))]
[TemplatePart("PART_Popup", typeof(Popup))]
[TemplatePart("PART_Error", typeof(TextBlock))]
public sealed class MaterialDatePickerField : TemplatedControl
{
    public static readonly StyledProperty<DateTimeOffset?> SelectedDateProperty =
        AvaloniaProperty.Register<MaterialDatePickerField, DateTimeOffset?>(
            nameof(SelectedDate), defaultBindingMode: BindingMode.TwoWay, coerce: CoerceDate);

    public static readonly StyledProperty<DateTimeOffset?> MinimumDateProperty =
        AvaloniaProperty.Register<MaterialDatePickerField, DateTimeOffset?>(nameof(MinimumDate));

    public static readonly StyledProperty<DateTimeOffset?> MaximumDateProperty =
        AvaloniaProperty.Register<MaterialDatePickerField, DateTimeOffset?>(nameof(MaximumDate));

    public static readonly StyledProperty<CultureInfo?> CultureProperty =
        AvaloniaProperty.Register<MaterialDatePickerField, CultureInfo?>(nameof(Culture));

    public static readonly StyledProperty<string?> DateFormatProperty =
        AvaloniaProperty.Register<MaterialDatePickerField, string?>(nameof(DateFormat));

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<MaterialDatePickerField, string?>(nameof(Label));

    public static readonly StyledProperty<string?> SupportingTextProperty =
        AvaloniaProperty.Register<MaterialDatePickerField, string?>(nameof(SupportingText));

    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<MaterialDatePickerField, bool>(
            nameof(IsOpen), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> IsEditableProperty =
        AvaloniaProperty.Register<MaterialDatePickerField, bool>(nameof(IsEditable), true);

    public static readonly DirectProperty<MaterialDatePickerField, string> PlaceholderTextProperty =
        AvaloniaProperty.RegisterDirect<MaterialDatePickerField, string>(
            nameof(PlaceholderText), o => o.PlaceholderText);

    public static readonly DirectProperty<MaterialDatePickerField, string?> ValidationMessageProperty =
        AvaloniaProperty.RegisterDirect<MaterialDatePickerField, string?>(
            nameof(ValidationMessage), o => o.ValidationMessage);

    private TextBox? _input;
    private Button? _openButton;
    private Popup? _popup;
    private TextBlock? _error;
    private MaterialDatePicker? _picker;
    private DateTimeOffset? _valueAtOpen;
    private bool _syncing;
    private bool _acceptingSelection;
    private bool _sessionPrepared;
    private DateTime _lastDismiss = DateTime.MinValue;
    private string _placeholderText = string.Empty;
    private string? _validationMessage;

    /// <summary>Raised when the popup opens.</summary>
    public event EventHandler? Opened;

    /// <summary>Raised when the popup closes for any reason.</summary>
    public event EventHandler? Closed;

    /// <summary>Raised after a typed date is parsed and accepted.</summary>
    public event EventHandler? SelectionCommitted;

    public DateTimeOffset? SelectedDate
    {
        get => GetValue(SelectedDateProperty);
        set => SetValue(SelectedDateProperty, value);
    }

    public DateTimeOffset? MinimumDate
    {
        get => GetValue(MinimumDateProperty);
        set => SetValue(MinimumDateProperty, value);
    }

    public DateTimeOffset? MaximumDate
    {
        get => GetValue(MaximumDateProperty);
        set => SetValue(MaximumDateProperty, value);
    }

    public CultureInfo? Culture
    {
        get => GetValue(CultureProperty);
        set => SetValue(CultureProperty, value);
    }

    public string? DateFormat
    {
        get => GetValue(DateFormatProperty);
        set => SetValue(DateFormatProperty, value);
    }

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string? SupportingText
    {
        get => GetValue(SupportingTextProperty);
        set => SetValue(SupportingTextProperty, value);
    }

    /// <summary>Whether the docked picker popup is open. Two-way bindable.</summary>
    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <summary>Allows typing in the field. The calendar button stays available.</summary>
    public bool IsEditable
    {
        get => GetValue(IsEditableProperty);
        set => SetValue(IsEditableProperty, value);
    }

    /// <summary>The localized short-date pattern used as the field watermark.</summary>
    public string PlaceholderText => _placeholderText;

    /// <summary>Validation message from the last typed value; null when valid.</summary>
    public string? ValidationMessage => _validationMessage;

    private CultureInfo EffectiveCulture => Culture ?? CultureInfo.CurrentCulture;

    private string EffectiveDateFormat =>
        string.IsNullOrWhiteSpace(DateFormat) ? EffectiveCulture.DateTimeFormat.ShortDatePattern : DateFormat;

    /// <summary>Opens the docked picker, restoring the popup to the current value.</summary>
    public void Open()
    {
        if (IsOpen)
            return;
        PrepareSession();
        SetCurrentValue(IsOpenProperty, true);
        Dispatcher.UIThread.Post(() =>
        {
            if (IsOpen && _picker is { } picker && picker.IsAttachedToVisualTree())
                picker.Focus(NavigationMethod.Unspecified);
        }, DispatcherPriority.Background);
    }

    /// <summary>Captures the value to restore on dismissal and points the picker at it.</summary>
    private void PrepareSession()
    {
        if (_sessionPrepared)
            return;
        _sessionPrepared = true;
        _valueAtOpen = SelectedDate;
        _acceptingSelection = false;
        if (_picker is null)
            return;
        SyncPickerOptions();
        _picker.SelectedDate = SelectedDate;
        _picker.SelectedEndDate = null;
        _picker.DisplayDate = SelectedDate ?? MaterialDatePicker.ToDateTimeOffset(DateTime.Today);
        _picker.View = MaterialDatePickerView.Day;
        _picker.IsInputMode = false;
    }

    /// <summary>Closes the popup without committing the live selection.</summary>
    public void Close() => SetCurrentValue(IsOpenProperty, false);

    /// <summary>Opens a closed popup or closes an open one.</summary>
    public void Toggle()
    {
        if (IsOpen)
            Close();
        else
            Open();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        DetachParts();
        base.OnApplyTemplate(e);

        _input = e.NameScope.Find<TextBox>("PART_Input");
        _openButton = e.NameScope.Find<Button>("PART_OpenButton");
        _popup = e.NameScope.Find<Popup>("PART_Popup");
        _error = e.NameScope.Find<TextBlock>("PART_Error");

        if (_input is not null)
        {
            _input.KeyDown += OnInputKeyDown;
            _input.LostFocus += OnInputLostFocus;
            _input.TextChanged += OnInputTextChanged;
            AutomationProperties.SetName(_input, Label ?? GetText("MaterialDatePickerDateText", "Date"));
        }
        if (_openButton is not null)
            _openButton.Click += OnOpenButtonClick;

        if (_popup is not null)
        {
            _popup.PlacementTarget = _input;
            _popup.VerticalOffset = 4;
            _popup.PropertyChanged += OnPopupPropertyChanged;
            _picker = new MaterialDatePicker
            {
                Mode = MaterialDatePickerMode.Docked,
                SelectionMode = MaterialDateSelectionMode.Single,
                ShowActions = true,
                ShowClearButton = true,
                AllowInputMode = false,
            };
            _picker.SelectionChanged += OnPickerSelectionChanged;
            _picker.Confirmed += OnPickerConfirmed;
            _picker.Cancelled += OnPickerCancelled;
            _popup.Child = CreatePopupContent(_picker);
        }

        UpdatePlaceholder();
        SyncPickerOptions();
        SyncPickerSelection();
        SyncInputText();
        UpdateError();
        if (_popup is not null)
        {
            _popup.PlacementTarget = _input ?? (Control)this;
            if (IsOpen)
            {
                // A template applied after Open() has to rebuild the session against the new parts.
                _sessionPrepared = false;
                PrepareSession();
            }
            _popup.IsOpen = IsOpen;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SelectedDateProperty)
        {
            if (_syncing)
                return;
            SyncPickerSelection();
            SyncInputText();
            return;
        }

        if (change.Property == MinimumDateProperty || change.Property == MaximumDateProperty ||
            change.Property == CultureProperty || change.Property == DateFormatProperty)
        {
            UpdatePlaceholder();
            SyncPickerOptions();
            SyncInputText();
            return;
        }

        if (change.Property == LabelProperty)
        {
            if (_input is not null)
                AutomationProperties.SetName(_input, Label ?? GetText("MaterialDatePickerDateText", "Date"));
            return;
        }

        if (change.Property == IsOpenProperty)
        {
            if (IsOpen)
                PrepareSession();
            if (_popup is not null && _popup.IsOpen != IsOpen)
                _popup.IsOpen = IsOpen;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        var popup = _popup;
        DetachParts();
        if (popup is not null)
            popup.Child = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void DetachParts()
    {
        if (_input is not null)
        {
            _input.KeyDown -= OnInputKeyDown;
            _input.LostFocus -= OnInputLostFocus;
            _input.TextChanged -= OnInputTextChanged;
        }
        if (_openButton is not null)
            _openButton.Click -= OnOpenButtonClick;
        if (_popup is not null)
        {
            _popup.PropertyChanged -= OnPopupPropertyChanged;
            _popup.IsOpen = false;
        }
        if (_picker is not null)
        {
            _picker.SelectionChanged -= OnPickerSelectionChanged;
            _picker.Confirmed -= OnPickerConfirmed;
            _picker.Cancelled -= OnPickerCancelled;
            _picker = null;
        }
        _input = null;
        _openButton = null;
        _popup = null;
        _error = null;
        _sessionPrepared = false;
    }

    private Control CreatePopupContent(MaterialDatePicker picker)
    {
        var border = new Border
        {
            CornerRadius = new CornerRadius(16),
            Child = picker,
        };
        border.Bind(Border.BackgroundProperty, border.GetResourceObservable("MaterialSurfaceContainerHighBrush"));
        border.Bind(Border.BoxShadowProperty, border.GetResourceObservable("MaterialElevationLevel2Shadow"));
        return border;
    }

    private void SyncPickerOptions()
    {
        if (_picker is null)
            return;
        _syncing = true;
        try
        {
            _picker.MinimumDate = MinimumDate;
            _picker.MaximumDate = MaximumDate;
            _picker.Culture = Culture;
            _picker.DateFormat = DateFormat;
        }
        finally
        {
            _syncing = false;
        }
    }

    private void SyncPickerSelection()
    {
        if (_picker is null)
            return;
        _syncing = true;
        try
        {
            _picker.SelectedDate = SelectedDate;
            _picker.SelectedEndDate = null;
            if (SelectedDate is { } value)
                _picker.DisplayDate = value;
        }
        finally
        {
            _syncing = false;
        }
    }

    private void SyncInputText()
    {
        if (_input is null || _input.IsFocused)
            return;
        _input.Text = SelectedDate is { } value ? FormatValue(value.Date) : string.Empty;
    }

    private string FormatValue(DateTime date) =>
        date.ToString(EffectiveDateFormat, EffectiveCulture);

    private void UpdatePlaceholder()
    {
        var value = EffectiveDateFormat.ToUpperInvariant();
        if (value == _placeholderText)
            return;
        var old = _placeholderText;
        _placeholderText = value;
        RaisePropertyChanged(PlaceholderTextProperty, old, value);
    }

    private void UpdateError()
    {
        if (_error is not null)
        {
            _error.Text = _validationMessage;
            _error.IsVisible = !string.IsNullOrEmpty(_validationMessage);
        }
    }

    private void SetValidationMessage(string? message)
    {
        if (message == _validationMessage)
            return;
        var old = _validationMessage;
        _validationMessage = message;
        RaisePropertyChanged(ValidationMessageProperty, old, message);
        UpdateError();
    }

    private void SetSelectedDate(DateTimeOffset? value)
    {
        _syncing = true;
        try
        {
            SetCurrentValue(SelectedDateProperty, value);
            if (_picker is not null)
            {
                _picker.SelectedDate = value;
                if (value is { } date)
                    _picker.DisplayDate = date;
            }
        }
        finally
        {
            _syncing = false;
        }
        SyncInputText();
    }

    private void OnOpenButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // Light dismissal handles the press before the click; ignore the resulting toggle.
        if ((DateTime.UtcNow - _lastDismiss).TotalMilliseconds < 250 && !IsOpen)
            return;
        Toggle();
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitText();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            RestoreOpenValue();
            Close();
            e.Handled = true;
        }
        else if (e.Key == Key.F4 || (e.Key == Key.Down && e.KeyModifiers == KeyModifiers.Alt))
        {
            Open();
            e.Handled = true;
        }
    }

    private void OnInputLostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // Focus moving to the calendar button or the popup is not a commit point.
        if (!IsOpen && !IsKeyboardFocusWithin)
            CommitText();
    }

    private void OnInputTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_validationMessage))
            SetValidationMessage(null);
    }

    private void CommitText()
    {
        if (_picker is null || _input is null)
            return;

        var text = _input.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            SetValidationMessage(null);
            SetSelectedDate(null);
            return;
        }

        if (!_picker.TryParseDate(text, out var date))
        {
            SetValidationMessage(GetText("MaterialDatePickerInvalidDateText", "Enter a valid date"));
            return;
        }
        if (!_picker.IsDateAllowed(date))
        {
            SetValidationMessage(GetText("MaterialDatePickerOutOfRangeText", "Date is outside the allowed range"));
            return;
        }

        SetValidationMessage(null);
        SetSelectedDate(MaterialDatePicker.ToDateTimeOffset(date));
        SelectionCommitted?.Invoke(this, EventArgs.Empty);
    }

    private void OnPickerSelectionChanged(object? sender, MaterialDateSelectionChangedEventArgs e)
    {
        if (_syncing)
            return;
        _syncing = true;
        try
        {
            SetCurrentValue(SelectedDateProperty, e.Start);
        }
        finally
        {
            _syncing = false;
        }
        SyncInputText();
    }

    private void OnPickerConfirmed(object? sender, EventArgs e)
    {
        _acceptingSelection = true;
        SetCurrentValue(IsOpenProperty, false);
    }

    private void OnPickerCancelled(object? sender, EventArgs e)
    {
        RestoreOpenValue();
        SetCurrentValue(IsOpenProperty, false);
    }

    private void OnPopupPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != Popup.IsOpenProperty)
            return;

        if (e.GetNewValue<bool>())
        {
            Opened?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (!_acceptingSelection)
        {
            RestoreOpenValue();
            _lastDismiss = DateTime.UtcNow;
        }
        _acceptingSelection = false;
        _sessionPrepared = false;

        if (IsOpen)
            SetCurrentValue(IsOpenProperty, false);
        Closed?.Invoke(this, EventArgs.Empty);

        Dispatcher.UIThread.Post(() =>
        {
            if (_input is { IsEffectivelyEnabled: true } input && input.IsAttachedToVisualTree())
                input.Focus(NavigationMethod.Unspecified);
        }, DispatcherPriority.Background);
    }

    private void RestoreOpenValue()
    {
        if (SelectedDate == _valueAtOpen)
            return;
        SetSelectedDate(_valueAtOpen);
    }

    private static DateTimeOffset? CoerceDate(AvaloniaObject instance, DateTimeOffset? value)
    {
        // The field and the picker share one date-only representation: UTC midnight of the
        // calendar date. A local DateTimeOffset bound by a consumer cannot shift the day.
        return value is { } date ? MaterialDatePicker.ToDateTimeOffset(date.Date) : null;
    }

    private string GetText(string key, string fallback) =>
        this.TryFindResource(key, out var value) && value is string text ? text : fallback;
}
