using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace ExpressiveAvalonia.Controls;

/// <summary>The container color of a FAB menu; the trigger uses the matching solid role.</summary>
public enum MaterialFabMenuColorRole
{
    PrimaryContainer,
    SecondaryContainer,
    TertiaryContainer
}

/// <summary>
/// One labeled action inside a <see cref="MaterialFabMenu"/>.
/// </summary>
/// <remarks>
/// Source: FAB Menu building block segments (local-000227..local-000256). A segment is a
/// 56 unit pill with a 24 unit icon, 8 unit gap, 24/16 padding and the 16/24 medium label from
/// the source. It is a <see cref="Button"/>, so <see cref="Button.Command"/>,
/// <see cref="Button.CommandParameter"/>, access keys and Enter/Space activation are native.
/// </remarks>
public class MaterialFabMenuSegment : MaterialFabBase
{
    /// <summary>Defines the <see cref="Icon"/> property.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<MaterialFabMenuSegment, Geometry?>(nameof(Icon));

    /// <summary>Defines the <see cref="Label"/> property.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<MaterialFabMenuSegment, string?>(nameof(Label));

    static MaterialFabMenuSegment()
    {
        LabelProperty.Changed.AddClassHandler<MaterialFabMenuSegment>((segment, _) =>
            segment.SyncAutomationName(segment.Label));
        IconProperty.Changed.AddClassHandler<MaterialFabMenuSegment>((segment, _) =>
        {
            if (segment._icon is not null)
                segment._icon.IsVisible = segment.Icon is not null;
        });
    }

    private PathIcon? _icon;

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _icon = e.NameScope.Find<PathIcon>("PART_Icon");
        if (_icon is not null)
            _icon.IsVisible = Icon is not null;
    }

    /// <summary>Gets or sets the icon geometry.</summary>
    public Geometry? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>Gets or sets the action label; also the accessible name.</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }
}

