using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Theming;
using ExpressiveAvalonia.Tokens;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// Hoisted state for a <see cref="MaterialScrollField"/>, mirroring the source
/// <c>ScrollFieldState</c>.
/// </summary>
/// <remarks>
/// The field is backed by an effectively infinite page range. <see cref="SelectedOption"/> is the
/// page closest to the snap position modulo <see cref="ItemCount"/>; <see cref="TargetOption"/>
/// changes as soon as a scroll target is chosen, before the animation settles.
/// </remarks>
public sealed class MaterialScrollFieldState : AvaloniaObject
{
    /// <summary>The number of pages the source pager exposes.</summary>
    public const int InfinitePageCount = 100_000;

    private readonly int _itemCount;
    private int _selectedOption;
    private int _targetOption;
    private bool _isScrollInProgress;
    private int _contextPage;
    private int _targetPage;

    /// <summary>Identifies the <see cref="SelectedOption"/> property.</summary>
    public static readonly DirectProperty<MaterialScrollFieldState, int> SelectedOptionProperty =
        AvaloniaProperty.RegisterDirect<MaterialScrollFieldState, int>(
            nameof(SelectedOption), state => state.SelectedOption,
            (state, value) => state.RequestSelectedOption(value));

    /// <summary>Identifies the <see cref="TargetOption"/> property.</summary>
    public static readonly DirectProperty<MaterialScrollFieldState, int> TargetOptionProperty =
        AvaloniaProperty.RegisterDirect<MaterialScrollFieldState, int>(
            nameof(TargetOption), state => state.TargetOption);

    /// <summary>Identifies the <see cref="IsScrollInProgress"/> property.</summary>
    public static readonly DirectProperty<MaterialScrollFieldState, bool> IsScrollInProgressProperty =
        AvaloniaProperty.RegisterDirect<MaterialScrollFieldState, bool>(
            nameof(IsScrollInProgress), state => state.IsScrollInProgress);

    /// <summary>Identifies the immutable <see cref="ItemCount"/> property.</summary>
    public static readonly DirectProperty<MaterialScrollFieldState, int> ItemCountProperty =
        AvaloniaProperty.RegisterDirect<MaterialScrollFieldState, int>(
            nameof(ItemCount), state => state.ItemCount);

    /// <summary>Creates a state for <paramref name="itemCount"/> distinct options.</summary>
    /// <param name="itemCount">Number of unique options. Must be greater than zero.</param>
    /// <param name="index">Initial option, clamped to the valid range.</param>
    public MaterialScrollFieldState(int itemCount, int index = 0)
    {
        if (itemCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(itemCount), "itemCount must be greater than 0");
        _itemCount = itemCount;
        _contextPage = InitialPage(itemCount, index);
        _targetPage = _contextPage;
        _selectedOption = Mod(_contextPage, itemCount);
        _targetOption = _selectedOption;
    }

    /// <summary>The number of unique options in this wheel.</summary>
    public int ItemCount => _itemCount;

    /// <summary>
    /// The option closest to the current snap position. Assigning a value clamps it and requests
    /// an immediate scroll that preserves the current page context.
    /// </summary>
    public int SelectedOption
    {
        get => _selectedOption;
        set => SetValue(SelectedOptionProperty, value);
    }

    /// <summary>The option the wheel is travelling towards, or the selected option at rest.</summary>
    public int TargetOption => _targetOption;

    /// <summary>Whether a user gesture, keyboard action or snap animation is running.</summary>
    public bool IsScrollInProgress => _isScrollInProgress;

    /// <summary>The internal pager page, expressed modulo <see cref="ItemCount"/>.</summary>
    internal int ContextPage => _contextPage;

    /// <summary>The internal target page.</summary>
    internal int TargetPage => _targetPage;

    internal MaterialScrollField? Owner { get; set; }

    /// <summary>Instantly scrolls to a clamped option.</summary>
    public void ScrollToOption(int option)
    {
        var target = CalculateTargetPage(Math.Clamp(option, 0, _itemCount - 1));
        if (Owner is { } owner)
            owner.RequestScroll(target, animate: false);
        else
            SetPages(target, target, false);
    }

    /// <summary>Animates to a clamped option using the wheel's snap spring.</summary>
    public void AnimateScrollToOption(int option)
    {
        var target = CalculateTargetPage(Math.Clamp(option, 0, _itemCount - 1));
        if (Owner is { } owner)
            owner.RequestScroll(target, animate: true);
        else
            SetPages(target, target, false);
    }

    internal int CalculateTargetPage(int option)
    {
        var currentContextPage = _contextPage;
        var currentOption = Mod(currentContextPage, _itemCount);
        return currentContextPage + (option - currentOption);
    }

    internal void SetPages(int contextPage, int targetPage, bool inProgress)
    {
        contextPage = Math.Clamp(contextPage, 0, InfinitePageCount - 1);
        targetPage = Math.Clamp(targetPage, 0, InfinitePageCount - 1);
        // PagerState.targetPage returns currentPage whenever no scroll is in progress, so an idle
        // publish can never retain a destination.
        if (!inProgress)
            targetPage = contextPage;
        if (_contextPage != contextPage)
        {
            _contextPage = contextPage;
            var selected = Mod(contextPage, _itemCount);
            if (selected != _selectedOption)
                SetAndRaise(SelectedOptionProperty, ref _selectedOption, selected);
        }
        if (_targetPage != targetPage)
        {
            _targetPage = targetPage;
            var target = Mod(targetPage, _itemCount);
            if (target != _targetOption)
                SetAndRaise(TargetOptionProperty, ref _targetOption, target);
        }
        if (_isScrollInProgress != inProgress)
            SetAndRaise(IsScrollInProgressProperty, ref _isScrollInProgress, inProgress);
    }

    internal void SetTargetPage(int targetPage)
    {
        targetPage = Math.Clamp(targetPage, 0, InfinitePageCount - 1);
        if (_targetPage == targetPage)
            return;
        _targetPage = targetPage;
        var target = Mod(targetPage, _itemCount);
        if (target != _targetOption)
            SetAndRaise(TargetOptionProperty, ref _targetOption, target);
    }

    internal void SetInProgress(bool inProgress)
    {
        // Restore the at-rest invariant before publishing idle; every cancellation path routes
        // through this method so disable, detach, lost capture and snap completion cannot diverge.
        if (!inProgress)
            ReconcileTarget();
        if (_isScrollInProgress == inProgress)
            return;
        SetAndRaise(IsScrollInProgressProperty, ref _isScrollInProgress, inProgress);
    }

    /// <summary>
    /// Restores the source's at-rest contract: <c>targetPage</c> is <c>currentPage</c> when no
    /// scroll is in progress.
    /// </summary>
    /// <returns><see langword="true"/> when the internal target page changed.</returns>
    internal bool ReconcileTarget()
    {
        if (_targetPage == _contextPage)
            return false;
        _targetPage = _contextPage;
        var target = Mod(_contextPage, _itemCount);
        if (target != _targetOption)
            SetAndRaise(TargetOptionProperty, ref _targetOption, target);
        return true;
    }

