using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Tokens;

namespace ExpressiveAvalonia.Theming;

/// <summary>The color treatment of an expressive Material menu surface and its items.</summary>
/// <remarks>
/// <see cref="Standard"/> paints the surface with SurfaceContainerLow and uses OnSurface /
/// OnSurfaceVariant content; a selected row uses the TertiaryContainer container with
/// OnTertiaryContainer content (StandardMenuTokens.ItemSelectedContainerColor).
/// <see cref="Vibrant"/> paints the surface with TertiaryContainer and OnTertiaryContainer content;
/// a selected row becomes Tertiary with OnTertiary content (VibrantMenuTokens).
/// </remarks>
public enum MaterialMenuVariant { Standard, Vibrant }

/// <summary>
/// A library extension: the row height axis of the baseline menu.
/// </summary>
/// <remarks>
/// The pinned MenuTokens/Menu.kt baseline has one row height (<c>MenuListItemContainerHeight</c>,
/// 48 DIP) and no density axis. This enum preserves the catalog's pre-port density control and is
/// therefore an ExpressiveAvalonia extension, not an upstream token: <see cref="Default"/> keeps the
/// previous 56 DIP row, <see cref="Dense2"/> subtracts 8 and <see cref="Dense4"/> subtracts 16.
/// Expressive menus are fixed at the source 44 DIP row and ignore this axis.
/// </remarks>
public enum MaterialMenuDensity { Default, Dense2, Dense4 }

/// <summary>The surface anatomy used by a Material menu.</summary>
/// <remarks>
/// <see cref="Baseline"/> is the pinned baseline menu: MenuTokens.ContainerColor
/// (SurfaceContainer), CornerExtraSmall (4 DIP) container, 8 DIP vertical padding, 112/280 DIP item
/// width bounds and the labelLarge item label. <see cref="Expressive"/> is the segmented menu:
/// CornerLarge (16 DIP) container, 44 DIP items with the CornerExtraSmall / CornerMedium shape
/// states, 4/2 DIP item insets and 12 DIP content padding.
/// </remarks>
public enum MaterialMenuSurfaceStyle { Baseline, Expressive }

/// <summary>
/// The position of a menu item in its group, which selects the source item shape.
/// </summary>
/// <remarks>
/// <c>MenuDefaults.itemShape(index, count)</c> resolves the same axis: a leading item has 12 DIP
/// outer corners and 4 DIP inner corners, a middle item is 4 DIP everywhere, a trailing item is the
/// mirror of the leading one and a standalone item is 4 DIP. A selected item always uses
/// <c>SegmentedMenuTokens.ItemSelectedShape</c> (CornerMedium, 12 DIP).
/// </remarks>
public enum MaterialMenuItemPosition { Standalone, Leading, Middle, Trailing }

/// <summary>
/// Attached properties that opt standard Avalonia <see cref="MenuItem"/>, <see cref="Menu"/>,
/// <see cref="ContextMenu"/>, <see cref="MenuFlyoutPresenter"/>, and the menu controls in this
/// library into the Material menu anatomy.
/// </summary>
/// <remarks>
/// <see cref="VariantProperty"/>, <see cref="SurfaceStyleProperty"/>,
/// <see cref="DensityProperty"/> and <see cref="ShowScrollBarProperty"/> inherit down the logical
/// tree, so setting them once on a menu (or on a <c>MaterialMenuGroup</c>) configures every item,
/// including nested submenu items. The remaining properties describe a single item.
/// </remarks>
public sealed class MaterialMenu : AvaloniaObject
{
    private MaterialMenu() { }

    /// <summary>The Standard/Vibrant expressive menu treatment. Inherited.</summary>
    public static readonly AttachedProperty<MaterialMenuVariant> VariantProperty =
        AvaloniaProperty.RegisterAttached<MaterialMenu, Control, MaterialMenuVariant>(
            "Variant", MaterialMenuVariant.Standard, inherits: true);

    /// <summary>The Baseline/Expressive surface anatomy. Inherited.</summary>
    public static readonly AttachedProperty<MaterialMenuSurfaceStyle> SurfaceStyleProperty =
        AvaloniaProperty.RegisterAttached<MaterialMenu, Control, MaterialMenuSurfaceStyle>(
            "SurfaceStyle", MaterialMenuSurfaceStyle.Baseline, inherits: true);

    /// <summary>The baseline row density (Default = 56, Dense2 = 48, Dense4 = 40 DIPs). Inherited.</summary>
    /// <remarks>Library extension; the expressive menu is fixed at 44 DIPs.</remarks>
    public static readonly AttachedProperty<MaterialMenuDensity> DensityProperty =
        AvaloniaProperty.RegisterAttached<MaterialMenu, Control, MaterialMenuDensity>(
            "Density", MaterialMenuDensity.Default, inherits: true);

