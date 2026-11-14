using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// Standalone composition equivalent to <c>PullToRefreshDefaults.IndicatorBox</c>: the pull
/// fraction translates the box to <c>DistanceFraction * MaxDistance - height</c>, the container is
/// clipped to the source shape and elevated while a pull or refresh is visible.
/// </summary>
public sealed class MaterialRefreshIndicatorBox : ContentControl
{
    /// <summary>Value from 0..1 for a normal pull; values above 1 are the source tension overshoot.</summary>
    public static readonly StyledProperty<double> DistanceFractionProperty =
        AvaloniaProperty.Register<MaterialRefreshIndicatorBox, double>(nameof(DistanceFraction));
    /// <summary>Whether a refresh is in progress; keeps the container elevated while it runs.</summary>
    public static readonly StyledProperty<bool> IsRefreshingProperty =
        AvaloniaProperty.Register<MaterialRefreshIndicatorBox, bool>(nameof(IsRefreshing));
    /// <summary>Visual travel in DIPs. Independent from the refresh threshold.</summary>
    public static readonly StyledProperty<double> MaxDistanceProperty =
        AvaloniaProperty.Register<MaterialRefreshIndicatorBox, double>(nameof(MaxDistance), 80,
            validate: value => double.IsFinite(value) && value >= 0);
    /// <summary>Optional container outline. When null, <see cref="TemplatedControl.CornerRadius"/> is used.</summary>
    public static readonly StyledProperty<Geometry?> ContainerShapeProperty =
        AvaloniaProperty.Register<MaterialRefreshIndicatorBox, Geometry?>(nameof(ContainerShape));
    /// <summary>How <see cref="ContainerShape"/> is fitted into the indicator bounds.</summary>
    public static readonly StyledProperty<Stretch> ContainerShapeStretchProperty =
        AvaloniaProperty.Register<MaterialRefreshIndicatorBox, Stretch>(nameof(ContainerShapeStretch), Stretch.Uniform);
    /// <summary>Material elevation token level, 0..5. The theme's matching shadow is used.</summary>
    public static readonly StyledProperty<double> ElevationProperty =
        AvaloniaProperty.Register<MaterialRefreshIndicatorBox, double>(nameof(Elevation), 2,
            validate: value => double.IsFinite(value) && value >= 0);

    private readonly TranslateTransform _translation = new();
    private ContentPresenter? _content;
    private Geometry? _fittedShape;
    private Geometry? _shapeSource;
    private Size _shapeSize;
    private Stretch _shapeStretch;
    private Geometry? _roundedClip;
    private Size _roundedSize;
    private CornerRadius _roundedCorner;
    private BoxShadows _shadow;
    private int _shadowLevel = -1;
    private int _effectLevel = -1;

    static MaterialRefreshIndicatorBox()
    {
        AffectsRender<MaterialRefreshIndicatorBox>(DistanceFractionProperty, IsRefreshingProperty,
            MaxDistanceProperty, ContainerShapeProperty, ContainerShapeStretchProperty,
            ElevationProperty, BackgroundProperty, CornerRadiusProperty, FlowDirectionProperty);
    }

    public MaterialRefreshIndicatorBox()
    {
        Width = Height = 40;
        CornerRadius = new CornerRadius(9999);
        ClipToBounds = false;
        IsHitTestVisible = false;
        RenderTransform = _translation;
        HorizontalContentAlignment = HorizontalAlignment.Center;
        VerticalContentAlignment = VerticalAlignment.Center;
        Template = new FuncControlTemplate<MaterialRefreshIndicatorBox>((owner, _) => owner.Build());
        AttachedToVisualTree += (_, _) =>
        {
            UpdateTranslation();
            UpdateShape(Bounds.Size);
            _shadowLevel = -1;
            UpdateShadow();
        };
        ActualThemeVariantChanged += (_, _) =>
        {
            _shadowLevel = -1;
            UpdateShadow();
            InvalidateVisual();
        };
    }

