using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Controls;

/// <summary>The position of an item inside a <see cref="MaterialButtonGroup"/>.</summary>
/// <remarks>Corner radii of connected and segmented groups depend on the position, so the group
/// keeps this property in sync with the realized containers.</remarks>
public enum MaterialButtonGroupItemPosition { Only, First, Middle, Last }

/// <summary>One selectable button inside a <see cref="MaterialButtonGroup"/>.</summary>
/// <remarks>
/// Derives from <see cref="ToggleButton"/>, so it keeps native pointer, Space/Enter activation,
/// focus visuals, commands, access keys and the Toggle automation pattern. The owning group
/// decides whether checking is allowed (no selection, one selection, or multiple selections)
/// and normalizes the selection; the item only reports its state.
/// </remarks>
public class MaterialButtonGroupItem : ToggleButton
{
    /// <summary>Optional leading icon; accepts an icon control, image or any other content.</summary>
    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<MaterialButtonGroupItem, object?>(nameof(Icon));

    /// <summary>The group anatomy the item is currently rendered in.</summary>
    public static readonly StyledProperty<MaterialButtonGroupKind> KindProperty =
        AvaloniaProperty.Register<MaterialButtonGroupItem, MaterialButtonGroupKind>(nameof(Kind));

    /// <summary>The Material Expressive size shared by every item of the group.</summary>
    public static readonly StyledProperty<MaterialControlSize> ControlSizeProperty =
        AvaloniaProperty.Register<MaterialButtonGroupItem, MaterialControlSize>(
            nameof(ControlSize), MaterialControlSize.Small);

    /// <summary>The resting silhouette of the item.</summary>
    public static readonly StyledProperty<MaterialButtonShape> ButtonShapeProperty =
        AvaloniaProperty.Register<MaterialButtonGroupItem, MaterialButtonShape>(nameof(ButtonShape));

    /// <summary>The color treatment of a standard group item.</summary>
    public static readonly StyledProperty<MaterialButtonVariant> VariantProperty =
        AvaloniaProperty.Register<MaterialButtonGroupItem, MaterialButtonVariant>(
            nameof(Variant), MaterialButtonVariant.Filled);

    /// <summary>Whether the selected state replaces the leading icon with a check mark.</summary>
    public static readonly StyledProperty<bool> ReplaceIconWhenSelectedProperty =
        AvaloniaProperty.Register<MaterialButtonGroupItem, bool>(nameof(ReplaceIconWhenSelected));

    /// <summary>Whether a selected item shows a check mark before its content.</summary>
    public static readonly StyledProperty<bool> ShowSelectedIconProperty =
        AvaloniaProperty.Register<MaterialButtonGroupItem, bool>(nameof(ShowSelectedIcon));

    /// <summary>Whether the check mark is currently painted; derived from selection.</summary>
    public static readonly StyledProperty<bool> ShowCheckIconProperty =
        AvaloniaProperty.Register<MaterialButtonGroupItem, bool>(nameof(ShowCheckIcon));

    /// <summary>Whether the leading icon is currently painted; derived from selection and icon.</summary>
    public static readonly StyledProperty<bool> ShowIconContentProperty =
        AvaloniaProperty.Register<MaterialButtonGroupItem, bool>(nameof(ShowIconContent));

    /// <summary>Whether the item has an icon and no content, which switches to icon-button metrics.</summary>
    public static readonly StyledProperty<bool> IsIconOnlyProperty =
        AvaloniaProperty.Register<MaterialButtonGroupItem, bool>(nameof(IsIconOnly));

    /// <summary>The edge length of the leading icon and check mark.</summary>
    public static readonly StyledProperty<double> IconSizeProperty =
        AvaloniaProperty.Register<MaterialButtonGroupItem, double>(nameof(IconSize), 24d);

    /// <summary>The gap between the leading icon, the check mark and the content.</summary>
    public static readonly StyledProperty<double> ContentSpacingProperty =
        AvaloniaProperty.Register<MaterialButtonGroupItem, double>(nameof(ContentSpacing), 8d);

