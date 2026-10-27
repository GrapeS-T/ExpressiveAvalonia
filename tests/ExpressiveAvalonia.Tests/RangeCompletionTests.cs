using System.ComponentModel;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Controls;
using Xunit;

namespace ExpressiveAvalonia.Tests;

/// <summary>
/// Interaction-finished contract for <see cref="MaterialRangeSlider"/>: source Slider.kt invokes
/// onValueChangeFinished after a drag stop, after a tap release, on the key-up of the keys that
/// move an endpoint and after a successful automation value change.
/// </summary>
public class RangeCompletionTests
{
    private static MaterialRangeSlider NewRange(double lower = 20, double upper = 80) => new()
    {
        // Fixed geometry keeps the press points independent of the window size.
        Width = 400,
        Height = 56,
        Minimum = 0,
        Maximum = 100,
        LowerValue = lower,
        UpperValue = upper,
    };

    private static Window Show(Control control)
    {
        var window = new Window { Width = 600, Height = 400, Content = control };
        window.Show();
        window.UpdateLayout();
        return window;
    }

    private static Thumb Thumb(MaterialRangeSlider range, string name) =>
        range.GetVisualDescendants().OfType<Thumb>().Single(t => t.Name == name);

    private static Point Center(Visual visual, Window window) =>
        visual.TranslatePoint(new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), window)
        ?? throw new InvalidOperationException("The visual is not in the window.");

    private static void Press(Window window, Key key) =>
        window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, null);

    private static void Release(Window window, Key key) =>
        window.KeyRelease(key, RawInputModifiers.None, PhysicalKey.None, null);

    [AvaloniaFact]
    public void ProgrammaticAndBoundUpdatesDoNotComplete()
    {
        var host = new Grid();
        var window = Show(host);
        try
        {
            var range = NewRange();
            host.Children.Add(range);
            window.UpdateLayout();

            var completed = 0;
            range.ValueChangeCompleted += (_, _) => completed++;

            range.LowerValue = 40;
            range.UpperValue = 70;
            range.Maximum = 60;
            Assert.Equal(40, range.LowerValue);
            Assert.Equal(60, range.UpperValue);
            Assert.Equal(0, completed);
            host.Children.Remove(range);

            var model = new RangeModel { Lower = 20, Upper = 80 };
            var bound = new MaterialRangeSlider { Width = 400, Height = 56, Minimum = 0, Maximum = 100 };
            bound.Bind(MaterialRangeSlider.LowerValueProperty,
                new Binding(nameof(RangeModel.Lower)) { Source = model, Mode = BindingMode.TwoWay });
            bound.Bind(MaterialRangeSlider.UpperValueProperty,
                new Binding(nameof(RangeModel.Upper)) { Source = model, Mode = BindingMode.TwoWay });
            host.Children.Add(bound);
            window.UpdateLayout();

            var boundCompleted = 0;
            bound.ValueChangeCompleted += (_, _) => boundCompleted++;

            model.Lower = 30;
            model.Upper = 90;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(30, bound.LowerValue);
            Assert.Equal(90, bound.UpperValue);
            Assert.Equal(0, boundCompleted);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void TapOnTrackCompletesOnceAfterTheValueIsApplied()
    {
        var range = NewRange();
        var window = Show(range);
        try
        {
            RoutedEventArgs? raised = null;
            var completed = 0;
            var observed = double.NaN;
            range.ValueChangeCompleted += (_, e) =>
            {
                completed++;
                raised = e;
                observed = range.LowerValue;
            };

            var canvas = range.GetVisualDescendants().OfType<Canvas>().Single(c => c.Name == "PART_TrackCanvas");
            // Left of the midpoint picks the lower endpoint and moves it toward the press point.
            var point = Center(canvas, window) + new Vector(-60, 0);
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);

            Assert.Equal(1, completed);
            Assert.NotNull(raised);
            Assert.Same(MaterialRangeSlider.ValueChangeCompletedEvent, raised!.RoutedEvent);
            Assert.Same(range, raised.Source);
            Assert.Equal(range.LowerValue, observed);
            Assert.True(observed > 20);
            Assert.True(observed < 80);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void DragCompletesOnceOnReleaseAfterTheFinalValue()
    {
        var range = NewRange();
        var window = Show(range);
        try
        {
            var completed = new List<double>();
            range.ValueChangeCompleted += (_, _) => completed.Add(range.LowerValue);

            var start = Center(Thumb(range, "PART_LowerThumb"), window);
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(start + new Vector(40, 0));
            window.UpdateLayout();
            window.MouseMove(start + new Vector(80, 0));
            window.UpdateLayout();

            Assert.Empty(completed);
            var final = range.LowerValue;
            Assert.True(final > 20);

            window.MouseUp(start + new Vector(80, 0), MouseButton.Left);
            Assert.Single(completed);
            Assert.Equal(final, completed[0]);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void DisableAndDetachCancelThePointerInteractionWithoutCompleting()
    {
        var range = NewRange();
        var host = new Grid { Children = { range } };
        var window = Show(host);
        try
        {
            var completed = 0;
            range.ValueChangeCompleted += (_, _) => completed++;

            var point = Center(Thumb(range, "PART_LowerThumb"), window);
            window.MouseDown(point, MouseButton.Left);
            window.MouseMove(point + new Vector(40, 0));
            range.IsEnabled = false;
            window.MouseUp(point + new Vector(40, 0), MouseButton.Left);
            Assert.Equal(0, completed);

            range.IsEnabled = true;
            window.UpdateLayout();
            point = Center(Thumb(range, "PART_LowerThumb"), window);
            window.MouseDown(point, MouseButton.Left);
            window.MouseMove(point + new Vector(40, 0));
            host.Children.Remove(range);
            window.MouseUp(point + new Vector(40, 0), MouseButton.Left);
            Assert.Equal(0, completed);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void KeyboardCompletesOnlyOnTheMatchingKeyUp()
    {
        var range = NewRange();
        range.SmallChange = 10;
        var window = Show(range);
        try
        {
            var completed = 0;
            var observed = double.NaN;
            range.ValueChangeCompleted += (_, _) =>
            {
                completed++;
                observed = range.LowerValue;
            };

            var lower = Thumb(range, "PART_LowerThumb");
            lower.Focus();

            Press(window, Key.Right);
            Assert.Equal(30, range.LowerValue);
            Assert.Equal(0, completed);

            Release(window, Key.Right);
            Assert.Equal(1, completed);
            Assert.Equal(30, observed);

            // Keys that never change an endpoint do not complete an interaction.
            Press(window, Key.A);
            Release(window, Key.A);
            Press(window, Key.Space);
            Release(window, Key.Space);
            Assert.Equal(1, completed);

            Press(window, Key.End);
            Release(window, Key.End);
            Assert.Equal(2, completed);
            Assert.Equal(80, observed);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void AutomationCompletesOnlyWhenTheEndpointValueChanges()
    {
        var range = NewRange();
        var window = Show(range);
        try
        {
            var completed = 0;
            range.ValueChangeCompleted += (_, _) => completed++;

            var peer = ControlAutomationPeer.CreatePeerForElement(Thumb(range, "PART_LowerThumb"));
            var provider = Assert.IsAssignableFrom<IRangeValueProvider>(peer);

            provider.SetValue(35);
            Assert.Equal(35, range.LowerValue);
            Assert.Equal(1, completed);

            provider.SetValue(35);
            Assert.Equal(1, completed);

            // The lower endpoint clamps at the upper one; repeating the value does not complete.
            provider.SetValue(80);
            Assert.Equal(2, completed);
            Assert.Equal(80, range.LowerValue);

            provider.SetValue(80);
            Assert.Equal(2, completed);
            Assert.Equal(80, range.LowerValue);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void TouchVerticalMovementCancelsWithoutValueChangeOrCompletion()
    {
        var range = NewRange();
        var window = Show(range);
        try
        {
            var completed = 0;
            range.ValueChangeCompleted += (_, _) => completed++;
            var canvas = range.GetVisualDescendants().OfType<Canvas>().Single(c => c.Name == "PART_TrackCanvas");
            var start = Center(canvas, window);
            var lower = range.LowerValue;
            var upper = range.UpperValue;

            var touch = window.TouchBegin(start);
            // Source pointerInput waits for slop: a touch press does not commit a value.
            Assert.Equal(lower, range.LowerValue);
            Assert.Equal(upper, range.UpperValue);
            window.TouchMove(touch, start + new Vector(0, 24));
            Assert.Equal(lower, range.LowerValue);
            Assert.Equal(upper, range.UpperValue);
            Assert.False(range.IsDragging);
            window.TouchEnd(touch, start + new Vector(0, 24));

            Assert.Equal(lower, range.LowerValue);
            Assert.Equal(upper, range.UpperValue);
            Assert.Equal(0, completed);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void TouchWaitsForSlopBeforeAHorizontalDrag()
    {
        var range = NewRange();
        var window = Show(range);
        try
        {
            var completed = new List<double>();
            range.ValueChangeCompleted += (_, _) => completed.Add(range.LowerValue);
            var start = Center(Thumb(range, "PART_LowerThumb"), window);

            var touch = window.TouchBegin(start);
            window.TouchMove(touch, start + new Vector(2, 1));
            Assert.Equal(20, range.LowerValue);
            Assert.False(range.IsDragging);

            window.TouchMove(touch, start + new Vector(60, 1));
            Assert.True(range.LowerValue > 20);
            Assert.True(range.IsDragging);

            window.TouchEnd(touch, start + new Vector(60, 1));
            Assert.Single(completed);
        }
        finally { window.Close(); }
    }

    private sealed class RangeModel : INotifyPropertyChanged
    {
        private double _lower;
        private double _upper;

        public double Lower
        {
            get => _lower;
            set
            {
                _lower = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Lower)));
            }
        }

        public double Upper
        {
            get => _upper;
            set
            {
                _upper = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Upper)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}