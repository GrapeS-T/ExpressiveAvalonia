using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace ExpressiveAvalonia.Theming;

public sealed partial class MaterialMotion
{
    /// <summary>Starts a finite frame-driven tween. Retargeting samples the current value and restarts its delay and duration.</summary>
    public static TweenAnimation Tween(Control owner, double from, double to, double durationSeconds,
        double delaySeconds, Action<double> apply, Func<double, double>? easing = null)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(apply);
        if (!double.IsFinite(to)) throw new ArgumentOutOfRangeException(nameof(to));
        var animation = new TweenAnimation(owner, from, durationSeconds, delaySeconds, apply, easing);
        animation.Retarget(to);
        return animation;
    }

    public sealed class TweenAnimation : IDisposable
    {
        private readonly Control _owner;
        private readonly Action<double> _apply;
        private readonly Func<double, double>? _easing;
        private readonly double _delay;
        private readonly Action<TimeSpan> _onFrame;
        private readonly Stopwatch _clock = new();
        private double _from, _value, _target, _duration;
        private bool _running, _pending, _disposed;

        internal TweenAnimation(Control owner, double from, double duration, double delay,
            Action<double> apply, Func<double, double>? easing)
        {
            if (!double.IsFinite(from)) throw new ArgumentOutOfRangeException(nameof(from));
            ValidateDuration(duration);
            if (!double.IsFinite(delay) || delay < 0) throw new ArgumentOutOfRangeException(nameof(delay));
            _owner = owner; _from = _value = _target = from; _duration = duration; _delay = delay;
            _apply = apply; _easing = easing; _onFrame = Frame;
            owner.DetachedFromVisualTree += Detached;
            owner.PropertyChanged += Changed;
        }

        public void Retarget(double to) => Retarget(to, _duration);

        public void Retarget(double to, double durationSeconds)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!double.IsFinite(to)) throw new ArgumentOutOfRangeException(nameof(to));
            ValidateDuration(durationSeconds);
            if (_running && to == _target && durationSeconds == _duration) return;
            if (_running) Sample();
            _from = _value; _target = to; _duration = durationSeconds;
            if ((_duration == 0 && _delay == 0) || GetIsReduced(_owner) || !_owner.IsEffectivelyVisible || !_owner.IsAttachedToVisualTree())
            {
                Finish();
                return;
            }
            _clock.Restart();
            if (!_running) ReducedMotionChanged += ReducedChanged;
            _running = true;
            _apply(_from);
            if (_running && !_disposed) Request();
        }

        private static void ValidateDuration(double duration)
        {
            if (!double.IsFinite(duration) || duration < 0) throw new ArgumentOutOfRangeException(nameof(duration));
        }

        private void Request()
        {
            if (_pending) return;
            if (TopLevel.GetTopLevel(_owner) is not { } root) { Finish(); return; }
            _pending = true;
            root.RequestAnimationFrame(_onFrame);
        }

        private void Sample()
        {
            var elapsed = _clock.Elapsed.TotalSeconds - _delay;
            if (elapsed < 0) { _value = _from; return; }
            var fraction = _duration == 0 ? 1 : Math.Min(elapsed / _duration, 1);
            var eased = _easing is null ? fraction : _easing(fraction);
            _value = fraction >= 1 ? _target : _from + (_target - _from) * eased;
        }

        private void Frame(TimeSpan timestamp)
        {
            _pending = false;
            if (!_running || _disposed) return;
            if (GetIsReduced(_owner) || !_owner.IsEffectivelyVisible || !_owner.IsAttachedToVisualTree() ||
                _clock.Elapsed.TotalSeconds >= _delay + _duration) { Finish(); return; }
            Sample();
            _apply(_value);
            if (_running && !_disposed) Request();
        }

        /// <summary>Stops frame requests and applies the exact destination.</summary>
        public void Finish()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            Stop();
            _value = _target;
            _apply(_target);
        }

        private void Stop()
        {
            _running = false;
            _clock.Stop();
            ReducedMotionChanged -= ReducedChanged;
        }

        private void Detached(object? sender, VisualTreeAttachmentEventArgs e)
        {
            // Event invocation snapshots can still contain this handler after an earlier owner
            // handler disposed the tween. Cancellation must not finish or apply its old target.
            if (!_disposed) Finish();
        }
        private void ReducedChanged(object? sender, EventArgs e)
        {
            if (!_disposed && GetIsReduced(_owner)) Finish();
        }
        private void Changed(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (!_disposed && (e.Property == IsReducedProperty || e.Property == Visual.IsVisibleProperty) &&
                (GetIsReduced(_owner) || !_owner.IsEffectivelyVisible)) Finish();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
            _owner.DetachedFromVisualTree -= Detached;
            _owner.PropertyChanged -= Changed;
        }
    }
}
