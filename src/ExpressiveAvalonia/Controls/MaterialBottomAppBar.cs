using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Controls;

/// <summary>The classic fixed bottom app bar and the flexible (docked toolbar) arrangement.</summary>
public enum MaterialBottomAppBarConfiguration
{
    /// <summary>80-unit bar with a FAB slot, Start-arranged actions and 4-unit content padding.</summary>
    Classic,
    /// <summary>64-unit minimum bar with 16-unit horizontal padding and SpaceBetween actions.</summary>
    Flexible
}

/// <summary>
/// Material bottom app bar. Classic form: 80 units, leading actions and an optional trailing FAB.
/// Flexible form: an expandedHeight (64 minimum) docked toolbar with configurable arrangement.
/// </summary>
/// <remarks>
/// <para>
/// Source mapping: <c>BottomAppBar</c> (80 units, BottomAppBarTokens.ContainerColor/Height,
/// content padding 4, Start arrangement, tonal elevation 0), its FAB overload (8/12 FAB padding)
/// and <c>FlexibleBottomAppBar</c> (DockedToolbarTokens height 64, SurfaceContainer container,
/// 16-unit horizontal padding, SpaceBetween arrangement). <see cref="MaterialAppBarScrollBehavior"/>
/// with <see cref="MaterialBarScrollMode.ExitAlways"/> reproduces the bottom bar's exit-always
/// behavior, including dragging the bar itself to expand or collapse it.
/// </para>
/// <para>
/// Extensions: the classic FAB slot is exposed in both forms, <see cref="HorizontalArrangement"/>
/// can override the source default, and <see cref="SafeAreaPadding"/> carries desktop host insets.
/// </para>
/// </remarks>
public class MaterialBottomAppBar : MaterialBarBase
{
    /// <summary>Content shown in the trailing floating-action-button slot.</summary>
    public static readonly StyledProperty<object?> FabProperty =
        AvaloniaProperty.Register<MaterialBottomAppBar, object?>(nameof(Fab));

    public static readonly StyledProperty<bool> IsFabVisibleProperty =
        AvaloniaProperty.Register<MaterialBottomAppBar, bool>(nameof(IsFabVisible), true);

    public static readonly StyledProperty<MaterialBottomAppBarConfiguration> ConfigurationProperty =
        AvaloniaProperty.Register<MaterialBottomAppBar, MaterialBottomAppBarConfiguration>(
            nameof(Configuration), MaterialBottomAppBarConfiguration.Classic);

    /// <summary>Expanded height in DIPs. NaN selects 80 for classic and 64 for flexible.</summary>
    public static readonly StyledProperty<double> ExpandedHeightProperty =
        AvaloniaProperty.Register<MaterialBottomAppBar, double>(nameof(ExpandedHeight), double.NaN);

    /// <summary>Content padding. NaN components select the configuration default.</summary>
    public static readonly StyledProperty<Thickness> ContentPaddingProperty =
        AvaloniaProperty.Register<MaterialBottomAppBar, Thickness>(
            nameof(ContentPadding), new Thickness(double.NaN));

    /// <summary>Action distribution; null selects Start (classic) or SpaceBetween (flexible).</summary>
    public static readonly StyledProperty<MaterialBarArrangement?> HorizontalArrangementProperty =
        AvaloniaProperty.Register<MaterialBottomAppBar, MaterialBarArrangement?>(
            nameof(HorizontalArrangement));

    /// <summary>Desktop safe-area padding; the bottom inset is consumed by the bar.</summary>
    public static readonly StyledProperty<Thickness> SafeAreaPaddingProperty =
        AvaloniaProperty.Register<MaterialBottomAppBar, Thickness>(nameof(SafeAreaPadding));

    /// <summary>Hoisted exit-always scroll behavior; null keeps the bar fixed.</summary>
    public static readonly StyledProperty<MaterialAppBarScrollBehavior?> ScrollBehaviorProperty =
        AvaloniaProperty.Register<MaterialBottomAppBar, MaterialAppBarScrollBehavior?>(nameof(ScrollBehavior));

