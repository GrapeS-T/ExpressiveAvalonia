using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using ExpressiveAvalonia.Controls;
using Xunit;

namespace ExpressiveAvalonia.Tests;

public class GeneralItemBehaviorTests
{
    [AvaloniaFact]
    public void KeyboardRepeatAndCancelledGesturesDoNotInvoke()
    {
        var tile = new MaterialGeneralItem { Width = 120, Height = 80 };
        var calls = 0;
        tile.Invoked += (_, _) => calls++;
        var panel = new StackPanel { Children = { tile } };
        var window = new Window { Width = 300, Height = 240, Content = panel };
        window.Show();
        window.UpdateLayout();
        try
        {
            Assert.True(tile.Focus());
            window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.None, null);
            window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.None, null);
            Assert.Equal(0, calls);
            window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.None, null);
            Assert.Equal(1, calls);
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.None, null);
            tile.IsEnabled = false;
            tile.IsEnabled = true;
            tile.Focus();
            window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.None, null);
            Assert.Equal(1, calls);
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.None, null);
            panel.Children.Remove(tile);
            panel.Children.Add(tile);
            window.UpdateLayout();
            tile.Focus();
            window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.None, null);
            Assert.Equal(1, calls);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void PointerRequiresOwnUncancelledPrimaryPressAndInsideRelease()
    {
        // Headless's curved-path stub excludes valid interior points. Exercise the real
        // input/capture lifecycle with a rectangular clip; rounded hit testing is native-smoked.
        var tile = new MaterialGeneralItem { Width = 120, Height = 80, TileCornerRadius = new CornerRadius(0) };
        var calls = 0;
        tile.Invoked += (_, _) => calls++;
        var window = new Window { Width = 300, Height = 240, Content = tile };
        window.Show();
        window.UpdateLayout();
        try
        {
            var inside = tile.TranslatePoint(new Point(20, 20), window)!.Value;
            var outside = new Point(290, 220);
            window.MouseDown(inside, MouseButton.Right);
            window.MouseUp(inside, MouseButton.Right);
            Assert.Equal(0, calls);
            window.MouseDown(inside, MouseButton.Left);
            window.MouseUp(outside, MouseButton.Left);
            Assert.Equal(0, calls);
            window.MouseDown(inside, MouseButton.Left);
            tile.IsEnabled = false;
            tile.IsEnabled = true;
            window.MouseUp(inside, MouseButton.Left);
            Assert.Equal(0, calls);
            window.MouseDown(inside, MouseButton.Left);
            window.MouseUp(inside, MouseButton.Left);
            Assert.Equal(1, calls);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ExplicitNameEqualToOriginalCaptionRemainsExplicit()
    {
        var tile = new MaterialGeneralItem { Label = "Original" };
        var window = new Window { Content = tile };
        window.Show();
        window.UpdateLayout();
        try
        {
            AutomationProperties.SetName(tile, "Original");
            tile.Label = "Changed";
            Assert.Equal("Original", AutomationProperties.GetName(tile));
        }
        finally { window.Close(); }
    }
}
