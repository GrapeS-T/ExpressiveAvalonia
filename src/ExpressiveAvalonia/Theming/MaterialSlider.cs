using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using ExpressiveAvalonia.Controls;
using Avalonia.Threading;

namespace ExpressiveAvalonia.Theming;

/// <summary>Presentation options shared by the standard and range sliders.</summary>
/// <remarks>
/// The slider anatomy follows the source tokens: five sizes, a 4-DIP handle that grows to the
/// control's cross size, a rounded track with 2-DIP inside corners, stop indicators and a value
/// indicator pill. Sizes reuse <see cref="MaterialAssist.ControlSize"/> so a slider matches the
/// surrounding controls; the source's XSmall maps to <see cref="MaterialControlSize.ExtraSmall"/>.
/// </remarks>
public sealed class MaterialSlider : AvaloniaObject
{
    private MaterialSlider() { }

    /// <summary>Whether pressing or keyboard-focusing a handle shows the value indicator pill.</summary>
    public static readonly AttachedProperty<bool> ShowValueIndicatorProperty =
        AvaloniaProperty.RegisterAttached<MaterialSlider, Control, bool>("ShowValueIndicator");

    /// <summary>
    /// Shows stop indicators at the values selected by TickFrequency.
    /// Enable snapping with the same frequency so every indicator is selectable.
    /// </summary>
    public static readonly AttachedProperty<bool> ShowStopsProperty =
        AvaloniaProperty.RegisterAttached<MaterialSlider, Control, bool>("ShowStops");

    /// <summary>
    /// Centers the fill on the track instead of filling from the minimum end. Use a symmetric
    /// range (for example -100 to 100) so the track midpoint is the zero value.
    /// </summary>
    public static readonly AttachedProperty<bool> IsCenteredProperty =
        AvaloniaProperty.RegisterAttached<MaterialSlider, Control, bool>("IsCentered");

    /// <summary>Shows an inset icon on standard sliders of size Medium, Large or ExtraLarge.</summary>
    public static readonly AttachedProperty<bool> ShowIconProperty =
        AvaloniaProperty.RegisterAttached<MaterialSlider, Control, bool>("ShowIcon");

    /// <summary>
    /// Geometry for the leading icon. When unset the source's default icon (volume_up, 24 DIP)
    /// is used. The glyph moves to the inactive track when the active track is too short.
    /// </summary>
    public static readonly AttachedProperty<Geometry?> IconProperty =
        AvaloniaProperty.RegisterAttached<MaterialSlider, Control, Geometry?>("Icon");

    /// <summary>Format string for the value indicator; defaults to the source's compact "G4".</summary>
    public static readonly AttachedProperty<string?> ValueFormatProperty =
        AvaloniaProperty.RegisterAttached<MaterialSlider, Control, string?>("ValueFormat");

    // SliderTokens. The source's default icon instance is volume_up (local-005657).
    private static readonly Lazy<Geometry> DefaultIcon = new(() => Geometry.Parse(
        "M14 20.725V18.675C15.5 18.2416 16.7083 17.4083 17.625 16.175C18.5417 14.9416 19 13.5416 " +
        "19 11.975C19 10.4083 18.5417 9.00831 17.625 7.77498C16.7083 6.54164 15.5 5.70831 14 " +
        "5.27498V3.22498C16.0667 3.69164 17.75 4.73748 19.05 6.36248C20.35 7.98748 21 9.85831 21 " +
        "11.975C21 14.0916 20.35 15.9625 19.05 17.5875C17.75 19.2125 16.0667 20.2583 14 20.725ZM3 " +
        "15V8.99998H7L12 3.99998V20L7 15H3ZM14 16V7.94998C14.7833 8.31664 15.3958 8.86664 15.8375 " +
        "9.59998C16.2792 10.3333 16.5 11.1333 16.5 12C16.5 12.85 16.2792 13.6375 15.8375 14.3625C15.3958 " +
        "15.0875 14.7833 15.6333 14 16ZM10 8.84998L7.85 11H5V13H7.85L10 15.15V8.84998Z"));

