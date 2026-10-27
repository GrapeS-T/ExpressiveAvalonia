using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ExpressiveAvalonia.Controls;
using Xunit;

namespace ExpressiveAvalonia.Tests;

public class InteractionSurfaceTests
{
    private static Window Show(Control content)
    {
        var window = new Window { Width = 320, Height = 240, Content = content };
        window.Show();
        window.UpdateLayout();
        return window;
    }

    private static MaterialAvatar InteractiveAvatar() => new() { IsInteractive = true };

    private static void KeyDown(Window window, Key key) =>
        window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, null);

    private static void KeyUp(Window window, Key key) =>
        window.KeyRelease(key, RawInputModifiers.None, PhysicalKey.None, null);

    private static Point Center(Control control, Window window) =>
        control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;

    [AvaloniaFact]
    public void SpaceCompletesOnceOnMatchingReleaseAndIgnoresAutoRepeat()
    {
        var avatar = InteractiveAvatar();
        var clicks = 0;
        avatar.Click += (_, _) => clicks++;
        var window = Show(avatar);
        try
        {
            Assert.True(avatar.Focus());
            KeyDown(window, Key.Space);
            KeyDown(window, Key.Space);
            KeyDown(window, Key.Space);
            Assert.Equal(0, clicks);

            KeyUp(window, Key.Space);
            Assert.Equal(1, clicks);

            // A stray release after the gesture was consumed must not activate again.
            KeyUp(window, Key.Space);
            Assert.Equal(1, clicks);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void EnterCompletesOnMatchingRelease()
    {
        var avatar = InteractiveAvatar();
        var clicks = 0;
        avatar.Click += (_, _) => clicks++;
        var window = Show(avatar);
        try
        {
            Assert.True(avatar.Focus());
            KeyDown(window, Key.Enter);
            Assert.Equal(0, clicks);
            KeyUp(window, Key.Enter);
            Assert.Equal(1, clicks);

            KeyDown(window, Key.Enter);
            KeyDown(window, Key.Enter);
            Assert.Equal(1, clicks);
            KeyUp(window, Key.Enter);
            Assert.Equal(2, clicks);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void FocusLossCancelsHeldActivationKeyEvenAfterRefocus()
    {
        var avatar = InteractiveAvatar();
        var other = new Button { Content = "Other" };
        var clicks = 0;
        avatar.Click += (_, _) => clicks++;
        var window = Show(new StackPanel { Children = { avatar, other } });
        try
        {
            Assert.True(avatar.Focus());
            KeyDown(window, Key.Space);
            other.Focus();
            Assert.True(avatar.Focus());
            KeyUp(window, Key.Space);
            Assert.Equal(0, clicks);

            // The surface still activates normally once a fresh gesture starts.
            KeyDown(window, Key.Space);
            KeyUp(window, Key.Space);
            Assert.Equal(1, clicks);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void DisableCancelsHeldActivationKey()
    {
        var avatar = InteractiveAvatar();
        var clicks = 0;
        avatar.Click += (_, _) => clicks++;
        var window = Show(avatar);
        try
        {
            Assert.True(avatar.Focus());
            KeyDown(window, Key.Space);
            avatar.IsEnabled = false;
            avatar.IsEnabled = true;
            Assert.True(avatar.Focus());
            KeyUp(window, Key.Space);
            Assert.Equal(0, clicks);

            KeyDown(window, Key.Space);
            KeyUp(window, Key.Space);
            Assert.Equal(1, clicks);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void InteractivityLossCancelsHeldActivationKey()
    {
        var avatar = InteractiveAvatar();
        var clicks = 0;
        avatar.Click += (_, _) => clicks++;
        var window = Show(avatar);
        try
        {
            Assert.True(avatar.Focus());
            KeyDown(window, Key.Space);
            avatar.IsInteractive = false;
            avatar.IsInteractive = true;
            Assert.True(avatar.Focus());
            KeyUp(window, Key.Space);
            Assert.Equal(0, clicks);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void DetachCancelsHeldActivationKey()
    {
        var avatar = InteractiveAvatar();
        var panel = new StackPanel { Children = { avatar } };
        var clicks = 0;
        avatar.Click += (_, _) => clicks++;
        var window = Show(panel);
        try
        {
            Assert.True(avatar.Focus());
            KeyDown(window, Key.Space);
            panel.Children.Remove(avatar);
            panel.Children.Add(avatar);
            window.UpdateLayout();
            Assert.True(avatar.Focus());
            KeyUp(window, Key.Space);
            Assert.Equal(0, clicks);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void CommandRunsOnceForTheCompletedGesture()
    {
        var avatar = InteractiveAvatar();
        var command = new RecordingCommand();
        avatar.Command = command;
        var window = Show(avatar);
        try
        {
            Assert.True(avatar.Focus());
            KeyDown(window, Key.Space);
            KeyDown(window, Key.Space);
            Assert.Equal(0, command.Executions);

            KeyUp(window, Key.Space);
            Assert.Equal(1, command.Executions);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void PointerActivatesOnceOnReleaseAndReleasesCapture()
    {
        var avatar = InteractiveAvatar();
        var clicks = 0;
        avatar.Click += (_, _) => clicks++;
        IPointer? pointer = null;
        avatar.AddHandler(InputElement.PointerPressedEvent, (_, e) => pointer = e.Pointer, RoutingStrategies.Bubble, handledEventsToo: true);
        var window = Show(avatar);
        try
        {
            var point = Center(avatar, window);
            window.MouseDown(point, MouseButton.Left);
            Assert.Equal(0, clicks);
            Assert.Same(avatar, pointer!.Captured);

            window.MouseUp(point, MouseButton.Left);
            Assert.Equal(1, clicks);
            Assert.Null(pointer.Captured);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void InteractivityLossCancelsPointerGestureAndReleasesCapture()
    {
        var avatar = InteractiveAvatar();
        var clicks = 0;
        avatar.Click += (_, _) => clicks++;
        IPointer? pointer = null;
        avatar.AddHandler(InputElement.PointerPressedEvent, (_, e) => pointer = e.Pointer, RoutingStrategies.Bubble, handledEventsToo: true);
        var window = Show(avatar);
        try
        {
            var point = Center(avatar, window);
            window.MouseDown(point, MouseButton.Left);
            Assert.Same(avatar, pointer!.Captured);

            avatar.IsInteractive = false;
            Dispatcher.UIThread.RunJobs();
            Assert.Null(pointer.Captured);

            window.MouseUp(point, MouseButton.Left);
            Assert.Equal(0, clicks);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void DisableCancelsPointerGestureAndReleasesCapture()
    {
        var avatar = InteractiveAvatar();
        var clicks = 0;
        avatar.Click += (_, _) => clicks++;
        IPointer? pointer = null;
        avatar.AddHandler(InputElement.PointerPressedEvent, (_, e) => pointer = e.Pointer, RoutingStrategies.Bubble, handledEventsToo: true);
        var window = Show(avatar);
        try
        {
            var point = Center(avatar, window);
            window.MouseDown(point, MouseButton.Left);
            Assert.Same(avatar, pointer!.Captured);

            avatar.IsEnabled = false;
            Dispatcher.UIThread.RunJobs();
            Assert.Null(pointer.Captured);

            window.MouseUp(point, MouseButton.Left);
            Assert.Equal(0, clicks);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void DetachCancelsPointerGestureAndReleasesCapture()
    {
        var avatar = InteractiveAvatar();
        var panel = new StackPanel { Children = { avatar } };
        var clicks = 0;
        avatar.Click += (_, _) => clicks++;
        IPointer? pointer = null;
        avatar.AddHandler(InputElement.PointerPressedEvent, (_, e) => pointer = e.Pointer, RoutingStrategies.Bubble, handledEventsToo: true);
        var window = Show(panel);
        try
        {
            var point = Center(avatar, window);
            window.MouseDown(point, MouseButton.Left);
            Assert.Same(avatar, pointer!.Captured);

            panel.Children.Remove(avatar);
            Dispatcher.UIThread.RunJobs();
            Assert.Null(pointer.Captured);

            window.MouseUp(point, MouseButton.Left);
            Assert.Equal(0, clicks);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void PointerCaptureLossClearsOnlyThePointerGesture()
    {
        var avatar = InteractiveAvatar();
        var clicks = 0;
        avatar.Click += (_, _) => clicks++;
        IPointer? pointer = null;
        avatar.AddHandler(InputElement.PointerPressedEvent, (_, e) => pointer = e.Pointer, RoutingStrategies.Bubble, handledEventsToo: true);
        var window = Show(avatar);
        try
        {
            Assert.True(avatar.Focus());
            KeyDown(window, Key.Space);

            var point = Center(avatar, window);
            window.MouseDown(point, MouseButton.Left);
            Assert.Same(avatar, pointer!.Captured);

            // Another element or the platform takes capture away while both gestures are active.
            pointer.Capture(null);
            Dispatcher.UIThread.RunJobs();
            Assert.Null(pointer.Captured);

            window.MouseUp(point, MouseButton.Left);
            Assert.Equal(0, clicks);

            // The held key still completes, because capture loss only cancels the pointer press.
            KeyUp(window, Key.Space);
            Assert.Equal(1, clicks);
        }
        finally { window.Close(); }
    }

    private sealed class RecordingCommand : ICommand
    {
        public int Executions { get; private set; }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => Executions++;

        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }
}