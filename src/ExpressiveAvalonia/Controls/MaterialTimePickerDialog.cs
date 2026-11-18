using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

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
/// the window supplies modal blocking, scrim light-dismiss, owner centering and focus restore.
/// </summary>
/// <remarks>
/// The picker instance is released from the window when the dialog closes so the same control can
/// be shown again or reused elsewhere. Escape, scrim click, the platform close gesture and the
/// picker's Cancel action all discard the draft, restore the time and input mode captured at open,
/// and produce a non-confirmed result; OK confirms the current time.
/// </remarks>
public sealed class MaterialTimePickerDialog : Window
{
    private const double ShadowMargin = 16;
    private const double ModalMargin = 24;

    private readonly MaterialTimePicker _picker;
    private readonly Panel _root;
    private readonly Border _scrim;
    private readonly TimeSpan _committedTime;
    private readonly MaterialTimeInputMode _committedMode;
    private readonly Thickness _savedMargin;
    private readonly HorizontalAlignment _savedHorizontalAlignment;
    private readonly VerticalAlignment _savedVerticalAlignment;
    private readonly double _savedMaxWidth;
    private Window? _ownerWindow;
    private IInputElement? _previousFocus;
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
        _savedMargin = picker.Margin;
        _savedHorizontalAlignment = picker.HorizontalAlignment;
        _savedVerticalAlignment = picker.VerticalAlignment;
        _savedMaxWidth = picker.MaxWidth;

        Title = ResolveTitle(picker);
        WindowDecorations = WindowDecorations.None;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Background = Brushes.Transparent;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;
        CanMinimize = false;
        CanMaximize = false;
        ShowInTaskbar = false;
        Padding = new Thickness(0);

        _scrim = new Border
        {
            IsVisible = false,
            IsHitTestVisible = true,
        };
        _scrim.PointerPressed += OnScrimPointerPressed;

        _root = new Panel { ClipToBounds = false };
        KeyboardNavigation.SetTabNavigation(_root, KeyboardNavigationMode.Cycle);
        _root.Children.Add(_scrim);
        _root.Children.Add(picker);
        Content = _root;

        ApplyPresentation(owner: null);

        picker.Confirmed += OnPickerConfirmed;
        picker.Cancelled += OnPickerCancelled;
        picker.PropertyChanged += OnPickerPropertyChanged;
    }

    private void OnPickerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == MaterialTimePicker.EffectiveHeadlineProperty ||
            e.Property == MaterialTimePicker.HeadlineProperty ||
            e.Property == MaterialTimePicker.VariantProperty)
        {
            Title = ResolveTitle(_picker);
        }
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
        dialog.AttachOwner(owner);
        var result = await dialog.ShowDialog<MaterialTimePickerResult?>(owner);
        return result ?? new MaterialTimePickerResult(picker.Time, false);
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (_ownerWindow is null && Owner is Window owner)
            AttachOwner(owner);
        else
            ApplyPresentation(_ownerWindow);
        Title = ResolveTitle(_picker);
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
        _scrim.PointerPressed -= OnScrimPointerPressed;
        if (_ownerWindow is not null)
            _ownerWindow.PropertyChanged -= OnOwnerPropertyChanged;

        if (_result is not { IsConfirmed: true })
        {
            _picker.Time = _committedTime;
            _picker.InputMode = _committedMode;
            // Time may already equal the snapshot while an invalid draft is pending; rebuild the
            // cells explicitly so a cancelled session never leaves "27" (or empty text) behind.
            _picker.ResetDrafts();
        }

        _root.Children.Clear();
        _picker.Margin = _savedMargin;
        _picker.HorizontalAlignment = _savedHorizontalAlignment;
        _picker.VerticalAlignment = _savedVerticalAlignment;
        _picker.MaxWidth = _savedMaxWidth;
        Content = null;
        base.OnClosed(e);

        if (_ownerWindow is { IsVisible: true } &&
            _previousFocus is Control { IsEffectivelyVisible: true, IsEffectivelyEnabled: true } target)
        {
            target.Focus();
        }
        _ownerWindow = null;
        _previousFocus = null;
    }

    private void AttachOwner(Window owner)
    {
        if (ReferenceEquals(_ownerWindow, owner))
            return;
        if (_ownerWindow is not null)
            _ownerWindow.PropertyChanged -= OnOwnerPropertyChanged;
        _ownerWindow = owner;
        _previousFocus ??= owner.FocusManager?.GetFocusedElement();
        owner.PropertyChanged += OnOwnerPropertyChanged;
        ApplyPresentation(owner);
    }

    private void OnOwnerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ClientSizeProperty && !_closed)
            ApplyPresentation(_ownerWindow);
    }

    private void ApplyPresentation(Window? owner)
    {
        if (double.IsPositiveInfinity(_savedMaxWidth))
            _picker.MaxWidth = 600;

        if (owner is { ClientSize: { Width: > 0, Height: > 0 } size })
        {
            SizeToContent = SizeToContent.Manual;
            Width = size.Width;
            Height = size.Height;
            _scrim.Background = ResolveScrimBrush();
            _scrim.Opacity = ResolveScrimOpacity();
            _scrim.IsVisible = true;
            _picker.HorizontalAlignment = HorizontalAlignment.Center;
            _picker.VerticalAlignment = VerticalAlignment.Center;
            _picker.Margin = new Thickness(ModalMargin);
            return;
        }

        _scrim.IsVisible = false;
        ClearValue(WidthProperty);
        ClearValue(HeightProperty);
        SizeToContent = SizeToContent.WidthAndHeight;
        _picker.HorizontalAlignment = HorizontalAlignment.Center;
        _picker.VerticalAlignment = VerticalAlignment.Center;
        _picker.Margin = new Thickness(ShadowMargin);
    }

    private IBrush ResolveScrimBrush() =>
        this.TryFindResource("MaterialScrimBrush", out var value) && value is IBrush brush
            ? brush
            : (_picker.TryFindResource("MaterialScrimBrush", out var pickerValue) && pickerValue is IBrush pickerBrush
                ? pickerBrush
                : Brushes.Black);

    private double ResolveScrimOpacity() =>
        this.TryFindResource("MaterialDialogScrimOpacity", out var value) && value is double opacity
            ? opacity
            : (_picker.TryFindResource("MaterialDialogScrimOpacity", out var pickerValue) && pickerValue is double pickerOpacity
                ? pickerOpacity
                : 0.32);

    private static string ResolveTitle(MaterialTimePicker picker)
    {
        if (picker.EffectiveHeadline is { } effectiveHeadline && !string.IsNullOrWhiteSpace(effectiveHeadline))
            return effectiveHeadline;
        if (picker.Headline is { } headline && !string.IsNullOrWhiteSpace(headline))
            return headline;
        var key = picker.InputMode == MaterialTimeInputMode.Keyboard
            ? "MaterialTimePickerEnterTimeText"
            : "MaterialTimePickerSelectTimeText";
        if (picker.TryFindResource(key, out var value) && value is string text && !string.IsNullOrWhiteSpace(text))
            return text;
        return picker.InputMode == MaterialTimeInputMode.Keyboard ? "Enter time" : "Select time";
    }

    private void OnScrimPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!ReferenceEquals(e.Source, _scrim))
            return;
        e.Handled = true;
        CancelDialog();
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
