using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Controls;

/// <summary>Reports a color-role copy from a schematic swatch.</summary>
public sealed class MaterialSwatchCopiedEventArgs(string role, string token, Color color) : EventArgs
{
    public string Role { get; } = role;
    public string Token { get; } = token;
    public Color Color { get; } = color;
    /// <summary>The copied hex string, e.g. #6750A4.</summary>
    public string Hex => $"#{Color.R:X2}{Color.G:X2}{Color.B:X2}";
}

/// <summary>
/// The color-role schematic: every Material role as a labelled swatch, in the current scheme and
/// optionally in a dark preview.
/// </summary>
/// <remarks>
/// Source anatomy (.Schematic group): role/token swatch groups for primary, secondary, tertiary and
/// error; the fixed-color roles; the surface ladder; and the inverse, scrim and shadow roles. Token
/// labels follow the source scheme (P-40 in light, P-80 in dark). Swatches are buttons: click,
/// Enter or Space copies the resolved hex value to the clipboard and raises
/// <see cref="SwatchCopied"/>. Swatch colors are resource bindings, so a seed or theme change
/// repaints them without rebuilding the control.
/// </remarks>
public sealed class MaterialSchematicGroup : TemplatedControl
{
    /// <summary>Shows a second scheme inside a dark ThemeVariantScope.</summary>
    public static readonly StyledProperty<bool> ShowDarkPreviewProperty =
        AvaloniaProperty.Register<MaterialSchematicGroup, bool>(nameof(ShowDarkPreview), true);

    /// <summary>Shows the fixed-color roles.</summary>
    public static readonly StyledProperty<bool> ShowFixedColorsProperty =
        AvaloniaProperty.Register<MaterialSchematicGroup, bool>(nameof(ShowFixedColors), true);

    /// <summary>Swatch width; the source uses 196 DIP.</summary>
    public static readonly StyledProperty<double> SwatchWidthProperty =
        AvaloniaProperty.Register<MaterialSchematicGroup, double>(nameof(SwatchWidth), 196,
            validate: static value => double.IsFinite(value) && value > 0);

    private readonly List<(Button Button, RoleSpec Role)> _currentSwatches = [];
    private WrapPanel? _schemes;
    private TextBlock? _status;

    static MaterialSchematicGroup()
    {
        ShowDarkPreviewProperty.Changed.AddClassHandler<MaterialSchematicGroup>(static (group, _) => group.RebuildContent());
        ShowFixedColorsProperty.Changed.AddClassHandler<MaterialSchematicGroup>(static (group, _) => group.RebuildContent());
        SwatchWidthProperty.Changed.AddClassHandler<MaterialSchematicGroup>(static (group, _) => group.RebuildContent());
    }

    public MaterialSchematicGroup()
    {
        Template = new FuncControlTemplate<MaterialSchematicGroup>((group, _) => group.Build());
        AutomationProperties.SetName(this, "Color scheme schematic");
    }

    /// <summary>Raised after a swatch value is copied.</summary>
    public event EventHandler<MaterialSwatchCopiedEventArgs>? SwatchCopied;

