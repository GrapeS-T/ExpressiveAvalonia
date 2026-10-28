using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Theming;
using Xunit;

namespace ExpressiveAvalonia.Tests;

public class FabMenuBehaviorTests
{
    [AvaloniaFact]
    public void ExpandedSegmentsDoNotClipTheirFocusIndicators()
    {
        var segment = new MaterialFabMenuSegment { Label = "Action" };
        var menu = new MaterialFabMenu();
        menu.Items.Add(segment);
        MaterialMotion.SetIsReduced(menu, true);
        var window = new Window { Width = 400, Height = 400, Content = menu };
        try
        {
            window.Show();
            window.UpdateLayout();
            menu.IsExpanded = true;
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Assert.False(segment.ClipToBounds);
            Assert.True(segment.IsHitTestVisible);
            Assert.Equal(1, segment.Opacity);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void OpeningDoesNotStealFocusButArrowNavigationEntersMenu()
    {
        var segment = new MaterialFabMenuSegment { Label = "Action" };
        var menu = new MaterialFabMenu();
        menu.Items.Add(segment);
        MaterialMotion.SetIsReduced(menu, true);
        var window = new Window { Width = 400, Height = 400, Content = menu };
        try
        {
            window.Show();
            window.UpdateLayout();
            var trigger = menu.GetVisualDescendants().OfType<MaterialFab>().Single();
            Assert.True(trigger.Focus());
            menu.IsExpanded = true;
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Assert.True(trigger.IsFocused);
            trigger.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Down });
            Assert.True(segment.IsFocused);
        }
        finally { window.Close(); }
    }
}
