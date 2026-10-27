using System;
using System.Collections;
using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Data;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Tokens;

namespace ExpressiveAvalonia.Theming;

/// <summary>Material list anatomy on native Avalonia list item containers.</summary>
/// <remarks>
/// <para>
/// Geometry, typography, colors and interaction shape for the interactive
/// <c>ListItem</c> family: 16 DIP start/end padding, 12 DIP between the leading and trailing
/// slots and the text block, 12 DIP top/bottom padding for precision pointers, 56/72/88 DIP
/// one/two/three-line minimum heights, corner-extra-small at rest, corner-medium on hover and
/// corner-large when selected/focused/pressed/dragged, surface container with on-surface content,
/// secondary-container/on-secondary-container when selected and
/// tertiary-container/on-tertiary-container while dragged.
/// </para>
/// <para>
/// <see cref="LineCountProperty"/> is an explicit 1/2/3 override. Zero (the default) derives
/// the line count from the presence of the overline and supporting slots plus the measured
/// supporting text, exactly like <c>ListItemType.invoke</c>: both slots or a multiline
/// supporting body is three-line, either one is two-line, neither is one-line. Upstream decides
/// "multiline" from the measured text bounding box
/// (<c>supportingPlaceable[FirstBaseline] != supportingPlaceable[LastBaseline]</c>), so the
/// derivation reads the supporting <c>TextBlock</c>'s native Avalonia
/// <c>TextLayout.TextLines.Count</c> after each layout and re-derives the classes when it
/// changes; an explicit <see cref="LineCountProperty"/> stays authoritative and suppresses the
/// measurement. The derivation is mirrored onto the
/// <c>md-list-1line</c>, <c>md-list-2line</c> and <c>md-list-3line</c> style classes used by the
/// item theme.
/// </para>
/// <para>
/// Content and ContentTemplate remain the headline when <see cref="HeadlineProperty"/> is not
/// set, so a custom ContentTemplate is never replaced by a generated array of controls.
/// <see cref="LeadingContentProperty"/> and <see cref="TrailingContentProperty"/> accept icons,
/// avatars, images, video thumbnails, icon buttons and selection controls; the built-in
/// <see cref="LeadingImageProperty"/>, <see cref="LeadingMonogramTextProperty"/> and
/// <see cref="LeadingIndentProperty"/> cover the source Image, Monogram and Indent building
/// blocks. Badge, monogram, image/video slots, indent, dividers, expansion and pointer reordering
/// are library extensions, not upstream Compose ListItem API.
/// </para>
/// <para><see cref="ExpandedContentProperty"/> turns the item into the source Expandable /
/// List item - Accordion extension: a trailing accordion button toggles a body that is revealed
/// with the Material medium-2 duration token, which is zero under reduced motion.</para>
/// <para>
/// <see cref="SegmentedProperty"/> mirrors <c>ListItemDefaults.segmentedShapes</c> for the
/// segmented list extension: the first row takes the container top corners, the last row the
/// bottom corners, and a single row all four, while middle rows keep the extra-small rest shape.
/// MaterialList re-derives <c>md-list-first</c>/<c>md-list-last</c>/<c>md-list-single</c> from the
/// live item index and count, and the interaction corner morph runs on the Material
/// <see cref="MaterialSpring.SpatialFast"/> spring (MaterialCornerRadiusTransition), retargeting
/// with the current velocity when the interaction state changes mid-morph.
/// </para>
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

    /// <summary>Explicit line count override. Zero (the default) derives it from the slots.</summary>
    public static readonly AttachedProperty<int> LineCountProperty =
        AvaloniaProperty.RegisterAttached<MaterialList, Control, int>("LineCount", 0,
            validate: static value => value is >= 0 and <= 3);

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
        OverlineProperty.Changed.AddClassHandler<Control>(static (control, _) => UpdateLineClasses(control));
        SupportingTextProperty.Changed.AddClassHandler<Control>(static (control, _) => UpdateLineClasses(control));
        LineCountProperty.Changed.AddClassHandler<Control>(static (control, _) =>
        {
            UpdateLineClasses(control);
            // Clearing an explicit count hands the derivation back to the measured text, which
            // the probe may have skipped while the override was in force.
            if (SupportingLineProbes.TryGetValue(control, out var probe))
                probe.Refresh();
        });
        SegmentedProperty.Changed.AddClassHandler<Control>(static (control, change) =>
            OnSegmentedChanged(control, change.GetNewValue<bool>()));
        // ListItemDefaults.segmentedShapes depends on the item's index and the list count, which
        // both change at runtime (insert/remove/reorder), so the item classes are derived from the
        // live containers rather than from a one-shot template state.
        TemplatedControl.CornerRadiusProperty.Changed.AddClassHandler<ListBoxItem>(static (item, change) =>
            OnItemCornerRadiusChanged(item, change.GetNewValue<CornerRadius>()));
        TemplatedControl.TemplateAppliedEvent.AddClassHandler<ListBoxItem>(static (item, e) =>
            AttachItemShapeMotion(item, e));
        TemplatedControl.TemplateAppliedEvent.AddClassHandler<ListBoxItem>(static (item, e) =>
            AttachSupportingLineProbe(item, e));
        // A recycled row that leaves and re-enters the tree loses its motion with the visual tree;
        // rebuild it from the current target so no style change is missed after reattachment.
        Control.LoadedEvent.AddClassHandler<ListBoxItem>(static (item, _) => EnsureItemShapeMotion(item));
        Control.LoadedEvent.AddClassHandler<ListBoxItem>(static (item, _) => EnsureSupportingLineProbe(item));
        Control.LoadedEvent.AddClassHandler<ListBox>(static (list, _) => OnListLoaded(list));
    }

    /// <summary>
    /// Mirrors <c>ListItemType.invoke</c> onto style classes. Three-line layouts are top-aligned
    /// and use the 12 DIP three-line padding; the theme rows read these classes so no screenshot
    /// constant is baked into the control.
    /// </summary>
    private static void UpdateLineClasses(Control control)
    {
        var explicitLines = GetLineCount(control);
        var lines = explicitLines switch
        {
            1 or 2 or 3 => explicitLines,
            _ => DeriveLineCount(control),
        };

        control.Classes.Set("md-list-1line", lines == 1);
        control.Classes.Set("md-list-2line", lines == 2);
        control.Classes.Set("md-list-3line", lines == 3);
        control.Classes.Set("md-list-overline", !string.IsNullOrEmpty(GetOverline(control)));
        control.Classes.Set("md-list-supporting", !string.IsNullOrEmpty(GetSupportingText(control)));
    }

    private static int DeriveLineCount(Control control)
    {
        var hasOverline = !string.IsNullOrEmpty(GetOverline(control));
        var supporting = GetSupportingText(control);
        var hasSupporting = !string.IsNullOrEmpty(supporting);
        // ListItem.kt treats a supporting slot as multiline when its measured text occupies more
        // than one line (FirstBaseline != LastBaseline); an explicit newline is the same case
        // before the first layout pass has measured the text.
        var multiline = hasSupporting &&
            ((SupportingLineProbes.TryGetValue(control, out var probe) && probe.IsMultiline) ||
             supporting!.Contains('\n', StringComparison.Ordinal));
        if ((hasOverline && hasSupporting) || multiline)
            return 3;
        return hasOverline || hasSupporting ? 2 : 1;
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

    private static readonly ConditionalWeakTable<ListBoxItem, ItemShapeMotion> ItemShapeMotions = new();
    private static readonly ConditionalWeakTable<Control, SupportingLineProbe> SupportingLineProbes = new();
    private static readonly ConditionalWeakTable<ListBox, SegmentedRows> SegmentedRowTrackers = new();

    /// <summary>
    /// Replaces the timed CornerRadius transition with the source FastSpatial shape spring
    /// (<c>ListItem.kt</c>: <c>shapeAnimationSpec = MotionSchemeKeyTokens.FastSpatial</c>). The
    /// state styles keep publishing <see cref="TemplatedControl.CornerRadiusProperty"/>; this
    /// motion renders that target on the clipped surface and the ripple layer, so a rapid
    /// press/release/select sequence retargets the running corners with their current velocity.
    /// </summary>
    private static void AttachItemShapeMotion(ListBoxItem item, TemplateAppliedEventArgs e)
    {
        if (ItemShapeMotions.TryGetValue(item, out var previous))
        {
            previous.Dispose();
            // Dispose removes the entry, but be explicit so a retemplate can never hit a
            // duplicate-key CWT Add after a partially disposed instance.
            ItemShapeMotions.Remove(item);
        }
        var surface = e.NameScope.Find<Border>("PART_SwipeContent");
        if (surface is null)
            return;
        var layer = e.NameScope.Find<MaterialRipple>("StateLayer");
        ItemShapeMotions.Remove(item);
        ItemShapeMotions.Add(item, new ItemShapeMotion(item, surface, layer));
    }

    private static void EnsureItemShapeMotion(ListBoxItem item)
    {
        if (ItemShapeMotions.TryGetValue(item, out _))
            return;
        var surface = item.GetVisualDescendants().OfType<Border>()
            .FirstOrDefault(visual => visual.Name == "PART_SwipeContent");
        if (surface is null)
            return;
        var layer = item.GetVisualDescendants().OfType<MaterialRipple>()
            .FirstOrDefault(visual => visual.Name == "StateLayer");
        ItemShapeMotions.Add(item, new ItemShapeMotion(item, surface, layer));
    }

    private static void OnItemCornerRadiusChanged(ListBoxItem item, CornerRadius target)
    {
        if (ItemShapeMotions.TryGetValue(item, out var motion))
            motion.Set(target);
    }

    private static void AttachSupportingLineProbe(ListBoxItem item, TemplateAppliedEventArgs e)
    {
        if (SupportingLineProbes.TryGetValue(item, out var previous))
        {
            previous.Dispose();
            SupportingLineProbes.Remove(item);
        }
        if (e.NameScope.Find<TextBlock>("SupportingText") is not { } text)
            return;
        // The measured lines of a recycled row must seed the anatomy of the new data, so the
        // classes are re-derived as soon as the probe has a value.
        SupportingLineProbes.Add(item, new SupportingLineProbe(item, text));
    }

    private static void EnsureSupportingLineProbe(ListBoxItem item)
    {
        if (SupportingLineProbes.TryGetValue(item, out _))
            return;
        var text = item.GetVisualDescendants().OfType<TextBlock>()
            .FirstOrDefault(visual => visual.Name == "SupportingText");
        if (text is null)
            return;
        SupportingLineProbes.Add(item, new SupportingLineProbe(item, text));
    }

    /// <summary>
    /// Source <c>ListItem.kt</c> classifies the supporting slot from the measured text
    /// (<c>supportingPlaceable[FirstBaseline] != supportingPlaceable[LastBaseline]</c>), which only
    /// exists after the text has been laid out. The probe reads the native
    /// <c>TextLayout.TextLines</c> of the supporting <c>TextBlock</c> and re-derives the item's
    /// line classes whenever the wrapped line count changes. An explicit
    /// <see cref="LineCountProperty"/> stays authoritative.
    /// </summary>
    private sealed class SupportingLineProbe : IDisposable
    {
        private readonly Control _item;
        private readonly TextBlock _text;
        private bool _disposed;

        public SupportingLineProbe(Control item, TextBlock text)
        {
            _item = item;
            _text = text;
            // SizeChanged is the layout result of both a text change and a width change: a
            // different wrap count always changes the arranged height of the text block.
            text.SizeChanged += OnTextSizeChanged;
            item.DetachedFromVisualTree += OnDetached;
            Refresh();
        }

        /// <summary>Gets whether the supporting text currently occupies more than one line.</summary>
        public bool IsMultiline { get; private set; }

        private void OnTextSizeChanged(object? sender, SizeChangedEventArgs e) => Refresh();

        internal void Refresh()
        {
            if (_disposed)
                return;
            // An explicit line count is authoritative, so the measurement is never read then.
            if (GetLineCount(_item) is 1 or 2 or 3)
                return;
            var multiline = _text.IsVisible &&
                _text.TextLayout.TextLines.Count > 1;
            if (multiline == IsMultiline)
                return;
            IsMultiline = multiline;
            UpdateLineClasses(_item);
        }

        private void OnDetached(object? sender, VisualTreeAttachmentEventArgs e) => Dispose();

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _text.SizeChanged -= OnTextSizeChanged;
            _item.DetachedFromVisualTree -= OnDetached;
            SupportingLineProbes.Remove(_item);
        }
    }

    private sealed class ItemShapeMotion : IDisposable
    {
        private readonly ListBoxItem _item;
        private readonly Border _surface;
        private readonly MaterialRipple? _layer;
        private readonly MaterialCornerRadiusTransition _corners;
        private bool _disposed;
        private bool _initialized;

        public ItemShapeMotion(ListBoxItem item, Border surface, MaterialRipple? layer)
        {
            _item = item;
            _surface = surface;
            _layer = layer;
            _corners = new MaterialCornerRadiusTransition(item, item.CornerRadius, Apply,
                MaterialSpring.SpatialFast);
            item.DetachedFromVisualTree += OnDetached;
            // Segment classes and the initial pseudo-classes resolve during the first layout;
            // those structural shapes must appear at their target. Interactions after the loaded
            // pass morph with FastSpatial like the source animatable.
            Dispatcher.UIThread.Post(() => _initialized = !_disposed, DispatcherPriority.Loaded);
        }

        public void Set(CornerRadius target) =>
            _corners.Set(target, _initialized, MaterialSpring.SpatialFast);

        private void Apply(CornerRadius value)
        {
            _surface.SetCurrentValue(Border.CornerRadiusProperty, value);
            _layer?.SetCurrentValue(MaterialRipple.CornerRadiusProperty, value);
        }

        private void OnDetached(object? sender, VisualTreeAttachmentEventArgs e) => Dispose();

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _item.DetachedFromVisualTree -= OnDetached;
            _corners.Dispose();
            ItemShapeMotions.Remove(_item);
        }
    }

    private static void OnSegmentedChanged(Control control, bool enabled)
    {
        if (control is not ListBox list)
            return;
        if (enabled)
        {
            EnsureSegmentedRows(list);
            return;
        }
        if (SegmentedRowTrackers.TryGetValue(list, out var tracker))
            tracker.Dispose();
        foreach (var container in list.GetRealizedContainers())
            ClearSegmentClasses(container);
    }

    private static void OnListLoaded(ListBox list)
    {
        // Realization and recycling can happen before or after the Segmented flag; re-derive the
        // first/last/single classes from the live containers on every load.
        if (GetSegmented(list))
            EnsureSegmentedRows(list);
    }

    private static void EnsureSegmentedRows(ListBox list)
    {
        if (!SegmentedRowTrackers.TryGetValue(list, out _))
            SegmentedRowTrackers.Add(list, new SegmentedRows(list));
        UpdateSegmentClasses(list);
    }

    /// <summary>
    /// Mirrors <c>ListItemDefaults.segmentedShapes</c> (outer rest corners from
    /// <c>ListTokens.ContainerShape</c> = corner-large on the first/last/single row). The middle
    /// rows keep the source extra-small rest shape; interaction shapes are still applied by the
    /// theme, so pressed/selected keep corner-large on every corner.
    /// </summary>
    private static void UpdateSegmentClasses(ListBox list)
    {
        foreach (var container in list.GetRealizedContainers())
        {
            if (container is not Control item)
                continue;
            UpdateSegmentClass(list, item, list.IndexFromContainer(item));
        }
    }

    private static void UpdateSegmentClass(ListBox list, Control item, int index)
    {
        var count = list.ItemCount;
        if (!GetSegmented(list) || count == 0 || index < 0)
        {
            ClearSegmentClasses(item);
            return;
        }

        var single = count == 1;
        item.Classes.Set("md-list-single", single);
        item.Classes.Set("md-list-first", !single && index == 0);
        item.Classes.Set("md-list-last", !single && index == count - 1);
        item.Classes.Set("md-list-middle", !single && index > 0 && index < count - 1);
    }

    private static void ClearSegmentClasses(Control item)
    {
        item.Classes.Set("md-list-first", false);
        item.Classes.Set("md-list-last", false);
        item.Classes.Set("md-list-single", false);
        item.Classes.Set("md-list-middle", false);
    }

    /// <summary>Tracks one segmented ListBox: container realization plus live count changes.</summary>
    private sealed class SegmentedRows : IDisposable
    {
        private readonly ListBox _list;
        private ItemsSourceView? _items;
        private bool _disposed;

        public SegmentedRows(ListBox list)
        {
            _list = list;
            list.ContainerPrepared += OnContainerPrepared;
            list.DetachedFromVisualTree += OnDetached;
            AttachItems();
        }

        private void AttachItems()
        {
            var items = _list.ItemsView;
            if (ReferenceEquals(items, _items))
                return;
            if (_items is not null)
                _items.CollectionChanged -= OnItemsChanged;
            _items = items;
            if (_items is not null)
                _items.CollectionChanged += OnItemsChanged;
        }

        private void OnContainerPrepared(object? sender, ContainerPreparedEventArgs e)
        {
            if (e.Container is Control container)
                UpdateSegmentClass(_list, container, e.Index);
        }

        private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            AttachItems();
            UpdateSegmentClasses(_list);
        }

        private void OnDetached(object? sender, VisualTreeAttachmentEventArgs e) => Dispose();

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _list.ContainerPrepared -= OnContainerPrepared;
            _list.DetachedFromVisualTree -= OnDetached;
            if (_items is not null)
            {
                _items.CollectionChanged -= OnItemsChanged;
                _items = null;
            }
            SegmentedRowTrackers.Remove(_list);
        }
    }

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