    /// <summary>
    /// Whether menu scrollbars are shown when the content overflows. The source design defaults to
    /// false, which keeps the popup scrollable through the wheel and keyboard without a visible bar.
    /// Inherited.
    /// </summary>
    public static readonly AttachedProperty<bool> ShowScrollBarProperty =
        AvaloniaProperty.RegisterAttached<MaterialMenu, Control, bool>(
            "ShowScrollBar", false, inherits: true);

    /// <summary>
    /// Places an item in the leading, middle, trailing or standalone position of its group.
    /// Expressive items only; the baseline item has a rectangular container.
    /// </summary>
    public static readonly AttachedProperty<MaterialMenuItemPosition> ItemPositionProperty =
        AvaloniaProperty.RegisterAttached<MaterialMenu, MenuItem, MaterialMenuItemPosition>(
            "ItemPosition", MaterialMenuItemPosition.Standalone);

    /// <summary>Optional second line of an expressive or baseline menu item.</summary>
    public static readonly AttachedProperty<string?> SupportingTextProperty =
        AvaloniaProperty.RegisterAttached<MaterialMenu, MenuItem, string?>("SupportingText");

    /// <summary>Optional trailing glyph, rendered before the submenu chevron.</summary>
    public static readonly AttachedProperty<object?> TrailingIconProperty =
        AvaloniaProperty.RegisterAttached<MaterialMenu, MenuItem, object?>("TrailingIcon");

    /// <summary>
    /// Draws the source menu divider under this item. The source <c>HorizontalDividerPadding</c> is
    /// 12 DIP horizontal and 2 DIP vertical.
    /// </summary>
    public static readonly AttachedProperty<bool> ShowDividerProperty =
        AvaloniaProperty.RegisterAttached<MaterialMenu, MenuItem, bool>("ShowDivider");

    /// <summary>
    /// Marks an item as the persistently active destination. The source "Active" state paints the
    /// 8% state layer without changing the item's checked state or selection.
    /// </summary>
    public static readonly AttachedProperty<bool> IsActiveProperty =
        AvaloniaProperty.RegisterAttached<MaterialMenu, MenuItem, bool>("IsActive");

    /// <summary>
    /// Renders a <see cref="MenuItem"/> as a non-interactive section label inside a real menu.
    /// Use this instead of a <c>MaterialMenuGroup</c> when the label and its items must stay part of
    /// the menu's native keyboard navigation.
    /// </summary>
    public static readonly AttachedProperty<bool> IsSectionLabelProperty =
        AvaloniaProperty.RegisterAttached<MaterialMenu, MenuItem, bool>("IsSectionLabel");

    /// <summary>Optional leading glyph of a <c>MaterialMenuGroup</c> header or <c>MaterialMenuLabel</c>.</summary>
    public static readonly AttachedProperty<object?> GroupIconProperty =
        AvaloniaProperty.RegisterAttached<MaterialMenu, Control, object?>("GroupIcon");

    /// <summary>
    /// Opts a <see cref="Popup"/> into the source menu entrance: the 0.8 → 1 scale spring
    /// (MotionSchemeKeyTokens.FastSpatial) and the 0 → 1 fade (FastEffects) from the pivot that
    /// <c>calculateTransformOrigin</c> derives from the anchor and menu bounds.
    /// </summary>
    /// <remarks>
    /// Entrance only. The exit transition is driven by <see cref="OpenStateProperty"/>, because
    /// Avalonia 12.1.4 keeps <c>Popup.Closing</c> internal and a template popup therefore cannot
    /// cancel its own close. Flyout-based menus use <c>MaterialMenuFlyout</c>, which owns
    /// <c>HideCore</c> and defers the real close until the exit springs settle.
    /// </remarks>
    public static readonly AttachedProperty<bool> AnimatePopupProperty =
        AvaloniaProperty.RegisterAttached<MaterialMenu, Popup, bool>("AnimatePopup");

    /// <summary>
    /// Mirrors a native menu item's submenu state into a template <see cref="Popup"/> so the popup
    /// can play its exit transition before <see cref="Popup.IsOpen"/> becomes false. The native menu
    /// remains the only owner of the open/close decision; the popup is never hidden while the menu
    /// considers it open.
    /// </summary>
    public static readonly AttachedProperty<bool> OpenStateProperty =
        AvaloniaProperty.RegisterAttached<MaterialMenu, Popup, bool>("OpenState");

