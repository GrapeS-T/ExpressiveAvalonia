using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace ExpressiveAvalonia.Theming;

/// <summary>The Material color treatment of a button.</summary>
/// <remarks>
/// <see cref="Standard"/> is the default: a filled container for labeled buttons and a
/// container-less icon button (icon on OnSurfaceVariant) for icon buttons, matching
/// AndroidX StandardIconButtonTokens. The other members apply the matching
/// *ButtonTokens/*IconButtonTokens color set.
/// </remarks>
public enum MaterialButtonVariant { Standard, Filled, Tonal, Outlined, Elevated, Text }

/// <summary>The five Material Expressive control sizes.</summary>
public enum MaterialControlSize { ExtraSmall, Small, Medium, Large, ExtraLarge }

/// <summary>The resting silhouette of a Material Expressive button.</summary>
public enum MaterialButtonShape { Round, Square }

/// <summary>
/// The three icon-button widths. Every size has a narrow, default and wide container;
/// the width only changes the horizontal container, never the icon size or the touch target.
/// </summary>
public enum MaterialIconButtonWidth { Narrow, Default, Wide }

/// <summary>Material presentation options for standard Avalonia controls.</summary>
public sealed class MaterialAssist : AvaloniaObject
{
    private MaterialAssist() { }

    public static readonly AttachedProperty<MaterialButtonVariant> ButtonVariantProperty =
        AvaloniaProperty.RegisterAttached<MaterialAssist, Control, MaterialButtonVariant>(
            "ButtonVariant", MaterialButtonVariant.Standard);

    public static readonly AttachedProperty<MaterialControlSize> ControlSizeProperty =
        AvaloniaProperty.RegisterAttached<MaterialAssist, Control, MaterialControlSize>(
            "ControlSize", MaterialControlSize.Small);

    public static readonly AttachedProperty<MaterialButtonShape> ButtonShapeProperty =
        AvaloniaProperty.RegisterAttached<MaterialAssist, Control, MaterialButtonShape>(
            "ButtonShape", MaterialButtonShape.Round);

    public static readonly AttachedProperty<bool> IsIconButtonProperty =
        AvaloniaProperty.RegisterAttached<MaterialAssist, Control, bool>("IsIconButton");

    public static readonly AttachedProperty<MaterialIconButtonWidth> IconButtonWidthProperty =
        AvaloniaProperty.RegisterAttached<MaterialAssist, Control, MaterialIconButtonWidth>(
            "IconButtonWidth", MaterialIconButtonWidth.Default);

    /// <summary>
    /// An optional icon rendered before <see cref="ContentControl.Content"/>.
    /// </summary>
    /// <remarks>
    /// The value is any content the presenter can render; a <see cref="PathIcon"/> or another
    /// <see cref="Control"/> is the usual choice. Icon buttons normally pass the glyph as
    /// Content instead, because their Content is already the glyph.
    /// </remarks>
    public static readonly AttachedProperty<object?> IconProperty =
        AvaloniaProperty.RegisterAttached<MaterialAssist, Control, object?>("Icon");

    /// <summary>
    /// The icon a <see cref="ToggleButton"/> shows while it is checked. When it is unset the
    /// control keeps showing <see cref="IconProperty"/>.
    /// </summary>
    public static readonly AttachedProperty<object?> SelectedIconProperty =
        AvaloniaProperty.RegisterAttached<MaterialAssist, Control, object?>("SelectedIcon");

    /// <summary>Whether the optional <see cref="IconProperty"/> participates in the layout.</summary>
    public static readonly AttachedProperty<bool> ShowIconProperty =
        AvaloniaProperty.RegisterAttached<MaterialAssist, Control, bool>("ShowIcon", true);

    // Template plumbing: the presenter reads one icon and one visibility flag, so the
    // checked/unchecked swap never needs a style to win over a template binding.
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly AttachedProperty<object?> DisplayedIconProperty =
        AvaloniaProperty.RegisterAttached<MaterialAssist, Control, object?>("DisplayedIcon");

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly AttachedProperty<bool> IsIconVisibleProperty =
        AvaloniaProperty.RegisterAttached<MaterialAssist, Control, bool>("IsIconVisible");

