using System;
using System.Globalization;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace ExpressiveAvalonia.Controls;

/// <summary>How a navigation bar destination arranges its icon and label.</summary>
public enum MaterialNavigationBarItemOrientation
{
    /// <summary>Follow the bar: icon beside label in a horizontal bar, stacked in a vertical bar.</summary>
    Auto,

    /// <summary>Icon and label share a 40 unit pill; the label sits to the right of the icon.</summary>
    Horizontal,

    /// <summary>A 56 by 32 icon pill with the label centered beneath it.</summary>
    Vertical
}

/// <summary>The optional count indicator of a navigation bar destination.</summary>
public enum MaterialNavigationBarBadge
{
    None,

    /// <summary>A six unit dot over the top-right of the icon.</summary>
    Small,

    /// <summary>A sixteen unit pill carrying <see cref="MaterialNavigationBarItem.BadgeText"/>.</summary>
    Large
}

/// <summary>
/// One destination of a <see cref="MaterialNavigationBar"/>: an icon, a label and an optional badge.
/// </summary>
/// <remarks>
/// The container is a <see cref="ListBoxItem"/>, so selection, disabled handling, pointer
/// selection, focus and the SelectionItem automation pattern come from Avalonia. The bar
/// supplies the horizontal or vertical anatomy through the <c>:horizontal</c> and
/// <c>:vertical</c> classes; <see cref="Orientation"/> can override it per item.
/// <see cref="Content"/> is the destination label. Activation by pointer, Enter or Space
/// executes <see cref="Command"/> and raises <see cref="Invoked"/>, which the bar forwards
/// as <see cref="MaterialNavigationBar.ItemInvoked"/>.
/// </remarks>
[PseudoClasses(":horizontal", ":vertical", ":no-label", ":badge-small", ":badge-large")]
public class MaterialNavigationBarItem : ListBoxItem
{
    /// <summary>The unselected icon path. Its fill comes from the item theme.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<MaterialNavigationBarItem, Geometry?>(nameof(Icon));

    /// <summary>Optional selected icon path; falls back to <see cref="Icon"/> when unset.</summary>
    public static readonly StyledProperty<Geometry?> SelectedIconProperty =
        AvaloniaProperty.Register<MaterialNavigationBarItem, Geometry?>(nameof(SelectedIcon));

    /// <summary>Overrides the icon/label arrangement inherited from the bar.</summary>
    public static readonly StyledProperty<MaterialNavigationBarItemOrientation> OrientationProperty =
        AvaloniaProperty.Register<MaterialNavigationBarItem, MaterialNavigationBarItemOrientation>(nameof(Orientation));

    /// <summary>Shows the destination label. Without a label the item keeps only its icon pill.</summary>
    public static readonly StyledProperty<bool> ShowLabelProperty =
        AvaloniaProperty.Register<MaterialNavigationBarItem, bool>(nameof(ShowLabel), true);

    /// <summary>The badge treatment shown over the icon.</summary>
    public static readonly StyledProperty<MaterialNavigationBarBadge> BadgeProperty =
        AvaloniaProperty.Register<MaterialNavigationBarItem, MaterialNavigationBarBadge>(nameof(Badge));

    /// <summary>Text of a <see cref="MaterialNavigationBarBadge.Large"/> badge.</summary>
    public static readonly StyledProperty<string?> BadgeTextProperty =
        AvaloniaProperty.Register<MaterialNavigationBarItem, string?>(nameof(BadgeText));

    /// <summary>Invoked when the destination is activated by pointer, Enter or Space.</summary>
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<MaterialNavigationBarItem, ICommand?>(nameof(Command));

    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<MaterialNavigationBarItem, object?>(nameof(CommandParameter));

    /// <summary>The selected icon, or <see cref="Icon"/> when no selected variant was supplied.</summary>
    public static readonly DirectProperty<MaterialNavigationBarItem, Geometry?> EffectiveSelectedIconProperty =
        AvaloniaProperty.RegisterDirect<MaterialNavigationBarItem, Geometry?>(nameof(EffectiveSelectedIcon),
            item => item.EffectiveSelectedIcon);

    /// <summary>The label text taken from <see cref="Content"/>.</summary>
    public static readonly DirectProperty<MaterialNavigationBarItem, string?> LabelTextProperty =
        AvaloniaProperty.RegisterDirect<MaterialNavigationBarItem, string?>(nameof(LabelText),
            item => item.LabelText);

    /// <summary>Whether the text label is painted; false without a label or when labels are off.</summary>
    public static readonly DirectProperty<MaterialNavigationBarItem, bool> IsLabelVisibleProperty =
        AvaloniaProperty.RegisterDirect<MaterialNavigationBarItem, bool>(nameof(IsLabelVisible),
            item => item.IsLabelVisible);

    /// <summary>Whether non-text content is painted in the label slot.</summary>
    public static readonly DirectProperty<MaterialNavigationBarItem, bool> IsContentVisibleProperty =
        AvaloniaProperty.RegisterDirect<MaterialNavigationBarItem, bool>(nameof(IsContentVisible),
            item => item.IsContentVisible);

    private Geometry? _effectiveSelectedIcon;
    private string? _labelText;
    private bool _isLabelVisible;
    private bool _isContentVisible;
    private bool _pressed;
    private string? _appliedAutomationName;

