using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using ExpressiveAvalonia.Tokens;

namespace ExpressiveAvalonia.Theming;

/// <summary>Material resources and the currently implemented control themes.</summary>
public sealed class MaterialTheme : Styles
{
    public static readonly StyledProperty<Color> SeedColorProperty =
        AvaloniaProperty.Register<MaterialTheme, Color>(nameof(SeedColor), Color.Parse("#6750A4"));

    public static readonly StyledProperty<double> ContrastProperty =
        AvaloniaProperty.Register<MaterialTheme, double>(nameof(Contrast), 0,
            validate: value => double.IsFinite(value) && value >= -1 && value <= 1);

    public Color SeedColor
    {
        get => GetValue(SeedColorProperty);
        set => SetValue(SeedColorProperty, value);
    }

    /// <summary>Material contrast level from -1 (reduced) to 1 (high).</summary>
    public double Contrast
    {
        get => GetValue(ContrastProperty);
        set => SetValue(ContrastProperty, value);
    }

    public MaterialTheme()
    {
        AvaloniaXamlLoader.Load(this);
        Resources.MergedDictionaries.Add(MaterialTokens.CreateResources());
        UpdateColors();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SeedColorProperty || change.Property == ContrastProperty)
            UpdateColors();
    }

    private void UpdateColors()
    {
        UpdateVariant(ThemeVariant.Light, false);
        UpdateVariant(ThemeVariant.Dark, true);
    }

    private void UpdateVariant(ThemeVariant variant, bool dark)
    {
        // Keep the dictionaries attached: replacing them would invalidate local variant scopes.
        if (!Resources.ThemeDictionaries.TryGetValue(variant, out var existing))
        {
            existing = new ResourceDictionary();
            Resources.ThemeDictionaries.Add(variant, existing);
        }

        var dictionary = (ResourceDictionary)existing;
        foreach (var (key, color) in MaterialColorScheme.Create(SeedColor, dark, Contrast))
        {
            dictionary[key] = color;
            var brushKey = key[..^"Color".Length] + "Brush";
            dictionary[brushKey] = new SolidColorBrush(color);
        }
    }
}
