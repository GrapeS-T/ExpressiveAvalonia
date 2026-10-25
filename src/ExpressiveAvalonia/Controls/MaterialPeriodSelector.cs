using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// The Material AM/PM period selector: two exclusive cells in one rounded, outlined container.
/// </summary>
/// <remarks>
/// <para>
/// Vertical is the 52 x 80 stack used beside the dial and keyboard time cells (two 52 x 40 cells);
/// <see cref="IsCompact"/> shortens it to 52 x 72 for the keyboard layout. Horizontal is the
/// 216 x 38 segmented control used under the inputs in the horizontal dial layout (two 108 x 38
/// cells). The selected cell uses the secondary container role.
/// </para>
/// <para>
/// The cells are native <see cref="ToggleButton"/>s, so Space/Enter toggle them and the group stays
/// exclusive; Up/Down (vertical) and Left/Right (horizontal) also select directly.
/// </para>
/// </remarks>
public sealed class MaterialPeriodSelector : TemplatedControl
{
    public static readonly StyledProperty<bool> IsPmProperty =
        AvaloniaProperty.Register<MaterialPeriodSelector, bool>(nameof(IsPm),
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<MaterialPeriodSelector, Orientation>(nameof(Orientation), Orientation.Vertical);

    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<MaterialPeriodSelector, bool>(nameof(IsCompact));

    private ToggleButton? _am;
    private ToggleButton? _pm;
    private bool _syncing;

    public MaterialPeriodSelector()
    {
        AddHandler(KeyDownEvent, OnSelectorKeyDown, RoutingStrategies.Tunnel);
    }

    /// <summary>True when PM is selected; false when AM is.</summary>
    public bool IsPm
    {
        get => GetValue(IsPmProperty);
        set => SetValue(IsPmProperty, value);
    }

    /// <summary>Vertical is the dial/keyboard stack; horizontal is the segmented row.</summary>
    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>Shortens the vertical container from 80 to 72 DIPs for the keyboard layout.</summary>
    public bool IsCompact
    {
        get => GetValue(IsCompactProperty);
        set => SetValue(IsCompactProperty, value);
    }

    /// <summary>Raised when the selected period changes through pointer or keyboard input.</summary>
    public event EventHandler? IsPmChanged;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_am is not null) _am.Click -= CellClick;
        if (_pm is not null) _pm.Click -= CellClick;

        base.OnApplyTemplate(e);
        _am = e.NameScope.Find<ToggleButton>("PART_Am");
        _pm = e.NameScope.Find<ToggleButton>("PART_Pm");
        if (_am is not null) _am.Click += CellClick;
        if (_pm is not null) _pm.Click += CellClick;
        UpdateOrientation();
        UpdateCompact();
        UpdateSelection();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsPmProperty)
        {
            UpdateSelection();
            IsPmChanged?.Invoke(this, EventArgs.Empty);
        }
        else if (change.Property == OrientationProperty)
        {
            UpdateOrientation();
        }
        else if (change.Property == IsCompactProperty)
        {
            UpdateCompact();
        }
    }

    private void CellClick(object? sender, RoutedEventArgs e)
    {
        var isPm = ReferenceEquals(sender, _pm);
        if (!_syncing)
        {
            if (IsPm != isPm)
                SetCurrentValue(IsPmProperty, isPm);
            // Clicking the cell that is already selected unchecks the native toggle; the group
            // must stay exclusive, so the state is re-applied either way.
            UpdateSelection();
        }
    }

    private void OnSelectorKeyDown(object? sender, KeyEventArgs e)
    {
        if (!IsEffectivelyEnabled) return;
        bool? requested = e.Key switch
        {
            Key.Up or Key.Left => false,
            Key.Down or Key.Right => true,
            _ => null,
        };

        if (requested is null) return;
        e.Handled = true;
        if (IsPm == requested.Value) return;
        SetCurrentValue(IsPmProperty, requested.Value);
        (requested.Value ? _pm : _am)?.Focus(NavigationMethod.Directional);
    }

    private void UpdateSelection()
    {
        _syncing = true;
        if (_am is not null) _am.IsChecked = !IsPm;
        if (_pm is not null) _pm.IsChecked = IsPm;
        _syncing = false;
    }

    private void UpdateOrientation()
    {
        // The template lays both cells out on a single axis; the class carries the axis so the
        // divider can follow without a second template.
        PseudoClasses.Set(":horizontal", Orientation == Avalonia.Layout.Orientation.Horizontal);
    }

    private void UpdateCompact() => PseudoClasses.Set(":compact", IsCompact);
}
