using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
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

    private static StackPanel TallContent()
    {
        var panel = new StackPanel();
        for (var i = 0; i < 30; i++)
            panel.Children.Add(new Border { Height = 40 });
        return panel;
    }
}