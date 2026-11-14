using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace ExpressiveAvalonia.Theming;

/// <summary>Selects the pinned Material 3 linear progress composable to port.</summary>
public enum ProgressIndicatorType
{
    /// <summary><c>LinearProgressIndicator</c>: a straight segment with a stop indicator.</summary>
    Flat,

    /// <summary><c>LinearWavyProgressIndicator</c>: a quadratic-bezier wave in a taller band.</summary>
    Wave,
}

/// <summary>
/// Source parameters of <c>LinearProgressIndicator</c> and <c>LinearWavyProgressIndicator</c>,
/// attached to the native <see cref="ProgressBar"/>.
/// </summary>
/// <remarks>
/// <para>The native <see cref="ProgressBar"/> keeps its range API, keyboard handling, automation
/// and indeterminate state; these attached properties only choose the drawing algorithm. The
/// presenter reads them from its templated parent, so no parallel progress control or second
/// range model is introduced.</para>
/// <para>Every default comes from the pinned snapshot:
/// <c>LinearProgressIndicatorTokens</c> supplies active/track thickness 4, active wave amplitude 3,
/// determinate wavelength 40, indeterminate wavelength 20, stop size 4, track/active space 4,
/// height 4 and wave height 10. The source indicator width is 240. The stop's trailing space is
/// 6 in <c>ProgressIndicator.kt</c>.</para>
/// <para><see cref="IndicatorType"/> is the project's selection between the two source
/// composables; the pinned snapshot has no Type axis. Everything else mirrors a source parameter:
/// <c>Thickness</c> is the active stroke width (and the flat band height), <c>TrackThickness</c>
/// the wavy track stroke, <c>WaveHeight</c> the wavy container height, the wavelengths, the gap,
/// the stop size, the caps, and the wave speed. Amplitude is a fixed fraction for the
/// indeterminate form and a progress function for the determinate form, exactly as upstream
/// declares them.</para>
/// </remarks>
public sealed class MaterialProgress : AvaloniaObject
{
    private MaterialProgress() { }

    /// <summary>Flat (source default) or wavy linear indicator.</summary>
    public static readonly AttachedProperty<ProgressIndicatorType> IndicatorTypeProperty =
        AvaloniaProperty.RegisterAttached<MaterialProgress, ProgressBar, ProgressIndicatorType>("IndicatorType");

    /// <summary>Active stroke width in DIPs; also the flat band height. Source: 4.</summary>
    public static readonly AttachedProperty<double> ThicknessProperty =
        AvaloniaProperty.RegisterAttached<MaterialProgress, ProgressBar, double>("Thickness", 4,
            validate: value => double.IsFinite(value) && value > 0);

    /// <summary>Track stroke width of the wavy indicator. Source: 4.</summary>
    public static readonly AttachedProperty<double> TrackThicknessProperty =
        AvaloniaProperty.RegisterAttached<MaterialProgress, ProgressBar, double>("TrackThickness", 4,
            validate: value => double.IsFinite(value) && value > 0);

    /// <summary>Height of the wavy band; the wave peak-to-peak is height − stroke. Source: 10.</summary>
    public static readonly AttachedProperty<double> WaveHeightProperty =
        AvaloniaProperty.RegisterAttached<MaterialProgress, ProgressBar, double>("WaveHeight", 10,
            validate: value => double.IsFinite(value) && value >= 0);

    /// <summary>Determinate wavelength in DIPs. Source: 40.</summary>
    public static readonly AttachedProperty<double> WavelengthProperty =
        AvaloniaProperty.RegisterAttached<MaterialProgress, ProgressBar, double>("Wavelength", 40,
            validate: value => double.IsFinite(value) && value > 0);

    /// <summary>Indeterminate wavelength in DIPs. Source: 20.</summary>
    public static readonly AttachedProperty<double> IndeterminateWavelengthProperty =
        AvaloniaProperty.RegisterAttached<MaterialProgress, ProgressBar, double>("IndeterminateWavelength", 20,
            validate: value => double.IsFinite(value) && value > 0);

    /// <summary>Gap between active segment and track. Source: 4.</summary>
    public static readonly AttachedProperty<double> GapSizeProperty =
        AvaloniaProperty.RegisterAttached<MaterialProgress, ProgressBar, double>("GapSize", 4,
            validate: value => double.IsFinite(value) && value >= 0);

    /// <summary>Stop indicator size; clamped to the band height. Source: 4.</summary>
    public static readonly AttachedProperty<double> StopSizeProperty =
        AvaloniaProperty.RegisterAttached<MaterialProgress, ProgressBar, double>("StopSize", 4,
            validate: value => double.IsFinite(value) && value >= 0);

