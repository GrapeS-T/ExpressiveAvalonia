using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace ExpressiveAvalonia.Controls;

/// <summary>The five Material state-layer states from the source building blocks.</summary>
/// <remarks>
/// The source documents them as <c>1. enabled</c>, <c>2. hovered</c>, <c>3. focused</c>,
/// <c>4. pressed</c> and <c>5. dragged</c>; the members keep that order.
/// </remarks>
public enum MaterialStateLayerState
{
    /// <summary>No overlay is drawn.</summary>
    Enabled,

    /// <summary>Pointer is over the layer.</summary>
    Hovered,

    /// <summary>The layer has keyboard focus.</summary>
    Focused,

    /// <summary>The primary pointer button is held over the layer.</summary>
    Pressed,

    /// <summary>The host reports an active drag; the surface and elevation are drawn as well.</summary>
    Dragged,
}

/// <summary>
/// A reusable Material state layer: the translucent on-surface overlay used for hover, focus,
/// press and drag feedback.
/// </summary>
/// <remarks>
/// <para>
/// With <see cref="IsInteractive"/> left false the layer is a static swatch driven by
/// <see cref="State"/>, which is what the source state-layer building blocks show. With
/// <see cref="IsInteractive"/> true the layer is focusable and tracks its own pointer and
/// keyboard state, using the same priority order as Material components: dragged, then pressed,
/// then hovered, then focused, then <see cref="State"/> as the resting value. It does not capture
/// the pointer, so an enclosing button or list item still receives its own input.
/// </para>
/// <para>
/// Opacities come from styled properties bound to the <c>Material*StateLayerOpacity</c> tokens,
/// so a theme can retune them. The dragged state additionally paints <see cref="SurfaceFill"/>
/// under the overlay with <see cref="Shadow"/>, reproducing the source swatch's surface fill and
/// level-5 elevation. Callers drive dragging through <see cref="IsDragging"/>.
/// </para>
/// <para>
/// The layer is presentational: it exposes no automation name and takes no tab stop unless
/// <see cref="IsInteractive"/> is set.
/// </para>
/// </remarks>
public sealed class MaterialStateLayer : Control
{
    /// <summary>Defines the <see cref="State"/> property.</summary>
    public static readonly StyledProperty<MaterialStateLayerState> StateProperty =
        AvaloniaProperty.Register<MaterialStateLayer, MaterialStateLayerState>(
            nameof(State), MaterialStateLayerState.Enabled);

    /// <summary>Defines the <see cref="IsInteractive"/> property.</summary>
    public static readonly StyledProperty<bool> IsInteractiveProperty =
        AvaloniaProperty.Register<MaterialStateLayer, bool>(nameof(IsInteractive));

    /// <summary>Defines the <see cref="IsDragging"/> property.</summary>
    public static readonly StyledProperty<bool> IsDraggingProperty =
        AvaloniaProperty.Register<MaterialStateLayer, bool>(nameof(IsDragging));

    /// <summary>Defines the <see cref="Background"/> property.</summary>
    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        AvaloniaProperty.Register<MaterialStateLayer, IBrush?>(nameof(Background));

    /// <summary>Defines the <see cref="CornerRadius"/> property.</summary>
    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<MaterialStateLayer, CornerRadius>(nameof(CornerRadius));

    /// <summary>Defines the <see cref="SurfaceFill"/> property.</summary>
    public static readonly StyledProperty<IBrush?> SurfaceFillProperty =
        AvaloniaProperty.Register<MaterialStateLayer, IBrush?>(nameof(SurfaceFill));

    /// <summary>Defines the <see cref="Shadow"/> property.</summary>
    public static readonly StyledProperty<BoxShadows> ShadowProperty =
        AvaloniaProperty.Register<MaterialStateLayer, BoxShadows>(nameof(Shadow));

    /// <summary>Defines the <see cref="HoverOpacity"/> property.</summary>
    public static readonly StyledProperty<double> HoverOpacityProperty =
        AvaloniaProperty.Register<MaterialStateLayer, double>(nameof(HoverOpacity), 0.08,
            validate: IsOpacity);

    /// <summary>Defines the <see cref="FocusOpacity"/> property.</summary>
    public static readonly StyledProperty<double> FocusOpacityProperty =
        AvaloniaProperty.Register<MaterialStateLayer, double>(nameof(FocusOpacity), 0.10,
            validate: IsOpacity);

    /// <summary>Defines the <see cref="PressedOpacity"/> property.</summary>
    public static readonly StyledProperty<double> PressedOpacityProperty =
        AvaloniaProperty.Register<MaterialStateLayer, double>(nameof(PressedOpacity), 0.10,
            validate: IsOpacity);

    /// <summary>Defines the <see cref="DraggedOpacity"/> property.</summary>
    public static readonly StyledProperty<double> DraggedOpacityProperty =
        AvaloniaProperty.Register<MaterialStateLayer, double>(nameof(DraggedOpacity), 0.16,
            validate: IsOpacity);

    /// <summary>Defines the <see cref="EffectiveState"/> property.</summary>
    public static readonly DirectProperty<MaterialStateLayer, MaterialStateLayerState> EffectiveStateProperty =
        AvaloniaProperty.RegisterDirect<MaterialStateLayer, MaterialStateLayerState>(
            nameof(EffectiveState), layer => layer._effectiveState);

    private MaterialStateLayerState _effectiveState = MaterialStateLayerState.Enabled;
    private bool _pointerOver;
    private bool _pressed;

