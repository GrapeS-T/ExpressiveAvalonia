using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace ExpressiveAvalonia.Theming;

/// <summary>The Material tab indicator treatment.</summary>
public enum MaterialTabVariant
{
    Primary,
    Secondary
}

/// <summary>Appearance and header keyboard navigation for standard Avalonia tabs.</summary>
public sealed class MaterialTabs : AvaloniaObject
{
    /// <summary>Primary uses a short rounded indicator; secondary uses a full-width indicator.</summary>
    public static readonly AttachedProperty<MaterialTabVariant> VariantProperty =
        AvaloniaProperty.RegisterAttached<MaterialTabs, Control, MaterialTabVariant>(
            "Variant", MaterialTabVariant.Primary, inherits: true);

    /// <summary>Whether tab headers scroll instead of sharing the available width equally.</summary>
    public static readonly AttachedProperty<bool> IsScrollableProperty =
        AvaloniaProperty.RegisterAttached<MaterialTabs, TabControl, bool>("IsScrollable");

    /// <summary>Whether a native tab has both an icon and a label, requiring the 64px primary header.</summary>
    public static readonly AttachedProperty<bool> HasIconAndHeaderProperty =
        AvaloniaProperty.RegisterAttached<MaterialTabs, TabItem, bool>("HasIconAndHeader");

    static MaterialTabs()
    {
        InputElement.KeyDownEvent.AddClassHandler<TabControl>(OnKeyDown, RoutingStrategies.Tunnel);
        TabItem.IconProperty.Changed.AddClassHandler<TabItem>(static (item, _) => UpdateHeaderLayout(item));
        TabItem.HeaderProperty.Changed.AddClassHandler<TabItem>(static (item, _) => UpdateHeaderLayout(item));
    }

    public static MaterialTabVariant GetVariant(Control control) => control.GetValue(VariantProperty);
    public static void SetVariant(Control control, MaterialTabVariant value) => control.SetValue(VariantProperty, value);
    public static bool GetIsScrollable(TabControl control) => control.GetValue(IsScrollableProperty);
    public static void SetIsScrollable(TabControl control, bool value) => control.SetValue(IsScrollableProperty, value);
    public static bool GetHasIconAndHeader(TabItem item) => item.GetValue(HasIconAndHeaderProperty);

    private static void UpdateHeaderLayout(TabItem item) =>
        item.SetValue(HasIconAndHeaderProperty, item.Icon is not null && item.Header is not null);

    private static void OnKeyDown(TabControl tabs, KeyEventArgs e)
    {
        if (e.Handled || e.KeyModifiers != KeyModifiers.None)
            return;

        // Never consume navigation from the selected page (text fields, lists, nested tabs).
        TabItem? header = null;
        for (var visual = e.Source as Visual; visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is TabItem item)
                header = item;
            if (visual is TabControl owner)
            {
                if (owner != tabs || header is null)
                    return;
                break;
            }
        }
        if (header is null)
            return;

        var vertical = tabs.TabStripPlacement is Dock.Left or Dock.Right;
        var forward = vertical ? Key.Down : Key.Right;
        var backward = vertical ? Key.Up : Key.Left;
        if (!vertical && tabs.FlowDirection == Avalonia.Media.FlowDirection.RightToLeft)
            (forward, backward) = (backward, forward);

        var count = tabs.ItemCount;
        if (count == 0)
            return;

        int index;
        int step;
        if (e.Key == Key.Home)
            (index, step) = (-1, 1);
        else if (e.Key == Key.End)
            (index, step) = (count, -1);
        else if (e.Key == forward || e.Key == backward)
            (index, step) = (tabs.IndexFromContainer(header), e.Key == forward ? 1 : -1);
        else
            return;

        for (var visited = 0; visited < count; visited++)
        {
            index = (index + step + count) % count;
            if (tabs.ContainerFromIndex(index) is TabItem { IsEffectivelyEnabled: true, IsVisible: true } target)
            {
                tabs.SetCurrentValue(TabControl.SelectedIndexProperty, index);
                target.Focus(NavigationMethod.Directional);
                target.BringIntoView();
                e.Handled = true;
                return;
            }
        }
    }
}