    /// <summary>
    /// Opts a <see cref="ContextMenu"/> into the source close transition. Avalonia's
    /// <see cref="ContextMenu.Closing"/> event is public and cancelable, so the close is cancelled
    /// once, the menu is animated to 0.8 scale / zero opacity with hit testing disabled, and then
    /// <see cref="ContextMenu.Close"/> is called for real.
    /// </summary>
    public static readonly AttachedProperty<bool> AnimateContextMenuProperty =
        AvaloniaProperty.RegisterAttached<MaterialMenu, ContextMenu, bool>("AnimateContextMenu");

    // Template plumbing: the inner content inset is separate from the item's outer padding so the
    // expressive selection container can be smaller than the 44 DIP row. Not part of the public API.
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly AttachedProperty<Thickness> ContentPaddingProperty =
        AvaloniaProperty.RegisterAttached<MaterialMenu, MenuItem, Thickness>("ContentPadding");

    // Outer inset of the expressive selection surface. DropdownMenuSelectableItemPadding is 4 DIP
    // horizontal plus 2 DIP vertical when the item has supporting text.
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly AttachedProperty<Thickness> ItemPaddingProperty =
        AvaloniaProperty.RegisterAttached<MaterialMenu, MenuItem, Thickness>("ItemPadding");

    // Template plumbing for the submenu popup. Values are set by the item themes rather than by
    // styles that target template children inside a Popup, whose styling scope is not guaranteed.
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly AttachedProperty<IBrush?> PopupBackgroundProperty =
        AvaloniaProperty.RegisterAttached<MaterialMenu, MenuItem, IBrush?>("PopupBackground");

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly AttachedProperty<CornerRadius> PopupCornerRadiusProperty =
        AvaloniaProperty.RegisterAttached<MaterialMenu, MenuItem, CornerRadius>("PopupCornerRadius", new CornerRadius(4));

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly AttachedProperty<Thickness> PopupPaddingProperty =
        AvaloniaProperty.RegisterAttached<MaterialMenu, MenuItem, Thickness>("PopupPadding", new Thickness(8));

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly AttachedProperty<ScrollBarVisibility> PopupScrollBarVisibilityProperty =
        AvaloniaProperty.RegisterAttached<MaterialMenu, MenuItem, ScrollBarVisibility>("PopupScrollBarVisibility", ScrollBarVisibility.Hidden);

    // The row Surface fill. Menu.kt:2012-2020 owns the expressive container color with
    // animateColorAsState(targetValue = containerColor, FastEffects), so the animated brush is
    // pushed to the template part while Background stays the style target. Baseline rows mirror
    // Background directly: their checked container is a library extension, not a source spring.
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly AttachedProperty<IBrush?> AnimatedBackgroundProperty =
        AvaloniaProperty.RegisterAttached<MaterialMenu, MenuItem, IBrush?>("AnimatedBackground");

    static MaterialMenu()
    {
        SupportingTextProperty.Changed.AddClassHandler<MenuItem>((item, _) => UpdateItemPadding(item));
        SurfaceStyleProperty.Changed.AddClassHandler<MenuItem>((item, _) =>
        {
            UpdateItemPadding(item);
            RowContainerColor.Get(item).Set(item.Background);
        });
        IsSectionLabelProperty.Changed.AddClassHandler<MenuItem>((item, _) => UpdateItemPadding(item));

        // The style still owns Background (it is what :checked changes); the row spring follows
        // that target. Template application seeds the first composition so a menu that opens with
        // a checked row does not animate from an unset brush.
        TemplatedControl.BackgroundProperty.Changed.AddClassHandler<MenuItem>(
            static (item, _) => RowContainerColor.Get(item).Set(item.Background));
        TemplatedControl.TemplateAppliedEvent.AddClassHandler<MenuItem>(
            static (item, _) => RowContainerColor.Get(item).Set(item.Background));
        AnimatePopupProperty.Changed.AddClassHandler<Popup>((popup, args) =>
        {
            if (args.NewValue is true)
                PopupEntrance.Attach(popup);
        });
        OpenStateProperty.Changed.AddClassHandler<Popup>((popup, args) =>
            PopupEntrance.Attach(popup).SetRequested(args.NewValue is true));
        AnimateContextMenuProperty.Changed.AddClassHandler<ContextMenu>((menu, args) =>
        {
            if (args.NewValue is true)
                ContextMenuExit.Attach(menu);
        });

        // Native menu navigation only traverses direct MenuItem containers. Limit the
        // flattened adapter to menu surfaces containing groups, including submenu hosts.
        // Left/Right, Escape and accelerators remain native.
        InputElement.KeyDownEvent.AddClassHandler<ItemsControl>((menu, args) => HandleGroupedMenuKey(menu, args),
            RoutingStrategies.Tunnel);
    }

