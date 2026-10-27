using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace ExpressiveAvalonia.Theming;

/// <summary>The two plain tooltip layouts exported by the source component.</summary>
public enum MaterialPlainToolTipType
{
    /// <summary>One line that hugs its text and truncates at the 200 DIP maximum width.</summary>
    SingleLine,
    /// <summary>A fixed 200 DIP frame whose text wraps onto as many lines as it needs.</summary>
    MultiLine,
}

/// <summary>Which edge of the tooltip surface carries the caret.</summary>
internal enum TooltipCaretSide
{
    /// <summary>The caret is on the top edge and points up at an anchor below it.</summary>
    Top,
    /// <summary>The caret is on the bottom edge and points down at an anchor above it.</summary>
    Bottom,
    /// <summary>The caret is on the left edge and points left at an anchor to its right.</summary>
    Left,
    /// <summary>The caret is on the right edge and points right at an anchor to its left.</summary>
    Right,
}

/// <summary>
/// Draws the optional 16 x 8 tooltip caret.
/// </summary>
/// <remarks>
/// Tooltip.kt draws the caret as an extension of the tooltip outline. This renderer reproduces the
/// same triangle next to the surface, using the real placed bounds of the surface and the real
/// anchor bounds. It therefore follows the placement Avalonia's popup chose (including a flip)
/// and re-centres the caret on the anchor when the popup is clamped against the window edge.
/// The caret lives in the margin the host reserves so the popup bounds contain it.
/// </remarks>
public sealed class TooltipCaretRenderer : Control
{
    /// <summary>The TooltipDefaults.caretSize width in DIP.</summary>
    public const double DefaultCaretWidth = 16;

    /// <summary>The TooltipDefaults.caretSize height in DIP.</summary>
    public const double DefaultCaretHeight = 8;

    /// <summary>Enables the caret.</summary>
    public static readonly StyledProperty<bool> ShowCaretProperty =
        AvaloniaProperty.Register<TooltipCaretRenderer, bool>(nameof(ShowCaret));

    /// <summary>The anchors the caret points at.</summary>
    public static readonly StyledProperty<Control?> AnchorProperty =
        AvaloniaProperty.Register<TooltipCaretRenderer, Control?>(nameof(Anchor));

    /// <summary>The surface whose edge carries the caret.</summary>
    public static readonly StyledProperty<Control?> SurfaceProperty =
        AvaloniaProperty.Register<TooltipCaretRenderer, Control?>(nameof(Surface));

    /// <summary>The brush shared with the tooltip surface.</summary>
    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<TooltipCaretRenderer, IBrush?>(nameof(Fill));

    /// <summary>
    /// The configured placement, used only to disambiguate a side placement from a vertical one;
    /// the caret still follows the actual bounds returned by the popup.
    /// </summary>
    public static readonly StyledProperty<PlacementMode> PlacementProperty =
        AvaloniaProperty.Register<TooltipCaretRenderer, PlacementMode>(nameof(Placement), PlacementMode.Pointer);

    private StreamGeometry? _geometry;
    private TooltipCaretSide _geometrySide;
    private TopLevel? _topLevel;

    static TooltipCaretRenderer()
    {
        AffectsRender<TooltipCaretRenderer>(ShowCaretProperty, AnchorProperty, SurfaceProperty, FillProperty, PlacementProperty);
    }

    public bool ShowCaret
    {
        get => GetValue(ShowCaretProperty);
        set => SetValue(ShowCaretProperty, value);
    }

    public Control? Anchor
    {
        get => GetValue(AnchorProperty);
        set => SetValue(AnchorProperty, value);
    }

    public Control? Surface
    {
        get => GetValue(SurfaceProperty);
        set => SetValue(SurfaceProperty, value);
    }

    public IBrush? Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public PlacementMode Placement
    {
        get => GetValue(PlacementProperty);
        set => SetValue(PlacementProperty, value);
    }

