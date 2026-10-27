using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;

namespace ExpressiveAvalonia.Controls;

/// <summary>The five Material window size classes, measured in device-independent pixels.</summary>
public enum MaterialWindowSizeClass
{
    /// <summary>0-599 dp.</summary>
    Compact,
    /// <summary>600-839 dp.</summary>
    Medium,
    /// <summary>840-1199 dp.</summary>
    Expanded,
    /// <summary>1200-1599 dp.</summary>
    Large,
    /// <summary>1600 dp and wider.</summary>
    ExtraLarge
}

/// <summary>How the navigation region is presented.</summary>
public enum MaterialNavigationRegion
{
    /// <summary>No navigation region.</summary>
    None,
    /// <summary>Compact rail (leading column when wide, bottom bar when narrow).</summary>
    NavigationRegion,
    /// <summary>Expanded navigation column; falls back to the compact rail when narrow.</summary>
    NavigationExpanded
}

/// <summary>Reports a window size class change.</summary>
public sealed class MaterialSizeClassChangedEventArgs(MaterialWindowSizeClass oldClass, MaterialWindowSizeClass newClass)
    : EventArgs
{
    public MaterialWindowSizeClass OldClass { get; } = oldClass;
    public MaterialWindowSizeClass NewClass { get; } = newClass;
}

/// <summary>
/// The adaptive layout grid from the source layout sheet: five window size classes crossed with
/// three navigation-region modes.
/// </summary>
/// <remarks>
/// A design-source desktop composition, not an M3 composable. The pinned M3 artifact has no
/// adaptive layout-grid component; only the common window size classes are applied.
/// <para>
/// The control measures its own width against the Material breakpoints (600, 840, 1200, 1600 dp)
/// and reflows the navigation region: a bottom bar below 840 dp, a 72 DIP rail from 840 dp, and an
/// expanded column from 1600 dp when <see cref="ExpandedNavigationContent"/> is supplied. Set
/// <see cref="RequestedNavigation"/> to force a mode; the resolved mode is published through
/// <see cref="NavigationRegion"/> and <see cref="SizeClassChanged"/>. All content is hosted in
/// native presenters, so focus order and automation follow the visual order after each reflow.
/// </para>
/// </remarks>
public sealed class MaterialLayoutGrid : ContentControl
{
    public const double MediumBreakpoint = 600;
    public const double ExpandedBreakpoint = 840;
    public const double LargeBreakpoint = 1200;
    public const double ExtraLargeBreakpoint = 1600;

    /// <summary>Forces a navigation mode; <see cref="MaterialNavigationRegion.None"/> means "no navigation".</summary>
    public static readonly StyledProperty<MaterialNavigationRegion> RequestedNavigationProperty =
        AvaloniaProperty.Register<MaterialLayoutGrid, MaterialNavigationRegion>(nameof(RequestedNavigation));

    /// <summary>Adapts the navigation mode to the measured size class when true.</summary>
    public static readonly StyledProperty<bool> AutoNavigationProperty =
        AvaloniaProperty.Register<MaterialLayoutGrid, bool>(nameof(AutoNavigation), true);

    /// <summary>The compact navigation region (a rail when wide, a bottom bar when narrow).</summary>
    public static readonly StyledProperty<object?> NavigationContentProperty =
        AvaloniaProperty.Register<MaterialLayoutGrid, object?>(nameof(NavigationContent));

    /// <summary>The expanded navigation column shown at extra-large widths.</summary>
    public static readonly StyledProperty<object?> ExpandedNavigationContentProperty =
        AvaloniaProperty.Register<MaterialLayoutGrid, object?>(nameof(ExpandedNavigationContent));

    /// <summary>Width of the compact rail; the source uses 72 DIP.</summary>
    public static readonly StyledProperty<double> RailWidthProperty =
        AvaloniaProperty.Register<MaterialLayoutGrid, double>(nameof(RailWidth), 72,
            validate: static value => double.IsFinite(value) && value >= 0);

    /// <summary>Width of the expanded navigation column; the source navigation drawer uses 360 DIP.</summary>
    public static readonly StyledProperty<double> ExpandedNavigationWidthProperty =
        AvaloniaProperty.Register<MaterialLayoutGrid, double>(nameof(ExpandedNavigationWidth), 360,
            validate: static value => double.IsFinite(value) && value >= 0);

