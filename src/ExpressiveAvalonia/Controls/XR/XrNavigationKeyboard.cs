using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// Keyboard selection shared by the XR navigation bar and rail. Both controls are
/// <see cref="ListBox"/> instances, so selection, focus, automation, and disabled handling stay
/// native; this helper only maps the axis arrows, Home/End, and Enter/Space onto them.
/// </summary>
internal static class XrNavigationKeyboard
{
    internal static bool TryHandle(ListBox list, KeyEventArgs e, bool vertical)
    {
        if (e.Handled || e.KeyModifiers != KeyModifiers.None || list.ItemCount == 0)
            return false;

        var handled = MoveCore(list, e, vertical);
        if (handled)
            e.Handled = true;
        return handled;
    }

    private static bool MoveCore(ListBox list, KeyEventArgs e, bool vertical)
    {
        var focused = FindItem(list, e.Source);
        var index = focused is not null ? list.IndexFromContainer(focused) : list.SelectedIndex;
        var rtl = list.FlowDirection == FlowDirection.RightToLeft;

        switch (e.Key)
        {
            case Key.Home:
                Select(list, 0, 1);
                return true;
            case Key.End:
                Select(list, list.ItemCount - 1, -1);
                return true;
            case Key.Enter:
            case Key.Space:
                if (focused is null)
                    return false;
                Select(list, index, 0);
                return true;
            case Key.Up when vertical:
            case Key.Left when !vertical:
                Move(list, index, -1, rtl && !vertical);
                return true;
            case Key.Down when vertical:
            case Key.Right when !vertical:
                Move(list, index, 1, rtl && !vertical);
                return true;
            default:
                return false;
        }
    }

    private static void Move(ListBox list, int index, int direction, bool reverse)
    {
        var delta = reverse ? -direction : direction;
        var start = index < 0 ? (delta > 0 ? -1 : 0) : index;
        var next = (start + delta + list.ItemCount) % list.ItemCount;
        Select(list, next, delta);
    }

    /// <summary>Selects <paramref name="index"/>, skipping disabled items in <paramref name="direction"/>.</summary>
    private static void Select(ListBox list, int index, int direction)
    {
        var count = list.ItemCount;
        if (count == 0)
            return;

        index = Math.Clamp(index, 0, count - 1);
        for (var attempt = 0; attempt < count; attempt++)
        {
            if (list.ContainerFromIndex(index) is ListBoxItem { IsEffectivelyEnabled: true } item)
            {
                list.SelectedIndex = index;
                item.Focus(NavigationMethod.Directional);
                return;
            }

            if (direction == 0)
                return;
            index = (index + direction + count) % count;
        }
    }

    private static ListBoxItem? FindItem(ListBox list, object? source)
    {
        for (var visual = source as Visual; visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is ListBoxItem item && list.IndexFromContainer(item) >= 0)
                return item;
        }

        return null;
    }
}
