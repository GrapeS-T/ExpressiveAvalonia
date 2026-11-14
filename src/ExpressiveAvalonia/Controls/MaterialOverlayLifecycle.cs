using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Theming;
using ExpressiveAvalonia.Tokens;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// An overlay that remembers the focus it will restore when it closes.
/// </summary>
internal interface IMaterialOverlayFocusChain
{
    /// <summary>The focus target to restore when this overlay closes.</summary>
    IInputElement? RestoreFocusTarget { get; set; }
}

/// <summary>
/// Tracks which Material overlay was opened last in a top level, so a nested layer owns Escape
/// instead of every layer dismissing at once.
/// </summary>
/// <remarks>
/// Android delivers back presses to the topmost dialog or predictive-back callback. Avalonia
/// routes a key along the focused element's tree, which already favours the focused layer, but a
/// toolkit popup or a second overlay opened from the first can leave focus outside the newest
/// layer. This registry is an internal arbitration rule, not a public overlay service.
/// </remarks>
internal static class MaterialOverlayLifecycle
{
    private static readonly ConditionalWeakTable<TopLevel, List<WeakReference<object>>> Owners = new();

    /// <summary>Marks <paramref name="overlay"/> as the most recently presented layer.</summary>
    public static void Register(TopLevel? owner, object overlay)
    {
        if (owner is null)
            return;
        ArgumentNullException.ThrowIfNull(overlay);

        var stack = Owners.GetOrCreateValue(owner);
        for (var i = stack.Count - 1; i >= 0; i--)
        {
            if (!stack[i].TryGetTarget(out var candidate))
                stack.RemoveAt(i);
            else if (ReferenceEquals(candidate, overlay))
                stack.RemoveAt(i);
        }

        stack.Add(new WeakReference<object>(overlay));
    }

    /// <summary>Removes a layer that has finished closing or has been detached.</summary>
    public static void Unregister(TopLevel? owner, object overlay)
    {
        if (owner is null || !Owners.TryGetValue(owner, out var stack))
            return;

        for (var i = stack.Count - 1; i >= 0; i--)
        {
            if (!stack[i].TryGetTarget(out var candidate) || ReferenceEquals(candidate, overlay))
                stack.RemoveAt(i);
        }
    }

    /// <summary>
    /// Replaces the saved focus target of newer layers that captured focus inside
    /// <paramref name="closing"/>. Removing a lower modal must not orphan the restoration chain
    /// of the layers still above it.
    /// </summary>
    public static void RebaseFocusTargets(TopLevel? owner, object closing, IInputElement? replacement)
    {
        if (owner is null || !Owners.TryGetValue(owner, out var stack))
            return;

        var closingIndex = -1;
        for (var i = stack.Count - 1; i >= 0; i--)
        {
            if (!stack[i].TryGetTarget(out var candidate))
            {
                stack.RemoveAt(i);
                continue;
            }
            if (ReferenceEquals(candidate, closing))
            {
                closingIndex = i;
                break;
            }
        }
        if (closingIndex < 0)
            return;

        // Only layers above the closing one captured focus from inside it.
        for (var i = stack.Count - 1; i > closingIndex; i--)
        {
            if (!stack[i].TryGetTarget(out var candidate))
            {
                stack.RemoveAt(i);
                continue;
            }
            if (candidate is IMaterialOverlayFocusChain layer &&
                layer.RestoreFocusTarget is { } target && IsWithin(target, closing))
                layer.RestoreFocusTarget = replacement;
        }
    }

    private static bool IsWithin(IInputElement target, object overlay) =>
        ReferenceEquals(target, overlay) ||
        (overlay is Visual overlayVisual && target is Visual targetVisual &&
         (ReferenceEquals(targetVisual, overlayVisual) || overlayVisual.IsVisualAncestorOf(targetVisual)));

    /// <summary>Whether <paramref name="overlay"/> is the newest still-open layer of the owner.</summary>
    public static bool IsTopmost(TopLevel? owner, object overlay)
    {
        if (owner is null || !Owners.TryGetValue(owner, out var stack))
            return true;

        for (var i = stack.Count - 1; i >= 0; i--)
        {
            if (!stack[i].TryGetTarget(out var candidate))
            {
                stack.RemoveAt(i);
                continue;
            }

            return ReferenceEquals(candidate, overlay);
        }

        return true;
    }
}

/// <summary>
/// Drives one scalar with a Material spring and reports when the value reaches its target.
/// </summary>
/// <remarks>
/// <see cref="MaterialMotion.SpringAnimation"/> deliberately has no completion event; it applies
/// the exact target once as its final frame. This driver observes that exact frame, which keeps
/// component code free of duration timers. Retargeting with the same spring preserves position
/// and velocity; changing the physical spec carries the sampled velocity into the new spring.
/// </remarks>
internal sealed class MaterialSpringValue : IDisposable
{
    private readonly Control _owner;
    private readonly Action<double> _apply;
    private MaterialMotion.SpringAnimation? _animation;
    private MaterialSpring? _spring;
    private Action? _settled;
    private double _value;
    private double _target;
    private int _generation;
    private bool _disposed;

    public MaterialSpringValue(Control owner, double initial, Action<double> apply)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        _value = initial;
        _target = initial;
        _apply(initial);
    }

    /// <summary>The most recently applied value.</summary>
    public double Value => _value;

    /// <summary>The spring currently driving the value, when one has been created.</summary>
    public MaterialSpring? Spring => _spring;

    /// <summary>Whether the value has not yet reached its target.</summary>
    public bool IsRunning => _animation is not null && _value != _target;

    /// <summary>The analytical velocity of the active spring, in value units per second.</summary>
    public double Velocity => _animation?.Velocity ?? 0;

    /// <summary>Places the value at <paramref name="value"/> without motion.</summary>
    public void JumpTo(double value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _generation++;
        _animation?.Dispose();
        _animation = null;
        _spring = null;
        _settled = null;
        _value = value;
        _target = value;
        _apply(value);
    }

    /// <summary>
    /// Springs to <paramref name="target"/>. <paramref name="settled"/> runs once when the exact
    /// target is applied, including the reduced-motion immediate path.
    /// </summary>
    public void AnimateTo(double target, MaterialSpring spring, Action? settled = null,
        double visibilityThreshold = 0.5)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _target = target;
        _settled = settled;

        if (_animation is not null && _spring == spring)
        {
            // Same physical spec: the engine retargets from the sampled position and velocity.
            _animation.Retarget(target);
            return;
        }

        _generation++;
        var generation = _generation;
        // A direction change uses the source's other spec (show/hide). Carry the analytical
        // velocity into the new spring so an interrupted gesture reverses without a hitch.
        var initialVelocity = _animation?.Velocity ?? 0;
        _animation?.Dispose();
        _animation = null;
        _spring = spring;

        var animation = MaterialMotion.Animate(_owner, _value, target, OnSample, spring,
            visibilityThreshold, initialVelocity);
        if (_generation == generation && !_disposed)
            _animation = animation;
        else
            animation.Dispose();
    }

    /// <summary>
    /// Moves the target without replacing the active spring or its completion. Used when anchors
    /// change during a resize; position and velocity stay continuous.
    /// </summary>
    public void RetargetTo(double target)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _target = target;
        if (_animation is not null)
            _animation.Retarget(target);
        else
            JumpTo(target);
    }

    private void OnSample(double value)
    {
        if (_disposed)
            return;

        _value = value;
        _apply(value);

        if (value == _target && _settled is { } settled)
        {
            _settled = null;
            settled();
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _generation++;
        _animation?.Dispose();
        _animation = null;
        _settled = null;
    }
}