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

/// <summary>
/// Pins the component tokens the library claims to implement, so a silent drift from the
/// AndroidX Material3 values fails the suite instead of the catalog.
/// </summary>
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
    public void SelectedIconToggleSwapsSilhouetteWhileLabeledToggleKeepsIt()
    {
        // XSmall/SmallIconButtonTokens: SelectedContainerShapeRound = CornerMedium (12),
        // SelectedContainerShapeSquare = CornerFull. Labeled *ButtonTokens repeat the
        // unselected shape, so a checked labeled toggle must not morph.
        var roundIcon = IconToggle(MaterialButtonShape.Round, isIcon: true);
        var squareIcon = IconToggle(MaterialButtonShape.Square, isIcon: true);
        var roundLabel = IconToggle(MaterialButtonShape.Round, isIcon: false);
        var squareLabel = IconToggle(MaterialButtonShape.Square, isIcon: false);
        var window = Show(new StackPanel { Children = { roundIcon, squareIcon, roundLabel, squareLabel } });
        try
        {
            Assert.Equal(12, MaterialAssist.GetAnimatedCornerRadius(roundIcon).TopLeft, 3);
            Assert.Equal(20, MaterialAssist.GetAnimatedCornerRadius(squareIcon).TopLeft, 3);
            Assert.Equal(20, MaterialAssist.GetAnimatedCornerRadius(roundLabel).TopLeft, 3);
            Assert.Equal(12, MaterialAssist.GetAnimatedCornerRadius(squareLabel).TopLeft, 3);
        }
        finally { window.Close(); }
    }

    private static ToggleButton IconToggle(MaterialButtonShape shape, bool isIcon)
    {
        var toggle = new ToggleButton { Content = "Label", IsChecked = true };
        MaterialAssist.SetButtonShape(toggle, shape);
        MaterialAssist.SetControlSize(toggle, MaterialControlSize.Small);
        MaterialAssist.SetIsIconButton(toggle, isIcon);
        return toggle;
    }

    [AvaloniaFact]
    public void TextButtonUsesOnSurfaceVariantLabel()
    {
        // TextButtonTokens.LabelColor = OnSurfaceVariant.
        var button = new Button { Content = "Text" };
        MaterialAssist.SetButtonVariant(button, MaterialButtonVariant.Text);
        var window = Show(button);
        try
        {
            Assert.Equal(Role(button, "MaterialOnSurfaceVariantBrush"), ((ISolidColorBrush)button.Foreground!).Color);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void StandardIconButtonHasNoContainerUntilFilled()
    {
        var standard = new Button { Content = "Icon" };
        MaterialAssist.SetIsIconButton(standard, true);
        var filled = new Button { Content = "Icon" };
        MaterialAssist.SetIsIconButton(filled, true);
        MaterialAssist.SetButtonVariant(filled, MaterialButtonVariant.Filled);
        var window = Show(new StackPanel { Children = { standard, filled } });
        try
        {
            Assert.Equal(Colors.Transparent, ((ISolidColorBrush)standard.Background!).Color);
            Assert.Equal(Role(filled, "MaterialPrimaryBrush"), ((ISolidColorBrush)filled.Background!).Color);
        }
        finally { window.Close(); }
    }

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
    public void PrimaryTabsHaveNoDividerAndSecondaryTabsUseThreeDipIndicator()
    {
        var primary = new TabControl();
        primary.Items.Add(new TabItem { Header = "One", Content = 1 });
        primary.Items.Add(new TabItem { Header = "Two", Content = 2 });
        var secondary = new TabControl();
        MaterialTabs.SetVariant(secondary, MaterialTabVariant.Secondary);
        secondary.Items.Add(new TabItem { Header = "One", Content = 1 });
        secondary.Items.Add(new TabItem { Header = "Two", Content = 2 });
        var window = Show(new StackPanel { Children = { primary, secondary } });
        try
        {
            var primaryItem = (TabItem)primary.Items[0]!;
            Assert.False(Part<Border>(primaryItem, "Divider").IsVisible);

            var secondaryItem = (TabItem)secondary.Items[0]!;
            Assert.True(Part<Border>(secondaryItem, "Divider").IsVisible);
            Assert.Equal(3, Part<Border>(secondaryItem, "Indicator").Height);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void MenuItemUsesLabelLargeTypography()
    {
        // Menu.kt wraps items in ProvideTextStyle(labelLarge).
        var menu = new Menu();
        var item = new MenuItem { Header = "Open" };
        menu.Items.Add(item);
        var window = Show(menu);
        try
        {
            Assert.Equal(14, item.FontSize);
            Assert.Equal(FontWeight.Medium, item.FontWeight);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ListItemUsesTokenPaddingAndContentGaps()
    {
        var list = new ListBox();
        var item = new ListBoxItem { Content = "One line" };
        list.Items.Add(item);
        var window = Show(list);
        try
        {
            Assert.Equal(new Thickness(16, 8), item.Padding);
            var leading = Part<Border>(item, "StateLayer"); // ensures the template is applied
            Assert.NotNull(leading);
            var leadingPresenter = item.GetVisualDescendants().OfType<ContentPresenter>()
                .First(p => p.Name == "LeadingContent");
            Assert.Equal(new Thickness(0, 0, 16, 0), leadingPresenter.Margin);
            var trailingPresenter = item.GetVisualDescendants().OfType<ContentPresenter>()
                .First(p => p.Name == "TrailingContent");
            Assert.Equal(new Thickness(16, 0, 0, 0), trailingPresenter.Margin);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void DisabledTonalButtonUsesTwelvePercentContainer()
    {
        // FilledTonalButtonTokens.DisabledContainerOpacity = 0.12, filled = 0.10.
        var tonal = new Button { Content = "Tonal", IsEnabled = false };
        MaterialAssist.SetButtonVariant(tonal, MaterialButtonVariant.Tonal);
        var filled = new Button { Content = "Filled", IsEnabled = false };
        MaterialAssist.SetButtonVariant(filled, MaterialButtonVariant.Filled);
        var window = Show(new StackPanel { Children = { tonal, filled } });
        try
        {
            Assert.Equal(0.12, Part<Border>(tonal, "Surface").Opacity, 3);
            Assert.Equal(0.10, Part<Border>(filled, "Surface").Opacity, 3);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ExtraSmallButtonUsesSixteenDipLeadingSpace()
    {
        // ButtonXSmallTokens.LeadingSpace/TrailingSpace = 16.
        var button = new Button { Content = "XS" };
        MaterialAssist.SetControlSize(button, MaterialControlSize.ExtraSmall);
        var window = Show(button);
        try
        {
            Assert.Equal(new Thickness(16, 6), button.Padding);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void CheckboxErrorUsesTheErrorRole()
    {
        // CheckboxTokens.UnselectedErrorOutlineColor = Error, SelectedErrorContainerColor = Error.
        var uncheckedBox = new CheckBox { Content = "Invalid" };
        DataValidationErrors.SetErrors(uncheckedBox, new object[] { "Required" });
        var checkedBox = new CheckBox { Content = "Invalid", IsChecked = true };
        DataValidationErrors.SetErrors(checkedBox, new object[] { "Required" });
        var window = Show(new StackPanel { Children = { uncheckedBox, checkedBox } });
        try
        {
            var error = Role(uncheckedBox, "MaterialErrorBrush");
            Assert.Equal(error, ((ISolidColorBrush)Part<Border>(uncheckedBox, "Box").BorderBrush!).Color);
            Assert.Equal(error, ((ISolidColorBrush)Part<Border>(checkedBox, "Box").Background!).Color);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void StateLayerTransitionsBindMotionTokens()
    {
        var button = new Button { Content = "Animated" };
        var window = Show(button);
        try
        {
            var stateLayer = Part<Border>(button, "StateLayer");
            var transition = Assert.IsType<DoubleTransition>(
                Assert.Single(stateLayer.Transitions!.OfType<DoubleTransition>()));
            Assert.Equal(Visual.OpacityProperty, transition.Property);
            // The test application runs in reduced mode, so the duration token is zeroed;
            // comparing against the resolved resource proves the binding rather than a literal.
            Assert.Equal(window.FindResource("MaterialDurationShort4"), transition.Duration);
            // The default easing of a transition is LinearEasing; a SplineEasing means the
            // MaterialEasingStandard resource was resolved.
            Assert.IsType<SplineEasing>(transition.Easing);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ReducedMotionZeroesEveryDurationToken()
    {
        var theme = new MaterialTheme { Motion = MaterialMotionMode.Expressive };
        Assert.Equal(TimeSpan.FromMilliseconds(200), Duration(theme, "MaterialDurationShort4"));
        Assert.Equal(TimeSpan.FromMilliseconds(1000), Duration(theme, "MaterialDurationExtraLong4"));
        theme.Motion = MaterialMotionMode.Reduced;
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