    internal static int InitialPage(int itemCount, int index)
    {
        var coerced = Math.Clamp(index, 0, itemCount - 1);
        return InfinitePageCount / 2 - InfinitePageCount / 2 % itemCount + coerced;
    }

    internal static int Mod(int value, int modulus) => (value % modulus + modulus) % modulus;

    private void RequestSelectedOption(int value)
    {
        if (_itemCount <= 0)
            return;
        var option = Math.Clamp(value, 0, _itemCount - 1);
        var target = CalculateTargetPage(option);
        if (Owner is { } owner)
            owner.RequestScroll(target, animate: false);
        else
            SetPages(target, target, false);
    }
}

/// <summary>One virtualized slot passed to a custom <see cref="MaterialScrollField.ItemTemplate"/>.</summary>
public sealed class MaterialScrollFieldItem : INotifyPropertyChanged
{
    private int _page;
    private int _index;
    private string _text = string.Empty;
    private bool _isSelected;
    private bool _isEnabled = true;

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The absolute pager page (0 to 99,999).</summary>
    public int Page => _page;

    /// <summary>The option index (0 to <c>ItemCount - 1</c>).</summary>
    public int Index => _index;

    /// <summary>The formatted option text.</summary>
    public string Text => _text;

    /// <summary>Whether this slot is the currently snapped option.</summary>
    public bool IsSelected => _isSelected;

    /// <summary>Whether the field is enabled.</summary>
    public bool IsEnabled => _isEnabled;

    /// <inheritdoc />
    public override string ToString() => _text;

    internal void Update(int page, int index, string text, bool selected, bool enabled)
    {
        if (_page != page)
        {
            _page = page;
            Raise(nameof(Page));
        }
        if (_index != index)
        {
            _index = index;
            Raise(nameof(Index));
        }
        if (_text != text)
        {
            _text = text;
            Raise(nameof(Text));
        }
        if (_isSelected != selected)
        {
            _isSelected = selected;
            Raise(nameof(IsSelected));
        }
        if (_isEnabled != enabled)
        {
            _isEnabled = enabled;
            Raise(nameof(IsEnabled));
        }
    }

    private void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// A vertically scrolling selection wheel ported from Material 3 Expressive
/// <c>ScrollField.kt</c>.
/// </summary>
/// <remarks>
/// <para>
/// The control renders only the slots around the viewport, so a state with 100,000 pages still
/// creates a handful of visuals. Native Avalonia pointer, keyboard, focus and automation contracts
/// are preserved: drag and wheel scroll, Up/Down move one page, a tap selects a visible option and
/// automation exposes the full 0–99,999 pager range.
/// </para>
/// <para>
/// The default slot uses the source's fast-spatial typography and colour animation from
/// DisplayMedium to DisplayLargeEmphasized. Set <see cref="ItemTemplate"/> to replace the slot
/// content with a native Avalonia template, or <see cref="Formatter"/> to change only its text.
/// </para>
/// </remarks>
public sealed class MaterialScrollField : Control
{
    private const double DefaultHeight = 120;
    private const double DefaultWidth = 120;
    private const double FocusRingThickness = 2;
    private const double FocusRingGap = 2;
    private const double DragThreshold = 3;

