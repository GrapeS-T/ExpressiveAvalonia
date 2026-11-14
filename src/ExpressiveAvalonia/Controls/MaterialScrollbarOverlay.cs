using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// The default values used by <see cref="MaterialScrollbarOverlay"/>, matching the source's
/// <c>NonInteractiveScrollbarDefaults</c>.
/// </summary>
public static class MaterialScrollbarDefaults
{
    /// <summary>Default opacity applied to the outline thumb role.</summary>
    public const double ThumbOpacity = 0.7;

    /// <summary>Default fade-out animation duration, in milliseconds.</summary>
    public const int FadeDurationMillis = 250;

    /// <summary>Default delay before the fade-out begins, in milliseconds.</summary>
    public const int FadeDelayMillis = 400;

    /// <summary>Default scrollbar thickness, in device-independent units (4 dp).</summary>
    public const double Thickness = 4;

    /// <summary>Default minimum thumb length (24 dp).</summary>
    public const double ThumbMinLength = 24;

    /// <summary>Default maximum thumb length as a fraction of the track (0.9).</summary>
    public const double ThumbMaxLengthFraction = 0.9;

    /// <summary>Default inset at both ends of the track (2 dp).</summary>
    public const double MainAxisTrackInset = 2;

    /// <summary>Default inset from the end edge of the scrollable node (0 dp).</summary>
    public const double CrossAxisTrackInset = 0;
}

/// <summary>
/// A visual-only scrollbar that mirrors a native <see cref="ScrollViewer"/>'s offset, viewport and
/// extent, ported from the source <c>Modifier.nonInteractiveScrollbar</c>.
/// </summary>
/// <remarks>
/// <para>
/// This control only paints. It never handles pointer, wheel or keyboard input and has no
/// automation peer, so the existing Avalonia <see cref="ScrollBar"/> controls, scrolling
/// commands, page/wheel behavior and templates remain the single scroll model. Place it beside
/// or over a <see cref="ScrollViewer"/> and assign <see cref="TargetScrollViewer"/>; enabling it
/// is optional.
/// </para>
/// <para>
/// The source draws a thumb only when content is strictly larger than the viewport. With
/// <see cref="IsFadeEnabled"/> true it starts at zero alpha, appears immediately on a scroll
/// offset change, remains visible for <see cref="FadeDelayMillis"/>, then fades to zero over
/// <see cref="FadeDurationMillis"/>. Every new offset change during that delay restarts it; an
/// offset change during the fade snaps the thumb back to full alpha and restarts the delay. With
/// fading disabled the bar is always visible while the content overflows.
/// </para>
/// <para>
/// The track uses the native content presenter's physical DIP bounds, while thumb ratios use
/// <see cref="ScrollViewer.Viewport"/> and <see cref="ScrollViewer.Extent"/> in the same native
/// scroll units. This also supports virtualizing panels whose viewport is measured in items.
/// Native scrollbar hit targets stay untouched. Place the overlay over the whole scroll viewer
/// (or at least over its top-left viewport origin). Vertical bars sit on the logical end edge;
/// horizontal thumbs reverse in right-to-left layouts. Ratios are not rounded to integer pixels
/// as in the source adapter.
/// </para>
/// <para>
/// The default thumb color is the Material outline role with <see
/// cref="MaterialScrollbarDefaults.ThumbOpacity"/>; set <see cref="ThumbColor"/> to override it
/// exactly. The source Scrollbar does not size itself from precision pointer flags.
/// </para>
/// </remarks>
public sealed class MaterialScrollbarOverlay : Control
{
    /// <summary>Defines the <see cref="TargetScrollViewer"/> property.</summary>
    public static readonly StyledProperty<ScrollViewer?> TargetScrollViewerProperty =
        AvaloniaProperty.Register<MaterialScrollbarOverlay, ScrollViewer?>(
            nameof(TargetScrollViewer));

    /// <summary>Defines the <see cref="Orientation"/> property.</summary>
    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<MaterialScrollbarOverlay, Orientation>(
            nameof(Orientation), Orientation.Vertical);

    /// <summary>Defines the <see cref="ThumbColor"/> property.</summary>
    /// <remarks><c>null</c> resolves the theme outline role at 70% alpha.</remarks>
    public static readonly StyledProperty<Color?> ThumbColorProperty =
        AvaloniaProperty.Register<MaterialScrollbarOverlay, Color?>(nameof(ThumbColor));

