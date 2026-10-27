using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Layout;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Theming;
using Xunit;

namespace ExpressiveAvalonia.Tests;

public class SwipeAnchorTests
{
    private static Window Show(Control content)
    {
        var window = new Window { Width = 600, Height = 400, Content = content };
        window.Show();
        window.UpdateLayout();
        return window;
    }

    private static MaterialSwipeListItem CreateItem() => new()
    {
        Width = 300,
        Height = 56,
        Content = "Swipe",
        EnableDismissFromStartToEnd = true,
        EnableDismissFromEndToStart = true,
    };

    [AvaloniaFact]
    public void ResizeDuringDismissSettlesAtTheNewWidth()
    {
        var item = CreateItem();
        var window = Show(item);
        try
        {
            MaterialMotion.SetIsReduced(item, false);
            item.Dismiss(MaterialSwipeState.StartToEnd);
            Assert.Equal(MaterialSwipeState.StartToEnd, item.TargetSwipeState);

            item.Width = 500;
            window.UpdateLayout();
            Assert.Equal(500, item.Bounds.Width);

            // Finish the pending settle without waiting for the 300 ms clock. The tween must have
            // been retargeted to the new anchor; the pre-resize endpoint would land at 300.
            MaterialMotion.SetIsReduced(item, true);

            Assert.Equal(MaterialSwipeState.StartToEnd, item.SwipeState);
            Assert.Equal(500, item.SwipeOffset, 3);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ResizeToZeroDuringDismissEndsOnTheZeroAnchor()
    {
        var item = CreateItem();
        var window = Show(item);
        try
        {
            MaterialMotion.SetIsReduced(item, false);
            item.Dismiss(MaterialSwipeState.StartToEnd);

            item.Width = 0;
            window.UpdateLayout();

            MaterialMotion.SetIsReduced(item, true);

            // A zero-width layout keeps the dismissed target at the zero anchor instead of
            // preserving the width captured when the dismiss started.
            Assert.Equal(MaterialSwipeState.StartToEnd, item.SwipeState);
            Assert.Equal(0, item.SwipeOffset, 3);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void DetachDuringResetDoesNotRetainAPartialOffset()
    {
        var item = CreateItem();
        var window = Show(item);
        var detachedOffset = double.NaN;
        try
        {
            MaterialMotion.SetIsReduced(item, true);
            item.Dismiss(MaterialSwipeState.StartToEnd);
            Assert.Equal(MaterialSwipeState.StartToEnd, item.SwipeState);

            MaterialMotion.SetIsReduced(item, false);
            item.Reset();
            Assert.Equal(MaterialSwipeState.Settled, item.TargetSwipeState);

            window.Content = null;
            detachedOffset = item.SwipeOffset;
        }
        finally { window.Close(); }

        Assert.Equal(0, detachedOffset, 3);
        Assert.Equal(MaterialSwipeState.Settled, item.SwipeState);

        var reattached = new Window { Width = 600, Height = 400, Content = item };
        reattached.Show();
        try
        {
            reattached.UpdateLayout();
            Assert.Equal(0, item.SwipeOffset, 3);
            Assert.Equal(MaterialSwipeState.Settled, item.SwipeState);
        }
        finally { reattached.Close(); }
    }

    [AvaloniaFact]
    public void RightToLeftSingleDirectionGestureReachesTheProgrammaticAnchor()
    {
        var item = CreateItem();
        item.EnableDismissFromEndToStart = false;
        item.FlowDirection = FlowDirection.RightToLeft;
        var window = Show(item);
        try
        {
            MaterialMotion.SetIsReduced(item, true);

            // Start at the item centre and drag one full width. Translating the local points into
            // window coordinates keeps the gesture correct under the RTL mirror. The headless
            // MouseMove helper defaults to no buttons, and the row only drags while the primary
            // button stays held, so every move must report it.
            var start = item.TranslatePoint(new Point(150, 28), window)!.Value;
            var edge = item.TranslatePoint(new Point(140, 28), window)!.Value;
            var end = item.TranslatePoint(new Point(150 - item.Bounds.Width, 28), window)!.Value;
            window.MouseDown(start, MouseButton.Left);
            // Cross the drag threshold while still inside the row so the pointer is captured; the
            // rest of the gesture then follows the captured pointer past the row edge.
            window.MouseMove(edge, RawInputModifiers.LeftMouseButton);
            window.MouseMove(end, RawInputModifiers.LeftMouseButton);
            window.MouseUp(end, MouseButton.Left);

            var gestured = item.SwipeOffset;
            Assert.Equal(MaterialSwipeState.StartToEnd, item.SwipeState);
            Assert.Equal(-300, gestured, 3);

            item.Reset();
            Assert.Equal(MaterialSwipeState.Settled, item.SwipeState);

            item.Dismiss(MaterialSwipeState.StartToEnd);
            Assert.Equal(MaterialSwipeState.StartToEnd, item.SwipeState);
            Assert.Equal(gestured, item.SwipeOffset, 3);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void DismissedRowIgnoresGesturesButKeepsProgrammaticStateChanges()
    {
        var item = CreateItem();
        // A transparent background keeps the row hit-testable after its foreground layer has
        // translated out of the item bounds.
        item.BackgroundContent = new Border { Background = Brushes.Transparent };
        var window = Show(item);
        try
        {
            MaterialMotion.SetIsReduced(item, true);
            item.Dismiss(MaterialSwipeState.StartToEnd);
            Assert.Equal(MaterialSwipeState.StartToEnd, item.SwipeState);
            var dismissedOffset = item.SwipeOffset;

            // Dragging a settled-dismissed row must not resume a gesture. The transparent
            // background stays over the row after the foreground layer translates away.
            var start = item.TranslatePoint(new Point(150, 28), window)!.Value;
            var end = item.TranslatePoint(new Point(50, 28), window)!.Value;
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(end, RawInputModifiers.LeftMouseButton);
            window.MouseUp(end, MouseButton.Left);

            Assert.Equal(MaterialSwipeState.StartToEnd, item.SwipeState);
            Assert.Equal(dismissedOffset, item.SwipeOffset, 3);

            item.Reset();
            Assert.Equal(MaterialSwipeState.Settled, item.SwipeState);
            Assert.Equal(0, item.SwipeOffset, 3);

            item.ConfirmValueChange = _ => false;
            item.Dismiss(MaterialSwipeState.StartToEnd);
            Assert.Equal(MaterialSwipeState.Settled, item.SwipeState);
            Assert.Equal(0, item.SwipeOffset, 3);

            item.ConfirmValueChange = null;
            item.Dismiss(MaterialSwipeState.EndToStart);
            Assert.Equal(MaterialSwipeState.EndToStart, item.SwipeState);
            Assert.Equal(-300, item.SwipeOffset, 3);
        }
        finally { window.Close(); }
    }
    [AvaloniaFact]
    public void ActionsAreKeyboardAccessibleAndFollowDirectionChanges()
    {
        var item = CreateItem();
        item.SwipeActions.Add(new MaterialSwipeAction { Label = "Archive" });
        var window = Show(item);
        try
        {
            var panel = Assert.IsType<StackPanel>(item.BackgroundContent);
            var button = Assert.IsType<Button>(Assert.Single(panel.Children));
            Assert.Equal(HorizontalAlignment.Right, panel.HorizontalAlignment);
            item.EnableDismissFromEndToStart = false;
            Assert.Same(panel, item.BackgroundContent);
            Assert.Equal(HorizontalAlignment.Left, panel.HorizontalAlignment);
            var invoked = 0;
            item.ActionInvoked += (_, e) => { Assert.False(e.BySwipe); invoked++; };
            Assert.True(button.IsTabStop);
            Assert.True(item.Focus());
            window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.None, null);
            window.KeyRelease(Key.Tab, RawInputModifiers.None, PhysicalKey.None, null);
            Assert.True(button.IsFocused);
            Assert.True(item.SwipeOffset > 0);
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.None, null);
            window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.None, null);
            Assert.Equal(1, invoked);
            item.Focus();
            Assert.Equal(0, item.SwipeOffset, 3);
        }
        finally { window.Close(); }
    }
}