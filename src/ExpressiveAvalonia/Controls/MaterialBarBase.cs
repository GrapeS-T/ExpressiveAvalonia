using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Presenters;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// Shared action, adaptive-overflow and keyboard plumbing for <see cref="MaterialAppBar"/>,
/// <see cref="MaterialBottomAppBar"/> and <see cref="MaterialToolbar"/>.
/// </summary>
/// <remarks>
/// <para>
/// Source mapping: the actions list and <see cref="MaxItemCount"/> reproduce
/// <c>AppBarRow(maxItemCount)</c> / <c>AppBarColumn(maxItemCount)</c>. Items are measured in
/// declaration order until the available main-axis space is exhausted; space for the overflow
/// indicator is reserved up front and, like upstream, the final item may use that reserved space
/// instead of overflowing. <see cref="MaxItemCount"/> is the upstream item cap: when more items
/// than the cap are present, at most <c>MaxItemCount - 1</c> render inline.
/// </para>
/// <para>
/// The overflow flyout hosts the hidden items plus <see cref="OverflowContent"/>. Arrow, Home and
/// End move focus between inline actions; Escape closes the flyout and returns focus to its
/// button. If a focused action becomes hidden because the bar resized, focus moves to the overflow
/// button so keyboard users never lose their place.
/// </para>
/// </remarks>
public abstract class MaterialBarBase : TemplatedControl
{
    /// <summary>
    /// Maximum number of actions before the overflow indicator is used. Upstream
    /// <c>maxItemCount</c>; at most <c>MaxItemCount - 1</c> items render when the list is longer.
    /// </summary>
    public static readonly StyledProperty<int> MaxItemCountProperty =
        AvaloniaProperty.Register<MaterialBarBase, int>(
            nameof(MaxItemCount), int.MaxValue, validate: value => value >= 0);

    /// <summary>Arbitrary content appended to the overflow flyout below the generated commands.</summary>
    public static readonly StyledProperty<object?> OverflowContentProperty =
        AvaloniaProperty.Register<MaterialBarBase, object?>(nameof(OverflowContent));

    /// <summary>Where the overflow flyout opens relative to the overflow button.</summary>
    public static readonly StyledProperty<PlacementMode> OverflowPlacementProperty =
        AvaloniaProperty.Register<MaterialBarBase, PlacementMode>(
            nameof(OverflowPlacement), PlacementMode.BottomEdgeAlignedRight);

    public static readonly DirectProperty<MaterialBarBase, bool> HasOverflowProperty =
        AvaloniaProperty.RegisterDirect<MaterialBarBase, bool>(
            nameof(HasOverflow), bar => bar.HasOverflow, (bar, value) => bar.HasOverflow = value);

    /// <summary>Number of actions currently laid out inline.</summary>
    public static readonly DirectProperty<MaterialBarBase, int> VisibleActionCountProperty =
        AvaloniaProperty.RegisterDirect<MaterialBarBase, int>(
            nameof(VisibleActionCount), bar => bar.VisibleActionCount,
            (bar, value) => bar.VisibleActionCount = value);

    private readonly AvaloniaList<MaterialBarAction> _actions = new();
    private readonly List<Control> _actionControls = new();
    private readonly MaterialAppBarMenuState _menuState = new();
    private MaterialBarActionsPanel? _actionsPanel;
    private Button? _overflowButton;
    private Flyout? _overflowFlyout;
    private bool _hasOverflow;
    private int _visibleActionCount;

    protected MaterialBarBase()
    {
        _actions.CollectionChanged += (_, _) => RebuildActionControls();
    }

    static MaterialBarBase()
    {
        MaxItemCountProperty.Changed.AddClassHandler<MaterialBarBase>((bar, _) => bar.UpdateMaxItemCount());
        OverflowContentProperty.Changed.AddClassHandler<MaterialBarBase>((bar, _) => bar.UpdateOverflowState());
        OrientationProperty.Changed.AddClassHandler<MaterialBarBase>((bar, _) => bar.UpdateOrientation());
    }

    /// <summary>Main-axis direction used by the adaptive action layout.</summary>
    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<MaterialBarBase, Orientation>(
            nameof(Orientation), Orientation.Horizontal);

    /// <summary>The actions in declaration order. Mutations are observed immediately.</summary>
    public IList<MaterialBarAction> Actions => _actions;

