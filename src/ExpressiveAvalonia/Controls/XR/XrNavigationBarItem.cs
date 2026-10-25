using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// A navigation bar destination: a 64 by 32 icon container, a 12 unit label, and an optional
/// small or large badge.
/// </summary>
/// <remarks>
/// The item is a native <see cref="ListBoxItem"/>; the owning <see cref="XrNavigationBar"/>
/// supplies single selection, pointer and keyboard selection, disabled handling, and the
/// SelectionItemPattern. Visual state comes from the <c>:selected</c> pseudo-class.
/// </remarks>
public class XrNavigationBarItem : ListBoxItem
{
    /// <summary>The unselected icon path.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<XrNavigationBarItem, Geometry?>(nameof(Icon));

    /// <summary>The selected icon path; falls back to <see cref="Icon"/> when unset.</summary>
    public static readonly StyledProperty<Geometry?> SelectedIconProperty =
        AvaloniaProperty.Register<XrNavigationBarItem, Geometry?>(nameof(SelectedIcon));

    /// <summary>The destination label, also used as the accessible name.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<XrNavigationBarItem, string?>(nameof(Label));

    /// <summary>Whether the label participates in the layout.</summary>
    public static readonly StyledProperty<bool> ShowLabelProperty =
        AvaloniaProperty.Register<XrNavigationBarItem, bool>(nameof(ShowLabel), true);

    public static readonly StyledProperty<XrBadgeKind> BadgeProperty =
        AvaloniaProperty.Register<XrNavigationBarItem, XrBadgeKind>(nameof(Badge));

    /// <summary>Text of a <see cref="XrBadgeKind.Large"/> badge.</summary>
    public static readonly StyledProperty<string?> BadgeTextProperty =
        AvaloniaProperty.Register<XrNavigationBarItem, string?>(nameof(BadgeText));

    /// <summary>The checked glyph, resolved from <see cref="SelectedIcon"/> and <see cref="Icon"/>.</summary>
    public static readonly DirectProperty<XrNavigationBarItem, Geometry?> EffectiveSelectedIconProperty =
        AvaloniaProperty.RegisterDirect<XrNavigationBarItem, Geometry?>(nameof(EffectiveSelectedIcon),
            item => item.EffectiveSelectedIcon);

    private Geometry? _effectiveSelectedIcon;

    static XrNavigationBarItem()
    {
        IconProperty.Changed.AddClassHandler<XrNavigationBarItem>((item, _) => item.UpdateSelectedIcon());
        SelectedIconProperty.Changed.AddClassHandler<XrNavigationBarItem>((item, _) => item.UpdateSelectedIcon());
        LabelProperty.Changed.AddClassHandler<XrNavigationBarItem>((item, _) => item.UpdateAutomation());
        BadgeProperty.Changed.AddClassHandler<XrNavigationBarItem>((item, _) => item.UpdateBadgeState());
        BadgeTextProperty.Changed.AddClassHandler<XrNavigationBarItem>((item, _) => item.UpdateAutomation());
        ShowLabelProperty.Changed.AddClassHandler<XrNavigationBarItem>((item, _) => item.UpdateBadgeState());
    }

    public XrNavigationBarItem()
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
