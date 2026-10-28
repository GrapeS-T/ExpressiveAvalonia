using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using System.Linq;
using Avalonia.Media;
using ExpressiveAvalonia.Controls;
using Xunit;

namespace ExpressiveAvalonia.Tests;

public class AppBarRebindTests
{
    /// <summary>
    /// Rebinding a pinned bar from an already-scrolled viewer to one at its origin (and the
    /// reverse) must republish the derived overlap immediately: the state recomputed
    /// <see cref="MaterialAppBarState.OverlappedFraction"/> during adapter attachment, and the bar
    /// has to mirror it without waiting for an unrelated scroll callback.
    /// </summary>
    [AvaloniaFact]
    public void PinnedBarMirrorsStateWhenScrollSourceIsRebound()
    {
        var behavior = MaterialAppBarScrollBehavior.Pinned();
        behavior.State.HeightOffsetLimit = -64;
        var bar = new MaterialAppBar { ScrollBehavior = behavior };
        var scrolled = new ScrollViewer { Content = TallContent() };
        var atStart = new ScrollViewer { Content = TallContent() };
        var window = new Window
        {
            Width = 640,
            Height = 480,
            Content = new Grid { Children = { scrolled, atStart, bar } },
        };
        try
        {
            window.Show();
            window.UpdateLayout();
            scrolled.Offset = new Vector(0, 120);
            window.UpdateLayout();
            Assert.True(scrolled.Offset.Y > 0, "test setup requires a scrollable pre-scrolled viewer");
            Assert.Equal(0d, atStart.Offset.Y);

            // Attach an existing pinned bar to a viewer that is already scrolled away from its origin.
            bar.ScrollSource = scrolled;

            Assert.Equal(1d, behavior.State.OverlappedFraction);
            Assert.Equal(behavior.State.OverlappedFraction, bar.OverlappedFraction);
            Assert.True(bar.IsScrolled);

            // Rebinding to a source at its origin must publish the reset just as promptly.
            bar.ScrollSource = atStart;

            Assert.Equal(0d, behavior.State.OverlappedFraction);
            Assert.Equal(behavior.State.OverlappedFraction, bar.OverlappedFraction);
            Assert.False(bar.IsScrolled);

            // Re-attaching the pre-scrolled source restores the overlapped presentation.
            bar.ScrollSource = scrolled;

            Assert.Equal(1d, behavior.State.OverlappedFraction);
            Assert.Equal(behavior.State.OverlappedFraction, bar.OverlappedFraction);
            Assert.True(bar.IsScrolled);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void FlexibleTitleWrapsAndExpandsBar()
    {
        var bar = new MaterialAppBar { Configuration = MaterialAppBarConfiguration.LargeFlexible, Title = "A long title that wraps onto two lines", Subtitle = "Subtitle", Width = 320 };
        var window = new Window { Width = 320, Height = 500, Content = bar };
        try
        {
            window.Show();
            window.UpdateLayout();
            var text = bar.GetVisualDescendants().OfType<TextBlock>().Single(x => x.Name == "PART_FlexibleTitleText");
            Assert.True(text.IsVisible);
            Assert.True(text.Bounds.Height > text.LineHeight);
            Assert.True(bar.DesiredSize.Height >= 64 + text.Bounds.Height + bar.ExpandedTitleBottomPadding);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void FlexibleImageIncreasesExpandedHeight()
    {
        var bar = new MaterialAppBar { Configuration = MaterialAppBarConfiguration.MediumFlexible, Title = "Title", Width = 320 };
        var window = new Window { Width = 320, Height = 500, Content = bar };
        try
        {
            window.Show();
            window.UpdateLayout();
            var withoutImage = bar.DesiredSize.Height;
            bar.ImageContent = new Border { Width = 40, Height = 80 };
            window.UpdateLayout();
            Assert.True(bar.DesiredSize.Height > withoutImage);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ScrollBehaviorPreservesUserBackground()
    {
        var brush = new SolidColorBrush(Colors.Coral);
        var behavior = MaterialAppBarScrollBehavior.Pinned();
        var bar = new MaterialAppBar { Background = brush, Configuration = MaterialAppBarConfiguration.MediumFlexible, ScrollBehavior = behavior };
        var scroller = new ScrollViewer { Content = TallContent() };
        var window = new Window { Width = 320, Height = 400, Content = new Grid { Children = { scroller, bar } } };
        try
        {
            window.Show();
            window.UpdateLayout();
            bar.ScrollSource = scroller;
            scroller.Offset = new Vector(0, 120);
            window.UpdateLayout();
            Assert.Same(brush, bar.Background);
            Assert.NotSame(brush, bar.RenderedBackground);
            bar.ScrollBehavior = null;
            Assert.Same(brush, bar.Background);
            Assert.Same(brush, bar.RenderedBackground);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void TwoRowTitleAccessibilityFollowsCollapse()
    {
        var behavior = MaterialAppBarScrollBehavior.ExitUntilCollapsed();
        var bar = new MaterialAppBar { Configuration = MaterialAppBarConfiguration.MediumFlexible, Title = "Page", ScrollBehavior = behavior };
        var window = new Window { Width = 320, Height = 400, Content = bar };
        try
        {
            window.Show();
            window.UpdateLayout();
            var top = bar.GetVisualDescendants().OfType<Control>().Single(x => x.Name == "PART_Title");
            var expanded = bar.GetVisualDescendants().OfType<Control>().Single(x => x.Name == "PART_ExpandedTitle");
            Assert.Equal(AccessibilityView.Raw, AutomationProperties.GetAccessibilityView(top));
            Assert.Equal(AccessibilityView.Content, AutomationProperties.GetAccessibilityView(expanded));
            behavior.State.HeightOffset = behavior.State.HeightOffsetLimit;
            window.UpdateLayout();
            Assert.Equal(AccessibilityView.Content, AutomationProperties.GetAccessibilityView(top));
            Assert.Equal(AccessibilityView.Raw, AutomationProperties.GetAccessibilityView(expanded));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void FlexibleObjectTitleCanBeDisplayed()
    {
        var title = new TextBlock { Text = "Custom title" };
        var behavior = MaterialAppBarScrollBehavior.ExitUntilCollapsed();
        var bar = new MaterialAppBar { Configuration = MaterialAppBarConfiguration.MediumFlexible, Title = title, ScrollBehavior = behavior };
        var window = new Window { Width = 320, Height = 400, Content = bar };
        try
        {
            window.Show();
            window.UpdateLayout();
            Assert.True(title.Bounds.Height > 0);
            behavior.State.HeightOffset = behavior.State.HeightOffsetLimit;
            window.UpdateLayout();
            Assert.True(title.Bounds.Height > 0);
        }
        finally { window.Close(); }
    }

    private static StackPanel TallContent()
    {
        var panel = new StackPanel();
        for (var i = 0; i < 30; i++)
            panel.Children.Add(new Border { Height = 40 });
        return panel;
    }
}