    /// <summary>
    /// Resolves the edge for the current surface and anchor bounds. Returns null until both are
    /// attached and laid out.
    /// </summary>
    internal TooltipCaretSide? ResolveSide()
    {
        var surface = Surface;
        var anchor = Anchor;
        if (surface is null || anchor is null ||
            !this.IsAttachedToVisualTree() || !surface.IsAttachedToVisualTree() ||
            !anchor.IsAttachedToVisualTree() || surface.Bounds.Width <= 0 || surface.Bounds.Height <= 0)
            return null;

        // The anchor lives in the window and this control lives in the native popup root, so
        // TranslatePoint cannot cross the two visual trees. PointToScreen is the common frame.
        var scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        var surfaceOrigin = surface.PointToScreen(default);
        var anchorOrigin = anchor.PointToScreen(default);
        var surfaceRect = new Rect(new Point(surfaceOrigin.X, surfaceOrigin.Y),
            new Size(surface.Bounds.Width * scale, surface.Bounds.Height * scale));
        var anchorRect = new Rect(new Point(anchorOrigin.X, anchorOrigin.Y),
            new Size(anchor.Bounds.Width * scale, anchor.Bounds.Height * scale));
        return ResolveSide(surfaceRect, anchorRect, Placement);
    }

    internal static TooltipCaretSide ResolveSide(Rect surface, Rect anchor, PlacementMode placement)
    {
        // Tooltip.kt uses the same test for the vertical case: the surface top below the anchor
        // top means the tooltip opened below the anchor. A horizontal placement is only taken
        // when the configured placement says so or when the surface really sits beside the anchor,
        // because a narrow tooltip below a wide anchor also has its left edge inside the anchor.
        var horizontal = placement is PlacementMode.Left or PlacementMode.Right ||
                         surface.Right <= anchor.Left || surface.Left >= anchor.Right;
        if (horizontal)
            return surface.Left >= anchor.Left ? TooltipCaretSide.Left : TooltipCaretSide.Right;
        return surface.Top > anchor.Top ? TooltipCaretSide.Top : TooltipCaretSide.Bottom;
    }

    internal static Thickness MarginFor(TooltipCaretSide side) => side switch
    {
        TooltipCaretSide.Top => new Thickness(0, DefaultCaretHeight, 0, 0),
        TooltipCaretSide.Bottom => new Thickness(0, 0, 0, DefaultCaretHeight),
        TooltipCaretSide.Left => new Thickness(DefaultCaretWidth, 0, 0, 0),
        _ => new Thickness(0, 0, DefaultCaretWidth, 0),
    };

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var surface = Surface;
        var anchor = Anchor;
        if (!ShowCaret || Fill is null || surface is null || anchor is null) return;

        var scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        var surfaceOrigin = surface.TranslatePoint(default, this);
        var surfaceScreenOrigin = surface.PointToScreen(default);
        var anchorScreenOrigin = anchor.PointToScreen(default);
        if (surfaceOrigin is null) return;

        var localSurface = new Rect(surfaceOrigin.Value, surface.Bounds.Size);
        var surfaceScreen = new Rect(
            new Point(surfaceScreenOrigin.X, surfaceScreenOrigin.Y),
            new Size(surface.Bounds.Width * scale, surface.Bounds.Height * scale));
        var anchorScreen = new Rect(
            new Point(anchorScreenOrigin.X, anchorScreenOrigin.Y),
            new Size(anchor.Bounds.Width * scale, anchor.Bounds.Height * scale));
        var side = ResolveSide(surfaceScreen, anchorScreen, Placement);

        double x, y;
        switch (side)
        {
            case TooltipCaretSide.Top:
                x = CaretX(localSurface, surfaceScreen, anchorScreen, scale) - DefaultCaretWidth / 2;
                y = localSurface.Top - DefaultCaretHeight;
                break;
            case TooltipCaretSide.Bottom:
                x = CaretX(localSurface, surfaceScreen, anchorScreen, scale) - DefaultCaretWidth / 2;
                y = localSurface.Bottom;
                break;
            case TooltipCaretSide.Left:
                x = localSurface.Left - DefaultCaretWidth;
                y = localSurface.Top + localSurface.Height / 2 - DefaultCaretHeight / 2;
                break;
            default:
                x = localSurface.Right;
                y = localSurface.Top + localSurface.Height / 2 - DefaultCaretHeight / 2;
                break;
        }

