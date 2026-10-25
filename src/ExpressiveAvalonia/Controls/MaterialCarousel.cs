using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Theming;
using ExpressiveAvalonia.Tokens;

namespace ExpressiveAvalonia.Controls;

/// <summary>The source carousel layouts.</summary>
public enum MaterialCarouselLayout
{
    /// <summary>One large card and a narrow peek of the next card.</summary>
    Hero,

    /// <summary>A narrow peek, the large centered card, and a trailing peek.</summary>
    CenterAlignedHero,

    /// <summary>One medium card followed by progressively narrower peeks.</summary>
    MultiBrowse,

    /// <summary>Equal medium cards that bleed off both edges with no end padding.</summary>
    Uncontained,

    /// <summary>Cards whose width follows each item's <see cref="MaterialCarouselItem.AspectRatio"/>.</summary>
    MultiAspectRatio,

    /// <summary>A single full-bleed card per page, sized to the carousel viewport.</summary>
    FullScreen,
}

/// <summary>The source context axis, which selects the mobile or tablet size ladder.</summary>
public enum MaterialCarouselContext
{
    Mobile,
    Tablet,
}

/// <summary>
/// A Material 3 carousel: a horizontally scrolling strip of media cards with paging snap,
/// single selection, and the five source layouts plus the full-screen variant.
/// </summary>
/// <remarks>
/// <para>
/// Item widths reproduce the source building blocks. The source frames are 380 units of content
/// on mobile and 568 on tablet; the ladder is scaled to the arranged width, so the same layouts
/// hold at any size. <see cref="Layout"/> selects the ladder, <see cref="Context"/> the mobile or
/// tablet row, and <see cref="ShowFourthItem"/>/<see cref="ShowFifthItem"/> reproduce the source
/// booleans that collapse those cards.
/// </para>
/// <para>
/// The control is a <see cref="ListBox"/>: selection, disabled cards, focus, pointer selection,
/// and SelectionPattern come from Avalonia. The strip publishes its card starts as scroll snap
/// points, so a wheel, touch pan, or scrollbar drag settles on a card. Left/Right (mirrored for
/// right-to-left) move the selection, Home/End jump to the ends, and Enter/Space invoke the
/// focused card, raising <see cref="ItemInvoked"/>. Selecting a card scrolls it into view with
/// the Material expressive spatial spring, or immediately when reduced motion is requested.
/// </para>
/// </remarks>
[TemplatePart("PART_ScrollViewer", typeof(ScrollViewer))]
[TemplatePart("PART_ItemsPresenter", typeof(ItemsPresenter))]
public class MaterialCarousel : ListBox
{
    private static readonly FuncTemplate<Panel?> DefaultPanel = new(() => new MaterialCarouselPanel());

    /// <summary>The source layout ladder.</summary>
    public static readonly StyledProperty<MaterialCarouselLayout> LayoutProperty =
        AvaloniaProperty.Register<MaterialCarousel, MaterialCarouselLayout>(nameof(Layout));

    /// <summary>The source context axis: mobile or tablet size ladder.</summary>
    public static readonly StyledProperty<MaterialCarouselContext> ContextProperty =
        AvaloniaProperty.Register<MaterialCarousel, MaterialCarouselContext>(nameof(Context));

    /// <summary>Gap between cards. The source uses 8 units.</summary>
    public static readonly StyledProperty<double> ItemSpacingProperty =
        AvaloniaProperty.Register<MaterialCarousel, double>(nameof(ItemSpacing), 8d,
            validate: value => double.IsFinite(value) && value >= 0);

    /// <summary>Card height. The source cards are 204 units high.</summary>
    public static readonly StyledProperty<double> ItemHeightProperty =
        AvaloniaProperty.Register<MaterialCarousel, double>(nameof(ItemHeight), 204d,
            validate: value => double.IsFinite(value) && value >= 0);

    /// <summary>Shows the fourth card (source "Show 4th item", default true).</summary>
    public static readonly StyledProperty<bool> ShowFourthItemProperty =
        AvaloniaProperty.Register<MaterialCarousel, bool>(nameof(ShowFourthItem), true);

    /// <summary>Shows the fifth card (source "Show 5th item", default true).</summary>
    public static readonly StyledProperty<bool> ShowFifthItemProperty =
        AvaloniaProperty.Register<MaterialCarousel, bool>(nameof(ShowFifthItem), true);

    /// <summary>Snaps scrolling to card starts. Turn off for free scrolling.</summary>
    public static readonly StyledProperty<bool> IsSnapToItemsProperty =
        AvaloniaProperty.Register<MaterialCarousel, bool>(nameof(IsSnapToItems), true);

