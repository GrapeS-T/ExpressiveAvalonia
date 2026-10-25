using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace ExpressiveAvalonia.Controls;

/// <summary>Where the rail's navigation items sit when they do not fill the rail.</summary>
public enum MaterialNavigationRailAlignment
{
    /// <summary>Items follow the menu/FAB group with the 40 unit group gap.</summary>
    Top,

    /// <summary>Items are centered in the full rail height, as in the M3 middle-aligned rail.</summary>
    Middle
}

/// <summary>The layout used by a navigation rail item.</summary>
public enum MaterialNavigationRailItemOrientation
{
    /// <summary>Derive the orientation from the rail: vertical when compact, horizontal when expanded.</summary>
    Auto,
    Vertical,
    Horizontal
}

/// <summary>The optional count indicator shown by a navigation rail item.</summary>
public enum MaterialNavigationRailBadge
{
    None,
    /// <summary>A six unit dot, overlaying the top-right of the icon.</summary>
    Small,
    /// <summary>A sixteen unit pill carrying <see cref="MaterialNavigationRailItem.BadgeText"/>.</summary>
    Large
}

/// <summary>
/// A Material 3 navigation rail with compact and expanded layouts, a menu button, a FAB,
/// and single-selection navigation over <see cref="MaterialNavigationRailItem"/> containers.
/// </summary>
/// <remarks>
/// Selection, focus, and automation use the native Avalonia list contracts: the rail is a
/// <see cref="ListBox"/> and its items are <see cref="ListBoxItem"/> containers, so
/// SelectionPattern/SelectionItemPattern, disabled items, and pointer selection keep working.
/// Keyboard selection wraps: Up/Left selects the previous item, Down/Right the next,
/// Home/End the first/last, and Enter/Space invokes the focused item.
/// Item icons are <see cref="Geometry"/> paths so their fill follows the selected, unselected,
/// and disabled roles instead of freezing a color inside user content.
/// </remarks>
[PseudoClasses(":expanded", ":floating", ":middle")]
[TemplatePart("PART_MenuFab", typeof(StackPanel))]
[TemplatePart("PART_ItemsPresenter", typeof(ItemsPresenter))]
[TemplatePart("PART_MenuButton", typeof(Button))]
[TemplatePart("PART_FabButton", typeof(Button))]
public class MaterialNavigationRail : ListBox
{
    /// <summary>Switches between the 96 unit compact rail and the 220 unit expanded rail.</summary>
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<MaterialNavigationRail, bool>(nameof(IsExpanded));

    /// <summary>Gives the expanded rail a surface-container background and 16 unit corner radius.</summary>
    public static readonly StyledProperty<bool> IsFloatingProperty =
        AvaloniaProperty.Register<MaterialNavigationRail, bool>(nameof(IsFloating));

    public static readonly StyledProperty<MaterialNavigationRailAlignment> RailAlignmentProperty =
        AvaloniaProperty.Register<MaterialNavigationRail, MaterialNavigationRailAlignment>(nameof(RailAlignment));

    /// <summary>Overrides the item orientation derived from <see cref="IsExpanded"/>.</summary>
    public static readonly StyledProperty<MaterialNavigationRailItemOrientation> ItemOrientationProperty =
        AvaloniaProperty.Register<MaterialNavigationRail, MaterialNavigationRailItemOrientation>(nameof(ItemOrientation));

    public static readonly StyledProperty<bool> ShowMenuButtonProperty =
        AvaloniaProperty.Register<MaterialNavigationRail, bool>(nameof(ShowMenuButton), true);

    public static readonly StyledProperty<bool> ShowFabProperty =
        AvaloniaProperty.Register<MaterialNavigationRail, bool>(nameof(ShowFab), true);

    public static readonly StyledProperty<Geometry?> MenuIconProperty =
        AvaloniaProperty.Register<MaterialNavigationRail, Geometry?>(nameof(MenuIcon),
            Geometry.Parse("M3,6 L21,6 L21,8 L3,8 Z M3,11 L21,11 L21,13 L3,13 Z M3,16 L21,16 L21,18 L3,18 Z"));

    public static readonly StyledProperty<Geometry?> FabIconProperty =
        AvaloniaProperty.Register<MaterialNavigationRail, Geometry?>(nameof(FabIcon),
            Geometry.Parse("M11,5 L13,5 L13,11 L19,11 L19,13 L13,13 L13,19 L11,19 L11,13 L5,13 L5,11 L11,11 Z"));

