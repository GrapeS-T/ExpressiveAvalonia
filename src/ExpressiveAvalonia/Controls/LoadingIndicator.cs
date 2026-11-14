using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using ExpressiveAvalonia.Shapes;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Controls;

/// <summary>Material loading indicator with determinate polygon morphing and an indeterminate cycle.</summary>
public sealed class LoadingIndicator : TemplatedControl
{
    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<LoadingIndicator, double>(nameof(Progress), 0,
            coerce: (_, value) => double.IsNaN(value) ? 0 : Math.Clamp(value, 0, 1));
    public static readonly StyledProperty<bool> IsIndeterminateProperty =
        AvaloniaProperty.Register<LoadingIndicator, bool>(nameof(IsIndeterminate), true);
    public static readonly StyledProperty<bool> IsAnimatedProperty =
        AvaloniaProperty.Register<LoadingIndicator, bool>(nameof(IsAnimated), true);
    public static readonly StyledProperty<bool> ShowContainerProperty =
        AvaloniaProperty.Register<LoadingIndicator, bool>(nameof(ShowContainer));
    public static readonly StyledProperty<IBrush?> ContainerBrushProperty =
        AvaloniaProperty.Register<LoadingIndicator, IBrush?>(nameof(ContainerBrush));
    public static readonly StyledProperty<IReadOnlyList<RoundedPolygon>?> PolygonsProperty =
        AvaloniaProperty.Register<LoadingIndicator, IReadOnlyList<RoundedPolygon>?>(nameof(Polygons),
            validate: value => value is null || (value.Count >= 2 && value.All(polygon => polygon is not null)));
    public static readonly StyledProperty<Geometry?> ContainerShapeProperty =
        AvaloniaProperty.Register<LoadingIndicator, Geometry?>(nameof(ContainerShape));
    public static readonly StyledProperty<Stretch> ContainerShapeStretchProperty =
        AvaloniaProperty.Register<LoadingIndicator, Stretch>(nameof(ContainerShapeStretch),
            Stretch.Uniform);

    private readonly ProgressAnimationClock _clock;
    private readonly LoadingIndicatorCycle _cycle = new();
    private IReadOnlyList<Morph>? _customMorphs;
    private RoundedPolygon[]? _customPolygons;
    private double _customScale;
    private StreamGeometry? _path;
    private (Morph Morph, double Progress, double Size, double Scale) _frameKey;
    private LoadingIndicatorAutomationPeer? _peer;
    private Geometry? _containerShapeSource;
    private Geometry? _containerShapeFrame;
    private Size _containerShapeFrameSize;
    private Stretch _containerShapeFrameStretch;

    static LoadingIndicator()
    {
        AffectsRender<LoadingIndicator>(ProgressProperty, IsIndeterminateProperty,
            ShowContainerProperty, ContainerBrushProperty, ForegroundProperty, IsAnimatedProperty,
            CornerRadiusProperty, FlowDirectionProperty, ContainerShapeProperty,
            ContainerShapeStretchProperty);
    }

    public LoadingIndicator() => _clock = new ProgressAnimationClock(this, () => IsAnimated && IsIndeterminate);

    /// <summary>Determinate progress, coerced to 0..1. NaN maps to zero.</summary>
    public double Progress { get => GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }
    public bool IsIndeterminate { get => GetValue(IsIndeterminateProperty); set => SetValue(IsIndeterminateProperty, value); }
    /// <summary>Whether the indeterminate cycle runs. Reduced motion takes precedence.</summary>
    public bool IsAnimated { get => GetValue(IsAnimatedProperty); set => SetValue(IsAnimatedProperty, value); }
    public bool ShowContainer { get => GetValue(ShowContainerProperty); set => SetValue(ShowContainerProperty, value); }
    public IBrush? ContainerBrush { get => GetValue(ContainerBrushProperty); set => SetValue(ContainerBrushProperty, value); }

    /// <summary>Optional sequence of at least two immutable polygons. Replace the list to update it.</summary>
    public IReadOnlyList<RoundedPolygon>? Polygons { get => GetValue(PolygonsProperty); set => SetValue(PolygonsProperty, value); }

    /// <summary>
    /// Optional container outline, used both to paint the container and to clip the active shape.
    /// Null keeps the <see cref="TemplatedControl.CornerRadius"/> / full-corner fallback.
    /// </summary>
    public Geometry? ContainerShape
    {
        get => GetValue(ContainerShapeProperty);
        set => SetValue(ContainerShapeProperty, value);
    }