    static MaterialStateLayer()
    {
        AffectsRender<MaterialStateLayer>(
            StateProperty, IsDraggingProperty, BackgroundProperty, CornerRadiusProperty,
            SurfaceFillProperty, ShadowProperty, HoverOpacityProperty, FocusOpacityProperty,
            PressedOpacityProperty, DraggedOpacityProperty);
        IsInteractiveProperty.Changed.AddClassHandler<MaterialStateLayer>((layer, _) =>
        {
            layer.UpdateEffectiveState();
            layer.InvalidateVisual();
        });
    }

    /// <summary>Gets or sets the resting state used when no interaction is active.</summary>
    public MaterialStateLayerState State
    {
        get => GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    /// <summary>Gets or sets whether the layer tracks its own pointer and keyboard state.</summary>
    public bool IsInteractive
    {
        get => GetValue(IsInteractiveProperty);
        set => SetValue(IsInteractiveProperty, value);
    }

    /// <summary>Gets or sets whether the host reports an active drag.</summary>
    public bool IsDragging
    {
        get => GetValue(IsDraggingProperty);
        set => SetValue(IsDraggingProperty, value);
    }

    /// <summary>Gets or sets the state-layer brush; the theme supplies the on-surface role.</summary>
    public IBrush? Background
    {
        get => GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    /// <summary>Gets or sets the corner radius of the layer.</summary>
    public CornerRadius CornerRadius
    {
        get => GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    /// <summary>Gets or sets the surface painted under the overlay in the dragged state.</summary>
    public IBrush? SurfaceFill
    {
        get => GetValue(SurfaceFillProperty);
        set => SetValue(SurfaceFillProperty, value);
    }

    /// <summary>Gets or sets the elevation drawn under the dragged surface.</summary>
    public BoxShadows Shadow
    {
        get => GetValue(ShadowProperty);
        set => SetValue(ShadowProperty, value);
    }

    /// <summary>Gets or sets the hover overlay opacity.</summary>
    public double HoverOpacity
    {
        get => GetValue(HoverOpacityProperty);
        set => SetValue(HoverOpacityProperty, value);
    }

    /// <summary>Gets or sets the focus overlay opacity.</summary>
    public double FocusOpacity
    {
        get => GetValue(FocusOpacityProperty);
        set => SetValue(FocusOpacityProperty, value);
    }

    /// <summary>Gets or sets the pressed overlay opacity.</summary>
    public double PressedOpacity
    {
        get => GetValue(PressedOpacityProperty);
        set => SetValue(PressedOpacityProperty, value);
    }

    /// <summary>Gets or sets the dragged overlay opacity.</summary>
    public double DraggedOpacity
    {
        get => GetValue(DraggedOpacityProperty);
        set => SetValue(DraggedOpacityProperty, value);
    }

    /// <summary>Gets the state currently rendered after interaction is resolved.</summary>
    public MaterialStateLayerState EffectiveState => _effectiveState;

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var state = _effectiveState;
        if (state == MaterialStateLayerState.Enabled)
        {
            // An interactive layer must be hit-testable while resting, otherwise hover can
            // never start. A Control that paints nothing receives no pointer input.
            if (IsInteractive)
                context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
            return;
        }

        var rounded = new RoundedRect(new Rect(Bounds.Size), CornerRadius);
        if (state == MaterialStateLayerState.Dragged && SurfaceFill is not null)
            context.DrawRectangle(SurfaceFill, null, rounded, Shadow);

        var opacity = state switch
        {
            MaterialStateLayerState.Hovered => HoverOpacity,
            MaterialStateLayerState.Focused => FocusOpacity,
            MaterialStateLayerState.Pressed => PressedOpacity,
            MaterialStateLayerState.Dragged => DraggedOpacity,
            _ => 0,
        };
        if (opacity <= 0 || Background is null)
            return;

        using (context.PushOpacity(opacity))
            context.DrawRectangle(Background, null, rounded);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StateProperty
            || change.Property == IsDraggingProperty
            || change.Property == IsKeyboardFocusWithinProperty)
        {
            UpdateEffectiveState();
        }
    }

    /// <inheritdoc />
    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        _pointerOver = true;
        UpdateEffectiveState();
    }

    /// <inheritdoc />
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _pointerOver = false;
        _pressed = false;
        UpdateEffectiveState();
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!IsInteractive || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        _pressed = true;
        // An interactive layer is its own target, so a press moves focus to it and the
        // focused state follows a real keyboard focus rather than a pointer approximation.
        if (Focusable && !IsKeyboardFocusWithin)
            Focus();
        UpdateEffectiveState();
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_pressed)
            return;

        _pressed = false;
        UpdateEffectiveState();
    }

    /// <inheritdoc />
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _pressed = false;
        UpdateEffectiveState();
    }

    private void UpdateEffectiveState()
    {
        var state = ResolveState();
        if (state == _effectiveState)
            return;

        var old = _effectiveState;
        _effectiveState = state;
        RaisePropertyChanged(EffectiveStateProperty, old, state);
        InvalidateVisual();
    }

    private MaterialStateLayerState ResolveState()
    {
        if (IsDragging)
            return MaterialStateLayerState.Dragged;

        if (IsInteractive)
        {
            if (_pressed)
                return MaterialStateLayerState.Pressed;
            if (_pointerOver)
                return MaterialStateLayerState.Hovered;
            if (IsKeyboardFocusWithin)
                return MaterialStateLayerState.Focused;
        }

        return State;
    }

    private static bool IsOpacity(double value) => double.IsFinite(value) && value is >= 0 and <= 1;
}