    /// <summary>Active stroke cap. Source default: round.</summary>
    public static readonly AttachedProperty<PenLineCap> StrokeCapProperty =
        AvaloniaProperty.RegisterAttached<MaterialProgress, ProgressBar, PenLineCap>("StrokeCap", PenLineCap.Round);

    /// <summary>Wavy track stroke cap. Source default: round.</summary>
    public static readonly AttachedProperty<PenLineCap> TrackStrokeCapProperty =
        AvaloniaProperty.RegisterAttached<MaterialProgress, ProgressBar, PenLineCap>("TrackStrokeCap", PenLineCap.Round);

    /// <summary>
    /// Wave travel speed in DIPs per second. The default is NaN, which resolves to the current
    /// wavelength (one wavelength per second), matching the source default argument.
    /// </summary>
    public static readonly AttachedProperty<double> WaveSpeedProperty =
        AvaloniaProperty.RegisterAttached<MaterialProgress, ProgressBar, double>("WaveSpeed", double.NaN,
            validate: value => double.IsNaN(value) || (double.IsFinite(value) && value >= 0));

    /// <summary>Fixed wave amplitude of the indeterminate form, coerced to 0..1. Source: 1.</summary>
    public static readonly AttachedProperty<double> AmplitudeProperty =
        AvaloniaProperty.RegisterAttached<MaterialProgress, ProgressBar, double>("Amplitude", 1,
            validate: value => double.IsFinite(value));

    /// <summary>
    /// Determinate amplitude as a function of progress. Null uses the source
    /// <c>WavyProgressIndicatorDefaults.indicatorAmplitude</c>: zero at or below 10% and from 95%,
    /// full otherwise.
    /// </summary>
    public static readonly AttachedProperty<Func<double, double>?> AmplitudeFunctionProperty =
        AvaloniaProperty.RegisterAttached<MaterialProgress, ProgressBar, Func<double, double>?>("AmplitudeFunction");

    public static ProgressIndicatorType GetIndicatorType(ProgressBar control) => control.GetValue(IndicatorTypeProperty);
    public static void SetIndicatorType(ProgressBar control, ProgressIndicatorType value) => control.SetValue(IndicatorTypeProperty, value);

    public static double GetThickness(ProgressBar control) => control.GetValue(ThicknessProperty);
    public static void SetThickness(ProgressBar control, double value) => control.SetValue(ThicknessProperty, value);

    public static double GetTrackThickness(ProgressBar control) => control.GetValue(TrackThicknessProperty);
    public static void SetTrackThickness(ProgressBar control, double value) => control.SetValue(TrackThicknessProperty, value);

    public static double GetWaveHeight(ProgressBar control) => control.GetValue(WaveHeightProperty);
    public static void SetWaveHeight(ProgressBar control, double value) => control.SetValue(WaveHeightProperty, value);

    public static double GetWavelength(ProgressBar control) => control.GetValue(WavelengthProperty);
    public static void SetWavelength(ProgressBar control, double value) => control.SetValue(WavelengthProperty, value);

    public static double GetIndeterminateWavelength(ProgressBar control) => control.GetValue(IndeterminateWavelengthProperty);
    public static void SetIndeterminateWavelength(ProgressBar control, double value) => control.SetValue(IndeterminateWavelengthProperty, value);

    public static double GetGapSize(ProgressBar control) => control.GetValue(GapSizeProperty);
    public static void SetGapSize(ProgressBar control, double value) => control.SetValue(GapSizeProperty, value);

    public static double GetStopSize(ProgressBar control) => control.GetValue(StopSizeProperty);
    public static void SetStopSize(ProgressBar control, double value) => control.SetValue(StopSizeProperty, value);

    public static PenLineCap GetStrokeCap(ProgressBar control) => control.GetValue(StrokeCapProperty);
    public static void SetStrokeCap(ProgressBar control, PenLineCap value) => control.SetValue(StrokeCapProperty, value);

    public static PenLineCap GetTrackStrokeCap(ProgressBar control) => control.GetValue(TrackStrokeCapProperty);
    public static void SetTrackStrokeCap(ProgressBar control, PenLineCap value) => control.SetValue(TrackStrokeCapProperty, value);

    public static double GetWaveSpeed(ProgressBar control) => control.GetValue(WaveSpeedProperty);
    public static void SetWaveSpeed(ProgressBar control, double value) => control.SetValue(WaveSpeedProperty, value);

    public static double GetAmplitude(ProgressBar control) => control.GetValue(AmplitudeProperty);
    public static void SetAmplitude(ProgressBar control, double value) => control.SetValue(AmplitudeProperty, value);

    public static Func<double, double>? GetAmplitudeFunction(ProgressBar control) => control.GetValue(AmplitudeFunctionProperty);
    public static void SetAmplitudeFunction(ProgressBar control, Func<double, double>? value) => control.SetValue(AmplitudeFunctionProperty, value);
}