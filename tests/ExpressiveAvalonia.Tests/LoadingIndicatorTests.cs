using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using ExpressiveAvalonia.Controls;
using Xunit;

namespace ExpressiveAvalonia.Tests;

/// <summary>
/// Pinned LoadingIndicator.kt semantics: the indeterminate cycle owns one morph per 650 ms slot,
/// banks a quarter turn when the lower-damping spring settles, and a polygon-list change re-keys
/// only the morph index. The remembered morph progress and quarter-turn angle continue, and the
/// restarted effect animates from that progress with zero velocity because cancelling an
/// <c>Animatable</c> resets its velocity. The restarted infinite rotation tween keeps the
/// remembered value but re-anchors every 4666 ms repetition to it.
/// </summary>
public class LoadingIndicatorTests
{
    private const double Interval = LoadingIndicatorShapes.MorphIntervalMilliseconds;
    private static readonly double Settle = LoadingIndicatorShapes.MorphDurationMilliseconds;

    [Fact]
    public void CycleAdvancesOneMorphPerSlotAndBanksAtSettle()
    {
        var cycle = new LoadingIndicatorCycle();

        var start = cycle.At(0);
        Assert.Equal(0, start.Slot);
        Assert.Equal(0, start.Progress);
        Assert.Equal(0, start.Completed);
        Assert.Equal(0, start.Rotation);

        var mid = cycle.At(120);
        Assert.Equal(0, mid.Slot);
        Assert.InRange(mid.Progress, 0.5, 0.8);
        Assert.Equal(0, mid.Completed);

        // The source snaps the progress back to zero the moment the spring reports Finished and
        // keeps the banked quarter turn for the remainder of the slot.
        var settled = cycle.At(Settle);
        Assert.Equal(0, settled.Slot);
        Assert.Equal(0, settled.Progress);
        Assert.Equal(1, settled.Completed);

        var nextSlot = cycle.At(Interval);
        Assert.Equal(1, nextSlot.Slot);
        Assert.Equal(0, nextSlot.Progress);
        Assert.Equal(1, nextSlot.Completed);
        Assert.Equal(Interval / LoadingIndicatorShapes.RotationMilliseconds * 360,
            nextSlot.Rotation, 9);

        var nextSettled = cycle.At(Interval + Settle);
        Assert.Equal(1, nextSettled.Slot);
        Assert.Equal(2, nextSettled.Completed);
    }

    [Fact]
    public void SequenceReplacementRestartsTheIndexButKeepsTheBankedTurn()
    {
        var cycle = new LoadingIndicatorCycle();
        var elapsed = Interval + 400;
        var before = cycle.At(elapsed);
        Assert.Equal(1, before.Slot);
        Assert.Equal(0, before.Progress);
        Assert.Equal(2, before.Completed);

        cycle.Restart(elapsed);

        var after = cycle.At(elapsed);
        Assert.Equal(0, after.Slot);
        Assert.Equal(before.Progress, after.Progress, 12);
        Assert.Equal(before.Completed, after.Completed);
        Assert.Equal(before.Rotation, after.Rotation, 9);

        // The restarted loop waits the remainder of its own 650 ms interval before the next morph.
        var nextSlot = cycle.At(elapsed + Interval);
        Assert.Equal(1, nextSlot.Slot);
        Assert.Equal(before.Completed + 1, nextSlot.Completed);
    }

    [Fact]
    public void SequenceReplacementContinuesTheInFlightMorphFromItsPreservedProgress()
    {
        var cycle = new LoadingIndicatorCycle();
        const double midMorph = 120;
        var before = cycle.At(midMorph);
        Assert.True(before.Progress > 0.5);

        cycle.Restart(midMorph);

        var after = cycle.At(midMorph);
        Assert.Equal(before.Progress, after.Progress, 12);
        Assert.Equal(0, after.Slot);
        Assert.Equal(before.Completed, after.Completed);

        // Starting part-way through the spring settles sooner than a fresh morph, but the next
        // slot still begins one interval after the replacement.
        var restartedSettle = cycle.At(midMorph + Interval - 1);
        Assert.Equal(1, restartedSettle.Slot);
        Assert.Equal(0, restartedSettle.Progress);
        Assert.Equal(before.Completed + 1, restartedSettle.Completed);

        var nextSlot = cycle.At(midMorph + Interval);
        Assert.Equal(1, nextSlot.Slot);
        Assert.Equal(before.Completed + 1, nextSlot.Completed);
    }

