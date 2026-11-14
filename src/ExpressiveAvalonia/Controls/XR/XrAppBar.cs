using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// An XR app bar: a 72 unit rounded surface bar with a leading slot, a headline, and a trailing
/// slot, in centered or small configurations.
/// </summary>
/// <remarks>
/// Desktop composition of <c>SpatialTopAppBar</c>/<c>SpatialTwoRowsTopAppBar</c> from the pinned XR
/// source: those wrap the common <c>TopAppBar</c> in a top-center Orbiter with a 24 dp offset and
/// the 50% container shape. A desktop window has no Orbiter or spatial elevation, so this control
/// supplies the 2D equivalent only: a 72 unit bar whose 36 radius is the 50% shape for that height.
/// The TopAppBar content contract itself belongs to the shared app-bar family.
/// <para>
/// The slots accept any control; place <see cref="XrIconButton"/> instances there to get native
/// pointer, keyboard, command, and automation behavior. The source's "show 1st/2nd/3rd trailing
/// icon" switches are expressed by the content of <see cref="TrailingContent"/>, so any number
/// of actions can be supplied without dead boolean properties.
/// </para>
/// </remarks>
public class XrAppBar : TemplatedControl
{
    public static readonly StyledProperty<string?> HeadlineProperty =
        AvaloniaProperty.Register<XrAppBar, string?>(nameof(Headline));

    public static readonly StyledProperty<XrAppBarConfiguration> ConfigurationProperty =
        AvaloniaProperty.Register<XrAppBar, XrAppBarConfiguration>(nameof(Configuration));

    public static readonly StyledProperty<XrSurfaceRole> ElevationProperty =
        AvaloniaProperty.Register<XrAppBar, XrSurfaceRole>(nameof(Elevation));

    /// <summary>Content of the leading slot, normally one <see cref="XrIconButton"/>.</summary>
    public static readonly StyledProperty<object?> LeadingContentProperty =
        AvaloniaProperty.Register<XrAppBar, object?>(nameof(LeadingContent));

    /// <summary>Content of the trailing slot, normally a horizontal panel of icon buttons.</summary>
    public static readonly StyledProperty<object?> TrailingContentProperty =
        AvaloniaProperty.Register<XrAppBar, object?>(nameof(TrailingContent));

    static XrAppBar()
    {
        ConfigurationProperty.Changed.AddClassHandler<XrAppBar>((bar, _) => bar.UpdateConfiguration());
        ElevationProperty.Changed.AddClassHandler<XrAppBar>((bar, _) => bar.UpdateElevation());
        HeadlineProperty.Changed.AddClassHandler<XrAppBar>((bar, _) => bar.UpdateAutomationName());
    }

    public XrAppBar()
    {
        UpdateConfiguration();
        UpdateElevation();
    }

    public string? Headline { get => GetValue(HeadlineProperty); set => SetValue(HeadlineProperty, value); }
    public XrAppBarConfiguration Configuration { get => GetValue(ConfigurationProperty); set => SetValue(ConfigurationProperty, value); }
    public XrSurfaceRole Elevation { get => GetValue(ElevationProperty); set => SetValue(ElevationProperty, value); }
    public object? LeadingContent { get => GetValue(LeadingContentProperty); set => SetValue(LeadingContentProperty, value); }
    public object? TrailingContent { get => GetValue(TrailingContentProperty); set => SetValue(TrailingContentProperty, value); }

    private void UpdateConfiguration() =>
        PseudoClasses.Set(":small", Configuration == XrAppBarConfiguration.Small);

    private void UpdateElevation()
    {
        PseudoClasses.Set(":elevation-high", Elevation == XrSurfaceRole.SurfaceContainerHigh);
        PseudoClasses.Set(":elevation-highest", Elevation == XrSurfaceRole.SurfaceContainerHighest);
    }

    private string? _appliedName;

    private void UpdateAutomationName()
    {
        var headline = Headline;
        if (string.IsNullOrEmpty(headline))
            return;

        var current = AutomationProperties.GetName(this);
        if (string.IsNullOrEmpty(current) || current == _appliedName)
        {
            AutomationProperties.SetName(this, headline);
            _appliedName = headline;
        }
    }
}
