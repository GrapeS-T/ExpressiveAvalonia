using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// An XR navigation bar: a rounded surface container of three, four, or five icon-and-label
/// destinations with single selection.
/// </summary>
/// <remarks>
/// The control is a <see cref="ListBox"/> with an <see cref="XrNavigationBarItem"/> container,
/// so selection, SelectionItemPattern, disabled items, and pointer activation are native.
/// Left/Right, Home/End, and Enter/Space select and focus destinations. Items may be item
/// controls or strings (used as the label). The segment axis is expressed by the item count.
/// </remarks>
public class XrNavigationBar : ListBox
{
    public XrNavigationBar()
    {
        SelectionMode = SelectionMode.Single;
        ItemsPanel = new FuncTemplate<Panel?>(() => new StackPanel
        {
            Orientation = Orientation.Horizontal
        });
    }

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) =>
        new XrNavigationBarItem();

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        if (container is XrNavigationBarItem destination && destination.Label is null && item is string text)
            destination.Label = text;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!XrNavigationKeyboard.TryHandle(this, e, vertical: false))
            base.OnKeyDown(e);
    }
}
