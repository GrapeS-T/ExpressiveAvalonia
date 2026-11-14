using System;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;

namespace ExpressiveAvalonia.Theming;

/// <summary>Which edge started a predictive back gesture.</summary>
public enum MaterialSwipeEdge
{
    /// <summary>A back gesture from the left edge of the window.</summary>
    Left,

    /// <summary>A back gesture from the right edge of the window.</summary>
    Right,
}

/// <summary>
/// The motion of one incoming or outgoing layer in a Material transition.
/// </summary>
/// <remarks>
/// Fractions are of the full content size, matching the pinned
/// <c>slideInHorizontally</c>/<c>slideOutHorizontally</c> lambdas. Alpha is a finite linear tween
/// in the pinned source; scale and slide use the no-bounce medium-low spring
/// (<c>Spring.StiffnessMediumLow</c>, damping 1), which is the default of
/// <c>fadeIn</c>/<c>slideInHorizontally</c>/<c>scaleIn</c>.
/// </remarks>
public sealed record MaterialTransitionSpec
{
    public MaterialTransitionSpec(
        double enterFromAlpha,
        double exitToAlpha,
        double enterFromOffsetFraction,
        double exitToOffsetFraction,
        double enterFromScale,
        double exitToScale,
        double enterFadeSeconds,
        double enterFadeDelaySeconds,
        double exitFadeSeconds,
        double exitFadeDelaySeconds,
        double targetContentZIndex = 0)
    {
        if (!double.IsFinite(enterFromAlpha) || !double.IsFinite(exitToAlpha) ||
            !double.IsFinite(enterFromOffsetFraction) || !double.IsFinite(exitToOffsetFraction) ||
            !double.IsFinite(enterFromScale) || enterFromScale <= 0 ||
            !double.IsFinite(exitToScale) || exitToScale <= 0 ||
            !double.IsFinite(enterFadeSeconds) || enterFadeSeconds < 0 ||
            !double.IsFinite(enterFadeDelaySeconds) || enterFadeDelaySeconds < 0 ||
            !double.IsFinite(exitFadeSeconds) || exitFadeSeconds < 0 ||
            !double.IsFinite(exitFadeDelaySeconds) || exitFadeDelaySeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(enterFromAlpha),
                "Transition values must be finite and non-negative where applicable.");
        }

        EnterFromAlpha = enterFromAlpha;
        ExitToAlpha = exitToAlpha;
        EnterFromOffsetFraction = enterFromOffsetFraction;
        ExitToOffsetFraction = exitToOffsetFraction;
        EnterFromScale = enterFromScale;
        ExitToScale = exitToScale;
        EnterFadeSeconds = enterFadeSeconds;
        EnterFadeDelaySeconds = enterFadeDelaySeconds;
        ExitFadeSeconds = exitFadeSeconds;
        ExitFadeDelaySeconds = exitFadeDelaySeconds;
        TargetContentZIndex = targetContentZIndex;
    }

    /// <summary>Alpha the incoming layer starts at; it always finishes at 1.</summary>
    public double EnterFromAlpha { get; }

    /// <summary>Alpha the outgoing layer finishes at.</summary>
    public double ExitToAlpha { get; }

    /// <summary>Signed horizontal slide the incoming layer starts at, as a fraction of full width.</summary>
    public double EnterFromOffsetFraction { get; }

    /// <summary>Signed horizontal slide the outgoing layer finishes at, as a fraction of full width.</summary>
    public double ExitToOffsetFraction { get; }

    /// <summary>Scale the incoming layer starts at; it always finishes at 1.</summary>
    public double EnterFromScale { get; }

    /// <summary>Scale the outgoing layer finishes at.</summary>
    public double ExitToScale { get; }

    /// <summary>Linear alpha tween duration of the incoming layer.</summary>
    public double EnterFadeSeconds { get; }

    /// <summary>Hold before the incoming alpha tween starts.</summary>
    public double EnterFadeDelaySeconds { get; }

    /// <summary>Linear alpha tween duration of the outgoing layer.</summary>
    public double ExitFadeSeconds { get; }

    /// <summary>Hold before the outgoing alpha tween starts.</summary>
    public double ExitFadeDelaySeconds { get; }

    /// <summary>Compose target content z-index; negative puts the incoming layer behind.</summary>
    public double TargetContentZIndex { get; }

    /// <summary>Duration of the longest alpha tween, used by hosts to finish outgoing layers.</summary>
    public double ExitLifetimeSeconds => ExitFadeSeconds + ExitFadeDelaySeconds;
}