    public int MaxItemCount
    {
        get => GetValue(MaxItemCountProperty);
        set => SetValue(MaxItemCountProperty, value);
    }

    public object? OverflowContent
    {
        get => GetValue(OverflowContentProperty);
        set => SetValue(OverflowContentProperty, value);
    }

    public PlacementMode OverflowPlacement
    {
        get => GetValue(OverflowPlacementProperty);
        set => SetValue(OverflowPlacementProperty, value);
    }

    /// <summary>Layout direction of the action row/column.</summary>
    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>True when at least one action is hidden or <see cref="OverflowContent"/> is set.</summary>
    public bool HasOverflow
    {
        get => _hasOverflow;
        private set
        {
            if (SetAndRaise(HasOverflowProperty, ref _hasOverflow, value))
                _menuState.Dismiss();
        }
    }

    /// <summary>Number of actions currently laid out inline; set by the adaptive panel.</summary>
    public int VisibleActionCount
    {
        get => _visibleActionCount;
        private set => SetAndRaise(VisibleActionCountProperty, ref _visibleActionCount, value);
    }

    /// <summary>Opens the overflow flyout on the template's overflow button.</summary>
    public void ShowOverflow()
    {
        if (_overflowButton is null || !_overflowButton.IsAttachedToVisualTree() || !HasOverflow)
            return;

        CloseOverflow(restoreFocus: false);

        var panel = new StackPanel { Spacing = 2, MinWidth = 200 };
        for (var i = VisibleActionCount; i < _actions.Count; i++)
            AddOverflowAction(panel, _actions[i]);
        panel.KeyDown += (_, args) => OnOverflowKeyDown(panel, args);

        if (OverflowContent is { } content)
        {
            panel.Children.Add(new ContentPresenter
            {
                Content = content,
                Margin = new Thickness(12, 8, 12, 4)
            });
        }

        _menuState.Show();
        var flyout = new Flyout
        {
            Content = panel,
            Placement = GetOverflowPlacement(),
            OverlayDismissEventPassThrough = false
        };
        if (this.TryFindResource("MaterialBarOverflowPresenter", out var presenter) && presenter is ControlTheme theme)
            flyout.FlyoutPresenterTheme = theme;

        _overflowFlyout = flyout;
        flyout.Closed += OnOverflowClosed;
        flyout.ShowAt(_overflowButton);
        // The popup root takes focus when it opens; move it to the first command so the menu is
        // reachable without another Tab.
        Dispatcher.UIThread.Post(() =>
        {
            if (flyout.IsOpen && panel.Children.Count > 0)
                panel.Children.OfType<Button>().FirstOrDefault()?.Focus(NavigationMethod.Directional);
        }, DispatcherPriority.Loaded);
    }

    /// <summary>Closes the overflow flyout and restores focus to the overflow button.</summary>
    public void HideOverflow() => CloseOverflow(restoreFocus: true);

    /// <summary>The menu state handed to custom overflow content.</summary>
    public MaterialAppBarMenuState MenuState => _menuState;

