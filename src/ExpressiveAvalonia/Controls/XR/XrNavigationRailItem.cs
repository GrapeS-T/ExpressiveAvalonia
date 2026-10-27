using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// A navigation rail destination: a 32 unit tall icon pill, an optional 12 unit label, and an
/// optional small or large badge.
/// </summary>
/// <remarks>
/// The item is a native <see cref="ListBoxItem"/>; the owning <see cref="XrNavigationRail"/>
/// supplies single selection, keyboard selection, disabled handling, and the
/// SelectionItemPattern. The icon pill grows from 24 to 56 units while selected and becomes a
/// 56 unit square when <see cref="ShowLabel"/> is false, as in the source rail.
/// </remarks>
public class XrNavigationRailItem : ListBoxItem
{
    /// <summary>The unselected icon path.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<XrNavigationRailItem, Geometry?>(nameof(Icon));

    /// <summary>The selected icon path; falls back to <see cref="Icon"/> when unset.</summary>
    public static readonly StyledProperty<Geometry?> SelectedIconProperty =
        AvaloniaProperty.Register<XrNavigationRailItem, Geometry?>(nameof(SelectedIcon));

    /// <summary>The destination label, also used as the accessible name.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<XrNavigationRailItem, string?>(nameof(Label));

    /// <summary>
    /// Whether the label participates in the layout. The source rail item keeps its label visible
    /// by default (<c>alwaysShowLabel = true</c>); the design rail shows labels in both its
    /// embedded-FAB and no-FAB variants.
    /// </summary>
    public static readonly StyledProperty<bool> ShowLabelProperty =
        AvaloniaProperty.Register<XrNavigationRailItem, bool>(nameof(ShowLabel), true);

    public static readonly StyledProperty<XrBadgeKind> BadgeProperty =
        AvaloniaProperty.Register<XrNavigationRailItem, XrBadgeKind>(nameof(Badge));

    /// <summary>Text of a <see cref="XrBadgeKind.Large"/> badge.</summary>
    public static readonly StyledProperty<string?> BadgeTextProperty =
        AvaloniaProperty.Register<XrNavigationRailItem, string?>(nameof(BadgeText));

    /// <summary>The checked glyph, resolved from <see cref="SelectedIcon"/> and <see cref="Icon"/>.</summary>
    public static readonly DirectProperty<XrNavigationRailItem, Geometry?> EffectiveSelectedIconProperty =
        AvaloniaProperty.RegisterDirect<XrNavigationRailItem, Geometry?>(nameof(EffectiveSelectedIcon),
            item => item.EffectiveSelectedIcon);

    private Geometry? _effectiveSelectedIcon;

    static XrNavigationRailItem()
    {
        IconProperty.Changed.AddClassHandler<XrNavigationRailItem>((item, _) => item.UpdateSelectedIcon());
        SelectedIconProperty.Changed.AddClassHandler<XrNavigationRailItem>((item, _) => item.UpdateSelectedIcon());
        LabelProperty.Changed.AddClassHandler<XrNavigationRailItem>((item, _) => item.UpdateAutomation());
        BadgeProperty.Changed.AddClassHandler<XrNavigationRailItem>((item, _) => item.UpdateBadgeState());
        BadgeTextProperty.Changed.AddClassHandler<XrNavigationRailItem>((item, _) => item.UpdateAutomation());
        ShowLabelProperty.Changed.AddClassHandler<XrNavigationRailItem>((item, _) => item.UpdateBadgeState());
    }

    public XrNavigationRailItem()
    {
        UpdateSelectedIcon();
        UpdateBadgeState();
    }

    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public Geometry? SelectedIcon { get => GetValue(SelectedIconProperty); set => SetValue(SelectedIconProperty, value); }
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public bool ShowLabel { get => GetValue(ShowLabelProperty); set => SetValue(ShowLabelProperty, value); }
    public XrBadgeKind Badge { get => GetValue(BadgeProperty); set => SetValue(BadgeProperty, value); }
    public string? BadgeText { get => GetValue(BadgeTextProperty); set => SetValue(BadgeTextProperty, value); }
    public Geometry? EffectiveSelectedIcon => _effectiveSelectedIcon;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ContentProperty && Label is null && Content is string text)
            SetCurrentValue(LabelProperty, text);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && IsEffectivelyEnabled)
            PseudoClasses.Set(":pressed", true);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        PseudoClasses.Set(":pressed", false);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        PseudoClasses.Set(":pressed", false);
    }

    private string? _appliedName;

    /// <summary>
    /// Applies the owning rail's variant state. The rail sets these pseudo classes so the wide
    /// expanded geometry can be expressed in this item's theme without changing the standard one.
    /// </summary>
    internal void SetRailState(bool wide, bool expanded)
    {
        PseudoClasses.Set(":rail-wide", wide);
        PseudoClasses.Set(":rail-expanded", expanded);
    }

    private void UpdateSelectedIcon()
    {
        var icon = SelectedIcon ?? Icon;
        if (icon == _effectiveSelectedIcon)
            return;

        var previous = _effectiveSelectedIcon;
        _effectiveSelectedIcon = icon;
        RaisePropertyChanged(EffectiveSelectedIconProperty, previous, icon);
    }

    private void UpdateBadgeState()
    {
        PseudoClasses.Set(":badge-small", Badge == XrBadgeKind.Small);
        PseudoClasses.Set(":badge-large", Badge == XrBadgeKind.Large);
        PseudoClasses.Set(":no-label", !ShowLabel);
    }

    private void UpdateAutomation()
    {
        var label = Label;
        if (!string.IsNullOrEmpty(label))
        {
            var current = AutomationProperties.GetName(this);
            if (string.IsNullOrEmpty(current) || current == _appliedName)
            {
                AutomationProperties.SetName(this, label);
                _appliedName = label;
            }
        }

        if (!string.IsNullOrEmpty(BadgeText))
            AutomationProperties.SetItemStatus(this, BadgeText);
    }
}
