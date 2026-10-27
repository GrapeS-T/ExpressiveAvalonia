using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Controls;
using Xunit;

namespace ExpressiveAvalonia.Tests;

public class SplitCommandTests
{
    private static Window Show(Control control)
    {
        var window = new Window { Width = 600, Height = 400, Content = control };
        window.Show();
        window.UpdateLayout();
        return window;
    }

    private static Button Part(MaterialSplitButton split, string name) =>
        split.GetVisualDescendants().OfType<Button>().Single(button => button.Name == name);

    private static void Invoke(Control control)
    {
        var peer = ControlAutomationPeer.CreatePeerForElement(control);
        Assert.IsAssignableFrom<IInvokeProvider>(peer).Invoke();
    }

    private static void Press(Window window, Key key)
    {
        window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, null);
        window.KeyRelease(key, RawInputModifiers.None, PhysicalKey.None, null);
    }

    private static Point Center(Control control, Visual root) =>
        control.TranslatePoint(
            new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), root)!.Value;

    [AvaloniaFact]
    public void PrimaryCommandExecutesOnceAndCanExecuteDisablesOnlyItsHalf()
    {
        var command = new GateCommand();
        var split = new MaterialSplitButton
        {
            Content = "Send",
            Command = command,
            CommandParameter = "payload"
        };
        var window = Show(split);
        try
        {
            var primary = Part(split, "PART_PrimaryButton");
            var trailing = Part(split, "PART_TrailingButton");

            // The native buttons own the command plumbing.
            Assert.Same(command, primary.Command);
            Assert.Equal("payload", primary.CommandParameter);
            Assert.True(split.IsPrimaryEffectivelyEnabled);
            Assert.True(primary.IsEffectivelyEnabled);

            Assert.True(primary.Focus());
            Press(window, Key.Enter);
            Assert.Equal(1, command.Executions);
            Assert.Equal("payload", command.LastParameter);

            command.CanExecuteCore = _ => false;
            command.RaiseCanExecuteChanged();
            Assert.False(split.IsPrimaryEffectivelyEnabled);
            Assert.False(primary.IsEffectivelyEnabled);
            Assert.True(split.IsTrailingEffectivelyEnabled);
            Assert.True(trailing.IsEffectivelyEnabled);

            // The disabled native half cannot run the command again.
            Press(window, Key.Enter);
            Assert.Equal(1, command.Executions);

            command.CanExecuteCore = _ => true;
            command.RaiseCanExecuteChanged();
            Assert.True(split.IsPrimaryEffectivelyEnabled);
            Assert.True(primary.IsEffectivelyEnabled);

            // Parameter changes re-evaluate CanExecute through the native button.
            command.CanExecuteCore = parameter => Equals(parameter, "ok");
            split.CommandParameter = "nope";
            Assert.False(split.IsPrimaryEffectivelyEnabled);
            Assert.False(primary.IsEffectivelyEnabled);
            split.CommandParameter = "ok";
            Assert.True(split.IsPrimaryEffectivelyEnabled);
            Assert.True(primary.IsEffectivelyEnabled);

            // The two half flags stay independent.
            split.IsPrimaryEnabled = false;
            Assert.False(split.IsPrimaryEffectivelyEnabled);
            Assert.False(primary.IsEffectivelyEnabled);
            Assert.True(split.IsTrailingEffectivelyEnabled);
            Assert.True(trailing.IsEffectivelyEnabled);
            split.IsPrimaryEnabled = true;
            Assert.True(split.IsPrimaryEffectivelyEnabled);

            // A disabled ancestor disables both native halves.
            split.IsEnabled = false;
            Assert.False(split.IsPrimaryEffectivelyEnabled);
            Assert.False(split.IsTrailingEffectivelyEnabled);
            Assert.False(primary.IsEffectivelyEnabled);
            Assert.False(trailing.IsEffectivelyEnabled);
            split.IsEnabled = true;
            Assert.True(split.IsPrimaryEffectivelyEnabled);
            Assert.True(split.IsTrailingEffectivelyEnabled);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void HandledPublicClickSuppressesTheNativeCommand()
    {
        var command = new GateCommand();
        var clicks = 0;
        var split = new MaterialSplitButton { Content = "Send", Command = command };
        split.Click += (_, e) =>
        {
            clicks++;
            e.Handled = true;
        };
        var window = Show(split);
        try
        {
            var primary = Part(split, "PART_PrimaryButton");
            Assert.True(primary.Focus());
            Press(window, Key.Enter);

            Assert.Equal(1, clicks);
            Assert.Equal(0, command.Executions);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void AutomationPrimaryInvocationUsesClickThenCommandOrdering()
    {
        var command = new GateCommand();
        var handled = true;
        var split = new MaterialSplitButton { Content = "Send", Command = command };
        split.Click += (_, e) => e.Handled = handled;
        var window = Show(split);
        try
        {
            Invoke(split);
            Assert.Equal(0, command.Executions);

            handled = false;
            Invoke(split);
            Assert.Equal(1, command.Executions);

            // Exactly one implementation of the Click-then-command ordering exists: the native
            // button raised the same public Click that a pointer or keyboard activation raises.
            handled = true;
            Invoke(split);
            Assert.Equal(1, command.Executions);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void TrailingCommandExecutesOnceWithoutFlyout()
    {
        var command = new GateCommand();
        var split = new MaterialSplitButton
        {
            Content = "Send",
            TrailingCommand = command,
            TrailingCommandParameter = 42
        };
        var window = Show(split);
        try
        {
            var trailing = Part(split, "PART_TrailingButton");
            Assert.Same(command, trailing.Command);
            Assert.Equal(42, trailing.CommandParameter);
            Assert.True(trailing.IsEffectivelyEnabled);

            Invoke(trailing);
            Assert.Equal(1, command.Executions);
            Assert.Equal(42, command.LastParameter);

            // A false CanExecute disables the trailing half without touching the primary half.
            command.CanExecuteCore = _ => false;
            command.RaiseCanExecuteChanged();
            Assert.False(split.IsTrailingEffectivelyEnabled);
            Assert.False(trailing.IsEffectivelyEnabled);
            Assert.True(split.IsPrimaryEffectivelyEnabled);
            Assert.True(Part(split, "PART_PrimaryButton").IsEffectivelyEnabled);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void HandledTrailingClickSuppressesTheNativeCommand()
    {
        var command = new GateCommand();
        var clicks = 0;
        var split = new MaterialSplitButton { Content = "Send", TrailingCommand = command };
        split.TrailingClick += (_, e) =>
        {
            clicks++;
            e.Handled = true;
        };
        var window = Show(split);
        try
        {
            Invoke(Part(split, "PART_TrailingButton"));
            Assert.Equal(1, clicks);
            Assert.Equal(0, command.Executions);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void FlyoutTakesPrecedenceOverTrailingCommand()
    {
        var command = new GateCommand { CanExecuteCore = _ => false };
        var flyout = new Flyout { Content = new TextBlock { Text = "Menu" } };
        var split = new MaterialSplitButton
        {
            Content = "Send",
            Flyout = flyout,
            TrailingCommand = command
        };
        var window = Show(split);
        try
        {
            var trailing = Part(split, "PART_TrailingButton");

            // The flyout wins even when TrailingCommand reports CanExecute == false.
            Assert.Null(trailing.Command);
            Assert.True(split.IsTrailingEffectivelyEnabled);
            Assert.True(trailing.IsEffectivelyEnabled);

            Invoke(trailing);
            Dispatcher.UIThread.RunJobs();
            Assert.True(flyout.IsOpen);
            Assert.Equal(0, command.Executions);
        }
        finally
        {
            flyout.Hide();
            window.Close();
        }
    }

    [AvaloniaFact]
    public void CheckedTrailingTogglesWithoutFlyoutAndCommandSeesNewState()
    {
        var command = new GateCommand();
        var notifications = new List<bool>();
        var split = new MaterialSplitButton
        {
            Content = "Send",
            TrailingCommand = command,
            IsTrailingCheckable = true
        };
        split.TrailingCheckedChanged += (_, _) => notifications.Add(split.IsTrailingChecked);
        var window = Show(split);
        try
        {
            var trailing = Part(split, "PART_TrailingButton");
            var peer = ControlAutomationPeer.CreatePeerForElement(trailing);
            var toggle = Assert.IsAssignableFrom<IToggleProvider>(peer);
            var layer = trailing.GetVisualDescendants().OfType<Border>()
                .Single(border => border.Name == "SegmentCheckedLayer");

            Assert.Null(split.Flyout);
            Assert.False(split.IsMenuOpen);
            Assert.False(split.IsTrailingChecked);
            Assert.Equal(ToggleState.Off, toggle.ToggleState);
            Assert.Equal(new CornerRadius(4, 20, 20, 4), trailing.CornerRadius);
            Assert.Equal(0d, layer.Opacity);

            // The trailing half is a real button: automation invoke runs Click and the native
            // TrailingCommand after the built-in toggle.
            Invoke(trailing);
            window.UpdateLayout();
            Assert.True(split.IsTrailingChecked);
            Assert.Equal(ToggleState.On, toggle.ToggleState);
            Assert.False(split.IsMenuOpen);
            Assert.Equal(1, command.Executions);
            Assert.Equal(new[] { true }, notifications);
            Assert.Equal(0.1, layer.Opacity, 3);
            Assert.Equal(new CornerRadius(20), trailing.CornerRadius);

            // Keyboard activation follows the same native path and toggles back.
            Assert.True(trailing.Focus());
            Press(window, Key.Enter);
            window.UpdateLayout();
            Assert.False(split.IsTrailingChecked);
            Assert.Equal(ToggleState.Off, toggle.ToggleState);
            Assert.Equal(2, command.Executions);
            Assert.Equal(new[] { true, false }, notifications);
            Assert.Equal(0d, layer.Opacity);
            Assert.Equal(new CornerRadius(4, 20, 20, 4), trailing.CornerRadius);

            // Programmatic checked state drives the same native and visual state without a click.
            split.IsTrailingChecked = true;
            window.UpdateLayout();
            Assert.Equal(ToggleState.On, toggle.ToggleState);
            Assert.Equal(new CornerRadius(20), trailing.CornerRadius);

            // Pressed still wins over checked; release returns to the checked circle.
            var point = Center(trailing, window);
            window.MouseDown(point, MouseButton.Left);
            window.UpdateLayout();
            Assert.Equal(new CornerRadius(12, 20, 20, 12), trailing.CornerRadius);
            window.MouseUp(point, MouseButton.Left);
            window.UpdateLayout();
            Assert.False(split.IsTrailingChecked);
            Assert.Equal(new CornerRadius(4, 20, 20, 4), trailing.CornerRadius);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void HandledTrailingClickSuppressesBothToggleAndNativeCommand()
    {
        var command = new GateCommand();
        var split = new MaterialSplitButton
        {
            Content = "Send",
            TrailingCommand = command,
            IsTrailingCheckable = true
        };
        split.TrailingClick += (_, e) => e.Handled = true;
        var window = Show(split);
        try
        {
            var trailing = Part(split, "PART_TrailingButton");
            Invoke(trailing);

            Assert.False(split.IsTrailingChecked);
            Assert.Equal(0, command.Executions);
            Assert.Equal(ToggleState.Off,
                Assert.IsAssignableFrom<IToggleProvider>(
                    ControlAutomationPeer.CreatePeerForElement(trailing)).ToggleState);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void CheckedTrailingBindingFollowsNativeActivationAndSourceUpdates()
    {
        var model = new CheckFlag();
        var split = new MaterialSplitButton
        {
            Content = "Send",
            IsTrailingCheckable = true
        };
        split.Bind(MaterialSplitButton.IsTrailingCheckedProperty,
            new Binding(nameof(CheckFlag.Value))
            {
                Source = model,
                Mode = BindingMode.TwoWay
            });
        var window = Show(split);
        try
        {
            var trailing = Part(split, "PART_TrailingButton");
            var toggle = Assert.IsAssignableFrom<IToggleProvider>(
                ControlAutomationPeer.CreatePeerForElement(trailing));

            Invoke(trailing);
            Assert.True(model.Value);
            Assert.True(split.IsTrailingChecked);
            Assert.Equal(ToggleState.On, toggle.ToggleState);

            model.Value = false;
            Assert.False(split.IsTrailingChecked);
            Assert.False(((ToggleButton)trailing).IsChecked);
            Assert.Equal(ToggleState.Off, toggle.ToggleState);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void DefaultTrailingStaysActionOnlyForAutomationAndActivation()
    {
        var command = new GateCommand();
        var split = new MaterialSplitButton { Content = "Send", TrailingCommand = command };
        var window = Show(split);
        try
        {
            var trailing = Part(split, "PART_TrailingButton");
            var toggle = Assert.IsAssignableFrom<IToggleProvider>(
                ControlAutomationPeer.CreatePeerForElement(trailing));

            Assert.Equal(ToggleState.Off, toggle.ToggleState);
            toggle.Toggle();
            Assert.False(split.IsTrailingChecked);
            Assert.Equal(0, command.Executions);

            // Checked is coerced off while the trailing half is not checkable.
            split.IsTrailingChecked = true;
            Assert.False(split.IsTrailingChecked);
            Assert.Equal(ToggleState.Off, toggle.ToggleState);

            Invoke(trailing);
            Assert.Equal(1, command.Executions);
            Assert.False(split.IsTrailingChecked);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void DisabledCheckedTrailingCannotToggleAndAutomationThrows()
    {
        var split = new MaterialSplitButton
        {
            Content = "Send",
            IsTrailingCheckable = true,
            IsTrailingEnabled = false
        };
        var window = Show(split);
        try
        {
            var trailing = Part(split, "PART_TrailingButton");
            var toggle = Assert.IsAssignableFrom<IToggleProvider>(
                ControlAutomationPeer.CreatePeerForElement(trailing));

            Assert.False(trailing.IsEffectivelyEnabled);
            Assert.Throws<ElementNotEnabledException>(toggle.Toggle);
            Assert.False(split.IsTrailingChecked);
            Assert.Equal(ToggleState.Off, toggle.ToggleState);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void FlyoutPrecedenceBlocksCheckedTrailingToggle()
    {
        var flyout = new Flyout { Content = new TextBlock { Text = "Menu" } };
        var split = new MaterialSplitButton
        {
            Content = "Send",
            Flyout = flyout,
            IsTrailingCheckable = true
        };
        var window = Show(split);
        try
        {
            var trailing = Part(split, "PART_TrailingButton");
            var toggle = Assert.IsAssignableFrom<IToggleProvider>(
                ControlAutomationPeer.CreatePeerForElement(trailing));

            toggle.Toggle();
            Dispatcher.UIThread.RunJobs();
            Assert.False(flyout.IsOpen);
            Assert.False(split.IsTrailingChecked);

            Invoke(trailing);
            Dispatcher.UIThread.RunJobs();
            Assert.True(flyout.IsOpen);
            Assert.True(split.IsMenuOpen);
            Assert.False(split.IsTrailingChecked);
            Assert.Equal(new CornerRadius(20), trailing.CornerRadius);
        }
        finally
        {
            flyout.Hide();
            window.Close();
        }
    }

    [AvaloniaFact]
    public void HoverKeepsRestingShapeWhilePressMorphsTheSeam()
    {
        var split = new MaterialSplitButton { Content = "Split" };
        var window = Show(split);
        try
        {
            var primary = Part(split, "PART_PrimaryButton");
            var resting = primary.CornerRadius;
            var point = Center(primary, window);

            window.MouseMove(point);
            window.UpdateLayout();
            Assert.Equal(resting, primary.CornerRadius);

            window.MouseDown(point, MouseButton.Left);
            window.UpdateLayout();
            Assert.Equal(new CornerRadius(20, 12, 12, 20), primary.CornerRadius);

            window.MouseUp(point, MouseButton.Left);
            window.UpdateLayout();
            Assert.Equal(resting, primary.CornerRadius);
        }
        finally { window.Close(); }
    }

    private sealed class CheckFlag : INotifyPropertyChanged
    {
        private bool _value;

        public bool Value
        {
            get => _value;
            set
            {
                if (_value == value)
                    return;
                _value = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private sealed class GateCommand : ICommand
    {
        public Func<object?, bool> CanExecuteCore { get; set; } = _ => true;
        public int Executions { get; private set; }
        public object? LastParameter { get; private set; }

        public bool CanExecute(object? parameter) => CanExecuteCore(parameter);

        public void Execute(object? parameter)
        {
            Executions++;
            LastParameter = parameter;
        }

        public event EventHandler? CanExecuteChanged;

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}