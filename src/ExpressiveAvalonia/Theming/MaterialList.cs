using System.Collections;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Data;

namespace ExpressiveAvalonia.Theming;

/// <summary>Material list anatomy on native Avalonia list item containers.</summary>
/// <remarks>
/// <para>The three source densities are exposed by <see cref="DensityProperty"/> as the source
/// values 0, -2 and -4. The 0 baseline uses 56/72/88 minimum heights with 8 DIP vertical
/// padding, -2 uses 48/64/80 with 4 DIP and -4 uses 40/56/72 with no vertical padding; the
/// extra fourth line adds one 16 DIP line in every density.</para>
/// <para>Content and ContentTemplate remain the headline when <see cref="HeadlineProperty"/> is
/// not set. Use two lines for a headline and supporting text, or three for two supporting lines.
/// <see cref="LeadingContentProperty"/> and <see cref="TrailingContentProperty"/> accept icons,
/// avatars, images, video thumbnails, icon buttons and selection controls; the built-in
/// <see cref="LeadingImageProperty"/>, <see cref="LeadingMonogramTextProperty"/> and
/// <see cref="LeadingIndentProperty"/> cover the source Image, Monogram and Indent building
/// blocks without a nested control.</para>
/// <para><see cref="ExpandedContentProperty"/> turns the item into the source Expandable /
/// List item - Accordion anatomy: a trailing accordion button toggles a body that is revealed
/// with the Material medium-2 duration token, which is zero under reduced motion.</para>
/// <para><see cref="AllowItemDragProperty"/> opts a ListBox into pointer reordering and raises
/// <see cref="ItemMovedEvent"/> after the source list has been updated.</para>
/// </remarks>
public sealed class MaterialList : AvaloniaObject
{

    /// <summary>One-line or multi-line headline text, used instead of Content.</summary>
    public static readonly AttachedProperty<string?> HeadlineProperty =
        AvaloniaProperty.RegisterAttached<MaterialList, Control, string?>("Headline");

    /// <summary>Supporting text below the headline.</summary>
    public static readonly AttachedProperty<string?> SupportingTextProperty =
        AvaloniaProperty.RegisterAttached<MaterialList, Control, string?>("SupportingText");

    /// <summary>Overline above the headline (source label/medium, 12/16).</summary>
    public static readonly AttachedProperty<string?> OverlineProperty =
        AvaloniaProperty.RegisterAttached<MaterialList, Control, string?>("Overline");

    /// <summary>Trailing metadata shown before the trailing element (source label/small, 11/16).</summary>
    public static readonly AttachedProperty<string?> TrailingSupportingTextProperty =
        AvaloniaProperty.RegisterAttached<MaterialList, Control, string?>("TrailingSupportingText");

    /// <summary>Optional badge label rendered as an error-container pill in the trailing slot.</summary>
    public static readonly AttachedProperty<string?> BadgeTextProperty =
        AvaloniaProperty.RegisterAttached<MaterialList, Control, string?>("BadgeText");
    public static readonly AttachedProperty<object?> LeadingContentProperty =
        AvaloniaProperty.RegisterAttached<MaterialList, Control, object?>("LeadingContent");
    public static readonly AttachedProperty<object?> TrailingContentProperty =
        AvaloniaProperty.RegisterAttached<MaterialList, Control, object?>("TrailingContent");

    /// <summary>An image shown as the 56x56 source thumbnail with a 4 DIP corner radius.</summary>
    public static readonly AttachedProperty<IImage?> LeadingImageProperty =
        AvaloniaProperty.RegisterAttached<MaterialList, Control, IImage?>("LeadingImage");

    /// <summary>An image shown as the 40x40 circular source avatar.</summary>
    public static readonly AttachedProperty<IImage?> LeadingAvatarProperty =
        AvaloniaProperty.RegisterAttached<MaterialList, Control, IImage?>("LeadingAvatar");

    /// <summary>An image shown as the 114x64 source video thumbnail with a 4 DIP corner radius.</summary>
    public static readonly AttachedProperty<IImage?> LeadingVideoThumbnailProperty =
        AvaloniaProperty.RegisterAttached<MaterialList, Control, IImage?>("LeadingVideoThumbnail");

    /// <summary>Source Alignment=Top-aligned: content hugs the top edge instead of centering.</summary>
    public static readonly AttachedProperty<bool> IsTopAlignedProperty =
        AvaloniaProperty.RegisterAttached<MaterialList, Control, bool>("IsTopAligned");