/// <summary>One side of a gesture-driven predictive back transform.</summary>
public readonly record struct MaterialPredictiveBackSurface(
    double Alpha,
    double Scale,
    double OffsetX,
    double OffsetY,
    Color? Veil);

/// <summary>Initial and target transforms for a predictive back gesture at one progress value.</summary>
public readonly record struct MaterialPredictiveBackFrame(
    MaterialPredictiveBackSurface Initial,
    MaterialPredictiveBackSurface Target,
    double PivotFractionX);

/// <summary>
/// Material 3 transition specifications and the pure math behind predictive back.
/// </summary>
/// <remarks>
/// <para>
/// The pinned API returns Compose <c>ContentTransform</c> objects. Avalonia content presenters
/// cannot consume those, so this port exposes the same public transitions as
/// <see cref="MaterialTransitionSpec"/> values (properties, durations, delays, z-index) and keeps
/// the predictive-back math as pure functions. <see cref="MaterialTransitionHost"/> in
/// <c>ExpressiveAvalonia.Controls</c> plays the specs with the shared
/// <see cref="MaterialMotion"/> engine.
/// </para>
/// <para>
/// There is deliberately no navigation router: the application decides when a destination
/// changes, calls the host, and supplies its own scrim colour for predictive back. Reduced motion
/// is handled by the playing host through the inherited
/// <see cref="MaterialMotion.IsReducedProperty"/> policy.
/// </para>
/// </remarks>
public static class MaterialTransitions
{
    /// <summary>Forward horizontal shared-axis transition: the destination enters from the end.</summary>
    public static class HorizontalSharedAxis
    {
        /// <summary>Forward (deeper) horizontal shared axis.</summary>
        public static MaterialTransitionSpec Forward() => new(
            enterFromAlpha: 0,
            exitToAlpha: 0,
            enterFromOffsetFraction: 0.2,
            exitToOffsetFraction: -0.2,
            enterFromScale: 1,
            exitToScale: 1,
            enterFadeSeconds: 0.09,
            enterFadeDelaySeconds: 0.05,
            exitFadeSeconds: 0.09,
            exitFadeDelaySeconds: 0.05,
            targetContentZIndex: 0);

        /// <summary>Backward (up) horizontal shared axis.</summary>
        public static MaterialTransitionSpec Backward() => new(
            enterFromAlpha: 0,
            exitToAlpha: 0,
            enterFromOffsetFraction: -0.2,
            exitToOffsetFraction: 0.2,
            enterFromScale: 1,
            // The pinned backward exit adds scaleOut(targetScale = 1f): a no-op scale animation.
            exitToScale: 1,
            enterFadeSeconds: 0.09,
            enterFadeDelaySeconds: 0.05,
            exitFadeSeconds: 0.09,
            exitFadeDelaySeconds: 0.05,
            targetContentZIndex: -10);

