using System;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace ExpressiveAvalonia.Controls;

/// <summary>How a <see cref="MaterialTimeInputSegment"/> is laid out.</summary>
public enum MaterialTimeSegmentVariant
{
    /// <summary>The large dial-picker cell: 96 x 80, 57 DIP value, no label.</summary>
    Dial,

    /// <summary>The keyboard-picker cell: 96 x 72 field plus a 12 DIP label, 45 DIP value.</summary>
    Keyboard,

    /// <summary>The vibrant picker cell: 100 x 120, 57 DIP value, no label.</summary>
    Vibrant,

    /// <summary>The uncontained vibrant cell: 100 x 136, 57 DIP value, no label.</summary>
    Uncontained,
}

/// <summary>Reports how a time segment finished entry.</summary>
public sealed class MaterialTimeEntryEventArgs(bool isExplicit) : EventArgs
{
    /// <summary>
    /// True when Enter committed the entry; false when the value became complete while typing,
    /// which advances focus without confirming the picker.
    /// </summary>
    public bool IsExplicit { get; } = isExplicit;
}

/// <summary>
/// One editable hour or minute cell of a <see cref="MaterialTimePicker"/>, and the direct-input
/// building block on its own.
/// </summary>
/// <remarks>
/// <para>
/// Editing is a native single-line <see cref="TextBox"/>: selection, caret, clipboard, IME and
/// screen-reader text editing all remain Avalonia's. The cell only adds time semantics: edits are
/// parsed as Unicode decimal digits, the value is range-checked against
/// <see cref="Minimum"/>/<see cref="Maximum"/>, out-of-range text raises the <c>:invalid</c>
/// pseudo-class instead of silently clamping, and a complete entry auto-advances. Native edits
/// are never filtered or truncated into a different value: a non-digit or overlong paste is
/// reverted to the text and selection it started from.
/// </para>
/// <para>
/// Up/Down step the value (wrapping by default), Enter commits, Escape asks the owner to cancel.
/// </para>
/// </remarks>
public sealed class MaterialTimeInputSegment : TemplatedControl
{
    public static readonly StyledProperty<int> ValueProperty =
        AvaloniaProperty.Register<MaterialTimeInputSegment, int>(nameof(Value), 0,
            defaultBindingMode: BindingMode.TwoWay,
            coerce: (o, value) => ((MaterialTimeInputSegment)o).CoerceSegmentValue(value));

    public static readonly StyledProperty<int> MinimumProperty =
        AvaloniaProperty.Register<MaterialTimeInputSegment, int>(nameof(Minimum), 0);

