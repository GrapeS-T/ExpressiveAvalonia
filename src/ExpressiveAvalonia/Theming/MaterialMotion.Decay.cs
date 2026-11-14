using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace ExpressiveAvalonia.Theming;

public sealed partial class MaterialMotion
{
    /// <summary>Android spline decay in DIP and DIP/second. Dispose cancels without snapping.</summary>
    public static DecayAnimation Decay(Control owner, double initialValue, double initialVelocity,
        Action<double, double> apply) => StartDecay(owner, new SplineDecay(initialValue, initialVelocity), apply);

    /// <summary>Compose exponential decay in value units and value units/second.</summary>
    public static DecayAnimation ExponentialDecay(Control owner, double initialValue, double initialVelocity,
        Action<double, double> apply, double frictionMultiplier = 1, double absVelocityThreshold = .1) =>
        StartDecay(owner, new global::ExpressiveAvalonia.Theming.ExponentialDecay(initialValue, initialVelocity, frictionMultiplier, absVelocityThreshold), apply);

    private static DecayAnimation StartDecay(Control owner, IMotionDecay decay, Action<double, double> apply)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(apply);
        var animation = new DecayAnimation(owner, decay, apply);
        animation.Start();
        return animation;
    }

    public sealed class DecayAnimation : IDisposable
    {
        private readonly Control _owner;
        private readonly IMotionDecay _decay;
        private readonly Action<double, double> _apply;
        private readonly Action<TimeSpan> _onFrame;
        private readonly Stopwatch _clock = new();
        private bool _running, _pending, _disposed;
        public bool IsRunning => _running;
        public double Duration => _decay.Duration;
        public double TargetValue => _decay.TargetValue;
        public event EventHandler? Completed;

        internal DecayAnimation(Control owner, IMotionDecay decay, Action<double, double> apply)
        {
            _owner = owner; _decay = decay; _apply = apply; _onFrame = Frame;
            owner.DetachedFromVisualTree += Detached;
            owner.PropertyChanged += Changed;
        }

        internal void Start()
        {
            // Defer even an immediate completion so the caller can store the handle and
            // subscribe before reduced/zero-velocity callbacks run.
            _running = true;
            ReducedMotionChanged += ReducedChanged;
            _clock.Start();
            if (TopLevel.GetTopLevel(_owner) is not { } root)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => { if (!_disposed && _running) Finish(); });
                return;
            }
            _pending = true;
            root.RequestAnimationFrame(_onFrame);
        }

        private void Frame(TimeSpan timestamp)
        {
            _pending = false;
            if (!_running || _disposed) return;
            if (GetIsReduced(_owner) || !_owner.IsEffectivelyVisible || !_owner.IsAttachedToVisualTree() ||
                _clock.Elapsed.TotalSeconds >= _decay.Duration) { Finish(); return; }
            var elapsed = _clock.Elapsed.TotalSeconds;
            _apply(_decay.ValueAt(elapsed), _decay.VelocityAt(elapsed));
            if (!_running || _disposed || _pending) return;
            if (TopLevel.GetTopLevel(_owner) is not { } root) { Finish(); return; }
            _pending = true;
            root.RequestAnimationFrame(_onFrame);
        }

        public void Finish()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_running) return;
            Stop();
            _apply(_decay.TargetValue, 0);
            if (!_disposed) Completed?.Invoke(this, EventArgs.Empty);
        }
        private void Stop()
        {
            _running = false;
            _clock.Stop();
            ReducedMotionChanged -= ReducedChanged;
        }
        private void Detached(object? sender, VisualTreeAttachmentEventArgs e) { if (!_disposed) Finish(); }
        private void ReducedChanged(object? sender, EventArgs e) { if (!_disposed && GetIsReduced(_owner)) Finish(); }
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