    /// <summary>Initials rendered in a 40x40 primary-container circle.</summary>
    public static readonly AttachedProperty<string?> LeadingMonogramTextProperty =
        AvaloniaProperty.RegisterAttached<MaterialList, Control, string?>("LeadingMonogramText");

    /// <summary>Shows the 20x20 selected check on the leading thumbnail.</summary>
    public static readonly AttachedProperty<bool> IsLeadingSelectedProperty =
        AvaloniaProperty.RegisterAttached<MaterialList, Control, bool>("IsLeadingSelected");

    /// <summary>Width of the leading Indent building block; 0 renders nothing.</summary>
    public static readonly AttachedProperty<double> LeadingIndentProperty =
        AvaloniaProperty.RegisterAttached<MaterialList, Control, double>("LeadingIndent",
            validate: static value => double.IsFinite(value) && value >= 0);

    /// <summary>The one-, two- or three-line layout, with 0-density minimum heights of 56, 72 and 88.</summary>
    public static readonly AttachedProperty<int> LineCountProperty =
        AvaloniaProperty.RegisterAttached<MaterialList, Control, int>("LineCount", 1,
            validate: static value => value is >= 1 and <= 3);

    /// <summary>Source density: 0 baseline, -2 or -4.</summary>
    public static readonly AttachedProperty<int> DensityProperty =
        AvaloniaProperty.RegisterAttached<MaterialList, Control, int>("Density", 0,
            validate: static value => value is 0 or -2 or -4);

    /// <summary>Draws the inset 1 DIP divider below this item.</summary>
    public static readonly AttachedProperty<bool> ShowDividerProperty =
        AvaloniaProperty.RegisterAttached<MaterialList, Control, bool>("ShowDivider");

    /// <summary>Inherited list-level switch that draws the divider below every item.</summary>
    public static readonly AttachedProperty<bool> ShowDividersProperty =
        AvaloniaProperty.RegisterAttached<MaterialList, Control, bool>("ShowDividers", inherits: true);

