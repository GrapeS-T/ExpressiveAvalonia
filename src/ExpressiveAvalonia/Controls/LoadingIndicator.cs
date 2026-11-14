using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Controls;

public enum LoadingIndicatorMotion
{
    /// <summary>Плавная трансформация силуэтов (smooth shape morphing).</summary>
    Smooth,
    /// <summary>Трансформация с пружинным скачком (spring bounce / jump morph).</summary>
    SpringBounce
}

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
    public static readonly StyledProperty<LoadingIndicatorMotion> MotionProperty =
        AvaloniaProperty.Register<LoadingIndicator, LoadingIndicatorMotion>(nameof(Motion), LoadingIndicatorMotion.Smooth);

    // Each step holds its silhouette briefly, then morphs into the next one.
    private const double HoldMilliseconds = 250;
    private const double MorphMilliseconds = 350;
    private const double SlotMilliseconds = HoldMilliseconds + MorphMilliseconds;
    private const double CycleMilliseconds = SlotMilliseconds * 7;

    private readonly ProgressAnimationClock _clock;

    static LoadingIndicator()
    {
        AffectsRender<LoadingIndicator>(StepProperty, ShowContainerProperty, ContainerBrushProperty,
            ForegroundProperty, IsAnimatedProperty, MotionProperty);
    }

    public LoadingIndicator() => _clock = new ProgressAnimationClock(this, () => IsAnimated);

    /// <summary>Source "Steps" axis: 1–7. Also the first silhouette of the animated cycle.</summary>
    public int Step { get => GetValue(StepProperty); set => SetValue(StepProperty, value); }

    /// <summary>Morphs through the seven source silhouettes; paused for reduced motion.</summary>
    public bool IsAnimated { get => GetValue(IsAnimatedProperty); set => SetValue(IsAnimatedProperty, value); }

    /// <summary>Source "Show container" axis: draws the circular container behind the shape.</summary>
    public bool ShowContainer { get => GetValue(ShowContainerProperty); set => SetValue(ShowContainerProperty, value); }

    public IBrush? ContainerBrush { get => GetValue(ContainerBrushProperty); set => SetValue(ContainerBrushProperty, value); }

    /// <summary>Animation style: Smooth shape morphing or SpringBounce jump morph.</summary>
    public LoadingIndicatorMotion Motion { get => GetValue(MotionProperty); set => SetValue(MotionProperty, value); }

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

        var scale = size / LoadingIndicatorShapes.Artboard;

        if (!IsAnimated || MaterialMotion.GetIsReduced(this))
        {
            DrawStaticShape(context, Step, 0, scale, center);
            return;
        }

        var (current, next, morphT, angle, springScale) = CurrentFrame();
        var geometry = LoadingIndicatorShapes.CreateMorphedGeometry(current, next, morphT);
        var totalScale = scale * springScale;

        using (context.PushTransform(
                   Matrix.CreateRotation(angle) *
                   Matrix.CreateScale(totalScale, totalScale) *
                   Matrix.CreateTranslation(center.X, center.Y)))
        {
            context.DrawGeometry(Foreground, null, geometry);
        }
    }

    private void DrawStaticShape(DrawingContext context, int step, double angle, double scale, Point center)
    {
        var geometry = LoadingIndicatorShapes.Get(step);
        var origin = LoadingIndicatorShapes.GetCenter(step);
        using (context.PushTransform(
                   Matrix.CreateTranslation(-origin.X, -origin.Y) *
                   Matrix.CreateRotation(angle) *
                   Matrix.CreateScale(scale, scale) *
                   Matrix.CreateTranslation(center.X, center.Y)))
        {
            context.DrawGeometry(Foreground, null, geometry);
        }
    }

    /// <summary>Returns the silhouette pair, morph blend t, current turn angle, and spring scale factor.</summary>
    private (int Current, int Next, double MorphT, double Angle, double SpringScale) CurrentFrame()
    {
        if (!IsAnimated || MaterialMotion.GetIsReduced(this))
            return (Step, Step, 0, 0, 1.0);

        // Start the cycle at the selected step so Step stays meaningful while animating.
        var time = (_clock.ElapsedMilliseconds + (Step - 1) * SlotMilliseconds) % CycleMilliseconds;
        var index = (int)(time / SlotMilliseconds);
        var local = time - index * SlotMilliseconds;
        var current = index + 1;
        var next = current % LoadingIndicatorShapes.Count + 1;
        var baseAngle = time / CycleMilliseconds * Math.Tau;

        if (local <= HoldMilliseconds)
        {
            return (current, next, 0, baseAngle, 1.0);
        }

        var p = (local - HoldMilliseconds) / MorphMilliseconds; // 0.0 to 1.0

        if (Motion == LoadingIndicatorMotion.Smooth)
        {
            // Smoothstep curve for seamless fluid shape morphing
            var morphT = p * p * (3.0 - 2.0 * p);
            return (current, next, morphT, baseAngle, 1.0);
        }
        else // SpringBounce
        {
            // Underdamped spring curve for morph parameter t with subtle overshoot
            var springProgress = 1.0 - Math.Exp(-5.0 * p) * Math.Cos(p * Math.PI * 2.5);
            var morphT = Math.Clamp(springProgress, 0.0, 1.12);

            // Dynamic spring scale pop / bounce:
            // Anticipatory squash, explosive spring pop outward to ~1.14, then harmonic settling
            var bounce = -Math.Sin(p * Math.PI * 3.5) * Math.Exp(-4.0 * p);
            var springScale = 1.0 + 0.16 * bounce;

            // Rotational spring torque impulse
            var kick = 0.12 * Math.Sin(p * Math.PI) * Math.Exp(-2.5 * p);
            var angle = baseAngle + kick;

            return (current, next, morphT, angle, springScale);
        }
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