    public static readonly StyledProperty<int> MaximumProperty =
        AvaloniaProperty.Register<MaterialTimeInputSegment, int>(nameof(Maximum), 59);

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<MaterialTimeInputSegment, string?>(nameof(Text),
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<MaterialTimeInputSegment, bool>(nameof(IsSelected));

    public static readonly StyledProperty<bool> IsInvalidProperty =
        AvaloniaProperty.Register<MaterialTimeInputSegment, bool>(nameof(IsInvalid));

    public static readonly StyledProperty<MaterialTimeSegmentVariant> VariantProperty =
        AvaloniaProperty.Register<MaterialTimeInputSegment, MaterialTimeSegmentVariant>(nameof(Variant));

    /// <summary>
    /// Height of the editable container alone. The dial cell is 80 (total 80); the keyboard cell
    /// is 72 with a 7 DIP gap and a 16 DIP label, so the control measures 95.
    /// </summary>
    public static readonly StyledProperty<double> FieldHeightProperty =
        AvaloniaProperty.Register<MaterialTimeInputSegment, double>(nameof(FieldHeight), 80,
            validate: value => double.IsFinite(value) && value > 0);

    /// <summary>
    /// Culture used for the displayed digits. Null follows <see cref="CultureInfo.CurrentCulture"/>,
    /// matching the source locale-aware <c>NumberFormat</c>; changing it reformats the visible text
    /// without touching an invalid draft.
    /// </summary>
    public static readonly StyledProperty<CultureInfo?> CultureProperty =
        AvaloniaProperty.Register<MaterialTimeInputSegment, CultureInfo?>(nameof(Culture));

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<MaterialTimeInputSegment, string?>(nameof(Label));

    public static readonly StyledProperty<string?> HelpTextProperty =
        AvaloniaProperty.Register<MaterialTimeInputSegment, string?>(nameof(HelpText));

    public static readonly StyledProperty<int> StepProperty =
        AvaloniaProperty.Register<MaterialTimeInputSegment, int>(nameof(Step), 1,
            validate: value => value > 0);

    public static readonly StyledProperty<bool> WrapProperty =
        AvaloniaProperty.Register<MaterialTimeInputSegment, bool>(nameof(Wrap), true);

    /// <summary>
    /// Identifies the <see cref="EmptyValue"/> property. Clearing the editor produces this value,
    /// matching the source's empty-buffer rule (0 for a 24-hour/minute field, 12 for a 12-hour
    /// field because 12 and 0 share a dial position).
    /// </summary>
    public static readonly StyledProperty<int> EmptyValueProperty =
        AvaloniaProperty.Register<MaterialTimeInputSegment, int>(nameof(EmptyValue));

    /// <summary>
    /// Identifies the <see cref="AutoAdvanceSingleDigit"/> property. The source only advances a
    /// one-digit hour entry in 12-hour mode once the digit can no longer be extended (2-9).
    /// </summary>
    public static readonly StyledProperty<bool> AutoAdvanceSingleDigitProperty =
        AvaloniaProperty.Register<MaterialTimeInputSegment, bool>(nameof(AutoAdvanceSingleDigit));

    private TextBox? _editor;
    private bool _updatingText;
    private bool _updatingValue;

    // TextBox.TextChanging fires synchronously before the native edit moves the caret. Keeping the
    // pre-edit text/selection lets the async TextChanged handler apply the source's
    // originalSelection-sensitive rules and revert rejected edits exactly.
    private string _editorText = string.Empty;
    private string _pendingOriginalText = string.Empty;
    private int _pendingOriginalSelectionStart;
    private int _pendingOriginalSelectionEnd;
    private bool _hasPendingEdit;

    private CultureInfo EffectiveCulture => Culture ?? CultureInfo.CurrentCulture;

    public MaterialTimeInputSegment()
    {
        AddHandler(PointerPressedEvent, OnSegmentPointerPressed, RoutingStrategies.Tunnel);
    }

    /// <summary>The numeric value shown by the cell.</summary>
    public int Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>The smallest accepted entry.</summary>
    public int Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    /// <summary>The largest accepted entry.</summary>
    public int Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    /// <summary>The text being edited, always two digits once committed.</summary>
    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>Whether this cell is the one the dial and typing apply to.</summary>
    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    /// <summary>True while the typed text is not a value the cell accepts.</summary>
    public bool IsInvalid
    {
        get => GetValue(IsInvalidProperty);
        private set => SetValue(IsInvalidProperty, value);
    }

    /// <summary>Which of the two source cell layouts to use.</summary>
    public MaterialTimeSegmentVariant Variant
    {
        get => GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    /// <summary>Height of the editable container, excluding the keyboard label row.</summary>
    public double FieldHeight
    {
        get => GetValue(FieldHeightProperty);
        set => SetValue(FieldHeightProperty, value);
    }

    /// <summary>
    /// Culture used for the displayed digits. Null follows <see cref="CultureInfo.CurrentCulture"/>.
    /// Input accepts Unicode decimal digits in any culture; display uses the culture's
    /// <see cref="NumberFormatInfo.NativeDigits"/>.
    /// </summary>
    public CultureInfo? Culture
    {
        get => GetValue(CultureProperty);
        set => SetValue(CultureProperty, value);
    }

    /// <summary>The accessible and visible label ("Hour", "Minute").</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Additional accessible guidance, normally the accepted range.</summary>
    public string? HelpText
    {
        get => GetValue(HelpTextProperty);
        set => SetValue(HelpTextProperty, value);
    }

    /// <summary>How much one Up/Down press changes the value.</summary>
    public int Step
    {
        get => GetValue(StepProperty);
        set => SetValue(StepProperty, value);
    }

    /// <summary>Whether stepping past an end wraps to the other end.</summary>
    public bool Wrap
    {
        get => GetValue(WrapProperty);
        set => SetValue(WrapProperty, value);
    }

    /// <summary>Value produced when the user clears the editor.</summary>
    public int EmptyValue
    {
        get => GetValue(EmptyValueProperty);
        set => SetValue(EmptyValueProperty, value);
    }

    /// <summary>
    /// Whether a single 2-9 digit completes this cell immediately. Enabled only for a 12-hour
    /// hour field; 24-hour hours and minutes wait for a second digit.
    /// </summary>
    public bool AutoAdvanceSingleDigit
    {
        get => GetValue(AutoAdvanceSingleDigitProperty);
        set => SetValue(AutoAdvanceSingleDigitProperty, value);
    }

    /// <summary>Raised when the value changes, from typing, stepping or the owner.</summary>
    public event EventHandler? ValueChanged;

    /// <summary>Raised when <see cref="IsInvalid"/> changes, so the owner can revalidate.</summary>
    public event EventHandler? ValidityChanged;

    /// <summary>Raised when a complete value was typed, or when Enter committed the entry.</summary>
    public event EventHandler<MaterialTimeEntryEventArgs>? EntryCompleted;

    /// <summary>Raised when the cell is clicked or focused, so the owner can make it active.</summary>
    public event EventHandler? SelectionRequested;

    /// <summary>Raised when Escape asks the owner to cancel.</summary>
    public event EventHandler? CancelRequested;

    /// <summary>Moves keyboard focus into the editable text.</summary>
    public void FocusEditor()
    {
        if (_editor is null) return;
        _editor.Focus(NavigationMethod.Tab);
        _editor.SelectAll();
    }

    /// <summary>Reformats the text from the current value without raising entry events.</summary>
    public void NormalizeText()
    {
        IsInvalid = false;
        SetTextFromValue(Value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_editor is not null)
        {
            _editor.TextChanging -= EditorTextChanging;
            _editor.TextChanged -= EditorTextChanged;
            _editor.GotFocus -= EditorGotFocus;
            _editor.LostFocus -= EditorLostFocus;
            _editor.KeyDown -= EditorKeyDown;
        }

        base.OnApplyTemplate(e);
        _editor = e.NameScope.Find<TextBox>("PART_Editor");
        if (_editor is not null)
        {
            // PART_Editor is a custom-editor contract; a consumer template may still cap its
            // MaxLength. A cap truncates a third digit or paste before the source transform can
            // replace or reject it, so the cell owns the two-digit rule in EditorTextChanged and
            // forces the editor to hand it the complete edit.
            _editor.MaxLength = 0;
            _editor.TextChanging += EditorTextChanging;
            _editor.TextChanged += EditorTextChanged;
            _editor.GotFocus += EditorGotFocus;
            _editor.LostFocus += EditorLostFocus;
            _editor.KeyDown += EditorKeyDown;
        }

        UpdateAutomation();
        UpdateVariantClasses();
        PseudoClasses.Set(":selected", IsSelected);
        PseudoClasses.Set(":invalid", IsInvalid);
        // The template may be re-applied (theme change, detach/attach); the editor is the only
        // text owner, so it is seeded from the current value every time.
        var initial = string.IsNullOrEmpty(Text) ? FormatValue(Value) : Text;
        _updatingText = true;
        SetCurrentValue(TextProperty, initial);
        if (_editor is not null) _editor.Text = initial;
        _updatingText = false;
        CaptureEditorSnapshot();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty)
        {
            if (!_updatingValue)
                SetTextFromValue(Value);
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
        else if (change.Property == MinimumProperty || change.Property == MaximumProperty)
        {
            CoerceValue(ValueProperty);
            if (!_updatingValue)
                SetTextFromValue(Value);
        }
        else if (change.Property == TextProperty && !_updatingText)
        {
            // A consumer or binding wrote the text; mirror it into the native editor first so
            // both stay in step, then validate.
            if (_editor is not null && !string.Equals(_editor.Text, Text, StringComparison.Ordinal))
            {
                _updatingText = true;
                _editor.Text = Text ?? string.Empty;
                _editor.CaretIndex = _editor.Text?.Length ?? 0;
                _updatingText = false;
            }

            ApplyTypedText();
            CaptureEditorSnapshot();
        }
        else if (change.Property == CultureProperty)
        {
            // An invalid draft belongs to the user, not the display culture; leave its digits
            // untouched until the next accepted edit. Valid values reformat in place.
            if (!IsInvalid)
                SetTextFromValue(Value);
        }
        else if (change.Property == IsInvalidProperty)
        {
            PseudoClasses.Set(":invalid", IsInvalid);
            ValidityChanged?.Invoke(this, EventArgs.Empty);
        }
        else if (change.Property == IsSelectedProperty)
        {
            PseudoClasses.Set(":selected", IsSelected);
        }
        else if (change.Property == VariantProperty)
        {
            UpdateVariantClasses();
        }
        else if (change.Property == LabelProperty || change.Property == HelpTextProperty)
        {
            UpdateAutomation();
        }
    }

    private void UpdateVariantClasses()
    {
        PseudoClasses.Set(":keyboard", Variant == MaterialTimeSegmentVariant.Keyboard);
        PseudoClasses.Set(":dial", Variant == MaterialTimeSegmentVariant.Dial);
        PseudoClasses.Set(":vibrant", Variant == MaterialTimeSegmentVariant.Vibrant);
        PseudoClasses.Set(":uncontained", Variant == MaterialTimeSegmentVariant.Uncontained);
    }

    private void UpdateAutomation()
    {
        if (_editor is null) return;
        AutomationProperties.SetName(_editor, Label ?? string.Empty);
        if (string.IsNullOrEmpty(HelpText))
            AutomationProperties.SetHelpText(_editor, null);
        else
            AutomationProperties.SetHelpText(_editor, HelpText);
    }

    private int CoerceSegmentValue(int value)
    {
        var low = Minimum;
        var high = Maximum;
        if (high < low) return low;
        return Math.Clamp(value, low, high);
    }

    private string FormatValue(int value) =>
        MaterialTimeNumerals.Format(value, 2, EffectiveCulture);

    private void SetTextFromValue(int value)
    {
        var text = FormatValue(value);
        _updatingText = true;
        if (!string.Equals(Text, text, StringComparison.Ordinal))
            SetCurrentValue(TextProperty, text);
        if (_editor is not null && !string.Equals(_editor.Text, text, StringComparison.Ordinal))
        {
            _editor.Text = text;
            _editor.CaretIndex = text.Length;
        }
        _updatingText = false;
        CaptureEditorSnapshot();
    }

    private void CaptureEditorSnapshot()
    {
        if (_editor is null) return;
        _editorText = _editor.Text ?? string.Empty;
    }

    private void EditorTextChanging(object? sender, TextChangingEventArgs e)
    {
        if (_updatingText || _editor is null) return;
        _pendingOriginalText = _editorText;
        // TextBox has already updated Text but not yet moved the caret to the insertion point.
        // Clamped old endpoints are therefore the source's originalSelection.
        _pendingOriginalSelectionStart = _editor.SelectionStart;
        _pendingOriginalSelectionEnd = _editor.SelectionEnd;
        _hasPendingEdit = true;
    }

    private void EditorTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_updatingText || _editor is null) return;
        var raw = _editor.Text ?? string.Empty;
        // TextChanged is dispatched asynchronously; programmatic synchronization or a revert may
        // already have restored the text. Only a captured native edit is transformed here.
        if (!_hasPendingEdit)
        {
            if (!string.Equals(raw, _editorText, StringComparison.Ordinal))
                CaptureEditorSnapshot();
            return;
        }

        var originalText = _pendingOriginalText;
        var originalStart = _pendingOriginalSelectionStart;
        var originalEnd = _pendingOriginalSelectionEnd;
        var originalSelectionCollapsed = originalStart == originalEnd;
        _hasPendingEdit = false;

        if (string.Equals(raw, originalText, StringComparison.Ordinal))
        {
            CaptureEditorSnapshot();
            return;
        }

        // Source transformInput: every character must be a decimal digit; otherwise revert.
        if (!MaterialTimeNumerals.AllDigits(raw))
        {
            RevertEdit(originalText, originalStart, originalEnd);
            return;
        }

        var text = raw;
        // Source isReplacingTwoDigits: a third digit typed into a full two-digit field replaces
        // the whole field with the inserted digit, selected by where the caret landed.
        if (originalSelectionCollapsed && originalText.Length == 2 && raw.Length == 3)
        {
            text = _editor.SelectionStart switch
            {
                1 => raw[..1],
                2 => raw.Substring(1, 1),
                3 => raw.Substring(2, 1),
                _ => raw,
            };
        }

        if (text.Length > 2)
        {
            RevertEdit(originalText, originalStart, originalEnd);
            return;
        }

        _updatingText = true;
        if (!string.Equals(Text, text, StringComparison.Ordinal))
            SetCurrentValue(TextProperty, text);
        if (!string.Equals(_editor.Text, text, StringComparison.Ordinal))
        {
            _editor.Text = text;
            _editor.CaretIndex = text.Length;
        }
        _updatingText = false;

        ApplyTypedText();
        CaptureEditorSnapshot();
    }

