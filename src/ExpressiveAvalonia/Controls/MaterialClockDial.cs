using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using ExpressiveAvalonia.Theming;
using ExpressiveAvalonia.Tokens;
using Avalonia.VisualTree;

namespace ExpressiveAvalonia.Controls;

/// <summary>The clock face a <see cref="MaterialClockDial"/> presents.</summary>
public enum MaterialClockDialSelection
{
    /// <summary>Hours: twelve positions for the 12-hour face, twelve outer plus twelve inner for the 24-hour face.</summary>
    Hour,

    /// <summary>Minutes: sixty positions on one ring, labelled every five minutes.</summary>
    Minute,
}

/// <summary>
/// Describes a completed pointer selection on a <see cref="MaterialClockDial"/>.
/// </summary>
/// <remarks>
/// Source <c>ClockDialModifier</c> handles taps and drags in separate pointer detectors: a tap runs
/// <c>rotateTo(angle, DefaultSpatial, animate = true)</c> and the picker waits for that rotation
/// before its 100 ms hour-to-minute step, while a drag switches the face on <c>onDragEnd</c>
/// immediately. <see cref="IsTap"/> keeps that distinction for the picker.
/// </remarks>
public sealed class MaterialClockDialSelectionEventArgs(bool isTap) : EventArgs
{
    /// <summary>True for a tap gesture, false when the pointer moved as a drag.</summary>
    public bool IsTap { get; } = isTap;
}

/// <summary>
/// The Material clock dial used by <see cref="MaterialTimePicker"/>: a round face with selectable
/// numbers, a radial hour line and a centre dot.
/// </summary>
/// <remarks>
/// <para>
/// Anatomy follows the source building blocks: a 256 x 256 face, 48 x 48 number cells centred at
/// 0.789 of the radius (101 DIP, outer ring) and 0.539 (69 DIP, 24-hour inner ring), with a
/// 0.578 (74 DIP) radius separating the two 24-hour rings, a 2-DIP hour line from the
/// centre to the selected number, and an 8-DIP centre dot.
/// </para>
/// <para>
/// Pointer capture drives free dragging; the nearest cell is highlighted. The dial is focusable and
/// answers to the arrow keys, Page Up/Down, Home/End. The selected indicator is a spring whose
/// travel is skipped when <see cref="MaterialMotion.IsReduced"/> is set for the control.
/// </para>
/// </remarks>
public sealed class MaterialClockDial : Control
{
    /// <summary>Radius of one number cell, in DIPs. The cell is the 48 x 48 touch target.</summary>
    public const double CellRadius = 24;

    /// <summary>
    /// Distance of the outer number ring from the centre, as a fraction of the face radius.
    /// Source: <c>OuterCircleToSizeRatio = 101.dp / ClockDialContainerSize (256.dp)</c>, i.e. a
    /// 101-DIP ring on a 256-DIP face.
    /// </summary>
    public const double OuterRingFactor = 101d / 128d;

    /// <summary>
    /// Distance of the 24-hour inner number ring from the centre, as a fraction of the radius.
    /// Source: <c>InnerCircleToSizeRatio = 69.dp / ClockDialContainerSize (256.dp)</c>.
    /// </summary>
    public const double InnerRingFactor = 69d / 128d;

    /// <summary>
    /// Radius that separates the 24-hour outer and inner rings.
    /// Source: <c>MaxDistance = 74.dp</c> measured from the centre of the 256-DIP face.
    /// </summary>
    public const double RingBoundaryFactor = 74d / 128d;

    /// <summary>Radius of the centre dot, in DIPs (the 8 x 8 centre ellipse).</summary>
    public const double CenterDotRadius = 4;

    public static readonly StyledProperty<int> ValueProperty =
        AvaloniaProperty.Register<MaterialClockDial, int>(nameof(Value), 0,
            defaultBindingMode: BindingMode.TwoWay,
            coerce: (o, value) => ((MaterialClockDial)o).CoerceDialValue(value));

    public static readonly StyledProperty<MaterialClockDialSelection> SelectionProperty =
        AvaloniaProperty.Register<MaterialClockDial, MaterialClockDialSelection>(nameof(Selection));

    public static readonly StyledProperty<bool> Is24HourProperty =
        AvaloniaProperty.Register<MaterialClockDial, bool>(nameof(Is24Hour));

