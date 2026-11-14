using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// A 48 by 48 icon button with an XR surface container, a 40 unit circular state layer,
/// and the expressive pressed shape.
/// </summary>
/// <remarks>
/// Desktop extension of the XR design source, not a port of <c>IconButton.kt</c> or of
/// <c>SpaceToggleButton.kt</c>. The 48 hit target / 40 container / 20 icon and the 8 unit pressed
/// shape come from the source component, and the interaction is native.
/// <para>
/// The control is a native <see cref="Button"/>: pointer, Space/Enter, access keys, commands,
/// focus, disabled handling, and the button automation peer are Avalonia's own.
/// The icon is a <see cref="Geometry"/> so its fill follows the surface's foreground role.
/// Set <see cref="Label"/> (or <c>AutomationProperties.Name</c>) because an icon-only button
/// has no text an assistive technology can read.
/// </para>
/// </remarks>
public class XrIconButton : Button
{
    /// <summary>The icon path, drawn at 20 units inside the 40 unit container.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<XrIconButton, Geometry?>(nameof(Icon));

    /// <summary>The emphasized surface the button belongs to.</summary>
    public static readonly StyledProperty<XrSurfaceRole> SurfaceProperty =
        AvaloniaProperty.Register<XrIconButton, XrSurfaceRole>(nameof(Surface));

    /// <summary>Accessible name applied when no explicit automation name is set.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<XrIconButton, string?>(nameof(Label));

    static XrIconButton()
    {
        SurfaceProperty.Changed.AddClassHandler<XrIconButton>((button, _) => button.UpdateSurfaceState());
        LabelProperty.Changed.AddClassHandler<XrIconButton>((button, _) => button.UpdateAutomationName());
    }

    public XrIconButton() => UpdateSurfaceState();

    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    public XrSurfaceRole Surface { get => GetValue(SurfaceProperty); set => SetValue(SurfaceProperty, value); }

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    private string? _appliedName;

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
