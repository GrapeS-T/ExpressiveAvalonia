using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using ExpressiveAvalonia.Controls;

namespace ExpressiveAvalonia.Catalog;

internal sealed partial class CatalogWindow
{
    private async void RunSmoke()
    {
        try
        {
            Color Primary() => ((ISolidColorBrush)this.FindResource(ActualThemeVariant, "MaterialPrimaryBrush")!).Color;
            RequestedThemeVariant = ThemeVariant.Light;
            var initial = Primary();
            RequestedThemeVariant = ThemeVariant.Dark;
            if (Primary() == initial) throw new InvalidOperationException("Theme did not update.");
            RequestedThemeVariant = ThemeVariant.Light;
            _theme.SeedColor = Color.Parse("#006C4C");
            if (Primary() == initial) throw new InvalidOperationException("Seed did not update.");
            var normal = Primary();
            _theme.Contrast = 1;
            if (Primary() == normal) throw new InvalidOperationException("Contrast did not update.");
            _theme.Contrast = 0;
            _theme.SeedColor = Color.Parse("#6750A4");
            foreach (var expressive in new[] { false, true })
            foreach (var variant in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            {
                RequestedThemeVariant = variant;
                var pages = expressive ? ExpressivePages : ClassicPages;
                for (var i = 0; i < pages.ItemCount; i++)
                {
                    SelectPage(expressive, i);
                    await Task.Delay(!expressive && i == 3 ? 15000 : 300);
                    if (expressive && (i == 4 || i == 25))
                    {
                        Inspector.ExerciseSmoke();
                        await Task.Delay(300);
                    }
                    Capture($"{(expressive ? "design-" : "")}{(variant == ThemeVariant.Dark ? "dark" : "light")}-{(expressive ? "" : "page-")}{i}");
                    Console.WriteLine($"Rendered {(expressive ? "Expressive" : "Classic")} {i}: {((TabItem)pages.Items[i]!).Header}");
                }
            }
            var owner = new Button { Content = "Tooltip owner" };
            var action = new Button { Content = "Закрыть" };
            var tooltip = new RichTooltip { Title = "Desktop popup", Content = "Escape dismiss", Actions = action };
            SetPreviewContent(owner);
            await Task.Delay(100);
            owner.Focus();
            tooltip.ShowAt(owner);
            await Task.Delay(300);
            if (!tooltip.IsOpen) throw new InvalidOperationException("Tooltip did not open.");
            action.Focus();
            action.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
            if (tooltip.IsOpen) throw new InvalidOperationException("Escape did not dismiss tooltip.");
            Console.WriteLine("SMOKE PASS: Classic/Expressive navigation, theme/seed/contrast, live inspector, 70 page renders, rich tooltip Escape.");
            Close();
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            Environment.ExitCode = 1;
            Close();
        }
    }

    private void Capture(string name)
    {
        UpdateLayout();
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)Width, (int)Height));
        bitmap.Render(this);
        Directory.CreateDirectory("artifacts");
        bitmap.Save($"artifacts/catalog-{name}.png", new PngBitmapEncoderOptions());
    }
}