    protected virtual PlacementMode GetOverflowPlacement() => OverflowPlacement;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_overflowButton is not null)
            _overflowButton.Click -= OnOverflowButtonClick;

        _actionsPanel = e.NameScope.Find<MaterialBarActionsPanel>("PART_ActionsPanel");
        _overflowButton = e.NameScope.Find<Button>("PART_OverflowButton");
        if (_overflowButton is not null)
            _overflowButton.Click += OnOverflowButtonClick;
        if (_actionsPanel is not null)
        {
            _actionsPanel.OverflowStateChanged = OnPanelOverflowChanged;
            _actionsPanel.Orientation = Orientation;
            _actionsPanel.MaxItemCount = MaxItemCount;
        }

        RebuildActionControls();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        CloseOverflow(restoreFocus: false);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
            return;

        if (e.Key == Key.Escape && _overflowFlyout is { IsOpen: true })
        {
            HideOverflow();
            e.Handled = true;
            return;
        }

        if (e.KeyModifiers != KeyModifiers.None)
            return;
        if (e.Key is not (Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End))
            return;

        var buttons = FocusableButtons();
        if (buttons.Count == 0)
            return;

        var source = e.Source as Visual;
        var index = -1;
        for (var i = 0; i < buttons.Count; i++)
        {
            if (ReferenceEquals(buttons[i], source) || (source is not null && buttons[i].IsVisualAncestorOf(source)))
            {
                index = i;
                break;
            }
        }
        if (index < 0)
            return;

        int next;
        if (e.Key == Key.Home)
            next = 0;
        else if (e.Key == Key.End)
            next = buttons.Count - 1;
        else
        {
            var forward = e.Key is Key.Right or Key.Down;
            next = (index + (forward ? 1 : -1) + buttons.Count) % buttons.Count;
        }

        buttons[next].Focus(NavigationMethod.Directional);
        e.Handled = true;
    }

    private void OnOverflowClosed(object? sender, EventArgs e)
    {
        _menuState.Dismiss();
        if (sender is Flyout flyout)
            flyout.Closed -= OnOverflowClosed;
    }

    private void OnOverflowKeyDown(Panel panel, KeyEventArgs e)
    {
        if (e.Handled)
            return;

        if (e.Key == Key.Escape)
        {
            CloseOverflow(restoreFocus: true);
            e.Handled = true;
            return;
        }

        if (e.KeyModifiers != KeyModifiers.None)
            return;

        var items = new List<Button>();
        foreach (var child in panel.Children)
        {
            if (child is Button { IsEffectivelyEnabled: true, IsEffectivelyVisible: true } button)
                items.Add(button);
        }
        if (items.Count == 0)
            return;

        var source = e.Source as Visual;
        var index = -1;
        for (var i = 0; i < items.Count; i++)
        {
            if (ReferenceEquals(items[i], source) || (source is not null && items[i].IsVisualAncestorOf(source)))
            {
                index = i;
                break;
            }
        }
        if (index < 0)
            return;

        var next = e.Key switch
        {
            Key.Home => 0,
            Key.End => items.Count - 1,
            Key.Down or Key.Right => (index + 1) % items.Count,
            Key.Up or Key.Left => (index - 1 + items.Count) % items.Count,
            _ => -1
        };
        if (next < 0)
            return;

        items[next].Focus(NavigationMethod.Directional);
        e.Handled = true;
    }

    private void OnOverflowButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_overflowFlyout is { IsOpen: true })
            HideOverflow();
        else
            ShowOverflow();
    }

    private void CloseOverflow(bool restoreFocus)
    {
        var flyout = _overflowFlyout;
        _overflowFlyout = null;
        if (flyout is null)
            return;
        flyout.Closed -= OnOverflowClosed;
        flyout.Hide();
        _menuState.Dismiss();
        if (!restoreFocus)
            return;
        if (_overflowButton is { IsEffectivelyEnabled: true, IsEffectivelyVisible: true } button && button.IsAttachedToVisualTree())
            button.Focus(NavigationMethod.Directional);
    }

    private void UpdateMaxItemCount()
    {
        if (_actionsPanel is not null)
        {
            _actionsPanel.MaxItemCount = MaxItemCount;
            _actionsPanel.InvalidateMeasure();
        }
    }

    private void UpdateOrientation()
    {
        if (_actionsPanel is not null)
        {
            _actionsPanel.Orientation = Orientation;
            _actionsPanel.InvalidateMeasure();
        }
    }

    private void UpdateOverflowState()
    {
        HasOverflow = _visibleActionCount < _actions.Count || OverflowContent is not null;
    }

    private void OnPanelOverflowChanged(int total, int visible)
    {
        VisibleActionCount = visible;
        HasOverflow = visible < total || OverflowContent is not null;
        EnsureFocusNotHidden();
    }

    private void EnsureFocusNotHidden()
    {
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual;
        if (focused is null)
            return;
        for (var i = VisibleActionCount; i < _actionControls.Count; i++)
        {
            var control = _actionControls[i];
            if (ReferenceEquals(control, focused) || control.IsVisualAncestorOf(focused))
            {
                if (_overflowButton is { IsEffectivelyVisible: true } button)
                    button.Focus(NavigationMethod.Directional);
                else if (_actionControls.Count > 0)
                    _actionControls[0].Focus(NavigationMethod.Directional);
                return;
            }
        }
    }

    private void RebuildActionControls()
    {
        foreach (var control in _actionControls)
            control.PropertyChanged -= OnActionControlPropertyChanged;
        _actionControls.Clear();

        for (var i = 0; i < _actions.Count; i++)
        {
            var control = CreateActionControl(_actions[i]);
            control.PropertyChanged += OnActionControlPropertyChanged;
            _actionControls.Add(control);
        }

        _actionsPanel?.SetActionControls(_actionControls);
        UpdateOverflowState();
    }

    private void OnActionControlPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        // A custom item's requested size can change at any time; let the panel re-measure.
        if (e.Property == BoundsProperty || e.Property == DesiredSizeProperty)
            return;
        _actionsPanel?.InvalidateMeasure();
    }

    private Control CreateActionControl(MaterialBarAction action)
    {
        Control control;
        switch (action.Kind)
        {
            case MaterialBarActionKind.Toggleable:
                var toggle = new ToggleButton
                {
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(0),
                    MinWidth = 48,
                    MinHeight = 48
                };
                MaterialAssist.SetIsIconButton(toggle, true);
                toggle.Bind(ToggleButton.IsCheckedProperty,
                    new Binding(nameof(MaterialBarAction.IsChecked)) { Source = action, Mode = BindingMode.TwoWay });
                toggle.Bind(Button.CommandProperty,
                    new Binding(nameof(MaterialBarAction.Command)) { Source = action });
                toggle.Bind(Button.CommandParameterProperty,
                    new Binding(nameof(MaterialBarAction.CommandParameter)) { Source = action });
                toggle.Bind(IsEnabledProperty,
                    new Binding(nameof(MaterialBarAction.IsEnabled)) { Source = action });
                toggle.Bind(AutomationProperties.NameProperty,
                    new Binding(nameof(MaterialBarAction.EffectiveAutomationName)) { Source = action });
                toggle.Content = CreateActionGlyph(action);
                control = toggle;
                break;

            case MaterialBarActionKind.Custom:
                control = new ContentPresenter
                {
                    Content = action.Content,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    HorizontalContentAlignment = HorizontalAlignment.Center
                };
                break;

            default:
                var button = new Button
                {
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(0),
                    MinWidth = 48,
                    MinHeight = 48
                };
                MaterialAssist.SetIsIconButton(button, true);
                button.Bind(Button.CommandProperty,
                    new Binding(nameof(MaterialBarAction.Command)) { Source = action });
                button.Bind(Button.CommandParameterProperty,
                    new Binding(nameof(MaterialBarAction.CommandParameter)) { Source = action });
                button.Bind(IsEnabledProperty,
                    new Binding(nameof(MaterialBarAction.IsEnabled)) { Source = action });
                button.Bind(AutomationProperties.NameProperty,
                    new Binding(nameof(MaterialBarAction.EffectiveAutomationName)) { Source = action });
                button.Content = CreateActionGlyph(action);
                control = button;
                break;
        }

        ToolTip.SetTip(control, action.ToolTip ?? action.Text);
        control.PropertyChanged += (_, _) => { };
        return control;
    }

    private static Control? CreateActionGlyph(MaterialBarAction action)
    {
        if (action.Icon is { } icon)
            return new PathIcon { Data = icon };
        if (!string.IsNullOrEmpty(action.Text))
            return new TextBlock { Text = action.Text, VerticalAlignment = VerticalAlignment.Center };
        return null;
    }

    private void AddOverflowAction(StackPanel panel, MaterialBarAction action)
    {
        if (action.Kind == MaterialBarActionKind.Custom)
        {
            var customContent = action.MenuContentFactory?.Invoke(_menuState) ?? action.MenuContent;
            if (customContent is not null)
            {
                panel.Children.Add(new ContentPresenter
                {
                    Content = customContent,
                    Margin = new Thickness(12, 8, 12, 4)
                });
            }
            return;
        }

        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (action.Icon is not null)
            content.Children.Add(new PathIcon { Data = action.Icon, Width = 20, Height = 20 });
        if (!string.IsNullOrEmpty(action.Text))
            content.Children.Add(new TextBlock { Text = action.Text, VerticalAlignment = VerticalAlignment.Center });

        var button = new Button
        {
            Content = content,
            Command = action.Command,
            CommandParameter = action.CommandParameter,
            IsEnabled = action.IsEnabled,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            MinHeight = 48,
            Padding = new Thickness(12, 0),
            Background = Brushes.Transparent
        };
        if (action.ToolTip is { } tip)
            ToolTip.SetTip(button, tip);
        // Text treatment keeps the neutral label color while reusing the shared state layer,
        // focus ring and disabled composition from the button theme.
        MaterialAssist.SetButtonVariant(button, MaterialButtonVariant.Text);
        AutomationProperties.SetName(button, action.EffectiveAutomationName ?? action.Text ?? string.Empty);
        button.Click += (_, _) =>
        {
            // Toggleable menu items flip the shared state before the command runs, matching
            // AppBarDsl's ToggleableAppBarItem.onCheckedChange(!checked).
            if (action.Kind == MaterialBarActionKind.Toggleable)
                action.IsChecked = !action.IsChecked;
            CloseOverflow(restoreFocus: true);
        };
        panel.Children.Add(button);
    }

    private List<Control> FocusableButtons()
    {
        var buttons = new List<Control>();
        foreach (var control in _actionControls)
        {
            if (control.Focusable && control.IsEffectivelyEnabled && control.IsEffectivelyVisible)
                buttons.Add(control);
        }
        if (_overflowButton is { Focusable: true, IsEffectivelyEnabled: true, IsEffectivelyVisible: true } overflow)
            buttons.Add(overflow);
        return buttons;
    }
}

