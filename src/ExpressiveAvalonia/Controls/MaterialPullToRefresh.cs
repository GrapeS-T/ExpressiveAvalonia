using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Input.GestureRecognizers;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Theming;
using ExpressiveAvalonia.Tokens;

namespace ExpressiveAvalonia.Controls;

/// <summary>Source default circular-arrow indicator or the Expressive polygon loader.</summary>
public enum MaterialRefreshIndicator { Circular, Loading }

/// <summary>Pull-to-refresh with externally owned refreshing state. Wheel scrolling never requests refresh.</summary>
public sealed class MaterialPullToRefresh : ContentControl
{
    public static readonly StyledProperty<bool> IsRefreshingProperty = AvaloniaProperty.Register<MaterialPullToRefresh, bool>(nameof(IsRefreshing));
    public static readonly StyledProperty<bool> AllowMousePullProperty = AvaloniaProperty.Register<MaterialPullToRefresh, bool>(nameof(AllowMousePull));
    public static readonly StyledProperty<MaterialRefreshIndicator> IndicatorTypeProperty = AvaloniaProperty.Register<MaterialPullToRefresh, MaterialRefreshIndicator>(nameof(IndicatorType));
    public static readonly StyledProperty<double> ThresholdProperty = AvaloniaProperty.Register<MaterialPullToRefresh, double>(nameof(Threshold), 80,
        validate: value => double.IsFinite(value) && value > 0);
    /// <summary>Indicator travel in DIPs, independent from <see cref="Threshold"/> (source default 80).</summary>
    public static readonly StyledProperty<double> MaxDistanceProperty = AvaloniaProperty.Register<MaterialPullToRefresh, double>(nameof(MaxDistance), 80,
        validate: value => double.IsFinite(value) && value >= 0);
    /// <summary>Optional custom indicator slot. Replaces the built-in indicator.</summary>
    public static readonly StyledProperty<object?> IndicatorContentProperty = AvaloniaProperty.Register<MaterialPullToRefresh, object?>(nameof(IndicatorContent));
    /// <summary>Optional template for <see cref="IndicatorContent"/>. With no content, the owner is the data context.</summary>
    public static readonly StyledProperty<IDataTemplate?> IndicatorTemplateProperty = AvaloniaProperty.Register<MaterialPullToRefresh, IDataTemplate?>(nameof(IndicatorTemplate));
    /// <summary>Optional container outline for the built-in indicator. Null uses a full corner (circle).</summary>
    public static readonly StyledProperty<Geometry?> IndicatorShapeProperty = AvaloniaProperty.Register<MaterialPullToRefresh, Geometry?>(nameof(IndicatorShape));
    /// <summary>How <see cref="IndicatorShape"/> is fitted into the built-in indicator.</summary>
    public static readonly StyledProperty<Stretch> IndicatorShapeStretchProperty = AvaloniaProperty.Register<MaterialPullToRefresh, Stretch>(nameof(IndicatorShapeStretch), Stretch.Uniform);
    /// <summary>Built-in indicator container color. Null follows the source color role for the indicator type.</summary>
    public static readonly StyledProperty<IBrush?> IndicatorContainerColorProperty = AvaloniaProperty.Register<MaterialPullToRefresh, IBrush?>(nameof(IndicatorContainerColor));
    /// <summary>Built-in arrow/loader color. Null follows the source color role for the indicator type.</summary>
    public static readonly StyledProperty<IBrush?> IndicatorColorProperty = AvaloniaProperty.Register<MaterialPullToRefresh, IBrush?>(nameof(IndicatorColor));
    /// <summary>Built-in indicator elevation token level (0..5). Null uses Level2 for Circular and Level0 for Loading.</summary>
    public static readonly StyledProperty<double?> IndicatorElevationProperty = AvaloniaProperty.Register<MaterialPullToRefresh, double?>(nameof(IndicatorElevation),
        validate: value => value is not { } elevation || (double.IsFinite(elevation) && elevation >= 0));
    public static readonly StyledProperty<ICommand?> RefreshCommandProperty = AvaloniaProperty.Register<MaterialPullToRefresh, ICommand?>(nameof(RefreshCommand));
    public static readonly StyledProperty<object?> RefreshCommandParameterProperty = AvaloniaProperty.Register<MaterialPullToRefresh, object?>(nameof(RefreshCommandParameter));
    public static readonly DirectProperty<MaterialPullToRefresh, double> DistanceFractionProperty =
        AvaloniaProperty.RegisterDirect<MaterialPullToRefresh, double>(nameof(DistanceFraction), owner => owner.DistanceFraction);
    public static readonly RoutedEvent<RoutedEventArgs> RefreshRequestedEvent =
        RoutedEvent.Register<MaterialPullToRefresh, RoutedEventArgs>(nameof(RefreshRequested), RoutingStrategies.Bubble);

