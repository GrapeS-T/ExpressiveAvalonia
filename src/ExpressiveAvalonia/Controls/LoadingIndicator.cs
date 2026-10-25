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
/// The Expressive loading indicator: one silhouette that morphs through the seven source "Steps",
/// optionally inside the 48 dp circular container.
/// </summary>
/// <remarks>
/// <para>
/// Source family: Loading indicator (local-004296), axes "Steps" 1–7 and "Show container"
/// False/True. The seven outlines are the exact source paths (see
/// <see cref="LoadingIndicatorShapes"/>); <see cref="Step"/> chooses the outline that stays on
/// screen when motion is unavailable, and also the outline the animation starts from.
/// </para>
/// <para>
/// Motion is the indeterminate Material loading indicator, taken from androidx
/// <c>LoadingIndicator</c>: every 650 ms the outline morphs into the next step along a spring with
/// damping 0.6 and stiffness 200, each morph adds a quarter turn, and the whole indicator makes one
/// slow turn every 4666 ms. The spring settles after ≈298 ms, so the shape holds still for the rest
/// of each slot. Exactly one outline is filled at full opacity on every frame — the morph
/// interpolates the silhouette itself, so nothing cross-fades.
/// </para>
/// <para>
/// Motion pauses when the control leaves the visual tree, when it or an ancestor is hidden, and
/// when <see cref="MaterialMotion.IsReduced"/> is set. In that case the selected step is shown
/// still, with no substitute geometry.
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

    // Indeterminate timing from androidx LoadingIndicator.kt: one morph every 650 ms, one full turn
    // every 4666 ms, and a quarter turn added by each morph.
    private const double MorphMilliseconds = 650;
    private const double RotationMilliseconds = 4666;
    private const double QuarterTurn = 90;

    // The morph spring: spring(dampingRatio = 0.6f, stiffness = 200f, visibilityThreshold = 0.1f).
    private const double SpringDamping = 0.6;
    private const double SpringStiffness = 200;
    private const double SpringThreshold = 0.1;

    private static readonly double SpringFrequency = Math.Sqrt(SpringStiffness);
    private static readonly double SpringDampedFrequency =
        SpringFrequency * Math.Sqrt(1 - SpringDamping * SpringDamping);
    private static readonly double SpringMilliseconds = SpringDuration();

    private readonly ProgressAnimationClock _clock;

    static LoadingIndicator()
    {
        AffectsRender<LoadingIndicator>(StepProperty, ShowContainerProperty, ContainerBrushProperty,
            ForegroundProperty, IsAnimatedProperty);
    }

    public LoadingIndicator() => _clock = new ProgressAnimationClock(this, () => IsAnimated);

    /// <summary>Source "Steps" axis: 1–7. Also the first silhouette of the animated cycle.</summary>
    public int Step { get => GetValue(StepProperty); set => SetValue(StepProperty, value); }

    /// <summary>Morphs through the seven source silhouettes; paused for reduced motion.</summary>
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

        var scale = size / LoadingIndicatorShapes.Artboard;
        if (TryGetMorph(out var step, out var progress, out var angle))
        {
            // Both outlines are centred on the origin, so the turn is about the container centre.
            using (context.PushTransform(
                       Matrix.CreateRotation(angle * Math.PI / 180) *
                       Matrix.CreateScale(scale, scale) *
                       Matrix.CreateTranslation(center.X, center.Y)))
                context.DrawGeometry(Foreground, null, LoadingIndicatorShapes.Morph(step, progress));
            return;
        }

        // Motion is unavailable: the selected source outline, upright and on its own.
        using (context.PushTransform(
                   Matrix.CreateScale(scale, scale) *
                   Matrix.CreateTranslation(center.X, center.Y)))
            context.DrawGeometry(Foreground, null, LoadingIndicatorShapes.Get(Step));
    }

    /// <summary>
    /// Returns the morph that is on screen right now: the step it starts from, the spring's progress
    /// (which can run slightly past one before the spring ends, exactly as Compose allows) and the
    /// turn angle in degrees.
    /// </summary>
    private bool TryGetMorph(out int step, out double progress, out double angle)
    {
        step = Step;
        progress = 0;
        angle = 0;
        if (!IsAnimated || MaterialMotion.GetIsReduced(this))
            return false;

        var elapsed = _clock.ElapsedMilliseconds;
        var completed = (long)(elapsed / MorphMilliseconds);
        // Start the cycle at the selected step so Step stays meaningful while animating.
        step = (int)((completed + Step - 1) % LoadingIndicatorShapes.Count) + 1;
        progress = Spring(elapsed - completed * MorphMilliseconds);
        // Compose turns the indicator by the spring's own quarter turn plus the quarter turn banked
        // for each completed morph, and adds one slow turn every 4666 ms. The banked turns must not
        // wrap on their own: the spring's quarter turn hands over to the next banked one at each
        // morph boundary, so the turn never jumps. The phase is shifted so the first frame is the
        // upright Step outline, which is what the control shows when motion is unavailable.
        var turn = elapsed / RotationMilliseconds;
        angle = (completed * QuarterTurn + progress * QuarterTurn +
                 (turn - Math.Floor(turn)) * 360) % 360;
        return true;
    }

    /// <summary>
    /// The source morph spring, evaluated analytically: a 0.6-damped, 200-stiffness spring from 0
    /// to 1 that settles after <see cref="SpringMilliseconds"/> and is held at exactly 1 from then
    /// on, which is where Compose ends a spring animation.
    /// </summary>
    private static double Spring(double milliseconds)
    {
        if (milliseconds >= SpringMilliseconds)
            return 1;
        var seconds = milliseconds / 1000;
        var decay = Math.Exp(-SpringDamping * SpringFrequency * seconds);
        var phase = SpringDampedFrequency * seconds;
        return 1 - decay * (Math.Cos(phase) +
                            SpringDamping * SpringFrequency / SpringDampedFrequency * Math.Sin(phase));
    }

    /// <summary>
    /// How long Compose lets this spring run, transcribed from <c>estimateAnimationDurationMillis</c>
    /// for the under-damped case: the envelope of the displacement has to fall below one visibility
    /// threshold. For a 0 → 1 morph that is ≈ 298 ms, comfortably inside the 650 ms morph slot.
    /// </summary>
    private static double SpringDuration()
    {
        var root = -SpringDamping * SpringFrequency;
        // Displacement in threshold units: one whole morph is 1 / 0.1 = 10 thresholds.
        var displacement = -1 / SpringThreshold;
        var sineCoefficient = -root * displacement / SpringDampedFrequency;
        var envelope = Math.Sqrt(displacement * displacement + sineCoefficient * sineCoefficient);
        return 1000 * Math.Log(1 / envelope) / root;
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