    static MaterialNavigationBarItem()
    {
        IconProperty.Changed.AddClassHandler<MaterialNavigationBarItem>((item, _) => item.UpdateSelectedIcon());
        SelectedIconProperty.Changed.AddClassHandler<MaterialNavigationBarItem>((item, _) => item.UpdateSelectedIcon());
    }

    /// <summary>Raised when the destination is activated; the owning bar forwards it.</summary>
    public event EventHandler? Invoked;

    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public Geometry? SelectedIcon { get => GetValue(SelectedIconProperty); set => SetValue(SelectedIconProperty, value); }
    public MaterialNavigationBarItemOrientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }
    public bool ShowLabel { get => GetValue(ShowLabelProperty); set => SetValue(ShowLabelProperty, value); }
    public MaterialNavigationBarBadge Badge { get => GetValue(BadgeProperty); set => SetValue(BadgeProperty, value); }
    public string? BadgeText { get => GetValue(BadgeTextProperty); set => SetValue(BadgeTextProperty, value); }
    public ICommand? Command { get => GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
    public object? CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }
    public Geometry? EffectiveSelectedIcon => _effectiveSelectedIcon;
    public string? LabelText => _labelText;
    public bool IsLabelVisible => _isLabelVisible;
    public bool IsContentVisible => _isContentVisible;

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

    /// <summary>Re-evaluates the orientation inherited from the owning bar.</summary>
    internal void RefreshBarState()
    {
        UpdateSelectedIcon();
        UpdateContentVisibility();
        UpdateVisualClasses();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateSelectedIcon();
        UpdateContentVisibility();
        UpdateVisualClasses();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ContentProperty)
            UpdateLabelText();
        if (change.Property == ShowLabelProperty)
            UpdateContentVisibility();
        if (change.Property == OrientationProperty ||
            change.Property == BadgeProperty || change.Property == BadgeTextProperty)
            UpdateVisualClasses();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && IsEffectivelyEnabled)
        {
            _pressed = true;
            PseudoClasses.Set(":pressed", true);
            Focus(NavigationMethod.Pointer);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_pressed)
            return;

        _pressed = false;
        PseudoClasses.Set(":pressed", false);
        if (IsEffectivelyEnabled && new Rect(Bounds.Size).Contains(e.GetPosition(this)))
            Invoke();
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        _pressed = false;
        PseudoClasses.Set(":pressed", false);
        base.OnPointerCaptureLost(e);
    }

    private void UpdateLabelText()
    {
        // Only text content doubles as the accessible destination name; rich content keeps
        // whatever name the consumer supplied.
        var text = Content as string;
        if (text == _labelText)
            return;

        SetAndRaise(LabelTextProperty, ref _labelText, text);
        UpdateContentVisibility();

        // Destination labels are the accessible name; keep it in sync unless the consumer
        // supplied an explicit name of their own.
        var current = AutomationProperties.GetName(this);
        if (string.IsNullOrEmpty(current) || current == _appliedAutomationName)
        {
            _appliedAutomationName = text ?? string.Empty;
            AutomationProperties.SetName(this, _appliedAutomationName);
        }
    }

    private void UpdateContentVisibility()
    {
        var labeled = !string.IsNullOrEmpty(_labelText);
        PseudoClasses.Set(":no-label", !ShowLabel);
        SetAndRaise(IsLabelVisibleProperty, ref _isLabelVisible, ShowLabel && labeled);
        SetAndRaise(IsContentVisibleProperty, ref _isContentVisible, ShowLabel && !labeled);
    }

    private void UpdateSelectedIcon()
    {
        SetAndRaise(EffectiveSelectedIconProperty, ref _effectiveSelectedIcon, SelectedIcon ?? Icon);
        UpdateVisualClasses();
    }

    // The badge is decorative for pointer input but its count is meaningful to a screen reader.
    private void UpdateBadgeAutomation()
    {
        string? help = null;
        if (Badge == MaterialNavigationBarBadge.Large && !string.IsNullOrWhiteSpace(BadgeText))
        {
            var format = Resource("MaterialNavigationBarBadgeHelpText", "{0} new items");
            help = string.Format(CultureInfo.CurrentCulture, format, BadgeText);
        }
        else if (Badge == MaterialNavigationBarBadge.Small)
            help = Resource("MaterialNavigationBarBadgeDotText", "New items");

        AutomationProperties.SetHelpText(this, help ?? string.Empty);
    }

    private string Resource(string key, string fallback) =>
        this.TryFindResource(key, out var value) && value is string text ? text : fallback;

    private void UpdateVisualClasses()
    {
        var bar = ItemsControl.ItemsControlFromItemContainer(this) as MaterialNavigationBar;
        var orientation = Orientation;
        if (orientation == MaterialNavigationBarItemOrientation.Auto)
            orientation = bar?.ResolveItemOrientation() ?? MaterialNavigationBarItemOrientation.Horizontal;

        var horizontal = orientation == MaterialNavigationBarItemOrientation.Horizontal;
        PseudoClasses.Set(":horizontal", horizontal);
        PseudoClasses.Set(":vertical", !horizontal);
        PseudoClasses.Set(":no-label", !ShowLabel);
        PseudoClasses.Set(":badge-small", Badge == MaterialNavigationBarBadge.Small);
        PseudoClasses.Set(":badge-large", Badge == MaterialNavigationBarBadge.Large);
        UpdateBadgeAutomation();
    }
}