    /// <summary>Defines the <see cref="TrackColor"/> property.</summary>
    public static readonly StyledProperty<Color> TrackColorProperty =
        AvaloniaProperty.Register<MaterialScrollbarOverlay, Color>(
            nameof(TrackColor), Colors.Transparent);

    /// <summary>Defines the <see cref="Thickness"/> property.</summary>
    public static readonly StyledProperty<double> ThicknessProperty =
        AvaloniaProperty.Register<MaterialScrollbarOverlay, double>(
            nameof(Thickness), MaterialScrollbarDefaults.Thickness,
            validate: IsNonNegativeFinite);

    /// <summary>Defines the <see cref="ThumbMinLength"/> property.</summary>
    public static readonly StyledProperty<double> ThumbMinLengthProperty =
        AvaloniaProperty.Register<MaterialScrollbarOverlay, double>(
            nameof(ThumbMinLength), MaterialScrollbarDefaults.ThumbMinLength,
            validate: IsNonNegativeFinite);

    /// <summary>Defines the <see cref="ThumbMaxLengthFraction"/> property.</summary>
    public static readonly StyledProperty<double> ThumbMaxLengthFractionProperty =
        AvaloniaProperty.Register<MaterialScrollbarOverlay, double>(
            nameof(ThumbMaxLengthFraction), MaterialScrollbarDefaults.ThumbMaxLengthFraction,
            validate: value => double.IsFinite(value) && value is >= 0 and <= 1);

    /// <summary>Defines the <see cref="IsFadeEnabled"/> property.</summary>
    public static readonly StyledProperty<bool> IsFadeEnabledProperty =
        AvaloniaProperty.Register<MaterialScrollbarOverlay, bool>(nameof(IsFadeEnabled), true);

    /// <summary>Defines the <see cref="FadeDurationMillis"/> property.</summary>
    public static readonly StyledProperty<int> FadeDurationMillisProperty =
        AvaloniaProperty.Register<MaterialScrollbarOverlay, int>(
            nameof(FadeDurationMillis), MaterialScrollbarDefaults.FadeDurationMillis,
            validate: value => value >= 0);

    /// <summary>Defines the <see cref="FadeDelayMillis"/> property.</summary>
    public static readonly StyledProperty<int> FadeDelayMillisProperty =
        AvaloniaProperty.Register<MaterialScrollbarOverlay, int>(
            nameof(FadeDelayMillis), MaterialScrollbarDefaults.FadeDelayMillis,
            validate: value => value >= 0);

    /// <summary>Defines the <see cref="MainAxisTrackInset"/> property.</summary>
    public static readonly StyledProperty<double> MainAxisTrackInsetProperty =
        AvaloniaProperty.Register<MaterialScrollbarOverlay, double>(
            nameof(MainAxisTrackInset), MaterialScrollbarDefaults.MainAxisTrackInset,
            validate: IsNonNegativeFinite);

    /// <summary>Defines the <see cref="CrossAxisTrackInset"/> property.</summary>
    public static readonly StyledProperty<double> CrossAxisTrackInsetProperty =
        AvaloniaProperty.Register<MaterialScrollbarOverlay, double>(
            nameof(CrossAxisTrackInset), MaterialScrollbarDefaults.CrossAxisTrackInset,
            validate: IsNonNegativeFinite);

    // Compose tween() defaults to FastOutSlowInEasing = CubicBezierEasing(0.4, 0, 0.2, 1).
    private static readonly Func<double, double> FadeEasing =
        new SplineEasing(0.4, 0, 0.2, 1).Ease;

    private readonly SolidColorBrush _thumbBrush = new(Colors.Transparent);
    private readonly SolidColorBrush _trackBrush = new(Colors.Transparent);
    private readonly DispatcherTimer _fadeDelayTimer;
    private MaterialMotion.TweenAnimation? _fadeAnimation;
    private ScrollViewer? _subscribedTarget;
    private ScrollContentPresenter? _presenter;
    private Color _themeOutlineColor;
    private double _fadeAlpha;
    private double _lastOffset;
    private bool _hasOffsetBaseline;