    /// <summary>Height of the bottom navigation bar used below 840 dp; the source uses 80 DIP.</summary>
    public static readonly StyledProperty<double> BottomNavigationHeightProperty =
        AvaloniaProperty.Register<MaterialLayoutGrid, double>(nameof(BottomNavigationHeight), 80,
            validate: static value => double.IsFinite(value) && value >= 0);

    public static readonly DirectProperty<MaterialLayoutGrid, MaterialWindowSizeClass> SizeClassProperty =
        AvaloniaProperty.RegisterDirect<MaterialLayoutGrid, MaterialWindowSizeClass>(
            nameof(SizeClass), static grid => grid.SizeClass);

    public static readonly DirectProperty<MaterialLayoutGrid, MaterialNavigationRegion> NavigationRegionProperty =
        AvaloniaProperty.RegisterDirect<MaterialLayoutGrid, MaterialNavigationRegion>(
            nameof(NavigationRegion), static grid => grid.NavigationRegion);

    private MaterialWindowSizeClass _sizeClass = MaterialWindowSizeClass.Compact;
    private MaterialNavigationRegion _navigationRegion = MaterialNavigationRegion.None;
    private Grid? _root;
    private ContentPresenter? _contentHost;
    private ContentPresenter? _navigationHost;

    static MaterialLayoutGrid()
    {
        AutoNavigationProperty.Changed.AddClassHandler<MaterialLayoutGrid>(static (grid, _) => grid.ApplyLayout());
        RequestedNavigationProperty.Changed.AddClassHandler<MaterialLayoutGrid>(static (grid, _) => grid.ApplyLayout());
        NavigationContentProperty.Changed.AddClassHandler<MaterialLayoutGrid>(static (grid, _) => grid.ApplyLayout());
        ExpandedNavigationContentProperty.Changed.AddClassHandler<MaterialLayoutGrid>(static (grid, _) => grid.ApplyLayout());
        RailWidthProperty.Changed.AddClassHandler<MaterialLayoutGrid>(static (grid, _) => grid.ApplyLayout());
        ExpandedNavigationWidthProperty.Changed.AddClassHandler<MaterialLayoutGrid>(static (grid, _) => grid.ApplyLayout());
        BottomNavigationHeightProperty.Changed.AddClassHandler<MaterialLayoutGrid>(static (grid, _) => grid.ApplyLayout());
    }

    public MaterialLayoutGrid()
    {
        Template = new FuncControlTemplate<MaterialLayoutGrid>((grid, _) => grid.Build());
        AutomationProperties.SetName(this, "Layout");
    }

    /// <summary>Raised when the measured size class changes.</summary>
    public event EventHandler<MaterialSizeClassChangedEventArgs>? SizeClassChanged;

    /// <summary>Raised when the resolved navigation mode changes.</summary>
    public event EventHandler? NavigationRegionChanged;

    public MaterialNavigationRegion RequestedNavigation { get => GetValue(RequestedNavigationProperty); set => SetValue(RequestedNavigationProperty, value); }
    public bool AutoNavigation { get => GetValue(AutoNavigationProperty); set => SetValue(AutoNavigationProperty, value); }
    public object? NavigationContent { get => GetValue(NavigationContentProperty); set => SetValue(NavigationContentProperty, value); }
    public object? ExpandedNavigationContent { get => GetValue(ExpandedNavigationContentProperty); set => SetValue(ExpandedNavigationContentProperty, value); }
    public double RailWidth { get => GetValue(RailWidthProperty); set => SetValue(RailWidthProperty, value); }
    public double ExpandedNavigationWidth { get => GetValue(ExpandedNavigationWidthProperty); set => SetValue(ExpandedNavigationWidthProperty, value); }
    public double BottomNavigationHeight { get => GetValue(BottomNavigationHeightProperty); set => SetValue(BottomNavigationHeightProperty, value); }

    /// <summary>The measured size class. Compact until the first layout pass.</summary>
    public MaterialWindowSizeClass SizeClass
    {
        get => _sizeClass;
        private set
        {
            if (_sizeClass == value) return;
            var old = _sizeClass;
            SetAndRaise(SizeClassProperty, ref _sizeClass, value);
            SizeClassChanged?.Invoke(this, new MaterialSizeClassChangedEventArgs(old, value));
            ApplyLayout();
        }
    }