    /// <summary>Identifies the <see cref="State"/> property.</summary>
    public static readonly StyledProperty<MaterialScrollFieldState?> StateProperty =
        AvaloniaProperty.Register<MaterialScrollField, MaterialScrollFieldState?>(
            nameof(State), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Identifies the <see cref="ItemHeight"/> property.</summary>
    public static readonly StyledProperty<double> ItemHeightProperty =
        AvaloniaProperty.Register<MaterialScrollField, double>(nameof(ItemHeight), 52,
            validate: value => double.IsFinite(value) && value > 0);

    /// <summary>Identifies the <see cref="CornerRadius"/> property.</summary>
    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<MaterialScrollField, CornerRadius>(nameof(CornerRadius),
            new CornerRadius(16));

    /// <summary>Identifies the <see cref="ContainerBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> ContainerBrushProperty =
        AvaloniaProperty.Register<MaterialScrollField, IBrush?>(nameof(ContainerBrush));

    /// <summary>Identifies the <see cref="ContentBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> ContentBrushProperty =
        AvaloniaProperty.Register<MaterialScrollField, IBrush?>(nameof(ContentBrush));

    /// <summary>Identifies the <see cref="SelectedContentBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> SelectedContentBrushProperty =
        AvaloniaProperty.Register<MaterialScrollField, IBrush?>(nameof(SelectedContentBrush));

    /// <summary>Identifies the <see cref="DisabledContainerBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> DisabledContainerBrushProperty =
        AvaloniaProperty.Register<MaterialScrollField, IBrush?>(nameof(DisabledContainerBrush));

    /// <summary>Identifies the <see cref="DisabledContentBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> DisabledContentBrushProperty =
        AvaloniaProperty.Register<MaterialScrollField, IBrush?>(nameof(DisabledContentBrush));

    /// <summary>Identifies the <see cref="DisabledSelectedContentBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> DisabledSelectedContentBrushProperty =
        AvaloniaProperty.Register<MaterialScrollField, IBrush?>(
            nameof(DisabledSelectedContentBrush));

    /// <summary>Identifies the <see cref="FocusRingBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> FocusRingBrushProperty =
        AvaloniaProperty.Register<MaterialScrollField, IBrush?>(nameof(FocusRingBrush));

    /// <summary>Identifies the <see cref="DisabledOpacity"/> property.</summary>
    public static readonly StyledProperty<double> DisabledOpacityProperty =
        AvaloniaProperty.Register<MaterialScrollField, double>(nameof(DisabledOpacity), 0.38,
            validate: value => double.IsFinite(value) && value is >= 0 and <= 1);

    /// <summary>Identifies the <see cref="ContentDescription"/> property.</summary>
    public static readonly StyledProperty<string?> ContentDescriptionProperty =
        AvaloniaProperty.Register<MaterialScrollField, string?>(nameof(ContentDescription));

    /// <summary>Identifies the <see cref="FieldAccessibilityDescription"/> property.</summary>
    public static readonly StyledProperty<Func<int, string>?> FieldAccessibilityDescriptionProperty =
        AvaloniaProperty.Register<MaterialScrollField, Func<int, string>?>(
            nameof(FieldAccessibilityDescription));

    /// <summary>Identifies the <see cref="Formatter"/> property.</summary>
    public static readonly StyledProperty<Func<int, string>?> FormatterProperty =
        AvaloniaProperty.Register<MaterialScrollField, Func<int, string>?>(nameof(Formatter));

    /// <summary>Identifies the <see cref="MinimumDigits"/> property.</summary>
    public static readonly StyledProperty<int> MinimumDigitsProperty =
        AvaloniaProperty.Register<MaterialScrollField, int>(nameof(MinimumDigits), 2,
            validate: value => value is >= 0 and <= 20);

    /// <summary>Identifies the <see cref="ItemTemplate"/> property.</summary>
    public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty =
        AvaloniaProperty.Register<MaterialScrollField, IDataTemplate?>(nameof(ItemTemplate));

    /// <summary>Identifies the <see cref="FontFamily"/> property.</summary>
    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        TextElement.FontFamilyProperty.AddOwner<MaterialScrollField>();

    private readonly Canvas _itemsHost;
    private readonly Dictionary<int, Slot> _slots = [];
    private readonly Dictionary<int, ContentControl> _templateItems = [];
    private readonly List<int> _removeBuffer = [];
    private MaterialScrollFieldState? _state;
    private MaterialScrollFieldAutomationPeer? _peer;
    private MaterialMotion.SpringAnimation? _scrollAnimation;
    private Pen? _focusPen;
    private IBrush? _focusPenBrush;
    private bool _renderInvalidationPosted;
    private int _formatVersion = 1;
    private double _offset;
    private double _scrollTarget;
    private int _slotFirst = int.MinValue;
    private int _slotLast = int.MinValue;
    private bool _pressed;
    private bool _dragging;
    private double _dragStartOffset;
    private double _dragStartY;
    private double _lastY;
    private double _velocity;
    private ulong _lastTimestamp;

    static MaterialScrollField()
    {
        AffectsRender<MaterialScrollField>(ContainerBrushProperty, ContentBrushProperty,
            SelectedContentBrushProperty, DisabledContainerBrushProperty, DisabledContentBrushProperty,
            DisabledSelectedContentBrushProperty, DisabledOpacityProperty, CornerRadiusProperty,
            FocusRingBrushProperty, ItemTemplateProperty, FormatterProperty, MinimumDigitsProperty,
            FontFamilyProperty);
        AffectsMeasure<MaterialScrollField>(ItemHeightProperty, ItemTemplateProperty);
        AffectsArrange<MaterialScrollField>(ItemHeightProperty, ItemTemplateProperty);
        FocusableProperty.OverrideDefaultValue<MaterialScrollField>(true);
    }

    /// <summary>Creates an empty field with a default ten-option state.</summary>
    public MaterialScrollField()
    {
        _itemsHost = new Canvas { IsHitTestVisible = false, ClipToBounds = true };
        VisualChildren.Add(_itemsHost);
        LogicalChildren.Add(_itemsHost);
        SetCurrentValue(StateProperty, new MaterialScrollFieldState(10));
        // Colour roles are resolved on each render. Observe live palette/theme changes so the
        // per-slot colour springs retarget while retaining their current position and velocity.
        ResourcesChanged += (_, _) => InvalidateVisual();
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    /// <summary>The hoisted wheel state. A default state is installed when none is supplied.</summary>
    public MaterialScrollFieldState? State
    {
        get => GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    /// <summary>The distance between adjacent item centres and the height of one slot.</summary>
    public double ItemHeight
    {
        get => GetValue(ItemHeightProperty);
        set => SetValue(ItemHeightProperty, value);
    }

    /// <summary>The container corner radius. The source uses ShapeDefaults.Large.</summary>
    public CornerRadius CornerRadius
    {
        get => GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    /// <summary>Container fill when enabled. Falls back to surfaceContainerLowest.</summary>
    public IBrush? ContainerBrush
    {
        get => GetValue(ContainerBrushProperty);
        set => SetValue(ContainerBrushProperty, value);
    }

    /// <summary>Unselected text colour when enabled. Falls back to outline.</summary>
    public IBrush? ContentBrush
    {
        get => GetValue(ContentBrushProperty);
        set => SetValue(ContentBrushProperty, value);
    }

    /// <summary>Selected text colour when enabled. Falls back to onSurface.</summary>
    public IBrush? SelectedContentBrush
    {
        get => GetValue(SelectedContentBrushProperty);
        set => SetValue(SelectedContentBrushProperty, value);
    }

    /// <summary>Container fill when disabled; when null the enabled fill is drawn at 38%.</summary>
    public IBrush? DisabledContainerBrush
    {
        get => GetValue(DisabledContainerBrushProperty);
        set => SetValue(DisabledContainerBrushProperty, value);
    }

    /// <summary>
    /// Disabled text colour for unselected slots. Falls back to
    /// <see cref="DisabledSelectedContentBrush"/>, then to the enabled unselected colour; when no
    /// disabled brush is supplied at all the enabled colours are drawn at
    /// <see cref="DisabledOpacity"/>.
    /// </summary>
    public IBrush? DisabledContentBrush
    {
        get => GetValue(DisabledContentBrushProperty);
        set => SetValue(DisabledContentBrushProperty, value);
    }

    /// <summary>
    /// Disabled text colour for the selected slot. Falls back to
    /// <see cref="DisabledContentBrush"/>, then to the enabled selected colour.
    /// </summary>
    public IBrush? DisabledSelectedContentBrush
    {
        get => GetValue(DisabledSelectedContentBrushProperty);
        set => SetValue(DisabledSelectedContentBrushProperty, value);
    }

    /// <summary>Focus ring colour. Falls back to the primary role.</summary>
    public IBrush? FocusRingBrush
    {
        get => GetValue(FocusRingBrushProperty);
        set => SetValue(FocusRingBrushProperty, value);
    }

    /// <summary>Opacity used when a disabled brush is not explicitly supplied.</summary>
    public double DisabledOpacity
    {
        get => GetValue(DisabledOpacityProperty);
        set => SetValue(DisabledOpacityProperty, value);
    }

    /// <summary>
    /// Accessibility name. The source calls this the content description and uses it to announce
    /// the option range, which is otherwise unknowable because the wheel wraps forever.
    /// </summary>
    public string? ContentDescription
    {
        get => GetValue(ContentDescriptionProperty);
        set => SetValue(ContentDescriptionProperty, value);
    }

    /// <summary>
    /// Produces the announced option text for an index. Defaults to the plain localised index,
    /// matching the source's default accessibility description (not the two-digit rendering).
    /// </summary>
    public Func<int, string>? FieldAccessibilityDescription
    {
        get => GetValue(FieldAccessibilityDescriptionProperty);
        set => SetValue(FieldAccessibilityDescriptionProperty, value);
    }

    /// <summary>Formats a slot's visible text. Null uses the localised index with two digits.</summary>
    public Func<int, string>? Formatter
    {
        get => GetValue(FormatterProperty);
        set => SetValue(FormatterProperty, value);
    }

    /// <summary>Minimum digit count for the default formatter.</summary>
    public int MinimumDigits
    {
        get => GetValue(MinimumDigitsProperty);
        set => SetValue(MinimumDigitsProperty, value);
    }

    /// <summary>
    /// Optional native content template. Its data context is a
    /// <see cref="MaterialScrollFieldItem"/>; only visible slots are realised.
    /// </summary>
    public IDataTemplate? ItemTemplate
    {
        get => GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    /// <summary>The typeface family used by the default slot.</summary>
    public FontFamily FontFamily
    {
        get => GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    /// <summary>Instantly jumps to a clamped option.</summary>
    public void ScrollToOption(int option) => _state?.ScrollToOption(option);

    /// <summary>Animates to a clamped option.</summary>
    public void AnimateScrollToOption(int option) => _state?.AnimateScrollToOption(option);

    /// <summary>Requests an internal pager page.</summary>
    internal void RequestScroll(int targetPage, bool animate)
    {
        targetPage = Math.Clamp(targetPage, 0, MaterialScrollFieldState.InfinitePageCount - 1);
        var oldTargetPage = _state?.TargetPage ?? targetPage;

        _scrollAnimation?.Dispose();
        _scrollAnimation = null;
        _scrollTarget = targetPage;
        _state?.SetTargetPage(targetPage);
        _state?.SetInProgress(true);
        _peer?.NotifyTargetChanged(oldTargetPage, targetPage,
            FormatAccessibility(oldTargetPage), FormatAccessibility(targetPage));

        if (!animate || !this.IsAttachedToVisualTree() ||
            Math.Abs(targetPage - _offset) < 0.0001)
        {
            _offset = targetPage;
            UpdateContextPage();
            _state?.SetPages(CurrentPage, targetPage, false);
            InvalidateVisual();
            if (ItemTemplate != null)
                InvalidateArrange();
            return;
        }

        _scrollAnimation = MaterialMotion.Animate(this, _offset, targetPage,
            ApplyScrollPosition, 400, 1.0, 0.001);
    }

    /// <summary>Requests a page relative to the current target (keyboard and wheel).</summary>
    internal void ScrollBy(int pages)
    {
        if (_state is not { } state)
            return;
        RequestScroll(state.TargetPage + pages, animate: true);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsNaN(Width)
            ? double.IsInfinity(availableSize.Width) ? DefaultWidth : availableSize.Width
            : Width;
        var height = double.IsNaN(Height) ? DefaultHeight : Height;
        width = Math.Max(0, width);
        height = Math.Max(0, height);
        _itemsHost.Measure(new Size(width, height));
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        UpdateTemplateItems(finalSize);
        _itemsHost.Arrange(new Rect(finalSize));
        return finalSize;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StateProperty)
        {
            var next = change.GetNewValue<MaterialScrollFieldState?>();
            if (next is null)
            {
                SetCurrentValue(StateProperty, new MaterialScrollFieldState(10));
                return;
            }
            AttachState(next);
        }
        else if (change.Property == IsEffectivelyEnabledProperty)
        {
            // Avalonia raises OnPropertyChanged for IsEnabled before InputElement recomputes
            // IsEffectivelyEnabled, so reading it there still sees the old, enabled value. The
            // effective property is raised after its backing field is updated, and it also covers
            // an ancestor disable, so cancellation is driven from it.
            if (!IsEffectivelyEnabled)
            {
                _pressed = false;
                _dragging = false;
                _velocity = 0;
                CancelScroll();
            }
            InvalidateVisual();
            InvalidateArrange();
        }
        else if (change.Property == ContentDescriptionProperty)
        {
            _peer?.NotifyNameChanged();
        }
        else if (change.Property == FieldAccessibilityDescriptionProperty)
        {
            _peer?.NotifyHelpChanged();
        }
        else if (change.Property == ItemTemplateProperty)
        {
            ResetTemplateItems();
            InvalidateArrange();
            InvalidateVisual();
        }
        else if (change.Property == FormatterProperty || change.Property == MinimumDigitsProperty)
        {
            _formatVersion++;
            ResetTemplateItems();
            InvalidateVisual();
            InvalidateArrange();
        }
        else if (change.Property == ItemHeightProperty)
        {
            _slotFirst = int.MinValue;
            _slotLast = int.MinValue;
            InvalidateVisual();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_state is not { } state)
            return;
        // While detached another field may have claimed the state; never steal it back.
        if (state.Owner is { } owner && !ReferenceEquals(owner, this))
            return;
        state.Owner = this;

        // The state may have been scrolled by its ownerless API while detached, so the cached
        // offset has to be rebuilt from the state instead of relying on the last rendered page.
        var page = Math.Clamp(state.ContextPage, 0,
            MaterialScrollFieldState.InfinitePageCount - 1);
        var changed = Math.Abs(_offset - page) > 0.0001;
        _offset = page;
        _scrollTarget = page;
        if (changed)
        {
            foreach (var slotPage in _slots.Keys.ToList())
                RemoveSlot(slotPage);
            _slotFirst = int.MinValue;
            _slotLast = int.MinValue;
            ResetTemplateItems();
            InvalidateVisual();
            InvalidateArrange();
        }
        state.SetPages(CurrentPage, page, false);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        StopAnimations();
        if (_state is { } state)
        {
            CancelScroll();
            state.Owner = null;
        }
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!IsEffectivelyEnabled)
            return;
        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed)
            return;

        Focus(NavigationMethod.Pointer);
        e.Pointer.Capture(this);
        _pressed = true;
        _dragging = false;
        _dragStartOffset = _offset;
        _dragStartY = _lastY = point.Position.Y;
        _lastTimestamp = e.Timestamp;
        _velocity = 0;
        _scrollAnimation?.Dispose();
        _scrollAnimation = null;
        _scrollTarget = _offset;
        if (_state is { } state)
        {
            var oldTarget = state.TargetPage;
            var target = CurrentPage;
            state.SetTargetPage(target);
            state.SetInProgress(true);
            if (oldTarget != target)
            {
                _peer?.NotifyTargetChanged(oldTarget, target, FormatAccessibility(oldTarget),
                    FormatAccessibility(target));
            }
        }
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_pressed || !IsEffectivelyEnabled)
            return;

        var y = e.GetPosition(this).Y;
        var delta = _dragStartY - y;
        if (!_dragging && Math.Abs(delta) > DragThreshold)
            _dragging = true;

        var now = e.Timestamp;
        var elapsed = (now - _lastTimestamp) / 1000d;
        if (elapsed > 0.0001)
        {
            var instantaneous = (_lastY - y) / ItemHeight / elapsed;
            _velocity = _velocity * 0.65 + instantaneous * 0.35;
            _lastTimestamp = now;
        }
        _lastY = y;

        if (_dragging)
        {
            _offset = Math.Clamp(_dragStartOffset + delta / ItemHeight, 0,
                MaterialScrollFieldState.InfinitePageCount - 1);
            UpdateContextPage();
            _state?.SetTargetPage(CurrentPage);
            InvalidateVisual();
            if (ItemTemplate != null)
                InvalidateArrange();
        }
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_pressed)
            return;
        _pressed = false;

        if (!_dragging)
        {
            RequestOptionAt(e.GetPosition(this).Y, animate: true);
        }
        else
        {
            var currentPage = CurrentPage;
            var projected = _offset + _velocity * 0.15;
            var target = (int)Math.Round(projected, MidpointRounding.AwayFromZero);
            // PagerSnapDistance.atMost(1): a fling can settle at most one page from the page
            // under the finger when it is released.
            target = Math.Clamp(target, currentPage - 1, currentPage + 1);
            RequestScroll(target, animate: true);
        }
        _velocity = 0;
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (!_pressed)
            return;
        _pressed = false;
        if (_dragging)
            RequestScroll(CurrentPage, animate: true);
        else
            CancelScroll();
        _velocity = 0;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (!IsEffectivelyEnabled || e.Delta.Y == 0)
            return;
        ScrollBy(e.Delta.Y < 0 ? 1 : -1);
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!IsEffectivelyEnabled)
            return;
        switch (e.Key)
        {
            case Key.Down:
                ScrollBy(1);
                e.Handled = true;
                break;
            case Key.Up:
                ScrollBy(-1);
                e.Handled = true;
                break;
        }
    }

