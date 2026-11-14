using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Data;
using Avalonia.Styling;
using ExpressiveAvalonia.Theming;
using ExpressiveAvalonia.Tokens;

namespace ExpressiveAvalonia.Controls;

/// <summary>The four source sizes of the regular FAB.</summary>
public enum MaterialFabSize
{
    /// <summary>40 unit square, 12 unit radius, 24 unit icon (source "Small").</summary>
    Small,

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

/// <summary>The two source anatomies of the extended FAB.</summary>
public enum MaterialExtendedFabMode
{
    /// <summary>
    /// The Material 3 Expressive extended FAB: the <see cref="MaterialExtendedFabSize"/> picks the
    /// container height, padding, corner radius and typography (source Small/Medium/Large
    /// overloads).
    /// </summary>
    Expressive,

    /// <summary>
    /// The baseline extended FAB: 56 units high, 16 unit radius, 24 unit icon, LabelLarge text,
    /// 16 leading and 20 trailing padding, a 12 unit icon gap and an 80 unit minimum width while
    /// expanded (source <c>ExtendedFloatingActionButton</c> overloads).
    /// </summary>
    Classic
}

/// <summary>
/// A Material 3 FAB: a 40, 56, 80 or 96 unit container carrying a single icon.
/// </summary>
/// <remarks>
/// <para>
/// Source: FAB (local-001450). All source variants are the cross product of
/// <see cref="MaterialFabSize"/> (Small, Default, Medium, Large), <see cref="MaterialFabBase.ColorRole"/>
/// (six roles) and the four interaction states, which the control renders with native
/// <c>:pointerover</c>, <c>:focus-visible</c> and <c>:pressed</c> states.
/// </para>
/// <para>
/// <see cref="Content"/> is the icon; a <see cref="PathIcon"/> picks up the role's content
/// color and is drawn at the source's 24, 24, 28 or 36 unit glyph size. The control is a
/// <see cref="Button"/>, so <see cref="Button.Command"/>, access keys and Enter/Space activation
/// are native. Set <c>AutomationProperties.Name</c> on icon-only FABs.
/// </para>
/// </remarks>
public class MaterialFab : MaterialFabBase
{
    /// <summary>Defines the <see cref="Size"/> property.</summary>
    public static readonly StyledProperty<MaterialFabSize> SizeProperty =
        AvaloniaProperty.Register<MaterialFab, MaterialFabSize>(nameof(Size));

    private IDisposable? _iconTheme;
    private IDisposable? _iconWidth;
    private IDisposable? _iconHeight;

    static MaterialFab()
    {
        ContentProperty.Changed.AddClassHandler<MaterialFab>((fab, _) => fab.UpdateContentIcon());
        SizeProperty.Changed.AddClassHandler<MaterialFab>((fab, _) => fab.UpdateContentIcon());
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateContentIcon();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        ClearContentIcon();
        base.OnDetachedFromVisualTree(e);
    }

    private void ClearContentIcon()
    {
        _iconTheme?.Dispose();
        _iconWidth?.Dispose();
        _iconHeight?.Dispose();
        _iconTheme = _iconWidth = _iconHeight = null;
    }

    private void UpdateContentIcon()
    {
        ClearContentIcon();
        if (Content is not PathIcon icon || !this.IsAttachedToVisualTree())
            return;

        if (this.TryFindResource("MaterialFabPathIconTheme", out var resource) && resource is ControlTheme theme)
            _iconTheme = icon.SetValue(ThemeProperty, theme, BindingPriority.Style);

        var size = Size switch { MaterialFabSize.Medium => 28d, MaterialFabSize.Large => 36d, _ => 24d };
        _iconWidth = icon.SetValue(WidthProperty, size, BindingPriority.Style);
        _iconHeight = icon.SetValue(HeightProperty, size, BindingPriority.Style);
    }

    /// <summary>Gets or sets the source FAB size.</summary>
    public MaterialFabSize Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }
}

