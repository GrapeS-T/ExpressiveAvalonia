using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// A labeled, toggleable XR button: a 40 unit pill with an optional leading icon and a label.
/// </summary>
/// <remarks>
/// Native <see cref="ToggleButton"/> behavior supplies pointer and Space/Enter toggling,
/// commands, focus, disabled handling, and the toggle automation peer. The selected container
/// uses the surface's emphasized role, as in the source building block.
/// </remarks>
public class XrToggleButton : ToggleButton
{
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<XrToggleButton, Geometry?>(nameof(Icon));

    /// <summary>The glyph shown while checked; falls back to <see cref="Icon"/> when unset.</summary>
    public static readonly StyledProperty<Geometry?> SelectedIconProperty =
        AvaloniaProperty.Register<XrToggleButton, Geometry?>(nameof(SelectedIcon));

    /// <summary>Whether the leading icon participates in the layout.</summary>
    public static readonly StyledProperty<bool> ShowIconProperty =
        AvaloniaProperty.Register<XrToggleButton, bool>(nameof(ShowIcon), true);

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<XrToggleButton, string?>(nameof(Label));

    public static readonly StyledProperty<XrSurfaceRole> SurfaceProperty =
        AvaloniaProperty.Register<XrToggleButton, XrSurfaceRole>(nameof(Surface));

    /// <summary>The checked glyph, resolved from <see cref="SelectedIcon"/> and <see cref="Icon"/>.</summary>
    public static readonly DirectProperty<XrToggleButton, Geometry?> EffectiveSelectedIconProperty =
        AvaloniaProperty.RegisterDirect<XrToggleButton, Geometry?>(nameof(EffectiveSelectedIcon),
            button => button.EffectiveSelectedIcon);

    private Geometry? _effectiveSelectedIcon;

    static XrToggleButton()
    {
        IconProperty.Changed.AddClassHandler<XrToggleButton>((button, _) => button.UpdateSelectedIcon());
        SelectedIconProperty.Changed.AddClassHandler<XrToggleButton>((button, _) => button.UpdateSelectedIcon());
        ShowIconProperty.Changed.AddClassHandler<XrToggleButton>((button, _) => button.UpdateIconVisibility());
        SurfaceProperty.Changed.AddClassHandler<XrToggleButton>((button, _) => button.UpdateSurfaceState());
        LabelProperty.Changed.AddClassHandler<XrToggleButton>((button, _) => button.UpdateAutomationName());
    }

    public XrToggleButton()
    {
        UpdateSurfaceState();
        UpdateSelectedIcon();
        UpdateIconVisibility();
    }

    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    public Geometry? SelectedIcon { get => GetValue(SelectedIconProperty); set => SetValue(SelectedIconProperty, value); }

    public bool ShowIcon { get => GetValue(ShowIconProperty); set => SetValue(ShowIconProperty, value); }

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public XrSurfaceRole Surface { get => GetValue(SurfaceProperty); set => SetValue(SurfaceProperty, value); }

    public Geometry? EffectiveSelectedIcon => _effectiveSelectedIcon;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ContentProperty && Label is null && Content is string text)
            SetCurrentValue(LabelProperty, text);
    }

    private string? _appliedName;

    private void UpdateSelectedIcon()
    {
        UpdateIconVisibility();
        var icon = SelectedIcon ?? Icon;
        if (icon == _effectiveSelectedIcon)
            return;

        var previous = _effectiveSelectedIcon;
        _effectiveSelectedIcon = icon;
        RaisePropertyChanged(EffectiveSelectedIconProperty, previous, icon);
    }

    // The row collapses when the switch is off or when neither glyph exists, so a text-only
    // toggle does not reserve an empty 18 unit icon slot.
    private void UpdateIconVisibility() =>
        PseudoClasses.Set(":no-icon", !ShowIcon || (Icon is null && SelectedIcon is null));

    private void UpdateSurfaceState()
    {
        PseudoClasses.Set(":surface-high", Surface == XrSurfaceRole.SurfaceContainerHigh);
        PseudoClasses.Set(":tertiary-container", Surface == XrSurfaceRole.TertiaryContainer);
    }

    private void UpdateAutomationName()
    {
        var label = Label;
        if (string.IsNullOrEmpty(label))
            return;

        var current = AutomationProperties.GetName(this);
        if (string.IsNullOrEmpty(current) || current == _appliedName)
        {
            AutomationProperties.SetName(this, label);
            _appliedName = label;
        }
    }
}
