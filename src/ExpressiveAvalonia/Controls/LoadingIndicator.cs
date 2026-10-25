using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// The Expressive loading indicator: the source silhouette slowly turns while it alternates
/// between the seven source "Steps", optionally inside the 48 dp circular container.
/// </summary>
/// <remarks>
/// <para>
/// Source family: Loading indicator (local-004296), axes "Steps" 1–7 and "Show container"
/// False/True. The seven outlines are the exact source paths (see
/// <see cref="LoadingIndicatorShapes"/>); <see cref="Step"/> chooses the outline that stays on
/// screen when motion is unavailable, and also the outline the animation starts from.
/// </para>
/// <para>
/// Motion pauses when the control leaves the visual tree, when it or an ancestor is hidden,
/// and when <see cref="MaterialMotion.IsReduced"/> is set. In that case the selected step is
/// shown still, with no substitute geometry.
/// </para>
/// </remarks>
public sealed class LoadingIndicator : TemplatedControl
{
    public static readonly StyledProperty<int> StepProperty =
        AvaloniaProperty.Register<LoadingIndicator, int>(nameof(Step), 1,
            coerce: (_, value) => Math.Clamp(value, 1, LoadingIndicatorShapes.Count));
    public static readonly StyledProperty<bool> IsAnimatedProperty =
        AvaloniaProperty.Register<LoadingIndicator, bool>(nameof(IsAnimated), true);
    public static readonly StyledProperty<bool> ShowContainerProperty =
        AvaloniaProperty.Register<LoadingIndicator, bool>(nameof(ShowContainer));
    public static readonly StyledProperty<IBrush?> ContainerBrushProperty =
        AvaloniaProperty.Register<LoadingIndicator, IBrush?>(nameof(ContainerBrush));

    // Each step holds its silhouette, then cross-fades into the next one.
    private const double HoldMilliseconds = 400;
    private const double FadeMilliseconds = 200;
    private const double SlotMilliseconds = HoldMilliseconds + FadeMilliseconds;
    private const double CycleMilliseconds = SlotMilliseconds * 7;

    private readonly ProgressAnimationClock _clock;

    static LoadingIndicator()
    {
        AffectsRender<LoadingIndicator>(StepProperty, ShowContainerProperty, ContainerBrushProperty,
            ForegroundProperty, IsAnimatedProperty);
    }

    public LoadingIndicator() => _clock = new ProgressAnimationClock(this, () => IsAnimated);

    /// <summary>Source "Steps" axis: 1–7. Also the first silhouette of the animated cycle.</summary>
    public int Step { get => GetValue(StepProperty); set => SetValue(StepProperty, value); }

    /// <summary>Cross-fades through the seven source silhouettes; paused for reduced motion.</summary>
    public bool IsAnimated { get => GetValue(IsAnimatedProperty); set => SetValue(IsAnimatedProperty, value); }

    /// <summary>Source "Show container" axis: draws the circular container behind the shape.</summary>
    public bool ShowContainer { get => GetValue(ShowContainerProperty); set => SetValue(ShowContainerProperty, value); }

    public IBrush? ContainerBrush { get => GetValue(ContainerBrushProperty); set => SetValue(ContainerBrushProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        // The source artboard is 48 dp; shrink only when a parent offers a real, smaller slot.
        var width = availableSize.Width > 0 && availableSize.Width < 48 ? availableSize.Width : 48;
        var height = availableSize.Height > 0 && availableSize.Height < 48 ? availableSize.Height : 48;
        return new Size(width, height);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new LoadingIndicatorAutomationPeer(this);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsAnimatedProperty)
            _clock.Refresh();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (size <= 0)
            return;

        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        if (ShowContainer && ContainerBrush is { } container)
            context.DrawEllipse(container, null, center, size / 2, size / 2);

        var (current, next, blend, angle) = CurrentFrame();
        var scale = size / LoadingIndicatorShapes.Artboard;
        DrawShape(context, current, 1 - blend, angle, scale, center);
        if (blend > 0 && next != current)
            DrawShape(context, next, blend, angle, scale, center);
    }

    /// <summary>
    /// Draws one source silhouette centred on <paramref name="center"/>. The path data is in the
    /// 48-unit artboard space, so the control scale is applied uniformly and each shape is
    /// centred by its own bounds instead of assuming the artwork is centred in its box.
    /// </summary>
    private void DrawShape(DrawingContext context, int step, double opacity, double angle,
        double scale, Point center)
    {
        if (opacity <= 0)
            return;
        var geometry = LoadingIndicatorShapes.Get(step);
        var bounds = geometry.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;
        using (context.PushTransform(
                   Matrix.CreateTranslation(-(bounds.X + bounds.Width / 2), -(bounds.Y + bounds.Height / 2)) *
                   Matrix.CreateRotation(angle) *
                   Matrix.CreateScale(scale, scale) *
                   Matrix.CreateTranslation(center.X, center.Y)))
        using (context.PushOpacity(opacity))
            context.DrawGeometry(Foreground, null, geometry);
    }

    /// <summary>Returns the silhouette pair, its cross-fade weight and the current turn angle.</summary>
    private (int Current, int Next, double Blend, double Angle) CurrentFrame()
    {
        if (!IsAnimated || MaterialMotion.GetIsReduced(this))
            return (Step, Step, 0, 0);

        // Start the cycle at the selected step so Step stays meaningful while animating.
        var time = (_clock.ElapsedMilliseconds + (Step - 1) * SlotMilliseconds) % CycleMilliseconds;
        var index = (int)(time / SlotMilliseconds);
        var local = time - index * SlotMilliseconds;
        var progress = local <= HoldMilliseconds ? 0 : (local - HoldMilliseconds) / FadeMilliseconds;
        // Smoothstep keeps the fade free of a velocity jump at both ends.
        var blend = progress * progress * (3 - 2 * progress);
        var current = index + 1;
        var next = current % LoadingIndicatorShapes.Count + 1;
        // One slow turn per cycle, as a loading affordance; the source art itself is static.
        return (current, next, blend, time / CycleMilliseconds * Math.Tau);
    }

    private sealed class LoadingIndicatorAutomationPeer(LoadingIndicator owner) : ControlAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(LoadingIndicator);
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ProgressBar;

        protected override string? GetNameCore() =>
            base.GetNameCore() is { Length: > 0 } name ? name : "Loading";

        // The indicator is always indeterminate: it reports a status, never a fake percentage.
        protected override string? GetItemStatusCore() =>
            AutomationProperties.GetItemStatus(owner) ?? "Indeterminate";

        protected override object? GetProviderCore(Type providerType) =>
            providerType == typeof(IRangeValueProvider) ? null : base.GetProviderCore(providerType);
    }
}