    /// <summary>How <see cref="ContainerShape"/> is mapped into the control bounds.</summary>
    public Stretch ContainerShapeStretch
    {
        get => GetValue(ContainerShapeStretchProperty);
        set => SetValue(ContainerShapeStretchProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(Math.Min(LoadingIndicatorShapes.ContainerWidth, availableSize.Width),
            Math.Min(LoadingIndicatorShapes.ContainerHeight, availableSize.Height));

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_containerShapeSource is { } source)
            source.PropertyChanged += ContainerShapeChanged;
        _containerShapeFrame = null;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_containerShapeSource is { } source)
            source.PropertyChanged -= ContainerShapeChanged;
        base.OnDetachedFromVisualTree(e);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => _peer = new LoadingIndicatorAutomationPeer(this);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        _peer?.NotifyChanged(change);
        if (change.Property == ContainerShapeProperty)
        {
            if (_containerShapeSource is { } previous)
                previous.PropertyChanged -= ContainerShapeChanged;
            _containerShapeSource = ContainerShape;
            if (VisualRoot is not null && _containerShapeSource is { } source)
                source.PropertyChanged += ContainerShapeChanged;
            _containerShapeFrame = null;
        }
        else if (change.Property == ContainerShapeStretchProperty)
            _containerShapeFrame = null;
        if (change.Property == PolygonsProperty || change.Property == IsIndeterminateProperty)
        {
            var snapshot = Polygons?.ToArray();
            // The source remembers the morph sequence and the morph index keyed by the polygon list.
            // RoundedPolygon.equals compares its feature list, but Feature.Edge/Corner have no equals
            // override, so distinct RoundedPolygons are never equal: the key is the list length,
            // order and the same polygon instances. The ported polygons use reference equality, so
            // a replacement that reuses the same instances does not restart the cycle.
            if (change.Property == IsIndeterminateProperty ||
                !SequenceEquals(snapshot, _customPolygons))
            {
                // Source LoadingIndicatorImpl keys currentMorphIndex on indicatorPolygons, so a
                // new list starts again at morph zero; morphProgress and morphRotationTargetAngle
                // are remembered without that key and keep their in-flight values.
                if (change.Property == PolygonsProperty && IsIndeterminate && IsAnimated &&
                    !MaterialMotion.GetIsReduced(this))
                    _cycle.Restart(_clock.ElapsedMilliseconds);
                else
                    _cycle.Reset();
                if (snapshot is { } polygons)
                {
                    _customMorphs = LoadingIndicatorShapes.BuildMorphSequence(polygons, IsIndeterminate);
                    _customScale = LoadingIndicatorShapes.CalculateScaleFactor(polygons) * LoadingIndicatorShapes.ActiveIndicatorScale;
                }
                else _customMorphs = null;
                _path = null;
                InvalidateVisual();
            }
            _customPolygons = snapshot;
        }
        if (change.Property == IsAnimatedProperty || change.Property == IsIndeterminateProperty || change.Property == PolygonsProperty)
            _clock?.Refresh();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var bounds = new Rect(Bounds.Size);
        // The active indicator keeps the source square aspect ratio inside the container; the
        // container itself is never limited to that square.
        var size = Math.Min(bounds.Width, bounds.Height);
        if (size <= 0) return;
        var center = new Point(bounds.Width / 2, bounds.Height / 2);
        // The source applies clip(containerShape).background(containerColor) to the whole layout
        // bounds, so a non-square container paints its full outline and clips the indicator to the
        // same shape. An authored ContainerShape replaces the rounded rectangle for both.
        var containerGeometry = ResolveContainerShape(bounds);
        var container = containerGeometry is null
            ? new RoundedRect(bounds, ResolveContainerCornerRadius(bounds))
            : default;
        if (ShowContainer && ContainerBrush is { } containerBrush)
        {
            if (containerGeometry is { } containerBrushGeometry)
            {
                // Stretch.UniformToFill can paint outside the layout bounds; the source shape is
                // always created for the available size, so the container never exceeds it.
                using (context.PushClip(bounds))
                    context.DrawGeometry(containerBrush, null, containerBrushGeometry);
            }
            else
                context.DrawRectangle(containerBrush, null, container);
        }