        var geometry = GetGeometry(side);
        using (context.PushTransform(Matrix.CreateTranslation(x, y)))
            context.DrawGeometry(Fill, null, geometry);
    }

    /// <summary>
    /// Tooltip.kt caretX: the caret is centred on the anchor, then held inside the actual surface
    /// when the popup was clamped against the window edge.
    /// </summary>
    private static double CaretX(Rect localSurface, Rect surfaceScreen, Rect anchorScreen, double scale)
    {
        var half = DefaultCaretWidth / 2;
        var min = localSurface.Left + half;
        var max = localSurface.Right - half;
        if (max < min) return localSurface.Left + localSurface.Width / 2;
        var center = (anchorScreen.Left + anchorScreen.Width / 2 - surfaceScreen.Left) / scale +
                     localSurface.Left;
        return Math.Clamp(center, min, max);
    }

    private StreamGeometry GetGeometry(TooltipCaretSide side)
    {
        if (_geometry is not null && _geometrySide == side) return _geometry;
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            var width = DefaultCaretWidth;
            var height = DefaultCaretHeight;
            switch (side)
            {
                case TooltipCaretSide.Top:
                    context.BeginFigure(new Point(0, height), true);
                    context.LineTo(new Point(width, height));
                    context.LineTo(new Point(width / 2, 0));
                    break;
                case TooltipCaretSide.Bottom:
                    context.BeginFigure(new Point(0, 0), true);
                    context.LineTo(new Point(width, 0));
                    context.LineTo(new Point(width / 2, height));
                    break;
                case TooltipCaretSide.Left:
                    context.BeginFigure(new Point(width, 0), true);
                    context.LineTo(new Point(width, height));
                    context.LineTo(new Point(0, height / 2));
                    break;
                default:
                    context.BeginFigure(new Point(0, 0), true);
                    context.LineTo(new Point(0, height));
                    context.LineTo(new Point(width, height / 2));
                    break;
            }
            context.EndFigure(true);
        }
        _geometry = geometry;
        _geometrySide = side;
        return geometry;
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _topLevel = TopLevel.GetTopLevel(this);
        if (_topLevel is not null) _topLevel.LayoutUpdated += OnTopLevelLayoutUpdated;
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_topLevel is not null) _topLevel.LayoutUpdated -= OnTopLevelLayoutUpdated;
        _topLevel = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnTopLevelLayoutUpdated(object? sender, EventArgs e) => InvalidateVisual();
}

/// <summary>
/// A native <see cref="ToolTip"/> that draws the optional Material caret. It is used when
/// <see cref="MaterialToolTip.ShowCaretProperty"/> is enabled on an owner, so ordinary string
/// tips and <see cref="MaterialToolTip.CreateContent"/> content gain the caret unchanged.
/// </summary>
public sealed class MaterialPlainToolTip : ToolTip
{
    /// <summary>Enables the 16 x 8 caret. The source default is no caret.</summary>
    public static readonly StyledProperty<bool> ShowCaretProperty =
        AvaloniaProperty.Register<MaterialPlainToolTip, bool>(nameof(ShowCaret));

    private Control? _surface;
    private TooltipCaretRenderer? _renderer;
    private Control? _anchor;

    public bool ShowCaret
    {
        get => GetValue(ShowCaretProperty);
        set => SetValue(ShowCaretProperty, value);
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _surface = e.NameScope.Find<Control>("PART_Surface");
        _renderer = e.NameScope.Find<TooltipCaretRenderer>("PART_Caret");
        if (_renderer is not null)
            _renderer.Surface = _surface;
        UpdateCaret();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        LayoutUpdated += OnLayoutUpdated;
        UpdateCaret();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        LayoutUpdated -= OnLayoutUpdated;
        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ShowCaretProperty) UpdateCaret();
    }

    private void OnLayoutUpdated(object? sender, EventArgs e) => UpdateCaret();

    private void UpdateCaret()
    {
        if (_renderer is null || _surface is null) return;
        // The ToolTipService makes this control a logical child of the Popup, whose
        // PlacementTarget is the adorned owner; no private Avalonia state is needed.
        var anchor = ResolveAnchor();
        if (!ReferenceEquals(anchor, _anchor))
        {
            _anchor = anchor;
            _renderer.Anchor = anchor;
        }
        var placement = anchor is null ? PlacementMode.Bottom : ToolTip.GetPlacement(anchor);
        _renderer.Placement = placement;
        _renderer.ShowCaret = ShowCaret;
        _renderer.Fill = Background;
        var side = _renderer.ResolveSide() ?? GuessSide(placement);
        var margin = ShowCaret ? TooltipCaretRenderer.MarginFor(side) : default;
        if (_surface.Margin != margin) _surface.Margin = margin;
        _renderer.InvalidateVisual();
    }

    private Control? ResolveAnchor()
    {
        for (var parent = Parent; parent is not null; parent = parent.Parent)
            if (parent is Popup popup)
                return popup.PlacementTarget;
        return null;
    }

    private static TooltipCaretSide GuessSide(PlacementMode placement) => placement switch
    {
        PlacementMode.Top => TooltipCaretSide.Bottom,
        PlacementMode.Left => TooltipCaretSide.Right,
        PlacementMode.Right => TooltipCaretSide.Left,
        _ => TooltipCaretSide.Top,
    };
}

