using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Tokens;

namespace ExpressiveAvalonia.Theming;

public enum MaterialMotionMode { Expressive, Reduced }

/// <summary>Interruptible unit-mass springs. Retargeting preserves position and velocity.</summary>
public sealed class MaterialMotion : AvaloniaObject
{
    public static readonly AttachedProperty<bool> IsReducedProperty = AvaloniaProperty.RegisterAttached<MaterialMotion, StyledElement, bool>("IsReduced", inherits: true);
    private static readonly List<WeakReference<SpringAnimation>> Active = [];
    private static DispatcherTimer? _settingsTimer;
    private static bool _systemReduced = ReadSystemReducedMotion();
    private static EventHandler? _reducedMotionChanged;

    /// <summary>Raised when the platform reduced-motion preference changes.</summary>
    public static event EventHandler? ReducedMotionChanged
    {
        add
        {
            _reducedMotionChanged += value;
            EnsureSettingsTimer();
        }
        remove
        {
            _reducedMotionChanged -= value;
            StopSettingsTimerIfIdle();
        }
    }

    public static bool GetIsReduced(AvaloniaObject value) => value.GetValue(IsReducedProperty) || _systemReduced;
    public static void SetIsReduced(AvaloniaObject value, bool reduced) => value.SetValue(IsReducedProperty, reduced);

    // Avalonia 12 exposes no cross-platform reduced-motion setting. Honor Windows' live
    // animation preference; other platforms can explicitly set the inherited IsReduced flag.
    private static bool ReadSystemReducedMotion() => OperatingSystem.IsWindows() && SystemParametersInfo(0x1042, 0, out var enabled, 0) && !enabled;
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(uint action, uint parameter, [MarshalAs(UnmanagedType.Bool)] out bool value, uint flags);

    /// <summary>
    /// Starts the one-second accessibility poll only while animations or listeners need it,
    /// so merely touching this class no longer keeps a timer alive for the process lifetime.
    /// </summary>
    private static void EnsureSettingsTimer()
    {
        _settingsTimer ??= new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => RefreshAccessibility());
        if (!_settingsTimer.IsEnabled)
            _settingsTimer.Start();
    }

    private static void StopSettingsTimerIfIdle()
    {
        if (_settingsTimer is { IsEnabled: true } && Active.Count == 0 && _reducedMotionChanged is null)
            _settingsTimer.Stop();
    }

    private static void RefreshAccessibility()
    {
        var reduced = ReadSystemReducedMotion();
        if (_systemReduced != reduced)
        {
            _systemReduced = reduced;
            _reducedMotionChanged?.Invoke(null, EventArgs.Empty);
        }
        for (var i = Active.Count - 1; i >= 0; i--)
        {
            if (!Active[i].TryGetTarget(out var animation)) Active.RemoveAt(i);
            else if (GetIsReduced(animation.Owner)) animation.Finish();
        }
        StopSettingsTimerIfIdle();
    }

    /// <summary>Creates a spring using Material Expressive fast spatial parameters (1400, 0.9).</summary>
    public static SpringAnimation Animate(Control owner, double from, double to, Action<double> apply,
        double stiffness = 1400, double dampingRatio = 0.9)
    {
        var animation = new SpringAnimation(owner, from, to, apply, stiffness, dampingRatio);
        Active.Add(new WeakReference<SpringAnimation>(animation));
        EnsureSettingsTimer();
        animation.Retarget(to);
        return animation;
    }

    /// <summary>Creates a spring from the AndroidX ExpressiveMotionTokens set.</summary>
    public static SpringAnimation Animate(Control owner, double from, double to, Action<double> apply, MaterialSpring spring)
    {
        var (stiffness, damping) = MaterialSpringTokens.Get(spring);
        return Animate(owner, from, to, apply, stiffness, damping);
    }

    public sealed class SpringAnimation : IDisposable
    {
        internal Control Owner { get; }
        private readonly Action<double> _apply;
        private readonly double _stiffness;
        private readonly double _damping;
        private readonly DispatcherTimer _timer;
        private readonly Stopwatch _clock = new();
        private double _position, _velocity, _target;
        private bool _disposed;

        internal SpringAnimation(Control owner, double from, double to, Action<double> apply, double stiffness, double dampingRatio)
        {
            if (!double.IsFinite(stiffness) || stiffness <= 0 || !double.IsFinite(dampingRatio) || dampingRatio <= 0)
                throw new ArgumentOutOfRangeException(nameof(stiffness));
            Owner = owner; _position = from; _target = to; _apply = apply;
            _stiffness = stiffness; _damping = 2 * dampingRatio * Math.Sqrt(stiffness);
            _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, (_, _) => Tick());
            owner.DetachedFromVisualTree += Detached;
            owner.PropertyChanged += OwnerChanged;
        }

        public void Retarget(double target)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!double.IsFinite(target)) throw new ArgumentOutOfRangeException(nameof(target));
            _target = target;
            if (GetIsReduced(Owner) || !Owner.IsEffectivelyVisible || !Owner.IsAttachedToVisualTree()) { Finish(); return; }
            _clock.Restart(); _timer.Start();
        }

        private void Tick()
        {
            if (GetIsReduced(Owner) || !Owner.IsEffectivelyVisible) { Finish(); return; }
            var remaining = Math.Min(_clock.Elapsed.TotalSeconds, 0.064);
            _clock.Restart();
            // Bounded semi-implicit integration remains stable after delayed UI frames.
            while (remaining > 0)
            {
                var dt = Math.Min(remaining, 1d / 240);
                _velocity += (-_stiffness * (_position - _target) - _damping * _velocity) * dt;
                _position += _velocity * dt;
                remaining -= dt;
            }
            _apply(_position);
            if (Math.Abs(_position - _target) < 0.0001 && Math.Abs(_velocity) < 0.001) Finish();
        }

        internal void Finish() { _timer.Stop(); _position = _target; _velocity = 0; _apply(_target); }
        private void Detached(object? sender, VisualTreeAttachmentEventArgs e) => Finish();
        private void OwnerChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == IsReducedProperty || e.Property == Visual.IsVisibleProperty)
                if (GetIsReduced(Owner) || !Owner.IsEffectivelyVisible) Finish();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true; _timer.Stop();
            Owner.DetachedFromVisualTree -= Detached; Owner.PropertyChanged -= OwnerChanged;
            for (var i = Active.Count - 1; i >= 0; i--)
                if (!Active[i].TryGetTarget(out var candidate) || ReferenceEquals(candidate, this))
                    Active.RemoveAt(i);
            StopSettingsTimerIfIdle();
        }
    }
}