    // Reserves the 48-DIP interactive target without enlarging the visual container.
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly AttachedProperty<Thickness> ContainerInsetProperty =
        AvaloniaProperty.RegisterAttached<MaterialAssist, Button, Thickness>("ContainerInset");

    // These template plumbing properties leave the consumer's CornerRadius untouched.
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly AttachedProperty<bool> AnimateShapeProperty =
        AvaloniaProperty.RegisterAttached<MaterialAssist, Button, bool>("AnimateShape");

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly AttachedProperty<CornerRadius> AnimatedCornerRadiusProperty =
        AvaloniaProperty.RegisterAttached<MaterialAssist, Button, CornerRadius>("AnimatedCornerRadius");

    // The focus indicator sits two DIPs outside the container and follows its animated
    // silhouette, so the ring keeps its 3 DIP stroke visually parallel to the surface.
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly AttachedProperty<CornerRadius> FocusRingCornerRadiusProperty =
        AvaloniaProperty.RegisterAttached<MaterialAssist, Button, CornerRadius>("FocusRingCornerRadius");

    // The pressed-ripple box per size, straight from the source "Ripple" vector bounds.
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly AttachedProperty<double> RippleWidthProperty =
        AvaloniaProperty.RegisterAttached<MaterialAssist, Button, double>("RippleWidth", 84d);

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly AttachedProperty<double> RippleHeightProperty =
        AvaloniaProperty.RegisterAttached<MaterialAssist, Button, double>("RippleHeight", 28d);

    private static readonly AttachedProperty<ButtonShapeAnimation?> ShapeAnimationProperty =
        AvaloniaProperty.RegisterAttached<MaterialAssist, Button, ButtonShapeAnimation?>("ShapeAnimation");

    static MaterialAssist()
    {
        AnimateShapeProperty.Changed.AddClassHandler<Button>((button, _) =>
        {
            if (GetAnimateShape(button))
            {
                if (button.GetValue(ShapeAnimationProperty) is null)
                    button.SetValue(ShapeAnimationProperty, new ButtonShapeAnimation(button));
            }
            else
            {
                button.GetValue(ShapeAnimationProperty)?.Dispose();
                button.ClearValue(ShapeAnimationProperty);
                // The template renders AnimatedCornerRadius, so it must mirror the consumer's
                // CornerRadius when the morph is off instead of falling back to 0.
                button.SetCurrentValue(AnimatedCornerRadiusProperty, button.CornerRadius);
            }
        });

        Button.CornerRadiusProperty.Changed.AddClassHandler<Button>((button, _) =>
        {
            if (button.GetValue(ShapeAnimationProperty) is null)
                button.SetCurrentValue(AnimatedCornerRadiusProperty, button.CornerRadius);
        });

        // The ring is drawn two DIPs outside the container, so its radius trails the
        // animated silhouette by exactly that offset.
        AnimatedCornerRadiusProperty.Changed.AddClassHandler<Button>((button, args) =>
        {
            var radius = args.NewValue is CornerRadius value ? value : default;
            button.SetCurrentValue(FocusRingCornerRadiusProperty, new CornerRadius(
                radius.TopLeft + 2, radius.TopRight + 2, radius.BottomRight + 2, radius.BottomLeft + 2));
        });

        // Icon selection is resolved on the control instead of in the template so a checked
        // toggle can swap glyphs without a style fighting a template binding.
        IconProperty.Changed.AddClassHandler<Control>((control, _) => UpdateIcon(control));
        SelectedIconProperty.Changed.AddClassHandler<Control>((control, _) => UpdateIcon(control));
        ShowIconProperty.Changed.AddClassHandler<Control>((control, _) => UpdateIcon(control));
        ToggleButton.IsCheckedProperty.Changed.AddClassHandler<ToggleButton>((toggle, _) => UpdateIcon(toggle));
    }

    private static void UpdateIcon(Control control)
    {
        var selected = control is ToggleButton { IsChecked: true } ? GetSelectedIcon(control) : null;
        var icon = selected ?? GetIcon(control);
        control.SetCurrentValue(DisplayedIconProperty, icon);
        control.SetCurrentValue(IsIconVisibleProperty, GetShowIcon(control) && icon is not null);
    }

