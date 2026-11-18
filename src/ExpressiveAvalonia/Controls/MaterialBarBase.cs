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
    private readonly List<MaterialBarAction> _subscribedActions = new();
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
            nameof(Orientation), Orientation.Horizontal,
            coerce: static (o, value) => ((MaterialBarBase)o).CoerceOrientation(value));

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

    /// <summary>Allows derived bars (such as docked toolbars) to restrict allowed orientations.</summary>
    protected virtual Orientation CoerceOrientation(Orientation value) => value;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_subscribedActions.Count == 0 && _actions.Count > 0)
        {
            SubscribeActions();
            SyncActionControls();
        }
    }

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
        UnsubscribeActions();
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

        var horizontal = Orientation == Orientation.Horizontal;
        var isAxisArrow = horizontal
            ? e.Key is Key.Left or Key.Right
            : e.Key is Key.Up or Key.Down;
        if (!isAxisArrow && e.Key is not (Key.Home or Key.End))
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

        var rtl = FlowDirection == FlowDirection.RightToLeft;
        int next;
        if (e.Key == Key.Home)
            next = 0;
        else if (e.Key == Key.End)
            next = buttons.Count - 1;
        else
        {
            var forward = horizontal
                ? (rtl ? e.Key == Key.Left : e.Key == Key.Right)
                : e.Key == Key.Down;
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

    private static readonly Geometry DefaultCheckGeometry =
        Geometry.Parse("M9 16.17L4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41z");

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
                else if (FocusableButtons() is { Count: > 0 } buttons)
                    buttons[0].Focus(NavigationMethod.Directional);
                return;
            }
        }
    }

    private void SubscribeActions()
    {
        UnsubscribeActions();
        for (var i = 0; i < _actions.Count; i++)
        {
            var action = _actions[i];
            action.PropertyChanged += OnBarActionPropertyChanged;
            _subscribedActions.Add(action);
        }
    }

    private void UnsubscribeActions()
    {
        for (var i = 0; i < _subscribedActions.Count; i++)
            _subscribedActions[i].PropertyChanged -= OnBarActionPropertyChanged;
        _subscribedActions.Clear();
    }

    private void SyncActionControls()
    {
        if (_actionControls.Count != _actions.Count)
        {
            RebuildActionControls();
            return;
        }

        for (var i = 0; i < _actions.Count; i++)
        {
            var action = _actions[i];
            var control = _actionControls[i];
            var kindMatches = action.Kind switch
            {
                MaterialBarActionKind.Toggleable => control.GetType() == typeof(ToggleButton),
                MaterialBarActionKind.Custom => control is ContentPresenter,
                _ => control.GetType() == typeof(Button)
            };
            if (!kindMatches)
            {
                RebuildActionControls();
                return;
            }

            if (control is Button button && action.Kind != MaterialBarActionKind.Custom)
            {
                ApplyInlineButtonMode(button, action);
                button.Content = CreateActionGlyph(action);
            }
            ToolTip.SetTip(control, action.ToolTip ?? action.Text);
        }
    }

    private void RebuildActionControls()
    {
        SubscribeActions();

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

    private void OnBarActionPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is not MaterialBarAction action)
            return;

        if (e.Property == MaterialBarAction.KindProperty)
        {
            RebuildActionControls();
            return;
        }

        var index = _actions.IndexOf(action);
        if (index < 0 || index >= _actionControls.Count)
            return;

        var control = _actionControls[index];
        if (e.Property == MaterialBarAction.IconProperty || e.Property == MaterialBarAction.TextProperty)
        {
            if (control is Button button && action.Kind != MaterialBarActionKind.Custom)
            {
                ApplyInlineButtonMode(button, action);
                button.Content = CreateActionGlyph(action);
            }
            ToolTip.SetTip(control, action.ToolTip ?? action.Text);
            _actionsPanel?.InvalidateMeasure();
        }
        else if (e.Property == MaterialBarAction.ToolTipProperty)
        {
            ToolTip.SetTip(control, action.ToolTip ?? action.Text);
        }
    }

    private void OnActionControlPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        // A custom item's requested size can change at any time; let the panel re-measure.
        if (e.Property == BoundsProperty || e.Property == DesiredSizeProperty)
            return;
        _actionsPanel?.InvalidateMeasure();
    }

    private static void ApplyInlineButtonMode(Button button, MaterialBarAction action)
    {
        if (action.Icon is not null || string.IsNullOrEmpty(action.Text))
        {
            MaterialAssist.SetIsIconButton(button, true);
            button.ClearValue(MaterialAssist.ButtonVariantProperty);
        }
        else
        {
            MaterialAssist.SetIsIconButton(button, false);
            MaterialAssist.SetButtonVariant(button, MaterialButtonVariant.Text);
        }
    }

    private Control CreateActionControl(MaterialBarAction action)
    {
        Control control;
        switch (action.Kind)
        {
            case MaterialBarActionKind.Toggleable:
                var toggle = new ToggleButton
                {
                    MinWidth = 48,
                    MinHeight = 48
                };
                ApplyInlineButtonMode(toggle, action);
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
                var presenter = new ContentPresenter
                {
                    VerticalContentAlignment = VerticalAlignment.Center,
                    HorizontalContentAlignment = HorizontalAlignment.Center
                };
                presenter.Bind(ContentPresenter.ContentProperty,
                    new Binding(nameof(MaterialBarAction.Content)) { Source = action });
                control = presenter;
                break;

            default:
                var button = new Button
                {
                    MinWidth = 48,
                    MinHeight = 48
                };
                ApplyInlineButtonMode(button, action);
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

        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            VerticalAlignment = VerticalAlignment.Center
        };

        var iconTheme = this.TryFindResource("MaterialButtonPathIconTheme", out var iconThemeResource)
            && iconThemeResource is ControlTheme resolvedIconTheme
            ? resolvedIconTheme
            : null;

        if (action.Icon is not null)
        {
            var leadingIcon = new PathIcon
            {
                Data = action.Icon,
                Width = 24,
                Height = 24,
                Margin = new Thickness(0, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            leadingIcon.Classes.Add("leading-icon");
            if (iconTheme is not null)
                leadingIcon.Theme = iconTheme;
            Grid.SetColumn(leadingIcon, 0);
            row.Children.Add(leadingIcon);
        }

        var label = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        label.Bind(TextBlock.TextProperty, new Binding(nameof(MaterialBarAction.Text)) { Source = action });
        Grid.SetColumn(label, 1);
        row.Children.Add(label);

        Button item;
        if (action.Kind == MaterialBarActionKind.Toggleable)
        {
            var checkGeo = this.TryFindResource("MaterialBarCheckIcon", out var g) && g is Geometry geo
                ? geo
                : DefaultCheckGeometry;
            var checkIcon = new PathIcon
            {
                Data = checkGeo,
                Width = 24,
                Height = 24,
                Margin = new Thickness(12, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            checkIcon.Classes.Add("trailing-check");
            if (iconTheme is not null)
                checkIcon.Theme = iconTheme;
            checkIcon.Bind(IsVisibleProperty, new Binding(nameof(MaterialBarAction.IsChecked)) { Source = action });
            Grid.SetColumn(checkIcon, 2);
            row.Children.Add(checkIcon);

            var toggle = new ToggleButton { Content = row };
            toggle.Bind(ToggleButton.IsCheckedProperty,
                new Binding(nameof(MaterialBarAction.IsChecked)) { Source = action, Mode = BindingMode.TwoWay });
            if (this.TryFindResource("MaterialBarOverflowToggleItemTheme", out var toggleThemeObj)
                && toggleThemeObj is ControlTheme toggleTheme)
            {
                toggle.Theme = toggleTheme;
            }
            item = toggle;
        }
        else
        {
            item = new Button { Content = row };
            if (this.TryFindResource("MaterialBarOverflowItemTheme", out var itemThemeObj)
                && itemThemeObj is ControlTheme itemTheme)
            {
                item.Theme = itemTheme;
            }
        }

        item.Bind(Button.CommandProperty, new Binding(nameof(MaterialBarAction.Command)) { Source = action });
        item.Bind(Button.CommandParameterProperty,
            new Binding(nameof(MaterialBarAction.CommandParameter)) { Source = action });
        item.Bind(IsEnabledProperty, new Binding(nameof(MaterialBarAction.IsEnabled)) { Source = action });
        item.Bind(AutomationProperties.NameProperty,
            new Binding(nameof(MaterialBarAction.EffectiveAutomationName)) { Source = action });
        if (action.ToolTip is { } tip)
            ToolTip.SetTip(item, tip);

        item.Click += (_, _) => CloseOverflow(restoreFocus: true);
        panel.Children.Add(item);
    }

    private List<Control> FocusableButtons()
    {
        var buttons = new List<Control>();
        var limit = _actionsPanel is not null
            ? Math.Min(_actionControls.Count, _visibleActionCount)
            : _actionControls.Count;
        for (var i = 0; i < limit; i++)
        {
            var control = _actionControls[i];
            if (!control.IsEffectivelyVisible || !control.IsEffectivelyEnabled || !control.IsHitTestVisible)
                continue;

            if (control.Focusable)
            {
                buttons.Add(control);
                continue;
            }

            Control? currentRoot = null;
            foreach (var descendant in control.GetVisualDescendants())
            {
                if (descendant is not Control candidate)
                    continue;
                if (currentRoot is not null && currentRoot.IsVisualAncestorOf(candidate))
                    continue;
                if (candidate is { Focusable: true, IsEffectivelyEnabled: true, IsEffectivelyVisible: true, IsHitTestVisible: true })
                {
                    buttons.Add(candidate);
                    currentRoot = candidate;
                }
            }
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
    /// <summary>Upstream <c>spacedBy(32, CenterHorizontally)</c> used by the docked toolbar.</summary>
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

    /// <summary>Minimum main-axis gap between inline items before overflowing.</summary>
    public static readonly StyledProperty<double> MinItemSpacingProperty =
        AvaloniaProperty.Register<MaterialBarActionsPanel, double>(
            nameof(MinItemSpacing), 0d, validate: value => double.IsFinite(value) && value >= 0);

    /// <summary>Maximum main-axis gap between inline items for <see cref="MaterialBarArrangement.Fixed"/>.</summary>
    public static readonly StyledProperty<double> MaxItemSpacingProperty =
        AvaloniaProperty.Register<MaterialBarActionsPanel, double>(
            nameof(MaxItemSpacing), FixedSpacing, validate: value => double.IsFinite(value) && value >= 0);

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
        MinItemSpacingProperty.Changed.AddClassHandler<MaterialBarActionsPanel>((panel, _) => panel.InvalidateMeasure());
        MaxItemSpacingProperty.Changed.AddClassHandler<MaterialBarActionsPanel>((panel, _) => panel.InvalidateArrange());
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

    public double MinItemSpacing
    {
        get => GetValue(MinItemSpacingProperty);
        set => SetValue(MinItemSpacingProperty, value);
    }

    public double MaxItemSpacing
    {
        get => GetValue(MaxItemSpacingProperty);
        set => SetValue(MaxItemSpacingProperty, value);
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
        var minGap = Math.Max(0, MinItemSpacing);
        var itemConstraint = vertical
            ? new Size(availableCross, double.PositiveInfinity)
            : new Size(double.PositiveInfinity, availableCross);

        double reserved = 0;
        if (_overflowIndicator is not null)
        {
            _overflowIndicator.Measure(itemConstraint);
            reserved = MainSize(_overflowIndicator.DesiredSize, vertical);
        }

        var count = _actions.Count;
        var actionMains = new double[count];
        var actionCrosses = new double[count];
        var allMain = 0d;
        var allCross = 0d;
        for (var i = 0; i < count; i++)
        {
            var child = _actions[i];
            child.Measure(itemConstraint);
            actionMains[i] = MainSize(child.DesiredSize, vertical);
            actionCrosses[i] = CrossSize(child.DesiredSize, vertical);
            allMain += actionMains[i] + (i > 0 ? minGap : 0);
            allCross = Math.Max(allCross, actionCrosses[i]);
        }

        int visible;
        double used;
        double cross;
        bool needsOverflow;

        if (count == 0)
        {
            visible = 0;
            used = 0;
            cross = 0;
            needsOverflow = false;
        }
        else if (count <= MaxItemCount && allMain <= availableMain + 1e-4)
        {
            visible = count;
            used = allMain;
            cross = allCross;
            needsOverflow = false;
        }
        else
        {
            needsOverflow = true;
            visible = 0;
            used = 0;
            cross = 0;
            var maxInline = MaxItemCount == 0 ? 0 : Math.Min(count - 1, MaxItemCount - 1);
            var overflowReserve = _overflowIndicator is not null ? minGap + reserved : 0;
            for (var i = 0; i < maxInline; i++)
            {
                var gapBefore = visible > 0 ? minGap : 0;
                if (used + gapBefore + actionMains[i] + overflowReserve > availableMain + 1e-4)
                    break;

                used += gapBefore + actionMains[i];
                cross = Math.Max(cross, actionCrosses[i]);
                visible++;
            }
        }

        double overflowMain = 0;
        if (needsOverflow && _overflowIndicator is not null)
        {
            var overflowGap = visible > 0 ? minGap : 0;
            var overflowConstraint = double.IsInfinity(availableMain)
                ? double.PositiveInfinity
                : Math.Max(0, availableMain - used - overflowGap);
            var constrained = vertical
                ? new Size(availableCross, overflowConstraint)
                : new Size(overflowConstraint, availableCross);
            _overflowIndicator.Measure(constrained);
            overflowMain = overflowGap + MainSize(_overflowIndicator.DesiredSize, vertical);
            cross = Math.Max(cross, CrossSize(_overflowIndicator.DesiredSize, vertical));
        }

        _visibleCount = visible;
        _overflowVisible = needsOverflow;
        OverflowStateChanged?.Invoke(count, visible);
        ApplyChildVisibility();

        var desiredMain = used + overflowMain;
        var main = double.IsInfinity(availableMain) ? desiredMain : Math.Min(availableMain, desiredMain);
        var desiredCross = double.IsInfinity(availableCross) ? cross : Math.Min(availableCross, cross);
        return vertical ? new Size(desiredCross, main) : new Size(main, desiredCross);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var vertical = Orientation == Orientation.Vertical;
        var finalMain = vertical ? finalSize.Height : finalSize.Width;
        var minGap = Math.Max(0, MinItemSpacing);
        var maxGap = Math.Max(minGap, MaxItemSpacing);

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

        var gaps = Math.Max(0, items.Count - 1);
        var minTotal = total + minGap * gaps;
        var extra = Math.Max(0, finalMain - minTotal);
        double offset;
        double gap;
        switch (Arrangement)
        {
            case MaterialBarArrangement.Center:
                offset = extra / 2;
                gap = minGap;
                break;
            case MaterialBarArrangement.End:
                offset = extra;
                gap = minGap;
                break;
            case MaterialBarArrangement.SpaceBetween:
                offset = 0;
                gap = gaps > 0 ? minGap + extra / gaps : 0;
                break;
            case MaterialBarArrangement.SpaceAround:
                gap = items.Count > 0 ? Math.Max(minGap, Math.Max(0, finalMain - total) / items.Count) : 0;
                offset = Math.Max(0, finalMain - (total + gap * gaps)) / 2;
                break;
            case MaterialBarArrangement.SpaceEvenly:
                gap = Math.Max(minGap, Math.Max(0, finalMain - total) / (items.Count + 1));
                offset = Math.Max(0, finalMain - (total + gap * gaps)) / 2;
                break;
            case MaterialBarArrangement.Fixed:
                gap = gaps > 0 ? Math.Clamp((finalMain - total) / gaps, minGap, maxGap) : 0;
                offset = Math.Max(0, (finalMain - (total + gap * gaps)) / 2);
                break;
            default:
                offset = 0;
                gap = minGap;
                break;
        }

        // Avalonia mirrors the X-coordinate plane in RTL, so logical 0..finalSize.Width places
        // inline actions from start (physical right) to end (physical left) without manual flipping.
        for (var i = 0; i < items.Count; i++)
        {
            var main = sizes[i];
            var pos = offset;
            var rect = vertical
                ? new Rect(0, pos, finalSize.Width, main)
                : new Rect(pos, 0, main, finalSize.Height);
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
            if (control is not ContentPresenter)
                control.Focusable = !hidden;
            KeyboardNavigation.SetTabNavigation(
                control, hidden ? KeyboardNavigationMode.None : KeyboardNavigationMode.Continue);
            AutomationProperties.SetAccessibilityView(
                control, hidden ? AccessibilityView.Raw : AccessibilityView.Content);
        }
        if (_overflowIndicator is not null)
            _overflowIndicator.IsVisible = _overflowVisible;
    }

    private static double MainSize(Size size, bool vertical) => vertical ? size.Height : size.Width;
    private static double CrossSize(Size size, bool vertical) => vertical ? size.Width : size.Height;
}