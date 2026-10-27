using System;
using System.Collections.Generic;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
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

/// <summary>Where the rail's navigation items sit when they do not fill the rail.</summary>
public enum MaterialNavigationRailAlignment
{
    /// <summary>Items follow the header with the source 40 DIP group gap.</summary>
    Top,

    /// <summary>Items are centered in the full rail height (source <c>Arrangement.Center</c>).</summary>
    Middle
}

/// <summary>The two source rail families.</summary>
public enum MaterialNavigationRailMode
{
    /// <summary>Classic <c>NavigationRail</c>: 80 DIP wide, 4 DIP vertical padding, header slot.</summary>
    Compact,

    /// <summary><c>WideNavigationRail</c>: 96 DIP collapsed, 220..360 DIP expanded.</summary>
    Wide
}

/// <summary>The target/current expansion value of a wide navigation rail.</summary>
public enum MaterialNavigationRailState
{
    Collapsed,
    Expanded
}

/// <summary>
/// Material navigation rail with the classic compact layout and the expressive wide rail.
/// </summary>
/// <remarks>
/// <para>
/// Adapted from <c>NavigationRail.kt</c> and <c>WideNavigationRail.kt</c> at the pinned commit.
/// Compact mode is the classic 80 DIP rail: header, 4 DIP vertical padding, 4 DIP between items,
/// 56x32 indicators. Wide mode is the expressive rail: collapsed 96 DIP, expanded minimum 220 DIP
/// and maximum 360 DIP, 44 DIP top content padding, 4→0 DIP item spacing, 64→48 DIP item minimum
/// height and animated top→start icon positions. The header is a caller-supplied slot; the menu
/// button and FAB are preserved library extensions that fill that slot when enabled.
/// </para>
/// <para>
/// <see cref="IsExpanded"/> is the target of <see cref="State"/>; the width and item geometry use
/// frame-based <see cref="MaterialMotion"/> springs (<c>DefaultSpatial</c> for permanent rails,
/// <c>FastSpatial</c> when a host requests the modal timing). Selection is native single-selection
/// on the same ListBox, so switching modes or modal/permanent presentation never recreates it.
/// </para>
/// </remarks>
[PseudoClasses(":compact", ":wide", ":expanded", ":middle", ":floating", ":modal")]
[TemplatePart("PART_HeaderHost", typeof(StackPanel))]
[TemplatePart("PART_HeaderPresenter", typeof(ContentPresenter))]
[TemplatePart("PART_MenuFab", typeof(StackPanel))]
[TemplatePart("PART_ItemsPresenter", typeof(ItemsPresenter))]
[TemplatePart("PART_MenuButton", typeof(Button))]
[TemplatePart("PART_FabButton", typeof(Button))]
public class MaterialNavigationRail : ListBox
{
    public static readonly StyledProperty<MaterialNavigationRailMode> ModeProperty =
        AvaloniaProperty.Register<MaterialNavigationRail, MaterialNavigationRailMode>(nameof(Mode));

    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<MaterialNavigationRail, bool>(
            nameof(IsExpanded), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<MaterialNavigationRailAlignment> RailAlignmentProperty =
        AvaloniaProperty.Register<MaterialNavigationRail, MaterialNavigationRailAlignment>(nameof(RailAlignment));

    /// <summary>Icon position inherited by items whose own position is Auto. Auto follows expansion.</summary>
    public static readonly StyledProperty<MaterialNavigationItemIconPosition> ItemIconPositionProperty =
        AvaloniaProperty.Register<MaterialNavigationRail, MaterialNavigationItemIconPosition>(
            nameof(ItemIconPosition), MaterialNavigationItemIconPosition.Auto);

    /// <summary>Extension: the expanded rail paints a floating rounded surface.</summary>
    public static readonly StyledProperty<bool> IsFloatingProperty =
        AvaloniaProperty.Register<MaterialNavigationRail, bool>(nameof(IsFloating));

    public static readonly StyledProperty<object?> HeaderProperty =
        AvaloniaProperty.Register<MaterialNavigationRail, object?>(nameof(Header));

    public static readonly StyledProperty<IDataTemplate?> HeaderTemplateProperty =
        AvaloniaProperty.Register<MaterialNavigationRail, IDataTemplate?>(nameof(HeaderTemplate));

    public static readonly StyledProperty<bool> ShowMenuButtonProperty =
        AvaloniaProperty.Register<MaterialNavigationRail, bool>(nameof(ShowMenuButton), true);

    public static readonly StyledProperty<bool> ShowFabProperty =
        AvaloniaProperty.Register<MaterialNavigationRail, bool>(nameof(ShowFab), true);

    public static readonly StyledProperty<Geometry?> MenuIconProperty =
        AvaloniaProperty.Register<MaterialNavigationRail, Geometry?>(nameof(MenuIcon),
            Geometry.Parse("M3,6 L21,6 L21,8 L3,8 Z M3,11 L21,11 L21,13 L3,13 Z M3,16 L21,16 L21,18 L3,18 Z"));

    public static readonly StyledProperty<Geometry?> FabIconProperty =
        AvaloniaProperty.Register<MaterialNavigationRail, Geometry?>(nameof(FabIcon),
            Geometry.Parse("M11,5 L13,5 L13,11 L19,11 L19,13 L13,13 L13,19 L11,19 L11,13 L5,13 L5,11 L11,11 Z"));

    /// <summary>Label of the expanded extended FAB. The collapsed FAB stays icon-only.</summary>
    public static readonly StyledProperty<string?> FabTextProperty =
        AvaloniaProperty.Register<MaterialNavigationRail, string?>(nameof(FabText));

    public static readonly StyledProperty<ICommand?> MenuCommandProperty =
        AvaloniaProperty.Register<MaterialNavigationRail, ICommand?>(nameof(MenuCommand));

    public static readonly StyledProperty<object?> MenuCommandParameterProperty =
        AvaloniaProperty.Register<MaterialNavigationRail, object?>(nameof(MenuCommandParameter));

    public static readonly StyledProperty<ICommand?> FabCommandProperty =
        AvaloniaProperty.Register<MaterialNavigationRail, ICommand?>(nameof(FabCommand));

    public static readonly StyledProperty<object?> FabCommandParameterProperty =
        AvaloniaProperty.Register<MaterialNavigationRail, object?>(nameof(FabCommandParameter));

    /// <summary>The spring used by the rail width and item geometry. A modal host selects fast spatial.</summary>
    public static readonly StyledProperty<MaterialSpring> ExpansionSpringProperty =
        AvaloniaProperty.Register<MaterialNavigationRail, MaterialSpring>(
            nameof(ExpansionSpring), MaterialSpring.SpatialDefault);

    /// <summary>The current width after the frame spring, read by the host for content insets.</summary>
    public static readonly DirectProperty<MaterialNavigationRail, double> ExpansionWidthProperty =
        AvaloniaProperty.RegisterDirect<MaterialNavigationRail, double>(
            nameof(ExpansionWidth), rail => rail.ExpansionWidth);

    /// <summary>The current expansion progress (0 collapsed, 1 expanded).</summary>
    public static readonly DirectProperty<MaterialNavigationRail, double> ExpansionProgressProperty =
        AvaloniaProperty.RegisterDirect<MaterialNavigationRail, double>(
            nameof(ExpansionProgress), rail => rail.ExpansionProgress);

    /// <summary>The animated StackPanel spacing used by the source item arrangement.</summary>
    public static readonly DirectProperty<MaterialNavigationRail, double> ItemSpacingProperty =
        AvaloniaProperty.RegisterDirect<MaterialNavigationRail, double>(
            nameof(ItemSpacing), rail => rail.ItemSpacing);

    private StackPanel? _headerHost;
    private ContentPresenter? _headerPresenter;
    private StackPanel? _menuFab;
    private ItemsPresenter? _itemsPresenter;
    private StackPanel? _itemsPanel;
    private Button? _fabButton;
    private Grid? _fabIconSlot;
    private TextBlock? _fabLabel;
    private MaterialMotion.SpringAnimation? _progressSpring;
    private MaterialMotion.SpringAnimation? _widthSpring;
    private MaterialMotion.SpringAnimation? _spacingSpring;
    private double _progress;
    private double _width = double.NaN;
    private double _expandedTargetWidth = 220;
    private double _itemSpacing = 4;
    private double _publishedWidth = double.NaN;
    private double _publishedProgress;
    private double _publishedSpacing = 4;
    private bool _widthUpdateQueued;
    private bool _updating;

    static MaterialNavigationRail()
    {
        ModeProperty.Changed.AddClassHandler<MaterialNavigationRail>((rail, _) => rail.OnModeChanged());
        IsExpandedProperty.Changed.AddClassHandler<MaterialNavigationRail>((rail, _) => rail.OnExpansionTargetChanged());
        RailAlignmentProperty.Changed.AddClassHandler<MaterialNavigationRail>((rail, _) => rail.UpdateLayoutGeometry());
        ItemIconPositionProperty.Changed.AddClassHandler<MaterialNavigationRail>((rail, _) => rail.UpdateItemStates());
        IsFloatingProperty.Changed.AddClassHandler<MaterialNavigationRail>((rail, _) => rail.UpdateVisualClasses());
        FabTextProperty.Changed.AddClassHandler<MaterialNavigationRail>((rail, _) => rail.UpdateFabContent());
        FabIconProperty.Changed.AddClassHandler<MaterialNavigationRail>((rail, _) => rail.UpdateFabContent());
        ShowMenuButtonProperty.Changed.AddClassHandler<MaterialNavigationRail>((rail, _) => rail.UpdateLayoutGeometry());
        ShowFabProperty.Changed.AddClassHandler<MaterialNavigationRail>((rail, _) => rail.UpdateLayoutGeometry());
        HeaderProperty.Changed.AddClassHandler<MaterialNavigationRail>((rail, _) => { rail.UpdateLayoutGeometry(); rail.ScheduleExpandedWidthUpdate(); });
        HeaderTemplateProperty.Changed.AddClassHandler<MaterialNavigationRail>((rail, _) => { rail.UpdateLayoutGeometry(); rail.ScheduleExpandedWidthUpdate(); });
        ExpansionSpringProperty.Changed.AddClassHandler<MaterialNavigationRail>((rail, _) => rail.RestartExpansionAnimation());
    }

    public MaterialNavigationRail()
    {
        AddHandler(KeyDownEvent, OnRailKeyDown, RoutingStrategies.Tunnel);
        AddHandler(GotFocusEvent, OnRailGotFocus, RoutingStrategies.Bubble);
        SizeChanged += (_, _) => ScheduleExpandedWidthUpdate();
    }

    /// <summary>Raised after a navigation item is activated by pointer or keyboard.</summary>
    public event EventHandler<MaterialNavigationRailItemInvokedEventArgs>? ItemInvoked;

    /// <summary>Raised on every frame while the rail width or item geometry changes.</summary>
    public event EventHandler? ExpansionProgressChanged;

    public MaterialNavigationRailMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    /// <summary>Target expansion of a wide rail. Compact rails ignore it.</summary>
    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    /// <summary>Source target value. Setting it drives the same spring as <see cref="IsExpanded"/>.</summary>
    public MaterialNavigationRailState State
    {
        get => IsExpanded ? MaterialNavigationRailState.Expanded : MaterialNavigationRailState.Collapsed;
        set => SetCurrentValue(IsExpandedProperty, value == MaterialNavigationRailState.Expanded);
    }

    /// <summary>Source current value: expanded only once the spring reaches the expanded anchor.</summary>
    public MaterialNavigationRailState CurrentState =>
        _progress >= 1 ? MaterialNavigationRailState.Expanded : MaterialNavigationRailState.Collapsed;

    /// <summary>Whether the expansion spring is still running.</summary>
    public bool IsExpansionAnimating => _progressSpring is { IsRunning: true } || _widthSpring is { IsRunning: true };

    public MaterialNavigationRailAlignment RailAlignment
    {
        get => GetValue(RailAlignmentProperty);
        set => SetValue(RailAlignmentProperty, value);
    }

    public MaterialNavigationItemIconPosition ItemIconPosition
    {
        get => GetValue(ItemIconPositionProperty);
        set => SetValue(ItemIconPositionProperty, value);
    }

    public bool IsFloating
    {
        get => GetValue(IsFloatingProperty);
        set => SetValue(IsFloatingProperty, value);
    }

    /// <summary>Caller-supplied header slot (source <c>header</c> parameter), shown above the items.</summary>
    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public IDataTemplate? HeaderTemplate
    {
        get => GetValue(HeaderTemplateProperty);
        set => SetValue(HeaderTemplateProperty, value);
    }

    public bool ShowMenuButton { get => GetValue(ShowMenuButtonProperty); set => SetValue(ShowMenuButtonProperty, value); }
    public bool ShowFab { get => GetValue(ShowFabProperty); set => SetValue(ShowFabProperty, value); }
    public Geometry? MenuIcon { get => GetValue(MenuIconProperty); set => SetValue(MenuIconProperty, value); }
    public Geometry? FabIcon { get => GetValue(FabIconProperty); set => SetValue(FabIconProperty, value); }
    public string? FabText { get => GetValue(FabTextProperty); set => SetValue(FabTextProperty, value); }
    public ICommand? MenuCommand { get => GetValue(MenuCommandProperty); set => SetValue(MenuCommandProperty, value); }
    public object? MenuCommandParameter { get => GetValue(MenuCommandParameterProperty); set => SetValue(MenuCommandParameterProperty, value); }
    public ICommand? FabCommand { get => GetValue(FabCommandProperty); set => SetValue(FabCommandProperty, value); }
    public object? FabCommandParameter { get => GetValue(FabCommandParameterProperty); set => SetValue(FabCommandParameterProperty, value); }
    public MaterialSpring ExpansionSpring { get => GetValue(ExpansionSpringProperty); set => SetValue(ExpansionSpringProperty, value); }
    public double ExpansionWidth => _width;
    public double ExpansionProgress => _progress;
    public double ItemSpacing => _itemSpacing;

    /// <summary>Expands the wide rail with the source spatial spring.</summary>
    public void Expand() => State = MaterialNavigationRailState.Expanded;

    /// <summary>Collapses the wide rail with the source spatial spring.</summary>
    public void Collapse() => State = MaterialNavigationRailState.Collapsed;

    /// <summary>Toggles the wide rail.</summary>
    public void Toggle() => State = IsExpanded ? MaterialNavigationRailState.Collapsed : MaterialNavigationRailState.Expanded;

    /// <summary>Sets the target without animation.</summary>
    public void SnapTo(MaterialNavigationRailState state)
    {
        SetCurrentValue(IsExpandedProperty, state == MaterialNavigationRailState.Expanded);
        SnapExpansion();
    }

    internal MaterialNavigationItemIconPosition ResolveItemIconPosition()
    {
        if (ItemIconPosition != MaterialNavigationItemIconPosition.Auto)
            return ItemIconPosition;
        if (Mode == MaterialNavigationRailMode.Compact)
            return MaterialNavigationItemIconPosition.Top;
        return IsExpanded
            ? MaterialNavigationItemIconPosition.Start
            : MaterialNavigationItemIconPosition.Top;
    }

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) =>
        new MaterialNavigationRailItem();

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        if (container is MaterialNavigationRailItem navItem)
        {
            navItem.Invoked -= OnItemInvoked;
            navItem.Invoked += OnItemInvoked;
            navItem.RefreshRailState();
        }
        ScheduleExpandedWidthUpdate();
    }

    protected override void ClearContainerForItemOverride(Control container)
    {
        if (container is MaterialNavigationRailItem navItem)
            navItem.Invoked -= OnItemInvoked;
        base.ClearContainerForItemOverride(container);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _headerHost = e.NameScope.Find<StackPanel>("PART_HeaderHost");
        _headerPresenter = e.NameScope.Find<ContentPresenter>("PART_HeaderPresenter");
        _menuFab = e.NameScope.Find<StackPanel>("PART_MenuFab");
        _itemsPresenter = e.NameScope.Find<ItemsPresenter>("PART_ItemsPresenter");
        _fabButton = e.NameScope.Find<Button>("PART_FabButton");
        _fabIconSlot = e.NameScope.Find<Grid>("PART_FabIconSlot");
        _fabLabel = e.NameScope.Find<TextBlock>("PART_FabLabel");
        _itemsPanel = null;

        if (_itemsPresenter is not null)
        {
            _itemsPresenter.ApplyTemplate();
            _itemsPresenter.SizeChanged += (_, _) => UpdateLayoutGeometry();
        }
        if (_headerHost is not null)
            _headerHost.SizeChanged += (_, _) => { UpdateLayoutGeometry(); ScheduleExpandedWidthUpdate(); };

        UpdateVisualClasses();
        UpdateItemStates();
        UpdateFabContent();
        UpdateLayoutGeometry();
        SnapExpansion();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateVisualClasses();
        UpdateItemStates();
        UpdateLayoutGeometry();
        SnapExpansion();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        DisposeExpansionAnimations();
        base.OnDetachedFromVisualTree(e);
    }

    private void OnModeChanged()
    {
        _width = double.NaN;
        UpdateVisualClasses();
        UpdateItemStates();
        UpdateFabContent();
        UpdateLayoutGeometry();
        SnapExpansion();
        ScheduleExpandedWidthUpdate();
    }

    private void OnExpansionTargetChanged()
    {
        UpdateVisualClasses();
        UpdateItemStates();
        UpdateFabContent();
        UpdateLayoutGeometry();
        if (Mode == MaterialNavigationRailMode.Wide)
        {
            _expandedTargetWidth = CalculateExpandedWidth();
            AnimateExpansion();
        }
        else
        {
            SnapExpansion();
        }
        ScheduleExpandedWidthUpdate();
    }

    private void RestartExpansionAnimation()
    {
        DisposeExpansionAnimations();
        AnimateExpansion(force: true);
    }

    private void UpdateVisualClasses()
    {
        var wide = Mode == MaterialNavigationRailMode.Wide;
        PseudoClasses.Set(":compact", !wide);
        PseudoClasses.Set(":wide", wide);
        PseudoClasses.Set(":expanded", wide && IsExpanded);
        PseudoClasses.Set(":middle", RailAlignment == MaterialNavigationRailAlignment.Middle);
        PseudoClasses.Set(":floating", IsFloating);
    }

    private void UpdateItemStates()
    {
        foreach (var container in GetRealizedContainers())
            if (container is MaterialNavigationRailItem item)
                item.RefreshRailState();
    }

    private void UpdateFabContent()
    {
        if (_fabIconSlot is not null)
            _fabIconSlot.IsVisible = FabIcon is not null;
        if (_fabLabel is not null)
            _fabLabel.IsVisible = Mode == MaterialNavigationRailMode.Wide && IsExpanded &&
                !string.IsNullOrEmpty(FabText);
        if (_fabButton is not null)
        {
            var fallback = _fabButton.TryFindResource("MaterialNavigationRailFabText", out var value) && value is string text
                ? text
                : "Create";
            AutomationProperties.SetName(_fabButton, string.IsNullOrEmpty(FabText) ? fallback : FabText);
        }
    }

    private void UpdateLayoutGeometry()
    {
        if (_headerHost is null || _itemsPresenter is null)
            return;

        var compact = Mode == MaterialNavigationRailMode.Compact;
        var inset = compact ? 0d : 20d;
        var top = compact ? 4d : 44d;

        _headerHost.HorizontalAlignment = compact ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        _headerHost.Margin = new Thickness(inset, top, inset, 0);
        _headerPresenter?.SetValue(HorizontalAlignmentProperty,
            compact ? HorizontalAlignment.Center : HorizontalAlignment.Left);
        if (_menuFab is not null)
            _menuFab.HorizontalAlignment = compact ? HorizontalAlignment.Center : HorizontalAlignment.Left;

        var headerHeight = HeaderHeight();
        var gap = headerHeight > 0 ? (compact ? 8d : 40d) : 0d;
        if (RailAlignment == MaterialNavigationRailAlignment.Middle)
        {
            _itemsPresenter.VerticalAlignment = VerticalAlignment.Center;
            _itemsPresenter.Margin = new Thickness(0);
        }
        else
        {
            _itemsPresenter.VerticalAlignment = VerticalAlignment.Top;
            _itemsPresenter.Margin = new Thickness(0, top + headerHeight + gap, 0, 0);
        }

        // The source StackPanel spacing animates independently from the item layout.
        if (_itemsPanel is null && _itemsPresenter.Panel is StackPanel panel)
            _itemsPanel = panel;
        if (_itemsPanel is not null)
            _itemsPanel.Spacing = _itemSpacing;
    }

    private double HeaderHeight()
    {
        if (_headerHost is null)
            return 0;
        var hasMenu = ShowMenuButton || ShowFab;
        var hasHeader = Header is not null;
        if (!hasMenu && !hasHeader)
            return 0;
        if (_headerHost.Bounds.Height > 0)
            return _headerHost.Bounds.Height;
        _headerHost.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return _headerHost.DesiredSize.Height;
    }

    private void SnapExpansion()
    {
        DisposeExpansionAnimations();
        var target = Mode == MaterialNavigationRailMode.Wide && IsExpanded ? 1d : 0d;
        _expandedTargetWidth = Mode == MaterialNavigationRailMode.Wide
            ? (IsExpanded ? CalculateExpandedWidth() : 96d)
            : 80d;
        var width = Mode == MaterialNavigationRailMode.Wide
            ? (IsExpanded ? _expandedTargetWidth : 96d)
            : 80d;
        _progress = target;
        _width = width;
        _itemSpacing = Mode == MaterialNavigationRailMode.Wide && IsExpanded ? 0d : 4d;
        ApplyExpansionFrame();
    }

    private void AnimateExpansion(bool force = false)
    {
        if (!this.IsAttachedToVisualTree())
        {
            SnapExpansion();
            return;
        }

        var target = IsExpanded ? 1d : 0d;
        var widthTarget = IsExpanded ? _expandedTargetWidth : 96d;
        if (force || _progressSpring is null)
        {
            DisposeExpansionAnimations();
            _progressSpring = MaterialMotion.Animate(this, _progress, target,
                value => { _progress = value; ApplyExpansionFrame(); }, MaterialSpring.SpatialDefault, 0.01);
            _widthSpring = MaterialMotion.Animate(this, _width, widthTarget,
                value => { _width = value; ApplyExpansionFrame(); }, ExpansionSpring, 0.5);
            _spacingSpring = MaterialMotion.Animate(this, _itemSpacing, IsExpanded ? 0d : 4d,
                value => { _itemSpacing = value; ApplyExpansionFrame(); }, MaterialSpring.SpatialDefault, 0.5);
        }
        else
        {
            _progressSpring?.Retarget(target);
            _widthSpring?.Retarget(widthTarget);
            _spacingSpring?.Retarget(IsExpanded ? 0d : 4d);
        }
    }

    private void ApplyExpansionFrame()
    {
        if (_updating)
            return;
        _updating = true;
        try
        {
            if (_itemsPanel is not null)
                _itemsPanel.Spacing = _itemSpacing;
            SetCurrentValue(WidthProperty, Math.Max(0, _width));
            SetAndRaise(ExpansionWidthProperty, ref _publishedWidth, _width);
            SetAndRaise(ExpansionProgressProperty, ref _publishedProgress, _progress);
            SetAndRaise(ItemSpacingProperty, ref _publishedSpacing, _itemSpacing);
        }
        finally
        {
            _updating = false;
        }
        ExpansionProgressChanged?.Invoke(this, EventArgs.Empty);
    }

    private void DisposeExpansionAnimations()
    {
        _progressSpring?.Dispose();
        _progressSpring = null;
        _widthSpring?.Dispose();
        _widthSpring = null;
        _spacingSpring?.Dispose();
        _spacingSpring = null;
    }

    /// <summary>Queues a width re-measure after labels, items or the header change.</summary>
    internal void ScheduleExpandedWidthUpdate()
    {
        if (_widthUpdateQueued || Mode != MaterialNavigationRailMode.Wide || !IsExpanded)
            return;
        _widthUpdateQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _widthUpdateQueued = false;
            var target = CalculateExpandedWidth();
            if (Math.Abs(target - _expandedTargetWidth) < 0.01 || !this.IsAttachedToVisualTree())
                return;
            _expandedTargetWidth = target;
            if (_widthSpring is not null)
                _widthSpring.Retarget(target);
            else
                AnimateExpansion();
        }, DispatcherPriority.Background);
    }

    private double CalculateExpandedWidth()
    {
        var widest = 220d;
        foreach (var container in GetRealizedContainers())
        {
            if (container is MaterialNavigationRailItem item)
                widest = Math.Max(widest, item.MeasureExpandedWidth());
        }
        if (_headerHost is not null && Header is not null)
        {
            _headerHost.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            widest = Math.Max(widest, _headerHost.DesiredSize.Width + 40);
        }
        return Math.Clamp(widest, 220d, 360d);
    }

    private void OnRailGotFocus(object? sender, FocusChangedEventArgs e)
    {
        if (e.NavigationMethod != NavigationMethod.Tab)
            return;

        if (FindItem(e.Source) is { } item)
        {
            var index = IndexFromContainer(item);
            if (index >= 0 && index != SelectedIndex)
                SelectedIndex = index;
        }
    }

    private void OnRailKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.KeyModifiers != KeyModifiers.None)
            return;

        var focusedItem = FindItem(e.Source);
        // The menu and FAB are native buttons: their arrows and Space/Enter must not be stolen.
        if (focusedItem is null && !ReferenceEquals(e.Source, this))
            return;

        var rtl = FlowDirection == Avalonia.Media.FlowDirection.RightToLeft;
        switch (e.Key)
        {
            case Key.Down:
                MoveSelection(focusedItem, 1, e);
                return;
            case Key.Up:
                MoveSelection(focusedItem, -1, e);
                return;
            case Key.Right:
                MoveSelection(focusedItem, rtl ? -1 : 1, e);
                return;
            case Key.Left:
                MoveSelection(focusedItem, rtl ? 1 : -1, e);
                return;
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
                if (focusedItem is not null && focusedItem.IsEffectivelyEnabled)
                {
                    SelectAndFocus(IndexFromContainer(focusedItem));
                    focusedItem.Invoke();
                    e.Handled = true;
                }
                return;
        }
    }

    private void MoveSelection(MaterialNavigationRailItem? focusedItem, int delta, KeyEventArgs e)
    {
        var count = ItemCount;
        if (count == 0)
            return;

        var current = focusedItem is not null ? IndexFromContainer(focusedItem) : SelectedIndex;
        var start = current < 0 || current >= count
            ? (delta > 0 ? 0 : count - 1)
            : (current + delta + count) % count;
        var next = FindEnabled(start, delta);
        if (next < 0)
            return;

        SelectAndFocus(next);
        e.Handled = true;
    }

    private int FindEnabled(int start, int delta)
    {
        var count = ItemCount;
        if (count == 0)
            return -1;

        var index = ((start % count) + count) % count;
        for (var i = 0; i < count; i++)
        {
            if (IsItemEnabled(index))
                return index;
            index = (index + delta + count) % count;
        }
        return -1;
    }

    private bool IsItemEnabled(int index) =>
        ContainerFromIndex(index) is not Control container || container.IsEffectivelyEnabled;

    private void SelectAndFocus(int index)
    {
        if (index < 0 || index >= ItemCount)
            return;

        SelectedIndex = index;
        (ContainerFromIndex(index) as Control)?.Focus(NavigationMethod.Directional);
    }

    private MaterialNavigationRailItem? FindItem(object? source)
    {
        for (var visual = source as Visual; visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is MaterialNavigationRailItem item && ItemsControlFromItemContainer(item) == this)
                return item;
            if (ReferenceEquals(visual, this))
                return null;
        }
        return null;
    }

    private void OnItemInvoked(object? sender, EventArgs e)
    {
        if (sender is not MaterialNavigationRailItem item)
            return;

        var index = IndexFromContainer(item);
        if (index >= 0 && SelectedIndex != index)
            SelectedIndex = index;
        ItemInvoked?.Invoke(this, new MaterialNavigationRailItemInvokedEventArgs(item, index));
    }

    /// <summary>
    /// Raises <see cref="ItemInvoked"/> for an activation that happened in another native
    /// presentation of the same rail (the host's collapsed presentation).
    /// </summary>
    internal void InvokeItemAt(int index, MaterialNavigationRailItem? item)
    {
        if (index < 0 || index >= ItemCount)
            return;

        if (SelectedIndex != index)
            SelectedIndex = index;

        item ??= ContainerFromIndex(index) as MaterialNavigationRailItem;
        if (item is not null)
            ItemInvoked?.Invoke(this, new MaterialNavigationRailItemInvokedEventArgs(item, index));
    }
}

/// <summary>The expressive wide rail from <c>WideNavigationRail.kt</c>.</summary>
public class MaterialWideNavigationRail : MaterialNavigationRail
{
    public MaterialWideNavigationRail()
    {
        Mode = MaterialNavigationRailMode.Wide;
    }

    protected override Type StyleKeyOverride => typeof(MaterialNavigationRail);
}

/// <summary>Describes the navigation item activated by pointer, Enter, or Space.</summary>
public sealed class MaterialNavigationRailItemInvokedEventArgs : EventArgs
{
    public MaterialNavigationRailItemInvokedEventArgs(MaterialNavigationRailItem item, int index)
    {
        Item = item;
        Index = index;
    }

    public MaterialNavigationRailItem Item { get; }
    public int Index { get; }
}