namespace ExpressiveAvalonia.Theming;

internal static class SpringDuration
{
    internal static long EstimateMilliseconds(double stiffness, double dampingRatio,
        double initialVelocity, double initialDisplacement, double threshold)
    {
        if (initialDisplacement == 0 && initialVelocity == 0) return 0;
        if (dampingRatio == 0) return long.MaxValue / 1_000_000;
        var damping = 2 * dampingRatio * Math.Sqrt(stiffness);
        var partial = damping * damping - 4 * stiffness;
        var real = partial < 0 ? 0 : Math.Sqrt(partial);
        var imaginary = partial < 0 ? Math.Sqrt(Math.Abs(partial)) : 0;
        var r1 = (-damping + real) * 0.5;
        var r2 = (-damping - real) * 0.5;
        var v = initialDisplacement < 0 ? -initialVelocity : initialVelocity;
        var p = Math.Abs(initialDisplacement);
        var seconds = dampingRatio > 1 ? OverDamped(r1, r2, p, v, threshold)
            : dampingRatio < 1 ? UnderDamped(r1, imaginary * 0.5, p, v, threshold)
            : CriticallyDamped(r1, p, v, threshold);
        // Kotlin Double.toLong saturates and maps NaN to zero.
        var milliseconds = seconds * 1000;
        if (double.IsNaN(milliseconds) || milliseconds <= 0) return 0;
        return milliseconds >= long.MaxValue ? long.MaxValue : (long)milliseconds;
    }

    private static double UnderDamped(double r, double imaginary, double p, double v, double delta)
    {
        var c2 = (v - r * p) / imaginary;
        return Math.Log(delta / Math.Sqrt(p * p + c2 * c2)) / r;
    }

    private static double CriticallyDamped(double r, double p, double v, double delta)
    {
        var c1 = p;
        var c2 = v - r * c1;
        var t1 = Math.Log(Math.Abs(delta / c1)) / r;
        var guess = Math.Log(Math.Abs(delta / c2));
        var t2 = guess;
        for (var i = 0; i <= 5; i++) t2 = guess - Math.Log(Math.Abs(t2 / r));
        t2 /= r;
        var current = !double.IsFinite(t1) ? t2 : !double.IsFinite(t2) ? t1 : Math.Max(t1, t2);
        var inflection = -(r * c1 + c2) / (r * c2);
        var x = (c1 + c2 * inflection) * Math.Exp(r * inflection);
        double signedDelta;
        if (double.IsNaN(inflection) || inflection <= 0) signedDelta = -delta;
        else if (-x < delta)
        {
            if (c2 < 0 && c1 > 0) current = 0;
            signedDelta = -delta;
        }
        else
        {
            current = -(2 / r) - c1 / c2;
            signedDelta = delta;
        }
        var difference = double.MaxValue;
        for (var i = 0; difference > 0.001 && i < 100; i++)
        {
            var last = current;
            var decay = Math.Exp(r * current);
            current -= ((c1 + c2 * current) * decay + signedDelta) /
                ((c2 * (r * current + 1) + c1 * r) * decay);
            difference = Math.Abs(last - current);
        }
        return current;
    }

    private static double OverDamped(double r1, double r2, double p, double v, double delta)
    {
        var c2 = (r1 * p - v) / (r1 - r2);
        var c1 = p - c2;
        var t1 = Math.Log(Math.Abs(delta / c1)) / r1;
        var t2 = Math.Log(Math.Abs(delta / c2)) / r2;
        var current = !double.IsFinite(t1) ? t2 : !double.IsFinite(t2) ? t1 : Math.Max(t1, t2);
        var inflection = Math.Log(c1 * r1 / (-c2 * r2)) / (r2 - r1);
        double signedDelta;
        if (double.IsNaN(inflection) || inflection <= 0) signedDelta = -delta;
        else if (-(c1 * Math.Exp(r1 * inflection) + c2 * Math.Exp(r2 * inflection)) < delta)
        {
            if (c2 > 0 && c1 < 0) current = 0;
            signedDelta = -delta;
        }
        else
        {
            current = Math.Log(-(c2 * r2 * r2) / (c1 * r1 * r1)) / (r1 - r2);
            signedDelta = delta;
        }
        if (Math.Abs(c1 * r1 * Math.Exp(r1 * current) + c2 * r2 * Math.Exp(r2 * current)) < 0.0001)
            return current;
        var difference = double.MaxValue;
        for (var i = 0; difference > 0.001 && i < 100; i++)
        {
            var last = current;
            var first = c1 * Math.Exp(r1 * current);
            var second = c2 * Math.Exp(r2 * current);
            current -= (first + second + signedDelta) / (first * r1 + second * r2);
            difference = Math.Abs(last - current);
        }
        return current;
    }
}