    private static void HandleGroupedMenuKey(ItemsControl menu, KeyEventArgs args)
    {
        if (menu is not (MenuBase or MenuItem or MenuFlyoutPresenter))
            return;
        if (args.Handled || args.Key is not (Key.Up or Key.Down or Key.Home or Key.End))
            return;
        if (!menu.Items.OfType<Control>().Any(x => x is MaterialMenuGroup))
            return;
        if (args.Source is not MenuItem current)
            return;

        var items = FlattenMenu(menu);
        var index = items.IndexOf(current);
        if (index < 0)
            return;

        var count = items.Count;
        int direction = args.Key == Key.Down ? 1 : args.Key == Key.Up ? -1 : 0;
        if (args.Key == Key.Home || args.Key == Key.End)
        {
            for (var step = 0; step < count; step++)
            {
                var candidate = args.Key == Key.Home ? step : count - 1 - step;
                if (items[candidate].IsEffectivelyEnabled)
                {
                    items[candidate].Focus(NavigationMethod.Directional);
                    args.Handled = true;
                    return;
                }
            }
            return;
        }

        for (var step = 1; step <= count; step++)
        {
            var candidate = ((index + direction * step) % count + count) % count;
            if (!items[candidate].IsEffectivelyEnabled)
                continue;
            items[candidate].Focus(NavigationMethod.Directional);
            args.Handled = true;
            return;
        }
    }

    /// <summary>Flattens direct menu items and the items of every nested group, in visual order.</summary>
    private static List<MenuItem> FlattenMenu(ItemsControl owner)
    {
        var result = new List<MenuItem>();

        void Add(ItemsControl container)
        {
            foreach (var child in container.Items.OfType<Control>())
            {
                switch (child)
                {
                    case MenuItem item when item.IsEffectivelyVisible:
                        result.Add(item);
                        break;
                    case MaterialMenuGroup group:
                        Add(group);
                        break;
                    case ItemsControl itemsControl:
                        Add(itemsControl);
                        break;
                }
            }
        }

        Add(owner);
        return result;
    }

    /// <summary>
    /// Recomputes <see cref="ItemPaddingProperty"/>: the expressive selection surface is inset
    /// 4 DIP horizontally and 2 DIP vertically when the row has supporting text. Baseline rows and
    /// section labels have no outer inset.
    /// </summary>
    private static void UpdateItemPadding(MenuItem item)
    {
        if (GetSurfaceStyle(item) != MaterialMenuSurfaceStyle.Expressive)
        {
            item.SetValue(ItemPaddingProperty, default);
            return;
        }

        // Section labels use the same 4 DIP horizontal inset as items but have no supporting-text
        // axis; regular rows add 2 DIP above and below only when the second line exists.
        var vertical = GetIsSectionLabel(item) || string.IsNullOrEmpty(GetSupportingText(item)) ? 0 : 2;
        item.SetValue(ItemPaddingProperty, new Thickness(4, vertical));
    }

    /// <summary>
    /// Port of <c>calculateTransformOrigin</c> (Menu.kt): the scale pivot is the edge of the menu
    /// nearest the anchor, or the centre of their intersection when they overlap.
    /// </summary>
    public static RelativePoint CalculatePopupTransformOrigin(Rect anchorBounds, Rect menuBounds)
    {
        double pivotX;
        if (menuBounds.Left >= anchorBounds.Right)
            pivotX = 0d;
        else if (menuBounds.Right <= anchorBounds.Left)
            pivotX = 1d;
        else if (menuBounds.Width <= 0)
            pivotX = 0d;
        else
        {
            var intersectionCenter =
                (Math.Max(anchorBounds.Left, menuBounds.Left) + Math.Min(anchorBounds.Right, menuBounds.Right)) / 2;
            pivotX = (intersectionCenter - menuBounds.Left) / menuBounds.Width;
        }

        double pivotY;
        if (menuBounds.Top >= anchorBounds.Bottom)
            pivotY = 0d;
        else if (menuBounds.Bottom <= anchorBounds.Top)
            pivotY = 1d;
        else if (menuBounds.Height <= 0)
            pivotY = 0d;
        else
        {
            var intersectionCenter =
                (Math.Max(anchorBounds.Top, menuBounds.Top) + Math.Min(anchorBounds.Bottom, menuBounds.Bottom)) / 2;
            pivotY = (intersectionCenter - menuBounds.Top) / menuBounds.Height;
        }

        return new RelativePoint(pivotX, pivotY, RelativeUnit.Relative);
    }