    public static bool GetShowValueIndicator(Control control) => control.GetValue(ShowValueIndicatorProperty);
    public static void SetShowValueIndicator(Control control, bool value) => control.SetValue(ShowValueIndicatorProperty, value);

    public static bool GetShowStops(Control control) => control.GetValue(ShowStopsProperty);
    public static void SetShowStops(Control control, bool value) => control.SetValue(ShowStopsProperty, value);

    public static bool GetIsCentered(Control control) => control.GetValue(IsCenteredProperty);
    public static void SetIsCentered(Control control, bool value) => control.SetValue(IsCenteredProperty, value);

    public static bool GetShowIcon(Control control) => control.GetValue(ShowIconProperty);
    public static void SetShowIcon(Control control, bool value) => control.SetValue(ShowIconProperty, value);

    public static Geometry? GetIcon(Control control) => control.GetValue(IconProperty);
    public static void SetIcon(Control control, Geometry? value) => control.SetValue(IconProperty, value);

    public static string? GetValueFormat(Control control) => control.GetValue(ValueFormatProperty);
    public static void SetValueFormat(Control control, string? value) => control.SetValue(ValueFormatProperty, value);

    internal static Geometry GetDisplayedIcon(Control control) => GetIcon(control) ?? DefaultIcon.Value;

    // TrackThickness / HandleLength / outer corner radius, in DIP.
    internal static (double Thickness, double Length, double Radius) Metrics(MaterialControlSize size) => size switch
    {
        MaterialControlSize.ExtraSmall => (16, 44, 8),
        MaterialControlSize.Small => (24, 44, 8),
        MaterialControlSize.Medium => (40, 52, 12),
        MaterialControlSize.Large => (56, 68, 16),
        MaterialControlSize.ExtraLarge => (96, 108, 28),
        _ => (40, 52, 12),
    };

    internal static double Fraction(double value, double minimum, double maximum) => maximum > minimum
        // Halving first keeps the fraction finite for opposite extreme bounds.
        ? Math.Clamp((value / 2 - minimum / 2) / (maximum / 2 - minimum / 2), 0, 1)
        : 0;

    /// <summary>
    /// Maps a value fraction to the handle center along the track. The handle center is inset
    /// by six DIP, matching the source's 4-DIP handle plus its clear space.
    /// </summary>
    internal static double Position(double fraction, double length, Orientation orientation, bool reversed)
    {
        var travel = Math.Max(0, length - 12);
        var directed = orientation == Orientation.Horizontal
            ? reversed ? 1 - fraction : fraction
            : reversed ? fraction : 1 - fraction;
        return 6 + directed * travel;
    }

    internal static string FormatValue(double value, Control owner)
    {
        var format = GetValueFormat(owner);
        return string.IsNullOrEmpty(format)
            ? value.ToString("G4", CultureInfo.CurrentCulture)
            : value.ToString(format, CultureInfo.CurrentCulture);
    }
}

/// <summary>Template part of the slider themes; not part of the consumer API.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class MaterialSliderThumb : Thumb
{
    public static readonly StyledProperty<bool> IsDraggingProperty =
        AvaloniaProperty.Register<MaterialSliderThumb, bool>(nameof(IsDragging));

    public bool IsDragging { get => GetValue(IsDraggingProperty); private set => SetValue(IsDraggingProperty, value); }

    protected override Type StyleKeyOverride => typeof(Thumb);

    protected override void OnDragStarted(Avalonia.Input.VectorEventArgs e)
    {
        IsDragging = true;
        base.OnDragStarted(e);
    }

    protected override void OnDragCompleted(Avalonia.Input.VectorEventArgs e)
    {
        IsDragging = false;
        base.OnDragCompleted(e);
    }
}