    /// <summary>The navigation mode after applying the request and the size class.</summary>
    public MaterialNavigationRegion NavigationRegion
    {
        get => _navigationRegion;
        private set
        {
            if (_navigationRegion == value) return;
            SetAndRaise(NavigationRegionProperty, ref _navigationRegion, value);
            NavigationRegionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Maps a width in DIPs to its Material window size class.</summary>
    public static MaterialWindowSizeClass FromWidth(double width) => width switch
    {
        < MediumBreakpoint => MaterialWindowSizeClass.Compact,
        < ExpandedBreakpoint => MaterialWindowSizeClass.Medium,
        < LargeBreakpoint => MaterialWindowSizeClass.Expanded,
        < ExtraLargeBreakpoint => MaterialWindowSizeClass.Large,
        _ => MaterialWindowSizeClass.ExtraLarge
    };

    private Control Build()
    {
        _contentHost = new ContentPresenter
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch
        };
        _contentHost[!ContentPresenter.ContentProperty] = this[!ContentProperty];
        _contentHost[!ContentPresenter.ContentTemplateProperty] = this[!ContentTemplateProperty];

        _navigationHost = new ContentPresenter
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch
        };

        _root = new Grid { ColumnDefinitions = new ColumnDefinitions("0,*"), RowDefinitions = new RowDefinitions("*,0") };
        _root.Children.Add(_contentHost);
        _root.Children.Add(_navigationHost);
        Grid.SetColumn(_contentHost, 1);

        SizeChanged += (_, _) => SizeClass = FromWidth(Bounds.Width);
        ApplyLayout();
        return _root;
    }

    private void ApplyLayout()
    {
        if (_root is null || _navigationHost is null) return;

        var requested = RequestedNavigation;
        var region = requested;
        if (AutoNavigation)
        {
            if (requested == MaterialNavigationRegion.None)
            {
                region = MaterialNavigationRegion.None;
            }
            else
            {
                var expandedAvailable = ExpandedNavigationContent is not null &&
                                        SizeClass == MaterialWindowSizeClass.ExtraLarge;
                region = expandedAvailable
                    ? MaterialNavigationRegion.NavigationExpanded
                    : NavigationContent is not null
                        ? MaterialNavigationRegion.NavigationRegion
                        : MaterialNavigationRegion.None;
            }
        }

        // An expanded request degrades to the compact rail when the window is too narrow or when
        // no expanded column was supplied.
        if (region == MaterialNavigationRegion.NavigationExpanded &&
            (SizeClass < MaterialWindowSizeClass.ExtraLarge || ExpandedNavigationContent is null))
            region = NavigationContent is not null ? MaterialNavigationRegion.NavigationRegion : MaterialNavigationRegion.None;

        NavigationRegion = region;

        var side = region != MaterialNavigationRegion.None && SizeClass >= MaterialWindowSizeClass.Expanded;
        var content = region == MaterialNavigationRegion.NavigationExpanded
            ? ExpandedNavigationContent ?? NavigationContent
            : NavigationContent;
        _navigationHost.Content = content;
        _navigationHost.IsVisible = region != MaterialNavigationRegion.None && content is not null;

        if (region == MaterialNavigationRegion.None)
        {
            _root.ColumnDefinitions = new ColumnDefinitions("0,*");
            _root.RowDefinitions = new RowDefinitions("*");
            Grid.SetColumn(_navigationHost, 0);
            Grid.SetColumnSpan(_navigationHost, 1);
            Grid.SetRow(_navigationHost, 0);
            Grid.SetRowSpan(_navigationHost, 1);
            return;
        }

        if (side)
        {
            var width = region == MaterialNavigationRegion.NavigationExpanded ? ExpandedNavigationWidth : RailWidth;
            _root.ColumnDefinitions = new ColumnDefinitions($"{width.ToString(System.Globalization.CultureInfo.InvariantCulture)},*");
            _root.RowDefinitions = new RowDefinitions("*");
            Grid.SetColumn(_navigationHost, 0);
            Grid.SetColumnSpan(_navigationHost, 1);
            Grid.SetRow(_navigationHost, 0);
            Grid.SetRowSpan(_navigationHost, 1);
        }
        else
        {
            _root.ColumnDefinitions = new ColumnDefinitions("0,*");
            _root.RowDefinitions = new RowDefinitions($"*,{BottomNavigationHeight.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            Grid.SetColumn(_navigationHost, 0);
            Grid.SetColumnSpan(_navigationHost, 2);
            Grid.SetRow(_navigationHost, 1);
            Grid.SetRowSpan(_navigationHost, 1);
        }
    }
}