    /// <summary>Native scroll viewer that drives the behavior.</summary>
    public static readonly StyledProperty<ScrollViewer?> ScrollSourceProperty =
        AvaloniaProperty.Register<MaterialBottomAppBar, ScrollViewer?>(nameof(ScrollSource));

    public static readonly DirectProperty<MaterialBottomAppBar, bool> HasFabProperty =
        AvaloniaProperty.RegisterDirect<MaterialBottomAppBar, bool>(
            nameof(HasFab), bar => bar.HasFab, (bar, value) => bar.HasFab = value);

    public static readonly DirectProperty<MaterialBottomAppBar, Thickness> EffectiveContentPaddingProperty =
        AvaloniaProperty.RegisterDirect<MaterialBottomAppBar, Thickness>(
            nameof(EffectiveContentPadding), bar => bar.EffectiveContentPadding);

    public static readonly DirectProperty<MaterialBottomAppBar, MaterialBarArrangement> EffectiveArrangementProperty =
        AvaloniaProperty.RegisterDirect<MaterialBottomAppBar, MaterialBarArrangement>(
            nameof(EffectiveArrangement), bar => bar.EffectiveArrangement);

    public static readonly DirectProperty<MaterialBottomAppBar, double> EffectiveExpandedHeightProperty =
        AvaloniaProperty.RegisterDirect<MaterialBottomAppBar, double>(
            nameof(EffectiveExpandedHeight), bar => bar.EffectiveExpandedHeight);

    public static readonly DirectProperty<MaterialBottomAppBar, Thickness> EffectiveFabMarginProperty =
        AvaloniaProperty.RegisterDirect<MaterialBottomAppBar, Thickness>(
            nameof(EffectiveFabMargin), bar => bar.EffectiveFabMargin);

    private bool _hasFab;
    private Thickness _effectiveContentPadding;
    private MaterialBarArrangement _effectiveArrangement;
    private double _effectiveExpandedHeight = 80;
    private Thickness _effectiveFabMargin;
    private MaterialBarScrollAdapter? _adapter;
    private MaterialAppBarState? _state;
    private bool _dragging;
    private double _dragLastY;
    private double _dragVelocity;
    private ulong _dragLastTimestamp;

    static MaterialBottomAppBar()
    {
        FabProperty.Changed.AddClassHandler<MaterialBottomAppBar>((bar, _) => bar.UpdateFab());
        IsFabVisibleProperty.Changed.AddClassHandler<MaterialBottomAppBar>((bar, _) => bar.UpdateFab());
        ConfigurationProperty.Changed.AddClassHandler<MaterialBottomAppBar>((bar, _) => bar.UpdateConfiguration());
        ExpandedHeightProperty.Changed.AddClassHandler<MaterialBottomAppBar>((bar, _) => bar.UpdateConfiguration());
        ContentPaddingProperty.Changed.AddClassHandler<MaterialBottomAppBar>((bar, _) => bar.UpdateConfiguration());
        HorizontalArrangementProperty.Changed.AddClassHandler<MaterialBottomAppBar>((bar, _) => bar.UpdateConfiguration());
        SafeAreaPaddingProperty.Changed.AddClassHandler<MaterialBottomAppBar>((bar, _) =>
        {
            bar.UpdateStateLimit();
            bar.InvalidateMeasure();
        });
        ScrollBehaviorProperty.Changed.AddClassHandler<MaterialBottomAppBar>((bar, args) =>
            bar.OnScrollBehaviorChanged(args.GetOldValue<MaterialAppBarScrollBehavior?>(),
                args.GetNewValue<MaterialAppBarScrollBehavior?>()));
        ScrollSourceProperty.Changed.AddClassHandler<MaterialBottomAppBar>((bar, _) => bar.UpdateAdapter());
    }

    public MaterialBottomAppBar() => UpdateConfiguration();

    public object? Fab
    {
        get => GetValue(FabProperty);
        set => SetValue(FabProperty, value);
    }

    public bool IsFabVisible
    {
        get => GetValue(IsFabVisibleProperty);
        set => SetValue(IsFabVisibleProperty, value);
    }

    public MaterialBottomAppBarConfiguration Configuration
    {
        get => GetValue(ConfigurationProperty);
        set => SetValue(ConfigurationProperty, value);
    }