        /// <summary>
        /// Gesture-phase transforms for a horizontal shared-axis predictive back. The caller applies
        /// <see cref="MaterialPredictiveBackFrame.Initial"/> to the current destination and
        /// <see cref="MaterialPredictiveBackFrame.Target"/> to the destination being revealed.
        /// </summary>
        /// <param name="progress">Raw back progress in [0, 1].</param>
        /// <param name="rawYDelta">Touch Y movement since the gesture started.</param>
        /// <param name="fullSize">Full content size of the transition container.</param>
        /// <param name="swipeEdge">Edge that started the gesture.</param>
        /// <param name="displayBoundsMargin">Margin that keeps the scaled content inside the display.</param>
        /// <param name="veil">Optional scrim colour drawn over the revealed destination; use <see cref="MaterialTransitionDefaults.ScrimColor"/>.</param>
        public static MaterialPredictiveBackFrame PredictiveBack(
            double progress,
            double rawYDelta,
            Size fullSize,
            MaterialSwipeEdge swipeEdge,
            double displayBoundsMargin = MaterialTransitionDefaults.DisplayBoundsMargin,
            Color? veil = null)
        {
            var eased = PredictiveBackEase(progress);
            var currentScale = 1 - (eased * ScaleProgressMultiplier);
            // The pinned source keeps the right-edge gesture near the centre and the other edge
            // near the end, because the content scales toward the edge that is being dragged.
            var pivotFractionX = swipeEdge == MaterialSwipeEdge.Right
                ? PivotFractionXRightEdge
                : PivotFractionXDefault;

            var initialOffset = CalculateSlideOffset(
                fullSize,
                currentScale,
                rawYDelta,
                displayBoundsMargin,
                pivotFractionX);
            var targetScale = currentScale - TargetScaleDelta;
            var targetOffset = CalculateSlideOffset(
                fullSize,
                currentScale,
                rawYDelta,
                displayBoundsMargin);
            var targetX = (int)(-0.2 * fullSize.Width);

            return new MaterialPredictiveBackFrame(
                new MaterialPredictiveBackSurface(1, currentScale, initialOffset.X, initialOffset.Y, null),
                new MaterialPredictiveBackSurface(1, targetScale, targetX, targetOffset.Y, veil),
                pivotFractionX);
        }
    }

    /// <summary>Fade-through transition: the outgoing layer fades first, then the incoming scales in.</summary>
    public static class FadeThrough
    {
        /// <summary>Commit-phase fade-through transition.</summary>
        public static MaterialTransitionSpec Create() => new(
            enterFromAlpha: 0,
            exitToAlpha: 0,
            enterFromOffsetFraction: 0,
            exitToOffsetFraction: 0,
            enterFromScale: 0.8,
            exitToScale: 1.05,
            enterFadeSeconds: 0.21,
            enterFadeDelaySeconds: 0.09,
            exitFadeSeconds: 0.09,
            exitFadeDelaySeconds: 0,
            targetContentZIndex: 0);

        /// <summary>Gesture-phase transforms for fade-through predictive back.</summary>
        /// <param name="progress">Raw back progress in [0, 1].</param>
        /// <param name="rawYDelta">Touch Y movement since the gesture started.</param>
        /// <param name="fullSize">Full content size of the transition container.</param>
        /// <param name="displayBoundsMargin">Margin that keeps the scaled content inside the display.</param>
        public static MaterialPredictiveBackFrame PredictiveBack(
            double progress,
            double rawYDelta,
            Size fullSize,
            double displayBoundsMargin = MaterialTransitionDefaults.DisplayBoundsMargin)
        {
            var eased = PredictiveBackEase(progress);
            var currentScale = 1 - (eased * ScaleProgressMultiplier);
            var initialOffset = CalculateSlideOffset(
                fullSize,
                currentScale,
                rawYDelta,
                displayBoundsMargin);

            return new MaterialPredictiveBackFrame(
                new MaterialPredictiveBackSurface(1, currentScale, initialOffset.X, initialOffset.Y, null),
                new MaterialPredictiveBackSurface(0, 0.8, 0, 0, null),
                0.5);
        }
    }

    /// <summary>
    /// Horizontal offset caused by scaling around a pivot that is not the exact centre, plus the
    /// damped vertical drag offset. Mirrors the pinned <c>calculateSlideOffset</c>, including its
    /// truncation to integer pixels.
    /// </summary>
    public static Point CalculateSlideOffset(
        Size fullSize,
        double currentScale,
        double rawYDelta,
        double displayBoundsMargin,
        double pivotFractionX = 0.5)
    {
        if (!double.IsFinite(currentScale) || currentScale <= 0 ||
            !double.IsFinite(rawYDelta) || !double.IsFinite(displayBoundsMargin) ||
            !double.IsFinite(pivotFractionX) || !double.IsFinite(fullSize.Width) ||
            !double.IsFinite(fullSize.Height))
        {
            throw new ArgumentOutOfRangeException(nameof(currentScale),
                "Predictive back inputs must be finite and the scale positive.");
        }

        var horizontal = (1 - currentScale) * (pivotFractionX - 0.5) * fullSize.Width;
        var vertical = CalculateDampedVerticalOffset(
            rawYDelta,
            fullSize.Height,
            currentScale,
            displayBoundsMargin);
        return new Point((int)horizontal, (int)vertical);
    }

