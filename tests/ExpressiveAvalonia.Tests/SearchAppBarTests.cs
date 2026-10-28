using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Controls;
using Xunit;

namespace ExpressiveAvalonia.Tests;

public class SearchAppBarTests
{
    [AvaloniaFact]
    public void SearchFieldUsesResponsiveWidth()
    {
        foreach (var (available, expected) in new[] { (300d, 300d), (500d, 312d), (800d, 400d) })
        {
            var appBar = new MaterialSearchAppBar();
            var window = new Window { Width = available + 16, Height = 400, Content = appBar };
            try
            {
                window.Show();
                window.UpdateLayout();
                Assert.InRange(appBar.SearchBar!.Bounds.Width, expected - 1, expected + 1);
            }
            finally { window.Close(); }
        }
    }

    [AvaloniaFact]
    public void SearchActionsOverflowOnNarrowBar()
    {
        var bar = new MaterialSearchAppBar { Width = 400 };
        for (var i = 0; i < 3; i++)
            bar.Actions.Add(new MaterialBarAction { Text = $"Action {i}" });
        var window = Show(bar);
        try
        {
            Assert.True(bar.HasOverflow);
            bar.Width = 800;
            window.UpdateLayout();
            Assert.False(bar.HasOverflow);
            Assert.Equal(3, bar.VisibleActionCount);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void SuppliedSearchFieldUsesAvailableWidthWithoutChangingStandaloneDefaults()
    {
        var field = new MaterialSearchBar();
        var appBar = new MaterialSearchAppBar { SearchBar = field };
        var window = new Window { Width = 316, Height = 400, Content = appBar };
        try
        {
            window.Show();
            window.UpdateLayout();
            Assert.InRange(field.Bounds.Width, 299, 301);
            appBar.SearchBar = new MaterialSearchBar();
            window.UpdateLayout();
            var standalone = new Window { Width = 800, Height = 400, Content = field };
            try
            {
                standalone.Show();
                standalone.UpdateLayout();
                Assert.Equal(360, field.MinWidth);
                Assert.Equal(720, field.MaxWidth);
            }
            finally { standalone.Close(); }
        }
        finally { window.Close(); }
    }

    private static Window Show(Control content)
    {
        var window = new Window { Width = 800, Height = 600, Content = content };
        window.Show();
        window.UpdateLayout();
        return window;
    }

    private static StackPanel TallContent()
    {
        var panel = new StackPanel();
        for (var i = 0; i < 30; i++)
            panel.Children.Add(new Border { Height = 40, Background = Brushes.Transparent });
        return panel;
    }

    private static (Window Window, MaterialSearchAppBar AppBar, ScrollViewer Scroller) ScrollHost(
        MaterialSearchBarScrollBehavior behavior)
    {
        var appBar = new MaterialSearchAppBar { ScrollBehavior = behavior };
        var scroller = new ScrollViewer { Content = TallContent() };
        var root = new DockPanel();
        DockPanel.SetDock(appBar, Dock.Top);
        root.Children.Add(appBar);
        root.Children.Add(scroller);
        var window = Show(root);
        appBar.ScrollSource = scroller;
        window.UpdateLayout();
        return (window, appBar, scroller);
    }

    private static void Wheel(Window window, Visual target, double deltaY)
    {
        var point = target.TranslatePoint(
            new Point(target.Bounds.Width / 2, Math.Min(100, target.Bounds.Height / 2)),
            window)!.Value;
        window.MouseWheel(point, new Vector(0, deltaY), RawInputModifiers.None);
    }

    /// <summary>
    /// The pinned pre-scroll pass always adds the available delta and consumes it only when the
    /// offset changed and the layout is not reversed. At a limit the clamped delta is not consumed.
    /// </summary>
    [AvaloniaFact]
    public void PreScrollConsumesFullDeltaOnlyWhileTheOffsetChanges()
    {
        var state = new MaterialSearchBarScrollState(-100, -90, 0);
        var behavior = MaterialSearchBarScrollBehavior.EnterAlways(state);

        Assert.Equal(-50d, behavior.OnPreScroll(-50));
        Assert.Equal(-100d, state.ScrollOffset);

        Assert.Equal(0d, behavior.OnPreScroll(-50));
        Assert.Equal(-100d, state.ScrollOffset);
    }

    /// <summary>
    /// In a reversed layout the pre pass tracks the hide delta but never consumes it; the post
    /// pass consumes only a pull down, and ignores a pull up.
    /// </summary>
    [AvaloniaFact]
    public void ReversePreScrollTracksHideWithoutConsumingAndPostConsumesOnlyPullDown()
    {
        var state = new MaterialSearchBarScrollState(-100, -10, 0);
        var behavior = MaterialSearchBarScrollBehavior.EnterAlways(state, reverseLayout: true);

        Assert.Equal(0d, behavior.OnPreScroll(-100));
        Assert.Equal(-100d, state.ScrollOffset);

        Assert.Equal(30d, behavior.OnPostScroll(0, 30));
        Assert.Equal(-70d, state.ScrollOffset);
        Assert.Equal(30d, state.ContentOffset);

        Assert.Equal(0d, behavior.OnPostScroll(0, -20));
        Assert.Equal(-70d, state.ScrollOffset);
        Assert.Equal(30d, state.ContentOffset);
    }

    /// <summary>
    /// The source's <c>scrollOffsetLimit</c> is a plain state value: changing it does not re-coerce
    /// an offset that was valid under the old limit. The next offset assignment clamps.
    /// </summary>
    [AvaloniaFact]
    public void ChangingTheLimitDoesNotRetroactivelyClampTheOffset()
    {
        var state = new MaterialSearchBarScrollState(-100, -40, 0);

        state.ScrollOffsetLimit = -20;
        Assert.Equal(-40d, state.ScrollOffset);

        state.ScrollOffset += -10;
        Assert.Equal(-20d, state.ScrollOffset);
    }

    /// <summary>
    /// A wheel delta towards the content end is consumed by the bar before the presenter, so the
    /// content stays at its origin while the bar hides.
    /// </summary>
    [AvaloniaFact]
    public void ForwardWheelConsumesTheHideDeltaBeforeContentScrolls()
    {
        var behavior = MaterialSearchBarScrollBehavior.EnterAlways();
        var (window, _, scroller) = ScrollHost(behavior);
        try
        {
            Assert.True(behavior.State.ScrollOffsetLimit < 0,
                "test setup requires the measured collapsed height as the offset limit");

            Wheel(window, scroller, -1);
            window.UpdateLayout();

            Assert.Equal(0d, scroller.Offset.Y);
            Assert.True(behavior.State.ScrollOffset < 0, "the bar consumes the hide movement");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// In a reversed layout the pre pass still hides the bar, but it consumes nothing, so the
    /// content keeps scrolling. The adapter must not drop the negative delta before that pass.
    /// </summary>
    [AvaloniaFact]
    public void ReverseWheelHidesBarAndStillLetsTheContentScroll()
    {
        var behavior = MaterialSearchBarScrollBehavior.EnterAlways(reverseLayout: true);
        var (window, _, scroller) = ScrollHost(behavior);
        try
        {
            Assert.True(behavior.State.ScrollOffsetLimit < 0,
                "test setup requires the measured collapsed height as the offset limit");

            Wheel(window, scroller, -1);
            window.UpdateLayout();

            Assert.True(scroller.Offset.Y > 0, "a reversed hide delta is not consumed");
            Assert.Equal(-scroller.Offset.Y, behavior.State.ScrollOffset, 3);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// The animated container color uses a separate template channel: <see cref="TemplatedControl.Background"/>
    /// stays the resting target and keeps accepting a new theme/host color while the bar is
    /// scrolled, and the new color is used once the bar returns to rest.
    /// </summary>
    [AvaloniaFact]
    public void ScrolledContainerColorDoesNotMaskTheBackgroundTarget()
    {
        var rest = new SolidColorBrush(Colors.Red);
        var scrolled = new SolidColorBrush(Colors.Blue);
        var changedRest = new SolidColorBrush(Colors.Green);
        var behavior = MaterialSearchBarScrollBehavior.EnterAlways();
        var appBar = new MaterialSearchAppBar
        {
            Background = rest,
            ScrolledBackground = scrolled,
            ScrollBehavior = behavior,
        };
        var window = Show(appBar);
        try
        {
            Assert.True(behavior.State.ScrollOffsetLimit < 0,
                "test setup requires the measured collapsed height as the offset limit");

            behavior.State.ContentOffset = behavior.State.ScrollOffsetLimit;
            window.UpdateLayout();

            Assert.Same(rest, appBar.Background);
            Assert.Equal(Colors.Blue, RenderedColor(appBar));

            // A live seed/dark update while scrolled must reach the resting target even though the
            // spring currently paints the scrolled color.
            appBar.Background = changedRest;
            window.UpdateLayout();
            Assert.Same(changedRest, appBar.Background);
            Assert.Equal(Colors.Blue, RenderedColor(appBar));

            behavior.State.ContentOffset = 0;
            window.UpdateLayout();
            Assert.Equal(Colors.Green, RenderedColor(appBar));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// The field colors are current values on the bar, not local assignments, and are cleared when
    /// the app bar stops owning that bar so the bar's own theme can take over again.
    /// </summary>
    [AvaloniaFact]
    public void ReplacingTheSearchBarRestoresItsOwnColors()
    {
        var magenta = Color.Parse("#FF00FF");
        var bar = new MaterialSearchBar();
        var appBar = new MaterialSearchAppBar
        {
            SearchBar = bar,
            SearchBarBackground = new SolidColorBrush(magenta),
            ScrolledSearchBarBackground = new SolidColorBrush(magenta),
            SearchBarForeground = new SolidColorBrush(magenta),
        };
        var window = Show(appBar);
        try
        {
            Assert.Equal<Color?>(magenta, ColorOf(bar.Background));
            Assert.Equal<Color?>(magenta, ColorOf(bar.Foreground));

            var replacement = new MaterialSearchBar();
            appBar.SearchBar = replacement;
            window.UpdateLayout();

            Assert.NotEqual<Color?>(magenta, ColorOf(bar.Background));
            Assert.NotEqual<Color?>(magenta, ColorOf(bar.Foreground));
            Assert.Equal<Color?>(magenta, ColorOf(replacement.Background));
            Assert.Equal<Color?>(magenta, ColorOf(replacement.Foreground));

            appBar.SearchBar = null;
            window.UpdateLayout();
            Assert.NotEqual<Color?>(magenta, ColorOf(replacement.Background));
            Assert.NotEqual<Color?>(magenta, ColorOf(replacement.Foreground));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// The app bar owns <see cref="MaterialSearchView.HideAnchorWhenExpanded"/> and a
    /// null <see cref="MaterialSearchView.ExpansionAnchor"/> only while the pairing lasts. Replacing
    /// or detaching the view/host restores the previous view state, and reattaching takes it again.
    /// </summary>
    [AvaloniaFact]
    public void SearchViewAnchorOwnershipIsRestoredAndReapplied()
    {
        var bar = new MaterialSearchBar();
        var appBar = new MaterialSearchAppBar { SearchBar = bar };
        var view1 = new MaterialSearchView();
        var view2 = new MaterialSearchView();

        appBar.SearchView = view1;
        Assert.False(view1.HideAnchorWhenExpanded);
        Assert.Same(bar, view1.ExpansionAnchor);

        appBar.SearchView = view2;
        Assert.True(view1.HideAnchorWhenExpanded);
        Assert.Null(view1.ExpansionAnchor);
        Assert.False(view2.HideAnchorWhenExpanded);
        Assert.Same(bar, view2.ExpansionAnchor);

        var replacement = new MaterialSearchBar();
        appBar.SearchBar = replacement;
        Assert.Same(replacement, view2.ExpansionAnchor);

        appBar.SearchView = null;
        Assert.True(view2.HideAnchorWhenExpanded);
        Assert.Null(view2.ExpansionAnchor);

        var root = new Grid();
        root.Children.Add(appBar);
        var window = Show(root);
        try
        {
            appBar.SearchView = view2;
            Assert.False(view2.HideAnchorWhenExpanded);

            root.Children.Remove(appBar);
            window.UpdateLayout();
            Assert.True(view2.HideAnchorWhenExpanded);
            Assert.Null(view2.ExpansionAnchor);

            root.Children.Add(appBar);
            window.UpdateLayout();
            Assert.False(view2.HideAnchorWhenExpanded);
            Assert.Same(appBar.SearchBar, view2.ExpansionAnchor);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// AppBarWithSearch hides the field only while a full-screen surface is collapsing
    /// (!expandsToFullScreen || !isExpanded || targetExpanded).
    /// </summary>
    [AvaloniaFact]
    public void FieldVisibilityFollowsCurrentAndTargetExpansion()
    {
        var appBar = new MaterialSearchAppBar
        {
            ExpandsToFullScreen = true,
            IsSearchExpanded = true,
            SearchExpansionTarget = true,
        };
        var window = Show(appBar);
        try
        {
            var searchHost = appBar.GetVisualDescendants()
                .OfType<Border>()
                .Single(border => border.Name == "PART_SearchHost");

            Assert.Equal(1d, searchHost.Opacity);

            appBar.SearchExpansionTarget = false;
            Assert.Equal(0d, searchHost.Opacity);

            appBar.IsSearchExpanded = false;
            Assert.Equal(1d, searchHost.Opacity);

            appBar.IsSearchExpanded = true;
            appBar.SearchExpansionTarget = true;
            Assert.Equal(1d, searchHost.Opacity);

            appBar.ExpandsToFullScreen = false;
            appBar.SearchExpansionTarget = false;
            Assert.Equal(1d, searchHost.Opacity);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ReopeningUnanchoredViewRestoresFocusToLatestCaller()
    {
        var first = new Button { Content = "First" };
        var second = new Button { Content = "Second" };
        var view = new MaterialSearchView { IsOpen = false, AutoFocus = true };
        var window = Show(new Grid { Children = { first, second, view } });
        try
        {
            first.Focus();
            view.Open();
            window.UpdateLayout();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            view.Dismiss();
            Assert.True(first.IsFocused);

            second.Focus();
            view.IsOpen = true;
            window.UpdateLayout();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            view.Dismiss();
            Assert.True(second.IsFocused);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void InitiallyOpenViewAdoptsSharedAnchorQuery()
    {
        var anchor = new MaterialSearchBar { Text = "existing query", SelectionStart = 2, SelectionEnd = 5 };
        var view = new MaterialSearchView();
        var appBar = new MaterialSearchAppBar { SearchBar = anchor, SearchView = view };
        var window = Show(new Grid { Children = { appBar, view } });
        try
        {
            Assert.Equal("existing query", view.Text);
            Assert.True(anchor.IsExpanded);
            var editor = view.GetVisualDescendants().OfType<TextBox>().Single();
            editor.SetCurrentValue(TextBox.TextProperty, "edited query");
            Assert.Equal("edited query", anchor.Text);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void SharedQueryEditFollowedByDismissDoesNotReopenOnPostedTextChange()
    {
        var anchor = new MaterialSearchBar { Text = "query", AutoFocus = false };
        var view = new MaterialSearchView { IsOpen = false };
        var appBar = new MaterialSearchAppBar { SearchBar = anchor, SearchView = view };
        var window = Show(new Grid { Children = { appBar, view } });
        try
        {
            view.Open();
            window.UpdateLayout();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            var editor = view.GetVisualDescendants().OfType<TextBox>().Single();
            editor.SetCurrentValue(TextBox.TextProperty, "changed query");
            view.Dismiss();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.False(view.IsOpen);
            Assert.Equal("changed query", anchor.Text);
            Assert.True(anchor.IsKeyboardFocusWithin);
        }
        finally { window.Close(); }
    }

    private static Color RenderedColor(MaterialSearchAppBar appBar) =>
        SolidColor(appBar.RenderedBackground);

    private static Color? ColorOf(IBrush? brush) =>
        (brush as ISolidColorBrush)?.Color;

    private static Color SolidColor(IBrush? brush) =>
        Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;
}