    private ScrollViewer? _scrollViewer;
    private MaterialCarouselPanel? _panel;
    private MaterialMotion.SpringAnimation? _offsetAnimation;
    private bool _applyingOffset;

    static MaterialCarousel()
    {
        ItemsPanelProperty.OverrideDefaultValue<MaterialCarousel>(DefaultPanel);
        LayoutProperty.Changed.AddClassHandler<MaterialCarousel>((carousel, _) => carousel.UpdateVisualClasses());
        ContextProperty.Changed.AddClassHandler<MaterialCarousel>((carousel, _) => carousel.UpdateVisualClasses());
        ItemSpacingProperty.Changed.AddClassHandler<MaterialCarousel>((carousel, _) => carousel.InvalidateStripMeasure());
        ItemHeightProperty.Changed.AddClassHandler<MaterialCarousel>((carousel, _) => carousel.InvalidateStripMeasure());
        ShowFourthItemProperty.Changed.AddClassHandler<MaterialCarousel>((carousel, _) => carousel.UpdateItemVisibility());
        ShowFifthItemProperty.Changed.AddClassHandler<MaterialCarousel>((carousel, _) => carousel.UpdateItemVisibility());
        IsSnapToItemsProperty.Changed.AddClassHandler<MaterialCarousel>((carousel, _) => carousel.UpdateVisualClasses());
    }

    public MaterialCarousel()
    {
        SelectionChanged += HandleSelectionChanged;
    }

    /// <summary>Raised when a card is activated by tap, Enter, or Space.</summary>
    public event EventHandler<MaterialCarouselItemInvokedEventArgs>? ItemInvoked;

    public MaterialCarouselLayout Layout { get => GetValue(LayoutProperty); set => SetValue(LayoutProperty, value); }
    public MaterialCarouselContext Context { get => GetValue(ContextProperty); set => SetValue(ContextProperty, value); }
    public double ItemSpacing { get => GetValue(ItemSpacingProperty); set => SetValue(ItemSpacingProperty, value); }
    public double ItemHeight { get => GetValue(ItemHeightProperty); set => SetValue(ItemHeightProperty, value); }
    public bool ShowFourthItem { get => GetValue(ShowFourthItemProperty); set => SetValue(ShowFourthItemProperty, value); }
    public bool ShowFifthItem { get => GetValue(ShowFifthItemProperty); set => SetValue(ShowFifthItemProperty, value); }
    public bool IsSnapToItems { get => GetValue(IsSnapToItemsProperty); set => SetValue(IsSnapToItemsProperty, value); }

    /// <summary>The strip that arranges the cards, once the template has been applied.</summary>
    public Panel? ItemPanel => _panel;

