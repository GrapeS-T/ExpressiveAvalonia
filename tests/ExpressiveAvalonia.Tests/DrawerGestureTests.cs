using System.Linq;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Theming;
using Xunit;

namespace ExpressiveAvalonia.Tests;

public class DrawerGestureTests
{
    private static Window Show(Control control)
    {
        var window = new Window { Width = 600, Height = 400, Content = control };
        window.Show();
        window.UpdateLayout();
        return window;
    }

    private static Border DrawerHost(MaterialNavigationDrawerHost host) =>
        host.GetVisualDescendants().OfType<Border>()
            .Single(border => border.Name == "PART_DrawerHost" && border.TemplatedParent == host);

    private static ContentPresenter PagePresenter(MaterialNavigationDrawerHost host) =>
        // Qualified by owner: ScrollViewer's own template also names its ScrollContentPresenter
        // PART_ContentPresenter, so an unnamed first match is ambiguous once the drawer is
        // realized (for example while a permanent drawer is visible).
        host.GetVisualDescendants().OfType<ContentPresenter>()
            .Single(presenter => presenter.Name == "PART_ContentPresenter" && presenter.TemplatedParent == host);

    private static double SlideX(Control control) =>
        ((TranslateTransform)control.RenderTransform!).X;

    [AvaloniaFact]
    public void ClosedModalHostKeepsClicksUntilHorizontalSlopAndOpensOnLtrSwipe()
    {
        var clicks = 0;
        var button = new Button
        {
            Content = "Page",
            Width = 120,
            Height = 48,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
        };
        button.Click += (_, _) => clicks++;
        var host = new MaterialNavigationDrawerHost
        {
            Presentation = MaterialNavigationDrawerPresentation.Modal,
            Drawer = new MaterialNavigationDrawer(),
            Content = new Grid { Children = { button } }
        };
        var window = Show(host);
        MaterialMotion.SetIsReduced(host, true);
        try
        {
            Assert.False(host.IsDrawerOpen);

            // Movement below the horizontal slop is still an ordinary click.
            var center = button.TranslatePoint(
                new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
            window.MouseDown(center, MouseButton.Left);
            window.MouseMove(center + new Vector(3, 0));
            window.MouseUp(center + new Vector(3, 0), MouseButton.Left);
            Assert.Equal(1, clicks);
            Assert.False(host.IsDrawerOpen);

            // The closed host itself opens the drawer when swiped toward the end edge.
            window.MouseDown(new Point(30, 200), MouseButton.Left);
            window.MouseMove(new Point(330, 200));
            window.MouseUp(new Point(330, 200), MouseButton.Left);
            window.UpdateLayout();

            Assert.True(host.IsDrawerOpen);
            Assert.Equal(1, clicks);
            Assert.Equal(0, SlideX(DrawerHost(host)), 3);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ClosedHostOpensOnRtlSwipe()
    {
        var host = new MaterialNavigationDrawerHost
        {
            Presentation = MaterialNavigationDrawerPresentation.Modal,
            FlowDirection = FlowDirection.RightToLeft,
            Drawer = new MaterialNavigationDrawer(),
            Content = new Grid()
        };
        var window = Show(host);
        MaterialMotion.SetIsReduced(host, true);
        try
        {
            Assert.False(host.IsDrawerOpen);
            // Avalonia mirrors the RTL plane once, so the source's leading-edge sheet stays on the
            // local left and the mirror renders it on the physical right.
            Assert.Equal(HorizontalAlignment.Left, DrawerHost(host).HorizontalAlignment);

            // Dragging leftwards across the physical right half is a positive local delta.
            window.MouseDown(new Point(560, 200), MouseButton.Left);
            window.MouseMove(new Point(360, 200));
            window.MouseUp(new Point(360, 200), MouseButton.Left);
            window.UpdateLayout();

            Assert.True(host.IsDrawerOpen);
            Assert.Equal(0, SlideX(DrawerHost(host)), 3);
            // The open sheet hugs the physical right edge exactly once: its local leading edge
            // lands on the window's right edge.
            var drawerLeft = DrawerHost(host).TranslatePoint(new Point(0, 0), window)!.Value.X;
            Assert.Equal(host.Bounds.Width, drawerLeft, 3);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void DragSettlesBySignedVelocityBeforePositionalThreshold()
    {
        var host = new MaterialNavigationDrawerHost
        {
            Presentation = MaterialNavigationDrawerPresentation.Modal,
            Drawer = new MaterialNavigationDrawer(),
            Content = new Grid(),
            IsDrawerOpen = true
        };
        var window = Show(host);
        MaterialMotion.SetIsReduced(host, true);
        try
        {
            var width = DrawerHost(host).Bounds.Width;
            Assert.True(width > 200);
            var start = new Point(300, 200);

            // A slow release below half closes positionally: 30% of the width over 700 ms is
            // below the 400 DIP/s velocity threshold.
            var slowEnd = new Point(start.X - width * 0.7, 200);
            window.MouseDown(start, MouseButton.Left);
            Thread.Sleep(700);
            window.MouseMove(slowEnd);
            window.MouseUp(slowEnd, MouseButton.Left);
            window.UpdateLayout();

            Assert.False(host.IsDrawerOpen);
            Assert.Equal(-width, SlideX(DrawerHost(host)), 3);

            // A fast reversal below half overrides the positional result and opens.
            host.IsDrawerOpen = true;
            window.UpdateLayout();
            Assert.True(host.IsDrawerOpen);

            var midpoint = new Point(start.X - width * 0.7, 200);
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(midpoint);
            Thread.Sleep(20);
            var reversal = new Point(midpoint.X + width * 0.17, 200);
            window.MouseMove(reversal);
            window.MouseUp(reversal, MouseButton.Left);
            window.UpdateLayout();

            Assert.True(host.IsDrawerOpen);
            Assert.Equal(0, SlideX(DrawerHost(host)), 3);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void DismissiblePageKeepsHostWidthAndTranslatesByExposedDrawerWidth()
    {
        var host = new MaterialNavigationDrawerHost
        {
            Presentation = MaterialNavigationDrawerPresentation.Dismissible,
            Drawer = new MaterialNavigationDrawer(),
            Content = new Border()
        };
        var window = Show(host);
        MaterialMotion.SetIsReduced(host, true);
        try
        {
            var presenter = PagePresenter(host);
            var widthBefore = presenter.Bounds.Width;
            Assert.Equal(host.Bounds.Width, widthBefore, 3);
            Assert.Equal(default(Thickness), presenter.Margin);
            Assert.Equal(0, SlideX(presenter), 3);

            window.MouseDown(new Point(30, 200), MouseButton.Left);
            window.MouseMove(new Point(330, 200));
            window.MouseUp(new Point(330, 200), MouseButton.Left);
            window.UpdateLayout();
            Assert.True(host.IsDrawerOpen);

            var drawerWidth = DrawerHost(host).Bounds.Width;
            Assert.True(drawerWidth > 0);
            Assert.Equal(widthBefore, presenter.Bounds.Width, 3);
            Assert.Equal(default(Thickness), presenter.Margin);
            Assert.Equal(drawerWidth, SlideX(presenter), 3);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void PermanentHostReservesDrawerWidthAndNeverDrags()
    {
        var host = new MaterialNavigationDrawerHost
        {
            Presentation = MaterialNavigationDrawerPresentation.Permanent,
            Drawer = new MaterialNavigationDrawer(),
            Content = new Border()
        };
        var window = Show(host);
        MaterialMotion.SetIsReduced(host, true);
        try
        {
            var presenter = PagePresenter(host);
            var drawerWidth = DrawerHost(host).Bounds.Width;
            Assert.True(drawerWidth > 0);
            Assert.True(host.IsDrawerOpen);
            var reserved = presenter.Margin.Left;
            Assert.Equal(drawerWidth, reserved, 3);
            var widthBefore = presenter.Bounds.Width;
            Assert.Equal(host.Bounds.Width - reserved, widthBefore, 3);
            Assert.Equal(0, SlideX(presenter), 3);

            window.MouseDown(new Point(30, 200), MouseButton.Left);
            window.MouseMove(new Point(330, 200));
            window.MouseUp(new Point(330, 200), MouseButton.Left);
            window.UpdateLayout();

            Assert.True(host.IsDrawerOpen);
            Assert.Equal(reserved, presenter.Margin.Left, 3);
            Assert.Equal(widthBefore, presenter.Bounds.Width, 3);
            Assert.Equal(0, SlideX(presenter), 3);
        }
        finally { window.Close(); }
    }
}