    public static readonly StyledProperty<int> MinuteIncrementProperty =
        AvaloniaProperty.Register<MaterialClockDial, int>(nameof(MinuteIncrement), 1,
            validate: value => value is > 0 and <= 60);

    /// <summary>
    /// Culture used for the face numbers. Null follows <see cref="CultureInfo.CurrentCulture"/>,
    /// matching the source <c>toLocalString</c> default. Changing it invalidates the cached face.
    /// </summary>
    public static readonly StyledProperty<CultureInfo?> CultureProperty =
        AvaloniaProperty.Register<MaterialClockDial, CultureInfo?>(nameof(Culture));

    public static readonly StyledProperty<double> NumberFontSizeProperty =
        AvaloniaProperty.Register<MaterialClockDial, double>(nameof(NumberFontSize), 16,
            validate: value => double.IsFinite(value) && value > 0);

    public static readonly StyledProperty<FontWeight> NumberFontWeightProperty =
        AvaloniaProperty.Register<MaterialClockDial, FontWeight>(nameof(NumberFontWeight), FontWeight.Normal);

    public static readonly StyledProperty<IBrush?> FaceBrushProperty =
        AvaloniaProperty.Register<MaterialClockDial, IBrush?>(nameof(FaceBrush));

    public static readonly StyledProperty<IBrush?> NumberBrushProperty =
        AvaloniaProperty.Register<MaterialClockDial, IBrush?>(nameof(NumberBrush));

    public static readonly StyledProperty<IBrush?> SelectedBrushProperty =
        AvaloniaProperty.Register<MaterialClockDial, IBrush?>(nameof(SelectedBrush));

    public static readonly StyledProperty<IBrush?> SelectedTextBrushProperty =
        AvaloniaProperty.Register<MaterialClockDial, IBrush?>(nameof(SelectedTextBrush));

    public static readonly StyledProperty<IBrush?> LineBrushProperty =
        AvaloniaProperty.Register<MaterialClockDial, IBrush?>(nameof(LineBrush));

    public static readonly StyledProperty<IBrush?> StateLayerBrushProperty =
        AvaloniaProperty.Register<MaterialClockDial, IBrush?>(nameof(StateLayerBrush));

    /// <summary>
    /// Opacity applied to <see cref="StateLayerBrush"/>. The source state layer is the OnSurface
    /// color at the standard 8% hover / 10% focus opacities.
    /// </summary>
    public static readonly StyledProperty<double> StateLayerOpacityProperty =
        AvaloniaProperty.Register<MaterialClockDial, double>(nameof(StateLayerOpacity), 0.08,
            validate: value => double.IsFinite(value) && value is >= 0 and <= 1);

    public static readonly StyledProperty<IBrush?> FocusRingBrushProperty =
        AvaloniaProperty.Register<MaterialClockDial, IBrush?>(nameof(FocusRingBrush));

    public static readonly StyledProperty<IBrush?> DisabledNumberBrushProperty =
        AvaloniaProperty.Register<MaterialClockDial, IBrush?>(nameof(DisabledNumberBrush));

    // The attached typography property inherits from the surrounding window, like every other
    // text-bearing Material control.
    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        TextElement.FontFamilyProperty.AddOwner<MaterialClockDial>();

    private const double LineThickness = 2;
    private const double FocusRingThickness = 2;
    private const double FocusRingGap = 2;

    private readonly Pen _linePen = new() { LineCap = PenLineCap.Round };
    private readonly Pen _focusPen = new() { LineCap = PenLineCap.Round };
    // Number cells and their formatted text are stable between frames; pointer drags re-render
    // every frame, so both are cached instead of rebuilt per pass.
    private readonly Dictionary<TextKey, FormattedText> _texts = [];
    private NumberCell[]? _cells;
    private MaterialMotion.SpringAnimation? _faceAnimation, _outgoingFaceAnimation;
    private NumberCell[]? _outgoingCells;
    private CellKey _outgoingCellKey;
    private double _faceOpacity = 1, _outgoingFaceOpacity;
    private CellKey _cellKey;
    private MaterialMotion.SpringAnimation? _selectionAnimation;
    private double _displayAngle = double.NaN;
    private int? _hoveredValue;
    private IPointer? _capturedPointer;
    private Point _pressPosition;
    private bool _dragMoved;
    /// <summary>
    /// Raised after a pointer interaction selects a value on the dial, before keyboard input.
    /// The time picker uses this to advance from hour to minute like the source dial.
    /// </summary>
    public event EventHandler<MaterialClockDialSelectionEventArgs>? PointerSelectionCompleted;

