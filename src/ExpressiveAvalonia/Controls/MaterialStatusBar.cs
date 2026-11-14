using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// The Android status bar strip used by the example device surfaces.
/// </summary>
/// <remarks>
/// <para>
/// This is a desktop composition for the example device surfaces, not an M3 composable and not the
/// operating-system status bar. It reads no system indicators beyond the local wall clock.
/// </para>
/// <para>
/// Source anatomy (Building Blocks/status-bar): a 52 DIP strip with the clock inset 24 DIP from
/// the leading edge, a centred camera cutout, and wifi, signal and battery glyphs inset 24 DIP
/// from the trailing edge. The glyph geometry comes from the design source. The clock is live:
/// it ticks while the bar is attached to a visual tree and stops on detach, so off-screen
/// example surfaces do not keep a timer alive.
/// </para>
/// </remarks>
public sealed class MaterialStatusBar : TemplatedControl
{
    /// <summary>Overrides the clock text. Leave null to show the local wall clock.</summary>
    public static readonly StyledProperty<string?> TimeTextProperty =
        AvaloniaProperty.Register<MaterialStatusBar, string?>(nameof(TimeText));

    /// <summary>Shows the live clock. Turn off for screenshots that must not depend on time.</summary>
    public static readonly StyledProperty<bool> ShowClockProperty =
        AvaloniaProperty.Register<MaterialStatusBar, bool>(nameof(ShowClock), true);

    /// <summary>Shows the centred camera cutout, matching the source boolean property.</summary>
    public static readonly StyledProperty<bool> ShowCameraCutoutProperty =
        AvaloniaProperty.Register<MaterialStatusBar, bool>(nameof(ShowCameraCutout), true);

    /// <summary>Height of the strip; the source uses 52 DIP.</summary>
    public static readonly StyledProperty<double> BarHeightProperty =
        AvaloniaProperty.Register<MaterialStatusBar, double>(nameof(BarHeight), 52,
            validate: static value => double.IsFinite(value) && value > 0);

    private TextBlock? _clock;
    private Control? _cutout;
    private DispatcherTimer? _timer;

    static MaterialStatusBar()
    {
        AffectsMeasure<MaterialStatusBar>(BarHeightProperty);
        ShowClockProperty.Changed.AddClassHandler<MaterialStatusBar>(static (bar, _) => bar.UpdateClockState());
        TimeTextProperty.Changed.AddClassHandler<MaterialStatusBar>(static (bar, _) => bar.UpdateClockText());
        ShowCameraCutoutProperty.Changed.AddClassHandler<MaterialStatusBar>(
            static (bar, _) => bar.UpdateCutoutVisibility());
    }

    public MaterialStatusBar()
    {
        Template = new FuncControlTemplate<MaterialStatusBar>((bar, _) => bar.Build());
        AutomationProperties.SetName(this, "Status bar");
    }

    public string? TimeText { get => GetValue(TimeTextProperty); set => SetValue(TimeTextProperty, value); }
    public bool ShowClock { get => GetValue(ShowClockProperty); set => SetValue(ShowClockProperty, value); }
    public bool ShowCameraCutout { get => GetValue(ShowCameraCutoutProperty); set => SetValue(ShowCameraCutoutProperty, value); }
    public double BarHeight { get => GetValue(BarHeightProperty); set => SetValue(BarHeightProperty, value); }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateClockState();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timer?.Stop();
    }

    private Control Build()
    {
        _clock = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(24, 0, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        MaterialTypography.SetStyle(_clock, MaterialTypeStyle.LabelLarge);
        _clock.Bind(TextBlock.ForegroundProperty, _clock.GetResourceObservable("MaterialOnSurfaceBrush"));
        AutomationProperties.SetLiveSetting(_clock, AutomationLiveSetting.Polite);

        var cutout = CreateIcon(MaterialSourceIcons.CameraCutout, 24);
        _cutout = cutout;

        var right = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 7,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 0, 24, 0)
        };
        right.Children.Add(CreateIcon(MaterialSourceIcons.Wifi, 17));
        right.Children.Add(CreateIcon(MaterialSourceIcons.Signal, 15));
        right.Children.Add(CreateIcon(MaterialSourceIcons.BatteryFull, 15));

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto,*"),
            Height = BarHeight
        };
        grid.Children.Add(_clock);
        Grid.SetColumn(cutout, 1);
        grid.Children.Add(cutout);
        Grid.SetColumn(right, 2);
        grid.Children.Add(right);

        UpdateClockText();
        UpdateCutoutVisibility();
        UpdateClockState();
        return grid;
    }

    private void UpdateClockText()
    {
        if (_clock is null) return;
        var text = TimeText;
        if (string.IsNullOrEmpty(text) && ShowClock) text = DateTime.Now.ToString("H:mm");
        _clock.Text = text;
        _clock.IsVisible = ShowClock || !string.IsNullOrEmpty(TimeText);
    }

    private void UpdateCutoutVisibility()
    {
        if (_cutout is not null) _cutout.IsVisible = ShowCameraCutout;
    }

    private void UpdateClockState()
    {
        // The timer only exists while a clock is both requested and on screen.
        if (!ShowClock || TimeText is not null || _clock is null || VisualRoot is null)
        {
            _timer?.Stop();
            return;
        }

        _timer ??= new DispatcherTimer(TimeSpan.FromSeconds(15), DispatcherPriority.Background,
            (_, _) =>
            {
                // Off-screen surfaces (background tabs, scrolled-away frames) skip the tick.
                if (IsEffectivelyVisible) UpdateClockText();
            });
        if (!_timer.IsEnabled) _timer.Start();
    }

    private Shape CreateIcon(string key, double size)
    {
        // Bind instead of resolving once so light/dark and seed changes repaint the glyphs.
        var shape = MaterialSourceIcons.Create(key, size, Brushes.Transparent);
        shape.Bind(Shape.FillProperty, this.GetResourceObservable("MaterialOnSurfaceBrush"));
        return shape;
    }
}
