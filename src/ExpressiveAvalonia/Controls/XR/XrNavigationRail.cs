using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// An XR navigation rail: a 96 unit rounded container with an optional menu button, an optional
/// embedded FAB, and single-selection destinations, optionally in the wide 220–360 unit form.
/// </summary>
/// <remarks>
/// <para>
/// Desktop composition of <c>SpatialNavigationRail</c> and <c>SpatialWideNavigationRail</c>: the
/// pinned source uses a 96 dp minimum width, 20 dp vertical padding, a circular Surface and a
/// 20 dp gap between items, inside a center-start Orbiter. This desktop composition applies those
/// 2D metrics and omits the Orbiter.
/// </para>
/// <para>
/// <see cref="Variant"/> selects the family. <see cref="XrNavigationRailVariant.Standard"/> keeps
/// the 96 wide menu/FAB anatomy. <see cref="XrNavigationRailVariant.Wide"/> keeps the same 96
/// collapsed width and circular Surface, and expands to <see cref="ExpandedWidth"/> (clamped to
/// 220–360) when <see cref="IsExpanded"/> is set; the 20 vertical padding and 20 destination gap
/// are unchanged. <see cref="Header"/> sits above the circular item surface with a 20-unit gap;
/// the wide family does not inject the standard menu/FAB chrome. Expansion is a
/// template geometry change driven by the <c>:wide</c> and <c>:expanded</c> pseudo classes; the
/// items receive matching <c>:rail-expanded</c> geometry.
/// </para>
/// <para>
/// The control is a <see cref="ListBox"/> with <see cref="XrNavigationRailItem"/> containers,
/// so selection, SelectionItemPattern, disabled items, and pointer activation are native.
/// Up/Down, Home/End, and Enter/Space select and focus destinations; the menu and FAB are
/// ordinary buttons and keep their own keyboard behavior. Items may be item controls or
/// strings (used as the label).
/// </para>
/// </remarks>
[PseudoClasses(":no-fab", ":wide", ":expanded", ":has-header")]
[TemplatePart("PART_Surface", typeof(Border))]
[TemplatePart("PART_RootStack", typeof(StackPanel))]
[TemplatePart("PART_Header", typeof(ContentPresenter))]
[TemplatePart("PART_Chrome", typeof(StackPanel))]
[TemplatePart("PART_ItemsPresenter", typeof(ItemsPresenter))]
public class XrNavigationRail : ListBox
{
    /// <summary>Whether the leading menu button is shown.</summary>
    public static readonly StyledProperty<bool> ShowMenuProperty =
        AvaloniaProperty.Register<XrNavigationRail, bool>(nameof(ShowMenu), true);

    public static readonly StyledProperty<Geometry?> MenuIconProperty =
        AvaloniaProperty.Register<XrNavigationRail, Geometry?>(nameof(MenuIcon), XrIcons.Menu);

    /// <summary>Accessible name of the menu button.</summary>
    public static readonly StyledProperty<string?> MenuLabelProperty =
        AvaloniaProperty.Register<XrNavigationRail, string?>(nameof(MenuLabel), "Menu");

    public static readonly StyledProperty<ICommand?> MenuCommandProperty =
        AvaloniaProperty.Register<XrNavigationRail, ICommand?>(nameof(MenuCommand));

    public static readonly StyledProperty<object?> MenuCommandParameterProperty =
        AvaloniaProperty.Register<XrNavigationRail, object?>(nameof(MenuCommandParameter));

    /// <summary>Whether the embedded FAB is shown.</summary>
    public static readonly StyledProperty<bool> ShowFabProperty =
        AvaloniaProperty.Register<XrNavigationRail, bool>(nameof(ShowFab), true);

    public static readonly StyledProperty<Geometry?> FabIconProperty =
        AvaloniaProperty.Register<XrNavigationRail, Geometry?>(nameof(FabIcon), XrIcons.Add);

    /// <summary>Accessible name of the embedded FAB.</summary>
    public static readonly StyledProperty<string?> FabLabelProperty =
        AvaloniaProperty.Register<XrNavigationRail, string?>(nameof(FabLabel), "Create");

    public static readonly StyledProperty<ICommand?> FabCommandProperty =
        AvaloniaProperty.Register<XrNavigationRail, ICommand?>(nameof(FabCommand));

    public static readonly StyledProperty<object?> FabCommandParameterProperty =
        AvaloniaProperty.Register<XrNavigationRail, object?>(nameof(FabCommandParameter));

    /// <summary>Standard (96 wide) or Wide (96 collapsed, 220–360 expanded) rail geometry.</summary>
    public static readonly StyledProperty<XrNavigationRailVariant> VariantProperty =
        AvaloniaProperty.Register<XrNavigationRail, XrNavigationRailVariant>(nameof(Variant));