/// <summary>
/// Adds the Material lifecycle around Avalonia's native plain <see cref="ToolTip"/>: keyboard
/// focus activation, touch long press, anchor placement, and an accessibility help text.
/// </summary>
/// <remarks>
/// Hover timing, pointer placement and light dismissal stay with Avalonia's
/// <c>ToolTipService</c>; this class never duplicates that timer. When enabled, a tooltip opens
/// when the owner receives focus through the keyboard or a screen reader, closes on Escape,
/// focus loss, owner disablement/hiding, window deactivation and visual-tree detachment.
/// <para>
/// Plain tooltip visuals (single line and multi line) are the <c>ToolTip</c> theme in
/// <c>Themes/Menus.axaml</c>: inverse surface, 4 px radius, 8/4 padding, 24 px minimum height,
/// body/small type and a 200 px wrap width.
/// </para>
/// </remarks>
public sealed class MaterialToolTip : AvaloniaObject
{
    /// <summary>Enables the keyboard, touch and accessibility lifecycle for the owner's tooltip.</summary>
    public static readonly AttachedProperty<bool> KeyboardEnabledProperty =
        AvaloniaProperty.RegisterAttached<MaterialToolTip, Control, bool>("KeyboardEnabled");

    /// <summary>
    /// Places the tooltip below the owner at a 4 px offset when the application has not chosen
    /// a placement itself. The pointer placement Avalonia uses by default is wrong for tooltips
    /// opened from the keyboard, where no pointer is near the focused control.
    /// </summary>
    public static readonly AttachedProperty<bool> AutoPlacementProperty =
        AvaloniaProperty.RegisterAttached<MaterialToolTip, Control, bool>("AutoPlacement", true);

    /// <summary>
    /// Opens the tooltip after a 500 ms touch press, the Material long-press gesture. Requires
    /// <see cref="KeyboardEnabledProperty"/>, which MaterialTheme already sets on interactive controls.
    /// </summary>
    public static readonly AttachedProperty<bool> LongPressEnabledProperty =
        AvaloniaProperty.RegisterAttached<MaterialToolTip, Control, bool>("LongPressEnabled");

    /// <summary>
    /// Mirrors a string tooltip into <see cref="AutomationProperties.HelpTextProperty"/> so screen
    /// readers can read it. Avalonia's tooltip popup exposes no automation peer of its own.
    /// An explicitly authored help text is never overwritten.
    /// </summary>
    public static readonly AttachedProperty<bool> ExposeHelpTextProperty =
        AvaloniaProperty.RegisterAttached<MaterialToolTip, Control, bool>("ExposeHelpText", true);

    /// <summary>
    /// Adds the source PlainTooltip caret: a 16 x 8 triangle on the edge that faces the owner.
    /// The source default is no caret, so this property defaults to <see langword="false"/> and
    /// an unset tooltip stays a plain native <see cref="ToolTip"/> with no anatomy change.
    /// </summary>
    public static readonly AttachedProperty<bool> ShowCaretProperty =
        AvaloniaProperty.RegisterAttached<MaterialToolTip, Control, bool>("ShowCaret");

    private const int LongPressMilliseconds = 500;
    // Tooltip.kt TooltipDuration: a non-persistent tooltip dismisses itself after 1.5 s. Hover
    // timing stays with Avalonia's ToolTipService (desktop policy); the source duration applies to
    // the keyboard and long-press activations this behavior owns.
    private const int TooltipDurationMilliseconds = 1500;
    private const double LongPressSlop = 8;
    private static readonly ConditionalWeakTable<Control, Behavior> Behaviors = new();

