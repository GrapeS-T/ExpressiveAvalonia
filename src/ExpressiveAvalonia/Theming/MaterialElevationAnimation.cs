using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace ExpressiveAvalonia.Theming;

/// <summary>The interaction elevations a Material component resolves for one control.</summary>
/// <remarks>
/// Source: <c>internal/Elevation.kt</c> at pinned
/// <c>77b2199d6f056c2fe829ce25523c34a905e769c1</c>. Values are logical elevation token levels
/// (0-5), matching <c>ElevationTokens.LevelN</c>; the helper maps level N to the
/// <c>MaterialElevationLevelNShadow</c> resource. <see cref="Dragged"/> exists for components
/// whose source tracks <c>DragInteraction.Start</c> (chips), and is unused by the FAB family.
/// </remarks>
internal readonly record struct MaterialElevationSpecs(
    double Default,
    double Pressed,
    double Focused,
    double Hovered,
    double Dragged,
    double Disabled);

/// <summary>
/// Ports the pinned interaction-driven elevation animation shared by Material components.
/// </summary>
/// <remarks>
/// <para>
/// Every interaction is collected while it is active and the most recently activated one selects
/// the target elevation, matching the interaction lists in
/// <c>FloatingActionButton.kt</c>, <c>FloatingActionButtonMenu.kt</c> and <c>Chip.kt</c>. Moving to
/// an interaction uses the 120 ms incoming FastOutSlowIn spec; leaving one uses 150 ms, except
/// leaving hover, which uses the 120 ms hovered-outgoing spec. Both outgoing specs use
/// cubic-bezier(0.4, 0, 0.6, 1). With no incoming or outgoing interaction the elevation snaps
/// (initial load and the disabled state), and reduced motion snaps as well.
/// </para>
/// <para>
/// The animated value is published through <see cref="MaterialAssist.AnimatedShadowProperty"/> so
/// a template Border can bind <c>BoxShadow="{TemplateBinding theming:MaterialAssist.AnimatedShadow}"</c>.
/// Shadow tokens are loaded from the component's resource scope and adjacent token levels are
/// blended, because Avalonia exposes discrete token shadows while Compose renders a continuous
/// elevation.
/// </para>
/// </remarks>
internal sealed class MaterialElevationAnimation : IDisposable
{
    // MaterialElevationLevel0Shadow .. MaterialElevationLevel5Shadow.
    private const int ElevationLevelCount = 6;
    private static readonly Func<double, double> IncomingElevationEasing = new SplineEasing(0.4, 0, 0.2, 1).Ease;
    private static readonly Func<double, double> OutgoingElevationEasing = new SplineEasing(0.4, 0, 0.6, 1).Ease;

    private readonly Button _owner;
    private readonly Func<MaterialElevationSpecs> _resolveSpecs;
    private readonly Func<bool>? _isDragging;
    private readonly List<Interaction> _interactions = new(4);
    private MaterialMotion.TweenAnimation? _tween;
    private BoxShadows?[]? _levelShadows;
    private Interaction? _lastInteraction;
    private double _value;
    private double _target;
    private bool _enterPressed;
    private bool _initialized;
    private bool _disposed;

    internal MaterialElevationAnimation(
        Button owner,
        Func<MaterialElevationSpecs> resolveSpecs,
        Func<bool>? isDragging = null)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _resolveSpecs = resolveSpecs ?? throw new ArgumentNullException(nameof(resolveSpecs));
        _isDragging = isDragging;

