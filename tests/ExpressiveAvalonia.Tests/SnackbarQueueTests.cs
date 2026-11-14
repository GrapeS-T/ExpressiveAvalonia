using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using ExpressiveAvalonia.Controls;
using Xunit;

namespace ExpressiveAvalonia.Tests;

public class SnackbarQueueTests
{
    [AvaloniaFact]
    public void ConsecutiveSnackbarsWaitForPreviousClosedEvent()
    {
        var host = new MaterialSnackbarHost();
        var first = new MaterialSnackbar { Text = "First" };
        var second = new MaterialSnackbar { Text = "Second" };
        var window = new Window { Content = host, Width = 500, Height = 300 };
        window.Show();
        try
        {
            window.UpdateLayout();
            var firstResult = host.Show(first);
            host.Show(second);
            Dispatcher.UIThread.RunJobs();
            Assert.True(first.IsOpen);
            Assert.False(second.IsOpen);
            var closed = false;
            first.Closed += (_, _) => closed = true;
            second.Opened += (_, _) => Assert.True(closed);
            first.Dismiss();
            Assert.True(firstResult.IsCompleted);
            Dispatcher.UIThread.RunJobs();
            if (closed)
                Assert.True(second.IsOpen);
            else
                Assert.False(second.IsOpen);
        }
        finally { window.Close(); }
    }
}