    static MaterialScrollbarOverlay()
    {
        AffectsRender<MaterialScrollbarOverlay>(
            FlowDirectionProperty,
            OrientationProperty,
            ThumbColorProperty,
            TrackColorProperty,
            ThicknessProperty,
            ThumbMinLengthProperty,
            ThumbMaxLengthFractionProperty,
            MainAxisTrackInsetProperty,
            CrossAxisTrackInsetProperty);
    }

    /// <summary>Creates a passive, non-focusable overlay.</summary>
    public MaterialScrollbarOverlay()
    {
        Focusable = false;
        IsHitTestVisible = false;
        _fadeDelayTimer = new DispatcherTimer();
        _fadeDelayTimer.Tick += OnFadeDelayElapsed;
        // Source initial alpha: Animatable(if (isFadeEnabled) 0f else 1f).
        _fadeAlpha = 0;
    }

    /// <summary>Gets or sets the native scroll viewer whose metrics are displayed.</summary>
    public ScrollViewer? TargetScrollViewer
    {
        get => GetValue(TargetScrollViewerProperty);
        set => SetValue(TargetScrollViewerProperty, value);
    }

    /// <summary>Gets or sets whether the bar is vertical or horizontal.</summary>
    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>
    /// Gets or sets the thumb color. <c>null</c> uses the theme outline role with
    /// <see cref="MaterialScrollbarDefaults.ThumbOpacity"/>, matching the source default.
    /// </summary>
    public Color? ThumbColor
    {
        get => GetValue(ThumbColorProperty);
        set => SetValue(ThumbColorProperty, value);
    }

    /// <summary>Gets or sets the track color. The source default is transparent.</summary>
    public Color TrackColor
    {
        get => GetValue(TrackColorProperty);
        set => SetValue(TrackColorProperty, value);
    }