    public double DistanceFraction { get => GetValue(DistanceFractionProperty); set => SetValue(DistanceFractionProperty, value); }
    public bool IsRefreshing { get => GetValue(IsRefreshingProperty); set => SetValue(IsRefreshingProperty, value); }
    public double MaxDistance { get => GetValue(MaxDistanceProperty); set => SetValue(MaxDistanceProperty, value); }
    public Geometry? ContainerShape { get => GetValue(ContainerShapeProperty); set => SetValue(ContainerShapeProperty, value); }
    public Stretch ContainerShapeStretch { get => GetValue(ContainerShapeStretchProperty); set => SetValue(ContainerShapeStretchProperty, value); }
    public double Elevation { get => GetValue(ElevationProperty); set => SetValue(ElevationProperty, value); }

    private Control Build()
    {
        var content = new ContentPresenter
        {
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
        };
        content[!ContentPresenter.ContentProperty] = this[!ContentProperty];
        content[!ContentPresenter.ContentTemplateProperty] = this[!ContentTemplateProperty];
        _content = content;
        UpdateShape(Bounds.Size);
        return content;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty)
        {
            UpdateTranslation(Bounds.Height);
            UpdateShape(Bounds.Size);
            UpdateShapeElevation();
        }
        if (change.Property == DistanceFractionProperty || change.Property == MaxDistanceProperty)
            UpdateTranslation();
        if (change.Property == DistanceFractionProperty || change.Property == IsRefreshingProperty ||
            change.Property == ContainerShapeProperty)
            UpdateShapeElevation();
        if (change.Property == ElevationProperty)
        {
            _shadowLevel = -1;
            UpdateShadow();
        }
        if (change.Property == ContainerShapeProperty || change.Property == ContainerShapeStretchProperty)
            UpdateShape(Bounds.Size);
        if (change.Property == ContainerShapeProperty)
        {
            if (change.OldValue is Geometry oldShape) oldShape.Changed -= OnShapeChanged;
            if (change.NewValue is Geometry newShape) newShape.Changed += OnShapeChanged;
        }
        if (change.Property == CornerRadiusProperty || change.Property == FlowDirectionProperty)
            UpdateShape(Bounds.Size);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var bounds = new Rect(Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        var showElevation = DistanceFraction > 0 || IsRefreshing;
        if (ContainerShape is { } source)
        {
            var fitted = FitShape(source, bounds.Size);
            if (Background is { } customBrush)
            {
                using (context.PushClip(bounds))
                    context.DrawGeometry(customBrush, null, fitted);
            }
            return;
        }

        var corner = ResolveCornerRadius(bounds);
        var rounded = new RoundedRect(bounds, corner);
        if (Background is { } brush)
            context.DrawRectangle(brush, null, rounded, showElevation ? _shadow : default);
    }

    private void UpdateTranslation() => UpdateTranslation(Bounds.Height);

    private void UpdateTranslation(double height) =>
        _translation.Y = DistanceFraction * MaxDistance - height;

    private void UpdateShape(Size size)
    {
        if (ContainerShape is not { } source)
        {
            _fittedShape = null;
            _shapeSource = null;
            if (size.Width > 0 && size.Height > 0)
            {
                var corner = ResolveCornerRadius(new Rect(size));
                if (_roundedClip is null || size != _roundedSize || corner != _roundedCorner)
                {
                    _roundedClip = CreateRoundedRectGeometry(new Rect(size), corner);
                    _roundedSize = size;
                    _roundedCorner = corner;
                }
            }
            else _roundedClip = null;
            if (_content is not null) _content.Clip = _roundedClip;
            return;
        }
        _roundedClip = null;
        _fittedShape = FitShape(source, size);
        if (_content is not null) _content.Clip = _fittedShape;
    }

