using System.Collections.Generic;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using ExpressiveAvalonia.Theming;

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
/// A reusable Material state layer: the translucent on-surface overlay used for hover, focus
/// and drag feedback.
/// </summary>
/// <remarks>
/// <para>
/// This control mirrors the <c>StateLayer</c> inside the pinned material-ripple <c>RippleNode</c>.
/// A press is deliberately not part of the overlay: in the source the press indication is the
/// ripple itself (<c>StateTokens.PressedStateLayerOpacity</c> is the ripple's pressed alpha), so
/// components that combine this layer with <see cref="MaterialRipple"/> never paint the same
/// press alpha twice.
/// </para>
/// <para>
/// With <see cref="IsInteractive"/> left false the layer is a static swatch driven by
/// <see cref="State"/>; <see cref="MaterialStateLayerState.Pressed"/> is then an explicit preview
/// value, not an interaction. With <see cref="IsInteractive"/> true it tracks pointer over,
/// keyboard focus and drag, and shows the most recently arrived interaction, exactly like
/// <c>interactions.lastOrNull()</c> in the source. Incoming hover is 15 ms linear, incoming focus
/// and drag are 45 ms linear; outgoing hover and focus are 15 ms linear and outgoing drag is
/// 150 ms linear. <see cref="State"/> is the resting fallback when no interaction is active.
/// </para>
/// <para>
/// Opacities come from styled properties bound to the <c>Material*StateLayerOpacity</c> tokens,
/// so a theme can retune them. The dragged state additionally paints <see cref="SurfaceFill"/>
/// under the overlay with <see cref="Shadow"/>, reproducing the source swatch's surface fill and
/// level-5 elevation. Callers drive dragging through <see cref="IsDragging"/>.
/// </para>
/// <para>
/// The layer is presentational: it exposes no automation name and takes no tab stop unless
/// <see cref="IsInteractive"/> is set. Disabling the control clears the overlay immediately:
/// the source has no disabled state layer.
/// </para>
/// </remarks>
public sealed class MaterialStateLayer : Control
{
    private const double DefaultSeconds = 0.015;
    private const double FocusInSeconds = 0.045;
    private const double DragInSeconds = 0.045;
    private const double DragOutSeconds = 0.150;

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

    private readonly List<MaterialStateLayerState> _interactions = new(3);
    private MaterialStateLayerState _effectiveState = MaterialStateLayerState.Enabled;
    private MaterialStateLayerState? _currentInteraction;
    private bool _pointerOver;
    private double _currentOpacity;
    private MaterialMotion.TweenAnimation? _opacityTween;

    static MaterialStateLayer()
    {
        AffectsRender<MaterialStateLayer>(
            StateProperty, IsDraggingProperty, BackgroundProperty, CornerRadiusProperty,
            SurfaceFillProperty, ShadowProperty, HoverOpacityProperty, FocusOpacityProperty,
            PressedOpacityProperty, DraggedOpacityProperty, IsInteractiveProperty);
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

    /// <summary>Gets or sets the pressed overlay opacity used by the static preview.</summary>
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
        if (state == MaterialStateLayerState.Enabled && _currentOpacity <= 0.001)
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

        if (_currentOpacity <= 0.001 || Background is null)
        {
            if (IsInteractive)
                context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
            return;
        }

        // The source draws a circle of the ripple end radius clipped to the target. For any
        // rectangle that circle covers the whole rounded silhouette, so painting the silhouette
        // is geometrically equivalent and avoids an offscreen clip.
        using (context.PushOpacity(_currentOpacity))
            context.DrawRectangle(Background, null, rounded);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == MaterialMotion.IsReducedProperty)
        {
            // Switching the system policy on mid-tween must land on the final interactive state,
            // not leave a half-faded overlay.
            if (MaterialMotion.GetIsReduced(this))
                SnapOpacity(OpacityFor(_effectiveState));
            return;
        }