        var morphs = _customMorphs ?? (IsIndeterminate ? LoadingIndicatorShapes.IndeterminateMorphs : LoadingIndicatorShapes.DeterminateMorphs);
        var scale = _customMorphs is not null ? _customScale : IsIndeterminate ? LoadingIndicatorShapes.IndeterminateScaleFactor : LoadingIndicatorShapes.DeterminateScaleFactor;
        int index;
        double progress, angle;
        if (IsIndeterminate)
        {
            var elapsed = IsAnimated && !MaterialMotion.GetIsReduced(this) ? _clock.ElapsedMilliseconds : 0;
            // The cycle advances the polygon and banks its quarter turn at spring completion,
            // then holds that endpoint until the next 650 ms slot begins.
            var frame = _cycle.At(elapsed);
            index = (int)(frame.Slot % morphs.Count);
            progress = frame.Progress;
            // globalRotation is the source Animatable value; the restarted infinite tween re-anchors
            // to the remembered value, so the cycle supplies it instead of a continuous sweep.
            angle = (90 + frame.Completed * 90 + progress * 90 + frame.Rotation) % 360;
        }
        else
        {
            var position = Progress * morphs.Count;
            index = Math.Min((int)position, morphs.Count - 1);
            progress = Progress == 1 ? 1 : position % 1;
            angle = -Progress * 180;
        }
        var key = (morphs[index], progress, size, scale);
        if (_path is null || key != _frameKey)
        {
            // Avalonia StreamGeometry.Open appends; a fresh native path is necessary for each
            // distinct frame. Static/held frames reuse it, and the matched cubics allocate nothing.
            _path = LoadingIndicatorShapes.CreateFrame(morphs[index], progress, size, scale);
            _frameKey = key;
        }
        using var containerClip = containerGeometry is { } outline
            ? context.PushGeometryClip(outline)
            : context.PushClip(container);
        using var boundsClip = containerGeometry is null
            ? default(DrawingContext.PushedState)
            : context.PushClip(bounds);
        using (context.PushTransform(Matrix.CreateTranslation(-size / 2, -size / 2) *
                   Matrix.CreateRotation(angle * Math.PI / 180) * Matrix.CreateTranslation(center.X, center.Y)))
            context.DrawGeometry(Foreground, null, _path);
    }

    /// <summary>
    /// The authored <see cref="ContainerShape"/> mapped into the control bounds, or null when
    /// the container should use the rounded-rectangle fallback. The clone is cached per authored
    /// instance, bounds and stretch so a frame never allocates a geometry.
    /// </summary>
    private Geometry? ResolveContainerShape(Rect bounds)
    {
        if (ContainerShape is not { } source) return null;
        var stretch = ContainerShapeStretch;
        if (_containerShapeFrame is { } cached &&
            ReferenceEquals(source, _containerShapeSource) &&
            _containerShapeFrameSize == bounds.Size &&
            _containerShapeFrameStretch == stretch)
            return cached;

        _containerShapeSource = source;
        _containerShapeFrame = LoadingIndicatorShapes.StretchGeometry(source, bounds, stretch);
        _containerShapeFrameSize = bounds.Size;
        _containerShapeFrameStretch = stretch;
        return _containerShapeFrame;
    }

    private void ContainerShapeChanged(object? sender, AvaloniaPropertyChangedEventArgs change)
    {
        _containerShapeFrame = null;
        InvalidateVisual();
    }

    private static bool SequenceEquals(RoundedPolygon[]? left, RoundedPolygon[]? right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null || left.Length != right.Length) return false;
        for (var index = 0; index < left.Length; index++)
        {
            if (!ReferenceEquals(left[index], right[index])) return false;
        }
        return true;
    }

    /// <summary>
    /// Corner radii of the container outline. The source <c>containerShape</c> defaults to
    /// <c>ShapeKeyTokens.CornerFull</c>, i.e. a size-relative 50% corner that reads as a circle on
    /// a square container and as a capsule on a non-square one. A caller-supplied
    /// <see cref="TemplatedControl.CornerRadius"/> replaces that default, including an explicit
    /// square value, and keeps its logical start/end meaning through the shared
    /// <see cref="MaterialAssist.CornerShape"/> resolution.
    /// </summary>
    private CornerRadius ResolveContainerCornerRadius(Rect bounds)
    {
        var minDimension = Math.Min(bounds.Width, bounds.Height);
        var rightToLeft = FlowDirection == FlowDirection.RightToLeft;
        var shape = IsSet(CornerRadiusProperty)
            ? MaterialAssist.CornerShape.FromCornerRadius(CornerRadius, rightToLeft)
            : MaterialAssist.CornerShape.Full;
        return shape.Resolve(shape, 1, minDimension, rightToLeft);
    }

    private sealed class LoadingIndicatorAutomationPeer(LoadingIndicator owner)
        : ControlAutomationPeer(owner), IRangeValueProvider
    {
        protected override string GetClassNameCore() => nameof(LoadingIndicator);
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ProgressBar;
        protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : "Loading";
        protected override string? GetItemStatusCore() =>
            AutomationProperties.GetItemStatus(owner) ?? (owner.IsIndeterminate ? "Indeterminate" : base.GetItemStatusCore());
        protected override object? GetProviderCore(Type providerType) =>
            providerType == typeof(IRangeValueProvider) ? owner.IsIndeterminate ? null : this : base.GetProviderCore(providerType);
        internal void NotifyChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == ProgressProperty)
                RaisePropertyChangedEvent(RangeValuePatternIdentifiers.ValueProperty, e.OldValue, e.NewValue);
            if (e.Property == IsIndeterminateProperty)
                RaisePropertyChangedEvent(AutomationElementIdentifiers.ItemStatusProperty,
                    e.OldValue is true ? "Indeterminate" : null, GetItemStatusCore());
        }
        bool IRangeValueProvider.IsReadOnly => true;
        double IRangeValueProvider.Minimum => 0;
        double IRangeValueProvider.Maximum => 1;
        double IRangeValueProvider.Value => owner.Progress;
        double IRangeValueProvider.SmallChange => double.NaN;
        double IRangeValueProvider.LargeChange => double.NaN;
        void IRangeValueProvider.SetValue(double value) => throw new InvalidOperationException("Loading progress is read-only to automation.");
    }
}