    public bool ShowDarkPreview { get => GetValue(ShowDarkPreviewProperty); set => SetValue(ShowDarkPreviewProperty, value); }
    public bool ShowFixedColors { get => GetValue(ShowFixedColorsProperty); set => SetValue(ShowFixedColorsProperty, value); }
    public double SwatchWidth { get => GetValue(SwatchWidthProperty); set => SetValue(SwatchWidthProperty, value); }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ActualThemeVariantChanged += OnThemeVariantChanged;
        UpdateTokens();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ActualThemeVariantChanged -= OnThemeVariantChanged;
    }

    private void OnThemeVariantChanged(object? sender, EventArgs e) => UpdateTokens();

    private Control Build()
    {
        _schemes = new WrapPanel { Orientation = Orientation.Horizontal };

        _status = new TextBlock { Margin = new Thickness(0, 8, 0, 0) };
        MaterialTypography.SetStyle(_status, MaterialTypeStyle.BodySmall);
        _status.Bind(TextBlock.ForegroundProperty, _status.GetResourceObservable("MaterialOnSurfaceVariantBrush"));
        AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);

        var root = new StackPanel();
        root.Children.Add(_schemes);
        root.Children.Add(_status);
        RebuildContent();
        UpdateTokens();
        return root;
    }

    private void RebuildContent()
    {
        if (_schemes is null) return;
        _currentSwatches.Clear();
        _schemes.Children.Clear();
        _schemes.Children.Add(BuildScheme(dark: false));
        if (ShowDarkPreview)
            _schemes.Children.Add(new ThemeVariantScope
            {
                RequestedThemeVariant = ThemeVariant.Dark,
                Child = BuildScheme(dark: true)
            });
        UpdateTokens();
    }

    private Control BuildScheme(bool dark)
    {
        var panel = new StackPanel { Spacing = 12, Margin = new Thickness(0, 0, 24, 0) };

        var heading = new TextBlock { Text = dark ? "Dark scheme" : "Light scheme" };
        MaterialTypography.SetStyle(heading, MaterialTypeStyle.TitleSmall);
        AutomationProperties.SetHeadingLevel(heading, 3);
        panel.Children.Add(heading);

        panel.Children.Add(BuildGroup("Primary", dark, Roles("Primary", "P")));
        panel.Children.Add(BuildGroup("Secondary", dark, Roles("Secondary", "S")));
        panel.Children.Add(BuildGroup("Tertiary", dark, Roles("Tertiary", "T")));
        panel.Children.Add(BuildGroup("Error", dark, Roles("Error", "E")));
        if (ShowFixedColors)
        {
            panel.Children.Add(BuildGroup("Primary fixed", dark, FixedRoles("Primary", "P", "S")));
            panel.Children.Add(BuildGroup("Secondary fixed", dark, FixedRoles("Secondary", "S", "S")));
            panel.Children.Add(BuildGroup("Tertiary fixed", dark, FixedRoles("Tertiary", "T", "S")));
        }
        panel.Children.Add(BuildGroup("Surfaces", dark, SurfaceRoles()));
        panel.Children.Add(BuildGroup("Inverse, scrim and shadow", dark, InverseRoles()));
        return panel;
    }

    private Control BuildGroup(string title, bool dark, IReadOnlyList<RoleSpec> roles)
    {
        var panel = new StackPanel { Spacing = 4 };
        var heading = new TextBlock { Text = title };
        MaterialTypography.SetStyle(heading, MaterialTypeStyle.LabelLarge);
        heading.Bind(TextBlock.ForegroundProperty, heading.GetResourceObservable("MaterialOnSurfaceVariantBrush"));
        panel.Children.Add(heading);

        var swatches = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var role in roles)
        {
            var button = BuildSwatch(role, dark);
            swatches.Children.Add(button);
            if (!dark) _currentSwatches.Add((button, role));
        }
        panel.Children.Add(swatches);
        return panel;
    }

    private Button BuildSwatch(RoleSpec role, bool dark)
    {
        var roleText = new TextBlock { Text = role.Name, TextTrimming = TextTrimming.CharacterEllipsis };
        MaterialTypography.SetStyle(roleText, MaterialTypeStyle.LabelMedium);
        var tokenText = new TextBlock { Text = dark ? role.DarkToken : role.LightToken };
        MaterialTypography.SetStyle(tokenText, MaterialTypeStyle.BodySmall);

        var text = new StackPanel { Spacing = 0 };
        text.Children.Add(roleText);
        text.Children.Add(tokenText);

        var button = new Button
        {
            Content = text,
            Width = SwatchWidth,
            MinHeight = 56,
            Padding = new Thickness(12, 8, 12, 8),
            HorizontalContentAlignment = HorizontalAlignment.Left,
            VerticalContentAlignment = VerticalAlignment.Center,
            CornerRadius = new CornerRadius(12),
            Margin = new Thickness(0, 0, 4, 4)
        };
        MaterialAssist.SetButtonVariant(button, MaterialButtonVariant.Text);
        MaterialAssist.SetButtonShape(button, MaterialButtonShape.Square);
        MaterialAssist.SetContainerInset(button, new Thickness(0));
        button.Bind(TemplatedControl.BackgroundProperty, button.GetResourceObservable(role.BrushKey));
        button.Bind(TemplatedControl.ForegroundProperty, button.GetResourceObservable(role.TextBrushKey));
        roleText[!TextBlock.ForegroundProperty] = button[!TemplatedControl.ForegroundProperty];
        tokenText[!TextBlock.ForegroundProperty] = button[!TemplatedControl.ForegroundProperty];

        AutomationProperties.SetName(button, role.Name);
        AutomationProperties.SetHelpText(button, $"Copy {role.BrushKey} as hex");
        button.Click += async (_, _) => await CopyAsync(role, dark);
        return button;
    }

    private async System.Threading.Tasks.Task CopyAsync(RoleSpec role, bool dark)
    {
        if (!this.TryFindResource(role.ColorKey, dark ? ThemeVariant.Dark : ActualThemeVariant, out var value) ||
            value is not Color color)
            return;

        var args = new MaterialSwatchCopiedEventArgs(role.Name, dark ? role.DarkToken : role.LightToken, color);
        SwatchCopied?.Invoke(this, args);
        if (_status is not null) _status.Text = $"Copied {role.Name} {args.Hex}";
        if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
            await clipboard.SetTextAsync(args.Hex);
    }

    private void UpdateTokens()
    {
        var dark = ActualThemeVariant == ThemeVariant.Dark;
        foreach (var (button, role) in _currentSwatches)
        {
            if (button.Content is not StackPanel { Children: [_, TextBlock token] }) continue;
            token.Text = dark ? role.DarkToken : role.LightToken;
        }
    }

    private static IReadOnlyList<RoleSpec> Roles(string role, string tokenPrefix) =>
    [
        new(role, $"Material{role}Color", $"Material{role}Brush", $"MaterialOn{role}Brush",
            Token(tokenPrefix, 40), Token(tokenPrefix, 80)),
        new($"On {role}", $"MaterialOn{role}Color", $"MaterialOn{role}Brush", $"Material{role}Brush",
            Token(tokenPrefix, 100), Token(tokenPrefix, 20)),
        new($"{role} container", $"Material{role}ContainerColor", $"Material{role}ContainerBrush",
            $"MaterialOn{role}ContainerBrush", Token(tokenPrefix, 90), Token(tokenPrefix, 30)),
        new($"On {role} container", $"MaterialOn{role}ContainerColor", $"MaterialOn{role}ContainerBrush",
            $"Material{role}ContainerBrush", Token(tokenPrefix, 30), Token(tokenPrefix, 90))
    ];

    private static IReadOnlyList<RoleSpec> FixedRoles(string role, string tokenPrefix, string variantPrefix) =>
    [
        new($"{role} fixed", $"Material{role}FixedColor", $"Material{role}FixedBrush", $"MaterialOn{role}FixedBrush",
            Token(tokenPrefix, 90), Token(tokenPrefix, 90)),
        new($"{role} fixed dim", $"Material{role}FixedDimColor", $"Material{role}FixedDimBrush",
            $"MaterialOn{role}FixedBrush", Token(tokenPrefix, 80), Token(tokenPrefix, 80)),
        new($"On {role} fixed", $"MaterialOn{role}FixedColor", $"MaterialOn{role}FixedBrush", $"Material{role}FixedBrush",
            Token(tokenPrefix, 10), Token(tokenPrefix, 10)),
        new($"On {role} fixed variant", $"MaterialOn{role}FixedVariantColor", $"MaterialOn{role}FixedVariantBrush",
            $"Material{role}FixedBrush", Token(variantPrefix, 30), Token(variantPrefix, 30))
    ];

    private static IReadOnlyList<RoleSpec> SurfaceRoles() =>
    [
        Surface("Surface dim", "SurfaceDim", 87, 6),
        Surface("Surface", "Surface", 98, 6),
        Surface("Surface bright", "SurfaceBright", 98, 24),
        Surface("Surface container lowest", "SurfaceContainerLowest", 100, 4),
        Surface("Surface container low", "SurfaceContainerLow", 96, 10),
        Surface("Surface container", "SurfaceContainer", 94, 12),
        Surface("Surface container high", "SurfaceContainerHigh", 92, 17),
        Surface("Surface container highest", "SurfaceContainerHighest", 90, 22),
        new("On surface", "MaterialOnSurfaceColor", "MaterialOnSurfaceBrush", "MaterialSurfaceBrush",
            "N-10", "N-90"),
        new("On surface variant", "MaterialOnSurfaceVariantColor", "MaterialOnSurfaceVariantBrush",
            "MaterialSurfaceBrush", "NV-30", "NV-80"),
        new("Outline", "MaterialOutlineColor", "MaterialOutlineBrush", "MaterialSurfaceBrush", "NV-50", "NV-60"),
        new("Outline variant", "MaterialOutlineVariantColor", "MaterialOutlineVariantBrush", "MaterialSurfaceBrush",
            "NV-80", "NV-30")
    ];

    private static RoleSpec Surface(string name, string suffix, int lightTone, int darkTone) =>
        new(name, $"Material{suffix}Color", $"Material{suffix}Brush", "MaterialOnSurfaceBrush",
            $"N-{lightTone.ToString(CultureInfo.InvariantCulture)}",
            $"N-{darkTone.ToString(CultureInfo.InvariantCulture)}");

    private static IReadOnlyList<RoleSpec> InverseRoles() =>
    [
        new("Inverse surface", "MaterialInverseSurfaceColor", "MaterialInverseSurfaceBrush",
            "MaterialInverseOnSurfaceBrush", "N-20", "N-90"),
        new("Inverse on surface", "MaterialInverseOnSurfaceColor", "MaterialInverseOnSurfaceBrush",
            "MaterialInverseSurfaceBrush", "N-95", "N-20"),
        new("Inverse primary", "MaterialInversePrimaryColor", "MaterialInversePrimaryBrush",
            "MaterialInverseSurfaceBrush", "P-80", "P-40"),
        new("Scrim", "MaterialScrimColor", "MaterialScrimBrush", "MaterialInverseOnSurfaceBrush", "N-0", "N-0"),
        new("Shadow", "MaterialShadowColor", "MaterialShadowBrush", "MaterialInverseOnSurfaceBrush", "N-0", "N-0")
    ];

    private static string Token(string prefix, int tone) => $"{prefix}-{tone.ToString(CultureInfo.InvariantCulture)}";

    private sealed record RoleSpec(
        string Name,
        string ColorKey,
        string BrushKey,
        string TextBrushKey,
        string LightToken,
        string DarkToken);
}