/// <summary>How free space between inline actions is distributed, mirroring Arrangement.Horizontal.</summary>
public enum MaterialBarArrangement
{
    Start,
    Center,
    End,
    SpaceBetween,
    SpaceAround,
    SpaceEvenly,
    /// <summary>Upstream <c>spacedBy(32, CenterHorizontally)</c> used by the flexible bottom bar.</summary>
    Fixed
}

/// <summary>
/// Adaptive action layout ported from <c>OverflowMeasurePolicy</c> and the AppBarRow/AppBarColumn
/// layout callbacks.
/// </summary>
/// <remarks>
/// The panel owns the inline action controls and exactly one overflow indicator control. It
/// measures every action in order, reserves the indicator's main-axis intrinsic size, allows the
/// final action to borrow the reserved space (upstream's <c>isLastContent</c> rule), and arranges
/// only the items that fit. Hidden items keep their measured desired size for the next pass but
/// are arranged into an empty rectangle; their focus, hit testing and accessibility content view
/// are suppressed.
/// </remarks>
public sealed class MaterialBarActionsPanel : Panel
{
    /// <summary>Main-axis direction.</summary>
    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<MaterialBarActionsPanel, Orientation>(
            nameof(Orientation), Orientation.Horizontal);

    /// <summary>Upstream <c>maxItemCount</c>. Long lists render at most this many minus one items.</summary>
    public static readonly StyledProperty<int> MaxItemCountProperty =
        AvaloniaProperty.Register<MaterialBarActionsPanel, int>(
            nameof(MaxItemCount), int.MaxValue, validate: value => value >= 0);

