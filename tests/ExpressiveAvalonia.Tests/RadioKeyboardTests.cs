using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Xunit;

namespace ExpressiveAvalonia.Tests;

public class RadioKeyboardTests
{
    [AvaloniaFact]
    public void ArrowsWrapAndSkipDisabledWithoutChangingOtherGroups()
    {
        var first = new RadioButton { Content = "First", GroupName = "choices", IsChecked = true };
        var disabled = new RadioButton { Content = "Disabled", GroupName = "choices", IsEnabled = false };
        var last = new RadioButton { Content = "Last", GroupName = "choices" };
        var other = new RadioButton { Content = "Other", GroupName = "other", IsChecked = true };
        var panel = new StackPanel();
        foreach (var radio in new[] { first, disabled, last, other }) panel.Children.Add(radio);
        var window = new Window { Content = panel, Width = 400, Height = 400 };
        window.Show();
        window.UpdateLayout();
        try
        {
            Assert.True(first.Focus());
            window.KeyPress(Key.Up, RawInputModifiers.None, PhysicalKey.None, null);
            Assert.True(last.IsFocused);
            Assert.True(last.IsChecked);
            Assert.True(other.IsChecked);
            window.KeyPress(Key.Down, RawInputModifiers.None, PhysicalKey.None, null);
            Assert.True(first.IsFocused);
            Assert.True(first.IsChecked);
            Assert.False(disabled.IsChecked);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ReverseTabEntersUnselectedGroupAtLastAndSelectedGroupAtSelection()
    {
        var before = new Button { Content = "Before" };
        var first = new RadioButton { Content = "First", GroupName = "choices" };
        var last = new RadioButton { Content = "Last", GroupName = "choices" };
        var after = new Button { Content = "After" };
        var panel = new StackPanel();
        foreach (var control in new Control[] { before, first, last, after }) panel.Children.Add(control);
        var window = new Window { Content = panel, Width = 400, Height = 400 };
        window.Show();
        window.UpdateLayout();
        try
        {
            Assert.True(after.Focus());
            window.KeyPress(Key.Tab, RawInputModifiers.Shift, PhysicalKey.None, null);
            window.KeyRelease(Key.Tab, RawInputModifiers.Shift, PhysicalKey.None, null);
            Assert.True(last.IsFocused);
            Assert.NotEqual(true, first.IsChecked);
            first.IsChecked = true;
            Assert.True(after.Focus());
            window.KeyPress(Key.Tab, RawInputModifiers.Shift, PhysicalKey.None, null);
            window.KeyRelease(Key.Tab, RawInputModifiers.Shift, PhysicalKey.None, null);
            Assert.True(first.IsFocused);
        }
        finally { window.Close(); }
    }
}
