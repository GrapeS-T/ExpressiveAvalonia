using System.ComponentModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Theming;
using Xunit;

namespace ExpressiveAvalonia.Tests;

public class DialogLifecycleTests
{
    private static Window ShowWindow(Control content)
    {
        var window = new Window { Width = 600, Height = 400, Content = content };
        window.Show();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static void Settle(Window window)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static void Press(Window window, Key key)
    {
        window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, null);
        window.KeyRelease(key, RawInputModifiers.None, PhysicalKey.None, null);
    }

    private static void Click(Window window, Control target) => Click(window, PointOn(window, target));

    private static Point PointOn(Window window, Control target)
    {
        var point = target.TranslatePoint(
            new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window);
        Assert.True(point.HasValue);
        return point.Value;
    }

    private static void Click(Window window, Point point)
    {
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
    }

    private static bool IsWithin(Control root, IInputElement? element) =>
        ReferenceEquals(element, root) ||
        (element is Visual visual && root.IsVisualAncestorOf(visual));

    private static Button ActionButton(MaterialDialog dialog, string text) =>
        dialog.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, text));

    [AvaloniaFact]
    public void ClosingLowerDialogKeepsUpperFocusAndRestoresOriginalTarget()
    {
        var pageFocus = new Button { Content = "Page" };
        var lowerContent = new Button { Content = "Lower content" };
        var page = new StackPanel { Children = { pageFocus } };
        var window = ShowWindow(page);
        try
        {
            MaterialMotion.SetIsReduced(window, true);
            Assert.True(pageFocus.Focus());

            var lower = new MaterialDialog { Title = "Lower", Content = lowerContent };
            var lowerTask = lower.ShowAsync(window);
            Settle(window);
            Assert.True(IsWithin(lower, window.FocusManager!.GetFocusedElement()));

            var upper = new MaterialDialog { Title = "Upper" };
            var upperTask = upper.ShowAsync(window);
            Settle(window);
            Assert.True(IsWithin(upper, window.FocusManager.GetFocusedElement()));

            // The lower modal is removed programmatically while the upper one is still open.
            lower.Close();
            Settle(window);
            Assert.False(lower.IsOpen);
            Assert.True(upper.IsOpen);
            Assert.True(IsWithin(upper, window.FocusManager.GetFocusedElement()));
            Assert.NotSame(pageFocus, window.FocusManager.GetFocusedElement());

            // The upper dialog must still receive Escape and restore the target the page had
            // before the lower dialog was opened.
            Press(window, Key.Escape);
            Settle(window);
            Assert.False(upper.IsOpen);
            Assert.Same(pageFocus, window.FocusManager.GetFocusedElement());
            Assert.True(lowerTask.IsCompleted);
            Assert.True(upperTask.IsCompleted);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void AcceptedCloseDisablesActionsUntilTheDialogIsShownAgain()
    {
        var invocations = 0;
        var pageClicks = 0;
        var page = new Button { Content = "Page" };
        page.Click += (_, _) => pageClicks++;
        var dialog = new MaterialDialog { Title = "Closing" };
        dialog.AddAction(new MaterialDialogAction("Run")
        {
            Command = new DelegateCommand(() => invocations++),
            Result = "run",
        });
        var window = ShowWindow(page);
        try
        {
            MaterialMotion.SetIsReduced(window, false);
            var first = dialog.ShowAsync(window);
            Settle(window);
            var button = ActionButton(dialog, "Run");
            Assert.True(button.IsEffectivelyEnabled);
            var actionPoint = PointOn(window, button);

            Click(window, actionPoint);
            Assert.Equal(1, invocations);
            Assert.False(dialog.IsOpen);
            Assert.False(dialog.IsEnabled);
            Assert.False(button.IsEffectivelyEnabled);

            // A second pointer or keyboard activation during the exit animation must not reach
            // the action command again, and the scrim keeps the click from the page.
            Click(window, actionPoint);
            Press(window, Key.Enter);
            Assert.Equal(1, invocations);
            Assert.Equal(0, pageClicks);

            MaterialMotion.SetIsReduced(window, true);
            Settle(window);
            Assert.True(first.IsCompleted);

            var second = dialog.ShowAsync(window);
            Settle(window);
            Assert.True(dialog.IsEnabled);
            var reopened = ActionButton(dialog, "Run");
            Assert.True(reopened.IsEffectivelyEnabled);

            Click(window, reopened);
            Assert.Equal(2, invocations);
            Assert.True(second.IsCompleted);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ActionCommandThatClosesDialogRunsOnce()
    {
        var invocations = 0;
        var dialog = new MaterialDialog { Title = "Command close" };
        dialog.AddAction(new MaterialDialogAction("Run")
        {
            Command = new DelegateCommand(() =>
            {
                invocations++;
                dialog.Close();
            }),
        });
        var window = ShowWindow(new Border());
        try
        {
            MaterialMotion.SetIsReduced(window, true);
            var task = dialog.ShowAsync(window);
            Settle(window);

            Click(window, ActionButton(dialog, "Run"));

            Assert.Equal(1, invocations);
            Assert.False(dialog.IsOpen);
            Assert.True(task.IsCompleted);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void VetoedCloseLeavesActionsInteractive()
    {
        var invocations = 0;
        var dialog = new MaterialDialog { Title = "Veto" };
        dialog.AddAction(new MaterialDialogAction("Run")
        {
            Command = new DelegateCommand(() => invocations++),
        });
        var window = ShowWindow(new Border());
        try
        {
            MaterialMotion.SetIsReduced(window, true);
            var closed = 0;
            dialog.Closed += (_, _) => closed++;
            var task = dialog.ShowAsync(window);
            Settle(window);
            var button = ActionButton(dialog, "Run");

            EventHandler<CancelEventArgs> veto = (_, args) => args.Cancel = true;
            dialog.Closing += veto;
            dialog.Close();
            Assert.True(dialog.IsOpen);
            Assert.True(dialog.IsEnabled);
            Assert.True(button.IsEffectivelyEnabled);

            Click(window, button);
            Assert.Equal(1, invocations);
            Assert.True(dialog.IsOpen);

            dialog.Closing -= veto;
            dialog.Close();
            Settle(window);
            Assert.False(dialog.IsOpen);
            Assert.Equal(1, closed);
            Assert.True(task.IsCompleted);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void CloseSkipsDetachedRestorationTarget()
    {
        var pageFocus = new Button { Content = "Page" };
        var page = new StackPanel { Children = { pageFocus } };
        var window = ShowWindow(page);
        try
        {
            MaterialMotion.SetIsReduced(window, true);
            Assert.True(pageFocus.Focus());

            var dialog = new MaterialDialog { Title = "Detached target" };
            var task = dialog.ShowAsync(window);
            Settle(window);

            page.Children.Remove(pageFocus);
            Settle(window);
            dialog.Close();
            Settle(window);

            Assert.False(dialog.IsOpen);
            Assert.True(task.IsCompleted);
            Assert.False(pageFocus.IsAttachedToVisualTree());
            Assert.NotSame(pageFocus, window.FocusManager!.GetFocusedElement());

            // The same instance can still be presented after the detached target was skipped.
            var again = dialog.ShowAsync(window);
            Settle(window);
            Assert.True(dialog.IsEnabled);
            dialog.Close();
            Settle(window);
            Assert.True(again.IsCompleted);
        }
        finally { window.Close(); }
    }

    private sealed class DelegateCommand(Action execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => execute();
    }
}