    private Control? _pullVisual, _busyVisual;
    private LoadingIndicator? _pull;
    private RefreshArrowIndicator? _arrow;
    private MaterialRefreshIndicatorBox? _indicator;
    private Grid? _builtInPanel;
    private ContentPresenter? _customIndicator;
    private readonly RotateTransform _rotation = new();
    private MaterialMotion.SpringAnimation? _settle, _crossfade;
    private double _distanceFraction, _distancePulled, _fade;
    private IPointer? _pointer;
    private Point _last, _start;
    private ScrollViewer? _scroller;
    private ScrollContentPresenter? _scrollPresenter;
    private bool _dragging;
    private ScrollGestureEventArgs? _pendingGesture;
    private double _gestureOffset;
    private int _releaseGeneration;
    private RefreshAutomationPeer? _peer;

    static MaterialPullToRefresh()
    {
        // ScrollGesture is bubble-only in Avalonia. A class handler at the source runs
        // before the native ScrollContentPresenter's instance handler, providing pre-scroll.
        ScrollGestureEvent.AddClassHandler<InputElement>((source, e) =>
        {
            if (!ReferenceEquals(source, e.Source)) return;
            for (Visual? visual = source; visual is not null; visual = visual.GetVisualParent())
            {
                if (visual is MaterialPullToRefresh refresh)
                {
                    refresh.GesturePreScroll(source, e);
                    break;
                }
            }
        }, RoutingStrategies.Bubble, true);
    }

    public MaterialPullToRefresh()
    {
        ClipToBounds = true;
        Template = new FuncControlTemplate<MaterialPullToRefresh>((owner, _) => owner.Build());
        AddHandler(PointerPressedEvent, Pressed, RoutingStrategies.Tunnel, true);
        AddHandler(PointerMovedEvent, Moved, RoutingStrategies.Tunnel, true);
        AddHandler(PointerReleasedEvent, Released, RoutingStrategies.Tunnel, true);
        AddHandler(PointerCaptureLostEvent, CaptureLost, RoutingStrategies.Tunnel, true);
        GestureRecognizers.Add(new ScrollGestureRecognizer { CanVerticallyScroll = true });
        AddHandler(ScrollGestureEvent, GesturePostScroll, RoutingStrategies.Bubble, true);
        AttachedToVisualTree += (_, _) => { ApplyIndicatorAppearance(); ApplyFraction(IsRefreshing ? 1 : 0); ApplyFade(IsRefreshing ? 1 : 0); };
        DetachedFromVisualTree += (_, _) => CancelGesture();
        ActualThemeVariantChanged += (_, _) => ApplyIndicatorAppearance();
    }

