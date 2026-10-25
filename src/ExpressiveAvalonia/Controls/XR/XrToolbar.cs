using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// An XR toolbar: a fully rounded surface pill of icon slots with an optional floating action
/// button beside (horizontal) or below (vertical) it.
/// </summary>
/// <remarks>
/// Items are ordinary focusable controls, normally <see cref="XrIconButton"/> or
/// <see cref="XrToggleIconButton"/>. Left/Right (horizontal) or Up/Down (vertical) move focus
/// between the realized slots, while Tab, Space/Enter, and access keys stay native.
/// The source axes are <see cref="Orientation"/> and <see cref="Elevation"/>; the FAB axis is
/// <see cref="ShowFab"/>.
/// </remarks>
public class XrToolbar : ItemsControl
{
    /// <summary>Horizontal places the FAB after the pill; vertical places it below.</summary>
    public static readonly StyledProperty<Avalonia.Layout.Orientation> OrientationProperty =
        AvaloniaProperty.Register<XrToolbar, Avalonia.Layout.Orientation>(nameof(Orientation), Avalonia.Layout.Orientation.Horizontal);

    /// <summary>The surface tone of the pill. The FAB follows the tertiary roles.</summary>
    public static readonly StyledProperty<XrSurfaceRole> ElevationProperty =
        AvaloniaProperty.Register<XrToolbar, XrSurfaceRole>(nameof(Elevation));

    /// <summary>Whether the spatial FAB is shown.</summary>
    public static readonly StyledProperty<bool> ShowFabProperty =
        AvaloniaProperty.Register<XrToolbar, bool>(nameof(ShowFab), true);

    public static readonly StyledProperty<Geometry?> FabIconProperty =
        AvaloniaProperty.Register<XrToolbar, Geometry?>(nameof(FabIcon), XrIcons.Add);

    /// <summary>Accessible name of the FAB.</summary>
    public static readonly StyledProperty<string?> FabLabelProperty =
        AvaloniaProperty.Register<XrToolbar, string?>(nameof(FabLabel), "Create");

    public static readonly StyledProperty<ICommand?> FabCommandProperty =
        AvaloniaProperty.Register<XrToolbar, ICommand?>(nameof(FabCommand));

    public static readonly StyledProperty<object?> FabCommandParameterProperty =
        AvaloniaProperty.Register<XrToolbar, object?>(nameof(FabCommandParameter));

    static XrToolbar()
    {
        OrientationProperty.Changed.AddClassHandler<XrToolbar>((toolbar, _) => toolbar.UpdateOrientation());
        ElevationProperty.Changed.AddClassHandler<XrToolbar>((toolbar, _) => toolbar.UpdateElevation());
    }

    public XrToolbar()
    {
        UpdateOrientation();
        UpdateElevation();
    }

    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public XrSurfaceRole Elevation { get => GetValue(ElevationProperty); set => SetValue(ElevationProperty, value); }
    public bool ShowFab { get => GetValue(ShowFabProperty); set => SetValue(ShowFabProperty, value); }
    public Geometry? FabIcon { get => GetValue(FabIconProperty); set => SetValue(FabIconProperty, value); }
    public string? FabLabel { get => GetValue(FabLabelProperty); set => SetValue(FabLabelProperty, value); }
    public ICommand? FabCommand { get => GetValue(FabCommandProperty); set => SetValue(FabCommandProperty, value); }
    public object? FabCommandParameter { get => GetValue(FabCommandParameterProperty); set => SetValue(FabCommandParameterProperty, value); }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!TryMoveFocus(e))
            base.OnKeyDown(e);
    }

    private void UpdateOrientation()
    {
        var orientation = Orientation;
        ItemsPanel = new FuncTemplate<Panel?>(() => new StackPanel
        {
            Orientation = orientation,
            Spacing = 4
        });
        PseudoClasses.Set(":vertical", orientation == Avalonia.Layout.Orientation.Vertical);
    }

    private void UpdateElevation()
    {
        PseudoClasses.Set(":elevation-high", Elevation == XrSurfaceRole.SurfaceContainerHigh);
        PseudoClasses.Set(":elevation-highest", Elevation == XrSurfaceRole.SurfaceContainerHighest);
        PseudoClasses.Set(":tertiary-container", Elevation == XrSurfaceRole.TertiaryContainer);
    }

    /// <summary>Moves focus between slots along the toolbar axis, wrapping at the ends.</summary>
    private bool TryMoveFocus(KeyEventArgs e)
    {
        if (e.Handled || e.KeyModifiers != KeyModifiers.None)
            return false;

        var delta = Orientation == Avalonia.Layout.Orientation.Horizontal
            ? e.Key switch { Key.Left => -1, Key.Right => 1, _ => 0 }
            : e.Key switch { Key.Up => -1, Key.Down => 1, _ => 0 };
        if (delta == 0)
            return false;

        // ItemsControl wraps items in non-focusable ContentPresenters, so resolve the first
        // focusable control inside each realized container before moving focus.
        var slots = new List<Control>();
        foreach (var container in GetRealizedContainers())
        {
            var target = container.Focusable
                ? container
                : container.GetVisualDescendants().OfType<Control>().FirstOrDefault(candidate => candidate.Focusable);
            if (target is { IsEffectivelyEnabled: true })
                slots.Add(target);
        }

        if (slots.Count == 0)
            return false;

        var current = slots.FindIndex(slot => IsAncestorOf(slot, e.Source as Visual));
        var next = current < 0 ? (delta > 0 ? 0 : slots.Count - 1) : (current + delta + slots.Count) % slots.Count;
        slots[next].Focus(NavigationMethod.Directional);
        e.Handled = true;
        return true;
    }

    private static bool IsAncestorOf(Control candidate, Visual? source)
    {
        for (var visual = source; visual is not null; visual = visual.GetVisualParent())
        {
            if (ReferenceEquals(visual, candidate))
                return true;
        }

        return false;
    }
}
