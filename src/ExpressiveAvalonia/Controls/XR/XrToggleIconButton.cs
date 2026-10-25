using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// A toggleable XR icon button. The selected container is the surface's emphasized role and
/// the optional <see cref="SelectedIcon"/> replaces the glyph while checked.
/// </summary>
/// <remarks>
/// Native <see cref="ToggleButton"/> behavior: Space/Enter toggle, <c>IsChecked</c> is
/// nullable, commands execute on click, and the toggle automation peer exposes TogglePattern.
/// </remarks>
public class XrToggleIconButton : ToggleButton
{
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<XrToggleIconButton, Geometry?>(nameof(Icon));

    /// <summary>The glyph shown while checked; falls back to <see cref="Icon"/> when unset.</summary>
    public static readonly StyledProperty<Geometry?> SelectedIconProperty =
        AvaloniaProperty.Register<XrToggleIconButton, Geometry?>(nameof(SelectedIcon));

    public static readonly StyledProperty<XrSurfaceRole> SurfaceProperty =
        AvaloniaProperty.Register<XrToggleIconButton, XrSurfaceRole>(nameof(Surface));

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<XrToggleIconButton, string?>(nameof(Label));

    /// <summary>The checked glyph, resolved from <see cref="SelectedIcon"/> and <see cref="Icon"/>.</summary>
    public static readonly DirectProperty<XrToggleIconButton, Geometry?> EffectiveSelectedIconProperty =
        AvaloniaProperty.RegisterDirect<XrToggleIconButton, Geometry?>(nameof(EffectiveSelectedIcon),
            button => button.EffectiveSelectedIcon);

    private Geometry? _effectiveSelectedIcon;

    static XrToggleIconButton()
    {
        IconProperty.Changed.AddClassHandler<XrToggleIconButton>((button, _) => button.UpdateSelectedIcon());
        SelectedIconProperty.Changed.AddClassHandler<XrToggleIconButton>((button, _) => button.UpdateSelectedIcon());
        SurfaceProperty.Changed.AddClassHandler<XrToggleIconButton>((button, _) => button.UpdateSurfaceState());
        LabelProperty.Changed.AddClassHandler<XrToggleIconButton>((button, _) => button.UpdateAutomationName());
    }

    public XrToggleIconButton()
    {
        UpdateSurfaceState();
        UpdateSelectedIcon();
    }

    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    public Geometry? SelectedIcon { get => GetValue(SelectedIconProperty); set => SetValue(SelectedIconProperty, value); }

    public XrSurfaceRole Surface { get => GetValue(SurfaceProperty); set => SetValue(SurfaceProperty, value); }

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public Geometry? EffectiveSelectedIcon => _effectiveSelectedIcon;

    private string? _appliedName;

    private void UpdateSelectedIcon()
    {
        var icon = SelectedIcon ?? Icon;
        if (icon == _effectiveSelectedIcon)
            return;

        var previous = _effectiveSelectedIcon;
        _effectiveSelectedIcon = icon;
        RaisePropertyChanged(EffectiveSelectedIconProperty, previous, icon);
    }

    private void UpdateSurfaceState()
    {
        PseudoClasses.Set(":surface-high", Surface == XrSurfaceRole.SurfaceContainerHigh);
        PseudoClasses.Set(":tertiary-container", Surface == XrSurfaceRole.TertiaryContainer);
    }

    private void UpdateAutomationName()
    {
        var label = Label;
        if (string.IsNullOrEmpty(label))
            return;

        var current = AutomationProperties.GetName(this);
        if (string.IsNullOrEmpty(current) || current == _appliedName)
        {
            AutomationProperties.SetName(this, label);
            _appliedName = label;
        }
    }
}