    public double ExpandedHeight
    {
        get => GetValue(ExpandedHeightProperty);
        set => SetValue(ExpandedHeightProperty, value);
    }

    public Thickness ContentPadding
    {
        get => GetValue(ContentPaddingProperty);
        set => SetValue(ContentPaddingProperty, value);
    }

    public MaterialBarArrangement? HorizontalArrangement
    {
        get => GetValue(HorizontalArrangementProperty);
        set => SetValue(HorizontalArrangementProperty, value);
    }

    public Thickness SafeAreaPadding
    {
        get => GetValue(SafeAreaPaddingProperty);
        set => SetValue(SafeAreaPaddingProperty, value);
    }

    public MaterialAppBarScrollBehavior? ScrollBehavior
    {
        get => GetValue(ScrollBehaviorProperty);
        set => SetValue(ScrollBehaviorProperty, value);
    }

    public ScrollViewer? ScrollSource
    {
        get => GetValue(ScrollSourceProperty);
        set => SetValue(ScrollSourceProperty, value);
    }

    /// <summary>True when <see cref="Fab"/> is set and <see cref="IsFabVisible"/> is enabled.</summary>
    public bool HasFab
    {
        get => _hasFab;
        private set => SetAndRaise(HasFabProperty, ref _hasFab, value);
    }

    public Thickness EffectiveContentPadding => _effectiveContentPadding;

    public MaterialBarArrangement EffectiveArrangement => _effectiveArrangement;

    public double EffectiveExpandedHeight => _effectiveExpandedHeight;

