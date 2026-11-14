using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Theming;
using Xunit;

namespace ExpressiveAvalonia.Tests;

public class SheetHandleAutomationTests
{
    private static Window Show(Control content)
    {
        var window = new Window { Width = 700, Height = 650, Content = content };
        window.Show();
        Settle(window);
        return window;
    }

    private static void Settle(Window window)
    {
        for (var i = 0; i < 8; i++)
        {
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
        }
    }

    /// <summary>
    /// A point just inside the sheet's scrolling content, below the drag handle and above the
    /// bottom edge, so the same logical point is over the content at both partial and expanded.
    /// </summary>
    private static Point ContentPoint(ScrollViewer content, Window window) =>
        content.TranslatePoint(new Point(content.Bounds.Width / 2, 20), window)!.Value;

    /// <summary>The surface's current anchor translation, the sheet's drag position.</summary>
    private static double SurfaceOffset(MaterialBottomSheet sheet) =>
        ((TransformGroup)sheet.GetVisualDescendants().OfType<Border>()
            .Single(border => border.Name == "PART_Surface").RenderTransform!)
        .Children.OfType<TranslateTransform>().Single().Y;

    private static (Window Window, MaterialBottomSheet Sheet, Control Handle) ShowSheet(
        bool skipPartial = false,
        bool gesturesEnabled = true)
    {
        var sheet = new MaterialBottomSheet
        {
            IsModal = false,
            SkipPartiallyExpanded = skipPartial,
            AreGesturesEnabled = gesturesEnabled,
            Content = new Border { Height = 300 },
        };
        var window = Show(new Grid { Children = { new Border(), sheet } });
        MaterialMotion.SetIsReduced(window, true);
        var open = sheet.OpenAsync();
        Settle(window);
        Assert.True(open.IsCompleted);
        var handle = sheet.GetVisualDescendants().OfType<Control>()
            .Single(control => control.Name == "PART_DragHandle");
        return (window, sheet, handle);
    }

    private static AutomationPeer Peer(Control handle) =>
        ControlAutomationPeer.CreatePeerForElement(handle);

