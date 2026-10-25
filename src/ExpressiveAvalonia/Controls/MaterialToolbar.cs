using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace ExpressiveAvalonia.Controls;

/// <summary>Whether the toolbar is docked to an edge or floats above the content.</summary>
public enum MaterialToolbarConfiguration
{
    /// <summary>Square, container-filled bar that spans its slot.</summary>
    Docked,
    /// <summary>Fully rounded, elevated pill that hugs its content.</summary>
    Floating
}

/// <summary>The toolbar color treatment.</summary>
public enum MaterialToolbarColor
{
    /// <summary>Surface container background with neutral icons.</summary>
    Standard,
    /// <summary>Secondary container background with the matching on-container icons.</summary>
    Vibrant
}

/// <summary>
/// A Material toolbar of icon actions in docked or floating form, horizontally or vertically.
/// </summary>
/// <remarks>
/// Floating toolbars use the full corner radius, the level 3 elevation and hug their content;
/// docked toolbars fill their slot and spread the actions evenly. Actions beyond
/// <see cref="MaterialBarBase.MaxVisibleActions"/> (six by default) move into the overflow flyout,
/// which opens below horizontal toolbars and to the side of vertical ones unless
/// <see cref="MaterialBarBase.OverflowPlacement"/> is set explicitly.
/// </remarks>
public class MaterialToolbar : MaterialBarBase
{
    public static readonly StyledProperty<MaterialToolbarConfiguration> ConfigurationProperty =
        AvaloniaProperty.Register<MaterialToolbar, MaterialToolbarConfiguration>(
            nameof(Configuration), MaterialToolbarConfiguration.Floating);

    /// <summary>Horizontal toolbars lay actions out in a row; vertical toolbars in a column.</summary>
    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<MaterialToolbar, Orientation>(
            nameof(Orientation), Avalonia.Layout.Orientation.Horizontal);

    public static readonly StyledProperty<MaterialToolbarColor> ColorProperty =
        AvaloniaProperty.Register<MaterialToolbar, MaterialToolbarColor>(
            nameof(Color), MaterialToolbarColor.Standard);

    /// <summary>Dock of the overflow button: right for horizontal toolbars, bottom for vertical ones.</summary>
    public static readonly DirectProperty<MaterialToolbar, Dock> OverflowDockProperty =
        AvaloniaProperty.RegisterDirect<MaterialToolbar, Dock>(
            nameof(OverflowDock), toolbar => toolbar.OverflowDock);

    private Dock _overflowDock = Dock.Right;

    static MaterialToolbar()
    {
        // The design's toolbar has six slots before an action has to overflow.
        MaxVisibleActionsProperty.OverrideDefaultValue<MaterialToolbar>(6);
        OrientationProperty.Changed.AddClassHandler<MaterialToolbar>((toolbar, _) => toolbar.UpdateOverflowDock());
    }

    public MaterialToolbar() => UpdateOverflowDock();

    public MaterialToolbarConfiguration Configuration
    {
        get => GetValue(ConfigurationProperty);
        set => SetValue(ConfigurationProperty, value);
    }

    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public MaterialToolbarColor Color
    {
        get => GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    /// <summary>Where the overflow button docks inside the toolbar's layout panel.</summary>
    public Dock OverflowDock
    {
        get => _overflowDock;
        private set => SetAndRaise(OverflowDockProperty, ref _overflowDock, value);
    }

    private void UpdateOverflowDock() =>
        OverflowDock = Orientation == Avalonia.Layout.Orientation.Vertical ? Dock.Bottom : Dock.Right;

    protected override PlacementMode GetOverflowPlacement() =>
        IsSet(OverflowPlacementProperty)
            ? base.GetOverflowPlacement()
            : Orientation == Avalonia.Layout.Orientation.Vertical
                ? PlacementMode.RightEdgeAlignedTop
                : PlacementMode.BottomEdgeAlignedRight;
}