/// <summary>
/// A Material 3 extended FAB: an icon plus a label, optionally collapsing to the icon.
/// </summary>
/// <remarks>
/// <para>
/// Source: Extended FAB (local-001377) and the baseline <c>ExtendedFloatingActionButton</c>
/// overloads (local-001377). <see cref="Mode"/> selects the expressive Small/Medium/Large
/// anatomy (the 72 source variants that cross <see cref="MaterialExtendedFabSize"/>, the six
/// <see cref="MaterialFabBase.ColorRole"/> values and the four interaction states) or the
/// classic 56 unit anatomy.
/// </para>
/// <para>
/// The label is <see cref="Label"/> text. Setting <see cref="ContentControl.Content"/> (with an
/// optional <see cref="ContentControl.ContentTemplate"/>) replaces the text with custom content,
/// matching the source text-only overload that accepts arbitrary row content; <see cref="Label"/>
/// then still supplies the accessible name. Both anatomies support an icon-less FAB: the classic
/// text-only overload is 80 wide at minimum with 20 units of horizontal padding and never
/// collapses, while the expressive sizes keep their icon gap and padding.
/// </para>
/// <para>
/// <see cref="IsExpanded"/> reproduces the Material collapsing behavior: when false the label
/// host animates to zero width and the container becomes a square icon-only FAB, while the
/// accessible name stays <see cref="Label"/> so screen readers still announce the action.
/// Container padding, the icon offset and the label clip are all driven by one interruptible
/// spring, so the total width moves continuously instead of jumping between template paddings.
/// The expressive sizes use FastSpatial for the width and FastEffects for the label alpha; the
/// classic mode follows the source <c>AnimatedVisibility</c> enter/exit pair: FastSpatial width
/// and DefaultEffects alpha when expanding, DefaultSpatial width and FastEffects alpha when
/// collapsing. <c>MaterialTheme</c> snaps the whole transition under reduced motion, and a
/// detach or reattach reconciles the painted width and alpha to the current target.
/// </para>
/// </remarks>
public class MaterialExtendedFab : MaterialFabBase
{
    /// <summary>Defines the <see cref="Size"/> property.</summary>
    public static readonly StyledProperty<MaterialExtendedFabSize> SizeProperty =
        AvaloniaProperty.Register<MaterialExtendedFab, MaterialExtendedFabSize>(nameof(Size));

    /// <summary>Defines the <see cref="Mode"/> property.</summary>
    public static readonly StyledProperty<MaterialExtendedFabMode> ModeProperty =
        AvaloniaProperty.Register<MaterialExtendedFab, MaterialExtendedFabMode>(nameof(Mode));

    /// <summary>Defines the <see cref="Icon"/> property.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<MaterialExtendedFab, Geometry?>(nameof(Icon));

    /// <summary>Defines the <see cref="Label"/> property.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<MaterialExtendedFab, string?>(nameof(Label));

    /// <summary>Defines the <see cref="IsExpanded"/> property.</summary>
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<MaterialExtendedFab, bool>(nameof(IsExpanded), true);

    /// <summary>The animated opacity of the label host, 0 while fully collapsed.</summary>
    public static readonly StyledProperty<double> LabelOpacityProperty =
        AvaloniaProperty.Register<MaterialExtendedFab, double>(nameof(LabelOpacity), 1d);

    // Unit progress springs: the width domain is 0 (collapsed) .. 1 (expanded), so the padding,
    // minimum width, label clip and icon offset all follow the same curve.
    private const double ProgressThreshold = 0.001;

    private Border? _labelHost;
    private TextBlock? _labelText;
    private ContentPresenter? _labelContent;
    private PathIcon? _icon;
    private StackPanel? _contentRow;
    private double _measuredLabelWidth;
    private MaterialMotion.SpringAnimation? _widthSpring;
    private MaterialMotion.SpringAnimation? _opacitySpring;
    private MaterialSpring? _widthSpringSpec;
    private MaterialSpring? _opacitySpringSpec;
    private double _widthProgress;
    private double _opacityProgress;
    private double _widthTarget = double.NaN;
    private (string? Text, object? Content, object? ContentTemplate, double FontSize, FontWeight Weight,
        FontFamily? Family, double Spacing, MaterialExtendedFabMode Mode, MaterialExtendedFabSize Size,
        bool HasIcon) _signature;

