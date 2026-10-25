using System.Linq;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// A labelled section inside a menu. The header is optional; when it is set the group renders the
/// 32 DIP Material section label above its items and gives the items an 8 DIP rounded surface.
/// </summary>
/// <remarks>
/// <para>
/// Items inherit <see cref="MaterialMenu.SurfaceStyleProperty"/>, <see cref="MaterialMenu.VariantProperty"/>
/// and <see cref="MaterialMenu.DensityProperty"/> from the group, so one
/// <c>md:MaterialMenu.SurfaceStyle="Expressive"</c> configures the whole section.
/// </para>
/// <para>
/// Inside a <see cref="Menu"/>, <see cref="ContextMenu"/> or <see cref="MenuFlyout"/> the native
/// menu interaction handler owns navigation and activation. When the group is hosted on its own
/// surface, this control supplies the equivalent contract: Up/Down move focus between items,
/// Enter/Space activate the focused item, and checkbox/radio items toggle on activation.
/// </para>
/// </remarks>
public class MaterialMenuGroup : HeaderedItemsControl
{
    static MaterialMenuGroup()
    {
        FocusableProperty.OverrideDefaultValue<MaterialMenuGroup>(false);
    }

    public MaterialMenuGroup()
    {
        AddHandler(MenuItem.ClickEvent, OnMenuItemClick, RoutingStrategies.Bubble);
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (string.IsNullOrEmpty(AutomationProperties.GetName(this)) && Header is string header && !string.IsNullOrWhiteSpace(header))
            AutomationProperties.SetName(this, header);
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!IsEmbeddedInMenu && e.Source is MenuItem source)
        {
            switch (e.Key)
            {
                case Key.Down:
                    MoveFocus(source, 1);
                    e.Handled = true;
                    return;
                case Key.Up:
                    MoveFocus(source, -1);
                    e.Handled = true;
                    return;
                case Key.Enter:
                case Key.Space:
                    Activate(source);
                    e.Handled = true;
                    return;
            }
        }

        base.OnKeyDown(e);
    }

    private bool IsEmbeddedInMenu =>
        this.FindAncestorOfType<MenuBase>() is not null || this.FindAncestorOfType<MenuFlyoutPresenter>() is not null;

    private MenuItem[] ItemsInOrder()
    {
        var realized = GetRealizedContainers().OfType<MenuItem>().ToArray();
        return realized is { Length: > 0 } ? realized : Items.OfType<MenuItem>().ToArray();
    }

    private void MoveFocus(MenuItem current, int delta)
    {
        var items = ItemsInOrder();
        if (items.Length == 0)
            return;

        var index = System.Array.IndexOf(items, current);
        if (index < 0)
            index = delta > 0 ? -1 : items.Length;

        for (var step = 0; step < items.Length; step++)
        {
            index = (index + delta + items.Length) % items.Length;
            var candidate = items[index];
            if (candidate.IsEffectivelyEnabled)
            {
                candidate.Focus(NavigationMethod.Directional);
                return;
            }
        }
    }

    private void Activate(MenuItem item)
    {
        if (!item.IsEffectivelyEnabled)
            return;

        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
    }

    private void OnMenuItemClick(object? sender, RoutedEventArgs e)
    {
        // The native menu handler already applies toggle semantics for embedded menus.
        if (IsEmbeddedInMenu || e.Source is not MenuItem item || item.ToggleType == MenuItemToggleType.None)
            return;

        if (item.ToggleType == MenuItemToggleType.Radio)
        {
            item.IsChecked = true;
            foreach (var sibling in ItemsInOrder().Where(x => x != item && x.GroupName == item.GroupName))
                sibling.IsChecked = false;
        }
        else
        {
            item.IsChecked = !item.IsChecked;
        }
    }
}
