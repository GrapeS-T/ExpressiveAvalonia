using Avalonia;
using Avalonia.Controls;

namespace ExpressiveAvalonia.Theming;

/// <summary>The source "Type" axis shared by the linear and circular progress indicators.</summary>
public enum ProgressIndicatorType
{
    /// <summary>A straight segment with rounded ends.</summary>
    Flat,

    /// <summary>A sinusoid along the segment, as in the source "Segment - wave" building block.</summary>
    Wave,
}

/// <summary>
/// Source anatomy for the native <see cref="ProgressBar"/>: the "Type" (Flat/Wave), the
/// "Thickness" (4 dp / 8 dp) and the wave's height and wavelength.
/// </summary>
/// <remarks>
/// The native <see cref="ProgressBar"/> keeps its range API, keyboard handling and automation;
/// these attached properties only describe how the indicator is drawn, so
/// <c>ProgressBar</c> stays a real progress bar for assistive technology.
/// Source axes: Linear-determinate (Type × Thickness × Progress) and Linear-indeterminate
/// (Type × Thickness × Step) plus the width-4/width-8 wave building blocks.
/// </remarks>
public sealed class MaterialProgress : AvaloniaObject
{
    private MaterialProgress() { }

    /// <summary>Flat (source default) or wavy segment.</summary>
    public static readonly AttachedProperty<ProgressIndicatorType> IndicatorTypeProperty =
        AvaloniaProperty.RegisterAttached<MaterialProgress, ProgressBar, ProgressIndicatorType>("IndicatorType");

    /// <summary>Segment thickness in device-independent pixels: 4 (source default) or 8.</summary>
    public static readonly AttachedProperty<double> ThicknessProperty =
        AvaloniaProperty.RegisterAttached<MaterialProgress, ProgressBar, double>("Thickness", 4,
            validate: value => double.IsFinite(value) && value > 0);

    /// <summary>Total band height of the wave; the wave amplitude is derived from it.</summary>
    public static readonly AttachedProperty<double> WaveHeightProperty =
        AvaloniaProperty.RegisterAttached<MaterialProgress, ProgressBar, double>("WaveHeight", 12,
            validate: value => double.IsFinite(value) && value >= 0);

    /// <summary>Distance between two crests along the segment: 40 (source default) or 20.</summary>
    public static readonly AttachedProperty<double> WavelengthProperty =
        AvaloniaProperty.RegisterAttached<MaterialProgress, ProgressBar, double>("Wavelength", 40,
            validate: value => double.IsFinite(value) && value > 0);

    public static ProgressIndicatorType GetIndicatorType(ProgressBar control) => control.GetValue(IndicatorTypeProperty);
    public static void SetIndicatorType(ProgressBar control, ProgressIndicatorType value) => control.SetValue(IndicatorTypeProperty, value);

    public static double GetThickness(ProgressBar control) => control.GetValue(ThicknessProperty);
    public static void SetThickness(ProgressBar control, double value) => control.SetValue(ThicknessProperty, value);

    public static double GetWaveHeight(ProgressBar control) => control.GetValue(WaveHeightProperty);
    public static void SetWaveHeight(ProgressBar control, double value) => control.SetValue(WaveHeightProperty, value);

    public static double GetWavelength(ProgressBar control) => control.GetValue(WavelengthProperty);
    public static void SetWavelength(ProgressBar control, double value) => control.SetValue(WavelengthProperty, value);
}