    /// <summary>The overflow indicator; it is added at the end of the visual children.</summary>
    public static readonly StyledProperty<Control?> OverflowIndicatorProperty =
        AvaloniaProperty.Register<MaterialBarActionsPanel, Control?>(nameof(OverflowIndicator));

    /// <summary>Distribution of free main-axis space between inline actions.</summary>
    public static readonly StyledProperty<MaterialBarArrangement> ArrangementProperty =
        AvaloniaProperty.Register<MaterialBarActionsPanel, MaterialBarArrangement>(nameof(Arrangement));

    private const double FixedSpacing = 32;

    private readonly List<Control> _actions = new();
    private Control? _overflowIndicator;
    private int _visibleCount;
    private bool _overflowVisible;

    static MaterialBarActionsPanel()
    {
        OverflowIndicatorProperty.Changed.AddClassHandler<MaterialBarActionsPanel>((panel, args) =>
            panel.OnOverflowIndicatorChanged(args.GetOldValue<Control?>(), args.GetNewValue<Control?>()));
        OrientationProperty.Changed.AddClassHandler<MaterialBarActionsPanel>((panel, _) => panel.InvalidateMeasure());
        MaxItemCountProperty.Changed.AddClassHandler<MaterialBarActionsPanel>((panel, _) => panel.InvalidateMeasure());
        ArrangementProperty.Changed.AddClassHandler<MaterialBarActionsPanel>((panel, _) => panel.InvalidateArrange());
    }

