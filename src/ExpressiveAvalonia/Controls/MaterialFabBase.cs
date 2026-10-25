using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Styling;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Controls;

/// <summary>The Material color treatment shared by the FAB family.</summary>
/// <remarks>
/// The three container roles are the resting FAB colors; the three solid roles are the
/// high-emphasis variants (a filled container with an on-color icon). Source packages:
/// FAB (local-001450), Extended FAB (local-001377), FAB Menu (local-001523).
/// </remarks>
public enum MaterialFabColorRole
{
    PrimaryContainer,
    SecondaryContainer,
    TertiaryContainer,
    Primary,
    Secondary,
    Tertiary
}

/// <summary>
/// Shared Material FAB behavior: color role, focus ring geometry, and the pointer ripple.
/// </summary>
/// <remarks>
/// <para>
/// The class derives from <see cref="Button"/>, so command execution, access keys,
/// Enter/Space activation, <c>:pressed</c>, <c>:focus-visible</c>, and the disabled state all
/// come from Avalonia rather than a reimplementation.
/// </para>
/// <para>
/// The template contract is <c>Border#Surface</c>, <c>Border#StateLayer</c>,
/// <c>Border#FocusRing</c> and <c>Canvas#PART_RippleHost</c>. The ripple is pointer-only and
/// expands from the press point at the source 8% content layer opacity; it is skipped when
/// <see cref="MaterialMotion.IsReduced"/> is set, so reduced motion leaves only the static
/// state layer.
/// </para>
/// </remarks>
public abstract class MaterialFabBase : Button
{
    /// <summary>Defines the <see cref="ColorRole"/> property.</summary>
    public static readonly StyledProperty<MaterialFabColorRole> ColorRoleProperty =
        AvaloniaProperty.Register<MaterialFabBase, MaterialFabColorRole>(
            nameof(ColorRole), MaterialFabColorRole.PrimaryContainer);

    /// <summary>
    /// The focus indicator radius: the surface radius plus the source's two unit inset.
    /// </summary>
    /// <remarks>Template plumbing; set from <see cref="Border.CornerRadius"/>.</remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly StyledProperty<CornerRadius> FocusRingCornerRadiusProperty =
        AvaloniaProperty.Register<MaterialFabBase, CornerRadius>(nameof(FocusRingCornerRadius));

    private static readonly TimeSpan DefaultRippleDuration = TimeSpan.FromMilliseconds(300);
    private Canvas? _rippleHost;
    private string? _autoAutomationName;

    static MaterialFabBase()
    {
        CornerRadiusProperty.Changed.AddClassHandler<MaterialFabBase>((fab, _) => fab.UpdateFocusRingRadius());
    }

    /// <summary>Gets or sets the Material color role of the FAB container.</summary>
    public MaterialFabColorRole ColorRole
    {
        get => GetValue(ColorRoleProperty);
        set => SetValue(ColorRoleProperty, value);
    }

    /// <summary>Gets the corner radius used by the template's focus indicator.</summary>
    public CornerRadius FocusRingCornerRadius
    {
        get => GetValue(FocusRingCornerRadiusProperty);
        private set => SetValue(FocusRingCornerRadiusProperty, value);
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_rippleHost is not null)
            _rippleHost.SizeChanged -= OnRippleHostSizeChanged;
        ClearRipples();

        base.OnApplyTemplate(e);

        _rippleHost = e.NameScope.Find<Canvas>("PART_RippleHost");
        if (_rippleHost is not null)
        {
            _rippleHost.SizeChanged += OnRippleHostSizeChanged;
            UpdateRippleClip();
        }
        UpdateFocusRingRadius();
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var properties = e.GetCurrentPoint(this).Properties;
        if (properties.IsLeftButtonPressed || e.Pointer.Type == PointerType.Touch)
            StartRipple(e.GetPosition(this));
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        ClearRipples();
        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>
    /// Keeps <see cref="AutomationProperties.Name"/> in step with a text label without
    /// overwriting a name the application set explicitly.
    /// </summary>
    internal void SyncAutomationName(string? label)
    {
        var current = AutomationProperties.GetName(this);
        if (!string.IsNullOrEmpty(current) && current != _autoAutomationName)
            return;

        var name = label ?? string.Empty;
        AutomationProperties.SetName(this, name);
        _autoAutomationName = name;
    }

    private void UpdateFocusRingRadius()
    {
        var radius = CornerRadius;
        var uniform = Math.Max(0, radius.TopLeft + 2);
        FocusRingCornerRadius = new CornerRadius(uniform, radius.TopRight + 2, radius.BottomRight + 2, radius.BottomLeft + 2);
    }

    private void OnRippleHostSizeChanged(object? sender, SizeChangedEventArgs e) => UpdateRippleClip();

    private void UpdateRippleClip()
    {
        if (_rippleHost is null)
            return;

        var width = _rippleHost.Bounds.Width;
        var height = _rippleHost.Bounds.Height;
        if (width <= 0 || height <= 0)
            return;

        var radius = Math.Min(Math.Min(CornerRadius.TopLeft, CornerRadius.TopRight), Math.Min(width, height) / 2);
        _rippleHost.Clip = radius > 0
            ? new RectangleGeometry(new Rect(0, 0, width, height), radius, radius)
            : null;
    }

    private void ClearRipples()
    {
        if (_rippleHost is null)
            return;

        _rippleHost.Children.Clear();
    }

    private void StartRipple(Point position)
    {
        if (_rippleHost is null || !IsEffectivelyEnabled || MaterialMotion.GetIsReduced(this))
            return;

        var brush = Foreground;
        var diameter = Math.Max(Bounds.Width, Bounds.Height) * 1.6;
        if (brush is null || !(diameter > 0))
            return;

        // The source's pressed snapshots show a content-colored ripple at 8% over the container.
        var ellipse = new Ellipse
        {
            Width = diameter,
            Height = diameter,
            Fill = brush,
            Opacity = 0.08,
            IsHitTestVisible = false,
            RenderTransformOrigin = RelativePoint.Center,
            RenderTransform = TransformOperations.Parse("scale(0.2)")
        };
        Canvas.SetLeft(ellipse, position.X - (diameter / 2));
        Canvas.SetTop(ellipse, position.Y - (diameter / 2));
        _rippleHost.Children.Add(ellipse);

        var duration = this.TryFindResource("MaterialDurationMedium2", out var value) && value is TimeSpan time
            ? time
            : DefaultRippleDuration;

        var animation = new Animation
        {
            Duration = duration,
            Easing = new CubicEaseOut(),
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0),
                    Setters =
                    {
                        new Setter(OpacityProperty, 0.08d),
                        new Setter(RenderTransformProperty, TransformOperations.Parse("scale(0.2)"))
                    }
                },
                new KeyFrame
                {
                    Cue = new Cue(1),
                    Setters =
                    {
                        new Setter(OpacityProperty, 0d),
                        new Setter(RenderTransformProperty, TransformOperations.Parse("scale(1)"))
                    }
                }
            }
        };

        _ = RunRippleAsync(animation, ellipse, _rippleHost);
    }

    private static async System.Threading.Tasks.Task RunRippleAsync(Animation animation, Ellipse ellipse, Canvas host)
    {
        try
        {
            await animation.RunAsync(ellipse);
        }
        catch (OperationCanceledException)
        {
        }

        host.Children.Remove(ellipse);
    }
}
