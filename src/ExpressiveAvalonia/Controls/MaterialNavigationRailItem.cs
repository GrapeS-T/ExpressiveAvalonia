using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// A navigation rail destination: an icon, a label, and an optional small or large badge.
/// </summary>
/// <remarks>
/// The container is a <see cref="ListBoxItem"/>, so selection, disabled handling, pointer
/// selection, focus, and SelectionItemPattern come from Avalonia. The rail supplies the
/// vertical (compact) or horizontal (expanded) layout through the <c>:vertical</c> and
/// <c>:horizontal</c> classes; <see cref="Orientation"/> can override that per item.
/// </remarks>
[PseudoClasses(":vertical", ":horizontal", ":no-label", ":no-icon", ":force-focus", ":custom-content", ":badge-small", ":badge-large")]
public class MaterialNavigationRailItem : ListBoxItem
{
    /// <summary>The unselected icon path. Its fill is supplied by the item theme.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<MaterialNavigationRailItem, Geometry?>(nameof(Icon));

    /// <summary>Optional selected icon path; falls back to <see cref="Icon"/> when unset.</summary>
    public static readonly StyledProperty<Geometry?> SelectedIconProperty =
        AvaloniaProperty.Register<MaterialNavigationRailItem, Geometry?>(nameof(SelectedIcon));

    public static readonly StyledProperty<MaterialNavigationRailItemOrientation> OrientationProperty =
        AvaloniaProperty.Register<MaterialNavigationRailItem, MaterialNavigationRailItemOrientation>(nameof(Orientation));

    /// <summary>Shows the label under the compact icon or beside the expanded icon.</summary>
    public static readonly StyledProperty<bool> ShowLabelProperty =
        AvaloniaProperty.Register<MaterialNavigationRailItem, bool>(nameof(ShowLabel), true);

    /// <summary>The source "Show icon" boolean of the horizontal item; hides the leading icon slot.</summary>
    public static readonly StyledProperty<bool> ShowIconProperty =
        AvaloniaProperty.Register<MaterialNavigationRailItem, bool>(nameof(ShowIcon), true);

    /// <summary>
    /// Forces the focus ring on, mirroring the source "Show focus indicator" boolean used by
    /// static previews. Keyboard focus shows the same ring without this property.
    /// </summary>
    public static readonly StyledProperty<bool> ShowFocusIndicatorProperty =
        AvaloniaProperty.Register<MaterialNavigationRailItem, bool>(nameof(ShowFocusIndicator));

    /// <summary>
    /// Explicit label text. When unset, the string value of <see cref="ContentControl.Content"/>
    /// is used, so a plain <c>Content="Home"</c> needs no extra property.
    /// </summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<MaterialNavigationRailItem, string?>(nameof(Label));

    /// <summary>The text actually rendered by the item template.</summary>
    public static readonly DirectProperty<MaterialNavigationRailItem, string?> EffectiveLabelProperty =
        AvaloniaProperty.RegisterDirect<MaterialNavigationRailItem, string?>(nameof(EffectiveLabel),
            item => item.EffectiveLabel);

    public static readonly StyledProperty<MaterialNavigationRailBadge> BadgeProperty =
        AvaloniaProperty.Register<MaterialNavigationRailItem, MaterialNavigationRailBadge>(nameof(Badge));

    /// <summary>Text of a <see cref="MaterialNavigationRailBadge.Large"/> badge; empty keeps the dot shape.</summary>
    public static readonly StyledProperty<string?> BadgeTextProperty =
        AvaloniaProperty.Register<MaterialNavigationRailItem, string?>(nameof(BadgeText));

    /// <summary>Invoked when the item is activated by pointer, Enter, or Space.</summary>
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<MaterialNavigationRailItem, ICommand?>(nameof(Command));

    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<MaterialNavigationRailItem, object?>(nameof(CommandParameter));

    /// <summary>The selected icon, or <see cref="Icon"/> when no selected variant was supplied.</summary>
    public static readonly DirectProperty<MaterialNavigationRailItem, Geometry?> EffectiveSelectedIconProperty =
        AvaloniaProperty.RegisterDirect<MaterialNavigationRailItem, Geometry?>(nameof(EffectiveSelectedIcon),
            item => item.EffectiveSelectedIcon);

    private Geometry? _effectiveSelectedIcon;
    private string? _effectiveLabel;
    private bool _pressed;

    static MaterialNavigationRailItem()
    {
        IconProperty.Changed.AddClassHandler<MaterialNavigationRailItem>((item, _) => item.UpdateSelectedIcon());
        SelectedIconProperty.Changed.AddClassHandler<MaterialNavigationRailItem>((item, _) => item.UpdateSelectedIcon());
        LabelProperty.Changed.AddClassHandler<MaterialNavigationRailItem>((item, _) => item.UpdateLabel());
    }

