using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Theming;
using ExpressiveAvalonia.Tokens;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// One day inside a <see cref="MaterialDatePicker"/> grid. The picker owns the state:
/// selection, today, out-of-month and range pseudo-classes are applied by the picker so the
/// control theme can style every combination without duplicating layout in code.
/// </summary>
/// <remarks>
/// <para>Cells are focusable but not individual tab stops. The picker is the single tab stop and
/// forwards focus to the active cell (roving focus), which keeps Tab navigation usable while
/// still letting pointer and screen-reader users activate any day.</para>
/// <para>The theme keeps owning every colour target, as in the rest of the library: the
/// <c>:selected</c>/<c>:disabled</c> styles write <see cref="TemplatedControl.Background"/> for the
/// day container and <see cref="TemplatedControl.Foreground"/> for the content. This cell springs
/// those two channels with the source <c>DefaultEffects</c> spec (DatePicker.kt
/// <c>dayContentColor</c>/<c>dayContainerColor</c>) and paints the template parts, so an
/// intermediate render colour is visible while the selection moves.</para>
/// </remarks>
public sealed class MaterialDateCell : Button
{
    private readonly MaterialDateColorAnimation _colors;

    public MaterialDateCell()
    {
        Focusable = true;
        IsTabStop = false;
        FocusAdorner = null;
        _colors = new MaterialDateColorAnimation(this, GetColorAnimationEligibility);
    }

    /// <summary>The month block this cell belongs to. Leading and trailing days are dimmed.</summary>
    internal DateTime ContextMonth { get; set; }

    internal void SetState(string name, bool value)
    {
        if (PseudoClasses.Contains(name) == value)
            return;

        PseudoClasses.Set(name, value);
        // Range classes do not always change a colour themselves (the start of a completed range
        // keeps the selected colours), but they switch the source between the animated
        // dayContentColor and the immediate dayInSelectionRangeContentColor state.
        _colors.Refresh();
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _colors.Bind(
            e.NameScope.Find<ContentPresenter>("PART_ContentPresenter"),
            e.NameScope.Find<Border>("Selection"));
    }

    /// <summary>
    /// DatePicker.kt dayContentColor animates unless <c>inRange</c>, where it uses
    /// <c>rememberUpdatedState</c>; dayContainerColor animates only when <c>animateChecked</c>,
    /// which the source sets to <c>startDateSelected</c> (the end date snaps behind the range
    /// marker).
    /// </summary>
    private (bool Content, bool Container) GetColorAnimationEligibility()
    {
        var immediateContent =
            PseudoClasses.Contains(":range-start") ||
            PseudoClasses.Contains(":range-end") ||
            PseudoClasses.Contains(":range-middle") ||
            // Range preview is a library extension of the range marker; it keeps the same
            // immediate content distinction as the completed range.
            PseudoClasses.Contains(":range-preview-start") ||
            PseudoClasses.Contains(":range-preview-end") ||
            PseudoClasses.Contains(":range-preview-middle");
        var animateContainer =
            PseudoClasses.Contains(":selected") && !PseudoClasses.Contains(":range-end");
        return (!immediateContent, animateContainer);
    }
}

internal interface IMaterialDateChoiceCell
{
    void SetSelected(bool selected);

    void SetCurrent(bool current);
}

/// <summary>A month or year chip used by the month and year views of a date picker.</summary>
/// <remarks>
/// The chip container and label spring with the source <c>DefaultEffects</c> spec
/// (DatePicker.kt <c>yearContainerColor</c>/<c>yearContentColor</c>); the theme supplies the
/// targets through <see cref="TemplatedControl.Background"/> and
/// <see cref="TemplatedControl.Foreground"/>.
/// </remarks>
public sealed class MaterialDateChoiceChip : Button, IMaterialDateChoiceCell
{
    private readonly MaterialDateColorAnimation _colors;

    public MaterialDateChoiceChip()
    {
        Focusable = true;
        IsTabStop = false;
        FocusAdorner = null;
        _colors = new MaterialDateColorAnimation(this, YearColorAnimationEligibility);
    }

    void IMaterialDateChoiceCell.SetSelected(bool selected) =>
        SetYearState(":selected", selected);