    /// <summary>
    /// Whether a <see cref="XrNavigationRailVariant.Wide"/> rail shows its expanded geometry.
    /// Standard rails ignore this value; expansion never changes their width or item layout.
    /// </summary>
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<XrNavigationRail, bool>(
            nameof(IsExpanded), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Expanded width of a <see cref="XrNavigationRailVariant.Wide"/> rail. Values outside the
    /// source 220–360 range are clamped.
    /// </summary>
    public static readonly StyledProperty<double> ExpandedWidthProperty =
        AvaloniaProperty.Register<XrNavigationRail, double>(nameof(ExpandedWidth), 300d);

    /// <summary>Caller-supplied content of the wide rail header slot.</summary>
    public static readonly StyledProperty<object?> HeaderProperty =
        AvaloniaProperty.Register<XrNavigationRail, object?>(nameof(Header));

    /// <summary>Data template of the wide rail header slot.</summary>
    public static readonly StyledProperty<IDataTemplate?> HeaderTemplateProperty =
        AvaloniaProperty.Register<XrNavigationRail, IDataTemplate?>(nameof(HeaderTemplate));

    /// <summary>
    /// Width applied to the rail surface: <see cref="double.NaN"/> for a standard rail (intrinsic
    /// sizing is unchanged), 96 for a collapsed wide rail, and the clamped
    /// <see cref="ExpandedWidth"/> for an expanded wide rail.
    /// </summary>
    public static readonly DirectProperty<XrNavigationRail, double> RailWidthProperty =
        AvaloniaProperty.RegisterDirect<XrNavigationRail, double>(
            nameof(RailWidth), rail => rail.RailWidth);

    private double _railWidth = double.NaN;

    static XrNavigationRail()
    {
        ShowFabProperty.Changed.AddClassHandler<XrNavigationRail>((rail, _) => rail.UpdateChromeState());
        VariantProperty.Changed.AddClassHandler<XrNavigationRail>((rail, _) => rail.UpdateRailState());
        IsExpandedProperty.Changed.AddClassHandler<XrNavigationRail>((rail, _) => rail.UpdateRailState());
        ExpandedWidthProperty.Changed.AddClassHandler<XrNavigationRail>((rail, _) => rail.UpdateRailState());
        HeaderProperty.Changed.AddClassHandler<XrNavigationRail>((rail, _) => rail.UpdateHeaderState());
    }

    public XrNavigationRail()
    {
        SelectionMode = SelectionMode.Single;
        ItemsPanel = new FuncTemplate<Panel?>(() => new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 20
        });
        UpdateRailState();
    }

    public bool ShowMenu { get => GetValue(ShowMenuProperty); set => SetValue(ShowMenuProperty, value); }
    public Geometry? MenuIcon { get => GetValue(MenuIconProperty); set => SetValue(MenuIconProperty, value); }
    public string? MenuLabel { get => GetValue(MenuLabelProperty); set => SetValue(MenuLabelProperty, value); }
    public ICommand? MenuCommand { get => GetValue(MenuCommandProperty); set => SetValue(MenuCommandProperty, value); }
    public object? MenuCommandParameter { get => GetValue(MenuCommandParameterProperty); set => SetValue(MenuCommandParameterProperty, value); }
    public bool ShowFab { get => GetValue(ShowFabProperty); set => SetValue(ShowFabProperty, value); }
    public Geometry? FabIcon { get => GetValue(FabIconProperty); set => SetValue(FabIconProperty, value); }
    public string? FabLabel { get => GetValue(FabLabelProperty); set => SetValue(FabLabelProperty, value); }
    public ICommand? FabCommand { get => GetValue(FabCommandProperty); set => SetValue(FabCommandProperty, value); }
    public object? FabCommandParameter { get => GetValue(FabCommandParameterProperty); set => SetValue(FabCommandParameterProperty, value); }
    public XrNavigationRailVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }
    public double ExpandedWidth { get => GetValue(ExpandedWidthProperty); set => SetValue(ExpandedWidthProperty, value); }
    public object? Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }
    public IDataTemplate? HeaderTemplate { get => GetValue(HeaderTemplateProperty); set => SetValue(HeaderTemplateProperty, value); }
    public double RailWidth => _railWidth;

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) =>
        new XrNavigationRailItem();

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        if (container is XrNavigationRailItem destination)
        {
            if (destination.Label is null && item is string text)
                destination.Label = text;
            destination.SetRailState(
                Variant == XrNavigationRailVariant.Wide,
                Variant == XrNavigationRailVariant.Wide && IsExpanded);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!XrNavigationKeyboard.TryHandle(this, e, vertical: true))
            base.OnKeyDown(e);
    }

    private void UpdateChromeState() => PseudoClasses.Set(":no-fab", !ShowFab);

    private void UpdateHeaderState() => PseudoClasses.Set(":has-header", Header is not null);

    // Wide geometry is a template concern: the rail only publishes its state as pseudo classes
    // (and the surface width), so the standard 96/20/20 anatomy keeps its existing template path.
    private void UpdateRailState()
    {
        var wide = Variant == XrNavigationRailVariant.Wide;
        var expanded = wide && IsExpanded;
        PseudoClasses.Set(":wide", wide);
        PseudoClasses.Set(":expanded", expanded);
        UpdateHeaderState();
        UpdateChromeState();
        UpdateRailWidth(wide, expanded);

        for (var i = 0; i < ItemCount; i++)
        {
            if (ContainerFromIndex(i) is XrNavigationRailItem item)
                item.SetRailState(wide, expanded);
        }
    }

    private void UpdateRailWidth(bool wide, bool expanded)
    {
        var requested = double.IsFinite(ExpandedWidth) ? ExpandedWidth : 300d;
        var width = wide
            ? expanded ? Math.Clamp(requested, 220d, 360d) : 96d
            : double.NaN;
        if (width.Equals(_railWidth))
            return;

        var previous = _railWidth;
        _railWidth = width;
        RaisePropertyChanged(RailWidthProperty, previous, width);
    }
}
