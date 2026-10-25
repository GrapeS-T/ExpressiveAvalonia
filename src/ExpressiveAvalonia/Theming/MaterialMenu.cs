using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace ExpressiveAvalonia.Theming;

/// <summary>The color treatment of an expressive Material menu surface and its items.</summary>
/// <remarks>
/// <see cref="Standard"/> paints the surface with SurfaceContainerLow and uses OnSurface /
/// OnSurfaceVariant content. <see cref="Vibrant"/> paints it with TertiaryContainer and switches
/// selected items to a Tertiary container with OnTertiary content.
/// </remarks>
public enum MaterialMenuVariant { Standard, Vibrant }

/// <summary>The vertical density of a baseline Material menu item.</summary>
/// <remarks>The source design stores the three values as 0, -2 and -4 DIPs from the 56 DIP row.</remarks>
public enum MaterialMenuDensity { Default, Dense2, Dense4 }

/// <summary>The surface anatomy used by a Material menu.</summary>
/// <remarks>
/// <see cref="Baseline"/> is the classic 4 DIP radius menu with 56 DIP rows (the density axis
/// applies). <see cref="Expressive"/> is the expressive 16 DIP radius menu with grouped,
/// 48 DIP rows.
/// </remarks>
public enum MaterialMenuSurfaceStyle { Baseline, Expressive }

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

    /// <summary>Optional second line of an expressive or baseline menu item.</summary>
    public static readonly AttachedProperty<string?> SupportingTextProperty =
        AvaloniaProperty.RegisterAttached<MaterialMenu, MenuItem, string?>("SupportingText");

    /// <summary>Optional trailing glyph, rendered before the submenu chevron.</summary>
    public static readonly AttachedProperty<object?> TrailingIconProperty =
        AvaloniaProperty.RegisterAttached<MaterialMenu, MenuItem, object?>("TrailingIcon");

    /// <summary>Draws the Material divider under this item, matching the source "Show divider" slot.</summary>
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

    // Template plumbing: the inner content inset is separate from the item's outer padding so the
    // expressive selection container can be smaller than the 48 DIP row. Not part of the public API.
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly AttachedProperty<Thickness> ContentPaddingProperty =
        AvaloniaProperty.RegisterAttached<MaterialMenu, MenuItem, Thickness>("ContentPadding");

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

    public static MaterialMenuVariant GetVariant(Control control) => control.GetValue(VariantProperty);
    public static void SetVariant(Control control, MaterialMenuVariant value) => control.SetValue(VariantProperty, value);
    public static MaterialMenuSurfaceStyle GetSurfaceStyle(Control control) => control.GetValue(SurfaceStyleProperty);
    public static void SetSurfaceStyle(Control control, MaterialMenuSurfaceStyle value) => control.SetValue(SurfaceStyleProperty, value);
    public static MaterialMenuDensity GetDensity(Control control) => control.GetValue(DensityProperty);
    public static void SetDensity(Control control, MaterialMenuDensity value) => control.SetValue(DensityProperty, value);
    public static bool GetShowScrollBar(Control control) => control.GetValue(ShowScrollBarProperty);
    public static void SetShowScrollBar(Control control, bool value) => control.SetValue(ShowScrollBarProperty, value);

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

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static Thickness GetContentPadding(MenuItem item) => item.GetValue(ContentPaddingProperty);
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void SetContentPadding(MenuItem item, Thickness value) => item.SetValue(ContentPaddingProperty, value);
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
}