    public override void Render(DrawingContext context)
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0)
            return;

        SyncSlots(animate: false);
        var rect = new Rect(Bounds.Size);
        var rounded = new RoundedRect(rect, CornerRadius);
        var disabled = !IsEffectivelyEnabled;
        var container = ResolveBrush(ContainerBrush, "MaterialSurfaceContainerLowestBrush");

        if (disabled)
        {
            var disabledContainer = ResolveBrush(DisabledContainerBrush, "MaterialScrollFieldDisabledContainerBrush");
            if (disabledContainer is not null)
                context.DrawRectangle(disabledContainer, null, rounded);
            else if (container is not null)
                using (context.PushOpacity(DisabledOpacity))
                    context.DrawRectangle(container, null, rounded);
        }
        else if (container is not null)
        {
            context.DrawRectangle(container, null, rounded);
        }

        if (ItemTemplate is null)
        {
            using (context.PushClip(rounded))
            {
                DrawItems(context, disabled);
            }
        }

        if (IsFocused && ResolveBrush(FocusRingBrush, "MaterialPrimaryBrush") is { } focusBrush)
        {
            var gap = FocusRingGap;
            var ringRect = rect.Inflate(gap);
            var ringRadius = new CornerRadius(
                Math.Max(0, CornerRadius.TopLeft + gap),
                Math.Max(0, CornerRadius.TopRight + gap),
                Math.Max(0, CornerRadius.BottomRight + gap),
                Math.Max(0, CornerRadius.BottomLeft + gap));
            context.DrawRectangle(null, GetFocusPen(focusBrush),
                new RoundedRect(ringRect, ringRadius));
        }
    }

    private Pen GetFocusPen(IBrush brush)
    {
        if (_focusPen is null || !ReferenceEquals(_focusPenBrush, brush))
        {
            _focusPen = new Pen(brush, FocusRingThickness);
            _focusPenBrush = brush;
        }
        return _focusPen;
    }

    protected override AutomationPeer OnCreateAutomationPeer()
    {
        _peer = new MaterialScrollFieldAutomationPeer(this);
        return _peer;
    }

    private void DrawItems(DrawingContext context, bool disabled)
    {
        var baseSize = ResolveDouble("MaterialDisplayMediumFontSize", 45);
        var selectedSize = ResolveDouble("MaterialDisplayLargeEmphasizedFontSize", 57);
        var baseWeight = ResolveWeight("MaterialDisplayMediumFontWeight", FontWeight.Normal);
        var selectedWeight = ResolveWeight("MaterialDisplayLargeEmphasizedFontWeight", FontWeight.Medium);
        var baseTracking = ResolveDouble("MaterialDisplayMediumLetterSpacing", 0);
        var selectedTracking = ResolveDouble("MaterialDisplayLargeEmphasizedLetterSpacing", 0);
        var baseLineHeight = ResolveDouble("MaterialDisplayMediumLineHeight", 0);
        var selectedLineHeight = ResolveDouble("MaterialDisplayLargeEmphasizedLineHeight", 0);
        var family = EffectiveFontFamily();
        var baseBrush = ResolveBrush(ContentBrush, "MaterialOutlineBrush");
        var selectedBrush = ResolveBrush(SelectedContentBrush, "MaterialOnSurfaceBrush");

        IBrush? disabledSelectedBrush = null;
        IBrush? disabledBrush = null;
        var useDisabledOpacity = false;
        if (disabled)
        {
            disabledSelectedBrush = ResolveBrush(DisabledSelectedContentBrush,
                "MaterialScrollFieldDisabledSelectedContentBrush");
            disabledBrush = ResolveBrush(DisabledContentBrush,
                "MaterialScrollFieldDisabledContentBrush");
            useDisabledOpacity = disabledSelectedBrush is null && disabledBrush is null;
            disabledSelectedBrush ??= disabledBrush ?? selectedBrush;
            disabledBrush ??= disabledSelectedBrush ?? baseBrush;
        }

        // ScrollField.Item computes its target colour independently of the typography fraction and
        // runs it through animateColorAsState. Oklab interpolation needs Color values, so a
        // caller-supplied non-solid brush keeps the discrete brush path instead.
        var hasBaseColor = TryGetColor(baseBrush, out var baseColor);
        var hasSelectedColor = TryGetColor(selectedBrush, out var selectedColor);
        var hasDisabledSelectedColor = TryGetColor(disabledSelectedBrush, out var disabledSelectedColor);
        var hasDisabledColor = TryGetColor(disabledBrush, out var disabledColor);
        var animateColor = hasBaseColor && hasSelectedColor &&
            (!disabled || (hasDisabledSelectedColor && hasDisabledColor));

        var opacity = useDisabledOpacity && !animateColor
            ? context.PushOpacity(DisabledOpacity)
            : default(DrawingContext.PushedState);
        try
        {
            var width = Bounds.Width;
            var centerY = Bounds.Height / 2;
            var (first, last) = GetVisibleRange(Bounds.Height);

            for (var page = first; page <= last; page++)
            {
                var selected = page == CurrentPage;
                var slot = GetOrCreateSlot(page, selected);
                var option = MaterialScrollFieldState.Mod(page, ItemCount);
                if (slot.Option != option || slot.FormatVersion != _formatVersion)
                {
                    slot.Option = option;
                    slot.FormatVersion = _formatVersion;
                    slot.Text = FormatOption(option);
                }
                // TextStyle.lerp interpolates fontSize, numeric fontWeight, letterSpacing and
                // lineHeight over the FastSpatial selection spring, clamped like the source.
                var fraction = Math.Clamp(slot.Fraction, 0, 1);
                var size = baseSize + (selectedSize - baseSize) * fraction;
                var weight = LerpWeight(baseWeight, selectedWeight, fraction);
                var tracking = baseTracking + (selectedTracking - baseTracking) * fraction;
                var lineHeight = baseLineHeight + (selectedLineHeight - baseLineHeight) * fraction;

                IBrush? brush;
                if (animateColor)
                {
                    var target = disabled
                        ? useDisabledOpacity
                            ? WithOpacity(selected ? selectedColor : baseColor, DisabledOpacity)
                            : selected ? disabledSelectedColor : disabledColor
                        : selected ? selectedColor : baseColor;
                    UpdateSlotColor(slot, target);
                    brush = slot.AnimatedBrush;
                }
                else
                {
                    brush = disabled
                        ? (selected ? disabledSelectedBrush : disabledBrush)
                        : InterpolateBrush(baseBrush, selectedBrush, fraction, slot.AnimatedBrush);
                }

                var y = centerY + (page - _offset) * ItemHeight;
                DrawSlotText(context, slot, family, size, weight, tracking, lineHeight, brush,
                    width, y);
            }
        }
        finally
        {
            opacity.Dispose();
        }
    }

    private static FontWeight LerpWeight(FontWeight from, FontWeight to, double fraction)
    {
        var weight = (int)Math.Round((int)from + ((int)to - (int)from) * fraction,
            MidpointRounding.AwayFromZero);
        return (FontWeight)Math.Clamp(weight, 1, 1000);
    }

    private static bool TryGetColor(IBrush? brush, out Color color)
    {
        if (brush is ISolidColorBrush solid)
        {
            color = solid.Color;
            return true;
        }
        color = default;
        return false;
    }

    private static Color WithOpacity(Color color, double opacity)
    {
        var alpha = (byte)Math.Round(color.A * Math.Clamp(opacity, 0, 1));
        return Color.FromArgb(alpha, color.R, color.G, color.B);
    }

    private void UpdateSlotColor(Slot slot, Color target)
    {
        if (slot.ColorAnimation is null)
        {
            // animateColorAsState seeds at its target on first composition; later target changes
            // retarget the same fast-spatial spring so the painted position and velocity carry.
            slot.TargetColor = target;
            slot.AnimatedBrush.Color = target;
            var animation = new MaterialSelectionColorAnimation(this, color =>
            {
                slot.AnimatedBrush.Color = color;
                if (ItemTemplate is null)
                    QueueRenderInvalidation();
            });
            animation.Seed(target);
            slot.ColorAnimation = animation;
            return;
        }

        if (slot.TargetColor == target)
            return;
        slot.TargetColor = target;
        slot.ColorAnimation.Set(target, MaterialSpring.SpatialFast, animate: true);
    }
    private void QueueRenderInvalidation()
    {
        if (_renderInvalidationPosted)
            return;
        _renderInvalidationPosted = true;
        Dispatcher.UIThread.Post(() =>
        {
            _renderInvalidationPosted = false;
            if (ItemTemplate is null && VisualRoot is not null)
                InvalidateVisual();
        }, DispatcherPriority.Render);
    }


    private void DrawSlotText(DrawingContext context, Slot slot, FontFamily family, double size,
        FontWeight weight, double tracking, double lineHeight, IBrush? brush, double width,
        double centerY)
    {
        if (Math.Abs(tracking) <= 0.0001)
        {
            var formatted = GetFormattedText(slot, family, size, weight, lineHeight, brush);
            context.DrawText(formatted,
                new Point((width - formatted.Width) / 2, centerY - formatted.Height / 2));
            return;
        }

        // FormattedText has no letter-spacing property. Apply the source's tracking between text
        // elements and reuse one layout per glyph so animation allocates no text buffers.
        EnsureGlyphLayouts(slot, family);
        EnsureGlyphStyle(slot, size, weight, lineHeight, brush);
        var glyphs = slot.GlyphLayouts!;
        var count = glyphs.Count;
        if (count == 0)
            return;
        var widths = slot.GlyphWidths;
        var total = slot.GlyphTotalWidth + tracking * (count - 1);
        var x = (width - total) / 2;
        for (var i = 0; i < count; i++)
        {
            context.DrawText(glyphs[i], new Point(x, centerY - glyphs[i].Height / 2));
            x += widths[i] + tracking;
        }
    }

    private void EnsureGlyphLayouts(Slot slot, FontFamily family)
    {
        if (slot.GlyphLayouts is not null && slot.GlyphText == slot.Text &&
            slot.GlyphFamily == family && slot.GlyphFlowDirection == FlowDirection)
            return;

        slot.GlyphText = slot.Text;
        slot.GlyphFamily = family;
        slot.GlyphFlowDirection = FlowDirection;
        slot.GlyphFontSize = 0;
        slot.GlyphFontWeight = default;
        slot.GlyphLineHeight = double.NaN;
        slot.GlyphForegroundBrush = null;
        slot.GlyphTotalWidth = 0;
        slot.GlyphLayouts ??= [];
        slot.GlyphLayouts.Clear();
        var enumerator = StringInfo.GetTextElementEnumerator(slot.Text);
        while (enumerator.MoveNext())
        {
            slot.GlyphLayouts.Add(new FormattedText(enumerator.GetTextElement(),
                CultureInfo.CurrentCulture, FlowDirection,
                new Typeface(family, FontStyle.Normal, FontWeight.Normal), 1, Brushes.Black));
        }
        if (slot.GlyphWidths.Length < slot.GlyphLayouts.Count)
            slot.GlyphWidths = new double[slot.GlyphLayouts.Count];
    }

    private static void EnsureGlyphStyle(Slot slot, double size, FontWeight weight,
        double lineHeight, IBrush? brush)
    {
        var glyphs = slot.GlyphLayouts!;
        var measure = false;
        if (slot.GlyphFontSize != size)
        {
            slot.GlyphFontSize = size;
            foreach (var glyph in glyphs)
                glyph.SetFontSize(size);
            measure = true;
        }
        if (slot.GlyphFontWeight != weight)
        {
            slot.GlyphFontWeight = weight;
            foreach (var glyph in glyphs)
                glyph.SetFontWeight(weight);
            measure = true;
        }
        if (slot.GlyphLineHeight != lineHeight)
        {
            slot.GlyphLineHeight = lineHeight;
            foreach (var glyph in glyphs)
                glyph.LineHeight = lineHeight;
        }
        if (!ReferenceEquals(slot.GlyphForegroundBrush, brush))
        {
            slot.GlyphForegroundBrush = brush;
            foreach (var glyph in glyphs)
                glyph.SetForegroundBrush(brush ?? Brushes.Black);
        }
        if (!measure)
            return;
        var widths = slot.GlyphWidths;
        var total = 0d;
        for (var i = 0; i < glyphs.Count; i++)
        {
            widths[i] = glyphs[i].WidthIncludingTrailingWhitespace;
            total += widths[i];
        }
        slot.GlyphTotalWidth = total;
    }

    private FormattedText GetFormattedText(Slot slot, FontFamily family, double size,
        FontWeight weight, double lineHeight, IBrush? brush)
    {
        if (slot.Layout is null || slot.LayoutText != slot.Text ||
            slot.FlowDirection != FlowDirection || slot.FontFamily != family)
        {
            slot.LayoutText = slot.Text;
            slot.FlowDirection = FlowDirection;
            slot.FontFamily = family;
            slot.FontSize = size;
            slot.FontWeight = weight;
            slot.FontLineHeight = lineHeight;
            slot.ForegroundBrush = brush;
            slot.Layout = new FormattedText(slot.Text, CultureInfo.CurrentCulture, FlowDirection,
                new Typeface(family, FontStyle.Normal, weight), size, brush ?? Brushes.Black)
            {
                LineHeight = lineHeight
            };
        }
        var formatted = slot.Layout;
        if (slot.FontSize != size)
        {
            slot.FontSize = size;
            formatted.SetFontSize(size);
        }
        if (slot.FontWeight != weight)
        {
            slot.FontWeight = weight;
            formatted.SetFontWeight(weight);
        }
        if (slot.FontLineHeight != lineHeight)
        {
            slot.FontLineHeight = lineHeight;
            formatted.LineHeight = lineHeight;
        }
        if (!ReferenceEquals(slot.ForegroundBrush, brush))
        {
            slot.ForegroundBrush = brush;
            formatted.SetForegroundBrush(brush ?? Brushes.Black);
        }
        return formatted;
    }

    private void SyncSlots(bool animate)
    {
        var (first, last) = GetVisibleRange(Bounds.Height > 0 ? Bounds.Height : DefaultHeight);
        if (first != _slotFirst || last != _slotLast)
        {
            _removeBuffer.Clear();
            foreach (var page in _slots.Keys)
            {
                if (page < first || page > last)
                    _removeBuffer.Add(page);
            }
            foreach (var page in _removeBuffer)
                RemoveSlot(page);
            _slotFirst = first;
            _slotLast = last;
        }

        var currentPage = CurrentPage;
        for (var page = first; page <= last; page++)
        {
            var selected = page == currentPage;
            if (!_slots.TryGetValue(page, out var slot))
            {
                slot = new Slot { Selected = selected, Fraction = selected ? 1 : 0 };
                _slots[page] = slot;
            }
            else if (slot.Selected != selected)
            {
                slot.Selected = selected;
                if (animate)
                    AnimateSlot(slot, selected ? 1 : 0);
                else
                    slot.Fraction = selected ? 1 : 0;
            }
        }
    }

    private void AnimateSlot(Slot slot, double target)
    {
        slot.Animation?.Dispose();
        var from = slot.Fraction;
        if (Math.Abs(from - target) < 0.0001)
        {
            slot.Fraction = target;
            slot.Animation = null;
            return;
        }
        slot.Animation = MaterialMotion.Animate(this, from, target, value =>
        {
            slot.Fraction = value;
            if (ItemTemplate is null)
                InvalidateVisual();
        }, MaterialSpring.SpatialFast, 0.01);
    }

    private Slot GetOrCreateSlot(int page, bool selected)
    {
        if (_slots.TryGetValue(page, out var slot))
            return slot;
        slot = new Slot { Selected = selected, Fraction = selected ? 1 : 0 };
        _slots[page] = slot;
        return slot;
    }

    private void RemoveSlot(int page)
    {
        if (!_slots.Remove(page, out var slot))
            return;
        slot.Animation?.Dispose();
        slot.Animation = null;
        slot.ColorAnimation?.Dispose();
        slot.ColorAnimation = null;
    }

    private void ApplyScrollPosition(double value)
    {
        _offset = Math.Clamp(value, 0, MaterialScrollFieldState.InfinitePageCount - 1);
        UpdateContextPage();
        InvalidateVisual();
        if (ItemTemplate != null)
            InvalidateArrange();
        if (Math.Abs(_offset - _scrollTarget) < 0.0001)
            _state?.SetInProgress(false);
    }

    private void UpdateContextPage()
    {
        if (_state is not { } state)
            return;
        var page = CurrentPage;
        state.SetPages(page, state.TargetPage, state.IsScrollInProgress);
        SyncSlots(animate: true);
    }

    private void RequestOptionAt(double y, bool animate)
    {
        if (_state is null)
            return;
        var page = (int)Math.Round(_offset + (y - Bounds.Height / 2) / ItemHeight,
            MidpointRounding.AwayFromZero);
        page = Math.Clamp(page, 0, MaterialScrollFieldState.InfinitePageCount - 1);
        RequestScroll(page, animate);
    }

    private int CurrentPage
    {
        get
        {
            var rounded = (int)Math.Round(_offset, MidpointRounding.AwayFromZero);
            return Math.Clamp(rounded, 0, MaterialScrollFieldState.InfinitePageCount - 1);
        }
    }

    private int ItemCount => _state?.ItemCount ?? 0;

    private (int First, int Last) GetVisibleRange(double height)
    {
        var half = height / 2;
        var first = (int)Math.Floor(_offset - half / ItemHeight) - 1;
        var last = (int)Math.Ceiling(_offset + half / ItemHeight) + 1;
        return (Math.Clamp(first, 0, MaterialScrollFieldState.InfinitePageCount - 1),
            Math.Clamp(last, 0, MaterialScrollFieldState.InfinitePageCount - 1));
    }

    private void UpdateTemplateItems(Size size)
    {
        if (ItemTemplate is null || _state is null)
        {
            if (_templateItems.Count > 0)
                ResetTemplateItems();
            return;
        }

        var (first, last) = GetVisibleRange(size.Height);
        _removeBuffer.Clear();
        foreach (var page in _templateItems.Keys)
        {
            if (page < first || page > last)
                _removeBuffer.Add(page);
        }
        foreach (var page in _removeBuffer)
        {
            var control = _templateItems[page];
            _templateItems.Remove(page);
            _itemsHost.Children.Remove(control);
        }

        for (var page = first; page <= last; page++)
        {
            if (!_templateItems.TryGetValue(page, out var item))
            {
                item = new ContentControl
                {
                    Width = size.Width,
                    Height = ItemHeight,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Focusable = false,
                    IsTabStop = false,
                    ContentTemplate = ItemTemplate
                };
                _templateItems.Add(page, item);
                _itemsHost.Children.Add(item);
            }
            else
            {
                item.ContentTemplate = ItemTemplate;
                item.Width = size.Width;
                item.Height = ItemHeight;
            }

            var option = MaterialScrollFieldState.Mod(page, _state.ItemCount);
            var selected = page == CurrentPage;
            if (item.Content is not MaterialScrollFieldItem model)
            {
                model = new MaterialScrollFieldItem();
                item.Content = model;
            }
            var text = model.Page == page && model.Index == option ? model.Text : FormatOption(option);
            model.Update(page, option, text, selected, IsEffectivelyEnabled);
            Canvas.SetLeft(item, 0);
            Canvas.SetTop(item, size.Height / 2 + (page - _offset) * ItemHeight - ItemHeight / 2);
            item.Measure(new Size(size.Width, ItemHeight));
        }
    }

    private void ResetTemplateItems()
    {
        if (_templateItems.Count == 0)
            return;
        _itemsHost.Children.Clear();
        _templateItems.Clear();
    }

    private void AttachState(MaterialScrollFieldState state)
    {
        if (ReferenceEquals(_state, state))
            return;
        if (_state is { } previous && ReferenceEquals(previous.Owner, this))
            previous.Owner = null;

        _state = state;
        state.Owner = this;
        _scrollAnimation?.Dispose();
        _scrollAnimation = null;
        foreach (var page in _slots.Keys.ToList())
            RemoveSlot(page);
        _slotFirst = int.MinValue;
        _slotLast = int.MinValue;
        ResetTemplateItems();
        _offset = Math.Clamp(state.ContextPage, 0, MaterialScrollFieldState.InfinitePageCount - 1);
        _scrollTarget = _offset;
        state.SetPages(CurrentPage, CurrentPage, false);
        InvalidateVisual();
        InvalidateArrange();
    }

    /// <summary>
    /// Cancels a running scroll and publishes a coherent idle state. The target is reconciled to
    /// the visible current page (and automation is told) before the field reports that scrolling
    /// stopped, matching <c>PagerState.targetPage</c>.
    /// </summary>
    private void CancelScroll()
    {
        _scrollAnimation?.Dispose();
        _scrollAnimation = null;
        _scrollTarget = _offset;
        if (_state is not { } state)
            return;

        var oldTargetPage = state.TargetPage;
        var targetChanged = state.ReconcileTarget();
        var newTargetPage = state.TargetPage;
        if (targetChanged)
        {
            _peer?.NotifyTargetChanged(oldTargetPage, newTargetPage,
                FormatAccessibility(oldTargetPage), FormatAccessibility(newTargetPage));
        }
        state.SetInProgress(false);
    }

    private void StopAnimations()
    {
        _scrollAnimation?.Dispose();
        _scrollAnimation = null;
        foreach (var slot in _slots.Values)
        {
            slot.Animation?.Dispose();
            slot.Animation = null;
        }
        _pressed = false;
        _dragging = false;
        _velocity = 0;
    }

    private string FormatOption(int index) => Formatter is { } formatter
        ? formatter(index) ?? string.Empty
        : index.ToString("D" + MinimumDigits, CultureInfo.CurrentCulture);

    private string FormatAccessibility(int page)
    {
        if (_state is not { } state)
            return string.Empty;
        var option = MaterialScrollFieldState.Mod(page, state.ItemCount);
        return FieldAccessibilityDescription is { } description
            ? description(option) ?? string.Empty
            : option.ToString(CultureInfo.CurrentCulture);
    }

    private FontFamily EffectiveFontFamily()
    {
        if (!string.IsNullOrEmpty(FontFamily.Name))
            return FontFamily;
        return this.TryFindResource("MaterialFontFamily", out var value) && value is FontFamily family
            ? family
            : FontFamily.Default;
    }

    private IBrush? ResolveBrush(IBrush? value, string fallbackKey) =>
        value ?? (this.TryFindResource(fallbackKey, out var resource) ? resource as IBrush : null);

    private double ResolveDouble(string key, double fallback) =>
        this.TryFindResource(key, out var value) && value is double result ? result : fallback;

    private FontWeight ResolveWeight(string key, FontWeight fallback) =>
        this.TryFindResource(key, out var value) && value is FontWeight result ? result : fallback;

    private static IBrush? InterpolateBrush(IBrush? from, IBrush? to, double fraction,
        SolidColorBrush target)
    {
        fraction = Math.Clamp(fraction, 0, 1);
        if (from is null)
            return to;
        if (to is null)
            return from;
        if (fraction <= 0)
            return from;
        if (fraction >= 1)
            return to;
        if (from is ISolidColorBrush start && to is ISolidColorBrush end)
        {
            var color = MaterialColorVector.Lerp(
                MaterialColorVector.FromColor(start.Color),
                MaterialColorVector.FromColor(end.Color),
                fraction).ToColor();
            target.Color = color;
            return target;
        }
        return fraction < 0.5 ? from : to;
    }

    private sealed class Slot
    {
        public int Option { get; set; } = -1;

        public int FormatVersion { get; set; } = -1;

        public string Text { get; set; } = string.Empty;

        public string LayoutText { get; set; } = string.Empty;

        public FlowDirection FlowDirection { get; set; }

        public FontFamily? FontFamily { get; set; }

        public double FontSize { get; set; }

        public FontWeight FontWeight { get; set; }

        public double FontLineHeight { get; set; }

        public IBrush? ForegroundBrush { get; set; }

        public SolidColorBrush AnimatedBrush { get; } = new(Colors.Black);

        public Color TargetColor { get; set; }

        public MaterialSelectionColorAnimation? ColorAnimation { get; set; }

        public FormattedText? Layout { get; set; }

        public List<FormattedText>? GlyphLayouts { get; set; }

        public double[] GlyphWidths { get; set; } = [];

        public double GlyphTotalWidth { get; set; }

        public string GlyphText { get; set; } = string.Empty;

        public FontFamily? GlyphFamily { get; set; }

        public FlowDirection GlyphFlowDirection { get; set; }

        public double GlyphFontSize { get; set; }

        public FontWeight GlyphFontWeight { get; set; }

        public double GlyphLineHeight { get; set; }

        public IBrush? GlyphForegroundBrush { get; set; }

        public bool Selected { get; set; }

        public double Fraction { get; set; }

        public MaterialMotion.SpringAnimation? Animation { get; set; }
    }

    private sealed class MaterialScrollFieldAutomationPeer(MaterialScrollField owner)
        : ControlAutomationPeer(owner), IRangeValueProvider
    {
        protected override string GetClassNameCore() => nameof(MaterialScrollField);

        protected override AutomationControlType GetAutomationControlTypeCore() =>
            AutomationControlType.Slider;

        protected override string? GetNameCore()
        {
            var name = base.GetNameCore();
            if (!string.IsNullOrEmpty(name))
                return name;
            return string.IsNullOrEmpty(owner.ContentDescription) ? null : owner.ContentDescription;
        }

        protected override string? GetHelpTextCore()
        {
            if (owner._state is not { } state)
                return base.GetHelpTextCore();
            return owner.FieldAccessibilityDescription is { } description
                ? description(state.TargetOption)
                : state.TargetOption.ToString(CultureInfo.CurrentCulture);
        }

        public bool IsReadOnly => !owner.IsEffectivelyEnabled;

        public double Minimum => 0;

        public double Maximum => Math.Max(0, (owner._state?.ItemCount ?? 1) - 1);

        public double SmallChange => 1;

        public double LargeChange => Math.Min(10, Maximum);

        public double Value => owner._state?.TargetOption ?? 0;

        public void SetValue(double value)
        {
            if (IsReadOnly || double.IsNaN(value) || owner._state is not { } state)
                return;
            var option = (int)Math.Round(Math.Clamp(value, Minimum, Maximum), MidpointRounding.AwayFromZero);
            state.AnimateScrollToOption(option);
        }

        internal void NotifyTargetChanged(int oldPage, int newPage, string oldHelp, string newHelp)
        {
            var count = owner._state?.ItemCount ?? 1;
            RaisePropertyChangedEvent(RangeValuePatternIdentifiers.ValueProperty,
                (double)MaterialScrollFieldState.Mod(oldPage, count),
                (double)MaterialScrollFieldState.Mod(newPage, count));
            RaisePropertyChangedEvent(AutomationElementIdentifiers.HelpTextProperty, oldHelp,
                newHelp);
        }

        internal void NotifyNameChanged() =>
            RaisePropertyChangedEvent(AutomationElementIdentifiers.NameProperty, null,
                GetNameCore());

        internal void NotifyHelpChanged() =>
            RaisePropertyChangedEvent(AutomationElementIdentifiers.HelpTextProperty, null,
                GetHelpTextCore());
    }
}
