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

    /// <summary>The time when the session ended; unchanged by Cancel.</summary>
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
/// action all produce a non-confirmed result; OK confirms the current time.
/// </remarks>
public sealed class MaterialTimePickerDialog : Window
{
    private readonly MaterialTimePicker _picker;
    private MaterialTimePickerResult? _result;

    public MaterialTimePickerDialog(MaterialTimePicker picker)
    {
        ArgumentNullException.ThrowIfNull(picker);
        _picker = picker;

        Title = picker.EffectiveHeadline ?? string.Empty;
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
            Close(new MaterialTimePickerResult(_picker.Time, false));
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

    private void OnPickerConfirmed(object? sender, MaterialTimeConfirmedEventArgs e)
    {
        if (_result is not null)
            return;
        _result = new MaterialTimePickerResult(e.Time, isConfirmed: true);
        Close(_result);
    }

    private void OnPickerCancelled(object? sender, EventArgs e)
    {
        if (_result is not null)
            return;
        _result = new MaterialTimePickerResult(_picker.Time, isConfirmed: false);
        Close(_result);
    }
}