    static MaterialToolTip()
    {
        KeyboardEnabledProperty.Changed.AddClassHandler<Control>((control, args) =>
        {
            if (args.NewValue is true)
                Behaviors.GetValue(control, static owner => new Behavior(owner)).Attach();
            else if (Behaviors.TryGetValue(control, out var behavior))
                behavior.Detach();
        });
        ExposeHelpTextProperty.Changed.AddClassHandler<Control>((control, _) =>
        {
            if (Behaviors.TryGetValue(control, out var behavior)) behavior.SyncHelpText();
        });
        LongPressEnabledProperty.Changed.AddClassHandler<Control>((control, _) =>
        {
            if (Behaviors.TryGetValue(control, out var behavior)) behavior.UpdatePointerHandlers();
        });
        AutoPlacementProperty.Changed.AddClassHandler<Control>((control, _) =>
        {
            if (Behaviors.TryGetValue(control, out var behavior)) behavior.UpdatePlacement();
        });
        ShowCaretProperty.Changed.AddClassHandler<Control>((control, _) =>
        {
            if (Behaviors.TryGetValue(control, out var behavior)) behavior.UpdateCaret();
        });
    }

    public static bool GetKeyboardEnabled(Control control) => control.GetValue(KeyboardEnabledProperty);
    public static void SetKeyboardEnabled(Control control, bool enabled) => control.SetValue(KeyboardEnabledProperty, enabled);
    public static bool GetAutoPlacement(Control control) => control.GetValue(AutoPlacementProperty);
    public static void SetAutoPlacement(Control control, bool enabled) => control.SetValue(AutoPlacementProperty, enabled);
    public static bool GetLongPressEnabled(Control control) => control.GetValue(LongPressEnabledProperty);
    public static void SetLongPressEnabled(Control control, bool enabled) => control.SetValue(LongPressEnabledProperty, enabled);
    public static bool GetExposeHelpText(Control control) => control.GetValue(ExposeHelpTextProperty);
    public static void SetExposeHelpText(Control control, bool enabled) => control.SetValue(ExposeHelpTextProperty, enabled);
    public static bool GetShowCaret(Control control) => control.GetValue(ShowCaretProperty);
    public static void SetShowCaret(Control control, bool show) => control.SetValue(ShowCaretProperty, show);

    /// <summary>
    /// Builds tooltip content that reproduces one of the two source plain tooltip variants.
    /// </summary>
    /// <remarks>
    /// A plain <see cref="string"/> tip is already correct for the single-line variant: the
    /// <c>ToolTip</c> theme hugs short text and wraps long text at the 200 DIP maximum width.
    /// Use this factory when the fixed 200 DIP frame matters, for example in a layout comparison,
    /// or to force single-line truncation for a long string. The returned block is 184 DIP wide
    /// because the 200 DIP source frame includes the theme's 8 DIP horizontal padding.
    /// </remarks>
    public static Control CreateContent(string text, MaterialPlainToolTipType type = MaterialPlainToolTipType.SingleLine)
    {
        var block = new TextBlock
        {
            Text = text,
            // The 200 DIP source frame includes the 8 DIP horizontal padding on both sides.
            MaxWidth = 184,
            TextWrapping = type == MaterialPlainToolTipType.MultiLine ? TextWrapping.Wrap : TextWrapping.NoWrap,
            TextTrimming = type == MaterialPlainToolTipType.SingleLine ? TextTrimming.CharacterEllipsis : TextTrimming.None
        };
        if (type == MaterialPlainToolTipType.MultiLine)
            block.Width = 184;
        // Body/small already binds font size, weight, letter spacing and line height.
        MaterialTypography.SetStyle(block, MaterialTypeStyle.BodySmall);
        return block;
    }

    /// <summary>Per-owner state; the table keeps it alive exactly as long as the owner.</summary>
    private sealed class Behavior
    {
        private readonly Control _control;
        private DispatcherTimer? _longPressTimer;
        private DispatcherTimer? _autoHideTimer;
        private Window? _window;
        private Point _pressOrigin;
        private bool _pressed;
        private bool _openedByLongPress;
        private bool _attached;
        private bool _helpTextOwned;
        private string? _helpTextWritten;
        private bool _placementApplied;
        private bool _offsetApplied;
        private bool _pointerAttached;
        private bool _syncingTip;
        private bool _tipWrapped;
        private object? _originalTip;
        private MaterialPlainToolTip? _caretHost;

        public Behavior(Control control) => _control = control;