    /// <summary>
    /// Builds the same resolved outline that <see cref="Render"/> paints as a
    /// <see cref="RoundedRect"/>, so caller content is clipped by the source shape contract
    /// rather than only by the parent's rectangular bounds.
    /// </summary>
    private static Geometry CreateRoundedRectGeometry(Rect rect, CornerRadius corner)
    {
        var maxRadius = Math.Min(rect.Width, rect.Height) / 2;
        var topLeft = Math.Clamp(corner.TopLeft, 0, maxRadius);
        var topRight = Math.Clamp(corner.TopRight, 0, maxRadius);
        var bottomRight = Math.Clamp(corner.BottomRight, 0, maxRadius);
        var bottomLeft = Math.Clamp(corner.BottomLeft, 0, maxRadius);
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(new Point(rect.X + topLeft, rect.Y), true);
            context.LineTo(new Point(rect.Right - topRight, rect.Y));
            if (topRight > 0)
                context.ArcTo(new Point(rect.Right, rect.Y + topRight), new Size(topRight, topRight), 0,
                    false, SweepDirection.Clockwise);
            context.LineTo(new Point(rect.Right, rect.Bottom - bottomRight));
            if (bottomRight > 0)
                context.ArcTo(new Point(rect.Right - bottomRight, rect.Bottom), new Size(bottomRight, bottomRight), 0,
                    false, SweepDirection.Clockwise);
            context.LineTo(new Point(rect.X + bottomLeft, rect.Bottom));
            if (bottomLeft > 0)
                context.ArcTo(new Point(rect.X, rect.Bottom - bottomLeft), new Size(bottomLeft, bottomLeft), 0,
                    false, SweepDirection.Clockwise);
            context.LineTo(new Point(rect.X, rect.Y + topLeft));
            if (topLeft > 0)
                context.ArcTo(new Point(rect.X + topLeft, rect.Y), new Size(topLeft, topLeft), 0,
                    false, SweepDirection.Clockwise);
            context.EndFigure(true);
        }
        return geometry;
    }

    private void OnShapeChanged(object? sender, EventArgs e)
    {
        _fittedShape = null;
        UpdateShape(Bounds.Size);
        InvalidateVisual();
    }

    private Geometry FitShape(Geometry source, Size size)
    {
        if (_fittedShape is not null && ReferenceEquals(source, _shapeSource) &&
            size == _shapeSize && ContainerShapeStretch == _shapeStretch)
            return _fittedShape;

        _shapeSource = source;
        _shapeSize = size;
        _shapeStretch = ContainerShapeStretch;
        return _fittedShape = LoadingIndicatorShapes.StretchGeometry(source, new Rect(size), ContainerShapeStretch);
    }

    private CornerRadius ResolveCornerRadius(Rect bounds)
    {
        var minDimension = Math.Min(bounds.Width, bounds.Height);
        var rightToLeft = FlowDirection == FlowDirection.RightToLeft;
        var shape = IsSet(CornerRadiusProperty)
            ? MaterialAssist.CornerShape.FromCornerRadius(CornerRadius, rightToLeft)
            : MaterialAssist.CornerShape.Full;
        return shape.Resolve(shape, 1, minDimension, rightToLeft);
    }

    private void UpdateShadow()
    {
        var level = Math.Clamp((int)Math.Round(Elevation), 0, 5);
        if (level != _shadowLevel)
        {
            _shadowLevel = level;
            _shadow = level > 0 && this.TryFindResource($"MaterialElevationLevel{level}Shadow", out var value) &&
                      value is BoxShadows shadows
                ? shadows
                : default;
            _effectLevel = -1;
            InvalidateVisual();
        }
        UpdateShapeElevation();
    }

    /// <summary>
    /// A geometry shape is painted with <see cref="DrawingContext.DrawGeometry"/>, which has no
    /// box-shadow overload. Avalonia's effect applies to the same clipped visual, preserving the
    /// source elevation while a pull or refresh is visible.
    /// </summary>
    private void UpdateShapeElevation()
    {
        var show = ContainerShape is not null && Elevation > 0 && _shadow.Count > 0 &&
                   (DistanceFraction > 0 || IsRefreshing);
        if (!show)
        {
            if (Effect is not null) Effect = null;
            _effectLevel = -1;
            return;
        }
        var level = Math.Clamp((int)Math.Round(Elevation), 0, 5);
        if (Effect is not null && level == _effectLevel) return;
        var shadow = _shadow[0];
        Effect = new DropShadowEffect
        {
            OffsetX = shadow.OffsetX,
            OffsetY = shadow.OffsetY,
            BlurRadius = shadow.Blur,
            Color = shadow.Color,
        };
        _effectLevel = level;
    }
}