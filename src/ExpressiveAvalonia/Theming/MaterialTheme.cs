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

    public static readonly StyledProperty<MaterialMotionScheme> MotionSchemeProperty =
        AvaloniaProperty.Register<MaterialTheme, MaterialMotionScheme>(nameof(MotionScheme), MaterialMotionScheme.Standard,
            validate: value => value is MaterialMotionScheme.Standard or MaterialMotionScheme.Expressive);
    public static readonly StyledProperty<bool> IsReducedMotionProperty =
        AvaloniaProperty.Register<MaterialTheme, bool>(nameof(IsReducedMotion));
    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        AvaloniaProperty.Register<MaterialTheme, FontFamily>(nameof(FontFamily), new FontFamily("avares://ExpressiveAvalonia/Assets/Fonts#Roboto"));

    /// <summary>
    /// Physical spring scheme applied to the theme. Defaults to <see cref="MaterialMotionScheme.Standard"/>;
    /// applications opt into <see cref="MaterialMotionScheme.Expressive"/> explicitly.
    /// </summary>
    public MaterialMotionScheme MotionScheme { get => GetValue(MotionSchemeProperty); set => SetValue(MotionSchemeProperty, value); }
    /// <summary>
    /// Accessibility reduced-motion policy. Independent of <see cref="MotionScheme"/>: when true it
    /// zeroes duration tokens and stops spring/indeterminate motion. The Windows animation setting
    /// is honored additionally.
    /// </summary>
    public bool IsReducedMotion { get => GetValue(IsReducedMotionProperty); set => SetValue(IsReducedMotionProperty, value); }
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
        ((IResourceProvider)this).OwnerChanged += (_, _) =>
        {
            MaterialMotion.ReducedMotionChanged -= SystemMotionChanged;
            if (((IResourceProvider)this).Owner is not null)
            {
                MaterialMotion.ReducedMotionChanged += SystemMotionChanged;
                UpdateMotion();
            }
        };
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SeedColorProperty || change.Property == ContrastProperty)
            UpdateColors();
        if (change.Property == MotionSchemeProperty || change.Property == IsReducedMotionProperty) UpdateMotion();
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
        _reduced = IsReducedMotion || MaterialMotion.IsSystemReduced;
        // All motion resources that can change at runtime are written to the Styles' own
        // dictionary, whose changes notify DynamicResource consumers (Window scheme, transition
        // durations); the merged token dictionary keeps the defaults. Spring numbers are also
        // read in code through MaterialSpringTokens by MaterialMotion.
        MaterialSpringTokens.Apply(Resources, MotionScheme);
        Resources["MaterialReducedMotion"] = _reduced;
        MaterialDurationTokens.Apply((ResourceDictionary)Resources, _reduced);
    }

    private void SystemMotionChanged(object? sender, EventArgs e) => UpdateMotion();

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
        // Compose composites some disabled roles over the scheme surface and draws the result
        // opaquely. These are real blends, not translucent layers: a translucent switch handle
        // would composite over the track instead of over the surface the source uses.
        void Composited(string key, string role, double opacity, string backdrop = "MaterialSurface")
        {
            var foreground = (Color)dictionary[role + "Color"]!;
            var background = (Color)dictionary[backdrop + "Color"]!;
            static byte Mix(byte fg, byte bg, double alpha) =>
                (byte)Math.Round(fg * alpha + bg * (1 - alpha));
            dictionary[key] = new SolidColorBrush(new Color(
                Mix(foreground.A, background.A, opacity),
                Mix(foreground.R, background.R, opacity),
                Mix(foreground.G, background.G, opacity),
                Mix(foreground.B, background.B, opacity)));
        }
        // Generic disabled roles. Compose composites these over the container; it never dims
        // the complete control, which would stack two translucent layers.
        Translucent("MaterialDisabledContainerBrush", "MaterialOnSurface", .04);
        Translucent("MaterialDisabledContentBrush", "MaterialOnSurface", .38);
        Translucent("MaterialDisabledOutlineBrush", "MaterialOnSurface", .12);
        Translucent("MaterialSelectionDisabledOnSurfaceBrush", "MaterialOnSurface", .38);
        Composited("MaterialSwitchDisabledTrackBrush", "MaterialOnSurface", .12);
        Composited("MaterialSwitchDisabledOffTrackBrush", "MaterialSurfaceContainerHighest", .12);
        Composited("MaterialSwitchDisabledOffIconBrush", "MaterialSurfaceContainerHighest", .38);
        Composited("MaterialSwitchDisabledOffThumbBrush", "MaterialOnSurface", .38);
        Composited("MaterialSwitchDisabledOnIconBrush", "MaterialOnSurface", .38);
        // Card.kt outlinedCardBorder(enabled = false) composites outline@0.12 over
        // ElevatedCardTokens.ContainerColor (surface-container-low), not over the outlined card's
        // own surface container; the stroke is that opaque result.
        Composited("MaterialCardOutlinedDisabledOutlineBrush", "MaterialOutline", .12,
            "MaterialSurfaceContainerLow");
        Composited("MaterialSliderDisabledHandleBrush", "MaterialOnSurface", .38);
        Translucent("MaterialTextFieldDisabledContentBrush", "MaterialOnSurface", .38);
        Translucent("MaterialTextFieldDisabledAffixBrush", "MaterialOnSurfaceVariant", .38);
        Translucent("MaterialTextFieldDisabledOutlineBrush", "MaterialOnSurface", .12);
        Translucent("MaterialTextFieldDisabledContainerBrush", "MaterialOnSurface", .04);
    }
}
