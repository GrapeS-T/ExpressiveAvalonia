using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Theming;
using Xunit;

namespace ExpressiveAvalonia.Tests;

public class SliderBehaviorTests
{
    private static Window Show(Control control)
    {
        control.HorizontalAlignment = HorizontalAlignment.Center;
        control.VerticalAlignment = VerticalAlignment.Center;
        var window = new Window { Width = 600, Height = 400, Content = control, UseLayoutRounding = false };
        window.Show();
        window.UpdateLayout();
        return window;
    }

    private static Point Center(Control control, Visual relativeTo) =>
        control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), relativeTo)!.Value;

    [AvaloniaFact]
    public void RtlArrowAndDragMoveTheHandleInThePhysicalDirection()
    {
        var slider = new Slider { Width = 400, Value = 40, FlowDirection = FlowDirection.RightToLeft };
        var window = Show(slider);
        try
        {
            var thumb = slider.GetVisualDescendants().OfType<Thumb>().Single();
            var before = Center(thumb, window);
            Assert.True(before.X > Center(slider, window).X);
            slider.Focus(NavigationMethod.Tab);
            window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, null);
            window.KeyRelease(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, null);
            window.UpdateLayout();
            Assert.Equal(39, slider.Value);
            Assert.True(Center(thumb, window).X > before.X);
            var grab = Center(thumb, window);
            window.MouseDown(grab, MouseButton.Left);
            window.MouseMove(grab + new Vector(40, 0));
            window.MouseUp(grab + new Vector(40, 0), MouseButton.Left);
            Assert.True(slider.Value < 39);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void RtlExplicitStopsAdvanceAndReverseInVisualOrder()
    {
        var slider = new Slider
        {
            Width = 400, Value = 40, FlowDirection = FlowDirection.RightToLeft,
            Ticks = new([0, 10, 40, 100]), IsSnapToTickEnabled = true
        };
        var window = Show(slider);
        try
        {
            slider.Focus(NavigationMethod.Tab);
            Press(Key.Right);
            Assert.Equal(10, slider.Value);
            Press(Key.Left);
            Assert.Equal(40, slider.Value);
            slider.IsDirectionReversed = true;
            Press(Key.Right);
            Assert.Equal(100, slider.Value);
            Press(Key.Home);
            Assert.Equal(0, slider.Value);
            void Press(Key key)
            {
                window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, null);
                window.KeyRelease(key, RawInputModifiers.None, PhysicalKey.None, null);
            }
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void RangeRtlEndpointsAgreeWithPointerAndKeyboardDirection()
    {
        var range = new MaterialRangeSlider { Width = 400, LowerValue = 20, UpperValue = 70, FlowDirection = FlowDirection.RightToLeft };
        var window = Show(range);
        try
        {
            var lower = range.GetVisualDescendants().OfType<Thumb>().Single(t => t.Name == "PART_LowerThumb");
            var upper = range.GetVisualDescendants().OfType<Thumb>().Single(t => t.Name == "PART_UpperThumb");
            Assert.True(Center(lower, window).X > Center(upper, window).X);
            var grab = Center(lower, window);
            window.MouseDown(grab, MouseButton.Left);
            window.MouseMove(grab + new Vector(40, 0));
            window.MouseUp(grab + new Vector(40, 0), MouseButton.Left);
            Assert.True(range.LowerValue < 20);
            Assert.Equal(70, range.UpperValue);
            lower.Focus(NavigationMethod.Tab);
            var value = range.LowerValue;
            window.KeyPress(Key.Left, RawInputModifiers.None, PhysicalKey.ArrowLeft, null);
            Assert.True(range.LowerValue > value);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void VerticalUpAndPointerMotionBothIncreaseTheValue()
    {
        var slider = new Slider { Height = 240, Orientation = Orientation.Vertical, Value = 40 };
        var window = Show(slider);
        try
        {
            var thumb = slider.GetVisualDescendants().OfType<Thumb>().Single();
            var before = Center(thumb, window);
            slider.Focus(NavigationMethod.Tab);
            window.KeyPress(Key.Up, RawInputModifiers.None, PhysicalKey.ArrowUp, null);
            window.UpdateLayout();
            Assert.Equal(41, slider.Value);
            Assert.True(Center(thumb, window).Y < before.Y);
            var grab = Center(thumb, window);
            window.MouseDown(grab, MouseButton.Left);
            window.MouseMove(grab - new Vector(0, 30));
            window.MouseUp(grab - new Vector(0, 30), MouseButton.Left);
            Assert.True(slider.Value > 41);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void DiscreteClicksSelectPaintedStopsInBothDirections()
    {
        foreach (var rtl in new[] { false, true })
        {
            var slider = new Slider
            {
                Width = 400, Value = 0, TickFrequency = 30, IsSnapToTickEnabled = true,
                FlowDirection = rtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight
            };
            MaterialAssist.SetControlSize(slider, MaterialControlSize.ExtraLarge);
            var window = Show(slider);
            try
            {
                foreach (var value in new[] { 60d, 30d, 90d, 0d, 100d })
                {
                    var x = value == 0 ? 2 : value == 100 ? 398 : 8 + value / 100 * 384;
                    var point = slider.TranslatePoint(new Point(x, slider.Bounds.Height / 2), window)!.Value;
                    window.MouseDown(point, MouseButton.Left);
                    window.MouseUp(point, MouseButton.Left);
                    window.UpdateLayout();
                    Assert.Equal(value, slider.Value);
                }
            }
            finally { window.Close(); }
        }
    }
}