        public void Attach()
        {
            if (_attached) return;
            _attached = true;
            _control.GotFocus += GotFocus;
            _control.LostFocus += LostFocus;
            _control.AddHandler(InputElement.KeyDownEvent, KeyDown, RoutingStrategies.Tunnel);
            _control.AttachedToVisualTree += AttachedToTree;
            _control.DetachedFromVisualTree += DetachedFromTree;
            _control.PropertyChanged += ControlPropertyChanged;
            UpdatePointerHandlers();
            UpdateCaret();
            ApplyPlacement();
            SyncHelpText();
            if (_control.IsAttachedToVisualTree()) HookWindow();
        }

        public void Detach()
        {
            if (!_attached) return;
            _attached = false;
            _control.GotFocus -= GotFocus;
            _control.LostFocus -= LostFocus;
            _control.RemoveHandler(InputElement.KeyDownEvent, KeyDown);
            UpdatePointerHandlers();
            _control.AttachedToVisualTree -= AttachedToTree;
            _control.DetachedFromVisualTree -= DetachedFromTree;
            _control.PropertyChanged -= ControlPropertyChanged;
            UnhookWindow();
            UpdateCaret();
            CancelLongPress();
            _pressed = false;
            _openedByLongPress = false;
            HideTip();
            RestorePlacement();
            ReleaseHelpText();
        }

        /// <summary>
        /// Pointer tracking is only needed for the opt-in touch long press; attaching it to every
        /// themed control would put a pointer-move handler on each one.
        /// </summary>
        public void UpdatePointerHandlers()
        {
            var wanted = _attached && GetLongPressEnabled(_control);
            if (wanted == _pointerAttached) return;
            _pointerAttached = wanted;
            if (wanted)
            {
                _control.AddHandler(InputElement.PointerPressedEvent, PointerPressed, RoutingStrategies.Tunnel);
                _control.AddHandler(InputElement.PointerReleasedEvent, PointerReleased, RoutingStrategies.Tunnel);
                _control.AddHandler(InputElement.PointerMovedEvent, PointerMoved, RoutingStrategies.Tunnel);
                // PointerCaptureLost is a direct event, so it is subscribed through its CLR event.
                _control.PointerCaptureLost += PointerCaptureLost;
            }
            else
            {
                _control.RemoveHandler(InputElement.PointerPressedEvent, PointerPressed);
                _control.RemoveHandler(InputElement.PointerReleasedEvent, PointerReleased);
                _control.RemoveHandler(InputElement.PointerMovedEvent, PointerMoved);
                _control.PointerCaptureLost -= PointerCaptureLost;
                CancelLongPress();
                _pressed = false;
                if (_openedByLongPress)
                {
                    _openedByLongPress = false;
                    HideTip();
                }
            }
        }

        private void GotFocus(object? sender, FocusChangedEventArgs args)
        {
            // Pointer focus keeps Avalonia's hover timing; only keyboard and programmatic focus
            // show the tooltip immediately. A focus move inside the owner must not re-open it.
            if (args.Source != _control || args.NavigationMethod == NavigationMethod.Pointer) return;
            ShowTip();
            StartAutoHide();
        }

        private void LostFocus(object? sender, FocusChangedEventArgs args)
        {
            if (!_control.IsKeyboardFocusWithin) HideTip();
        }

        private void KeyDown(object? sender, KeyEventArgs args)
        {
            if (args.Key != Key.Escape || !ToolTip.GetIsOpen(_control)) return;
            HideTip();
            args.Handled = true;
        }

        private void PointerPressed(object? sender, PointerPressedEventArgs args)
        {
            if (args.Pointer.Type != PointerType.Touch) return;
            if (!GetLongPressEnabled(_control) || ToolTip.GetTip(_control) is null) return;
            _pressed = true;
            _pressOrigin = args.GetPosition(_control);
            CancelLongPress();
            _longPressTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(LongPressMilliseconds),
                DispatcherPriority.Input, OnLongPress);
            _longPressTimer.Start();
        }

        private void OnLongPress(object? sender, EventArgs args)
        {
            CancelLongPress();
            if (!_pressed || !_control.IsEffectivelyEnabled || !_control.IsEffectivelyVisible) return;
            if (ToolTip.GetTip(_control) is null) return;
            _openedByLongPress = true;
            ShowTip();
            StartAutoHide();
        }

        private void PointerReleased(object? sender, PointerReleasedEventArgs args)
        {
            _pressed = false;
            CancelLongPress();
            // Material long-press tooltips are dismissed when the finger lifts; keyboard-opened
            // tooltips stay until Escape, focus loss or another pointer action.
            if (_openedByLongPress)
            {
                _openedByLongPress = false;
                HideTip();
            }
        }