    void IMaterialDateChoiceCell.SetCurrent(bool current) =>
        SetYearState(":current", current);

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _colors.Bind(
            e.NameScope.Find<ContentPresenter>("PART_ContentPresenter"),
            e.NameScope.Find<Border>("Surface"));
    }

    private void SetYearState(string name, bool value)
    {
        if (PseudoClasses.Contains(name) == value)
            return;

        PseudoClasses.Set(name, value);
        _colors.Refresh();
    }

    // yearContentColor and yearContainerColor always run through animateColorAsState.
    private static (bool Content, bool Container) YearColorAnimationEligibility() => (true, true);
}

/// <summary>A full-width month or year row used by the docked (desktop) date picker list.</summary>
/// <remarks>
/// Same source colour animation as <see cref="MaterialDateChoiceChip"/>: the docked row uses the
/// secondary-container selection pair, but the content and container channels still animate
/// independently with <c>DefaultEffects</c>.
/// </remarks>
public sealed class MaterialDateChoiceRow : Button, IMaterialDateChoiceCell
{
    private readonly MaterialDateColorAnimation _colors;

    public MaterialDateChoiceRow()
    {
        Focusable = true;
        IsTabStop = false;
        FocusAdorner = null;
        _colors = new MaterialDateColorAnimation(this, YearColorAnimationEligibility);
    }

    void IMaterialDateChoiceCell.SetSelected(bool selected) =>
        SetYearState(":selected", selected);

    void IMaterialDateChoiceCell.SetCurrent(bool current) =>
        SetYearState(":current", current);

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _colors.Bind(
            e.NameScope.Find<ContentPresenter>("PART_ContentPresenter"),
            e.NameScope.Find<Border>("Surface"));
    }

    private void SetYearState(string name, bool value)
    {
        if (PseudoClasses.Contains(name) == value)
            return;

        PseudoClasses.Set(name, value);
        _colors.Refresh();
    }

    // yearContentColor and yearContainerColor always run through animateColorAsState.
    private static (bool Content, bool Container) YearColorAnimationEligibility() => (true, true);
}

/// <summary>
/// Springs the content and container colour channels of a date cell or year chip with the source
/// <c>MotionSchemeKeyTokens.DefaultEffects</c> spec, and paints the animated values into the
/// template parts.
/// </summary>
/// <remarks>
/// <para>This mirrors the owned-tint pattern of <see cref="MaterialNavigationBarItem"/>: the
/// control theme stays the single source of truth for targets, the state reads
/// <see cref="TemplatedControl.Foreground"/> for content and
/// <see cref="TemplatedControl.Background"/> for the container whenever a style applies a new
/// value, and a local <see cref="SolidColorBrush"/> carries the spring to the part. A live palette
/// change replaces the dynamic-resource brush, so the new target retargets the running spring
/// (position and velocity are preserved by <see cref="MaterialColorAnimation"/>) instead of
/// snapping.</para>
/// <para>Targets apply immediately on the first composition, while the control is off the visual
/// tree, and whenever the owning control reports the source's immediate state (range content and
/// the non-animated day container); everything else uses the interruptible spring. Reduced motion
/// is handled by <see cref="MaterialColorAnimation"/> itself, which finishes any running spring when
/// <see cref="MaterialMotion.GetIsReduced"/> becomes true.</para>
/// </remarks>
internal sealed class MaterialDateColorAnimation
{
    private readonly TemplatedControl _owner;
    private readonly Func<(bool Content, bool Container)> _animate;
    private readonly MaterialSelectionColorAnimation _content;
    private readonly MaterialSelectionColorAnimation _container;
    private readonly SolidColorBrush _contentBrush = new(Colors.Transparent);
    private readonly SolidColorBrush _containerBrush = new(Colors.Transparent);
    private Control? _contentPart;
    private Border? _containerPart;
    private IBrush? _contentTarget;
    private IBrush? _containerTarget;
    private Color _contentColor;
    private Color _containerColor;
    private bool _contentIsSolid;
    private bool _containerIsSolid;
    private bool _contentAnimate;
    private bool _containerAnimate;
    private bool _initialized;

    internal MaterialDateColorAnimation(TemplatedControl owner, Func<(bool Content, bool Container)> animate)
    {
        _owner = owner;
        _animate = animate;
        _content = new MaterialSelectionColorAnimation(owner, ApplyContent);
        _container = new MaterialSelectionColorAnimation(owner, ApplyContainer);
        owner.PropertyChanged += OnOwnerPropertyChanged;
        owner.Loaded += OnLoaded;
        owner.DetachedFromVisualTree += OnDetachedFromVisualTree;
        owner.ResourcesChanged += OnResourcesChanged;
        owner.ActualThemeVariantChanged += OnThemeVariantChanged;
    }

