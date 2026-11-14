using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Tokens;

namespace ExpressiveAvalonia.Theming;

/// <summary>
/// Retargetable Oklab color spring matching Compose Color.VectorConverter semantics. Retargeting
/// can change the spring (a directional transition spec or a live motion-scheme switch); the
/// outgoing simulation is sampled first so the painted position and velocity both carry over.
/// </summary>
internal sealed class MaterialColorAnimation : IDisposable
{
    private readonly Control _owner;
    private readonly Action<Color> _apply;
    private SpringSimulation _simulation;
    private MaterialSpring _spring;
    private double _stiffness, _damping;
    private readonly Action<TimeSpan> _onFrame;
    private readonly Stopwatch _clock = new();
    private MaterialColorVector _start, _initialVelocity, _target, _value, _velocity;
    private double _duration;
    private bool _running, _pending, _disposed;

    internal MaterialColorAnimation(Control owner, Color initial, Action<Color> apply, MaterialSpring spring)
    {
        _owner = owner;
        _apply = apply;
        _spring = spring;
        (_stiffness, _damping) = MaterialSpringTokens.Get(spring, MaterialMotion.GetScheme(owner));
        _simulation = new SpringSimulation(_stiffness, _damping);
        _value = _start = _target = MaterialColorVector.FromColor(initial);
        _onFrame = Frame;
        owner.DetachedFromVisualTree += Detached;
        owner.PropertyChanged += Changed;
    }

    /// <summary>Retargets with the coefficients selected at construction or by the last spring.</summary>
    internal void Retarget(Color target) => Retarget(target, _stiffness, _damping);

    /// <summary>
    /// Retargets with <paramref name="spring"/>. When the resolved coefficients change (a
    /// selection direction switch, or a live motion-scheme change), the outgoing simulation is
    /// sampled before the new spec replaces it, so the retarget keeps both the painted position
    /// and the current velocity, as Compose's Transition animateColor does when its spec changes.
    /// </summary>
    internal void Retarget(Color target, MaterialSpring spring)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var (stiffness, damping) = MaterialSpringTokens.Get(spring, MaterialMotion.GetScheme(_owner));
        _spring = spring;
        Retarget(target, stiffness, damping);
    }

    private void Retarget(Color target, double stiffness, double damping)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var vector = MaterialColorVector.FromColor(target);
        if (stiffness == _stiffness && damping == _damping)
        {
            if (_running && vector == _target) return;
            if (_running) Sample();
        }
        else
        {
            // The spec is changing: sample the outgoing simulation before swapping the
            // coefficients, so its position and velocity seed the new spring instead of a
            // position-only restart.
            if (_running) Sample();
            _stiffness = stiffness;
            _damping = damping;
            _simulation = new SpringSimulation(stiffness, damping);
        }
        _target = vector;
        _start = _value;
        _initialVelocity = _velocity;
        double Duration(double from, double to, double velocity) =>
            SpringDuration.EstimateMilliseconds(_stiffness, _damping, velocity / 0.01, (from - to) / 0.01, 1) / 1000d;
        _duration = Math.Max(Math.Max(Duration(_start.Alpha, _target.Alpha, _velocity.Alpha),
                Duration(_start.L, _target.L, _velocity.L)),
            Math.Max(Duration(_start.A, _target.A, _velocity.A), Duration(_start.B, _target.B, _velocity.B)));
        if (_duration <= 0 || MaterialMotion.GetIsReduced(_owner) || !_owner.IsEffectivelyVisible || !_owner.IsAttachedToVisualTree())
        {
            Finish();
            return;
        }
        _clock.Restart();
        if (!_running) MaterialMotion.ReducedMotionChanged += ReducedChanged;
        _running = true;
        Request();
    }

    private void Request()
    {
        if (_pending) return;
        if (TopLevel.GetTopLevel(_owner) is not { } root) { Finish(); return; }
        _pending = true;
        root.RequestAnimationFrame(_onFrame);
    }

    private void Frame(TimeSpan timestamp)
    {
        _pending = false;
        if (!_running || _disposed) return;
        if (MaterialMotion.GetIsReduced(_owner) || !_owner.IsEffectivelyVisible || _clock.Elapsed.TotalSeconds >= _duration)
        {
            Finish();
            return;
        }
        Sample();
        _apply(_value.ToColor());
        if (_running && !_disposed) Request();
    }

    private void Sample()
    {
        var t = _clock.Elapsed.TotalSeconds;
        if (t >= _duration) { _value = _target; _velocity = default; return; }
        var alpha = _simulation.Evaluate(_start.Alpha, _initialVelocity.Alpha, _target.Alpha, t);
        var l = _simulation.Evaluate(_start.L, _initialVelocity.L, _target.L, t);
        var a = _simulation.Evaluate(_start.A, _initialVelocity.A, _target.A, t);
        var b = _simulation.Evaluate(_start.B, _initialVelocity.B, _target.B, t);
        _value = new(alpha.Position, l.Position, a.Position, b.Position);
        _velocity = new(alpha.Velocity, l.Velocity, a.Velocity, b.Velocity);
    }

    private void Finish()
    {
        _running = false;
        MaterialMotion.ReducedMotionChanged -= ReducedChanged;
        _clock.Stop();
        _value = _target;
        _velocity = default;
        _apply(_target.ToColor());
    }

    private void Detached(object? sender, VisualTreeAttachmentEventArgs e) { if (!_disposed) Finish(); }
    private void ReducedChanged(object? sender, EventArgs e)
    {
        if (!_disposed && MaterialMotion.GetIsReduced(_owner)) Finish();
    }
    private void Changed(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_disposed) return;
        if (e.Property == MaterialMotion.SchemeProperty)
        {
            // The scheme is inherited, so an ancestor switch reaches the owner here. Re-resolve
            // the same spring kind and retune the running simulation without dropping velocity.
            var (stiffness, damping) = MaterialSpringTokens.Get(_spring, MaterialMotion.GetScheme(_owner));
            if (stiffness != _stiffness || damping != _damping)
                Retarget(_target.ToColor(), stiffness, damping);
            return;
        }
        if ((e.Property == MaterialMotion.IsReducedProperty || e.Property == Visual.IsVisibleProperty) &&
            (MaterialMotion.GetIsReduced(_owner) || !_owner.IsEffectivelyVisible)) Finish();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _running = false;
        _clock.Stop();
        _owner.DetachedFromVisualTree -= Detached;
        _owner.PropertyChanged -= Changed;
        MaterialMotion.ReducedMotionChanged -= ReducedChanged;
    }
}