    private void RevertEdit(string text, int selectionStart, int selectionEnd)
    {
        _updatingText = true;
        if (_editor is not null)
        {
            _editor.Text = text;
            var start = Math.Clamp(Math.Min(selectionStart, selectionEnd), 0, text.Length);
            var end = Math.Clamp(Math.Max(selectionStart, selectionEnd), 0, text.Length);
            _editor.SelectionStart = start;
            _editor.SelectionEnd = end;
        }
        if (!string.Equals(Text, text, StringComparison.Ordinal))
            SetCurrentValue(TextProperty, text);
        _updatingText = false;
        CaptureEditorSnapshot();
    }

    private void ApplyTypedText()
    {
        var digits = Text ?? string.Empty;
        if (digits.Length == 0)
        {
            // Source transformInput: an empty hour/minute buffer resolves to the period's zero
            // target and remains a valid input. The cell displays that target again.
            IsInvalid = false;
            if (Value != EmptyValue)
            {
                _updatingValue = true;
                SetCurrentValue(ValueProperty, EmptyValue);
                _updatingValue = false;
            }
            SetTextFromValue(Value);
            return;
        }

        if (!MaterialTimeNumerals.TryParse(digits, out var parsed))
        {
            IsInvalid = true;
            return;
        }

        if (parsed < Minimum || parsed > Maximum)
        {
            // A single digit that is only out of range because the entry is incomplete stays
            // neutral: "0" in a 1-12 hour cell can still become "09". Anything else is invalid.
            var canExtend = digits.Length == 1 && parsed * 10 <= Maximum;
            IsInvalid = !canExtend;
            return;
        }

        IsInvalid = false;
        if (parsed != Value)
        {
            _updatingValue = true;
            SetCurrentValue(ValueProperty, parsed);
            _updatingValue = false;
        }

        // Two digits always complete an entry. A single digit completes a 12-hour hour field only
        // when it is 2-9 (the source's `is12HourAutoAdvanceDigit`); a 24-hour hour or a minute
        // waits for the second digit.
        if (digits.Length == 2 ||
            (AutoAdvanceSingleDigit && digits.Length == 1 && parsed >= 2 && parsed <= 9))
            RaiseEntryCompleted(isExplicit: false);
    }

