using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace ExpressiveAvalonia.Controls;

/// <summary>The outcome of a modal date picker session.</summary>
public sealed class MaterialDatePickerResult
{
    public MaterialDatePickerResult(DateTimeOffset? start, DateTimeOffset? end, bool isConfirmed)
    {
        Start = start;
        End = end;
        IsConfirmed = isConfirmed;
    }

    /// <summary>The selected start date, or null when the session was cancelled without a value.</summary>
    public DateTimeOffset? Start { get; }

    /// <summary>The selected range end, or null for single selection and incomplete ranges.</summary>
    public DateTimeOffset? End { get; }

    /// <summary>True when the OK/Save action confirmed the session.</summary>
    public bool IsConfirmed { get; }

    /// <summary>A cancelled result with no selection.</summary>
    public static MaterialDatePickerResult Cancelled { get; } = new(null, null, false);
}

/// <summary>
/// Native modal host for a <see cref="MaterialDatePicker"/>. The picker keeps its own surface;
/// the window supplies modal blocking, owner centering and focus restore.
/// </summary>
/// <remarks>
/// The picker instance is released from the window when the dialog closes so the same control
/// can be shown again or reused elsewhere. The dialog owns the commit boundary: Escape, the
/// platform close gesture and the picker's Cancel all discard the draft and restore the selection
/// captured when the dialog opened; OK and Save confirm the current selection.
/// </remarks>
public sealed class MaterialDatePickerDialog : Window
{
    private readonly MaterialDatePicker _picker;
    private readonly DateTimeOffset? _committedStart;
    private readonly DateTimeOffset? _committedEnd;
    private readonly DateTimeOffset? _committedDisplay;
    private readonly MaterialDatePickerView _committedView;
    private readonly bool _committedInputMode;
    private MaterialDatePickerResult? _result;
    private bool _closed;

    public MaterialDatePickerDialog(MaterialDatePicker picker)
    {
        ArgumentNullException.ThrowIfNull(picker);
        _picker = picker;

        // The picker mutates its selection while the user works, matching the source state. The
        // dialog owns the commit boundary: cancelling restores this snapshot, confirming keeps
        // the live values as the result.
        _committedStart = picker.SelectedDate;
        _committedEnd = picker.SelectedEndDate;
        _committedDisplay = picker.DisplayDate;
        _committedView = picker.View;
        _committedInputMode = picker.IsInputMode;

        Title = picker.EffectiveLabel;
        WindowDecorations = WindowDecorations.BorderOnly;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;
        CanMinimize = false;
        CanMaximize = false;
        ShowInTaskbar = false;
        Padding = new Thickness(0);
        Content = picker;

        picker.Confirmed += OnPickerConfirmed;
        picker.Cancelled += OnPickerCancelled;
        picker.PropertyChanged += OnPickerPropertyChanged;
    }

    /// <summary>The hosted picker.</summary>
    public MaterialDatePicker Picker => _picker;

    /// <summary>
    /// Shows the picker as a modal owned window. The caller owns the picker and may reuse it
    /// after the dialog closes.
    /// </summary>
    /// <exception cref="InvalidOperationException">The picker is already parented elsewhere.</exception>
    public static async Task<MaterialDatePickerResult> ShowAsync(Window owner, MaterialDatePicker picker)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(picker);
        if (picker.Parent is not null)
            throw new InvalidOperationException(
                "The date picker already has a parent. Create a picker for the dialog instead.");

        var dialog = new MaterialDatePickerDialog(picker);
        var result = await dialog.ShowDialog<MaterialDatePickerResult?>(owner);
        return result ?? MaterialDatePickerResult.Cancelled;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _picker.Focus(NavigationMethod.Unspecified);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
            return;
        if (e.Key == Key.Escape)
        {
            CancelDialog();
            e.Handled = true;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _closed = true;
        _picker.Confirmed -= OnPickerConfirmed;
        _picker.Cancelled -= OnPickerCancelled;
        _picker.PropertyChanged -= OnPickerPropertyChanged;
        // Any close that was not a confirmation discards the draft, like the source dialog where
        // the caller simply does not read the state after a cancel.
        if (_result is not { IsConfirmed: true })
            RestoreCommittedState();
        Content = null;
        base.OnClosed(e);
    }

    private void OnPickerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == MaterialDatePicker.EffectiveLabelProperty)
            Title = _picker.EffectiveLabel;
    }

    private void CancelDialog()
    {
        if (_closed || _result is not null)
            return;
        _result = new MaterialDatePickerResult(_committedStart, _committedEnd, isConfirmed: false);
        Close(_result);
    }

    private void RestoreCommittedState()
    {
        // DateRangePickerStateImpl.setSelection validates the pair as a unit. Restoring the
        // endpoints one by one could expose the draft's end while the committed start is being
        // written, which the picker would reject by clearing both.
        _picker.RestoreSelection(_committedStart, _committedEnd);
        _picker.DisplayDate = _committedDisplay;
        _picker.View = _committedView;
        _picker.IsInputMode = _committedInputMode;
        // An IsInputMode transition re-targets the displayed month to the selected date, so the
        // captured month is re-applied after the mode restore when it was altered.
        if (_picker.DisplayDate != _committedDisplay)
            _picker.DisplayDate = _committedDisplay;
    }

    private void OnPickerConfirmed(object? sender, EventArgs e)
    {
        if (_closed || _result is not null)
            return;
        _result = new MaterialDatePickerResult(_picker.SelectedDate, _picker.SelectedEndDate, isConfirmed: true);
        Close(_result);
    }

    private void OnPickerCancelled(object? sender, EventArgs e) => CancelDialog();
}
