using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Threading;

namespace ExpressiveAvalonia.Controls;

/// <summary>The three source sizes of the regular FAB.</summary>
public enum MaterialFabSize
{
    /// <summary>56 unit square, 16 unit radius, 24 unit icon (source "Default").</summary>
    Default,

    /// <summary>80 unit square, 20 unit radius, 28 unit icon.</summary>
    Medium,

    /// <summary>96 unit square, 28 unit radius, 36 unit icon.</summary>
    Large
}

/// <summary>The three source sizes of the extended FAB.</summary>
public enum MaterialExtendedFabSize
{
    /// <summary>56 unit high, 16 unit radius, 24 unit icon, 16/24 label.</summary>
    Small,

    /// <summary>80 unit high, 20 unit radius, 28 unit icon, 22/28 label.</summary>
    Medium,

    /// <summary>96 unit high, 28 unit radius, 36 unit icon, 24/32 label.</summary>
    Large
}

/// <summary>
/// A Material 3 FAB: a 56, 80 or 96 unit container carrying a single icon.
/// </summary>
/// <remarks>
/// <para>
/// Source: FAB (local-001450). All 72 source variants are the cross product of
/// <see cref="MaterialFabSize"/> (Default, Medium, Large), <see cref="MaterialFabBase.ColorRole"/>
/// (six roles) and the four interaction states, which the control renders with native
/// <c>:pointerover</c>, <c>:focus-visible</c> and <c>:pressed</c> states.
/// </para>
/// <para>
/// <see cref="Content"/> is the icon; a <see cref="PathIcon"/> picks up the role's content
/// color and is drawn at the source's 20, 23 or 30 unit glyph size inside its 24, 28 or 36
/// unit icon box. The control is a <see cref="Button"/>, so <see cref="Button.Command"/>,
/// access keys and Enter/Space activation are native. Set <c>AutomationProperties.Name</c>
/// on icon-only FABs.
/// </para>
/// </remarks>
public class MaterialFab : MaterialFabBase
{
    /// <summary>Defines the <see cref="Size"/> property.</summary>
    public static readonly StyledProperty<MaterialFabSize> SizeProperty =
        AvaloniaProperty.Register<MaterialFab, MaterialFabSize>(nameof(Size));

    /// <summary>Gets or sets the source FAB size.</summary>
    public MaterialFabSize Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }
}

/// <summary>
/// A Material 3 extended FAB: an icon plus a text label, optionally collapsing to the icon.
/// </summary>
/// <remarks>
/// <para>
/// Source: Extended FAB (local-001377). The 72 source variants are the cross product of
/// <see cref="MaterialExtendedFabSize"/> (Small, Medium, Large), the six
/// <see cref="MaterialFabBase.ColorRole"/> values and the four interaction states.
/// </para>
/// <para>
/// <see cref="IsExpanded"/> reproduces the Material collapsing behavior: when false the label
/// host animates to zero width and the container becomes a square icon-only FAB, while the
/// accessible name stays <see cref="Label"/> so screen readers still announce the action.
/// The width transition uses the theme's short motion duration, which <c>MaterialTheme</c>
/// zeroes under reduced motion.
/// </para>
/// </remarks>
public class MaterialExtendedFab : MaterialFabBase
{
    /// <summary>Defines the <see cref="Size"/> property.</summary>
    public static readonly StyledProperty<MaterialExtendedFabSize> SizeProperty =
        AvaloniaProperty.Register<MaterialExtendedFab, MaterialExtendedFabSize>(nameof(Size));

    /// <summary>Defines the <see cref="Icon"/> property.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<MaterialExtendedFab, Geometry?>(nameof(Icon));

    /// <summary>Defines the <see cref="Label"/> property.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<MaterialExtendedFab, string?>(nameof(Label));

    /// <summary>Defines the <see cref="IsExpanded"/> property.</summary>
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<MaterialExtendedFab, bool>(nameof(IsExpanded), true);