        private void PointerMoved(object? sender, PointerEventArgs args)
        {
            if (!_pressed) return;
            var delta = args.GetPosition(_control) - _pressOrigin;
            if (Math.Abs(delta.X) > LongPressSlop || Math.Abs(delta.Y) > LongPressSlop)
            {
                _pressed = false;
                CancelLongPress();
            }
        }

        private void PointerCaptureLost(object? sender, PointerCaptureLostEventArgs args)
        {
            _pressed = false;
            CancelLongPress();
            if (_openedByLongPress)
            {
                _openedByLongPress = false;
                HideTip();
            }
        }

        private void ControlPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs change)
        {
            if (change.Property == ToolTip.TipProperty)
            {
                if (_syncingTip) return;
                if (_tipWrapped && !ReferenceEquals(ToolTip.GetTip(_control), _caretHost))
                {
                    // Another author replaced the tip; the old wrapper no longer owns it.
                    _tipWrapped = false;
                    _originalTip = null;
                    _caretHost = null;
                }
                UpdateCaret();
                SyncHelpText();
            }
            else if (change.Property == InputElement.IsEffectivelyEnabledProperty && !_control.IsEffectivelyEnabled ||
                     change.Property == Visual.IsVisibleProperty && !_control.IsVisible)
            {
                CancelLongPress();
                _pressed = false;
                HideTip();
            }
        }

        private void AttachedToTree(object? sender, VisualTreeAttachmentEventArgs args) => HookWindow();

        private void DetachedFromTree(object? sender, VisualTreeAttachmentEventArgs args)
        {
            UnhookWindow();
            CancelLongPress();
            _pressed = false;
            HideTip();
        }

        private void HookWindow()
        {
            var window = TopLevel.GetTopLevel(_control) as Window;
            if (ReferenceEquals(window, _window)) return;
            UnhookWindow();
            _window = window;
            if (_window is not null) _window.Deactivated += WindowDeactivated;
        }

        private void UnhookWindow()
        {
            if (_window is null) return;
            _window.Deactivated -= WindowDeactivated;
            _window = null;
        }

        private void WindowDeactivated(object? sender, EventArgs args)
        {
            if (ToolTip.GetIsOpen(_control)) HideTip();
        }

        private void ShowTip()
        {
            if (!_control.IsEffectivelyEnabled || !_control.IsEffectivelyVisible) return;
            if (ToolTip.GetTip(_control) is null) return;
            ToolTip.SetIsOpen(_control, true);
        }

        private void StartAutoHide()
        {
            CancelAutoHide();
            _autoHideTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(TooltipDurationMilliseconds),
                DispatcherPriority.Input, (_, _) =>
                {
                    CancelAutoHide();
                    if (_openedByLongPress)
                        _openedByLongPress = false;
                    HideTip();
                });
            _autoHideTimer.Start();
        }

        private void CancelAutoHide()
        {
            _autoHideTimer?.Stop();
            _autoHideTimer = null;
        }

        private void HideTip()
        {
            CancelAutoHide();
            ToolTip.SetIsOpen(_control, false);
        }

        private void CancelLongPress()
        {
            _longPressTimer?.Stop();
            _longPressTimer = null;
        }

        public void UpdatePlacement()
        {
            if (!_attached) return;
            if (GetAutoPlacement(_control)) ApplyPlacement();
            else RestorePlacement();
        }

        /// <summary>
        /// Swaps the native tip for a <see cref="MaterialPlainToolTip"/> only while the caret is
        /// requested. The original tip object is restored exactly when the caret is turned off or
        /// the behavior detaches, so an ordinary tooltip keeps Avalonia's own ToolTip instance and
        /// no private Avalonia state is involved.
        /// </summary>
        public void UpdateCaret()
        {
            if (!_attached)
            {
                UnwrapTip();
                return;
            }

            var tip = ToolTip.GetTip(_control);
            if (_tipWrapped && !ReferenceEquals(tip, _caretHost))
            {
                _tipWrapped = false;
                _originalTip = null;
                _caretHost = null;
            }

            if (!GetShowCaret(_control))
            {
                if (tip is MaterialPlainToolTip host) host.ShowCaret = false;
                UnwrapTip();
                ApplyPlacement();
                return;
            }

            if (tip is MaterialPlainToolTip existing)
            {
                existing.ShowCaret = true;
            }
            else if (tip is null or ToolTip)
            {
                // A null tip has nothing to show; a consumer-authored ToolTip owns its own surface.
            }
            else if (_tipWrapped)
            {
                if (_caretHost is not null) _caretHost.ShowCaret = true;
            }
            else
            {
                _originalTip = tip;
                _caretHost = new MaterialPlainToolTip { Content = tip, ShowCaret = true };
                _tipWrapped = true;
                SetTipSilently(_caretHost);
            }
            ApplyPlacement();
        }

        private void UnwrapTip()
        {
            if (!_tipWrapped) return;
            var original = _originalTip;
            var host = _caretHost;
            _tipWrapped = false;
            _originalTip = null;
            _caretHost = null;
            SetTipSilently(original);
            if (host is not null) host.ShowCaret = false;
        }

        private void SetTipSilently(object? tip)
        {
            _syncingTip = true;
            try
            {
                ToolTip.SetTip(_control, tip);
            }
            finally
            {
                _syncingTip = false;
            }
        }

        private void ApplyPlacement()
        {
            if (!GetAutoPlacement(_control) || ToolTip.GetTip(_control) is null) return;
            // Only take ownership of a value the application has not authored itself, so a
            // consumer style or local value always wins.
            if (!_placementApplied && !_control.IsSet(ToolTip.PlacementProperty))
            {
                _control.SetValue(ToolTip.PlacementProperty, PlacementMode.Bottom);
                _placementApplied = true;
            }
            if (_offsetApplied || !_control.IsSet(ToolTip.VerticalOffsetProperty))
            {
                // Tooltip.kt places the surface 4 DIP from the anchor; a caret occupies 8 DIP of
                // the popup bounds, so the popup origin moves back by the caret height. This keeps
                // the surface at the same 4 DIP while the caret reaches toward the anchor.
                var caretHeight = ToolTip.GetTip(_control) is MaterialPlainToolTip { ShowCaret: true }
                    ? TooltipCaretRenderer.DefaultCaretHeight
                    : 0d;
                _control.SetValue(ToolTip.VerticalOffsetProperty, 4d - caretHeight);
                _offsetApplied = true;
            }
        }

        private void RestorePlacement()
        {
            if (_placementApplied)
            {
                _placementApplied = false;
                _control.ClearValue(ToolTip.PlacementProperty);
            }
            if (_offsetApplied)
            {
                _offsetApplied = false;
                _control.ClearValue(ToolTip.VerticalOffsetProperty);
            }
        }

        public void SyncHelpText()
        {
            if (!_attached)
            {
                ReleaseHelpText();
                return;
            }

            var text = GetTipText(ToolTip.GetTip(_control));
            if (!GetExposeHelpText(_control) || text is null)
            {
                ReleaseHelpText();
                return;
            }

            var current = AutomationProperties.GetHelpText(_control);
            if (_helpTextOwned)
            {
                if (!string.Equals(current, _helpTextWritten, StringComparison.Ordinal))
                {
                    // Another author replaced the value; never fight it.
                    _helpTextOwned = false;
                    _helpTextWritten = null;
                }
                else
                {
                    AutomationProperties.SetHelpText(_control, text);
                    _helpTextWritten = text;
                    return;
                }
            }

            if (_control.IsSet(AutomationProperties.HelpTextProperty) || !string.IsNullOrEmpty(current))
            {
                // Authored or bound help text wins over the tooltip mirror.
                return;
            }

            AutomationProperties.SetHelpText(_control, text);
            _helpTextOwned = true;
            _helpTextWritten = text;
        }

        private void ReleaseHelpText()
        {
            if (!_helpTextOwned)
            {
                _helpTextWritten = null;
                return;
            }
            if (string.Equals(AutomationProperties.GetHelpText(_control), _helpTextWritten, StringComparison.Ordinal))
                AutomationProperties.SetHelpText(_control, null);
            _helpTextOwned = false;
            _helpTextWritten = null;
        }

        private static string? GetTipText(object? tip) => tip switch
        {
            string text when !string.IsNullOrWhiteSpace(text) => text,
            TextBlock block when !string.IsNullOrWhiteSpace(block.Text) => block.Text,
            MaterialPlainToolTip host => GetTipText(host.Content),
            _ => null
        };
    }
}
