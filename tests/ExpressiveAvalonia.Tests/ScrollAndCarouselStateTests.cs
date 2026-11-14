using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.GestureRecognizers;
using Avalonia.Layout;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Theming;
using Xunit;

namespace ExpressiveAvalonia.Tests;

/// <summary>
/// State contracts from the pinned ScrollField/Pager/CarouselState sources:
/// <c>PagerState.targetPage</c> is <c>currentPage</c> at rest, and the carousel's mutation anchor
/// is its pager-derived <c>CarouselState.currentItem</c> rather than the selection intent.
/// </summary>
public class ScrollAndCarouselStateTests
{
    private static Window Show(Control control)
    {
        var window = new Window { Width = 800, Height = 400, Content = control };
        window.Show();
        window.UpdateLayout();
        return window;
    }

    [AvaloniaFact]
    public void ScrollViewerRespondsToWheelAndScrollbarDrag()
    {
        var scroller = new ScrollViewer
        {
            Content = new Border { Height = 2000 },
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            AllowAutoHide = false,
        };
        var window = Show(scroller);
        try
        {
            window.MouseWheel(new Point(200, 100), new Vector(0, -1));
            window.UpdateLayout();
            Assert.True(scroller.Offset.Y > 0);

            var thumb = scroller.GetVisualDescendants().OfType<Thumb>().Single();
            var point = thumb.TranslatePoint(new Point(thumb.Bounds.Width / 2, thumb.Bounds.Height / 2), window)!.Value;
            var before = scroller.Offset.Y;
            window.MouseDown(point, MouseButton.Left);
            window.MouseMove(point + new Vector(0, 100));
            window.MouseUp(point + new Vector(0, 100), MouseButton.Left);
            window.UpdateLayout();
            Assert.True(scroller.Offset.Y > before);
        }
        finally { window.Close(); }
    }

