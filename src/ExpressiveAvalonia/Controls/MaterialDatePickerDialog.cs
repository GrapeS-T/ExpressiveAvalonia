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
/// can be shown again or reused elsewhere. Escape and the platform close gesture both produce
/// <see cref="MaterialDatePickerResult.Cancelled"/>; OK and Save confirm the current selection.
/// </remarks>
public sealed class MaterialDatePickerDialog : Window
{
    private readonly MaterialDatePicker _picker;
    private MaterialDatePickerResult? _result;

    public MaterialDatePickerDialog(MaterialDatePicker picker)
    {
        ArgumentNullException.ThrowIfNull(picker);
        _picker = picker;

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
            Close(MaterialDatePickerResult.Cancelled);
            e.Handled = true;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _picker.Confirmed -= OnPickerConfirmed;
        _picker.Cancelled -= OnPickerCancelled;
        Content = null;
        base.OnClosed(e);
    }

    private void OnPickerConfirmed(object? sender, EventArgs e)
    {
        if (_result is not null)
            return;
        _result = new MaterialDatePickerResult(_picker.SelectedDate, _picker.SelectedEndDate, isConfirmed: true);
        Close(_result);
    }

    private void OnPickerCancelled(object? sender, EventArgs e)
    {
        if (_result is not null)
            return;
        _result = new MaterialDatePickerResult(_picker.SelectedDate, _picker.SelectedEndDate, isConfirmed: false);
        Close(_result);
    }
}