        owner.PropertyChanged += OnPropertyChanged;
        owner.AttachedToVisualTree += OnAttached;
        owner.DetachedFromVisualTree += OnDetached;
        owner.ActualThemeVariantChanged += OnThemeVariantChanged;
        owner.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Bubble, handledEventsToo: true);
        owner.AddHandler(InputElement.KeyUpEvent, OnKeyUp, RoutingStrategies.Bubble, handledEventsToo: true);
        Refresh(snap: true);
    }

    /// <summary>The elevation level currently published to the template.</summary>
    internal double Value => _value;

    /// <summary>Re-evaluates the active interactions and moves toward the resolved level.</summary>
    /// <param name="snap">Applies the destination immediately instead of animating.</param>
    internal void Refresh(bool snap)
    {
        if (_disposed)
            return;

        var enabled = _owner.IsEffectivelyEnabled;
        SyncInteractions(enabled);
        var specs = _resolveSpecs();
        var interaction = LastInteraction;
        var target = enabled ? TargetFor(interaction, specs) : specs.Disabled;
        var previousTarget = _target;
        var previousInteraction = _lastInteraction;
        _target = target;
        // Track the interaction even when the value is unchanged, so a later target change
        // animates from the interaction that was actually active.
        _lastInteraction = interaction;

        if (snap
            || !_initialized
            || !_owner.IsAttachedToVisualTree()
            || !enabled
            || MaterialMotion.GetIsReduced(_owner))
        {
            _initialized = true;
            Stop();
            Apply(target);
            return;
        }

        if (target == previousTarget)
            return;

        double duration;
        Func<double, double> easing;
        if (interaction is not null)
        {
            duration = 0.120;
            easing = IncomingElevationEasing;
        }
        else if (previousInteraction is not null)
        {
            duration = previousInteraction == Interaction.Hover ? 0.120 : 0.150;
            easing = OutgoingElevationEasing;
        }
        else
        {
            Stop();
            Apply(target);
            return;
        }

        Stop();
        _tween = MaterialMotion.Tween(_owner, _value, target, duration, 0, Apply, easing);
    }

    private static double TargetFor(Interaction? interaction, MaterialElevationSpecs specs) => interaction switch
    {
        Interaction.Press => specs.Pressed,
        Interaction.Hover => specs.Hovered,
        Interaction.Focus => specs.Focused,
        Interaction.Drag => specs.Dragged,
        _ => specs.Default,
    };

    private Interaction? LastInteraction => _interactions.Count > 0 ? _interactions[^1] : null;

    // Clickable.kt emits PressInteraction.Press for both Space and Enter on key down. Space
    // already becomes Button.IsPressed on Avalonia; Enter is tracked here so elevation reacts to
    // keyboard activation as well.
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || _enterPressed || !_owner.IsEffectivelyEnabled)
            return;
        _enterPressed = true;
        Refresh(snap: false);
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || !_enterPressed)
            return;
        _enterPressed = false;
        Refresh(snap: false);
    }

    private void OnAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        // The constructor can run before theme resources are reachable; retry the elevation
        // tokens once the control is in a tree, as the source's initial load snaps.
        _levelShadows = null;
        Refresh(snap: true);
    }

    private void OnDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _enterPressed = false;
        // Cancel the in-flight segment: the shared tween skips its own detach callback once
        // disposed, so a detach does not apply the cancelled target.
        Stop();
        _initialized = false;
    }

    private void OnThemeVariantChanged(object? sender, EventArgs e)
    {
        _levelShadows = null;
        Apply(_value);
    }

    private void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.Property == Button.IsPressedProperty
            || args.Property == InputElement.IsPointerOverProperty
            || args.Property == InputElement.IsFocusedProperty
            || args.Property == InputElement.IsKeyboardFocusWithinProperty
            || args.Property == InputElement.IsEnabledProperty
            || args.Property == InputElement.IsEffectivelyEnabledProperty)
        {
            if (!_owner.IsKeyboardFocusWithin || !_owner.IsEffectivelyEnabled)
                _enterPressed = false;
            Refresh(snap: false);
        }
        else if (args.Property == MaterialMotion.IsReducedProperty)
        {
            Refresh(snap: MaterialMotion.GetIsReduced(_owner));
        }
    }

    private void SyncInteractions(bool enabled)
    {
        Set(Interaction.Press, (_owner.IsPressed || _enterPressed) && enabled);
        Set(Interaction.Hover, _owner.IsPointerOver && enabled);
        // FocusInteraction.Focus is emitted for every focus, not only the visible focus ring;
        // keyboard modality remains the template's :focus-visible concern.
        Set(Interaction.Focus, _owner.IsFocused && enabled);
        Set(Interaction.Drag, enabled && _isDragging is not null && _isDragging());

        void Set(Interaction interaction, bool active)
        {
            var index = _interactions.IndexOf(interaction);
            if (active && index < 0)
                _interactions.Add(interaction);
            else if (!active && index >= 0)
                _interactions.RemoveAt(index);
        }
    }

    private void Apply(double elevation)
    {
        _value = elevation;
        var shadows = _levelShadows ??= LoadShadows();
        _owner.SetCurrentValue(MaterialAssist.AnimatedShadowProperty, InterpolateShadow(shadows, elevation));
    }

    private BoxShadows?[] LoadShadows()
    {
        var shadows = new BoxShadows?[ElevationLevelCount];
        for (var i = 0; i < shadows.Length; i++)
        {
            if (_owner.TryFindResource($"MaterialElevationLevel{i}Shadow", _owner.ActualThemeVariant, out var value)
                && value is BoxShadows boxShadows)
                shadows[i] = boxShadows;
        }
        return shadows;
    }

    private static BoxShadows InterpolateShadow(BoxShadows?[] shadows, double level)
    {
        if (level <= 0)
            return shadows[0] ?? default;
        var last = shadows.Length - 1;
        if (level >= last)
            return shadows[last] ?? default;

        // Specs carry logical elevation levels, not DIP distances. Blend the two adjacent token
        // shadows while moving between LevelN and LevelN+1.
        var lower = (int)Math.Floor(level);
        var upper = lower + 1;
        var from = shadows[lower];
        var to = shadows[upper];
        if (from is null || to is null)
            return from ?? to ?? default;

        return LerpShadow(from.Value, to.Value, level - lower);
    }

    /// <summary>
    /// Avalonia exposes discrete token shadows; Compose renders a continuous elevation.
    /// Blending the two adjacent token shadows is the measurable desktop adaptation.
    /// </summary>
    private static BoxShadows LerpShadow(BoxShadows from, BoxShadows to, double progress)
    {
        if (progress <= 0)
            return from;
        if (progress >= 1)
            return to;

        var count = Math.Max(from.Count, to.Count);
        if (count == 0)
            return default;

        var first = LerpShadowAt(from, to, 0, progress);
        if (count == 1)
            return new BoxShadows(first);

        // BoxShadows retains its tail array: each published value must own it. Allocate only
        // that required storage, not an intermediate array followed by a sliced copy.
        var rest = new BoxShadow[count - 1];
        for (var i = 1; i < count; i++)
            rest[i - 1] = LerpShadowAt(from, to, i, progress);
        return new BoxShadows(first, rest);
    }

    private static BoxShadow LerpShadowAt(BoxShadows from, BoxShadows to, int index, double progress)
    {
        var a = index < from.Count ? from[index] : default;
        var b = index < to.Count ? to[index] : default;
        return new BoxShadow
        {
            OffsetX = Lerp(a.OffsetX, b.OffsetX, progress),
            OffsetY = Lerp(a.OffsetY, b.OffsetY, progress),
            Blur = Lerp(a.Blur, b.Blur, progress),
            Spread = Lerp(a.Spread, b.Spread, progress),
            Color = Lerp(a.Color, b.Color, progress),
            IsInset = a.IsInset,
        };
    }

    private static double Lerp(double from, double to, double progress) => from + (to - from) * progress;

    private static Color Lerp(Color from, Color to, double progress) => Color.FromArgb(
        (byte)Math.Round(Lerp(from.A, to.A, progress)),
        (byte)Math.Round(Lerp(from.R, to.R, progress)),
        (byte)Math.Round(Lerp(from.G, to.G, progress)),
        (byte)Math.Round(Lerp(from.B, to.B, progress)));

    private void Stop()
    {
        _tween?.Dispose();
        _tween = null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Stop();
        _owner.PropertyChanged -= OnPropertyChanged;
        _owner.AttachedToVisualTree -= OnAttached;
        _owner.DetachedFromVisualTree -= OnDetached;
        _owner.ActualThemeVariantChanged -= OnThemeVariantChanged;
        _owner.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
        _owner.RemoveHandler(InputElement.KeyUpEvent, OnKeyUp);
    }

    private enum Interaction { Press, Hover, Focus, Drag }
}