    /// <summary>
    /// Trailing FAB slot margin: the source's FAB box padding (8 above, 12 before the edge) added
    /// to the resolved content padding because the slot lives outside the actions row.
    /// </summary>
    public Thickness EffectiveFabMargin => _effectiveFabMargin;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        UpdateAdapter();
        SubscribeState();
        UpdateConfiguration();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FlowDirectionProperty)
        {
            PseudoClasses.Set(":rtl", FlowDirection == FlowDirection.RightToLeft);
            UpdateConfiguration();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateAdapter();
        SubscribeState();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _adapter?.Dispose();
        _adapter = null;
        UnsubscribeState();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var baseSize = base.MeasureOverride(availableSize);
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : baseSize.Width;
        var height = GetBarHeight();
        if (double.IsFinite(availableSize.Height))
            height = Math.Min(height, availableSize.Height);
        return new Size(width, height);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (ScrollBehavior is not { Mode: MaterialBarScrollMode.ExitAlways })
            return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        if (IsInteractiveSource(e.Source))
            return;
        _dragging = true;
        _dragLastY = e.GetPosition(this).Y;
        _dragVelocity = 0;
        _dragLastTimestamp = e.Timestamp;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_dragging || ScrollBehavior is not { } behavior)
            return;
        var y = e.GetPosition(this).Y;
        var delta = y - _dragLastY;
        _dragLastY = y;
        var dt = Math.Max(1, e.Timestamp - _dragLastTimestamp) / 1000d;
        _dragLastTimestamp = e.Timestamp;
        _dragVelocity = delta / dt;
        behavior.CancelSettle();
        // BottomAppBarLayout: rememberDraggableState { state.heightOffset -= delta }.
        behavior.State.HeightOffset -= delta;
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_dragging)
            return;
        _dragging = false;
        e.Pointer.Capture(null);
        ScrollBehavior?.OnPostFling(_dragVelocity, this);
        _dragVelocity = 0;
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _dragging = false;
    }

    private static bool IsInteractiveSource(object? source)
    {
        var visual = source as Visual;
        while (visual is not null)
        {
            if (visual is Button or ToggleButton or TextBox or ComboBox
                or Avalonia.Controls.Primitives.RangeBase or ScrollBar)
                return true;
            visual = visual.GetVisualParent();
        }
        return false;
    }

    private double GetBarHeight()
    {
        var offset = ScrollBehavior?.State.HeightOffset ?? 0;
        // BottomAppBarLayout: placeable.height (container + window insets) plus the offset,
        // coerced at zero; the whole bar including the safe area can leave the screen.
        return Math.Max(0, _effectiveExpandedHeight + SafeAreaPadding.Top + SafeAreaPadding.Bottom + offset);
    }

    private void UpdateFab() => HasFab = IsFabVisible && Fab is not null;

    private void UpdateConfiguration()
    {
        var flexible = Configuration == MaterialBottomAppBarConfiguration.Flexible;
        var defaultHeight = flexible ? 64 : 80;
        _effectiveExpandedHeight = double.IsFinite(ExpandedHeight) && ExpandedHeight > 0
            ? ExpandedHeight
            : defaultHeight;

        var padding = ContentPadding;
        var defaultPadding = flexible
            ? new Thickness(16, 0, 16, 0)
            : new Thickness(4, 4, 4, 0);
        _effectiveContentPadding = new Thickness(
            double.IsNaN(padding.Left) ? defaultPadding.Left : padding.Left,
            double.IsNaN(padding.Top) ? defaultPadding.Top : padding.Top,
            double.IsNaN(padding.Right) ? defaultPadding.Right : padding.Right,
            double.IsNaN(padding.Bottom) ? defaultPadding.Bottom : padding.Bottom);

        _effectiveArrangement = HorizontalArrangement
            ?? (flexible ? MaterialBarArrangement.SpaceBetween : MaterialBarArrangement.Start);

        // FABVerticalPadding = 12 - BottomAppBarVerticalPadding (4); FABHorizontalPadding =
        // 16 - BottomAppBarHorizontalPadding (4). The slot is a sibling of the padded row, so the
        // content padding is folded into its margin. The slot follows the end edge, so the
        // horizontal inset mirrors with the flow direction.
        var fabTop = _effectiveContentPadding.Top + 8;
        var fabInline = _effectiveContentPadding.Right + 12;
        _effectiveFabMargin = FlowDirection == FlowDirection.RightToLeft
            ? new Thickness(_effectiveContentPadding.Left + 12, fabTop, 0, 0)
            : new Thickness(0, fabTop, fabInline, 0);

        RaisePropertyChanged(EffectiveExpandedHeightProperty, 0d, _effectiveExpandedHeight);
        RaisePropertyChanged(EffectiveContentPaddingProperty, default(Thickness), _effectiveContentPadding);
        RaisePropertyChanged(EffectiveFabMarginProperty, default(Thickness), _effectiveFabMargin);
        RaisePropertyChanged(EffectiveArrangementProperty, MaterialBarArrangement.Start, _effectiveArrangement);

        UpdateStateLimit();
        InvalidateMeasure();
    }

    private void UpdateStateLimit()
    {
        if (_state is null)
            return;
        _state.HeightOffsetLimit =
            -(_effectiveExpandedHeight + SafeAreaPadding.Top + SafeAreaPadding.Bottom);
        _state.HeightOffset = _state.HeightOffset;
    }

    private void OnScrollBehaviorChanged(MaterialAppBarScrollBehavior? oldValue,
        MaterialAppBarScrollBehavior? newValue)
    {
        oldValue?.CancelSettle();
        UnsubscribeState();
        UpdateAdapter();
        SubscribeState();
        InvalidateMeasure();
    }

    private void SubscribeState()
    {
        var state = ScrollBehavior?.State;
        if (ReferenceEquals(_state, state))
            return;
        _state = state;
        if (_state is not null)
        {
            _state.PropertyChanged += OnStatePropertyChanged;
            UpdateStateLimit();
        }
        InvalidateMeasure();
    }

    private void UnsubscribeState()
    {
        if (_state is not null)
            _state.PropertyChanged -= OnStatePropertyChanged;
        _state = null;
    }

    private void OnStatePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == MaterialAppBarState.HeightOffsetProperty
            || e.Property == MaterialAppBarState.HeightOffsetLimitProperty)
            InvalidateMeasure();
    }

    private void UpdateAdapter()
    {
        _adapter?.Dispose();
        _adapter = null;
        if (ScrollBehavior is not { } behavior)
            return;
        _adapter = new MaterialBarScrollAdapter(this, behavior);
        _adapter.Attach(ScrollSource);
    }
}