    static MaterialExtendedFab()
    {
        SizeProperty.Changed.AddClassHandler<MaterialExtendedFab>((fab, _) => fab.RefreshGeometry());
        ModeProperty.Changed.AddClassHandler<MaterialExtendedFab>((fab, _) => fab.RefreshGeometry());
        IconProperty.Changed.AddClassHandler<MaterialExtendedFab>((fab, _) =>
        {
            fab.UpdateLabelVisibility();
            fab.RefreshGeometry();
        });
        LabelProperty.Changed.AddClassHandler<MaterialExtendedFab>((fab, _) =>
        {
            fab.SyncAutomationName(fab.Label);
            fab.UpdateLabelWidth();
            fab.ApplyExpansion();
        });
        ContentProperty.Changed.AddClassHandler<MaterialExtendedFab>((fab, _) =>
        {
            fab.UpdateLabelVisibility();
            fab.UpdateLabelWidth();
            fab.ApplyExpansion();
        });
        ContentTemplateProperty.Changed.AddClassHandler<MaterialExtendedFab>((fab, _) =>
        {
            fab.UpdateLabelWidth();
            fab.ApplyExpansion();
        });
        IsExpandedProperty.Changed.AddClassHandler<MaterialExtendedFab>((fab, e) =>
            fab.OnIsExpandedChanged((bool)e.NewValue!));
    }

    /// <summary>Gets or sets the source extended FAB size; ignored by the classic mode.</summary>
    public MaterialExtendedFabSize Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    /// <summary>Gets or sets the expressive or classic extended FAB anatomy.</summary>
    public MaterialExtendedFabMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
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

    /// <summary>Gets the animated label opacity, 0 while fully collapsed.</summary>
    public double LabelOpacity
    {
        get => GetValue(LabelOpacityProperty);
        private set => SetValue(LabelOpacityProperty, value);
    }

    // A classic FAB without an icon is the source text-only overload, which has no collapsed form.
    private bool IsCollapsible => Mode is MaterialExtendedFabMode.Expressive || Icon is not null;

    // Source motions. Classic AnimatedVisibility: expandHorizontally(FastSpatial) +
    // fadeIn(DefaultEffects) on enter, shrinkHorizontally(DefaultSpatial) + fadeOut(FastEffects)
    // on exit. The expressive overload always uses FastSpatial width and FastEffects alpha.
    private MaterialSpring WidthSpec => Mode is MaterialExtendedFabMode.Classic
        ? IsExpanded ? MaterialSpring.SpatialFast : MaterialSpring.SpatialDefault
        : MaterialSpring.SpatialFast;

    private MaterialSpring OpacitySpec => Mode is MaterialExtendedFabMode.Classic
        ? IsExpanded ? MaterialSpring.EffectsDefault : MaterialSpring.EffectsFast
        : MaterialSpring.EffectsFast;

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _labelHost = e.NameScope.Find<Border>("PART_LabelHost");
        _labelText = e.NameScope.Find<TextBlock>("PART_Label");
        _labelContent = e.NameScope.Find<ContentPresenter>("PART_LabelContent");
        _icon = e.NameScope.Find<PathIcon>("PART_Icon");
        _contentRow = e.NameScope.Find<StackPanel>("PART_ContentRow");
        UpdateLabelVisibility();
        SyncAutomationName(Label);
        _signature = default;
        UpdateCollapsedPseudo(IsExpanded);
        UpdateLabelWidth();
        ApplyExpansion(animate: false);