    private Border? _labelHost;
    private TextBlock? _labelText;
    private PathIcon? _icon;
    private double _measuredLabelWidth;
    private (string? Text, double FontSize, FontWeight Weight, FontFamily? Family, double Spacing, Thickness Margin) _signature;

    static MaterialExtendedFab()
    {
        SizeProperty.Changed.AddClassHandler<MaterialExtendedFab>((fab, _) => fab.ApplyLabelWidth());
        IconProperty.Changed.AddClassHandler<MaterialExtendedFab>((fab, _) =>
        {
            if (fab._icon is not null)
                fab._icon.IsVisible = fab.Icon is not null;
        });
        LabelProperty.Changed.AddClassHandler<MaterialExtendedFab>((fab, _) =>
        {
            fab.SyncAutomationName(fab.Label);
            fab.UpdateLabelWidth();
            fab.ApplyLabelWidth();
        });
        IsExpandedProperty.Changed.AddClassHandler<MaterialExtendedFab>((fab, e) =>
            fab.OnIsExpandedChanged((bool)e.NewValue!));
    }

    /// <summary>Gets or sets the source extended FAB size.</summary>
    public MaterialExtendedFabSize Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    /// <summary>Gets or sets the icon geometry, drawn in the source icon box.</summary>
    public Geometry? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>Gets or sets the label text; also the accessible name.</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Gets or sets whether the label is visible.</summary>
    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _labelHost = e.NameScope.Find<Border>("PART_LabelHost");
        _labelText = e.NameScope.Find<TextBlock>("PART_Label");
        _icon = e.NameScope.Find<PathIcon>("PART_Icon");
        if (_icon is not null)
            _icon.IsVisible = Icon is not null;
        SyncAutomationName(Label);
        _signature = default;
        PseudoClasses.Set(":collapsed", !IsExpanded);
        UpdateLabelWidth();
        ApplyLabelWidth();

        // The label's themed font is resolved during the first layout pass, so measure again
        // once it is available instead of freezing the default typeface metrics.
        Dispatcher.UIThread.Post(() =>
        {
            UpdateLabelWidth();
            ApplyLabelWidth();
        }, DispatcherPriority.Loaded);
        LayoutUpdated -= OnLayoutUpdated;
        LayoutUpdated += OnLayoutUpdated;
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        UpdateLabelWidth();
        ApplyLabelWidth();
    }

    private void OnIsExpandedChanged(bool expanded)
    {
        PseudoClasses.Set(":collapsed", !expanded);
        ApplyLabelWidth();
    }

    private void UpdateLabelWidth()
    {
        if (_labelText is null)
            return;

        var signature = (Label, _labelText.FontSize, _labelText.FontWeight, _labelText.FontFamily,
            _labelText.LetterSpacing, _labelText.Margin);
        if (signature == _signature)
            return;

        _signature = signature;
        if (string.IsNullOrEmpty(Label))
        {
            _measuredLabelWidth = 0;
            return;
        }

        var probe = new TextBlock
        {
            Text = Label,
            FontFamily = _labelText.FontFamily,
            FontSize = _labelText.FontSize,
            FontStyle = _labelText.FontStyle,
            FontWeight = _labelText.FontWeight,
            LetterSpacing = _labelText.LetterSpacing,
            TextWrapping = TextWrapping.NoWrap
        };
        probe.Measure(Avalonia.Size.Infinity);
        _measuredLabelWidth = Math.Ceiling(probe.DesiredSize.Width);
    }

    private void ApplyLabelWidth()
    {
        if (_labelHost is null)
            return;

        var margins = _labelText?.Margin ?? default;
        var expanded = _measuredLabelWidth + margins.Left + margins.Right;
        var target = IsExpanded && expanded > 0 ? expanded : 0;
        if (!double.IsNaN(_labelHost.Width) && Math.Abs(_labelHost.Width - target) < 0.01)
            return;

        _labelHost.Width = target;
    }
}