    /// <summary>Called with (total action count, inline count) after every measure.</summary>
    public Action<int, int>? OverflowStateChanged { get; set; }

    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public int MaxItemCount
    {
        get => GetValue(MaxItemCountProperty);
        set => SetValue(MaxItemCountProperty, value);
    }

    public Control? OverflowIndicator
    {
        get => GetValue(OverflowIndicatorProperty);
        set => SetValue(OverflowIndicatorProperty, value);
    }

    public MaterialBarArrangement Arrangement
    {
        get => GetValue(ArrangementProperty);
        set => SetValue(ArrangementProperty, value);
    }

    /// <summary>Replaces the inline action controls.</summary>
    public void SetActionControls(IEnumerable<Control> controls)
    {
        CaptureOverflowIndicator();
        foreach (var control in _actions)
        {
            Children.Remove(control);
            control.PropertyChanged -= OnActionPropertyChanged;
        }
        _actions.Clear();
        foreach (var control in controls)
        {
            _actions.Add(control);
            control.PropertyChanged += OnActionPropertyChanged;
        }
        RebuildChildren();
        InvalidateMeasure();
    }

    private void CaptureOverflowIndicator()
    {
        if (_overflowIndicator is not null)
            return;
        foreach (var child in Children)
        {
            if (_actions.Contains(child))
                continue;
            _overflowIndicator = child;
            return;
        }
    }