    public bool IsRefreshing { get => GetValue(IsRefreshingProperty); set => SetValue(IsRefreshingProperty, value); }
    public bool AllowMousePull { get => GetValue(AllowMousePullProperty); set => SetValue(AllowMousePullProperty, value); }
    public MaterialRefreshIndicator IndicatorType { get => GetValue(IndicatorTypeProperty); set => SetValue(IndicatorTypeProperty, value); }
    public double Threshold { get => GetValue(ThresholdProperty); set => SetValue(ThresholdProperty, value); }
    /// <summary>Maximum indicator travel. Changing it does not change the pull trigger threshold.</summary>
    public double MaxDistance { get => GetValue(MaxDistanceProperty); set => SetValue(MaxDistanceProperty, value); }
    /// <summary>Custom indicator overlay replacing the built-in indicator.</summary>
    public object? IndicatorContent { get => GetValue(IndicatorContentProperty); set => SetValue(IndicatorContentProperty, value); }
    /// <summary>Template for <see cref="IndicatorContent"/>; when content is null its data context is this control.</summary>
    public IDataTemplate? IndicatorTemplate { get => GetValue(IndicatorTemplateProperty); set => SetValue(IndicatorTemplateProperty, value); }
    /// <summary>Built-in indicator container outline; null means the source circular/full shape.</summary>
    public Geometry? IndicatorShape { get => GetValue(IndicatorShapeProperty); set => SetValue(IndicatorShapeProperty, value); }
    public Stretch IndicatorShapeStretch { get => GetValue(IndicatorShapeStretchProperty); set => SetValue(IndicatorShapeStretchProperty, value); }
    /// <summary>Built-in indicator container color; null uses the source color role for the indicator type.</summary>
    public IBrush? IndicatorContainerColor { get => GetValue(IndicatorContainerColorProperty); set => SetValue(IndicatorContainerColorProperty, value); }
    /// <summary>Built-in indicator foreground; null uses the source color role for the indicator type.</summary>
    public IBrush? IndicatorColor { get => GetValue(IndicatorColorProperty); set => SetValue(IndicatorColorProperty, value); }
    /// <summary>Built-in elevation level; null uses Level2 (Circular) or Level0 (Loading).</summary>
    public double? IndicatorElevation { get => GetValue(IndicatorElevationProperty); set => SetValue(IndicatorElevationProperty, value); }
    public ICommand? RefreshCommand { get => GetValue(RefreshCommandProperty); set => SetValue(RefreshCommandProperty, value); }
    public object? RefreshCommandParameter { get => GetValue(RefreshCommandParameterProperty); set => SetValue(RefreshCommandParameterProperty, value); }
    public double DistanceFraction => _distanceFraction;
    public event EventHandler<RoutedEventArgs>? RefreshRequested { add => AddHandler(RefreshRequestedEvent, value); remove => RemoveHandler(RefreshRequestedEvent, value); }

    // Upstream DragMultiplier = .5; tension saturates at twice the positional threshold.
    internal static double CalculateDistanceFraction(double pulled, double threshold)
    {
        var adjusted = Math.Max(0, pulled) * .5;
        if (adjusted <= threshold) return adjusted / threshold;
        var tension = Math.Clamp(adjusted / threshold - 1, 0, 2);
        return 1 + tension - tension * tension / 4;
    }