    /// <summary>Raised when the item is activated; the owning rail translates it into <see cref="MaterialNavigationRail.ItemInvoked"/>.</summary>
    public event EventHandler? Invoked;

    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public Geometry? SelectedIcon { get => GetValue(SelectedIconProperty); set => SetValue(SelectedIconProperty, value); }
    public MaterialNavigationRailItemOrientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }
    public bool ShowLabel { get => GetValue(ShowLabelProperty); set => SetValue(ShowLabelProperty, value); }
    public bool ShowIcon { get => GetValue(ShowIconProperty); set => SetValue(ShowIconProperty, value); }
    public bool ShowFocusIndicator { get => GetValue(ShowFocusIndicatorProperty); set => SetValue(ShowFocusIndicatorProperty, value); }
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string? EffectiveLabel => _effectiveLabel;
    public MaterialNavigationRailBadge Badge { get => GetValue(BadgeProperty); set => SetValue(BadgeProperty, value); }
    public string? BadgeText { get => GetValue(BadgeTextProperty); set => SetValue(BadgeTextProperty, value); }
    public ICommand? Command { get => GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
    public object? CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }
    public Geometry? EffectiveSelectedIcon => _effectiveSelectedIcon;

    /// <summary>Executes <see cref="Command"/> and raises <see cref="Invoked"/>; disabled items do nothing.</summary>
    public void Invoke()
    {
        if (!IsEffectivelyEnabled)
            return;

        var command = Command;
        var parameter = CommandParameter;
        if (command?.CanExecute(parameter) == true)
            command.Execute(parameter);
        Invoked?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Re-evaluates the orientation inherited from the owning rail.</summary>
    internal void RefreshRailState()
    {
        UpdateSelectedIcon();
        UpdateLabel();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateSelectedIcon();
        UpdateLabel();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == OrientationProperty || change.Property == ShowLabelProperty ||
            change.Property == ShowIconProperty || change.Property == ShowFocusIndicatorProperty ||
            change.Property == BadgeProperty || change.Property == BadgeTextProperty)
            UpdateVisualClasses();
        if (change.Property == ContentProperty)
            UpdateLabel();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        // ListBoxItem's PressedMixin owns the :pressed class; this flag only gates activation.
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && IsEffectivelyEnabled)
        {
            _pressed = true;
            Focus(NavigationMethod.Pointer);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_pressed)
            return;

        _pressed = false;
        // A release outside the item cancels activation, matching native button semantics.
        if (IsEffectivelyEnabled && new Rect(Bounds.Size).Contains(e.GetPosition(this)))
            Invoke();
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        _pressed = false;
        base.OnPointerCaptureLost(e);
    }

    private void UpdateSelectedIcon()
    {
        SetAndRaise(EffectiveSelectedIconProperty, ref _effectiveSelectedIcon, SelectedIcon ?? Icon);
        UpdateVisualClasses();
    }

    private void UpdateLabel()
    {
        SetAndRaise(EffectiveLabelProperty, ref _effectiveLabel, Label ?? Content as string);
        UpdateVisualClasses();
    }

    private void UpdateVisualClasses()
    {
        var rail = ItemsControl.ItemsControlFromItemContainer(this) as MaterialNavigationRail;
        var orientation = Orientation;
        if (orientation == MaterialNavigationRailItemOrientation.Auto)
            orientation = rail?.ItemOrientation ?? MaterialNavigationRailItemOrientation.Auto;
        if (orientation == MaterialNavigationRailItemOrientation.Auto)
            orientation = rail?.IsExpanded == true
                ? MaterialNavigationRailItemOrientation.Horizontal
                : MaterialNavigationRailItemOrientation.Vertical;

        var horizontal = orientation == MaterialNavigationRailItemOrientation.Horizontal;
        // Content that is not text (an icon control, a badge, a two-line label block) is shown
        // through the template's ContentPresenter instead of the text label, so the source's
        // slot-style segments keep working instead of disappearing.
        var customContent = Content is not null and not string;
        PseudoClasses.Set(":horizontal", horizontal);
        PseudoClasses.Set(":vertical", !horizontal);
        PseudoClasses.Set(":custom-content", customContent);
        // The source's "Show label" axis only exists on the vertical item: hiding the label
        // collapses the icon pill to the 56 unit circle. An item with no text collapses the
        // same way, so an icon-only destination never reserves an empty 16 unit text row.
        PseudoClasses.Set(":no-label", !customContent && (!ShowLabel || string.IsNullOrEmpty(_effectiveLabel)));
        PseudoClasses.Set(":badge-small", Badge == MaterialNavigationRailBadge.Small);
        PseudoClasses.Set(":badge-large", Badge == MaterialNavigationRailBadge.Large);
        PseudoClasses.Set(":no-icon", !ShowIcon);
        PseudoClasses.Set(":force-focus", ShowFocusIndicator);

        // The badge is drawn, not content, so expose its value through the item's automation
        // status; assistive technology then reads the count together with the destination.
        AutomationProperties.SetItemStatus(this,
            Badge != MaterialNavigationRailBadge.None && !string.IsNullOrEmpty(BadgeText) ? BadgeText : null);
    }
}