/// <summary>
/// Draws the slider anatomy: rounded track segments, stop indicators, the handle, the leading
/// icon and the value indicator pill. The native <see cref="Track"/> above it keeps pointer,
/// keyboard, snapping and automation behavior.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class MaterialSliderTrack : Control
{
    public static readonly StyledProperty<Control?> OwnerProperty =
        AvaloniaProperty.Register<MaterialSliderTrack, Control?>(nameof(Owner));
    public static readonly StyledProperty<Thumb?> ThumbProperty =
        AvaloniaProperty.Register<MaterialSliderTrack, Thumb?>(nameof(Thumb));
    public static readonly StyledProperty<IBrush?> ActiveBrushProperty =
        AvaloniaProperty.Register<MaterialSliderTrack, IBrush?>(nameof(ActiveBrush));
    public static readonly StyledProperty<IBrush?> InactiveBrushProperty =
        AvaloniaProperty.Register<MaterialSliderTrack, IBrush?>(nameof(InactiveBrush));
    public static readonly StyledProperty<IBrush?> DisabledBrushProperty =
        AvaloniaProperty.Register<MaterialSliderTrack, IBrush?>(nameof(DisabledBrush));
    public static readonly StyledProperty<IBrush?> StopBrushProperty =
        AvaloniaProperty.Register<MaterialSliderTrack, IBrush?>(nameof(StopBrush));
    public static readonly StyledProperty<IBrush?> SelectedStopBrushProperty =
        AvaloniaProperty.Register<MaterialSliderTrack, IBrush?>(nameof(SelectedStopBrush));
    public static readonly StyledProperty<IBrush?> IndicatorBackgroundProperty =
        AvaloniaProperty.Register<MaterialSliderTrack, IBrush?>(nameof(IndicatorBackground));
    public static readonly StyledProperty<IBrush?> IndicatorForegroundProperty =
        AvaloniaProperty.Register<MaterialSliderTrack, IBrush?>(nameof(IndicatorForeground));
    public static readonly StyledProperty<IBrush?> IconBrushProperty =
        AvaloniaProperty.Register<MaterialSliderTrack, IBrush?>(nameof(IconBrush));
    public static readonly StyledProperty<IBrush?> FocusBrushProperty =
        AvaloniaProperty.Register<MaterialSliderTrack, IBrush?>(nameof(FocusBrush));
    public static readonly StyledProperty<bool> IsDraggingProperty =
        AvaloniaProperty.Register<MaterialSliderTrack, bool>(nameof(IsDragging));

    private readonly Pen _focusPen = new() { Thickness = 3 };
    private Control? _subscribedOwner;
    private Thumb? _subscribedThumb;
    private AvaloniaList<double>? _ticks;
    private bool _ownerPressed;
    private bool _ownerFocusVisible;
    private Popup? _indicatorPopup;
    private TextBlock? _indicatorText;
    private Border? _indicatorBorder;
    private bool _indicatorUpdateQueued;
    private Rect? _indicatorAnchor;
    private string? _indicatorValue;
    private bool _indicatorVertical;

    public Control? Owner { get => GetValue(OwnerProperty); set => SetValue(OwnerProperty, value); }
    public Thumb? Thumb { get => GetValue(ThumbProperty); set => SetValue(ThumbProperty, value); }
    public IBrush? ActiveBrush { get => GetValue(ActiveBrushProperty); set => SetValue(ActiveBrushProperty, value); }
    public IBrush? InactiveBrush { get => GetValue(InactiveBrushProperty); set => SetValue(InactiveBrushProperty, value); }
    public IBrush? DisabledBrush { get => GetValue(DisabledBrushProperty); set => SetValue(DisabledBrushProperty, value); }
    public IBrush? StopBrush { get => GetValue(StopBrushProperty); set => SetValue(StopBrushProperty, value); }
    public IBrush? SelectedStopBrush { get => GetValue(SelectedStopBrushProperty); set => SetValue(SelectedStopBrushProperty, value); }
    public IBrush? FocusBrush { get => GetValue(FocusBrushProperty); set => SetValue(FocusBrushProperty, value); }
    public IBrush? IndicatorBackground { get => GetValue(IndicatorBackgroundProperty); set => SetValue(IndicatorBackgroundProperty, value); }
    public IBrush? IndicatorForeground { get => GetValue(IndicatorForegroundProperty); set => SetValue(IndicatorForegroundProperty, value); }
    public IBrush? IconBrush { get => GetValue(IconBrushProperty); set => SetValue(IconBrushProperty, value); }
    public bool IsDragging { get => GetValue(IsDraggingProperty); set => SetValue(IsDraggingProperty, value); }

    static MaterialSliderTrack() => AffectsRender<MaterialSliderTrack>(OwnerProperty, ThumbProperty,
        ActiveBrushProperty, InactiveBrushProperty, DisabledBrushProperty, StopBrushProperty,
        SelectedStopBrushProperty, IndicatorBackgroundProperty, IndicatorForegroundProperty,
        IconBrushProperty, FocusBrushProperty, IsEnabledProperty, IsDraggingProperty);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == OwnerProperty)
            SubscribeOwner();
        else if (change.Property == ThumbProperty)
            SubscribeThumb();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        SubscribeOwner();
        SubscribeThumb();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        UnsubscribeOwner();
        _indicatorAnchor = null;
        if (_indicatorPopup is not null)
            _indicatorPopup.IsOpen = false;
        if (_subscribedThumb is not null)
            _subscribedThumb.PropertyChanged -= ThumbChanged;
        _subscribedThumb = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void SubscribeOwner()
    {
        UnsubscribeOwner();
        _subscribedOwner = Owner;
        if (_subscribedOwner is not null)
        {
            _subscribedOwner.PropertyChanged += OwnerChanged;
            _subscribedOwner.AddHandler(InputElement.PointerPressedEvent, OwnerPointerPressed, RoutingStrategies.Tunnel);
            _subscribedOwner.AddHandler(InputElement.PointerReleasedEvent, OwnerPointerReleased, RoutingStrategies.Tunnel);
            _subscribedOwner.AddHandler(InputElement.PointerCaptureLostEvent, OwnerPointerCaptureLost, RoutingStrategies.Tunnel);
            _subscribedOwner.AddHandler(InputElement.GotFocusEvent, OwnerGotFocus, RoutingStrategies.Bubble);
            _subscribedOwner.AddHandler(InputElement.LostFocusEvent, OwnerLostFocus, RoutingStrategies.Bubble);
        }
        SubscribeTicks();
    }

    private void UnsubscribeOwner()
    {
        if (_subscribedOwner is not null)
        {
            _subscribedOwner.PropertyChanged -= OwnerChanged;
            _subscribedOwner.RemoveHandler(InputElement.PointerPressedEvent, OwnerPointerPressed);
            _subscribedOwner.RemoveHandler(InputElement.PointerReleasedEvent, OwnerPointerReleased);
            _subscribedOwner.RemoveHandler(InputElement.PointerCaptureLostEvent, OwnerPointerCaptureLost);
            _subscribedOwner.RemoveHandler(InputElement.GotFocusEvent, OwnerGotFocus);
            _subscribedOwner.RemoveHandler(InputElement.LostFocusEvent, OwnerLostFocus);
        }
        _subscribedOwner = null;
        _ownerPressed = false;
        _ownerFocusVisible = false;
        if (_ticks is not null)
            _ticks.CollectionChanged -= TicksChanged;
        _ticks = null;
    }

    private void OwnerPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(_subscribedOwner).Properties.IsLeftButtonPressed)
            return;
        _ownerPressed = true;
        InvalidateVisual();
    }

    private void OwnerPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_ownerPressed)
            return;
        _ownerPressed = false;
        InvalidateVisual();
    }

    private void OwnerPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (!_ownerPressed)
            return;
        _ownerPressed = false;
        InvalidateVisual();
    }

    private void OwnerGotFocus(object? sender, FocusChangedEventArgs e)
    {
        // Window activation restores focus with Unspecified, not keyboard navigation.
        _ownerFocusVisible = e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional;
        InvalidateVisual();
    }

    private void OwnerLostFocus(object? sender, FocusChangedEventArgs e)
    {
        _ownerFocusVisible = false;
        InvalidateVisual();
    }

    private void SubscribeThumb()
    {
        if (_subscribedThumb is not null)
            _subscribedThumb.PropertyChanged -= ThumbChanged;
        _subscribedThumb = Thumb;
        if (_subscribedThumb is not null)
            _subscribedThumb.PropertyChanged += ThumbChanged;
        InvalidateVisual();
    }

    private void ThumbChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == IsPointerOverProperty || e.Property == IsFocusedProperty)
            InvalidateVisual();
    }

    private void SubscribeTicks()
    {
        if (_ticks is not null)
            _ticks.CollectionChanged -= TicksChanged;
        _ticks = (_subscribedOwner as Slider)?.Ticks;
        if (_ticks is not null)
            _ticks.CollectionChanged += TicksChanged;
    }

    private void OwnerChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Slider.TicksProperty)
            SubscribeTicks();
        InvalidateVisual();
    }

    private void TicksChanged(object? sender, NotifyCollectionChangedEventArgs e) => InvalidateVisual();
    private void QueueIndicator(Rect? anchor, string? value, bool vertical)
    {
        _indicatorAnchor = anchor;
        _indicatorValue = value;
        _indicatorVertical = vertical;
        if (_indicatorUpdateQueued)
            return;
        _indicatorUpdateQueued = true;
        // Popup layout must not mutate the visual tree during Render.
        Dispatcher.UIThread.Post(UpdateIndicator);
    }

    private void UpdateIndicator()
    {
        _indicatorUpdateQueued = false;
        if (_indicatorAnchor is not { } anchor || TopLevel.GetTopLevel(this) is null ||
            Owner?.IsEffectivelyEnabled != true || IndicatorBackground is null || IndicatorForeground is null)
        {
            if (_indicatorPopup is not null)
                _indicatorPopup.IsOpen = false;
            return;
        }
        if (_indicatorPopup is null)
        {
            _indicatorText = new TextBlock
            {
                FontSize = 14, FontWeight = FontWeight.Normal, LineHeight = 20,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _indicatorBorder = new Border
            {
                MinWidth = 48, Height = 44, CornerRadius = new CornerRadius(22),
                Padding = new Thickness(16, 0), Child = _indicatorText
            };
            _indicatorPopup = new Popup
            {
                PlacementTarget = this, Placement = PlacementMode.AnchorAndGravity,
                IsHitTestVisible = false, IsLightDismissEnabled = false,
                WindowManagerAddShadowHint = false, Child = _indicatorBorder
            };
            _indicatorPopup.Opened += (_, _) =>
            {
                if (TopLevel.GetTopLevel(_indicatorBorder) is PopupRoot root)
                {
                    root.Background = Brushes.Transparent;
                    root.TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
                }
            };
        }
        _indicatorText!.Text = _indicatorValue;
        _indicatorText.Foreground = IndicatorForeground;
        _indicatorText.FontFamily = (Owner as TemplatedControl)?.FontFamily ?? FontFamily.Default;
        _indicatorBorder!.Background = IndicatorBackground;
        _indicatorPopup.PlacementRect = anchor;
        var (_, handleLength, _) = MaterialSlider.Metrics(MaterialAssist.GetControlSize(Owner!));
        var cross = _indicatorVertical ? Bounds.Width : Bounds.Height;
        handleLength = Math.Min(handleLength, Math.Max(4, cross));
        var offset = cross / 2 - handleLength / 2 - 12;
        _indicatorPopup.HorizontalOffset = _indicatorVertical ? offset : 0;
        _indicatorPopup.VerticalOffset = _indicatorVertical ? 0 : offset;
        _indicatorPopup.PlacementAnchor = _indicatorVertical
            ? Avalonia.Controls.Primitives.PopupPositioning.PopupAnchor.Left
            : Avalonia.Controls.Primitives.PopupPositioning.PopupAnchor.Top;
        _indicatorPopup.PlacementGravity = _indicatorVertical
            ? Avalonia.Controls.Primitives.PopupPositioning.PopupGravity.Left
            : Avalonia.Controls.Primitives.PopupPositioning.PopupGravity.Top;
        _indicatorPopup.IsOpen = true;
    }


    public override void Render(DrawingContext context)
    {
        base.Render(context);
        QueueIndicator(null, null, false);
        if (Owner is not { } owner)
            return;
        var slider = owner as Slider;
        var range = owner as MaterialRangeSlider;
        if (slider is null && range is null)
            return;

        var vertical = slider?.Orientation == Orientation.Vertical;
        var length = vertical ? Bounds.Height : Bounds.Width;
        var cross = vertical ? Bounds.Width : Bounds.Height;
        if (!(length > 24) || !(cross > 8))
            return;

        var size = MaterialAssist.GetControlSize(owner);
        var (thickness, handleLength, outerRadius) = MaterialSlider.Metrics(size);
        thickness = Math.Min(thickness, Math.Max(4, cross - 2));
        outerRadius = Math.Min(outerRadius, thickness / 2);
        handleLength = Math.Min(handleLength, Math.Max(4, cross));

        var crossCenter = cross / 2;
        var enabled = owner.IsEffectivelyEnabled;
        // Focus ring: keyboard focus only (pointer focus stays clean), and only while focus is
        // still inside the slider - a thumb may hold focus while the slider itself is unfocused.
        var focusVisible = _ownerFocusVisible && owner.IsKeyboardFocusWithin;
        var reversed = slider?.IsDirectionReversed == true;
        var centered = MaterialSlider.GetIsCentered(owner);
        var minimum = slider?.Minimum ?? range!.Minimum;
        var maximum = slider?.Maximum ?? range!.Maximum;
        if (!(maximum > minimum))
            return;

        void Segment(double start, double end, bool active)
        {
            var extent = end - start;
            if (!(extent > 0.01))
                return;
            // Only actual track endpoints get the outer radius. A handle-facing edge must
            // stay full-width even when the handle is inside the outer corner's former bounds.
            var startRadius = start <= 0 ? outerRadius : 2;
            var endRadius = end >= length ? outerRadius : 2;
            // Extend short segments before rounding, then crop them at the handle.
            // The outer circular arc must never be squeezed into an ellipse.
            var drawingStart = start;
            var drawingEnd = end;
            if (start <= 0)
                drawingEnd = Math.Max(end, start + startRadius + endRadius);
            else if (end >= length)
                drawingStart = Math.Min(start, end - startRadius - endRadius);
            var rect = vertical
                ? new Rect(crossCenter - thickness / 2, start, thickness, extent)
                : new Rect(start, crossCenter - thickness / 2, extent, thickness);
            var drawingRect = vertical
                ? new Rect(crossCenter - thickness / 2, drawingStart, thickness, drawingEnd - drawingStart)
                : new Rect(drawingStart, crossCenter - thickness / 2, drawingEnd - drawingStart, thickness);
            var brush = enabled ? active ? ActiveBrush : InactiveBrush : DisabledBrush;
            if (brush is null)
                return;
            var rounded = vertical
                ? new RoundedRect(drawingRect, startRadius, startRadius, endRadius, endRadius)
                : new RoundedRect(drawingRect, startRadius, endRadius, endRadius, startRadius);
            using (context.PushClip(rect))
            using (context.PushOpacity(enabled ? 1 : active ? 0.38 : 0.12))
                context.DrawRectangle(brush, null, rounded);
        }

        void Dot(double position, bool active, double opacity)
        {
            var brush = enabled ? active ? SelectedStopBrush : StopBrush
                : active ? IndicatorForeground : DisabledBrush;
            if (brush is null)
                return;
            var center = vertical ? new Point(crossCenter, position) : new Point(position, crossCenter);
            using (context.PushOpacity(opacity))
                context.DrawEllipse(brush, null, center, 2, 2);
        }

        double HandleGap(bool pressed, bool focused) =>
            6 + (enabled && (pressed || focused) ? 1 : 2);

        void Handle(double position, bool pressed, bool focused)
        {
            if (!enabled && pressed)
                pressed = false;
            var handleThickness = enabled && (pressed || focused) ? 2 : 4;
            var rect = vertical
                ? new Rect(crossCenter - handleLength / 2, position - handleThickness / 2, handleLength, handleThickness)
                : new Rect(position - handleThickness / 2, crossCenter - handleLength / 2, handleThickness, handleLength);
            var rounded = new RoundedRect(rect, handleThickness / 2);
            var handleBrush = enabled ? ActiveBrush : DisabledBrush;
            if (handleBrush is null)
                return;
            using (context.PushOpacity(enabled ? 1 : 0.38))
                context.DrawRectangle(handleBrush, null, rounded);
            if (focused && enabled)
            {
                // Follow the handle silhouette, not a circular state layer.
                var ring = rect.Inflate(4.5);
                _focusPen.Brush = FocusBrush;
                context.DrawRectangle(null, _focusPen, new RoundedRect(ring, handleThickness / 2 + 4.5));
            }
        }

        void Indicator(double position, string text)
        {
            var anchor = vertical
                ? new Rect(0, position - 0.5, 1, 1)
                : new Rect(position - 0.5, 0, 1, 1);
            QueueIndicator(anchor, text, vertical);
        }

        void Icon()
        {
            if (!MaterialSlider.GetShowIcon(owner) || IconBrush is null || slider is null || centered ||
                size is MaterialControlSize.ExtraSmall or MaterialControlSize.Small)
                return;
            var glyph = MaterialSlider.GetDisplayedIcon(owner);
            var iconSize = size == MaterialControlSize.ExtraLarge ? 32.0 : 24.0;
            var inset = size == MaterialControlSize.ExtraLarge ? 8.0 : 6.0;
            var position = MaterialSlider.Position(MaterialSlider.Fraction(slider.Value, minimum, maximum),
                length, vertical ? Orientation.Vertical : Orientation.Horizontal, reversed);
            var minimumAtStart = vertical ? reversed : !reversed;
            var activeLength = minimumAtStart ? position - 8 : length - position - 8;
            var onActive = activeLength >= iconSize + inset * 2;
            var start = onActive ? minimumAtStart : !minimumAtStart;
            var along = start ? inset : length - inset - iconSize;
            var x = vertical ? crossCenter - iconSize / 2 : along;
            var y = vertical ? along : crossCenter - iconSize / 2;
            var brush = enabled ? onActive ? IconBrush : ActiveBrush
                : onActive ? IndicatorForeground : DisabledBrush;
            var scale = iconSize / 24;
            using (context.PushOpacity(enabled ? 1 : 0.38))
            using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(x, y)))
                context.DrawGeometry(brush, null, glyph);
        }

        // Stop indicators and snapping share the same value grid.
        void Ticks(double handleA, double handleB, double gapA, double gapB)
        {
            var showStops = MaterialSlider.GetShowStops(owner);
            var frequency = slider?.TickFrequency ?? range!.TickFrequency;
            var showFrequency = showStops || (slider is not null && slider.TickPlacement != TickPlacement.None);
            var step = 0.0;
            if (showFrequency && frequency > 0)
            {
                var steps = (maximum / 2 - minimum / 2) / (frequency / 2);
                if (!double.IsFinite(steps) || steps <= 0)
                    return;
                // More than one tick per pixel cannot be resolved; stride uniformly.
                step = Math.Max(1, Math.Ceiling(steps / Math.Max(1, length / 4))) / steps;
            }
            else
                return;

            for (var fraction = 0.0; fraction <= 1.0001; fraction += step)
            {
                var position = MaterialSlider.Position(fraction, length, vertical ? Orientation.Vertical : Orientation.Horizontal, reversed);
                if (Math.Abs(position - handleA) < gapA + 2 || Math.Abs(position - handleB) < gapB + 2 ||
                    (centered && Math.Abs(position - length / 2) < 4))
                    continue;
                if (position < 8 || position > length - 8)
                    continue;
                bool active;
                if (range is not null)
                {
                    var (_, _, coincident) = range.GetVisualPositions(length);
                    active = !coincident && position > handleA + gapA && position < handleB - gapB;
                }
                else if (centered)
                    active = position > Math.Min(length / 2, handleA) && position < Math.Max(length / 2, handleA);
                else
                    active = (vertical ? reversed : !reversed) ? position < handleA : position > handleA;
                Dot(position, active, enabled ? 1 : 0.38);
            }
        }

        if (range is not null)
        {
            var (lower, upper, coincident) = range.GetVisualPositions(length);
            var lowerThumb = range.LowerThumb;
            var upperThumb = range.UpperThumb;
            var dragging = range.IsDragging;
            var upperActive = range.IsUpperEndpointActive;
            var lowerFocused = lowerThumb is MaterialRangeThumb { IsFocusVisible: true } && !dragging;
            var upperFocused = upperThumb is MaterialRangeThumb { IsFocusVisible: true } && !dragging;
            var lowerGap = HandleGap(dragging && !upperActive, lowerFocused);
            var upperGap = HandleGap(dragging && upperActive, upperFocused);
            Segment(0, lower - lowerGap, false);
            if (!coincident)
                Segment(lower + lowerGap, upper - upperGap, true);
            Segment(upper + upperGap, length, false);
            if (lower - lowerGap > Math.Max(8, outerRadius))
                Dot(6, false, enabled ? 1 : 0.38);
            if (upper + upperGap < length - Math.Max(8, outerRadius))
                Dot(length - 6, false, enabled ? 1 : 0.38);
            Handle(lower, dragging && !upperActive, lowerFocused);
            Handle(upper, dragging && upperActive, upperFocused);
            if ((dragging || lowerFocused || upperFocused) && MaterialSlider.GetShowValueIndicator(owner))
            {
                var showUpper = dragging ? upperActive : upperFocused;
                Indicator(showUpper ? upper : lower,
                    MaterialSlider.FormatValue(showUpper ? range.UpperValue : range.LowerValue, owner));
            }

            Ticks(lower, upper, lowerGap, upperGap);
            Icon();
            return;
        }

        var value = slider!.Value;
        var thumb = Thumb;
        var handlePosition = MaterialSlider.Position(MaterialSlider.Fraction(value, minimum, maximum),
            length, vertical ? Orientation.Vertical : Orientation.Horizontal, reversed);
        // The minimum end is at the start for a horizontal non-reversed slider and for a
        // vertical reversed slider; the fill always grows from that end.
        var activeTowardStart = vertical ? reversed : !reversed;
        var pressed = IsDragging || _ownerPressed || (thumb is MaterialSliderThumb { IsDragging: true });
        var gap = HandleGap(pressed, focusVisible);

        if (centered)
        {
            var center = length / 2;
            // The centered track has a 4-DIP total break at every control size.
            Segment(0, Math.Min(center - 2, handlePosition - gap), false);
            Segment(Math.Max(center + 2, handlePosition + gap), length, false);
            if (handlePosition < center)
                Segment(handlePosition + gap, center - 2, true);
            else
                Segment(center + 2, handlePosition - gap, true);
            if (handlePosition - gap > Math.Max(8, outerRadius))
                Dot(6, false, enabled ? 1 : 0.38);
            if (handlePosition + gap < length - Math.Max(8, outerRadius))
                Dot(length - 6, false, enabled ? 1 : 0.38);
            Ticks(handlePosition, handlePosition, gap, gap);
        }
        else
        {
            if (activeTowardStart)
            {
                Segment(0, handlePosition - gap, true);
                Segment(handlePosition + gap, length, false);
                if (handlePosition + gap < length - Math.Max(10, outerRadius))
                    Dot(length - 6, false, enabled ? 1 : 0.38);
            }
            else
            {
                Segment(0, handlePosition - gap, false);
                Segment(handlePosition + gap, length, true);
                if (handlePosition - gap > Math.Max(10, outerRadius))
                    Dot(6, false, enabled ? 1 : 0.38);
            }
            Ticks(handlePosition, handlePosition, gap, gap);
        }

        Handle(handlePosition, pressed, focusVisible);
        if (MaterialSlider.GetShowValueIndicator(owner) && (pressed || focusVisible))
            Indicator(handlePosition, MaterialSlider.FormatValue(value, owner));
        Icon();
    }
}