    private static void RaiseKey(Control control, Key key) =>
        control.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = key,
        });

    [AvaloniaFact]
    public void DisablingDuringScrollReconcilesTargetBeforeIdleAndKeepsRelativeNavigation()
    {
        var state = new MaterialScrollFieldState(10, 2);
        var field = new MaterialScrollField { State = state, Width = 120, Height = 120 };
        var window = Show(field);
        try
        {
            // A real spring is required for an in-progress target that has not settled yet.
            MaterialMotion.SetIsReduced(field, false);
            field.AnimateScrollToOption(7);

            Assert.True(state.IsScrollInProgress);
            Assert.Equal(7, state.TargetOption);
            Assert.Equal(2, state.SelectedOption);

            // Cancellation must restore targetPage == currentPage before reporting idle.
            field.IsEnabled = false;

            Assert.False(state.IsScrollInProgress);
            Assert.Equal(2, state.SelectedOption);
            Assert.Equal(state.SelectedOption, state.TargetOption);

            // Relative input now moves from the visible page, not from the cancelled destination.
            field.IsEnabled = true;
            RaiseKey(field, Key.Down);

            Assert.Equal(3, state.TargetOption);
            Assert.Equal(2, state.SelectedOption);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ClickingWrappedNeighboursTargetsTheirActualPages()
    {
        var state = new MaterialScrollFieldState(10, 9);
        var field = new MaterialScrollField { State = state, Width = 120, Height = 160 };
        var window = Show(field);
        try
        {
            MaterialMotion.SetIsReduced(field, false);
            var page = state.ContextPage;
            var center = Center(field, window);
            var next = new Point(center.X, center.Y + field.ItemHeight);
            window.MouseDown(next, MouseButton.Left);
            window.MouseUp(next, MouseButton.Left);
            Assert.Equal(page + 1, state.TargetPage);
            Assert.Equal(0, state.TargetOption);

            field.IsEnabled = false;
            field.IsEnabled = true;
            state.ScrollToOption(0);
            page = state.ContextPage;
            var previous = new Point(center.X, center.Y - field.ItemHeight);
            window.MouseDown(previous, MouseButton.Left);
            window.MouseUp(previous, MouseButton.Left);
            Assert.Equal(page - 1, state.TargetPage);
            Assert.Equal(9, state.TargetOption);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ScrollFieldAutomationUsesOptionRangeAndValue()
    {
        var state = new MaterialScrollFieldState(12, 11);
        var field = new MaterialScrollField { State = state, Width = 120, Height = 160 };
        var window = Show(field);
        try
        {
            var provider = Assert.IsAssignableFrom<IRangeValueProvider>(
                ControlAutomationPeer.CreatePeerForElement(field));
            Assert.Equal(0, provider.Minimum);
            Assert.Equal(11, provider.Maximum);
            Assert.Equal(11, provider.Value);
            provider.SetValue(3);
            Assert.Equal(3, state.TargetOption);
            Assert.Equal(3, provider.Value);
            field.IsEnabled = false;
            Assert.True(provider.IsReadOnly);
            provider.SetValue(5);
            Assert.NotEqual(5, state.TargetOption);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void InsertBeforeScrolledCurrentItemAnchorsTheCurrentItemNotTheSelectionOrigin()
    {
        var items = new ObservableCollection<string> { "A", "Peek", "B", "C", "D" };
        var carousel = new MaterialCarousel
        {
            ItemsSource = items,
            Layout = MaterialCarouselLayout.MultiBrowse,
            Width = 600,
            Height = 220,
            SelectedIndex = 0,
        };
        var window = Show(carousel);
        try
        {
            window.UpdateLayout();
            var panel = Assert.IsType<MaterialCarouselPanel>(carousel.ItemPanel);
            var scrollViewer = carousel.GetVisualDescendants().OfType<ScrollViewer>().First();
            var snapPoints = panel.GetIrregularSnapPoints(Orientation.Horizontal,
                SnapPointsAlignment.Near);

            // Record A as the explicit selection intent. Its snapped position is already current,
            // so the request completes immediately while the intent stays recorded.
            carousel.SelectedIndex = -1;
            carousel.SelectedIndex = 0;
            window.UpdateLayout();

            // Scrolling changes the pager-derived current item while the recorded selection
            // target still points at A, so selection synchronization is suppressed.
            scrollViewer.Offset = new Vector(snapPoints[2], scrollViewer.Offset.Y);
            carousel.InvalidateArrange();
            window.UpdateLayout();

            Assert.Equal(2, carousel.CurrentItem);
            Assert.Equal(0, carousel.SelectedIndex);

            // Inserting before the visible current item must keep B as the anchor, not A.
            items.Insert(1, "Inserted");
            window.UpdateLayout();

            Assert.Equal(3, carousel.CurrentItem);
            Assert.Equal(3, carousel.SelectedIndex);
        }
        finally { window.Close(); }
    }

    private static Point Center(Control control, TopLevel window) =>
        control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;

    private static IEnumerable<AutomationPeer> Descendants(AutomationPeer peer)
    {
        foreach (var child in peer.GetChildren())
        {
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }

    private static MaterialCarousel CreateUserScrollCarousel(ObservableCollection<string> items, bool userScrollEnabled)
    {
        var carousel = new MaterialCarousel
        {
            ItemsSource = items,
            Layout = MaterialCarouselLayout.MultiBrowse,
            Width = 600,
            Height = 220,
            SelectedIndex = 0,
            UserScrollEnabled = userScrollEnabled,
        };
        MaterialMotion.SetIsReduced(carousel, true);
        return carousel;
    }

    [AvaloniaFact]
    public void DisabledUserScrollBlocksWheelPagingAndAutomationButKeepsCardsAndProgrammaticScroll()
    {
        var items = new ObservableCollection<string> { "A", "B", "C", "D", "E", "F", "G", "H" };
        var invoked = 0;
        var carousel = CreateUserScrollCarousel(items, userScrollEnabled: false);
        carousel.ItemInvoked += (_, _) => invoked++;
        var window = Show(carousel);
        try
        {
            window.UpdateLayout();

            var panel = Assert.IsType<MaterialCarouselPanel>(carousel.ItemPanel);
            var scrollViewer = carousel.GetVisualDescendants().OfType<ScrollViewer>().First();
            var presenter = scrollViewer.GetVisualDescendants().OfType<ScrollContentPresenter>().First();
            var snapPoints = panel.GetIrregularSnapPoints(Orientation.Horizontal, SnapPointsAlignment.Near);
            Assert.True(snapPoints.Count > 2);
            Assert.Equal(0, scrollViewer.Offset.X);

            // The pinned Pager only passes userScrollEnabled to scrollable(enabled = ...), so the
            // native pan/inertia recognizer must not be able to move the strip.
            Assert.DoesNotContain(presenter.GestureRecognizers, recognizer => recognizer is ScrollGestureRecognizer);

            // scrollable(enabled = false) publishes no scroll node: the host scroll bars leave the
            // automation tree and cannot be hit, while the cards stay in it.
            var hostBars = scrollViewer.GetVisualDescendants().OfType<ScrollBar>()
                .Where(bar => ReferenceEquals(bar.TemplatedParent, scrollViewer)).ToArray();
            Assert.True(hostBars.Length > 0);
            Assert.All(hostBars, bar => Assert.False(bar.IsHitTestVisible));
            Assert.DoesNotContain(Descendants(ControlAutomationPeer.CreatePeerForElement(carousel)),
                peer => peer is ScrollViewerAutomationPeer);

            // Pinned: wheel is a gesture through the same scrollable.
            window.MouseWheel(Center(carousel, window), new Vector(-1, 0));
            window.UpdateLayout();
            Assert.Equal(0, scrollViewer.Offset.X);

            // Pinned ScrollableNode.onKeyEvent pages only while scrolling is enabled.
            RaiseKey(carousel, Key.PageDown);
            window.UpdateLayout();
            Assert.Equal(0, carousel.SelectedIndex);
            Assert.Equal(0, carousel.CurrentItem);
            Assert.Equal(0, scrollViewer.Offset.X);

            // Pinned LazyLayoutSemantics/PagerSemantics publish scroll actions only while enabled.
            var scroller = Assert.IsAssignableFrom<IScrollProvider>(
                ControlAutomationPeer.CreatePeerForElement(carousel));
            Assert.False(scroller.HorizontallyScrollable);
            scroller.Scroll(ScrollAmount.LargeIncrement, ScrollAmount.NoAmount);
            scroller.SetScrollPercent(50, ScrollPatternIdentifiers.NoScroll);
            window.UpdateLayout();
            Assert.Equal(0, scrollViewer.Offset.X);

            // The content is not disabled: the visible card still activates on a pointer press.
            var card = Assert.IsType<MaterialCarouselItem>(carousel.ContainerFromIndex(0));
            var mask = card.MaskRect;
            var local = mask.Width > 0 && mask.Height > 0
                ? new Point(mask.X + (mask.Width / 2), mask.Y + (mask.Height / 2))
                : new Point(card.Bounds.Width / 2, card.Bounds.Height / 2);
            var point = card.TranslatePoint(local, window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Assert.Equal(1, invoked);

            // Programmatic scrolling and the design-source selection intent are not user input.
            carousel.ScrollToItem(4);
            window.UpdateLayout();
            Assert.Equal(snapPoints[4], scrollViewer.Offset.X, 3);
            Assert.Equal(4, carousel.CurrentItem);

            carousel.SelectedIndex = 1;
            window.UpdateLayout();
            Assert.Equal(snapPoints[1], scrollViewer.Offset.X, 3);
            Assert.Equal(1, carousel.CurrentItem);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void EnablingUserScrollRestoresWheelGestureAndAutomationScrolling()
    {
        var items = new ObservableCollection<string> { "A", "B", "C", "D", "E", "F", "G", "H" };
        var carousel = CreateUserScrollCarousel(items, userScrollEnabled: false);
        var window = Show(carousel);
        try
        {
            window.UpdateLayout();

            var panel = Assert.IsType<MaterialCarouselPanel>(carousel.ItemPanel);
            var scrollViewer = carousel.GetVisualDescendants().OfType<ScrollViewer>().First();
            var presenter = scrollViewer.GetVisualDescendants().OfType<ScrollContentPresenter>().First();
            var snapPoints = panel.GetIrregularSnapPoints(Orientation.Horizontal, SnapPointsAlignment.Near);
            var hostBars = scrollViewer.GetVisualDescendants().OfType<ScrollBar>()
                .Where(bar => ReferenceEquals(bar.TemplatedParent, scrollViewer)).ToArray();
            var carouselPeer = ControlAutomationPeer.CreatePeerForElement(carousel);
            Assert.DoesNotContain(Descendants(carouselPeer), peer => peer is ScrollViewerAutomationPeer);

            carousel.UserScrollEnabled = true;
            window.UpdateLayout();

            Assert.Contains(presenter.GestureRecognizers, recognizer => recognizer is ScrollGestureRecognizer);
            Assert.All(hostBars, bar => Assert.True(bar.IsHitTestVisible));
            Assert.Contains(Descendants(carouselPeer), peer => peer is ScrollViewerAutomationPeer);

            var scroller = Assert.IsAssignableFrom<IScrollProvider>(carouselPeer);
            Assert.True(scroller.HorizontallyScrollable);

            window.MouseWheel(Center(carousel, window), new Vector(-1, 0));
            window.UpdateLayout();
            Assert.True(scrollViewer.Offset.X > 0);
            Assert.Contains(snapPoints, snapPoint => Math.Abs(snapPoint - scrollViewer.Offset.X) < 0.5);

            var beforeAutomation = scrollViewer.Offset.X;
            scroller.SetScrollPercent(100, ScrollPatternIdentifiers.NoScroll);
            window.UpdateLayout();
            Assert.True(scrollViewer.Offset.X > beforeAutomation);
        }
        finally { window.Close(); }
    }
    [AvaloniaTheory]
    [InlineData(MaterialCarouselLayout.MultiBrowse)]
    [InlineData(MaterialCarouselLayout.MultiAspectRatio)]
    public void EmptyStripCanPopulateClearAndRepopulate(MaterialCarouselLayout layout)
    {
        var items = new ObservableCollection<string>();
        var carousel = CreateUserScrollCarousel(items, userScrollEnabled: false);
        carousel.Layout = layout;
        var window = Show(carousel);
        try
        {
            var panel = Assert.IsType<MaterialCarouselPanel>(carousel.ItemPanel);
            Assert.Empty(panel.GetIrregularSnapPoints(Orientation.Horizontal, SnapPointsAlignment.Near));
            foreach (var item in new[] { "A", "B", "C", "D", "E", "F" }) items.Add(item);
            window.UpdateLayout();
            carousel.ScrollToItem(2);
            window.UpdateLayout();
            Assert.Equal(2, carousel.CurrentItem);

            items.Clear();
            window.UpdateLayout();
            Assert.Empty(panel.GetIrregularSnapPoints(Orientation.Horizontal, SnapPointsAlignment.Near));
            items.Add("Replacement");
            window.UpdateLayout();
            Assert.Equal(0, carousel.CurrentItem);
            Assert.Equal("Replacement", Assert.IsType<MaterialCarouselItem>(carousel.ContainerFromIndex(0)).Content);
        }
        finally { window.Close(); }
    }
}