    /// <summary>Attaches the owned brushes to the content and container template parts.</summary>
    internal void Bind(Control? contentPart, Border? containerPart)
    {
        _contentPart = contentPart;
        _containerPart = containerPart;
        Refresh();
        PaintContent();
        PaintContainer();
    }

    /// <summary>Re-reads both style targets and retargets the springs.</summary>
    internal void Refresh()
    {
        var (animateContent, animateContainer) = _animate();
        // animateColorAsState starts on its target for a fresh composition, and a cell prepared
        // off-screen (a pooled month block) is the same case: both channels snap until the control
        // has completed its first layout/render pass.
        var animate = _initialized && _owner.IsAttachedToVisualTree();
        RefreshContent(animate && animateContent);
        RefreshContainer(animate && animateContainer);
    }

    private void RefreshContent(bool animate, bool force = false)
    {
        var target = _owner.Foreground;
        var solid = TryGetColor(target, out var color);
        var changed = force ||
                      !ReferenceEquals(target, _contentTarget) ||
                      solid != _contentIsSolid ||
                      animate != _contentAnimate ||
                      (solid && color != _contentColor);
        _contentTarget = target;
        _contentIsSolid = solid;
        _contentAnimate = animate;
        if (!changed)
            return;

        if (solid)
        {
            _contentColor = color;
            _content.Set(color, MaterialSpring.EffectsDefault, animate);
        }
        else
        {
            // No colour spring for a null/gradient target: mirror the supplied brush exactly.
            _content.Reset();
        }
        PaintContent();
    }

    private void RefreshContainer(bool animate, bool force = false)
    {
        var target = _owner.Background;
        var solid = TryGetColor(target, out var color);
        var changed = force ||
                      !ReferenceEquals(target, _containerTarget) ||
                      solid != _containerIsSolid ||
                      animate != _containerAnimate ||
                      (solid && color != _containerColor);
        _containerTarget = target;
        _containerIsSolid = solid;
        _containerAnimate = animate;
        if (!changed)
            return;

        if (solid)
        {
            _containerColor = color;
            _container.Set(color, MaterialSpring.EffectsDefault, animate);
        }
        else
        {
            _container.Reset();
        }
        PaintContainer();
    }

    private void ApplyContent(Color color) => _contentBrush.Color = color;

    private void ApplyContainer(Color color) => _containerBrush.Color = color;

    private void PaintContent()
    {
        _contentPart?.SetValue(
            TextElement.ForegroundProperty,
            _contentIsSolid ? _contentBrush : _contentTarget);
    }

    private void PaintContainer()
    {
        _containerPart?.SetValue(
            Border.BackgroundProperty,
            _containerIsSolid ? _containerBrush : _containerTarget);
    }

    private static bool TryGetColor(IBrush? brush, out Color color)
    {
        if (brush is ISolidColorBrush solid)
        {
            color = solid.Color;
            // Fold brush-level opacity into the animated colour so the spring still starts and
            // ends on the exactly painted value.
            if (solid.Opacity < 1)
            {
                color = Color.FromArgb(
                    (byte)Math.Clamp(Math.Round(color.A * solid.Opacity), 0, 255),
                    color.R, color.G, color.B);
            }
            return true;
        }

        color = default;
        return false;
    }

    private void OnOwnerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TemplatedControl.ForegroundProperty ||
            e.Property == TemplatedControl.BackgroundProperty)
        {
            Refresh();
        }
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        // First composition starts on the target (animateColorAsState is seeded with its initial
        // target): force both channels to their current style targets instead of springing from
        // the theme's base colours during the same pass that applies the initial state.
        _initialized = true;
        RefreshContent(animate: false, force: true);
        RefreshContainer(animate: false, force: true);
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        // A detached cell is reused like a fresh composition: the next target snaps instead of
        // continuing an interrupted spring (and a pooled month block must not fade in).
        _initialized = false;
        _content.Reset();
        _container.Reset();
    }

    private void OnResourcesChanged(object? sender, ResourcesChangedEventArgs e) => Refresh();

    private void OnThemeVariantChanged(object? sender, EventArgs e) => Refresh();
}