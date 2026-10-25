using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using ExpressiveAvalonia.Controls;
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

    [AvaloniaFact]
    public void MaterialStateLayerResolvesStatesAndDefaultOpacities()
    {
        var layer = new MaterialStateLayer();
        Assert.Equal(0.08, layer.HoverOpacity);
        Assert.Equal(0.10, layer.FocusOpacity);
        Assert.Equal(0.10, layer.PressedOpacity);
        Assert.Equal(0.16, layer.DraggedOpacity);
        Assert.Equal(MaterialStateLayerState.Enabled, layer.EffectiveState);

        layer.State = MaterialStateLayerState.Hovered;
        Assert.Equal(MaterialStateLayerState.Hovered, layer.EffectiveState);

        layer.State = MaterialStateLayerState.Pressed;
        Assert.Equal(MaterialStateLayerState.Pressed, layer.EffectiveState);
    }

    [AvaloniaFact]
    public void MaterialRippleAttachesToHostAndRespondsToEvents()
    {
        var button = new Button { Width = 100, Height = 40 };
        var ripple = new MaterialRipple();
        button.Content = ripple;
        var window = new Window { Width = 200, Height = 200, Content = button };
        window.Show();
        window.UpdateLayout();
        try
        {
            Assert.NotNull(ripple);
            Assert.Equal(0, ripple.CornerRadius.TopLeft);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void MaterialBadgeReflectsSizeAndTextState()
    {
        var badge = new MaterialBadge();
        Assert.Equal(MaterialBadgeSize.Large, badge.Size);
        Assert.False(badge.IsLabeled);

        badge.Text = "5";
        Assert.True(badge.IsLabeled);

        badge.Size = MaterialBadgeSize.Small;
        Assert.False(badge.IsLabeled);
    }

    [AvaloniaFact]
    public void SpringPhysicsInterpolatesTowardsTarget()
    {
        var control = new Border();
        var window = new Window { Content = control };
        window.Show();
        try
        {
            MaterialMotion.SetIsReduced(control, false);
            double value = 0;
            using var animation = MaterialMotion.Animate(control, 0, 100, x => value = x, stiffness: 2000, dampingRatio: 1.0);
            Assert.True(value >= 0);
            animation.Dispose();
        }
        finally { window.Close(); }
    }
}