    [Fact]
    public void ReplacementWhileRotatingRestartsTheInfiniteTweenFromTheRememberedValue()
    {
        var cycle = new LoadingIndicatorCycle();
        var quarterTurn = LoadingIndicatorShapes.RotationMilliseconds / 4;
        var before = cycle.At(quarterTurn);
        Assert.Equal(90, before.Rotation, 9);

        cycle.Restart(quarterTurn);

        // Animatable keeps its value across the cancelled effect, so the replacement begins at
        // the remembered angle instead of zero.
        Assert.Equal(before.Rotation, cycle.At(quarterTurn).Rotation, 9);

        // The restarted TargetBasedAnimation captured R0 as its initial value, and
        // VectorizedInfiniteRepeatableSpec reuses that initial value for every repetition: each
        // 4666 ms iteration is a linear R0 -> 360 sweep.
        Assert.Equal(225, cycle.At(quarterTurn + LoadingIndicatorShapes.RotationMilliseconds / 2)
            .Rotation, 6);
        Assert.True(cycle.At(quarterTurn + LoadingIndicatorShapes.RotationMilliseconds - 0.001)
            .Rotation > 359.9);

        // Repetition boundary: the value jumps back to R0, the pinned source behavior.
        Assert.Equal(90, cycle.At(quarterTurn + LoadingIndicatorShapes.RotationMilliseconds)
            .Rotation, 6);
    }

    [Fact]
    public void RestartAnchorIsDroppedWhenTheClockStartsOver()
    {
        var cycle = new LoadingIndicatorCycle();
        cycle.Restart(1000);

        var restarted = cycle.At(0);
        Assert.Equal(0, restarted.Slot);
        Assert.Equal(0, restarted.Completed);
        Assert.Equal(0, restarted.Rotation);

        // A dropped anchor must not resurface when the new clock passes the old restart time,
        // neither for the morph index nor for the rotation tween.
        var later = cycle.At(1000);
        Assert.Equal(1, later.Slot);
        Assert.Equal(2, later.Completed);
        Assert.Equal(1000 / LoadingIndicatorShapes.RotationMilliseconds * 360, later.Rotation, 9);
    }

    [AvaloniaFact]
    public void ContainerShapeStretchMapsTheOutlineIntoTheBounds()
    {
        var target = new Rect(0, 0, 100, 50);

        var fill = LoadingIndicatorShapes.StretchGeometry(Square(), target, Stretch.Fill);
        AssertRect(new Rect(0, 0, 100, 50), fill.Bounds);

        var uniform = LoadingIndicatorShapes.StretchGeometry(Square(), target, Stretch.Uniform);
        AssertRect(new Rect(25, 0, 50, 50), uniform.Bounds);

        var cover = LoadingIndicatorShapes.StretchGeometry(Square(), target, Stretch.UniformToFill);
        AssertRect(new Rect(0, -25, 100, 100), cover.Bounds);

        // Stretch.None keeps the authored coordinate space, like Path.Data without a view box.
        var none = LoadingIndicatorShapes.StretchGeometry(Square(), target, Stretch.None);
        AssertRect(new Rect(0, 0, 10, 10), none.Bounds);
    }

    [AvaloniaFact]
    public void ContainerShapeFittingDoesNotMutateTheAuthoredGeometry()
    {
        var source = Square();
        var fitted = LoadingIndicatorShapes.StretchGeometry(source, new Rect(0, 0, 100, 50),
            Stretch.Fill);

        Assert.NotSame(source, fitted);
        Assert.Null(source.Transform);
        AssertRect(new Rect(0, 0, 10, 10), source.Bounds);
    }

    private static Geometry Square() => Geometry.Parse("M0,0 L10,0 L10,10 L0,10 Z");

    private static void AssertRect(Rect expected, Rect actual)
    {
        Assert.Equal(expected.X, actual.X, 6);
        Assert.Equal(expected.Y, actual.Y, 6);
        Assert.Equal(expected.Width, actual.Width, 6);
        Assert.Equal(expected.Height, actual.Height, 6);
    }
}