    private void RaiseEntryCompleted(bool isExplicit)
    {
        SetTextFromValue(Value);
        EntryCompleted?.Invoke(this, new MaterialTimeEntryEventArgs(isExplicit));
    }

    private void EditorGotFocus(object? sender, RoutedEventArgs e)
    {
        _editor?.SelectAll();
        SelectionRequested?.Invoke(this, EventArgs.Empty);
    }

    private void EditorLostFocus(object? sender, RoutedEventArgs e)
    {
        // The source keeps hourInput/minuteInput drafts independently from the valid hour and
        // minute, so an invalid draft survives focus changes and only an external value update
        // replaces it. A valid draft is merely formatted with its two-digit display text.
        if (IsInvalid)
            return;

        NormalizeText();
    }

    private void EditorKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Up:
                StepBy(Step);
                e.Handled = true;
                break;
            case Key.Down:
                StepBy(-Step);
                e.Handled = true;
                break;
            case Key.Enter:
                if (!IsInvalid)
                {
                    NormalizeText();
                    EntryCompleted?.Invoke(this, new MaterialTimeEntryEventArgs(isExplicit: true));
                }
                e.Handled = true;
                break;
            case Key.Escape:
                CancelRequested?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
                break;
        }
    }

    private void StepBy(int delta)
    {
        var low = Minimum;
        var high = Maximum;
        if (high < low) return;

        var next = Value + delta;
        if (Wrap)
        {
            var span = high - low + 1;
            next = low + ((next - low) % span + span) % span;
        }
        else
        {
            next = Math.Clamp(next, low, high);
        }

        if (next == Value)
        {
            SetTextFromValue(Value);
            return;
        }

        SetCurrentValue(ValueProperty, next);
        IsInvalid = false;
        _editor?.SelectAll();
    }

    private void OnSegmentPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsEffectivelyEnabled) return;
        SelectionRequested?.Invoke(this, EventArgs.Empty);
        if (_editor is not null && !_editor.IsFocused)
            _editor.Focus(NavigationMethod.Pointer);
    }
}

