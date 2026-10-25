using Avalonia;
using Avalonia.Controls.Primitives;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// A Material bottom app bar: a row of navigation or page actions on the leading edge and a
/// floating action button on the trailing edge.
/// </summary>
/// <remarks>
/// The bar is 80 units tall and hosts the actions declared in <see cref="MaterialBarBase.Actions"/>
/// as 48-unit icon buttons with an 8-unit gap. Actions beyond
/// <see cref="MaterialBarBase.MaxVisibleActions"/> (four by default, matching the design's
/// one-to-four icon variants) move into the overflow flyout. The FAB is arbitrary content, so an
/// application can host the library's FAB control or its own; the bar only reserves the slot.
/// </remarks>
public class MaterialBottomAppBar : MaterialBarBase
{
    /// <summary>Content shown in the trailing floating-action-button slot.</summary>
    public static readonly StyledProperty<object?> FabProperty =
        AvaloniaProperty.Register<MaterialBottomAppBar, object?>(nameof(Fab));

    public static readonly StyledProperty<bool> IsFabVisibleProperty =
        AvaloniaProperty.Register<MaterialBottomAppBar, bool>(nameof(IsFabVisible), true);

    public static readonly DirectProperty<MaterialBottomAppBar, bool> HasFabProperty =
        AvaloniaProperty.RegisterDirect<MaterialBottomAppBar, bool>(
            nameof(HasFab), bar => bar.HasFab, (bar, value) => bar.HasFab = value);

    private bool _hasFab;

    static MaterialBottomAppBar()
    {
        // The design's bottom app bar shows up to four icons before overflowing.
        MaxVisibleActionsProperty.OverrideDefaultValue<MaterialBottomAppBar>(4);
        FabProperty.Changed.AddClassHandler<MaterialBottomAppBar>((bar, _) => bar.UpdateFab());
        IsFabVisibleProperty.Changed.AddClassHandler<MaterialBottomAppBar>((bar, _) => bar.UpdateFab());
    }

    public MaterialBottomAppBar() => UpdateFab();

    public object? Fab
    {
        get => GetValue(FabProperty);
        set => SetValue(FabProperty, value);
    }

    public bool IsFabVisible
    {
        get => GetValue(IsFabVisibleProperty);
        set => SetValue(IsFabVisibleProperty, value);
    }

    /// <summary>True when <see cref="Fab"/> is set and <see cref="IsFabVisible"/> is enabled.</summary>
    public bool HasFab
    {
        get => _hasFab;
        private set => SetAndRaise(HasFabProperty, ref _hasFab, value);
    }

    private void UpdateFab() => HasFab = IsFabVisible && Fab is not null;
}