    /// <summary>Gets or sets the scrollbar thickness.</summary>
    public double Thickness
    {
        get => GetValue(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }

    /// <summary>Gets or sets the minimum thumb length.</summary>
    public double ThumbMinLength
    {
        get => GetValue(ThumbMinLengthProperty);
        set => SetValue(ThumbMinLengthProperty, value);
    }

    /// <summary>Gets or sets the maximum thumb length as a fraction of the track.</summary>
    public double ThumbMaxLengthFraction
    {
        get => GetValue(ThumbMaxLengthFractionProperty);
        set => SetValue(ThumbMaxLengthFractionProperty, value);
    }

    /// <summary>Gets or sets whether the bar fades after scrolling stops.</summary>
    public bool IsFadeEnabled
    {
        get => GetValue(IsFadeEnabledProperty);
        set => SetValue(IsFadeEnabledProperty, value);
    }

    /// <summary>Gets or sets the fade duration in milliseconds.</summary>
    public int FadeDurationMillis
    {
        get => GetValue(FadeDurationMillisProperty);
        set => SetValue(FadeDurationMillisProperty, value);
    }

    /// <summary>Gets or sets the delay before fading in milliseconds.</summary>
    public int FadeDelayMillis
    {
        get => GetValue(FadeDelayMillisProperty);
        set => SetValue(FadeDelayMillisProperty, value);
    }

    /// <summary>Gets or sets the track inset at both ends of the scroll axis.</summary>
    public double MainAxisTrackInset
    {
        get => GetValue(MainAxisTrackInsetProperty);
        set => SetValue(MainAxisTrackInsetProperty, value);
    }

    /// <summary>Gets or sets the track inset from the end edge of the overlay.</summary>
    public double CrossAxisTrackInset
    {
        get => GetValue(CrossAxisTrackInsetProperty);
        set => SetValue(CrossAxisTrackInsetProperty, value);
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (TargetScrollViewer is not { } scroll)
            return;

        var vertical = Orientation == Orientation.Vertical;
        var extent = scroll.Extent;
        var viewport = scroll.Viewport;
        var contentSize = vertical ? extent.Height : extent.Width;
        var viewportSize = vertical ? viewport.Height : viewport.Width;
        var offset = vertical ? scroll.Offset.Y : scroll.Offset.X;

        if (!MaterialScrollbarGeometry.TryCalculate(
                _presenter?.Bounds.Size ?? scroll.Bounds.Size,
                Orientation,
                FlowDirection,
                offset,
                contentSize,
                viewportSize,
                Thickness,
                ThumbMinLength,
                ThumbMaxLengthFraction,
                MainAxisTrackInset,
                CrossAxisTrackInset,
                out var track,
                out var thumb))
        {
            return;
        }

        var fadeAlpha = IsFadeEnabled ? Math.Clamp(_fadeAlpha, 0, 1) : 1;
        if (fadeAlpha <= 0)
            return;

        var thumbColor = ResolveThumbColor();
        var trackColor = TrackColor;
        var drawThumb = thumbColor.A > 0;
        var drawTrack = trackColor.A > 0;
        if (!drawThumb && !drawTrack)
            return;

        _trackBrush.Color = trackColor;
        _thumbBrush.Color = thumbColor;

        using (context.PushOpacity(fadeAlpha))
        {
            if (drawTrack)
                context.DrawRectangle(_trackBrush, null, track);

            if (drawThumb)
            {
                // Source: CornerRadius(thicknessPx / 2, thicknessPx / 2), cached by thickness.
                var radius = Thickness / 2;
                context.DrawRectangle(_thumbBrush, null, thumb, radius, radius, default);
            }
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        ResourcesChanged += OnResourcesChanged;
        ActualThemeVariantChanged += OnActualThemeVariantChanged;
        UpdateThemeOutlineColor();

        if (TargetScrollViewer is not null)
            Subscribe(TargetScrollViewer);
        else
            BaselineOffset();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        // Stop before the base call raises DetachedFromVisualTree: MaterialMotion.Tween listens
        // to that event and would otherwise apply the fade destination on detach. The source
        // cancels the fade job without changing the current alpha.
        StopFade();
        Unsubscribe();

        ResourcesChanged -= OnResourcesChanged;
        ActualThemeVariantChanged -= OnActualThemeVariantChanged;

        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TargetScrollViewerProperty)
        {
            ResetFadeState();
            if (this.IsAttachedToVisualTree())
            {
                Unsubscribe();
                if (TargetScrollViewer is { } target)
                    Subscribe(target);
                else
                    BaselineOffset();
            }
        }
        else if (change.Property == OrientationProperty ||
                 change.Property == ThumbColorProperty ||
                 change.Property == TrackColorProperty ||
                 change.Property == ThicknessProperty ||
                 change.Property == ThumbMinLengthProperty ||
                 change.Property == ThumbMaxLengthFractionProperty ||
                 change.Property == IsFadeEnabledProperty ||
                 change.Property == FadeDurationMillisProperty ||
                 change.Property == FadeDelayMillisProperty ||
                 change.Property == MainAxisTrackInsetProperty ||
                 change.Property == CrossAxisTrackInsetProperty)
        {
            // The source ModifierNodeElement recreates its node for every parameter change, which
            // resets the Animatable to 0/1 and baselines lastOffset in onAttach.
            ResetFadeState();
        }
    }

    private static bool IsNonNegativeFinite(double value) =>
        double.IsFinite(value) && value >= 0;

    private Color ResolveThumbColor()
    {
        if (ThumbColor is { } configured)
            return configured;

        var alpha = (byte)(MaterialScrollbarDefaults.ThumbOpacity * 255);
        return Color.FromArgb(alpha, _themeOutlineColor.R, _themeOutlineColor.G, _themeOutlineColor.B);
    }

    private void OnResourcesChanged(object? sender, ResourcesChangedEventArgs e) =>
        UpdateThemeOutlineColor();

    private void OnActualThemeVariantChanged(object? sender, EventArgs e) =>
        UpdateThemeOutlineColor();

    private void UpdateThemeOutlineColor()
    {
        var previous = _themeOutlineColor;
        if (this.TryFindResource("MaterialOutlineColor", ActualThemeVariant, out var value) && value is Color color)
            _themeOutlineColor = color;
        else if (this.TryFindResource("MaterialOutlineBrush", ActualThemeVariant, out var brush) &&
                 brush is ISolidColorBrush solid)
            _themeOutlineColor = solid.Color;
        else
            _themeOutlineColor = Colors.Transparent;

        if (ThumbColor is null && previous != _themeOutlineColor)
        {
            // The source thumbColor default is a composable getter. A color-scheme change
            // produces a new color argument and therefore a new modifier node, whose Animatable
            // starts at 0/1 again.
            ResetFadeState();
            return;
        }

        InvalidateVisual();
    }

    private void Subscribe(ScrollViewer target)
    {
        if (ReferenceEquals(_subscribedTarget, target))
            return;

        Unsubscribe();
        _subscribedTarget = target;
        _subscribedTarget.ScrollChanged += OnTargetScrollChanged;
        target.TemplateApplied += OnTargetTemplateApplied;
        _presenter = target.GetVisualDescendants().OfType<ScrollContentPresenter>().FirstOrDefault();
        BaselineOffset();
    }

    private void Unsubscribe()
    {
        if (_subscribedTarget is null)
            return;

        _subscribedTarget.ScrollChanged -= OnTargetScrollChanged;
        _subscribedTarget.TemplateApplied -= OnTargetTemplateApplied;
        _presenter = null;
        _subscribedTarget = null;
    }

    private void OnTargetTemplateApplied(object? sender, TemplateAppliedEventArgs e) =>
        _presenter = e.NameScope.Find<ScrollContentPresenter>("PART_ContentPresenter");

    private void OnTargetScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer scroll)
            return;