/// <summary>
/// A Material 3 FAB menu: a trigger FAB that expands a vertical list of labeled actions.
/// </summary>
/// <remarks>
/// <para>
/// Source: FAB menu (local-001523) and its Primary/Secondary/Tertiary building blocks
/// (local-000227..local-000256). The trigger is a 56 unit full-round FAB using the solid
/// color role; the actions use the matching container role, a 56 unit pill shape, 4 unit
/// spacing between actions and 8 units between the last action and the trigger.
/// </para>
/// <para>
/// Interaction follows the native menu-button contract: click or Enter/Space on the trigger
/// toggles <see cref="IsExpanded"/>, Up/Down opens the menu, Up/Down/Home/End move focus
/// between actions with wrap-around, Escape collapses and returns focus to the trigger, an
/// outside pointer press dismisses the menu, and moving focus out of the menu collapses it.
/// Actions are ordinary <see cref="MaterialFabMenuSegment"/> buttons (or any data object with
/// an <see cref="ItemsControl.ItemTemplate"/>), so their commands execute on activation and the
/// menu collapses afterwards.
/// </para>
/// <para>
/// Expand/collapse reveals the actions with the theme's short motion duration, which is zero
/// under reduced motion; the collapsed state reserves no layout space, so a FAB menu anchored
/// to a corner grows upward without moving its trigger.
/// </para>
/// </remarks>
public class MaterialFabMenu : ItemsControl
{
    /// <summary>Defines the <see cref="IsExpanded"/> property.</summary>
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<MaterialFabMenu, bool>(
            nameof(IsExpanded), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Defines the <see cref="ColorRole"/> property.</summary>
    public static readonly StyledProperty<MaterialFabMenuColorRole> ColorRoleProperty =
        AvaloniaProperty.Register<MaterialFabMenu, MaterialFabMenuColorRole>(
            nameof(ColorRole), MaterialFabMenuColorRole.PrimaryContainer);

    private static readonly Geometry DefaultTriggerIcon = Geometry.Parse(
        "M11,5 L13,5 L13,11 L19,11 L19,13 L13,13 L13,19 L11,19 L11,13 L5,13 L5,11 L11,11 Z");

    /// <summary>Defines the <see cref="TriggerIcon"/> property.</summary>
    public static readonly StyledProperty<Geometry?> TriggerIconProperty =
        AvaloniaProperty.Register<MaterialFabMenu, Geometry?>(nameof(TriggerIcon), DefaultTriggerIcon);

    /// <summary>Defines the <see cref="ExpandAutomationName"/> property.</summary>
    public static readonly StyledProperty<string?> ExpandAutomationNameProperty =
        AvaloniaProperty.Register<MaterialFabMenu, string?>(nameof(ExpandAutomationName));

    /// <summary>Defines the <see cref="CollapseAutomationName"/> property.</summary>
    public static readonly StyledProperty<string?> CollapseAutomationNameProperty =
        AvaloniaProperty.Register<MaterialFabMenu, string?>(nameof(CollapseAutomationName));

    private const string HiddenClass = "material-fab-menu-hidden";

    private readonly HashSet<MaterialFabBase> _menuColoredSegments = [];
    private readonly EventHandler<PointerPressedEventArgs> _dismissHandler;
    private Border? _menuHost;
    private MaterialFab? _trigger;
    private PathIcon? _triggerIcon;
    private TopLevel? _topLevel;
    private DispatcherTimer? _hideTimer;
    private int? _pendingFocusIndex;
    private bool _restoreFocusOnCollapse;
    private bool _detached;

    static MaterialFabMenu()
    {
        IsExpandedProperty.Changed.AddClassHandler<MaterialFabMenu>((menu, e) =>
        {
            var expanded = (bool)e.NewValue!;
            menu.ApplyExpandedState(expanded);
            if (menu._detached)
                return;
            if (expanded)
                menu.Expanded?.Invoke(menu, EventArgs.Empty);
            else
                menu.Collapsed?.Invoke(menu, EventArgs.Empty);
        });
        ColorRoleProperty.Changed.AddClassHandler<MaterialFabMenu>((menu, _) => menu.ApplyColorRoles());
        TriggerIconProperty.Changed.AddClassHandler<MaterialFabMenu>((menu, _) => menu.UpdateTriggerIcon());
    }

    public MaterialFabMenu()
    {
        _dismissHandler = OnTopLevelPointerPressed;
        AddHandler(LostFocusEvent, OnLostFocus, RoutingStrategies.Bubble);
        AddHandler(Button.ClickEvent, OnDescendantClick, RoutingStrategies.Bubble);
    }

    /// <summary>Raised after the action list becomes visible.</summary>
    public event EventHandler? Expanded;

    /// <summary>Raised after the action list begins collapsing.</summary>
    public event EventHandler? Collapsed;

    /// <summary>Gets or sets whether the action list is visible.</summary>
    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    /// <summary>Gets or sets the container color family of the menu.</summary>
    public MaterialFabMenuColorRole ColorRole
    {
        get => GetValue(ColorRoleProperty);
        set => SetValue(ColorRoleProperty, value);
    }

    /// <summary>Gets or sets the trigger icon; the default is the source's plus glyph.</summary>
    public Geometry? TriggerIcon
    {
        get => GetValue(TriggerIconProperty);
        set => SetValue(TriggerIconProperty, value);
    }

    /// <summary>
    /// Gets or sets the trigger's accessible name while collapsed. Defaults to the
    /// <c>MaterialFabMenuOpenText</c> resource.
    /// </summary>
    public string? ExpandAutomationName
    {
        get => GetValue(ExpandAutomationNameProperty);
        set => SetValue(ExpandAutomationNameProperty, value);
    }

    /// <summary>
    /// Gets or sets the trigger's accessible name while expanded. Defaults to the
    /// <c>MaterialFabMenuCloseText</c> resource.
    /// </summary>
    public string? CollapseAutomationName
    {
        get => GetValue(CollapseAutomationNameProperty);
        set => SetValue(CollapseAutomationNameProperty, value);
    }

    private MaterialFabColorRole TriggerColorRole => ColorRole switch
    {
        MaterialFabMenuColorRole.SecondaryContainer => MaterialFabColorRole.Secondary,
        MaterialFabMenuColorRole.TertiaryContainer => MaterialFabColorRole.Tertiary,
        _ => MaterialFabColorRole.Primary
    };

    private MaterialFabColorRole SegmentColorRole => ColorRole switch
    {
        MaterialFabMenuColorRole.SecondaryContainer => MaterialFabColorRole.SecondaryContainer,
        MaterialFabMenuColorRole.TertiaryContainer => MaterialFabColorRole.TertiaryContainer,
        _ => MaterialFabColorRole.PrimaryContainer
    };

    private string ExpandText => ExpandAutomationName ?? ResourceText("MaterialFabMenuOpenText", "Open actions menu");

    private string CollapseText => CollapseAutomationName ?? ResourceText("MaterialFabMenuCloseText", "Close actions menu");

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_trigger is not null)
        {
            _trigger.Click -= OnTriggerClick;
            _trigger.KeyDown -= OnTriggerKeyDown;
        }