    private bool _isDragging;
    private int _gestureVersion;

    internal async Task<bool> WaitForSelectionAsync()
    {
        var version = _gestureVersion;
        while (_selectionAnimation is { IsRunning: true })
        {
            await Task.Delay(16);
            if (version != _gestureVersion || !this.IsAttachedToVisualTree()) return false;
        }
        await Task.Delay(100);
        return version == _gestureVersion && this.IsAttachedToVisualTree();
    }

    static MaterialClockDial()
    {
        AffectsRender<MaterialClockDial>(ValueProperty, SelectionProperty, Is24HourProperty, MinuteIncrementProperty,
            CultureProperty, NumberFontSizeProperty, NumberFontWeightProperty, FaceBrushProperty, NumberBrushProperty,
            SelectedBrushProperty, SelectedTextBrushProperty, LineBrushProperty, StateLayerBrushProperty,
            StateLayerOpacityProperty, FocusRingBrushProperty, DisabledNumberBrushProperty, FontFamilyProperty);
        FocusableProperty.OverrideDefaultValue<MaterialClockDial>(true);
    }

    /// <summary>The selected hour (0-23) or minute (0-59).</summary>
    public int Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>Whether the face selects hours or minutes.</summary>
    public MaterialClockDialSelection Selection
    {
        get => GetValue(SelectionProperty);
        set => SetValue(SelectionProperty, value);
    }

    /// <summary>Whether the hour face shows 1-12 outside and 13-24 inside.</summary>
    public bool Is24Hour
    {
        get => GetValue(Is24HourProperty);
        set => SetValue(Is24HourProperty, value);
    }

    /// <summary>The minute grid the pointer and arrow keys snap to. One means every minute.</summary>
    public int MinuteIncrement
    {
        get => GetValue(MinuteIncrementProperty);
        set => SetValue(MinuteIncrementProperty, value);
    }

    /// <summary>Culture used for face numbers; null follows the current culture.</summary>
    public CultureInfo? Culture
    {
        get => GetValue(CultureProperty);
        set => SetValue(CultureProperty, value);
    }

    private CultureInfo EffectiveCulture => Culture ?? CultureInfo.CurrentCulture;

    public double NumberFontSize
    {
        get => GetValue(NumberFontSizeProperty);
        set => SetValue(NumberFontSizeProperty, value);
    }

    public FontWeight NumberFontWeight
    {
        get => GetValue(NumberFontWeightProperty);
        set => SetValue(NumberFontWeightProperty, value);
    }

    public IBrush? FaceBrush { get => GetValue(FaceBrushProperty); set => SetValue(FaceBrushProperty, value); }
    public IBrush? NumberBrush { get => GetValue(NumberBrushProperty); set => SetValue(NumberBrushProperty, value); }
    public IBrush? SelectedBrush { get => GetValue(SelectedBrushProperty); set => SetValue(SelectedBrushProperty, value); }
    public IBrush? SelectedTextBrush { get => GetValue(SelectedTextBrushProperty); set => SetValue(SelectedTextBrushProperty, value); }
    public IBrush? LineBrush { get => GetValue(LineBrushProperty); set => SetValue(LineBrushProperty, value); }
    public IBrush? StateLayerBrush { get => GetValue(StateLayerBrushProperty); set => SetValue(StateLayerBrushProperty, value); }

    public double StateLayerOpacity
    {
        get => GetValue(StateLayerOpacityProperty);
        set => SetValue(StateLayerOpacityProperty, value);
    }
    public IBrush? FocusRingBrush { get => GetValue(FocusRingBrushProperty); set => SetValue(FocusRingBrushProperty, value); }
    public IBrush? DisabledNumberBrush { get => GetValue(DisabledNumberBrushProperty); set => SetValue(DisabledNumberBrushProperty, value); }

