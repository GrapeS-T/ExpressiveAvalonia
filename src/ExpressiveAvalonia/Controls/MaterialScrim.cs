using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// The Material scrim: a dimming barrier that covers the content behind a modal surface.
/// </summary>
/// <remarks>
/// <para>
/// The source scrim is a solid <c>M3/sys/light/scrim</c> fill at 32% opacity. The theme sets
/// <see cref="Fill"/> to the scrim role and <see cref="ScrimOpacity"/> to 0.32; the control's
/// own <see cref="Visual.Opacity"/> stays free for the open/close fade, which is driven by the
/// <c>MaterialDurationMedium2</c> resource. Reduced motion zeroes that duration, so the scrim
/// appears and disappears without a fade.
/// </para>
/// <para>
/// Interaction follows standard overlay semantics: while <see cref="IsOpen"/> is true the scrim
/// blocks pointer input, a primary-button release inside it or Escape raises
/// <see cref="DismissRequested"/>, and the scrim closes itself unless a handler marks the event
/// handled. Opening moves focus to the scrim when it is focusable so keyboard users can dismiss
/// it; set <see cref="InputElement.Focusable"/> to false if a host surface owns focus instead.
/// </para>
/// <para>
/// The close fade delays hiding by one duration token. Detaching the control cancels a pending
/// hide, so a recycled scrim never stays visible after being removed from the tree.
/// </para>
/// </remarks>
public sealed class MaterialScrim : Control
{
    /// <summary>Defines the <see cref="Fill"/> property.</summary>
    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<MaterialScrim, IBrush?>(nameof(Fill));

    /// <summary>Defines the <see cref="ScrimOpacity"/> property.</summary>
    public static readonly StyledProperty<double> ScrimOpacityProperty =
        AvaloniaProperty.Register<MaterialScrim, double>(nameof(ScrimOpacity), 0.32,
            validate: value => double.IsFinite(value) && value is >= 0 and <= 1);

    /// <summary>Defines the <see cref="IsOpen"/> property.</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<MaterialScrim, bool>(nameof(IsOpen));

    /// <summary>Defines the <see cref="IsDismissEnabled"/> property.</summary>
    public static readonly StyledProperty<bool> IsDismissEnabledProperty =
        AvaloniaProperty.Register<MaterialScrim, bool>(nameof(IsDismissEnabled), true);

    /// <summary>Identifies the <see cref="DismissRequested"/> routed event.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> DismissRequestedEvent =
        RoutedEvent.Register<MaterialScrim, RoutedEventArgs>(nameof(DismissRequested), RoutingStrategies.Bubble);

    private bool _pressed;
    private int _hideVersion;

    /// <summary>Creates a closed scrim. <see cref="IsOpen"/> drives visibility from here on.</summary>
    public MaterialScrim()
    {
        // A closed scrim must not paint at full alpha before IsOpen is first assigned.
        IsVisible = false;
        Opacity = 0;
    }

    static MaterialScrim()
    {
        AffectsRender<MaterialScrim>(FillProperty, ScrimOpacityProperty);
    }

    /// <summary>Raised when the user asks to dismiss the scrim by pointer or Escape.</summary>
    /// <remarks>Set <see cref="RoutedEventArgs.Handled"/> to keep the scrim open.</remarks>
    public event EventHandler<RoutedEventArgs>? DismissRequested
    {
        add => AddHandler(DismissRequestedEvent, value);
        remove => RemoveHandler(DismissRequestedEvent, value);
    }

    /// <summary>Gets or sets the scrim brush; the theme supplies the scrim role.</summary>
    public IBrush? Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    /// <summary>Gets or sets the alpha applied to <see cref="Fill"/> (0.32 in the source).</summary>
    public double ScrimOpacity
    {
        get => GetValue(ScrimOpacityProperty);
        set => SetValue(ScrimOpacityProperty, value);
    }

    /// <summary>Gets or sets whether the scrim is shown. Changing it runs the open/close lifecycle.</summary>
    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <summary>Gets or sets whether pointer and Escape input may dismiss the scrim.</summary>
    public bool IsDismissEnabled
    {
        get => GetValue(IsDismissEnabledProperty);
        set => SetValue(IsDismissEnabledProperty, value);
    }

    /// <summary>Raises <see cref="DismissRequested"/> and closes the scrim when it is not handled.</summary>
    public void Dismiss()
    {
        var args = new RoutedEventArgs(DismissRequestedEvent, this);
        RaiseEvent(args);
        if (!args.Handled)
            IsOpen = false;
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Fill is null)
        {
            // Keep the open scrim hit-testable: a Control that paints nothing receives no
            // pointer input, which would silently disable click-to-dismiss.
            context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
            return;
        }

        if (ScrimOpacity <= 0)
            return;

        using (context.PushOpacity(ScrimOpacity))
            context.FillRectangle(Fill, new Rect(Bounds.Size));
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOpenProperty)
            UpdateOpenState(change.GetNewValue<bool>());
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!IsDismissEnabled || !IsOpen || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        _pressed = true;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_pressed)
            return;

        _pressed = false;
        e.Pointer.Capture(null);
        if (!IsDismissEnabled || !IsOpen || e.InitialPressMouseButton != MouseButton.Left)
            return;
        if (!new Rect(Bounds.Size).Contains(e.GetPosition(this)))
            return;

        e.Handled = true;
        Dismiss();
    }

    /// <inheritdoc />
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _pressed = false;
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!IsDismissEnabled || !IsOpen || e.Key != Key.Escape)
            return;

        e.Handled = true;
        Dismiss();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // A close that was interrupted by a detach leaves IsVisible true; a re-attached closed
        // scrim must not flash over its host.
        if (IsOpen)
        {
            if (Focusable)
                Focus();
            return;
        }

        _hideVersion++;
        IsVisible = false;
        Opacity = 0;
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // Invalidate any pending hide so a recycled scrim does not get hidden later.
        _hideVersion++;
        _pressed = false;
    }

    private void UpdateOpenState(bool open)
    {
        _hideVersion++;
        if (open)
        {
            IsVisible = true;
            if (MaterialMotion.GetIsReduced(this))
            {
                Opacity = 1;
            }
            else
            {
                // Establish the start value in this frame, then fade in on the next render pass
                // so the opacity transition has a real 0 -> 1 change to animate.
                Opacity = 0;
                Dispatcher.UIThread.Post(
                    () =>
                    {
                        if (IsOpen)
                            Opacity = 1;
                    },
                    DispatcherPriority.Render);
            }

            if (Focusable && this.IsAttachedToVisualTree())
                Focus();
            return;
        }

        if (!IsVisible)
            return;

        Opacity = 0;
        if (MaterialMotion.GetIsReduced(this))
        {
            IsVisible = false;
            return;
        }

        var delay = this.TryFindResource("MaterialDurationMedium2", out var value) && value is TimeSpan span
            ? span
            : TimeSpan.FromMilliseconds(300);
        if (delay <= TimeSpan.Zero)
        {
            IsVisible = false;
            return;
        }

        var version = _hideVersion;
        DispatcherTimer.RunOnce(
            () =>
            {
                if (version == _hideVersion && !IsOpen)
                    IsVisible = false;
            },
            delay,
            DispatcherPriority.Background);
    }
}
