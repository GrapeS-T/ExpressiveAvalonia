using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace ExpressiveAvalonia.Controls;

/// <summary>A checkbox list for <see cref="MaterialDialog.List"/>.</summary>
/// <remarks>
/// <para>
/// Items are native <see cref="ListBoxItem"/> containers, so pointer, focus and automation
/// behavior come from Avalonia rather than a custom row. The default selection mode is
/// <see cref="SelectionMode.Multiple"/> combined with <see cref="SelectionMode.Toggle"/>:
/// clicking or pressing Space on a row toggles only that row, which is the checkbox semantics of
/// the source dialog family. The row checkbox reflects <see cref="ListBoxItem.IsSelected"/> and
/// is not interactive, so a click can never toggle the same row twice.
/// </para>
/// <para>
/// Arrow, Home and End keys move focus between rows without changing which rows are checked,
/// matching a checkbox list. Modified keys (for example Shift) keep the native ListBox
/// range behavior.
/// </para>
/// <para>
/// <see cref="IsScrollable"/> keeps the list viewport at the source family's 170 unit height and
/// enables the vertical scrollbar; without it the list grows to fit its rows. Items added directly
/// as <see cref="MaterialDialogListItem"/> are their own containers. Any other item is wrapped in
/// a generated <see cref="MaterialDialogListItem"/> and shown through Content/ContentTemplate.
/// </para>
/// </remarks>
public class MaterialDialogList : ListBox
{
    /// <summary>Constrains the list to the dialog list viewport and enables scrolling.</summary>
    public static readonly StyledProperty<bool> IsScrollableProperty =
        AvaloniaProperty.Register<MaterialDialogList, bool>(nameof(IsScrollable));

    public MaterialDialogList()
    {
        SelectionMode = SelectionMode.Multiple | SelectionMode.Toggle;
        Padding = default;
    }

    /// <summary>Whether the list scrolls inside a fixed viewport instead of growing.</summary>
    public bool IsScrollable
    {
        get => GetValue(IsScrollableProperty);
        set => SetValue(IsScrollableProperty, value);
    }

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey)
        => new MaterialDialogListItem();

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
        => NeedsContainer<MaterialDialogListItem>(item, out recycleKey);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // Native ListBox arrow navigation writes the selection, which in a checkbox list would
        // tick every row the caret passes. Move focus only; Space toggles the focused row.
        if (e.KeyModifiers == KeyModifiers.None && e.Key is Key.Up or Key.Down or Key.Home or Key.End &&
            MoveFocusWithoutSelection(e.Key))
        {
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }

    private bool MoveFocusWithoutSelection(Key key)
    {
        if (ItemCount == 0)
            return false;

        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;
        var index = focused is not null ? IndexOfContainer(focused) : -1;
        if (index < 0)
            index = SelectedIndex >= 0 ? SelectedIndex : 0;

        var target = key switch
        {
            Key.Up => index - 1,
            Key.Down => index + 1,
            Key.Home => 0,
            Key.End => ItemCount - 1,
            _ => index,
        };
        target = Math.Clamp(target, 0, ItemCount - 1);

        ScrollIntoView(target);
        var container = ContainerFromIndex(target);
        if (container is null)
        {
            // Virtualization may not have realized the row yet; scrolling first then retrying
            // keeps keyboard navigation working in a long scrollable dialog list.
            UpdateLayout();
            container = ContainerFromIndex(target);
        }
        return container?.Focus() ?? false;
    }

    private int IndexOfContainer(Control control)
    {
        // Focus may sit on a descendant of the row, so resolve the nearest item container first.
        foreach (var ancestor in control.GetSelfAndVisualAncestors().OfType<Control>())
        {
            var index = IndexFromContainer(ancestor);
            if (index >= 0)
                return index;
        }
        return -1;
    }
}
