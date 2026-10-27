using System.Threading;
using System.Diagnostics;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Theming;
using ExpressiveAvalonia.Tokens;
using Xunit;

namespace ExpressiveAvalonia.Tests;

public class MotionBehaviorTests
{
    [AvaloniaFact]
    public void ReducedMotionImmediatelyCompletesAndRetargets()
    {
        var control = new Border();
        var window = new Window { Content = control };
        window.Show();
        try
        {
            MaterialMotion.SetIsReduced(control, true);
            double value = 0;
            using var animation = MaterialMotion.Animate(control, 0, 10, x => value = x);
            Assert.Equal(10, value);
            animation.Retarget(-5);
            Assert.Equal(-5, value);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void DetachingSpringCompletesTargetWithoutFurtherTicks()
    {
        var control = new Border();
        var window = new Window { Content = control };
        window.Show();
        try
        {
            MaterialMotion.SetIsReduced(control, false);
            double value = 0;
            using var animation = MaterialMotion.Animate(control, 0, 10, x => value = x);
            window.Content = null;
            Assert.Equal(10, value);
            animation.Retarget(20);
            Assert.Equal(20, value);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void DetachCancellationDoesNotApplyTheCancelledTweenTarget()
    {
        var control = new Border();
        var window = new Window { Content = control };
        MaterialMotion.TweenAnimation? animation = null;
        // The component owns its detach handler before the scheduler subscribes. The event's
        // invocation snapshot still includes the scheduler after this handler cancels it.
        control.DetachedFromVisualTree += (_, _) => animation?.Dispose();
        window.Show();
        try
        {
            double value = 0;
            MaterialMotion.SetIsReduced(control, false);
            animation = MaterialMotion.Tween(control, 0, 10, 1, 1, x => value = x);
            window.Content = null;
            Assert.Equal(0, value);
        }
        finally
        {
            animation?.Dispose();
            window.Close();
        }
    }


    [AvaloniaFact]
    public void LiveSchemeChangeRetunesSemanticSpringWithoutDroppingState()
    {
        var control = new Border();
        var window = new Window { Content = control };
        window.Show();
        try
        {
            MaterialMotion.SetIsReduced(control, false);
            MaterialMotion.SetScheme(control, MaterialMotionScheme.Standard);
            using var animation = MaterialMotion.Animate(control, 0, 100, _ => { },
                MaterialSpring.SpatialDefault, visibilityThreshold: 0.01, initialVelocity: 100);
            Assert.Equal(
                MaterialSpringTokens.Get(MaterialSpring.SpatialDefault, MaterialMotionScheme.Standard),
                animation.Coefficients);

            // Freeze the outgoing clock so assertion/JIT latency cannot advance the old segment
            // between sampling it and changing the scheme. The new segment resumes normally.
            Thread.Sleep(30);
            const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
            var clock = (Stopwatch)typeof(MaterialMotion.SpringAnimation)
                .GetField("_clock", fields)!.GetValue(animation)!;
            clock.Stop();
            var (positionBefore, velocityBefore) = animation.SampleState();
            Assert.InRange(positionBefore, 0.5, 99.5);

            MaterialMotion.SetScheme(control, MaterialMotionScheme.Expressive);
            Assert.Equal(
                MaterialSpringTokens.Get(MaterialSpring.SpatialDefault, MaterialMotionScheme.Expressive),
                animation.Coefficients);
            Assert.True(clock.IsRunning);
            clock.Stop();
            clock.Reset();
            var (positionAfter, velocityAfter) = animation.SampleState();
            Assert.Equal(positionBefore, positionAfter, precision: 10);
            Assert.Equal(velocityBefore, velocityAfter, precision: 10);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void SemanticSpringResolvesNewSchemeAfterFinishAndOnRetarget()
    {
        var control = new Border();
        var window = new Window { Content = control };
        window.Show();
        try
        {
            MaterialMotion.SetIsReduced(control, true);
            using var animation = MaterialMotion.Animate(control, 0, 10, _ => { }, MaterialSpring.SpatialFast);
            Assert.Equal(
                MaterialSpringTokens.Get(MaterialSpring.SpatialFast, MaterialMotionScheme.Standard),
                animation.Coefficients);

            MaterialMotion.SetScheme(control, MaterialMotionScheme.Expressive);
            Assert.Equal(
                MaterialSpringTokens.Get(MaterialSpring.SpatialFast, MaterialMotionScheme.Expressive),
                animation.Coefficients);

            animation.Retarget(20);
            Assert.Equal(
                MaterialSpringTokens.Get(MaterialSpring.SpatialFast, MaterialMotionScheme.Expressive),
                animation.Coefficients);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ExplicitSpringCoefficientsIgnoreLiveSchemeChange()
    {
        var control = new Border();
        var window = new Window { Content = control };
        window.Show();
        try
        {
            MaterialMotion.SetIsReduced(control, false);
            using var animation = MaterialMotion.Animate(control, 0, 10, _ => { }, 1400d, 0.9d);
            MaterialMotion.SetScheme(control, MaterialMotionScheme.Expressive);
            Assert.Equal((1400d, 0.9d), animation.Coefficients);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void MaterialBadgeReflectsSizeAndTextState()
    {
        var badge = new MaterialBadge();

        badge.Text = "5";
        Assert.True(badge.IsLabeled);

        badge.Size = MaterialBadgeSize.Small;
        Assert.False(badge.IsLabeled);
    }

}
