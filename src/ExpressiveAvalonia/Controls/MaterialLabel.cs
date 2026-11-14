using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// A Material label anchored above its content, matching the source <c>Label</c> composable.
/// </summary>
/// <remarks>
/// <para>
/// The source anchors a plain tooltip above its content. While <see cref="IsPersistent"/> is true
/// the label is always shown and interactions are ignored; otherwise it appears while the content
/// is hovered, pressed or dragged. Press, hover and drag are tracked separately, so the label stays
/// visible when a pressed thumb starts dragging and the pointer leaves the content, which is the
/// source's explicit fix for slider thumbs shrinking under the pointer.
/// </para>
/// <para>
/// The desktop adaptation keeps the label out of layout: the label follows the source plain
/// tooltip's above placement, flips below when it would cross the top of the top level, and never
/// contributes to the measured size, so showing it cannot move the content. Its visuals
/// use the plain-tooltip tokens (inverse-surface container, inverse-on-surface text, body-small
/// type, extra-small shape, 8x4 padding, 40x24 minimum, 200 maximum width) from
/// <c>PlainTooltipTokens</c>; the label gap is 4 DIPs.
/// </para>
/// <para>
/// The label is presentational and never takes focus or hit testing. Hosts that own a drag gesture,
/// such as a slider thumb, set <see cref="IsDragging"/>; plain interactive content works without
/// extra wiring.
/// </para>
/// </remarks>
public sealed class MaterialLabel : ContentControl
{
    /// <summary>Defines the <see cref="Label"/> property.</summary>
    public static readonly StyledProperty<object?> LabelProperty =
        AvaloniaProperty.Register<MaterialLabel, object?>(nameof(Label));

    /// <summary>Defines the <see cref="LabelTemplate"/> property.</summary>
    public static readonly StyledProperty<Avalonia.Controls.Templates.IDataTemplate?> LabelTemplateProperty =
        AvaloniaProperty.Register<MaterialLabel, Avalonia.Controls.Templates.IDataTemplate?>(nameof(LabelTemplate));

    /// <summary>Defines the <see cref="IsPersistent"/> property.</summary>
    public static readonly StyledProperty<bool> IsPersistentProperty =
        AvaloniaProperty.Register<MaterialLabel, bool>(nameof(IsPersistent));

    /// <summary>Defines the <see cref="IsDragging"/> property.</summary>
    public static readonly StyledProperty<bool> IsDraggingProperty =
        AvaloniaProperty.Register<MaterialLabel, bool>(nameof(IsDragging));

    /// <summary>Defines the <see cref="IsLabelVisible"/> property.</summary>
    public static readonly DirectProperty<MaterialLabel, bool> IsLabelVisibleProperty =
        AvaloniaProperty.RegisterDirect<MaterialLabel, bool>(nameof(IsLabelVisible), label => label.IsLabelVisible);

    private bool _pointerOver;
    private bool _pressed;
    private bool _isLabelVisible;

    static MaterialLabel()
    {
        IsPersistentProperty.Changed.AddClassHandler<MaterialLabel>((label, _) => label.UpdateVisibility());
        IsDraggingProperty.Changed.AddClassHandler<MaterialLabel>((label, _) => label.UpdateVisibility());
        ContentProperty.Changed.AddClassHandler<MaterialLabel>((label, _) => label.UpdateVisibility());
    }

    /// <summary>Gets or sets the label content.</summary>
    public object? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Gets or sets the label template.</summary>
    public Avalonia.Controls.Templates.IDataTemplate? LabelTemplate
    {
        get => GetValue(LabelTemplateProperty);
        set => SetValue(LabelTemplateProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the label is always visible. Persistent labels do not track input.
    /// </summary>
    public bool IsPersistent
    {
        get => GetValue(IsPersistentProperty);
        set => SetValue(IsPersistentProperty, value);
    }

    /// <summary>Gets or sets whether the host is dragging its content.</summary>
    public bool IsDragging
    {
        get => GetValue(IsDraggingProperty);
        set => SetValue(IsDraggingProperty, value);
    }

    /// <summary>Gets whether the label is currently rendered.</summary>
    public bool IsLabelVisible => _isLabelVisible;

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _pointerOver = false;
        _pressed = false;
        UpdateVisibility();
    }

    /// <inheritdoc />
    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        _pointerOver = true;
        UpdateVisibility();
    }

