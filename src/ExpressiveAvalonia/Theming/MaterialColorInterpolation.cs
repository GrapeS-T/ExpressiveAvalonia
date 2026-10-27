using Avalonia.Media;

namespace ExpressiveAvalonia.Theming;

internal readonly record struct MaterialColorVector(double Alpha, double L, double A, double B)
{
    internal static MaterialColorVector FromColor(Color color)
    {
        static double Linear(byte c)
        {
            var value = c / 255d;
            return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }
        var r = Linear(color.R);
        var g = Linear(color.G);
        var b = Linear(color.B);
        // Compose's XYZ/D50 transforms compose to the sRGB/D65 Oklab matrix here.
        var l = Math.Cbrt(0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b);
        var m = Math.Cbrt(0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b);
        var s = Math.Cbrt(0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b);
        return new(color.A / 255d,
            0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s,
            1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s,
            0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s);
    }

    internal Color ToColor()
    {
        var lightness = Math.Clamp(L, 0, 1);
        var a = Math.Clamp(A, -0.5, 0.5);
        var b = Math.Clamp(B, -0.5, 0.5);
        var l = lightness + 0.3963377774 * a + 0.2158037573 * b;
        var m = lightness - 0.1055613458 * a - 0.0638541728 * b;
        var s = lightness - 0.0894841775 * a - 1.2914855480 * b;
        l *= l * l; m *= m * m; s *= s * s;
        static byte Encoded(double value)
        {
            value = value <= 0.0031308 ? 12.92 * value : 1.055 * Math.Pow(value, 1 / 2.4) - 0.055;
            return (byte)Math.Round(Math.Clamp(value, 0, 1) * 255);
        }
        return Color.FromArgb((byte)Math.Round(Math.Clamp(Alpha, 0, 1) * 255),
            Encoded(4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s),
            Encoded(-1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s),
            Encoded(-0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s));
    }

    internal static MaterialColorVector Lerp(MaterialColorVector from, MaterialColorVector to, double fraction) =>
        new(from.Alpha + (to.Alpha - from.Alpha) * fraction,
            from.L + (to.L - from.L) * fraction,
            from.A + (to.A - from.A) * fraction,
            from.B + (to.B - from.B) * fraction);
}