    /// <summary>
    /// Quadratic-decelerated vertical drag, clamped so the scaled content never leaves the display
    /// margins. Mirrors the pinned <c>calculateDampedVerticalOffset</c>.
    /// </summary>
    public static double CalculateDampedVerticalOffset(
        double rawYDelta,
        double containerHeight,
        double currentScale,
        double displayBoundsMargin)
    {
        if (!double.IsFinite(rawYDelta) || !double.IsFinite(containerHeight) ||
            !double.IsFinite(currentScale) || !double.IsFinite(displayBoundsMargin))
        {
            throw new ArgumentOutOfRangeException(nameof(rawYDelta),
                "Damped offset inputs must be finite.");
        }

        if (containerHeight <= 0)
            return 0;

        var maxDeltaY = Math.Max(0,
            ((containerHeight - (containerHeight * currentScale)) / 2) - displayBoundsMargin);
        var deltaYRatio = Math.Min(1, Math.Abs(rawYDelta) / (containerHeight / 2));
        var interpolatedRatio = 1 - ((1 - deltaYRatio) * (1 - deltaYRatio)); // quadratic decelerate
        var yDirection = rawYDelta < 0 ? -1d : 1d;
        return maxDeltaY * interpolatedRatio * yDirection;
    }

    /// <summary>The pinned predictive-back easing, <c>CubicBezierEasing(0.1, 0.1, 0, 1)</c>.</summary>
    public static double PredictiveBackEase(double progress)
    {
        if (!double.IsFinite(progress))
            throw new ArgumentOutOfRangeException(nameof(progress));
        return PredictiveBackEasing.Ease(Math.Clamp(progress, 0, 1));
    }

    private static readonly SplineEasing PredictiveBackEasing = new(0.1, 0.1, 0, 1);
    private const double ScaleProgressMultiplier = 0.15;
    private const double TargetScaleDelta = 0.05;
    private const double PivotFractionXRightEdge = 0.5;
    private const double PivotFractionXDefault = 0.9;
}

/// <summary>Default values for <see cref="MaterialTransitions"/>.</summary>
public static class MaterialTransitionDefaults
{
    /// <summary>Margin in device-independent pixels kept clear of display bounds while scaling.</summary>
    public const double DisplayBoundsMargin = 8;

    /// <summary>
    /// The recommended scrim colour for navigation transitions: the scheme scrim with an alpha
    /// that adapts to the surface luminance (0.2 on light surfaces, 0.8 on dark surfaces).
    /// </summary>
    public static Color ScrimColor(Color scrim, Color surface)
    {
        var alpha = Luminance(surface) < 0.5 ? 0.8 : 0.2;
        return Color.FromArgb((byte)Math.Round(255 * alpha), scrim.R, scrim.G, scrim.B);
    }

    /// <summary>
    /// Resolves <see cref="ScrimColor(Color, Color)"/> from the nearest <c>MaterialScrimColor</c>
    /// and <c>MaterialSurfaceColor</c> resources.
    /// </summary>
    /// <param name="owner">Element whose resource scope supplies the scheme.</param>
    /// <param name="color">The adapted scrim colour.</param>
    /// <returns>False when the colour scheme resources are not available.</returns>
    public static bool TryGetScrimColor(Control owner, out Color color)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (owner.TryFindResource("MaterialScrimColor", out var scrim) && scrim is Color scrimColor &&
            owner.TryFindResource("MaterialSurfaceColor", out var surface) && surface is Color surfaceColor)
        {
            color = ScrimColor(scrimColor, surfaceColor);
            return true;
        }

        color = default;
        return false;
    }

    // Compose Color.luminance: linearized sRGB channels with Rec. 709 coefficients.
    private static double Luminance(Color color) =>
        (0.2126 * Linear(color.R)) + (0.7152 * Linear(color.G)) + (0.0722 * Linear(color.B));

    private static double Linear(byte channel)
    {
        var value = channel / 255d;
        return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }
}