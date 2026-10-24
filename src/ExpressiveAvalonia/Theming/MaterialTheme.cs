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

    public static readonly StyledProperty<MaterialMotionMode> MotionProperty =
        AvaloniaProperty.Register<MaterialTheme, MaterialMotionMode>(nameof(Motion), MaterialMotionMode.Expressive);
    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        AvaloniaProperty.Register<MaterialTheme, FontFamily>(nameof(FontFamily), new FontFamily("avares://ExpressiveAvalonia/Assets/Fonts#Roboto"));

    public MaterialMotionMode Motion { get => GetValue(MotionProperty); set => SetValue(MotionProperty, value); }
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

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

    private readonly ResourceDictionary _tokens;
    private bool _reduced;

    public MaterialTheme()
    {
        AvaloniaXamlLoader.Load(this);
        _tokens = MaterialTokens.CreateResources();
        Resources.MergedDictionaries.Add(_tokens);
        UpdateColors();
        Resources["MaterialFontFamily"] = FontFamily;
        UpdateMotion();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SeedColorProperty || change.Property == ContrastProperty)
            UpdateColors();
        if (change.Property == MotionProperty) UpdateMotion();
        if (change.Property == FontFamilyProperty) Resources["MaterialFontFamily"] = FontFamily;
    }

    private void UpdateColors()
    {
        UpdateVariant(ThemeVariant.Light, false);
        UpdateVariant(ThemeVariant.Dark, true);
    }

    // Reduced motion zeroes every duration token, which collapses the state transitions that
    // bind Duration to those keys. Springs and indeterminate loops stop on their own.
    private void UpdateMotion()
    {
        _reduced = Motion == MaterialMotionMode.Reduced;
        Resources["MaterialReducedMotion"] = _reduced;
        MaterialDurationTokens.Apply(_tokens, _reduced);
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
        // Brushes in detached template dictionaries cannot resolve a control's local variant.
        // Generate translucent roles alongside their source colors in each variant dictionary.
        void Translucent(string key, string role, double opacity) =>
            dictionary[key] = new SolidColorBrush((Color)dictionary[role + "Color"]!, opacity);
        // Generic disabled roles. Compose composites these over the container; it never dims
        // the complete control, which would stack two translucent layers.
        Translucent("MaterialDisabledContainerBrush", "MaterialOnSurface", .04);
        Translucent("MaterialDisabledContentBrush", "MaterialOnSurface", .38);
        Translucent("MaterialDisabledOutlineBrush", "MaterialOnSurface", .12);
        Translucent("MaterialSelectionDisabledOnSurfaceBrush", "MaterialOnSurface", .38);
        Translucent("MaterialSwitchDisabledTrackBrush", "MaterialOnSurface", .12);
        Translucent("MaterialSwitchDisabledOffTrackBrush", "MaterialSurfaceContainerHighest", .12);
        Translucent("MaterialSwitchDisabledOffIconBrush", "MaterialSurfaceContainerHighest", .38);
        Translucent("MaterialTextFieldDisabledContentBrush", "MaterialOnSurface", .38);
        Translucent("MaterialTextFieldDisabledAffixBrush", "MaterialOnSurfaceVariant", .38);
        Translucent("MaterialTextFieldDisabledOutlineBrush", "MaterialOnSurface", .12);
        Translucent("MaterialTextFieldDisabledContainerBrush", "MaterialOnSurface", .04);
    }
}
