using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using ExpressiveAvalonia.Theming;
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
}