    /// <summary>Label of the expanded extended FAB. The compact FAB stays icon-only.</summary>
    public static readonly StyledProperty<string?> FabTextProperty =
        AvaloniaProperty.Register<MaterialNavigationRail, string?>(nameof(FabText));

    public static readonly StyledProperty<ICommand?> MenuCommandProperty =
        AvaloniaProperty.Register<MaterialNavigationRail, ICommand?>(nameof(MenuCommand));

    public static readonly StyledProperty<object?> MenuCommandParameterProperty =
        AvaloniaProperty.Register<MaterialNavigationRail, object?>(nameof(MenuCommandParameter));

    public static readonly StyledProperty<ICommand?> FabCommandProperty =
        AvaloniaProperty.Register<MaterialNavigationRail, ICommand?>(nameof(FabCommand));

    public static readonly StyledProperty<object?> FabCommandParameterProperty =
        AvaloniaProperty.Register<MaterialNavigationRail, object?>(nameof(FabCommandParameter));

    private StackPanel? _menuFab;
    private ItemsPresenter? _itemsPresenter;
    private Button? _fabButton;
    private Grid? _fabIconSlot;
    private TextBlock? _fabLabel;

    static MaterialNavigationRail()
    {
        IsExpandedProperty.Changed.AddClassHandler<MaterialNavigationRail>((rail, _) => rail.UpdateRailState());
        IsFloatingProperty.Changed.AddClassHandler<MaterialNavigationRail>((rail, _) => rail.UpdateRailState());
        RailAlignmentProperty.Changed.AddClassHandler<MaterialNavigationRail>((rail, _) => rail.UpdateRailState());
        ItemOrientationProperty.Changed.AddClassHandler<MaterialNavigationRail>((rail, _) => rail.UpdateItemOrientations());
        FabTextProperty.Changed.AddClassHandler<MaterialNavigationRail>((rail, _) => rail.UpdateFabContent());
        FabIconProperty.Changed.AddClassHandler<MaterialNavigationRail>((rail, _) => rail.UpdateFabContent());
        ShowMenuButtonProperty.Changed.AddClassHandler<MaterialNavigationRail>((rail, _) => rail.UpdateLayoutGeometry());
        ShowFabProperty.Changed.AddClassHandler<MaterialNavigationRail>((rail, _) => rail.UpdateLayoutGeometry());
    }