    [AvaloniaFact]
    public void InvokeReusesTheHandleTapCycleAcrossAvailableAnchors()
    {
        var (window, sheet, handle) = ShowSheet();
        try
        {
            Assert.Equal(MaterialSheetValue.PartiallyExpanded, sheet.CurrentValue);
            var invoke = Assert.IsAssignableFrom<IInvokeProvider>(Peer(handle));

            invoke.Invoke();
            Settle(window);
            Assert.Equal(MaterialSheetValue.Expanded, sheet.CurrentValue);
            Assert.True(sheet.IsOpen);

            // Expanded -> dismiss is the same cycle the pointer tap and Enter use.
            invoke.Invoke();
            Settle(window);
            Assert.Equal(MaterialSheetValue.Hidden, sheet.CurrentValue);
            Assert.False(sheet.IsOpen);

            // Closed Invoke mirrors the keyboard path: activation is unavailable, so automation
            // clients receive ElementNotEnabledException instead of silently reopening.
            Assert.Throws<ElementNotEnabledException>(() => invoke.Invoke());
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ExpandCollapseReportsAndMovesThroughExpandedPartialAndCollapsed()
    {
        var (window, sheet, handle) = ShowSheet();
        try
        {
            var pattern = Assert.IsAssignableFrom<IExpandCollapseProvider>(Peer(handle));
            Assert.False(pattern.ShowsMenu);
            Assert.Equal(ExpandCollapseState.PartiallyExpanded, pattern.ExpandCollapseState);

            pattern.Expand();
            Settle(window);
            Assert.Equal(MaterialSheetValue.Expanded, sheet.CurrentValue);
            Assert.Equal(ExpandCollapseState.Expanded, pattern.ExpandCollapseState);

            pattern.Collapse();
            Settle(window);
            Assert.Equal(MaterialSheetValue.PartiallyExpanded, sheet.CurrentValue);
            Assert.Equal(ExpandCollapseState.PartiallyExpanded, pattern.ExpandCollapseState);

            pattern.Collapse();
            Settle(window);
            Assert.Equal(MaterialSheetValue.Hidden, sheet.CurrentValue);
            Assert.False(sheet.IsOpen);
            Assert.Equal(ExpandCollapseState.Collapsed, pattern.ExpandCollapseState);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void CollapseDismissesWhenNoPartialAnchorExists()
    {
        var (window, sheet, handle) = ShowSheet(skipPartial: true);
        try
        {
            Assert.Equal(MaterialSheetValue.Expanded, sheet.CurrentValue);
            var pattern = Assert.IsAssignableFrom<IExpandCollapseProvider>(Peer(handle));

            // With no partial anchor the source only offers dismiss from the expanded state.
            pattern.Collapse();
            Settle(window);
            Assert.Equal(MaterialSheetValue.Hidden, sheet.CurrentValue);
            Assert.False(sheet.IsOpen);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ExpandAndDismissHonorVetoesAndTheGestureGate()
    {
        var (window, sheet, handle) = ShowSheet();
        try
        {
            var pattern = Assert.IsAssignableFrom<IExpandCollapseProvider>(Peer(handle));
            var invoke = Assert.IsAssignableFrom<IInvokeProvider>(Peer(handle));

            // A ValueChangeRequested veto leaves the anchor and automation state untouched.
            EventHandler<SheetValueChangeEventArgs> veto = (_, e) =>
            {
                if (e.Value == MaterialSheetValue.Expanded)
                    e.Cancel = true;
            };
            sheet.ValueChangeRequested += veto;
            pattern.Expand();
            Settle(window);
            Assert.Equal(MaterialSheetValue.PartiallyExpanded, sheet.CurrentValue);
            Assert.Equal(ExpandCollapseState.PartiallyExpanded, pattern.ExpandCollapseState);
            sheet.ValueChangeRequested -= veto;

            // BottomSheet.kt gates the semantics actions, but the handle's clickable cycle stays
            // available; Invoke therefore still expands while gestures are disabled.
            sheet.AreGesturesEnabled = false;
            Settle(window);
            Assert.Throws<ElementNotEnabledException>(() => pattern.Expand());
            Assert.Equal(MaterialSheetValue.PartiallyExpanded, sheet.CurrentValue);

            invoke.Invoke();
            Settle(window);
            Assert.Equal(MaterialSheetValue.Expanded, sheet.CurrentValue);

            // With no partial anchor, Collapse is the source dismiss action; a Hidden veto keeps
            // the sheet open at the expanded anchor instead of animating away.
            sheet.AreGesturesEnabled = true;
            sheet.SkipPartiallyExpanded = true;
            Settle(window);
            EventHandler<SheetValueChangeEventArgs> dismissVeto = (_, e) =>
            {
                if (e.Value == MaterialSheetValue.Hidden)
                    e.Cancel = true;
            };
            sheet.ValueChangeRequested += dismissVeto;
            pattern.Collapse();
            Settle(window);
            Assert.True(sheet.IsOpen);
            Assert.Equal(MaterialSheetValue.Expanded, sheet.CurrentValue);
            Assert.Equal(ExpandCollapseState.Expanded, pattern.ExpandCollapseState);
            sheet.ValueChangeRequested -= dismissVeto;

            pattern.Collapse();
            Settle(window);
            Assert.False(sheet.IsOpen);
            Assert.Equal(MaterialSheetValue.Hidden, sheet.CurrentValue);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void NestedContentReleaseForwardsVelocityIntoTheAnchorSettle()
    {
        var (window, sheet, _) = ShowSheet();
        try
        {
            Assert.Equal(MaterialSheetValue.PartiallyExpanded, sheet.CurrentValue);
            var content = sheet.GetVisualDescendants().OfType<ScrollViewer>()
                .Single(scroll => scroll.Name == "PART_ContentScroll");

            // SheetDefaults.kt onPostFling/onPreFling: a forward content release (positive offset
            // delta, finger moving up) expands the sheet, so the sheet velocity is the negation.
            Assert.Equal(-1500d, MaterialBottomSheet.ToSheetVelocity(1500d));

            // AnchoredDraggable's computeTarget is inclusive: closestAnchor(offset, searchUpwards)
            // still accepts the anchor the offset already rests on (delta 0), so a release with no
            // consumed nested movement keeps the current anchor.
            Assert.True(sheet.ReleaseNestedGesture(1500));
            Settle(window);
            Assert.Equal(MaterialSheetValue.PartiallyExpanded, sheet.CurrentValue);

            // A real nested release has already consumed inner-scroll movement, and the desktop
            // adapter applies that movement to the sheet before the fling settles. The displaced
            // release then selects the next anchor in the fling direction.
            var anchor = SurfaceOffset(sheet);
            window.MouseWheel(ContentPoint(content, window), new Vector(0, -1), RawInputModifiers.None);
            Assert.True(SurfaceOffset(sheet) < anchor - 0.5);
            Assert.True(sheet.ReleaseNestedGesture(1500));
            Settle(window);
            Assert.Equal(MaterialSheetValue.Expanded, sheet.CurrentValue);

            // The reverse fling uses the same velocity threshold to pick the next anchor below,
            // again only after the consumed nested movement left the expanded anchor.
            Assert.Equal(1500d, MaterialBottomSheet.ToSheetVelocity(-1500d));
            anchor = SurfaceOffset(sheet);
            window.MouseWheel(ContentPoint(content, window), new Vector(0, 1), RawInputModifiers.None);
            Assert.True(SurfaceOffset(sheet) > anchor + 0.5);
            Assert.True(sheet.ReleaseNestedGesture(-1500));
            Settle(window);
            Assert.Equal(MaterialSheetValue.PartiallyExpanded, sheet.CurrentValue);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void StandaloneHandleIndicationFollowsPressDragAndCaptureCancellation()
    {
        var handle = new MaterialDragHandle
        {
            Width = 120,
            Height = 120,
        };
        var window = Show(handle);
        try
        {
            var pill = handle.GetVisualDescendants().OfType<Border>()
                .Single(border => border.Name == "PART_Handle");
            var indication = handle.GetVisualDescendants().OfType<MaterialRipple>()
                .Single(ripple => ripple.Name == "PART_StateLayer");
            Assert.Equal(4, pill.Width);
            Assert.Equal(48, pill.Height);

            var point = pill.TranslatePoint(
                new Point(pill.Bounds.Width / 2, pill.Bounds.Height / 2), window)!.Value;
            IPointer? pointer = null;
            handle.AddHandler(
                InputElement.PointerPressedEvent,
                (_, e) => pointer = e.Pointer,
                Avalonia.Interactivity.RoutingStrategies.Bubble,
                handledEventsToo: true);

            window.MouseDown(point, MouseButton.Left);
            Settle(window);

            // Pressed geometry snaps to 12x52 with the medium corner, and the shared indication
            // is clipped by the same silhouette instead of the control's padded target.
            Assert.Equal(12, pill.Width);
            Assert.Equal(52, pill.Height);
            Assert.Equal(12, pill.CornerRadius.TopLeft);
            Assert.Equal(12, indication.Width);
            Assert.Equal(52, indication.Height);
            Assert.Equal(12, indication.CornerRadius.TopLeft);
            Assert.True(indication.IsDragging);
            Assert.Same(handle, pointer!.Captured);

            // Losing capture cancels the gesture without a release: the pill and indication snap
            // back and the drag state layer is cleared.
            pointer.Capture(null);
            Settle(window);
            Assert.Null(pointer.Captured);
            Assert.Equal(4, pill.Width);
            Assert.Equal(48, pill.Height);
            Assert.Equal(4, indication.Width);
            Assert.Equal(48, indication.Height);
            Assert.False(indication.IsDragging);

            // A normal release follows the same resting boundary.
            window.MouseDown(point, MouseButton.Left);
            Settle(window);
            Assert.Equal(12, pill.Width);
            Assert.True(indication.IsDragging);
            window.MouseUp(point, MouseButton.Left);
            Settle(window);
            Assert.Equal(4, pill.Width);
            Assert.Equal(48, pill.Height);
            Assert.Equal(4, indication.Width);
            Assert.Equal(48, indication.Height);
            Assert.False(indication.IsDragging);
        }
        finally { window.Close(); }
    }
}