    public static MaterialButtonVariant GetButtonVariant(Control control) => control.GetValue(ButtonVariantProperty);
    public static void SetButtonVariant(Control control, MaterialButtonVariant value) => control.SetValue(ButtonVariantProperty, value);
    public static MaterialControlSize GetControlSize(Control control) => control.GetValue(ControlSizeProperty);
    public static void SetControlSize(Control control, MaterialControlSize value) => control.SetValue(ControlSizeProperty, value);
    public static MaterialButtonShape GetButtonShape(Control control) => control.GetValue(ButtonShapeProperty);
    public static void SetButtonShape(Control control, MaterialButtonShape value) => control.SetValue(ButtonShapeProperty, value);
    public static bool GetIsIconButton(Control control) => control.GetValue(IsIconButtonProperty);
    public static void SetIsIconButton(Control control, bool value) => control.SetValue(IsIconButtonProperty, value);
    public static MaterialIconButtonWidth GetIconButtonWidth(Control control) => control.GetValue(IconButtonWidthProperty);
    public static void SetIconButtonWidth(Control control, MaterialIconButtonWidth value) => control.SetValue(IconButtonWidthProperty, value);
    public static object? GetIcon(Control control) => control.GetValue(IconProperty);
    public static void SetIcon(Control control, object? value) => control.SetValue(IconProperty, value);
    public static object? GetSelectedIcon(Control control) => control.GetValue(SelectedIconProperty);
    public static void SetSelectedIcon(Control control, object? value) => control.SetValue(SelectedIconProperty, value);
    public static bool GetShowIcon(Control control) => control.GetValue(ShowIconProperty);
    public static void SetShowIcon(Control control, bool value) => control.SetValue(ShowIconProperty, value);

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static object? GetDisplayedIcon(Control control) => control.GetValue(DisplayedIconProperty);
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void SetDisplayedIcon(Control control, object? value) => control.SetValue(DisplayedIconProperty, value);
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static bool GetIsIconVisible(Control control) => control.GetValue(IsIconVisibleProperty);
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void SetIsIconVisible(Control control, bool value) => control.SetValue(IsIconVisibleProperty, value);
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static CornerRadius GetFocusRingCornerRadius(Button button) => button.GetValue(FocusRingCornerRadiusProperty);
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void SetFocusRingCornerRadius(Button button, CornerRadius value) => button.SetValue(FocusRingCornerRadiusProperty, value);
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static double GetRippleWidth(Button button) => button.GetValue(RippleWidthProperty);
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void SetRippleWidth(Button button, double value) => button.SetValue(RippleWidthProperty, value);
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static double GetRippleHeight(Button button) => button.GetValue(RippleHeightProperty);
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void SetRippleHeight(Button button, double value) => button.SetValue(RippleHeightProperty, value);

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static Thickness GetContainerInset(Button button) => button.GetValue(ContainerInsetProperty);
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void SetContainerInset(Button button, Thickness value) => button.SetValue(ContainerInsetProperty, value);

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static bool GetAnimateShape(Button button) => button.GetValue(AnimateShapeProperty);
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void SetAnimateShape(Button button, bool value) => button.SetValue(AnimateShapeProperty, value);
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static CornerRadius GetAnimatedCornerRadius(Button button) => button.GetValue(AnimatedCornerRadiusProperty);
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void SetAnimatedCornerRadius(Button button, CornerRadius value) => button.SetValue(AnimatedCornerRadiusProperty, value);

    private sealed class ButtonShapeAnimation : IDisposable
    {
        private readonly Button _button;
        private MaterialMotion.SpringAnimation? _spring;
        private double _progress;

        public ButtonShapeAnimation(Button button)
        {
            _button = button;
            button.PropertyChanged += OnPropertyChanged;
            button.AttachedToVisualTree += OnAttached;
            button.DetachedFromVisualTree += OnDetached;
            Update(false);
        }

        private void OnAttached(object? sender, VisualTreeAttachmentEventArgs args) => Update(false);

