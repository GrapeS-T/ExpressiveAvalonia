using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// The Material focus indicator: a 3 dp ring drawn outside the focused surface.
/// </summary>
/// <remarks>
/// <para>
/// The source indicator is a 3 dp outline in the secondary role with a 2 dp corner radius and
/// <c>strokeAlign: OUTSIDE</c>. The ring is therefore drawn against the outer edge of the
/// control's own bounds, inset by half the stroke so no part is clipped. To reproduce the
/// source's outside alignment over a surface, place the indicator over that surface with a
/// negative margin equal to <see cref="StrokeThickness"/> (or make it that much larger).
/// </para>
/// <para>
/// <see cref="IsActive"/> forces the ring on, which is what a static source-style preview needs.
/// Setting <see cref="FocusTarget"/> additionally mirrors that control's keyboard focus: the ring
/// appears while the target contains keyboard focus and disappears when it does not, which is the
/// behavior a template needs without a second binding. The indicator never takes focus or hit
/// testing.
/// </para>
/// </remarks>
public sealed class MaterialFocusIndicator : Control
{
    /// <summary>Defines the <see cref="IsActive"/> property.</summary>
    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<MaterialFocusIndicator, bool>(nameof(IsActive));

    /// <summary>Defines the <see cref="Stroke"/> property.</summary>
    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<MaterialFocusIndicator, IBrush?>(nameof(Stroke));

    /// <summary>Defines the <see cref="StrokeThickness"/> property.</summary>
    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<MaterialFocusIndicator, double>(nameof(StrokeThickness), 3,
            validate: value => double.IsFinite(value) && value >= 0);

    /// <summary>Defines the <see cref="CornerRadius"/> property.</summary>
    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<MaterialFocusIndicator, CornerRadius>(nameof(CornerRadius), new CornerRadius(2));

    /// <summary>Defines the <see cref="FocusTarget"/> property.</summary>
    public static readonly StyledProperty<Control?> FocusTargetProperty =
        AvaloniaProperty.Register<MaterialFocusIndicator, Control?>(nameof(FocusTarget));

    private readonly Pen _pen = new();
    private Control? _target;
    private bool _targetFocused;

    static MaterialFocusIndicator()
    {
        AffectsRender<MaterialFocusIndicator>(
            IsActiveProperty, StrokeProperty, StrokeThicknessProperty, CornerRadiusProperty);
    }

    /// <summary>Gets or sets whether the ring is shown regardless of focus.</summary>
    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    /// <summary>Gets or sets the ring brush; the theme supplies the secondary role.</summary>
    public IBrush? Stroke
    {
        get => GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    /// <summary>Gets or sets the ring thickness in device-independent units.</summary>
    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    /// <summary>Gets or sets the ring corner radius, matching the decorated surface.</summary>
    public CornerRadius CornerRadius
    {
        get => GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    /// <summary>Gets or sets a control whose keyboard focus is mirrored by the ring.</summary>
    public Control? FocusTarget
    {
        get => GetValue(FocusTargetProperty);
        set => SetValue(FocusTargetProperty, value);
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var thickness = StrokeThickness;
        var brush = Stroke;
        if (brush is null || thickness <= 0 || !(IsActive || _targetFocused))
            return;

        var inset = thickness / 2;
        var width = Bounds.Width - thickness;
        var height = Bounds.Height - thickness;
        if (width <= 0 || height <= 0)
            return;

        _pen.Brush = brush;
        _pen.Thickness = thickness;
        var radius = new CornerRadius(
            Math.Max(0, CornerRadius.TopLeft - inset),
            Math.Max(0, CornerRadius.TopRight - inset),
            Math.Max(0, CornerRadius.BottomRight - inset),
            Math.Max(0, CornerRadius.BottomLeft - inset));
        context.DrawRectangle(null, _pen, new RoundedRect(new Rect(inset, inset, width, height), radius));
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FocusTargetProperty)
            Subscribe(change.GetNewValue<Control?>());
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // Re-subscribe: the target may have been assigned while the indicator was off-tree.
        Subscribe(GetValue(FocusTargetProperty));
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Unsubscribe();
    }

    private void Subscribe(Control? target)
    {
        Unsubscribe();
        _target = target;
        if (_target is not null)
        {
            _target.PropertyChanged += OnTargetPropertyChanged;
            _target.AddHandler(InputElement.GotFocusEvent, OnTargetFocusEvent, RoutingStrategies.Bubble, true);
            _target.AddHandler(InputElement.LostFocusEvent, OnTargetFocusEvent, RoutingStrategies.Bubble, true);
        }

        _targetFocused = _target?.IsKeyboardFocusWithin ?? false;
        InvalidateVisual();
    }

    private void Unsubscribe()
    {
        if (_target is null)
            return;

        _target.PropertyChanged -= OnTargetPropertyChanged;
        _target.RemoveHandler(InputElement.GotFocusEvent, OnTargetFocusEvent);
        _target.RemoveHandler(InputElement.LostFocusEvent, OnTargetFocusEvent);
        _target = null;
    }

    private void OnTargetFocusEvent(object? sender, RoutedEventArgs e) => UpdateTargetFocus();

    private void OnTargetPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == InputElement.IsKeyboardFocusWithinProperty)
            UpdateTargetFocus();
    }

    private void UpdateTargetFocus()
    {
        var focused = _target?.IsKeyboardFocusWithin ?? false;
        if (focused == _targetFocused)
            return;

        _targetFocused = focused;
        InvalidateVisual();
    }
}