    private Control Build()
    {
        var content = new ContentPresenter { HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
        content[!ContentPresenter.ContentProperty] = this[!ContentProperty];
        content[!ContentPresenter.ContentTemplateProperty] = this[!ContentTemplateProperty];
        _builtInPanel = new Grid();
        _indicator = new MaterialRefreshIndicatorBox
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
        };
        _indicator.Content = _builtInPanel;
        _customIndicator = new ContentPresenter
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Top,
            IsHitTestVisible = false,
        };
        BuildIndicator();
        ApplyIndicatorMode();
        ApplyFraction(_distanceFraction);
        ApplyFade(IsRefreshing ? 1 : 0);
        return new Grid { Children = { content, _indicator, _customIndicator } };
    }

    private void BuildIndicator()
    {
        var loading = IndicatorType == MaterialRefreshIndicator.Loading;
        _rotation.Angle = 0;
        _pull = loading ? new LoadingIndicator { IsIndeterminate = false, ShowContainer = true,
            RenderTransform = _rotation, RenderTransformOrigin = RelativePoint.Center } : null;
        _arrow = loading ? null : new RefreshArrowIndicator();
        _pullVisual = (Control?)_pull ?? _arrow!;
        _busyVisual = loading ? new LoadingIndicator { IsIndeterminate = true, ShowContainer = true }
            : new CircularProgressIndicator { Width = 16, Height = 16, MinWidth = 0, MinHeight = 0,
                IsIndeterminate = true, StrokeThickness = 2.5, ShowTrack = false };
        if (_builtInPanel is not null)
        {
            _builtInPanel.Children.Clear();
            _builtInPanel.Children.Add(_pullVisual);
            _builtInPanel.Children.Add(_busyVisual);
        }
        ApplyIndicatorAppearance();
        ApplyFraction(_distanceFraction);
        ApplyFade(_fade);
    }

    /// <summary>
    /// Source <c>PullToRefreshDefaults.Indicator</c>/<c>LoadingIndicator</c> colors. A local
    /// override wins; otherwise the color role follows the selected indicator type.
    /// </summary>
    private void ApplyIndicatorAppearance()
    {
        if (_indicator is null) return;
        var loading = IndicatorType == MaterialRefreshIndicator.Loading;
        _indicator.MaxDistance = MaxDistance;
        _indicator.ContainerShape = IndicatorShape;
        _indicator.ContainerShapeStretch = IndicatorShapeStretch;
        _indicator.Width = _indicator.Height = loading ? 48 : 40;
        _indicator.Elevation = IndicatorElevation ?? (loading ? 0 : 2);
        _indicator.IsRefreshing = IsRefreshing;

        var container = ResolveBrush(IndicatorContainerColor,
            loading ? "MaterialPrimaryContainerBrush" : "MaterialSurfaceContainerHighBrush");
        var color = ResolveBrush(IndicatorColor,
            loading ? "MaterialOnPrimaryContainerBrush" : "MaterialOnSurfaceVariantBrush");
        _indicator.Background = container;
        _indicator.Foreground = color;
        if (_pull is not null)
        {
            _pull.Foreground = color;
            _pull.ContainerBrush = container;
        }
        else if (_arrow is not null) _arrow.Foreground = color;
        switch (_busyVisual)
        {
            case LoadingIndicator loadingBusy:
                loadingBusy.Foreground = color;
                loadingBusy.ContainerBrush = container;
                break;
            case CircularProgressIndicator circularBusy:
                circularBusy.Foreground = color;
                break;
        }
    }

    private IBrush? ResolveBrush(IBrush? custom, string resourceKey) =>
        custom ?? (this.TryFindResource(resourceKey, out var value) ? value as IBrush : null);

    private void ApplyIndicatorMode()
    {
        if (_indicator is null || _customIndicator is null) return;
        var content = IndicatorContent;
        var template = IndicatorTemplate;
        var custom = content is not null || template is not null;
        if (!custom)
        {
            _customIndicator.ContentTemplate = null;
            _customIndicator.Content = null;
        }
        else if (content is not null)
        {
            _customIndicator.ContentTemplate = template;
            _customIndicator.Content = content;
        }
        else
        {
            // Build the template with this control as data instead of making it the presenter's
            // visual content: a control cannot be added as its own descendant.
            _customIndicator.ContentTemplate = null;
            var built = template!.Build(this);
            if (built is not null) built.DataContext = this;
            _customIndicator.Content = built;
        }
        _customIndicator.IsVisible = custom;
        _indicator.IsVisible = !custom;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        _peer?.NotifyChanged(change);
        if (change.Property == IsRefreshingProperty)
        {
            if (_indicator is not null) _indicator.IsRefreshing = IsRefreshing;
            Settle(IsRefreshing ? 1 : 0);
            _crossfade ??= MaterialMotion.Animate(this, _fade, IsRefreshing ? 1 : 0, ApplyFade, MaterialSpring.EffectsDefault);
            _crossfade.Retarget(IsRefreshing ? 1 : 0);
        }
        // The source progress derives from the pulled distance and the positional threshold;
        // once released, the state animation owns the fraction until it settles.
        if (change.Property == ThresholdProperty && _pointer is not null && _settle?.IsRunning != true)
            ApplyFraction(CalculateDistanceFraction(_distancePulled, Threshold));
        if (change.Property == IndicatorTypeProperty && _builtInPanel is not null)
        {
            BuildIndicator();
            ApplyIndicatorMode();
        }
        if (change.Property == IndicatorContentProperty || change.Property == IndicatorTemplateProperty)
            ApplyIndicatorMode();
        if (change.Property == MaxDistanceProperty && _indicator is not null)
            _indicator.MaxDistance = MaxDistance;
        if (change.Property == IndicatorShapeProperty || change.Property == IndicatorShapeStretchProperty ||
            change.Property == IndicatorContainerColorProperty || change.Property == IndicatorColorProperty ||
            change.Property == IndicatorElevationProperty)
            ApplyIndicatorAppearance();
        if ((change.Property == IsEffectivelyEnabledProperty && !IsEffectivelyEnabled) ||
            (change.Property == IsVisibleProperty && !IsEffectivelyVisible)) CancelGesture();
    }

    private void ApplyFade(double value)
    {
        _fade = value;
        if (_busyVisual is null || _pullVisual is null) return;
        _busyVisual.Opacity = Math.Clamp(value, 0, 1);
        _busyVisual.IsVisible = IsRefreshing || value > 0;
        _pullVisual.Opacity = 1 - Math.Clamp(value, 0, 1);
        _pullVisual.IsVisible = !IsRefreshing || value < 1;
    }

    private void ApplyFraction(double value)
    {
        SetAndRaise(DistanceFractionProperty, ref _distanceFraction, value);
        if (_indicator is not null)
        {
            _indicator.DistanceFraction = value;
            _indicator.IsRefreshing = IsRefreshing;
        }
        if (_pull is not null) _pull.Progress = value;
        if (_arrow is not null) _arrow.Progress = value;
        _rotation.Angle = value > 1 ? -(value - 1) * 180 : 0;
    }

    private void Settle(double target)
    {
        _distancePulled = 0;
        // PullToRefreshStateImpl uses Animatable's default spring (1500/1), not theme spatial.
        _settle ??= MaterialMotion.Animate(this, _distanceFraction, target, ApplyFraction, 1500, 1);
        _settle.Retarget(target);
    }

    private void Pressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsEffectivelyEnabled || IsRefreshing || _settle?.IsRunning == true || _pointer is not null ||
            (e.Pointer.Type == PointerType.Mouse && (!AllowMousePull || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed))) return;
        _pointer = e.Pointer;
        _start = _last = e.GetPosition(this);
        _scroller = e.Source as ScrollViewer ?? (e.Source as Visual)?.FindAncestorOfType<ScrollViewer>();
        _scrollPresenter = _scroller?.GetVisualDescendants().OfType<ScrollContentPresenter>().FirstOrDefault();
        _distancePulled = 0;
    }

    private void Moved(object? sender, PointerEventArgs e)
    {
        if (e.Pointer != _pointer || e.Pointer.Type != PointerType.Mouse || IsRefreshing || !IsEffectivelyEnabled) return;
        var point = e.GetPosition(this);
        var delta = point.Y - _last.Y;
        _last = point;
        if (!_dragging)
        {
            if (Math.Abs(point.X - _start.X) > Math.Abs(point.Y - _start.Y)) return;
            if (point.Y - _start.Y < 4 || (_scroller?.Offset.Y ?? 0) > 0) return;
            _dragging = true;
            _settle?.Dispose(); _settle = null;
            e.Pointer.Capture(this);
            e.PreventGestureRecognition();
        }
        // Upward movement first collapses the pull; downward movement first consumes native
        // scroll offset. Only the residual is offered to the refresh state.
        if (delta > 0 && _scroller is { } scroll && scroll.Offset.Y > 0)
        {
            var consumed = Math.Min(delta, scroll.Offset.Y);
            scroll.Offset = new Vector(scroll.Offset.X, scroll.Offset.Y - consumed);
            delta -= consumed;
        }
        var next = Math.Max(0, _distancePulled + delta);
        var remaining = delta - (next - _distancePulled);
        _distancePulled = next;
        ApplyFraction(CalculateDistanceFraction(next, Threshold));
        if (remaining < 0 && _scroller is { } scroller)
            scroller.Offset = new Vector(scroller.Offset.X, scroller.Offset.Y - remaining);
        e.Handled = true;
    }

    private void GesturePreScroll(object? sender, ScrollGestureEventArgs e)
    {
        _pendingGesture = null;
        if (_pointer is null || _pointer.Type == PointerType.Mouse || IsRefreshing ||
            !IsEffectivelyEnabled || _settle?.IsRunning == true) return;
        _pendingGesture = e;
        _gestureOffset = _scroller?.Offset.Y ?? 0;
        // Upward drag first collapses the indicator, before native content consumes the rest.
        if (e.Delta.Y > 0 && _distancePulled > 0)
        {
            var consumed = Math.Min(e.Delta.Y, _distancePulled);
            _distancePulled -= consumed;
            ApplyFraction(CalculateDistanceFraction(_distancePulled, Threshold));
            e.Handled = true;
            if (_scroller is { } scroll)
            {
                var residual = e.Delta.Y - consumed;
                scroll.SetCurrentValue(ScrollViewer.OffsetProperty,
                    new Vector(scroll.Offset.X, scroll.Offset.Y + residual / ScrollUnitPixels()));
            }
        }
    }

    private void GesturePostScroll(object? sender, ScrollGestureEventArgs e)
    {
        if (!ReferenceEquals(_pendingGesture, e)) return;
        _pendingGesture = null;
        if (e.Delta.Y >= 0) return;
        // Avalonia reports the full delta even when the child hits its edge. Subtract the
        // offset actually consumed by the native scroller; only the remainder is a pull.
        var nativeConsumed = Math.Max(0, _gestureOffset - (_scroller?.Offset.Y ?? 0)) * ScrollUnitPixels();
        var remaining = Math.Max(0, -e.Delta.Y - nativeConsumed);
        if (remaining <= 0) return;
        _dragging = true;
        _distancePulled += remaining;
        ApplyFraction(CalculateDistanceFraction(_distancePulled, Threshold));
        e.Handled = true;
    }

    private double ScrollUnitPixels()
    {
        if (_scrollPresenter is { } presenter &&
            presenter.Viewport.Height > 0)
            return presenter.Bounds.Height / presenter.Viewport.Height;
        return 1;
    }

    private void Released(object? sender, PointerReleasedEventArgs e)
    {
        if (e.Pointer != _pointer) return;
        _pendingGesture = null;
        var generation = ++_releaseGeneration;
        var dragged = _dragging;
        var trigger = dragged && !IsRefreshing && _distancePulled * .5 > Threshold;
        _pointer = null; _dragging = false;
        if (!dragged) return;
        e.Pointer.Capture(null);
        e.Handled = true;
        if (trigger)
        {
            RaiseEvent(new RoutedEventArgs(RefreshRequestedEvent));
            if (RefreshCommand is { } command && command.CanExecute(RefreshCommandParameter)) command.Execute(RefreshCommandParameter);
            // Like source withFrameNanos: allow externally bound IsRefreshing to update first.
            if (TopLevel.GetTopLevel(this) is { } root)
                root.RequestAnimationFrame(_ =>
                {
                    if (generation == _releaseGeneration && this.IsAttachedToVisualTree()) Settle(IsRefreshing ? 1 : 0);
                });
            else Settle(IsRefreshing ? 1 : 0);
        }
        else Settle(IsRefreshing ? 1 : 0);
    }

    private void CaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (_pointer == e.Pointer) CancelGesture();
    }

    private void CancelGesture()
    {
        var pointer = _pointer;
        _releaseGeneration++;
        _pendingGesture = null;
        _pointer = null; _dragging = false; _distancePulled = 0;
        if (pointer?.Captured == this) pointer.Capture(null);
        _settle?.Dispose(); _settle = null;
        _crossfade?.Dispose(); _crossfade = null;
        ApplyFraction(IsRefreshing ? 1 : 0);
        ApplyFade(IsRefreshing ? 1 : 0);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => _peer = new RefreshAutomationPeer(this);
    private sealed class RefreshAutomationPeer(MaterialPullToRefresh owner) : ControlAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(MaterialPullToRefresh);
        protected override string? GetItemStatusCore() => AutomationProperties.GetItemStatus(owner) ?? (owner.IsRefreshing ? "Refreshing" : null);
        internal void NotifyChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == IsRefreshingProperty)
                RaisePropertyChangedEvent(AutomationElementIdentifiers.ItemStatusProperty, e.OldValue is true ? "Refreshing" : null, GetItemStatusCore());
        }
    }
}
