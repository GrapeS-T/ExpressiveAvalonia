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
/// screen-reader text editing all remain Avalonia's. The cell only adds time semantics: digits
/// are filtered, the value is range-checked against <see cref="Minimum"/>/<see cref="Maximum"/>,
/// out-of-range text raises the <c>:invalid</c> pseudo-class instead of silently clamping, and a
/// complete entry auto-advances.
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

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<MaterialTimeInputSegment, string?>(nameof(Label));

    public static readonly StyledProperty<string?> HelpTextProperty =
        AvaloniaProperty.Register<MaterialTimeInputSegment, string?>(nameof(HelpText));

    public static readonly StyledProperty<int> StepProperty =
        AvaloniaProperty.Register<MaterialTimeInputSegment, int>(nameof(Step), 1,
            validate: value => value > 0);

    public static readonly StyledProperty<bool> WrapProperty =
        AvaloniaProperty.Register<MaterialTimeInputSegment, bool>(nameof(Wrap), true);

    private TextBox? _editor;
    private bool _updatingText;
    private bool _updatingValue;

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
            _editor.TextChanged -= EditorTextChanged;
            _editor.GotFocus -= EditorGotFocus;
            _editor.LostFocus -= EditorLostFocus;
            _editor.KeyDown -= EditorKeyDown;
        }

        base.OnApplyTemplate(e);
        _editor = e.NameScope.Find<TextBox>("PART_Editor");
        if (_editor is not null)
        {
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
        var initial = string.IsNullOrEmpty(Text) ? Value.ToString("D2", CultureInfo.InvariantCulture) : Text;
        _updatingText = true;
        SetCurrentValue(TextProperty, initial);
        if (_editor is not null) _editor.Text = initial;
        _updatingText = false;
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

    private void SetTextFromValue(int value)
    {
        var text = value.ToString("D2", CultureInfo.InvariantCulture);
        _updatingText = true;
        if (!string.Equals(Text, text, StringComparison.Ordinal))
            SetCurrentValue(TextProperty, text);
        if (_editor is not null && !string.Equals(_editor.Text, text, StringComparison.Ordinal))
        {
            _editor.Text = text;
            _editor.CaretIndex = text.Length;
        }
        _updatingText = false;
    }

    private void EditorTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_updatingText || _editor is null) return;
        var raw = _editor.Text ?? string.Empty;
        var digits = Sanitize(raw);

        _updatingText = true;
        SetCurrentValue(TextProperty, digits);
        if (!string.Equals(digits, raw, StringComparison.Ordinal))
        {
            _editor.Text = digits;
            _editor.CaretIndex = digits.Length;
        }
        _updatingText = false;

        ApplyTypedText();
    }

    private static string Sanitize(string raw)
    {
        Span<char> buffer = stackalloc char[2];
        var length = 0;
        foreach (var c in raw)
        {
            if (!char.IsAsciiDigit(c)) continue;
            if (length == buffer.Length) break;
            buffer[length++] = c;
        }

        return new string(buffer[..length]);
    }

    private void ApplyTypedText()
    {
        var digits = Sanitize(Text ?? string.Empty);
        if (digits.Length == 0)
        {
            IsInvalid = true;
            return;
        }

        if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
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

        // Two digits complete a value; a single digit completes it as soon as appending a digit
        // could no longer fit the accepted range (for example 9 in a 1-12 hour cell).
        if (digits.Length == 2 || parsed * 10 > Maximum)
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
        // Leaving the cell restores the last accepted value; an incomplete entry is discarded
        // rather than committed.
        if (IsInvalid || string.IsNullOrEmpty(Text))
        {
            IsInvalid = false;
            SetTextFromValue(Value);
            return;
        }

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
                if (IsInvalid)
                {
                    NormalizeText();
                }
                else
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
