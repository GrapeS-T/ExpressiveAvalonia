using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Tokens;

namespace ExpressiveAvalonia.Theming;

/// <summary>
/// Interruptible unit-mass springs. Retargeting preserves position and velocity, and a spring
/// created from a <see cref="MaterialSpring"/> follows live motion-scheme changes the same way.
/// </summary>
public sealed partial class MaterialMotion : AvaloniaObject
{
    public static readonly AttachedProperty<bool> IsReducedProperty = AvaloniaProperty.RegisterAttached<MaterialMotion, StyledElement, bool>("IsReduced", inherits: true);
    public static readonly AttachedProperty<MaterialMotionScheme> SchemeProperty =
        AvaloniaProperty.RegisterAttached<MaterialMotion, StyledElement, MaterialMotionScheme>("Scheme", MaterialMotionScheme.Standard, inherits: true);
    public static MaterialMotionScheme GetScheme(AvaloniaObject owner) => owner.GetValue(SchemeProperty);
    public static void SetScheme(AvaloniaObject owner, MaterialMotionScheme scheme) => owner.SetValue(SchemeProperty, scheme);
    public static bool IsSystemReduced => _systemReduced;
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

    /// <summary>Creates a spring with explicit physical parameters and a value-unit visibility threshold.</summary>
    public static SpringAnimation Animate(Control owner, double from, double to, Action<double> apply,
        double stiffness, double dampingRatio, double visibilityThreshold = 0.01, double initialVelocity = 0)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(apply);
        if (!double.IsFinite(to)) throw new ArgumentOutOfRangeException(nameof(to));
        var animation = new SpringAnimation(owner, from, to, apply, stiffness, dampingRatio, visibilityThreshold, initialVelocity);
        animation.Retarget(to);
        return animation;
    }

    /// <summary>
    /// Creates a spring from the owner's inherited Material motion scheme. Unlike the explicit
    /// stiffness/damping overload, this spring keeps following <see cref="SchemeProperty"/>: a live
    /// Standard/Expressive switch resamples the outgoing solver and continues with the newly
    /// resolved coefficients at the same position and velocity.
    /// </summary>
    public static SpringAnimation Animate(Control owner, double from, double to, Action<double> apply,
        MaterialSpring spring = MaterialSpring.SpatialFast, double visibilityThreshold = 0.01, double initialVelocity = 0)
    {
        var (stiffness, damping) = MaterialSpringTokens.Get(spring, GetScheme(owner));
        var animation = Animate(owner, from, to, apply, stiffness, damping, visibilityThreshold, initialVelocity);
        // The initial segment already used the coefficients resolved for the current scheme; the
        // marker makes later retargets and scheme switches re-resolve them.
        animation._spring = spring;
        return animation;
    }

    public sealed class SpringAnimation : IDisposable
    {
        internal Control Owner { get; }
        internal bool IsRunning => _running;
        /// <summary>Current analytical velocity, sampled at the time of access.</summary>
        public double Velocity { get { if (_running) Sample(); return _velocity; } }
        /// <summary>Coefficients the running segment currently integrates with.</summary>
        internal (double Stiffness, double DampingRatio) Coefficients => (_stiffness, _dampingRatio);
        internal (double Position, double Velocity) SampleState()
        {
            if (_running) Sample();
            return (_position, _velocity);
        }
        private readonly Action<double> _apply;
        private SpringSimulation _simulation;
        private double _stiffness, _dampingRatio;
        private readonly double _visibilityThreshold;
        // Set only by the semantic overload; an explicit stiffness/damping spring never retunes.
        internal MaterialSpring? _spring;
        private readonly Action<TimeSpan> _onFrame;
        private readonly Stopwatch _clock = new();
        private double _position, _velocity, _target;
        private double _initialPosition, _initialVelocity;
        private double _durationSeconds;
        private bool _running, _framePending;
        private bool _disposed;

        internal SpringAnimation(Control owner, double from, double to, Action<double> apply,
            double stiffness, double dampingRatio, double visibilityThreshold, double initialVelocity)
        {
            if (!double.IsFinite(from)) throw new ArgumentOutOfRangeException(nameof(from));
            if (!double.IsFinite(initialVelocity)) throw new ArgumentOutOfRangeException(nameof(initialVelocity));
            if (!double.IsFinite(visibilityThreshold) || visibilityThreshold <= 0)
                throw new ArgumentOutOfRangeException(nameof(visibilityThreshold));
            _simulation = new SpringSimulation(stiffness, dampingRatio);
            _stiffness = stiffness; _dampingRatio = dampingRatio; _visibilityThreshold = visibilityThreshold;
            Owner = owner; _position = from; _initialPosition = from; _target = to; _apply = apply;
            _velocity = _initialVelocity = initialVelocity;
            _onFrame = Frame;
            owner.DetachedFromVisualTree += Detached;
            owner.PropertyChanged += OwnerChanged;
        }

        public void Retarget(double target)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!double.IsFinite(target)) throw new ArgumentOutOfRangeException(nameof(target));
            if (_running && target == _target) return;
            // Sample the outgoing solver before any coefficient swap so this segment keeps the
            // current position and velocity instead of restarting from a stale spec.
            if (_running) Sample();
            // A semantic spring re-resolves on every retarget, so a scheme switch that arrived
            // before this call cannot leave the previous coefficients in place.
            UpdateSemanticCoefficients();
            _target = target;
            if (GetIsReduced(Owner) || !Owner.IsEffectivelyVisible || !Owner.IsAttachedToVisualTree()) { Finish(); return; }
            _initialPosition = _position;
            _initialVelocity = _velocity;
            _durationSeconds = EstimateDuration();
            if (_durationSeconds <= 0) { Finish(); return; }
            _clock.Restart();
            if (!_running)
            {
                _running = true;
                Active.Add(new WeakReference<SpringAnimation>(this));
                EnsureSettingsTimer();
            }
            RequestFrame();
        }

        private double EstimateDuration() =>
            SpringDuration.EstimateMilliseconds(_stiffness, _dampingRatio,
                _initialVelocity / _visibilityThreshold, (_initialPosition - _target) / _visibilityThreshold, 1) / 1000d;

        /// <summary>
        /// Re-resolves the semantic spring's coefficients for the owner's current scheme. Returns
        /// true when they changed; the caller is responsible for having sampled the outgoing solver
        /// first so the new coefficients continue from the current state.
        /// </summary>
        private bool UpdateSemanticCoefficients()
        {
            if (_spring is not { } spring) return false;
            var (stiffness, damping) = MaterialSpringTokens.Get(spring, GetScheme(Owner));
            if (stiffness == _stiffness && damping == _dampingRatio) return false;
            _stiffness = stiffness;
            _dampingRatio = damping;
            _simulation = new SpringSimulation(stiffness, damping);
            return true;
        }

        /// <summary>
        /// Live <see cref="SchemeProperty"/> switch: material tokens changed under a running
        /// semantic spring, so continue the current target with the new physical coefficients.
        /// Position and velocity are carried over; nothing is restarted globally.
        /// </summary>
        private void SchemeChanged()
        {
            if (_spring is null) return;
            if (!_running)
            {
                // Nothing is in flight; the next Retarget resolves the new coefficients anyway.
                UpdateSemanticCoefficients();
                return;
            }
            Sample();
            if (!UpdateSemanticCoefficients()) return;
            _initialPosition = _position;
            _initialVelocity = _velocity;
            _durationSeconds = EstimateDuration();
            if (_durationSeconds <= 0) { Finish(); return; }
            _clock.Restart();
            RequestFrame();
        }

        private void RequestFrame()
        {
            if (_framePending) return;
            if (TopLevel.GetTopLevel(Owner) is not { } root) { Finish(); return; }
            _framePending = true;
            root.RequestAnimationFrame(_onFrame);
        }

        private void Frame(TimeSpan timestamp)
        {
            _framePending = false;
            if (!_running || _disposed) return;
            if (GetIsReduced(Owner) || !Owner.IsEffectivelyVisible || !Owner.IsAttachedToVisualTree()) { Finish(); return; }
            if (_clock.Elapsed.TotalSeconds >= _durationSeconds) { Finish(); return; }
            Sample();
            _apply(_position);
            if (_running && !_disposed) RequestFrame();
        }

        private void Sample()
        {
            var elapsed = _clock.Elapsed.TotalSeconds;
            if (elapsed >= _durationSeconds)
            {
                _position = _target;
                _velocity = 0;
            }
            else
                (_position, _velocity) = _simulation.Evaluate(
                    _initialPosition, _initialVelocity, _target, elapsed);
        }

        internal void Finish()
        {
            Stop();
            _position = _target; _velocity = 0;
            _apply(_target);
        }

        private void Stop()
        {
            _running = false;
            _clock.Stop();
            for (var i = Active.Count - 1; i >= 0; i--)
                if (!Active[i].TryGetTarget(out var candidate) || ReferenceEquals(candidate, this)) Active.RemoveAt(i);
            StopSettingsTimerIfIdle();
        }
        private void Detached(object? sender, VisualTreeAttachmentEventArgs e) { if (!_disposed) Finish(); }
        private void OwnerChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (_disposed) return;
            if (e.Property == SchemeProperty)
            {
                // The scheme is inherited, so an ancestor switch reaches the owner here.
                SchemeChanged();
                return;
            }
            if (e.Property == IsReducedProperty || e.Property == Visual.IsVisibleProperty)
                if (GetIsReduced(Owner) || !Owner.IsEffectivelyVisible) Finish();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
            Owner.DetachedFromVisualTree -= Detached; Owner.PropertyChanged -= OwnerChanged;
        }
    }
}