        if (change.Property == StateProperty
            || change.Property == IsDraggingProperty
            || change.Property == IsInteractiveProperty
            || change.Property == IsKeyboardFocusWithinProperty
            || change.Property == IsEnabledProperty
            || change.Property == IsEffectivelyEnabledProperty
            || change.Property == HoverOpacityProperty
            || change.Property == FocusOpacityProperty
            || change.Property == DraggedOpacityProperty
            || change.Property == PressedOpacityProperty)
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
        UpdateEffectiveState();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _opacityTween?.Dispose();
        _opacityTween = null;
    }

    private void UpdateEffectiveState()
    {
        var enabled = IsEffectivelyEnabled;
        SyncInteraction(MaterialStateLayerState.Hovered, IsInteractive && enabled && _pointerOver);
        SyncInteraction(MaterialStateLayerState.Focused, IsInteractive && enabled && IsKeyboardFocusWithin);
        SyncInteraction(MaterialStateLayerState.Dragged, enabled && IsDragging);

        MaterialStateLayerState? nextInteraction = null;
        if (enabled && _interactions.Count > 0)
            nextInteraction = _interactions[^1];

        var state = !enabled
            ? MaterialStateLayerState.Enabled
            : nextInteraction ?? State;

        var previousInteraction = _currentInteraction;
        _currentInteraction = nextInteraction;
        SetEffectiveState(state);

        var target = OpacityFor(state);
        if (!enabled)
        {
            SnapOpacity(0);
            return;
        }

        double duration;
        if (nextInteraction is not null)
        {
            duration = nextInteraction == previousInteraction
                ? DefaultSeconds
                : nextInteraction switch
                {
                    MaterialStateLayerState.Focused => FocusInSeconds,
                    MaterialStateLayerState.Dragged => DragInSeconds,
                    _ => DefaultSeconds,
                };
        }
        else if (previousInteraction is not null)
        {
            duration = previousInteraction == MaterialStateLayerState.Dragged
                ? DragOutSeconds
                : DefaultSeconds;
        }
        else
        {
            // A caller-driven State change (a static preview or a template pseudo-class) is not
            // an interaction arrival: the source has no spec for it, so apply it directly.
            SnapOpacity(target);
            return;
        }

        AnimateOpacity(target, duration);
    }

    private void SyncInteraction(MaterialStateLayerState interaction, bool active)
    {
        var index = _interactions.IndexOf(interaction);
        if (active && index < 0)
            _interactions.Add(interaction);
        else if (!active && index >= 0)
            _interactions.RemoveAt(index);
    }

    private void SetEffectiveState(MaterialStateLayerState state)
    {
        if (state == _effectiveState)
            return;
        var old = _effectiveState;
        _effectiveState = state;
        RaisePropertyChanged(EffectiveStateProperty, old, state);
        InvalidateVisual();
    }

    private double OpacityFor(MaterialStateLayerState state) => state switch
    {
        MaterialStateLayerState.Hovered => HoverOpacity,
        MaterialStateLayerState.Focused => FocusOpacity,
        MaterialStateLayerState.Pressed => PressedOpacity,
        MaterialStateLayerState.Dragged => DraggedOpacity,
        _ => 0.0,
    };

    private void AnimateOpacity(double target, double duration)
    {
        if (MaterialMotion.GetIsReduced(this))
        {
            SnapOpacity(target);
            return;
        }

        if (_opacityTween is null)
            _opacityTween = MaterialMotion.Tween(this, _currentOpacity, target, duration, 0, ApplyOpacity);
        else
            _opacityTween.Retarget(target, duration);
    }

    private void ApplyOpacity(double value)
    {
        _currentOpacity = value;
        InvalidateVisual();
    }

    private void SnapOpacity(double value)
    {
        _opacityTween?.Dispose();
        _opacityTween = null;
        _currentOpacity = value;
        InvalidateVisual();
    }

    private static bool IsOpacity(double value) => double.IsFinite(value) && value is >= 0 and <= 1;
}