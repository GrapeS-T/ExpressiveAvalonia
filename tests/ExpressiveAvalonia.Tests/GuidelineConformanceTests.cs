using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Theming;
using ExpressiveAvalonia.Tokens;
using Xunit;

namespace ExpressiveAvalonia.Tests;

/// <summary>Consumer-visible theming, validation and localization contracts.</summary>
public class GuidelineConformanceTests
{
    private static Window Show(Control control)
    {
        var window = new Window { Width = 600, Height = 400, Content = control };
        window.Show();
        window.UpdateLayout();
        return window;
    }

    private static Color Role(Control control, string key) =>
        ((ISolidColorBrush)control.FindResource(control.ActualThemeVariant, key)!).Color;

    private static Border Part<T>(Control control, string name) where T : Control =>
        control.GetVisualDescendants().OfType<T>().First(t => t.Name == name) as Border
        ?? throw new InvalidOperationException($"{name} is not a Border.");


    [AvaloniaFact]
    public void DisablingTheShapeMorphKeepsTheConsumerCornerRadius()
    {
        var button = new Button { Content = "Plain", CornerRadius = new CornerRadius(8) };
        MaterialAssist.SetAnimateShape(button, false);
        var window = Show(button);
        try
        {
            Assert.Equal(new CornerRadius(8), Part<Border>(button, "Surface").CornerRadius);
        }
        finally { window.Close(); }
    }




    [AvaloniaFact]
    public void ReducedMotionZeroesEveryDurationToken()
    {
        var theme = new MaterialTheme();
        Assert.Equal(TimeSpan.FromMilliseconds(200), Duration(theme, "MaterialDurationShort4"));
        Assert.Equal(TimeSpan.FromMilliseconds(1000), Duration(theme, "MaterialDurationExtraLong4"));
        theme.IsReducedMotion = true;
        Assert.All(MaterialDurationTokens.All.Keys,
            key => Assert.Equal(TimeSpan.Zero, Duration(theme, key)));
    }

    private static object? Duration(MaterialTheme theme, string key) =>
        theme.Resources.TryGetResource(key, null, out var value) ? value : null;

    [AvaloniaFact]
    public void BuiltInStringsResolveThroughOverridableResources()
    {
        var password = new TextBox { Text = "secret", PasswordChar = '●' };
        MaterialTextField.SetShowPasswordReveal(password, true);
        var window = Show(password);
        window.Resources["MaterialPasswordShowText"] = "Показать пароль";
        try
        {
            window.UpdateLayout();
            var reveal = password.GetVisualDescendants().OfType<ToggleButton>()
                .Single(b => b.Name == "PasswordReveal");
            Assert.Equal("Показать пароль", AutomationProperties.GetName(reveal));
        }
        finally { window.Close(); }
    }
}
