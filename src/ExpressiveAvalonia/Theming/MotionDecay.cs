namespace ExpressiveAvalonia.Theming;

internal interface IMotionDecay
{
    double Duration { get; }
    double TargetValue { get; }
    double ValueAt(double seconds);
    double VelocityAt(double seconds);
}

/// <summary>Compose exponential decay in value units per second, with finite velocity cutoff.</summary>
internal sealed class ExponentialDecay : IMotionDecay
{
    private readonly double _initial, _velocity, _friction;
    public double Duration { get; }
    public double TargetValue { get; }

    public ExponentialDecay(double initialValue, double initialVelocity, double frictionMultiplier = 1,
        double absVelocityThreshold = .1)
    {
        if (!double.IsFinite(initialValue)) throw new ArgumentOutOfRangeException(nameof(initialValue));
        if (!double.IsFinite(initialVelocity)) throw new ArgumentOutOfRangeException(nameof(initialVelocity));
        if (!double.IsFinite(frictionMultiplier) || frictionMultiplier <= 0) throw new ArgumentOutOfRangeException(nameof(frictionMultiplier));
        if (!double.IsFinite(absVelocityThreshold) || absVelocityThreshold <= 0) throw new ArgumentOutOfRangeException(nameof(absVelocityThreshold));
        _initial = initialValue; _velocity = initialVelocity;
        _friction = -4.2 * Math.Max(.0001, frictionMultiplier);
        var threshold = Math.Max(.0000001, absVelocityThreshold);
        if (Math.Abs(initialVelocity) <= threshold) { TargetValue = initialValue; return; }
        var exactDuration = Math.Log(threshold / Math.Abs(initialVelocity)) / _friction;
        Duration = Math.Floor(exactDuration * 1000) / 1000;
        TargetValue = initialValue - initialVelocity / _friction + initialVelocity / _friction * Math.Exp(_friction * exactDuration);
    }

    public double ValueAt(double seconds)
    {
        if (seconds >= Duration) return TargetValue;
        var t = Math.Floor(Math.Max(0, seconds) * 1000) / 1000;
        return _initial - _velocity / _friction + _velocity / _friction * Math.Exp(_friction * t);
    }
    public double VelocityAt(double seconds)
    {
        if (seconds >= Duration) return 0;
        return _velocity * Math.Exp(_friction * Math.Floor(Math.Max(0, seconds) * 1000) / 1000);
    }
}

/// <summary>Android fling spline at density one: Avalonia DIP corresponds to Compose dp.</summary>
internal sealed class SplineDecay : IMotionDecay
{
    // Android ViewConfiguration.getScrollFriction(), not the spline's StartTension (.5).
    private const double ScrollFriction = .015;
    private static readonly double DecelerationRate = Math.Log(.78) / Math.Log(.9);
    private const double PhysicalCoefficient = 9.80665 * 39.37 * 160 * .84;
    private static readonly double[] Positions = BuildPositions();
    private readonly double _initial, _distance;
    public double Duration { get; }
    public double TargetValue => _initial + _distance;

    public SplineDecay(double initialValue, double initialVelocity)
    {
        if (!double.IsFinite(initialValue)) throw new ArgumentOutOfRangeException(nameof(initialValue));
        if (!double.IsFinite(initialVelocity)) throw new ArgumentOutOfRangeException(nameof(initialVelocity));
        _initial = initialValue;
        if (initialVelocity == 0) return;
        var friction = ScrollFriction * PhysicalCoefficient;
        var deceleration = Math.Log(.35 * Math.Abs(initialVelocity) / friction);
        Duration = Math.Floor(1000 * Math.Exp(deceleration / (DecelerationRate - 1))) / 1000;
        _distance = Math.Sign(initialVelocity) * friction * Math.Exp(DecelerationRate / (DecelerationRate - 1) * deceleration);
    }

    private static double[] BuildPositions()
    {
        var positions = new double[101];
        double minimum = 0;
        for (var i = 0; i < 100; i++)
        {
            var alpha = i / 100d;
            double maximum = 1, x, coefficient;
            while (true)
            {
                x = minimum + (maximum - minimum) / 2;
                coefficient = 3 * x * (1 - x);
                var time = coefficient * ((1 - x) * .175 + x * .35) + x * x * x;
                if (Math.Abs(time - alpha) < 1e-5) break;
                if (time > alpha) maximum = x; else minimum = x;
            }
            positions[i] = coefficient * ((1 - x) * .5 + x) + x * x * x;
        }
        positions[100] = 1;
        return positions;
    }

    private (double Distance, double Velocity) Coefficients(double seconds)
    {
        if (Duration <= 0 || seconds >= Duration) return (1, 0);
        var time = Math.Clamp(Math.Floor(seconds * 1000) / (Duration * 1000), 0, 1);
        var index = Math.Min(99, (int)(time * 100));
        var velocity = (Positions[index + 1] - Positions[index]) * 100;
        return (Positions[index] + (time - index / 100d) * velocity, velocity);
    }
    public double ValueAt(double seconds) => _initial + _distance * Coefficients(seconds).Distance;
    public double VelocityAt(double seconds) => Duration <= 0 ? 0 : _distance / Duration * Coefficients(seconds).Velocity;
}
