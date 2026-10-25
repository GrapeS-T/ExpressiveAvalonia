using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;

namespace ExpressiveAvalonia.Controls;

/// <summary>A non-interactive group heading inside a <see cref="MaterialNavigationDrawer"/>.</summary>
/// <remarks>
/// Matches the source section header: 56 DIP high, 18 DIP horizontal and 16 DIP vertical
/// padding, full radius, title-small typography on the on-surface-variant role. Content is the
/// heading text and is exposed to assistive technology as a level-2 heading. Headers are not
/// focusable, so arrow-key traversal skips them.
/// </remarks>
public sealed class MaterialNavigationSectionHeader : ContentControl
{
    public MaterialNavigationSectionHeader()
    {
        Focusable = false;
        IsTabStop = false;
        AutomationProperties.SetHeadingLevel(this, 2);
    }
}