        base.OnApplyTemplate(e);
        _menuHost = e.NameScope.Find<Border>("PART_MenuHost");
        _trigger = e.NameScope.Find<MaterialFab>("PART_Trigger");
        _triggerIcon = e.NameScope.Find<PathIcon>("PART_TriggerIcon");

        if (_trigger is not null)
        {
            _trigger.Click += OnTriggerClick;
            _trigger.KeyDown += OnTriggerKeyDown;
            _trigger.ColorRole = TriggerColorRole;
        }

        UpdateTriggerIcon();
        ApplyExpandedState(IsExpanded);
        ApplyColorRoles();
        UpdateTriggerName(IsExpanded);
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _detached = false;
        ApplyExpandedState(IsExpanded);
        if (IsExpanded)
            AttachDismiss();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _detached = true;
        DetachDismiss();
        CancelHide();
        if (IsExpanded)
            SetCurrentValue(IsExpandedProperty, false);
        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsEnabledProperty && !IsEnabled && IsExpanded)
            SetCurrentValue(IsExpandedProperty, false);
    }

    /// <inheritdoc />
    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        container.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right;
        Dispatcher.UIThread.Post(ApplyColorRoles, DispatcherPriority.Loaded);

        SetContainerHidden(container, true);
        if (IsExpanded)
            Dispatcher.UIThread.Post(RevealSegments, DispatcherPriority.Render);
    }

    /// <inheritdoc />
    protected override void ClearContainerForItemOverride(Control container)
    {
        // Containers are recycled: release the adopted segments so a later ColorRole change
        // cannot reach a segment that now belongs to another item.
        if (container is MaterialFabBase direct)
            _menuColoredSegments.Remove(direct);
        foreach (var fab in container.GetVisualDescendants().OfType<MaterialFabBase>().ToList())
            _menuColoredSegments.Remove(fab);

        base.ClearContainerForItemOverride(container);
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!IsExpanded || e.Handled || e.KeyModifiers != KeyModifiers.None)
        {
            base.OnKeyDown(e);
            return;
        }

        var segments = GetSegmentControls();
        switch (e.Key)
        {
            case Key.Escape:
                CollapseAndRestoreFocus();
                e.Handled = true;
                return;
            case Key.Up:
                MoveSegmentFocus(segments, -1);
                e.Handled = true;
                return;
            case Key.Down:
                MoveSegmentFocus(segments, 1);
                e.Handled = true;
                return;
            case Key.Home:
                FocusSegment(segments, 0);
                e.Handled = true;
                return;
            case Key.End:
                FocusSegment(segments, segments.Count - 1);
                e.Handled = true;
                return;
        }

        base.OnKeyDown(e);
    }

    private void ApplyExpandedState(bool expanded)
    {
        PseudoClasses.Set(":expanded", expanded);
        UpdateTriggerName(expanded);

        if (_detached)
            return;

        if (expanded)
        {
            CancelHide();
            if (_menuHost is not null)
                _menuHost.IsVisible = true;

            foreach (var container in GetRealizedContainers())
                SetContainerHidden(container, true);

            AttachDismiss();

            var index = _pendingFocusIndex ?? 0;
            _pendingFocusIndex = null;
            FocusSegment(index);

            // Reveal after the newly visible containers have been measured, so their opacity
            // transition starts from the hidden value instead of popping in.
            Dispatcher.UIThread.Post(RevealSegments, DispatcherPriority.Render);
        }
        else
        {
            DetachDismiss();
            foreach (var container in GetRealizedContainers())
                SetContainerHidden(container, true);

            ScheduleHide();

            if (_restoreFocusOnCollapse)
            {
                _restoreFocusOnCollapse = false;
                _trigger?.Focus(NavigationMethod.Directional);
            }
        }
    }

    private void RevealSegments()
    {
        if (!IsExpanded)
            return;

        foreach (var container in GetRealizedContainers())
            SetContainerHidden(container, false);
    }

    // ItemTemplate containers are presenters, so the reveal class must also reach the segment
    // controls inside them; direct containers are themselves segments.
    private static void SetContainerHidden(Control container, bool hidden)
    {
        if (hidden)
            container.Classes.Add(HiddenClass);
        else
            container.Classes.Remove(HiddenClass);

        foreach (var fab in container.GetVisualDescendants().OfType<MaterialFabBase>())
        {
            if (hidden)
                fab.Classes.Add(HiddenClass);
            else
                fab.Classes.Remove(HiddenClass);
        }
    }

    private void ScheduleHide()
    {
        CancelHide();
        if (_menuHost is null || !_menuHost.IsVisible)
            return;

        var duration = RevealDuration;
        if (duration <= TimeSpan.Zero)
        {
            HideMenuHost();
            return;
        }

        _hideTimer = new DispatcherTimer(duration, DispatcherPriority.Background, (_, _) =>
        {
            CancelHide();
            HideMenuHost();
        });
        _hideTimer.Start();
    }

    private void CancelHide()
    {
        _hideTimer?.Stop();
        _hideTimer = null;
    }

    private void HideMenuHost()
    {
        if (_menuHost is not null && !IsExpanded)
            _menuHost.IsVisible = false;
    }

    private TimeSpan RevealDuration =>
        this.TryFindResource("MaterialDurationShort4", out var value) && value is TimeSpan duration
            ? duration
            : TimeSpan.FromMilliseconds(200);

    private string ResourceText(string key, string fallback) =>
        this.TryFindResource(key, out var value) && value is string text ? text : fallback;

    private void UpdateTriggerName(bool expanded)
    {
        if (_trigger is not null)
            AutomationProperties.SetName(_trigger, expanded ? CollapseText : ExpandText);
    }

    private void UpdateTriggerIcon()
    {
        if (_triggerIcon is not null)
            _triggerIcon.IsVisible = TriggerIcon is not null;
    }

    private void ApplyColorRoles()
    {
        if (_trigger is not null)
            _trigger.ColorRole = TriggerColorRole;

        // Segments can be direct containers or live inside an ItemTemplate, so collect both.
        foreach (var container in GetRealizedContainers())
        {
            if (container is MaterialFabBase direct)
                AdoptSegment(direct);

            foreach (var fab in container.GetVisualDescendants().OfType<MaterialFabBase>())
                AdoptSegment(fab);
        }

        foreach (var segment in _menuColoredSegments)
            segment.ColorRole = SegmentColorRole;
    }

    // A segment the application did not color follows the menu's role. A segment with its own
    // ColorRole (set before the menu first saw it) keeps that role.
    private void AdoptSegment(MaterialFabBase segment)
    {
        if (_menuColoredSegments.Contains(segment) || !segment.IsSet(MaterialFabBase.ColorRoleProperty))
            _menuColoredSegments.Add(segment);
    }

    private void OnTriggerClick(object? sender, RoutedEventArgs e)
    {
        if (IsExpanded)
            CollapseAndRestoreFocus();
        else
            SetCurrentValue(IsExpandedProperty, true);
    }

    private void OnTriggerKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.KeyModifiers != KeyModifiers.None)
            return;

        if (e.Key is Key.Up or Key.Down)
        {
            if (IsExpanded)
            {
                // Already open: the trigger's arrows move focus into the action list.
                MoveSegmentFocus(GetSegmentControls(), e.Key == Key.Up ? -1 : 1);
            }
            else
            {
                _pendingFocusIndex = e.Key == Key.Up ? -1 : 0;
                SetCurrentValue(IsExpandedProperty, true);
            }

            e.Handled = true;
        }
    }

    private void OnDescendantClick(object? sender, RoutedEventArgs e)
    {
        if (!IsExpanded || e.Source is not Visual source)
            return;

        // The click's source is the deepest visual (an icon or a label), so walk up to the
        // first Button: the trigger toggles itself, while any other button is a menu action.
        for (Visual? visual = source; visual is not null; visual = visual.GetVisualParent())
        {
            if (ReferenceEquals(visual, this))
                return;
            if (ReferenceEquals(visual, _trigger))
                return;
            if (visual is MaterialFabMenuSegment or Button)
            {
                CollapseAndRestoreFocus();
                return;
            }
        }
    }

    private void CollapseAndRestoreFocus()
    {
        _restoreFocusOnCollapse = true;
        if (IsExpanded)
            SetCurrentValue(IsExpandedProperty, false);
        else
            _trigger?.Focus(NavigationMethod.Directional);
    }

    private void OnLostFocus(object? sender, FocusChangedEventArgs e)
    {
        if (!IsExpanded)
            return;

        Dispatcher.UIThread.Post(() =>
        {
            if (!IsExpanded)
                return;

            var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
            if (focused is Visual visual && (ReferenceEquals(visual, this) || this.IsVisualAncestorOf(visual)))
                return;

            SetCurrentValue(IsExpandedProperty, false);
        }, DispatcherPriority.Input);
    }

    private void AttachDismiss()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (ReferenceEquals(topLevel, _topLevel))
            return;

        DetachDismiss();
        _topLevel = topLevel;
        // RemoveHandler compares delegates by reference, so the subscription reuses one instance.
        _topLevel?.AddHandler(PointerPressedEvent, _dismissHandler, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private void DetachDismiss()
    {
        _topLevel?.RemoveHandler(PointerPressedEvent, _dismissHandler);
        _topLevel = null;
    }

    private void OnTopLevelPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsExpanded)
            return;

        if (e.Source is Visual visual && (ReferenceEquals(visual, this) || this.IsVisualAncestorOf(visual)))
            return;

        SetCurrentValue(IsExpandedProperty, false);
    }

    private void FocusSegment(int index)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!IsExpanded)
                return;

            var segments = GetSegmentControls();
            FocusSegment(segments, index < 0 ? segments.Count - 1 : index);
        }, DispatcherPriority.Loaded);
    }

    private void FocusSegment(List<Control> segments, int index)
    {
        if (segments.Count == 0 || index < 0 || index >= segments.Count)
            return;

        segments[index].Focus(NavigationMethod.Directional);
    }

    private void MoveSegmentFocus(List<Control> segments, int delta)
    {
        if (segments.Count == 0)
            return;

        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        var index = focused is Visual visual
            ? segments.FindIndex(segment => ReferenceEquals(segment, visual) || segment.IsVisualAncestorOf(visual))
            : -1;
        var next = index < 0
            ? (delta > 0 ? 0 : segments.Count - 1)
            : ((index + delta) % segments.Count + segments.Count) % segments.Count;
        segments[next].Focus(NavigationMethod.Directional);
    }

    private List<Control> GetSegmentControls()
    {
        var segments = new List<Control>();
        foreach (var container in GetRealizedContainers())
        {
            if (!container.IsEffectivelyVisible)
                continue;

            if (container is Button button)
            {
                if (button.Focusable && button.IsEffectivelyEnabled)
                    segments.Add(button);
                continue;
            }

            foreach (var descendant in container.GetVisualDescendants().OfType<Button>())
            {
                if (descendant.Focusable && descendant.IsEffectivelyEnabled && descendant.IsEffectivelyVisible)
                    segments.Add(descendant);
            }
        }

        return segments;
    }
}
