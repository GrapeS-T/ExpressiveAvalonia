namespace ExpressiveAvalonia.Theming;

/// <summary>Analytical unit-mass spring, evaluated from the start of a target segment.</summary>
internal readonly struct SpringSimulation
{
    private readonly double _naturalFrequency;
    private readonly double _dampingRatio;

    internal SpringSimulation(double stiffness, double dampingRatio)
    {
        if (!double.IsFinite(stiffness) || stiffness <= 0)
            throw new ArgumentOutOfRangeException(nameof(stiffness));
        if (!double.IsFinite(dampingRatio) || dampingRatio <= 0)
            throw new ArgumentOutOfRangeException(nameof(dampingRatio));
        _naturalFrequency = Math.Sqrt(stiffness);
        _dampingRatio = dampingRatio;
    }

    internal (double Position, double Velocity) Evaluate(
        double initialPosition, double initialVelocity, double target, double elapsedSeconds)
    {
        var adjustedDisplacement = initialPosition - target;
        var dampingSquared = _dampingRatio * _dampingRatio;
        var r = -_dampingRatio * _naturalFrequency;
        double displacement, velocity;

        if (_dampingRatio > 1)
        {
            var s = _naturalFrequency * Math.Sqrt(dampingSquared - 1);
            var gammaPlus = r + s;
            var gammaMinus = r - s;
            var coefficientB = (gammaMinus * adjustedDisplacement - initialVelocity) / (gammaMinus - gammaPlus);
            var coefficientA = adjustedDisplacement - coefficientB;
            var decayMinus = Math.Exp(gammaMinus * elapsedSeconds);
            var decayPlus = Math.Exp(gammaPlus * elapsedSeconds);
            displacement = coefficientA * decayMinus + coefficientB * decayPlus;
            velocity = coefficientA * gammaMinus * decayMinus + coefficientB * gammaPlus * decayPlus;
        }
        else if (_dampingRatio == 1)
        {
            var coefficientB = initialVelocity + _naturalFrequency * adjustedDisplacement;
            var decay = Math.Exp(r * elapsedSeconds);
            displacement = (adjustedDisplacement + coefficientB * elapsedSeconds) * decay;
            velocity = displacement * -_naturalFrequency + coefficientB * decay;
        }
        else
        {
            var dampedFrequency = _naturalFrequency * Math.Sqrt(1 - dampingSquared);
            var sinCoefficient = (-r * adjustedDisplacement + initialVelocity) / dampedFrequency;
            var angle = dampedFrequency * elapsedSeconds;
            var decay = Math.Exp(r * elapsedSeconds);
            var cosine = Math.Cos(angle);
            var sine = Math.Sin(angle);
            displacement = decay * (adjustedDisplacement * cosine + sinCoefficient * sine);
            velocity = displacement * r + decay *
                (-dampedFrequency * adjustedDisplacement * sine + dampedFrequency * sinCoefficient * cosine);
        }

        return (displacement + target, velocity);
    }
}
