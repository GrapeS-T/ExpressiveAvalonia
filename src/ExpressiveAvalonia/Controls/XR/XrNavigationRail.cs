using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// An XR navigation rail: a 96 unit rounded container with an optional menu button, an optional
/// embedded FAB, and single-selection destinations.
/// </summary>
/// <remarks>
/// The control is a <see cref="ListBox"/> with <see cref="XrNavigationRailItem"/> containers,
/// so selection, SelectionItemPattern, disabled items, and pointer activation are native.
/// Up/Down, Home/End, and Enter/Space select and focus destinations; the menu and FAB are
/// ordinary buttons and keep their own keyboard behavior. Items may be item controls or
/// strings (used as the label).
/// </remarks>
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

    static XrNavigationRail()
    {
        ShowFabProperty.Changed.AddClassHandler<XrNavigationRail>((rail, _) => rail.UpdateChromeState());
    }

    public XrNavigationRail()
    {
        SelectionMode = SelectionMode.Single;
        ItemsPanel = new FuncTemplate<Panel?>(() => new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 4
        });
        UpdateChromeState();
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

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) =>
        new XrNavigationRailItem();

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        if (container is XrNavigationRailItem destination && destination.Label is null && item is string text)
            destination.Label = text;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!XrNavigationKeyboard.TryHandle(this, e, vertical: true))
            base.OnKeyDown(e);
    }

    private void UpdateChromeState() => PseudoClasses.Set(":no-fab", !ShowFab);
}
