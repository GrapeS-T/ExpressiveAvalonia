using System;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Theming;
using ExpressiveAvalonia.Tokens;

namespace ExpressiveAvalonia.Controls;

/// <summary>The position of a menu group in the menu, which selects the source group shape.</summary>
/// <remarks>
/// Mirrors <c>MenuDefaults.groupShape(index, count)</c> and <c>SegmentedMenuTokens</c>: a standalone
/// group is CornerLarge (16 DIP) on every corner, the leading group is 16 DIP on the outer top
/// corners and CornerSmall (8 DIP) below, the middle group is 8 DIP everywhere and the trailing
/// group mirrors the leading one. Once a group has been hovered and the pointer leaves, it settles
/// on <c>InactiveContainerShape</c> (8 DIP), exactly as <c>shapeByInteraction</c> does.
/// </remarks>
public enum MaterialMenuGroupPosition { Standalone, Leading, Middle, Trailing }

/// <summary>
/// A group inside a menu, following the expressive <c>DropdownMenuGroup</c> anatomy.
/// </summary>
/// <remarks>
/// <para>
/// The header is optional; when it is set the group renders the 32 DIP Material group label
/// (labelLarge, 12/4 padding) above its items on the group surface. Content padding is the source
/// <c>DropdownMenuGroupContentPadding</c> (0 horizontal, 2 DIP vertical).
/// </para>
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
    public static readonly StyledProperty<MaterialMenuGroupPosition> PositionProperty =
        AvaloniaProperty.Register<MaterialMenuGroup, MaterialMenuGroupPosition>(nameof(Position));

    private Border? _surface;
    private MaterialMotion.SpringAnimation? _morph;
    private CornerRadius _morphFrom;
    private CornerRadius _morphTo;
    private bool _hasBeenHovered;
    private bool _hovered;

    static MaterialMenuGroup()
    {
        FocusableProperty.OverrideDefaultValue<MaterialMenuGroup>(false);
        PositionProperty.Changed.AddClassHandler<MaterialMenuGroup>((group, _) => group.UpdateGroupShape());
    }

    public MaterialMenuGroup()
    {
        AddHandler(MenuItem.ClickEvent, OnMenuItemClick, RoutingStrategies.Bubble);
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    /// <summary>The group's position in the menu; selects its outer corner shape.</summary>
    public MaterialMenuGroupPosition Position
    {
        get => GetValue(PositionProperty);
        set => SetValue(PositionProperty, value);
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (string.IsNullOrEmpty(AutomationProperties.GetName(this)) && Header is string header && !string.IsNullOrWhiteSpace(header))
            AutomationProperties.SetName(this, header);

        _surface = e.NameScope.Find<Border>("PART_Surface");
        UpdateGroupShape(animate: false);
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // Native menus wrap arbitrary content in a MenuItem. The wrapper is structural;
        // allowing it to focus makes its selection handler steal focus from a group row.
        if (this.GetVisualAncestors().OfType<MenuItem>().FirstOrDefault() is { } wrapper &&
            ReferenceEquals(wrapper.Header, this))
            wrapper.SetCurrentValue(FocusableProperty, false);
        _hasBeenHovered = false;
        _hovered = false;
        UpdateGroupShape(animate: false);
    }

    /// <inheritdoc />
    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        _hasBeenHovered = true;
        _hovered = true;
        UpdateGroupShape();
    }

    /// <inheritdoc />
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hovered = false;
        UpdateGroupShape();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == MaterialMenu.SurfaceStyleProperty || change.Property == MaterialMenu.VariantProperty)
            UpdateGroupShape(animate: false);
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

    /// <summary>
    /// Native <see cref="MenuItem"/> handles Up/Down within its own ItemsControl (this group), so a
    /// tunnel handler moves focus to the neighbouring group or menu item when the arrow would leave
    /// the group. Left/Right, Escape, Home/End and access keys keep their native menu behavior.
    /// </summary>
    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.Key is not (Key.Up or Key.Down) || e.Source is not MenuItem source)
            return;

        var items = ItemsInOrder();
        if (items.Length == 0)
            return;

        var index = System.Array.IndexOf(items, source);
        if (index < 0)
            return;

        var delta = e.Key == Key.Down ? 1 : -1;
        var boundary = delta > 0 ? index >= LastEnabledIndex(items) : index <= FirstEnabledIndex(items);
        if (boundary && MoveToSibling(delta))
            e.Handled = true;
    }

    private static int FirstEnabledIndex(MenuItem[] items)
    {
        for (var i = 0; i < items.Length; i++)
            if (items[i].IsEffectivelyEnabled)
                return i;
        return items.Length - 1;
    }

    private static int LastEnabledIndex(MenuItem[] items)
    {
        for (var i = items.Length - 1; i >= 0; i--)
            if (items[i].IsEffectivelyEnabled)
                return i;
        return 0;
    }

    private bool MoveToSibling(int delta)
    {
        var parent = this.GetLogicalAncestors().OfType<ItemsControl>().FirstOrDefault(x => !ReferenceEquals(x, this));
        if (parent is null)
            return false;

        var siblings = parent.Items.OfType<Control>().ToList();
        var index = siblings.IndexOf(this);
        if (index < 0)
            return false;

        for (var step = 1; step < siblings.Count; step++)
        {
            var next = (index + delta * step % siblings.Count + siblings.Count) % siblings.Count;
            switch (siblings[next])
            {
                case MaterialMenuGroup group:
                    var target = delta > 0
                        ? group.ItemsInOrder().FirstOrDefault(x => x.IsEffectivelyEnabled)
                        : group.ItemsInOrder().LastOrDefault(x => x.IsEffectivelyEnabled);
                    if (target is not null)
                    {
                        target.Focus(NavigationMethod.Directional);
                        return true;
                    }
                    break;
                case MenuItem item when item.IsEffectivelyEnabled:
                    item.Focus(NavigationMethod.Directional);
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Resolves the source group shape for the current position and hover history. Hover leaves the
    /// group at <c>InactiveContainerShape</c>; the morph uses MotionSchemeKeyTokens.FastSpatial.
    /// </summary>
    private void UpdateGroupShape(bool animate = true)
    {
        if (_surface is null)
            return;

        var expressive = MaterialMenu.GetSurfaceStyle(this) == MaterialMenuSurfaceStyle.Expressive;
        var target = !expressive
            ? default
            : Position switch
            {
                MaterialMenuGroupPosition.Leading => new CornerRadius(16, 16, 8, 8),
                MaterialMenuGroupPosition.Trailing => new CornerRadius(8, 8, 16, 16),
                MaterialMenuGroupPosition.Middle => new CornerRadius(8),
                _ => _hasBeenHovered && !_hovered ? new CornerRadius(8) : new CornerRadius(16),
            };

        _morph?.Dispose();
        _morph = null;

        if (!animate || MaterialMotion.GetIsReduced(this) || _surface.CornerRadius == target)
        {
            _surface.CornerRadius = target;
            return;
        }

        _morphFrom = _surface.CornerRadius;
        _morphTo = target;
        _morph = MaterialMotion.Animate(this, 0, 1, progress =>
        {
            if (_surface is not null)
                _surface.CornerRadius = Interpolate(_morphFrom, _morphTo, progress);
        }, MaterialSpring.SpatialFast, visibilityThreshold: 0.005);
    }

    private static CornerRadius Interpolate(CornerRadius from, CornerRadius to, double progress) =>
        new(
            from.TopLeft + (to.TopLeft - from.TopLeft) * progress,
            from.TopRight + (to.TopRight - from.TopRight) * progress,
            from.BottomRight + (to.BottomRight - from.BottomRight) * progress,
            from.BottomLeft + (to.BottomLeft - from.BottomLeft) * progress);

    private MenuItem[] ItemsInOrder() => Items.OfType<MenuItem>().ToArray();

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