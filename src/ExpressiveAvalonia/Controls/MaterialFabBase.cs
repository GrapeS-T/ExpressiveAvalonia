using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Controls;

/// <summary>The Material color treatment shared by the FAB family.</summary>
/// <remarks>
/// The three container roles are the resting FAB colors; the three solid roles are the
/// high-emphasis variants (a filled container with an on-color icon). Source packages:
/// FAB (local-001450), Extended FAB (local-001377), FAB Menu (local-001523).
/// </remarks>
public enum MaterialFabColorRole
{
    PrimaryContainer,
    SecondaryContainer,
    TertiaryContainer,
    Primary,
    Secondary,
    Tertiary
}

/// <summary>
/// Shared Material FAB behavior: color role, focus ring geometry and interaction elevation.
/// </summary>
/// <remarks>
/// <para>
/// The class derives from <see cref="Button"/>, so command execution, access keys,
/// Enter/Space activation, <c>:pressed</c>, <c>:focus-visible</c>, and the disabled state all
/// come from Avalonia rather than a reimplementation.
/// </para>
/// <para>
/// Elevation follows the pinned <c>FloatingActionButton.kt</c> elevation animator: the most
/// recently activated interaction wins, hover raises the FAB to level 4, every other interaction
/// settles at level 3, incoming changes take 120 ms and outgoing changes take 150 ms (120 ms when
/// leaving hover). The shared <see cref="MaterialElevationAnimation"/> publishes the interpolated
/// token shadow through <see cref="MaterialAssist.AnimatedShadowProperty"/>; the template's
/// <c>Border#Surface</c> binds it. Disabling snaps to the flat level-0 adaptation, and reduced
/// motion snaps every change.
/// </para>
/// <para>
/// The template contract is <c>Border#Surface</c>, <c>Border#ContentHost</c>,
/// <c>Border#FocusRing</c> and a <see cref="MaterialRipple"/> named <c>Ripple</c>. The shared
/// ripple is the source RippleNode: it draws the hover/focus state layer and the bounded press
/// ripple from the press origin, with the FAB's 8% pressed alpha and the source release timeline.
/// It follows reduced motion, so a held press keeps one static indication.
/// </para>
/// </remarks>
public abstract class MaterialFabBase : Button
{
    // Fab*Tokens: ContainerElevation/Focused/Pressed = Level3, Hovered = Level4. The pinned FAB
    // has no disabled elevation token; this port keeps the established flat disabled adaptation.
    private static readonly MaterialElevationSpecs FabElevationSpecs = new(3, 3, 3, 4, 3, 0);

    /// <summary>Defines the <see cref="ColorRole"/> property.</summary>
    public static readonly StyledProperty<MaterialFabColorRole> ColorRoleProperty =
        AvaloniaProperty.Register<MaterialFabBase, MaterialFabColorRole>(
            nameof(ColorRole), MaterialFabColorRole.PrimaryContainer);

    /// <summary>
    /// The focus indicator radius: the surface radius plus the source's two unit inset.
    /// </summary>
    /// <remarks>Template plumbing; set from <see cref="Border.CornerRadius"/>.</remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly StyledProperty<CornerRadius> FocusRingCornerRadiusProperty =
        AvaloniaProperty.Register<MaterialFabBase, CornerRadius>(nameof(FocusRingCornerRadius));

    private readonly MaterialElevationAnimation _elevation;
    private string? _autoAutomationName;

    static MaterialFabBase()
    {
        CornerRadiusProperty.Changed.AddClassHandler<MaterialFabBase>((fab, _) => fab.UpdateFocusRingRadius());
    }

    /// <summary>Initializes the shared interaction elevation animation.</summary>
    protected MaterialFabBase()
    {
        _elevation = new MaterialElevationAnimation(this, () => FabElevationSpecs);
    }

    /// <summary>Gets or sets the Material color role of the FAB container.</summary>
    public MaterialFabColorRole ColorRole
    {
        get => GetValue(ColorRoleProperty);
        set => SetValue(ColorRoleProperty, value);
    }

    /// <summary>Gets the corner radius used by the template's focus indicator.</summary>
    public CornerRadius FocusRingCornerRadius
    {
        get => GetValue(FocusRingCornerRadiusProperty);
        private set => SetValue(FocusRingCornerRadiusProperty, value);
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        UpdateFocusRingRadius();
        _elevation.Refresh(snap: true);
    }

    /// <summary>
    /// The interaction elevation currently applied to the FAB surface, in DIP levels.
    /// </summary>
    /// <remarks>Assembly diagnostics use only; the rendered value is the animated box shadow.</remarks>
    internal double ElevationLevel => _elevation.Value;

    /// <summary>
    /// Keeps <see cref="AutomationProperties.Name"/> in step with a text label without
    /// overwriting a name the application set explicitly.
    /// </summary>
    internal void SyncAutomationName(string? label)
    {
        var current = AutomationProperties.GetName(this);
        if (!string.IsNullOrEmpty(current) && current != _autoAutomationName)
            return;

        var name = label ?? string.Empty;
        AutomationProperties.SetName(this, name);
        _autoAutomationName = name;
    }

    private void UpdateFocusRingRadius()
    {
        var radius = CornerRadius;
        var uniform = Math.Max(0, radius.TopLeft + 2);
        FocusRingCornerRadius = new CornerRadius(uniform, radius.TopRight + 2, radius.BottomRight + 2, radius.BottomLeft + 2);
    }
}
