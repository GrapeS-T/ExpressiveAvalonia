using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Styling;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Theming;
using Avalonia.VisualTree;
using Avalonia.Threading;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(ExpressiveAvalonia.Tests.TestApp))]
namespace ExpressiveAvalonia.Tests;

public class TestApp : Application
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<TestApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
    public override void Initialize() { Styles.Add(new MaterialTheme { Motion = MaterialMotionMode.Reduced }); }
}

public class ControlBehaviorTests
{
    private static Window Show(Control control)
    {
        var window = new Window { Width = 600, Height = 400, Content = control };
        window.Show();
        window.UpdateLayout();
        return window;
    }

    private static void Press(Window window, Key key)
    {
        window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, null);
        window.KeyRelease(key, RawInputModifiers.None, PhysicalKey.None, null);
    }

    [AvaloniaFact]
    public void TextEditingAndSelectionSurviveThemeChange()
    {
        var text = new TextBox();
        MaterialTextField.SetLabel(text, "Name");
        var window = Show(text);
        try
        {
            text.Focus();
            window.KeyTextInput("material");
            Assert.Equal("material", text.Text);
            text.SelectionStart = 1;
            text.SelectionEnd = 4;
            window.RequestedThemeVariant = ThemeVariant.Dark;
            Assert.Equal("material", text.Text);
            Assert.Equal("ate", text.SelectedText);
            window.KeyTextInput("D");
            Assert.Equal("mDrial", text.Text);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ToggleKeyboardAndDisabledInput()
    {
        var toggle = new ToggleButton { Content = "Favorite" };
        var window = Show(toggle);
        try
        {
            toggle.Focus();
            Press(window, Key.Space);
            Assert.True(toggle.IsChecked);
            toggle.IsEnabled = false;
            Press(window, Key.Space);
            Assert.True(toggle.IsChecked);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void RangeValuesClampOnCrossingAndBoundsChange()
    {
        var range = new MaterialRangeSlider { Minimum = 0, Maximum = 100, LowerValue = 20, UpperValue = 80 };
        range.LowerValue = 90;
        Assert.Equal(80, range.LowerValue);
        range.Maximum = 50;
        Assert.InRange(range.LowerValue, range.Minimum, 50);
        Assert.InRange(range.UpperValue, range.LowerValue, 50);
        range.UpperValue = -10;
        Assert.Equal(range.LowerValue, range.UpperValue);
    }

    [AvaloniaFact]
    public void RadioGroupingAndTriStateKeyboard()
    {
        var first = new RadioButton { GroupName = "test", IsChecked = true };
        var second = new RadioButton { GroupName = "test" };
        var check = new CheckBox { IsThreeState = true, IsChecked = false };
        var panel = new StackPanel(); panel.Children.Add(first); panel.Children.Add(second); panel.Children.Add(check);
        var window = Show(panel);
        try
        {
            second.Focus(); Press(window, Key.Space);
            Assert.True(second.IsChecked); Assert.False(first.IsChecked);
            check.Focus(); Press(window, Key.Space); Assert.True(check.IsChecked);
            Press(window, Key.Space); Assert.Null(check.IsChecked);
            Press(window, Key.Space); Assert.False(check.IsChecked);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void TabsKeyboardSelectsNextContent()
    {
        var tabs = new TabControl();
        tabs.Items.Add(new TabItem { Header = "One", Content = "First" });
        tabs.Items.Add(new TabItem { Header = "Two", Content = "Second" });
        var window = Show(tabs);
        try
        {
            ((TabItem)tabs.Items[0]!).Focus();
            Press(window, Key.Right);
            Assert.Equal(1, tabs.SelectedIndex);
        }
        finally { window.Close(); }
    }
    [AvaloniaFact]
    public void RangeThumbKeyboardSnapsAndCannotCross()
    {
        var range = new MaterialRangeSlider { LowerValue = 20, UpperValue = 40, TickFrequency = 10, IsSnapToTickEnabled = true, SmallChange = 10 };
        var window = Show(range);
        try
        {
            var lower = range.GetVisualDescendants().OfType<Thumb>().Single(t => t.Name == "PART_LowerThumb");
            lower.Focus();
            Press(window, Key.Right); Assert.Equal(30, range.LowerValue);
            Press(window, Key.End); Assert.Equal(40, range.LowerValue);
            Press(window, Key.Right); Assert.Equal(40, range.LowerValue);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void RichTooltipEscapeDismissesAndRestoresOwnerFocus()
    {
        var action = new Button { Content = "Action" };
        var rich = new RichTooltip { Title = "Help", Content = "Details", Actions = action };
        var owner = new Button { Content = "Help", Flyout = rich };
        var window = Show(owner);
        try
        {
            owner.Focus();
            rich.ShowAt(owner);
            Dispatcher.UIThread.RunJobs();
            Assert.True(rich.IsOpen);
            action.Focus();
            action.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
            Dispatcher.UIThread.RunJobs();
            Assert.False(rich.IsOpen);
            Assert.True(owner.IsFocused);
        }
        finally { rich.Hide(); window.Close(); }
    }

    [AvaloniaFact]
    public void PasswordRevealConcealsOnFocusLeavingField()
    {
        var password = new TextBox { Text = "secret", PasswordChar = '●' };
        MaterialTextField.SetShowPasswordReveal(password, true);
        var other = new Button { Content = "Other" };
        var panel = new StackPanel(); panel.Children.Add(password); panel.Children.Add(other);
        var window = Show(panel);
        try
        {
            password.Focus(); password.RevealPassword = true;
            other.Focus();
            Assert.False(password.RevealPassword);
            Assert.Equal("secret", password.Text);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void PlainTooltipFocusAndEscape()
    {
        var owner = new Button { Content = "Help" };
        ToolTip.SetTip(owner, "Details");
        var window = Show(owner);
        try
        {
            owner.Focus(NavigationMethod.Tab);
            Assert.True(ToolTip.GetIsOpen(owner));
            Press(window, Key.Escape);
            Assert.False(ToolTip.GetIsOpen(owner));
            Assert.True(owner.IsFocused);
        }
        finally { window.Close(); }
    }
    [AvaloniaFact]
    public void CustomButtonTextInheritsButtonForegroundAcrossThemeChanges()
    {
        var label = new TextBlock { Text = "Action" };
        var button = new Button { Content = label };
        var window = Show(button);
        try
        {
            Assert.Equal(button.Foreground, label.Foreground);
            window.RequestedThemeVariant = ThemeVariant.Dark;
            Assert.Equal(button.Foreground, label.Foreground);
            button.IsEnabled = false;
            Assert.Equal(button.Foreground, label.Foreground);
        }
        finally { window.Close(); }
    }
}