    /// <summary>Raised after a navigation item is activated by pointer or keyboard.</summary>
    public event EventHandler<MaterialNavigationRailItemInvokedEventArgs>? ItemInvoked;

    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }
    public bool IsFloating { get => GetValue(IsFloatingProperty); set => SetValue(IsFloatingProperty, value); }
    public MaterialNavigationRailAlignment RailAlignment { get => GetValue(RailAlignmentProperty); set => SetValue(RailAlignmentProperty, value); }
    public MaterialNavigationRailItemOrientation ItemOrientation { get => GetValue(ItemOrientationProperty); set => SetValue(ItemOrientationProperty, value); }
    public bool ShowMenuButton { get => GetValue(ShowMenuButtonProperty); set => SetValue(ShowMenuButtonProperty, value); }
    public bool ShowFab { get => GetValue(ShowFabProperty); set => SetValue(ShowFabProperty, value); }
    public Geometry? MenuIcon { get => GetValue(MenuIconProperty); set => SetValue(MenuIconProperty, value); }
    public Geometry? FabIcon { get => GetValue(FabIconProperty); set => SetValue(FabIconProperty, value); }
    public string? FabText { get => GetValue(FabTextProperty); set => SetValue(FabTextProperty, value); }
    public ICommand? MenuCommand { get => GetValue(MenuCommandProperty); set => SetValue(MenuCommandProperty, value); }
    public object? MenuCommandParameter { get => GetValue(MenuCommandParameterProperty); set => SetValue(MenuCommandParameterProperty, value); }
    public ICommand? FabCommand { get => GetValue(FabCommandProperty); set => SetValue(FabCommandProperty, value); }
    public object? FabCommandParameter { get => GetValue(FabCommandParameterProperty); set => SetValue(FabCommandParameterProperty, value); }

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) =>
        new MaterialNavigationRailItem();

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        if (container is MaterialNavigationRailItem navItem)
        {
            navItem.Invoked -= OnItemInvoked;
            navItem.Invoked += OnItemInvoked;
            navItem.RefreshRailState();
        }
    }

    protected override void ClearContainerForItemOverride(Control container)
    {
        if (container is MaterialNavigationRailItem navItem)
            navItem.Invoked -= OnItemInvoked;
        base.ClearContainerForItemOverride(container);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _menuFab = e.NameScope.Find<StackPanel>("PART_MenuFab");
        _itemsPresenter = e.NameScope.Find<ItemsPresenter>("PART_ItemsPresenter");
        _fabButton = e.NameScope.Find<Button>("PART_FabButton");
        _fabIconSlot = e.NameScope.Find<Grid>("PART_FabIconSlot");
        _fabLabel = e.NameScope.Find<TextBlock>("PART_FabLabel");

        UpdateRailState();
    }

    public MaterialNavigationRail()
    {
        // Tunnel so an arrow is consumed before ListBoxItem's own key handling can see it.
        AddHandler(KeyDownEvent, OnRailKeyDown, RoutingStrategies.Tunnel);
        // Tab into the rail lands on the first destination; keep the selected destination in
        // step so the rail never shows a focus ring on a different item than the selection.
        AddHandler(GotFocusEvent, OnRailGotFocus, RoutingStrategies.Bubble);
    }

    private void OnRailGotFocus(object? sender, FocusChangedEventArgs e)
    {
        if (e.NavigationMethod != NavigationMethod.Tab)
            return;

        if (FindItem(e.NewFocusedElement) is { } item)
        {
            var index = IndexFromContainer(item);
            if (index >= 0 && index != SelectedIndex)
                SelectedIndex = index;
        }
    }

    private void OnRailKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.KeyModifiers != KeyModifiers.None)
            return;

        var focusedItem = FindItem(e.Source);
        // The menu and FAB are native buttons: their arrows and Space/Enter must not be stolen.
        if (focusedItem is null && !ReferenceEquals(e.Source, this))
            return;

        var rtl = FlowDirection == FlowDirection.RightToLeft;
        switch (e.Key)
        {
            case Key.Down:
                MoveSelection(focusedItem, 1, e);
                return;
            case Key.Up:
                MoveSelection(focusedItem, -1, e);
                return;
            case Key.Right:
                MoveSelection(focusedItem, rtl ? -1 : 1, e);
                return;
            case Key.Left:
                MoveSelection(focusedItem, rtl ? 1 : -1, e);
                return;
            case Key.Home:
                SelectAndFocus(FindEnabled(0, 1));
                e.Handled = true;
                return;
            case Key.End:
                SelectAndFocus(FindEnabled(ItemCount - 1, -1));
                e.Handled = true;
                return;
            case Key.Enter:
            case Key.Space:
                if (focusedItem is not null && focusedItem.IsEffectivelyEnabled)
                {
                    SelectAndFocus(IndexFromContainer(focusedItem));
                    focusedItem.Invoke();
                    e.Handled = true;
                }
                return;
        }
    }

    private void MoveSelection(MaterialNavigationRailItem? focusedItem, int delta, KeyEventArgs e)
    {
        var count = ItemCount;
        if (count == 0)
            return;

        var current = focusedItem is not null ? IndexFromContainer(focusedItem) : SelectedIndex;
        var start = current < 0 || current >= count
            ? (delta > 0 ? 0 : count - 1)
            : (current + delta + count) % count;
        var next = FindEnabled(start, delta);
        if (next < 0)
            return;

        SelectAndFocus(next);
        e.Handled = true;
    }

    private int FindEnabled(int start, int delta)
    {
        var count = ItemCount;
        if (count == 0)
            return -1;

        var index = ((start % count) + count) % count;
        for (var i = 0; i < count; i++)
        {
            if (IsItemEnabled(index))
                return index;
            index = (index + delta + count) % count;
        }
        return -1;
    }

    private bool IsItemEnabled(int index)
    {
        // An unrealized container will be enabled unless its item disables it after realization.
        return ContainerFromIndex(index) is not Control container || container.IsEffectivelyEnabled;
    }

    private void SelectAndFocus(int index)
    {
        if (index < 0 || index >= ItemCount)
            return;

        SelectedIndex = index;
        // Directional focus marks the state as keyboard focus, which reveals the focus ring.
        (ContainerFromIndex(index) as Control)?.Focus(NavigationMethod.Directional);
    }

    private MaterialNavigationRailItem? FindItem(object? source)
    {
        for (var visual = source as Visual; visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is MaterialNavigationRailItem item && ItemsControlFromItemContainer(item) == this)
                return item;
            if (ReferenceEquals(visual, this))
                return null;
        }
        return null;
    }

    private void OnItemInvoked(object? sender, EventArgs e)
    {
        if (sender is not MaterialNavigationRailItem item)
            return;

        var index = IndexFromContainer(item);
        if (index >= 0 && SelectedIndex != index)
            SelectedIndex = index;
        ItemInvoked?.Invoke(this, new MaterialNavigationRailItemInvokedEventArgs(item, index));
    }

    private void UpdateRailState()
    {
        PseudoClasses.Set(":expanded", IsExpanded);
        PseudoClasses.Set(":floating", IsFloating);
        PseudoClasses.Set(":middle", RailAlignment == MaterialNavigationRailAlignment.Middle);
        UpdateLayoutGeometry();
        UpdateFabContent();
        UpdateItemOrientations();
    }

    private void UpdateItemOrientations()
    {
        foreach (var container in GetRealizedContainers())
            if (container is MaterialNavigationRailItem item)
                item.RefreshRailState();
    }

    private void UpdateFabContent()
    {
        // The 24 unit slot collapses with the glyph so a text-only extended FAB keeps the
        // source's 104 unit width instead of reserving an empty icon column.
        if (_fabIconSlot is not null)
            _fabIconSlot.IsVisible = FabIcon is not null;
        if (_fabLabel is not null)
            _fabLabel.IsVisible = IsExpanded && !string.IsNullOrEmpty(FabText);
        if (_fabButton is not null)
        {
            var fallback = _fabButton.TryFindResource("MaterialNavigationRailFabText", out var value) && value is string text
                ? text
                : "Create";
            AutomationProperties.SetName(_fabButton, string.IsNullOrEmpty(FabText) ? fallback : FabText);
        }
    }

    /// <summary>
    /// The source puts the menu/FAB group 44 units from the top. A top-aligned item group
    /// follows it after the 40 unit group gap; a middle-aligned group is centered in the
    /// full rail height (the source positions it absolutely), independent of the group.
    /// </summary>
    private void UpdateLayoutGeometry()
    {
        if (_menuFab is null || _itemsPresenter is null)
            return;

        var expanded = IsExpanded;
        var inset = expanded ? 20d : 0d;

        _menuFab.HorizontalAlignment = expanded ? HorizontalAlignment.Left : HorizontalAlignment.Center;
        _menuFab.Margin = new Thickness(inset, 44, inset, 0);

        _itemsPresenter.HorizontalAlignment = expanded ? HorizontalAlignment.Left : HorizontalAlignment.Stretch;
        if (RailAlignment == MaterialNavigationRailAlignment.Middle)
        {
            _itemsPresenter.VerticalAlignment = VerticalAlignment.Center;
            _itemsPresenter.Margin = new Thickness(inset, 0, inset, 0);
            return;
        }

        _itemsPresenter.VerticalAlignment = VerticalAlignment.Top;
        // The group is two 56 buttons with the 4 gap, exactly as in the source; deriving the
        // offset from the flags avoids depending on a layout pass that has not run yet.
        var shown = (ShowMenuButton ? 1 : 0) + (ShowFab ? 1 : 0);
        var menuHeight = shown * 56 + (shown == 2 ? 4 : 0);
        // With no group at all the 40 group gap has nothing to separate, so only the top inset applies.
        var top = shown == 0 ? 44 : 44 + menuHeight + 40;
        _itemsPresenter.Margin = new Thickness(inset, top, inset, 0);
    }
}

/// <summary>The expanded (220 unit) navigation rail. Same API and behavior as <see cref="MaterialNavigationRail"/>.</summary>
public sealed class MaterialNavigationRailExpanded : MaterialNavigationRail
{
    public MaterialNavigationRailExpanded() => IsExpanded = true;

    /// <summary>Both rail types share the compact control theme; the expanded class only changes the default state.</summary>
    protected override Type StyleKeyOverride => typeof(MaterialNavigationRail);
}

/// <summary>Describes the navigation item activated by pointer, Enter, or Space.</summary>
public sealed class MaterialNavigationRailItemInvokedEventArgs : EventArgs
{
    public MaterialNavigationRailItemInvokedEventArgs(MaterialNavigationRailItem item, int index)
    {
        Item = item;
        Index = index;
    }

    public MaterialNavigationRailItem Item { get; }
    public int Index { get; }
}