    private void OnActionPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == IsVisibleProperty || e.Property == DesiredSizeProperty)
            InvalidateMeasure();
    }

    private void OnOverflowIndicatorChanged(Control? oldValue, Control? newValue)
    {
        if (oldValue is not null)
            Children.Remove(oldValue);
        _overflowIndicator = newValue;
        if (newValue is not null && !Children.Contains(newValue))
            Children.Add(newValue);
        InvalidateMeasure();
    }

    private void RebuildChildren()
    {
        Children.Clear();
        foreach (var control in _actions)
            Children.Add(control);
        if (_overflowIndicator is not null)
            Children.Add(_overflowIndicator);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        CaptureOverflowIndicator();
        var vertical = Orientation == Orientation.Vertical;
        var availableMain = vertical ? availableSize.Height : availableSize.Width;
        var availableCross = vertical ? availableSize.Width : availableSize.Height;
        var loose = double.IsInfinity(availableMain) && double.IsInfinity(availableCross)
            ? new Size(double.PositiveInfinity, double.PositiveInfinity)
            : new Size(
                vertical ? availableCross : availableMain,
                vertical ? availableMain : availableCross);

        // Reserve the overflow indicator's main-axis size before fitting content.
        double reserved = 0;
        if (_overflowIndicator is not null)
        {
            _overflowIndicator.Measure(loose);
            reserved = MainSize(_overflowIndicator.DesiredSize, vertical);
        }

        var remaining = availableMain;
        if (!double.IsInfinity(remaining))
            remaining = Math.Max(0, remaining - reserved);

        var visible = 0;
        double used = 0;
        var cross = 0d;
        var needsOverflow = false;
        for (var i = 0; i < _actions.Count; i++)
        {
            var child = _actions[i];
            child.Measure(loose);
            var isLast = i == _actions.Count - 1;
            if (!isLast && i == MaxItemCount - 1)
            {
                needsOverflow = true;
                break;
            }

            var childMain = MainSize(child.DesiredSize, vertical);
            var fits = childMain <= remaining ||
                       (isLast && childMain <= remaining + reserved);
            if (!fits)
            {
                needsOverflow = true;
                break;
            }

            visible++;
            used += childMain;
            cross = Math.Max(cross, CrossSize(child.DesiredSize, vertical));
            remaining = Math.Max(0, remaining - childMain);
        }

        if (visible != _actions.Count)
            needsOverflow = true;

        double overflowMain = 0;
        if (needsOverflow && _overflowIndicator is not null)
        {
            var overflowConstraint = remaining + reserved;
            var constrained = vertical
                ? new Size(availableCross, overflowConstraint)
                : new Size(overflowConstraint, availableCross);
            _overflowIndicator.Measure(constrained);
            overflowMain = MainSize(_overflowIndicator.DesiredSize, vertical);
            cross = Math.Max(cross, CrossSize(_overflowIndicator.DesiredSize, vertical));
        }

        _visibleCount = visible;
        _overflowVisible = needsOverflow;
        OverflowStateChanged?.Invoke(_actions.Count, visible);
        ApplyChildVisibility();

        var main = Math.Min(double.IsInfinity(availableMain) ? used + overflowMain : availableMain,
            used + overflowMain);
        var desiredCross = double.IsInfinity(availableCross) ? cross : Math.Min(availableCross, cross);
        return vertical ? new Size(desiredCross, main) : new Size(main, desiredCross);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var vertical = Orientation == Orientation.Vertical;
        var finalMain = vertical ? finalSize.Height : finalSize.Width;

        // Collect the controls that actually participate: visible actions then the overflow.
        var items = new List<Control>(_visibleCount + 1);
        for (var i = 0; i < _visibleCount && i < _actions.Count; i++)
            items.Add(_actions[i]);
        if (_overflowVisible && _overflowIndicator is not null)
            items.Add(_overflowIndicator);

        var sizes = new double[items.Count];
        var total = 0d;
        for (var i = 0; i < items.Count; i++)
        {
            sizes[i] = MainSize(items[i].DesiredSize, vertical);
            total += sizes[i];
        }

        var extra = Math.Max(0, finalMain - total);
        double offset;
        double gap;
        switch (Arrangement)
        {
            case MaterialBarArrangement.Center:
                offset = extra / 2;
                gap = 0;
                break;
            case MaterialBarArrangement.End:
                offset = extra;
                gap = 0;
                break;
            case MaterialBarArrangement.SpaceBetween:
                offset = 0;
                gap = items.Count > 1 ? extra / (items.Count - 1) : 0;
                break;
            case MaterialBarArrangement.SpaceAround:
                gap = items.Count > 0 ? extra / items.Count : 0;
                offset = gap / 2;
                break;
            case MaterialBarArrangement.SpaceEvenly:
                gap = extra / (items.Count + 1);
                offset = gap;
                break;
            case MaterialBarArrangement.Fixed:
                gap = FixedSpacing;
                offset = items.Count > 1
                    ? Math.Max(0, (extra - FixedSpacing * (items.Count - 1)) / 2)
                    : extra / 2;
                break;
            default:
                offset = 0;
                gap = 0;
                break;
        }

        var rtl = FlowDirection == FlowDirection.RightToLeft;
        for (var i = 0; i < items.Count; i++)
        {
            var size = items[i].DesiredSize;
            var main = sizes[i];
            var pos = offset;
            var rect = vertical
                ? new Rect(0, pos, finalSize.Width, main)
                : new Rect(pos, 0, main, finalSize.Height);
            if (!vertical && rtl)
                rect = new Rect(finalSize.Width - pos - main, 0, main, finalSize.Height);
            items[i].Arrange(rect);
            offset += main + gap;
        }

        // Actions and overflow that did not participate are collapsed out of the way.
        for (var i = _visibleCount; i < _actions.Count; i++)
            _actions[i].Arrange(default);
        if (_overflowIndicator is not null && !(_overflowVisible && items.Contains(_overflowIndicator)))
            _overflowIndicator.Arrange(default);

        return finalSize;
    }

    private void ApplyChildVisibility()
    {
        for (var i = 0; i < _actions.Count; i++)
        {
            var control = _actions[i];
            var hidden = i >= _visibleCount;
            // Avalonia measures are invalidated by IsVisible changes, which would make hidden
            // children measure as zero on the next pass. Keep them measurable and hide their
            // interaction and accessibility instead; the overflow flyout exposes them.
            control.Opacity = hidden ? 0 : 1;
            control.IsHitTestVisible = !hidden;
            control.Focusable = !hidden;
            AutomationProperties.SetAccessibilityView(
                control, hidden ? AccessibilityView.Raw : AccessibilityView.Content);
        }
        if (_overflowIndicator is not null)
            _overflowIndicator.IsVisible = _overflowVisible;
    }

    private static double MainSize(Size size, bool vertical) => vertical ? size.Height : size.Width;
    private static double CrossSize(Size size, bool vertical) => vertical ? size.Width : size.Height;
}