using Avalonia.Controls.Primitives;

namespace ExpressiveAvalonia.Controls;

/// <summary>A 1 DIP group separator inside a <see cref="MaterialNavigationDrawer"/>.</summary>
/// <remarks>
/// Matches the source middle-inset divider: the outline-variant line is inset 16 DIP from both
/// drawer edges. The control contributes only the line to layout, so consecutive groups stay
/// aligned with the source 56 DIP rhythm. It is decorative and therefore not focusable.
/// </remarks>
public sealed class MaterialNavigationDivider : TemplatedControl
{
    public MaterialNavigationDivider()
    {
        Focusable = false;
        IsTabStop = false;
    }
}