        // The label's themed font is resolved during the first layout pass, so measure again
        // once it is available instead of freezing the default typeface metrics.
        Dispatcher.UIThread.Post(() =>
        {
            UpdateLabelWidth();
            ApplyExpansion();
        }, DispatcherPriority.Loaded);
        LayoutUpdated -= OnLayoutUpdated;
        LayoutUpdated += OnLayoutUpdated;
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // Reconcile with the current state instead of resuming a flight that was cut by the
        // detach; the painted width and alpha must never keep a stale partial label.
        UpdateLabelWidth();
        ApplyExpansion(animate: false);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        DisposeExpansionSprings();
        ApplyExpansion(animate: false);
        base.OnDetachedFromVisualTree(e);
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        UpdateLabelWidth();
        ApplyExpansion();
    }

    private void RefreshGeometry()
    {
        UpdateLabelVisibility();
        UpdateCollapsedPseudo(IsExpanded);
        UpdateLabelWidth();
        ApplyExpansion();
    }

    private void OnIsExpandedChanged(bool expanded)
    {
        UpdateCollapsedPseudo(expanded);
        ApplyExpansion();
    }

    private void UpdateCollapsedPseudo(bool expanded) =>
        PseudoClasses.Set(":collapsed", !expanded && IsCollapsible);

    private void UpdateLabelVisibility()
    {
        if (_labelText is not null)
            _labelText.IsVisible = Content is null;
        if (_labelContent is not null)
            _labelContent.IsVisible = Content is not null;
        if (_icon is not null)
            _icon.IsVisible = Icon is not null;
    }

    private void UpdateLabelWidth()
    {
        if (_labelText is null)
            return;

        var signature = (Label, Content, (object?)ContentTemplate, _labelText.FontSize,
            _labelText.FontWeight, _labelText.FontFamily, _labelText.LetterSpacing, Mode, Size,
            Icon is not null);
        if (signature == _signature)
            return;

        _signature = signature;

        if (Content is not null)
        {
            if (_labelContent is null || !Equals(_labelContent.Content, Content))
            {
                // The template binding has not reached the presenter yet; let the next layout
                // pass measure again with the bound content.
                _signature = default;
                return;
            }

            _measuredLabelWidth = MeasureContentWidth();
            return;
        }

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

    private double MeasureContentWidth()
    {
        if (_labelContent is null)
            return 0;

        _labelContent.Measure(Avalonia.Size.Infinity);
        return Math.Ceiling(_labelContent.DesiredSize.Width);
    }

    /// <summary>
    /// Drives the collapse/expand progress. The width and the label alpha are separate springs
    /// because the source uses different motion specs per property and direction; when the
    /// direction flips mid-flight the spec changes, so the spring is rebuilt from its sampled
    /// position and velocity to keep a reversal continuous.
    /// </summary>
    private void ApplyExpansion(bool animate = true)
    {
        if (_labelHost is null || _contentRow is null)
            return;

        var target = IsCollapsible && !IsExpanded ? 0d : 1d;
        if (!animate || MaterialMotion.GetIsReduced(this) || !this.IsAttachedToVisualTree())
        {
            DisposeExpansionSprings();
            _widthTarget = target;
            _widthProgress = target;
            _opacityProgress = target;
            Paint();
            return;
        }

        if (!double.IsNaN(_widthTarget) && Math.Abs(_widthTarget - target) < 0.001)
        {
            // Same target: keep the running trajectory and only repaint the geometry that the
            // current size, mode, icon or label measurement changed.
            Paint();
            return;
        }

        _widthTarget = target;
        StepWidthSpring(target);
        StepOpacitySpring(target);
        Paint();
    }

    private void StepWidthSpring(double target)
    {
        var spec = WidthSpec;
        if (_widthSpring is null || _widthSpringSpec != spec)
        {
            var position = _widthProgress;
            var velocity = 0d;
            if (_widthSpring is { } previous)
            {
                (position, velocity) = previous.SampleState();
                previous.Dispose();
                _widthProgress = position;
            }

            _widthSpringSpec = spec;
            _widthSpring = MaterialMotion.Animate(this, position, target, value =>
            {
                _widthProgress = value;
                Paint();
            }, spec, ProgressThreshold, velocity);
            return;
        }

        _widthSpring.Retarget(target);
    }

    private void StepOpacitySpring(double target)
    {
        var spec = OpacitySpec;
        if (_opacitySpring is null || _opacitySpringSpec != spec)
        {
            var position = _opacityProgress;
            var velocity = 0d;
            if (_opacitySpring is { } previous)
            {
                (position, velocity) = previous.SampleState();
                previous.Dispose();
                _opacityProgress = position;
            }

            _opacitySpringSpec = spec;
            _opacitySpring = MaterialMotion.Animate(this, position, target, value =>
            {
                _opacityProgress = value;
                Paint();
            }, spec, ProgressThreshold, velocity);
            return;
        }

        _opacitySpring.Retarget(target);
    }

    private void Paint()
    {
        if (_labelHost is null || _contentRow is null || _labelText is null)
            return;

        var geometry = ComputeGeometry();
        var width = Math.Clamp(_widthProgress, 0, 1);
        var opacity = Math.Clamp(_opacityProgress, 0, 1);

        var leading = Lerp(geometry.CollapsedLeading, geometry.ExpandedLeading, width);
        var trailing = Lerp(geometry.CollapsedTrailing, geometry.ExpandedTrailing, width);
        var vertical = Lerp(geometry.CollapsedVertical, geometry.ExpandedVertical, width);
        var hostWidth = geometry.HostWidth * width;
        var gap = new Thickness(geometry.Gap, 0, 0, 0);

        _contentRow.HorizontalAlignment = Mode is MaterialExtendedFabMode.Classic
            ? Avalonia.Layout.HorizontalAlignment.Left
            : Avalonia.Layout.HorizontalAlignment.Center;
        _contentRow.MinWidth = Lerp(geometry.CollapsedRowWidth, geometry.ExpandedRowWidth, width);

        _labelText.Width = _measuredLabelWidth;
        _labelText.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
        if (_labelContent is not null)
        {
            _labelContent.Width = _measuredLabelWidth;
            _labelContent.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
        }
        if (_labelText.Margin != gap)
            _labelText.Margin = gap;
        if (_labelContent is not null && _labelContent.Margin != gap)
            _labelContent.Margin = gap;

        if (Math.Abs(_labelHost.Width - hostWidth) > 0.001)
            _labelHost.Width = Math.Max(0, hostWidth);

        SetCurrentValue(LabelOpacityProperty, opacity);
        SetCurrentValue(PaddingProperty, new Thickness(leading, vertical, trailing, vertical));
    }

    private ExpansionGeometry ComputeGeometry()
    {
        var label = _measuredLabelWidth;
        if (Mode is MaterialExtendedFabMode.Classic)
        {
            // Baseline ExtendedFabPrimaryTokens: 56 high, 24 icon, LabelLarge, 16 leading and
            // 20 trailing, 12 icon gap, ExtendedFabMinimumWidth 80. Collapsed keeps the same
            // leading padding, so the icon is centred in the 56 square exactly like the source's
            // Arrangement.Center with zero padding.
            return Icon is not null
                ? new ExpansionGeometry(label + 12, 12, 16, 20, 16, 16, 16, 16, 24, 44)
                : new ExpansionGeometry(label, 0, 20, 20, 20, 20, 16, 16, 40, 40);
        }

        // Expressive: LeadingSpace/TrailingSpace and IconLabelSpace per size, with the collapsed
        // padding that makes the icon-only container exactly square.
        var (padding, gap, collapsed) = Size switch
        {
            MaterialExtendedFabSize.Small => (16d, 8d, 16d),
            MaterialExtendedFabSize.Medium => (26d, 12d, 26d),
            _ => (28d, 16d, 30d)
        };
        return new ExpansionGeometry(label + gap, gap, padding, padding, collapsed, collapsed,
            padding, collapsed, 0, 0);
    }

    private void DisposeExpansionSprings()
    {
        _widthSpring?.Dispose();
        _widthSpring = null;
        _widthSpringSpec = null;
        _opacitySpring?.Dispose();
        _opacitySpring = null;
        _opacitySpringSpec = null;
    }

    private static double Lerp(double from, double to, double progress) =>
        from + (to - from) * progress;

    /// <summary>
    /// The measured geometry of one painted frame. <see cref="HostWidth"/> excludes the container
    /// padding, <see cref="Gap"/> is the icon-to-label space inside the clipped label host, and
    /// the row widths are the content-row minimums that hold the classic 80/56 container widths.
    /// </summary>
    private readonly record struct ExpansionGeometry(
        double HostWidth,
        double Gap,
        double ExpandedLeading,
        double ExpandedTrailing,
        double CollapsedLeading,
        double CollapsedTrailing,
        double ExpandedVertical,
        double CollapsedVertical,
        double CollapsedRowWidth,
        double ExpandedRowWidth);
}