        var vertical = Orientation == Orientation.Vertical;
        var extent = scroll.Extent;
        var viewport = scroll.Viewport;
        var contentSize = vertical ? extent.Height : extent.Width;
        var viewportSize = vertical ? viewport.Height : viewport.Width;

        // Source ordering: content-fit checks run before scrollOffset is observed, so a hidden
        // bar does not baseline or trigger a fade.
        if (IsFadeEnabled &&
            contentSize > viewportSize && viewportSize > 0 && contentSize > 0)
        {
            var offset = vertical ? scroll.Offset.Y : scroll.Offset.X;
            if (!_hasOffsetBaseline || offset != _lastOffset)
            {
                _lastOffset = offset;
                _hasOffsetBaseline = true;
                StartFadeSession();
            }
        }

        InvalidateVisual();
    }

    private void BaselineOffset()
    {
        if (_subscribedTarget is { } target)
        {
            _lastOffset = Orientation == Orientation.Vertical
                ? target.Offset.Y
                : target.Offset.X;
            _hasOffsetBaseline = true;
        }
        else
        {
            _lastOffset = 0;
            _hasOffsetBaseline = false;
        }
    }

    private void ResetFadeState()
    {
        StopFade();
        _fadeAlpha = IsFadeEnabled ? 0 : 1;
        BaselineOffset();
        InvalidateVisual();
    }

    private void StartFadeSession()
    {
        // Source: every offset change cancels a running fade, snaps to 1, then waits for the
        // fade delay again. While the delay is active it only restarts the timeout.
        StopFade();
        _fadeAlpha = 1;
        InvalidateVisual();

        if (FadeDelayMillis <= 0)
        {
            BeginFade();
            return;
        }

        _fadeDelayTimer.Interval = TimeSpan.FromMilliseconds(FadeDelayMillis);
        _fadeDelayTimer.Start();
    }

    private void OnFadeDelayElapsed(object? sender, EventArgs e)
    {
        _fadeDelayTimer.Stop();
        BeginFade();
    }

    private void BeginFade()
    {
        if (!IsFadeEnabled)
            return;

        // The source test with motionDurationScale = 0 keeps the delay independent of the
        // animation scale, then hides without a fade. MaterialMotion.Tween would detect reduced
        // motion in Retarget and finish before its delay, so the collapse is handled here.
        if (MaterialMotion.GetIsReduced(this) || FadeDurationMillis == 0)
        {
            _fadeAnimation?.Dispose();
            _fadeAnimation = null;
            _fadeAlpha = 0;
            InvalidateVisual();
            return;
        }

        _fadeAnimation?.Dispose();
        _fadeAnimation = MaterialMotion.Tween(
            this,
            1,
            0,
            FadeDurationMillis / 1000d,
            delaySeconds: 0,
            value =>
            {
                _fadeAlpha = value;
                InvalidateVisual();
            },
            easing: FadeEasing);
    }

    private void StopFade()
    {
        _fadeDelayTimer.Stop();
        _fadeAnimation?.Dispose();
        _fadeAnimation = null;
    }
}