    /// <summary>Expanded state of the accordion body, two-way bound by the template toggle.</summary>
    public static readonly AttachedProperty<bool> IsExpandedProperty =
        AvaloniaProperty.RegisterAttached<MaterialList, Control, bool>("IsExpanded",
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The body revealed below the header row while <see cref="IsExpandedProperty"/> is true.</summary>
    public static readonly AttachedProperty<object?> ExpandedContentProperty =
        AvaloniaProperty.RegisterAttached<MaterialList, Control, object?>("ExpandedContent");

    /// <summary>The raised visual state used while a list item is being reordered.</summary>
    public static readonly AttachedProperty<bool> IsDraggedProperty =
        AvaloniaProperty.RegisterAttached<MaterialList, Control, bool>("IsDragged");

    /// <summary>Enables pointer reordering on a ListBox; the behavior is attached on change.</summary>
    public static readonly AttachedProperty<bool> AllowItemDragProperty =
        AvaloniaProperty.RegisterAttached<MaterialList, Control, bool>("AllowItemDrag");

    /// <summary>Inherited switch that applies the source Segmented (filled) list container.</summary>
    public static readonly AttachedProperty<bool> SegmentedProperty =
        AvaloniaProperty.RegisterAttached<MaterialList, Control, bool>("Segmented", inherits: true);

    /// <summary>Bubbling notification raised after <see cref="AllowItemDragProperty"/> reorders an item.</summary>
    public static readonly RoutedEvent<MaterialListItemMovedEventArgs> ItemMovedEvent =
        RoutedEvent.Register<MaterialList, MaterialListItemMovedEventArgs>("ItemMoved", RoutingStrategies.Bubble);
    private static readonly ConditionalWeakTable<Control, object> ExpansionHooked = new();
    private static readonly ConditionalWeakTable<ListBox, ListDragBehavior> DragBehaviors = new();
    static MaterialList()
    {
        IsExpandedProperty.Changed.AddClassHandler<Control>((control, change) =>
            OnExpansionChanged(control, change.GetNewValue<bool>()));
        ExpandedContentProperty.Changed.AddClassHandler<Control>((control, _) =>
        {
            if (control is ListBoxItem item && GetIsExpanded(item))
                UpdateExpandedHeight(item, expanded: true);
        });
        AllowItemDragProperty.Changed.AddClassHandler<Control>((control, change) =>
            OnAllowItemDragChanged(control, change.GetNewValue<bool>()));
    }
    public static string? GetHeadline(Control item) => item.GetValue(HeadlineProperty);
    public static void SetHeadline(Control item, string? value) => item.SetValue(HeadlineProperty, value);
    public static string? GetSupportingText(Control item) => item.GetValue(SupportingTextProperty);
    public static void SetSupportingText(Control item, string? value) => item.SetValue(SupportingTextProperty, value);
    public static string? GetOverline(Control item) => item.GetValue(OverlineProperty);
    public static void SetOverline(Control item, string? value) => item.SetValue(OverlineProperty, value);
    public static string? GetTrailingSupportingText(Control item) => item.GetValue(TrailingSupportingTextProperty);
    public static void SetTrailingSupportingText(Control item, string? value) => item.SetValue(TrailingSupportingTextProperty, value);
    public static string? GetBadgeText(Control item) => item.GetValue(BadgeTextProperty);
    public static void SetBadgeText(Control item, string? value) => item.SetValue(BadgeTextProperty, value);
    public static object? GetLeadingContent(Control item) => item.GetValue(LeadingContentProperty);
    public static void SetLeadingContent(Control item, object? value) => item.SetValue(LeadingContentProperty, value);
    public static object? GetTrailingContent(Control item) => item.GetValue(TrailingContentProperty);
    public static void SetTrailingContent(Control item, object? value) => item.SetValue(TrailingContentProperty, value);
    public static IImage? GetLeadingImage(Control item) => item.GetValue(LeadingImageProperty);
    public static void SetLeadingImage(Control item, IImage? value) => item.SetValue(LeadingImageProperty, value);
    public static IImage? GetLeadingAvatar(Control item) => item.GetValue(LeadingAvatarProperty);
    public static void SetLeadingAvatar(Control item, IImage? value) => item.SetValue(LeadingAvatarProperty, value);
    public static IImage? GetLeadingVideoThumbnail(Control item) => item.GetValue(LeadingVideoThumbnailProperty);
    public static void SetLeadingVideoThumbnail(Control item, IImage? value) => item.SetValue(LeadingVideoThumbnailProperty, value);
    public static bool GetIsTopAligned(Control item) => item.GetValue(IsTopAlignedProperty);
    public static void SetIsTopAligned(Control item, bool value) => item.SetValue(IsTopAlignedProperty, value);
    public static string? GetLeadingMonogramText(Control item) => item.GetValue(LeadingMonogramTextProperty);
    public static void SetLeadingMonogramText(Control item, string? value) => item.SetValue(LeadingMonogramTextProperty, value);
    public static bool GetIsLeadingSelected(Control item) => item.GetValue(IsLeadingSelectedProperty);
    public static void SetIsLeadingSelected(Control item, bool value) => item.SetValue(IsLeadingSelectedProperty, value);
    public static double GetLeadingIndent(Control item) => item.GetValue(LeadingIndentProperty);
    public static void SetLeadingIndent(Control item, double value) => item.SetValue(LeadingIndentProperty, value);
    public static int GetLineCount(Control item) => item.GetValue(LineCountProperty);
    public static void SetLineCount(Control item, int value) => item.SetValue(LineCountProperty, value);
    public static int GetDensity(Control item) => item.GetValue(DensityProperty);
    public static void SetDensity(Control item, int value) => item.SetValue(DensityProperty, value);
    public static bool GetShowDivider(Control item) => item.GetValue(ShowDividerProperty);
    public static void SetShowDivider(Control item, bool value) => item.SetValue(ShowDividerProperty, value);
    public static bool GetShowDividers(Control item) => item.GetValue(ShowDividersProperty);
    public static void SetShowDividers(Control item, bool value) => item.SetValue(ShowDividersProperty, value);
    public static bool GetIsExpanded(Control item) => item.GetValue(IsExpandedProperty);
    public static void SetIsExpanded(Control item, bool value) => item.SetValue(IsExpandedProperty, value);
    public static object? GetExpandedContent(Control item) => item.GetValue(ExpandedContentProperty);
    public static void SetExpandedContent(Control item, object? value) => item.SetValue(ExpandedContentProperty, value);
    public static bool GetIsDragged(Control item) => item.GetValue(IsDraggedProperty);
    public static void SetIsDragged(Control item, bool value) => item.SetValue(IsDraggedProperty, value);
    public static bool GetAllowItemDrag(Control list) => list.GetValue(AllowItemDragProperty);
    public static void SetAllowItemDrag(Control list, bool value) => list.SetValue(AllowItemDragProperty, value);
    public static bool GetSegmented(Control list) => list.GetValue(SegmentedProperty);
    public static void SetSegmented(Control list, bool value) => list.SetValue(SegmentedProperty, value);
    public static void AddItemMovedHandler(Control control, EventHandler<MaterialListItemMovedEventArgs> handler) =>
        control.AddHandler(ItemMovedEvent, handler);
    public static void RemoveItemMovedHandler(Control control, EventHandler<MaterialListItemMovedEventArgs> handler) =>
        control.RemoveHandler(ItemMovedEvent, handler);

    private static void OnExpansionChanged(Control control, bool expanded)
    {
        if (control is not ListBoxItem item)
            return;
        // The template must exist before the body host can be measured; defer one layout pass.
        Dispatcher.UIThread.Post(() => UpdateExpandedHeight(item, expanded), DispatcherPriority.Loaded);
        if (ExpansionHooked.TryGetValue(item, out _))
            return;
        ExpansionHooked.Add(item, new object());
        item.TemplateApplied += (_, _) =>
        {
            if (GetIsExpanded(item))
                Dispatcher.UIThread.Post(() => UpdateExpandedHeight(item, expanded: true), DispatcherPriority.Loaded);
        };
        // A width change reflows the body, so the measured reveal height must follow it.
        item.SizeChanged += (_, _) =>
        {
            if (GetIsExpanded(item))
                UpdateExpandedHeight(item, expanded: true);
        };
    }
    // The template animates MaxHeight through the MaterialDurationMedium2 token, so reduced
    // motion collapses the reveal without any code path.
    private static void UpdateExpandedHeight(ListBoxItem item, bool expanded)
    {
        var host = item.GetVisualDescendants().OfType<Border>().FirstOrDefault(x => x.Name == "ExpandedHost");
        if (host is null || host.Child is not Control content)
            return;
        if (!expanded)
        {
            host.MaxHeight = 0;
            host.IsHitTestVisible = false;
            return;
        }
        host.IsHitTestVisible = true;
        var width = item.Bounds.Width - item.Padding.Left - item.Padding.Right;
        // A row that has not been measured yet would wrap at zero width and report a bogus
        // height; SizeChanged re-measures once the real width is known.
        if (!double.IsFinite(width) || width <= 0)
            return;
        content.Measure(new Size(width, double.PositiveInfinity));
        var height = content.DesiredSize.Height;
        host.MaxHeight = double.IsFinite(height) && height > 0 ? height : 0;
    }

    private static void OnAllowItemDragChanged(Control control, bool enabled)
    {
        if (control is not ListBox list)
            return;
        if (enabled)
        {
            if (!DragBehaviors.TryGetValue(list, out _))
                DragBehaviors.Add(list, new ListDragBehavior(list));
        }
        else if (DragBehaviors.TryGetValue(list, out var behavior))
        {
            behavior.Dispose();
            DragBehaviors.Remove(list);
        }
    }

    /// <summary>Pointer reordering for <see cref="AllowItemDragProperty"/>.</summary>
    private sealed class ListDragBehavior : IDisposable
    {
        private readonly ListBox _list;
        private ListBoxItem? _item;
        private int _startIndex;
        private Point _start;
        private double _delta;
        private bool _pressed;
        private bool _dragging;
        private int _originalZIndex;
        public ListDragBehavior(ListBox list)
        {
            _list = list;
            list.AddHandler(InputElement.PointerPressedEvent, OnPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
            list.AddHandler(InputElement.PointerMovedEvent, OnMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
            list.AddHandler(InputElement.PointerReleasedEvent, OnReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
            list.AddHandler(InputElement.PointerCaptureLostEvent, OnCaptureLost, RoutingStrategies.Tunnel, handledEventsToo: true);
        }
        private void OnPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(_list).Properties.IsLeftButtonPressed)
                return;
            if (e.Source is not Visual source || FindItem(source) is not { } item)
                return;
            // Never steal a press that belongs to a nested interactive control.
            if (FindInteractive(source, item) is not null)
                return;
            _item = item;
            _startIndex = _list.IndexFromContainer(item);
            if (_startIndex < 0)
            {
                _item = null;
                return;
            }
            _start = e.GetPosition(_list);
            _delta = 0;
            _pressed = true;
            _dragging = false;
        }
        private void OnMoved(object? sender, PointerEventArgs e)
        {
            if (!_pressed || _item is null)
                return;
            var point = e.GetPosition(_list);
            var dx = point.X - _start.X;
            var dy = point.Y - _start.Y;
            if (!_dragging)
            {
                if (Math.Abs(dy) < 6 || Math.Abs(dy) < Math.Abs(dx))
                    return;
                _dragging = true;
                _originalZIndex = _item.ZIndex;
                _item.ZIndex = 100;
                SetIsDragged(_item, true);
                _item.RenderTransform = new TranslateTransform();
                e.Pointer.Capture(_list);
            }
            _delta = dy;
            if (_item.RenderTransform is TranslateTransform translate)
                translate.Y = dy;
            e.Handled = true;
        }
        private void OnReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (!_pressed)
                return;
            var item = _item;
            var dragged = _dragging;
            _pressed = false;
            _dragging = false;
            _item = null;
            if (!dragged || item is null)
                return;
            e.Pointer.Capture(null);
            Reset(item);
            var target = TargetIndex(item, _delta);
            if (target != _startIndex)
                Move(_startIndex, target);
            e.Handled = true;
        }
        private void OnCaptureLost(object? sender, PointerCaptureLostEventArgs e)
        {
            if (_item is { } item)
                Reset(item);
            _pressed = false;
            _dragging = false;
            _item = null;
        }
        private void Reset(ListBoxItem item)
        {
            item.RenderTransform = null;
            item.ZIndex = _originalZIndex;
            SetIsDragged(item, false);
        }
        private int TargetIndex(ListBoxItem item, double delta)
        {
            var height = item.Bounds.Height;
            if (!double.IsFinite(height) || height <= 0)
                return _startIndex;
            var count = _list.ItemCount;
            if (count <= 1)
                return _startIndex;
            // Translate the drag distance into whole item slots, then clamp to the list.
            var offset = (int)Math.Round(delta / height, MidpointRounding.AwayFromZero);
            return Math.Clamp(_startIndex + offset, 0, count - 1);
        }
        private void Move(int from, int to)
        {
            var source = _list.ItemsSource as IList ?? _list.Items as IList;
            if (source is null || source.IsReadOnly || source.IsFixedSize)
                return;
            var item = source[from];
            source.RemoveAt(from);
            source.Insert(to, item);
            _list.RaiseEvent(new MaterialListItemMovedEventArgs(ItemMovedEvent, _list, from, to, item));
        }
        private ListBoxItem? FindItem(Visual source)
        {
            for (Visual? current = source; current is not null; current = current.GetVisualParent())
            {
                // Avalonia 12 has no GetVisualRoot(); container ownership is the reliable
                // scope check, since ItemsControlFromItemContainer returns null for foreign rows.
                if (current is ListBoxItem item)
                    return ReferenceEquals(ItemsControl.ItemsControlFromItemContainer(item), _list) ? item : null;
                if (ReferenceEquals(current, _list))
                    break;
            }
            return null;
        }
        private static Control? FindInteractive(Visual source, ListBoxItem item)
        {
            for (Visual? current = source; current is not null && !ReferenceEquals(current, item); current = current.GetVisualParent())
            {
                if (current is Button or TextBox or Slider or ComboBox)
                    return current as Control;
            }
            return null;
        }
        public void Dispose()
        {
            _list.RemoveHandler(InputElement.PointerPressedEvent, OnPressed);
            _list.RemoveHandler(InputElement.PointerMovedEvent, OnMoved);
            _list.RemoveHandler(InputElement.PointerReleasedEvent, OnReleased);
            _list.RemoveHandler(InputElement.PointerCaptureLostEvent, OnCaptureLost);
            DragBehaviors.Remove(_list);
        }
    }
}

/// <summary>Reports a completed <see cref="MaterialList.AllowItemDragProperty"/> reorder.</summary>
public sealed class MaterialListItemMovedEventArgs : RoutedEventArgs
{
    public MaterialListItemMovedEventArgs(RoutedEvent routedEvent, object? source, int oldIndex, int newIndex, object? item)
        : base(routedEvent, source)
    {
        OldIndex = oldIndex;
        NewIndex = newIndex;
        Item = item;
    }
    public int OldIndex { get; }
    public int NewIndex { get; }
    public object? Item { get; }
}