    public static MaterialMenuVariant GetVariant(Control control) => control.GetValue(VariantProperty);
    public static void SetVariant(Control control, MaterialMenuVariant value) => control.SetValue(VariantProperty, value);
    public static MaterialMenuSurfaceStyle GetSurfaceStyle(Control control) => control.GetValue(SurfaceStyleProperty);
    public static void SetSurfaceStyle(Control control, MaterialMenuSurfaceStyle value) => control.SetValue(SurfaceStyleProperty, value);
    public static MaterialMenuDensity GetDensity(Control control) => control.GetValue(DensityProperty);
    public static void SetDensity(Control control, MaterialMenuDensity value) => control.SetValue(DensityProperty, value);
    public static bool GetShowScrollBar(Control control) => control.GetValue(ShowScrollBarProperty);
    public static void SetShowScrollBar(Control control, bool value) => control.SetValue(ShowScrollBarProperty, value);

    public static MaterialMenuItemPosition GetItemPosition(MenuItem item) => item.GetValue(ItemPositionProperty);
    public static void SetItemPosition(MenuItem item, MaterialMenuItemPosition value) => item.SetValue(ItemPositionProperty, value);
    public static string? GetSupportingText(MenuItem item) => item.GetValue(SupportingTextProperty);
    public static void SetSupportingText(MenuItem item, string? value) => item.SetValue(SupportingTextProperty, value);
    public static object? GetTrailingIcon(MenuItem item) => item.GetValue(TrailingIconProperty);
    public static void SetTrailingIcon(MenuItem item, object? value) => item.SetValue(TrailingIconProperty, value);
    public static bool GetShowDivider(MenuItem item) => item.GetValue(ShowDividerProperty);
    public static void SetShowDivider(MenuItem item, bool value) => item.SetValue(ShowDividerProperty, value);
    public static bool GetIsActive(MenuItem item) => item.GetValue(IsActiveProperty);
    public static void SetIsActive(MenuItem item, bool value) => item.SetValue(IsActiveProperty, value);
    public static bool GetIsSectionLabel(MenuItem item) => item.GetValue(IsSectionLabelProperty);
    public static void SetIsSectionLabel(MenuItem item, bool value) => item.SetValue(IsSectionLabelProperty, value);

    public static object? GetGroupIcon(Control control) => control.GetValue(GroupIconProperty);
    public static void SetGroupIcon(Control control, object? value) => control.SetValue(GroupIconProperty, value);

    public static bool GetAnimatePopup(Popup popup) => popup.GetValue(AnimatePopupProperty);
    public static void SetAnimatePopup(Popup popup, bool value) => popup.SetValue(AnimatePopupProperty, value);
    public static bool GetOpenState(Popup popup) => popup.GetValue(OpenStateProperty);
    public static void SetOpenState(Popup popup, bool value) => popup.SetValue(OpenStateProperty, value);
    public static bool GetAnimateContextMenu(ContextMenu menu) => menu.GetValue(AnimateContextMenuProperty);
    public static void SetAnimateContextMenu(ContextMenu menu, bool value) => menu.SetValue(AnimateContextMenuProperty, value);

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static Thickness GetContentPadding(MenuItem item) => item.GetValue(ContentPaddingProperty);
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void SetContentPadding(MenuItem item, Thickness value) => item.SetValue(ContentPaddingProperty, value);
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static Thickness GetItemPadding(MenuItem item) => item.GetValue(ItemPaddingProperty);
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void SetItemPadding(MenuItem item, Thickness value) => item.SetValue(ItemPaddingProperty, value);
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static IBrush? GetPopupBackground(MenuItem item) => item.GetValue(PopupBackgroundProperty);
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void SetPopupBackground(MenuItem item, IBrush? value) => item.SetValue(PopupBackgroundProperty, value);
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static CornerRadius GetPopupCornerRadius(MenuItem item) => item.GetValue(PopupCornerRadiusProperty);
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void SetPopupCornerRadius(MenuItem item, CornerRadius value) => item.SetValue(PopupCornerRadiusProperty, value);
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static Thickness GetPopupPadding(MenuItem item) => item.GetValue(PopupPaddingProperty);
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void SetPopupPadding(MenuItem item, Thickness value) => item.SetValue(PopupPaddingProperty, value);
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static ScrollBarVisibility GetPopupScrollBarVisibility(MenuItem item) => item.GetValue(PopupScrollBarVisibilityProperty);
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void SetPopupScrollBarVisibility(MenuItem item, ScrollBarVisibility value) => item.SetValue(PopupScrollBarVisibilityProperty, value);
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static IBrush? GetAnimatedBackground(MenuItem item) => item.GetValue(AnimatedBackgroundProperty);
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void SetAnimatedBackground(MenuItem item, IBrush? value) => item.SetValue(AnimatedBackgroundProperty, value);