    /// <inheritdoc />
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _pointerOver = false;
        // A drag keeps the label open through the pointer exit, matching the source interaction
        // list; only releasing the press or ending the drag hides it.
        UpdateVisibility();
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        // The label observes input; the enclosed control still handles the press.
        _pressed = true;
        UpdateVisibility();
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_pressed)
            return;
        _pressed = false;
        UpdateVisibility();
    }

    /// <inheritdoc />
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _pressed = false;
        UpdateVisibility();
    }

    private void UpdateVisibility()
    {
        var visible = IsPersistent || _pointerOver || _pressed || IsDragging;
        if (visible == _isLabelVisible)
            return;
        SetAndRaise(IsLabelVisibleProperty, ref _isLabelVisible, visible);
    }
}

/// <summary>
/// Lays out a <see cref="MaterialLabel"/> anchor and its label overlay.
/// </summary>
/// <remarks>
/// The first child is the anchor and is arranged into the panel's slot with its own margin and
/// alignment. Every later child is a label: it is measured with unbounded space, does not
/// contribute to the panel's desired size, and uses the source tooltip <c>Above</c> position
/// provider. Normally the label is centered above the anchor's arranged bounds with the 4 DIP
/// tooltip gap. When that would cross the top edge of the top level's client area, the label flips
/// below the anchor, and both axes are then clamped into the client area, matching
/// <c>Tooltip.kt</c> <c>abovePositioning</c>. The client rectangle is mapped into the panel's
/// coordinate space through the visual transform, so offset, mirrored and scaled anchors use the
/// same math. Outside a top level, such as in a designer or a detached layout test, the label stays
/// centered above its anchor.
/// </remarks>
public sealed class MaterialLabelPanel : Panel
{
    /// <summary>The gap between the label and its anchor, from <c>SpacingBetweenTooltipAndAnchor</c>.</summary>
    public const double AnchorGap = 4;

    private bool _arrangedAgainstViewport;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count == 0)
            return default;

        var anchor = Children[0];
        anchor.Measure(availableSize);
        var size = anchor.DesiredSize;

        for (var i = 1; i < Children.Count; i++)
            Children[i].Measure(Size.Infinity);

        return size;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count == 0)
            return finalSize;

        var anchor = Children[0];
        anchor.Arrange(new Rect(finalSize));
        var anchorBounds = anchor.Bounds;

        // The viewport is the top level's client rectangle in this panel's coordinate space.
        // Without a top level there is no window edge to avoid, so the local above position stands.
        var viewport = GetViewport();
        _arrangedAgainstViewport = viewport.HasValue;

        for (var i = 1; i < Children.Count; i++)
        {
            var label = Children[i];
            var width = label.DesiredSize.Width;
            var height = label.DesiredSize.Height;

            // Tooltip.kt abovePositioning: center over the anchor, clamp horizontally, then prefer
            // above. Crossing the top edge flips the label below before the vertical clamp.
            var x = anchorBounds.Center.X - width / 2;
            var y = anchorBounds.Top - height - AnchorGap;

            if (viewport is { } bounds)
            {
                if (y < bounds.Top)
                    y = anchorBounds.Bottom + AnchorGap;

                var maxX = Math.Max(bounds.Left, bounds.Right - width);
                var maxY = Math.Max(bounds.Top, bounds.Bottom - height);
                x = Math.Clamp(x, bounds.Left, maxX);
                y = Math.Clamp(y, bounds.Top, maxY);
            }

            label.Arrange(new Rect(x, y, width, height));
        }

        return finalSize;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        // Avalonia assigns this panel's Bounds after ArrangeOverride returns, so the first pass
        // maps the window through the previous bounds. Arrange once more once the real bounds are
        // available; the second pass moves no further, so this cannot loop.
        if (change.Property == BoundsProperty && _arrangedAgainstViewport && this.IsAttachedToVisualTree())
            InvalidateArrange();
    }

    private Rect? GetViewport()
    {
        if (TopLevel.GetTopLevel(this) is not { } topLevel)
            return null;

        var size = topLevel.ClientSize;
        if (size.Width <= 0 || size.Height <= 0 ||
            !double.IsFinite(size.Width) || !double.IsFinite(size.Height))
        {
            return null;
        }

        if (topLevel.TranslatePoint(default, this) is not { } topLeft ||
            topLevel.TranslatePoint(new Point(size.Width, size.Height), this) is not { } bottomRight)
        {
            return null;
        }

        // Opposite corners cover translation, scale and the root mirror transform used by RTL.
        return new Rect(
            Math.Min(topLeft.X, bottomRight.X),
            Math.Min(topLeft.Y, bottomRight.Y),
            Math.Abs(bottomRight.X - topLeft.X),
            Math.Abs(bottomRight.Y - topLeft.Y));
    }
}