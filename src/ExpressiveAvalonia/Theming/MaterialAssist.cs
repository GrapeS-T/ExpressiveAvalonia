using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Tokens;

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
    // CornerBasedShape uses CircleShape (RoundedCornerShape(50)) for ShapeKeyTokens.CornerFull;
    // this theme spells "full" as the MaterialShapeFullCornerRadius sentinel 9999. Any resting
    // corner at or above this value is treated as the percentage corner and re-resolved to half of
    // the smaller dimension every frame, exactly like a CornerSize whose toPx is size-relative.
    private const double FullCornerThreshold = 1000;

    // MotionSchemeKeyTokens.DefaultEffects is intentional in Button.kt/IconButton.kt to prevent a
    // bounce: both schemes resolve it to the effects default spring (1600 stiffness / 1 damping).
    // ToggleButton.kt instead asks for FastSpatial, which is scheme-dependent, so the semantic
    // MaterialSpring token is used rather than a hard-coded pair.
    private const double FloatVisibilityThreshold = 0.01;

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

    // The animated shadow is resolved from the MaterialElevationLevel*Shadow tokens with the
    // upstream interaction specs; the template binds the surface's BoxShadow to it.
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly AttachedProperty<BoxShadows> AnimatedShadowProperty =
        AvaloniaProperty.RegisterAttached<MaterialAssist, Button, BoxShadows>("AnimatedShadow");

    // ToggleButton.kt's animateBorderStrokeAsState paints the interpolated BorderStroke rather
    // than the styled border target: the width runs through FastSpatial and a solid color runs
    // through DefaultEffects. The template binds the outline border to these two values, so the
    // caller's BorderBrush/BorderThickness stay the untouched target contract.
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly AttachedProperty<Thickness> AnimatedOutlineThicknessProperty =
        AvaloniaProperty.RegisterAttached<MaterialAssist, Button, Thickness>("AnimatedOutlineThickness");

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly AttachedProperty<IBrush?> AnimatedOutlineBrushProperty =
        AvaloniaProperty.RegisterAttached<MaterialAssist, Button, IBrush?>("AnimatedOutlineBrush");

    private static readonly AttachedProperty<ButtonAnatomy?> AnatomyProperty =
        AvaloniaProperty.RegisterAttached<MaterialAssist, Button, ButtonAnatomy?>("Anatomy");

    static MaterialAssist()
    {
        // The anatomy owns the animated shape, the elevation and the resolved silhouette. It is
        // created even when the morph is disabled: AnimateShape only suppresses the corner spring,
        // so a consumer that turns it off still gets the interaction-driven static shape, the
        // elevation states and the focus-ring radius instead of a stale CornerRadius.
        AnimateShapeProperty.Changed.AddClassHandler<Button>((button, _) =>
        {
            if (button.GetValue(AnatomyProperty) is null)
                button.SetValue(AnatomyProperty, new ButtonAnatomy(button));
            else
                button.GetValue(AnatomyProperty)!.Refresh(snap: true);
        });

        Button.CornerRadiusProperty.Changed.AddClassHandler<Button>((button, _) =>
        {
            if (button.GetValue(AnatomyProperty) is null)
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
    public static BoxShadows GetAnimatedShadow(Button button) => button.GetValue(AnimatedShadowProperty);
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void SetAnimatedShadow(Button button, BoxShadows value) => button.SetValue(AnimatedShadowProperty, value);
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static Thickness GetAnimatedOutlineThickness(Button button) => button.GetValue(AnimatedOutlineThicknessProperty);
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void SetAnimatedOutlineThickness(Button button, Thickness value) => button.SetValue(AnimatedOutlineThicknessProperty, value);
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static IBrush? GetAnimatedOutlineBrush(Button button) => button.GetValue(AnimatedOutlineBrushProperty);
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void SetAnimatedOutlineBrush(Button button, IBrush? value) => button.SetValue(AnimatedOutlineBrushProperty, value);

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

    /// <summary>
    /// The pinned <c>ColorScheme.surfaceColorAtElevation</c>: the surface role with the
    /// surface-tint role composited over it.
    /// </summary>
    /// <param name="surface">The <c>surface</c> role color.</param>
    /// <param name="surfaceTint">The <c>surfaceTint</c> role color (primary in the default scheme).</param>
    /// <param name="elevation">The elevation distance in DIP/dp, not a discrete elevation level.</param>
    /// <remarks>
    /// The overlay alpha is exactly <c>((4.5 * ln(elevation + 1)) + 2) / 100</c> from
    /// <c>ColorScheme.kt</c>; zero (and negative) elevation returns the surface unchanged. The
    /// tint is composited in the same sRGB channel space as Compose <c>compositeOver</c>, not by
    /// scaling every surface channel.
    /// </remarks>
    public static Color SurfaceColorAtElevation(Color surface, Color surfaceTint, double elevation)
    {
        if (!(elevation > 0) || !double.IsFinite(elevation))
            return surface;

        var alpha = Math.Clamp(((4.5 * Math.Log(elevation + 1)) + 2) / 100, 0, 1);
        return CompositeOver(surfaceTint, surface, alpha);
    }

    /// <summary>
    /// The pinned <c>ColorScheme.applyTonalElevation</c>: a tonal overlay is applied only when the
    /// component color is exactly the <c>surface</c> role and tonal elevation is enabled.
    /// </summary>
    /// <remarks>
    /// A component that passes <c>surfaceContainer</c>, <c>surfaceContainerLow</c>, primary, or any
    /// other role color keeps that exact color: upstream deliberately does not darken/lighten those
    /// containers with elevation. <paramref name="tonalElevationEnabled"/> mirrors
    /// <c>LocalTonalElevationEnabled</c> and defaults to true.
    /// </remarks>
    public static Color ApplyTonalElevation(
        Color backgroundColor,
        Color surface,
        Color surfaceTint,
        double elevation,
        bool tonalElevationEnabled = true)
        => tonalElevationEnabled && backgroundColor == surface
            ? SurfaceColorAtElevation(surface, surfaceTint, elevation)
            : backgroundColor;

    private static Color CompositeOver(Color foreground, Color background, double foregroundAlpha)
    {
        var backgroundAlpha = background.A / 255.0;
        var alpha = foregroundAlpha + (backgroundAlpha * (1 - foregroundAlpha));
        if (alpha <= 0)
            return Colors.Transparent;

        byte Channel(byte foregroundChannel, byte backgroundChannel)
        {
            var value = (((foregroundChannel / 255.0) * foregroundAlpha)
                         + ((backgroundChannel / 255.0) * backgroundAlpha * (1 - foregroundAlpha))) / alpha;
            return (byte)Math.Clamp(Math.Round(value * 255), 0, 255);
        }

        return Color.FromArgb(
            (byte)Math.Clamp(Math.Round(alpha * 255), 0, 255),
            Channel(foreground.R, background.R),
            Channel(foreground.G, background.G),
            Channel(foreground.B, background.B));
    }

    /// <summary>
    /// The four-corner shape family used by the Material button state machine.
    /// </summary>
    /// <remarks>
    /// Corners are stored in logical order (topStart, topEnd, bottomEnd, bottomStart) as
    /// <see cref="CornerTerm"/>s, mirroring Compose <c>CornerBasedShape</c>. Resolution to
    /// Avalonia's physical <see cref="CornerRadius"/>, the pair scaling of
    /// <c>CornerBasedShape.createOutline</c> and the RTL mapping happen once per frame.
    /// </remarks>
    internal readonly struct CornerShape : IEquatable<CornerShape>
    {
        private readonly CornerTerm _topStart;
        private readonly CornerTerm _topEnd;
        private readonly CornerTerm _bottomEnd;
        private readonly CornerTerm _bottomStart;

        private CornerShape(CornerTerm topStart, CornerTerm topEnd, CornerTerm bottomEnd, CornerTerm bottomStart)
        {
            _topStart = topStart;
            _topEnd = topEnd;
            _bottomEnd = bottomEnd;
            _bottomStart = bottomStart;
        }

        public static CornerShape All(double value) => new(
            new FixedTerm(value), new FixedTerm(value), new FixedTerm(value), new FixedTerm(value));

        public static CornerShape Full => new(
            CornerTerm.Full, CornerTerm.Full, CornerTerm.Full, CornerTerm.Full);

        public static CornerShape FromCornerRadius(CornerRadius radius, bool rightToLeft) =>
            rightToLeft
                ? new CornerShape(Term(radius.TopRight), Term(radius.TopLeft),
                    Term(radius.BottomLeft), Term(radius.BottomRight))
                : new CornerShape(Term(radius.TopLeft), Term(radius.TopRight),
                    Term(radius.BottomRight), Term(radius.BottomLeft));

        /// <summary>
        /// Composes a horizontally arranged silhouette from an edge pair: the start edge
        /// (topStart/bottomStart) comes from <paramref name="start"/>, the end edge
        /// (topEnd/bottomEnd) from <paramref name="end"/>.
        /// </summary>
        /// <remarks>
        /// This is the <c>RoundedCornerShape(topStart = …, topEnd = …)</c> arrangement of
        /// ButtonGroup.kt leaders and trailers, kept in logical order so the flow direction is
        /// applied once by <see cref="Resolve"/>.
        /// </remarks>
        public static CornerShape SplitEdges(CornerShape start, CornerShape end) =>
            new(start._topStart, end._topEnd, end._bottomEnd, start._bottomStart);

        /// <summary>
        /// Composes a vertically arranged silhouette from a row pair: the start row
        /// (topStart/topEnd) comes from <paramref name="start"/>, the end row
        /// (bottomEnd/bottomStart) from <paramref name="end"/>.
        /// </summary>
        public static CornerShape SplitRows(CornerShape start, CornerShape end) =>
            new(start._topStart, start._topEnd, end._bottomEnd, end._bottomStart);

        /// <summary>
        /// Resolves physical corners for a container exactly like <c>CornerBasedShape.createOutline</c>
        /// without building corner terms: a full corner becomes half the smaller side and a start or
        /// end pair wider than the container is scaled down as a pair. Render paths that already hold
        /// physical corners use this instead of <see cref="FromCornerRadius"/> to avoid per-frame terms.
        /// </summary>
        public static CornerRadius ClampToContainer(CornerRadius corners, double minDimension)
        {
            if (minDimension <= 0)
                return default;

            var topLeft = ClampPx(corners.TopLeft);
            var topRight = ClampPx(corners.TopRight);
            var bottomRight = ClampPx(corners.BottomRight);
            var bottomLeft = ClampPx(corners.BottomLeft);

            if (topLeft + bottomLeft > minDimension)
            {
                var scale = minDimension / (topLeft + bottomLeft);
                topLeft *= scale;
                bottomLeft *= scale;
            }
            if (topRight + bottomRight > minDimension)
            {
                var scale = minDimension / (topRight + bottomRight);
                topRight *= scale;
                bottomRight *= scale;
            }

            return new CornerRadius(topLeft, topRight, bottomRight, bottomLeft);

            double ClampPx(double value) => value >= FullCornerThreshold
                ? minDimension / 2
                : value <= 0 ? 0 : value;
        }

        private static CornerTerm Term(double value) => value >= FullCornerThreshold
            ? CornerTerm.Full
            : new FixedTerm(value <= 0 ? 0 : value);

        public static CornerShape Lerp(CornerShape from, CornerShape to, double progress)
        {
            if (progress == 0)
                return from;
            if (progress == 1)
                return to;
            return new CornerShape(
                new BlendTerm(from._topStart, to._topStart, progress),
                new BlendTerm(from._topEnd, to._topEnd, progress),
                new BlendTerm(from._bottomEnd, to._bottomEnd, progress),
                new BlendTerm(from._bottomStart, to._bottomStart, progress));
        }

        /// <summary>
        /// Resolves the interpolated outline exactly like <c>CornerBasedShape.createOutline</c>:
        /// interpolate the resolved corner pixels, scale a start pair or an end pair that exceeds
        /// the smaller dimension, then map logical corners to physical ones for the flow direction.
        /// </summary>
        public CornerRadius Resolve(CornerShape target, double progress, double minDimension, bool rightToLeft)
        {
            if (minDimension <= 0)
                return default;

            var topStart = LerpToPx(_topStart, target._topStart, progress, minDimension);
            var topEnd = LerpToPx(_topEnd, target._topEnd, progress, minDimension);
            var bottomEnd = LerpToPx(_bottomEnd, target._bottomEnd, progress, minDimension);
            var bottomStart = LerpToPx(_bottomStart, target._bottomStart, progress, minDimension);

            if (topStart + bottomStart > minDimension)
            {
                var scale = minDimension / (topStart + bottomStart);
                topStart *= scale;
                bottomStart *= scale;
            }
            if (topEnd + bottomEnd > minDimension)
            {
                var scale = minDimension / (topEnd + bottomEnd);
                topEnd *= scale;
                bottomEnd *= scale;
            }

            return rightToLeft
                ? new CornerRadius(topEnd, topStart, bottomStart, bottomEnd)
                : new CornerRadius(topStart, topEnd, bottomEnd, bottomStart);
        }

        private static double LerpToPx(CornerTerm from, CornerTerm to, double progress, double minDimension)
        {
            var a = from.ToPx(minDimension);
            var b = to.ToPx(minDimension);
            return a + (b - a) * progress;
        }

        public bool Equals(CornerShape other) =>
            _topStart.EqualsTerm(other._topStart)
            && _topEnd.EqualsTerm(other._topEnd)
            && _bottomEnd.EqualsTerm(other._bottomEnd)
            && _bottomStart.EqualsTerm(other._bottomStart);

        public override bool Equals(object? obj) => obj is CornerShape other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(
            _topStart.GetTermHashCode(), _topEnd.GetTermHashCode(),
            _bottomEnd.GetTermHashCode(), _bottomStart.GetTermHashCode());
    }

    /// <summary>A <c>CornerSize</c>: either a DIP value, the 50% "full" corner, or their lerp.</summary>
    private abstract class CornerTerm
    {
        public static readonly CornerTerm Full = new FullTerm();

        public abstract double ToPx(double minDimension);

        public abstract bool EqualsTerm(CornerTerm other);

        public abstract int GetTermHashCode();
    }

    private sealed class FixedTerm : CornerTerm
    {
        private readonly double _value;

        public FixedTerm(double value) => _value = value;

        public override double ToPx(double minDimension) => _value;

        public override bool EqualsTerm(CornerTerm other) => other is FixedTerm fixedTerm && fixedTerm._value == _value;

        public override int GetTermHashCode() => _value.GetHashCode();
    }

    private sealed class FullTerm : CornerTerm
    {
        public override double ToPx(double minDimension) => minDimension / 2;

        public override bool EqualsTerm(CornerTerm other) => other is FullTerm;

        public override int GetTermHashCode() => 17;
    }

    private sealed class BlendTerm : CornerTerm
    {
        private readonly CornerTerm _from;
        private readonly CornerTerm _to;
        private readonly double _progress;

        public BlendTerm(CornerTerm from, CornerTerm to, double progress)
        {
            _from = from;
            _to = to;
            _progress = progress;
        }

        public override double ToPx(double minDimension)
        {
            var a = _from.ToPx(minDimension);
            var b = _to.ToPx(minDimension);
            return a + (b - a) * _progress;
        }

        public override bool EqualsTerm(CornerTerm other) =>
            other is BlendTerm blend
            && blend._progress == _progress
            && _from.EqualsTerm(blend._from)
            && _to.EqualsTerm(blend._to);

        public override int GetTermHashCode() => HashCode.Combine(_progress, _from.GetTermHashCode(), _to.GetTermHashCode());
    }

    /// <summary>
    /// Ports AnimatedShapeState + Button.kt shapeByInteraction and ButtonElevation for one button.
    /// </summary>
    private sealed class ButtonAnatomy : IDisposable
    {
        private readonly Button _button;
        private readonly MaterialElevationAnimation _elevation;
        private MaterialMotion.SpringAnimation? _shapeSpring;
        private CornerShape _shapeFrom;
        private CornerShape _shapeTarget;
        private double _shapeProgress = 1;
        private bool _shapeInitialized;
        private bool _enterPressed;
        private bool _disposed;

        // ToggleButton.kt animateBorderStrokeAsState: the BorderStroke is painted from animated
        // values, never from the styled target. Width is a uniform Dp in the source; the styled
        // Thickness is still resolved edge-by-edge so a caller's non-uniform extension keeps its
        // semantics.
        private readonly double[] _outlineWidths = new double[4];
        private readonly double[] _outlineTargets = new double[4];
        private readonly MaterialMotion.SpringAnimation?[] _outlineWidthSprings =
            new MaterialMotion.SpringAnimation?[4];
        private readonly Action<double>[] _outlineWidthApply;
        private readonly SolidColorBrush _outlineBrush = new(Colors.Transparent);
        private MaterialColorAnimation? _outlineColorAnimation;
        private Color _outlineColor = Colors.Transparent;
        private bool _outlineInitialized;

        public ButtonAnatomy(Button button)
        {
            _button = button;
            _outlineWidthApply =
            [
                value => SetOutlineWidth(0, value),
                value => SetOutlineWidth(1, value),
                value => SetOutlineWidth(2, value),
                value => SetOutlineWidth(3, value),
            ];
            _elevation = new MaterialElevationAnimation(button, ResolveElevationSpecs);
            button.PropertyChanged += OnPropertyChanged;
            button.Classes.CollectionChanged += OnClassesChanged;
            button.AttachedToVisualTree += OnAttached;
            button.DetachedFromVisualTree += OnDetached;
            button.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Bubble, handledEventsToo: true);
            button.AddHandler(InputElement.KeyUpEvent, OnKeyUp, RoutingStrategies.Bubble, handledEventsToo: true);
            Refresh(snap: true);
        }

        // Clickable.kt emits PressInteraction.Press for both Space and Enter on key down. Space
        // already becomes Button.IsPressed on Avalonia; Enter is tracked here so the pressed
        // silhouette and elevation react to keyboard activation as well.
        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter || _enterPressed || !_button.IsEffectivelyEnabled)
                return;
            _enterPressed = true;
            Refresh(snap: false);
        }

        private void OnKeyUp(object? sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter || !_enterPressed)
                return;
            _enterPressed = false;
            Refresh(snap: false);
        }

        private void OnAttached(object? sender, VisualTreeAttachmentEventArgs e)
        {
            // The first Refresh in the constructor can run before the theme resources are
            // reachable; the shared elevation helper retries its tokens on attach as well.
            Refresh(snap: true);
        }

        private void OnDetached(object? sender, VisualTreeAttachmentEventArgs e)
        {
            _enterPressed = false;
            StopShape();
            StopOutline();
            _shapeInitialized = false;
            _outlineInitialized = false;
        }

        private void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs args)
        {
            if (args.Property == Button.IsPressedProperty
                || args.Property == ToggleButton.IsCheckedProperty
                || args.Property == InputElement.IsPointerOverProperty
                || args.Property == InputElement.IsKeyboardFocusWithinProperty
                || args.Property == InputElement.IsEnabledProperty
                || args.Property == InputElement.IsEffectivelyEnabledProperty)
            {
                if (!_button.IsKeyboardFocusWithin || !_button.IsEffectivelyEnabled)
                    _enterPressed = false;
                Refresh(snap: false);
            }
            else if (args.Property == Visual.BoundsProperty)
            {
                // A resize re-resolves a percentage corner without restarting the morph.
                if (_shapeInitialized)
                    ApplyShape(_shapeProgress);
            }
            else if (args.Property == Visual.FlowDirectionProperty)
            {
                // Corners are stored logically; only the physical mapping changes, so the current
                // morph progress is re-resolved without a new spring segment.
                if (_shapeInitialized)
                    ApplyShape(_shapeProgress);
            }
            else if (args.Property == Button.BorderBrushProperty
                     || args.Property == Button.BorderThicknessProperty)
            {
                UpdateOutline(snap: !_outlineInitialized || MaterialMotion.GetIsReduced(_button));
            }
            else if (args.Property == Button.CornerRadiusProperty
                     || args.Property == ControlSizeProperty
                     || args.Property == ButtonShapeProperty
                     || args.Property == IsIconButtonProperty
                     || args.Property == ContainerInsetProperty
                     || args.Property == ButtonVariantProperty
                     || args.Property == MaterialMotion.IsReducedProperty)
            {
                Refresh(snap: !_shapeInitialized || MaterialMotion.GetIsReduced(_button));
            }
        }

        public void Refresh(bool snap)
        {
            UpdateShape(snap);
            UpdateOutline(snap);
            _elevation.Refresh(snap);
        }

        private void OnClassesChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
            Refresh(snap: false);

        private bool IsEnabled => _button.IsEffectivelyEnabled;

        private CornerShape ResolveShapeTarget()
        {
            var size = GetControlSize(_button);
            if ((_button.IsPressed || _enterPressed) && IsEnabled)
                return CornerShape.All(size switch
                {
                    MaterialControlSize.ExtraSmall or MaterialControlSize.Small => 8,
                    MaterialControlSize.Medium => 12,
                    _ => 16,
                });

            // ToggleButtonDefaults.defaultToggleButtonShapes: the checked shape is
            // SelectedContainerShapeSquare for labeled buttons, independently of the resting
            // Round/Square axis. IconButtonDefaults.defaultIconToggleButtonShapes uses
            // SelectedContainerShapeRound; the exposed SelectedContainerShapeSquare pairs with a
            // square resting icon button.
            if (_button is ToggleButton { IsChecked: true })
            {
                var selectedRadius = size switch
                {
                    MaterialControlSize.ExtraSmall or MaterialControlSize.Small => 12,
                    MaterialControlSize.Medium => 16,
                    _ => 28,
                };
                return GetIsIconButton(_button) && GetButtonShape(_button) == MaterialButtonShape.Square
                    ? CornerShape.Full
                    : CornerShape.All(selectedRadius);
            }

            return CornerShape.FromCornerRadius(
                _button.CornerRadius, _button.FlowDirection == FlowDirection.RightToLeft);
        }

        private void UpdateShape(bool snap)
        {
            var target = ResolveShapeTarget();
            if (snap
                || !_shapeInitialized
                || !_button.IsAttachedToVisualTree()
                || MaterialMotion.GetIsReduced(_button)
                || !GetAnimateShape(_button))
            {
                StopShape();
                _shapeFrom = _shapeTarget = target;
                _shapeProgress = 1;
                _shapeInitialized = true;
                ApplyShape(1);
                return;
            }

            if (target.Equals(_shapeTarget))
                return;

            if (_shapeSpring is null)
            {
                // Resting state: start a new segment from the shape currently displayed.
                _shapeFrom = _shapeTarget;
                _shapeTarget = target;
                _shapeProgress = 0;
                StartShapeSpring(0, 1, 0);
                ApplyShape(0);
                return;
            }

            var state = _shapeSpring.SampleState();
            _shapeProgress = state.Position;

            if (target.Equals(_shapeFrom))
            {
                // The user reversed before the morph finished: flip progress and velocity, as
                // AnimatedShapeState.animateToShape does.
                var velocity = state.Velocity;
                var from = _shapeTarget;
                _shapeTarget = target;
                _shapeFrom = from;
                _shapeProgress = 1 - _shapeProgress;
                StartShapeSpring(_shapeProgress, 1, -velocity);
                ApplyShape(_shapeProgress);
                return;
            }

            // A third target: freeze the currently visible interpolated shape and morph toward
            // the new target from zero. Exact 0/1 boundaries avoid nesting blended corner terms.
            var progress = _shapeProgress;
            _shapeFrom = progress == 1
                ? _shapeTarget
                : progress == 0
                    ? _shapeFrom
                    : CornerShape.Lerp(_shapeFrom, _shapeTarget, progress);
            _shapeTarget = target;
            _shapeProgress = 0;
            StartShapeSpring(0, 1, 0);
            ApplyShape(0);
        }

        private void StartShapeSpring(double from, double to, double initialVelocity)
        {
            _shapeSpring?.Dispose();
            // ToggleButton.kt shapeByInteraction uses FastSpatial; Button.kt and IconButton.kt use
            // DefaultEffects. Icon toggles are the IconButton implementation, so only the labeled
            // ToggleButton takes the spatial spec.
            var spring = _button is ToggleButton && !GetIsIconButton(_button)
                ? MaterialSpring.SpatialFast
                : MaterialSpring.EffectsDefault;
            _shapeSpring = MaterialMotion.Animate(
                _button, from, to, ApplyShape,
                spring, FloatVisibilityThreshold,
                initialVelocity: initialVelocity);
        }

        private void ApplyShape(double progress)
        {
            _shapeProgress = progress;
            var (_, _, minDimension) = ContainerSize();
            var rightToLeft = _button.FlowDirection == FlowDirection.RightToLeft;
            _button.SetCurrentValue(AnimatedCornerRadiusProperty,
                _shapeFrom.Resolve(_shapeTarget, progress, minDimension, rightToLeft));
        }

        private void StopShape()
        {
            _shapeSpring?.Dispose();
            _shapeSpring = null;
        }

        /// <summary>
        /// ToggleButton.kt <c>animateBorderStrokeAsState</c>: the styled BorderStroke is the target,
        /// the painted stroke is the interpolation. Width uses FastSpatial and a solid color uses
        /// DefaultEffects, so a checked outlined toggle fades its outline away instead of snapping
        /// to the source's null border. This only paints template values; the consumer's
        /// BorderBrush/BorderThickness are never mutated.
        /// </summary>
        private void UpdateOutline(bool snap)
        {
            var thickness = _button.BorderThickness;
            _outlineTargets[0] = thickness.Left;
            _outlineTargets[1] = thickness.Top;
            _outlineTargets[2] = thickness.Right;
            _outlineTargets[3] = thickness.Bottom;

            var brush = _button.BorderBrush;
            var targetColor = brush switch
            {
                null => Colors.Transparent,
                ISolidColorBrush solid => solid.Color,
                _ => (Color?)null,
            };

            // The source only animates the labeled ToggleButton implementation, and animateDpAsState
            // starts at its target on first composition rather than animating from the property
            // default. A zero-width pre-layout control is that first composition.
            var animate = !snap
                && _outlineInitialized
                && _button is ToggleButton
                && !GetIsIconButton(_button)
                && _button.IsAttachedToVisualTree()
                && !MaterialMotion.GetIsReduced(_button)
                && _button.Bounds.Width > 0;

            for (var i = 0; i < _outlineWidths.Length; i++)
            {
                var target = _outlineTargets[i];
                var spring = _outlineWidthSprings[i];
                if (!animate)
                {
                    spring?.Dispose();
                    _outlineWidthSprings[i] = null;
                    _outlineWidths[i] = target;
                    continue;
                }

                if (spring is not null)
                {
                    // Retargeting keeps the current position and velocity, so a rapid
                    // checked->unchecked reversal continues from the painted width.
                    spring.Retarget(target);
                }
                else if (Math.Abs(_outlineWidths[i] - target) > FloatVisibilityThreshold)
                {
                    _outlineWidthSprings[i] = MaterialMotion.Animate(
                        _button, _outlineWidths[i], target, _outlineWidthApply[i],
                        MaterialSpring.SpatialFast, FloatVisibilityThreshold);
                }
                else
                {
                    _outlineWidths[i] = target;
                }
            }
            ApplyOutlineWidths();

            if (targetColor is null)
            {
                // animateBorderStrokeAsState keeps a non-solid brush as-is and only animates its
                // width; the width channel above already handled that.
                _outlineColorAnimation?.Dispose();
                _outlineColorAnimation = null;
                _button.SetCurrentValue(AnimatedOutlineBrushProperty, brush);
                _outlineInitialized = true;
                return;
            }

            if (!animate)
            {
                _outlineColorAnimation?.Dispose();
                _outlineColorAnimation = null;
                _outlineColor = targetColor.Value;
                ApplyOutlineColor();
            }
            else
            {
                _outlineColorAnimation ??= new MaterialColorAnimation(
                    _button, _outlineColor, ApplyOutlineColor, MaterialSpring.EffectsDefault);
                _outlineColorAnimation.Retarget(targetColor.Value);
            }

            _outlineInitialized = true;
        }

        private void SetOutlineWidth(int index, double value)
        {
            _outlineWidths[index] = value;
            ApplyOutlineWidths();
        }

        private void ApplyOutlineWidths()
        {
            _button.SetCurrentValue(AnimatedOutlineThicknessProperty,
                new Thickness(_outlineWidths[0], _outlineWidths[1], _outlineWidths[2], _outlineWidths[3]));
        }

        private void ApplyOutlineColor()
        {
            ApplyOutlineColor(_outlineColor);
        }

        private void ApplyOutlineColor(Color color)
        {
            _outlineColor = color;
            _outlineBrush.Color = color;
            _button.SetCurrentValue(AnimatedOutlineBrushProperty, _outlineBrush);
        }

        private void StopOutline()
        {
            for (var i = 0; i < _outlineWidthSprings.Length; i++)
            {
                _outlineWidthSprings[i]?.Dispose();
                _outlineWidthSprings[i] = null;
            }
            _outlineColorAnimation?.Dispose();
            _outlineColorAnimation = null;
        }

        private (double Width, double Height, double MinDimension) ContainerSize()
        {
            var inset = GetContainerInset(_button);
            var height = Math.Max(0, (_button.Bounds.Height > 0 ? _button.Bounds.Height : _button.MinHeight) - inset.Top - inset.Bottom);
            var width = Math.Max(0, (_button.Bounds.Width > 0 ? _button.Bounds.Width : _button.MinWidth) - inset.Left - inset.Right);
            return (width, height, Math.Min(width, height));
        }

        /// <summary>
        /// ButtonElevation values from the pinned *ButtonTokens: the default and small button
        /// elevation, plus the hovered, pressed, focused and disabled states.
        /// </summary>
        private MaterialElevationSpecs ResolveElevationSpecs()
        {
            // Icon buttons have no elevation tokens in the pinned source. Elevated is an Avalonia
            // extension of the shared variant enum, so it keeps the elevated button specs instead
            // of silently losing its container shadow.
            if (GetIsIconButton(_button) && GetButtonVariant(_button) != MaterialButtonVariant.Elevated)
                return default;

            return GetButtonVariant(_button) switch
            {
                MaterialButtonVariant.Elevated => new MaterialElevationSpecs(1, 1, 1, 2, 0, 0),
                MaterialButtonVariant.Outlined or MaterialButtonVariant.Text => default,
                // FilledButtonTokens/TonalButtonTokens: hovered is Level1, every other non-hover
                // state is Level0. ToggleButtonDefaults reuses these specs.
                _ => new MaterialElevationSpecs(0, 0, 0, 1, 0, 0),
            };
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            StopShape();
            StopOutline();
            _elevation.Dispose();
            _button.PropertyChanged -= OnPropertyChanged;
            _button.Classes.CollectionChanged -= OnClassesChanged;
            _button.AttachedToVisualTree -= OnAttached;
            _button.DetachedFromVisualTree -= OnDetached;
            _button.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
            _button.RemoveHandler(InputElement.KeyUpEvent, OnKeyUp);
        }
    }
}