    /// <summary>
    /// The expressive row container color animation (Menu.kt:2012-2020:
    /// <c>animateColorAsState(containerColor, MotionSchemeKeyTokens.FastEffects)</c>).
    /// </summary>
    /// <remarks>
    /// The style writes the target to <see cref="TemplatedControl.BackgroundProperty"/>; this
    /// state springs <see cref="AnimatedBackgroundProperty"/> from its current color to that
    /// target. Baseline rows copy the brush straight through, because their checked container is a
    /// library extension and must not fabricate a source spring. The first composition and every
    /// change while the item is off the visual tree snap, matching the source's initial
    /// <c>animateColorAsState</c> value and avoiding an entrance fade behind the popup entrance.
    /// </remarks>
    private sealed class RowContainerColor
    {
        private static readonly ConditionalWeakTable<MenuItem, RowContainerColor> States = new();

        private readonly MenuItem _item;
        private readonly SolidColorBrush _brush = new(Colors.Transparent);
        private MaterialColorAnimation? _animation;
        private Color _current;
        private bool _hasValue;
        private bool _attached;

        private RowContainerColor(MenuItem item)
        {
            _item = item;
            _attached = item.IsAttachedToVisualTree();
            item.AttachedToVisualTree += OnAttachedToVisualTree;
            item.DetachedFromVisualTree += OnDetachedFromVisualTree;
            item.ActualThemeVariantChanged += OnThemeVariantChanged;
            item.ResourcesChanged += OnResourcesChanged;
        }

        internal static RowContainerColor Get(MenuItem item)
        {
            if (States.TryGetValue(item, out var state))
                return state;
            state = new RowContainerColor(item);
            States.Add(item, state);
            return state;
        }

        /// <summary>Moves the painted row fill to the current Background target.</summary>
        internal void Set(IBrush? background)
        {
            if (!TryGetColor(background, out var target))
            {
                // A gradient and a null brush have no color spring; mirror the target so a themed
                // consumer still paints exactly what it supplied.
                _animation?.Dispose();
                _animation = null;
                _hasValue = false;
                _item.SetValue(AnimatedBackgroundProperty, background);
                return;
            }

            var animate = _hasValue
                          && _attached
                          && GetSurfaceStyle(_item) == MaterialMenuSurfaceStyle.Expressive
                          && !MaterialMotion.GetIsReduced(_item);
            if (!animate)
            {
                _animation?.Dispose();
                _animation = null;
                _current = target;
                _hasValue = true;
                Paint(target);
                return;
            }

            if (_animation is null)
                _animation = new MaterialColorAnimation(_item, _current, Paint, MaterialSpring.EffectsFast);
            _animation.Retarget(target);
        }

        private void Paint(Color color)
        {
            _current = color;
            _brush.Color = color;
            _item.SetValue(AnimatedBackgroundProperty, _brush);
        }

        private static bool TryGetColor(IBrush? background, out Color color)
        {
            if (background is ISolidColorBrush solid)
            {
                color = solid.Color;
                // A brush-level opacity is not animated separately; fold it into the color so the
                // spring still starts and ends on the exact painted value.
                if (background.Opacity < 1)
                {
                    color = Color.FromArgb(
                        (byte)Math.Clamp(Math.Round(color.A * background.Opacity), 0, 255),
                        color.R, color.G, color.B);
                }
                return true;
            }

            color = default;
            return false;
        }

        private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            _attached = true;
            Set(_item.Background);
        }