    /// <summary>Scrolls the card at <paramref name="index"/> to the leading edge.</summary>
    /// <param name="index">The card index.</param>
    /// <param name="animate">
    /// Use the Material spatial spring. Reduced motion and an unattached control scroll directly.
    /// </param>
    public void ScrollToIndex(int index, bool animate = true)
    {
        if (_scrollViewer is null || _panel is null || index < 0 || index >= ItemCount)
            return;

        if (ContainerFromIndex(index) is not Control container)
        {
            ScrollIntoView(index);
            return;
        }

        var start = _panel.GetItemStart(container);
        if (start is not { } value)
            return;

        var target = Math.Max(0, value - Padding.Left);
        var from = _scrollViewer.Offset.X;
        _offsetAnimation?.Dispose();
        _offsetAnimation = null;

        if (!animate || MaterialMotion.GetIsReduced(this) || !this.IsAttachedToVisualTree() ||
            Math.Abs(target - from) < 0.5)
        {
            _scrollViewer.SetCurrentValue(ScrollViewer.OffsetProperty, new Vector(target, _scrollViewer.Offset.Y));
            return;
        }

        var viewer = _scrollViewer;
        _offsetAnimation = MaterialMotion.Animate(this, from, target, offset =>
        {
            _applyingOffset = true;
            viewer.SetCurrentValue(ScrollViewer.OffsetProperty, new Vector(offset, viewer.Offset.Y));
            _applyingOffset = false;
        }, MaterialSpring.SpatialDefault);
    }

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) =>
        new MaterialCarouselItem();

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey) =>
        NeedsContainer<MaterialCarouselItem>(item, out recycleKey);

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        if (container is not MaterialCarouselItem card)
            return;

        card.Invoked -= OnCardInvoked;
        card.Invoked += OnCardInvoked;
        ApplyItemVisibility(card, index);
        ApplyDefaultAspectRatio(card, index);
    }

    protected override void ClearContainerForItemOverride(Control container)
    {
        if (container is MaterialCarouselItem card)
            card.Invoked -= OnCardInvoked;
        base.ClearContainerForItemOverride(container);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_scrollViewer is not null)
            _scrollViewer.PropertyChanged -= OnScrollViewerPropertyChanged;

        base.OnApplyTemplate(e);
        _scrollViewer = e.NameScope.Find<ScrollViewer>("PART_ScrollViewer");
        _panel = FindPanel();

        if (_scrollViewer is not null)
            _scrollViewer.PropertyChanged += OnScrollViewerPropertyChanged;

        UpdateVisualClasses();
        UpdateItemVisibility();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // The panel exists after the first template pass; re-resolve for a re-templated control.
        _panel ??= FindPanel();
        UpdateVisualClasses();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _offsetAnimation?.Dispose();
        _offsetAnimation = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void HandleSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (SelectedIndex < 0 || _scrollViewer is null)
            return;

        // Keep the newly selected card in view; snapping then aligns it to the leading edge.
        ScrollToIndex(SelectedIndex);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Handled || e.KeyModifiers != KeyModifiers.None)
        {
            base.OnKeyDown(e);
            return;
        }

        var card = FindCard(e.Source);
        if (card is null && !ReferenceEquals(e.Source, this))
        {
            base.OnKeyDown(e);
            return;
        }

        switch (e.Key)
        {
            case Key.Home:
                SelectAndFocus(FindEnabled(0, 1));
                e.Handled = true;
                return;
            case Key.End:
                SelectAndFocus(FindEnabled(ItemCount - 1, -1));
                e.Handled = true;
                return;
            case Key.Enter:
            case Key.Space:
                if (card is { IsEffectivelyEnabled: true })
                {
                    SelectAndFocus(IndexFromContainer(card));
                    card.Invoke();
                    e.Handled = true;
                    return;
                }
                break;
            case Key.PageDown:
            case Key.PageUp:
                MoveByPage(e.Key == Key.PageDown ? 1 : -1, e);
                if (!e.Handled)
                    base.OnKeyDown(e);
                return;
        }

        base.OnKeyDown(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty)
            UpdateItemVisibility();
    }

    private void MoveByPage(int direction, KeyEventArgs e)
    {
        if (ItemCount == 0)
            return;

        // A page is the number of cards the source ladder shows at once.
        var page = Layout switch
        {
            MaterialCarouselLayout.FullScreen => 1,
            MaterialCarouselLayout.Hero => 1,
            MaterialCarouselLayout.CenterAlignedHero => 1,
            MaterialCarouselLayout.MultiBrowse => 2,
            MaterialCarouselLayout.MultiAspectRatio => 2,
            _ => 3,
        };

        var start = SelectedIndex;
        if (start < 0)
            start = direction > 0 ? -1 : ItemCount;

        var index = start;
        for (var step = 0; step < page; step++)
        {
            var next = FindEnabled(index + direction, direction);
            if (next < 0)
                break;
            index = next;
        }

        if (index == start || index < 0 || index >= ItemCount)
            return;

        SelectAndFocus(index);
        e.Handled = true;
    }

    private void SelectAndFocus(int index)
    {
        if (index < 0 || index >= ItemCount)
            return;

        var previous = SelectedIndex;
        SelectedIndex = index;
        // OnSelectionChanged already scrolls a changed selection; an unchanged one still needs
        // to be brought back to the leading edge after a manual pan.
        if (previous == index)
            ScrollToIndex(index);
        (ContainerFromIndex(index) as Control)?.Focus(NavigationMethod.Directional);
    }

    private int FindEnabled(int start, int delta)
    {
        var count = ItemCount;
        if (count == 0)
            return -1;

        var index = Math.Clamp(start, 0, count - 1);
        for (var visited = 0; visited < count; visited++)
        {
            if (IsCardEnabled(index))
                return index;
            index += delta;
            if (index < 0 || index >= count)
                return -1;
        }
        return -1;
    }

    private bool IsCardEnabled(int index) =>
        ContainerFromIndex(index) is not Control container ||
        (container.IsEffectivelyEnabled && container.IsVisible);

    private MaterialCarouselItem? FindCard(object? source)
    {
        for (var visual = source as Visual; visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is MaterialCarouselItem card && ItemsControlFromItemContainer(card) == this)
                return card;
            if (ReferenceEquals(visual, this))
                return null;
        }
        return null;
    }

    private void OnCardInvoked(object? sender, EventArgs e)
    {
        if (sender is not MaterialCarouselItem card)
            return;

        var index = IndexFromContainer(card);
        if (index < 0)
            return;

        if (SelectedIndex != index)
            SelectedIndex = index;
        ItemInvoked?.Invoke(this, new MaterialCarouselItemInvokedEventArgs(card, index));
    }

    private void OnScrollViewerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != ScrollViewer.OffsetProperty || _applyingOffset)
            return;

        // A manual pan, wheel, or scrollbar drag takes over from an in-flight selection scroll.
        if (_offsetAnimation is not null && e.GetNewValue<Vector>().X != e.GetOldValue<Vector>().X)
        {
            _offsetAnimation.Dispose();
            _offsetAnimation = null;
        }
    }

    private MaterialCarouselPanel? FindPanel()
    {
        foreach (var visual in this.GetVisualDescendants())
            if (visual is MaterialCarouselPanel panel)
                return panel;
        return null;
    }

    private void InvalidateStripMeasure() => _panel?.InvalidateMeasure();

    private void UpdateItemVisibility()
    {
        for (var i = 0; i < ItemCount; i++)
            if (ContainerFromIndex(i) is MaterialCarouselItem card)
                ApplyItemVisibility(card, i);
        _panel?.InvalidateMeasure();
    }

    private void ApplyItemVisibility(MaterialCarouselItem card, int index)
    {
        var visible = index switch
        {
            3 => ShowFourthItem,
            4 => ShowFifthItem,
            _ => true,
        };
        card.IsVisible = visible;
    }

    // The multi-aspect-ratio source orders 16:9, 4:3, 1:1, 3:4, 9:16. A card that does not set
    // its own ratio inherits that sequence, so the layout is correct without extra markup.
    private static readonly double[] SourceRatios = [16d / 9d, 4d / 3d, 1d, 3d / 4d, 9d / 16d];

    private void ApplyDefaultAspectRatio(MaterialCarouselItem card, int index)
    {
        if (Layout != MaterialCarouselLayout.MultiAspectRatio || card.IsSet(MaterialCarouselItem.AspectRatioProperty))
            return;

        card.AspectRatio = SourceRatios[index % SourceRatios.Length];
    }

    private void UpdateVisualClasses()
    {
        PseudoClasses.Set(":hero", Layout == MaterialCarouselLayout.Hero);
        PseudoClasses.Set(":center-aligned-hero", Layout == MaterialCarouselLayout.CenterAlignedHero);
        PseudoClasses.Set(":multi-browse", Layout == MaterialCarouselLayout.MultiBrowse);
        PseudoClasses.Set(":uncontained", Layout == MaterialCarouselLayout.Uncontained);
        PseudoClasses.Set(":multi-aspect-ratio", Layout == MaterialCarouselLayout.MultiAspectRatio);
        PseudoClasses.Set(":full-screen", Layout == MaterialCarouselLayout.FullScreen);
        PseudoClasses.Set(":tablet", Context == MaterialCarouselContext.Tablet);
        PseudoClasses.Set(":no-snap", !IsSnapToItems);

        foreach (var container in GetRealizedContainers())
        {
            if (container is not MaterialCarouselItem card)
                continue;
            var index = IndexFromContainer(card);
            if (index >= 0)
                ApplyDefaultAspectRatio(card, index);
        }

        _panel?.InvalidateMeasure();
    }
}

/// <summary>The single-card, full-bleed carousel (source "Carousel - Full screen").</summary>
/// <remarks>
/// The layout and context axes of the source have exactly one value each. The card fills the
/// carousel viewport and the content overlay stays hidden unless a card opts in with
/// <see cref="MaterialCarouselItem.ShowContent"/>.
/// </remarks>
public sealed class MaterialCarouselFullScreen : MaterialCarousel
{
    public MaterialCarouselFullScreen()
    {
        Layout = MaterialCarouselLayout.FullScreen;
        Context = MaterialCarouselContext.Mobile;
    }

    /// <summary>Both carousels share the control theme; this class only changes the defaults.</summary>
    protected override Type StyleKeyOverride => typeof(MaterialCarousel);
}

/// <summary>Describes the card activated by tap, Enter, or Space.</summary>
public sealed class MaterialCarouselItemInvokedEventArgs : EventArgs
{
    public MaterialCarouselItemInvokedEventArgs(MaterialCarouselItem item, int index)
    {
        Item = item;
        Index = index;
    }

    public MaterialCarouselItem Item { get; }
    public int Index { get; }
}
