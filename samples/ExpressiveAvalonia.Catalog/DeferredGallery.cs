using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace ExpressiveAvalonia.Catalog;

/// <summary>Owns only the currently attached example tree; never caches visited pages.</summary>
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