/// <summary>
/// Locale number formatting for the time controls, the Avalonia counterpart of the source's
/// <c>Int.toLocalString</c> (Java <c>NumberFormat</c>, grouping off). Parsing intentionally accepts
/// every Unicode decimal digit, like the source's <c>Char.isDigit</c>/<c>toIntOrNull</c> pair.
/// </summary>
internal static class MaterialTimeNumerals
{
    internal static string Format(int value, int minDigits, CultureInfo culture)
    {
        var ascii = value.ToString("D" + Math.Max(1, minDigits), CultureInfo.InvariantCulture);
        var native = culture.NumberFormat.NativeDigits;
        if (native.Length < 10)
            return ascii;

        var characters = ascii.ToCharArray();
        var changed = false;
        for (var index = 0; index < characters.Length; index++)
        {
            var digit = characters[index] - '0';
            if (digit is < 0 or > 9)
                continue;
            var symbol = native[digit];
            if (symbol.Length != 1 || symbol[0] == characters[index])
                continue;
            characters[index] = symbol[0];
            changed = true;
        }

        return changed ? new string(characters) : ascii;
    }

    internal static bool AllDigits(string text)
    {
        foreach (var character in text)
        {
            if (CharUnicodeInfo.GetDecimalDigitValue(character) < 0)
                return false;
        }

        return true;
    }

    internal static bool TryParse(string text, out int value)
    {
        value = 0;
        if (text.Length == 0)
            return false;

        foreach (var character in text)
        {
            var digit = CharUnicodeInfo.GetDecimalDigitValue(character);
            if (digit < 0 || value > (int.MaxValue - digit) / 10)
                return false;
            value = value * 10 + digit;
        }

        return true;
    }
}
