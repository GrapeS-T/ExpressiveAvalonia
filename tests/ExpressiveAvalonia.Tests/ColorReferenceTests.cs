using Avalonia.Media;
using ExpressiveAvalonia.Tokens;
using MaterialColorUtilities.HCT;
using MaterialColorUtilities.Utils;
using Xunit;

namespace ExpressiveAvalonia.Tests;

// Google MCU reference commit bd811e4ca2ab94c4ce3cadc89c19b1820b9704b3:
// typescript/hct/hct_test.ts, palettes/palettes_test.ts, dynamiccolor/fixed_dynamic_color_test.ts
// swift/Tests/MaterialColorUtilitiesTests/SchemeTests/SchemeTonalSpotTests.swift
public class ColorReferenceTests
{
    private static uint Pack(ArgbColor color) => ((uint)color.Alpha << 24) | ((uint)color.Red << 16) | ((uint)color.Green << 8) | color.Blue;
    private static ArgbColor Argb(uint value) => new((byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value);

    [Theory]
    [InlineData(0xFF00FF00u, 142.139, 108.410, 87.737)]
    [InlineData(0xFF0000FFu, 282.788, 87.230, 32.302)]
    public void HctMatchesGoogleNumericVectors(uint argb, double hue, double chroma, double tone)
    {
        var hct = Hct.From(Argb(argb));
        Assert.InRange(hct.Hue, hue - .005, hue + .005);
        Assert.InRange(hct.Chroma, chroma - .005, chroma + .005);
        Assert.InRange(hct.Tone, tone - .005, tone + .005);
    }

    [Theory]
    [InlineData(0xFFFE0315u)] [InlineData(0xFF15FE03u)] [InlineData(0xFF0315FEu)]
    public void HctSolverRoundTripsHighChroma(uint argb)
    {
        var hct = Hct.From(Argb(argb));
        Assert.Equal(argb, Pack(Hct.From(hct.Hue, hct.Chroma, hct.Tone).Argb));
    }

    [Theory]
    [InlineData(100, 0xFFFFFFFFu)] [InlineData(95, 0xFFF1EFFFu)]
    [InlineData(90, 0xFFE0E0FFu)] [InlineData(80, 0xFFBEC2FFu)]
    [InlineData(70, 0xFF9DA3FFu)] [InlineData(60, 0xFF7C84FFu)]
    [InlineData(50, 0xFF5A64FFu)] [InlineData(40, 0xFF343DFFu)]
    [InlineData(30, 0xFF0000EFu)] [InlineData(20, 0xFF0001ACu)]
    [InlineData(10, 0xFF00006Eu)] [InlineData(0, 0xFF000000u)]
    public void OutOfGamutBluePaletteMatchesGoogle(double tone, uint expected)
    {
        var blue = Hct.From(Argb(0xFF0000FF));
        Assert.Equal(expected, Pack(Hct.From(blue.Hue, blue.Chroma, tone).Argb));
    }

    [Theory]
    [InlineData("#0000FF", false, "Primary", "#555992")]
    [InlineData("#0000FF", true, "Primary", "#BEC2FF")]
    [InlineData("#6750A4", false, "Primary", "#65558F")]
    [InlineData("#6750A4", true, "Primary", "#CFBDFE")]
    [InlineData("#6750A4", false, "Secondary", "#625B71")]
    [InlineData("#6750A4", true, "Secondary", "#CBC2DB")]
    [InlineData("#6750A4", false, "Tertiary", "#7E5260")]
    [InlineData("#6750A4", true, "Tertiary", "#EFB8C8")]
    [InlineData("#6750A4", false, "Surface", "#FDF7FF")]
    [InlineData("#6750A4", true, "Surface", "#141218")]
    public void TonalSpotRolesMatchGoogle(string seed, bool dark, string role, string expected)
    {
        Assert.Equal(Color.Parse(expected), MaterialColorScheme.Create(Color.Parse(seed), dark)[$"Material{role}Color"]);
    }

    [Theory]
    [InlineData("PrimaryFixed", "#FFDAD4")] [InlineData("PrimaryFixedDim", "#FFB4A8")]
    [InlineData("OnPrimaryFixed", "#3A0905")] [InlineData("OnPrimaryFixedVariant", "#73342A")]
    [InlineData("SecondaryFixed", "#FFDAD4")] [InlineData("SecondaryFixedDim", "#E7BDB6")]
    [InlineData("OnSecondaryFixed", "#2C1512")] [InlineData("OnSecondaryFixedVariant", "#5D3F3B")]
    [InlineData("TertiaryFixed", "#FBDFA6")] [InlineData("TertiaryFixedDim", "#DEC48C")]
    [InlineData("OnTertiaryFixed", "#251A00")] [InlineData("OnTertiaryFixedVariant", "#564419")]
    public void FixedRolesMatchGoogle(string role, string expected)
    {
        Assert.Equal(Color.Parse(expected), MaterialColorScheme.Create(Colors.Red, true)[$"Material{role}Color"]);
    }

}