        private void OnDetached(object? sender, VisualTreeAttachmentEventArgs args)
        {
            _spring?.Dispose();
            _spring = null;
        }

        private void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs args)
        {
            if (args.Property == Button.IsPressedProperty || args.Property == ToggleButton.IsCheckedProperty)
                Update(true);
            else if (args.Property == MaterialMotion.IsReducedProperty ||
                     args.Property == Button.CornerRadiusProperty || args.Property == Button.BoundsProperty ||
                     args.Property == ControlSizeProperty || args.Property == ButtonShapeProperty ||
                     args.Property == IsIconButtonProperty || args.Property == ContainerInsetProperty)
                Update(false);
        }

        private void Update(bool animate)
        {
            var inset = GetContainerInset(_button);
            var height = Math.Max(0, (_button.Bounds.Height > 0 ? _button.Bounds.Height : _button.MinHeight) - inset.Top - inset.Bottom);
            var width = Math.Max(0, (_button.Bounds.Width > 0 ? _button.Bounds.Width : _button.MinWidth) - inset.Left - inset.Right);
            var limit = Math.Min(width, height) / 2;
            var square = GetControlSize(_button) switch
            {
                MaterialControlSize.ExtraSmall or MaterialControlSize.Small => 12d,
                MaterialControlSize.Medium => 16d,
                _ => 28d,
            };
            var targetRadius = Math.Min(_button.CornerRadius.TopLeft, limit);
            if (_button.IsPressed)
                targetRadius = GetControlSize(_button) switch
                {
                    MaterialControlSize.ExtraSmall or MaterialControlSize.Small => 8d,
                    MaterialControlSize.Medium => 12d,
                    _ => 16d,
                };
            // Toggle buttons swap their silhouette when selected: AndroidX
            // SelectedContainerShapeRound = CornerMedium|Large|ExtraLarge and
            // SelectedContainerShapeSquare = CornerFull for labeled and icon toggles alike,
            // while the unselected shapes are Round = full and Square = CornerMedium|... .
            // `square` above is SelectedContainerShapeRound.
            else if (_button is ToggleButton { IsChecked: true })
                targetRadius = GetButtonShape(_button) == MaterialButtonShape.Round ? square : limit;
            var target = Math.Min(targetRadius, limit) - Math.Min(_button.CornerRadius.TopLeft, limit);
            if (!animate || !_button.IsAttachedToVisualTree() || MaterialMotion.GetIsReduced(_button))
            {
                _spring?.Dispose();
                _spring = null;
                Apply(target);
            }
            else if (_spring is not null)
            {
                _spring.Retarget(target);
            }
            else
            {
                _spring = MaterialMotion.Animate(_button, _progress, target, Apply);
            }
        }

        private void Apply(double progress)
        {
            _progress = progress;
            var inset = GetContainerInset(_button);
            var height = Math.Max(0, (_button.Bounds.Height > 0 ? _button.Bounds.Height : _button.MinHeight) - inset.Top - inset.Bottom);
            var width = Math.Max(0, (_button.Bounds.Width > 0 ? _button.Bounds.Width : _button.MinWidth) - inset.Left - inset.Right);
            var limit = Math.Min(width, height) / 2;
            var resting = _button.CornerRadius;
            double Corner(double value) => Math.Clamp(Math.Min(value, limit) + progress, 0, limit);
            _button.SetCurrentValue(AnimatedCornerRadiusProperty, new CornerRadius(
                Corner(resting.TopLeft), Corner(resting.TopRight),
                Corner(resting.BottomRight), Corner(resting.BottomLeft)));
        }

        public void Dispose()
        {
            _spring?.Dispose();
            _button.PropertyChanged -= OnPropertyChanged;
            _button.AttachedToVisualTree -= OnAttached;
            _button.DetachedFromVisualTree -= OnDetached;
        }
    }
}