    public FontFamily FontFamily
    {
        get => GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    /// <summary>Raised when pointer or keyboard input selects a different value.</summary>
    public event EventHandler? ValueChanged;

    /// <summary>
    /// The clockwise angle of a dial position measured from 12 o'clock, in radians.
    /// The twelve-hour face and the minute face use twelve positions; the 24-hour face uses
    /// the same twelve angles twice, on the outer and the inner ring.
    /// </summary>
    public static double GetPositionAngle(int index, int count)
    {
        if (count <= 0) return 0;
        var normalized = ((index % count) + count) % count;
        return normalized * Math.Tau / count;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // The face is square: the smaller of the two axes wins so the ring never clips.
        var width = double.IsNaN(Width) ? 256 : Width;
        var height = double.IsNaN(Height) ? 256 : Height;
        return new Size(width, height);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty || change.Property == SelectionProperty ||
            change.Property == Is24HourProperty || change.Property == MinuteIncrementProperty)
        {
            if (change.Property == SelectionProperty)
                CoerceValue(ValueProperty);
            if (change.Property == SelectionProperty || change.Property == Is24HourProperty)
                BeginFaceTransition();
            if (change.Property == ValueProperty)
                ValueChanged?.Invoke(this, EventArgs.Empty);
            MoveSelectionIndicator();
        }
        else if (change.Property == CultureProperty)
        {
            // Face labels and their FormattedText glyph runs belong to the old culture.
            _cells = null;
            _outgoingCells = null;
            _texts.Clear();
            InvalidateVisual();
        }
        else if (change.Property == IsEnabledProperty)
        {
            if (!IsEffectivelyEnabled) EndDrag();
            InvalidateVisual();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        EndDrag();
        _gestureVersion++;
        _selectionAnimation?.Dispose();
        _selectionAnimation = null;
        _faceAnimation?.Dispose();
        _outgoingFaceAnimation?.Dispose();
        _faceAnimation = _outgoingFaceAnimation = null;
        _outgoingCells = null;
        _faceOpacity = 1;
        _outgoingFaceOpacity = 0;
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!IsEffectivelyEnabled) return;
        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed) return;
        Focus(NavigationMethod.Pointer);
        _gestureVersion++;
        _capturedPointer = e.Pointer;
        _isDragging = true;
        _dragMoved = false;
        _pressPosition = e.GetPosition(this);
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!IsEffectivelyEnabled) return;
        if (_isDragging)
        {
            var position = e.GetPosition(this);
            if (!_dragMoved)
            {
                var delta = position - _pressPosition;
                _dragMoved = Math.Abs(delta.X) > 2 || Math.Abs(delta.Y) > 2;
            }
            if (_dragMoved) _isDragging = true;
            ApplyPointer(position);
            e.Handled = true;
            return;
        }

        var hovered = HitTestNumber(e.GetPosition(this));
        if (hovered != _hoveredValue)
        {
            _hoveredValue = hovered;
            InvalidateVisual();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_isDragging) return;
        // Source detectTapGestures snaps a minute tap to the nearest five-minute label; a drag
        // tracks the pointer continuously (toMinute has one-minute precision).
        var isTap = !_dragMoved;
        _isDragging = !isTap;
        ApplyPointer(e.GetPosition(this), snapMinuteTap: isTap);
        EndDrag();
        MoveSelectionIndicator();
        PointerSelectionCompleted?.Invoke(this, new MaterialClockDialSelectionEventArgs(isTap));
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _isDragging = false;
        _capturedPointer = null;
        MoveSelectionIndicator();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_hoveredValue is null) return;
        _hoveredValue = null;
        InvalidateVisual();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!IsEffectivelyEnabled) return;
        var step = Selection == MaterialClockDialSelection.Minute ? Math.Max(1, MinuteIncrement) : 1;
        var handled = true;
        switch (e.Key)
        {
            case Key.Up:
            case Key.Right:
                SetValueFromKeyboard(Value + step);
                break;
            case Key.Down:
            case Key.Left:
                SetValueFromKeyboard(Value - step);
                break;
            case Key.PageUp:
                SetValueFromKeyboard(Value + (Selection == MaterialClockDialSelection.Minute ? 5 : 3));
                break;
            case Key.PageDown:
                SetValueFromKeyboard(Value - (Selection == MaterialClockDialSelection.Minute ? 5 : 3));
                break;
            case Key.Home:
                SetValueFromKeyboard(Selection == MaterialClockDialSelection.Minute ? 0 : Is24Hour ? 0 : 1);
                break;
            case Key.End:
                SetValueFromKeyboard(Selection == MaterialClockDialSelection.Minute ? 59 : Is24Hour ? 23 : 12);
                break;
            default:
                handled = false;
                break;
        }

        if (handled) e.Handled = true;
    }

    public override void Render(DrawingContext context)
    {
        var faceSize = Math.Min(Bounds.Width, Bounds.Height);
        if (faceSize <= 0) return;

        // The first render happens before any value change, so the indicator must be seeded here.
        if (double.IsNaN(_displayAngle))
            _displayAngle = Unwrap(TargetAngle(Value));

        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var radius = faceSize / 2;

        if (FaceBrush is { } face)
            context.DrawEllipse(face, null, center, radius, radius);

        var selectedPoint = PointOnRing(center, SelectedRingRadius(radius), _displayAngle);

        if (LineBrush is { } lineBrush)
        {
            _linePen.Brush = lineBrush;
            _linePen.Thickness = LineThickness;
            context.DrawLine(_linePen, center, selectedPoint);
        }

        var cells = BuildCells(center, radius);
        if (_outgoingCells is not null && _outgoingFaceOpacity > 0)
            using (context.PushOpacity(Math.Clamp(_outgoingFaceOpacity, 0, 1)))
                foreach (var cell in _outgoingCells) DrawCell(context, cell, false);
        using (context.PushOpacity(Math.Clamp(_faceOpacity, 0, 1)))
            foreach (var cell in cells) DrawCell(context, cell);

        if (SelectedBrush is { } selected)
        {
            context.DrawEllipse(selected, null, selectedPoint, CellRadius, CellRadius);
            DrawNumber(context, selectedPoint, SelectedLabel(), SelectedTextBrush);
            // Selected + hovered: the 8% state layer sits on top of the filled cell.
            if (IsSelectedCellHovered() && StateLayerBrush is { } selectedHover)
                DrawStateLayer(context, selectedHover, selectedPoint);
        }

        if (SelectedBrush is { } dot)
            context.DrawEllipse(dot, null, center, CenterDotRadius, CenterDotRadius);

        if (IsFocused && FocusRingBrush is { } ringBrush)
        {
            _focusPen.Brush = ringBrush;
            _focusPen.Thickness = FocusRingThickness;
            context.DrawEllipse(null, _focusPen, selectedPoint, CellRadius + FocusRingGap,
                CellRadius + FocusRingGap);
        }
    }

    private void DrawCell(DrawingContext context, NumberCell cell, bool current = true)
    {
        if (current && IsCellSelected(cell)) return;

        var isHovered = _hoveredValue is { } hovered && hovered == cell.Value;
        var numberBrush = IsEffectivelyEnabled ? NumberBrush : DisabledNumberBrush;

        if (isHovered && StateLayerBrush is { } hover)
            DrawStateLayer(context, hover, cell.Center);

        if (isHovered && IsFocused && FocusRingBrush is { } hoverRing)
        {
            _focusPen.Brush = hoverRing;
            _focusPen.Thickness = FocusRingThickness;
            context.DrawEllipse(null, _focusPen, cell.Center, CellRadius + FocusRingGap,
                CellRadius + FocusRingGap);
        }

        DrawNumber(context, cell.Center, cell.Label, numberBrush);
    }

    private bool IsSelectedCellHovered() => _hoveredValue is { } hovered && hovered == Value;

    private void DrawStateLayer(DrawingContext context, IBrush brush, Point center)
    {
        using (context.PushOpacity(StateLayerOpacity))
            context.DrawEllipse(brush, null, center, CellRadius, CellRadius);
    }

    private void DrawNumber(DrawingContext context, Point center, string? text, IBrush? brush)
    {
        if (brush is null || string.IsNullOrEmpty(text)) return;
        var key = new TextKey(text, brush, NumberFontSize, NumberFontWeight, FontFamily);
        if (!_texts.TryGetValue(key, out var formatted))
        {
            // Keys include the brush instance, so a consumer that swaps brushes per frame could
            // otherwise grow the cache without bound.
            if (_texts.Count >= 64) _texts.Clear();
            var typeface = new Typeface(FontFamily, FontStyle.Normal, NumberFontWeight);
            formatted = new FormattedText(text, EffectiveCulture, FlowDirection.LeftToRight,
                typeface, NumberFontSize, brush);
            _texts[key] = formatted;
        }

        context.DrawText(formatted, new Point(center.X - formatted.Width / 2, center.Y - formatted.Height / 2));
    }

    /// <summary>
    /// Builds (or returns the cached) number geometry for the current face. Render calls this
    /// every frame, so it never mutates animation state or invalidates the control: the face
    /// crossfade is started from <see cref="OnPropertyChanged"/> instead. Starting a spring here
    /// would run its reduced-motion <c>Finish</c> callback inside the render pass and throw
    /// "Visual was invalidated during the render pass".
    /// </summary>
    private IReadOnlyList<NumberCell> BuildCells(Point center, double radius)
    {
        var key = new CellKey(center.X, center.Y, radius, Selection, Is24Hour);
        if (_cells is { } cached && _cellKey.Equals(key))
            return cached;

        var cells = new List<NumberCell>(24);
        var outer = radius * OuterRingFactor;
        var inner = radius * InnerRingFactor;

        if (Selection == MaterialClockDialSelection.Minute)
        {
            for (var index = 0; index < 12; index++)
            {
                var value = index * 5;
                // ClockText uses value.toLocalString(): minutes have no leading zero.
                cells.Add(new NumberCell(value, MaterialTimeNumerals.Format(value, 1, EffectiveCulture),
                    PointOnRing(center, outer, GetPositionAngle(index, 12)), false));
            }
        }
        else
        {
            for (var index = 0; index < 12; index++)
            {
                // The source's outer 24-hour ring is screen[index] % 12 (0 at 12 o'clock), while
                // the 12-hour face shows Hours (12 at 12 o'clock).
                var value = Is24Hour ? index : index == 0 ? 12 : index;
                cells.Add(new NumberCell(value, MaterialTimeNumerals.Format(value, 1, EffectiveCulture),
                    PointOnRing(center, outer, GetPositionAngle(index, 12)), false));
            }

            if (Is24Hour)
            {
                // ExtraHours = Hours % 12 + 12: 12 at 12 o'clock, then 13 through 23.
                for (var index = 0; index < 12; index++)
                {
                    var value = 12 + index;
                    cells.Add(new NumberCell(value, MaterialTimeNumerals.Format(value, 1, EffectiveCulture),
                        PointOnRing(center, inner, GetPositionAngle(index, 12)), true));
                }
            }
        }

        _cells = [.. cells];
        _cellKey = key;
        return _cells;
    }

    /// <summary>
    /// Starts the crossfade between the hour and minute faces, or between the 12- and 24-hour
    /// faces. Called from the property change (never from Render) so a reduced-motion spring may
    /// finish synchronously without touching the render pass.
    /// </summary>
    private void BeginFaceTransition()
    {
        // Nothing has been rendered yet (or the control is detached), so the new face simply
        // appears at full opacity.
        if (_cells is null || !this.IsAttachedToVisualTree())
            return;

        // The built face already matches the requested one (for example Selection was set to the
        // value it already had); there is no crossfade to start.
        if (_cellKey.Selection == Selection && _cellKey.Is24Hour == Is24Hour)
            return;

        var previousCells = _cells;
        var previousKey = _cellKey;
        var previousOpacity = _faceOpacity;
        var previousVelocity = _faceAnimation?.Velocity ?? 0;

        // Reversing a transition: the face being replaced is the one currently fading out.
        var reversing = _outgoingCells is not null &&
                        _outgoingCellKey.Selection == Selection &&
                        _outgoingCellKey.Is24Hour == Is24Hour;
        var incomingCells = reversing ? _outgoingCells : null;
        var incomingKey = reversing ? _outgoingCellKey : default;
        var incomingOpacity = reversing ? _outgoingFaceOpacity : 0;
        var incomingVelocity = reversing ? _outgoingFaceAnimation?.Velocity ?? 0 : 0;

        _faceAnimation?.Dispose();
        _outgoingFaceAnimation?.Dispose();
        _outgoingCells = previousCells;
        _outgoingCellKey = previousKey;
        _faceOpacity = incomingOpacity;
        _outgoingFaceOpacity = previousOpacity;
        if (reversing)
        {
            _cells = incomingCells;
            _cellKey = incomingKey;
        }

        _faceAnimation = MaterialMotion.Animate(this, incomingOpacity, 1,
            alpha => { _faceOpacity = alpha; InvalidateVisual(); },
            MaterialSpring.EffectsDefault, initialVelocity: incomingVelocity);
        _outgoingFaceAnimation = MaterialMotion.Animate(this, previousOpacity, 0,
            alpha => { _outgoingFaceOpacity = alpha; InvalidateVisual(); },
            MaterialSpring.EffectsDefault, initialVelocity: previousVelocity);
    }

    // 12-hour faces hold 1-12; 24-hour faces hold 1-12 outside and 0,13-23 inside, so the
    // value identifies exactly one cell on either ring.
    private bool IsCellSelected(NumberCell cell) =>
        Selection == MaterialClockDialSelection.Hour && cell.Value == Value;

    private double SelectedRingRadius(double radius)
    {
        if (Selection == MaterialClockDialSelection.Minute) return radius * OuterRingFactor;
        if (!Is24Hour) return radius * OuterRingFactor;
        // Outer ring carries 0-11 and the inner ring carries 12-23 in 24-hour mode.
        return Value >= 12 ? radius * InnerRingFactor : radius * OuterRingFactor;
    }

    /// <summary>The number carried by the moving indicator; the resting cell is covered by it.</summary>
    private string SelectedLabel() => MaterialTimeNumerals.Format(Value, 1, EffectiveCulture);

    private static Point PointOnRing(Point center, double radius, double angle) =>
        new(center.X + radius * Math.Sin(angle), center.Y - radius * Math.Cos(angle));

    private void ApplyPointer(Point position, bool snapMinuteTap = false)
    {
        var next = ValueFromPoint(position, snapMinuteTap);
        if (next == Value)
        {
            SnapIndicator(next);
            InvalidateVisual();
            return;
        }

        SetCurrentValue(ValueProperty, next);
    }

    private int ValueFromPoint(Point position, bool snapMinuteTap = false)
    {
        var faceSize = Math.Min(Bounds.Width, Bounds.Height);
        var radius = faceSize / 2;
        if (radius <= 0) return Value;

        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var offset = position - center;
        var distance = Math.Sqrt(offset.X * offset.X + offset.Y * offset.Y);
        // The centre is a dead zone; the dot itself must not select an arbitrary number.
        if (distance < CenterDotRadius * 2) return Value;

        var angle = Math.Atan2(offset.X, -offset.Y);
        if (angle < 0) angle += Math.Tau;
        var fraction = angle / Math.Tau;

        if (Selection == MaterialClockDialSelection.Minute)
        {
            var raw = fraction * 60;
            if (snapMinuteTap)
            {
                var tapped = (int)Math.Round(raw / 5, MidpointRounding.AwayFromZero) * 5;
                return ((tapped % 60) + 60) % 60;
            }
            var increment = Math.Max(1, MinuteIncrement);
            var snapped = (int)Math.Round(raw / increment, MidpointRounding.AwayFromZero) * increment;
            return ((snapped % 60) + 60) % 60;
        }

        var index = (int)Math.Round(fraction * 12, MidpointRounding.AwayFromZero) % 12;
        if (!Is24Hour)
            return index == 0 ? 12 : index;

        var innerRing = distance < radius * RingBoundaryFactor;
        if (!innerRing)
            return index;
        return 12 + index;
    }

    private int? HitTestNumber(Point position)
    {
        var faceSize = Math.Min(Bounds.Width, Bounds.Height);
        if (faceSize <= 0) return null;
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var radius = faceSize / 2;

        int? nearest = null;
        var nearestDistance = CellRadius;
        foreach (var cell in BuildCells(center, radius))
        {
            var offset = position - cell.Center;
            var distance = Math.Sqrt(offset.X * offset.X + offset.Y * offset.Y);
            if (distance > nearestDistance) continue;
            nearestDistance = distance;
            nearest = cell.Value;
        }

        return nearest;
    }

    private void SetValueFromKeyboard(int value)
    {
        var next = WrapValue(value);
        if (next == Value) return;
        SetCurrentValue(ValueProperty, next);
        MoveSelectionIndicator();
    }

    private int WrapValue(int value)
    {
        if (Selection == MaterialClockDialSelection.Minute) return ((value % 60) + 60) % 60;
        if (Is24Hour) return ((value % 24) + 24) % 24;
        var wrapped = ((value - 1) % 12 + 12) % 12;
        return wrapped + 1;
    }

    private int CoerceDialValue(int value) => Selection == MaterialClockDialSelection.Minute
        ? Math.Clamp(value, 0, 59)
        : Math.Clamp(value, 0, 23);

    /// <summary>The clockwise angle, from 12 o'clock, at which <paramref name="value"/> sits.</summary>
    private double TargetAngle(int value)
    {
        if (Selection == MaterialClockDialSelection.Minute)
            return value * Math.Tau / 60;

        // Both rings use twelve positions; value % 12 selects the position, and the ring is
        // chosen by SelectedRingRadius.
        return GetPositionAngle(value % 12, 12);
    }

    // Dragging follows the pointer frame by frame, so the indicator keeps its current turn
    // instead of jumping back a full revolution when the value crosses 12 o'clock.
    private void SnapIndicator(int value) => _displayAngle = double.IsNaN(_displayAngle)
        ? Unwrap(TargetAngle(value))
        : UnwrapNear(TargetAngle(value), _displayAngle);

    private void MoveSelectionIndicator()
    {
        if (_isDragging)
        {
            SnapIndicator(Value);
            InvalidateVisual();
            return;
        }

        var target = TargetAngle(Value);
        if (double.IsNaN(_displayAngle))
        {
            _displayAngle = Unwrap(target);
            InvalidateVisual();
            return;
        }

        var unwrapped = UnwrapNear(target, _displayAngle);
        if (Math.Abs(unwrapped - _displayAngle) < 0.0001)
        {
            InvalidateVisual();
            return;
        }

        if (_selectionAnimation is not null)
            _selectionAnimation.Retarget(unwrapped);
        else
            _selectionAnimation = MaterialMotion.Animate(this, _displayAngle, unwrapped, angle =>
            {
                _displayAngle = angle;
                InvalidateVisual();
            }, MaterialSpring.SpatialDefault);
    }

    private static double Unwrap(double angle)
    {
        var wrapped = angle % Math.Tau;
        return wrapped < 0 ? wrapped + Math.Tau : wrapped;
    }

    private static double UnwrapNear(double target, double reference)
    {
        var delta = Unwrap(target - reference + Math.PI) - Math.PI;
        return reference + delta;
    }

    private void EndDrag()
    {
        if (_capturedPointer is { } pointer)
        {
            pointer.Capture(null);
            _capturedPointer = null;
        }

        _isDragging = false;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new MaterialClockDialAutomationPeer(this);

    private readonly record struct NumberCell(int Value, string Label, Point Center, bool IsInner);

    private readonly record struct CellKey(double CenterX, double CenterY, double Radius,
        MaterialClockDialSelection Selection, bool Is24Hour);

    private readonly record struct TextKey(string Text, IBrush Brush, double FontSize,
        FontWeight Weight, FontFamily Family);

    private sealed class MaterialClockDialAutomationPeer(MaterialClockDial owner)
        : ControlAutomationPeer(owner), IRangeValueProvider
    {
        protected override string GetClassNameCore() => nameof(MaterialClockDial);

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Slider;

        public bool IsReadOnly => false;

        public double LargeChange => owner.Selection == MaterialClockDialSelection.Minute ? 5 : 3;

        public double Maximum => owner.Selection == MaterialClockDialSelection.Minute
            ? 59
            : owner.Is24Hour ? 23 : 12;

        public double Minimum => owner.Selection == MaterialClockDialSelection.Minute
            ? 0
            : owner.Is24Hour ? 0 : 1;

        public double SmallChange => owner.Selection == MaterialClockDialSelection.Minute
            ? Math.Max(1, owner.MinuteIncrement)
            : 1;

        public double Value => owner.Value;

        public void SetValue(double value)
        {
            if (!owner.IsEffectivelyEnabled || double.IsNaN(value)) return;
            owner.SetCurrentValue(ValueProperty, (int)Math.Round(value));
        }
    }
}