/// <summary>
/// The Material press ripple and state layer of one interactive target, ported from the pinned
/// material-ripple <c>RippleNode</c>, <c>RippleAnimation</c> and <c>StateLayer</c>.
/// </summary>
/// <remarks>
/// <para>
/// The ripple is a circular expansion, not a vector silhouette. Bounded ripples start at the
/// pointer press position and are clipped by <see cref="CornerRadius"/>; unbounded ripples always
/// start at the target centre and are never clipped. The end radius is the diagonal half length
/// plus 10 DIP for bounded targets, the start radius is 30% of the largest dimension, and the
/// timeline is exactly the source's: alpha fades in over 75 ms linearly while the radius grows
/// over 225 ms with fast-out-slow-in and the centre travels for the full 225 ms; release or cancel
/// fades the alpha out over 150 ms. A short click therefore jumps the alpha to one, finishes the
/// expansion, then fades, and a new press finishes the previous ripples first.
/// </para>
/// <para>
/// The same node draws the hover, focus and drag state layer exactly like <c>RippleNode</c>, using
/// the most recently arrived interaction and the source's 15/45/45 ms incoming and 15/15/150 ms
/// outgoing linear tweens. Press is not added to the state layer: the press indication is the
/// ripple itself, so the same alpha is never painted twice.
/// </para>
/// <para>
/// Input is read from the templated parent (or <see cref="InteractionTarget"/>): pointer press and
/// release through the tunnelled handlers (the ripple starts at the press position), Space/Enter
/// on key down and key up (the ripple starts at the centre), pointer over, keyboard focus and
/// effective enabled state. <see cref="Button.IsPressed"/> is only consulted when a template is
/// applied while the native host is already pressed, because that case has no pointer origin to
/// read. The node is purely decorative and never participates in hit testing.
/// </para>
/// <para>
/// A press or key that starts inside a nested interactive action belongs to that action, exactly
/// like Compose's inner clickable consuming the down event, so the host's own ripple does not
/// start for it. The host's own template visuals, content presenters and icons are never
/// classified as actions, so a host always keeps its own press.
/// </para>
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class MaterialRipple : Control, Avalonia.Rendering.ICustomHitTest
{
    private const double FadeInSeconds = 0.075;
    private const double RadiusSeconds = 0.225;
    private const double FadeOutSeconds = 0.150;
    private const double BoundedExtraRadius = 10;
    private const double StartRadiusFraction = 0.3;
    private const double HoverInSeconds = 0.015;
    private const double FocusInSeconds = 0.045;
    private const double DragInSeconds = 0.045;
    private const double HoverOutSeconds = 0.015;
    private const double FocusOutSeconds = 0.015;
    private const double DragOutSeconds = 0.150;

    private static readonly Func<double, double> FastOutSlowIn = new SplineEasing(0.4, 0, 0.2, 1).Ease;

    /// <summary>Gets or sets the ripple and state-layer brush; the theme passes the content color.</summary>
    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<MaterialRipple, IBrush?>(nameof(Fill));

    /// <summary>Gets or sets the silhouette a bounded ripple is clipped by.</summary>
    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<MaterialRipple, CornerRadius>(nameof(CornerRadius));

    /// <summary>Whether the ripple is clipped by the target bounds and starts at the press point.</summary>
    public static readonly StyledProperty<bool> IsBoundedProperty =
        AvaloniaProperty.Register<MaterialRipple, bool>(nameof(IsBounded), true);

    /// <summary>An explicit end radius in DIP; NaN derives it from the target size.</summary>
    public static readonly StyledProperty<double> RadiusProperty =
        AvaloniaProperty.Register<MaterialRipple, double>(nameof(Radius), double.NaN);

    /// <summary>Gets or sets the pressed ripple alpha (StateTokens.PressedStateLayerOpacity).</summary>
    public static readonly StyledProperty<double> PressOpacityProperty =
        AvaloniaProperty.Register<MaterialRipple, double>(nameof(PressOpacity), 0.10, validate: IsOpacity);

    /// <summary>Gets or sets the hover state-layer alpha.</summary>
    public static readonly StyledProperty<double> HoverOpacityProperty =
        AvaloniaProperty.Register<MaterialRipple, double>(nameof(HoverOpacity), 0.08, validate: IsOpacity);

    /// <summary>Gets or sets the focus state-layer alpha.</summary>
    public static readonly StyledProperty<double> FocusOpacityProperty =
        AvaloniaProperty.Register<MaterialRipple, double>(nameof(FocusOpacity), 0.10, validate: IsOpacity);

    /// <summary>Gets or sets the drag state-layer alpha.</summary>
    public static readonly StyledProperty<double> DraggedOpacityProperty =
        AvaloniaProperty.Register<MaterialRipple, double>(nameof(DraggedOpacity), 0.16, validate: IsOpacity);

    /// <summary>Whether a press draws a ripple.</summary>
    public static readonly StyledProperty<bool> EnablePressProperty =
        AvaloniaProperty.Register<MaterialRipple, bool>(nameof(EnablePress), true);

    /// <summary>Whether hover draws a state layer.</summary>
    public static readonly StyledProperty<bool> EnableHoverProperty =
        AvaloniaProperty.Register<MaterialRipple, bool>(nameof(EnableHover), true);

    /// <summary>Whether focus draws a state layer.</summary>
    public static readonly StyledProperty<bool> EnableFocusProperty =
        AvaloniaProperty.Register<MaterialRipple, bool>(nameof(EnableFocus), true);

    /// <summary>Whether drag draws a state layer.</summary>
    public static readonly StyledProperty<bool> EnableDragProperty =
        AvaloniaProperty.Register<MaterialRipple, bool>(nameof(EnableDrag), true);

    /// <summary>Whether the host reports an active drag.</summary>
    public static readonly StyledProperty<bool> IsDraggingProperty =
        AvaloniaProperty.Register<MaterialRipple, bool>(nameof(IsDragging));

    /// <summary>The control whose input drives the ripple; defaults to the templated parent.</summary>
    public static readonly StyledProperty<Control?> InteractionTargetProperty =
        AvaloniaProperty.Register<MaterialRipple, Control?>(nameof(InteractionTarget));

    private readonly List<PressRipple> _ripples = new(2);
    private readonly List<RippleInteraction> _interactions = new(3);
    private readonly System.Diagnostics.Stopwatch _clock = new();
    private Control? _host;
    private Button? _buttonHost;
    private PressRipple? _pointerPress;
    private readonly Dictionary<Key, PressRipple> _keyPresses = new(2);
    private PressRipple? _unknownPress;
    private MaterialMotion.TweenAnimation? _stateTween;
    private double _stateAlpha;
    private double _stateTarget;
    private RippleInteraction? _stateInteraction;
    private bool _framePending;
    private TopLevel? _frameRoot;
    private int _frameGeneration;
    private Action<TimeSpan>? _onOwnedFrame;
    private bool _detached = true;

    static MaterialRipple()
    {
        AffectsRender<MaterialRipple>(
            FillProperty, CornerRadiusProperty, IsBoundedProperty, RadiusProperty, IsDraggingProperty,
            PressOpacityProperty, HoverOpacityProperty, FocusOpacityProperty, DraggedOpacityProperty,
            EnablePressProperty, EnableHoverProperty, EnableFocusProperty, EnableDragProperty);
        InteractionTargetProperty.Changed.AddClassHandler<MaterialRipple>(
            static (ripple, _) => ripple.RebindHost());
        IsDraggingProperty.Changed.AddClassHandler<MaterialRipple>(
            static (ripple, _) => ripple.UpdateStateLayer());
        EnableHoverProperty.Changed.AddClassHandler<MaterialRipple>(
            static (ripple, _) => ripple.UpdateStateLayer());
        EnableFocusProperty.Changed.AddClassHandler<MaterialRipple>(
            static (ripple, _) => ripple.UpdateStateLayer());
        EnableDragProperty.Changed.AddClassHandler<MaterialRipple>(
            static (ripple, _) => ripple.UpdateStateLayer());
        MaterialMotion.IsReducedProperty.Changed.AddClassHandler<MaterialRipple>(
            static (ripple, _) => ripple.OnReducedMotionChanged());
    }

    public MaterialRipple()
    {
        IsHitTestVisible = false;
        Focusable = false;
    }

    bool Avalonia.Rendering.ICustomHitTest.HitTest(Point point) => false;

    /// <summary>Gets or sets the ripple and state-layer brush.</summary>
    public IBrush? Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    /// <summary>Gets or sets the rounded silhouette used to clip a bounded ripple.</summary>
    public CornerRadius CornerRadius
    {
        get => GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    /// <summary>Gets or sets whether the ripple is bounded by the target.</summary>
    public bool IsBounded
    {
        get => GetValue(IsBoundedProperty);
        set => SetValue(IsBoundedProperty, value);
    }

    /// <summary>Gets or sets an explicit end radius in DIP.</summary>
    public double Radius
    {
        get => GetValue(RadiusProperty);
        set => SetValue(RadiusProperty, value);
    }

    /// <summary>Gets or sets the pressed ripple alpha.</summary>
    public double PressOpacity
    {
        get => GetValue(PressOpacityProperty);
        set => SetValue(PressOpacityProperty, value);
    }

    /// <summary>Gets or sets the hover state-layer alpha.</summary>
    public double HoverOpacity
    {
        get => GetValue(HoverOpacityProperty);
        set => SetValue(HoverOpacityProperty, value);
    }

    /// <summary>Gets or sets the focus state-layer alpha.</summary>
    public double FocusOpacity
    {
        get => GetValue(FocusOpacityProperty);
        set => SetValue(FocusOpacityProperty, value);
    }

    /// <summary>Gets or sets the drag state-layer alpha.</summary>
    public double DraggedOpacity
    {
        get => GetValue(DraggedOpacityProperty);
        set => SetValue(DraggedOpacityProperty, value);
    }

    /// <summary>Gets or sets whether a press draws a ripple.</summary>
    public bool EnablePress
    {
        get => GetValue(EnablePressProperty);
        set => SetValue(EnablePressProperty, value);
    }

    /// <summary>Gets or sets whether hover draws a state layer.</summary>
    public bool EnableHover
    {
        get => GetValue(EnableHoverProperty);
        set => SetValue(EnableHoverProperty, value);
    }

    /// <summary>Gets or sets whether focus draws a state layer.</summary>
    public bool EnableFocus
    {
        get => GetValue(EnableFocusProperty);
        set => SetValue(EnableFocusProperty, value);
    }

    /// <summary>Gets or sets whether drag draws a state layer.</summary>
    public bool EnableDrag
    {
        get => GetValue(EnableDragProperty);
        set => SetValue(EnableDragProperty, value);
    }

    /// <summary>Gets or sets whether the host reports an active drag.</summary>
    public bool IsDragging
    {
        get => GetValue(IsDraggingProperty);
        set => SetValue(IsDraggingProperty, value);
    }

    /// <summary>Gets or sets the control whose input drives the ripple.</summary>
    public Control? InteractionTarget
    {
        get => GetValue(InteractionTargetProperty);
        set => SetValue(InteractionTargetProperty, value);
    }

    private static bool IsOpacity(double value) => double.IsFinite(value) && value is >= 0 and <= 1;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _detached = false;
        _clock.Restart();
        RebindHost();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _detached = true;
        UnbindHost();
        _ripples.Clear();
        _interactions.Clear();
        _stateInteraction = null;
        _stateAlpha = 0;
        _stateTween?.Dispose();
        _stateTween = null;
        _clock.Reset();
    }

    private void RebindHost()
    {
        if (_detached)
            return;

        var host = InteractionTarget
                   ?? TemplatedParent as Control
                   ?? this.FindAncestorOfType<Button>()
                   ?? this.FindAncestorOfType<TemplatedControl>();
        if (ReferenceEquals(host, _host))
            return;

        UnbindHost();
        _host = host;
        if (_host is null)
            return;

        _buttonHost = _host as Button;
        _host.PropertyChanged += OnHostPropertyChanged;
        _host.Classes.CollectionChanged += OnHostClassesChanged;
        _host.AddHandler(InputElement.PointerPressedEvent, OnHostPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        _host.AddHandler(InputElement.PointerReleasedEvent, OnHostPointerReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        _host.AddHandler(InputElement.PointerCaptureLostEvent, OnHostPointerCaptureLost, RoutingStrategies.Tunnel, handledEventsToo: true);
        _host.AddHandler(InputElement.KeyDownEvent, OnHostKeyDown, RoutingStrategies.Bubble, handledEventsToo: true);
        _host.AddHandler(InputElement.KeyUpEvent, OnHostKeyUp, RoutingStrategies.Bubble, handledEventsToo: true);
        _host.AddHandler(InputElement.LostFocusEvent, OnHostLostFocus, RoutingStrategies.Bubble, handledEventsToo: true);

        UpdateStateLayer();
        if (!_host.IsEffectivelyEnabled)
        {
            CancelInteractions();
        }
        else if (_buttonHost?.IsPressed == true && _pointerPress is null && _keyPresses.Count == 0)
        {
            // The template was applied while the native host was already pressed (a recycled
            // container): the press position is not available, so this falls back to the centre.
            // Either release path finishes it because the source is unknown.
            _unknownPress = StartPress(origin: null);
        }
    }

    private void UnbindHost()
    {
        if (_host is null)
            return;

        ResetHostInteractions();
        _host.PropertyChanged -= OnHostPropertyChanged;
        _host.Classes.CollectionChanged -= OnHostClassesChanged;
        _host.RemoveHandler(InputElement.PointerPressedEvent, OnHostPointerPressed);
        _host.RemoveHandler(InputElement.PointerReleasedEvent, OnHostPointerReleased);
        _host.RemoveHandler(InputElement.PointerCaptureLostEvent, OnHostPointerCaptureLost);
        _host.RemoveHandler(InputElement.KeyDownEvent, OnHostKeyDown);
        _host.RemoveHandler(InputElement.KeyUpEvent, OnHostKeyUp);
        _host.RemoveHandler(InputElement.LostFocusEvent, OnHostLostFocus);
        _host = null;
        _buttonHost = null;
        _frameRoot = null;
        _framePending = false;
        _frameGeneration++;
        _onOwnedFrame = null;
    }

    private void OnHostPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_host is null)
            return;

        if (e.Property == StyledElement.DataContextProperty || e.Property == ContentControl.ContentProperty)
        {
            ResetHostInteractions();
            UpdateStateLayer();
            return;
        }

        if (e.Property == InputElement.IsPointerOverProperty
                 || e.Property == InputElement.IsKeyboardFocusWithinProperty
                 || e.Property == InputElement.IsEnabledProperty
                 || e.Property == InputElement.IsEffectivelyEnabledProperty)
        {
            if (!_host.IsEffectivelyEnabled)
                CancelInteractions();
            else
                UpdateStateLayer();
        }
        else if (e.Property == TemplatedControl.ForegroundProperty && Fill is null)
        {
            // The ripple color is LocalContentColor in the source; without an explicit Fill the
            // host's foreground is that content color, so a changed role must repaint.
            InvalidateVisual();
        }
    }

    // :focus-visible is Avalonia's input-modality signal; the old theme used it for the state
    // layer so a pointer click does not leave a focus tint, matching desktop Compose where a
    // pointer activation does not request focus. The pseudo-class lives in Classes and can
    // arrive after IsKeyboardFocusWithin, hence the second update.
    private void OnHostClassesChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateStateLayer();

    private void OnHostLostFocus(object? sender, RoutedEventArgs e)
    {
        foreach (var press in _keyPresses.Values) FinishPress(press);
        _keyPresses.Clear();
        FinishPress(_unknownPress);
        _unknownPress = null;
    }

    private void OnHostPointerPressed(object? sender, PointerEventArgs e)
    {
        if (_host is null || !_host.IsEffectivelyEnabled || !EnablePress)
            return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        if (IsNestedActionSource(e.Source))
            return;
        // The pointer press is the interaction for every host, including a Button. Avalonia calls
        // the host's class handler before this instance handler, so a Button.IsPressed change
        // cannot carry the press position: start from the event itself, in the ripple's space.
        var position = e.GetPosition(this);
        var origin = new Point(
            Math.Clamp(position.X, 0, Math.Max(0, Bounds.Width)),
            Math.Clamp(position.Y, 0, Math.Max(0, Bounds.Height)));
        _pointerPress = StartPress(origin);
    }

    private void OnHostPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Left)
            return;
        FinishPress(_pointerPress);
        FinishPress(_unknownPress);
        _pointerPress = _unknownPress = null;
    }

    private void OnHostPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        FinishPress(_pointerPress);
        FinishPress(_unknownPress);
        _pointerPress = _unknownPress = null;
    }

    // Native Button owns activation; the indication observes Space and Enter independently.
    private void OnHostKeyDown(object? sender, KeyEventArgs e)
    {
        if (!HostEnabled || !EnablePress || _keyPresses.ContainsKey(e.Key))
            return;
        if (e.Key is not (Key.Enter or Key.Space) || IsNestedActionSource(e.Source))
            return;
        if (StartPress(origin: null) is { } press)
            _keyPresses.Add(e.Key, press);
    }

    // A nested interactive action owns its own indication: Compose's inner clickable consumes the
    // down event in the Main pass, so the outer RippleNode never starts. Avalonia instead delivers
    // the tunnelled press to every ancestor before the child can mark it handled, so the origin is
    // classified instead. The walk stops at the host, which keeps the host's own template visuals,
    // content presenters and icons from suppressing its ripple; the host itself is never visited,
    // so a button, row or toggle that hosts a ripple still draws its own press.
    private bool IsNestedActionSource(object? source)
    {
        for (var visual = source as Visual;
             visual is not null && !ReferenceEquals(visual, _host);
             visual = visual.GetVisualParent())
        {
            if (visual is Button or ToggleButton or TextBox or ComboBox or Slider or ScrollBar
                or MenuItem or ListBoxItem)
                return true;

            // A nested card is an independent action only when it is itself clickable; a plain
            // nested surface stays part of this target.
            if (visual is MaterialCard { IsInteractive: true })
                return true;
        }

        return false;
    }

    private void OnHostKeyUp(object? sender, KeyEventArgs e)
    {
        if (_keyPresses.Remove(e.Key, out var press)) FinishPress(press);
        FinishPress(_unknownPress);
        _unknownPress = null;
    }

    private bool HostEnabled => _host?.IsEffectivelyEnabled ?? false;

    // Buttons and selection controls focus on a pointer click in Avalonia, while desktop
    // Compose does not request focus from a pointer activation. Restrict the focus layer to
    // :focus-visible so only keyboard focus paints it.
    private bool IsHostFocusVisible =>
        _host is { IsKeyboardFocusWithin: true } host
        && host.Classes.Contains(":focus-visible");

    private void UpdateStateLayer()
    {
        if (_host is null)
            return;

        SetInteraction(RippleInteraction.Hover, EnableHover && HostEnabled && _host.IsPointerOver);
        SetInteraction(RippleInteraction.Focus, EnableFocus && HostEnabled && IsHostFocusVisible);
        SetInteraction(RippleInteraction.Drag, EnableDrag && HostEnabled && IsDragging);

        // A disabled host loses every interaction. The source still runs the outgoing state-layer
        // tween instead of snapping, so disabled is handled like "no interaction" below.
        var next = _interactions.Count > 0 ? _interactions[^1] : (RippleInteraction?)null;
        double target;
        double duration;
        if (next is { } kind)
        {
            target = kind switch
            {
                RippleInteraction.Hover => HoverOpacity,
                RippleInteraction.Focus => FocusOpacity,
                _ => DraggedOpacity,
            };
            duration = next == _stateInteraction
                ? 0.015
                : kind switch
                {
                    RippleInteraction.Hover => HoverInSeconds,
                    RippleInteraction.Focus => FocusInSeconds,
                    _ => DragInSeconds,
                };
        }
        else
        {
            target = 0;
            duration = _stateInteraction switch
            {
                RippleInteraction.Hover => HoverOutSeconds,
                RippleInteraction.Focus => FocusOutSeconds,
                RippleInteraction.Drag => DragOutSeconds,
                _ => HoverOutSeconds,
            };
        }

        if (next == _stateInteraction && Math.Abs(target - _stateTarget) < 1e-9)
            return;

        _stateInteraction = next;
        _stateTarget = target;

        if (MaterialMotion.GetIsReduced(this))
        {
            SnapStateAlpha(target);
            return;
        }

        if (_stateTween is null)
            _stateTween = MaterialMotion.Tween(this, _stateAlpha, target, duration, 0, ApplyStateAlpha);
        else
            _stateTween.Retarget(target, duration);
    }

    private void OnReducedMotionChanged()
    {
        if (MaterialMotion.GetIsReduced(this))
        {
            // Reduced motion finishes the state layer instantly and drops ripples that were already
            // releasing; an active press keeps its single static ripple.
            SnapStateAlpha(_stateTarget);
            for (var i = _ripples.Count - 1; i >= 0; i--)
            {
                if (_ripples[i].FinishSeconds is not null)
                    _ripples.RemoveAt(i);
            }
            InvalidateVisual();
        }
        else if (_ripples.Count > 0)
        {
            RequestFrame();
        }
    }

    private void SetInteraction(RippleInteraction interaction, bool active)
    {
        var index = _interactions.IndexOf(interaction);
        if (active && index < 0)
            _interactions.Add(interaction);
        else if (!active && index >= 0)
            _interactions.RemoveAt(index);
    }

    private void ApplyStateAlpha(double alpha)
    {
        _stateAlpha = alpha;
        InvalidateVisual();
    }

    private void SnapStateAlpha(double alpha)
    {
        _stateTween?.Dispose();
        _stateTween = null;
        _stateAlpha = alpha;
        InvalidateVisual();
    }

    private void ResetHostInteractions()
    {
        _pointerPress = _unknownPress = null;
        _keyPresses.Clear();
        _ripples.Clear();
        _interactions.Clear();
        _stateInteraction = null;
        _stateTarget = 0;
        SnapStateAlpha(0);
    }

    private void CancelInteractions()
    {
        _interactions.Clear();
        if (_host is not null)
        {
            // Re-evaluate against the current host state so the outgoing tween starts from the
            // same interaction the source would use after its Cancel events.
            UpdateStateLayer();
        }
        else
        {
            _stateInteraction = null;
            _stateTarget = 0;
            SnapStateAlpha(0);
        }
        _pointerPress = _unknownPress = null;
        _keyPresses.Clear();
        FinishPresses();
    }

    private PressRipple? StartPress(Point? origin)
    {
        if (!EnablePress || !HostEnabled)
            return null;

        // RippleNode finishes every existing ripple before starting a new one.
        FinishPresses();

        var reduced = MaterialMotion.GetIsReduced(this);
        // RippleNode uses the press position only for a bounded ripple; an unbounded ripple
        // always starts at the target centre (RippleAnimation receives origin = null).
        var ripple = new PressRipple
        {
            StartSeconds = _clock.Elapsed.TotalSeconds,
            Origin = IsBounded ? origin : null,
        };
        _ripples.Add(ripple);
        InvalidateVisual();
        if (!reduced)
            RequestFrame();
        return ripple;
    }

    private void FinishPress(PressRipple? press)
    {
        if (press is null || press.FinishSeconds is not null) return;
        if (MaterialMotion.GetIsReduced(this)) _ripples.Remove(press);
        else press.FinishSeconds = _clock.Elapsed.TotalSeconds;
        InvalidateVisual();
        if (_ripples.Count > 0) RequestFrame();
    }

    private void FinishPresses()
    {
        if (_ripples.Count == 0)
            return;

        var now = _clock.Elapsed.TotalSeconds;
        var reduced = MaterialMotion.GetIsReduced(this);
        for (var i = _ripples.Count - 1; i >= 0; i--)
        {
            var ripple = _ripples[i];
            if (ripple.FinishSeconds is not null)
                continue;
            if (reduced)
                _ripples.RemoveAt(i);
            else
                ripple.FinishSeconds = now;
        }
        InvalidateVisual();
        if (_ripples.Count > 0 && !reduced)
            RequestFrame();
    }

    private void RequestFrame()
    {
        if (_ripples.Count == 0)
            return;
        if (TopLevel.GetTopLevel(this) is not { } topLevel)
        {
            _ripples.Clear();
            InvalidateVisual();
            return;
        }

        if (_framePending && ReferenceEquals(_frameRoot, topLevel)) return;
        if (!ReferenceEquals(_frameRoot, topLevel))
        {
            _frameRoot = topLevel;
            var generation = ++_frameGeneration;
            _onOwnedFrame = timestamp =>
            {
                if (_frameGeneration == generation) OnFrame(timestamp);
            };
        }
        _framePending = true;
        topLevel.RequestAnimationFrame(_onOwnedFrame!);
    }

    private void OnFrame(TimeSpan timestamp)
    {
        _framePending = false;
        if (_detached || _ripples.Count == 0)
            return;

        if (!IsEffectivelyVisible || !this.IsAttachedToVisualTree())
        {
            _ripples.Clear();
            InvalidateVisual();
            return;
        }

        if (MaterialMotion.GetIsReduced(this))
        {
            // Reduced mode has no timeline: a held press keeps the single static indication and
            // releasing drops it. Only already-finished ripples are discarded here.
            for (var i = _ripples.Count - 1; i >= 0; i--)
            {
                if (_ripples[i].FinishSeconds is not null)
                    _ripples.RemoveAt(i);
            }
            InvalidateVisual();
            return;
        }

        var now = _clock.Elapsed.TotalSeconds;
        for (var i = _ripples.Count - 1; i >= 0; i--)
        {
            var finish = _ripples[i].FinishSeconds;
            if (finish is double finishedAt && now >= FadeStart(_ripples[i], finishedAt) + FadeOutSeconds)
                _ripples.RemoveAt(i);
        }

        InvalidateVisual();
        if (_ripples.Count > 0)
            RequestFrame();
    }

    private static double FadeStart(PressRipple ripple, double finishSeconds)
        => Math.Max(finishSeconds, ripple.StartSeconds + RadiusSeconds);

    private Size _arrangedSize;

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        if (size != _arrangedSize)
        {
            _arrangedSize = size;
            InvalidateVisual();
        }
        return size;
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        // Source ripple color defaults to LocalContentColor. An explicit Fill wins; otherwise the
        // host's foreground is the content color in this adaptation (selection templates rely on
        // that, exactly like the Material checkbox/radio ripple).
        var fill = Fill ?? (_host as TemplatedControl)?.Foreground;
        var size = Bounds.Size;
        if (fill is null || !(size.Width > 0) || !(size.Height > 0))
            return;

        var bounded = IsBounded;
        var radii = ClampCornerRadius(CornerRadius, size);
        var center = new Point(size.Width / 2, size.Height / 2);
        var stateRadius = ComputeEndRadius(size);

        if (bounded)
        {
            using (context.PushClip(new RoundedRect(new Rect(size), radii)))
                DrawLayers(context, fill, size, center, stateRadius);
        }
        else
        {
            DrawLayers(context, fill, size, center, stateRadius);
        }
    }

    private void DrawLayers(DrawingContext context, IBrush fill, Size size, Point center, double stateRadius)
    {
        if (_stateAlpha > 0)
        {
            using (context.PushOpacity(_stateAlpha))
                context.DrawEllipse(fill, null, center, stateRadius, stateRadius);
        }

        if (_ripples.Count == 0)
            return;

        var now = _clock.Elapsed.TotalSeconds;
        var reduced = MaterialMotion.GetIsReduced(this);
        var pressAlpha = PressOpacity;
        foreach (var ripple in _ripples)
        {
            if (ripple.StartRadius is not double startRadius)
            {
                // RippleAnimation resolves these on its first draw.
                startRadius = Math.Max(size.Width, size.Height) * StartRadiusFraction;
                ripple.StartRadius = startRadius;
                ripple.EndRadius = ComputeEndRadius(size);
                ripple.TargetCenter = center;
            }

            var elapsed = Math.Max(0, now - ripple.StartSeconds);
            double alpha;
            if (reduced)
                alpha = 1;
            else if (ripple.FinishSeconds is double finish)
            {
                var fadeStart = FadeStart(ripple, finish);
                alpha = now < fadeStart ? 1 : 1 - Math.Clamp((now - fadeStart) / FadeOutSeconds, 0, 1);
            }
            else
            {
                alpha = Math.Clamp(elapsed / FadeInSeconds, 0, 1);
            }

            if (alpha <= 0)
                continue;

            var radiusProgress = reduced ? 1 : FastOutSlowIn(Math.Clamp(elapsed / RadiusSeconds, 0, 1));
            var centerProgress = reduced ? 1 : Math.Clamp(elapsed / RadiusSeconds, 0, 1);
            var endRadius = ripple.EndRadius is double end ? end : ComputeEndRadius(size);
            var radius = startRadius + (endRadius - startRadius) * radiusProgress;
            var origin = ripple.Origin ?? ripple.TargetCenter ?? center;
            var target = ripple.TargetCenter ?? center;
            var rippleCenter = new Point(
                origin.X + (target.X - origin.X) * centerProgress,
                origin.Y + (target.Y - origin.Y) * centerProgress);

            using (context.PushOpacity(pressAlpha * alpha))
                context.DrawEllipse(fill, null, rippleCenter, radius, radius);
        }
    }

    /// <summary>
    /// <c>getRippleEndRadius</c>: half the bounds diagonal, plus 10 DIP for bounded ripples.
    /// </summary>
    private double ComputeEndRadius(Size size)
    {
        var explicitRadius = Radius;
        if (double.IsFinite(explicitRadius) && explicitRadius > 0)
            return explicitRadius;

        var diagonal = Math.Sqrt(size.Width * size.Width + size.Height * size.Height) / 2;
        return IsBounded ? diagonal + BoundedExtraRadius : diagonal;
    }

    private static CornerRadius ClampCornerRadius(CornerRadius corners, Size size) =>
        // Resolved numerically: a bounded ripple renders every frame, and the shape-family
        // resolution would otherwise allocate four corner terms per frame.
        MaterialAssist.CornerShape.ClampToContainer(corners, Math.Min(size.Width, size.Height));

    private enum RippleInteraction { Hover, Focus, Drag }

    private sealed class PressRipple
    {
        public double StartSeconds { get; init; }

        public double? FinishSeconds { get; set; }

        public Point? Origin { get; init; }

        public double? StartRadius { get; set; }

        public double? EndRadius { get; set; }

        public Point? TargetCenter { get; set; }
    }
}