/// <summary>
/// Paints the source pressed-ripple vector: the normalized Material ripple shape, scaled to the
/// token ripple box, anchored to the container's right and bottom edges, and clipped by the
/// button silhouette.
/// </summary>
/// <remarks>
/// The built-in shape is the button-family source vector, normalized: every Button, ToggleButton
/// and IconButton variant in the source scales this one path to its box (labelled Small 84x28,
/// XLarge 248x82, icon Small 38x28, XLarge 118x82, ...), so the same normalized geometry
/// reproduces all of them, including the square bottom-right corner the button source has.
/// Other source families can have a slightly different outline in that corner (the checkbox
/// ripple rounds it); pass <see cref="Data"/> to use their exact vector instead.
/// The host control clips instead of squeezing the shape, so a Narrow icon button lets the ripple
/// overflow and be cut by the rounded container exactly as the Figma frame does with
/// <c>clipsContent</c>.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class MaterialRipple : Control, Avalonia.Rendering.ICustomHitTest
{
    // Source ripple normalized to its own bounding box (x / 84, y / 28 of the Small vector).
    private static readonly Geometry Shape = StreamGeometry.Parse(
        "M1,0.111568 L1,1 L0,1 C0,0.447714 0.306768,0 0.685186,0 C0.798683,0 0.905736,0.0402754 1,0.111568 Z");

    /// <summary>
    /// A purely decorative layer never takes pointer input, so it can never block the interactive
    /// control it is layered on, whatever the host's z-order or Opacity.
    /// </summary>
    bool Avalonia.Rendering.ICustomHitTest.HitTest(Point point) => false;

    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<MaterialRipple, IBrush?>(nameof(Fill));

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<MaterialRipple, CornerRadius>(nameof(CornerRadius));

    public static readonly StyledProperty<double> RippleWidthProperty =
        AvaloniaProperty.Register<MaterialRipple, double>(nameof(RippleWidth), 84d);

    public static readonly StyledProperty<double> RippleHeightProperty =
        AvaloniaProperty.Register<MaterialRipple, double>(nameof(RippleHeight), 28d);

    /// <summary>
    /// An optional ripple outline in normalized (0..1) coordinates that replaces the built-in
    /// source vector. Families whose source Ripple differs in a corner can pass their own path.
    /// </summary>
    public static readonly StyledProperty<Geometry?> DataProperty =
        AvaloniaProperty.Register<MaterialRipple, Geometry?>(nameof(Data));

    static MaterialRipple()
    {
        AffectsRender<MaterialRipple>(
            FillProperty, CornerRadiusProperty, RippleWidthProperty, RippleHeightProperty, DataProperty);
    }

    /// <summary>Gets or sets the ripple brush; the theme passes the button foreground.</summary>
    public IBrush? Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    /// <summary>Gets or sets the silhouette the ripple is clipped by.</summary>
    public CornerRadius CornerRadius
    {
        get => GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    /// <summary>Gets or sets the ripple box width from the source vector.</summary>
    public double RippleWidth
    {
        get => GetValue(RippleWidthProperty);
        set => SetValue(RippleWidthProperty, value);
    }

    /// <summary>Gets or sets the ripple box height from the source vector.</summary>
    public double RippleHeight
    {
        get => GetValue(RippleHeightProperty);
        set => SetValue(RippleHeightProperty, value);
    }

    /// <summary>
    /// Gets or sets a normalized (0..1) ripple outline. When null the built-in source vector is
    /// used; see <see cref="Shape"/>.
    /// </summary>
    public Geometry? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    private Size _arrangedSize;
    private Point? _origin;
    private double _progress;
    private double _alpha;
    private bool _pressed;
    private double _releasedAt;
    private readonly System.Diagnostics.Stopwatch _clock = new();
    private Avalonia.Threading.DispatcherTimer? _timer;
    private InputElement? _host;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        DetachHost();
        _host = this.FindAncestorOfType<Button>() as InputElement ?? this.FindAncestorOfType<InputElement>();
        if (_host != null)
        {
            _host.AddHandler(InputElement.PointerPressedEvent, OnHostPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
            _host.PropertyChanged += OnHostChanged;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        DetachHost();
        _timer?.Stop();
        _clock.Reset();
        _alpha = 0;
    }

    private void DetachHost()
    {
        if (_host != null)
        {
            _host.RemoveHandler(InputElement.PointerPressedEvent, OnHostPointerPressed);
            _host.PropertyChanged -= OnHostChanged;
            _host = null;
        }
    }

    private void OnHostPointerPressed(object? sender, PointerEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            var point = e.GetPosition(this);
            _origin = new Point(Math.Clamp(point.X, 0, Bounds.Width), Math.Clamp(point.Y, 0, Bounds.Height));
        }
    }

    private void OnHostChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Button.IsPressedProperty && _host is Button button)
        {
            _pressed = button.IsPressed;
            if (_pressed)
            {
                _origin ??= new Point(Bounds.Width / 2, Bounds.Height / 2);
                _clock.Restart();
                _progress = 0;
                _alpha = 1;
                _releasedAt = double.PositiveInfinity;
                _timer ??= new Avalonia.Threading.DispatcherTimer(TimeSpan.FromMilliseconds(16), Avalonia.Threading.DispatcherPriority.Render, (_, _) => Tick());
                _timer.Start();
            }
            else
                _releasedAt = _clock.Elapsed.TotalMilliseconds;
            if (!_pressed && _alpha > 0) _timer?.Start();
            Tick();
        }
        else if (e.Property == InputElement.IsEnabledProperty && !_host!.IsEffectivelyEnabled)
        {
            _timer?.Stop();
            _alpha = 0;
            _origin = null;
            InvalidateVisual();
        }
    }

    private void Tick()
    {
        var reduced = MaterialMotion.GetIsReduced(this) || this.TryFindResource("MaterialReducedMotion", out var setting) && setting is true;
        var elapsed = _clock.Elapsed.TotalMilliseconds;
        var t = Math.Clamp(elapsed / 300, 0, 1);
        _progress = reduced ? 1 : 1 - Math.Pow(1 - t, 3);
        var fadeStart = Math.Max(180, _releasedAt);
        _alpha = reduced ? (_pressed ? 1 : 0) : 1 - Math.Clamp((elapsed - fadeStart) / 180, 0, 1);
        if (_alpha <= 0 || _pressed && _progress >= 1)
        {
            _timer?.Stop();
            if (!_pressed) _origin = null;
        }
        InvalidateVisual();
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);

        // The ripple is drawn from Bounds, so a container resize has to re-record the drawing.
        if (!_arrangedSize.Equals(size))
        {
            _arrangedSize = size;
            InvalidateVisual();
        }

        return size;
    }

    public override void Render(DrawingContext context)
    {
        var fill = Fill;
        var width = RippleWidth;
        var height = RippleHeight;
        var size = Bounds.Size;

        if (fill is null || !(size.Width > 0) || !(size.Height > 0))
            return;

        var limit = Math.Min(size.Width, size.Height) / 2;
        double Clamp(double value) => double.IsNaN(value) || value < 0 ? 0 : Math.Min(value, limit);
        var corners = CornerRadius;
        var radii = new CornerRadius(
            Clamp(corners.TopLeft), Clamp(corners.TopRight),
            Clamp(corners.BottomRight), Clamp(corners.BottomLeft));

        using (context.PushClip(new RoundedRect(new Rect(size), radii)))
        using (context.PushOpacity(_alpha))
        {
            if (Data is not null)
            {
                var p = _origin ?? new Point(size.Width / 2, size.Height / 2);
                var radius = Math.Sqrt(size.Width * size.Width + size.Height * size.Height) * _progress;
                using (context.PushClip(new RoundedRect(new Rect(p.X - radius, p.Y - radius, radius * 2, radius * 2), radius)))
                using (context.PushTransform(Matrix.CreateTranslation(size.Width - width, size.Height - height)))
                using (context.PushTransform(Matrix.CreateScale(width, height)))
                    context.DrawGeometry(fill, null, Data);
            }
            else
            {
                var p = _origin ?? new Point(size.Width / 2, size.Height / 2);
                var dx = Math.Max(p.X, size.Width - p.X);
                var dy = Math.Max(p.Y, size.Height - p.Y);
                var maxRadius = Math.Sqrt(dx * dx + dy * dy);
                var r = Math.Max(1.0, maxRadius * Math.Max(0.08, _progress));
                context.DrawEllipse(fill, null, p, r, r);
            }
        }
    }
}
