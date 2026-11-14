using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Theming;
using ExpressiveAvalonia.Tokens;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// Frame-driven corner morph for Material containers whose silhouette follows interaction.
/// </summary>
/// <remarks>
/// Every corner is an independent interruptible spring. When the target shape changes while a
/// morph is running, the affected corners are retargeted with the current position and velocity,
/// so a press → release → checked sequence never snaps or restarts from the resting radius.
/// The default visibility threshold is half a device-independent pixel, the source's shape
/// resolution; callers pass the exact source motion token for their component family.
/// </remarks>
internal sealed class MaterialCornerRadiusTransition : IDisposable
{
    private const double CornerThreshold = 0.5;

    private readonly Control _owner;
    private readonly Action<CornerRadius> _apply;
    private readonly MaterialMotion.SpringAnimation?[] _springs = new MaterialMotion.SpringAnimation?[4];
    private readonly MaterialSpring[] _springKinds = new MaterialSpring[4];
    private CornerRadius _value;
    private CornerRadius _target;
    private MaterialSpring _spring;
    private bool _disposed;

    public MaterialCornerRadiusTransition(Control owner, CornerRadius initial,
        Action<CornerRadius> apply, MaterialSpring spring)
    {
        _owner = owner;
        _apply = apply;
        _spring = spring;
        _value = _target = initial;
        _springKinds[0] = _springKinds[1] = _springKinds[2] = _springKinds[3] = spring;
        _apply(initial);
    }

    /// <summary>Animates to <paramref name="target"/> with the given spring, or snaps when not animating.</summary>
    public void Set(CornerRadius target, bool animate, MaterialSpring? spring = null)
    {
        if (_disposed)
            return;
        if (spring is { } next && next != _spring)
        {
            _spring = next;
            for (var i = 0; i < _springs.Length; i++)
            {
                _springs[i]?.Dispose();
                _springs[i] = null;
                _springKinds[i] = next;
            }
        }

        _target = target;
        if (!animate || MaterialMotion.GetIsReduced(_owner) || !_owner.IsAttachedToVisualTree())
        {
            DisposeSprings();
            _value = target;
            _apply(_value);
            return;
        }

        Spring(0, target.TopLeft);
        Spring(1, target.TopRight);
        Spring(2, target.BottomRight);
        Spring(3, target.BottomLeft);
    }

    private void Spring(int corner, double target)
    {
        if (Math.Abs(Component(corner) - target) < 0.001)
        {
            _springs[corner]?.Dispose();
            _springs[corner] = null;
            SetComponent(corner, target);
            return;
        }

        if (_springs[corner] is { } running)
        {
            running.Retarget(target);
            return;
        }

        _springs[corner] = MaterialMotion.Animate(_owner, Component(corner), target,
            value => SetComponent(corner, value), _springKinds[corner], CornerThreshold);
    }

    private double Component(int corner) => corner switch
    {
        0 => _value.TopLeft,
        1 => _value.TopRight,
        2 => _value.BottomRight,
        _ => _value.BottomLeft
    };

    private void SetComponent(int corner, double value)
    {
        _value = corner switch
        {
            0 => new CornerRadius(value, _value.TopRight, _value.BottomRight, _value.BottomLeft),
            1 => new CornerRadius(_value.TopLeft, value, _value.BottomRight, _value.BottomLeft),
            2 => new CornerRadius(_value.TopLeft, _value.TopRight, value, _value.BottomLeft),
            _ => new CornerRadius(_value.TopLeft, _value.TopRight, _value.BottomRight, value)
        };
        _apply(_value);
    }

    private void DisposeSprings()
    {
        foreach (var spring in _springs)
            spring?.Dispose();
        Array.Clear(_springs);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        DisposeSprings();
    }
}