using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Theming;
using Xunit;
using Path = Avalonia.Controls.Shapes.Path;

namespace ExpressiveAvalonia.Tests;

public class ButtonVisualRegressionTests
{
    private static Window Show(Button button)
    {
        button.HorizontalAlignment = HorizontalAlignment.Center;
        button.VerticalAlignment = VerticalAlignment.Center;
        var window = new Window { Width = 800, Height = 500, Content = button, UseLayoutRounding = false };
        window.Show();
        window.UpdateLayout();
        return window;
    }

    private static T Part<T>(Control control, string name) where T : Control =>
        control.GetVisualDescendants().OfType<T>().Single(part => part.Name == name);

    private static Color ColorOf(IBrush? brush) => Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;
    private static Color Role(Control control, string name) =>
        ColorOf((IBrush)control.FindResource(control.ActualThemeVariant, name)!);

    [AvaloniaFact]
    public void LeadingIconsFollowLiveSizeWithoutInflatingTheContainer()
    {
        var button = new Button { Content = "Label" };
        var window = Show(button);
        try
        {
            foreach (var icon in new Control[]
            {
                new MaterialIcon { IconName = "star" },
                new PathIcon { Data = Geometry.Parse("M 5,5 L 19,5 L 19,19 L 5,19 Z") }
            })
            {
                MaterialAssist.SetIcon(button, icon);
                foreach (var (size, height, glyphSize, gap) in new[]
                {
                    (MaterialControlSize.ExtraLarge, 136d, 40d, 16d),
                    (MaterialControlSize.ExtraSmall, 32d, 20d, 4d),
                    (MaterialControlSize.Large, 96d, 32d, 12d),
                    (MaterialControlSize.Small, 40d, 20d, 8d),
                    (MaterialControlSize.Medium, 56d, 24d, 8d)
                })
                {
                    MaterialAssist.SetControlSize(button, size);
                    window.UpdateLayout();
                    Assert.Equal(height, Part<Border>(button, "Surface").Bounds.Height, 3);
                    Assert.Equal(glyphSize, icon.Bounds.Width, 3);
                    var label = Part<ContentPresenter>(button, "PART_ContentPresenter");
                    Assert.Equal(gap, label.TranslatePoint(default, button)!.Value.X
                        - icon.TranslatePoint(new Point(icon.Bounds.Width, 0), button)!.Value.X, 3);
                    Assert.Equal(button.Bounds.Height / 2,
                        icon.TranslatePoint(new Point(0, icon.Bounds.Height / 2), button)!.Value.Y, 3);
                }
            }
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void IconViewportAndCallerOverridesSurviveSwapAndDetach()
    {
        var glyph = new PathIcon { Data = Geometry.Parse("M 5,5 L 19,5 L 19,19 L 5,19 Z") };
        var selected = new MaterialIcon { IconName = "star_filled", Width = 30, Height = 30 };
        var button = new ToggleButton();
        MaterialAssist.SetIsIconButton(button, true);
        MaterialAssist.SetIcon(button, glyph);
        MaterialAssist.SetSelectedIcon(button, selected);
        var window = Show(button);
        try
        {
            var path = glyph.GetVisualDescendants().OfType<Path>().Single();
            var ink = path.RenderedGeometry!.Bounds;
            Assert.Equal(14, path.TranslatePoint(ink.BottomRight, button)!.Value.X
                - path.TranslatePoint(ink.TopLeft, button)!.Value.X, 3);
            button.IsChecked = true;
            window.UpdateLayout();
            Assert.Equal(30, selected.Width);
            Assert.True(double.IsNaN(glyph.Width));
            Assert.Null(glyph.Theme);
            button.IsChecked = false;
            window.UpdateLayout();
            Assert.Equal(24, glyph.Width);
            window.Content = null;
            Assert.True(double.IsNaN(glyph.Width));
            Assert.Null(glyph.Theme);
            window.Content = button;
            window.UpdateLayout();
            Assert.Equal(24, glyph.Bounds.Width);
            Assert.Equal(30, selected.Width);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void SquareToggleBecomesRoundAndPressedTakesPrecedence()
    {
        var button = new ToggleButton { Content = "Toggle" };
        MaterialAssist.SetButtonShape(button, MaterialButtonShape.Square);
        var window = Show(button);
        try
        {
            foreach (var iconOnly in new[] { false, true })
            {
                MaterialAssist.SetIsIconButton(button, iconOnly);
                foreach (var (size, square, pressed) in new[]
                {
                    (MaterialControlSize.Small, 12d, 8d),
                    (MaterialControlSize.Medium, 16d, 12d),
                    (MaterialControlSize.Large, 28d, 16d)
                })
                {
                    MaterialAssist.SetControlSize(button, size);
                    button.IsChecked = false;
                    window.UpdateLayout();
                    var surface = Part<Border>(button, "Surface");
                    Assert.Equal(square, surface.CornerRadius.TopLeft, 3);
                    button.IsChecked = true;
                    window.UpdateLayout();
                    Assert.Equal(Math.Min(surface.Bounds.Width, surface.Bounds.Height) / 2,
                        surface.CornerRadius.TopLeft, 3);
                    button.Focus(NavigationMethod.Tab);
                    window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
                    Assert.Equal(pressed, surface.CornerRadius.TopLeft, 3);
                    window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
                    Assert.False(button.IsChecked);
                    Assert.Equal(square, surface.CornerRadius.TopLeft, 3);
                }
            }
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void KeyboardFocusHasAnUnclippedTwoDipGapAndPointerFocusDoesNot()
    {
        var button = new Button { Content = "Focus" };
        var window = Show(button);
        try
        {
            button.Focus(NavigationMethod.Tab);
            window.UpdateLayout();
            var ring = Part<MaterialFocusIndicator>(button, "FocusRing");
            var surface = Part<Border>(button, "Surface");
            Assert.True(ring.IsVisible);
            Assert.False(button.ClipToBounds);
            Assert.Equal(2, surface.TranslatePoint(default, button)!.Value.X
                - ring.TranslatePoint(default, button)!.Value.X - ring.StrokeThickness, 3);
            Assert.Equal(surface.CornerRadius.TopLeft + 5, ring.CornerRadius.TopLeft, 3);
            window.FocusManager!.Focus(null);
            button.Focus(NavigationMethod.Pointer);
            Assert.False(ring.IsVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void DisabledOutlinedSelectionRemainsVisibleWithoutLosingItsOutlineWhenUnselected()
    {
        var button = new ToggleButton { Content = "Selection", IsEnabled = false };
        MaterialAssist.SetButtonVariant(button, MaterialButtonVariant.Outlined);
        var window = Show(button);
        try
        {
            foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            foreach (var iconOnly in new[] { false, true })
            {
                window.RequestedThemeVariant = theme;
                MaterialAssist.SetIsIconButton(button, iconOnly);
                button.IsChecked = false;
                window.UpdateLayout();
                var surface = Part<Border>(button, "Surface");
                var outline = Part<Border>(button, "Outline");
                Assert.Equal(0, ColorOf(surface.Background).A);
                Assert.Equal(Role(button, "MaterialOutlineVariantBrush"), ColorOf(outline.BorderBrush));
                Assert.Equal(1, outline.Opacity);
                Assert.Equal(Role(button, "MaterialOnSurfaceBrush"), ColorOf(button.Foreground));
                button.IsChecked = true;
                window.UpdateLayout();
                Assert.Equal(Role(button, "MaterialOnSurfaceBrush"), ColorOf(surface.Background));
                Assert.Equal(.1, surface.Opacity);
                Assert.Equal(new Thickness(0), outline.BorderThickness);
                button.IsEnabled = true;
                Assert.Equal(Role(button, "MaterialInverseSurfaceBrush"), ColorOf(surface.Background));
                button.IsEnabled = false;
            }
        }
        finally { window.Close(); }
    }
}
