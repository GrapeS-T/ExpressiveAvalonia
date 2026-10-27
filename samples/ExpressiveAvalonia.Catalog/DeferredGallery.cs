using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace ExpressiveAvalonia.Catalog;

/// <summary>
/// Owns only the currently attached example tree; never caches visited pages.
/// </summary>
/// <remarks>
/// The page control is created when the tab is first attached and dropped when the tab is
/// detached, so a visited page contributes no visual tree, event subscriptions or animation
/// loops while another tab is selected. Local page state therefore resets on every revisit;
/// anything that must survive a tab switch belongs to the catalog model, not to the page.
/// <see cref="Program.Preview"/> keeps the tree alive because the exporter inspects every page
/// from one window.
/// </remarks>
internal sealed class DeferredGallery(Func<Control> create) : Decorator
{
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Child ??= create();
        base.OnAttachedToVisualTree(e);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (!Program.Preview) Child = null;
    }
}
