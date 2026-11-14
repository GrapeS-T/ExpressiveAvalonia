using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace ExpressiveAvalonia.Controls;

/// <summary>The outcome of a modal time picker session.</summary>
public sealed class MaterialTimePickerResult
{
    public MaterialTimePickerResult(TimeSpan time, bool isConfirmed)
    {
        Time = time;
        IsConfirmed = isConfirmed;
    }

    /// <summary>The confirmed time, or the value captured when a non-confirmed session opened.</summary>
    public TimeSpan Time { get; }

    /// <summary>True when the OK action confirmed the session.</summary>
    public bool IsConfirmed { get; }

    /// <summary>A cancelled result at midnight, used when the window closed without a result.</summary>
    public static MaterialTimePickerResult Cancelled { get; } = new(TimeSpan.Zero, false);
}

/// <summary>
/// Native modal host for a <see cref="MaterialTimePicker"/>. The picker keeps its own surface;
/// the window supplies modal blocking, owner centering and focus restore.
/// </summary>
/// <remarks>
/// The picker instance is released from the window when the dialog closes so the same control can
/// be shown again or reused elsewhere. Escape, the platform close gesture and the picker's Cancel
/// action all discard the draft, restore the time and input mode captured at open, and produce a
/// non-confirmed result; OK confirms the current time.
/// </remarks>
public sealed class MaterialTimePickerDialog : Window
{
    private readonly MaterialTimePicker _picker;
    private readonly TimeSpan _committedTime;
    private readonly MaterialTimeInputMode _committedMode;
    private MaterialTimePickerResult? _result;
    private bool _closed;

    public MaterialTimePickerDialog(MaterialTimePicker picker)
    {
        ArgumentNullException.ThrowIfNull(picker);
        _picker = picker;

        // The picker mutates Time while the user turns the dial, as the source state does. The
        // dialog supplies the commit boundary: a non-confirmed close restores this snapshot.
        _committedTime = picker.Time;
        _committedMode = picker.InputMode;

        Title = picker.Variant == MaterialTimePickerVariant.Standard
            ? picker.EffectiveHeadline ?? string.Empty
            : string.Empty;
        MinWidth = 328;
        MaxWidth = 600;
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

    private void OnPickerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == MaterialTimePicker.EffectiveHeadlineProperty)
            Title = _picker.Variant == MaterialTimePickerVariant.Standard
                ? _picker.EffectiveHeadline ?? string.Empty
                : string.Empty;
    }

    /// <summary>The hosted picker.</summary>
    public MaterialTimePicker Picker => _picker;

    /// <summary>
    /// Shows the picker as a modal owned window. The caller owns the picker and may reuse it after
    /// the dialog closes.
    /// </summary>
    /// <exception cref="InvalidOperationException">The picker is already parented elsewhere.</exception>
    public static async System.Threading.Tasks.Task<MaterialTimePickerResult> ShowAsync(
        Window owner, MaterialTimePicker picker)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(picker);
        if (picker.Parent is not null)
            throw new InvalidOperationException(
                "The time picker already has a parent. Create a picker for the dialog instead.");

        var dialog = new MaterialTimePickerDialog(picker);
        var result = await dialog.ShowDialog<MaterialTimePickerResult?>(owner);
        return result ?? new MaterialTimePickerResult(picker.Time, false);
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _picker.FocusFirstField();
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
        if (_result is not { IsConfirmed: true })
        {
            _picker.Time = _committedTime;
            _picker.InputMode = _committedMode;
            // Time may already equal the snapshot while an invalid draft is pending; rebuild the
            // cells explicitly so a cancelled session never leaves "27" (or empty text) behind.
            _picker.ResetDrafts();
        }
        Content = null;
        base.OnClosed(e);
    }

    private void CancelDialog()
    {
        if (_closed || _result is not null)
            return;
        _result = new MaterialTimePickerResult(_committedTime, isConfirmed: false);
        Close(_result);
    }

    private void OnPickerConfirmed(object? sender, MaterialTimeConfirmedEventArgs e)
    {
        if (_closed || _result is not null)
            return;
        _result = new MaterialTimePickerResult(e.Time, isConfirmed: true);
        Close(_result);
    }

    private void OnPickerCancelled(object? sender, EventArgs e) => CancelDialog();
}