    /// <summary>The position inside the group, which drives the corner radii.</summary>
    public static readonly StyledProperty<MaterialButtonGroupItemPosition> PositionProperty =
        AvaloniaProperty.Register<MaterialButtonGroupItem, MaterialButtonGroupItemPosition>(nameof(Position));

    /// <summary>
    /// The inset of the painted container inside the item's interactive target, which keeps the
    /// 48-DIP target of the smaller sizes while the visible container stays 32 or 40 DIP high.
    /// </summary>
    public static readonly StyledProperty<Thickness> ContainerInsetProperty =
        AvaloniaProperty.Register<MaterialButtonGroupItem, Thickness>(nameof(ContainerInset));

    /// <summary>The group orientation, which rotates the corner radii for vertical groups.</summary>
    public static readonly StyledProperty<Orientation> GroupOrientationProperty =
        AvaloniaProperty.Register<MaterialButtonGroupItem, Orientation>(nameof(GroupOrientation));

    static MaterialButtonGroupItem()
    {
        IconProperty.Changed.AddClassHandler<MaterialButtonGroupItem>(static (item, _) => item.UpdateContentSlots());
        ContentProperty.Changed.AddClassHandler<MaterialButtonGroupItem>(static (item, _) => item.UpdateContentSlots());
        IsCheckedProperty.Changed.AddClassHandler<MaterialButtonGroupItem>(static (item, _) => item.UpdateContentSlots());
        ShowSelectedIconProperty.Changed.AddClassHandler<MaterialButtonGroupItem>(static (item, _) => item.UpdateContentSlots());
        ReplaceIconWhenSelectedProperty.Changed.AddClassHandler<MaterialButtonGroupItem>(static (item, _) => item.UpdateContentSlots());
    }

    public MaterialButtonGroupItem() => UpdateContentSlots();

    /// <summary>The owning group; set by the group when the container is prepared.</summary>
    public MaterialButtonGroup? Group { get; internal set; }

    public object? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public MaterialButtonGroupKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public MaterialControlSize ControlSize { get => GetValue(ControlSizeProperty); set => SetValue(ControlSizeProperty, value); }
    public MaterialButtonShape ButtonShape { get => GetValue(ButtonShapeProperty); set => SetValue(ButtonShapeProperty, value); }
    public MaterialButtonVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
    public bool ReplaceIconWhenSelected { get => GetValue(ReplaceIconWhenSelectedProperty); set => SetValue(ReplaceIconWhenSelectedProperty, value); }
    public bool ShowSelectedIcon { get => GetValue(ShowSelectedIconProperty); set => SetValue(ShowSelectedIconProperty, value); }
    public bool ShowCheckIcon { get => GetValue(ShowCheckIconProperty); private set => SetValue(ShowCheckIconProperty, value); }
    public bool ShowIconContent { get => GetValue(ShowIconContentProperty); private set => SetValue(ShowIconContentProperty, value); }
    public bool IsIconOnly { get => GetValue(IsIconOnlyProperty); private set => SetValue(IsIconOnlyProperty, value); }
    public double IconSize { get => GetValue(IconSizeProperty); set => SetValue(IconSizeProperty, value); }
    public double ContentSpacing { get => GetValue(ContentSpacingProperty); set => SetValue(ContentSpacingProperty, value); }
    public MaterialButtonGroupItemPosition Position { get => GetValue(PositionProperty); set => SetValue(PositionProperty, value); }
    public Thickness ContainerInset { get => GetValue(ContainerInsetProperty); set => SetValue(ContainerInsetProperty, value); }
    public Orientation GroupOrientation { get => GetValue(GroupOrientationProperty); set => SetValue(GroupOrientationProperty, value); }

    private void UpdateContentSlots()
    {
        var selected = IsChecked == true;
        var check = selected && ShowSelectedIcon;
        SetCurrentValue(ShowCheckIconProperty, check);
        SetCurrentValue(ShowIconContentProperty, Icon is not null && !(check && ReplaceIconWhenSelected));
        SetCurrentValue(IsIconOnlyProperty, Icon is not null && Content is null);
        if (Content is string text && !string.IsNullOrWhiteSpace(text) &&
            string.IsNullOrEmpty(AutomationProperties.GetName(this)))
            AutomationProperties.SetName(this, text);
    }
}