        private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            _attached = false;
            _animation?.Dispose();
            _animation = null;
            // A reused container starts from the styled target, like a fresh composition, instead
            // of continuing a spring that was interrupted by the popup closing.
            _hasValue = false;
        }

        private void OnThemeVariantChanged(object? sender, EventArgs e) => Set(_item.Background);
        private void OnResourcesChanged(object? sender, ResourcesChangedEventArgs e) => Set(_item.Background);
    }

    /// <summary>Per-popup state: entrance, exit and the requested open state.</summary>
    private sealed class PopupEntrance
    {
        private static readonly ConditionalWeakTable<Popup, PopupEntrance> States = new();

        private readonly Popup _popup;
        private MaterialMotion.SpringAnimation? _scaleAnimation;
        private MaterialMotion.SpringAnimation? _opacityAnimation;
        private MaterialMotion.SpringAnimation? _exitOpacityAnimation;
        private MaterialMotion.SpringAnimation? _exitScaleAnimation;
        private DispatcherTimer? _exitTimer;
        private ScaleTransform? _scale;
        private ScaleTransform? _exitScale;
        private Control? _child;
        private bool _requested;
        private bool _exiting;

        // Menu.kt menu open/close animation targets.
        private const double ClosedScale = 0.8d;

        private PopupEntrance(Popup popup) => _popup = popup;

        public static PopupEntrance Attach(Popup popup)
        {
            if (States.TryGetValue(popup, out var existing)) return existing;
            var state = new PopupEntrance(popup);
            States.Add(popup, state);
            popup.Opened += state.OnOpened;
            popup.Closed += state.OnClosed;
            return state;
        }

        /// <summary>
        /// Mirrors the menu item's open state into the popup so a close can be animated before the
        /// popup is actually hidden. Avalonia 12.1.4 exposes <c>Popup.Closing</c> as an internal
        /// event, so a template popup cannot cancel its own close; owning the <c>IsOpen</c> write is
        /// the public equivalent. The native menu still owns every open/close decision.
        /// </summary>
        public void SetRequested(bool requested)
        {
            if (_requested == requested) return;
            _requested = requested;
            if (requested)
            {
                CancelExit();
                if (!_popup.IsOpen)
                    _popup.IsOpen = true;
            }
            else if (_popup.IsOpen)
            {
                BeginExit();
            }
        }

        private void OnOpened(object? sender, EventArgs args)
        {
            CancelExit();
            _child = _popup.Child;
            if (_child is null || MaterialMotion.GetIsReduced(_child) || !_child.IsAttachedToVisualTree())
                return;

            _child.RenderTransformOrigin = Origin();
            _scale = new ScaleTransform(ClosedScale, ClosedScale);
            _child.RenderTransform = _scale;
            _child.Opacity = 0;
            _child.IsHitTestVisible = true;
            // MotionSchemeKeyTokens.FastSpatial and FastEffects are the source transition specs.
            _scaleAnimation = MaterialMotion.Animate(_child, ClosedScale, 1, value =>
            {
                if (_scale is { } scale)
                {
                    scale.ScaleX = value;
                    scale.ScaleY = value;
                }
            }, MaterialSpring.SpatialFast);
            _opacityAnimation = MaterialMotion.Animate(_child, 0, 1, value => _child.Opacity = value,
                MaterialSpring.EffectsFast);
        }

        private void BeginExit()
        {
            if (_exiting) return;
            var child = _popup.Child;
            if (child is null || MaterialMotion.GetIsReduced(child) || !child.IsAttachedToVisualTree())
            {
                _popup.IsOpen = false;
                return;
            }

            _exiting = true;
            _child = child;
            child.IsHitTestVisible = false;
            _exitScale = _scale;
            _exitScaleAnimation = _scaleAnimation;
            _exitOpacityAnimation = _opacityAnimation;
            _scaleAnimation = null;
            _opacityAnimation = null;
            _exitScaleAnimation?.Retarget(ClosedScale);
            _exitOpacityAnimation?.Retarget(0);

            _exitTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, (_, _) =>
            {
                if (_exitOpacityAnimation is { IsRunning: true } || _exitScaleAnimation is { IsRunning: true })
                    return;
                _exitTimer?.Stop();
                _exitTimer = null;
                if (!_exiting) return;
                _exiting = false;
                CleanupExit();
                _popup.IsOpen = false;
            });
            _exitTimer.Start();
        }

        private void CancelExit()
        {
            if (!_exiting) return;
            _exitTimer?.Stop();
            _exitTimer = null;
            _exiting = false;
            _exitOpacityAnimation?.Retarget(1);
            _exitScaleAnimation?.Retarget(1);
            _opacityAnimation = _exitOpacityAnimation;
            _scaleAnimation = _exitScaleAnimation;
            _scale = _exitScale;
            _exitOpacityAnimation = null;
            _exitScaleAnimation = null;
            _exitScale = null;
            if (_popup.Child is { } child)
                child.IsHitTestVisible = true;
        }

        private RelativePoint Origin()
        {
            if (_child is null) return RelativePoint.TopLeft;
            var root = TopLevel.GetTopLevel(_child);
            if (root is null || _popup.PlacementTarget is not { } target)
                return RelativePoint.TopLeft;

            var childOrigin = _child.TranslatePoint(new Point(0, 0), root);
            var anchorOrigin = target.TranslatePoint(new Point(0, 0), root);
            if (childOrigin is not { } menuPoint || anchorOrigin is not { } anchorPoint)
                return RelativePoint.TopLeft;

            return CalculatePopupTransformOrigin(
                new Rect(anchorPoint, target.Bounds.Size),
                new Rect(menuPoint, _child.Bounds.Size));
        }

        private void OnClosed(object? sender, EventArgs args)
        {
            _exiting = false;
            _exitTimer?.Stop();
            _exitTimer = null;
            CleanupExit();
            DisposeEntrance();
            if (_child is not null)
            {
                _child.Opacity = 1;
                _child.RenderTransform = null;
                _child.IsHitTestVisible = true;
            }
            _child = null;
            _scale = null;
            _requested = false;
        }

        private void DisposeEntrance()
        {
            _scaleAnimation?.Dispose();
            _scaleAnimation = null;
            _opacityAnimation?.Dispose();
            _opacityAnimation = null;
        }

        private void CleanupExit()
        {
            _exitOpacityAnimation?.Dispose();
            _exitOpacityAnimation = null;
            _exitScaleAnimation?.Dispose();
            _exitScaleAnimation = null;
            _exitScale = null;
        }
    }

    /// <summary>
    /// Close transition for a <see cref="ContextMenu"/>. The menu is the popup child, so it is
    /// animated directly; <see cref="ContextMenu.Closing"/> cancels the first close attempt and the
    /// deferred <see cref="ContextMenu.Close"/> performs the real one.
    /// </summary>
    private sealed class ContextMenuExit
    {
        private static readonly ConditionalWeakTable<ContextMenu, ContextMenuExit> States = new();

        private readonly ContextMenu _menu;
        private ScaleTransform? _scale;
        private MaterialMotion.SpringAnimation? _opacityAnimation;
        private MaterialMotion.SpringAnimation? _scaleAnimation;
        private DispatcherTimer? _timer;
        private bool _exiting;
        private bool _allowClose;

        private ContextMenuExit(ContextMenu menu) => _menu = menu;

        public static void Attach(ContextMenu menu)
        {
            if (States.TryGetValue(menu, out _)) return;
            var state = new ContextMenuExit(menu);
            States.Add(menu, state);
            menu.Closing += state.OnClosing;
            menu.Opened += state.OnOpened;
            menu.Closed += state.OnClosed;
        }

        private void OnClosing(object? sender, CancelEventArgs args)
        {
            if (_allowClose) return;
            if (_exiting)
            {
                args.Cancel = true;
                return;
            }
            if (MaterialMotion.GetIsReduced(_menu) || !_menu.IsAttachedToVisualTree())
                return;

            args.Cancel = true;
            BeginExit();
        }

        private void BeginExit()
        {
            _exiting = true;
            _menu.IsHitTestVisible = false;
            if (_scale is null)
            {
                _scale = new ScaleTransform(1, 1);
                _menu.RenderTransformOrigin = RelativePoint.Center;
                _menu.RenderTransform = _scale;
                _scaleAnimation = MaterialMotion.Animate(_menu, 1, 0.8, value =>
                {
                    if (_scale is { } scale)
                        scale.ScaleX = scale.ScaleY = value;
                }, MaterialSpring.SpatialFast);
                _opacityAnimation = MaterialMotion.Animate(_menu, _menu.Opacity, 0,
                    value => _menu.Opacity = value, MaterialSpring.EffectsFast);
            }
            else
            {
                _scaleAnimation?.Retarget(0.8);
                _opacityAnimation?.Retarget(0);
            }

            _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, (_, _) =>
            {
                if (_opacityAnimation is { IsRunning: true } || _scaleAnimation is { IsRunning: true })
                    return;
                _timer?.Stop();
                _timer = null;
                if (!_exiting) return;
                _exiting = false;
                Cleanup();
                _allowClose = true;
                try
                {
                    _menu.Close();
                }
                finally
                {
                    _allowClose = false;
                }

                // A different Closing subscriber may have cancelled the real close.
                if (_menu.IsOpen)
                    Restore();
            });
            _timer.Start();
        }

        private void OnOpened(object? sender, EventArgs args)
        {
            if (!_exiting) return;
            _timer?.Stop();
            _timer = null;
            _exiting = false;
            _opacityAnimation?.Retarget(1);
            _scaleAnimation?.Retarget(1);
            _menu.IsHitTestVisible = true;
        }

        private void OnClosed(object? sender, EventArgs args)
        {
            _exiting = false;
            _timer?.Stop();
            _timer = null;
            Cleanup();
            Restore();
        }

        private void Cleanup()
        {
            _opacityAnimation?.Dispose();
            _opacityAnimation = null;
            _scaleAnimation?.Dispose();
            _scaleAnimation = null;
            _scale = null;
        }

        private void Restore()
        {
            _menu.Opacity = 1;
            _menu.RenderTransform = null;
            _menu.IsHitTestVisible = true;
        }
    }
}